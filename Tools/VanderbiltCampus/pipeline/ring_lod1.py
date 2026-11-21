"""LoD1 buildings for the 150 m backdrop ring (Stage 1, STANDARDS.md sections 2-3).

Which buildings: those whose footprint centroid lies in the backdrop ring (not the campus).
Footprints: derived/buildings/v1/ring_footprints.json if the buildings step has written it;
otherwise OSM buildings, plus Metro footprints where OSM has no building (Metro-only ids are
B_new_<Metro OBJECTID>).

Height (meters above ground), first that works, the source is recorded:
  lidar_class6        90th percentile of class-6 (building) point heights inside the footprint
  lidar_first_return  90th percentile of first-return class-1 points inside (no class 6 there)
  metro_height        Metro "Height" (feet -> meters)
  osm_levels          3 m x building:levels
  default_6m          6 m
Ground Z = median of LiDAR ground (class 2) points 0.5-4 m outside the footprint (DTM if none).

Mesh: the footprint extruded from (lowest ground - 0.5 m) to the roof height; flat top
(roof_flat), walls brick_buff if lower than 12 m else concrete (a logged guess), closed and
watertight. All ring buildings of a tile are merged into one mesh with one tile UV atlas
(every roof, bottom and wall face is its own chart; charts never overlap). No colliders: the
ring is outside PlayableBounds.

Writes: derived/buildings/ring/v1/<tile>.npz, derived/buildings/ring/v1/ring.json,
reports/stage1/ring_lod1.json (height check) and reports/stage1/ring_overview.png.
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
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
import trees_common as tc  # noqa: E402
import vcmesh  # noqa: E402
from common import data_root, fail, load_json, save_json  # noqa: E402

LICENSE = "Derived: OpenStreetMap (ODbL 1.0), USGS 3DEP LiDAR (public domain), Metro Nashville open data"
TALL = 12.0              # walls: brick_buff below this height, concrete above (a guess)
BASE_DROP = 0.5          # walls start this far below the lowest nearby ground
MIN_ROOF_POINTS = 10
CHART_PAD = 0.05


# ---- footprints -------------------------------------------------------------

def load_footprints(root):
    """Ring building records: {id, osm, poly, levels, metro_height, footprint_source}."""
    import shapely
    from shapely.geometry import Polygon
    polys = tc.zone_polys()
    given = root / "derived/buildings/v1/ring_footprints.json"
    metro = tc.metro_footprints(root)
    mtree = shapely.STRtree([m["poly"] for m in metro])

    def metro_match(poly):
        """Metro footprint overlapping this polygon the most (for its height)."""
        best, share = None, 0.0
        for j in mtree.query(poly, predicate="intersects"):
            s = metro[j]["poly"].intersection(poly).area / max(poly.area, 1e-6)
            if s > share:
                best, share = metro[j], s
        return best if share > 0.3 else None

    out = []
    if given.exists():
        data = load_json(given)
        osm_tags = {b["id"]: b["tags"] for b in tc.osm_buildings(root)}
        for b in data.get("buildings", data) if isinstance(data, dict) else data:
            poly = reconciled_polygon(b)
            if poly is None or poly.is_empty:
                continue
            m = metro_match(poly)
            lv = osm_tags.get(b["id"], {}).get("building:levels")
            try:
                lv = float(lv) if lv is not None else None
            except ValueError:
                lv = None
            out.append({"id": b["id"], "osm": b.get("osm"), "poly": poly, "levels": lv,
                        "metro_height": m["height_m"] if m else None,
                        "footprint_source": "buildings_step:" + str(b.get("footprint_source")),
                        "step_top_z": b.get("top_z"), "step_ground_z": b.get("ground_z")})
        return out, "derived/buildings/v1/ring_footprints.json"

    osm = tc.osm_buildings(root)
    for b in osm:
        c = b["poly"].centroid
        if polys["campus"].contains(c) or not polys["backdrop_ring"].contains(c):
            continue
        m = metro_match(b["poly"])
        lv = b["tags"].get("building:levels")
        try:
            lv = float(lv) if lv is not None else None
        except ValueError:
            lv = None
        out.append({"id": b["id"], "osm": b["osm"], "poly": b["poly"], "levels": lv,
                    "metro_height": m["height_m"] if m else None, "footprint_source": "osm"})
    otree = shapely.STRtree([b["poly"] for b in osm])
    for m in metro:
        c = m["poly"].centroid
        if polys["campus"].contains(c) or not polys["backdrop_ring"].contains(c):
            continue
        covered = sum(osm[j]["poly"].intersection(m["poly"]).area
                      for j in otree.query(m["poly"], predicate="intersects"))
        if covered > 0.3 * m["poly"].area:
            continue                       # OSM already has this building
        out.append({"id": f"B_new_{m['objectid']}", "osm": None, "poly": m["poly"], "levels": None,
                    "metro_height": m["height_m"], "footprint_source": "metro"})
    return out, "OSM + Metro footprints"


def reconciled_polygon(b):
    """Polygon of a buildings-step record: "polygon" = list of parts, each [exterior, holes...]
    (rings may be open); older/other layouts with "footprint" are accepted too."""
    from shapely.geometry import MultiPolygon, Polygon
    if "polygon" in b:
        parts = [Polygon(p[0], p[1:]) for p in b["polygon"] if len(p) and len(p[0]) >= 3]
    elif "footprint" in b:
        fp = b["footprint"]
        parts = [Polygon(fp["exterior"], fp.get("holes", []))] if isinstance(fp, dict) else [Polygon(fp)]
    else:
        return None
    if not parts:
        return None
    return (parts[0] if len(parts) == 1 else MultiPolygon(parts)).buffer(0)


def clean_poly(poly):
    """Simplify a little (drops collinear points) and keep only real polygon parts."""
    from shapely.geometry import MultiPolygon, Polygon
    from shapely.geometry.polygon import orient
    poly = poly.buffer(0).simplify(0.05, preserve_topology=True)
    parts = list(poly.geoms) if isinstance(poly, MultiPolygon) else [poly]
    out = []
    for p in parts:
        if not isinstance(p, Polygon) or p.area <= 1.0:
            continue
        xy = np.vstack([np.asarray(p.exterior.coords)[:-1]] + [np.asarray(r.coords)[:-1] for r in p.interiors])
        if len(np.unique(np.round(xy, 6), axis=0)) < len(xy):
            # A courtyard touching the outer wall at one corner would make a non-closed mesh;
            # shrinking 2 cm opens that corner.
            q = p.buffer(-0.02, join_style=2)
            out += [x for x in getattr(q, "geoms", [q]) if isinstance(x, Polygon) and x.area > 1.0]
        else:
            out.append(p)
    return [orient(p, 1.0) for p in out]


# ---- mesh -------------------------------------------------------------------

def extrude(poly, z0, z1, wall_mat, roof_mat):
    """Closed prism of one polygon (holes allowed). Returns verts, faces, mats, charts.

    charts: list of (face indices, local 2-D coords per face corner (k, 3, 2), (w, h)).
    """
    import shapely
    rings = [np.asarray(poly.exterior.coords)[:-1]] + [np.asarray(r.coords)[:-1] for r in poly.interiors]
    ring_xy = np.vstack(rings)
    n = len(ring_xy)
    verts = np.vstack([np.column_stack([ring_xy, np.full(n, z0)]), np.column_stack([ring_xy, np.full(n, z1)])])
    key = {(round(x, 6), round(y, 6)): i for i, (x, y) in enumerate(ring_xy)}
    tris = shapely.constrained_delaunay_triangles(poly)
    top = []
    for t in tris.geoms:
        c = np.asarray(t.exterior.coords)[:3]
        try:
            idx = [key[(round(x, 6), round(y, 6))] for x, y in c]
        except KeyError:
            raise ValueError("triangulation added a point")
        a, b, cc = ring_xy[idx]
        if (b[0] - a[0]) * (cc[1] - a[1]) - (b[1] - a[1]) * (cc[0] - a[0]) < 0:
            idx = [idx[0], idx[2], idx[1]]           # counter-clockwise seen from above
        top.append(idx)
    top = np.asarray(top, np.int64)
    faces, mats, charts = [], [], []
    minx, miny = ring_xy.min(0)
    size = tuple(ring_xy.max(0) - ring_xy.min(0))

    def add(fs, mat, local, wh):
        start = sum(len(f) for f in faces)
        faces.append(np.asarray(fs, np.int64))
        mats.append(np.full(len(fs), mat, np.uint8))
        charts.append((np.arange(start, start + len(fs)), np.asarray(local, float), wh))

    # Roof (normal up) and bottom (normal down): top-down charts.
    add(top + n, roof_mat, ring_xy[top] - [minx, miny], size)
    bot = top[:, [0, 2, 1]]
    add(bot, wall_mat, ring_xy[bot] - [minx, miny], size)
    # Walls: one quad per ring edge, its own chart (length x height).
    hgt = z1 - z0
    start = 0
    for r in rings:
        m = len(r)
        for i in range(m):
            a, b = start + i, start + (i + 1) % m
            L = float(np.linalg.norm(ring_xy[b] - ring_xy[a]))
            fs = [[a, b, b + n], [a, b + n, a + n]]
            local = [[[0, 0], [L, 0], [L, hgt]], [[0, 0], [L, hgt], [0, hgt]]]
            add(fs, wall_mat, local, (L, hgt))
        start += m
    return verts, np.vstack(faces), np.concatenate(mats), charts


def pack_tile(parts):
    """Merge building pieces of a tile and give them one shared, non-overlapping UV atlas.

    parts: list of (verts, faces, mats, charts). Returns a VC mesh dict plus atlas info.
    """
    sizes, refs = [], []
    for p, (_, _, _, charts) in enumerate(parts):
        for ch in charts:
            sizes.append(ch[2]); refs.append((p, ch))
    offsets, side = vcmesh.pack_charts(sizes, CHART_PAD)
    V, F, M, UV = [], [], [], []
    off = 0
    for p, (v, f, m, charts) in enumerate(parts):
        uv = np.zeros((len(f), 3, 2))
        V.append(v); F.append(f + off); M.append(m); UV.append(uv)
        off += len(v)
    for (p, (fi, local, wh)), o in zip(refs, offsets):
        UV[p][fi] = (local + np.asarray(o)) / side
    # Exact check: the packed rectangles must not overlap.
    rects = np.array([[o[0], o[1], o[0] + w, o[1] + h] for o, (w, h) in zip(offsets, sizes)])
    overlap = rects_overlap(rects)
    mesh = {"verts": np.vstack(V), "faces": np.vstack(F).astype(np.int32), "face_mat": np.concatenate(M),
            "uv0": np.clip(np.vstack(UV), 0, 1).astype(np.float32), "meta": {}}
    return mesh, {"atlas_side_m": round(float(side), 2), "charts": len(sizes), "chart_overlaps": int(overlap)}


def rects_overlap(r):
    """Number of overlapping rectangle pairs (sweep on x)."""
    order = np.argsort(r[:, 0])
    r = r[order]
    hits = 0
    for i in range(len(r)):
        j = i + 1
        while j < len(r) and r[j, 0] < r[i, 2] - 1e-9:
            if r[j, 1] < r[i, 3] - 1e-9 and r[i, 1] < r[j, 3] - 1e-9:
                hits += 1
            j += 1
    return hits


# ---- heights (parallel per tile) ---------------------------------------------

_W = {}


def _init(grid, work):
    _W["g"] = grid
    _W["dtm"] = np.load(Path(work) / "dtm.npy", mmap_mode="r")


def tile_heights(job):
    """LiDAR roof and ground numbers for the buildings of one tile."""
    import shapely
    from shapely import wkb
    tile, items = job
    polys = [wkb.loads(w) for _, w in items]
    b = np.array([p.bounds for p in polys])
    pts = lidar_io.load_points(b[:, 0].min(), b[:, 1].min(), b[:, 2].max(), b[:, 3].max(), classes=[1, 2, 6],
                               buffer=6.0)
    x, y, z, cls, rn = pts["x"], pts["y"], pts["z"], pts["classification"], pts["return_number"]
    out = []
    for (bid, _), poly in zip(items, polys):
        r = {"id": bid}
        bb = poly.bounds
        near = (x > bb[0] - 5) & (x < bb[2] + 5) & (y > bb[1] - 5) & (y < bb[3] + 5)
        xs, ys, zs, cs, rs = x[near], y[near], z[near], cls[near], rn[near]
        inside = shapely.contains_xy(poly, xs, ys)
        ring = shapely.contains_xy(poly.buffer(4.0), xs, ys) & ~shapely.contains_xy(poly.buffer(0.5), xs, ys)
        g = zs[ring & (cs == 2)]
        if len(g) >= 5:
            r["ground_z"], r["ground_low"], r["ground_n"] = float(np.median(g)), float(np.percentile(g, 10)), int(len(g))
        else:
            bxy = np.asarray(poly.buffer(2.0).exterior.coords)
            dz = tc.sample_bilinear(_W["dtm"], _W["g"], bxy[:, 0], bxy[:, 1])
            r["ground_z"], r["ground_low"], r["ground_n"] = float(np.median(dz)), float(np.min(dz)), 0
        roof6 = zs[inside & (cs == 6)]
        first = zs[inside & (cs == 1) & (rs == 1)]
        r["n_class6"], r["n_first"] = int(len(roof6)), int(len(first))
        if len(roof6) >= MIN_ROOF_POINTS:
            r["roof_p90"], r["roof_median"], r["roof_src"] = float(np.percentile(roof6, 90)), float(np.median(roof6)), "lidar_class6"
            r["roof_pts"] = roof6
        elif len(first) >= MIN_ROOF_POINTS:
            r["roof_p90"], r["roof_median"], r["roof_src"] = float(np.percentile(first, 90)), float(np.median(first)), "lidar_first_return"
            r["roof_pts"] = first
        out.append(r)
    for r in out:      # keep only small summaries of the roof points
        pts_ = r.pop("roof_pts", None)
        if pts_ is not None:
            top = r["roof_p90"]
            r["roof_abs_dev_median"] = float(np.median(np.abs(pts_ - top)))
    return tile, out


def choose_height(b, lid):
    """Height above ground + its source, following the order in the module help."""
    if "roof_p90" in lid:
        h = lid["roof_p90"] - lid["ground_z"]
        if h >= 2.0:
            return h, lid["roof_src"]
    if b.get("metro_height"):
        return float(b["metro_height"]), "metro_height"
    if b.get("levels"):
        return 3.0 * float(b["levels"]), "osm_levels"
    return 6.0, "default_6m"


# ---- picture ------------------------------------------------------------------

def overview(records, root, path):
    import matplotlib
    from PIL import Image, ImageDraw
    from shapely.geometry import shape
    from render_stage0 import font, hillshade
    dtm, g = tc.read_raster(tc.rasters_dir(root) / "dtm.tif")
    polys = tc.zone_polys()
    xb = polys["backdrop_ring"].bounds
    step = 2                                            # 1 m pixels from the 0.5 m grid
    r0, c0 = tc.cell_of(xb[0] - 20, xb[3] + 20, g)
    r1, c1 = tc.cell_of(xb[2] + 20, xb[1] - 20, g)
    r0, c0 = max(int(r0), 0), max(int(c0), 0)
    sub = dtm[r0:r1:step, c0:c1:step].astype(float)
    sg = {"origin_x": g["origin_x"] + c0 * g["cell_m"], "origin_y": g["origin_y"] - r0 * g["cell_m"],
          "cell_m": g["cell_m"] * step, "width": sub.shape[1], "height": sub.shape[0]}
    top = tc.rasterize([(shape(r["_geom"]), r["top_z"]) for r in records], sg, dtype=np.float32, fill=np.nan)
    hgt = tc.rasterize([(shape(r["_geom"]), r["height"]) for r in records], sg, dtype=np.float32, fill=np.nan)
    surf = np.where(np.isfinite(top), top, sub)
    shade = hillshade(surf, sg["cell_m"])
    rgb = np.repeat((0.35 + 0.45 * hillshade(sub, sg["cell_m"]))[..., None], 3, 2)
    bld = np.isfinite(hgt)
    col = matplotlib.colormaps["turbo"](np.clip(np.nan_to_num(hgt) / 40.0, 0, 1))[..., :3]
    rgb[bld] = col[bld] * (0.35 + 0.65 * shade[bld][:, None])
    img = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(img, "RGBA")
    to_px = lambda X, Y: ((X - sg["origin_x"]) / sg["cell_m"], (sg["origin_y"] - Y) / sg["cell_m"])  # noqa: E731
    for geom, colr in ((polys["backdrop_ring"], (0, 230, 255, 255)), (polys["campus"], (255, 220, 0, 255))):
        for gg in getattr(geom, "geoms", [geom]):
            for ringg in [gg.exterior, *gg.interiors]:
                d.line([to_px(x, y) for x, y in ringg.coords], fill=colr, width=3)
    f = font(max(14, img.width // 70))
    lines = [f"Backdrop ring LoD1: {len(records)} buildings, coloured by height (0-40 m, turbo)",
             "yellow: campus boundary   cyan: ring outer edge   gray: LiDAR ground hillshade",
             "OSM (ODbL), Metro Nashville footprints, USGS 3DEP LiDAR"]
    tw = max(d.textlength(s, font=f) for s in lines)
    d.rectangle([10, 10, 30 + tw, 20 + 1.3 * f.size * len(lines)], fill=(0, 0, 0, 170))
    for i, s in enumerate(lines):
        d.text((20, 15 + i * 1.3 * f.size), s, font=f, fill=(255, 255, 255, 255))
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, "PNG", optimize=True)


# ---- main ---------------------------------------------------------------------

def summary(a, target=0.5):
    a = np.abs(np.asarray(a, float))
    if not len(a):
        return {"n": 0}
    return {"n": int(len(a)), "median": round(float(np.median(a)), 3), "p95": round(float(np.percentile(a, 95)), 3),
            "max": round(float(a.max()), 3), "within_0.5m_pct": round(100 * float((a <= target).mean()), 2)}


def main(argv=None):
    import tempfile
    from shapely import wkb
    from shapely.geometry import mapping
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--jobs", type=int, default=max(1, min(12, (os.cpu_count() or 2) - 2)))
    p.add_argument("--no-manifest", action="store_true")
    args = p.parse_args(argv)
    t0 = time.time()
    root = data_root()
    out_dir = root / "derived/buildings/ring/v1"
    mats = vcmesh.material_index()

    blds, fp_source = load_footprints(root)
    if not blds:
        fail("no ring buildings found")
    for b in blds:
        c = b["poly"].centroid
        b["tile"] = fr.tile_of(c.x, c.y)
    print(f"{len(blds)} ring buildings from {fp_source}; LiDAR heights with {args.jobs} workers...", flush=True)

    by_tile = {}
    for b in blds:
        by_tile.setdefault(b["tile"], []).append((b["id"], wkb.dumps(b["poly"])))
    dtm, g = tc.read_raster(tc.rasters_dir(root) / "dtm.tif")
    with tempfile.TemporaryDirectory(prefix="vc_ring_") as work:
        np.save(Path(work) / "dtm.npy", dtm.astype(np.float32))
        del dtm
        jobs = sorted(by_tile.items())
        if args.jobs > 1:
            with Pool(args.jobs, initializer=_init, initargs=(g, work)) as pool:
                res = pool.map(tile_heights, jobs, chunksize=1)
        else:
            _init(g, work)
            res = [tile_heights(j) for j in jobs]
            _W.clear()
    lid = {r["id"]: r for _, rs in res for r in rs}

    records, tiles_parts, failures = [], {}, []
    for b in sorted(blds, key=lambda b: b["id"]):
        L = lid[b["id"]]
        h, src = choose_height(b, L)
        ground, top = L["ground_z"], L["ground_z"] + h
        base = L["ground_low"] - BASE_DROP
        wall = "brick_buff" if h < TALL else "concrete"
        parts = clean_poly(b["poly"])
        pieces = []
        try:
            for part in parts:
                pieces.append(extrude(part, base, top, mats[wall], mats["roof_flat"]))
        except ValueError as e:
            failures.append({"id": b["id"], "error": str(e)})
            continue
        if not pieces:
            failures.append({"id": b["id"], "error": "empty footprint after cleaning"})
            continue
        # Watertight check for every part (parts of one building may touch, so each is checked alone).
        probs = sorted({pr for v, f, m, _ in pieces for pr in vcmesh.check_mesh(
            {"verts": v, "faces": f, "face_mat": m, "uv0": np.zeros((len(f), 3, 2))}, need_watertight=True)})
        if probs:
            failures.append({"id": b["id"], "error": ", ".join(probs)})
            continue
        tiles_parts.setdefault(b["tile"], []).extend(pieces)
        geom = mapping(parts[0] if len(parts) == 1 else b["poly"].simplify(0.05))
        rec = {"id": b["id"], "osm": b["osm"], "tile": b["tile"], "height": round(h, 2), "source": src,
               "ground_z": round(ground, 2), "base_z": round(base, 2), "top_z": round(top, 2),
               "wall_material": wall, "wall_material_guess": True, "roof_material": "roof_flat",
               "footprint_source": b["footprint_source"], "parts": len(parts),
               "metro_height": round(b["metro_height"], 2) if b.get("metro_height") else None,
               "levels": b.get("levels"), "lidar_class6_points": L["n_class6"],
               "lidar_first_return_points": L["n_first"], "ground_points": L["ground_n"],
               "footprint": {"exterior": [[round(x, 2), round(y, 2)] for x, y in
                                          np.asarray(parts[0].exterior.coords)],
                             "holes": [[[round(x, 2), round(y, 2)] for x, y in np.asarray(r.coords)]
                                       for r in parts[0].interiors],
                             "extra_parts": len(parts) - 1}}
        if b.get("step_top_z") is not None:
            rec["buildings_step_top_z"] = round(float(b["step_top_z"]), 2)
        if "roof_median" in L:
            rec["lidar_roof_median_z"] = round(L["roof_median"], 2)
            rec["lidar_roof_abs_dev_median"] = round(L["roof_abs_dev_median"], 2)
        records.append({**rec, "_geom": geom})

    # One merged mesh per tile with its own atlas.
    out_dir.mkdir(parents=True, exist_ok=True)
    for f in out_dir.glob("T_*.npz"):
        if f.stem not in tiles_parts:
            f.unlink()
    files, tile_info = [], {}
    for tile, parts in sorted(tiles_parts.items()):
        mesh, info = pack_tile(parts)
        probs = vcmesh.check_mesh(mesh)
        if probs or info["chart_overlaps"]:
            failures.append({"tile": tile, "error": f"{probs} overlaps={info['chart_overlaps']}"})
        ids = [r["id"] for r in records if r["tile"] == tile]
        path = out_dir / f"{tile}.npz"
        vcmesh.save_mesh(path, mesh["verts"], mesh["faces"], mesh["face_mat"], mesh["uv0"],
                         {"tile": tile, "lod": "LoD1", "building_ids": ids, "uv_meters_per_unit": info["atlas_side_m"],
                          "uv_note": "one tile atlas, every face its own chart, no overlaps",
                          "frame": "campus meters, Z up", "source": "ring_lod1.py"})
        tile_info[tile] = {"buildings": len(ids), "triangles": int(len(mesh["faces"])), **info}
        files.append(path)

    # Independent LiDAR height: 90th percentile of the Stage 0 DSM cells (0.5 m, highest point per
    # cell) inside the footprint shrunk by 0.5 m. Area-weighted, so it does not favour dense spots.
    from shapely.geometry import shape
    dsm, dg = tc.read_raster(tc.rasters_dir(root) / "dsm.tif")
    lab = tc.rasterize([(shape(r["_geom"]).buffer(-0.5), i + 1) for i, r in enumerate(records)], dg, dtype=np.int32)
    li = lab.ravel()
    sel = np.nonzero(li)[0]
    vals, ids = dsm.ravel()[sel], li[sel]
    o = np.argsort(ids, kind="stable")
    vals, ids = vals[o], ids[o]
    starts = np.searchsorted(ids, np.arange(1, len(records) + 2))
    for i, r in enumerate(records):
        v = vals[starts[i]:starts[i + 1]]
        if len(v) >= 4:
            r["lidar_dsm_p90_z"] = round(float(np.percentile(v, 90)), 2)
    del dsm, lab
    clean = [{k: v for k, v in r.items() if k != "_geom"} for r in records]
    hist = {}
    for r in clean:
        hist[r["source"]] = hist.get(r["source"], 0) + 1
    save_json(out_dir / "ring.json", {
        "version": 1, "made": datetime.datetime.now().isoformat(timespec="seconds"),
        "footprints_from": fp_source, "frame": "campus meters (config/frame.json)",
        "height_rule": "height above ground_z; top_z = ground_z + height; mesh from base_z (lowest ground - 0.5 m)",
        "wall_rule": f"brick_buff below {TALL} m, concrete above (guess)", "height_sources": hist,
        "buildings": clean})
    files.append(out_dir / "ring.json")

    # Height check: flat LoD1 top vs the LiDAR roof (median roof point), and vs Metro height.
    lid_recs = [r for r in clean if r["source"].startswith("lidar")]
    d_dsm = [r["top_z"] - r["lidar_dsm_p90_z"] for r in lid_recs if "lidar_dsm_p90_z" in r]
    d_dsm_all = [r["top_z"] - r["lidar_dsm_p90_z"] for r in clean if "lidar_dsm_p90_z" in r]
    d_med = [r["top_z"] - r["lidar_roof_median_z"] for r in lid_recs]
    d_metro = [r["height"] - r["metro_height"] for r in lid_recs if r["metro_height"]]
    fallback_vs_lidar = [r["top_z"] - r["lidar_roof_median_z"] for r in clean
                         if not r["source"].startswith("lidar") and "lidar_roof_median_z" in r]
    over = [{"id": r["id"], "top_minus_dsm_p90": round(r["top_z"] - r["lidar_dsm_p90_z"], 2), "height": r["height"],
             "source": r["source"]} for r in clean if "lidar_dsm_p90_z" in r and abs(r["top_z"] - r["lidar_dsm_p90_z"]) > 0.5]
    report = {
        "made": datetime.datetime.now().isoformat(timespec="seconds"),
        "buildings": len(clean), "tiles": len(tile_info), "failures": failures,
        "height_sources": hist,
        "triangles_total": int(sum(t["triangles"] for t in tile_info.values())),
        "check": {
            "note": "LoD1 top = 90th percentile of roof points, so it sits above most of a pitched roof. "
                    "'top_vs_lidar_roof_median' is the flat top minus the median roof point (target 0.5 m for LoD1).",
            "top_vs_lidar_dsm_p90 (main check, LiDAR-height buildings)": summary(d_dsm),
            "top_vs_lidar_dsm_p90_all_buildings": summary(d_dsm_all),
            "top_vs_lidar_roof_median": summary(d_med),
            "roof_points_abs_dev_from_top_median": summary([r["lidar_roof_abs_dev_median"] for r in lid_recs]),
            "lidar_height_vs_metro_height": summary(d_metro),
            "fallback_top_vs_lidar_roof_median": summary(fallback_vs_lidar),
            "top_vs_buildings_step_top_z": summary([r["top_z"] - r["buildings_step_top_z"] for r in clean
                                                    if "buildings_step_top_z" in r]),
            "over_0.5m": over,
        },
        "per_tile": tile_info,
    }
    rp = root / "reports/stage1/ring_lod1.json"
    save_json(rp, report)
    files.append(rp)
    png = root / "reports/stage1/ring_overview.png"
    overview(records, root, png)
    files.append(png)
    if not args.no_manifest:
        db = manifest.connect(root)
        for f in files:
            manifest.add_file(f, "ring_lod1.py (OSM, Metro footprints, derived/lidar/v1)", LICENSE, 1, root=root, db=db)
        db.commit(); db.close()
    print(f"{len(clean)} buildings in {len(tile_info)} tiles, {report['triangles_total']:,} triangles; "
          f"sources {hist}; top vs DSM p90 {summary(d_dsm)}; vs roof median {summary(d_med)}; "
          f"{len(failures)} failures [{time.time() - t0:.0f} s]")
    if failures:
        print("failures:", failures[:10])
        sys.exit(1)


if __name__ == "__main__":
    main()
