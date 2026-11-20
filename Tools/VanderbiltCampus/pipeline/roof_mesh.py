"""Turn a 2.5D roof partition into a closed (watertight) building mesh, with UV0.

Input: "cells" = flat polygons that tile the footprint (neighbors share their edge
vertices exactly), each with a height function z(x, y) (its roof plane) and a material.
Output: one triangle mesh: roof cells on their planes, vertical walls wherever two
neighbors (or a cell and the outside) differ in height, and a flat base cap at base_z.

How it stays watertight:
- T-junctions are removed first (a vertex lying on a neighbor's edge is inserted there).
- Where two neighbor roofs cross along their shared edge, the edge is split at the
  crossing, so each wall has one "high" side.
- At every footprint vertex there is a vertical "column" of heights (one per touching
  roof, plus the base on the outline). Heights closer than 2 cm are merged. Every wall
  touching that column is cut at all of the column's heights in its range, so wall
  edges always meet edge to edge.

UV0 (STANDARDS.md section 7): one chart per roof cell (projected into its own plane) and
per wall segment (u = along the wall, v = height), plus one for the base; all at true
scale, packed with vcmesh.pack_charts into 0..1. meta uv_m_per_unit = meters per 1.0 UV.
"""
import math

import numpy as np
import shapely
from shapely.geometry import Polygon

from vcmesh import pack_charts

EPS_Z = 0.02          # heights closer than this at one column are the same point
KEY = 1e6             # 2D vertex key resolution (1 micrometer)


def _key(x, y):
    return (int(round(x * KEY)), int(round(y * KEY)))


def _rings_ccw(poly):
    poly = shapely.geometry.polygon.orient(poly, 1.0)
    rings = [np.asarray(poly.exterior.coords)[:-1, :2]]
    rings += [np.asarray(r.coords)[:-1, :2] for r in poly.interiors]
    return rings


class _Verts:
    """2D vertex table keyed by rounded coordinates."""

    def __init__(self):
        self.xy, self.index = [], {}

    def get(self, x, y):
        k = _key(x, y)
        i = self.index.get(k)
        if i is None:
            i = len(self.xy)
            self.index[k] = i
            self.xy.append((float(x), float(y)))
        return i


def _fix_tjunctions(rings_by_cell, V):
    """Insert vertices that lie on the inside of another ring's edge (within 1e-5 m)."""
    xy = np.asarray(V.xy)
    pts = shapely.points(xy)
    tree = shapely.STRtree(pts)
    out = []
    for rings in rings_by_cell:
        new_rings = []
        for ring in rings:
            nr = []
            n = len(ring)
            for k in range(n):
                a, b = ring[k], ring[(k + 1) % n]
                nr.append(a)
                pa, pb = xy[a], xy[b]
                seg = shapely.LineString([pa, pb])
                cand = tree.query(seg.buffer(1e-5))
                ins = []
                d = pb - pa
                L2 = float(d @ d)
                for c in cand:
                    c = int(c)
                    if c in (a, b):
                        continue
                    t = float((xy[c] - pa) @ d) / L2
                    if 1e-9 < t < 1 - 1e-9:
                        q = pa + t * d
                        if np.hypot(*(xy[c] - q)) < 1e-5:
                            ins.append((t, c))
                for _, c in sorted(ins):
                    nr.append(c)
            new_rings.append(nr)
        out.append(new_rings)
    return out


def _cdt(poly):
    """Constrained Delaunay triangles of a polygon (shapely / GEOS), as coordinate triples."""
    tris = shapely.constrained_delaunay_triangles(poly)
    out = []
    for t in getattr(tris, "geoms", []):
        c = np.asarray(t.exterior.coords)[:3, :2]
        out.append(c)
    return out


def _renode(cells):
    """Re-node cell polygons so neighbors share vertices; keep each cell's z and mat."""
    from shapely.ops import polygonize, unary_union
    polys = [c["poly"] for c in cells]
    net = unary_union([p.boundary for p in polys])
    tree = shapely.STRtree(polys)
    out = {}
    for f in polygonize(net):
        if f.area < 1e-9:
            continue
        rp = f.representative_point()
        cand = [int(k) for k in tree.query(rp, predicate="intersects")]
        if not cand:
            continue
        k = max(cand, key=lambda k: polys[k].intersection(f).area)
        out.setdefault(k, []).append(f)
    res = []
    for k, fs in out.items():
        u = shapely.unary_union(fs)
        for p in getattr(u, "geoms", [u]):
            if p.geom_type == "Polygon" and p.area > 1e-9:
                res.append({**cells[k], "poly": p})
    # unary_union may drop shared vertices; a final polygonize pass restores consistent nodes
    net = unary_union([c["poly"].boundary for c in res])
    tree = shapely.STRtree([c["poly"] for c in res])
    final = []
    for f in polygonize(net):
        if f.area < 1e-9:
            continue
        rp = f.representative_point()
        cand = [int(k) for k in tree.query(rp, predicate="intersects")]
        if cand:
            k = max(cand, key=lambda k: res[k]["poly"].intersection(f).area)
            final.append({**res[k], "poly": f})
    return final


def _wedge_angle(p, v, r):
    w = p.intersection(shapely.Point(v).buffer(r, quad_segs=4))
    if w.is_empty or w.area < 1e-12:
        return None, w
    c = w.centroid
    return math.atan2(c.y - v[1], c.x - v[0]), w


def fix_pinches(cells, base_z, eps=EPS_Z, delta=0.05, rounds=6):
    """Remove "pinch" vertices where roof heights go up and down more than once around one
    point (a closed mesh would need the same wall edge 4 times there).

    The fix moves a tiny corner wedge (radius delta) of one cell to its neighbor, so that
    cell no longer touches the vertex. Returns (cells, number_of_fixes)."""
    fixes = 0
    for _ in range(rounds):
        polys = [c["poly"] for c in cells]
        fp = shapely.unary_union(polys)
        fb = fp.boundary
        shapely.prepare(fb)
        tree = shapely.STRtree(polys)
        touched = set()
        verts = {}
        for i, p in enumerate(polys):
            for ring in [p.exterior] + list(p.interiors):
                for x, y in ring.coords[:-1]:
                    verts.setdefault(_key(x, y), (x, y))
        changed = False
        for k, v in verts.items():
            pt = shapely.Point(v)
            inc = [int(j) for j in tree.query(pt.buffer(1e-6), predicate="intersects")]
            if not inc or touched.intersection(inc):
                continue
            on_outline = fb.distance(pt) < 1e-6
            if not on_outline and len(inc) < 3:
                continue
            # cyclic order of regions around v
            seq = []
            disk = shapely.Point(v).buffer(delta, quad_segs=4)
            for j in inc:
                w = polys[j].intersection(disk)
                h = float(cells[j]["z"](np.array([v[0]]), np.array([v[1]]))[0])
                # a cell touching itself at v gives two wedges: list each one
                for piece in getattr(w, "geoms", [w]):
                    if piece.geom_type == "Polygon" and piece.area > 1e-12:
                        c = piece.centroid
                        seq.append((math.atan2(c.y - v[1], c.x - v[0]), j, max(h, base_z + 0.3), piece.area))
            if on_outline:
                outside = shapely.Point(v).buffer(delta, quad_segs=4).difference(fp)
                for o in getattr(outside, "geoms", [outside]):
                    if o.area > 1e-12:
                        c = o.centroid
                        seq.append((math.atan2(c.y - v[1], c.x - v[0]), -1, base_z, o.area))
            seq.sort()
            hs = [s[2] for s in seq]
            n = len(hs)
            if n < 4:
                continue
            levels = sorted(set(round(h, 3) for h in hs))
            worst = 0
            for lo, hi in zip(levels[:-1], levels[1:]):
                mid = (lo + hi) / 2
                cnt = sum(1 for i in range(n) if min(hs[i], hs[(i + 1) % n]) < mid < max(hs[i], hs[(i + 1) % n])
                          and abs(hs[i] - hs[(i + 1) % n]) > eps)
                worst = max(worst, cnt)
            if worst <= 2:
                continue
            # pick a cell that is a local extremum, give its corner wedge to a cell neighbor
            for i in range(n):
                a, j, h, _ = seq[i]
                if j < 0:
                    continue
                hp, hn = seq[i - 1][2], seq[(i + 1) % n][2]
                if not ((h > hp + eps and h > hn + eps) or (h < hp - eps and h < hn - eps)):
                    continue
                nbr = [seq[i - 1], seq[(i + 1) % n]]
                nbr = [s for s in nbr if s[1] >= 0 and s[1] != j]
                wedge = polys[j].intersection(shapely.Point(v).buffer(delta, quad_segs=4))
                newj = polys[j].difference(wedge)
                if newj.is_empty:
                    continue
                if nbr:
                    y = min(nbr, key=lambda s: abs(s[2] - h))[1]
                    newy = shapely.unary_union([polys[y], wedge])
                    if newy.geom_type != "Polygon":
                        continue
                    cells[y] = {**cells[y], "poly": newy}
                    polys[y] = newy
                # with only the outside next to it, the tiny wedge is simply cut off
                cells[j] = {**cells[j], "poly": newj}
                polys[j] = newj
                touched.update(inc)
                fixes += 1
                changed = True
                break
        if not changed:
            break
        flat = []
        for c in cells:
            for p in getattr(c["poly"], "geoms", [c["poly"]]):
                if p.geom_type == "Polygon" and p.area > 1e-9:
                    flat.append({**c, "poly": p})
        cells = _renode(flat)
    return cells, fixes


def snap_cells(cells, grid=0.001):
    """Snap every cell to a grid (meters) so nearly identical vertices become one."""
    out = []
    polys = shapely.set_precision(np.array([c["poly"] for c in cells], dtype=object), grid)
    for c, p in zip(cells, polys):
        for q in getattr(p, "geoms", [p]):
            if q.geom_type == "Polygon" and q.area > grid * grid:
                out.append({**c, "poly": q})
    return _renode(out) if len(out) > 1 else out


def build_solid(cells, base_z, wall_mat, base_mat, min_above_base=0.3, snap=0.001):
    """Watertight mesh from roof cells.

    cells: list of dicts {poly: shapely Polygon, z: callable(x, y) -> z, mat: int, name: str}
    Returns dict(verts, faces, face_mat, uv0, charts, meta) with UV0 packed.
    """
    if snap:
        cells = snap_cells(list(cells), snap)
    cells, _ = fix_pinches(list(cells), base_z)
    V = _Verts()
    rings_by_cell = []
    for c in cells:
        rings = []
        for r in _rings_ccw(c["poly"]):
            ids = [V.get(x, y) for x, y in r]
            # drop repeated ids (zero-length edges)
            ids = [i for k, i in enumerate(ids) if i != ids[k - 1]]
            if len(ids) >= 3:
                rings.append(ids)
        rings_by_cell.append(rings)
    rings_by_cell = _fix_tjunctions(rings_by_cell, V)

    def heights(rings_by_cell):
        xy = np.asarray(V.xy)
        H = []
        for ci, rings in enumerate(rings_by_cell):
            ids = sorted({i for r in rings for i in r})
            z = cells[ci]["z"](xy[ids, 0], xy[ids, 1])
            z = np.maximum(np.asarray(z, float), base_z + min_above_base)
            h = dict(zip(ids, z))
            for v in ids:
                if (ci, v) in override:
                    h[v] = override[(ci, v)]
            H.append(h)
        return H

    # at a crossing split the two roofs are forced to the same height (their planes may be
    # clipped, so the straight-line crossing estimate is not exact)
    override = {}

    def halfedges(rings_by_cell):
        he = {}
        for ci, rings in enumerate(rings_by_cell):
            for r in rings:
                for k in range(len(r)):
                    he[(r[k], r[(k + 1) % len(r)])] = ci
        return he

    def snapped_heights(rings_by_cell, he):
        """Heights with each vertex column's values merged (closer than EPS_Z -> their mean),
        so crossings are judged on the same numbers the walls are built from."""
        H = heights(rings_by_cell)
        outer_v = set()
        for (a, b) in he:
            if (b, a) not in he:
                outer_v.update((a, b))
        cols = {}
        for ci, h in enumerate(H):
            for v, z in h.items():
                cols.setdefault(v, []).append((z, ci))
        for v, zc in cols.items():
            vals = sorted(zc + ([(base_z, -1)] if v in outer_v else []))
            groups = [[vals[0]]]
            for item in vals[1:]:
                if item[0] - groups[-1][-1][0] < EPS_Z:
                    groups[-1].append(item)
                else:
                    groups.append([item])
            for g in groups:
                if any(ci < 0 for _, ci in g):
                    m = base_z
                else:
                    m = float(np.mean([z for z, _ in g]))
                for _, ci in g:
                    if ci >= 0:
                        H[ci][v] = m
        return H

    # split edges where neighbor roofs cross
    for _ in range(4):
        he = halfedges(rings_by_cell)
        H = snapped_heights(rings_by_cell, he)
        splits = {}
        xy = np.asarray(V.xy)
        for (a, b), c1 in he.items():
            c2 = he.get((b, a))
            if c2 is None or a > b:
                continue
            da = H[c1][a] - H[c2][a]
            db = H[c1][b] - H[c2][b]
            if da * db < 0:
                t = da / (da - db)
                if 1e-4 < t < 1 - 1e-4:
                    p = xy[a] + t * (xy[b] - xy[a])
                    sv = V.get(p[0], p[1])
                    splits[(a, b)] = sv
                    z1 = float(cells[c1]["z"](np.array([p[0]]), np.array([p[1]]))[0])
                    z2 = float(cells[c2]["z"](np.array([p[0]]), np.array([p[1]]))[0])
                    zm = max((z1 + z2) / 2, base_z + min_above_base)
                    override[(c1, sv)] = override[(c2, sv)] = zm
        if not splits:
            break
        for ci, rings in enumerate(rings_by_cell):
            for r in rings:
                k = 0
                while k < len(r):
                    a, b = r[k], r[(k + 1) % len(r)]
                    s = splits.get((a, b)) or splits.get((b, a))
                    if s is not None and s not in (a, b):
                        r.insert(k + 1, s)
                        k += 1
                    k += 1
    he = halfedges(rings_by_cell)
    H = snapped_heights(rings_by_cell, he)
    xy = np.asarray(V.xy)
    nv2 = len(xy)
    outer = set()
    for (a, b) in he:
        if (b, a) not in he:
            outer.add(a)
            outer.add(b)

    # columns: merged heights at every 2D vertex
    col_vals = {}
    for ci, h in enumerate(H):
        for v, z in h.items():
            col_vals.setdefault(v, []).append(z)
    for v in outer:
        col_vals.setdefault(v, []).append(base_z)
    verts3, vmap, levels = [], {}, {}
    for v, zs in col_vals.items():
        zs = sorted(zs)
        groups = [[zs[0]]]
        for z in zs[1:]:
            if z - groups[-1][-1] < EPS_Z:
                groups[-1].append(z)
            else:
                groups.append([z])
        lv = []
        for g in groups:
            zz = base_z if (v in outer and abs(g[0] - base_z) < EPS_Z) else float(np.mean(g))
            vmap[(v, round(zz, 6))] = len(verts3)
            verts3.append((xy[v, 0], xy[v, 1], zz))
            lv.append(zz)
        levels[v] = lv

    def snap(v, z):
        lv = levels[v]
        return lv[int(np.argmin([abs(z - q) for q in lv]))]

    def vid(v, z):
        return vmap[(v, round(snap(v, z), 6))]

    faces, mats, chart_of = [], [], []
    charts = []        # per chart: dict(kind, faces idx list, frame)

    # roofs
    for ci, rings in enumerate(rings_by_cell):
        if not rings:
            continue
        poly = Polygon(xy[rings[0]], [xy[r] for r in rings[1:]])
        if not poly.is_valid or poly.area < 1e-8:
            poly = shapely.make_valid(poly)
        tris = _cdt(poly)
        chart = {"kind": "roof", "cell": ci, "faces": []}
        for t in tris:
            ids = []
            for x, y in t:
                k = _key(x, y)
                if k not in V.index:
                    raise RuntimeError("triangulation added a vertex")
                ids.append(V.index[k])
            if len(set(ids)) < 3:
                continue
            (x0, y0), (x1, y1), (x2, y2) = xy[ids[0]], xy[ids[1]], xy[ids[2]]
            if (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0) < 0:
                ids = [ids[0], ids[2], ids[1]]
            f = [vid(i, H[ci][i]) for i in ids]
            chart["faces"].append(len(faces))
            faces.append(f)
            mats.append(cells[ci]["mat"])
            # base: same 2D triangle at base_z, facing down (stored for later)
            chart.setdefault("base2d", []).append(ids)
        charts.append(chart)

    # base cap (2D triangles of every roof cell, reversed, at base_z)
    base_vid = {}

    def bvid(v):
        if v in outer:
            return vmap[(v, round(base_z, 6))]
        if v not in base_vid:
            base_vid[v] = len(verts3)
            verts3.append((xy[v, 0], xy[v, 1], base_z))
        return base_vid[v]
    bchart = {"kind": "base", "faces": []}
    for ch in charts:
        for ids in ch.pop("base2d", []):
            f = [bvid(ids[0]), bvid(ids[2]), bvid(ids[1])]
            bchart["faces"].append(len(faces))
            faces.append(f)
            mats.append(base_mat)
    charts.append(bchart)

    # walls
    done = set()
    for (a, b), c1 in he.items():
        if (a, b) in done:
            continue
        c2 = he.get((b, a))
        done.add((a, b))
        done.add((b, a))
        h1a, h1b = snap(a, H[c1][a]), snap(b, H[c1][b])
        if c2 is None:
            h2a, h2b = base_z, base_z
        else:
            h2a, h2b = snap(a, H[c2][a]), snap(b, H[c2][b])
        if abs(h1a - h2a) < 1e-9 and abs(h1b - h2b) < 1e-9:
            continue
        s = 1.0 if (h1a - h2a) + (h1b - h2b) > 0 else -1.0
        lo_a, hi_a = min(h1a, h2a), max(h1a, h2a)
        lo_b, hi_b = min(h1b, h2b), max(h1b, h2b)
        A = [z for z in levels[a] if lo_a - 1e-9 <= z <= hi_a + 1e-9]
        B = [z for z in levels[b] if lo_b - 1e-9 <= z <= hi_b + 1e-9]
        d = xy[b] - xy[a]
        right = np.array([d[1], -d[0], 0.0])
        want = s * right
        chart = {"kind": "wall", "a": a, "b": b, "faces": []}
        i = j = 0
        tris = []
        while i < len(A) - 1 or j < len(B) - 1:
            if i == len(A) - 1 or (j < len(B) - 1 and B[j + 1] < A[i + 1]):
                tris.append(((a, A[i]), (b, B[j]), (b, B[j + 1])))
                j += 1
            else:
                tris.append(((a, A[i]), (b, B[j]), (a, A[i + 1])))
                i += 1
        for t in tris:
            f = [vmap[(v, round(z, 6))] for v, z in t]
            p = np.asarray([verts3[k] for k in f])
            n = np.cross(p[1] - p[0], p[2] - p[0])
            if n @ want < 0:
                f = [f[0], f[2], f[1]]
            chart["faces"].append(len(faces))
            faces.append(f)
            mats.append(wall_mat)
        if chart["faces"]:
            charts.append(chart)

    verts = np.asarray(verts3, float)
    faces = np.asarray(faces, np.int64)
    uv0, side = uv_charts(verts, faces, charts)
    return {"verts": verts, "faces": faces.astype(np.int32), "face_mat": np.asarray(mats, np.uint8),
            "uv0": uv0, "meta": {"uv_m_per_unit": side}, "charts": len(charts)}


def _chart_frame(P, n):
    """Two in-plane unit axes for a chart with normal n (u horizontal when possible)."""
    n = n / max(np.linalg.norm(n), 1e-12)
    up = np.array([0.0, 0.0, 1.0])
    e1 = np.cross(up, n)
    if np.linalg.norm(e1) < 1e-6:
        e1 = np.array([1.0, 0.0, 0.0])
        if n[2] < 0:
            e1 = -e1
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(n, e1)
    return e1, e2


def uv_charts(verts, faces, charts, padding=0.05):
    """Per-chart planar projection at true scale, then shelf packing into 0..1."""
    local, sizes = [], []
    for ch in charts:
        f = faces[ch["faces"]]
        P = verts[f]                                   # (k, 3, 3)
        nsum = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]).sum(axis=0)
        e1, e2 = _chart_frame(P.reshape(-1, 3), nsum)
        uv = np.stack([P @ e1, P @ e2], axis=-1)       # (k, 3, 2), counter-clockwise seen from outside
        mn = uv.reshape(-1, 2).min(axis=0)
        uv = uv - mn
        mx = uv.reshape(-1, 2).max(axis=0)
        local.append(uv)
        sizes.append((max(mx[0], 1e-3), max(mx[1], 1e-3)))
    offsets, side = pack_charts(sizes, padding)
    uv0 = np.zeros((len(faces), 3, 2), np.float32)
    for ch, uv, off in zip(charts, local, offsets):
        uv0[ch["faces"]] = (uv + np.asarray(off)) / side
    return np.clip(uv0, 0.0, 1.0), float(side)


# ---- colliders ---------------------------------------------------------------

def convex_pieces(poly, tol=0.3, max_pieces=12):
    """Split a polygon into convex pieces (triangulate, then Hertel-Mehlhorn merging).

    The polygon is first simplified by up to tol meters (allowed collider deviation).
    Triangles are merged across their shared edges (longest first) while the union stays
    convex; each shared edge is tried once, so this is fast even for big footprints."""
    p = poly.simplify(tol, preserve_topology=True)
    if p.is_empty or p.geom_type != "Polygon":
        p = poly
    if p.convex_hull.area - p.area < 0.02 * p.area and not p.interiors:
        return [p.convex_hull]
    tris = [Polygon(t) for t in _cdt(p)]
    if not tris:
        return [p.convex_hull]
    parent = list(range(len(tris)))
    shape = {i: t for i, t in enumerate(tris)}

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    edges = {}
    for i, t in enumerate(tris):
        c = [_key(x, y) for x, y in t.exterior.coords[:-1]]
        for k in range(3):
            e = tuple(sorted((c[k], c[(k + 1) % 3])))
            edges.setdefault(e, []).append(i)
    diag = [(e, ts) for e, ts in edges.items() if len(ts) == 2]
    diag.sort(key=lambda d: -math.hypot(d[0][0][0] - d[0][1][0], d[0][0][1] - d[0][1][1]))
    for _, (i, j) in diag:
        a, b = find(i), find(j)
        if a == b:
            continue
        u = shapely.union(shape[a], shape[b])
        if u.geom_type != "Polygon" or u.interiors:
            continue
        if u.convex_hull.area - u.area <= 1e-6 * max(u.area, 1.0):
            parent[b] = a
            shape[a] = shapely.convex_hull(u)
            del shape[b]
    return [shape[k] for k in shape]


def prism_hull(piece, base_z, top_pts):
    """Convex hull of a convex 2D piece at base_z and the given 3D top points."""
    import trimesh
    xy = np.asarray(piece.exterior.coords)[:-1]
    P = np.vstack([np.c_[xy, np.full(len(xy), base_z)], np.asarray(top_pts)])
    hull = trimesh.convex.convex_hull(P)
    return hull
