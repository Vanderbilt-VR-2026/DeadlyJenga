"""Contact sheets for eyeballing a trailer.

python -I sheets.py <video.mp4> <out_dir> filmstrip [--from S] [--to S] [--per 30]
    one row cell per shot: first / middle / last frame, labelled "#i start dur"
python -I sheets.py <video.mp4> <out_dir> validate
    for every detected cut: frame before | frame after (green label);
    for near-miss peaks (from summary.json): before | after (orange label "?")
"""
import csv, json, os, sys
import cv2
import numpy as np

W, H = 176, 99
LAB = 14


def grab(path, wanted):
    wanted = sorted(set(int(x) for x in wanted))
    cap = cv2.VideoCapture(path)
    out, i, k = {}, 0, 0
    while k < len(wanted):
        ok, fr = cap.read()
        if not ok:
            break
        if i == wanted[k]:
            out[i] = cv2.resize(fr, (W, H), interpolation=cv2.INTER_AREA)
            while k < len(wanted) and wanted[k] == i:
                k += 1
        i += 1
    cap.release()
    return out


def cell(imgs, label, color):
    c = np.zeros((H + LAB, W * len(imgs) + 4, 3), np.uint8)
    for j, im in enumerate(imgs):
        if im is not None:
            c[LAB:, j * W:(j + 1) * W] = im
    cv2.putText(c, label, (2, 11), cv2.FONT_HERSHEY_SIMPLEX, 0.38, color, 1, cv2.LINE_AA)
    return c


def tile(cells, per_row, path):
    rows = [cells[i:i + per_row] for i in range(0, len(cells), per_row)]
    w = max(c.shape[1] for c in cells) * per_row
    blank = np.zeros_like(cells[0])
    img = np.vstack([np.hstack(r + [blank] * (per_row - len(r))) for r in rows])
    cv2.imwrite(path, img[:, :w], [cv2.IMWRITE_JPEG_QUALITY, 82])


def main():
    video, out, mode = sys.argv[1:4]
    args = sys.argv[4:]
    opt = {args[i].lstrip("-"): args[i + 1] for i in range(0, len(args) - 1, 2)}
    shots = list(csv.DictReader(open(os.path.join(out, "shots.csv"))))
    summ = json.load(open(os.path.join(out, "summary.json")))
    fps = summ["fps"]
    os.makedirs(os.path.join(out, "sheets"), exist_ok=True)
    if mode == "filmstrip":
        t0, t1 = float(opt.get("from", 0)), float(opt.get("to", 1e9))
        per = int(opt.get("per", 30))
        sel = [s for s in shots if float(s["end"]) > t0 and float(s["start"]) < t1]
        want = []
        for s in sel:
            a, b = int(round(float(s["start"]) * fps)), int(round(float(s["end"]) * fps)) - 1
            want += [a + 1 if b > a + 2 else a, (a + b) // 2, max(a, b - 1)]
        fr = grab(video, want)
        cells = []
        for s in sel:
            a, b = int(round(float(s["start"]) * fps)), int(round(float(s["end"]) * fps)) - 1
            ids = [a + 1 if b > a + 2 else a, (a + b) // 2, max(a, b - 1)]
            cells.append(cell([fr.get(i) for i in ids], f"#{s['i']} {float(s['start']):.2f}s {float(s['dur']):.2f}s m{float(s['motion']):.1f}", (255, 255, 255)))
        for p in range(0, len(cells), per):
            tile(cells[p:p + per], 3, os.path.join(out, "sheets", f"film_{int(t0)}_{p // per:02d}.jpg"))
        print(len(cells), "shots")
    elif mode == "dense":
        # one frame every --step seconds, 10 per row, labelled with time: for hand segmentation
        step, t0, t1 = float(opt.get("step", 0.33)), float(opt.get("from", 0)), float(opt.get("to", summ["duration"]))
        ts = np.arange(t0, min(t1, summ["duration"] - 0.05), step)
        fr = grab(video, [int(round(t * fps)) for t in ts])
        cells = [cell([fr.get(int(round(t * fps)))], f"{t:.2f}", (255, 255, 0)) for t in ts]
        for p in range(0, len(cells), 120):
            tile(cells[p:p + 120], 10, os.path.join(out, "sheets", f"dense_{int(t0)}_{p // 120:02d}.jpg"))
        print(len(cells), "frames")
    else:
        cuts = [int(round(float(s["start"]) * fps)) for s in shots[1:]]
        miss = [int(round(t * fps)) for t in summ["near_misses"]]
        fr = grab(video, [c - 1 for c in cuts] + cuts + [m - 1 for m in miss] + miss)
        cells = [cell([fr.get(c - 1), fr.get(c)], f"C{k+1} {c / fps:.2f}", (80, 255, 80)) for k, c in enumerate(cuts)]
        cells += [cell([fr.get(m - 1), fr.get(m)], f"?{k+1} {m / fps:.2f}", (0, 160, 255)) for k, m in enumerate(miss)]
        for p in range(0, len(cells), 40):
            tile(cells[p:p + 40], 4, os.path.join(out, "sheets", f"validate_{p // 40:02d}.jpg"))
        print(len(cuts), "cuts", len(miss), "near misses")


if __name__ == "__main__":
    main()
