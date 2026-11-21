"""Stage 3 measurement: facade feature detection, plane fit, layout check and the review save path.

All synthetic and fast: a drawn facade image, a synthetic camera, temporary archive folders.
"""
import json
import sys
from pathlib import Path

import cv2
import numpy as np
import pytest

TOOLS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(TOOLS / "pipeline"))
sys.path.insert(0, str(TOOLS / "gpu"))
import facade_layout as fl  # noqa: E402
import facade_measure as fm  # noqa: E402
import facade_review as fr  # noqa: E402

PPM = 40
W, H = 12.0, 8.0
PLANE = {"origin": [0.0, 0.0, 10.0], "u": [1.0, 0.0, 0.0], "v": [0.0, 0.0, 1.0], "normal": [0.0, -1.0, 0.0],
         "width_m": W, "height_m": H}


def facade_image(windows, cornice_v=7.2):
    """Light wall, dark windows (rects in meters), a dark cornice line."""
    img = np.full((int(H * PPM), int(W * PPM), 3), 190, np.uint8)
    rng = np.random.default_rng(0)
    img = np.clip(img + rng.normal(0, 4, img.shape), 0, 255).astype(np.uint8)
    for u0, v0, u1, v1 in windows:
        cv2.rectangle(img, (int(u0 * PPM), int((H - v1) * PPM)), (int(u1 * PPM), int((H - v0) * PPM)), (50, 55, 60), -1)
    r = int((H - cornice_v) * PPM)
    cv2.rectangle(img, (0, r - 3), (img.shape[1], r + 3), (70, 70, 70), -1)
    return img


WINDOWS = [(1 + 2.5 * i, 3.5, 2.2 + 2.5 * i, 5.3) for i in range(4)] + [(1 + 2.5 * i, 0.9, 2.2 + 2.5 * i, 2.5) for i in range(1, 4)]
DOOR = (1.0, 0.0, 2.3, 2.3)


def test_detects_window_grid_door_and_cornice():
    img = facade_image(WINDOWS + [DOOR])
    valid = np.ones(img.shape[:2], bool)
    feats = fm.detect_features(img, valid, PPM, W, H)
    wins = [f for f in feats if f["type"] == "window"]
    doors = [f for f in feats if f["type"] == "door"]
    assert len(wins) == len(WINDOWS)
    for w0 in WINDOWS:
        best = min(wins, key=lambda f: np.abs(np.array(f["rect"]) - w0).max())
        assert np.abs(np.array(best["rect"]) - w0).max() < 0.1
        assert best["confidence"] > 0.5                       # repeated on its row
    assert len(doors) == 1 and np.abs(np.array(doors[0]["rect"]) - DOOR).max() < 0.1
    cor = [f for f in feats if f["type"] in ("cornice", "string_course") and abs(f["rect"][1] - 7.1) < 0.3]
    assert cor and cor[0]["type"] == "cornice"


def test_plane_offset_moves_wall_to_points():
    rng = np.random.default_rng(1)
    pts = np.column_stack([rng.uniform(0, W, 200), np.full(200, -0.35) + rng.normal(0, 0.03, 200),
                           rng.uniform(10, 18, 200)])
    new, info = fm.fit_plane_offset(PLANE, pts)
    assert info["points_near_wall"] == 200
    assert abs(info["shift_m"] - 0.35) < 0.02                 # normal points to -y, points sit at y = -0.35
    assert abs(new["origin"][1] + 0.35) < 0.02
    few, info2 = fm.fit_plane_offset(PLANE, pts[:5])
    assert few == PLANE and info2["shift_m"] == 0.0


def test_feature_depth_from_points():
    rng = np.random.default_rng(2)
    rect = [2.0, 2.0, 3.0, 3.5]
    inside = np.column_stack([rng.uniform(2.1, 2.9, 20), rng.uniform(2.1, 3.4, 20), np.full(20, -0.2)])
    around = np.column_stack([rng.uniform(1.6, 3.4, 40), rng.uniform(1.6, 3.9, 40), np.zeros(40)])
    around = around[~((around[:, 0] >= 2) & (around[:, 0] <= 3) & (around[:, 1] >= 2) & (around[:, 1] <= 3.5))]
    d = fm.feature_depth(rect, np.vstack([inside, around]))
    assert abs(d + 0.2) < 1e-6
    assert fm.feature_depth(rect, around) is None


def synthetic_camera():
    """A camera 15 m in front of the facade (facade faces -y), looking at its middle."""
    C = np.array([6.0, -15.0, 11.6])
    z = np.array([6.0, 0.0, 14.0]) - C
    z /= np.linalg.norm(z)
    x = np.cross(z, [0, 0, 1.0])
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    R = np.stack([x, y, z])
    cam = {"model": "SIMPLE_RADIAL", "params": [1500.0, 1000.0, 750.0, 0.0], "width": 2000, "height": 1500,
           "focal_px": 1500.0}
    return {"cam": cam, "R": R, "C": C, "t": -R @ C}


def test_edge_offset_recovers_known_shift():
    ph = synthetic_camera()
    rect = [3.0, 3.0, 5.0, 6.0]
    true = (0.20, -0.12)                                       # where the window really is
    img = np.zeros((1500, 2000), np.uint8)
    O = fm.outline_uv([rect[0] + true[0], rect[1] + true[1], rect[2] + true[0], rect[3] + true[1]], 0.01)
    xy, _ = fm.project(ph["cam"], ph["R"], ph["t"], fl.to_world(PLANE, O[:, 0], O[:, 1]))
    poly = xy.astype(np.int32).reshape(-1, 1, 2)
    cv2.fillPoly(img, [poly], 200)
    g = cv2.GaussianBlur(img, (0, 0), 1.5).astype(np.float32)
    gx, gy = cv2.Sobel(g, cv2.CV_32F, 1, 0), cv2.Sobel(g, cv2.CV_32F, 0, 1)
    r = fm.edge_offset(gx, gy, PLANE, ph, rect, 1.0, 1.0, search=0.5)
    assert abs(r["du"] - true[0]) <= 0.021 and abs(r["dv"] - true[1]) <= 0.021
    assert r["du_contrast"] > 1.5


@pytest.fixture()
def archive(tmp_path, monkeypatch):
    monkeypatch.setenv("VC_DATA_ROOT", str(tmp_path))
    return tmp_path


def base_layout():
    feats = [fl.new_feature("D001", "window", [1, 3.5, 2.2, 5.3], confidence=0.7, source="detected"),
             fl.new_feature("D002", "window", [3.5, 3.5, 4.7, 5.3], confidence=0.6, source="detected"),
             fl.new_feature("D003", "cornice", [0, 7.1, W, 7.3], confidence=0.8, source="detected")]
    return fl.new_layout("B_1", "F01", PLANE, feats,
                         source={"alignment_set": "s/v1", "photos": ["s/a.jpg"], "rectified_image": None, "px_per_m": 40.0})


def test_review_save_path_writes_new_version(archive):
    fl.save(base_layout(), archive, record=False)
    edits = {"building": "B_1", "facade": "F01", "base_version": 1, "features": [
        {"id": "D001", "type": "window", "rect": [1, 3.5, 2.2, 5.3], "arch": "none", "accepted": True},
        {"id": "D003", "type": "cornice", "rect": [0, 7.0, W, 7.3], "arch": "none", "accepted": True},
        {"id": None, "type": "door", "rect": [1.0, 0.0, 2.3, 2.3], "arch": "round", "accepted": True}]}
    path, summary = fr.save_review("B_1", "F01", edits, archive)
    lay = fl.load(path)
    assert path.name == "layout_v2.json" and lay["version"] == 2 and lay["reviewed"] is True
    by = {f["id"]: f for f in lay["features"]}
    assert by["D001"]["source"] == "detected" and by["D001"]["reviewed"] is True
    assert by["D003"]["source"] == "manual" and by["D003"]["rect"][1] == 7.0
    assert "D002" not in by
    new = [f for f in lay["features"] if f["id"].startswith("M")]
    assert len(new) == 1 and new[0]["type"] == "door" and new[0]["source"] == "manual" and new[0]["arch"] == "round"
    assert summary == {"saved": "layout_v2.json", "features": 3, "kept": 1, "manual": 2, "deleted": 1}
    assert fl.load(archive / "buildings" / "B_1" / "facades" / "F01" / "layout_v1.json")["version"] == 1   # untouched
    with pytest.raises(ValueError):
        fr.save_review("B_1", "F01", dict(edits, base_version=1), archive)                           # stale base


def test_review_page_embeds_image_and_boxes(archive):
    d = archive / "buildings" / "B_1" / "facades" / "F01"
    d.mkdir(parents=True)
    cv2.imwrite(str(d / "rectified_v1.jpg"), facade_image(WINDOWS))
    lay = base_layout()
    lay["source"]["rectified_image"] = "buildings/B_1/facades/F01/rectified_v1.jpg"
    fl.save(lay, archive, record=False)
    html = fr.render_page("B_1", archive, served=True)
    assert "data:image/jpeg;base64," in html and '"D002"' in html and "SERVED=true;" in html
    assert "__DATA__" not in html


def test_dense_script_commands(tmp_path):
    import dense_colmap as dc
    reg = {"transforms": {"1.4": {"colmap_to_campus_4x4": [[2, 0, 0, 1], [0, 2, 0, 2], [0, 0, 2, 3], [0, 0, 0, 1]]}}}
    dc.transform_file(reg, "1.4", tmp_path / "t.txt")
    rows = [list(map(float, r.split())) for r in (tmp_path / "t.txt").read_text().splitlines()]
    assert rows == [[2, 0, 0, 1], [0, 2, 0, 2], [0, 0, 2, 3]]
    cmds = dc.commands("colmap", tmp_path / "img", tmp_path / "sparse", tmp_path / "w", tmp_path / "l.txt", 3200, "0")
    assert [c[0] for c in cmds] == ["model_transformer", "image_undistorter", "patch_match_stereo", "stereo_fusion",
                                    "poisson_mesher", "delaunay_mesher"]
    assert "--PatchMatchStereo.geom_consistency" in cmds[2][1]


if __name__ == "__main__":
    sys.exit(pytest.main([__file__, "-q"]))
