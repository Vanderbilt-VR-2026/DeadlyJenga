"""Intake (Stage 2) plumbing on tiny synthetic data made inside the tests.

Covers: copy/hash/write-once, EXIF GPS -> campus frame, area assignment
geometry with a fake shot list, 360 frame splitting, quality flags,
blur/mask rules, and the coverage_status merge. Detector accuracy is not
tested here (that is the 50-image spot check).
"""
import json
import shutil
import stat
import subprocess
import sys
from pathlib import Path

import cv2
import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
import common  # noqa: E402
import frame  # noqa: E402
import intake  # noqa: E402
import intake_areas  # noqa: E402
import intake_copy  # noqa: E402
import intake_coverage  # noqa: E402
import intake_images  # noqa: E402
import intake_meta  # noqa: E402
import intake_privacy  # noqa: E402
import intake_quality  # noqa: E402
import intake_video  # noqa: E402
import manifest  # noqa: E402


def _exiftool():
    try:
        exe = common.local_config().get("exiftool")
    except SystemExit:
        exe = None
    if exe and Path(exe).exists():
        return exe
    return shutil.which("exiftool")


EXIFTOOL = _exiftool()
needs_exiftool = pytest.mark.skipif(not EXIFTOOL, reason="exiftool not installed")
needs_ffmpeg = pytest.mark.skipif(not shutil.which("ffmpeg"), reason="ffmpeg not on PATH")


def textured(seed=0, size=(720, 960)):
    """A sharp, mid-gray test picture (random blocks + edges)."""
    rng = np.random.default_rng(seed)
    small = rng.integers(40, 210, (size[0] // 16, size[1] // 16, 3), dtype=np.uint8)
    img = cv2.resize(small, (size[1], size[0]), interpolation=cv2.INTER_NEAREST)
    cv2.putText(img, f"set {seed}", (40, 200), cv2.FONT_HERSHEY_SIMPLEX, 3, (20, 20, 20), 6)
    return img


def write_photo(path, img, lat=None, lon=None, when="2026:11:20 10:00:00", heading=None):
    intake_images.write_jpeg(path, img, 92)
    args = [EXIFTOOL, "-q", "-overwrite_original", f"-DateTimeOriginal={when}", "-Model=TestCam"]
    if lat is not None:
        args += [f"-GPSLatitude={abs(lat)}", f"-GPSLatitudeRef={'N' if lat >= 0 else 'S'}",
                 f"-GPSLongitude={abs(lon)}", f"-GPSLongitudeRef={'E' if lon >= 0 else 'W'}"]
    if heading is not None:
        args += [f"-GPSImgDirection={heading}", "-GPSImgDirectionRef=T"]
    subprocess.run(args + [str(path)], check=True, capture_output=True)


def run_intake(tmp_path, source, *extra):
    argv = [str(source), "--jobs", "1", "--skip-blur",
            "--shot-list", str(tmp_path / "no_shot_list.json"),
            "--footprints", str(tmp_path / "no_footprints.json"),
            "--coverage", str(tmp_path / "coverage_status.json"),
            "--exiftool", EXIFTOOL, *extra]
    intake.main(argv)


# ---- copy, hash, write-once ---------------------------------------------

@needs_exiftool
def test_copy_is_hashed_write_once_and_idempotent(tmp_path, monkeypatch):
    archive = tmp_path / "archive"
    monkeypatch.setenv("VC_DATA_ROOT", str(archive))
    card = tmp_path / "card"
    lon, lat, _ = frame.from_campus(150.0, -50.0, 0.0, 4326)
    write_photo(card / "IMG_0001.JPG", textured(1), float(lat), float(lon))
    write_photo(card / "IMG_0002.JPG", textured(2), float(lat), float(lon), when="2026:11:20 10:00:05")
    (card / ".DS_Store").write_bytes(b"junk")

    run_intake(tmp_path, card, "--date", "2026-11-20", "--device", "iphone")
    raw = archive / "raw" / "capture" / "2026-11-20" / "iphone"
    files = sorted(p.name for p in raw.iterdir())
    assert files == ["IMG_0001.JPG", "IMG_0002.JPG"]                    # junk not copied
    first = raw / "IMG_0001.JPG"
    assert intake_copy.sha256(first) == intake_copy.sha256(card / "IMG_0001.JPG")
    assert not (first.stat().st_mode & stat.S_IWRITE)                    # read-only
    ok, problems = manifest.verify("raw/", root=archive)
    assert ok == 2 and problems == []
    db = manifest.connect(archive)
    lic = db.execute("SELECT license, stage FROM files WHERE path LIKE 'raw/capture/%IMG_0001.JPG'").fetchone()
    db.close()
    assert lic == (intake.OWN_LICENSE, 2)

    out = archive / "derived" / "capture" / "2026-11-20" / "iphone"
    rep = json.loads((out / "intake_report.json").read_text())
    assert rep["originals"]["copied"] == 2 and rep["captured"]["photos"] == 2

    # Second run: nothing copied again.
    run_intake(tmp_path, card, "--date", "2026-11-20", "--device", "iphone")
    rep = json.loads((out / "intake_report.json").read_text())
    assert rep["originals"]["skipped_same"] == 2 and rep["originals"]["copied"] == 0
    assert rep["captured"]["reused_from_last_run"] == 2

    # A different file with the same name is stored beside it, never over it.
    card2 = tmp_path / "card2"
    write_photo(card2 / "IMG_0001.JPG", textured(3), float(lat), float(lon))
    before = intake_copy.sha256(first)
    run_intake(tmp_path, card2, "--date", "2026-11-20", "--device", "iphone")
    digest = intake_copy.sha256(card2 / "IMG_0001.JPG")
    assert (raw / f"IMG_0001__{digest[:8]}.JPG").exists()
    assert intake_copy.sha256(first) == before
    ok, problems = manifest.verify("raw/", root=archive)
    assert ok == 3 and problems == []


@needs_exiftool
def test_gps_lands_in_campus_frame(tmp_path, monkeypatch):
    archive = tmp_path / "archive"
    monkeypatch.setenv("VC_DATA_ROOT", str(archive))
    card = tmp_path / "card"
    lon, lat, _ = frame.from_campus(150.0, -50.0, 0.0, 4326)
    write_photo(card / "a.jpg", textured(4), float(lat), float(lon), heading=90)
    write_photo(card / "b.jpg", textured(5), when="2026:11:20 10:00:10")          # no GPS
    run_intake(tmp_path, card, "--date", "2026-11-20", "--device", "phone")
    data = json.loads((archive / "derived/capture/2026-11-20/phone/photos.json").read_text())
    rec = {p["name"]: p for p in data["photos"]}
    c = rec["a.jpg"]["campus"]
    assert abs(c["X"] - 150.0) < 0.05 and abs(c["Y"] + 50.0) < 0.05     # exiftool keeps ~1e-7 deg
    assert c["tile"] == "T_01_-01"
    assert abs(c["heading_deg"] - 90.0) < 1.0                            # grid vs true north < 1 deg
    assert rec["a.jpg"]["time"].startswith("2026-11-20T10:00:00")
    assert rec["a.jpg"]["camera"]["model"] == "TestCam"
    assert "campus" not in rec["b.jpg"] and rec["b.jpg"]["area"] is None


def test_exif_time_parsing():
    iso, t = intake_meta.parse_exif_time({"SubSecDateTimeOriginal": "2026:11:20 10:00:00.25-06:00"})
    assert iso == "2026-11-20T10:00:00.250-06:00"
    iso2, t2 = intake_meta.parse_exif_time({"DateTimeOriginal": "2026:11:20 10:00:01"})
    assert iso2 == "2026-11-20T10:00:01.000" and t2 is not None
    iso3, _ = intake_meta.parse_sidecar_time("2020-09-03 22:02:54.000")
    assert iso3 == "2020-09-03T22:02:54.000+00:00"


def test_orientation_is_applied(tmp_path):
    from PIL import Image
    img = Image.fromarray(np.zeros((40, 100, 3), np.uint8))
    exif = Image.Exif()
    exif[0x0112] = 6                                   # rotate 90 degrees clockwise to view
    p = tmp_path / "rot.jpg"
    img.save(p, exif=exif)
    up = intake_images.read_upright(p)
    assert up.shape[:2] == (100, 40)


# ---- area assignment ------------------------------------------------------

def fake_shot_list(path):
    entries = [
        {"id": "F_south", "kind": "facade", "building": "B1", "facade": "south", "priority": "hero",
         "centerline": [[0, 0], [10, 0]], "normal": [0, -1], "min_photos": 4},
        {"id": "F_north", "kind": "facade", "building": "B1", "facade": "north", "priority": "normal",
         "centerline": [[0, 30], [10, 30]], "normal": [0, 1]},
        {"id": "Court", "kind": "courtyard", "centerline": [[100, 0], [120, 0], [120, 20], [100, 20], [100, 0]]},
        {"id": "Swatch", "kind": "swatch", "centerline": [[5, -9.5]]},
    ]
    path.write_text(json.dumps({"version": 1, "entries": entries}))


def test_area_assignment_geometry(tmp_path):
    fake_shot_list(tmp_path / "shot_list.json")
    targets, source = intake_areas.load_targets(tmp_path / "shot_list.json", tmp_path / "none.json")
    assert source == "shot_list" and len(targets) == 4
    a = intake_areas.Assigner(targets)
    assert a.assign(5, -10, 0)["id"] == "F_south"           # looking north at the south wall
    assert a.assign(5, -10, 180) is None                    # looking away: wedge misses it
    assert a.assign(5, -10, 80)["id"] == "F_south"          # wall still within +-75 degrees
    assert a.assign(5, 10, 0) is None                       # behind the south wall, inside the building
    assert a.assign(5, 40, 180)["id"] == "F_north"
    assert a.assign(5, 40, None)["id"] == "F_north"         # no heading: nearest facing target
    assert a.assign(110, 10, None)["id"] == "Court"         # inside a courtyard
    assert a.assign(5, -75, 0) is None                      # more than 60 m away
    hit = a.assign(5, -10, 0)
    assert hit["facade"] == "south" and hit["min_photos"] == 4 and hit["distance_m"] == 10.0


def test_area_fallback_to_footprints(tmp_path):
    fp = {"buildings": [{"id": "B_x", "name": "Test Hall",
                         "polygon": [[[[0, 0], [20, 0], [20, 20], [0, 20], [0, 0]]]]}]}
    (tmp_path / "footprints.json").write_text(json.dumps(fp))
    targets, source = intake_areas.load_targets(tmp_path / "missing.json", tmp_path / "footprints.json")
    assert source == "footprints"
    hit = intake_areas.Assigner(targets).assign(10, -15, 10)
    assert hit["id"] == "B_x" and hit["facade"] is None and hit["name"] == "Test Hall"


def test_fill_area_by_time():
    recs = [{"name": "a", "t": 100.0, "campus": {"X": 0}, "area": {"id": "F"}},
            {"name": "b", "t": 110.0},
            {"name": "c", "t": 500.0}]
    assert intake_areas.fill_by_time(recs) == 1
    assert recs[1]["area"]["id"] == "F" and recs[1]["area"]["by"] == "time"
    assert "area" not in recs[2]


# ---- 360 video -------------------------------------------------------------

def test_view_geometry_points_the_right_way():
    eq = np.zeros((256, 512, 3), np.uint8)
    # A red patch at longitude +90 degrees (3/4 of the width) on the horizon.
    cv2.rectangle(eq, (384 - 8, 128 - 8), (384 + 8, 128 + 8), (0, 0, 255), -1)
    views = dict(((y, p), img) for y, p, img in intake_video.split_frame(eq, [(0, 0), (90, 0), (180, 0)], 90, 64))
    assert tuple(views[(90, 0)][32, 32]) == (0, 0, 255)
    assert tuple(views[(0, 0)][32, 32]) == (0, 0, 0)
    lon, lat = intake_video.ray_angles(64, 0.0, 90.0)
    assert lat[0, 32] > np.radians(40) and lat[63, 32] < -np.radians(40)
    mask = intake_privacy.operator_mask({"pitch": 0.0, "fov": 90.0}, 64)
    assert mask[63].all() and not mask[0].any()            # bottom masked, top kept


def test_frame_spacing():
    assert intake_video.choose_fps(None) == 2.0            # 1 m/s assumed -> 0.5 m per frame
    assert intake_video.choose_fps(1.5) == 3.0
    assert intake_video.choose_fps(100) == intake_video.FPS_MAX


@needs_ffmpeg
@needs_exiftool
def test_360_clip_is_split_into_views(tmp_path, monkeypatch):
    archive = tmp_path / "archive"
    monkeypatch.setenv("VC_DATA_ROOT", str(archive))
    card = tmp_path / "x5"
    card.mkdir()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=size=512x256:rate=10",
                    "-t", "3", "-pix_fmt", "yuv420p", str(card / "walk.mp4")], check=True)
    info = intake_video.probe(card / "walk.mp4")
    assert intake_video.is_equirect(info) and abs(info["duration"] - 3.0) < 0.2
    run_intake(tmp_path, card, "--date", "2026-11-20", "--device", "x5", "--views", "4")
    out = archive / "derived/capture/2026-11-20/x5"
    data = json.loads((out / "photos.json").read_text())
    views = [p for p in data["photos"] if p["kind"] == "360_view"]
    assert len(views) == 6 * 4                              # 3 s at 2 fps, 4 views each
    v = views[0]["view"]
    assert v["fov"] == 90.0 and v["pitch"] == 0.0 and "frame_time_s" in v
    vj = next((out / "views").rglob("views.json"))
    meta = json.loads(vj.read_text())
    assert meta["params"]["fps"] == 2.0 and len(meta["views"]) == 24
    first = archive / views[0]["input"]
    assert cv2.imread(str(first)).shape[:2] == (128, 128)   # 512 * 90 / 360
    # A second run reuses the views.
    run_intake(tmp_path, card, "--date", "2026-11-20", "--device", "x5", "--views", "4")
    rep = json.loads((out / "intake_report.json").read_text())
    assert rep["captured"]["reused_from_last_run"] == 24


# ---- quality flags --------------------------------------------------------

def _item(name, img, t=None, campus=None, sha=None):
    return {"name": name, "quality": intake_quality.measure(img), "t": t, "campus": campus,
            "size": 200_000, "sha256": sha or name}


def test_quality_flags():
    sharp = textured(7)
    blurry = cv2.GaussianBlur(sharp, (0, 0), 12)
    white = np.full_like(sharp, 255)
    dark = np.full_like(sharp, 3)
    items = [_item(f"s{i}", textured(10 + i), t=100.0 + 10 * i) for i in range(4)]
    items += [_item("sharp", sharp, t=1000.0), _item("sharp_again", sharp.copy(), t=1000.5),
              _item("later_same_view", sharp.copy(), t=2000.0),
              _item("blurry", blurry, t=3000.0), _item("white", white, t=4000.0), _item("dark", dark, t=5000.0)]
    tiny = _item("tiny", textured(20, (200, 300)), t=6000.0)
    tiny["size"] = 10_000
    items.append(tiny)
    intake_quality.flag_set(items)
    f = {r["name"]: r["quality"]["flags"] for r in items}
    assert f["sharp"] == [] and f["s0"] == []
    assert "duplicate" in f["sharp_again"]                   # same picture 0.5 s later
    assert "duplicate" not in f["later_same_view"]           # same picture, but much later, no GPS
    assert "blurry" in f["blurry"]
    assert "overexposed" in f["white"]
    assert "underexposed" in f["dark"]
    assert "tiny" in f["tiny"]


def test_duplicate_by_baseline():
    img = textured(8)
    a = _item("a", img, t=0.0, campus={"X": 0.0, "Y": 0.0})
    b = _item("b", img.copy(), t=60.0, campus={"X": 0.05, "Y": 0.05})
    c = _item("c", img.copy(), t=120.0, campus={"X": 5.0, "Y": 0.0})
    intake_quality.flag_set([a, b, c])
    assert b["quality"]["flags"] == ["duplicate"] and b["quality"]["duplicate_of"] == "a"
    assert c["quality"]["flags"] == []


# ---- blur and mask rules ---------------------------------------------------

def test_blur_regions_and_mask():
    dets = {"faces": [(0.9, [100, 100, 20, 24])],
            "objects": [("person", 0.8, [300, 50, 40, 120]), ("car", 0.7, [20, 300, 200, 100])],
            "plates": [(0.7, [80, 370, 40, 12])]}
    regions = intake_privacy.blur_regions(dets, 640, 480)
    reasons = sorted(r for r, _ in regions)
    assert reasons == ["face", "head", "plate", "plate_band"]
    head = dict(regions)["head"]
    assert head[1] <= 50 and head[1] + head[3] <= 50 + 0.3 * 120     # top of the person only
    band = dict(regions)["plate_band"]
    assert band[1] > 300 and band[1] + band[3] >= 399                # lower part of the car
    mask = intake_privacy.build_mask(dets, regions, 640, 480)
    assert mask[110, 110] == 0 and mask[100, 320] == 0 and mask[350, 100] == 0
    assert mask[5, 600] == 255
    img = np.random.default_rng(9).integers(0, 256, (480, 640, 3), dtype=np.uint8)   # fine detail
    before = img[100:124, 100:120].astype(float).var()
    intake_privacy.pixelate_blur(img, [100, 100, 20, 24])
    assert img[100:124, 100:120].astype(float).var() < before * 0.5


# ---- coverage status merge -------------------------------------------------

def test_coverage_merge(tmp_path):
    path = tmp_path / "coverage_status.json"
    info = {"kind": "shot_list", "building": "B1", "facade": "south", "min_photos": 12}
    intake_coverage.update(path, "2026-12-03/iphone",
                           {"F1": {"photos": 10, "usable": 8, "flagged": 2, "last_capture": "2026-12-03",
                                   "info": info}},
                           all_targets=[{"id": "F1", **info}, {"id": "F2", "kind": "shot_list"}])
    d = json.loads(path.read_text())
    assert d["entries"]["F1"]["status"] == "needs_reshoot"           # only 8 usable of 12
    assert d["entries"]["F2"]["status"] == "not_started"
    intake_coverage.update(path, "2026-12-05/iphone",
                           {"F1": {"photos": 6, "usable": 6, "flagged": 0, "last_capture": "2026-12-05",
                                   "info": info}})
    d = json.loads(path.read_text())
    e = d["entries"]["F1"]
    assert e["status"] == "captured" and e["usable_count"] == 14 and e["photo_count"] == 16
    assert set(e["sets"]) == {"2026-12-03/iphone", "2026-12-05/iphone"}
    assert e["last_capture"] == "2026-12-05"
    # Re-running one set replaces only its own numbers.
    intake_coverage.update(path, "2026-12-03/iphone",
                           {"F1": {"photos": 10, "usable": 2, "flagged": 8, "last_capture": "2026-12-03",
                                   "info": info}})
    e = json.loads(path.read_text())["entries"]["F1"]
    assert e["photo_count"] == 16 and e["flagged_count"] == 8
    assert e["status"] == "needs_reshoot"                                 # 8 of 16 flagged > 40%
    # > 40% flagged -> needs_reshoot
    assert intake_coverage.status_for(10, 5, 5, 3)[0] == "needs_reshoot"
    # "aligned" (written by the alignment stage) is kept.
    d = json.loads(path.read_text())
    d["entries"]["F1"]["status"] = "aligned"
    path.write_text(json.dumps(d))
    intake_coverage.update(path, "2026-12-07/x5", {})
    assert json.loads(path.read_text())["entries"]["F1"]["status"] == "aligned"
    assert "F2" in json.loads(path.read_text())["entries"]                # other entries kept


@needs_exiftool
def test_blurred_copy_keeps_gps_but_not_thumbnail(tmp_path):
    src, dst, thumb = tmp_path / "orig.jpg", tmp_path / "blurred.jpg", tmp_path / "thumb.jpg"
    write_photo(src, textured(11), 36.1475, -86.8040, heading=45)
    intake_images.write_jpeg(thumb, cv2.resize(textured(11), (160, 120)), 80)
    subprocess.run([EXIFTOOL, "-q", "-overwrite_original", f"-ThumbnailImage<={thumb}", "-Orientation#=6",
                    str(src)], check=True, capture_output=True)
    intake_images.write_jpeg(dst, textured(12), 95)
    assert intake_privacy.copy_exif([(src, dst)], EXIFTOOL) == 0
    tags = json.loads(subprocess.run([EXIFTOOL, "-json", "-n", "-ThumbnailImage", "-Orientation", "-GPSLatitude",
                                      "-Model", str(dst)], capture_output=True, check=True).stdout)[0]
    assert abs(tags["GPSLatitude"] - 36.1475) < 1e-6 and tags["Model"] == "TestCam"
    assert tags["Orientation"] == 1 and "ThumbnailImage" not in tags
