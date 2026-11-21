"""Stage 3 registration: move an aligned capture set (align.py) into the campus frame.

    python register_lidar.py --set kartaview-2026-11-19
    python register_lidar.py --set kartaview-2026-11-19 --fit-on lidar --starts 27

For every COLMAP component (align.py keeps them separately) with GPS on at least
--min-gps photos:

  (a) GPS start: a 7-DoF similarity (scale, rotation, translation) from the COLMAP
      camera centers to the GPS positions, robust Umeyama with RANSAC. GPS has no
      height, so the target height is LiDAR ground + --cam-height. Straight roads
      leave the roll about the road unknown, so the fit also uses each camera's
      "down" axis (photos are taken upright) as a gravity hint. The GPS heading is
      compared with the cameras' forward direction to catch a flipped solution.
  (b) LiDAR refinement: sparse points are sorted into
        facade  (local neighborhood is a near-vertical plane) and
        ground  (near-horizontal plane close to the LiDAR ground).
      They are fitted (point-to-plane, robust Huber loss, scale included) to
        "LiDAR facade points": LiDAR class 1/6 points whose neighborhood is vertical,
        "footprint walls":     building footprints (derived/buildings/v1) extruded from
                               ground to eave height, as vertical planes,
        LiDAR ground:          class 2 points with their local normal.
      ICP-style: match, fit, repeat with shrinking match distances, from several
      starts around the GPS start (yaw and shift offsets); the best robust cost wins.
  (c) Residuals (RMS / median / p95 / count) per data type, before and after, with the
      match cutoff stated (residuals only count points within --report-cutoff m of
      a target; the share within the cutoff is reported too).
  (d) Independent check: 30% of the area (10 m blocks) is held out of the fit and its
      residuals are reported separately; camera heights above LiDAR ground (1-3 m
      expected for street photos); cameras inside building footprints (violations).

Writes derived/alignment/<set>/<version>/registration.json and updates
cameras.json (campus pose per photo), points.ply (campus frame, all registered
components, with component and label columns) and report.json.
Exit code 1 when no component could be registered.
"""
import argparse
import datetime
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import align_geom as geom  # noqa: E402
from common import data_root, fail, load_json, save_json  # noqa: E402

LABELS = {"other": 0, "facade": 1, "ground": 2}


def log(msg):
    print(time.strftime("%H:%M:%S ") + msg, flush=True)


# ---------------------------------------------------------------------------
# Campus data around the cameras
# ---------------------------------------------------------------------------

class Ground:
    """LiDAR ground height lookup from derived/rasters/v1/dtm.tif (nearest cell)."""

    def __init__(self, root):
        import rasterio
        meta = load_json(root / "derived" / "rasters" / "v1" / "dtm.json")
        with rasterio.open(root / "derived" / "rasters" / "v1" / "dtm.tif") as r:
            self.z = r.read(1).astype(np.float32)
        self.ox, self.oy, self.cell = meta["origin_x"], meta["origin_y"], meta["cell_m"]

    def __call__(self, X, Y):
        X, Y = np.asarray(X, dtype=float), np.asarray(Y, dtype=float)
        c = np.floor((X - self.ox) / self.cell).astype(int)
        r = np.floor((self.oy - Y) / self.cell).astype(int)
        ok = (c >= 0) & (r >= 0) & (c < self.z.shape[1]) & (r < self.z.shape[0])
        out = np.full(X.shape, np.nan)
        out[ok] = self.z[r[ok], c[ok]]
        out[~np.isfinite(out)] = np.nan
        out[out < -100] = np.nan
        return out


def load_footprints(root, box):
    """Campus + ring footprints touching the box, with ground and eave heights."""
    fp = load_json(root / "derived" / "buildings" / "v1" / "footprints.json")
    out = []
    x0, y0, x1, y1 = box
    for b in fp["buildings"]:
        cx, cy = b["centroid"]
        if not (x0 - 150 < cx < x1 + 150 and y0 - 150 < cy < y1 + 150):
            continue
        polys = b["polygon"]
        rings = [np.asarray(poly[0], dtype=float) for poly in polys]   # outer rings only
        g = (b.get("ground") or {}).get("ground_z")
        roof = b.get("roof_estimate") or {}
        eave = roof.get("eave_z") or roof.get("mean_z")
        if g is None or eave is None:
            continue
        out.append({"id": b["id"], "name": b.get("name"), "rings": rings, "ground_z": float(g),
                    "eave_z": float(eave), "top_z": float(roof.get("top_z") or eave)})
    return out


def subsample(P, cell):
    """One point per cell (keeps the first): evens out the LiDAR density."""
    if len(P) == 0:
        return np.zeros(len(P), dtype=bool)
    key = np.floor(P / cell).astype(np.int64)
    _, idx = np.unique(key, axis=0, return_index=True)
    m = np.zeros(len(P), dtype=bool)
    m[idx] = True
    return m


def lidar_targets(root, cams_xy, corridor, ground=None, max_points=2000000, seed=0):
    """LiDAR ground points and LiDAR facade points (vertical neighborhoods) near the cameras."""
    import lidar_io
    from scipy.spatial import cKDTree
    x0, y0 = cams_xy.min(0) - corridor
    x1, y1 = cams_xy.max(0) + corridor
    pts = lidar_io.load_points(x0, y0, x1, y1, classes=[1, 2, 6])
    P = np.stack([pts["x"], pts["y"], pts["z"]], 1)
    cls = pts["classification"]
    if len(P) == 0:
        return None
    # Only points within the corridor around the camera path.
    d, _ = cKDTree(cams_xy).query(P[:, :2])
    keep = d <= corridor
    P, cls = P[keep], cls[keep]
    out = {}
    g = P[cls == 2]
    g = g[subsample(g, 0.5)]
    gn, gpl, _ = geom.local_planes(g, k=10)
    ok = np.abs(gn[:, 2]) > 0.8
    gn[gn[:, 2] < 0] *= -1
    out["ground"] = (g[ok], gn[ok])
    b = P[cls != 2]
    if ground is not None:
        hb = b[:, 2] - ground(b[:, 0], b[:, 1])
        b = b[np.isfinite(hb) & (hb > 1.0)]            # walls start above the ground clutter
    b = b[subsample(b, 0.25)]
    if len(b) > max_points:
        b = b[np.random.default_rng(seed).choice(len(b), max_points, replace=False)]
    bn, bpl, bsp = geom.local_planes(b, k=16, radius=2.0)
    vert = (np.abs(bn[:, 2]) < 0.25) & (bpl > 0.3) & (bsp < 0.15) & (np.linalg.norm(bn, axis=1) > 0.5)
    out["lidar_facade"] = (b[vert], bn[vert])
    out["counts"] = {"lidar_points_in_corridor": int(len(P)), "ground_targets": int(len(out["ground"][0])),
                     "lidar_facade_targets": int(vert.sum()), "building_and_unclassified_above_1m_sampled": int(len(b))}
    return out


def wall_targets(footprints, spacing=0.5):
    """Footprint walls as sample points with outward normals (ground to eave)."""
    P, N, ids = [], [], []
    segs = []
    for b in footprints:
        z0, z1 = b["ground_z"], b["eave_z"]
        if z1 - z0 < 2:
            continue
        for a, c, _, _, n2 in geom.wall_segments(b["rings"], z0, z1):
            L = np.linalg.norm(c - a)
            segs.append((a, c, z0, z1, n2, b["id"]))
            ns = max(2, int(L / spacing))
            nz = max(2, int((z1 - z0) / spacing))
            u = (np.arange(ns) + 0.5) / ns
            zz = z0 + (np.arange(nz) + 0.5) / nz * (z1 - z0)
            xy = a[None] + u[:, None] * (c - a)[None]
            grid = np.array([[x, y, z] for x, y in xy for z in zz])
            P.append(grid)
            N.append(np.tile([n2[0], n2[1], 0.0], (len(grid), 1)))
            ids += [b["id"]] * len(grid)
    if not P:
        return np.zeros((0, 3)), np.zeros((0, 3)), segs
    return np.vstack(P), np.vstack(N), segs


# ---------------------------------------------------------------------------
# Fitting
# ---------------------------------------------------------------------------

def gps_start(C, R_cw, gps_xyz, heading, threshold=10.0, seed=0):
    """Similarity COLMAP -> campus from camera centers + an upright-camera gravity hint."""
    n = len(C)
    # Scale guess from far-apart pairs.
    i, j = np.triu_indices(n, 1)
    dc = np.linalg.norm(C[i] - C[j], axis=1)
    dg = np.linalg.norm(gps_xyz[i, :2] - gps_xyz[j, :2], axis=1)
    far = dg > max(20.0, np.percentile(dg, 50))
    s0 = float(np.median(dg[far] / np.maximum(dc[far], 1e-12))) if far.any() else 1.0

    def ok(M):
        s = geom.sim_parts(M)[0]
        return s0 / 2 < s < s0 * 2

    M, inl = geom.umeyama_ransac(C, gps_xyz, threshold, iters=3000, seed=seed, sample_ok=ok)
    if M is None:
        return None
    # Refit with gravity pseudo-points: C + L*down_cam  ->  gps + (0, 0, -L).
    s = geom.sim_parts(M)[0]
    L = 10.0
    down = np.einsum("nij,j->ni", np.transpose(R_cw, (0, 2, 1)), np.array([0, 1.0, 0]))
    src = np.vstack([C[inl], C[inl] + down[inl] * (L / s)])
    dst = np.vstack([gps_xyz[inl], gps_xyz[inl] + np.array([0, 0, -L])])
    M2 = geom.umeyama(src, dst)
    d = np.linalg.norm(geom.sim_apply(M2, C) - gps_xyz, axis=1)
    inl2 = d < threshold
    src = np.vstack([C[inl2], C[inl2] + down[inl2] * (L / s)])
    dst = np.vstack([gps_xyz[inl2], gps_xyz[inl2] + np.array([0, 0, -L])])
    M2 = geom.umeyama(src, dst)
    info = {"scale_guess": s0, "inliers": int(inl2.sum()), "gps_used": int(n), "threshold_m": threshold}
    d = np.linalg.norm(geom.sim_apply(M2, C)[:, :2] - gps_xyz[:, :2], axis=1)
    info["gps_horizontal_rms_m"] = float(np.sqrt((d[inl2] ** 2).mean()))
    info["gps_horizontal_median_m"] = float(np.median(d))
    # Heading check: camera forward direction in campus vs GPS heading (0 = north, clockwise).
    Rm = geom.sim_parts(M2)[1]
    fwd = np.einsum("ij,nj->ni", Rm, np.einsum("nij,j->ni", np.transpose(R_cw, (0, 2, 1)), np.array([0, 0, 1.0])))
    yaw = np.degrees(np.arctan2(fwd[:, 0], fwd[:, 1])) % 360
    if heading is not None:
        h = np.asarray(heading, dtype=float)
        okh = np.isfinite(h)
        if okh.any():
            dd = np.abs((yaw[okh] - h[okh] + 180) % 360 - 180)
            info["heading_diff_median_deg"] = float(np.median(dd))
            info["heading_diff_p90_deg"] = float(np.percentile(dd, 90))
    tilt = np.degrees(np.arcsin(np.clip(fwd[:, 2], -1, 1)))
    info["camera_pitch_median_deg"] = float(np.median(tilt))
    return M2, info


def match_planes(X, targets, cutoff):
    """Nearest target point (with its normal) for each point; returns (Q, N, dist, mask)."""
    from scipy.spatial import cKDTree
    Tp, Tn, tree = targets
    if len(Tp) == 0:
        z = np.zeros((len(X), 3))
        return z, z, np.full(len(X), np.inf), np.zeros(len(X), dtype=bool)
    d, j = tree.query(X, distance_upper_bound=cutoff * 3 + 1)
    ok = np.isfinite(d)
    j = np.where(ok, j, 0)
    Q, N = Tp[j], Tn[j]
    pd = np.abs(((X - Q) * N).sum(1))
    # A match counts if the point is close to the plane and not far along it.
    m = ok & (pd < cutoff) & (d < cutoff * 3 + 1)
    return Q, N, np.where(ok, pd, np.inf), m


def icp(Pf, Pg, M0, T, schedule=(3.0, 2.0, 1.0, 0.6, 0.4, 0.3), loss=0.15, fix_scale=False,
        use=("lidar_facade", "footprint_wall", "ground"), scale_sigma=0.10, min_facade=20, cam_prior=None):
    """Point-to-plane ICP of the similarity on facade points Pf and ground points Pg.

    The scale gets a soft prior at the start scale (scale_sigma, relative). With fewer than
    min_facade facade matches the scale is held fixed (ground alone cannot fix it).
    Cost = mean over all candidate points of min(distance, cutoff)^2, so dropping points
    from the match never lowers the cost.
    cam_prior = (C_model, target_xyz, sigma_xy, sigma_z): soft pull of the camera centers toward
    their GPS position (x, y) and LiDAR ground + camera height (z). Walls say nothing about
    height and street photos see little ground, so without it the height can drift."""
    M = M0.copy()
    s_start = geom.sim_parts(M0)[0]
    cost = None
    for cutoff in schedule:
        for _ in range(3):
            P_all, Q_all, N_all, w_all = [], [], [], []
            Xf = geom.sim_apply(M, Pf) if len(Pf) else np.zeros((0, 3))
            Xg = geom.sim_apply(M, Pg) if len(Pg) else np.zeros((0, 3))
            parts = []
            n_cand = 0
            n_fac = 0
            for key, src, X in (("lidar_facade", Pf, Xf), ("footprint_wall", Pf, Xf), ("ground", Pg, Xg)):
                if key not in use or len(X) == 0 or T.get(key) is None:
                    continue
                Q, N, d, m = match_planes(X, T[key], cutoff)
                n_cand += len(X)
                if m.sum() < 5:
                    continue
                if key != "ground":
                    n_fac += int(m.sum())
                P_all.append(src[m]); Q_all.append(Q[m]); N_all.append(N[m])
                # Balance the data types: each type gets the same total weight.
                w_all.append(np.full(m.sum(), 1.0 / m.sum()))
                parts.append(d[m])
            if not P_all:
                return M, np.inf, 0
            if cam_prior is not None:
                Cm, Ct, sxy, sz = cam_prior
                lsc = max(loss, cutoff / 4)
                for axis, sig in ((0, sxy), (1, sxy), (2, sz)):
                    nrm = np.zeros((len(Cm), 3))
                    nrm[:, axis] = 1.0
                    P_all.append(Cm); Q_all.append(Ct); N_all.append(nrm)
                    # weight relative to one data type: (loss scale / sigma)^2
                    w_all.append(np.full(len(Cm), (lsc / sig) ** 2 / len(Cm)))
            P_, Q_, N_ = np.vstack(P_all), np.vstack(Q_all), np.vstack(N_all)
            w_ = np.concatenate(w_all)
            M = geom.refine_point_to_plane(P_, Q_, N_, M, loss_scale=max(loss, cutoff / 4), iters=8,
                                           fix_scale=fix_scale or n_fac < min_facade, weights=w_ * len(w_),
                                           scale_prior=(s_start, scale_sigma))
            r = np.concatenate(parts)
            n_match = len(r)
            cost = float((np.sum(np.minimum(r, cutoff) ** 2) + (n_cand - n_match) * cutoff ** 2) / max(n_cand, 1))
    return M, cost, n_match


def residual_table(X, T, cutoffs):
    """RMS / median / p95 of point-to-plane distances for matched points, per cutoff."""
    rows = {}
    if T is None or len(X) == 0 or len(T[0]) == 0:
        return {"n_points": int(len(X)), "n_targets": 0 if T is None else int(len(T[0]))}
    _, _, d, _ = match_planes(X, T, max(cutoffs))
    from scipy.spatial import cKDTree
    dn, _ = T[2].query(X)
    for c in cutoffs:
        m = (d < c) & (dn < c * 3 + 1)
        r = d[m]
        rows[f"within_{c:g}m"] = {
            "n": int(m.sum()), "share": round(float(m.mean()), 4),
            "rms_m": round(float(np.sqrt((r ** 2).mean())), 4) if len(r) else None,
            "median_m": round(float(np.median(r)), 4) if len(r) else None,
            "p95_m": round(float(np.percentile(r, 95)), 4) if len(r) else None}
    rows["n_points"] = int(len(X))
    rows["n_targets"] = int(len(T[0]))
    return rows


def classify_points(Xc, ground_fn, cams_c, k=15):
    """Label sparse points (already roughly in campus frame) as facade, ground or other."""
    from scipy.spatial import cKDTree
    n, pl, sp = geom.local_planes(Xc, k=k, radius=4.0)
    gz = ground_fn(Xc[:, 0], Xc[:, 1])
    hag = Xc[:, 2] - gz
    dcam, _ = cKDTree(cams_c[:, :2]).query(Xc[:, :2])
    lab = np.zeros(len(Xc), dtype=int)
    facade = (np.abs(n[:, 2]) < 0.3) & (pl > 0.3) & (hag > 1.0) & (hag < 60) & (sp < 0.5)
    ground = (np.abs(n[:, 2]) > 0.85) & (np.abs(hag) < 1.5) & (sp < 0.3) & (dcam < 40)
    lab[facade] = LABELS["facade"]
    lab[ground & ~facade] = LABELS["ground"]
    return lab, hag


def block_split(XY, holdout, block=10.0, seed=0):
    """True for points in held-out 10 m blocks (about `holdout` share of the blocks)."""
    key = np.floor(XY / block).astype(np.int64)
    uniq, inv = np.unique(key, axis=0, return_inverse=True)
    rng = np.random.default_rng(seed)
    held = rng.random(len(uniq)) < holdout
    return held[inv.ravel()]


def make_target(P, N):
    from scipy.spatial import cKDTree
    return (P, N, cKDTree(P)) if len(P) else (P, N, cKDTree(np.zeros((1, 3)) + 1e9))


def flag_unreliable(cams, max_offset):
    """Mark campus poses farther than max_offset m from the photo's GPS as unreliable
    (GPS is good to about 5-10 m; a bigger gap means the model is bent there).
    Returns counts. Later steps (sources.json, overlays) use reliable poses only."""
    n_ok = n_bad = 0
    for e in cams["images"]:
        c = e.get("campus")
        if not c:
            continue
        g = e.get("gps")
        off = float(np.hypot(c["center"][0] - g["X"], c["center"][1] - g["Y"])) if g else None
        c["gps_offset_m"] = round(off, 2) if off is not None else None
        c["reliable"] = off is None or off <= max_offset
        n_ok += c["reliable"]
        n_bad += not c["reliable"]
    return {"reliable": n_ok, "unreliable": n_bad, "rule": f"camera within {max_offset} m of its GPS position"}


def pooled_summary(units_info, scale_ok_pct=3.0):
    """Residuals pooled over all registered units (RMS combined as sqrt(sum n*rms^2 / sum n)).

    Only units whose LiDAR refinement ran and whose scale stayed within +-scale_ok_pct of the
    GPS scale are pooled under "accepted"; "all_refined" pools every refined unit."""
    out = {}
    for group in ("accepted", "all_refined"):
        g = {}
        for u in units_info:
            if "residuals" not in u or not u.get("refinement", {}).get("lidar_refined"):
                continue
            if u.get("split_into_segments"):
                continue                       # replaced by its segments
            if group == "accepted" and abs(u["scale"]["change_pct"]) > scale_ok_pct:
                continue
            for stage, parts in u["residuals"].items():
                for part, types in parts.items():
                    for typ, tab in types.items():
                        for cut, row in tab.items():
                            if not isinstance(row, dict) or not row.get("n"):
                                continue
                            acc = g.setdefault(stage, {}).setdefault(part, {}).setdefault(typ, {}).setdefault(
                                cut, {"n": 0, "sq": 0.0, "points": 0})
                            acc["n"] += row["n"]
                            acc["sq"] += row["n"] * row["rms_m"] ** 2
                        acc_pts = g.setdefault(stage, {}).setdefault(part, {}).setdefault(typ, {})
                        acc_pts.setdefault("_points", 0)
                        acc_pts["_points"] += tab.get("n_points", 0)
        for stage in g.values():
            for part in stage.values():
                for typ, cuts in part.items():
                    npts = cuts.pop("_points", 0)
                    for cut, acc in cuts.items():
                        cuts[cut] = {"n": acc["n"], "rms_m": round(float(np.sqrt(acc["sq"] / acc["n"])), 4),
                                     "share_of_points": round(acc["n"] / npts, 4) if npts else None}
                    cuts["n_points"] = npts
        out[group] = g
    used = [u for u in units_info if not u.get("split_into_segments")]
    refined = [u for u in used if u.get("refinement", {}).get("lidar_refined")]
    out["units"] = {"registered": sum(1 for u in used if "residuals" in u),
                    "lidar_refined": len(refined),
                    "scale_within_3pct": sum(1 for u in refined if abs(u["scale"]["change_pct"]) <= scale_ok_pct)}
    out["note"] = ("RMS counts only points within the stated cutoff of a target (share_of_points says how many); "
                   "a residual inside a small cutoff is not proof of a correct fit")
    return out


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def segment_point_ids(rec, subset):
    """3D points whose observations are mostly in the photo subset (one segment)."""
    out = []
    for pid in rec.point3D_ids():
        els = rec.points3D[pid].track.elements
        inside = sum(1 for e in els if e.image_id in subset)
        if inside * 2 > len(els):
            out.append(pid)
    return out


def register_component(k, rec, cams_json, root, args, ground, warnings, subset=None):
    import pycolmap  # noqa: F401
    names = {rec.image(i).name: i for i in rec.reg_image_ids() if subset is None or i in subset}
    byname = {e["name"]: e for e in cams_json["images"]}
    rows = [(n, i) for n, i in names.items() if byname.get(n, {}).get("gps")]
    info = {"component": k, "registered_photos": len(names), "photos_with_gps": len(rows)}
    if len(rows) < args.min_gps:
        info["skipped"] = f"only {len(rows)} photos with GPS (needs {args.min_gps})"
        return None, info
    C = np.array([np.asarray(rec.image(i).projection_center()) for _, i in rows])
    R_cw = np.array([rec.image(i).cam_from_world().matrix()[:, :3] for _, i in rows])
    gxy = np.array([[byname[n]["gps"]["X"], byname[n]["gps"]["Y"]] for n, _ in rows])
    span = float(np.ptp(gxy, axis=0).max())
    info["gps_span_m"] = round(span, 1)
    if span < 15:
        info["skipped"] = f"GPS positions span only {span:.1f} m"
        return None, info
    gz = ground(gxy[:, 0], gxy[:, 1])
    if np.isnan(gz).all():
        info["skipped"] = "no LiDAR ground under the cameras"
        return None, info
    gz = np.where(np.isnan(gz), np.nanmedian(gz), gz)
    gps_xyz = np.column_stack([gxy, gz + args.cam_height])
    heading = [byname[n]["gps"].get("heading_deg") for n, _ in rows]
    heading = [np.nan if h is None else h for h in heading]
    st = gps_start(C, R_cw, gps_xyz, heading, threshold=args.gps_threshold)
    if st is None:
        info["skipped"] = "GPS RANSAC found no consistent similarity"
        return None, info
    M_gps, ginfo = st
    info["gps_start"] = ginfo
    if ginfo.get("heading_diff_median_deg", 0) > 45:
        warnings.append(f"component {k}: camera forward vs GPS heading differ by "
                        f"{ginfo['heading_diff_median_deg']:.0f} deg (median); GPS start may be wrong")

    # Sparse points in the COLMAP frame and their rough campus positions.
    ids = list(rec.point3D_ids()) if subset is None else segment_point_ids(rec, subset)
    if len(ids) < 50:
        info["skipped"] = f"only {len(ids)} sparse points"
        return None, info
    P = np.array([rec.points3D[i].xyz for i in ids])
    tl = np.array([rec.points3D[i].track.length() for i in ids])
    err = np.array([rec.points3D[i].error for i in ids])
    good = (tl >= args.min_track) & (err < args.max_point_error)
    Xc = geom.sim_apply(M_gps, P)
    cams_c = geom.sim_apply(M_gps, np.array([np.asarray(rec.image(i).projection_center()) for i in names.values()]))
    from scipy.spatial import cKDTree
    dcam, _ = cKDTree(cams_c[:, :2]).query(Xc[:, :2])
    good &= dcam < args.corridor
    lab, _ = classify_points(Xc, ground, cams_c)
    lab[~good] = LABELS["other"]
    info["sparse_points"] = {"total": len(ids), "used_quality": int(good.sum()),
                             "facade": int((lab == 1).sum()), "ground": int((lab == 2).sum()),
                             "rules": f"track >= {args.min_track}, error < {args.max_point_error} px, "
                                      f"within {args.corridor} m of a camera; facade: neighborhood normal "
                                      f"|nz| < 0.3, planar, 1-60 m above ground; ground: |nz| > 0.85, within "
                                      f"1.5 m of LiDAR ground"}

    # Targets.
    t = time.time()
    LT = lidar_targets(root, cams_c[:, :2], args.corridor + 10, ground)
    if LT is None:
        info["skipped"] = "no LiDAR around the cameras"
        return None, info
    box = (*(cams_c[:, :2].min(0) - args.corridor), *(cams_c[:, :2].max(0) + args.corridor))
    fps = load_footprints(root, box)
    WP, WN, segs = wall_targets(fps)
    if len(WP):
        from scipy.spatial import cKDTree
        dw, _ = cKDTree(cams_c[:, :2]).query(WP[:, :2])
        WP, WN = WP[dw < args.corridor + 10], WN[dw < args.corridor + 10]
    info["targets"] = dict(LT["counts"], footprint_wall_samples=int(len(WP)), footprints=len(fps),
                           seconds=round(time.time() - t, 1))

    # Hold out 30 % of the area (10 m blocks) for the independent check.
    Pf_all, Pg_all = P[lab == 1], P[lab == 2]
    Xf0, Xg0 = Xc[lab == 1], Xc[lab == 2]
    hf = block_split(Xf0[:, :2], args.holdout, seed=1)
    hg = block_split(Xg0[:, :2], args.holdout, seed=1)
    tg = {}
    for key, (TP, TN) in (("lidar_facade", LT["lidar_facade"]), ("footprint_wall", (WP, WN)),
                          ("ground", LT["ground"])):
        h = block_split(TP[:, :2], args.holdout, seed=1) if len(TP) else np.zeros(0, dtype=bool)
        tg[key] = {"fit": make_target(TP[~h], TN[~h]), "held": make_target(TP[h], TN[h]),
                   "all": make_target(TP, TN)}
    use = {"lidar": ("lidar_facade", "ground"), "walls": ("footprint_wall", "ground"),
           "both": ("lidar_facade", "footprint_wall", "ground")}[args.fit_on]
    fitT = {kk: v["fit"] for kk, v in tg.items()}

    # Multi-start ICP around the GPS start.
    t = time.time()
    starts = []
    offs = [0.0] if args.starts <= 1 else [-args.start_shift, 0.0, args.start_shift]
    yaws = [0.0] if args.starts <= 1 else [-args.start_yaw, 0.0, args.start_yaw]
    c0 = cams_c.mean(0)
    for dy in yaws:
        for dx in offs:
            for dyy in offs:
                if len(starts) >= args.starts:
                    break
                Rz = geom.yaw_matrix(dy)
                U = np.eye(4)
                U[:3, :3] = Rz
                U[:3, 3] = c0 - Rz @ c0 + np.array([dx, dyy, 0])
                starts.append(((dy, dx, dyy), U @ M_gps))
    results = []
    for s_id, M0 in starts:
        M, cost, nm = icp(Pf_all[~hf], Pg_all[~hg], M0, fitT, fix_scale=args.fix_scale, use=use,
                          cam_prior=(C, gps_xyz, args.prior_sigma_xy, args.prior_sigma_z))
        results.append((cost if np.isfinite(cost) else 1e9, -nm, s_id, M))
    results.sort(key=lambda r: (r[0], r[1]))
    best_cost, _, best_start, M = results[0]
    costs = sorted(r[0] for r in results)
    info["refinement"] = {"method": "point-to-plane ICP of a 7-DoF similarity, Huber loss, match cutoff "
                                    "3 -> 0.3 m, data types weighted equally; soft camera prior: GPS x/y "
                                    f"(sigma {args.prior_sigma_xy} m) and height = LiDAR ground + "
                                    f"{args.cam_height} m (sigma {args.prior_sigma_z} m)",
                          "fit_on": list(use), "starts": len(starts), "best_start": {"yaw_deg": best_start[0],
                          "dx_m": best_start[1], "dy_m": best_start[2]}, "best_cost": best_cost,
                          "start_costs_sorted": [round(c, 5) for c in costs[:10]],
                          "starts_within_10pct_of_best": int(sum(c <= best_cost * 1.1 for c in costs)),
                          "lidar_refined": bool(best_cost < 1e8),
                          "seconds": round(time.time() - t, 1)}
    s_gps = geom.sim_parts(M_gps)[0]
    s_ref = geom.sim_parts(M)[0]
    dscale = s_ref / s_gps - 1
    info["scale"] = {"gps_start": s_gps, "refined": s_ref, "change_pct": round(100 * dscale, 3),
                     "check": "within +-3%" if abs(dscale) <= 0.03 else "OUTSIDE +-3%"}
    if abs(dscale) > 0.03:
        warnings.append(f"component {k}: refined scale differs from the GPS scale by {100 * dscale:.1f}%")
    dM = M @ np.linalg.inv(M_gps)
    info["change_from_gps_start"] = {"rotation_deg": round(geom.rotation_angle_deg(geom.sim_parts(dM)[1]), 3),
                                     "camera_shift_median_m": round(float(np.median(np.linalg.norm(
                                         geom.sim_apply(M, C) - geom.sim_apply(M_gps, C), axis=1))), 3)}

    # Residual tables: before (GPS start) / after, fit area / held-out area, per data type.
    cut = [0.5, 1.0, 2.0]
    res = {}
    for stage, MM in (("before", M_gps), ("after", M)):
        Xf, Xg = geom.sim_apply(MM, Pf_all), geom.sim_apply(MM, Pg_all)
        res[stage] = {}
        for part, mf, mg, tk in (("fit", ~hf, ~hg, "fit"), ("held_out", hf, hg, "held")):
            res[stage][part] = {
                "lidar_facade": residual_table(Xf[mf], tg["lidar_facade"][tk], cut),
                "footprint_wall": residual_table(Xf[mf], tg["footprint_wall"][tk], cut),
                "ground": residual_table(Xg[mg], tg["ground"][tk], cut)}
    info["residuals"] = res

    # Plausibility: camera heights and cameras inside footprints.
    allC = np.array([np.asarray(rec.image(i).projection_center()) for i in names.values()])
    Cc = geom.sim_apply(M, allC)
    hz = Cc[:, 2] - ground(Cc[:, 0], Cc[:, 1])
    okz = np.isfinite(hz)
    inside = geom.point_in_polygons(Cc[:, :2], [r for b in fps for r in b["rings"]]) if fps else np.zeros(len(Cc), bool)
    gd = np.linalg.norm(Cc[:, :2] - np.array([[byname[n]["gps"]["X"], byname[n]["gps"]["Y"]]
                                               if byname[n].get("gps") else [np.nan, np.nan] for n in names]), axis=1)
    info["plausibility"] = {
        "note": ("camera heights are NOT an independent check: the GPS start and the ICP camera prior both "
                 f"pull the height toward LiDAR ground + {args.cam_height} m (sigma {args.prior_sigma_z} m). "
                 "Cameras inside footprints is independent of the fit (footprints are only used as walls)."),
        "camera_height_above_lidar_ground_m": {
            "median": round(float(np.median(hz[okz])), 3) if okz.any() else None,
            "p5": round(float(np.percentile(hz[okz], 5)), 3) if okz.any() else None,
            "p95": round(float(np.percentile(hz[okz], 95)), 3) if okz.any() else None,
            "in_1_to_3m": int(((hz >= 1) & (hz <= 3)).sum()), "outside_1_to_3m": int(((hz < 1) | (hz > 3)).sum()),
            "no_ground": int((~okz).sum())},
        "cameras_inside_building_footprints": int(inside.sum()),
        "camera_to_gps_horizontal_m": {"median": round(float(np.nanmedian(gd)), 3),
                                       "p95": round(float(np.nanpercentile(gd, 95)), 3)}}
    if inside.any():
        warnings.append(f"component {k}: {int(inside.sum())} cameras lie inside building footprints")
    out = {"M": M, "M_gps": M_gps, "labels": lab, "ids": ids, "P": P, "names": names}
    return out, info


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--set", required=True)
    ap.add_argument("--version", default="v1", help="alignment version folder (default v1)")
    ap.add_argument("--cam-height", type=float, default=1.6, help="assumed camera height above ground for the GPS start")
    ap.add_argument("--gps-threshold", type=float, default=10.0, help="GPS RANSAC inlier distance (m)")
    ap.add_argument("--min-gps", type=int, default=5)
    ap.add_argument("--corridor", type=float, default=50.0, help="use points and LiDAR within N m of the cameras")
    ap.add_argument("--min-track", type=int, default=3)
    ap.add_argument("--max-point-error", type=float, default=2.0, help="px; noisier sparse points are ignored")
    ap.add_argument("--fit-on", default="both", choices=["both", "lidar", "walls"],
                    help="facade targets used in the fit: LiDAR facade points, footprint walls or both")
    ap.add_argument("--fix-scale", action="store_true", help="keep the GPS scale (do not refine it)")
    ap.add_argument("--starts", type=int, default=27, help="multi-start count (yaw x shift grid)")
    ap.add_argument("--start-yaw", type=float, default=3.0, help="yaw offset of the extra starts (deg)")
    ap.add_argument("--start-shift", type=float, default=3.0, help="shift of the extra starts (m)")
    ap.add_argument("--segment", type=int, default=20,
                    help="register a non-rigid component in segments of N consecutive photos (0 = never)")
    ap.add_argument("--segment-if", type=float, default=0.7,
                    help="split when fewer than this share of cameras fit the whole-component GPS start")
    ap.add_argument("--prior-sigma-xy", type=float, default=5.0, help="GPS sigma for the camera prior in ICP (m)")
    ap.add_argument("--prior-sigma-z", type=float, default=0.5,
                    help="camera height sigma (around LiDAR ground + --cam-height) in ICP (m)")
    ap.add_argument("--max-gps-offset", type=float, default=25.0,
                    help="campus poses farther than this from GPS are marked unreliable (m)")
    ap.add_argument("--holdout", type=float, default=0.30, help="share of 10 m blocks held out of the fit")
    ap.add_argument("--no-manifest", action="store_true")
    args = ap.parse_args(argv)

    import pycolmap
    from align import write_ply
    t0 = time.time()
    root = data_root()
    out = root / "derived" / "alignment" / args.set.strip("/") / args.version
    cj = out / "cameras.json"
    if not cj.exists():
        fail(f"{cj} missing; run align.py --set {args.set} first")
    cams = load_json(cj)
    ground = Ground(root)
    warnings = []
    comps_info, regs = [], {}
    unit_of = {}                                   # photo name -> registered unit key
    for k in range(len(cams["components"])):
        rec = pycolmap.Reconstruction(str(out / "sparse" / str(k)))
        log(f"component {k}: {rec.num_reg_images()} photos, {rec.num_points3D()} points")
        r, info = register_component(k, rec, cams, root, args, ground, warnings)
        units = [(str(k), r, info, None)]
        gs = info.get("gps_start") or {}
        share = gs.get("inliers", 0) / max(gs.get("gps_used", 1), 1)
        n_img = rec.num_reg_images()
        if args.segment > 0 and n_img >= 2 * args.segment and (r is None or share < args.segment_if):
            # The model is not one rigid shape in the GPS frame (scale drift along a long street
            # sequence): register consecutive segments of photos separately.
            why = (f"GPS start fits only {gs.get('inliers')}/{gs.get('gps_used')} cameras within "
                   f"{args.gps_threshold} m" if gs else info.get("skipped"))
            warnings.append(f"component {k}: not rigid against GPS ({why}); registered in segments of "
                            f"{args.segment} photos")
            log(f"  {why}: splitting into segments of {args.segment} photos")
            info["split_into_segments"] = {"why": why, "segment_photos": args.segment}
            comps_info.append(info)
            order = sorted(rec.reg_image_ids(), key=lambda i: rec.image(i).name)
            nseg = max(1, round(len(order) / args.segment))
            bounds = np.linspace(0, len(order), nseg + 1).astype(int)
            units = []
            for si in range(nseg):
                sub = set(order[bounds[si]:bounds[si + 1]])
                key = f"{k}.{si}"
                rs, inf = register_component(key, rec, cams, root, args, ground, warnings, subset=sub)
                inf["segment_of_component"] = k
                inf["first_photo"] = rec.image(order[bounds[si]]).name
                inf["last_photo"] = rec.image(order[bounds[si + 1] - 1]).name
                units.append((key, rs, inf, sub))
        for key, r, info, sub in units:
            comps_info.append(info)
            if r is None:
                log(f"  {key} not registered: {info.get('skipped')}")
                continue
            regs[key] = (r, rec)
            for n in r["names"]:
                unit_of[n] = key
            a = info["residuals"]["after"]
            log(f"  {key}: scale {info['scale']['refined']:.4f} ({info['scale']['change_pct']:+.2f}% vs GPS), "
                f"facade(LiDAR) fit {a['fit']['lidar_facade'].get('within_0.5m', {}).get('rms_m')} m, "
                f"walls fit {a['fit']['footprint_wall'].get('within_0.5m', {}).get('rms_m')} m, "
                f"ground fit {a['fit']['ground'].get('within_0.5m', {}).get('rms_m')} m")
    if not regs:
        save_json(out / "registration.json", {"set": args.set, "registered": False, "components": comps_info,
                                              "warnings": warnings})
        fail("no component could be registered to the campus frame (see registration.json)")

    # Update cameras.json with campus poses.
    for e in cams["images"]:
        e["campus"] = None
        e["registration_unit"] = None
        k = unit_of.get(e["name"])
        if k is None:
            continue
        e["registration_unit"] = k
        r, rec = regs[k]
        M = r["M"]
        s, Rm, tm = geom.sim_parts(M)
        R_cw = np.array(e["colmap"]["R_cam_from_world"])
        C = np.array(e["colmap"]["center"])
        Cc = geom.sim_apply(M, C[None])[0]
        R_cc = R_cw @ Rm.T                     # cam_from_campus rotation
        fwd = R_cc.T @ np.array([0, 0, 1.0])
        e["campus"] = {"center": Cc.round(4).tolist(), "R_cam_from_campus": R_cc.round(9).tolist(),
                       "t_cam_from_campus": (-R_cc @ Cc).round(6).tolist(),
                       "heading_deg": round(float(np.degrees(np.arctan2(fwd[0], fwd[1])) % 360), 2),
                       "pitch_deg": round(float(np.degrees(np.arcsin(np.clip(fwd[2], -1, 1)))), 2),
                       "height_above_ground_m": round(float(Cc[2] - ground(Cc[0:1], Cc[1:2])[0]), 3)
                       if np.isfinite(ground(Cc[0:1], Cc[1:2])[0]) else None}
    cams["frames"]["campus"] = "registered: campus meters (config/frame.json), Z up; see registration.json"
    reliability = flag_unreliable(cams, args.max_gps_offset)
    save_json(cj, cams)

    # points.ply in the campus frame (all registered components).
    xyz, rgb, comp, lab, unit = [], [], [], [], []
    unit_keys = list(regs)
    for ui, (k, (r, rec)) in enumerate(regs.items()):
        X = geom.sim_apply(r["M"], r["P"])
        xyz.append(X)
        rgb.append(np.array([rec.points3D[i].color for i in r["ids"]]))
        comp.append(np.full(len(X), int(str(k).split(".")[0])))
        unit.append(np.full(len(X), ui))
        lab.append(r["labels"])
    write_ply(out / "points.ply", np.vstack(xyz), np.vstack(rgb),
              {"component": np.concatenate(comp), "unit": np.concatenate(unit), "label": np.concatenate(lab)})

    reg = {"version": 1, "set": args.set, "alignment_version": args.version,
           "made": datetime.datetime.now().isoformat(timespec="seconds"),
           "script": "Tools/VanderbiltCampus/pipeline/register_lidar.py", "argv": sys.argv[1:] if argv is None else argv,
           "registered": True, "frame": "campus meters (config/frame.json)",
           "transforms": {str(k): {"photos": len(r["names"]),"colmap_to_campus_4x4": r["M"].round(10).tolist(),
                                   "gps_start_4x4": r["M_gps"].round(10).tolist(),
                                   "scale": geom.sim_parts(r["M"])[0]} for k, (r, _) in regs.items()},
           "units": unit_keys,
           "summary": pooled_summary(comps_info),
           "camera_reliability": reliability,
           "components": comps_info, "warnings": warnings,
           "labels_in_points_ply": LABELS,
           "data_types": {"lidar_facade": "LiDAR facade points: class 1/6 LiDAR points whose 1.5 m neighborhood is a "
                                          "vertical plane (aerial LiDAR: sparse on walls)",
                          "footprint_wall": "footprint walls: derived/buildings/v1 footprints extruded from ground "
                                            "to eave height (not measured wall points; footprint accuracy ~0.3 m)",
                          "ground": "LiDAR class 2 points, 0.5 m subsample, local normal"},
           "targets": {"facade_rms_m": 0.1}, "runtime_s": round(time.time() - t0, 1),
           "options": vars(args)}
    save_json(out / "registration.json", reg)
    rp = out / "report.json"
    if rp.exists():
        rep = load_json(rp)
        rep["registration"] = {"file": "registration.json", "components_registered": sorted(regs),
                               "warnings": warnings}
        save_json(rp, rep)
    if not args.no_manifest:
        import manifest
        db = manifest.connect(root)
        lic = (cams["images"][0].get("license") or "") + " (derived)"
        for f in (out / "registration.json", cj, out / "points.ply", rp):
            if f.exists():
                manifest.add_file(f, source=f"pipeline/register_lidar.py --set {args.set}", license=lic, stage=3,
                                  root=root, db=db)
        db.commit()
        db.close()
    log(f"done in {time.time() - t0:.0f} s; components registered: {sorted(regs)}; warnings: {len(warnings)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
