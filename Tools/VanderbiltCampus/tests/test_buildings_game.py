"""buildings_game.py on a synthetic, finely split gable house (slow: starts Blender once)."""
import json
import os
import subprocess
import sys
from pathlib import Path

import numpy as np
import pytest

TOOLS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(TOOLS / "pipeline"))
from vcmesh import load_mesh, save_mesh, uv_overlap_area  # noqa: E402

if not Path(json.loads((TOOLS / "config" / "local.json").read_text()).get("blender", "")).exists():
    pytest.skip("Blender not installed", allow_module_level=True)

WALL, ROOF, BASE = 0, 11, 4


def gable_house():
    """10 x 6 m house, eaves at 4 m, ridge at 6 m along X; 16 coarse triangles, outward winding."""
    v = np.array([[0, 0, 0], [10, 0, 0], [10, 6, 0], [0, 6, 0],          # 0-3 ground
                  [0, 0, 4], [10, 0, 4], [10, 6, 4], [0, 6, 4],          # 4-7 eaves
                  [0, 3, 6], [10, 3, 6]], float)                         # 8-9 ridge
    f = [([0, 2, 1], BASE), ([0, 3, 2], BASE),
         ([0, 1, 5], WALL), ([0, 5, 4], WALL), ([2, 3, 7], WALL), ([2, 7, 6], WALL),     # long walls
         ([1, 2, 6], WALL), ([1, 6, 5], WALL), ([5, 6, 9], WALL),                       # east gable
         ([3, 0, 4], WALL), ([3, 4, 7], WALL), ([4, 8, 7], WALL),                       # west gable
         ([4, 5, 9], ROOF), ([4, 9, 8], ROOF), ([7, 8, 9], ROOF), ([7, 9, 6], ROOF)]    # roof
    return v, np.array([t for t, _ in f]), np.array([m for _, m in f])


def split(v, f, m, times):
    """Midpoint subdivision: every triangle -> 4 (coplanar pieces, shared edges match)."""
    for _ in range(times):
        nv, nf, nm = [], [], []
        for t, mat in zip(f, m):
            a, b, c = v[t]
            ab, bc, ca = (a + b) / 2, (b + c) / 2, (c + a) / 2
            for tri in ((a, ab, ca), (ab, b, bc), (ca, bc, c), (ab, bc, ca)):
                nf.append([len(nv), len(nv) + 1, len(nv) + 2])
                nv += list(tri)
                nm.append(mat)
        v, f, m = np.array(nv), np.array(nf), np.array(nm)
    return v, f, m


def areas(v, f):
    return 0.5 * np.linalg.norm(np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]]), axis=1)


@pytest.fixture(scope="module")
def run(tmp_path_factory):
    tmp = tmp_path_factory.mktemp("game")
    root = tmp / "archive"
    v, f, m = split(*gable_house(), 3)                 # 16 * 64 = 1024 triangles
    uv = np.tile(np.array([[0.1, 0.1], [0.2, 0.1], [0.1, 0.2]], np.float32), (len(f), 1, 1))
    folder = root / "buildings/B_test/v2"
    save_mesh(folder / "lod2.npz", v, f, m, uv)
    cf = f.copy()
    flip = np.arange(len(cf)) % 3 == 0                  # a third of the collider wound the wrong way
    cf[flip] = cf[flip][:, [0, 2, 1]]
    save_mesh(folder / "col_mesh.npz", v, cf, np.zeros(len(cf)), uv)
    idx = root / "derived/buildings/v2"
    idx.mkdir(parents=True)
    (idx / "index.json").write_text(json.dumps({"buildings": [{"id": "B_test", "tile": "T_00_00", "version": "v2"}]}))
    env = dict(os.environ, VC_DATA_ROOT=str(root))
    r = subprocess.run([sys.executable, str(TOOLS / "pipeline/buildings_game.py"), "--jobs", "1"],
                       env=env, capture_output=True, text=True, timeout=900)
    return {"root": root, "folder": folder, "proc": r, "src": (v, f, m)}


def test_runs(run):
    assert run["proc"].returncode == 0, run["proc"].stdout + run["proc"].stderr
    rep = json.loads((run["root"] / "reports/stage1/buildings_game.json").read_text())
    assert rep["totals"]["visual_before"] == 1024 and rep["failures"] == []


def test_visual_back_to_a_handful(run):
    g = load_mesh(run["folder"] / "game_lod0.npz")
    assert len(g["faces"]) <= 20                         # 16 coarse triangles (+ a few from n-gon fills)
    stats = json.loads((run["root"] / "derived/buildings/game/v1/B_test.json").read_text())
    assert stats["visual"]["dev_max_m"] <= 0.05 and stats["visual"]["crease_max_m"] <= 0.05
    assert stats["problems"] == []
    # Materials kept where they were: the area per material is unchanged.
    v, f, m = run["src"]
    for mat in (WALL, ROOF, BASE):
        assert abs(areas(g["verts"], g["faces"])[g["face_mat"] == mat].sum() - areas(v, f)[m == mat].sum()) < 0.01


def test_uv_valid(run):
    g = load_mesh(run["folder"] / "game_lod0.npz")
    assert g["uv0"].min() >= 0 and g["uv0"].max() <= 1
    assert uv_overlap_area(g["uv0"]) < 0.02
    # Uniform texel density: UV area / surface area is the same for every triangle.
    d1, d2 = g["uv0"][:, 1] - g["uv0"][:, 0], g["uv0"][:, 2] - g["uv0"][:, 0]
    uva = 0.5 * np.abs(d1[:, 0] * d2[:, 1] - d1[:, 1] * d2[:, 0])
    ratio = uva / areas(g["verts"], g["faces"])
    assert ratio.max() / ratio.min() < 1.01


def test_collider_winding(run):
    c = load_mesh(run["folder"] / "game_col.npz")
    assert len(c["faces"]) <= 20
    f = c["faces"]
    e = np.vstack([f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]]])
    _u, counts = np.unique(e, axis=0, return_counts=True)
    assert counts.max() == 1                              # every directed edge once: consistent winding
    v = c["verts"]
    volume = np.einsum("ij,ij->i", v[f[:, 0]], np.cross(v[f[:, 1]], v[f[:, 2]])).sum() / 6
    assert volume > 0                                     # normals face out
    assert areas(v, f).min() > 1e-8                       # no degenerate triangles
