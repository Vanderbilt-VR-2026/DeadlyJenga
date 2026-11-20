"""Ground surface materials from OpenStreetMap (Stage 1, terrain step 1).

Turns the OSM snapshot into material regions in the campus frame:
  derived/terrain/v1/surfaces.geojson          one feature per material (campus meters,
                                               overlaps already resolved by priority),
                                               plus the tag -> material table and the default rule
  derived/terrain/v1/<tile>/surface.npy        uint8 material index per 0.25 m pixel (400 x 400),
                                               row 0 = south edge, column 0 = west edge
  derived/terrain/v1/surface_grid.json         how to read surface.npy
  reports/stage1/surfaces.json                 areas per material (campus / ring), rule counts

Priority where regions overlap (high -> low):
  water > building footprint (paving_concrete; the building step covers it) > roads (asphalt)
  > parking (asphalt) > footways / paths / pedestrian areas (paving_concrete or paving_brick)
  > planting (flowerbeds, scrub, hedges, woods) > grass (lawns, parks, gardens, pitches)
  > default.
Planting sits above grass (the task listed it below) because flowerbeds and hedges are drawn
inside parks and gardens; with grass on top they would vanish.

Default (nothing mapped): a weak LiDAR hint per 1 m cell. "Soft" if the cell has low or medium
vegetation points (classes 3, 4) or its ground returns are bright (median intensity above a
threshold calibrated between mapped grass and mapped hard surfaces). Soft -> grass, else
paving_concrete. A 5 m majority filter removes speckle.

Usage:
  python surfaces.py            build everything (skips if done)
  python surfaces.py --force    rebuild
"""
import argparse
import json
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
import vcmesh  # noqa: E402
from common import data_root, fail, load_json, save_json  # noqa: E402

VERSION = "v1"
PIXEL = 0.25                      # surface raster cell (m)
OSM_LICENSE = "ODbL 1.0 (c) OpenStreetMap contributors"

# Road widths (m) when there is no width tag. With a lanes tag: lanes * LANE_M + 1 m.
ROAD_WIDTH = {"motorway": 16, "trunk": 16, "primary": 16, "primary_link": 8,
              "secondary": 11, "secondary_link": 7, "tertiary": 10, "tertiary_link": 7,
              "residential": 7, "unclassified": 7, "living_street": 6, "service": 5}
LANE_M = 3.25
PATH_WIDTH = {"footway": 2.5, "path": 2.0, "steps": 2.5, "cycleway": 2.5, "pedestrian": 4.0,
              "track": 3.0, "bridleway": 2.0}
BRICK_SURFACES = {"paving_stones", "brick", "sett", "cobblestone", "unhewn_cobblestone"}
SOFT_SURFACES = {"grass", "ground", "dirt", "earth", "gravel", "unpaved", "sand", "mud", "fine_gravel",
                 "woodchips", "grass_paver"}
HEDGE_WIDTH = 1.2

# Priority order, lowest first (later layers paint over earlier ones).
LAYERS = ["grass", "planting", "footway", "parking", "road", "building", "water"]
LAYER_RULES = [
    {"layer": "water", "material": "water",
     "tags": "natural=water; amenity=fountain (areas; nodes as 2 m radius); leisure=swimming_pool unless indoor"},
    {"layer": "building", "material": "paving_concrete",
     "tags": "building=* areas (not building=roof, not underground); the building step covers them"},
    {"layer": "road", "material": "asphalt",
     "tags": "highway=motorway..service lines, buffered by width tag, else lanes*3.25+1 m, else defaults "
             + json.dumps(ROAD_WIDTH) + "; surface=paving_stones/brick/sett -> paving_brick; "
             "tunnels and building passages skipped"},
    {"layer": "parking", "material": "asphalt",
     "tags": "amenity=parking areas (not multi-storey / underground)"},
    {"layer": "footway", "material": "paving_concrete (paving_brick when surface=paving_stones/brick/sett)",
     "tags": "highway=footway/path/steps/cycleway/pedestrian/track lines buffered by width tag else "
             + json.dumps(PATH_WIDTH) + "; highway=pedestrian/footway areas as polygons; "
             "unpaved surfaces (grass, ground, gravel...) skipped"},
    {"layer": "planting", "material": "planting",
     "tags": "landuse=flowerbed; natural=scrub/shrubbery/wood; landuse=forest; "
             "barrier=hedge lines buffered 1.2 m"},
    {"layer": "grass", "material": "grass",
     "tags": "landuse=grass/meadow/village_green/recreation_ground; leisure=park/garden/pitch/dog_park/"
             "playground; natural=grassland/grass; golf=*"},
]


def out_dir(root):
    return Path(root) / "derived" / "terrain" / VERSION


def terrain_tiles(root=None):
    """Index entries of every tile in zone campus or ring."""
    return [t for t in lidar_io.load_index(root=root)["tiles"] if t["zone"] in ("campus", "ring")]


# ---- OSM -> shapes in the campus frame ------------------------------------

def load_osm(root):
    import make_boundary as mb
    snap = mb.newest_snapshot(Path(root))
    data = load_json(snap)
    nodes, ways, rels = mb.index(data["elements"])
    ids = np.array(list(nodes.keys()))
    lon = np.array([nodes[i]["lon"] for i in ids])
    lat = np.array([nodes[i]["lat"] for i in ids])
    X, Y, _ = fr.to_campus(lon, lat, np.zeros_like(lon), 4326)
    xy = {int(i): (float(x), float(y)) for i, x, y in zip(ids, X, Y)}
    return snap, data["elements"], nodes, ways, rels, xy


def way_line(way, xy):
    return [xy[n] for n in way.get("nodes", []) if n in xy]


def way_polygon(way, xy):
    from shapely.geometry import Polygon
    c = way_line(way, xy)
    if len(c) >= 4 and way["nodes"][0] == way["nodes"][-1]:
        p = Polygon(c).buffer(0)
        return p if not p.is_empty else None
    return None


def relation_polygon(rel, ways, xy):
    """Multipolygon relation: outer rings minus inner rings."""
    from shapely.geometry import LineString
    from shapely.ops import polygonize, unary_union
    outer, inner = [], []
    for m in rel.get("members", []):
        if m["type"] == "way" and m["ref"] in ways:
            c = way_line(ways[m["ref"]], xy)
            if len(c) >= 2:
                (inner if m.get("role") == "inner" else outer).append(LineString(c))
    if not outer:
        return None
    poly = unary_union(list(polygonize(unary_union(outer))))
    if inner:
        poly = poly.difference(unary_union(list(polygonize(unary_union(inner)))))
    return None if poly.is_empty else poly


def num(text):
    """First number in a tag value like '7', '7 m', '7.5;8'. None if not a number."""
    try:
        return float(str(text).split(";")[0].replace("m", "").strip())
    except ValueError:
        return None


def road_width(tags):
    w = num(tags.get("width")) if "width" in tags else None
    if w and 2 <= w <= 40:
        return w, "width tag"
    lanes = num(tags.get("lanes")) if "lanes" in tags else None
    if lanes and 1 <= lanes <= 10:
        return lanes * LANE_M + 1.0, "lanes tag"
    return float(ROAD_WIDTH[tags["highway"]]), "default"


def classify(elements, ways, xy):
    """List of (layer, material, shape, element id, rule) for every OSM element that paints the ground."""
    from shapely.geometry import LineString, Point
    out = []
    for el in elements:
        tags = el.get("tags") or {}
        if not tags:
            continue
        kind = el["type"]
        if kind == "node":
            if tags.get("amenity") == "fountain" and el["id"] in xy:
                out.append(("water", "water", Point(xy[el["id"]]).buffer(2.0), f"node/{el['id']}", "fountain node"))
            continue
        if kind == "relation" and tags.get("type") != "multipolygon":
            continue
        closed = kind == "relation" or (len(el.get("nodes", [])) >= 4 and el["nodes"][0] == el["nodes"][-1])

        def area():
            return relation_polygon(el, ways, xy) if kind == "relation" else way_polygon(el, xy)

        eid = f"{kind}/{el['id']}"
        hw, lu, le, na, am = (tags.get(k) for k in ("highway", "landuse", "leisure", "natural", "amenity"))
        surface = tags.get("surface", "")
        indoor = tags.get("indoor") == "yes" or tags.get("location") in ("indoor", "underground")
        underground = tags.get("location") == "underground" or (num(tags.get("layer", 0)) or 0) < 0

        # water
        if (na == "water" or am == "fountain" or (le == "swimming_pool" and not indoor)) and closed:
            g = area()
            if g is not None:
                out.append(("water", "water", g, eid, f"natural={na}" if na else f"{'amenity=fountain' if am else 'leisure=swimming_pool'}"))
            continue
        # buildings
        if "building" in tags and tags["building"] not in ("roof", "no") and closed and not underground:
            g = area()
            if g is not None:
                out.append(("building", "paving_concrete", g, eid, "building=*"))
            # a building may also carry amenity=parking etc.; the building wins anyway
            continue
        # roads and paths (lines, or areas with area=yes)
        if hw:
            if tags.get("tunnel") in ("yes", "building_passage") or tags.get("covered") == "yes":
                continue
            is_area = closed and tags.get("area") == "yes"
            if hw in ROAD_WIDTH:
                mat = "paving_brick" if surface in BRICK_SURFACES else "asphalt"
                if is_area:
                    g = area()
                    w_rule = "area"
                else:
                    c = way_line(el, xy)
                    if len(c) < 2:
                        continue
                    w, w_rule = road_width(tags)
                    g = LineString(c).buffer(w / 2)
                if g is not None:
                    out.append(("road", mat, g, eid, f"highway={hw} ({w_rule})"))
            elif hw in PATH_WIDTH:
                if surface in SOFT_SURFACES:
                    continue
                mat = "paving_brick" if surface in BRICK_SURFACES else "paving_concrete"
                if is_area or (hw == "pedestrian" and closed):
                    g = area()
                    rule = f"highway={hw} area"
                else:
                    c = way_line(el, xy)
                    if len(c) < 2:
                        continue
                    w = num(tags.get("width")) if "width" in tags else None
                    w = w if w and 0.5 <= w <= 30 else PATH_WIDTH[hw]
                    g = LineString(c).buffer(w / 2)
                    rule = f"highway={hw}" + (" (width tag)" if "width" in tags else "")
                if g is not None:
                    out.append(("footway", mat, g, eid, rule))
            continue
        if am == "parking" and closed and tags.get("parking") not in ("multi-storey", "underground"):
            g = area()
            if g is not None:
                out.append(("parking", "asphalt", g, eid, "amenity=parking"))
            continue
        if tags.get("barrier") == "hedge" and kind == "way":
            c = way_line(el, xy)
            if len(c) >= 2:
                g = area() if closed and tags.get("area") == "yes" else LineString(c).buffer(HEDGE_WIDTH / 2)
                if g is not None:
                    out.append(("planting", "planting", g, eid, "barrier=hedge"))
            continue
        if closed and (lu in ("flowerbed", "forest") or na in ("scrub", "shrubbery", "wood")):
            g = area()
            if g is not None:
                out.append(("planting", "planting", g, eid, f"landuse={lu}" if lu else f"natural={na}"))
            continue
        if closed and (lu in ("grass", "meadow", "village_green", "recreation_ground")
                       or le in ("park", "garden", "pitch", "dog_park", "playground")
                       or na in ("grassland", "grass") or "golf" in tags):
            if le == "pitch" and surface in ("tartan", "asphalt", "concrete", "clay"):
                continue
            g = area()
            if g is not None:
                rule = f"landuse={lu}" if lu else (f"leisure={le}" if le else (f"natural={na}" if na else "golf"))
                out.append(("grass", "grass", g, eid, rule))
    return out


def paint_layers(items, gx0, gy0, W, H, mat_idx, pixel=PIXEL):
    """Material raster (row 0 = south) from classified shapes: lowest priority layer first,
    higher layers paint over it. 255 = nothing mapped."""
    import rasterio.features
    from rasterio.transform import Affine
    transform = Affine(pixel, 0, gx0, 0, -pixel, gy0 + H * pixel)    # rasterize north-up, then flip
    raster = np.full((H, W), 255, np.uint8)
    for layer in LAYERS:
        shapes = [(g, mat_idx[m]) for (lay, m, g, _, _) in items if lay == layer]
        if shapes:
            rasterio.features.rasterize(shapes, out=raster, transform=transform, all_touched=False)
    return np.flipud(raster)


# ---- default rule from LiDAR ----------------------------------------------

def lidar_hint(tiles, x0, y0, nx, ny):
    """Per 1 m cell: median ground intensity (nan if none) and count of class 3/4 points."""
    inten = np.full(ny * nx, np.nan, np.float32)
    lowveg = np.zeros(ny * nx, np.int32)
    folder = lidar_io.lidar_dir()
    for t in tiles:
        d = lidar_io.read_tile(folder / t["file"])
        ci = np.floor(d["x"] - x0).astype(np.int64)
        cj = np.floor(d["y"] - y0).astype(np.int64)
        ok = (ci >= 0) & (ci < nx) & (cj >= 0) & (cj < ny)
        cell = cj * nx + ci
        cls = d["classification"]
        v = ok & np.isin(cls, (3, 4))
        np.add.at(lowveg, cell[v], 1)
        g = ok & (cls == 2)
        if g.any():
            c, val = cell[g], d["intensity"][g].astype(np.float32)
            order = np.lexsort((val, c))
            c, val = c[order], val[order]
            starts = np.flatnonzero(np.r_[True, c[1:] != c[:-1]])
            counts = np.diff(np.r_[starts, c.size])
            med = val[starts + counts // 2]
            inten[c[starts]] = med
    return inten.reshape(ny, nx), lowveg.reshape(ny, nx)


def majority(mask, size):
    """Majority (mode) filter of a boolean array over a size x size window."""
    from scipy.ndimage import uniform_filter
    return uniform_filter(mask.astype(np.float32), size=size, mode="nearest") > 0.5


# ---- main build -----------------------------------------------------------

def build(root, force=False):
    import rasterio.features
    import shapely
    from rasterio.transform import Affine
    from shapely.geometry import mapping
    from shapely.ops import unary_union

    t_start = time.time()
    od = out_dir(root)
    gj_path = od / "surfaces.geojson"
    tiles = terrain_tiles(root)
    if gj_path.exists() and not force and all((od / t["name"] / "surface.npy").exists() for t in tiles):
        print(f"surfaces already built ({gj_path}); use --force to rebuild")
        return
    mat_idx = vcmesh.material_index()

    # Grid over every campus + ring tile.
    gx0 = min(t["bounds"][0] for t in tiles)
    gy0 = min(t["bounds"][1] for t in tiles)
    gx1 = max(t["bounds"][2] for t in tiles)
    gy1 = max(t["bounds"][3] for t in tiles)
    W, H = int(round((gx1 - gx0) / PIXEL)), int(round((gy1 - gy0) / PIXEL))
    print(f"grid {W} x {H} pixels at {PIXEL} m over X {gx0}..{gx1}, Y {gy0}..{gy1}")

    snap, elements, nodes, ways, rels, xy = load_osm(root)
    items = classify(elements, ways, xy)
    print(f"{len(items)} OSM shapes paint the ground")

    transform = Affine(PIXEL, 0, gx0, 0, -PIXEL, gy1)
    raster = paint_layers(items, gx0, gy0, W, H, mat_idx)
    mapped = raster != 255
    layer_shapes = {layer: [(g, m) for (lay, m, g, _, _) in items if lay == layer] for layer in LAYERS}
    rule_counts = {}
    for (lay, m, g, eid, rule) in items:
        key = f"{lay}: {rule} -> {m}"
        rule_counts[key] = rule_counts.get(key, 0) + 1

    # Default rule: LiDAR hint on a 1 m grid.
    nx1, ny1 = int(gx1 - gx0), int(gy1 - gy0)
    inten, lowveg = lidar_hint(tiles, gx0, gy0, nx1, ny1)
    up = lambda a: np.repeat(np.repeat(a, 4, axis=0), 4, axis=1)      # 1 m -> 0.25 m
    inten_q, lowveg_q = up(inten), up(lowveg)
    grass_px = (raster == mat_idx["grass"]) & np.isfinite(inten_q)
    hard_px = np.isin(raster, [mat_idx["asphalt"], mat_idx["paving_concrete"], mat_idx["paving_brick"]]) \
        & np.isfinite(inten_q) & (lowveg_q == 0)
    # Building footprints are painted paving_concrete but are not measured ground; leave them out.
    bmask = np.zeros((H, W), np.uint8)
    bshapes = [g for g, _ in layer_shapes["building"]]
    if bshapes:
        rasterio.features.rasterize([(g, 1) for g in bshapes], out=bmask, transform=transform)
    bmask = np.flipud(bmask).astype(bool)
    hard_px &= ~bmask
    g_med = float(np.median(inten_q[grass_px][::16])) if grass_px.any() else 3000.0
    h_med = float(np.median(inten_q[hard_px][::16])) if hard_px.any() else 2000.0
    thresh = (g_med + h_med) / 2
    soft = (lowveg > 0) | (np.nan_to_num(inten, nan=-1) >= thresh)
    soft = majority(soft, 5)
    default = np.where(up(soft), mat_idx["grass"], mat_idx["paving_concrete"]).astype(np.uint8)
    check_soft = up(soft)
    agree_grass = float(check_soft[grass_px].mean()) if grass_px.any() else float("nan")
    agree_hard = float((~check_soft[hard_px]).mean()) if hard_px.any() else float("nan")
    raster = np.where(mapped, raster, default)
    print(f"default rule: intensity threshold {thresh:.0f} (grass median {g_med:.0f}, hard median {h_med:.0f}); "
          f"on mapped areas it agrees {agree_grass:.0%} on grass, {agree_hard:.0%} on hard")

    # Per-tile rasters.
    grid_info = {"version": VERSION, "pixel_m": PIXEL, "size": [400, 400],
                 "layout": "surface.npy[j, i] = material index of the pixel with X = xmin + (i + 0.5) * 0.25, "
                           "Y = ymin + (j + 0.5) * 0.25 (row 0 = south edge); xmin, ymin = tile_bounds(tile)",
                 "materials": "config/material_ids.json index", "frame": fr.load_frame()["name"]}
    db = manifest.connect(root)
    src = f"surfaces.py from {snap.relative_to(root).as_posix()} + derived/lidar/v1"
    for t in tiles:
        x0, y0, _, _ = t["bounds"]
        i0, j0 = int(round((x0 - gx0) / PIXEL)), int(round((y0 - gy0) / PIXEL))
        tile_r = np.ascontiguousarray(raster[j0:j0 + 400, i0:i0 + 400])
        p = od / t["name"] / "surface.npy"
        p.parent.mkdir(parents=True, exist_ok=True)
        np.save(p, tile_r)
        manifest.add_file(p, src, OSM_LICENSE, stage=1, root=root, db=db)
    save_json(od / "surface_grid.json", grid_info)
    manifest.add_file(od / "surface_grid.json", "surfaces.py", "", stage=1, root=root, db=db)

    # Resolved polygons: each layer minus everything above it, clipped to the tile area.
    area_box = shapely.box(gx0, gy0, gx1, gy1)
    above = shapely.Polygon()
    by_mat = {}
    for layer in reversed(LAYERS):
        groups = {}
        for g, m in layer_shapes[layer]:
            groups.setdefault(m, []).append(g)
        layer_union = []
        for m, gs in groups.items():
            u = unary_union(gs).intersection(area_box)
            u = u.difference(above)
            if not u.is_empty:
                by_mat.setdefault(m, []).append(u)
            layer_union.append(unary_union(gs))
        if layer_union:
            above = unary_union([above] + layer_union)
    polys = boundary_polys()
    features = []
    for m, gs in sorted(by_mat.items()):
        u = unary_union(gs).simplify(0.05)
        features.append({"type": "Feature",
                         "properties": {"material": m, "index": mat_idx[m], "area_m2": round(u.area, 1),
                                        "area_campus_m2": round(u.intersection(polys["campus"]).area, 1),
                                        "area_ring_m2": round(u.intersection(polys["backdrop_ring"]).area, 1)},
                         "geometry": json.loads(json.dumps(mapping(shapely.set_precision(u, 0.01))))})
    # Raster areas per material, inside campus and ring (includes the default rule).
    areas = raster_areas(raster, gx0, gy0, polys, mat_idx)
    gj = {"type": "FeatureCollection",
          "name": "Vanderbilt campus ground surfaces v1",
          "crs_note": "campus frame meters (config/frame.json), not a geographic CRS",
          "osm_snapshot": snap.relative_to(root).as_posix(),
          "priority_high_to_low": list(reversed(LAYERS)) + ["default"],
          "rules": LAYER_RULES,
          "default_rule": {"rule": "per 1 m cell: soft if class 3/4 points present or median ground intensity "
                                   ">= threshold; soft -> grass, else paving_concrete; 5 m majority filter. "
                                   "Raster only (not in these polygons).",
                           "intensity_threshold": round(thresh, 1),
                           "grass_median_intensity": round(g_med, 1), "hard_median_intensity": round(h_med, 1),
                           "agreement_on_mapped_grass": round(agree_grass, 3),
                           "agreement_on_mapped_hard": round(agree_hard, 3)},
          "rule_counts": dict(sorted(rule_counts.items())),
          "features": features}
    save_json(gj_path, gj)
    manifest.add_file(gj_path, src, OSM_LICENSE, stage=1, root=root, db=db)
    rep = Path(root) / "reports" / "stage1" / "surfaces.json"
    save_json(rep, {"made": time.strftime("%Y-%m-%dT%H:%M:%S"), "tiles": len(tiles),
                    "osm_shapes": len(items), "rule_counts": gj["rule_counts"],
                    "default_rule": gj["default_rule"], "raster_area_m2": areas,
                    "seconds": round(time.time() - t_start, 1)})
    manifest.add_file(rep, "surfaces.py", "", stage=1, root=root, db=db)
    db.commit()
    db.close()
    print(json.dumps(areas, indent=2))
    print(f"wrote {gj_path} and {len(tiles)} tile rasters in {time.time() - t_start:.0f} s")


def boundary_polys():
    return fr.boundary_polygons()


def raster_areas(raster, gx0, gy0, polys, mat_idx):
    """m2 per material inside the campus boundary and inside the ring (sampled at pixel centers)."""
    import rasterio.features
    from rasterio.transform import Affine
    H, W = raster.shape
    # south-up transform: row j -> Y = gy0 + (j + 0.5) * PIXEL; rasterize north-up then flip
    transform = Affine(PIXEL, 0, gx0, 0, -PIXEL, gy0 + H * PIXEL)
    zone = np.zeros((H, W), np.uint8)
    rasterio.features.rasterize([(polys["backdrop_ring"], 2), (polys["campus"], 1)], out=zone, transform=transform)
    zone = np.flipud(zone)
    names = {v: k for k, v in mat_idx.items()}
    out = {}
    for z, label in ((1, "campus"), (2, "ring")):
        vals, counts = np.unique(raster[zone == z], return_counts=True)
        out[label] = {names.get(int(v), str(v)): round(float(c) * PIXEL * PIXEL, 1) for v, c in zip(vals, counts)}
    return out


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--force", action="store_true", help="rebuild even if the outputs exist")
    args = p.parse_args(argv)
    build(data_root(), args.force)


if __name__ == "__main__":
    main()
