"""Stage 1, buildings step 1: reconcile building footprints (OSM, Metro, LiDAR).

For every building whose footprint centroid lies inside the campus or the 150 m
backdrop ring (config/boundary.geojson):

1. OSM and Metro Nashville footprints are converted to the campus frame.
   Overlapping OSM outlines are cleaned (a building drawn inside another is cut out
   of the bigger one; partial overlaps are cut from the bigger one; duplicates dropped).
2. OSM and Metro outlines are matched by overlap. Matched pairs are compared corner
   by corner (each corner's distance to the other outline).
3. The LiDAR outline: class 6 points (plus class 1 points 2 m above the DTM) on a
   0.25 m grid, closed and hole-filled (buildings_common.LidarEdge). Each footprint
   edge is sampled every 0.25 m and its offset to the LiDAR edge measured. Samples on
   shared walls (another building right outside) or under trees are not used.
4. Choice: OSM and Metro agree within 0.3 m -> OSM. Otherwise the one closer to the
   LiDAR edge. Roofs overhang walls: the building's typical offset (median, clipped
   to 0..0.6 m) is treated as overhang and never "corrected". An edge that is still
   more than 0.3 m off along most of its length (and long enough, with clean
   evidence) is moved parallel to the LiDAR edge; corners are re-intersected so
   right angles and directions stay. Every decision is written per building.
5. Buildings in Metro but not OSM (and LiDAR-only roofs not in either) become
   B_new_<n>, numbered by centroid (sorted by X, then Y).

Outputs (archive):
  derived/buildings/v1/footprints.json        every campus + ring building
  derived/buildings/v1/ring_footprints.json   ring buildings for the LoD1 ring worker:
                                              id, polygon, ground Z, eave Z, top Z
Run:
  python pipeline/buildings_footprints.py [--jobs 14] [--only B_544397715 ...]
"""
import argparse
import math
import sys
import time
from multiprocessing import Pool
from pathlib import Path

import numpy as np
import shapely
from shapely.geometry import MultiPolygon, Polygon, box as sbox
from shapely.strtree import STRtree

sys.path.insert(0, str(Path(__file__).resolve().parent))
import buildings_common as bc  # noqa: E402
import frame as fr  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
from common import data_root, fail  # noqa: E402

AGREE_M = 0.30          # OSM and Metro agree when their corners are this close
EDGE_TOL = 0.30         # an edge more than this off the LiDAR edge (after overhang) is moved
MAX_OVERHANG = 0.60     # a uniform offset up to this is roof overhang, not an error
MIN_EDGE_FOR_MOVE = 1.0 # shorter edges are jogs, never moved alone
MIN_GOOD_SHARE = 0.5    # an edge needs clean LiDAR evidence on this share of its samples


# ---- step A: gather and clean the candidates (single process) ------------

def stats(d):
    d = np.asarray(d, float)
    d = d[np.isfinite(d)]
    if not len(d):
        return None
    return {"median": round(float(np.median(d)), 3), "p95": round(float(np.percentile(d, 95)), 3),
            "max": round(float(d.max()), 3), "n": int(len(d))}


def corner_dist(g, ref):
    """Distance of each corner of g to the outline of ref."""
    c = bc.corners(g)
    if not len(c) or ref is None:
        return np.zeros(0)
    return shapely.distance(shapely.points(c), ref.boundary)


def resolve_osm_overlaps(blds, log):
    """Cut overlaps between OSM outlines so no two buildings share floor area."""
    geoms = [b["geom"] for b in blds]
    tree = STRtree(geoms)
    drop = set()
    for i, b in enumerate(blds):
        for j in tree.query(b["geom"], predicate="intersects"):
            j = int(j)
            if j <= i or j in drop or i in drop:
                continue
            a, c = blds[i], blds[j]
            inter = a["geom"].intersection(c["geom"]).area
            if inter < 0.5:
                continue
            small, big = (a, c) if a["geom"].area <= c["geom"].area else (c, a)
            if inter > 0.8 * big["geom"].area:
                # near duplicates: keep the named / bigger one
                loser = small if (big.get("name") or not small.get("name")) else big
                drop.add(blds.index(loser))
                log.append(f"{loser['id']} dropped: duplicate of "
                           f"{(big if loser is small else small)['id']} ({inter:.1f} m2 shared)")
                continue
            cut = bc.clean_polygon(big["geom"].difference(small["geom"]))
            if cut is None:
                continue
            kind = "contains" if inter > 0.8 * small["geom"].area else "overlaps"
            big["geom"] = cut
            big.setdefault("decisions", []).append(
                f"OSM outline {kind} {small['id']}: {inter:.1f} m2 cut out of this building")
            small.setdefault("decisions", []).append(
                f"OSM outline overlapped by {big['id']} ({inter:.1f} m2); this building keeps that area")
    return [b for k, b in enumerate(blds) if k not in drop]


def match_metro(blds, metro):
    """Link OSM buildings and Metro footprints that overlap by > 30% of the smaller one."""
    tree = STRtree([m["geom"] for m in metro])
    links_b = {i: [] for i in range(len(blds))}
    links_m = {k: [] for k in range(len(metro))}
    for i, b in enumerate(blds):
        for k in tree.query(b["geom"], predicate="intersects"):
            k = int(k)
            m = metro[k]
            inter = b["geom"].intersection(m["geom"]).area
            if inter > 0.3 * min(b["geom"].area, m["geom"].area):
                links_b[i].append(k)
                links_m[k].append(i)
    return links_b, links_m


def lidar_only_candidates(known, area_poly, root):
    """Roof areas in the 0.5 m building mask that no OSM/Metro footprint covers."""
    import rasterio.features
    from rasterio.transform import Affine
    from scipy import ndimage
    mask = bc.Grid(Path(root) / "derived" / "rasters" / "v1" / "building_mask.tif")
    ndsm = bc.Grid(Path(root) / "derived" / "rasters" / "v1" / "ndsm.tif")
    tr = Affine(mask.cell, 0, mask.x0, 0, -mask.cell, mask.y0)
    m = mask.data > 0
    m = ndimage.binary_closing(m, np.ones((3, 3), bool))
    cover = rasterio.features.rasterize([g.buffer(1.0) for g in known], out_shape=m.shape, transform=tr)
    inside = rasterio.features.rasterize([area_poly], out_shape=m.shape, transform=tr)
    m &= (cover == 0) & (inside > 0)
    m = ndimage.binary_opening(m, np.ones((5, 5), bool))
    lab, n = ndimage.label(m)
    out = []
    if not n:
        return out
    sizes = ndimage.sum(m, lab, np.arange(1, n + 1)) * mask.cell ** 2
    slices = ndimage.find_objects(lab)
    for k in np.nonzero(sizes >= 30.0)[0] + 1:
        sl = slices[k - 1]
        comp = lab[sl] == k
        h = ndsm.data[sl][comp]
        if np.nanmedian(h) < 2.5:
            continue
        r0, c0 = sl[0].start, sl[1].start
        trk = Affine(mask.cell, 0, mask.x0 + c0 * mask.cell, 0, -mask.cell, mask.y0 - r0 * mask.cell)
        shapes = [shapely.geometry.shape(s) for s, v in
                  rasterio.features.shapes(comp.astype(np.uint8), mask=comp, transform=trk) if v == 1]
        g = bc.clean_polygon(shapely.unary_union(shapes))
        if g is None:
            continue
        g = max(bc.polys_of(g), key=lambda p: p.area)
        g = Polygon(g.exterior)
        rect = g.minimum_rotated_rectangle
        g = rect if g.area / rect.area > 0.8 else bc.clean_polygon(g.simplify(0.4))
        if g is not None:
            out.append({"geom": g, "ndsm_median": float(np.nanmedian(h)), "area": float(sizes[k - 1])})
    return out


def gather(root, log):
    zones = bc.boundary()
    campus, ring = zones["campus"], zones["backdrop_ring"]
    area_all = campus.union(ring)
    osm, osm_points, skipped = bc.load_osm_buildings(root)
    metro = bc.load_metro(root)

    def zone_of(g):
        c = g.centroid
        if campus.contains(c):
            return "campus"
        if ring.contains(c):
            return "ring"
        return None

    blds = []
    for b in osm:
        z = zone_of(b["geom"])
        if z:
            b["zone"] = z
            b["source_geoms"] = {"osm": b["geom"]}
            blds.append(b)
    for s in skipped:
        log.append(f"{s['id']} skipped: {s['reason']}")
    blds = resolve_osm_overlaps(blds, log)
    for b in blds:
        b["source_geoms"]["osm"] = b["geom"]
    links_b, links_m = match_metro(blds, metro)
    for i, b in enumerate(blds):
        ks = links_b[i]
        b["metro_ids"] = [metro[k]["metro_id"] for k in ks]
        b["metro"] = [metro[k] for k in ks]
        b.setdefault("decisions", [])
        if not ks:
            b["decisions"].append("no Metro footprint overlaps this OSM building")
            continue
        shared = [k for k in ks if len(links_m[k]) > 1]
        if shared:
            others = sorted({blds[j]["id"] for k in shared for j in links_m[k] if j != i})
            b["decisions"].append(f"Metro footprint(s) {[metro[k]['metro_id'] for k in shared]} also cover "
                                  f"{others}: OSM splits what Metro merges; OSM split kept")
            mg = None
        else:
            mg = bc.clean_polygon(shapely.unary_union([metro[k]["geom"] for k in ks]))
            if len(ks) > 1:
                b["decisions"].append(f"Metro has {len(ks)} footprints here where OSM has one; compared "
                                      "against their union, OSM kept as one building (roof parts split it)")
        if mg is not None:
            b["source_geoms"]["metro"] = mg

    # Metro-only buildings (no OSM outline): B_new
    new = []
    taken = {k for ks in links_b.values() for k in ks}
    pt_tree = shapely.points([p["xy"] for p in osm_points]) if osm_points else None
    for k, m in enumerate(metro):
        if k in taken:
            continue
        z = zone_of(m["geom"])
        if not z:
            continue
        name, osm_ref = None, None
        if pt_tree is not None:
            inside = np.nonzero(shapely.contains(m["geom"], pt_tree))[0]
            if len(inside):
                name, osm_ref = osm_points[inside[0]]["name"], osm_points[inside[0]]["osm"]
        new.append({"name": name, "osm": osm_ref, "tags": {}, "geom": m["geom"], "zone": z,
                    "metro_ids": [m["metro_id"]], "metro": [m], "source_geoms": {"metro": m["geom"]},
                    "decisions": ["in Metro footprints but not in OSM" +
                                  (f" (OSM has only a point: {osm_ref})" if osm_ref else "")],
                    "new_kind": "metro"})
    known = [b["geom"] for b in blds] + [n["geom"] for n in new]
    for c in lidar_only_candidates(known, area_all, root):
        z = zone_of(c["geom"])
        if not z:
            continue
        new.append({"name": None, "osm": None, "tags": {}, "geom": c["geom"], "zone": z, "metro_ids": [],
                    "metro": [], "source_geoms": {"lidar_trace": c["geom"]},
                    "decisions": [f"LiDAR roof ({c['area']:.0f} m2, median {c['ndsm_median']:.1f} m above "
                                  "ground) with no OSM or Metro footprint; outline traced from the LiDAR mask"],
                    "new_kind": "lidar"})
    new.sort(key=lambda n: (round(n["geom"].centroid.x, 2), round(n["geom"].centroid.y, 2)))
    for i, n in enumerate(new, 1):
        n["id"] = f"B_new_{i}"
    return blds + new


# ---- step B: per building LiDAR comparison (worker processes) ------------

def edge_offsets(g, edge, others):
    """Per-edge LiDAR offsets for footprint g. + = LiDAR edge lies outside the footprint."""
    out = []
    for e in bc.edge_samples(g):
        s = e["samples"]
        off = -edge.signed(s)
        good = np.abs(off) < 3.0
        if others is not None:
            probe = s + e["normal"] * 0.6
            party = shapely.contains_xy(others, probe[:, 0], probe[:, 1])
            good &= ~party
        else:
            party = np.zeros(len(s), bool)
        under_tree = edge.is_veg(s + e["normal"] * 0.3) & (off < -EDGE_TOL)
        good &= ~under_tree
        e["off"], e["good"], e["party"], e["tree"] = off, good, party, under_tree
        out.append(e)
    return out


def lidar_score(edges, overhang=None):
    """Length-weighted mean |offset - overhang| over clean samples, plus the overhang used."""
    offs = np.concatenate([e["off"][e["good"]] for e in edges]) if edges else np.zeros(0)
    if len(offs) < 8:
        return None, None, 0
    if overhang is None:
        overhang = wall_overhang(edges)
        if overhang is None:
            overhang = float(np.clip(np.median(offs), 0.0, MAX_OVERHANG))
    return float(np.mean(np.abs(offs - overhang))), overhang, len(offs)


def wall_consistent(e):
    """A wall's LiDAR evidence is clean when most samples are usable and agree (IQR <= 0.5 m)."""
    g = e["good"]
    if g.sum() < 3 or g.mean() < MIN_GOOD_SHARE:
        return False
    q1, q3 = np.percentile(e["off"][g], [25, 75])
    return (q3 - q1) <= 0.5


def wall_overhang(edges):
    """Roof overhang = length-weighted median of the per-wall offsets of the clean walls
    (porches, stairs and trees only spoil some walls; they do not shift this number)."""
    meds, w = [], []
    for e in edges:
        if wall_consistent(e):
            meds.append(float(np.median(e["off"][e["good"]])))
            w.append(e["length"])
    if not meds:
        return None
    o = np.argsort(meds)
    cw = np.cumsum(np.asarray(w)[o])
    m = float(np.asarray(meds)[o][np.searchsorted(cw, cw[-1] / 2)])
    return float(np.clip(m, 0.0, MAX_OVERHANG))


def adjust_to_lidar(g, edges, overhang):
    """Move edges that are consistently > EDGE_TOL off the LiDAR edge (after overhang)."""
    moves = {}
    notes = []
    for e in edges:
        good = e["good"]
        if e["length"] < MIN_EDGE_FOR_MOVE or good.mean() < MIN_GOOD_SHARE or good.sum() < 3:
            continue
        r = e["off"][good] - overhang
        med = float(np.median(r))
        q1, q3 = np.percentile(r, [25, 75])
        if abs(med) <= EDGE_TOL or (q3 - q1) > 0.5 or np.mean(np.abs(r) > EDGE_TOL) < 0.6:
            continue
        moves[(e["poly"], e["ring"], e["edge"])] = med
        notes.append({"edge": [e["poly"], e["ring"], e["edge"]], "length_m": round(e["length"], 2),
                      "moved_m": round(med, 3), "direction": "outward" if med > 0 else "inward",
                      "from": [round(float(e["a"][0]), 2), round(float(e["a"][1]), 2)],
                      "to": [round(float(e["b"][0]), 2), round(float(e["b"][1]), 2)]})
    if not moves:
        return g, []
    polys = []
    for pi, p in enumerate(bc.polys_of(g)):
        p = bc.orient_ccw(p)
        rings = []
        for ri, r in enumerate([p.exterior] + list(p.interiors)):
            xy = bc.ring_xy(r)
            off = np.array([moves.get((pi, ri, i), 0.0) for i in range(len(xy))])
            rings.append(bc.offset_ring_edges(xy, off) if np.any(off) else xy)
        polys.append(Polygon(rings[0], rings[1:]))
    new = bc.clean_polygon(MultiPolygon(polys) if len(polys) > 1 else polys[0])
    if (new is None or not new.is_valid or abs(new.area - g.area) > 0.3 * g.area
            or len(bc.polys_of(new)) != len(bc.polys_of(g))):
        return g, [{"failed": True, "edges": notes}]
    return new, notes


def ground_of(pts, g, others):
    """Ground heights in a ring 1-3 m outside the footprint (widened to 8 m, then the DTM)."""
    gx, gy, gz = pts["x"], pts["y"], pts["z"]
    for r0, r1 in ((1.0, 3.0), (1.0, 8.0)):
        ringg = g.buffer(r1).difference(g.buffer(r0))
        if others is not None:
            ringg = ringg.difference(others)
        if ringg.is_empty:
            continue
        sel = (pts["classification"] == 2) & shapely.contains_xy(ringg, gx, gy)
        if sel.sum() >= 15:
            z = gz[sel]
            return {"ground_z": float(np.median(z)), "ground_min": float(np.percentile(z, 2)),
                    "n": int(sel.sum()), "method": f"LiDAR class 2, ring {r0:.0f}-{r1:.0f} m"}
    ringg = g.buffer(3.0).difference(g.buffer(1.0))
    xy = np.asarray(ringg.exterior.coords) if ringg.geom_type == "Polygon" else bc.corners(ringg)
    z = bc.dtm().sample(xy[:, 0], xy[:, 1])
    z = z[np.isfinite(z)]
    if not len(z):
        return {"ground_z": None, "ground_min": None, "n": 0, "method": "none"}
    return {"ground_z": float(np.median(z)), "ground_min": float(z.min()), "n": int(len(z)),
            "method": "DTM raster on the 3 m ring"}


def roof_height_estimate(pts, g, ground_z):
    """Rough eave / mean / top heights from the roof points (for the ring LoD1 list)."""
    if ground_z is None:
        return None
    inner = g.buffer(-0.5)
    if inner.is_empty:
        inner = g
    c = pts["classification"]
    sel = ((c == 6) | ((c == 1) & (pts["z"] > ground_z + 2.0))) & shapely.contains_xy(inner, pts["x"], pts["y"])
    if sel.sum() < 10:
        return None
    z = pts["z"][sel]
    return {"eave_z": float(np.percentile(z, 10)), "mean_z": float(np.mean(z)),
            "top_z": float(np.percentile(z, 98)), "n": int(sel.sum()),
            "density": float(sel.sum() / max(inner.area, 1.0))}


def process_building(b, pts, others):
    """Choose and adjust one footprint. Returns the record that goes into footprints.json."""
    g0 = b["geom"]
    bx = g0.bounds
    edge = bc.LidarEdge(pts, (bx[0] - 4, bx[1] - 4, bx[2] + 4, bx[3] + 4))
    dec = list(b.get("decisions", []))
    flags = []
    src = b["source_geoms"]
    cand = {}
    for name, g in src.items():
        s, removed = bc.simplify_footprint(g, 0.2)
        cand[name] = (s, removed)
    scores = {}
    for name, (g, _) in cand.items():
        sc, oh, n = lidar_score(edge_offsets(g, edge, others))
        scores[name] = {"mean_abs_residual_m": None if sc is None else round(sc, 3),
                        "overhang_m": None if oh is None else round(oh, 3), "samples": n}
    # choose a source
    if "osm" in src and "metro" in src:
        d1 = corner_dist(src["osm"], src["metro"])
        d2 = corner_dist(src["metro"], src["osm"])
        dd = np.concatenate([d1, d2])
        agree = float(np.percentile(dd, 95)) <= AGREE_M
        if agree:
            choice = "osm"
            dec.append(f"OSM and Metro agree (corner p95 {np.percentile(dd, 95):.2f} m): OSM used")
        else:
            so, sm = scores["osm"]["mean_abs_residual_m"], scores["metro"]["mean_abs_residual_m"]
            if so is None and sm is None:
                choice = "osm"
                dec.append(f"OSM and Metro differ (corner p95 {np.percentile(dd, 95):.2f} m) and the LiDAR "
                           "edge is not usable here: OSM used")
                flags.append("no_lidar_edge")
            elif sm is not None and (so is None or sm + 0.05 < so):
                choice = "metro"
                dec.append(f"OSM and Metro differ (corner p95 {np.percentile(dd, 95):.2f} m); Metro is closer "
                           f"to the LiDAR edge ({sm:.2f} vs {so if so is not None else float('nan'):.2f} m): Metro used")
            else:
                choice = "osm"
                dec.append(f"OSM and Metro differ (corner p95 {np.percentile(dd, 95):.2f} m); OSM is as close "
                           f"or closer to the LiDAR edge ({so:.2f} vs "
                           f"{sm if sm is not None else float('nan'):.2f} m): OSM used")
    else:
        choice = next(iter(src))
        if choice == "osm":
            dec.append("OSM only")
    g, removed = cand[choice]
    edges = edge_offsets(g, edge, others)
    sc, overhang, nsamp = lidar_score(edges)
    coverage = edge.coverage(g)
    adjusted = []
    source = choice
    if overhang is None:
        flags.append("no_lidar_edge")
        dec.append("too few clean LiDAR edge samples to check this outline")
        overhang = 0.0
    elif coverage < 0.2:
        flags.append("no_lidar_roof")
        dec.append(f"LiDAR roof covers only {coverage:.0%} of the footprint (new since 2022, demolished, "
                   "or not a roofed building): outline kept")
    else:
        if overhang > 0.15:
            dec.append(f"LiDAR roof edge lies a uniform {overhang:.2f} m outside the walls: logged as roof "
                       "overhang, not corrected")
        adjusted = []
        for _ in range(3):              # moving walls can change the overhang estimate: repeat
            g2, moved = adjust_to_lidar(g, edges, overhang)
            if moved and moved[0].get("failed"):
                flags.append("lidar_adjust_failed")
                dec.append("LiDAR edge adjustment produced an invalid outline: kept the outline so far")
                break
            if not moved:
                break
            g = g2
            adjusted += moved
            edges = edge_offsets(g, edge, others)
            _, oh2, _ = lidar_score(edges)
            overhang = oh2 if oh2 is not None else overhang
        if adjusted:
            source = choice + "+lidar"
            dec.append(f"{len(adjusted)} edge(s) more than {EDGE_TOL} m off the LiDAR edge moved to it: "
                       + "; ".join(f"{a['length_m']} m edge {a['direction']} {abs(a['moved_m']):.2f} m"
                                   for a in adjusted[:6]) + (" ..." if len(adjusted) > 6 else ""))
        edges = edge_offsets(g, edge, others)
    n_tree = int(sum(e["tree"].sum() for e in edges))
    n_party = int(sum(e["party"].sum() for e in edges))
    n_all = int(sum(len(e["off"]) for e in edges))
    if n_all and n_tree > 0.2 * n_all:
        flags.append("tree_cover")
    # corner errors
    c = bc.corners(g)
    cs = -edge.signed(c)
    if others is not None:
        party_c = shapely.distance(shapely.points(c), others) < 0.6 if len(c) else np.zeros(0, bool)
    else:
        party_c = np.zeros(len(c), bool)
    tree_c = edge.is_veg(c) & (cs < overhang - EDGE_TOL)
    use = ~party_c & ~tree_c & (np.abs(cs) < 3.0)
    errs = {"vs_osm": stats(corner_dist(g, src.get("osm"))) if "osm" in src else None,
            "vs_metro": stats(corner_dist(g, src.get("metro"))) if "metro" in src else None,
            "vs_lidar": stats(np.abs(cs[use] - overhang)) if use.sum() else None,
            "lidar_corners_used": int(use.sum()), "lidar_corners_total": int(len(c))}
    ground = ground_of(pts, g, others)
    rh = roof_height_estimate(pts, g, ground["ground_z"])
    rejected = None
    if b.get("new_kind") == "lidar":
        # a LiDAR-only "building" must have a planar roof; class 6 on tree crowns does not
        import roof_fit
        idx = roof_fit.roof_point_selection(pts, g, others, ground["ground_z"])
        P = np.c_[pts["x"][idx], pts["y"][idx], pts["z"][idx]]
        cov = roof_fit.segment(P, len(P) / max(g.area, 1.0))[2].get("coverage", 0.0) if len(P) >= 12 else 0.0
        if cov < 0.5:
            rejected = (f"LiDAR-only candidate rejected: only {cov:.0%} of its {len(P)} points lie on roof "
                        "planes (tree crowns or clutter classified as building)")
    m0 = b["metro"][0] if b.get("metro") else None
    t = b.get("tags", {})
    if t.get("building") in ("roof", "construction"):
        flags.append(f"osm_building_{t['building']}")
    cen = g.centroid
    rec = {
        "id": b["id"], "osm": b.get("osm"), "name": b.get("name"), "zone": b["zone"],
        "tile": fr.tile_of(cen.x, cen.y), "centroid": [round(cen.x, 3), round(cen.y, 3)],
        "area_m2": round(g.area, 2), "source": source, "decisions": dec, "flags": flags,
        "polygon": bc.geom_to_json(g),
        "sources": {k: bc.geom_to_json(v) for k, v in src.items()},
        "source_lidar_scores": scores,
        "metro_ids": b.get("metro_ids", []),
        "metro_height_m": round(m0["height_m"], 2) if m0 and m0["height_m"] else None,
        "metro_roof_type": m0["roof_type"] if m0 else None,
        "osm_tags": {k: t[k] for k in ("building", "building:levels", "height", "roof:shape", "roof:levels")
                     if k in t},
        "overhang_m": round(overhang, 3), "lidar_coverage": round(coverage, 3),
        "lidar_edge_samples": {"total": n_all, "shared_wall": n_party, "under_trees": n_tree},
        "adjusted_edges": adjusted,
        "removed_jogs": [{"xy": [round(x, 3), round(y, 3)], "deviation_m": round(d, 3)}
                         for x, y, d in removed if d > 0.05],
        "corner_errors": errs,
        "ground": {k: (round(v, 3) if isinstance(v, float) else v) for k, v in ground.items()},
        "roof_estimate": None if rh is None else {k: (round(v, 3) if isinstance(v, float) else v)
                                                  for k, v in rh.items()},
    }
    if rejected:
        rec["rejected"] = rejected
    return rec


def run_job(job):
    """One tile: load its points once, process its buildings."""
    tile, items, neighbors = job
    xs = [it["geom"].bounds for it in items]
    xmin = min(b[0] for b in xs) - 10
    ymin = min(b[1] for b in xs) - 10
    xmax = max(b[2] for b in xs) + 10
    ymax = max(b[3] for b in xs) + 10
    pts = lidar_io.load_points(xmin, ymin, xmax, ymax)
    out, errors = [], []
    for it in items:
        others = [g for nid, g in neighbors if nid != it["id"] and g.intersects(it["geom"].buffer(10))]
        others = shapely.unary_union(others) if others else None
        if others is not None:
            shapely.prepare(others)
        try:
            out.append(process_building(it, pts, others))
        except Exception as ex:  # keep going; the failure is reported
            import traceback
            errors.append({"id": it["id"], "error": repr(ex), "trace": traceback.format_exc()})
    return tile, out, errors


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--jobs", type=int, default=14, help="worker processes")
    p.add_argument("--only", nargs="*", help="only these building ids (debugging; writes nothing)")
    args = p.parse_args(argv)
    t0 = time.time()
    root = data_root()
    log = []
    blds = gather(root, log)
    print(f"{len(blds)} buildings in campus + ring ({sum(b['zone'] == 'campus' for b in blds)} campus)")
    if args.only:
        blds = [b for b in blds if b["id"] in set(args.only)]
    by_tile = {}
    for b in blds:
        c = b["geom"].centroid
        by_tile.setdefault(fr.tile_of(c.x, c.y), []).append(b)
    allg = [(b["id"], b["geom"]) for b in blds] if not args.only else \
        [(b["id"], b["geom"]) for b in gather(root, [])]
    tree = STRtree([g for _, g in allg])
    jobs = []
    for tile, items in sorted(by_tile.items()):
        bx = shapely.unary_union([it["geom"] for it in items]).buffer(12).envelope
        nb = [allg[int(k)] for k in tree.query(bx)]
        clean = [{k: v for k, v in it.items()} for it in items]
        jobs.append((tile, clean, nb))
    records, errors = [], []
    if args.jobs > 1 and len(jobs) > 1:
        with Pool(args.jobs) as pool:
            for tile, out, err in pool.imap_unordered(run_job, jobs):
                records += out
                errors += err
    else:
        for j in jobs:
            _, out, err = run_job(j)
            records += out
            errors += err
    records.sort(key=lambda r: r["id"])
    rejected = [r for r in records if r.get("rejected")]
    records = [r for r in records if not r.get("rejected")]
    for r in rejected:
        log.append(f"{r['id']} dropped: {r['rejected']}")
    for e in errors:
        print(f"ERROR {e['id']}: {e['error']}\n{e['trace']}", file=sys.stderr)
    if args.only:
        for r in records:
            print(bc.dumps({k: r[k] for k in ("id", "name", "source", "decisions", "flags", "corner_errors",
                                               "overhang_m", "lidar_coverage", "ground", "roof_estimate")}))
        return 1 if errors else 0
    # summary
    from collections import Counter
    summary = {
        "buildings": len(records),
        "by_zone": dict(Counter(r["zone"] for r in records)),
        "by_source": {z: dict(Counter(r["source"] for r in records if r["zone"] == z)) for z in ("campus", "ring")},
        "new": dict(Counter(("metro" if "metro" in r["sources"] else "lidar_trace") for r in records
                            if r["id"].startswith("B_new_"))),
        "flags": dict(Counter(f for r in records for f in r["flags"])),
        "lidar_only_rejected": len(rejected),
        "errors": [e["id"] for e in errors],
    }
    od = bc.out_dir(root)
    fp = bc.save(od / "footprints.json", {
        "version": 1, "frame": "campus meters (config/frame.json)", "made": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "script": "Tools/VanderbiltCampus/pipeline/buildings_footprints.py",
        "rules": {"agree_m": AGREE_M, "edge_tolerance_m": EDGE_TOL, "max_overhang_m": MAX_OVERHANG,
                  "min_edge_moved_m": MIN_EDGE_FOR_MOVE, "simplify_m": 0.2,
                  "lidar_edge": "class 6 + class 1 > 2 m above DTM, 0.25 m grid, closed 1 cell, holes < 4 m2 filled"},
        "summary": summary, "log": log, "buildings": records})
    ring = []
    for r in records:
        if r["zone"] != "ring":
            continue
        g = r["ground"]["ground_z"]
        est = r["roof_estimate"]
        if est:
            eave, top, how = est["eave_z"], est["top_z"], "LiDAR roof points (p10 / p98)"
        else:
            h = r["metro_height_m"] or 8.0
            eave, top = (g or 0.0) + h * 0.85, (g or 0.0) + h
            how = "Metro Height" if r["metro_height_m"] else "default 8 m (no LiDAR roof, no Metro height)"
        ring.append({"id": r["id"], "name": r["name"], "tile": r["tile"], "polygon": r["polygon"],
                     "ground_z": g, "base_z": None if r["ground"]["ground_min"] is None
                     else round(r["ground"]["ground_min"] - 0.5, 3),
                     "eave_z": round(eave, 3), "top_z": round(top, 3),
                     "mean_roof_z": est["mean_z"] if est else round(eave, 3),
                     "height_source": how, "footprint_source": r["source"], "flags": r["flags"]})
    rp = bc.save(od / "ring_footprints.json", {
        "version": 1, "frame": "campus meters (config/frame.json)",
        "note": "Backdrop ring buildings for LoD1 boxes. polygon = list of [exterior, holes...]; Z values in "
                "campus meters; base_z = lowest ground 1-3 m outside minus 0.5 m.",
        "buildings": ring})
    db = manifest.connect(root)
    for f in (fp, rp):
        manifest.add_file(f, source="buildings_footprints.py", license="derived (OSM ODbL, Metro open data, "
                          "USGS LiDAR public domain)", stage=1, root=root, db=db)
    db.commit()
    db.close()
    print(bc.dumps(summary))
    print(f"wrote {fp} and {rp} in {time.time() - t0:.0f} s")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
