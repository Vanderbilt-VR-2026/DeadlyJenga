"""Write config/unity_map.json: spawns, teams, hill, test route, climb anchors, viewpoints.

    python pipeline/make_unity_map.py              # needs terrain, buildings and trees
    python pipeline/make_unity_map.py --preview    # allow stand-ins (Stage 0 DTM, OSM footprints/trees)

The Unity builder reads this file (schema: Assets/_DeadlyJenga/World/Maps/
VanderbiltCampus/VanderbiltCampus.md). Every position is written in UNITY axes,
map-root local: [x east, y up, z north] = campus (X, Z, Y), meters.

Inputs (archive; VC_DATA_ROOT overrides the root):
  derived/terrain/v1/<tile>/heights.npy + heights.json   ground heights (0.5 m grid)
  derived/terrain/v1/<tile>/terrain.npz, terrain_col.npz  surface material, lowest ground
  derived/buildings/v1/footprints.json                    building footprints (campus meters)
  buildings/<id>/v1/lod1.npz, lod2.npz, model.json        heights and names
  derived/trees/v1/trees.json                             tree trunks
  raw/osm/<date>/campus_overpass.json                     paths (route), named lawns (spawns)
  config/boundary.geojson, config/hero_spots.json
With --preview, missing terrain heights fall back to derived/rasters/v1/dtm.tif,
missing footprints to OSM buildings and missing trees to OSM tree nodes; the
file is then marked "preview": true. Without --preview missing inputs fail.

Also writes reports/stage1/unity_map_report.json (route length, steps, what was rejected).
"""
import argparse
import math
import re
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CONFIG, data_root, fail, load_json, save_json  # noqa: E402

EYE = 1.7
SPAWN_LIFT = 0.1
MAX_SLOPE_DEG = 6.0
CLEAR_BUILDING = 4.0       # spawn distance to any footprint / trunk
CLEAR_TREE = 4.0
WALK_MATERIALS = {16, 17, 20}          # paving_brick, paving_concrete, grass
PATH_TYPES = {"footway", "path", "pedestrian", "steps", "crossing", "service", "residential"}
PATH_COST = {"service": 1.3, "residential": 1.3, "steps": 1.2}

# Open areas for spawns, in the order they are used (OSM names; inside the campus only).
SPAWN_SITES = [
    ("Alumni Lawn", "AlumniLawn", 2), ("Library Lawn", "LibraryLawn", 2), ("Peabody Esplanade", "PeabodyEsplanade", 2),
    ("Commons Center Lawn", "CommonsLawn", 1), ("Magnolia Lawn", "MagnoliaLawn", 1), ("The Lawn", "TheLawn", 1),
    ("The Quad", "TheQuad", 1), ("Curry Field", "CurryField", 1), ("SLC Lawn", "SLCLawn", 1),
    ("South Patio Lawn", "SouthPatioLawn", 1), ("Chapman Quad", "ChapmanQuad", 1), ("Sunken Garden", "SunkenGarden", 1),
    ("Flora Wilson Community Plaza", "FloraWilsonPlaza", 1), ("The Martha Rivers Ingram Commons", "IngramCommons", 1),
]
KIRKLAND = "B_544397715"
BODY_CLEAR = 0.6           # survivor radius + margin around building colliders (route)
TRUNK_CLEAR = 0.5          # around trunk colliders (route)
BUILT_CLEAR = 2.0          # spawns, flags, hill, viewpoints stay this far outside building colliders
MAX_STEP = 0.35            # height step between route samples that is walked without a warning
JUMP = 1.0                 # the survivor can jump about this much; more is an error
NAMES = {}                 # building id -> name (from footprints.json)
ZONES = {}                 # building id -> campus / ring (from footprints.json)


def to_unity(x, y, z):
    return [round(float(x), 2), round(float(z), 2), round(float(y), 2)]


# ---- ground heights -----------------------------------------------------------

class Grid:
    """A height raster. Two layouts are understood:
      Stage 0 rasters: origin = top-left corner, values at cell centers, row 0 = north;
      terrain heights.npy: values at grid nodes X = origin_x + I * cell, Y = origin_y + J * cell,
      row 0 = south (its sidecar "layout" says so)."""

    def __init__(self, a, side):
        self.a = np.asarray(a, np.float64)
        self.x0, self.y0, self.cell = float(side["origin_x"]), float(side["origin_y"]), float(side["cell_m"])
        text = (str(side.get("layout", "")) + " " + str(side.get("row0", ""))).lower()
        self.south_up = "south" in text
        self.nodes = "node" in text
        self.flip = False

    def sample(self, x, y):
        """Bilinear height at campus points (nan outside or on missing cells)."""
        x, y = np.atleast_1d(np.asarray(x, float)), np.atleast_1d(np.asarray(y, float))
        h, w = self.a.shape
        half = 0.0 if self.nodes else 0.5
        fc = (x - self.x0) / self.cell - half
        if self.south_up != self.flip:
            fr = (y - self.y0) / self.cell - half
        else:
            fr = (self.y0 - y) / self.cell - half
        c0 = np.clip(np.floor(fc).astype(int), -1, w)
        r0 = np.clip(np.floor(fr).astype(int), -1, h)
        tx, ty = fc - c0, fr - r0
        # Points exactly on the last node row/column use the cell before it.
        edge_c, edge_r = (c0 == w - 1) & (tx < 1e-9), (r0 == h - 1) & (ty < 1e-9)
        c0, tx = np.where(edge_c, w - 2, c0), np.where(edge_c, 1.0, tx)
        r0, ty = np.where(edge_r, h - 2, r0), np.where(edge_r, 1.0, ty)
        out = np.full(x.shape, np.nan)
        ok = (c0 >= 0) & (r0 >= 0) & (c0 + 1 < w) & (r0 + 1 < h)
        if ok.any():
            c, r, a = c0[ok], r0[ok], self.a
            v = (a[r, c] * (1 - tx[ok]) * (1 - ty[ok]) + a[r, c + 1] * tx[ok] * (1 - ty[ok]) +
                 a[r + 1, c] * (1 - tx[ok]) * ty[ok] + a[r + 1, c + 1] * tx[ok] * ty[ok])
            out[ok] = v
        return out


class Ground:
    """Per-tile terrain heights (heights.npy) with the Stage 0 DTM as preview fallback."""

    def __init__(self, root, preview):
        self.tiles, self.notes = {}, []
        self.dtm = None
        dtm = root / "derived" / "rasters" / "v1" / "dtm.tif"
        if dtm.exists():
            import rasterio
            with rasterio.open(dtm) as src:
                self.dtm = Grid(src.read(1), load_json(dtm.with_suffix(".json")))
        for d in sorted((root / "derived" / "terrain" / "v1").glob("T_*")):
            h = d / "heights.npy"
            side = next((p for p in (d / "heights.json", d / "heights.npy.json", d / "tile.json") if p.exists()), None)
            if not h.exists() or side is None:
                continue
            try:
                s = load_json(side)
                s = s.get("heights", s) if isinstance(s.get("heights"), dict) else s
                g = Grid(np.load(h), s)
            except Exception as e:
                self.notes.append(f"{d.name}: heights not readable ({e})")
                continue
            if self.dtm is not None and not self._agrees(g, d.name):
                g.flip = not g.flip
                if not self._agrees(g, d.name):
                    self.notes.append(f"{d.name}: heights.npy disagrees with the DTM by > 1 m; DTM used")
                    continue
            self.tiles[d.name] = g
        if not self.tiles:
            if not preview or self.dtm is None:
                fail("no derived/terrain/v1/<tile>/heights.npy (+ heights.json); run the terrain step "
                     "(or use --preview to fall back to the Stage 0 DTM)")
            self.notes.append("preview: all heights from the Stage 0 DTM")
        self.preview_dtm = preview

    def _agrees(self, g, tile):
        from export_all import tile_bounds
        x0, y0, x1, y1 = tile_bounds(tile)
        xs, ys = np.meshgrid(np.linspace(x0 + 5, x1 - 5, 7), np.linspace(y0 + 5, y1 - 5, 7))
        a, b = g.sample(xs.ravel(), ys.ravel()), self.dtm.sample(xs.ravel(), ys.ravel())
        ok = np.isfinite(a) & np.isfinite(b)
        return ok.sum() >= 10 and np.median(np.abs(a[ok] - b[ok])) < 1.0

    def __call__(self, x, y):
        from export_all import tile_of
        x, y = np.atleast_1d(np.asarray(x, float)), np.atleast_1d(np.asarray(y, float))
        out = np.full(x.shape, np.nan)
        names = np.array([tile_of(a, b) for a, b in zip(x, y)])
        for t in np.unique(names):
            m = names == t
            if t in self.tiles:
                out[m] = self.tiles[t].sample(x[m], y[m])
        miss = ~np.isfinite(out)
        if miss.any() and self.dtm is not None and (self.preview_dtm or not self.tiles):
            out[miss] = self.dtm.sample(x[miss], y[miss])
        return out

    def slope_deg(self, x, y, d=1.0):
        hx = (self(x + d, y) - self(x - d, y)) / (2 * d)
        hy = (self(x, y + d) - self(x, y - d)) / (2 * d)
        return np.degrees(np.arctan(np.hypot(hx, hy)))


class Surface:
    """Terrain material at a point: the face_mat of the nearest terrain.npz triangle."""

    def __init__(self, root):
        self.root, self.cache = root, {}

    def __call__(self, x, y):
        from export_all import tile_of
        t = tile_of(x, y)
        if t not in self.cache:
            p = self.root / "derived" / "terrain" / "v1" / t / "terrain.npz"
            if not p.exists():
                self.cache[t] = None
            else:
                from scipy.spatial import cKDTree
                with np.load(p) as z:
                    v, f, fm = z["verts"], z["faces"], z["face_mat"]
                self.cache[t] = (cKDTree(v[f][:, :, :2].mean(1)), fm)
        if self.cache[t] is None:
            return None
        tree, fm = self.cache[t]
        return int(fm[tree.query([x, y])[1]])


# ---- footprints, buildings, trees ----------------------------------------------

def _poly_from(coords):
    """Polygon or MultiPolygon from GeoJSON geometry or nested coordinate lists."""
    from shapely.geometry import MultiPolygon, Polygon, shape
    if isinstance(coords, dict):
        return shape(coords)
    depth, c = 0, coords
    while isinstance(c, (list, tuple)) and c and isinstance(c[0], (list, tuple)):
        depth, c = depth + 1, c[0]
    if depth == 1:                       # [[x, y], ...]
        return Polygon(coords)
    if depth == 2:                       # [ring, hole, ...]
        return Polygon(coords[0], coords[1:])
    return MultiPolygon([Polygon(p[0], p[1:]) for p in coords])   # [[ring, ...], ...]


def _looks_lonlat(polys):
    import shapely
    b = shapely.total_bounds(polys)
    return -88 < b[0] and b[2] < -85 and 35 < b[1] and b[3] < 37.5


def load_footprints(root, preview, notes):
    """{building id: shapely Polygon in campus meters}."""
    import shapely
    path = root / "derived" / "buildings" / "v1" / "footprints.json"
    out = {}
    if path.exists():
        data = load_json(path)
        if isinstance(data, dict) and "features" in data:
            for ft in data["features"]:
                pr = ft.get("properties", {})
                bid = pr.get("id") or pr.get("building") or pr.get("building_id") or ft.get("id")
                out[str(bid)] = _poly_from(ft["geometry"])
        else:
            items = data.get("buildings", data) if isinstance(data, dict) else data
            if isinstance(items, dict):
                items = [dict(v, id=k) if isinstance(v, dict) else {"id": k, "footprint": v} for k, v in items.items()]
            for it in items:
                bid = it.get("id") or it.get("building") or it.get("building_id")
                geom = next((it[k] for k in ("footprint", "polygon", "outline", "geometry", "coords", "exterior")
                             if k in it), None)
                if bid and geom is not None:
                    out[str(bid)] = _poly_from(geom)
                    NAMES[str(bid)] = it.get("name")
                    ZONES[str(bid)] = it.get("zone")
        if out and _looks_lonlat(list(out.values())):
            import frame
            out = {k: shapely.transform(v, lambda x, y: frame.to_campus(x, y, np.zeros_like(x), 4326)[:2],
                                        interleaved=False) for k, v in out.items()}
        out = {(k if k.startswith("B_") else "B_" + k): v.buffer(0) for k, v in out.items() if not v.is_empty}
        if not out:
            fail(f"{path} has no footprints I can read")
        return out
    if not preview:
        fail(f"missing {path}; run the buildings step (or --preview to use OSM footprints)")
    notes.append("preview: footprints from OSM buildings")
    for bid, poly in osm_buildings(root).items():
        out[bid] = poly
    return out


def osm_data(root):
    import make_boundary as mb
    snap = mb.newest_snapshot(root)
    data = load_json(snap)
    return data, mb.index(data["elements"])


def osm_buildings(root):
    import frame
    import make_boundary as mb
    import shapely
    data, (nodes, ways, _rels) = osm_data(root)
    out = {}
    for el in data["elements"]:
        if el["type"] in ("way", "relation") and "building" in el.get("tags", {}):
            poly = mb.area_of(el, nodes, ways)
            if poly is None or poly.is_empty:
                continue
            poly = shapely.transform(poly, lambda x, y: frame.to_campus(x, y, np.zeros_like(x), 4326)[:2],
                                     interleaved=False)
            out[("B_r" if el["type"] == "relation" else "B_") + str(el["id"])] = poly
    return out


def building_info(root, bid):
    """Name and top height (campus Z) of an archived building, from model.json and lod1/lod2."""
    folder = root / "buildings" / bid / "v1"
    name, top = bid, None
    if (folder / "model.json").exists():
        name = load_json(folder / "model.json").get("name") or bid
    elif bid in NAMES:
        name = NAMES[bid] or bid
    for f in ("lod2.npz", "lod1.npz"):
        if (folder / f).exists():
            with np.load(folder / f) as z:
                top = float(z["verts"][:, 2].max())
            break
    return name, top


def load_trees(root, preview, notes):
    """(N, 3) array of trunk x, y, radius."""
    path = root / "derived" / "trees" / "v1" / "trees.json"
    rows = []
    if path.exists():
        data = load_json(path)
        items = data.get("trees", data) if isinstance(data, dict) else data
        for t in items:
            if "x" in t and "y" in t:
                x, y = t["x"], t["y"]
            else:
                p = t.get("pos") or t.get("position") or t.get("xy") or t.get("base")
                if not p:
                    continue
                x, y = p[0], p[1]
            r = t.get("trunk_r") or t.get("trunk_radius") or (t.get("trunk_d") or t.get("dbh") or 0.6) / 2
            rows.append((float(x), float(y), float(r)))
        return np.array(rows).reshape(-1, 3)
    if not preview:
        fail(f"missing {path}; run the trees step (or --preview to use OSM tree nodes)")
    import frame
    data, _ = osm_data(root)
    pts = [(e["lon"], e["lat"]) for e in data["elements"]
           if e["type"] == "node" and e.get("tags", {}).get("natural") == "tree"]
    notes.append(f"preview: {len(pts)} trees from OSM nodes")
    if not pts:
        return np.zeros((0, 3))
    lon, lat = np.array(pts).T
    X, Y, _ = frame.to_campus(lon, lat, np.zeros_like(lon), 4326)
    return np.column_stack([X, Y, np.full(len(X), 0.3)])


# ---- what the survivor collides with (the exported colliders) ----------------------

def _hull2d(verts):
    from shapely.geometry import MultiPoint
    return MultiPoint(np.asarray(verts)[:, :2]).convex_hull


def _mesh_outline(path):
    """2D outline (union of the triangles seen from above) of a VC mesh."""
    import shapely
    with np.load(path) as z:
        tri = z["verts"][z["faces"]][:, :, :2]
    polys = shapely.polygons(tri)
    return shapely.union_all(polys[shapely.area(polys) > 1e-6])


def built_obstacles(root, notes):
    """{building id: 2D outline} of every exported building, from the same records the exporter
    uses (export_all.building_records: the v2 index when present, else v1 folders).
    Outline: the v2 index footprint if given; else col_mesh.npz seen from above; else the union of
    the col_*.npz convex hulls (Unity makes convex MeshColliders from them); else lod1/lod2."""
    import shapely
    from export_all import building_records
    out, bad, src = {}, 0, {}
    for r in building_records(root):
        folder, bid = Path(r["folder"]), r["id"]
        try:
            if r.get("footprint"):
                geom, how = _poly_from(r["footprint"]), "index footprint"
            elif (folder / "col_mesh.npz").exists():
                geom, how = _mesh_outline(folder / "col_mesh.npz"), "col_mesh"
            elif list(folder.glob("col_*.npz")):
                parts = []
                for c in sorted(folder.glob("col_*.npz")):
                    with np.load(c) as z:
                        parts.append(_hull2d(z["verts"]))
                geom, how = shapely.union_all(parts), "col hulls"
            else:
                f = next((folder / n for n in ("lod1.npz", "lod2.npz") if (folder / n).exists()), None)
                if f is None:
                    continue
                geom, how = _mesh_outline(f), "lod outline"
        except Exception:          # a building being rewritten right now: skip it, say so
            bad += 1
            continue
        if geom is not None and not geom.is_empty:
            out[bid] = geom.buffer(0)
            src[how] = src.get(how, 0) + 1
    if bad:
        notes.append(f"{bad} buildings could not be read (being rewritten?) and were ignored")
    notes.append(f"{len(out)} exported buildings used as obstacles: {src}")
    return out


def trunk_colliders(root):
    """2D outlines of the trunk colliders (trees_col.npz): one convex hull per connected piece."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    out = []
    for p in sorted((root / "derived" / "trees" / "v1").glob("T_*/trees_col.npz")):
        try:
            with np.load(p) as z:
                v, f = z["verts"], z["faces"]
        except Exception:
            continue
        if not len(f):
            continue
        e = np.vstack([f[:, [0, 1]], f[:, [1, 2]]])
        g = coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(len(v), len(v)))
        n, lab = connected_components(g, directed=False)
        for k in range(n):
            pts = v[lab == k]
            if len(pts) >= 3:
                out.append(_hull2d(pts))
    return out


class WalkSurface:
    """Height of the terrain collider (terrain_col.npz) at points: the surface the survivor walks on,
    including curbs and walls that heights.npy smooths away. Falls back to Ground."""

    def __init__(self, root, ground):
        self.root, self.ground, self.cache = root, ground, {}

    def _tile(self, t):
        if t not in self.cache:
            import shapely
            from shapely.strtree import STRtree
            d = self.root / "derived" / "terrain" / "v1" / t
            p = d / "terrain_col.npz" if (d / "terrain_col.npz").exists() else d / "terrain.npz"
            self.cache[t] = None
            if p.exists():
                with np.load(p) as z:
                    T = z["verts"][z["faces"]]
                polys = shapely.polygons(T[:, :, :2])
                keep = shapely.area(polys) > 1e-8           # vertical faces have no area seen from above
                self.cache[t] = (STRtree(polys[keep]), T[keep])
        return self.cache[t]

    def __call__(self, x, y):
        import shapely
        from export_all import tile_of
        x, y = np.atleast_1d(np.asarray(x, float)), np.atleast_1d(np.asarray(y, float))
        out = np.full(x.shape, np.nan)
        names = np.array([tile_of(a, b) for a, b in zip(x, y)])
        for t in np.unique(names):
            m = np.nonzero(names == t)[0]
            data = self._tile(t)
            if data is None:
                out[m] = self.ground(x[m], y[m])
                continue
            tree, T = data
            pi, ti = tree.query(shapely.points(x[m], y[m]), predicate="intersects")
            a, b, c = T[ti, 0], T[ti, 1], T[ti, 2]
            px, py = x[m][pi], y[m][pi]
            det = (b[:, 0] - a[:, 0]) * (c[:, 1] - a[:, 1]) - (c[:, 0] - a[:, 0]) * (b[:, 1] - a[:, 1])
            u = ((px - a[:, 0]) * (c[:, 1] - a[:, 1]) - (c[:, 0] - a[:, 0]) * (py - a[:, 1])) / det
            w = ((b[:, 0] - a[:, 0]) * (py - a[:, 1]) - (px - a[:, 0]) * (b[:, 1] - a[:, 1])) / det
            z = a[:, 2] + u * (b[:, 2] - a[:, 2]) + w * (c[:, 2] - a[:, 2])
            res = np.full(len(m), -np.inf)
            np.maximum.at(res, pi, z)                       # top surface where triangles overlap
            res[np.isinf(res)] = np.nan
            out[m] = res
        return out


# ---- the obstacle test shared by spawns, teams and climb anchors ------------------

class World:
    """Everything the placement rules test against. Obstacles = footprints.json outlines plus the
    exported building colliders (built); blocked = colliders + BODY_CLEAR and trunks + TRUNK_CLEAR."""

    def __init__(self, ground, surface, footprints, trees, campus, built=None, trunks=None, walk=None):
        import shapely
        from shapely.strtree import STRtree
        self.ground, self.surface, self.campus = ground, surface, campus
        self.built = built or {}
        self.walk_height = walk or ground
        self.ids = list(footprints) + [f"built:{b}" for b in self.built]
        self.polys = list(footprints.values()) + list(self.built.values())
        parts = [g.buffer(BODY_CLEAR) for g in self.built.values()] + [t.buffer(TRUNK_CLEAR) for t in (trunks or [])]
        self.blocked = shapely.union_all(parts) if parts else None
        if self.blocked is not None:
            shapely.prepare(self.blocked)
        self.built_union = shapely.union_all(list(self.built.values())) if self.built else None
        self.tree = STRtree(self.polys)
        self.trees = trees
        self.union = shapely.union_all(self.polys)
        shapely.prepare(self.union)
        shapely.prepare(campus)
        from scipy.spatial import cKDTree
        self.trunks = cKDTree(trees[:, :2]) if len(trees) else None

    def building_distance(self, x, y):
        import shapely
        _idx, dist = self.tree.query_nearest(shapely.Point(x, y), return_distance=True)
        return float(dist[0]) if len(dist) else 1e9

    def built_distance(self, x, y):
        import shapely
        return float(shapely.distance(self.built_union, shapely.Point(x, y))) if self.built_union is not None else 1e9

    def tree_distance(self, x, y):
        if self.trunks is None:
            return 1e9
        d, i = self.trunks.query([x, y])
        return float(d - self.trees[i, 2])

    def spawn_ok(self, x, y, why=None):
        """True when (x, y) is a safe free spawn (see the module help). why: list collecting reasons."""
        import shapely
        reason = None
        if not self.campus.contains(shapely.Point(x, y)):
            reason = "outside campus"
        elif not np.isfinite(self.ground(x, y)[0]):
            reason = "no ground"
        elif self.building_distance(x, y) < CLEAR_BUILDING or self.built_distance(x, y) < BUILT_CLEAR:
            reason = "near building"
        elif self.tree_distance(x, y) < CLEAR_TREE:
            reason = "near tree"
        elif self.ground.slope_deg(x, y)[0] >= MAX_SLOPE_DEG or self.ground.slope_deg(x, y, 3.0)[0] >= MAX_SLOPE_DEG:
            reason = "slope"
        else:
            m = self.surface(x, y)
            if m is not None and m not in WALK_MATERIALS:
                reason = f"surface material {m}"
        if why is not None and reason:
            why.append(reason)
        return reason is None

    def point(self, x, y, lift=SPAWN_LIFT):
        return to_unity(x, y, self.ground(x, y)[0] + lift)


# ---- spawns, teams, hill ---------------------------------------------------------

def site_geometry(root, name):
    """(shapely geometry, center) of a named OSM area or node in campus meters, or None."""
    import frame
    import make_boundary as mb
    import shapely
    from shapely.geometry import Point
    data, (nodes, ways, _rels) = osm_data(root)
    for el in data["elements"]:
        if el.get("tags", {}).get("name") != name:
            continue
        if el["type"] == "node":
            X, Y, _ = frame.to_campus(el["lon"], el["lat"], 0.0, 4326)
            p = Point(float(X), float(Y))
            return p.buffer(35.0), p
        poly = mb.area_of(el, nodes, ways)
        if poly is not None and not poly.is_empty:
            poly = shapely.transform(poly, lambda x, y: frame.to_campus(x, y, np.zeros_like(x), 4326)[:2],
                                     interleaved=False)
            return poly, poly.representative_point() if not poly.contains(poly.centroid) else poly.centroid
    return None


def candidates(world, geom, center, step=2.0):
    """Valid spawn points inside geom, best first (far from obstacles, near the center)."""
    import shapely
    x0, y0, x1, y1 = geom.bounds
    xs, ys = np.meshgrid(np.arange(x0, x1, step), np.arange(y0, y1, step))
    pts = np.column_stack([xs.ravel(), ys.ravel()])
    if not len(pts):
        return []
    inside = shapely.contains_xy(geom, pts[:, 0], pts[:, 1])
    pts = pts[inside]
    d_center = np.hypot(pts[:, 0] - center.x, pts[:, 1] - center.y)
    pts = pts[np.argsort(d_center)][:1500]
    good = [(x, y) for x, y in pts if world.spawn_ok(x, y)]
    if not good:
        return []
    good = np.array(good)
    clear = np.array([min(world.building_distance(x, y), world.tree_distance(x, y), 12.0) for x, y in good])
    d = np.hypot(good[:, 0] - center.x, good[:, 1] - center.y)
    order = np.argsort(-(clear - 0.15 * d))
    return [tuple(good[i]) for i in order]


def pick_spread(cands, n, min_gap):
    out = []
    for c in cands:
        if all(math.hypot(c[0] - o[0], c[1] - o[1]) >= min_gap for o in out):
            out.append(c)
            if len(out) == n:
                break
    return out


def make_spawns(root, world, report):
    spawns, sites = [], {}
    for osm_name, short, count in SPAWN_SITES:
        found = site_geometry(root, osm_name)
        if not found:
            report["spawn_sites_missing"].append(osm_name)
            continue
        geom, center = found
        cands = candidates(world, geom, center)
        sites[short] = (geom, center, cands)
        if not cands:
            report["spawn_sites_rejected"].append(osm_name)
            continue
        for i, (x, y) in enumerate(pick_spread(cands, count, 20.0)):
            if len(spawns) < 16:
                spawns.append({"name": f"{short}_{i + 1}", "pos": world.point(x, y), "campus": [x, y]})
    if len(spawns) < 12:
        report["problems"].append(f"only {len(spawns)} spawns found (want 12-16)")
    return spawns, sites


def make_team(name, site, world):
    import shapely
    if not site or not site[2]:
        return None
    geom, center, cands = site
    fx, fy = cands[0]
    ring = []
    for r in (8.0, 11.0, 14.0, 18.0):
        for k in range(16):
            a = 2 * math.pi * k / 16
            x, y = fx + r * math.cos(a), fy + r * math.sin(a)
            if world.spawn_ok(x, y):
                ring.append((x, y))
    pts = pick_spread(ring, 6, 4.0)
    if len(pts) < 6:
        pts += pick_spread([c for c in cands[1:] if c not in pts], 6 - len(pts), 4.0)
    return {"name": name, "flag": world.point(fx, fy, 0.0), "spawns": [world.point(x, y) for x, y in pts[:6]]}


# ---- route -----------------------------------------------------------------------
# The route is checked against what the survivor actually collides with: the
# exported building colliders (col_*.npz), buffered by BODY_CLEAR, and the tree
# trunk colliders (trees_col.npz), buffered by TRUNK_CLEAR.

def path_graph(root, allow_covered):
    """OSM pedestrian graph: (networkx Graph, {node: (x, y)}, list of stair lines)."""
    import frame
    import networkx as nx
    from shapely.geometry import LineString
    data, (nodes, _ways, _rels) = osm_data(root)
    G = nx.Graph()
    pos, stairs = {}, []
    for el in data["elements"]:
        tags = el.get("tags", {})
        if el["type"] != "way" or tags.get("highway") not in PATH_TYPES:
            continue
        if tags.get("access") == "private" and tags.get("highway") in ("service", "residential"):
            continue
        covered = (tags.get("tunnel") not in (None, "no") or tags.get("indoor") not in (None, "no")
                   or tags.get("covered") == "yes" or str(tags.get("layer", "0")).startswith("-"))
        if covered and not allow_covered:
            continue
        ids = [n for n in el.get("nodes", []) if n in nodes]
        for n in ids:
            if n not in pos:
                X, Y, _ = frame.to_campus(nodes[n]["lon"], nodes[n]["lat"], 0.0, 4326)
                pos[n] = (float(X), float(Y))
        if tags["highway"] == "steps" and len(ids) > 1:
            stairs.append(LineString([pos[n] for n in ids]))
        cost = PATH_COST.get(tags["highway"], 1.0) * (3.0 if covered else 1.0)
        for a, b in zip(ids, ids[1:]):
            length = math.dist(pos[a], pos[b])
            if length > 1e-6:
                G.add_edge(a, b, weight=length * cost, length=length, highway=tags["highway"])
    return G, pos, stairs


def drop_blocked_edges(G, pos, blocked):
    """Remove every edge whose straight segment touches the blocked area. Returns how many."""
    import shapely
    if blocked is None or blocked.is_empty:
        return 0
    edges = list(G.edges)
    if not edges:
        return 0
    segs = shapely.linestrings([[pos[a], pos[b]] for a, b in edges])
    shapely.prepare(blocked)
    hit = shapely.intersects(blocked, segs)
    G.remove_edges_from([e for e, h in zip(edges, hit) if h])
    return int(hit.sum())


def clear_leg(blocked, p, q):
    from shapely.geometry import LineString
    return blocked is None or not blocked.intersects(LineString([p, q]))


def nearest_reachable(G, pos, p, blocked, limit=80.0):
    """Nearest graph node whose straight leg from p is clear (None if none within limit)."""
    cands = sorted(G.nodes, key=lambda n: (pos[n][0] - p[0]) ** 2 + (pos[n][1] - p[1]) ** 2)
    for n in cands[:200]:
        d = math.dist(pos[n], p)
        if d > limit:
            break
        if clear_leg(blocked, p, pos[n]):
            return n, d
    return None, None


def grid_route(world, blocked, stairs, start, end, cell=1.0, margin=120.0):
    """A* over free 1 m cells (free = outside blocked; neighbor step <= MAX_STEP, or <= JUMP on a
    mapped stair). Returns a list of points or None."""
    import heapq
    import shapely
    x0, x1 = min(start[0], end[0]) - margin, max(start[0], end[0]) + margin
    y0, y1 = min(start[1], end[1]) - margin, max(start[1], end[1]) + margin
    xs, ys = np.arange(x0, x1, cell), np.arange(y0, y1, cell)
    X, Y = np.meshgrid(xs, ys)
    free = ~shapely.contains_xy(blocked, X.ravel(), Y.ravel()).reshape(X.shape) if blocked is not None \
        else np.ones(X.shape, bool)
    H = world.ground(X.ravel(), Y.ravel()).reshape(X.shape)
    free &= np.isfinite(H)
    stair = np.zeros(X.shape, bool)
    if stairs:
        sz = shapely.union_all(stairs).buffer(1.5)
        stair = shapely.contains_xy(sz, X.ravel(), Y.ravel()).reshape(X.shape)
    h, w = X.shape

    def idx(p):
        return int(round((p[1] - y0) / cell)), int(round((p[0] - x0) / cell))
    s, g = idx(start), idx(end)
    if not (free[s] and free[g]):
        return None
    steps = [(-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1)]
    dist = {s: 0.0}
    prev = {}
    heap = [(0.0, s)]
    while heap:
        _f, u = heapq.heappop(heap)
        if u == g:
            break
        du = dist[u]
        for dr, dc in steps:
            v = (u[0] + dr, u[1] + dc)
            if not (0 <= v[0] < h and 0 <= v[1] < w) or not free[v]:
                continue
            limit = JUMP if (stair[u] or stair[v]) else MAX_STEP
            if abs(H[v] - H[u]) > limit:
                continue
            nd = du + cell * (1.4142 if dr and dc else 1.0)
            if nd < dist.get(v, 1e18):
                dist[v], prev[v] = nd, u
                heapq.heappush(heap, (nd + cell * math.hypot(v[0] - g[0], v[1] - g[1]), v))
    if g not in prev:
        return None
    cells, u = [g], g
    while u != s:
        u = prev[u]
        cells.append(u)
    return [start] + [(x0 + c * cell, y0 + r * cell) for r, c in reversed(cells)] + [end]


def simplify_safe(pts, blocked, step=15.0):
    """Drop points that are not needed (tolerance 1 m, then 0.3 m, then none) as long as every leg
    stays clear, then split legs longer than step (a clear straight leg stays clear when split)."""
    from shapely.geometry import LineString
    coords = pts
    for tol in (1.0, 0.3, 0.0):
        c = list(LineString(pts).simplify(tol, preserve_topology=False).coords) if tol else list(pts)
        if all(clear_leg(blocked, p, q) for p, q in zip(c, c[1:])):
            coords = c
            break
    out = [coords[0]]
    for a, b in zip(coords, coords[1:]):
        n = max(1, math.ceil(math.dist(a, b) / step))
        for k in range(1, n + 1):
            out.append((a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n))
    return out


def simulate_walk(world, blocked, wps, sample=0.25):
    """Sample the polyline every 0.25 m: positions inside the blocked area, and height steps on the
    walk surface (terrain collider) between samples. Returns (inside, warnings, errors, worst)."""
    import shapely
    pts = []
    for p, q in zip(wps, wps[1:]):
        n = max(2, int(math.ceil(math.dist(p, q) / sample)) + 1)
        t = np.linspace(0, 1, n)[:-1]
        pts.append(np.column_stack([p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t]))
    pts.append(np.array([wps[-1]]))
    pts = np.vstack(pts)
    inside = []
    if blocked is not None:
        hit = shapely.contains_xy(blocked, pts[:, 0], pts[:, 1])
        inside = [to_unity(x, y, 0) for x, y in pts[hit][:20]]
    h = world.walk_height(pts[:, 0], pts[:, 1])
    dh = np.abs(np.diff(h))
    warnings, errors = [], []
    for j in np.nonzero(np.isnan(h))[0][:20]:
        errors.append({"at_unity": to_unity(pts[j, 0], pts[j, 1], 0), "problem": "no walk surface"})
    for j in np.nonzero(dh > MAX_STEP)[0]:
        item = {"at_unity": to_unity(pts[j, 0], pts[j, 1], h[j]), "step_m": round(float(dh[j]), 2)}
        (errors if dh[j] > JUMP else warnings).append(item)
    return inside, warnings, errors, (float(np.nanmax(dh)) if len(dh) else 0.0)


def make_route(root, world, start, end, report):
    import networkx as nx
    blocked = world.blocked
    path_pts, method, highways = None, None, []
    for allow_covered in (False, True):
        G, pos, stairs = path_graph(root, allow_covered)
        removed = drop_blocked_edges(G, pos, blocked)
        a, da = nearest_reachable(G, pos, start, blocked)
        b, db = nearest_reachable(G, pos, end, blocked)
        if a is None or b is None:
            continue
        try:
            path = nx.shortest_path(G, a, b, weight="weight")
        except nx.NetworkXNoPath:
            continue
        path_pts = [start] + [pos[n] for n in path] + [end]
        highways = sorted({G.edges[u, v]["highway"] for u, v in zip(path, path[1:])})
        method = "osm_graph" + ("_with_covered" if allow_covered else "")
        report["route_notes"].append(f"{removed} OSM path edges removed: they touch a building collider "
                                     f"(+{BODY_CLEAR} m) or a trunk (+{TRUNK_CLEAR} m)")
        break
    if path_pts is None:
        _G, _pos, stairs = path_graph(root, True)
        path_pts = grid_route(world, blocked, stairs, start, end)
        method = "grid_astar_1m"
        da = db = 0.0
    if path_pts is None:
        report["problems"].append("no clear walk from Alumni Lawn to Peabody Esplanade (OSM graph and 1 m grid)")
        return []
    wps = simplify_safe(path_pts, blocked)
    length = sum(math.dist(p, q) for p, q in zip(wps, wps[1:]))
    inside, warnings, errors, worst = simulate_walk(world, blocked, wps)
    report["route"] = {"method": method, "waypoints": len(wps), "length_m": round(length, 1),
                       "start_offset_m": round(da, 1), "end_offset_m": round(db, 1), "highways": highways,
                       "clearance": f"building colliders + {BODY_CLEAR} m, trunks + {TRUNK_CLEAR} m",
                       "samples_inside_clearance": inside, "max_step_m": round(worst, 2),
                       f"steps_{MAX_STEP}_to_{JUMP}m_warnings": warnings, f"steps_over_{JUMP}m_errors": errors}
    if inside:
        report["problems"].append(f"route passes inside building/trunk clearance at {inside[:3]}")
    if errors:
        report["problems"].append(f"route has {len(errors)} steps over {JUMP} m or gaps: {errors[:3]}")
    return [world.point(x, y, 0.0) for x, y in wps]


# ---- climb anchors -------------------------------------------------------------

def rectangularity(poly):
    r = poly.minimum_rotated_rectangle
    return poly.area / r.area if r.area > 0 else 0.0


def wall_anchors(bid, poly, world):
    """Valid anchors on straight walls >= 8 m: (score, x, y, nx, ny) with n pointing out of the wall."""
    import shapely
    from shapely.geometry import Polygon
    outline = Polygon(poly.exterior).simplify(0.3)
    if outline.is_empty or outline.geom_type != "Polygon":
        return []
    c = list(outline.exterior.coords)
    ccw = outline.exterior.is_ccw
    out = []
    for a, b in zip(c, c[1:]):
        L = math.dist(a, b)
        if L < 8.0:
            continue
        tx, ty = (b[0] - a[0]) / L, (b[1] - a[1]) / L
        nx, ny = (ty, -tx) if ccw else (-ty, tx)            # outward normal
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        x, y = mx + 0.6 * nx, my + 0.6 * ny
        # The wall really faces out here: 0.4 m behind it is inside this building, 0.3 m in front is not.
        if not (poly.contains(shapely.Point(mx - 0.4 * nx, my - 0.4 * ny))
                and not poly.contains(shapely.Point(mx + 0.3 * nx, my + 0.3 * ny))):
            continue
        # Clear, flat ground in front: a 2 m wide strip from 0.3 m to 3.6 m out.
        strip = Polygon([(mx + s * tx + d * nx, my + s * ty + d * ny)
                         for s, d in ((-1, 0.3), (1, 0.3), (1, 3.6), (-1, 3.6))])
        if world.union.intersects(strip) or world.tree_distance(x, y) < 2.0:
            continue
        if not world.campus.contains(shapely.Point(x, y)):
            continue
        hs = world.ground([mx + d * nx for d in (0.6, 1.8, 3.0)] + [x - tx, x + tx],
                          [my + d * ny for d in (0.6, 1.8, 3.0)] + [y - ty, y + ty])
        if not np.isfinite(hs).all() or hs.max() - hs.min() > 0.4:
            continue
        m = world.surface(x, y)
        if m is not None and m not in WALK_MATERIALS:
            continue
        if world.built and not 0.45 <= world.built_distance(x, y) <= 0.8:
            continue                                  # must sit 0.6 m off the collider wall
        out.append((L, x, y, nx, ny))
    return sorted(out, reverse=True)


def make_climbs(root, world, footprints, near_pts, report, count=6):
    import shapely
    pts = shapely.multipoints(np.asarray(near_pts)) if near_pts else None
    scored = []
    for bid, poly in footprints.items():
        if ZONES.get(bid, "campus") != "campus":
            continue
        if world.built:
            if bid not in world.built:
                continue                     # not exported: nothing to climb in the game
            poly = world.built[bid]          # the collider outline, not the OSM/Metro footprint
            if poly.geom_type == "MultiPolygon":
                poly = max(poly.geoms, key=lambda g: g.area)
        if poly.geom_type != "Polygon" or poly.area < 80:
            continue
        rect = rectangularity(poly)
        dist = float(shapely.distance(pts, poly)) if pts is not None else 0.0
        if bid != KIRKLAND and (rect < 0.85 or dist > 80):
            continue
        name, top = building_info(root, bid)
        if top is not None:
            gx, gy = poly.representative_point().x, poly.representative_point().y
            if top - world.ground(gx, gy)[0] < 6.0:
                continue
        walls = wall_anchors(bid, poly, world)
        if walls:
            scored.append((bid == KIRKLAND, -dist + 2 * walls[0][0] + 20 * rect, bid, name, walls[0]))
    scored.sort(key=lambda s: (s[0], s[1]), reverse=True)
    climbs = []
    for is_k, score, bid, name, (L, x, y, nx, ny) in scored[:count]:
        climbs.append({"building": bid, "pos": world.point(x, y, 0.0),
                       "forward": [round(-nx, 4), 0.0, round(-ny, 4)]})
        report["climb"].append({"building": bid, "name": name, "wall_m": round(L, 1)})
    if not any(c["building"] == KIRKLAND for c in climbs):
        report["route_notes"].append("Kirkland Hall has no clear straight wall >= 8 m at LoD2 (not a climb anchor)")
    if len(climbs) < count:
        report["problems"].append(f"only {len(climbs)} climb anchors")
    return climbs


# ---- viewpoints (budget check camera spots) ---------------------------------------

def tile_triangles(root, plans):
    """Triangles per tile of what is drawn up close (terrain, LOD0 buildings, trees, ring)."""
    out = {}
    for pl in plans:
        n = 0
        for o in pl["objects"]:
            if o["role"] in ("terrain", "building_lod0", "trees", "ring"):
                for s in o["src"]:
                    with np.load(s) as z:
                        n += len(z["faces"])
        out[pl["tile"]] = n
    return out


def preview_triangles(footprints):
    """Without meshes: 80k terrain + 600 per building per tile (ranking only)."""
    from export_all import tile_of
    out = {}
    for poly in footprints.values():
        c = poly.representative_point()
        t = tile_of(c.x, c.y)
        out[t] = out.get(t, 80000) + 600
    return out


def view_score(x, y, heading, tris):
    from export_all import tile_bounds
    total = 0
    hx, hy = math.cos(heading), math.sin(heading)
    for t, n in tris.items():
        x0, y0, x1, y1 = tile_bounds(t)
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        dx, dy = cx - x, cy - y
        d = math.hypot(dx, dy)
        if d > 700:
            continue
        if d < 71 or (dx * hx + dy * hy) / d >= math.cos(math.radians(45)):
            total += n
    return total


def make_viewpoints(world, cands, tris, alumni, kirkland, peabody, report):
    def view(name, x, y, look_xy):
        p = world.point(x, y, EYE)
        lx, ly = look_xy
        return {"name": name, "pos": p, "look": [round(lx, 2), p[1], round(ly, 2)]}

    def best_heading(x, y):
        hs = [k * math.pi / 8 for k in range(16)]
        scores = [view_score(x, y, h, tris) for h in hs]
        k = int(np.argmax(scores))
        return hs[k], scores[k]

    views = []
    ax, ay = alumni
    views.append(view("AlumniLawn_Kirkland", ax, ay, kirkland))
    report["viewpoints"].append({"name": "AlumniLawn_Kirkland",
                                 "score": view_score(ax, ay, math.atan2(kirkland[1] - ay, kirkland[0] - ax), tris)})
    px, py = peabody
    h, s = best_heading(px, py)
    views.append(view("PeabodyEsplanade", px, py, (px + 50 * math.cos(h), py + 50 * math.sin(h))))
    report["viewpoints"].append({"name": "PeabodyEsplanade", "score": s})
    ranked = []
    for name, (x, y) in cands:
        h, s = best_heading(x, y)
        ranked.append((s, name, x, y, h))
    ranked.sort(reverse=True)
    used = [(ax, ay), (px, py)]
    for s, name, x, y, h in ranked:
        if len(views) == 5:
            break
        if all(math.hypot(x - u[0], y - u[1]) >= 150 for u in used):
            views.append(view(name, x, y, (x + 50 * math.cos(h), y + 50 * math.sin(h))))
            report["viewpoints"].append({"name": name, "score": s})
            used.append((x, y))
    return views


# ---- main ------------------------------------------------------------------------

def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--out", type=Path, default=CONFIG / "unity_map.json")
    p.add_argument("--preview", action="store_true", help="allow stand-in inputs (marks the file preview)")
    p.add_argument("--layout-version", type=int, help="default: keep the current file's value (or 1)")
    args = p.parse_args(argv)
    t0 = time.time()
    root = data_root()
    import frame
    import shapely
    from export_all import plan_tiles, tile_zones

    report = {"made": time.strftime("%Y-%m-%dT%H:%M:%S"), "preview": args.preview, "notes": [], "problems": [],
              "spawn_sites_missing": [], "spawn_sites_rejected": [], "route_notes": [], "climb": [],
              "viewpoints": []}
    polys = frame.boundary_polygons()
    campus = polys["campus"]
    ground = Ground(root, args.preview)
    report["notes"] += ground.notes
    footprints = load_footprints(root, args.preview, report["notes"])
    trees = load_trees(root, args.preview, report["notes"])
    built = built_obstacles(root, report["notes"])
    trunks = trunk_colliders(root)
    report["notes"].append(f"{len(trunks)} trunk colliders from trees_col.npz")
    world = World(ground, Surface(root), footprints, trees, campus, built, trunks, WalkSurface(root, ground))

    # Spawns, teams, hill.
    spawns, sites = make_spawns(root, world, report)
    teams = [t for t in (make_team("North", sites.get("AlumniLawn"), world),
                         make_team("South", sites.get("PeabodyEsplanade"), world)) if t]
    if len(teams) < 2:
        report["problems"].append("could not place both team areas")
    lib = sites.get("LibraryLawn")
    hill = world.point(*lib[2][0], 0.0) if lib and lib[2] else (spawns[0]["pos"] if spawns else None)

    # Route Alumni Lawn -> Peabody Esplanade.
    alumni = sites["AlumniLawn"][2][0] if sites.get("AlumniLawn") and sites["AlumniLawn"][2] else (0.0, 0.0)
    peabody = sites["PeabodyEsplanade"][2][0] if sites.get("PeabodyEsplanade") and sites["PeabodyEsplanade"][2] \
        else (450.37, -670.71)
    route = make_route(root, world, alumni, peabody, report)

    # Climb anchors near the route and the spawns.
    near = [(r[0], r[2]) for r in route] + [tuple(s["campus"]) for s in spawns]
    climbs = make_climbs(root, world, footprints, near, report)

    # Viewpoints.
    plans, _ = plan_tiles(root, Path("unused"), {})
    tris = preview_triangles(footprints)
    tris.update(tile_triangles(root, plans))
    if len(plans) < len(tris):
        report["notes"].append(f"viewpoints: {len(tris) - len(plans)} tiles without meshes use estimated "
                               "triangle counts (80k + 600 per building)")
    kirk = footprints.get(KIRKLAND)
    kirk_xy = (kirk.centroid.x, kirk.centroid.y) if kirk is not None else (104.59, 83.08)
    cands = [(s["name"], tuple(s["campus"])) for s in spawns] + \
            [(f"Route_{i:02d}", (r[0], r[2])) for i, r in enumerate(route)]
    cands = [c for c in cands if world.built_distance(*c[1]) >= BUILT_CLEAR]
    views = make_viewpoints(world, cands, tris, alumni, kirk_xy, peabody, report)

    # Area and lowest ground.
    zones = tile_zones(root)
    lowest = None
    for pl in plans:
        col = next((o for o in pl["objects"] if o["role"] == "terrain_col"), None)
        if col:
            with np.load(col["src"][0]) as z:
                m = float(z["verts"][:, 2].min())
            lowest = m if lowest is None else min(lowest, m)
    if lowest is None:
        fr = frame.load_frame()
        lowest = float(fr["vertical"]["min_ground_Z"])
        report["notes"].append("lowest_ground from frame.json (no terrain colliders yet)")
    tops = [building_info(root, b)[1] for b in footprints]
    tops = [t for t in tops if t is not None]
    if tops:
        high = max(tops)
    else:
        high = float(np.nanmax(ground.dtm.a)) + 60.0 if ground.dtm is not None else lowest + 100.0
        report["notes"].append("highest roof estimated (no building meshes yet)")
    x0, y0, x1, y1 = campus.bounds
    lo_v, hi_v = lowest - 10.0, high + 30.0
    local_area = {"center": to_unity((x0 + x1) / 2, (y0 + y1) / 2, (lo_v + hi_v) / 2),
                  "size": [round(x1 - x0 + 60, 2), round(hi_v - lo_v, 2), round(y1 - y0 + 60, 2)]}
    if lowest < 0:
        report["problems"].append(f"lowest ground {lowest:.2f} < 0 (the Unity contract needs y >= 0)")

    version = args.layout_version
    if version is None:
        version = load_json(args.out).get("layout_version", 1) if args.out.exists() else 1
    out = {"layout_version": version, "local_area": local_area, "lowest_ground": round(lowest, 2),
           "spawns": [{"name": s["name"], "pos": s["pos"]} for s in spawns],
           "teams": teams, "hill": hill, "viewpoints": views, "route": route,
           "climb": climbs, "sheltered": []}
    if args.preview:
        out["preview"] = True
    # Every standing point must be BUILT_CLEAR outside the building colliders (climb anchors: 0.6 m).
    pts = [("spawn " + sp["name"], sp["pos"]) for sp in out["spawns"]]
    pts += [(f"team {t['name']} flag", t["flag"]) for t in teams] +         [(f"team {t['name']} spawn", q) for t in teams for q in t["spawns"]]
    pts += [("hill", hill)] if hill else []
    pts += [("viewpoint " + v["name"], v["pos"]) for v in views]
    close = [(n, round(world.built_distance(q[0], q[2]), 2)) for n, q in pts
             if world.built_distance(q[0], q[2]) < BUILT_CLEAR]
    report["points_too_close_to_buildings"] = close
    if close:
        report["problems"].append(f"{len(close)} points closer than {BUILT_CLEAR} m to a building collider: {close[:5]}")
    report["climb_wall_distance_m"] = [round(world.built_distance(c["pos"][0], c["pos"][2]), 2) for c in climbs]
    save_json(args.out, out)
    report["sheltered"] = "none: LoD2 buildings are solid, no roofed open areas"
    report["zones_known"] = len(zones)
    report["seconds"] = round(time.time() - t0, 1)
    rpath = root / "reports" / "stage1" / "unity_map_report.json"
    save_json(rpath, report)
    try:
        import manifest
        manifest.add_file(rpath, source="pipeline/make_unity_map.py", stage=1, root=root)
    except Exception as e:
        print(f"warning: manifest not updated: {e}")
    r = report.get("route", {})
    print(f"wrote {args.out}: {len(spawns)} spawns, {len(teams)} teams, route {r.get('length_m')} m "
          f"({r.get('waypoints')} waypoints, {len(r.get('steps_over_0.5m', []))} steps > 0.5 m), "
          f"{len(climbs)} climbs, {len(views)} viewpoints")
    for prob in report["problems"]:
        print("PROBLEM", prob)
    sys.exit(1 if report["problems"] else 0)


if __name__ == "__main__":
    main()
