"""The coverage page builds offline from a tiny synthetic shot list (no archive data needed)."""
import base64
import gzip
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
import coverage_map as cm  # noqa: E402
from test_shotlist import entry, synthetic_shot  # noqa: E402


def tiny(tmp_path):
    shot = synthetic_shot(n=6)
    shot["entries"].append(dict(entry(99, 50, 50, kind="plaza", method="360_walk"), id="plaza/way-1",
                                centerline=[[40, 40], [60, 40], [60, 60], [40, 60], [40, 40]], building=None,
                                facade=None, normal=None, bearing_deg=None, standoff_m=None, name="Test Lawn"))
    shot["entries"].append(dict(entry(98, 10, 80, kind="detail", method="detail_set"), id="detail/statue-1",
                                centerline=[[10, 80]], camera_path=[], normal=None, building=None, facade=None))
    shot["buildings"] = [{"id": "B_0", "name": "Test Hall", "hero": True,
                          "outlines": [[[-5, 0], [5, 0], [5, 8], [-5, 8], [-5, 0]]]}]
    shot["hero_areas"] = [{"name": "Test Lawn", "approx": True, "how": "test",
                           "polygon": [[40, 40], [60, 40], [60, 60], [40, 60], [40, 40]]}]
    sp = tmp_path / "shot_list.json"
    sp.write_text(json.dumps(shot), encoding="utf-8")
    status = {"version": 1, "updated": "2026-12-02T10:00:00",
              "entries": {"B_0/south": {"status": "captured", "photos": 50, "last": "2026-12-02"},
                          "B_1/south": "aligned", "B_2/south": {"status": "needs_reshoot", "note": "blurry"},
                          "B_unknown/north": "captured"}}
    st = tmp_path / "coverage_status.json"
    st.write_text(json.dumps(status), encoding="utf-8")
    return shot, sp, st


def embedded(html):
    m = re.search(r'<script type="application/octet-stream" id="data" data-enc="([^"]+)">(.*?)</script>', html, re.S)
    assert m, "data block missing"
    enc, payload = m.group(1), m.group(2)
    if enc == "json":
        return json.loads(payload.replace("<\\/", "</"))
    return json.loads(gzip.decompress(base64.b64decode(payload)).decode("utf-8"))


def test_page_builds_and_contains_everything(tmp_path):
    shot, sp, st = tiny(tmp_path)
    out = tmp_path / "index.html"
    path, info = cm.build_page(sp, st, tmp_path / "none.json", out, with_background=False)
    html = out.read_text(encoding="utf-8")
    for e in shot["entries"]:
        assert e["id"] in html
    for s in cm.STATUSES:
        assert "st-" + s in html
    assert info["unknown_status_ids"] == ["B_unknown/north"]
    data = embedded(html)
    by = {e["i"]: e for e in data["entries"]}
    assert by["B_0/south"]["s"] == 1 and by["B_0/south"]["ph"] == 50
    assert by["B_1/south"]["s"] == 2 and by["B_2/south"]["s"] == 3 and "s" not in by["B_3/south"]
    assert data["buildings"][0]["h"] == 1
    # Self-contained: no external requests.
    assert not re.search(r'(src|href)="https?://', html)
    assert "<link" not in html and "@import" not in html
    assert info["bytes"] < 200_000


def test_page_without_status_file_is_all_not_started(tmp_path):
    shot, sp, _ = tiny(tmp_path)
    out = tmp_path / "index.html"
    cm.build_page(sp, tmp_path / "missing.json", tmp_path / "none.json", out, with_background=False)
    data = embedded(out.read_text(encoding="utf-8"))
    assert all("s" not in e for e in data["entries"])


def test_gzip_payload_and_idempotent(tmp_path, monkeypatch):
    shot, sp, st = tiny(tmp_path)
    monkeypatch.setattr(cm, "PLAIN_LIMIT", 0)
    a, b = tmp_path / "a.html", tmp_path / "b.html"
    cm.build_page(sp, st, tmp_path / "none.json", a, with_background=False)
    cm.build_page(sp, st, tmp_path / "none.json", b, with_background=False)
    assert a.read_bytes() == b.read_bytes()
    data = embedded(a.read_text(encoding="utf-8"))
    assert {e["i"] for e in data["entries"]} == {e["id"] for e in shot["entries"]}


def test_status_formats(tmp_path):
    p = tmp_path / "s.json"
    p.write_text(json.dumps([{"id": "a", "status": "aligned"}, {"id": "b", "status": "weird"}]), encoding="utf-8")
    st, _ = cm.load_status(p)
    assert st["a"]["status"] == "aligned" and st["b"]["status"] == "not_started"


def test_frame_affine_matches_the_frame():
    aff = cm.frame_affine()
    assert aff["max_err_m"] < 0.05
    import frame as fr
    import numpy as np
    lon, lat = aff["lon0"] + 0.004, aff["lat0"] - 0.006
    X, Y, _ = fr.to_campus(np.array([lon]), np.array([lat]), np.zeros(1), 4326)
    dl, dt = lon - aff["lon0"], lat - aff["lat0"]
    b = [1, dl, dt, dl * dl, dl * dt, dt * dt]
    assert abs(sum(c * v for c, v in zip(aff["cx"], b)) - X[0]) < 0.05
    assert abs(sum(c * v for c, v in zip(aff["cy"], b)) - Y[0]) < 0.05
