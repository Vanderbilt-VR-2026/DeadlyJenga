"""Make config/boundary.geojson: the campus outline and the 150 m backdrop ring.

Input: the newest OSM snapshot raw/osm/<date>/campus_overpass.json (fetch_osm.py).

How the campus outline is built:
1. Take the OSM amenity=university area(s) named "Vanderbilt University", plus any
   Peabody College area (Peabody is only a point in OSM today; we check it lies
   inside the Vanderbilt area).
2. Work in meters (UTM zone 16N, EPSG:32616). "Close" the shape lightly: grow it
   by --close meters and shrink it back. This fills small notches.
3. Snap to the bounding streets: the city streets (residential and bigger) cut the
   map into blocks. A block that is mostly campus (>= --block-share of its area)
   is added whole, so the edge follows the street centerline instead of a
   hand-traced line. Blocks that are only partly campus keep the OSM edge.
4. The backdrop ring = campus grown by --ring meters, minus the campus.

Output (WGS84, a FeatureCollection with two features):
  role "campus" and role "backdrop_ring", each with area_m2 and the OSM ids used.
Also written: derived/boundary/boundary_report.json in the archive (what was
chosen and why: street distances, medical-center check) and recorded in the manifest.
"""
import argparse
import json
import sys
from pathlib import Path

from pyproj import Transformer
from shapely.geometry import LineString, Point, Polygon, mapping, shape
import shapely
from shapely.ops import polygonize, unary_union

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CONFIG, data_root, fail, save_json  # noqa: E402
import manifest  # noqa: E402

METRIC = 32616   # UTM 16N, meters
STREET_KINDS = {"motorway", "trunk", "primary", "secondary", "tertiary", "residential",
                "unclassified", "motorway_link", "trunk_link", "primary_link",
                "secondary_link", "tertiary_link"}
# Streets we expect around campus (only for the report; nothing depends on these names).
EXPECTED_EDGE_STREETS = ["West End Avenue", "21st Avenue South", "Blakemore Avenue",
                         "Wedgewood Avenue", "Natchez Trace", "25th Avenue South",
                         "Edgehill Avenue", "Children's Way", "Medical Center Drive"]


def transform(fn, geom):
    """Reproject a shape with a pyproj transform function."""
    return shapely.transform(geom, lambda x, y: fn(x, y), interleaved=False)


def newest_snapshot(root):
    snaps = sorted((root / "raw" / "osm").glob("*/campus_overpass.json"))
    if not snaps:
        fail("no OSM snapshot found; run fetch_osm.py first")
    return snaps[-1]


def index(elements):
    nodes, ways, rels = {}, {}, {}
    for el in elements:
        {"node": nodes, "way": ways, "relation": rels}[el["type"]][el["id"]] = el
    return nodes, ways, rels


def way_coords(way, nodes):
    pts = [nodes[n] for n in way.get("nodes", []) if n in nodes]
    return [(p["lon"], p["lat"]) for p in pts]


def area_of(el, nodes, ways):
    """Polygon (lon/lat) for a closed way or a multipolygon relation, else None."""
    if el["type"] == "way":
        c = way_coords(el, nodes)
        return Polygon(c).buffer(0) if len(c) >= 4 and c[0] == c[-1] else None
    lines = []
    for m in el.get("members", []):
        if m["type"] == "way" and m["ref"] in ways and m.get("role", "outer") in ("outer", ""):
            c = way_coords(ways[m["ref"]], nodes)
            if len(c) >= 2:
                lines.append(LineString(c))
    polys = list(polygonize(unary_union(lines)))
    return unary_union(polys) if polys else None


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--snapshot", help="path to campus_overpass.json (default: newest)")
    p.add_argument("--close", type=float, default=20.0, help="closing distance in meters (default 20)")
    p.add_argument("--block-share", type=float, default=0.75,
                   help="add a street block when at least this share is campus (default 0.75)")
    p.add_argument("--ring", type=float, default=150.0, help="backdrop ring width in meters (default 150)")
    p.add_argument("--out", default=str(CONFIG / "boundary.geojson"))
    args = p.parse_args(argv)

    root = data_root()
    snap = Path(args.snapshot) if args.snapshot else newest_snapshot(root)
    data = json.loads(snap.read_text(encoding="utf-8"))
    nodes, ways, rels = index(data["elements"])
    to_m = Transformer.from_crs(4326, METRIC, always_xy=True).transform
    to_ll = Transformer.from_crs(METRIC, 4326, always_xy=True).transform
    notes = {"snapshot": manifest.rel(snap, root), "osm_base": data.get("osm3s", {}).get("timestamp_osm_base")}

    # 1. University areas.
    used, parts, peabody_points = [], [], []
    for el in list(ways.values()) + list(rels.values()):
        t = el.get("tags", {})
        name = t.get("name", "")
        if t.get("amenity") in ("university", "college") and ("Vanderbilt" in name or "Peabody" in name):
            poly = area_of(el, nodes, ways)
            if poly is not None and not poly.is_empty:
                used.append(f"{el['type']}/{el['id']}")
                parts.append(transform(to_m, poly))
                print(f"area: {el['type']}/{el['id']} {name} {transform(to_m, poly).area:.0f} m2")
    for el in nodes.values():
        t = el.get("tags", {})
        if t.get("amenity") in ("university", "college") and "Peabody" in t.get("name", ""):
            peabody_points.append((el["id"], Point(to_m(el["lon"], el["lat"]))))
    if not parts:
        fail("no Vanderbilt amenity=university area in the snapshot")
    osm_campus = unary_union(parts)
    for nid, pt in peabody_points:
        inside = osm_campus.contains(pt)
        notes.setdefault("peabody", []).append({"osm": f"node/{nid}", "inside_vanderbilt_area": inside})
        print(f"Peabody College node/{nid}: inside Vanderbilt area = {inside}")
        if not inside:
            print("  WARNING: Peabody point is outside the Vanderbilt area; check the outline by hand")

    # Medical center: report only (it is not a separate university area).
    med = []
    for el in list(ways.values()) + list(rels.values()):
        t = el.get("tags", {})
        if t.get("amenity") == "hospital" and "Vanderbilt" in t.get("name", ""):
            poly = area_of(el, nodes, ways)
            if poly is not None and not poly.is_empty:
                pm = transform(to_m, poly)
                share = pm.intersection(osm_campus).area / pm.area if pm.area else 0
                med.append({"osm": f"{el['type']}/{el['id']}", "name": t.get("name"),
                            "area_m2": round(pm.area), "share_inside_campus": round(share, 3)})
    notes["hospital_areas"] = med
    for m in med:
        print(f"hospital {m['osm']} {m['name']}: {m['share_inside_campus']:.0%} inside campus (kept as is)")

    # 2. Light closing.
    closed = osm_campus.buffer(args.close, join_style="mitre").buffer(-args.close, join_style="mitre")
    closed = closed.union(osm_campus)

    # 3. Street blocks.
    street_lines, edge_report = [], {}
    for w in ways.values():
        t = w.get("tags", {})
        if t.get("highway") in STREET_KINDS:
            c = way_coords(w, nodes)
            if len(c) >= 2:
                line = transform(to_m, LineString(c))
                street_lines.append(line)
                nm = t.get("name")
                d = line.distance(closed.boundary)
                if nm and d < 60:
                    edge_report.setdefault(nm, []).append(round(d, 1))
    blocks = list(polygonize(unary_union(street_lines)))
    added = []
    for b in blocks:
        share = b.intersection(closed).area / b.area if b.area else 0
        if share >= args.block_share:
            added.append(b)
    campus = unary_union([closed] + added).buffer(0)
    if campus.geom_type == "MultiPolygon":
        biggest = max(campus.geoms, key=lambda g: g.area)
        notes["dropped_islands_m2"] = [round(g.area) for g in campus.geoms if g is not biggest]
        campus = biggest
    campus = Polygon(campus.exterior).simplify(0.2)          # no holes; 20 cm simplify
    notes["areas_m2"] = {"osm_university_area": round(osm_campus.area), "after_closing": round(closed.area),
                         "after_street_blocks": round(campus.area)}
    notes["street_blocks_added"] = len(added)
    notes["streets_near_edge_m"] = {k: min(v) for k, v in sorted(edge_report.items())}
    notes["expected_edge_streets_missing"] = [s for s in EXPECTED_EDGE_STREETS if s not in edge_report]
    print(f"campus: OSM {osm_campus.area:.0f} m2 -> closed {closed.area:.0f} -> with blocks {campus.area:.0f}"
          f" ({len(added)} blocks added)")

    # 4. Ring.
    outer = campus.buffer(args.ring, join_style="round")
    ring = outer.difference(campus)

    def feature(geom_m, props):
        g = transform(to_ll, geom_m)
        return {"type": "Feature", "properties": props, "geometry": mapping(g)}

    fc = {"type": "FeatureCollection",
          "name": "vanderbilt_campus_boundary",
          "crs_note": "WGS84 lon/lat (EPSG:4326); areas computed in EPSG:32616",
          "features": [
              feature(campus, {"role": "campus", "area_m2": round(campus.area),
                               "osm_ids": used, "peabody_osm": [n["osm"] for n in notes.get("peabody", [])],
                               "method": f"OSM university area, closed {args.close} m, street blocks >= "
                                         f"{args.block_share:.0%} campus added",
                               "osm_snapshot": notes["snapshot"], "osm_base": notes["osm_base"],
                               "license": "ODbL-1.0; (c) OpenStreetMap contributors"}),
              feature(ring, {"role": "backdrop_ring", "area_m2": round(ring.area), "width_m": args.ring,
                             "osm_ids": used}),
          ]}
    # Round coordinates to ~1 cm so the file stays small and readable.
    text = json.dumps(fc, indent=1)
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    fc = json.loads(text, parse_float=lambda s: round(float(s), 7))
    save_json(out, fc)
    print(f"wrote {out}: campus {campus.area:.0f} m2, ring {ring.area:.0f} m2")

    report = root / "derived" / "boundary" / "boundary_report.json"
    save_json(report, notes)
    manifest.add_file(report, source="make_boundary.py", license="ODbL-1.0; (c) OpenStreetMap contributors",
                      stage=0, root=root)
    print(f"report: {report}")


if __name__ == "__main__":
    main()
