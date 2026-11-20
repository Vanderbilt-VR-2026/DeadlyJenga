"""Build the tree meshes per tile from derived/trees/v1/trees.json (Stage 1).

For every tile with trees (campus and ring), writes to derived/trees/v1/<tile>/:
  trees_lod0.npz   all trees of the tile merged, kit LOD0 (<= 60 triangles per tree)
  trees_lod1.npz   the same with kit LOD1 (<= 16 triangles per tree)
  trees_col.npz    trunk colliders (closed 6-sided prisms from below the ground to the crown
                   base), only campus-zone trees whose trunk is >= 0.25 m (trunk diameter is
                   estimated as 0.03 x height, at least 0.2 m)
Only kind "tree" is built (shrubs and OSM nodes without a LiDAR tree are skipped).

The merged meshes repeat the kit UVs for every tree, so UV0 overlaps between different
trees. That is accepted for instanced props and recorded as "uv_shared_per_instance": true.

Budget: the whole campus must stay <= 400k LOD0 tree triangles. If it would not, small and
medium trees switch to their LOD1 kit at LOD0 ("lean" mode) and the report says so.
Report: derived/trees/v1/build.json and reports/stage1/trees_build.json.

--version v2 (Stage 5) builds the species trees instead: it reads derived/trees/v2/trees.json
(tree_species.py), uses tree_species.tree_mesh, writes the same files under
derived/trees/v2/<tile>/ (v1 is left untouched), compares triangles with v1 per tile, and
reports to reports/stage5/trees_v2_build.json (manifest stage 5).
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
import tree_kit as kit  # noqa: E402
import vcmesh  # noqa: E402
from common import data_root, fail, load_json, save_json  # noqa: E402

BUDGET_LOD0 = 400_000
MIN_TRUNK_D = 0.25
LICENSE = "Derived: USGS 3DEP LiDAR (public domain), OpenStreetMap (ODbL 1.0)"


def tree_mesh(tree, lod, version):
    if version == "v2":
        import tree_species as ts
        return ts.tree_mesh(tree, lod)
    return kit.tree_mesh(tree, lod)


def tree_tris(tree, lod, version):
    if version == "v2":
        import tree_species as ts
        return len(ts.species_mesh(tree["species"], ts.detail_of(float(tree["height"])), lod)["faces"])
    return len(kit.kit_mesh(kit.size_class(float(tree["height"])), kit.shape_of(tree), lod)["faces"])


def lod0_level(tree, lean):
    """Kit LOD to use for a tree's LOD0 mesh (lean mode drops small/medium trees to LOD1)."""
    if lean and kit.size_class(float(tree["height"])) in ("small", "medium"):
        return 1
    return 0


def build_tile(job):
    tile, trees, out_dir, lean, version = job
    out = Path(out_dir) / tile
    res = {"tile": tile, "trees": len(trees)}
    meta = {"tile": tile, "kit_version": kit.KIT_VERSION if version == "v1" else 2, "tree_version": version,
            "uv_shared_per_instance": True,
            "tree_ids": [t["id"] for t in trees], "frame": "campus meters, Z up", "source": "trees_build.py"}
    for name, lodf in (("trees_lod0", lambda t: lod0_level(t, lean)), ("trees_lod1", lambda t: 1)):
        m = vcmesh.merge([tree_mesh(t, lodf(t), version) for t in trees])
        vcmesh.save_mesh(out / f"{name}.npz", m["verts"], m["faces"], m["face_mat"], m["uv0"],
                         {**meta, "lod": name[-4:], "lean": bool(lean and name == "trees_lod0")})
        res[name] = int(len(m["faces"]))
        res[name + "_problems"] = vcmesh.check_mesh(m)
    cols = [t for t in trees if t["zone"] == "campus" and 2 * kit.trunk_radius(float(t["height"])) >= MIN_TRUNK_D]
    res["colliders"] = len(cols)
    cpath = out / "trees_col.npz"
    if cols:
        m = vcmesh.merge([kit.collider_mesh(t) for t in cols])
        vcmesh.save_mesh(cpath, m["verts"], m["faces"], m["face_mat"], m["uv0"],
                         {**meta, "tree_ids": [t["id"] for t in cols], "lod": "collider",
                          "note": "collision only; never rendered"})
        res["trees_col"] = int(len(m["faces"]))
        res["trees_col_problems"] = vcmesh.check_mesh(m, need_watertight=True)
    else:
        res["trees_col"] = 0
        if cpath.exists():
            cpath.unlink()           # a rerun with fewer colliders must not leave an old file
    return res


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--jobs", type=int, default=max(1, min(12, (os.cpu_count() or 2) - 2)))
    p.add_argument("--version", choices=["v1", "v2"], default="v1",
                   help="v1 = single-shape kit (Stage 1); v2 = species trees (Stage 5)")
    p.add_argument("--lean", action="store_true", help="force lean LOD0 (small/medium trees use the LOD1 kit)")
    p.add_argument("--no-manifest", action="store_true")
    args = p.parse_args(argv)
    t0 = time.time()
    root = data_root()
    version = args.version
    stage = 1 if version == "v1" else 5
    tdir = root / "derived/trees" / version
    report_path = root / ("reports/stage1/trees_build.json" if version == "v1" else "reports/stage5/trees_v2_build.json")
    if not (tdir / "trees.json").exists():
        fail("derived/trees/v1/trees.json is missing; run trees_detect.py first")
    data = load_json(tdir / "trees.json")
    trees = [t for t in data["trees"] if t["kind"] == "tree" and t["zone"] in ("campus", "ring")]

    # Budget check before writing anything.
    def lod0_tris(t, lean):
        return tree_tris(t, lod0_level(t, lean), version)
    campus_full = sum(lod0_tris(t, False) for t in trees if t["zone"] == "campus")
    lean = args.lean or campus_full > BUDGET_LOD0
    if lean and not args.lean:
        print(f"campus LOD0 would be {campus_full:,} triangles (> {BUDGET_LOD0:,}): lean mode")

    by_tile = {}
    for t in trees:
        by_tile.setdefault(t["tile"], []).append(t)
    jobs = [(tile, ts, str(tdir), lean, version) for tile, ts in sorted(by_tile.items())]
    # Remove tile folders of an earlier run that no longer have trees.
    for d in tdir.glob("T_*"):
        if d.is_dir() and d.name not in by_tile:
            for f in d.glob("trees_*.npz"):
                f.unlink()
    if args.jobs > 1:
        with Pool(args.jobs) as pool:
            results = pool.map(build_tile, jobs, chunksize=4)
    else:
        results = [build_tile(j) for j in jobs]

    problems = {r["tile"]: {k: v for k, v in r.items() if k.endswith("_problems") and v} for r in results}
    problems = {k: v for k, v in problems.items() if v}
    zone_of_tile = {}
    for t in trees:
        zone_of_tile.setdefault(t["tile"], set()).add(t["zone"])
    campus_lod0 = sum(lod0_tris(t, lean) for t in trees if t["zone"] == "campus")
    campus_lod1 = sum(tree_tris(t, 1, version) for t in trees if t["zone"] == "campus")
    sizes = {}
    for t in trees:
        key = (f"{kit.size_class(float(t['height']))}_{kit.shape_of(t)}" if version == "v1"
               else t["species"])
        sizes[key] = sizes.get(key, 0) + 1
    report = {
        "made": datetime.datetime.now().isoformat(timespec="seconds"),
        "kit_version": kit.KIT_VERSION if version == "v1" else 2,
        "tree_version": version,
        "trees_built": len(trees),
        "trees_campus": sum(t["zone"] == "campus" for t in trees),
        "trees_ring": sum(t["zone"] == "ring" for t in trees),
        "kit_use": dict(sorted(sizes.items())),
        "lean": lean,
        "budget_lod0_campus": BUDGET_LOD0,
        "campus_lod0_triangles": campus_lod0,
        "campus_lod1_triangles": campus_lod1,
        "campus_lod0_full_kit_triangles": campus_full,
        "all_lod0_triangles": sum(r["trees_lod0"] for r in results),
        "all_lod1_triangles": sum(r["trees_lod1"] for r in results),
        "colliders": sum(r["colliders"] for r in results),
        "collider_triangles": sum(r["trees_col"] for r in results),
        "tiles": len(results),
        "problems": problems,
        "uv_shared_per_instance": True,
        "per_tile": [{k: v for k, v in r.items() if not k.endswith("_problems")} for r in results],
    }
    v1b = root / "derived/trees/v1/build.json"
    if version == "v2" and v1b.exists():
        v1 = load_json(v1b)
        old = {r["tile"]: r for r in v1["per_tile"]}
        for r in report["per_tile"]:
            o = old.get(r["tile"])
            if o:
                r["v1_lod0"], r["v1_lod1"] = o["trees_lod0"], o["trees_lod1"]
        report["vs_v1"] = {
            "campus_lod0_v1": v1["campus_lod0_triangles"], "campus_lod0_v2": report["campus_lod0_triangles"],
            "campus_lod0_change_pct": round(100.0 * (report["campus_lod0_triangles"] / v1["campus_lod0_triangles"] - 1), 2),
            "all_lod0_change_pct": round(100.0 * (report["all_lod0_triangles"] / v1["all_lod0_triangles"] - 1), 2),
            "all_lod1_change_pct": round(100.0 * (report["all_lod1_triangles"] / v1["all_lod1_triangles"] - 1), 2),
            "max_tile_lod0_v1": max(o["trees_lod0"] for o in v1["per_tile"]),
            "max_tile_lod0_v2": max(r["trees_lod0"] for r in report["per_tile"]),
            "max_tile_change_pct": round(max(100.0 * (r["trees_lod0"] / r["v1_lod0"] - 1)
                                             for r in report["per_tile"] if r.get("v1_lod0")), 2),
            "note": "tree positions and sizes are the same as v1; only the model shapes differ",
        }
    save_json(tdir / "build.json", report)
    save_json(report_path, report)
    if not args.no_manifest:
        db = manifest.connect(root)
        for r in results:
            for name in ("trees_lod0", "trees_lod1", "trees_col"):
                f = tdir / r["tile"] / f"{name}.npz"
                if f.exists():
                    manifest.add_file(f, f"trees_build.py --version {version}", LICENSE, stage, root=root, db=db)
        for f in (tdir / "build.json", report_path):
            manifest.add_file(f, f"trees_build.py --version {version}", LICENSE, stage, root=root, db=db)
        db.commit(); db.close()
    print(f"{len(trees):,} trees in {len(results)} tiles; campus LOD0 {campus_lod0:,} / LOD1 {campus_lod1:,} tris "
          f"(budget {BUDGET_LOD0:,}{', lean' if lean else ''}); all LOD0 {report['all_lod0_triangles']:,}; "
          f"{report['colliders']:,} colliders; problems: {problems or 'none'} [{time.time() - t0:.0f} s]")
    if problems:
        sys.exit(1)


if __name__ == "__main__":
    main()
