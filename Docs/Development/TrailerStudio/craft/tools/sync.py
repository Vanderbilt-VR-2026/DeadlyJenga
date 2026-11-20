"""Tracker-independent cut/music sync measures for one analysed trailer.

python -P sync.py <analysis_dir> [<analysis_dir> ...]
Needs audio.wav, audio.json, shots.csv, struct.json (optional). Writes sync.json:
  phase_R1 / phase_p1   vector strength of cut phases on the beat grid (1.0 = every cut at the
                        same point of the beat, 0 = spread evenly) and its Rayleigh p-value.
                        Does not care whether the tracker chose the right beat phase.
  phase_R2 / phase_p2   same on the half-beat grid (catches tempo-octave errors).
  phase_mean_frames     where in the beat the cuts cluster (frames after the tracked beat).
  onset_pct_mean        mean percentile of audio onset strength at the cut (+-1 video frame) against
                        every frame of the body: 50 = cuts ignore the sound, 100 = cuts always on
                        the strongest audio events.
  onset_top10_share     share of cuts whose onset strength is in the top 10% of the body (chance 0.10).
"""
import csv, json, os, sys
import numpy as np
import librosa

LAT = 0.030


def run(d):
    au = json.load(open(os.path.join(d, "audio.json")))
    summ = json.load(open(os.path.join(d, "summary.json")))
    st = json.load(open(os.path.join(d, "struct.json"))) if os.path.exists(os.path.join(d, "struct.json")) else {}
    fps = summ["fps"]
    shots = list(csv.DictReader(open(os.path.join(d, "shots.csv"))))
    t0 = st.get("content_start", summ["active_start"])
    t1 = st.get("final_title", st.get("endcard_start", summ["active_end"]))
    starts = [float(s["start"]) for s in shots]
    durs = [float(s["dur"]) for s in shots]
    # cuts in the body; skip cuts that only end a micro fragment (< 0.12 s)
    cuts = [t for k, t in enumerate(starts) if t0 + 0.05 < t <= t1 + 0.05 and (k == 0 or durs[k - 1] >= 0.12)]
    beats = np.array(au["beats"])
    out = dict(n=len(cuts))
    if len(beats) > 4 and cuts:
        ph = []
        for t in cuts:
            j = np.searchsorted(beats, t) - 1
            if 0 <= j < len(beats) - 1:
                ph.append((t - beats[j]) / (beats[j + 1] - beats[j]))
        ph = np.array(ph)
        if len(ph):
            z1 = np.exp(2j * np.pi * ph).mean(); z2 = np.exp(4j * np.pi * ph).mean()
            N = len(ph)
            out.update(phase_n=N, phase_R1=round(abs(z1), 3), phase_p1=float(np.exp(-N * abs(z1) ** 2)),
                       phase_R2=round(abs(z2), 3), phase_p2=float(np.exp(-N * abs(z2) ** 2)),
                       phase_mean_frames=round(float((np.angle(z1) / (2 * np.pi)) % 1 * np.median(np.diff(beats)) * fps), 2))
    y, sr = librosa.load(os.path.join(d, "audio.wav"), sr=22050)
    hop = 256
    env = librosa.onset.onset_strength(y=y, sr=sr, hop_length=hop)
    tt = librosa.frames_to_time(np.arange(len(env)), sr=sr, hop_length=hop) - LAT
    w = int(round(1 / fps * sr / hop))

    def local(t):
        k = int(np.searchsorted(tt, t))
        return env[max(0, k - w):k + w + 1].max() if len(env) else 0

    grid = np.arange(t0, t1, 1 / fps)
    ref = np.array([local(t) for t in grid])
    if cuts and len(ref):
        vals = np.array([local(t) for t in cuts])
        pct = np.array([(ref < v).mean() * 100 for v in vals])
        out.update(onset_pct_mean=round(float(pct.mean()), 1), onset_top10_share=round(float((pct >= 90).mean()), 3),
                   onset_top25_share=round(float((pct >= 75).mean()), 3))
    json.dump(out, open(os.path.join(d, "sync.json"), "w"), indent=1)
    return out


for d in sys.argv[1:]:
    print(os.path.basename(d), run(d))
