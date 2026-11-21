"""Check the hero list (STANDARDS.md section 3) against OSM and write config/hero_spots.json.

For each hero spot we look for OSM features by exact name (and known other names,
for example "Central Library" is OSM's "Jean & Alexander Heard Library").
Each spot gets a status:
  ok         one clear OSM match
  alias      one match, but under a different name (the OSM name is recorded)
  group      the spot is several OSM features (for example a quad of houses)
  point      OSM has only a point for it, no outline
  ambiguous  more than one candidate; the first is used, the rest are listed
  unknown    not in OSM; candidates or a note say where to look (Stage 2 confirms)

Output per spot: OSM name and id, building id B_<osm_id> (buildings only),
campus-frame centroid, tile.
"""
import argparse
import sys
from pathlib import Path

import numpy as np
import shapely
from shapely.geometry import Point

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
import make_boundary as mb  # noqa: E402
from common import CONFIG, data_root, load_json, save_json  # noqa: E402

# spot, OSM names to look for (first = preferred), status if found, note
SPOTS = [
    ("Kirkland Hall", ["Kirkland Hall"], None, ""),
    ("Kirkland Hall tower", [], "unknown",
     "No separate tower feature (no building:part) in OSM; the tower is inside the Kirkland Hall "
     "footprint way/544397715. Stage 1 finds it in the LiDAR nDSM."),
    ("Alumni Lawn", ["Alumni Lawn"], None, ""),
    ("Central Library", ["Jean & Alexander Heard Library"], "alias",
     "Central Library is part of the Jean and Alexander Heard Library building in OSM."),
    ("Library Lawn", ["Library Lawn"], None, ""),
    ("Peabody Esplanade", ["Peabody Esplanade"], None, ""),
    ("Cohen Memorial Hall", ["Cohen Memorial Hall"], None, ""),
    ("Furman Hall", ["Furman Hall"], None, ""),
    ("Benson Hall", ["Benson Science Hall"], "alias", "OSM name is 'Benson Science Hall'."),
    ("Wyatt Center", ["Wyatt Center"], None, ""),
    ("Commons", ["Commons Center", "The Martha Rivers Ingram Commons", "Commons Center Lawn"], "ambiguous",
     "'Commons' may mean the Commons Center building or the whole Ingram Commons area (OSM point only). "
     "Commons Center is used; Stage 2 confirms."),
    ("Rand Hall", ["Rand Hall"], None, ""),
    ("Rand Wall", [], "unknown",
     "No wall or seating wall named or tagged near Rand Hall in OSM (no barrier=wall within 80 m). "
     "Stage 2 locates it on site / from Vanderbilt's map."),
    ("Engineering and Science Building", ["Science and Engineering Building"], "alias",
     "OSM name is 'Science and Engineering Building'."),
    ("Featheringill/Jacobs Hall", ["Featheringill / Jacobs Hall"], None,
     "One OSM building outline covers both halls."),
    ("Student Life Center", ["Student Life Center"], None, ""),
    ("Branscomb Quad", ["Vaughn House", "Lupton House", "Scales House", "Stapleton House", "Branscomb Quad Market"],
     "group", "No Branscomb Quad outline in OSM; the quad is the four houses plus Branscomb Quad Market. "
     "Centroid = centroid of the members' footprints."),
    ("Memorial Gymnasium exterior", ["Memorial Gymnasium"], None, ""),
]


def geometry(el, nodes, ways):
    if el["type"] == "node":
        return Point(el["lon"], el["lat"])
    return mb.area_of(el, nodes, ways)


def to_campus_geom(g):
    return shapely.transform(g, lambda x, y: fr.to_campus(x, y, np.zeros_like(x), 4326)[:2], interleaved=False)


def describe(el, g):
    t = el.get("tags", {})
    c = g.centroid
    is_building = "building" in t
    return {
        "name": t.get("name"),
        "osm": f"{el['type']}/{el['id']}",
        "building_id": f"B_{el['id']}" if is_building else None,
        "kind": "building" if is_building else (t.get("leisure") or t.get("landuse") or t.get("amenity") or "other"),
        "geometry": g.geom_type,
        "centroid": [round(c.x, 2), round(c.y, 2)],
        "tile": fr.tile_of(c.x, c.y),
        "area_m2": round(g.area, 1) if g.area else None,
    }


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--out", default=str(CONFIG / "hero_spots.json"))
    args = p.parse_args(argv)

    root = data_root()
    snap = mb.newest_snapshot(root)
    data = load_json(snap)
    nodes, ways, rels = mb.index(data["elements"])
    by_name = {}
    for el in data["elements"]:
        n = el.get("tags", {}).get("name")
        if n:
            by_name.setdefault(n, []).append(el)

    spots = []
    for spot, names, status, note in SPOTS:
        found = []
        for n in names:
            for el in by_name.get(n, []):
                g = geometry(el, nodes, ways)
                if g is not None and not g.is_empty:
                    found.append(describe(el, to_campus_geom(g)))
        entry = {"spot": spot, "status": status, "note": note, "searched": names}
        if not found:
            entry["status"] = "unknown"
            entry["match"] = None
        elif status == "group":
            entry["members"] = found
            pts = [Point(f["centroid"]) for f in found]
            c = shapely.MultiPoint(pts).centroid
            entry["match"] = {"name": spot, "osm": None, "building_id": None, "kind": "group",
                              "centroid": [round(c.x, 2), round(c.y, 2)], "tile": fr.tile_of(c.x, c.y)}
        else:
            entry["match"] = found[0]
            if len(found) > 1:
                entry["status"] = "ambiguous"
                entry["candidates"] = found[1:]
            elif entry["status"] is None:
                entry["status"] = "point" if found[0]["geometry"] == "Point" else "ok"
        spots.append(entry)

    out = {"source": snap.relative_to(root).as_posix(),
           "standard": "STANDARDS.md section 3 (starting list; Stage 2 may add and confirms names)",
           "frame": "campus meters, config/frame.json",
           "spots": spots}
    save_json(args.out, out)
    for s in spots:
        m = s["match"] or {}
        print(f"{s['status']:9} {s['spot']:34} {m.get('osm') or '-':18} {m.get('tile') or '-':10} {m.get('name') or ''}")
    print(f"wrote {args.out}")


if __name__ == "__main__":
    main()
