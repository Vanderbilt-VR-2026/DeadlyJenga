"""Stage 3: which registered photos see which facades -> sources.json per building.

    python align_sources.py --set kartaview-2026-11-19
    python align_sources.py --set kartaview-2026-11-19 --dry-run

Reads the registered cameras (cameras.json after register_lidar.py) and the
facades in config/shot_list.json (id <building>/<facade>, centerline, normal,
height). Each facade is sampled as a rectangle (every --step m along the
centerline, from the building's ground to ground + height). A photo sees a
sample point when:
  - the camera is in front of the facade plane,
  - the point projects inside the photo (view frustum) and lies in front of the camera,
  - it is closer than --max-distance m,
  - the sight line (top view) does not cross another building's footprint.
Coverage % = share of the facade samples seen by >= 2 photos at an incidence angle
<= 70 deg (angle between the sight line and the facade normal). A photo is listed
for a facade when it sees at least --min-share of its samples.

Writes (STANDARDS section 6) buildings/<id>/sources.json in the archive and the git
mirror Tools/VanderbiltCampus/buildings/<id>/sources.json. Existing entries from
other sets are kept (later stages only add). Then marks the facades with coverage
> 0 and >= 2 photos as "aligned" in config/coverage_status.json (through
intake_coverage's lock and atomic write) and rebuilds the coverage page.
"""
import argparse
import datetime
import subprocess
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CONFIG, TOOLS, data_root, fail, load_json, save_json  # noqa: E402

MAX_INCIDENCE = 70.0


def project(cam, R, t, X):
    """Pixel coordinates of world points X for a COLMAP camera (SIMPLE_RADIAL / PINHOLE / OPENCV)."""
    Xc = X @ np.asarray(R).T + np.asarray(t)
    z = Xc[:, 2]
    with np.errstate(divide="ignore", invalid="ignore"):
        u, v = Xc[:, 0] / z, Xc[:, 1] / z
    p = cam["params"]
    m = cam["model"]
    if m == "SIMPLE_RADIAL":
        f, cx, cy, k = p
        d = 1 + k * (u * u + v * v)
        x, y = f * u * d + cx, f * v * d + cy
    elif m == "SIMPLE_PINHOLE":
        f, cx, cy = p
        x, y = f * u + cx, f * v + cy
    elif m == "PINHOLE":
        fx, fy, cx, cy = p
        x, y = fx * u + cx, fy * v + cy
    elif m == "RADIAL":
        f, cx, cy, k1, k2 = p
        r2 = u * u + v * v
        d = 1 + k1 * r2 + k2 * r2 * r2
        x, y = f * u * d + cx, f * v * d + cy
    elif m == "OPENCV":
        fx, fy, cx, cy, k1, k2, p1, p2 = p
        r2 = u * u + v * v
        d = 1 + k1 * r2 + k2 * r2 * r2
        uu = u * d + 2 * p1 * u * v + p2 * (r2 + 2 * u * u)
        vv = v * d + p1 * (r2 + 2 * v * v) + 2 * p2 * u * v
        x, y = fx * uu + cx, fy * vv + cy
    else:
        raise ValueError(f"camera model {m} not supported")
    return np.stack([x, y], 1), z


def facade_samples(centerline, ground_z, height, step):
    """Sample points (N x 3) on the facade rectangle plus their along-wall coordinate."""
    line = np.asarray(centerline, dtype=float)
    pts = []
    for a, b in zip(line[:-1], line[1:]):
        L = np.linalg.norm(b - a)
        n = max(1, int(np.ceil(L / step)))
        for i in range(n):
            pts.append(a + (i + 0.5) / n * (b - a))
    nz = max(1, int(np.ceil(height / step)))
    zs = ground_z + (np.arange(nz) + 0.5) / nz * height
    return np.array([[x, y, z] for x, y in pts for z in zs])


def visibility(samples, normal2, cams, blockers, max_distance, min_share=0.05):
    """Per camera: which samples it sees and at what incidence/distance.

    cams: list of dicts with center, R, t, cam (intrinsics), w, h, id.
    blockers: shapely STRtree + geometries of other buildings' footprints (top view).
    Returns {cam id: (seen mask, incidence deg, distance m)} for cameras that see >= min_share."""
    from shapely.geometry import LineString
    nrm = np.array([normal2[0], normal2[1], 0.0])
    nrm /= np.linalg.norm(nrm)
    tree, geoms = blockers
    out = {}
    for c in cams:
        C = np.asarray(c["center"])
        v = C[None] - samples                       # sample -> camera
        dist = np.linalg.norm(v, axis=1)
        front = (v @ nrm) > 0.1
        if not front.any():
            continue
        cosang = (v @ nrm) / np.maximum(dist, 1e-9)
        inc = np.degrees(np.arccos(np.clip(cosang, -1, 1)))
        xy, z = project(c["cam"], c["R"], c["t"], samples)
        inview = (z > 0.1) & (xy[:, 0] >= 0) & (xy[:, 0] < c["w"]) & (xy[:, 1] >= 0) & (xy[:, 1] < c["h"])
        seen = front & inview & (dist <= max_distance)
        if seen.sum() < max(1, min_share * len(samples)):
            continue
        if tree is not None:
            # Occlusion in top view: test each distinct wall column once.
            cols = np.unique(np.round(samples[seen, :2], 2), axis=0)
            blocked_cols = set()
            for p in cols:
                # shorten the segment a little at the facade end so its own wall line does not count
                seg = LineString([tuple(C[:2]), tuple(p + (C[:2] - p) / max(np.linalg.norm(C[:2] - p), 1e-6) * 0.3)])
                for gi in tree.query(seg):
                    if geoms[gi].intersects(seg):
                        blocked_cols.add((round(p[0], 2), round(p[1], 2)))
                        break
            if blocked_cols:
                key = [(round(x, 2), round(y, 2)) for x, y in samples[:, :2]]
                blk = np.array([k in blocked_cols for k in key])
                seen &= ~blk
        if seen.sum() < max(1, min_share * len(samples)):
            continue
        out[c["id"]] = (seen, inc, dist)
    return out


def coverage(n_samples, vis):
    good = np.zeros(n_samples, dtype=int)
    for seen, inc, _ in vis.values():
        good += (seen & (inc <= MAX_INCIDENCE)).astype(int)
    cov = float((good >= 2).mean()) if n_samples else 0.0
    dists = [float(dist[seen].mean()) for seen, _, dist in vis.values() if seen.any()]
    return cov, (float(np.mean(dists)) if dists else None)


def load_registered_cams(cams_json, set_name):
    out = []
    for e in cams_json["images"]:
        if not e.get("campus") or not e.get("camera") or e["campus"].get("reliable") is False:
            continue
        cam = e["camera"]
        R = np.array(e["campus"]["R_cam_from_campus"])
        C = np.array(e["campus"]["center"])
        out.append({"id": f"{set_name}/{e['name']}", "center": C, "R": R, "t": -R @ C, "cam": cam,
                    "w": cam["width"], "h": cam["height"], "derived": e["derived"], "component": e["component"]})
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--set", required=True)
    ap.add_argument("--version", default="v1")
    ap.add_argument("--step", type=float, default=0.5, help="facade sample spacing (m)")
    ap.add_argument("--max-distance", type=float, default=80.0, help="farthest useful photo (m)")
    ap.add_argument("--min-share", type=float, default=0.05, help="a photo must see this share of the facade")
    ap.add_argument("--dry-run", action="store_true", help="print results; write nothing")
    ap.add_argument("--no-coverage", action="store_true", help="do not touch coverage_status.json")
    args = ap.parse_args(argv)

    from shapely.geometry import Polygon
    from shapely.strtree import STRtree
    root = data_root()
    set_name = args.set.strip("/")
    out = root / "derived" / "alignment" / set_name / args.version
    cams_json = load_json(out / "cameras.json")
    cams = load_registered_cams(cams_json, set_name)
    if not cams:
        fail("no photos with a campus pose; run register_lidar.py first")
    C = np.array([c["center"] for c in cams])
    shot = load_json(CONFIG / "shot_list.json")
    fps = {b["id"]: b for b in load_json(root / "derived" / "buildings" / "v1" / "footprints.json")["buildings"]}
    from scipy.spatial import cKDTree
    ctree = cKDTree(C[:, :2])
    facades = [e for e in shot["entries"] if e.get("kind") == "facade" and e.get("centerline")]
    near = []
    for e in facades:
        d, _ = ctree.query(np.asarray(e["centerline"], dtype=float))
        if np.min(d) <= args.max_distance:
            near.append(e)
    # Footprint polygons for occlusion (every building near the cameras).
    poly_of = {}
    for bid, b in fps.items():
        cx, cy = b["centroid"]
        if np.min(np.hypot(C[:, 0] - cx, C[:, 1] - cy)) > args.max_distance + 150:
            continue
        rings = [Polygon(p[0]) for p in b["polygon"] if len(p[0]) >= 3]
        poly_of[bid] = [r.buffer(0) for r in rings]
    print(f"{len(cams)} registered photos; {len(near)} of {len(facades)} shot-list facades within "
          f"{args.max_distance} m of a camera")
    results = {}
    for e in near:
        bid = e["building"]
        b = fps.get(bid)
        gz = (b or {}).get("ground", {}).get("ground_z") if b else None
        if gz is None:
            continue
        height = float(e.get("height_m") or 10.0)
        S = facade_samples(e["centerline"], gz, height, args.step)
        others = [g for k, gl in poly_of.items() if k != bid for g in gl]
        blk = (STRtree(others), others) if others else (None, [])
        dmin = np.min(np.linalg.norm(C[:, None, :2] - S[None, :, :2], axis=2), axis=1)
        cand = [c for c, d in zip(cams, dmin) if d <= args.max_distance]
        vis = visibility(S, e["normal"], cand, blk, args.max_distance, args.min_share)
        if not vis:
            continue
        cov, mdist = coverage(len(S), vis)
        photos = sorted(vis)
        results[e["id"]] = {"building": bid, "facade": e["facade"], "shot_list_id": e["id"],
                            "photos": photos, "photo_count": len(photos),
                            "coverage_pct": round(100 * cov, 1),
                            "mean_view_distance_m": round(mdist, 2) if mdist else None,
                            "samples": int(len(S)), "height_m": height,
                            "incidence_le_70_photos": int(sum(1 for s, i, _ in vis.values() if (s & (i <= MAX_INCIDENCE)).any()))}
    by_b = {}
    for fid, r in results.items():
        by_b.setdefault(r["building"], {})[r["facade"]] = r
    print(f"{len(results)} facades on {len(by_b)} buildings are seen by registered photos")
    for bid, fs in sorted(by_b.items()):
        for f, r in sorted(fs.items()):
            print(f"  {bid}/{f}: {r['photo_count']} photos, coverage {r['coverage_pct']}%, "
                  f"mean distance {r['mean_view_distance_m']} m")
    if args.dry_run:
        return 0

    now = datetime.datetime.now().isoformat(timespec="seconds")
    import manifest
    db = manifest.connect(root)
    written = []
    # Remove this set's earlier entries everywhere first (a re-run replaces, never piles up).
    for folder in (root / "buildings", TOOLS / "buildings"):
        for f in folder.glob("*/sources.json"):
            old = load_json(f)
            changed = False
            for fname in list(old.get("facades", {})):
                ent = old["facades"][fname]
                if set_name in ent.get("sets", {}):
                    ent["sets"].pop(set_name)
                    ent["photos"] = [p for p in ent.get("photos", []) if not p.startswith(set_name + "/")]
                    changed = True
                    if not ent["sets"] and not ent["photos"] and not ent.get("frames_360") and not ent.get("scans"):
                        old["facades"].pop(fname)
            if changed:
                if old["facades"]:
                    save_json(f, old)
                else:
                    f.unlink()                      # only this set had written it
                    try:
                        f.parent.rmdir()            # git mirror folder made only for it
                    except OSError:
                        pass
    for bid, fs in sorted(by_b.items()):
        for target in (root / "buildings" / bid / "sources.json", TOOLS / "buildings" / bid / "sources.json"):
            old = load_json(target) if target.exists() else {"building": bid, "version": 1, "facades": {}}
            old.setdefault("facades", {})
            for f, r in fs.items():
                ent = old["facades"].setdefault(f, {"photos": [], "frames_360": [], "scans": [], "sets": {}})
                ent.setdefault("sets", {})
                keep = [p for p in ent.get("photos", []) if not p.startswith(set_name + "/")]
                ent["photos"] = sorted(set(keep) | set(r["photos"]))
                ent.setdefault("frames_360", [])
                ent.setdefault("scans", [])
                ent["shot_list_id"] = r["shot_list_id"]
                ent["sets"][set_name] = {"alignment": f"derived/alignment/{set_name}/{args.version}",
                                         "photos": r["photo_count"], "coverage_pct": r["coverage_pct"],
                                         "mean_view_distance_m": r["mean_view_distance_m"]}
                # Totals: the best set's coverage (sets are not combined until a joint alignment exists).
                ent["coverage_pct"] = max(s["coverage_pct"] for s in ent["sets"].values())
                ds = [s["mean_view_distance_m"] for s in ent["sets"].values() if s["mean_view_distance_m"]]
                ent["mean_view_distance_m"] = round(float(np.mean(ds)), 2) if ds else None
            old["updated"] = now
            old["script"] = "Tools/VanderbiltCampus/pipeline/align_sources.py"
            old["rule"] = ("photo listed when it sees >= %g of the facade samples (in front, in frustum, <= %g m, "
                           "not behind another footprint); coverage_pct = share of samples seen by >= 2 photos at "
                           "incidence <= 70 deg" % (args.min_share, args.max_distance))
            save_json(target, old)
            written.append(target)
        manifest.add_file(root / "buildings" / bid / "sources.json", source="pipeline/align_sources.py",
                          license="derived from capture photos (see sets)", stage=3, root=root, db=db)
    db.commit()
    db.close()

    aligned = [fid for fid, r in results.items() if r["coverage_pct"] > 0 and r["photo_count"] >= 2]
    summary = {"set": set_name, "version": args.version, "made": now, "facades_seen": len(results),
               "buildings": sorted(by_b), "aligned_entries": sorted(aligned), "facades": results,
               "options": vars(args)}
    save_json(out / "sources_summary.json", summary)
    if not args.no_coverage and aligned:
        import intake_coverage as ic
        path = CONFIG / "coverage_status.json"
        with ic._Lock(path):
            data = ic._read(path)
            ents = data.setdefault("entries", {})
            # Undo this set's earlier "aligned" marks, then set them again from this run.
            for fid, e in ents.items():
                al = e.get("aligned") or {}
                if set_name in al:
                    al.pop(set_name)
                    if not al:
                        e.pop("aligned", None)
                        if e.get("status") == "aligned":
                            st, reason = ic.status_for(e.get("photo_count", 0), e.get("usable_count", 0),
                                                       e.get("flagged_count", 0), int(e.get("min_photos") or 12))
                            e["status"] = st
                            if reason:
                                e["reason"] = reason
            for fid in aligned:
                e = ents.setdefault(fid, {"status": "not_started", "sets": {}})
                info = next(x for x in facades if x["id"] == fid)
                for k in ("kind", "building", "facade", "priority", "min_photos"):
                    if info.get(k) is not None and k not in e:
                        e[k] = info[k]
                e["status"] = "aligned"
                e.pop("reason", None)
                e.setdefault("aligned", {})[set_name] = {
                    "version": args.version, "photos": results[fid]["photo_count"],
                    "coverage_pct": results[fid]["coverage_pct"], "date": now[:10]}
            data["updated"] = now
            data["entries"] = dict(sorted(ents.items()))
            ic._write_atomic(path, data)
        print(f"coverage_status.json: {len(aligned)} facades set to aligned")
        r = subprocess.run([sys.executable, str(Path(__file__).parent / "coverage_map.py"), "build"])
        if r.returncode != 0:
            fail("coverage_map.py build failed")
    print(f"wrote {len(written)} sources.json files ({len(by_b)} buildings, archive + git mirror)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
