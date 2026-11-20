"""Stage 2 music map for hand-reveal-2: measures the excerpt of the project's own cue we cut against.

Usage: python music_map.py <cue.wav> <start_s> <length_s> <out_folder>
Writes music.yaml (beats, onsets, hits, stop-downs, loudness every 100 ms) and the excerpt as music-excerpt.wav.
Times are excerpt times; frames are at 30 fps. librosa times are corrected by -0.030 s (skill note).
"""
import json, subprocess, sys, os
import numpy as np, librosa, soundfile as sf

cue, start, length, out = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), sys.argv[4]
os.makedirs(out, exist_ok=True)
y, sr = librosa.load(cue, sr=48000, mono=False, offset=start, duration=length)
if y.ndim == 1:
    y = np.stack([y, y])
sf.write(os.path.join(out, "music-excerpt.wav"), y.T, sr, subtype="PCM_24")
mono = y.mean(axis=0)
LAG = 0.030
FPS = 30

tempo, beats = librosa.beat.beat_track(y=mono, sr=sr, units="time")
hop = 480  # 10 ms
env = librosa.onset.onset_strength(y=mono, sr=sr, hop_length=hop)
onsets = librosa.onset.onset_detect(onset_envelope=env, sr=sr, hop_length=hop, units="frames")
rms = librosa.feature.rms(y=mono, frame_length=2048, hop_length=hop)[0]
db = 20 * np.log10(rms + 1e-7)

on_list = []
for i in onsets:
    t = i * hop / sr - LAG
    rise = float(db[i:i + 10].max() - db[max(0, i - 50):max(1, i - 5)].mean())
    on_list.append({"t": round(t, 3), "frame": round(t * FPS, 1), "strength": round(float(env[i]), 2), "rise_db": round(rise, 1)})
strong = sorted(on_list, key=lambda o: -o["strength"])[:max(3, len(on_list) // 20)]
hits = [o for o in strong if o["rise_db"] >= 6]

# biggest hit: largest rise from a low 0.25-2 s before to the peak 0.1 s after
best = (0, 0)
for i in range(200, len(db) - 10):
    rise = db[i:i + 10].max() - db[i - 200:i - 25].min()
    if rise > best[0]:
        best = (rise, i)
biggest = {"t": round(best[1] * hop / sr - LAG, 3), "rise_db": round(float(best[0]), 1)}

# stop-downs: 0.25 s or more where the level is 15 dB below the previous 2 s
stop = []
run = None
for i in range(200, len(db)):
    low = db[i] < db[i - 200:i].max() - 15
    if low and run is None:
        run = i
    if (not low or i == len(db) - 1) and run is not None:
        if i - run >= 25:
            stop.append({"from": round(run * hop / sr, 2), "to": round(i * hop / sr, 2)})
        run = None

# loudness every 100 ms with ffmpeg ebur128
ex = os.path.join(out, "music-excerpt.wav")
r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", ex, "-af", "ebur128=framelog=verbose", "-f", "null", "-"], capture_output=True, text=True)
moments = []
for line in r.stderr.splitlines():
    if "t:" in line and "M:" in line:
        try:
            t = float(line.split("t:")[1].split()[0]); m = float(line.split("M:")[1].split()[0]); s = float(line.split("S:")[1].split()[0])
            moments.append((round(t, 1), m, s))
        except Exception:
            pass
summary = [l.strip() for l in r.stderr.splitlines() if l.strip().startswith(("I:", "LRA:"))]

doc = {
    "file": cue, "excerpt_start_s": start, "excerpt_length_s": length,
    "bpm": round(float(np.atleast_1d(tempo)[0]), 2),
    "beats_s": [round(b - LAG, 3) for b in beats],
    "onsets": on_list, "hits_top5pct_rise6db": hits, "biggest_hit": biggest, "stopdowns": stop,
    "loudness_summary": summary,
    "momentary_every_0.5s": [m for m in moments if abs(m[0] * 2 - round(m[0] * 2)) < .01],
}
with open(os.path.join(out, "music.yaml"), "w") as f:
    f.write("# Measured by tools/music_map.py (JSON is valid YAML)\n")
    json.dump(doc, f, indent=1)
print("bpm", doc["bpm"], "biggest", biggest, "hits", hits, "stopdowns", stop, summary)
