"""Make a synthetic calibration clip: clicks at 120 bpm, a hard colour cut exactly on every second click.

python -P make_calib.py <out_dir>   -> <out_dir>/calib.mp4
Run analyze.py + sync.py on it: every cut should be within 1 frame of a beat and phase_R1 should be ~1.
This is how the 30 ms onset/beat latency correction (analyze.LATENCY) was measured.
"""
import os, subprocess, sys, wave
import numpy as np

out = sys.argv[1]
os.makedirs(out, exist_ok=True)
sr, dur, bpm = 48000, 20.0, 120
y = np.zeros(int(sr * dur))
rng = np.random.default_rng(1)
for k, t in enumerate(np.arange(0.5, dur, 60 / bpm)):
    i, n = int(t * sr), int(0.08 * sr)
    env = np.exp(-np.arange(n) / (0.012 * sr))
    amp = 1.0 if k % 4 == 0 else 0.5
    y[i:i + n] += amp * env * (rng.standard_normal(n) * 0.5 + np.sin(2 * np.pi * 80 * np.arange(n) / sr))
y += 0.02 * rng.standard_normal(len(y))
y = np.clip(y / np.abs(y).max() * 0.8, -1, 1)
wav = os.path.join(out, "clicks.wav")
with wave.open(wav, "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
    w.writeframes((y * 32767).astype(np.int16).tobytes())
cols = ["red", "blue", "green", "yellow", "purple", "orange", "cyan", "white", "gray", "pink"]
segs = [0.5] + [1.0] * 19 + [0.5]
inputs = []
for k, d in enumerate(segs):
    inputs += ["-f", "lavfi", "-i", f"color=c={cols[k % len(cols)]}:s=640x360:r=30:d={d}"]
fc = "".join(f"[{k}:v]" for k in range(len(segs))) + f"concat=n={len(segs)}:v=1:a=0[v]"
subprocess.run(["ffmpeg", "-y", "-v", "error", *inputs, "-i", wav, "-filter_complex", fc, "-map", "[v]",
                "-map", f"{len(segs)}:a", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest",
                os.path.join(out, "calib.mp4")], check=True)
print("written", os.path.join(out, "calib.mp4"))
