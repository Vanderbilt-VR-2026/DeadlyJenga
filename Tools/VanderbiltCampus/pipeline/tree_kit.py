"""Placeholder stylized trees (low-poly candy style), generated in code (Stage 1).

8 kit meshes = 4 size classes x 2 shapes:
  size by tree height: small <= 7 m, medium <= 13 m, large <= 20 m, giant > 20 m
  shape: "round" broadleaf (faceted blobs) or "conical" evergreen (stacked 6-sided cones);
         conical when crown width / height < 0.35 (or OSM says leaf_type=needleleaved)
Each kit mesh has a tapered 6-sided trunk (material bark) and 1-3 blobs or cones (foliage).
LOD0 <= 60 triangles, LOD1 <= 16 triangles (still solid shapes, no crossed quads).

Kit meshes are "unit sized" in two parts:
  trunk:   radius 1 at the bottom, height 1  -> scaled to the trunk radius and trunk height
  foliage: x, y in -0.5..0.5, z in 0..1     -> scaled to crown width, from crown base to the top
tree_mesh(tree, lod) places one tree in the campus frame (meters, Z up).

UV0: every triangle is its own chart, laid out at true shape for a reference tree of the
class and packed without overlaps (vcmesh.pack_charts). A tree scaled differently from the
reference stretches its UVs a little; fine for a placeholder prop.

Run this file to print the triangle count of each kit mesh and check them.
"""
import argparse
import sys
import zlib
from functools import lru_cache
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import vcmesh  # noqa: E402

KIT_VERSION = 1
SIZES = [("small", 7.0), ("medium", 13.0), ("large", 20.0), ("giant", 1e9)]
CONICAL_RATIO = 0.35
# Reference trees used for the UV layout: (height, crown width, crown base).
REFERENCE = {"small": (5.0, 4.0, 1.5), "medium": (10.0, 7.0, 3.0), "large": (16.0, 11.0, 4.5),
             "giant": (25.0, 15.0, 6.0)}
REFERENCE_CONICAL = {"small": (5.0, 1.6, 1.0), "medium": (10.0, 3.0, 1.5), "large": (16.0, 5.0, 2.0),
                     "giant": (25.0, 7.5, 3.0)}
TRUNK_SINK = 0.3            # trunks start this far below the ground so they never float on slopes


# ---- primitives (convex pieces; each returns verts, faces, interior point) ----

def icosahedron(c, r):
    t = (1 + 5 ** 0.5) / 2
    v = np.array([[-1, t, 0], [1, t, 0], [-1, -t, 0], [1, -t, 0], [0, -1, t], [0, 1, t], [0, -1, -t], [0, 1, -t],
                  [t, 0, -1], [t, 0, 1], [-t, 0, -1], [-t, 0, 1]], float)
    v /= np.linalg.norm(v[0])
    f = [[0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11], [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6],
         [7, 1, 8], [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9], [4, 9, 5], [2, 4, 11], [6, 2, 10],
         [8, 6, 7], [9, 8, 1]]
    return v * np.asarray(r) + np.asarray(c), np.array(f), np.asarray(c, float)


def octahedron(c, r):
    v = np.array([[1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1]], float)
    f = [[0, 2, 4], [2, 1, 4], [1, 3, 4], [3, 0, 4], [2, 0, 5], [1, 2, 5], [3, 1, 5], [0, 3, 5]]
    return v * np.asarray(r) + np.asarray(c), np.array(f), np.asarray(c, float)


def bipyramid(c, r, n=6):
    """A faceted gem: a ring of n points with an apex above and below (2n triangles)."""
    a = np.linspace(0, 2 * np.pi, n, endpoint=False) + np.pi / n
    ring = np.column_stack([np.cos(a), np.sin(a), np.zeros(n)])
    v = np.vstack([ring, [[0, 0, 1], [0, 0, -1]]])
    f = [[i, (i + 1) % n, n] for i in range(n)] + [[(i + 1) % n, i, n + 1] for i in range(n)]
    return v * np.asarray(r) + np.asarray(c), np.array(f), np.asarray(c, float)


def cone(z0, radius, height, n=6, cx=0.0, cy=0.0):
    """n-sided cone standing on z0, with a flat (fan-free) base: n + (n - 2) triangles."""
    a = np.linspace(0, 2 * np.pi, n, endpoint=False)
    ring = np.column_stack([cx + radius * np.cos(a), cy + radius * np.sin(a), np.full(n, z0)])
    v = np.vstack([ring, [[cx, cy, z0 + height]]])
    f = [[i, (i + 1) % n, n] for i in range(n)] + [[0, i + 1, i] for i in range(1, n - 1)]
    return v, np.array(f), np.array([cx, cy, z0 + height / 4])


def frustum(n, r0, r1, z0=0.0, z1=1.0, caps=False):
    """n-sided tapered prism along Z (sides only unless caps=True)."""
    a = np.linspace(0, 2 * np.pi, n, endpoint=False)
    bot = np.column_stack([r0 * np.cos(a), r0 * np.sin(a), np.full(n, z0)])
    top = np.column_stack([r1 * np.cos(a), r1 * np.sin(a), np.full(n, z1)])
    v = np.vstack([bot, top])
    f = []
    for i in range(n):
        j = (i + 1) % n
        f += [[i, j, n + j], [i, n + j, n + i]]
    if caps:
        f += [[0, i + 1, i] for i in range(1, n - 1)]                 # bottom
        f += [[n, n + i, n + i + 1] for i in range(1, n - 1)]         # top
    return v, np.array(f), np.array([0.0, 0.0, (z0 + z1) / 2])


def orient(v, f, inside):
    """Flip triangles so they face away from the piece's interior point (counter-clockwise outside)."""
    a, b, c = v[f[:, 0]], v[f[:, 1]], v[f[:, 2]]
    n = np.cross(b - a, c - a)
    flip = (n * ((a + b + c) / 3 - inside)).sum(1) < 0
    f = f.copy()
    f[flip] = f[flip][:, [0, 2, 1]]
    return f


# ---- kit layouts ---------------------------------------------------------------
# Foliage pieces in foliage-unit space (x, y in -0.5..0.5, z in 0..1).

def _foliage_pieces(size, shape, lod):
    if lod == 1:
        if shape == "round":
            return [octahedron((0, 0, 0.5), (0.5, 0.5, 0.5))]
        return [cone(0.0, 0.5, 1.0, n=5)]
    if shape == "round":
        return {
            "small": [icosahedron((0, 0, 0.5), (0.5, 0.5, 0.5))],
            "medium": [icosahedron((-0.06, 0, 0.55), (0.44, 0.44, 0.45)),
                       bipyramid((0.17, 0.08, 0.36), (0.33, 0.33, 0.33))],
            "large": [icosahedron((-0.1, 0, 0.56), (0.4, 0.4, 0.44)),
                      icosahedron((0.15, 0.05, 0.4), (0.35, 0.35, 0.38))],
            "giant": [icosahedron((0, 0, 0.6), (0.4, 0.4, 0.4)),
                      bipyramid((0.22, 0.1, 0.34), (0.28, 0.28, 0.32)),
                      bipyramid((-0.2, -0.15, 0.33), (0.3, 0.3, 0.32))],
        }[size]
    return {
        "small": [cone(0.0, 0.5, 0.65), cone(0.42, 0.34, 0.58)],
        "medium": [cone(0.0, 0.5, 0.5), cone(0.3, 0.39, 0.42), cone(0.58, 0.27, 0.42)],
        "large": [cone(0.0, 0.5, 0.46), cone(0.3, 0.4, 0.4), cone(0.56, 0.28, 0.44)],
        "giant": [cone(0.0, 0.5, 0.42), cone(0.28, 0.41, 0.4), cone(0.55, 0.3, 0.45)],
    }[size]


def _trunk_piece(lod):
    if lod == 1:
        return frustum(3, 1.0, 0.6)
    return frustum(6, 1.0, 0.6)


def size_class(height):
    for name, top in SIZES:
        if height <= top:
            return name
    return "giant"


def shape_of(tree):
    tags = tree.get("osm_tags") or {}
    if tags.get("leaf_type") == "needleleaved":
        return "conical"
    if tree["crown_diameter"] / max(tree["height"], 0.1) < CONICAL_RATIO:
        return "conical"
    return "round"


def trunk_radius(height):
    """Estimated trunk radius: diameter = 0.03 x height, at least 0.2 m."""
    return max(0.2, 0.03 * height) / 2


def dims(tree):
    """(height, crown width, crown base, trunk radius, trunk top) used to scale the kit."""
    h = max(float(tree["height"]), 1.5)
    w = max(float(tree["crown_diameter"]), 1.0)
    b = float(np.clip(tree["crown_base"], 0.3, 0.8 * h))
    return h, w, b, trunk_radius(h), b + 0.4 * (h - b)


@lru_cache(maxsize=None)
def kit_mesh(size, shape, lod):
    """The unit kit mesh: dict with verts, faces, face_mat, uv0 and 'part' (0 trunk, 1 foliage)."""
    mats = vcmesh.material_index()
    pieces = [(_trunk_piece(lod), 0)] + [(p, 1) for p in _foliage_pieces(size, shape, lod)]
    V, F, part = [], [], []
    off = 0
    for (v, f, inside), prt in pieces:
        F.append(orient(v, f, inside) + off)
        V.append(v)
        part.append(np.full(len(v), prt))
        off += len(v)
    V, F, part = np.vstack(V), np.vstack(F), np.concatenate(part)
    # Stretch the foliage so it fills the unit box exactly (width 1, z from 0 to 1): then a
    # placed tree reaches its measured height and crown width.
    fol = part == 1
    lo, hi = V[fol].min(0), V[fol].max(0)
    V[fol, 0] = (V[fol, 0] - (lo[0] + hi[0]) / 2) / (hi[0] - lo[0])
    V[fol, 1] = (V[fol, 1] - (lo[1] + hi[1]) / 2) / (hi[1] - lo[1])
    V[fol, 2] = (V[fol, 2] - lo[2]) / (hi[2] - lo[2])
    face_part = part[F[:, 0]]
    face_mat = np.where(face_part == 0, mats["bark"], mats["foliage"]).astype(np.uint8)
    ref = (REFERENCE if shape == "round" else REFERENCE_CONICAL)[size]
    tree = {"height": ref[0], "crown_diameter": ref[1], "crown_base": ref[2]}
    uv0 = triangle_uvs(place(V, part, tree)[F])
    return {"verts": V, "faces": F, "face_mat": face_mat, "uv0": uv0, "part": part}


def triangle_uvs(tris):
    """One true-shape chart per triangle, packed into 0..1 without overlaps."""
    local, sizes = [], []
    for a, b, c in tris:
        e1 = b - a
        L = np.linalg.norm(e1)
        u = e1 / L
        nrm = np.cross(e1, c - a)
        w = np.cross(nrm, u)
        w /= np.linalg.norm(w)
        p = np.array([[0.0, 0.0], [L, 0.0], [np.dot(c - a, u), np.dot(c - a, w)]])
        p -= p.min(0)
        local.append(p)
        sizes.append(tuple(p.max(0)))
    offsets, side = vcmesh.pack_charts(sizes, padding=0.04)
    uv = np.array([(p + np.asarray(o)) / side for p, o in zip(local, offsets)], np.float32)
    return np.clip(uv, 0, 1)


def place(V, part, tree, yaw=0.0, origin=(0.0, 0.0, 0.0)):
    """Scale the unit kit to the tree, rotate by yaw and move it to origin (x, y, ground z)."""
    h, w, b, tr, ttop = dims(tree)
    out = np.empty_like(V, dtype=np.float64)
    trunk = part == 0
    out[trunk, 0] = V[trunk, 0] * tr
    out[trunk, 1] = V[trunk, 1] * tr
    out[trunk, 2] = -TRUNK_SINK + V[trunk, 2] * (ttop + TRUNK_SINK)
    fol = ~trunk
    out[fol, 0] = V[fol, 0] * w
    out[fol, 1] = V[fol, 1] * w
    out[fol, 2] = b + V[fol, 2] * (h - b)
    if yaw:
        c, s = np.cos(yaw), np.sin(yaw)
        x, y = out[:, 0].copy(), out[:, 1].copy()
        out[:, 0], out[:, 1] = c * x - s * y, s * x + c * y
    return out + np.asarray(origin, float)


def yaw_of(tree):
    """A fixed pseudo-random rotation per tree id, so the kit does not look copy-pasted."""
    return (zlib.crc32(str(tree.get("id", "")).encode()) % 3600) / 3600 * 2 * np.pi


def tree_mesh(tree, lod=0):
    """Mesh dict for one tree in the campus frame. lod 0 (<= 60 tris) or 1 (<= 16 tris)."""
    k = kit_mesh(size_class(float(tree["height"])), shape_of(tree), lod)
    v = place(k["verts"], k["part"], tree, yaw_of(tree), (tree["x"], tree["y"], tree["ground_z"]))
    return {"verts": v, "faces": k["faces"].copy(), "face_mat": k["face_mat"].copy(), "uv0": k["uv0"].copy(),
            "meta": {}}


def collider_mesh(tree):
    """Trunk collider: closed 6-sided prism from below the ground to the crown base."""
    h, w, b, tr, ttop = dims(tree)
    v, f, inside = frustum(6, tr, tr, -TRUNK_SINK, b, caps=True)
    f = orient(v, f, inside)
    v = v + np.array([tree["x"], tree["y"], tree["ground_z"]])
    uv = _collider_uv()
    return {"verts": v, "faces": f, "face_mat": np.full(len(f), vcmesh.material_index()["bark"], np.uint8),
            "uv0": uv.copy(), "meta": {}}


@lru_cache(maxsize=None)
def _collider_uv():
    v, f, inside = frustum(6, 0.2, 0.2, 0.0, 3.0, caps=True)
    return triangle_uvs(v[orient(v, f, inside)])


def all_kits():
    return {(s, sh, lod): kit_mesh(s, sh, lod) for s, _ in SIZES for sh in ("round", "conical") for lod in (0, 1)}


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.parse_args(argv)
    bad = 0
    for (s, sh, lod), k in all_kits().items():
        probs = vcmesh.check_mesh(k)
        ov = vcmesh.uv_overlap_area(k["uv0"], grid=512)
        limit = 60 if lod == 0 else 16
        ok = not probs and len(k["faces"]) <= limit and ov < 0.02
        bad += not ok
        print(f"{s:7s} {sh:8s} LOD{lod}: {len(k['faces']):3d} tris (limit {limit}), uv overlap {ov:.4f} "
              f"{'ok' if ok else 'PROBLEM ' + str(probs)}")
    if bad:
        sys.exit(1)


if __name__ == "__main__":
    main()
