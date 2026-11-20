"""Fetch public, permissively licensed stand-in photos for the capture stages.

Evan's own phone capture does not exist yet. Until it does, Stage 2 (intake) and
Stage 3 (alignment, measurement) are tested on public photos of the campus.

Steps (run one with its name, or "all"):
  commons    Wikimedia Commons photos of Vanderbilt buildings (category tree plus
             every geotagged file inside the campus box). Exteriors only, licenses
             PD / CC0 / CC BY / CC BY-SA. Old photos (before 1990) go to a separate
             historic/ list; at most 30 of them are downloaded.
             -> raw/capture/commons-<date>/wikimedia/<category>/<file>
                plus licenses.json (one per category folder and one for the set).
  kartaview  KartaView street-level photos (CC BY-SA 4.0) inside the campus box,
             through the public API (no account). Faces and plates are blurred by
             KartaView ("proc" images).
             -> raw/capture/kartaview-<date>/<sequence>/<photo>.jpg + sequence.json
  video360   One outdoor 360 video (equirectangular) from Commons, kept whole in
             raw/, plus a short stream-copied cut in derived/ (needs ffmpeg).
             -> raw/capture/commons-<date>/video360/ and derived/capture/commons-<date>/video360/
  fallback   A small street-facade photogrammetry set (Zenodo 16576623, Paganico,
             Italy, 65 JPGs, CC BY 4.0) for testing alignment while Commons has too
             few overlapping Kirkland photos. Zip kept in raw/, images unpacked to
             derived/capture/fallback-<date>/.
  assess     Quick CPU check of the Kirkland Hall photos: EXIF table, facade guess,
             SIFT + exhaustive matching (pycolmap) on copies scaled to <= 1600 px,
             largest connected group with >= 30 inlier matches. No reconstruction.
             -> derived/capture/commons-<date>/assessment.json

Not used (checked, see ATTRIBUTION.md): Mapillary (needs an access token) and
Flickr (its API needs a key; the keyless feed has no license field).
No Google content. Every downloaded file is recorded in the manifest with its
source URL and license. Running again skips files already present.
"""
import argparse
import datetime
import html
import json
import math
import re
import shutil
import sqlite3
import subprocess
import sys
import time
from pathlib import Path

import requests

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import data_root, fail, load_json, save_json  # noqa: E402
import manifest  # noqa: E402
import netfetch  # noqa: E402

STAGE = 2
COMMONS_API = "https://commons.wikimedia.org/w/api.php"
KARTA_API = "https://api.kartaview.org/2.0"

# Campus box (south, west, north, east) and Kirkland Hall (OSM footprint centre).
BBOX = (36.1346, -86.8172, 36.1529, -86.7917)
KIRKLAND = (36.1486, -86.8027)

# Commons categories walked, in priority order: (category, depth of subcategories).
CATEGORIES = [
    ("Kirkland Hall", 1),
    ("Vanderbilt University structures", 2),
    ("Vanderbilt University", 0),
    ("Peabody College", 0),
    ("Peabody Library", 0),
    ("Blair School of Music", 0),
    ("Owen Graduate School of Management", 0),
    ("Vanderbilt University Law School", 0),
    ("Vanderbilt University School of Medicine", 0),
    ("Vanderbilt University Medical Center", 1),
    ("Statue of Vanderbilt (Moretti), Vanderbilt University", 0),
    ("Images from Vanderbilt University Special Collections", 0),
]
# Subcategories never entered (people, sport teams, logos, art collections, off campus).
SKIP_CATEGORY = re.compile(r"people|alumni|faculty|players|coaches|athlet|logos?\b|bands|uniforms|"
                           r"group photographs|art in the christian|fine arts gallery|police|rotc|"
                           r"centennial sportsplex|by year|games|tennis|swimming|soccer", re.I)
# Words that mark a file as not a building exterior.
NOT_EXTERIOR = re.compile(r"interior|inside|lobby|hallway|corridor|classroom|auditorium|\broom\b|"
                          r"chamber|stained glass|organ\b|portrait|headshot|logo|seal\b|wordmark|"
                          r"diploma|\bmap\b|floor plan|painting|drawing|yearbook|coin|ticket|"
                          r"signature|book cover|program\b|team photo|basketball game|concert|"
                          r"exhibit|gallery|altar|ceiling|staircase interior|crowd", re.I)
LICENSE_OK = re.compile(r"^(cc0|pd|public domain|cc[- ]by(-sa)?[- ]\d|cc[- ]by(-sa)?$)", re.I)
LICENSE_BAD = re.compile(r"\bnc\b|\bnd\b|non-?commercial|no-?deriv|fair use|all rights", re.I)
PHOTO_MIME = {"image/jpeg", "image/png", "image/tiff", "image/webp"}
HISTORIC_YEAR = 1990
HISTORIC_MAX = 30

VIDEO360_TITLE = "File:3-mins-in-360 on The Old Ghost Road.webm"
VIDEO360_CUT_S = 45

# Fallback street-facade photogrammetry set: Zenodo record 16576623, "Photogrammetry
# Photo Dataset of Historic Architecture in Paganico, Italy", 65 JPGs, CC BY 4.0.
ZENODO_API = "https://zenodo.org/api"
FALLBACK_RECORD = 16576623


def today():
    return datetime.date.today().isoformat()


def plain(text):
    """Commons metadata is HTML; keep the readable text."""
    return html.unescape(re.sub(r"<[^>]+>", "", text or "")).strip()


def safe_name(text):
    return re.sub(r"[^A-Za-z0-9._()\-]+", "_", text).strip("_")[:120]


def distance_m(a, b):
    """Rough ground distance in metres between two (lat, lon) points."""
    dy = (a[0] - b[0]) * 111320.0
    dx = (a[1] - b[1]) * 111320.0 * math.cos(math.radians(a[0]))
    return math.hypot(dx, dy)


def bearing_deg(frm, to):
    dy = to[0] - frm[0]
    dx = (to[1] - frm[1]) * math.cos(math.radians(frm[0]))
    return (math.degrees(math.atan2(dx, dy)) + 360) % 360


def in_box(lat, lon, margin=0.0):
    s, w, n, e = BBOX
    return s - margin <= lat <= n + margin and w - margin <= lon <= e + margin


class Api:
    """Paced JSON GETs. Commons throttles fast clients, so wait between calls."""

    def __init__(self, url, pause):
        self.url, self.pause, self.last = url, pause, 0.0

    def get(self, path="", **params):
        wait = self.pause - (time.time() - self.last)
        if wait > 0:
            time.sleep(wait)
        r = netfetch.get(self.url + path, params=params, timeout=120)
        self.last = time.time()
        try:
            return r.json()
        except ValueError:
            fail(f"{self.url}{path}: answer was not JSON")


# ---------------------------------------------------------------- Commons

def commons_members(api, category):
    files, subcats, cont = [], [], {}
    while True:
        r = api.get(action="query", format="json", list="categorymembers", cmtitle="Category:" + category,
                    cmtype="subcat|file", cmlimit=500, **cont)
        for m in r.get("query", {}).get("categorymembers", []):
            (files if m["ns"] == 6 else subcats).append(m["title"])
        if "continue" not in r:
            return files, subcats
        cont = r["continue"]


def commons_candidates(api):
    """Ordered {file title: folder name} from the category tree, then geotagged files."""
    found, seen = {}, set()

    def walk(cat, depth, folder):
        if cat in seen:
            return
        seen.add(cat)
        files, subcats = commons_members(api, cat)
        print(f"  Category:{cat}: {len(files)} file(s), {len(subcats)} subcategories")
        for f in files:
            found.setdefault(f, folder)
        if depth > 0:
            for sc in subcats:
                name = sc.split(":", 1)[1]
                if not SKIP_CATEGORY.search(name):
                    walk(name, depth - 1, safe_name(name))

    for cat, depth in CATEGORIES:
        walk(cat, depth, safe_name(cat))
    # Geotagged files inside the campus box (any category).
    centre = ((BBOX[0] + BBOX[2]) / 2, (BBOX[1] + BBOX[3]) / 2)
    r = api.get(action="query", format="json", list="geosearch", gscoord=f"{centre[0]}|{centre[1]}",
                gsradius=1600, gslimit=500, gsnamespace=6)
    geo = [g for g in r.get("query", {}).get("geosearch", []) if in_box(g["lat"], g["lon"])]
    print(f"  geosearch: {len(geo)} geotagged file(s) inside the campus box")
    for g in geo:
        found.setdefault(g["title"], "geotagged_campus")
    return found


def commons_info(api, titles):
    """imageinfo for up to 50 titles per call."""
    out = {}
    for i in range(0, len(titles), 25):
        chunk = titles[i:i + 25]
        r = api.get(action="query", format="json", titles="|".join(chunk), prop="imageinfo|categories",
                    iiprop="url|size|mime|sha1|extmetadata|commonmetadata|metadata", cllimit=500,
                    iimetadataversion="latest")
        for p in r.get("query", {}).get("pages", {}).values():
            if "imageinfo" in p:
                out[p["title"]] = p
    return out


def meta_value(em, key):
    return plain(em.get(key, {}).get("value", ""))


def exif_dict(ii):
    d = {}
    for item in (ii.get("commonmetadata") or []) + (ii.get("metadata") or []):
        if isinstance(item.get("value"), (str, int, float)):
            d.setdefault(item["name"], item["value"])
    return d


def describe(title, page):
    """Turn one Commons page into a license record and a keep/skip decision."""
    ii = page["imageinfo"][0]
    em = ii.get("extmetadata", {})
    exif = exif_dict(ii)
    cats = [c["title"].split(":", 1)[1] for c in page.get("categories", [])]
    lic_short = meta_value(em, "LicenseShortName")
    lic_code = meta_value(em, "License")
    date = meta_value(em, "DateTimeOriginal") or meta_value(em, "DateTime")
    year = None
    m = re.search(r"(1[89]\d\d|20\d\d)", date)
    if m:
        year = int(m.group(1))
    lat = em.get("GPSLatitude", {}).get("value")
    lon = em.get("GPSLongitude", {}).get("value")
    gps = None
    try:
        if lat and lon:
            gps = [float(lat), float(lon)]
    except ValueError:
        gps = None
    rec = {
        "file": title.split(":", 1)[1],
        "page_url": ii.get("descriptionurl"),
        "file_url": ii.get("url"),
        "author": meta_value(em, "Artist") or meta_value(em, "Credit"),
        "credit": meta_value(em, "Credit"),
        "license": lic_short,
        "license_code": lic_code,
        "license_url": meta_value(em, "LicenseUrl"),
        "attribution_required": meta_value(em, "AttributionRequired"),
        "date_taken": date,
        "gps": gps,
        "gps_heading": exif.get("GPSImgDirection"),
        "width": ii.get("width"), "height": ii.get("height"), "size": ii.get("size"),
        "mime": ii.get("mime"), "sha1": ii.get("sha1"),
        "description": meta_value(em, "ImageDescription")[:400],
        "categories": cats,
        "camera": " ".join(str(exif.get(k, "")) for k in ("Make", "Model")).strip(),
        "focal_length": exif.get("FocalLength"),
        "focal_length_35mm": exif.get("FocalLengthIn35mmFilm"),
    }
    text = " ".join([rec["file"], rec["description"], " ".join(cats)])
    lic = lic_short or lic_code
    if ii.get("mime") not in PHOTO_MIME:
        return rec, "skip", f"not a photo ({ii.get('mime')})"
    if not lic or LICENSE_BAD.search(lic) or not (LICENSE_OK.search(lic_short) or LICENSE_OK.search(lic_code)):
        return rec, "skip", f"license not accepted ({lic!r})"
    if meta_value(em, "NonFree").lower() == "true":
        return rec, "skip", "marked non-free"
    if NOT_EXTERIOR.search(text):
        return rec, "skip", "looks like an interior, portrait, logo or artwork"
    if gps and not in_box(gps[0], gps[1], margin=0.002):
        return rec, "skip", "GPS outside the campus box"
    if (ii.get("width") or 0) < 640:
        return rec, "skip", "smaller than 640 px"
    if (year and year < HISTORIC_YEAR) or "Special Collections" in text or "circa 18" in text.lower():
        return rec, "historic", "taken before 1990"
    return rec, "keep", ""


def sha1_check(expected):
    def check(path):
        import hashlib
        h = hashlib.sha1()
        with open(path, "rb") as f:
            for chunk in iter(lambda: f.read(1 << 20), b""):
                h.update(chunk)
        return None if not expected or h.hexdigest() == expected else "sha1 differs from Commons"
    return check


def write_licenses(path, records, db, root, source):
    """licenses.json in raw/ is write-once; a changed list goes to derived/ instead."""
    content = (json.dumps(records, indent=2, ensure_ascii=False) + "\n").encode("utf-8")
    if path.exists():
        if path.read_bytes() == content:
            return
        alt = root / "derived" / path.relative_to(root / "raw").parent / f"licenses-{today()}-update.json"
        alt.parent.mkdir(parents=True, exist_ok=True)
        alt.write_bytes(content)
        manifest.add_file(alt, source=source, license="metadata", stage=STAGE, date=today(), root=root, db=db)
        db.commit()
        print(f"  {path.name} already written; newer list saved to {alt}")
        return
    netfetch.save_bytes_once(content, path, source, "metadata (per-file licenses inside)", db, root, STAGE)


def run_commons(args, root, db):
    api = Api(COMMONS_API, args.pause)
    base = root / "raw" / "capture" / f"commons-{args.date}" / "wikimedia"
    # The API answers are cached in derived/ so a resumed run does not ask again.
    cache = root / "derived" / "capture" / f"commons-{args.date}" / "api_cache.json"
    if cache.exists():
        found, info = load_json(cache)["found"], load_json(cache)["info"]
        titles = list(found)
        print(f"Commons: {len(titles)} candidate file(s) from {cache}")
    else:
        print("Commons: listing categories")
        found = commons_candidates(api)
        titles = list(found)
        print(f"Commons: {len(titles)} candidate file(s); reading metadata")
        info = commons_info(api, titles)
        save_json(cache, {"found": found, "info": info})
    keep, historic, skipped = [], [], []
    for t in titles:
        if t not in info:
            skipped.append({"file": t, "reason": "no imageinfo"})
            continue
        rec, verdict, why = describe(t, info[t])
        rec["folder"] = found[t]
        if verdict == "keep":
            keep.append(rec)
        elif verdict == "historic":
            rec["folder"] = "historic"
            historic.append(rec)
        else:
            skipped.append({"file": rec["file"], "page_url": rec["page_url"], "license": rec["license"],
                            "reason": why})
    print(f"Commons: keep {len(keep)}, historic {len(historic)}, skipped {len(skipped)}")

    # Download order: Kirkland Hall first (its category, then photos taken within
    # 250 m of it), then everything else in category order.
    def priority(rec):
        if rec["folder"].startswith("Kirkland"):
            return 0
        if rec.get("gps") and distance_m(rec["gps"], KIRKLAND) < 250:
            return 1
        return 2
    keep.sort(key=priority)
    total, done, failures = 0, [], []
    progress = root / "derived" / "capture" / f"commons-{args.date}" / "licenses-progress.json"

    def summary():
        return {"date": args.date, "source": "Wikimedia Commons", "api": COMMONS_API,
                "downloaded": done, "historic_not_downloaded": historic[HISTORIC_MAX:],
                "skipped": skipped, "failed": failures}
    for rec in keep + historic[:HISTORIC_MAX]:
        if len(done) >= args.max_files or total + (rec["size"] or 0) > args.max_bytes:
            print("  cap reached; stopping downloads")
            break
        dest = base / rec["folder"] / safe_name(rec["file"])
        lic = f"{rec['license']} | author: {rec['author'][:120]} | {rec['page_url']}"
        try:
            _, new = netfetch.download_once(rec["file_url"].split("?")[0], dest, lic, db, root, STAGE,
                                            check=sha1_check(rec["sha1"]))
        except SystemExit:
            failures.append(rec["file"])
            continue
        rec["local_path"] = manifest.rel(dest, root)
        total += rec["size"] or 0
        done.append(rec)
        if new:
            print(f"  [{len(done)}] {rec['folder']}/{dest.name}")
            time.sleep(args.file_pause)          # Commons throttles unidentified clients hard
            if len(done) % 10 == 0:
                save_json(progress, summary())
    save_json(progress, summary())
    if failures:
        # licenses.json in raw/ is written once, so only for a complete run.
        print(f"Commons: {len(failures)} download(s) failed; partial list in {progress}; run again")
        return False
    # licenses.json beside the files (per folder) and one for the whole set.
    for folder in sorted({r["folder"] for r in done}):
        write_licenses(base / folder / "licenses.json", [r for r in done if r["folder"] == folder], db, root,
                       COMMONS_API)
    write_licenses(base.parent / "licenses.json", summary(), db, root, COMMONS_API)
    print(f"Commons: {len(done)} file(s), {total / 1e6:.0f} MB")
    return not failures


# ---------------------------------------------------------------- KartaView

def karta_sequences(api):
    """Find sequences with photos in the box: the nearby search returns one photo per sequence."""
    s, w, n, e = BBOX
    step = 0.6  # km between search centres (each search has a 500 m radius)
    dlat = step / 111.32
    dlon = step / (111.32 * math.cos(math.radians((s + n) / 2)))
    seqs = set()
    lat = s
    while lat < n + dlat / 2:
        lon = w
        while lon < e + dlon / 2:
            r = api.get("/photo/", lat=round(lat, 5), lng=round(lon, 5), radius=500, itemsPerPage=150)
            for p in (r.get("result") or {}).get("data", []):
                if in_box(float(p["lat"]), float(p["lng"])):
                    seqs.add(p["sequenceId"])
            lon += dlon
        lat += dlat
    return sorted(seqs)


def karta_photos(api, seq):
    photos, page = [], 1
    while True:
        r = api.get(f"/sequence/{seq}/photos", page=page, itemsPerPage=100)
        res = r.get("result") or {}
        photos += res.get("data", [])
        if not res.get("hasMoreData"):
            return photos
        page += 1


def run_kartaview(args, root, db):
    api = Api(KARTA_API, 1.0)
    base = root / "raw" / "capture" / f"kartaview-{args.date}"
    print("KartaView: searching the campus box")
    seq_ids = karta_sequences(api)
    print(f"KartaView: {len(seq_ids)} sequence(s) with photos in the box")
    plans = []
    for seq in seq_ids:
        info = (api.get(f"/sequence/{seq}").get("result") or {}).get("data") or {}
        photos = [p for p in karta_photos(api, seq) if in_box(float(p["lat"]), float(p["lng"]))]
        if not photos:
            continue
        closest = min(distance_m((float(p["lat"]), float(p["lng"])), KIRKLAND) for p in photos)
        plans.append((closest, seq, info, photos))
    plans.sort(key=lambda x: x[0])
    count, failures, missing, report = 0, [], [], []
    for closest, seq, info, photos in plans:
        lic = (f"CC BY-SA 4.0 | KartaView contributor (user id {info.get('userId')}) | "
               f"https://kartaview.org/details/{seq}")
        folder = base / str(seq)
        keepers = []
        for p in sorted(photos, key=lambda p: int(p["sequenceIndex"])):
            if count >= args.max_karta:
                break
            url = p.get("fileurlProc") or p["fileurl"].replace("{{sizeprefix}}", "proc")
            dest = folder / f"{int(p['sequenceIndex']):05d}_{p['name']}"
            try:
                netfetch.download_once(url, dest, lic, db, root, STAGE)
            except SystemExit:
                # A photo KartaView no longer stores (HTTP 404) is noted, not retried.
                try:
                    gone = requests.head(url, headers=netfetch.HEADERS, timeout=60).status_code == 404
                except requests.RequestException:
                    gone = False
                (missing if gone else failures).append(url)
                continue
            count += 1
            keepers.append({k: p.get(k) for k in ("id", "sequenceIndex", "name", "lat", "lng", "heading",
                                                    "shotDate", "width", "height", "projection",
                                                    "fieldOfView", "cameraParameters",
                                                    "autoImgProcessingResult", "fileurlProc")}
                           | {"local_file": dest.name})
        if keepers:
            meta = {"sequence": {k: v for k, v in info.items() if not isinstance(v, (dict, list))},
                    "camera": info.get("cameraParameters"), "license": "CC BY-SA 4.0",
                    "license_url": "https://creativecommons.org/licenses/by-sa/4.0/",
                    "credit": f"KartaView contributor (user id {info.get('userId')})",
                    "source": f"{KARTA_API}/sequence/{seq}/photos", "photos": keepers}
            write_licenses(folder / "sequence.json", meta, db, root, f"{KARTA_API}/sequence/{seq}")
        report.append({"sequence": seq, "photos_in_box": len(photos), "downloaded": len(keepers),
                       "closest_to_kirkland_m": round(closest, 1),
                       "shot_dates": sorted({p["shotDate"][:10] for p in photos}),
                       "sizes": sorted({f"{p['width']}x{p['height']}" for p in photos}),
                       "blurred": sorted({p.get("autoImgProcessingResult") or "?" for p in photos}),
                       "projection": sorted({p.get("projection") or "?" for p in photos})})
    out = root / "derived" / "capture" / f"kartaview-{args.date}" / "summary.json"
    save_json(out, {"date": args.date, "bbox": BBOX, "kirkland": KIRKLAND, "images": count,
                    "sequences": report, "failed": failures,
                    "missing_at_source": missing})
    manifest.add_file(out, source="fetch_samples.py kartaview", license="metadata", stage=STAGE,
                      date=today(), root=root, db=db)
    db.commit()
    print(f"KartaView: {count} image(s) from {len(report)} sequence(s); summary {out}")
    return not failures


# ---------------------------------------------------------------- 360 video

def run_video360(args, root, db):
    api = Api(COMMONS_API, args.pause)
    r = api.get(action="query", format="json", titles=VIDEO360_TITLE, prop="videoinfo",
                viprop="url|size|mime|sha1|extmetadata")
    page = next(iter(r["query"]["pages"].values()))
    if "videoinfo" not in page:
        fail(f"{VIDEO360_TITLE} not found on Commons")
    vi = page["videoinfo"][0]
    em = vi["extmetadata"]
    lic = meta_value(em, "LicenseShortName")
    if not LICENSE_OK.search(lic) or LICENSE_BAD.search(lic):
        fail(f"{VIDEO360_TITLE}: license {lic!r} not accepted")
    rec = {"file": VIDEO360_TITLE.split(":", 1)[1], "page_url": vi["descriptionurl"], "file_url": vi["url"],
           "author": meta_value(em, "Artist"), "license": lic, "license_url": meta_value(em, "LicenseUrl"),
           "width": vi["width"], "height": vi["height"], "duration_s": vi.get("duration"), "size": vi["size"],
           "description": meta_value(em, "ImageDescription")[:400], "projection": "equirectangular"}
    raw_dir = root / "raw" / "capture" / f"commons-{args.date}" / "video360"
    dest = raw_dir / safe_name(rec["file"])
    netfetch.download_once(vi["url"], dest, f"{lic} | author: {rec['author'][:120]} | {rec['page_url']}",
                           db, root, STAGE, check=sha1_check(vi.get("sha1")))
    write_licenses(raw_dir / "licenses.json", [rec], db, root, COMMONS_API)
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        fail("ffmpeg not found; the raw video is saved but the cut was not made")
    cut = root / "derived" / "capture" / f"commons-{args.date}" / "video360" / (dest.stem + f"_first{VIDEO360_CUT_S}s.webm")
    if not cut.exists():
        cut.parent.mkdir(parents=True, exist_ok=True)
        res = subprocess.run([ffmpeg, "-y", "-v", "error", "-i", str(dest), "-t", str(VIDEO360_CUT_S),
                              "-c", "copy", str(cut)], capture_output=True, text=True)
        if res.returncode != 0:
            fail("ffmpeg cut failed: " + res.stderr[-400:])
        manifest.add_file(cut, source=f"ffmpeg stream copy of {manifest.rel(dest, root)}",
                          license=f"{lic} | author: {rec['author'][:120]}", stage=STAGE, date=today(),
                          root=root, db=db)
        db.commit()
    print(f"360 video: {dest} ({vi['size'] / 1e6:.0f} MB), cut {cut}")
    return True


# ---------------------------------------------------------------- fallback set

def run_fallback(args, root, db):
    """A small street-facade photogrammetry set (CC BY 4.0) for when Commons is too thin."""
    import zipfile
    api = Api(ZENODO_API, 1.0)
    rec = api.get(f"/records/{FALLBACK_RECORD}")
    lic = (rec["metadata"].get("license") or {}).get("id", "")
    if lic not in ("cc-by-4.0", "cc-zero", "cc-by-sa-4.0"):
        fail(f"Zenodo {FALLBACK_RECORD}: license {lic!r} not accepted")
    creators = ", ".join(c["name"] for c in rec["metadata"]["creators"])
    page = rec["links"]["self_html"]
    raw_dir = root / "raw" / "capture" / f"fallback-{args.date}" / f"zenodo-{FALLBACK_RECORD}"
    note = f"{lic.upper()} | author: {creators} | {page}"
    write_licenses(raw_dir / "record.json", {"license": lic, "license_url": "https://creativecommons.org/licenses/by/4.0/",
                                             "creators": creators, "page_url": page, "title": rec["metadata"]["title"],
                                             "description": plain(rec["metadata"]["description"]),
                                             "files": rec["files"]}, db, root, page)
    out_dir = root / "derived" / "capture" / f"fallback-{args.date}" / f"zenodo-{FALLBACK_RECORD}"
    for f in rec["files"]:
        dest = raw_dir / f["key"]
        md5 = f["checksum"].split(":", 1)[1]

        def check(path, md5=md5):
            import hashlib
            h = hashlib.md5()
            with open(path, "rb") as fh:
                for chunk in iter(lambda: fh.read(1 << 20), b""):
                    h.update(chunk)
            return None if h.hexdigest() == md5 else "md5 differs from Zenodo"
        netfetch.download_once(f["links"]["self"], dest, note, db, root, STAGE, check=check)
        if dest.suffix.lower() != ".zip":
            continue
        # Unpack only images, flat, into derived/ (never run anything from the archive).
        with zipfile.ZipFile(dest) as z:
            for info in z.infolist():
                name = Path(info.filename).name
                if info.is_dir() or name.startswith("._") or Path(name).suffix.lower() not in (".jpg", ".jpeg", ".png", ".tif", ".tiff"):
                    continue
                target = out_dir / "images" / safe_name(name)
                if target.exists():
                    continue
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(z.read(info))
                manifest.add_file(target, source=f"unzipped from {manifest.rel(dest, root)}", license=note,
                                  stage=STAGE, date=today(), root=root, db=db)
        db.commit()
    n = len(list((out_dir / "images").glob("*"))) if (out_dir / "images").exists() else 0
    print(f"fallback: Zenodo {FALLBACK_RECORD} ({lic}), {n} image(s) in {out_dir / 'images'}")
    return True


# ---------------------------------------------------------------- assessment

def facade_guess(rec):
    """Which side of Kirkland Hall a photo looks at, from GPS (camera position) or words."""
    if rec.get("gps"):
        d = distance_m(rec["gps"], KIRKLAND)
        if 15 < d < 600:
            b = bearing_deg(KIRKLAND, rec["gps"])   # building -> camera
            side = "NESW"[int(((b + 45) % 360) // 90)]
            return f"{side} (GPS, {d:.0f} m away, bearing {b:.0f})"
        return f"unknown (GPS {d:.0f} m from centre)"
    text = (rec.get("file", "") + " " + rec.get("description", "")).lower()
    for word, side in (("north", "N"), ("south", "S"), ("east", "E"), ("west", "W"), ("front", "E?"),
                       ("tower", "tower"), ("steps", "E? (steps)")):
        if word in text:
            return f"{side} (from text '{word}')"
    return "unknown"


def exif_from_file(path):
    from PIL import Image, ExifTags
    out = {}
    try:
        with Image.open(path) as im:
            ex = im.getexif()
            tags = {ExifTags.TAGS.get(k, k): v for k, v in ex.items()}
            tags.update({ExifTags.TAGS.get(k, k): v for k, v in ex.get_ifd(0x8769).items()})
            for k in ("Make", "Model", "DateTimeOriginal", "FocalLength", "FocalLengthIn35mmFilm"):
                if k in tags:
                    v = tags[k]
                    out[k] = float(v) if k.startswith("Focal") else str(v).strip("\x00 ")
    except Exception as e:  # unreadable EXIF is not fatal
        out["error"] = str(e)
    return out


def run_assess(args, root, db):
    from PIL import Image
    # Kirkland Hall candidates come from the cached Commons metadata, so photos that
    # could not be downloaded yet still appear in the table (downloaded: false).
    out_dir = root / "derived" / "capture" / f"commons-{args.date}"
    cache = out_dir / "api_cache.json"
    if not cache.exists():
        fail(f"{cache} missing; run the commons step first")
    c = load_json(cache)
    base = root / "raw" / "capture" / f"commons-{args.date}" / "wikimedia"
    kirk = []
    for t, folder in c["found"].items():
        if t not in c["info"]:
            continue
        r, verdict, _ = describe(t, c["info"][t])
        near = r.get("gps") and distance_m(r["gps"], KIRKLAND) < 120
        if verdict == "keep" and ("Kirkland" in r["file"] or folder.startswith("Kirkland") or near):
            dest = base / folder / safe_name(r["file"])
            r["local_path"] = manifest.rel(dest, root) if dest.exists() else None
            kirk.append(r)
    small = out_dir / "kirkland_1600"
    small.mkdir(parents=True, exist_ok=True)
    table = []
    for r in kirk:
        row = {"file": r["file"], "downloaded": bool(r["local_path"]), "small": None,
               "resolution": f"{r['width']}x{r['height']}", "date": r.get("date_taken"),
               "camera": r.get("camera"), "focal_mm": r.get("focal_length"),
               "focal_35mm": r.get("focal_length_35mm"), "facade_guess": facade_guess(r),
               "license": r["license"], "author": r["author"][:80], "page_url": r["page_url"]}
        if r["local_path"]:
            src = root / r["local_path"]
            ex = exif_from_file(src)
            row["small"] = Path(r["local_path"]).stem + ".jpg"
            with Image.open(src) as im:
                im = im.convert("RGB")
                im.thumbnail((1600, 1600))
                im.save(small / row["small"], quality=92)
            row["camera"] = row["camera"] or f"{ex.get('Make', '')} {ex.get('Model', '')}".strip()
            row["focal_mm"] = row["focal_mm"] or ex.get("FocalLength")
            row["focal_35mm"] = row["focal_35mm"] or ex.get("FocalLengthIn35mmFilm")
        table.append(row)
    have = sum(1 for row in table if row["downloaded"])
    print(f"assess: {len(table)} Kirkland Hall candidate(s), {have} downloaded")
    if have >= 2:
        result = match_test(small, out_dir / "kirkland_match.db")
    else:
        result = {"images": have, "pairs_verified": 0, "pairs_ge_30_inliers": 0, "largest_group_size": have,
                  "largest_group": [], "group_sizes": [], "top_pairs": [], "links": {},
                  "method": "not run: fewer than 2 Kirkland photos downloaded"}
    for row in table:
        row["strong_links"] = result["links"].get(row["small"], 0)
        row["in_largest_group"] = row["small"] in result["largest_group"]
    result.pop("links")
    result.update({"date": args.date, "set": "Wikimedia Commons, Kirkland Hall", "photos": table})
    out = out_dir / "assessment.json"
    save_json(out, result)
    manifest.add_file(out, source="fetch_samples.py assess", license="metadata", stage=STAGE, date=today(),
                      root=root, db=db)
    db.commit()
    print(f"assess: Kirkland largest group {result['largest_group_size']} of {len(table)}; "
          f"{result['pairs_ge_30_inliers']} strong pair(s); {out}")
    # The fallback set, when present, gets the same test (shows the pipeline works on a good set).
    fb = root / "derived" / "capture" / f"fallback-{args.date}" / f"zenodo-{FALLBACK_RECORD}"
    if (fb / "images").exists():
        fsmall = fb / "images_1600"
        fsmall.mkdir(exist_ok=True)
        for src in sorted((fb / "images").iterdir()):
            dst = fsmall / (src.stem + ".jpg")
            if not dst.exists():
                with Image.open(src) as im:
                    im = im.convert("RGB")
                    im.thumbnail((1600, 1600))
                    im.save(dst, quality=92)
        print("assess: fallback set; extracting SIFT")
        fres = match_test(fsmall, fb / "match.db")
        fres.pop("links")
        fres.update({"date": args.date, "set": f"Zenodo {FALLBACK_RECORD} (Paganico, CC BY 4.0)"})
        fout = fb / "assessment.json"
        save_json(fout, fres)
        manifest.add_file(fout, source="fetch_samples.py assess", license="metadata", stage=STAGE, date=today(),
                          root=root, db=db)
        db.commit()
        print(f"assess: fallback largest group {fres['largest_group_size']} of {fres['images']}; {fout}")
    return True


def match_test(image_dir, dbp, min_inliers=30):
    """SIFT + exhaustive matching on CPU; report the largest group linked by >= min_inliers pairs."""
    import pycolmap
    if dbp.exists():
        dbp.unlink()
    opts = pycolmap.FeatureExtractionOptions()
    opts.sift.max_num_features = 8192
    pycolmap.extract_features(dbp, image_dir, extraction_options=opts, camera_mode=pycolmap.CameraMode.PER_IMAGE,
                              device=pycolmap.Device.cpu)
    pycolmap.match_exhaustive(dbp, device=pycolmap.Device.cpu)
    con = sqlite3.connect(dbp)
    names = dict(con.execute("SELECT image_id, name FROM images"))
    pairs = []
    for pair_id, rows in con.execute("SELECT pair_id, rows FROM two_view_geometries WHERE rows > 0"):
        i2 = pair_id % 2147483647
        i1 = (pair_id - i2) // 2147483647
        pairs.append((names.get(i1), names.get(i2), rows))
    con.close()
    parent = {n: n for n in names.values()}

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    strong = [p for p in pairs if p[2] >= min_inliers]
    for a, b, _ in strong:
        parent[find(a)] = find(b)
    groups = {}
    for n in names.values():
        groups.setdefault(find(n), []).append(n)
    biggest = max(groups.values(), key=len) if groups else []
    links = {n: 0 for n in names.values()}
    for a, b, _ in strong:
        links[a] += 1
        links[b] += 1
    return {"images": len(names), "pairs_verified": len(pairs), "pairs_ge_30_inliers": len(strong),
            "largest_group_size": len(biggest), "largest_group": sorted(biggest),
            "group_sizes": sorted((len(g) for g in groups.values()), reverse=True),
            "top_pairs": sorted(pairs, key=lambda p: -p[2])[:40], "links": links,
            "method": "pycolmap SIFT (CPU, <=8192 features, images <=1600 px), exhaustive matching, "
                      f"two-view geometry inliers >= {min_inliers}; no reconstruction"}


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("step", choices=["commons", "kartaview", "video360", "fallback", "assess", "all"])
    p.add_argument("--date", default=today(), help="folder date (default today); reuse it to resume")
    p.add_argument("--max-files", type=int, default=600, help="Commons photo cap")
    p.add_argument("--max-bytes", type=float, default=4e9, help="Commons photo size cap in bytes")
    p.add_argument("--max-karta", type=int, default=3000, help="KartaView image cap")
    p.add_argument("--pause", type=float, default=3.0, help="seconds between Commons API requests")
    p.add_argument("--file-pause", type=float, default=12.0, help="seconds between Commons file downloads")
    args = p.parse_args(argv)
    root = data_root()
    db = manifest.connect(root)
    steps = ["commons", "kartaview", "video360", "fallback", "assess"] if args.step == "all" else [args.step]
    ok = True
    for step in steps:
        ok = {"commons": run_commons, "kartaview": run_kartaview, "video360": run_video360,
              "fallback": run_fallback, "assess": run_assess}[step](args, root, db) and ok
    db.close()
    if not ok:
        fail("some downloads failed; see the messages above and run again")


if __name__ == "__main__":
    main()
