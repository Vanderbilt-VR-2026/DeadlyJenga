"""LoD2 roof fitting and meshing on synthetic point clouds (no archive needed).

Each test samples a known building shape at 12 points/m2 with 2 cm noise (class 6 roof,
class 2 ground around it), runs the roof fit, the parts, the watertight mesh and the
colliders, and checks: roof type, eave and ridge within 0.1 m, footprint kept, watertight,
UV0 inside 0..1 without overlaps, colliders covering the footprint.
"""
import math
import sys
from pathlib import Path

import numpy as np
import pytest
import shapely
from shapely.geometry import Polygon, box

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "pipeline"))
import buildings_lod2 as bl  # noqa: E402
import roof_fit as rf  # noqa: E402
import roof_mesh as rm  # noqa: E402
import vcmesh  # noqa: E402

DENSITY = 12.0
NOISE = 0.02


def sample(footprint, zfun, density=DENSITY, seed=0, ground=0.0):
    """Random roof points over the footprint plus a ring of ground points."""
    rng = np.random.default_rng(seed)
    x0, y0, x1, y1 = footprint.bounds
    n = int((x1 - x0 + 8) * (y1 - y0 + 8) * density)
    x = rng.uniform(x0 - 4, x1 + 4, n)
    y = rng.uniform(y0 - 4, y1 + 4, n)
    inside = shapely.contains_xy(footprint, x, y)
    z = np.where(inside, zfun(x, y), ground) + rng.normal(0, NOISE, n)
    cls = np.where(inside, 6, 2).astype(np.uint8)
    return {"x": x, "y": y, "z": z, "classification": cls}


def run(footprint, zfun, seed=0):
    pts = sample(footprint, zfun, seed=seed)
    fit = rf.fit_roof(pts, footprint, None, ground_z=0.0, base_z=-0.5)
    assert fit["fallback"] is None, fit["fallback"]
    cells, planes = fit["cells"], fit["planes"]
    used = sorted({l for _, l in cells})
    remap = {o: i for i, o in enumerate(used)}
    planes = [planes[o] for o in used]
    cells = [(c, remap[l]) for c, l in cells]
    parts = bl.find_parts(cells, planes)
    rows = []
    for members in parts:
        poly, eave, ridge = bl.part_heights(cells, planes, members, -0.5)
        ls = sorted({cells[k][1] for k in members})
        areas = [sum(cells[k][0].area for k in members if cells[k][1] == l) for l in ls]
        rows.append({"poly": poly, "eave": eave, "ridge": ridge,
                     "type": rf.classify_part([planes[l] for l in ls], areas, False)})
    cd = [{"poly": c, "z": planes[l].z, "mat": 13 if planes[l].slope_deg < rf.FLAT_DEG else 11} for c, l in cells]
    mesh = rm.build_solid(cd, -0.5, 0, 4)
    hulls, colinfo = bl.colliders(cells, planes, parts, -0.5)
    return {"cells": cells, "planes": planes, "parts": rows, "mesh": mesh, "hulls": hulls, "colinfo": colinfo}


def common_checks(r, footprint):
    fp = shapely.unary_union([c for c, _ in r["cells"]])
    assert fp.symmetric_difference(footprint).area < 0.005 * footprint.area      # footprint kept
    assert vcmesh.check_mesh(r["mesh"], need_watertight=True) == []
    uv = r["mesh"]["uv0"]
    assert uv.min() >= 0 and uv.max() <= 1
    assert vcmesh.uv_overlap_area(uv) < 0.02
    import trimesh
    t = trimesh.Trimesh(r["mesh"]["verts"], r["mesh"]["faces"], process=True)
    assert t.volume > 0                                   # outward normals
    cover = shapely.unary_union([shapely.MultiPoint(h.vertices[:, :2]).convex_hull for h in r["hulls"]])
    assert footprint.difference(cover).area < 0.01 * footprint.area   # colliders cover the footprint
    assert r["colinfo"]["count"] <= 12


def main_part(r):
    return max(r["parts"], key=lambda p: p["poly"].area)


def test_flat_box():
    fp = box(0, 0, 20, 12)
    r = run(fp, lambda x, y: np.full_like(x, 10.0))
    common_checks(r, fp)
    p = main_part(r)
    assert p["type"] == "flat"
    assert abs(p["eave"] - 10) < 0.1 and abs(p["ridge"] - 10) < 0.1


def test_gable():
    fp = box(0, 0, 20, 12)
    # ridge along x at y = 6: eave 8 m, ridge 12 m (slope 4/6)
    r = run(fp, lambda x, y: 12.0 - np.abs(y - 6.0) * (4.0 / 6.0))
    common_checks(r, fp)
    p = main_part(r)
    assert p["type"] == "gable"
    assert len(r["parts"]) == 1
    assert abs(p["eave"] - 8) < 0.1 and abs(p["ridge"] - 12) < 0.1


def test_hip():
    fp = box(0, 0, 20, 12)
    s = math.tan(math.radians(35))
    # hip roof: height = eave + s * distance to the nearest wall
    r = run(fp, lambda x, y: 8.0 + s * np.minimum(np.minimum(x, 20 - x), np.minimum(y, 12 - y)))
    common_checks(r, fp)
    p = main_part(r)
    assert p["type"] == "hip"
    assert abs(p["eave"] - 8) < 0.1 and abs(p["ridge"] - (8 + 6 * s)) < 0.1


def test_l_shape_two_heights():
    a, b = box(0, 0, 24, 10), box(0, 10, 10, 24)
    fp = shapely.unary_union([a, b])
    fp = Polygon(fp.exterior)
    r = run(fp, lambda x, y: np.where(y < 10, 9.0, 14.0))
    common_checks(r, fp)
    assert len(r["parts"]) == 2
    hs = sorted((round(p["eave"], 1), round(p["ridge"], 1)) for p in r["parts"])
    assert abs(hs[0][0] - 9) < 0.1 and abs(hs[0][1] - 9) < 0.1
    assert abs(hs[1][0] - 14) < 0.1 and abs(hs[1][1] - 14) < 0.1
    for p in r["parts"]:
        assert p["type"] == "flat"
    big = [p for p in r["parts"] if p["eave"] > 12][0]
    assert big["poly"].symmetric_difference(b).area < 0.05 * b.area


def test_tower_on_hall():
    fp = box(0, 0, 30, 20)
    tower = box(12, 7, 18, 13)
    r = run(fp, lambda x, y: np.where(shapely.contains_xy(tower, x, y), 25.0, 10.0))
    common_checks(r, fp)
    assert len(r["parts"]) == 2
    t = max(r["parts"], key=lambda p: p["ridge"])
    h = min(r["parts"], key=lambda p: p["ridge"])
    assert abs(t["ridge"] - 25) < 0.1 and abs(t["eave"] - 25) < 0.1
    assert abs(h["ridge"] - 10) < 0.1 and abs(h["eave"] - 10) < 0.1
    assert t["poly"].symmetric_difference(tower).area < 0.1 * tower.area


def test_pinch_vertex_stays_watertight():
    # four cells meeting at one point with heights high-low-high-low
    c = [box(0, 0, 5, 5), box(5, 0, 10, 5), box(5, 5, 10, 10), box(0, 5, 5, 10)]
    zs = [12.0, 8.0, 12.0, 8.0]
    cells = [{"poly": p, "z": (lambda x, y, z=z: np.full(np.shape(x), z)), "mat": 13} for p, z in zip(c, zs)]
    mesh = rm.build_solid(cells, 0.0, 0, 4)
    assert vcmesh.check_mesh(mesh, need_watertight=True) == []
