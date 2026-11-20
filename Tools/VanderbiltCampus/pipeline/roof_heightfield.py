"""Stage 1 buildings v2: a roof that follows the LiDAR roof surface directly (bounded error).

No file input/output here, so tests can feed synthetic points. buildings_v2.py calls it.

Steps (heightfield_roof does them in order):
 1. Grid: 0.25 m cells in a frame turned to the building's main wall direction (so walls and
    roof steps mostly run along grid lines and stay straight).
 2. Cell heights: one real LiDAR height per cell (the lower median of its points, about one
    point per cell), then a 3x3 median that only uses neighbors on the same side of a height
    break (within 0.3 m). Single-cell spikes are noise. Empty cells are filled from their
    neighbors, then by nearest value.
 3. Optional v1 snap: a cell whose v1 roof plane is within 0.08 m takes the plane's exact
    height (keeps clean planar faces where v1 was right).
 4. Breaks: a step of >= 0.3 m between two neighbor cells that is not explained by the
    slope on both sides (a steep roof is a slope, not a step). Cells joined without a break
    form "regions" (smooth roof surfaces); breaks between regions become vertical walls.
 5. Merge rule (LoD2): a region under 2 m2 whose relief is under 0.5 m, or one narrower than
    0.5 m both ways, joins its neighbor; each merge is a ledger entry.
 6. Mesh cells: each region's outline (simplified as a coverage, so neighbors still share
    their borders, then clipped to the footprint, which stays the wall position). A region
    that fits one plane within 0.08 m is one cell on that plane. Otherwise it is split
    (k-d style, at the line of largest error) until every piece, triangulated from its
    corners and lifted onto the region's surface, is within 0.08 m of the height field.
    Pieces of one region share its surface, so they meet without gaps.
 7. roof_mesh.build_solid turns the cells into one watertight solid (walls at every height
    difference, base cap).
"""
import math
import warnings

import numpy as np
import shapely
from scipy import ndimage
from scipy.spatial import cKDTree
from shapely.geometry import Polygon, box

import buildings_common as bc

CELL = 0.25          # m, grid cell
BREAK = 0.30         # m, smallest step kept as a vertical face
TOL = 0.08           # m, mesh vs height field
MERGE_RELIEF = 0.5   # m, LoD2 merge threshold (STANDARDS section 4)
MERGE_AREA = 2.0     # m2, features smaller than this with relief < 0.5 m are merged
NARROW = 0.5         # m, features narrower than this both ways are merged
SIMPLIFY = 0.15      # m, coverage simplification of region outlines


# ---- frame -------------------------------------------------------------------

class Grid:
    """0.25 m grid in a frame rotated by theta (u along the main wall direction).
    Cell (j, i) covers u in [u0 + i*cell, ...], v in [v0 + j*cell, ...]; row 0 is the low-v edge."""

    def __init__(self, footprint, theta, cell=CELL, margin=1.0):
        self.theta, self.cell = float(theta), float(cell)
        self.c, self.s = math.cos(theta), math.sin(theta)
        fu = self.to_uv_geom(footprint)
        u0, v0, u1, v1 = fu.bounds
        self.u0 = math.floor((u0 - margin) / cell) * cell
        self.v0 = math.floor((v0 - margin) / cell) * cell
        self.nu = int(math.ceil((u1 + margin - self.u0) / cell))
        self.nv = int(math.ceil((v1 + margin - self.v0) / cell))
        self.footprint_uv = fu

    def to_uv(self, x, y):
        x, y = np.asarray(x, float), np.asarray(y, float)
        return x * self.c + y * self.s, -x * self.s + y * self.c

    def to_xy(self, u, v):
        u, v = np.asarray(u, float), np.asarray(v, float)
        return u * self.c - v * self.s, u * self.s + v * self.c

    def to_uv_geom(self, g):
        return shapely.transform(g, lambda p: np.c_[self.to_uv(p[:, 0], p[:, 1])])

    def to_xy_geom(self, g):
        return shapely.transform(g, lambda p: np.c_[self.to_xy(p[:, 0], p[:, 1])])

    def centers(self):
        jj, ii = np.mgrid[0:self.nv, 0:self.nu]
        return self.u0 + (ii + 0.5) * self.cell, self.v0 + (jj + 0.5) * self.cell

    @property
    def transform(self):
        from rasterio.transform import Affine
        return Affine(self.cell, 0, self.u0, 0, self.cell, self.v0)

    def rasterize(self, geoms_values, all_touched=False, fill=0, dtype=np.int32):
        import rasterio.features
        return rasterio.features.rasterize(geoms_values, out_shape=(self.nv, self.nu), transform=self.transform,
                                           fill=fill, dtype=dtype, all_touched=all_touched)


# ---- height field ----------------------------------------------------------------

def cell_values(grid, u, v, z):
    """Lower median of the points in each cell (a real measured height, never an average of two
    heights across a step). Returns (H with nan for empty cells, count)."""
    i = np.floor((u - grid.u0) / grid.cell).astype(np.int64)
    j = np.floor((v - grid.v0) / grid.cell).astype(np.int64)
    ok = (i >= 0) & (i < grid.nu) & (j >= 0) & (j < grid.nv)
    idx, zz = (j * grid.nu + i)[ok], z[ok]
    H = np.full(grid.nv * grid.nu, np.nan)
    cnt = np.bincount(idx, minlength=grid.nv * grid.nu)
    if idx.size:
        order = np.lexsort((zz, idx))
        idx, zz = idx[order], zz[order]
        starts = np.flatnonzero(np.r_[True, idx[1:] != idx[:-1]])
        counts = np.diff(np.r_[starts, idx.size])
        H[idx[starts]] = zz[starts + (counts - 1) // 2]
    return H.reshape(grid.nv, grid.nu), cnt.reshape(grid.nv, grid.nu)


def _stack3(H):
    P = np.pad(H, 1, constant_values=np.nan)
    ny, nx = H.shape
    return np.stack([P[1 + dj:1 + dj + ny, 1 + di:1 + di + nx] for dj in (-1, 0, 1) for di in (-1, 0, 1)])


def same_side_median(H, thr=BREAK):
    """3x3 median using only neighbors within thr of the cell (same side of any break).
    Returns (smoothed, spikes): spikes = cells with >= 3 neighbors and none within thr."""
    S = _stack3(H)
    near = np.abs(S - H[None]) < thr
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", RuntimeWarning)
        out = np.nanmedian(np.where(near, S, np.nan), axis=0)
    nfin = np.isfinite(S).sum(axis=0) - 1
    nnear = near.sum(axis=0) - 1            # minus the cell itself
    spikes = np.isfinite(H) & (nfin >= 3) & (nnear == 0)
    out[~np.isfinite(H)] = np.nan
    return out, spikes


def _plane_sums(grid, i, j, u, v, z, keep, radius):
    """Least-squares plane per cell from the points of its (2r+1)^2 neighborhood that pass keep.
    Returns (coef (n, 3) = height at center, slope per cell in u, in v; ok mask)."""
    nu, nv, cs = grid.nu, grid.nv, grid.cell
    S = np.zeros((10, nv * nu))
    for dj in range(-radius, radius + 1):
        for di in range(-radius, radius + 1):
            tj, ti = j - dj, i - di                 # the target cell this point helps
            good = (tj >= 0) & (tj < nv) & (ti >= 0) & (ti < nu)
            t = np.clip(tj * nu + ti, 0, nv * nu - 1)
            du = (u - (grid.u0 + (ti + 0.5) * cs)) / cs
            dv = (v - (grid.v0 + (tj + 0.5) * cs)) / cs
            good &= keep(t, du, dv)
            t, du, dv, zz = t[good], du[good], dv[good], z[good]
            zc = zz - zz.mean() if len(zz) else zz
            for k, w in enumerate((np.ones_like(du), du, dv, du * du, du * dv, dv * dv, zz, du * zz, dv * zz)):
                S[k] += np.bincount(t, weights=w, minlength=nv * nu)
            S[9] += np.bincount(t, weights=zz * zz, minlength=nv * nu)
    n, su, sv, suu, suv, svv, sz, suz, svz, szz = S
    nn = np.maximum(n, 1)
    vu, vv_, cuv = suu / nn - (su / nn) ** 2, svv / nn - (sv / nn) ** 2, suv / nn - (su / nn) * (sv / nn)
    mineig = (vu + vv_) / 2 - np.sqrt(((vu - vv_) / 2) ** 2 + cuv ** 2)
    ok = (n >= 5) & (mineig >= 0.15)          # points spread in both directions
    A = np.stack([np.stack([n, su, sv], -1), np.stack([su, suu, suv], -1), np.stack([sv, suv, svv], -1)], -2)
    rhs = np.stack([sz, suz, svz], -1)
    sol = np.full((nv * nu, 3), np.nan)
    rms = np.full(nv * nu, np.inf)
    if ok.any():
        sol[ok] = np.linalg.solve(A[ok], rhs[ok][..., None])[..., 0]
        rss = szz[ok] - np.einsum("ni,ni->n", sol[ok], rhs[ok])
        rms[ok] = np.sqrt(np.maximum(rss, 0) / np.maximum(n[ok] - 3, 1))   # unbiased: few points do not look better
    return sol, ok, rms


def sharpen(coef, rms, nv, nu, ref, measured, thr):
    """Edge-keeping step (a Kuwahara filter with planes): every cell may take a neighbor's 3x3
    plane, extended to its own center, when that plane fits its points better. Next to a
    ridge, an eave or a step a centered window mixes two surfaces; a window shifted to the
    cell's own side does not, so ridges and eaves stay sharp."""
    C = coef.reshape(nv, nu, 3)
    R = rms.reshape(nv, nu)
    out = C.copy()
    best = R.copy()
    P = np.pad(C, ((1, 1), (1, 1), (0, 0)), constant_values=np.nan)
    PR = np.pad(R, 1, constant_values=np.inf)
    ref = ref.reshape(nv, nu)
    meas = measured if measured is not None else np.ones((nv, nu), bool)
    for dj in (-1, 0, 1):
        for di in (-1, 0, 1):
            if dj == 0 and di == 0:
                continue
            N = P[1 + dj:1 + dj + nv, 1 + di:1 + di + nu]
            NR = PR[1 + dj:1 + dj + nv, 1 + di:1 + di + nu]
            ext = N[..., 0] - N[..., 1] * di - N[..., 2] * dj
            ok = np.isfinite(ext) & (NR < best * 0.8) & (~meas | (np.abs(ext - ref) < thr))
            if ok.any():
                out[ok, 0] = ext[ok]
                out[ok, 1] = N[ok, 1]
                out[ok, 2] = N[ok, 2]
                best[ok] = NR[ok]
    return out.reshape(-1, 3)


def local_planes(grid, u, v, z, ref, measured=None, thr=BREAK):
    """Height at every cell center from a small plane fitted to the points of its 3x3 cells
    (5x5 where those are too few, e.g. at the roof edge) that lie on its side of any break.
    Pass 1 keeps points within thr of ref (the same-side median); pass 2 keeps points within
    0.15 m of the pass-1 plane. A single point per cell sits anywhere in the cell, so on a slope
    its height is not the center height: the plane fixes that. Cells with no good fit keep ref."""
    nu, nv, cs = grid.nu, grid.nv, grid.cell
    i = np.floor((u - grid.u0) / cs).astype(np.int64)
    j = np.floor((v - grid.v0) / cs).astype(np.int64)
    inr = (i >= 0) & (i < nu) & (j >= 0) & (j < nv)
    i, j, u, v, z = i[inr], j[inr], u[inr], v[inr], z[inr]
    r = ref.ravel()

    def near_ref(t, du, dv):
        return np.isfinite(r[t]) & (np.abs(z - r[t]) < thr)
    coef = np.full((nv * nu, 3), np.nan)
    for radius in (1, 2):
        todo = ~np.isfinite(coef[:, 0])
        sol, ok, _ = _plane_sums(grid, i, j, u, v, z, near_ref, radius)
        far = np.abs(sol[:, 0] - r) >= thr
        if measured is not None:
            far &= measured.ravel()           # a filled (empty) cell's ref is only a guess
        take = todo & ok & ~far
        coef[take] = sol[take]
    for radius in (1, 2):
        c1 = coef.copy()

        def near_plane(t, du, dv, c1=c1):
            c = c1[t]
            return np.isfinite(c[:, 0]) & (np.abs(z - (c[:, 0] + c[:, 1] * du + c[:, 2] * dv)) < 0.15)
        sol, ok, rms = _plane_sums(grid, i, j, u, v, z, near_plane, radius)
        take = ok & np.isfinite(c1[:, 0]) & (np.abs(sol[:, 0] - c1[:, 0]) < 0.15)
        if radius == 1:
            coef[take] = sol[take]
            done1 = take
            rms1 = np.where(take, rms, np.inf)
        else:
            take &= ~done1
            coef[take] = sol[take]
    # cells still without a plane: extend a neighbor's plane (the one closest to ref, so it is
    # most likely on the same side of a break); ref only where no neighbor within 2 cells has one
    C = coef.reshape(nv, nu, 3)
    H = C[..., 0].copy()
    miss = ~np.isfinite(H)
    if miss.any():
        R = ref
        best = np.full((nv, nu), np.nan)
        bestd = np.full((nv, nu), np.inf)
        P = np.pad(C, ((2, 2), (2, 2), (0, 0)), constant_values=np.nan)
        for dj in range(-2, 3):
            for di in range(-2, 3):
                if dj == 0 and di == 0:
                    continue
                N = P[2 + dj:2 + dj + nv, 2 + di:2 + di + nu]
                ext = N[..., 0] - N[..., 1] * di - N[..., 2] * dj
                d = np.abs(ext - R) + 0.01 * (abs(di) + abs(dj))
                take = miss & np.isfinite(ext) & (d < bestd)
                best[take], bestd[take] = ext[take], d[take]
        H[miss] = np.where(np.isfinite(best[miss]), best[miss], R[miss])
    return H


def fill_empty(H, iters=4):
    """Empty cells take the median of their filled 3x3 neighbors (a few rounds), then nearest."""
    H = H.copy()
    for _ in range(iters):
        miss = ~np.isfinite(H)
        if not miss.any():
            break
        S = _stack3(H)
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", RuntimeWarning)
            med = np.nanmedian(S, axis=0)
        n = np.isfinite(S).sum(axis=0)
        take = miss & (n >= 2)
        H[take] = med[take]
    miss = ~np.isfinite(H)
    if miss.any():
        if miss.all():
            raise ValueError("no roof heights")
        _, (jj, ii) = ndimage.distance_transform_edt(miss, return_indices=True)
        H[miss] = H[jj[miss], ii[miss]]
    return H


def breaks(H, thr=BREAK):
    """Step masks: bu[j, i] between cells (j, i) and (j, i+1); bv[j, i] between (j, i) and (j+1, i).
    A step is a height difference >= thr that the slopes on both sides do not explain."""
    def along(A):
        d = A[:, 1:] - A[:, :-1]                       # (n, m-1)
        sa = np.zeros_like(d)
        sb = np.zeros_like(d)
        sa[:, 1:] = d[:, :-1]
        sb[:, :-1] = d[:, 1:]
        exp = (sa + sb) / 2
        return (np.abs(d) >= thr) & (np.abs(d - exp) >= thr)
    return along(H), along(H.T).T


def regions(H, domain, bu, bv):
    """Connected cells (4-neighbors without a break) inside domain. Returns labels (-1 outside)."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    ny, nx = H.shape
    idx = np.arange(ny * nx).reshape(ny, nx)
    eu = domain[:, :-1] & domain[:, 1:] & ~bu
    ev = domain[:-1, :] & domain[1:, :] & ~bv
    a = np.r_[idx[:, :-1][eu], idx[:-1, :][ev]]
    b = np.r_[idx[:, 1:][eu], idx[1:, :][ev]]
    g = coo_matrix((np.ones(len(a)), (a, b)), shape=(ny * nx, ny * nx))
    _, lab = connected_components(g, directed=False)
    lab = lab.reshape(ny, nx)
    lab = np.where(domain, lab, -1)
    u, inv = np.unique(lab, return_inverse=True)
    inv = inv.reshape(lab.shape)
    off = 1 if u[0] == -1 else 0
    return np.where(lab >= 0, inv - off, -1)


def merge_small(lab, H, grid, ledger):
    """LoD2 merge rule on regions (see module help). Merged cells take the height of the
    region they join (nearest of its own cells). Appends ledger entries; returns (lab, H)."""
    c2 = grid.cell ** 2
    uu, vv = grid.centers()
    for _ in range(4):
        changed = False
        n = lab.max() + 1
        if n <= 1:
            break
        areas = np.bincount(lab[lab >= 0], minlength=n) * c2
        objs = ndimage.find_objects(lab + 1)
        for k in np.argsort(areas):
            if areas[k] >= MERGE_AREA or objs[k] is None:
                continue
            sl = objs[k]
            sl = tuple(slice(max(s.start - 1, 0), s.stop + 1) for s in sl)
            L = lab[sl]
            m = L == k
            if not m.any():
                continue
            ring = ndimage.binary_dilation(m) & ~m & (L >= 0)
            if not ring.any():
                continue
            vals, cnt = np.unique(L[ring], return_counts=True)
            best = int(vals[np.argmax(cnt)])
            Hs = H[sl]
            relief = float(np.median(Hs[m]) - np.median(Hs[ring & (L == best)]))
            ext_u = (np.ptp(np.nonzero(m)[1]) + 1) * grid.cell
            ext_v = (np.ptp(np.nonzero(m)[0]) + 1) * grid.cell
            narrow = ext_u < NARROW and ext_v < NARROW
            if abs(relief) >= MERGE_RELIEF and not narrow:
                continue
            L[m] = best
            changed = True
            if abs(relief) >= 0.05:
                cu, cv = float(uu[sl][m].mean()), float(vv[sl][m].mean())
                x, y = grid.to_xy(cu, cv)
                kind = ("chimney_or_vent" if relief >= MERGE_RELIEF else "roof_equipment") if relief > 0 \
                    else "small_roof_recess"
                ledger.append({"type": kind, "merged_into": "roof", "center": [round(float(x), 2), round(float(y), 2),
                               round(float(np.median(Hs[ring & (L == best)])), 2)],
                               "size": [round(ext_u, 2), round(ext_v, 2), round(abs(relief), 2)],
                               "count": 1, "material": "brick_red" if kind == "chimney_or_vent" else "roof_flat",
                               "photos": [], "restore_hint": "geometry" if abs(relief) >= MERGE_RELIEF else "normal_map",
                               "area_m2": round(float(m.sum() * c2), 3), "rule": "narrow" if narrow else "relief<0.5 and area<2"})
        if not changed:
            break
        # relabel compactly
        u, inv = np.unique(lab, return_inverse=True)
        inv = inv.reshape(lab.shape)
        off = 1 if u[0] == -1 else 0
        lab = np.where(lab >= 0, inv - off, -1)
    return lab


def region_heights(lab, H, orig):
    """Cells whose region changed in the merge (orig label != final) take the nearest height
    of the final region's own cells."""
    H = H.copy()
    moved = (lab >= 0) & ~orig
    if not moved.any():
        return H
    for k in np.unique(lab[moved]):
        own = (lab == k) & orig
        mv = (lab == k) & moved
        if not own.any():
            continue
        jo, io = np.nonzero(own)
        jm, im = np.nonzero(mv)
        _, nn = cKDTree(np.c_[jo, io]).query(np.c_[jm, im])
        H[jm, im] = H[jo[nn], io[nn]]
    return H


class Surface:
    """Height of one region at any (u, v): bilinear over the region's own cell centers (weights
    renormalized over the region's cells), nearest region cell where none of the four is in it."""

    def __init__(self, grid, lab, H, k, snap=None, planes_uv=None):
        self.g, self.lab, self.H, self.k = grid, lab, H, k
        jj, ii = np.nonzero(lab == k)
        self.cells = np.c_[jj, ii]
        self.tree = cKDTree(self.cells)
        self.snap, self.planes = snap, planes_uv

    def __call__(self, u, v):
        out = self._bilinear(u, v)
        if self.snap is None:
            return out
        # where the nearest cell was snapped to a v1 plane, use that plane exactly: ridges and
        # valleys between snapped cells stay sharp (bilinear interpolation would cut them)
        g = self.g
        u, v = np.asarray(u, float).ravel(), np.asarray(v, float).ravel()
        i = np.clip(np.floor((u - g.u0) / g.cell).astype(np.int64), 0, g.nu - 1)
        j = np.clip(np.floor((v - g.v0) / g.cell).astype(np.int64), 0, g.nv - 1)
        pid = np.where(self.lab[j, i] == self.k, self.snap[j, i], -1)
        for l in np.unique(pid[pid >= 0]):
            m = pid == l
            au, av, c, lo, hi = self.planes[l]
            z = au * u[m] + av * v[m] + c
            if lo is not None:
                z = np.clip(z, lo, hi)
            out[m] = z
        return out

    def _bilinear(self, u, v):
        g = self.g
        u, v = np.asarray(u, float).ravel(), np.asarray(v, float).ravel()
        fi = (u - g.u0) / g.cell - 0.5
        fj = (v - g.v0) / g.cell - 0.5
        i0, j0 = np.floor(fi).astype(np.int64), np.floor(fj).astype(np.int64)
        tx, ty = fi - i0, fj - j0
        num = np.zeros(len(u))
        den = np.zeros(len(u))
        for dj, di, w in ((0, 0, (1 - tx) * (1 - ty)), (0, 1, tx * (1 - ty)), (1, 0, (1 - tx) * ty), (1, 1, tx * ty)):
            j, i = j0 + dj, i0 + di
            ok = (j >= 0) & (j < g.nv) & (i >= 0) & (i < g.nu)
            jc, ic = np.clip(j, 0, g.nv - 1), np.clip(i, 0, g.nu - 1)
            ok &= self.lab[jc, ic] == self.k
            ww = np.where(ok, w, 0.0) + np.where(ok, 1e-9, 0.0)
            num += ww * np.where(ok, self.H[jc, ic], 0.0)
            den += ww
        out = np.full(len(u), np.nan)
        has = den > 1e-12
        out[has] = num[has] / den[has]
        if (~has).any():
            _, nn = self.tree.query(np.c_[fj[~has], fi[~has]])
            c = self.cells[nn]
            out[~has] = self.H[c[:, 0], c[:, 1]]
        return out


# ---- region outlines ---------------------------------------------------------------

def region_polygons(lab, grid, simplify=SIMPLIFY):
    """Outline polygons of every region in the uv frame, covering the footprint exactly:
    labels are spread outside the footprint by nearest, outlines simplified as one coverage
    (shared borders stay shared), then clipped to the footprint. Returns list of (Polygon, k)."""
    import rasterio.features
    full = lab.copy()
    miss = full < 0
    if miss.any():
        _, (jj, ii) = ndimage.distance_transform_edt(miss, return_indices=True)
        full[miss] = full[jj[miss], ii[miss]]
    raw = [(shapely.geometry.shape(g), int(v)) for g, v in
           rasterio.features.shapes(full.astype(np.int32), transform=grid.transform)]
    geoms = np.array([g for g, _ in raw], dtype=object)
    simp = None
    for tol in (simplify, 0.06, 0.0) if simplify > 0 else (0.0,):
        if tol == 0.0:
            simp = geoms
            break
        try:
            s = shapely.coverage_simplify(geoms, tol, simplify_boundary=False)
        except Exception:
            continue
        if all(g is not None and not g.is_empty and g.is_valid for g in s):
            simp = s
            break
    fp = grid.footprint_uv
    out = []
    for g, (_, k) in zip(simp, raw):
        c = shapely.make_valid(g).intersection(fp)
        for p in bc.polys_of(c):
            if p.area > 1e-6:
                out.append([p, k])
    return absorb_slivers(out)


def absorb_slivers(pieces, min_area=0.02):
    """Pieces under min_area join the neighbor (any region) they share the longest border with."""
    big = [p for p in pieces if p[0].area >= min_area]
    small = [p for p in pieces if p[0].area < min_area]
    if not small or not big:
        return pieces
    tree = shapely.STRtree([p for p, _ in big])
    for p, _ in small:
        pb = p.buffer(1e-6)
        best, bl = None, 0.0
        for j in tree.query(pb, predicate="intersects"):
            L = pb.intersection(big[int(j)][0].boundary).length
            if L > bl:
                best, bl = int(j), L
        if best is not None:
            u = shapely.unary_union([big[best][0], p])
            if u.geom_type == "Polygon":
                big[best][0] = u
    return big


# ---- splitting a region until its pieces fit ---------------------------------------

def _lift_error(piece, surf, grid, H, k, lab):
    """Max |triangulated piece lifted onto surf - H| over the region-k cell centers inside the
    piece. Returns (max error, error per center, centers (j, i))."""
    u0, v0, u1, v1 = piece.bounds
    i0 = max(int(math.floor((u0 - grid.u0) / grid.cell)), 0)
    i1 = min(int(math.ceil((u1 - grid.u0) / grid.cell)), grid.nu)
    j0 = max(int(math.floor((v0 - grid.v0) / grid.cell)), 0)
    j1 = min(int(math.ceil((v1 - grid.v0) / grid.cell)), grid.nv)
    jj, ii = np.mgrid[j0:j1, i0:i1]
    jj, ii = jj.ravel(), ii.ravel()
    cu = grid.u0 + (ii + 0.5) * grid.cell
    cv = grid.v0 + (jj + 0.5) * grid.cell
    ins = shapely.contains_xy(piece.buffer(1e-6), cu, cv) & (lab[jj, ii] == k)
    if not ins.any():
        return 0.0, None, None
    jj, ii, cu, cv = jj[ins], ii[ins], cu[ins], cv[ins]
    tris = shapely.constrained_delaunay_triangles(piece)
    zt = np.full(len(cu), np.nan)
    T = np.asarray([np.asarray(t.exterior.coords)[:3] for t in getattr(tris, "geoms", [])])
    if len(T):
        Z = surf(T[:, :, 0].ravel(), T[:, :, 1].ravel()).reshape(-1, 3)     # one call for all corners
        for (xy, z) in zip(T, Z):
            (xa, ya), (xb, yb), (xc, yc) = xy
            det = (yb - yc) * (xa - xc) + (xc - xb) * (ya - yc)
            if abs(det) < 1e-14:
                continue
            l1 = ((yb - yc) * (cu - xc) + (xc - xb) * (cv - yc)) / det
            l2 = ((yc - ya) * (cu - xc) + (xa - xc) * (cv - yc)) / det
            l3 = 1 - l1 - l2
            inside = (l1 >= -1e-9) & (l2 >= -1e-9) & (l3 >= -1e-9) & np.isnan(zt)
            zt[inside] = l1[inside] * z[0] + l2[inside] * z[1] + l3[inside] * z[2]
    err = np.abs(zt - H[jj, ii])
    err = np.where(np.isfinite(err), err, 0.0)
    # a piece fits when 99% of its cells are within tol and none is off by more than 3 tol
    # (single noisy cells do not force splits)
    score = max(float(np.percentile(err, 99)), float(err.max()) / 3.0)
    return score, err, (jj, ii)


def split_line(piece, err, cells, grid):
    """Where to cut a piece that does not fit: along the axis whose error profile varies most,
    through the cell centers of the worst row/column (a ridge or valley lands on the cut)."""
    jj, ii = cells
    u0, v0, u1, v1 = piece.bounds
    best = None
    for axis, idx in (("u", ii), ("v", jj)):
        lo, hi = idx.min(), idx.max()
        if hi - lo < 2:
            continue
        prof = np.bincount(idx - lo, weights=err) / np.maximum(np.bincount(idx - lo), 1)
        inner = prof[1:-1]
        if not len(inner):
            continue
        k = int(np.argmax(inner)) + 1 + lo
        score = float(np.std(prof))
        if best is None or score > best[0]:
            best = (score, axis, k)
    if best is None:
        return None
    _, axis, k = best
    out = []
    for off in (0.5, 0.0, 1.0):        # through the worst row's centers, or along either edge
        if axis == "u":
            c = grid.u0 + (k + off) * grid.cell
            out.append((box(u0 - 1, v0 - 1, c, v1 + 1), box(c, v0 - 1, u1 + 1, v1 + 1)))
        else:
            c = grid.v0 + (k + off) * grid.cell
            out.append((box(u0 - 1, v0 - 1, u1 + 1, c), box(u0 - 1, c, u1 + 1, v1 + 1)))
    return out


def fit_region(piece, surf, grid, H, k, lab, tol=TOL, max_pieces=6000):
    """Split one region outline into pieces whose lifted triangulation fits within tol.
    A strip at most 3 cells (0.75 m) wide is the grid's resolution limit (ridge and valley
    lines are rounded there by the cell fit): it is kept when within 2 tol, so noise along a
    ridge does not chop it into single cells."""
    todo = [piece]
    done = []
    lim = 3 * grid.cell + 1e-6
    while todo:
        p = todo.pop()
        e, err, cells = _lift_error(p, surf, grid, H, k, lab)
        b = p.bounds
        thin = min(b[2] - b[0], b[3] - b[1]) <= lim
        if e <= tol or err is None or (thin and e <= 2 * tol) or len(done) + len(todo) > max_pieces:
            done.append(p)
            continue
        options = split_line(p, err, cells, grid)
        if options is None:
            done.append(p)
            continue
        # try the three cut positions; keep the one whose worse half fits best (a ridge exactly on
        # a cell edge is then cut on that edge)
        best = None
        for halves in options:
            parts = []
            for h in halves:
                parts += [q for q in bc.polys_of(p.intersection(h)) if q.area > 1e-6]
            if len(parts) < 2:
                continue
            sc = max(_lift_error(q, surf, grid, H, k, lab)[0] for q in parts)
            if best is None or sc < best[0]:
                best = (sc, parts)
        if best is None:
            done.append(p)
            continue
        todo += best[1]
    return done


def node_cells(cells, fp, grid_size=1e-4):
    """Make the pieces one clean coverage of the footprint: snap to a 0.1 mm grid, cut every
    piece by all piece borders (polygonize), and give each face to the piece it lies in (gaps
    between pieces, if any, go to the piece they touch most). Neighbors then share vertices."""
    from shapely.ops import polygonize, unary_union
    polys = shapely.set_precision(np.array([c["poly_uv"] for c in cells], dtype=object), grid_size)
    fp = shapely.set_precision(fp, grid_size)
    keep = [k for k, p in enumerate(polys) if p is not None and not p.is_empty]
    polys = [polys[k] for k in keep]
    cells = [cells[k] for k in keep]
    net = unary_union([p.boundary for p in polys] + [fp.boundary])
    tree = shapely.STRtree(polys)
    out = []
    for f in polygonize(net):
        if f.area < 1e-8:
            continue
        rp = f.representative_point()
        if not fp.contains(rp):
            continue
        cand = [int(k) for k in tree.query(rp, predicate="intersects")]
        if not cand:
            cand = [int(k) for k in tree.query(f.buffer(1e-3), predicate="intersects")]
            if not cand:
                continue
            k = max(cand, key=lambda k: polys[k].buffer(1e-3).intersection(f).area)
        else:
            k = max(cand, key=lambda k: polys[k].intersection(f).area)
        out.append({**cells[k], "poly_uv": f})
    return out


def fit_plane_cells(H, mask, grid):
    """Least-squares plane z = a u + b v + c over the masked cells; returns (a, b, c, max_err, p99)."""
    uu, vv = grid.centers()
    u, v, z = uu[mask], vv[mask], H[mask]
    if len(z) < 3:
        return None
    A = np.c_[u - u.mean(), v - v.mean(), np.ones(len(u))]
    sol, *_ = np.linalg.lstsq(A, z, rcond=None)
    r = np.abs(z - A @ sol)
    a, b, c0 = sol
    return a, b, c0 - a * u.mean() - b * v.mean(), float(r.max()), float(np.percentile(r, 99))


# ---- the whole roof -----------------------------------------------------------------

def roof_points(pts, footprint, others, ground_z):
    """Roof points (same selection as v1 and the check) without facade points and noise spikes.
    Returns an (n, 3) array."""
    import roof_fit as rf
    import terrain_mesh as tm
    idx = rf.roof_point_selection(pts, footprint, others, ground_z)
    P = np.c_[pts["x"][idx], pts["y"][idx], pts["z"][idx]]
    if len(P) >= 10:
        N, _, _ = rf.normals(P, 10)
        P = P[np.abs(N[:, 2]) >= 0.35]
    if len(P) >= 10:
        P = P[tm.drop_spikes(P[:, 0], P[:, 1], P[:, 2])]
    return P


def heightfield_roof(P, footprint, v1=None, tol=TOL, theta=None, simplify=SIMPLIFY):
    """Height-field roof for one footprint.

    P: (n, 3) roof points (roof_points). v1: optional (cells, planes) from roof_fit (cells =
    [(Polygon, plane index)]) for the snap step. Returns a dict with the mesh cells for
    roof_mesh.build_solid ("cells": [{poly (xy), z (callable x, y), region}]), the regions
    (id, polygon xy, area), the grid and height field, ledger entries and stats."""
    if theta is None:
        theta = bc.dominant_angle(footprint)
    grid = Grid(footprint, theta)
    u, v = grid.to_uv(P[:, 0], P[:, 1])
    H0, cnt = cell_values(grid, u, v, P[:, 2])
    H1, spikes = same_side_median(H0)
    H1[spikes] = np.nan
    measured = np.isfinite(H1)
    if measured.sum() < 4:
        raise ValueError("too few roof points for a height field")
    Hf = local_planes(grid, u, v, P[:, 2], fill_empty(H1), measured)
    domain = grid.rasterize([(grid.footprint_uv, 1)], all_touched=True).astype(bool)
    stats = {"cells": int(domain.sum()), "measured_share": round(float(measured[domain].mean()), 3),
             "noise_cells": int(spikes.sum()), "theta_deg": round(math.degrees(theta), 2)}
    snapped = np.zeros_like(domain)
    snapid = np.full(domain.shape, -1, np.int64)
    planes_uv = None
    if v1 is not None and v1[0]:
        vcells, vplanes = v1
        lab1 = grid.rasterize([(grid.to_uv_geom(c), l + 1) for c, l in vcells], fill=0) - 1
        uu, vv = grid.centers()
        x, y = grid.to_xy(uu, vv)
        for l in np.unique(lab1[lab1 >= 0]):
            m = (lab1 == l) & domain
            zp = vplanes[l].z(x[m], y[m])
            ok = np.abs(zp - Hf[m]) <= tol
            idx = np.nonzero(m)
            Hf[idx[0][ok], idx[1][ok]] = zp[ok]
            snapped[idx[0][ok], idx[1][ok]] = True
            snapid[idx[0][ok], idx[1][ok]] = l
        stats["snapped_to_v1_share"] = round(float(snapped[domain].mean()), 3)
        # v1 planes in the grid frame: x = u c - v s, y = u s + v c
        planes_uv = [(p.a * grid.c + p.b * grid.s, -p.a * grid.s + p.b * grid.c, p.c, p.zlo, p.zhi) for p in vplanes]
    bu, bv = breaks(Hf)
    lab0 = regions(Hf, domain, bu, bv)
    ledger = []
    lab = merge_small(lab0.copy(), Hf, grid, ledger)
    # cells whose final region differs from the one they started in were merged
    orig = np.zeros_like(domain)
    for k in np.unique(lab[lab >= 0]):
        m = lab == k
        vals, cnt_ = np.unique(lab0[m], return_counts=True)
        main = vals[np.argmax(cnt_)]
        orig |= m & (lab0 == main)
    H = region_heights(lab, Hf, orig)
    stats["regions"] = int(lab.max() + 1)
    stats["break_edges"] = int(bu[domain[:, :-1] & domain[:, 1:]].sum() + bv[domain[:-1] & domain[1:]].sum())
    pieces = region_polygons(lab, grid, simplify)
    surfs = {}
    cells, regs = [], {}
    n_planar = n_split = 0
    for p, k in pieces:
        if k not in surfs:
            # merged cells (not their region's own) never use a plane
            surfs[k] = Surface(grid, lab, H, k, np.where(orig, snapid, -1) if planes_uv else None, planes_uv)
        mask = lab == k
        pl = fit_plane_cells(H, mask, grid)
        regs.setdefault(k, []).append(p)
        if pl is not None and pl[4] <= tol and pl[3] <= 3 * tol:
            a, b, c = pl[:3]
            zf = (lambda u_, v_, a=a, b=b, c=c: a * np.asarray(u_) + b * np.asarray(v_) + c)
            cells.append({"poly_uv": p, "zuv": zf, "region": k, "planar": True})
            n_planar += 1
            continue
        for q in fit_region(p, surfs[k], grid, H, k, lab, tol):
            cells.append({"poly_uv": q, "zuv": surfs[k], "region": k, "planar": False})
            n_split += 1
    cells = node_cells(cells, grid.footprint_uv)
    # planar regions use the plane for every piece; all pieces of a region use one function
    zfun = {}
    for c in cells:
        zfun.setdefault(c["region"], c["zuv"])
    out_cells = []
    for c in cells:
        f = zfun[c["region"]]

        def zxy(x, y, f=f):
            uu_, vv_ = grid.to_uv(np.asarray(x, float).ravel(), np.asarray(y, float).ravel())
            return np.asarray(f(uu_, vv_), float).reshape(np.shape(x))
        out_cells.append({"poly": grid.to_xy_geom(c["poly_uv"]), "z": zxy, "region": c["region"],
                          "planar": c["planar"]})
    region_rows = []
    for k, ps in sorted(regs.items()):
        g = grid.to_xy_geom(shapely.unary_union(ps))
        region_rows.append({"region": int(k), "polygon": g, "area_m2": float(g.area),
                            "planar": any(c["planar"] for c in cells if c["region"] == k)})
    stats.update({"planar_regions": n_planar, "pieces": len(out_cells), "split_pieces": n_split})
    return {"cells": out_cells, "regions": region_rows, "grid": grid, "H": H, "lab": lab,
            "measured": measured, "ledger": ledger, "stats": stats, "zfun": zfun}
