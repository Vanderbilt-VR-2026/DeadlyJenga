"""Fast synthetic checks for tree detection and the tree kit (no archive data needed)."""
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
import tree_kit as kit  # noqa: E402
import trees_detect as td  # noqa: E402
import vcmesh  # noqa: E402

CELL = 0.5


def cone_chm(size, cones):
    """CHM grid (rows, cols) with cones given as (x, y, height, radius) in meters; cell centers at (c+0.5)*CELL."""
    yy, xx = np.mgrid[0:size, 0:size]
    X, Y = (xx + 0.5) * CELL, (yy + 0.5) * CELL
    chm = np.zeros((size, size), np.float32)
    for x, y, h, r in cones:
        d = np.hypot(X - x, Y - y)
        chm = np.maximum(chm, np.where(d < r, h * (1 - 0.7 * d / r), 0)).astype(np.float32)
    return chm


def to_xy(r, c):
    return (c + 0.5) * CELL, (r + 0.5) * CELL


def test_three_cones():
    # Cone apexes on cell centres, so the raster really contains each apex height.
    cones = [(10.25, 10.25, 12.0, 4.0), (30.25, 12.25, 8.0, 3.0), (20.25, 32.25, 18.0, 5.0)]
    S = td.smooth_chm(cone_chm(90, cones), CELL, 0.5)
    r, c, h = td.detect_tops(S, CELL, 3.0, 1.5, 0.12)
    assert len(r) == 3
    found = sorted(zip(*to_xy(r, c), h), key=lambda t: t[0])
    for (x, y, hh), (cx, cy, ch, _) in zip(found, sorted(cones)):
        assert np.hypot(x - cx, y - cy) <= 0.5
    # Height = highest raw CHM value within 1 m of the top (as trees_detect reports it), within 0.3 m.
    # (The smoothed CHM only places the top; it lowers sharp synthetic peaks.)
    raw = cone_chm(90, cones)
    from scipy.ndimage import maximum_filter
    hr = maximum_filter(raw, size=5)[r, c]
    for hh, (_, _, ch, _) in zip(sorted(hr), sorted(cones, key=lambda t: t[2])):
        assert abs(hh - ch) <= 0.3


def test_merged_crowns_are_separated():
    # Two overlapping crowns 6 m apart: two tops, two watershed regions split between them.
    cones = [(10.0, 15.0, 14.0, 5.0), (16.0, 15.0, 13.0, 5.0)]
    S = td.smooth_chm(cone_chm(60, cones), CELL, 0.5)
    r, c, h = td.detect_tops(S, CELL, 3.0, 1.5, 0.12)
    assert len(r) == 2
    labels = td.delineate(S, r, c, CELL)
    areas = td.crown_areas(labels, 2, CELL)
    assert (areas > 20).all()
    # The boundary between them lies between the two tops.
    row = int(15.0 / CELL)
    lab_row = labels[row]
    left = np.nonzero(lab_row == labels[r[np.argmin(c)], c.min()])[0]
    assert (left + 0.5).max() * CELL < 16.0 and (left + 0.5).max() * CELL > 10.0


def test_point_stats():
    lab = np.array([1, 1, 1, 1, 2, 2])
    x = np.array([0, 1, 2, 3, 10, 11.0]); y = np.zeros(6)
    h = np.array([2, 4, 6, 10, 5, 6.0]); veg = np.array([1, 1, 1, 0, 1, 1], bool)
    st = td.point_stats(lab, x, y, h, veg, 2)
    assert st["count"].tolist() == [4, 2]
    assert abs(st["veg_frac"][0] - 0.75) < 1e-9
    assert st["top_x"][0] == 3.0


def test_kit_meshes_pass_checks():
    for (size, shape, lod), m in kit.all_kits().items():
        assert vcmesh.check_mesh(m) == []
        assert len(m["faces"]) <= (60 if lod == 0 else 16)
        assert vcmesh.uv_overlap_area(m["uv0"], grid=256) < 0.02


def test_kit_pieces_are_closed_and_outward():
    import trimesh
    for p in (kit.icosahedron((0, 0, 0), (1, 1, 1)), kit.bipyramid((0, 0, 0), (1, 1, 1)),
              kit.cone(0, 1, 2), kit.octahedron((0, 0, 0), (1, 1, 1)), kit.frustum(6, 1, 1, caps=True)):
        v, f, inside = p
        tm = trimesh.Trimesh(v, kit.orient(v, f, inside), process=True)
        assert tm.is_watertight and tm.volume > 0


def test_tree_mesh_placement():
    t = {"id": "TR_00001", "x": 100.0, "y": -50.0, "ground_z": 20.0, "height": 15.0, "crown_diameter": 9.0,
         "crown_base": 4.0}
    m = kit.tree_mesh(t, 0)
    v = m["verts"]
    assert abs(v[:, 2].max() - 35.0) < 0.05
    assert v[:, 2].min() < 20.0                         # trunk sinks below ground
    assert np.hypot(v[:, 0] - 100, v[:, 1] + 50).max() <= 9.0 / 2 * 1.5
    assert vcmesh.check_mesh(m) == []
    assert kit.shape_of({"height": 15, "crown_diameter": 4}) == "conical"
    c = kit.collider_mesh(t)
    assert vcmesh.check_mesh(c, need_watertight=True) == []
