"""Campus frame: 10 known points (expected values from an independent path),
round trips, Unity axes and tile names."""
import json
import sys
from pathlib import Path

import numpy as np
import pytest

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "pipeline"))
import frame  # noqa: E402

POINTS = json.loads((HERE / "frame_points.json").read_text())["points"]


def test_ten_points():
    assert len(POINTS) == 10


@pytest.mark.parametrize("p", POINTS, ids=[p["name"] for p in POINTS])
def test_known_point_within_1cm(p):
    X, Y, Z = frame.to_campus(*p["source"], p["epsg"], p.get("z_unit"))
    err = np.hypot(np.hypot(X - p["expected"][0], Y - p["expected"][1]), Z - p["expected"][2])
    assert err < 0.01, f"{p['name']}: off by {err:.4f} m"


@pytest.mark.parametrize("p", POINTS, ids=[p["name"] for p in POINTS])
def test_round_trip_within_1mm(p):
    X, Y, Z = frame.to_campus(*p["source"], p["epsg"], p.get("z_unit"))
    x, y, z = frame.from_campus(X, Y, Z, p["epsg"], p.get("z_unit"))
    X2, Y2, Z2 = frame.to_campus(x, y, z, p["epsg"], p.get("z_unit"))
    assert max(abs(X2 - X), abs(Y2 - Y), abs(Z2 - Z)) < 0.001


@pytest.mark.parametrize("epsg", [6576, 4326, 3857, 32616])
def test_round_trip_from_campus(epsg):
    X = np.array([0.0, -1100.0, 995.0, 38.0, 416.0])
    Y = np.array([0.0, -672.0, -495.0, -1324.0, 500.0])
    Z = np.array([26.4, 2.0, 59.5, 10.0, 30.0])
    x, y, z = frame.from_campus(X, Y, Z, epsg)
    X2, Y2, Z2 = frame.to_campus(x, y, z, epsg)
    assert np.max(np.abs(np.r_[X2 - X, Y2 - Y, Z2 - Z])) < 0.001


def test_origin_is_zero():
    f = frame.load_frame()
    X, Y, Z = frame.to_campus(f["origin"]["E_ftUS"], f["origin"]["N_ftUS"], 0.0, 6576)
    assert abs(X) < 1e-9 and abs(Y) < 1e-9


def test_true_ground_distance():
    # 100 grid meters east of the origin is 100 / k campus meters.
    f = frame.load_frame()
    k = f["horizontal"]["k"]
    E0, N0 = f["origin"]["E_ftUS"], f["origin"]["N_ftUS"]
    X, _, _ = frame.to_campus(E0 + 100 / frame.FTUS, N0, 0.0, 6576)
    assert X == pytest.approx(100 / k, abs=1e-9)


def test_z_unit_flag():
    a = frame.to_campus(-9662091.91, 4320285.85, 179.93, 3857)               # meters (default)
    b = frame.to_campus(-9662091.91, 4320285.85, 179.93 / frame.FTUS, 3857, "ftUS")
    assert abs(a[2] - b[2]) < 1e-9


def test_unity_axes():
    assert frame.campus_to_unity(1.0, 2.0, 3.0) == (1.0, 3.0, 2.0)
    assert frame.unity_to_campus(*frame.campus_to_unity(1.0, 2.0, 3.0)) == (1.0, 2.0, 3.0)


@pytest.mark.parametrize("X, Y, name", [
    (0.0, 0.0, "T_00_00"),
    (350.0, -120.0, "T_03_-02"),
    (99.999, 99.999, "T_00_00"),
    (100.0, 100.0, "T_01_01"),
    (-0.001, -0.001, "T_-01_-01"),
    (-100.0, -100.0, "T_-01_-01"),
    (-100.001, 5.0, "T_-02_00"),
    (-1100.0, -1324.5, "T_-11_-14"),
    (12345.0, -67.0, "T_123_-01"),
])
def test_tile_names(X, Y, name):
    assert frame.tile_of(X, Y) == name


def test_tile_bounds_and_parse():
    assert frame.tile_bounds("T_03_-02") == (300.0, -200.0, 400.0, -100.0)
    assert frame.tile_bounds("T_-01_00") == (-100.0, 0.0, 0.0, 100.0)
    for X, Y in [(0.5, 0.5), (-250.0, 730.0), (-0.2, -999.9)]:
        xmin, ymin, xmax, ymax = frame.tile_bounds(frame.tile_of(X, Y))
        assert xmin <= X < xmax and ymin <= Y < ymax
    with pytest.raises(ValueError):
        frame.parse_tile("T_3_-2")
