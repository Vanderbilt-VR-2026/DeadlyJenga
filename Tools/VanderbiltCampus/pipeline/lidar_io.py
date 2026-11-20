"""Read the working LiDAR tiles (made by prepare_lidar.py) for any box in the campus frame.

    import lidar_io
    pts = lidar_io.load_points(-50, -50, 50, 50, classes=[2, 6], buffer=5)
    pts["x"], pts["y"], pts["z"]        # campus meters (numpy float64)
    pts["classification"], pts["intensity"], pts["return_number"],
    pts["number_of_returns"], pts["gps_time"]

Tiles live in data_root/derived/lidar/<version>/tiles/T_<col>_<row>.laz and are
listed in derived/lidar/<version>/index.json.
"""
import sys
from pathlib import Path

import laspy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import data_root, fail, load_json  # noqa: E402

FIELDS = ["x", "y", "z", "classification", "intensity", "return_number", "number_of_returns", "gps_time"]
DTYPES = {"x": np.float64, "y": np.float64, "z": np.float64, "classification": np.uint8,
          "intensity": np.uint16, "return_number": np.uint8, "number_of_returns": np.uint8,
          "gps_time": np.float64}


def lidar_dir(version="v1", root=None):
    return Path(root or data_root()) / "derived" / "lidar" / version


def load_index(version="v1", root=None):
    path = lidar_dir(version, root) / "index.json"
    if not path.exists():
        fail(f"{path} is missing; run prepare_lidar.py first")
    return load_json(path)


def tiles_touching(xmin, ymin, xmax, ymax, version="v1", root=None):
    """Index entries of the tiles that overlap the box."""
    idx = load_index(version, root)
    out = []
    for t in idx["tiles"]:
        bx0, by0, bx1, by1 = t["bounds"]
        if bx0 < xmax and bx1 > xmin and by0 < ymax and by1 > ymin:
            out.append(t)
    return out


def read_tile(path):
    """All points of one tile file as a dict of numpy arrays."""
    las = laspy.read(path)
    return {f: np.asarray(getattr(las, f), dtype=DTYPES[f]) for f in FIELDS}


def empty():
    return {f: np.zeros(0, dtype=DTYPES[f]) for f in FIELDS}


def load_points(xmin, ymin, xmax, ymax, classes=None, buffer=0.0, version="v1", root=None):
    """Points inside the box (grown by buffer meters), optionally only some classes."""
    xmin, ymin, xmax, ymax = xmin - buffer, ymin - buffer, xmax + buffer, ymax + buffer
    folder = lidar_dir(version, root)
    parts = []
    for t in tiles_touching(xmin, ymin, xmax, ymax, version, root):
        d = read_tile(folder / t["file"])
        keep = (d["x"] >= xmin) & (d["x"] < xmax) & (d["y"] >= ymin) & (d["y"] < ymax)
        if classes is not None:
            keep &= np.isin(d["classification"], np.asarray(list(classes), dtype=np.uint8))
        parts.append({f: v[keep] for f, v in d.items()})
    if not parts:
        return empty()
    return {f: np.concatenate([p[f] for p in parts]) for f in FIELDS}
