"""Make the working LiDAR: campus-frame 100 m tiles from the USGS EPT copy (DECISIONS D007).

Steps:
1. Read the D22 EPT crop (EPSG:3857, Z in meters) in chunks.
2. Convert every point to the campus frame (frame.py: 3857 -> EPSG:6576 -> campus).
3. Keep only tiles that touch boundary + ring grown by 20 m.
4. Remove exact duplicates (the EPT crop holds every point twice): same source
   X, Y, Z, gps_time and return_number.
5. Write one LAZ per tile: derived/lidar/v1/tiles/T_<col>_<row>.laz
   (scale 0.001 m, campus meters, no CRS WKT; a text VLR says what the frame is)
   and derived/lidar/v1/index.json (tiles, counts per class, bounds, campus/ring/outside).
6. Validate: compare the tiles with every complete original D22 tile in raw/.../LAZ/
   (counts and nearest-neighbor distances) -> reports/stage0/ept_vs_original.json.
7. Optional (--with-2011): the 2011 EPT crop to derived/lidar/v1_2011/ as one file.

Every file written is added to the manifest (stage 0). Running it again rewrites
the same files.
"""
import argparse
import datetime
import json
import sys
import time
from pathlib import Path

import laspy
import numpy as np
import shapely
from shapely.geometry import box

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
from common import data_root, fail, save_json  # noqa: E402

EPT_D22 = "raw/lidar/TN_DavidsonCounty_D22/EPT/TN_DavidsonCounty_D22_campus_ring_ept.laz"
ORIGINALS = "raw/lidar/TN_DavidsonCounty_D22/LAZ"
EPT_2011 = "raw/lidar/TN_NASHVILLE_2011/EPT/TN_NASHVILLE_2011_campus_ring_ept.laz"
LICENSE = "Public domain (USGS 3DEP); credit: U.S. Geological Survey"
KEEP = ["intensity", "return_number", "number_of_returns", "classification", "gps_time"]
CHUNK = 5_000_000


def note_text(source, frame):
    return (f"Deadly Jenga Vanderbilt campus frame v{frame['version']}. Coordinates are campus meters: "
            "X = grid east, Y = grid north (EPSG:6576 grid), Z = NAVD88 height minus "
            f"{frame['vertical']['Z0_m']} m. Origin = Alumni Lawn centroid E {frame['origin']['E_ftUS']} "
            f"N {frame['origin']['N_ftUS']} ftUS (EPSG:6576), scale k = {frame['horizontal']['k']}. "
            f"See Tools/VanderbiltCampus/config/frame.json. Source: {source}.")


def kept_tiles(polys, buffer_m):
    """Set of (col, row) tiles that touch boundary + ring grown by buffer_m."""
    area = shapely.union_all(list(polys.values())).buffer(buffer_m)
    x0, y0, x1, y1 = area.bounds
    keep = set()
    for col in range(int(np.floor(x0 / fr.TILE)), int(np.floor(x1 / fr.TILE)) + 1):
        for row in range(int(np.floor(y0 / fr.TILE)), int(np.floor(y1 / fr.TILE)) + 1):
            if area.intersects(box(col * fr.TILE, row * fr.TILE, (col + 1) * fr.TILE, (row + 1) * fr.TILE)):
                keep.add((col, row))
    return keep


def tile_key(col, row):
    """One integer per tile, so numpy can group points quickly."""
    return (np.asarray(col, dtype=np.int64) + 10000) * 100000 + (np.asarray(row, dtype=np.int64) + 10000)


def key_to_tile(key):
    return int(key // 100000 - 10000), int(key % 100000 - 10000)


def dedupe(d):
    """Remove exact duplicates (same source X, Y, Z, gps_time, return_number). Returns (d, removed)."""
    n = len(d["gps_time"])
    if n == 0:
        return d, 0
    order = np.lexsort((d["return_number"], d["gps_time"], d["sZ"], d["sY"], d["sX"]))
    same = np.ones(n, dtype=bool)
    for f in ("sX", "sY", "sZ", "gps_time", "return_number"):
        v = d[f][order]
        same[1:] &= v[1:] == v[:-1]
    same[0] = False
    keep = order[~same]
    keep.sort()                      # keep the original point order
    return {f: v[keep] for f, v in d.items()}, int(same.sum())


def read_and_split(path, frame, keep, z_unit, epsg=3857):
    """Read a LAZ in chunks, convert to campus, and group points by kept tile."""
    keep_keys = np.array(sorted(tile_key(c, r) for c, r in keep), dtype=np.int64)
    groups = {}
    total = 0
    with laspy.open(path) as f:
        for chunk in f.chunk_iterator(CHUNK):
            total += len(chunk)
            X, Y, Z = fr.to_campus(np.asarray(chunk.x), np.asarray(chunk.y), np.asarray(chunk.z),
                                   epsg, z_unit, frame=frame)
            key = tile_key(np.floor(X / fr.TILE), np.floor(Y / fr.TILE))
            m = np.isin(key, keep_keys)
            if not m.any():
                continue
            part = {"x": X[m], "y": Y[m], "z": Z[m],
                    "sX": np.asarray(chunk.X)[m], "sY": np.asarray(chunk.Y)[m], "sZ": np.asarray(chunk.Z)[m]}
            for name in KEEP:
                part[name] = np.asarray(chunk[name])[m]
            k = key[m]
            order = np.argsort(k, kind="stable")
            k = k[order]
            cuts = np.flatnonzero(np.diff(k)) + 1
            starts = np.r_[0, cuts]
            ends = np.r_[cuts, len(k)]
            for s, e in zip(starts, ends):
                idx = order[s:e]
                groups.setdefault(int(k[s]), []).append({n: v[idx] for n, v in part.items()})
    merged = {key: {n: np.concatenate([p[n] for p in parts]) for n in parts[0]} for key, parts in groups.items()}
    return merged, total


def write_laz(path, d, note, offset):
    """Write campus-frame points as LAZ 1.4, point format 6, 1 mm scale, text VLR, no CRS."""
    header = laspy.LasHeader(point_format=6, version="1.4")
    header.scales = np.array([0.001, 0.001, 0.001])
    header.offsets = np.array(offset, dtype=float)
    header.system_identifier = "Deadly Jenga campus frame"
    header.generating_software = "prepare_lidar.py"
    header.vlrs.append(laspy.VLR(user_id="DeadlyJenga", record_id=1, description="campus frame v1 (no CRS)",
                                 record_data=note.encode("utf-8")))
    las = laspy.LasData(header)
    las.x, las.y, las.z = d["x"], d["y"], d["z"]
    for name in KEEP:
        las[name] = d[name]
    path.parent.mkdir(parents=True, exist_ok=True)
    las.write(path)


def class_counts(cls):
    v, c = np.unique(cls, return_counts=True)
    return {str(int(a)): int(b) for a, b in zip(v, c)}


def tile_zone(col, row, polys):
    b = box(col * fr.TILE, row * fr.TILE, (col + 1) * fr.TILE, (row + 1) * fr.TILE)
    campus = b.intersection(polys["campus"]).area / b.area
    ring = b.intersection(polys["backdrop_ring"]).area / b.area
    zone = "campus" if campus > 0 else ("ring" if ring > 0 else "outside")
    return zone, round(campus, 4), round(ring, 4)


def make_tiles(src, out_dir, frame, polys, buffer_m=20.0, z_unit="m", root=None, db=None, source_label=""):
    """Steps 1-5. Returns the index dict (also written to out_dir/index.json)."""
    keep = kept_tiles(polys, buffer_m)
    groups, total = read_and_split(src, frame, keep, z_unit)
    note = note_text(source_label or str(src), frame)
    tiles, removed, unique = [], 0, 0
    for key in sorted(groups, key=lambda k: key_to_tile(k)[::-1]):
        col, row = key_to_tile(key)
        d, r = dedupe(groups[key])
        removed += r
        unique += len(d["x"])
        name = fr.tile_name(col, row)
        rel = f"tiles/{name}.laz"
        write_laz(out_dir / rel, d, note, (col * fr.TILE, row * fr.TILE, 0.0))
        zone, cf, rf = tile_zone(col, row, polys)
        tiles.append({"name": name, "file": rel, "col": col, "row": row,
                      "bounds": [col * fr.TILE, row * fr.TILE, (col + 1) * fr.TILE, (row + 1) * fr.TILE],
                      "zone": zone, "campus_fraction": cf, "ring_fraction": rf,
                      "points": int(len(d["x"])), "duplicates_removed": r,
                      "classes": class_counts(d["classification"]),
                      "z_range": [round(float(d["z"].min()), 3), round(float(d["z"].max()), 3)]})
        if root is not None:
            manifest.add_file(out_dir / rel, f"prepare_lidar.py from {source_label}", LICENSE, 0, root=root, db=db)
    # Tiles in the area with no points at all are listed too (empty), so nothing is silently missing.
    have = {(t["col"], t["row"]) for t in tiles}
    empty_tiles = sorted(fr.tile_name(c, r) for c, r in keep - have)
    totals = {}
    for t in tiles:
        for c, n in t["classes"].items():
            totals[c] = totals.get(c, 0) + n
    zones = {}
    for t in tiles:
        zones[t["zone"]] = zones.get(t["zone"], 0) + 1
    index = {
        "version": out_dir.name,
        "made": datetime.datetime.now().isoformat(timespec="seconds"),
        "source": source_label,
        "frame_version": frame["version"],
        "frame": "campus meters (config/frame.json); Z = NAVD88 - Z0",
        "Z0_m": frame["vertical"]["Z0_m"],
        "scale_m": 0.001,
        "tile_size_m": fr.TILE,
        "selection": f"tiles touching boundary + ring grown by {buffer_m} m",
        "source_points": total,
        "points_in_kept_tiles_before_dedupe": unique + removed,
        "duplicates_removed": removed,
        "unique_points": unique,
        "tile_count": len(tiles),
        "tiles_by_zone": zones,
        "empty_tiles": empty_tiles,
        "classes": dict(sorted(totals.items(), key=lambda kv: int(kv[0]))),
        "tiles": tiles,
    }
    save_json(out_dir / "index.json", index)
    if root is not None:
        manifest.add_file(out_dir / "index.json", "prepare_lidar.py", LICENSE, 0, root=root, db=db)
    return index


def nn_stats(d):
    d = np.asarray(d)
    return {"median_mm": round(float(np.median(d)) * 1000, 3),
            "p95_mm": round(float(np.percentile(d, 95)) * 1000, 3),
            "p99_mm": round(float(np.percentile(d, 99)) * 1000, 3),
            "max_mm": round(float(d.max()) * 1000, 3),
            "share_le_2mm": round(float(np.mean(d <= 0.002)), 4),
            "share_le_5mm": round(float(np.mean(d <= 0.005)), 4),
            "share_le_10mm": round(float(np.mean(d <= 0.010)), 4)}


def validate_original(orig_path, ept_header_bounds, frame, version="v1", root=None, margin=1.0):
    """Compare one original D22 tile (EPSG:6576, ftUS) with the working tiles in their overlap."""
    from scipy.spatial import cKDTree
    o = laspy.read(orig_path)
    X, Y, Z = fr.to_campus(np.asarray(o.x), np.asarray(o.y), np.asarray(o.z), 6576, frame=frame)
    ocls = np.asarray(o.classification)
    # Overlap = original tile rectangle ∩ EPT crop rectangle ∩ working tiles (all in campus), shrunk by margin.
    ox0, oy0, ox1, oy1 = [float(v) for v in (o.header.mins[0], o.header.mins[1], o.header.maxs[0], o.header.maxs[1])]
    orect = shapely.transform(box(ox0, oy0, ox1, oy1).segmentize(10),
                              lambda x, y: fr.to_campus(x, y, 0 * x, 6576, frame=frame)[:2], interleaved=False)
    ex0, ey0, ex1, ey1 = ept_header_bounds
    erect = shapely.transform(box(ex0, ey0, ex1, ey1).segmentize(10),
                              lambda x, y: fr.to_campus(x, y, 0 * x, 3857, frame=frame)[:2], interleaved=False)
    # Only the area the working tiles cover (tiles touching boundary + ring + 20 m) can be compared.
    covered = shapely.union_all([box(*t["bounds"]) for t in lidar_io.load_index(version, root)["tiles"]])
    overlap = orect.intersection(erect).intersection(covered).buffer(-margin)
    if overlap.is_empty:
        return {"file": orig_path.name, "overlap_m2": 0}
    shapely.prepare(overlap)
    x0, y0, x1, y1 = overlap.bounds
    e = lidar_io.load_points(x0, y0, x1, y1, version=version, root=root)
    min_o = shapely.contains_xy(overlap, X, Y)
    min_e = shapely.contains_xy(overlap, e["x"], e["y"])
    O = np.c_[X[min_o], Y[min_o], Z[min_o]]
    E = np.c_[e["x"][min_e], e["y"][min_e], e["z"][min_e]]
    tree_o = cKDTree(O)
    d_eo, _ = tree_o.query(E, workers=-1)
    d_oe, _ = cKDTree(E).query(O, workers=-1)
    # Exact same pulses: match by gps_time + return_number, then measure the offset.
    ok = {}
    og = np.asarray(o.gps_time)[min_o]
    orn = np.asarray(o.return_number)[min_o]
    okey = np.round(og * 1e6).astype(np.int64) * 16 + orn
    ekey = np.round(e["gps_time"][min_e] * 1e6).astype(np.int64) * 16 + e["return_number"][min_e]
    common, oi, ei = np.intersect1d(okey, ekey, return_indices=True)
    if len(common):
        diff = O[oi] - E[ei]
        ok = {"matched_by_gps_time_and_return": int(len(common)),
              "dxy": nn_stats(np.hypot(diff[:, 0], diff[:, 1])),
              "dz": nn_stats(np.abs(diff[:, 2])),
              "d3": nn_stats(np.linalg.norm(diff, axis=1)),
              "mean_offset_mm": [round(float(v) * 1000, 3) for v in diff.mean(axis=0)]}
    return {
        "file": orig_path.name,
        "overlap_m2": round(overlap.area, 1),
        "original_points": int(min_o.sum()),
        "working_points": int(min_e.sum()),
        "original_withheld": int(np.asarray(o.withheld)[min_o].sum()),
        "classes_original": class_counts(ocls[min_o]),
        "classes_working": class_counts(e["classification"][min_e]),
        "nn_working_to_original": nn_stats(d_eo),
        "nn_original_to_working": nn_stats(d_oe),
        "same_pulse": ok,
    }


def do_2011(root, frame, polys, buffer_m, db):
    """2011 EPT crop -> one campus-frame file (comparison only)."""
    src = root / EPT_2011
    out = root / "derived/lidar/v1_2011/TN_NASHVILLE_2011_campus.laz"
    keep = kept_tiles(polys, buffer_m)
    groups, total = read_and_split(src, frame, keep, "m")
    d = {n: np.concatenate([g[n] for g in groups.values()]) for n in next(iter(groups.values()))}
    d, removed = dedupe(d)
    write_laz(out, d, note_text(EPT_2011, frame), (0.0, 0.0, 0.0))
    info = {"source": EPT_2011, "source_points": total, "duplicates_removed": removed,
            "unique_points": int(len(d["x"])), "classes": class_counts(d["classification"]),
            "bounds": [round(float(v), 3) for v in (d["x"].min(), d["y"].min(), d["x"].max(), d["y"].max())],
            "z_unit_source": "m", "file": out.name}
    save_json(out.parent / "index.json", info)
    manifest.add_file(out, "prepare_lidar.py from " + EPT_2011, LICENSE, 0, root=root, db=db)
    manifest.add_file(out.parent / "index.json", "prepare_lidar.py", LICENSE, 0, root=root, db=db)
    return info


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--version", default="v1", help="output folder name under derived/lidar/")
    p.add_argument("--buffer", type=float, default=20.0, help="meters around boundary + ring")
    p.add_argument("--skip-tiles", action="store_true", help="only validate (tiles already made)")
    p.add_argument("--with-2011", action="store_true", help="also convert the 2011 EPT crop")
    args = p.parse_args(argv)

    root = data_root()
    frame = fr.load_frame()
    polys = fr.boundary_polygons()
    src = root / EPT_D22
    if not src.exists():
        fail(f"missing {src}")
    out_dir = lidar_io.lidar_dir(args.version, root)
    db = manifest.connect(root)
    t = time.time()
    if not args.skip_tiles:
        index = make_tiles(src, out_dir, frame, polys, args.buffer, "m", root, db, EPT_D22)
        db.commit()
        print(f"{index['tile_count']} tiles {index['tiles_by_zone']}, {index['unique_points']:,} unique points, "
              f"{index['duplicates_removed']:,} duplicates removed ({time.time() - t:.0f} s)")

    # Validation against every complete original tile.
    with laspy.open(src) as f:
        eb = (f.header.mins[0], f.header.mins[1], f.header.maxs[0], f.header.maxs[1])
    results = []
    for orig in sorted((root / ORIGINALS).glob("*.laz")):
        r = validate_original(orig, eb, frame, args.version, root)
        results.append(r)
        print(json.dumps(r, indent=1))
    report = root / "reports/stage0/ept_vs_original.json"
    save_json(report, {"made": datetime.datetime.now().isoformat(timespec="seconds"),
                       "note": "Original D22 tiles (EPSG:6576, ftUS) vs working tiles from the EPT copy, "
                               "both in the campus frame, inside their overlap (original tile, EPT crop "
                               "box and working tiles) shrunk by 1 m.",
                       "tiles": results})
    manifest.add_file(report, "prepare_lidar.py", LICENSE, 0, root=root, db=db)
    db.commit()          # do not hold the manifest lock during the 2011 step

    if args.with_2011:
        info = do_2011(root, frame, polys, args.buffer, db)
        print("2011:", json.dumps({k: v for k, v in info.items() if k != "classes"}))
    db.commit()
    db.close()


if __name__ == "__main__":
    main()
