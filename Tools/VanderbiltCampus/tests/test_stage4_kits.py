"""Stage 4 kit pieces: valid meshes at several sizes, sizes follow the parameters (1 mm), merge rule."""
import sys
from pathlib import Path

import numpy as np
import pytest

TOOLS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(TOOLS / "pipeline"))
sys.path.insert(0, str(TOOLS / "blender"))

import vcmesh  # noqa: E402
from kits import KITS, geom, get_kit, pieces  # noqa: E402

MM = 1e-3
SIZES = [(0.8, 1.2), (1.3, 3.4), (2.4, 5.0)]


def bounds(mb):
    lo, hi = mb.bounds()
    return lo, hi


def valid(p):
    m = p.mesh.finish()
    assert vcmesh.check_mesh(m) == []
    assert m["uv0"].min() >= 0 and m["uv0"].max() <= 1
    assert m["meta"]["charts"] >= 1
    return m


@pytest.mark.parametrize("kit_name", sorted(KITS))
@pytest.mark.parametrize("w,h", SIZES)
@pytest.mark.parametrize("arch", ["none", "round", "pointed", "segmental"])
def test_window_sizes(kit_name, w, h, arch):
    kit = get_kit(kit_name)
    m = geom.Merger("3+")
    rect = (1.0, 2.0, 1.0 + w, 2.0 + h)
    p = pieces.window(kit, m, rect, arch=arch)
    valid(p)
    hole = np.asarray(p.hole)
    assert abs(hole[:, 0].min() - rect[0]) < MM and abs(hole[:, 0].max() - rect[2]) < MM
    assert abs(hole[:, 1].min() - rect[1]) < MM and abs(hole[:, 1].max() - rect[3]) < MM
    # glass sits exactly in the opening, at the reveal depth
    T = np.asarray(p.mesh.tris)[np.asarray(p.mesh.mats) == geom.mat_id(kit["materials"]["glass"])]
    P = T.reshape(-1, 3)
    assert abs(P[:, 0].min() - rect[0]) < MM and abs(P[:, 0].max() - rect[2]) < MM
    assert abs(P[:, 1].max() - rect[3]) < MM
    assert abs(P[:, 2].min() + kit["window"]["reveal"]) < MM or abs(P[:, 2].min()) < MM


@pytest.mark.parametrize("kit_name", sorted(KITS))
def test_all_pieces_valid_and_sized(kit_name):
    kit = get_kit(kit_name)
    for lod in ("3", "3+"):
        m = geom.Merger(lod)
        p = pieces.door(kit, m, (0, 0, 2.2, 4.5))
        valid(p)
        assert abs(np.ptp(np.asarray(p.hole)[:, 1]) - 4.5) < MM
        p = pieces.arch(kit, m, (0, 0, 3, 5), arch_type="round")
        valid(p)
        p = pieces.column(kit, m, 1.0, 0.5, 0.0, 5.0, 0.5)
        lo, hi = bounds(p.mesh)
        assert abs(lo[1] - 0.0) < MM and abs(hi[1] - 5.0) < MM
        T = np.asarray(p.mesh.tris)
        shaft = T[np.ptp(T[:, :, 1], axis=1) > 3.0].reshape(-1, 3)
        r = np.hypot(shaft[:, 0] - 1.0, shaft[:, 2] - 0.5)
        assert abs(r.max() - 0.25) < MM                     # shaft corners on the 0.5 m circle
        valid(p)
        p = pieces.course(kit, m, 0, 10, 3.0, height=0.4, depth=0.3, kind="cornice")
        lo, hi = bounds(p.mesh)
        assert abs(hi[0] - lo[0] - 10) < MM and abs(hi[2] - 0.3) < MM and abs(hi[1] - lo[1] - 0.4) < MM
        valid(p)
        p = pieces.buttress(kit, m, (0, 0, 0.8, 10), 1.0)
        lo, hi = bounds(p.mesh)
        assert abs(hi[2] - 1.0) < MM and abs(hi[1] - 10) < MM and abs(hi[0] - lo[0] - 0.8) < MM
        valid(p)
        p = pieces.chimney(kit, m, 0, 0, 0, 1.0, 0.6, 2.5)
        lo, hi = bounds(p.mesh)
        assert abs(hi[1] - 2.5) < MM and abs(lo[1]) < MM
        valid(p)
        p = pieces.pilaster(kit, m, (0, 0, 0.6, 8), 0.2)
        lo, hi = bounds(p.mesh)
        assert abs(hi[1] - 8) < MM and hi[2] >= 0.2 - MM
        valid(p)
        p = pieces.balustrade(kit, m, 0, 4, 0, 1.0, 0.25)
        lo, hi = bounds(p.mesh)
        assert abs(hi[0] - lo[0] - 4) < MM and abs(hi[1] - 1.0) < MM
        valid(p)
        p = pieces.dormer(kit, m, 0, 0, 1.8, 1.6, 2.5)
        lo, hi = bounds(p.mesh)
        assert abs(hi[0] - lo[0] - 1.8) < MM and abs(lo[2] + 2.5) < MM
        valid(p)
        p = pieces.projection(kit, m, geom.circle_outline(0, 0, 1.5), 0.2)
        valid(p)
        p = pieces.recess(kit, m, geom.opening_outline(0, 0, 2, 4, "pointed"), 0.4)
        valid(p)


@pytest.mark.parametrize("rise,run", [(0.5, 1.0), (1.0, 3.0), (3.5, 8.0)])
def test_steps_sizes_and_collider(rise, run):
    kit = get_kit("victorian_gothic")
    p = pieces.steps(kit, geom.Merger("3"), 0, 4, 0.0, rise, run, cheeks=False)
    lo, hi = bounds(p.mesh)
    assert abs(hi[1] - rise) < MM
    assert abs(hi[0] - lo[0] - 4) < MM
    n = round(rise / 0.165)
    assert abs(hi[2] - max(run, 0.3 * n)) < MM
    valid(p)
    assert len(p.col) == len(p.mesh)       # the steps are real collider geometry


def test_merge_rule_and_ledger():
    kit = get_kit("victorian_gothic")
    m3, m3p = geom.Merger("3", "F01_wall"), geom.Merger("3+", "F01_wall")
    p3 = pieces.window(kit, m3, (0, 0, 1.3, 3.0), arch="pointed")
    p3p = pieces.window(kit, m3p, (0, 0, 1.3, 3.0), arch="pointed")
    # frame (0.06 m relief), sill (0.07) and hood (0.08) are under 15 cm but over 5 cm
    t3 = {e["type"] for e in m3.entries}
    assert {"window_frame", "sill", "label_hood"} <= t3
    assert not m3p.entries
    assert len(p3.mesh) < len(p3p.mesh)
    for e in m3.entries:
        assert e["size"][1] < 0.15 or (e["size"][0] < 0.15 and e["size"][2] < 0.15)
        assert e["restore_hint"] in {"normal_map", "geometry", "decal", "texture_only"}
    # a 0.12 m string course is merged at LoD3 and modeled at LoD3+
    m = geom.Merger("3")
    assert len(pieces.course(kit, m, 0, 5, 0, height=0.25, depth=0.12).mesh) == 0
    assert m.entries[0]["type"] == "string_course"
    assert len(pieces.course(kit, geom.Merger("3+"), 0, 5, 0, height=0.25, depth=0.12).mesh) > 0
    # a feature smaller than the threshold in both surface directions is merged even when deep
    m = geom.Merger("3+")
    assert not m.keep("knob", 0.3, 0.04, 0.04, (0, 0, 0), (0.04, 0.3, 0.04), "limestone", "wall")
    # LoD2: pieces are nothing, with one ledger entry
    m = geom.Merger("2")
    assert len(pieces.window(kit, m, (0, 0, 1, 2)).mesh) == 0 and len(m.entries) == 1


def test_uv_charts_do_not_overlap():
    import check_building as cb
    kit = get_kit("georgian")
    p = pieces.window(kit, geom.Merger("3+"), (0, 0, 1.4, 2.6), arch="round")
    m = p.mesh.finish()
    assert cb.uv_overlap(m["uv0"]) < 0.002
