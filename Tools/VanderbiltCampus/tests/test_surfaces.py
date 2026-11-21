"""surfaces.py on a few hand-made OSM elements (campus meters, no archive needed)."""
import sys
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "pipeline"))
import surfaces as sf  # noqa: E402
import vcmesh  # noqa: E402


def square(nid0, x0, y0, s):
    """Node ids and coordinates of a closed square way."""
    pts = [(x0, y0), (x0 + s, y0), (x0 + s, y0 + s), (x0, y0 + s)]
    ids = list(range(nid0, nid0 + 4))
    return ids + [ids[0]], dict(zip(ids, pts))


def make_osm():
    xy, els = {}, []
    lawn, p = square(100, 0, 0, 40)
    xy.update(p)
    els.append({"type": "way", "id": 1, "nodes": lawn, "tags": {"leisure": "park"}})
    bed, p = square(200, 5, 5, 4)
    xy.update(p)
    els.append({"type": "way", "id": 2, "nodes": bed, "tags": {"landuse": "flowerbed"}})
    bld, p = square(300, 20, 20, 10)
    xy.update(p)
    els.append({"type": "way", "id": 3, "nodes": bld, "tags": {"building": "university"}})
    pool, p = square(400, 22, 22, 2)
    xy.update(p)
    els.append({"type": "way", "id": 4, "nodes": pool, "tags": {"amenity": "fountain"}})
    xy.update({500: (0, 15), 501: (40, 15)})
    els.append({"type": "way", "id": 5, "nodes": [500, 501], "tags": {"highway": "primary", "lanes": "4"}})
    xy.update({600: (10, 0), 601: (10, 40)})
    els.append({"type": "way", "id": 6, "nodes": [600, 601], "tags": {"highway": "footway", "surface": "paving_stones"}})
    xy.update({700: (35, 0), 701: (35, 40)})
    els.append({"type": "way", "id": 7, "nodes": [700, 701], "tags": {"highway": "footway", "tunnel": "yes"}})
    xy.update({800: (2, 38)})
    els.append({"type": "node", "id": 800, "tags": {"amenity": "fountain"}})
    ways = {e["id"]: e for e in els if e["type"] == "way"}
    return els, ways, xy


def test_road_width_rules():
    assert sf.road_width({"highway": "primary", "width": "19"}) == (19.0, "width tag")
    assert sf.road_width({"highway": "secondary", "lanes": "2"}) == (2 * sf.LANE_M + 1.0, "lanes tag")
    assert sf.road_width({"highway": "residential"}) == (7.0, "default")
    assert sf.road_width({"highway": "service", "width": "bogus"}) == (5.0, "default")


def test_classify_and_priority():
    els, ways, xy = make_osm()
    items = sf.classify(els, ways, xy)
    layers = {eid: (lay, mat) for lay, mat, g, eid, rule in items}
    assert layers["way/1"] == ("grass", "grass")
    assert layers["way/2"] == ("planting", "planting")
    assert layers["way/3"] == ("building", "paving_concrete")
    assert layers["way/4"] == ("water", "water")
    assert layers["way/5"] == ("road", "asphalt")
    assert layers["way/6"] == ("footway", "paving_brick")
    assert "way/7" not in layers                      # tunnels are skipped
    assert layers["node/800"] == ("water", "water")
    road = next(g for lay, m, g, eid, r in items if eid == "way/5")
    assert abs(road.bounds[3] - road.bounds[1] - (4 * sf.LANE_M + 1.0)) < 0.01

    ids = vcmesh.material_index()
    r = sf.paint_layers(items, 0.0, 0.0, 160, 160, ids)           # 40 m x 40 m at 0.25 m, row 0 = south
    at = lambda X, Y: int(r[int(Y / 0.25), int(X / 0.25)])
    assert at(30, 35) == ids["grass"]                 # park
    assert at(6, 6) == ids["planting"]                # flowerbed above grass
    assert at(10.5, 30) == ids["paving_brick"]        # brick footway above the park
    assert at(10.5, 15) == ids["asphalt"]             # road above footway
    assert at(25, 25) == ids["paving_concrete"]       # building footprint
    assert at(23, 23) == ids["water"]                 # fountain above building
    assert at(35, 30) == ids["grass"]                 # tunnel footway painted nothing
    assert (r != 255).all()                           # the park covers everything here
