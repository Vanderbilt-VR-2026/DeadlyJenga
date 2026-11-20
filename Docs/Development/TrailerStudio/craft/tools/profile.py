"""Build reference_profile.json from aggregate/rows.json, and check a trailer against it.

python -P profile.py build <root>                 -> <root>/aggregate/reference_profile.json
python -P profile.py check <root> <slug> [class]  -> table of metric, value, reference range, verdict
  class: short | mid | long | all (default: by duration; short falls back to mid for montage metrics)
Verdicts: PASS = inside the middle half (p25-p75) of the reference trailers, OK = inside p10-p90,
OUT = outside p10-p90. Rules with a hard numeric threshold are listed under "rules" and checked too.
"""
import json, os, sys
import numpy as np

METRICS = {
    # key: (description, used for montage-only fallback)
    "msl": ("median picture-shot length, s", True),
    "asl": ("mean picture-shot length, s", True),
    "sl_p10": ("10th-percentile shot length, s", True),
    "sl_max": ("longest picture shot, s", True),
    "cuts_per_min": ("cuts per minute of body", True),
    "max_cuts_5s": ("most cuts in any 5 s window", True),
    "accel": ("median shot length last quarter / first quarter", True),
    "first_gameplay": ("first gameplay frame, s", False),
    "first_what": ("first 'what is this game' moment, s", False),
    "final_title_frac": ("closing title start / runtime", False),
    "endcard_len": ("closing title + end card length, s", False),
    "lufs": ("integrated loudness, LUFS", False),
    "peak": ("true peak, dBTP", False),
    "lra": ("loudness range, LU", False),
    "loud_ramp": ("short-term loudness last quarter minus first quarter, LU", False),
    "title_vs_body_LU": ("closing title loudness relative to body, LU", False),
    "onset_pct_mean": ("mean onset-strength percentile at cuts (50 = random)", True),
    "onset_top25_share": ("share of cuts on top-25% audio onsets (0.25 = random)", True),
    "size_W": ("share of wide shots", True),
    "size_C": ("share of close shots", True),
    "static_share": ("share of static-camera shots", True),
    "adj_same_size": ("share of cuts joining two shots of the same size", True),
    "gameplay_share": ("share of runtime that is gameplay (by eye)", False),
}
LOWER_OK = {"first_gameplay", "first_what"}
INFO = {"gameplay_share", "adj_same_size", "lra", "static_share"}
CLASSES = {"short": lambda r: r["duration"] <= 32, "mid": lambda r: 32 < r["duration"] <= 75,
           "long": lambda r: r["duration"] > 75, "all": lambda r: True}


def ok(v):
    return isinstance(v, (int, float)) and not np.isnan(v)


def build(root):
    rows = [r for r in json.load(open(os.path.join(root, "aggregate", "rows.json"))) if r["slug"] != "attempt-1"]
    prof = {"source": "TrailerCraft-Reference measured sample (22 official trailers)", "classes": {}}
    for c, f in CLASSES.items():
        rs = [r for r in rows if f(r)]
        prof["classes"][c] = {"n": len(rs), "trailers": [r["slug"] for r in rs], "metrics": {}}
        for k, (desc, _) in METRICS.items():
            v = [r[k] for r in rs if ok(r.get(k))]
            if not v:
                continue
            prof["classes"][c]["metrics"][k] = dict(desc=desc, n=len(v), median=round(float(np.median(v)), 3),
                                                    p10=round(float(np.percentile(v, 10)), 3), p25=round(float(np.percentile(v, 25)), 3),
                                                    p75=round(float(np.percentile(v, 75)), 3), p90=round(float(np.percentile(v, 90)), 3),
                                                    min=round(float(min(v)), 3), max=round(float(max(v)), 3))
    prof["rules"] = [
        {"id": "first_gameplay", "metric": "first_gameplay", "op": "<=", "value": 2.0, "classes": ["short", "mid"], "support": "13 of 21 trailers with gameplay; 17 of 21 within 8 s"},
        {"id": "first_what", "metric": "first_what", "op": "<=", "value": 12.0, "classes": ["mid", "long"], "support": "15 of 21"},
        {"id": "final_title_late", "metric": "final_title_frac", "op": "between", "value": [0.80, 0.95], "classes": ["mid", "long"], "support": "18 of 22"},
        {"id": "endcard", "metric": "endcard_len", "op": "between", "value": [4.0, 8.0], "classes": ["short", "mid"], "support": "15 of 22 (long trailers hold 8-13 s)"},
        {"id": "msl", "metric": "msl", "op": "between", "value": [1.0, 2.2], "classes": ["mid", "long", "short-montage"], "support": "16 of 18 mid/long trailers"},
        {"id": "loud_ramp", "metric": "loud_ramp", "op": ">=", "value": 0.0, "classes": ["all"], "support": "16 of 20"},
        {"id": "title_quieter", "metric": "title_vs_body_LU", "op": "<=", "value": -3.0, "classes": ["all"], "support": "16 of 21 at -6 LU or lower"},
        {"id": "lufs", "metric": "lufs", "op": "between", "value": [-20.0, -12.0], "classes": ["all"], "support": "13 of 22 (median -16)"},
        {"id": "peak", "metric": "peak", "op": "<=", "value": -1.0, "classes": ["all"], "support": "delivery spec, not a sample norm: 11 of 22 references exceed -1 dBTP"},
        {"id": "close_ups_present", "metric": "size_C", "op": ">=", "value": 0.05, "classes": ["mid", "long"], "support": "median 5%; 15 of 21 at 10% or less; most trailers have at least one"},
    ]
    json.dump(prof, open(os.path.join(root, "aggregate", "reference_profile.json"), "w"), indent=1)
    print("written", os.path.join(root, "aggregate", "reference_profile.json"))


def check(root, slug, cls=None):
    prof = json.load(open(os.path.join(root, "aggregate", "reference_profile.json")))
    row = [r for r in json.load(open(os.path.join(root, "aggregate", "rows.json"))) if r["slug"] == slug][0]
    if cls is None:
        cls = "short" if row["duration"] <= 32 else ("mid" if row["duration"] <= 75 else "long")
    print(f"{slug}: {row['duration']:.1f} s, class {cls} (n={prof['classes'][cls]['n']})")
    print(f"{'metric':20s} {'value':>8s}  {'p10':>7s} {'p25':>7s} {'med':>7s} {'p75':>7s} {'p90':>7s}  ref  verdict")
    for k, (desc, montage) in METRICS.items():
        c = cls
        if cls == "short" and montage:
            c = "mid"          # short references are mostly single takes; use mid-length montages
        m = prof["classes"][c]["metrics"].get(k)
        v = row.get(k)
        if not m or not ok(v):
            continue
        verdict = "PASS" if m["p25"] <= v <= m["p75"] else ("OK" if m["p10"] <= v <= m["p90"] else "OUT")
        if k in LOWER_OK and v <= m["p75"]:
            verdict = "PASS"
        if k in INFO:
            verdict = "info:" + verdict
        print(f"{k:20s} {v:8.2f}  {m['p10']:7.2f} {m['p25']:7.2f} {m['median']:7.2f} {m['p75']:7.2f} {m['p90']:7.2f}  {c:5s} {verdict}")
    print("rules:")
    for r in prof["rules"]:
        v = row.get(r["metric"])
        if not ok(v):
            continue
        if r["op"] == "<=":
            good = v <= r["value"]
        elif r["op"] == ">=":
            good = v >= r["value"]
        else:
            good = r["value"][0] <= v <= r["value"][1]
        print(f"  {r['id']:18s} {v:8.2f} {r['op']} {r['value']}  -> {'pass' if good else 'FAIL'}   ({r['support']})")


if __name__ == "__main__":
    if sys.argv[1] == "build":
        build(sys.argv[2])
    else:
        check(sys.argv[2], sys.argv[3], sys.argv[4] if len(sys.argv) > 4 else None)
