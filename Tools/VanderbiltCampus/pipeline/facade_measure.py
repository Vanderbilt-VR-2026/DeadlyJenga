"""Stage 3 measurement: measure one facade from aligned photos (CPU, classical image methods).

    python facade_measure.py measure --building B_820346890 --facade F01 --units 1.4
    python facade_measure.py check   --building B_820346890 --facade F01 --photos 3

measure
  1. Facade plane: the facade's frame from facade_layout.facades_of() (LoD2 walls from
     buildings/<id>/v2; buildings without a v2 model, such as ring buildings, use their v1
     footprint extruded from ground to eave). The wall is then moved along its normal to the
     median of the registered sparse points that lie on it (only a shift; the wall stays
     vertical and keeps its direction). The shift and point count are logged.
  2. Rectified image: every reliable registered photo that sees the facade (in front of it,
     incidence <= --max-incidence, inside the frame, outside the privacy mask) is resampled
     onto the facade grid (--px-per-m) and the photos are blended, weighted by cos(incidence)^2
     / distance. The image stays in the archive (the photos are CC BY-SA 4.0 street photos).
  3. Features, found with classical methods (OpenCV, no learned model):
       cornice / string_course  long horizontal edge lines (row profile of vertical gradients)
       column                   pairs of strong vertical edge lines running most of the height
       window / door            dark, rectangle-like regions (local threshold, contours); a door
                                touches the ground and is >= 1.8 m tall; similar boxes in a row
                                raise each other's confidence; a rounded top marks an arch
  4. Depth per feature from the sparse points inside its rectangle compared with the wall around
     it (needs 3 points each; otherwise null).
  Output: facade_layout.json through facade_layout.save() (next version, "source": "detected"),
  plus rectified_v<n>.jpg and measure_v<n>.json (plane fit, photos, detector settings) next to it.

check (Stage 3 "done when" 3)
  Draws the newest layout into N registered photos and measures, per feature, how far its
  outline is from the edges visible in the photo: the outline is shifted in the facade plane
  (+-1 m in 2 cm steps) and the shift where it lies on the strongest image edges is taken as
  the offset. Writes overlay images and check_v<n>.json.

Exit code 1 on failure (no photos see the facade, unknown building, ...).
"""
import argparse
import datetime
import json
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import facade_layout as fl  # noqa: E402
from align_sources import project  # noqa: E402
from common import REPO, data_root, fail, load_json, save_json  # noqa: E402

CREDIT = "Photos: Imagery (c) KartaView contributors, CC BY-SA 4.0 (kartaview.org; faces/plates blurred)"


def log(msg):
    print(msg, flush=True)


# ---------------------------------------------------------------------------
# Facade plane
# ---------------------------------------------------------------------------

def box_mesh(ring, z0, z1):
    """A plain extruded footprint (walls only) in the mesh format facades_of() reads."""
    p = np.asarray(ring, float)
    if np.allclose(p[0], p[-1]):
        p = p[:-1]
    n = len(p)
    V = np.vstack([np.c_[p, np.full(n, z0)], np.c_[p, np.full(n, z1)]])
    F = []
    for i in range(n):
        j = (i + 1) % n
        F += [[i, j, n + j], [i, n + j, n + i]]
    return {"verts": V, "faces": np.array(F)}


def facade_plane(root, building, facade):
    """(plane dict, how) for a building facade, using facades_of()."""
    v2 = root / "buildings" / building / "v2" / "lod2.npz"
    if v2.exists():
        facs = fl.facades_of(building, root)
        how = "facade_layout.facades_of (v2 footprint + LoD2 walls)"
    else:
        fps = {b["id"]: b for b in load_json(root / "derived" / "buildings" / "v1" / "footprints.json")["buildings"]}
        b = fps.get(building)
        if b is None:
            fail(f"{building} is not in derived/buildings/v1/footprints.json")
        g = (b.get("ground") or {}).get("ground_z")
        ez = (b.get("roof_estimate") or {}).get("eave_z")
        if g is None or ez is None:
            fail(f"{building} has no ground or eave height")
        ring = np.asarray(b["polygon"][0][0], float)
        facs = fl.facades_of(building, root, footprint=ring, lod2=box_mesh(ring, g, ez), ground_z=g)
        how = ("facade_layout.facades_of with the v1 footprint extruded from ground to eave "
               "(no v2 LoD2 model for this building)")
    for f in facs:
        if f["facade"] == facade:
            return dict(f["plane"]), how
    fail(f"{building} has no facade {facade} (has {[f['facade'] for f in facs]})")


def read_ply_points(path):
    from align_review import read_ply
    a = read_ply(path)
    return np.stack([a["x"], a["y"], a["z"]], 1), a


def fit_plane_offset(plane, pts, max_dist=1.0, margin=0.5):
    """Shift the plane along its normal to the median depth of the points on the wall.
    Returns (new plane, info)."""
    W, H = plane["width_m"], plane["height_m"]
    uvd = fl.to_facade(plane, pts) if len(pts) else np.zeros((0, 3))
    m = ((uvd[:, 0] > -margin) & (uvd[:, 0] < W + margin) & (uvd[:, 1] > -margin) & (uvd[:, 1] < H + 2)
         & (np.abs(uvd[:, 2]) < max_dist)) if len(uvd) else np.zeros(0, bool)
    info = {"points_near_wall": int(m.sum()), "search_m": max_dist}
    if m.sum() < 10:
        info["shift_m"] = 0.0
        info["note"] = "fewer than 10 sparse points on the wall: LoD2 plane kept"
        return plane, info
    d = uvd[m, 2]
    shift = float(np.median(d))
    info["shift_m"] = round(shift, 3)
    info["spread_mad_m"] = round(float(np.median(np.abs(d - shift))), 3)
    new = dict(plane)
    o = np.asarray(plane["origin"], float) + shift * np.asarray(plane["normal"], float)
    new["origin"] = [round(float(c), 3) for c in o]
    return new, info


# ---------------------------------------------------------------------------
# Photos and rectification
# ---------------------------------------------------------------------------

def load_photos(root, set_name, version, units=None, names=None):
    cj = load_json(root / "derived" / "alignment" / set_name / version / "cameras.json")
    out = []
    for e in cj["images"]:
        c = e.get("campus")
        if not c or not e.get("camera") or c.get("reliable") is False:
            continue
        if units and e.get("registration_unit") not in units:
            continue
        if names and not any(e["name"].startswith(n) or e["name"].split("/")[-1].startswith(n) for n in names):
            continue
        R = np.array(c["R_cam_from_campus"])
        C = np.array(c["center"])
        out.append({"name": e["name"], "id": f"{set_name}/{e['name']}", "derived": e["derived"], "mask": e.get("mask"),
                    "cam": e["camera"], "R": R, "C": C, "t": -R @ C, "unit": e.get("registration_unit"),
                    "license": e.get("license")})
    return out


def facade_grid(plane, ppm):
    W, H = plane["width_m"], plane["height_m"]
    nu, nv = max(2, int(round(W * ppm))), max(2, int(round(H * ppm)))
    u = (np.arange(nu) + 0.5) / ppm
    v = H - (np.arange(nv) + 0.5) / ppm            # image row 0 = top of the facade
    U, V = np.meshgrid(u, v)
    return U, V


def rectify_one(root, ph, plane, U, V, max_incidence, ignore_bottom):
    """Resample one photo onto the facade grid. Returns (rgb float image, weight map, info)."""
    import cv2
    P = fl.to_world(plane, U.ravel(), V.ravel())
    nrm = np.asarray(plane["normal"], float)
    view = ph["C"][None] - P
    dist = np.linalg.norm(view, axis=1)
    cosi = (view @ nrm) / np.maximum(dist, 1e-9)
    xy, z = project(ph["cam"], ph["R"], ph["t"], P)
    img = cv2.imread(str(root / ph["derived"]), cv2.IMREAD_COLOR)
    h, w = img.shape[:2]
    sx, sy = w / ph["cam"]["width"], h / ph["cam"]["height"]
    mx = (xy[:, 0] * sx).astype(np.float32).reshape(U.shape)
    my = (xy[:, 1] * sy).astype(np.float32).reshape(U.shape)
    ok = ((z > 0.5) & (cosi > np.cos(np.radians(max_incidence))) & (xy[:, 0] >= 0) & (xy[:, 0] < ph["cam"]["width"] - 1)
          & (xy[:, 1] >= 0) & (xy[:, 1] < ph["cam"]["height"] * (1 - ignore_bottom))).reshape(U.shape)
    rgb = cv2.remap(img, mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT).astype(np.float32)
    if ph.get("mask") and (root / ph["mask"]).exists():
        mk = cv2.imread(str(root / ph["mask"]), cv2.IMREAD_GRAYSCALE)
        mk = cv2.resize(mk, (w, h), interpolation=cv2.INTER_NEAREST) if mk.shape[:2] != (h, w) else mk
        ok &= cv2.remap(mk, mx, my, cv2.INTER_NEAREST) > 127
    wgt = np.where(ok, (np.clip(cosi, 0, 1) ** 2 / np.maximum(dist, 1.0)).reshape(U.shape), 0.0)
    info = {"photo": ph["id"], "unit": ph["unit"], "coverage": round(float(ok.mean()), 3),
            "mean_distance_m": round(float(dist[ok.ravel()].mean()), 2) if ok.any() else None,
            "mean_incidence_deg": round(float(np.degrees(np.arccos(np.clip(cosi[ok.ravel()], -1, 1))).mean()), 1)
            if ok.any() else None}
    return rgb, wgt, info


def blend(stack):
    num = sum(r * w[..., None] for r, w, _ in stack)
    den = sum(w for _, w, _ in stack)
    out = np.where(den[..., None] > 0, num / np.maximum(den[..., None], 1e-12), 0)
    return out.astype(np.uint8), den > 0


# ---------------------------------------------------------------------------
# Feature detection (classical)
# ---------------------------------------------------------------------------

def _peaks(profile, min_sep, thresh):
    """Indices of local maxima above thresh, at least min_sep apart (strongest first)."""
    idx = [i for i in range(1, len(profile) - 1)
           if profile[i] >= thresh and profile[i] >= profile[i - 1] and profile[i] >= profile[i + 1]]
    idx.sort(key=lambda i: -profile[i])
    keep = []
    for i in idx:
        if all(abs(i - j) >= min_sep for j in keep):
            keep.append(i)
    return sorted(keep)


def detect_features(rgb, valid, ppm, W, H, params=None):
    """Find facade features in a rectified image. Returns a list of dicts
    {type, rect [u0, v0, u1, v1] (m, v up from the ground), arch, confidence, why}."""
    import cv2
    p = {"line_run": 0.55, "line_sigma": 2.0, "col_height": 0.5, "win_min_w": 0.5, "win_max_w": 4.0,
         "win_min_h": 0.6, "win_max_h": 3.5, "fill_min": 0.55, "door_min_h": 1.8, "block_m": 2.0, "C": 6}
    p.update(params or {})
    gray = cv2.cvtColor(rgb, cv2.COLOR_BGR2GRAY).astype(np.float32)
    nv, nu = gray.shape
    vm = valid.astype(np.float32)
    gx = np.abs(cv2.Sobel(gray, cv2.CV_32F, 1, 0, ksize=3)) * vm
    gy = np.abs(cv2.Sobel(gray, cv2.CV_32F, 0, 1, ksize=3)) * vm
    feats = []

    def to_rect(r0, r1, c0, c1):
        # image rows grow downward from the facade top; v grows upward from the ground
        return [c0 / ppm, H - r1 / ppm, c1 / ppm, H - r0 / ppm]

    # Horizontal lines: rows where strong vertical gradients run along most of the valid width.
    thr = np.percentile(gy[valid], 90) if valid.any() else 1
    run = ((gy > thr).sum(1)) / np.maximum(valid.sum(1), 1)
    prof = gy.sum(1) / np.maximum(valid.sum(1), 1)
    rows = [r for r in _peaks(prof, int(0.4 * ppm), prof.mean() + p["line_sigma"] * prof.std())
            if run[r] >= p["line_run"] * 0.5 and valid[r].mean() > 0.3]
    for r in rows:
        v = H - r / ppm
        if v < 0.3:
            continue
        ftype = "cornice" if v > 0.75 * H else "string_course"
        cols = np.where(valid[r])[0]
        th = max(0.08, 3 / ppm)
        feats.append({"type": ftype, "rect": [cols.min() / ppm, max(0, v - th), (cols.max() + 1) / ppm, min(H, v + th)],
                      "arch": "none", "confidence": round(float(min(0.9, 0.4 + run[r])), 2),
                      "why": f"horizontal edge line over {run[r]:.0%} of the row"})

    # Vertical lines -> columns: pairs of strong column-profile peaks 0.15-1.0 m apart running most of the height.
    vcov = valid.sum(0) / nv
    cprof = gx.sum(0) / np.maximum(valid.sum(0), 1)
    thrx = np.percentile(gx[valid], 90) if valid.any() else 1
    crun = ((gx > thrx).sum(0)) / np.maximum(valid.sum(0), 1)
    cpk = [c for c in _peaks(cprof, int(0.1 * ppm), cprof.mean() + 2 * cprof.std())
           if crun[c] >= p["col_height"] and vcov[c] > 0.5]
    used = set()
    for a, b in zip(cpk[:-1], cpk[1:]):
        if a in used or not (0.15 * ppm <= b - a <= 1.0 * ppm):
            continue
        used |= {a, b}
        rr = np.where(valid[:, a] & valid[:, b])[0]
        if len(rr) < 0.5 * nv:
            continue
        feats.append({"type": "column", "rect": to_rect(rr.min(), rr.max() + 1, a, b + 1), "arch": "none",
                      "confidence": round(float(min(0.85, 0.3 + 0.5 * min(crun[a], crun[b]))), 2),
                      "why": "pair of vertical edge lines over most of the height"})

    # Dark rectangle-like regions -> windows and doors.
    block = int(p["block_m"] * ppm) | 1
    g8 = np.clip(gray, 0, 255).astype(np.uint8)
    fill = int(np.median(g8[valid])) if valid.any() else 128
    g8 = np.where(valid, g8, fill).astype(np.uint8)
    dark = cv2.adaptiveThreshold(g8, 255, cv2.ADAPTIVE_THRESH_MEAN_C, cv2.THRESH_BINARY_INV, block, p["C"])
    dark[~valid] = 0
    k = max(3, int(0.12 * ppm)) | 1
    dark = cv2.morphologyEx(dark, cv2.MORPH_OPEN, np.ones((k, k), np.uint8))
    dark = cv2.morphologyEx(dark, cv2.MORPH_CLOSE, np.ones((k, k), np.uint8))
    n, lab, stats, _ = cv2.connectedComponentsWithStats(dark, 8)
    boxes = []
    for i in range(1, n):
        x, y, w, h, area = stats[i]
        wm, hm = w / ppm, h / ppm
        if not (p["win_min_w"] <= wm <= p["win_max_w"] and p["win_min_h"] <= hm <= p["win_max_h"]):
            continue
        fr = area / float(w * h)
        if fr < p["fill_min"]:
            continue
        if valid[y:y + h, x:x + w].mean() < 0.8:
            continue
        rect = to_rect(y, y + h, x, x + w)
        sub = lab[y:y + h, x:x + w] == i
        top = sub[: max(1, h // 5)]
        corners = np.r_[top[:, : max(1, w // 5)].ravel(), top[:, -max(1, w // 5):].ravel()].mean()
        mid = top[:, w // 3: 2 * w // 3].mean() if w >= 3 else 1
        arch = "round" if corners < 0.35 and mid > 0.7 and hm > 1.0 else "none"
        ftype = "door" if rect[1] < 0.4 and hm >= p["door_min_h"] else "window"
        boxes.append({"type": ftype, "rect": rect, "arch": arch, "fill": fr})
    # Repetition: boxes of similar size on the same row support each other.
    for b in boxes:
        w0, h0 = b["rect"][2] - b["rect"][0], b["rect"][3] - b["rect"][1]
        mates = sum(1 for o in boxes if o is not b and abs((o["rect"][2] - o["rect"][0]) / w0 - 1) < 0.25
                    and abs((o["rect"][3] - o["rect"][1]) / h0 - 1) < 0.25 and abs(o["rect"][1] - b["rect"][1]) < 0.3)
        conf = 0.3 + 0.3 * (b["fill"] - p["fill_min"]) / (1 - p["fill_min"]) + (0.25 if mates else 0)
        feats.append({"type": b["type"], "rect": b["rect"], "arch": b["arch"], "confidence": round(float(min(conf, 0.9)), 2),
                      "why": f"dark rectangle (fill {b['fill']:.2f}, {mates} similar on its row)"})
    # Clip to the facade.
    for f in feats:
        r = f["rect"]
        f["rect"] = [max(0.0, r[0]), max(0.0, r[1]), min(W, r[2]), min(H, r[3])]
    return [f for f in feats if f["rect"][2] - f["rect"][0] > 0.05 and f["rect"][3] - f["rect"][1] > 0.05]


def feature_depth(rect, uvd, ring=0.5, min_pts=3):
    """Depth of a feature relative to the wall around it (sparse points), or None."""
    if uvd is None or not len(uvd):
        return None
    u0, v0, u1, v1 = rect
    inside = (uvd[:, 0] >= u0) & (uvd[:, 0] <= u1) & (uvd[:, 1] >= v0) & (uvd[:, 1] <= v1) & (np.abs(uvd[:, 2]) < 1.5)
    around = ((uvd[:, 0] >= u0 - ring) & (uvd[:, 0] <= u1 + ring) & (uvd[:, 1] >= v0 - ring) & (uvd[:, 1] <= v1 + ring)
              & ~inside & (np.abs(uvd[:, 2]) < 1.5))
    if inside.sum() < min_pts or around.sum() < min_pts:
        return None
    return float(np.median(uvd[inside, 2]) - np.median(uvd[around, 2]))


# ---------------------------------------------------------------------------
# Check: draw a layout into photos and measure the offsets
# ---------------------------------------------------------------------------

def outline_uv(rect, step=0.05):
    u0, v0, u1, v1 = rect
    a = np.arange(u0, u1 + 1e-9, step)
    b = np.arange(v0, v1 + 1e-9, step)
    return np.r_[np.c_[a, np.full(len(a), v0)], np.c_[np.full(len(b), u1), b],
                 np.c_[a[::-1], np.full(len(a), v1)], np.c_[np.full(len(b), u0), b[::-1]]]


def _side_score(gx, gy, ph, P, sx, sy):
    """Mean image gradient across a projected edge (component normal to the edge in the photo)."""
    xy, z = project(ph["cam"], ph["R"], ph["t"], P)
    if (z <= 0.5).any():
        return None
    x, y = xy[:, 0] * sx, xy[:, 1] * sy
    h, w = gx.shape
    xi, yi = x.astype(int), y.astype(int)
    if ((xi < 0) | (xi >= w) | (yi < 0) | (yi >= h)).mean() > 0.2:
        return None
    ok = (xi >= 0) & (xi < w) & (yi >= 0) & (yi < h)
    d = np.array([x[-1] - x[0], y[-1] - y[0]])
    n = np.array([-d[1], d[0]]) / max(np.linalg.norm(d), 1e-9)
    return float(np.abs(gx[yi[ok], xi[ok]] * n[0] + gy[yi[ok], xi[ok]] * n[1]).mean())


def edge_offset(gx, gy, plane, ph, rect, sx, sy, search=0.5, step=0.02):
    """Offsets (du, dv) in meters that put the feature's edges on the strongest image edges.

    du: the two vertical sides are slid sideways together; dv: the two horizontal sides are slid
    up/down together (only for features at least 0.3 m tall/wide in that direction). Each score is
    the image gradient across the projected edge. Returns dict with du, dv (None when not measurable)
    and the contrast of each peak (best score / median score over the search)."""
    u0, v0, u1, v1 = rect
    eu, ev = np.asarray(plane["u"], float), np.asarray(plane["v"], float)
    shifts = np.arange(-search, search + 1e-9, step)
    out = {}
    tv = np.linspace(v0, v1, 25)
    tu = np.linspace(u0, u1, 25)
    sides = {"du": ([np.c_[np.full(25, u0), tv], np.c_[np.full(25, u1), tv]], eu),
             "dv": ([np.c_[tu, np.full(25, v0)], np.c_[tu, np.full(25, v1)]], ev)}
    for key, (segs, ax) in sides.items():
        if key == "dv" and (u1 - u0) < 0.3:
            out[key], out[key + "_contrast"] = None, None
            continue
        scores = []
        for sh in shifts:
            tot, ok = 0.0, True
            for seg in segs:
                P = fl.to_world(plane, seg[:, 0], seg[:, 1]) + sh * ax
                sc = _side_score(gx, gy, ph, P, sx, sy)
                if sc is None:
                    ok = False
                    break
                tot += sc
            scores.append(tot if ok else np.nan)
        scores = np.array(scores)
        if np.isnan(scores).mean() > 0.2:
            out[key], out[key + "_contrast"] = None, None
            continue
        i = int(np.nanargmax(scores))
        con = float(scores[i] / max(np.nanmedian(scores), 1e-9))
        at_limit = i in (0, len(shifts) - 1)
        out[key] = None if at_limit else float(shifts[i])
        out[key + "_contrast"] = round(con, 3)
    return out


def run_check(args, root):
    import cv2
    from align_review import save_jpg
    from PIL import Image, ImageDraw, ImageFont
    lay = fl.load_latest(args.building, args.facade, root)
    if lay is None:
        fail("no layout for this facade")
    plane = lay["plane"]
    photos = load_photos(root, args.set, args.version, args.units.split(",") if args.units else None,
                         args.photo_names.split(",") if args.photo_names else None)
    photos = [p for p in photos if p["id"] in lay["source"]["photos"]] or photos
    # The photos that see the most of the facade, nearest first.
    U, V = facade_grid(plane, 10)
    scored = []
    for ph in photos:
        _, wgt, info = rectify_one(root, ph, plane, U, V, args.max_incidence, args.ignore_bottom)
        if info["coverage"] > 0.5:
            scored.append((info["mean_distance_m"], ph, info))
    scored.sort(key=lambda t: t[0])
    if len(scored) < args.photos:
        log(f"only {len(scored)} photos see more than half of the facade")
    picks = scored[:args.photos]
    if not picks:
        fail("no photo sees the facade")
    feats = [f for f in lay["features"] if f["type"] in ("window", "door", "column", "pilaster", "arch")]
    results = []
    out_imgs = []
    rep = root / "reports" / "stage3" / "facades" / args.building / args.facade
    rep.mkdir(parents=True, exist_ok=True)
    for n, (dist, ph, info) in enumerate(picks, 1):
        img = cv2.imread(str(root / ph["derived"]), cv2.IMREAD_COLOR)
        h, w = img.shape[:2]
        sx, sy = w / ph["cam"]["width"], h / ph["cam"]["height"]
        g = cv2.GaussianBlur(cv2.cvtColor(img, cv2.COLOR_BGR2GRAY), (0, 0), 1.5).astype(np.float32)
        gx, gy = cv2.Sobel(g, cv2.CV_32F, 1, 0), cv2.Sobel(g, cv2.CV_32F, 0, 1)
        rows = []
        for f in feats:
            r = edge_offset(gx, gy, plane, ph, f["rect"], sx, sy, search=args.search)
            row = {"feature": f["id"], "type": f["type"]}
            for k in ("du", "dv"):
                ok = r[k] is not None and (r[k + "_contrast"] or 0) >= args.min_contrast
                row[k + "_m"] = round(r[k], 3) if ok else None
                row[k + "_contrast"] = r[k + "_contrast"]
            rows.append(row)
        # One offset per measured edge direction: |du| from vertical edges, |dv| from horizontal edges.
        offs = [abs(r[k]) for r in rows for k in ("du_m", "dv_m") if r[k] is not None]
        good = [r for r in rows if r["du_m"] is not None or r["dv_m"] is not None]
        off = np.array(offs)
        m_px = dist / ph["cam"]["focal_px"]
        summary = {"photo": ph["id"], "unit": ph["unit"], "mean_distance_m": dist, "m_per_px": round(m_px, 4),
                   "features_drawn": len(rows), "features_measured": len(good), "edges_measured": int(len(off)),
                   "median_abs_du_m": round(float(np.median([abs(r["du_m"]) for r in rows if r["du_m"] is not None])), 3)
                   if any(r["du_m"] is not None for r in rows) else None,
                   "median_abs_dv_m": round(float(np.median([abs(r["dv_m"]) for r in rows if r["dv_m"] is not None])), 3)
                   if any(r["dv_m"] is not None for r in rows) else None,
                   "median_offset_m": round(float(np.median(off)), 3) if len(off) else None,
                   "p90_offset_m": round(float(np.percentile(off, 90)), 3) if len(off) else None,
                   "max_offset_m": round(float(off.max()), 3) if len(off) else None,
                   "within_0.1m": int((off <= 0.1).sum()), "rows": rows}
        results.append(summary)
        # Overlay: drawn layout (yellow) and the best-fit position (cyan) for measured features.
        pil = Image.open(root / ph["derived"]).convert("RGB")
        dr = ImageDraw.Draw(pil)
        for f in lay["features"]:
            O = outline_uv(f["rect"], 0.1)
            xy, z = project(ph["cam"], ph["R"], ph["t"], fl.to_world(plane, O[:, 0], O[:, 1]))
            if (z > 0.5).all():
                col = (255, 230, 0) if f["type"] in ("window", "door", "column", "pilaster", "arch") else (255, 120, 0)
                dr.line([(x * sx, y * sy) for x, y in xy] + [(xy[0, 0] * sx, xy[0, 1] * sy)], fill=col, width=3)
        for r in good:
            f = next(x for x in feats if x["id"] == r["feature"])
            O = outline_uv(f["rect"], 0.1)
            P = fl.to_world(plane, O[:, 0] + (r["du_m"] or 0.0), O[:, 1] + (r["dv_m"] or 0.0))
            xy, z = project(ph["cam"], ph["R"], ph["t"], P)
            dr.line([(x * sx, y * sy) for x, y in xy], fill=(0, 230, 255), width=2)
        # Crop around the facade for a readable image.
        Wf, Hf = plane["width_m"], plane["height_m"]
        cxy, cz = project(ph["cam"], ph["R"], ph["t"], fl.to_world(plane, np.array([0, Wf, Wf, 0]), np.array([0, 0, Hf, Hf])))
        x0, x1 = max(0, int(cxy[:, 0].min() * sx) - 150), min(pil.width, int(cxy[:, 0].max() * sx) + 150)
        y0, y1 = max(0, int(cxy[:, 1].min() * sy) - 150), min(pil.height, int(cxy[:, 1].max() * sy) + 150)
        crop = pil.crop((x0, y0, x1, y1))
        scale = min(1.0, 1200 / crop.width)
        crop = crop.resize((int(crop.width * scale), int(crop.height * scale)))
        canvas = Image.new("RGB", (crop.width, crop.height + 52), (20, 20, 20))
        canvas.paste(crop, (0, 0))
        dc = ImageDraw.Draw(canvas)
        try:
            font = ImageFont.truetype("arial.ttf", 13)
        except OSError:
            font = ImageFont.load_default()
        med = summary["median_offset_m"]
        dc.text((6, crop.height + 4), f"{args.building} {args.facade} layout v{lay['version']} in {ph['name']}: yellow = "
                f"layout, cyan = best edge fit; median offset {med} m over {len(off)} edge offsets", fill=(235, 235, 235), font=font)
        lic = ph.get("license") or ""
        user = lic.split("|")[1].strip() if "|" in lic else ""
        dc.text((6, crop.height + 26), f"{CREDIT} [{user}]", fill=(200, 200, 200), font=font)
        pth = rep / f"check{args.tag}_{n}.jpg"
        save_jpg(canvas, pth)
        out_imgs.append(pth)
        log(f"{ph['name']}: {len(good)}/{len(rows)} features, {len(off)} edge offsets, median {summary['median_offset_m']} m "
            f"(|du| {summary['median_abs_du_m']}, |dv| {summary['median_abs_dv_m']}), "
            f"p90 {summary['p90_offset_m']} m, {summary['within_0.1m']} within 0.1 m ({m_px * 100:.1f} cm per px)")
    allo = np.array([abs(r[k]) for s in results for r in s["rows"] for k in ("du_m", "dv_m") if r[k] is not None])
    rep_json = {"building": args.building, "facade": args.facade, "layout_version": lay["version"],
                "made": datetime.datetime.now().isoformat(timespec="seconds"),
                "method": ("for each window/door/column/pilaster/arch of the layout, its two vertical sides are slid "
                           "sideways together (du) and its two horizontal sides up/down together (dv) in the facade "
                           "plane, -search..+search in 2 cm steps; at each shift the image gradient across the "
                           "projected edges is summed; the shift with the strongest edges is the offset. An offset "
                           "counts only if its peak is >= min_contrast x the median over all shifts and not at the "
                           "search limit. Offsets are per edge direction (|du| or |dv|), in meters on the facade."),
                "search_m": args.search,
                "min_contrast": args.min_contrast, "target_m": 0.1,
                "overall": {"edge_offsets_measured": int(len(allo)),
                            "median_offset_m": round(float(np.median(allo)), 3) if len(allo) else None,
                            "p90_offset_m": round(float(np.percentile(allo, 90)), 3) if len(allo) else None,
                            "within_0.1m": int((allo <= 0.1).sum())},
                "photos": results, "images": [str(p.relative_to(root)).replace("\\", "/") for p in out_imgs]}
    save_json(rep / f"check{args.tag}_v{lay['version']}.json", rep_json)
    if args.copy_to_docs:
        import shutil
        dst = REPO / "Docs" / "Development" / "VanderbiltCampus" / "progress" / "images"
        for k, p in enumerate(out_imgs, 1):
            shutil.copyfile(p, dst / f"stage3-facade-{k}.jpg")
        log(f"copied {len(out_imgs)} overlays to {dst}")
    import manifest
    db = manifest.connect(root)
    for f in list(out_imgs) + [rep / f"check{args.tag}_v{lay['version']}.json"]:
        manifest.add_file(f, source="pipeline/facade_measure.py check", license="derived; " + CREDIT, stage=3,
                          root=root, db=db)
    db.commit()
    db.close()
    log(f"overall: {rep_json['overall']}")
    return rep_json


# ---------------------------------------------------------------------------
# Measure
# ---------------------------------------------------------------------------

def run_measure(args, root):
    import cv2
    plane, how = facade_plane(root, args.building, args.facade)
    aln = root / "derived" / "alignment" / args.set / args.version
    photos = load_photos(root, args.set, args.version, args.units.split(",") if args.units else None,
                         args.photo_names.split(",") if args.photo_names else None)
    if not photos:
        fail("no reliable registered photos (check --units / --photos)")
    pts, arr = read_ply_points(aln / "points.ply")
    reg = load_json(aln / "registration.json")
    if args.units and "unit" in arr.dtype.names:
        keep = np.isin(np.array(reg["units"])[arr["unit"]], args.units.split(","))
        pts = pts[keep]
    plane, fit = fit_plane_offset(plane, pts) if not args.no_plane_fit else (plane, {"shift_m": 0.0, "note": "skipped"})
    log(f"plane: {how}; shift {fit.get('shift_m')} m from {fit['points_near_wall'] if 'points_near_wall' in fit else 0} points")
    U, V = facade_grid(plane, args.px_per_m)
    stack, used = [], []
    for ph in photos:
        rgb, wgt, info = rectify_one(root, ph, plane, U, V, args.max_incidence, args.ignore_bottom)
        if info["coverage"] < args.min_coverage:
            continue
        stack.append((rgb, wgt, info))
        used.append(info)
        log(f"  {ph['name']}: covers {info['coverage']:.0%} at {info['mean_distance_m']} m, "
            f"incidence {info['mean_incidence_deg']} deg")
    if not stack:
        fail("no photo sees this facade (coverage below --min-coverage)")
    img, valid = blend(stack)
    W, H = plane["width_m"], plane["height_m"]
    params = json.loads(args.detector_params) if args.detector_params else None
    feats = detect_features(img, valid, args.px_per_m, W, H, params)
    uvd = fl.to_facade(plane, pts) if len(pts) else None
    if uvd is not None:
        near = (uvd[:, 0] > -1) & (uvd[:, 0] < W + 1) & (uvd[:, 1] > -1) & (uvd[:, 1] < H + 1) & (np.abs(uvd[:, 2]) < 1.5)
        uvd = uvd[near]
    out_feats = []
    for i, f in enumerate(sorted(feats, key=lambda f: (f["type"], -f["rect"][3], f["rect"][0])), 1):
        d = feature_depth(f["rect"], uvd)
        out_feats.append(fl.new_feature(f"D{i:03d}", f["type"], f["rect"], arch=f["arch"], depth_m=d,
                                        confidence=f["confidence"], source="detected"))
    v_next = (fl.versions(args.building, args.facade, root) or [0])[-1] + 1
    fdir = fl.facade_dir(args.building, args.facade, root)
    fdir.mkdir(parents=True, exist_ok=True)
    rect_path = fdir / f"rectified_v{v_next}.jpg"
    cv2.imwrite(str(rect_path), img, [cv2.IMWRITE_JPEG_QUALITY, 92])
    layout = fl.new_layout(args.building, args.facade, plane, out_feats,
                           source={"alignment_set": f"{args.set}/{args.version}", "photos": [u["photo"] for u in used],
                                   "rectified_image": rect_path.relative_to(root).as_posix(),
                                   "px_per_m": float(args.px_per_m)})
    path = fl.save(layout, root, source="pipeline/facade_measure.py", license="derived; " + CREDIT, stage=3)
    side = {"building": args.building, "facade": args.facade, "layout": path.name,
            "made": datetime.datetime.now().isoformat(timespec="seconds"), "plane_from": how, "plane_fit": fit,
            "photos": used, "detector": {"method": "classical OpenCV (Sobel line profiles, adaptive threshold + "
                                                   "connected components); no learned model",
                                         "opencv": cv2.__version__, "params": params or "defaults"},
            "features": [{"id": o["id"], "why": f["why"]} for o, f in
                         zip(out_feats, sorted(feats, key=lambda f: (f["type"], -f["rect"][3], f["rect"][0])))],
            "credit": CREDIT, "note": "rectified image is for measuring only and stays in the archive"}
    side_path = fdir / f"measure_v{v_next}.json"
    save_json(side_path, side)
    import manifest
    for f in (rect_path, side_path):
        manifest.add_file(f, source="pipeline/facade_measure.py", license="derived; " + CREDIT, stage=3, root=root)
    counts = {}
    for f in out_feats:
        counts[f["type"]] = counts.get(f["type"], 0) + 1
    log(f"wrote {path} ({len(out_feats)} features: {counts}); rectified {img.shape[1]}x{img.shape[0]} px from "
        f"{len(used)} photos")
    return path


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("measure", "check"):
        a = sub.add_parser(name)
        a.add_argument("--building", required=True)
        a.add_argument("--facade", required=True, help="facade id from facade_layout.facades_of(), e.g. F01")
        a.add_argument("--set", default="kartaview-2026-11-19")
        a.add_argument("--version", default="v1", help="alignment version")
        a.add_argument("--units", help="only photos of these registration units (comma separated)")
        a.add_argument("--photo-names", help="only photos whose names start with these (comma separated)")
        a.add_argument("--max-incidence", type=float, default=70.0)
        a.add_argument("--ignore-bottom", type=float, default=0.12,
                       help="ignore this share of the image bottom (dashcam hood)")
    m = sub.choices["measure"]
    m.add_argument("--px-per-m", type=float, default=40.0)
    m.add_argument("--min-coverage", type=float, default=0.2, help="photo must see this share of the facade")
    m.add_argument("--no-plane-fit", action="store_true")
    m.add_argument("--detector-params", help="JSON overrides for detect_features()")
    c = sub.choices["check"]
    c.add_argument("--photos", type=int, default=3)
    c.add_argument("--min-contrast", type=float, default=1.15)
    c.add_argument("--search", type=float, default=0.5, help="largest shift tried (m)")
    c.add_argument("--tag", default="", help="suffix for the output names (e.g. _unit17)")
    c.add_argument("--copy-to-docs", action="store_true",
                   help="copy the overlays to Docs/.../progress/images/stage3-facade-<n>.jpg")
    args = ap.parse_args(argv)
    root = data_root()
    if args.cmd == "measure":
        run_measure(args, root)
    else:
        run_check(args, root)
    return 0


if __name__ == "__main__":
    sys.exit(main())
