"""Stage 0 result images: top-down views with the boundary, ring, origin and tile grid.

  (a) LiDAR heights: nDSM colors over a DSM hillshade      -> reports/stage0/stage0_lidar_height.jpg
  (b) the same lines over the NAIP 2021 orthophoto         -> reports/stage0/stage0_naip2021.jpg
  (c) the same lines over the Metro 2026 orthophoto        -> reports/stage0/stage0_metro2026.jpg
      (Metro imagery has no stated license: archive only, never copied into git)

--git-copies also writes (a) and (b) at <= 2000 px into
Docs/Development/VanderbiltCampus/progress/images/ (LiDAR and NAIP are public domain).
Inputs come from make_rasters.py (derived/rasters/v1, derived/ortho/v1).
"""
import argparse
import sys
from pathlib import Path

import numpy as np
import rasterio
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
from common import REPO, data_root, fail, load_json  # noqa: E402

Image.MAX_IMAGE_PIXELS = None
GIT_IMAGES = REPO / "Docs/Development/VanderbiltCampus/progress/images"
NDSM_MAX = 40.0


def read_raster(path):
    if not path.exists():
        fail(f"missing {path}; run make_rasters.py first")
    side = load_json(path.with_suffix(".json"))
    with rasterio.open(path) as src:
        a = src.read()
    return (a[0] if a.shape[0] == 1 else np.moveaxis(a, 0, 2)), side


def hillshade(z, cell, azimuth=315.0, altitude=45.0):
    """Classic hillshade (0..1) from a height grid, light from the north-west."""
    gy, gx = np.gradient(z, cell)          # rows go south, so gy is -dZ/dY
    slope = np.arctan(np.hypot(gx, gy))
    aspect = np.arctan2(-gy, gx)
    az, alt = np.radians(360.0 - azimuth + 90.0), np.radians(altitude)
    s = np.sin(alt) * np.cos(slope) + np.cos(alt) * np.sin(slope) * np.cos(az - aspect)
    return np.clip(s, 0, 1)


def lidar_picture(root):
    import matplotlib
    dsm, side = read_raster(root / "derived/rasters/v1/dsm.tif")
    ndsm, _ = read_raster(root / "derived/rasters/v1/ndsm.tif")
    shade = hillshade(dsm, side["cell_m"])
    colors = matplotlib.colormaps["turbo"](np.clip(ndsm / NDSM_MAX, 0, 1))[..., :3]
    ground = ndsm < 0.5
    colors[ground] = 0.55                                  # ground: plain gray, shaded
    rgb = colors * (0.30 + 0.70 * shade[..., None])
    # Cells more than 3 m from any LiDAR point were only filled in: draw them dark, not as data.
    from scipy import ndimage
    density, _ = read_raster(root / "derived/rasters/v1/density.tif")
    far = ndimage.distance_transform_edt(density == 0) * side["cell_m"] > 3.0
    rgb[far] = 0.12
    return (rgb * 255).astype(np.uint8), side


def font(size):
    for name in ("arial.ttf", "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def draw_overlay(img, side, polys, tiles, title, credit, legend=False):
    """Draw grid, tile names, boundary, ring, origin, scale bar and text on a PIL image."""
    W, H = img.size
    x0, y_top = side["origin_x"], side["origin_y"]
    cell = side["cell_m"] * side["width"] / W              # meters per output pixel
    to_px = lambda X, Y: ((X - x0) / cell, (y_top - Y) / cell)  # noqa: E731
    tile_px = fr.TILE / cell
    d = ImageDraw.Draw(img, "RGBA")
    lw = max(1, round(W / 1500))

    # Tile grid and names.
    f_tile = font(int(np.clip(tile_px * 0.15, 8, 28)))
    for t in tiles:
        a = to_px(t["bounds"][0], t["bounds"][3])
        b = to_px(t["bounds"][2], t["bounds"][1])
        d.rectangle([a, b], outline=(255, 255, 255, 110), width=lw)
        d.text((a[0] + 0.05 * tile_px, a[1] + 0.04 * tile_px), t["name"], font=f_tile,
               fill=(255, 255, 255, 230), stroke_width=max(1, lw), stroke_fill=(0, 0, 0, 200))

    # Ring outer edge (cyan) and campus boundary (yellow).
    def line(geom, color, width):
        for ring in [geom.exterior, *geom.interiors]:
            d.line([to_px(x, y) for x, y in ring.coords], fill=color, width=width, joint="curve")
    ring = polys["backdrop_ring"]
    d.line([to_px(x, y) for x, y in ring.exterior.coords], fill=(0, 230, 255, 255), width=3 * lw)
    line(polys["campus"], (255, 220, 0, 255), 4 * lw)

    # Origin marker.
    ox, oy = to_px(0.0, 0.0)
    r = max(8, tile_px * 0.12)
    d.ellipse([ox - r, oy - r, ox + r, oy + r], outline=(255, 0, 80, 255), width=3 * lw)
    d.line([ox - 2 * r, oy, ox + 2 * r, oy], fill=(255, 0, 80, 255), width=2 * lw)
    d.line([ox, oy - 2 * r, ox, oy + 2 * r], fill=(255, 0, 80, 255), width=2 * lw)
    f_txt = font(int(np.clip(W / 90, 11, 48)))
    d.text((ox + 2.2 * r, oy - 2.2 * r), "origin (0, 0): Alumni Lawn", font=f_txt, fill=(255, 255, 255, 255),
           stroke_width=2 * lw, stroke_fill=(0, 0, 0, 255))

    # Title box, legend, scale bar.
    pad = int(W / 100)
    lines = [title, "yellow: campus boundary   cyan: 150 m backdrop ring   grid: 100 m tiles T_<col>_<row>",
             "up = grid north (EPSG:6576); campus frame meters", credit]
    tw = max(d.textlength(s, font=f_txt) for s in lines)
    lh = f_txt.size * 1.3
    d.rectangle([pad, pad, pad * 3 + tw, pad * 2 + lh * len(lines)], fill=(0, 0, 0, 170))
    for i, s in enumerate(lines):
        d.text((pad * 2, pad * 1.5 + i * lh), s, font=f_txt, fill=(255, 255, 255, 255))
    bar = 200.0 / cell
    by = H - 3 * pad
    d.rectangle([pad, by - 2.2 * lh, pad * 3 + max(bar, 6 * lh), by + pad], fill=(0, 0, 0, 170))
    d.line([2 * pad, by, 2 * pad + bar, by], fill=(255, 255, 255, 255), width=4 * lw)
    d.text((2 * pad, by - 1.6 * lh), "200 m", font=f_txt, fill=(255, 255, 255, 255))
    if legend:
        lx, ly, lwid, lhgt = W - pad * 3 - 10 * lh, pad, 10 * lh, lh
        import matplotlib
        d.rectangle([lx - pad, ly, W - pad, ly + 3.2 * lh + pad], fill=(0, 0, 0, 170))
        cm = matplotlib.colormaps["turbo"]
        for i in range(int(lwid)):
            c = tuple(int(255 * v) for v in cm(i / lwid)[:3])
            d.line([lx + i, ly + pad, lx + i, ly + pad + lhgt], fill=c)
        d.text((lx, ly + pad + 1.1 * lhgt), f"height above ground 0 - {NDSM_MAX:.0f} m", font=f_txt,
               fill=(255, 255, 255, 255))
    return img


def make(base, side, polys, tiles, title, credit, max_px=None, legend=False):
    img = Image.fromarray(base)
    if max_px and max(img.size) > max_px:
        s = max_px / max(img.size)
        img = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
    return draw_overlay(img.convert("RGB"), side, polys, tiles, title, credit, legend)


def save_jpg(img, path, max_bytes=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    for q in (90, 85, 80, 75, 70, 60):
        img.save(path, "JPEG", quality=q, optimize=True)
        if not max_bytes or path.stat().st_size <= max_bytes:
            return q
    fail(f"{path} is still over {max_bytes} bytes")


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--git-copies", action="store_true", help="also write the <= 2000 px copies into git")
    p.add_argument("--archive-max-px", type=int, default=8000, help="largest side of the archive images")
    args = p.parse_args(argv)

    root = data_root()
    polys = fr.boundary_polygons()
    tiles = lidar_io.load_index("v1", root)["tiles"]
    out = root / "reports/stage0"
    db = manifest.connect(root)

    lidar, side = lidar_picture(root)
    naip, nside = read_raster(root / "derived/ortho/v1/naip2021_campus_0.6m.tif")
    metro, mside = read_raster(root / "derived/ortho/v1/metro2026_campus_0.15m.tif")
    jobs = [
        ("stage0_lidar_height.jpg", lidar, side, "Stage 0: LiDAR heights (USGS 3DEP D22, nDSM over hillshade)",
         "LiDAR: U.S. Geological Survey 3DEP. Boundary: (c) OpenStreetMap contributors", True, True,
         "Public domain (USGS 3DEP); boundary ODbL"),
        ("stage0_naip2021.jpg", naip, nside, "Stage 0: NAIP 2021 orthophoto (0.6 m)",
         "Imagery: USDA NAIP via USGS. Boundary: (c) OpenStreetMap contributors", False, True,
         "Public domain (USDA NAIP); boundary ODbL"),
        ("stage0_metro2026.jpg", metro, mside, "Stage 0: Metro Nashville 2026 orthophoto (reference only)",
         "Imagery: Metro Nashville GIS (no license stated; not for sharing)", False, False,
         "Metro Nashville 2026 imagery; reference only, never shared"),
    ]
    for name, base, sd, title, credit, legend, to_git, lic in jobs:
        img = make(base, sd, polys, tiles, title, credit, args.archive_max_px, legend)
        save_jpg(img, out / name)
        manifest.add_file(out / name, "render_stage0.py", lic, 0, root=root, db=db)
        print(f"{out / name}: {img.size[0]} x {img.size[1]}, {(out / name).stat().st_size / 1e6:.1f} MB")
        if args.git_copies and to_git:
            small = make(base, sd, polys, tiles, title, credit, 2000, legend)
            q = save_jpg(small, GIT_IMAGES / name, 1_500_000)
            print(f"  git copy {GIT_IMAGES / name}: {small.size[0]} x {small.size[1]}, "
                  f"{(GIT_IMAGES / name).stat().st_size / 1e6:.2f} MB (JPEG q{q})")
    db.commit()
    db.close()


if __name__ == "__main__":
    main()
