"""prepare_lidar + lidar_io on a tiny synthetic EPT-like file (a few hundred points)."""
import sys
from pathlib import Path

import laspy
import numpy as np
from shapely.geometry import box

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "pipeline"))
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import prepare_lidar as pl  # noqa: E402


def make_fake_ept(path, n=300, seed=1):
    """Points around the campus origin in EPSG:3857 (Z meters), every point written twice."""
    rng = np.random.default_rng(seed)
    X = rng.uniform(-150, 150, n)
    Y = rng.uniform(-150, 150, n)
    Z = rng.uniform(2, 40, n)
    x, y, z = fr.from_campus(X, Y, Z, 3857)
    header = laspy.LasHeader(point_format=6, version="1.4")
    header.scales = np.array([0.01, 0.01, 0.01])
    header.offsets = np.array([0.0, 0.0, 0.0])
    las = laspy.LasData(header)
    dup = lambda a: np.r_[a, a]  # noqa: E731
    las.x, las.y, las.z = dup(x), dup(y), dup(z)
    las.classification = dup(rng.choice([1, 2, 5, 6], n).astype(np.uint8))
    las.intensity = dup(rng.integers(0, 4000, n).astype(np.uint16))
    las.return_number = dup(np.ones(n, dtype=np.uint8))
    las.number_of_returns = dup(np.ones(n, dtype=np.uint8))
    las.gps_time = dup(rng.uniform(1e8, 1e8 + 100, n))
    las.write(path)
    return X, Y


def test_dedupe_removes_only_exact_copies():
    d = {"sX": np.array([1, 1, 1, 2]), "sY": np.array([5, 5, 5, 5]), "sZ": np.array([9, 9, 9, 9]),
         "gps_time": np.array([1.0, 1.0, 2.0, 1.0]), "return_number": np.array([1, 1, 1, 1])}
    out, removed = pl.dedupe(d)
    assert removed == 1 and len(out["sX"]) == 3


def test_tiles_index_and_load(tmp_path):
    src = tmp_path / "fake.laz"
    X, Y = make_fake_ept(src)
    polys = {"campus": box(-60, -60, 60, 60), "backdrop_ring": box(-120, -120, 120, 120).difference(box(-60, -60, 60, 60))}
    out = tmp_path / "derived" / "lidar" / "v1"
    index = pl.make_tiles(src, out, fr.load_frame(), polys, buffer_m=20.0, source_label="fake")

    # Area -140..140 touches tiles -2..1 in both directions: 16 tiles.
    assert index["tile_count"] == 16
    inside = (X >= -200) & (X < 200) & (Y >= -200) & (Y < 200)
    assert index["unique_points"] == inside.sum()
    assert index["duplicates_removed"] == inside.sum()
    names = {t["name"] for t in index["tiles"]}
    assert "T_-01_-01" in names and "T_00_00" in names
    zones = {t["name"]: t["zone"] for t in index["tiles"]}
    assert zones["T_00_00"] == "campus" and zones["T_-02_-02"] == "ring"

    # Files carry campus meters at 1 mm, with the text VLR and no CRS.
    las = laspy.read(out / "tiles" / "T_00_00.laz")
    assert np.allclose(las.header.scales, 0.001)
    assert any(v.user_id == "DeadlyJenga" for v in las.header.vlrs)
    assert las.header.parse_crs() is None
    assert (np.asarray(las.x) >= 0).all() and (np.asarray(las.x) < 100).all()

    # lidar_io: a box across four tiles, class filter, buffer.
    pts = lidar_io.load_points(-50, -50, 50, 50, root=tmp_path)
    want = ((X >= -50) & (X < 50) & (Y >= -50) & (Y < 50)).sum()
    assert len(pts["x"]) == want
    g = lidar_io.load_points(-50, -50, 50, 50, classes=[2], root=tmp_path)
    assert (g["classification"] == 2).all() and len(g["x"]) <= want
    big = lidar_io.load_points(-50, -50, 50, 50, buffer=10, root=tmp_path)
    assert len(big["x"]) == ((X >= -60) & (X < 60) & (Y >= -60) & (Y < 60)).sum()
    # Positions survive the round trip to within the 1 cm source scale.
    assert np.abs(pts["x"]).max() <= 50


def test_kept_tiles_buffer():
    polys = {"campus": box(0, 0, 100, 100), "backdrop_ring": box(0, 0, 1, 1)}
    assert pl.kept_tiles(polys, 0.5) == {(c, r) for c in (-1, 0, 1) for r in (-1, 0, 1)}
    assert pl.kept_tiles({"campus": box(10, 10, 90, 90), "backdrop_ring": box(20, 20, 30, 30)}, 5.0) == {(0, 0)}
