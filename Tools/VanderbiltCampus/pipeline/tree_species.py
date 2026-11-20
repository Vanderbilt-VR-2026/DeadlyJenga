"""Stylized tree species and species assignment for trees v2 (Stage 5).

Species models (low-poly candy style, made in code like tree_kit.py):
  oak        broad, spreading, slightly flat crown
  maple      dense round-oval crown with a rounded top
  elm        vase: two forking limbs and a wide, flat-topped crown
  magnolia   evergreen, dense pyramid-oval crown down low
  conifer    evergreen, stacked cones
  ornamental small flowering tree (dogwood / redbud stand-in), flat round crown
Each has three detail levels picked by tree height (like v1 sizes): small <= 7 m (<= 32
triangles), medium <= 13 m (<= 44), full > 13 m (<= 56), and a LOD1 (<= 16 triangles).
They are unit sized and scaled per tree to its LiDAR height, crown width and crown base
(tree_kit.place), so v2 trees are as tall and wide as v1 trees.

How each tree gets its species ("species_source"), first rule that applies:
  famous_public_source   a public source names and locates the tree (see FAMOUS)
  osm_tags               OSM species / genus / leaf_type / leaf_cycle on the matched node
  lidar_leafoff          the LiDAR was flown in leaf-off season (20-21 March 2022, D22
                         metadata). A crown with >= 40 points that lets fewer than 10% of its
                         returns pass to the ground or a roof below is evergreen (threshold 10%, a guess):
                         conifer if crown width / height < 0.5, else magnolia. A crown that lets
                         more through is deciduous: ornamental if <= 8 m tall and <= 7 m wide,
                         else a seeded oak / maple / elm mix ("lidar_leafoff+seeded")
  seeded_mix             too few points to tell: a fixed pseudo-random mix (seed = tree id)
The NAIP 2021 greenness of each crown is also measured and reported, but not used: its
acquisition season is not recorded here and it agrees poorly with the LiDAR evidence.

Commands:
  tree_species.py           assign species -> derived/trees/v2/trees.json (+ species report)
  tree_species.py --check   print triangle counts and check every species mesh
Then run trees_build.py --version v2 to write the v2 tile meshes.
"""
import argparse
import datetime
import os
import sys
import zlib
from functools import lru_cache
from multiprocessing import Pool
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import frame as fr  # noqa: E402
import manifest  # noqa: E402
import tree_kit as tk  # noqa: E402
import vcmesh  # noqa: E402
from common import data_root, fail, load_json, save_json  # noqa: E402

KIT_VERSION = 2
SPECIES = ["oak", "maple", "elm", "magnolia", "conifer", "ornamental"]
DETAILS = [("small", 7.0), ("medium", 13.0), ("full", 1e9)]
LIMITS = {"small": 32, "medium": 44, "full": 56, "lod1": 16}
EVERGREEN_PEN = 0.10           # not calibrated: no ground-truth evergreen trees inside the LiDAR area
MIN_POINTS = 40
ORNAMENTAL_MAX_H, ORNAMENTAL_MAX_W = 8.0, 7.0
DECIDUOUS_MIX = [("oak", 0.42), ("maple", 0.34), ("elm", 0.24)]
UNKNOWN_MIX = [("oak", 0.36), ("maple", 0.29), ("elm", 0.2), ("magnolia", 0.08), ("conifer", 0.07)]
LICENSE = "Derived: USGS 3DEP LiDAR (public domain), OpenStreetMap (ODbL 1.0), Bioimages (CC0 metadata)"

# Famous trees: only ones a public source names and locates.
FAMOUS = [{
    "name": "Bicentennial Oak",
    "species": "oak", "scientific": "Quercus macrocarpa (bur oak)",
    "lat": 36.14642, "lon": -86.80219, "uncertainty_m": 10.0,
    "location": "center of the lawn between Rand/Sarratt and Garland Halls",
    "sources": ["https://bioimages.vanderbilt.edu/vanderbilt/7-314.htm (coordinates; metadata CC0)",
                "https://news.vanderbilt.edu/2022/11/16/bicentennial-oak-beloved-campus-landmark-has-died/"],
    "status": "fell 12 Nov 2022 (age-related decay); the LiDAR (March 2022) still shows it. A young bur oak, "
              "the Sesquicentennial Oak, was planted nearby in April 2023 (exact spot not published).",
    "standing_today": False,
}]


# ---- models ----------------------------------------------------------------------

def _branch(angle_deg, length=0.55, r=0.025):
    """A thin 3-sided limb from the crown base, leaning angle_deg about Y (foliage units)."""
    v, f, inside = tk.frustum(3, r, r * 0.6, 0.0, length)
    a = np.radians(angle_deg)
    Ry = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])
    return v @ Ry.T, f, inside @ Ry.T


def _foliage(species, detail, lod):
    """List of (piece, material) in foliage units (x, y in -0.5..0.5, z 0..1 after stretching)."""
    F, B = "foliage", "bark"
    if lod == 1:
        return {
            "oak": [(tk.octahedron((0, 0, 0.5), (0.5, 0.5, 0.36)), F)],
            "maple": [(tk.octahedron((0, 0, 0.5), (0.48, 0.48, 0.5)), F)],
            "elm": [(tk.octahedron((0, 0, 0.62), (0.5, 0.5, 0.32)), F)],
            "magnolia": [(tk.bipyramid((0, 0, 0.42), (0.5, 0.5, 0.5), n=5), F)],
            "conifer": [(tk.cone(0.0, 0.5, 1.0, n=5), F)],
            "ornamental": [(tk.octahedron((0, 0, 0.5), (0.5, 0.5, 0.36)), F)],
        }[species]
    designs = {
        "oak": [(tk.icosahedron((0, 0, 0.55), (0.42, 0.42, 0.36)), F),
                (tk.bipyramid((0.27, 0.08, 0.42), (0.27, 0.27, 0.24)), F),
                (tk.bipyramid((-0.24, -0.12, 0.45), (0.27, 0.27, 0.24)), F)],
        "maple": [(tk.icosahedron((0, 0, 0.5), (0.45, 0.45, 0.5)), F),
                  (tk.bipyramid((0.02, 0.0, 0.86), (0.27, 0.27, 0.18)), F),
                  (tk.bipyramid((0.2, -0.16, 0.36), (0.25, 0.25, 0.25)), F)],
        "elm": [(tk.icosahedron((0, 0, 0.72), (0.5, 0.5, 0.28)), F),
                None,                                                    # the two limbs (12 tris)
                (tk.bipyramid((0, 0, 0.42), (0.22, 0.22, 0.3)), F)],
        "magnolia": [(tk.icosahedron((0, 0, 0.55), (0.4, 0.4, 0.4)), F),
                     (tk.bipyramid((0, 0, 0.27), (0.5, 0.5, 0.27)), F),
                     (tk.bipyramid((0, 0, 0.9), (0.2, 0.2, 0.14)), F)],
        "ornamental": [(tk.icosahedron((0, 0, 0.5), (0.5, 0.5, 0.36)), F),
                       (tk.bipyramid((0.22, 0.15, 0.64), (0.22, 0.22, 0.17)), F),
                       (tk.bipyramid((-0.2, -0.16, 0.6), (0.22, 0.22, 0.17)), F)],
    }
    if species == "conifer":
        return {"small": [(tk.cone(0.0, 0.5, 0.65), F), (tk.cone(0.42, 0.34, 0.58), F)],
                "medium": [(tk.cone(0.0, 0.5, 0.5), F), (tk.cone(0.3, 0.39, 0.42), F), (tk.cone(0.58, 0.27, 0.42), F)],
                "full": [(tk.cone(0.0, 0.5, 0.42), F), (tk.cone(0.24, 0.42, 0.38), F), (tk.cone(0.46, 0.33, 0.34), F),
                         (tk.cone(0.66, 0.23, 0.34), F)]}[detail]
    d = designs[species]
    n = {"small": 1, "medium": 2, "full": 3}[detail]
    out = []
    for piece in d[:n]:
        if piece is None:
            out += [(_branch(-32), B), (_branch(30), B)]
        else:
            out.append(piece)
    return out


def detail_of(height):
    for name, top in DETAILS:
        if height <= top:
            return name
    return "full"


@lru_cache(maxsize=None)
def species_mesh(species, detail, lod):
    """Unit species mesh: dict verts, faces, face_mat, uv0, part (0 trunk, 1 foliage space)."""
    mats = vcmesh.material_index()
    trunk = tk.frustum(3, 1.0, 0.6) if lod == 1 else tk.frustum(6, 1.0, 0.6)
    pieces = [(trunk, 0, "bark")] + [(p, 1, m) for p, m in _foliage(species, detail, lod)]
    V, F, part, fm, off = [], [], [], [], 0
    for (v, f, inside), prt, mat in pieces:
        F.append(tk.orient(v, f, inside) + off); V.append(v)
        part.append(np.full(len(v), prt)); fm.append(np.full(len(f), mats[mat], np.uint8)); off += len(v)
    V, F, part, fm = np.vstack(V), np.vstack(F), np.concatenate(part), np.concatenate(fm)
    fol = part == 1
    lo, hi = V[fol].min(0), V[fol].max(0)          # stretch the crown to fill the unit box exactly
    V[fol, 0] = (V[fol, 0] - (lo[0] + hi[0]) / 2) / (hi[0] - lo[0])
    V[fol, 1] = (V[fol, 1] - (lo[1] + hi[1]) / 2) / (hi[1] - lo[1])
    V[fol, 2] = (V[fol, 2] - lo[2]) / (hi[2] - lo[2])
    ref_size = {"small": "small", "medium": "medium", "full": "large"}[detail]
    ref = (tk.REFERENCE_CONICAL if species == "conifer" else tk.REFERENCE)[ref_size]
    uv0 = tk.triangle_uvs(tk.place(V, part, {"height": ref[0], "crown_diameter": ref[1], "crown_base": ref[2]})[F])
    return {"verts": V, "faces": F, "face_mat": fm, "uv0": uv0, "part": part}


def tree_mesh(tree, lod=0):
    """Mesh dict for one v2 tree (needs "species") in the campus frame."""
    k = species_mesh(tree["species"], detail_of(float(tree["height"])), lod)
    v = tk.place(k["verts"], k["part"], tree, tk.yaw_of(tree), (tree["x"], tree["y"], tree["ground_z"]))
    return {"verts": v, "faces": k["faces"].copy(), "face_mat": k["face_mat"].copy(), "uv0": k["uv0"].copy(),
            "meta": {}}


def check_species():
    rows, bad = [], 0
    for s in SPECIES:
        for d, _ in DETAILS:
            for lod in (0, 1):
                if lod == 1 and d != "full":
                    continue
                m = species_mesh(s, d, lod)
                n = len(m["faces"])
                probs = vcmesh.check_mesh(m)
                lim = LIMITS["lod1"] if lod == 1 else LIMITS[d]
                if n > lim:
                    probs.append(f"{n} > {lim} triangles")
                if vcmesh.uv_overlap_area(m["uv0"], grid=512) >= 0.02:
                    probs.append("uv overlap")
                bad += bool(probs)
                rows.append((s, d if lod == 0 else "lod1", n, probs))
    return rows, bad


# ---- evidence ----------------------------------------------------------------------

_W = {}


def _init(crowns_path):
    _W["crowns"] = tc_read(crowns_path)


def tc_read(path):
    import trees_common as tc
    return tc.read_raster(path)


def tile_counts(job):
    """Per crown label: (all points, points that reached the ground or a roof) in one tile."""
    import lidar_io
    import trees_common as tc
    tile_file, n = job
    cr, g = _W["crowns"]
    d = lidar_io.read_tile(lidar_io.lidar_dir() / tile_file)
    r, c = tc.cell_of(d["x"], d["y"], g)
    ok = tc.inside_grid(r, c, g)
    lab = np.zeros(len(r), np.int64)
    lab[ok] = cr[r[ok], c[ok]]
    k = lab > 0
    cls = d["classification"]
    countable = k & np.isin(cls, np.array([1, 2, 3, 4, 5, 6], np.uint8))
    through = countable & np.isin(cls, np.array([2, 6], np.uint8))
    return (np.bincount(lab[countable], minlength=n + 1)[:n + 1], np.bincount(lab[through], minlength=n + 1)[:n + 1])


def lidar_penetration(root, n, jobs):
    import lidar_io
    crowns = root / "derived/trees/v1/crowns.tif"
    tiles = [t["file"] for t in lidar_io.load_index()["tiles"] if t["zone"] in ("campus", "ring") and t["points"] > 0]
    with Pool(jobs, initializer=_init, initargs=(crowns,)) as pool:
        res = pool.map(tile_counts, [(f, n) for f in tiles], chunksize=4)
    allp = np.sum([r[0] for r in res], axis=0)
    thr = np.sum([r[1] for r in res], axis=0)
    return allp[1:], thr[1:]


def naip_greenness(root, trees):
    """Median excess-green (2G - R - B) / (R + G + B) of NAIP pixels in the inner crown."""
    import trees_common as tc
    o, s = tc.read_raster(root / "derived/ortho/v1/naip2021_campus_0.6m.tif")
    out = np.full(len(trees), np.nan)
    for i, t in enumerate(trees):
        r = max(0.6, 0.3 * t["crown_diameter"])
        xs = np.arange(t["x"] - r, t["x"] + r + 0.01, 0.6)
        X, Y = np.meshgrid(xs, np.arange(t["y"] - r, t["y"] + r + 0.01, 0.6))
        m = np.hypot(X - t["x"], Y - t["y"]) <= r
        rr, cc = tc.cell_of(X[m], Y[m], s)
        ok = tc.inside_grid(rr, cc, s)
        px = o[rr[ok], cc[ok]].astype(float)
        if len(px):
            out[i] = np.median((2 * px[:, 1] - px[:, 0] - px[:, 2]) / (px.sum(1) + 1))
    return out


def seeded(tree_id, mix):
    u = (zlib.crc32(("species:" + str(tree_id)).encode()) % 100000) / 100000.0
    acc = 0.0
    for name, w in mix:
        acc += w
        if u < acc:
            return name
    return mix[-1][0]


def deciduous_choice(t):
    if t["height"] <= ORNAMENTAL_MAX_H and t["crown_diameter"] <= ORNAMENTAL_MAX_W:
        return "ornamental", "size"
    return seeded(t["id"], DECIDUOUS_MIX), "seeded"


def assign(trees, pen, npts):
    """Set species, species_source and species_evidence on every built tree (in place)."""
    famous = {}
    for f in FAMOUS:
        X, Y, _ = fr.to_campus(f["lon"], f["lat"], 0.0, 4326)
        cand = [(np.hypot(t["x"] - X, t["y"] - Y), i) for i, t in enumerate(trees) if t["kind"] == "tree"]
        cand = [c for c in cand if c[0] <= f["uncertainty_m"]]
        if cand:
            # the tallest detected tree within the stated uncertainty
            i = max(cand, key=lambda c: trees[c[1]]["height"])[1]
            famous[i] = {**f, "matched_tree": trees[i]["id"],
                         "distance_m": round(float(min(c[0] for c in cand if c[1] == i)), 2),
                         "campus_xy": [round(float(X), 2), round(float(Y), 2)]}
    for i, t in enumerate(trees):
        if t["kind"] != "tree":
            continue
        ev = {"lidar_points": int(npts[i]), "lidar_pass_fraction": round(float(pen[i]), 3) if npts[i] else None}
        tags = t.get("osm_tags") or {}
        if i in famous:
            f = famous[i]
            t.update(species=f["species"], species_source="famous_public_source", famous_name=f["name"],
                     famous_standing_today=f["standing_today"], famous_note=f["status"])
        elif tags.get("species") or tags.get("genus"):
            g = (tags.get("genus") or tags.get("species", "")).split()[0].lower()
            sp = {"quercus": "oak", "acer": "maple", "ulmus": "elm", "magnolia": "magnolia", "pinus": "conifer",
                  "juniperus": "conifer", "cedrus": "conifer", "picea": "conifer", "tsuga": "conifer",
                  "cornus": "ornamental", "cercis": "ornamental", "prunus": "ornamental", "malus": "ornamental"}.get(g)
            if sp:
                t.update(species=sp, species_source="osm_tags")
        if "species" not in t and (tags.get("leaf_type") == "needleleaved"):
            t.update(species="conifer", species_source="osm_tags")
        if "species" not in t and tags.get("leaf_cycle") == "evergreen":
            t.update(species="magnolia", species_source="osm_tags")
        if "species" not in t and tags.get("leaf_cycle") in ("deciduous", "semi_deciduous", "semi_evergreen"):
            sp, how = deciduous_choice(t)
            t.update(species=sp, species_source="osm_tags+" + how)
        if "species" not in t and npts[i] >= MIN_POINTS:
            if pen[i] < EVERGREEN_PEN:
                ratio = t["crown_diameter"] / max(t["height"], 0.1)
                t.update(species="conifer" if ratio < 0.5 else "magnolia", species_source="lidar_leafoff")
                ev["leaf"] = "evergreen"
            else:
                sp, how = deciduous_choice(t)
                t.update(species=sp, species_source="lidar_leafoff+" + how)
                ev["leaf"] = "deciduous"
        if "species" not in t:
            if t["height"] <= ORNAMENTAL_MAX_H and t["crown_diameter"] <= ORNAMENTAL_MAX_W:
                t.update(species="ornamental", species_source="seeded_mix+size")
            else:
                t.update(species=seeded(t["id"], UNKNOWN_MIX), species_source="seeded_mix")
        t["species_evidence"] = ev
    return list(famous.values())


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--check", action="store_true", help="only check the species meshes")
    p.add_argument("--jobs", type=int, default=min(4, os.cpu_count() or 1), help="worker processes (default 4)")
    p.add_argument("--no-manifest", action="store_true")
    args = p.parse_args(argv)
    rows, bad = check_species()
    for s, d, n, probs in rows:
        if args.check or probs:
            print(f"{s:11s} {d:7s} {n:3d} tris {'ok' if not probs else probs}")
    if bad:
        sys.exit(1)
    if args.check:
        return
    root = data_root()
    src = root / "derived/trees/v1/trees.json"
    if not src.exists():
        fail("derived/trees/v1/trees.json is missing; run trees_detect.py first")
    data = load_json(src)
    trees = data["trees"]
    npts, thr = lidar_penetration(root, len(trees), args.jobs)
    pen = np.where(npts > 0, thr / np.maximum(npts, 1), 1.0)
    famous = assign(trees, pen, npts)
    green = naip_greenness(root, trees)
    for t, gval in zip(trees, green):
        if "species_evidence" in t and np.isfinite(gval):
            t["species_evidence"]["naip_exg"] = round(float(gval), 3)

    built = [t for t in trees if t["kind"] == "tree"]
    counts, by_src = {}, {}
    for t in built:
        counts[t["species"]] = counts.get(t["species"], 0) + 1
        by_src[t["species_source"]] = by_src.get(t["species_source"], 0) + 1
    ev = [t for t in built if t["species_evidence"].get("leaf")]
    eg = np.array([t["species_evidence"].get("leaf") == "evergreen" for t in ev])
    gx = np.array([t["species_evidence"].get("naip_exg", np.nan) for t in ev])
    okg = np.isfinite(gx)
    osm_check = [{"id": t["id"], "osm_leaf_cycle": t["osm_tags"].get("leaf_cycle"),
                  "lidar_pass_fraction": t["species_evidence"]["lidar_pass_fraction"],
                  "lidar_says": ("evergreen" if t["species_evidence"]["lidar_pass_fraction"] is not None and
                                 t["species_evidence"]["lidar_points"] >= MIN_POINTS and
                                 t["species_evidence"]["lidar_pass_fraction"] < EVERGREEN_PEN else "deciduous/unknown")}
                 for t in built if (t.get("osm_tags") or {}).get("leaf_cycle")]
    report = {
        "made": datetime.datetime.now().isoformat(timespec="seconds"),
        "kit_version": KIT_VERSION,
        "rules": {"evergreen_pass_fraction_below": EVERGREEN_PEN, "min_points": MIN_POINTS,
                  "ornamental_max": [ORNAMENTAL_MAX_H, ORNAMENTAL_MAX_W], "deciduous_mix": DECIDUOUS_MIX,
                  "unknown_mix": UNKNOWN_MIX, "lidar_season": "leaf-off, flown 2022-03-20/21 (D22 metadata)"},
        "species_counts": dict(sorted(counts.items())),
        "species_source_counts": dict(sorted(by_src.items())),
        "evidence": {
            "trees_with_lidar_leaf_evidence": len(ev), "evergreen": int(eg.sum()), "deciduous": int((~eg).sum()),
            "naip_exg_median_evergreen": round(float(np.nanmedian(gx[eg & okg])), 3) if (eg & okg).any() else None,
            "naip_exg_median_deciduous": round(float(np.nanmedian(gx[~eg & okg])), 3) if (~eg & okg).any() else None,
            "note": "NAIP greenness is reported for comparison only; it is not used to assign species.",
        },
        "osm_leaf_cycle_vs_lidar": osm_check,
        "famous_trees": famous,
    }
    out_dir = root / "derived/trees/v2"
    data2 = {**data, "version": 2, "made": report["made"], "species": report,
             "fields_v2": {"species": "one of " + ", ".join(SPECIES), "species_source": "how the species was chosen",
                           "species_evidence": "LiDAR leaf-off pass fraction, point count, NAIP greenness"}}
    save_json(out_dir / "trees.json", data2)
    save_json(out_dir / "species.json", report)
    rp = root / "reports/stage5/tree_species.json"
    save_json(rp, report)
    if not args.no_manifest:
        db = manifest.connect(root)
        for f in (out_dir / "trees.json", out_dir / "species.json", rp):
            manifest.add_file(f, "tree_species.py (derived/trees/v1, derived/lidar/v1, NAIP, OSM, Bioimages)", LICENSE,
                              5, root=root, db=db)
        db.commit(); db.close()
    print(f"species {report['species_counts']}; sources {report['species_source_counts']}; "
          f"evidence {report['evidence']}; famous {[f['name'] + ' -> ' + f['matched_tree'] for f in famous]}")


if __name__ == "__main__":
    main()
