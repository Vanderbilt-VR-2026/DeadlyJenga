"""Contact sheet for the blur spot check (Stage 2 "Done when" 4).

Picks up to N blurred images that had something blurred (faces first, then
plates, then people's heads and vehicle plate bands), and draws a 5 x 10
grid. Each cell shows up to four crops around that image's blurred boxes,
taken from the BLURRED copy, so a person can check that no face or plate is
still readable. A second sheet with the whole frames (downscaled) is written
next to it for context.

Usage:
  intake_spotcheck.py PHOTOS_JSON [PHOTOS_JSON ...] [--n 50] [--out reports/stage2/blur_spotcheck.jpg]
"""
import argparse
import sys
from pathlib import Path

import cv2
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import data_root, load_json  # noqa: E402
import manifest  # noqa: E402
from intake_images import read_upright, write_jpeg  # noqa: E402

RANK = {"face": 0, "plate": 1, "head": 2, "plate_band": 3}
CELL_W, CELL_H, LABEL = 384, 288, 18


def pick(photo_sets, n):
    """Images with blurred boxes, best ones first, spread over the given sets."""
    per_set = []
    for recs in photo_sets:
        cands = [r for r in recs if (r.get("privacy") or {}).get("blur_boxes") and r["privacy"].get("blurred")]
        cands.sort(key=lambda r: (min(RANK[b["reason"]] for b in r["privacy"]["blur_boxes"]),
                                  -len(r["privacy"]["blur_boxes"])))
        per_set.append(cands)
    out = []
    while len(out) < n and any(per_set):
        for cands in per_set:                      # round robin over the sets
            if cands and len(out) < n:
                out.append(cands.pop(0))
    return out


def crop_cell(img, boxes):
    """Up to four crops (2 x 2) around the most important boxes."""
    cell = np.full((CELL_H, CELL_W, 3), 40, np.uint8)
    boxes = sorted(boxes, key=lambda b: (RANK[b["reason"]], -b["box"][2] * b["box"][3]))[:4]
    h, w = img.shape[:2]
    sw, sh = CELL_W // 2, (CELL_H - LABEL) // 2
    for k, b in enumerate(boxes):
        x, y, bw, bh = b["box"]
        side = int(max(bw, bh) * 1.8) + 8              # some context around the box
        cx, cy = x + bw // 2, y + bh // 2
        x0, y0 = max(0, cx - side // 2), max(0, cy - side // 2)
        x1, y1 = min(w, x0 + side), min(h, y0 + side)
        crop = img[y0:y1, x0:x1]
        if crop.size == 0:
            continue
        s = min(sw / crop.shape[1], sh / crop.shape[0])
        crop = cv2.resize(crop, (max(1, int(crop.shape[1] * s)), max(1, int(crop.shape[0] * s))),
                          interpolation=cv2.INTER_NEAREST if s > 1 else cv2.INTER_AREA)
        ox, oy = (k % 2) * sw, LABEL + (k // 2) * sh
        cell[oy:oy + crop.shape[0], ox:ox + crop.shape[1]] = crop
        cv2.putText(cell, b["reason"], (ox + 3, oy + 14), cv2.FONT_HERSHEY_SIMPLEX, 0.45, (0, 255, 255), 1)
    return cell


def frame_cell(img):
    cell = np.full((CELL_H, CELL_W, 3), 40, np.uint8)
    s = min(CELL_W / img.shape[1], (CELL_H - LABEL) / img.shape[0])
    small = cv2.resize(img, (int(img.shape[1] * s), int(img.shape[0] * s)), interpolation=cv2.INTER_AREA)
    cell[LABEL:LABEL + small.shape[0], :small.shape[1]] = small
    return cell


def sheet(cells, labels, cols=5):
    rows = (len(cells) + cols - 1) // cols
    out = np.full((rows * CELL_H, cols * CELL_W, 3), 20, np.uint8)
    for i, (c, lab) in enumerate(zip(cells, labels)):
        r, q = divmod(i, cols)
        cv2.putText(c, lab[:46], (3, 13), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (255, 255, 255), 1)
        out[r * CELL_H:(r + 1) * CELL_H, q * CELL_W:(q + 1) * CELL_W] = c
        cv2.rectangle(out, (q * CELL_W, r * CELL_H), ((q + 1) * CELL_W - 1, (r + 1) * CELL_H - 1), (90, 90, 90), 1)
    return out


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("photos_json", nargs="+")
    p.add_argument("--n", type=int, default=50)
    p.add_argument("--out", default="reports/stage2/blur_spotcheck.jpg", help="path inside the archive")
    args = p.parse_args(argv)
    root = data_root()
    sets = [load_json(f)["photos"] for f in args.photos_json]
    chosen = pick(sets, args.n)
    if not chosen:
        sys.exit("no blurred images with detections found")
    cells, fcells, labels = [], [], []
    for r in chosen:
        img = read_upright(root / r["privacy"]["blurred"])
        if img is None:
            continue
        cells.append(crop_cell(img, r["privacy"]["blur_boxes"]))
        fcells.append(frame_cell(img))
        p_ = r["privacy"]
        labels.append(f"{Path(r['name']).name[:22]} f{p_['faces']} p{p_['plates']} h{p_['persons']} v{p_['vehicles']}")
    out = root / args.out
    write_jpeg(out, sheet(cells, labels), 90)
    out2 = out.with_name(out.stem + "_frames.jpg")
    write_jpeg(out2, sheet(fcells, labels), 90)
    db = manifest.connect(root)
    for f in (out, out2):
        manifest.add_file(f, source="pipeline/intake_spotcheck.py", license="derived from blurred copies",
                          stage=2, root=root, db=db)
    db.commit()
    db.close()
    print(f"{len(cells)} images; wrote {out} and {out2}")


if __name__ == "__main__":
    main()
