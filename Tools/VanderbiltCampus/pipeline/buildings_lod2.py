"""Stage 1, buildings step 2: LoD2 (and LoD1) models of every campus building.

Needs derived/buildings/v1/footprints.json (buildings_footprints.py).
For every building whose footprint centroid is inside the campus polygon:
  1. roof_fit.fit_roof: roof planes and the 2.5D partition of the footprint
  2. parts: cells joined at equal heights (ridges, hips) form one part; height steps
     separate parts (wings, towers, penthouses, roof volumes)
  3. roof_mesh.build_solid: watertight LoD2 mesh (roofs, step walls, outer walls down to
     the lowest surrounding ground - 0.5 m, base cap), UV0 packed
  4. LoD1: footprint extruded to the area-weighted mean roof height
  5. colliders: convex pieces of each part (<= 0.3 m simplification), each a convex hull of
     its base and its roof surface (so gables/hips get a roof wedge)
  6. ledger (merged features under 0.5 m, footprint jogs), roof_planes.json, model.json

Materials: walls brick_red (a guess, "wall_material_guess": true); flat roofs roof_flat;
pitched roofs roof_slate, or roof_copper when the Metro 2026 orthophoto mean color over the
pitched roof is clearly green (G > R + 8 and G >= B - 5 and saturation > 0.10), or roof_tile
when clearly orange-red (R > G + 25, R > B + 35, saturation > 0.30). Base cap concrete.

Writes buildings/<id>/v1/{lod2.npz, lod1.npz, col_<n>.npz, roof_planes.json, ledger.json,
model.json} in the archive (recorded in the manifest) and a git mirror of model.json and
ledger.json in Tools/VanderbiltCampus/buildings/<id>/.
A finished building (model.json present) is skipped unless --force.

  python pipeline/buildings_lod2.py --jobs 14 [--force] [--only B_544397715 ...]
"""
import argparse
import colorsys
import math
import sys
import time
import traceback
from multiprocessing import Pool
from pathlib import Path

import numpy as np
import shapely
from shapely.geometry import Polygon
from shapely.strtree import STRtree

sys.path.insert(0, str(Path(__file__).resolve().parent))
import buildings_common as bc  # noqa: E402
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
import roof_fit as rf  # noqa: E402
import roof_mesh as rm  # noqa: E402
import vcmesh  # noqa: E402
from common import data_root, fail, load_json  # noqa: E402

MAT = None


def mats():
    global MAT
    if MAT is None:
        MAT = vcmesh.material_index()
    return MAT


# ---- parts --------------------------------------------------------------------

def find_parts(cells, planes):
    """Group cells into parts: neighbors whose roofs meet at the same height (within 0.15 m)."""
    polys = [c for c, _ in cells]
    n = len(polys)
    parent = list(range(n))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    tree = STRtree(polys)
    for i, p in enumerate(polys):
        for j in tree.query(p, predicate="intersects"):
            j = int(j)
            if j <= i:
                continue
            shared = p.boundary.intersection(polys[j].boundary)
            if shared.length < 0.05:
                continue
            xy = []
            for g in getattr(shared, "geoms", [shared]):
                if g.geom_type in ("LineString", "LinearRing"):
                    xy += list(g.coords)
            if not xy:
                continue
            xy = np.asarray(xy)
            pi_, pj_ = planes[cells[i][1]], planes[cells[j][1]]
            # unclipped planes: two roof planes of one part meet exactly on their join line
            zi = pi_.a * xy[:, 0] + pi_.b * xy[:, 1] + pi_.c
            zj = pj_.a * xy[:, 0] + pj_.b * xy[:, 1] + pj_.c
            if np.max(np.abs(zi - zj)) < 0.15:
                parent[find(i)] = find(j)
    groups = {}
    for i in range(n):
        groups.setdefault(find(i), []).append(i)
    return sorted(groups.values(), key=lambda g: -sum(polys[k].area for k in g))


def part_heights(cells, planes, members, base_z, outline=None):
    """eave = lowest roof height where the part meets the footprint outline (the part's own
    outline if it does not touch the footprint, e.g. a penthouse); ridge = highest roof vertex."""
    poly = shapely.unary_union([cells[k][0] for k in members])
    ext = bc.corners(poly)
    if outline is not None and len(ext):
        on = shapely.distance(shapely.points(ext), outline) < 0.01
        if on.any():
            ext = ext[on]
    zs_out = []
    for x, y in ext:
        best = None
        for k in members:
            c, l = cells[k]
            if c.distance(shapely.Point(x, y)) < 1e-6:
                z = float(planes[l].z(x, y))
                best = z if best is None else min(best, z)
        if best is not None:
            zs_out.append(best)
    allz = []
    for k in members:
        c, l = cells[k]
        xy = bc.corners(c)
        allz.append(planes[l].z(xy[:, 0], xy[:, 1]))
    allz = np.concatenate(allz)
    eave = max(min(zs_out) if zs_out else float(allz.min()), base_z + 0.3)
    return poly, float(eave), float(max(allz.max(), base_z + 0.3))


# ---- roof color from the orthophoto ------------------------------------------------

def roof_material(cells, planes, root):
    """roof_slate / roof_copper / roof_tile for the pitched cells (see module help)."""
    pitched = [c for c, l in cells if planes[l].slope_deg >= rf.FLAT_DEG]
    if not pitched:
        return "roof_slate", None
    g = shapely.unary_union(pitched).buffer(-0.5)
    if g.is_empty:
        return "roof_slate", None
    x0, y0, x1, y1 = g.bounds
    try:
        rgb, (ox, oy, cell) = bc.ortho_window("metro2026_campus_0.15m", x0, y0, x1, y1, root)
    except Exception as ex:  # ortho missing: keep the default
        return "roof_slate", {"error": repr(ex)}
    if rgb is None:
        return "roof_slate", None
    h, w, _ = rgb.shape
    cx = ox + (np.arange(w) + 0.5) * cell
    cy = oy - (np.arange(h) + 0.5) * cell
    X, Y = np.meshgrid(cx, cy)
    inside = shapely.contains_xy(g, X, Y) & (rgb.sum(axis=2) > 0)
    if inside.sum() < 20:
        return "roof_slate", None
    r, gg, b = (float(rgb[..., k][inside].mean()) for k in range(3))
    _, s, v = colorsys.rgb_to_hsv(r / 255, gg / 255, b / 255)
    info = {"mean_rgb": [round(r), round(gg), round(b)], "saturation": round(s, 3), "value": round(v, 3)}
    if gg > r + 8 and gg >= b - 5 and s > 0.10:
        return "roof_copper", info
    if r > gg + 25 and r > b + 35 and s > 0.30:
        return "roof_tile", info
    return "roof_slate", info


# ---- one building -----------------------------------------------------------------

def flat_cells(footprint, z):
    pl = rf.Plane(0.0, 0.0, z, kind="fallback")
    return [(p, 0) for p in bc.polys_of(footprint)], [pl]


def fallback_height(rec, pts_top):
    """Height for a building without usable roof points."""
    g = rec["ground"]["ground_z"] or 0.0
    if pts_top is not None:
        return pts_top, "robust top (90th percentile) of the roof points"
    if rec.get("metro_height_m"):
        return g + rec["metro_height_m"], "Metro Height attribute"
    t = rec.get("osm_tags", {})
    try:
        if "height" in t:
            return g + float(str(t["height"]).split()[0]), "OSM height tag"
        if "building:levels" in t:
            return g + 3.5 * float(t["building:levels"]), "OSM building:levels x 3.5 m"
    except ValueError:
        pass
    return g + 8.0, "default 8 m (no LiDAR roof, no Metro or OSM height)"


MIN_EXTENT, MIN_VOLUME = 0.3, 0.1      # every collider part must be a real solid (PhysX)


def hull_solid(q, cells, planes, base_z):
    """Convex hull of a 2D piece from base_z up to the model roof over it, plus its deviation
    from the model roof (max |hull top - roof| on a 1 m grid). Returns (trimesh hull, dev) or
    (None, None)."""
    top = []
    for c, l in cells:
        if not c.intersects(q):
            continue
        inter = c.intersection(q)
        for g in bc.polys_of(inter):
            xy = bc.corners(g)
            top.append(np.c_[xy, planes[l].z(xy[:, 0], xy[:, 1])])
    if not top:
        return None, None
    try:
        hull = clean_hull(rm.prism_hull(q, base_z, np.vstack(top)))
    except Exception:
        return None, None
    x0, y0, x1, y1 = q.bounds
    gx, gy = np.meshgrid(np.arange(x0 + 0.25, x1, 1.0), np.arange(y0 + 0.25, y1, 1.0))
    gx, gy = gx.ravel(), gy.ravel()
    ins = shapely.contains_xy(q, gx, gy)
    dev = 0.0
    if ins.any():
        gx, gy = gx[ins], gy[ins]
        zm = rf.model_z(cells, planes, gx, gy)
        n = hull.face_normals
        up = n[:, 2] > 0.1                     # roof-like faces (walls are vertical)
        if up.any():
            o = hull.triangles[up][:, 0]
            nn = n[up]
            # top of a convex solid = lowest of its upward face planes
            zt = np.min(o[:, 2][None, :] - (nn[:, 0][None, :] * (gx[:, None] - o[:, 0][None, :])
                                            + nn[:, 1][None, :] * (gy[:, None] - o[:, 1][None, :]))
                        / nn[:, 2][None, :], axis=1)
            d = np.abs(zt - zm)
            d = d[np.isfinite(d)]
            dev = float(d.max()) if len(d) else 0.0
    return hull, dev


def solid_ok(hull):
    """Volume >= 0.1 m3 and the oriented box at least 0.3 m in all three directions."""
    if hull is None or len(hull.vertices) < 4:
        return False
    try:
        from scipy.spatial import ConvexHull
        vol = ConvexHull(np.asarray(hull.vertices)).volume
        ext = hull.bounding_box_oriented.primitive.extents
    except Exception:
        return False
    return vol >= MIN_VOLUME and float(np.min(ext)) >= MIN_EXTENT


def colliders(cells, planes, parts, base_z, tol=0.3, target=12):
    """Convex collider parts for one building.

    1. Each roof part's outline (simplified by tol; by 0.6 m or 1.0 m when the building would
       otherwise need more than target pieces - recorded as simplify_m) is cut into convex pieces.
    2. Neighbor pieces (also across parts) are merged while the merged convex hull stays within
       tol of the true shape: plan outline (Hausdorff distance) and roof height (1 m grid).
       Smallest pieces are tried first; merging stops at the target count or when nothing fits.
    3. Every piece must be a real solid (volume >= 0.1 m3, >= 0.3 m thick in all directions);
       a sliver is merged into the neighbor it touches most, or dropped if under 1 m2.
    Returns (list of trimesh hulls, info)."""
    # coarser outline simplification only when the 0.3 m version needs more than the target
    for t in (tol, 0.6, 1.0):
        pieces = []
        for members in parts:
            poly = shapely.unary_union([cells[k][0] for k in members])
            for p in bc.polys_of(poly):
                pieces += [q for q in rm.convex_pieces(p, t) if q.area > 1e-4]
        if len(pieces) <= target:
            break
    tol_used = t
    before = len(pieces)
    items = []
    for q in pieces:
        h, d = hull_solid(q, cells, planes, base_z)
        items.append([q, h, d])
    merged_over_tol, dropped_area = 0, 0.0
    for _ in range(6):                      # merge passes
        alive = [True] * len(items)
        changed = False
        order = sorted(range(len(items)), key=lambda i: items[i][0].area)
        for i in order:
            if not alive[i]:
                continue
            qi = items[i][0]
            bi = qi.bounds
            best = None
            for j in range(len(items)):
                if j == i or not alive[j]:
                    continue
                bj = items[j][0].bounds
                if bj[0] > bi[2] + 0.01 or bj[2] < bi[0] - 0.01 or bj[1] > bi[3] + 0.01 or bj[3] < bi[1] - 0.01:
                    continue
                if qi.intersection(items[j][0].buffer(1e-6)).length < 0.05:
                    continue
                u = shapely.unary_union([qi, items[j][0]])
                hull2d = u.convex_hull
                if hull2d.hausdorff_distance(u) > tol:
                    continue
                h, d = hull_solid(hull2d, cells, planes, base_z)
                if h is None or d > tol:
                    continue
                if best is None or d < best[2]:
                    best = (j, hull2d, d, h)
            if best is not None:
                j, hull2d, d, h = best
                items[j] = [hull2d, h, d]
                alive[i] = False
                changed = True
        items = [it for it, a in zip(items, alive) if a]
        if not changed or len(items) <= 1:
            break
    # slivers
    k = 0
    while k < len(items):
        q, h, d = items[k]
        if solid_ok(h):
            k += 1
            continue
        nb = [(q.intersection(items[j][0].buffer(1e-6)).length, j) for j in range(len(items)) if j != k]
        nb = [t for t in nb if t[0] > 0.05]
        if nb:
            _, j = max(nb)
            u = shapely.unary_union([q, items[j][0]]).convex_hull
            h2, d2 = hull_solid(u, cells, planes, base_z)
            if solid_ok(h2):
                items[j] = [u, h2, d2]
                items.pop(k)
                merged_over_tol += int(d2 > tol)
                continue
        dropped_area += q.area
        items.pop(k)
    hulls = [h for _, h, _ in items]
    devs = [d for _, _, d in items if d is not None]
    cover = shapely.unary_union([q for q, _, _ in items]) if items else shapely.Polygon()
    fp = shapely.unary_union([c for c, _ in cells])
    return hulls, {"count": len(hulls), "pieces_before_merging": before, "simplify_m": tol_used,
                   "roof_max_dev_m": round(max(devs), 3) if devs else None,
                   "sliver_merges_over_tolerance": merged_over_tol,
                   "slivers_dropped_m2": round(dropped_area, 3),
                   "footprint_uncovered_m2": round(fp.difference(cover).area, 3),
                   "footprint_extra_m2": round(cover.difference(fp).area, 3)}


def solid(cells, base_z):
    """build_solid with walls brick_red and base concrete; if the result is not watertight
    (nearly coincident vertices), retry with a coarser vertex snap (1 cm, then 5 cm)."""
    M = mats()
    for snap in (0.001, 0.01, 0.05):
        m = rm.build_solid(cells, base_z, M["brick_red"], M["concrete"], snap=snap)
        m["meta"]["snap_m"] = snap
        if not vcmesh.check_mesh(m, need_watertight=True):
            return m
    return m


def clean_hull(h):
    """Convex hull rebuilt from its vertices rounded to 1 mm, without degenerate or duplicate faces."""
    import trimesh
    v = np.unique(np.round(np.asarray(h.vertices), 3), axis=0)
    try:
        t = trimesh.convex.convex_hull(v)
    except Exception:
        return h
    t.update_faces(t.nondegenerate_faces(height=1e-6))
    t.update_faces(t.unique_faces())
    t.remove_unreferenced_vertices()
    return t if t.is_watertight and len(t.faces) >= 4 else h


def clean_mesh(m):
    """Drop zero-area and duplicate triangles when the mesh stays watertight without them."""
    v, f = m["verts"], m["faces"]
    area = np.linalg.norm(np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]]), axis=1) / 2
    _, first = np.unique(np.sort(f, axis=1), axis=0, return_index=True)
    keep = np.zeros(len(f), bool)
    keep[first] = True
    keep &= area > 1e-8
    if keep.all():
        return m, 0
    trial = {**m, "faces": f[keep], "face_mat": m["face_mat"][keep], "uv0": m["uv0"][keep]}
    if not vcmesh.check_mesh(trial, need_watertight=True):
        return trial, int((~keep).sum())
    return m, 0


def mesh_dict_from_trimesh(t, mat):
    f = np.asarray(t.faces)
    return {"verts": np.asarray(t.vertices), "faces": f, "face_mat": np.full(len(f), mat, np.uint8),
            "uv0": np.zeros((len(f), 3, 2), np.float32)}


def model_building(rec, pts, others, root):
    """Fit, mesh and describe one building. Returns (model, ledger, planes_json, meshes)."""
    M = mats()
    footprint = bc.geom_from_json(rec["polygon"])
    ground_z = rec["ground"]["ground_z"]
    base_z = rec["ground"]["ground_min"]
    flags = list(rec["flags"])
    if ground_z is None:
        ground_z = float(np.nanmedian(bc.dtm().sample(*bc.corners(footprint).T)))
        base_z = ground_z
        flags.append("ground_from_dtm")
    base_z = float(base_z) - 0.5
    fit = rf.fit_roof(pts, footprint, others, ground_z, base_z)
    fallback = fit["fallback"]
    if fallback is None and footprint.geom_type != "Polygon":
        # multi-polygon footprints: fit_roof handles them as one partition already
        pass
    if fallback:
        P = fit["P"]
        top = float(np.percentile(P[:, 2], 90)) if len(P) >= 12 else None
        z, how = fallback_height(rec, top)
        cells, planes = flat_cells(footprint, z)
        flags.append("fallback_flat")
        fallback = f"{fallback}; flat roof at {how}"
        equipment, info = [], fit["info"]
    else:
        cells, planes = fit["cells"], fit["planes"]
        equipment, info = fit["equipment"], fit["info"]
    if info.get("curved"):
        flags.append("curved")
    # renumber planes to the used ones
    used = sorted({l for _, l in cells})
    remap = {o: i for i, o in enumerate(used)}
    planes = [planes[o] for o in used]
    cells = [(c, remap[l]) for c, l in cells]
    for k, pl in enumerate(planes):
        pl.area = sum(c.area for c, l in cells if l == k)
    parts = find_parts(cells, planes)
    roof_mat, color = roof_material(cells, planes, root)
    cell_mat = [M["roof_flat"] if planes[l].slope_deg < rf.FLAT_DEG else M[roof_mat] for _, l in cells]
    cd = [{"poly": c, "z": planes[l].z, "mat": m} for (c, l), m in zip(cells, cell_mat)]
    lod2 = solid(cd, base_z)
    # LoD1: area-weighted mean roof height
    tot = sum(c.area for c, _ in cells)
    mean_z = sum(planes[l].z(c.centroid.x, c.centroid.y) * c.area for c, l in cells) / tot
    l1cells = [{"poly": p, "z": (lambda x, y, z=mean_z: np.full(np.shape(x), z)), "mat": M["roof_flat"]}
               for p in bc.polys_of(footprint)]
    lod1 = solid(l1cells, base_z)
    hulls, colinfo = colliders(cells, planes, parts, base_z)
    # parts summary
    part_rows = []
    curved_planes = {k for k, pl in enumerate(planes) if pl.kind == "loose"}
    for pi, members in enumerate(parts):
        poly, eave, ridge = part_heights(cells, planes, members, base_z, footprint.boundary)
        ls = sorted({cells[k][1] for k in members})
        areas = [sum(cells[k][0].area for k in members if cells[k][1] == l) for l in ls]
        curved = sum(a for l, a in zip(ls, areas) if l in curved_planes) > 0.3 * sum(areas)
        rtype = "flat" if fallback else rf.classify_part([planes[l] for l in ls], areas, curved)
        if any(planes[l].kind == "volume" for l in ls) and len(ls) == 1:
            rtype = "roof_volume"
        part_rows.append({"id": f"P{pi + 1}", "area_m2": round(poly.area, 2), "roof_type": rtype,
                          "planes": ls, "ground_z": round(ground_z, 3), "base_z": round(base_z, 3),
                          "eave_z": round(eave, 3), "ridge_z": round(ridge, 3),
                          "eave_height_m": round(eave - ground_z, 3), "ridge_height_m": round(ridge - ground_z, 3),
                          "polygon": bc.geom_to_json(poly)})
    main = max(part_rows, key=lambda p: p["area_m2"]) if part_rows else None
    btype = main["roof_type"] if main else "flat"
    if fallback:
        btype = "fallback_flat"
    elif len(part_rows) > 1 and all(p["roof_type"] in ("flat", "roof_volume") for p in part_rows):
        btype = "flat"
    # ledger
    entries = []
    for e in equipment:
        entries.append({"type": e["type"], "merged_into": "roof", "center": e["center"], "size": e["size"],
                        "count": 1, "material": "roof_flat" if e["type"] != "chimney_or_vent" else "brick_red",
                        "photos": [], "restore_hint": e["restore_hint"], "points": e["points"]})
    for j in rec.get("removed_jogs", []):
        entries.append({"type": "footprint_jog", "merged_into": "wall",
                        "center": [j["xy"][0], j["xy"][1], round(ground_z, 2)],
                        "size": [round(j["deviation_m"], 3), round(j["deviation_m"], 3), 0.0], "count": 1,
                        "material": "brick_red", "photos": [], "restore_hint": "geometry"})
    for k, e in enumerate(entries, 1):
        e["id"] = f"L{k:04d}"
    ledger = {"building": rec["id"], "version": 1, "lod": "LoD2", "threshold_m": rf.MERGE_MIN,
              "entries": [{"id": e["id"], **{k: v for k, v in e.items() if k != "id"}} for e in entries]}
    planes_json = {"building": rec["id"], "version": 1, "frame": "campus meters; z = a*x + b*y + c",
                   "fit_info": info, "fallback": fallback,
                   "planes": [pl.to_json() for pl in planes],
                   "cells": [{"plane": l, "polygon": bc.geom_to_json(c), "material":
                              ("roof_flat" if planes[l].slope_deg < rf.FLAT_DEG else roof_mat)} for c, l in cells],
                   "lines": [{"kind": k, "from": [round(float(a[0]), 3), round(float(a[1]), 3)],
                              "to": [round(float(b[0]), 3), round(float(b[1]), 3)]}
                             for a, b, k in (fit.get("lines") or [])] if not fallback else []}
    used_mats = sorted({"brick_red", "concrete"} | {("roof_flat" if planes[l].slope_deg < rf.FLAT_DEG else roof_mat)
                                                     for _, l in cells})
    meshes = {"lod2": lod2, "lod1": lod1, "colliders": hulls}
    tri = {"lod2": int(len(lod2["faces"])), "lod1": int(len(lod1["faces"])),
           "colliders": int(sum(len(h.faces) for h in hulls))}
    cen = footprint.centroid
    model = {
        "building": rec["id"], "version": 1, "name": rec.get("name"), "osm": rec.get("osm"),
        "metro_ids": rec.get("metro_ids", []), "tile": fr.tile_of(cen.x, cen.y), "zone": rec["zone"],
        "style_kit": None, "lod_reached": "LoD2",
        "footprint": {"source": rec["source"], "decisions": rec["decisions"], "area_m2": rec["area_m2"],
                      "corners": int(len(bc.corners(footprint))), "overhang_m": rec["overhang_m"],
                      "corner_errors": rec["corner_errors"]},
        "ground_z": round(ground_z, 3), "base_z": round(base_z, 3),
        "roof_type": btype, "metro_roof_type": rec.get("metro_roof_type"),
        "parts": part_rows,
        "roof_fit": {"points": int(len(fit["P"])), "density_pts_m2": round(fit["density"], 2),
                     "coverage": info.get("coverage"), "planes": len(planes), "fallback": fallback,
                     "roof_color": color, "equipment_volumes": len(fit.get("volumes") or [])},
        "materials": used_mats, "wall_material_guess": True,
        "material_rule": "walls brick_red (guess); flat roofs roof_flat; pitched roof_slate unless the Metro 2026 "
                         "ortho mean color is clearly green (roof_copper) or orange-red (roof_tile); base concrete",
        "flags": sorted(set(flags)),
        "uv_m_per_unit": {"lod2": round(lod2["meta"]["uv_m_per_unit"], 4),
                          "lod1": round(lod1["meta"]["uv_m_per_unit"], 4)},
        "triangles": tri, "colliders": colinfo,
        "ledger_entries": len(entries),
        "files": {"lod2": "lod2.npz", "lod1": "lod1.npz",
                  "colliders": [f"col_{k}.npz" for k in range(len(hulls))],
                  "roof_planes": "roof_planes.json", "ledger": "ledger.json"},
        "accuracy": None,
    }
    return model, ledger, planes_json, meshes


def write_building(rec, model, ledger, planes_json, meshes, root):
    """Save one building's files; return the archive paths written."""
    M = mats()
    d = bc.building_dir(rec["id"], root)
    d.mkdir(parents=True, exist_ok=True)
    for old in d.glob("col_*.npz"):
        old.unlink()
    files = []
    for name in ("lod2", "lod1"):
        m, dropped = clean_mesh(meshes[name])
        meshes[name] = m
        meta = {"building": rec["id"], "lod": name.upper().replace("LOD", "LoD"), "frame": "campus meters, Z up",
                "uv_m_per_unit": m["meta"]["uv_m_per_unit"], "charts": m["charts"],
                "script": "buildings_lod2.py", "version": 1}
        vcmesh.save_mesh(d / f"{name}.npz", m["verts"], m["faces"], m["face_mat"], m["uv0"], meta)
        files.append(d / f"{name}.npz")
    for k, h in enumerate(meshes["colliders"]):
        md = mesh_dict_from_trimesh(clean_hull(h), M["concrete"])
        vcmesh.save_mesh(d / f"col_{k}.npz", md["verts"], md["faces"], md["face_mat"], md["uv0"],
                         {"building": rec["id"], "kind": "convex collider", "uv0": "unused (zeros)", "version": 1})
        files.append(d / f"col_{k}.npz")
    files.append(bc.save(d / "roof_planes.json", planes_json))
    files.append(bc.save(d / "ledger.json", ledger))
    files.append(bc.save(d / "model.json", model))
    g = bc.git_building_dir(rec["id"])
    bc.save(g / "model.json", model)
    bc.save(g / "ledger.json", ledger)
    return files


def run_job(job):
    tile, recs, neighbors, force, root = job
    out = []
    todo = []
    for rec in recs:
        if not force and (bc.building_dir(rec["id"], root) / "model.json").exists():
            out.append({"id": rec["id"], "status": "skipped (finished; use --force)", "files": []})
        else:
            todo.append(rec)
    if not todo:
        return out
    gs = [bc.geom_from_json(r["polygon"]) for r in todo]
    b = np.array([g.bounds for g in gs])
    pts = lidar_io.load_points(b[:, 0].min() - 6, b[:, 1].min() - 6, b[:, 2].max() + 6, b[:, 3].max() + 6,
                               root=root)
    for rec, g in zip(todo, gs):
        t0 = time.time()
        try:
            others = [ng for nid, ng in neighbors if nid != rec["id"] and ng.intersects(g.buffer(8))]
            others = shapely.unary_union(others) if others else None
            model, ledger, planes_json, meshes = model_building(rec, pts, others, root)
            for name in ("lod2", "lod1"):
                meshes[name], _ = clean_mesh(meshes[name])
            meshes["colliders"] = [clean_hull(h) for h in meshes["colliders"]]
            model["triangles"] = {"lod2": int(len(meshes["lod2"]["faces"])), "lod1": int(len(meshes["lod1"]["faces"])),
                                  "colliders": int(sum(len(h.faces) for h in meshes["colliders"]))}
            probs2 = vcmesh.check_mesh(meshes["lod2"], need_watertight=True)
            probs1 = vcmesh.check_mesh(meshes["lod1"], need_watertight=True)
            model["mesh_checks"] = {"lod2": probs2, "lod1": probs1}
            if probs2:
                model["flags"] = sorted(set(model["flags"]) | {"mesh_problem"})
            model["seconds"] = round(time.time() - t0, 2)
            files = write_building(rec, model, ledger, planes_json, meshes, root)
            out.append({"id": rec["id"], "status": "ok", "files": [str(f) for f in files],
                        "problems": probs2 + probs1})
        except Exception as ex:
            out.append({"id": rec["id"], "status": "error", "error": repr(ex),
                        "trace": traceback.format_exc(), "files": []})
    return out


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--jobs", type=int, default=14)
    p.add_argument("--force", action="store_true", help="redo finished buildings")
    p.add_argument("--only", nargs="*", help="only these building ids")
    p.add_argument("--zone", default="campus", choices=["campus", "ring", "all"],
                   help="which buildings get LoD2 (default campus; the ring gets LoD1 boxes elsewhere)")
    args = p.parse_args(argv)
    t0 = time.time()
    root = data_root()
    fpath = bc.out_dir(root) / "footprints.json"
    if not fpath.exists():
        fail(f"{fpath} is missing; run buildings_footprints.py first")
    allrec = load_json(fpath)["buildings"]
    recs = [r for r in allrec if args.zone == "all" or r["zone"] == args.zone]
    if args.only:
        recs = [r for r in recs if r["id"] in set(args.only)]
    allg = [(r["id"], bc.geom_from_json(r["polygon"])) for r in allrec]
    tree = STRtree([g for _, g in allg])
    by_tile = {}
    for r in recs:
        by_tile.setdefault(r["tile"], []).append(r)
    jobs = []
    for tile, rs in sorted(by_tile.items()):
        env = shapely.box(*np.array([bc.geom_from_json(r["polygon"]).bounds for r in rs]).min(axis=0)[:2] - 10,
                        *np.array([bc.geom_from_json(r["polygon"]).bounds for r in rs]).max(axis=0)[2:] + 10)
        nb = [allg[int(k)] for k in tree.query(env)]
        # big tiles first so the pool stays busy
        jobs.append((tile, rs, nb, args.force, root))
    jobs.sort(key=lambda j: -sum(r["area_m2"] for r in j[1]))
    results = []
    if args.jobs > 1 and len(jobs) > 1:
        with Pool(args.jobs) as pool:
            for out in pool.imap_unordered(run_job, jobs):
                results += out
                done = sum(1 for r in results if r["status"] != "error")
                print(f"  {len(results)}/{len(recs)} buildings ({done} ok/skipped)", flush=True)
    else:
        for j in jobs:
            results += run_job(j)
    db = manifest.connect(root)
    nfiles = 0
    for r in results:
        for f in r["files"]:
            manifest.add_file(f, source="buildings_lod2.py", license="derived (OSM ODbL, Metro open data, "
                              "USGS LiDAR public domain)", stage=1, root=root, db=db)
            nfiles += 1
    # forget manifest rows of files a rebuild removed (e.g. fewer collider parts than before)
    stale = 0
    for r in results:
        if r["status"] != "ok":
            continue
        prefix = f"buildings/{r['id']}/{bc.VERSION}/"
        for (path,) in db.execute("SELECT path FROM files WHERE path LIKE ?", (prefix + "%",)).fetchall():
            if not (root / path).exists():
                db.execute("DELETE FROM files WHERE path = ?", (path,))
                stale += 1
    db.commit()
    db.close()
    if stale:
        print(f"removed {stale} manifest rows of files that no longer exist")
    errs = [r for r in results if r["status"] == "error"]
    probs = [r for r in results if r.get("problems")]
    for r in errs:
        print(f"ERROR {r['id']}: {r['error']}\n{r['trace']}", file=sys.stderr)
    for r in probs:
        print(f"MESH {r['id']}: {r['problems']}", file=sys.stderr)
    log = bc.reports_dir(root) / "buildings_lod2_run.json"
    bc.save(log, {"made": time.strftime("%Y-%m-%dT%H:%M:%S"), "seconds": round(time.time() - t0, 1),
                  "buildings": len(recs), "ok": sum(r["status"] == "ok" for r in results),
                  "skipped": sum(r["status"].startswith("skipped") for r in results),
                  "errors": [{"id": r["id"], "error": r["error"]} for r in errs],
                  "mesh_problems": [{"id": r["id"], "problems": r["problems"]} for r in probs]})
    print(f"{len(recs)} buildings: {sum(r['status'] == 'ok' for r in results)} built, "
          f"{sum(r['status'].startswith('skipped') for r in results)} skipped, {len(errs)} errors, "
          f"{len(probs)} with mesh problems; {nfiles} files recorded; {time.time() - t0:.0f} s")
    return 1 if errs or probs else 0


if __name__ == "__main__":
    sys.exit(main())
