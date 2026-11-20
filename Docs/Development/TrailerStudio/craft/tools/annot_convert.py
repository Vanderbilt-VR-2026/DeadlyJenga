"""Turn hand annotations (annot_src/<slug>.txt) into analysis/<slug>/annot.csv + struct.json.

Text format:
  # key=value; key=value ...        structure (times in seconds, hand-confirmed from sheets)
  <i or a-b> <size> <move> <kind> <job> <note>
  size: EW W M C T(text/graphic)   move: st pu pl pa fo or cr ha pov un
  kind: g gameplay  c cinematic/CG  t text/graphic  l live action  u UI/menu screen
  job: hook teach esc wow joke var build title cta brand
"""
import csv, json, os, sys

root = sys.argv[1]
src = os.path.join(root, "annot_src")
MOVES = {"st": "static", "pu": "push", "pl": "pull", "pa": "pan", "fo": "follow", "or": "orbit", "cr": "crane",
         "ha": "handheld", "pov": "pov", "un": "unsure"}
KINDS = {"g": "gameplay", "c": "cinematic", "t": "text", "l": "liveaction", "u": "ui"}
for fn in sorted(os.listdir(src)):
    slug = fn[:-4]
    d = os.path.join(root, "analysis", slug)
    n_shots = sum(1 for _ in open(os.path.join(d, "shots.csv"))) - 1
    st, rows = {}, []
    for line in open(os.path.join(src, fn), encoding="utf-8"):
        line = line.strip()
        if not line:
            continue
        if line.startswith("#"):
            for kv in line[1:].split(";"):
                if "=" in kv:
                    k, v = kv.split("=", 1)
                    k, v = k.strip(), v.strip()
                    if v == "":
                        continue
                    try:
                        st[k] = float(v)
                    except ValueError:
                        st[k] = v
            continue
        idx, size, move, kind, job, *note = line.split(" ")
        a, b = (idx.split("-") + [idx])[:2] if "-" in idx else (idx, idx)
        for i in range(int(a), int(b) + 1):
            rows.append(dict(i=i, size=size, move=MOVES[move], kind=KINDS[kind], job=job, note=" ".join(note)))
    bad = [r["i"] for r in rows if r["i"] >= n_shots]
    if bad:
        print("WARN", slug, "indices beyond shot count", n_shots, bad[:5])
    with open(os.path.join(d, "annot.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["i", "size", "move", "kind", "job", "note"])
        w.writeheader()
        w.writerows(rows)
    json.dump(st, open(os.path.join(d, "struct.json"), "w"), indent=1)
    print(slug, "shots", n_shots, "annotated", len(rows))
