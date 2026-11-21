"""Make light "game" meshes from heavy building meshes. Runs inside Blender (bmesh).

    blender -b --factory-startup --python blender/game_mesh.py -- --jobs-file JOBS.json

JOBS.json (written by pipeline/buildings_game.py) lists buildings:
  {"id", "lod2", "col_mesh", "out_lod0", "out_col", "stats"} (paths), plus "tol_visual",
  "tol_col" and optional "suffix" for a looser second level.

Per mesh:
  1. clean: merge vertices closer than 0.1 mm, drop degenerate and duplicate triangles,
     make the winding consistent with normals facing out (bmesh recalc_face_normals);
  2. merge near-coplanar faces: "limited dissolve" (visual: only faces of the same
     material), largest angle in ANGLES whose error stays within the tolerance;
  3. error-bounded decimation: Blender's quadric "collapse" decimation on that result,
     with the smallest ratio (bisection) whose measured error still stays within the
     tolerance. Error always wins over triangle count;
  4. measure (every candidate): two-sided distance, sampled on both surfaces with BVH
     nearest-point queries. Visual: against lod2 and per material (a lod2 point of material
     k must be near a result face of material k and the other way round), so material
     borders stay in place too; plus crease lines: points along every sharp edge
     (>= 20 degrees, footprint corners, eaves, ridges) of lod2 must lie within the
     tolerance of a sharp edge of the result. Collider: against col_mesh (its input), and
     the one-sided lod2 -> collider distance is reported;
  5. UV0 (visual): near-coplanar triangles of one material (0.5 degrees) form one planar
     chart, true size in meters, packed with vcmesh.pack_charts (same meters per UV unit
     everywhere, no overlaps).
"""
import argparse
import json
import math
import sys
import time
from pathlib import Path

import bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parent / "pipeline"))
import vc_fbx  # noqa: E402
import vcmesh  # noqa: E402

ANGLES = [0.05, 0.5, 1, 2, 3, 5, 7, 10, 14, 19]     # degrees; must stay under the 20 degree crease limit
CREASE_DEG = 20.0
RNG = np.random.default_rng(1)


# ---- bmesh <-> arrays ---------------------------------------------------------------

def to_bmesh(m):
    bm = bmesh.new()
    vs = [bm.verts.new(tuple(p)) for p in m["verts"]]
    bm.verts.ensure_lookup_table()
    for f, mat in zip(m["faces"], m["face_mat"]):
        a, b, c = (int(i) for i in f)
        if a == b or b == c or a == c:
            continue
        try:
            face = bm.faces.new((vs[a], vs[b], vs[c]))
        except ValueError:               # the same triangle twice: keep one
            continue
        face.material_index = int(mat)
    return bm


def tri_arrays(bm):
    """(verts (N,3), faces (M,3), face_mat (M,)) of a triangulated bmesh."""
    rest = [f for f in bm.faces if len(f.verts) != 3]
    if rest:                       # decimation can leave a few quads / n-gons
        bmesh.ops.triangulate(bm, faces=rest, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.verts.index_update()
    v = np.array([p.co[:] for p in bm.verts], np.float64).reshape(-1, 3)
    f = np.array([[l.vert.index for l in face.loops] for face in bm.faces], np.int64).reshape(-1, 3)
    mats = np.array([face.material_index for face in bm.faces], np.int64)
    return v, f, mats


def clean(bm):
    """Merge near vertices, drop degenerate / duplicate faces, fix winding (normals out)."""
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-4)
    seen, dup = set(), []
    bm.verts.index_update()
    for f in bm.faces:
        key = tuple(sorted(v.index for v in f.verts))
        if key in seen:
            dup.append(f)
        seen.add(key)
    if dup:
        bmesh.ops.delete(bm, geom=dup, context="FACES_ONLY")
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return len(dup)


def simplify(bm, angle_deg, by_material):
    """Limited dissolve + triangulate on a copy; returns (triangulated copy, polygon copy)."""
    poly = bm.copy()
    bmesh.ops.dissolve_limit(poly, angle_limit=math.radians(angle_deg), use_dissolve_boundaries=False,
                             verts=poly.verts, edges=poly.edges,
                             delimit={"MATERIAL"} if by_material else set())
    tri = poly.copy()
    bmesh.ops.triangulate(tri, faces=tri.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.dissolve_degenerate(tri, edges=tri.edges, dist=1e-5)
    return tri, poly


# ---- measuring ----------------------------------------------------------------------

def sample_surface(v, f, n):
    a, b, c = v[f[:, 0]], v[f[:, 1]], v[f[:, 2]]
    area = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)
    if not len(f) or area.sum() <= 0:
        return np.zeros((0, 3))
    idx = RNG.choice(len(f), size=n, p=area / area.sum())
    r1, r2 = RNG.random(n), RNG.random(n)
    s = np.sqrt(r1)
    return a[idx] * (1 - s)[:, None] + b[idx] * (s * (1 - r2))[:, None] + c[idx] * (s * r2)[:, None]


def bvh(v, f):
    return BVHTree.FromPolygons([tuple(p) for p in v], [tuple(int(i) for i in t) for t in f], epsilon=0.0)


def distances(tree, pts):
    out = np.empty(len(pts))
    for i, p in enumerate(pts):
        hit = tree.find_nearest(Vector(p))
        out[i] = hit[3] if hit[0] is not None else np.inf
    return out


def crease_points(v, f, step):
    """Points along sharp (>= CREASE_DEG) and open-boundary edges of a triangle mesh."""
    # Weld vertices at the same place first (triangle soups would make every edge a "boundary").
    _u, first, inv = np.unique(np.round(v / 1e-4).astype(np.int64), axis=0, return_index=True, return_inverse=True)
    f = inv.reshape(-1)[f]
    v = v[first]
    a, b, c = v[f[:, 0]], v[f[:, 1]], v[f[:, 2]]
    nrm = np.cross(b - a, c - a)
    ln = np.linalg.norm(nrm, axis=1)
    nrm = nrm / np.where(ln > 0, ln, 1)[:, None]
    edges = {}
    for fi, t in enumerate(f):
        for i in range(3):
            e = tuple(sorted((int(t[i]), int(t[(i + 1) % 3]))))
            edges.setdefault(e, []).append(fi)
    cos = math.cos(math.radians(CREASE_DEG))
    segs = []
    for e, fs in edges.items():
        if len(fs) != 2 or float(np.dot(nrm[fs[0]], nrm[fs[1]])) < cos:
            segs.append(e)
    pts = []
    for i, j in segs:
        p, q = v[i], v[j]
        n = max(1, int(math.ceil(np.linalg.norm(q - p) / step)))
        t = np.linspace(0, 1, n + 1)
        pts.append(p + (q - p) * t[:, None])
    return np.vstack(pts) if pts else np.zeros((0, 3))


def sample_with_faces(v, f, n):
    """Area-weighted surface points and the face each one lies on."""
    a, b, c = v[f[:, 0]], v[f[:, 1]], v[f[:, 2]]
    area = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)
    if not len(f) or area.sum() <= 0:
        return np.zeros((0, 3)), np.zeros(0, np.int64)
    idx = RNG.choice(len(f), size=n, p=area / area.sum())
    r1, r2 = RNG.random(n), RNG.random(n)
    s = np.sqrt(r1)
    pts = a[idx] * (1 - s)[:, None] + b[idx] * (s * (1 - r2))[:, None] + c[idx] * (s * r2)[:, None]
    return pts, idx


class Ref:
    """A reference surface prepared once: one BVH tree per material, samples, crease points."""

    def __init__(self, v, f, mats, n_samples, per_material=True):
        self.n, self.per_material = n_samples, per_material
        self.mats = mats if per_material else np.zeros(len(f), np.int64)
        self.trees = {int(k): bvh(v, f[self.mats == k]) for k in np.unique(self.mats)}
        self.samples, idx = sample_with_faces(v, f, n_samples)
        self.sample_mat = self.mats[idx]
        self.creases = crease_points(v, f, 0.25)


def measure(ref, new, check_creases=True):
    """Two-sided sampled distance between ref and new (per material when ref.per_material),
    plus the crease check. new = (verts, faces, face_mat)."""
    nv, nf, nm = new
    nm = nm if ref.per_material else np.zeros(len(nf), np.int64)
    inf = {"max": float("inf"), "p95": float("inf"), "crease_max": float("inf")}
    if not len(nf):
        return inf
    d = []
    for k in np.unique(np.concatenate([ref.sample_mat, nm])):
        sel = nm == k
        if k not in ref.trees or not sel.any():
            return inf                                   # a material vanished or appeared
        t_new = bvh(nv, nf[sel])
        d.append(distances(t_new, ref.samples[ref.sample_mat == k]))
    pts, idx = sample_with_faces(nv, nf, ref.n)
    pm = nm[idx]
    for k in np.unique(pm):
        d.append(distances(ref.trees[int(k)], pts[pm == k]))
    d = np.concatenate(d) if d else np.zeros(0)
    crease = 0.0
    if check_creases:
        rc = ref.creases
        nc = crease_points(nv, nf, 0.02)
        if len(rc) and len(nc):
            kd = KDTree(len(nc))
            for i, p in enumerate(nc):
                kd.insert(Vector(p), i)
            kd.balance()
            crease = max(kd.find(Vector(p))[2] for p in rc)
        elif len(rc):
            crease = float("inf")
    return {"max": float(d.max()) if len(d) else 0.0, "p95": float(np.percentile(d, 95)) if len(d) else 0.0,
            "crease_max": float(crease)}


# ---- UV0: one planar chart per merged polygon -----------------------------------------

def planar_uvs(poly_bm):
    """{face index: [(u, v) meters per loop]} with charts packed into 0..1 by vcmesh.pack_charts."""
    local, sizes = [], []
    for face in poly_bm.faces:
        n = face.normal.copy()
        if n.length < 1e-9:
            n = Vector((0, 0, 1))
        longest = max(face.edges, key=lambda e: e.calc_length())
        u = (longest.verts[1].co - longest.verts[0].co)
        u = (u - n * u.dot(n))
        if u.length < 1e-9:
            u = n.orthogonal()
        u.normalize()
        w = n.cross(u)
        o = face.verts[0].co
        pts = [((l.vert.co - o).dot(u), (l.vert.co - o).dot(w)) for l in face.loops]
        xs, ys = [p[0] for p in pts], [p[1] for p in pts]
        x0, y0 = min(xs), min(ys)
        local.append([(x - x0, y - y0) for x, y in pts])
        sizes.append((max(max(xs) - x0, 1e-3), max(max(ys) - y0, 1e-3)))
    offsets, side = vcmesh.pack_charts(sizes)
    return local, offsets, side


def visual_arrays(poly_bm):
    """Triangulate the merged polygons keeping their charts; returns VC mesh arrays."""
    local, offsets, side = planar_uvs(poly_bm)
    uvl = poly_bm.loops.layers.uv.new("UV0")
    for face, pts, off in zip(poly_bm.faces, local, offsets):
        for l, (x, y) in zip(face.loops, pts):
            l[uvl].uv = ((off[0] + x) / side, (off[1] + y) / side)
    bmesh.ops.triangulate(poly_bm, faces=poly_bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.dissolve_degenerate(poly_bm, edges=poly_bm.edges, dist=1e-5)
    uvl = poly_bm.loops.layers.uv["UV0"]
    v, f, mats = tri_arrays(poly_bm)
    uv = np.array([[l[uvl].uv[:] for l in face.loops] for face in poly_bm.faces], np.float32).reshape(-1, 3, 2)
    return v, f, mats, np.clip(uv, 0.0, 1.0), side


# ---- one mesh, one building -----------------------------------------------------------

def decimate(bm_tri, ratio, lock_materials=False):
    """Quadric collapse decimation (Blender's Decimate modifier) of a triangle bmesh copy.
    lock_materials: vertices on material borders, open edges and creases get vertex-group
    weight 0, so the decimation leaves them where they are (borders and creases stay)."""
    import bpy
    me = bpy.data.meshes.new("dec")
    bm_tri.to_mesh(me)
    ob = bpy.data.objects.new("dec", me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new("dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    if lock_materials:
        bm_tri.verts.index_update()
        lock = set()
        for e in bm_tri.edges:
            fs = e.link_faces
            if len(fs) != 2 or fs[0].material_index != fs[1].material_index or                     fs[0].normal.angle(fs[1].normal, 0.0) > math.radians(CREASE_DEG):
                lock.update(v.index for v in e.verts)
        vg = ob.vertex_groups.new(name="free")
        vg.add([v.index for v in bm_tri.verts if v.index not in lock], 1.0, "REPLACE")
        if lock:
            vg.add(sorted(lock), 0.0, "REPLACE")
        mod.vertex_group = "free"
        mod.vertex_group_factor = 1000.0
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    m2 = ev.to_mesh()
    out = bmesh.new()
    out.from_mesh(m2)
    ev.to_mesh_clear()
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(me, do_unlink=True)
    bmesh.ops.dissolve_degenerate(out, edges=out.edges, dist=1e-5)
    return out


def regroup(tri_bm, by_material):
    """Polygons for UV charts: merge triangles that are coplanar within 0.5 degrees."""
    poly = tri_bm.copy()
    bmesh.ops.dissolve_limit(poly, angle_limit=math.radians(0.5), use_dissolve_boundaries=False,
                             verts=poly.verts, edges=poly.edges, delimit={"MATERIAL"} if by_material else set())
    return poly


def tri_copy(bm):
    tri = bm.copy()
    bmesh.ops.triangulate(tri, faces=tri.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.dissolve_degenerate(tri, edges=tri.edges, dist=1e-5)
    return tri


def simplify_bounded(src, ref, tol, by_material, creases, extra=None):
    """Steps 2 + 3: (result polygon bmesh, info). The result is within tol of ref (measured);
    if not even the cleaned input passes, the cleaned input is returned unchanged."""
    cache = {}

    def ok(m):
        return m["max"] <= tol and (not creases or m["crease_max"] <= tol) and m.get("extra_max", 0.0) <= tol

    def judge(tri):
        a = tri_arrays(tri)
        m = measure(ref, a, creases)
        if extra is not None:
            m["extra_max"] = extra(a)
        return m

    def dissolve(k):
        if ("a", k) not in cache:
            tri, poly = simplify(src, ANGLES[k], by_material)
            poly.free()
            cache[("a", k)] = (tri, judge(tri))
        return cache[("a", k)]
    base = src.copy()
    angle = None
    if ok(dissolve(0)[1]):
        lo, hi = 0, len(ANGLES) - 1
        while lo < hi:                                   # error grows with the angle
            mid = (lo + hi + 1) // 2
            if ok(dissolve(mid)[1]):
                lo = mid
            else:
                hi = mid - 1
        angle = ANGLES[lo]
        base = dissolve(lo)[0].copy()
    base_n = len(base.faces)

    def at_ratio(r):
        if ("r", r) not in cache:
            tri = decimate(base, r, by_material) if r < 1.0 else base.copy()
            cache[("r", r)] = (tri, judge(tri))
        return cache[("r", r)]
    # Smallest ratio that still passes (bisection on a log scale, 8 steps).
    best_r, lo, hi = 1.0, max(0.005, 8.0 / max(base_n, 1)), 1.0
    if base_n > 8 and ok(at_ratio(lo)[1]):
        best_r = lo
    elif base_n > 8:
        for _ in range(8):
            mid = math.sqrt(lo * hi)
            if ok(at_ratio(mid)[1]):
                hi = best_r = mid
            else:
                lo = mid
    tri = at_ratio(best_r)[0] if best_r < 1.0 else base
    info = {"angle_deg": angle, "after_dissolve": base_n, "decimate_ratio": round(best_r, 4)}
    for k, (bmx, _m) in cache.items():
        if bmx is not tri:
            bmx.free()
    return tri, info


def directed_edge_repeats(f):
    e = np.vstack([f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]]])
    _u, counts = np.unique(e, axis=0, return_counts=True)
    return int((counts > 1).sum())


def area_of(m):
    v, f = m["verts"], m["faces"]
    return float(0.5 * np.linalg.norm(np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]]), axis=1).sum())


def process(job):
    t0 = time.time()
    res = {"id": job["id"], "problems": [], "suffix": job.get("suffix", ""), "tol_visual": job["tol_visual"],
           "tol_col": job["tol_col"]}
    lod2 = vc_fbx.load_npz(job["lod2"])
    n_samples = int(np.clip(area_of(lod2) * 3, 3000, 40000))

    # Visual: within tol_visual of lod2, per material, creases kept.
    if job.get("part") == "col" and Path(job["out_lod0"]).exists() and Path(job["stats"]).exists():
        old = vc_fbx.load_plan(job["stats"])          # keep the visual result, redo the collider only
        if "visual" in old:
            res["visual"] = old["visual"]
            res["problems"] += [q for q in old.get("problems", []) if q.startswith("visual")]
            job = dict(job, skip_visual=True)
    ref = Ref(lod2["verts"], lod2["faces"], lod2["face_mat"], n_samples)
    bm = to_bmesh(lod2) if not job.get("skip_visual") else None
    if bm is not None:
        res["visual_dupes_dropped"] = clean(bm)
    if bm is not None:
        tri, info = simplify_bounded(bm, ref, job["tol_visual"], True, True)
        poly = regroup(tri, True)
        v, f, mats, uv, side = visual_arrays(poly)
        final = measure(ref, (v, f, mats))
        if final["max"] > job["tol_visual"] or final["crease_max"] > job["tol_visual"]:
            # Re-triangulating the chart groups moved something: use one chart per triangle instead.
            poly.free()
            poly = tri.copy()
            v, f, mats, uv, side = visual_arrays(poly)
            final = measure(ref, (v, f, mats))
            info["uv_charts"] = "one per triangle"
        res["visual"] = dict(before=int(len(lod2["faces"])), after=int(len(f)), **info,
                             dev_max_m=round(final["max"], 4), dev_p95_m=round(final["p95"], 4),
                             crease_max_m=round(final["crease_max"], 4), uv_m_per_unit=round(side, 3))
        if info["angle_deg"] is None:
            res["problems"].append("visual: even the near-lossless merge exceeds the tolerance; cleaned input kept")
        if final["max"] > job["tol_visual"] or final["crease_max"] > job["tol_visual"]:
            res["problems"].append(f"visual deviation {final['max']:.3f} m / crease {final['crease_max']:.3f} m "
                                   f"> {job['tol_visual']} m")
        if sorted(set(mats.tolist())) != sorted(set(lod2["face_mat"].tolist())):
            res["problems"].append("visual: material set changed")
        vcmesh.save_mesh(job["out_lod0"], v, f, mats, uv,
                         {"source": "lod2.npz", "step": "buildings_game", "tolerance_m": job["tol_visual"], **info,
                          "uv": "planar chart per near-coplanar group (0.5 deg), vcmesh.pack_charts",
                          "uv_m_per_unit": side, "deviation_max_m": final["max"], "deviation_p95_m": final["p95"]})
        for x in (bm, tri, poly):
            x.free()

    # Collider: within tol_col of its input (col_mesh); lod2 -> collider reported.
    if job.get("col_mesh"):
        col = vc_fbx.load_npz(job["col_mesh"])
        cref = Ref(col["verts"], col["faces"], col["face_mat"], n_samples, per_material=False)
        lv, lf = lod2["verts"], lod2["faces"]
        nz = np.cross(lv[lf[:, 1]] - lv[lf[:, 0]], lv[lf[:, 2]] - lv[lf[:, 0]])
        up = nz[:, 2] / np.maximum(np.linalg.norm(nz, axis=1), 1e-12) > -0.9     # not the base cap
        seen = sample_with_faces(lv, lf[up], n_samples)[0]
        lod2_to_in = distances(bvh(col["verts"], col["faces"]), seen)
        # Also keep every visible lod2 point within tol of the collider (or within what the input
        # collider already had, if that was worse).
        slack = max(0.0, float(lod2_to_in.max()) - job["tol_col"])

        def lod2_gap(a):
            return float(distances(bvh(a[0], a[1]), seen).max()) - slack
        bm = to_bmesh(col)
        clean(bm)
        tri, cinfo = simplify_bounded(bm, cref, job["tol_col"], False, False, lod2_gap)
        bmesh.ops.recalc_face_normals(tri, faces=tri.faces)
        cv, cf, cm = tri_arrays(tri)
        final_c = measure(cref, (cv, cf, cm), False)
        lod2_to_col = distances(bvh(cv, cf), seen)
        rep = directed_edge_repeats(cf)
        res["collider"] = dict(before=int(len(col["faces"])), after=int(len(cf)), **cinfo,
                               dev_max_m=round(final_c["max"], 4), dev_p95_m=round(final_c["p95"], 4),
                               lod2_to_collider_max_m=round(float(lod2_to_col.max()), 4),
                               lod2_to_collider_p95_m=round(float(np.percentile(lod2_to_col, 95)), 4),
                               lod2_to_input_collider_max_m=round(float(lod2_to_in.max()), 4),
                               directed_edge_repeats=rep,
                               input_directed_edge_repeats=directed_edge_repeats(col["faces"]))
        if final_c["max"] > job["tol_col"]:
            res["problems"].append(f"collider deviation {final_c['max']:.3f} m > {job['tol_col']} m")
        if lod2_to_col.max() > job["tol_col"] + slack + 1e-3:
            res["problems"].append(f"collider: lod2 up to {lod2_to_col.max():.3f} m away from it")
        if rep:
            res["problems"].append(f"collider: {rep} directed edges used twice (non-manifold input)")
        a, b, c = cv[cf[:, 0]], cv[cf[:, 1]], cv[cf[:, 2]]
        degenerate = int((np.linalg.norm(np.cross(b - a, c - a), axis=1) < 1e-8).sum())
        if degenerate:
            res["problems"].append(f"collider: {degenerate} degenerate triangles")
        vcmesh.save_mesh(job["out_col"], cv, cf, cm.astype(np.uint8), np.zeros((len(cf), 3, 2), np.float32),
                         {"source": "col_mesh.npz", "step": "buildings_game", "tolerance_m": job["tol_col"],
                          **cinfo, "deviation_max_m": final_c["max"]})
        bm.free()
        tri.free()
    res["seconds"] = round(time.time() - t0, 1)
    vc_fbx.write_json(job["stats"], res)
    return res


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--jobs-file", required=True)
    args = p.parse_args(vc_fbx.blender_argv(sys.argv))
    jobs = vc_fbx.load_plan(args.jobs_file)["jobs"]
    failed = 0
    for job in jobs:
        try:
            r = process(job)
            print(f"GAME {job['id']} visual {r['visual']['before']}->{r['visual']['after']} "
                  f"col {r.get('collider', {}).get('before')}->{r.get('collider', {}).get('after')} "
                  f"{r['seconds']}s problems={len(r['problems'])}", flush=True)
        except Exception as e:
            failed += 1
            print(f"FAILED {job['id']}: {e!r}", flush=True)
            vc_fbx.write_json(job["stats"], {"id": job["id"], "problems": [f"crashed: {e!r}"], "crashed": True})
    print(f"DONE buildings={len(jobs)} failures={failed}", flush=True)
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
