"""Ground-truth helper for tuning the cut detector.

python -P candidates.py <video.mp4> <analysis_dir>
  Lists every frame that MIGHT be a cut (local peak of pixel or histogram change,
  far below the detector's thresholds), writes candidates.json and before|after
  sheets (cand_XX.jpg) labelled K<n>. Label them by eye in labels.json:
  {"cuts": [K numbers that are real cuts], "unsure": [...]}.
python -P candidates.py --eval <analysis_dir> [<analysis_dir> ...]
  Grid-searches detector settings against the labels and prints miss / false rates.
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import analyze, sheets


def local_peak(x, i, w=2):
    lo, hi = max(1, i - w), min(len(x), i + w + 1)
    return x[i] >= max(x[lo:hi])


def find(fm):
    c, h = fm["content"], fm["hist"]
    rp, rh = analyze.ratios(c), analyze.ratios(h)
    out = []
    for i in range(1, len(c)):
        if (local_peak(c, i) and c[i] >= 8 and rp[i] >= 1.3) or (local_peak(h, i) and h[i] >= 0.12 and rh[i] >= 1.3):
            if out and i - out[-1] < 2:
                continue
            out.append(i)
    return out


def main_make(video, d):
    fm = dict(np.load(os.path.join(d, "frames.npz")))
    if "hist" not in fm:
        fm["hist"] = analyze.hist_metric(video)
        np.savez_compressed(os.path.join(d, "frames.npz"), **fm)
    fps, _ = analyze.probe(video)
    cand = find(fm)
    json.dump(dict(fps=fps, frames=cand), open(os.path.join(d, "candidates.json"), "w"))
    fr = sheets.grab(video, [f - 1 for f in cand] + cand)
    cells = [sheets.cell([fr.get(f - 1), fr.get(f)], f"K{k} {f / fps:.2f}", (80, 255, 80)) for k, f in enumerate(cand)]
    os.makedirs(os.path.join(d, "sheets"), exist_ok=True)
    for p in range(0, len(cells), 48):
        sheets.tile(cells[p:p + 48], 4, os.path.join(d, "sheets", f"cand_{p // 48:02d}.jpg"))
    print(len(cand), "candidates")


def score(dirs, **kw):
    tp = fp = fn = 0
    for d in dirs:
        fm = dict(np.load(os.path.join(d, "frames.npz")))
        cj = json.load(open(os.path.join(d, "candidates.json")))
        lab = json.load(open(os.path.join(d, "labels.json")))
        truth = set(cj["frames"][k] for k in lab["cuts"])
        unsure = set(cj["frames"][k] for k in lab.get("unsure", []))
        cuts, _ = analyze.detect_cuts(fm["content"], fm["luma"], hist=fm["hist"], **kw)
        det = set(cuts)
        for t in truth:
            if any(abs(t - x) <= 1 for x in det):
                tp += 1
            else:
                fn += 1
        for x in det:
            if not any(abs(t - x) <= 1 for t in truth | unsure):
                fp += 1
    return tp, fp, fn


def main_eval(dirs):
    rows = []
    for ratio in (2.0, 2.5, 3.0):
        for minval in (10, 15, 20):
            for hratio in (0, 1.5, 2.0, 2.5, 3.0):
                for hmin in (0.2, 0.3, 0.4):
                    if hratio == 0 and hmin != 0.3:
                        continue
                    kw = dict(ratio=ratio, minval=minval, hratio=hratio if hratio else 1e9, hmin=hmin)
                    tp, fp, fn = score(dirs, **kw)
                    rows.append((fp + fn, ratio, minval, hratio, hmin, tp, fp, fn))
    rows.sort()
    print("errors ratio minval hratio hmin | tp fp fn | miss% false%")
    for r in rows[:12] + [x for x in rows if x[1] == 3.0 and x[2] == 15 and x[3] == 0]:
        tp, fp, fn = r[5:]
        print(r[:5], "|", tp, fp, fn, "|", round(100 * fn / max(tp + fn, 1), 1), round(100 * fp / max(tp + fp, 1), 1))
    for d in dirs:
        print(d, score([d], ratio=rows[0][1], minval=rows[0][2], hratio=rows[0][3] or 1e9, hmin=rows[0][4]))


if __name__ == "__main__":
    if sys.argv[1] == "--eval":
        main_eval(sys.argv[2:])
    else:
        main_make(sys.argv[1], sys.argv[2])
