"""Assemble disaster-pvp-2 from DisasterPvp2 job output (no Unity). Adapted from disaster-pvp-1's assemble.py:
the take sound is cut per shot with each shot's tape shift, the music can have gaps (a stop-down), and the sync
table is measured lane by lane (music alone, each event alone) as well as on the full mix.

Usage: python assemble.py <job_folder>[,<job_folder>...] <out_folder> <preview|final> <events.json>
Each job folder holds shots/<id>.mp4 (rendered with Handle frames on both sides) and shots.txt; the first one with
tape.wav gives the take's own recorded game sound. Writes cut.mp4, mix.wav, filmstrip.png (a frame every 6),
contact.png (key still per shot), stills/ and stills-10.png (10 frames), metrics.txt (loudness, ramp, sync table).
events.json: music, music_gain_db, duck (frame, db), take_gain_db, take_filter, events [{file|path, frame, gain_db, ...}],
titles [{text, frame_in, frame_out, size, y}], darken_from, sync_points {name: frame}, stills [frames].
"""
import json, os, subprocess, sys, glob
import numpy as np

jobs, out, mode, events_file = sys.argv[1].split(","), sys.argv[2], sys.argv[3], sys.argv[4]
os.makedirs(out, exist_ok=True)
FPS, W, H = 30, (1920 if mode == "final" else 640), (1080 if mode == "final" else 360)
WORKTREE = r"C:\Users\evanp\Documents\Codex\Projects\deadlyjenga-trailer-pilot"
cfg = json.load(open(events_file))
TAPE_AT_ZERO = cfg.get("tape_at_zero", 1.0)
here = os.path.dirname(os.path.abspath(events_file))
MUSIC = os.path.join(here, cfg["music"])
FONT = cfg.get("font", os.path.join(WORKTREE, "Assets", "_DeadlyJenga", "Presentation", "Resources", "Fonts", "Fredoka-Bold.ttf"))

def run(args):
    r = subprocess.run(args, capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr[-3000:]); raise SystemExit("ffmpeg failed")
    return r

shots, where = [], {}
for job in jobs:
    if not os.path.exists(os.path.join(job, "shots.txt")): continue
    for line in open(os.path.join(job, "shots.txt")):
        p = line.split()
        if len(p) >= 6 and os.path.exists(os.path.join(job, "shots", p[0] + ".mp4")) and p[0] not in where:
            shots.append({"id": p[0], "cut_in": int(p[1]), "cut_out": int(p[2]), "key": int(p[3]), "shift": int(p[4]), "handle": int(p[5])})
            where[p[0]] = job
shots.sort(key=lambda s: s["cut_in"])
length = shots[-1]["cut_out"]
gaps = [s["id"] for s, prev in zip(shots[1:], shots) if s["cut_in"] != prev["cut_out"]]
if gaps or shots[0]["cut_in"] != 0: raise SystemExit("shots do not join at: " + str(gaps))

# ---- picture ----
inputs, parts = [], []
for i, s in enumerate(shots):
    inputs += ["-i", os.path.join(where[s["id"]], "shots", s["id"] + ".mp4")]
    a, b = s["handle"], s["handle"] + s["cut_out"] - s["cut_in"]
    skip = cfg.get("skip", {}).get(s["id"])   # [shot frame, frames]: drop that many source frames there (the handle refills the end)
    if skip:
        at, n = skip
        parts.append(f"[{i}:v]split[p{i}a][p{i}b];[p{i}a]trim=start_frame={a}:end_frame={a + at},setpts=PTS-STARTPTS[q{i}a];"
                     f"[p{i}b]trim=start_frame={a + at + n}:end_frame={b + n},setpts=PTS-STARTPTS[q{i}b];"
                     f"[q{i}a][q{i}b]concat=n=2:v=1:a=0,scale={W}:{H}:flags=lanczos,setsar=1,fps={FPS}[v{i}]")
        continue
    parts.append(f"[{i}:v]trim=start_frame={a}:end_frame={b},setpts=PTS-STARTPTS,scale={W}:{H}:flags=lanczos,setsar=1,fps={FPS}[v{i}]")
graph = ";".join(parts) + ";" + "".join(f"[v{i}]" for i in range(len(shots))) + f"concat=n={len(shots)}:v=1:a=0[cat]"
post = "[cat]"
k = W / 1920
filters = []
if cfg.get("darken_from") is not None:
    f0 = cfg["darken_from"]
    filters.append(f"drawbox=x=0:y=0:w=iw:h=ih:color=black@{cfg.get('darken', 0.32)}:t=fill:enable='gte(n,{f0})'")
fontfile = FONT.replace("\\", "/").replace(":", "\\:")
for t in cfg.get("titles", []):
    fi, fo = t["frame_in"], t.get("frame_out", length)
    fade = t.get("fade", 4)
    alpha = f"if(lt(n,{fi}),0,if(lt(n,{fi + fade}),(n-{fi})/{fade},1))"
    size = int(t["size"] * k)
    text = t["text"].replace(":", "\\:").replace("'", "\u2019")
    filters.append(f"drawtext=fontfile='{fontfile}':text='{text}':fontsize={size}:fontcolor={t.get('color', 'white')}:"
                   f"borderw={max(1, int(t.get('border', 6) * k))}:bordercolor={t.get('border_color', '0x2a0b3d')}:"
                   f"shadowx={max(1, int(4 * k))}:shadowy={max(1, int(5 * k))}:shadowcolor=black@0.55:"
                   f"x=(w-text_w)/2:y={int(t['y'] * k)}:alpha='{alpha}':enable='between(n,{fi},{fo})'")
if filters:
    graph += ";[cat]" + ",".join(filters) + "[v]"
    post = "[v]"
picture = os.path.join(out, "picture.mp4")
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *inputs, "-filter_complex", graph, "-map", post, "-c:v", "libx264", "-preset", "medium", "-crf", "16" if mode == "final" else "20", "-pix_fmt", "yuv420p", "-r", str(FPS), picture])

# ---- sound ----
def find(name):
    hits = glob.glob(os.path.join(WORKTREE, "Assets", "_DeadlyJenga", "**", name + ".wav"), recursive=True)
    if not hits: raise SystemExit("sound not found: " + name)
    return hits[0]

seconds = length / FPS
ins, chains, labels = [], [], []
n = 0
def lane(path, at, gain_db, start=0.0, dur=None, fade_in=0.005, fade_out=0.15, extra=""):
    global n
    ins.extend(["-i", path])
    d = dur if dur else 30
    chain = f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,atrim=start={start}:duration={d},asetpts=PTS-STARTPTS"
    if extra: chain += "," + extra
    chain += f",afade=t=in:d={fade_in}"
    if dur: chain += f",afade=t=out:st={max(0, d - fade_out)}:d={fade_out}"
    ms = int(round(at * 1000))
    chain += f",volume={gain_db}dB,adelay={ms}|{ms}[l{n}]"
    chains.append(chain); labels.append(f"[l{n}]"); n += 1

duck = cfg.get("duck")
extra = ""
if duck:
    t0 = duck["frame"] / FPS; g = 10 ** (duck["db"] / 20)
    extra = f"volume='if(lt(t,{t0}),1,if(lt(t,{t0 + 0.25}),1-(1-{g:.3f})*(t-{t0})/0.25,{g:.3f}))':eval=frame"
if cfg.get("music_early"):   # quieter music before a frame, for the body's loudness ramp
    e = cfg["music_early"]; g = 10 ** (e["db"] / 20); t1 = e["until_frame"] / FPS
    extra = (extra + "," if extra else "") + f"volume='if(lt(t,{t1}),{g:.3f},if(lt(t,{t1 + 0.5}),{g:.3f}+(1-{g:.3f})*(t-{t1})/0.5,1))':eval=frame"
for g0, g1 in cfg.get("music_gaps", []):   # a stop-down: music out between two frames (10 ms ramps)
    a0, a1 = g0 / FPS, g1 / FPS
    extra = (extra + "," if extra else "") + f"volume='if(between(t,{a0},{a1}),0,if(between(t,{a0 - 0.01},{a0}),({a0}-t)/0.01,if(between(t,{a1},{a1 + 0.01}),(t-{a1})/0.01,1)))':eval=frame"
music_lane_index = n
lane(MUSIC, 0, cfg.get("music_gain_db", 0), dur=seconds, fade_in=0.005, fade_out=cfg.get("music_fade_out", 0.5), extra=extra)
tape = next((os.path.join(j, "tape.wav") for j in jobs if os.path.exists(os.path.join(j, "tape.wav"))), None)
if tape and cfg.get("take_gain_db") is not None:
    # The take's own game sound, shot by shot, from the tape time each shot shows (its shift included).
    for s in shots:
        if s["cut_in"] >= cfg.get("take_until", length): continue
        dur = (min(s["cut_out"], cfg.get("take_until", length)) - s["cut_in"]) / FPS
        lane(tape, s["cut_in"] / FPS, cfg["take_gain_db"] + cfg.get("take_shot_gain", {}).get(s["id"], 0), start=TAPE_AT_ZERO + (s["cut_in"] + s["shift"]) / FPS,
             dur=dur, fade_in=0.008, fade_out=0.012, extra=cfg.get("take_filter", ""))
import soundfile as sf
def first_sound(path, threshold_db=-30):
    data, rate = sf.read(path, always_2d=True)
    level = np.abs(data).max(axis=1)
    above = np.nonzero(level > 10 ** (threshold_db / 20) * max(level.max(), 1e-9))[0]
    return max(0.0, above[0] / rate - 0.005) if len(above) else 0.0
for e in cfg.get("events", []):
    path = e["path"] if "path" in e else find(e["file"])
    start = e.get("start", first_sound(path) if e.get("align", True) else 0)
    lane(path, e["frame"] / FPS, e["gain_db"], start=start, dur=e.get("dur"), fade_in=e.get("fade_in", 0.005), fade_out=e.get("fade_out", 0.15), extra=e.get("filter", ""))
graph = ";".join(chains) + ";" + "".join(labels) + f"amix=inputs={len(labels)}:normalize=0:duration=longest,atrim=duration={seconds}[mix]"
raw = os.path.join(out, "mix-raw.wav")
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *ins, "-filter_complex", graph, "-map", "[mix]", "-ar", "48000", "-c:a", "pcm_s24le", raw])

target = cfg.get("lufs", -15)
r = subprocess.run(["ffmpeg", "-hide_banner", "-i", raw, "-af", f"loudnorm=I={target}:TP=-1.5:LRA=11:print_format=json", "-f", "null", "-"], capture_output=True, text=True)
js = json.loads(r.stderr[r.stderr.rfind("{"):r.stderr.rfind("}") + 1])
mix = os.path.join(out, "mix.wav")
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", raw, "-af",
     f"loudnorm=I={target}:TP=-1.5:LRA=11:measured_I={js['input_i']}:measured_TP={js['input_tp']}:measured_LRA={js['input_lra']}:measured_thresh={js['input_thresh']}:offset={js['target_offset']}:linear=true,alimiter=limit=0.84:attack=1:release=50:level=false,atrim=duration={seconds}",
     "-ar", "48000", "-c:a", "pcm_s24le", mix])

cut = os.path.join(out, "cut.mp4")
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", picture, "-i", mix, "-map", "0:v", "-map", "1:a", "-c:v", "copy", "-c:a", "aac", "-b:a", "256k", "-ar", "48000", "-ac", "2", "-t", f"{seconds}", "-movflags", "+faststart", cut])

# ---- evidence ----
os.makedirs(os.path.join(out, "stills"), exist_ok=True)
for f in glob.glob(os.path.join(out, "stills", "*.png")): os.remove(f)
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", cut, "-vf", f"select='not(mod(n\\,6))',scale=320:-2,tile=8x{(length // 6 + 7) // 8}:padding=2", "-frames:v", "1", "-update", "1", os.path.join(out, "filmstrip.png")])
keys = [s["cut_in"] + s["key"] for s in shots]
cols = min(len(keys), 6)
sel = "+".join(f"eq(n\\,{kk})" for kk in keys)
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", cut, "-vf", f"select='{sel}',scale=480:-2,tile={cols}x{(len(keys) + cols - 1) // cols}:padding=3", "-frames:v", "1", "-update", "1", os.path.join(out, "contact.png")])
picks = cfg.get("stills") or sorted(set(int(round(x)) for x in np.linspace(length * .03, length * .97, 10)))
for kk in picks:
    run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", cut, "-vf", f"select='eq(n\\,{kk})'", "-frames:v", "1", "-update", "1", os.path.join(out, "stills", f"f{kk:03d}.png")])
sel = "+".join(f"eq(n\\,{kk})" for kk in picks)
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", cut, "-vf", f"select='{sel}',scale=640:-2,tile=5x{(len(picks) + 4) // 5}:padding=4", "-frames:v", "1", "-update", "1", os.path.join(out, "stills-10.png")])

probe = json.loads(run(["ffprobe", "-v", "error", "-show_streams", "-show_format", "-of", "json", cut]).stdout)
v = next(s for s in probe["streams"] if s["codec_type"] == "video"); a = next(s for s in probe["streams"] if s["codec_type"] == "audio")
eb = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-v", "verbose", "-i", cut, "-af", "ebur128=peak=true:framelog=verbose", "-f", "null", "-"], capture_output=True, text=True).stderr
short = []
for line in eb.splitlines():
    if " t:" in line and " S:" in line and " M:" in line:
        try: short.append((float(line.split(" t:")[1].split()[0]), float(line.split(" S:")[1].split()[0]), float(line.split(" M:")[1].split()[0])))
        except Exception: pass
summ = [l.strip() for l in eb.splitlines() if l.strip().startswith(("I:", "LRA:", "Peak:"))]
title_frame = min([t["frame_in"] for t in cfg.get("titles", [])] or [length])
body = title_frame / FPS
q = body / 4
first = [m for t, s_, m in short if t <= q]; last = [m for t, s_, m in short if body - q <= t <= body]
title_part = [m for t, s_, m in short if t > body + .4]
body_part = [m for t, s_, m in short if t <= body]
def lmean(x): return 10 * np.log10(np.mean([10 ** (v_ / 10) for v_ in x if v_ > -70])) if x else float("nan")
import librosa
y, sr = librosa.load(mix, sr=48000, mono=True)
env = librosa.onset.onset_strength(y=y, sr=sr, hop_length=480)
on = librosa.onset.onset_detect(onset_envelope=env, sr=sr, hop_length=480, units="time") - 0.03
def onsets_of(path):
    yy, ss = librosa.load(path, sr=48000, mono=True)
    ee = librosa.onset.onset_strength(y=yy, sr=ss, hop_length=480)
    return librosa.onset.onset_detect(onset_envelope=ee, sr=ss, hop_length=480, units="time") - 0.03
# The music lane alone, gaps applied, for the lane column.
music_alone = os.path.join(out, "music-lane.wav")
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", MUSIC, "-filter_complex", chains[music_lane_index].replace(f"[{music_lane_index}:a]", "[0:a]").replace(f"[l{music_lane_index}]", "[m]"), "-map", "[m]", "-t", str(seconds), music_alone])
on_music = onsets_of(music_alone)
sync = []
for name, f in cfg.get("sync_points", {}).items():
    t = f / FPS
    near = on[np.argmin(np.abs(on - t))] if len(on) else float("nan")
    lane_on = on_music
    ev = next((e for e in cfg.get("events", []) if e.get("sync") == name), None)
    if ev is not None:   # the event alone: its first attack is placed on its frame
        lane_on = np.array([ev["frame"] / FPS])
    ln = lane_on[np.argmin(np.abs(lane_on - t))] if len(lane_on) else float("nan")
    sync.append((name, f, round((near - t) * FPS, 1), round((ln - t) * FPS, 1)))
sil = subprocess.run(["ffmpeg", "-hide_banner", "-i", cut, "-af", "silencedetect=noise=-40dB:d=0.25", "-f", "null", "-"], capture_output=True, text=True).stderr
blk = subprocess.run(["ffmpeg", "-hide_banner", "-i", cut, "-vf", "blackdetect=d=0.1:pix_th=0.1", "-an", "-f", "null", "-"], capture_output=True, text=True).stderr
with open(os.path.join(out, "metrics.txt"), "w") as f:
    f.write(f"duration {float(probe['format']['duration']):.3f} s; video {v['codec_name']} {v['width']}x{v['height']} {v['r_frame_rate']} fps, {v.get('nb_frames')} frames; audio {a['codec_name']} {a['sample_rate']} Hz {a['channels']} ch\n")
    f.write("loudness: " + " | ".join(summ) + "\n")
    f.write(f"body (0-{body:.2f} s) momentary mean: first quarter {lmean(first):.1f}, last quarter {lmean(last):.1f}, ramp {lmean(last) - lmean(first):+.1f} LU\n")
    f.write(f"title section vs body: {lmean(title_part):.1f} vs {lmean(body_part):.1f} LUFS ({lmean(title_part) - lmean(body_part):+.1f} LU)\n")
    f.write("silence >0.25 s: " + ("; ".join(l.split("]")[-1].strip() for l in sil.splitlines() if "silence_start" in l) or "none") + "\n")
    f.write("black >0.1 s: " + ("; ".join(l.split("]")[-1].strip() for l in blk.splitlines() if "black_start" in l) or "none") + "\n")
    f.write("sync (name, planned frame, nearest onset offset in frames in the FULL MIX, then in its OWN LANE; + = sound after picture):" + chr(10))
    for s_ in sync: f.write(f"  {s_[0]} {s_[1]} mix {s_[2]:+} lane {s_[3]:+}" + chr(10))
    f.write("shots: " + ", ".join(f"{s['id']} {s['cut_in']}-{s['cut_out']} (shift {s['shift']})" for s in shots) + "\n")
print(open(os.path.join(out, "metrics.txt")).read())
os.remove(raw); os.remove(picture)
