"""Fast checks for the Stage 5 tree species models and assignment rules (no archive data needed)."""
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
import tree_kit as tk  # noqa: E402
import tree_species as ts  # noqa: E402
import vcmesh  # noqa: E402


def test_species_meshes_pass_checks():
    rows, bad = ts.check_species()
    assert bad == 0, [r for r in rows if r[3]]


def test_v2_not_heavier_than_v1_per_size():
    # per size class, a species tree costs at most the v1 round kit + 4 triangles
    v1 = {"small": tk.kit_mesh("small", "round", 0), "medium": tk.kit_mesh("medium", "round", 0),
          "full": tk.kit_mesh("large", "round", 0)}
    for s in ts.SPECIES:
        for d in ("small", "medium", "full"):
            assert len(ts.species_mesh(s, d, 0)["faces"]) <= len(v1[d]["faces"]) + 4


def test_tree_mesh_matches_measured_size():
    t = {"id": "TR_1", "x": 5.0, "y": 6.0, "ground_z": 10.0, "height": 16.0, "crown_diameter": 11.0,
         "crown_base": 4.0}
    for s in ts.SPECIES:
        m = ts.tree_mesh({**t, "species": s}, 0)
        v = m["verts"]
        assert abs(v[:, 2].max() - 26.0) < 0.05, s
        assert np.hypot(v[:, 0] - 5, v[:, 1] - 6).max() <= 11.0 / 2 * 1.45, s
        assert vcmesh.check_mesh(m) == []


def tree(i, h, w, **kw):
    return {"id": f"TR_{i:05d}", "kind": "tree", "x": float(i) * 50, "y": 0.0, "height": h, "crown_diameter": w,
            "crown_base": 2.0, **kw}


def test_assignment_rules():
    trees = [tree(1, 15, 12), tree(2, 15, 5), tree(3, 6, 5), tree(4, 15, 12), tree(5, 15, 12),
             tree(6, 15, 12, osm_tags={"leaf_type": "needleleaved"})]
    pen = np.array([0.05, 0.05, 0.4, 0.4, 0.4, 0.4])
    npts = np.array([500, 500, 500, 500, 10, 500])
    famous = ts.assign(trees, pen, npts)
    assert famous == []                                      # none of these sit at a famous tree
    assert trees[0]["species"] == "magnolia" and trees[0]["species_source"] == "lidar_leafoff"
    assert trees[1]["species"] == "conifer"
    assert trees[2]["species"] == "ornamental" and trees[2]["species_source"] == "lidar_leafoff+size"
    assert trees[3]["species"] in ("oak", "maple", "elm") and trees[3]["species_source"] == "lidar_leafoff+seeded"
    assert trees[4]["species_source"].startswith("seeded_mix")
    assert trees[5]["species"] == "conifer" and trees[5]["species_source"] == "osm_tags"


def test_seeded_mix_is_stable_and_mixed():
    picks = [ts.seeded(f"TR_{i:05d}", ts.DECIDUOUS_MIX) for i in range(600)]
    assert picks == [ts.seeded(f"TR_{i:05d}", ts.DECIDUOUS_MIX) for i in range(600)]
    share = {s: picks.count(s) / 600 for s in ("oak", "maple", "elm")}
    for s, w in ts.DECIDUOUS_MIX:
        assert abs(share[s] - w) < 0.08


def test_famous_tree_matching():
    import frame as fr
    f = ts.FAMOUS[0]
    X, Y, _ = fr.to_campus(f["lon"], f["lat"], 0.0, 4326)
    trees = [{**tree(1, 25, 13), "x": float(X) + 3, "y": float(Y)}, {**tree(2, 30, 13), "x": float(X) + 30, "y": float(Y)}]
    out = ts.assign(trees, np.array([0.3, 0.3]), np.array([500, 500]))
    assert len(out) == 1 and out[0]["matched_tree"] == "TR_00001"
    assert trees[0]["species"] == "oak" and trees[0]["famous_standing_today"] is False
