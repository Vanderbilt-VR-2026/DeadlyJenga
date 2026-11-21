"""Stage 5 batch production: every campus building, in shot-list priority order.

For each building (config/shot_list.json "buildings"; order: hero, normal, background, then
the earliest capture day in config/capture_days.json, then id):
  1. photos: buildings/<id>/sources.json (Stage 3 align_sources.py) lists the photos that see
     each shot-list facade. A photo counts when its alignment's cameras.json gives it a campus
     pose marked reliable. A facade is "photographed" with >= 2 reliable photos and "covered"
     when it also has coverage_pct >= 50 (Stage 3's rule: share of the facade seen by >= 2 photos
     at incidence <= 70 deg).
  2. measured layouts: the newest layout version of each facade (buildings/<id>/facades/F##/)
     whose features come from photos ("detected" or "manual", written by facade_measure.py).
  3. decision:
     - measured layouts -> assemble (LoD3; hero spots in config/hero_spots.json LoD3+) with the kit
       from config/building_kits.json, run check_building.py (photo check on the building's
       alignment sets), accept into derived/buildings/lod3/index.json or put on the review list
       (model.json "accepted": false; the folder is kept). Not re-assembled when the newest folder
       already used the same layouts, kit and level.
     - reliable photos but no measured layout -> review list, "awaiting measurement".
     - no reliable photos -> stays LoD2, "needs shots".
     Existing estimated stand-ins (measured=false, Kirkland and Tibbot from Stage 4) stay in the
     index, marked as such, until measured layouts replace them.
  Every facade that is not covered goes into Docs/Development/VanderbiltCampus/progress/NEEDS-SHOTS.md
  (with its priority and capture day). Nothing is deleted; new lod3 versions are added.

Outputs: reports/stage5/build_all.json, review_list.json, review_list.md (archive, manifest),
NEEDS-SHOTS.md (git), derived/buildings/lod3/index.json.

    python pipeline/build_all.py                 # everything
    python pipeline/build_all.py --only B_544413714 --dry-run
"""
import argparse
import datetime
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "blender"))
from common import CONFIG, REPO, data_root, load_json, save_json  # noqa: E402

PRIORITY = {"hero": 0, "normal": 1, "background": 2}
MIN_PHOTOS = 2
COVERED_PCT = 50.0
ACCURACY = {"3": 0.10, "3+": 0.05}          # STANDARDS section 5: median photo offset per facade
NEEDS_SHOTS = REPO / "Docs" / "Development" / "VanderbiltCampus" / "progress" / "NEEDS-SHOTS.md"


# ---- inputs ------------------------------------------------------------------------------

class Cameras:
    """name -> {"registered", "reliable"} per alignment folder, loaded once."""

    def __init__(self, root):
        self.root = Path(root)
        self.sets = {}

    def get(self, alignment):
        if alignment not in self.sets:
            path = self.root / alignment / "cameras.json"
            table = {}
            if path.exists():
                for e in load_json(path).get("images", []):
                    c = e.get("campus")
                    table[e["name"]] = {"registered": bool(c), "reliable": bool(c) and c.get("reliable") is not False}
            self.sets[alignment] = table
        return self.sets[alignment]

    def reliable(self, photo_id, alignment):
        """photo_id like 'kartaview-2026-11-19/3632465/x.jpg' (set + name)."""
        set_name = alignment.split("/")[2] if alignment.startswith("derived/alignment/") else ""
        name = photo_id[len(set_name) + 1:] if set_name and photo_id.startswith(set_name + "/") else photo_id
        return self.get(alignment).get(name, {}).get("reliable", False)


def stage3_sources(bid, root):
    """Stage 3 sources.json (archive copy, else git mirror); Stage 4 wrapped it in stage3_sources."""
    for p in (Path(root) / "buildings" / bid / "sources.json",
              CONFIG.parent / "buildings" / bid / "sources.json"):
        if p.exists():
            s = load_json(p)
            return s.get("stage3_sources", s)
    return None


def facade_photos(bid, root, cams):
    """{shot facade: {"reliable": n, "photos": n, "coverage_pct": x, "alignments": [...]}}"""
    s = stage3_sources(bid, root)
    out = {}
    for fname, f in ((s or {}).get("facades") or {}).items():
        sets = f.get("sets") or {}
        aligns = [v["alignment"] for v in sets.values() if v.get("alignment")]
        ids = [ph for ph in f.get("photos", []) if any(cams.reliable(ph, a) for a in aligns)]
        out[fname] = {"photos": len(f.get("photos", [])), "reliable": len(ids), "reliable_ids": ids,
                      "coverage_pct": f.get("coverage_pct", 0.0), "alignments": aligns}
    return out


def measured_layouts(bid, root):
    """{F##: newest layout whose features are photo-measured (detected/manual)}."""
    import facade_layout as fl
    base = Path(root) / "buildings" / bid / "facades"
    out = {}
    for d in sorted(base.iterdir()) if base.exists() else []:
        for v in reversed(fl.versions(bid, d.name, root)):
            try:
                lay = fl.load(d / f"layout_v{v}.json")
            except ValueError:
                continue
            srcs = {f["source"] for f in lay["features"]}
            if lay["features"] and srcs <= {"detected", "manual"}:
                out[d.name] = lay
                break
    return out


def pick_kit(bid, osm_tags, v2_model, rules=None):
    """Kit name and the reason, from config/building_kits.json (rule text in that file)."""
    rules = rules or load_json(CONFIG / "building_kits.json")
    if bid in rules["overrides"]:
        o = rules["overrides"][bid]
        return o["kit"], f"override: {o['why']}"
    b = (osm_tags or {}).get("building")
    if b in rules["by_osm_building"]:
        return rules["by_osm_building"][b], f"OSM building={b}"
    for m in (v2_model or {}).get("materials", []):
        if m in rules["by_wall_material"]:
            return rules["by_wall_material"][m], f"Stage 1 wall material {m}"
    return rules["default"], "default"


def hero_ids():
    return {(s.get("match") or {}).get("building_id") for s in load_json(CONFIG / "hero_spots.json")["spots"]} - {None}


def capture_day_of():
    days = {}
    for d in load_json(CONFIG / "capture_days.json")["days"]:
        for st in d.get("stops", []):
            days.setdefault(st["id"], d["day"])
    return days


def newest_lod3(bid, root):
    vers = sorted((Path(root) / "buildings" / bid).glob("lod3_v*"),
                  key=lambda d: int(d.name.split("_v")[1]) if d.name.split("_v")[1].isdigit() else 0)
    for d in reversed(vers):
        if (d / "model.json").exists():
            return d, load_json(d / "model.json")
    return None, None


# ---- one building -------------------------------------------------------------------------

def build_one(bid, info, root, cams, heroes, kit_rules, osm_tags, dry_run=False):
    import check_building as cb
    root = Path(root)
    fph = facade_photos(bid, root, cams)
    photographed = {k: v for k, v in fph.items() if v["reliable"] >= MIN_PHOTOS}
    meas = measured_layouts(bid, root)
    folder, model = newest_lod3(bid, root)
    level = "3+" if bid in heroes else "3"
    row = {"id": bid, "name": info.get("name"), "priority": info["priority"], "level_target": level,
           "facades_photographed": sorted(photographed), "measured_layouts": sorted(meas)}
    if meas:
        v2 = load_json(root / "buildings" / bid / "v2" / "model.json")
        kit, why = pick_kit(bid, osm_tags.get(bid), v2, kit_rules)
        row.update(kit=kit, kit_rule=why)
        used = {k: lay["version"] for k, lay in meas.items()}
        same = (model and model.get("measured") and model.get("style_kit") == kit and model.get("level") == level
                and {k: x["version"] for k, x in model["inputs"]["layouts"].items()} == used)
        if dry_run:
            row["status"] = "would assemble" if not same else "up to date"
            return row
        if not same:
            import assemble_building as ab
            s = ab.assemble(bid, kit, level, root=root, layouts=meas,
                            notes=[f"build_all.py: kit by rule ({why})"])
            folder = Path(s["folder"])
            model = load_json(folder / "model.json")
        aligns = sorted({a.replace("derived/alignment/", "") for v in fph.values() for a in v["alignments"]})
        res = cb.run(folder, root, alignments=aligns)
        save_json(folder / "check.json", res)
        reasons = [f"check {k} failed" for k, v in res["checks"].items() if not v["ok"]]
        pc = res.get("photo_check", {})
        for fk, r in (pc.get("per_facade") or {}).items():
            if fk in meas and r["median_m"] > ACCURACY[level]:
                reasons.append(f"{fk}: photo offset median {r['median_m']} m > {ACCURACY[level]} m")
        if not pc.get("per_facade"):
            reasons.append("photo check could not run (no reliable camera sees the model)")
        model["accepted"] = not reasons
        model["review_reasons"] = reasons
        save_json(folder / "model.json", model)
        import manifest
        for n in ("model.json", "check.json"):
            manifest.add_file(folder / n, "pipeline/build_all.py", "derived", stage=5, root=root)
        mirror = CONFIG.parent / "buildings" / bid / "lod3" / "model.json"
        if mirror.parent.exists() and not __import__("os").environ.get("VC_DATA_ROOT"):
            save_json(mirror, model)
        row.update(status="lod3_measured" if not reasons else "review", reasons=reasons, folder=str(folder))
        return row
    if model is not None and not model.get("measured"):
        row["status"] = "lod3_estimated"
        row["reasons"] = (["estimated stand-in; reliable photos exist on " + ", ".join(sorted(photographed))
                           + " but no measured layout yet (run facade_measure.py)"] if photographed else
                          ["estimated stand-in; no reliable photos yet"])
        return row
    if photographed:
        row["status"] = "review"
        row["reasons"] = [f"reliable aligned photos on {', '.join(sorted(photographed))} but no measured layout yet "
                          "(awaiting facade_measure.py)"]
        return row
    row["status"] = "lod2_needs_shots"
    return row


# ---- reports ------------------------------------------------------------------------------

def needs_shots_rows(shot, fph_all, days):
    rows = []
    by_b = {}
    for e in shot["entries"]:
        if e["kind"] == "facade":
            by_b.setdefault(e["building"], []).append(e)
    for b in shot["buildings"]:
        fph = fph_all.get(b["id"], {})
        missing = []
        for e in by_b.get(b["id"], []):
            f = fph.get(e["facade"], {})
            if f.get("reliable", 0) >= MIN_PHOTOS and f.get("coverage_pct", 0) >= COVERED_PCT:
                continue
            missing.append((e["facade"], e["priority"], days.get(e["id"]), f.get("reliable", 0)))
        if missing:
            rows.append({"id": b["id"], "name": b.get("name"), "facades": missing})
    return rows


def write_needs_shots(rows, building_status, path=NEEDS_SHOTS):
    lines = ["# Needs shots (Stage 5)", "",
             f"Written by `Tools/VanderbiltCampus/pipeline/build_all.py` on {datetime.date.today().isoformat()}. "
             "Each row is a building with facades that are not covered yet: fewer than "
             f"{MIN_PHOTOS} reliable aligned photos, or under {COVERED_PCT:.0f}% coverage (Stage 3 rule). "
             "Facade names and capture days are from `config/shot_list.json` and `config/capture_days.json`; "
             "the number in brackets is how many reliable photos that facade already has (only shown when > 0). "
             "Status: current model level. Sorted by priority, then capture day.", "",
             f"Buildings listed: {len(rows)}; facades listed: {sum(len(r['facades']) for r in rows)}.", "",
             "| # | Building | Name | Status | Priority | Capture day(s) | Facades lacking photos |",
             "|---|----------|------|--------|----------|----------------|------------------------|"]
    def key(r):
        pr = min(PRIORITY.get(f[1], 3) for f in r["facades"])
        dd = min([f[2] for f in r["facades"] if f[2]] or [999])
        return (pr, dd, r["id"])
    for i, r in enumerate(sorted(rows, key=key), 1):
        pr = min((f[1] for f in r["facades"]), key=lambda p: PRIORITY.get(p, 3))
        ds = sorted({f[2] for f in r["facades"] if f[2]})
        fac = ", ".join(f"{f[0]}" + (f" [{f[3]}]" if f[3] else "") for f in r["facades"])
        lines.append(f"| {i} | {r['id']} | {r['name'] or ''} | {building_status.get(r['id'], 'lod2')} | {pr} | "
                     f"{', '.join(str(d) for d in ds) or '-'} | {fac} |")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    return path


def write_review(rows, root):
    out = Path(root) / "reports" / "stage5"
    rev = [r for r in rows if r["status"] == "review" or (r["status"] == "lod3_estimated" and r.get("reasons"))]
    save_json(out / "review_list.json", {"made": datetime.datetime.now().isoformat(timespec="seconds"),
                                         "note": "Buildings kept out of the game's LoD3 set (status review), and "
                                                 "estimated stand-ins with what they wait on.",
                                         "buildings": rev})
    lines = ["# Stage 5 review list", "", "| Building | Name | Status | Priority | Reasons |", "|---|---|---|---|---|"]
    for r in rev:
        lines.append(f"| {r['id']} | {r.get('name') or ''} | {r['status']} | {r['priority']} | {'; '.join(r.get('reasons', []))} |")
    (out / "review_list.md").write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    return out / "review_list.json", out / "review_list.md"


def run(root=None, only=None, dry_run=False):
    import buildings_common as bc
    import assemble_building as ab
    ab._imports()
    root = Path(root or data_root())
    shot = load_json(CONFIG / "shot_list.json")
    days = capture_day_of()
    heroes = hero_ids()
    kit_rules = load_json(CONFIG / "building_kits.json")
    try:
        osm_tags = {b["id"]: b["tags"] for b in bc.load_osm_buildings(root)[0]}
    except (OSError, KeyError, IndexError):
        osm_tags = {}
    cams = Cameras(root)
    prio = {}
    for e in shot["entries"]:
        if e["kind"] == "facade":
            b = e["building"]
            p = PRIORITY.get(e["priority"], 3)
            d = days.get(e["id"], 999)
            prio[b] = min(prio.get(b, (9, 999)), (p, d))
    buildings = [b for b in shot["buildings"] if not only or b["id"] in only]
    buildings.sort(key=lambda b: (prio.get(b["id"], (9, 999)), b["id"]))
    rows, fph_all = [], {}
    names = {0: "hero", 1: "normal", 2: "background"}
    for b in buildings:
        fph_all[b["id"]] = facade_photos(b["id"], root, cams)
        info = {"name": b.get("name"), "priority": names.get(prio.get(b["id"], (9,))[0], "none")}
        try:
            rows.append(build_one(b["id"], info, root, cams, heroes, kit_rules, osm_tags, dry_run))
        except Exception as ex:          # one bad building must not stop the batch
            rows.append({"id": b["id"], "name": b.get("name"), "priority": info["priority"], "status": "review",
                         "reasons": [f"build failed: {type(ex).__name__}: {ex}"]})
    counts = {}
    for r in rows:
        counts[r["status"]] = counts.get(r["status"], 0) + 1
    summary = {"made": datetime.datetime.now().isoformat(timespec="seconds"), "buildings": len(rows), "counts": counts,
               "rules": {"photographed": f">= {MIN_PHOTOS} reliable aligned photos",
                         "covered": f"photographed and coverage_pct >= {COVERED_PCT}",
                         "accuracy_median_m": ACCURACY}, "rows": rows}
    if dry_run:
        return summary
    out = root / "reports" / "stage5"
    save_json(out / "build_all.json", summary)
    j, m = write_review(rows, root)
    status = {r["id"]: r["status"] for r in rows}
    ns = write_needs_shots(needs_shots_rows(shot, fph_all, days) if not only else
                           [r for r in needs_shots_rows(shot, fph_all, days) if r["id"] in only], status)
    ab.write_index(root)
    import manifest
    for p in (out / "build_all.json", j, m):
        manifest.add_file(p, "pipeline/build_all.py", "derived", stage=5, root=root)
    summary["files"] = [str(out / "build_all.json"), str(j), str(m), str(ns)]
    return summary


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", nargs="*", help="limit to these building ids")
    ap.add_argument("--dry-run", action="store_true", help="decide and print; build and write nothing")
    a = ap.parse_args(argv)
    s = run(only=a.only, dry_run=a.dry_run)
    print(json.dumps(s["counts"], indent=1))
    for f in s.get("files", []):
        print("wrote", f)


if __name__ == "__main__":
    main()
