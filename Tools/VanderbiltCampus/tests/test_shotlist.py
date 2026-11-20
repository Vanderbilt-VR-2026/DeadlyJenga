"""Fast synthetic checks for the Stage 2 shot list and capture days (no archive data needed)."""
import datetime as dt
import math
import sys
from pathlib import Path

import pytest
from shapely.geometry import Polygon
from shapely.geometry.polygon import orient

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
import capture_days as cd  # noqa: E402
import shotlist as sl  # noqa: E402

NOV = dt.date(2026, 11, 20)


def facades_of(coords, hole=False):
    poly = orient(Polygon(coords), 1.0)
    return sl.name_facades(sl.split_ring(list(poly.exterior.coords)))


def test_l_shape_sides_bearings_lengths():
    fs = facades_of([(0, 0), (30, 0), (30, 10), (10, 10), (10, 25), (0, 25)])
    got = {f["name"]: (round(f["length"], 2), round(f["bearing"], 1)) for f in fs}
    assert got == {"south": (30.0, 180.0), "east-1": (15.0, 90.0), "east-2": (10.0, 90.0),
                   "north-1": (20.0, 0.0), "north-2": (10.0, 0.0), "west": (25.0, 270.0)}
    for f in fs:  # normals are unit vectors pointing away from the building
        assert math.isclose(math.hypot(*f["normal"]), 1.0, abs_tol=1e-9)


def test_ring_start_and_direction_do_not_matter():
    a = facades_of([(10, 10), (10, 25), (0, 25), (0, 0), (30, 0), (30, 10)])
    b = facades_of([(0, 0), (0, 25), (10, 25), (10, 10), (30, 10), (30, 0)])   # clockwise input
    key = lambda fs: sorted((f["name"], round(f["length"], 2)) for f in fs)  # noqa: E731
    assert key(a) == key(b)


def test_small_jog_joins_the_wall_and_curves_split():
    fs = facades_of([(0, 0), (20, 0), (20, 10), (12, 10), (12, 11), (8, 11), (8, 10), (0, 10)])
    names = {f["name"]: round(f["length"], 1) for f in fs}
    assert names == {"south": 20.0, "east": 10.0, "north": 22.0, "west": 10.0}
    # A nearly straight wall (5 degree kinks) stays one facade.
    wall = [(0, 0), (10, 0), (20, 0.87), (30, 2.6), (30, 12), (0, 12)]
    fs = facades_of(wall)
    assert sum(1 for f in fs if f["name"].startswith("south")) == 1
    # A circle (36 segments of 10 degrees) becomes several arcs, none over ~40 degrees of turn.
    circ = [(20 * math.cos(math.radians(a)), 20 * math.sin(math.radians(a))) for a in range(0, 360, 10)]
    fs = facades_of(circ)
    assert 8 <= len(fs) <= 18


def test_courtyard_walls_face_into_the_courtyard():
    poly = orient(Polygon([(0, 0), (40, 0), (40, 40), (0, 40)], [[(10, 10), (10, 30), (30, 30), (30, 10)]]), 1.0)
    fs = sl.name_facades(sl.split_ring(list(poly.interiors[0].coords)), "courtyard-")
    by = {f["name"]: f for f in fs}
    assert set(by) == {"courtyard-north", "courtyard-south", "courtyard-east", "courtyard-west"}
    # The wall along y = 10 is the courtyard's south side; it faces north into the courtyard.
    south_wall = min(fs, key=lambda f: sum(p[1] for p in f["points"]))
    assert south_wall["name"] == "courtyard-north" and round(south_wall["bearing"]) == 0


def test_compass_sides():
    assert [sl.compass_side(b) for b in (0, 44, 46, 90, 135, 180, 225, 270, 315, 359)] == \
        ["north", "north-east", "north-east", "east", "south-east", "south", "south-west", "west", "north-west",
         "north"]


def test_est_photos_formula():
    step = sl.photo_step(6.0)
    assert math.isclose(step, 2 * 6 * math.tan(math.radians(35)) * 0.25)
    # 20 m wall, 6 m stand-off, 4 m tall: 3 passes x 2 heights x ceil(20 / 2.1) + 6 corner shots x 2 heights.
    assert sl.est_photos(20, 6, 4) == 3 * 2 * math.ceil(20 / step) + 12 == 72
    # Tall wall (10 m > 1.2 x 6 m): add one far pass from 10 m.
    assert sl.est_photos(20, 6, 10) == 72 + math.ceil(20 / sl.photo_step(10.0)) == 78
    assert sl.min_photos(72) == 29 and sl.min_photos(0) == 0 and sl.min_photos(4) == 3
    assert sl.est_photos(0, 6, 4) == 0
    assert sl.standoff_for(2) == 3.0 and sl.standoff_for(10) == 8.0 and sl.standoff_for(40) == 15.0
    assert sl.standoff_for(20, space=5.0) == 5.0 and sl.standoff_for(20, space=1.0) == 3.0


def test_sun_noon_elevation():
    best = max(sl.sun_az_el(dt.datetime(2026, 11, 20, 17, m, tzinfo=dt.timezone.utc))[1] for m in range(0, 60, 2))
    # 90 - 36.15 - declination (about -19.8 deg) = about 34 deg.
    assert 33.0 < best < 35.5


def test_best_time_four_sides_in_november():
    n, e, s, w = (sl.best_time(b, NOV) for b in (0, 90, 180, 270))
    assert n["summary"].startswith("any time") and n["morning_ok"] and n["afternoon_ok"]
    assert "afternoon" in e["summary"] and e["afternoon_ok"] and not e["morning_ok"]
    assert "morning" in w["summary"] and w["morning_ok"] and not w["afternoon_ok"]
    assert s["summary"].startswith("sunlit all day") and not s["morning_ok"] and not s["afternoon_ok"]
    for bt in (n, e, s, w):
        assert bt["summary"].endswith("overcast: any time")


def test_best_time_north_in_june_is_not_all_day():
    n = sl.best_time(0, dt.date(2026, 6, 21))
    assert not n["summary"].startswith("any time")      # early and late sun reaches north walls in summer
    assert n["shade"]


def entry(i, x, y, est=40, kind="facade", method="photo_set", prio="normal", **kw):
    e = {"id": f"B_{i}/south", "kind": kind, "building": f"B_{i}", "facade": "south", "priority": prio,
         "method": method, "est_photos": est, "min_photos": sl.min_photos(est),
         "centerline": [[x - 5, y], [x + 5, y]], "normal": [0.0, -1.0], "bearing_deg": 180.0, "standoff_m": 5.0,
         "camera_path": [[x - 5, y - 5], [x + 5, y - 5]], "tile": "T_00_00",
         "best_time": sl.best_time(180, NOV), "access": "public", "access_notes": "", "example_framing": "",
         "day": None, "length_m": 10.0}
    e.update(kw)
    return e


def test_schema_validation():
    doc = {"version": 1, "generated": "2026-11-20T00:00:00", "entries": [entry(1, 0, 0)]}
    assert sl.validate(doc) == []
    bad = {"version": 1, "generated": "x", "entries": [dict(entry(1, 0, 0), method="video")]}
    assert any("method" in m for m in sl.validate(bad))
    missing = entry(1, 0, 0)
    del missing["camera_path"]
    assert any("camera_path" in m for m in sl.validate({"version": 1, "generated": "x", "entries": [missing]}))
    dup = {"version": 1, "generated": "x", "entries": [entry(1, 0, 0), entry(1, 5, 5)]}
    assert any("duplicate" in m for m in sl.validate(dup))
    assert sl.validate({"entries": []})


def synthetic_shot(n=240, spacing=35.0):
    es = []
    for i in range(n):
        x, y = (i % 16) * spacing, (i // 16) * spacing
        es.append(entry(i, x, y, est=30 + (i * 7) % 50, prio="hero" if i < 20 else "normal"))
    clusters = [{"id": "C01", "title": "C01 west", "center": [100, 200], "buildings": [f"B_{i}" for i in range(n) if i % 16 < 8],
                 "hero": True},
                {"id": "C02", "title": "C02 east", "center": [420, 200], "buildings": [f"B_{i}" for i in range(n) if i % 16 >= 8],
                 "hero": False}]
    return {"version": 1, "generated": "2026-11-20T00:00:00", "entries": es, "clusters": clusters}


def test_days_are_2_to_3_5_hours():
    shot = synthetic_shot()
    out = cd.build(graph=cd.EuclidGraph(), shot=shot, write=False)
    days = out["days"]
    assert len(days) >= 3
    for d in days:
        assert 2.0 <= d["hours"] <= 3.5, d["hours"]
    ids = [s["id"] for d in days for s in d["stops"]]
    assert sorted(ids) == sorted(e["id"] for e in shot["entries"])       # every entry exactly once
    assert all(e["day"] is not None for e in shot["entries"])
    # The hero cluster (C01, west half) is walked before the other one; at most one day is shared.
    west = [e["day"] for i, e in enumerate(shot["entries"]) if i % 16 < 8]
    east = [e["day"] for i, e in enumerate(shot["entries"]) if i % 16 >= 8]
    assert max(west) <= min(east)


def test_first_capture_entries_are_day_one_and_background_is_optional():
    shot = synthetic_shot()
    for e in shot["entries"][200:205]:
        e["first_capture"] = True
    for e in shot["entries"][100:140]:
        e["priority"] = "background"
    out = cd.build(graph=cd.EuclidGraph(), shot=shot, write=False)
    by = {e["id"]: e for e in shot["entries"]}
    assert all(by[e["id"]]["day"] == 1 for e in shot["entries"][200:205])
    opt = [d for d in out["days"] if d["optional"]]
    assert opt and all(by[s["id"]]["priority"] == "background" for d in opt for s in d["stops"])
    assert min(d["day"] for d in opt) > max(d["day"] for d in out["days"] if not d["optional"])


def test_two_opt_untangles_a_square():
    import numpy as np
    pts = np.array([[0, 0], [10, 10], [10, 0], [0, 10]], float)
    D = np.hypot(pts[:, None, 0] - pts[None, :, 0], pts[:, None, 1] - pts[None, :, 1])
    tour = cd.tour_2opt(D, [0, 1, 2, 3])
    length = sum(D[a, b] for a, b in zip(tour, tour[1:] + tour[:1]))
    assert length == pytest.approx(40.0)


def test_time_model():
    e = entry(1, 0, 0, est=90)
    assert cd.entry_seconds(e) == 360.0
    walk = dict(entry(2, 0, 0), method="360_walk", kind="path", length_m=150.0)
    assert cd.entry_seconds(walk) == 300.0     # two passes at 1 m/s
