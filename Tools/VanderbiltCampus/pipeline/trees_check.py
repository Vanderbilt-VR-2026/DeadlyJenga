"""Check the detected trees against independent measures (Stage 1, STANDARDS.md section 5).

Targets: tree position within 1 m, tree height within 1.5 m.

 (a) Height: detected height vs the 99th-percentile LiDAR vegetation height inside the crown.
 (b) Position: detected top vs the centroid of the highest 10% of the crown's points, and vs
     OSM natural=tree nodes (hand-placed, so only a rough check; reported both for the
     matched pairs and for every OSM node's nearest detected tree).
 (c) Coverage (recall / precision proxies): how much of the Metro 2021 canopy polygons and of
     the LiDAR high-vegetation cells the crown circles cover, and how much of the circle area
     lies outside any canopy.
 (d) Pictures: crown circles over the NAIP 2021 orthophoto, the whole campus plus zooms
     (Alumni Lawn, Peabody lawn, the densest grove, a parking lot).

Reads derived/trees/v1/trees.json + crowns.tif (trees_detect.py) and the LiDAR tiles again.
Writes reports/stage1/trees_accuracy.json and trees_*.png, and with --git-copies the two
small pictures Docs/Development/VanderbiltCampus/progress/images/stage1-trees-*.png.
"""
import argparse
import datetime
import os
import sys
import time
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import lidar_io  # noqa: E402
import manifest  # noqa: E402
import trees_common as tc  # noqa: E402
import trees_detect as td  # noqa: E402
from common import REPO, data_root, fail, load_json, save_json  # noqa: E402

Image.MAX_IMAGE_PIXELS = None
GIT_IMAGES = REPO / "Docs/Development/VanderbiltCampus/progress/images"
POS_TARGET, H_TARGET = 1.0, 1.5
GIT_MAX_BYTES = 1_500_000


def summary(err, target):
    err = np.abs(np.asarray(err, float))
    err = err[np.isfinite(err)]
    if not len(err):
        return {"n": 0}
    return {"n": int(len(err)), "median": round(float(np.median(err)), 3),
            "p95": round(float(np.percentile(err, 95)), 3), "max": round(float(err.max()), 3),
            "within_target_pct": round(100.0 * float((err <= target).mean()), 2), "target": target}


# ---- (a) + (b) ------------------------------------------------------------

def crown_point_checks(trees, crowns, g, jobs, root):
    dtm, _ = tc.read_raster(tc.rasters_dir(root) / "dtm.tif")
    _, fp1, _ = td.footprint_masks(g, root)
    tiles = [t for t in lidar_io.load_index()["tiles"] if t["zone"] in ("campus", "ring") and t["points"] > 0]
    pts = td.read_all_points(tiles, g, dtm.astype(np.float32), fp1, jobs)
    lab = crowns[pts["r"], pts["c"]]
    keep = lab > 0
    n = len(trees)
    st = td.point_stats(lab[keep], pts["x"][keep].astype(float), pts["y"][keep].astype(float),
                        pts["h"][keep].astype(float), pts["veg"][keep], n)
    return st


# ---- (c) ------------------------------------------------------------------

def coverage(trees, g, root):
    from scipy.ndimage import binary_closing
    from shapely.geometry import Point
    polys = tc.zone_polys()
    zone_masks = {z: tc.rasterize([(polys[k], 1)], g).astype(bool)
                  for z, k in (("campus", "campus"), ("ring", "backdrop_ring"))}
    canopy = tc.rasterize([(p, 1) for p in tc.canopy_polygons(root)], g).astype(bool)
    hv, _ = tc.read_raster(tc.rasters_dir(root) / "highveg_mask.tif")
    hv = binary_closing(hv > 0, np.ones((3, 3), bool))       # fill the gaps between points
    built = [t for t in trees if t["kind"] == "tree"]
    circles = tc.rasterize([(Point(t["x"], t["y"]).buffer(t["crown_diameter"] / 2, 16), 1) for t in built],
                           g).astype(bool)
    crowns, _ = tc.read_raster(root / "derived/trees/v1/crowns.tif")
    crown_cells = crowns > 0
    a = g["cell_m"] ** 2
    out = {}
    for z, m in zone_masks.items():
        c, cv, h, cr = circles & m, canopy & m, hv & m, crown_cells & m
        out[z] = {
            "zone_area_m2": round(float(m.sum() * a)),
            "metro_canopy_m2": round(float(cv.sum() * a)),
            "lidar_highveg_m2": round(float(h.sum() * a)),
            "crown_circles_m2": round(float(c.sum() * a)),
            "canopy_covered_by_circles_pct": round(100 * float((c & cv).sum()) / max(1, cv.sum()), 2),
            "highveg_covered_by_circles_pct": round(100 * float((c & h).sum()) / max(1, h.sum()), 2),
            "circles_outside_canopy_pct": round(100 * float((c & ~cv).sum()) / max(1, c.sum()), 2),
            "circles_outside_highveg_pct": round(100 * float((c & ~h).sum()) / max(1, c.sum()), 2),
            "circles_outside_both_pct": round(100 * float((c & ~cv & ~h).sum()) / max(1, c.sum()), 2),
            "canopy_covered_by_crowns_pct": round(100 * float((cr & cv).sum()) / max(1, cv.sum()), 2),
            "highveg_covered_by_crowns_pct": round(100 * float((cr & h).sum()) / max(1, h.sum()), 2),
            "canopy_vs_highveg_agreement_pct": round(100 * float((cv & h).sum()) / max(1, (cv | h).sum()), 2),
        }
    return out


# ---- (d) pictures ------------------------------------------------------------

KIND_COLOR = {"lidar": (255, 230, 0), "lidar+osm": (0, 255, 120), "osm_only": (255, 60, 200)}


def draw_trees(img, x0, y_top, mpp, trees, width=1, label=None):
    """Draw crown circles (yellow: LiDAR, green: LiDAR + OSM, pink: OSM only) and tops."""
    d = ImageDraw.Draw(img, "RGBA")
    for t in trees:
        if t["kind"] != "tree":
            continue
        px, py = (t["x"] - x0) / mpp, (y_top - t["y"]) / mpp
        r = max(1.5, t["crown_diameter"] / 2 / mpp)
        col = KIND_COLOR.get(t["source"], (255, 255, 255))
        d.ellipse([px - r, py - r, px + r, py + r], outline=col + (235,), width=width)
        if r > 6:
            d.ellipse([px - 1.5 * width, py - 1.5 * width, px + 1.5 * width, py + 1.5 * width], fill=(255, 0, 0, 255))
    if label:
        from render_stage0 import font
        f = font(max(12, img.width // 60))
        tw = max(d.textlength(s, font=f) for s in label)
        d.rectangle([8, 8, 24 + tw, 16 + len(label) * f.size * 1.3], fill=(0, 0, 0, 170))
        for i, s in enumerate(label):
            d.text((16, 12 + i * f.size * 1.3), s, font=f, fill=(255, 255, 255, 255))
    return img


def draw_boundary(img, x0, y_top, mpp, width):
    d = ImageDraw.Draw(img, "RGBA")
    polys = tc.zone_polys()
    for geom, col in ((polys["backdrop_ring"], (0, 230, 255, 255)), (polys["campus"], (255, 140, 0, 255))):
        gs = getattr(geom, "geoms", [geom])
        for gg in gs:
            for ring in [gg.exterior, *gg.interiors]:
                d.line([((x - x0) / mpp, (y_top - y) / mpp) for x, y in ring.coords], fill=col, width=width)


def crop(ortho, side, xmin, ymin, xmax, ymax, scale=1):
    cell = side["cell_m"]
    c0 = int(np.floor((xmin - side["origin_x"]) / cell)); c1 = int(np.ceil((xmax - side["origin_x"]) / cell))
    r0 = int(np.floor((side["origin_y"] - ymax) / cell)); r1 = int(np.ceil((side["origin_y"] - ymin) / cell))
    c0, r0 = max(c0, 0), max(r0, 0)
    a = ortho[r0:r1, c0:c1]
    img = Image.fromarray(a).convert("RGB")
    if scale != 1:
        img = img.resize((img.width * scale, img.height * scale), Image.BICUBIC)
    return img, side["origin_x"] + c0 * cell, side["origin_y"] - r0 * cell, cell / scale


def save_png_limited(img, path, max_bytes=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    im = img
    while True:
        q = im.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE) if max_bytes else im
        q.save(path, "PNG", optimize=True)
        if not max_bytes or path.stat().st_size <= max_bytes:
            return im.size
        im = im.resize((int(im.width * 0.85), int(im.height * 0.85)), Image.LANCZOS)


def zoom_centers(trees, root):
    """Four zoom boxes: Alumni Lawn (origin), Peabody lawn, densest grove tile, a big parking lot."""
    import shapely
    centers = {"alumni_lawn": (0.0, 0.0)}
    hero = load_json(Path(tc.fr.CONFIG) / "hero_spots.json")
    pe = [s for s in hero["spots"] if s["spot"] == "Peabody Esplanade" and s["match"]]
    centers["peabody_lawn"] = tuple(pe[0]["match"]["centroid"]) if pe else (450.0, -670.0)
    per_tile = {}
    for t in trees:
        if t["kind"] == "tree" and t["zone"] == "campus":
            per_tile[t["tile"]] = per_tile.get(t["tile"], 0) + 1
    dense = max(per_tile, key=per_tile.get)
    b = tc.fr.tile_bounds(dense)
    centers["dense_grove"] = ((b[0] + b[2]) / 2, (b[1] + b[3]) / 2)
    nodes, ways, rels, _ = tc.osm_elements(root)
    campus = tc.zone_polys()["campus"]
    best = None
    for el in ways.values():
        tg = el.get("tags", {})
        if tg.get("amenity") == "parking" and "building" not in tg and tg.get("parking") in (None, "surface"):
            poly = tc.area_with_holes(el, nodes, ways)
            if poly is None:
                continue
            poly = tc.to_campus_geom(poly)
            if campus.contains(poly.centroid) and (best is None or poly.area > best.area):
                best = poly
    centers["parking_lot"] = (best.centroid.x, best.centroid.y) if best is not None else (0.0, -200.0)
    return {k: (round(float(v[0]), 1), round(float(v[1]), 1)) for k, v in centers.items()}, dense, per_tile[dense]


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--jobs", type=int, default=max(1, min(12, (os.cpu_count() or 2) - 2)))
    p.add_argument("--git-copies", action="store_true", help="also write the two <= 1.5 MB pictures into git")
    p.add_argument("--zoom-size", type=float, default=160.0, help="zoom box side (m)")
    p.add_argument("--no-manifest", action="store_true")
    args = p.parse_args(argv)
    t0 = time.time()
    root = data_root()
    tdir = root / "derived/trees/v1"
    if not (tdir / "trees.json").exists():
        fail("derived/trees/v1/trees.json is missing; run trees_detect.py first")
    data = load_json(tdir / "trees.json")
    trees = data["trees"]
    crowns, g = tc.read_raster(tdir / "crowns.tif")
    out_dir = root / "reports/stage1"

    # (a) + (b) from the points inside each crown.
    print("reading points for the crown checks...", flush=True)
    st = crown_point_checks(trees, crowns, g, args.jobs, root)
    lid = [i for i, t in enumerate(trees) if t["kind"] == "tree" and t["source"] != "osm_only"]
    h_err = np.array([trees[i]["height"] - st["p99"][i] for i in lid])
    p_err = np.array([np.hypot(trees[i]["x"] - st["top_x"][i], trees[i]["y"] - st["top_y"][i]) for i in lid])
    zones = np.array([trees[i]["zone"] for i in lid])
    acc = {"height_vs_p99": {}, "position_vs_top10_centroid": {}}
    for z in ("campus", "ring", "all"):
        m = np.ones(len(lid), bool) if z == "all" else zones == z
        acc["height_vs_p99"][z] = summary(h_err[m], H_TARGET)
        acc["position_vs_top10_centroid"][z] = summary(p_err[m], POS_TARGET)
    acc["height_vs_p99"]["signed_median"] = round(float(np.nanmedian(h_err)), 3)
    big = [i for i in lid if trees[i]["height"] >= 10]
    acc["height_vs_p99"]["trees_10m_plus"] = summary([trees[i]["height"] - st["p99"][i] for i in big], H_TARGET)
    acc["position_vs_top10_centroid"]["trees_10m_plus"] = summary(
        [np.hypot(trees[i]["x"] - st["top_x"][i], trees[i]["y"] - st["top_y"][i]) for i in big], POS_TARGET)
    over = []
    for k, i in enumerate(lid):
        if (np.isfinite(h_err[k]) and abs(h_err[k]) > H_TARGET) or (np.isfinite(p_err[k]) and p_err[k] > POS_TARGET):
            over.append({"id": trees[i]["id"], "zone": trees[i]["zone"], "height": trees[i]["height"],
                         "height_err": round(float(h_err[k]), 2), "pos_err": round(float(p_err[k]), 2),
                         "points": trees[i]["points"]})

    # OSM nodes (hand-placed): matched pairs, and nearest detected tree for every node.
    from scipy.spatial import cKDTree
    matched = [t["osm_offset_m"] for t in trees if "osm_offset_m" in t]
    osm = [o for o in tc.osm_trees(root) if tc.zone_of(o["x"], o["y"])[0] != "outside"]
    lt = [t for t in trees if t["kind"] == "tree" and t["source"] != "osm_only"]
    kd = cKDTree(np.array([[t["x"], t["y"]] for t in lt]))
    near = kd.query(np.array([[o["x"], o["y"]] for o in osm]))[0] if osm else np.zeros(0)
    acc["position_vs_osm"] = {
        "note": "OSM tree nodes are hand-placed on imagery; matched pairs are within 2 m by construction",
        "matched_pairs": summary(matched, POS_TARGET),
        "nearest_detected_tree_all_nodes": summary(near, POS_TARGET),
        "nodes_in_area": len(osm),
    }

    # (c) coverage.
    print("coverage...", flush=True)
    cov = coverage(trees, g, root)

    # (d) pictures.
    print("pictures...", flush=True)
    ortho, oside = tc.read_raster(root / "derived/ortho/v1/naip2021_campus_0.6m.tif")
    rb = tc.zone_polys()["backdrop_ring"].bounds
    files = []
    img, x0, yt, mpp = crop(ortho, oside, rb[0] - 10, rb[1] - 10, rb[2] + 10, rb[3] + 10)
    draw_boundary(img, x0, yt, mpp, 3)
    n_campus = data["counts"]["campus"]["tree"]
    draw_trees(img, x0, yt, mpp, trees, 1, [
        f"Stage 1 trees: {n_campus} campus + {data['counts']['ring']['tree']} ring (crown circles)",
        "yellow LiDAR, green LiDAR + OSM, pink OSM only; orange campus, cyan ring",
        "NAIP 2021 0.6 m (public domain); USGS 3DEP LiDAR; OSM (ODbL)"])
    ov = out_dir / "trees_overview.png"
    save_png_limited(img, ov)
    files.append(ov)
    centers, dense_tile, dense_n = zoom_centers(trees, root)
    mpath = root / "derived/ortho/v1/metro2026_campus_0.15m.tif"
    metro, mside = tc.read_raster(mpath) if mpath.exists() else (None, None)
    zooms = {}
    half = args.zoom_size / 2
    for name, (cx, cy) in centers.items():
        zi, zx0, zyt, zmpp = crop(ortho, oside, cx - half, cy - half, cx + half, cy + half, scale=4)
        draw_trees(zi, zx0, zyt, zmpp, trees, 2, [f"{name.replace('_', ' ')} ({cx:.0f}, {cy:.0f}), "
                                                    f"{args.zoom_size:.0f} m box", "NAIP 2021 0.6 m, x4"])
        zp = out_dir / f"trees_zoom_{name}.png"
        save_png_limited(zi, zp)
        files.append(zp)
        zooms[name] = {"center": [cx, cy], "file": zp.relative_to(root).as_posix()}
        if metro is not None:
            # Archive-only copy on the sharper 2026 Metro imagery (license unclear: never shared).
            mi, mx0, myt, mmpp = crop(metro, mside, cx - half, cy - half, cx + half, cy + half)
            draw_trees(mi, mx0, myt, mmpp, trees, 2, [f"{name.replace('_', ' ')} on Metro 2026 0.15 m",
                                                      "reference only - do not share"])
            mp = out_dir / f"trees_zoom_{name}_metro2026_private.png"
            save_png_limited(mi, mp)
            files.append(mp)
            zooms[name]["metro_file_private"] = mp.relative_to(root).as_posix()
    zooms["dense_grove"]["tile"] = dense_tile
    zooms["dense_grove"]["trees_in_tile"] = dense_n
    git = {}
    if args.git_copies:
        a = GIT_IMAGES / "stage1-trees-overview.png"
        sz = save_png_limited(Image.open(ov).convert("RGB"), a, GIT_MAX_BYTES)
        b = GIT_IMAGES / "stage1-trees-zoom.png"
        sz2 = save_png_limited(Image.open(out_dir / "trees_zoom_alumni_lawn.png").convert("RGB"), b, GIT_MAX_BYTES)
        git = {a.name: {"px": list(sz), "bytes": a.stat().st_size}, b.name: {"px": list(sz2), "bytes": b.stat().st_size}}

    counts = data["counts"]
    report = {
        "made": datetime.datetime.now().isoformat(timespec="seconds"),
        "standard": "STANDARDS.md section 5: tree position <= 1 m, height <= 1.5 m",
        "trees_json_made": data["made"],
        "counts": counts, "dropped_counts": data["dropped_counts"], "osm": data["osm"],
        "accuracy": acc,
        "coverage": cov,
        "coverage_note": "Metro canopy is 2021 (from imagery); the LiDAR is the USGS D22 collection. Crown circles "
                         "are round, real crowns are not, so neither proxy can reach 100%.",
        "pictures": {"overview": ov.relative_to(root).as_posix(), "zooms": zooms, "git_copies": git},
        "over_target_count": len(over),
        "over_target": over,
    }
    out_dir.mkdir(parents=True, exist_ok=True)
    rp = out_dir / "trees_accuracy.json"
    save_json(rp, report)
    files.append(rp)
    if not args.no_manifest:
        db = manifest.connect(root)
        for f in files:
            manifest.add_file(f, "trees_check.py", "Derived: USGS LiDAR, NAIP (public domain), OSM (ODbL 1.0)", 1,
                              root=root, db=db)
        db.commit(); db.close()
    show = {k: acc[k]["all"] for k in ("height_vs_p99", "position_vs_top10_centroid")}
    print(f"height/position: {show}")
    print(f"osm: {acc['position_vs_osm']}")
    print(f"coverage campus: {cov['campus']}")
    print(f"{len(over)} trees over target; wrote {rp} in {time.time() - t0:.0f} s")


if __name__ == "__main__":
    main()
