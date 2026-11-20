"""Mesh building blocks shared by every kit piece (pure NumPy + shapely, no Blender needed).

Pieces are built in a facade frame: x = along the wall (u), y = up (v), z = out of the wall.
That frame is right-handed (u x v = normal), so triangles that are counter-clockwise seen
from outside stay that way after facade_layout.to_world().

MeshBuilder collects triangles with a material each; finish() merges duplicate vertices,
unwraps UV0 (planar charts at true scale, packed with vcmesh.pack_charts) and returns a
VC mesh dict (see pipeline/vcmesh.py).
"""
import math
import sys
from pathlib import Path

import numpy as np
import shapely
from shapely.geometry import Polygon

PIPE = Path(__file__).resolve().parents[2] / "pipeline"
if str(PIPE) not in sys.path:
    sys.path.insert(0, str(PIPE))
import vcmesh  # noqa: E402

MAT = vcmesh.material_index()
THRESHOLDS = {"2": 0.50, "3": 0.15, "3+": 0.05}     # merge rule, STANDARDS.md section 4


def mat_id(name):
    if name not in MAT:
        raise KeyError(f"material {name} is not in config/material_ids.json")
    return MAT[name]


# ---- outlines (2D, counter-clockwise) ------------------------------------------------

def arch_rise(width, arch):
    if arch == "round":
        return width / 2
    if arch == "pointed":
        return width * math.sqrt(3) / 2          # equilateral arch
    if arch == "segmental":
        return width * 0.15
    return 0.0


def opening_outline(u0, v0, u1, v1, arch="none", segments=8):
    """Counter-clockwise outline of an opening: a rectangle whose top is an arch.
    The arch fits inside the rect (its crown touches v1)."""
    w = u1 - u0
    rise = min(arch_rise(w, arch), (v1 - v0) * 0.9)
    pts = [(u0, v0), (u1, v0)]
    if arch == "none" or rise <= 1e-6:
        return pts + [(u1, v1), (u0, v1)]
    vs = v1 - rise
    pts.append((u1, vs))
    c = (u0 + u1) / 2
    if arch == "round":
        # half ellipse (circle when rise = w/2)
        for i in range(1, segments):
            a = math.pi * i / segments
            pts.append((c + w / 2 * math.cos(a), vs + rise * math.sin(a)))
    elif arch == "segmental":
        R = (w * w / 4 + rise * rise) / (2 * rise)
        a0 = math.asin(min(1.0, (w / 2) / R))
        cy = v1 - R
        for i in range(1, segments):
            a = a0 - 2 * a0 * i / segments
            pts.append((c + R * math.sin(a), cy + R * math.cos(a)))
    else:   # pointed: two arcs of radius w (scaled to the rise), centers at the springing points
        half = max(segments // 2, 2)
        k = rise / (w * math.sqrt(3) / 2)
        for i in range(1, half):          # right arc, center at the left springer
            a = (math.pi / 3) * i / half
            pts.append((u0 + w * math.cos(a), vs + k * w * math.sin(a)))
        pts.append((c, v1))
        for i in range(half - 1, 0, -1):  # left arc, center at the right springer
            a = (math.pi / 3) * i / half
            pts.append((u1 - w * math.cos(a), vs + k * w * math.sin(a)))
    pts.append((u0, vs))
    return pts


def circle_outline(cx, cy, r, segments=16):
    return [(cx + r * math.cos(2 * math.pi * i / segments), cy + r * math.sin(2 * math.pi * i / segments))
            for i in range(segments)]


def ccw(pts):
    p = np.asarray(pts, float)
    a = 0.5 * np.sum(p[:, 0] * np.roll(p[:, 1], -1) - np.roll(p[:, 0], -1) * p[:, 1])
    return p if a > 0 else p[::-1]


def triangulate(poly):
    """Constrained Delaunay triangles of a shapely Polygon (with holes). Returns (k, 3, 2) CCW."""
    if poly.is_empty or poly.area < 1e-9:
        return np.zeros((0, 3, 2))
    out = []
    for t in shapely.constrained_delaunay_triangles(poly).geoms:
        c = np.asarray(t.exterior.coords)[:3]
        d = (c[1, 0] - c[0, 0]) * (c[2, 1] - c[0, 1]) - (c[2, 0] - c[0, 0]) * (c[1, 1] - c[0, 1])
        if abs(d) < 1e-12:
            continue
        out.append(c if d > 0 else c[::-1])
    return np.asarray(out).reshape(-1, 3, 2)


# ---- the builder ----------------------------------------------------------------------

AXES = {"x": (1, 2, 0), "y": (2, 0, 1), "z": (0, 1, 2)}   # (s, t, axis): s x t = axis (right-handed)


def _lift(st, a, axis):
    """2D (s, t) points + axis coordinate -> 3D in the (x, y, z) frame."""
    si, ti, ai = AXES[axis]
    st = np.asarray(st, float)
    out = np.zeros(st.shape[:-1] + (3,))
    out[..., si] = st[..., 0]
    out[..., ti] = st[..., 1]
    out[..., ai] = a
    return out


class MeshBuilder:
    def __init__(self):
        self.tris = []      # (3, 3) arrays
        self.mats = []

    def __len__(self):
        return len(self.tris)

    def tri(self, a, b, c, mat):
        self.tris.append(np.array([a, b, c], float))
        self.mats.append(mat_id(mat) if isinstance(mat, str) else int(mat))

    def quad(self, a, b, c, d, mat):
        """Corners counter-clockwise seen from the side the face looks at."""
        self.tri(a, b, c, mat)
        self.tri(a, c, d, mat)

    def extend(self, other, transform=None):
        for t, m in zip(other.tris, other.mats):
            self.tris.append(transform(t) if transform else t)
            self.mats.append(m)

    def flat(self, poly2d, a, axis, mat, flip=False):
        """A flat face: a shapely Polygon (or point list) in the (s, t) plane at axis = a.
        Faces +axis, or -axis when flip."""
        poly = poly2d if isinstance(poly2d, Polygon) else Polygon(ccw(poly2d))
        for t in triangulate(poly):
            P = _lift(t, a, axis)
            if flip:
                self.tri(P[0], P[2], P[1], mat)
            else:
                self.tri(P[0], P[1], P[2], mat)

    def sides(self, pts2d, a0, a1, axis, mat, inward=False, closed=True):
        """Side walls of a prism: the CCW outline swept from axis = a0 to a1 (a1 > a0).
        Faces point away from the outline's inside, or into it when inward (for a hole's reveal)."""
        p = np.asarray(pts2d, float)
        n = len(p)
        rng = range(n) if closed else range(n - 1)
        for i in rng:
            s, e = p[i], p[(i + 1) % n]
            if np.linalg.norm(e - s) < 1e-9:
                continue
            A, B = _lift(s, a0, axis), _lift(e, a0, axis)
            C, D = _lift(e, a1, axis), _lift(s, a1, axis)
            if inward:
                self.quad(A, D, C, B, mat)
            else:
                self.quad(A, B, C, D, mat)

    def prism(self, pts2d, a0, a1, axis, mat, caps=(True, True), cap_mat=None):
        """A closed prism: outline (any orientation) in the (s, t) plane swept along axis."""
        p = ccw(pts2d)
        self.sides(p, a0, a1, axis, mat)
        if caps[1]:
            self.flat(p, a1, axis, cap_mat or mat)
        if caps[0]:
            self.flat(p, a0, axis, cap_mat or mat, flip=True)

    def box(self, x0, x1, y0, y1, z0, z1, mat, skip=()):
        """Axis-aligned box; skip names faces to leave out ('x-', 'x+', 'y-', 'y+', 'z-', 'z+')."""
        if x1 - x0 < 1e-6 or y1 - y0 < 1e-6 or z1 - z0 < 1e-6:
            return
        v = np.array([[x0, y0, z0], [x1, y0, z0], [x1, y1, z0], [x0, y1, z0],
                      [x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]])
        faces = {"z-": (0, 3, 2, 1), "z+": (4, 5, 6, 7), "y-": (0, 1, 5, 4), "y+": (3, 7, 6, 2),
                 "x-": (0, 4, 7, 3), "x+": (1, 2, 6, 5)}
        for k, (a, b, c, d) in faces.items():
            if k not in skip:
                self.quad(v[a], v[b], v[c], v[d], mat)

    def ring(self, outer, inner, a0, a1, axis, mat, back=False, outer_sides=True):
        """A flat frame between two CCW outlines (inner inside outer), swept from a0 to a1.
        outer_sides=False leaves out the outside edge faces (hidden when the frame sits in a reveal)."""
        poly = Polygon(ccw(outer), [ccw(inner)[::-1]])
        self.flat(poly, a1, axis, mat)
        if back:
            self.flat(poly, a0, axis, mat, flip=True)
        if outer_sides:
            self.sides(ccw(outer), a0, a1, axis, mat)
        self.sides(ccw(inner), a0, a1, axis, mat, inward=True)

    def bounds(self):
        if not self.tris:
            return np.zeros(3), np.zeros(3)
        T = np.asarray(self.tris).reshape(-1, 3)
        return T.min(axis=0), T.max(axis=0)

    def arrays(self):
        """Raw (verts, faces, face_mat) with duplicate vertices merged at 0.01 mm."""
        if not self.tris:
            return np.zeros((0, 3)), np.zeros((0, 3), np.int32), np.zeros(0, np.uint8)
        T = np.asarray(self.tris).reshape(-1, 3)
        q = np.round(T / 1e-5).astype(np.int64)
        _, first, inv = np.unique(q, axis=0, return_index=True, return_inverse=True)
        v = T[first]
        f = inv.reshape(-1, 3).astype(np.int32)
        m = np.asarray(self.mats, np.uint8)
        a = np.linalg.norm(np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]]), axis=1)
        ok = (f[:, 0] != f[:, 1]) & (f[:, 1] != f[:, 2]) & (f[:, 0] != f[:, 2]) & (a > 1e-10)
        return v, f[ok], m[ok]

    def finish(self, meta=None):
        v, f, m = self.arrays()
        uv, info = unwrap(v, f)
        return {"verts": v, "faces": f, "face_mat": m, "uv0": uv, "meta": {**(meta or {}), **info}}


# ---- UV0 -------------------------------------------------------------------------------

def _frame(n):
    n = n / max(np.linalg.norm(n), 1e-12)
    e1 = np.cross([0.0, 0.0, 1.0], n)
    if np.linalg.norm(e1) < 1e-6:
        e1 = np.array([1.0, 0.0, 0.0]) if n[2] > 0 else np.array([-1.0, 0.0, 0.0])
    e1 /= np.linalg.norm(e1)
    return e1, np.cross(n, e1)


def unwrap(verts, faces, max_deg=12.0, padding=0.04, up_axis=2):
    """UV0 charts: edge-connected faces whose normals stay within max_deg of the chart's first
    face, projected flat at true scale; a chart whose projection flips a triangle is split into
    single faces. Charts are shelf-packed (vcmesh.pack_charts), so every chart keeps the same
    meters per UV unit and no two charts overlap. up_axis is the 'up' used to orient charts."""
    nf = len(faces)
    if nf == 0:
        return np.zeros((0, 3, 2), np.float32), {"charts": 0, "uv_m_per_unit": 1.0}
    V = np.asarray(verts, float)
    if up_axis != 2:      # rotate so "up" is z for the chart frames (facade frame has y up)
        V = V[:, [0, 2, 1]] * np.array([1, -1, 1])
    T = V[faces]
    n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    area = np.linalg.norm(n, axis=1) / 2
    n = n / np.maximum(2 * area, 1e-12)[:, None]
    edges = {}
    for fi, t in enumerate(faces):
        for k in range(3):
            a, b = int(t[k]), int(t[(k + 1) % 3])
            edges.setdefault((min(a, b), max(a, b)), []).append(fi)
    nbr = [[] for _ in range(nf)]
    for fs in edges.values():
        if len(fs) == 2:
            nbr[fs[0]].append(fs[1])
            nbr[fs[1]].append(fs[0])
    cosmax = math.cos(math.radians(max_deg))
    chart = np.full(nf, -1)
    groups = []
    for seed in np.argsort(-area):
        if chart[seed] >= 0:
            continue
        cid = len(groups)
        chart[seed] = cid
        members, stack, ns = [seed], [seed], n[seed]
        while stack:
            f0 = stack.pop()
            for g in nbr[f0]:
                if chart[g] < 0 and n[g] @ ns >= cosmax:
                    chart[g] = cid
                    members.append(g)
                    stack.append(g)
        groups.append(np.array(members))
    final = []
    for mem in groups:
        nsum = (n[mem] * area[mem, None]).sum(axis=0)
        if np.linalg.norm(nsum) < 1e-12:
            nsum = n[mem[0]]
        e1, e2 = _frame(nsum)
        P = T[mem]
        uv = np.stack([P @ e1, P @ e2], axis=-1)
        d = (uv[:, 1, 0] - uv[:, 0, 0]) * (uv[:, 2, 1] - uv[:, 0, 1]) - (uv[:, 2, 0] - uv[:, 0, 0]) * (uv[:, 1, 1] - uv[:, 0, 1])
        if len(mem) > 1 and (d <= 1e-12).any():
            for fm in mem:
                final.append((np.array([fm]), None))
        else:
            final.append((mem, uv))
    local, sizes = [], []
    for mem, uv in final:
        if uv is None:
            e1, e2 = _frame(n[mem[0]])
            P = T[mem]
            uv = np.stack([P @ e1, P @ e2], axis=-1)
        uv = uv - uv.reshape(-1, 2).min(axis=0)
        mx = uv.reshape(-1, 2).max(axis=0)
        local.append(uv)
        sizes.append((max(mx[0], 1e-3), max(mx[1], 1e-3)))
    offsets, side = vcmesh.pack_charts(sizes, padding)
    out = np.zeros((nf, 3, 2), np.float32)
    for (mem, _), uv, off in zip(final, local, offsets):
        out[mem] = (uv + np.asarray(off)) / side
    return np.clip(out, 0, 1), {"charts": len(final), "uv_m_per_unit": float(side)}


# ---- merge rule ------------------------------------------------------------------------

class Merger:
    """Applies the merge rule (STANDARDS.md section 4) and collects ledger entries.

    keep() returns True when a feature is big enough to model at this level; otherwise it
    records one ledger entry (center and size in the piece's facade frame) and returns False."""

    def __init__(self, lod="3", where=""):
        if lod not in THRESHOLDS:
            raise ValueError(f"lod must be one of {list(THRESHOLDS)}")
        self.lod = lod
        self.threshold = THRESHOLDS[lod]
        self.where = where
        self.entries = []

    def keep(self, kind, relief, w, h, center, size, material, merged_into, hint="normal_map", count=1):
        thr = self.threshold
        if relief >= thr and not (w < thr and h < thr):
            return True
        self.entries.append({"type": kind, "merged_into": merged_into or self.where,
                             "center": [round(float(c), 3) for c in center],
                             "size": [round(float(s), 3) for s in size], "count": int(count),
                             "material": material, "restore_hint": hint,
                             "reason": (f"relief {relief:.3f} m < {thr} m" if relief < thr
                                        else f"{w:.3f} x {h:.3f} m both < {thr} m")})
        return False
