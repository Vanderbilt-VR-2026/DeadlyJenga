"""Intake step 2a: read photo metadata (time, GPS, camera) and convert GPS to the campus frame.

Sources, in order of trust:
- a sidecar file written by the provider (KartaView sequence.json): GPS,
  heading and time per photo;
- the photo's own EXIF/XMP tags, read with ExifTool (-json -n gives numbers).

Headings are compass degrees (0 = north, clockwise). The campus frame uses
grid north, which is about half a degree off true north, so headings are
converted by projecting a short step along the heading (no sign rules to
remember).
"""
import datetime
import json
import math
import re
import subprocess
import tempfile
from pathlib import Path

import numpy as np

# Tags asked from ExifTool. "Composite:" tags are computed by ExifTool
# (for example GPSLatitude with its N/S sign already applied).
EXIF_TAGS = [
    "DateTimeOriginal", "SubSecTimeOriginal", "OffsetTimeOriginal", "CreateDate", "SubSecDateTimeOriginal",
    "Composite:GPSLatitude", "Composite:GPSLongitude", "GPSLatitudeRef", "GPSLongitudeRef",
    "GPSAltitude", "GPSAltitudeRef", "GPSImgDirection", "GPSImgDirectionRef", "GPSHPositioningError",
    "ExposureTime", "FNumber", "ISO", "FocalLength", "FocalLengthIn35mmFormat", "Composite:FocalLength35efl",
    "LensModel", "LensMake", "Make", "Model", "ImageWidth", "ImageHeight", "Orientation",
    "ProjectionType", "UsePanoramaViewer",
]
# Approximate magnetic declination at Nashville in 2026 (degrees, west negative).
# Only used when a photo says its heading is magnetic (rare; phones write true).
DECLINATION_DEG = -3.3


def run_exiftool(paths, exe):
    """Read EXIF_TAGS for many files in one ExifTool run. Returns {resolved path str: tags}."""
    paths = [str(Path(p).resolve()) for p in paths]
    if not paths:
        return {}
    # An argument file avoids the Windows command-line length limit.
    with tempfile.NamedTemporaryFile("w", suffix=".args", delete=False, encoding="utf-8") as f:
        for p in paths:
            f.write(p + "\n")
        argfile = f.name
    try:
        cmd = [exe, "-json", "-n", "-charset", "filename=utf8", "-api", "largefilesupport=1"]
        cmd += ["-" + t for t in EXIF_TAGS] + ["-@", argfile]
        res = subprocess.run(cmd, capture_output=True, timeout=3600)
    finally:
        Path(argfile).unlink(missing_ok=True)
    text = res.stdout.decode("utf-8", errors="replace").strip()
    if not text:
        if res.returncode != 0:
            raise RuntimeError("exiftool failed: " + res.stderr.decode("utf-8", errors="replace")[:500])
        return {}
    out = {}
    for row in json.loads(text):
        out[str(Path(row["SourceFile"]).resolve())] = row
    return out


def parse_exif_time(tags):
    """(iso string, seconds) from EXIF time tags, or (None, None).

    Without an offset tag the time is local camera time; seconds are then
    counted as if it were UTC, which is fine for comparing photos of one set."""
    raw = tags.get("SubSecDateTimeOriginal") or tags.get("DateTimeOriginal") or tags.get("CreateDate")
    if not raw or not isinstance(raw, str) or raw.startswith("0000"):
        return None, None
    s = raw.strip()
    try:
        main = s[:19]
        dt = datetime.datetime.strptime(main, "%Y:%m:%d %H:%M:%S")
    except ValueError:
        return None, None
    rest = s[19:]
    frac = 0.0
    if rest.startswith("."):
        digits = re.match(r"\d*", rest[1:]).group(0)       # leading digits only
        frac = float("0." + digits) if digits else 0.0
        rest = rest[1 + len(digits):]
    elif tags.get("SubSecTimeOriginal") not in (None, ""):
        try:
            frac = float("0." + str(tags["SubSecTimeOriginal"]).strip())
        except ValueError:
            frac = 0.0
    offset = rest.strip() or tags.get("OffsetTimeOriginal")
    tz = datetime.timezone.utc
    if isinstance(offset, str) and len(offset) >= 6 and offset[0] in "+-":
        sign = 1 if offset[0] == "+" else -1
        tz = datetime.timezone(sign * datetime.timedelta(hours=int(offset[1:3]), minutes=int(offset[4:6])))
    dt = dt.replace(tzinfo=tz)
    iso = (dt + datetime.timedelta(seconds=frac)).isoformat(timespec="milliseconds")
    if offset is None:
        iso = iso[:-6]                       # no known offset: do not pretend it is UTC
    return iso, dt.timestamp() + frac


def parse_sidecar_time(text):
    """'2020-09-03 22:02:54.000' -> (iso, seconds). KartaView times are UTC."""
    try:
        dt = datetime.datetime.strptime(text[:19], "%Y-%m-%d %H:%M:%S").replace(tzinfo=datetime.timezone.utc)
    except (TypeError, ValueError):
        return None, None
    frac = 0.0
    if len(text) > 20 and text[19] == ".":
        try:
            frac = float("0" + text[19:])
        except ValueError:
            pass
    return (dt + datetime.timedelta(seconds=frac)).isoformat(timespec="milliseconds"), dt.timestamp() + frac


def _num(v):
    try:
        f = float(v)
        return f if math.isfinite(f) else None
    except (TypeError, ValueError):
        return None


def exif_record(tags):
    """The metadata fields intake keeps, from one ExifTool row."""
    iso, t = parse_exif_time(tags)
    lat, lon = _num(tags.get("GPSLatitude")), _num(tags.get("GPSLongitude"))
    # Guard against files where the composite sign was not applied.
    if lat is not None and str(tags.get("GPSLatitudeRef", "")).upper().startswith("S") and lat > 0:
        lat = -lat
    if lon is not None and str(tags.get("GPSLongitudeRef", "")).upper().startswith("W") and lon > 0:
        lon = -lon
    gps = None
    if lat is not None and lon is not None and not (lat == 0 and lon == 0):
        alt = _num(tags.get("GPSAltitude"))
        if alt is not None and str(tags.get("GPSAltitudeRef", "0")) in ("1", "Below Sea Level"):
            alt = -alt
        heading = _num(tags.get("GPSImgDirection"))
        if heading is not None and str(tags.get("GPSImgDirectionRef", "T")).upper().startswith("M"):
            heading = (heading + DECLINATION_DEG) % 360.0
        gps = {"lat": lat, "lon": lon, "alt": alt, "heading": heading,
               "accuracy_m": _num(tags.get("GPSHPositioningError")), "source": "exif"}
    focal35 = _num(tags.get("FocalLengthIn35mmFormat")) or _num(tags.get("FocalLength35efl"))
    camera = {
        "make": tags.get("Make"), "model": tags.get("Model"),
        "lens": tags.get("LensModel") or tags.get("LensMake"),
        "focal_mm": _num(tags.get("FocalLength")), "focal_35mm": focal35,
        "exposure_s": _num(tags.get("ExposureTime")), "f_number": _num(tags.get("FNumber")),
        "iso": _num(tags.get("ISO")),
        "width": tags.get("ImageWidth"), "height": tags.get("ImageHeight"),
        "orientation": tags.get("Orientation", 1),
        "projection": tags.get("ProjectionType"),
    }
    return {"time": iso, "t": t, "gps": gps, "camera": camera}


def read_sidecars(folder):
    """Provider sidecars under folder: {photo path (resolved str): record}.

    Today this reads KartaView sequence.json files (a "photos" list with
    local_file, lat, lng, heading, shotDate)."""
    out = {}
    for side in sorted(Path(folder).rglob("sequence.json")):
        try:
            data = json.loads(side.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        for ph in data.get("photos") or []:
            local = ph.get("local_file")
            if not local:
                continue
            iso, t = parse_sidecar_time(ph.get("shotDate") or "")
            lat, lon = _num(ph.get("lat")), _num(ph.get("lng"))
            gps = None
            if lat is not None and lon is not None:
                gps = {"lat": lat, "lon": lon, "alt": None, "heading": _num(ph.get("heading")),
                       "accuracy_m": None, "source": "sidecar:" + side.name}
            out[str((side.parent / local).resolve())] = {
                "time": iso, "t": t, "gps": gps,
                "provider": {"id": ph.get("id"), "sequence": (data.get("sequence") or {}).get("id"),
                             "blur": ph.get("autoImgProcessingResult"), "projection": ph.get("projection"),
                             "width": _num(ph.get("width")), "height": _num(ph.get("height"))},
                "license": data.get("license"),
            }
    return out


def merge(exif, sidecar):
    """Sidecar values win for time and GPS; camera fields come from EXIF."""
    rec = dict(exif or {"time": None, "t": None, "gps": None, "camera": {}})
    if sidecar:
        if sidecar.get("time"):
            rec["time"], rec["t"] = sidecar["time"], sidecar["t"]
        if sidecar.get("gps"):
            rec["gps"] = sidecar["gps"]
        rec["provider"] = sidecar.get("provider")
    return rec


# ---- campus frame --------------------------------------------------------

def to_campus_many(lons, lats, alts, frame=None):
    """Arrays of lon/lat/alt (alt may be None) -> campus X, Y, Z (Z None when alt unknown)."""
    from frame import to_campus
    lons = np.asarray(lons, dtype=float)
    lats = np.asarray(lats, dtype=float)
    z = np.array([a if a is not None else 0.0 for a in alts], dtype=float)
    X, Y, Z = to_campus(lons, lats, z, 4326, frame=frame)
    Z = [float(v) if a is not None else None for v, a in zip(np.atleast_1d(Z), alts)]
    return np.atleast_1d(X), np.atleast_1d(Y), Z


def grid_heading(lon, lat, heading, frame=None, step=10.0):
    """Compass heading (true north) -> heading in the campus frame (grid north)."""
    from pyproj import Geod
    from frame import to_campus
    lon2, lat2, _ = Geod(ellps="WGS84").fwd(lon, lat, heading, step)
    X1, Y1, _ = to_campus(lon, lat, 0.0, 4326, frame=frame)
    X2, Y2, _ = to_campus(lon2, lat2, 0.0, 4326, frame=frame)
    return math.degrees(math.atan2(float(X2 - X1), float(Y2 - Y1))) % 360.0


def add_campus(records, frame=None):
    """Fill rec['campus'] = {X, Y, Z, tile, heading_deg} for records that have GPS."""
    from frame import tile_of
    have = [r for r in records if r.get("gps")]
    if not have:
        return
    X, Y, Z = to_campus_many([r["gps"]["lon"] for r in have], [r["gps"]["lat"] for r in have],
                             [r["gps"].get("alt") for r in have], frame)
    for r, x, y, z in zip(have, X, Y, Z):
        h = r["gps"].get("heading")
        hg = grid_heading(r["gps"]["lon"], r["gps"]["lat"], h, frame) if h is not None else None
        r["campus"] = {"X": round(float(x), 3), "Y": round(float(y), 3),
                       "Z": round(z, 3) if z is not None else None,
                       "tile": tile_of(float(x), float(y)),
                       "heading_deg": round(hg, 2) if hg is not None else None}
