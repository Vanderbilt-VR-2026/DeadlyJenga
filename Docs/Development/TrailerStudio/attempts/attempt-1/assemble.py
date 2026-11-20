"""Flatten the Trailer Studio cut into one MP4 (the Studio only writes FCPXML/EDL + per-shot media + cut-sound.wav).

Usage: python assemble.py <renders folder> <output.mp4> [--title "TEXT"] [--title-from SECONDS]
Reads pieces.tsv written by TrailerAttempt1 (StudioExport.Resolve output), uses each shot's PNG sequence,
centres dissolves on the edit point like the Studio's EDL, and muxes the Studio's cut-sound.wav.
"""
import csv, os, subprocess, sys, argparse

ap = argparse.ArgumentParser()
ap.add_argument("renders"); ap.add_argument("out")
ap.add_argument("--title", default=""); ap.add_argument("--title-from", type=float, default=0)
ap.add_argument("--sub", default="")
ap.add_argument("--audio", default="")
ap.add_argument("--music", default="")
ap.add_argument("--music-db", type=float, default=0)
ap.add_argument("--sfx-db", type=float, default=0)
a = ap.parse_args()

rows = list(csv.DictReader(open(os.path.join(a.renders, "pieces.tsv"), encoding="utf-8"), delimiter="\t"))
fps = int(rows[0]["fps"])
def shot_frames(folder):
    return len([f for f in os.listdir(folder) if f.endswith(".png")])

pieces = []
for r in rows:
    folder = os.path.join(a.renders, os.path.basename(os.path.dirname(r["media"])))
    # media is the movie or first picture; use the folder's PNG sequence (exact frames)
    p = dict(folder=folder, inF=int(r["inFrame"]), n=int(r["frames"]), rec=int(r["recordStart"]),
             dis=float(r["dissolve"]), total=shot_frames(folder), name=r["shot"])
    pieces.append(p)

# dissolve lengths (same rule as StudioExport.Dissolve)
for i, p in enumerate(pieces):
    p["head"] = p["len"] = 0
    if i == 0 or p["dis"] <= 0: continue
    length = max(2, round(p["dis"] * fps)); before = pieces[i - 1]
    spare_after = before["total"] - (before["inF"] + before["n"]); spare_before = p["inF"]
    head = length // 2; tail = length - head
    if head > spare_before or tail > spare_after:
        head = tail = max(0, min(head, spare_before, spare_after)); length = head + tail
    if length >= 2: p["head"], p["len"] = head, length

inputs, filters = [], []
for i, p in enumerate(pieces):
    start = p["inF"] - p["head"]
    end = p["inF"] + p["n"]
    if i + 1 < len(pieces): end += pieces[i + 1]["len"] - pieces[i + 1]["head"]
    inputs += ["-framerate", str(fps), "-start_number", str(start), "-i", os.path.join(p["folder"], "%05d.png")]
    filters.append(f"[{i}:v]trim=end_frame={end - start},setpts=PTS-STARTPTS,settb=1/{fps},fps={fps},format=yuv420p[v{i}]")
    p["clip_len"] = end - start

cur = "v0"; acc = pieces[0]["clip_len"]
for i in range(1, len(pieces)):
    p = pieces[i]
    if p["len"] > 0:
        off = (acc - p["len"]) / fps
        filters.append(f"[{cur}][v{i}]xfade=transition=fade:duration={p['len']/fps:.4f}:offset={off:.4f}[x{i}]")
        acc = acc - p["len"] + p["clip_len"]
    else:
        filters.append(f"[{cur}][v{i}]concat=n=2:v=1:a=0,settb=1/{fps}[x{i}]")
        acc += p["clip_len"]
    cur = f"x{i}"
if a.title:
    font = "C\\:/Windows/Fonts/impact.ttf"
    t0 = a.title_from
    alpha = f"if(lt(t,{t0}),0,min(1,(t-{t0})/0.35))"
    filters.append(f"[{cur}]drawtext=fontfile='{font}':text='{a.title}':fontsize=h/7:fontcolor=white:borderw=6:bordercolor=black@0.85:"
                   f"x=(w-text_w)/2:y=(h-text_h)/2-h/14:alpha='{alpha}'" + (
                   f",drawtext=fontfile='{font}':text='{a.sub}':fontsize=h/22:fontcolor=white:borderw=3:bordercolor=black@0.85:x=(w-text_w)/2:y=(h/2)+h/12:alpha='{alpha}'" if a.sub else "") + "[vt]")
    cur = "vt"
audio = a.audio or os.path.join(a.renders, "cut-sound.wav")
inputs += ["-i", audio]
amap = f"{len(pieces)}:a"
total = acc / fps
if a.music or a.sfx_db:
    filters.append(f"[{len(pieces)}:a]volume={a.sfx_db}dB,aformat=sample_rates=48000:channel_layouts=stereo[sfx]")
    if a.music:
        inputs += ["-i", a.music]
        filters.append(f"[{len(pieces)+1}:a]atrim=0:{total},asetpts=PTS-STARTPTS,volume={a.music_db}dB,afade=t=out:st={total-1.2}:d=1.2,aformat=sample_rates=48000:channel_layouts=stereo[mus]")
        filters.append("[sfx][mus]amix=inputs=2:normalize=0:duration=first,alimiter=limit=0.89[aout]")
    else:
        filters.append("[sfx]alimiter=limit=0.89[aout]")
    amap = "[aout]"
cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"] + inputs + [
    "-filter_complex", ";".join(filters), "-map", f"[{cur}]", "-map", amap,
    "-c:v", "libx264", "-preset", "slow", "-crf", "17", "-pix_fmt", "yuv420p", "-r", str(fps),
    "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", a.out]
print("frames:", acc, "=", acc / fps, "s")
for p in pieces: print(p["name"], "in", p["inF"], "n", p["n"], "head", p["head"], "dissolve", p["len"], "clip", p["clip_len"])
subprocess.run(cmd, check=True)
print("wrote", a.out)
