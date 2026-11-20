"""Intake step 2b: sort photos into shot-list areas by where they were taken.

Targets come from config/shot_list.json when it exists (written by the
shot-list step). Each entry has an id, building, facade, priority and a
shape in campus meters: "polygon" (area: courtyard, plaza) or "centerline"
/ "line" (a facade). A facade may give "normal" [nx, ny] (the outward
direction) or "facing_deg"; if not, the outward side is taken as the one
away from its building's footprint.

Without a shot list, the targets are the building footprints from
derived/buildings/v1/footprints.json (facade left empty).

Rule for one photo at camera point P with view heading H (campus grid degrees):
- consider targets within MAX_DIST (60 m) of P;
- if H is known, only the part of the target inside the view wedge
  (H +- HALF_ANGLE = 75 degrees, radius 60 m) counts;
- a facade only counts if P is on its outward side (the facade faces the camera);
- the nearest remaining target wins. Otherwise the photo is "unassigned".
"""
import math
from pathlib import Path

import numpy as np
import shapely
import shapely.ops
from shapely.geometry import LineString, MultiPolygon, Point, Polygon

MAX_DIST = 60.0
HALF_ANGLE = 75.0
DEFAULT_MIN_PHOTOS = 12
CLOSE_UP_KINDS = {"swatch", "detail"}


class Target:
    def __init__(self, id, geom, kind, building=None, facade=None, normal=None, priority=None,
                 min_photos=DEFAULT_MIN_PHOTOS, name=None):
        self.id, self.geom, self.kind = id, geom, kind
        self.building, self.facade, self.normal = building, facade, normal
        self.priority, self.min_photos, self.name = priority, min_photos, name
        self.shot_kind = None          # shot-list kind: facade, path, courtyard, swatch, ...

    def info(self):
        return {"id": self.id, "kind": self.kind, "shot_kind": self.shot_kind, "building": self.building,
                "facade": self.facade, "priority": self.priority, "min_photos": self.min_photos,
                "name": self.name}


def _ring(points):
    pts = [tuple(p[:2]) for p in points]
    return Polygon(pts) if len(pts) >= 3 else None


def polygon_from(data):
    """A polygon from [[x,y],...], [[[x,y],...]] (rings) or [[[[x,y]...]]] (multi)."""
    arr = data
    depth = 0
    while isinstance(arr, list) and arr and isinstance(arr[0], list):
        arr = arr[0]
        depth += 1
    if depth == 1:
        return _ring(data)
    if depth == 2:
        shell = data[0]
        holes = [h for h in data[1:] if len(h) >= 3]
        return Polygon([tuple(p[:2]) for p in shell], [[tuple(p[:2]) for p in h] for h in holes])
    if depth == 3:
        polys = [polygon_from(p) for p in data]
        polys = [p for p in polys if p is not None and not p.is_empty]
        return MultiPolygon(polys) if len(polys) > 1 else (polys[0] if polys else None)
    return None


def load_footprints(path):
    """Building targets from footprints.json (the Stage 1 output)."""
    from common import load_json
    data = load_json(path)
    out = []
    for b in data.get("buildings", []):
        geom = polygon_from(b.get("polygon") or [])
        if geom is None or geom.is_empty:
            continue
        if not geom.is_valid:
            geom = geom.buffer(0)
        out.append(Target(b["id"], geom, "building", building=b["id"], name=b.get("name")))
    return out


def _normal_from(entry):
    if entry.get("normal"):
        nx, ny = entry["normal"][:2]
        n = math.hypot(nx, ny)
        return (nx / n, ny / n) if n > 0 else None
    if entry.get("facing_deg") is not None:
        a = math.radians(float(entry["facing_deg"]))
        return (math.sin(a), math.cos(a))
    return None


def load_shot_list(path, buildings=None):
    """Targets from config/shot_list.json (a list, or {"entries": [...]})."""
    from common import load_json
    data = load_json(path)
    entries = data.get("entries", data.get("shots", [])) if isinstance(data, dict) else data
    by_building = {t.id: t for t in (buildings or [])}
    out = []
    for e in entries:
        line = e.get("centerline") or e.get("line") or e.get("segment")
        if line:
            pts = [tuple(p[:2]) for p in line]
            if len(pts) == 1:
                geom = Point(pts[0])                       # swatch / detail spot
            elif len(pts) >= 4 and pts[0] == pts[-1]:
                geom = Polygon(pts)                        # closed ring: courtyard, plaza
                if not geom.is_valid:
                    geom = geom.buffer(0)
            else:
                geom = LineString(pts)                     # facade, path, stairs
        elif e.get("polygon"):
            geom = polygon_from(e["polygon"])
        elif e.get("point"):
            geom = Point(e["point"][:2])
        else:
            continue
        if geom is None or geom.is_empty:
            continue
        normal = _normal_from(e)
        if normal is None and isinstance(geom, LineString) and e.get("building") in by_building:
            normal = outward_normal(geom, by_building[e["building"]].geom)
        t = Target(e["id"], geom, "shot_list", building=e.get("building"), facade=e.get("facade"),
                   normal=normal, priority=e.get("priority"),
                   min_photos=int(e.get("min_photos") or DEFAULT_MIN_PHOTOS),
                   name=e.get("name") or e.get("building_name"))
        t.shot_kind = e.get("kind")
        out.append(t)
    return out


def outward_normal(line, building_geom):
    """Unit normal of a facade line pointing away from its building."""
    (x0, y0), (x1, y1) = line.coords[0], line.coords[-1]
    dx, dy = x1 - x0, y1 - y0
    n = math.hypot(dx, dy) or 1.0
    nx, ny = dy / n, -dx / n
    mid = line.interpolate(0.5, normalized=True)
    c = building_geom.centroid
    if (mid.x - c.x) * nx + (mid.y - c.y) * ny < 0:
        nx, ny = -nx, -ny
    return (nx, ny)


def load_targets(shot_list_path, footprints_path):
    """(targets, source label). The shot list wins when it exists."""
    buildings = load_footprints(footprints_path) if Path(footprints_path).exists() else []
    if Path(shot_list_path).exists():
        return load_shot_list(shot_list_path, buildings), "shot_list"
    return buildings, "footprints"


def wedge(x, y, heading_deg, radius=MAX_DIST, half_angle=HALF_ANGLE, steps=24):
    """The view wedge (a pie slice) in front of a camera, as a polygon."""
    pts = [(x, y)]
    for i in range(steps + 1):
        a = math.radians(heading_deg - half_angle + 2 * half_angle * i / steps)
        pts.append((x + radius * math.sin(a), y + radius * math.cos(a)))
    return Polygon(pts)


class Assigner:
    """Finds the target a camera position (and heading) belongs to."""

    def __init__(self, targets, max_dist=MAX_DIST, half_angle=HALF_ANGLE):
        # Close-up entries (material swatches, small details) are shot from
        # about 1 m, closer than phone GPS can tell apart; they are not
        # assigned by GPS (sort them by hand or by time).
        targets = [t for t in targets if getattr(t, "shot_kind", None) not in CLOSE_UP_KINDS]
        self.targets = targets
        self.max_dist, self.half_angle = max_dist, half_angle
        self.tree = shapely.STRtree([t.geom for t in targets]) if targets else None

    def assign(self, x, y, heading_deg=None):
        if self.tree is None:
            return None
        p = Point(x, y)
        cone = wedge(x, y, heading_deg, self.max_dist, self.half_angle) if heading_deg is not None else None
        best, best_d = None, None
        for i in self.tree.query(p.buffer(self.max_dist)):
            t = self.targets[int(i)]
            g = t.geom
            if cone is not None and not g.contains(p):
                g = g.intersection(cone)
                if g.is_empty:
                    continue
            d = p.distance(g)
            if d > self.max_dist:
                continue
            if t.normal is not None:
                q = shapely.ops.nearest_points(g, p)[0]
                if (x - q.x) * t.normal[0] + (y - q.y) * t.normal[1] <= 0:
                    continue                       # camera is behind this facade
            if best_d is None or d < best_d:
                best, best_d = t, d
        if best is None:
            return None
        out = best.info()
        out["distance_m"] = round(float(best_d), 2)
        return out


def fill_by_time(records, max_gap_s=30.0):
    """Photos without GPS borrow the area of the closest-in-time photo that has one.

    Returns how many were filled. Only within max_gap_s seconds."""
    timed = sorted([r for r in records if r.get("t") is not None and r.get("area")], key=lambda r: r["t"])
    if not timed:
        return 0
    ts = np.array([r["t"] for r in timed])
    n = 0
    for r in records:
        if r.get("area") or r.get("campus") or r.get("t") is None:
            continue
        i = int(np.argmin(np.abs(ts - r["t"])))
        if abs(ts[i] - r["t"]) <= max_gap_s:
            r["area"] = dict(timed[i]["area"], by="time", from_photo=timed[i]["name"])
            n += 1
    return n
