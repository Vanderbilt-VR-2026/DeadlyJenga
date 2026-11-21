"""Footprint helpers on synthetic data: wall direction, moving single walls, LiDAR outline."""
import math
import sys
from pathlib import Path

import numpy as np
from shapely import affinity
from shapely.geometry import Polygon, box

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "pipeline"))
import buildings_common as bc  # noqa: E402


def test_dominant_angle_exact():
    assert abs(bc.dominant_angle(box(0, 0, 20, 10))) < 1e-9
    g = affinity.rotate(box(0, 0, 20, 10), 17, origin=(0, 0))
    assert abs(math.degrees(bc.dominant_angle(g)) - 17) < 1e-6


def test_offset_one_wall_keeps_right_angles():
    xy = np.array([[0, 0], [20, 0], [20, 10], [0, 10]], float)      # CCW
    out = bc.offset_ring_edges(xy, np.array([0.0, 0.5, 0.0, 0.0]))   # east wall out 0.5 m
    p = Polygon(out)
    assert abs(p.area - 20.5 * 10) < 1e-9
    assert np.allclose(sorted(out[:, 0]), [0, 0, 20.5, 20.5])


def test_lidar_edge_signed_distance():
    rng = np.random.default_rng(1)
    n = 6000
    x, y = rng.uniform(-5, 25, n), rng.uniform(-5, 15, n)
    inside = (x > 0) & (x < 20) & (y > 0) & (y < 10)
    pts = {"x": x, "y": y, "z": np.where(inside, 10.0, 0.0),
           "classification": np.where(inside, 6, 2).astype(np.uint8)}
    e = bc.LidarEdge(pts, (-5, -5, 25, 15), hag=np.where(inside, 10.0, 0.0))
    d = e.signed(np.array([[10, -1.0], [10, 5.0], [21.0, 5.0]]))
    assert d[0] > 0.5 and d[1] < -3 and d[2] > 0.5
    assert abs(e.signed(np.array([[20.0, 5.0]]))[0]) < 0.35          # the edge itself
    assert e.coverage(box(0, 0, 20, 10)) > 0.95
