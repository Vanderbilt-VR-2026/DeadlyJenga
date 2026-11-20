"""Helpers shared by the tree and backdrop-ring scripts (trees_*.py, tree_kit.py, ring_lod1.py).

Nothing here writes files. It reads:
  - the Stage 0 rasters (derived/rasters/v1/*.tif + JSON sidecars), all on one 0.5 m grid
  - OSM building footprints and tree nodes (raw/osm/<date>/campus_overpass.json)
  - Metro building footprints and 2021 tree canopy (raw/metro/<date>/...; GeoJSON in EPSG:4326)
and turns everything into the campus frame (meters, config/frame.json).

A "grid" is the sidecar dict of a raster: origin_x, origin_y (top-left corner), cell_m,
width, height. Row 0 is the north edge.
"""
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
from common import data_root, fail, load_json  # noqa: E402

FT = 0.3048                         # one international foot (Metro "Height" is in feet)


# ---- rasters --------------------------------------------------------------

def read_raster(path):
    """(array, sidecar) for a Stage 0 raster. Multi-band rasters come back as (rows, cols, bands)."""
    import rasterio
    path = Path(path)
    if not path.exists():
        fail(f"missing {path}; run make_rasters.py first")
    side = load_json(path.with_suffix(".json"))
    with rasterio.open(path) as src:
        a = src.read()
    return (a[0] if a.shape[0] == 1 else np.moveaxis(a, 0, 2)), side


def rasters_dir(root=None):
    return Path(root or data_root()) / "derived" / "rasters" / "v1"


def cell_of(x, y, g):
    """(row, col) integer cell indices of campus points (may fall outside the grid)."""
    c = np.floor((np.asarray(x) - g["origin_x"]) / g["cell_m"]).astype(np.int64)
    r = np.floor((g["origin_y"] - np.asarray(y)) / g["cell_m"]).astype(np.int64)
    return r, c


def inside_grid(r, c, g):
    return (r >= 0) & (r < g["height"]) & (c >= 0) & (c < g["width"])


def cell_center(r, c, g):
    x = g["origin_x"] + (np.asarray(c) + 0.5) * g["cell_m"]
    y = g["origin_y"] - (np.asarray(r) + 0.5) * g["cell_m"]
    return x, y


def sample(a, g, x, y, fill=np.nan):
    """Nearest-cell value of raster a at campus points."""
    r, c = cell_of(x, y, g)
    ok = inside_grid(r, c, g)
    out = np.full(np.shape(r), fill, dtype=np.float64)
    out[ok] = a[r[ok], c[ok]]
    return out


def sample_bilinear(a, g, x, y):
    """Bilinear value of raster a at campus points (edge cells clamp)."""
    fx = (np.asarray(x) - g["origin_x"]) / g["cell_m"] - 0.5
    fy = (g["origin_y"] - np.asarray(y)) / g["cell_m"] - 0.5
    c0 = np.clip(np.floor(fx).astype(np.int64), 0, g["width"] - 2)
    r0 = np.clip(np.floor(fy).astype(np.int64), 0, g["height"] - 2)
    tx = np.clip(fx - c0, 0, 1)
    ty = np.clip(fy - r0, 0, 1)
    v00, v01 = a[r0, c0], a[r0, c0 + 1]
    v10, v11 = a[r0 + 1, c0], a[r0 + 1, c0 + 1]
    return (v00 * (1 - tx) * (1 - ty) + v01 * tx * (1 - ty) + v10 * (1 - tx) * ty + v11 * tx * ty)


def affine(g):
    from rasterio.transform import Affine
    return Affine(g["cell_m"], 0, g["origin_x"], 0, -g["cell_m"], g["origin_y"])


def write_raster(path, a, g, units, extra=None):
    """Write a one-band GeoTIFF on grid g plus its JSON sidecar (same layout as Stage 0)."""
    import datetime
    import rasterio
    from common import save_json
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    prof = {"driver": "GTiff", "width": g["width"], "height": g["height"], "count": 1, "dtype": a.dtype,
            "transform": affine(g), "tiled": True, "blockxsize": 512, "blockysize": 512,
            "compress": "DEFLATE", "predictor": 3 if a.dtype == np.float32 else 2}
    with rasterio.open(path, "w", **prof) as dst:
        dst.write(a[None])
    side = {k: g[k] for k in ("origin_x", "origin_y", "cell_m", "width", "height")}
    side.update({"file": path.name, "frame": "Vanderbilt campus frame v1 (Tools/VanderbiltCampus/config/frame.json)",
                 "origin_is": "top-left corner of the top-left cell (campus meters)",
                 "row0": "north edge (y decreases down the rows)",
                 "pixel_to_campus": "X = origin_x + (col + 0.5) * cell_m ; Y = origin_y - (row + 0.5) * cell_m",
                 "units": units, "dtype": str(a.dtype),
                 "made": datetime.datetime.now().isoformat(timespec="seconds")})
    side.update(extra or {})
    save_json(path.with_suffix(".json"), side)
    return [path, path.with_suffix(".json")]


def rasterize(shapes, g, dtype=np.uint8, fill=0, all_touched=False):
    """Burn (geometry, value) pairs into an array on grid g."""
    from rasterio import features
    shapes = [(s, v) for s, v in shapes if s is not None and not s.is_empty]
    if not shapes:
        return np.full((g["height"], g["width"]), fill, dtype)
    return features.rasterize(shapes, out_shape=(g["height"], g["width"]), transform=affine(g),
                              fill=fill, dtype=dtype, all_touched=all_touched)


# ---- boundary / zones ----------------------------------------------------

_polys = None


def zone_polys():
    """{"campus": polygon, "backdrop_ring": polygon} in campus meters (cached)."""
    global _polys
    if _polys is None:
        import shapely
        _polys = fr.boundary_polygons()
        for p in _polys.values():
            shapely.prepare(p)
    return _polys


def zone_of(x, y):
    """'campus', 'ring' or 'outside' for each campus point (arrays)."""
    import shapely
    p = zone_polys()
    x, y = np.atleast_1d(np.asarray(x, float)), np.atleast_1d(np.asarray(y, float))
    out = np.full(x.shape, "outside", dtype=object)
    out[shapely.contains_xy(p["backdrop_ring"], x, y)] = "ring"
    out[shapely.contains_xy(p["campus"], x, y)] = "campus"
    return out


# ---- source data -----------------------------------------------------------

def newest(root, pattern):
    hits = sorted(Path(root).glob(pattern))
    if not hits:
        fail(f"nothing matches {pattern} under {root}")
    return hits[-1]


def osm_snapshot(root=None):
    return newest(Path(root or data_root()) / "raw" / "osm", "*/campus_overpass.json")


def metro_dir(root=None):
    return newest(Path(root or data_root()) / "raw" / "metro", "*/footprints").parent


def to_campus_geom(geom):
    """WGS84 lon/lat shapely geometry -> campus meters."""
    import shapely
    return shapely.transform(geom, lambda x, y: fr.to_campus(x, y, np.zeros_like(x), 4326)[:2],
                             interleaved=False)


_osm = None


def osm_elements(root=None):
    """(nodes, ways, rels, path) of the newest OSM snapshot (cached)."""
    global _osm
    if _osm is None:
        import make_boundary as mb
        path = osm_snapshot(root)
        data = load_json(path)
        nodes, ways, rels = mb.index(data["elements"])
        _osm = (nodes, ways, rels, path)
    return _osm


def area_with_holes(el, nodes, ways):
    """Like make_boundary.area_of, but a multipolygon's inner ways become holes (courtyards)."""
    import make_boundary as mb
    from shapely.geometry import LineString
    from shapely.ops import polygonize, unary_union
    if el["type"] == "way":
        return mb.area_of(el, nodes, ways)
    rings = {"outer": [], "inner": []}
    for m in el.get("members", []):
        if m["type"] == "way" and m["ref"] in ways:
            role = "inner" if m.get("role") == "inner" else "outer"
            c = mb.way_coords(ways[m["ref"]], nodes)
            if len(c) >= 2:
                rings[role].append(LineString(c))
    outer = list(polygonize(unary_union(rings["outer"]))) if rings["outer"] else []
    if not outer:
        return None
    poly = unary_union(outer)
    inner = list(polygonize(unary_union(rings["inner"]))) if rings["inner"] else []
    if inner:
        poly = poly.difference(unary_union(inner))
    return poly


def osm_buildings(root=None):
    """OSM building footprints: list of dicts {osm, id, poly (campus meters), tags}.
    Multipolygon inner rings are kept as holes."""
    nodes, ways, rels, _ = osm_elements(root)
    out = []
    for el in list(ways.values()) + list(rels.values()):
        tags = el.get("tags", {})
        if "building" not in tags or tags.get("building") == "no":
            continue
        poly = area_with_holes(el, nodes, ways)
        if poly is None or poly.is_empty:
            continue
        poly = to_campus_geom(poly).buffer(0)
        if poly.area < 2.0:
            continue
        out.append({"osm": f"{el['type']}/{el['id']}", "id": f"B_{el['id']}", "poly": poly, "tags": tags})
    return out


def osm_trees(root=None):
    """OSM natural=tree nodes: list of {osm_id, x, y, tags}."""
    nodes, _, _, _ = osm_elements(root)
    pts = [n for n in nodes.values() if n.get("tags", {}).get("natural") == "tree"]
    if not pts:
        return []
    X, Y, _ = fr.to_campus(np.array([p["lon"] for p in pts]), np.array([p["lat"] for p in pts]),
                           np.zeros(len(pts)), 4326)
    return [{"osm_id": p["id"], "x": float(x), "y": float(y), "tags": p.get("tags", {})}
            for p, x, y in zip(pts, X, Y)]


def read_geojson_pages(folder, prefix):
    import json
    feats = []
    for f in sorted(Path(folder).glob(prefix + "_page*.geojson")):
        with open(f, encoding="utf-8") as fh:
            gj = json.load(fh)
        crs = gj.get("crs", {}).get("properties", {}).get("name", "EPSG:4326")
        if "4326" not in crs and "CRS84" not in crs:
            fail(f"{f}: expected EPSG:4326, found {crs}")
        feats += gj["features"]
    return feats


def metro_footprints(root=None):
    """Metro footprints: list of {objectid, poly (campus), height_m (or None), roof}."""
    from shapely.geometry import shape
    out = []
    for ft in read_geojson_pages(metro_dir(root) / "footprints", "footprints"):
        if not ft.get("geometry"):
            continue
        poly = to_campus_geom(shape(ft["geometry"])).buffer(0)
        if poly.is_empty or poly.area < 2.0:
            continue
        p = ft.get("properties", {})
        h = p.get("Height")
        out.append({"objectid": p.get("OBJECTID", ft.get("id")), "poly": poly,
                    "height_m": float(h) * FT if h not in (None, 0) else None,
                    "roof": p.get("RoofType")})
    return out


def canopy_polygons(root=None):
    """Metro 2021 tree canopy polygons in campus meters."""
    from shapely.geometry import shape
    out = []
    for ft in read_geojson_pages(metro_dir(root) / "canopy", "canopy"):
        if ft.get("geometry"):
            g = to_campus_geom(shape(ft["geometry"])).buffer(0)
            if not g.is_empty:
                out.append(g)
    return out


def ring_points(cx, cy, r, n=12):
    """n points on a circle (for drawing)."""
    a = np.linspace(0, 2 * np.pi, n, endpoint=False)
    return np.column_stack([cx + r * np.cos(a), cy + r * np.sin(a)])
