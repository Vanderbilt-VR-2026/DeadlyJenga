"""Evidence pack for a hand-reveal-2 render (skill review-rubric section 2), plus an optional planted-defect copy.

Usage: python pack.py <job_output_folder> <assembled_folder> <pack_folder> [--plant <defect_pack_folder>]
Writes framing.md (engine numbers per shot at first/key/last), copies stills, contact sheet, filmstrip,
metrics and the take log. With --plant, writes a second pack with three injected defects (recorded in
planted.txt beside the packs, never inside them).
"""
import csv, os, shutil, sys, random

job, asm, pack = sys.argv[1], sys.argv[2], sys.argv[3]
plant = sys.argv[sys.argv.index("--plant") + 1] if "--plant" in sys.argv else None
shots = [dict(zip(["id", "cut_in", "cut_out", "key", "shift", "handle"], l.split())) for l in open(os.path.join(job, "shots.txt")) if l.strip()]

def framing(s):
    path = os.path.join(job, "shots", s["id"] + "-metrics.csv")
    if not os.path.exists(path): return f"{s['id']}: no metrics\n"
    rows = list(csv.DictReader(open(path)))
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

def build(dest, defects=None):
    os.makedirs(os.path.join(dest, "stills"), exist_ok=True)
    for name in ("contact.png", "filmstrip.png"):
        shutil.copy(os.path.join(asm, name), os.path.join(dest, name))
    for s in shots:
        for k in ("first", "key", "last"):
            src = os.path.join(job, "shots", f"{s['id']}-{k}.png")
            if os.path.exists(src): shutil.copy(src, os.path.join(dest, "stills", f"{s['id']}-{k}.png"))
    metrics = open(os.path.join(asm, "metrics.txt")).read()
    fr = "# Engine framing numbers (StudioFraming: area = share of screen, h = height share, x,y 0..1 with y up, occl = share of rays blocked, facing = degrees from facing the camera)\n\n" + "".join(framing(s) for s in shots)
    notes = open(os.path.join(job, "notes.md"), encoding="utf-8").read()
    take = notes[notes.find("Take ("):]
    take = take[:take.find("\n\n")] if "\n\n" in take else take
    if defects:
        lines = metrics.splitlines()
        for i, l in enumerate(lines):
            if l.strip().startswith("main "):
                lines[i] = "  main 150 +5.0"
            if l.strip().startswith("grip "):
                lines[i] = "  grip 116 (no onset within 10 frames)"
        metrics = "\n".join(lines) + "\n"
        # the S02 face still replaced by S01's last frame (no readable face)
        shutil.copy(os.path.join(job, "shots", "S01-last.png"), os.path.join(dest, "stills", "S02-key.png"))
    open(os.path.join(dest, "metrics.txt"), "w").write(metrics)
    open(os.path.join(dest, "framing.md"), "w").write(fr)
    open(os.path.join(dest, "take-log.md"), "w", encoding="utf-8").write(take)

build(pack)
if plant:
    build(plant, defects=True)
    open(os.path.join(os.path.dirname(plant), "planted.txt"), "w").write(
        f"Defective pack: {plant}\n1. sync table: main hit moved to +5 frames\n2. sync table: the grip impact sound removed\n3. stills/S02-key.png replaced by S01-last.png (no face)\n")
print("pack written", pack, plant or "")
