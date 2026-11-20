"""Stage 4: facade layout round trip and the assembler on a synthetic box building."""
import json
import sys
from pathlib import Path

import numpy as np
import pytest

TOOLS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(TOOLS / "pipeline"))
sys.path.insert(0, str(TOOLS / "blender"))

import facade_layout as fl  # noqa: E402
import vcmesh  # noqa: E402
from kits import geom  # noqa: E402

BID = "B_9999"
FOOT = [[0, 0], [20, 0], [20, 10], [0, 10]]


def box_mesh(x1=20.0, y1=10.0, z0=-0.5, z1=12.0):
    mb = geom.MeshBuilder()
    mb.box(0, x1, 0, y1, z0, z1, "brick_red")     # x, y, z = campus X, Y, Z here
    m = mb.finish()
    return m


@pytest.fixture()
def archive(tmp_path, monkeypatch):
    monkeypatch.setenv("VC_DATA_ROOT", str(tmp_path))
    b = tmp_path / "buildings" / BID / "v2"
    m = box_mesh()
    for name in ("lod2", "lod1", "col_mesh"):
        vcmesh.save_mesh(b / f"{name}.npz", m["verts"], m["faces"], m["face_mat"], m["uv0"])
    (b / "model.json").write_text(json.dumps({"building": BID, "name": "Test box", "ground_z": 0.0,
                                              "parts": [{"eave_z": 12.0, "area_m2": 200.0}], "accuracy": {}}))
    (b / "ledger.json").write_text(json.dumps({"building": BID, "entries": []}))
    idx = tmp_path / "derived" / "buildings" / "v2" / "index.json"
    idx.parent.mkdir(parents=True)
    idx.write_text(json.dumps({"buildings": [{"id": BID, "name": "Test box", "footprint": FOOT, "holes": []}]}))
    return tmp_path


def make_layout(fc):
    W, H = fc["plane"]["width_m"], fc["plane"]["height_m"]
    feats = [fl.new_feature("w001", "window", [2, 4, 3.3, 7], arch="pointed", source="synthetic"),
             fl.new_feature("w002", "window", [5, 4, 6.2, 6.5], arch="none", source="synthetic"),
             fl.new_feature("c001", "string_course", [0, 3.0, W, 3.35], depth_m=0.25, source="synthetic"),
             fl.new_feature("k001", "cornice", [0, H - 0.7, W, H], depth_m=0.35, source="synthetic")]
    if fc["facade"] == "F01":
        feats += [fl.new_feature("d001", "door", [8, 1.0, 10, 3.6], arch="round", depth_m=-0.4, source="synthetic"),
                  fl.new_feature("s001", "steps", [7.5, 0.0, 10.5, 1.0], depth_m=2.0, source="synthetic"),
                  fl.new_feature("b001", "buttress", [12, 0, 12.8, 8], depth_m=0.6, source="synthetic")]
    return fl.new_layout(BID, fc["facade"], fc["plane"], feats)


def test_facades_of_box(archive):
    fcs = fl.facades_of(BID, archive)
    assert [f["facade"] for f in fcs] == ["F01", "F02", "F03", "F04"]
    # F01 starts at the north-west corner and runs clockwise: the north wall, seen from the north
    p = fcs[0]["plane"]
    assert np.allclose(p["normal"], [0, 1, 0], atol=1e-6)
    assert np.allclose(p["origin"][:2], [20, 10]) and abs(p["width_m"] - 20) < 1e-6
    assert abs(p["height_m"] - 12) < 1e-6
    for f in fcs:
        w = fl.to_world(f["plane"], f["plane"]["width_m"] / 2, 1.0, 1.0)
        assert not (0 < w[0] < 20 and 0 < w[1] < 10)        # 1 m along the normal is outside
        assert np.allclose(fl.to_facade(f["plane"], w), [f["plane"]["width_m"] / 2, 1.0, 1.0])


def test_layout_round_trip_and_versions(archive):
    fc = fl.facades_of(BID, archive)[0]
    lay = make_layout(fc)
    assert fl.validate(lay) == []
    p1 = fl.save(lay, archive)
    p2 = fl.save(lay, archive)
    assert p1.name == "layout_v1.json" and p2.name == "layout_v2.json"     # never overwritten
    back = fl.load(p1)
    assert back["features"] == lay["features"] and back["plane"] == lay["plane"]
    assert fl.load_latest(BID, "F01", archive)["version"] == 2
    bad = json.loads(json.dumps(lay))
    bad["features"][0]["type"] = "gargoyle"
    bad["plane"]["v"] = [0, 1, 0]
    probs = fl.validate(bad)
    assert any("unknown type" in p for p in probs) and any("v must be" in p for p in probs)


@pytest.mark.parametrize("level", ["3", "3+"])
def test_assembler_on_box(archive, level):
    import assemble_building as ab
    import check_building as cb
    lays = {f["facade"]: fl.load(fl.save(make_layout(f), archive)) for f in fl.facades_of(BID, archive)}
    s = ab.assemble(BID, "victorian_gothic", level, root=archive, layouts=lays, roof={}, blend=False)
    assert s["lod3_problems"] == [] and s["collider"]["problems"] == []
    out = Path(s["folder"])
    for name in ("lod3", "lod2", "lod1", "impostor", "game_col"):
        assert (out / f"{name}.npz").exists()
    model = json.loads((out / "model.json").read_text())
    assert model["measured"] is False and model["level"] == level
    idx = json.loads((archive / "derived" / "buildings" / "lod3" / "index.json").read_text())
    assert idx["buildings"][0]["id"] == BID
    res = cb.run(out, archive, photo=False)
    for k in ("files", "mesh", "merge", "collider", "envelope"):
        assert res["checks"][k]["ok"], (k, res["checks"][k])
    assert res["checks"]["collider"]["steps_ok"] and res["checks"]["collider"]["ledges_ok"]
    ledger = json.loads((out / "ledger.json").read_text())
    if level == "3":
        assert any(e["type"] == "window_frame" for e in ledger["entries"])     # merged under 15 cm
    imp = vcmesh.load_mesh(out / "impostor.npz")
    assert len(imp["faces"]) == 12 and vcmesh.check_mesh(imp, need_watertight=True) == []


def test_photo_check_synthetic_camera(archive, tmp_path):
    """Render the model from a known camera, save it as the 'photo', and run the photo check:
    model edges must land on the photo's edges (median offset a few cm)."""
    import assemble_building as ab
    import check_building as cb
    import render_building as rb
    from PIL import Image
    lays = {f["facade"]: fl.load(fl.save(make_layout(f), archive)) for f in fl.facades_of(BID, archive)}
    s = ab.assemble(BID, "victorian_gothic", "3", root=archive, layouts=lays, roof={}, blend=False)
    out = Path(s["folder"])
    mesh = vcmesh.load_mesh(out / "lod3.npz")
    w, h = 800, 600
    K = rb.intrinsics(w, h, 60)
    R, t = rb.look_at([10, 30, 4], [10, 10, 5])
    Image.fromarray(rb.render([mesh], K, R, t, w, h)).save(archive / "photo.png")
    cams = {"cameras": [{"id": "synthetic_01", "image": str(archive / "photo.png"), "width": w, "height": h,
                         **K, "distortion": [0, 0, 0, 0], "R": R.tolist(), "t": t.tolist()}]}
    cdir = archive / "derived" / "alignment" / "synthetic" / "v1"
    cdir.mkdir(parents=True)
    (cdir / "cameras.json").write_text(json.dumps(cams))
    res = cb.run(out, archive, alignments=["synthetic/v1"], overlap=False)
    pc = res["photo_check"]
    assert pc["images_used"] == 1
    assert "F01" in pc["per_facade"]
    assert pc["per_facade"]["F01"]["median_m"] < 0.05
