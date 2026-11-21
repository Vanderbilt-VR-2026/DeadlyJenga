"""Street prop library, modeled once in code (Stage 5).

Kinds: bench, lamp_post, bin, bike_rack, bollard, planter, sign_post, fountain_basin,
fountain_tier, statue, flagpole.

Every kind has three meshes (VC mesh dicts, see vcmesh.py):
  LOD0  the stylized prop, roughly 100-600 triangles
  LOD1  a far version, under 60 triangles
  COL   a simple closed collider (boxes / prisms), never rendered
Local frame of a prop: meters, origin on the ground at the prop's center, Z up, the prop's
front faces local +Y (a bench's sitter looks toward +Y; a lamp arm points to +Y).
Placed instances rotate by heading_deg (counter-clockwise about Z, 0 = front faces campus +Y).

Materials come from config/material_ids.json. Sign faces and flags use signage_blank (no
text, no logos). UV0: one true-shape chart per triangle, packed without overlaps.

Run this file to print triangle counts and check every mesh.
"""
import argparse
import sys
from functools import lru_cache
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import tree_kit as tk  # noqa: E402  (primitives, orient, triangle_uvs)
import vcmesh  # noqa: E402

KIT_VERSION = 1
KINDS = ["bench", "lamp_post", "bin", "bike_rack", "bollard", "planter", "sign_post", "fountain_basin",
         "fountain_tier", "statue", "flagpole"]
LOD0_RANGE = (100, 600)
LOD1_MAX = 59


# ---- primitives: each returns (verts, faces, interior point, material or per-face materials) ----

def _rot(v, rx=0.0, rz=0.0):
    """Rotate points about X (tilt) then Z (turn), angles in degrees."""
    a, b = np.radians(rx), np.radians(rz)
    Rx = np.array([[1, 0, 0], [0, np.cos(a), -np.sin(a)], [0, np.sin(a), np.cos(a)]])
    Rz = np.array([[np.cos(b), -np.sin(b), 0], [np.sin(b), np.cos(b), 0], [0, 0, 1]])
    return v @ Rx.T @ Rz.T


def box(center, size, mat, rx=0.0, rz=0.0, face_mats=None):
    """Box of size (sx, sy, sz) at center. face_mats: dict like {"+y": m, "-y": m} for some faces."""
    sx, sy, sz = np.asarray(size) / 2
    v = np.array([[x, y, z] for z in (-sz, sz) for y in (-sy, sy) for x in (-sx, sx)], float)
    quads = {"-z": [0, 2, 3, 1], "+z": [4, 5, 7, 6], "-y": [0, 1, 5, 4], "+y": [2, 6, 7, 3],
             "-x": [0, 4, 6, 2], "+x": [1, 3, 7, 5]}
    f, m = [], []
    for name, q in quads.items():
        f += [[q[0], q[1], q[2]], [q[0], q[2], q[3]]]
        mm = (face_mats or {}).get(name, mat)
        m += [mm, mm]
    v = _rot(v, rx, rz) + np.asarray(center, float)
    return v, np.array(f), np.asarray(center, float), np.array(m)


def prism(n, r0, r1, z0, z1, mat, center=(0.0, 0.0), rot0=0.0):
    """Closed n-sided tapered prism standing on z0 (caps included)."""
    v, f, inside = tk.frustum(n, r0, r1, z0, z1, caps=True)
    if rot0:
        v = _rot(v, 0, rot0)
    off = np.array([center[0], center[1], 0.0])
    return v + off, f, inside + off, mat


def blob(center, radii, mat, kind="ico"):
    v, f, inside = (tk.icosahedron if kind == "ico" else tk.octahedron)(center, radii)
    return v, f, inside, mat


def gem(center, radii, mat, n=6):
    v, f, inside = tk.bipyramid(center, radii, n)
    return v, f, inside, mat


def wedge(r_in, r_out, a0, a1, z0, z1, mat):
    """One convex segment of a ring wall (8 corners)."""
    pts = []
    for z in (z0, z1):
        for r, a in ((r_in, a0), (r_out, a0), (r_out, a1), (r_in, a1)):
            pts.append([r * np.cos(a), r * np.sin(a), z])
    v = np.array(pts)
    f = [[0, 2, 1], [0, 3, 2], [4, 5, 6], [4, 6, 7], [0, 1, 5], [0, 5, 4], [1, 2, 6], [1, 6, 5],
         [2, 3, 7], [2, 7, 6], [3, 0, 4], [3, 4, 7]]
    return v, np.array(f), v.mean(0), mat


def ring(n, r_in, r_out, z0, z1, mat):
    return [wedge(r_in, r_out, 2 * np.pi * i / n, 2 * np.pi * (i + 1) / n, z0, z1, mat) for i in range(n)]


# ---- assembling -------------------------------------------------------------------

def assemble(parts):
    """Join parts into one VC mesh dict with outward faces, material ids and packed UV0."""
    mats = vcmesh.material_index()
    V, F, M, off = [], [], [], 0
    for v, f, inside, mat in parts:
        f = tk.orient(np.asarray(v, float), np.asarray(f), np.asarray(inside, float))
        V.append(np.asarray(v, float)); F.append(f + off); off += len(v)
        if isinstance(mat, str):
            M.append(np.full(len(f), mats[mat], np.uint8))
        else:
            M.append(np.array([mats[m] for m in mat], np.uint8))
    V, F, M = np.vstack(V), np.vstack(F).astype(np.int32), np.concatenate(M)
    return {"verts": V, "faces": F, "face_mat": M, "uv0": tk.triangle_uvs(V[F]), "meta": {}}


# ---- the props ----------------------------------------------------------------------
# Each builder returns (lod0 parts, lod1 parts, collider parts).

def bench():
    L, W = 1.8, 0.62
    lod0 = []
    for i, y in enumerate((-0.2, -0.05, 0.1)):                       # seat slats
        lod0.append(box((0, y, 0.45), (L, 0.12, 0.04), "wood"))
    for z in (0.62, 0.76):                                            # back slats, tilted back
        lod0.append(box((0, -0.29 - (z - 0.55) * 0.18, z), (L, 0.04, 0.11), "wood", rx=-12))
    for x in (-0.75, 0.75):                                           # cast frames
        lod0 += [box((x, 0.12, 0.215), (0.06, 0.06, 0.43), "metal_trim"),        # front leg
                 box((x, -0.25, 0.37), (0.06, 0.06, 0.74), "metal_trim", rx=-8),  # back leg + back support
                 box((x, -0.05, 0.42), (0.06, 0.5, 0.04), "metal_trim"),          # seat rail
                 box((x, -0.02, 0.62), (0.07, 0.42, 0.05), "metal_trim"),         # arm rest
                 box((x, 0.15, 0.53), (0.05, 0.05, 0.18), "metal_trim")]          # arm post
    lod0.append(box((0, -0.27, 0.25), (L - 0.1, 0.04, 0.04), "metal_trim"))     # stretcher
    lod1 = [box((0, -0.05, 0.45), (L, 0.44, 0.05), "wood"), box((0, -0.3, 0.68), (L, 0.05, 0.3), "wood", rx=-12),
            box((-0.75, -0.05, 0.22), (0.06, 0.44, 0.44), "metal_trim"),
            box((0.75, -0.05, 0.22), (0.06, 0.44, 0.44), "metal_trim")]
    col = [box((0, -0.05, 0.42), (L, W, 0.84), "metal_trim")]
    return lod0, lod1, col


def lamp_post():
    lod0 = [prism(8, 0.16, 0.12, 0.0, 0.5, "metal_trim", rot0=22.5),       # base
            prism(8, 0.06, 0.05, 0.5, 3.6, "metal_trim", rot0=22.5),       # pole
            prism(8, 0.09, 0.09, 1.2, 1.3, "metal_trim", rot0=22.5),       # collar
            prism(8, 0.10, 0.07, 3.6, 3.75, "metal_trim", rot0=22.5),      # lantern seat
            prism(8, 0.13, 0.19, 3.75, 4.15, "glass", rot0=22.5),          # lantern glass
            prism(8, 0.24, 0.05, 4.15, 4.35, "metal_trim", rot0=22.5),     # cap
            blob((0, 0, 4.42), (0.05, 0.05, 0.07), "metal_trim")]          # finial
    lod1 = [prism(4, 0.08, 0.06, 0.0, 3.7, "metal_trim", rot0=45),
            prism(4, 0.17, 0.2, 3.7, 4.15, "glass", rot0=45),
            prism(4, 0.22, 0.04, 4.15, 4.35, "metal_trim", rot0=45)]
    col = [prism(6, 0.14, 0.14, 0.0, 4.3, "metal_trim")]
    return lod0, lod1, col


def bin_():
    lod0 = [prism(10, 0.26, 0.29, 0.0, 0.08, "concrete"),
            prism(10, 0.25, 0.28, 0.08, 0.88, "metal_panel"),
            prism(10, 0.30, 0.30, 0.88, 0.94, "metal_trim"),
            prism(10, 0.28, 0.12, 0.94, 1.02, "metal_trim"),
            prism(10, 0.13, 0.13, 1.02, 1.04, "metal_trim")]
    lod1 = [prism(6, 0.27, 0.29, 0.0, 0.94, "metal_panel"), prism(6, 0.29, 0.1, 0.94, 1.03, "metal_trim")]
    col = [prism(6, 0.3, 0.3, 0.0, 1.04, "metal_panel")]
    return lod0, lod1, col


def bike_rack():
    lod0 = []
    for x in (-0.9, 0.0, 0.9):                                   # three inverted-U hoops
        for y in (-0.33, 0.33):
            lod0.append(box((x, y, 0.38), (0.05, 0.05, 0.76), "metal_trim"))
        for s in (-1, 1):
            lod0.append(box((x, s * 0.255, 0.81), (0.05, 0.22, 0.05), "metal_trim", rx=-s * 34))
        lod0.append(box((x, 0, 0.86), (0.05, 0.36, 0.05), "metal_trim"))
    for y in (-0.33, 0.33):                                      # floor rails
        lod0.append(box((0, y, 0.015), (2.0, 0.08, 0.03), "metal_trim"))
    lod1 = [box((x, 0, 0.43), (0.05, 0.72, 0.86), "metal_trim") for x in (-0.9, 0.0, 0.9)] + \
           [box((0, 0, 0.015), (2.0, 0.74, 0.03), "metal_trim")]
    col = [box((0, 0, 0.44), (1.9, 0.75, 0.88), "metal_trim")]
    return lod0, lod1, col


def bollard():
    lod0 = [prism(12, 0.13, 0.12, 0.0, 0.75, "metal_trim"),
            prism(12, 0.125, 0.125, 0.55, 0.62, "metal_panel"),
            prism(12, 0.12, 0.06, 0.75, 0.86, "metal_trim"),
            prism(12, 0.06, 0.0005, 0.86, 0.9, "metal_trim")]
    lod1 = [prism(6, 0.13, 0.12, 0.0, 0.8, "metal_trim"), prism(6, 0.12, 0.01, 0.8, 0.9, "metal_trim")]
    col = [prism(6, 0.14, 0.14, 0.0, 0.9, "metal_trim")]
    return lod0, lod1, col


def planter():
    s, h = 1.2, 0.6
    lod0 = [box((0, 0, 0.05), (s + 0.08, s + 0.08, 0.1), "concrete"),          # plinth
            box((0, 0, 0.3), (s - 0.2, s - 0.2, 0.4), "concrete")]             # core
    for i, (cx, cy, sx, sy) in enumerate(((0, (s - 0.1) / 2, s, 0.1), (0, -(s - 0.1) / 2, s, 0.1),
                                          ((s - 0.1) / 2, 0, 0.1, s - 0.2), (-(s - 0.1) / 2, 0, 0.1, s - 0.2))):
        lod0.append(box((cx, cy, h / 2), (sx, sy, h), "concrete"))             # walls
    lod0 += [box((0, 0, h - 0.06), (s - 0.2, s - 0.2, 0.04), "planting"),      # soil top
             blob((0.05, 0.05, h + 0.32), (0.42, 0.42, 0.34), "planting"),    # shrub
             gem((-0.22, -0.2, h + 0.18), (0.25, 0.25, 0.2), "planting"),
             gem((0.25, -0.2, h + 0.15), (0.22, 0.22, 0.18), "planting")]
    lod1 = [box((0, 0, h / 2), (s, s, h), "concrete"), blob((0, 0, h + 0.3), (0.45, 0.45, 0.35), "planting", "oct")]
    col = [box((0, 0, h / 2), (s, s, h), "concrete")]
    return lod0, lod1, col


def sign_post():
    face = {"+y": "signage_blank", "-y": "signage_blank"}
    lod0 = [prism(8, 0.045, 0.04, 0.0, 2.25, "metal_trim", center=(-0.45, 0), rot0=22.5),
            prism(8, 0.045, 0.04, 0.0, 2.25, "metal_trim", center=(0.45, 0), rot0=22.5),
            blob((-0.45, 0, 2.28), (0.05, 0.05, 0.05), "metal_trim", "oct"),
            blob((0.45, 0, 2.28), (0.05, 0.05, 0.05), "metal_trim", "oct"),
            box((0, 0, 1.6), (1.1, 0.05, 0.75), "metal_trim", face_mats=face),         # panel: blank faces
            box((0, 0, 2.02), (1.16, 0.08, 0.06), "metal_trim"),                        # top rail
            box((0, 0, 1.19), (1.16, 0.08, 0.06), "metal_trim"),                        # bottom rail
            box((0, 0, 0.04), (1.2, 0.3, 0.08), "concrete")]                            # footing
    lod1 = [box((-0.45, 0, 1.12), (0.08, 0.08, 2.25), "metal_trim"),
            box((0.45, 0, 1.12), (0.08, 0.08, 2.25), "metal_trim"),
            box((0, 0, 1.6), (1.1, 0.06, 0.8), "metal_trim", face_mats=face)]
    col = [box((0, 0, 1.15), (1.2, 0.2, 2.3), "metal_trim")]
    return lod0, lod1, col


def fountain_basin():
    """Round basin of unit radius 2 m (instances scale it to the OSM size)."""
    R, wall, h = 2.0, 0.3, 0.55
    lod0 = ring(16, R - wall, R, 0.0, h, "limestone") + ring(16, R - 0.02, R + 0.12, h - 0.04, h + 0.05, "limestone")
    lod0 += [prism(16, R - wall + 0.01, R - wall + 0.01, -0.05, 0.12, "limestone", rot0=11.25),   # floor
             prism(16, R - wall + 0.005, R - wall + 0.005, 0.12, h - 0.12, "water", rot0=11.25)]  # water
    lod1 = [prism(6, R, R, 0.0, h - 0.1, "limestone"),                  # solid drum; water disk on top
            prism(6, R - wall, R - wall, h - 0.12, h - 0.07, "water")]
    col = [prism(8, R + 0.1, R + 0.1, 0.0, h, "limestone")]
    return lod0, lod1, col


def fountain_tier():
    """Center piece: pedestal, upper bowl with water, spout."""
    lod0 = [prism(12, 0.45, 0.4, 0.0, 0.25, "limestone"),
            prism(12, 0.22, 0.16, 0.25, 1.1, "limestone"),
            prism(12, 0.18, 0.75, 1.1, 1.35, "limestone"),                  # bowl underside
            *ring(12, 0.66, 0.78, 1.35, 1.47, "limestone"),                 # bowl rim
            prism(12, 0.67, 0.67, 1.33, 1.43, "water", rot0=15),            # water inside the rim
            prism(8, 0.08, 0.05, 1.42, 1.8, "limestone", rot0=22.5),
            blob((0, 0, 1.88), (0.12, 0.12, 0.1), "water")]
    lod1 = [prism(5, 0.4, 0.18, 0.0, 1.1, "limestone"), prism(5, 0.18, 0.75, 1.1, 1.45, "limestone"),
            prism(5, 0.08, 0.04, 1.45, 1.85, "limestone")]
    col = [prism(6, 0.45, 0.45, 0.0, 1.1, "limestone"), prism(8, 0.8, 0.8, 1.1, 1.47, "limestone")]
    return lod0, lod1, col


def statue():
    """Simplified figure on a plinth (a stand-in: real statues are not modeled)."""
    lod0 = [box((0, 0, 0.15), (1.5, 1.5, 0.3), "limestone"),
            box((0, 0, 0.95), (1.1, 1.1, 1.3), "limestone"),
            box((0, 0, 1.66), (1.25, 1.25, 0.12), "limestone"),
            box((0.12, 0.05, 2.12), (0.2, 0.24, 0.8), "stone_rough"),           # legs
            box((-0.12, -0.03, 2.12), (0.2, 0.24, 0.8), "stone_rough"),
            box((0, 0, 2.85), (0.56, 0.32, 0.7), "stone_rough"),                # torso
            box((0.36, 0.06, 2.8), (0.14, 0.16, 0.66), "stone_rough", rx=-15),  # arms
            box((-0.36, 0.0, 2.85), (0.14, 0.16, 0.62), "stone_rough", rx=10),
            box((0, 0, 3.27), (0.14, 0.14, 0.1), "stone_rough"),                # neck
            blob((0, 0.02, 3.47), (0.15, 0.17, 0.19), "stone_rough"),           # head
            box((0, 0, 1.72), (0.7, 0.5, 0.06), "stone_rough")]                 # figure base
    lod1 = [box((0, 0, 0.88), (1.3, 1.3, 1.75), "limestone"), box((0, 0, 2.55), (0.55, 0.35, 1.6), "stone_rough"),
            box((0, 0, 3.47), (0.3, 0.32, 0.36), "stone_rough")]
    col = [box((0, 0, 0.86), (1.5, 1.5, 1.72), "limestone"), box((0, 0, 2.62), (0.7, 0.45, 1.8), "stone_rough")]
    return lod0, lod1, col


def flagpole():
    lod0 = [prism(8, 0.35, 0.3, 0.0, 0.3, "concrete", rot0=22.5),
            prism(8, 0.08, 0.04, 0.3, 10.0, "metal_trim", rot0=22.5),
            blob((0, 0, 10.1), (0.1, 0.1, 0.1), "metal_trim"),
            box((0.78, 0, 9.35), (1.5, 0.02, 1.0), "signage_blank"),           # blank flag
            prism(6, 0.05, 0.05, 1.2, 1.35, "metal_trim")]                     # cleat
    lod1 = [prism(4, 0.08, 0.04, 0.0, 10.05, "metal_trim", rot0=45), box((0.78, 0, 9.35), (1.5, 0.02, 1.0), "signage_blank")]
    col = [prism(6, 0.3, 0.3, 0.0, 10.0, "metal_trim")]
    return lod0, lod1, col


BUILDERS = {"bench": bench, "lamp_post": lamp_post, "bin": bin_, "bike_rack": bike_rack, "bollard": bollard,
            "planter": planter, "sign_post": sign_post, "fountain_basin": fountain_basin,
            "fountain_tier": fountain_tier, "statue": statue, "flagpole": flagpole}


@lru_cache(maxsize=None)
def prop_mesh(kind, lod):
    """lod: 0, 1 or "col". Returns a VC mesh dict in the prop's local frame (cached)."""
    lod0, lod1, col = BUILDERS[kind]()
    parts = {0: lod0, 1: lod1, "col": col}[lod]
    m = assemble(parts)
    m["meta"] = {"kind": kind, "lod": str(lod), "kit_version": KIT_VERSION,
                 "frame": "prop local: origin on the ground, Z up, front = +Y, meters"}
    return m


def placed(kind, lod, x, y, z, heading_deg=0.0, scale=1.0):
    """The prop mesh moved into the campus frame."""
    m = prop_mesh(kind, lod)
    v = _rot(m["verts"] * scale, 0, heading_deg) + np.array([x, y, z])
    return {"verts": v, "faces": m["faces"].copy(), "face_mat": m["face_mat"].copy(), "uv0": m["uv0"].copy(),
            "meta": {}}


def check_all():
    rows, bad = [], 0
    for k in KINDS:
        for lod in (0, 1, "col"):
            m = prop_mesh(k, lod)
            probs = vcmesh.check_mesh(m, need_watertight=(lod == "col"))
            n = len(m["faces"])
            if lod == 0 and not (LOD0_RANGE[0] <= n <= LOD0_RANGE[1]):
                probs.append(f"LOD0 has {n} triangles")
            if lod == 1 and n > LOD1_MAX:
                probs.append(f"LOD1 has {n} triangles")
            ov = vcmesh.uv_overlap_area(m["uv0"], grid=512)
            if ov >= 0.02:
                probs.append(f"uv overlap {ov:.3f}")
            bad += bool(probs)
            rows.append((k, lod, n, probs))
    return rows, bad


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.parse_args(argv)
    rows, bad = check_all()
    for k, lod, n, probs in rows:
        print(f"{k:15s} {str(lod):4s} {n:4d} tris  {'ok' if not probs else probs}")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
