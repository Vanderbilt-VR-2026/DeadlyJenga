"""Overview pictures for the Stage 5 props and species trees (small PNGs for git).

  stage5-props-library.png   contact sheet of the prop library (LOD0 big, LOD1 small)
  stage5-trees-species.png   contact sheet of the six species (small / medium / full, LOD1)
  stage5-props-area.png      oblique view of one area: terrain colors, buildings v2,
                             trees v2 and props
All three go to Docs/Development/VanderbiltCampus/progress/images/ (each kept under 600 KB)
and full-size copies to reports/stage5/ in the archive (manifest, stage 5).

The renderer is a tiny painter's-algorithm drawer (no GPU): triangles are sorted far to
near, back faces skipped, and shaded by one light. Colors are the candy palette of each
material id (config/material_ids.json).
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
import manifest  # noqa: E402
import props_kit as pk  # noqa: E402
import tree_species as ts  # noqa: E402
import vcmesh  # noqa: E402
from common import CONFIG, REPO, data_root, load_json  # noqa: E402

GIT_IMAGES = REPO / "Docs/Development/VanderbiltCampus/progress/images"
MAX_BYTES = 600_000
BG = (236, 240, 246)


def palette():
    cols = {}
    for m in load_json(CONFIG / "material_ids.json")["ids"]:
        h = m["candy"].lstrip("#")
        cols[m["index"]] = np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], float)
    return cols


PAL = palette()


def project(V, az, el):
    """World -> (u, v, depth) for a camera turned az degrees about Z and looking down el degrees."""
    a, e = np.radians(az), np.radians(el)
    x = V[:, 0] * np.cos(a) - V[:, 1] * np.sin(a)
    y = V[:, 0] * np.sin(a) + V[:, 1] * np.cos(a)
    z = V[:, 2]
    u = x
    v = y * np.sin(e) + z * np.cos(e)
    depth = y * np.cos(e) - z * np.sin(e)
    return u, v, depth, np.array([0, np.cos(e), -np.sin(e)]), a


def draw_meshes(draw, meshes, az, el, to_px, light=(0.45, -0.35, 0.82), cull=True):
    """Draw a list of mesh dicts with the given screen mapping to_px(u, v) -> (px, py)."""
    tris, cols, depths = [], [], []
    L = np.asarray(light, float); L /= np.linalg.norm(L)
    for m in meshes:
        V, F = np.asarray(m["verts"], float), m["faces"]
        if not len(F):
            continue
        u, v, d, D, a = project(V, az, el)
        A, B, C = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
        n = np.cross(B - A, C - A)
        n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
        # rotate normals into the camera frame to test facing
        nx = n[:, 0] * np.cos(a) - n[:, 1] * np.sin(a)
        ny = n[:, 0] * np.sin(a) + n[:, 1] * np.cos(a)
        facing = nx * 0 + ny * D[1] + n[:, 2] * D[2]
        keep = facing < 0 if cull else np.ones(len(F), bool)
        shade = 0.45 + 0.55 * np.clip(n @ L, 0, 1)
        base = np.array([PAL.get(int(k), np.array([154, 154, 154.0])) for k in m["face_mat"]])
        col = np.clip(base * shade[:, None], 0, 255).astype(int)
        px, py = to_px(u, v)
        P = np.stack([px[F], py[F]], axis=2)                   # (M, 3, 2)
        tris.append(P[keep]); cols.append(col[keep]); depths.append(d[F].mean(1)[keep])
    if not tris:
        return
    P, C, Dp = np.vstack(tris), np.vstack(cols), np.concatenate(depths)
    for i in np.argsort(-Dp, kind="stable"):
        draw.polygon([tuple(p) for p in P[i]], fill=tuple(C[i]))


def fit_mapping(meshes, az, el, box, margin=0.08):
    """Screen mapping that fits the meshes into box (x0, y0, x1, y1) in pixels."""
    us, vs = [], []
    for m in meshes:
        V = np.asarray(m["verts"], float)
        if len(m["faces"]):
            V = V[np.unique(m["faces"])]            # only the vertices the (clipped) faces use
        u, v, _, _, _ = project(V, az, el)
        us.append(u); vs.append(v)
    u, v = np.concatenate(us), np.concatenate(vs)
    x0, y0, x1, y1 = box
    w, h = (x1 - x0) * (1 - 2 * margin), (y1 - y0) * (1 - 2 * margin)
    s = min(w / max(np.ptp(u), 1e-6), h / max(np.ptp(v), 1e-6))
    cu, cv = (u.min() + u.max()) / 2, (v.min() + v.max()) / 2
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    return lambda uu, vv: (cx + (uu - cu) * s, cy - (vv - cv) * s), s


def font(size):
    from render_stage0 import font as f
    return f(size)


def save_small(img, path, max_bytes=MAX_BYTES):
    path.parent.mkdir(parents=True, exist_ok=True)
    im = img
    while True:
        q = im.quantize(colors=128, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
        q.save(path, "PNG", optimize=True)
        if path.stat().st_size <= max_bytes:
            return im.size, path.stat().st_size
        im = im.resize((int(im.width * 0.85), int(im.height * 0.85)), Image.LANCZOS)


def contact_sheet(items, cols, cell, title, ss=2):
    """items: list of (label, [meshes], small_meshes or None). Returns a PIL image."""
    rows = (len(items) + cols - 1) // cols
    W, H = cols * cell * ss, (rows * cell + 60) * ss
    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img)
    f, fs = font(20 * ss), font(14 * ss)
    d.text((16 * ss, 14 * ss), title, font=f, fill=(30, 30, 40))
    for k, (label, big, small) in enumerate(items):
        r, c = divmod(k, cols)
        x0, y0 = c * cell * ss, (60 + r * cell) * ss
        d.rectangle([x0 + 4 * ss, y0 + 4 * ss, x0 + (cell - 4) * ss, y0 + (cell - 4) * ss], outline=(200, 205, 215),
                    width=ss)
        box = (x0, y0 + 10 * ss, x0 + int(cell * (0.72 if small else 1.0)) * ss, y0 + (cell - 34) * ss)
        to_px, _ = fit_mapping(big, 35, 28, box)
        draw_meshes(d, big, 35, 28, to_px)
        if small:
            sbox = (x0 + int(cell * 0.68) * ss, y0 + int(cell * 0.45) * ss, x0 + cell * ss, y0 + (cell - 34) * ss)
            to_px2, _ = fit_mapping(small, 35, 28, sbox, margin=0.15)
            draw_meshes(d, small, 35, 28, to_px2)
        d.text((x0 + 12 * ss, y0 + (cell - 30) * ss), label, font=fs, fill=(30, 30, 40))
    return img.resize((W // ss, H // ss), Image.LANCZOS)


def props_sheet():
    items = []
    for k in pk.KINDS:
        m0, m1 = pk.prop_mesh(k, 0), pk.prop_mesh(k, 1)
        items.append((f"{k}  LOD0 {len(m0['faces'])} / LOD1 {len(m1['faces'])} tris", [m0], [m1]))
    return contact_sheet(items, 4, 300, "Stage 5 prop library (made in code; candy palette per material id; "
                                         "sign faces and flag are signage_blank)")


def species_sheet():
    items = []
    dims = {"small": (6.0, 5.0, 1.8), "medium": (11.0, 8.5, 3.0), "full": (18.0, 14.0, 4.5)}
    for s in ts.SPECIES:
        meshes, x = [], 0.0
        for d in ("small", "medium", "full"):
            h, w, b = dims[d]
            if s == "conifer":
                w = h * 0.4
            if s == "ornamental" and d != "small":
                h, w = h * 0.7, w * 0.8
            t = {"id": f"demo_{s}_{d}", "x": x, "y": 0.0, "ground_z": 0.0, "height": h, "crown_diameter": w,
                 "crown_base": b, "species": s}
            meshes.append(ts.tree_mesh(t, 0))
            x += w / 2 + 7.0 + dims["full"][1] / 4
        t1 = {"id": f"demo_{s}_lod1", "x": 0.0, "y": 0.0, "ground_z": 0.0, "height": 18.0,
              "crown_diameter": 7.2 if s == "conifer" else 14.0, "crown_base": 4.5, "species": s}
        tris = [len(ts.species_mesh(s, d, 0)["faces"]) for d in ("small", "medium", "full")]
        items.append((f"{s}: {tris[0]}/{tris[1]}/{tris[2]} tris, LOD1 {len(ts.species_mesh(s, 'full', 1)['faces'])}",
                      meshes, [ts.tree_mesh(t1, 1)]))
    return contact_sheet(items, 3, 380, "Stage 5 tree species (small <= 7 m, medium <= 13 m, full; right: LOD1)")


def clip(m, x0, y0, x1, y1):
    V, F = m["verts"], m["faces"]
    c = V[F].mean(1)
    k = (c[:, 0] >= x0) & (c[:, 0] < x1) & (c[:, 1] >= y0) & (c[:, 1] < y1)
    return {"verts": V, "faces": F[k], "face_mat": m["face_mat"][k], "uv0": m["uv0"][k]}


def area_view(root, cx, cy, half):
    x0, y0, x1, y1 = cx - half, cy - half, cx + half, cy + half
    tiles = {fr.tile_of(x, y) for x in np.arange(x0, x1 + 1, 50) for y in np.arange(y0, y1 + 1, 50)}
    meshes = []
    for t in sorted(tiles):
        for p in (root / "derived/terrain/v1" / t / "terrain.npz", root / "derived/trees/v2" / t / "trees_lod0.npz",
                  root / "derived/props/v1/tiles" / t / "props_lod0.npz"):
            if p.exists():
                meshes.append(clip(vcmesh.load_mesh(p), x0, y0, x1, y1))
    idx = root / "derived/buildings/v2/index.json"
    nb = 0
    if idx.exists():
        for b in load_json(idx)["buildings"]:
            if b["tile"] in tiles:
                p = root / "buildings" / b["id"] / b.get("version", "v2") / "lod2.npz"
                if p.exists():
                    m = vcmesh.load_mesh(p)
                    c = m["verts"].mean(0)
                    if x0 < c[0] < x1 and y0 < c[1] < y1:          # whole buildings whose center is inside
                        meshes.append(m); nb += 1
    ss, W, H = 2, 1400, 1000
    img = Image.new("RGB", (W * ss, H * ss), BG)
    d = ImageDraw.Draw(img)
    az, el = 30, 42
    to_px, _ = fit_mapping([m for m in meshes if len(m["faces"])], az, el, (0, 40 * ss, W * ss, H * ss), margin=0.02)
    draw_meshes(d, meshes, az, el, to_px, cull=True)
    f = font(18 * ss)
    d.rectangle([0, 0, W * ss, 40 * ss], fill=(25, 28, 36))
    d.text((12 * ss, 9 * ss), f"Stage 5 area view around ({cx:.0f}, {cy:.0f}), {2 * half:.0f} m: terrain v1 colors, "
                              f"{nb} buildings v2, trees v2 (species), props v1 (OSM + rule)", font=f, fill=(255, 255, 255))
    return img.resize((W, H), Image.LANCZOS)


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--center", type=float, nargs=2, default=[150.0, -160.0], help="area view center (campus m)")
    p.add_argument("--half", type=float, default=50.0, help="area view half size (m)")
    p.add_argument("--no-manifest", action="store_true")
    args = p.parse_args(argv)
    root = data_root()
    out = {"stage5-props-library.png": props_sheet(), "stage5-trees-species.png": species_sheet(),
           "stage5-props-area.png": area_view(root, args.center[0], args.center[1], args.half)}
    files = []
    for name, img in out.items():
        full = root / "reports/stage5" / name
        full.parent.mkdir(parents=True, exist_ok=True)
        img.save(full, "PNG", optimize=True)
        files.append(full)
        size, nbytes = save_small(img, GIT_IMAGES / name)
        print(f"{name}: {size[0]}x{size[1]} px, {nbytes / 1000:.0f} KB")
    if not args.no_manifest:
        db = manifest.connect(root)
        for f in files:
            manifest.add_file(f, "props_render.py", "Derived renders (OSM ODbL, USGS LiDAR)", 5, root=root, db=db)
        db.commit(); db.close()


if __name__ == "__main__":
    main()
