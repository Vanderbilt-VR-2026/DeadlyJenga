"""Assemble a cut from a Studio driver job's output: picture, sound, loudness, evidence (no Unity).

Usage:
  python assemble.py <job_folder> <out_folder> <preview|final> <sound_events.json> [--sound-root DIR]

Reads from <job_folder>:
  shots.txt        one line per shot: id cut_in cut_out key shift handle (trailer frames; `handle` =
                   frames rendered before cut_in; written by hand-reveal-2's HandRevealMoment driver, so a
                   new driver must write the same six columns)
  shots/<id>.mp4   each shot rendered with `handle` frames on both sides
  tape.wav         the take's own sound (optional; used when the config sets take_gain_db)
Reads <sound_events.json> (see sound_events.example.json): the music file, gains, every designed or
game sound by file name and trailer frame, and the sync points to measure.
Writes to <out_folder>: cut.mp4, mix.wav, filmstrip.png (a frame every 6), contact.png (each shot's
key frame), stills/ (up to 10 frames), metrics.txt (duration, size, codec, loudness, true peak,
ramp, silence/black at the start, and the sync table).

Sound files are found by name (<file>.wav) under --sound-root, or the config's "sound_root", or the
environment variable DJ_SOUND_ROOT (for example a checkout's Assets/_DeadlyJenga folder).

The sync table has two columns (revision 3): the nearest onset in the full mix, and the nearest
onset in that sync point's own sound lane rendered alone. A voice line or any soft attack can read
many frames late in the full mix (hand-reveal-2: +17.3 frames on a line that was in sync), so trust
the lane column, and every event marked "voice": true is listed as NEEDS EARS whatever the numbers say.

Limits: loudness is normalised to -15 LUFS / -1.5 dBTP by two-pass loudnorm plus a limiter, then
measured; it does not judge balance between lanes. No titles (burn them in afterwards with drawtext
or an overlay). Needs ffmpeg/ffprobe on PATH, numpy, soundfile and librosa.
Started from hand-reveal-2's tools/assemble.py (2026-11-20); generalised: no hard-wired worktree,
tape offset, fps or loudness target, and the per-lane sync check.
"""
import argparse
import glob
import json
import os
import subprocess

ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
ap.add_argument("job")
ap.add_argument("out")
ap.add_argument("mode", choices=["preview", "final"])
ap.add_argument("events")
ap.add_argument("--sound-root", default=None)
args = ap.parse_args()

import numpy as np

job, out, mode = args.job, args.out, args.mode
cfg = json.load(open(args.events))
here = os.path.dirname(os.path.abspath(args.events))
os.makedirs(out, exist_ok=True)
FPS = cfg.get("fps", 30)
W, H = (1920, 1080) if mode == "final" else (640, 360)
TAPE_AT_ZERO = cfg.get("tape_at_zero_s", 0.0)       # tape second that plays at trailer frame 0 (hand-reveal-2: 1.6)
TARGET_I, TARGET_TP = cfg.get("target_lufs", -15), cfg.get("mix_true_peak", -1.5)
SOUND_ROOT = args.sound_root or cfg.get("sound_root") or os.environ.get("DJ_SOUND_ROOT")
MUSIC = os.path.join(here, cfg["music"]) if cfg.get("music") else None


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr[-3000:])
        raise SystemExit("ffmpeg failed: " + " ".join(cmd[:6]))
    return r


shots = []
for line in open(os.path.join(job, "shots.txt")):
    p = line.split()
    if len(p) >= 6:
        shots.append({"id": p[0], "cut_in": int(p[1]), "cut_out": int(p[2]), "key": int(p[3]), "shift": int(p[4]), "handle": int(p[5])})
length = shots[-1]["cut_out"]
seconds = length / FPS

# ---- picture ----
inputs, parts = [], []
for i, s in enumerate(shots):
    inputs += ["-i", os.path.join(job, "shots", s["id"] + ".mp4")]
    a, b = s["handle"], s["handle"] + s["cut_out"] - s["cut_in"]
    parts.append(f"[{i}:v]trim=start_frame={a}:end_frame={b},setpts=PTS-STARTPTS,scale={W}:{H}:flags=lanczos,setsar=1,fps={FPS}[v{i}]")
graph = ";".join(parts) + ";" + "".join(f"[v{i}]" for i in range(len(shots))) + f"concat=n={len(shots)}:v=1:a=0[v]"
picture = os.path.join(out, "picture.mp4")
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *inputs, "-filter_complex", graph, "-map", "[v]", "-c:v", "libx264",
     "-preset", "medium", "-crf", "16" if mode == "final" else "20", "-pix_fmt", "yuv420p", picture])

# ---- sound ----
import soundfile as sf


def find(name):
    if os.path.isfile(name):
        return name
    if not SOUND_ROOT:
        raise SystemExit("no sound root: pass --sound-root, set sound_root in the config, or DJ_SOUND_ROOT")
    found = glob.glob(os.path.join(SOUND_ROOT, "**", name + ".wav"), recursive=True)
    if not found:
        raise SystemExit("sound not found: " + name)
    return found[0]


def first_sound(path, threshold_db=-30):
    """Seconds of leading near-silence in a clip, so its attack (not its file start) lands on the planned frame."""
    data, rate = sf.read(path, always_2d=True)
    level = np.abs(data).max(axis=1)
    above = np.nonzero(level > 10 ** (threshold_db / 20) * max(level.max(), 1e-9))[0]
    return max(0.0, above[0] / rate - 0.005) if len(above) else 0.0


class Lanes:
    def __init__(self):
        self.ins, self.chains, self.labels = [], [], []

    def add(self, path, at, gain_db, start=0.0, dur=None, fade_in=0.005, fade_out=0.15, extra=""):
        n = len(self.labels)
        self.ins += ["-i", path]
        d = dur if dur else 30
        chain = f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,atrim=start={start}:duration={d},asetpts=PTS-STARTPTS"
        if extra:
            chain += "," + extra
        chain += f",afade=t=in:d={fade_in}"
        if dur:
            chain += f",afade=t=out:st={max(0, d - fade_out)}:d={fade_out}"
        ms = int(round(at * 1000))
        chain += f",volume={gain_db}dB,adelay={ms}|{ms}[l{n}]"
        self.chains.append(chain)
        self.labels.append(f"[l{n}]")

    def render(self, path):
        graph = ";".join(self.chains) + ";" + "".join(self.labels) + f"amix=inputs={len(self.labels)}:normalize=0:duration=longest,apad,atrim=duration={seconds}[mix]"
        run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *self.ins, "-filter_complex", graph, "-map", "[mix]", "-ar", "48000", "-c:a", "pcm_s24le", path])


def add_event(lanes, e):
    path = find(e["file"])
    start = e.get("start", first_sound(path) if e.get("align", True) else 0)
    lanes.add(path, e["frame"] / FPS, e["gain_db"], start=start, dur=e.get("dur"), fade_in=e.get("fade_in", 0.005),
              fade_out=e.get("fade_out", 0.15), extra=e.get("filter", ""))


mixl = Lanes()
if MUSIC:
    boost = cfg.get("music_boost_after_main_db", 0)
    extra = f"volume='if(gte(t,{cfg.get('main_frame', 0) / FPS}),{10 ** (boost / 20):.3f},1)':eval=frame" if boost else ""
    mixl.add(MUSIC, 0, cfg.get("music_gain_db", -2), dur=seconds, fade_in=0.01, fade_out=cfg.get("music_fade_out", 0.4), extra=extra)
tape = os.path.join(job, "tape.wav")
if os.path.exists(tape) and cfg.get("take_gain_db") is not None:
    mixl.add(tape, 0, cfg["take_gain_db"], start=TAPE_AT_ZERO, dur=seconds, fade_in=0.05, fade_out=0.3)
for e in cfg["events"]:
    add_event(mixl, e)
raw = os.path.join(out, "mix-raw.wav")
mixl.render(raw)

# two-pass loudness, then a limiter
r = subprocess.run(["ffmpeg", "-hide_banner", "-i", raw, "-af", f"loudnorm=I={TARGET_I}:TP={TARGET_TP}:LRA=11:print_format=json", "-f", "null", "-"], capture_output=True, text=True)
js = json.loads(r.stderr[r.stderr.rfind("{"):r.stderr.rfind("}") + 1])
mix = os.path.join(out, "mix.wav")
limit = 10 ** ((TARGET_TP - 0.0) / 20)
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", raw, "-af",
     f"loudnorm=I={TARGET_I}:TP={TARGET_TP}:LRA=11:measured_I={js['input_i']}:measured_TP={js['input_tp']}:measured_LRA={js['input_lra']}:measured_thresh={js['input_thresh']}:offset={js['target_offset']}:linear=true,alimiter=limit={limit:.3f}:attack=1:release=50:level=false,atrim=duration={seconds}",
     "-ar", "48000", "-c:a", "pcm_s24le", mix])

cut = os.path.join(out, "cut.mp4")
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", picture, "-i", mix, "-map", "0:v", "-map", "1:a", "-c:v", "copy",
     "-c:a", "aac", "-b:a", "256k", "-ar", "48000", "-t", f"{seconds}", "-movflags", "+faststart", cut])

# ---- evidence ----
os.makedirs(os.path.join(out, "stills"), exist_ok=True)
for f in glob.glob(os.path.join(out, "stills", "*.png")):
    os.remove(f)
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", cut, "-vf", f"select='not(mod(n\\,6))',scale=320:-2,tile=8x{(length // 6 + 7) // 8}:padding=2",
     "-frames:v", "1", "-update", "1", os.path.join(out, "filmstrip.png")])
keys = [s["cut_in"] + s["key"] for s in shots]
sel = "+".join(f"eq(n\\,{k})" for k in keys)
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", cut, "-vf", f"select='{sel}',scale=480:-2,tile={len(keys)}x1:padding=3",
     "-frames:v", "1", "-update", "1", os.path.join(out, "contact.png")])
picks = sorted(set(keys + [0, length - 1] + [int(length * q) for q in (0.2, 0.5)]))[:10]
for k in picks:
    run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", cut, "-vf", f"select='eq(n\\,{k})'", "-frames:v", "1", "-update", "1",
         os.path.join(out, "stills", f"f{k:04d}.png")])

# ---- metrics ----
probe = json.loads(run(["ffprobe", "-v", "error", "-show_streams", "-show_format", "-of", "json", cut]).stdout)
v = next(s for s in probe["streams"] if s["codec_type"] == "video")
a = next(s for s in probe["streams"] if s["codec_type"] == "audio")
eb = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-v", "verbose", "-i", cut, "-af", "ebur128=peak=true:framelog=verbose", "-f", "null", "-"],
                    capture_output=True, text=True).stderr
short = []
for line in eb.splitlines():
    if " t:" in line and " S:" in line:
        try:
            short.append((float(line.split(" t:")[1].split()[0]), float(line.split(" S:")[1].split()[0]), float(line.split(" M:")[1].split()[0])))
        except Exception:
            pass
summ = [l.strip() for l in eb.splitlines() if l.strip().startswith(("I:", "LRA:", "Peak:"))]
q = seconds / 4
first = [m for t, _, m in short if t <= q]
last = [m for t, _, m in short if t >= seconds - q]


def lmean(x):
    x = [v_ for v_ in x if v_ > -70]
    return 10 * np.log10(np.mean([10 ** (v_ / 10) for v_ in x])) if x else float("nan")


import librosa


def onsets_of(path):
    y, sr = librosa.load(path, sr=48000, mono=True)
    env = librosa.onset.onset_strength(y=y, sr=sr, hop_length=480)
    return librosa.onset.onset_detect(onset_envelope=env, sr=sr, hop_length=480, units="time") - 0.03


def nearest(on, t):
    return round((on[np.argmin(np.abs(on - t))] - t) * FPS, 1) if len(on) else float("nan")


mix_on = onsets_of(mix)
sync = []
for name, f in cfg.get("sync_points", {}).items():
    t = f / FPS
    lane_events = [e for e in cfg["events"] if e.get("sync") == name] or [e for e in cfg["events"] if abs(e["frame"] - f) <= 1]
    lane_off = float("nan")
    if lane_events:
        solo = Lanes()
        for e in lane_events:
            add_event(solo, e)
        solo_path = os.path.join(out, f"lane-{name}.wav")
        solo.render(solo_path)
        lane_off = nearest(onsets_of(solo_path), t)
        os.remove(solo_path)
    voice = any(e.get("voice") for e in lane_events)
    sync.append((name, f, nearest(mix_on, t), lane_off, voice))

sil = subprocess.run(["ffmpeg", "-hide_banner", "-i", cut, "-af", "silencedetect=noise=-40dB:d=0.25", "-f", "null", "-"], capture_output=True, text=True).stderr
blk = subprocess.run(["ffmpeg", "-hide_banner", "-i", cut, "-vf", "blackdetect=d=0.1:pix_th=0.1", "-an", "-f", "null", "-"], capture_output=True, text=True).stderr
with open(os.path.join(out, "metrics.txt"), "w") as f:
    f.write(f"duration {float(probe['format']['duration']):.3f} s; video {v['codec_name']} {v['width']}x{v['height']} {v['r_frame_rate']} fps, {v.get('nb_frames')} frames; audio {a['codec_name']} {a['sample_rate']} Hz {a['channels']} ch\n")
    f.write("loudness: " + " | ".join(summ) + "\n")
    f.write(f"momentary mean, first quarter {lmean(first):.1f} LUFS, last quarter {lmean(last):.1f} LUFS, ramp {lmean(last) - lmean(first):+.1f} LU\n")
    f.write("silence >0.25 s: " + ("; ".join(l.split("]")[-1].strip() for l in sil.splitlines() if "silence_start" in l) or "none") + "\n")
    f.write("black >0.1 s: " + ("; ".join(l.split("]")[-1].strip() for l in blk.splitlines() if "black_start" in l) or "none") + "\n")
    f.write("sync (name, planned frame, nearest onset in the mix, nearest onset in its own lane; + = sound after picture):\n")
    for name, fr, m_off, l_off, voice in sync:
        f.write(f"  {name} {fr} mix {m_off:+} lane {l_off:+}" + ("  NEEDS EARS (voice)" if voice else "") + "\n")
print(open(os.path.join(out, "metrics.txt")).read())
os.remove(raw)
os.remove(picture)
