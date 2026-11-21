"""Roof planes and the 2.5D roof partition for one building (Stage 1 LoD2).

Steps (fit_roof does them in order):
 1. Roof points: class 6 inside the footprint grown by 0.5 m (roof overhang), plus
    class 1 points well inside the footprint and > 2.5 m above ground (towers are often
    left unclassified). Vegetation and noise classes are dropped.
 2. Normals from the 10 nearest neighbors (PCA).
 3. Region growing: start at the flattest points, add neighbors whose normal is within
    10 degrees and that lie within 0.10 m of the region's plane. Regions smaller than
    about 4 m2 are dropped. Nearly coplanar neighbor regions are merged.
    If much of the roof is left over (curved roofs), a looser second pass cuts it into
    facets and the roof is flagged "curved".
 4. Label grid (0.25 m, 0.5 m for big roofs): each cell takes the plane most of its points
    belong to; empty cells take the nearest label; a majority filter and a small-region
    clean-up smooth it.
 5. Lines: where two neighbor planes meet at about the same height, the exact plane
    intersection (ridge, hip, valley) is used. Where heights jump, the region outline is
    simplified, snapped to the building's wall directions and fitted to the step.
 6. Partition: the footprint is cut by all lines (each extended across the footprint);
    every cell takes the plane most of its grid cells voted for; neighbor cells with the
    same plane are merged. Cells = flat roof polygons, each on one plane.
 7. Roof equipment: points well above the model roof are grouped. Boxes at least 0.5 m high
    and 2 m wide become roof volumes (one more partition pass); smaller things go to the
    ledger (merge rule, STANDARDS.md section 4).

The result feeds roof_mesh.build_solid (watertight mesh) and buildings_lod2.py.
"""
import math

import numpy as np
import shapely
from scipy import ndimage
from scipy.spatial import cKDTree
from shapely.geometry import LineString, Polygon, MultiPolygon, Point, box as sbox
from shapely.ops import polygonize, unary_union

import buildings_common as bc

FLAT_DEG = 6.0           # planes flatter than this count as flat
MERGE_MIN = 0.5          # LoD2 merge threshold (m): smaller features are ledgered, not modeled


# ---- planes -----------------------------------------------------------------

class Plane:
    """z = a*x + b*y + c, fitted to points; slope and aspect for reporting."""

    def __init__(self, a, b, c, rms=0.0, n=0, kind="strict"):
        self.a, self.b, self.c = float(a), float(b), float(c)
        self.rms, self.n, self.kind = float(rms), int(n), kind
        self.area = 0.0
        self.zlo = self.zhi = None      # optional clip: the height range the LiDAR saw

    def z(self, x, y):
        v = self.a * np.asarray(x) + self.b * np.asarray(y) + self.c
        if self.zlo is not None:
            v = np.clip(v, self.zlo, self.zhi)
        return v

    @property
    def slope_deg(self):
        return math.degrees(math.atan(math.hypot(self.a, self.b)))

    @property
    def aspect_deg(self):
        """Down-slope direction, degrees counter-clockwise from +X (east)."""
        return math.degrees(math.atan2(-self.b, -self.a)) % 360

    @property
    def normal(self):
        n = np.array([-self.a, -self.b, 1.0])
        return n / np.linalg.norm(n)

    def to_json(self):
        return {"a": round(self.a, 6), "b": round(self.b, 6), "c": round(self.c, 4),
                "slope_deg": round(self.slope_deg, 2), "aspect_deg": round(self.aspect_deg, 1),
                "rms_m": round(self.rms, 4), "points": self.n, "area_m2": round(self.area, 2),
                "kind": self.kind,
                "zlo": None if self.zlo is None else round(float(self.zlo), 3),
                "zhi": None if self.zhi is None else round(float(self.zhi), 3)}


def fit_plane(P, robust=True):
    """Least-squares plane z = ax + by + c (trimmed once at 2.5 sigma)."""
    A = np.c_[P[:, 0] - P[:, 0].mean(), P[:, 1] - P[:, 1].mean(), np.ones(len(P))]
    sol, *_ = np.linalg.lstsq(A, P[:, 2], rcond=None)
    r = P[:, 2] - A @ sol
    if robust and len(P) > 10:
        s = max(np.sqrt(np.mean(r ** 2)), 0.01)
        keep = np.abs(r) < 2.5 * s
        if keep.sum() >= 3:
            sol, *_ = np.linalg.lstsq(A[keep], P[keep, 2], rcond=None)
            r = P[:, 2] - A @ sol
            r = r[keep]
    a, b, c0 = sol
    c = c0 - a * P[:, 0].mean() - b * P[:, 1].mean()
    return Plane(a, b, c, rms=float(np.sqrt(np.mean(r ** 2))), n=len(P))


def normals(P, k=10):
    """Unit normals (pointing up) and curvature from the k nearest neighbors."""
    tree = cKDTree(P)
    k = min(k, len(P))
    _, idx = tree.query(P, k=k)
    Q = P[idx] - P[idx].mean(axis=1, keepdims=True)
    C = np.einsum("nki,nkj->nij", Q, Q) / k
    w, v = np.linalg.eigh(C)
    N = v[:, :, 0]
    N[N[:, 2] < 0] *= -1
    curv = w[:, 0] / np.maximum(w.sum(axis=1), 1e-12)
    return N, curv, idx


def grow_regions(P, N, curv, knn, angle_deg=10.0, dist=0.10, min_pts=20, seed_curv=0.02,
                 label=None, first_id=0):
    """Region growing on the kNN graph. Returns labels (-1 = no region)."""
    n = len(P)
    label = np.full(n, -1, np.int64) if label is None else label.copy()
    tried = label >= 0
    cosmax = math.cos(math.radians(angle_deg))
    rid = first_id
    for seed in np.argsort(curv):
        if tried[seed] or curv[seed] > seed_curv or N[seed, 2] < 0.25:
            continue
        nrm, cen = N[seed].copy(), P[seed].copy()
        members = [np.array([seed])]
        label[seed] = rid
        tried[seed] = True
        frontier = np.array([seed])
        size, last_fit = 1, 1
        while frontier.size:
            nb = np.unique(knn[frontier].ravel())
            nb = nb[label[nb] < 0]
            if not nb.size:
                break
            ok = (np.abs((P[nb] - cen) @ nrm) < dist) & (np.abs(N[nb] @ nrm) > cosmax)
            nb = nb[ok]
            label[nb] = rid
            members.append(nb)
            size += nb.size
            frontier = nb
            if size > 1.5 * last_fit and size >= 6:
                allm = np.concatenate(members)
                Q = P[allm]
                cen = Q.mean(axis=0)
                w, v = np.linalg.eigh(np.cov((Q - cen).T))
                nrm = v[:, 0] if v[2, 0] >= 0 else -v[:, 0]
                last_fit = size
        allm = np.concatenate(members)
        tried[allm] = True
        if allm.size < min_pts:
            label[allm] = -1
        else:
            rid += 1
    return label


def relabel(label):
    u = np.unique(label[label >= 0])
    m = {int(o): i for i, o in enumerate(u)}
    out = np.full_like(label, -1)
    for o, i in m.items():
        out[label == o] = i
    return out


def merge_coplanar(P, label, knn, angle_deg=6.0, tol=0.08):
    """Merge neighbor regions whose planes agree (angle and point residuals)."""
    label = relabel(label)
    k = label.max() + 1
    if k <= 1:
        return label
    planes = [fit_plane(P[label == i]) for i in range(k)]
    a = np.repeat(label, knn.shape[1])
    b = label[knn.ravel()]
    ok = (a >= 0) & (b >= 0) & (a != b)
    pairs = np.unique(np.sort(np.c_[a[ok], b[ok]], axis=1), axis=0)
    parent = list(range(k))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    cosmax = math.cos(math.radians(angle_deg))
    for i, j in pairs:
        pi, pj = planes[i], planes[j]
        if abs(pi.normal @ pj.normal) < cosmax:
            continue
        Pi, Pj = P[label == i], P[label == j]
        ri = np.sqrt(np.mean((Pj[:, 2] - pi.z(Pj[:, 0], Pj[:, 1])) ** 2))
        rj = np.sqrt(np.mean((Pi[:, 2] - pj.z(Pi[:, 0], Pi[:, 1])) ** 2))
        if max(ri, rj) < tol:
            parent[find(i)] = find(j)
    roots = np.array([find(i) for i in range(k)])
    out = np.where(label >= 0, roots[np.maximum(label, 0)], -1)
    return relabel(out)


def absorb_leftovers(P, label, knn, planes, tol=0.15):
    """Give unlabeled points to a neighbor plane they fit within tol."""
    for _ in range(3):
        free = np.nonzero(label < 0)[0]
        if not free.size:
            break
        cand = label[knn[free]]
        best = np.full(free.size, -1)
        bestr = np.full(free.size, np.inf)
        for col in range(cand.shape[1]):
            l = cand[:, col]
            ok = l >= 0
            if not ok.any():
                continue
            r = np.full(free.size, np.inf)
            idx = np.nonzero(ok)[0]
            for pid in np.unique(l[ok]):
                s = idx[l[idx] == pid]
                pl = planes[pid]
                r[s] = np.abs(P[free[s], 2] - pl.z(P[free[s], 0], P[free[s], 1]))
            better = r < bestr
            bestr[better] = r[better]
            best[better] = l[better]
        take = bestr < tol
        if not take.any():
            break
        label[free[take]] = best[take]
    return label


def segment(P, density, min_area=4.0):
    """Planes for a roof point cloud. Returns (label per point, planes, info)."""
    info = {"points": int(len(P))}
    if len(P) < 12:
        return np.full(len(P), -1), [], {**info, "coverage": 0.0}
    N, curv, knn = normals(P, 10)
    min_pts = max(10, int(0.6 * min_area * density))
    label = grow_regions(P, N, curv, knn, 10.0, 0.10, min_pts)
    label = merge_coplanar(P, label, knn)
    planes = [fit_plane(P[label == i]) for i in range(label.max() + 1)]
    label = absorb_leftovers(P, label, knn, planes)
    cov1 = float(np.mean(label >= 0))
    info["coverage_strict"] = round(cov1, 3)
    curved = False
    if cov1 < 0.8 and np.sum(label < 0) > 3 * min_pts:
        # second, looser pass on what is left: facets for curved or rough roofs
        k0 = label.max() + 1
        lab2 = grow_regions(P, N, curv, knn, 22.0, 0.25, max(8, min_pts // 2), seed_curv=0.08,
                            label=label, first_id=k0)
        new = lab2 >= k0
        if new.sum() > 0.15 * len(P):
            curved = True
        label = lab2
    label = relabel(label)
    planes = []
    for i in range(label.max() + 1):
        pl = fit_plane(P[label == i])
        planes.append(pl)
    # mark loose planes
    if curved:
        for i, pl in enumerate(planes):
            if pl.rms > 0.08:
                pl.kind = "loose"
    label = absorb_leftovers(P, label, knn, planes, tol=0.2 if curved else 0.15)
    info["coverage"] = round(float(np.mean(label >= 0)), 3)
    info["curved"] = curved
    return label, planes, info


# ---- label grid -------------------------------------------------------------

class LabelGrid:
    def __init__(self, footprint, cell):
        import rasterio.features
        from rasterio.transform import Affine
        x0, y0, x1, y1 = footprint.bounds
        self.cell = cell
        self.x0, self.y0 = x0 - cell, y0 - cell
        self.w = int(math.ceil((x1 - x0) / cell)) + 2
        self.h = int(math.ceil((y1 - y0) / cell)) + 2
        self.tr = Affine(cell, 0, self.x0, 0, cell, self.y0)   # row 0 = south edge
        self.inside = rasterio.features.rasterize([footprint], out_shape=(self.h, self.w),
                                                  transform=self.tr).astype(bool)
        # cells too small to have a center inside (thin wings) still count if touched
        if self.inside.sum() < 4:
            self.inside = rasterio.features.rasterize([footprint], out_shape=(self.h, self.w),
                                                      transform=self.tr, all_touched=True).astype(bool)

    def rc(self, x, y):
        c = np.floor((np.asarray(x) - self.x0) / self.cell).astype(np.int64)
        r = np.floor((np.asarray(y) - self.y0) / self.cell).astype(np.int64)
        return np.clip(r, 0, self.h - 1), np.clip(c, 0, self.w - 1)

    def centers(self):
        rr, cc = np.mgrid[0:self.h, 0:self.w]
        return self.x0 + (cc + 0.5) * self.cell, self.y0 + (rr + 0.5) * self.cell

    def rasterize(self, geoms_values):
        import rasterio.features
        return rasterio.features.rasterize(geoms_values, out_shape=(self.h, self.w), transform=self.tr,
                                           fill=-1, dtype=np.int32)


def label_grid(P, label, nplanes, grid, smooth=1):
    """Cell labels from point votes, holes filled by nearest label, majority-smoothed."""
    r, c = grid.rc(P[:, 0], P[:, 1])
    ok = label >= 0
    votes = np.zeros((grid.h * grid.w, nplanes), np.int32)
    np.add.at(votes, (r[ok] * grid.w + c[ok], label[ok]), 1)
    has = votes.sum(axis=1) > 0
    lab = np.where(has, votes.argmax(axis=1), -1).reshape(grid.h, grid.w)
    if not has.any():
        return np.where(grid.inside, -1, -1)
    known = lab >= 0
    _, (ir, ic) = ndimage.distance_transform_edt(~known, return_indices=True)
    lab = lab[ir, ic]
    for _ in range(smooth):
        best = np.zeros(lab.shape, np.float32) - 1
        out = lab.copy()
        for k in range(nplanes):
            s = ndimage.uniform_filter((lab == k).astype(np.float32), 3, mode="nearest")
            take = s > best
            out[take] = k
            best[take] = s[take]
        lab = out
    lab[~grid.inside] = -1
    return lab


def clean_small_regions(lab, grid, min_area):
    """Regions under min_area take the label they share the longest border with."""
    for _ in range(3):
        changed = False
        for k in np.unique(lab[lab >= 0]):
            comp, n = ndimage.label(lab == k)
            if n == 0:
                continue
            sizes = ndimage.sum(np.ones_like(comp), comp, np.arange(1, n + 1)) * grid.cell ** 2
            for ci in np.nonzero(sizes < min_area)[0] + 1:
                m = comp == ci
                ring = ndimage.binary_dilation(m) & ~m & (lab >= 0)
                if not ring.any():
                    continue
                vals, cnt = np.unique(lab[ring], return_counts=True)
                lab[m] = vals[np.argmax(cnt)]
                changed = True
        if not changed:
            break
    return lab


# ---- lines and partition -----------------------------------------------------

def _snap_angle(theta, dirs, tol_deg=15.0):
    """Snap a line direction to the closest wall direction within tol (mod 180).
    Returns (angle, snapped?)."""
    best, bd, ok = theta, math.radians(tol_deg), False
    for d in dirs:
        diff = (theta - d + math.pi / 2) % math.pi - math.pi / 2
        if abs(diff) < bd:
            best, bd, ok = d % math.pi, abs(diff), True
    return best, ok


def wall_directions(footprint):
    """Dominant direction (and its right angle) plus directions of long footprint edges."""
    th = bc.dominant_angle(footprint)
    dirs = [th, th + math.pi / 2]
    for p in bc.polys_of(footprint):
        xy = np.asarray(p.exterior.coords)
        d = np.diff(xy, axis=0)
        L = np.hypot(d[:, 0], d[:, 1])
        for (dx, dy), l in zip(d, L):
            if l > 4.0:
                dirs.append(math.atan2(dy, dx) % math.pi)
    return dirs


# A "segment" is (p0, p1, kind): kind "join" (exact plane intersection) or "step".

def merge_grid_coplanar(lab, grid, planes, P, label, angle_deg=3.0, tol=0.10):
    """Merge labels that touch in the grid and lie on the same plane (same height there).

    Pairs are merged greedily (closest first) and only while one plane still fits the
    merged points well, so small differences cannot pile up along a chain."""
    k = len(planes)
    parent = list(range(k))
    members = {i: np.nonzero(label == i)[0] for i in range(k)}
    rms = {i: max(planes[i].rms, 0.01) for i in range(k)}

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    xs, ys = grid.centers()
    cosmax = math.cos(math.radians(angle_deg))
    cand = []
    for dr, dc in ((0, 1), (1, 0)):
        a = lab[: lab.shape[0] - dr, : lab.shape[1] - dc]
        b = lab[dr:, dc:]
        ok = (a >= 0) & (b >= 0) & (a != b)
        if not ok.any():
            continue
        pa, pb = a[ok], b[ok]
        x, y = xs[: lab.shape[0] - dr, : lab.shape[1] - dc][ok], ys[: lab.shape[0] - dr, : lab.shape[1] - dc][ok]
        for i, j in set(zip(pa.tolist(), pb.tolist())):
            if i > j:
                continue
            s = ((pa == i) & (pb == j)) | ((pa == j) & (pb == i))
            if abs(planes[i].normal @ planes[j].normal) < cosmax:
                continue
            dz = float(np.median(np.abs(planes[i].z(x[s], y[s]) - planes[j].z(x[s], y[s]))))
            if dz < tol:
                cand.append((dz, i, j))
    changed = False
    newfit = {}
    for dz, i, j in sorted(cand):
        ri, rj = find(i), find(j)
        if ri == rj:
            continue
        m = np.concatenate([members[ri], members[rj]])
        if len(m) < 3:
            continue
        pl = fit_plane(P[m])
        if pl.rms > max(0.05, 1.3 * max(rms[ri], rms[rj])):
            continue
        parent[ri] = rj
        members[rj] = m
        rms[rj] = max(pl.rms, 0.01)
        newfit[rj] = pl
        changed = True
    if not changed:
        return lab, planes, label
    roots = np.array([find(i) for i in range(k)])
    u = np.unique(roots)
    mp = {int(r): n for n, r in enumerate(u)}
    newmap = np.array([mp[int(r)] for r in roots])
    lab = np.where(lab >= 0, newmap[np.maximum(lab, 0)], -1)
    label = np.where(label >= 0, newmap[np.maximum(label, 0)], -1)
    newp = []
    for r in u:
        pl = newfit.get(int(r), planes[int(r)])
        pl.kind = planes[int(r)].kind
        newp.append(pl)
    return lab, newp, label


def region_segments(lab, grid, footprint, join_pairs, dirs, tol=0.5):
    """Step segments from the outlines of label regions, snapped to wall directions."""
    import rasterio.features
    segs = []
    fb = footprint.boundary
    for geom, val in rasterio.features.shapes(lab.astype(np.int32), mask=lab >= 0, transform=grid.tr):
        poly = shapely.geometry.shape(geom)
        for ring in [poly.exterior] + list(poly.interiors):
            xy = np.asarray(ring.coords)[:-1]
            if len(xy) < 4:
                continue
            simp = np.asarray(LineString(np.vstack([xy, xy[:1]])).simplify(tol).coords)[:-1]
            if len(simp) < 3:
                continue
            idx = [int(np.argmin(np.hypot(xy[:, 0] - x, xy[:, 1] - y))) for x, y in simp]
            for k in range(len(simp)):
                a, b = simp[k], simp[(k + 1) % len(simp)]
                L = float(np.hypot(*(b - a)))
                if L < 0.6:
                    continue
                theta0 = math.atan2(b[1] - a[1], b[0] - a[0]) % math.pi
                theta, snapped = _snap_angle(theta0, dirs)
                if not snapped and L < 3.0:
                    continue                    # short edges in odd directions are label noise
                u = np.array([math.cos(theta), math.sin(theta)])
                nrm = np.array([-u[1], u[0]])
                i0, i1 = idx[k], idx[(k + 1) % len(simp)]
                run = xy[i0:i1 + 1] if i1 >= i0 else np.vstack([xy[i0:], xy[:i1 + 1]])
                rho = float(np.median(run @ nrm))
                t = run @ u
                t0, t1 = float(t.min()), float(t.max())
                p0 = u * t0 + nrm * rho
                p1 = u * t1 + nrm * rho
                mid = (p0 + p1) / 2
                # skip edges lying on the footprint outline (the outline is already a cut)
                inside_a = footprint.contains(Point(mid + nrm * 0.5))
                inside_b = footprint.contains(Point(mid - nrm * 0.5))
                if inside_a != inside_b and fb.distance(Point(mid)) < 0.6:
                    continue
                if not inside_a and not inside_b:
                    continue
                ra, ca = grid.rc(*(mid + nrm * 0.4))
                rb, cb = grid.rc(*(mid - nrm * 0.4))
                la, lb = int(lab[ra, ca]), int(lab[rb, cb])
                if la >= 0 and lb >= 0 and la != lb and (min(la, lb), max(la, lb)) in join_pairs:
                    continue                    # replaced by the exact plane intersection
                segs.append((p0, p1, "step"))
    return segs


def join_segments(lab, grid, planes, max_dz=0.3):
    """Exact plane-plane intersection segments where neighbor planes meet at the same height."""
    pairs = {}
    for dr, dc in ((0, 1), (1, 0)):
        a = lab[: lab.shape[0] - dr, : lab.shape[1] - dc]
        b = lab[dr:, dc:]
        ok = (a >= 0) & (b >= 0) & (a != b)
        rr, cc = np.nonzero(ok)
        for la, lb, r, c in zip(a[ok], b[ok], rr, cc):
            key = (int(min(la, lb)), int(max(la, lb)))
            pairs.setdefault(key, []).append((r + dr / 2, c + dc / 2))
    out, joins = [], set()
    for (i, j), cells in pairs.items():
        if len(cells) * grid.cell < 1.0:
            continue
        cells = np.asarray(cells)
        x = grid.x0 + (cells[:, 1] + 0.5) * grid.cell
        y = grid.y0 + (cells[:, 0] + 0.5) * grid.cell
        pi, pj = planes[i], planes[j]
        dz = np.abs(pi.z(x, y) - pj.z(x, y))
        da, db = pi.a - pj.a, pi.b - pj.b
        g = math.hypot(da, db)
        if np.median(dz) > max_dz or g < math.tan(math.radians(5.0)):
            continue
        nrm = np.array([da, db]) / g            # line: nrm . p = rho
        rho = -(pi.c - pj.c) / g
        d = np.abs(x * nrm[0] + y * nrm[1] - rho)
        if np.median(d) > 0.75:
            continue
        u = np.array([-nrm[1], nrm[0]])
        t = x * u[0] + y * u[1]
        p0 = nrm * rho + u * float(t.min())
        p1 = nrm * rho + u * float(t.max())
        out.append((p0, p1, "join"))
        joins.add((i, j))
    return out, joins


def dedupe_segments(segs, ang_tol=4.0, off_tol=0.3, gap=0.5):
    """Merge segments on the same line whose extents overlap (joins win over steps)."""
    order = sorted(segs, key=lambda s: (s[2] != "join", -float(np.hypot(*(s[1] - s[0])))))
    kept = []
    for p0, p1, kind in order:
        d = p1 - p0
        L = float(np.hypot(*d))
        if L < 1e-6:
            continue
        u = d / L
        nrm = np.array([-u[1], u[0]])
        merged = False
        for k, (q0, q1, kk) in enumerate(kept):
            e = q1 - q0
            Le = float(np.hypot(*e))
            v = e / Le
            if abs(u[0] * v[1] - u[1] * v[0]) > math.sin(math.radians(ang_tol)):
                continue
            n2 = np.array([-v[1], v[0]])
            if abs((p0 - q0) @ n2) > off_tol or abs((p1 - q0) @ n2) > off_tol:
                continue
            a, b = sorted([(p0 - q0) @ v, (p1 - q0) @ v])
            if b < -gap or a > Le + gap:
                continue
            lo, hi = min(0.0, a), max(Le, b)
            kept[k] = (q0 + v * lo, q0 + v * hi, kk)
            merged = True
            break
        if not merged:
            kept.append((p0, p1, kind))
    return kept


def extend_segments(segs, footprint, reach=4.0, default=1.0, margin=3.0, link=1.5):
    """Extend each segment end along its line until it hits another segment (or the outline)
    within reach meters, so region outlines close even where a corner was cut off.
    An end that hits nothing is also linked to the nearest other segment end within link
    meters (this closes small jogs that were too short to become segments)."""
    if not segs:
        return []
    base = []
    for p0, p1, _ in segs:
        d = p1 - p0
        u = d / max(float(np.hypot(*d)), 1e-9)
        base.append(LineString([p0 - u * margin, p1 + u * margin]))
    tree = shapely.STRtree(base)
    fb = footprint.boundary
    out = []
    loose = []
    allends = np.array([p for p0, p1, _ in segs for p in (p0, p1)])
    for i, (p0, p1, _) in enumerate(segs):
        d = p1 - p0
        u = d / max(float(np.hypot(*d)), 1e-9)
        ends = []
        for p, s in ((p0, -1.0), (p1, 1.0)):
            ray = LineString([p + s * u * 0.02, p + s * u * reach])
            best = None
            for j in tree.query(ray, predicate="intersects"):
                if int(j) == i:
                    continue
                x = ray.intersection(base[int(j)])
                for q in getattr(x, "geoms", [x]):
                    if q.geom_type == "Point":
                        dist = float(np.hypot(q.x - p[0], q.y - p[1]))
                        best = dist if best is None else min(best, dist)
            x = ray.intersection(fb)
            for q in getattr(x, "geoms", [x]):
                if q.geom_type == "Point":
                    dist = float(np.hypot(q.x - p[0], q.y - p[1]))
                    best = dist if best is None else min(best, dist)
            ends.append(p + s * u * ((best + 0.05) if best is not None else default))
            if best is None:
                loose.append((i, p))
        out.append(LineString([ends[0], ends[1]]))
    for i, p in loose:
        dd = np.hypot(allends[:, 0] - p[0], allends[:, 1] - p[1])
        dd[2 * i:2 * i + 2] = np.inf
        j = int(np.argmin(dd))
        if dd[j] < link:
            q = allends[j]
            v = (q - p) / max(dd[j], 1e-9)
            out.append(LineString([p - v * 0.05, q + v * 0.05]))
    return out


def lost_region_rings(cells, lab, grid, min_area=2.0, tol=0.35):
    """Outlines of label regions that the partition lost (their loop did not close).

    A region is lost when less than half of its grid cells ended up in a cell with its label.
    Its simplified outline is returned so the next partition pass cuts it out for sure."""
    import rasterio.features
    got = grid.rasterize([(c, int(l)) for c, l in cells])
    rings = []
    for k in np.unique(lab[lab >= 0]):
        comp, n = ndimage.label(lab == k)
        for ci in range(1, n + 1):
            m = comp == ci
            if m.sum() * grid.cell ** 2 < min_area:
                continue
            if np.mean(got[m] == k) >= 0.5:
                continue
            for geom, v in rasterio.features.shapes(m.astype(np.uint8), mask=m, transform=grid.tr):
                poly = shapely.geometry.shape(geom).simplify(tol)
                if poly.is_valid and not poly.is_empty:
                    rings.append(poly.exterior)
                    rings += list(poly.interiors)
    return rings


def partition(footprint, segs, lab, grid, nplanes, extra=()):
    """Cut the footprint by the segments (extended to close their loops); label cells by
    majority; merge same-label cells. Returns a list of (Polygon, label), consistently noded."""
    lines = [footprint.boundary] + extend_segments(segs, footprint) + list(extra)
    net = unary_union(lines)
    faces = [f for f in polygonize(net) if f.area > 1e-6]
    faces = [f for f in faces if footprint.contains(f.representative_point())]
    if not faces:
        return []
    ids = grid.rasterize([(f, i) for i, f in enumerate(faces)])
    labs = np.full(len(faces), -1)
    sel = (ids >= 0) & (lab >= 0)
    if sel.any():
        cnt = np.zeros((len(faces), nplanes), np.int64)
        np.add.at(cnt, (ids[sel], lab[sel]), 1)
        has = cnt.sum(axis=1) > 0
        labs[has] = cnt[has].argmax(axis=1)
    known = np.argwhere(lab >= 0)
    for i in np.nonzero(labs < 0)[0]:
        p = faces[i].representative_point()
        r, c = grid.rc(p.x, p.y)
        l = lab[r, c]
        if l < 0:
            if not len(known):
                l = 0
            else:
                d = np.hypot(known[:, 0] - r, known[:, 1] - c)
                l = lab[tuple(known[np.argmin(d)])]
        labs[i] = l
    return dissolve(faces, labs, footprint)


def dissolve(faces, labs, footprint):
    """Merge same-label faces and re-node all boundaries so neighbors share vertices."""
    groups = {}
    for f, l in zip(faces, labs):
        groups.setdefault(int(l), []).append(f)
    merged = []
    for l, fs in groups.items():
        u = unary_union(fs)
        for p in bc.polys_of(u):
            merged.append((p, l))
    # tiny cells (< 0.3 m2) join the neighbor they share most border with
    big = [(p, l) for p, l in merged if p.area >= 0.3]
    small = [(p, l) for p, l in merged if p.area < 0.3]
    if small and big:
        btree = shapely.STRtree([q for q, _ in big])
        for p, l in small:
            best, bl = None, -1.0
            pb = p.buffer(1e-6)
            for k in btree.query(pb, predicate="intersects"):
                k = int(k)
                s = pb.intersection(big[k][0].boundary).length
                if s > bl:
                    best, bl = k, s
            if best is not None and bl > 0:
                q, lq = big[best]
                big[best] = (unary_union([q, p]), lq)
        merged = []
        for q, lq in big:
            for p in bc.polys_of(q):
                merged.append((p, lq))
    # re-node
    net = unary_union([p.boundary for p, _ in merged])
    out = []
    lookup = shapely.STRtree([p for p, _ in merged])
    for f in polygonize(net):
        if f.area < 1e-6:
            continue
        rp = f.representative_point()
        if not footprint.buffer(1e-6).contains(rp):
            continue
        cand = [int(k) for k in lookup.query(rp, predicate="intersects")]
        if not cand:
            continue
        k = max(cand, key=lambda k: merged[k][0].intersection(f).area)
        out.append((f, merged[k][1]))
    return out


# ---- the whole fit -----------------------------------------------------------

def roof_point_selection(pts, footprint, others, ground_z):
    """Indices of the roof points of one building."""
    x, y, z, c = pts["x"], pts["y"], pts["z"], pts["classification"]
    grown = footprint.buffer(0.5)
    if others is not None:
        grown = grown.difference(others)
    inner = footprint.buffer(-0.5)
    in_g = shapely.contains_xy(grown, x, y)
    sel = in_g & (c == 6)
    if ground_z is not None:
        sel &= z > ground_z + 1.0
        if not inner.is_empty:
            sel |= (c == 1) & (z > ground_z + 2.5) & shapely.contains_xy(inner, x, y)
    return np.nonzero(sel)[0]


def classify_part(planes_in, areas, curved):
    """Roof type of one part from its planes (see module help)."""
    tot = sum(areas) or 1.0
    big = [(p, a) for p, a in zip(planes_in, areas) if a / tot >= 0.05]
    if curved:
        return "curved"
    sloped = [(p, a) for p, a in big if p.slope_deg >= FLAT_DEG]
    if not sloped:
        return "flat"
    flat_share = sum(a for p, a in big if p.slope_deg < FLAT_DEG) / tot
    asp = [p.aspect_deg for p, _ in sloped]
    if len(sloped) == 1:
        return "shed" if flat_share < 0.5 else "flat"

    def dirs(angles, tol=25):
        groups = []
        for a in angles:
            for g in groups:
                if abs((a - g[0] + 180) % 360 - 180) < tol:
                    g.append(a)
                    break
            else:
                groups.append([a])
        return groups
    groups = dirs(asp)
    nd = len(groups)
    steep = [p.slope_deg for p, _ in sloped]
    two_levels = any(len(g) >= 2 for g in groups) and (max(steep) - min(steep) > 15)
    if flat_share > 0.25 and nd >= 3 and min(steep) > 40:
        return "mansard"
    if nd == 2:
        a, b = groups[0][0], groups[1][0]
        opposite = abs((a - b + 180) % 360 - 180) > 155
        if opposite:
            if two_levels and len(sloped) >= 4:
                return "gambrel"
            if len(sloped) == 2:
                return "gable"
            return "gable" if flat_share < 0.2 else "complex"
        return "complex"
    if nd == 4 or nd == 3:
        if two_levels and len(sloped) >= 6:
            return "mansard"
        if len(sloped) == nd:
            # hip vs pyramid: a pyramid's planes all reach the same top point
            return "hip"
        if len(sloped) <= nd + 2:
            return "hip"
    if nd >= 5 and len(sloped) == nd and np.std(steep) < 8:
        return "pyramidal"
    return "complex"


def fit_roof(pts, footprint, others, ground_z, base_z, density_hint=None, cell=None):
    """Fit planes and the 2.5D partition for one building footprint.

    Returns a dict: cells [(Polygon, plane_index)], planes [Plane], info, equipment,
    point arrays for checks. Raises nothing for weak data: it reports fallback reasons."""
    idx = roof_point_selection(pts, footprint, others, ground_z)
    P = np.c_[pts["x"][idx], pts["y"][idx], pts["z"][idx]]
    area = footprint.area
    density = len(P) / max(area, 1.0)
    res = {"P": P, "point_index": idx, "density": density, "info": {}, "cells": [], "planes": [],
           "equipment": [], "fallback": None}
    if len(P) < 12 or density < 2.0:
        res["fallback"] = (f"thin roof coverage ({density:.1f} pts/m2, {len(P)} points): glass roof, "
                           "trees over the roof, or built after the 2022 LiDAR")
        return res
    label, planes, info = segment(P, density)
    res["info"] = info
    if not planes or info["coverage"] < 0.4:
        res["fallback"] = f"plane fitting covered only {info.get('coverage', 0):.0%} of the roof points"
        res["label"] = label
        return res
    cell = cell or (0.25 if area < 4000 else 0.5)
    grid = LabelGrid(footprint, cell)
    dirs = wall_directions(footprint)

    def build(label, planes):
        lab = label_grid(P, label, len(planes), grid)
        lab = clean_small_regions(lab, grid, 2.0)
        lab, planes, label = merge_grid_coplanar(lab, grid, planes, P, label)
        jl, joins = join_segments(lab, grid, planes)
        sl = region_segments(lab, grid, footprint, joins, dirs)
        segs = dedupe_segments(jl + sl)
        cells = partition(footprint, segs, lab, grid, len(planes))
        lost = lost_region_rings(cells, lab, grid) if cells else []
        if lost:
            cells = partition(footprint, segs, lab, grid, len(planes), extra=lost)
            for r in lost:
                xy = np.asarray(r.coords)
                segs = segs + [(xy[i], xy[i + 1], "region") for i in range(len(xy) - 1)]
        return lab, cells, segs, planes, label

    N, _, _ = normals(P, 10)
    steep = np.abs(N[:, 2]) < 0.35            # wall points (steeper than about 70 degrees)
    merged_log = []
    lab, cells, lines, planes, label = build(label, planes)
    if not cells:
        res["fallback"] = "partition failed"
        return res
    cells, planes = refine_cells(cells, planes, P, steep, footprint, density, merged_log)
    # equipment: points well above the model roof (and above any roof within 0.75 m,
    # so points on the walls of height steps do not count)
    zm = model_z(cells, planes, P[:, 0], P[:, 1])
    resid = P[:, 2] - zm
    equipment, volumes = find_equipment(P, P[:, 2] - zmax_near(cells, planes, grid, P), footprint, grid,
                                        free=label < 0)
    if volumes:
        # boxes >= 0.5 m high and >= 2 m wide become flat roof volumes
        label0 = label
        label2 = label.copy()
        for v in volumes:
            planes.append(Plane(0.0, 0.0, v["top_z"], rms=v["rms"], n=v["n"], kind="volume"))
            inside = shapely.contains_xy(v["rect"], P[:, 0], P[:, 1])
            label2[inside] = len(planes) - 1
        label = label2
        lab, cells, lines, planes, label = build(label, planes)
        merged_log = []
        cells, planes = refine_cells(cells, planes, P, steep, footprint, density, merged_log)
        zm = model_z(cells, planes, P[:, 0], P[:, 1])
        resid = P[:, 2] - zm
        equipment, _ = find_equipment(P, P[:, 2] - zmax_near(cells, planes, grid, P), footprint, grid,
                                      allow_volumes=False, free=label0 < 0)
    tidy_planes(cells, planes, P, steep)
    zm = model_z(cells, planes, P[:, 0], P[:, 1])
    resid = P[:, 2] - zm
    used = sorted({l for _, l in cells})
    for k, pl in enumerate(planes):
        pl.area = sum(c.area for c, l in cells if l == k)
    res.update({"label": label, "planes": planes, "cells": cells, "lines": lines, "grid": grid,
                "lab": lab, "equipment": equipment + merged_log, "volumes": volumes, "resid": resid,
                "steep": steep,
                "used_planes": used})
    return res


def zmax_near(cells, planes, grid, P, size=3):
    """Highest model roof within about one grid cell of each point."""
    xs, ys = grid.centers()
    ins = grid.inside
    z = np.full(xs.shape, -np.inf)
    z[ins] = model_z(cells, planes, xs[ins], ys[ins])
    z = np.nan_to_num(z, nan=-np.inf)
    zmx = ndimage.maximum_filter(z, size=size, mode="nearest")
    r, c = grid.rc(P[:, 0], P[:, 1])
    out = zmx[r, c]
    zm = model_z(cells, planes, P[:, 0], P[:, 1])
    return np.where(np.isfinite(out), np.fmax(out, np.nan_to_num(zm, nan=-np.inf)), zm)


def refine_cells(cells, planes, P, steep, footprint, density, merged_log):
    """Check every cell against its own points and fix the plane choice.

    1. Each cell takes the plane (its own, a neighbor's, or one its points belong to) with the
       lowest median vertical residual. If no plane fits (median > 0.3 m) and the cell has enough
       points, a new plane is fitted to them.
    2. Cells under 15 m2 whose points also fit a neighbor's plane within 0.25 m (a step or tilt
       under the 0.5 m merge threshold) take the neighbor's plane; each merge is logged for the
       ledger.
    Returns (cells, planes) with same-plane neighbors merged."""
    if not cells:
        return cells, planes
    polys = [c for c, _ in cells]
    labs = [l for _, l in cells]
    tree = shapely.STRtree(polys)
    use = ~steep
    pi, gi = tree.query(shapely.points(P[:, :2]), predicate="within")
    keep = use[pi]
    pi, gi = pi[keep], gi[keep]
    order = np.argsort(gi, kind="stable")
    pi, gi = pi[order], gi[order]
    starts = np.searchsorted(gi, np.arange(len(polys) + 1))
    nbrs = []
    for i, p in enumerate(polys):
        nb = [int(j) for j in tree.query(p.buffer(0.05), predicate="intersects") if int(j) != i]
        nbrs.append(nb)

    def med_res(idx, k):
        q = P[idx]
        return float(np.median(np.abs(q[:, 2] - planes[k].z(q[:, 0], q[:, 1]))))
    min_pts = max(15, int(2.0 * density))
    # 1. best plane per cell
    for i in range(len(polys)):
        idx = pi[starts[i]:starts[i + 1]]
        if len(idx) < 6:
            continue
        cand = {labs[i]} | {labs[j] for j in nbrs[i]}
        best = min(cand, key=lambda k: med_res(idx, k))
        r = med_res(idx, best)
        if r > 0.3 and len(idx) >= min_pts:
            pl = fit_plane(P[idx])
            if pl.rms < 0.15 and med_res_plane(P[idx], pl) < r:
                pl.kind = "cell_fit"
                planes.append(pl)
                best = len(planes) - 1
        labs[i] = best
    # 2. merge small cells into neighbors they fit
    for i in sorted(range(len(polys)), key=lambda i: polys[i].area):
        if polys[i].area >= 15.0:
            continue
        idx = pi[starts[i]:starts[i + 1]]
        own = labs[i]
        cand = {labs[j] for j in nbrs[i]} - {own}
        if not cand:
            continue
        if len(idx) >= 4:
            k = min(cand, key=lambda k: med_res(idx, k))
            r = med_res(idx, k)
        else:
            c = polys[i].representative_point()
            k = min(cand, key=lambda k: abs(planes[k].z(c.x, c.y) - planes[own].z(c.x, c.y)))
            r = abs(planes[k].z(c.x, c.y) - planes[own].z(c.x, c.y))
        if r < 0.25:
            c = polys[i].centroid
            dz = float(planes[own].z(c.x, c.y) - planes[k].z(c.x, c.y))
            if abs(dz) > 0.05:
                x0, y0, x1, y1 = polys[i].bounds
                merged_log.append({"type": "small_roof_step", "center": [round(c.x, 2), round(c.y, 2),
                                   round(float(planes[k].z(c.x, c.y)), 2)],
                                   "size": [round(x1 - x0, 2), round(y1 - y0, 2), round(abs(dz), 2)],
                                   "points": int(len(idx)), "restore_hint": "normal_map"})
            labs[i] = k
    # 3. features too small for LoD2 (under 4 m2, or under 2 m wide: chimneys, vents, hatches)
    #    are merged into the neighbor they share the longest border with and ledgered
    cells2 = dissolve(polys, np.asarray(labs), footprint)
    polys = [c for c, _ in cells2]
    labs = [l for _, l in cells2]
    tree = shapely.STRtree(polys)
    for i in sorted(range(len(polys)), key=lambda i: polys[i].area):
        p = polys[i]
        rect = p.minimum_rotated_rectangle
        xy = np.asarray(rect.exterior.coords)[:4] if rect.geom_type == "Polygon" else None
        width = min(np.hypot(*(xy[1] - xy[0])), np.hypot(*(xy[2] - xy[1]))) if xy is not None else 0.0
        if p.area >= 4.0 and width >= 2.0:
            continue
        pb = p.buffer(1e-6)
        best, bl = None, 0.0
        for j in tree.query(pb, predicate="intersects"):
            j = int(j)
            if j == i or labs[j] == labs[i]:
                continue
            L = pb.intersection(polys[j].boundary).length
            if L > bl:
                best, bl = j, L
        if best is None:
            continue
        c = p.centroid
        own, k = labs[i], labs[best]
        dz = float(planes[own].z(c.x, c.y) - planes[k].z(c.x, c.y))
        if abs(dz) > 0.05:
            x0, y0, x1, y1 = p.bounds
            merged_log.append({"type": "chimney_or_vent" if dz > 0 else "small_roof_recess",
                               "center": [round(c.x, 2), round(c.y, 2), round(float(planes[k].z(c.x, c.y)), 2)],
                               "size": [round(x1 - x0, 2), round(y1 - y0, 2), round(abs(dz), 2)], "points": 0,
                               "restore_hint": "geometry" if abs(dz) >= MERGE_MIN else "normal_map"})
        labs[i] = k
    return dissolve(polys, np.asarray(labs), footprint), planes


def tidy_planes(cells, planes, P, steep):
    """Two clean-ups using each plane's own points (points in its cells, within 0.3 m of it):

    - a small (< 40 m2), gently sloped (< 15 deg) plane becomes level when a level plane fits its
      points almost as well (RMS within 3 cm): tilts of small noisy patches are not real;
    - a sloped plane is clipped to the height range its points cover (0.5th-99.5th percentile
      +-3 cm), so a plane never runs above the highest or below the lowest roof the LiDAR saw
      (stops roof planes from overshooting ridges or dropping below eaves at far cell corners)."""
    if not cells:
        return
    polys = [c for c, _ in cells]
    labs = np.array([l for _, l in cells])
    tree = shapely.STRtree(polys)
    pi, gi = tree.query(shapely.points(P[:, :2]), predicate="within")
    keep = ~steep[pi]
    pi, gi = pi[keep], gi[keep]
    pl_of = labs[gi]
    for k, pl in enumerate(planes):
        idx = pi[pl_of == k]
        if len(idx) < 10:
            continue
        Q = P[idx]
        r = Q[:, 2] - (pl.a * Q[:, 0] + pl.b * Q[:, 1] + pl.c)
        Q = Q[np.abs(r) < 0.3]
        if len(Q) < 10:
            continue
        area = sum(polys[j].area for j in np.nonzero(labs == k)[0])
        if area < 40.0 and 0.0 < pl.slope_deg < 15.0:
            zmed = float(np.median(Q[:, 2]))
            rms_flat = float(np.sqrt(np.mean((Q[:, 2] - zmed) ** 2)))
            rms_tilt = float(np.sqrt(np.mean((Q[:, 2] - (pl.a * Q[:, 0] + pl.b * Q[:, 1] + pl.c)) ** 2)))
            if rms_flat <= rms_tilt + 0.03:
                pl.a, pl.b, pl.c = 0.0, 0.0, zmed
                pl.kind = pl.kind + "+levelled"
                continue
        if pl.slope_deg >= 2.0:
            lo, hi = np.percentile(Q[:, 2], [0.5, 99.5])
            pl.zlo, pl.zhi = float(lo - 0.03), float(hi + 0.03)


def med_res_plane(Q, pl):
    return float(np.median(np.abs(Q[:, 2] - pl.z(Q[:, 0], Q[:, 1]))))


def model_z(cells, planes, x, y):
    """Model roof height at points (NaN outside every cell)."""
    z = np.full(len(x), np.nan)
    if not cells:
        return z
    polys = [c for c, _ in cells]
    tree = shapely.STRtree(polys)
    pidx, gidx = tree.query(shapely.points(np.c_[x, y]), predicate="within")
    if len(pidx) == 0:
        pidx, gidx = tree.query(shapely.points(np.c_[x, y]), predicate="intersects")
    for p, g in zip(pidx, gidx):
        pass
    labs = np.array([l for _, l in cells])
    if len(pidx):
        pl = labs[gidx]
        for k in np.unique(pl):
            s = pidx[pl == k]
            z[s] = planes[k].z(x[s], y[s])
    return z


def find_equipment(P, resid, footprint, grid, allow_volumes=True, free=None):
    """Group points more than 0.3 m above the model roof into features.

    free: points that belong to no roof plane (equipment is never part of a big plane).

    Returns (ledger entries, volumes). A volume is a box >= 0.5 m high and >= 2 m wide in
    both directions (it becomes a roof volume); everything else is ledgered."""
    hi = np.isfinite(resid) & (resid > 0.3)
    if free is not None:
        hi &= free
    if hi.sum() < 4:
        return [], []
    r, c = grid.rc(P[hi, 0], P[hi, 1])
    m = np.zeros((grid.h, grid.w), bool)
    m[r, c] = True
    m = ndimage.binary_closing(m, np.ones((3, 3), bool)) & grid.inside
    comp, n = ndimage.label(m)
    ents, vols = [], []
    hp = P[hi]
    hr = resid[hi]
    pc = comp[r, c]
    fb = footprint.boundary
    for k in range(1, n + 1):
        s = pc == k
        if s.sum() < 3:
            continue
        q = hp[s]
        h = float(np.percentile(hr[s], 90))
        pts2 = shapely.MultiPoint(q[:, :2])
        rect = pts2.minimum_rotated_rectangle.buffer(0.15, join_style=2)
        if rect.geom_type != "Polygon" or rect.area < 0.05:
            continue
        xy = np.asarray(rect.exterior.coords)[:4]
        e = np.hypot(*(xy[1] - xy[0])), np.hypot(*(xy[2] - xy[1]))
        w, L = min(e), max(e)
        cen = rect.centroid
        near_edge = fb.distance(cen) < 1.0 + w / 2
        if near_edge and L > 3 * w and h < MERGE_MIN + 0.3:
            kind = "parapet"
        elif h >= MERGE_MIN and w < 1.5 and L < 1.5:
            kind = "chimney_or_vent"
        else:
            kind = "roof_equipment"
        filled = (comp == k).sum() * grid.cell ** 2 / max(rect.area, 1e-6)
        top = q[:, 2][hr[s] > 0.3]
        if (allow_volumes and h >= MERGE_MIN and w >= 2.0 and kind == "roof_equipment" and filled > 0.6
                and rect.area < 150.0 and np.std(top) < 0.35):
            vols.append({"rect": rect.intersection(footprint.buffer(-0.05)), "top_z": float(np.percentile(top, 75)),
                         "rms": float(np.std(top)), "n": int(len(top))})
            continue
        ents.append({"type": kind, "center": [round(cen.x, 2), round(cen.y, 2),
                                              round(float(np.median(q[:, 2])), 2)],
                     "size": [round(L, 2), round(w, 2), round(h, 2)], "points": int(s.sum()),
                     "restore_hint": "geometry" if h >= MERGE_MIN else "normal_map"})
    vols = [v for v in vols if v["rect"].geom_type == "Polygon" and v["rect"].area > 2.0]
    return ents, vols
