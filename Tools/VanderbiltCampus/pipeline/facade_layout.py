"""Facade layouts (facade_layout.json): where the windows, doors and trim sit on one wall.

One file describes one facade of one building, in that facade's own 2D frame:
  plane.origin   the facade's lower-left corner seen from outside (campus meters)
  plane.u        unit vector left to right along the wall (horizontal)
  plane.v        unit vector up, always [0, 0, 1]
  plane.normal   unit vector out of the wall (u x v)
  width_m, height_m   size of the facade rectangle
Each feature has rect [u0, v0, u1, v1] (meters in that frame), an optional outline,
an arch type, depth_m (+ sticks out, - recess), confidence, reviewed flag and source.

Stored in the archive at buildings/<id>/facades/<facade>/layout_v<n>.json.
A layout is never overwritten: saving writes the next version number.

  python pipeline/facade_layout.py facades B_544397715          # list derived facade planes
  python pipeline/facade_layout.py check  path/to/layout_v1.json # validate a file
"""
import argparse
import json
import math
import re
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import data_root, fail, load_json  # noqa: E402

FEATURE_TYPES = ("window", "door", "column", "pilaster", "cornice", "string_course", "arch", "buttress",
                 "dormer", "chimney", "balustrade", "steps", "recess", "projection")
ARCH_TYPES = ("none", "round", "pointed", "segmental")
SOURCES = ("detected", "manual", "estimated", "synthetic")
TOP_KEYS = ("building", "facade", "version", "reviewed", "plane", "source", "features")
PLANE_KEYS = ("origin", "u", "v", "normal", "width_m", "height_m")
SOURCE_KEYS = ("alignment_set", "photos", "rectified_image", "px_per_m")
FEATURE_KEYS = ("id", "type", "rect", "outline", "arch", "depth_m", "confidence", "reviewed", "source")
MIN_RUN_M = 2.0          # wall runs shorter than this get no facade
RUN_ANGLE_DEG = 8.0      # consecutive edges turning less than this are one straight run


# ---- validation ---------------------------------------------------------------------

def _vec(x, n=3):
    return isinstance(x, (list, tuple)) and len(x) == n and all(isinstance(c, (int, float)) for c in x)


def validate(layout, tol=1e-3):
    """Return a list of problems (empty = valid)."""
    p = []
    if not isinstance(layout, dict):
        return ["layout is not an object"]
    for k in TOP_KEYS:
        if k not in layout:
            p.append(f"missing key {k}")
    if p:
        return p
    extra = set(layout) - set(TOP_KEYS)
    if extra:
        p.append(f"unknown keys {sorted(extra)}")
    if not re.fullmatch(r"B_\w+", str(layout["building"])):
        p.append("building must look like B_<id>")
    if not re.fullmatch(r"F\d{2,}", str(layout["facade"])):
        p.append("facade must look like F03")
    if not isinstance(layout["version"], int) or layout["version"] < 1:
        p.append("version must be an integer >= 1")
    if not isinstance(layout["reviewed"], bool):
        p.append("reviewed must be true/false")
    pl = layout["plane"]
    if not isinstance(pl, dict) or set(pl) != set(PLANE_KEYS):
        p.append(f"plane needs exactly {PLANE_KEYS}")
        return p
    if not all(_vec(pl[k]) for k in ("origin", "u", "v", "normal")):
        return p + ["plane vectors must be 3 numbers"]
    u, v, n = (np.asarray(pl[k], float) for k in ("u", "v", "normal"))
    if abs(np.linalg.norm(u) - 1) > tol or abs(u[2]) > tol:
        p.append("u must be a horizontal unit vector")
    if np.abs(v - [0, 0, 1]).max() > tol:
        p.append("v must be [0, 0, 1]")
    if np.abs(n - np.cross(u, v)).max() > 10 * tol:
        p.append("normal must equal u x v (pointing out of the wall)")
    W, H = pl["width_m"], pl["height_m"]
    if not (isinstance(W, (int, float)) and W > 0 and isinstance(H, (int, float)) and H > 0):
        p.append("width_m and height_m must be positive")
        W = H = 0
    src = layout["source"]
    if not isinstance(src, dict) or set(src) != set(SOURCE_KEYS):
        p.append(f"source needs exactly {SOURCE_KEYS}")
    else:
        if not isinstance(src["photos"], list):
            p.append("source.photos must be a list")
        if src["px_per_m"] is not None and not (isinstance(src["px_per_m"], (int, float)) and src["px_per_m"] > 0):
            p.append("source.px_per_m must be null or positive")
    ids = set()
    if not isinstance(layout["features"], list):
        return p + ["features must be a list"]
    for i, f in enumerate(layout["features"]):
        where = f"feature {i} ({f.get('id') if isinstance(f, dict) else '?'})"
        if not isinstance(f, dict) or set(f) != set(FEATURE_KEYS):
            p.append(f"{where}: needs exactly {FEATURE_KEYS}")
            continue
        if f["id"] in ids:
            p.append(f"{where}: duplicate id")
        ids.add(f["id"])
        if f["type"] not in FEATURE_TYPES:
            p.append(f"{where}: unknown type {f['type']}")
        if f["arch"] not in ARCH_TYPES:
            p.append(f"{where}: unknown arch {f['arch']}")
        if f["source"] not in SOURCES:
            p.append(f"{where}: unknown source {f['source']}")
        r = f["rect"]
        if not _vec(r, 4) or r[2] <= r[0] or r[3] <= r[1]:
            p.append(f"{where}: rect must be [u0, v0, u1, v1] with u1 > u0 and v1 > v0")
        elif W and (r[0] < -tol or r[2] > W + tol or r[3] > H + tol):
            p.append(f"{where}: rect outside the facade")
        if f["outline"] is not None:
            if not isinstance(f["outline"], list) or len(f["outline"]) < 3 or not all(_vec(q, 2) for q in f["outline"]):
                p.append(f"{where}: outline must be null or 3+ [u, v] points")
        if f["depth_m"] is not None and not isinstance(f["depth_m"], (int, float)):
            p.append(f"{where}: depth_m must be a number or null")
        c = f["confidence"]
        if not isinstance(c, (int, float)) or not 0 <= c <= 1:
            p.append(f"{where}: confidence must be 0..1")
        if not isinstance(f["reviewed"], bool):
            p.append(f"{where}: reviewed must be true/false")
    return p


def new_feature(fid, ftype, rect, arch="none", depth_m=None, confidence=0.5, source="estimated",
                outline=None, reviewed=False):
    """A feature dict with every key set (rounded to 1 mm)."""
    return {"id": fid, "type": ftype, "rect": [round(float(x), 3) for x in rect],
            "outline": outline, "arch": arch, "depth_m": None if depth_m is None else round(float(depth_m), 3),
            "confidence": float(confidence), "reviewed": bool(reviewed), "source": source}


def new_layout(building, facade, plane, features=(), source=None, version=1, reviewed=False):
    src = {"alignment_set": None, "photos": [], "rectified_image": None, "px_per_m": None}
    src.update(source or {})
    return {"building": building, "facade": facade, "version": int(version), "reviewed": bool(reviewed),
            "plane": plane, "source": src, "features": list(features)}


# ---- files --------------------------------------------------------------------------

def facade_dir(building_id, facade, root=None):
    return Path(root or data_root()) / "buildings" / building_id / "facades" / facade


def versions(building_id, facade, root=None):
    """Existing layout version numbers for one facade, ascending."""
    d = facade_dir(building_id, facade, root)
    out = []
    for f in d.glob("layout_v*.json") if d.exists() else []:
        m = re.fullmatch(r"layout_v(\d+)\.json", f.name)
        if m:
            out.append(int(m.group(1)))
    return sorted(out)


def load(path):
    """Read and validate one layout file. Raises ValueError if it is not valid."""
    layout = load_json(path)
    problems = validate(layout)
    if problems:
        raise ValueError(f"{path}: " + "; ".join(problems))
    return layout


def load_latest(building_id, facade, root=None):
    """The newest layout of a facade, or None."""
    v = versions(building_id, facade, root)
    if not v:
        return None
    return load(facade_dir(building_id, facade, root) / f"layout_v{v[-1]}.json")


def load_building(building_id, root=None):
    """{facade: newest layout} for every facade folder of a building."""
    base = Path(root or data_root()) / "buildings" / building_id / "facades"
    out = {}
    for d in sorted(base.iterdir()) if base.exists() else []:
        lay = load_latest(building_id, d.name, root)
        if lay:
            out[d.name] = lay
    return out


def save(layout, root=None, source="facade_layout.py", license="derived", stage=4, record=True):
    """Validate and write as the next version (never overwrites). Returns the path.
    The layout's "version" field is set to the number actually written."""
    root = Path(root or data_root())
    v = versions(layout["building"], layout["facade"], root)
    layout = dict(layout)
    layout["version"] = (v[-1] + 1) if v else 1
    problems = validate(layout)
    if problems:
        raise ValueError("layout not valid: " + "; ".join(problems))
    path = facade_dir(layout["building"], layout["facade"], root) / f"layout_v{layout['version']}.json"
    if path.exists():
        raise FileExistsError(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "x", encoding="utf-8", newline="\n") as f:
        json.dump(layout, f, indent=1)
        f.write("\n")
    if record:
        import manifest
        manifest.add_file(path, source, license, stage=stage, root=root)
    return path


# ---- geometry -----------------------------------------------------------------------

def to_world(layout_or_plane, u, v, depth=0.0):
    """Facade (u, v, depth) -> campus (X, Y, Z). depth is along the outward normal.
    Works on arrays too (returns shape (..., 3))."""
    pl = layout_or_plane.get("plane", layout_or_plane)
    o, eu, ev, en = (np.asarray(pl[k], float) for k in ("origin", "u", "v", "normal"))
    u, v, d = np.asarray(u, float), np.asarray(v, float), np.asarray(depth, float)
    return o + u[..., None] * eu + v[..., None] * ev + d[..., None] * en


def to_facade(layout_or_plane, xyz):
    """Campus points -> (u, v, depth) in the facade frame."""
    pl = layout_or_plane.get("plane", layout_or_plane)
    o, eu, ev, en = (np.asarray(pl[k], float) for k in ("origin", "u", "v", "normal"))
    d = np.asarray(xyz, float) - o
    return np.stack([d @ eu, d @ ev, d @ en], axis=-1)


def _signed_area(p):
    x, y = p[:, 0], p[:, 1]
    return 0.5 * float(np.sum(x * np.roll(y, -1) - np.roll(x, -1) * y))


def wall_runs(footprint, min_run=MIN_RUN_M, angle_deg=RUN_ANGLE_DEG):
    """Straight wall runs of a footprint ring, clockwise seen from above, starting at the
    north-west-most corner (largest y - x). Returns a list of (start_xy, end_xy) longer than min_run.
    Consecutive edges that turn less than angle_deg are joined into one run."""
    p = np.asarray(footprint, float)
    if np.allclose(p[0], p[-1]):
        p = p[:-1]
    if _signed_area(p) > 0:          # counter-clockwise -> clockwise
        p = p[::-1]
    # drop corners where the wall goes straight on
    keep = []
    n = len(p)
    for i in range(n):
        a, b, c = p[i - 1], p[i], p[(i + 1) % n]
        d1, d2 = b - a, c - b
        if np.linalg.norm(d1) < 1e-6 or np.linalg.norm(d2) < 1e-6:
            continue
        cos = d1 @ d2 / (np.linalg.norm(d1) * np.linalg.norm(d2))
        if math.degrees(math.acos(max(-1.0, min(1.0, cos)))) >= angle_deg:
            keep.append(i)
    p = p[keep] if len(keep) >= 3 else p
    start = int(np.argmax(p[:, 1] - p[:, 0]))
    p = np.roll(p, -start, axis=0)
    runs = []
    for i in range(len(p)):
        a, b = p[i], p[(i + 1) % len(p)]
        if np.linalg.norm(b - a) > min_run:
            runs.append((a, b))
    return runs


def _wall_top(lod2, a, b, near=1.0):
    """Highest wall-vertex Z along the segment a-b in the LoD2 mesh (None if no wall found).
    Uses the median of the per-face tops so a tower corner does not set the whole facade."""
    v, f = lod2["verts"], lod2["faces"]
    T = v[f]
    nrm = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    ln = np.linalg.norm(nrm, axis=1)
    wall = (ln > 1e-9) & (np.abs(nrm[:, 2]) < 0.1 * np.maximum(ln, 1e-12))
    d = b - a
    L = np.linalg.norm(d)
    t = d / L
    side = np.array([-t[1], t[0]])
    xy = T[:, :, :2] - a
    along = xy @ t
    off = np.abs(xy @ side)
    on = wall & (off.max(axis=1) < near) & (along.min(axis=1) > -near) & (along.max(axis=1) < L + near)
    if not on.any():
        return None, None
    # sample along the run every 0.25 m: the wall top there is the highest wall face covering it
    lo, hi = along[on].min(axis=1), along[on].max(axis=1)
    ztop = T[on][:, :, 2].max(axis=1)
    s = np.arange(0.125, L, 0.25)
    cover = (lo[None, :] <= s[:, None] + 1e-6) & (hi[None, :] >= s[:, None] - 1e-6)
    tops = np.where(cover, ztop[None, :], -np.inf).max(axis=1)
    tops = tops[np.isfinite(tops)]
    if not len(tops):
        return None, None
    return float(np.median(tops)), float(tops.max())


def facades_of(building_id, root=None, min_run=MIN_RUN_M, lod2=None, footprint=None, ground_z=None):
    """Facade planes of a building from its v2 footprint and LoD2 mesh.

    One facade per straight wall run longer than min_run, numbered F01, F02, ... clockwise
    (seen from above) from the north-west-most corner. origin z is the building's ground height
    (v2 model.json ground_z); height_m reaches the wall top (eave) taken from the LoD2 walls on that
    run (median face top; the run's highest wall top is kept in the returned "top_max_z").
    Returns a list of {"facade", "plane", "length_m", "eave_z", "top_max_z"}."""
    root = Path(root or data_root())
    if footprint is None or lod2 is None or ground_z is None:
        bdir = root / "buildings" / building_id / "v2"
        if footprint is None:
            idx = load_json(root / "derived" / "buildings" / "v2" / "index.json")
            rec = next((b for b in idx["buildings"] if b["id"] == building_id), None)
            if rec is None:
                raise KeyError(f"{building_id} is not in derived/buildings/v2/index.json")
            footprint = rec["footprint"]
        if lod2 is None:
            import vcmesh
            lod2 = vcmesh.load_mesh(bdir / "lod2.npz")
        if ground_z is None:
            ground_z = float(load_json(bdir / "model.json")["ground_z"])
    out = []
    for i, (a, b) in enumerate(wall_runs(footprint, min_run)):
        top, top_max = _wall_top(lod2, a, b)
        if top is None:
            top = top_max = float(lod2["verts"][:, 2].max())
        # Clockwise walk: the outside is on the left of a->b. Seen from outside, left-to-right
        # runs from b to a, so the lower-left corner is b.
        u2 = (a - b) / np.linalg.norm(a - b)
        u = np.array([u2[0], u2[1], 0.0])
        v = np.array([0.0, 0.0, 1.0])
        n = np.cross(u, v)
        plane = {"origin": [round(float(b[0]), 3), round(float(b[1]), 3), round(float(ground_z), 3)],
                 "u": [round(float(c), 6) for c in u], "v": [0.0, 0.0, 1.0],
                 "normal": [round(float(c), 6) for c in n],
                 "width_m": round(float(np.linalg.norm(a - b)), 3),
                 "height_m": round(max(top - ground_z, 0.5), 3)}
        out.append({"facade": f"F{i + 1:02d}", "plane": plane, "length_m": plane["width_m"],
                    "eave_z": round(top, 3), "top_max_z": round(top_max, 3)})
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    a = sub.add_parser("facades", help="print the derived facade planes of a building")
    a.add_argument("building")
    c = sub.add_parser("check", help="validate layout files")
    c.add_argument("paths", nargs="+")
    args = ap.parse_args(argv)
    if args.cmd == "facades":
        for fc in facades_of(args.building):
            pl = fc["plane"]
            print(f"{fc['facade']}  width {pl['width_m']:7.2f} m  height {pl['height_m']:6.2f} m  "
                  f"normal ({pl['normal'][0]:+.2f}, {pl['normal'][1]:+.2f})  origin {pl['origin']}")
    else:
        bad = 0
        for path in args.paths:
            try:
                load(path)
                print(f"ok: {path}")
            except ValueError as e:
                bad += 1
                print(f"INVALID: {e}")
        if bad:
            fail(f"{bad} invalid layout(s)")


if __name__ == "__main__":
    main()
