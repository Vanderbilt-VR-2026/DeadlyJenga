"""Review images for the Stage 1 buildings (for the lead to eyeball roof quality).

Commands:
  overview                 reports/stage1/buildings_overview.png: every LoD2 model shaded from
                           above over the NAIP 2021 orthophoto, footprint outlines green (pass)
                           or red (fail) from reports/stage1/buildings_accuracy.json.
                           A <= 1.5 MB copy goes to Docs/.../progress/images/stage1-buildings-overview.png
  sheet [--ids ...]        contact sheets: per building an oblique shaded view of the LoD2 mesh
                           next to the LiDAR roof points colored by the model plane they fit
                           (gray = fits no plane within 0.15 m). Default: about 12 representative
                           buildings (Kirkland Hall included). Written to reports/stage1/buildings/.
  --git-copies             also copy two sheets (<= 1 MB each) into the git images folder.
"""
import argparse
import io
import math
import shutil
import sys
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
import numpy as np  # noqa: E402
import shapely  # noqa: E402
from mpl_toolkits.mplot3d.art3d import Poly3DCollection  # noqa: E402

sys.path.insert(0, str(Path(__file__).resolve().parent))
import buildings_common as bc  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
import roof_fit as rf  # noqa: E402
import vcmesh  # noqa: E402
from common import CONFIG, REPO, data_root, load_json  # noqa: E402

GIT_IMAGES = REPO / "Docs" / "Development" / "VanderbiltCampus" / "progress" / "images"


def palette():
    ids = load_json(CONFIG / "material_ids.json")["ids"]
    out = {}
    for m in ids:
        h = m["plain"].lstrip("#")
        out[m["index"]] = np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)]) / 255.0
    return out


def shade(mesh, light=(-0.4, -0.5, 0.75)):
    """Face colors: plain palette color times a simple lambert term."""
    pal = palette()
    v, f = mesh["verts"], mesh["faces"]
    n = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
    L = np.asarray(light, float)
    L /= np.linalg.norm(L)
    k = 0.35 + 0.65 * np.clip(n @ L, 0, 1)
    base = np.array([pal.get(int(m), np.array([0.6, 0.6, 0.6])) for m in mesh["face_mat"]])
    return np.clip(base * k[:, None], 0, 1), n


def load_lod2(bid, root):
    p = bc.building_dir(bid, root) / "lod2.npz"
    return vcmesh.load_mesh(p) if p.exists() else None


# ---- contact sheets -------------------------------------------------------------

def pick_representative(models, n=12):
    """Kirkland Hall, plus the biggest example of each roof type, then the biggest others."""
    chosen = []
    kirk = [m for m in models if (m.get("name") or "").startswith("Kirkland Hall")]
    chosen += kirk[:1]
    by_type = {}
    for m in sorted(models, key=lambda m: -m["footprint"]["area_m2"]):
        by_type.setdefault(m["roof_type"], []).append(m)
    for t in ("gable", "hip", "complex", "mansard", "curved", "pyramidal", "shed", "gambrel", "flat",
              "fallback_flat"):
        for m in by_type.get(t, [])[:1 if t != "complex" else 2]:
            if m not in chosen:
                chosen.append(m)
    # fill with failing buildings so the lead sees problems too
    fails = [m for m in models if m.get("accuracy") and not m["accuracy"].get("pass")]
    for m in sorted(fails, key=lambda m: -m["footprint"]["area_m2"]):
        if len(chosen) >= n:
            break
        if m not in chosen:
            chosen.append(m)
    for m in sorted(models, key=lambda m: -m["footprint"]["area_m2"]):
        if len(chosen) >= n:
            break
        if m not in chosen:
            chosen.append(m)
    return chosen[:n]


def draw_building(ax3, ax2, model, root):
    bid = model["building"]
    mesh = load_lod2(bid, root)
    planes = load_json(bc.building_dir(bid, root) / "roof_planes.json")
    col, _ = shade(mesh)
    v, f = mesh["verts"], mesh["faces"]
    keep = col.sum(axis=1) > 0
    tris = v[f]
    pc = Poly3DCollection(tris, facecolors=col, edgecolors=(0, 0, 0, 0.15), linewidths=0.2)
    ax3.add_collection3d(pc)
    lo, hi = v.min(axis=0), v.max(axis=0)
    d = np.maximum(hi - lo, 1e-3)
    ax3.set_xlim(lo[0], hi[0])
    ax3.set_ylim(lo[1], hi[1])
    ax3.set_zlim(lo[2], hi[2])
    ax3.set_box_aspect((d[0], d[1], d[2]), zoom=1.25)
    ax3.view_init(elev=32, azim=-58)
    ax3.set_axis_off()
    acc = model.get("accuracy") or {}
    status = "PASS" if acc.get("pass") else ("FAIL" if acc else "")
    name = (model.get("name") or "")[:28]
    ax3.set_title(f"{bid} {name}\n{model['roof_type']}, {len(model['parts'])} part(s) {status}", fontsize=8)
    # points colored by plane
    fp = bc.geom_from_json(planes["cells"][0]["polygon"]) if planes["cells"] else None
    cells = [(bc.geom_from_json(cc["polygon"]), cc["plane"]) for cc in planes["cells"]]
    pls = []
    for p in planes["planes"]:
        pl = rf.Plane(p["a"], p["b"], p["c"])
        pl.zlo, pl.zhi = p.get("zlo"), p.get("zhi")
        pls.append(pl)
    allp = shapely.unary_union([c for c, _ in cells])
    x0, y0, x1, y1 = allp.bounds
    pts = lidar_io.load_points(x0, y0, x1, y1, classes=[1, 6], buffer=0.5, root=root)
    ins = shapely.contains_xy(allp.buffer(0.3), pts["x"], pts["y"]) & (pts["z"] > model["ground_z"] + 1.0)
    x, y, z = pts["x"][ins], pts["y"][ins], pts["z"][ins]
    zm = rf.model_z(cells, pls, x, y)
    lab = np.full(len(x), -1)
    if len(x):
        tree = shapely.STRtree([c for c, _ in cells])
        pi, gi = tree.query(shapely.points(np.c_[x, y]), predicate="within")
        lab[pi] = np.array([l for _, l in cells])[gi]
    ok = np.abs(z - zm) < 0.15
    cmap = plt.get_cmap("tab20")
    colors = np.where(ok[:, None], cmap((lab % 20) / 20.0)[:, :3], 0.6)
    ax2.scatter(x, y, c=colors, s=0.4, linewidths=0)
    for c, _ in cells:
        for p in bc.polys_of(c):
            xx, yy = p.exterior.xy
            ax2.plot(xx, yy, "k-", lw=0.3)
    ax2.set_aspect("equal")
    ax2.set_xticks([])
    ax2.set_yticks([])
    e = acc.get("eave_err_m")
    r = acc.get("ridge_err_m")
    fpe = acc.get("footprint_ref_p95_m")
    ax2.set_title(f"LiDAR by plane; fp p95 {fpe}, eave {e}, ridge {r}", fontsize=7)


def sheets(models, root, per_sheet=6, prefix="contact"):
    out = []
    od = bc.reports_dir(root) / "buildings"
    od.mkdir(parents=True, exist_ok=True)
    for s in range(0, len(models), per_sheet):
        group = models[s:s + per_sheet]
        rows = (len(group) + 1) // 2
        fig = plt.figure(figsize=(18, 4.6 * rows))
        for i, m in enumerate(group):
            ax3 = fig.add_subplot(rows, 4, 2 * i + 1, projection="3d")
            ax2 = fig.add_subplot(rows, 4, 2 * i + 2)
            try:
                draw_building(ax3, ax2, m, root)
            except Exception as ex:
                ax2.set_title(f"{m['building']}: {ex!r}", fontsize=7)
        fig.subplots_adjust(left=0.01, right=0.99, top=0.95, bottom=0.02, wspace=0.05, hspace=0.25)
        p = od / f"{prefix}_{s // per_sheet + 1:02d}.png"
        fig.savefig(p, dpi=70)
        plt.close(fig)
        out.append(p)
    return out


# ---- overview ---------------------------------------------------------------------

def overview(models, root, out_png):
    naip_meta = load_json(Path(root) / "derived" / "ortho" / "v1" / "naip2021_campus_0.6m.json")
    allp = shapely.unary_union([bc.geom_from_json(p["polygon"]) for m in models for p in m["parts"]])
    x0, y0, x1, y1 = allp.buffer(20).bounds
    rgb, (ox, oy, cell) = bc.ortho_window("naip2021_campus_0.6m", x0, y0, x1, y1, root)
    W = (x1 - x0)
    H = (y1 - y0)
    scale = 2600 / max(W, H)
    fig = plt.figure(figsize=(W * scale / 100, H * scale / 100), dpi=100)
    ax = fig.add_axes([0, 0, 1, 1])
    h, w, _ = rgb.shape
    ax.imshow(rgb, extent=(ox, ox + w * cell, oy - h * cell, oy), alpha=0.75)
    from matplotlib.collections import PolyCollection
    for m in models:
        mesh = load_lod2(m["building"], root)
        if mesh is None:
            continue
        col, n = shade(mesh, light=(-0.5, 0.4, 0.8))
        up = n[:, 2] > 0.05
        v, f = mesh["verts"], mesh["faces"][up]
        tris = v[f]
        order = np.argsort(tris[:, :, 2].mean(axis=1))
        ax.add_collection(PolyCollection(tris[order][:, :, :2], facecolors=col[up][order], edgecolors="none"))
    for m in models:
        acc = m.get("accuracy") or {}
        color = "#22dd22" if acc.get("pass") else "#ff2222"
        fp = shapely.unary_union([bc.geom_from_json(p["polygon"]) for p in m["parts"]])
        for p in bc.polys_of(fp):
            xx, yy = p.exterior.xy
            ax.plot(xx, yy, color=color, lw=0.6)
    ax.set_xlim(x0, x1)
    ax.set_ylim(y0, y1)
    ax.set_axis_off()
    fig.savefig(out_png, dpi=100)
    plt.close(fig)
    return out_png


def small_copy(src, dst, max_bytes):
    """Save a PNG/JPEG-compressed copy under max_bytes (shrinking until it fits)."""
    from PIL import Image
    im = Image.open(src).convert("RGB")
    dst.parent.mkdir(parents=True, exist_ok=True)
    w, h = im.size
    for s in (1.0, 0.85, 0.7, 0.6, 0.5, 0.4, 0.33, 0.25):
        t = im.resize((int(w * s), int(h * s)), Image.LANCZOS) if s < 1 else im
        buf = io.BytesIO()
        t.quantize(colors=128, method=Image.Quantize.MEDIANCUT).save(buf, "PNG", optimize=True)
        if buf.tell() <= max_bytes:
            dst.write_bytes(buf.getvalue())
            return dst, s
    dst.write_bytes(buf.getvalue())
    return dst, s


def load_models(root):
    out = []
    for d in sorted((Path(root) / "buildings").glob("B_*")):
        p = d / bc.VERSION / "model.json"
        if p.exists():
            m = load_json(p)
            if m.get("zone") == "campus":
                out.append(m)
    return out


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("cmd", choices=["overview", "sheet"])
    p.add_argument("--ids", nargs="*")
    p.add_argument("--prefix", default="contact")
    p.add_argument("--git-copies", action="store_true")
    args = p.parse_args(argv)
    root = data_root()
    models = load_models(root)
    if not models:
        print("ERROR: no building models found; run buildings_lod2.py", file=sys.stderr)
        return 1
    db = manifest.connect(root)
    written = []
    if args.cmd == "overview":
        out = bc.reports_dir(root) / "buildings_overview.png"
        out.parent.mkdir(parents=True, exist_ok=True)
        overview(models, root, out)
        written.append(out)
        if args.git_copies:
            dst, s = small_copy(out, GIT_IMAGES / "stage1-buildings-overview.png", 1_500_000)
            print(f"git copy {dst} (scale {s}, {dst.stat().st_size} bytes)")
    else:
        if args.ids:
            byid = {m["building"]: m for m in models}
            chosen = [byid[i] for i in args.ids if i in byid]
        else:
            chosen = pick_representative(models)
        outs = sheets(chosen, root, prefix=args.prefix)
        written += outs
        if args.git_copies:
            for k, o in enumerate(outs[:2], 1):
                dst, s = small_copy(o, GIT_IMAGES / f"stage1-buildings-sheet-{k}.png", 1_000_000)
                print(f"git copy {dst} (scale {s}, {dst.stat().st_size} bytes)")
    for f in written:
        manifest.add_file(f, source="buildings_render.py", license="derived; NAIP public domain", stage=1,
                          root=root, db=db)
        print(f"wrote {f}")
    db.commit()
    db.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
