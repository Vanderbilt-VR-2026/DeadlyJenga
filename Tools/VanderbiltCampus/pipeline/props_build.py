"""Write the prop library and the per-tile prop meshes (Stage 5).

Reads derived/props/v1/instances.json (props_place.py) and writes:
  derived/props/v1/library/<kind>_lod0.npz, <kind>_lod1.npz, <kind>_col.npz
      each kind once, in its local frame (origin on the ground, Z up, front = +Y)
  derived/props/v1/tiles/<tile>/props_lod0.npz, props_lod1.npz, props_col.npz
      every prop of the tile merged, placed in the campus frame (meters, Z up) exactly like
      derived/trees/v1/<tile>/trees_*.npz, so the exporter can treat them like
      Trees / Trees_LOD1 / COL_Trees. UV0 repeats the library UVs per instance
      ("uv_shared_per_instance": true).
  derived/props/v1/build.json and reports/stage5/props_build.json (triangles per tile).
Everything written is recorded in the manifest (stage 5).
"""
import argparse
import datetime
import os
import sys
import time
from multiprocessing import Pool
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import manifest  # noqa: E402
import props_kit as pk  # noqa: E402
import vcmesh  # noqa: E402
from common import data_root, fail, load_json, save_json  # noqa: E402

LICENSE = "Derived: OpenStreetMap (ODbL 1.0); meshes made in code"


def build_tile(job):
    tile, items, out_dir = job
    out = Path(out_dir) / tile
    meta = {"tile": tile, "kit_version": pk.KIT_VERSION, "uv_shared_per_instance": True,
            "prop_ids": [i["id"] for i in items], "frame": "campus meters, Z up", "source": "props_build.py"}
    res = {"tile": tile, "props": len(items)}
    for name, lod in (("props_lod0", 0), ("props_lod1", 1), ("props_col", "col")):
        m = vcmesh.merge([pk.placed(i["kind"], lod, i["x"], i["y"], i["z"], i["heading_deg"], i["scale"])
                          for i in items])
        vcmesh.save_mesh(out / f"{name}.npz", m["verts"], m["faces"], m["face_mat"], m["uv0"],
                         {**meta, "lod": str(lod), **({"note": "collision only; never rendered"} if lod == "col" else {})})
        res[name] = int(len(m["faces"]))
        probs = vcmesh.check_mesh(m, need_watertight=False)
        if probs:
            res[name + "_problems"] = probs
    return res


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--jobs", type=int, default=min(4, os.cpu_count() or 1), help="worker processes (default 4)")
    p.add_argument("--no-manifest", action="store_true")
    args = p.parse_args(argv)
    t0 = time.time()
    root = data_root()
    pdir = root / "derived/props/v1"
    if not (pdir / "instances.json").exists():
        fail("derived/props/v1/instances.json is missing; run props_place.py first")
    inst = load_json(pdir / "instances.json")["instances"]

    # 1. Library.
    rows, bad = pk.check_all()
    if bad:
        fail(f"prop kit problems: {[r for r in rows if r[3]]}")
    files, lib = [], {}
    for kind in pk.KINDS:
        lib[kind] = {}
        for lod, suf in ((0, "lod0"), (1, "lod1"), ("col", "col")):
            m = pk.prop_mesh(kind, lod)
            f = pdir / "library" / f"{kind}_{suf}.npz"
            vcmesh.save_mesh(f, m["verts"], m["faces"], m["face_mat"], m["uv0"], m["meta"])
            files.append(f)
            lib[kind][suf] = int(len(m["faces"]))

    # 2. Tiles.
    by_tile = {}
    for i in inst:
        by_tile.setdefault(i["tile"], []).append(i)
    tdir = pdir / "tiles"
    for d in tdir.glob("T_*"):
        if d.is_dir() and d.name not in by_tile:
            for f in d.glob("props_*.npz"):
                f.unlink()
    jobs = [(t, items, str(tdir)) for t, items in sorted(by_tile.items())]
    if args.jobs > 1:
        with Pool(args.jobs) as pool:
            results = pool.map(build_tile, jobs, chunksize=4)
    else:
        results = [build_tile(j) for j in jobs]
    problems = {r["tile"]: {k: v for k, v in r.items() if k.endswith("_problems")} for r in results}
    problems = {k: v for k, v in problems.items() if v}
    for r in results:
        for name in ("props_lod0", "props_lod1", "props_col"):
            files.append(tdir / r["tile"] / f"{name}.npz")

    lod0 = [r["props_lod0"] for r in results]
    report = {
        "made": datetime.datetime.now().isoformat(timespec="seconds"),
        "kit_version": pk.KIT_VERSION,
        "library_triangles": lib,
        "instances": len(inst),
        "tiles": len(results),
        "triangles_lod0": int(sum(lod0)), "triangles_lod1": int(sum(r["props_lod1"] for r in results)),
        "triangles_col": int(sum(r["props_col"] for r in results)),
        "per_tile_lod0_max": int(max(lod0)) if lod0 else 0,
        "per_tile_lod0_median": float(np.median(lod0)) if lod0 else 0,
        "uv_shared_per_instance": True,
        "problems": problems,
        "per_tile": [{k: v for k, v in r.items() if not k.endswith("_problems")} for r in results],
    }
    save_json(pdir / "build.json", report)
    rp = root / "reports/stage5/props_build.json"
    save_json(rp, report)
    files += [pdir / "build.json", rp]
    if not args.no_manifest:
        db = manifest.connect(root)
        for f in files:
            manifest.add_file(f, "props_build.py + props_kit.py", LICENSE, 5, root=root, db=db)
        db.commit(); db.close()
    print(f"{len(inst)} props in {len(results)} tiles; LOD0 {report['triangles_lod0']:,} "
          f"(max {report['per_tile_lod0_max']:,}/tile), LOD1 {report['triangles_lod1']:,}, "
          f"COL {report['triangles_col']:,}; problems: {problems or 'none'} [{time.time() - t0:.0f} s]")
    if problems:
        sys.exit(1)


if __name__ == "__main__":
    main()
