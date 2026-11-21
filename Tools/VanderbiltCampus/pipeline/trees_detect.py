"""Find every tree in the campus and the backdrop ring from the LiDAR (Stage 1).

Steps:
 1. Canopy height model (CHM) on the Stage 0 0.5 m grid: for each cell the highest
    vegetation point above the ground (dtm.tif). Vegetation = classes 4 and 5, plus
    class-1 points more than 2 m up that are not inside a building footprint grown by 1 m.
 2. Smooth lightly (a Gaussian of 0.5 m that also fills the small holes between points).
 3. Tree tops = local maxima with a window that grows with height:
    radius = 1.5 m + 0.12 x height. Tops lower than 3 m are ignored.
 4. Crowns = watershed of the upside-down CHM, started from the tops (each crown grows
    downhill from its top until it meets a neighbour). Crown cells lower than 30% of the
    top or farther than the cap radius from the top are dropped.
 5. Per tree, from the points inside its crown: crown base (15th percentile of the point
    heights above 1 m), point count, share of real vegetation points, confidence.
 6. Clean up: tops on buildings (unless clearly a tree over the roof), poles and wires
    (mostly non-vegetation points) are dropped; narrow low ones become "shrub".
 7. Merge with OSM natural=tree nodes within 2 m (OSM ids and tags are kept).

Output (archive):
  derived/trees/v1/trees.json   {"trees": [...], "dropped": [...], counts, parameters}
  derived/trees/v1/chm.tif      smoothed CHM (m above ground) + .json sidecar
  derived/trees/v1/crowns.tif   crown of each tree: value = index in "trees" + 1 (0 = none)
Tree ids TR_<n> are numbered by sorted position (x, then y), so they are the same on
every run with the same data.
"""
import argparse
import datetime
import os
import sys
import tempfile
import time
from multiprocessing import Pool
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
import trees_common as tc  # noqa: E402
from common import data_root, fail, save_json  # noqa: E402

OUT_VERSION = "v1"
LICENSE = "Derived: USGS 3DEP LiDAR (public domain), OpenStreetMap (ODbL 1.0), Metro Nashville open data"

# Detection parameters (all heights in meters above ground).
P = {
    "cell_m": 0.5,
    "smooth_sigma_m": 0.5,
    "min_tree_height": 3.0,
    "window_a": 1.5,            # local-max window radius = a + b * height
    "window_b": 0.12,
    "crown_min_cell_height": 2.0,
    "crown_rel_height": 0.30,   # crown cells must be >= 30% of the top height
    "crown_cap_diameter": 25.0,
    "crown_cap_per_height": 1.6,  # crown radius cap = min(cap/2, 0.8 * height + 1)
    "class1_min_height": 2.0,
    "footprint_buffer": 1.0,
    "shrub_max_crown": 1.5,
    "shrub_max_height": 5.0,
    "min_veg_fraction": 0.25,
    "osm_match_m": 2.0,
    "spike_m": 3.0,              # single points this far above the smoothed CHM are noise
    "roof_clearance": 1.0,       # a tree over a roof must top the roof under it by 1 m
    "over_roof_min_points": 30,
    "over_roof_min_veg": 0.6,
}


# ---- core algorithm (pure functions, also used by the tests) --------------

def smooth_chm(chm_raw, cell, sigma_m):
    """Normalized Gaussian: smooths and fills cells without points from their neighbours."""
    from scipy.ndimage import gaussian_filter
    s = sigma_m / cell
    valid = (chm_raw > 0).astype(np.float32)
    num = gaussian_filter(chm_raw.astype(np.float32), s, truncate=3.0)
    den = gaussian_filter(valid, s, truncate=3.0)
    out = np.zeros_like(num)
    ok = den > 0.2
    out[ok] = num[ok] / den[ok]
    return out


def _disc(R):
    yy, xx = np.mgrid[-R:R + 1, -R:R + 1]
    return (xx * xx + yy * yy) <= R * R


def detect_tops(S, cell, min_h=3.0, a=1.5, b=0.12):
    """Local maxima of S with a height-dependent window. Returns (rows, cols, heights)."""
    from scipy.ndimage import maximum_filter
    H, W = S.shape
    cand = (S >= maximum_filter(S, size=3)) & (S >= min_h)
    rr, cc = np.nonzero(cand)
    hh = S[rr, cc]
    order = np.argsort(-hh, kind="stable")
    kept = np.zeros(S.shape, bool)
    discs = {}
    out = []
    for i in order:
        r, c, h = rr[i], cc[i], hh[i]
        R = int(np.ceil((a + b * h) / cell))
        if R not in discs:
            discs[R] = _disc(R)
        d = discs[R]
        r0, r1, c0, c1 = r - R, r + R + 1, c - R, c + R + 1
        dr0, dc0 = max(0, -r0), max(0, -c0)
        dr1, dc1 = d.shape[0] - max(0, r1 - H), d.shape[1] - max(0, c1 - W)
        dd = d[dr0:dr1, dc0:dc1]
        win = S[max(r0, 0):min(r1, H), max(c0, 0):min(c1, W)]
        if win[dd].max() > h:
            continue                                  # a higher cell inside the window
        if kept[max(r0, 0):min(r1, H), max(c0, 0):min(c1, W)][dd].any():
            continue                                  # an equal top was already taken
        kept[r, c] = True
        out.append(i)
    out = np.asarray(out, dtype=np.int64)
    return rr[out], cc[out], hh[out]


def delineate(S, tops_r, tops_c, cell, min_cell_h=2.0, rel_h=0.30, cap_d=25.0, cap_per_h=1.6):
    """Crown labels (1..N, 0 = no crown) by marker-controlled watershed on -S."""
    from skimage.segmentation import watershed
    n = len(tops_r)
    markers = np.zeros(S.shape, np.int32)
    markers[tops_r, tops_c] = np.arange(1, n + 1, dtype=np.int32)
    mask = S >= min_cell_h
    mask[tops_r, tops_c] = True
    labels = watershed(-S, markers, mask=mask)
    # Remove cells too low for their crown or too far from its top.
    lr, lc = np.nonzero(labels)
    lab = labels[lr, lc]
    htop = np.concatenate([[0], S[tops_r, tops_c]])
    tr = np.concatenate([[0], tops_r]); tcc = np.concatenate([[0], tops_c])
    cap_r = np.minimum(cap_d / 2, 0.5 * cap_per_h * htop + 1.0)
    dist = np.hypot(lr - tr[lab], lc - tcc[lab]) * cell
    bad = (S[lr, lc] < rel_h * htop[lab]) | (dist > cap_r[lab])
    labels[lr[bad], lc[bad]] = 0
    return labels


def refine_tops(S, labels, tr, tc_, th, g, drop=0.5, R=2):
    """Sub-cell top position: weighted centre of the crown's cells within 0.5 m of the top
    height and 1 m of the top cell (weights = how far above top - 0.5 m they are)."""
    X, Y = tc.cell_center(tr, tc_, g)
    H, W = S.shape
    yy, xx = np.mgrid[-R:R + 1, -R:R + 1]
    for k in range(len(tr)):
        r, c = tr[k], tc_[k]
        if r < R or c < R or r >= H - R or c >= W - R:
            continue
        win = S[r - R:r + R + 1, c - R:c + R + 1]
        w = np.clip(win - (th[k] - drop), 0, None) * (labels[r - R:r + R + 1, c - R:c + R + 1] == k + 1)
        if w.sum() > 0:
            X[k] += (w * xx).sum() / w.sum() * g["cell_m"]
            Y[k] -= (w * yy).sum() / w.sum() * g["cell_m"]
    return X, Y


def crown_areas(labels, n, cell):
    return np.bincount(labels.ravel(), minlength=n + 1)[1:n + 1] * cell * cell


def group_points(lab, n):
    """Index arrays of the points of each label 1..n (list of arrays, position = label-1)."""
    order = np.argsort(lab, kind="stable")
    sl = lab[order]
    starts = np.searchsorted(sl, np.arange(1, n + 2))
    return [order[starts[k]:starts[k + 1]] for k in range(n)]


def point_stats(lab, x, y, h, is_veg, n):
    """Per crown: count, veg fraction, crown-base p15, p99 height, centroid of the top 10%."""
    stats = {k: np.full(n, np.nan) for k in ("count", "veg_frac", "p15", "p99", "top_x", "top_y")}
    for k, idx in enumerate(group_points(lab, n)):
        stats["count"][k] = len(idx)
        if len(idx) == 0:
            continue
        hk = h[idx]
        stats["veg_frac"][k] = is_veg[idx].mean()
        up = hk[hk > 1.0]
        if len(up):
            stats["p15"][k] = np.percentile(up, 15)
        stats["p99"][k] = np.percentile(hk, 99)
        top = hk >= np.percentile(hk, 90)
        stats["top_x"][k] = x[idx][top].mean()
        stats["top_y"][k] = y[idx][top].mean()
    return stats


# ---- reading the LiDAR in parallel ----------------------------------------

_W = {}


def _init_worker(work_dir, grid, folder):
    _W["dtm"] = np.load(Path(work_dir) / "dtm.npy", mmap_mode="r")
    _W["fp1"] = np.load(Path(work_dir) / "fp1.npy", mmap_mode="r")
    _W["g"] = grid
    _W["folder"] = Path(folder)


def tile_points(tile_file):
    """Vegetation points (x, y, height above ground, class) of one tile and its roof heights."""
    g = _W["g"]
    d = lidar_io.read_tile(_W["folder"] / tile_file)
    cls = d["classification"]
    sel = np.isin(cls, np.array([1, 4, 5, 6], np.uint8))
    x, y, z, cls = d["x"][sel], d["y"][sel], d["z"][sel], cls[sel]
    r, c = tc.cell_of(x, y, g)
    ok = tc.inside_grid(r, c, g)
    x, y, z, cls, r, c = x[ok], y[ok], z[ok], cls[ok], r[ok], c[ok]
    ground = tc.sample_bilinear(_W["dtm"], g, x, y)
    h = z - ground
    veg = ((cls == 4) | (cls == 5)) & (h > 0.5)
    other = (cls == 1) & (h > P["class1_min_height"]) & (_W["fp1"][r, c] == 0)
    keep = veg | other
    roof = (cls == 6) & (h > 1.0)
    lin = r[roof] * g["width"] + c[roof]
    rh = h[roof].astype(np.float32)
    if len(lin):
        o = np.argsort(lin)
        lin, rh = lin[o], rh[o]
        first = np.r_[True, lin[1:] != lin[:-1]]
        starts = np.nonzero(first)[0]
        rh = np.maximum.reduceat(rh, starts)
        lin = lin[starts]
    return {"x": x[keep].astype(np.float32), "y": y[keep].astype(np.float32),
            "h": h[keep].astype(np.float32), "veg": veg[keep], "r": r[keep].astype(np.int32),
            "c": c[keep].astype(np.int32), "roof_lin": lin, "roof_h": rh}


def read_all_points(tiles, g, dtm, fp1, jobs):
    with tempfile.TemporaryDirectory(prefix="vc_trees_") as work:
        np.save(Path(work) / "dtm.npy", dtm)
        np.save(Path(work) / "fp1.npy", fp1)
        folder = lidar_io.lidar_dir()
        files = [t["file"] for t in tiles]
        if jobs <= 1:
            _init_worker(work, g, folder)
            parts = [tile_points(f) for f in files]
        else:
            with Pool(jobs, initializer=_init_worker, initargs=(work, g, str(folder))) as pool:
                parts = pool.map(tile_points, files, chunksize=2)
        _W.clear()      # drop the memory maps before the temp folder is removed
    out = {k: np.concatenate([p[k] for p in parts]) for k in parts[0]}
    return out


# ---- main -----------------------------------------------------------------

def footprint_masks(g, root):
    """(inside footprint, inside footprint grown by 1 m) from OSM + Metro footprints."""
    polys = [b["poly"] for b in tc.osm_buildings(root)] + [m["poly"] for m in tc.metro_footprints(root)]
    # Reconciled footprints from the buildings step, when it has run (same "polygon" layout as
    # ring_footprints.json: list of parts, each [exterior, holes...]).
    from shapely.geometry import Polygon
    from common import load_json as _lj
    for name in ("footprints.json", "ring_footprints.json"):
        f = root / "derived/buildings/v1" / name
        if f.exists():
            for b in _lj(f).get("buildings", []):
                for part in b.get("polygon") or []:
                    if part and len(part[0]) >= 3:
                        polys.append(Polygon(part[0], part[1:]).buffer(0))
    fp0 = tc.rasterize([(p, 1) for p in polys], g)
    fp1 = tc.rasterize([(p.buffer(P["footprint_buffer"]), 1) for p in polys], g)
    return fp0, fp1, len(polys)


def window_max(a, r, c, R):
    H, W = a.shape
    return a[max(r - R, 0):min(r + R + 1, H), max(c - R, 0):min(c + R + 1, W)].max()


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--jobs", type=int, default=max(1, min(12, (os.cpu_count() or 2) - 2)),
                   help="worker processes for reading tiles (default: CPU count - 2, max 12)")
    p.add_argument("--zones", default="campus,ring", help="tile zones to read (default campus,ring)")
    p.add_argument("--no-manifest", action="store_true", help="do not record outputs in the manifest")
    args = p.parse_args(argv)
    t0 = time.time()
    root = data_root()
    out_dir = root / "derived" / "trees" / OUT_VERSION

    dtm, g = tc.read_raster(tc.rasters_dir(root) / "dtm.tif")
    dtm = dtm.astype(np.float32)
    cell = g["cell_m"]
    if abs(cell - P["cell_m"]) > 1e-9:
        fail(f"dtm.tif cell is {cell} m; expected {P['cell_m']}")
    zones = set(args.zones.split(","))
    tiles = [t for t in lidar_io.load_index()["tiles"] if t["zone"] in zones and t["points"] > 0]
    print(f"{len(tiles)} tiles in zones {sorted(zones)}; footprints...", flush=True)
    fp0, fp1, nfp = footprint_masks(g, root)

    print(f"reading points with {args.jobs} workers...", flush=True)
    pts = read_all_points(tiles, g, dtm, fp1, args.jobs)
    print(f"  {len(pts['h']):,} vegetation points ({pts['veg'].mean():.1%} class 4/5) "
          f"[{time.time() - t0:.0f} s]", flush=True)

    # 1-2. CHM and smoothing.
    chm_raw = np.zeros((g["height"], g["width"]), np.float32)
    np.maximum.at(chm_raw, (pts["r"], pts["c"]), pts["h"])
    roof = np.zeros_like(chm_raw)
    rl = pts["roof_lin"]
    np.maximum.at(roof.reshape(-1), rl, pts["roof_h"])
    S = smooth_chm(chm_raw, cell, P["smooth_sigma_m"])

    # 3. Tops.
    tr, tcol, th = detect_tops(S, cell, P["min_tree_height"], P["window_a"], P["window_b"])
    print(f"  {len(tr):,} tree tops [{time.time() - t0:.0f} s]", flush=True)

    # 4. Crowns.
    labels = delineate(S, tr, tcol, cell, P["crown_min_cell_height"], P["crown_rel_height"],
                       P["crown_cap_diameter"], P["crown_cap_per_height"])
    n = len(tr)
    area = crown_areas(labels, n, cell)
    print(f"  crowns delineated [{time.time() - t0:.0f} s]", flush=True)

    # 5. Point statistics per crown.
    plab = labels[pts["r"], pts["c"]]
    inc = plab > 0
    st = point_stats(plab[inc], pts["x"][inc].astype(np.float64), pts["y"][inc].astype(np.float64),
                     pts["h"][inc].astype(np.float64), pts["veg"][inc], n)
    from scipy.ndimage import maximum_filter
    # Height: highest point within ~1 m of the top, after removing single-point spikes
    # (birds, noise) that stand more than 3 m above the smoothed CHM.
    despiked = np.where(chm_raw > S + P["spike_m"], S, chm_raw)
    raw_near = maximum_filter(despiked, size=5)[tr, tcol]
    X, Y = refine_tops(S, labels, tr, tcol, th, g)
    ground = tc.sample_bilinear(dtm, g, X, Y)
    zone = tc.zone_of(X, Y)

    # 6. Build the records and drop what is not a tree.
    trees, dropped = [], []
    for k in range(n):
        h = float(max(raw_near[k], th[k]))
        diam = float(min(2 * np.sqrt(area[k] / np.pi), P["crown_cap_diameter"]))
        cnt = int(st["count"][k])
        vf = float(st["veg_frac"][k]) if cnt else 0.0
        base = st["p15"][k]
        base = float(np.clip(base if np.isfinite(base) else 0.35 * h, max(1.0, 0.15 * h), 0.7 * h))
        rec = {"x": round(float(X[k]), 2), "y": round(float(Y[k]), 2), "ground_z": round(float(ground[k]), 2),
               "height": round(h, 2), "crown_diameter": round(diam, 2), "crown_base": round(base, 2),
               "points": cnt, "veg_fraction": round(vf, 3), "zone": str(zone[k]),
               "tile": fr.tile_of(float(X[k]), float(Y[k])), "_label": k + 1}
        reason = None
        if zone[k] == "outside":
            reason = "outside_ring"
        elif cnt == 0 or vf < P["min_veg_fraction"]:
            reason = "not_vegetation"       # wires, poles, rooftop units seen as class 1
        elif fp0[tr[k], tcol[k]]:
            # Inside a footprint: keep it only if it is clearly a tree over (or in a courtyard of)
            # the building: enough real vegetation points and the top well above the roof right under it.
            roof_near = float(window_max(roof, tr[k], tcol[k], 1))
            over = (cnt >= P["over_roof_min_points"] and vf >= P["over_roof_min_veg"]
                    and (roof_near == 0 or h > roof_near + P["roof_clearance"]))
            if not over:
                reason = "on_building"
            else:
                rec["over_building"] = True
        if reason:
            rec["reason"] = reason
            dropped.append(rec)
            continue
        rec["kind"] = "shrub" if (diam < P["shrub_max_crown"] and h < P["shrub_max_height"]) else "tree"
        conf = 0.4 * min(1.0, cnt / 60.0) + 0.3 * min(1.0, (h - 3.0) / 7.0) + 0.3 * vf
        rec["confidence"] = round(float(conf), 3)
        rec["source"] = "lidar"
        trees.append(rec)
    print(f"  {len(trees):,} kept, {len(dropped):,} dropped", flush=True)

    # 7. OSM natural=tree nodes.
    from scipy.spatial import cKDTree
    osm = tc.osm_trees(root)
    osm_report = {"nodes": len(osm), "matched": 0, "inside_crown": 0, "added": 0, "no_lidar": 0}
    if osm and trees:
        kd = cKDTree(np.array([[t["x"], t["y"]] for t in trees]))
        pairs = []
        for j, o in enumerate(osm):
            for i in kd.query_ball_point([o["x"], o["y"]], P["osm_match_m"]):
                pairs.append((np.hypot(trees[i]["x"] - o["x"], trees[i]["y"] - o["y"]), i, j))
        used_t, used_o = set(), set()
        for d, i, j in sorted(pairs):
            if i in used_t or j in used_o:
                continue
            used_t.add(i); used_o.add(j)
            t = trees[i]
            t["osm_id"] = osm[j]["osm_id"]; t["osm_tags"] = osm[j]["tags"]; t["osm_offset_m"] = round(float(d), 2)
            t["source"] = "lidar+osm"
            t["confidence"] = round(min(1.0, t["confidence"] + 0.1), 3)
            osm_report["matched"] += 1
        label_to_tree = {t["_label"]: t for t in trees}
        for j, o in enumerate(osm):
            if j in used_o:
                continue
            if tc.zone_of(o["x"], o["y"])[0] == "outside":
                osm_report["outside"] = osm_report.get("outside", 0) + 1
                continue
            r, c = tc.cell_of(o["x"], o["y"], g)
            lab = int(labels[r, c]) if tc.inside_grid(r, c, g) else 0
            if lab in label_to_tree:
                label_to_tree[lab].setdefault("osm_in_crown", []).append(o["osm_id"])
                osm_report["inside_crown"] += 1
                continue
            hloc = float(window_max(chm_raw, int(r), int(c), int(round(3.0 / cell))))
            z = tc.zone_of(o["x"], o["y"])[0]
            rec = {"x": round(o["x"], 2), "y": round(o["y"], 2),
                   "ground_z": round(float(tc.sample_bilinear(dtm, g, o["x"], o["y"])), 2),
                   "points": 0, "veg_fraction": 0.0, "zone": str(z), "tile": fr.tile_of(o["x"], o["y"]),
                   "osm_id": o["osm_id"], "osm_tags": o["tags"], "source": "osm_only", "_label": 0}
            if hloc >= 2.0:
                rec.update(kind="tree", height=round(hloc, 2), crown_diameter=round(float(np.clip(0.6 * hloc, 2, 8)), 2),
                           crown_base=round(max(1.0, 0.35 * hloc), 2), confidence=0.3)
                osm_report["added"] += 1
            else:
                # LiDAR (2022) sees nothing over 2 m here: probably removed or a sapling. Not built.
                rec.update(kind="osm_missing", height=round(hloc, 2), crown_diameter=0.0, crown_base=0.0,
                           confidence=0.1)
                osm_report["no_lidar"] += 1
            trees.append(rec)

    # Stable ids by position, then the crown raster that points into the list.
    trees.sort(key=lambda t: (round(t["x"], 1), round(t["y"], 1)))
    remap = np.zeros(n + 1, np.int32)
    for i, t in enumerate(trees):
        t["id"] = f"TR_{i + 1:05d}"
        if t["_label"]:
            remap[t["_label"]] = i + 1
    crowns = remap[labels]
    for t in trees + dropped:
        t.pop("_label", None)
    keyorder = ["id", "kind", "x", "y", "ground_z", "height", "crown_diameter", "crown_base", "points",
                "veg_fraction", "confidence", "source", "zone", "tile"]
    trees = [{**{k: t[k] for k in keyorder if k in t}, **{k: v for k, v in t.items() if k not in keyorder}}
             for t in trees]

    def count(kind=None, zone_=None):
        return sum(1 for t in trees if (kind is None or t["kind"] == kind) and (zone_ is None or t["zone"] == zone_))
    counts = {z: {k: count(k, z) for k in ("tree", "shrub", "osm_missing")} for z in ("campus", "ring")}
    drop_counts = {}
    for d in dropped:
        drop_counts[d["reason"]] = drop_counts.get(d["reason"], 0) + 1

    out = {
        "version": 1,
        "made": datetime.datetime.now().isoformat(timespec="seconds"),
        "frame": "campus meters (config/frame.json); x, y = tree top (sub-cell); ground_z = DTM at the top",
        "fields": {"height": "m above ground (highest LiDAR point within 1 m of the top)",
                   "crown_diameter": "m, equivalent diameter of the watershed crown (capped)",
                   "crown_base": "m above ground, 15th percentile of crown point heights over 1 m (clamped)",
                   "confidence": "0..1 = 0.4 point count + 0.3 height + 0.3 vegetation share (+0.1 OSM match)",
                   "kind": "tree | shrub (narrow and low, not built) | osm_missing (OSM node, no LiDAR tree)"},
        "sources": {"lidar": "derived/lidar/v1 (classes 1, 4, 5, 6)", "ground": "derived/rasters/v1/dtm.tif",
                    "osm": tc.osm_snapshot(root).relative_to(root).as_posix(),
                    "footprints": f"OSM + Metro + reconciled (derived/buildings/v1, if present) footprints ({nfp} polygons)"},
        "parameters": P,
        "counts": counts,
        "dropped_counts": drop_counts,
        "osm": osm_report,
        "trees": trees,
        "dropped": dropped,
    }
    out_dir.mkdir(parents=True, exist_ok=True)
    save_json(out_dir / "trees.json", out)
    files = [out_dir / "trees.json"]
    files += tc.write_raster(out_dir / "chm.tif", S.astype(np.float32), g, "smoothed canopy height (m above ground)",
                             {"source": "trees_detect.py"})
    files += tc.write_raster(out_dir / "crowns.tif", crowns.astype(np.int32), g,
                             "crown of tree: index in trees.json 'trees' + 1 (0 = none)", {"source": "trees_detect.py"})
    if not args.no_manifest:
        db = manifest.connect(root)
        for f in files:
            manifest.add_file(f, "trees_detect.py (derived/lidar/v1, OSM, Metro footprints)", LICENSE, 1,
                              root=root, db=db)
        db.commit(); db.close()
    print(f"counts {counts}; dropped {drop_counts}; osm {osm_report}")
    print(f"wrote {out_dir} in {time.time() - t0:.0f} s")


if __name__ == "__main__":
    main()
