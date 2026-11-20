"""Estimated facade layouts for buildings that have no measured (photo) layout yet.

Every feature written here has "source": "estimated" and the layout "reviewed": false. They
are stand-ins: a later worker replaces them with measured layouts from aligned photos, and
the assembler is simply re-run.

Two generators share the same LiDAR readings (per facade: ground height in front of it, and
raised clusters outside the footprint = steps):
  grid_layouts(ctx, kit)    rule only: storeys from the kit storey height, a regular window grid,
                            one door (+ steps) mid-way along the longest facade, coping/cornice at
                            the eave; LiDAR only gives the ground line in front of each facade.
  kirkland_layouts(bid)     Kirkland Hall (Victorian Gothic): raised basement up to the main
                            stair landing (LiDAR), three storeys with pointed / round / segmental
                            heads, limestone base course (a ledge) and drip courses, cornice,
                            pointed portal with flanking columns at the main stair, tower faces
                            with stages, a clock face and belfry openings (tower heights from
                            LiDAR; opening sizes are typical proportions, not measurements).

    python pipeline/estimate_layouts.py B_544413714 --rule grid --kit brick_institutional
    python pipeline/estimate_layouts.py B_544397715 --rule kirkland
"""
import argparse
import json
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "blender"))
import facade_layout as fl  # noqa: E402
from common import data_root, load_json  # noqa: E402

ESTIMATED = "estimated"


# ---- LiDAR readings ---------------------------------------------------------------------------

def load_context(bid, root=None, points=None):
    """Facade planes (footprint + tower), ground in front of each, and step clusters."""
    import assemble_building as ab
    import shapely
    from shapely.geometry import Polygon
    ab._imports()
    root = Path(root or data_root())
    inp = ab.load_inputs(bid, root)
    rec, model = inp["rec"], inp["model"]
    ground = float(model["ground_z"])
    fp = np.asarray(rec["footprint"])
    if points is None:
        import lidar_io
        points = lidar_io.load_points(fp[:, 0].min(), fp[:, 1].min(), fp[:, 0].max(), fp[:, 1].max(), buffer=16,
                                      root=root)
    roof = ab.detect_roof_features(bid, inp, root, points)
    facs = fl.facades_of(bid, root, lod2=inp["lod2"], footprint=rec["footprint"], ground_z=ground)
    tf = ab.tower_facades(roof["tower"], len(facs), ground) if roof["tower"] else []
    P = np.c_[points["x"], points["y"], points["z"]]
    c = points["classification"]
    g = c == 2
    from scipy.spatial import cKDTree
    tree = cKDTree(P[g, :2])
    _, j = tree.query(P[:, :2], k=8)
    local_ground = np.median(P[g, 2][j], axis=1)
    h = P[:, 2] - local_ground
    poly = Polygon(rec["footprint"])
    inside = shapely.contains_xy(poly, P[:, 0], P[:, 1])
    dist = shapely.distance(poly.boundary, shapely.points(P[:, :2]))
    ctx = {"bid": bid, "root": root, "model": model, "rec": rec, "roof": roof, "ground_z": ground,
           "facades": facs, "tower_facades": tf, "points": P, "cls": c}
    # ground in front of every facade
    for fc in facs + tf:
        pl = fc["plane"]
        q = fl.to_facade(pl, P[g])
        sel = (q[:, 2] > 1.0) & (q[:, 2] < 8.0) & (q[:, 0] > 0) & (q[:, 0] < pl["width_m"])
        fc["ground_v"] = round(float(np.median(q[sel, 1])), 3) if sel.sum() >= 5 else 0.0
        prof = []
        for b0 in np.arange(0.0, pl["width_m"], 2.0):
            sb = sel & (q[:, 0] >= b0) & (q[:, 0] < b0 + 2.0)
            if sb.sum() >= 3:
                prof.append([round(float(b0 + 1.0), 2), round(float(np.median(q[sb, 1])), 3)])
        fc["ground_profile"] = prof or [[pl["width_m"] / 2, fc["ground_v"]]]
    # raised clusters outside the footprint (steps / terraces): not trees, not buildings
    cand = (~inside) & (dist < 14) & (h > 0.35) & (h < 4.5) & (c != 5) & (c != 4) & (c != 3) & (c != 7)
    clusters = []
    if cand.sum() >= 20:
        from sklearn.cluster import DBSCAN
        lab = DBSCAN(eps=0.9, min_samples=8).fit_predict(P[cand, :2])
        idx = np.where(cand)[0]
        for k in set(lab) - {-1}:
            mem = idx[lab == k]
            if len(mem) < 30:
                continue
            best = None
            for fc in facs + tf:
                pl = fc["plane"]
                q = fl.to_facade(pl, P[mem])
                front = (q[:, 2] > -0.8) & (q[:, 2] < 15) & (q[:, 0] > -1) & (q[:, 0] < pl["width_m"] + 1)
                cu = np.median(q[:, 0])
                score = front.mean() - 0.05 * np.median(np.abs(q[:, 2])) - (0.5 if not 0 <= cu <= pl["width_m"] else 0)
                if best is None or score > best[0]:
                    best = (score, fc, q)
            score, fc, q = best
            if score < 0.4:
                continue
            hh = h[mem]
            near = q[:, 2] < 1.5
            top_h = float(np.percentile(hh[near], 90)) if near.sum() >= 5 else float(np.percentile(hh, 90))
            corr = float(np.corrcoef(q[:, 2], hh)[0, 1]) if len(mem) > 3 else 0.0
            u0, u1 = np.percentile(q[:, 0], [3, 97])
            # cheek walls: points well above a straight tread line from the top (at the wall) to the
            # foot; the stair is the widest empty gap between two groups of such points
            run95 = float(np.percentile(q[:, 2], 95))
            cheeks = None
            allq = fl.to_facade(fc["plane"], P)
            near_c = (allq[:, 2] > 1.0) & (allq[:, 2] < run95 - 0.5) & (allq[:, 0] > u0 - 2) & (allq[:, 0] < u1 + 2)                 & (c != 5) & (allq[:, 1] < top_h + 3)
            exp = (np.median(local_ground[mem]) - fc["plane"]["origin"][2]) + top_h * (1 - allq[:, 2] / max(run95, 1.0))
            hi_u = allq[near_c & (allq[:, 1] > exp + 0.5), 0]
            if len(hi_u) >= 10:
                edges = np.arange(u0 - 2, u1 + 2.25, 0.25)
                cnt, _ = np.histogram(hi_u, bins=edges)
                empty = cnt <= 1
                bestrun, i = None, 0
                while i < len(empty):
                    if empty[i]:
                        j = i
                        while j < len(empty) and empty[j]:
                            j += 1
                        if i > 0 and j < len(empty) and (j - i) * 0.25 >= 2.0 and (bestrun is None or j - i > bestrun[1] - bestrun[0]):
                            bestrun = (i, j)
                        i = j
                    else:
                        i += 1
                if bestrun:
                    u0, u1 = float(edges[bestrun[0]]), float(edges[bestrun[1]])
                    cheeks = [round(u0, 2), round(u1, 2)]
            clusters.append({"facade": fc["facade"], "points": int(len(mem)), "u0": round(float(u0), 2),
                             "u1": round(float(u1), 2), "run": round(float(np.percentile(q[:, 2], 95)), 2),
                             "top_v": round(float(np.median(local_ground[mem]) + top_h - fc["plane"]["origin"][2]), 2),
                             "foot_v": round(float(np.median(local_ground[mem][q[:, 2] > np.percentile(q[:, 2], 70)])
                                                   - fc["plane"]["origin"][2]), 2),
                             "top_h": round(top_h, 2), "slope_corr": round(corr, 2), "cheek_walls_u": cheeks,
                             "kind": "stair" if corr < -0.3 else "terrace"})
    ctx["clusters"] = sorted(clusters, key=lambda s: -s["points"])
    return ctx


# ---- helpers ---------------------------------------------------------------------------------

class Ids:
    def __init__(self):
        self.n = {}

    def __call__(self, t):
        p = {"window": "w", "door": "d", "steps": "s", "string_course": "c", "cornice": "k", "column": "o",
             "buttress": "b", "projection": "p", "recess": "r", "arch": "a"}.get(t, "x")
        self.n[p] = self.n.get(p, 0) + 1
        return f"{p}{self.n[p]:03d}"


def bays(width, bay_w, margin):
    """Centres of regular bays along a wall."""
    usable = width - 2 * margin
    if usable < bay_w * 0.6:
        return [width / 2] if width >= 2.4 else []
    n = max(1, int(usable // bay_w))
    step = usable / n
    return [margin + (i + 0.5) * step for i in range(n)]


def clip_rect(r, W, H):
    return [max(0.0, r[0]), r[1], min(W, r[2]), min(H, r[3])]


def _layout(bid, fc, features, note):
    pl = fc["plane"]
    W, H = pl["width_m"], pl["height_m"]
    feats = []
    for f in features:
        r = clip_rect(f["rect"], W, H)
        if r[2] - r[0] < 0.05 or r[3] - r[1] < 0.05:
            continue
        f = dict(f)
        f["rect"] = [round(x, 3) for x in r]
        feats.append(f)
    return fl.new_layout(bid, fc["facade"], pl, feats,
                         source={"alignment_set": None, "photos": [], "rectified_image": None, "px_per_m": None})


def _stairs_for(ctx, fc):
    """Step clusters in front of this facade: sloped ones, or big flat ones (>= 100 points),
    with their along-wall range clipped to the facade."""
    W = fc["plane"]["width_m"]
    out = []
    for s in ctx["clusters"]:
        if s["facade"] != fc["facade"] or not (s["kind"] == "stair" or s["points"] >= 100):
            continue
        s = dict(s, u0=max(0.3, s["u0"]), u1=min(W - 0.3, s["u1"]))
        if s["u1"] - s["u0"] >= 1.0:
            out.append(s)
    return out


# ---- rule-based grid ---------------------------------------------------------------------------

def ground_at(fc, u):
    prof = np.asarray(fc["ground_profile"], float)
    return float(np.interp(u, prof[:, 0], prof[:, 1]))


def grid_layouts(ctx, kit_name="brick_institutional"):
    """Rule only: storeys of the kit storey height from the facade's lowest ground up to the eave,
    a window in every bay of every storey (skipped where the ground in front is higher than the
    sill), one door with steps in the middle of the longest facade, a coping/cornice at the top.
    LiDAR is used only for the ground line in front of each facade."""
    from kits import get_kit
    kit = get_kit(kit_name)
    bid = ctx["bid"]
    sh, bw = kit["storey_h"], kit["bay_w"]
    out = {}
    longest = max(ctx["facades"], key=lambda f: f["plane"]["width_m"])
    for fc in ctx["facades"]:
        pl = fc["plane"]
        W, H = pl["width_m"], pl["height_m"]
        if H < 2.5 or W < 2.0:
            continue
        ids = Ids()
        g_lo = min(v for _, v in fc["ground_profile"])
        floor0 = g_lo + 0.15
        top = H - kit["cornice"]["height"]
        n_st = max(1, int(round((top - floor0) / sh)))
        st_h = (top - floor0) / n_st
        feats, doors = [], []
        if fc is longest:
            c = W / 2
            g = ground_at(fc, c)
            # door on the storey line nearest above the ground at the door
            k = max(0, int(math.ceil((g + 0.1 - floor0) / st_h - 1e-6)))
            fv = floor0 + k * st_h
            doors.append((c - 1.3, c + 1.3))
            feats.append(dict(fl.new_feature(ids("door"), "door", [c - 1.0, fv, c + 1.0, fv + min(2.7, st_h - 0.4)],
                                             confidence=0.2, source=ESTIMATED)))
            if fv - g > 0.1:
                n = max(1, round((fv - g) / 0.165))
                feats.append(dict(fl.new_feature(ids("steps"), "steps", [c - 1.5, g, c + 1.5, fv],
                                                 depth_m=max(0.3 * n, 0.6), confidence=0.2, source=ESTIMATED)))
        for k in range(n_st):
            fv = floor0 + k * st_h
            ww, wh = 1.2, min(1.8, st_h - 1.4)
            if wh < 0.8:
                continue
            for cu in bays(W, bw, 0.9):
                if any(a < cu < b for a, b in doors) and fv <= feats[0]["rect"][1] + 0.01:
                    continue
                sill = fv + 0.9
                if sill < max(ground_at(fc, cu - ww / 2), ground_at(fc, cu + ww / 2)) + 0.3:
                    continue
                feats.append(dict(fl.new_feature(ids("window"), "window", [cu - ww / 2, sill, cu + ww / 2, sill + wh],
                                                 confidence=0.2, source=ESTIMATED)))
        feats.append(dict(fl.new_feature(ids("cornice"), "cornice", [0, top, W, H],
                                         depth_m=kit["cornice"]["depth"], confidence=0.3, source=ESTIMATED)))
        out[fc["facade"]] = _layout(bid, fc, feats, "grid")
    return out


# ---- Kirkland Hall ---------------------------------------------------------------------------

KIRKLAND_FACTS = [
    "Kirkland Hall (1874, rebuilt 1905-06 after a fire): red brick Victorian Gothic with limestone trim",
    "One central clock tower since the rebuild (the 1874 building had two towers)",
    "Raised basement with a monumental stone stair up to a pointed-arch main portal (Commons photo 2019_Sit-in)",
    "Tall windows with stone surrounds; pointed and round-arched heads; stone string courses",
]


def kirkland_layouts(ctx):
    """Kirkland Hall stand-in layouts (see module help). Returns {facade: layout}."""
    bid = ctx["bid"]
    tw = ctx["roof"]["tower"]
    stairs = [s for s in ctx["clusters"] if s["kind"] == "stair"]
    big = max(stairs, key=lambda s: s["points"]) if stairs else None
    main_v = big["top_v"] if big else 3.4                 # main floor = top of the main stair (LiDAR)
    out = {}
    tshaft = None
    if tw:
        from shapely.geometry import Polygon
        tshaft = Polygon(tw["shaft"]).buffer(1.0)
    eave_v = float(np.median([f["plane"]["height_m"] for f in ctx["facades"] if f["plane"]["height_m"] > 10]))
    cornice_h = 0.7
    st_top = eave_v - cornice_h
    n_st = 3
    st_h = (st_top - main_v) / n_st
    # storey -> (arch, width, sill above floor, height)
    storeys = [("pointed", 1.3, 0.8, min(3.6, st_h - 1.2)), ("round", 1.3, 0.9, min(3.0, st_h - 1.4)),
               ("segmental", 1.2, 0.9, min(2.4, st_h - 1.6))]
    from shapely.geometry import Point
    for fc in ctx["facades"]:
        pl = fc["plane"]
        W, H = pl["width_m"], pl["height_m"]
        mid = fl.to_world(pl, W / 2, 0)
        if tshaft is not None and tshaft.contains(Point(mid[0], mid[1])):
            continue                                   # covered by the rebuilt tower's faces
        if H < 6.0 or W < 2.0:
            continue                                   # low porch / landing blocks: steps only, no facade
        ids = Ids()
        g = fc["ground_v"]
        feats = []
        my_st = _stairs_for(ctx, fc)
        doorz = []
        for s in my_st:
            c = (s["u0"] + s["u1"]) / 2
            sw = s["u1"] - s["u0"]
            is_main = big is not None and s["facade"] == big["facade"] and s["points"] == big["points"]
            top = s["top_v"]
            if is_main:
                dw, dh = 2.6, 5.2
                feats.append(dict(fl.new_feature(ids("door"), "door", [c - dw / 2, top, c + dw / 2, top + dh], arch="pointed",
                                                 depth_m=-0.9, confidence=0.35, source=ESTIMATED)))
                for side in (-1, 1):
                    cu = c + side * (dw / 2 + 0.45)
                    feats.append(dict(fl.new_feature(ids("column"), "column", [cu - 0.2, top, cu + 0.2, top + 3.4],
                                                     depth_m=0.35, confidence=0.3, source=ESTIMATED)))
                doorz.append((c - dw / 2 - 1.0, c + dw / 2 + 1.0))
            else:
                dw = min(1.8, max(1.2, sw * 0.5))
                feats.append(dict(fl.new_feature(ids("door"), "door", [c - dw / 2, top, c + dw / 2, top + 3.0],
                                                 arch="segmental", depth_m=-0.45, confidence=0.25, source=ESTIMATED)))
                doorz.append((c - dw / 2 - 0.6, c + dw / 2 + 0.6))
            feats.append(dict(fl.new_feature(ids("steps"), "steps", [s["u0"], s["foot_v"], s["u1"], top],
                                             depth_m=max(s["run"], 0.6), confidence=0.6 if s["kind"] == "stair" else 0.35,
                                             source=ESTIMATED)))
        # basement windows (segmental), only where they clear the ground in front
        for cu in bays(W, 3.8, 1.0):
            if any(a < cu < b for a, b in doorz):
                continue
            sill = max(g + 0.35, 0.6)
            head = main_v - 0.55
            if head - sill >= 0.9:
                feats.append(dict(fl.new_feature(ids("window"), "window", [cu - 0.55, sill, cu + 0.55, min(head, sill + 1.4)],
                                                 arch="segmental", confidence=0.25, source=ESTIMATED)))
            for k, (arch, ww, sill_a, wh) in enumerate(storeys):
                fv = main_v + k * st_h
                if k == 0 and any(a < cu < b for a, b in doorz):
                    continue
                if wh < 1.0:
                    continue
                feats.append(dict(fl.new_feature(ids("window"), "window",
                                                 [cu - ww / 2, fv + sill_a, cu + ww / 2, fv + sill_a + wh],
                                                 arch=arch, confidence=0.25, source=ESTIMATED)))
        # limestone base course at the main floor (deep enough to stand on: the ledge), drip courses, cornice
        feats.append(dict(fl.new_feature(ids("string_course"), "string_course", [0, main_v - 0.35, W, main_v],
                                         depth_m=0.25, confidence=0.4, source=ESTIMATED)))
        for k in (1, 2):
            fv = main_v + k * st_h + 0.55
            feats.append(dict(fl.new_feature(ids("string_course"), "string_course", [0, fv, W, fv + 0.25],
                                             depth_m=0.12, confidence=0.3, source=ESTIMATED)))
        feats.append(dict(fl.new_feature(ids("cornice"), "cornice", [0, H - cornice_h, W, H],
                                         depth_m=0.35, confidence=0.3, source=ESTIMATED)))
        out[fc["facade"]] = _layout(bid, fc, feats, "kirkland")
    # tower faces: stages from the LiDAR heights; clock and belfry are typical proportions
    for fc in ctx["tower_facades"]:
        pl = fc["plane"]
        W, H = pl["width_m"], pl["height_m"]
        o = pl["origin"][2]
        ids = Ids()
        feats = []
        roof_v = ctx["roof"]["eave_z"] - o + 1.0          # first visible point above the main roof
        cor_v = tw["cornice_z"] - o
        belfry0, belfry1 = cor_v - 7.0, cor_v - 1.2
        clock_c = belfry0 - 2.6
        for v in (roof_v + 0.2, belfry0 - 4.8, belfry0 - 0.3):
            feats.append(dict(fl.new_feature(ids("string_course"), "string_course", [0, v, W, v + 0.3],
                                             depth_m=0.15, confidence=0.3, source=ESTIMATED)))
        feats.append(dict(fl.new_feature(ids("cornice"), "cornice", [0, cor_v - 0.3, W, cor_v + 0.3],
                                         depth_m=tw["cornice_projection_m"], confidence=0.5, source=ESTIMATED)))
        r = min(1.5, W * 0.25)
        circ = [[round(W / 2 + r * math.cos(2 * math.pi * i / 20), 3), round(clock_c + r * math.sin(2 * math.pi * i / 20), 3)]
                for i in range(20)]
        feats.append(dict(fl.new_feature(ids("projection"), "projection", [W / 2 - r, clock_c - r, W / 2 + r, clock_c + r],
                                         depth_m=0.10, outline=circ, confidence=0.3, source=ESTIMATED)))
        ow = min(1.1, W * 0.18)
        for cu in (W / 2 - ow * 0.75, W / 2 + ow * 0.75):
            rect = [cu - ow / 2, belfry0 + 0.4, cu + ow / 2, belfry1]
            from kits.geom import opening_outline
            ol = [[round(a, 3), round(b, 3)] for a, b in opening_outline(*rect, "pointed", 8)]
            feats.append(dict(fl.new_feature(ids("recess"), "recess", rect, arch="pointed", depth_m=-0.45, outline=ol,
                                             confidence=0.3, source=ESTIMATED)))
        lw = min(1.2, W * 0.2)
        feats.append(dict(fl.new_feature(ids("window"), "window", [W / 2 - lw / 2, roof_v + 1.5, W / 2 + lw / 2,
                                                                   roof_v + 5.0], arch="pointed", confidence=0.25,
                                         source=ESTIMATED)))
        for u0 in (0.0, W - 0.6):
            feats.append(dict(fl.new_feature(ids("buttress"), "buttress", [u0, roof_v, u0 + 0.6, belfry0 - 0.3],
                                             depth_m=0.35, confidence=0.2, source=ESTIMATED)))
        out[fc["facade"]] = _layout(bid, fc, feats, "kirkland tower")
    return out


def save_if_changed(layouts, root=None, source="estimate_layouts.py"):
    """Save each layout as a new version unless the newest saved one has the same plane and features."""
    saved = {}
    for k, lay in layouts.items():
        old = fl.load_latest(lay["building"], k, root)
        if old and old["plane"] == lay["plane"] and old["features"] == lay["features"] and old["source"] == lay["source"]:
            saved[k] = old
            continue
        p = fl.save(lay, root, source=source, license="derived (estimated)")
        saved[k] = fl.load(p)
    return saved


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("building")
    ap.add_argument("--rule", choices=["grid", "kirkland"], default="grid")
    ap.add_argument("--kit", default="brick_institutional")
    ap.add_argument("--dry-run", action="store_true", help="print the readings and feature counts; save nothing")
    args = ap.parse_args(argv)
    ctx = load_context(args.building)
    print("tower:", json.dumps(ctx["roof"]["tower"]))
    for s in ctx["clusters"]:
        print("cluster:", s)
    lays = kirkland_layouts(ctx) if args.rule == "kirkland" else grid_layouts(ctx, args.kit)
    for k, lay in sorted(lays.items()):
        kinds = {}
        for f in lay["features"]:
            kinds[f["type"]] = kinds.get(f["type"], 0) + 1
        print(k, lay["plane"]["width_m"], lay["plane"]["height_m"], kinds)
    if not args.dry_run:
        saved = save_if_changed(lays)
        for k, lay in sorted(saved.items()):
            print(f"{k}: layout_v{lay['version']}")


if __name__ == "__main__":
    main()
