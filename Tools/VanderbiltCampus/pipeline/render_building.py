"""Offline review renders of a VC mesh (NumPy + OpenCV, CPU only, no Blender or GPU).

Painter's algorithm: back faces are culled, the rest are sorted far to near and filled with the
candy palette color of their material (config/material_ids.json) shaded by a sun and sky light.
Drawn at 2x and shrunk for smooth edges. Cameras use the COLMAP convention (x right, y down,
z forward; x_cam = R @ X + t), so the same function renders "the model from a photo's camera"
for check_building.py.

    python pipeline/render_building.py <lod3 folder> NAME [--commons]
writes Docs/Development/VanderbiltCampus/progress/images/stage4-NAME-1..6.png (four sides, two
obliques; each kept under 600 KB) and with --commons stage4-NAME-commons.png (the Kirkland
render beside the Commons reference photo from a similar viewpoint).
"""
import argparse
import io
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CONFIG, REPO, data_root, load_json  # noqa: E402

IMAGES = REPO / "Docs" / "Development" / "VanderbiltCampus" / "progress" / "images"
MAX_BYTES = 600 * 1024


def palette(kind="candy"):
    ids = load_json(CONFIG / "material_ids.json")["ids"]
    pal = np.zeros((256, 3))
    pal[:] = (154, 154, 154)
    for m in ids:
        h = m[kind].lstrip("#")
        pal[m["index"]] = [int(h[i:i + 2], 16) for i in (0, 2, 4)]
    return pal


def look_at(eye, target, up=(0, 0, 1)):
    """World-to-camera R, t for a camera at eye looking at target (x right, y down, z forward)."""
    eye, target = np.asarray(eye, float), np.asarray(target, float)
    z = target - eye
    z /= np.linalg.norm(z)
    x = np.cross(z, up)
    if np.linalg.norm(x) < 1e-6:
        x = np.array([1.0, 0, 0])
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    R = np.stack([x, y, z])
    return R, -R @ eye


def intrinsics(w, h, hfov_deg):
    f = (w / 2) / math.tan(math.radians(hfov_deg) / 2)
    return {"fx": f, "fy": f, "cx": w / 2, "cy": h / 2}


def render(meshes, K, R, t, w, h, bg=(236, 240, 246), kind="candy", ss=2, light=(-0.35, -0.55, 0.76),
           project=None, edges=True, under=()):
    """Render a list of VC mesh dicts. K: fx, fy, cx, cy (pixels at the output size).
    project: optional function world (N,3) -> (pixels (N,2), depth (N,)) for real photo cameras.
    under: meshes drawn first, before everything else (the ground).
    Returns an HxWx3 uint8 RGB image."""
    import cv2
    pal = palette(kind)
    tris, cols, first = [], [], []
    L = np.asarray(light, float)
    L /= np.linalg.norm(L)
    C = -R.T @ t
    for k, m in enumerate(list(under) + list(meshes)):
        T = m["verts"][m["faces"]]
        n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
        n /= np.maximum(np.linalg.norm(n, axis=1), 1e-12)[:, None]
        facing = ((C - T.mean(axis=1)) * n).sum(axis=1) > 0
        T, n, fm = T[facing], n[facing], m["face_mat"][facing]
        shade = 0.55 + 0.45 * np.clip(n @ L, 0, 1) + 0.08 * n[:, 2]
        tris.append(T)
        cols.append(np.clip(pal[fm] * shade[:, None], 0, 255))
        first.append(np.full(len(T), k < len(under)))
    if not tris:
        return np.full((h, w, 3), bg, np.uint8)
    T = np.concatenate(tris)
    col = np.concatenate(cols)
    first = np.concatenate(first)
    P = T.reshape(-1, 3)
    if project is None:
        Xc = P @ R.T + t
        z = Xc[:, 2]
        with np.errstate(divide="ignore", invalid="ignore"):
            px = np.stack([K["fx"] * Xc[:, 0] / z + K["cx"], K["fy"] * Xc[:, 1] / z + K["cy"]], 1)
    else:
        px, z = project(P)
    px = px.reshape(-1, 3, 2) * ss
    z = z.reshape(-1, 3)
    ok = (z > 0.2).all(axis=1) & np.isfinite(px).all(axis=(1, 2))
    ok &= (np.abs(px) < 1e5).all(axis=(1, 2))
    px, z, col, first = px[ok], z[ok], col[ok], first[ok]
    order = np.lexsort((-z.mean(axis=1), ~first))         # ground first, then far to near
    img = np.zeros((h * ss, w * ss, 3), np.uint8)
    img[:] = bg[::-1]
    pts = np.round(px * 16).astype(np.int32)
    for i in order:
        c = col[i]
        cv2.fillConvexPoly(img, pts[i], (int(c[2]), int(c[1]), int(c[0])), lineType=cv2.LINE_AA, shift=4)
        if edges:
            cv2.polylines(img, [pts[i]], True, (int(c[2]), int(c[1]), int(c[0])), 1,
                          lineType=cv2.LINE_AA, shift=4)
    img = cv2.resize(img, (w, h), interpolation=cv2.INTER_AREA)
    return img[:, :, ::-1].copy()


def ground_quad(center, size, z, mat="grass"):
    import vcmesh
    i = vcmesh.material_index()[mat]
    cx, cy = center
    s = size
    v = np.array([[cx - s, cy - s, z], [cx + s, cy - s, z], [cx + s, cy + s, z], [cx - s, cy + s, z]])
    return {"verts": v, "faces": np.array([[0, 1, 2], [0, 2, 3]]), "face_mat": np.array([i, i], np.uint8)}


def save_png(img, path, max_bytes=MAX_BYTES):
    """PNG under max_bytes: full color if it fits, else 256-color palette, else smaller."""
    from PIL import Image
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    im = Image.fromarray(img)
    for attempt in range(6):
        buf = io.BytesIO()
        cand = im if attempt == 0 else im.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
        cand.save(buf, "PNG", optimize=True)
        if buf.tell() <= max_bytes:
            path.write_bytes(buf.getvalue())
            return path, buf.tell()
        if attempt >= 1:
            im = im.resize((int(im.width * 0.85), int(im.height * 0.85)), Image.LANCZOS)
    path.write_bytes(buf.getvalue())
    return path, buf.tell()


def label(img, text):
    import cv2
    out = img.copy()
    cv2.rectangle(out, (0, 0), (out.shape[1], 30), (255, 255, 255), -1)
    cv2.putText(out, text, (10, 21), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (40, 40, 60), 1, cv2.LINE_AA)
    return out


def six_views(mesh, axis_u, ground_z, w=1280, h=860, hfov=40.0, title=""):
    """Four sides (along +/- the main wall direction and its normal) and two obliques."""
    v = mesh["verts"]
    lo, hi = v.min(axis=0), v.max(axis=0)
    c = (lo + hi) / 2
    radius = np.linalg.norm(hi - lo) / 2
    dist = radius / math.tan(math.radians(hfov) / 2) * 1.08
    u = np.array([axis_u[0], axis_u[1], 0.0])
    u /= np.linalg.norm(u)
    nrm = np.cross(u, [0, 0, 1.0])
    views = [("side +n", nrm, 8), ("side +u", u, 8), ("side -n", -nrm, 8), ("side -u", -u, 8),
             ("oblique", (nrm + u) / np.linalg.norm(nrm + u), 35), ("oblique", (-nrm - u) / np.linalg.norm(nrm + u), 35)]
    gq = ground_quad(c[:2], radius * 3, ground_z)
    K = intrinsics(w, h, hfov)
    out = []
    for name, d, elev in views:
        e = math.radians(elev)
        eye = c + dist * (np.cos(e) * d + np.sin(e) * np.array([0, 0, 1.0]))
        R, t = look_at(eye, c)
        img = render([mesh], K, R, t, w, h, under=[gq])
        az = (math.degrees(math.atan2(d[0], d[1])) % 360)
        out.append(label(img, f"{title}  view from azimuth {az:.0f} deg, elevation {elev} deg ({name})"))
    return out


def commons_pair(mesh, folder, root, ground_z):
    """Kirkland: model rendered from the foot of the main stair, beside the Commons photo."""
    import cv2
    import facade_layout as fl
    from PIL import Image
    model = load_json(folder / "model.json")
    best = None
    for k, info in model["inputs"]["layouts"].items():
        lay = load_json(folder.parent / info["layout"])
        doors = [f for f in lay["features"] if f["type"] == "door" and (f["depth_m"] or 0) <= -0.8]
        st = [f for f in lay["features"] if f["type"] == "steps"]
        if doors and st:
            best = (lay, doors[0], min(st, key=lambda s: abs((s["rect"][0] + s["rect"][2]) / 2 - (doors[0]["rect"][0] + doors[0]["rect"][2]) / 2)))
    if best is None:
        return None
    lay, d, s = best
    cu = (d["rect"][0] + d["rect"][2]) / 2
    eye = fl.to_world(lay, cu, s["rect"][1] + 1.6, s["depth_m"] + 1.0)
    tgt = fl.to_world(lay, cu, d["rect"][1] + 2.2, 0.0)
    photo = Image.open(Path(root) / "derived/capture/commons-2026-11-19/blurred/wikimedia/Kirkland_Hall/2019_Sit-in.jpg").convert("RGB")
    W, H = photo.size
    R, t = look_at(eye, tgt)
    gq = ground_quad(eye[:2], 60, ground_z)
    img = render([mesh], intrinsics(W, H, 62.0), R, t, W, H, under=[gq])
    both = np.concatenate([label(np.asarray(photo), "Commons photo 2019_Sit-in.jpg (public domain; faces blurred)"),
                           label(img, "Stage 4 stand-in model, camera at the foot of the main stair (estimated)")], 1)
    return both


def review_set(folder, name, commons=False, root=None):
    """Write the six review views (and the Commons pair) for a lod3 folder. Returns the paths."""
    import vcmesh
    folder = Path(folder)
    root = Path(root or data_root())
    mesh = vcmesh.load_mesh(folder / "lod3.npz")
    model = load_json(folder / "model.json")
    lay0 = next(iter(model["inputs"]["layouts"].values()))
    lay = load_json(folder.parent / lay0["layout"])
    ground = lay["plane"]["origin"][2] - 0.05
    title = f"{model['name'] or model['building']} {model['lod_reached']} ({model['style_kit']}, " \
            f"{'measured' if model['measured'] else 'ESTIMATED layout'}) {model['triangles']['lod3']} tris"
    paths = []
    for i, img in enumerate(six_views(mesh, lay["plane"]["u"], ground, title=title)):
        p, n = save_png(img, IMAGES / f"stage4-{name}-{i + 1}.png")
        paths.append((str(p), n))
    if commons:
        both = commons_pair(mesh, folder, root, ground)
        if both is not None:
            p, n = save_png(both, IMAGES / f"stage4-{name}-commons.png")
            paths.append((str(p), n))
    return paths


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("folder")
    ap.add_argument("name")
    ap.add_argument("--commons", action="store_true")
    a = ap.parse_args(argv)
    for p in review_set(a.folder, a.name, a.commons):
        print(p)


if __name__ == "__main__":
    main()
