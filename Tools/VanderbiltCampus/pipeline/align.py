"""Stage 3 alignment: find where every photo of a capture set was taken (COLMAP on CPU).

    python align.py --set kartaview-2026-11-19
    python align.py --set fallback-2026-11-19/zenodo-16576623 --mapper both
    python align.py --set kartaview-2026-11-19 --chunk 200 --overlap 40 --out chunk2

Input: the intake output derived/capture/<set>/photos.json and its blurred/
images and masks/ (white = usable; people and cars are black and get no
features). raw/ is never read.

Steps (each is logged with its options in report.json):
  1. Feature extraction: SIFT on CPU, images shrunk to --max-size pixels on the long
     side, masks applied. One shared camera per sequence folder and image size. The
     focal length starts from EXIF (35 mm equivalent) when present, otherwise from a
     typical phone main camera (26 mm equivalent, --default-focal-35mm); bundle
     adjustment refines it.
  2. Matching: pairs are chosen by --matcher:
       exhaustive  every pair (small sets)
       sequential  each photo with the next --seq-overlap photos of its sequence
       spatial     each photo with GPS neighbors within --gps-radius meters (any sequence)
       hybrid      sequential + spatial (default for sets with GPS)
       vocab       COLMAP vocabulary-tree retrieval (needs --vocab-tree FILE)
     COLMAP then matches the pairs and keeps geometrically verified matches.
  3. Mapping: COLMAP incremental mapping (default), the global mapper (--mapper global),
     or both, keeping whichever registers more images (then lower error).
     --chunk N --overlap M splits the photos (in sequence order) into overlapping
     chunks, maps each chunk, joins the chunks with a similarity transform fitted on
     the shared photos' camera centers, re-triangulates and bundle-adjusts the result.
  4. Fallbacks when fewer than --target-registered of the photos register: (a) longer
     sequential overlap and a larger GPS radius, (b) more features with a lower SIFT
     threshold. The best attempt is kept; all attempts are in the report.

Output in derived/alignment/<set>/<out>/ (default out v1):
  sparse/<k>/     COLMAP model of component k (binary and text side by side)
  cameras.json    per photo: intrinsics, COLMAP pose, derived + raw path (campus pose
                  is added by register_lidar.py)
  points.ply      sparse points of component 0 in the COLMAP frame (register_lidar.py
                  rewrites it in the campus frame)
  report.json     images given / registered, reprojection error, track length,
                  per-sequence numbers, runtimes, every option used
  work/           COLMAP database, masks renamed for COLMAP, pair lists, mapper output

Exit code 1 when nothing registers or the inputs are missing.
"""
import argparse
import datetime
import json
import shutil
import sys
import time
from collections import defaultdict
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import align_geom as geom  # noqa: E402
from common import data_root, fail, load_json, local_config, save_json  # noqa: E402

DB_MANIFEST_LIMIT = 500 * 1024 * 1024     # COLMAP databases above this stay out of the manifest


def log(msg):
    print(time.strftime("%H:%M:%S ") + msg, flush=True)


# ---------------------------------------------------------------------------
# Inputs
# ---------------------------------------------------------------------------

def set_slug(set_name):
    return set_name.strip("/").replace("\\", "/")


def find_photos_json(root, set_name):
    p = root / "derived" / "capture" / set_name / "photos.json"
    if not p.exists():
        fail(f"{p} not found (run intake.py for this set first)")
    return p


def load_photos(root, set_name, images_dir=None, masks_dir=None, skip_flagged=False):
    """The set's photos as plain dicts. Image names are relative to one image root."""
    pj = find_photos_json(root, set_name)
    data = load_json(pj)
    photos = []
    for p in data["photos"]:
        if p.get("kind") != "photo":
            continue
        pr = p.get("privacy") or {}
        if not pr.get("blurred"):
            continue
        if skip_flagged and (p.get("quality") or {}).get("flags"):
            continue
        blurred = root / pr["blurred"]
        mask = root / pr["mask"] if pr.get("mask") else None
        if not blurred.exists():
            continue
        photos.append({"src": p, "blurred": blurred, "mask": mask})
    if not photos:
        fail(f"no blurred photos listed in {pj}")
    if images_dir:
        image_root = Path(images_dir)
    else:
        import os
        image_root = Path(os.path.commonpath([str(q["blurred"].parent) for q in photos]))
    for q in photos:
        q["name"] = q["blurred"].resolve().relative_to(image_root.resolve()).as_posix()
        if masks_dir:
            m = Path(masks_dir) / (Path(q["name"]).with_suffix(".png"))
            q["mask"] = m if m.exists() else None
        src = q["src"]
        prov = src.get("provider") or {}
        q["sequence"] = prov.get("sequence") or (str(Path(q["name"]).parent) if "/" in q["name"] else "all")
        c = src.get("campus") or {}
        q["gps"] = None
        if c.get("X") is not None and c.get("Y") is not None:
            q["gps"] = {"X": c["X"], "Y": c["Y"], "Z": c.get("Z"),
                        "heading_deg": c.get("heading_deg"),
                        "accuracy_m": (src.get("gps") or {}).get("accuracy_m")}
        q["flags"] = (src.get("quality") or {}).get("flags") or []
    photos.sort(key=lambda q: (q["sequence"], q["name"]))
    return data, image_root, photos


def image_size(path):
    from PIL import Image
    with Image.open(path) as im:
        return im.size


def focal_prior(q, w, h, default_35mm):
    """Focal length in pixels from the 35 mm equivalent (36 mm frame width on the long side)."""
    cam = q["src"].get("camera") or {}
    f35 = cam.get("focal_35mm")
    if f35:
        return float(f35) / 36.0 * max(w, h), f"EXIF focal_35mm {f35:.1f}"
    return default_35mm / 36.0 * max(w, h), f"assumed {default_35mm} mm equivalent (no EXIF focal)"


# ---------------------------------------------------------------------------
# Pair lists
# ---------------------------------------------------------------------------

def sequential_pairs(photos, overlap):
    pairs = set()
    by = defaultdict(list)
    for i, q in enumerate(photos):
        by[q["sequence"]].append(i)
    for idx in by.values():
        for a in range(len(idx)):
            for b in range(a + 1, min(len(idx), a + 1 + overlap)):
                pairs.add((idx[a], idx[b]))
    return pairs


def spatial_pairs(photos, radius, max_neighbors, max_heading_diff=None):
    from scipy.spatial import cKDTree
    pts, ids = [], []
    for i, q in enumerate(photos):
        if q["gps"]:
            pts.append([q["gps"]["X"], q["gps"]["Y"]])
            ids.append(i)
    pairs = set()
    if len(pts) < 2:
        return pairs
    pts = np.array(pts)
    tree = cKDTree(pts)
    k = min(max_neighbors + 1, len(pts))
    d, nb = tree.query(pts, k, distance_upper_bound=radius)
    for a in range(len(pts)):
        for dist, b in zip(d[a], nb[a]):
            if not np.isfinite(dist) or b == a or b >= len(pts):
                continue
            ia, ib = ids[a], ids[b]
            if max_heading_diff is not None:
                ha, hb = photos[ia]["gps"]["heading_deg"], photos[ib]["gps"]["heading_deg"]
                if ha is not None and hb is not None:
                    dh = abs((ha - hb + 180) % 360 - 180)
                    if dh > max_heading_diff:
                        continue
            pairs.add((min(ia, ib), max(ia, ib)))
    return pairs


def write_pairs(path, photos, pairs):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        for a, b in sorted(pairs):
            f.write(f"{photos[a]['name']} {photos[b]['name']}\n")


# ---------------------------------------------------------------------------
# COLMAP steps
# ---------------------------------------------------------------------------

def prepare_masks(photos, mask_dir):
    """COLMAP looks for masks at <mask_path>/<image name>.png; copy ours there."""
    n = 0
    for q in photos:
        if not q["mask"] or not Path(q["mask"]).exists():
            continue
        dst = mask_dir / (q["name"] + ".png")
        dst.parent.mkdir(parents=True, exist_ok=True)
        if not dst.exists():
            shutil.copyfile(q["mask"], dst)
        n += 1
    return n


def extract(pycolmap, db_path, image_root, photos, mask_dir, args, steps, max_features, peak):
    """SIFT per camera group (sequence folder + image size), one shared camera each."""
    groups = defaultdict(list)
    for q in photos:
        w, h = image_size(q["blurred"])
        q["size"] = (w, h)
        groups[(q["sequence"], w, h)].append(q)
    cams = []
    for (seq, w, h), qs in sorted(groups.items()):
        f, why = focal_prior(qs[0], w, h, args.default_focal_35mm)
        reader = pycolmap.ImageReaderOptions()
        reader.camera_model = args.camera_model
        reader.mask_path = str(mask_dir)
        if args.camera_model == "SIMPLE_RADIAL":
            reader.camera_params = f"{f:.3f},{w / 2:.3f},{h / 2:.3f},0"
        elif args.camera_model == "OPENCV":
            reader.camera_params = f"{f:.3f},{f:.3f},{w / 2:.3f},{h / 2:.3f},0,0,0,0"
        ext = pycolmap.FeatureExtractionOptions()
        ext.max_image_size = args.max_size
        ext.num_threads = args.threads
        ext.use_gpu = False
        ext.sift.max_num_features = max_features
        ext.sift.peak_threshold = peak
        t = time.time()
        pycolmap.extract_features(db_path, image_root, image_names=[q["name"] for q in qs],
                                  camera_mode=pycolmap.CameraMode.SINGLE, reader_options=reader,
                                  extraction_options=ext, device=pycolmap.Device.cpu)
        dt = time.time() - t
        log(f"  features: sequence {seq} {w}x{h}: {len(qs)} images in {dt:.0f} s (focal prior {f:.0f} px, {why})")
        steps.append({"step": "extract_features", "sequence": seq, "images": len(qs), "size": [w, h],
                      "focal_prior_px": round(f, 1), "focal_prior_from": why, "seconds": round(dt, 1),
                      "reader_options": str(reader), "extraction_options": str(ext)})
        cams.append({"sequence": seq, "size": [w, h], "focal_prior_px": round(f, 1), "from": why})
    return cams


def reset_camera_priors(pycolmap, db_path, photos, args):
    """With reused features: set each camera's starting focal again from EXIF or
    --default-focal-35mm (features do not depend on it, so no re-extraction)."""
    db = pycolmap.Database.open(db_path)
    by_name = {q["name"]: q for q in photos}
    done, info = set(), []
    try:
        for im in db.read_all_images():
            if im.camera_id in done or im.name not in by_name:
                continue
            cam = db.read_camera(im.camera_id)
            f, why = focal_prior(by_name[im.name], cam.width, cam.height, args.default_focal_35mm)
            p = list(cam.params)
            p[0] = f
            if cam.model_name == "OPENCV":
                p[1] = f
            for i in cam.extra_params_idxs():
                p[i] = 0.0
            cam.params = p
            db.update_camera(cam)
            done.add(im.camera_id)
            info.append({"camera_id": int(im.camera_id), "focal_prior_px": round(f, 1), "from": why})
    finally:
        db.close()
    log(f"  camera priors reset: {info}")
    return {"step": "reset_camera_priors", "cameras": info}


def write_gps_priors(pycolmap, db_path, photos, root, sigma_xy, cam_height=1.6):
    """Store each photo's GPS position (campus frame; height = LiDAR ground + cam_height) as a
    COLMAP pose prior, so the incremental mapper can use it (--gps-prior)."""
    from register_lidar import Ground
    ground = Ground(root)
    by_name = {q["name"]: q for q in photos}
    db = pycolmap.Database.open(db_path)
    n = 0
    try:
        db.clear_pose_priors()
        for im in db.read_all_images():
            q = by_name.get(im.name)
            if not q or not q["gps"]:
                continue
            X, Y = q["gps"]["X"], q["gps"]["Y"]
            z = float(ground(np.array([X]), np.array([Y]))[0])
            if not np.isfinite(z):
                continue
            pr = pycolmap.PosePrior()
            pr.corr_data_id = im.data_id
            pr.position = np.array([X, Y, z + cam_height])
            pr.position_covariance = np.diag([sigma_xy ** 2, sigma_xy ** 2, 1.0])
            pr.coordinate_system = pycolmap.PosePriorCoordinateSystem.CARTESIAN
            db.write_pose_prior(pr)
            n += 1
    finally:
        db.close()
    log(f"  GPS pose priors written: {n} (sigma {sigma_xy} m horizontal, 1 m vertical)")
    return {"step": "gps_pose_priors", "written": n, "sigma_xy_m": sigma_xy, "z": f"LiDAR DTM + {cam_height} m"}


def match(pycolmap, db_path, work, photos, matcher, seq_overlap, gps_radius, args, steps):
    mopt = pycolmap.FeatureMatchingOptions()
    mopt.num_threads = args.threads
    mopt.use_gpu = False
    mopt.guided_matching = args.guided
    vopt = pycolmap.TwoViewGeometryOptions()
    t = time.time()
    info = {"step": "match", "matcher": matcher}
    if matcher == "exhaustive":
        pycolmap.match_exhaustive(db_path, matching_options=mopt, verification_options=vopt,
                                  device=pycolmap.Device.cpu)
        n = len(photos)
        info["pairs"] = n * (n - 1) // 2
    elif matcher == "vocab":
        if not args.vocab_tree or not Path(args.vocab_tree).exists():
            fail("--matcher vocab needs --vocab-tree FILE (a COLMAP vocabulary tree; none is installed)")
        popt = pycolmap.VocabTreePairingOptions()
        popt.vocab_tree_path = str(args.vocab_tree)
        pycolmap.match_vocabtree(db_path, matching_options=mopt, pairing_options=popt,
                                 verification_options=vopt, device=pycolmap.Device.cpu)
    else:
        pairs = set()
        if matcher in ("sequential", "hybrid"):
            sp = sequential_pairs(photos, seq_overlap)
            info["sequential_pairs"] = len(sp)
            pairs |= sp
        if matcher in ("spatial", "hybrid"):
            gp = spatial_pairs(photos, gps_radius, args.gps_neighbors)
            info["spatial_pairs"] = len(gp)
            pairs |= gp
        if not pairs:
            fail(f"matcher {matcher} made no pairs (no GPS? try --matcher exhaustive or sequential)")
        pf = work / f"pairs_{matcher}_o{seq_overlap}_r{int(gps_radius)}.txt"
        write_pairs(pf, photos, pairs)
        info["pairs"] = len(pairs)
        info["pair_file"] = pf.name
        popt = pycolmap.ImportedPairingOptions()
        popt.match_list_path = str(pf)
        pycolmap.match_image_pairs(db_path, matching_options=mopt, pairing_options=popt,
                                   verification_options=vopt, device=pycolmap.Device.cpu)
    info.update({"seq_overlap": seq_overlap, "gps_radius_m": gps_radius, "seconds": round(time.time() - t, 1),
                 "matching_options": str(mopt), "verification_options": str(vopt)})
    db = pycolmap.Database.open(db_path) if hasattr(pycolmap.Database, "open") else pycolmap.Database(db_path)
    try:
        info["verified_pairs"] = int(db.num_verified_image_pairs())
    except Exception:
        pass
    db.close()
    log(f"  matching ({matcher}): {info.get('pairs', '?')} pairs, {info.get('verified_pairs', '?')} verified, "
        f"{info['seconds']:.0f} s")
    steps.append(info)


def map_incremental(pycolmap, db_path, image_root, out, args, image_names=None):
    opt = pycolmap.IncrementalPipelineOptions()
    opt.num_threads = args.threads
    opt.min_model_size = args.min_model_size
    opt.multiple_models = True
    if args.init_min_tri_angle is not None:
        opt.mapper.init_min_tri_angle = args.init_min_tri_angle
    if args.gps_prior:
        opt.use_prior_position = True
        opt.use_robust_loss_on_prior_position = True
    if args.fix_intrinsics:
        opt.ba_refine_focal_length = False
        opt.ba_refine_extra_params = False
        opt.mapper.abs_pose_refine_focal_length = False
        opt.mapper.abs_pose_refine_extra_params = False
    if image_names:
        opt.image_names = list(image_names)
    out.mkdir(parents=True, exist_ok=True)
    recs = pycolmap.incremental_mapping(db_path, image_root, out, opt)
    return recs, str(opt)


def map_global(pycolmap, db_path, image_root, out, args, image_names=None):
    opt = pycolmap.GlobalPipelineOptions()
    opt.num_threads = args.threads
    opt.min_model_size = args.min_model_size
    opt.mapper.num_threads = args.threads
    opt.mapper.global_positioning.use_gpu = False
    opt.mapper.bundle_adjustment.ceres.use_gpu = False
    if args.fix_intrinsics:
        opt.mapper.bundle_adjustment.refine_focal_length = False
        opt.mapper.bundle_adjustment.refine_extra_params = False
    if image_names:
        opt.image_names = list(image_names)
    out.mkdir(parents=True, exist_ok=True)
    recs = pycolmap.global_mapping(db_path, image_root, out, opt)
    return recs, str(opt)


# ---------------------------------------------------------------------------
# Model statistics
# ---------------------------------------------------------------------------

def observation_errors(rec):
    """Reprojection error (px) of every observation of every 3D point."""
    errs = []
    pts = rec.points3D
    for iid in rec.reg_image_ids():
        im = rec.image(iid)
        cam = rec.camera(im.camera_id)
        xy, xyz = [], []
        for p2 in im.points2D:
            if p2.has_point3D():
                xy.append(p2.xy)
                xyz.append(pts[p2.point3D_id].xyz)
        if not xy:
            continue
        xyz = np.array(xyz)
        T = im.cam_from_world().matrix()
        Xc = xyz @ T[:, :3].T + T[:, 3]
        ok = Xc[:, 2] > 1e-9
        proj = np.array([cam.img_from_cam(x) for x in Xc[ok]]) if ok.any() else np.zeros((0, 2))
        if len(proj) and proj.ndim == 2:
            errs.append(np.linalg.norm(proj[:, :2] - np.array(xy)[ok], axis=1))
    return np.concatenate(errs) if errs else np.zeros(0)


def model_stats(rec):
    e = observation_errors(rec)
    return {"registered": int(rec.num_reg_images()), "points": int(rec.num_points3D()),
            "observations": int(len(e)),
            "mean_reproj_px": round(float(e.mean()), 4) if len(e) else None,
            "median_reproj_px": round(float(np.median(e)), 4) if len(e) else None,
            "p95_reproj_px": round(float(np.percentile(e, 95)), 4) if len(e) else None,
            "colmap_mean_reproj_px": round(float(rec.compute_mean_reprojection_error()), 4),
            "mean_track_length": round(float(rec.compute_mean_track_length()), 3)}


def reg_names(rec):
    return {rec.image(i).name for i in rec.reg_image_ids()}


def summarize(recs):
    """Sort components by size; union of registered names."""
    items = sorted(recs.values(), key=lambda r: -r.num_reg_images())
    names = set()
    for r in items:
        names |= reg_names(r)
    return items, names


def score(items, names):
    """Higher is better: photos in the largest model first, then all registered photos,
    then lower mean error of the largest model."""
    if not items:
        return (0, 0, 0.0)
    err = items[0].compute_mean_reprojection_error()
    return (items[0].num_reg_images(), len(names), -err)


def refine_model(pycolmap, rec, db_path, image_root, out, args):
    """Optional clean-up of one model (--refine-px): re-triangulate every track from the
    database with the fixed poses, bundle-adjust (Cauchy loss), drop observations whose
    reprojection error is above --refine-px (and points seen at < 1.5 deg), adjust again.
    Returns (model, info). COLMAP's own mappers filter at 4 px."""
    before = model_stats(rec)
    out.mkdir(parents=True, exist_ok=True)
    opt = pycolmap.IncrementalPipelineOptions()
    opt.num_threads = args.threads
    r = pycolmap.triangulate_points(rec, db_path, image_root, out, clear_points=True, options=opt,
                                    refine_intrinsics=False)
    ba = pycolmap.BundleAdjustmentOptions()
    ba.print_summary = False
    ba.refine_focal_length = not args.fix_intrinsics
    ba.refine_extra_params = not args.fix_intrinsics
    ba.ceres.loss_function_type = pycolmap.LossFunctionType.CAUCHY
    ba.ceres.solver_options.num_threads = args.threads
    filtered = 0
    for _ in range(2):
        pycolmap.bundle_adjustment(r, ba)
        filtered += pycolmap.ObservationManager(r).filter_all_points3D(args.refine_px, 1.5)
    pycolmap.bundle_adjustment(r, ba)
    after = model_stats(r)
    info = {"before": before, "after": after, "filtered_observations": int(filtered),
            "rule": f"re-triangulate, BA (Cauchy), drop observations > {args.refine_px} px or < 1.5 deg, x2"}
    if after["registered"] < before["registered"]:
        info["note"] = "photos lost their last points in the filter"
    return r, info


# ---------------------------------------------------------------------------
# Chunk + merge
# ---------------------------------------------------------------------------

def make_chunks(photos, size, overlap):
    """Overlapping windows over the photos in sequence order."""
    n = len(photos)
    if size <= 0 or size >= n:
        return [list(range(n))]
    step = max(1, size - overlap)
    chunks, s = [], 0
    while True:
        e = min(n, s + size)
        chunks.append(list(range(s, e)))
        if e >= n:
            break
        s += step
    return chunks


def centers_by_name(rec):
    return {rec.image(i).name: np.asarray(rec.image(i).projection_center()) for i in rec.reg_image_ids()}


def chunk_transform(base_centers, chunk_centers, threshold_ratio=0.05):
    """Similarity mapping chunk coordinates into base coordinates from shared camera centers.

    Returns (4x4, shared count, inlier count, rms) or None when fewer than 3 photos are shared."""
    common = sorted(set(base_centers) & set(chunk_centers))
    if len(common) < 3:
        return None
    src = np.array([chunk_centers[n] for n in common])
    dst = np.array([base_centers[n] for n in common])
    extent = max(np.ptp(dst, axis=0).max(), 1e-6)
    M, inl = geom.umeyama_ransac(src, dst, threshold=threshold_ratio * extent, iters=500)
    if M is None:
        return None
    d = np.linalg.norm(geom.sim_apply(M, src[inl]) - dst[inl], axis=1)
    return M, len(common), int(inl.sum()), float(np.sqrt((d ** 2).mean()))


def merge_chunks(pycolmap, chunk_recs, db_path, image_root, out, args, steps):
    """Join chunk models (each its largest component) into one model.

    Chunks are added in order; each is brought into the frame of what is merged so
    far by a similarity on shared camera centers. Shared photos keep the earlier pose.
    Then all 3D points are re-triangulated from the database and bundle-adjusted."""
    base = chunk_recs[0]
    merged = pycolmap.Reconstruction(base)
    joins = []
    for k, rec in enumerate(chunk_recs[1:], start=1):
        fit = chunk_transform(centers_by_name(merged), centers_by_name(rec))
        if fit is None:
            joins.append({"chunk": k, "joined": False, "why": "fewer than 3 shared registered photos"})
            log(f"  chunk {k}: not joined (fewer than 3 shared photos)")
            continue
        M, shared, inliers, rms = fit
        s, R, t = geom.sim_parts(M)
        rec = pycolmap.Reconstruction(rec)
        rec.transform(pycolmap.Sim3d(s, pycolmap.Rotation3d(R), t))
        have = reg_names(merged)
        added = 0
        for iid in rec.reg_image_ids():
            im = rec.image(iid)
            if im.name in have:
                continue
            if merged.exists_image(iid):
                # The photo is in the merged model but not registered there: give its frame the pose.
                fr = merged.frame(merged.image(iid).frame_id)
                fr.set_cam_from_world(im.camera_id, im.cam_from_world())
                merged.register_frame(fr.frame_id)
                added += 1
                continue
            if not merged.exists_camera(im.camera_id):
                merged.add_camera_with_trivial_rig(rec.camera(im.camera_id))
            kps = np.array([p.xy for p in im.points2D]).reshape(-1, 2)
            new = pycolmap.Image(name=im.name, keypoints=kps, camera_id=im.camera_id, image_id=im.image_id)
            merged.add_image_with_trivial_frame(new, im.cam_from_world())
            added += 1
        joins.append({"chunk": k, "joined": True, "shared_photos": shared, "inliers": inliers,
                      "center_rms_model_units": round(rms, 5), "scale": round(s, 6), "added_photos": added})
        log(f"  chunk {k}: joined on {inliers}/{shared} shared photos (rms {rms:.4f}), added {added}")
    t = time.time()
    tri_dir = out / "merged_triangulated"
    tri_dir.mkdir(parents=True, exist_ok=True)
    opt = pycolmap.IncrementalPipelineOptions()
    opt.num_threads = args.threads
    merged = pycolmap.triangulate_points(merged, db_path, image_root, tri_dir, clear_points=True, options=opt)
    # Drop badly triangulated points before adjusting (a few wild points can wreck the solve).
    pycolmap.ObservationManager(merged).filter_all_points3D(4.0, 1.5)
    ba = pycolmap.BundleAdjustmentOptions()
    ba.print_summary = False
    fix = bool(getattr(args, "fix_intrinsics", False))
    ba.refine_focal_length = not fix
    ba.refine_extra_params = not fix
    ba.ceres.loss_function_type = pycolmap.LossFunctionType.CAUCHY
    ba.ceres.solver_options.num_threads = args.threads
    backup = pycolmap.Reconstruction(merged)
    pycolmap.bundle_adjustment(merged, ba)
    err = merged.compute_mean_reprojection_error()
    if not np.isfinite(err) or err > 10:
        log(f"  merged bundle adjustment diverged (mean error {err:.3g} px); kept the triangulated model")
        merged = backup
    pycolmap.ObservationManager(merged).filter_all_points3D(4.0, 1.5)
    steps.append({"step": "merge_chunks", "joins": joins, "seconds": round(time.time() - t, 1),
                  "triangulation_options": "IncrementalPipelineOptions(num_threads=%d)" % args.threads,
                  "bundle_adjustment": "Cauchy loss, all frames and points"})
    return merged, joins


# ---------------------------------------------------------------------------
# Outputs
# ---------------------------------------------------------------------------

def write_ply(path, xyz, rgb, extra=None):
    """Binary little-endian PLY with x y z (double) red green blue (uchar) [+ int fields]."""
    xyz = np.asarray(xyz, dtype="<f8").reshape(-1, 3)
    rgb = np.asarray(rgb, dtype=np.uint8).reshape(-1, 3)
    extra = extra or {}
    fields = [("x", "<f8"), ("y", "<f8"), ("z", "<f8"), ("red", "u1"), ("green", "u1"), ("blue", "u1")]
    fields += [(k, "<i4") for k in extra]
    arr = np.zeros(len(xyz), dtype=fields)
    arr["x"], arr["y"], arr["z"] = xyz[:, 0], xyz[:, 1], xyz[:, 2]
    arr["red"], arr["green"], arr["blue"] = rgb[:, 0], rgb[:, 1], rgb[:, 2]
    for k, v in extra.items():
        arr[k] = np.asarray(v, dtype="<i4")
    names = {"<f8": "double", "u1": "uchar", "<i4": "int"}
    head = ["ply", "format binary_little_endian 1.0", f"element vertex {len(arr)}"]
    head += [f"property {names[t]} {n}" for n, t in fields]
    head.append("end_header")
    with open(path, "wb") as f:
        f.write(("\n".join(head) + "\n").encode("ascii"))
        f.write(arr.tobytes())


def rec_points(rec):
    ids = list(rec.point3D_ids())
    xyz = np.array([rec.points3D[i].xyz for i in ids]).reshape(-1, 3)
    rgb = np.array([rec.points3D[i].color for i in ids]).reshape(-1, 3)
    return ids, xyz, rgb


def camera_json(cam):
    d = {"camera_id": int(cam.camera_id), "model": cam.model_name, "width": int(cam.width),
         "height": int(cam.height), "params": [float(v) for v in cam.params],
         "params_info": cam.params_info}
    d["focal_px"] = float(cam.mean_focal_length())
    d["principal_point"] = [float(cam.principal_point_x), float(cam.principal_point_y)]
    return d


def write_outputs(out, set_name, photos, items, image_root, root, report):
    sparse = out / "sparse"
    if sparse.exists():
        shutil.rmtree(sparse)
    comp_of = {}
    comps = []
    for k, rec in enumerate(items):
        d = sparse / str(k)
        d.mkdir(parents=True, exist_ok=True)
        rec.write_binary(str(d))
        rec.write_text(str(d))
        st = model_stats(rec)
        st["component"] = k
        comps.append(st)
        for i in rec.reg_image_ids():
            comp_of.setdefault(rec.image(i).name, (k, rec, i))
    images = []
    for q in photos:
        src = q["src"]
        e = {"name": q["name"], "sequence": q["sequence"],
             "derived": Path(q["blurred"]).resolve().relative_to(root.resolve()).as_posix(),
             "mask": Path(q["mask"]).resolve().relative_to(root.resolve()).as_posix() if q["mask"] else None,
             "raw": src.get("raw"), "raw_member": src.get("raw_member"), "license": src.get("license"),
             "time": src.get("time"), "gps": q["gps"], "flags": q["flags"],
             "registered": q["name"] in comp_of, "component": None, "camera": None, "colmap": None,
             "campus": None}
        if q["name"] in comp_of:
            k, rec, iid = comp_of[q["name"]]
            im = rec.image(iid)
            T = im.cam_from_world().matrix()
            e["component"] = k
            e["camera"] = camera_json(rec.camera(im.camera_id))
            e["colmap"] = {"image_id": int(iid), "R_cam_from_world": T[:, :3].round(9).tolist(),
                           "t_cam_from_world": T[:, 3].round(9).tolist(),
                           "center": np.asarray(im.projection_center()).round(9).tolist()}
        images.append(e)
    cams = {"version": 1, "set": set_name, "made": datetime.datetime.now().isoformat(timespec="seconds"),
            "script": "Tools/VanderbiltCampus/pipeline/align.py",
            "image_root": image_root.resolve().relative_to(root.resolve()).as_posix(),
            "pose_convention": "COLMAP cam_from_world: x_cam = R @ x_world + t; center = -R.T @ t; "
                               "camera axes x right, y down, z forward (viewing direction)",
            "frames": {"colmap": "each component has its own arbitrary COLMAP frame and scale",
                       "campus": "filled by register_lidar.py (campus meters, config/frame.json)"},
            "components": comps, "images": images}
    save_json(out / "cameras.json", cams)
    if items:
        ids, xyz, rgb = rec_points(items[0])
        write_ply(out / "points.ply", xyz, rgb, {"component": np.zeros(len(ids))})
    return comps, comp_of


def per_sequence(photos, comp_of):
    by = defaultdict(lambda: {"given": 0, "registered": 0, "components": set()})
    for q in photos:
        s = by[q["sequence"]]
        s["given"] += 1
        if q["name"] in comp_of:
            s["registered"] += 1
            s["components"].add(comp_of[q["name"]][0])
    return {k: {"given": v["given"], "registered": v["registered"],
                "registered_pct": round(100.0 * v["registered"] / v["given"], 1),
                "components": sorted(v["components"])} for k, v in sorted(by.items())}


def record_manifest(out, root, set_name, license_text):
    import manifest
    db = manifest.connect(root)
    skipped = []
    n = 0
    try:
        for f in sorted(out.rglob("*")):
            if not f.is_file() or f.suffix in (".lock",):
                continue
            if f.stat().st_size > DB_MANIFEST_LIMIT:
                skipped.append({"file": f.relative_to(out).as_posix(), "bytes": f.stat().st_size})
                continue
            manifest.add_file(f, source=f"pipeline/align.py --set {set_name}", license=license_text,
                              stage=3, root=root, db=db)
            n += 1
        db.commit()
    finally:
        db.close()
    return n, skipped


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def run_attempt(pycolmap, name, cfg, photos, image_root, mask_dir, work, args, steps, chunked):
    """One extraction + matching + mapping attempt. Returns (items, names, info)."""
    db_path = work / f"database_{cfg['db']}.db"
    t0 = time.time()
    info = {"attempt": name, "config": {k: v for k, v in cfg.items()}}
    # A database made earlier in this same run (another attempt) is always reused.
    made = getattr(args, "_made_dbs", set())
    args._made_dbs = made
    fresh = not db_path.exists() or (not args.reuse and db_path not in made)
    if fresh:
        made.add(db_path)
        if db_path.exists():
            db_path.unlink()
        extract(pycolmap, db_path, image_root, photos, mask_dir, args, steps, cfg["max_features"], cfg["peak"])
    else:
        log(f"  reusing features in {db_path.name}")
        steps.append({"step": "extract_features", "reused": db_path.name})
        steps.append(reset_camera_priors(pycolmap, db_path, photos, args))
    match_key = (cfg["matcher"], cfg["overlap"], cfg["radius"])
    done = work / f"{db_path.stem}.matched.json"
    already = load_json(done) if done.exists() else []
    if fresh or list(match_key) not in already:
        match(pycolmap, db_path, work, photos, cfg["matcher"], cfg["overlap"], cfg["radius"], args, steps)
        already = ([] if fresh else already) + [list(match_key)]
        save_json(done, already)
    else:
        log("  reusing matches")
        steps.append({"step": "match", "reused": True, "matcher": cfg["matcher"]})
    if args.gps_prior:
        steps.append(write_gps_priors(pycolmap, db_path, photos, args.root, args.gps_sigma))
    results = {}
    mappers = ["incremental", "global"] if args.mapper == "both" else [args.mapper]
    for mp in mappers:
        t = time.time()
        mdir = work / (f"map_{name}_{mp}" + ("_chunked" if chunked else ""))
        if mdir.exists():
            shutil.rmtree(mdir)
        try:
            if chunked:
                chunks = make_chunks(photos, args.chunk, args.overlap)
                chunk_recs, chunk_info = [], []
                for ci, idx in enumerate(chunks):
                    names = [photos[i]["name"] for i in idx]
                    fn = map_incremental if mp == "incremental" else map_global
                    recs, ostr = fn(pycolmap, db_path, image_root, mdir / f"chunk{ci}", args, names)
                    its, nm = summarize(recs)
                    chunk_info.append({"chunk": ci, "images": len(idx), "first": names[0], "last": names[-1],
                                       "components": [r.num_reg_images() for r in its]})
                    log(f"  chunk {ci}: {len(idx)} images -> components {[r.num_reg_images() for r in its]}")
                    if its:
                        chunk_recs.append(its[0])
                steps.append({"step": "map_chunks", "mapper": mp, "chunk": args.chunk, "overlap": args.overlap,
                              "chunks": chunk_info, "options": ostr})
                if not chunk_recs:
                    results[mp] = ([], set())
                    continue
                merged, joins = merge_chunks(pycolmap, chunk_recs, db_path, image_root, mdir, args, steps)
                items, names = summarize({0: merged})
                # Chunks that did not join are kept as their own components.
                for j in joins:
                    if not j["joined"]:
                        r = chunk_recs[j["chunk"]]
                        items.append(r)
                        names |= reg_names(r)
            else:
                fn = map_incremental if mp == "incremental" else map_global
                recs, ostr = fn(pycolmap, db_path, image_root, mdir, args)
                items, names = summarize(recs)
                steps.append({"step": "map", "mapper": mp, "attempt": name, "options": ostr,
                              "components": [r.num_reg_images() for r in items],
                              "seconds": round(time.time() - t, 1)})
        except Exception as ex:                       # a mapper crash should not lose the other mapper
            log(f"  mapper {mp} failed: {ex}")
            steps.append({"step": "map", "mapper": mp, "attempt": name, "error": str(ex)})
            items, names = [], set()
        dt = time.time() - t
        err = items[0].compute_mean_reprojection_error() if items else float("nan")
        log(f"  {mp}: {len(names)}/{len(photos)} registered, components {[r.num_reg_images() for r in items]}, "
            f"largest mean reproj {err:.3f} px, {dt:.0f} s")
        results[mp] = (items, names)
        info[mp] = {"registered": len(names), "components": [r.num_reg_images() for r in items],
                    "largest_mean_reproj_px": round(err, 4) if items else None, "seconds": round(dt, 1)}
    best_mp = max(results, key=lambda m: score(*results[m]))
    info["kept_mapper"] = best_mp
    if args.refine_px:
        items, names = results[best_mp]
        new, rinfo = [], []
        for k, r in enumerate(items):
            t = time.time()
            r2, ri = refine_model(pycolmap, r, db_path, image_root, work / f"refine_{name}_{k}", args)
            ri["component"] = k
            ri["seconds"] = round(time.time() - t, 1)
            log(f"  refine component {k}: mean reproj {ri['before']['mean_reproj_px']} -> "
                f"{ri['after']['mean_reproj_px']} px, points {ri['before']['points']} -> {ri['after']['points']}")
            new.append(r2)
            rinfo.append(ri)
        new.sort(key=lambda r: -r.num_reg_images())
        nm = set()
        for r in new:
            nm |= reg_names(r)
        results[best_mp] = (new, nm)
        steps.append({"step": "refine", "attempt": name, "components": rinfo})
    info["seconds"] = round(time.time() - t0, 1)
    items, names = results[best_mp]
    return items, names, info, db_path


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--set", required=True, help="capture set under derived/capture/ (for example kartaview-2026-11-19)")
    ap.add_argument("--images", help="image folder (default: the set's blurred/ folder from photos.json)")
    ap.add_argument("--masks", help="mask folder with <name>.png (default: masks from photos.json)")
    ap.add_argument("--matcher", default="auto",
                    choices=["auto", "exhaustive", "sequential", "spatial", "hybrid", "vocab"],
                    help="auto: exhaustive for sets <= 100 photos without GPS, otherwise hybrid")
    ap.add_argument("--mapper", default="incremental", choices=["incremental", "global", "both"])
    ap.add_argument("--max-size", type=int, default=2400, help="long side in pixels for SIFT (default 2400)")
    ap.add_argument("--max-features", type=int, default=8192)
    ap.add_argument("--peak-threshold", type=float, default=0.0066667, help="SIFT peak threshold (COLMAP default)")
    ap.add_argument("--camera-model", default="SIMPLE_RADIAL", choices=["SIMPLE_RADIAL", "OPENCV"])
    ap.add_argument("--default-focal-35mm", type=float, default=26.0,
                    help="35 mm equivalent focal length when EXIF has none (26 = typical phone main camera)")
    ap.add_argument("--seq-overlap", type=int, default=10, help="sequential matching: next N photos")
    ap.add_argument("--gps-radius", type=float, default=60.0, help="spatial matching radius in meters")
    ap.add_argument("--gps-neighbors", type=int, default=20, help="spatial matching: at most N neighbors")
    ap.add_argument("--guided", action="store_true", help="guided matching (slower, sometimes more matches)")
    ap.add_argument("--vocab-tree", help="COLMAP vocabulary tree file for --matcher vocab")
    ap.add_argument("--chunk", type=int, default=0, help="split into chunks of N photos (0 = no chunks)")
    ap.add_argument("--overlap", type=int, default=30, help="photos shared by neighboring chunks")
    ap.add_argument("--min-model-size", type=int, default=5)
    ap.add_argument("--init-min-tri-angle", type=float, default=None,
                    help="incremental mapper initial pair triangulation angle (COLMAP default 16)")
    ap.add_argument("--fix-intrinsics", action="store_true",
                    help="keep focal length and distortion at the prior (helps forward-moving street sequences, "
                         "where self-calibration is poorly constrained)")
    ap.add_argument("--gps-prior", action="store_true",
                    help="incremental mapper: use GPS positions as pose priors (robust loss)")
    ap.add_argument("--gps-sigma", type=float, default=5.0, help="GPS standard deviation for --gps-prior (m)")
    ap.add_argument("--refine-px", type=float, default=None,
                    help="after mapping: re-triangulate, bundle-adjust and drop observations above this error (px)")
    ap.add_argument("--threads", type=int, default=8)
    ap.add_argument("--target-registered", type=float, default=0.90, help="fallbacks run below this share")
    ap.add_argument("--no-fallbacks", action="store_true")
    ap.add_argument("--max-fallbacks", type=int, default=2, help="run at most N fallbacks (1 = overlap only)")
    ap.add_argument("--sequences", help="only these sequences (comma separated), e.g. for a chunk test")
    ap.add_argument("--skip-flagged", action="store_true", help="leave out photos with intake quality flags")
    ap.add_argument("--out", default="v1", help="output version folder under derived/alignment/<set>/")
    ap.add_argument("--reuse", action="store_true", help="reuse features/matches already in work/ (same options)")
    ap.add_argument("--work", help="work folder (default <out>/work); point two runs at one to share features")
    ap.add_argument("--no-manifest", action="store_true")
    args = ap.parse_args(argv)

    import pycolmap
    t_start = time.time()
    root = data_root()
    args.root = root
    set_name = set_slug(args.set)
    data, image_root, photos = load_photos(root, set_name, args.images, args.masks, args.skip_flagged)
    if args.sequences:
        keep = set(args.sequences.split(","))
        photos = [q for q in photos if q["sequence"] in keep]
        if not photos:
            fail(f"no photos in sequences {sorted(keep)}")
    has_gps = sum(1 for q in photos if q["gps"]) >= 3
    matcher = args.matcher
    if matcher == "auto":
        matcher = "exhaustive" if (len(photos) <= 100 and not has_gps) else "hybrid"
    out = root / "derived" / "alignment" / set_name / args.out
    work = Path(args.work) if args.work else out / "work"
    work.mkdir(parents=True, exist_ok=True)
    mask_dir = work / "masks"
    n_masks = prepare_masks(photos, mask_dir)
    log(f"set {set_name}: {len(photos)} photos, {n_masks} masks, GPS {'yes' if has_gps else 'no'}, "
        f"matcher {matcher}, mapper {args.mapper}, max size {args.max_size}, threads {args.threads}")

    steps = []
    attempts = [{"name": "base", "db": "base", "matcher": matcher, "overlap": args.seq_overlap,
                 "radius": args.gps_radius, "max_features": args.max_features, "peak": args.peak_threshold}]
    if not args.no_fallbacks:
        if matcher in ("sequential", "hybrid", "spatial"):
            attempts.append(dict(attempts[0], name="fallback_overlap", overlap=args.seq_overlap * 2,
                                 radius=args.gps_radius * 1.5))
        attempts.append(dict(attempts[-1], name="fallback_features", db="morefeat",
                             max_features=max(16384, args.max_features * 2), peak=args.peak_threshold * 0.6))
        attempts = attempts[:1 + args.max_fallbacks]
    tried = []
    best = None
    for cfg in attempts:
        log(f"attempt {cfg['name']}")
        items, names, info, db_path = run_attempt(pycolmap, cfg["name"], cfg, photos, image_root, mask_dir,
                                                  work, args, steps, chunked=args.chunk > 0)
        tried.append(info)
        if best is None or score(items, names) > score(best[0], best[1]):
            best = (items, names, cfg, db_path)
        # Judge by the largest connected model: photos in a separate small model are not
        # aligned with the rest.
        share = (items[0].num_reg_images() / len(photos)) if items else 0.0
        ok_err = bool(items) and items[0].compute_mean_reprojection_error() <= 1.0
        if share >= args.target_registered and ok_err:
            break
        if cfg is not attempts[-1]:
            log(f"  {share:.0%} registered; trying the next fallback")

    items, names, cfg, db_path = best
    if not items:
        fail("no photos registered in any attempt")
    comps, comp_of = write_outputs(out, set_name, photos, items, image_root, root, None)
    largest = comps[0]
    report = {
        "version": 1, "set": set_name, "out": args.out,
        "made": datetime.datetime.now().isoformat(timespec="seconds"),
        "script": "Tools/VanderbiltCampus/pipeline/align.py", "argv": sys.argv[1:] if argv is None else argv,
        "colmap": {"pycolmap": pycolmap.__version__, "cuda": bool(pycolmap.has_cuda),
                   "colmap_exe": local_config().get("colmap")},
        "images_given": len(photos),
        "registered": len(names),
        "registered_pct": round(100.0 * len(names) / len(photos), 2),
        "registered_largest_component": largest["registered"],
        "registered_largest_pct": round(100.0 * largest["registered"] / len(photos), 2),
        "components": comps,
        "mean_reproj_px": largest["mean_reproj_px"], "median_reproj_px": largest["median_reproj_px"],
        "mean_track_length": largest["mean_track_length"],
        "note": "error and track numbers at top level are the largest component; per component in 'components'",
        "per_sequence": per_sequence(photos, comp_of),
        "kept_attempt": cfg["name"], "attempts": tried,
        "options": {"matcher": matcher, "mapper": args.mapper, "max_size": args.max_size,
                    "max_features": cfg["max_features"], "peak_threshold": cfg["peak"],
                    "camera_model": args.camera_model, "default_focal_35mm": args.default_focal_35mm,
                    "seq_overlap": cfg["overlap"], "gps_radius_m": cfg["radius"],
                    "gps_neighbors": args.gps_neighbors, "chunk": args.chunk, "chunk_overlap": args.overlap,
                    "min_model_size": args.min_model_size, "fix_intrinsics": args.fix_intrinsics, "gps_prior": args.gps_prior, "refine_px": args.refine_px,
                    "init_min_tri_angle": args.init_min_tri_angle, "threads": args.threads, "masks": n_masks,
                    "skip_flagged": args.skip_flagged},
        "steps": steps,
        "targets": {"registered_pct": 90.0, "mean_reproj_px": 1.0},
        "meets_targets": {"registered": len(names) / len(photos) >= 0.9,
                          "registered_largest_component": largest["registered"] / len(photos) >= 0.9,
                          "mean_reproj": (largest["mean_reproj_px"] or 99) <= 1.0},
        "runtime_s": round(time.time() - t_start, 1),
        "registration": None,
    }
    save_json(out / "report.json", report)
    log(f"done: {len(names)}/{len(photos)} registered ({report['registered_pct']}%), largest component "
        f"{largest['registered']}, mean reproj {largest['mean_reproj_px']} px, track {largest['mean_track_length']}, "
        f"{report['runtime_s']:.0f} s")
    if not args.no_manifest:
        lic = (data["photos"][0].get("license") or "") + " (derived)"
        n, skipped = record_manifest(out, root, set_name, lic)
        report["manifest"] = {"recorded": n, "skipped_over_500MB": skipped}
        save_json(out / "report.json", report)
        import manifest
        manifest.add_file(out / "report.json", source=f"pipeline/align.py --set {set_name}", license=lic, stage=3,
                          root=root)
        log(f"manifest: {n} files recorded, {len(skipped)} large files skipped")
    return 0


if __name__ == "__main__":
    sys.exit(main())
