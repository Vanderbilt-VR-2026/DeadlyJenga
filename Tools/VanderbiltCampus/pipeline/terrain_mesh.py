"""Terrain building blocks (no file input/output here, so tests can use synthetic points).

Grid words used below:
  cell   a square of the height grid (0.5 m in campus tiles, 2 m in ring tiles)
  node   a cell corner; a tile of n x n cells has (n + 1) x (n + 1) nodes
  break  a cell edge where the ground steps by >= 15 cm (curb, step, retaining wall).
         The mesh keeps it as a real vertical face: the cells on the two sides do not
         share corner vertices there, and a vertical quad joins them.

Arrays are indexed [row, col] = [j, i] with row 0 at the SOUTH edge:
  cell (j, i) covers X in [x0 + i*cell, x0 + (i+1)*cell], Y in [y0 + j*cell, ...]
  node (J, I) sits at X = x0 + I*cell, Y = y0 + J*cell
  bx[j, i]  break between cells (j, i) and (j, i+1)   shape (n, n-1)
  by[j, i]  break between cells (j, i) and (j+1, i)   shape (n-1, n)
Cell corners are numbered SW=0, SE=1, NW=2, NE=3.
"""
import numpy as np

SW, SE, NW, NE = 0, 1, 2, 3
BREAK_MIN = 0.15            # m, smallest step kept as a vertical face (STANDARDS section 4, LoD3 rule)
CURB_MAX = 0.30             # m, break faces up to this height are "curb", taller ones "concrete"
UV_SCALE = 0.94             # planar UV uses 0..0.94; the top 6% holds the vertical break faces


# ---- ground points -> cell heights ------------------------------------------

def drop_spikes(x, y, z, max_dz=0.5, k=8, radius=1.5):
    """Mask of points to keep. A point is a spike if it is more than max_dz away from the
    median of its k nearest neighbors (within radius) AND fewer than 2 neighbors are
    within 0.2 m of its height. Walls survive because points on top of a wall have
    neighbors at their own height."""
    from scipy.spatial import cKDTree
    n = len(x)
    if n < k + 1:
        return np.ones(n, bool)
    tree = cKDTree(np.c_[x, y])
    d, idx = tree.query(np.c_[x, y], k=k + 1, distance_upper_bound=radius)
    d, idx = d[:, 1:], idx[:, 1:]
    import warnings
    valid = np.isfinite(d)
    zn = np.where(valid, z[np.minimum(idx, n - 1)], np.nan)
    dz = zn - z[:, None]
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", RuntimeWarning)       # points with no neighbors
        med = np.nanmedian(dz, axis=1)
    close = (np.abs(dz) < 0.2).sum(axis=1)
    spike = (np.abs(np.nan_to_num(med, nan=0.0)) > max_dz) & (close < 2) & (valid.sum(axis=1) >= 3)
    return ~spike


def cell_index(x, y, gx0, gy0, cell, nx, ny):
    """Flat cell index of each point, -1 outside the grid."""
    i = np.floor((x - gx0) / cell).astype(np.int64)
    j = np.floor((y - gy0) / cell).astype(np.int64)
    ok = (i >= 0) & (i < nx) & (j >= 0) & (j < ny)
    return np.where(ok, j * nx + i, -1)


def cell_heights(x, y, z, gx0, gy0, cell, nx, ny):
    """Robust height per cell: median for >= 3 points, mean for 1-2 points, nan if empty.
    Returns (h, count)."""
    c = cell_index(x, y, gx0, gy0, cell, nx, ny)
    ok = c >= 0
    c, zz = c[ok], z[ok]
    h = np.full(nx * ny, np.nan)
    cnt = np.bincount(c, minlength=nx * ny)
    if c.size:
        order = np.lexsort((zz, c))
        c, zz = c[order], zz[order]
        starts = np.flatnonzero(np.r_[True, c[1:] != c[:-1]])
        counts = np.diff(np.r_[starts, c.size])
        sums = np.add.reduceat(zz, starts)
        mid = zz[starts + counts // 2]
        mid_lo = zz[starts + np.maximum(counts - 1, 0) // 2]
        med = np.where(counts % 2 == 1, mid, (mid + mid_lo) / 2)
        h[c[starts]] = np.where(counts >= 3, med, sums / counts)
    return h.reshape(ny, nx), cnt.reshape(ny, nx)


def fill_holes(h, cell):
    """Fill empty cells: linear interpolation over a triangulation of the measured cells
    (a TIN), then nearest value outside it. Returns (filled, gap_m) where gap_m is the
    distance from each cell to the nearest measured cell (0 if measured)."""
    from scipy.interpolate import griddata
    from scipy.ndimage import distance_transform_edt
    meas = np.isfinite(h)
    if not meas.any():
        raise ValueError("no ground points in this grid")
    gap = distance_transform_edt(~meas) * cell
    out = h.copy()
    if (~meas).any():
        jj, ii = np.nonzero(meas)
        hj, hi = np.nonzero(~meas)
        vals = h[meas]
        if meas.sum() >= 3:
            try:
                out[~meas] = griddata((ii, jj), vals, (hi, hj), method="linear")
            except Exception:       # all measured cells in a line: no triangulation
                pass
        left = ~np.isfinite(out)
        if left.any():
            _, (nj, ni) = distance_transform_edt(~meas, return_indices=True)
            out[left] = h[nj[left], ni[left]]
    return out, gap


# ---- breaks -------------------------------------------------------------------

def _step_fit(s, z, half=0.25):
    """Fit z = a + b*s (slope) and z = a + b*s + g*[s > s0] (step), where the step position s0
    may be anywhere within +-half of the edge (s = 0); every split between two neighboring
    points in that range is tried. Returns (g, a, b, rms_step, rms_slope, s0)."""
    order = np.argsort(s)
    s, z = s[order], z[order]
    A1 = np.c_[np.ones_like(s), s]
    c1, *_ = np.linalg.lstsq(A1, z, rcond=None)
    r1 = np.sqrt(np.mean((A1 @ c1 - z) ** 2))
    best = None
    for k in range(2, len(s) - 1):                 # left = first k points (at least 2 each side)
        s0 = (s[k - 1] + s[k]) / 2
        if not (-half < s0 <= half):               # a step nearer another edge belongs to that edge
            continue
        A2 = np.c_[A1, (s > s0).astype(float)]
        c2, *_ = np.linalg.lstsq(A2, z, rcond=None)
        r2 = np.sqrt(np.mean((A2 @ c2 - z) ** 2))
        if best is None or r2 < best[3]:
            best = (c2[2], c2[0], c2[1], r2, r1, s0)
    if best is None:
        return 0.0, c1[0], c1[1], r1, r1, 0.0
    return best


def detect_breaks(x, y, z, hf, meas, gx0, gy0, cell, region, min_step=BREAK_MIN):
    """Find steps between neighboring cells inside region = (j0, j1, i0, i1) of the grid.

    An edge is a candidate when the filled heights change by >= 0.12 m across it (or across
    the cell next to it, which catches a curb that runs through a cell). It becomes a break
    when the LiDAR points in the 2 m x 0.5 m strip across the edge fit a STEP model
    (plane + jump at the edge) much better than a SLOPE model (one plane), with a jump of
    >= min_step. Gentle and steady slopes fit the slope model and stay smooth.
    Of two accepted neighboring edges only the better fit is kept.

    Returns (bx, by, h_sharp, info): break masks for the region's cells, the cell heights
    with the two cells beside each break set from the step fit (so a cell that straddles
    a curb takes the height of its side), and counts.
    """
    ny, nx = hf.shape
    j0, j1, i0, i1 = region
    n_r, n_c = j1 - j0, i1 - i0
    c = cell_index(x, y, gx0, gy0, cell, nx, ny)
    ok = c >= 0
    order = np.argsort(c[ok], kind="stable")
    px, py, pz, pc = x[ok][order], y[ok][order], z[ok][order], c[ok][order]
    starts = np.searchsorted(pc, np.arange(nx * ny))
    ends = np.searchsorted(pc, np.arange(nx * ny), side="right")

    def pts(cells):
        sl = [np.arange(starts[k], ends[k]) for k in cells]
        return np.concatenate(sl) if sl else np.zeros(0, np.int64)

    sharp_sum = np.zeros_like(hf)
    sharp_n = np.zeros_like(hf)
    results = {}
    for axis in (0, 1):                    # 0: x-edges (cells side by side), 1: y-edges
        if axis == 0:
            d_lo = np.abs(hf[j0:j1, i0 + 1:i1] - hf[j0:j1, i0:i1 - 1])
        else:
            d_lo = np.abs(hf[j0 + 1:j1, i0:i1] - hf[j0:j1 - 1, i0:i1])
        # also look one cell further on each side (curb inside a cell)
        cand = d_lo >= 0.12
        # edge between cells i and i+1: also compare h[i+1] - h[i-1] and h[i+2] - h[i]
        # (the region must sit at least 2 cells inside the grid, which the 10 m buffer gives)
        if axis == 0:
            cand |= np.abs(hf[j0:j1, i0 + 1:i1] - hf[j0:j1, i0 - 1:i1 - 2]) >= 0.15
            cand |= np.abs(hf[j0:j1, i0 + 2:i1 + 1] - hf[j0:j1, i0:i1 - 1]) >= 0.15
        else:
            cand |= np.abs(hf[j0 + 1:j1, i0:i1] - hf[j0 - 1:j1 - 2, i0:i1]) >= 0.15
            cand |= np.abs(hf[j0 + 2:j1 + 1, i0:i1] - hf[j0:j1 - 1, i0:i1]) >= 0.15
        acc = np.zeros(d_lo.shape, bool)
        score = np.zeros(d_lo.shape)
        fit = {}
        for rj, ri in zip(*np.nonzero(cand)):
            gj, gi = rj + j0, ri + i0                 # lower/left cell of the edge
            # points in a window 6 cells across the edge x 3 cells along it (3 m x 1.5 m at 0.5 m)
            across = (-2, -1, 0, 1, 2, 3)
            if axis == 0:
                line = [(gj + a, gi + k) for k in across for a in (-1, 0, 1)
                        if 0 <= gi + k < nx and 0 <= gj + a < ny]
                edge = gx0 + (gi + 1) * cell
            else:
                line = [(gj + k, gi + a) for k in across for a in (-1, 0, 1)
                        if 0 <= gj + k < ny and 0 <= gi + a < nx]
                edge = gy0 + (gj + 1) * cell
            idx = pts([jj * nx + ii for jj, ii in line])
            if idx.size < 4:
                continue
            s = (px[idx] if axis == 0 else py[idx]) - edge
            nl, nr = (s <= 0).sum(), (s > 0).sum()
            if nl < 2 or nr < 2:
                continue
            g, a0, b0, r_step, r_slope, _ = _step_fit(s, pz[idx], half=cell / 2)
            if abs(g) < min_step or r_step > max(0.05, 0.15 * abs(g)) or r_slope < 1.8 * r_step:
                continue
            lo = a0 - b0 * cell / 2
            hi = a0 + b0 * cell / 2 + g
            if abs(hi - lo) < min_step:
                continue
            acc[rj, ri] = True
            score[rj, ri] = abs(g) / (r_step + 0.01)
            fit[(rj, ri)] = (lo, hi)
        # keep only the better of two accepted edges in a row (non-maximum suppression)
        if axis == 0:
            both = acc[:, :-1] & acc[:, 1:]
            worse_left = both & (score[:, :-1] < score[:, 1:])
            worse_right = both & ~worse_left
            acc[:, :-1] &= ~worse_left
            acc[:, 1:] &= ~worse_right
        else:
            both = acc[:-1, :] & acc[1:, :]
            worse_lo = both & (score[:-1, :] < score[1:, :])
            worse_hi = both & ~worse_lo
            acc[:-1, :] &= ~worse_lo
            acc[1:, :] &= ~worse_hi
        for rj, ri in zip(*np.nonzero(acc)):
            lo, hi = fit[(rj, ri)]
            gj, gi = rj + j0, ri + i0
            other = (gj, gi + 1) if axis == 0 else (gj + 1, gi)
            sharp_sum[gj, gi] += lo
            sharp_n[gj, gi] += 1
            sharp_sum[other] += hi
            sharp_n[other] += 1
        results[axis] = (acc, int(cand.sum()))
    h_sharp = np.where(sharp_n > 0, sharp_sum / np.maximum(sharp_n, 1), hf)
    sub = h_sharp[j0:j1, i0:i1]
    bx = results[0][0] & (np.abs(sub[:, 1:] - sub[:, :-1]) >= min_step)
    by = results[1][0] & (np.abs(sub[1:, :] - sub[:-1, :]) >= min_step)
    found = int(bx.sum() + by.sum())
    # closing pass: a diagonal curb is a staircase of x- and y-edges; where two cells that
    # both border a found break still differ by >= min_step, the edge between them is a break too
    sides = break_sides(bx, by)
    bx |= sides[:, 1:] & sides[:, :-1] & (np.abs(sub[:, 1:] - sub[:, :-1]) >= min_step)
    by |= sides[1:, :] & sides[:-1, :] & (np.abs(sub[1:, :] - sub[:-1, :]) >= min_step)
    info = {"candidate_edges": results[0][1] + results[1][1], "step_edges": found,
            "closing_edges": int(bx.sum() + by.sum()) - found}
    return bx, by, h_sharp, info


def force_breaks(lines, h, x0, y0, cell, min_step=BREAK_MIN):
    """Breaks forced by mapped retaining walls: every cell edge whose two cell centers lie on
    opposite sides of a wall line, where the heights differ by >= min_step.
    lines: list of [(X, Y), ...] in campus meters. h: tile cell heights (n_r, n_c)."""
    import shapely
    from shapely.geometry import LineString
    n_r, n_c = h.shape
    fx = np.zeros((n_r, n_c - 1), bool)
    fy = np.zeros((n_r - 1, n_c), bool)
    if not lines:
        return fx, fy
    xs = x0 + (np.arange(n_c) + 0.5) * cell
    ys = y0 + (np.arange(n_r) + 0.5) * cell
    for coords in lines:
        wall = LineString(coords)
        bx0, by0, bx1, by1 = wall.bounds
        i_lo = max(int((bx0 - x0) / cell) - 2, 0)
        i_hi = min(int((bx1 - x0) / cell) + 2, n_c - 1)
        j_lo = max(int((by0 - y0) / cell) - 2, 0)
        j_hi = min(int((by1 - y0) / cell) + 2, n_r - 1)
        if i_lo > i_hi or j_lo > j_hi:
            continue
        for axis, out in ((0, fx), (1, fy)):
            jj, ii = np.mgrid[j_lo:j_hi + 1, i_lo:i_hi + 1]
            jj, ii = jj.ravel(), ii.ravel()
            if axis == 0:
                keep = ii < n_c - 1
                jj, ii = jj[keep], ii[keep]
                segs = shapely.linestrings(np.stack([np.c_[xs[ii], ys[jj]], np.c_[xs[ii + 1], ys[jj]]], axis=1))
                dh = np.abs(h[jj, ii + 1] - h[jj, ii])
            else:
                keep = jj < n_r - 1
                jj, ii = jj[keep], ii[keep]
                segs = shapely.linestrings(np.stack([np.c_[xs[ii], ys[jj]], np.c_[xs[ii], ys[jj + 1]]], axis=1))
                dh = np.abs(h[jj + 1, ii] - h[jj, ii])
            hit = shapely.intersects(segs, wall) & (dh >= min_step)
            out[jj[hit], ii[hit]] = True
    return fx, fy


def clean_breaks(bx, by, keep_x=None, keep_y=None, min_edges=3):
    """Drop break lines shorter than min_edges cell edges (noise), except forced ones.
    Two break edges belong to the same line when their midpoints are at most 1.5 cells
    apart (so the steps of a diagonal staircase count as one line)."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    from scipy.spatial import cKDTree
    ex_j, ex_i = np.nonzero(bx)        # x-edge (j, i): midpoint (i + 1, j + 0.5) in cell units
    ey_j, ey_i = np.nonzero(by)        # y-edge (j, i): midpoint (i + 0.5, j + 1)
    if ex_j.size + ey_j.size == 0:
        return bx, by, 0
    mids = np.r_[np.c_[ex_i + 1.0, ex_j + 0.5], np.c_[ey_i + 0.5, ey_j + 1.0]]
    pairs = cKDTree(mids).query_pairs(1.5 + 1e-6, output_type="ndarray")
    N = len(mids)
    g = coo_matrix((np.ones(len(pairs)), (pairs[:, 0], pairs[:, 1])), shape=(N, N))
    _, edge_lab = connected_components(g, directed=False)
    sizes = np.bincount(edge_lab)
    small = sizes[edge_lab] < min_edges
    forced = np.zeros(N, bool)
    if keep_x is not None:
        forced[:ex_j.size] = keep_x[ex_j, ex_i]
    if keep_y is not None:
        forced[ex_j.size:] = keep_y[ey_j, ey_i]
    drop = small & ~forced
    bx, by = bx.copy(), by.copy()
    bx[ex_j[drop[:ex_j.size]], ex_i[drop[:ex_j.size]]] = False
    by[ey_j[drop[ex_j.size:]], ey_i[drop[ex_j.size:]]] = False
    return bx, by, int(drop.sum())


def denoise(h, count, keep=None, passes=1):
    """Weighted 3 x 3 mean of cell heights (weight = point count, 0.5 for filled cells).
    With about one LiDAR point per 0.5 m cell, single cells carry a few cm of noise; this
    removes most of it so flat ground really is flat. Cells in `keep` (both sides of a
    break) keep their value; with a 3 x 3 window no other cell's window crosses a break."""
    from scipy.ndimage import uniform_filter
    w = np.where(count > 0, count, 0.5).astype(float)
    out = h.copy()
    for _ in range(passes):
        num = uniform_filter(out * w, size=3, mode="nearest")
        den = uniform_filter(w, size=3, mode="nearest")
        sm = num / den
        if keep is not None:
            sm = np.where(keep, out, sm)
        out = sm
    return out


def break_sides(bx, by):
    """Cells on either side of any break."""
    n_r, n_c = bx.shape[0], by.shape[1]
    f = np.zeros((n_r, n_c), bool)
    f[:, :-1] |= bx
    f[:, 1:] |= bx
    f[:-1, :] |= by
    f[1:, :] |= by
    return f


def node_heights(h_ext, pad, n):
    """Smooth node heights (breaks ignored) for the n x n tile cells starting at (pad, pad)
    of an extended cell grid: each node = mean of its 4 surrounding cells."""
    s = h_ext[pad - 1:pad + n + 1, pad - 1:pad + n + 1]
    return 0.25 * (s[:-1, :-1] + s[:-1, 1:] + s[1:, :-1] + s[1:, 1:])


# ---- tile borders -------------------------------------------------------------

def simplify_profile(z, tol):
    """Douglas-Peucker on an evenly spaced height profile. Returns a bool mask of kept
    samples (both ends always kept). Deterministic, so two tiles sharing an edge agree."""
    n = len(z)
    keep = np.zeros(n, bool)
    keep[0] = keep[-1] = True
    stack = [(0, n - 1)]
    while stack:
        a, b = stack.pop()
        if b - a < 2:
            continue
        t = np.arange(a + 1, b)
        line = z[a] + (z[b] - z[a]) * (t - a) / (b - a)
        err = np.abs(z[a + 1:b] - line)
        k = int(np.argmax(err))
        if err[k] > tol:
            m = a + 1 + k
            keep[m] = True
            stack.append((a, m))
            stack.append((m, b))
    return keep


# ---- mesh ---------------------------------------------------------------------

def _components(bx, by, n):
    """For every node, a component label (0..3) for each of its 4 cells (SW, SE, NW, NE).
    Cells around a node share a vertex unless a break separates them. Border nodes are
    always one vertex (so neighboring tiles meet without cracks)."""
    lab = np.zeros((n + 1, n + 1, 4), np.int8)
    if n < 2:
        return lab
    # links around interior node (J, I): True = connected (no break)
    J, I = np.mgrid[1:n, 1:n]
    Ls = ~bx[J - 1, I - 1]     # SW-SE
    Le = ~by[J - 1, I]         # SE-NE
    Ln = ~bx[J, I - 1]         # NW-NE
    Lw = ~by[J - 1, I - 1]     # SW-NW
    missing = 4 - (Ls.astype(int) + Le + Ln + Lw)
    for jj, ii in zip(*np.nonzero(missing >= 2)):
        links = [(SW, SE, Ls[jj, ii]), (SE, NE, Le[jj, ii]), (NW, NE, Ln[jj, ii]), (SW, NW, Lw[jj, ii])]
        parent = list(range(4))

        def find(a):
            while parent[a] != a:
                a = parent[a]
            return a
        for a, b, linked in links:
            if linked:
                ra, rb = find(a), find(b)
                if ra != rb:
                    parent[max(ra, rb)] = min(ra, rb)
        roots = [find(k) for k in range(4)]
        order = {r: q for q, r in enumerate(sorted(set(roots)))}
        lab[jj + 1, ii + 1] = [order[r] for r in roots]
    return lab


def _bary(tri_xy, tri_z, px, py):
    """Linear interpolation of z over a triangle at points (px, py); also returns the
    barycentric weights."""
    (x1, y1), (x2, y2), (x3, y3) = tri_xy
    det = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3)
    l1 = ((y2 - y3) * (px - x3) + (x3 - x2) * (py - y3)) / det
    l2 = ((y3 - y1) * (px - x3) + (x1 - x3) * (py - y3)) / det
    l3 = 1 - l1 - l2
    return l1 * tri_z[0] + l2 * tri_z[1] + l3 * tri_z[2], (l1, l2, l3)


def _sat(a):
    """Summed-area table with a zero row/column in front."""
    s = np.zeros((a.shape[0] + 1, a.shape[1] + 1), np.int64)
    s[1:, 1:] = np.cumsum(np.cumsum(a.astype(np.int64), 0), 1)
    return s


def _sat_sum(s, j0, j1, i0, i1):
    if j1 <= j0 or i1 <= i0:
        return 0
    return s[j1, i1] - s[j0, i1] - s[j1, i0] + s[j0, i0]


def _block_fit(NH, tol):
    """Can this block of nodes be drawn as one quad? Tries the 2-triangle split (both
    diagonals) and the 4-triangle fan around the center. tol may be one number or an
    array the shape of NH (looser where no LiDAR point was seen).
    Returns (ok, mode, center_z, (diag_a_ok, diag_b_ok, fan_ok))."""
    h, w = NH.shape[0] - 1, NH.shape[1] - 1
    v, u = np.mgrid[0:h + 1, 0:w + 1]
    u, v = u / w, v / h
    z00, z10, z01, z11 = NH[0, 0], NH[0, -1], NH[-1, 0], NH[-1, -1]
    # diagonal SW-NE
    za = np.where(u >= v, z00 + (z10 - z00) * u + (z11 - z10) * v, z00 + (z01 - z00) * v + (z11 - z01) * u)
    ea = (np.abs(za - NH) / tol).max()
    # diagonal SE-NW
    zb = np.where(u + v <= 1, z00 + (z10 - z00) * u + (z01 - z00) * v,
                  z11 + (z01 - z11) * (1 - u) + (z10 - z11) * (1 - v))
    eb = (np.abs(zb - NH) / tol).max()
    if min(ea, eb) <= 1:                # a 2-triangle split is good enough; skip the fan
        return True, ("diag_a" if ea <= eb else "diag_b"), None, (ea <= 1, eb <= 1, False)
    # fan around the center (bilinear center height)
    cu, cv = w / 2, h / 2
    i0, j0 = int(np.floor(cu)), int(np.floor(cv))
    i1, j1 = min(i0 + 1, w), min(j0 + 1, h)
    fu, fv = cu - i0, cv - j0
    zc = (NH[j0, i0] * (1 - fu) * (1 - fv) + NH[j0, i1] * fu * (1 - fv)
          + NH[j1, i0] * (1 - fu) * fv + NH[j1, i1] * fu * fv)
    du, dv = (u - 0.5), (v - 0.5)
    corners = {"b": ((0, 0), (1, 0), z00, z10), "r": ((1, 0), (1, 1), z10, z11),
               "t": ((1, 1), (0, 1), z11, z01), "l": ((0, 1), (0, 0), z01, z00)}
    sector = np.where(np.abs(du) >= np.abs(dv), np.where(du >= 0, "r", "l"), np.where(dv >= 0, "t", "b"))
    zf = np.zeros_like(NH)
    for key, (p, q, zp, zq) in corners.items():
        m = sector == key
        if m.any():
            zf[m], _ = _bary(((0.5, 0.5), p, q), (zc, zp, zq), u[m], v[m])
    ef = (np.abs(zf - NH) / tol).max()
    # errors are now in units of the tolerance: <= 1 passes
    best = min((ea, "diag_a"), (eb, "diag_b"), (ef, "fan"))
    return best[0] <= 1, best[1], (zc if best[1] == "fan" else None), (ea <= 1, eb <= 1, ef <= 1)


def _zipper(c1, c2):
    """Triangulate a rectangle whose border is split into two vertex chains that share their
    first and last vertex (c1 counter-clockwise, c2 clockwise). Gives P - 2 triangles for P
    border vertices, all counter-clockwise seen from above, none degenerate."""
    n1, n2 = len(c1) - 1, len(c2) - 1
    tris = [(c1[0], c1[1], c2[1])]
    i, j = 1, 1
    while not (i == n1 - 1 and j == n2 - 1):
        if i == n1 - 1 or (j < n2 - 1 and (j + 1) / n2 <= (i + 1) / n1):
            tris.append((c1[i], c2[j + 1], c2[j]))
            j += 1
        else:
            tris.append((c1[i], c1[i + 1], c2[j]))
            i += 1
    tris.append((c1[n1 - 1], c1[n1], c2[n2 - 1]))
    return tris


def _greedy_rectangles(n, forced, mat, node_block, NT, max_side=64):
    """Cover the cells with rectangles, scanning from the south-west corner. Each rectangle
    starts at the first free cell and grows east and north, one column/row at a time, while
    the new cells are free, not beside a break, the same material, and the rectangle still
    fits its nodes within the tolerance. Rectangles follow staircase material edges as
    strips instead of breaking them into single cells."""
    free = np.ones((n, n), bool)
    leaves = []
    for j in range(n):
        for i in range(n):
            if not free[j, i]:
                continue
            if forced[j, i]:
                free[j, i] = False
                leaves.append((j, j + 1, i, i + 1, "cell", None))
                continue
            m = mat[j, i]
            w = h = 1
            best = None
            grow_e = grow_n = True
            while grow_e or grow_n:
                if grow_e:
                    ii = i + w
                    if (w < max_side and ii < n and free[j:j + h, ii].all() and not forced[j:j + h, ii].any()
                            and (mat[j:j + h, ii] == m).all()):
                        fit = _block_fit(node_block(j, j + h, i, ii + 1), NT[j:j + h + 1, i:ii + 2])
                        if fit[0]:
                            w += 1
                            best = fit
                        else:
                            grow_e = False
                    else:
                        grow_e = False
                if grow_n:
                    jj = j + h
                    if (h < max_side and jj < n and free[jj, i:i + w].all() and not forced[jj, i:i + w].any()
                            and (mat[jj, i:i + w] == m).all()):
                        fit = _block_fit(node_block(j, jj + 1, i, i + w), NT[j:jj + 2, i:i + w + 1])
                        if fit[0]:
                            h += 1
                            best = fit
                        else:
                            grow_n = False
                    else:
                        grow_n = False
            free[j:j + h, i:i + w] = False
            if w == 1 and h == 1:
                leaves.append((j, j + 1, i, i + 1, "cell", None))
            else:
                # every accepted growth step stored its fit, so `best` is the final rectangle's fit
                leaves.append((j, j + h, i, i + w, best[1], (best[2], best[3], node_block(j, j + h, i, i + w))))
    return leaves


def build_mesh(hc, x0, y0, cell, bx=None, by=None, mat=None, border_z=None, border_keep=None,
               tol=0.03, merge_materials=False, uv_strip=True, mat_ids=None, hole=None, hole_tol=0.10,
               partition="greedy", max_side=64):
    """Triangle mesh of one tile.

    hc          (n, n) cell heights (breaks already sharpened)
    bx, by      break masks (None = no breaks)
    mat         (n, n) material index per cell (None = all the same)
    border_z    (n+1, n+1) node heights to use on the tile border (nan elsewhere)
    border_keep (n+1, n+1) border nodes that must be vertices (the shared border rule)
    tol         max vertical error (m) when merging flat cells into bigger quads
    merge_materials  True for the collider (materials ignored)
    hole        (n, n) True where no LiDAR ground point is near (under buildings); nodes whose
                4 cells are all holes may be off by hole_tol instead of tol (nothing to be
                accurate to there, and buildings cover most of it)
    Returns dict(verts, faces, face_mat, uv0, stats).
    """
    n = hc.shape[0]
    if bx is None:
        bx = np.zeros((n, n - 1), bool)
    if by is None:
        by = np.zeros((n - 1, n), bool)
    if mat is None or merge_materials:
        mat_cells = np.zeros((n, n), np.int16) if mat is None else mat
        mat_split = np.zeros((n, n), np.int16)
    else:
        mat_cells = mat
        mat_split = mat
    mat_ids = mat_ids or {"curb": 19, "concrete": 4}

    # 1. vertices: one per (node, component)
    lab = _components(bx, by, n)
    ncomp = lab.max(axis=2).astype(np.int64) + 1
    base = np.zeros((n + 1) * (n + 1) + 1, np.int64)
    base[1:] = np.cumsum(ncomp.ravel())
    nverts = int(base[-1])
    vbase = base[:-1].reshape(n + 1, n + 1)
    # cell heights around each node (nan outside the tile)
    pad = np.full((n + 2, n + 2), np.nan)
    pad[1:-1, 1:-1] = hc
    around = np.stack([pad[:-1, :-1], pad[:-1, 1:], pad[1:, :-1], pad[1:, 1:]], axis=2)  # SW, SE, NW, NE
    vz_sum = np.zeros(nverts)
    vz_n = np.zeros(nverts)
    vid_k = vbase[:, :, None] + lab                                   # vertex id per (node, cell slot)
    fin = np.isfinite(around)
    np.add.at(vz_sum, vid_k[fin], around[fin])
    np.add.at(vz_n, vid_k[fin], 1)
    vz = vz_sum / np.maximum(vz_n, 1)
    JJ, II = np.mgrid[0:n + 1, 0:n + 1]
    node_of_v = np.repeat(np.arange((n + 1) * (n + 1)), ncomp.ravel())
    vx = x0 + II.ravel()[node_of_v] * cell
    vy = y0 + JJ.ravel()[node_of_v] * cell
    if border_z is not None:
        bmask = np.isfinite(border_z)
        nodes = np.flatnonzero(bmask.ravel())
        vz[vbase.ravel()[nodes]] = border_z.ravel()[nodes]        # border nodes have one vertex

    # corner vertex ids and heights of every cell
    CV = np.empty((n, n, 4), np.int64)
    CV[:, :, SW] = vid_k[:-1, :-1, NE]
    CV[:, :, SE] = vid_k[:-1, 1:, NW]
    CV[:, :, NW] = vid_k[1:, :-1, SE]
    CV[:, :, NE] = vid_k[1:, 1:, SW]
    CH = vz[CV]

    # 2. forced single cells: both sides of every break
    forced = np.zeros((n, n), bool)
    forced[:, :-1] |= bx
    forced[:, 1:] |= bx
    forced[:-1, :] |= by
    forced[1:, :] |= by
    s_forced = _sat(forced)
    s_mx = _sat(mat_split[:, 1:] != mat_split[:, :-1])
    s_my = _sat(mat_split[1:, :] != mat_split[:-1, :])
    # per-node tolerance
    NT = np.full((n + 1, n + 1), float(tol))
    if hole is not None:
        hp = np.zeros((n + 2, n + 2), bool)
        hp[1:-1, 1:-1] = hole
        all_hole = hp[:-1, :-1] & hp[:-1, 1:] & hp[1:, :-1] & hp[1:, 1:]
        NT[all_hole] = max(tol, hole_tol)

    def node_block(j0, j1, i0, i1):
        NH = np.empty((j1 - j0 + 1, i1 - i0 + 1))
        NH[:-1, :-1] = CH[j0:j1, i0:i1, SW]
        NH[-1, :-1] = CH[j1 - 1, i0:i1, NW]
        NH[:-1, -1] = CH[j0:j1, i1 - 1, SE]
        NH[-1, -1] = CH[j1 - 1, i1 - 1, NE]
        return NH

    # 3. merge cells into rectangles that are flat enough, one material, no breaks
    leaves = []
    if partition == "greedy":
        leaves = _greedy_rectangles(n, forced, mat_split, node_block, NT, max_side)
    stack = [(0, n, 0, n)] if partition == "quadtree" else []
    while stack:
        j0, j1, i0, i1 = stack.pop()
        h, w = j1 - j0, i1 - i0
        if h == 1 and w == 1:
            leaves.append((j0, j1, i0, i1, "cell", None))
            continue
        ok = (_sat_sum(s_forced, j0, j1, i0, i1) == 0
              and _sat_sum(s_mx, j0, j1, i0, i1 - 1) == 0
              and _sat_sum(s_my, j0, j1 - 1, i0, i1) == 0)
        if ok:
            NH = np.empty((h + 1, w + 1))
            NH[:-1, :-1] = CH[j0:j1, i0:i1, SW]
            NH[-1, :-1] = CH[j1 - 1, i0:i1, NW]
            NH[:-1, -1] = CH[j0:j1, i1 - 1, SE]
            NH[-1, -1] = CH[j1 - 1, i1 - 1, NE]
            fit_ok, mode, zc, okmodes = _block_fit(NH, NT[j0:j1 + 1, i0:i1 + 1])
            if fit_ok:
                leaves.append((j0, j1, i0, i1, mode, (zc, okmodes, NH)))
                continue
        # split the block in two along each dimension longer than one cell
        jm = j0 + h // 2 if h > 1 else None
        im = i0 + w // 2 if w > 1 else None
        js = [(j0, jm), (jm, j1)] if jm else [(j0, j1)]
        is_ = [(i0, im), (im, i1)] if im else [(i0, i1)]
        for a, b in js:
            for c, d in is_:
                stack.append((a, b, c, d))

    # 4. which nodes are vertices: every leaf corner and the kept border nodes
    used = np.zeros((n + 1, n + 1), bool)
    for j0, j1, i0, i1, _, _ in leaves:
        used[j0, i0] = used[j0, i1] = used[j1, i0] = used[j1, i1] = True
    if border_keep is not None:
        used |= border_keep

    def node_vid(J, I, j0, j1, i0, i1):
        cj = J if J < j1 else j1 - 1
        ci = I if I < i1 else i1 - 1
        corner = (0 if cj == J else 2) + (0 if ci == I else 1)
        return CV[cj, ci, corner]

    faces, fmat, kind = [], [], []           # kind 0 = ground, 1 = break face
    extra_xyz = []
    for j0, j1, i0, i1, mode, data in leaves:
        m = int(mat_cells[j0, i0])
        if mode == "cell":
            c = CV[j0, i0]
            # pick the diagonal whose midpoint is closest to the measured cell height
            da = abs((CH[j0, i0, SW] + CH[j0, i0, NE]) / 2 - hc[j0, i0])
            db = abs((CH[j0, i0, SE] + CH[j0, i0, NW]) / 2 - hc[j0, i0])
            if da <= db:
                faces += [(c[SW], c[SE], c[NE]), (c[SW], c[NE], c[NW])]
            else:
                faces += [(c[SW], c[SE], c[NW]), (c[SE], c[NE], c[NW])]
            fmat += [m, m]
            kind += [0, 0]
            continue
        zc, okmodes, NH = data
        per = []
        for I in range(i0, i1):                          # bottom, west -> east
            if used[j0, I]:
                per.append(node_vid(j0, I, j0, j1, i0, i1))
        for J in range(j0, j1):                          # right, south -> north
            if used[J, i1]:
                per.append(node_vid(J, i1, j0, j1, i0, i1))
        for I in range(i1, i0, -1):                      # top, east -> west
            if used[j1, I]:
                per.append(node_vid(j1, I, j0, j1, i0, i1))
        for J in range(j1, j0, -1):                      # left, north -> south
            if used[J, i0]:
                per.append(node_vid(J, i0, j0, j1, i0, i1))
        p00 = node_vid(j0, i0, j0, j1, i0, i1)
        p10 = node_vid(j0, i1, j0, j1, i0, i1)
        p11 = node_vid(j1, i1, j0, j1, i0, i1)
        p01 = node_vid(j1, i0, j0, j1, i0, i1)
        if mode in ("diag_a", "diag_b"):
            # two chains between opposite corners, zipped together: P - 2 triangles. The zipper's
            # rungs run across the chains, so for the SW-NE diagonal the chains go SE -> NW.
            start = per.index(p10) if mode == "diag_a" else per.index(p00)
            ring = per[start:] + per[:start]
            end = ring.index(p01) if mode == "diag_a" else ring.index(p11)
            c1 = ring[:end + 1]                       # counter-clockwise half
            c2 = [ring[0]] + ring[end:][::-1]         # clockwise half, same start and end
            tris = _zipper(c1, c2)
            faces += tris
            fmat += [m] * len(tris)
            kind += [0] * len(tris)
            continue
        # fan around a center vertex
        h, w = j1 - j0, i1 - i0
        if mode == "fan":
            cz = zc
        elif mode == "diag_a":
            cz = (NH[0, 0] + NH[-1, -1]) / 2
        else:
            cz = (NH[0, -1] + NH[-1, 0]) / 2
        cid = nverts + len(extra_xyz)
        extra_xyz.append((x0 + (i0 + w / 2) * cell, y0 + (j0 + h / 2) * cell, cz))
        for k in range(len(per)):
            faces.append((cid, per[k], per[(k + 1) % len(per)]))
            fmat.append(m)
            kind.append(0)

    # 5. vertical faces along breaks
    curb, concrete = mat_ids["curb"], mat_ids["concrete"]
    vfaces = []        # (tri, edge id) so each break edge gets one UV chart
    edges = []
    for axis, mask in ((0, bx), (1, by)):
        for j, i in zip(*np.nonzero(mask)):
            if axis == 0:     # between (j, i) [left] and (j, i+1) [right]
                a1, a2 = CV[j, i, SE], CV[j, i, NE]
                b1, b2 = CV[j, i + 1, SW], CV[j, i + 1, NW]
                lower_dir = np.array([1.0, 0.0]) if hc[j, i] > hc[j, i + 1] else np.array([-1.0, 0.0])
            else:             # between (j, i) [south] and (j+1, i) [north]
                a1, a2 = CV[j, i, NW], CV[j, i, NE]
                b1, b2 = CV[j + 1, i, SW], CV[j + 1, i, SE]
                lower_dir = np.array([0.0, 1.0]) if hc[j, i] > hc[j + 1, i] else np.array([0.0, -1.0])
            hgt = max(abs(vz[a1] - vz[b1]), abs(vz[a2] - vz[b2]))
            if hgt < 1e-4:
                continue
            fm = curb if hgt <= CURB_MAX else concrete
            eid = len(edges)
            edges.append((axis, j, i, hgt))
            for tri in ((a1, b1, b2), (a1, b2, a2)):
                if len(set(tri)) < 3:
                    continue
                vfaces.append((tri, eid, lower_dir, fm))

    verts = np.c_[vx, vy, vz]
    if extra_xyz:
        verts = np.vstack([verts, np.array(extra_xyz)])
    faces = np.array(faces, np.int64).reshape(-1, 3)
    fmat = np.array(fmat, np.int64)
    kind = np.array(kind, np.int8)
    vf_tris, vf_edge, vf_mat = [], [], []
    for tri, eid, lower_dir, fm in vfaces:
        p = verts[list(tri)]
        nrm = np.cross(p[1] - p[0], p[2] - p[0])
        if np.linalg.norm(nrm) < 1e-8:
            continue
        if nrm[:2] @ lower_dir < 0:
            tri = (tri[0], tri[2], tri[1])
        vf_tris.append(tri)
        vf_edge.append(eid)
        vf_mat.append(fm)
    if vf_tris:
        faces = np.vstack([faces, np.array(vf_tris, np.int64)])
        fmat = np.r_[fmat, vf_mat]
        kind = np.r_[kind, np.ones(len(vf_tris), np.int8)]

    # drop degenerate ground triangles (can happen when two break sides meet at one point)
    p0, p1, p2 = verts[faces[:, 0]], verts[faces[:, 1]], verts[faces[:, 2]]
    good = np.linalg.norm(np.cross(p1 - p0, p2 - p0), axis=1) > 1e-10
    vf_edge = np.array(vf_edge, np.int64)
    vf_good = good[kind == 1]
    faces, fmat, kind = faces[good], fmat[good], kind[good]
    vf_edge = vf_edge[vf_good] if vf_edge.size else vf_edge

    # 6. compact: keep only used vertices
    keep = np.zeros(len(verts), bool)
    keep[faces.ravel()] = True
    remap = np.cumsum(keep) - 1
    verts = verts[keep]
    faces = remap[faces]

    # 7. UV0
    uv, uv_info = make_uv(verts, faces, kind, vf_edge, x0, y0, cell, uv_strip)
    stats = {"triangles": int(len(faces)), "ground_triangles": int((kind == 0).sum()),
             "break_triangles": int((kind == 1).sum()), "leaves": len(leaves),
             "break_edges": int(len(edges)),
             "break_length_m": round(len(edges) * cell, 1),
             "break_face_area_m2": round(float(sum(e[3] for e in edges) * cell), 2),
             "max_break_m": round(float(max([e[3] for e in edges], default=0.0)), 3),
             "tol_m": tol, **uv_info}
    return {"verts": verts, "faces": faces.astype(np.int32), "face_mat": fmat.astype(np.uint8),
            "uv0": uv.astype(np.float32), "kind": kind, "stats": stats}


def make_uv(verts, faces, kind, vf_edge, x0, y0, cell, strip=True, tile=100.0, pad_m=0.05):
    """UV0: ground faces planar over the tile scaled into 0..0.94 (true shape, same
    meters per UV unit both ways); every break edge gets its own unrolled chart, shelf
    packed into the reserved strip v in (0.94, 1]. If the charts do not fit, all of them are
    scaled down by the same factor (recorded)."""
    m_per_unit = tile / UV_SCALE
    uv = np.zeros((len(faces), 3, 2))
    g = kind == 0
    P = verts[faces[g]]
    uv[g, :, 0] = (P[:, :, 0] - x0) / m_per_unit
    uv[g, :, 1] = (P[:, :, 1] - y0) / m_per_unit
    info = {"uv_m_per_unit": round(m_per_unit, 6), "uv_ground": f"u = (X - xmin) / {m_per_unit:.4f}, "
            f"v = (Y - ymin) / {m_per_unit:.4f} (0..{UV_SCALE})", "uv_break_strip": f"v in ({UV_SCALE}, 1]"}
    vidx = np.flatnonzero(kind == 1)
    if not vidx.size:
        info["uv_break_scale"] = 1.0
        return np.clip(uv, 0, 1), info
    if not strip:
        # collider: no UV needs; planar coordinates keep the array valid
        P = verts[faces[vidx]]
        uv[vidx, :, 0] = (P[:, :, 0] - x0) / m_per_unit
        uv[vidx, :, 1] = (P[:, :, 1] - y0) / m_per_unit
        info["uv_break_scale"] = None
        return np.clip(uv, 0, 1), info
    # local chart coordinates per break edge: (distance along the edge, height above its lowest point)
    charts = {}
    for f_i, e in zip(vidx, vf_edge):
        charts.setdefault(int(e), []).append(f_i)
    loc = {}
    sizes = {}
    for e, fl in charts.items():
        P = verts[faces[fl]].reshape(-1, 3)
        dx, dy = np.ptp(P[:, 0]), np.ptp(P[:, 1])
        along = (P[:, 0] - P[:, 0].min()) if dx >= dy else (P[:, 1] - P[:, 1].min())
        up = P[:, 2] - P[:, 2].min()
        loc[e] = np.c_[along, up].reshape(len(fl), 3, 2)
        sizes[e] = (max(along.max(), 1e-3), max(up.max(), 1e-3))
    strip_w = m_per_unit - pad_m
    strip_h = (1 - UV_SCALE) * m_per_unit - 2 * pad_m
    scale = 1.0
    keys = sorted(sizes, key=lambda e: -sizes[e][1])
    while True:
        off, x, y, shelf, ok = {}, pad_m, pad_m, 0.0, True
        for e in keys:
            w, h = sizes[e][0] * scale + pad_m, sizes[e][1] * scale + pad_m
            if x + w > strip_w:
                x, y, shelf = pad_m, y + shelf, 0.0
            if y + h > strip_h:
                ok = False
                break
            off[e] = (x, y)
            x += w
            shelf = max(shelf, h)
        if ok:
            break
        scale *= 0.9
    v0 = UV_SCALE * m_per_unit + pad_m
    for e, fl in charts.items():
        ox, oy = off[e]
        uv[fl, :, 0] = (ox + loc[e][:, :, 0] * scale) / m_per_unit
        uv[fl, :, 1] = (v0 + oy + loc[e][:, :, 1] * scale) / m_per_unit
    info["uv_break_scale"] = round(scale, 4)
    return np.clip(uv, 0, 1), info


# ---- mesh height at points (accuracy checks) ------------------------------------

def mesh_z_at(verts, faces, px, py, x0, y0, size=100.0, res=0.1, return_face=False):
    """Height of the mesh's non-vertical faces straight above/below each point (nan if
    no face covers it). Faces are drawn into an id image (res m pixels) to find the
    candidate face, then checked exactly with barycentric weights (neighbors tried too).
    With return_face, also returns the face index per point (-1 if none)."""
    import cv2
    P = verts[faces]
    nz = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0])[:, 2]
    flat = np.abs(nz) > 1e-9
    fid = np.flatnonzero(flat)
    N = int(round(size / res)) + 2
    img = np.full((N, N), -1, np.int32)
    for f in fid:
        pts = np.round(((P[f, :, :2] - (x0, y0)) / res) * 16).astype(np.int32)
        cv2.fillConvexPoly(img, pts, int(f), lineType=cv2.LINE_8, shift=4)
    out = np.full(len(px), np.nan)
    face = np.full(len(px), -1, np.int64)
    ci = np.clip(np.round((px - x0) / res).astype(int), 0, N - 1)
    cj = np.clip(np.round((py - y0) / res).astype(int), 0, N - 1)
    todo = np.ones(len(px), bool)
    for dj, di in [(0, 0), (0, 1), (1, 0), (0, -1), (-1, 0), (1, 1), (-1, -1), (1, -1), (-1, 1),
                   (0, 2), (2, 0), (0, -2), (-2, 0)]:
        if not todo.any():
            break
        idx = np.flatnonzero(todo)
        f = img[np.clip(cj[idx] + dj, 0, N - 1), np.clip(ci[idx] + di, 0, N - 1)]
        has = f >= 0
        idx, f = idx[has], f[has]
        if not idx.size:
            continue
        T = P[f]
        x1, y1, x2, y2, x3, y3 = T[:, 0, 0], T[:, 0, 1], T[:, 1, 0], T[:, 1, 1], T[:, 2, 0], T[:, 2, 1]
        det = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3)
        l1 = ((y2 - y3) * (px[idx] - x3) + (x3 - x2) * (py[idx] - y3)) / det
        l2 = ((y3 - y1) * (px[idx] - x3) + (x1 - x3) * (py[idx] - y3)) / det
        l3 = 1 - l1 - l2
        inside = (l1 >= -1e-9) & (l2 >= -1e-9) & (l3 >= -1e-9)
        z = l1 * T[:, 0, 2] + l2 * T[:, 1, 2] + l3 * T[:, 2, 2]
        out[idx[inside]] = z[inside]
        face[idx[inside]] = f[inside]
        todo[idx[inside]] = False
    # thin triangles may cover no pixel: test the rest against every face whose box holds them
    if todo.any():
        fP = P[fid]
        bmin, bmax = fP[:, :, :2].min(axis=1), fP[:, :, :2].max(axis=1)
        for k in np.flatnonzero(todo):
            q = np.array([px[k], py[k]])
            c = np.flatnonzero(((bmin <= q + 1e-9) & (bmax >= q - 1e-9)).all(axis=1))
            if not c.size:
                continue
            z, ok = _bary_many(fP[c], q[0], q[1])
            if ok.any():
                out[k] = z[np.argmax(ok)]
                face[k] = fid[c[np.argmax(ok)]]
    return (out, face) if return_face else out


def _bary_many(T, qx, qy):
    """Height at one point over several triangles; (z, inside) arrays."""
    x1, y1, x2, y2, x3, y3 = T[:, 0, 0], T[:, 0, 1], T[:, 1, 0], T[:, 1, 1], T[:, 2, 0], T[:, 2, 1]
    det = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3)
    l1 = ((y2 - y3) * (qx - x3) + (x3 - x2) * (qy - y3)) / det
    l2 = ((y3 - y1) * (qx - x3) + (x1 - x3) * (qy - y3)) / det
    l3 = 1 - l1 - l2
    inside = (l1 >= -1e-9) & (l2 >= -1e-9) & (l3 >= -1e-9)
    return l1 * T[:, 0, 2] + l2 * T[:, 1, 2] + l3 * T[:, 2, 2], inside


def error_stats(err):
    e = np.abs(err[np.isfinite(err)])
    if not e.size:
        return {"points": 0}
    return {"points": int(e.size), "median_m": round(float(np.median(e)), 4),
            "p95_m": round(float(np.percentile(e, 95)), 4), "max_m": round(float(e.max()), 3),
            "within_0.15_pct": round(float((e <= 0.15).mean() * 100), 2),
            "signed_mean_m": round(float(np.nanmean(err)), 4)}
