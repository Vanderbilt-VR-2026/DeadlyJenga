"""The pipeline's mesh file ("VC mesh", a NumPy .npz) and helpers every step shares.

One file holds one triangle mesh in the campus frame (meters, Z up):
  verts     float64 (N, 3)   positions
  faces     int32   (M, 3)   vertex indices, counter-clockwise seen from outside
  face_mat  uint8   (M,)     material index from config/material_ids.json
  uv0       float32 (M, 3, 2) one UV per triangle corner, inside 0..1, charts never overlap
  meta      JSON text        free notes (uv meters-per-unit, source, version...)

UV0 is the unwrap a later texture bake will use (STANDARDS.md section 7), so it must
be real: every chart keeps its true shape and the same texel density.
"""
import json
from pathlib import Path

import numpy as np

from common import CONFIG, load_json


def material_index():
    """Material id -> index, from config/material_ids.json."""
    return {m["id"]: m["index"] for m in load_json(CONFIG / "material_ids.json")["ids"]}


def save_mesh(path, verts, faces, face_mat, uv0, meta=None):
    verts = np.asarray(verts, np.float64).reshape(-1, 3)
    faces = np.asarray(faces, np.int32).reshape(-1, 3)
    face_mat = np.asarray(face_mat, np.uint8).reshape(-1)
    uv0 = np.asarray(uv0, np.float32).reshape(-1, 3, 2)
    if len(face_mat) != len(faces) or len(uv0) != len(faces):
        raise ValueError("face_mat and uv0 need one entry per face")
    if len(faces) and faces.max() >= len(verts):
        raise ValueError("face index past the vertex list")
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    np.savez_compressed(path, verts=verts, faces=faces, face_mat=face_mat, uv0=uv0,
                        meta=json.dumps(meta or {}))


def load_mesh(path):
    with np.load(path, allow_pickle=False) as z:
        return {"verts": z["verts"], "faces": z["faces"], "face_mat": z["face_mat"],
                "uv0": z["uv0"], "meta": json.loads(str(z["meta"]))}


def merge(meshes):
    """Join several mesh dicts into one (vertex indices are shifted)."""
    verts, faces, mats, uvs, off = [], [], [], [], 0
    for m in meshes:
        verts.append(m["verts"]); faces.append(m["faces"] + off)
        mats.append(m["face_mat"]); uvs.append(m["uv0"]); off += len(m["verts"])
    if not verts:
        return {"verts": np.zeros((0, 3)), "faces": np.zeros((0, 3), np.int32),
                "face_mat": np.zeros(0, np.uint8), "uv0": np.zeros((0, 3, 2), np.float32), "meta": {}}
    return {"verts": np.vstack(verts), "faces": np.vstack(faces), "face_mat": np.concatenate(mats),
            "uv0": np.vstack(uvs), "meta": {}}


def pack_charts(sizes, padding=0.05):
    """Shelf-pack rectangles (width, height in meters) into one square atlas.

    Returns (offsets, side): the lower-left corner of each rectangle in meters and the
    atlas side length. Divide (offset + local) by side to get UVs in 0..1. Every chart
    keeps the same meters-per-UV scale, so texel density is uniform.
    """
    sizes = [(float(w) + padding, float(h) + padding) for w, h in sizes]
    if not sizes:
        return [], 1.0
    order = sorted(range(len(sizes)), key=lambda i: -sizes[i][1])
    side = max(np.sqrt(sum(w * h for w, h in sizes)) * 1.1, max(w for w, _ in sizes), max(h for _, h in sizes))
    while True:
        offsets, x, y, shelf = [None] * len(sizes), 0.0, 0.0, 0.0
        ok = True
        for i in order:
            w, h = sizes[i]
            if x + w > side:            # start a new shelf
                x, y, shelf = 0.0, y + shelf, 0.0
            if y + h > side:
                ok = False
                break
            offsets[i] = (x + padding / 2, y + padding / 2)
            x += w
            shelf = max(shelf, h)
        if ok:
            return offsets, side
        side *= 1.08


def uv_overlap_area(uv0, grid=1024):
    """Rough overlap test: rasterize every UV triangle; return the fraction of covered
    texels hit by triangles of more than one chart-less triangle soup. Triangles that only
    share an edge add a little noise, so treat < 0.02 as "no overlap"."""
    import cv2
    count = np.zeros((grid, grid), np.uint16)
    tri = np.zeros((grid, grid), np.uint8)
    for t in np.asarray(uv0, np.float64):
        pts = np.round(t * (grid - 1) * 16).astype(np.int32)   # 4 fractional bits
        tri[:] = 0
        cv2.fillConvexPoly(tri, pts, 1, lineType=cv2.LINE_8, shift=4)
        count += cv2.erode(tri, np.ones((3, 3), np.uint8))       # shrink so shared edges don't count
    covered = (count > 0).sum()
    return float((count > 1).sum()) / max(int(covered), 1)


def check_mesh(mesh, need_watertight=False):
    """Return a list of problems (empty = fine). Used by tests and by every exporter."""
    problems = []
    v, f, uv = mesh["verts"], mesh["faces"], mesh["uv0"]
    if not len(f):
        return ["no faces"]
    if not np.isfinite(v).all():
        problems.append("non-finite vertex")
    if uv.min() < -1e-6 or uv.max() > 1 + 1e-6:
        problems.append("uv0 outside 0..1")
    known = set(material_index().values())
    if not set(np.unique(mesh["face_mat"]).tolist()) <= known:
        problems.append("unknown material index")
    a, b, c = v[f[:, 0]], v[f[:, 1]], v[f[:, 2]]
    if (np.linalg.norm(np.cross(b - a, c - a), axis=1) < 1e-10).any():
        problems.append("degenerate triangle")
    if need_watertight:
        import trimesh
        tm = trimesh.Trimesh(v, f, process=True)       # process merges duplicate vertices
        if not tm.is_watertight:
            problems.append("not watertight")
    return problems
