"""Terrain on synthetic ground: a tilted plane with a 30 cm curb and a 10 cm bump, two tiles side by side."""
import sys
from pathlib import Path

import numpy as np
import pytest

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "pipeline"))
import terrain  # noqa: E402
import terrain_mesh as tm  # noqa: E402
import vcmesh  # noqa: E402

CURB_X = 40.2          # curb line (inside cell column 80, so the fit has to find the right edge)
CURB_H = 0.30
BUMP = (60.0, 64.0, 60.0, 64.0)
BUMP_H = 0.10


def ground_z(x, y):
    z = 10 + 0.02 * x + 0.01 * y
    z = z + np.where(x > CURB_X, CURB_H, 0.0)
    inb = (x > BUMP[0]) & (x < BUMP[1]) & (y > BUMP[2]) & (y < BUMP[3])
    return z + np.where(inb, BUMP_H, 0.0)


def tile(col, row, zone="campus"):
    return {"name": f"T_{col:02d}_{row:02d}", "col": col, "row": row, "zone": zone,
            "campus_fraction": 1.0 if zone == "campus" else 0.0,
            "bounds": [col * 100.0, row * 100.0, col * 100.0 + 100, row * 100.0 + 100]}


@pytest.fixture(scope="module", params=["campus", "ring"])
def built(tmp_path_factory, request):
    """Tile T_00_00 (0.5 m) with the curb and bump, and its east neighbor T_01_00 as a 0.5 m
    campus tile or a 2 m ring tile."""
    root = tmp_path_factory.mktemp("terrain")
    rng = np.random.default_rng(7)
    n = int(220 * 120 * 4)                         # about 4 ground points per m2, like the real data
    x = rng.uniform(-10, 210, n)
    y = rng.uniform(-10, 110, n)
    z = ground_z(x, y) + rng.normal(0, 0.01, n)
    tiles = [tile(0, 0), tile(1, 0, request.param)]
    ids = vcmesh.material_index()
    out = {"root": root, "tiles": tiles, "pts": (x, y, z)}
    for t in tiles:
        x0, y0, x1, y1 = t["bounds"]
        m = (x >= x0 - 10) & (x < x1 + 10) & (y >= y0 - 10) & (y < y1 + 10)
        g = terrain.grid_from_points(x[m], y[m], z[m], t)
        terrain.save_grid(t["name"], t, g, root, "v1")
        out[t["name"] + "_grid"] = g
    ts = terrain.TileSet(tiles, root, "v1")
    for t in tiles:
        g = out[t["name"] + "_grid"]
        nc = g["hc"].shape[0]
        mat = np.full((nc, nc), ids["grass"], np.int16)
        jj, ii = np.mgrid[0:nc, 0:nc]
        mat[np.abs(jj - ii) < nc // 40] = ids["paving_concrete"]     # a diagonal path (staircase edges)
        vis, col = terrain.meshes_from_cells(g["hc"].astype(float), g["bx"], g["by"], g["holes"] == 2, mat, t, ts, ids)
        out[t["name"]] = (vis, col)
        fm = np.full((25, 25), ids["grass"], np.int16)
        fj, fi = np.mgrid[0:25, 0:25]
        fm[np.abs(fj - fi) < 2] = ids["paving_concrete"]
        out[t["name"] + "_far"] = terrain.far_mesh(t, ts, g["nodes"].astype(float), fm, ids)
    return out


def test_far_mesh_budget_uv_and_accuracy(built):
    x, y, z = built["pts"]
    for t in built["tiles"]:
        far = built[t["name"] + "_far"]
        assert len(far["faces"]) <= terrain.FAR_BUDGET[terrain.kind_of(t)]
        assert vcmesh.check_mesh(far) == []
        assert far["uv0"].min() >= 0 and far["uv0"].max() <= 1
        x0 = t["bounds"][0]
        m = (x >= x0) & (x < x0 + 100) & (y >= 0) & (y < 100)
        s = tm.error_stats(z[m] - tm.mesh_z_at(far["verts"], far["faces"], x[m], y[m], x0, 0.0))
        assert s["points"] > 0.99 * m.sum()
        assert s["max_m"] <= 1.0 and s["p95_m"] <= terrain.FAR_TOL


def test_far_mesh_skirt(built):
    for t in built["tiles"]:
        far = built[t["name"] + "_far"]
        x0, y0 = t["bounds"][0], t["bounds"][1]
        sk = far["faces"][far["kind"] == 2]
        assert len(sk) >= 8                           # at least one quad per side
        P = far["verts"][sk]
        # skirt faces are vertical, on the border, and reach 1.5 m below the edge
        nrm = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0])
        assert np.all(np.abs(nrm[:, 2]) < 1e-9)
        onb = (np.isclose(P[:, :, 0], x0) | np.isclose(P[:, :, 0], x0 + 100)
               | np.isclose(P[:, :, 1], y0) | np.isclose(P[:, :, 1], y0 + 100))
        assert onb.all()
        # every skirt corner either is a ground vertex or sits exactly FAR_SKIRT below one
        ground = far["verts"][np.unique(far["faces"][far["kind"] == 0].ravel())]
        gkey = {(round(v[0], 6), round(v[1], 6)): v[2] for v in ground}
        for v in far["verts"][np.unique(sk.ravel())]:
            zt = gkey[(round(v[0], 6), round(v[1], 6))]
            assert np.isclose(v[2], zt) or np.isclose(zt - v[2], terrain.FAR_SKIRT)
        # the skirt runs all the way round (its top edges add up to the 400 m border) and faces out
        mid = P.mean(axis=1)
        cx, cy = x0 + 50, y0 + 50
        dirn = np.c_[mid[:, 0] - cx, mid[:, 1] - cy]
        assert np.all((nrm[:, :2] * dirn).sum(axis=1) > 0), "skirt faces point out of the tile"
        top_len = 0.0
        for tri in P:
            hi = np.array([v for v in tri if np.isclose(v[2], gkey[(round(v[0], 6), round(v[1], 6))])])
            if len(hi) == 2:
                top_len += np.linalg.norm(hi[0, :2] - hi[1, :2])
        assert top_len == pytest.approx(400.0, abs=1e-6)


def test_far_edges_match(built):
    a = built["T_00_00_far"]
    b = built["T_01_00_far"]
    ea = np.unique(a["verts"][a["faces"][a["kind"] == 0].ravel()], axis=0)
    eb = np.unique(b["verts"][b["faces"][b["kind"] == 0].ravel()], axis=0)
    ea, eb = ea[np.isclose(ea[:, 0], 100.0)], eb[np.isclose(eb[:, 0], 100.0)]
    ea, eb = ea[np.argsort(ea[:, 1])], eb[np.argsort(eb[:, 1])]
    assert ea[0, 1] == 0 and ea[-1, 1] == 100 and eb[0, 1] == 0 and eb[-1, 1] == 100
    assert np.abs(np.interp(ea[:, 1], eb[:, 1], eb[:, 2]) - ea[:, 2]).max() < 1e-9
    assert np.abs(np.interp(eb[:, 1], ea[:, 1], ea[:, 2]) - eb[:, 2]).max() < 1e-9


def test_curb_kept_as_vertical_face(built):
    vis, _ = built["T_00_00"]
    v, f, k = vis["verts"], vis["faces"], vis["kind"]
    vert = f[k == 1]
    assert len(vert) > 100, "the curb should give many vertical triangles"
    P = v[vert]
    xs = P[:, :, 0].ravel()
    assert np.all(np.isin(np.round(xs, 6), [40.0, 40.5])), "curb faces sit on the cell edge next to the line"
    # the faces cover nearly the whole 100 m curb, about 30 cm tall
    ys = P[:, :, 1]
    assert np.ptp(ys) > 95
    heights = np.ptp(P[:, :, 2], axis=1)
    assert np.median(heights) == pytest.approx(CURB_H, abs=0.04)
    # vertical faces really are vertical; the ones across X face the low side (-X)
    nrm = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0])
    assert np.all(np.abs(nrm[:, 2]) < 1e-9)
    across_x = np.abs(nrm[:, 0]) > np.abs(nrm[:, 1])
    assert across_x.sum() > 0.7 * len(nrm)
    assert np.all(nrm[across_x, 0] < 0)
    assert vis["stats"]["break_edges"] >= 190


def test_bump_under_15cm_is_not_a_break(built):
    vis, _ = built["T_00_00"]
    P = vis["verts"][vis["faces"][vis["kind"] == 1]]
    near_bump = (P[:, :, 0] > BUMP[0] - 1) & (P[:, :, 0] < BUMP[1] + 1) & \
                (P[:, :, 1] > BUMP[2] - 1) & (P[:, :, 1] < BUMP[3] + 1)
    assert not near_bump.any()
    # ...but the bump is still in the surface (smoothed, not flattened away)
    z = tm.mesh_z_at(vis["verts"], vis["faces"], np.array([62.0]), np.array([62.0]), 0, 0)
    assert z[0] - (10 + 0.02 * 62 + 0.01 * 62 + CURB_H) > 0.05


def test_edges_match_between_tiles(built):
    for which in (0, 1):            # visual, collider
        a = built["T_00_00"][which]["verts"]
        b = built["T_01_00"][which]["verts"]
        ea = a[np.isclose(a[:, 0], 100.0)]
        eb = b[np.isclose(b[:, 0], 100.0)]
        assert len(ea) >= 2 and len(eb) >= 2       # a straight edge may need only its 2 corners
        ea, eb = ea[np.argsort(ea[:, 1])], eb[np.argsort(eb[:, 1])]
        # every edge vertex of one tile lies exactly on the other tile's edge polyline
        assert np.abs(np.interp(ea[:, 1], eb[:, 1], eb[:, 2]) - ea[:, 2]).max() < 1e-9
        assert np.abs(np.interp(eb[:, 1], ea[:, 1], ea[:, 2]) - eb[:, 2]).max() < 1e-9
        assert ea[0, 1] == 0 and ea[-1, 1] == 100 and eb[0, 1] == 0 and eb[-1, 1] == 100


def test_mesh_valid_and_uv(built):
    for name in ("T_00_00", "T_01_00"):
        vis, col = built[name]
        assert vcmesh.check_mesh(vis) == []
        assert vcmesh.check_mesh(col) == []
        assert vis["uv0"].min() >= 0 and vis["uv0"].max() <= 1
        g = vis["kind"] == 0
        assert vis["uv0"][g][:, :, 1].max() <= tm.UV_SCALE + 1e-6
        assert (vis["uv0"][~g][:, :, 1] > tm.UV_SCALE).all()
        assert vcmesh.uv_overlap_area(vis["uv0"], grid=256) < 0.02
        assert len(set(np.unique(vis["face_mat"]).tolist())) >= 2      # grass + path (+ curb faces)


def test_accuracy_within_5cm(built):
    x, y, z = built["pts"]
    for name, x0 in (("T_00_00", 0.0), ("T_01_00", 100.0)):
        for mesh in built[name]:
            m = (x >= x0) & (x < x0 + 100) & (y >= 0) & (y < 100)
            zm = tm.mesh_z_at(mesh["verts"], mesh["faces"], x[m], y[m], x0, 0.0)
            s = tm.error_stats(z[m] - zm)
            assert s["points"] > 0.99 * m.sum()
            assert s["median_m"] <= 0.02
            assert s["p95_m"] <= 0.05 if mesh is built[name][0] else s["p95_m"] <= 0.10


def test_triangle_budget(built):
    vis, col = built["T_00_00"]
    assert vis["stats"]["triangles"] <= terrain.VIS_BUDGET["campus"]
    assert col["stats"]["triangles"] <= terrain.COL_BUDGET
    east = built["tiles"][1]
    vis_e, _ = built["T_01_00"]
    assert vis_e["stats"]["triangles"] <= terrain.VIS_BUDGET[terrain.kind_of(east)]


def test_simplify_profile_and_zipper():
    z = np.array([0, 0, 0, 1, 0, 0, 0], float)
    keep = tm.simplify_profile(z, 0.1)
    assert keep.tolist() == [True, False, True, True, True, False, True]
    tris = tm._zipper([0, 1, 2, 3], [0, 5, 4, 3])          # 6-vertex rectangle border
    assert len(tris) == 4 and all(len(set(t)) == 3 for t in tris)
