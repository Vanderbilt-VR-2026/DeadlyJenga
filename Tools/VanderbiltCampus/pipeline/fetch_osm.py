"""Download a dated OpenStreetMap snapshot of Vanderbilt main campus + Peabody.

Steps:
1. Ask Overpass for the amenity=university area named "Vanderbilt University"
   and the "Peabody College" feature (the "seed"). Saved as campus_seed_overpass.json.
2. Make a bounding box around them, padded by the 150 m backdrop ring plus a
   margin (default 250 m in total), measured in meters (UTM zone 16N).
3. Download everything OSM has in that box: buildings and building parts
   (with height, levels, roof tags), roads and paths, landuse/leisure/natural,
   parking, fountains, trees, barriers and walls, surfaces, names.

Output (write-once, recorded in the manifest, stage 0):
  raw/osm/<YYYY-MM-DD>/campus_seed_overpass.json  + campus_seed_query.overpassql
  raw/osm/<YYYY-MM-DD>/campus_overpass.json       + campus_query.overpassql
Running again on the same day skips files already downloaded.

License: ODbL 1.0, "© OpenStreetMap contributors".
"""
import argparse
import json
import sys
from pathlib import Path

from pyproj import Transformer

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import data_root, fail  # noqa: E402
import manifest  # noqa: E402
import netfetch  # noqa: E402

# Main public Overpass server first; the second is another public instance listed on
# the OSM wiki (same OSM data), used only when the first is overloaded.
OVERPASS = ["https://overpass-api.de/api/interpreter",
            "https://overpass.private.coffee/api/interpreter"]
LICENSE = "ODbL-1.0; (c) OpenStreetMap contributors"

# Rough search box for the seed query only (south, west, north, east).
SEED_BOX = (36.12, -86.83, 36.17, -86.78)

SEED_QUERY = """[out:json][timeout:120][bbox:{s},{w},{n},{e}];
(
  nwr["amenity"="university"]["name"~"Vanderbilt"];
  nwr["amenity"~"^(university|college)$"]["name"~"Peabody"];
);
out body geom;
"""

# Everything in the box. Ways and multipolygon/building relations are recursed
# down to their nodes so every shape is complete; other relations (bus routes,
# city limits) are listed with their members but not recursed (that would pull
# in the whole county).
CAMPUS_QUERY = """[out:json][timeout:300][bbox:{s},{w},{n},{e}];
(
  node;
  way;
  rel[type~"^(multipolygon|building)$"];
)->.a;
(.a; .a >;)->.b;
.b out body;
rel[type!~"^(multipolygon|building)$"];
out body;
"""


def seed_bbox(seed):
    """Lat/lon bounds of all seed features."""
    lats, lons = [], []
    for el in seed["elements"]:
        if el["type"] == "node":
            lats.append(el["lat"])
            lons.append(el["lon"])
        for g in el.get("geometry", []) or []:
            lats.append(g["lat"])
            lons.append(g["lon"])
        for m in el.get("members", []) or []:
            for g in m.get("geometry", []) or []:
                lats.append(g["lat"])
                lons.append(g["lon"])
    if not lats:
        fail("seed query found no Vanderbilt/Peabody features")
    return min(lats), min(lons), max(lats), max(lons)


def pad_bbox(s, w, n, e, pad_m):
    """Grow a lat/lon box by pad_m meters on every side (computed in UTM 16N)."""
    to_utm = Transformer.from_crs(4326, 32616, always_xy=True)
    to_ll = Transformer.from_crs(32616, 4326, always_xy=True)
    xs, ys = zip(*[to_utm.transform(x, y) for x, y in ((w, s), (e, s), (e, n), (w, n))])
    x0, y0, x1, y1 = min(xs) - pad_m, min(ys) - pad_m, max(xs) + pad_m, max(ys) + pad_m
    corners = [to_ll.transform(x, y) for x, y in ((x0, y0), (x1, y0), (x1, y1), (x0, y1))]
    lons, lats = zip(*corners)
    return round(min(lats), 6), round(min(lons), 6), round(max(lats), 6), round(max(lons), 6)


def run_query(query, urls):
    """Try each Overpass server in turn. Returns (raw bytes, parsed JSON, server used)."""
    r = None
    for url in urls:
        print(f"Overpass: {url}")
        try:
            r = netfetch.get(url, data={"data": query}, tries=3, wait=30, timeout=400)
            break
        except SystemExit:
            print(f"  {url} failed; trying the next server")
    if r is None:
        fail("every Overpass server failed")
    try:
        data = r.json()
    except ValueError:
        fail("Overpass did not return JSON: " + r.text[:300])
    if data.get("remark") and "error" in data["remark"].lower():
        fail("Overpass error: " + data["remark"])
    return r.content, data, url


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--date", default=netfetch.today(), help="snapshot folder name (default: today)")
    p.add_argument("--pad", type=float, default=250.0,
                   help="meters added around campus: 150 m ring + margin (default 250)")
    p.add_argument("--overpass", nargs="+", default=OVERPASS, help="Overpass interpreter URL(s), tried in order")
    args = p.parse_args(argv)

    root = data_root()
    folder = root / "raw" / "osm" / args.date
    db = manifest.connect(root)

    # 1. Seed: the university polygon(s).
    seed_q = SEED_QUERY.format(s=SEED_BOX[0], w=SEED_BOX[1], n=SEED_BOX[2], e=SEED_BOX[3])
    seed_path = folder / "campus_seed_overpass.json"
    if netfetch.already_have(seed_path, db, root):
        print(f"have {seed_path}")
        seed = json.loads(seed_path.read_text(encoding="utf-8"))
    else:
        content, seed, used = run_query(seed_q, args.overpass)
        netfetch.save_bytes_once(content, seed_path, used, LICENSE, db, root)
        netfetch.save_bytes_once(seed_q.encode(), folder / "campus_seed_query.overpassql",
                                 "query text written by fetch_osm.py", LICENSE, db, root)
    for el in seed["elements"]:
        print(f"  seed: {el['type']}/{el['id']} {el.get('tags', {}).get('name')}")

    # 2. Box = seed bounds + padding.
    s, w, n, e = pad_bbox(*seed_bbox(seed), args.pad)
    print(f"bbox (S,W,N,E) = {s}, {w}, {n}, {e}")

    # 3. Everything in the box.
    out = folder / "campus_overpass.json"
    if netfetch.already_have(out, db, root):
        print(f"have {out}; skipping")
    else:
        q = CAMPUS_QUERY.format(s=s, w=w, n=n, e=e)
        content, data, used = run_query(q, args.overpass)
        netfetch.save_bytes_once(content, out, used, LICENSE, db, root)
        netfetch.save_bytes_once(q.encode(), folder / "campus_query.overpassql",
                                 "query text written by fetch_osm.py", LICENSE, db, root)
        counts = {}
        for el in data["elements"]:
            counts[el["type"]] = counts.get(el["type"], 0) + 1
        print(f"saved {out} ({len(content) / 1e6:.1f} MB): {counts}")
        print(f"  timestamp_osm_base: {data.get('osm3s', {}).get('timestamp_osm_base')}")
    db.close()


if __name__ == "__main__":
    main()
