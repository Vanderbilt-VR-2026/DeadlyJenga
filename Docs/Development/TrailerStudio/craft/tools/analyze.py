"""Measure one trailer (any mp4): shots, motion, audio, cut-to-music alignment.

Usage: python -I analyze.py <video.mp4> <out_dir> [--ratio 3.0] [--minval 15]

Writes to out_dir:
  frames.npz     per-frame metrics (content diff, luma, motion) - cached, reused on rerun
  shots.csv      one row per shot
  audio.json     tempo, beats, downbeats, onsets, loudness curve, hits, silences
  summary.json   everything aggregated (shot stats, alignment vs chance, structure hints)
"""
import argparse, json, os, re, subprocess, sys
import numpy as np
import cv2

SMALL = (128, 72)
LATENCY = 0.030  # seconds, see calib/


def probe(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
                          "stream=r_frame_rate,nb_frames:format=duration", "-of", "json", path],
                         capture_output=True, text=True).stdout
    j = json.loads(out)
    num, den = j["streams"][0]["r_frame_rate"].split("/")
    return float(num) / float(den), float(j["format"]["duration"])


def frame_metrics(path):
    cap = cv2.VideoCapture(path)
    prev_hsv = prev_gray = None
    content, luma, motion, sat = [], [], [], []
    i = 0
    while True:
        ok, frame = cap.read()
        if not ok:
            break
        small = cv2.resize(frame, SMALL, interpolation=cv2.INTER_AREA)
        hsv = cv2.cvtColor(small, cv2.COLOR_BGR2HSV).astype(np.int16)
        gray = cv2.cvtColor(small, cv2.COLOR_BGR2GRAY)
        if prev_hsv is None:
            content.append(0.0); motion.append(0.0)
        else:
            content.append(float(np.abs(hsv - prev_hsv).mean()))
            # optical flow magnitude (pixels per frame at 128x72), every frame
            flow = cv2.calcOpticalFlowFarneback(prev_gray, gray, None, 0.5, 2, 9, 2, 5, 1.1, 0)
            motion.append(float(np.linalg.norm(flow, axis=2).mean()))
        luma.append(float(gray.mean()))
        sat.append(float(hsv[..., 1].mean()))
        prev_hsv, prev_gray = hsv, gray
        i += 1
    cap.release()
    return dict(content=np.array(content), luma=np.array(luma), motion=np.array(motion), sat=np.array(sat))


def hist_metric(path):
    """Colour-histogram change between consecutive frames (Bhattacharyya, 0..1).
    Motion inside one shot barely changes it; a cut usually does."""
    cap = cv2.VideoCapture(path)
    prev, out = None, []
    while True:
        ok, frame = cap.read()
        if not ok:
            break
        small = cv2.resize(frame, SMALL, interpolation=cv2.INTER_AREA)
        hsv = cv2.cvtColor(small, cv2.COLOR_BGR2HSV)
        h = cv2.calcHist([hsv], [0, 1, 2], None, [16, 8, 8], [0, 180, 0, 256, 0, 256])
        cv2.normalize(h, h)
        out.append(0.0 if prev is None else float(cv2.compareHist(prev, h, cv2.HISTCMP_BHATTACHARYYA)))
        prev = h
    cap.release()
    return np.array(out)


def ratios(x, window=2):
    n = len(x)
    r = np.zeros(n)
    for i in range(1, n):
        lo, hi = max(1, i - window), min(n, i + window + 1)
        nb = [x[j] for j in range(lo, hi) if j != i]
        r[i] = x[i] / max(sum(nb) / len(nb), 1e-3) if nb else 0
    return r


def detect_cuts(content, luma, ratio=3.0, minval=15.0, window=2, min_len=3, hist=None, hratio=2.0, hmin=0.3):
    """Adaptive hard-cut detector. A frame is a cut if EITHER
    - its HSV pixel difference is >= minval and >= ratio x its neighbours' mean, OR
    - its colour-histogram change is >= hmin and >= hratio x its neighbours' mean
      (catches cuts inside very fast footage where pixel differences are high everywhere)."""
    n = len(content)
    rp = ratios(content, window)
    rh = ratios(hist, window) if hist is not None else np.zeros(n)
    cuts = []
    for i in range(1, n):
        r = rp[i]
        hit = content[i] >= minval and (r >= ratio or content[i] >= 4 * minval)
        if hist is not None and hist[i] >= hmin and rh[i] >= hratio and content[i] >= minval * 0.6:
            hit = True
        if hit:
            if cuts and i - cuts[-1] < min_len:
                if content[i] > content[cuts[-1]]:
                    cuts[-1] = i
                continue
            cuts.append(i)
    # fades: runs of near-black / near-white frames become boundaries
    dark = (luma < 12) | (luma > 245)
    fades = []
    i = 0
    while i < n:
        if dark[i]:
            j = i
            while j < n and dark[j]:
                j += 1
            if j - i >= 2:
                fades.append((i, j))
            i = j
        else:
            i += 1
    return cuts, fades


def near_misses(content, cuts, ratio=3.0, window=2, k=12):
    n = len(content)
    cand = []
    cs = set(cuts)
    for i in range(1, n):
        if i in cs or any(abs(i - c) < 3 for c in cuts):
            continue
        lo, hi = max(1, i - window), min(n, i + window + 1)
        nb = [content[j] for j in range(lo, hi) if j != i]
        avg = sum(nb) / len(nb)
        r = content[i] / max(avg, 1e-3)
        if content[i] >= 6 and r >= 1.6:
            cand.append((content[i] * min(r, ratio), i))
    cand.sort(reverse=True)
    return [i for _, i in cand[:k]]


def ebur128(path):
    p = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", path, "-vn", "-af",
                        "ebur128=framelog=info:peak=true", "-f", "null", "-"],
                       capture_output=True, text=True, encoding="utf-8", errors="ignore")
    t, M, S = [], [], []
    for line in p.stderr.splitlines():
        m = re.search(r"t:\s*([\d.]+)\s+TARGET.*?M:\s*(-?[\d.inf]+)\s+S:\s*(-?[\d.inf]+)", line)
        if m:
            t.append(float(m.group(1)))
            M.append(float(m.group(2)) if "inf" not in m.group(2) else -120.0)
            S.append(float(m.group(3)) if "inf" not in m.group(3) else -120.0)
    tail = p.stderr[p.stderr.rfind("Summary:"):]
    I = re.search(r"I:\s*(-?[\d.]+) LUFS", tail)
    LRA = re.search(r"LRA:\s*([\d.]+) LU", tail)
    pk = re.search(r"Peak:\s*(-?[\d.inf]+) dBFS", tail)
    return dict(t=t, M=M, S=S, integrated=float(I.group(1)) if I else None,
                lra=float(LRA.group(1)) if LRA else None,
                true_peak=float(pk.group(1)) if pk and "inf" not in pk.group(1) else None)


def audio_analysis(path, work):
    import librosa
    wav = os.path.join(work, "audio.wav")
    if not os.path.exists(wav):
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-i", path, "-vn", "-ac", "1", "-ar", "22050", wav], check=True)
    y, sr = librosa.load(wav, sr=22050)
    hop = 512
    oenv = librosa.onset.onset_strength(y=y, sr=sr, hop_length=hop)
    tempo, beats = librosa.beat.beat_track(onset_envelope=oenv, sr=sr, hop_length=hop)
    tempo = float(np.atleast_1d(tempo)[0])
    bt = librosa.frames_to_time(beats, sr=sr, hop_length=hop)
    # pulse clarity: autocorrelation of onset envelope at the beat period
    ac = librosa.autocorrelate(oenv - oenv.mean())
    ac = ac / (ac[0] + 1e-9)
    lag = int(round(60.0 / max(tempo, 1) * sr / hop))
    pulse = float(ac[lag]) if 0 < lag < len(ac) else 0.0
    # downbeat guess: phase (of 4) whose beats carry the most low-frequency onset energy
    S = np.abs(librosa.stft(y, hop_length=hop))
    low = librosa.onset.onset_strength(S=librosa.amplitude_to_db(S[:40]), sr=sr, hop_length=hop)
    best, downs = -1, []
    for ph in range(4):
        sel = beats[ph::4]
        if len(sel) == 0:
            continue
        sc = low[np.clip(sel, 0, len(low) - 1)].mean()
        if sc > best:
            best, downs = sc, librosa.frames_to_time(sel, sr=sr, hop_length=hop).tolist()
    on_f = librosa.onset.onset_detect(onset_envelope=oenv, sr=sr, hop_length=hop, backtrack=False)
    on_t = librosa.frames_to_time(on_f, sr=sr, hop_length=hop)
    on_s = oenv[on_f] if len(on_f) else np.array([])
    strong_thr = float(np.percentile(on_s, 80)) if len(on_s) else 0
    strong = on_t[on_s >= strong_thr].tolist() if len(on_s) else []
    rms = librosa.feature.rms(y=y, hop_length=hop)[0]
    rms_db = 20 * np.log10(rms + 1e-6)
    rt = librosa.frames_to_time(np.arange(len(rms)), sr=sr, hop_length=hop)
    # hits: onset strength x loudness jump, keep top 8 separated by >=1 s
    hit_score = []
    for f in on_f:
        a = rms_db[max(0, f - 10):f].mean() if f > 0 else rms_db[0]
        b = rms_db[f:f + 6].max()
        hit_score.append(oenv[f] * max(b - a, 0.1) * (10 ** ((b + 20) / 40)))
    order = np.argsort(hit_score)[::-1]
    hits = []
    for k in order:
        t = float(on_t[k])
        if all(abs(t - h["t"]) >= 1.0 for h in hits):
            hits.append(dict(t=round(t, 3), score=round(float(hit_score[k]), 2), db=round(float(rms_db[on_f[k]:on_f[k]+6].max()), 1)))
        if len(hits) >= 8:
            break
    # silences / stop-downs: >=0.15 s at least 20 dB below the running median (or below -45 dBFS)
    med = float(np.median(rms_db))
    quiet = (rms_db < med - 20) | (rms_db < -45)
    sil = []
    i = 0
    while i < len(quiet):
        if quiet[i]:
            j = i
            while j < len(quiet) and quiet[j]:
                j += 1
            d = rt[min(j, len(rt) - 1)] - rt[i]
            if d >= 0.15:
                sil.append((round(float(rt[i]), 2), round(float(d), 2)))
            i = j
        else:
            i += 1
    return dict(tempo=tempo, pulse_clarity=round(pulse, 3), beats=bt.round(3).tolist(), downbeats=[round(x, 3) for x in downs],
                onsets=on_t.round(3).tolist(), strong_onsets=[round(x, 3) for x in strong],
                hits=hits, silences=sil, duration=float(len(y) / sr),
                rms_db_1s=[round(float(rms_db[(rt >= s) & (rt < s + 1)].mean()), 1) for s in range(int(len(y) / sr))])


def nearest_dist(times, refs):
    refs = np.asarray(refs)
    if len(refs) == 0:
        return np.full(len(times), np.inf)
    idx = np.searchsorted(refs, times)
    d = np.full(len(times), np.inf)
    for k, t in enumerate(times):
        for j in (idx[k] - 1, idx[k]):
            if 0 <= j < len(refs):
                d[k] = min(d[k], abs(t - refs[j]))
    return d


def alignment(cut_t, refs, fps, dur, start, end):
    cut_t = np.array([t for t in cut_t if start <= t <= end])
    out = dict(n=int(len(cut_t)))
    grid = np.arange(start, end, 1.0 / fps / 4)
    dg = nearest_dist(grid, refs)
    dc = nearest_dist(cut_t, refs)
    for k in (1, 2, 3):
        tol = k / fps + 1e-6
        out[f"within{k}f"] = round(float((dc <= tol).mean()), 3) if len(dc) else None
        out[f"chance{k}f"] = round(float((dg <= tol).mean()), 3)
    out["median_offset_frames"] = round(float(np.median(dc) * fps), 2) if len(dc) else None
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("video"); ap.add_argument("out")
    ap.add_argument("--ratio", type=float, default=2.5)
    ap.add_argument("--minval", type=float, default=20.0)
    ap.add_argument("--window", type=int, default=2)
    ap.add_argument("--hratio", type=float, default=2.5, help="0 disables the histogram test")
    ap.add_argument("--hmin", type=float, default=0.3)
    ap.add_argument("--redo-audio", action="store_true")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    fps, dur = probe(a.video)
    cache = os.path.join(a.out, "frames.npz")
    if os.path.exists(cache):
        fm = dict(np.load(cache))
    else:
        fm = frame_metrics(a.video)
        np.savez_compressed(cache, **fm)
    if "hist" not in fm:
        fm["hist"] = hist_metric(a.video)
        np.savez_compressed(cache, **fm)
    n = len(fm["content"])
    cuts, fades = detect_cuts(fm["content"], fm["luma"], a.ratio, a.minval, a.window,
                              hist=None if a.hratio <= 0 else fm["hist"], hratio=a.hratio, hmin=a.hmin)
    bounds = sorted(set([0] + cuts + [s for s, e in fades] + [e for s, e in fades if e < n] + [n]))
    bounds = [b for k, b in enumerate(bounds) if k == 0 or b - bounds[k - 1] >= 2]
    if bounds[-1] != n:
        bounds.append(n)
    # optional hand corrections after looking at the filmstrips:
    # edits.json = {"add": [seconds...], "remove": [seconds...]}  (wipes, dissolves, missed cuts, false cuts)
    n_auto = len(bounds) - 2
    ed_path = os.path.join(a.out, "edits.json")
    edits = json.load(open(ed_path)) if os.path.exists(ed_path) else {}
    for t in edits.get("remove", []):
        f = int(round(t * fps))
        near = [b for b in bounds[1:-1] if abs(b - f) <= 3]
        if near:
            bounds.remove(min(near, key=lambda b: abs(b - f)))
    for t in edits.get("add", []):
        f = int(round(t * fps))
        if 0 < f < n and all(abs(b - f) > 1 for b in bounds):
            bounds.append(f)
    bounds = sorted(bounds)
    shots = []
    for k in range(len(bounds) - 1):
        s, e = bounds[k], bounds[k + 1]
        mo = fm["motion"][s + 1:e] if e - s > 1 else fm["motion"][s:e]
        lu = fm["luma"][s:e]
        is_fade = any(fs <= s and e <= fe + 1 for fs, fe in fades)
        shots.append(dict(i=k, start_f=s, end_f=e, start=round(s / fps, 3), end=round(e / fps, 3),
                          dur=round((e - s) / fps, 3), motion=round(float(np.mean(mo)) if len(mo) else 0, 3),
                          luma=round(float(lu.mean()), 1), black=bool(is_fade or lu.mean() < 12)))
    with open(os.path.join(a.out, "shots.csv"), "w") as f:
        f.write("i,start,end,dur,motion,luma,black\n")
        for s in shots:
            f.write(f"{s['i']},{s['start']},{s['end']},{s['dur']},{s['motion']},{s['luma']},{int(s['black'])}\n")
    aj = os.path.join(a.out, "audio.json")
    if os.path.exists(aj) and not a.redo_audio:
        au = json.load(open(aj))
    else:
        au = audio_analysis(a.video, a.out)
        au["ebur128"] = ebur128(a.video)
        json.dump(au, open(aj, "w"))
    if not au.get("lat_corr"):
        # librosa onset/beat times land ~30 ms late; measured on a synthetic clip with cuts exactly on
        # clicks (calib/). Shift them back so cut-to-beat distances are not biased by one frame.
        for k in ("beats", "downbeats", "onsets", "strong_onsets"):
            au[k] = [round(t - LATENCY, 3) for t in au[k]]
        for h in au["hits"]:
            h["t"] = round(h["t"] - LATENCY, 3)
        au["lat_corr"] = LATENCY
        json.dump(au, open(aj, "w"))
    if not au["ebur128"].get("t"):
        au["ebur128"] = ebur128(a.video)
        json.dump(au, open(aj, "w"))
    # stats
    real = [s for s in shots if not s["black"]]
    d = np.array([s["dur"] for s in real])
    cut_t = [s["start"] for s in shots[1:]]
    # active window: from first non-black content to last non-black shot end
    act0 = real[0]["start"] if real else 0
    act1 = real[-1]["end"] if real else dur
    thirds = []
    if len(real) >= 3:
        span = act1 - act0
        for q in range(4):
            lo, hi = act0 + q * span / 4, act0 + (q + 1) * span / 4
            ds = [s["dur"] for s in real if lo <= (s["start"] + s["end"]) / 2 < hi]
            thirds.append(round(float(np.median(ds)), 3) if ds else None)
    win = [sum(1 for t in cut_t if w <= t < w + 5) for w in np.arange(0, dur, 5)]
    al_beat = alignment(cut_t, au["beats"], fps, dur, act0, act1)
    al_down = alignment(cut_t, au["downbeats"], fps, dur, act0, act1)
    al_on = alignment(cut_t, au["onsets"], fps, dur, act0, act1)
    al_strong = alignment(cut_t, au["strong_onsets"], fps, dur, act0, act1)
    S = [x for x in au["ebur128"]["S"] if x > -70]
    big = au["hits"][0] if au["hits"] else None
    big_shot = None
    if big:
        for s in shots:
            if s["start"] <= big["t"] < s["end"]:
                big_shot = s["i"]
    summ = dict(video=os.path.basename(a.video), fps=round(fps, 3), duration=round(dur, 3), frames=n,
                params=dict(ratio=a.ratio, minval=a.minval, window=a.window, hratio=a.hratio, hmin=a.hmin),
                n_auto_cuts=n_auto, n_added=len(edits.get('add', [])), n_removed=len(edits.get('remove', [])),
                n_shots=len(shots), n_real_shots=len(real), n_cuts=len(cut_t), n_fades=len(fades),
                shot_mean=round(float(d.mean()), 3) if len(d) else None,
                shot_median=round(float(np.median(d)), 3) if len(d) else None,
                shot_min=round(float(d.min()), 3) if len(d) else None,
                shot_max=round(float(d.max()), 3) if len(d) else None,
                shot_median_by_quarter=thirds,
                cuts_per_5s=win, active_start=act0, active_end=act1,
                motion_mean=round(float(np.mean([s["motion"] for s in real])), 3) if real else None,
                tempo=round(au["tempo"], 1), pulse_clarity=au["pulse_clarity"],
                align_beat=al_beat, align_downbeat=al_down, align_onset=al_on, align_strong_onset=al_strong,
                integrated_lufs=au["ebur128"]["integrated"], lra=au["ebur128"]["lra"], true_peak=au["ebur128"]["true_peak"],
                short_term_lufs_max=round(max(S), 1) if S else None,
                biggest_hit=big, biggest_hit_shot=big_shot, biggest_hit_frac=round(big["t"] / dur, 3) if big else None,
                silences=au["silences"], hits=au["hits"],
                near_misses=[round(i / fps, 3) for i in near_misses(fm["content"], cuts)])
    json.dump(summ, open(os.path.join(a.out, "summary.json"), "w"), indent=1)
    print(json.dumps({k: summ[k] for k in ("video", "duration", "n_shots", "shot_median", "tempo", "pulse_clarity", "integrated_lufs")}))
    print("beat", al_beat); print("strong", al_strong)


if __name__ == "__main__":
    main()
