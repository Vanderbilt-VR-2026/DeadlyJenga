"""Dense reconstruction of one building from an aligned capture set (COLMAP MVS, NVIDIA GPU).

Written for a machine with an NVIDIA GPU and a CUDA build of COLMAP (ACCRE or a cloud GPU).
It is NOT run on Evan's laptop (no NVIDIA GPU). See gpu/README.md.

    python gpu/dense_colmap.py --set kartaview-2026-11-19 --unit 1.4 --building B_820346890 --dry-run
    python gpu/dense_colmap.py --set <set> --unit <unit> --building <id> --colmap /path/to/colmap

Steps (each one COLMAP command, printed before it runs):
  1. model_transformer   COLMAP model of the unit's component -> campus frame (registration.json),
                         so every dense output is in campus meters
  2. image_undistorter   undistorted copies of only the photos that see the building
                         (sources.json; or all photos of the unit with --all-photos)
  3. patch_match_stereo  depth and normal maps on the GPU (geometric consistency on)
  4. stereo_fusion       fused.ply (dense colored points)
  5. poisson_mesher      meshed-poisson.ply (and delaunay_mesher -> meshed-delaunay.ply)
Output: derived/dense/<set>/v<n>/<building>/ with the outputs above and run.json (commands,
versions, timings). Dense outputs are derived from the photos: same license as the photos
(KartaView: CC BY-SA 4.0).
"""
import argparse
import datetime
import json
import shlex
import subprocess
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
from common import data_root, fail, load_json, local_config  # noqa: E402


def photos_for(root, set_name, version, unit, building, all_photos):
    cams = load_json(root / "derived" / "alignment" / set_name / version / "cameras.json")
    in_unit = [e for e in cams["images"] if e.get("registration_unit") == unit and e.get("campus")
               and e["campus"].get("reliable", True)]
    if all_photos or not building:
        return [e["name"] for e in in_unit]
    src = root / "buildings" / building / "sources.json"
    seen = set()
    if src.exists():
        for f in load_json(src).get("facades", {}).values():
            seen |= {p.split("/", 1)[1] for p in f.get("photos", []) if p.startswith(set_name + "/")}
    # facade layouts also list their photos
    for lay in (root / "buildings" / building / "facades").glob("*/layout_v*.json"):
        seen |= {p.split("/", 1)[1] for p in load_json(lay)["source"]["photos"] if p.startswith(set_name + "/")}
    return [e["name"] for e in in_unit if e["name"] in seen]


def transform_file(reg, unit, path):
    """COLMAP model_transformer reads a 3x4 [sR | t] matrix (text, 3 rows)."""
    M = np.array(reg["transforms"][unit]["colmap_to_campus_4x4"], float)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        for row in M[:3]:
            f.write(" ".join(f"{v:.12g}" for v in row) + "\n")


def commands(colmap, image_root, sparse_in, work, image_list, max_size, gpu):
    sp_campus = work / "sparse_campus"
    dense = work / "dense"
    q = lambda p: str(p)  # noqa: E731
    return [
        ("model_transformer", [colmap, "model_transformer", "--input_path", q(sparse_in), "--output_path", q(sp_campus),
                               "--transform_path", q(work / "colmap_to_campus.txt")]),
        ("image_undistorter", [colmap, "image_undistorter", "--image_path", q(image_root), "--input_path", q(sp_campus),
                               "--output_path", q(dense), "--output_type", "COLMAP", "--image_list_path", q(image_list),
                               "--max_image_size", str(max_size)]),
        ("patch_match_stereo", [colmap, "patch_match_stereo", "--workspace_path", q(dense), "--workspace_format", "COLMAP",
                                "--PatchMatchStereo.geom_consistency", "1", "--PatchMatchStereo.gpu_index", gpu,
                                "--PatchMatchStereo.max_image_size", str(max_size)]),
        ("stereo_fusion", [colmap, "stereo_fusion", "--workspace_path", q(dense), "--workspace_format", "COLMAP",
                           "--input_type", "geometric", "--output_path", q(dense / "fused.ply"),
                           "--StereoFusion.min_num_pixels", "3", "--StereoFusion.max_reproj_error", "2"]),
        ("poisson_mesher", [colmap, "poisson_mesher", "--input_path", q(dense / "fused.ply"),
                            "--output_path", q(dense / "meshed-poisson.ply")]),
        ("delaunay_mesher", [colmap, "delaunay_mesher", "--input_path", q(dense), "--input_type", "dense",
                             "--output_path", q(dense / "meshed-delaunay.ply")]),
    ]


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--set", required=True)
    ap.add_argument("--version", default="v1", help="alignment version")
    ap.add_argument("--unit", required=True, help="registration unit (registration.json 'units', e.g. 1.4 or 2)")
    ap.add_argument("--building", help="only photos that see this building (sources.json / facade layouts)")
    ap.add_argument("--all-photos", action="store_true", help="use every reliable photo of the unit")
    ap.add_argument("--out-version", default="v1", help="derived/dense/<set>/<out-version>/")
    ap.add_argument("--colmap", help="CUDA COLMAP executable (default: 'colmap' key of config/local.json, else 'colmap')")
    ap.add_argument("--max-size", type=int, default=3200, help="long image side for MVS")
    ap.add_argument("--gpu", default="0", help="GPU index list for patch_match_stereo, e.g. 0 or 0,1")
    ap.add_argument("--dry-run", action="store_true", help="print the commands; run nothing")
    args = ap.parse_args(argv)

    root = data_root()
    aln = root / "derived" / "alignment" / args.set / args.version
    reg = load_json(aln / "registration.json")
    if args.unit not in reg.get("transforms", {}):
        fail(f"unit {args.unit} is not registered (units: {list(reg.get('transforms', {}))})")
    component = args.unit.split(".")[0]
    cams = load_json(aln / "cameras.json")
    image_root = root / cams["image_root"]
    names = photos_for(root, args.set, args.version, args.unit, args.building, args.all_photos)
    if len(names) < 3:
        fail(f"only {len(names)} photos of unit {args.unit} see {args.building}; MVS needs at least 3")
    tag = args.building or f"unit{args.unit}"
    work = root / "derived" / "dense" / args.set / args.out_version / tag
    try:
        colmap = args.colmap or local_config().get("colmap") or "colmap"
    except SystemExit:
        colmap = args.colmap or "colmap"
    cmds = commands(colmap, image_root, aln / "sparse" / component, work, work / "images.txt", args.max_size, args.gpu)
    print(f"{len(names)} photos -> {work}")
    for name, c in cmds:
        print(f"[{name}] " + " ".join(shlex.quote(x) for x in c))
    if args.dry_run:
        return 0
    work.mkdir(parents=True, exist_ok=True)
    (work / "images.txt").write_text("\n".join(names) + "\n", encoding="utf-8")
    transform_file(reg, args.unit, work / "colmap_to_campus.txt")
    log = {"set": args.set, "alignment": f"{args.version}", "unit": args.unit, "building": args.building,
           "photos": names, "started": datetime.datetime.now().isoformat(timespec="seconds"), "steps": [],
           "license": "derived from the capture photos (KartaView: CC BY-SA 4.0)",
           "note": "the unit's similarity transform is applied to the whole component model; only the unit's photos "
                   "are undistorted, so the dense result is in the campus frame"}
    for name, c in cmds:
        t = time.time()
        r = subprocess.run(c)
        log["steps"].append({"step": name, "command": c, "returncode": r.returncode, "seconds": round(time.time() - t, 1)})
        (work / "run.json").write_text(json.dumps(log, indent=1), encoding="utf-8")
        if r.returncode != 0:
            fail(f"{name} failed (exit {r.returncode}); see {work / 'run.json'}")
    import manifest
    db = manifest.connect(root)
    for f in [work / "dense" / "fused.ply", work / "dense" / "meshed-poisson.ply", work / "dense" / "meshed-delaunay.ply",
              work / "run.json"]:
        if f.exists():
            manifest.add_file(f, source="gpu/dense_colmap.py", license=log["license"], stage=3, root=root, db=db)
    db.commit()
    db.close()
    print(f"done: {work / 'dense' / 'fused.ply'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
