"""Make a SYNTHETIC equirectangular test video for the 360 intake path.

Used when no real, permissively licensed 360 clip can be downloaded. It is
clearly labeled synthetic (file name, metadata comment and sidecar JSON).
It is not a capture and must never be aligned or used for textures.

Picture (2:1 equirectangular): sky gradient on top, a ring of the given
still photos around the horizon (like building fronts), a checkered ground
and a dark blob at the very bottom where a 360 operator would be. The video
turns slowly (6 degrees per second) so every frame differs.

Usage:
  intake_synth360.py OUT.mp4 STILL [STILL ...] [--seconds 30] [--width 3840]
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

import cv2
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from intake_images import read_upright, write_png  # noqa: E402


def make_panorama(stills, width=3840):
    h = width // 2
    pano = np.zeros((h, width, 3), np.uint8)
    top, band = int(h * 0.30), int(h * 0.32)
    for y in range(top):                                   # sky: light blue to white
        a = y / max(1, top)
        pano[y, :] = (int(235 - 40 * (1 - a)), int(215 - 30 * (1 - a)), int(170 + 60 * a))
    x = 0
    imgs = [im for im in (read_upright(s) for s in stills) if im is not None]
    if not imgs:
        raise SystemExit("no readable stills")
    i = 0
    while x < width:                                       # ring of photos around the horizon
        im = imgs[i % len(imgs)]
        w = int(im.shape[1] * band / im.shape[0])
        part = cv2.resize(im, (w, band), interpolation=cv2.INTER_AREA)[:, :width - x]
        pano[top:top + band, x:x + part.shape[1]] = part
        x += part.shape[1]
        i += 1
    yy, xx = np.mgrid[top + band:h, 0:width]               # checkered ground
    checker = (((xx // 64) + (yy // 48)) % 2).astype(np.uint8)
    pano[top + band:h] = np.where(checker[..., None] == 1, 120, 90).astype(np.uint8)
    cv2.ellipse(pano, (width // 2, h), (width // 2, int(h * 0.12)), 0, 180, 360, (30, 30, 40), -1)
    return pano


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("out")
    p.add_argument("stills", nargs="+")
    p.add_argument("--seconds", type=float, default=30.0)
    p.add_argument("--width", type=int, default=3840)
    p.add_argument("--fps", type=int, default=24)
    args = p.parse_args(argv)
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    pano_path = out.with_suffix(".panorama.png")
    write_png(pano_path, make_panorama(args.stills, args.width))
    turn = 6.0 / 360.0 / args.fps                          # fraction of the width per frame
    cmd = ["ffmpeg", "-v", "error", "-y", "-loop", "1", "-framerate", str(args.fps), "-t", str(args.seconds),
           "-i", str(pano_path), "-vf", f"scroll=horizontal={turn:.8f},format=yuv420p",
           "-c:v", "libx264", "-preset", "veryfast", "-crf", "20",
           "-metadata", "comment=SYNTHETIC equirectangular test clip made by intake_synth360.py; not a capture",
           str(out)]
    res = subprocess.run(cmd, capture_output=True)
    if res.returncode != 0:
        raise SystemExit("ffmpeg failed: " + res.stderr.decode(errors="replace")[:500])
    pano_path.unlink()
    side = out.with_suffix(".synthetic.json")
    side.write_text(json.dumps({
        "synthetic": True, "made_by": "pipeline/intake_synth360.py", "seconds": args.seconds,
        "width": args.width, "height": args.width // 2, "fps": args.fps, "turn_deg_per_s": 6.0,
        "stills": [Path(s).name for s in args.stills],
        "note": "Test input for the 360 code path only. Not a real capture; do not align or texture from it.",
    }, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {out} and {side}")


if __name__ == "__main__":
    main()
