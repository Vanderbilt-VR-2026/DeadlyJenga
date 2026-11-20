"""Stage 2 shot list: what Evan photographs on campus, generated from the LoD2 model.

Reads the campus buildings (derived/buildings/v2/index.json if present, else
v1/footprints.json), each building's model.json (heights), the terrain surface
map (walkable ground), the trees, the OSM snapshot (paths, plazas, steps,
barriers, hospital tags, statues) and config/hero_spots.json.

Writes:
  Tools/VanderbiltCampus/config/shot_list.json            (the list; schema below)
  Docs/Development/VanderbiltCampus/capture/SHOT-LIST.md  (summary for people)
  <data_root>/derived/capture_plan/v1/shot_list.json      (archive copy, in the manifest)

Entry kinds: facade, courtyard, plaza, path, stairs, detail, swatch.
Methods: photo_set, 360_walk, detail_set, swatch.

Commands:
  shotlist.py build [--date YYYY-MM-DD]   build everything (date = season for best_time; default today)
  shotlist.py validate [file]             check a shot_list.json against SCHEMA

capture_days.py fills the "day" field afterwards; coverage_map.py draws the page.
"""
import argparse
import datetime as dt
import json
import math
import sys
from pathlib import Path
from zoneinfo import ZoneInfo

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CONFIG, REPO, data_root, fail, load_json, save_json  # noqa: E402

VERSION = 1
SHOT_LIST = CONFIG / "shot_list.json"
DOCS = REPO / "Docs" / "Development" / "VanderbiltCampus" / "capture"
SHOT_MD = DOCS / "SHOT-LIST.md"

# ---- rules (numbers used everywhere below) --------------------------------
MAX_TURN_DEG = 20.0        # consecutive wall segments closer than this in direction are one facade
MIN_FACADE_M = 4.0         # a facade is at least this long
MIN_COURTYARD_M2 = 20.0    # smaller interior rings are light wells (not photographable)
VIEW_M = 25.0              # how far in front of a facade we look for public ground
BLOCKED_M = 3.0            # another building closer than this in front = blocked
STANDOFF_MIN, STANDOFF_MAX, STANDOFF_K = 3.0, 15.0, 0.8   # stand-off = 0.8 x wall height, 3..15 m
FOV_DEG = 70.0             # phone 1x lens, horizontal field of view (landscape)
OVERLAP = 0.75             # 70-80 % overlap between neighbouring photos
PASSES, HEIGHTS = 3, 2     # straight + two angled passes, chest + overhead
CORNER_SHOTS = 6           # shots per corner turn per height (guide: 5 to 8)
MIN_SHARE = 0.4            # min_photos = 40 % of est_photos
LAT, LON = 36.15, -86.80   # Nashville
TZ = ZoneInfo("America/Chicago")
CELL = 0.5                 # raster cell for ground / building lookups (m)
LEG_M = 150.0              # path legs for 360 walks
HERO_ID = "B_544397715"    # Kirkland Hall: capture day 1
ALUMNI_LAWN = "way/259811259"

SIDES = ["north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west"]
KINDS = ["facade", "courtyard", "plaza", "path", "stairs", "detail", "swatch"]
METHODS = ["photo_set", "360_walk", "detail_set", "swatch"]
PRIORITIES = ["hero", "normal", "background"]
ACCESS = ["public", "restricted", "blocked"]

UTILITY_BUILDINGS = {"service", "industrial", "garage", "garages", "shed", "roof", "transformer_tower",
                     "greenhouse", "farm_auxiliary", "storage_tank", "hut", "kiosk", "utility", "construction"}
UTILITY_WORDS = ("power", "plant", "utility", "chiller", "steam", "substation", "maintenance", "warehouse")
CLINICAL_AMENITY = {"hospital", "clinic", "doctors"}

# ---- schema (documented in SHOT-LIST.md; intake relies on it) -------------
NUM = (int, float)
POINT = "point"        # [x, y]
LINE = "line"          # [[x, y], ...]
SCHEMA = {
    "top": {"version": int, "generated": str, "entries": list},
    "entry": {
        "id": (str,), "kind": (str,), "building": (str, None), "facade": (str, None),
        "priority": (str,), "method": (str,), "est_photos": (int,), "min_photos": (int,),
        "centerline": (LINE,), "normal": (POINT, None), "bearing_deg": (NUM, None),
        "standoff_m": (NUM, None), "camera_path": (LINE,), "tile": (str,), "best_time": (dict,),
        "access": (str,), "access_notes": (str,), "example_framing": (str,), "day": (int, None),
    },
    "enums": {"kind": KINDS, "method": METHODS, "priority": PRIORITIES, "access": ACCESS},
}


def _is_point(v):
    return isinstance(v, list) and len(v) == 2 and all(isinstance(c, NUM) for c in v)


def _type_ok(v, t):
    if t is None:
        return v is None
    if t == POINT:
        return _is_point(v)
    if t == LINE:
        return isinstance(v, list) and all(_is_point(p) for p in v)
    if t is int:
        return isinstance(v, int) and not isinstance(v, bool)
    if isinstance(t, tuple):
        return isinstance(v, t) and not isinstance(v, bool)
    return isinstance(v, t)


def validate(doc):
    """Return a list of problems (empty = valid)."""
    errs = []
    for k, t in SCHEMA["top"].items():
        if k not in doc or not isinstance(doc[k], t):
            errs.append(f"top-level '{k}' missing or not {t.__name__}")
    if errs:
        return errs
    seen = set()
    for i, e in enumerate(doc["entries"]):
        where = f"entry {i} ({e.get('id', '?')})"
        for k, types in SCHEMA["entry"].items():
            if k not in e:
                errs.append(f"{where}: missing '{k}'")
            elif not any(_type_ok(e[k], t) for t in types):
                errs.append(f"{where}: '{k}' has the wrong type")
        for k, allowed in SCHEMA["enums"].items():
            if e.get(k) not in allowed:
                errs.append(f"{where}: '{k}' = {e.get(k)!r} not in {allowed}")
        if e.get("id") in seen:
            errs.append(f"{where}: duplicate id")
        seen.add(e.get("id"))
        if isinstance(e.get("best_time"), dict) and "summary" not in e["best_time"]:
            errs.append(f"{where}: best_time has no 'summary'")
        if e.get("kind") == "facade" and e.get("normal") is None:
            errs.append(f"{where}: facade without normal")
    return errs


# ---- geometry: splitting outlines into facades ----------------------------

def _angle_diff(a, b):
    d = abs(a - b) % 360.0
    return min(d, 360.0 - d)


def _dir_deg(v):
    return math.degrees(math.atan2(v[1], v[0]))


def bearing_of(normal):
    """Compass bearing (0 = grid north, 90 = east) that a normal [x, y] points to."""
    return math.degrees(math.atan2(normal[0], normal[1])) % 360.0


def compass_side(bearing):
    return SIDES[int(((bearing % 360.0) + 22.5) // 45.0) % 8]


def split_ring(coords, max_turn=MAX_TURN_DEG, min_len=MIN_FACADE_M):
    """Split one closed ring into facades.

    coords: ring points, interior of the building on the LEFT when walking them
    (shapely orient(poly, 1): outer ring counter-clockwise, holes clockwise).
    Returns facades as dicts: points (polyline), length, dir (unit), normal
    (unit, pointing away from the building), bearing (grid degrees).
    """
    pts = [tuple(map(float, p)) for p in coords]
    if len(pts) > 1 and pts[0] == pts[-1]:
        pts = pts[:-1]
    clean = []
    for p in pts:
        if not clean or math.dist(p, clean[-1]) > 1e-6:
            clean.append(p)
    if len(clean) > 1 and math.dist(clean[0], clean[-1]) <= 1e-6:
        clean.pop()
    n = len(clean)
    if n < 3:
        return []
    segs = []
    for i in range(n):
        a, b = clean[i], clean[(i + 1) % n]
        ln = math.dist(a, b)
        if ln > 1e-6:
            segs.append({"a": a, "b": b, "len": ln, "ang": _dir_deg((b[0] - a[0], b[1] - a[1]))})
    if len(segs) < 3:
        return []
    # Start at a real corner so a straight wall is not cut at the ring's first point.
    start = 0
    for i in range(len(segs)):
        if _angle_diff(segs[i]["ang"], segs[i - 1]["ang"]) >= max_turn:
            start = i
            break
    segs = segs[start:] + segs[:start]

    def mean_ang(group):
        vx = sum(s["len"] * math.cos(math.radians(s["ang"])) for s in group)
        vy = sum(s["len"] * math.sin(math.radians(s["ang"])) for s in group)
        return _dir_deg((vx, vy))

    groups = [[segs[0]]]
    for s in segs[1:]:
        if _angle_diff(s["ang"], mean_ang(groups[-1])) < max_turn:
            groups[-1].append(s)
        else:
            groups.append([s])
    if len(groups) > 1 and _angle_diff(mean_ang(groups[0]), mean_ang(groups[-1])) < max_turn:
        groups[0] = groups.pop() + groups[0]

    def glen(g):
        return sum(s["len"] for s in g)

    # A short jog between two walls running the same way (a setback) joins them.
    changed = True
    while changed and len(groups) > 3:
        changed = False
        k = len(groups)
        for i, g in enumerate(groups):
            if glen(g) >= min_len:
                continue
            p, q = (i - 1) % k, (i + 1) % k
            if _angle_diff(mean_ang(groups[p]), mean_ang(groups[q])) >= max_turn:
                continue
            if i == 0:
                groups = [groups[k - 1] + groups[0] + groups[1]] + groups[2:k - 1]
            elif i == k - 1:
                groups = groups[1:k - 2] + [groups[k - 2] + groups[k - 1] + groups[0]]
            else:
                groups = groups[:i - 1] + [groups[i - 1] + g + groups[i + 1]] + groups[i + 2:]
            changed = True
            break
    # Short leftovers are corners (covered by the corner turns); keep the longest piece if nothing is long.
    kept = [g for g in groups if glen(g) >= min_len] or [max(groups, key=glen)]
    out = []
    for g in kept:
        ang = math.radians(mean_ang(g))
        d = (math.cos(ang), math.sin(ang))
        normal = (d[1], -d[0])  # right-hand side = outside (interior is on the left)
        points = [g[0]["a"]] + [s["b"] for s in g]
        out.append({"points": points, "length": glen(g), "dir": d, "normal": normal,
                    "bearing": bearing_of(normal)})
    return out


def facade_frame(f):
    """Straight chord for a facade: origin, u range along the wall, v of the outermost point."""
    d, nrm = f["dir"], f["normal"]
    P = np.array(f["points"])
    u = P @ np.array(d)
    v = P @ np.array(nrm)
    return {"u0": float(u.min()), "u1": float(u.max()), "v": float(v.max()), "d": d, "n": nrm}


def chord_point(fr, u, off):
    """Campus point at distance u along the wall and off in front of its outermost line."""
    d, n = fr["d"], fr["n"]
    v = fr["v"] + off
    return (u * d[0] + v * n[0], u * d[1] + v * n[1])


def name_facades(facades, prefix=""):
    """Give each facade a compass name; add -1, -2 (longest first) when a side has several."""
    by_side = {}
    for f in facades:
        by_side.setdefault(compass_side(f["true_bearing"] if "true_bearing" in f else f["bearing"]), []).append(f)
    for side, fs in by_side.items():
        fs.sort(key=lambda f: -f["length"])
        for i, f in enumerate(fs):
            f["name"] = prefix + side + (f"-{i + 1}" if len(fs) > 1 else "")
    return facades


# ---- photo counts -----------------------------------------------------------

def photo_step(distance, fov_deg=FOV_DEG, overlap=OVERLAP):
    """Sideways step between photos so neighbours overlap by `overlap` at `distance`."""
    width = 2.0 * distance * math.tan(math.radians(fov_deg / 2.0))
    return max(0.5, width * (1.0 - overlap))


def far_distance(height):
    return min(15.0, max(10.0, height))


def needs_far_pass(height, standoff):
    """The guide's extra pass from 10-15 m when the wall is taller than the near view."""
    return height > 1.2 * standoff


def est_photos(length, standoff, height):
    """3 passes x 2 heights x ceil(length / step) + corner turns (+ a far pass for tall walls)."""
    if length <= 0:
        return 0
    per_pass = max(2, math.ceil(length / photo_step(standoff)))
    n = PASSES * HEIGHTS * per_pass + CORNER_SHOTS * HEIGHTS
    if needs_far_pass(height, standoff):
        n += max(2, math.ceil(length / photo_step(far_distance(height))))
    return n


def min_photos(est):
    return 0 if est <= 0 else max(3, math.ceil(MIN_SHARE * est))


def standoff_for(height, space=None):
    """0.8 x wall height, 3..15 m, no farther than the open space in front."""
    d = min(STANDOFF_MAX, max(STANDOFF_MIN, STANDOFF_K * height))
    if space is not None:
        d = min(d, max(STANDOFF_MIN, space))
    return round(d, 1)


# ---- sun and shade ------------------------------------------------------------

def sun_az_el(when_utc, lat=LAT, lon=LON):
    """Sun azimuth (deg from true north, clockwise) and elevation (deg). Accurate to ~0.5 deg."""
    jd = when_utc.timestamp() / 86400.0 + 2440587.5
    n = jd - 2451545.0
    L = (280.460 + 0.9856474 * n) % 360
    g = math.radians((357.528 + 0.9856003 * n) % 360)
    lam = math.radians(L + 1.915 * math.sin(g) + 0.020 * math.sin(2 * g))
    eps = math.radians(23.439 - 0.0000004 * n)
    ra = math.atan2(math.cos(eps) * math.sin(lam), math.cos(lam))
    dec = math.asin(math.sin(eps) * math.sin(lam))
    gmst = (18.697374558 + 24.06570982441908 * n) % 24
    ha = math.radians((gmst * 15 + lon) % 360) - ra
    la = math.radians(lat)
    el = math.asin(math.sin(la) * math.sin(dec) + math.cos(la) * math.cos(dec) * math.cos(ha))
    az = math.atan2(-math.sin(ha), math.tan(dec) * math.cos(la) - math.sin(la) * math.cos(ha))
    return math.degrees(az) % 360, math.degrees(el)


def _hm(t):
    return t.strftime("%H:%M")


def best_time(bearing_true, date, step_min=5):
    """When a facade facing `bearing_true` is in shade (sun below the horizon or behind the wall plane)."""
    day = dt.datetime(date.year, date.month, date.day, tzinfo=TZ)
    samples = []
    for k in range(0, 24 * 60, step_min):
        t = day + dt.timedelta(minutes=k)
        az, el = sun_az_el(t.astimezone(dt.timezone.utc))
        if el > 0:
            lit = math.cos(math.radians(az - bearing_true)) > 0
            samples.append((t, az, el, not lit))
    if not samples:
        return {"summary": "any time; overcast: any time", "shade": [], "morning_ok": True,
                "afternoon_ok": True, "date": date.isoformat()}
    noon = max(samples, key=lambda s: s[2])[0]
    windows, cur = [], None
    for t, az, el, shade in samples:
        if shade and cur is None:
            cur = [t, t]
        elif shade:
            cur[1] = t
        elif cur is not None:
            windows.append(cur)
            cur = None
    if cur is not None:
        windows.append(cur)
    am = [s for s in samples if s[0] <= noon]
    pm = [s for s in samples if s[0] > noon]
    am_share = sum(s[3] for s in am) / max(1, len(am))
    pm_share = sum(s[3] for s in pm) / max(1, len(pm))
    rise, sset = samples[0][0], samples[-1][0]
    if all(s[3] for s in samples):
        text = "any time (in shade all day this season)"
    elif not windows:
        low = [s for s in samples if s[2] < 15]
        early = [s[0] for s in low if s[0] < noon]
        late = [s[0] for s in low if s[0] > noon]
        parts = []
        if early:
            parts.append("before " + _hm(early[-1]))
        if late:
            parts.append("after " + _hm(late[0]))
        text = ("sunlit all day this season: best on an overcast day; on a sunny day keep the sun behind you"
                + (" or go when it is low (" + " / ".join(parts) + ")" if parts else ""))
    else:
        spans = []
        for a, b in windows:
            if a == rise:
                spans.append(f"sunrise to {_hm(b)}")
            elif b == sset:
                spans.append(f"{_hm(a)} to sunset")
            else:
                spans.append(f"{_hm(a)}-{_hm(b)}")
        if am_share >= 0.6 and pm_share >= 0.6:
            when = "most of the day"
        elif am_share >= 0.6:
            when = "mornings"
        elif pm_share >= 0.6:
            when = "afternoons"
        else:
            when = "a short window"
        text = f"{when}: in shade " + ", ".join(spans)
    return {"summary": text + "; overcast: any time", "shade": [[_hm(a), _hm(b)] for a, b in windows],
            "morning_ok": am_share >= 0.6, "afternoon_ok": pm_share >= 0.6,
            "sunrise": _hm(rise), "sunset": _hm(sset), "solar_noon": _hm(noon), "date": date.isoformat()}


def flat_time(date, what="ground"):
    """best_time for things without a facing (plazas, paths, swatches)."""
    text = {"ground": "any time; even light (overcast or full shade) is best for ground and swatches",
            "swatch": "overcast, or a fully shaded patch at any time (no sun spots)",
            "detail": "overcast is best; on a sunny day choose the side in shade"}[what]
    return {"summary": text, "shade": [], "morning_ok": True, "afternoon_ok": True, "date": date.isoformat()}


# ---- loading ------------------------------------------------------------------

def load_buildings(root):
    """All buildings (campus + ring) from the newest footprint index."""
    v2 = root / "derived" / "buildings" / "v2" / "index.json"
    src = v2 if v2.exists() else root / "derived" / "buildings" / "v1" / "footprints.json"
    doc = load_json(src)
    blds = doc["buildings"] if isinstance(doc, dict) else doc
    return src, blds


def load_model(root, bid):
    for v in ("v2", "v1"):
        p = root / "buildings" / bid / v / "model.json"
        if p.exists():
            return load_json(p)
    return None


def multipolygon(coords):
    """footprints.json polygon ([[outer, holes...], ...]) -> shapely (Multi)Polygon, oriented."""
    from shapely.geometry import MultiPolygon, Polygon
    from shapely.geometry.polygon import orient
    polys = []
    for rings in coords:
        if rings and len(rings[0]) >= 3:
            p = Polygon(rings[0], rings[1:]).buffer(0)
            if not p.is_empty:
                polys.extend(p.geoms if p.geom_type == "MultiPolygon" else [p])
    polys = [orient(p, 1.0) for p in polys]
    return polys[0] if len(polys) == 1 else MultiPolygon(polys)


def polys_of(geom):
    if geom is None or geom.is_empty:
        return []
    if geom.geom_type == "Polygon":
        return [geom]
    return [g for g in getattr(geom, "geoms", []) if g.geom_type == "Polygon"]


class Grid:
    """Rasters on a 0.5 m grid for quick lookups: buildings, walkable ground, asphalt, restricted, ..."""

    def __init__(self, bounds, cell=CELL):
        x0, y0, x1, y1 = bounds
        self.cell = cell
        self.x0, self.y1 = x0, y1
        self.W = int(math.ceil((x1 - x0) / cell))
        self.H = int(math.ceil((y1 - y0) / cell))
        self.layers = {}

    def transform(self):
        from rasterio.transform import Affine
        return Affine(self.cell, 0, self.x0, 0, -self.cell, self.y1)

    def burn(self, name, shapes, dtype="uint8", all_touched=False):
        from rasterio.features import rasterize
        shapes = [(g, v) for g, v in shapes if g is not None and not g.is_empty]
        if not shapes:
            self.layers[name] = np.zeros((self.H, self.W), dtype)
            return
        self.layers[name] = rasterize(shapes, out_shape=(self.H, self.W), transform=self.transform(),
                                      fill=0, dtype=dtype, all_touched=all_touched)

    def sample(self, name, xs, ys):
        a = self.layers[name]
        c = np.floor((np.asarray(xs) - self.x0) / self.cell).astype(int)
        r = np.floor((self.y1 - np.asarray(ys)) / self.cell).astype(int)
        ok = (c >= 0) & (c < self.W) & (r >= 0) & (r < self.H)
        out = np.zeros(np.shape(xs), a.dtype)
        out[ok] = a[r[ok], c[ok]]
        return out


def material_index(mid):
    return {m["id"]: m["index"] for m in mid["ids"]}


def load_surface_rasters(root, grid):
    """Terrain surface rasters (0.25 m, one per tile, row 0 = south) resampled onto the 0.5 m grid.

    These include the LiDAR-classified ground between mapped features (lawns, plazas), which the
    surfaces.geojson polygons leave out. 255 = no tile.
    """
    import frame as fr
    out = np.full((grid.H, grid.W), 255, np.uint8)
    step = max(1, int(round(grid.cell / 0.25)))
    for p in sorted((root / "derived" / "terrain" / "v1").glob("T_*/surface.npy")):
        try:
            xmin, ymin, xmax, ymax = fr.tile_bounds(p.parent.name)
        except ValueError:
            continue
        a = np.load(p)[::step, ::step][::-1]          # north row first, like the grid
        c0 = int(round((xmin - grid.x0) / grid.cell))
        r0 = int(round((grid.y1 - ymax) / grid.cell))
        h, w = a.shape
        rs, cs = max(0, r0), max(0, c0)
        re, ce = min(grid.H, r0 + h), min(grid.W, c0 + w)
        if re > rs and ce > cs:
            out[rs:re, cs:ce] = a[rs - r0:re - r0, cs - c0:ce - c0]
    return out


class OSM:
    """The OSM snapshot in campus meters."""

    def __init__(self, root):
        import surfaces
        self.snap, self.elements, self.nodes, self.ways, self.rels, self.xy = surfaces.load_osm(root)
        self._wl, self._wp, self._rp = surfaces.way_line, surfaces.way_polygon, surfaces.relation_polygon

    def line(self, way):
        from shapely.geometry import LineString
        c = self._wl(way, self.xy)
        return LineString(c) if len(c) >= 2 else None

    def area(self, el):
        if el["type"] == "way":
            return self._wp(el, self.xy)
        if el["type"] == "relation":
            return self._rp(el, self.ways, self.xy)
        return None

    def point(self, node):
        from shapely.geometry import Point
        p = self.xy.get(node["id"])
        return Point(p) if p else None

    def tagged(self, test):
        for el in self.elements:
            t = el.get("tags")
            if t and test(t):
                yield el

    def element(self, ref):
        kind, num = ref.split("/")
        return {"way": self.ways, "relation": self.rels, "node": self.nodes}[kind].get(int(num))


# ---- context: everything the classifier needs -------------------------------

class Context:
    def __init__(self, root, date):
        import shapely
        from shapely.geometry import shape
        from shapely.ops import unary_union
        import frame as fr
        self.root, self.date = root, date
        self.fr = fr
        self.conv = fr.load_frame()["horizontal"]["meridian_convergence_deg"]
        self.src, all_b = load_buildings(root)
        self.osm = OSM(root)
        bp = fr.boundary_polygons()
        self.campus = bp["campus"]
        self.buildings = [b for b in all_b if b.get("zone") == "campus"]
        self.others = all_b
        self.geom = {b["id"]: multipolygon(b["polygon"]) for b in all_b}
        self.models = {b["id"]: load_model(root, b["id"]) for b in self.buildings}
        self.tags = {}
        for b in all_b:
            el = self.osm.element(b["osm"]) if b.get("osm") else None
            self.tags[b["id"]] = dict(el.get("tags", {})) if el else dict(b.get("osm_tags") or {})
        xmin, ymin, xmax, ymax = self.campus.bounds
        # Tile-aligned grid (100 m) so the terrain surface rasters drop straight in.
        self.grid = Grid((math.floor(xmin / 100 - 1) * 100, math.floor(ymin / 100 - 1) * 100,
                          math.ceil(xmax / 100 + 1) * 100, math.ceil(ymax / 100 + 1) * 100))
        g = self.grid
        self.bindex = {b["id"]: i + 1 for i, b in enumerate(all_b)}
        self.bid_of = {i + 1: b["id"] for i, b in enumerate(all_b)}
        g.burn("bld", [(self.geom[b["id"]], self.bindex[b["id"]]) for b in all_b], "int32")
        surf = load_json(root / "derived" / "terrain" / "v1" / "surfaces.geojson")
        self.surfaces = {f["properties"]["material"]: shape(f["geometry"]) for f in surf["features"]}
        g.layers["surf"] = load_surface_rasters(root, g)
        mid = load_json(CONFIG / "material_ids.json")
        self.mat_index = material_index(mid)
        codes = [self.mat_index[m] for m in ("grass", "paving_brick", "paving_concrete") if m in self.mat_index]
        g.layers["walk"] = np.isin(g.layers["surf"], codes).astype("uint8")
        g.layers["asphalt"] = (g.layers["surf"] == self.mat_index.get("asphalt", 255)).astype("uint8")
        g.burn("campus", [(self.campus, 1)])
        # OSM: restricted areas, fences, hospital areas, footways.
        restricted, fences, hospital = [], [], []
        for el in self.osm.elements:
            t = el.get("tags") or {}
            if not t or el["type"] == "node":
                continue
            area = None
            if t.get("access") in ("private", "no", "permit") or t.get("landuse") == "construction" \
                    or t.get("leisure") == "stadium" or t.get("barrier") in ("fence",):
                area = self.osm.area(el)
                if area is not None and "building" not in t:
                    restricted.append(area)
            if el["type"] == "way" and "highway" in t and t.get("access") in ("private", "no"):
                ln = self.osm.line(el)
                if ln is not None:
                    restricted.append(ln.buffer(3.0))      # private service road / yard
            if t.get("barrier") in ("fence", "hedge") and el["type"] == "way":
                ln = self.osm.line(el)
                if ln is not None:
                    fences.append(ln.buffer(0.35))
            if (t.get("amenity") in CLINICAL_AMENITY or t.get("healthcare") in ("hospital", "clinic")) \
                    and "building" not in t:
                a = self.osm.area(el)
                if a is not None:
                    hospital.append(a)
        self.restricted = unary_union(restricted) if restricted else None
        self.hospital = unary_union(hospital) if hospital else None
        g.burn("restricted", [(a, 1) for a in restricted])
        g.burn("fence", [(f, 1) for f in fences], all_touched=True)
        # Trees.
        trees = load_json(root / "derived" / "trees" / "v1" / "trees.json")["trees"]
        tr = [t for t in trees if t.get("kind") == "tree"]
        self.trees = np.array([[t["x"], t["y"], t.get("crown_diameter", 4.0) / 2] for t in tr]) \
            if tr else np.zeros((0, 3))
        self.hero = load_json(CONFIG / "hero_spots.json")
        self.unary = unary_union
        self.shapely = shapely

    def true_bearing(self, grid_bearing):
        return (grid_bearing + self.conv) % 360.0

    def tile(self, x, y):
        return self.fr.tile_of(x, y)


# ---- building classification ----------------------------------------------------

def building_class(ctx, b):
    """'clinical', 'parking', 'utility' or None, with a reason."""
    t = ctx.tags.get(b["id"], {})
    name = (b.get("name") or t.get("name") or "").lower()
    if t.get("building") == "hospital" or t.get("amenity") in CLINICAL_AMENITY or "healthcare" in t \
            or t.get("emergency") == "yes":
        return "clinical", "hospital/clinical tags in OSM"
    if ctx.hospital is not None:
        from shapely.geometry import Point
        if ctx.hospital.contains(Point(b["centroid"])):
            return "clinical", "inside an OSM hospital area"
    if t.get("building") == "parking" or t.get("amenity") == "parking" or "garage" in name or "parking" in name:
        return "parking", "parking structure"
    if t.get("building") in UTILITY_BUILDINGS or any(w in name for w in UTILITY_WORDS):
        return "utility", "utility/service building"
    return None, ""


def part_polys(model):
    out = []
    for p in (model or {}).get("parts", []):
        try:
            g = multipolygon(p["polygon"]) if isinstance(p["polygon"][0][0][0], list) else None
        except (TypeError, IndexError, KeyError):
            g = None
        if g is None:
            try:
                from shapely.geometry import Polygon
                g = Polygon(p["polygon"][0])
            except Exception:  # noqa: BLE001 - odd part outlines are skipped
                g = None
        if g is not None and not g.is_empty:
            out.append((p, g))
    return out


def building_height(b, model):
    """Main wall height (m above ground): the largest part's eave, else Metro height, else 10 m."""
    parts = (model or {}).get("parts", [])
    if parts:
        big = max(parts, key=lambda p: p.get("area_m2", 0))
        h = big.get("eave_height_m")
        if h is None and big.get("eave_z") is not None:
            h = big["eave_z"] - big.get("ground_z", model.get("ground_z", 0))
        if h:
            return max(2.5, float(h))
    if b.get("metro_height_m"):
        return float(b["metro_height_m"])
    return 10.0


def facade_height(line, parts, default):
    """Highest eave among the model parts that run along this facade."""
    if not parts:
        return default
    zone = line.buffer(1.5)
    best = None
    for p, g in parts:
        try:
            overlap = g.boundary.intersection(zone).length
        except Exception:  # noqa: BLE001
            continue
        if overlap >= 0.25 * line.length:
            h = p.get("eave_height_m")
            if h is None and p.get("eave_z") is not None:
                h = p["eave_z"] - p.get("ground_z", 0)
            if h is not None:
                best = h if best is None else max(best, h)
    return max(2.5, float(best)) if best else default


# ---- facade analysis ---------------------------------------------------------------

def analyse_facade(ctx, b, f, height, own):
    """Rays straight out from the wall: free space, ground types, blocking buildings."""
    g = ctx.grid
    fr = facade_frame(f)
    L = fr["u1"] - fr["u0"]
    ns = max(3, int(math.ceil(L / 2.0)) + 1)
    us = np.linspace(fr["u0"] + 0.3, fr["u1"] - 0.3, ns) if L > 0.6 else np.array([(fr["u0"] + fr["u1"]) / 2])
    steps = np.arange(0.75, VIEW_M + 0.01, 0.5)
    d, n = fr["d"], fr["n"]
    U, S = np.meshgrid(us, steps, indexing="ij")
    X = U * d[0] + (fr["v"] + S) * n[0]
    Y = U * d[1] + (fr["v"] + S) * n[1]
    bld = g.sample("bld", X, Y)
    fence = g.sample("fence", X, Y)
    walk = g.sample("walk", X, Y)
    asph = g.sample("asphalt", X, Y)
    restr = g.sample("restricted", X, Y)
    hero = g.sample("hero", X, Y)
    obstacle = (bld != 0) | (fence != 0)
    first = np.where(obstacle.any(axis=1), obstacle.argmax(axis=1), len(steps))
    free = np.where(first < len(steps), steps[np.minimum(first, len(steps) - 1)], VIEW_M + 1)
    visible = np.arange(len(steps))[None, :] < first[:, None]
    hit_bld = np.where(first < len(steps), bld[np.arange(ns), np.minimum(first, len(steps) - 1)], 0)
    near = free < BLOCKED_M
    blockers = {}
    for k in np.where(near)[0]:
        bid = ctx.bid_of.get(int(hit_bld[k]))
        if bid:
            blockers[bid] = blockers.get(bid, 0) + 1
    pub = (visible & (walk != 0) & (restr == 0)).any(axis=1)
    stop = np.minimum(first, len(steps) - 1)
    fenced = (first < len(steps)) & (fence[np.arange(ns), stop] != 0)
    res = (visible & (restr != 0)).any(axis=1) | fenced
    asp = (visible & (asph != 0) & (restr == 0)).any(axis=1)
    heroes = hero[visible]
    hero_ids = {int(h) for h in np.unique(heroes) if h}
    space = float(np.median(free)) - 0.5
    # Camera path along the chord at the stand-off distance, kept where the ground is walkable.
    standoff = standoff_for(height, space)
    up = np.arange(fr["u0"], fr["u1"] + 1e-6, 1.0) if L >= 1 else np.array([fr["u0"], fr["u1"]])
    px = up * d[0] + (fr["v"] + standoff) * n[0]
    py = up * d[1] + (fr["v"] + standoff) * n[1]
    on_walk = (g.sample("walk", px, py) != 0) & (g.sample("bld", px, py) == 0) & (g.sample("restricted", px, py) == 0)
    on_asph = (g.sample("asphalt", px, py) != 0) & (g.sample("bld", px, py) == 0)
    ok = on_walk if on_walk.mean() >= 0.3 or not on_asph.any() else (on_walk | on_asph)
    path = []
    if ok.any():
        i0, i1 = int(np.argmax(ok)), int(len(ok) - 1 - np.argmax(ok[::-1]))
        path = [[round(float(px[i0]), 2), round(float(py[i0]), 2)], [round(float(px[i1]), 2), round(float(py[i1]), 2)]]
    # Trees between the camera path and the wall.
    trees = 0
    if len(ctx.trees):
        T = ctx.trees
        rel = T[:, :2]
        tu = rel @ np.array(d)
        tv = rel @ np.array(n) - fr["v"]
        m = (tu > fr["u0"] - 2) & (tu < fr["u1"] + 2) & (tv > -T[:, 2]) & (tv < standoff + 1 + T[:, 2])
        trees = int(m.sum())
    return {
        "frame": fr, "samples": ns, "blocked_share": float(near.mean()), "blockers": blockers,
        "public_share": float(pub.mean()), "restricted_any": bool(res.any()), "asphalt_any": bool(asp.any()),
        "asphalt_share": float(asp.mean()),
        "hero_areas": sorted(hero_ids), "space": space, "standoff": standoff, "camera_path": path,
        "path_walk_share": float(on_walk.mean()) if len(on_walk) else 0.0,
        "path_on_asphalt": bool(ok.any() and not on_walk.mean() >= 0.3 and on_asph.any()),
        "trees": trees, "own_blocked": [k for k in blockers if k == own],
    }


def facade_access(ctx, info, bclass):
    notes = []
    blocked = info["blocked_share"] >= 0.5
    if blocked:
        who = sorted(info["blockers"].items(), key=lambda kv: -kv[1])
        names = []
        for bid, _ in who[:2]:
            nm = next((b.get("name") for b in ctx.others if b["id"] == bid), None)
            names.append(("its own wing" if bid in info["own_blocked"] else (nm or bid)))
        notes.append("another building is under 3 m in front (" + ", ".join(names) + "): not photographable")
        return "blocked", notes
    if info["blocked_share"] >= 0.2:
        notes.append(f"{round(100 * info['blocked_share'])}% of the wall has a building under 3 m in front")
    if bclass == "clinical":
        notes.append("hospital/clinical frontage: stay on public sidewalks, no entrances or patients in frame; "
                     "background priority")
        return "restricted", notes
    if info["public_share"] >= 0.3:
        notes.append("public ground in front (sidewalk/path/lawn)")
        if info["restricted_any"]:
            notes.append("part of the view crosses a fenced or private area")
        return "public", notes
    if info["asphalt_any"] and info["asphalt_share"] >= 0.3:
        notes.append("faces a road, drive or parking lot (no sidewalk mapped): stand at its edge, watch traffic")
        if info["restricted_any"]:
            notes.append("part of the view crosses a fenced or private area")
        return "public", notes
    if info["restricted_any"]:
        notes.append("faces only a fenced, private or construction area; shoot only from public ground")
    else:
        notes.append("no mapped walkable ground in front (planting beds or unmapped); check on site")
    return "restricted", notes


def framing_text(name, side, length, height, standoff, info, far):
    w = 2 * standoff * math.tan(math.radians(FOV_DEG / 2))
    step = photo_step(standoff)
    txt = (f"Stand about {standoff:g} m out from the {side} wall of {name}, phone square to it; each photo shows "
           f"about {w:.0f} m of wall. Walk the full {length:.0f} m, one photo every {step:.1f} m "
           f"(about {max(1, round(step / 0.7))} step{'s' if round(step / 0.7) > 1 else ''}): straight, then "
           f"angled both ways, at chest and overhead height. Include the ground strip and the corners "
           f"(slow 5-8 shot arcs).")
    if far:
        txt += (f" The wall is {height:.0f} m tall: add one pass from {far_distance(height):.0f} m back so the "
                "roof edge is in frame.")
    if info["trees"]:
        txt += f" {info['trees']} tree(s) stand between path and wall: add angled shots around them."
    if info["path_on_asphalt"]:
        txt += " The path runs on asphalt (parking/drive): keep to the edge."
    if not info["camera_path"]:
        txt += " No walkable ground at the stand-off line: shoot from the nearest public point."
    return txt


# ---- hero areas (approximate where OSM has no outline) ---------------------------

def hero_areas(ctx):
    """Named hero areas: OSM polygons where they exist, else approximate shapes (flagged)."""
    from shapely.geometry import Point
    areas = []
    spots = {s["spot"]: s for s in ctx.hero["spots"]}

    def osm_area(ref):
        el = ctx.osm.element(ref)
        return ctx.osm.area(el) if el else None

    for spot, ref in (("Alumni Lawn", ALUMNI_LAWN), ("Library Lawn", spots["Library Lawn"]["match"]["osm"])):
        a = osm_area(ref)
        if a is not None:
            areas.append({"name": spot, "geom": a, "approx": False, "source": ref,
                          "how": "OSM polygon " + ref})
    def grass_near(xy, radius):
        circle = Point(xy).buffer(radius)
        parts = [p for p in polys_of(raster_grass(ctx, xy, radius).intersection(circle)) if p.area > 50]
        if not parts:
            return Point(xy).buffer(radius / 2)
        best = min(parts, key=lambda p: p.distance(Point(xy)) - 1e-6 * p.area)
        near = [p for p in parts if p.distance(best) < 6]
        return ctx.unary(near).convex_hull.difference(ctx.unary([ctx.geom[b["id"]] for b in ctx.buildings
                                                                   if ctx.geom[b["id"]].intersects(circle)]))

    pe = spots["Peabody Esplanade"]["match"]
    areas.append({"name": "Peabody Esplanade", "geom": grass_near(pe["centroid"], 150), "approx": True,
                  "source": pe["osm"], "how": "approximate: lawn from the terrain surface map within 150 m of OSM "
                  "node " + pe["osm"] + ", hull minus buildings"})
    cl = next(c for c in spots["Commons"]["candidates"] if c["name"] == "Commons Center Lawn")
    areas.append({"name": "Commons Center Lawn", "geom": grass_near(cl["centroid"], 80), "approx": True,
                  "source": cl["osm"], "how": "approximate: lawn within 80 m of OSM node " + cl["osm"]})
    members = [ctx.geom[m["building_id"]] for m in spots["Branscomb Quad"]["members"] if m["building_id"] in ctx.geom]
    if members:
        hull = ctx.unary(members).convex_hull
        quad = hull.difference(ctx.unary(members).buffer(1.0))
        areas.append({"name": "Branscomb Quad", "geom": quad, "approx": True, "source": "group",
                      "how": "approximate: convex hull of Vaughn, Lupton, Scales, Stapleton and Branscomb Quad "
                      "Market minus their footprints"})
    return areas


def raster_grass(ctx, xy, radius):
    """Lawn polygons (terrain surface raster, buildings removed) in a square around xy."""
    from rasterio.features import shapes
    from rasterio.transform import Affine
    from shapely.geometry import shape
    g = ctx.grid
    c0 = max(0, int((xy[0] - radius - g.x0) / g.cell))
    c1 = min(g.W, int((xy[0] + radius - g.x0) / g.cell) + 1)
    r0 = max(0, int((g.y1 - (xy[1] + radius)) / g.cell))
    r1 = min(g.H, int((g.y1 - (xy[1] - radius)) / g.cell) + 1)
    win = (g.layers["surf"][r0:r1, c0:c1] == ctx.mat_index["grass"]) & (g.layers["bld"][r0:r1, c0:c1] == 0)
    tr = Affine(g.cell, 0, g.x0 + c0 * g.cell, 0, -g.cell, g.y1 - r0 * g.cell)
    polys = [shape(s) for s, v in shapes(win.astype("uint8"), mask=win, transform=tr) if v == 1]
    return ctx.unary(polys)


def rand_wall_area(ctx, rand_facades):
    """Rand Wall: a strip in front of the Rand Hall facade with the most footway in front (approximate)."""
    from shapely.geometry import Polygon
    if not rand_facades:
        return None
    walk = ctx.footway_lines

    def score(f):
        fr = f["_frame"]
        strip = Polygon([chord_point(fr, fr["u0"], 0), chord_point(fr, fr["u1"], 0),
                         chord_point(fr, fr["u1"], 15), chord_point(fr, fr["u0"], 15)])
        return sum(w.intersection(strip).length for w in walk if w.intersects(strip)), strip

    best = max(rand_facades, key=lambda f: score(f)[0])
    fr = best["_frame"]
    strip = Polygon([chord_point(fr, fr["u0"], 1.0), chord_point(fr, fr["u1"], 1.0),
                     chord_point(fr, fr["u1"], 6.0), chord_point(fr, fr["u0"], 6.0)])
    return {"name": "Rand Wall", "geom": strip, "approx": True, "source": "Rand Hall " + best["name"],
            "how": f"approximate: 1-6 m strip along Rand Hall's {best['name']} facade, the side with the most "
                   "OSM footway in front; confirm the seating wall on site"}


# ---- builders for each entry kind ---------------------------------------------------

def r2(p):
    return [round(float(p[0]), 2), round(float(p[1]), 2)]


def line_coords(geom):
    return [r2(c) for c in geom.coords]


def base_entry(ctx, eid, kind, method, priority, centerline, anchor, best, access, notes, framing, **extra):
    e = {"id": eid, "kind": kind, "building": None, "facade": None, "priority": priority, "method": method,
         "est_photos": 0, "min_photos": 0, "centerline": centerline, "normal": None, "bearing_deg": None,
         "standoff_m": None, "camera_path": [], "tile": ctx.tile(*anchor), "best_time": best,
         "access": access, "access_notes": "; ".join(notes), "example_framing": framing, "day": None}
    e.update(extra)
    return e


def walk_photos(length):
    """Phone-only fallback for a 360 walk: one photo every 1.5 m, two passes."""
    return max(4, 2 * math.ceil(length / 1.5))


def build_facades(ctx):
    from shapely.geometry import LineString, Point
    entries, summary, courtyards, by_building = [], [], [], {}
    hero_bids = set()
    for s in ctx.hero["spots"]:
        if s.get("match") and s["match"].get("building_id"):
            hero_bids.add(s["match"]["building_id"])
        for m in s.get("members", []):
            hero_bids.add(m["building_id"])
    ctx.hero_bids = hero_bids
    for b in ctx.buildings:
        bid = b["id"]
        geom = ctx.geom[bid]
        model = ctx.models.get(bid)
        parts = part_polys(model)
        h_default = building_height(b, model)
        name = b.get("name") or ctx.tags.get(bid, {}).get("name") or f"unnamed building {bid}"
        bclass, why = building_class(ctx, b)
        facades = []
        for pi, poly in enumerate(polys_of(geom)):
            for f in split_ring(list(poly.exterior.coords)):
                f["ring"] = "outer"
                facades.append(f)
            for hi, hole in enumerate(poly.interiors):
                from shapely.geometry import Polygon
                hp = Polygon(hole)
                if hp.area < MIN_COURTYARD_M2:
                    continue
                cid = f"courtyard/{bid}/{len(courtyards) + 1}"
                courtyards.append({"id": cid, "building": bid, "name": name, "geom": hp})
                cf = split_ring(list(hole.coords))
                tag = "" if sum(1 for c in courtyards if c["building"] == bid) == 1 else \
                    "abcdefghij"[sum(1 for c in courtyards if c["building"] == bid) - 1] + "-"
                for f in cf:
                    f["ring"] = "courtyard"
                    f["prefix"] = "courtyard-" + tag
                    f["courtyard"] = cid
                facades.extend(cf)
        for f in facades:
            f["true_bearing"] = ctx.true_bearing(f["bearing"])
        outer = [f for f in facades if f["ring"] == "outer"]
        inner = [f for f in facades if f["ring"] == "courtyard"]
        name_facades(outer)
        groups = {}
        for f in inner:
            groups.setdefault(f["prefix"], []).append(f)
        for pre, fs in groups.items():
            name_facades(fs, pre)
        counts = {"public": 0, "restricted": 0, "blocked": 0}
        for f in facades:
            line = LineString(f["points"])
            height = facade_height(line, parts, h_default)
            info = analyse_facade(ctx, b, f, height, bid)
            f["_frame"] = info["frame"]
            access, notes = facade_access(ctx, info, bclass)
            mid = line.interpolate(0.5, normalized=True)
            out_pt = Point(mid.x + f["normal"][0] * 30, mid.y + f["normal"][1] * 30)
            prio, pnote = "normal", ""
            if bid in hero_bids:
                prio = "hero"
            elif bclass:
                prio, pnote = "background", why
            elif f["ring"] == "outer" and not ctx.campus.contains(out_pt):
                prio, pnote = "background", "faces out of campus (backdrop-ring side)"
            elif info["hero_areas"]:
                prio = "hero"
                pnote = "faces hero area " + ", ".join(ctx.hero_area_names[i - 1] for i in info["hero_areas"])
            if pnote:
                notes.append(pnote)
            sd = info["standoff"]
            if info["standoff"] < standoff_for(height):
                notes.append(f"stand-off limited to {sd:g} m by the space in front")
            far = needs_far_pass(height, sd)
            est = est_photos(f["length"], sd, height) if access != "blocked" else 0
            side = f["name"].replace("courtyard-", "courtyard ")
            e = base_entry(
                ctx, f"{bid}/{f['name']}", "facade", "photo_set", prio,
                [r2(p) for p in f["points"]], (mid.x, mid.y),
                best_time(f["true_bearing"], ctx.date), access, notes,
                framing_text(name, side, f["length"], height, sd, info, far) if access != "blocked"
                else "Not photographable from outside (another wall is under 3 m away). Skip.",
                building_name=name, length_m=round(f["length"], 2), height_m=round(height, 2),
                area_m2=round(f["length"] * height, 1), trees_in_view=info["trees"],
                public_share=round(info["public_share"], 2), ring=f["ring"], far_pass=far)
            e.update({"building": bid, "facade": f["name"], "normal": r2(f["normal"]),
                      "bearing_deg": round(f["true_bearing"], 1), "standoff_m": sd,
                      "camera_path": info["camera_path"], "est_photos": est, "min_photos": min_photos(est)})
            if f.get("courtyard"):
                e["courtyard"] = f["courtyard"]
            if bid == HERO_ID:
                e["first_capture"] = True
            counts[access] += 1
            entries.append(e)
        by_building[bid] = facades
        summary.append({"id": bid, "name": name, "tile": b.get("tile") or ctx.tile(*b["centroid"]),
                        "class": bclass, "hero": bid in hero_bids, "facades": len(facades), **counts,
                        "height": round(h_default, 1),
                        "est": sum(e["est_photos"] for e in entries if e["building"] == bid)})
    return entries, summary, courtyards, by_building


def build_courtyards(ctx, courtyards, facade_entries):
    out = []
    for c in courtyards:
        g = c["geom"]
        ring = g.exterior
        inner = g.buffer(-2.0)
        path = line_coords(inner.exterior) if not inner.is_empty and inner.geom_type == "Polygon" else line_coords(ring)
        open_path = any(w.crosses(g) or w.within(g) for w in ctx.footway_lines if w.intersects(g))
        prio = "hero" if c["building"] in ctx.hero_bids else "normal"
        access = "public" if open_path else "restricted"
        notes = (["an OSM path leads in"] if open_path else
                 ["enclosed by the building; enter only through an open public passage, never through the building"])
        L = ring.length
        e = base_entry(ctx, c["id"], "courtyard", "360_walk", prio, line_coords(ring),
                       (g.centroid.x, g.centroid.y), flat_time(ctx.date), access, notes,
                       f"Courtyard of {c['name']} ({g.area:.0f} m2): walk one loop about 2 m from the walls "
                       f"({L:.0f} m), then shoot each courtyard facade as its own photo set. Phone only: one "
                       "photo every 2 steps, two loops (chest and overhead).",
                       building_name=c["name"], length_m=round(L, 1), area_m2=round(g.area, 1))
        e.update({"building": c["building"], "camera_path": path,
                  "est_photos": walk_photos(L), "min_photos": min_photos(walk_photos(L))})
        if c["building"] == HERO_ID:
            e["first_capture"] = True
        out.append(e)
    return out


def build_plazas(ctx, areas):
    """Named lawns, gardens, squares and pedestrian areas on campus, plus the hero areas."""
    from shapely.geometry import Point
    cand = []
    for el in ctx.osm.elements:
        t = el.get("tags") or {}
        if el["type"] == "node" or not t or "building" in t:
            continue
        kind = None
        if t.get("leisure") in ("park", "garden") or t.get("landuse") == "grass" or t.get("place") == "square":
            kind = "lawn" if t.get("leisure") != "garden" else "garden"
        elif t.get("highway") == "pedestrian" and t.get("area") == "yes" or t.get("area:highway") == "pedestrian":
            kind = "plaza"
        if not kind:
            continue
        a = ctx.osm.area(el)
        if a is None or a.is_empty or not ctx.campus.contains(a.representative_point()):
            continue
        if a.area < 400 and not t.get("name"):
            continue
        cand.append((f"{el['type']}/{el['id']}", t.get("name"), kind, a, t))
    hero_geoms = [(h["name"], h["geom"]) for h in areas]
    out, kept = [], []
    approx = [("approx/" + h["name"].lower().replace(" ", "-"), h["name"], "lawn", h["geom"], {"approx": True})
              for h in areas if h["approx"] and h["name"] != "Rand Wall" and not h["geom"].is_empty]
    for ref, name, kind, a, t in approx + cand:   # approximate hero areas first so their ids are stable
        if any(a.intersection(k).area > 0.8 * min(a.area, k.area) for k in kept):
            continue
        kept.append(a)
        poly = max(polys_of(a), key=lambda p: p.area)
        L = poly.exterior.length
        inner = poly.buffer(-2.0)
        inner = max(polys_of(inner), key=lambda p: p.area) if polys_of(inner) else poly
        hero_hit = [hn for hn, hg in hero_geoms if hg.intersection(a).area > 0.3 * min(a.area, hg.area)]
        prio = "hero" if hero_hit else "normal"
        if ctx.hospital is not None and ctx.hospital.contains(a.representative_point()):
            prio = "background"
        access = "restricted" if t.get("access") in ("private", "no") else "public"
        title = name or f"{kind} near {nearest_name(ctx, poly.centroid)}"
        notes = ["open ground"] if access == "public" else ["access tagged private in OSM: view from outside"]
        if t.get("approx"):
            notes.append("outline is approximate (no OSM outline); see SHOT-LIST.md")
        e = base_entry(ctx, "plaza/" + ref.replace("/", "-"), "plaza", "360_walk", prio,
                       [r2(c) for c in poly.exterior.coords], (poly.centroid.x, poly.centroid.y),
                       flat_time(ctx.date), access, notes,
                       f"{title} ({a.area:.0f} m2): walk a loop about 2 m inside the edge ({L:.0f} m), then cross "
                       "the middle in overlapping lines; end where you started. Phone only: one photo every 2 "
                       "steps facing outward to the buildings, then inward.",
                       name=title, area_type=kind, length_m=round(L, 1), area_m2=round(a.area, 1),
                       approx=bool(t.get("approx")))
        e["camera_path"] = [r2(c) for c in inner.exterior.coords]
        e["est_photos"] = walk_photos(L)
        e["min_photos"] = min_photos(e["est_photos"])
        if ref == ALUMNI_LAWN:
            e["first_capture"] = True
        out.append(e)
    return out


def nearest_name(ctx, pt):
    best, bd = "campus", 1e18
    for b in ctx.buildings:
        if b.get("name"):
            d = (b["centroid"][0] - pt.x) ** 2 + (b["centroid"][1] - pt.y) ** 2
            if d < bd:
                best, bd = b["name"], d
    return best


def footway_ways(ctx):
    for w in ctx.osm.ways.values():
        t = w.get("tags") or {}
        if t.get("highway") in ("footway", "pedestrian", "path", "cycleway", "living_street") and t.get("area") != "yes":
            yield w


def build_paths(ctx, areas):
    """OSM footway network on campus, cut into ~150 m legs (greedy straightest trails)."""
    from shapely.geometry import LineString, Point
    adj, coords, edges = {}, {}, set()
    for w in footway_ways(ctx):
        ns = [n for n in w["nodes"] if n in ctx.osm.xy]
        for a, b in zip(ns, ns[1:]):
            if a == b:
                continue
            pa, pb = ctx.osm.xy[a], ctx.osm.xy[b]
            if not ctx.campus.contains(Point((pa[0] + pb[0]) / 2, (pa[1] + pb[1]) / 2)):
                continue
            key = (min(a, b), max(a, b))
            if key in edges:
                continue
            edges.add(key)
            coords[a], coords[b] = pa, pb
            adj.setdefault(a, set()).add(b)
            adj.setdefault(b, set()).add(a)
    unvisited = set(edges)

    def free_deg(n):
        return sum(1 for m in adj[n] if (min(n, m), max(n, m)) in unvisited)

    legs = []
    order = sorted(adj)
    while unvisited:
        odd = [n for n in order if free_deg(n) % 2 == 1]
        start = odd[0] if odd else next(n for n in order if free_deg(n) > 0)
        cur, prev_dir, length, pts = start, None, 0.0, [coords[start]]
        while length < LEG_M:
            opts = [m for m in adj[cur] if (min(cur, m), max(cur, m)) in unvisited]
            if not opts:
                break

            def turn(m):
                v = (coords[m][0] - coords[cur][0], coords[m][1] - coords[cur][1])
                return 0 if prev_dir is None else _angle_diff(_dir_deg(v), prev_dir)

            m = min(opts, key=lambda m: (turn(m), m))
            unvisited.discard((min(cur, m), max(cur, m)))
            v = (coords[m][0] - coords[cur][0], coords[m][1] - coords[cur][1])
            prev_dir = _dir_deg(v)
            length += math.hypot(*v)
            pts.append(coords[m])
            cur = m
        if length >= 25.0:
            legs.append(LineString(pts))
    hero_zone = ctx.unary([h["geom"].buffer(15) for h in areas] +
                          [ctx.geom[b].buffer(20) for b in ctx.hero_bids if b in ctx.geom])
    edge = ctx.campus.exterior
    out = []
    for i, ln in enumerate(legs):
        mid = ln.interpolate(0.5, normalized=True)
        if ln.intersection(hero_zone).length >= 0.5 * ln.length:
            prio = "hero"
        elif edge.distance(mid) < 20 or (ctx.hospital is not None and ctx.hospital.contains(mid)):
            prio = "background"
        else:
            prio = "normal"
        L = ln.length
        near = nearest_name(ctx, mid)
        e = base_entry(ctx, f"path/P{i + 1:04d}", "path", "360_walk", prio, line_coords(ln.simplify(0.5)),
                       (mid.x, mid.y), flat_time(ctx.date), "public", ["public footway (OSM)"],
                       f"Path leg near {near} ({L:.0f} m): 360 camera at 1 m/s, two passes (stick low, then high), "
                       "end where you started. Phone only: one photo every 2-3 steps along the path, looking ahead, "
                       "so neighbouring facade sets link up.",
                       name=f"path leg near {near}", length_m=round(L, 1))
        e["camera_path"] = e["centerline"]
        e["est_photos"] = max(4, math.ceil(L / 2.0))
        e["min_photos"] = min_photos(e["est_photos"])
        if prio == "hero" and any(h["name"] == "Alumni Lawn" and ln.intersection(h["geom"].buffer(15)).length
                                  >= 0.5 * L for h in areas):
            e["first_capture"] = True
        out.append(e)
    return out


def build_stairs(ctx):
    from shapely.geometry import Point
    out = []
    hero_zone = ctx.unary([ctx.geom[b].buffer(15) for b in ctx.hero_bids if b in ctx.geom] +
                          [h["geom"].buffer(10) for h in ctx.hero_area_list])
    for w in ctx.osm.ways.values():
        t = w.get("tags") or {}
        if t.get("highway") != "steps":
            continue
        ln = ctx.osm.line(w)
        if ln is None or not ctx.campus.contains(ln.interpolate(0.5, normalized=True)):
            continue
        mid = ln.interpolate(0.5, normalized=True)
        prio = "hero" if ln.intersects(hero_zone) else (
            "background" if ctx.hospital is not None and ctx.hospital.contains(mid) else "normal")
        access = "restricted" if t.get("access") in ("private", "no") else "public"
        est = 24 + 2 * math.ceil(ln.length)
        near = nearest_name(ctx, mid)
        e = base_entry(ctx, f"stairs/way-{w['id']}", "stairs", "detail_set", prio, line_coords(ln),
                       (mid.x, mid.y), flat_time(ctx.date, "detail"), access,
                       [f"OSM steps{', ' + str(t['step_count']) + ' steps' if t.get('step_count') else ''}"],
                       f"Steps near {near} ({ln.length:.0f} m run): slow arc of 10-20 shots from the bottom and the "
                       "top, one shot per step along each side, plus 3-4 wide shots that show the steps and the wall "
                       "or path around them. Include railings and the top and bottom landings.",
                       name=f"steps near {near}", length_m=round(ln.length, 1))
        e["camera_path"] = e["centerline"]
        e["est_photos"] = est
        e["min_photos"] = min_photos(est)
        out.append(e)
    return out


def detail_entry(ctx, eid, name, xy, prio, framing, est, notes, building=None, first=False, centerline=None,
                 kind_note=None, normal=None, bearing=None, standoff=None):
    e = base_entry(ctx, eid, "detail", "detail_set", prio, centerline or [r2(xy)], xy,
                   flat_time(ctx.date, "detail") if bearing is None else best_time(bearing, ctx.date),
                   "public", notes, framing, name=name)
    e.update({"building": building, "est_photos": est, "min_photos": min_photos(est), "camera_path": [],
              "normal": normal, "bearing_deg": None if bearing is None else round(bearing, 1),
              "standoff_m": standoff})
    if kind_note:
        e["detail_type"] = kind_note
    if first:
        e["first_capture"] = True
    return e


def build_details(ctx, by_building, areas, facade_entries):
    """Entrances, towers, statues/fountains for each hero spot; Kirkland's Stage 4 close-ups."""
    from shapely.geometry import Point
    out = []
    fac_by_b = {}
    for e in facade_entries:
        fac_by_b.setdefault(e["building"], []).append(e)
    entrance_nodes = [n for n in ctx.osm.nodes.values() if (n.get("tags") or {}).get("entrance") in
                      ("yes", "main", "service", "exit") and n["id"] in ctx.osm.xy]
    for bid in sorted(ctx.hero_bids):
        if bid not in ctx.geom:
            continue
        g = ctx.geom[bid]
        name = next((b.get("name") for b in ctx.buildings if b["id"] == bid), bid)
        first = bid == HERO_ID
        ents = [n for n in entrance_nodes if g.exterior.distance(Point(ctx.osm.xy[n["id"]])) < 3]
        pub = [e for e in fac_by_b.get(bid, []) if e["access"] == "public" and e["ring"] == "outer"]
        if first:
            pass               # Kirkland's entrance is in kirkland_details
        elif ents:
            for n in ents:
                xy = ctx.osm.xy[n["id"]]
                out.append(detail_entry(ctx, f"detail/{bid}/entrance-{n['id']}", f"{name} entrance", xy, "hero",
                                        "Entrance: slow arc of 10-20 shots from 1-2 m (door, frame, steps, lamps), "
                                        "then 3-4 wider shots with the wall around it.", 19,
                                        ["OSM entrance node"], bid, first, kind_note="entrance"))
        elif pub:
            best = max(pub, key=lambda e: (e["priority"] == "hero", e["length_m"]))
            mid = best["centerline"][len(best["centerline"]) // 2]
            out.append(detail_entry(ctx, f"detail/{bid}/main-entrance", f"{name} main entrance", mid, "hero",
                                    f"Main entrance (not mapped in OSM; most likely on the {best['facade']} side, "
                                    "find it on site): slow arc of 10-20 shots from 1-2 m, then 3-4 wider shots "
                                    "with the wall around it.", 19, ["entrance position assumed; confirm on site"],
                                    bid, first, kind_note="entrance"))
        # Towers: model parts much taller than the main roof and small in area.
        model = ctx.models.get(bid)
        parts = part_polys(model)
        if parts:
            main_eave = max(parts, key=lambda pg: pg[0].get("area_m2", 0))[0].get("eave_height_m") or 0
            total = sum(p.get("area_m2", 0) for p, _ in parts)

            def top(pp):
                return (pp[0].get("ridge_z") or 0) - (pp[0].get("ground_z") or model.get("ground_z", 0))

            for p, pg in sorted(parts, key=top)[-1:]:      # the tallest part only
                h = top((p, pg))
                if p.get("area_m2", 0) < 0.15 * total and h > 1.4 * max(main_eave, 1) and h > 12:
                    c = pg.centroid
                    if first:
                        continue       # Kirkland's tower gets the four-side set below
                    out.append(detail_entry(ctx, f"detail/{bid}/tower-{p['id']}", f"{name} tower", (c.x, c.y),
                                            "hero", f"Tower ({h:.0f} m): from 20-40 m away on each open side, "
                                            "5-8 shots per side with the 1x lens, then the same with the 2x/3x "
                                            "optical lens (whole set on one lens, no digital zoom).", 40,
                                            ["tower position from the LiDAR roof parts"], bid, first,
                                            kind_note="tower"))
    # Statues, fountains, monuments in or near hero areas.
    zone = ctx.unary([h["geom"].buffer(40) for h in areas] + [ctx.geom[b].buffer(40) for b in ctx.hero_bids
                                                                if b in ctx.geom])
    for el in ctx.osm.elements:
        t = el.get("tags") or {}
        if not (t.get("tourism") == "artwork" or t.get("historic") in ("monument", "memorial")
                or t.get("amenity") == "fountain"):
            continue
        if el["type"] == "node":
            p = ctx.osm.point(el)
        else:
            a = ctx.osm.area(el) or (ctx.osm.line(el) if el["type"] == "way" else None)
            p = a.centroid if a is not None else None
        if p is None or not zone.contains(p) or not ctx.campus.contains(p):
            continue
        what = t.get("artwork_type") or t.get("historic") or t.get("amenity")
        nm = t.get("name") or f"{what} near {nearest_name(ctx, p)}"
        out.append(detail_entry(ctx, f"detail/osm-{el['type']}-{el['id']}", nm, (p.x, p.y), "hero",
                                f"{nm}: orbit it, one photo every 10-15 degrees (24-36 shots), two circles "
                                "(chest height, then higher or lower), object filling most of the frame.", 60,
                                [f"OSM {what}"], None, False, kind_note=what))
    # Kirkland Hall: Stage 4's close-ups (first full-detail building).
    out.extend(kirkland_details(ctx, fac_by_b.get(HERO_ID, [])))
    # Rand Wall (approximate strip).
    rand = next((a for a in ctx.hero_area_list if a["name"] == "Rand Wall"), None)
    if rand is not None:
        g = rand["geom"]
        out.append(detail_entry(ctx, "detail/rand-wall", "Rand Wall (approximate)", (g.centroid.x, g.centroid.y),
                                "hero", "Rand Wall: walk its length 1-2 m away, one photo every step, chest and "
                                "low height, then 3-4 wide shots with Rand Hall behind. Confirm the wall's real "
                                "position on site; the outline here is a guess.", 60,
                                [rand["how"]], "B_1465205768", False,
                                centerline=[r2(c) for c in g.exterior.coords], kind_note="wall"))
    return out


def kirkland_details(ctx, facades):
    """Tower faces from 4 sides, entrance, window types, cornice, buttresses (Stage 4 needs them)."""
    out = []
    model = ctx.models.get(HERO_ID)
    parts = part_polys(model)
    if not parts:
        return out
    tower_p, tower_g = max(parts, key=lambda pg: pg[0].get("ridge_z") or 0)
    c = tower_g.centroid
    th = (tower_p.get("ridge_z") or 0) - (tower_p.get("ground_z") or model.get("ground_z", 0))
    for side, (nx, ny) in (("north", (0, 1)), ("east", (1, 0)), ("south", (0, -1)), ("west", (-1, 0))):
        minx, miny, maxx, maxy = tower_g.bounds
        edge = {"north": [[minx, maxy], [maxx, maxy]], "south": [[maxx, miny], [minx, miny]],
                "east": [[maxx, miny], [maxx, maxy]], "west": [[minx, maxy], [minx, miny]]}[side]
        d = 30.0
        stand = (c.x + nx * d, c.y + ny * d)
        e = detail_entry(
            ctx, f"detail/{HERO_ID}/tower-{side}", f"Kirkland Hall tower, {side} face", (c.x, c.y), "hero",
            f"Tower {side} face ({th:.0f} m to the top): stand {d:.0f}-40 m out on the {side} side, tower centered. "
            "5-8 shots with the 1x lens moving one step sideways each time, then the whole set again with the "
            "2x/3x optical telephoto (one lens per set, never digital zoom) for the clock, tracery and parapet. "
            "Long-lens note: lock exposure on the stone, not the sky.", 25,
            ["tower position from the LiDAR roof parts (approximate outline)",
             "some sides may be hidden by the main roof or trees: shoot from the farthest open point"],
            HERO_ID, True, centerline=[r2(p) for p in edge], kind_note="tower face",
            normal=[float(nx), float(ny)], bearing=ctx.true_bearing(bearing_of((nx, ny))), standoff=d)
        e["camera_path"] = [r2(stand)]
        out.append(e)
    pub = [f for f in facades if f["access"] == "public" and f["ring"] == "outer"]
    lawn = sorted(pub, key=lambda f: (f["priority"] != "hero", -f["length_m"]))
    main = lawn[0] if lawn else (facades[0] if facades else None)
    if main is None:
        return out
    mid = main["centerline"][len(main["centerline"]) // 2]
    items = [
        ("entrance", "main entrance", 30,
         f"Main entrance (assumed on the {main['facade']} side; confirm): slow arc of 10-20 shots from 1-2 m "
         "covering doors, frame, carving and steps, then 3-4 wider shots with the wall around it."),
        ("window-types", "window types", 60,
         "Every window type (pointed arch, rectangular, tower, basement): for each type pick one clear window "
         "and take an arc of 10-20 shots from 1-2 m (frame, sill, lintel, tracery), plus 2 shots square-on. "
         "Note each type's location."),
        ("cornice", "cornice and parapet", 40,
         "Cornice, parapet and string courses: from 6-10 m, a pass along each main side with the phone tilted up "
         "to the roof edge, overhead height, plus 2x optical close-ups of the profile at the corners."),
        ("buttresses", "buttresses", 30,
         "Buttresses and pilasters: for two typical ones, an arc of 10-15 shots from 1-2 m showing their steps "
         "and caps, then shots along the wall so their spacing can be measured."),
    ]
    for key, label, est, text in items:
        out.append(detail_entry(ctx, f"detail/{HERO_ID}/{key}", f"Kirkland Hall {label}", mid, "hero", text, est,
                                ["Stage 4 needs these for the LoD3+ model"], HERO_ID, True, kind_note=key))
    return out


def build_swatches(ctx, facade_entries):
    """One swatch set per material family per architectural cluster (k-means on building centroids)."""
    from sklearn.cluster import KMeans
    from shapely.geometry import Point
    cents = np.array([b["centroid"] for b in ctx.buildings])
    k = min(12, len(cents))
    km = KMeans(n_clusters=k, n_init=10, random_state=0).fit(cents)
    out, clusters = [], []
    order = np.argsort([math.atan2(c[1], c[0]) for c in km.cluster_centers_])
    for ci, label in enumerate(order):
        members = [b for b, l in zip(ctx.buildings, km.labels_) if l == label]
        hero = sorted([b for b in members if b["id"] in ctx.hero_bids], key=lambda b: b["id"] != HERO_ID)
        named = sorted([b for b in members if b.get("name")], key=lambda b: -b.get("area_m2", 0))
        ref = (hero or named or members)[0]
        cname = f"C{ci + 1:02d}"
        title = f"{cname} around {ref.get('name') or ref['id']}"
        cx, cy = km.cluster_centers_[label]
        clusters.append({"id": cname, "title": title, "center": [round(cx, 1), round(cy, 1)],
                         "buildings": [b["id"] for b in members], "hero": bool(hero)})
        prio = "hero" if hero else "normal"
        first = any(b["id"] == HERO_ID for b in members)
        # Wall families: the Stage 1 materials are guesses (all brick), so ask for each family seen.
        pubf = [e for e in facade_entries if e["building"] in {b["id"] for b in members} and e["access"] == "public"]
        wall_spot = None
        if pubf:
            fe = max(pubf, key=lambda e: (e["building"] == HERO_ID, e["priority"] == "hero", e["length_m"]))
            wall_spot = (fe["centerline"][0], fe["id"])
        fams = [("brick", "wall"), ("stone_trim", "wall"), ("concrete", "wall")]
        grid = ctx.grid
        for m in ("paving_brick", "paving_concrete", "asphalt", "grass", "planting"):
            geom = ctx.surfaces.get(m)
            if geom is not None and geom.intersects(Point(cx, cy).buffer(120)):
                fams.append((m, "ground"))
        if len(ctx.trees) and (np.hypot(ctx.trees[:, 0] - cx, ctx.trees[:, 1] - cy) < 120).any():
            fams.append(("bark", "tree"))
        for fam, where in fams:
            if where == "wall" and wall_spot:
                xy = wall_spot[0]
                note = f"on a shaded wall of {wall_spot[1].split('/')[0]} or any building nearby with this material"
            elif where == "ground":
                xy = nearest_patch(ctx.surfaces[fam], (cx, cy))
                note = "the patch nearest the cluster center (any similar patch nearby is fine)"
            elif where == "tree":
                t = ctx.trees[np.argmin(np.hypot(ctx.trees[:, 0] - cx, ctx.trees[:, 1] - cy))]
                xy = (t[0], t[1])
                note = "two or three tree types in this area"
            else:
                xy = (cx, cy)
                note = "find a wall with this material in the cluster"
            e = base_entry(ctx, f"swatch/{cname}/{fam}", "swatch", "swatch", prio, [r2(xy)], xy,
                           flat_time(ctx.date, "swatch"), "public",
                           [note, "Stage 1 wall materials are guesses: also shoot any other wall material you see"
                            if where == "wall" else "ground material from the terrain surface map"],
                           f"{fam.replace('_', ' ')} swatch ({title}): even light, phone square-on about 1 m away "
                           "(ground: straight down), gray card and ruler in the first shot, one shot without them, "
                           "then 4-6 overlapping shots stepping sideways. Write down where.",
                           name=f"{fam.replace('_', ' ')} swatch, {title}", cluster=cname, family=fam)
            e["est_photos"] = 8
            e["min_photos"] = 3
            if first:
                e["first_capture"] = True
            out.append(e)
    return out, clusters


def nearest_patch(geom, xy):
    from shapely.geometry import Point
    from shapely.ops import nearest_points
    p = Point(xy)
    if geom.contains(p):
        return (p.x, p.y)
    q = nearest_points(geom, p)[0]
    return (q.x, q.y)


# ---- markdown -------------------------------------------------------------------

def counter(items, key):
    out = {}
    for e in items:
        out[e[key]] = out.get(e[key], 0) + 1
    return dict(sorted(out.items()))


def write_markdown(doc, summary, areas, ctx):
    E = doc["entries"]
    fac = [e for e in E if e["kind"] == "facade"]
    lines = ["# Shot list (Stage 2)", "",
             f"Generated {doc['generated']} by `Tools/VanderbiltCampus/pipeline/shotlist.py` from "
             f"`{doc['sources']['buildings']}`. Season for best times: {doc['season_date']} (re-run with "
             "`--date` for another season). The full list is `Tools/VanderbiltCampus/config/shot_list.json`; "
             "the phone page is [coverage/index.html](coverage/index.html); the walking days are "
             "[CAPTURE-DAYS.md](CAPTURE-DAYS.md).", "",
             "## Counts", "",
             f"- Campus buildings: {len(summary)}; facades: {len(fac)} "
             f"({', '.join(f'{k} {v}' for k, v in counter(fac, 'access').items())}).",
             f"- Entries by kind: {', '.join(f'{k} {v}' for k, v in counter(E, 'kind').items())}.",
             f"- By priority: {', '.join(f'{k} {v}' for k, v in counter(E, 'priority').items())}.",
             f"- By method: {', '.join(f'{k} {v}' for k, v in counter(E, 'method').items())}.",
             f"- Estimated photos: {sum(e['est_photos'] for e in E):,} (minimum to count as captured: "
             f"{sum(e['min_photos'] for e in E):,}).", "",
             "## Rules used", "",
             f"- Facades: consecutive wall segments whose direction differs by less than {MAX_TURN_DEG:g} degrees "
             f"merge; a facade is at least {MIN_FACADE_M:g} m (a short jog between two walls running the same way "
             "joins them; other short pieces are corners, covered by the corner turns). Names are the compass side "
             "the wall faces (true north), with -1, -2 (longest first) when a side has several. Interior rings "
             f"over {MIN_COURTYARD_M2:g} m2 are courtyards (`courtyard-...`, then `courtyard-b-...` for a second "
             "one).",
             f"- Access: rays straight out from the wall every 2 m up to {VIEW_M:g} m, stopped by buildings, "
             f"fences and hedges. `blocked` when half the wall has a building under {BLOCKED_M:g} m in front; "
             "`public` when at least 30% of the rays reach a path, sidewalk or lawn (terrain surface map) that is "
             "not private; else `restricted` (fenced/private/construction, only asphalt, or nothing walkable). "
             "Hospital and clinical buildings are always `restricted` (stay on public sidewalks) and background.",
             f"- Stand-off: {STANDOFF_K:g} x wall height, {STANDOFF_MIN:g}-{STANDOFF_MAX:g} m, no farther than the "
             "median open space in front. Camera path: parallel to the wall at that distance, trimmed to walkable "
             "ground (asphalt only when nothing else is there).",
             f"- est_photos (photo sets) = {PASSES} passes x {HEIGHTS} heights x ceil(length / step) + "
             f"{CORNER_SHOTS} corner shots x {HEIGHTS} heights, step = {1 - OVERLAP:.2f} x the {FOV_DEG:g} degree "
             "view width at the stand-off (75% overlap: 0.35 x distance), plus one far pass from 10-15 m when the "
             f"wall is taller than 1.2 x the stand-off. min_photos = {MIN_SHARE:.0%} of est (at least 3). "
             "360 walks: phone fallback of one photo every 1.5 m, two passes. Paths: one photo every 2 m.",
             f"- best_time: the sun (Nashville {LAT}N) is computed every 5 minutes on the season date; a wall is in "
             "shade when the sun is down or behind the wall plane. North walls: any time; east: afternoon; west: "
             "morning; south: sunlit all day in late autumn, so overcast or low sun. Overcast is always fine.",
             "- Priority: `hero` = hero buildings, hero areas, and other facades that face a hero lawn; `background` "
             "= hospital/clinical, parking structures, utility buildings, and walls facing out of campus "
             "(backdrop ring); else `normal`.", "",
             "## Hero spots", "",
             "| Spot (STANDARDS section 3) | Used in the shot list | OSM | Status |",
             "|---|---|---|---|"]
    for s in ctx.hero["spots"]:
        m = s.get("match") or {}
        if s["spot"] == "Kirkland Hall tower":
            used, osm, st = ("Kirkland Hall tower: four `detail/B_544397715/tower-*` entries at the tallest LiDAR "
                             "roof part", "inside way/544397715", "approximate (LiDAR roof part, no OSM outline)")
        elif s["spot"] == "Rand Wall":
            ra = next((a for a in areas if a["name"] == "Rand Wall"), None)
            used, osm, st = ("`detail/rand-wall`: " + (ra["how"] if ra else "not placed"), "none",
                             "approximate, confirm on site")
        elif s["spot"] == "Peabody Esplanade":
            used, osm, st = ("`plaza/approx-peabody-esplanade`", m.get("osm", ""),
                             "approximate outline (OSM has a node only)")
        elif s["spot"] == "Branscomb Quad":
            used = "the four houses + Branscomb Quad Market (hero buildings) and `plaza/approx-branscomb-quad`"
            osm, st = ", ".join(x["osm"] for x in s["members"]), "group; quad outline approximate"
        elif s["spot"] == "Commons":
            used = ("Commons Center building + `plaza/approx-commons-center-lawn` (the wider Ingram Commons area "
                    "is a node only)")
            osm, st = m.get("osm", ""), "ambiguous name resolved to Commons Center; lawn approximate"
        else:
            used, osm = m.get("name", ""), m.get("osm", "")
            st = {"ok": "confirmed", "alias": "confirmed under the OSM name"}.get(s["status"], s["status"])
        lines.append(f"| {s['spot']} | {used} | {osm} | {st} |")
    lines += ["", "Approximate shapes (drawn by the script from nearby OSM features; treat as guesses until "
              "checked on site):", ""]
    for a in areas:
        if a["approx"]:
            lines.append(f"- **{a['name']}**: {a['how']} ({a['geom'].area:.0f} m2).")
    lines += ["", "## Capture day 1: Kirkland Hall and Alumni Lawn", "",
              "Kirkland is the first full-detail building (Stage 4), so its entries carry `first_capture: true`:", ""]
    for e in E:
        if e.get("first_capture"):
            lines.append(f"- `{e['id']}` ({e['method']}, {e['est_photos']} photos, {e['access']}): "
                         f"{e['best_time']['summary']}")
    lines += ["", "## Schema of `shot_list.json` (intake reads this)", "",
              "Top level: `{version: int, generated: str (ISO time), season_date, sources, rules, clusters, "
              "hero_areas, entries: [...]}`.", "",
              "Each entry (required keys; extra keys may be added later, never renamed):", "",
              "| Key | Type | Meaning |", "|---|---|---|",
              "| id | str | unique; facades `<building id>/<facade>`, else `courtyard/..`, `plaza/..`, `path/P0001`, "
              "`stairs/way-..`, `detail/..`, `swatch/<cluster>/<family>` |",
              f"| kind | str | one of {', '.join(KINDS)} |",
              "| building | str or null | building id (`B_...`) |",
              "| facade | str or null | facade name (`north`, `east-2`, `courtyard-south`) |",
              f"| priority | str | {', '.join(PRIORITIES)} |",
              f"| method | str | {', '.join(METHODS)} |",
              "| est_photos | int | planned photos (0 for blocked facades) |",
              "| min_photos | int | photos at which intake marks it captured |",
              "| centerline | [[x, y], ...] | campus meters (facade polyline; area outline; path line; point) |",
              "| normal | [x, y] or null | unit outward normal (facades, tower faces) |",
              "| bearing_deg | number or null | compass bearing the facade faces, true north |",
              "| standoff_m | number or null | suggested camera distance from the wall |",
              "| camera_path | [[x, y], ...] | where to walk (may be empty when no walkable ground) |",
              "| tile | str | `T_<col>_<row>` of the entry's anchor point |",
              "| best_time | object | `summary` text, `shade` [[from, to], ...] local time, `morning_ok`, "
              "`afternoon_ok`, `date` |",
              f"| access | str | {', '.join(ACCESS)} |",
              "| access_notes | str | why, and what to watch for |",
              "| example_framing | str | where to stand and what to include |",
              "| day | int or null | capture day (filled by capture_days.py; null = not scheduled) |", "",
              "Common extra keys: `name`, `building_name`, `length_m`, `height_m`, `area_m2`, `trees_in_view`, "
              "`first_capture`, `ring` (outer/courtyard), `far_pass`, `cluster`, `family`, `detail_type`, "
              "`approx`, `day_order`.", "",
              "Coverage status (written by intake to `config/coverage_status.json`, read by coverage_map.py): "
              "`{version: 1, updated: str, entries: {<entry id>: {status, photos, last, note}}}` (intake's `usable_count`/`photo_count`, `last_capture` and `reason` are read as photos, last and note) with status one "
              "of `not_started`, `captured`, `aligned`, `needs_reshoot` (a bare status string per id also works).",
              "", "`python pipeline/shotlist.py validate` checks a file against this schema.", "",
              "## Buildings", "",
              "| Building | Tile | Priority | Facades (public / restricted / blocked) | Wall height m | Est photos |",
              "|---|---|---|---|---|---|"]
    for s in sorted(summary, key=lambda s: (not s["hero"], s["class"] is not None, s["name"])):
        prio = "hero" if s["hero"] else ("background (" + s["class"] + ")" if s["class"] else "normal")
        lines.append(f"| {s['name']} (`{s['id']}`) | {s['tile']} | {prio} | {s['facades']} ({s['public']} / "
                     f"{s['restricted']} / {s['blocked']}) | {s['height']} | {s['est']:,} |")
    SHOT_MD.parent.mkdir(parents=True, exist_ok=True)
    SHOT_MD.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


# ---- main -------------------------------------------------------------------------

def build(date):
    root = data_root()
    print("loading data ...", flush=True)
    ctx = Context(root, date)
    ctx.footway_lines = [ln for ln in (ctx.osm.line(w) for w in footway_ways(ctx)) if ln is not None]
    # Hero areas: rasterized so facades can tell when they face one.
    areas = hero_areas(ctx)
    ctx.hero_area_list = areas
    ctx.hero_area_names = [a["name"] for a in areas]
    ctx.grid.burn("hero", [(a["geom"], i + 1) for i, a in enumerate(areas)])
    print("facades ...", flush=True)
    facades, summary, courtyards, by_building = build_facades(ctx)
    rand_facades = [f for f in by_building.get("B_1465205768", []) if f["ring"] == "outer"]
    ra = rand_wall_area(ctx, rand_facades)
    if ra:
        areas.append(ra)
        ctx.hero_area_list = areas
    print("other entries ...", flush=True)
    entries = list(facades)
    entries += build_courtyards(ctx, courtyards, facades)
    entries += build_plazas(ctx, areas)
    entries += build_paths(ctx, areas)
    entries += build_stairs(ctx)
    entries += build_details(ctx, by_building, areas, facades)
    sw, clusters = build_swatches(ctx, facades)
    entries += sw
    old = load_json(SHOT_LIST) if SHOT_LIST.exists() else None
    keep_days = {e["id"]: (e.get("day"), e.get("day_order")) for e in (old or {}).get("entries", [])}
    for e in entries:
        if e["id"] in keep_days and keep_days[e["id"]][0] is not None:
            e["day"], e["day_order"] = keep_days[e["id"]]
    doc = {"version": VERSION, "generated": dt.datetime.now().isoformat(timespec="seconds"),
           "season_date": date.isoformat(),
           "sources": {"buildings": str(ctx.src.relative_to(root)).replace("\\", "/"),
                       "osm": str(ctx.osm.snap.relative_to(root)).replace("\\", "/"),
                       "surfaces": "derived/terrain/v1/surfaces.geojson", "trees": "derived/trees/v1/trees.json",
                       "hero_spots": "Tools/VanderbiltCampus/config/hero_spots.json"},
           "rules": {"max_turn_deg": MAX_TURN_DEG, "min_facade_m": MIN_FACADE_M, "view_m": VIEW_M,
                     "blocked_m": BLOCKED_M, "standoff": [STANDOFF_MIN, STANDOFF_MAX, STANDOFF_K],
                     "fov_deg": FOV_DEG, "overlap": OVERLAP, "passes": PASSES, "heights": HEIGHTS,
                     "corner_shots": CORNER_SHOTS, "min_share": MIN_SHARE, "lat": LAT, "lon": LON},
           "clusters": clusters,
           "buildings": [{"id": b["id"], "name": b.get("name"), "hero": b["id"] in ctx.hero_bids,
                          "outlines": [[r2(c) for c in p.simplify(0.3).exterior.coords]
                                       for p in polys_of(ctx.geom[b["id"]])]} for b in ctx.buildings],
           "hero_areas": [{"name": a["name"], "approx": a["approx"], "how": a["how"],
                           "polygon": [r2(c) for c in max(polys_of(a["geom"]), key=lambda p: p.area).exterior.coords]}
                          for a in areas if not a["geom"].is_empty],
           "entries": entries}
    errs = validate(doc)
    if errs:
        fail(f"{len(errs)} schema problems, first: {errs[:5]}")
    save_json(SHOT_LIST, doc)
    write_markdown(doc, summary, areas, ctx)
    arch = root / "derived" / "capture_plan" / "v1" / "shot_list.json"
    save_json(arch, doc)
    import manifest
    manifest.add_file(arch, source="pipeline/shotlist.py", license="derived (OSM ODbL; LiDAR public domain)",
                      stage=2, root=root)
    fac = [e for e in entries if e["kind"] == "facade"]
    print(f"buildings {len(summary)}, facades {len(fac)} {counter(fac, 'access')}")
    print(f"entries {len(entries)} kinds {counter(entries, 'kind')}")
    print(f"priority {counter(entries, 'priority')} method {counter(entries, 'method')}")
    print(f"est photos {sum(e['est_photos'] for e in entries):,}; wrote {SHOT_LIST} and {SHOT_MD}")
    return doc


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("build", help="build the shot list")
    b.add_argument("--date", help="season date for best_time (YYYY-MM-DD, default today)")
    v = sub.add_parser("validate", help="validate a shot_list.json")
    v.add_argument("file", nargs="?", default=str(SHOT_LIST))
    a = ap.parse_args(argv)
    if a.cmd == "build":
        date = dt.date.fromisoformat(a.date) if a.date else dt.date.today()
        build(date)
    else:
        errs = validate(load_json(a.file))
        if errs:
            print("\n".join(errs[:50]))
            fail(f"{len(errs)} problems")
        print("valid")


if __name__ == "__main__":
    main()
