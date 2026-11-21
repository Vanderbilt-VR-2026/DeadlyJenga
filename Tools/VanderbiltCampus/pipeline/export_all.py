"""Stage 1 export driver: unity_map.json, then one FBX per tile, then checks.

    python pipeline/export_all.py                  # everything that changed
    python pipeline/export_all.py --force          # rebuild every tile
    python pipeline/export_all.py --tiles T_00_00 T_01_00 --jobs 4
    python pipeline/export_all.py --skip-map --out C:/temp/fbx   # test folder

Steps:
  1. pipeline/make_unity_map.py -> config/unity_map.json (skip with --skip-map)
  2. plan: which npz files and markers go into which tile (tiles with no
     terrain are skipped) -> derived/export/v1/plan.json
  3. headless Blender (blender/export_tiles.py), --jobs processes in parallel,
     each with a slice of the tiles -> Models/T_<col>_<row>.fbx and
     derived/export/v1/<tile>.json (stats)
  4. pipeline/check_fbx.py on every exported tile -> derived/export/v1/check/<tile>.json
  5. reports/stage1/export_report.json in the archive

A tile is re-exported only when its plan (input files, their sizes and times,
markers, exporter code) changed or its last check failed; --force redoes all.
Exits non-zero if anything failed.

Archive inputs (VC_DATA_ROOT overrides the archive root):
  derived/terrain/v1/<tile>/terrain.npz, terrain_col.npz
  derived/buildings/v2/index.json (when present: which version folder per building), else
  buildings/<id>/v1/model.json (key "tile"); in the folder lod2.npz (or game_lod0.npz from buildings_game.py) -> B_<id>_LOD0,
  lod1.npz -> B_<id>_LOD1, col_mesh.npz (or game_col.npz) -> COLM_<id> (one non-convex collider; replaces
  the convex col_<n>.npz -> COL_<id>_<n>)
  derived/trees/v2/<tile>/ (else v1) trees_lod0.npz, trees_lod1.npz, trees_col.npz  (Trees, Trees_LOD1, COL_Trees)
  derived/props/v1/tiles/<tile>/props_lod0.npz, props_lod1.npz, props_col.npz  (Props, Props_LOD1, COL_Props;
                                                          not in the HLOD)
  derived/terrain/v1/<tile>/terrain_far.npz: ground of the HLOD object (fallback: terrain_col.npz)
  terrain_col.npz is also drawn as Terrain_LOD1 (the builder groups Terrain/Terrain_LOD1 and
  Trees/Trees_LOD1 under LODGroups)
  derived/buildings/ring/v1/<tile>.npz
  derived/lidar/v1/index.json (zone of each tile: campus / ring / outside)
"""
import argparse
import hashlib
import json
import re
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from common import CONFIG, REPO, data_root, fail, load_json, save_json  # noqa: E402

TOOLS = HERE.parent
EXPORTER = TOOLS / "blender" / "export_tiles.py"
CHECKER = HERE / "check_fbx.py"
DEFAULT_OUT = REPO / "Assets" / "_DeadlyJenga" / "World" / "Maps" / "VanderbiltCampus" / "Models"
TILE_RE = re.compile(r"^T_(-?\d{2,})_(-?\d{2,})$")
COL_RE = re.compile(r"^col_(\d+)\.npz$")
RING_COLLIDER_DISTANCE = 20.0     # ring tiles keep COL_Terrain only this close to the campus


def tile_name(col, row):
    f = lambda v: ("-" if v < 0 else "") + "%02d" % abs(int(v))  # noqa: E731
    return f"T_{f(col)}_{f(row)}"


def tile_of(x, y):
    import math
    return tile_name(math.floor(x / 100.0), math.floor(y / 100.0))


def tile_bounds(name):
    m = TILE_RE.match(name)
    c, r = int(m.group(1)), int(m.group(2))
    return c * 100.0, r * 100.0, (c + 1) * 100.0, (r + 1) * 100.0


def safe_name(text):
    """Letters, digits and underscores only (object names in the FBX)."""
    return re.sub(r"[^A-Za-z0-9_]+", "", text.replace(" ", "_")) or "x"


# ---- planning -----------------------------------------------------------------

def tile_zones(root):
    path = root / "derived" / "lidar" / "v1" / "index.json"
    if not path.exists():
        return {}
    return {t["name"]: t.get("zone", "campus") for t in load_json(path)["tiles"]}


def near_campus(tile, campus_poly):
    """True when a tile square is within RING_COLLIDER_DISTANCE of the campus boundary."""
    if campus_poly is None:
        return True
    from shapely.geometry import box
    return campus_poly.distance(box(*tile_bounds(tile))) <= RING_COLLIDER_DISTANCE


def campus_polygon():
    try:
        import frame
        return frame.boundary_polygons()["campus"]
    except Exception as e:   # tests run with a fake archive and no boundary
        print(f"note: no campus boundary ({e}); every ring tile keeps COL_Terrain")
        return None


def _v2_index(root):
    """Records from derived/buildings/v2/index.json, or None when there is no v2 index.
    Each record: id, tile, folder (the version folder to export), footprint (coords or None)."""
    path = root / "derived" / "buildings" / "v2" / "index.json"
    if not path.exists():
        return None
    data = load_json(path)
    items = data.get("buildings", data) if isinstance(data, dict) else data
    if isinstance(items, dict):
        items = [dict(v, id=k) for k, v in items.items()]
    out = []
    for it in items:
        bid = str(it.get("id") or it.get("building") or it.get("building_id"))
        if not bid.startswith("B_"):
            bid = "B_" + bid
        folder = it.get("folder") or it.get("dir") or it.get("path")
        if folder:
            folder = Path(folder) if Path(folder).is_absolute() else root / folder
        else:
            v = str(it.get("version") or it.get("export_version") or "v2")
            folder = root / "buildings" / bid / (v if v.startswith("v") else "v" + v)
        out.append({"id": bid, "tile": it.get("tile"), "folder": folder,
                    "footprint": it.get("footprint") or it.get("polygon")})
    return out


def building_records(root):
    """Every building to export: [{id, tile, folder, footprint}].
    From the v2 index when it exists, else buildings/<id>/v1 folders with a model.json."""
    recs = _v2_index(root)
    if recs is not None:
        return recs
    out = []
    for model in sorted((root / "buildings").glob("*/v1/model.json")):
        folder = model.parent
        bid = folder.parent.name
        if not bid.startswith("B_"):
            bid = "B_" + bid
        try:
            info = load_json(model)
        except Exception as e:
            print(f"warning: unreadable {model}: {e}")
            continue
        tile = info.get("tile")
        if not tile and (folder / "lod1.npz").exists():
            import numpy as np
            with np.load(folder / "lod1.npz") as z:
                c = z["verts"][:, :2].mean(0)
            tile = tile_of(*c)
        out.append({"id": bid, "tile": tile, "folder": folder, "footprint": None})
    return out


def building_index(root):
    """tile -> [(building id, folder)]."""
    out = {}
    for r in building_records(root):
        if r["tile"]:
            out.setdefault(r["tile"], []).append((r["id"], Path(r["folder"])))
    return out


def lod3_folders(root):
    """{building id: folder} from derived/buildings/lod3/index.json (Stage 4 LoD3 models)."""
    path = root / "derived" / "buildings" / "lod3" / "index.json"
    if not path.exists():
        return {}
    out = {}
    for it in load_json(path).get("buildings", []):
        f = Path(it["folder"])
        out[str(it["id"])] = f if f.is_absolute() else root / f
    return out


def building_objects(bid, folder, lod3=None):
    """Plan entries for one building. col_mesh.npz (v2: one non-convex collider) replaces col_<n>.npz.
    lod3: the building's LoD3 folder, if any: lod3.npz -> LOD0, its lod1.npz -> LOD1, its
    game_col.npz -> COLM. (No LOD2 object: the Unity builder only knows LOD0 and LOD1.)"""
    if lod3 is not None and (lod3 / "lod3.npz").exists():
        objs = [{"name": f"{bid}_LOD0", "role": "building_lod0", "src": [str(lod3 / "lod3.npz")], "lod3": True}]
        lod1 = lod3 / "lod1.npz" if (lod3 / "lod1.npz").exists() else folder / "lod1.npz"
        if lod1.exists():
            objs.append({"name": f"{bid}_LOD1", "role": "building_lod1", "src": [str(lod1)]})
        colm = next((c for c in (lod3 / "game_col.npz", folder / "game_col.npz", folder / "col_mesh.npz")
                     if c.exists()), None)
        if colm is not None:
            objs.append({"name": f"COLM_{bid}", "role": "building_colm", "collider": True, "src": [str(colm)]})
        return objs
    objs = []
    # Light game meshes from buildings_game.py are preferred when present.
    lod0 = folder / "game_lod0.npz" if (folder / "game_lod0.npz").exists() else folder / "lod2.npz"
    if lod0.exists():
        objs.append({"name": f"{bid}_LOD0", "role": "building_lod0", "src": [str(lod0)]})
    if (folder / "lod1.npz").exists():
        objs.append({"name": f"{bid}_LOD1", "role": "building_lod1", "src": [str(folder / "lod1.npz")]})
    colm = folder / "game_col.npz" if (folder / "game_col.npz").exists() else folder / "col_mesh.npz"
    if colm.exists():
        objs.append({"name": f"COLM_{bid}", "role": "building_colm", "collider": True, "src": [str(colm)]})
    else:
        for col in sorted(folder.glob("col_*.npz")):
            m = COL_RE.match(col.name)
            if m:
                objs.append({"name": f"COL_{bid}_{int(m.group(1))}", "role": "building_col",
                             "collider": True, "src": [str(col)]})
    return objs


def markers_by_tile(unity_map):
    """Spawn_/Route_/Climb_ empties from unity_map.json, grouped by the tile holding them."""
    out = {}
    if not unity_map:
        return out

    def campus(p):            # Unity (x, y up, z north) -> campus (X, Y, Z)
        return [float(p[0]), float(p[2]), float(p[1])]

    def put(name, pos, forward=None):
        out.setdefault(tile_of(pos[0], pos[1]), []).append({"name": name, "pos": pos, "forward": forward})

    for s in unity_map.get("spawns", []):
        put("Spawn_" + safe_name(s["name"]), campus(s["pos"]))
    route = unity_map.get("route", [])
    width = max(2, len(str(len(route))))
    for i, p in enumerate(route):
        put(f"Route_{i:0{width}d}", campus(p))
    seen = set()
    for c in unity_map.get("climb", []):
        name = "Climb_" + safe_name(c["building"])
        while name in seen:
            name += "_b"
        seen.add(name)
        f = c.get("forward", [0, 0, 1])
        put(name, campus(c["pos"]), [float(f[0]), float(f[2])])
    return out


def file_sig(paths):
    return [[str(p), Path(p).stat().st_size, int(Path(p).stat().st_mtime)] for p in paths]


def plan_tiles(root, out_dir, unity_map, only=None):
    """Return (plans, skipped_empty) for every tile that has terrain."""
    terrain_root = root / "derived" / "terrain" / "v1"
    zones = tile_zones(root)
    buildings = building_index(root)
    lod3 = lod3_folders(root)
    markers = markers_by_tile(unity_map)
    campus_poly = None
    tiles = sorted(p.name for p in terrain_root.glob("T_*") if p.is_dir() and TILE_RE.match(p.name)) \
        if terrain_root.exists() else []
    if only:
        tiles = [t for t in tiles if t in set(only)]
    export_dir = root / "derived" / "export" / "v1"
    code_sig = file_sig([EXPORTER, TOOLS / "blender" / "vc_fbx.py"])
    plans, empty = [], []
    for tile in tiles:
        tdir = terrain_root / tile
        if not (tdir / "terrain.npz").exists():
            empty.append(tile)
            continue
        zone = zones.get(tile, "campus")
        objects = [{"name": "Terrain", "role": "terrain", "src": [str(tdir / "terrain.npz")]}]
        if (tdir / "terrain_col.npz").exists():
            # Cheap far-distance terrain: the decimated collider surface drawn as a visual mesh
            # (its own material indices, colors and UVs). Every tile that has one gets it.
            objects.append({"name": "Terrain_LOD1", "role": "terrain_lod1", "src": [str(tdir / "terrain_col.npz")]})
            keep = zone != "ring"
            if not keep:
                if campus_poly is None:
                    campus_poly = campus_polygon() or False
                keep = near_campus(tile, campus_poly or None)
            if keep:
                objects.append({"name": "COL_Terrain", "role": "terrain_col", "collider": True,
                                "src": [str(tdir / "terrain_col.npz")]})
        for bid, folder in sorted(buildings.get(tile, [])):
            objects += building_objects(bid, folder, lod3.get(bid))
        # Trees: the v2 folder when it exists (same layout), else v1.
        trees = root / "derived" / "trees" / "v2" / tile
        if not trees.is_dir():
            trees = root / "derived" / "trees" / "v1" / tile
        if (trees / "trees_lod0.npz").exists():
            objects.append({"name": "Trees", "role": "trees", "src": [str(trees / "trees_lod0.npz")]})
        if (trees / "trees_lod1.npz").exists():
            objects.append({"name": "Trees_LOD1", "role": "trees_lod1", "src": [str(trees / "trees_lod1.npz")]})
        if (trees / "trees_col.npz").exists():
            objects.append({"name": "COL_Trees", "role": "trees_col", "collider": True,
                            "src": [str(trees / "trees_col.npz")]})
        # Props: handled like the trees, but never part of the HLOD.
        props = root / "derived" / "props" / "v1" / "tiles" / tile
        for fname, name, role, col in (("props_lod0.npz", "Props", "props", False),
                                       ("props_lod1.npz", "Props_LOD1", "props_lod1", False),
                                       ("props_col.npz", "COL_Props", "props_col", True)):
            if (props / fname).exists():
                o = {"name": name, "role": role, "src": [str(props / fname)]}
                if col:
                    o["collider"] = True
                objects.append(o)
        ring = root / "derived" / "buildings" / "ring" / "v1" / f"{tile}.npz"
        if ring.exists():
            objects.append({"name": "Ring", "role": "ring", "src": [str(ring)]})
        # HLOD: the far version of the whole tile in one mesh and one material slot (one draw call).
        # Ground: terrain_far.npz (about 200 triangles with a border skirt), else the Terrain_LOD1 source.
        if (tdir / "terrain_far.npz").exists():
            far, ground = [str(tdir / "terrain_far.npz")], "terrain_far"
        else:
            far = [o["src"][0] for o in objects if o["role"] == "terrain_lod1"]
            ground = "terrain_col (no terrain_far.npz)"
        far += [o["src"][0] for o in objects if o["role"] in ("building_lod1", "trees_lod1", "ring")]
        if far:
            objects.append({"name": "HLOD", "role": "hlod", "src": far, "single_material": "mat_mixed",
                            "note": f"ground from {ground} + building LOD1 + Trees_LOD1 + Ring merged; one slot "
                                    "mat_mixed, color R = material index; UV0 copied per source, so charts overlap"})
        empties = markers.get(tile, [])
        plan = {"tile": tile, "zone": zone, "fbx": str(Path(out_dir) / f"{tile}.fbx"),
                "stats": str(export_dir / f"{tile}.json"), "check": str(export_dir / "check" / f"{tile}.json"),
                "objects": objects, "empties": empties}
        sig_src = {"objects": [[o["name"], file_sig(o["src"])] for o in objects], "empties": empties,
                   "code": code_sig, "fbx": plan["fbx"]}
        plan["signature"] = hashlib.sha1(json.dumps(sig_src, sort_keys=True).encode()).hexdigest()
        plans.append(plan)
    return plans, empty


def up_to_date(plan):
    """Same signature as the last export, FBX present, and its check passed."""
    try:
        stats = load_json(plan["stats"])
        check = load_json(plan["check"])
    except Exception:
        return False
    return (stats.get("signature") == plan["signature"] and Path(plan["fbx"]).exists()
            and check.get("pass") and check.get("signature") == plan["signature"])


# ---- driver -----------------------------------------------------------------

def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--tiles", nargs="*", help="only these tiles")
    p.add_argument("--jobs", type=int, default=6, help="parallel Blender processes (default 6)")
    p.add_argument("--force", action="store_true", help="re-export even if nothing changed")
    p.add_argument("--out", type=Path, default=DEFAULT_OUT, help="FBX folder (default: the Unity Models folder)")
    p.add_argument("--skip-map", action="store_true", help="do not re-run make_unity_map.py")
    p.add_argument("--preview-map", action="store_true", help="run make_unity_map.py --preview (stand-in inputs)")
    p.add_argument("--unity-map", type=Path, default=CONFIG / "unity_map.json")
    p.add_argument("--report", type=Path, help="report path (default archive reports/stage1/export_report.json)")
    args = p.parse_args(argv)
    t0 = time.time()
    root = data_root()
    failures = []

    if not args.skip_map:
        cmd = [sys.executable, str(HERE / "make_unity_map.py"), "--out", str(args.unity_map)]
        r = subprocess.run(cmd + (["--preview"] if args.preview_map else []))
        if r.returncode != 0:
            failures.append("make_unity_map.py failed (markers come from the previous unity_map.json if any)")
    unity_map = load_json(args.unity_map) if args.unity_map.exists() else {}
    if not unity_map:
        print("note: no unity_map.json; tiles get no Spawn_/Route_/Climb_ markers")

    plans, empty = plan_tiles(root, args.out, unity_map, args.tiles)
    if not plans:
        fail("no tile has derived/terrain/v1/<tile>/terrain.npz yet; nothing to export")
    export_dir = root / "derived" / "export" / "v1"
    plan_path = export_dir / "plan.json"
    save_json(plan_path, {"made": time.strftime("%Y-%m-%dT%H:%M:%S"), "tiles": plans})
    todo = [pl["tile"] for pl in plans if args.force or not up_to_date(pl)]
    print(f"{len(plans)} tiles with terrain ({len(empty)} without), {len(todo)} to export")

    sys.path.insert(0, str(HERE))
    import check_fbx
    logs = export_dir / "logs"
    if todo:
        ok, lines = check_fbx.run_parallel(EXPORTER, plan_path, todo, args.jobs, logs)
        for ln in lines:
            print(ln)
        if not ok:
            failures += [ln for ln in lines if ln.startswith(("FAILED", "Error", "Traceback"))] or ["exporter failed"]
        ok, lines = check_fbx.run_parallel(CHECKER, plan_path, todo, args.jobs, logs)
        for ln in lines:
            print(ln)

    # Report over every planned tile (fresh or up to date).
    from collections import Counter
    tris, objects, fbx_bytes, tiles = Counter(), 0, 0, []
    for pl in plans:
        try:
            stats = load_json(pl["stats"])
            check = load_json(pl["check"])
        except Exception:
            failures.append(f"{pl['tile']}: no stats or check file")
            continue
        for o in stats["objects"]:
            tris[o["role"]] += o["triangles"]
        objects += len(stats["objects"])
        fbx_bytes += stats.get("fbx_bytes", 0)
        if not check.get("pass"):
            failures.append(f"{pl['tile']}: " + "; ".join(check.get("problems", [])[:5]))
        tiles.append({"tile": pl["tile"], "zone": pl["zone"], "objects": len(stats["objects"]),
                      "empties": len(stats.get("empties", [])), "triangles": sum(o["triangles"] for o in stats["objects"]),
                      "fbx_bytes": stats.get("fbx_bytes", 0), "check": bool(check.get("pass")),
                      "roles": sorted({o["role"] for o in stats["objects"]})})
    report = {"made": time.strftime("%Y-%m-%dT%H:%M:%S"), "seconds": round(time.time() - t0, 1),
              "fbx_folder": str(args.out), "unity_map": str(args.unity_map),
              "tiles_exported": len(tiles), "tiles_without_terrain": empty, "tiles_redone": todo,
              "objects": objects, "triangles_per_role": dict(tris), "fbx_bytes_total": fbx_bytes,
              "fbx_mb_total": round(fbx_bytes / 1e6, 1), "failures": failures, "tiles": tiles,
              "settings": "blender/vc_fbx.py FBX_SETTINGS; axes: Unity = (X, Z, Y)"}
    report_path = args.report or root / "reports" / "stage1" / "export_report.json"
    save_json(report_path, report)
    try:
        import manifest
        db = manifest.connect(root)
        for path in [report_path, plan_path] + [Path(pl["stats"]) for pl in plans] + [Path(pl["check"]) for pl in plans]:
            if path.exists() and root.resolve() in path.resolve().parents:
                manifest.add_file(path, source="pipeline/export_all.py", stage=1, root=root, db=db)
        db.commit()
        db.close()
    except Exception as e:
        print(f"warning: manifest not updated: {e}")
    print(f"report {report_path}: {len(tiles)} tiles, {objects} objects, {report['fbx_mb_total']} MB FBX, "
          f"{len(failures)} failures")
    for f_ in failures[:20]:
        print("FAIL", f_)
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
