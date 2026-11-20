"""Stage 1 buildings v2: hybrid LoD2 (v1 roof planes where they pass, LiDAR height field elsewhere)
and one exact non-convex collider mesh per building.

Needs the v1 run (buildings_footprints.py, buildings_lod2.py, buildings_check.py): the v1 roof
planes are used to snap the height field, and the v1 accuracy result decides the method:
  - a building that passes the v1 gate keeps its v1 mesh ("planes_v1");
  - every other building gets the height-field roof (roof_heightfield.py, "heightfield_v2");
  - hero buildings (config/hero_spots.json, Kirkland Hall first) take whichever method has the
    smaller roof-surface error (median and p95 vertical distance of the roof points to the mesh);
  - buildings with no LiDAR roof (v1 flat fallback) keep v1: there is nothing to follow.

Per building it writes buildings/<id>/v2/ in the archive:
  lod2.npz      LoD2 mesh (watertight, UV0 charts = face groups with normals within 15 deg,
                each projected in its own plane at true scale; walls one chart per straight run)
  lod1.npz      copy of v1's LoD1
  col_mesh.npz  one non-convex collider: the LoD2 shell without the base cap, vertices merged at
                1 mm, no degenerate/duplicate triangles, simplified (open3d quadric decimation) to
                <= 0.15 m deviation and about <= 2000 triangles
  roof_planes.json, ledger.json, model.json (version 2, roof_method, parts, accuracy inputs)
plus git mirrors of model.json and ledger.json, and derived/buildings/v2/index.json (every campus
building: id, tile, version folder to export, footprint, holes, name, roof_method).

  python pipeline/buildings_v2.py --jobs 14 [--force] [--only B_544397715 ...]
"""
import argparse
import math
import shutil
import sys
import time
import traceback
from multiprocessing import Pool
from pathlib import Path

import numpy as np
import shapely
from shapely.strtree import STRtree

sys.path.insert(0, str(Path(__file__).resolve().parent))
import buildings_common as bc  # noqa: E402
import buildings_lod2 as bl  # noqa: E402
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
import roof_fit as rf  # noqa: E402
import roof_heightfield as rh  # noqa: E402
import roof_mesh as rm  # noqa: E402
import vcmesh  # noqa: E402
from common import CONFIG, data_root, fail, load_json  # noqa: E402

V2 = "v2"
CHART_DEG = 15.0
COL_DEV, COL_TRIS = 0.15, 2000


def v2_dir(bid, root=None):
    return bc.building_dir(bid, root, V2)


def index_path(root=None):
    return Path(root or data_root()) / "derived" / "buildings" / V2 / "index.json"


def hero_ids():
    ids = []
    for s in load_json(CONFIG / "hero_spots.json")["spots"]:
        m = s.get("match") or {}
        if m.get("building_id"):
            ids.append(m["building_id"])
    return ids


# ---- mesh helpers -------------------------------------------------------------------

class MeshZ:
    """Top surface height of a mesh at (x, y): the highest upward-facing triangle above each point
    (triangles binned on a 2 m grid, exact barycentric test). NaN where no triangle is above."""

    def __init__(self, mesh, bin_m=2.0):
        v = np.asarray(mesh["verts"], float)
        f = np.asarray(mesh["faces"])
        T = v[f]
        n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
        # roof faces only: walls (also walls tilted a hair by vertex snapping, whose 2D shadow is
        # a thin sliver reaching up to the wall top) and the base cap are never "above"
        nz = n[:, 2] / np.maximum(np.linalg.norm(n, axis=1), 1e-12)
        up = nz > 0.05
        self.T = T[up]
        self.bin = bin_m
        lo = self.T[:, :, :2].min(axis=1)
        hi = self.T[:, :, :2].max(axis=1)
        self.o = lo.min(axis=0) - 1.0 if len(lo) else np.zeros(2)
        b0 = np.floor((lo - self.o) / bin_m).astype(np.int64)
        b1 = np.floor((hi - self.o) / bin_m).astype(np.int64)
        self.nb = (b1.max(axis=0) + 2) if len(b1) else np.array([1, 1])
        ids, keys = [], []
        for t in range(len(self.T)):
            xs = np.arange(b0[t, 0], b1[t, 0] + 1)
            ys = np.arange(b0[t, 1], b1[t, 1] + 1)
            X, Y = np.meshgrid(xs, ys)
            keys.append((Y * self.nb[0] + X).ravel())
            ids.append(np.full(X.size, t))
        keys = np.concatenate(keys) if keys else np.zeros(0, np.int64)
        ids = np.concatenate(ids) if ids else np.zeros(0, np.int64)
        order = np.argsort(keys, kind="stable")
        self.keys, self.ids = keys[order], ids[order]

    def __call__(self, x, y):
        x, y = np.asarray(x, float).ravel(), np.asarray(y, float).ravel()
        out = np.full(len(x), np.nan)
        if not len(x) or not len(self.T):
            return out
        bx = np.floor((x - self.o[0]) / self.bin).astype(np.int64)
        by = np.floor((y - self.o[1]) / self.bin).astype(np.int64)
        ok = (bx >= 0) & (by >= 0) & (bx < self.nb[0]) & (by < self.nb[1])
        key = np.where(ok, by * self.nb[0] + bx, -1)
        s0 = np.searchsorted(self.keys, key, "left")
        s1 = np.searchsorted(self.keys, key, "right")
        cnt = np.where(ok, s1 - s0, 0)
        q = np.repeat(np.arange(len(x)), cnt)
        if not len(q):
            return out
        first = np.repeat(s0, cnt)
        within = np.arange(len(q)) - np.repeat(np.cumsum(cnt) - cnt, cnt)
        t = self.ids[first + within]
        A, B, C = self.T[t, 0], self.T[t, 1], self.T[t, 2]
        px, py = x[q], y[q]
        det = (B[:, 1] - C[:, 1]) * (A[:, 0] - C[:, 0]) + (C[:, 0] - B[:, 0]) * (A[:, 1] - C[:, 1])
        l1 = ((B[:, 1] - C[:, 1]) * (px - C[:, 0]) + (C[:, 0] - B[:, 0]) * (py - C[:, 1])) / det
        l2 = ((C[:, 1] - A[:, 1]) * (px - C[:, 0]) + (A[:, 0] - C[:, 0]) * (py - C[:, 1])) / det
        l3 = 1 - l1 - l2
        e = -1e-9
        inside = (l1 >= e) & (l2 >= e) & (l3 >= e)
        z = l1 * A[:, 2] + l2 * B[:, 2] + l3 * C[:, 2]
        z = np.where(inside, z, -np.inf)
        best = np.full(len(x), -np.inf)
        np.maximum.at(best, q, z)
        out[np.isfinite(best)] = best[np.isfinite(best)]
        return out


def face_normals(v, f):
    n = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    a = np.linalg.norm(n, axis=1)
    return n / np.maximum(a, 1e-12)[:, None], a / 2


def uv_by_normals(verts, faces, max_deg=CHART_DEG, padding=0.05):
    """UV0 charts: connected faces (sharing an edge) whose normals are within max_deg of the
    chart's first (largest) face, each projected in its own best-fit plane at true scale, packed
    with vcmesh.pack_charts. A chart whose projection would fold onto itself is split into single
    faces. Returns (uv0, side_m, charts, check) where check is the exact overlap test result."""
    n, area = face_normals(verts, faces)
    nf = len(faces)
    edges = {}
    for fi, t in enumerate(faces):
        for k in range(3):
            e = (min(t[k], t[(k + 1) % 3]), max(t[k], t[(k + 1) % 3]))
            edges.setdefault(e, []).append(fi)
    nbr = [[] for _ in range(nf)]
    for fs in edges.values():
        if len(fs) == 2:
            a, b = fs
            nbr[a].append(b)
            nbr[b].append(a)
    cosmax = math.cos(math.radians(max_deg))
    chart = np.full(nf, -1)
    charts = []
    for seed in np.argsort(-area):
        if chart[seed] >= 0:
            continue
        cid = len(charts)
        chart[seed] = cid
        members, stack = [seed], [seed]
        ns = n[seed]
        while stack:
            f0 = stack.pop()
            for g in nbr[f0]:
                if chart[g] < 0 and n[g] @ ns >= cosmax:
                    chart[g] = cid
                    members.append(g)
                    stack.append(g)
        charts.append(members)
    # project, check folding, split folded charts into single faces
    final = []
    for members in charts:
        uv = project(verts, faces[members], n[members], area[members])
        if len(members) > 1 and folds(uv):
            final += [[m] for m in members]
        else:
            final.append(members)
    local, sizes = [], []
    for members in final:
        uv = project(verts, faces[members], n[members], area[members])
        mn = uv.reshape(-1, 2).min(axis=0)
        uv = uv - mn
        mx = uv.reshape(-1, 2).max(axis=0)
        local.append(uv)
        sizes.append((max(mx[0], 1e-3), max(mx[1], 1e-3)))
    offsets, side = vcmesh.pack_charts(sizes, padding)
    uv0 = np.zeros((nf, 3, 2), np.float32)
    for members, uv, off in zip(final, local, offsets):
        uv0[members] = (uv + np.asarray(off)) / side
    check = rect_overlap_check(offsets, sizes, side)
    return np.clip(uv0, 0, 1), float(side), len(final), check


def project(verts, F, N, A):
    nsum = (N * A[:, None]).sum(axis=0)
    if np.linalg.norm(nsum) < 1e-9:
        nsum = N[0]
    e1, e2 = rm._chart_frame(None, nsum)
    P = verts[F]
    return np.stack([P @ e1, P @ e2], axis=-1)


def folds(uv):
    """True when the projected triangles overlap each other (or flip): exact polygon test."""
    tris = [shapely.Polygon(t) for t in uv]
    s = sum(t.area for t in tris)
    if s <= 0:
        return True
    # flipped triangles (wrong winding in the chart frame) mean the chart folds over
    d = (uv[:, 1, 0] - uv[:, 0, 0]) * (uv[:, 2, 1] - uv[:, 0, 1]) - (uv[:, 2, 0] - uv[:, 0, 0]) * (uv[:, 1, 1] - uv[:, 0, 1])
    if (d < -1e-9).any() and (d > 1e-9).any():
        return True
    u = shapely.unary_union(tris).area
    return u < s * (1 - 1e-6) - 1e-9


def rect_overlap_check(offsets, sizes, side):
    """Exact check of the packing: every chart rectangle inside the atlas, no two overlapping."""
    R = np.array([(o[0], o[1], o[0] + w, o[1] + h) for o, (w, h) in zip(offsets, sizes)])
    inside = bool((R[:, 0] >= -1e-9).all() and (R[:, 1] >= -1e-9).all() and (R[:, 2] <= side + 1e-9).all()
                  and (R[:, 3] <= side + 1e-9).all())
    order = np.argsort(R[:, 0])
    R = R[order]
    overlaps = 0
    for i in range(len(R)):
        j = i + 1
        while j < len(R) and R[j, 0] < R[i, 2] - 1e-9:
            if R[j, 1] < R[i, 3] - 1e-9 and R[i, 1] < R[j, 3] - 1e-9:
                overlaps += 1
            j += 1
    return {"charts": int(len(R)), "inside_atlas": inside, "rectangle_overlaps": int(overlaps)}


def solid(cells, base_z):
    """build_solid with walls brick_red and base concrete, retried with a coarser snap if needed."""
    M = bl.mats()
    m = None
    for snap in (0.001, 0.01, 0.05):
        try:
            m = rm.build_solid(cells, base_z, M["brick_red"], M["concrete"], snap=snap)
        except Exception:
            continue
        m["meta"]["snap_m"] = snap
        if not vcmesh.check_mesh(m, need_watertight=True):
            return m
    if m is None:
        raise RuntimeError("build_solid failed at every snap")
    return m


def rebase_cap(mesh):
    """Replace build_solid's base cap (a copy of every roof cell's triangles) with one constrained
    triangulation of the footprint that uses exactly the wall-bottom vertices: same closed solid,
    far fewer triangles. Returns (mesh, base triangles before, after)."""
    from shapely.ops import polygonize
    v, f = mesh["verts"], mesh["faces"]
    n, _ = face_normals(v, f)
    zb = v[:, 2].min()
    base = (n[:, 2] < -0.99) & (np.abs(v[f][:, :, 2] - zb).max(axis=1) < 1e-6)
    keep = f[~base]
    e = np.sort(np.r_[keep[:, [0, 1]], keep[:, [1, 2]], keep[:, [2, 0]]], axis=1)
    u, cnt = np.unique(e, axis=0, return_counts=True)
    bnd = u[cnt == 1]
    rings = sorted(polygonize(shapely.unary_union([shapely.LineString([v[a, :2], v[b, :2]]) for a, b in bnd])),
                   key=lambda p: -p.area)
    shape = None
    for p in rings:                          # nested rings: courtyards are holes, islands in them parts
        shape = p if shape is None else (shape.difference(p) if shape.contains(p) else shape.union(p))
    idx = {(round(x, 6), round(y, 6)): i for i, (x, y) in ((i, v[i, :2]) for i in np.unique(bnd))}
    tris = []
    for p in bc.polys_of(shape):
        for t in shapely.constrained_delaunay_triangles(p).geoms:
            c = np.asarray(t.exterior.coords)[:3]
            ids = [idx.get((round(x, 6), round(y, 6))) for x, y in c]
            if None in ids:
                raise RuntimeError("cap triangulation added a vertex")
            (x0, y0), (x1, y1), (x2, y2) = c
            if (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0) > 0:     # face down
                ids = [ids[0], ids[2], ids[1]]
            tris.append(ids)
    M = bl.mats()
    nf = np.vstack([keep, np.asarray(tris, np.int64)])
    fm = np.r_[mesh["face_mat"][~base], np.full(len(tris), M["concrete"])]
    used = np.unique(nf)
    remap = np.full(len(v), -1)
    remap[used] = np.arange(len(used))
    out = {"verts": v[used], "faces": remap[nf].astype(np.int32), "face_mat": fm.astype(np.uint8),
           "uv0": np.zeros((len(nf), 3, 2), np.float32), "meta": {**mesh.get("meta", {}), "base_cap": "rebuilt"}}
    if vcmesh.check_mesh(out, need_watertight=True):
        raise RuntimeError("rebuilt base cap is not watertight")
    return out, int(base.sum()), len(tris)


def shell_of(mesh):
    """LoD2 without its base cap (faces facing straight down at the base height)."""
    v, f = mesh["verts"], mesh["faces"]
    n, _ = face_normals(v, f)
    zb = v[:, 2].min()
    base = (n[:, 2] < -0.99) & (np.abs(v[f][:, :, 2] - zb).max(axis=1) < 1e-6)
    return v, f[~base]


def clean_tris(v, f, merge=0.001):
    """Merge vertices closer than 1 mm, drop degenerate, zero-area and duplicate triangles."""
    q = np.round(v / merge).astype(np.int64)
    _, first, inv = np.unique(q, axis=0, return_index=True, return_inverse=True)
    inv = inv.ravel()
    v2 = v[first]
    f2 = inv[f]
    ok = (f2[:, 0] != f2[:, 1]) & (f2[:, 1] != f2[:, 2]) & (f2[:, 0] != f2[:, 2])
    f2 = f2[ok]
    _, a = face_normals(v2, f2)
    f2 = f2[a > 1e-6]
    _, keep = np.unique(np.sort(f2, axis=1), axis=0, return_index=True)
    f2 = f2[np.sort(keep)]
    used = np.unique(f2)
    remap = np.full(len(v2), -1)
    remap[used] = np.arange(len(used))
    return v2[used], remap[f2]


def surface_distance(va, fa, vb, fb, n_samples=20000, seed=0):
    """Two-sided distances between meshes A and B: area-weighted samples (and vertices) of each
    surface to the other one. Returns (max, p95) in meters."""
    import open3d as o3d

    def samples(v, f, n):
        _, a = face_normals(v, f)
        rng = np.random.default_rng(seed)
        k = rng.choice(len(f), size=n, p=a / a.sum())
        r1, r2 = rng.random(n), rng.random(n)
        s = np.sqrt(r1)
        T = v[f[k]]
        p = (1 - s)[:, None] * T[:, 0] + (s * (1 - r2))[:, None] * T[:, 1] + (s * r2)[:, None] * T[:, 2]
        return np.vstack([p, v])

    def dist(v, f, P, off):
        sc = o3d.t.geometry.RaycastingScene()
        sc.add_triangles(o3d.core.Tensor((v - off).astype(np.float32)), o3d.core.Tensor(f.astype(np.uint32)))
        return sc.compute_distance(o3d.core.Tensor((P - off).astype(np.float32))).numpy()
    off = va.mean(axis=0)
    d = np.r_[dist(vb, fb, samples(va, fa, n_samples), off), dist(va, fa, samples(vb, fb, n_samples // 2), off)]
    return float(d.max()), float(np.percentile(d, 95))


def collider(mesh, max_dev=COL_DEV, max_tris=COL_TRIS):
    """One non-convex collider from the LoD2 shell (see module help). Returns (mesh dict, info)."""
    import open3d as o3d
    v, f = shell_of(mesh)
    v, f = clean_tris(v, f)
    best = (v, f, 0.0, 0.0)
    tried = []
    if len(f) > max_tris:
        m = o3d.geometry.TriangleMesh(o3d.utility.Vector3dVector(v - v.mean(axis=0)),
                                      o3d.utility.Vector3iVector(f.astype(np.int32)))
        target = max_tris
        while target < len(f):
            s = m.simplify_quadric_decimation(int(target), boundary_weight=1000.0)
            sv = np.asarray(s.vertices) + v.mean(axis=0)
            sf = np.asarray(s.triangles)
            sv, sf = clean_tris(sv, sf)
            if len(sf) >= 4:
                dmax, d95 = surface_distance(v, f, sv, sf)
                tried.append((int(target), len(sf), round(dmax, 3)))
                if dmax <= max_dev:
                    best = (sv, sf, dmax, d95)
                    break
            target = int(target * 1.6)
    cv, cf, dmax, d95 = best
    M = bl.mats()
    out = {"verts": cv, "faces": cf.astype(np.int32), "face_mat": np.full(len(cf), M["concrete"], np.uint8),
           "uv0": np.zeros((len(cf), 3, 2), np.float32)}
    info = {"triangles": int(len(cf)), "shell_triangles": int(len(f)), "max_dev_m": round(dmax, 4),
            "p95_dev_m": round(d95, 4), "simplified": len(cf) < len(f), "tries": tried,
            "within_tolerance": dmax <= max_dev + 1e-9, "over_triangle_budget": len(cf) > max_tris}
    return out, info


def collider_problems(m):
    v, f = m["verts"], m["faces"]
    why = []
    _, a = face_normals(v, f)
    if (a < 1e-6).any():
        why.append("degenerate triangles")
    if len(np.unique(np.sort(f, axis=1), axis=0)) < len(f):
        why.append("duplicate triangles")
    if len(np.unique(np.round(v, 3), axis=0)) < len(v):
        why.append("vertices closer than 1 mm")
    # consistent winding: every interior edge used once in each direction
    e = np.r_[f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]]]
    s = set(map(tuple, e))
    if len(s) < len(e):
        why.append("an edge used twice in the same direction (inconsistent winding)")
    return why


# ---- parts of a height-field building ---------------------------------------------------

def ring_samples(poly, step=0.1, inset=0.05):
    g = poly.buffer(-inset)
    if g.is_empty:
        g = poly
    pts = []
    for p in bc.polys_of(g):
        for r in [p.exterior] + list(p.interiors):
            L = r.length
            n = max(int(L / step), 4)
            pts.append(np.asarray([r.interpolate(t).coords[0] for t in np.linspace(0, L, n, endpoint=False)]))
    return np.vstack(pts) if pts else np.zeros((0, 2))


def grid_samples(poly, step=0.25):
    x0, y0, x1, y1 = poly.bounds
    gx, gy = np.meshgrid(np.arange(x0 + step / 2, x1, step), np.arange(y0 + step / 2, y1, step))
    gx, gy = gx.ravel(), gy.ravel()
    ins = shapely.contains_xy(poly, gx, gy)
    return np.c_[gx[ins], gy[ins]]


def ridge_samples(poly, inset=0.3):
    """Sample points for a part's ridge: a 0.25 m grid and the outline ring, 0.3 m inside the part
    (the whole part when it is thinner than that)."""
    g = poly.buffer(-inset)
    if g.is_empty or g.area < 0.05:
        g = poly.buffer(-0.05)
        if g.is_empty:
            g = poly
    gs = grid_samples(g)
    rs = ring_samples(g, inset=0.0)
    return np.vstack([gs, rs]) if len(gs) else rs


def ridge_heights(poly, mz):
    xy = ridge_samples(poly)
    if not len(xy):
        return np.zeros(0)
    z = mz(xy[:, 0], xy[:, 1])
    return z[np.isfinite(z)]


def part_type(mesh, poly, mz):
    """flat / pitched / curved from the roof faces inside the part."""
    v, f = mesh["verts"], mesh["faces"]
    n, a = face_normals(v, f)
    c = v[f].mean(axis=1)
    roof = (n[:, 2] > 0.2) & shapely.contains_xy(poly, c[:, 0], c[:, 1])
    if not roof.any():
        return "flat"
    sl = np.degrees(np.arccos(np.clip(n[roof, 2], -1, 1)))
    w = a[roof]
    zr = v[f[roof]][:, :, 2]
    if w[sl >= rf.FLAT_DEG].sum() < 0.1 * w.sum() and zr.max() - zr.min() < 0.5:
        return "flat"
    # distinct facet directions among the pitched faces (15 deg clusters, >= 5% of the area)
    nn, ww = n[roof][sl >= rf.FLAT_DEG], w[sl >= rf.FLAT_DEG]
    order = np.argsort(-ww)
    centers = []
    for i in order:
        for cc in centers:
            if nn[i] @ cc[0] > math.cos(math.radians(15)):
                cc[1] += ww[i]
                break
        else:
            centers.append([nn[i], ww[i]])
    big = [cc for cc in centers if cc[1] >= 0.05 * ww.sum()]
    covered = sum(cc[1] for cc in big) / ww.sum()
    return "pitched" if len(big) <= 6 and covered > 0.85 else "curved"


def hf_parts(hf, mesh, footprint, ground_z, base_z):
    mz = MeshZ(mesh)
    outline = footprint.boundary
    rows = []
    regs = sorted(hf["regions"], key=lambda r: -r["area_m2"])
    for k, r in enumerate(regs):
        poly = r["polygon"]
        if poly.is_empty:
            continue
        ring = ring_samples(poly)
        if not len(ring):
            continue
        zr = mz(ring[:, 0], ring[:, 1])
        on = shapely.distance(shapely.points(ring), outline) < 0.1
        ze = zr[on] if on.any() and np.isfinite(zr[on]).any() else zr
        # the ridge is read 0.3 m inside the part, so a taller neighbor across a wall (whose
        # mesh border may sit a few cm off the region outline) never counts as this part's ridge
        zg = ridge_heights(poly, mz)
        allz = zg if len(zg) else np.r_[zr]
        allz = allz[np.isfinite(allz)]
        if not len(allz) or not np.isfinite(ze).any():
            continue
        eave = max(float(np.nanmin(ze)), base_z + 0.3)
        ridge = max(float(allz.max()), base_z + 0.3)
        rows.append({"id": f"P{len(rows) + 1}", "area_m2": round(poly.area, 2), "roof_type": part_type(mesh, poly, mz),
                     "region": int(r["region"]), "planar": bool(r["planar"]),
                     "ground_z": round(ground_z, 3), "base_z": round(base_z, 3),
                     "eave_z": round(eave, 3), "ridge_z": round(ridge, 3),
                     "eave_height_m": round(eave - ground_z, 3), "ridge_height_m": round(ridge - ground_z, 3),
                     "touches_outline": bool(on.any()),
                     "polygon": bc.geom_to_json(poly)})
    return rows


def roof_surface_error(P, footprint, mesh):
    """Vertical distance of roof points (inside the footprint) to the mesh's top surface."""
    if len(P) == 0:
        return None
    ins = shapely.contains_xy(footprint, P[:, 0], P[:, 1])
    Q = P[ins]
    if not len(Q):
        return None
    d = np.abs(Q[:, 2] - MeshZ(mesh)(Q[:, 0], Q[:, 1]))
    d = d[np.isfinite(d)]
    if not len(d):
        return None
    return {"n": int(len(d)), "median": round(float(np.median(d)), 3), "p90": round(float(np.percentile(d, 90)), 3),
            "p95": round(float(np.percentile(d, 95)), 3), "within_0.2": round(float(np.mean(d <= 0.2)), 4)}


# ---- one building ----------------------------------------------------------------------

def v1_cells(planes_json):
    cells = [(bc.geom_from_json(c["polygon"]), c["plane"]) for c in planes_json["cells"]]
    planes = []
    for p in planes_json["planes"]:
        pl = rf.Plane(p["a"], p["b"], p["c"], kind=p.get("kind", "strict"))
        pl.zlo, pl.zhi = p.get("zlo"), p.get("zhi")
        planes.append(pl)
    return cells, planes


def heightfield_model(rec, P, v1m, v1planes, root):
    """Height-field LoD2 for one building. Returns (mesh, parts, hf, info)."""
    M = bl.mats()
    footprint = bc.geom_from_json(rec["polygon"])
    base_z = v1m["base_z"]
    cells, planes = v1_cells(v1planes)
    try:
        hf = rh.heightfield_roof(P, footprint, v1=(cells, planes))
    except shapely.errors.GEOSException:
        # a simplified region outline can trip GEOS; retry with the exact (unsimplified) outlines
        hf = rh.heightfield_roof(P, footprint, v1=(cells, planes), simplify=0.0)
        hf["stats"]["outline_simplify_m"] = 0.0
    # roof material from the orthophoto color of the pitched pieces (same rule as v1)
    slopes = []
    fake_cells, fake_planes = [], []
    for c in hf["cells"]:
        poly = c["poly"]
        xy = bc.corners(poly)
        z = c["z"](xy[:, 0], xy[:, 1])
        A = np.c_[xy - xy.mean(axis=0), np.ones(len(xy))]
        sol, *_ = np.linalg.lstsq(A, z, rcond=None)
        pl = rf.Plane(sol[0], sol[1], 0.0)
        slopes.append(pl.slope_deg)
        fake_cells.append((poly, len(fake_planes)))
        fake_planes.append(pl)
    roof_mat, color = bl.roof_material(fake_cells, fake_planes, root)
    cd = [{"poly": c["poly"], "z": c["z"], "mat": M["roof_flat"] if s < rf.FLAT_DEG else M[roof_mat]}
          for c, s in zip(hf["cells"], slopes)]
    mesh = solid(cd, base_z)
    mesh, _ = bl.clean_mesh(mesh)
    try:
        mesh, _, _ = rebase_cap(mesh)
    except Exception as ex:          # keep build_solid's own cap
        mesh["meta"]["base_cap"] = f"build_solid (rebuild failed: {ex!r})"
    parts = hf_parts(hf, mesh, footprint, v1m["ground_z"], base_z)
    return mesh, parts, hf, {"roof_material": roof_mat, "roof_color": color}


def model_building(rec, pts, others, v1m, v1planes, v1ledger, v1acc, heroes, root, keep_v1=False):
    """Build the v2 files of one building (in memory). Returns (model, ledger, planes_json, meshes)."""
    bid = rec["id"]
    footprint = bc.geom_from_json(rec["polygon"])
    d1 = bc.building_dir(bid, root)
    lod2_v1 = vcmesh.load_mesh(d1 / "lod2.npz")
    lod1_v1 = vcmesh.load_mesh(d1 / "lod1.npz")
    P = rh.roof_points(pts, footprint, others, v1m["ground_z"])
    v1_pass = bool(v1acc.get("pass"))
    hero = bid in heroes
    no_lidar = "fallback_flat" in v1m["flags"] or len(P) < 12
    method, why = "planes_v1", None
    hf = mesh2 = parts2 = None
    err_v1 = roof_surface_error(P, footprint, lod2_v1)
    err_v2 = None
    hf_error = None
    if keep_v1:
        hf_error = "not run (--keep-v1: the height field went over the run's time budget for this building)"
    if not no_lidar and not keep_v1 and (not v1_pass or hero):
        try:
            mesh2, parts2, hf, matinfo = heightfield_model(rec, P, v1m, v1planes, root)
            err_v2 = roof_surface_error(P, footprint, mesh2)
        except Exception as ex:
            hf_error = repr(ex)
            mesh2 = None
    if no_lidar:
        why = "no LiDAR roof (v1 flat fallback): nothing for a height field to follow"
    elif mesh2 is None and not v1_pass:
        why = f"height field failed: {hf_error}"
    elif v1_pass and not hero:
        why = "v1 passes the accuracy gate"
    elif hero:
        better = err_v2 is not None and (err_v1 is None or (err_v2["p95"] < err_v1["p95"] and
                                                            err_v2["median"] <= err_v1["median"] + 0.01))
        method = "heightfield_v2" if better else "planes_v1"
        why = f"hero building: lower roof-surface error with {method} (v1 {err_v1}, v2 {err_v2})"
    else:
        method, why = "heightfield_v2", "v1 fails the accuracy gate"
    M = bl.mats()
    ledger = {"building": bid, "version": 2, "lod": "LoD2", "threshold_m": rf.MERGE_MIN,
              "entries": [{**e, "source": "v1"} for e in v1ledger["entries"]]}
    planes_json = dict(v1planes)
    planes_json["version"] = 2
    planes_json["v1_planes_note"] = "planes and cells are v1's (used to snap the height field)"
    if method == "heightfield_v2":
        mesh = mesh2
        parts = parts2
        n0 = len(ledger["entries"])
        for k, e in enumerate(hf["ledger"], 1):
            ledger["entries"].append({"id": f"L{n0 + k:04d}", **e, "source": "v2 height field merge rule"})
        planes_json["heightfield"] = {**hf["stats"], "cell_m": rh.CELL, "break_m": rh.BREAK, "tol_m": rh.TOL,
                                      "merge": {"relief_m": rh.MERGE_RELIEF, "area_m2": rh.MERGE_AREA,
                                                "narrow_m": rh.NARROW}}
        uv0, side, ncharts, uvcheck = uv_by_normals(mesh["verts"], mesh["faces"])
        mesh["uv0"] = uv0
        mesh["meta"] = {**mesh.get("meta", {}), "uv_m_per_unit": side}
        mesh["charts"] = ncharts
        roof_type = max(parts, key=lambda p: p["area_m2"])["roof_type"] if parts else "flat"
        if len(parts) > 1 and all(p["roof_type"] == "flat" for p in parts):
            roof_type = "flat"
    else:
        mesh = {**lod2_v1, "charts": lod2_v1["meta"].get("charts")}
        mesh["meta"] = {"uv_m_per_unit": lod2_v1["meta"]["uv_m_per_unit"]}
        parts = v1m["parts"]
        roof_type = v1m["roof_type"]
        uvcheck = {"charts": lod2_v1["meta"].get("charts"), "note": "v1 UV (one chart per roof cell and wall segment)"}
        if hf is not None:
            planes_json["heightfield"] = {**hf["stats"], "used": False}
    col, colinfo = collider(mesh)
    colinfo["problems"] = collider_problems(col)
    model = {k: v for k, v in v1m.items() if k not in ("accuracy", "files", "colliders", "triangles", "seconds")}
    used = sorted({int(m) for m in np.unique(mesh["face_mat"])})
    inv = {i: k for k, i in M.items()}
    model.update({
        "version": 2, "roof_method": method, "roof_method_reason": why, "hero": hero,
        "roof_type": roof_type, "parts": parts,
        "materials": sorted(inv[i] for i in used),
        "uv_m_per_unit": {"lod2": round(float(mesh["meta"]["uv_m_per_unit"]), 4),
                          "lod1": round(float(lod1_v1["meta"]["uv_m_per_unit"]), 4)},
        "uv_check": uvcheck,
        "triangles": {"lod2": int(len(mesh["faces"])), "lod1": int(len(lod1_v1["faces"])),
                      "col_mesh": int(len(col["faces"]))},
        "collider": {"kind": "one non-convex static triangle mesh (LoD2 shell without base cap)", **colinfo},
        "roof_surface_error": {"planes_v1": err_v1, "heightfield_v2": err_v2},
        "heightfield_error": hf_error,
        "ledger_entries": len(ledger["entries"]),
        "files": {"lod2": "lod2.npz", "lod1": "lod1.npz", "col_mesh": "col_mesh.npz",
                  "roof_planes": "roof_planes.json", "ledger": "ledger.json"},
        "accuracy": None, "v1_accuracy_pass": v1_pass,
    })
    if method == "heightfield_v2":
        model["material_rule"] = ("walls and vertical roof steps brick_red (guess); roof faces flatter than 6 deg "
                                  "roof_flat, steeper roof_slate unless the Metro 2026 ortho color is clearly green "
                                  "(roof_copper) or orange-red (roof_tile); base concrete")
        model["roof_fit"] = {**v1m.get("roof_fit", {}), "heightfield": hf["stats"], **matinfo}
    probs = vcmesh.check_mesh(mesh, need_watertight=True)
    model["mesh_checks"] = {"lod2": probs, "lod1": vcmesh.check_mesh(lod1_v1, need_watertight=True)}
    if probs:
        model["flags"] = sorted(set(model["flags"]) | {"mesh_problem"})
    return model, ledger, planes_json, {"lod2": mesh, "lod1": lod1_v1, "col": col}


def write_building(bid, model, ledger, planes_json, meshes, root):
    d = v2_dir(bid, root)
    d.mkdir(parents=True, exist_ok=True)
    files = []
    for name in ("lod2", "lod1"):
        m = meshes[name]
        meta = {"building": bid, "lod": name.upper().replace("LOD", "LoD"), "frame": "campus meters, Z up",
                "uv_m_per_unit": m["meta"]["uv_m_per_unit"], "charts": m.get("charts"),
                "script": "buildings_v2.py", "version": 2, "roof_method": model["roof_method"],
                "base_cap": m["meta"].get("base_cap", "v1" if model["roof_method"] == "planes_v1" else None)}
        vcmesh.save_mesh(d / f"{name}.npz", m["verts"], m["faces"], m["face_mat"], m["uv0"], meta)
        files.append(d / f"{name}.npz")
    c = meshes["col"]
    vcmesh.save_mesh(d / "col_mesh.npz", c["verts"], c["faces"], c["face_mat"], c["uv0"],
                     {"building": bid, "kind": "non-convex static collider (LoD2 shell, no base cap)",
                      "uv0": "unused (zeros)", "version": 2, "max_dev_m": model["collider"]["max_dev_m"]})
    files.append(d / "col_mesh.npz")
    files.append(bc.save(d / "roof_planes.json", planes_json))
    files.append(bc.save(d / "ledger.json", ledger))
    files.append(bc.save(d / "model.json", model))
    g = bc.git_building_dir(bid)
    bc.save(g / "model.json", model)
    bc.save(g / "ledger.json", ledger)
    return files


def run_job(job):
    tile, recs, neighbors, force, root, v1acc, heroes = job[:7]
    keep = job[7] if len(job) > 7 else set()
    out, todo = [], []
    for rec in recs:
        if not force and (v2_dir(rec["id"], root) / "model.json").exists():
            out.append({"id": rec["id"], "status": "skipped (finished; use --force)", "files": []})
        else:
            todo.append(rec)
    if not todo:
        return out
    gs = [bc.geom_from_json(r["polygon"]) for r in todo]
    b = np.array([g.bounds for g in gs])
    pts = lidar_io.load_points(b[:, 0].min() - 6, b[:, 1].min() - 6, b[:, 2].max() + 6, b[:, 3].max() + 6, root=root)
    for rec, g in zip(todo, gs):
        t0 = time.time()
        bid = rec["id"]
        try:
            d1 = bc.building_dir(bid, root)
            v1m = load_json(d1 / "model.json")
            v1p = load_json(d1 / "roof_planes.json")
            v1l = load_json(d1 / "ledger.json")
            others = [ng for nid, ng in neighbors if nid != bid and ng.intersects(g.buffer(8))]
            others = shapely.unary_union(others) if others else None
            model, ledger, pj, meshes = model_building(rec, pts, others, v1m, v1p, v1l, v1acc.get(bid, {}), heroes, root,
                                                          keep_v1=bid in keep)
            model["seconds"] = round(time.time() - t0, 2)
            files = write_building(bid, model, ledger, pj, meshes, root)
            out.append({"id": bid, "status": "ok", "files": [str(f) for f in files], "method": model["roof_method"],
                        "problems": model["mesh_checks"]["lod2"] + model["collider"]["problems"],
                        "seconds": model["seconds"]})
        except Exception as ex:
            out.append({"id": bid, "status": "error", "error": repr(ex), "trace": traceback.format_exc(), "files": []})
    return out


def finalize_one(bid, root):
    """Rebuild the base cap of a finished height-field building made before rebase_cap existed,
    then its UV0, collider and the model numbers. Returns written files."""
    d = v2_dir(bid, root)
    model = load_json(d / "model.json")
    if model.get("roof_method") != "heightfield_v2":
        return []
    m = vcmesh.load_mesh(d / "lod2.npz")
    if m["meta"].get("base_cap") == "rebuilt":
        return []
    mesh, _, _ = rebase_cap(m)
    uv0, side, ncharts, uvcheck = uv_by_normals(mesh["verts"], mesh["faces"])
    mesh["uv0"], mesh["charts"] = uv0, ncharts
    mesh["meta"] = {**m["meta"], "uv_m_per_unit": side, "charts": ncharts, "base_cap": "rebuilt"}
    col, colinfo = collider(mesh)
    colinfo["problems"] = collider_problems(col)
    model["uv_m_per_unit"]["lod2"] = round(side, 4)
    model["uv_check"] = uvcheck
    model["triangles"]["lod2"] = int(len(mesh["faces"]))
    model["triangles"]["col_mesh"] = int(len(col["faces"]))
    model["collider"] = {"kind": model["collider"]["kind"], **colinfo}
    model["mesh_checks"]["lod2"] = vcmesh.check_mesh(mesh, need_watertight=True)
    vcmesh.save_mesh(d / "lod2.npz", mesh["verts"], mesh["faces"], mesh["face_mat"], mesh["uv0"], mesh["meta"])
    vcmesh.save_mesh(d / "col_mesh.npz", col["verts"], col["faces"], col["face_mat"], col["uv0"],
                     {"building": bid, "kind": "non-convex static collider (LoD2 shell, no base cap)",
                      "uv0": "unused (zeros)", "version": 2, "max_dev_m": colinfo["max_dev_m"]})
    bc.save(d / "model.json", model)
    bc.save(bc.git_building_dir(bid) / "model.json", model)
    return [d / "lod2.npz", d / "col_mesh.npz", d / "model.json"]


def refresh_parts(bid, root):
    """Recompute the eave / ridge numbers and roof type of a height-field building's parts from
    its saved LoD2 mesh (after the MeshZ wall-sliver fix). Returns written files."""
    d = v2_dir(bid, root)
    model = load_json(d / "model.json")
    if model.get("roof_method") != "heightfield_v2":
        return []
    mesh = vcmesh.load_mesh(d / "lod2.npz")
    fp = shapely.unary_union([bc.geom_from_json(p["polygon"]) for p in model["parts"]])
    regions = [{"region": p.get("region", k), "polygon": bc.geom_from_json(p["polygon"]),
                "area_m2": p["area_m2"], "planar": p.get("planar", False)} for k, p in enumerate(model["parts"])]
    rows = hf_parts({"regions": regions}, mesh, fp, model["ground_z"], model["base_z"])
    model["parts"] = rows
    if rows:
        model["roof_type"] = max(rows, key=lambda p: p["area_m2"])["roof_type"]
        if len(rows) > 1 and all(p["roof_type"] == "flat" for p in rows):
            model["roof_type"] = "flat"
    model["parts_refreshed"] = time.strftime("%Y-%m-%dT%H:%M:%S")
    bc.save(d / "model.json", model)
    bc.save(bc.git_building_dir(bid) / "model.json", model)
    return [d / "model.json"]


def _refresh_job(args):
    bid, root = args
    try:
        return bid, [str(f) for f in refresh_parts(bid, root)], None
    except Exception as ex:
        return bid, [], repr(ex)


def _finalize_job(args):
    bid, root = args
    try:
        return bid, [str(f) for f in finalize_one(bid, root)], None
    except Exception as ex:
        return bid, [], repr(ex)


def write_index(recs, root):
    """derived/buildings/v2/index.json: every campus building and the folder to export."""
    rows = []
    for r in sorted(recs, key=lambda r: r["id"]):
        g = bc.geom_from_json(r["polygon"])
        polys = sorted(bc.polys_of(g), key=lambda p: -p.area)
        m = v2_dir(r["id"], root) / "model.json"
        model = load_json(m) if m.exists() else None
        c = g.centroid
        main = polys[0]
        row = {"id": r["id"], "name": r.get("name"), "tile": fr.tile_of(c.x, c.y),
               "version": V2 if model else "v1",
               "roof_method": model["roof_method"] if model else "planes_v1",
               "footprint": [[round(x, 3), round(y, 3)] for x, y in main.exterior.coords[:-1]],
               "holes": [[[round(x, 3), round(y, 3)] for x, y in h.coords[:-1]] for h in main.interiors],
               "area_m2": r["area_m2"], "zone": r["zone"], "source": r["source"]}
        if len(polys) > 1:
            row["extra_polygons"] = bc.geom_to_json(shapely.MultiPolygon(polys[1:]))
        rows.append(row)
    p = index_path(root)
    bc.save(p, {"version": 2, "made": time.strftime("%Y-%m-%dT%H:%M:%S"), "frame": "campus meters",
                "note": "version = building folder to export (buildings/<id>/<version>/); footprint = outer ring of "
                        "the main polygon, holes = its courtyards; extra_polygons for multi-part footprints "
                        "(same layout as v1 footprints.json polygons)",
                "buildings": rows})
    return p


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--jobs", type=int, default=14)
    p.add_argument("--force", action="store_true", help="redo finished buildings")
    p.add_argument("--only", nargs="*", help="only these building ids")
    p.add_argument("--keep-v1", nargs="*", default=[],
                   help="these ids keep v1 without trying the height field (time budget)")
    p.add_argument("--refresh-parts", action="store_true",
                   help="only recompute part eave/ridge/type of finished height-field buildings from their mesh")
    p.add_argument("--record", action="store_true",
                   help="only rewrite the index and record every buildings/*/v2 file in the manifest")
    p.add_argument("--finalize", action="store_true",
                   help="only rebuild the base cap, UV0 and collider of finished height-field buildings "
                        "made before the base-cap rebuild existed")
    args = p.parse_args(argv)
    t0 = time.time()
    root = data_root()
    if args.record:
        allrec = load_json(bc.out_dir(root) / "footprints.json")["buildings"]
        ip = write_index([r for r in allrec if r["zone"] == "campus"], root)
        db = manifest.connect(root)
        n = 0
        for f in sorted((root / "buildings").glob("B_*/v2/*")) + [ip]:
            manifest.add_file(f, source="buildings_v2.py", license="derived (OSM ODbL, Metro open data, "
                              "USGS LiDAR public domain)", stage=1, root=root, db=db)
            n += 1
        db.commit()
        db.close()
        print(f"recorded {n} files; index {ip}")
        return 0
    if args.refresh_parts:
        ids = [d.parent.name for d in sorted((root / "buildings").glob("B_*/v2")) if (d / "model.json").exists()]
        with Pool(args.jobs) as pool:
            res = pool.map(_refresh_job, [(i, root) for i in ids])
        db = manifest.connect(root)
        for bid, files, err in res:
            for f in files:
                manifest.add_file(f, source="buildings_v2.py --refresh-parts", license="derived", stage=1, root=root, db=db)
            if err:
                print(f"ERROR {bid}: {err}", file=sys.stderr)
        db.commit()
        db.close()
        print(f"refreshed {sum(1 for _, f, _ in res if f)} buildings, {sum(1 for *_, e in res if e)} errors")
        return 1 if any(e for *_, e in res) else 0
    if args.finalize:
        ids = [d.parent.name for d in sorted((root / "buildings").glob("B_*/v2")) if (d / "model.json").exists()]
        if args.only:
            ids = [i for i in ids if i in set(args.only)]
        with Pool(args.jobs) as pool:
            res = pool.map(_finalize_job, [(i, root) for i in ids])
        db = manifest.connect(root)
        for bid, files, err in res:
            for f in files:
                manifest.add_file(f, source="buildings_v2.py --finalize", license="derived", stage=1, root=root, db=db)
            if err:
                print(f"ERROR {bid}: {err}", file=sys.stderr)
        db.commit()
        db.close()
        print(f"finalized {sum(1 for _, f, _ in res if f)} buildings, {sum(1 for *_, e in res if e)} errors")
        return 1 if any(e for *_, e in res) else 0
    fpath = bc.out_dir(root) / "footprints.json"
    acc = bc.reports_dir(root) / "buildings_accuracy.json"
    if not fpath.exists() or not acc.exists():
        fail("run buildings_footprints.py, buildings_lod2.py and buildings_check.py (v1) first")
    allrec = load_json(fpath)["buildings"]
    campus = [r for r in allrec if r["zone"] == "campus"]
    recs = [r for r in campus if not args.only or r["id"] in set(args.only)]
    v1acc = {r["building"]: {"pass": r.get("pass")} for r in load_json(acc)["buildings"]}
    heroes = hero_ids()
    allg = [(r["id"], bc.geom_from_json(r["polygon"])) for r in allrec]
    tree = STRtree([g for _, g in allg])
    by_tile = {}
    for r in recs:
        by_tile.setdefault(r["tile"], []).append(r)
    jobs = []
    for tile, rs in sorted(by_tile.items()):
        # one job per building keeps the pool busy (big buildings take longest)
        for r in sorted(rs, key=lambda r: -r["area_m2"]):
            env = bc.geom_from_json(r["polygon"]).buffer(10).envelope
            jobs.append((tile, [r], [allg[int(k)] for k in tree.query(env)], args.force, root, v1acc, heroes,
                         set(args.keep_v1)))
    jobs.sort(key=lambda j: -j[1][0]["area_m2"])
    results = []
    if args.jobs > 1 and len(jobs) > 1:
        with Pool(args.jobs, maxtasksperchild=8) as pool:
            for out in pool.imap_unordered(run_job, jobs):
                results += out
                if len(results) % 25 == 0:
                    print(f"  {len(results)}/{len(recs)} buildings", flush=True)
    else:
        for j in jobs:
            results += run_job(j)
    db = manifest.connect(root)
    nfiles = 0
    for r in results:
        for f in r["files"]:
            manifest.add_file(f, source="buildings_v2.py", license="derived (OSM ODbL, Metro open data, "
                              "USGS LiDAR public domain)", stage=1, root=root, db=db)
            nfiles += 1
    ip = write_index(campus, root)
    manifest.add_file(ip, source="buildings_v2.py", license="derived", stage=1, root=root, db=db)
    errs = [r for r in results if r["status"] == "error"]
    probs = [r for r in results if r.get("problems")]
    log = bc.reports_dir(root) / "buildings_v2_run.json"
    bc.save(log, {"made": time.strftime("%Y-%m-%dT%H:%M:%S"), "seconds": round(time.time() - t0, 1),
                  "buildings": len(recs), "ok": sum(r["status"] == "ok" for r in results),
                  "methods": {m: sum(1 for r in results if r.get("method") == m) for m in ("planes_v1", "heightfield_v2")},
                  "errors": [{"id": r["id"], "error": r["error"], "trace": r["trace"]} for r in errs],
                  "problems": [{"id": r["id"], "problems": r["problems"]} for r in probs],
                  "slowest": sorted([(r.get("seconds", 0), r["id"]) for r in results], reverse=True)[:10]})
    manifest.add_file(log, source="buildings_v2.py", license="derived", stage=1, root=root, db=db)
    db.commit()
    db.close()
    for r in errs:
        print(f"ERROR {r['id']}: {r['error']}\n{r['trace']}", file=sys.stderr)
    for r in probs:
        print(f"PROBLEM {r['id']}: {r['problems']}", file=sys.stderr)
    print(f"{len(recs)} buildings: {sum(r['status'] == 'ok' for r in results)} built, "
          f"{sum(r['status'].startswith('skipped') for r in results)} skipped, {len(errs)} errors, "
          f"{len(probs)} with mesh problems; {nfiles} files recorded; index {ip}; {time.time() - t0:.0f} s")
    return 1 if errs or probs else 0


if __name__ == "__main__":
    sys.exit(main())
