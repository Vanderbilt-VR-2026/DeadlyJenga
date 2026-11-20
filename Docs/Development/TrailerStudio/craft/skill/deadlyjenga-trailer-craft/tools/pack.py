"""Evidence pack for a render (review-rubric.md section 2), plus an optional planted-defect copy.

Usage:
  python pack.py <job_folder> <assembled_folder> <pack_folder>
         [--plant <defect_pack_folder> --shift-sync NAME --drop-sync NAME --swap-still SHOT-key:SHOT-last]

Writes to <pack_folder>: framing.md (the engine's framing numbers per shot at first, key and last
frame, from shots/<id>-metrics.csv), the stills shots/<id>-first|key|last.png, contact.png,
filmstrip.png, metrics.txt (from assemble.py) and take-log.md (the "Take (" section of the job's
notes.md, if any).
With --plant it also writes a second pack with three injected defects and records them in
planted.txt BESIDE the packs (never inside them), for the critic's planted-defect check:
  --shift-sync NAME      that sync line moved 5 frames late (default: main)
  --drop-sync NAME       that sync line replaced by "no onset within 10 frames" (an impact sound removed)
  --swap-still A:B       stills/A.png replaced by stills/B.png (e.g. S02-key:S01-last, a face shot replaced
                         by a frame where the subject is hidden or absent)
Give the critic both packs in random order and do not say which is which.

Limits: it copies what the job and assemble.py wrote; it does not draw the subject overlay or the
motion strip (items 3-4 of the pack) and builds no audio plot. The metrics CSV columns are the ones
hand-reveal-2's driver wrote (<subject>_on, _area, _height, _x, _y, _occluded, _facing,
cam_speed_mps, cam_turn_dps). Started from hand-reveal-2's tools/pack.py (2026-11-20); the defects
are now chosen on the command line instead of hard-wired to that clip.
"""
import argparse
import csv
import os
import shutil

ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
ap.add_argument("job")
ap.add_argument("asm")
ap.add_argument("pack")
ap.add_argument("--plant", default=None)
ap.add_argument("--shift-sync", default="main")
ap.add_argument("--drop-sync", default=None)
ap.add_argument("--swap-still", default=None)
args = ap.parse_args()
job, asm, pack, plant = args.job, args.asm, args.pack, args.plant
if plant and not (args.drop_sync and args.swap_still):
    raise SystemExit("--plant needs --drop-sync NAME and --swap-still A:B (three defects: shift, drop, swap)")

shots = [dict(zip(["id", "cut_in", "cut_out", "key", "shift", "handle"], l.split())) for l in open(os.path.join(job, "shots.txt")) if l.strip()]


def framing(s):
    path = os.path.join(job, "shots", s["id"] + "-metrics.csv")
    if not os.path.exists(path):
        return f"### {s['id']}: no metrics\n"
    rows = list(csv.DictReader(open(path)))
    if not rows:
        return f"### {s['id']}: empty metrics\n"
    h, n = int(s["handle"]), int(s["cut_out"]) - int(s["cut_in"])
    lines = [f"### {s['id']} (trailer frames {s['cut_in']}-{s['cut_out']}, key {int(s['cut_in']) + int(s['key'])})"]
    cols = [c[:-3] for c in rows[0] if c.endswith("_on")]
    lines.append("| at | camera m/s | turn deg/s | " + " | ".join(f"{c} on / area / h / x,y / occl / facing" for c in cols) + " |")
    lines.append("|" + "---|" * (3 + len(cols)))
    for label, i in (("first", h), ("key", h + int(s["key"])), ("last", h + n - 1)):
        r = rows[min(i, len(rows) - 1)]
        cells = [f"{r[c + '_on']} / {float(r[c + '_area']):.3f} / {float(r[c + '_height']):.2f} / {float(r[c + '_x']):.2f},{float(r[c + '_y']):.2f} / {float(r[c + '_occluded']):.2f} / {float(r[c + '_facing']):.0f}" for c in cols]
        lines.append(f"| {label} | {float(r['cam_speed_mps']):.2f} | {float(r['cam_turn_dps']):.1f} | " + " | ".join(cells) + " |")
    return "\n".join(lines) + "\n"


def build(dest, defects=False):
    os.makedirs(os.path.join(dest, "stills"), exist_ok=True)
    for name in ("contact.png", "filmstrip.png"):
        if os.path.exists(os.path.join(asm, name)):
            shutil.copy(os.path.join(asm, name), os.path.join(dest, name))
    for s in shots:
        for k in ("first", "key", "last"):
            src = os.path.join(job, "shots", f"{s['id']}-{k}.png")
            if os.path.exists(src):
                shutil.copy(src, os.path.join(dest, "stills", f"{s['id']}-{k}.png"))
    metrics = open(os.path.join(asm, "metrics.txt")).read()
    fr = ("# Engine framing numbers (area = share of screen, h = height share, x,y 0..1 with y up, "
          "occl = share of rays blocked, facing = degrees from facing the camera)\n\n") + "".join(framing(s) for s in shots)
    notes_path = os.path.join(job, "notes.md")
    take = ""
    if os.path.exists(notes_path):
        notes = open(notes_path, encoding="utf-8").read()
        if "Take (" in notes:
            take = notes[notes.find("Take ("):]
            take = take[:take.find("\n\n")] if "\n\n" in take else take
    if defects:
        lines = metrics.splitlines()
        for i, l in enumerate(lines):
            parts = l.split()
            if len(parts) >= 2 and parts[0] == args.shift_sync:
                lines[i] = f"  {parts[0]} {parts[1]} mix +5.0 lane +5.0"
            if len(parts) >= 2 and parts[0] == args.drop_sync:
                lines[i] = f"  {parts[0]} {parts[1]} (no onset within 10 frames)"
        metrics = "\n".join(lines) + "\n"
        a, b = args.swap_still.split(":")
        shutil.copy(os.path.join(job, "shots", b + ".png"), os.path.join(dest, "stills", a + ".png"))
    open(os.path.join(dest, "metrics.txt"), "w").write(metrics)
    open(os.path.join(dest, "framing.md"), "w").write(fr)
    open(os.path.join(dest, "take-log.md"), "w", encoding="utf-8").write(take)


build(pack)
if plant:
    build(plant, defects=True)
    open(os.path.join(os.path.dirname(os.path.abspath(plant)), "planted.txt"), "w").write(
        f"Defective pack: {plant}\n1. sync table: {args.shift_sync} moved 5 frames late\n2. sync table: {args.drop_sync} impact removed\n"
        f"3. stills/{args.swap_still.split(':')[0]}.png replaced by {args.swap_still.split(':')[1]}.png\n")
print("pack written", pack, plant or "")
