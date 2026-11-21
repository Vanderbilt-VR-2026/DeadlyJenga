"""The parametric piece library every kit shares.

Every piece is built in the facade frame (x = along the wall, y = up, z = out of the wall;
z = 0 is the wall surface) from the sizes it is given, so nothing is fixed. Each function
takes the kit (profiles.KITS[name]), a geom.Merger (which knows the LoD level and applies the
merge rule, writing a ledger entry for every sub-part it drops) and the sizes, and returns a
Piece:
  mesh   MeshBuilder with the visible triangles
  col    MeshBuilder with closed solids for the game collider (steps, ledges, buttresses...)
  hole   CCW outline (u, v) to cut out of the wall, or None
At LoD2 a piece is "nothing": it returns empty meshes and one ledger entry.
"""
import math
from dataclasses import dataclass, field

import numpy as np
from shapely.geometry import Polygon, box as sbox

from . import geom
from .geom import MeshBuilder, ccw, opening_outline


@dataclass
class Piece:
    kind: str
    mesh: MeshBuilder = field(default_factory=MeshBuilder)
    col: MeshBuilder = field(default_factory=MeshBuilder)
    hole: list = None
    size: tuple = (0.0, 0.0, 0.0)          # (along, up, out) overall size, for tests and the ledger


def _segments(kit, merger, base=None):
    n = base or kit["window"].get("segments", 8)
    return max(4, int(n * 0.75)) if merger.lod == "3+" else max(4, n // 2)


def _lod2(kind, merger, center, size, material):
    """LoD2: the piece is not modeled; one ledger entry stands for it."""
    merger.entries.append({"type": kind, "merged_into": merger.where or "wall", "center": [round(c, 3) for c in center],
                           "size": [round(s, 3) for s in size], "count": 1, "material": material,
                           "restore_hint": "geometry", "reason": "LoD2 has no facade pieces"})
    return Piece(kind, size=(size[0], size[2], size[1]))


def _solid(mb, poly, z0, z1, mat, back=True):
    """Extrude a shapely polygon (may have holes) from z0 to z1 along the wall normal."""
    if poly.is_empty:
        return
    polys = list(poly.geoms) if hasattr(poly, "geoms") else [poly]
    for p in polys:
        if p.area < 1e-6:
            continue
        p = Polygon(ccw(p.exterior.coords[:-1]), [ccw(h.coords[:-1])[::-1] for h in p.interiors])
        mb.flat(p, z1, "z", mat)
        if back:
            mb.flat(p, z0, "z", mat, flip=True)
        mb.sides(ccw(p.exterior.coords[:-1]), z0, z1, "z", mat)
        for h in p.interiors:
            mb.sides(ccw(h.coords[:-1]), z0, z1, "z", mat, inward=True)


def _inset(outline, d):
    p = Polygon(outline).buffer(-d, join_style=2, mitre_limit=10)
    if p.is_empty or p.area < 1e-4:
        return None
    if hasattr(p, "geoms"):
        p = max(p.geoms, key=lambda g: g.area)
    return list(ccw(p.exterior.coords[:-1]))


def _hood(kit_w, outline, rect, arch, merger, mb, mats, label_drop=0.3):
    """Lintel (flat block over the opening) or label hood mould (band following the head)."""
    u0, v0, u1, v1 = rect
    w = u1 - u0
    style, hh, rel, ext = kit_w["hood"], kit_w["hood_h"], kit_w["hood_relief"], kit_w.get("hood_ext", 0.1)
    if style == "none":
        return
    if style == "lintel" and arch in ("round", "pointed"):
        style = "label"                      # a flat lintel cannot sit on a tall arch
    spring = v1 - min(geom.arch_rise(w, arch), (v1 - v0) * 0.9)
    if style == "lintel":
        shape = sbox(u0 - ext, v1, u1 + ext, v1 + hh)
        center, size = ((u0 + u1) / 2, v1 + hh / 2, rel / 2), (w + 2 * ext, rel, hh)
    else:
        op = Polygon(outline)
        band = op.buffer(hh, join_style=2, mitre_limit=3).difference(op)
        cut = spring - label_drop if arch != "none" else v1 - label_drop
        shape = band.intersection(sbox(u0 - hh - 1, cut, u1 + hh + 1, v1 + hh + 1))
        center, size = ((u0 + u1) / 2, (cut + v1 + hh) / 2, rel / 2), (w + 2 * hh, rel, v1 + hh - cut)
    if merger.keep(f"{style}_hood" if style == "label" else "lintel", rel, size[0], hh, center, size,
                   mats["trim"], None, "normal_map" if rel > 0.01 else "texture_only"):
        _solid(mb, shape, 0.0, rel, mats["trim"], back=False)


def window(kit, merger, rect, arch=None, reveal=None, frame=None, sill=True, hood=None, mullions=None,
           panel_mat=None):
    """A window opening: reveal (cut into the wall), glass, frame, mullion/transom, sill, lintel or
    hood. rect = (u0, v0, u1, v1) of the opening; arch none/round/pointed/segmental."""
    kw = dict(kit["window"])
    if hood is not None:
        kw["hood"] = hood
    mats = kit["materials"]
    u0, v0, u1, v1 = rect
    w, h = u1 - u0, v1 - v0
    arch = arch or kw["arch"]
    if merger.lod == "2":
        return _lod2("window", merger, ((u0 + u1) / 2, (v0 + v1) / 2, 0), (w, 0.0, h), mats["glass"])
    p = Piece("window", size=(w, h, kw["reveal"] if reveal is None else reveal))
    outline = opening_outline(u0, v0, u1, v1, arch, _segments(kit, merger))
    p.hole = outline
    d = kw["reveal"] if reveal is None else reveal
    mb = p.mesh
    center = ((u0 + u1) / 2, (v0 + v1) / 2, -d / 2)
    if merger.keep("window_reveal", d, w, h, center, (w, d, h), mats["wall"], None, "geometry"):
        mb.sides(ccw(outline), -d, 0.0, "z", mats["wall"], inward=True)
        gz = -d
    else:
        gz = 0.0
    mb.flat(outline, gz, "z", panel_mat or mats["glass"])
    fw = kw["frame_w"] if frame is None else frame
    fr = kw["frame_relief"]
    inner = _inset(outline, fw)
    if inner is not None and merger.keep("window_frame", fr, w, h, (center[0], center[1], gz + fr / 2),
                                         (w, fr, h), mats["frame"], None, "normal_map"):
        mb.ring(outline, inner, gz, gz + fr, "z", mats["frame"], outer_sides=gz == 0.0)
    spring = v1 - min(geom.arch_rise(w, arch), h * 0.9)
    mw, mr = kw["mullion_w"], kw["mullion_relief"]
    n_mull = mullions if mullions is not None else (1 if w >= kw["mullion_min_width"] else 0)
    if n_mull:
        for k in range(n_mull):
            x = u0 + w * (k + 1) / (n_mull + 1)
            top = spring if arch != "none" else v1 - fw
            if merger.keep("mullion", mr, mw, top - v0, (x, (v0 + top) / 2, gz + mr / 2), (mw, mr, top - v0),
                           mats["frame"], None, "normal_map"):
                mb.box(x - mw / 2, x + mw / 2, v0 + fw, top, gz, gz + mr, mats["frame"], skip=("z-",))
    if h >= kw["transom_min_height"]:
        y = min(spring, v0 + 0.72 * h)
        if merger.keep("transom", mr, w, mw, ((u0 + u1) / 2, y, gz + mr / 2), (w - 2 * fw, mr, mw),
                       mats["frame"], None, "normal_map"):
            mb.box(u0 + fw, u1 - fw, y - mw / 2, y + mw / 2, gz, gz + mr, mats["frame"], skip=("z-",))
    if sill:
        sh, sp, se = kw["sill_h"], kw["sill_proj"], kw["sill_ext"]
        if merger.keep("sill", sp, w + 2 * se, sh, ((u0 + u1) / 2, v0 - sh / 2, sp / 2), (w + 2 * se, sp, sh),
                       mats["trim"], None, "normal_map"):
            mb.box(u0 - se, u1 + se, v0 - sh, v0, -0.02, sp, mats["trim"], skip=("z-",))
    _hood(kw, outline, rect, arch, merger, mb, mats)
    return p


def door(kit, merger, rect, arch=None, reveal=None, panel_h=2.5, hood=None):
    """A door: deeper reveal, door leaf up to panel_h, glass fanlight/transom above, frame, hood."""
    kd = dict(kit["door"])
    if hood is not None:
        kd["hood"] = hood
    mats = kit["materials"]
    u0, v0, u1, v1 = rect
    w, h = u1 - u0, v1 - v0
    arch = arch or kd["arch"]
    if merger.lod == "2":
        return _lod2("door", merger, ((u0 + u1) / 2, (v0 + v1) / 2, 0), (w, 0.0, h), mats["door"])
    d = kd["reveal"] if reveal is None else reveal
    p = Piece("door", size=(w, h, d))
    outline = opening_outline(u0, v0, u1, v1, arch, _segments(kit, merger))
    p.hole = outline
    mb = p.mesh
    if merger.keep("door_reveal", d, w, h, ((u0 + u1) / 2, (v0 + v1) / 2, -d / 2), (w, d, h), mats["trim"],
                   None, "geometry"):
        mb.sides(ccw(outline), -d, 0.0, "z", mats["trim"], inward=True)
        gz = -d
    else:
        gz = 0.0
    op = Polygon(outline)
    split = v0 + min(panel_h, h)
    leaf = op.intersection(sbox(u0 - 1, v0 - 1, u1 + 1, split))
    fan = op.difference(sbox(u0 - 1, v0 - 1, u1 + 1, split))
    for part, mat in ((leaf, mats["door"]), (fan, mats["glass"])):
        for g in (part.geoms if hasattr(part, "geoms") else [part]):
            if g.area > 1e-4:
                mb.flat(Polygon(ccw(g.exterior.coords[:-1])), gz, "z", mat)
    fw, fr = kd["frame_w"], kd["frame_relief"]
    inner = _inset(outline, fw)
    if inner is not None and merger.keep("door_frame", fr, w, h, ((u0 + u1) / 2, (v0 + v1) / 2, gz + fr / 2),
                                         (w, fr, h), mats["trim"], None, "normal_map"):
        mb.ring(outline, inner, gz, gz + fr, "z", mats["trim"], outer_sides=gz == 0.0)
    if fan.area > 1e-3 and merger.keep("door_transom_bar", fr, w, 0.1, ((u0 + u1) / 2, split, gz + fr / 2),
                                       (w, fr, 0.1), mats["frame"], None, "normal_map"):
        mb.box(u0 + fw, u1 - fw, split - 0.05, split + 0.05, gz, gz + fr, mats["frame"], skip=("z-",))
    _hood(kd, outline, rect, arch, merger, mb, mats)
    return p


def arch(kit, merger, rect, arch_type="round", reveal=None, back_mat=None):
    """An archway (portal or arcade bay): deep reveal closed by a back panel, voussoir band around it."""
    ka = kit["arch"]
    mats = kit["materials"]
    u0, v0, u1, v1 = rect
    w, h = u1 - u0, v1 - v0
    if merger.lod == "2":
        return _lod2("arch", merger, ((u0 + u1) / 2, (v0 + v1) / 2, 0), (w, 0.0, h), mats["trim"])
    d = ka["reveal"] if reveal is None else reveal
    p = Piece("arch", size=(w, h, d))
    outline = opening_outline(u0, v0, u1, v1, arch_type, _segments(kit, merger, 12))
    p.hole = outline
    mb = p.mesh
    mb.sides(ccw(outline), -d, 0.0, "z", mats["trim"], inward=True)
    mb.flat(outline, -d, "z", back_mat or mats["wall"])
    bw, br = ka["band_w"], ka["band_relief"]
    op = Polygon(outline)
    band = op.buffer(bw, join_style=2, mitre_limit=3).difference(op).intersection(sbox(u0 - bw - 1, v0, u1 + bw + 1, v1 + bw + 1))
    if merger.keep("voussoir_band", br, w + 2 * bw, h + bw, ((u0 + u1) / 2, (v0 + v1) / 2, br / 2),
                   (w + 2 * bw, br, h + bw), mats["trim"], None, "normal_map"):
        _solid(mb, band, 0.0, br, mats["trim"], back=False)
    return p


def column(kit, merger, cu, cz, v0, v1, diameter):
    """A freestanding column centred at (u = cu, out = cz) from v0 to v1: base, shaft, capital."""
    kc = kit["column"]
    mats = kit["materials"]
    h = v1 - v0
    if merger.lod == "2":
        return _lod2("column", merger, (cu, (v0 + v1) / 2, cz), (diameter, diameter, h), mats["trim"])
    p = Piece("column", size=(diameter, h, diameter))
    mb = p.mesh
    r = diameter / 2
    bh, ch = kc["base_h"] * h, kc["cap_h"] * h
    bo, co = kc["base_over"] * diameter, kc["cap_over"] * diameter
    n = kc["sides"] if merger.lod == "3+" else max(6, kc["sides"] // 2)
    ring = [(cz + r * math.cos(2 * math.pi * i / n), cu + r * math.sin(2 * math.pi * i / n)) for i in range(n)]
    mb.prism(ring, v0 + bh, v1 - ch, "y", mats["trim"], caps=(bh <= 0, ch <= 0))
    for kind, y0, y1, over in (("column_base", v0, v0 + bh, bo), ("column_capital", v1 - ch, v1, co)):
        if y1 - y0 < 1e-4:
            continue
        if merger.keep(kind, over, diameter + 2 * over, y1 - y0, (cu, (y0 + y1) / 2, cz),
                       (diameter + 2 * over, over, y1 - y0), mats["trim"], f"column_{cu:.2f}", "geometry"):
            s = r + over
            mb.box(cu - s, cu + s, y0, y1, cz - s, cz + s, mats["trim"])
        else:
            mb.box(cu - r, cu + r, y0, y1, cz - r, cz + r, mats["trim"])
    p.col.box(cu - r, cu + r, v0, v1, cz - r, cz + r, "concrete")
    return p


def pilaster(kit, merger, rect, proj):
    """A flat pier on the wall (rect on the facade, sticking out proj) with base and capital."""
    kp = kit["pilaster"]
    mats = kit["materials"]
    u0, v0, u1, v1 = rect
    w, h = u1 - u0, v1 - v0
    if merger.lod == "2" or not merger.keep("pilaster", proj, w, h, ((u0 + u1) / 2, (v0 + v1) / 2, proj / 2),
                                            (w, proj, h), mats["trim"], None, "normal_map"):
        return Piece("pilaster", size=(w, h, proj))
    p = Piece("pilaster", size=(w, h, proj))
    bh, ch = kp["base_h"] * h, kp["cap_h"] * h
    bo, co = kp["base_over"], kp["cap_over"]
    p.mesh.box(u0, u1, v0 + bh, v1 - ch, 0.0, proj, mats["trim"], skip=("z-",))
    for kind, y0, y1, over in (("pilaster_base", v0, v0 + bh, bo), ("pilaster_capital", v1 - ch, v1, co)):
        if merger.keep(kind, proj + over, w + 2 * over, y1 - y0, ((u0 + u1) / 2, (y0 + y1) / 2, (proj + over) / 2),
                       (w + 2 * over, proj + over, y1 - y0), mats["trim"], "pilaster", "geometry") and over > 0:
            p.mesh.box(u0 - over, u1 + over, y0, y1, 0.0, proj + over, mats["trim"], skip=("z-",))
        else:
            p.mesh.box(u0, u1, y0, y1, 0.0, proj, mats["trim"], skip=("z-",))
    if proj >= 0.15:
        p.col.box(u0, u1, v0, v1, -0.05, proj, "concrete")
    return p


def course(kit, merger, u0, u1, v0, height=None, depth=None, section=None, kind="string_course", mat=None, ledge=False):
    """A cornice or string course: the kit's section scaled to height x depth, run from u0 to u1
    with its bottom at v0. ledge=True also adds it to the collider (something to stand on)."""
    kc = kit["cornice" if kind == "cornice" else "string_course"]
    h = kc["height"] if height is None else height
    d = kc["depth"] if depth is None else depth
    sec = section or kc["section"]
    mats = kit["materials"]
    mat = mat or mats["trim"]
    L = u1 - u0
    if merger.lod == "2":
        return _lod2(kind, merger, ((u0 + u1) / 2, v0 + h / 2, d / 2), (L, d, h), mat)
    p = Piece(kind, size=(L, h, d))
    if not merger.keep(kind, d, L, h, ((u0 + u1) / 2, v0 + h / 2, d / 2), (L, d, h), mat, None,
                       "normal_map" if d > 0.02 else "texture_only"):
        return p
    from .profiles import SECTIONS
    prof = SECTIONS[sec]
    pts = [(v0 + up * h, out * d) for out, up in prof]        # (s, t) = (y, z) for axis x
    mb = p.mesh
    pc = ccw(pts)
    mb.sides(pc, u0, u1, "x", mat)
    mb.flat(pc, u1, "x", mat)
    mb.flat(pc, u0, "x", mat, flip=True)
    if ledge or d >= 0.15:
        p.col.box(u0, u1, v0, v0 + h, -0.1, d, "concrete")
    return p


def buttress(kit, merger, rect, depth, stages=None):
    """A stepped buttress: rect (u0, v0, u1, v1) on the wall, depth at the foot, each stage set back
    with a sloped weathering on top."""
    kb = kit["buttress"]
    mats = kit["materials"]
    u0, v0, u1, v1 = rect
    w, h = u1 - u0, v1 - v0
    if merger.lod == "2" or not merger.keep("buttress", depth, w, h, ((u0 + u1) / 2, (v0 + v1) / 2, depth / 2),
                                            (w, depth, h), mats["wall"], None, "geometry"):
        return Piece("buttress", size=(w, h, depth))
    p = Piece("buttress", size=(w, h, depth))
    n = stages or kb["stages"]
    wt = min(kb["weathering"], h / (2 * n))
    pts = [(v0, -0.03), (v0, depth)]
    for i in range(n):
        di = depth * (1 - i / n * 0.6)
        dn = depth * (1 - (i + 1) / n * 0.6) if i < n - 1 else 0.0
        top = v0 + h * (i + 1) / n
        pts += [(top - wt, di), (top, dn)] if i < n - 1 else [(top - wt * 1.5, di), (top, -0.03)]
    pc = ccw(pts)
    p.mesh.sides(pc, u0, u1, "x", mats["wall"])
    p.mesh.flat(pc, u1, "x", mats["wall"])
    p.mesh.flat(pc, u0, "x", mats["wall"], flip=True)
    p.col.sides(pc, u0, u1, "x", "concrete")
    p.col.flat(pc, u1, "x", "concrete")
    p.col.flat(pc, u0, "x", "concrete", flip=True)
    return p


def dormer(kit, merger, cu, v0, width, height, depth, pitch_deg=None):
    """A gabled dormer: front face at out = 0 centred at u = cu, bottom at v0 (on the roof),
    reaching back depth meters, with a recessed window."""
    kd = kit["dormer"]
    mats = kit["materials"]
    if merger.lod == "2":
        return _lod2("dormer", merger, (cu, v0 + height / 2, -depth / 2), (width, depth, height), mats["roof"])
    p = Piece("dormer", size=(width, height, depth))
    pitch = math.radians(pitch_deg or kd["pitch_deg"])
    rise = width / 2 * math.tan(pitch)
    x0, x1 = cu - width / 2, cu + width / 2
    y1, yr = v0 + height, v0 + height + rise
    mb = p.mesh
    ww = width * kd["window_frac"]
    wrect = (cu - ww / 2, v0 + 0.15 * height, cu + ww / 2, v0 + 0.9 * height)
    win = window(kit, merger, wrect, arch="none", hood="none")
    front = Polygon([(x0, v0), (x1, v0), (x1, y1), (cu, yr), (x0, y1)]).difference(Polygon(win.hole))
    mb.flat(front, 0.0, "z", mats["wall"])
    mb.extend(win.mesh)
    # cheeks (sides) and roof slopes
    mb.quad((x0, v0, -depth), (x0, v0, 0), (x0, y1, 0), (x0, y1, -depth), mats["wall"])
    mb.quad((x1, v0, 0), (x1, v0, -depth), (x1, y1, -depth), (x1, y1, 0), mats["wall"])
    mb.quad((x0, y1, 0), (cu, yr, 0), (cu, yr, -depth), (x0, y1, -depth), mats["roof"])
    mb.quad((cu, yr, 0), (x1, y1, 0), (x1, y1, -depth), (cu, yr, -depth), mats["roof"])
    return p


def chimney(kit, merger, cu, cz, v0, w, d, h):
    """A chimney (or tower pier): shaft w x d (along x out) centred at (cu, cz), from v0 up h, with a cap."""
    kc = kit["chimney"]
    mats = kit["materials"]
    if merger.lod == "2":
        return _lod2("chimney", merger, (cu, v0 + h / 2, cz), (w, d, h), mats["chimney"])
    p = Piece("chimney", size=(w, h, d))
    ch, co = min(kc["cap_h"], h * 0.3), kc["cap_over"]
    top = v0 + h
    p.mesh.box(cu - w / 2, cu + w / 2, v0, top - ch, cz - d / 2, cz + d / 2, mats["chimney"], skip=("y-",))
    over = co if merger.keep("chimney_cap", co, w + 2 * co, ch, (cu, top - ch / 2, cz), (w + 2 * co, co, ch),
                             mats["cap"], "chimney", "geometry") else 0.0
    p.mesh.box(cu - w / 2 - over, cu + w / 2 + over, top - ch, top, cz - d / 2 - over, cz + d / 2 + over, mats["cap"])
    p.col.box(cu - w / 2, cu + w / 2, v0, top, cz - d / 2, cz + d / 2, "concrete")
    return p


def balustrade(kit, merger, u0, u1, v0, height, thickness, cz=None):
    """A balustrade from u0 to u1 standing on v0: plinth, top rail and balusters (a solid panel
    when the balusters fall under the merge rule)."""
    kb = kit["balustrade"]
    mats = kit["materials"]
    cz = thickness / 2 if cz is None else cz
    L = u1 - u0
    if merger.lod == "2":
        return _lod2("balustrade", merger, ((u0 + u1) / 2, v0 + height / 2, cz), (L, thickness, height), mats["trim"])
    p = Piece("balustrade", size=(L, height, thickness))
    rh, ph = kb["rail_h"], kb["plinth_h"]
    z0, z1 = cz - thickness / 2, cz + thickness / 2
    mb = p.mesh
    mb.box(u0, u1, v0, v0 + ph, z0, z1, mats["trim"])
    mb.box(u0, u1, v0 + height - rh, v0 + height, z0 - 0.02, z1 + 0.02, mats["trim"])
    bw = kb["baluster_w"]
    n = max(1, int(L / kb["spacing"]))
    y0, y1 = v0 + ph, v0 + height - rh
    if merger.keep("baluster", bw, bw, y1 - y0, ((u0 + u1) / 2, (y0 + y1) / 2, cz), (L, bw, y1 - y0),
                   mats["trim"], "balustrade_panel", "geometry", count=n):
        for i in range(n):
            x = u0 + (i + 0.5) * L / n
            mb.box(x - bw / 2, x + bw / 2, y0, y1, cz - bw / 2, cz + bw / 2, mats["trim"], skip=("y-", "y+"))
    else:
        mb.box(u0, u1, y0, y1, cz - bw / 3, cz + bw / 3, mats["trim"], skip=("y-", "y+"))
    p.col.box(u0, u1, v0, v0 + height, z0, z1, "concrete")
    return p


def steps(kit, merger, u0, u1, v_ground, v_top, run, landing=0.0, cheeks=True, riser=None):
    """A flight of steps in front of the wall: from v_ground (out at `run` meters) up to v_top at the
    wall, each riser close to the kit riser height, with an optional top landing and sloped cheek
    walls on both sides. The same solids go into the collider (the play check climbs them)."""
    ks = kit["steps"]
    mats = kit["materials"]
    rise = v_top - v_ground
    W = u1 - u0
    if rise <= 0.02:
        return Piece("steps", size=(W, 0.0, run))
    if merger.lod == "2" and rise < geom.THRESHOLDS["2"]:
        return _lod2("steps", merger, ((u0 + u1) / 2, v_ground + rise / 2, run / 2), (W, run, rise), mats["steps"])
    n = max(1, int(round(rise / (riser or ks["riser"]))))
    r = rise / n
    flight = max(run - landing, 0.3 * n)
    tread = flight / n
    base = v_ground - 0.25                          # sink the foot so uneven ground does not show a gap
    pts = [(base, 0.0), (base, landing + flight)]
    for k in range(1, n + 1):
        z_out = landing + flight - (k - 1) * tread
        z_in = landing + flight - k * tread if k < n else 0.0
        pts += [(v_ground + k * r, z_out), (v_ground + k * r, z_in)]
    p = Piece("steps", size=(W, rise, landing + flight))
    pc = ccw(pts)
    for mb, mat in ((p.mesh, mats["steps"]), (p.col, "concrete")):
        mb.sides(pc, u0, u1, "x", mat)
        mb.flat(pc, u1, "x", mat)
        mb.flat(pc, u0, "x", mat, flip=True)
    if cheeks and W > 1.2:
        cw, ca = ks["cheek_w"], ks["cheek_above"]
        cpts = ccw([(base - 0.05, 0.0), (base - 0.05, landing + flight + 0.15), (v_ground + ca * 0.7, landing + flight + 0.15),
                    (v_top + ca, landing), (v_top + ca, 0.0)])
        for a, b in ((u0 - cw, u0 + 0.03), (u1 - 0.03, u1 + cw)):
            for mb, mat in ((p.mesh, mats["trim"]), (p.col, "concrete")):
                mb.sides(cpts, a, b, "x", mat)
                mb.flat(cpts, b, "x", mat)
                mb.flat(cpts, a, "x", mat, flip=True)
    return p


def projection(kit, merger, outline, depth, mat=None, kind="projection"):
    """Any outline on the wall pushed out by depth (a bay, a clock face, a panel)."""
    mats = kit["materials"]
    mat = mat or mats["wall"]
    P = Polygon(outline)
    x0, y0, x1, y1 = P.bounds
    if merger.lod == "2" or not merger.keep(kind, depth, x1 - x0, y1 - y0, ((x0 + x1) / 2, (y0 + y1) / 2, depth / 2),
                                            (x1 - x0, depth, y1 - y0), mat, None, "geometry"):
        return Piece(kind, size=(x1 - x0, y1 - y0, depth))
    p = Piece(kind, size=(x1 - x0, y1 - y0, depth))
    _solid(p.mesh, P, 0.0, depth, mat, back=False)
    if depth >= 0.3:
        _solid(p.col, P, -0.05, depth, "concrete")
    return p


def recess(kit, merger, outline, depth, mat=None, kind="recess"):
    """Any outline pushed into the wall by depth (> 0): cut, reveal and back panel."""
    mats = kit["materials"]
    mat = mat or mats["wall"]
    P = Polygon(outline)
    x0, y0, x1, y1 = P.bounds
    if merger.lod == "2" or not merger.keep(kind, depth, x1 - x0, y1 - y0, ((x0 + x1) / 2, (y0 + y1) / 2, -depth / 2),
                                            (x1 - x0, depth, y1 - y0), mat, None, "geometry"):
        return Piece(kind, size=(x1 - x0, y1 - y0, depth))
    p = Piece(kind, size=(x1 - x0, y1 - y0, depth))
    p.hole = list(ccw(outline))
    p.mesh.sides(ccw(outline), -depth, 0.0, "z", mat, inward=True)
    p.mesh.flat(outline, -depth, "z", mat)
    return p


PIECES = {"window": window, "door": door, "arch": arch, "column": column, "pilaster": pilaster,
          "course": course, "buttress": buttress, "dormer": dormer, "chimney": chimney,
          "balustrade": balustrade, "steps": steps, "projection": projection, "recess": recess}
