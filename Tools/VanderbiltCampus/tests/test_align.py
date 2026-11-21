"""Stage 3 alignment helpers on synthetic data (fast; COLMAP is not run on photos).

Covers: pair lists (sequential / GPS spatial), chunk windows, chunk merging of two
overlapping synthetic reconstructions (pycolmap's synthetic dataset, one chunk
moved by a known similarity), and the sources.json visibility geometry on a box
building (in front, frustum, incidence, occlusion by another footprint).
"""
import sys
from pathlib import Path
from types import SimpleNamespace

import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
import align  # noqa: E402
import align_geom as geom  # noqa: E402
import align_sources as src  # noqa: E402


def fake_photos(n, seqs=("a",), step=10.0):
    out = []
    for s_i, s in enumerate(seqs):
        for i in range(n):
            out.append({"name": f"{s}/{i:04d}.jpg", "sequence": s,
                        "gps": {"X": i * step, "Y": 50.0 * s_i, "heading_deg": 90.0}})
    return out


def test_sequential_pairs_stay_in_sequence():
    ph = fake_photos(6, seqs=("a", "b"))
    pairs = align.sequential_pairs(ph, 2)
    assert (0, 1) in pairs and (0, 2) in pairs and (0, 3) not in pairs
    assert all(ph[a]["sequence"] == ph[b]["sequence"] for a, b in pairs)
    assert len(pairs) == 2 * (5 + 4)


def test_spatial_pairs_cross_sequences_by_gps():
    ph = fake_photos(5, seqs=("a", "b"), step=10.0)       # sequence b runs 50 m north of a
    near = align.spatial_pairs(ph, radius=25, max_neighbors=10)
    assert all(abs(ph[a]["gps"]["Y"] - ph[b]["gps"]["Y"]) < 1 for a, b in near)
    far = align.spatial_pairs(ph, radius=60, max_neighbors=10)
    assert any(ph[a]["sequence"] != ph[b]["sequence"] for a, b in far)


def test_make_chunks_overlap():
    ph = fake_photos(100)
    ch = align.make_chunks(ph, 40, 10)
    assert ch[0][0] == 0 and ch[-1][-1] == 99
    for a, b in zip(ch[:-1], ch[1:]):
        assert len(set(a) & set(b)) == 10
    assert align.make_chunks(ph, 0, 10) == [list(range(100))]


def test_chunk_transform_recovers_similarity():
    rng = np.random.default_rng(0)
    base = {f"im{i}": rng.normal(0, 5, 3) for i in range(30)}
    M = geom.sim_matrix(0.4, geom.small_rotation([0.3, -0.2, 1.0]), [3, -4, 2])
    Mi = np.linalg.inv(M)
    chunk = {k: geom.sim_apply(Mi, v[None])[0] for k, v in list(base.items())[10:]}
    chunk["im29"] = chunk["im29"] + 20.0                   # one bad shared camera
    fit = align.chunk_transform(base, chunk)
    assert fit is not None
    Mf, shared, inl, rms = fit
    assert shared == 20 and inl == 19 and rms < 1e-6
    assert np.allclose(Mf, M, atol=1e-6)
    assert align.chunk_transform(base, {"im0": np.zeros(3), "im1": np.ones(3)}) is None


def test_merge_two_overlapping_synthetic_reconstructions(tmp_path):
    pycolmap = pytest.importorskip("pycolmap")
    pycolmap.set_random_seed(1)
    db_path = tmp_path / "db.db"
    opts = pycolmap.SyntheticDatasetOptions()
    opts.num_rigs = 1
    opts.num_cameras_per_rig = 1
    opts.num_frames_per_rig = 16
    opts.num_points3D = 400
    opts.num_points2D_without_point3D = 5
    db = pycolmap.Database.open(str(db_path))
    truth = pycolmap.synthesize_dataset(opts, db)
    db.close()
    ids = sorted(truth.reg_image_ids())
    a_ids, b_ids = set(ids[:10]), set(ids[6:])               # 4 shared photos

    def crop(keep):
        r = pycolmap.Reconstruction(truth)
        for i in ids:
            if i not in keep:
                r.deregister_frame(r.image(i).frame_id)
        return r

    A, B = crop(a_ids), crop(b_ids)
    M = geom.sim_matrix(2.5, geom.small_rotation([0.5, 0.1, -0.7]), [10, 0, -3])
    s, R, t = geom.sim_parts(M)
    B.transform(pycolmap.Sim3d(s, pycolmap.Rotation3d(R), t))   # chunk B lives in another frame
    args = SimpleNamespace(threads=2)
    steps = []
    merged, joins = align.merge_chunks(pycolmap, [A, B], db_path, tmp_path, tmp_path / "out", args, steps)
    assert joins[0]["joined"] and joins[0]["shared_photos"] == 4
    assert merged.num_reg_images() == len(ids)
    assert merged.num_points3D() > 100
    # Merged camera centers match the truth (A was not moved, so the frames agree up to BA gauge).
    tc = np.array([truth.image(i).projection_center() for i in ids])
    mc = np.array([merged.image(i).projection_center() for i in ids])
    G = geom.umeyama(mc, tc)
    err = np.linalg.norm(geom.sim_apply(G, mc) - tc, axis=1)
    assert err.max() < 1e-3 * np.ptp(tc, axis=0).max()


# --------------------------------------------------------------------------
# sources.json visibility on a box building
# --------------------------------------------------------------------------

_n = [0]


def look(C, target, w=2000, h=1500, f=1500.0):
    z = np.asarray(target, float) - np.asarray(C, float)
    z /= np.linalg.norm(z)
    x = np.cross(z, [0, 0, 1.0])
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    R = np.stack([x, y, z])
    C = np.asarray(C, float)
    _n[0] += 1
    return {"id": f"cam{_n[0]}", "center": C, "R": R, "t": -R @ C, "w": w, "h": h,
            "cam": {"model": "SIMPLE_RADIAL", "params": [f, w / 2, h / 2, 0.0], "width": w, "height": h}}


def box(x0, y0, x1, y1):
    from shapely.geometry import Polygon
    return Polygon([(x0, y0), (x1, y0), (x1, y1), (x0, y1)])


def test_visibility_box_building():
    from shapely.strtree import STRtree
    # Building 20 x 20 m centered at the origin, 10 m tall; its south facade is y = -10.
    south = [[-10.0, -10.0], [10.0, -10.0]]
    S = src.facade_samples(south, 0.0, 10.0, 0.5)
    assert len(S) == 40 * 20
    normal = [0.0, -1.0]
    front = look([0, -30, 1.6], [0, -10, 5])                  # faces the south facade
    behind = look([0, 30, 1.6], [0, -10, 5])                  # north of the building
    away = look([0, -30, 1.6], [50, -30, 1.6])                # in front but looking east
    oblique = look([60, -12, 1.6], [0, -10, 5])               # grazing view, ~88 deg incidence
    vis = src.visibility(S, normal, [front, behind, away, oblique], (None, []), 80.0)
    assert front["id"] in vis and behind["id"] not in vis and away["id"] not in vis
    seen, inc, dist = vis[front["id"]]
    assert seen.mean() > 0.9 and inc[seen].max() < 70
    cov, md = src.coverage(len(S), {front["id"]: vis[front["id"]]})
    assert cov == 0.0                                          # one photo is not enough (needs >= 2)
    second = look([5, -28, 1.6], [0, -10, 5])
    vis2 = src.visibility(S, normal, [front, second], (None, []), 80.0)
    cov2, md2 = src.coverage(len(S), vis2)
    assert cov2 > 0.8 and 15 < md2 < 30
    if oblique["id"] in vis:                                   # grazing photos do not add coverage
        assert (vis[oblique["id"]][1][vis[oblique["id"]][0]] > 70).all()
    # A shed between the camera and the west half of the facade blocks that half.
    shed = [box(-9, -22, -1, -18)]
    vis3 = src.visibility(S, normal, [front], (STRtree(shed), shed), 80.0)
    seen3 = vis3[front["id"]][0]
    west = S[:, 0] < -3
    east = S[:, 0] > 3
    assert seen3[west].mean() < 0.2 and seen3[east].mean() > 0.9


def test_project_matches_pinhole():
    c = look([0, 0, 0], [0, 10, 0])
    xy, z = src.project(c["cam"], c["R"], c["t"], np.array([[0, 10, 0.0], [1, 10, 0.0]]))
    assert np.allclose(xy[0], [1000, 750]) and xy[1, 0] > 1000 and np.allclose(z, 10)


if __name__ == "__main__":
    sys.exit(pytest.main([__file__, "-q"]))
