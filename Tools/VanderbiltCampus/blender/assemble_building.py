"""Stage 4 facade assembler: v2 LoD2 shell + facade layouts + a style kit -> LoD3 / LoD3+ building.

Run with the pipeline Python (the geometry is NumPy + shapely); it calls headless Blender
itself only to write the authored .blend:

    python blender/assemble_building.py B_544397715 --kit victorian_gothic --level 3+
    python blender/assemble_building.py B_X --kit brick_institutional --level 3 --no-blend

(pipeline/assemble.py makes estimated layouts first and then calls assemble() here.)
Inside Blender (`blender -b --python assemble_building.py -- --blend DIR`) it only builds
DIR/<id>_lod3.blend from the .npz files in DIR.

What it does, per building:
  1. loads buildings/<id>/v2 (lod2, lod1, col_mesh, model.json, ledger.json) and the newest
     layout of every facade (buildings/<id>/facades/F##/layout_v<n>.json);
  2. finds roof features in the LiDAR: a tower (points far above the eave; rebuilt as a clean
     shaft with its four faces as extra facades, a flat roof, parapet and corner piers at the
     heights the points show) and chimneys / roof volumes (Stage 1 ledger bumps with enough
     points, modeled now that the LoD3 merge threshold is smaller than LoD2's);
  3. per facade: takes the LoD2 wall triangles on that plane out, rebuilds the wall in the
     facade frame with the openings cut, and places kit pieces at the layout positions
     (projections and recesses extruded by depth_m);
  4. applies the merge rule through the kit (LoD3 0.15 m, LoD3+ 0.05 m) and writes one ledger
     entry per merged feature group;
  5. writes buildings/<id>/lod3_v<n>/ : lod3.npz (LoD0 game mesh), lod2.npz, lod1.npz,
     impostor.npz, game_col.npz, model.json, ledger.json, sources.json, <id>_lod3.blend, plus
     git mirrors of the JSON in Tools/VanderbiltCampus/buildings/<id>/lod3/ and
     derived/buildings/lod3/index.json.
"""
import argparse
import datetime
import json
import math
import os
import subprocess
import sys
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(TOOLS / "pipeline"))

try:
    import bpy  # noqa: F401
    IN_BLENDER = True
except ImportError:
    IN_BLENDER = False

LEVELS = ("3", "3+")
COL_TARGET = {"3": 1750, "3+": 1750}     # simplified shell triangles; steps and ledges come on top
TRI_TARGET = {"3": 20000, "3+": 30000}


# ======================================================================================
# geometry side (pipeline Python)
# ======================================================================================

def _imports():
    global shapely, Polygon, geom, pieces, get_kit, fl, vcmesh, manifest, common
    import shapely  # noqa: F811
    from shapely.geometry import Polygon  # noqa: F811
    from kits import geom, pieces, get_kit  # noqa: F811
    import facade_layout as fl  # noqa: F811
    import vcmesh  # noqa: F811
    import manifest  # noqa: F811
    import common  # noqa: F811


def face_normals(v, f):
    n = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    a = np.linalg.norm(n, axis=1)
    return n / np.maximum(a, 1e-12)[:, None], a / 2


def load_inputs(bid, root):
    b = Path(root) / "buildings" / bid / "v2"
    idx = common.load_json(Path(root) / "derived" / "buildings" / "v2" / "index.json")
    rec = next((r for r in idx["buildings"] if r["id"] == bid), None)
    if rec is None:
        raise KeyError(f"{bid} not in derived/buildings/v2/index.json")
    return {"rec": rec, "lod2": vcmesh.load_mesh(b / "lod2.npz"), "lod1": vcmesh.load_mesh(b / "lod1.npz"),
            "col": vcmesh.load_mesh(b / "col_mesh.npz"), "model": common.load_json(b / "model.json"),
            "ledger": common.load_json(b / "ledger.json") if (b / "ledger.json").exists() else {"entries": []}}


# ---- roof features from the LiDAR ----------------------------------------------------------

def main_eave(model):
    """Typical eave height of the building's big parts (area-weighted median)."""
    parts = [p for p in model["parts"] if p.get("eave_z") is not None]
    if not parts:
        return None
    z = np.array([p["eave_z"] for p in parts])
    a = np.array([p.get("area_m2", 1.0) for p in parts])
    o = np.argsort(z)
    c = np.cumsum(a[o])
    return float(z[o][np.searchsorted(c, c[-1] / 2)])


def detect_tower(points, footprint, eave_z, ground_z, min_rise=10.0, min_points=60):
    """A tower = a compact cluster of non-vegetation LiDAR points more than min_rise above the eave.
    Returns heights read from the points (roof, parapet, pier tops, cornice band) or None."""
    from shapely.geometry import MultiPoint, Point
    import shapely as sh
    P = np.c_[points["x"], points["y"], points["z"]]
    c = points["classification"]
    fp = Polygon(footprint).buffer(2.0)
    keep = (P[:, 2] > eave_z + min_rise) & (c != 5) & (c != 7) & (c != 18) & sh.contains_xy(fp, P[:, 0], P[:, 1])
    T = P[keep]
    if len(T) < min_points:
        return None
    # largest dense group (towers are compact): keep points within 6 m of the median
    med = np.median(T[:, :2], axis=0)
    T = T[np.linalg.norm(T[:, :2] - med, axis=1) < 8.0]
    if len(T) < min_points:
        return None
    rect = MultiPoint([tuple(p) for p in T[:, :2]]).minimum_rotated_rectangle
    outer = np.asarray(rect.exterior.coords)[:4]
    zs = np.sort(T[:, 2])
    hist, edges = np.histogram(zs, bins=np.arange(zs.min(), zs.max() + 0.21, 0.2))
    roof_z = float(edges[np.argmax(hist)] + 0.1)                 # the flat roof is the densest band
    dist_edge = np.array([rect.exterior.distance(Point(p)) for p in T[:, :2]])
    rim = dist_edge < 0.7
    pier_z = float(np.percentile(zs, 99.5))
    par = rim & (T[:, 2] > roof_z + 0.2) & (T[:, 2] < pier_z - 0.35)
    parapet_z = float(np.median(T[par, 2])) if par.sum() >= 5 else roof_z + 0.5
    low = rim & (T[:, 2] < roof_z - 0.8)
    cornice_z = float(np.median(T[low, 2])) if low.sum() >= 5 else roof_z - 1.5
    # the outermost points are the cornice overhang; the shaft is assumed 0.3 m inside it
    shaft = np.asarray(rect.buffer(-0.3, join_style=2).exterior.coords)[:4]
    # piers / pinnacles: compact groups of points standing above the parapet
    piers = []
    hi = T[T[:, 2] > parapet_z + 0.35]
    if len(hi) >= 3:
        from sklearn.cluster import DBSCAN
        lab = DBSCAN(eps=0.6, min_samples=3).fit_predict(hi[:, :2])
        for k in sorted(set(lab) - {-1}):
            g = hi[lab == k]
            ext = g[:, :2].max(axis=0) - g[:, :2].min(axis=0)
            piers.append({"center": g[:, :2].mean(axis=0).round(3).tolist(), "size": round(float(max(0.6, ext.max() + 0.3)), 2),
                          "top_z": round(float(g[:, 2].max()), 3), "points": int(len(g))})
    return {"outer": outer.round(3).tolist(), "shaft": shaft.round(3).tolist(), "points": int(len(T)),
            "roof_z": round(roof_z, 3), "parapet_z": round(parapet_z, 3), "pier_z": round(pier_z, 3),
            "cornice_z": round(cornice_z, 3), "piers": piers, "base_z": round(ground_z, 3),
            "cornice_projection_m": 0.3,
            "method": "LiDAR points > eave + 10 m: minimum rotated rectangle; roof = densest height band; "
                      "parapet / cornice = medians of rim points above / below it; piers = point groups "
                      "standing above the parapet (size from their spread, at least 0.6 m); shaft = rectangle shrunk 0.3 m (assumed cornice overhang)"}


def tower_facades(tower, n_before, ground_z):
    """The tower's four faces as facade planes (same convention as facade_layout.facades_of),
    numbered after the footprint facades."""
    runs = fl.wall_runs(tower["shaft"], min_run=1.0)
    out = []
    for i, (a, b) in enumerate(runs):
        a, b = np.asarray(a), np.asarray(b)
        u2 = (a - b) / np.linalg.norm(a - b)
        u = np.array([u2[0], u2[1], 0.0])
        n = np.cross(u, [0.0, 0.0, 1.0])
        out.append({"facade": f"F{n_before + i + 1:02d}", "tower": True,
                    "plane": {"origin": [round(float(b[0]), 3), round(float(b[1]), 3), round(ground_z, 3)],
                              "u": [round(float(c), 6) for c in u], "v": [0.0, 0.0, 1.0],
                              "normal": [round(float(c), 6) for c in n],
                              "width_m": round(float(np.linalg.norm(a - b)), 3),
                              "height_m": round(tower["roof_z"] - ground_z, 3)}})
    return out


def detect_roof_features(bid, inputs, root, points=None):
    """Tower + chimneys/roof volumes. points: LiDAR dict (lidar_io format) or None to load."""
    model, rec = inputs["model"], inputs["rec"]
    eave = main_eave(model) or float(inputs["lod2"]["verts"][:, 2].max())
    ground = float(model["ground_z"])
    if points is None:
        import lidar_io
        fp = np.asarray(rec["footprint"])
        points = lidar_io.load_points(fp[:, 0].min(), fp[:, 1].min(), fp[:, 0].max(), fp[:, 1].max(), buffer=3,
                                      root=root)
    tower = detect_tower(points, rec["footprint"], eave, ground)
    feats = {"eave_z": round(eave, 3), "tower": tower, "chimneys": [], "roof_volumes": [], "not_modeled": []}
    tpoly = Polygon(tower["outer"]).buffer(1.0) if tower else None
    fpoly = Polygon(rec["footprint"]).buffer(0.3)
    u = np.asarray(fl.wall_runs(rec["footprint"])[0][1]) - np.asarray(fl.wall_runs(rec["footprint"])[0][0])
    u = u / np.linalg.norm(u)
    nrm = np.array([-u[1], u[0]])
    feats["axis_u"] = u.round(6).tolist()
    for e in inputs["ledger"].get("entries", []):
        c, s = e["center"], e["size"]
        pts = e.get("points") or 0
        if tpoly is not None and tpoly.contains(shapely.Point(c[0], c[1])):
            continue
        kind = None
        if e["type"] == "chimney_or_vent" and pts >= 5 and min(s[0], s[1]) >= 0.5 and s[2] >= 0.8:
            kind = "chimneys"
        elif e["type"] == "roof_equipment" and pts >= 8 and min(s) >= 0.5 and max(s[0], s[1]) <= 8.0:
            kind = "roof_volumes"
        swap = False
        if kind:
            # the box must sit on the roof: try it along the main wall, then turned 90 degrees
            ok = False
            for swap in (False, True):
                a, b = (s[1], s[0]) if swap else (s[0], s[1])
                corners = [np.asarray(c[:2]) + i * a / 2 * u + j * b / 2 * nrm for i in (-1, 1) for j in (-1, 1)]
                if all(fpoly.contains(shapely.Point(*q)) for q in corners):
                    ok = True
                    break
            if not ok:
                kind = None
        if kind:
            feats[kind].append({"from": e["id"], "center": c, "size": [s[1], s[0], s[2]] if swap else s,
                                "points": pts, "turned": bool(swap)})
        else:
            feats["not_modeled"].append(e)
    return feats


# ---- walls ---------------------------------------------------------------------------------

def match_walls(lod2, planes, taken, max_depth=1.0, cos_min=math.cos(math.radians(15))):
    """Assign LoD2 wall triangles to facade planes. Returns {facade: face indices}."""
    v, f = lod2["verts"], lod2["faces"]
    n, a = face_normals(v, f)
    cen = v[f].mean(axis=1)
    wall = (np.abs(n[:, 2]) < 0.1) & (a > 1e-8) & ~taken
    best = np.full(len(f), -1)
    bestd = np.full(len(f), np.inf)
    names = list(planes)
    for k, name in enumerate(names):
        pl = planes[name]
        q = fl.to_facade(pl, v[f].reshape(-1, 3)).reshape(-1, 3, 3)
        nn = np.asarray(pl["normal"])
        ok = wall & (n @ nn >= cos_min) & (np.abs(q[:, :, 2]).max(axis=1) < max_depth)
        ok &= (q[:, :, 0].min(axis=1) > -0.3) & (q[:, :, 0].max(axis=1) < pl["width_m"] + 0.3)
        d = np.abs(q[:, :, 2]).mean(axis=1)
        better = ok & (d < bestd)
        best[better] = k
        bestd[better] = d[better]
    return {name: np.where(best == k)[0] for k, name in enumerate(names)}


def wall_polygon(lod2, faces, plane):
    """Union of the facade's LoD2 wall triangles in (u, v), and a depth-of-u lookup."""
    v, f = lod2["verts"], lod2["faces"]
    q = fl.to_facade(plane, v[f[faces]].reshape(-1, 3)).reshape(-1, 3, 3)
    tris = [Polygon(t[:, :2]) for t in q if Polygon(t[:, :2]).area > 1e-6]
    poly = shapely.unary_union([t.buffer(1e-4, join_style=2) for t in tris]).buffer(-1e-4, join_style=2)
    poly = poly.simplify(0.002)
    uu, dd = q[:, :, 0].ravel(), q[:, :, 2].ravel()
    o = np.argsort(uu)
    uu, dd = uu[o], dd[o]
    ku, inv = np.unique(np.round(uu, 3), return_inverse=True)
    kd = np.bincount(inv, dd) / np.bincount(inv)
    return poly, (lambda u: np.interp(u, ku, kd)) if len(ku) > 1 else (lambda u: np.full_like(np.asarray(u, float), dd.mean()))


def _place(mb, plane, depth_fn, center_u=None):
    """Facade-frame triangles -> world triangles (with the wall's local depth added)."""
    out = []
    if not mb.tris:
        return np.zeros((0, 3, 3)), np.zeros(0, np.uint8)
    T = np.asarray(mb.tris)
    if center_u is None:
        d = depth_fn(T[:, :, 0])
    else:
        d = np.full(T.shape[:2], float(depth_fn(center_u)))
    W = fl.to_world(plane, T[:, :, 0], T[:, :, 1], T[:, :, 2] + d)
    return W, np.asarray(mb.mats, np.uint8)


def build_facade(kit, level, layout, plane, wall_poly, depth_fn, report):
    """Rebuild one facade. Returns (world tris, mats, col tris, ledger entries)."""
    merger = geom.Merger(level, where=f"{layout['facade']}_wall")
    mats = kit["materials"]
    holes, parts, cols = [], [], []
    skipped = []
    for ft in layout["features"]:
        t = ft["type"]
        u0, v0, u1, v1 = ft["rect"]
        d = ft["depth_m"]
        out = ft["outline"]
        p = None
        if t in ("window", "door", "arch", "recess") and wall_poly is not None:
            r = Polygon(out) if out else shapely.box(u0, v0, u1, v1)
            if r.intersection(wall_poly).area < 0.95 * r.area:
                skipped.append(ft["id"])
                continue
        if t == "window":
            p = pieces.window(kit, merger, (u0, v0, u1, v1), arch=ft["arch"], reveal=None if d is None else abs(d))
        elif t == "door":
            p = pieces.door(kit, merger, (u0, v0, u1, v1), arch=ft["arch"], reveal=None if d is None else abs(d))
        elif t == "arch":
            p = pieces.arch(kit, merger, (u0, v0, u1, v1), arch_type=ft["arch"] if ft["arch"] != "none" else "round",
                            reveal=None if d is None else abs(d))
        elif t in ("cornice", "string_course"):
            p = pieces.course(kit, merger, u0, u1, v0, height=v1 - v0, depth=d, kind=t,
                              ledge=(d or 0) >= 0.2)
        elif t == "pilaster":
            p = pieces.pilaster(kit, merger, (u0, v0, u1, v1), d if d is not None else 0.15)
        elif t == "column":
            dia = u1 - u0
            p = pieces.column(kit, merger, (u0 + u1) / 2, d if d is not None else dia / 2 + 0.05, v0, v1, dia)
        elif t == "buttress":
            p = pieces.buttress(kit, merger, (u0, v0, u1, v1), d if d is not None else 0.6)
        elif t == "steps":
            p = pieces.steps(kit, merger, u0, u1, v0, v1, d if d is not None else 2.0)
        elif t == "balustrade":
            p = pieces.balustrade(kit, merger, u0, u1, v0, v1 - v0, 0.25, cz=(d or 0.0) + 0.125)
        elif t == "chimney":
            p = pieces.chimney(kit, merger, (u0 + u1) / 2, -(d or 0.6) / 2, v0, u1 - u0, abs(d or 0.6), v1 - v0)
        elif t == "dormer":
            p = pieces.dormer(kit, merger, (u0 + u1) / 2, v0, u1 - u0, (v1 - v0) * 0.6, abs(d or 2.0))
        elif t in ("projection", "recess"):
            outline = out or [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
            dep = d if d is not None else (0.2 if t == "projection" else -0.2)
            if dep >= 0:
                p = pieces.projection(kit, merger, outline, dep,
                                      mat=mats["trim"] if out else mats["wall"])
            else:
                p = pieces.recess(kit, merger, outline, -dep,
                                  mat=mats["metal"] if ft["arch"] != "none" else mats["wall"])
        if p is None:
            continue
        if p.hole is not None:
            holes.append(Polygon(p.hole))
        cu = (u0 + u1) / 2
        W, M = _place(p.mesh, plane, depth_fn, cu)
        parts.append((W, M))
        C, _ = _place(p.col, plane, depth_fn, cu)
        if len(C):
            cols.append(C)
    # the wall itself, with the openings cut
    wall_tris, wall_mats = [], []
    if wall_poly is not None and not wall_poly.is_empty:
        cut = wall_poly.difference(shapely.unary_union(holes)) if holes else wall_poly
        mb = geom.MeshBuilder()
        for g in (cut.geoms if hasattr(cut, "geoms") else [cut]):
            if g.geom_type == "Polygon" and g.area > 1e-4:
                mb.flat(g, 0.0, "z", mats["wall"])
        W, M = _place(mb, plane, depth_fn)
        parts.insert(0, (W, M))
    T = np.concatenate([w for w, _ in parts]) if parts else np.zeros((0, 3, 3))
    Mt = np.concatenate([m for _, m in parts]) if parts else np.zeros(0, np.uint8)
    entries = []
    for e in merger.entries:
        c = e["center"]
        e = dict(e)
        e["center"] = [round(float(x), 3) for x in fl.to_world(plane, c[0], c[1], c[2] + float(depth_fn(c[0])))]
        e["facade"] = layout["facade"]
        entries.append(e)
    report[layout["facade"]] = {"features": len(layout["features"]), "skipped_outside_wall": skipped,
                                "merged_entries": len(merger.entries), "triangles": int(len(T))}
    return T, Mt, (np.concatenate(cols) if cols else np.zeros((0, 3, 3))), entries


# ---- tower top and roof pieces --------------------------------------------------------------

def world_frame(u2):
    """A facade-like frame lying along horizontal direction u2 (for roof pieces)."""
    u = np.array([u2[0], u2[1], 0.0]) / np.linalg.norm(u2)
    return {"origin": [0.0, 0.0, 0.0], "u": u.tolist(), "v": [0.0, 0.0, 1.0],
            "normal": np.cross(u, [0, 0, 1.0]).tolist()}


def build_tower_top(kit, level, tower, tplanes, ledger):
    """Flat roof, parapet ring and corner piers on the rebuilt tower, built in its first face's frame."""
    merger = geom.Merger(level, where="tower_top")
    pl = tplanes[0]["plane"]
    W = pl["width_m"]
    D = tplanes[1]["plane"]["width_m"]
    mats = kit["materials"]
    roof, par, pier = tower["roof_z"], tower["parapet_z"], tower["pier_z"]
    o = pl["origin"][2]
    mb, col = geom.MeshBuilder(), geom.MeshBuilder()
    # roof cap: facade frame (x along, y up, z out); the tower occupies z in [-D, 0]
    mb.box(0, W, roof - o - 0.05, roof - o, -D, 0, mats["roof"] if kit["name"] != "victorian_gothic" else "roof_flat",
           skip=("y-",))
    t = 0.35
    if par - roof >= merger.threshold:
        for x0, x1, z0, z1 in ((0, W, -t, 0), (0, W, -D, -D + t), (0, t, -D + t, -t), (W - t, W, -D + t, -t)):
            mb.box(x0, x1, roof - o, par - o, z0, z1, mats["trim"], skip=("y-",))
    else:
        merger.keep("parapet", par - roof, W, par - roof, (W / 2, par - o, -D / 2), (W, D, par - roof), mats["trim"],
                    "tower_roof", "geometry")
    for pr in tower.get("piers", []):
        q = fl.to_facade(pl, [pr["center"][0], pr["center"][1], o])
        p = pieces.chimney(kit, merger, float(q[0]), float(q[2]), roof - o, pr["size"], pr["size"], pr["top_z"] - roof)
        mb.extend(p.mesh)
    Wt, Mt = _place(mb, pl, lambda u: np.zeros_like(np.asarray(u, float)), 0.0)
    for e in merger.entries:
        c = e["center"]
        e = dict(e)
        e["center"] = [round(float(x), 3) for x in fl.to_world(pl, c[0], c[1], c[2])]
        ledger.append(e)
    return Wt, Mt


def build_roof_pieces(kit, level, feats, u2, ledger):
    """Chimneys and roof volumes from the LiDAR bumps (axis = the building's main wall direction)."""
    merger = geom.Merger(level, where="roof")
    fr = world_frame(u2)
    u, n = np.asarray(fr["u"]), np.asarray(fr["normal"])
    mb, col = geom.MeshBuilder(), geom.MeshBuilder()
    for c in feats["chimneys"] + feats["roof_volumes"]:
        x, y, z = c["center"]
        sx, sy, sz = c["size"]
        cu, cz = x * u[0] + y * u[1], x * n[0] + y * n[1]
        v0, h = z - sz / 2 - 0.4, sz + 0.4            # sink 0.4 m into the roof so no gap shows on a slope
        if c in feats["chimneys"]:
            p = pieces.chimney(kit, merger, cu, cz, v0, max(sx, 0.5), max(sy, 0.5), h)
            mb.extend(p.mesh)
            col.extend(p.col)
        else:
            mb.box(cu - sx / 2, cu + sx / 2, v0, v0 + h, cz - sy / 2, cz + sy / 2, kit["materials"]["roof"], skip=("y-",))
    W, M = _place(mb, fr, lambda q: np.zeros_like(np.asarray(q, float)), 0.0)
    C, _ = _place(col, fr, lambda q: np.zeros_like(np.asarray(q, float)), 0.0)
    for e in merger.entries:
        e = dict(e)
        cc = e["center"]
        e["center"] = [round(float(q), 3) for q in fl.to_world(fr, cc[0], cc[1], cc[2])]
        ledger.append(e)
    return W, M, C


# ---- outputs ---------------------------------------------------------------------------------

def tri_soup_to_mesh(T, M, unwrap=True):
    mb = geom.MeshBuilder()
    mb.tris = list(T)
    mb.mats = list(M)
    v, f, m = mb.arrays()
    if unwrap:
        uv, info = geom.unwrap(v, f)
    else:
        uv, info = np.zeros((len(f), 3, 2), np.float32), {}
    return {"verts": v, "faces": f, "face_mat": m, "uv0": uv, "meta": info}


def impostor(lod1, footprint):
    """An oriented box around the building (12 triangles) with real UV0."""
    from shapely.geometry import MultiPoint
    r = np.asarray(MultiPoint([tuple(p) for p in footprint]).minimum_rotated_rectangle.exterior.coords)[:4]
    z0, z1 = float(lod1["verts"][:, 2].min()), float(lod1["verts"][:, 2].max())
    e1 = r[1] - r[0]
    e2 = r[3] - r[0]
    mb = geom.MeshBuilder()
    W, D = np.linalg.norm(e1), np.linalg.norm(e2)
    mb.box(0, W, 0, z1 - z0, 0, D, "brick_red")
    T = np.asarray(mb.tris)
    a1, a2 = e1 / W, e2 / D
    out = np.empty_like(T)
    out[..., 0] = r[0][0] + T[..., 0] * a1[0] + T[..., 2] * a2[0]
    out[..., 1] = r[0][1] + T[..., 0] * a1[1] + T[..., 2] * a2[1]
    out[..., 2] = z0 + T[..., 1]
    # box() is built in a right-handed (x, y=up, z) frame; mapping (x, z, y) -> world flips
    # handedness when a1 x a2 points down, so fix the winding if needed
    if np.cross(np.r_[a1, 0], np.r_[a2, 0])[2] > 0:
        out = out[:, ::-1]
    return tri_soup_to_mesh(out, np.asarray(mb.mats, np.uint8))


def simplify_collider(col, target):
    """Decimate the v2 collider shell to about `target` triangles (open3d quadric), keeping it
    clean (no degenerate / duplicate triangles, consistent winding). Returns (v, f, info)."""
    import open3d as o3d
    import buildings_v2 as b2
    v, f = b2.clean_tris(col["verts"], col["faces"])
    info = {"source_triangles": int(len(f)), "target": target}
    if len(f) <= target:
        info.update(triangles=int(len(f)), max_dev_m=0.0)
        return v, f, info
    off = v.mean(axis=0)
    m = o3d.geometry.TriangleMesh(o3d.utility.Vector3dVector(v - off), o3d.utility.Vector3iVector(f.astype(np.int32)))
    tgt = target
    best = None
    while tgt < len(f):
        s = m.simplify_quadric_decimation(int(tgt), boundary_weight=1000.0)
        s.remove_degenerate_triangles()
        s.remove_duplicated_triangles()
        s.remove_non_manifold_edges()
        sv, sf = b2.clean_tris(np.asarray(s.vertices) + off, np.asarray(s.triangles))
        probs = b2.collider_problems({"verts": sv, "faces": sf})
        if not probs and len(sf) >= 4:
            dmax, d95 = b2.surface_distance(v, f, sv, sf, n_samples=8000)
            best = (sv, sf, dmax, d95)
            break
        tgt = int(tgt * 1.15)
    if best is None:
        info.update(triangles=int(len(f)), max_dev_m=0.0, note="decimation never clean; full shell kept")
        return v, f, info
    sv, sf, dmax, d95 = best
    info.update(triangles=int(len(sf)), max_dev_m=round(dmax, 3), p95_dev_m=round(d95, 3))
    return sv, sf, info


def next_version(bdir):
    n = 0
    for d in bdir.glob("lod3_v*"):
        try:
            n = max(n, int(d.name.split("_v")[1]))
        except ValueError:
            pass
    return n + 1


def write_index(root):
    """derived/buildings/lod3/index.json: every building that has a lod3 folder (newest version)."""
    root = Path(root)
    rows = []
    for bdir in sorted((root / "buildings").glob("*")):
        vers = sorted(bdir.glob("lod3_v*"), key=lambda d: int(d.name.split("_v")[1]) if d.name.split("_v")[1].isdigit() else 0)
        if not vers or not (vers[-1] / "model.json").exists():
            continue
        m = common.load_json(vers[-1] / "model.json")
        rows.append({"id": bdir.name, "folder": f"buildings/{bdir.name}/{vers[-1].name}", "level": m["lod_reached"],
                     "kit": m["style_kit"], "measured": bool(m["measured"]), "name": m.get("name")})
    path = root / "derived" / "buildings" / "lod3" / "index.json"
    common.save_json(path, {"version": 1, "made": datetime.datetime.now().isoformat(timespec="seconds"),
                            "note": "Buildings with a Stage 4 LoD3/LoD3+ model. measured=false means the facade "
                                    "layouts are estimated (no aligned photos yet); re-run when they exist.",
                            "buildings": rows})
    manifest.add_file(path, "blender/assemble_building.py", "derived", stage=4, root=root)
    return path


def assemble(bid, kit_name, level, root=None, layouts=None, points=None, roof=None, write=True, blend=True,
             version=None, git_mirror=True, extra_sources=None, notes=None):
    """Build one LoD3/LoD3+ building. layouts: {facade: layout} (default: newest in the archive).
    roof: precomputed detect_roof_features() result (or {} to skip roof features).
    Returns a summary dict (and writes the files when write=True)."""
    _imports()
    if level not in LEVELS:
        raise ValueError(f"level must be one of {LEVELS}")
    root = Path(root or common.data_root())
    kit = get_kit(kit_name)
    inp = load_inputs(bid, root)
    lod2, model, rec = inp["lod2"], inp["model"], inp["rec"]
    ground = float(model["ground_z"])
    layouts = layouts if layouts is not None else fl.load_building(bid, root)
    feats = roof if roof is not None else detect_roof_features(bid, inp, root, points)
    tower = feats.get("tower") if feats else None
    fac = {f["facade"]: f for f in fl.facades_of(bid, root, lod2=lod2, footprint=rec["footprint"], ground_z=ground)}
    tplanes = tower_facades(tower, len(fac), ground) if tower else []
    tnames = {t["facade"] for t in tplanes}
    for t in tplanes:
        fac[t["facade"]] = t

    v, f = lod2["verts"], lod2["faces"]
    n, _ = face_normals(v, f)
    cen = v[f].mean(axis=1)
    removed = np.zeros(len(f), bool)
    if tower:
        tp = Polygon(tower["shaft"]).buffer(0.6, join_style=2)
        inside = shapely.contains_xy(tp, cen[:, 0], cen[:, 1])
        removed |= inside & ((cen[:, 2] > feats["eave_z"] + 1.0) | (np.abs(n[:, 2]) < 0.1))
    report, ledger, tris, mats, cols = {}, [], [], [], []
    planes = {k: fac[k]["plane"] for k in layouts if k in fac and k not in tnames}
    for k in layouts:
        if k not in fac:
            report[k] = {"error": "facade not found on this building (layout ignored)"}
    matched = match_walls(lod2, planes, removed)
    for k, lay in sorted(layouts.items()):
        if k not in fac:
            continue
        pl = fac[k]["plane"]
        if k in tnames:
            wp = shapely.box(0, 0, pl["width_m"], pl["height_m"])
            dfn = (lambda u: np.zeros_like(np.asarray(u, float)))
        else:
            idx = matched.get(k, np.zeros(0, int))
            if not len(idx):
                report[k] = {"error": "no LoD2 wall triangles on this plane; facade left as LoD2"}
                continue
            removed[idx] = True
            wp, dfn = wall_polygon(lod2, idx, pl)
        T, M, C, E = build_facade(kit, level, lay, pl, wp, dfn, report)
        tris.append(T); mats.append(M); cols.append(C); ledger += E
    u_main = np.asarray(feats.get("axis_u") or next(iter(fac.values()))["plane"]["u"][:2]) if feats else None
    if tower:
        T, M = build_tower_top(kit, level, tower, tplanes, ledger)
        tris.append(T); mats.append(M)
    if feats:
        T, M, C = build_roof_pieces(kit, level, feats, u_main, ledger)
        tris.append(T); mats.append(M); cols.append(C)
        for e in feats.get("not_modeled", []):
            ledger.append({"type": e["type"], "merged_into": "roof", "center": e["center"], "size": e["size"],
                           "count": e.get("count", 1), "material": e.get("material", "roof_slate"),
                           "restore_hint": e.get("restore_hint", "geometry"),
                           "reason": "Stage 1 LiDAR bump kept merged: too few points, too small, or its box "
                                     f"does not fit on the roof ({e['id']}; {e.get('points')} points)"})
    keep = ~removed
    T0 = v[f[keep]]
    M0 = lod2["face_mat"][keep]
    allT = np.concatenate([T0] + [t for t in tris if len(t)])
    allM = np.concatenate([M0] + [m for m in mats if len(m)])
    lod3 = tri_soup_to_mesh(allT, allM)
    lod3["meta"].update({"building": bid, "lod": "LoD3+" if level == "3+" else "LoD3", "kit": kit_name,
                         "frame": "campus meters, Z up", "script": "blender/assemble_building.py"})
    problems = vcmesh.check_mesh(lod3)

    # collider: simplified v2 shell + steps, ledges, buttresses, chimneys
    sv, sf, cinfo = simplify_collider(inp["col"], COL_TARGET[level])
    extra = np.concatenate([c for c in cols if len(c)]) if any(len(c) for c in cols) else np.zeros((0, 3, 3))
    import buildings_v2 as b2
    if len(extra):
        mb = geom.MeshBuilder()
        mb.tris = list(extra)
        mb.mats = [geom.mat_id("concrete")] * len(extra)
        ev, ef, _ = mb.arrays()
        cv = np.vstack([sv, ev])
        cf = np.vstack([sf, ef + len(sv)])
    else:
        cv, cf = sv, sf
    cv, cf = b2.clean_tris(cv, cf)
    col = {"verts": cv, "faces": cf.astype(np.int32), "face_mat": np.full(len(cf), geom.mat_id("concrete"), np.uint8),
           "uv0": np.zeros((len(cf), 3, 2), np.float32), "meta": {"collider": True}}
    cinfo["extra_solid_triangles"] = int(len(extra))
    cinfo["triangles_total"] = int(len(cf))
    cinfo["problems"] = b2.collider_problems(col)
    imp = impostor(inp["lod1"], rec["footprint"])

    # ledger ids, merging repeats of the same feature type on the same facade into one row
    groups = {}
    for e in ledger:
        key = (e.get("facade", e["merged_into"]), e["type"], e["material"], e["restore_hint"])
        groups.setdefault(key, []).append(e)
    rows = []
    for (facade, typ, mat, hint), es in groups.items():
        C = np.array([e["center"] for e in es], float)
        S = np.array([e["size"] for e in es], float)
        rows.append({"id": f"L{len(rows) + 1:04d}", "type": typ, "merged_into": es[0]["merged_into"],
                     "center": C.mean(axis=0).round(3).tolist(),
                     "size": S.max(axis=0).round(3).tolist(), "count": int(sum(e.get("count", 1) for e in es)),
                     "material": mat, "photos": [], "restore_hint": hint, "reason": es[0].get("reason", ""),
                     "facade": facade if str(facade).startswith("F") else None,
                     "spread": (C.max(axis=0) - C.min(axis=0)).round(3).tolist() if len(es) > 1 else None})

    summary = {"building": bid, "kit": kit_name, "level": level, "triangles": {
        "lod3": int(len(lod3["faces"])), "lod2": int(len(lod2["faces"])), "lod1": int(len(inp["lod1"]["faces"])),
        "impostor": int(len(imp["faces"])), "game_col": int(len(cf))},
        "facades": report, "ledger_rows": len(rows), "lod3_problems": problems, "collider": cinfo,
        "roof": {"tower": tower, "chimneys": len(feats.get("chimneys", [])) if feats else 0,
                 "roof_volumes": len(feats.get("roof_volumes", [])) if feats else 0,
                 "not_modeled": len(feats.get("not_modeled", [])) if feats else 0}}
    if not write:
        summary["meshes"] = {"lod3": lod3, "col": col, "impostor": imp}
        summary["ledger"] = rows
        return summary

    # ---- write -----------------------------------------------------------------------------
    bdir = root / "buildings" / bid
    ver = version or next_version(bdir)
    out = bdir / f"lod3_v{ver}"
    out.mkdir(parents=True, exist_ok=True)
    files = {}
    for name, m in (("lod3", lod3), ("lod2", lod2), ("lod1", inp["lod1"]), ("impostor", imp), ("game_col", col)):
        p = out / f"{name}.npz"
        vcmesh.save_mesh(p, m["verts"], m["faces"], m["face_mat"], m["uv0"], m.get("meta"))
        files[name] = p.name
    measured_by_facade = {}
    for k, lay in sorted(layouts.items()):
        srcs = sorted({ft["source"] for ft in lay["features"]})
        measured_by_facade[k] = {"layout": f"facades/{k}/layout_v{lay['version']}.json", "version": lay["version"],
                                 "reviewed": lay["reviewed"], "feature_sources": srcs,
                                 "measured": bool(lay["features"]) and all(s in ("detected", "manual") for s in srcs)
                                 and lay["source"].get("alignment_set") is not None}
    measured = bool(measured_by_facade) and all(x["measured"] for x in measured_by_facade.values())
    mid = vcmesh.material_index()
    inv = {i: k for k, i in mid.items()}
    v2acc = model.get("accuracy", {})
    model_json = {
        "building": bid, "name": model.get("name"), "version": ver, "stage": 4, "style_kit": kit_name,
        "lod_reached": "LoD3+" if level == "3+" else "LoD3", "level": level,
        "merge_threshold_m": geom.THRESHOLDS[level], "measured": measured,
        "status": ("measured" if measured else
                   "STAND-IN: facade layouts are estimated, not photo-measured. Re-run assemble.py with measured "
                   "layouts when the aligned capture exists (DECISIONS D009)."),
        "made": datetime.datetime.now().isoformat(timespec="seconds"),
        "inputs": {"lod2": f"buildings/{bid}/v2/lod2.npz", "collider_from": f"buildings/{bid}/v2/col_mesh.npz",
                   "layouts": measured_by_facade},
        "triangles": summary["triangles"], "triangle_target": TRI_TARGET[level],
        "within_triangle_target": summary["triangles"]["lod3"] <= TRI_TARGET[level],
        "collider": cinfo, "materials_used": sorted(inv[int(i)] for i in np.unique(lod3["face_mat"])),
        "uv": {"charts": lod3["meta"].get("charts"), "uv_m_per_unit": lod3["meta"].get("uv_m_per_unit"),
               "texel_density_px_per_m_at_4k": round(4096 / lod3["meta"]["uv_m_per_unit"], 1)
               if lod3["meta"].get("uv_m_per_unit") else None},
        "roof_features": summary["roof"], "facades": report,
        "accuracy": {"footprint_corners_and_heights_from_v2": v2acc,
                     "lod3_feature_positions": None if not measured else "see check_building.py report",
                     "note": "LoD3 feature accuracy (0.1 m / 0.05 m targets) needs aligned photos; "
                             "check_building.py photo check reports it when cameras exist."},
        "files": {**files, "blend": f"{bid}_lod3.blend", "ledger": "ledger.json", "sources": "sources.json"},
        "mesh_problems": problems, "notes": notes or [],
    }
    ledger_json = {"building": bid, "version": ver, "lod": model_json["lod_reached"],
                   "threshold_m": geom.THRESHOLDS[level],
                   "size_axes": "facade rows: [along wall, out of wall, up]; roof rows: [along main wall, across, up]",
                   "note": "one row per merged feature type per facade (count = how many were merged); "
                           "center = mean of the merged features, size = largest one, spread = extent of the centers",
                   "entries": rows}
    sources = {"building": bid, "version": ver, "stage": 4,
               "lidar": {"path": "derived/lidar/v1/tiles", "use": "tower heights, roof bumps (chimneys), "
                                                                   "stairs and ground beside each facade"},
               "lod2": f"buildings/{bid}/v2/lod2.npz",
               "facades": {k: {"layout": x["layout"], "photos": layouts[k]["source"]["photos"],
                               "alignment_set": layouts[k]["source"]["alignment_set"],
                               "measured": x["measured"]} for k, x in measured_by_facade.items()},
               "reference_images": (extra_sources or {}).get("reference_images", []),
               "public_facts": (extra_sources or {}).get("public_facts", [])}
    prev = bdir / "sources.json"
    if prev.exists():
        sources["stage3_sources"] = common.load_json(prev)
    for name, data in (("model.json", model_json), ("ledger.json", ledger_json), ("sources.json", sources)):
        common.save_json(out / name, data)
    for p in sorted(out.glob("*.npz")) + [out / "model.json", out / "ledger.json", out / "sources.json"]:
        manifest.add_file(p, "blender/assemble_building.py", "derived", stage=4, root=root)
    if blend:
        write_blend(out, bid)
        bp = out / f"{bid}_lod3.blend"
        if bp.exists():
            manifest.add_file(bp, "blender/assemble_building.py (Blender)", "derived", stage=4, root=root)
        else:
            model_json["files"]["blend"] = None
            model_json["notes"].append("Blender did not write the .blend (see console)")
            common.save_json(out / "model.json", model_json)
            manifest.add_file(out / "model.json", "blender/assemble_building.py", "derived", stage=4, root=root)
    if git_mirror and not os.environ.get("VC_DATA_ROOT"):
        g = TOOLS / "buildings" / bid / "lod3"
        for name, data in (("model.json", model_json), ("ledger.json", ledger_json), ("sources.json", sources)):
            common.save_json(g / name, data)
    write_index(root)
    summary["folder"] = str(out)
    summary["measured"] = measured
    return summary


def write_blend(out, bid):
    exe = common.local_config().get("blender")
    if not exe or not Path(exe).exists():
        print("Blender not found; .blend skipped")
        return
    cmd = [exe, "-b", "--factory-startup", "--python", str(Path(__file__).resolve()), "--", "--blend", str(out),
           "--id", bid]
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=600)
    if r.returncode != 0:
        print(r.stdout[-2000:], r.stderr[-2000:])


# ======================================================================================
# Blender side: build the authored .blend from the npz files
# ======================================================================================

def blender_main(argv):
    import bpy
    import vcmesh as vm
    ap = argparse.ArgumentParser()
    ap.add_argument("--blend", required=True)
    ap.add_argument("--id", required=True)
    a = ap.parse_args(argv)
    out = Path(a.blend)
    ids = json.load(open(TOOLS / "config" / "material_ids.json"))["ids"]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {}
    for m in ids:
        mat = bpy.data.materials.new(f"mat_{m['id']}")
        h = m["candy"].lstrip("#")
        rgb = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        mat.use_nodes = True
        bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs[0].default_value = (*[c ** 2.2 for c in rgb], 1.0)
        mat.diffuse_color = (*rgb, 1.0)
        mats[m["index"]] = mat
    coll = bpy.data.collections.new(a.id)
    bpy.context.scene.collection.children.link(coll)
    for name in ("lod3", "lod2", "lod1", "impostor", "game_col"):
        p = out / f"{name}.npz"
        if not p.exists():
            continue
        m = vm.load_mesh(p)
        me = bpy.data.meshes.new(f"{a.id}_{name}")
        me.from_pydata(m["verts"].tolist(), [], m["faces"].tolist())
        used = sorted(set(int(i) for i in np.unique(m["face_mat"])))
        for i in used:
            me.materials.append(mats[i])
        slot = {i: k for k, i in enumerate(used)}
        me.polygons.foreach_set("material_index", [slot[int(i)] for i in m["face_mat"]])
        uvl = me.uv_layers.new(name="UV0")
        uvl.data.foreach_set("uv", m["uv0"].reshape(-1).tolist())
        col = me.color_attributes.new("mat_index", "FLOAT_COLOR", "CORNER")
        cols = np.ones((len(m["faces"]), 3, 4), np.float32)
        cols[:, :, 0] = (m["face_mat"].astype(np.float32) / 255.0)[:, None]
        col.data.foreach_set("color", cols.reshape(-1).tolist())
        me.update()
        ob = bpy.data.objects.new(f"{a.id}_{name.upper()}", me)
        coll.objects.link(ob)
        if name != "lod3":
            ob.hide_render = True
            ob.hide_set(True) if hasattr(ob, "hide_set") else None
    target = out / f"{a.id}_lod3.blend"
    if target.exists():
        target.unlink()          # no .blend1 backup copies in the archive
    bpy.ops.wm.save_as_mainfile(filepath=str(target))
    print("saved", out / f"{a.id}_lod3.blend")


def main(argv=None):
    if IN_BLENDER:
        argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
        blender_main(argv)
        return
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("building")
    ap.add_argument("--kit", required=True)
    ap.add_argument("--level", choices=LEVELS, default="3")
    ap.add_argument("--no-blend", action="store_true")
    args = ap.parse_args(argv)
    s = assemble(args.building, args.kit, args.level, blend=not args.no_blend)
    print(json.dumps({k: s[k] for k in ("building", "triangles", "ledger_rows", "lod3_problems", "folder", "measured")},
                     indent=1))
    if s["lod3_problems"] or s["collider"]["problems"]:
        common.fail(f"problems: {s['lod3_problems']} {s['collider']['problems']}")


if __name__ == "__main__":
    main()
