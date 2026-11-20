"""Intake step 3: 360 video -> flat perspective views.

A 360 camera (after export) gives an equirectangular video: each frame is a
2:1 image where x is the direction around you (longitude) and y is up/down
(latitude). Alignment tools want normal flat photos, so each chosen frame is
cut into several 90-degree views looking around the horizon (like the side
faces of a cube). The top (sky) and bottom (the person holding the camera)
faces are dropped. An optional second row tilts up to see roofs.

Frame spacing: one frame per SPACING_M (0.5 m) of walking. Speed comes from
the video's GPS track when ExifTool can read one, else 1 m/s is assumed
(= 2 frames per second).

Insta360 .insv files are two fisheye images, not equirectangular: export
them as 360 MP4 in Insta360 Studio first (intake reports them).
"""
import json
import math
import subprocess
from functools import lru_cache
from pathlib import Path

import cv2
import numpy as np

SPACING_M = 0.5
DEFAULT_SPEED = 1.0
FPS_MIN, FPS_MAX = 0.5, 6.0
FOV = 90.0


def probe(path):
    """Width, height, duration (s) and frame rate of a video (ffprobe)."""
    res = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
                          "stream=width,height,avg_frame_rate,codec_name:format=duration",
                          "-of", "json", str(path)], capture_output=True, timeout=120)
    if res.returncode != 0:
        raise RuntimeError("ffprobe failed: " + res.stderr.decode(errors="replace")[:300])
    data = json.loads(res.stdout or b"{}")
    st = (data.get("streams") or [{}])[0]
    num, _, den = (st.get("avg_frame_rate") or "0/1").partition("/")
    fps = float(num) / float(den or 1) if float(den or 1) else 0.0
    return {"width": int(st.get("width", 0)), "height": int(st.get("height", 0)),
            "duration": float((data.get("format") or {}).get("duration") or 0.0),
            "fps": round(fps, 3), "codec": st.get("codec_name")}


def is_equirect(info):
    return info["height"] > 0 and abs(info["width"] / info["height"] - 2.0) < 0.02


def gps_track(path, exe):
    """[(time_s, lat, lon)] from a video's embedded GPS (ExifTool -ee), or []."""
    try:
        res = subprocess.run([exe, "-ee", "-n", "-json", "-api", "largefilesupport=1", "-G3",
                              "-GPSLatitude", "-GPSLongitude", "-SampleTime", str(path)],
                             capture_output=True, timeout=600)
        rows = json.loads(res.stdout or b"[]")
    except (OSError, ValueError, subprocess.TimeoutExpired):
        return []
    if not rows:
        return []
    groups = {}
    for key, val in rows[0].items():
        doc, _, tag = key.partition(":")
        groups.setdefault(doc, {})[tag] = val
    track = []
    for doc, tags in groups.items():
        if "GPSLatitude" in tags and "GPSLongitude" in tags and "SampleTime" in tags:
            try:
                track.append((float(tags["SampleTime"]), float(tags["GPSLatitude"]), float(tags["GPSLongitude"])))
            except (TypeError, ValueError):
                pass
    return sorted(track)


def track_speed(track):
    """Median walking speed (m/s) along a GPS track, or None."""
    if len(track) < 3:
        return None
    from pyproj import Geod
    g = Geod(ellps="WGS84")
    speeds = []
    for (t0, la0, lo0), (t1, la1, lo1) in zip(track, track[1:]):
        if t1 - t0 > 0:
            speeds.append(g.inv(lo0, la0, lo1, la1)[2] / (t1 - t0))
    return float(np.median(speeds)) if speeds else None


def choose_fps(speed=None):
    """Frames per second for one frame per SPACING_M at the given speed."""
    s = speed if speed and speed > 0.05 else DEFAULT_SPEED
    return float(min(FPS_MAX, max(FPS_MIN, s / SPACING_M)))


def position_at(track, t):
    """(lat, lon) interpolated along a GPS track at time t, or None."""
    if not track:
        return None
    ts = np.array([p[0] for p in track])
    if t < ts[0] - 1 or t > ts[-1] + 1:
        return None
    lat = float(np.interp(t, ts, [p[1] for p in track]))
    lon = float(np.interp(t, ts, [p[2] for p in track]))
    return lat, lon


def extract_frames(video, out_dir, fps, max_seconds=None):
    """Write frames as JPEGs (quality ~95) with ffmpeg; returns [(file, time_s)]."""
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    cmd = ["ffmpeg", "-v", "error", "-y", "-i", str(video)]
    if max_seconds:
        cmd += ["-t", str(max_seconds)]
    cmd += ["-vf", f"fps={fps}", "-q:v", "2", str(out_dir / "f_%06d.jpg")]
    res = subprocess.run(cmd, capture_output=True, timeout=7200)
    if res.returncode != 0:
        raise RuntimeError("ffmpeg failed: " + res.stderr.decode(errors="replace")[:500])
    files = sorted(out_dir.glob("f_*.jpg"))
    # The fps filter's frame k (0-based) shows the video at about k / fps seconds.
    return [(f, round(k / fps, 3)) for k, f in enumerate(files)]


def view_list(n_horizontal=8, up_row=False, up_pitch=35.0):
    """(yaw, pitch) of every view. Yaw 0 = the middle of the equirectangular frame."""
    step = 360.0 / n_horizontal
    views = [(round(i * step, 2), 0.0) for i in range(n_horizontal)]
    if up_row:
        views += [(round(i * 90.0 + 45.0, 2), up_pitch) for i in range(4)]
    return views


@lru_cache(maxsize=64)
def ray_angles(size, pitch_deg, fov_deg, yaw_deg=0.0):
    """Longitude and latitude (radians) of the ray through every pixel of a view."""
    f = (size / 2) / math.tan(math.radians(fov_deg) / 2)
    u, v = np.meshgrid(np.arange(size, dtype=np.float32) + 0.5, np.arange(size, dtype=np.float32) + 0.5)
    x = (u - size / 2) / f          # right
    y = -(v - size / 2) / f         # up
    z = np.ones_like(x)             # forward
    p, yw = math.radians(pitch_deg), math.radians(yaw_deg)
    # Tilt up by the pitch (around the right axis), then turn by the yaw (around up).
    y2 = y * math.cos(p) + z * math.sin(p)
    z2 = -y * math.sin(p) + z * math.cos(p)
    x3 = x * math.cos(yw) + z2 * math.sin(yw)
    z3 = -x * math.sin(yw) + z2 * math.cos(yw)
    lon = np.arctan2(x3, z3)
    lat = np.arctan2(y2, np.hypot(x3, z3))
    return lon, lat


@lru_cache(maxsize=64)
def _maps(eq_w, eq_h, yaw, pitch, fov, size):
    lon, lat = ray_angles(size, pitch, fov, yaw)
    mx = ((lon / (2 * math.pi) + 0.5) * eq_w - 0.5).astype(np.float32)
    my = ((0.5 - lat / math.pi) * eq_h - 0.5).astype(np.float32)
    return mx, my


def view_size(eq_w, fov=FOV):
    """Pixels per view so the view keeps the video's own resolution."""
    return int(round(eq_w * fov / 360.0 / 16.0) * 16) or 16


def split_frame(eq, views, fov=FOV, size=None):
    """Cut one equirectangular frame into perspective views. Returns [(yaw, pitch, image)]."""
    h, w = eq.shape[:2]
    size = size or view_size(w, fov)
    out = []
    for yaw, pitch in views:
        mx, my = _maps(w, h, float(yaw), float(pitch), float(fov), int(size))
        img = cv2.remap(eq, mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_WRAP)
        out.append((yaw, pitch, img))
    return out
