"""Blender tile export + check_fbx on two tiny synthetic tiles (slow: starts Blender 4 times).

Archive layout is faked in a temp folder (VC_DATA_ROOT); FBX files go to a temp
folder through export_all.py --out. Checks the object names, mat_<id> slots,
color bytes (all 27 material indices), UV0, and the axis convention:
campus (X, Y, Z) -> raw FBX (X, Z, -Y) -> Unity (X, Z, Y)
(calibrated against a real Unity import on 2026-11-20).
"""
import json
import os
import subprocess
import sys
from pathlib import Path

import numpy as np
import pytest

TOOLS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(TOOLS / "pipeline"))
from vcmesh import save_mesh  # noqa: E402

if not Path(json.loads((TOOLS / "config" / "local.json").read_text()).get("blender", "")).exists():
    pytest.skip("Blender not installed", allow_module_level=True)


def grid(x0, y0, size, n, z, mats):
    """Flat n x n quad grid (2 triangles per quad), face materials cycling through mats."""
    xs = np.linspace(x0, x0 + size, n + 1)
    X, Y = np.meshgrid(xs, np.linspace(y0, y0 + size, n + 1))
    verts = np.column_stack([X.ravel(), Y.ravel(), np.full(X.size, z)])
    faces = []
    for r in range(n):
        for c in range(n):
            a = r * (n + 1) + c
            b, d, e = a + 1, a + n + 1, a + n + 2
            faces += [[a, b, e], [a, e, d]]       # counter-clockwise seen from above
    faces = np.array(faces)
    face_mat = np.array([mats[i % len(mats)] for i in range(len(faces))])
    uv = (verts[faces][:, :, :2] - [x0, y0]) / size
    return verts, faces, face_mat, uv


def box(x0, y0, z0, x1, y1, z1, wall, roof):
    v = np.array([[x, y, z] for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)], float)
    # 0..3 bottom, 4..7 top; faces counter-clockwise seen from outside
    f = [[0, 2, 3], [0, 3, 1], [4, 5, 7], [4, 7, 6], [0, 1, 5], [0, 5, 4],
         [1, 3, 7], [1, 7, 5], [3, 2, 6], [3, 6, 7], [2, 0, 4], [2, 4, 6]]
    mats = [wall, wall, roof, roof] + [wall] * 8
    uv = np.tile(np.array([[0.1, 0.1], [0.9, 0.1], [0.9, 0.9]], np.float32), (12, 1, 1))
    return v, np.array(f), np.array(mats), uv


@pytest.fixture(scope="module")
def run(tmp_path_factory):
    tmp = tmp_path_factory.mktemp("export")
    root, out = tmp / "archive", tmp / "fbx"
    t0, t1 = root / "derived/terrain/v1/T_00_00", root / "derived/terrain/v1/T_01_00"
    # Terrain of tile T_00_00 uses all 27 material indices.
    save_mesh(t0 / "terrain.npz", *grid(0, 0, 100, 4, 2.0, list(range(27))))
    save_mesh(t0 / "terrain_col.npz", *grid(0, 0, 100, 1, 2.0, [20, 17]))
    save_mesh(t0 / "terrain_far.npz", *grid(0, 0, 100, 1, 2.0, [18]))
    save_mesh(t1 / "terrain.npz", *grid(100, 0, 100, 1, 3.0, [20]))
    (root / "derived/terrain/v1/T_02_00").mkdir(parents=True)          # empty tile: skipped
    # One building with an id containing underscores; its corner is the axis marker (10, 20, 5).
    b = root / "buildings/B_new_3/v1"
    save_mesh(b / "lod2.npz", *box(10, 20, 2, 20, 30, 5, 0, 13))
    save_mesh(b / "lod1.npz", *box(10, 20, 2, 20, 30, 5, 1, 13))
    save_mesh(b / "col_0.npz", *box(10, 20, 2, 20, 30, 5, 0, 0))
    (b / "model.json").write_text(json.dumps({"building": "B_new_3", "tile": "T_00_00"}))
    # Buildings v2: an index picks the folder; col_mesh.npz (one non-convex collider) replaces col_<n>.npz.
    k = root / "buildings/B_544397715/v2"
    save_mesh(k / "lod2.npz", *box(60, 60, 2, 70, 70, 12, 2, 11))
    save_mesh(k / "lod1.npz", *box(60, 60, 2, 70, 70, 12, 2, 11))
    save_mesh(k / "col_mesh.npz", *box(60, 60, 2, 70, 70, 12, 2, 2))
    save_mesh(k / "col_0.npz", *box(60, 60, 2, 70, 70, 12, 2, 2))      # old convex part: must be ignored
    idx = root / "derived/buildings/v2"
    idx.mkdir(parents=True)
    (idx / "index.json").write_text(json.dumps({"buildings": [
        {"id": "B_new_3", "tile": "T_00_00", "folder": "buildings/B_new_3/v1"},
        {"id": "B_544397715", "tile": "T_00_00", "version": "v2",
         "footprint": [[60, 60], [70, 60], [70, 70], [60, 70]]}]}))
    tr = root / "derived/trees/v1/T_00_00"
    save_mesh(tr / "trees_lod0.npz", *box(50, 50, 2, 51, 51, 8, 22, 23))
    save_mesh(tr / "trees_lod1.npz", *box(50, 50, 2, 51, 51, 8, 22, 23))
    save_mesh(tr / "trees_col.npz", *box(50, 50, 2, 51, 51, 8, 22, 22))
    save_mesh(root / "derived/buildings/ring/v1/T_01_00.npz", *box(150, 50, 3, 160, 60, 20, 26, 13))
    lidar = root / "derived/lidar/v1"
    lidar.mkdir(parents=True)
    (lidar / "index.json").write_text(json.dumps({"tiles": [{"name": "T_00_00", "zone": "campus"},
                                                            {"name": "T_01_00", "zone": "ring"}]}))
    # Markers in Unity axes (x east, y up, z north).
    umap = tmp / "unity_map.json"
    umap.write_text(json.dumps({
        "spawns": [{"name": "AlumniLawn_1", "pos": [40, 2.1, 60]}],
        "route": [[30, 2.1, 10], [130, 3.1, 10]],
        "climb": [{"building": "B_new_3", "pos": [15, 2, 19.4], "forward": [0, 0, 1]},
                  {"building": "B_new_3", "pos": [20.6, 2, 25], "forward": [-1, 0, 0]}]}))
    env = dict(os.environ, VC_DATA_ROOT=str(root))
    cmd = [sys.executable, str(TOOLS / "pipeline/export_all.py"), "--skip-map", "--unity-map", str(umap),
           "--out", str(out), "--jobs", "2", "--report", str(tmp / "report.json")]
    first = subprocess.run(cmd, env=env, capture_output=True, text=True, timeout=900)
    second = subprocess.run(cmd, env=env, capture_output=True, text=True, timeout=900)
    return {"root": root, "out": out, "first": first, "second": second, "report": tmp / "report.json"}


def check(run, tile):
    return json.loads((run["root"] / f"derived/export/v1/check/{tile}.json").read_text())


def test_driver_passes_and_is_idempotent(run):
    assert run["first"].returncode == 0, run["first"].stdout + run["first"].stderr
    assert "2 tiles with terrain (1 without), 0 to export" in run["second"].stdout, run["second"].stdout
    report = json.loads(run["report"].read_text())
    assert report["tiles_exported"] == 2 and report["failures"] == []
    assert report["tiles_without_terrain"] == ["T_02_00"]
    assert (run["out"] / "T_00_00.fbx").exists() and (run["out"] / "T_01_00.fbx").exists()
    assert not list(run["out"].glob("*.meta"))


def test_names_and_materials(run):
    c0, c1 = check(run, "T_00_00"), check(run, "T_01_00")
    assert c0["pass"] and c1["pass"], (c0["problems"], c1["problems"])
    assert set(c0["objects"]) == {"Terrain", "Terrain_LOD1", "HLOD", "COL_Terrain", "B_new_3_LOD0", "B_new_3_LOD1", "COL_B_new_3_0",
                                  "B_544397715_LOD0", "B_544397715_LOD1", "COLM_B_544397715",
                                  "Trees", "Trees_LOD1", "COL_Trees"}
    assert set(c1["objects"]) >= {"Terrain", "Ring"}
    assert set(c0["info"]["empties"]) == {"Spawn_AlumniLawn_1", "Route_00", "Climb_B_new_3", "Climb_B_new_3_b"}
    assert set(c1["info"]["empties"]) == {"Route_01"}
    assert c0["objects"]["B_new_3_LOD0"]["materials"] == ["mat_brick_red", "mat_roof_flat"]
    assert c1["objects"]["Ring"]["materials"] == ["mat_roof_flat", "mat_signage_blank"]
    assert c0["objects"]["Terrain"]["uv_layers"][0] == "UV0"
    # Terrain_LOD1 = terrain_col.npz drawn as a visual: its materials, colors and UVs kept.
    lod1 = c0["objects"]["Terrain_LOD1"]
    assert lod1["triangles"] == 2 and lod1["materials"] == ["mat_paving_concrete", "mat_grass"]
    assert lod1["color_r_bytes"] == [17, 20] and lod1["uv_layers"][0] == "UV0"
    assert "Terrain_LOD1" not in c1["objects"]
    t1 = c0["objects"]["Trees_LOD1"]
    assert t1["triangles"] == 12 and t1["materials"] == ["mat_bark", "mat_foliage"]
    assert t1["color_r_bytes"] == [22, 23] and t1["uv_layers"][0] == "UV0"
    assert "Trees_LOD1" not in c1["objects"]
    # v2 building: one non-convex collider object, no convex parts.
    assert c0["objects"]["COLM_B_544397715"]["triangles"] == 12
    assert not any(n.startswith("COL_B_544397715") for n in c0["objects"])
    # HLOD = terrain_far (2, asphalt 18) + 2 building LOD1 (24) + Trees_LOD1 (12) in one mesh, one slot.
    h = c0["objects"]["HLOD"]
    assert h["triangles"] == 38 and h["materials"] == ["mat_mixed"] and h["uv_layers"][0] == "UV0"
    assert h["color_r_bytes"] == [1, 2, 11, 13, 18, 22, 23] and h["import_color_bytes"] == [1, 2, 11, 13, 18, 22, 23]
    assert c1["objects"]["HLOD"]["triangles"] == 12            # ring tile: only the Ring exists          # T_01_00 has no terrain_col.npz


def test_color_bytes_all_indices(run):
    t = check(run, "T_00_00")["objects"]["Terrain"]
    assert t["color_r_bytes"] == list(range(27))           # raw FBX
    assert t["import_color_bytes"] == list(range(27))      # Blender re-import, bytes
    assert len(t["materials"]) == 27
    assert check(run, "T_01_00")["objects"]["Ring"]["color_r_bytes"] == [13, 26]


def test_axis_convention(run):
    b = check(run, "T_00_00")["objects"]["B_new_3_LOD0"]
    # Campus box X 10..20 (east), Y 20..30 (north), Z 2..5 (up); marker corner (10, 20, 5).
    assert b["fbx_min"] == [10.0, 2.0, -30.0] and b["fbx_max"] == [20.0, 5.0, -20.0]   # raw FBX = (X, Z, -Y)
    assert b["unity_min"] == [10.0, 2.0, 20.0] and b["unity_max"] == [20.0, 5.0, 30.0]  # Unity = (X, Z, Y)
    e = check(run, "T_00_00")["info"]["empties"]
    assert e["Spawn_AlumniLawn_1"]["fbx_translation"] == [40.0, 2.1, -60.0]
    assert e["Spawn_AlumniLawn_1"]["unity_pos"] == [40.0, 2.1, 60.0]
    assert np.allclose(e["Climb_B_new_3"]["unity_forward"], [0, 0, 1], atol=1e-4)      # north, at the wall
    assert np.allclose(e["Climb_B_new_3_b"]["unity_forward"], [-1, 0, 0], atol=1e-4)   # west, at the wall
    assert check(run, "T_00_00")["info"]["header"]["UnitScaleFactor"] == 100.0
