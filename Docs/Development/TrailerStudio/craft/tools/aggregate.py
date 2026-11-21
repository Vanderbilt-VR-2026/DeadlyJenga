"""Aggregate per-trailer measurements into tables, pattern counts and a reference profile.

python -P aggregate.py <root>   (root holds scripts/meta.csv and analysis/<slug>/)
Reads per slug: summary.json, shots.csv, audio.json, annot.csv, struct.json (see annot_convert.py).
Writes <root>/aggregate/: per_trailer.csv, rows.json, tables.md, pooled_alignment.json
Body = from struct.content_start to struct.final_title (the part before the closing title/end card).
Shot-length statistics use "picture shots": body shots that are not black, not annotated as text
cards, with fragments shorter than MICRO seconds (flash frames, glitch frames) merged into the next shot.
"""
import csv, json, os, sys
from collections import Counter
import numpy as np
from scipy.stats import binomtest, spearmanr

MICRO = 0.12
root = sys.argv[1]
A = os.path.join(root, "analysis")
meta = list(csv.DictReader(open(os.path.join(root, "scripts", "meta.csv"), encoding="utf-8")))


def load(slug):
    d = os.path.join(A, slug)
    s = json.load(open(os.path.join(d, "summary.json")))
    shots = list(csv.DictReader(open(os.path.join(d, "shots.csv"))))
    for x in shots:
        for k in ("start", "end", "dur", "motion", "luma"):
            x[k] = float(x[k])
        x["i"] = int(x["i"]); x["black"] = int(x["black"])
    au = json.load(open(os.path.join(d, "audio.json")))
    ann = {}
    p = os.path.join(d, "annot.csv")
    if os.path.exists(p):
        for r in csv.DictReader(open(p, encoding="utf-8")):
            ann[int(r["i"])] = r
    sp = os.path.join(d, "struct.json")
    st = json.load(open(sp)) if os.path.exists(sp) else {}
    return s, shots, au, ann, st


def length_class(d):
    return "short<=32s" if d <= 32 else ("mid33-75s" if d <= 75 else "long>75s")


def picture_shots(shots, ann, t0, t1):
    body = [dict(x) for x in shots if t0 - 0.02 <= x["start"] < t1 - 0.02]
    merged = []
    carry = None
    for x in body:
        if carry is not None:
            x["start"] = carry["start"]; x["dur"] = x["end"] - x["start"]; carry = None
        if x["dur"] < MICRO:
            carry = x
            continue
        merged.append(x)
    return [x for x in merged if not x["black"] and ann.get(x["i"], {}).get("kind") != "text"]


def nearest(refs, t):
    refs = np.asarray(refs)
    return np.min(np.abs(refs - t)) if len(refs) else np.inf


def per_trailer(m):
    slug = m["slug"]
    s, shots, au, ann, st = load(slug)
    fps, dur = s["fps"], s["duration"]
    t0 = st.get("content_start", s["active_start"])
    t1 = st.get("final_title", st.get("endcard_start", s["active_end"]))
    pic = picture_shots(shots, ann, t0, t1)
    d = np.array([x["dur"] for x in pic]) if pic else np.array([np.nan])
    span = t1 - t0
    q, mot = [], []
    for k in range(4):
        lo, hi = t0 + k * span / 4, t0 + (k + 1) * span / 4
        sel = [x for x in pic if lo <= (x["start"] + x["end"]) / 2 < hi]
        q.append(float(np.median([x["dur"] for x in sel])) if sel else np.nan)
        mot.append(float(np.mean([x["motion"] for x in sel])) if sel else np.nan)
    rho = spearmanr([(x["start"] + x["end"]) / 2 for x in pic], [x["dur"] for x in pic])[0] if len(pic) >= 6 else np.nan
    # cuts inside the body, including the cut into the title (picture-shot starts + the title cut)
    cut_t = sorted(set([round(x["start"], 3) for x in pic if x["start"] > t0 + 0.05] +
                       [round(x["start"], 3) for x in shots if abs(x["start"] - t1) < 0.05]))
    cut_t = np.array(cut_t)
    win = [int(((cut_t >= w) & (cut_t < w + 5)).sum()) for w in np.arange(t0, max(t1 - 2.5, t0 + 0.1), 5)] if len(cut_t) else [0]

    def align(refs, k):
        if len(cut_t) == 0 or len(refs) == 0:
            return np.nan, np.nan, 0, 0
        dc = np.array([nearest(refs, t) for t in cut_t])
        grid = np.arange(t0, t1, 1 / fps / 4)
        dg = np.array([nearest(refs, t) for t in grid])
        tol = k / fps + 1e-6
        return float((dc <= tol).mean()), float((dg <= tol).mean()), int((dc <= tol).sum()), len(dc)

    row = dict(slug=slug, title=m["title"], type=m["type"], vr=int(m["vr"]), genre=m["genre"],
               duration=dur, cls=length_class(dur), fps=fps, body_len=span, n_pic=len(pic),
               asl=float(np.mean(d)), msl=float(np.median(d)), sl_min=float(np.min(d)), sl_max=float(np.max(d)),
               sl_p10=float(np.percentile(d, 10)), sl_p90=float(np.percentile(d, 90)),
               msl_q1=q[0], msl_q2=q[1], msl_q3=q[2], msl_q4=q[3],
               accel=q[3] / q[0] if q[0] and not np.isnan(q[0]) and not np.isnan(q[3]) else np.nan,
               rho_len_time=rho, cuts_per_min=len(cut_t) / max(span, 1e-6) * 60,
               max_cuts_5s=max(win), med_cuts_5s=float(np.median(win)),
               motion_q1=mot[0], motion_q2=mot[1], motion_q3=mot[2], motion_q4=mot[3],
               motion_mean=float(np.mean([x["motion"] for x in pic])) if pic else np.nan,
               tempo=s["tempo"], pulse=s["pulse_clarity"],
               lufs=s["integrated_lufs"], lra=s["lra"], peak=s["true_peak"], st_max=s["short_term_lufs_max"],
               n_fades=s["n_fades"], n_added=s.get("n_added", 0), n_removed=s.get("n_removed", 0), n_auto=s.get("n_auto_cuts", 0))
    for name, refs in (("beat", au["beats"]), ("down", au["downbeats"]), ("strong", au["strong_onsets"]), ("onset", au["onsets"])):
        for k in (1, 2, 3):
            f, c, hits, n = align(refs, k)
            row[f"{name}{k}f"], row[f"{name}{k}f_chance"] = f, c
            row[f"{name}{k}f_hits"], row[f"n_cuts"] = hits, n
        row[f"{name}2f_p"] = binomtest(row[f"{name}2f_hits"], row["n_cuts"], row[f"{name}2f_chance"], alternative="greater").pvalue if row["n_cuts"] else np.nan
        row[f"{name}2f_lift"] = row[f"{name}2f"] / row[f"{name}2f_chance"] if row["n_cuts"] and row[f"{name}2f_chance"] else np.nan
    # biggest audio hit inside the body (onset strength x loudness jump) and what is on screen there
    hits_in = [h for h in au["hits"] if t0 + 1.0 <= h["t"] <= t1 + 0.5]
    hit = max(hits_in, key=lambda h: h["score"]) if hits_in else None
    row["hit_t"] = hit["t"] if hit else np.nan
    row["hit_body_frac"] = (hit["t"] - t0) / span if hit else np.nan
    hs = [x for x in shots if x["start"] <= row["hit_t"] < x["end"]] if hit else []
    row["hit_shot"] = hs[0]["i"] if hs else -1
    a = ann.get(row["hit_shot"], {})
    row["hit_picture"] = f'{a.get("job","?")}/{a.get("kind","?")}: {a.get("note","(not annotated)")}'
    row["hit_cut_dist_f"] = min(abs(row["hit_t"] - x["start"]) for x in shots) * fps if hit else np.nan
    row["hit_rel_title_s"] = row["hit_t"] - t1 if hit else np.nan
    # loudness curve (EBU R128 momentary, 400 ms) inside the body
    e = au["ebur128"]
    tm, M, S = np.array(e["t"]), np.array(e["M"]), np.array(e["S"])
    inb = (tm >= t0) & (tm < t1) & (M > -70)
    row["loud_q"] = [float(np.mean(S[(tm >= t0 + k * span / 4) & (tm < t0 + (k + 1) * span / 4) & (S > -70)])) if np.any((tm >= t0 + k * span / 4) & (tm < t0 + (k + 1) * span / 4) & (S > -70)) else np.nan for k in range(4)]
    for k in range(4):
        row[f"loud_q{k+1}"] = row["loud_q"][k]
    row["loud_ramp"] = row["loud_q4"] - row["loud_q1"]
    if np.any(inb):
        row["loudest_body_frac"] = float((tm[inb][np.argmax(M[inb])] - t0) / span)
        medM = float(np.median(M[inb]))
        dips, k = [], 0
        idx = np.where((tm >= t0 + 1) & (tm < t1 + 0.5))[0]
        quiet = M[idx] < medM - 12
        while k < len(quiet):
            if quiet[k]:
                j = k
                while j < len(quiet) and quiet[j]:
                    j += 1
                if (j - k) * 0.1 >= 0.3:
                    dips.append((float(tm[idx[k]]), (j - k) * 0.1))
                k = j
            else:
                k += 1
        row["n_stopdowns"] = len(dips)
        row["stopdown_times"] = dips
        row["stopdown_before_title"] = int(any(t1 - 4 <= a0 + l <= t1 + 0.6 for a0, l in dips))
        endM = M[(tm >= t1) & (tm < dur) & (M > -70)]
        row["title_vs_body_LU"] = float(np.median(endM) - medM) if len(endM) else np.nan
    else:
        row["loudest_body_frac"] = np.nan; row["n_stopdowns"] = 0; row["stopdown_before_title"] = 0; row["title_vs_body_LU"] = np.nan
    # structure (hand-confirmed)
    for k in ("content_start", "first_logo", "first_gameplay", "first_what", "first_title", "final_title", "endcard_start", "endcard_end", "gameplay_share"):
        v = st.get(k, np.nan)
        row[k] = v if isinstance(v, (int, float)) else np.nan
    row["first_title_frac"] = row["first_title"] / dur
    row["final_title_frac"] = row["final_title"] / dur
    row["first_gameplay_frac"] = row["first_gameplay"] / dur
    row["endcard_len"] = row["endcard_end"] - row["endcard_start"] if not np.isnan(row["endcard_start"]) else np.nan
    row["tail_len"] = dur - row["final_title"]
    row["button"] = st.get("button", "")
    # shot grammar from annotations, picture shots only (text cards excluded), within annotated part
    an = [ann[i] for i in sorted(ann)]
    pics = [x for x in an if x["kind"] != "text"]
    n = max(len(pics), 1)
    sizes = Counter(x["size"] for x in pics)
    for k in ("EW", "W", "M", "C"):
        row[f"size_{k}"] = sizes.get(k, 0) / n if pics else np.nan
    moves = Counter(x["move"] for x in pics)
    row["static_share"] = moves.get("static", 0) / n if pics else np.nan
    row["pov_share"] = moves.get("pov", 0) / n if pics else np.nan
    row["designed_move_share"] = sum(moves.get(k, 0) for k in ("push", "pull", "pan", "follow", "orbit", "crane")) / n if pics else np.nan
    seq = [x["size"] for x in pics]
    row["adj_same_size"] = (sum(1 for a1, b1 in zip(seq, seq[1:]) if a1 == b1) / max(len(seq) - 1, 1)) if len(seq) > 1 else np.nan
    kinds = Counter(x["kind"] for x in an)
    row["n_annot"] = len(an)
    for k in ("gameplay", "cinematic", "text", "liveaction", "ui"):
        row[f"kind_{k}"] = kinds.get(k, 0) / max(len(an), 1) if an else np.nan
    row["has_liveaction"] = int(kinds.get("liveaction", 0) > 0)
    row["has_caption_cards"] = int(any(x["kind"] == "text" and x["job"] in ("teach", "joke") for x in an) or
                                   any("caption" in x["note"] or "text:" in x["note"] for x in an if x["kind"] != "text"))
    jobs = Counter(x["job"] for x in an)
    row["jobs"] = dict(jobs)
    first_job = [x["job"] for x in an if x["kind"] != "text" or x["job"] in ("hook", "joke", "teach")]
    row["opens_with"] = an[0]["kind"] if an else ""
    sp = os.path.join(A, slug, "sync.json")
    sy = json.load(open(sp)) if os.path.exists(sp) else {}
    for k in ("phase_R1", "phase_p1", "phase_R2", "phase_p2", "onset_pct_mean", "onset_top10_share", "onset_top25_share"):
        row[k] = float(sy[k]) if k in sy else np.nan
    return row


rows = [per_trailer(m) for m in meta]
out_dir = os.path.join(root, "aggregate")
os.makedirs(out_dir, exist_ok=True)
keys = [k for k in rows[0] if k not in ("jobs", "loud_q", "stopdown_times")]
with open(os.path.join(out_dir, "per_trailer.csv"), "w", newline="", encoding="utf-8") as f:
    w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
    w.writeheader()
    for r in rows:
        w.writerow({k: (round(v, 3) if isinstance(v, float) else v) for k, v in r.items() if k in keys})


def vals(rs, k):
    return [r[k] for r in rs if isinstance(r.get(k), (int, float)) and not np.isnan(r[k])]


def med(rs, k):
    v = vals(rs, k)
    return (float(np.median(v)), float(np.percentile(v, 25)), float(np.percentile(v, 75)), len(v)) if v else (np.nan,) * 3 + (0,)


def fmt(t):
    m, lo, hi, n = t
    return "-" if n == 0 else f"{m:.2f} [{lo:.2f}-{hi:.2f}] n={n}"


rows_ref = [r for r in rows if r['slug'] != 'attempt-1']
groups = {"all": rows_ref}
for c in ("short<=32s", "mid33-75s", "long>75s"):
    groups[c] = [r for r in rows_ref if r["cls"] == c]
for t in ("reveal", "launch", "gameplay"):
    groups["type:" + t] = [r for r in rows_ref if r["type"] == t]
groups["VR"] = [r for r in rows_ref if r["vr"]]
groups["flat"] = [r for r in rows_ref if not r["vr"]]
groups["party/physics"] = [r for r in rows_ref if "party" in r["genre"] or "physics" in r["genre"]]

stats = ["duration", "body_len", "n_pic", "msl", "asl", "sl_p10", "sl_p90", "sl_max", "msl_q1", "msl_q2", "msl_q3", "msl_q4", "accel", "rho_len_time",
         "cuts_per_min", "max_cuts_5s", "first_gameplay", "first_what", "first_title_frac", "final_title_frac", "endcard_len", "tail_len",
         "lufs", "peak", "lra", "st_max", "tempo", "pulse", "beat1f", "beat1f_chance", "beat2f", "beat2f_chance", "beat3f", "beat3f_chance",
         "strong2f", "strong2f_chance", "onset2f", "onset2f_chance", "phase_R1", "onset_pct_mean", "onset_top10_share", "onset_top25_share", "hit_body_frac", "hit_rel_title_s", "loudest_body_frac", "loud_q1", "loud_q4", "loud_ramp", "title_vs_body_LU", "n_stopdowns", "stopdown_before_title",
         "size_EW", "size_W", "size_M", "size_C", "static_share", "pov_share", "designed_move_share", "adj_same_size",
         "kind_gameplay", "kind_cinematic", "kind_text", "kind_liveaction", "motion_q1", "motion_q4"]
out = ["| stat | " + " | ".join(f"{g} (n={len(v)})" for g, v in groups.items()) + " |", "|---|" + "---|" * len(groups)]
for k in stats:
    out.append(f"| {k} | " + " | ".join(fmt(med(g, k)) for g in groups.values()) + " |")
open(os.path.join(out_dir, "tables.md"), "w", encoding="utf-8").write("\n".join(out))

ref = [r for r in rows if r["slug"] != "attempt-1"]
pool = {}
for name in ("beat", "down", "strong", "onset"):
    for k in (1, 2, 3):
        rs = [r for r in ref if r["n_cuts"] and not np.isnan(r[f"{name}{k}f"])]
        hits = sum(r[f"{name}{k}f_hits"] for r in rs)
        n = sum(r["n_cuts"] for r in rs)
        ch = sum(r[f"{name}{k}f_chance"] * r["n_cuts"] for r in rs) / max(n, 1)
        pool[f"{name}{k}f"] = dict(observed=round(hits / max(n, 1), 3), chance=round(ch, 3), n_cuts=int(n), hits=int(hits),
                                   p=float(binomtest(int(hits), int(n), ch, alternative="greater").pvalue) if n else None)
json.dump(pool, open(os.path.join(out_dir, "pooled_alignment.json"), "w"), indent=1)
json.dump(rows, open(os.path.join(out_dir, "rows.json"), "w"), indent=1, default=float)
print("\n".join(out))
print(json.dumps(pool, indent=1))
