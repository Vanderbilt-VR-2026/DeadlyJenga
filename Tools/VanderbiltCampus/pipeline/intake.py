"""Stage 2 intake: bring one day's capture into the archive and get it ready for alignment.

Usage:
  intake.py <source folder> [--date YYYY-MM-DD] [--device NAME] [--set NAME]
            [--license TEXT] [--dry-run] [--skip-blur] [--jobs N]

Steps:
 1. Copy the originals into raw/capture/<date>/<device>/ with sha256 hashes,
    never changing them (intake_copy.py). A folder that is already inside
    raw/capture/ (the sample sets) is used where it is.
 2. Read time, GPS, exposure and lens (ExifTool, or a provider sidecar such
    as KartaView's sequence.json), convert GPS to the campus frame and sort
    photos into shot-list areas (intake_meta.py, intake_areas.py).
 3. 360 videos: pick frames about every 0.5 m and cut them into flat views
    (intake_video.py).
 4. Blur faces and license plates in derived/ copies only, and write masks
    that hide people and vehicles for alignment (intake_privacy.py).
 5. Flag blurry, badly exposed, duplicate and tiny shots; nothing is deleted
    (intake_quality.py).
 6. Record every derived file in the manifest, write photos.json and
    intake_report.json, print a short report, and update
    config/coverage_status.json (intake_coverage.py).

Outputs in derived/capture/<date>/<device>/ (for in-place sample sets:
derived/capture/<set folder>/):
  photos.json          one record per photo / 360 view (metadata, area, flags, detections)
  intake_report.json   counts, flags per reason, areas, what to reshoot, timings
  blurred/             blurred JPEG copies (EXIF kept, including GPS; no thumbnails)
  masks/               PNG masks, white = usable for alignment
  views/               360 views before blurring (360 sets only)
  unpacked/            photos taken out of a zip in raw/ (zipped sets only)

Running it again skips work that is already done (same input hash, same rules).
"""
import argparse
import datetime
import json
import os
import re
import shutil
import sys
import time
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path

os.environ.setdefault("OPENCV_LOG_LEVEL", "ERROR")   # hide OpenCV DNN info warnings
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CONFIG, data_root, fail, load_json, local_config, save_json  # noqa: E402
import manifest  # noqa: E402
import intake_areas  # noqa: E402
import intake_copy  # noqa: E402
import intake_coverage  # noqa: E402
import intake_meta  # noqa: E402
import intake_privacy  # noqa: E402
import intake_quality  # noqa: E402
import intake_video  # noqa: E402
import intake_work  # noqa: E402
from intake_images import is_photo, is_video  # noqa: E402

OWN_LICENSE = "Evan's own capture"
DATE_RE = re.compile(r"(\d{4}-\d{2}-\d{2})")
RAW_EXT = {".dng"}
SHAREABLE = {
    "safe_to_share": ["config/coverage_status.json (status and counts only)",
                      "intake_report.json (counts, names, no pixels)",
                      "masks/ (black and white shapes only)"],
    "keep_on_this_machine": ["raw/ (originals: faces, plates, GPS)",
                             "views/ and unpacked/ (unblurred derived copies)",
                             "blurred/ (faces and plates blurred, but EXIF GPS and time are kept; "
                             "share only after a visual check and only if location data may go along)",
                             "photos.json (GPS positions and capture times)"],
}


def rel(path, root):
    return Path(path).resolve().relative_to(Path(root).resolve()).as_posix()


def slug(text):
    s = re.sub(r"[^A-Za-z0-9]+", "-", str(text or "")).strip("-").lower()
    return s or "camera"


def out_name(name):
    """Blurred copies are always .jpg (HEIC/DNG/PNG inputs get .jpg added)."""
    return name if Path(name).suffix.lower() in (".jpg", ".jpeg") else name + ".jpg"


def log(msg):
    print(msg, flush=True)


# ---- step 1: originals ---------------------------------------------------

def take_originals(args, root, db, files, src):
    """Returns (raw_dir, out_dir, set name, date, device, [(raw path, item name)], licenses, copy summary)."""
    cap = root / "raw" / "capture"
    summary = {"copied": 0, "copied_renamed": 0, "skipped_same": 0, "in_place": 0}
    licenses = {}
    if intake_copy.is_inside(src, cap):
        set_rel = src.resolve().relative_to(cap.resolve()).as_posix()
        m = DATE_RE.search(set_rel)
        date = args.date or (m.group(1) if m else datetime.date.today().isoformat())
        device = args.device or src.name
        out_dir = root / "derived" / "capture" / set_rel
        set_name = args.set or set_rel
        pairs = []
        for f in files:
            r = rel(f, root)
            row = db.execute("SELECT sha256, license FROM files WHERE path=?", (r,)).fetchone()
            digest = intake_copy.sha256(f)
            if row and row[0] != digest:
                fail(f"{r} no longer matches its manifest hash; raw/ must not change. Stop and investigate.")
            if not row and not args.dry_run:
                manifest.add_file(f, source="already in raw/capture (intake in place)",
                                  license=args.license or "unknown (not in manifest before intake)",
                                  stage=2, date=date, root=root, db=db)
            licenses[r] = (row[1] if row else None) or args.license or "unknown"
            pairs.append((f, f.relative_to(src).as_posix(), digest))
            summary["in_place"] += 1
        return src, out_dir, set_name, date, device, pairs, licenses, summary

    # Copy mode: the source is a card, phone folder or download folder.
    date = args.date
    if not date:
        exif = intake_meta.run_exiftool([f for f in files if is_photo(f) or is_video(f)], args.exiftool)
        times = sorted(t for t in (intake_meta.exif_record(v)["time"] for v in exif.values()) if t)
        date = times[0][:10] if times else datetime.date.today().isoformat()
    device = slug(args.device) if args.device else "camera"
    raw_dir = cap / date / device
    out_dir = root / "derived" / "capture" / date / device
    set_name = args.set or f"{date}/{device}"
    have = intake_copy.known_hashes(raw_dir, db, root)
    plan = intake_copy.plan_copy(files, raw_dir, have)
    lic = args.license or OWN_LICENSE
    pairs = []
    for p in plan:
        if p["action"] == "skip_same":
            summary["skipped_same"] += 1
        else:
            summary["copied" if p["action"] == "copy" else "copied_renamed"] += 1
            if not args.dry_run:
                intake_copy.copy_verified(p["src"], p["dest"], p["sha256"])
                manifest.add_file(p["dest"], source=str(p["src"]), license=lic, stage=2, date=date,
                                  root=root, db=db)
        # In a dry run nothing is copied, so later steps look at the source file.
        where = Path(p["src"]) if args.dry_run and p["action"] != "skip_same" else Path(p["dest"])
        pairs.append((where, Path(p["dest"]).name, p["sha256"]))
        if not args.dry_run:
            row = db.execute("SELECT license FROM files WHERE path=?", (rel(p["dest"], root),)).fetchone()
            licenses[rel(p["dest"], root)] = row[0] if row else lic
        else:
            licenses[str(p["dest"])] = lic
    if not args.dry_run:
        db.commit()
    return raw_dir, out_dir, set_name, date, device, pairs, licenses, summary


# ---- step 2: photo records ----------------------------------------------

def photo_items(pairs, root, out_dir, licenses, dry_run):
    """One record per photo to process (zip members unpacked into derived/unpacked/)."""
    items, new_files, notes = [], [], []
    by_stem = {}
    for f, name, digest in pairs:
        if is_photo(f):
            by_stem.setdefault((str(Path(name).parent), Path(name).stem.lower()), []).append((f, name, digest))
    for group in by_stem.values():
        # A DNG with a JPEG/HEIC of the same name: process the JPEG/HEIC, keep the DNG as the raw pair.
        main = [g for g in group if Path(g[0]).suffix.lower() not in RAW_EXT] or group
        dng = [g for g in group if Path(g[0]).suffix.lower() in RAW_EXT and g not in main]
        for f, name, digest in main:
            r = rel(f, root) if not dry_run or intake_copy.is_inside(f, root) else str(f)
            items.append({"name": name, "kind": "photo", "raw": r, "input_path": Path(f), "sha256": digest,
                          "license": licenses.get(r, "unknown"),
                          "raw_pair": rel(dng[0][0], root) if dng and not dry_run else None})
    for f, name, digest in pairs:
        if Path(f).suffix.lower() != ".zip":
            continue
        if dry_run:
            notes.append(f"{name}: zip would be unpacked into derived/unpacked/")
            continue
        zr = rel(f, root)
        before = set((out_dir / "unpacked").rglob("*")) if (out_dir / "unpacked").exists() else set()
        for member, path in intake_copy.unpack_zip(f, out_dir / "unpacked"):
            if path not in before:
                new_files.append(path)
            items.append({"name": rel(path, out_dir), "kind": "photo", "raw": zr, "raw_member": member,
                          "input_path": path, "sha256": intake_copy.sha256(path),
                          "license": licenses.get(zr, "unknown")})
    return items, new_files, notes


def read_metadata(items, exe, sidecar_dir):
    exif = intake_meta.run_exiftool([it["input_path"] for it in items], exe)
    side = intake_meta.read_sidecars(sidecar_dir)
    for it in items:
        key = str(Path(it["input_path"]).resolve())
        tags = exif.get(key)
        rec = intake_meta.merge(intake_meta.exif_record(tags) if tags else None, side.get(key))
        it.update(rec)
        it["size"] = Path(it["input_path"]).stat().st_size


# ---- step 3: 360 videos ---------------------------------------------------

def video_items(pairs, root, out_dir, licenses, args, pool_run):
    """Split each equirectangular video into views. Returns (items, new files, notes)."""
    items, new_files, notes = [], [], []
    for f, name, digest in pairs:
        if not is_video(f):
            continue
        info = intake_video.probe(f)
        if not intake_video.is_equirect(info):
            notes.append(f"{name}: {info['width']}x{info['height']} is not equirectangular (2:1); "
                         "export 360 MP4 from the camera app first. Skipped.")
            continue
        track = intake_video.gps_track(f, args.exiftool)
        speed = intake_video.track_speed(track)
        fps = intake_video.choose_fps(speed)
        key = f"{slug(Path(name).stem)}_{digest[:8]}"
        views = intake_video.view_list(args.views, args.up_row)
        size = intake_video.view_size(info["width"])
        params = {"fps": fps, "views": views, "fov": intake_video.FOV, "size": size,
                  "max_seconds": args.max_video_seconds}
        vdir = out_dir / "views" / key
        vjson = vdir / "views.json"
        recs = None
        if vjson.exists():
            old = load_json(vjson)
            if old.get("video_sha256") == digest and old.get("params") == json.loads(json.dumps(params)) \
                    and all((vdir.parent / r["name"]).exists() for r in old["views"]):
                recs = old["views"]
        if recs is None:
            if args.dry_run:
                n = int(min(info["duration"], args.max_video_seconds or 1e9) * fps) * len(views)
                notes.append(f"{name}: would extract about {n} views at {fps:g} fps")
                continue
            tmp = out_dir / "_frames" / key
            shutil.rmtree(tmp, ignore_errors=True)
            log(f"  360 {name}: {info['width']}x{info['height']}, {info['duration']:.1f} s, "
                f"speed {'%.2f m/s (GPS)' % speed if speed else 'unknown (1 m/s assumed)'}, {fps:g} fps")
            frames = intake_video.extract_frames(f, tmp, fps, args.max_video_seconds)
            tasks = [{"frame": str(fr), "time_s": t, "video_key": key, "views": views, "fov": intake_video.FOV,
                      "size": size, "out_dir": str(out_dir / "views"), "frame_index": i}
                     for i, (fr, t) in enumerate(frames)]
            recs = [r for chunk in pool_run(intake_work.split_video_frame, tasks, False) for r in chunk]
            for r in recs:
                r["name"] = key + "/" + Path(r["name"]).name
                r.pop("file", None)
            shutil.rmtree(tmp, ignore_errors=True)
            save_json(vjson, {"video": rel(f, root), "video_sha256": digest, "probe": info,
                              "gps_track_points": len(track), "speed_m_s": speed, "params": params,
                              "operator_mask_pitch_deg": intake_privacy.OPERATOR_PITCH,
                              "note": "yaw 0 = centre of the equirectangular frame, positive to the right; "
                                      "pitch positive = up; fov = horizontal and vertical field of view",
                              "views": recs})
            new_files += [vdir.parent / r["name"] for r in recs] + [vjson]
        notes.append(f"{name}: {len(recs)} views from {len(recs) // max(1, len(views))} frames at {fps:g} fps")
        meta = intake_meta.run_exiftool([f], args.exiftool).get(str(Path(f).resolve()))
        start_iso, start_t = intake_meta.parse_exif_time(meta or {})
        vr = rel(f, root)
        for r in recs:
            path = vdir.parent / r["name"]
            gps = None
            pos = intake_video.position_at(track, r["time_s"])
            if pos:
                gps = {"lat": pos[0], "lon": pos[1], "alt": None, "heading": None, "accuracy_m": None,
                       "source": "video GPS track"}
            items.append({
                "name": "views/" + r["name"], "kind": "360_view", "raw": vr, "input_path": path,
                "sha256": intake_copy.sha256(path), "license": licenses.get(vr, "unknown"),
                "time": start_iso, "t": (start_t + r["time_s"]) if start_t is not None else r["time_s"],
                "gps": gps, "camera": {"width": r["size"], "height": r["size"], "focal_35mm": 18.0,
                                       "projection": "perspective view of equirectangular video"},
                "view": {"video": vr, "yaw": r["yaw"], "pitch": r["pitch"], "fov": r["fov"],
                         "frame_time_s": r["time_s"], "frame_index": r["frame_index"]},
                "size": path.stat().st_size,
            })
    return items, new_files, notes


# ---- steps 4 and 5: per-image work ----------------------------------------

def make_pool_runner(jobs, model_dir):
    """Returns run(fn, tasks, need_models) that maps fn over tasks in worker processes."""
    def run(fn, tasks, need_models=True):
        if not tasks:
            return []
        if jobs <= 1:
            intake_work.init_worker(model_dir, need_models)
            return [fn(t) for t in tasks]
        with ProcessPoolExecutor(max_workers=min(jobs, len(tasks)), initializer=intake_work.init_worker,
                                 initargs=(model_dir, need_models)) as ex:
            return list(ex.map(fn, tasks, chunksize=1))
    return run


def process_images(items, out_dir, args, pool_run, prev):
    """Blur, mask and measure every item (skipping ones done before). Returns (new files, errors, reused)."""
    key = f"rules{intake_privacy.VERSION}-q1-{'noblur' if args.skip_blur else 'blur'}"
    tasks, by_name, reused = [], {}, 0
    for it in items:
        it["params_key"] = key
        on = out_name(it["name"])
        it["blurred"] = None if args.skip_blur else (out_dir / "blurred" / on)
        it["mask"] = None if args.skip_blur else (out_dir / "masks" / Path(on).with_suffix(".png"))
        old = prev.get(it["name"])
        done = old and old.get("sha256") == it["sha256"] and old.get("params_key") == key and \
            (args.skip_blur or (it["blurred"].exists() and it["mask"].exists()))
        if done:
            q = dict(old.get("quality") or {})
            q.pop("flags", None)
            q.pop("duplicate_of", None)
            it["quality"] = q
            if old.get("privacy"):
                it["privacy"] = old["privacy"]
            reused += 1
            continue
        by_name[it["name"]] = it
        tasks.append({"name": it["name"], "input": str(it["input_path"]), "skip_blur": args.skip_blur,
                      "blurred": str(it["blurred"]) if it["blurred"] else None,
                      "mask": str(it["mask"]) if it["mask"] else None, "view": it.get("view")})
    if tasks:
        log(f"  processing {len(tasks)} image(s) with {args.jobs} job(s); {reused} already done")
    results = pool_run(intake_work.process_image, tasks, not args.skip_blur)
    new_files, errors = [], []
    for res in results:
        it = by_name[res["name"]]
        it["quality"] = res.get("quality") or {}
        if res.get("error"):
            errors.append(f"{res['name']}: {res['error']}")
            continue
        it["seconds"] = res.get("seconds")
        if res.get("privacy") is not None:
            it["privacy"] = res["privacy"]
            new_files += [it["blurred"], it["mask"]]
    return new_files, errors, reused


# ---- step 6: outputs -----------------------------------------------------

def clean_record(it, root):
    """The JSON form of one item (archive-relative paths only)."""
    keep = ["name", "kind", "raw", "raw_member", "raw_pair", "sha256", "size", "license", "time", "t",
            "gps", "campus", "camera", "provider", "view", "area", "quality", "privacy", "params_key", "seconds"]
    out = {k: it.get(k) for k in keep if it.get(k) is not None}
    out["input"] = rel(it["input_path"], root)
    if it.get("blurred") and Path(it["blurred"]).exists():
        out.setdefault("privacy", {})
        out["privacy"]["blurred"] = rel(it["blurred"], root)
        out["privacy"]["mask"] = rel(it["mask"], root)
    if "area" not in out:
        out["area"] = None
    return out


def area_counts(items, date):
    """Per target id: photos, usable, flagged, last capture date, target info."""
    counts = {}
    for it in items:
        a = it.get("area")
        if not a:
            continue
        c = counts.setdefault(a["id"], {"photos": 0, "usable": 0, "flagged": 0, "last_capture": None,
                                        "info": {k: a.get(k) for k in ("kind", "shot_kind", "building", "facade", "priority",
                                                                       "name", "min_photos")}})
        c["photos"] += 1
        if it["quality"].get("flags"):
            c["flagged"] += 1
        else:
            c["usable"] += 1
        d = (it.get("time") or date)[:10]
        c["last_capture"] = max(c["last_capture"] or d, d)
    return counts


def build_report(set_name, date, device, items, counts, copy_summary, notes, timings, quality_summary,
                 target_source, detectors, errors, reused, filled):
    flags = {}
    for it in items:
        for f in it["quality"].get("flags", []):
            flags[f] = flags.get(f, 0) + 1
    usable = sum(1 for it in items if not it["quality"].get("flags"))
    det = {"faces": 0, "plates": 0, "persons": 0, "vehicles": 0, "images_with_detections": 0}
    for it in items:
        p = it.get("privacy")
        if p:
            for k in ("faces", "plates", "persons", "vehicles"):
                det[k] += p.get(k, 0)
            det["images_with_detections"] += 1 if p.get("blur_boxes") else 0
    areas = []
    for tid, c in sorted(counts.items(), key=lambda kv: -kv[1]["photos"]):
        status, reason = intake_coverage.status_for(c["photos"], c["usable"], c["flagged"],
                                                    int(c["info"].get("min_photos") or 12))
        areas.append({"id": tid, **{k: c["info"].get(k) for k in ("building", "facade", "name")},
                      "photos": c["photos"], "usable": c["usable"], "flagged": c["flagged"],
                      "status_from_this_set": status, "reason": reason})
    reshoot = [a for a in areas if a["status_from_this_set"] == "needs_reshoot"]
    flagged_photos = [{"name": it["name"], "flags": it["quality"]["flags"],
                       "area": (it.get("area") or {}).get("id")} for it in items if it["quality"].get("flags")]
    return {
        "version": 1, "set": set_name, "date": date, "device": device,
        "made": datetime.datetime.now().isoformat(timespec="seconds"), "script": "pipeline/intake.py",
        "originals": copy_summary,
        "captured": {"total": len(items), "photos": sum(1 for i in items if i["kind"] == "photo"),
                     "views_360": sum(1 for i in items if i["kind"] == "360_view"),
                     "with_gps": sum(1 for i in items if i.get("campus")),
                     "assigned": sum(1 for i in items if i.get("area")),
                     "assigned_by_time": filled,
                     "unassigned": sum(1 for i in items if not i.get("area")),
                     "usable": usable, "flagged": len(items) - usable, "reused_from_last_run": reused},
        "flagged_by_reason": dict(sorted(flags.items())),
        "quality": quality_summary, "quality_params": intake_quality.PARAMS,
        "detections": det, "detectors": detectors,
        "targets": target_source, "areas": areas,
        "reshoot": {"areas": reshoot, "flagged_photos": flagged_photos},
        "notes": notes, "errors": errors, "timings_s": timings, "sharing": SHAREABLE,
    }


def print_report(rep):
    c = rep["captured"]
    log("")
    log(f"Intake report: {rep['set']}")
    o = rep["originals"]
    log(f"  originals: {o['copied']} copied, {o['copied_renamed']} copied with hash suffix, "
        f"{o['skipped_same']} already there, {o['in_place']} used in place")
    log(f"  captured: {c['total']} images ({c['photos']} photos, {c['views_360']} 360 views); "
        f"{c['with_gps']} with GPS; {c['usable']} usable, {c['flagged']} flagged")
    if rep["flagged_by_reason"]:
        log("  flagged: " + ", ".join(f"{k} {v}" for k, v in rep["flagged_by_reason"].items()))
    d = rep["detections"]
    log(f"  privacy: {d['faces']} faces, {d['plates']} plates, {d['persons']} people, "
        f"{d['vehicles']} vehicles; {d['images_with_detections']} images had something blurred")
    log(f"  areas ({rep['targets']}): {c['assigned']} assigned ({c['assigned_by_time']} by time), "
        f"{c['unassigned']} unassigned")
    for a in rep["areas"][:12]:
        label = a.get("name") or a.get("building") or ""
        log(f"    {a['id']:<20} {label[:28]:<28} {a['photos']:>4} photos, {a['usable']:>4} usable"
            f"  -> {a['status_from_this_set']}")
    if len(rep["areas"]) > 12:
        log(f"    ... {len(rep['areas']) - 12} more in intake_report.json")
    if rep["reshoot"]["areas"]:
        log("  reshoot: " + "; ".join(f"{a['id']} ({a['reason']})" for a in rep["reshoot"]["areas"][:8]))
    for n in rep["notes"]:
        log("  note: " + n)
    for e in rep["errors"][:10]:
        log("  error: " + e)
    log("  time: " + ", ".join(f"{k} {v:.1f}s" for k, v in rep["timings_s"].items()))


def run(args):
    t_all = time.time()
    timings = {}
    root = data_root()
    src = Path(args.source).resolve()
    if not src.is_dir():
        fail(f"{src} is not a folder")
    args.exiftool = args.exiftool or local_config().get("exiftool") or "exiftool"
    if not args.skip_blur and not args.dry_run:
        missing = intake_privacy.check_models(args.models)
        if missing:
            fail(f"detector models missing in {args.models or intake_privacy.default_model_dir()}: {missing}. "
                 "See Tools/VanderbiltCampus/README.md (capture intake).")
    files = intake_copy.scan(src)
    if not files:
        fail(f"no files in {src}")
    db = manifest.connect(root)

    t = time.time()
    raw_dir, out_dir, set_name, date, device, pairs, licenses, copy_summary = \
        take_originals(args, root, db, files, src)
    timings["originals"] = time.time() - t
    log(f"set {set_name}: {len(pairs)} original file(s); outputs in {out_dir}")

    t = time.time()
    items, new_files, notes = photo_items(pairs, root, out_dir, licenses, args.dry_run)
    if not args.dry_run:
        read_metadata(items, args.exiftool, raw_dir)
    timings["metadata"] = time.time() - t

    pool_run = make_pool_runner(args.jobs, args.models)
    t = time.time()
    vitems, vfiles, vnotes = video_items(pairs, root, out_dir, licenses, args, pool_run)
    items += vitems
    new_files += vfiles
    notes += vnotes
    timings["video_360"] = time.time() - t

    if args.dry_run:
        n_photo = sum(1 for i in items if i["kind"] == "photo")
        log(f"dry run: {copy_summary}; {n_photo} photo(s) would be processed")
        for n in notes:
            log("  note: " + n)
        db.close()
        return None

    t = time.time()
    intake_meta.add_campus(items)
    targets, target_source = intake_areas.load_targets(args.shot_list, args.footprints)
    assigner = intake_areas.Assigner(targets)
    for it in items:
        c = it.get("campus")
        if c:
            a = assigner.assign(c["X"], c["Y"], c.get("heading_deg"))
            it["area"] = dict(a, by="gps") if a else None
    filled = intake_areas.fill_by_time(items)
    timings["areas"] = time.time() - t

    t = time.time()
    photos_json = out_dir / "photos.json"
    prev = {}
    if photos_json.exists():
        prev = {p["name"]: p for p in load_json(photos_json).get("photos", [])}
    pfiles, errors, reused = process_images(items, out_dir, args, pool_run, prev)
    new_files += pfiles
    timings["blur_and_quality"] = time.time() - t

    t = time.time()
    exif_pairs = [(it["input_path"], it["blurred"]) for it in items
                  if it["kind"] == "photo" and it.get("blurred") in pfiles]
    bad = intake_privacy.copy_exif(exif_pairs, args.exiftool)
    if bad:
        errors.append(f"exiftool could not copy metadata to {bad} blurred file(s)")
    timings["exif_copy"] = time.time() - t

    quality_summary = intake_quality.flag_set(items)
    detectors = intake_privacy.detector_info(args.models) if not args.skip_blur else {"skipped": True}
    counts = area_counts(items, date)
    timings["total"] = time.time() - t_all
    report = build_report(set_name, date, device, items, counts, copy_summary, notes, timings,
                          quality_summary, target_source, detectors, errors, reused, filled)
    items.sort(key=lambda it: it["name"])
    save_json(photos_json, {
        "version": 1, "set": set_name, "date": date, "device": device,
        "made": report["made"], "script": "pipeline/intake.py", "targets": target_source,
        "detectors": detectors, "quality_params": intake_quality.PARAMS, "quality": quality_summary,
        "frame": "campus frame v1 (config/frame.json); heading_deg = campus grid degrees, 0 = +Y, clockwise",
        "photos": [clean_record(it, root) for it in items],
    })
    report_path = out_dir / "intake_report.json"
    save_json(report_path, report)

    # Manifest: every derived file written this run, in one transaction.
    t = time.time()
    lic_of = {}
    for it in items:
        lic_of[str(it["input_path"])] = it.get("license")
        if it.get("blurred"):
            lic_of[str(it["blurred"])] = it.get("license")
            lic_of[str(it["mask"])] = it.get("license")
    for f in new_files + [photos_json, report_path]:
        f = Path(f)
        if f.exists():
            lic = lic_of.get(str(f))
            manifest.add_file(f, source="pipeline/intake.py", license=(lic + " (derived)") if lic else
                              "derived from the set's originals", stage=2, root=root, db=db)
    db.commit()
    db.close()
    timings["manifest"] = time.time() - t

    all_targets = [tg.info() for tg in targets] if target_source == "shot_list" else None
    intake_coverage.update(args.coverage, set_name, counts, all_targets, target_source)
    print_report(report)
    log(f"wrote {photos_json}\nwrote {report_path}\nupdated {args.coverage}")
    if errors:
        fail(f"{len(errors)} error(s); see {report_path}")
    return report


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("source", help="folder with the day's files (or a set folder already in raw/capture/)")
    p.add_argument("--date", help="capture date YYYY-MM-DD (default: earliest photo date)")
    p.add_argument("--device", help="short device name, e.g. iphone, pixel, x5 (default: camera)")
    p.add_argument("--set", help="set name for the coverage file (default: <date>/<device>)")
    p.add_argument("--license", help=f"license for copied files (default: {OWN_LICENSE!r})")
    p.add_argument("--dry-run", action="store_true", help="show what would happen; write nothing")
    p.add_argument("--skip-blur", action="store_true", help="no face/plate blur or masks (metadata and flags only)")
    p.add_argument("--jobs", type=int, default=8, help="worker processes for blur and 360 splitting (default 8)")
    p.add_argument("--views", type=int, default=8, choices=[4, 8], help="horizontal views per 360 frame")
    p.add_argument("--up-row", action="store_true", help="also cut 4 views tilted 35 degrees up (roofs)")
    p.add_argument("--max-video-seconds", type=float, help="only use the first N seconds of each video")
    p.add_argument("--models", help="detector model folder (default: ~/VanderbiltCampusTools/models)")
    p.add_argument("--shot-list", default=str(CONFIG / "shot_list.json"), help=argparse.SUPPRESS)
    p.add_argument("--footprints", help=argparse.SUPPRESS)
    p.add_argument("--coverage", default=str(CONFIG / "coverage_status.json"), help=argparse.SUPPRESS)
    p.add_argument("--exiftool", help=argparse.SUPPRESS)
    args = p.parse_args(argv)
    if args.footprints is None:
        args.footprints = str(data_root() / "derived" / "buildings" / "v1" / "footprints.json")
    if args.jobs < 1:
        fail("--jobs must be at least 1")
    try:
        run(args)
    except SystemExit:
        raise
    except Exception as e:                          # any crash -> non-zero exit with the reason
        import traceback
        traceback.print_exc()
        fail(f"intake failed: {e}")


if __name__ == "__main__":
    main()
