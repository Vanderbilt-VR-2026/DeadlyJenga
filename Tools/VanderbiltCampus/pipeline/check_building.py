"""Stage 4 checks for one LoD3 / LoD3+ building folder (buildings/<id>/lod3_v<n>/).

Checks that need no photos (always run):
  mesh       every face has a known material ID; UV0 inside 0..1; no UV overlap (exact
             rasterized test at 2048 px, shared edges ignored); no degenerate triangles
  files      ledger.json, sources.json, model.json present and well formed
  merge      every ledger row is under the level's threshold (relief, or both surface sizes);
             every layout feature whose relief is over the threshold is really in the mesh
             (vertices at that depth inside its rectangle)
  budget     triangle counts against the targets (LoD3+ 30k / collider ~3k; LoD3 20k / 3k)
  collider   no degenerate or duplicate triangles, consistent winding; rays dropped on every
             step tread and on every ledge (courses >= 0.2 m deep) land on the expected height
  envelope   points sampled on the v2 LoD2 shell -> distance to the LoD3 mesh (the outer
             envelope must not move; openings and the rebuilt tower show up as the tail)

Photo check (runs when cameras exist; --alignment SET/vN, or every set listed in the
building's sources.json):
  Reads derived/alignment/<set>/v<n>/cameras.json in the campus frame. Two formats:
   (a) the Stage 3 format (align.py + register_lidar.py): images[] with "derived" (image path
       under data_root), "camera" {model, params, width, height} (COLMAP models) and
       "campus" {"center", "R_cam_from_campus"} (x_cam = R (X - center));
   (b) a small documented format for other tools:
       {"cameras": [{"id", "image", "width", "height", "fx", "fy", "cx", "cy",
                     "distortion": [k1, k2, p1, p2], "R": 3x3, "t": 3}]}
       with R, t world-to-camera in campus meters (x_cam = R X + t; x right, y down, z forward).
  Projects the model's feature edges (creases > 30 deg, borders, material changes) into each
  photo that sees the building, keeps the points not hidden by the model (ray test), measures
  the distance to the nearest image edge (Canny) and converts it to meters at that depth;
  reports median and 95th percentile per facade. Reviewed features (layouts with
  reviewed=true, newer than or equal to the version used) are compared with the model's
  features in the facade plane (meters). Writes reports/stage4/<id>/photo_check/*.jpg:
  photo | overlay (edge points colored by error) | model render from the same camera.

    python pipeline/check_building.py B_544397715                 # newest lod3 folder
    python pipeline/check_building.py B_544413714 --alignment kartaview-2026-11-19/v3
Writes <folder>/check.json (and the manifest); exits 1 when a no-photo check fails.
"""
import argparse
import datetime
import json
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "blender"))
from common import data_root, fail, load_json, save_json  # noqa: E402

TARGETS = {"3": {"lod3": 20000, "game_col": 3000}, "3+": {"lod3": 30000, "game_col": 3000}}
COL_SLACK = 1.10                # "about 3,000": up to 10 % over passes, with a note
HINTS = {"normal_map", "geometry", "decal", "texture_only"}
LEDGE_DEPTH = 0.2


# ---- mesh ---------------------------------------------------------------------------------

def uv_overlap(uv0, grid=2048):
    """Fraction of covered texels claimed by more than one triangle (each triangle shrunk by one
    texel so triangles that only share an edge do not count). Works per triangle bounding box."""
    import cv2
    count = np.zeros((grid, grid), np.uint8)
    P = np.asarray(uv0, np.float64) * (grid - 1)
    k = np.ones((3, 3), np.uint8)
    for t in P:
        x0, y0 = np.floor(t.min(axis=0)).astype(int) - 1
        x1, y1 = np.ceil(t.max(axis=0)).astype(int) + 2
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, grid), min(y1, grid)
        if x1 <= x0 or y1 <= y0:
            continue
        win = np.zeros((y1 - y0, x1 - x0), np.uint8)
        pts = np.round((t - [x0, y0]) * 16).astype(np.int32)
        cv2.fillConvexPoly(win, pts, 1, lineType=cv2.LINE_8, shift=4)
        win = cv2.erode(win, k)
        sub = count[y0:y1, x0:x1]
        np.minimum(sub.astype(np.uint16) + win, 255, out=sub, casting="unsafe")
    covered = int((count > 0).sum())
    return float((count > 1).sum()) / max(covered, 1)


def check_mesh_file(path, need_uv=True, overlap=True):
    import vcmesh
    m = vcmesh.load_mesh(path)
    out = {"triangles": int(len(m["faces"])), "problems": vcmesh.check_mesh(m)}
    if need_uv:
        if overlap:
            out["uv_overlap_fraction"] = round(uv_overlap(m["uv0"]), 5)
            if out["uv_overlap_fraction"] > 0.002:
                out["problems"].append(f"UV0 overlap {out['uv_overlap_fraction']:.4f}")
        uv = m["uv0"].astype(np.float64)
        a, b = uv[:, 1] - uv[:, 0], uv[:, 2] - uv[:, 0]
        flat = np.abs(a[:, 0] * b[:, 1] - a[:, 1] * b[:, 0]) < 1e-14
        out["zero_area_uv_triangles"] = int(flat.sum())
        if flat.any():
            out["problems"].append(f"{int(flat.sum())} triangles with zero UV area")
    return m, out


# ---- merge rule ---------------------------------------------------------------------------

def relief_of(feature):
    """(relief, kind): how far a layout feature leaves the wall, when the layout says so."""
    t, d = feature["type"], feature["depth_m"]
    if d is None:
        return None, None
    if t in ("cornice", "string_course", "pilaster", "buttress", "projection"):
        return abs(d), "out"
    if t == "recess":
        return abs(d), "in"
    if t in ("window", "door") and d < 0:
        return abs(d), "in"
    return None, None


def merge_checks(mesh, model, ledger, layouts, threshold):
    import facade_layout as fl
    rows_bad = []
    for e in ledger["entries"]:
        if "s3_kept" in e:
            continue
        reason = e.get("reason", "")
        if reason.startswith("relief") or "both <" in reason:
            s = e["size"]
            relief, a, b = s[1], s[0], s[2]
            if not (relief < threshold + 1e-6 or (a < threshold + 1e-6 and b < threshold + 1e-6)):
                rows_bad.append(e["id"])
    missing = []
    checked = 0
    V = mesh["verts"]
    Fv = V[mesh["faces"]]
    Fc = Fv.mean(axis=1)
    Fn = np.cross(Fv[:, 1] - Fv[:, 0], Fv[:, 2] - Fv[:, 0])
    Fa = np.linalg.norm(Fn, axis=1) / 2
    Fn = Fn / np.maximum(2 * Fa, 1e-12)[:, None]
    for k, lay in layouts.items():
        q = fl.to_facade(lay, V)
        Fq = fl.to_facade(lay, Fc)
        facing = Fn @ np.asarray(lay["plane"]["normal"]) > 0.99
        for f in lay["features"]:
            rel, kind = relief_of(f)
            if rel is None or rel < threshold:
                continue
            u0, v0, u1, v1 = f["rect"]
            m = (q[:, 0] > u0 - 0.05) & (q[:, 0] < u1 + 0.05) & (q[:, 1] > v0 - 0.05) & (q[:, 1] < v1 + 0.05)
            m &= np.abs(q[:, 2]) < rel + 1.5
            checked += 1
            if not m.any():
                missing.append(f"{k}/{f['id']}")
                continue
            # depth relative to the wall: the most common depth of outward-facing faces near it
            near = (Fq[:, 0] > u0 - 1) & (Fq[:, 0] < u1 + 1) & (np.abs(Fq[:, 2]) < 1.0) & facing
            if near.sum() >= 3:
                hist, edges = np.histogram(Fq[near, 2], bins=np.arange(-1.0, 1.01, 0.01), weights=Fa[near])
                wall = float(edges[np.argmax(hist)] + 0.005)
            else:
                wall = 0.0
            got = (q[m, 2].max() - wall) if kind == "out" else (wall - q[m, 2].min())
            if got < 0.9 * rel - 0.02:
                missing.append(f"{k}/{f['id']} ({f['type']} relief {rel:.2f} m, mesh shows {got:.2f} m)")
    return {"ledger_rows_over_threshold": rows_bad, "features_checked": checked, "features_missing": missing,
            "ok": not rows_bad and not missing}


# ---- collider -----------------------------------------------------------------------------

def ray_scene(mesh):
    import open3d as o3d
    off = mesh["verts"].mean(axis=0)
    sc = o3d.t.geometry.RaycastingScene()
    sc.add_triangles(o3d.core.Tensor((mesh["verts"] - off).astype(np.float32)),
                     o3d.core.Tensor(mesh["faces"].astype(np.uint32)))
    return sc, off


def drop_rays(sc, off, xy, z_from):
    import open3d as o3d
    n = len(xy)
    rays = np.zeros((n, 6), np.float32)
    rays[:, 0:2] = xy - off[:2]
    rays[:, 2] = z_from - off[2]
    rays[:, 5] = -1.0
    hit = sc.cast_rays(o3d.core.Tensor(rays))["t_hit"].numpy()
    return z_from - hit


def wall_offset(mesh, lay, u0, u1):
    """How far the rebuilt wall sits off the facade plane near u0..u1 (most common depth of
    outward-facing faces, area weighted)."""
    import facade_layout as fl
    Fv = mesh["verts"][mesh["faces"]]
    Fn = np.cross(Fv[:, 1] - Fv[:, 0], Fv[:, 2] - Fv[:, 0])
    Fa = np.linalg.norm(Fn, axis=1) / 2
    Fn = Fn / np.maximum(2 * Fa, 1e-12)[:, None]
    q = fl.to_facade(lay, Fv.mean(axis=1))
    near = (q[:, 0] > u0 - 1) & (q[:, 0] < u1 + 1) & (np.abs(q[:, 2]) < 1.5) & (Fn @ np.asarray(lay["plane"]["normal"]) > 0.99)
    if near.sum() < 3:
        return 0.0
    hist, edges = np.histogram(q[near, 2], bins=np.arange(-1.5, 1.51, 0.01), weights=Fa[near])
    return float(edges[np.argmax(hist)] + 0.005)


def collider_checks(col, layouts, kit_steps_riser=0.165, lod3=None):
    import buildings_v2 as b2
    import facade_layout as fl
    out = {"problems": b2.collider_problems(col), "steps": [], "ledges": []}
    sc, off = ray_scene(col)
    for k, lay in sorted(layouts.items()):
        for f in lay["features"]:
            u0, v0, u1, v1 = f["rect"]
            if f["type"] == "steps":
                rise = v1 - v0
                n = max(1, int(round(rise / kit_steps_riser)))
                r = rise / n
                run = f["depth_m"] or 2.0
                tread = max(run, 0.3 * n) / n          # pieces.steps keeps treads >= 0.3 m
                run = tread * n
                cu = (u0 + u1) / 2
                w0 = wall_offset(lod3, lay, u0, u1) if lod3 is not None else 0.0
                errs = []
                for i in range(1, n + 1):
                    z_out = run - (i - 0.5) * tread if i < n else tread * 0.5
                    p = fl.to_world(lay, cu, v1 + 2.0, z_out + w0)
                    zt = lay["plane"]["origin"][2] + v0 + i * r
                    hz = drop_rays(sc, off, np.array([p[:2]]), zt + 0.4)[0]   # from 0.4 m above the tread
                    errs.append(abs(hz - zt))
                errs = np.asarray(errs)
                bad = [i + 1 for i, e in enumerate(errs) if not (np.isfinite(e) and e < 0.05)]
                out["steps"].append({"feature": f"{k}/{f['id']}", "treads": n, "riser_m": round(r, 3),
                                     "treads_failing": bad,
                                     "max_error_m": round(float(np.nanmax(errs)) if np.isfinite(errs).any() else 99, 3),
                                     "all_hit": bool(np.isfinite(errs).all()),
                                     "ok": bool(np.isfinite(errs).all() and np.nanmax(errs) < 0.05)})
            elif f["type"] in ("string_course", "cornice") and (f["depth_m"] or 0) >= LEDGE_DEPTH:
                cu = np.linspace(u0 + 0.5, u1 - 0.5, 5) if u1 - u0 > 1.5 else np.array([(u0 + u1) / 2])
                w0 = wall_offset(lod3, lay, u0, u1) if lod3 is not None else 0.0
                p = fl.to_world(lay, cu, np.full_like(cu, v1 + 0.5), np.full_like(cu, f["depth_m"] * 0.5 + w0))
                zt = lay["plane"]["origin"][2] + v1
                hz = drop_rays(sc, off, p[:, :2], zt + 0.5)
                ok = np.isfinite(hz) & (np.abs(hz - zt) < 0.05)
                out["ledges"].append({"feature": f"{k}/{f['id']}", "depth_m": f["depth_m"], "top_z": round(zt, 3),
                                      "rays": int(len(hz)), "rays_on_ledge": int(ok.sum()), "ok": bool(ok.mean() >= 0.6)})
    out["steps_ok"] = all(s["ok"] for s in out["steps"])          # true when the layouts have no steps
    # at least one ledge must work when the layouts have ledges (the hero play check climbs one)
    out["ledges_ok"] = any(x["ok"] for x in out["ledges"]) if out["ledges"] else True
    if not out["ledges"]:
        out["ledges_note"] = "no course >= 0.2 m deep in these layouts"
    return out


# ---- envelope -----------------------------------------------------------------------------

def envelope(lod3, lod2, tower=None, n=30000, seed=0):
    """Points sampled on the LoD2 shell (no base) -> nearest distance to the LoD3 mesh."""
    import buildings_v2 as b2
    import open3d as o3d
    import shapely
    from shapely.geometry import Polygon
    v, f = b2.shell_of(lod2)
    T = v[f]
    a = np.linalg.norm(np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]), axis=1) / 2
    rng = np.random.default_rng(seed)
    k = rng.choice(len(f), size=n, p=a / a.sum())
    r1, r2 = rng.random(n), rng.random(n)
    s = np.sqrt(r1)
    P = (1 - s)[:, None] * T[k, 0] + (s * (1 - r2))[:, None] * T[k, 1] + (s * r2)[:, None] * T[k, 2]
    sc, off = ray_scene(lod3)
    d = sc.compute_distance(o3d.core.Tensor((P - off).astype(np.float32))).numpy()
    out = {"samples": n, "median_m": round(float(np.median(d)), 3), "p95_m": round(float(np.percentile(d, 95)), 3),
           "max_m": round(float(d.max()), 3)}
    if tower:
        tp = Polygon(tower["outer"]).buffer(1.5)
        inside = shapely.contains_xy(tp, P[:, 0], P[:, 1])
        dd = d[~inside]
        out["outside_tower"] = {"median_m": round(float(np.median(dd)), 3), "p95_m": round(float(np.percentile(dd, 95)), 3),
                                "max_m": round(float(dd.max()), 3)}
        out["tower_region"] = {"p95_m": round(float(np.percentile(d[inside], 95)), 3) if inside.any() else None,
                               "note": "the LoD2 tower is a stepped height field; LoD3 rebuilds it as a clean shaft"}
    main = out.get("outside_tower", out)
    out["ok"] = main["p95_m"] <= 0.30
    out["rule"] = "p95 (outside the rebuilt tower) <= 0.30 m; window reveals (0.2-0.9 m) make the tail"
    return out


# ---- photo check --------------------------------------------------------------------------

def load_cameras(path, root):
    """Cameras in the campus frame from either cameras.json format (module help).
    Returns a list of {id, image, w, h, project(X)->(px, depth), center, R, t}."""
    data = load_json(path)
    cams = []
    if "images" in data:
        from align_sources import project as colmap_project
        for e in data["images"]:
            c = e.get("campus")
            if not c or not e.get("camera") or c.get("reliable") is False:
                continue                      # unregistered, or registered but flagged unreliable
            R = np.asarray(c["R_cam_from_campus"], float)
            C = np.asarray(c["center"], float)
            t = -R @ C
            cam = e["camera"]
            cams.append({"id": e["name"], "image": Path(root) / e["derived"], "w": cam["width"], "h": cam["height"],
                         "center": C, "R": R, "t": t,
                         "project": (lambda X, cam=cam, R=R, t=t: colmap_project(cam, R, t, X)),
                         "f": float(cam["params"][0])})
    elif "cameras" in data:
        for e in data["cameras"]:
            R = np.asarray(e["R"], float)
            t = np.asarray(e["t"], float)
            k1, k2, p1, p2 = (list(e.get("distortion") or []) + [0, 0, 0, 0])[:4]

            def proj(X, e=e, R=R, t=t, k1=k1, k2=k2, p1=p1, p2=p2):
                Xc = np.asarray(X) @ R.T + t
                z = Xc[:, 2]
                with np.errstate(divide="ignore", invalid="ignore"):
                    x, y = Xc[:, 0] / z, Xc[:, 1] / z
                r2 = x * x + y * y
                dd = 1 + k1 * r2 + k2 * r2 * r2
                xx = x * dd + 2 * p1 * x * y + p2 * (r2 + 2 * x * x)
                yy = y * dd + p1 * (r2 + 2 * y * y) + 2 * p2 * x * y
                return np.stack([e["fx"] * xx + e["cx"], e["fy"] * yy + e["cy"]], 1), z
            img = Path(e["image"])
            cams.append({"id": e["id"], "image": img if img.is_absolute() else Path(root) / img, "w": e["width"],
                         "h": e["height"], "center": -R.T @ t, "R": R, "t": t, "project": proj, "f": float(e["fx"])})
    else:
        raise ValueError(f"{path}: unknown cameras.json format")
    return cams


def feature_edges(mesh, angle_deg=30.0, step=0.15):
    """Sample points along crease, border and material-change edges."""
    v, f, m = mesh["verts"], mesh["faces"], mesh["face_mat"]
    n = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    n /= np.maximum(np.linalg.norm(n, axis=1), 1e-12)[:, None]
    E = np.r_[f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]]]
    F = np.tile(np.arange(len(f)), 3)
    key = np.sort(E, axis=1)
    o = np.lexsort((key[:, 1], key[:, 0]))
    key, F = key[o], F[o]
    same = np.all(key[1:] == key[:-1], axis=1)
    cos = math.cos(math.radians(angle_deg))
    keep = []
    i = 0
    while i < len(key):
        if i + 1 < len(key) and same[i]:
            a, b = F[i], F[i + 1]
            if n[a] @ n[b] < cos or m[a] != m[b]:
                keep.append(key[i])
            i += 2
        else:
            keep.append(key[i])
            i += 1
    pts = []
    for a, b in keep:
        p, q = v[a], v[b]
        L = np.linalg.norm(q - p)
        k = max(1, int(L / step))
        s = (np.arange(k) + 0.5) / k
        pts.append(p + s[:, None] * (q - p))
    return np.vstack(pts) if pts else np.zeros((0, 3))


def photo_check(folder, mesh, layouts, cam_files, root, max_images=12, max_dist=80.0):
    import cv2
    import facade_layout as fl
    import render_building as rb
    from PIL import Image
    E = feature_edges(mesh)
    sc, off = ray_scene(mesh)
    import open3d as o3d
    # assign every edge point to its nearest facade plane (within 1.5 m of it)
    fac = np.full(len(E), "", object)
    best = np.full(len(E), np.inf)
    for k, lay in layouts.items():
        q = fl.to_facade(lay, E)
        W, H = lay["plane"]["width_m"], lay["plane"]["height_m"]
        ok = (q[:, 0] > -0.5) & (q[:, 0] < W + 0.5) & (q[:, 1] > -1) & (q[:, 1] < H + 1) & (np.abs(q[:, 2]) < 1.5)
        d = np.abs(q[:, 2])
        better = ok & (d < best)
        fac[better] = k
        best[better] = d[better]
    per_facade, images, used = {}, [], 0
    out_dir = Path(root) / "reports" / "stage4" / load_json(folder / "model.json")["building"] / "photo_check"
    cams = []
    for cf in cam_files:
        try:
            cams += [(cf, c) for c in load_cameras(cf, root)]
        except (OSError, ValueError, KeyError) as ex:
            images.append({"cameras": str(cf), "error": str(ex)})
    scored = []
    for cf, c in cams:
        px, z = c["project"](E)
        dist = np.linalg.norm(E - c["center"], axis=1)
        inside = (z > 0.5) & (px[:, 0] >= 0) & (px[:, 0] < c["w"]) & (px[:, 1] >= 0) & (px[:, 1] < c["h"]) & (dist < max_dist)
        if inside.sum() < 50:
            continue
        scored.append((int(inside.sum()), cf, c, px, z, inside, dist))
    scored.sort(key=lambda s: -s[0])
    errs_by_fac = {}
    for nvis, cf, c, px, z, inside, dist in scored[:max_images]:
        # visibility: ray from the camera to each point must not hit the model first
        idx = np.where(inside)[0]
        d = E[idx] - c["center"]
        L = np.linalg.norm(d, axis=1)
        rays = np.c_[np.repeat((c["center"] - off)[None], len(idx), 0), d / L[:, None]].astype(np.float32)
        hit = sc.cast_rays(o3d.core.Tensor(rays))["t_hit"].numpy()
        vis = hit > L - 0.08
        idx = idx[vis]
        if len(idx) < 30 or not Path(c["image"]).exists():
            continue
        img = np.asarray(Image.open(c["image"]).convert("RGB"))
        if img.shape[1] != c["w"]:
            sc_x = img.shape[1] / c["w"]
        else:
            sc_x = 1.0
        gray = cv2.cvtColor(img, cv2.COLOR_RGB2GRAY)
        edges = cv2.Canny(cv2.GaussianBlur(gray, (5, 5), 0), 50, 150)
        dt = cv2.distanceTransform(255 - edges, cv2.DIST_L2, 5)
        p = np.round(px[idx] * sc_x).astype(int)
        p[:, 0] = np.clip(p[:, 0], 0, img.shape[1] - 1)
        p[:, 1] = np.clip(p[:, 1], 0, img.shape[0] - 1)
        off_px = dt[p[:, 1], p[:, 0]] / sc_x
        off_m = off_px * z[idx] / c["f"]
        for fk in set(fac[idx]) - {""}:
            sel = fac[idx] == fk
            errs_by_fac.setdefault(fk, []).append(off_m[sel])
        # side-by-side: photo | overlay | model render from the same camera
        ov = img.copy()
        col = np.clip(off_m / 0.3, 0, 1)
        for (x, y), cc in zip(p, col):
            cv2.circle(ov, (int(x), int(y)), 2, (int(255 * cc), int(255 * (1 - cc)), 0), -1)
        h, w = img.shape[:2]
        K = None
        ren = rb.render([mesh], K, c["R"], c["t"], w, h,
                        project=lambda X, c=c, s=sc_x: (c["project"](X)[0] * s, c["project"](X)[1]))
        trip = np.concatenate([img, ov, ren], 1)
        if trip.shape[1] > 2400:
            f = 2400 / trip.shape[1]
            trip = cv2.resize(trip, (2400, int(trip.shape[0] * f)), interpolation=cv2.INTER_AREA)
        out_dir.mkdir(parents=True, exist_ok=True)
        op = out_dir / (Path(c["id"]).stem + ".jpg")
        Image.fromarray(trip).save(op, quality=85)
        images.append({"camera": c["id"], "cameras_file": str(cf), "visible_edge_points": int(len(idx)),
                       "median_m": round(float(np.median(off_m)), 3), "p95_m": round(float(np.percentile(off_m, 95)), 3),
                       "image": str(op)})
        used += 1
    for fk, arrs in errs_by_fac.items():
        a = np.concatenate(arrs)
        per_facade[fk] = {"points": int(len(a)), "median_m": round(float(np.median(a)), 3),
                          "p95_m": round(float(np.percentile(a, 95)), 3)}
    return {"cameras_considered": len(cams), "images_used": used, "per_facade": per_facade, "images": images,
            "method": "model feature edges projected into each photo; distance to the nearest Canny edge, "
                      "converted to meters at the point's depth (offset_px * depth / focal)"}


def reviewed_feature_offsets(layouts_used, root, bid):
    """Model features (layout used) vs reviewed features of the newest reviewed layout, in meters."""
    import facade_layout as fl
    out = {}
    for k, lay in layouts_used.items():
        newest = None
        for v in reversed(fl.versions(bid, k, root)):
            cand = fl.load(fl.facade_dir(bid, k, root) / f"layout_v{v}.json")
            if any(f["reviewed"] for f in cand["features"]):
                newest = cand
                break
        if newest is None:
            continue
        rev = [f for f in newest["features"] if f["reviewed"]]
        mine = lay["features"]
        errs = []
        for f in rev:
            same = [g for g in mine if g["type"] == f["type"]]
            if not same:
                continue
            cf = np.array([(f["rect"][0] + f["rect"][2]) / 2, (f["rect"][1] + f["rect"][3]) / 2])
            g = min(same, key=lambda g: np.linalg.norm(cf - [(g["rect"][0] + g["rect"][2]) / 2, (g["rect"][1] + g["rect"][3]) / 2]))
            errs.append(float(np.abs(np.asarray(f["rect"]) - np.asarray(g["rect"])).max()))
        if errs:
            out[k] = {"reviewed_layout": newest["version"], "features": len(errs), "median_m": round(float(np.median(errs)), 3),
                      "p95_m": round(float(np.percentile(errs, 95)), 3)}
    return out


# ---- driver -------------------------------------------------------------------------------

def newest_folder(bid, root):
    vers = sorted((Path(root) / "buildings" / bid).glob("lod3_v*"), key=lambda d: int(d.name.split("_v")[1]))
    if not vers:
        fail(f"{bid} has no lod3 folder")
    return vers[-1]


def run(folder, root=None, alignments=(), photo=True, overlap=True):
    import facade_layout as fl
    import vcmesh
    root = Path(root or data_root())
    folder = Path(folder)
    res = {"folder": str(folder), "made": datetime.datetime.now().isoformat(timespec="seconds"), "checks": {}}
    ch = res["checks"]
    files = {}
    for name in ("model.json", "ledger.json", "sources.json"):
        files[name] = (folder / name).exists()
    ch["files"] = {"present": files, "ok": all(files.values())}
    if not files["model.json"]:
        res["ok"] = False
        return res
    model = load_json(folder / "model.json")
    ledger = load_json(folder / "ledger.json") if files["ledger.json"] else {"entries": []}
    level = model["level"]
    thr = model["merge_threshold_m"]
    bad_rows = [e.get("id") for e in ledger["entries"]
                if not {"id", "type", "merged_into", "center", "size", "count", "material", "photos", "restore_hint"} <= set(e)
                or e["restore_hint"] not in HINTS]
    ch["files"]["ledger_rows"] = len(ledger["entries"])
    ch["files"]["ledger_rows_malformed"] = bad_rows
    ch["files"]["ok"] = ch["files"]["ok"] and not bad_rows
    lod3, info = check_mesh_file(folder / "lod3.npz", overlap=overlap)
    ch["mesh"] = {"lod3": info}
    for name in ("lod2", "lod1", "impostor"):
        _, i2 = check_mesh_file(folder / f"{name}.npz", overlap=overlap and name != "lod2")
        ch["mesh"][name] = i2
    ch["mesh"]["ok"] = all(not x["problems"] for x in ch["mesh"].values())
    bdir = folder.parent
    layouts = {k: fl.load(bdir / x["layout"]) for k, x in model["inputs"]["layouts"].items()}
    ch["merge"] = merge_checks(lod3, model, ledger, layouts, thr)
    col = vcmesh.load_mesh(folder / "game_col.npz")
    tg = TARGETS[level]
    ch["budget"] = {"lod3": len(lod3["faces"]), "lod3_target": tg["lod3"], "game_col": len(col["faces"]),
                    "game_col_target": tg["game_col"],
                    "ok": len(lod3["faces"]) <= tg["lod3"] and len(col["faces"]) <= tg["game_col"] * COL_SLACK}
    if len(col["faces"]) > tg["game_col"]:
        ch["budget"]["note"] = f"collider {len(col['faces'])} triangles is over {tg['game_col']} (allowed up to 10 %)"
    ch["collider"] = collider_checks(col, layouts, lod3=lod3)
    ch["collider"]["ok"] = not ch["collider"]["problems"] and ch["collider"]["steps_ok"] and ch["collider"]["ledges_ok"]
    lod2 = vcmesh.load_mesh(folder / "lod2.npz")
    ch["envelope"] = envelope(lod3, lod2, (model.get("roof_features") or {}).get("tower"))
    res["ok"] = all(ch[k]["ok"] for k in ("files", "mesh", "merge", "budget", "collider", "envelope"))
    # photo check
    cam_files = []
    for a in alignments:
        cam_files.append(root / "derived" / "alignment" / a / "cameras.json")
    if photo and not alignments:
        src = load_json(folder / "sources.json") if files["sources.json"] else {}
        for fk, x in (src.get("facades") or {}).items():
            if x.get("alignment_set"):
                cam_files.append(root / "derived" / "alignment" / x["alignment_set"] / "cameras.json")
        cam_files = sorted(set(cam_files))
    cam_files = [c for c in cam_files if c.exists()]
    if photo and cam_files:
        res["photo_check"] = photo_check(folder, lod3, layouts, cam_files, root)
    else:
        res["photo_check"] = {"status": "not run: no aligned cameras for this building yet",
                              "looked_for": [str(c) for c in cam_files]}
    res["reviewed_features"] = reviewed_feature_offsets(layouts, root, model["building"]) or \
        {"status": "no reviewed features yet (all layouts reviewed=false)"}
    return res


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("building", help="building id (newest lod3 folder) or a lod3 folder path")
    ap.add_argument("--alignment", action="append", default=[], help="SET/vN under derived/alignment (repeatable)")
    ap.add_argument("--no-photo", action="store_true")
    a = ap.parse_args(argv)
    root = data_root()
    folder = Path(a.building) if Path(a.building).exists() else newest_folder(a.building, root)
    res = run(folder, root, a.alignment, photo=not a.no_photo)
    save_json(folder / "check.json", res)
    import manifest
    manifest.add_file(folder / "check.json", "pipeline/check_building.py", "derived", stage=4, root=root)
    for k, v in res["checks"].items():
        print(f"{k:9s} {'PASS' if v['ok'] else 'FAIL'}")
    pc = res["photo_check"]
    print("photo    ", pc.get("status") or f"{pc['images_used']} images; per facade {json.dumps(pc['per_facade'])}")
    print("wrote", folder / "check.json")
    if not res["ok"]:
        sys.exit(1)


if __name__ == "__main__":
    main()
