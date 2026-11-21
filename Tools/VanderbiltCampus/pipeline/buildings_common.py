"""Helpers shared by the Stage 1 building scripts (buildings_*.py, roof_*.py).

What is here:
  - where building files go (archive and git mirror)
  - OSM and Metro footprints converted to the campus frame
  - the LiDAR DTM raster (for "height above ground" of single points)
  - polygon helpers: dominant wall direction, moving single edges, corner lists
  - LidarEdge: the building outline as the LiDAR sees it (a 0.25 m mask of roof
    points) and a signed distance to it, used to check and correct footprints

Everything is in campus meters (config/frame.json): X east, Y north, Z up.
"""
import json
import math
import sys
from functools import lru_cache
from pathlib import Path

import numpy as np
import shapely
from shapely.geometry import LineString, Polygon, MultiPolygon, Point, shape
from shapely.ops import polygonize, unary_union

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
from common import TOOLS, data_root, fail, load_json, save_json  # noqa: E402

VERSION = "v1"
OSM_SNAPSHOT = "raw/osm/2026-11-19/campus_overpass.json"
METRO_DIR = "raw/metro/2026-11-19/footprints"
FT = 0.3048                       # international foot (Metro heights are in feet)

# LiDAR classes (ASPRS): 1 unclassified, 2 ground, 3/4/5 vegetation, 6 building,
# 7 low noise, 18 high noise.
VEG = (3, 4, 5)
NOISE = (7, 18)


# ---- paths ---------------------------------------------------------------

def out_dir(root=None):
    """derived/buildings/v1 in the archive (footprints.json, ring_footprints.json)."""
    return Path(root or data_root()) / "derived" / "buildings" / VERSION


def building_dir(bid, root=None, version=None):
    """Archive folder for one building's files: buildings/<id>/<version>/ (default v1)."""
    return Path(root or data_root()) / "buildings" / bid / (version or VERSION)


def git_building_dir(bid):
    """Small text mirror in git: Tools/VanderbiltCampus/buildings/<id>/."""
    return TOOLS / "buildings" / bid


def reports_dir(root=None):
    return Path(root or data_root()) / "reports" / "stage1"


def dumps(obj):
    return json.dumps(obj, indent=2)


def save(path, obj):
    save_json(path, obj)
    return Path(path)


# ---- geometry conversion --------------------------------------------------

def to_campus_geom(g):
    """lon/lat shapely geometry -> campus meters."""
    return shapely.transform(g, lambda x, y: fr.to_campus(x, y, np.zeros_like(x), 4326)[:2],
                             interleaved=False)


def clean_polygon(g):
    """Valid polygon(s) only, tiny slivers removed. Returns Polygon, MultiPolygon or None."""
    if g is None or g.is_empty:
        return None
    if not g.is_valid:
        g = shapely.make_valid(g)
    polys = [p for p in _polys(g) if p.area > 0.5]
    if not polys:
        return None
    return polys[0] if len(polys) == 1 else MultiPolygon(polys)


def _polys(g):
    if g.geom_type == "Polygon":
        return [g]
    if hasattr(g, "geoms"):
        out = []
        for h in g.geoms:
            out += _polys(h)
        return out
    return []


def polys_of(g):
    """List of Polygons inside any geometry."""
    return [] if g is None else _polys(g)


def geom_to_json(g, nd=3):
    """Polygon/MultiPolygon -> list of polygons, each [exterior, hole1, ...] as [[x, y], ...]."""
    out = []
    for p in polys_of(g):
        rings = [p.exterior] + list(p.interiors)
        out.append([[[round(x, nd), round(y, nd)] for x, y in r.coords[:-1]] for r in rings])
    return out


def geom_from_json(data):
    polys = [Polygon(rings[0], rings[1:]) for rings in data]
    g = polys[0] if len(polys) == 1 else MultiPolygon(polys)
    if not g.is_valid:
        g = clean_polygon(g) or g        # rounding to 1 mm can make a sliver self-touch
    return g


# ---- OSM and Metro footprints --------------------------------------------

def _way_ll(way, nodes):
    return [(nodes[n]["lon"], nodes[n]["lat"]) for n in way.get("nodes", []) if n in nodes]


def _relation_polygon(rel, nodes, ways):
    """Multipolygon relation -> polygon with holes (outer minus inner), lon/lat."""
    outer, inner = [], []
    for m in rel.get("members", []):
        if m["type"] != "way" or m["ref"] not in ways:
            continue
        c = _way_ll(ways[m["ref"]], nodes)
        if len(c) >= 2:
            (inner if m.get("role") == "inner" else outer).append(LineString(c))
    if not outer:
        return None
    o = unary_union(list(polygonize(unary_union(outer))))
    if inner:
        i = unary_union(list(polygonize(unary_union(inner))))
        o = o.difference(i)
    return o


def load_osm_buildings(root=None):
    """OSM buildings in campus meters.

    Returns (buildings, points): buildings = list of dicts {id, osm, name, tags, geom};
    points = OSM buildings mapped only as a point (used to name Metro-only buildings).
    Ways that are an outer member of a building relation are dropped (the relation wins).
    building=no and underground buildings (layer < 0) are left out and listed in 'skipped'.
    """
    root = Path(root or data_root())
    data = load_json(root / OSM_SNAPSHOT)
    nodes, ways, rels = {}, {}, {}
    for el in data["elements"]:
        {"node": nodes, "way": ways, "relation": rels}[el["type"]][el["id"]] = el
    in_relation = set()
    for r in rels.values():
        if "building" in r.get("tags", {}):
            for m in r.get("members", []):
                if m["type"] == "way":
                    in_relation.add(m["ref"])
    out, points, skipped = [], [], []
    for el in data["elements"]:
        t = el.get("tags", {})
        if "building" not in t:
            continue
        osm = f"{el['type']}/{el['id']}"
        if t["building"] == "no":
            continue
        try:
            layer = int(t.get("layer", "0"))
        except ValueError:
            layer = 0
        if el["type"] == "node":
            X, Y, _ = fr.to_campus(el["lon"], el["lat"], 0.0, 4326)
            points.append({"osm": osm, "name": t.get("name"), "xy": (float(X), float(Y)), "tags": t})
            continue
        if el["type"] == "way":
            if el["id"] in in_relation and not t.get("name"):
                continue
            c = _way_ll(el, nodes)
            if len(c) < 4 or c[0] != c[-1]:
                continue
            g = Polygon(c)
            bid = f"B_{el['id']}"
        else:
            g = _relation_polygon(el, nodes, ways)
            bid = f"B_r{el['id']}"
        g = clean_polygon(to_campus_geom(g)) if g is not None else None
        if g is None:
            continue
        rec = {"id": bid, "osm": osm, "name": t.get("name"), "tags": t, "geom": g}
        if layer < 0:
            skipped.append({"id": bid, "osm": osm, "name": t.get("name"), "reason": "underground (layer < 0)"})
            continue
        out.append(rec)
    # A way that is also a relation member but carries its own name stays; drop exact duplicates.
    return out, points, skipped


def load_metro(root=None):
    """Metro Nashville footprints (EPSG:4326 GeoJSON) in campus meters."""
    root = Path(root or data_root())
    out = []
    for page in sorted((root / METRO_DIR).glob("footprints_page*.geojson")):
        gj = load_json(page)
        crs = gj.get("crs", {}).get("properties", {}).get("name", "EPSG:4326")
        if "4326" not in crs and "CRS84" not in crs:
            fail(f"{page}: unexpected CRS {crs}")
        for f in gj["features"]:
            p = f["properties"]
            g = clean_polygon(to_campus_geom(shape(f["geometry"])))
            if g is None:
                continue
            h = p.get("Height")
            out.append({"metro_id": int(p["OBJECTID"]), "bldg_id": p.get("BldgID"),
                        "height_m": float(h) * FT if h not in (None, 0) else None,
                        "roof_type": p.get("RoofType"), "building_type": p.get("BuildingType"),
                        "geom": g})
    return out


def boundary():
    """{'campus': Polygon, 'backdrop_ring': Polygon} in campus meters."""
    return fr.boundary_polygons()


# ---- rasters (sidecar JSON + GeoTIFF in the campus frame) -------------------

class Grid:
    """A north-up raster in the campus frame with its JSON sidecar."""

    def __init__(self, tif, band=1, window=None):
        import rasterio
        self.meta = load_json(Path(tif).with_suffix(".json"))
        self.cell = self.meta["cell_m"]
        self.x0, self.y0 = self.meta["origin_x"], self.meta["origin_y"]
        with rasterio.open(tif) as r:
            self.data = r.read(band)
        self.h, self.w = self.data.shape

    def sample(self, x, y):
        """Nearest-cell value (NaN outside)."""
        x, y = np.asarray(x, float), np.asarray(y, float)
        c = np.floor((x - self.x0) / self.cell).astype(np.int64)
        r = np.floor((self.y0 - y) / self.cell).astype(np.int64)
        ok = (c >= 0) & (c < self.w) & (r >= 0) & (r < self.h)
        out = np.full(x.shape, np.nan)
        out[ok] = self.data[r[ok], c[ok]]
        return out


@lru_cache(maxsize=1)
def dtm(root=None):
    """The 0.5 m bare-earth raster from make_rasters.py (one copy per process)."""
    return Grid(Path(root or data_root()) / "derived" / "rasters" / "v1" / "dtm.tif")


def ortho_window(name, xmin, ymin, xmax, ymax, root=None):
    """RGB pixels of an orthophoto (derived/ortho/v1/<name>.tif) over a campus box.

    Returns (rgb uint8 (h, w, 3), (x0, y0top, cell)) or (None, None) outside."""
    import rasterio
    from rasterio.windows import Window
    tif = Path(root or data_root()) / "derived" / "ortho" / "v1" / f"{name}.tif"
    meta = load_json(tif.with_suffix(".json"))
    cell = meta["cell_m"]
    c0 = int(math.floor((xmin - meta["origin_x"]) / cell))
    c1 = int(math.ceil((xmax - meta["origin_x"]) / cell))
    r0 = int(math.floor((meta["origin_y"] - ymax) / cell))
    r1 = int(math.ceil((meta["origin_y"] - ymin) / cell))
    c0, r0 = max(c0, 0), max(r0, 0)
    c1, r1 = min(c1, meta["width"]), min(r1, meta["height"])
    if c1 <= c0 or r1 <= r0:
        return None, None
    with rasterio.open(tif) as r:
        rgb = r.read(window=Window(c0, r0, c1 - c0, r1 - r0))
    return np.moveaxis(rgb, 0, -1), (meta["origin_x"] + c0 * cell, meta["origin_y"] - r0 * cell, cell)


# ---- polygon helpers -------------------------------------------------------

def ring_xy(ring):
    """Ring coordinates without the closing repeat, as an (n, 2) array."""
    return np.asarray(ring.coords)[:-1, :2]


def corners(g):
    """All footprint corners (exterior and hole rings) as an (n, 2) array."""
    pts = []
    for p in polys_of(g):
        pts.append(ring_xy(p.exterior))
        for r in p.interiors:
            pts.append(ring_xy(r))
    return np.vstack(pts) if pts else np.zeros((0, 2))


def dominant_angle(g):
    """Main wall direction (radians in [0, pi/2)), length-weighted over all edges.

    A 1-degree histogram (mod 90) finds the peak; the exact angle is the length-weighted
    circular mean of the edges within 7 degrees of it."""
    ang, lens = [], []
    for p in polys_of(g):
        for r in [p.exterior] + list(p.interiors):
            xy = np.asarray(r.coords)[:, :2]
            d = np.diff(xy, axis=0)
            ang.append(np.degrees(np.arctan2(d[:, 1], d[:, 0])) % 90)
            lens.append(np.hypot(d[:, 0], d[:, 1]))
    if not ang:
        return 0.0
    a, L = np.concatenate(ang), np.concatenate(lens)
    hist = np.zeros(90)
    np.add.at(hist, np.floor(a).astype(int) % 90, L)
    k = np.array([1, 2, 3, 2, 1], float)
    sm = sum(k[i] * np.roll(hist, i - 2) for i in range(5))
    peak = np.argmax(sm) + 0.5
    diff = (a - peak + 45) % 90 - 45
    near = np.abs(diff) <= 7
    if not near.any():
        return math.radians(peak)
    z = np.sum(L[near] * np.exp(1j * np.radians(a[near]) * 4))
    return (np.angle(z) / 4) % (math.pi / 2)


def simplify_footprint(g, tol=0.2):
    """Remove footprint vertices that move the outline by <= tol (collinear points, tiny jogs).

    Returns (simplified, removed) where removed lists (x, y, deviation) of dropped vertices."""
    s = clean_polygon(g.simplify(tol, preserve_topology=True))
    if s is None:
        return g, []
    keep = corners(s)
    removed = []
    bnd = s.boundary
    for x, y in corners(g):
        if keep.size and np.min(np.hypot(keep[:, 0] - x, keep[:, 1] - y)) < 1e-6:
            continue
        removed.append((float(x), float(y), float(bnd.distance(Point(x, y)))))
    return s, removed


def orient_ccw(p):
    return shapely.geometry.polygon.orient(p, sign=1.0)


def offset_ring_edges(xy, offsets, min_angle_deg=12.0):
    """Move each edge of a closed ring (CCW, no repeat) outward by offsets[i] meters.

    Edge i goes from xy[i] to xy[i+1]. Each new corner is the intersection of the two
    moved neighbor edges, so directions (and right angles) are kept. Where two neighbor
    edges are nearly parallel, the corner splits into two points joined by a short step.
    """
    n = len(xy)
    P = np.asarray(xy, float)
    D = np.roll(P, -1, axis=0) - P
    L = np.hypot(D[:, 0], D[:, 1])
    D = D / np.maximum(L, 1e-12)[:, None]
    N = np.stack([D[:, 1], -D[:, 0]], axis=1)        # outward normal for a CCW ring
    A = P + N * np.asarray(offsets)[:, None]          # a point on each moved edge
    out = []
    for i in range(n):
        j = (i - 1) % n                               # edge before vertex i
        cross = D[j, 0] * D[i, 1] - D[j, 1] * D[i, 0]
        if abs(cross) < math.sin(math.radians(min_angle_deg)):
            out.append(P[i] + N[j] * offsets[j])
            if abs(offsets[i] - offsets[j]) > 1e-6:
                out.append(P[i] + N[i] * offsets[i])
            continue
        # solve A[j] + t D[j] = A[i] + s D[i]
        rhs = A[i] - A[j]
        t = (rhs[0] * D[i, 1] - rhs[1] * D[i, 0]) / cross
        out.append(A[j] + t * D[j])
    return np.asarray(out)


# ---- the LiDAR outline -----------------------------------------------------

class LidarEdge:
    """Building outline as the LiDAR sees it, on a 0.25 m grid around one building.

    mask  = cells holding building points (class 6), closed by one cell and with holes
            under 4 m2 filled
    sdf   = signed distance to the mask edge in meters (negative inside the LiDAR building)
    veg   = cells with vegetation more than 2 m above the DTM (trees can hide roof edges)
    """

    def __init__(self, pts, box, cell=0.25, hag=None):
        from scipy import ndimage
        xmin, ymin, xmax, ymax = box
        self.cell = cell
        self.x0, self.y0 = xmin, ymin
        self.w = int(math.ceil((xmax - xmin) / cell))
        self.h = int(math.ceil((ymax - ymin) / cell))
        x, y, z, c = pts["x"], pts["y"], pts["z"], pts["classification"]
        if hag is None:
            hag = z - dtm().sample(x, y)
        hag = np.nan_to_num(hag, nan=0.0)
        col = np.floor((x - xmin) / cell).astype(np.int64)
        row = np.floor((y - ymin) / cell).astype(np.int64)
        ok = (col >= 0) & (col < self.w) & (row >= 0) & (row < self.h)
        # Only class 6: class 1 points 2 m up are mostly unclassified tree crowns at the edges.
        roof = ok & (c == 6)
        veg = ok & np.isin(c, VEG) & (hag > 2.0)
        m = np.zeros((self.h, self.w), bool)
        m[row[roof], col[roof]] = True
        st = np.ones((3, 3), bool)
        m = ndimage.binary_erosion(ndimage.binary_dilation(m, st), st, border_value=0)
        # fill holes smaller than 4 m2 (courtyards stay open)
        holes = ndimage.binary_fill_holes(m) & ~m
        lab, n = ndimage.label(holes)
        if n:
            sizes = ndimage.sum(holes, lab, np.arange(1, n + 1))
            small = np.isin(lab, np.nonzero(sizes * cell * cell < 4.0)[0] + 1)
            m |= small
        lab, n = ndimage.label(m)
        if n:
            sizes = ndimage.sum(m, lab, np.arange(1, n + 1))
            m &= np.isin(lab, np.nonzero(sizes * cell * cell >= 4.0)[0] + 1)
        self.mask = m
        v = np.zeros((self.h, self.w), bool)
        v[row[veg], col[veg]] = True
        self.veg = ndimage.binary_dilation(v, st, iterations=2)
        din = ndimage.distance_transform_edt(m)
        dout = ndimage.distance_transform_edt(~m)
        self.sdf = (np.where(m, -(din - 0.5), dout - 0.5)) * cell

    def _rc(self, xy):
        xy = np.asarray(xy, float).reshape(-1, 2)
        return (xy[:, 1] - self.y0) / self.cell - 0.5, (xy[:, 0] - self.x0) / self.cell - 0.5

    def signed(self, xy):
        """Signed distance (m) to the LiDAR outline at points; + = outside the LiDAR building."""
        from scipy import ndimage
        r, c = self._rc(xy)
        return ndimage.map_coordinates(self.sdf, [r, c], order=1, mode="nearest")

    def is_veg(self, xy):
        r, c = self._rc(xy)
        r = np.clip(np.round(r).astype(int), 0, self.h - 1)
        c = np.clip(np.round(c).astype(int), 0, self.w - 1)
        return self.veg[r, c]

    def coverage(self, g):
        """Share of the footprint covered by the LiDAR roof mask."""
        import rasterio.features
        from rasterio.transform import Affine
        tr = Affine(self.cell, 0, self.x0, 0, self.cell, self.y0)
        inside = rasterio.features.rasterize([g], out_shape=(self.h, self.w), transform=tr).astype(bool)
        n = inside.sum()
        return float((inside & self.mask).sum()) / max(int(n), 1)


def edge_samples(g, step=0.25, end_gap=0.4):
    """Sample points along each footprint edge, skipping end_gap meters at the corners.

    Returns a list of dicts per edge: ring index, edge index, a, b (end points), length,
    outward normal, samples (k, 2)."""
    out = []
    for pi, p in enumerate(polys_of(g)):
        p = orient_ccw(p)
        for ri, r in enumerate([p.exterior] + list(p.interiors)):
            xy = ring_xy(r)
            for i in range(len(xy)):
                a, b = xy[i], xy[(i + 1) % len(xy)]
                d = b - a
                L = float(np.hypot(*d))
                if L < 1e-6:
                    continue
                u = d / L
                n = np.array([u[1], -u[0]])          # outward for exterior (CCW) and holes (CW)
                if L > 2 * end_gap + 1e-6:
                    t = np.arange(end_gap, L - end_gap + 1e-9, step)
                else:
                    t = np.array([L / 2])
                out.append({"poly": pi, "ring": ri, "edge": i, "a": a, "b": b, "length": L,
                            "normal": n, "samples": a + np.outer(t, u)})
    return out
