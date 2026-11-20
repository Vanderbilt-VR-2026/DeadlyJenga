"""Accuracy check of the terrain meshes against LiDAR ground points (Stage 1, terrain step 3).

For every finished tile, the vertical distance from EVERY class-2 point in the tile (spikes
included) to the visual mesh and to the collider mesh: median, 95th percentile, max, share
within 0.15 m. Target (STANDARDS.md section 5): 0.15 m. Every tile whose p95 is over target is
listed with a likely reason, found from where its large errors sit (next to kept breaks,
next to buildings / unmeasured holes, steep ground, under high vegetation, or isolated spikes).

Writes:
  reports/stage1/terrain_accuracy.json
  reports/stage1/terrain_error.png       campus-wide map of median |error| per 2 m cell (visual)
  reports/stage1/terrain_materials.png   shaded relief of the final visual meshes, colored by
                                         material, breaks in dark lines, boundary drawn
  Docs/Development/VanderbiltCampus/progress/images/stage1-terrain-error.png      (<= 1.5 MB copies
  Docs/Development/VanderbiltCampus/progress/images/stage1-terrain-materials.png   for git)

Usage:
  python terrain_check.py --jobs 10 [--version v1]
"""
import argparse
import io
import json
import sys
import time
import traceback
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
import terrain as tr  # noqa: E402
import terrain_mesh as tm  # noqa: E402
import vcmesh  # noqa: E402
from common import REPO, data_root, fail, load_json, save_json  # noqa: E402

TARGET = 0.15
HIST_BINS = np.arange(0, 5.0005, 0.001)       # 1 mm bins for the pooled percentiles
ERR_CELL = 2.0                                # error map cell (m)
REL_CELL = 0.5                                # relief render cell (m)
GIT_IMAGES = REPO / "Docs" / "Development" / "VanderbiltCampus" / "progress" / "images"
GIT_LIMIT = 1.5e6


def raster_lookup(name, root):
    """A derived/rasters/v1 raster (north-up) and a function giving its value at campus X, Y."""
    import rasterio
    info = load_json(Path(root) / "derived" / "rasters" / "v1" / f"{name}.json")
    with rasterio.open(Path(root) / "derived" / "rasters" / "v1" / info["file"]) as src:
        a = src.read(1)

    def at(X, Y):
        c = np.clip(((X - info["origin_x"]) / info["cell_m"]).astype(int), 0, info["width"] - 1)
        r = np.clip(((info["origin_y"] - Y) / info["cell_m"]).astype(int), 0, info["height"] - 1)
        return a[r, c]
    return at


def reason_for(x, y, err, cells, x0, y0, root):
    """Where do the points with |error| > TARGET sit? Returns (label, shares)."""
    from scipy.ndimage import binary_dilation, distance_transform_edt
    bad = np.abs(err) > TARGET
    if not bad.any():
        return "none", {}
    cell = 100.0 / cells["hc"].shape[0]
    n = cells["hc"].shape[0]
    ci = np.clip(((x[bad] - x0) / cell).astype(int), 0, n - 1)
    cj = np.clip(((y[bad] - y0) / cell).astype(int), 0, n - 1)
    sides = tm.break_sides(cells["bx"], cells["by"])
    near_break = binary_dilation(sides, iterations=max(1, int(1.0 / cell)))
    hole = cells["holes"] == 2
    near_hole = distance_transform_edt(~hole) * cell <= 2.0 if hole.any() else np.zeros_like(hole)
    gy, gx = np.gradient(cells["hc"], cell)
    steep = np.hypot(gx, gy) > 0.5                     # steeper than about 27 degrees
    hv = raster_lookup("highveg_mask", root)(x[bad], y[bad]) > 0
    shares = {"next_to_break": float(near_break[cj, ci].mean()),
              "next_to_building_or_hole": float(near_hole[cj, ci].mean()),
              "steep_ground": float(steep[cj, ci].mean()),
              "under_high_vegetation": float(hv.mean())}
    labels = {"next_to_break": "dense steps / curbs (errors beside kept breaks)",
              "next_to_building_or_hole": "building edges / unmeasured ground (ground points against walls "
                                          "or misclassified low roofs)",
              "steep_ground": "steep banks and terraces (slopes the height grid smooths; 2 m in ring tiles)",
              "under_high_vegetation": "vegetation misclassified as ground (under canopy)"}
    key = max(shares, key=shares.get)
    if shares[key] < 0.3:
        return "scattered noisy ground points", {k: round(v, 3) for k, v in shares.items()}
    return labels[key], {k: round(v, 3) for k, v in shares.items()}


def check_tile(args):
    name, t, root, version = args
    try:
        d = tr.tdir(root, version, name)
        x0, y0 = t["bounds"][0], t["bounds"][1]
        pts = lidar_io.load_points(x0, y0, x0 + 100, y0 + 100, classes=[2], root=root)
        x, y, z = pts["x"], pts["y"], pts["z"]
        out = {"tile": name, "kind": tr.kind_of(t)}
        hists = {}
        nerr = None
        for label, fn in (("visual", "terrain.npz"), ("collider", "terrain_col.npz")):
            m = vcmesh.load_mesh(d / fn)
            zm, _ = mesh_sample(m, x, y, x0, y0)
            err = z - zm
            out[label] = tm.error_stats(err)
            out[label]["triangles"] = int(len(m["faces"]))
            e = np.abs(err[np.isfinite(err)])
            hists[label] = np.histogram(np.minimum(e, 5.0), HIST_BINS)[0]
            if label == "visual":
                verr = err
        cz = np.load(d / "cells.npz")
        cells = {k: cz[k] for k in ("hc", "bx", "by", "holes")}
        for label in ("visual", "collider"):
            if out[label].get("p95_m", 0) > TARGET:
                err = verr if label == "visual" else z - mesh_sample(vcmesh.load_mesh(d / "terrain_col.npz"),
                                                                     x, y, x0, y0)[0]
                r, shares = reason_for(x, y, err, cells, x0, y0, root)
                out[label]["over_target_reason"] = r
                out[label]["error_location_shares"] = shares
        # error map: median |error| per 2 m cell (visual)
        k = int(100 / ERR_CELL)
        ok = np.isfinite(verr)
        ci = np.clip(((x[ok] - x0) / ERR_CELL).astype(int), 0, k - 1)
        cj = np.clip(((y[ok] - y0) / ERR_CELL).astype(int), 0, k - 1)
        emap = np.full(k * k, np.nan, np.float32)
        flat = cj * k + ci
        e = np.abs(verr[ok])
        order = np.lexsort((e, flat))
        flat, e = flat[order], e[order]
        if flat.size:
            starts = np.flatnonzero(np.r_[True, flat[1:] != flat[:-1]])
            cnt = np.diff(np.r_[starts, flat.size])
            emap[flat[starts]] = e[starts + cnt // 2]
        # relief render samples: visual mesh height + material at 0.5 m pixel centers
        r = int(100 / REL_CELL)
        cc = (np.arange(r) + 0.5) * REL_CELL
        GX, GY = np.meshgrid(x0 + cc, y0 + cc)
        m = vcmesh.load_mesh(d / "terrain.npz")
        zr, fr_ = mesh_sample(m, GX.ravel(), GY.ravel(), x0, y0)
        mat = np.where(fr_ >= 0, m["face_mat"][np.maximum(fr_, 0)], 255).astype(np.uint8)
        # break lines for the render
        brk = tm.break_sides(cells["bx"], cells["by"])
        if brk.shape[0] != r:
            brk = np.zeros((r, r), bool)
        return name, out, hists, emap.reshape(k, k), zr.reshape(r, r).astype(np.float32), mat.reshape(r, r), \
            brk, None
    except Exception:
        return name, None, None, None, None, None, None, traceback.format_exc()


def mesh_sample(m, px, py, x0, y0):
    """Mesh height and face index at points (face -1 where none)."""
    return tm.mesh_z_at(m["verts"], m["faces"], px, py, x0, y0, return_face=True)


def pooled(hist):
    """Median / p95 / share within target from a 1 mm histogram."""
    c = np.cumsum(hist)
    n = c[-1]
    if not n:
        return {}
    med = HIST_BINS[np.searchsorted(c, 0.5 * n)]
    p95 = HIST_BINS[np.searchsorted(c, 0.95 * n)]
    within = c[int(round(TARGET / 0.001)) - 1] / n
    return {"points": int(n), "median_m": round(float(med), 3), "p95_m": round(float(p95), 3),
            "within_0.15_pct": round(float(within * 100), 2)}


def mosaic(tiles, arrays, cell, fill):
    gx0 = min(t["bounds"][0] for t in tiles)
    gy0 = min(t["bounds"][1] for t in tiles)
    gx1 = max(t["bounds"][2] for t in tiles)
    gy1 = max(t["bounds"][3] for t in tiles)
    W, H = int((gx1 - gx0) / cell), int((gy1 - gy0) / cell)
    sample = next(iter(arrays.values()))
    out = np.full((H, W), fill, sample.dtype)
    k = int(100 / cell)
    for t in tiles:
        a = arrays.get(t["name"])
        if a is None:
            continue
        i0 = int((t["bounds"][0] - gx0) / cell)
        j0 = int((t["bounds"][1] - gy0) / cell)
        out[j0:j0 + k, i0:i0 + k] = a
    return np.flipud(out), (gx0, gy0, gx1, gy1)        # north-up for images


def boundary_pixels(extent, cell, H):
    """Campus and ring outlines as pixel polylines for drawing."""
    gx0, gy0, gx1, gy1 = extent
    lines = []
    for role, g in fr.boundary_polygons().items():
        geoms = getattr(g, "geoms", [g])
        for p in geoms:
            for ring in [p.exterior] + list(p.interiors):
                xy = np.asarray(ring.coords)
                lines.append((role, np.c_[(xy[:, 0] - gx0) / cell, (gy1 - xy[:, 1]) / cell]))
    return lines


def save_png(img, path, git_path, title, legend=None):
    """Save the full image to the archive and a <= 1.5 MB copy for git."""
    from PIL import Image, ImageDraw
    im = Image.fromarray(img)
    if title:
        d = ImageDraw.Draw(im)
        d.rectangle([0, 0, 12 + 7 * len(title), 22], fill=(255, 255, 255))
        d.text((6, 5), title, fill=(0, 0, 0))
    if legend:
        d = ImageDraw.Draw(im)
        y = 30
        for label, color in legend:
            d.rectangle([6, y, 20, y + 12], fill=tuple(color), outline=(0, 0, 0))
            d.rectangle([22, y - 1, 26 + 7 * len(label), y + 13], fill=(255, 255, 255))
            d.text((25, y), label, fill=(0, 0, 0))
            y += 16
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path, optimize=True)
    git_path.parent.mkdir(parents=True, exist_ok=True)
    w = im.width
    while True:
        small = im.resize((w, int(im.height * w / im.width)), Image.LANCZOS) if w != im.width else im
        buf = io.BytesIO()
        small.quantize(colors=128, method=Image.Quantize.MEDIANCUT).save(buf, format="PNG", optimize=True)
        if buf.tell() <= GIT_LIMIT:
            git_path.write_bytes(buf.getvalue())
            return small.size, buf.tell()
        w = int(w * 0.85)


def draw_lines(img, lines, cell_scale=1.0):
    from PIL import Image, ImageDraw
    im = Image.fromarray(img)
    d = ImageDraw.Draw(im)
    for role, px in sorted(lines, key=lambda rl: rl[0] == "campus"):     # campus outline drawn last, on top
        color = (20, 60, 230) if role == "campus" else (240, 150, 0)
        d.line([tuple(p) for p in px * cell_scale], fill=color, width=5 if role == "campus" else 4)
    return np.asarray(im)


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--jobs", type=int, default=1)
    p.add_argument("--version", default="v1")
    args = p.parse_args(argv)
    root = data_root()
    t_start = time.time()
    tiles = [t for t in lidar_io.load_index(root=root)["tiles"] if t["zone"] in ("campus", "ring")]
    done = [t for t in tiles if (tr.tdir(root, args.version, t["name"]) / "tile.json").exists()]
    if not done:
        fail("no finished terrain tiles; run terrain.py first")
    jobs = [(t["name"], t, str(root), args.version) for t in done]
    results, errs = {}, {}
    hist = {"visual": np.zeros(len(HIST_BINS) - 1, np.int64), "collider": np.zeros(len(HIST_BINS) - 1, np.int64)}
    hist_kind = {}
    emaps, zmaps, mmaps, bmaps = {}, {}, {}, {}
    with ProcessPoolExecutor(max_workers=max(1, args.jobs)) as ex:
        for k, (name, out, h, emap, zr, mat, brk, err) in enumerate(ex.map(check_tile, jobs)):
            if err:
                errs[name] = err
                print(f"{name} FAILED\n{err}", flush=True)
                continue
            results[name] = out
            for lab in hist:
                hist[lab] += h[lab]
                hist_kind.setdefault((out["kind"], lab), np.zeros(len(HIST_BINS) - 1, np.int64))
                hist_kind[(out["kind"], lab)] += h[lab]
            emaps[name], zmaps[name], mmaps[name] = emap, zr, mat
            bmaps[name] = brk
            if (k + 1) % 50 == 0:
                print(f"{k + 1}/{len(jobs)} tiles checked, {time.time() - t_start:.0f} s", flush=True)

    over = {lab: sorted([n for n, r in results.items() if r[lab].get("p95_m", 0) > TARGET]) for lab in hist}
    report = {
        "made": time.strftime("%Y-%m-%dT%H:%M:%S"), "version": args.version, "target_m": TARGET,
        "method": "vertical distance from every LiDAR class-2 point in each tile (spikes included) to the mesh; "
                  "vertical break faces are skipped (a point is measured against the ground face above/below it)",
        "tiles": len(results),
        "overall": {lab: pooled(hist[lab]) for lab in hist},
        "by_kind": {f"{k}_{lab}": pooled(h) for (k, lab), h in sorted(hist_kind.items())},
        "tiles_passing_pct": {lab: round(100 * (1 - len(over[lab]) / max(len(results), 1)), 1) for lab in hist},
        "tiles_over_target": {lab: [{"tile": n, "p95_m": results[n][lab]["p95_m"],
                                     "reason": results[n][lab].get("over_target_reason"),
                                     "shares": results[n][lab].get("error_location_shares")} for n in over[lab]]
                              for lab in hist},
        "per_tile": {n: {lab: results[n][lab] for lab in hist} for n in sorted(results)},
        "failed_tiles": sorted(errs),
    }
    rp = Path(root) / "reports" / "stage1" / "terrain_accuracy.json"
    save_json(rp, report)

    # images
    import matplotlib
    matplotlib.use("Agg")
    from matplotlib import colors
    emos, ext = mosaic(done, emaps, ERR_CELL, np.nan)
    norm = colors.Normalize(0, 0.30)
    rgba = (matplotlib.colormaps["turbo"](norm(np.nan_to_num(emos, nan=0))) * 255).astype(np.uint8)[:, :, :3]
    rgba[~np.isfinite(emos)] = (235, 235, 235)
    lines_err = boundary_pixels(ext, ERR_CELL, emos.shape[0])
    rgba = draw_lines(rgba, lines_err)
    legend = [(f"median |error| {v:.2f} m", (np.array(matplotlib.colormaps["turbo"](norm(v))[:3]) * 255).astype(int))
              for v in (0.0, 0.05, 0.10, 0.15, 0.20, 0.30)] + [("no ground points", (235, 235, 235))]
    ep = Path(root) / "reports" / "stage1" / "terrain_error.png"
    size_e = save_png(rgba, ep, GIT_IMAGES / "stage1-terrain-error.png",
                      "Stage 1 terrain: median |mesh - LiDAR ground| per 2 m cell (visual mesh)", legend)

    zmos, ext2 = mosaic(done, zmaps, REL_CELL, np.nan)
    mmos, _ = mosaic(done, mmaps, REL_CELL, 255)
    bmos, _ = mosaic(done, bmaps, REL_CELL, False)
    zf = np.where(np.isfinite(zmos), zmos, np.nanmin(zmos))
    gy, gx = np.gradient(zf, REL_CELL)
    az, alt = np.radians(315), np.radians(40)
    slope = np.arctan(np.hypot(gx, gy) * 1.5)
    aspect = np.arctan2(-gx, gy)
    shade = np.sin(alt) * np.cos(slope) + np.cos(alt) * np.sin(slope) * np.cos(az - aspect)
    shade = np.clip(0.55 + 0.6 * shade, 0.25, 1.15)
    from common import CONFIG
    mats = load_json(CONFIG / "material_ids.json")["ids"]
    lut = np.full((256, 3), 200, np.float32)
    for m in mats:
        lut[m["index"]] = [int(m["plain"][i:i + 2], 16) for i in (1, 3, 5)]
    rgb = lut[mmos] * shade[:, :, None]
    rgb[bmos] = (220, 30, 30)
    rgb = np.clip(rgb, 0, 255).astype(np.uint8)
    rgb = draw_lines(rgb, boundary_pixels(ext2, REL_CELL, zmos.shape[0]))
    used = sorted(set(np.unique(mmos).tolist()) - {255})
    legend = [(m["id"], [int(m["plain"][i:i + 2], 16) for i in (1, 3, 5)]) for m in mats if m["index"] in used]
    legend.append(("break sides (curbs, steps, walls)", (220, 30, 30)))
    mp = Path(root) / "reports" / "stage1" / "terrain_materials.png"
    size_m = save_png(rgb, mp, GIT_IMAGES / "stage1-terrain-materials.png",
                      "Stage 1 terrain: shaded relief of the visual meshes by material (plain palette); "
                      "blue = campus boundary, orange = ring edge", legend)
    db = manifest.connect(root)
    for f in (rp, ep, mp):
        manifest.add_file(f, "terrain_check.py", "", stage=1, root=root, db=db)
    db.commit()
    db.close()
    print(json.dumps({k: report[k] for k in ("tiles", "overall", "by_kind", "tiles_passing_pct")}, indent=2))
    for lab in hist:
        print(f"{lab}: {len(over[lab])} tiles over {TARGET} m p95")
        for row in report["tiles_over_target"][lab]:
            print(f"  {row['tile']} p95 {row['p95_m']} - {row['reason']}")
    print(f"git images: error {size_e}, materials {size_m}")
    print(f"done in {time.time() - t_start:.0f} s")
    if errs:
        fail(f"{len(errs)} tiles failed: {sorted(errs)}")


if __name__ == "__main__":
    main()
