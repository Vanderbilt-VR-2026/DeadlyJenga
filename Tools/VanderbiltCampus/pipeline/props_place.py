"""Place street props on the campus (Stage 5): OSM first, then simple rules where OSM is thin.

From OSM (raw/osm/<date>/campus_overpass.json), "source": "osm":
  amenity=bench -> bench          amenity=waste_basket / recycling -> bin
  amenity=bicycle_parking -> bike_rack                barrier=bollard -> bollard
  highway=street_lamp -> lamp_post                    man_made=flagpole -> flagpole
  amenity=fountain -> fountain_basin (+ fountain_tier when the basin is >= 3 m across;
                      OSM areas set the basin size)
  tourism=artwork (statue / sculpture), historic=memorial / monument, memorial=statue -> statue
  tourism=information, highway=bus_stop -> sign_post (blank face)
  man_made=planter -> planter
By rule, "source": "rule" (each instance says which rule):
  lamp_post   every 30 m along campus footpaths, 0.6 m off one edge (left, else right), none
              within 12 m of another lamp
  bench       every 60 m along footpaths that are not sidewalks or crossings, on grass or
              paving beside the path, none within 25 m of another bench
  bin         beside every second bench that has no bin within 20 m
  planter     beside paved footpath junctions (3+ paths meet), none within 40 m of another
  bike_rack   beside each campus building of 1500 m2 or more, 2.5 m out from the wall nearest
              a footpath, facing the path (OSM has no racks inside the campus)
Rules for every prop: inside the campus boundary; never inside a building footprint
(buildings v2 index + v1 reconciled + OSM, grown 0.3 m), never on a road carriageway (OSM
car roads grown by half their width; an OSM prop on a road is pushed to the road edge, up to
4 m), not in water (except fountains), at least 1.2 m from a tree trunk (rule props only).
Ground Z from the terrain heights (derived/terrain/v1/<tile>/heights.npy).
Heading: front (+Y of the prop) faces the nearest footpath within 12 m, else faces away
from the nearest building wall within 15 m, else 0. heading_deg is counter-clockwise about
Z; 0 = front faces campus +Y.

Writes derived/props/v1/instances.json (and records it in the manifest, stage 5).
"""
import argparse
import datetime
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
import manifest  # noqa: E402
import props_kit as pk  # noqa: E402
import trees_common as tc  # noqa: E402
import vcmesh  # noqa: E402
from common import data_root, fail, load_json, save_json  # noqa: E402

VERSION = "v1"
LICENSE = "Derived: OpenStreetMap (ODbL 1.0), USGS 3DEP LiDAR (public domain)"
PATH_KINDS = {"footway", "pedestrian", "path", "cycleway"}
ROAD_HALF_WIDTH = {"motorway": 8.0, "trunk": 7.5, "primary": 7.0, "secondary": 6.0, "tertiary": 5.0,
                   "residential": 4.0, "unclassified": 3.5, "service": 2.75, "living_street": 3.0,
                   "primary_link": 3.5, "secondary_link": 3.5, "tertiary_link": 3.5}
R = {"lamp_step": 30.0, "lamp_edge": 0.6, "lamp_spacing": 12.0, "bench_step": 60.0, "bench_edge": 1.0,
     "bench_spacing": 25.0, "bin_near_bench": 20.0, "planter_spacing": 40.0, "planter_offset": 2.5,
     "trunk_clear": 1.2, "building_clear": 0.3, "road_push_max": 4.0, "path_search": 12.0,
     "building_search": 15.0, "default_path_width": 2.0, "bike_rack_min_area": 1500.0,
     "bike_rack_off_wall": 2.5}


# ---- inputs ----------------------------------------------------------------------

class Ground:
    """Terrain height and surface material lookups from derived/terrain/v1 tiles (lazy)."""

    def __init__(self, root):
        self.dir = Path(root) / "derived/terrain/v1"
        if not (self.dir / "index.json").exists():
            fail("derived/terrain/v1/index.json is missing; run the terrain step first")
        self.h, self.s = {}, {}
        self.mats = {v: k for k, v in vcmesh.material_index().items()}

    def _tile(self, tile):
        if tile not in self.h:
            d = self.dir / tile
            if (d / "heights.npy").exists():
                self.h[tile] = (np.load(d / "heights.npy"), load_json(d / "heights.json"))
                self.s[tile] = np.load(d / "surface.npy") if (d / "surface.npy").exists() else None
            else:
                self.h[tile] = None
                self.s[tile] = None
        return self.h[tile]

    def z(self, x, y):
        t = self._tile(fr.tile_of(x, y))
        if t is None:
            return None
        a, meta = t
        fx = (x - meta["origin_x"]) / meta["cell_m"]
        fy = (y - meta["origin_y"]) / meta["cell_m"]
        i0 = int(np.clip(np.floor(fx), 0, a.shape[1] - 2)); j0 = int(np.clip(np.floor(fy), 0, a.shape[0] - 2))
        tx, ty = float(np.clip(fx - i0, 0, 1)), float(np.clip(fy - j0, 0, 1))
        return float(a[j0, i0] * (1 - tx) * (1 - ty) + a[j0, i0 + 1] * tx * (1 - ty) +
                     a[j0 + 1, i0] * (1 - tx) * ty + a[j0 + 1, i0 + 1] * tx * ty)

    def surface(self, x, y):
        tile = fr.tile_of(x, y)
        self._tile(tile)
        s = self.s.get(tile)
        if s is None:
            return None
        x0, y0, _, _ = fr.tile_bounds(tile)
        i = int(np.clip((x - x0) / 0.25, 0, s.shape[1] - 1)); j = int(np.clip((y - y0) / 0.25, 0, s.shape[0] - 1))
        return self.mats.get(int(s[j, i]))


def building_polys(root):
    """All footprints we know: buildings v2 index, v1 reconciled footprints, OSM."""
    from shapely.geometry import Polygon
    polys, src = [], {}
    v2 = Path(root) / "derived/buildings/v2/index.json"
    if v2.exists():
        for b in load_json(v2)["buildings"]:
            polys.append(Polygon(b["footprint"], b.get("holes") or []).buffer(0))
            for ex in b.get("extra_polygons") or []:
                if ex and len(ex[0]) >= 3:
                    polys.append(Polygon(ex[0], ex[1:]).buffer(0))
        src["buildings_v2"] = len(polys)
    v1 = Path(root) / "derived/buildings/v1/footprints.json"
    if v1.exists():
        n = len(polys)
        for b in load_json(v1)["buildings"]:
            for part in b.get("polygon") or []:
                if part and len(part[0]) >= 3:
                    polys.append(Polygon(part[0], part[1:]).buffer(0))
        src["buildings_v1"] = len(polys) - n
    osm = tc.osm_buildings(root)
    polys += [b["poly"] for b in osm]
    src["osm"] = len(osm)
    return [p for p in polys if not p.is_empty], src


def osm_lines(root):
    """(paths, roads): footpath lines [(LineString, tags)] and road polygons (grown by half width)."""
    import make_boundary as mb
    from shapely.geometry import LineString
    nodes, ways, _, _ = tc.osm_elements(root)
    paths, roads = [], []
    for w in ways.values():
        t = w.get("tags", {})
        hw = t.get("highway")
        if not hw:
            continue
        c = mb.way_coords(w, nodes)
        if len(c) < 2:
            continue
        line = tc.to_campus_geom(LineString(c))
        if hw in ROAD_HALF_WIDTH and t.get("area") != "yes" and t.get("tunnel") != "yes":
            half = ROAD_HALF_WIDTH[hw]
            try:
                if "width" in t:
                    half = float(str(t["width"]).split()[0]) / 2
                elif "lanes" in t:
                    half = max(half * 0.6, 1.65 * float(t["lanes"]))
            except ValueError:
                pass
            if hw == "service" and t.get("service") in ("parking_aisle", "driveway"):
                half = 2.5
            roads.append(line.buffer(half, cap_style=2))
        if hw in PATH_KINDS or hw == "steps":
            paths.append((line, t, w["id"]))
    return paths, roads


# ---- geometry helpers ---------------------------------------------------------------

def heading_towards(px, py, qx, qy):
    """heading_deg so that the prop's front (+Y) points from p to q."""
    return float(np.degrees(np.arctan2(-(qx - px), qy - py))) % 360.0


class Scene:
    def __init__(self, root):
        import shapely
        self.root = root
        self.campus = tc.zone_polys()["campus"]
        bl, self.building_sources = building_polys(root)
        self.bl = bl
        self.bl_tree = shapely.STRtree(bl)
        self.paths, roads = osm_lines(root)
        self.roads = roads
        self.road_tree = shapely.STRtree(roads)
        walkable = [p for p, t, _ in self.paths if t.get("highway") != "steps"]
        self.path_geoms = walkable
        self.path_tree = shapely.STRtree(walkable)
        self.ground = Ground(root)
        tj = Path(root) / "derived/trees/v1/trees.json"
        trees = [t for t in load_json(tj)["trees"] if t["kind"] == "tree"] if tj.exists() else []
        from scipy.spatial import cKDTree
        self.trunks = cKDTree(np.array([[t["x"], t["y"]] for t in trees])) if trees else None

    def in_building(self, x, y, clear=None):
        from shapely.geometry import Point
        p = Point(x, y).buffer(clear if clear is not None else R["building_clear"], 8)
        return len(self.bl_tree.query(p, predicate="intersects")) > 0

    def road_at(self, x, y):
        from shapely.geometry import Point
        hits = self.road_tree.query(Point(x, y), predicate="intersects")
        return [self.roads[i] for i in hits]

    def push_off_road(self, x, y):
        """Move a point out of every road polygon (to the nearest edge + 0.3 m). None if too far."""
        from shapely.geometry import Point
        from shapely.ops import nearest_points, unary_union
        hits = self.road_at(x, y)
        if not hits:
            return x, y, 0.0
        u = unary_union([h.buffer(0.3) for h in hits] + [r for r in self.roads if r.distance(Point(x, y)) < 6])
        q = nearest_points(u.exterior if u.geom_type == "Polygon" else u.boundary, Point(x, y))[0]
        d = float(np.hypot(q.x - x, q.y - y))
        if d > R["road_push_max"] or self.road_at(q.x, q.y):
            return None
        return q.x, q.y, d

    def near_trunk(self, x, y, r):
        return self.trunks is not None and len(self.trunks.query_ball_point([x, y], r)) > 0

    def heading(self, x, y):
        """(heading_deg, from) following the module rules."""
        from shapely.geometry import Point
        from shapely.ops import nearest_points
        p = Point(x, y)
        i = self.path_tree.nearest(p)
        if i is not None:
            g = self.path_geoms[i]
            d = g.distance(p)
            if d <= R["path_search"]:
                q = nearest_points(g, p)[0]
                if d > 0.05:
                    return heading_towards(x, y, q.x, q.y), "path"
                s = g.project(p)
                a, b = g.interpolate(max(s - 0.5, 0)), g.interpolate(min(s + 0.5, g.length))
                return float(np.degrees(np.arctan2(b.y - a.y, b.x - a.x))) % 360.0, "path_along"
        cand = self.bl_tree.query(p.buffer(R["building_search"]))
        if len(cand):
            j = min(cand, key=lambda k: self.bl[k].distance(p))
            q = nearest_points(self.bl[j].boundary, p)[0]
            if q.distance(p) > 0.01:
                return heading_towards(q.x, q.y, x, y), "building"
        return 0.0, "none"

    def ok_spot(self, x, y, water_ok=False):
        """Reason a spot is not allowed, or None if it is fine."""
        if not self.campus.contains(__import__("shapely").geometry.Point(x, y)):
            return "outside_campus"
        if self.in_building(x, y):
            return "in_building"
        if self.road_at(x, y):
            return "on_road"
        if not water_ok and self.ground.surface(x, y) == "water":
            return "in_water"
        if self.ground.z(x, y) is None:
            return "no_terrain"
        return None


# ---- OSM props ---------------------------------------------------------------------------

def osm_kind(tags):
    a, hw = tags.get("amenity"), tags.get("highway")
    if a == "bench":
        return "bench"
    if a in ("waste_basket", "recycling") and tags.get("recycling_type") != "centre":
        return "bin"
    if a == "bicycle_parking":
        return "bike_rack"
    if a == "fountain":
        return "fountain_basin"
    if hw == "street_lamp":
        return "lamp_post"
    if tags.get("barrier") == "bollard":
        return "bollard"
    if tags.get("man_made") == "flagpole":
        return "flagpole"
    if tags.get("man_made") == "planter":
        return "planter"
    if (tags.get("tourism") == "artwork" and tags.get("artwork_type") in ("statue", "sculpture", "bust")) \
            or tags.get("historic") in ("memorial", "monument") or tags.get("memorial") == "statue":
        return "statue"
    if tags.get("tourism") == "information" or hw == "bus_stop":
        return "sign_post"
    return None


def osm_candidates(root):
    """(kind, x, y, scale, osm id, tags) for every mapped OSM node, area or line node."""
    import make_boundary as mb
    from shapely.geometry import Point
    nodes, ways, _, _ = tc.osm_elements(root)
    out, unmapped = [], {}
    for el in list(nodes.values()) + list(ways.values()):
        t = el.get("tags", {})
        if not t:
            continue
        kind = osm_kind(t)
        if kind is None:
            for k, v in (("amenity", "picnic_table"), ("leisure", "picnic_table"), ("amenity", "drinking_water"),
                         ("amenity", "post_box"), ("amenity", "vending_machine"), ("emergency", "phone"),
                         ("tourism", "artwork")):
                if t.get(k) == v:
                    key = f"{k}={v}" + (f" ({t.get('artwork_type')})" if k == "tourism" else "")
                    unmapped[key] = unmapped.get(key, 0) + 1
            continue
        scale = 1.0
        if el["type"] == "node":
            X, Y, _ = fr.to_campus(el["lon"], el["lat"], 0.0, 4326)
            out.append((kind, float(X), float(Y), scale, f"node/{el['id']}", t))
            continue
        poly = tc.area_with_holes(el, nodes, ways)
        if poly is not None and not poly.is_empty:
            poly = tc.to_campus_geom(poly)
            c = poly.representative_point() if kind != "fountain_basin" else poly.centroid
            if kind == "fountain_basin":
                scale = float(np.clip(np.sqrt(poly.area / np.pi) / 2.0, 0.4, 4.0))
            out.append((kind, c.x, c.y, scale, f"way/{el['id']}", t))
        elif kind == "bollard":
            # a line of bollards: one every 1.5 m along the way
            from shapely.geometry import LineString
            c = mb.way_coords(el, nodes)
            if len(c) >= 2:
                line = tc.to_campus_geom(LineString(c))
                for s in np.arange(0, line.length + 1e-6, 1.5):
                    q = line.interpolate(s)
                    out.append((kind, q.x, q.y, 1.0, f"way/{el['id']}", t))
    return out, unmapped


# ---- rules -------------------------------------------------------------------------------

def footpath_lines(scene, sidewalks=True):
    """Merged campus footpath lines eligible for rule props."""
    from shapely.ops import linemerge, unary_union
    keep = []
    for line, t, _ in scene.paths:
        if t.get("highway") == "steps" or t.get("footway") in ("crossing", "traffic_island"):
            continue
        if t.get("indoor") == "yes" or t.get("tunnel") == "yes" or t.get("covered") == "yes" \
                or t.get("bridge") == "yes" or t.get("layer", "0") not in ("0",):
            continue
        if not sidewalks and t.get("footway") == "sidewalk":
            continue
        keep.append(line)
    merged = linemerge(unary_union(keep).intersection(scene.campus))
    return [g for g in getattr(merged, "geoms", [merged]) if g.geom_type == "LineString" and g.length > 5]


def offset_point(line, s, side, dist):
    """Point dist meters to the left (side=+1) or right (-1) of the line at distance s, and the path point."""
    a = line.interpolate(max(s - 0.5, 0)); b = line.interpolate(min(s + 0.5, line.length))
    t = np.array([b.x - a.x, b.y - a.y]); t /= max(np.linalg.norm(t), 1e-9)
    n = np.array([-t[1], t[0]]) * side
    p = line.interpolate(s)
    return p.x + n[0] * dist, p.y + n[1] * dist, (p.x, p.y), t


class Placed:
    """Placed instances with a per-kind KD lookup for spacing rules."""

    def __init__(self):
        self.items = []

    def near(self, kind, x, y, r):
        return any(i["kind"] == kind and (i["x"] - x) ** 2 + (i["y"] - y) ** 2 < r * r for i in self.items)

    def add(self, **kw):
        self.items.append(kw)


def place_rule_along(scene, placed, lines, kind, step, start, edge, spacing, rule, need_surface=None, report=None):
    width = R["default_path_width"]
    for line in lines:
        L = line.length
        ss = np.arange(min(start, L / 2), L, step)
        for s in ss:
            done = False
            for side in (1, -1):
                x, y, (px, py), t = offset_point(line, s, side, width / 2 + edge)
                why = scene.ok_spot(x, y)
                if why is None and scene.near_trunk(x, y, R["trunk_clear"]):
                    why = "tree_trunk"
                if why is None and need_surface and scene.ground.surface(x, y) not in need_surface:
                    why = "surface"
                if why is None and placed.near(kind, x, y, spacing):
                    why = "spacing"
                if why is None:
                    placed.add(kind=kind, x=x, y=y, heading_deg=heading_towards(x, y, px, py), heading_from="path",
                               scale=1.0, source="rule", rule=rule)
                    done = True
                    break
                if report is not None:
                    report[why] = report.get(why, 0) + 1
                if why == "spacing":
                    break
            _ = done


def junctions(lines, scene):
    """Points where 3+ footpath lines meet (from the merged network)."""
    ends = {}
    for line in lines:
        for q in (line.coords[0], line.coords[-1]):
            k = (round(q[0], 1), round(q[1], 1))
            ends[k] = ends.get(k, 0) + 1
    return [k for k, n in ends.items() if n >= 3]


# ---- main --------------------------------------------------------------------------------

def run(root):
    t0 = time.time()
    scene = Scene(root)
    placed = Placed()
    dropped, pushed = [], 0
    cands, unmapped = osm_candidates(root)
    for kind, x, y, scale, oid, tags in cands:
        if not scene.campus.contains(__import__("shapely").geometry.Point(x, y)):
            continue
        if scene.in_building(x, y, 0.0):
            dropped.append({"kind": kind, "osm": oid, "reason": "inside_building (probably indoors)"})
            continue
        r = scene.push_off_road(x, y)
        if r is None:
            dropped.append({"kind": kind, "osm": oid, "reason": "on_road_and_cannot_move"})
            continue
        x, y, d = r
        pushed += d > 0
        why = scene.ok_spot(x, y, water_ok=(kind == "fountain_basin"))
        if why:
            dropped.append({"kind": kind, "osm": oid, "reason": why})
            continue
        hd, hfrom = scene.heading(x, y)
        keep_tags = {k: v for k, v in tags.items() if k in ("amenity", "highway", "tourism", "artwork_type", "historic",
                                                            "memorial", "man_made", "barrier", "material", "name")}
        rec = dict(kind=kind, x=x, y=y, heading_deg=hd, heading_from=hfrom, scale=scale, source="osm", osm=oid,
                   osm_tags=keep_tags)
        if d > 0:
            rec["moved_off_road_m"] = round(d, 2)
        placed.add(**rec)
        if kind == "fountain_basin" and scale * 4.0 >= 3.0:
            placed.add(kind="fountain_tier", x=x, y=y, heading_deg=hd, heading_from=hfrom, scale=min(scale, 1.5),
                       source="osm", osm=oid, osm_tags=keep_tags)

    rule_report = {}
    lines_all = footpath_lines(scene, sidewalks=True)
    lines_park = footpath_lines(scene, sidewalks=False)
    rr = rule_report.setdefault("lamp_post", {})
    place_rule_along(scene, placed, lines_all, "lamp_post", R["lamp_step"], R["lamp_step"] / 2, R["lamp_edge"],
                     R["lamp_spacing"], "lamp every 30 m along footpaths", report=rr)
    rr = rule_report.setdefault("bench", {})
    place_rule_along(scene, placed, lines_park, "bench", R["bench_step"], R["bench_step"] / 2, R["bench_edge"],
                     R["bench_spacing"], "bench every 60 m along non-sidewalk footpaths",
                     need_surface={"grass", "paving_brick", "paving_concrete"}, report=rr)
    # Bins beside every second bench without a bin nearby.
    benches = sorted([i for i in placed.items if i["kind"] == "bench"], key=lambda i: (round(i["x"], 1), round(i["y"], 1)))
    rr = rule_report.setdefault("bin", {})
    for k, b in enumerate(benches):
        if k % 2 or placed.near("bin", b["x"], b["y"], R["bin_near_bench"]):
            continue
        a = np.radians(b["heading_deg"])
        for side in (1, -1):                     # local +X / -X of the bench, 1.3 m out
            x = b["x"] + side * 1.3 * np.cos(a); y = b["y"] + side * 1.3 * np.sin(a)
            why = scene.ok_spot(x, y) or ("tree_trunk" if scene.near_trunk(x, y, 0.8) else None)
            if why is None:
                placed.add(kind="bin", x=x, y=y, heading_deg=b["heading_deg"], heading_from="bench", scale=1.0,
                           source="rule", rule="bin beside every second bench")
                break
            rr[why] = rr.get(why, 0) + 1
    # Planters beside footpath junctions on paving.
    rr = rule_report.setdefault("planter", {})
    for (jx, jy) in junctions(lines_all, scene):
        if scene.ground.surface(jx, jy) not in ("paving_brick", "paving_concrete"):
            rr["not_paved"] = rr.get("not_paved", 0) + 1
            continue
        for ang in range(0, 360, 45):
            x = jx + R["planter_offset"] * np.cos(np.radians(ang)); y = jy + R["planter_offset"] * np.sin(np.radians(ang))
            why = scene.ok_spot(x, y) or ("tree_trunk" if scene.near_trunk(x, y, R["trunk_clear"]) else None)
            if why is None and placed.near("planter", x, y, R["planter_spacing"]):
                why = "spacing"
            if why is None and any(g.distance(__import__("shapely").geometry.Point(x, y)) < 1.2 for g in
                                   [scene.path_geoms[i] for i in scene.path_tree.query(
                                       __import__("shapely").geometry.Point(x, y).buffer(1.2))]):
                why = "on_path"
            if why is None:
                placed.add(kind="planter", x=x, y=y, heading_deg=heading_towards(x, y, jx, jy), heading_from="junction",
                           scale=1.0, source="rule", rule="planter beside paved footpath junction")
                break
            rr[why] = rr.get(why, 0) + 1
            if why == "spacing":
                break

    # Bike racks: one beside each large campus building (no OSM racks inside the campus).
    rr = rule_report.setdefault("bike_rack", {})
    v2 = Path(root) / "derived/buildings/v2/index.json"
    from shapely.geometry import Point, Polygon
    from shapely.ops import nearest_points
    for b in (load_json(v2)["buildings"] if v2.exists() else []):
        if b.get("area_m2", 0) < R["bike_rack_min_area"]:
            continue
        poly = Polygon(b["footprint"], b.get("holes") or []).buffer(0)
        near = scene.path_tree.query(poly.buffer(25.0))
        if not len(near):
            rr["no_path_near"] = rr.get("no_path_near", 0) + 1
            continue
        g = min((scene.path_geoms[i] for i in near), key=lambda g: g.distance(poly))
        wall, path_pt = nearest_points(poly.exterior, g)
        d = np.array([path_pt.x - wall.x, path_pt.y - wall.y])
        if np.linalg.norm(d) < 0.5:
            rr["path_touches_wall"] = rr.get("path_touches_wall", 0) + 1
            continue
        d /= np.linalg.norm(d)
        x, y = wall.x + d[0] * R["bike_rack_off_wall"], wall.y + d[1] * R["bike_rack_off_wall"]
        why = scene.ok_spot(x, y) or ("tree_trunk" if scene.near_trunk(x, y, R["trunk_clear"]) else None)
        if why is None and placed.near("bike_rack", x, y, 30.0):
            why = "spacing"
        if why:
            rr[why] = rr.get(why, 0) + 1
            continue
        placed.add(kind="bike_rack", x=x, y=y, heading_deg=heading_towards(x, y, path_pt.x, path_pt.y),
                   heading_from="path", scale=1.0, source="rule", rule="bike rack beside each building >= 1500 m2",
                   building=b["id"])

    # Ground height, tile, ids.
    items = []
    for i in placed.items:
        z = scene.ground.z(i["x"], i["y"])
        i = {**i, "x": round(float(i["x"]), 3), "y": round(float(i["y"]), 3), "z": round(float(z), 3),
             "heading_deg": round(float(i["heading_deg"]), 2), "scale": round(float(i["scale"]), 3),
             "tile": fr.tile_of(i["x"], i["y"])}
        items.append(i)
    items.sort(key=lambda i: (i["kind"], round(i["x"], 1), round(i["y"], 1)))
    counts, per = {}, {}
    for i in items:
        per[i["kind"]] = per.get(i["kind"], 0) + 1
        i["id"] = f"P_{i['kind']}_{per[i['kind']]:05d}"
        c = counts.setdefault(i["kind"], {"osm": 0, "rule": 0})
        c[i["source"]] += 1
    order = ["id", "kind", "x", "y", "z", "heading_deg", "scale", "tile", "source"]
    items = [{**{k: i[k] for k in order}, **{k: v for k, v in i.items() if k not in order}} for i in items]
    out = {
        "version": 1,
        "made": datetime.datetime.now().isoformat(timespec="seconds"),
        "frame": "campus meters (config/frame.json); z = terrain ground under the prop",
        "heading": "heading_deg = counter-clockwise rotation about Z; 0 = the prop's front (+Y) faces campus +Y",
        "scale": "uniform scale of the library mesh (fountain basins are sized from OSM areas)",
        "library": "derived/props/v1/library/<kind>_lod0|_lod1|_col.npz (props_build.py)",
        "rules": R,
        "footprints": scene.building_sources,
        "counts": dict(sorted(counts.items())),
        "osm_unmapped": unmapped,
        "osm_moved_off_road": int(pushed),
        "osm_dropped": dropped,
        "rule_rejections": rule_report,
        "instances": items,
    }
    print(f"placed {len(items)} props in {time.time() - t0:.0f} s: {out['counts']}")
    return out


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--no-manifest", action="store_true")
    args = p.parse_args(argv)
    root = data_root()
    out = run(root)
    path = root / "derived/props" / VERSION / "instances.json"
    save_json(path, out)
    if not args.no_manifest:
        manifest.add_file(path, "props_place.py (OSM, terrain v1, buildings v2/v1)", LICENSE, 5, root=root)
    print(f"wrote {path}")


if __name__ == "__main__":
    main()
