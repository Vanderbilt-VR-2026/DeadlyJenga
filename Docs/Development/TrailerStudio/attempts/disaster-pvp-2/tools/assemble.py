"""Assemble disaster-pvp-2 from DisasterPvp2 job output (no Unity). Adapted from disaster-pvp-1's assemble.py:
the take sound is cut per shot with each shot's tape shift, the music can have gaps (a stop-down), and the sync
table is measured lane by lane (music alone, each event alone) as well as on the full mix.

Usage: python assemble.py <job_folder>[,<job_folder>...] <out_folder> <preview|final> <events.json> [options]
Split jobs may repeat shot declarations, but each shot needs exactly one clip.
When any job has render-manifest.json, every job must have matching provenance and verified input hashes.
Music and explicit config paths are relative to events.json. Use --help for options.
Writes cut.mp4, mix.wav, filmstrip.png (a frame every 6),
contact.png (key still per shot), stills/ and stills-10.png (10 frames), metrics.txt (loudness, ramp, sync table).
events.json: music, music_gain_db, duck (frame, db), take_gain_db, take_filter, events [{file|path, frame, gain_db, ...}],
titles [{text, frame_in, frame_out, size, y}], darken_from, sync_points {name: frame}, stills [frames].
"""
import argparse, glob, hashlib, json, os, re, shutil, subprocess
from fractions import Fraction
from pathlib import Path

import librosa
import numpy as np
import soundfile as sf

FPS = 30
PROJECT = Path(__file__).resolve().parents[6]
DEFAULT_SOUND_ROOT = PROJECT / "Assets" / "_DeadlyJenga"
DEFAULT_FONT = DEFAULT_SOUND_ROOT / "Presentation" / "Resources" / "Fonts" / "Fredoka-Bold.ttf"

def run(args):
    r = subprocess.run(args, capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr[-3000:]); raise SystemExit("ffmpeg failed")
    return r

def required_file(path):
    if not path.is_file() or path.stat().st_size == 0:
        raise ValueError(f"Missing or empty input: {path}")
    return path


def load_shots(jobs, expected_frames):
    declarations, clips = {}, {}
    for job in jobs:
        manifest = required_file(job / "shots.txt")
        declared_here = {}
        for number, line in enumerate(manifest.read_text(encoding="utf-8-sig").splitlines(), 1):
            fields = line.split()
            if not fields:
                continue
            if len(fields) != 6 or not re.fullmatch(r"[A-Za-z0-9_-]+", fields[0]):
                raise ValueError(f"Invalid shot row: {manifest}:{number}")
            shot_id = fields[0].upper()
            if shot_id in declared_here:
                raise ValueError(f"Duplicate declaration for {shot_id} in {manifest}")
            cut_in, cut_out, key, shift, handle = map(int, fields[1:])
            if not 0 <= cut_in < cut_out or not 0 <= key < cut_out - cut_in or handle < 0:
                raise ValueError(f"Invalid cut, key or handle for {shot_id} in {manifest}")
            shot = dict(id=shot_id, cut_in=cut_in, cut_out=cut_out, key=key, shift=shift, handle=handle)
            if shot_id in declarations and declarations[shot_id] != shot:
                raise ValueError(f"Conflicting declarations for {shot_id} in {manifest}")
            declared_here[shot_id] = declarations[shot_id] = shot

        job_clips = sorted((job / "shots").glob("*.mp4"))
        if not job_clips:
            raise ValueError(f"No shot clips in {job / 'shots'}")
        for clip in job_clips:
            shot_id = clip.stem.upper()
            if shot_id not in declared_here:
                raise ValueError(f"Shot clip has no declaration in {manifest}: {clip}")
            if shot_id in clips:
                raise ValueError(f"Duplicate clip for {shot_id}: {clips[shot_id]} and {clip}")
            clips[shot_id] = required_file(clip)

    shots = sorted(declarations.values(), key=lambda shot: shot["cut_in"])
    end = 0
    for shot in shots:
        if shot["cut_in"] != end:
            raise ValueError(f"Gap or overlap before {shot['id']}: expected frame {end}")
        end = shot["cut_out"]
        if shot["id"] not in clips:
            raise ValueError(f"Missing clip for {shot['id']}")
    if end != expected_frames:
        raise ValueError(f"Cut ends at frame {end}; expected {expected_frames}")
    return shots, clips


def file_sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def manifest_hash(value, field, *, revision=False):
    pattern = r"(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})" if revision else r"[0-9a-fA-F]{64}"
    if not isinstance(value, str) or not re.fullmatch(pattern, value):
        raise ValueError(f"Invalid manifest {field}: {value!r}")
    return value.lower()


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate manifest key: {key}")
        result[key] = value
    return result


def verify_hash(path, expected):
    if file_sha256(required_file(path)) != expected:
        raise ValueError(f"SHA-256 mismatch: {path}")


def validate_render_manifests(jobs, clips, explicit_tape):
    manifests = [job / "render-manifest.json" for job in jobs]
    if not any(os.path.lexists(path) for path in manifests):
        return {}
    reference = None
    clip_metadata = {}
    for path in manifests:
        try:
            manifest = json.loads(required_file(path).read_text(encoding="utf-8-sig"), object_pairs_hook=unique_object)
            if type(manifest["version"]) is not int or manifest["version"] != 1:
                raise ValueError("Expected render-manifest version 1")
            identity = {
                "revision": manifest_hash(manifest["revision"], "revision", revision=True),
                **{field: manifest_hash(manifest[field], field)
                   for field in ("takeSha256", "setSha256", "cueLogSha256")},
                "audioSha256": None if manifest["audioSha256"] == "" else manifest["audioSha256"],
            }
            if identity["audioSha256"] is not None:
                identity["audioSha256"] = manifest_hash(identity["audioSha256"], "audioSha256")
            if reference is None:
                reference = identity
            differences = [field for field in reference if reference[field] != identity[field]]
            if differences:
                raise ValueError(f"Render provenance differs across jobs: {', '.join(differences)}")

            job = path.parent
            verify_hash(job / "set-rendered.json", identity["setSha256"])
            verify_hash(job / "cuelog.txt", identity["cueLogSha256"])
            tape = job / "tape.wav"
            if identity["audioSha256"] is not None:
                verify_hash(tape, identity["audioSha256"])
            elif os.path.lexists(tape):
                raise ValueError("tape.wav is present but the manifest declares no audio")

            declared = {}
            if not isinstance(manifest["clips"], list):
                raise ValueError("Expected a manifest clips array")
            for clip in manifest["clips"]:
                shot_id = clip["id"]
                if not isinstance(shot_id, str) or not re.fullmatch(r"[A-Z0-9_-]+", shot_id):
                    raise ValueError(f"Invalid manifest clip ID: {shot_id!r}")
                if shot_id in declared:
                    raise ValueError(f"Duplicate manifest clip: {shot_id}")
                declared[shot_id] = manifest_hash(clip["sha256"], f"{shot_id} sha256")
                metadata = {field: clip[field] for field in ("frames", "fps", "width", "height")}
                if any(type(value) is not int or value <= 0 for value in metadata.values()):
                    raise ValueError(f"Invalid encoded media metadata for {shot_id}: {metadata}")
                clip_metadata[shot_id] = metadata
            actual = {shot_id: clip for shot_id, clip in clips.items() if clip.parent.parent == job}
            if declared.keys() != actual.keys():
                missing = sorted(actual.keys() - declared.keys())
                extra = sorted(declared.keys() - actual.keys())
                raise ValueError(f"Manifest clip list differs from files (undeclared: {missing}, missing files: {extra})")
            for shot_id, clip in actual.items():
                verify_hash(clip, declared[shot_id])
        except (KeyError, OSError, TypeError, ValueError) as error:
            raise ValueError(f"{path}: {error}") from error
    if explicit_tape:
        if reference["audioSha256"] is None:
            raise ValueError("--tape cannot override a render manifest with no audio")
        verify_hash(explicit_tape.resolve(), reference["audioSha256"])
    return clip_metadata


def read_stream(path, selector, *options):
    result = subprocess.run(
        ["ffprobe", "-v", "error", "-select_streams", selector, *options,
         "-show_streams", "-of", "json", str(path)],
        capture_output=True, text=True,
    )
    if result.returncode or result.stderr.strip():
        raise ValueError(f"Cannot read {selector} stream in {path}: {result.stderr.strip()}")
    streams = json.loads(result.stdout)["streams"]
    if len(streams) != 1:
        raise ValueError(f"Expected one {selector} stream in {path}")
    return streams[0]


def validate_clips(shots, clips, skips, clip_metadata):
    unknown = skips.keys() - clips.keys()
    if unknown:
        raise ValueError(f"Skip refers to unknown shots: {', '.join(sorted(unknown))}")
    dimensions = None
    for shot in shots:
        shot_id = shot["id"]
        duration = shot["cut_out"] - shot["cut_in"]
        handle = shot["handle"]
        if shot_id in skips:
            at, count = skips[shot_id]
            if (type(at) is not int or type(count) is not int
                    or not 0 < at < duration or not 0 < count <= handle):
                raise ValueError(f"Invalid skip for {shot_id}: {skips[shot_id]}")
        clip = clips[shot_id]
        stream = read_stream(clip, "v", "-count_frames")
        rate = Fraction(stream.get("avg_frame_rate", "0/1"))
        frames = int(stream.get("nb_read_frames", 0))
        required_frames = duration + 2 * handle
        if rate != FPS or frames < required_frames:
            raise ValueError(f"{clip}: need at least {required_frames} frames at {FPS} fps, got {frames} at {rate} fps")
        if clip_metadata:
            actual = dict(frames=frames, fps=rate, width=stream["width"], height=stream["height"])
            expected = clip_metadata[shot_id]
            if actual != expected:
                raise ValueError(f"{clip}: encoded media differs from manifest (expected {expected}, got {actual})")
            size = actual["width"], actual["height"]
            if dimensions is None:
                dimensions = size
            elif dimensions != size:
                raise ValueError(f"{clip}: encoded dimensions {size} do not match other manifest clips {dimensions}")


def find_sound(name, root, sound_files):
    if not isinstance(name, str) or not name or any(char in name for char in "/\\:*?[]"):
        raise ValueError(f"Invalid sound basename: {name!r}; use path for explicit files")
    filename = (name + ".wav").casefold()
    hits = sound_files.get(filename, [])
    if len(hits) != 1:
        matches = ", ".join(map(str, hits)) or "no matches"
        raise ValueError(f"Expected one sound named {name}.wav under {root}: {matches}")
    return required_file(hits[0])


def audio_duration(path):
    stream = read_stream(path, "a")
    duration = float(stream.get("duration", "nan"))
    if not np.isfinite(duration) or duration <= 0:
        raise ValueError(f"Audio has no positive duration: {path}")
    return duration


def take_audio(args, jobs):
    if args.tape:
        return required_file(args.tape.resolve())
    tapes = [required_file(job / "tape.wav") for job in jobs if (job / "tape.wav").exists()]
    if not tapes:
        raise ValueError("Take audio is enabled, but no job contains tape.wav")
    if len(tapes) > 1:
        checksums = {file_sha256(path) for path in tapes}
        if len(checksums) != 1:
            raise ValueError("Jobs contain different tape.wav files; select the intended take with --tape")
    return tapes[0]


def first_sound(path, threshold_db=-30):
    data, rate = sf.read(path, always_2d=True)
    level = np.abs(data).max(axis=1)
    above = np.nonzero(level > 10 ** (threshold_db / 20) * max(level.max(), 1e-9))[0]
    return max(0.0, above[0] / rate - 0.005) if len(above) else 0.0


parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
parser.add_argument("jobs", help="Comma-separated render job directories")
parser.add_argument("out", type=Path)
parser.add_argument("mode", choices=("preview", "final"))
parser.add_argument("events_file", type=Path)
parser.add_argument("--expected-frames", type=int, default=450, help="Completed cut length (default: 450)")
parser.add_argument("--sound-root", type=Path, default=DEFAULT_SOUND_ROOT)
parser.add_argument("--tape", type=Path, help="Explicit take audio when job copies differ")
parser.add_argument("--preflight-only", action="store_true", help="Validate inputs without writing any output")
args = parser.parse_args()

try:
    if args.expected_frames <= 0:
        raise ValueError("--expected-frames must be positive")
    job_names = args.jobs.split(",")
    if not all(job_names):
        raise ValueError("Job list contains an empty directory")
    jobs = [Path(job).resolve() for job in job_names]
    if len(set(jobs)) != len(jobs):
        raise ValueError("Duplicate job directory")
    for command in ("ffmpeg", "ffprobe"):
        if not shutil.which(command):
            raise ValueError(f"Required executable was not found on PATH: {command}")

    events_file = required_file(args.events_file.resolve())
    cfg = json.loads(events_file.read_text(encoding="utf-8-sig"))
    here = events_file.parent
    shots, clips = load_shots(jobs, args.expected_frames)
    clip_metadata = validate_render_manifests(jobs, clips, args.tape)
    length = args.expected_frames
    validate_clips(shots, clips, cfg.get("skip", {}), clip_metadata)

    MUSIC = str(required_file(here / cfg["music"]))
    audio_duration(MUSIC)
    FONT = str(required_file(here / cfg.get("font", DEFAULT_FONT))) if cfg.get("titles") else ""
    TAPE_AT_ZERO = cfg.get("tape_at_zero", 1.0)
    tape = None
    take_until = cfg.get("take_until", length)
    if type(take_until) is not int or not 0 <= take_until <= length:
        raise ValueError(f"take_until is outside the cut: {take_until}")
    if cfg.get("take_gain_db") is not None and take_until:
        tape = take_audio(args, jobs)
        duration = audio_duration(tape)
        for shot in shots:
            if shot["cut_in"] < take_until:
                start = TAPE_AT_ZERO + (shot["cut_in"] + shot["shift"]) / FPS
                end = TAPE_AT_ZERO + (min(shot["cut_out"], take_until) + shot["shift"]) / FPS
                if not 0 <= start < end <= duration + 1 / FPS:
                    raise ValueError(f"{tape} does not cover the take audio for {shot['id']} ({start:.3f}–{end:.3f}s)")
    for frame in [*cfg.get("stills", []), *cfg.get("sync_points", {}).values()]:
        if type(frame) is not int or not 0 <= frame < length:
            raise ValueError(f"Evidence frame outside cut: {frame}")
    for title in cfg.get("titles", []):
        first, last = title["frame_in"], title.get("frame_out", length)
        if type(first) is not int or type(last) is not int or not 0 <= first < last <= length:
            raise ValueError(f"Invalid title frame range: {first}–{last}")
    events = cfg.get("events", [])
    sound_files = {}
    if any("file" in event for event in events):
        for path in sorted(args.sound_root.rglob("*")):
            if path.suffix.casefold() == ".wav" and path.is_file():
                sound_files.setdefault(path.name.casefold(), []).append(path)
    event_sources = []
    for event in events:
        if ("path" in event) == ("file" in event):
            raise ValueError(f"Event at frame {event.get('frame')} needs exactly one of path or file")
        if type(event["frame"]) is not int or not 0 <= event["frame"] < length:
            raise ValueError(f"Event frame outside cut: {event['frame']}")
        path = required_file(here / event["path"]) if "path" in event else find_sound(event["file"], args.sound_root, sound_files)
        duration = audio_duration(path)
        start = event.get("start")
        if start is None:
            start = first_sound(path) if event.get("align", True) else 0
        if not 0 <= start < duration:
            raise ValueError(f"Event trim starts outside {path}: {start}")
        event_sources.append((str(path), start))
except (AttributeError, KeyError, OSError, RuntimeError, TypeError, ValueError, ZeroDivisionError) as error:
    parser.exit(2, f"Preflight failed: {error}\n")

if args.preflight_only:
    parser.exit(0, f"Preflight passed: {len(shots)} clips, {length} frames, {len(event_sources)} audio events.\n")

out, mode = args.out, args.mode
W, H = (1920, 1080) if mode == "final" else (640, 360)

# ---- picture ----
inputs, parts = [], []
for i, s in enumerate(shots):
    inputs += ["-i", str(clips[s["id"]])]
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
picture_graph = graph

# ---- sound ----
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
if tape:
    # The take's own game sound, shot by shot, from the tape time each shot shows (its shift included).
    for s in shots:
        if s["cut_in"] >= take_until: continue
        dur = (min(s["cut_out"], take_until) - s["cut_in"]) / FPS
        lane(tape, s["cut_in"] / FPS, cfg["take_gain_db"] + cfg.get("take_shot_gain", {}).get(s["id"], 0), start=TAPE_AT_ZERO + (s["cut_in"] + s["shift"]) / FPS,
             dur=dur, fade_in=0.008, fade_out=0.012, extra=cfg.get("take_filter", ""))
for e, (path, start) in zip(events, event_sources):
    lane(path, e["frame"] / FPS, e["gain_db"], start=start, dur=e.get("dur"), fade_in=e.get("fade_in", 0.005), fade_out=e.get("fade_out", 0.15), extra=e.get("filter", ""))
graph = ";".join(chains) + ";" + "".join(labels) + f"amix=inputs={len(labels)}:normalize=0:duration=longest,atrim=duration={seconds}[mix]"
raw = os.path.join(out, "mix-raw.wav")
out.mkdir(parents=True, exist_ok=True)
run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *inputs, "-filter_complex", picture_graph, "-map", post, "-c:v", "libx264", "-preset", "medium", "-crf", "16" if mode == "final" else "20", "-pix_fmt", "yuv420p", "-r", str(FPS), picture])
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
