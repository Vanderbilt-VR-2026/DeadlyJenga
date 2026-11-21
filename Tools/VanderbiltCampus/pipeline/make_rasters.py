"""Make the shared rasters in the campus frame (0.5 m cells) and the two orthophoto mosaics.

LiDAR rasters (from the working tiles, prepare_lidar.py) -> derived/rasters/v1/:
  dtm.tif            ground height: lowest class-2 point per cell, holes filled
                     smoothly from the cells around them (push-pull pyramid fill)
  dtm_measured.tif   1 where the cell had a ground point, 0 where it was filled
  dsm.tif            highest first return per cell (no class 7 / 18 noise), holes filled
  ndsm.tif           dsm - dtm (negative values set to 0)
  building_mask.tif  1 where a class-6 (building) point falls in the cell
  highveg_mask.tif   1 where a class-5 (high vegetation) point falls in the cell
  density.tif        points per square meter (all classes except 7 / 18)
Heights are float32 campus Z (meters, Z = NAVD88 - Z0); masks are uint8.

Orthophoto mosaics (from raw/metro/<date>/) -> derived/ortho/v1/:
  metro2026_campus_0.15m.tif   Metro 2026 imagery: reference only, never shared
  naip2021_campus_0.6m.tif     USDA NAIP: public domain

Every .tif has a sidecar .json: origin (top-left corner) and cell size in campus
meters, size, units. Row 0 is the north edge. The .tif files carry the same
transform but no CRS (the campus frame is local).
"""
import argparse
import datetime
import sys
import time
from pathlib import Path

import numpy as np
import rasterio
from rasterio.transform import Affine

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
from common import data_root, fail, save_json  # noqa: E402

NOISE = (7, 18)
LIDAR_LICENSE = "Public domain (USGS 3DEP); credit: U.S. Geological Survey"
ORTHO = {
    "metro2026": {"folder": "ortho_2026", "prefix": "ortho_2026_", "cell": 0.15,
                  "out": "metro2026_campus_0.15m.tif", "compress": "JPEG",
                  "license": "Metro Nashville 2026 imagery; license not stated: reference only, never shared"},
    "naip2021": {"folder": "naip", "prefix": "naip_", "cell": 0.6,
                 "out": "naip2021_campus_0.6m.tif", "compress": "DEFLATE",
                 "license": "Public domain (USDA NAIP via USGS The National Map)"},
}


# ---- grid ----------------------------------------------------------------

def grid_spec(polys, buffer_m=20.0, cell=0.5):
    """Grid covering boundary + ring + buffer, snapped outward to whole meters."""
    import shapely
    x0, y0, x1, y1 = shapely.union_all(list(polys.values())).buffer(buffer_m).bounds
    x0, y0, x1, y1 = np.floor(x0), np.floor(y0), np.ceil(x1), np.ceil(y1)
    return grid_from_bounds(x0, y0, x1, y1, cell)


def grid_from_bounds(x0, y0, x1, y1, cell):
    w = int(round((x1 - x0) / cell))
    h = int(round((y1 - y0) / cell))
    # Anchored at the top-left corner, so every raster and mosaic shares the same origin.
    return {"x0": float(x0), "y_top": float(y1), "cell": cell, "width": w, "height": h}


def cells_of(x, y, g):
    """Flat cell index of each point (-1 if outside the grid)."""
    c = np.floor((x - g["x0"]) / g["cell"]).astype(np.int64)
    r = np.floor((g["y_top"] - y) / g["cell"]).astype(np.int64)
    ok = (c >= 0) & (c < g["width"]) & (r >= 0) & (r < g["height"])
    return np.where(ok, r * g["width"] + c, -1)


def cell_centers(g, rows=None):
    rows = np.arange(g["height"]) if rows is None else rows
    xs = g["x0"] + (np.arange(g["width"]) + 0.5) * g["cell"]
    ys = g["y_top"] - (rows + 0.5) * g["cell"]
    return xs, ys


class Accumulator:
    """Per-cell min ground, max first return, counts and class flags, filled tile by tile."""

    def __init__(self, g):
        n = g["width"] * g["height"]
        self.g = g
        self.ground = np.full(n, np.inf, dtype=np.float32)
        self.top = np.full(n, -np.inf, dtype=np.float32)
        self.count = np.zeros(n, dtype=np.int32)
        self.building = np.zeros(n, dtype=bool)
        self.highveg = np.zeros(n, dtype=bool)

    def add(self, pts):
        cls = pts["classification"]
        good = ~np.isin(cls, NOISE)
        idx = cells_of(pts["x"], pts["y"], self.g)
        inside = idx >= 0
        z = pts["z"].astype(np.float32)
        m = inside & (cls == 2)
        np.minimum.at(self.ground, idx[m], z[m])
        m = inside & good & (pts["return_number"] == 1)
        np.maximum.at(self.top, idx[m], z[m])
        m = inside & good
        self.count += np.bincount(idx[m], minlength=self.count.size).astype(np.int32)
        self.building[idx[inside & (cls == 6)]] = True
        self.highveg[idx[inside & (cls == 5)]] = True

    def shape(self, a):
        return a.reshape(self.g["height"], self.g["width"])


def _half(a):
    """Average 2 x 2 blocks, ignoring NaN (a block of only NaN stays NaN)."""
    h, w = a.shape
    p = np.pad(a, ((0, h % 2), (0, w % 2)), constant_values=np.nan)
    b = p.reshape(p.shape[0] // 2, 2, p.shape[1] // 2, 2)
    ok = np.isfinite(b)
    n = ok.sum(axis=(1, 3))
    total = np.where(ok, b, 0).sum(axis=(1, 3))
    with np.errstate(invalid="ignore", divide="ignore"):
        return np.where(n > 0, total / n, np.nan)


def fill_holes(a, smooth=60):
    """Fill NaN cells smoothly from the measured cells around them ("push-pull").

    1. Average 2 x 2 blocks again and again until the coarse grid has no holes.
    2. Come back up level by level: a hole cell takes the bilinear value from the
       level below (coarser), measured cells keep their value.
    3. A few smoothing passes over the hole cells only (each becomes the mean of
       its 4 neighbours), so filled areas join the measured ones without steps.
    Fast on the 15 M-cell campus grid (a triangulation took far too long).
    """
    import cv2
    holes = np.isnan(a)
    if not holes.any():
        return a
    if holes.all():
        return np.zeros_like(a)
    small = _half(a)
    small = fill_holes(small, smooth=0)
    up = cv2.resize(small.astype(np.float32), (small.shape[1] * 2, small.shape[0] * 2),
                    interpolation=cv2.INTER_LINEAR)[:a.shape[0], :a.shape[1]]
    out = np.where(holes, up, a).astype(np.float32)
    for _ in range(smooth):
        p = np.pad(out, 1, mode="edge")
        avg = (p[:-2, 1:-1] + p[2:, 1:-1] + p[1:-1, :-2] + p[1:-1, 2:]) / 4
        out[holes] = avg[holes]
    return out


def build_lidar_rasters(acc):
    """Turn accumulated cells into the raster arrays (all row 0 = north)."""
    ground = acc.shape(acc.ground).astype(np.float32)
    top = acc.shape(acc.top).astype(np.float32)
    measured = np.isfinite(ground)
    ground[~measured] = np.nan
    top[~np.isfinite(top)] = np.nan
    dtm = fill_holes(ground).astype(np.float32)
    dsm = fill_holes(top).astype(np.float32)
    dsm = np.maximum(dsm, dtm)
    ndsm = np.maximum(dsm - dtm, 0).astype(np.float32)
    cell_area = acc.g["cell"] ** 2
    return {
        "dtm": dtm,
        "dtm_measured": measured.astype(np.uint8),
        "dsm": dsm,
        "ndsm": ndsm,
        "building_mask": acc.shape(acc.building).astype(np.uint8),
        "highveg_mask": acc.shape(acc.highveg).astype(np.uint8),
        "density": (acc.shape(acc.count) / cell_area).astype(np.float32),
    }


# ---- writing -------------------------------------------------------------

def sidecar(g, path, units, extra=None):
    d = {
        "file": Path(path).name,
        "frame": "Vanderbilt campus frame v1 (Tools/VanderbiltCampus/config/frame.json)",
        "origin_x": g["x0"], "origin_y": g["y_top"],
        "origin_is": "top-left corner of the top-left cell (campus meters)",
        "cell_m": g["cell"], "width": g["width"], "height": g["height"],
        "bounds": [g["x0"], g["y_top"] - g["height"] * g["cell"], g["x0"] + g["width"] * g["cell"], g["y_top"]],
        "row0": "north edge (y decreases down the rows)",
        "pixel_to_campus": "X = origin_x + (col + 0.5) * cell_m ; Y = origin_y - (row + 0.5) * cell_m",
        "units": units,
        "made": datetime.datetime.now().isoformat(timespec="seconds"),
    }
    d.update(extra or {})
    return d


def write_tif(path, arrays, g, compress="DEFLATE", nodata=None):
    """Write one band (2-D array) or RGB (3, h, w) with the campus transform and no CRS."""
    a = arrays if arrays.ndim == 3 else arrays[None]
    path.parent.mkdir(parents=True, exist_ok=True)
    profile = {"driver": "GTiff", "width": g["width"], "height": g["height"], "count": a.shape[0],
               "dtype": a.dtype, "transform": Affine(g["cell"], 0, g["x0"], 0, -g["cell"], g["y_top"]),
               "tiled": True, "blockxsize": 512, "blockysize": 512, "compress": compress, "BIGTIFF": "IF_SAFER"}
    if compress == "JPEG":
        profile.update({"photometric": "YCBCR", "jpeg_quality": 90})
    else:
        profile["predictor"] = 3 if a.dtype == np.float32 else 2
    if nodata is not None:
        profile["nodata"] = nodata
    with rasterio.open(path, "w", **profile) as dst:
        dst.write(a)


def save(path, arr, g, units, root, db, source, lic, extra=None, compress="DEFLATE"):
    write_tif(path, arr, g, compress)
    side = path.with_suffix(".json")
    save_json(side, sidecar(g, path, units, extra))
    if root is not None:
        manifest.add_file(path, source, lic, 0, root=root, db=db)
        manifest.add_file(side, source, lic, 0, root=root, db=db)


# ---- orthophotos ----------------------------------------------------------

def read_world(pgw):
    a, d, b, e, c, f = [float(v) for v in Path(pgw).read_text().split()]
    return a, e, c, f          # pixel width, pixel height (negative), center x/y of the top-left pixel


def mosaic_utm(folder, prefix):
    """Paste the PNG chunks into one UTM image. Returns (rgb, a, e, c, f)."""
    from PIL import Image
    chunks = sorted(folder.glob(prefix + "r*_c*.png"))
    if not chunks:
        fail(f"no {prefix}*.png in {folder}")
    info = []
    for p in chunks:
        a, e, c, f = read_world(p.with_suffix(".pgw"))
        with Image.open(p) as im:
            info.append((p, a, e, c, f, im.size))
    a, e = info[0][1], info[0][2]
    c0 = min(i[3] for i in info)
    f0 = max(i[4] for i in info)
    W = max(int(round((i[3] - c0) / a)) + i[5][0] for i in info)
    H = max(int(round((i[4] - f0) / e)) + i[5][1] for i in info)
    rgb = np.zeros((H, W, 3), dtype=np.uint8)
    for p, _, _, c, f, (w, h) in info:
        col, row = int(round((c - c0) / a)), int(round((f - f0) / e))
        with Image.open(p) as im:
            rgb[row:row + h, col:col + w] = np.asarray(im.convert("RGB"))
    return rgb, a, e, c0, f0


def reproject_ortho(src_rgb, a, e, c0, f0, g, block=1024):
    """Resample a UTM mosaic onto the campus grid (bilinear). Returns (3, h, w) uint8."""
    import cv2
    out = np.zeros((3, g["height"], g["width"]), dtype=np.uint8)
    for r0 in range(0, g["height"], block):
        rows = np.arange(r0, min(r0 + block, g["height"]))
        xs, ys = cell_centers(g, rows)
        X, Y = np.meshgrid(xs, ys)
        u, v, _ = fr.from_campus(X.ravel(), Y.ravel(), np.zeros(X.size), 32616)
        mx = ((u - c0) / a).reshape(X.shape).astype(np.float32)
        my = ((v - f0) / e).reshape(X.shape).astype(np.float32)
        part = cv2.remap(src_rgb, mx, my, interpolation=cv2.INTER_LINEAR,
                         borderMode=cv2.BORDER_CONSTANT, borderValue=0)
        out[:, rows[0]:rows[-1] + 1] = np.moveaxis(part, 2, 0)
    return out


# ---- main ----------------------------------------------------------------

def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--cell", type=float, default=0.5)
    p.add_argument("--buffer", type=float, default=20.0)
    p.add_argument("--lidar-version", default="v1")
    p.add_argument("--metro-date", default=None, help="raw/metro/<date> folder (default: newest)")
    p.add_argument("--only", choices=["lidar", "ortho"], help="make only one group")
    args = p.parse_args(argv)

    root = data_root()
    polys = fr.boundary_polygons()
    g = grid_spec(polys, args.buffer, args.cell)
    db = manifest.connect(root)
    t = time.time()

    if args.only in (None, "lidar"):
        out = root / "derived/rasters/v1"
        acc = Accumulator(g)
        idx = lidar_io.load_index(args.lidar_version, root)
        folder = lidar_io.lidar_dir(args.lidar_version, root)
        for i, tile in enumerate(idx["tiles"]):
            acc.add(lidar_io.read_tile(folder / tile["file"]))
        print(f"read {len(idx['tiles'])} tiles ({time.time() - t:.0f} s); filling holes", flush=True)
        rasters = build_lidar_rasters(acc)
        print(f"surfaces done ({time.time() - t:.0f} s)", flush=True)
        units = {"dtm": "campus Z meters", "dsm": "campus Z meters", "ndsm": "meters above ground",
                 "dtm_measured": "1 = measured, 0 = filled", "building_mask": "1 = class 6 point present",
                 "highveg_mask": "1 = class 5 point present", "density": "points per m2 (no class 7/18)"}
        src = f"make_rasters.py from derived/lidar/{args.lidar_version}"
        for name, arr in rasters.items():
            save(out / f"{name}.tif", arr, g, units[name], root, db, src, LIDAR_LICENSE,
                 {"dtype": str(arr.dtype)})
        db.commit()
        m = rasters["dtm_measured"]
        print(f"LiDAR rasters {g['width']} x {g['height']} at {g['cell']} m, ground measured in "
              f"{m.mean():.1%} of cells, mean density {rasters['density'].mean():.1f} pts/m2 ({time.time() - t:.0f} s)")

    if args.only in (None, "ortho"):
        metro = (root / "raw/metro" / args.metro_date) if args.metro_date else sorted((root / "raw/metro").iterdir())[-1]
        out = root / "derived/ortho/v1"
        for key, o in ORTHO.items():
            rgb, a, e, c0, f0 = mosaic_utm(metro / o["folder"], o["prefix"])
            go = grid_from_bounds(g["x0"], g["y_top"] - g["height"] * g["cell"],
                                  g["x0"] + g["width"] * g["cell"], g["y_top"], o["cell"])
            img = reproject_ortho(rgb, a, e, c0, f0, go)
            src = f"make_rasters.py from {metro.relative_to(root).as_posix()}/{o['folder']} (EPSG:32616)"
            save(out / o["out"], img, go, "RGB uint8 (0,0,0 = no data)", root, db, src, o["license"],
                 {"license": o["license"], "resampling": "bilinear"}, compress=o["compress"])
            db.commit()
            print(f"{o['out']}: {go['width']} x {go['height']} at {o['cell']} m ({time.time() - t:.0f} s)")
    db.close()


if __name__ == "__main__":
    main()
