"""Fast checks for the Stage 5 prop library and placement helpers (no archive data needed)."""
import json
import sys
from pathlib import Path

import numpy as np
from shapely.geometry import LineString

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
import props_kit as pk  # noqa: E402
import props_place as pp  # noqa: E402
import vcmesh  # noqa: E402


def test_library_meshes_pass_checks():
    rows, bad = pk.check_all()
    assert bad == 0, [r for r in rows if r[3]]
    assert {r[0] for r in rows} == set(pk.KINDS)


def test_library_uses_known_materials_and_blank_signs():
    mats = vcmesh.material_index()
    sign = pk.prop_mesh("sign_post", 0)
    assert mats["signage_blank"] in set(sign["face_mat"].tolist())
    flag = pk.prop_mesh("flagpole", 0)
    assert mats["signage_blank"] in set(flag["face_mat"].tolist())
    for k in pk.KINDS:
        m = pk.prop_mesh(k, 0)
        assert m["verts"][:, 2].min() >= -0.06        # sits on the ground (basin floor sinks 5 cm)


def test_colliders_closed_and_cover_the_prop():
    for k in pk.KINDS:
        c, v = pk.prop_mesh(k, "col"), pk.prop_mesh(k, 0)
        assert vcmesh.check_mesh(c, need_watertight=True) == []
        # the collider footprint is at least most of the visual footprint
        cw = np.ptp(c["verts"][:, :2], axis=0); vw = np.ptp(v["verts"][:, :2], axis=0)
        if k not in ("flagpole", "lamp_post"):  # flag and lantern head sit above reach: pole-only colliders
            assert (cw >= 0.6 * vw).all(), k


def test_placed_rotation_and_scale():
    m = pk.placed("bench", 0, 10.0, 20.0, 5.0, heading_deg=90.0, scale=1.0)
    # rotated 90 degrees: the 1.8 m long bench now runs along Y
    span = np.ptp(m["verts"][:, :2], axis=0)
    assert span[1] > span[0]
    assert abs(m["verts"][:, 2].min() - 5.0) < 0.05
    b = pk.placed("fountain_basin", 0, 0, 0, 0, scale=2.0)
    assert abs(np.ptp(b["verts"][:, 0]) - 2 * 2 * 2.12) < 0.2


def test_heading_faces_target():
    # front (+Y) pointing toward +X means turning -90 degrees (270)
    assert abs(pp.heading_towards(0, 0, 5, 0) - 270.0) < 1e-6
    assert abs(pp.heading_towards(0, 0, 0, 5) - 0.0) < 1e-6
    assert abs(pp.heading_towards(0, 0, -5, 0) - 90.0) < 1e-6


def test_offset_point_sides():
    line = LineString([(0, 0), (100, 0)])
    x, y, (px, py), t = pp.offset_point(line, 30.0, 1, 1.6)
    assert abs(x - 30) < 1e-6 and abs(y - 1.6) < 1e-6           # left of an eastward path is north
    x, y, _, _ = pp.offset_point(line, 30.0, -1, 1.6)
    assert abs(y + 1.6) < 1e-6


def test_osm_kind_mapping():
    assert pp.osm_kind({"amenity": "bench"}) == "bench"
    assert pp.osm_kind({"amenity": "waste_basket"}) == "bin"
    assert pp.osm_kind({"highway": "street_lamp"}) == "lamp_post"
    assert pp.osm_kind({"tourism": "artwork", "artwork_type": "statue"}) == "statue"
    assert pp.osm_kind({"tourism": "artwork", "artwork_type": "mural"}) is None
    assert pp.osm_kind({"highway": "bus_stop"}) == "sign_post"
    assert pp.osm_kind({"man_made": "flagpole"}) == "flagpole"


def test_placed_spacing():
    pl = pp.Placed()
    pl.add(kind="lamp_post", x=0.0, y=0.0)
    assert pl.near("lamp_post", 5, 5, 12)
    assert not pl.near("lamp_post", 20, 0, 12)
    assert not pl.near("bench", 1, 1, 12)


def test_ground_lookup(tmp_path):
    d = tmp_path / "derived/terrain/v1/T_00_00"
    d.mkdir(parents=True)
    (tmp_path / "derived/terrain/v1/index.json").write_text("{}")
    h = np.fromfunction(lambda j, i: 10 + 0.1 * i + 0.2 * j, (201, 201)).astype(np.float32)
    np.save(d / "heights.npy", h)
    (d / "heights.json").write_text(json.dumps({"origin_x": 0.0, "origin_y": 0.0, "cell_m": 0.5}))
    s = np.full((400, 400), vcmesh.material_index()["grass"], np.uint8)
    np.save(d / "surface.npy", s)
    g = pp.Ground(tmp_path)
    assert abs(g.z(10.25, 20.0) - (10 + 0.1 * 20.5 + 0.2 * 40)) < 1e-4
    assert g.surface(50, 50) == "grass"
    assert g.z(-50, -50) is None
