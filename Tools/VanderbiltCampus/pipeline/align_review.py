"""Stage 3 review images for an aligned and registered capture set.

    python align_review.py --set kartaview-2026-11-19
    python align_review.py --set kartaview-2026-11-19 --copy-to-docs

Writes to reports/stage3/<set>/:
  topdown.jpg          camera positions (GPS, GPS start, refined) and sparse points over
                       the NAIP orthophoto and the building footprints (campus frame),
                       before and after the LiDAR refinement; one zoom panel on the
                       area with the most facade points
  overlay_<n>.jpg      6 registered photos (blurred copies) with LiDAR building points
                       (colored by distance) and footprint wall edges (ground, eave and
                       corner lines) projected into them, to judge the alignment by eye
Each image is at most 400 KB. --copy-to-docs copies the top-down plot and 3 overlays
(<= 1.2 MB together) to Docs/Development/VanderbiltCampus/progress/images/ as
stage3-<set>-*.jpg. Every image carries the credit line of its photos in the footer.
"""
import argparse
import io
import shutil
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import align_geom as geom  # noqa: E402
from align_sources import project  # noqa: E402
from common import REPO, data_root, fail, load_json  # noqa: E402

CREDITS = {
    "kartaview": "Photos: Imagery (c) KartaView contributors, CC BY-SA 4.0 (kartaview.org; faces/plates blurred)",
    "fallback": "Photos: Legut-Pintal, Maria, Zenodo 16576623, CC BY 4.0",
}
DATA_CREDIT = ("Lidar: USGS 3DEP. Ortho: USDA FPAC Business Center (NAIP). Footprints: (c) OpenStreetMap "
               "contributors (ODbL) / Metro Nashville Open Data")
MAX_BYTES = 400 * 1024


def credit_for(set_name):
    for k, v in CREDITS.items():
        if set_name.startswith(k):
            return v
    return "Photos: see photos.json license field"


def save_jpg(fig_or_img, path, max_bytes=MAX_BYTES, max_width=None):
    """Save a matplotlib figure or a PIL image as JPEG under max_bytes (quality steps down)."""
    from PIL import Image
    if hasattr(fig_or_img, "savefig"):
        buf = io.BytesIO()
        fig_or_img.savefig(buf, format="png", dpi=fig_or_img.dpi)
        buf.seek(0)
        img = Image.open(buf).convert("RGB")
    else:
        img = fig_or_img.convert("RGB")
    if max_width and img.width > max_width:
        img = img.resize((max_width, int(img.height * max_width / img.width)), Image.LANCZOS)
    for q in (88, 82, 76, 70, 62, 55, 48, 40):
        buf = io.BytesIO()
        img.save(buf, format="JPEG", quality=q, optimize=True)
        if buf.tell() <= max_bytes:
            break
    else:
        img = img.resize((int(img.width * 0.8), int(img.height * 0.8)), Image.LANCZOS)
        return save_jpg(img, path, max_bytes)
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_bytes(buf.getvalue())
    return buf.tell()


def read_ortho(root, box):
    import rasterio
    from rasterio.windows import Window
    meta = load_json(root / "derived" / "ortho" / "v1" / "naip2021_campus_0.6m.json")
    ox, oy, cell = meta["origin_x"], meta["origin_y"], meta["cell_m"]
    x0, y0, x1, y1 = box
    c0, c1 = int((x0 - ox) / cell), int(np.ceil((x1 - ox) / cell))
    r0, r1 = int((oy - y1) / cell), int(np.ceil((oy - y0) / cell))
    c0, r0 = max(c0, 0), max(r0, 0)
    c1, r1 = min(c1, meta["width"]), min(r1, meta["height"])
    if c1 <= c0 or r1 <= r0:
        return None, None
    with rasterio.open(root / "derived" / "ortho" / "v1" / meta["file"]) as r:
        img = r.read(window=Window(c0, r0, c1 - c0, r1 - r0))
    img = np.transpose(img[:3], (1, 2, 0))
    ext = (ox + c0 * cell, ox + c1 * cell, oy - r1 * cell, oy - r0 * cell)
    return img, ext


def footprints_in(root, box):
    fp = load_json(root / "derived" / "buildings" / "v1" / "footprints.json")
    x0, y0, x1, y1 = box
    out = []
    for b in fp["buildings"]:
        cx, cy = b["centroid"]
        if x0 - 100 < cx < x1 + 100 and y0 - 100 < cy < y1 + 100:
            out.append(b)
    return out


def topdown(root, set_name, cams, reg, ply, out_path):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    reg_c = [e for e in cams["images"] if e.get("campus") and e["campus"].get("reliable", True)]
    if not reg_c:
        return None
    Cc = np.array([e["campus"]["center"] for e in reg_c])
    G = np.array([[e["gps"]["X"], e["gps"]["Y"]] for e in reg_c if e.get("gps")])
    # GPS-start positions of the same cameras.
    Cs = []
    for e in reg_c:
        T = np.array(reg["transforms"][e["registration_unit"]]["gps_start_4x4"])
        Cs.append(geom.sim_apply(T, np.array(e["colmap"]["center"])[None])[0])
    Cs = np.array(Cs)
    xyz, lab = ply
    # Zoom on the 120 m square with the most facade points.
    fac = xyz[lab == 1]
    if len(fac):
        H, xe, ye = np.histogram2d(fac[:, 0], fac[:, 1], bins=[np.arange(fac[:, 0].min(), fac[:, 0].max() + 60, 60),
                                                                np.arange(fac[:, 1].min(), fac[:, 1].max() + 60, 60)])
        i, j = np.unravel_index(H.argmax(), H.shape)
        zc = np.array([(xe[i] + xe[i + 1]) / 2, (ye[j] + ye[j + 1]) / 2])
    else:
        zc = Cc[:, :2].mean(0)
    zbox = (zc[0] - 60, zc[1] - 60, zc[0] + 60, zc[1] + 60)
    pad = 40
    fbox = (*(Cc[:, :2].min(0) - pad), *(Cc[:, :2].max(0) + pad))
    fps = footprints_in(root, fbox)
    fig, axes = plt.subplots(1, 3, figsize=(16, 7.2), dpi=90)
    titles = ["Overview: refined cameras", "Zoom, GPS start (before LiDAR refinement)", "Zoom, after LiDAR refinement"]
    for ax, title, box, which in zip(axes, titles, (fbox, zbox, zbox), ("after", "before", "after")):
        img, ext = read_ortho(root, box)
        if img is not None:
            ax.imshow(img, extent=ext, interpolation="bilinear")
        for b in fps:
            for poly in b["polygon"]:
                r = np.asarray(poly[0])
                ax.plot(np.r_[r[:, 0], r[0, 0]], np.r_[r[:, 1], r[0, 1]], color="yellow", lw=0.8)
        inb = lambda P: (P[:, 0] > box[0]) & (P[:, 0] < box[2]) & (P[:, 1] > box[1]) & (P[:, 1] < box[3])  # noqa: E731
        if which == "before":
            # Points as they were with the GPS start transform.
            P0 = ply_before
            m = inb(P0)
            ax.scatter(P0[m & (lab == 0), 0], P0[m & (lab == 0), 1], s=0.4, c="white", alpha=0.4, lw=0)
            ax.scatter(P0[m & (lab == 1), 0], P0[m & (lab == 1), 1], s=1.5, c="red", lw=0, label="facade points")
            ax.scatter(P0[m & (lab == 2), 0], P0[m & (lab == 2), 1], s=1.0, c="lime", lw=0, label="ground points")
            ax.scatter(Cs[:, 0], Cs[:, 1], s=10, c="orange", label="cameras (GPS start)")
        else:
            m = inb(xyz)
            ax.scatter(xyz[m & (lab == 0), 0], xyz[m & (lab == 0), 1], s=0.4, c="white", alpha=0.4, lw=0)
            ax.scatter(xyz[m & (lab == 1), 0], xyz[m & (lab == 1), 1], s=1.5, c="red", lw=0, label="facade points")
            ax.scatter(xyz[m & (lab == 2), 0], xyz[m & (lab == 2), 1], s=1.0, c="lime", lw=0, label="ground points")
            ax.scatter(Cc[:, 0], Cc[:, 1], s=6 if box is fbox else 12, c="deepskyblue", label="cameras (refined)")
        if len(G):
            ax.scatter(G[:, 0], G[:, 1], s=6 if box is fbox else 14, marker="x", c="magenta", lw=0.8,
                       label="GPS positions")
        if box is fbox:
            ax.add_patch(plt.Rectangle(zbox[:2], 120, 120, fill=False, ec="cyan", lw=1.2))
        ax.set_xlim(box[0], box[2])
        ax.set_ylim(box[1], box[3])
        ax.set_aspect("equal")
        ax.set_title(title, fontsize=10)
        ax.tick_params(labelsize=7)
        ax.set_xlabel("campus X (m)", fontsize=8)
    axes[0].set_ylabel("campus Y (m)", fontsize=8)
    axes[2].legend(loc="lower left", fontsize=7, markerscale=3, framealpha=0.7)
    fig.suptitle(f"Stage 3 registration: {set_name} (campus frame)", fontsize=11)
    fig.text(0.01, 0.01, credit_for(set_name) + ". " + DATA_CREDIT, fontsize=7)
    fig.tight_layout(rect=(0, 0.03, 1, 0.97))
    n = save_jpg(fig, out_path)
    plt.close(fig)
    return n


ply_before = None


def read_ply(path):
    with open(path, "rb") as f:
        header = []
        while True:
            line = f.readline().decode("ascii").strip()
            header.append(line)
            if line == "end_header":
                break
        n = int([h for h in header if h.startswith("element vertex")][0].split()[-1])
        types = {"double": "<f8", "uchar": "u1", "int": "<i4", "float": "<f4"}
        fields = [(h.split()[2], types[h.split()[1]]) for h in header if h.startswith("property")]
        arr = np.frombuffer(f.read(), dtype=fields, count=n)
    return arr


def overlay(root, e, lidar, walls, out_path, credit, label):
    from PIL import Image, ImageDraw, ImageFont
    img = Image.open(root / e["derived"]).convert("RGB")
    cam = e["camera"]
    sx = img.width / cam["width"]
    R = np.array(e["campus"]["R_cam_from_campus"])
    C = np.array(e["campus"]["center"])
    t = -R @ C
    W = 1280
    s = W / img.width
    img = img.resize((W, int(img.height * s)), Image.LANCZOS)
    dr = ImageDraw.Draw(img, "RGBA")
    # LiDAR building points: color by distance (near = red, far = blue).
    P = lidar
    d = np.linalg.norm(P - C, axis=1)
    m = d < 70
    xy, z = project(cam, R, t, P[m])
    ok = (z > 1) & (xy[:, 0] >= 0) & (xy[:, 0] < cam["width"]) & (xy[:, 1] >= 0) & (xy[:, 1] < cam["height"])
    dd = d[m][ok]
    xy = xy[ok] * sx * s
    order = np.argsort(-dd)
    for (x, y), di in zip(xy[order], dd[order]):
        a = np.clip((di - 5) / 60, 0, 1)
        col = (int(255 * (1 - a)), int(80 + 80 * (1 - abs(a - 0.5) * 2)), int(255 * a), 170)
        dr.ellipse((float(x) - 1.5, float(y) - 1.5, float(x) + 1.5, float(y) + 1.5), fill=col)
    # Footprint walls: ground line, eave line and corners, clipped to points in front of the camera.
    for a, b, z0, z1 in walls:
        if min(np.linalg.norm(a - C[:2]), np.linalg.norm(b - C[:2])) > 80:
            continue
        for zz in (z0, z1):
            seg = np.linspace(0, 1, 25)[:, None]
            XY = a[None] + seg * (b - a)[None]
            P3 = np.column_stack([XY, np.full(len(XY), zz)])
            q, zc = project(cam, R, t, P3)
            q = q * sx * s
            for i in range(len(q) - 1):
                if zc[i] > 1 and zc[i + 1] > 1:
                    dr.line([tuple(q[i]), tuple(q[i + 1])], fill=(255, 255, 0, 230), width=2)
        for corner in (a, b):
            P3 = np.array([[corner[0], corner[1], z0], [corner[0], corner[1], z1]])
            q, zc = project(cam, R, t, P3)
            if (zc > 1).all():
                q = q * sx * s
                dr.line([tuple(q[0]), tuple(q[1])], fill=(255, 255, 0, 230), width=2)
    # Footer.
    foot = 46
    canvas = Image.new("RGB", (img.width, img.height + foot), (20, 20, 20))
    canvas.paste(img, (0, 0))
    dc = ImageDraw.Draw(canvas)
    try:
        font = ImageFont.truetype("arial.ttf", 13)
    except OSError:
        font = ImageFont.load_default()
    dc.text((8, img.height + 4), f"{label}   dots: LiDAR building/unclassified points (red near, blue far); "
                                  f"yellow: footprint walls (ground, eave, corners)", fill=(230, 230, 230), font=font)
    dc.text((8, img.height + 24), credit + ". " + DATA_CREDIT, fill=(200, 200, 200), font=font)
    return save_jpg(canvas, out_path)


def main(argv=None):
    global ply_before
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--set", required=True)
    ap.add_argument("--version", default="v1")
    ap.add_argument("--overlays", type=int, default=6)
    ap.add_argument("--copy-to-docs", action="store_true")
    args = ap.parse_args(argv)
    root = data_root()
    set_name = args.set.strip("/")
    slug = set_name.replace("/", "_")
    out = root / "derived" / "alignment" / set_name / args.version
    cams = load_json(out / "cameras.json")
    reg = load_json(out / "registration.json")
    if not reg.get("registered"):
        fail("set is not registered; nothing to review")
    arr = read_ply(out / "points.ply")
    xyz = np.stack([arr["x"], arr["y"], arr["z"]], 1)
    lab = arr["label"].astype(int)
    unit = arr["unit"].astype(int)
    # Points under the GPS-start transform (for the "before" panel).
    ply_before = np.zeros_like(xyz)
    for ui, k in enumerate(reg["units"]):
        T = reg["transforms"][k]
        m = unit == ui
        Mi = np.linalg.inv(np.array(T["colmap_to_campus_4x4"]))
        ply_before[m] = geom.sim_apply(np.array(T["gps_start_4x4"]), geom.sim_apply(Mi, xyz[m]))
    rep = root / "reports" / "stage3" / slug
    rep.mkdir(parents=True, exist_ok=True)
    made = {}
    made["topdown.jpg"] = topdown(root, set_name, cams, reg, (xyz, lab), rep / "topdown.jpg")
    print(f"topdown.jpg {made['topdown.jpg']} bytes")

    # Overlays: photos with the most facade points within 40 m, spread along the route.
    import lidar_io
    from scipy.spatial import cKDTree
    reg_c = [e for e in cams["images"] if e.get("campus") and e["campus"].get("reliable", True)]
    Cc = np.array([e["campus"]["center"] for e in reg_c])
    fac = xyz[lab == 1]
    score = np.zeros(len(reg_c))
    if len(fac):
        tree = cKDTree(fac[:, :2])
        for i, e in enumerate(reg_c):
            R = np.array(e["campus"]["R_cam_from_campus"])
            nb = tree.query_ball_point(Cc[i, :2], 40)
            if nb:
                q, z = project(e["camera"], R, -R @ Cc[i], fac[nb])
                score[i] = int(((z > 1) & (q[:, 0] > 0) & (q[:, 0] < e["camera"]["width"]) &
                                (q[:, 1] > 0) & (q[:, 1] < e["camera"]["height"])).sum())
    order = np.argsort(-score)
    picks = []
    for i in order:
        if score[i] <= 0 or len(picks) >= args.overlays:
            break
        if all(np.linalg.norm(Cc[i, :2] - Cc[j, :2]) > 40 for j in picks):
            picks.append(i)
    box = (*(Cc[picks, :2].min(0) - 80), *(Cc[picks, :2].max(0) + 80)) if picks else None
    if picks:
        lp = lidar_io.load_points(box[0], box[1], box[2], box[3], classes=[1, 6])
        L = np.stack([lp["x"], lp["y"], lp["z"]], 1)
        g = lidar_io.load_points(box[0], box[1], box[2], box[3], classes=[2])
        L = L[(L[:, 2] - np.median(g["z"]) > 1.5) if len(g["z"]) else slice(None)] if len(L) else L
        L = L[::max(1, len(L) // 300000)]
        walls = []
        for b in footprints_in(root, box):
            gz = (b.get("ground") or {}).get("ground_z")
            ez = (b.get("roof_estimate") or {}).get("eave_z")
            if gz is None or ez is None:
                continue
            for poly in b["polygon"]:
                for a, c, z0, z1, _ in geom.wall_segments([np.asarray(poly[0])], gz, ez):
                    walls.append((a, c, z0, z1))
        credit = credit_for(set_name)
        for n, i in enumerate(picks):
            e = reg_c[i]
            lic = e.get("license") or ""
            seq_credit = credit + (f" [{lic.split('|')[1].strip()}]" if "|" in lic else "")
            label = f"{e['name']}  (camera {Cc[i, 0]:.1f}, {Cc[i, 1]:.1f}, {Cc[i, 2]:.1f} m)"
            p = rep / f"overlay_{n + 1}.jpg"
            made[p.name] = overlay(root, e, L, walls, p, seq_credit, label)
            print(f"{p.name} {made[p.name]} bytes ({e['name']}, {int(score[i])} facade points in view)")
    import manifest
    db = manifest.connect(root)
    for f in rep.glob("*.jpg"):
        manifest.add_file(f, source="pipeline/align_review.py", license="derived; " + credit_for(set_name), stage=3,
                          root=root, db=db)
    db.commit()
    db.close()
    if args.copy_to_docs:
        dst = REPO / "Docs" / "Development" / "VanderbiltCampus" / "progress" / "images"
        dst.mkdir(parents=True, exist_ok=True)
        names = ["topdown.jpg"] + [f"overlay_{k}.jpg" for k in (1, 2, 3)]
        total = 0
        budget = 1_150_000                     # stay under 1.2 MB (decimal) for the four images
        top = rep / "topdown.jpg"
        per = (budget - (top.stat().st_size if top.exists() else 0)) // 3
        from PIL import Image
        for nm in names:
            src = rep / nm
            if src.exists():
                d = dst / f"stage3-{slug}-{nm}"
                if nm == "topdown.jpg" or src.stat().st_size <= per:
                    shutil.copyfile(src, d)
                else:
                    save_jpg(Image.open(src), d, max_bytes=per)
                total += d.stat().st_size
        print(f"copied {len(names)} images to {dst} ({total} bytes)")
        if total > 1_200_000:
            fail(f"docs images total {total} bytes > 1.2 MB")
    return 0


if __name__ == "__main__":
    sys.exit(main())
