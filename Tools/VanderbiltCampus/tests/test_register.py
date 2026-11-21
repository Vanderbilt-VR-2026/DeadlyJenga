"""Stage 3 registration math on a synthetic street scene (no COLMAP run, no archive).

A known similarity maps a "COLMAP" model into the campus frame. The scene has
facade points on wall planes and ground points; 12 cameras with known poses
look at it, the points are projected into their images and triangulated back
(so the sparse points carry the usual small SfM noise). Then GPS-start Umeyama
RANSAC + point-to-plane refinement must recover the transform within
2 cm / 0.05 deg.
"""
import sys
from pathlib import Path

import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
import align_geom as geom  # noqa: E402
import register_lidar as rl  # noqa: E402

F, W, H = 1500.0, 2000, 1500


def look_rotation(fwd):
    """cam_from_world rotation for an upright camera looking along fwd (campus Z up)."""
    z = np.asarray(fwd, float)
    z /= np.linalg.norm(z)
    x = np.cross(z, [0, 0, 1.0])
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    return np.stack([x, y, z])          # rows = camera axes in world


def scene(seed=0, walls=("west", "east", "north")):
    """Points on wall planes and the ground (campus frame) plus plane targets."""
    rng = np.random.default_rng(seed)
    P, targets = [], {"facade": [], "ground": []}
    if "west" in walls:     # x = -8, facing +x, y in [0, 60]
        p = np.column_stack([np.full(400, -8.0), rng.uniform(0, 60, 400), rng.uniform(0.5, 12, 400)])
        P.append(p); targets["facade"].append(((-8, 0, 0), (1, 0, 0), "x", (0, 60), (0, 14)))
    if "east" in walls:     # x = +8, facing -x
        p = np.column_stack([np.full(400, 8.0), rng.uniform(0, 60, 400), rng.uniform(0.5, 12, 400)])
        P.append(p); targets["facade"].append(((8, 0, 0), (-1, 0, 0), "x", (0, 60), (0, 14)))
    if "north" in walls:    # y = 70, facing -y
        p = np.column_stack([rng.uniform(-8, 8, 200), np.full(200, 70.0), rng.uniform(0.5, 12, 200)])
        P.append(p); targets["facade"].append(((0, 70, 0), (0, -1, 0), "y", (-8, 8), (0, 14)))
    g = np.column_stack([rng.uniform(-7, 7, 500), rng.uniform(-5, 65, 500), np.zeros(500)])
    return np.vstack(P), g


def target_samples(walls, spacing=0.25):
    """Dense plane samples standing in for LiDAR facade points and LiDAR ground."""
    fac_P, fac_N = [], []
    ys = np.arange(-2, 72, spacing)
    zs = np.arange(0, 14, spacing)
    if "west" in walls:
        Y, Z = np.meshgrid(ys, zs)
        fac_P.append(np.column_stack([np.full(Y.size, -8.0), Y.ravel(), Z.ravel()])); fac_N.append([1, 0, 0])
    if "east" in walls:
        Y, Z = np.meshgrid(ys, zs)
        fac_P.append(np.column_stack([np.full(Y.size, 8.0), Y.ravel(), Z.ravel()])); fac_N.append([-1, 0, 0])
    if "north" in walls:
        X, Z = np.meshgrid(np.arange(-9, 9, spacing), zs)
        fac_P.append(np.column_stack([X.ravel(), np.full(X.size, 70.0), Z.ravel()])); fac_N.append([0, -1, 0])
    FP = np.vstack(fac_P)
    FN = np.vstack([np.tile(n, (len(p), 1)) for p, n in zip(fac_P, fac_N)]).astype(float)
    X, Y = np.meshgrid(np.arange(-8, 8, 0.5), np.arange(-6, 72, 0.5))
    GP = np.column_stack([X.ravel(), Y.ravel(), np.zeros(X.size)])
    GN = np.tile([0, 0, 1.0], (len(GP), 1))
    return rl.make_target(FP, FN), rl.make_target(GP, GN)


def cameras():
    """12 street cameras driving north along x = 0, 1.6 m up, slight yaw wiggle."""
    out = []
    for i in range(12):
        C = np.array([np.sin(i) * 1.0, -5 + 5.0 * i, 1.6])
        yaw = np.radians(8 * np.sin(1.7 * i))
        fwd = np.array([np.sin(yaw), np.cos(yaw), -0.05])
        out.append((C, look_rotation(fwd)))
    return out


def triangulate(P, cams, rng, noise_px=0.3):
    """Project points into every camera that sees them, add pixel noise, triangulate (DLT)."""
    K = np.array([[F, 0, W / 2], [0, F, H / 2], [0, 0, 1]])
    out, keep = [], []
    for j, X in enumerate(P):
        rows = []
        for C, R in cams:
            t = -R @ C
            xc = R @ X + t
            if xc[2] < 1:
                continue
            uv = (K @ xc)[:2] / xc[2]
            if not (0 <= uv[0] < W and 0 <= uv[1] < H):
                continue
            uv = uv + rng.normal(0, noise_px, 2)
            Pm = K @ np.hstack([R, t[:, None]])
            rows.append(uv[0] * Pm[2] - Pm[0])
            rows.append(uv[1] * Pm[2] - Pm[1])
        if len(rows) < 4:
            continue
        _, _, Vt = np.linalg.svd(np.array(rows))
        Xh = Vt[-1]
        out.append(Xh[:3] / Xh[3])
        keep.append(j)
    return np.array(out), np.array(keep)


def true_transform():
    """campus_from_colmap: scale 0.37, a tilted rotation, a big offset (like a COLMAP frame)."""
    R = geom.small_rotation([0.4, -1.1, 2.3])
    return geom.sim_matrix(1 / 0.37, R, [512.3, -84.1, 21.7])


def make_model(walls, seed=0):
    rng = np.random.default_rng(seed)
    Pf, Pg = scene(seed, walls)
    cams = cameras()
    Tf, kf = triangulate(Pf, cams, rng)
    Tg, kg = triangulate(Pg, cams, rng)
    M = true_transform()
    Mi = np.linalg.inv(M)
    # The "COLMAP" model: points and cameras in the model frame.
    Pf_c = geom.sim_apply(Mi, Tf)
    Pg_c = geom.sim_apply(Mi, Tg)
    C_c, R_c = [], []
    s, Rm, _ = geom.sim_parts(M)
    for C, R in cams:
        C_c.append(geom.sim_apply(Mi, C[None])[0])
        R_c.append(R @ Rm)                  # cam_from_model = cam_from_campus * campus_from_model rotation
    return M, Pf_c, Pg_c, np.array(C_c), np.array(R_c), cams


def errors(M_est, M_true, P):
    """Max point error (m) over P (model frame) and rotation error (deg)."""
    d = np.linalg.norm(geom.sim_apply(M_est, P) - geom.sim_apply(M_true, P), axis=1).max()
    dR = geom.rotation_angle_deg(geom.sim_parts(M_est)[1] @ geom.sim_parts(M_true)[1].T)
    return float(d), dR


def test_umeyama_exact_and_ransac_with_outliers():
    rng = np.random.default_rng(3)
    src = rng.normal(0, 10, (40, 3))
    M = true_transform()
    dst = geom.sim_apply(M, src)
    assert np.allclose(geom.umeyama(src, dst), M, atol=1e-8)
    noisy = dst + rng.normal(0, 2.0, dst.shape)          # GPS-like noise
    noisy[:8] += rng.uniform(30, 60, (8, 3))             # 20 % gross outliers
    M2, inl = geom.umeyama_ransac(src, noisy, threshold=8.0)
    assert inl[:8].sum() == 0 and inl[8:].mean() > 0.9
    s2 = geom.sim_parts(M2)[0]
    assert abs(s2 / geom.sim_parts(M)[0] - 1) < 0.05


def test_gps_start_then_plane_refinement_recovers_similarity():
    walls = ("west", "east", "north")
    M, Pf, Pg, C, R, cams = make_model(walls)
    rng = np.random.default_rng(7)
    # GPS: true camera positions with ~5 m noise, height from "LiDAR ground" + 1.6 m.
    gps = np.array([c for c, _ in cams]) + np.column_stack([rng.normal(0, 4, (12, 2)), np.zeros(12)])
    heading = [np.degrees(np.arctan2(r[2, 0], r[2, 1])) % 360 for _, r in cams]
    M0, info = rl.gps_start(C, R, gps, heading, threshold=12.0)
    assert info["heading_diff_median_deg"] < 10
    e0, r0 = errors(M0, M, Pf)
    assert e0 > 0.2                                       # the GPS start alone is not good enough
    T_fac, T_gnd = target_samples(walls)
    T = {"lidar_facade": T_fac, "footprint_wall": None, "ground": T_gnd}
    best = None
    for dy in (-3, 0, 3):
        c0 = geom.sim_apply(M0, C).mean(0)
        U = np.eye(4)
        Rz = geom.yaw_matrix(dy)
        U[:3, :3] = Rz
        U[:3, 3] = c0 - Rz @ c0
        Mr, cost, n = rl.icp(Pf, Pg, U @ M0, T, use=("lidar_facade", "ground"))
        if best is None or cost < best[0]:
            best = (cost, Mr)
    e, dr = errors(best[1], M, np.vstack([Pf, Pg]))
    assert e < 0.02, f"max point error {e:.4f} m"
    assert dr < 0.05, f"rotation error {dr:.4f} deg"
    s_est, s_true = geom.sim_parts(best[1])[0], geom.sim_parts(M)[0]
    assert abs(s_est / s_true - 1) < 0.001


def test_two_walls_and_ground_with_known_scale():
    """Two crossing walls + ground fix rotation and translation but not scale (all three
    planes meet in one point); with the scale held (fix_scale) the rest is recovered."""
    walls = ("west", "north")
    M, Pf, Pg, C, R, cams = make_model(walls, seed=2)
    # Start about 1.5 m / 0.5 deg off (what the GPS start typically leaves), turned about the scene center.
    c = np.array([0.0, 30.0, 3.0])
    Rp = geom.small_rotation(np.radians([0.2, -0.2, 0.5]))
    U = np.eye(4)
    U[:3, :3] = Rp
    U[:3, 3] = c - Rp @ c + np.array([0.8, -1.0, 0.3])
    M0 = U @ M
    T_fac, T_gnd = target_samples(walls)
    T = {"lidar_facade": T_fac, "footprint_wall": None, "ground": T_gnd}
    Mr, cost, n = rl.icp(Pf, Pg, M0, T, use=("lidar_facade", "ground"), fix_scale=True)
    e, dr = errors(Mr, M, np.vstack([Pf, Pg]))
    assert e < 0.02 and dr < 0.05


def test_block_split_share():
    rng = np.random.default_rng(0)
    xy = rng.uniform(0, 500, (20000, 2))
    h = rl.block_split(xy, 0.3, seed=1)
    assert 0.2 < h.mean() < 0.4
    # Same block -> same side.
    h2 = rl.block_split(np.array([[1.0, 1.0], [9.0, 9.0], [11.0, 1.0]]), 0.5, seed=4)
    assert h2[0] == h2[1]


def test_local_planes_finds_vertical_wall():
    rng = np.random.default_rng(1)
    wall = np.column_stack([np.zeros(300), rng.uniform(0, 10, 300), rng.uniform(0, 10, 300)])
    n, pl, sp = geom.local_planes(wall, k=12)
    assert np.median(np.abs(n[:, 0])) > 0.99 and np.median(pl) > 0.3


def test_wall_segments_outward_normals():
    sq = np.array([[0, 0], [10, 0], [10, 10], [0, 10]], float)   # counter-clockwise
    segs = geom.wall_segments([sq], 0, 5)
    mids = [((a + b) / 2, n) for a, b, _, _, n in segs]
    for m, n in mids:
        assert np.dot(m - np.array([5, 5]), n) > 0
    segs_cw = geom.wall_segments([sq[::-1]], 0, 5)
    for a, b, _, _, n in segs_cw:
        assert np.dot((a + b) / 2 - np.array([5, 5]), n) > 0


if __name__ == "__main__":
    sys.exit(pytest.main([__file__, "-q"]))
