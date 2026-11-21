"""make_rasters on tiny synthetic data: grid, DTM/DSM/nDSM, masks, density, hole filling, ortho resampling."""
import json
import sys
from pathlib import Path

import numpy as np
import rasterio

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "pipeline"))
import frame as fr  # noqa: E402
import make_rasters as mr  # noqa: E402


def pts(x, y, z, cls, rn=None):
    n = len(x)
    return {"x": np.asarray(x, float), "y": np.asarray(y, float), "z": np.asarray(z, float),
            "classification": np.asarray(cls, np.uint8),
            "return_number": np.ones(n, np.uint8) if rn is None else np.asarray(rn, np.uint8)}


def test_grid_and_cells():
    g = mr.grid_from_bounds(0, 0, 10, 5, 0.5)
    assert (g["width"], g["height"], g["y_top"]) == (20, 10, 5.0)
    idx = mr.cells_of(np.array([0.1, 9.9, 0.1, 11.0]), np.array([4.9, 0.1, 0.1, 1.0]), g)
    assert list(idx) == [0, 9 * 20 + 19, 9 * 20 + 0, -1]


def test_surfaces_masks_density():
    g = mr.grid_from_bounds(0, 0, 10, 10, 0.5)
    rng = np.random.default_rng(0)
    # Sloped ground z = 2 + 0.1 x, 4 points per cell, except a 3 m x 3 m hole under a "building".
    gx, gy = np.meshgrid(np.arange(0.125, 10, 0.25), np.arange(0.125, 10, 0.25))
    gx, gy = gx.ravel(), gy.ravel()
    hole = (gx > 4) & (gx < 7) & (gy > 4) & (gy < 7)
    gx, gy = gx[~hole], gy[~hole]
    ground = pts(gx, gy, 2 + 0.1 * gx, np.full(gx.size, 2))
    roof = pts(rng.uniform(4.1, 6.9, 200), rng.uniform(4.1, 6.9, 200), np.full(200, 20.0), np.full(200, 6))
    noise = pts([1.1], [1.1], [500.0], [7])
    tree = pts([8.2, 8.2], [8.2, 8.2], [12.0, 9.0], [5, 5], rn=[1, 2])
    acc = mr.Accumulator(g)
    for p in (ground, roof, noise, tree):
        acc.add(p)
    r = mr.build_lidar_rasters(acc)

    xs, ys = mr.cell_centers(g)
    X, Y = np.meshgrid(xs, ys)
    lowest = 2 + 0.1 * (X - 0.125)       # min of the 4 ground points in each cell
    assert np.allclose(r["dtm"][r["dtm_measured"] == 1], lowest[r["dtm_measured"] == 1], atol=1e-5)
    # Filled cells under the roof follow the slope (linear fill of a plane), within a few cm.
    filled = r["dtm_measured"] == 0
    assert filled.sum() > 0 and np.abs(r["dtm"][filled] - lowest[filled]).max() < 0.05
    # DSM: roof at 20, noise ignored, tree uses the first return (12, not 9).
    c = mr.cells_of(np.array([5.5, 1.1, 8.2]), np.array([5.5, 1.1, 8.2]), g)
    dsm = r["dsm"].ravel()
    assert dsm[c[0]] == 20.0 and dsm[c[1]] < 100 and dsm[c[2]] == 12.0
    assert r["ndsm"].ravel()[c[0]] > 17 and (r["ndsm"] >= 0).all()
    assert r["building_mask"].ravel()[c[0]] == 1 and r["building_mask"].ravel()[c[1]] == 0
    assert r["highveg_mask"].ravel()[c[2]] == 1
    # Density: 4 ground points per 0.25 m2 cell = 16 / m2 away from roof, tree and noise.
    assert r["density"].ravel()[mr.cells_of(np.array([2.6]), np.array([8.6]), g)[0]] == 16.0


def test_write_tif_and_sidecar(tmp_path):
    g = mr.grid_from_bounds(-10, -10, 10, 10, 0.5)
    a = np.arange(g["width"] * g["height"], dtype=np.float32).reshape(g["height"], g["width"])
    path = tmp_path / "x.tif"
    mr.save(path, a, g, "m", None, None, "test", "CC0")
    with rasterio.open(path) as src:
        assert src.crs is None and src.transform.c == -10 and src.transform.f == 10
        assert np.array_equal(src.read(1), a)
    side = json.loads(path.with_suffix(".json").read_text())
    assert side["origin_x"] == -10 and side["origin_y"] == 10 and side["cell_m"] == 0.5


def test_ortho_resampling_lands_in_the_right_place():
    # A UTM image whose red = column % 256 and green = row % 256 around the campus origin.
    u0, v0, _ = fr.from_campus(0.0, 0.0, 0.0, 32616)
    a, e = 0.6, -0.6
    c0, f0 = float(u0) - 60.0, float(v0) + 60.0           # center of the top-left pixel
    H = W = 200
    cols, rows = np.meshgrid(np.arange(W), np.arange(H))
    src = np.zeros((H, W, 3), np.uint8)
    src[..., 0] = cols % 256
    src[..., 1] = rows % 256
    g = mr.grid_from_bounds(-30, -30, 30, 30, 0.6)
    out = mr.reproject_ortho(src, a, e, c0, f0, g, block=37)
    # Check a few output cells against the exact source pixel position.
    for r_, c_ in [(0, 0), (50, 50), (99, 10)]:
        xs, ys = mr.cell_centers(g, np.array([r_]))
        u, v, _ = fr.from_campus(xs[c_], ys[0], 0.0, 32616)
        col, row = (float(u) - c0) / a, (float(v) - f0) / e
        assert abs(out[0, r_, c_] - col) <= 1.0 and abs(out[1, r_, c_] - row) <= 1.0
