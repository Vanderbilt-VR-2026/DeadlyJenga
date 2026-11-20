"""Terrain meshes per tile from LiDAR ground points (Stage 1, terrain step 2).

For every tile in zone campus or ring (derived/lidar/v1/index.json):
  phase 1 "grid"   ground points (class 2, 10 m buffer) -> spikes dropped -> cell heights
                   (0.5 m cells if the tile touches the campus boundary, else 2 m) -> holes
                   filled (TIN, then nearest) -> breaks >= 15 cm found (step test on the
                   points; mapped retaining walls force breaks) -> node heights.
  phase 2 "mesh"   visual mesh (flat areas merged with <= 3 cm error, materials from
                   surfaces.py, breaks as vertical faces, UV0) and collider mesh (<= 8 cm,
                   materials ignored), checked against the tile's ground points.

Tile edges never crack: a node on a tile edge is "owned" by one tile (the tile that holds
it by floor(X/100), floor(Y/100), else the next existing one), both tiles read the owner's
node heights, sample the edge at the coarser of their two grids, and simplify that profile
with the same Douglas-Peucker rule (3 cm visual, 8 cm collider). Nodes in between lie
exactly on the shared straight segments.

Outputs in data_root/derived/terrain/<version>/<tile>/:
  terrain.npz       visual mesh (vcmesh format; face_mat from config/material_ids.json)
  terrain_col.npz   collider mesh
  heights.npy       node heights float32 [(n+1) x (n+1)], breaks ignored; heights.json explains it
  cells.npz         phase 1 working data (cell heights, point counts, gaps, break masks)
  tile.json         triangles, breaks, hole fraction, material areas, accuracy
plus derived/terrain/<version>/index.json and reports/stage1/terrain_build.json.

Usage:
  python terrain.py --all --jobs 10
  python terrain.py --tile T_00_00            (also grids its neighbors if needed)
  python terrain.py --all --force             (rebuild; refused once <version>/ACCEPTED exists)
Run surfaces.py first.
"""
import argparse
import json
import math
import sys
import time
import traceback
from concurrent.futures import ProcessPoolExecutor, as_completed
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
import terrain_mesh as tm  # noqa: E402
import vcmesh  # noqa: E402
from common import data_root, fail, load_json, save_json  # noqa: E402

LICENSE = "Public domain (USGS 3DEP); credit: U.S. Geological Survey; surfaces ODbL (c) OpenStreetMap contributors"
BUFFER = 10.0                       # m of extra points around each tile
VIS_BORDER_TOL = 0.03               # border profile rule, visual (same for every tile)
COL_BORDER_TOL = 0.08               # border profile rule, collider
VIS_TOL = {"campus": [0.03, 0.04, 0.05, 0.06], "ring": [0.03, 0.06, 0.10]}
VIS_BUDGET = {"campus": 12000, "ring": 1500}
COL_TOL = 0.08
COL_BUDGET = 2500
# 3 x 3 denoise passes: 2 on 0.5 m cells (about 1 point per cell); none on 2 m cells, which
# already average about 16 points and where a 6 m window would flatten real ground shape
DENOISE_PASSES = {"campus": 2, "ring": 0}
FAR_CELL = 4.0                      # far mesh grid (m), the same for every tile so far edges match
FAR_TOL = 0.5                       # far mesh max vertical error (m), also its border rule
FAR_BUDGET = {"campus": 300, "ring": 150}
FAR_SKIRT = 1.5                     # m the far mesh's border skirt hangs down
HOLE_TOL = {"visual": 0.10, "collider": 0.15}    # error allowed where no ground point is within 1 m


def cell_size(t):
    """0.5 m for tiles touching the campus boundary, 2 m for ring-only tiles."""
    return 0.5 if t["campus_fraction"] > 0 else 2.0


def kind_of(t):
    return "campus" if cell_size(t) == 0.5 else "ring"


def tdir(root, version, name):
    return Path(root) / "derived" / "terrain" / version / name


# ---- retaining walls from OSM -------------------------------------------------

def retaining_walls(root):
    import surfaces
    _, elements, _, ways, _, xy = surfaces.load_osm(root)
    out = []
    for el in elements:
        if el["type"] == "way" and (el.get("tags") or {}).get("barrier") == "retaining_wall":
            c = surfaces.way_line(el, xy)
            if len(c) >= 2:
                out.append({"id": f"way/{el['id']}", "coords": c})
    return out


# ---- phase 1: grid -------------------------------------------------------------

def grid_tile(name, t, walls, root, version):
    """Phase 1 for one tile: read points, compute, write heights.npy / heights.json / cells.npz."""
    t0 = time.time()
    x0, y0, x1, y1 = t["bounds"]
    pts = lidar_io.load_points(x0, y0, x1, y1, classes=[2], buffer=BUFFER, root=root)
    if pts["x"].size < 10:
        raise RuntimeError(f"{name}: only {pts['x'].size} ground points around the tile")
    g = grid_from_points(pts["x"], pts["y"], pts["z"], t, walls)
    g["stats"]["grid_seconds"] = round(time.time() - t0, 2)
    return save_grid(name, t, g, root, version), g["stats"]


def save_grid(name, t, g, root, version):
    x0, y0 = t["bounds"][0], t["bounds"][1]
    d = tdir(root, version, name)
    d.mkdir(parents=True, exist_ok=True)
    np.save(d / "heights.npy", g["nodes"])
    save_json(d / "heights.json", {
        "file": "heights.npy", "tile": name, "dtype": "float32", "shape": list(g["nodes"].shape),
        "cell_m": cell_size(t), "origin_x": x0, "origin_y": y0,
        "layout": "heights[J, I] = ground Z (campus meters) at node X = origin_x + I * cell_m, "
                  "Y = origin_y + J * cell_m (row 0 = south edge). Node = mean of its 4 cells; "
                  "breaks are not represented here (see cells.npz / terrain.npz).",
        "frame": "Vanderbilt campus frame v1 (config/frame.json)"})
    np.savez_compressed(d / "cells.npz", hc=g["hc"].astype(np.float32), count=g["count"].astype(np.uint16),
                        holes=g["holes"], bx=g["bx"], by=g["by"], fx=g["fx"], fy=g["fy"],
                        stats=json.dumps(g["stats"]))
    return [d / "heights.npy", d / "heights.json", d / "cells.npz"]


def grid_from_points(x, y, z, t, walls=()):
    """Ground points (tile + 10 m buffer) -> cell heights, breaks, node heights."""
    x0, y0, x1, y1 = t["bounds"]
    cell = cell_size(t)
    n = int(round(100 / cell))
    P = int(round(BUFFER / cell))
    in_tile = (x >= x0) & (x < x1) & (y >= y0) & (y < y1)
    keep = tm.drop_spikes(x, y, z)
    spikes_tile = int((~keep & in_tile).sum())
    x, y, z = x[keep], y[keep], z[keep]
    gx0, gy0 = x0 - P * cell, y0 - P * cell
    Ne = n + 2 * P
    h, cnt = tm.cell_heights(x, y, z, gx0, gy0, cell, Ne, Ne)
    hf, gap = tm.fill_holes(h, cell)
    meas = np.isfinite(h)
    info = {"candidate_edges": 0, "step_edges": 0}
    sl = (slice(P, P + n), slice(P, P + n))
    near = [w["coords"] for w in walls
            if min(c[0] for c in w["coords"]) < x1 + 1 and max(c[0] for c in w["coords"]) > x0 - 1
            and min(c[1] for c in w["coords"]) < y1 + 1 and max(c[1] for c in w["coords"]) > y0 - 1]
    if cell == 0.5:
        # campus tiles: breaks found in the points, plus the mapped retaining walls
        bx, by, hs, info = tm.detect_breaks(x, y, z, hf, meas, gx0, gy0, cell, (P, P + n, P, P + n))
        fx, fy = tm.force_breaks(near, hs[sl], x0, y0, cell)
        bx, by = bx | fx, by | fy
        bx, by, dropped = tm.clean_breaks(bx, by, fx, fy)
    else:
        # ring tiles (2 m): only the mapped retaining walls make breaks
        hs = hf
        fx, fy = tm.force_breaks(near, hs[sl], x0, y0, cell)
        bx, by = fx.copy(), fy.copy()
        dropped = 0
    # light 3 x 3 denoise; the cells beside each break keep their sharp values
    keep = np.zeros_like(hs, bool)
    keep[sl] = tm.break_sides(bx, by)
    hs = tm.denoise(hs, cnt, keep, passes=DENOISE_PASSES[kind_of(t)])
    nodes = tm.node_heights(hs, P, n).astype(np.float32)
    tile_cnt = cnt[sl]
    tile_gap = gap[sl]
    holes = np.where(tile_cnt > 0, 0, np.where(tile_gap <= 1.0, 1, 2)).astype(np.uint8)
    stats = {
        "ground_points_tile": int(in_tile.sum()), "ground_points_used": int(x.size),
        "ground_density_per_m2": round(float(in_tile.sum()) / 1e4, 3),
        "spikes_dropped_tile": spikes_tile,
        "empty_cell_fraction": round(float((tile_cnt == 0).mean()), 4),
        "hole_fraction": round(float((holes == 2).mean()), 4),
        "hole_rule": "hole = cell more than 1 m from any ground point (filled by TIN, then nearest)",
        "break_candidates": info["candidate_edges"], "break_step_edges": info["step_edges"],
        "break_forced_edges": int(fx.sum() + fy.sum()), "break_small_dropped": dropped,
        "break_edges": int(bx.sum() + by.sum())}
    return {"nodes": nodes, "hc": hs[sl], "count": tile_cnt, "holes": holes, "bx": bx, "by": by,
            "fx": fx, "fy": fy, "stats": stats}


# ---- tile borders ---------------------------------------------------------------

class TileSet:
    """All terrain tiles: cell sizes and (lazily loaded) node heights."""

    def __init__(self, tiles, root, version):
        self.by_cr = {(t["col"], t["row"]): t for t in tiles}
        self.root, self.version = root, version
        self._h = {}

    def heights(self, t):
        if t["name"] not in self._h:
            p = tdir(self.root, self.version, t["name"]) / "heights.npy"
            if not p.exists():
                raise RuntimeError(f"{t['name']} has no heights.npy yet (phase 1)")
            self._h[t["name"]] = np.load(p)
        return self._h[t["name"]]

    def owner(self, X, Y):
        """Tile whose heights define the node at (X, Y): the floor tile, else the next existing one."""
        cols = {math.floor(X / 100)}
        rows = {math.floor(Y / 100)}
        if X % 100 == 0:
            cols.add(int(X // 100) - 1)
        if Y % 100 == 0:
            rows.add(int(Y // 100) - 1)
        for c in sorted(cols, reverse=True):
            for r in sorted(rows, reverse=True):
                if (c, r) in self.by_cr:
                    return self.by_cr[(c, r)]
        return None

    def node_z(self, X, Y):
        t = self.owner(X, Y)
        c = cell_size(t)
        H = self.heights(t)
        I = int(round((X - t["bounds"][0]) / c))
        J = int(round((Y - t["bounds"][1]) / c))
        return float(H[J, I])

    def border(self, t, tol, step=None):
        """(border_z, border_keep) arrays for a tile's nodes, from the shared edge rule.
        step: grid spacing used by EVERY tile for this mesh kind (the far meshes use one
        4 m grid everywhere); None = each tile's own height grid."""
        cell = step or cell_size(t)
        n = int(round(100 / cell))
        x0, y0 = t["bounds"][0], t["bounds"][1]
        bz = np.full((n + 1, n + 1), np.nan)
        bk = np.zeros((n + 1, n + 1), bool)
        c, r = t["col"], t["row"]
        sides = {"S": (c, r - 1), "N": (c, r + 1), "W": (c - 1, r), "E": (c + 1, r)}
        for side, nb in sides.items():
            nbt = self.by_cr.get(nb)
            if step is not None:
                es = step
            else:
                es = max(cell, cell_size(nbt)) if nbt else cell
            m = int(round(100 / es))
            s = np.arange(m + 1) * es
            if side in ("S", "N"):
                Y = y0 if side == "S" else y0 + 100
                zc = np.array([self.node_z(x0 + v, Y) for v in s])
            else:
                X = x0 if side == "W" else x0 + 100
                zc = np.array([self.node_z(X, y0 + v) for v in s])
            keep = tm.simplify_profile(zc, tol)
            sf = np.arange(n + 1) * cell
            zf = np.interp(sf, s[keep], zc[keep])
            kf = np.zeros(n + 1, bool)
            kf[np.round(s[keep] / cell).astype(int)] = True
            if side == "S":
                bz[0, :], bk[0, :] = zf, kf
            elif side == "N":
                bz[n, :], bk[n, :] = zf, kf
            elif side == "W":
                bz[:, 0], bk[:, 0] = zf, kf
            else:
                bz[:, n], bk[:, n] = zf, kf
        return bz, bk


# ---- phase 2: mesh ---------------------------------------------------------------

def cell_materials(surface, n):
    """Majority material of the 0.25 m pixels in each cell, then single-cell specks removed."""
    f = surface.shape[0] // n
    blocks = surface.reshape(n, f, n, f).transpose(0, 2, 1, 3).reshape(n, n, f * f)
    ids = np.unique(blocks)
    counts = np.stack([(blocks == k).sum(axis=2) for k in ids], axis=2)
    mat = ids[np.argmax(counts, axis=2)].astype(np.int16)
    # a cell whose 4 neighbors all agree on another material takes it
    p = np.pad(mat, 1, mode="edge")
    nb = np.stack([p[:-2, 1:-1], p[2:, 1:-1], p[1:-1, :-2], p[1:-1, 2:]])
    same = (nb == nb[0]).all(axis=0) & (nb[0] != mat)
    mat[same] = nb[0][same]
    return mat


def face_areas(verts, faces):
    P = verts[faces]
    return 0.5 * np.linalg.norm(np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]), axis=1)


def mesh_tile(name, t, tiles, root, version):
    t0 = time.time()
    d = tdir(root, version, name)
    cz = np.load(d / "cells.npz")
    hc, bx, by = cz["hc"].astype(np.float64), cz["bx"], cz["by"]
    gstats = json.loads(str(cz["stats"]))
    n = hc.shape[0]
    sp = d / "surface.npy"
    if not sp.exists():
        raise RuntimeError(f"{sp} missing; run surfaces.py first")
    mat = cell_materials(np.load(sp), n)
    ids = vcmesh.material_index()
    ts = TileSet(tiles, root, version)
    vis, col = meshes_from_cells(hc, bx, by, cz["holes"] == 2, mat, t, ts, ids)
    return finish_tile(name, t, d, vis, col, gstats, ids, root, version, t0)


def meshes_from_cells(hc, bx, by, hole, mat, t, ts, ids):
    """Visual and collider meshes for one tile (borders from the shared TileSet rule)."""
    cell = cell_size(t)
    kind = kind_of(t)
    x0, y0 = t["bounds"][0], t["bounds"][1]
    bz, bk = ts.border(t, VIS_BORDER_TOL)
    for tol in VIS_TOL[kind]:
        vis = tm.build_mesh(hc, x0, y0, cell, bx, by, mat, bz, bk, tol=tol, mat_ids=ids,
                            hole=hole, hole_tol=HOLE_TOL["visual"])
        if vis["stats"]["triangles"] <= VIS_BUDGET[kind]:
            break
    cbz, cbk = ts.border(t, COL_BORDER_TOL)
    col = tm.build_mesh(hc, x0, y0, cell, bx, by, mat, cbz, cbk, tol=COL_TOL, merge_materials=True,
                        uv_strip=False, mat_ids=ids, hole=hole, hole_tol=HOLE_TOL["collider"])
    return vis, col


def finish_tile(name, t, d, vis, col, gstats, ids, root, version, t0):
    """Accuracy self-check, save both meshes and tile.json."""
    cell = cell_size(t)
    kind = kind_of(t)
    x0, y0 = t["bounds"][0], t["bounds"][1]
    # accuracy against every ground point in the tile (spikes included)
    pts = lidar_io.load_points(x0, y0, x0 + 100, y0 + 100, classes=[2], root=root)
    acc = {}
    for label, m in (("visual", vis), ("collider", col)):
        zm = tm.mesh_z_at(m["verts"], m["faces"], pts["x"], pts["y"], x0, y0)
        acc[label] = tm.error_stats(pts["z"] - zm)

    names = {v: k for k, v in ids.items()}
    areas = {}
    fa = face_areas(vis["verts"], vis["faces"])
    for k in np.unique(vis["face_mat"]):
        areas[names[int(k)]] = round(float(fa[vis["face_mat"] == k].sum()), 1)
    problems = {"visual": vcmesh.check_mesh(vis), "collider": vcmesh.check_mesh(col)}
    meta_common = {"tile": name, "version": version, "frame": "Vanderbilt campus frame v1",
                   "source": "terrain.py from derived/lidar/v1 class 2 + derived/terrain/v1 surfaces",
                   "cell_m": cell, "bounds": t["bounds"]}
    vcmesh.save_mesh(d / "terrain.npz", vis["verts"], vis["faces"], vis["face_mat"], vis["uv0"],
                     {**meta_common, "kind": "visual", **vis["stats"]})
    vcmesh.save_mesh(d / "terrain_col.npz", col["verts"], col["faces"], col["face_mat"], col["uv0"],
                     {**meta_common, "kind": "collider", "uv_note": "no UV requirements (collider)",
                      **col["stats"]})
    info = {"tile": name, "zone": t["zone"], "kind": kind, "cell_m": cell, "bounds": t["bounds"],
            "campus_fraction": t["campus_fraction"],
            "visual": {k: vis["stats"][k] for k in ("triangles", "ground_triangles", "break_triangles", "tol_m",
                                                    "uv_m_per_unit", "uv_break_scale")},
            "visual_budget": VIS_BUDGET[kind],
            "collider": {k: col["stats"][k] for k in ("triangles", "ground_triangles", "break_triangles", "tol_m")},
            "collider_budget": COL_BUDGET,
            "hole_tol_m": HOLE_TOL, "denoise_passes": DENOISE_PASSES[kind],
            "breaks": {"edges": vis["stats"]["break_edges"], "length_m": vis["stats"]["break_length_m"],
                       "face_area_m2": vis["stats"]["break_face_area_m2"], "max_m": vis["stats"]["max_break_m"],
                       "forced_by_osm_edges": gstats["break_forced_edges"],
                       "step_test_edges": gstats["break_step_edges"],
                       "candidates": gstats["break_candidates"], "short_dropped": gstats["break_small_dropped"]},
            "ground": {k: gstats[k] for k in ("ground_points_tile", "ground_density_per_m2", "spikes_dropped_tile",
                                              "empty_cell_fraction", "hole_fraction", "hole_rule")},
            "material_area_m2": areas,
            "accuracy": acc,
            "border_rule": f"owner tile node heights, coarser grid of the two tiles, Douglas-Peucker "
                           f"{VIS_BORDER_TOL} m visual / {COL_BORDER_TOL} m collider",
            "mesh_problems": problems,
            "seconds": {"grid": gstats["grid_seconds"], "mesh": round(time.time() - t0, 2)}}
    save_json(d / "tile.json", info)
    return [d / "terrain.npz", d / "terrain_col.npz", d / "tile.json"], info


# ---- far meshes (seen from 250 m and more) ----------------------------------------

def far_heights(H, cell):
    """Coarse cell heights for the far grid: mean of the tile's height nodes in each 4 m cell."""
    k = int(round(FAR_CELL / cell))
    m = int(round(100 / FAR_CELL))
    out = np.empty((m, m))
    for j in range(m):
        for i in range(m):
            out[j, i] = H[j * k:j * k + k + 1, i * k:i * k + k + 1].mean()
    return out


def far_mesh(t, ts, H, mat_far, ids):
    """Far mesh of one tile: 4 m grid, no breaks, up to FAR_TOL vertical error, materials kept
    where the budget allows (else each merged rectangle takes its majority material), shared
    edges at 4 m with the Douglas-Peucker rule, and a 1.5 m skirt around the border."""
    x0, y0 = t["bounds"][0], t["bounds"][1]
    hc = far_heights(H, cell_size(t))
    bz, bk = ts.border(t, FAR_TOL, step=FAR_CELL)
    budget = FAR_BUDGET[kind_of(t)]
    best = None
    # accuracy first: a 0.25 m fit with majority materials beats a 0.5 m fit with exact ones
    for tol, merge in ((FAR_TOL / 2, False), (FAR_TOL / 2, True), (FAR_TOL, False), (FAR_TOL, True)):
        m = tm.build_mesh(hc, x0, y0, FAR_CELL, mat=mat_far, border_z=bz, border_keep=bk, tol=tol,
                          merge_materials=merge, majority_mat=merge, uv_strip=False, mat_ids=ids)
        m = tm.add_skirt(m, x0, y0, depth=FAR_SKIRT)
        m["stats"].update({"tol_m": tol, "materials": "majority per merged rectangle" if merge
                           else "4 m cells, one material per rectangle"})
        best = m
        if len(m["faces"]) <= budget:
            break
    return best          # the last try is returned even if it is still over budget (tile.json says so)


def far_tile(name, t, tiles, root, version):
    t0 = time.time()
    d = tdir(root, version, name)
    H = np.load(d / "heights.npy").astype(np.float64)
    mat_far = cell_materials(np.load(d / "surface.npy"), int(round(100 / FAR_CELL)))
    ids = vcmesh.material_index()
    ts = TileSet(tiles, root, version)
    m = far_mesh(t, ts, H, mat_far, ids)
    x0, y0 = t["bounds"][0], t["bounds"][1]
    pts = lidar_io.load_points(x0, y0, x0 + 100, y0 + 100, classes=[2], root=root)
    zm = tm.mesh_z_at(m["verts"], m["faces"], pts["x"], pts["y"], x0, y0)
    acc = tm.error_stats(pts["z"] - zm)
    k = m["kind"]
    info = {"triangles": int(len(m["faces"])), "ground_triangles": int((k == 0).sum()),
            "skirt_triangles": int((k == 2).sum()), "budget": FAR_BUDGET[kind_of(t)],
            "cell_m": FAR_CELL, "tol_m": m["stats"]["tol_m"], "materials": m["stats"]["materials"],
            "skirt_depth_m": FAR_SKIRT, "breaks": "none (all breaks dropped in the far mesh)",
            "accuracy": acc, "mesh_problems": vcmesh.check_mesh(m),
            "border_rule": f"owner tile node heights sampled every {FAR_CELL} m, Douglas-Peucker {FAR_TOL} m",
            "seconds": round(time.time() - t0, 2)}
    vcmesh.save_mesh(d / "terrain_far.npz", m["verts"], m["faces"], m["face_mat"], m["uv0"],
                     {"tile": name, "version": version, "kind": "far", "frame": "Vanderbilt campus frame v1",
                      "uv_m_per_unit": round(100 / tm.UV_SCALE, 6), "uv_note": "planar; skirt faces planar too",
                      **{k2: v for k2, v in info.items() if k2 not in ("accuracy", "mesh_problems")}})
    tj = load_json(d / "tile.json")
    tj["far"] = info
    save_json(d / "tile.json", tj)
    return [d / "terrain_far.npz", d / "tile.json"], info


# ---- workers (top level so Windows can pickle them) ------------------------------

def _far_job(args):
    name, t, tiles, root, version = args
    try:
        files, info = far_tile(name, t, tiles, root, version)
        return name, [str(f) for f in files], info, None
    except Exception:
        return name, [], None, traceback.format_exc()


def _grid_job(args):
    name, t, walls, root, version = args
    try:
        files, stats = grid_tile(name, t, walls, root, version)
        return name, [str(f) for f in files], stats, None
    except Exception:
        return name, [], None, traceback.format_exc()


def _mesh_job(args):
    name, t, tiles, root, version = args
    try:
        files, info = mesh_tile(name, t, tiles, root, version)
        return name, [str(f) for f in files], info, None
    except Exception:
        return name, [], None, traceback.format_exc()


def run_jobs(fn, jobs, n_workers, label, root):
    """Run jobs in parallel; record written files in the manifest. Returns (results, errors)."""
    results, errors = {}, {}
    if not jobs:
        return results, errors
    db = manifest.connect(root)
    done = 0
    t0 = time.time()

    def record(name, files, info, err):
        nonlocal done
        done += 1
        if err:
            errors[name] = err
            print(f"[{label}] {name} FAILED\n{err}", flush=True)
            return
        results[name] = info
        for f in files:
            manifest.add_file(f, f"terrain.py {label}", LICENSE, stage=1, root=root, db=db)
        if done % 20 == 0 or done == len(jobs):
            db.commit()
            print(f"[{label}] {done}/{len(jobs)} tiles, {time.time() - t0:.0f} s", flush=True)

    if n_workers <= 1:
        for j in jobs:
            record(*fn(j))
    else:
        with ProcessPoolExecutor(max_workers=n_workers) as ex:
            for fut in as_completed([ex.submit(fn, j) for j in jobs]):
                record(*fut.result())
    db.commit()
    db.close()
    return results, errors


def neighbors(t, by_cr):
    out = []
    for dc in (-1, 0, 1):
        for dr in (-1, 0, 1):
            if (dc or dr) and (t["col"] + dc, t["row"] + dr) in by_cr:
                out.append(by_cr[(t["col"] + dc, t["row"] + dr)])
    return out


def summarize(root, version, tiles):
    """index.json over all finished tiles + reports/stage1/terrain_build.json."""
    rows = []
    for t in tiles:
        p = tdir(root, version, t["name"]) / "tile.json"
        if p.exists():
            rows.append(load_json(p))
    tot = lambda key, sub: int(sum(r[key][sub] for r in rows))
    summary = {
        "made": time.strftime("%Y-%m-%dT%H:%M:%S"), "version": version, "tiles": len(rows),
        "tiles_campus_0.5m": sum(r["kind"] == "campus" for r in rows),
        "tiles_ring_2m": sum(r["kind"] == "ring" for r in rows),
        "triangles_visual": tot("visual", "triangles"), "triangles_collider": tot("collider", "triangles"),
        "break_edges": tot("breaks", "edges"),
        "break_length_m": round(sum(r["breaks"]["length_m"] for r in rows), 1),
        "break_forced_by_osm_edges": tot("breaks", "forced_by_osm_edges"),
        "visual_over_budget": [r["tile"] for r in rows if r["visual"]["triangles"] > r["visual_budget"]],
        "collider_over_budget": [r["tile"] for r in rows if r["collider"]["triangles"] > r["collider_budget"]],
        "visual_tol_raised": {r["tile"]: r["visual"]["tol_m"] for r in rows if r["visual"]["tol_m"] > 0.03},
        "mesh_problems": {r["tile"]: r["mesh_problems"] for r in rows
                          if r["mesh_problems"]["visual"] or r["mesh_problems"]["collider"]},
        "spikes_dropped": int(sum(r["ground"]["spikes_dropped_tile"] for r in rows)),
        "low_ground_density_tiles": [r["tile"] for r in rows if r["ground"]["ground_density_per_m2"] < 0.2],
        "material_area_m2": {},
        "seconds_cpu": round(sum(r["seconds"]["grid"] + r["seconds"]["mesh"] for r in rows), 1),
    }
    for r in rows:
        for k, v in r["material_area_m2"].items():
            summary["material_area_m2"][k] = round(summary["material_area_m2"].get(k, 0) + v, 1)
    far = [r for r in rows if "far" in r]
    if far:
        def dist(vals):
            a = np.array(vals, float)
            return {"median": round(float(np.median(a)), 3), "p90": round(float(np.percentile(a, 90)), 3),
                    "max": round(float(a.max()), 3)}
        summary["far"] = {
            "tiles": len(far), "cell_m": FAR_CELL, "tol_m": FAR_TOL, "skirt_m": FAR_SKIRT, "budget": FAR_BUDGET,
            "triangles_total": int(sum(r["far"]["triangles"] for r in far)),
            "triangles_campus_total": int(sum(r["far"]["triangles"] for r in far if r["kind"] == "campus")),
            "triangles_ring_total": int(sum(r["far"]["triangles"] for r in far if r["kind"] == "ring")),
            "triangles_per_tile_campus": dist([r["far"]["triangles"] for r in far if r["kind"] == "campus"] or [0]),
            "triangles_per_tile_ring": dist([r["far"]["triangles"] for r in far if r["kind"] == "ring"] or [0]),
            "over_budget": [r["tile"] for r in far if r["far"]["triangles"] > r["far"]["budget"]],
            "majority_material_tiles": sum("majority" in r["far"]["materials"] for r in far),
            "tile_p95_error_m": dist([r["far"]["accuracy"].get("p95_m", 0) for r in far]),
            "tile_max_error_m": dist([r["far"]["accuracy"].get("max_m", 0) for r in far]),
            "tile_median_error_m": dist([r["far"]["accuracy"].get("median_m", 0) for r in far]),
            "tiles_p95_over_0.5m": [r["tile"] for r in far if r["far"]["accuracy"].get("p95_m", 0) > 0.5],
            "mesh_problems": {r["tile"]: r["far"]["mesh_problems"] for r in far if r["far"]["mesh_problems"]},
        }
    idx = {"version": version, "made": summary["made"],
           "tiles": [{"tile": r["tile"], "kind": r["kind"], "cell_m": r["cell_m"], "bounds": r["bounds"],
                      "triangles_visual": r["visual"]["triangles"], "triangles_collider": r["collider"]["triangles"],
                      "break_edges": r["breaks"]["edges"], "triangles_far": r.get("far", {}).get("triangles"),
                      "files": ["terrain.npz", "terrain_col.npz", "terrain_far.npz", "heights.npy",
                                                                     "tile.json"]} for r in rows]}
    ip = Path(root) / "derived" / "terrain" / version / "index.json"
    rp = Path(root) / "reports" / "stage1" / "terrain_build.json"
    save_json(ip, idx)
    save_json(rp, summary)
    db = manifest.connect(root)
    manifest.add_file(ip, "terrain.py", LICENSE, stage=1, root=root, db=db)
    manifest.add_file(rp, "terrain.py", "", stage=1, root=root, db=db)
    db.commit()
    db.close()
    return summary


def far_only(args, root):
    """Build terrain_far.npz for the chosen tiles (skips finished ones unless --force)."""
    t_start = time.time()
    tiles = [t for t in lidar_io.load_index(root=root)["tiles"] if t["zone"] in ("campus", "ring")]
    by_name = {t["name"]: t for t in tiles}
    targets = tiles if args.all else [by_name[n] for n in args.tile]
    missing = [t["name"] for t in tiles if not (tdir(root, args.version, t["name"]) / "heights.npy").exists()]
    if missing:
        fail(f"{len(missing)} tiles have no heights.npy yet (run terrain.py first): {missing[:5]}")
    jobs = [(t["name"], t, tiles, str(root), args.version) for t in targets
            if (tdir(root, args.version, t["name"]) / "tile.json").exists()
            and (args.force or not (tdir(root, args.version, t["name"]) / "terrain_far.npz").exists())]
    _, ferr = run_jobs(_far_job, jobs, args.jobs, "far", root)
    summary = summarize(root, args.version, tiles)
    print(json.dumps(summary.get("far", {}), indent=2)[:3000])
    print(f"done in {time.time() - t_start:.0f} s wall")
    if ferr:
        fail(f"{len(ferr)} far mesh failures: {sorted(ferr)}")


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    g = p.add_mutually_exclusive_group(required=True)
    g.add_argument("--tile", action="append", help="tile name (repeatable), e.g. T_00_00")
    g.add_argument("--all", action="store_true", help="every campus + ring tile")
    p.add_argument("--jobs", type=int, default=1, help="parallel worker processes")
    p.add_argument("--force", action="store_true", help="rebuild finished tiles")
    p.add_argument("--version", default="v1", help="output folder version (default v1)")
    p.add_argument("--far-only", action="store_true",
                   help="only (re)build the far meshes terrain_far.npz; the visual and collider meshes "
                        "are not touched (allowed on an ACCEPTED version: the far mesh is an added file)")
    args = p.parse_args(argv)
    root = data_root()
    t_start = time.time()
    vdir = Path(root) / "derived" / "terrain" / args.version
    if args.far_only:
        return far_only(args, root)
    if args.force and (vdir / "ACCEPTED").exists():
        fail(f"{vdir} is marked ACCEPTED; write new geometry to a new --version (e.g. v2)")
    tiles = [t for t in lidar_io.load_index(root=root)["tiles"] if t["zone"] in ("campus", "ring")]
    by_name = {t["name"]: t for t in tiles}
    by_cr = {(t["col"], t["row"]): t for t in tiles}
    if args.all:
        targets = tiles
    else:
        bad = [n for n in args.tile if n not in by_name]
        if bad:
            fail(f"not campus/ring tiles: {bad}")
        targets = [by_name[n] for n in args.tile]
    # surfaces are shared input; copy-free check
    for t in targets:
        if not (vdir / t["name"] / "surface.npy").exists() and args.version == "v1":
            fail(f"{t['name']}: surface.npy missing; run surfaces.py first")
    if args.version != "v1":
        # a new version reuses the v1 surface rasters
        import shutil
        for t in tiles:
            src = Path(root) / "derived" / "terrain" / "v1" / t["name"] / "surface.npy"
            dst = vdir / t["name"] / "surface.npy"
            if src.exists() and not dst.exists():
                dst.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(src, dst)

    walls = retaining_walls(root)
    print(f"{len(targets)} target tiles; {len(walls)} OSM retaining walls")

    # phase 1: targets (and, for mesh borders, their neighbors) need heights
    need_grid = {t["name"] for t in targets}
    for t in targets:
        need_grid |= {nb["name"] for nb in neighbors(t, by_cr)}
    grid_jobs = []
    for nme in sorted(need_grid):
        d = tdir(root, args.version, nme)
        done = (d / "heights.npy").exists() and (d / "cells.npz").exists()
        is_target = any(t["name"] == nme for t in targets)
        if not done or (args.force and is_target):
            grid_jobs.append((nme, by_name[nme], walls, str(root), args.version))
    _, gerr = run_jobs(_grid_job, grid_jobs, args.jobs, "grid", root)
    regridded = {j[0] for j in grid_jobs} - set(gerr)

    # phase 2: mesh targets (and finished neighbors whose shared border may have changed)
    mesh_names = set()
    for t in targets:
        if args.force or not (tdir(root, args.version, t["name"]) / "tile.json").exists():
            mesh_names.add(t["name"])
    for nme in regridded:
        for nb in neighbors(by_name[nme], by_cr) + [by_name[nme]]:
            if (tdir(root, args.version, nb["name"]) / "tile.json").exists():
                mesh_names.add(nb["name"])
    mesh_jobs = [(nme, by_name[nme], tiles, str(root), args.version) for nme in sorted(mesh_names)
                 if nme not in gerr]
    _, merr = run_jobs(_mesh_job, mesh_jobs, args.jobs, "mesh", root)

    # phase 3: far meshes (tiles without one, tiles just meshed and their neighbors, or --force)
    far_names = set()
    for t in targets:
        if args.force or not (tdir(root, args.version, t["name"]) / "terrain_far.npz").exists():
            far_names.add(t["name"])
    for nme in regridded:
        far_names |= {nb["name"] for nb in neighbors(by_name[nme], by_cr)} | {nme}
    far_jobs = [(nme, by_name[nme], tiles, str(root), args.version) for nme in sorted(far_names)
                if nme not in merr and nme not in gerr
                and (tdir(root, args.version, nme) / "tile.json").exists()]
    _, ferr = run_jobs(_far_job, far_jobs, args.jobs, "far", root)
    merr = {**merr, **ferr}

    summary = summarize(root, args.version, tiles)
    print(json.dumps({k: v for k, v in summary.items() if k not in ("material_area_m2",)}, indent=2)[:4000])
    print(f"done in {time.time() - t_start:.0f} s wall")
    if gerr or merr:
        fail(f"{len(gerr)} grid and {len(merr)} mesh failures: {sorted(set(gerr) | set(merr))}")


if __name__ == "__main__":
    main()
