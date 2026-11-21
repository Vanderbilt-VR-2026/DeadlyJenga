"""Stage 2 capture days: the shot list cut into walking routes of about 2-3 hours.

Reads config/shot_list.json (from shotlist.py) and the OSM snapshot (walking graph).
Writes:
  Tools/VanderbiltCampus/config/capture_days.json
  Docs/Development/VanderbiltCampus/capture/CAPTURE-DAYS.md
  Docs/Development/VanderbiltCampus/capture/days/day-NNN.png   (route maps, NAIP background)
  the "day" and "day_order" fields of config/shot_list.json
  <data_root>/derived/capture_plan/v1/capture_days.json        (archive copy, in the manifest)

How days are made:
  1. Hero and normal entries form the main days; background entries form optional later days.
     Blocked facades (nothing to photograph) get no day.
  2. Areas = the shot list's architectural clusters. Kirkland Hall's cluster first, then the other
     hero clusters (nearest next), then the rest. Inside an area: Kirkland/Alumni Lawn first-capture
     entries, then morning-shade and any-time entries, then afternoon-shade ones (nearest-neighbour).
  3. That sequence is cut into days of equal length (target 2.6 h).
  4. Each day is re-ordered as a loop on the OSM path graph (nearest neighbour + 2-opt), starting and
     ending at the path point nearest the day's middle.
Time model: walking 1.2 m/s between entries, 4 s per photo, 360 legs at 1 m/s with two passes.

Commands:
  capture_days.py build
"""
import argparse
import datetime as dt
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CONFIG, REPO, data_root, fail, load_json, save_json  # noqa: E402

SHOT_LIST = CONFIG / "shot_list.json"
DAYS_JSON = CONFIG / "capture_days.json"
DOCS = REPO / "Docs" / "Development" / "VanderbiltCampus" / "capture"
DAYS_MD = DOCS / "CAPTURE-DAYS.md"
MAP_DIR = DOCS / "days"

WALK_MPS = 1.2
SECONDS_PER_PHOTO = 4.0
WALK360_MPS = 1.0
WALK360_PASSES = 2
TARGET_H = 2.6
MAX_H = 3.5
DETOUR = 1.3                 # straight-line distance x this ~ walking distance (used before routing)
MB_RAW, MB_HEIC = 25.0, 8.0  # per photo: ProRAW/DNG, HEIC/JPEG
MAP_PX = 560
MAP_MAX_BYTES = 300_000
HERO_ID = "B_544397715"


# ---- time model ----------------------------------------------------------------

def entry_seconds(e):
    """Time spent at one entry (not counting the walk to it)."""
    if e["method"] == "360_walk":
        return WALK360_PASSES * float(e.get("length_m") or 0.0) / WALK360_MPS
    return e["est_photos"] * SECONDS_PER_PHOTO


def anchor(e):
    """Where the walker starts an entry (campus meters)."""
    cp = e.get("camera_path") or []
    if e["kind"] == "facade" and cp:
        return tuple(np.mean(np.array(cp), axis=0))
    if cp:
        return tuple(cp[0])
    c = e["centerline"]
    if e.get("normal") and e.get("standoff_m") and len(c) >= 2:
        m = np.mean(np.array(c), axis=0)
        return (m[0] + e["normal"][0] * e["standoff_m"], m[1] + e["normal"][1] * e["standoff_m"])
    return tuple(c[len(c) // 2])


def session_of(e):
    bt = e.get("best_time") or {}
    am, pm = bt.get("morning_ok", True), bt.get("afternoon_ok", True)
    if am and not pm:
        return "am"
    if pm and not am:
        return "pm"
    return "any"


# ---- ordering and cutting ---------------------------------------------------------

def nn_order(items, start):
    """Nearest-neighbour order of items (each has 'xy') from a start point."""
    left = list(items)
    out, cur = [], np.array(start, float)
    while left:
        d = [np.hypot(*(np.array(it["xy"]) - cur)) for it in left]
        k = int(np.argmin(d))
        out.append(left.pop(k))
        cur = np.array(out[-1]["xy"])
    return out


def sequence(items, clusters, first_cluster=None):
    """Order items area by area: first cluster, then hero clusters by proximity, then the rest."""
    by = {}
    for it in items:
        by.setdefault(it["cluster"], []).append(it)
    centers = {c["id"]: np.array(c["center"]) for c in clusters}
    hero = {c["id"] for c in clusters if c.get("hero")}
    left = [c for c in by if c in centers] + [c for c in by if c not in centers]
    order = []
    cur = centers.get(first_cluster) if first_cluster in by else None
    if first_cluster in by:
        order.append(first_cluster)
        left.remove(first_cluster)
    while left:
        pool = [c for c in left if c in hero] or left
        if cur is None:
            nxt = pool[0]
        else:
            nxt = min(pool, key=lambda c: np.hypot(*(centers.get(c, cur) - cur)))
        order.append(nxt)
        left.remove(nxt)
        cur = centers.get(nxt, cur)
    seq, pos = [], (centers[order[0]] if order and order[0] in centers else (0.0, 0.0))
    for c in order:
        group = by[c]
        first = [g for g in group if g.get("first")]
        am = [g for g in group if not g.get("first") and g["session"] in ("am", "any")]
        pm = [g for g in group if not g.get("first") and g["session"] == "pm"]
        for part in (first, am, pm):
            if part:
                o = nn_order(part, pos)
                seq.extend(o)
                pos = o[-1]["xy"]
    return seq


def cut_days(seq, target_h=TARGET_H):
    """Cut an ordered list into days of equal length; returns a list of lists."""
    if not seq:
        return []
    t, prev = [], None
    for it in seq:
        walk = 0.0 if prev is None else DETOUR * math.dist(prev["xy"], it["xy"]) / WALK_MPS
        t.append(walk + it["seconds"])
        prev = it
    total = sum(t)
    n = max(1, int(round(total / 3600.0 / target_h)))
    size = total / n
    days = [[] for _ in range(n)]
    cum = 0.0
    for it, s in zip(seq, t):
        mid = cum + s / 2
        days[min(n - 1, int(mid // size))].append(it)
        cum += s
    return [d for d in days if d]


def tour_2opt(D, order):
    """Improve a closed tour (order[0] is the fixed start) with 2-opt moves."""
    best = list(order)
    n = len(best)
    improved = True
    while improved:
        improved = False
        for i in range(1, n - 1):
            for k in range(i + 1, n):
                a, b = best[i - 1], best[i]
                c, d = best[k], best[(k + 1) % n]
                if D[a, c] + D[b, d] < D[a, b] + D[c, d] - 1e-6:
                    best[i:k + 1] = reversed(best[i:k + 1])
                    improved = True
    return best


def nn_tour(D):
    n = len(D)
    left = set(range(1, n))
    tour = [0]
    while left:
        cur = tour[-1]
        nxt = min(left, key=lambda j: (D[cur, j], j))
        tour.append(nxt)
        left.remove(nxt)
    return tour


# ---- walking graph -------------------------------------------------------------------

class WalkGraph:
    """Every OSM highway (sidewalks, paths, service roads, streets) as an undirected graph."""

    def __init__(self, root):
        import scipy.sparse as sp
        import surfaces
        _, _, _, ways, _, xy = surfaces.load_osm(root)
        ids, rows, cols, w = {}, [], [], []
        for way in ways.values():
            t = way.get("tags") or {}
            hw = t.get("highway")
            if not hw or hw in ("motorway", "motorway_link", "construction", "proposed", "trunk") \
                    or t.get("foot") == "no" or t.get("area") == "yes":
                continue
            ns = [n for n in way["nodes"] if n in xy]
            for a, b in zip(ns, ns[1:]):
                for n in (a, b):
                    if n not in ids:
                        ids[n] = len(ids)
                d = math.dist(xy[a], xy[b])
                rows += [ids[a], ids[b]]
                cols += [ids[b], ids[a]]
                w += [d, d]
        self.xy = np.zeros((len(ids), 2))
        for n, i in ids.items():
            self.xy[i] = xy[n]
        self.M = sp.csr_matrix((w, (rows, cols)), shape=(len(ids), len(ids)))
        from scipy.spatial import cKDTree
        self.tree = cKDTree(self.xy)

    def snap(self, pts):
        d, i = self.tree.query(np.asarray(pts, float))
        return i, d

    def distances(self, nodes):
        from scipy.sparse.csgraph import dijkstra
        D, P = dijkstra(self.M, directed=False, indices=nodes, return_predecessors=True)
        return D, P

    def path(self, P_row, src, dst):
        out, cur = [], dst
        guard = 0
        while cur != src and cur >= 0 and guard < 100000:
            out.append(cur)
            cur = P_row[cur]
            guard += 1
        if cur != src:
            return None
        out.append(src)
        return self.xy[out[::-1]]


class EuclidGraph:
    """Stand-in for tests: straight lines x DETOUR."""

    def snap(self, pts):
        pts = np.asarray(pts, float)
        self.xy = pts
        return np.arange(len(pts)), np.zeros(len(pts))

    def distances(self, nodes):
        P = self.xy[nodes]
        D = DETOUR * np.hypot(P[:, None, 0] - P[None, :, 0], P[:, None, 1] - P[None, :, 1])
        return D, None

    def path(self, P_row, src, dst):
        return None


def route_day(items, graph):
    """Loop route for one day: returns ordered items, leg distances (m), the start point, the polyline."""
    pts = np.array([it["xy"] for it in items])
    centroid = pts.mean(axis=0)
    all_pts = np.vstack([centroid[None, :], pts])
    nodes, snapd = graph.snap(all_pts)
    D, P = graph.distances(np.asarray(nodes))
    D = D[:, np.asarray(nodes)] if D.shape[1] != len(nodes) else D
    n = len(nodes)
    # Unreachable pairs (gaps in the OSM graph) fall back to straight line x DETOUR.
    straight = DETOUR * np.hypot(all_pts[:, None, 0] - all_pts[None, :, 0], all_pts[:, None, 1] - all_pts[None, :, 1])
    D = np.where(np.isfinite(D), D, straight)
    D = D + snapd[:, None] + snapd[None, :]
    np.fill_diagonal(D, 0)
    tour = tour_2opt(D, nn_tour(D)) if n > 3 else list(range(n))
    legs, line = [], []
    for a, b in zip(tour, tour[1:] + [tour[0]]):
        legs.append(float(D[a, b]))
        seg = graph.path(P[a], nodes[a], nodes[b]) if P is not None else None
        if seg is None:
            seg = np.array([all_pts[a], all_pts[b]])
        else:
            seg = np.vstack([all_pts[a][None, :], seg, all_pts[b][None, :]])
        line.extend(seg.tolist())
    ordered = [items[i - 1] for i in tour[1:]]
    start_xy = graph.xy[nodes[0]] if hasattr(graph, "xy") and P is not None else centroid
    return ordered, legs, (float(start_xy[0]), float(start_xy[1])), line


def simplify_line(line, tol=1.5):
    from shapely.geometry import LineString
    if len(line) < 2:
        return [[round(p[0], 1), round(p[1], 1)] for p in line]
    s = LineString(line).simplify(tol)
    return [[round(x, 1), round(y, 1)] for x, y in s.coords]


def plan_days(entries, clusters, graph, first_day=1, optional=False, first_cluster=None):
    items = []
    cmap = {}
    for c in clusters:
        for b in c["buildings"]:
            cmap[b] = c["id"]
    centers = {c["id"]: np.array(c["center"]) for c in clusters}
    for e in entries:
        xy = anchor(e)
        cl = e.get("cluster") or cmap.get(e.get("building"))
        if cl is None and centers:
            cl = min(centers, key=lambda c: np.hypot(*(centers[c] - np.array(xy))))
        items.append({"e": e, "xy": xy, "cluster": cl or "C00", "seconds": entry_seconds(e),
                      "session": session_of(e), "first": bool(e.get("first_capture"))})
    groups = []
    firsts = [it for it in items if it["first"]]
    if firsts:
        # Day 1: every first-capture entry (Kirkland + Alumni Lawn), then the nearest hero entries,
        # then the nearest others, until the day reaches the target length.
        c = np.mean([it["xy"] for it in firsts], axis=0)
        rest = sorted([it for it in items if not it["first"]],
                      key=lambda it: (it["e"]["priority"] != "hero", math.dist(it["xy"], c)))
        day1, t = list(firsts), sum(it["seconds"] for it in firsts)
        t += DETOUR * 60 * len(firsts) / WALK_MPS
        for it in rest:
            if t >= TARGET_H * 3600 or math.dist(it["xy"], c) > 250:
                break
            day1.append(it)
            t += it["seconds"] + DETOUR * 40 / WALK_MPS
        taken = {id(it) for it in day1}
        items = [it for it in items if id(it) not in taken]
        groups.append(day1)
    seq = sequence(items, clusters, first_cluster)
    groups += cut_days(seq)
    days = []
    for k, group in enumerate(groups):
        ordered, legs, start, line = route_day(group, graph)
        walk_m = sum(legs)
        work = sum(it["seconds"] for it in ordered)
        hours = (walk_m / WALK_MPS + work) / 3600.0
        photos = sum(it["e"]["est_photos"] for it in ordered)
        stops, t = [], 0.0
        for i, (it, leg) in enumerate(zip(ordered, legs)):
            t += leg / WALK_MPS + it["seconds"]
            stops.append({"n": i + 1, "id": it["e"]["id"], "walk_m": round(leg), "minutes": round(it["seconds"] / 60, 1),
                          "photos": it["e"]["est_photos"], "elapsed_min": round(t / 60)})
        sess = {"am": [], "pm": [], "any": []}
        for it in ordered:
            sess[it["session"]].append(it["e"]["id"])
        am_ph = sum(it["e"]["est_photos"] for it in ordered if it["session"] == "am")
        pm_ph = sum(it["e"]["est_photos"] for it in ordered if it["session"] == "pm")
        session = "morning" if am_ph > pm_ph else ("afternoon" if pm_ph > am_ph else "either")
        clusters_used = sorted({it["cluster"] for it in ordered})
        days.append({"day": first_day + k, "optional": optional, "clusters": clusters_used,
                     "start": [round(start[0], 1), round(start[1], 1)], "session": session,
                     "walk_m": round(walk_m), "hours": round(hours, 2), "photos": photos,
                     "gb_raw": round(photos * MB_RAW / 1000, 1), "gb_heic": round(photos * MB_HEIC / 1000, 1),
                     "stops": stops, "morning": sess["am"], "afternoon": sess["pm"], "any_time": sess["any"],
                     "route": simplify_line(line), "_items": ordered})
    return days


# ---- maps -------------------------------------------------------------------------------

class Ortho:
    def __init__(self, root):
        from PIL import Image
        Image.MAX_IMAGE_PIXELS = None
        meta = load_json(root / "derived" / "ortho" / "v1" / "naip2021_campus_0.6m.json")
        self.meta = meta
        self.img = Image.open(root / "derived" / "ortho" / "v1" / meta["file"]).convert("RGB")

    def crop(self, x0, y0, x1, y1):
        m = self.meta
        c0 = (x0 - m["origin_x"]) / m["cell_m"]
        c1 = (x1 - m["origin_x"]) / m["cell_m"]
        r0 = (m["origin_y"] - y1) / m["cell_m"]
        r1 = (m["origin_y"] - y0) / m["cell_m"]
        return self.img.crop((int(c0), int(r0), int(math.ceil(c1)), int(math.ceil(r1))))


def draw_map(day, ortho, path, title):
    from PIL import Image, ImageDraw, ImageEnhance, ImageFont
    pts = [it["xy"] for it in day["_items"]] + [tuple(day["start"])] + [tuple(p) for p in day["route"]]
    for it in day["_items"]:
        pts += [tuple(p) for p in it["e"]["centerline"]]
    P = np.array(pts)
    x0, y0 = P.min(axis=0) - 30
    x1, y1 = P.max(axis=0) + 30
    span = max(x1 - x0, y1 - y0, 200)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    w, h = max(x1 - x0, 200), max(y1 - y0, 200)
    x0, x1, y0, y1 = cx - w / 2, cx + w / 2, cy - h / 2, cy + h / 2
    scale = MAP_PX / span
    W, H = int(round(w * scale)), int(round(h * scale)) + 22
    if ortho is not None:
        base = ortho.crop(x0, y0, x1, y1).resize((W, H - 22), Image.BILINEAR)
        base = ImageEnhance.Color(base).enhance(0.55)
        base = ImageEnhance.Brightness(base).enhance(0.8)
    else:
        base = Image.new("RGB", (W, H - 22), (60, 60, 60))
    img = Image.new("RGB", (W, H), (20, 20, 20))
    img.paste(base, (0, 22))
    d = ImageDraw.Draw(img)

    def px(p):
        return ((p[0] - x0) * scale, 22 + (y1 - p[1]) * scale)

    try:
        font = ImageFont.load_default(size=12)
        big = ImageFont.load_default(size=14)
    except TypeError:
        font = big = ImageFont.load_default()
    for it in day["_items"]:
        c = [px(p) for p in it["e"]["centerline"]]
        col = (230, 159, 0) if it["e"]["priority"] == "hero" else (86, 180, 233)
        if len(c) >= 2:
            d.line(c, fill=col, width=4)
    route = [px(p) for p in day["route"]]
    if len(route) >= 2:
        d.line(route, fill=(255, 255, 255), width=5)
        d.line(route, fill=(0, 114, 178), width=3)
    sx, sy = px(day["start"])
    d.rectangle([sx - 7, sy - 7, sx + 7, sy + 7], fill=(255, 255, 255), outline=(0, 0, 0))
    d.text((sx, sy), "S", fill=(0, 0, 0), font=font, anchor="mm")
    for s, it in zip(day["stops"], day["_items"]):
        x, y = px(it["xy"])
        r = 8 if s["n"] < 100 else 10
        d.ellipse([x - r, y - r, x + r, y + r], fill=(213, 94, 0) if it["e"]["priority"] == "hero" else (0, 0, 0),
                  outline=(255, 255, 255))
        d.text((x, y), str(s["n"]), fill=(255, 255, 255), font=font, anchor="mm")
    d.text((6, 4), title, fill=(255, 255, 255), font=big)
    # North arrow and 50 m scale bar.
    d.line([(W - 14, H - 40), (W - 14, H - 62)], fill=(255, 255, 255), width=2)
    d.text((W - 14, H - 70), "N", fill=(255, 255, 255), font=font, anchor="mm")
    d.line([(8, H - 10), (8 + 50 * scale, H - 10)], fill=(255, 255, 255), width=3)
    d.text((12 + 50 * scale, H - 10), "50 m", fill=(255, 255, 255), font=font, anchor="lm")
    path.parent.mkdir(parents=True, exist_ok=True)
    for colors in (64, 32, 16):
        q = img.quantize(colors=colors, method=Image.Quantize.MEDIANCUT)
        q.save(path, optimize=True)
        if path.stat().st_size <= MAP_MAX_BYTES:
            break
    return path.stat().st_size


# ---- markdown ---------------------------------------------------------------------------

def fmt_h(h):
    return f"{int(h)} h {int(round((h - int(h)) * 60)):02d} min"


def write_markdown(doc, entries_by_id):
    S = doc["summary"]
    L = ["# Capture days (Stage 2)", "",
         f"Generated {doc['generated']} by `Tools/VanderbiltCampus/pipeline/capture_days.py` from "
         "[SHOT-LIST.md](SHOT-LIST.md) / `config/shot_list.json`. Each day is a loop that starts and ends at "
         "**S** on its map. Times: walking 1.2 m/s between stops, 4 s per photo, 360 legs at 1 m/s with two "
         "passes. Storage: 25 MB per ProRAW/DNG photo, 8 MB per HEIC/JPEG.", "",
         "## Summary", "",
         f"- Main days (hero + normal): **{S['days']}**, {S['hours']:.0f} h, {S['photos']:,} photos, "
         f"{S['gb_raw']:,.0f} GB as DNG / {S['gb_heic']:,.0f} GB as HEIC.",
         f"- Optional later days (background: hospital, parking, utility, walls facing out of campus): "
         f"**{S['optional_days']}**, {S['optional_hours']:.0f} h, {S['optional_photos']:,} photos, "
         f"{S['optional_gb_raw']:,.0f} GB DNG / {S['optional_gb_heic']:,.0f} GB HEIC.",
         f"- Day length: {S['min_hours']:.2f}-{S['max_hours']:.2f} h (target {TARGET_H} h).",
         f"- Not scheduled: {S['unscheduled']} blocked facades (nothing to photograph).",
         "- 360 legs are timed as 360 walks; with a phone only, use the photo counts given (one photo every "
         "1.5-2 m). If you use a 360 camera, 8K video is about 15 MB per second "
         f"(~{S['video_gb']:,.0f} GB for every 360 entry, two passes).",
         "- **Session**: morning or afternoon, whichever fits more of the day's shade-side photos. Entries "
         "listed under the other session are best on an overcast day (or swap them with a later day).", "",
         "| Day | Areas | Session | Hours | Walk km | Stops | Photos | GB DNG / HEIC | Map |",
         "|---|---|---|---|---|---|---|---|---|"]
    for d in doc["days"]:
        tag = f"{d['day']}" + (" (optional)" if d["optional"] else "")
        L.append(f"| {tag} | {', '.join(d['clusters'])} | {d['session']} | {d['hours']:.2f} | "
                 f"{d['walk_m'] / 1000:.1f} | {len(d['stops'])} | {d['photos']:,} | {d['gb_raw']} / {d['gb_heic']} | "
                 f"[map]({d['map']}) |")
    L += ["", "Areas: " + "; ".join(f"{c['id']} = {c['title']}" for c in doc["clusters"]) + ".", ""]
    for d in doc["days"]:
        L += [f"## Day {d['day']}{' (optional)' if d['optional'] else ''}: {fmt_h(d['hours'])}, "
              f"{d['photos']:,} photos", "",
              f"![Day {d['day']} route]({d['map']})", "",
              f"Start and end: **S** at ({d['start'][0]:.0f}, {d['start'][1]:.0f}) campus m. Suggested session: "
              f"**{d['session']}**. Walk {d['walk_m'] / 1000:.1f} km. Storage {d['gb_raw']} GB DNG / "
              f"{d['gb_heic']} GB HEIC.", ""]
        for s in d["stops"]:
            e = entries_by_id[s["id"]]
            label = e.get("building_name") or e.get("name") or e["id"]
            what = f"{label} / {e['facade']}" if e.get("facade") else label
            sess = {"am": " [morning]", "pm": " [afternoon]"}.get(
                "am" if s["id"] in d["morning"] else "pm" if s["id"] in d["afternoon"] else "", "")
            L.append(f"{s['n']}. {what} - {e['method']}, {s['photos']} photos, {s['minutes']} min, walk "
                     f"{s['walk_m']} m{sess}{' (first capture)' if e.get('first_capture') else ''}")
        L.append("")
    DAYS_MD.write_text("\n".join(L) + "\n", encoding="utf-8", newline="\n")


# ---- main -------------------------------------------------------------------------------

def build(graph=None, ortho=None, shot=None, write=True):
    root = data_root() if write else None
    doc_in = shot or load_json(SHOT_LIST)
    entries = doc_in["entries"]
    clusters = doc_in.get("clusters", [])
    if graph is None:
        print("walking graph ...", flush=True)
        graph = WalkGraph(root)
    if ortho is None and write:
        ortho = Ortho(root)
    plan = [e for e in entries if e["est_photos"] > 0 or e["method"] == "360_walk"]
    main = [e for e in plan if e["priority"] != "background"]
    back = [e for e in plan if e["priority"] == "background"]
    first_cluster = next((c["id"] for c in clusters if HERO_ID in c["buildings"]), None)
    print("planning days ...", flush=True)
    days = plan_days(main, clusters, graph, 1, False, first_cluster)
    days += plan_days(back, clusters, graph, len(days) + 1, True)
    for e in entries:
        e["day"] = None
        e.pop("day_order", None)
    by_id = {e["id"]: e for e in entries}
    for d in days:
        for s in d["stops"]:
            by_id[s["id"]]["day"] = d["day"]
            by_id[s["id"]]["day_order"] = s["n"]
        d["map"] = f"days/day-{d['day']:03d}.png"
    sizes = []
    if write:
        for d in days:
            title = (f"Day {d['day']}{' (optional)' if d['optional'] else ''}: {d['hours']:.1f} h, "
                     f"{d['photos']:,} photos, {d['walk_m'] / 1000:.1f} km, {d['session']}")
            sizes.append(draw_map(d, ortho, DOCS / d["map"], title))
    md = [d for d in days if not d["optional"]]
    od = [d for d in days if d["optional"]]
    video_s = sum(entry_seconds(e) for e in plan if e["method"] == "360_walk")
    hours = [d["hours"] for d in days]
    summary = {"days": len(md), "hours": round(sum(d["hours"] for d in md), 1),
               "photos": sum(d["photos"] for d in md),
               "gb_raw": round(sum(d["photos"] for d in md) * MB_RAW / 1000, 1),
               "gb_heic": round(sum(d["photos"] for d in md) * MB_HEIC / 1000, 1),
               "optional_days": len(od), "optional_hours": round(sum(d["hours"] for d in od), 1),
               "optional_photos": sum(d["photos"] for d in od),
               "optional_gb_raw": round(sum(d["photos"] for d in od) * MB_RAW / 1000, 1),
               "optional_gb_heic": round(sum(d["photos"] for d in od) * MB_HEIC / 1000, 1),
               "min_hours": min(hours) if hours else 0, "max_hours": max(hours) if hours else 0,
               "unscheduled": sum(1 for e in entries if e["day"] is None),
               "video_gb": round(video_s * 15 / 1000, 1),
               "map_bytes_max": max(sizes) if sizes else 0, "map_bytes_total": sum(sizes)}
    out = {"version": 1, "generated": dt.datetime.now().isoformat(timespec="seconds"),
           "shot_list_generated": doc_in.get("generated"),
           "params": {"walk_mps": WALK_MPS, "seconds_per_photo": SECONDS_PER_PHOTO, "walk360_mps": WALK360_MPS,
                      "walk360_passes": WALK360_PASSES, "target_h": TARGET_H, "mb_raw": MB_RAW, "mb_heic": MB_HEIC},
           "summary": summary, "clusters": [{"id": c["id"], "title": c["title"]} for c in clusters],
           "days": [{k: v for k, v in d.items() if k != "_items"} for d in days]}
    if write:
        save_json(DAYS_JSON, out)
        save_json(SHOT_LIST, doc_in)
        write_markdown(out, by_id)
        arch = root / "derived" / "capture_plan" / "v1" / "capture_days.json"
        save_json(arch, out)
        import manifest
        manifest.add_file(arch, source="pipeline/capture_days.py", license="derived (OSM ODbL)", stage=2, root=root)
        print(f"main days {summary['days']} ({summary['hours']} h, {summary['photos']:,} photos, "
              f"{summary['gb_raw']:,} GB DNG / {summary['gb_heic']:,} GB HEIC); optional {summary['optional_days']} "
              f"({summary['optional_hours']} h); day length {summary['min_hours']}-{summary['max_hours']} h; "
              f"largest map {summary['map_bytes_max'] / 1000:.0f} KB")
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("build", help="make the capture days from config/shot_list.json")
    ap.parse_args(argv)
    if not SHOT_LIST.exists():
        fail(f"{SHOT_LIST} is missing; run shotlist.py build first")
    out = build()
    if out["summary"]["map_bytes_max"] > MAP_MAX_BYTES:
        fail("a route map is over 300 KB")


if __name__ == "__main__":
    main()
