"""Small geometry helpers for Stage 3 alignment and registration (numpy only).

Used by align.py (chunk merging), register_lidar.py (GPS and LiDAR fits) and
the unit tests. Everything here is plain math so it can be tested without
COLMAP.

Conventions:
  A similarity transform maps a point p to  s * R @ p + t  (7 numbers:
  1 scale, 3 rotation, 3 translation). As a 4x4 matrix it is
  [[s*R, t], [0 0 0 1]].
  COLMAP poses are cam_from_world: x_cam = R_cw @ x_world + t_cw, and the
  camera center is C = -R_cw.T @ t_cw.
"""
import numpy as np


# ---------------------------------------------------------------------------
# Similarity transforms
# ---------------------------------------------------------------------------

def sim_matrix(s, R, t):
    M = np.eye(4)
    M[:3, :3] = s * np.asarray(R)
    M[:3, 3] = np.asarray(t).ravel()
    return M


def sim_parts(M):
    """Split a 4x4 similarity into (scale, rotation, translation)."""
    A = np.asarray(M)[:3, :3]
    s = float(np.cbrt(np.linalg.det(A)))
    return s, A / s, np.asarray(M)[:3, 3].copy()


def sim_apply(M, P):
    P = np.asarray(P, dtype=float)
    return P @ np.asarray(M)[:3, :3].T + np.asarray(M)[:3, 3]


def umeyama(src, dst, with_scale=True, weights=None):
    """Least-squares similarity dst ~ s R src + t (Umeyama 1991). Returns a 4x4 matrix."""
    src = np.asarray(src, dtype=float)
    dst = np.asarray(dst, dtype=float)
    w = np.ones(len(src)) if weights is None else np.asarray(weights, dtype=float)
    w = w / w.sum()
    mu_s = (w[:, None] * src).sum(0)
    mu_d = (w[:, None] * dst).sum(0)
    a, b = src - mu_s, dst - mu_d
    cov = (w[:, None] * b).T @ a
    U, D, Vt = np.linalg.svd(cov)
    S = np.eye(3)
    if np.linalg.det(U) * np.linalg.det(Vt) < 0:
        S[2, 2] = -1
    R = U @ S @ Vt
    var_s = (w * (a ** 2).sum(1)).sum()
    s = float(np.trace(np.diag(D) @ S) / var_s) if with_scale and var_s > 0 else 1.0
    t = mu_d - s * R @ mu_s
    return sim_matrix(s, R, t)


def umeyama_ransac(src, dst, threshold, iters=2000, min_inliers=3, seed=0, with_scale=True,
                   sample_ok=None):
    """Robust Umeyama: random 3-point samples, keep the transform with the most inliers
    (distance after transform < threshold), then refit on all inliers.

    sample_ok(M) may reject a candidate (for example a scale far from expected).
    Returns (4x4 matrix, inlier mask) or (None, None) when nothing fits."""
    src = np.asarray(src, dtype=float)
    dst = np.asarray(dst, dtype=float)
    n = len(src)
    if n < 3:
        return None, None
    rng = np.random.default_rng(seed)
    best, best_count, best_err = None, 0, np.inf
    for _ in range(iters):
        idx = rng.choice(n, 3, replace=False)
        a = src[idx]
        # Skip nearly collinear samples (they do not fix the rotation).
        if np.linalg.norm(np.cross(a[1] - a[0], a[2] - a[0])) < 1e-6 * max(1.0, np.ptp(src) ** 2):
            continue
        M = umeyama(a, dst[idx], with_scale)
        if sample_ok is not None and not sample_ok(M):
            continue
        d = np.linalg.norm(sim_apply(M, src) - dst, axis=1)
        inl = d < threshold
        c = int(inl.sum())
        err = float(np.mean(np.minimum(d, threshold)))
        if c > best_count or (c == best_count and err < best_err):
            best, best_count, best_err = inl, c, err
    if best is None or best_count < min_inliers:
        return None, None
    M = umeyama(src[best], dst[best], with_scale)
    # One more pass: re-select inliers with the refit transform.
    d = np.linalg.norm(sim_apply(M, src) - dst, axis=1)
    inl = d < threshold
    if inl.sum() >= min_inliers:
        M = umeyama(src[inl], dst[inl], with_scale)
    return M, inl


def rotation_angle_deg(R):
    c = (np.trace(np.asarray(R)) - 1) / 2
    return float(np.degrees(np.arccos(np.clip(c, -1, 1))))


def small_rotation(w):
    """Rotation matrix for a small rotation vector w (Rodrigues)."""
    w = np.asarray(w, dtype=float)
    th = np.linalg.norm(w)
    if th < 1e-12:
        return np.eye(3)
    k = w / th
    K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
    return np.eye(3) + np.sin(th) * K + (1 - np.cos(th)) * K @ K


def yaw_matrix(deg):
    a = np.radians(deg)
    c, s = np.cos(a), np.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1.0]])


# ---------------------------------------------------------------------------
# Point-to-plane refinement of a similarity transform
# ---------------------------------------------------------------------------

def refine_point_to_plane(P, Q, N, M0, loss_scale=0.1, iters=30, fix_scale=False, weights=None,
                          scale_prior=None):
    """Refine a similarity M (src -> dst) so that the transformed source points P lie on
    the planes through Q with unit normals N (point i belongs to plane i).

    Gauss-Newton with a Huber-style robust weight (loss_scale in meters). Parameters are
    a small update [rotation(3), translation(3), log-scale(1)] applied on the left:
      M <- exp(update) * M
    scale_prior = (s0, sigma) adds a soft pull of the scale toward s0 (relative sigma, for
    example 0.02 = 2 %), so a point cloud cannot shrink onto a single plane.
    Returns the refined 4x4 matrix."""
    P = np.asarray(P, dtype=float)
    Q = np.asarray(Q, dtype=float)
    N = np.asarray(N, dtype=float)
    w0 = np.ones(len(P)) if weights is None else np.asarray(weights, dtype=float)
    M = np.array(M0, dtype=float)
    for _ in range(iters):
        X = sim_apply(M, P)                       # transformed points
        c = X.mean(0)                             # linearize around the centroid (better conditioned)
        r = ((X - Q) * N).sum(1)                  # signed distance to each plane
        a = np.abs(r)
        wr = np.where(a <= loss_scale, 1.0, loss_scale / np.maximum(a, 1e-12)) * w0
        Xc = X - c
        # d r / d(rot) = (Xc x N) . ; d r / d(trans) = N ; d r / d(logscale) = Xc . N
        J = np.hstack([np.cross(Xc, N), N, (Xc * N).sum(1, keepdims=True)])
        if fix_scale:
            J[:, 6] = 0
        H = J.T @ (wr[:, None] * J) + 1e-9 * np.eye(7)
        g = J.T @ (wr * r)
        if scale_prior is not None and not fix_scale:
            s0, sig = scale_prior
            wsum = float(wr.sum())
            ls = np.log(sim_parts(M)[0] / s0)
            H[6, 6] += wsum / sig ** 2 * loss_scale ** 2
            g[6] += wsum / sig ** 2 * loss_scale ** 2 * ls
        if fix_scale:
            H[6, 6] = 1.0
            g[6] = 0.0
        dx = -np.linalg.solve(H, g)
        Rd = small_rotation(dx[:3])
        sd = float(np.exp(dx[6]))
        # update about the centroid c: x -> sd * Rd (x - c) + c + dt
        U = np.eye(4)
        U[:3, :3] = sd * Rd
        U[:3, 3] = c + dx[3:6] - sd * Rd @ c
        M = U @ M
        if np.linalg.norm(dx[:6]) < 1e-7 and abs(dx[6]) < 1e-9:
            break
    return M


# ---------------------------------------------------------------------------
# Point neighborhoods: is a sparse point on a vertical wall or on the ground?
# ---------------------------------------------------------------------------

def local_planes(P, k=12, radius=None):
    """For every point: unit normal of its k-neighborhood, planarity and the RMS
    off-plane spread. Uses scipy's cKDTree."""
    from scipy.spatial import cKDTree
    P = np.asarray(P, dtype=float)
    n = len(P)
    normals = np.zeros((n, 3))
    planarity = np.zeros(n)
    spread = np.full(n, np.inf)
    if n < 4:
        return normals, planarity, spread
    tree = cKDTree(P)
    kk = min(k, n)
    d, idx = tree.query(P, kk)
    # Batched: neighbors outside the radius get zero weight.
    wgt = np.ones(d.shape) if radius is None else (d <= radius).astype(float)
    cnt = wgt.sum(1)
    for s in range(0, n, 200000):
        e = min(n, s + 200000)
        nb = P[idx[s:e]]                                   # (m, k, 3)
        w_ = wgt[s:e, :, None]
        c = cnt[s:e, None]
        mu = (nb * w_).sum(1) / np.maximum(c, 1)
        Y = (nb - mu[:, None, :]) * w_
        cov = np.einsum("mki,mkj->mij", Y, Y) / np.maximum(c, 1)[:, :, None]
        w, v = np.linalg.eigh(cov)
        ok = cnt[s:e] >= 5
        normals[s:e][ok] = v[ok, :, 0]
        l1, l2, l3 = w[:, 2], w[:, 1], w[:, 0]
        planarity[s:e][ok] = ((l2 - l3) / np.maximum(l1, 1e-12))[ok]
        spread[s:e][ok] = np.sqrt(np.maximum(l3, 0))[ok]
    return normals, planarity, spread


# ---------------------------------------------------------------------------
# Footprint walls
# ---------------------------------------------------------------------------

def wall_segments(polygons, z0, z1):
    """Vertical wall rectangles from footprint rings: list of (a, b, z0, z1, normal2d)."""
    out = []
    for ring in polygons:
        ring = np.asarray(ring, dtype=float)
        if len(ring) < 3:
            continue
        if np.allclose(ring[0], ring[-1]):
            ring = ring[:-1]
        area = 0.5 * np.sum(ring[:, 0] * np.roll(ring[:, 1], -1) - np.roll(ring[:, 0], -1) * ring[:, 1])
        for i in range(len(ring)):
            a, b = ring[i], ring[(i + 1) % len(ring)]
            e = b - a
            L = np.linalg.norm(e)
            if L < 0.5:
                continue
            # outward normal: right of the edge for counter-clockwise rings
            nrm = np.array([e[1], -e[0]]) / L
            if area < 0:
                nrm = -nrm
            out.append((a, b, z0, z1, nrm))
    return out


def nearest_on_segments(pts2, segs_a, segs_b):
    """Nearest point on any 2D segment for each 2D point. Returns (index, distance, foot)."""
    pts2 = np.asarray(pts2, dtype=float)
    A = np.asarray(segs_a, dtype=float)
    B = np.asarray(segs_b, dtype=float)
    E = B - A
    L2 = np.maximum((E ** 2).sum(1), 1e-12)
    best_i = np.zeros(len(pts2), dtype=int)
    best_d = np.full(len(pts2), np.inf)
    best_f = np.zeros_like(pts2)
    chunk = 2048
    for s in range(0, len(pts2), chunk):
        p = pts2[s:s + chunk, None, :]
        u = np.clip(((p - A[None]) * E[None]).sum(2) / L2[None], 0, 1)
        F = A[None] + u[..., None] * E[None]
        d = np.linalg.norm(p - F, axis=2)
        j = d.argmin(1)
        best_i[s:s + chunk] = j
        best_d[s:s + chunk] = d[np.arange(len(j)), j]
        best_f[s:s + chunk] = F[np.arange(len(j)), j]
    return best_i, best_d, best_f


def point_in_polygons(pts2, polygons):
    """True for 2D points inside any of the polygons (rings of [x, y])."""
    from matplotlib.path import Path as MplPath
    pts2 = np.asarray(pts2, dtype=float)
    inside = np.zeros(len(pts2), dtype=bool)
    for ring in polygons:
        inside |= MplPath(np.asarray(ring, dtype=float)).contains_points(pts2)
    return inside
