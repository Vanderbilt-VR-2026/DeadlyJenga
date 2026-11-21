"""The campus frame: one local coordinate system for the whole Vanderbilt map.

Axes (STANDARDS.md section 1; frame definition decided in Stage 0):
  X = grid east, Y = grid north (EPSG:6576 grid north), Z = up, all in meters.
  Blender uses (X, Y, Z) as is. Unity uses (X, Z, Y): Unity x = X, y = Z, z = Y.

Horizontal: X = (E - E0) * ftUS / k,  Y = (N - N0) * ftUS / k
  E, N are EPSG:6576 coordinates (NAD83(2011) / Tennessee, US survey feet),
  E0, N0 the origin (center of Alumni Lawn), ftUS = 0.3048006096012192 m,
  k = grid scale factor * elevation factor at the origin, so campus-frame
  distances are true ground meters.
Vertical:   Z = H_ftUS * ftUS - Z0  (H = NAVD88 height). Z0 puts the lowest
  LiDAR ground point inside boundary + ring at Z = 2.0 m.

All numbers live in config/frame.json. "build" computes them; the other
commands only read them.

Commands:
  build                         compute config/frame.json from OSM + LiDAR
  convert --epsg E x y z        source -> campus (add --inverse for campus -> source)
  tile X Y                      tile name for a campus point
  show                          print frame.json

Source systems for convert / to_campus / from_campus:
  6576   E, N in ftUS, Z = NAVD88 ftUS (original D22 LiDAR tiles)
  4326   lon, lat (degrees), Z = NAVD88 meters (OSM)
  3857   web mercator meters, Z = NAVD88 meters (USGS EPT copies; checked against the
         original D22 tile: the EPT stores Z in meters, not feet); --z-unit ftUS if needed
  32616  UTM 16N meters, Z = NAVD88 meters (orthophotos)
The NAD83(2011) <-> WGS 84 step is PROJ's null transform (same as USGS EPT used).
"""
import argparse
import json
import math
import re
import sys
from functools import lru_cache
from pathlib import Path

import numpy as np
from pyproj import Proj, Transformer

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CONFIG, fail, load_json, save_json  # noqa: E402

FTUS = 0.3048006096012192          # one US survey foot in meters
GRID_EPSG = 6576                   # NAD83(2011) / Tennessee (ftUS)
TILE = 100.0                       # tile size in meters
FRAME_PATH = CONFIG / "frame.json"
# Default Z unit for each source system (see the module help).
Z_UNITS = {6576: "ftUS", 4326: "m", 3857: "m", 32616: "m"}

_frame = None


def load_frame(path=None):
    """Read frame.json (cached). Tests may pass their own path."""
    global _frame
    if path is not None:
        return load_json(path)
    if _frame is None:
        if not FRAME_PATH.exists():
            fail(f"{FRAME_PATH} is missing; run frame.py build")
        _frame = load_json(FRAME_PATH)
    return _frame


@lru_cache(maxsize=None)
def _to_grid(epsg):
    return Transformer.from_crs(epsg, GRID_EPSG, always_xy=True)


@lru_cache(maxsize=None)
def _from_grid(epsg):
    return Transformer.from_crs(GRID_EPSG, epsg, always_xy=True)


def _z_unit(epsg, z_unit):
    if epsg not in Z_UNITS:
        raise ValueError(f"unsupported EPSG {epsg}; use one of {sorted(Z_UNITS)}")
    unit = z_unit or Z_UNITS[epsg]
    if unit not in ("ftUS", "m"):
        raise ValueError("z_unit must be 'ftUS' or 'm'")
    return unit


def grid_to_campus(E, N, H_ftus, frame=None):
    """EPSG:6576 (E, N, H all ftUS) -> campus (X, Y, Z) meters."""
    f = frame or load_frame()
    k = f["horizontal"]["k"]
    E0, N0 = f["origin"]["E_ftUS"], f["origin"]["N_ftUS"]
    X = (np.asarray(E, dtype=float) - E0) * FTUS / k
    Y = (np.asarray(N, dtype=float) - N0) * FTUS / k
    Z = np.asarray(H_ftus, dtype=float) * FTUS - f["vertical"]["Z0_m"]
    return X, Y, Z


def campus_to_grid(X, Y, Z, frame=None):
    """Campus (X, Y, Z) meters -> EPSG:6576 (E, N, H all ftUS)."""
    f = frame or load_frame()
    k = f["horizontal"]["k"]
    E = np.asarray(X, dtype=float) * k / FTUS + f["origin"]["E_ftUS"]
    N = np.asarray(Y, dtype=float) * k / FTUS + f["origin"]["N_ftUS"]
    H = (np.asarray(Z, dtype=float) + f["vertical"]["Z0_m"]) / FTUS
    return E, N, H


def to_campus(x, y, z, epsg, z_unit=None, frame=None):
    """Source coordinates -> campus (X, Y, Z). Works on numbers or numpy arrays."""
    unit = _z_unit(epsg, z_unit)
    z = np.asarray(z, dtype=float)
    H_ftus = z if unit == "ftUS" else z / FTUS
    if epsg == GRID_EPSG:
        E, N = np.asarray(x, dtype=float), np.asarray(y, dtype=float)
    else:
        E, N = _to_grid(epsg).transform(np.asarray(x, dtype=float), np.asarray(y, dtype=float))
    return grid_to_campus(E, N, H_ftus, frame)


def from_campus(X, Y, Z, epsg, z_unit=None, frame=None):
    """Campus (X, Y, Z) -> source coordinates (inverse of to_campus)."""
    unit = _z_unit(epsg, z_unit)
    E, N, H_ftus = campus_to_grid(X, Y, Z, frame)
    z = H_ftus if unit == "ftUS" else H_ftus * FTUS
    if epsg == GRID_EPSG:
        return E, N, z
    x, y = _from_grid(epsg).transform(E, N)
    return np.asarray(x), np.asarray(y), z


def campus_to_unity(X, Y, Z):
    """Campus (X east, Y north, Z up) -> Unity (x, y, z) = (X, Z, Y)."""
    return X, Z, Y


def unity_to_campus(x, y, z):
    """Unity (x, y up, z north) -> campus (X, Y, Z) = (x, z, y)."""
    return x, z, y


# ---- tiles ---------------------------------------------------------------
# A tile is 100 m x 100 m. col = floor(X / 100), row = floor(Y / 100).
# Name: "T_<col>_<row>", each number written as an optional minus sign then
# at least two digits of its absolute value: T_03_-02, T_-01_00, T_123_-07.
# (This is not C's "%02d": that would print -2 as "-2", not "-02".)
TILE_RE = re.compile(r"^T_(-?\d{2,})_(-?\d{2,})$")


def _fmt(v):
    v = int(v)
    return ("-" if v < 0 else "") + "%02d" % abs(v)


def tile_index(X, Y):
    """(col, row) of the tile holding a campus point."""
    return int(math.floor(X / TILE)), int(math.floor(Y / TILE))


def tile_name(col, row):
    return f"T_{_fmt(col)}_{_fmt(row)}"


def tile_of(X, Y):
    """Tile name for a campus point, e.g. tile_of(350, -120) == 'T_03_-02'."""
    return tile_name(*tile_index(X, Y))


def parse_tile(name):
    m = TILE_RE.match(name)
    if not m:
        raise ValueError(f"not a tile name: {name!r}")
    return int(m.group(1)), int(m.group(2))


def tile_bounds(name):
    """(xmin, ymin, xmax, ymax) of a tile in campus meters."""
    col, row = parse_tile(name)
    return col * TILE, row * TILE, (col + 1) * TILE, (row + 1) * TILE


# ---- campus boundary in the campus frame ---------------------------------

def boundary_polygons(path=None, frame=None):
    """config/boundary.geojson as shapely polygons in campus meters: {"campus": ..., "backdrop_ring": ...}."""
    import shapely
    from shapely.geometry import shape
    gj = load_json(path or CONFIG / "boundary.geojson")
    out = {}
    for feat in gj["features"]:
        geom = shape(feat["geometry"])
        out[feat["properties"]["role"]] = shapely.transform(
            geom, lambda x, y: to_campus(x, y, np.zeros_like(x), 4326, frame=frame)[:2], interleaved=False)
    return out


# ---- build frame.json ----------------------------------------------------

def gaussian_radius(lat_deg, a=6378137.0, f=1 / 298.257222101):
    """Mean radius of curvature sqrt(M * N) on GRS80 at a latitude (meters)."""
    e2 = f * (2 - f)
    s2 = math.sin(math.radians(lat_deg)) ** 2
    M = a * (1 - e2) / (1 - e2 * s2) ** 1.5
    Nn = a / math.sqrt(1 - e2 * s2)
    return math.sqrt(M * Nn)


def build(args):
    """Compute frame.json: origin, scale factor, Z0."""
    import laspy
    import shapely
    from shapely.geometry import shape
    from shapely.ops import unary_union
    from common import data_root
    import make_boundary as mb

    root = data_root()
    snap = mb.newest_snapshot(root)
    data = load_json(snap)
    nodes, ways, rels = mb.index(data["elements"])
    to6576 = Transformer.from_crs(4326, GRID_EPSG, always_xy=True)

    # 1. Origin: centroid of the OSM "Alumni Lawn" area, computed in EPSG:6576.
    lawn = [el for el in data["elements"] if el.get("tags", {}).get("name") == args.origin_name
            and el["type"] in ("way", "relation")]
    if len(lawn) != 1:
        fail(f"expected one OSM area named {args.origin_name!r}, found {len(lawn)}")
    lawn = lawn[0]
    poly_ll = mb.area_of(lawn, nodes, ways)
    poly_grid = mb.transform(to6576.transform, poly_ll)
    c = poly_grid.centroid
    E0, N0 = round(c.x, 2), round(c.y, 2)
    lon0, lat0 = Transformer.from_crs(GRID_EPSG, 4326, always_xy=True).transform(E0, N0)

    # 2. Scale: grid factor of the Lambert projection, times the elevation factor.
    fac = Proj(GRID_EPSG).get_factors(lon0, lat0)
    grid_factor = fac.meridional_scale
    convergence = fac.meridian_convergence

    # 3. LiDAR ground (class 2): lowest point in boundary + ring, and the origin's ground.
    boundary = load_json(CONFIG / "boundary.geojson")
    area_ll = unary_union([shape(f["geometry"]) for f in boundary["features"]])
    area_grid = mb.transform(to6576.transform, area_ll)
    shapely.prepare(area_grid)
    ept = root / args.ept
    from3857 = Transformer.from_crs(3857, GRID_EPSG, always_xy=True)
    lowest = None          # (H_ftUS, E, N)
    near = []              # ground heights (ftUS) within 3 m of the origin
    # The EPT copy stores Z in meters; convert to ftUS so the numbers match the grid.
    all_h = []
    radius_ft = 3.0 / FTUS
    with laspy.open(ept) as f:
        for chunk in f.chunk_iterator(10_000_000):
            g = np.asarray(chunk.classification) == 2
            if not g.any():
                continue
            E, N = from3857.transform(np.asarray(chunk.x)[g], np.asarray(chunk.y)[g])
            H = np.asarray(chunk.z)[g] / FTUS
            d2 = (E - E0) ** 2 + (N - N0) ** 2
            near.append(H[d2 <= radius_ft ** 2])
            inside = shapely.contains_xy(area_grid, E, N)
            if inside.any():
                Hi = H[inside]
                i = int(np.argmin(Hi))
                if lowest is None or Hi[i] < lowest[0]:
                    lowest = (float(Hi[i]), float(E[inside][i]), float(N[inside][i]))
                all_h.append(Hi)
    near = np.concatenate(near)
    all_h = np.concatenate(all_h)
    if near.size == 0 or lowest is None:
        fail("no LiDAR ground points near the origin or inside the boundary")
    ground0_ft = float(np.median(near))
    ground0_m = ground0_ft * FTUS
    hmin_m = lowest[0] * FTUS
    hmax_m = float(all_h.max()) * FTUS
    Z0 = round(hmin_m - args.lowest_z, 2)

    # Elevation factor R / (R + h), h = ellipsoid height = NAVD88 height + geoid height.
    R = gaussian_radius(lat0)
    h = ground0_m + args.geoid_m
    elev_factor = R / (R + h)
    k = grid_factor * elev_factor

    frame = {
        "version": 1,
        "name": "Vanderbilt campus frame v1",
        "axes": {
            "X": "grid east (EPSG:6576 grid, meters)",
            "Y": "grid north (EPSG:6576 grid north, meters; not true north)",
            "Z": "up (NAVD88 height minus Z0, meters)",
            "blender": "(X, Y, Z) unchanged, Z up",
            "unity": "Unity (x, y, z) = (X, Z, Y); Unity y is up",
        },
        "origin": {
            "osm_element": f"{lawn['type']}/{lawn['id']}",
            "osm_name": args.origin_name,
            "osm_snapshot": snap.relative_to(root).as_posix(),
            "method": "area centroid of the OSM polygon computed in EPSG:6576, rounded to 0.01 ftUS",
            "lon": round(lon0, 9), "lat": round(lat0, 9),
            "E_ftUS": E0, "N_ftUS": N0,
            "ground_navd88_m": round(ground0_m, 3),
            "ground_navd88_ftUS": round(ground0_ft, 3),
            "ground_points_within_3m": int(near.size),
            "ground_method": "median NAVD88 height of LiDAR class-2 points within 3 m (horizontal)",
            "Z": round(ground0_m - Z0, 3),
        },
        "horizontal": {
            "grid_epsg": GRID_EPSG,
            "grid_name": "NAD83(2011) / Tennessee (ftUS)",
            "ftUS_m": FTUS,
            "formula": "X = (E - E0) * ftUS_m / k ; Y = (N - N0) * ftUS_m / k",
            "k": k,
            "grid_scale_factor": grid_factor,
            "elevation_factor": elev_factor,
            "earth_radius_m": round(R, 3),
            "radius_method": "GRS80 Gaussian mean radius sqrt(M*N) at the origin latitude",
            "ellipsoid_height_m": round(h, 3),
            "geoid_height_m_assumed": args.geoid_m,
            "geoid_note": "GEOID18 grid not installed; approximate value for Nashville. "
                          "1 m error in h changes k by 1.6e-7 (0.016 mm per 100 m).",
            "meridian_convergence_deg": convergence,
            "rotation_applied": "none (the frame keeps grid north)",
            "datum_shift_wgs84": "PROJ 'NAD83(2011) to WGS 84 (1)' = null transform",
        },
        "vertical": {
            "datum": "NAVD88 (GEOID18)",
            "epsg": 6360,
            "formula": "Z = H_ftUS * ftUS_m - Z0_m",
            "Z0_m": Z0,
            "target_lowest_ground_Z": args.lowest_z,
            "lowest_ground_navd88_m": round(hmin_m, 3),
            "lowest_ground_E_N_ftUS": [round(lowest[1], 2), round(lowest[2], 2)],
            "min_ground_Z": round(hmin_m - Z0, 3),
            "max_ground_Z": round(hmax_m - Z0, 3),
            "ground_Z_p001": round(float(np.percentile(all_h, 0.1)) * FTUS - Z0, 3),
            "ground_points_in_area": int(all_h.size),
            "ground_source": args.ept + " (class 2, inside boundary + ring, duplicates included; EPT Z is meters)",
        },
        "tiles": {
            "size_m": TILE,
            "col": "floor(X / 100)", "row": "floor(Y / 100)",
            "name": "T_<col>_<row>; each number = optional '-' then at least two digits "
                    "of the absolute value (T_03_-02, T_-01_00)",
        },
        "sources": {
            "6576": "E, N ftUS; Z NAVD88 ftUS (original D22 tiles)",
            "4326": "lon, lat; Z NAVD88 m (OSM)",
            "3857": "web mercator m; Z NAVD88 m (D22 and 2011 EPT copies store Z in meters)",
            "32616": "UTM 16N m; Z NAVD88 m (orthophotos)",
        },
    }
    save_json(FRAME_PATH, frame)
    print(json.dumps(frame["origin"], indent=2))
    print(f"k = {k:.12f} (grid {grid_factor:.12f} x elevation {elev_factor:.12f}), "
          f"convergence {convergence:.6f} deg")
    print(f"Z0 = {Z0} m; ground Z {hmin_m - Z0:.3f} .. {hmax_m - Z0:.3f} m")
    print(f"wrote {FRAME_PATH}")


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("build", help="compute config/frame.json")
    b.add_argument("--origin-name", default="Alumni Lawn")
    b.add_argument("--ept", default="raw/lidar/TN_DavidsonCounty_D22/EPT/TN_DavidsonCounty_D22_campus_ring_ept.laz")
    b.add_argument("--lowest-z", type=float, default=2.0, help="campus Z of the lowest ground point")
    b.add_argument("--geoid-m", type=float, default=-30.0, help="approximate GEOID18 height at campus (m)")
    c = sub.add_parser("convert", help="convert one point")
    c.add_argument("--epsg", type=int, required=True, choices=sorted(Z_UNITS))
    c.add_argument("--z-unit", choices=["ftUS", "m"])
    c.add_argument("--inverse", action="store_true", help="input is campus X Y Z")
    c.add_argument("--unity", action="store_true", help="also print Unity (x, y, z)")
    c.add_argument("coords", nargs=3, type=float)
    t = sub.add_parser("tile", help="tile name of a campus point")
    t.add_argument("X", type=float)
    t.add_argument("Y", type=float)
    sub.add_parser("show", help="print frame.json")
    args = p.parse_args(argv)

    if args.cmd == "build":
        build(args)
    elif args.cmd == "convert":
        a, b_, c_ = args.coords
        if args.inverse:
            x, y, z = from_campus(a, b_, c_, args.epsg, args.z_unit)
            print(f"EPSG:{args.epsg}  {float(x):.9f} {float(y):.9f} {float(z):.4f}")
        else:
            X, Y, Z = to_campus(a, b_, c_, args.epsg, args.z_unit)
            print(f"campus  X={float(X):.4f} Y={float(Y):.4f} Z={float(Z):.4f}  tile {tile_of(float(X), float(Y))}")
            if args.unity:
                print("unity   x={:.4f} y={:.4f} z={:.4f}".format(*(float(v) for v in campus_to_unity(X, Y, Z))))
    elif args.cmd == "tile":
        print(tile_of(args.X, args.Y))
    elif args.cmd == "show":
        print(json.dumps(load_frame(), indent=2))


if __name__ == "__main__":
    main()
