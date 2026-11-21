"""Write tests/frame_points.json: 10 known points and their expected campus coordinates.

The expected values are computed here by a separate path that does NOT call
pipeline/frame.py: the Tennessee Lambert projection is written out as a PROJ
string (meters), web mercator is inverted by hand, and the frame formula is
written longhand. Only the frame constants (E0, N0, k, Z0) come from
config/frame.json, because they are the definition being tested.

Run: python tests/make_frame_points.py   (needs the archive for the LiDAR points)
"""
import json
import math
import sys
from pathlib import Path

import laspy
import numpy as np
from pyproj import Transformer

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parent
sys.path.insert(0, str(TOOLS / "pipeline"))
from common import data_root  # noqa: E402  (only to find the archive files)

FT = 1200.0 / 3937.0      # US survey foot, by definition
# EPSG:6576 written out longhand, in meters (false easting 1968500 ftUS = 600000 m).
LCC = ("+proj=lcc +lat_1=36.41666666666666 +lat_2=35.25 +lat_0=34.33333333333334 "
       "+lon_0=-86 +x_0=600000 +y_0=0 +ellps=GRS80 +units=m +no_defs")
lonlat_to_lcc = Transformer.from_proj("+proj=longlat +ellps=GRS80 +no_defs", LCC, always_xy=True)

frame = json.loads((TOOLS / "config" / "frame.json").read_text())
E0m = frame["origin"]["E_ftUS"] * FT
N0m = frame["origin"]["N_ftUS"] * FT
k = frame["horizontal"]["k"]
Z0 = frame["vertical"]["Z0_m"]


def expected_from_lonlat(lon, lat, h_m):
    e, n = lonlat_to_lcc.transform(lon, lat)
    return [(e - E0m) / k, (n - N0m) / k, h_m - Z0]


def mercator_inverse(x, y):
    R = 6378137.0
    lon = math.degrees(x / R)
    lat = math.degrees(2 * math.atan(math.exp(y / R)) - math.pi / 2)
    return lon, lat


def main():
    root = data_root()
    osm = json.loads((root / "raw/osm/2026-11-19/campus_overpass.json").read_text(encoding="utf-8"))
    nodes = {e["id"]: e for e in osm["elements"] if e["type"] == "node"}
    ways = {e["id"]: e for e in osm["elements"] if e["type"] == "way"}
    pts = []

    o = frame["origin"]
    pts.append({"name": "origin (Alumni Lawn centroid)", "epsg": 4326,
                "source": [o["lon"], o["lat"], o["ground_navd88_m"]]})

    boundary = json.loads((TOOLS / "config/boundary.geojson").read_text())
    ring = [f for f in boundary["features"] if f["properties"]["role"] == "backdrop_ring"][0]
    outer = ring["geometry"]["coordinates"][0]
    for label, key in [("west", lambda p: p[0]), ("east", lambda p: -p[0]),
                       ("south", lambda p: p[1]), ("north", lambda p: -p[1])]:
        p = min(outer, key=key)
        pts.append({"name": f"boundary+ring {label}-most vertex", "epsg": 4326, "source": [p[0], p[1], 160.0]})

    for name in ["Kirkland Hall", "Cohen Memorial Hall", "Furman Hall"]:
        w = [e for e in ways.values() if e.get("tags", {}).get("name") == name and "building" in e["tags"]][0]
        n = nodes[w["nodes"][0]]
        pts.append({"name": f"{name} corner (way/{w['id']} node/{n['id']})", "epsg": 4326,
                    "source": [n["lon"], n["lat"], 170.0]})

    # One EPT point (EPSG:3857, Z meters) and one original-tile point (EPSG:6576, Z ftUS).
    ept = root / "raw/lidar/TN_DavidsonCounty_D22/EPT/TN_DavidsonCounty_D22_campus_ring_ept.laz"
    with laspy.open(ept) as f:
        c = next(f.chunk_iterator(200_000))
        i = int(np.argmax(np.asarray(c.classification) == 6))
        pts.append({"name": "D22 EPT point (class 6)", "epsg": 3857, "z_unit": "m",
                    "source": [float(c.x[i]), float(c.y[i]), float(c.z[i])]})
    tile = root / "raw/lidar/TN_DavidsonCounty_D22/LAZ/USGS_LPC_TN_DavidsonCounty_D22_092150.laz"
    with laspy.open(tile) as f:
        c = next(f.chunk_iterator(200_000))
        i = int(np.argmax(np.asarray(c.classification) == 2))
        pts.append({"name": "D22 original tile 092150 point (class 2)", "epsg": 6576, "z_unit": "ftUS",
                    "source": [float(c.x[i]), float(c.y[i]), float(c.z[i])]})

    for p in pts:
        x, y, z = p["source"]
        if p["epsg"] == 4326:
            p["expected"] = expected_from_lonlat(x, y, z)
        elif p["epsg"] == 3857:
            lon, lat = mercator_inverse(x, y)
            p["expected"] = expected_from_lonlat(lon, lat, z)
        elif p["epsg"] == 6576:
            p["expected"] = [(x * FT - E0m) / k, (y * FT - N0m) / k, z * FT - Z0]
        p["expected"] = [round(v, 6) for v in p["expected"]]

    out = {"note": "Expected campus coordinates computed by tests/make_frame_points.py "
                   "(PROJ string + longhand formula, not frame.py).",
           "frame_version": frame["version"], "points": pts}
    (HERE / "frame_points.json").write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {len(pts)} points to {HERE / 'frame_points.json'}")


if __name__ == "__main__":
    main()
