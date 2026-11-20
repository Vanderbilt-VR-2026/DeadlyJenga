"""Fast synthetic checks for the backdrop-ring LoD1 extrusion (no archive data needed)."""
import sys
from pathlib import Path

import numpy as np
from shapely.geometry import Polygon, box

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
import ring_lod1 as rl  # noqa: E402
import vcmesh  # noqa: E402


def build(polys, z0=10.0, z1=22.0):
    mats = vcmesh.material_index()
    parts = []
    for p in polys:
        for q in rl.clean_poly(p):
            parts.append(rl.extrude(q, z0, z1, mats["brick_buff"], mats["roof_flat"]))
    return parts, rl.pack_tile(parts)


def test_box_is_watertight_with_valid_uvs():
    parts, (mesh, info) = build([box(0, 0, 20, 10)])
    assert vcmesh.check_mesh(mesh, need_watertight=True) == []
    assert len(mesh["faces"]) == 12
    assert info["chart_overlaps"] == 0
    assert vcmesh.uv_overlap_area(mesh["uv0"], grid=256) < 0.02
    import trimesh
    tm = trimesh.Trimesh(mesh["verts"], mesh["faces"], process=True)
    assert abs(tm.volume - 20 * 10 * 12) < 1e-6              # outward-facing and closed


def test_courtyard_and_tile_atlas():
    court = Polygon([(0, 0), (30, 0), (30, 30), (0, 30)], [[(10, 10), (20, 10), (20, 20), (10, 20)]])
    parts, (mesh, info) = build([court, box(40, 0, 50, 8)])
    for v, f, m, _ in parts:
        assert vcmesh.check_mesh({"verts": v, "faces": f, "face_mat": m, "uv0": np.zeros((len(f), 3, 2))},
                                 need_watertight=True) == []
    assert info["chart_overlaps"] == 0
    assert vcmesh.check_mesh(mesh) == []
    assert vcmesh.uv_overlap_area(mesh["uv0"], grid=512) < 0.02


def test_hole_touching_outer_wall_is_opened():
    # A courtyard that touches the outer wall at one corner.
    p = Polygon([(0, 0), (20, 0), (20, 20), (0, 20)], [[(0, 10), (8, 6), (8, 14)]])
    parts, _ = build([p])
    for v, f, m, _ in parts:
        assert vcmesh.check_mesh({"verts": v, "faces": f, "face_mat": m, "uv0": np.zeros((len(f), 3, 2))},
                                 need_watertight=True) == []


def test_height_choice_order():
    assert rl.choose_height({}, {"roof_p90": 30.0, "ground_z": 10.0, "roof_src": "lidar_class6"}) == (20.0, "lidar_class6")
    assert rl.choose_height({"metro_height": 9.0}, {"ground_z": 10.0}) == (9.0, "metro_height")
    assert rl.choose_height({"levels": 3}, {"ground_z": 10.0}) == (9.0, "osm_levels")
    assert rl.choose_height({}, {"ground_z": 10.0}) == (6.0, "default_6m")
