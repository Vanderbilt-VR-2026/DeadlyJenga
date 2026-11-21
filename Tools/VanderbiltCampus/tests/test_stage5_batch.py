"""Stage 5: build_all decisions, the index's review handling, readiness check and the CPU bake,
all on a synthetic box building with one synthetic, perfectly aligned 'photo'."""
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
SET = "synthetic-set"
FOOT = [[0, 0], [20, 0], [20, 10], [0, 10]]


@pytest.fixture()
def archive(tmp_path, monkeypatch):
    monkeypatch.setenv("VC_DATA_ROOT", str(tmp_path))
    b = tmp_path / "buildings" / BID / "v2"
    mb = geom.MeshBuilder()
    mb.box(0, 20, 0, 10, -0.5, 12, "brick_red")
    m = mb.finish()
    for name in ("lod2", "lod1", "col_mesh"):
        vcmesh.save_mesh(b / f"{name}.npz", m["verts"], m["faces"], m["face_mat"], m["uv0"])
    (b / "model.json").write_text(json.dumps({"building": BID, "name": "Test box", "ground_z": 0.0,
                                              "parts": [{"eave_z": 12.0, "area_m2": 200.0}], "accuracy": {},
                                              "materials": ["brick_red", "roof_flat"]}))
    (b / "ledger.json").write_text(json.dumps({"building": BID, "entries": []}))
    idx = tmp_path / "derived" / "buildings" / "v2" / "index.json"
    idx.parent.mkdir(parents=True)
    idx.write_text(json.dumps({"buildings": [{"id": BID, "name": "Test box", "footprint": FOOT, "holes": []}]}))
    return tmp_path


def layout(fc, source):
    W, H = fc["plane"]["width_m"], fc["plane"]["height_m"]
    feats = [fl.new_feature("w001", "window", [3, 4, 4.3, 6.5], source=source),
             fl.new_feature("w002", "window", [8, 4, 9.3, 6.5], source=source),
             fl.new_feature("c001", "string_course", [0, 3.0, W, 3.3], depth_m=0.25, source=source)]
    return fl.new_layout(BID, fc["facade"], fc["plane"], feats,
                         source={"alignment_set": f"{SET}/v1" if source != "estimated" else None})


def add_photo(archive, mesh=None):
    """Render the model (default: the plain box) from a camera north of it and store it as a
    Stage 3 style aligned photo (two reliable cameras, one unreliable)."""
    import render_building as rb
    from PIL import Image
    if mesh is None:
        mb = geom.MeshBuilder()
        mb.box(0, 20, 0, 10, -0.5, 12, "brick_red")
        mesh = mb.finish()
    w, h = 800, 600
    K = rb.intrinsics(w, h, 60)
    C = np.array([10.0, 32.0, 5.0])
    R, t = rb.look_at(C, [10, 10, 5])
    img = rb.render([mesh], K, R, t, w, h)
    p = archive / "derived" / "capture" / SET / "blurred" / "p1.png"
    p.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(img).save(p)
    cams = {"images": [{"name": n, "derived": f"derived/capture/{SET}/blurred/p1.png", "mask": None,
                        "camera": {"model": "PINHOLE", "params": [K["fx"], K["fy"], K["cx"], K["cy"]], "width": w, "height": h},
                        "campus": {"center": C.tolist(), "R_cam_from_campus": R.tolist(), "reliable": rel}}
                       for n, rel in (("p1.png", True), ("p2.png", True), ("p3.png", False))]}
    a = archive / "derived" / "alignment" / SET / "v1"
    a.mkdir(parents=True, exist_ok=True)
    (a / "cameras.json").write_text(json.dumps(cams))
    src = {"building": BID, "facades": {"north": {"photos": [f"{SET}/p1.png", f"{SET}/p2.png", f"{SET}/p3.png"],
                                                  "sets": {SET: {"alignment": f"derived/alignment/{SET}/v1"}},
                                                  "coverage_pct": 80.0}}}
    (archive / "buildings" / BID / "sources.json").write_text(json.dumps(src))
    return img


def test_pick_kit_rules():
    import build_all as ba
    rules = json.loads((TOOLS / "config" / "building_kits.json").read_text())
    assert ba.pick_kit("B_544397715", {}, {}, rules)[0] == "victorian_gothic"
    assert ba.pick_kit("B_x", {"building": "parking"}, {}, rules)[0] == "modern"
    assert ba.pick_kit("B_x", {"building": "yes"}, {"materials": ["stone_rough"]}, rules)[0] == "collegiate_gothic"
    assert ba.pick_kit("B_x", {}, {}, rules) == (rules["default"], "default")


def test_build_one_decisions(archive):
    import build_all as ba
    cams = ba.Cameras(archive)
    info = {"name": "Test box", "priority": "normal"}
    # no photos -> stays LoD2 and needs shots
    r = ba.build_one(BID, info, archive, cams, set(), json.loads((TOOLS / "config" / "building_kits.json").read_text()), {})
    assert r["status"] == "lod2_needs_shots"
    # reliable photos but no measured layout -> review, awaiting measurement
    add_photo(archive)
    cams = ba.Cameras(archive)
    fph = ba.facade_photos(BID, archive, cams)
    assert fph["north"]["reliable"] == 2          # the unreliable third photo does not count
    r = ba.build_one(BID, info, archive, cams, set(), json.loads((TOOLS / "config" / "building_kits.json").read_text()), {})
    assert r["status"] == "review" and "awaiting facade_measure" in r["reasons"][0]
    # an estimated layout is not a measured one
    fcs = {f["facade"]: f for f in fl.facades_of(BID, archive)}
    fl.save(layout(fcs["F01"], "estimated"), archive)
    assert ba.measured_layouts(BID, archive) == {}
    # a measured layout -> assembled, checked with the photo, accepted into the index
    lay = fl.load(fl.save(layout(fcs["F01"], "manual"), archive))
    assert list(ba.measured_layouts(BID, archive)) == ["F01"]
    # the 'photo' now shows the building as measured (windows and course included)
    import assemble_building as ab0
    s0 = ab0.assemble(BID, "brick_institutional", "3", root=archive, layouts={"F01": lay}, roof={}, write=False)
    add_photo(archive, s0["meshes"]["lod3"])
    r = ba.build_one(BID, info, archive, cams, set(), json.loads((TOOLS / "config" / "building_kits.json").read_text()), {})
    assert r["status"] == "lod3_measured", r.get("reasons")
    import assemble_building as ab
    ab._imports()
    ab.write_index(archive)
    idx = json.loads((archive / "derived" / "buildings" / "lod3" / "index.json").read_text())
    assert idx["buildings"][0]["measured"] is True
    # re-running does not assemble again (same layouts, kit and level)
    n = len(list((archive / "buildings" / BID).glob("lod3_v*")))
    ba.build_one(BID, info, archive, cams, set(), json.loads((TOOLS / "config" / "building_kits.json").read_text()), {})
    assert len(list((archive / "buildings" / BID).glob("lod3_v*"))) == n
    # a rejected version is kept on disk but left out of the index
    folder = Path(r["folder"])
    m = json.loads((folder / "model.json").read_text())
    m["accepted"] = False
    (folder / "model.json").write_text(json.dumps(m))
    ab.write_index(archive)
    idx = json.loads((archive / "derived" / "buildings" / "lod3" / "index.json").read_text())
    assert idx["buildings"] == [] and folder.exists()


def test_readiness_and_bake(archive, tmp_path):
    import assemble_building as ab
    import readiness_check as rc
    import realism_bake as rbk
    ab._imports()
    fcs = {f["facade"]: f for f in fl.facades_of(BID, archive)}
    lays = {"F01": fl.load(fl.save(layout(fcs["F01"], "estimated"), archive))}
    ab.assemble(BID, "brick_institutional", "3", root=archive, layouts=lays, roof={}, blend=False)
    res = rc.run(archive)
    assert res["rows"][0]["missing"] == ["photos"]           # reported, not hidden
    add_photo(archive)
    res = rc.run(archive)
    assert res["rows"][0]["missing"] == []
    info = rbk.run(BID, "F01", f"{SET}/v1", size=256, root=archive, out_img=tmp_path / "bake.jpg")
    assert info["cameras_used"] == 2 and info["coverage_pct"] > 60
    tex = np.asarray(__import__("PIL.Image", fromlist=["Image"]).open(archive / "derived" / "bake" / BID / "v1" / "F01_bake.png"))
    # baked wall texels carry the photo's (palette brick) color: red channel dominant
    lit = tex[tex.sum(axis=2) > 0]
    assert np.median(lit[:, 0]) > np.median(lit[:, 2]) + 40
    j = json.loads((archive / "derived" / "bake" / BID / "v1" / "bake.json").read_text())
    assert set(j["uv_rect"]) >= {"u0", "v0", "u1", "v1"} and j["exposure"]["gray_card"] is None
    assert (tmp_path / "bake.jpg").stat().st_size < 500 * 1024
