"""Stage 1 accuracy gate for the LoD2 buildings (STANDARDS.md section 5, STAGE-1 "Done when" 2).

Per campus building (needs buildings_lod2.py output):

Footprint
  - vs reference: every model footprint corner's distance to the chosen reference. For
    corners kept from OSM/Metro the reference is that outline; for corners moved to the
    LiDAR edge (source "...+lidar") and LiDAR-traced footprints it is the LiDAR edge
    (minus the building's roof overhang). median / p95 / max.
  - vs LiDAR (informative): every corner with clean evidence (not on a shared wall, not
    under trees): |distance to the LiDAR edge - overhang|.
Heights, per part (parts >= 4 m2)
  - eave: model eave (lowest roof edge on the footprint outline) vs the 5th-10th percentile
    band of the roof points within 0.75 m inside the part's eave edges (outline edges at the
    eave height). Points within 0.4 m of the part's inner borders (height steps to other parts)
    are not used for either band.
  - ridge: model ridge (highest roof point) vs the 98th-99.5th percentile band of the
    roof points within 0.75 m of the ridge lines (pitched) or over the whole part (flat).
  - error = 0 inside the band, else the distance to the nearer band end.
  - band points are this part's roof points: within 1.5 m (vertically) of the model roof, so
    facade/ground points near the outline and tree crowns above do not count; gross misfits
    are caught by the "fit" test below instead.
  - points over features merged into the ledger (roof equipment, chimneys, parapets: more
    than 0.3 m above the model and inside the ledger entry's extent) are left out of the
    eave/ridge bands (they are listed in the ledger instead); they still count in the
    roof-surface error.
Roof surface: vertical distance from all roof points to the model roof (median, p95).
Facade points (local surface steeper than about 70 degrees, from 10-neighbor PCA normals) are
not roof points and are left out of every height test; their count is reported.

A building passes when footprint p95 (vs reference) <= 0.3 m and every checked part has
|eave| and |ridge| errors <= 0.2 m and at least 75% of its roof points (facade points and
ledgered features left out) within 0.5 m of the model roof ("fit": catches gross misfits that
the bands, which only use points within 1.5 m of the model, would not see).

--version v2 checks the v2 buildings (buildings_v2.py) with the same gate. Buildings that kept
their v1 roof planes ("planes_v1") are checked exactly as above. Height-field buildings
("heightfield_v2") have no roof planes: their parts are the height field's smooth regions; the
model roof height is read from the LoD2 mesh (rays cast straight down); eave samples are the
part's edge on the footprint outline whose model height is within 0.3 m of the part's eave,
ridge samples are model points within 0.15 m of the part's ridge. The LiDAR bands are computed
from the raw points exactly as for v1 (never from the model's own raster).
Every building also gets an independent whole-roof metric: vertical distance of all its roof
points (inside the footprint, facade points left out) to the LoD2 mesh (median, p90, p95,
share within 0.2 m). v2 writes reports/stage1/buildings_accuracy_v2.json/.md and replaces
Docs/.../progress/stage1-buildings-accuracy.md (keeping a short v1 paragraph at the top).

Writes reports/stage1/buildings_accuracy.json, reports/stage1/buildings_accuracy.md (also
copied to Docs/Development/VanderbiltCampus/progress/stage1-buildings-accuracy.md), and the
numbers into each model.json (archive and git mirror). Exit code 1 if the pass rate is
below --target (default 95%).
"""
import argparse
import sys
import time
from collections import Counter
from multiprocessing import Pool
from pathlib import Path

import numpy as np
import shapely
from shapely.geometry import LineString, Point

sys.path.insert(0, str(Path(__file__).resolve().parent))
import buildings_common as bc  # noqa: E402
import lidar_io  # noqa: E402
import manifest  # noqa: E402
import roof_fit as rf  # noqa: E402
from common import REPO, data_root, fail, load_json  # noqa: E402

FP_TOL, H_TOL = 0.30, 0.20
FIT_SHARE = 0.75        # a part also needs 75% of its roof points within 0.5 m of the model
DOCS = REPO / "Docs" / "Development" / "VanderbiltCampus" / "progress"


def st(d):
    d = np.asarray(d, float)
    d = d[np.isfinite(d)]
    if not len(d):
        return None
    return {"median": round(float(np.median(d)), 3), "p95": round(float(np.percentile(d, 95)), 3),
            "max": round(float(d.max()), 3), "n": int(len(d))}


def band_err(model, lo, hi):
    if model < lo:
        return model - lo
    if model > hi:
        return model - hi
    return 0.0


def footprint_check(rec, fp, pts, others):
    """Corner errors vs the reference and vs the LiDAR edge.

    A corner's LiDAR error is measured on its two walls: the median offset of the LiDAR edge
    from each wall over its clean samples (every 0.25 m, 0.4 m corner tips skipped because the
    0.25 m LiDAR mask rounds them; porches, stairs and trees covering less than half a wall do
    not move the median), minus the building's roof overhang; the larger of the two is the
    corner error. This is the same per-wall measure buildings_footprints.py uses to move walls.
    Walls whose LiDAR evidence is not clean (less than half the samples usable, or samples
    spread over more than 0.5 m IQR) give no evidence; a corner with no clean wall is counted
    in corners_without_clean_evidence and keeps its distance to the source outline."""
    bx = fp.bounds
    edge = bc.LidarEdge(pts, (bx[0] - 4, bx[1] - 4, bx[2] + 4, bx[3] + 4))
    oh = rec["overhang_m"]
    E = bc.edge_samples(fp)
    per_edge = []
    for e in E:
        s = e["samples"]
        off = -edge.signed(s)
        good = np.abs(off) < 3.0
        if others is not None:
            probe = s + e["normal"] * 0.6
            good &= ~shapely.contains_xy(others, probe[:, 0], probe[:, 1])
        good &= ~(edge.is_veg(s + e["normal"] * 0.3) & (off < oh - FP_TOL))
        t = np.hypot(*(s - e["a"]).T)
        per_edge.append((off, good, t, e["length"]))
    corners, lid = [], []
    groups = {}
    for k, e in enumerate(E):
        groups.setdefault((e["poly"], e["ring"]), []).append(k)
    for ks in groups.values():
        for n, k in enumerate(ks):
            prev = ks[n - 1]
            errs = []
            for kk in (k, prev):                          # the two walls meeting at this corner
                off, good, t, L = per_edge[kk]
                if good.sum() >= 3 and good.mean() >= 0.5:
                    q1, q3 = np.percentile(off[good], [25, 75])
                    if q3 - q1 <= 0.5:          # same "clean wall" rule as buildings_footprints
                        errs.append(abs(float(np.median(off[good])) - oh))
            corners.append(E[k]["a"])
            lid.append(max(errs) if errs else np.nan)
    c = np.asarray(corners)
    lid = np.asarray(lid)
    clean = np.isfinite(lid)
    src = rec["source"].split("+")[0]
    ref_geom = bc.geom_from_json(rec["sources"][src]) if src in rec["sources"] else None
    if src == "lidar_trace" or ref_geom is None:
        ref = lid
        moved = np.ones(len(c), bool)
    else:
        d_src = shapely.distance(shapely.points(c), ref_geom.boundary)
        moved = d_src > 0.05
        ref = np.where(moved, np.where(clean, lid, d_src), d_src)
    return {"vs_reference": st(ref), "vs_lidar": st(lid[clean]), "corners": int(len(c)),
            "corners_moved_to_lidar": int(moved.sum()), "corners_with_lidar_evidence": int(clean.sum()),
            "corners_without_clean_evidence": int((~clean).sum()),
            "reference": ("LiDAR edge" if src == "lidar_trace" else f"{src} outline; LiDAR edge for moved corners")}


def part_lines(cells, planes, members, part_poly, eave, ridge, outline):
    """Eave edges (part outline at eave height, on the footprint outline when the part touches
    it) and ridge lines (cell edges at ridge height)."""
    eaves, ridges = [], []
    ext = []
    touches = part_poly.boundary.intersection(outline.buffer(0.01)).length > 0.5
    for p in bc.polys_of(part_poly):
        ext.append(np.asarray(p.exterior.coords))
    for xy in ext:
        for a, b in zip(xy[:-1], xy[1:]):
            m = (a + b) / 2
            z = None
            for k in members:
                c, l = cells[k]
                if c.distance(Point(m)) < 1e-3:
                    za, zb = planes[l].z(a[0], a[1]), planes[l].z(b[0], b[1])
                    z = max(za, zb) if z is None else min(z, max(za, zb))
            if touches and outline.distance(Point(m)) > 0.01:
                continue
            if z is not None and z - eave < 0.3 and np.hypot(*(b - a)) > 0.3:
                eaves.append(LineString([a, b]))
    for k in members:
        c, l = cells[k]
        xy = np.asarray(c.exterior.coords)
        z = planes[l].z(xy[:, 0], xy[:, 1])
        for i in range(len(xy) - 1):
            if ridge - z[i] < 0.15 and ridge - z[i + 1] < 0.15 and np.hypot(*(xy[i + 1] - xy[i])) > 0.2:
                ridges.append(LineString([xy[i], xy[i + 1]]))
        top = int(np.argmax(z))
        if ridge - z[top] < 0.15:
            ridges.append(Point(xy[top]))
    return eaves, ridges


def check_building(model, rec, planes_json, ledger, pts, others):
    bid = model["building"]
    fp = bc.geom_from_json(rec["polygon"])
    out = {"building": bid, "name": model.get("name"), "roof_type": model["roof_type"],
           "area_m2": rec["area_m2"], "flags": model["flags"], "source": rec["source"]}
    out["footprint"] = footprint_check(rec, fp, pts, others)
    cells = [(bc.geom_from_json(c["polygon"]), c["plane"]) for c in planes_json["cells"]]
    planes = []
    for p in planes_json["planes"]:
        pl = rf.Plane(p["a"], p["b"], p["c"])
        pl.zlo, pl.zhi = p.get("zlo"), p.get("zhi")
        planes.append(pl)
    idx = rf.roof_point_selection(pts, fp, others, model["ground_z"])
    P = np.c_[pts["x"][idx], pts["y"][idx], pts["z"][idx]]
    inside = shapely.contains_xy(fp, P[:, 0], P[:, 1])
    if len(P) >= 10:
        N, _, _ = rf.normals(P, 10)
        wall = np.abs(N[:, 2]) < 0.35          # facade points (steeper than ~70 deg) are not roof
    else:
        wall = np.zeros(len(P), bool)
    out["wall_points_dropped"] = int((wall & inside).sum())
    P = P[inside & ~wall]
    zm = rf.model_z(cells, planes, P[:, 0], P[:, 1])
    res = P[:, 2] - zm
    ok = np.isfinite(res)
    out["roof_surface"] = st(np.abs(res[ok]))
    out["roof_points"] = int(ok.sum())
    out["roof_points_per_m2"] = round(float(ok.sum()) / max(fp.area, 1.0), 2)
    # points over ledgered features are left out of the eave / ridge bands
    feat = np.zeros(len(P), bool)
    for e in ledger["entries"]:
        if e["type"] == "footprint_jog":
            continue
        r = float(np.hypot(e["size"][0], e["size"][1])) / 2 + 0.3
        near = np.hypot(P[:, 0] - e["center"][0], P[:, 1] - e["center"][1]) < r
        feat |= near & (res > 0.3)
    parts = []
    pts2 = shapely.points(P[:, :2])
    for part in model["parts"]:
        pp = bc.geom_from_json(part["polygon"])
        row = {"id": part["id"], "area_m2": part["area_m2"], "roof_type": part["roof_type"],
               "eave_z": part["eave_z"], "ridge_z": part["ridge_z"]}
        if part["area_m2"] < 4.0:
            row["checked"] = False
            row["why_not"] = "part under 4 m2"
            parts.append(row)
            continue
        members = [k for k, (c, l) in enumerate(cells) if c.representative_point().within(pp.buffer(1e-6))]
        eaves, ridges = part_lines(cells, planes, members, pp, part["eave_z"], part["ridge_z"], fp.boundary)
        # stay 0.4 m away from the part's inner borders (height steps to other parts), so points
        # of a neighbor part never land in this part's bands
        inner = pp.boundary.difference(fp.boundary.buffer(0.01))
        inpart = shapely.contains_xy(pp, P[:, 0], P[:, 1])
        okr = inpart & np.isfinite(res)
        if not inner.is_empty:
            okr &= shapely.distance(pts2, inner) >= 0.4
        row["fit_share"] = round(float(np.mean(np.abs(res[okr & ~feat]) <= 0.5)), 3) if (okr & ~feat).sum() else None
        # band points: roof points of this part (within 1.5 m of the model roof; facade and
        # ground points near the outline and tree crowns above are not roof)
        inp = inpart & ~feat & okr & (np.abs(res) <= 1.5)
        Pin = P[inp]
        din = pts2[inp]

        def near(geoms, r):
            if not geoms or not len(Pin):
                return np.zeros(len(Pin), bool)
            g = shapely.unary_union(geoms)
            return shapely.distance(din, g) <= r
        e_sel = near(eaves, 0.75)
        if e_sel.sum() < 10:
            e_sel = near(eaves, 1.5)
        flat = part["roof_type"] in ("flat", "roof_volume", "fallback_flat") or (
            part["ridge_z"] - part["eave_z"] < 0.5)
        r_sel = np.ones(len(Pin), bool) if flat else near(ridges, 0.75)
        if r_sel.sum() < 10 and not flat:
            r_sel = near(ridges, 1.5)
        if e_sel.sum() >= 10:
            lo, hi = np.percentile(Pin[e_sel, 2], [5, 10])
            row["lidar_eave_band"] = [round(float(lo), 3), round(float(hi), 3)]
            row["eave_err_m"] = round(band_err(part["eave_z"], lo, hi), 3)
            row["eave_diff_to_p7.5_m"] = round(part["eave_z"] - float(np.percentile(Pin[e_sel, 2], 7.5)), 3)
            row["eave_points"] = int(e_sel.sum())
        if r_sel.sum() >= 10:
            lo, hi = np.percentile(Pin[r_sel, 2], [98, 99.5])
            row["lidar_ridge_band"] = [round(float(lo), 3), round(float(hi), 3)]
            row["ridge_err_m"] = round(band_err(part["ridge_z"], lo, hi), 3)
            row["ridge_points"] = int(r_sel.sum())
        row["checked"] = "eave_err_m" in row and "ridge_err_m" in row
        if row["checked"] and row["fit_share"] is not None and row["fit_share"] < FIT_SHARE:
            row["fit_fail"] = True
        if not row["checked"]:
            row["why_not"] = "fewer than 10 LiDAR points near the eave or ridge"
        parts.append(row)
    return finish(out, parts, model, rec)


def whole_roof(P, mz):
    """Vertical distance of roof points to the mesh (the independent whole-roof metric)."""
    if not len(P):
        return None
    d = np.abs(P[:, 2] - mz(P[:, 0], P[:, 1]))
    d = d[np.isfinite(d)]
    if not len(d):
        return None
    return {"n": int(len(d)), "median": round(float(np.median(d)), 3), "p90": round(float(np.percentile(d, 90)), 3),
            "p95": round(float(np.percentile(d, 95)), 3), "within_0.2": round(float(np.mean(d <= 0.2)), 4),
            "n_within_0.2": int((d <= 0.2).sum())}


def roof_points_checked(model, rec, pts, others):
    """Roof points used by every height test: v1 selection, inside the footprint, facade left out."""
    fp = bc.geom_from_json(rec["polygon"])
    idx = rf.roof_point_selection(pts, fp, others, model["ground_z"])
    P = np.c_[pts["x"][idx], pts["y"][idx], pts["z"][idx]]
    inside = shapely.contains_xy(fp, P[:, 0], P[:, 1])
    if len(P) >= 10:
        N, _, _ = rf.normals(P, 10)
        wall = np.abs(N[:, 2]) < 0.35
    else:
        wall = np.zeros(len(P), bool)
    return P[inside & ~wall], int((wall & inside).sum())


def check_building_hf(model, rec, ledger, pts, others, mesh):
    """The gate for a height-field (v2) building; same tests and bands as check_building."""
    import buildings_v2 as b2
    bid = model["building"]
    fp = bc.geom_from_json(rec["polygon"])
    out = {"building": bid, "name": model.get("name"), "roof_type": model["roof_type"],
           "area_m2": rec["area_m2"], "flags": model["flags"], "source": rec["source"],
           "roof_method": model.get("roof_method")}
    out["footprint"] = footprint_check(rec, fp, pts, others)
    mz = b2.MeshZ(mesh)
    P, nwall = roof_points_checked(model, rec, pts, others)
    out["wall_points_dropped"] = nwall
    zm = mz(P[:, 0], P[:, 1]) if len(P) else np.zeros(0)
    res = P[:, 2] - zm
    ok = np.isfinite(res)
    out["roof_surface"] = st(np.abs(res[ok]))
    out["whole_roof"] = whole_roof(P, mz)
    out["roof_points"] = int(ok.sum())
    out["roof_points_per_m2"] = round(float(ok.sum()) / max(fp.area, 1.0), 2)
    feat = np.zeros(len(P), bool)
    for e in ledger["entries"]:
        if e["type"] == "footprint_jog":
            continue
        r = float(np.hypot(e["size"][0], e["size"][1])) / 2 + 0.3
        near = np.hypot(P[:, 0] - e["center"][0], P[:, 1] - e["center"][1]) < r
        feat |= near & (res > 0.3)
    parts = []
    pts2 = shapely.points(P[:, :2])
    outline = fp.boundary
    for part in model["parts"]:
        pp = bc.geom_from_json(part["polygon"])
        row = {"id": part["id"], "area_m2": part["area_m2"], "roof_type": part["roof_type"],
               "eave_z": part["eave_z"], "ridge_z": part["ridge_z"]}
        if part["area_m2"] < 4.0:
            row["checked"] = False
            row["why_not"] = "part under 4 m2"
            parts.append(row)
            continue
        ring = b2.ring_samples(pp)
        zr = mz(ring[:, 0], ring[:, 1]) if len(ring) else np.zeros(0)
        on = shapely.distance(shapely.points(ring), outline) < 0.1 if len(ring) else np.zeros(0, bool)
        sel = on if on.any() else np.ones(len(ring), bool)
        eav = ring[sel & (np.nan_to_num(zr, nan=1e9) - part["eave_z"] < 0.3)]
        allp = b2.ridge_samples(pp)           # 0.3 m inside the part, as the model's ridge
        zall = mz(allp[:, 0], allp[:, 1])
        rid = allp[np.nan_to_num(zall, nan=-1e9) >= part["ridge_z"] - 0.15]
        inner = pp.boundary.difference(fp.boundary.buffer(0.01))
        inpart = shapely.contains_xy(pp, P[:, 0], P[:, 1])
        okr = inpart & np.isfinite(res)
        if not inner.is_empty:
            okr &= shapely.distance(pts2, inner) >= 0.4
        row["fit_share"] = round(float(np.mean(np.abs(res[okr & ~feat]) <= 0.5)), 3) if (okr & ~feat).sum() else None
        inp = inpart & ~feat & okr & (np.abs(res) <= 1.5)
        Pin = P[inp]
        din = pts2[inp]

        def near(xy, r):
            if not len(xy) or not len(Pin):
                return np.zeros(len(Pin), bool)
            from scipy.spatial import cKDTree
            d, _ = cKDTree(xy).query(Pin[:, :2], distance_upper_bound=r + 1e-9)
            return np.isfinite(d)
        e_sel = near(eav, 0.75)
        if e_sel.sum() < 10:
            e_sel = near(eav, 1.5)
        # a height-field region can hold several levels joined by ramps or slopes: it counts as
        # flat (ridge band over the whole part) only when its own relief is under 0.5 m
        flat = part["ridge_z"] - part["eave_z"] < 0.5
        r_sel = np.ones(len(Pin), bool) if flat else near(rid, 0.75)
        if r_sel.sum() < 10 and not flat:
            r_sel = near(rid, 1.5)
        if e_sel.sum() >= 10:
            lo, hi = np.percentile(Pin[e_sel, 2], [5, 10])
            row["lidar_eave_band"] = [round(float(lo), 3), round(float(hi), 3)]
            row["eave_err_m"] = round(band_err(part["eave_z"], lo, hi), 3)
            row["eave_diff_to_p7.5_m"] = round(part["eave_z"] - float(np.percentile(Pin[e_sel, 2], 7.5)), 3)
            row["eave_points"] = int(e_sel.sum())
        if r_sel.sum() >= 10:
            lo, hi = np.percentile(Pin[r_sel, 2], [98, 99.5])
            row["lidar_ridge_band"] = [round(float(lo), 3), round(float(hi), 3)]
            row["ridge_err_m"] = round(band_err(part["ridge_z"], lo, hi), 3)
            row["ridge_points"] = int(r_sel.sum())
        row["checked"] = "eave_err_m" in row and "ridge_err_m" in row
        if row["checked"] and row["fit_share"] is not None and row["fit_share"] < FIT_SHARE:
            row["fit_fail"] = True
        if not row["checked"]:
            row["why_not"] = "fewer than 10 LiDAR points near the eave or ridge"
        parts.append(row)
    return finish(out, parts, model, rec)


def finish(out, parts, model, rec):
    """Pass/fail and summary numbers of one building from its part rows (shared by v1 and v2)."""
    out["parts"] = parts
    checked = [p for p in parts if p.get("checked")]
    fpr = out["footprint"]["vs_reference"]
    fp_ok = fpr is None or fpr["p95"] <= FP_TOL
    eave_ok = all(abs(p["eave_err_m"]) <= H_TOL for p in checked)
    out["pass_fit"] = all(not p.get("fit_fail") for p in checked)
    ridge_ok = all(abs(p["ridge_err_m"]) <= H_TOL for p in checked)
    main = max(checked, key=lambda p: p["area_m2"]) if checked else None
    out["eave_err_m"] = None if main is None else main["eave_err_m"]
    out["ridge_err_m"] = None if main is None else main["ridge_err_m"]
    out["worst_eave_err_m"] = max((abs(p["eave_err_m"]) for p in checked), default=None)
    out["worst_ridge_err_m"] = max((abs(p["ridge_err_m"]) for p in checked), default=None)
    out["footprint_ref_p95_m"] = None if fpr is None else fpr["p95"]
    out["checked_parts"] = len(checked)
    out["pass_footprint"], out["pass_eave"], out["pass_ridge"] = fp_ok, eave_ok, ridge_ok
    out["pass"] = bool(fp_ok and eave_ok and ridge_ok and out["pass_fit"] and checked)
    out["pass_main_part"] = bool(main is not None and fp_ok and abs(main["eave_err_m"]) <= H_TOL
                                 and abs(main["ridge_err_m"]) <= H_TOL and not main.get("fit_fail"))
    big = [p for p in checked if p["area_m2"] >= 50.0]
    out["pass_parts_50m2"] = bool(checked and fp_ok and all(abs(p["eave_err_m"]) <= H_TOL and abs(p["ridge_err_m"]) <= H_TOL
                                                            and not p.get("fit_fail") for p in big))
    out["parts_checked_failing"] = sum(1 for p in checked if abs(p["eave_err_m"]) > H_TOL or
                                       abs(p["ridge_err_m"]) > H_TOL or p.get("fit_fail"))
    if not checked:
        out["pass"] = False
    out["reason"] = None if out["pass"] else reason(out, model, rec)
    return out


def reason(out, model, rec):
    """Best guess at why a building fails (from its flags and which test failed)."""
    flags = set(model["flags"])
    why = []
    if "no_lidar_roof" in flags:
        why.append("no LiDAR roof over most of the footprint (built after the 2022 LiDAR, demolished, or "
                   "an open structure)")
    if "fallback_flat" in flags:
        why.append("flat fallback: " + (model["roof_fit"].get("fallback") or ""))
    if "curved" in flags:
        why.append("curved roof (dome/vault) approximated by facets")
    if "tree_cover" in flags:
        why.append("trees over parts of the roof edge")
    if out.get("roof_points_per_m2", 99) < 4:
        why.append(f"thin LiDAR coverage ({out['roof_points_per_m2']} pts/m2: glass roof or occlusion)")
    if not out.get("checked_parts"):
        why.append("no part had enough LiDAR points to check")
    if not out["pass_footprint"]:
        fr = out["footprint"]["vs_reference"]
        why.append(f"footprint p95 {fr['p95']} m vs reference ({out['footprint']['corners_moved_to_lidar']} "
                   "corners moved to an irregular LiDAR edge)")
    bad = [p for p in out["parts"] if p.get("checked") and (abs(p["eave_err_m"]) > H_TOL or
                                                            abs(p["ridge_err_m"]) > H_TOL or p.get("fit_fail"))]
    for p in bad[:3]:
        why.append(f"{p['id']} ({p['roof_type']}, {p['area_m2']:.0f} m2): eave {p['eave_err_m']:+.2f} m, "
                   f"ridge {p['ridge_err_m']:+.2f} m, {p['fit_share']:.0%} of roof points within 0.5 m")
    if len(bad) > 3:
        why.append(f"... {len(bad) - 3} more parts over 0.2 m")
    if model.get("metro_roof_type") and model["metro_roof_type"] not in ("Flat",) and model["roof_type"] == "flat":
        why.append(f"Metro says roof type {model['metro_roof_type']}")
    return "; ".join(why) or "unknown"


def mesh_checks_v2(bid, root):
    """v2: the single non-convex collider (col_mesh.npz) must have no degenerate or duplicate
    triangles, no vertices closer than 1 mm and consistent winding; LoD2/LoD1 watertight."""
    import buildings_v2 as b2
    import vcmesh
    d = bc.building_dir(bid, root, "v2")
    out = {"colliders": 1, "bad_colliders": [], "lod_problems": {}}
    c = vcmesh.load_mesh(d / "col_mesh.npz")
    pr = b2.collider_problems(c)
    out["col_mesh_triangles"] = int(len(c["faces"]))
    if pr:
        out["bad_colliders"].append({"file": "col_mesh.npz", "problems": pr})
    for name in ("lod2", "lod1"):
        m = vcmesh.load_mesh(d / f"{name}.npz")
        probs = vcmesh.check_mesh(m, need_watertight=True)
        F = m["faces"]
        if len(np.unique(np.sort(F, axis=1), axis=0)) < len(F):
            probs.append("duplicate triangles")
        if probs:
            out["lod_problems"][name] = probs
    return out


def mesh_checks(bid, root):
    """Colliders: each col_<n>.npz must be a real convex solid for PhysX (scipy ConvexHull
    volume >= 0.1 m3, oriented box >= 0.3 m in every direction, no duplicate vertices, no
    degenerate or duplicate triangles). LoD2/LoD1: watertight, no degenerate/duplicate triangles."""
    import trimesh
    from scipy.spatial import ConvexHull
    import vcmesh
    d = bc.building_dir(bid, root)
    out = {"colliders": 0, "bad_colliders": [], "lod_problems": {}}
    for f in sorted(d.glob("col_*.npz")):
        m = vcmesh.load_mesh(f)
        v, F = m["verts"], m["faces"]
        out["colliders"] += 1
        why = []
        try:
            vol = ConvexHull(v).volume
        except Exception:
            vol = 0.0
        if vol < 0.1:
            why.append(f"volume {vol:.3f} m3")
        ext = trimesh.Trimesh(v, F, process=False).bounding_box_oriented.primitive.extents
        if ext.min() < 0.3:
            why.append(f"thickness {ext.min():.3f} m")
        if len(np.unique(np.round(v, 6), axis=0)) < len(v):
            why.append("duplicate vertices")
        area = np.linalg.norm(np.cross(v[F[:, 1]] - v[F[:, 0]], v[F[:, 2]] - v[F[:, 0]]), axis=1) / 2
        if (area < 1e-8).any():
            why.append("degenerate triangles")
        if len(np.unique(np.sort(F, axis=1), axis=0)) < len(F):
            why.append("duplicate triangles")
        if why:
            out["bad_colliders"].append({"file": f.name, "problems": why})
    for name in ("lod2", "lod1"):
        m = vcmesh.load_mesh(d / f"{name}.npz")
        probs = vcmesh.check_mesh(m, need_watertight=True)
        F = m["faces"]
        if len(np.unique(np.sort(F, axis=1), axis=0)) < len(F):
            probs.append("duplicate triangles")
        if probs:
            out["lod_problems"][name] = probs
    return out


def run_job(job):
    tile, items, neighbors, root, version = job
    import buildings_v2 as b2
    import vcmesh
    gs = [bc.geom_from_json(r["polygon"]) for _, r in items]
    b = np.array([g.bounds for g in gs])
    pts = lidar_io.load_points(b[:, 0].min() - 8, b[:, 1].min() - 8, b[:, 2].max() + 8, b[:, 3].max() + 8,
                               root=root)
    out = []
    for (bid, rec), g in zip(items, gs):
        d = bc.building_dir(bid, root, version)
        try:
            model = load_json(d / "model.json")
            planes_json = load_json(d / "roof_planes.json")
            ledger = load_json(d / "ledger.json")
            others = [ng for nid, ng in neighbors if nid != bid and ng.intersects(g.buffer(8))]
            others = shapely.unary_union(others) if others else None
            mesh = vcmesh.load_mesh(d / "lod2.npz")
            if version == "v2" and model.get("roof_method") == "heightfield_v2":
                r = check_building_hf(model, rec, ledger, pts, others, mesh)
            else:
                r = check_building(model, rec, planes_json, ledger, pts, others)
                r["roof_method"] = model.get("roof_method", "planes_v1")
                P, _ = roof_points_checked(model, rec, pts, others)
                r["whole_roof"] = whole_roof(P, b2.MeshZ(mesh))
            r["meshes"] = mesh_checks_v2(bid, root) if version == "v2" else mesh_checks(bid, root)
            if version == "v2":
                r["collider"] = {k: model.get("collider", {}).get(k) for k in ("triangles", "max_dev_m", "p95_dev_m")}
                r["triangles"] = model.get("triangles")
            out.append(r)
        except Exception as ex:
            import traceback
            out.append({"building": bid, "error": repr(ex), "trace": traceback.format_exc(), "pass": False,
                        "reason": f"check failed: {ex!r}"})
    return out


def distribution(vals):
    v = np.abs(np.asarray([x for x in vals if x is not None], float))
    if not len(v):
        return None
    return {"n": int(len(v)), "median": round(float(np.median(v)), 3), "p90": round(float(np.percentile(v, 90)), 3),
            "p95": round(float(np.percentile(v, 95)), 3), "max": round(float(v.max()), 3),
            "within_0.2": round(float(np.mean(v <= 0.2)), 4), "zero_in_band": round(float(np.mean(v == 0)), 4)}


def whole_roof_summary(rows):
    """Whole-roof metric over all buildings: per-building medians/p95 and the pooled share of
    roof points within 0.2 m."""
    w = [r["whole_roof"] for r in rows if r.get("whole_roof")]
    if not w:
        return None
    n = sum(x["n"] for x in w)
    return {"buildings": len(w), "points": n,
            "pooled_within_0.2": round(sum(x["n_within_0.2"] for x in w) / max(n, 1), 4),
            "median_of_medians": round(float(np.median([x["median"] for x in w])), 3),
            "median_of_p90": round(float(np.median([x["p90"] for x in w])), 3),
            "median_of_p95": round(float(np.median([x["p95"] for x in w])), 3),
            "p95_of_p95": round(float(np.percentile([x["p95"] for x in w], 95)), 3),
            "buildings_p95_within_0.2": sum(1 for x in w if x["p95"] <= 0.2)}


def collider_summary(rows):
    c = [r["collider"] for r in rows if r.get("collider") and r["collider"].get("triangles") is not None]
    if not c:
        return None
    t = [x["triangles"] for x in c]
    d = [x["max_dev_m"] for x in c]
    return {"buildings": len(c), "triangles_total": int(sum(t)), "triangles_median": float(np.median(t)),
            "triangles_max": int(max(t)), "over_2000": sum(1 for x in t if x > 2000),
            "max_dev_max_m": round(float(max(d)), 3), "max_dev_p95_m": round(float(np.percentile(d, 95)), 3),
            "p95_dev_max_m": round(float(max(x["p95_dev_m"] for x in c)), 3),
            "over_0.15": sum(1 for x in d if x > 0.15 + 1e-9)}


V1_HISTORY = ("**v1 result (2026-11-20, roof planes only):** 106 of 375 campus buildings passed the strict gate "
              "(28.3%); 210 (56.0%) passed on the main part. v1 split many roofs into too many small parts, "
              "approximated curved roofs by facets, lumped roof equipment and lower roof edges into the wrong "
              "planes, and modeled Kirkland Hall as flat with a tower. v2 keeps v1 where it passed and follows "
              "the LiDAR roof surface (height field, <= 0.08 m mesh tolerance) everywhere else.")


def write_md(path, summary, rows):
    if summary.get("version") == "v2":
        return write_md_v2(path, summary, rows)
    L = ["# Stage 1 buildings accuracy (LoD2)", "",
         f"Made {summary['made']} by `Tools/VanderbiltCampus/pipeline/buildings_check.py`. "
         "Targets: footprint corners <= 0.3 m (p95 vs the reference), eave and ridge <= 0.2 m from the LiDAR "
         "bands (STANDARDS.md section 5).", "",
         f"**Pass: {summary['passed']} of {summary['buildings']} campus buildings "
         f"({summary['pass_rate']:.1%}); target {summary['target']:.0%}.** (strict: footprint and every "
         "checked part >= 4 m2)", "",
         f"- Main part only (footprint + the biggest part): {summary['pass_main_part']} of {summary['buildings']} "
         f"({summary['pass_main_part'] / max(summary['buildings'], 1):.1%})",
         f"- Footprint + every part >= 50 m2: {summary['pass_parts_50m2']} of {summary['buildings']} "
         f"({summary['pass_parts_50m2'] / max(summary['buildings'], 1):.1%})",
         f"- Parts: {summary['parts_passing']} of {summary['parts_checked']} checked parts pass eave, ridge and fit "
         f"({summary['parts_passing'] / max(summary['parts_checked'], 1):.1%})",
         f"- Colliders: {summary['colliders_total']} convex parts, {summary['colliders_bad']} failing the solid test "
         f"(volume >= 0.1 m3, >= 0.3 m thick, no duplicate/degenerate triangles); max "
         f"{summary['colliders_per_building_max']} per building; {summary['buildings_over_12_colliders']} buildings over 12",
         f"- LoD2/LoD1 meshes with problems: {len(summary['lod_mesh_problems'])} "
         + ", ".join(f"{k} {v}" for k, v in summary['lod_mesh_problems'].items()), "",
         "| Test | Pass rate |", "|---|---|"]
    for k in ("pass_footprint", "pass_eave", "pass_ridge", "pass_fit"):
        L.append(f"| {k.replace('pass_', '')} | {summary['by_test'][k]:.1%} |")
    L += ["", "Error distributions (absolute, m):", "", "| What | n | median | p90 | p95 | max |", "|---|---|---|---|---|---|"]
    for k in ("footprint_ref_p95", "footprint_lidar_p95", "eave_main_part", "ridge_main_part",
              "eave_all_parts", "ridge_all_parts", "roof_surface_median", "roof_surface_p95"):
        d = summary["distributions"].get(k)
        if d:
            L.append(f"| {k} | {d['n']} | {d['median']} | {d['p90']} | {d['p95']} | {d['max']} |")
    L += ["", "Roof types: " + ", ".join(f"{k} {v}" for k, v in sorted(summary["roof_types"].items(),
                                                                         key=lambda t: -t[1])), "",
          "## Outliers (every failing building)", "",
          "| Building | Name | Roof | Area m2 | fp p95 | eave | ridge | Reason |", "|---|---|---|---|---|---|---|---|"]
    for r in sorted(rows, key=lambda r: -(r.get("area_m2") or 0)):
        if r.get("pass"):
            continue
        L.append(f"| {r['building']} | {(r.get('name') or '').replace('|', '/')} | {r.get('roof_type', '')} | "
                 f"{r.get('area_m2', 0):.0f} | {r.get('footprint_ref_p95_m')} | {r.get('worst_eave_err_m')} | "
                 f"{r.get('worst_ridge_err_m')} | {r.get('reason', '')} |")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(L) + "\n", encoding="utf-8", newline="\n")


def write_md_v2(path, summary, rows):
    n = max(summary["buildings"], 1)
    w = summary.get("whole_roof") or {}
    c = summary.get("collider_mesh") or {}
    L = ["# Stage 1 buildings accuracy (LoD2, v2)", "", V1_HISTORY, "",
         f"Made {summary['made']} by `Tools/VanderbiltCampus/pipeline/buildings_check.py --version v2`. "
         "Targets: footprint corners <= 0.3 m (p95 vs the reference), eave and ridge <= 0.2 m from the LiDAR "
         "bands (STANDARDS.md section 5). Same gate as v1 (tolerances unchanged, every campus building in the "
         "denominator).", "",
         f"**Pass: {summary['passed']} of {summary['buildings']} campus buildings ({summary['pass_rate']:.1%}); "
         f"target {summary['target']:.0%}.** (strict: footprint and every checked part >= 4 m2)", "",
         f"- Main part only (footprint + the biggest part): {summary['pass_main_part']} ({summary['pass_main_part'] / n:.1%})",
         f"- Footprint + every part >= 50 m2: {summary['pass_parts_50m2']} ({summary['pass_parts_50m2'] / n:.1%})",
         f"- Parts: {summary['parts_passing']} of {summary['parts_checked']} checked parts pass eave, ridge and fit "
         f"({summary['parts_passing'] / max(summary['parts_checked'], 1):.1%})",
         f"- Roof methods: " + ", ".join(f"{k} {v} (pass {summary['pass_by_method'].get(k, 0)})"
                                        for k, v in sorted(summary["methods"].items())),
         f"- Whole-roof metric (all roof points vs the LoD2 mesh, vertical): {w.get('pooled_within_0.2', 0):.1%} of "
         f"{w.get('points', 0)} points within 0.2 m; per building median of medians {w.get('median_of_medians')} m, "
         f"median p90 {w.get('median_of_p90')} m, median p95 {w.get('median_of_p95')} m, p95 of the per-building p95 "
         f"{w.get('p95_of_p95')} m; {w.get('buildings_p95_within_0.2')} of {w.get('buildings')} buildings have p95 <= 0.2 m",
         f"- Triangles: LoD2 {summary['triangles_total']['lod2']} total (median {summary['triangles_lod2_median']} per "
         f"building), LoD1 {summary['triangles_total']['lod1']}, colliders {summary['triangles_total']['col_mesh']}",
         f"- Colliders: one non-convex mesh per building ({c.get('buildings')} buildings, median {c.get('triangles_median')} "
         f"triangles, max {c.get('triangles_max')}, {c.get('over_2000')} over 2000); deviation from the LoD2 shell max "
         f"{c.get('max_dev_max_m')} m (p95 over buildings {c.get('max_dev_p95_m')} m, worst per-building p95 "
         f"{c.get('p95_dev_max_m')} m), {c.get('over_0.15')} over 0.15 m; {summary['colliders_bad']} failing the "
         "mesh test (degenerate/duplicate triangles, vertices closer than 1 mm, inconsistent winding)",
         f"- LoD2/LoD1 meshes with problems: {len(summary['lod_mesh_problems'])} "
         + ", ".join(f"{k} {v}" for k, v in summary['lod_mesh_problems'].items()), "",
         "| Test | Pass rate |", "|---|---|"]
    for k in ("pass_footprint", "pass_eave", "pass_ridge", "pass_fit"):
        L.append(f"| {k.replace('pass_', '')} | {summary['by_test'][k]:.1%} |")
    L += ["", "Error distributions (absolute, m):", "", "| What | n | median | p90 | p95 | max |", "|---|---|---|---|---|---|"]
    for k in ("footprint_ref_p95", "footprint_lidar_p95", "eave_main_part", "ridge_main_part",
              "eave_all_parts", "ridge_all_parts", "whole_roof_median", "whole_roof_p95"):
        d = summary["distributions"].get(k)
        if d:
            L.append(f"| {k} | {d['n']} | {d['median']} | {d['p90']} | {d['p95']} | {d['max']} |")
    fails = [r for r in rows if not r.get("pass")]
    cls = Counter(failure_class(r) for r in fails)
    L += ["", "Failures by class: " + ", ".join(f"{k} {v}" for k, v in cls.most_common()), "",
          "Roof types: " + ", ".join(f"{k} {v}" for k, v in sorted(summary["roof_types"].items(), key=lambda t: -t[1])), "",
          "## Every failing building", "",
          "| Building | Name | Method | Class | Area m2 | fp p95 | eave | ridge | roof p95 | Reason |",
          "|---|---|---|---|---|---|---|---|---|---|"]
    for r in sorted(fails, key=lambda r: -(r.get("area_m2") or 0)):
        wr = (r.get("whole_roof") or {}).get("p95")
        L.append(f"| {r['building']} | {(r.get('name') or '').replace('|', '/')} | {r.get('roof_method', '')} | "
                 f"{failure_class(r)} | {r.get('area_m2', 0):.0f} | {r.get('footprint_ref_p95_m')} | "
                 f"{r.get('worst_eave_err_m')} | {r.get('worst_ridge_err_m')} | {wr} | {r.get('reason', '')} |")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(L) + "\n", encoding="utf-8", newline="\n")


def failure_class(r):
    """One word for why a building fails (first matching rule)."""
    if r.get("pass"):
        return "pass"
    if "error" in r:
        return "check_error"
    if not r.get("checked_parts"):
        return "no_lidar_roof"
    fp_bad = r.get("pass_footprint") is False
    roof_bad = not (r.get("pass_eave") and r.get("pass_ridge") and r.get("pass_fit"))
    if fp_bad and roof_bad:
        return "footprint+roof"
    if fp_bad:
        return "footprint_only"
    if not r.get("pass_eave") and r.get("pass_ridge"):
        return "eave"
    if not r.get("pass_ridge") and r.get("pass_eave"):
        return "ridge"
    if not r.get("pass_fit") and r.get("pass_eave") and r.get("pass_ridge"):
        return "fit"
    return "eave+ridge"


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--jobs", type=int, default=14)
    p.add_argument("--target", type=float, default=0.95)
    p.add_argument("--only", nargs="*")
    p.add_argument("--version", default="v1", choices=["v1", "v2"], help="which building folders to check")
    args = p.parse_args(argv)
    ver = args.version
    t0 = time.time()
    root = data_root()
    allrec = load_json(bc.out_dir(root) / "footprints.json")["buildings"]
    recs = {r["id"]: r for r in allrec if r["zone"] == "campus"}
    if args.only:
        recs = {k: v for k, v in recs.items() if k in set(args.only)}
    missing = [b for b in recs if not (bc.building_dir(b, root, ver) / "model.json").exists()]
    allg = [(r["id"], bc.geom_from_json(r["polygon"])) for r in allrec]
    tree = shapely.STRtree([g for _, g in allg])
    by_tile = {}
    for bid, r in recs.items():
        if bid not in missing:
            by_tile.setdefault(r["tile"], []).append((bid, r))
    jobs = []
    for tile, items in sorted(by_tile.items()):
        env = shapely.box(*np.array([bc.geom_from_json(r["polygon"]).bounds for _, r in items]).min(axis=0)[:2] - 10,
                        *np.array([bc.geom_from_json(r["polygon"]).bounds for _, r in items]).max(axis=0)[2:] + 10)
        nb = [allg[int(k)] for k in tree.query(env)]
        if ver == "v2":       # one job per building: height-field checks of big buildings take a while
            for it in sorted(items, key=lambda t: -t[1]["area_m2"]):
                jobs.append((tile, [it], nb, root, ver))
        else:
            jobs.append((tile, items, nb, root, ver))
    rows = []
    if args.jobs > 1 and len(jobs) > 1:
        with Pool(args.jobs) as pool:
            for out in pool.imap_unordered(run_job, jobs):
                rows += out
    else:
        for j in jobs:
            rows += run_job(j)
    for b in missing:
        rows.append({"building": b, "name": recs[b].get("name"), "pass": False, "area_m2": recs[b]["area_m2"],
                     "reason": "no model (buildings_lod2.py / buildings_v2.py failed or not run)"})
    rows.sort(key=lambda r: r["building"])
    n = len(rows)
    passed = sum(1 for r in rows if r.get("pass"))
    ok_rows = [r for r in rows if "parts" in r]
    allparts = [p for r in ok_rows for p in r["parts"] if p.get("checked")]
    summary = {
        "made": time.strftime("%Y-%m-%dT%H:%M:%S"), "buildings": n, "passed": passed,
        "pass_rate": passed / max(n, 1), "target": args.target,
        "pass_main_part": sum(1 for r in rows if r.get("pass_main_part")),
        "pass_parts_50m2": sum(1 for r in rows if r.get("pass_parts_50m2")),
        "parts_checked": len(allparts),
        "parts_passing": sum(1 for p in allparts if abs(p["eave_err_m"]) <= H_TOL and abs(p["ridge_err_m"]) <= H_TOL
                             and not p.get("fit_fail")),
        "by_test": {k: float(np.mean([r.get(k, False) for r in rows])) for k in ("pass_footprint", "pass_eave", "pass_ridge", "pass_fit")},
        "distributions": {
            "footprint_ref_p95": distribution([r["footprint"]["vs_reference"]["p95"] for r in ok_rows
                                               if r["footprint"]["vs_reference"]]),
            "footprint_lidar_p95": distribution([r["footprint"]["vs_lidar"]["p95"] for r in ok_rows
                                                 if r["footprint"]["vs_lidar"]]),
            "footprint_lidar_median": distribution([r["footprint"]["vs_lidar"]["median"] for r in ok_rows
                                                    if r["footprint"]["vs_lidar"]]),
            "eave_main_part": distribution([r["eave_err_m"] for r in ok_rows]),
            "ridge_main_part": distribution([r["ridge_err_m"] for r in ok_rows]),
            "eave_all_parts": distribution([p["eave_err_m"] for p in allparts]),
            "ridge_all_parts": distribution([p["ridge_err_m"] for p in allparts]),
            "roof_surface_median": distribution([r["roof_surface"]["median"] for r in ok_rows if r["roof_surface"]]),
            "roof_surface_p95": distribution([r["roof_surface"]["p95"] for r in ok_rows if r["roof_surface"]]),
            "whole_roof_median": distribution([r["whole_roof"]["median"] for r in ok_rows if r.get("whole_roof")]),
            "whole_roof_p95": distribution([r["whole_roof"]["p95"] for r in ok_rows if r.get("whole_roof")]),
        },
        "roof_types": dict(Counter(r.get("roof_type") for r in rows if r.get("roof_type"))),
        "failures_by_test": dict(Counter(k for r in rows if not r.get("pass")
                                         for k in ("pass_footprint", "pass_eave", "pass_ridge", "pass_fit") if r.get(k) is False)),
        "errors": [r["building"] for r in rows if "error" in r],
        "colliders_total": sum(r.get("meshes", {}).get("colliders", 0) for r in rows),
        "colliders_bad": sum(len(r.get("meshes", {}).get("bad_colliders", [])) for r in rows),
        "colliders_per_building_max": max((r.get("meshes", {}).get("colliders", 0) for r in rows), default=0),
        "buildings_over_12_colliders": sum(1 for r in rows if r.get("meshes", {}).get("colliders", 0) > 12),
        "lod_mesh_problems": {r["building"]: r["meshes"]["lod_problems"] for r in rows
                              if r.get("meshes", {}).get("lod_problems")},
        "version": ver,
        "methods": dict(Counter(r.get("roof_method") for r in rows if r.get("roof_method"))),
        "pass_by_method": {m: sum(1 for r in rows if r.get("roof_method") == m and r.get("pass"))
                           for m in ("planes_v1", "heightfield_v2")},
        "whole_roof": whole_roof_summary(rows),
        "triangles_total": {k: sum((r.get("triangles") or {}).get(k, 0) for r in rows) for k in ("lod2", "lod1", "col_mesh")},
        "triangles_lod2_median": float(np.median([r["triangles"]["lod2"] for r in rows if r.get("triangles")])) if any(r.get("triangles") for r in rows) else None,
        "collider_mesh": collider_summary(rows),
        "rules": {"footprint_tol_m": FP_TOL, "height_tol_m": H_TOL,
                  "eave_band": "p5-p10 of roof points within 0.75 m inside the eave edges",
                  "ridge_band": "p98-p99.5 of roof points within 0.75 m of ridge lines (whole part if flat)",
                  "excluded": "points > 0.3 m above the model inside ledgered feature extents"},
    }
    rd = bc.reports_dir(root)
    sfx = "_v2" if ver == "v2" else ""
    jp = bc.save(rd / f"buildings_accuracy{sfx}.json", {"summary": summary, "buildings": rows})
    mp = rd / f"buildings_accuracy{sfx}.md"
    write_md(mp, summary, rows)
    if not args.only:
        write_md(DOCS / "stage1-buildings-accuracy.md", summary, rows)
    # numbers into model.json (archive + git mirror)
    db = manifest.connect(root)
    for r in rows:
        d = bc.building_dir(r["building"], root, ver)
        mpath = d / "model.json"
        if not mpath.exists():
            continue
        m = load_json(mpath)
        m["accuracy"] = {k: r.get(k) for k in ("pass", "pass_footprint", "pass_eave", "pass_ridge", "reason",
                                               "footprint_ref_p95_m", "eave_err_m", "ridge_err_m",
                                               "worst_eave_err_m", "worst_ridge_err_m", "roof_surface",
                                               "roof_points_per_m2", "whole_roof", "roof_method")}
        m["accuracy"]["footprint"] = r.get("footprint")
        m["accuracy"]["parts"] = [{k: v for k, v in pp.items()} for pp in r.get("parts", [])]
        m["accuracy"]["checked"] = summary["made"]
        bc.save(mpath, m)
        bc.save(bc.git_building_dir(r["building"]) / "model.json", m)
        manifest.add_file(mpath, source="buildings_check.py", license="derived", stage=1, root=root, db=db)
    for f in (jp, mp):
        manifest.add_file(f, source="buildings_check.py", license="derived", stage=1, root=root, db=db)
    db.commit()
    db.close()
    print(f"colliders {summary['colliders_total']} ({summary['colliders_bad']} bad, max "
          f"{summary['colliders_per_building_max']} per building, {summary['buildings_over_12_colliders']} buildings > 12); "
          f"LoD mesh problems: {len(summary['lod_mesh_problems'])}")
    print(f"main part {summary['pass_main_part']}/{n}, parts>=50m2 {summary['pass_parts_50m2']}/{n}, "
          f"parts {summary['parts_passing']}/{summary['parts_checked']}")
    print(f"pass {passed}/{n} = {passed / max(n, 1):.1%} (target {args.target:.0%}); by test "
          + ", ".join(f"{k} {v:.1%}" for k, v in summary["by_test"].items()))
    for k, v in summary["distributions"].items():
        print(f"  {k}: {v}")
    print(f"wrote {jp} and {mp} in {time.time() - t0:.0f} s")
    return 0 if passed / max(n, 1) >= args.target else 1


if __name__ == "__main__":
    sys.exit(main())
