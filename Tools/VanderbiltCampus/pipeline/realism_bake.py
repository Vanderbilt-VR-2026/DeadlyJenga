"""Stage 5 realism readiness test: a CPU low-resolution photo bake of one facade onto UV0.

It proves the archive (photos), UV0 and the alignment are enough to texture a building later.
Steps:
  1. pick the building and facade: the given --building (default B_544413714, Tibbot Art Center)
     and the facade (layout plane) that the most reliable cameras see; the model is the
     building's newest lod3 mesh (else its v2 LoD2 mesh);
  2. rasterize the facade's outward-facing triangles in UV0 space into a texture (--size px on the
     longer side of the facade's UV rectangle) and get each texel's 3D point;
  3. for every reliable registered photo of --alignment (campus.reliable true in cameras.json):
     project the texel points, keep the ones in the frame, in front of the camera, at incidence
     <= 70 deg (as align_sources.py), not hidden (ray test against this model and the LoD2 meshes
     of every building within 200 m), and not masked (people / cars masks from intake);
  4. exposure: if a gray-card frame exists for the set it would set the white balance and exposure.
     None exists today, so each photo gets one gain that brings its mean brightness on the facade
     to the median of all photos (a simple equalization, logged in the JSON);
  5. blend: weight = cos(incidence) / distance^2, softened near the image border;
  6. write derived/bake/<id>/v1/: <facade>_bake.png (texture), <facade>_coverage.png,
     bake.json (building, facade, UV rectangle in UV0, texture size, cameras and gains, coverage,
     credit), all in the manifest; and the review picture
     Docs/Development/VanderbiltCampus/progress/images/stage5-realism-bake.jpg (< 500 KB): the
     palette version and the baked version rendered from the same view, with the credit line.

    python pipeline/realism_bake.py
    python pipeline/realism_bake.py --building B_544413714 --facade F11 --size 1024
"""
import argparse
import datetime
import io
import json
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REPO, data_root, fail, load_json, save_json  # noqa: E402

CREDIT = "Imagery (c) KartaView contributors, CC BY-SA 4.0"
OUT_IMG = REPO / "Docs" / "Development" / "VanderbiltCampus" / "progress" / "images" / "stage5-realism-bake.jpg"
MAX_INC = 70.0


def load_model(bid, root):
    import vcmesh
    vers = sorted((Path(root) / "buildings" / bid).glob("lod3_v*"),
                  key=lambda d: int(d.name.split("_v")[1]) if d.name.split("_v")[1].isdigit() else 0)
    for d in reversed(vers):
        if (d / "lod3.npz").exists():
            return vcmesh.load_mesh(d / "lod3.npz"), str(d.relative_to(root).as_posix()) + "/lod3.npz"
    p = Path(root) / "buildings" / bid / "v2" / "lod2.npz"
    return vcmesh.load_mesh(p), f"buildings/{bid}/v2/lod2.npz"


def reliable_cameras(alignment, root):
    import check_building as cb
    data = load_json(Path(root) / "derived" / "alignment" / alignment / "cameras.json")
    masks = {e["name"]: e.get("mask") for e in data["images"]}
    cams = cb.load_cameras(Path(root) / "derived" / "alignment" / alignment / "cameras.json", root)   # reliable only
    for c in cams:
        c["mask"] = Path(root) / masks[c["id"]] if masks.get(c["id"]) else None
    return cams


def facade_faces(mesh, plane):
    import facade_layout as fl
    T = mesh["verts"][mesh["faces"]]
    n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    n /= np.maximum(np.linalg.norm(n, axis=1), 1e-12)[:, None]
    q = fl.to_facade(plane, T.mean(axis=1))
    W, H = plane["width_m"], plane["height_m"]
    sel = (n @ np.asarray(plane["normal"]) > 0.9) & (np.abs(q[:, 2]) < 1.5) & (q[:, 0] > -0.5) & (q[:, 0] < W + 0.5) \
        & (q[:, 1] > -3) & (q[:, 1] < H + 1)
    return np.where(sel)[0], n


def occluder_scene(mesh, bid, center, root, radius=200.0):
    """Raycasting scene: the model plus the LoD2 meshes of nearby buildings."""
    import open3d as o3d
    import vcmesh
    idx = load_json(Path(root) / "derived" / "buildings" / "v2" / "index.json")["buildings"]
    V, F, off = [mesh["verts"]], [mesh["faces"]], len(mesh["verts"])
    n_other = 0
    for b in idx:
        if b["id"] == bid:
            continue
        c = np.asarray(b["footprint"]).mean(axis=0)
        if np.linalg.norm(c - center[:2]) > radius:
            continue
        p = Path(root) / "buildings" / b["id"] / "v2" / "lod2.npz"
        if p.exists():
            m = vcmesh.load_mesh(p)
            V.append(m["verts"]); F.append(m["faces"] + off); off += len(m["verts"]); n_other += 1
    V, F = np.vstack(V), np.vstack(F)
    o = V.mean(axis=0)
    sc = o3d.t.geometry.RaycastingScene()
    sc.add_triangles(o3d.core.Tensor((V - o).astype(np.float32)), o3d.core.Tensor(F.astype(np.uint32)))
    return sc, o, n_other


def texels(mesh, faces, size):
    """Rasterize the faces in UV0. Returns (uv_rect, shape, texel face index (-1 none), 3D points)."""
    import cv2
    uv = mesh["uv0"][faces].astype(np.float64)
    u0, v0 = uv.reshape(-1, 2).min(axis=0)
    u1, v1 = uv.reshape(-1, 2).max(axis=0)
    s = size / max(u1 - u0, v1 - v0)
    W, H = int(math.ceil((u1 - u0) * s)) + 1, int(math.ceil((v1 - v0) * s)) + 1
    ids = np.full((H, W), -1.0, np.float32)
    px = np.stack([(uv[..., 0] - u0) * s, (v1 - uv[..., 1]) * s], -1)        # row 0 = top (max v)
    for k, t in enumerate(px):
        cv2.fillConvexPoly(ids, np.round(t * 16).astype(np.int32), float(k), lineType=cv2.LINE_8, shift=4)
    ids = ids.astype(np.int64)
    rr, cc = np.nonzero(ids >= 0)
    k = ids[rr, cc]
    p = np.stack([cc + 0.5, rr + 0.5], 1)
    A, B, C = px[k, 0], px[k, 1], px[k, 2]
    d = (B[:, 0] - A[:, 0]) * (C[:, 1] - A[:, 1]) - (C[:, 0] - A[:, 0]) * (B[:, 1] - A[:, 1])
    d = np.where(np.abs(d) < 1e-12, 1e-12, d)
    l1 = ((p[:, 0] - A[:, 0]) * (C[:, 1] - A[:, 1]) - (C[:, 0] - A[:, 0]) * (p[:, 1] - A[:, 1])) / d
    l2 = ((B[:, 0] - A[:, 0]) * (p[:, 1] - A[:, 1]) - (p[:, 0] - A[:, 0]) * (B[:, 1] - A[:, 1])) / d
    l1, l2 = np.clip(l1, 0, 1), np.clip(l2, 0, 1)
    s12 = np.maximum(l1 + l2, 1.0)
    l1, l2 = l1 / s12, l2 / s12
    T = mesh["verts"][mesh["faces"][faces[k]]]
    P = (1 - l1 - l2)[:, None] * T[:, 0] + l1[:, None] * T[:, 1] + l2[:, None] * T[:, 2]
    return (float(u0), float(v0), float(u1), float(v1)), (H, W), rr, cc, k, P


def bake(bid, facade, alignment, size, root):
    import facade_layout as fl
    import open3d as o3d
    from PIL import Image
    mesh, mesh_path = load_model(bid, root)
    planes = {f["facade"]: f["plane"] for f in fl.facades_of(bid, root)}
    for k, lay in fl.load_building(bid, root).items():
        planes.setdefault(k, lay["plane"])
    cams = reliable_cameras(alignment, root)
    if not cams:
        fail(f"no reliable cameras in {alignment}")
    # choose the facade the most reliable cameras see (centre in frame, in front, <= 120 m)
    def seen_by(pl):
        c = fl.to_world(pl, pl["width_m"] / 2, min(pl["height_m"] / 2, 6.0))
        n = np.asarray(pl["normal"])
        out = []
        for cam in cams:
            v = cam["center"] - c
            dist = np.linalg.norm(v)
            if dist > 120 or v @ n < 0.2 * dist:
                continue
            xy, z = cam["project"](c[None])
            if z[0] > 0 and 0 <= xy[0, 0] < cam["w"] and 0 <= xy[0, 1] < cam["h"]:
                out.append(cam)
        return out
    if facade is None:
        facade = max(planes, key=lambda k: (len(seen_by(planes[k])), planes[k]["width_m"]))
    pl = planes[facade]
    cand = seen_by(pl)
    if not cand:
        fail(f"no reliable camera sees {bid}/{facade}")
    faces, fn = facade_faces(mesh, pl)
    uv_rect, (H, W), rr, cc, k, P = texels(mesh, faces, size)
    N = fn[faces[k]]
    center = fl.to_world(pl, pl["width_m"] / 2, pl["height_m"] / 2)
    sc, off, n_other = occluder_scene(mesh, bid, center, root)
    acc = np.zeros((len(P), 3))
    wsum = np.zeros(len(P))
    per_cam = []
    samples = []
    for cam in cand:
        img = np.asarray(Image.open(cam["image"]).convert("RGB"), np.float32)
        sx = img.shape[1] / cam["w"]
        xy, z = cam["project"](P)
        xy = xy * sx
        v = cam["center"] - P
        dist = np.linalg.norm(v, axis=1)
        cosi = (v * N).sum(1) / np.maximum(dist, 1e-9)
        ok = (z > 0.5) & (xy[:, 0] >= 1) & (xy[:, 0] < img.shape[1] - 2) & (xy[:, 1] >= 1) & (xy[:, 1] < img.shape[0] - 2)
        ok &= cosi >= math.cos(math.radians(MAX_INC))
        idx = np.where(ok)[0]
        if len(idx):
            d = P[idx] - cam["center"]
            L = np.linalg.norm(d, axis=1)
            rays = np.c_[np.repeat((cam["center"] - off)[None], len(idx), 0), d / L[:, None]].astype(np.float32)
            hit = sc.cast_rays(o3d.core.Tensor(rays))["t_hit"].numpy()
            idx = idx[hit > L - 0.15]
        if len(idx) and cam.get("mask") and Path(cam["mask"]).exists():
            mk = np.asarray(Image.open(cam["mask"]).convert("L"))
            ms = mk.shape[1] / img.shape[1]
            mx = np.clip((xy[idx, 0] * ms).astype(int), 0, mk.shape[1] - 1)
            my = np.clip((xy[idx, 1] * ms).astype(int), 0, mk.shape[0] - 1)
            idx = idx[mk[my, mx] > 127]
        if len(idx) < 50:
            per_cam.append({"camera": cam["id"], "texels": int(len(idx)), "used": False})
            continue
        # bilinear sample
        x, y = xy[idx, 0], xy[idx, 1]
        x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
        fx, fy = (x - x0)[:, None], (y - y0)[:, None]
        col = (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x0 + 1] * fx * (1 - fy) +
               img[y0 + 1, x0] * (1 - fx) * fy + img[y0 + 1, x0 + 1] * fx * fy)
        border = np.minimum.reduce([x, y, img.shape[1] - x, img.shape[0] - y]) / (0.1 * img.shape[1])
        w = cosi[idx] / np.maximum(dist[idx], 1.0) ** 2 * np.clip(border, 0.05, 1.0)
        samples.append((idx, col, w))
        per_cam.append({"camera": cam["id"], "texels": int(len(idx)), "used": True,
                        "mean_distance_m": round(float(dist[idx].mean()), 1),
                        "mean_incidence_deg": round(float(np.degrees(np.arccos(np.clip(cosi[idx], -1, 1))).mean()), 1),
                        "mean_luma": round(float((col @ [0.299, 0.587, 0.114]).mean()), 1)})
    if not samples:
        fail("no camera contributed (all texels hidden, masked or out of frame)")
    lumas = [p["mean_luma"] for p in per_cam if p.get("used")]
    target = float(np.median(lumas))
    j = 0
    for p in per_cam:
        if not p.get("used"):
            continue
        idx, col, w = samples[j]
        j += 1
        g = target / max(p["mean_luma"], 1.0)
        p["exposure_gain"] = round(g, 3)
        acc[idx] += np.clip(col * g, 0, 255) * w[:, None]
        wsum[idx] += w
    covered = wsum > 0
    tex = np.zeros((H, W, 3), np.uint8)
    cov = np.zeros((H, W), np.uint8)
    rgb = np.where(covered[:, None], acc / np.maximum(wsum, 1e-12)[:, None], 0)
    tex[rr, cc] = np.clip(rgb, 0, 255).astype(np.uint8)
    cov[rr, cc] = np.where(covered, 255, 96)
    info = {"building": bid, "facade": facade, "model": mesh_path, "alignment": alignment,
            "uv_rect": {"u0": uv_rect[0], "v0": uv_rect[1], "u1": uv_rect[2], "v1": uv_rect[3],
                        "note": "UV0 rectangle the texture covers; texture row 0 = v1 (top), column 0 = u0"},
            "texture_px": [W, H], "facade_triangles": int(len(faces)), "facade_texels": int(len(P)),
            "texels_covered": int(covered.sum()), "coverage_pct": round(100.0 * covered.mean(), 1),
            "texel_size_m": round(float(pl["width_m"] / max(1, W)), 4) if W else None,
            "cameras_candidate": len(cand), "cameras_used": sum(1 for p in per_cam if p.get("used")), "cameras": per_cam,
            "exposure": {"gray_card": None,
                         "method": "no gray-card frame exists for this set; per-photo gain to the median mean "
                                   f"brightness of the photos on this facade (target luma {target:.1f})"},
            "occluders": f"this model + {n_other} LoD2 buildings within 200 m",
            "blend": "weight = cos(incidence) / distance^2, softened within 10% of the image border; incidence <= 70 deg",
            "credit": CREDIT, "license": "CC BY-SA 4.0 (KartaView); derived texture keeps the same license",
            "made": datetime.datetime.now().isoformat(timespec="seconds")}
    return mesh, pl, faces, tex, cov, info


def render_pair(mesh, pl, faces, tex, info, w=900, h=640):
    """Ray-cast render (same camera) of the palette version and the baked version, side by side."""
    import cv2
    import facade_layout as fl
    import open3d as o3d
    import render_building as rb
    pal = rb.palette("candy")
    W, Hh = pl["width_m"], pl["height_m"]
    tgt = fl.to_world(pl, W / 2, Hh * 0.45)
    eye = fl.to_world(pl, W * 0.62, 2.0, max(W, Hh) * 0.95)
    R, t = rb.look_at(eye, tgt)
    K = rb.intrinsics(w, h, 55.0)
    off = mesh["verts"].mean(axis=0)
    sc = o3d.t.geometry.RaycastingScene()
    sc.add_triangles(o3d.core.Tensor((mesh["verts"] - off).astype(np.float32)), o3d.core.Tensor(mesh["faces"].astype(np.uint32)))
    jj, ii = np.meshgrid(np.arange(w) + 0.5, np.arange(h) + 0.5)
    d_cam = np.stack([(jj - K["cx"]) / K["fx"], (ii - K["cy"]) / K["fy"], np.ones_like(jj)], -1).reshape(-1, 3)
    d = d_cam @ R
    d /= np.linalg.norm(d, axis=1)[:, None]
    rays = np.c_[np.repeat((eye - off)[None], len(d), 0), d].astype(np.float32)
    r = sc.cast_rays(o3d.core.Tensor(rays))
    hit = np.isfinite(r["t_hit"].numpy())
    prim = r["primitive_ids"].numpy().astype(np.int64)
    bary = r["primitive_uvs"].numpy()
    nrm = r["primitive_normals"].numpy()
    L = np.array([-0.35, -0.55, 0.76]); L /= np.linalg.norm(L)
    shade = 0.55 + 0.45 * np.clip(np.abs(nrm @ L), 0, 1)
    # ground plane and sky
    gz = pl["origin"][2] - 0.05
    tg = (gz - eye[2]) / np.where(np.abs(d[:, 2]) < 1e-9, -1e-9, d[:, 2])
    ground = (~hit) & (tg > 0)
    base = np.tile(np.array([236, 240, 246], float), (len(d), 1))
    base[ground] = pal[20] * 0.9
    pal_img = base.copy()
    m = hit
    pal_img[m] = pal[mesh["face_mat"][prim[m]]] * shade[m, None]
    bake_img = pal_img.copy()
    in_fac = np.zeros(len(mesh["faces"]), bool)
    in_fac[faces] = True
    fm = m & in_fac[np.where(m, prim, 0)]
    f = prim[fm]
    b1, b2 = bary[fm, 0], bary[fm, 1]
    uv = mesh["uv0"][f].astype(np.float64)
    uvp = (1 - b1 - b2)[:, None] * uv[:, 0] + b1[:, None] * uv[:, 1] + b2[:, None] * uv[:, 2]
    ur = info["uv_rect"]
    Th, Tw = tex.shape[:2]
    s = (Tw - 1) / max(ur["u1"] - ur["u0"], 1e-12)
    cx = np.clip(((uvp[:, 0] - ur["u0"]) * s).astype(int), 0, Tw - 1)
    cy = np.clip(((ur["v1"] - uvp[:, 1]) * s).astype(int), 0, Th - 1)
    texc = tex[cy, cx].astype(float)
    have = texc.sum(axis=1) > 0
    sel = np.where(fm)[0]
    bake_img[sel[have]] = texc[have]
    A = pal_img.reshape(h, w, 3).astype(np.uint8)
    B = bake_img.reshape(h, w, 3).astype(np.uint8)
    both = np.concatenate([A, np.full((h, 8, 3), 255, np.uint8), B], 1)
    bar = np.full((70, both.shape[1], 3), 255, np.uint8)
    cv2.putText(bar, f"{info['building']} facade {info['facade']}: palette (left) vs CPU photo bake on UV0 (right), "
                     f"{info['cameras_used']} reliable photos, coverage {info['coverage_pct']}%",
                (10, 24), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (40, 40, 60), 1, cv2.LINE_AA)
    cv2.putText(bar, f"Not Kirkland (no photos yet). Exposure: per-photo equalization (no gray card).   {CREDIT}",
                (10, 52), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (40, 40, 60), 1, cv2.LINE_AA)
    return np.concatenate([bar, both], 0)


def save_jpg(img, path, max_bytes=500 * 1024):
    from PIL import Image
    path.parent.mkdir(parents=True, exist_ok=True)
    for q in (90, 82, 74, 66, 58, 50):
        buf = io.BytesIO()
        Image.fromarray(img).save(buf, "JPEG", quality=q, optimize=True)
        if buf.tell() <= max_bytes:
            break
    path.write_bytes(buf.getvalue())
    return buf.tell()


def run(bid="B_544413714", facade=None, alignment="kartaview-2026-11-19/v1", size=1024, root=None, out_img=OUT_IMG):
    from PIL import Image
    import manifest
    root = Path(root or data_root())
    mesh, pl, faces, tex, cov, info = bake(bid, facade, alignment, size, root)
    out = root / "derived" / "bake" / bid / "v1"
    out.mkdir(parents=True, exist_ok=True)
    tp, cp = out / f"{info['facade']}_bake.png", out / f"{info['facade']}_coverage.png"
    Image.fromarray(tex).save(tp)
    Image.fromarray(cov).save(cp)
    info["files"] = {"texture": tp.name, "coverage": cp.name}
    pair = render_pair(mesh, pl, faces, tex, info)
    if out_img:
        info["review_image"] = str(out_img)
        info["review_image_bytes"] = save_jpg(pair, Path(out_img))
    save_json(out / "bake.json", info)
    for p in (tp, cp, out / "bake.json"):
        manifest.add_file(p, f"pipeline/realism_bake.py from {alignment} photos", info["license"], stage=5, root=root)
    return info


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--building", default="B_544413714")
    ap.add_argument("--facade")
    ap.add_argument("--alignment", default="kartaview-2026-11-19/v1")
    ap.add_argument("--size", type=int, default=1024)
    a = ap.parse_args(argv)
    info = run(a.building, a.facade, a.alignment, a.size)
    print(json.dumps({k: info[k] for k in ("building", "facade", "texture_px", "coverage_pct", "cameras_candidate",
                                           "cameras_used", "review_image_bytes")}, indent=1))


if __name__ == "__main__":
    main()
