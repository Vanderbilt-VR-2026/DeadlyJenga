"""Light "game" meshes for every building in the buildings v2 index (Quest budget).

    python pipeline/buildings_game.py --jobs 6            # every building, skips finished ones
    python pipeline/buildings_game.py --only B_544397715   # one building
    python pipeline/buildings_game.py --force              # redo all
    python pipeline/buildings_game.py --level loose        # the looser second level (0.10 m / 0.25 m)

For each building in derived/buildings/v2/index.json writes, next to its v2 files:
  game_lod0.npz   from lod2.npz:     visual, within --tol-visual (0.05 m) of lod2
  game_col.npz    from col_mesh.npz: collider, within --tol-col (0.15 m) of lod2
(the loose level writes game_lod0_loose.npz / game_col_loose.npz; export_all.py uses
the normal ones). The work runs in headless Blender (blender/game_mesh.py, bmesh), in
--jobs parallel processes. Method, measured error and triangle counts per building go
to reports/stage1/buildings_game[_loose].json; per-building results are kept in
derived/buildings/game/v1/<id>[_loose].json so a re-run only redoes changed buildings.

Targets (reported, not forced: error wins over triangle count):
  visual about 900k triangles campus-wide, no building over about 12k;
  colliders about 350k, no building over about 6k.
"""
import argparse
import json
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from common import data_root, fail, load_json, save_json  # noqa: E402

TOOLS = HERE.parent
WORKER = TOOLS / "blender" / "game_mesh.py"
LEVELS = {"normal": ("", 0.05, 0.15), "loose": ("_loose", 0.10, 0.25)}
TARGETS = {"visual_total": 900_000, "visual_building": 12_000, "col_total": 350_000, "col_building": 6_000}


def jobs_for(root, only, force, level, tol_visual, tol_col):
    from export_all import _v2_index
    recs = _v2_index(root)
    if recs is None:
        fail(f"missing {root / 'derived/buildings/v2/index.json'} (buildings v2)")
    suffix = LEVELS[level][0]
    stats_dir = root / "derived" / "buildings" / "game" / "v1"
    code = max(WORKER.stat().st_mtime, Path(__file__).stat().st_mtime)
    todo, all_jobs = [], []
    for r in recs:
        folder = Path(r["folder"])
        lod2, col = folder / "lod2.npz", folder / "col_mesh.npz"
        if not lod2.exists():
            print(f"warning: {r['id']} has no {lod2}")
            continue
        job = {"id": r["id"], "lod2": str(lod2), "col_mesh": str(col) if col.exists() else None,
               "out_lod0": str(folder / f"game_lod0{suffix}.npz"), "out_col": str(folder / f"game_col{suffix}.npz"),
               "stats": str(stats_dir / f"{r['id']}{suffix}.json"), "tol_visual": tol_visual, "tol_col": tol_col,
               "suffix": suffix}
        all_jobs.append(job)
        outs = [Path(job["out_lod0"]), Path(job["stats"])] + ([Path(job["out_col"])] if job["col_mesh"] else [])
        newest_in = max([lod2.stat().st_mtime, code] + ([col.stat().st_mtime] if col.exists() else []))
        fresh = all(o.exists() and o.stat().st_mtime >= newest_in for o in outs)
        if fresh:
            try:
                s = load_json(job["stats"])
                fresh = not s.get("crashed") and s.get("tol_visual", tol_visual) == tol_visual
            except Exception:
                fresh = False
        if only:                        # --only: rebuild just these; the report still covers all
            if r["id"] in only:
                todo.append(job)
        elif force or not fresh:
            todo.append(job)
    return todo, all_jobs


def run_workers(todo, jobs, log_dir):
    import subprocess
    from check_fbx import blender_exe
    # Heaviest first, dealt round-robin, so the slices finish at about the same time.
    import numpy as np
    def size(j):
        try:
            with np.load(j["lod2"]) as z:
                return len(z["faces"])
        except Exception:
            return 0
    todo = sorted(todo, key=size, reverse=True)
    slices = [todo[i::jobs] for i in range(min(jobs, len(todo)))]
    procs = []
    log_dir.mkdir(parents=True, exist_ok=True)
    for k, part in enumerate(slices):
        jf = log_dir / f"game_jobs_{k}.json"
        save_json(jf, {"jobs": part})
        log = open(log_dir / f"game_{k}.log", "w", encoding="utf-8")
        cmd = [blender_exe(), "-b", "--factory-startup", "--python-exit-code", "1",
               "--python", str(WORKER), "--", "--jobs-file", str(jf)]
        procs.append((subprocess.Popen(cmd, stdout=log, stderr=subprocess.STDOUT), log))
    ok = True
    for proc, log in procs:
        proc.wait()
        log.close()
        ok &= proc.returncode == 0
    return ok


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--jobs", type=int, default=6, help="parallel Blender processes (default 6)")
    p.add_argument("--only", nargs="*", help="building ids")
    p.add_argument("--force", action="store_true")
    p.add_argument("--level", choices=sorted(LEVELS), default="normal")
    p.add_argument("--tol-visual", type=float, help="override the visual tolerance (m)")
    p.add_argument("--tol-col", type=float, help="override the collider tolerance (m)")
    p.add_argument("--part", choices=["all", "col"], default="all",
                   help="col: redo only the colliders (keeps existing game_lod0 results)")
    args = p.parse_args(argv)
    t0 = time.time()
    root = data_root()
    suffix, tv, tc = LEVELS[args.level]
    tv, tc = args.tol_visual or tv, args.tol_col or tc
    todo, all_jobs = jobs_for(root, set(args.only or []), args.force, args.level, tv, tc)
    for j in todo:
        j["part"] = args.part
    print(f"{len(all_jobs)} buildings, {len(todo)} to (re)build, level {args.level} "
          f"(visual {tv} m, collider {tc} m)")
    ok = run_workers(todo, args.jobs, root / "derived" / "buildings" / "game" / "logs") if todo else True

    rows, problems = [], []
    tot = {"visual_before": 0, "visual_after": 0, "col_before": 0, "col_after": 0}
    for j in all_jobs:
        try:
            r = load_json(j["stats"])
        except Exception:
            problems.append(f"{j['id']}: no result")
            continue
        rows.append(r)
        if r.get("crashed"):
            problems.append(f"{j['id']}: " + "; ".join(r["problems"]))
            continue
        tot["visual_before"] += r["visual"]["before"]
        tot["visual_after"] += r["visual"]["after"]
        if "collider" in r:
            tot["col_before"] += r["collider"]["before"]
            tot["col_after"] += r["collider"]["after"]
    good = [r for r in rows if not r.get("crashed")]
    over_v = sorted(((r["visual"]["after"], r["id"]) for r in good if r["visual"]["after"] > TARGETS["visual_building"]),
                    reverse=True)
    over_c = sorted(((r["collider"]["after"], r["id"]) for r in good
                     if "collider" in r and r["collider"]["after"] > TARGETS["col_building"]), reverse=True)
    dev_v = [r["visual"]["dev_max_m"] for r in good]
    report = {"made": time.strftime("%Y-%m-%dT%H:%M:%S"), "level": args.level,
              "tolerance_visual_m": tv, "tolerance_collider_m": tc, "targets": TARGETS, "totals": tot,
              "visual_total_misses_target": tot["visual_after"] > TARGETS["visual_total"],
              "collider_total_misses_target": tot["col_after"] > TARGETS["col_total"],
              "buildings_over_visual_target": [{"id": i, "triangles": n} for n, i in over_v],
              "buildings_over_collider_target": [{"id": i, "triangles": n} for n, i in over_c],
              "visual_dev_max_m": max(dev_v) if dev_v else None,
              "buildings_with_problems": [{"id": r["id"], "problems": r["problems"]} for r in rows if r.get("problems")],
              "failures": problems, "seconds": round(time.time() - t0, 1), "buildings": rows}
    rpath = root / "reports" / "stage1" / f"buildings_game{suffix}.json"
    save_json(rpath, report)
    try:
        import manifest
        db = manifest.connect(root)
        for j in all_jobs:
            for k in ("out_lod0", "out_col"):
                if j.get("col_mesh") or k == "out_lod0":
                    if Path(j[k]).exists():
                        manifest.add_file(j[k], source="pipeline/buildings_game.py", stage=1, root=root, db=db)
        manifest.add_file(rpath, source="pipeline/buildings_game.py", stage=1, root=root, db=db)
        db.commit()
        db.close()
    except Exception as e:
        print(f"warning: manifest not updated: {e}")
    print(f"visual {tot['visual_before']} -> {tot['visual_after']} triangles (target {TARGETS['visual_total']}), "
          f"{len(over_v)} buildings over {TARGETS['visual_building']}")
    print(f"collider {tot['col_before']} -> {tot['col_after']} triangles (target {TARGETS['col_total']}), "
          f"{len(over_c)} buildings over {TARGETS['col_building']}")
    print(f"max visual deviation {report['visual_dev_max_m']} m; {len(report['buildings_with_problems'])} buildings "
          f"with problems; {len(problems)} failures; report {rpath}")
    sys.exit(0 if ok and not problems else 1)


if __name__ == "__main__":
    main()
