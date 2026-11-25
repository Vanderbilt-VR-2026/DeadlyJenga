# Shared scene setup, mesh helpers, bone tools, map-style structure helpers and export for the
# Fossil Basin exhibit pieces. A build script runs exec(open(<this file>).read(), globals()),
# builds its members, then calls finish(title, shots).
import bpy, bmesh, math, json, os, sys, random
from mathutils import Vector, Matrix, Quaternion

OUT = sys.argv[sys.argv.index("--") + 1]
SRC = os.path.join(os.path.dirname(OUT), "Concept Rebuild", "Fossil Basin Faceoff.blend")
random.seed(11)
COMPOSE = bool(os.environ.get("FB_COMPOSE"))   # building into an already open map file
if not COMPOSE:
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections): bpy.data.collections.remove(c)

WANT = ["Terracotta deck 0", "Terracotta deck 1", "Terracotta deck 2", "Terracotta deck 3", "Oxidized teal 0",
        "Oxidized teal 1", "Oxidized teal 2", "Weathered clay rim", "Oxide-stained beam", "Oil-dark brass",
        "Dark bronze", "Ivory cortical fossil", "Mineralized ivory", "Ivory teeth", "Lantern amber", "Black violet tar"]
with bpy.data.libraries.load(SRC, link=False) as (src, dst):
    dst.materials = [m for m in src.materials if m in WANT]
    dst.worlds = [w for w in src.worlds if w == "Tropical dusk"]
    dst.objects = [] if COMPOSE else [o for o in src.objects if o in ("Violet sky fill", "Warm low sun", "Warm sand bounce")]
M = {m.name: m for m in bpy.data.materials}
scene = bpy.context.scene
scene.world = bpy.data.worlds["Tropical dusk"]
hollow = bpy.data.materials.new("Socket shadow"); hollow.use_nodes = True
hollow.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.035, .02, .03, 1)
hollow.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1
M["Socket shadow"] = hollow

def coll(name, parent=None):
    c = bpy.data.collections.new(name); (parent or scene.collection).children.link(c); return c
LIGHTS = coll("00 • Lighting and cameras")
for o in dst.objects: LIGHTS.objects.link(o)
PROXY = coll("99 • Physics proxies")

members, current = {}, None
def member(name, group, kind):
    global current
    current = members[name] = {"coll": coll(name + " • breakaway member", group), "proxies": [], "kind": kind}
    return current

def mesh_obj(name, bm, mat, bevel=0.0):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); o.data.materials.append(M[mat])
    if bevel > 0:
        mod = o.modifiers.new("Chamfer", "BEVEL"); mod.width = bevel; mod.segments = 1; mod.limit_method = "ANGLE"
    current["coll"].objects.link(o)
    return o

def frame(a, b):
    d = b - a
    return (Vector((0, 0, 1)).rotation_difference(d.normalized()) if d.length > 1e-6 else Quaternion()), d.length

def proxy(center, size, rot=Quaternion()):
    current["proxies"].append((Vector(center), Vector(size), rot.copy()))

def box(name, center, size, mat, rot=Quaternion(), bevel=.05, px=False):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = rot @ Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) + Vector(center)
    o = mesh_obj(name, bm, mat, bevel)
    if px: proxy(center, size, rot)
    return o

def shaft(name, a, b, w, d, mat, taper=1.0, px=False, bevel=.03, roll=None):
    a, b = Vector(a), Vector(b); q, n = frame(a, b)
    if roll is not None:   # turn the flat side of a blade to face 'roll'
        side = q @ Vector((1, 0, 0)); want = Vector(roll) - (b - a).normalized() * Vector(roll).dot((b - a).normalized())
        if want.length > 1e-4: q = Quaternion((b - a).normalized(), side.angle(want) * (1 if side.cross(want).dot(b - a) > 0 else -1)) @ q
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        s = taper if v.co.z > 0 else 1.0
        v.co = q @ Vector((v.co.x * w * s, v.co.y * d * s, (v.co.z + .5) * n)) + a
    o = mesh_obj(name, bm, mat, bevel)
    if px: proxy((a + b) / 2, (w, d, n), q)
    return o

def hull(name, back, front, mat, bevel=.12, px=False):
    # Eight-corner block: back and front rectangles listed (-y,-z), (+y,-z), (+y,+z), (-y,+z).
    bm = bmesh.new()
    b = [bm.verts.new(Vector(p)) for p in back]; f = [bm.verts.new(Vector(p)) for p in front]
    bm.faces.new((b[0], b[3], b[2], b[1])); bm.faces.new((f[0], f[1], f[2], f[3]))
    for i in range(4): bm.faces.new((b[i], b[(i + 1) % 4], f[(i + 1) % 4], f[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = mesh_obj(name, bm, mat, bevel)
    if px:
        pts = [Vector(p) for p in back + front]
        lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
        proxy((lo + hi) / 2, hi - lo)
    return o

def disk(name, center, axis, radius, depth, mat, verts=10):
    q, _ = frame(Vector(), Vector(axis)); bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=verts, radius1=radius, radius2=radius, depth=depth)
    for v in bm.verts: v.co = q @ v.co + Vector(center)
    return mesh_obj(name, bm, mat, .02)

def ring(name, center, axis, major, minor, mat, seg=12, stretch=1.0, spin=0.0):
    bm = bmesh.new(); rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        c = Vector((math.cos(a) * major, math.sin(a) * major * stretch, 0)); out = Vector((math.cos(a), math.sin(a), 0))
        rings.append([bm.verts.new(c + out * minor * math.cos(math.pi * k / 2) + Vector((0, 0, minor * math.sin(math.pi * k / 2)))) for k in range(4)])
    for i in range(seg):
        r0, r1 = rings[i], rings[(i + 1) % seg]
        for k in range(4): bm.faces.new((r0[k], r1[k], r1[(k + 1) % 4], r0[(k + 1) % 4]))
    q = frame(Vector(), Vector(axis))[0] @ Quaternion(Vector((0, 0, 1)), spin)
    for v in bm.verts: v.co = q @ v.co + Vector(center)
    return mesh_obj(name, bm, mat)

def socket(name, center, axis, radius, stretch=1.0, spin=0.0, rim=.2):
    # A bone ring with a dark recess behind it reads as a hollow eye socket from far away.
    ring(name + " rim", center, axis, radius, rim, "Ivory cortical fossil", 14, stretch, spin)
    q = frame(Vector(), Vector(axis))[0] @ Quaternion(Vector((0, 0, 1)), spin); bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=14, radius1=radius, radius2=radius, depth=.1)
    for v in bm.verts: v.co = q @ Vector((v.co.x, v.co.y * stretch, v.co.z)) + Vector(center) - Vector(axis).normalized() * .12
    mesh_obj(name + " hollow", bm, "Socket shadow")

def tube(name, pts, radius, mat, taper=.45, px=False, flat=1.0):
    pts = [Vector(p) for p in pts]
    for i in range(len(pts) - 1):
        r0 = radius * (1 - (1 - taper) * i / (len(pts) - 1)); r1 = radius * (1 - (1 - taper) * (i + 1) / (len(pts) - 1))
        a, b = pts[i], pts[i + 1]; d = (b - a).normalized()
        shaft(f"{name} {i}", a - d * r0 * .35, b + d * r1 * .35, 2 * r0, 2 * r0 * flat, mat, r1 / r0)
        if px: proxy((a + b) / 2, (2 * r0, 2 * r0 * flat, (b - a).length), frame(a, b)[0])

def tooth(name, base, tip_dir, out_dir, w, h, mat="Ivory teeth", hook=0.0):
    T = Vector(tip_dir).normalized(); N = Vector(out_dir).normalized()
    S = T.cross(N).normalized(); N = S.cross(T).normalized(); B = Vector(base); th = w * .3
    tip = B + T * h - N * h * hook; bm = bmesh.new()
    a = bm.verts.new(B - S * w / 2 + N * th / 2); b = bm.verts.new(B + S * w / 2 + N * th / 2)
    c = bm.verts.new(B + S * w / 2 - N * th / 2); d = bm.verts.new(B - S * w / 2 - N * th / 2)
    m = bm.verts.new(B + T * h * .5 - N * h * hook * .3 + N * th * .25)
    t = bm.verts.new(tip)
    for f in ((a, b, c, d), (a, m, b), (b, m, t), (m, a, t), (d, c, t), (a, d, t), (b, t, c)): bm.faces.new(f)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_obj(name, bm, mat)

def fan(name, origin, d0, d1, lengths, width, spread=.14, px=False):
    roots, tips = [], []
    d0, d1 = Vector(d0).normalized(), Vector(d1).normalized(); n = d0.cross(d1).normalized()
    for k, L in enumerate(lengths):
        f = k / max(1, len(lengths) - 1); d = (d0 * (1 - f) + d1 * f).normalized()
        o = Vector(origin) + d1 * spread * k; roots.append(o); tips.append(o + d * L)
        shaft(f"{name} ray {k}", o, o + d * L, width * (1 - .4 * f), width * .55, "Ivory cortical fossil", .3, roll=n)
    bm = bmesh.new(); outline = roots[::-1] + tips
    for side in (-1, 1):
        vs = [bm.verts.new(v + n * side * .04) for v in outline]; bm.faces.new(vs if side > 0 else vs[::-1])
    mesh_obj(f"{name} plate", bm, "Mineralized ivory")
    if px:
        u = d0; v = n.cross(u)
        cu = [(w - roots[0]).dot(u) for w in outline]; cv = [(w - roots[0]).dot(v) for w in outline]
        c = roots[0] + u * (max(cu) + min(cu)) / 2 + v * (max(cv) + min(cv)) / 2
        proxy(c, (max(cv) - min(cv), .3, max(cu) - min(cu)), Matrix((v, n, u)).transposed().to_quaternion())

joints = []
def join(a, b, at, force): joints.append({"a": a, "b": b, "at": Vector(at), "breakForce": force})


def _bone_material(name, light, dark, crevice, rough=.82, bump=.35):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; n = nt.nodes; L = nt.links
    bsdf = n["Principled BSDF"]
    tex = n.new("ShaderNodeTexCoord")
    noise = n.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 3.5; noise.inputs["Detail"].default_value = 8
    L.new(tex.outputs["Object"], noise.inputs["Vector"])
    ramp = n.new("ShaderNodeValToRGB"); ramp.color_ramp.elements[0].color = (*dark, 1); ramp.color_ramp.elements[1].color = (*light, 1)
    ramp.color_ramp.elements[0].position = .3; ramp.color_ramp.elements[1].position = .7
    L.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    ao = n.new("ShaderNodeAmbientOcclusion"); ao.inputs["Distance"].default_value = 1.4
    mix = n.new("ShaderNodeMix"); mix.data_type = "RGBA"
    mix.inputs[6].default_value = (*crevice, 1)
    L.new(ao.outputs["AO"], mix.inputs[0]); L.new(ramp.outputs["Color"], mix.inputs[7])
    L.new(mix.outputs[2], bsdf.inputs["Base Color"])
    fine = n.new("ShaderNodeTexNoise"); fine.inputs["Scale"].default_value = 28; fine.inputs["Detail"].default_value = 10
    L.new(tex.outputs["Object"], fine.inputs["Vector"])
    bp = n.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = bump
    L.new(fine.outputs["Fac"], bp.inputs["Height"]); L.new(bp.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = rough
    M[name] = m
    return name

BONE = _bone_material("Weathered fossil bone", (.78, .73, .62), (.50, .47, .40), (.15, .13, .11))
TOOTH = _bone_material("Weathered fossil tooth", (.90, .84, .70), (.70, .62, .48), (.25, .19, .14), .55, .15)
FIN = _bone_material("Weathered fossil cartilage", (.66, .57, .44), (.42, .35, .27), (.14, .11, .09), .85, .45)
CUTTERS = coll("07 • Socket cutters")
GRIT = bpy.data.textures.new("Fossil grit", "CLOUDS"); GRIT.noise_scale = .16; GRIT.noise_depth = 4
DARK = _bone_material("Fossil socket interior", (.20, .16, .12), (.10, .08, .06), (.03, .02, .02), .95, .6)
def weather(o, strength=.035):
    d = o.modifiers.new("Weathering", "DISPLACE"); d.texture = GRIT; d.strength = strength; d.mid_level = .5
    d.texture_coords = "LOCAL"

def _catmull(ctrl, per=6):
    pts = [Vector(p) for p in ctrl]
    if len(pts) == 2: return [pts[0].lerp(pts[1], i / (per * 2)) for i in range(per * 2 + 1)]
    ext = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    out = []
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for k in range(per):
            t = k / per; t2, t3 = t * t, t * t * t
            out.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(pts[-1])
    return out

def ell(rx, ry, k=10): return [(rx * math.cos(2 * math.pi * i / k), ry * math.sin(2 * math.pi * i / k)) for i in range(k)]
def rrect(w, top, bot, e=.5, k=18):
    out = []
    for i in range(k):
        a = 2 * math.pi * i / k; c, s = math.cos(a), math.sin(a)
        out.append((w * math.copysign(abs(c) ** e, c), (top if s > 0 else bot) * math.copysign(abs(s) ** e, s)))
    return out

def loft(name, ctrl, section, mat=None, up=(0, 0, 1), per=6, sub=2, px=False, cap=True, grit=.03):
    mat = mat or BONE
    pts = _catmull(ctrl, per); n = len(pts)
    tans = [(pts[min(n - 1, i + 1)] - pts[max(0, i - 1)]).normalized() for i in range(n)]
    nrm = Vector(up) - tans[0] * Vector(up).dot(tans[0])
    if nrm.length < 1e-4: nrm = tans[0].orthogonal()
    nrm.normalize()
    bm = bmesh.new(); rings = []
    for i, (p, t) in enumerate(zip(pts, tans)):
        if i: nrm = (nrm - t * nrm.dot(t)).normalized()       # parallel transport
        b = t.cross(nrm)
        rings.append([bm.verts.new(p + b * u + nrm * v) for u, v in section(i / (n - 1))])
    k = len(rings[0])
    for r0, r1 in zip(rings, rings[1:]):
        for j in range(k): bm.faces.new((r0[j], r0[(j + 1) % k], r1[(j + 1) % k], r1[j]))
    if cap:
        for ring_, p, t, sgn in ((rings[0], pts[0], tans[0], -1), (rings[-1], pts[-1], tans[-1], 1)):
            c = sum((v.co for v in ring_), Vector()) / k
            tipv = bm.verts.new(c + t * sgn * .25 * max((v.co - c).length for v in ring_))
            for j in range(k):
                f = (ring_[j], ring_[(j + 1) % k], tipv) if sgn > 0 else (ring_[(j + 1) % k], ring_[j], tipv)
                bm.faces.new(f)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = mesh_obj(name, bm, mat)
    o.data.shade_smooth()
    if sub:
        s = o.modifiers.new("Smooth", "SUBSURF"); s.levels = 1; s.render_levels = sub
        if sub >= 2 and grit: weather(o, grit)
    if px:
        cs = [Vector(p) for p in ctrl]
        for a, b in zip(cs, cs[1:]):
            mid = (a + b) / 2; sec = section(.5)
            w = 2 * max(abs(u) for u, v in sec); d = max(v for u, v in sec) - min(v for u, v in sec)
            proxy(mid, (w, d, (b - a).length), frame(a, b)[0])
    return o

def knobby(r0, r1=None, flat=1.0, knob=1.45, k=10):
    r1 = r0 if r1 is None else r1
    def sec(t):
        r = (r0 + (r1 - r0) * t) * (1 + (knob - 1) * (math.exp(-(t / .09) ** 2) + math.exp(-((1 - t) / .09) ** 2)))
        return ell(r, r * flat, k)
    return sec

def taper(r0, r1, flat=1.0, k=10, power=1.0):
    return lambda t: ell(r0 + (r1 - r0) * t ** power, (r0 + (r1 - r0) * t ** power) * flat, k)

def bone(name, ctrl, r0, r1=None, flat=1.0, knob=1.45, up=(0, 0, 1), px=True):
    return loft(name, ctrl, knobby(r0, r1, flat, knob), up=up, px=px)

def fang(name, base, tip_dir, out_dir, w, h, hook=.15, mat=None):
    T = Vector(tip_dir).normalized(); N = Vector(out_dir).normalized(); B = Vector(base)
    ctrl = [B - T * h * .12, B + T * h * .45 - N * h * hook * .3, B + T * h - N * h * hook]
    sec = lambda t: ell(w * .5 * (1 - t) ** .9 + .006, w * .34 * (1 - t) ** .9 + .005, 7)
    return loft(name, ctrl, sec, mat or TOOTH, up=N, per=4, sub=1)

def plate(name, outline, normal, th=.08, mat=None, sub=2):
    nrm = Vector(normal).normalized(); bm = bmesh.new()
    top = [bm.verts.new(Vector(p) + nrm * th) for p in outline]; bot = [bm.verts.new(Vector(p) - nrm * th) for p in outline]
    bm.faces.new(top); bm.faces.new(bot[::-1]); k = len(outline)
    for j in range(k): bm.faces.new((top[j], bot[j], bot[(j + 1) % k], top[(j + 1) % k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = mesh_obj(name, bm, mat or FIN); o.data.shade_smooth()
    s = o.modifiers.new("Smooth", "SUBSURF"); s.levels = 1; s.render_levels = sub
    return o

def cutters(label, items):
    c = coll(label + " cutters", CUTTERS)
    for centre, radii in items:
        bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1)
        for v in bm.verts: v.co = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2])) + Vector(centre)
        me = bpy.data.meshes.new(label + " cutter"); bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new(label + " cutter", me); o.data.materials.append(M[DARK]); o.display_type = "WIRE"; o.hide_render = True; c.objects.link(o)
    return c

def carve(obj, cutter_coll):
    b = obj.modifiers.new("Sockets", "BOOLEAN"); b.operation = "DIFFERENCE"
    b.operand_type = "COLLECTION"; b.collection = cutter_coll; b.solver = "EXACT"; b.material_mode = "TRANSFER"

def fin(name, root0, root1, tips, normal, rays=True, th=.07):
    # Cartilage fin: a rounded plate with raised radial rays on both faces.
    root0, root1 = Vector(root0), Vector(root1)
    roots = [root0.lerp(root1, i / (len(tips) - 1)) for i in range(len(tips))]
    plate(name + " plate", roots[::-1] + [Vector(t) for t in tips], normal, th)
    nrm = Vector(normal).normalized()
    if rays:
        for i, (r, t) in enumerate(zip(roots, tips)):
            t = Vector(t); L = (t - r).length
            for side in (-1, 1):
                loft(f"{name} ray {i}", [r + nrm * side * th * .8, r.lerp(t, .5) + nrm * side * th * .9, t.lerp(r, .04) + nrm * side * th * .3],
                     taper(.07 + .015 * L, .02, .6, 6), FIN, up=nrm, per=3, sub=1)


CLAYS = ["Terracotta deck 0", "Terracotta deck 1", "Terracotta deck 2", "Terracotta deck 3"]
TEALS = ["Oxidized teal 0", "Oxidized teal 1", "Oxidized teal 2"]
BLOCK, TILE = 1.30, (1.25, 1.17, .64)
BEAM = .62

def pier(name, x, y, z0, z1, group, unit=3, size=1.02, kind="pier"):
    blocks, z, k = [], z0, 0
    while z + .3 < z1:
        h = min(BLOCK, z1 - z); blocks.append((z + h / 2, h, "Weathered clay rim" if k % 3 == 1 else TEALS[k % 3])); z += h + .02; k += 1
    for s in range(0, len(blocks), unit):
        member(f"{name} unit {s // unit}", group, kind); part = blocks[s:s + unit]
        for zc, h, mat in part: box(f"{name} block", (x, y, zc), (size, size, h - .02), mat, bevel=.07)
        lo, hi = part[0][0] - part[0][1] / 2, part[-1][0] + part[-1][1] / 2
        proxy((x, y, (lo + hi) / 2), (size, size, hi - lo))

def floor(label, top, x0, x1, y0, y1, strips, group):
    xs = [x0 + TILE[0] * (i + .5) for i in range(int(round((x1 - x0) / TILE[0])))]
    ys = [y0 + TILE[1] * (j + .5) for j in range(int(round((y1 - y0) / TILE[1])))]
    for s in range(strips):
        member(f"{label} strip {s}", group, "deck")
        sx = [x for i, x in enumerate(xs) if min(strips - 1, i * strips // len(xs)) == s]
        for x in sx:
            for y in ys: box("Terracotta deck stone", (x, y, top - TILE[2] / 2), (TILE[0] - .03, TILE[1] - .03, TILE[2]), random.choice(CLAYS), bevel=.08)
        proxy(((sx[0] + sx[-1]) / 2, (ys[0] + ys[-1]) / 2, top - TILE[2] / 2), (sx[-1] - sx[0] + TILE[0], ys[-1] - ys[0] + TILE[1], TILE[2]))

def lantern(x, y, z):
    box("Lantern plinth", (x, y, z + .1), (.3, .3, .2), "Oxide-stained beam", bevel=.03)
    box("Lantern glass", (x, y, z + .34), (.17, .17, .28), "Lantern amber", bevel=.02)


def chain(label, top, bottom, to_top, to_bottom, size=.13, group=None):
    global current
    top, bottom = Vector(top), Vector(bottom); n = int((top - bottom).length / (size * 2.05)); per = 7
    names = []
    for s in range(0, n, per):
        member(f"Chain {label} {s // per}", group, "chain"); names.append(f"Chain {label} {s // per}")
        for i in range(s, min(n, s + per)):
            ring(f"Chain link {label} {i}", top.lerp(bottom, (i + .5) / n), (1, 0, 0) if i % 2 else (0, 1, 0), size, size * .32, "Dark bronze", 8, 1.7, math.pi / 2)
        a, b = top.lerp(bottom, s / n), top.lerp(bottom, min(n, s + per) / n)
        proxy((a + b) / 2, (size * 2.4, size * 2.4, (b - a).length), frame(a, b)[0])
    box(f"Hoist shackle {label}", bottom - Vector((0, 0, .2)), (size * 2.6, size, .4), "Oil-dark brass", bevel=.03)
    current = members[to_top]
    disk(f"Hoist pulley {label}", top + Vector((0, 0, .15)), (0, 1, 0), size * 2.8, .3, "Oil-dark brass", 12)
    join(names[0], to_top, top, 9000)
    for a, b in zip(names[1:], names): join(a, b, members[a]["proxies"][0][0], 9000)
    join(names[-1], to_bottom, bottom, 9000)


def make_tar(group):
    tar = bpy.data.objects.new("Black violet tar", bpy.data.meshes.new("Tar"))
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=160); bm.to_mesh(tar.data); bm.free()
    tar.data.materials.append(M["Black violet tar"]); group.objects.link(tar)

def transformed(build, origin, yaw_deg, scale=1.0):
    # Run build() in a local frame, then move every new member, proxy, joint and cutter into place.
    before_m, before_j = set(members), len(joints)
    before_o = set(bpy.data.objects)
    build()
    Rq = Quaternion(Vector((0, 0, 1)), math.radians(yaw_deg))
    mat = Matrix.Translation(Vector(origin)) @ Rq.to_matrix().to_4x4() @ Matrix.Scale(scale, 4)
    for o in set(bpy.data.objects) - before_o: o.matrix_world = mat @ o.matrix_world
    for n in set(members) - before_m:
        members[n]["proxies"] = [(mat @ c, s * scale, Rq @ q) for c, s, q in members[n]["proxies"]]
    for j in joints[before_j:]: j["at"] = mat @ Vector(j["at"])

def use(name):
    global current
    current = members[name]

def stair_tower(label, x0, y0, bottom, top, flights, group, lane=2.4, gap=.2, depth=3.51, run=11.78, openings=(), open_edges=()):
    # Switchback stair: flights alternate between two lanes and turn on full-width landings.
    # Every step is a solid block with an even rise (about 0.2 m), so survivors and bots can walk it.
    # openings: landing numbers whose west side is left open for a bridge.
    x_end = x0 + 2 * lane + gap; y1 = y0 + 2 * depth + run
    zs = [bottom + (top - bottom) * j / flights for j in range(flights + 1)]
    for j, z in enumerate(zs):
        south = j % 2 == 0
        ya, yb = (y0, y0 + depth) if south else (y1 - depth, y1)
        floor(f"{label} landing {j}", z, x0, x_end, ya, yb, 1, group)
        edge_y = ya + .12 if south else yb - .12
        if j not in open_edges:
            for k in range(5):
                x = x0 + .3 + (x_end - x0 - .6) * k / 4
                box("Landing railing post", (x, edge_y, z + .55), (.12, .12, 1.1), "Oxide-stained beam", bevel=.02)
            box("Landing railing", ((x0 + x_end) / 2, edge_y, z + 1.05), (x_end - x0, .07, .07), "Oxide-stained beam", bevel=0)
        box("Landing railing", (x_end - .12, (ya + yb) / 2, z + 1.05), (.07, yb - ya, .07), "Oxide-stained beam", bevel=0)
        if j not in openings:
            box("Landing railing", (x0 + .12, (ya + yb) / 2, z + 1.05), (.07, yb - ya, .07), "Oxide-stained beam", bevel=0)
        member(f"{label} landing {j} beams", group, "timber")
        for y in (ya + .4, yb - .4):
            box("Landing beam", ((x0 + x_end) / 2, y, z - TILE[2] - .2), (x_end - x0 + 1.7, .42, .4), "Oxide-stained beam", bevel=.04, px=True)
    for x in (x0 - .45, x_end + .45):
        for y in (y0 + .4, y0 + depth - .4, y1 - depth + .4, y1 - .4):
            pier(f"{label} column {x:+.1f}/{y:+.1f}", x, y, -.03, top + 1.2, group, size=.8)
    for k in range(flights):
        z0, z1 = zs[k], zs[k + 1]
        north = k % 2 == 0
        xa, xb = (x0, x0 + lane) if north else (x0 + lane + gap, x_end)
        ya, yb = (y0 + depth, y1 - depth) if north else (y1 - depth, y0 + depth)
        n = max(2, round((z1 - z0) / .2)); rise = (z1 - z0) / n; step = (yb - ya) / n
        member(f"{label} flight {k}", group, "stair")
        cx = (xa + xb) / 2
        for i in range(n):
            ytop = ya + step * (i + .5); ztop = z0 + rise * (i + 1)
            box("Stair step", (cx, ytop, ztop - (rise + .3) / 2), (lane - .1, abs(step) + .01, rise + .3), random.choice(TEALS), bevel=.03)
            box("Brass nosing", (cx, ya + step * i + .03 * math.copysign(1, step), ztop - .02), (lane - .1, .05, .05), "Oil-dark brass", bevel=0)
        a = Vector((cx, ya, z0)); b = Vector((cx, yb, z1))
        q, length = frame(a, b)
        proxy((a + b) / 2 - Vector((0, 0, .3)), (lane, .6, length), q)
        for sx in (xa + .06, xb - .06):
            shaft("Stair stringer", (sx, ya, z0 - .45), (sx, yb, z1 - .45), .12, .5, "Oxide-stained beam", bevel=.02)
            for f in (.06, .5, .94):
                p = Vector((sx, ya + (yb - ya) * f, z0 + (z1 - z0) * f))
                box("Handrail post", p + Vector((0, 0, .5)), (.1, .1, 1.0), "Oxide-stained beam", bevel=.02)
            shaft("Handrail", (sx, ya, z0 + 1.0), (sx, yb, z1 + 1.0), .09, .09, "Oil-dark brass", bevel=.01)
    return x_end, y1, zs

def flight(label, a, b, width, group):
    # A straight stair between two walking surfaces: solid 0.2 m steps, stringers, handrails, two supports.
    member(label, group, "stair")
    a, b = Vector(a), Vector(b); lo, hi = (a, b) if a.z < b.z else (b, a)
    run = Vector((hi.x - lo.x, hi.y - lo.y, 0)); L = run.length; d = run / L; side = Vector((-d.y, d.x, 0))
    n = max(2, round((hi.z - lo.z) / .2)); r = (hi.z - lo.z) / n; st = L / n
    yaw = Quaternion(Vector((0, 0, 1)), math.atan2(d.y, d.x))
    for i in range(n):
        c = lo + d * st * (i + .5); zt = lo.z + r * (i + 1)
        box("Stair step", (c.x, c.y, zt - (r + .3) / 2), (st + .01, width - .1, r + .3), random.choice(TEALS), yaw, .03)
        box("Brass nosing", lo + d * (st * i + .03) + Vector((0, 0, zt - lo.z - .02)), (.05, width - .1, .05), "Oil-dark brass", yaw, 0)
    q, length = frame(lo, hi); proxy((lo + hi) / 2 - Vector((0, 0, .3)), (width, .6, length), q)
    for s in (-1, 1):
        o = side * s * (width / 2 - .06)
        shaft("Stair stringer", lo + o - Vector((0, 0, .45)), hi + o - Vector((0, 0, .45)), .12, .5, "Oxide-stained beam", bevel=.02)
        shaft("Handrail", lo + o + Vector((0, 0, 1.0)), hi + o + Vector((0, 0, 1.0)), .09, .09, "Oil-dark brass", bevel=.01)
        for f in (.05, .5, .95): box("Handrail post", lo.lerp(hi, f) + o + Vector((0, 0, .5)), (.1, .1, 1.0), "Oxide-stained beam", bevel=.02)
    for f in (.33, .66):
        p = lo.lerp(hi, f)
        if p.z > 1.2: pier(f"{label} support {f:.2f}", p.x, p.y, -.03, p.z - .55, group, size=.8)

def bridge(label, a, b, width, z, group, piers=False):
    # A plank bridge with rails between two walking surfaces at the same height.
    member(label, group, "deck")
    a, b = Vector((a[0], a[1], z)), Vector((b[0], b[1], z))
    d = b - a; side = Vector((-d.y, d.x, 0)).normalized(); q, length = frame(a, b)
    n = max(2, int(length / .32))
    flat = Quaternion(Vector((0, 0, 1)), math.atan2(d.y, d.x))
    for i in range(n):
        box("Bridge plank", a.lerp(b, (i + .5) / n) - Vector((0, 0, .1)), (length / n + .02, width, .2), "Oxide-stained beam", flat, .02)
    proxy((a + b) / 2 - Vector((0, 0, .1)), (length, width, .2), flat)
    for s in (-1, 1):
        shaft("Bridge rail", a + side * s * width / 2 + Vector((0, 0, 1.0)), b + side * s * width / 2 + Vector((0, 0, 1.0)), .08, .08, "Oil-dark brass", bevel=.01)
        for f in (0, .5, 1):
            box("Bridge post", a.lerp(b, f) + side * s * width / 2 + Vector((0, 0, .5)), (.1, .1, 1.0), "Oxide-stained beam", bevel=.02)
    if piers:
        for k in range(1, int(length / 4.5) + 1):
            p = a.lerp(b, k / (int(length / 4.5) + 1)); pier(f"{label} pier {k}", p.x, p.y, -.03, z - .2, group, size=.7)

def cam(name, eye, look, lens):
    c = bpy.data.objects.new(name, bpy.data.cameras.new(name)); LIGHTS.objects.link(c)
    c.location = eye; c.rotation_euler = (Vector(look) - Vector(eye)).to_track_quat("-Z", "Y").to_euler()
    c.data.lens = lens; c.data.clip_end = 500; return c

def finish(title, shots_spec, prefix=""):
    STRENGTH = {"pier": 7200, "timber": 8000, "deck": 5200, "roof": 4000, "stair": 3900}
    def boxes_of(m):
        out = []
        for c, s, q in m["proxies"]:
            pts = [c + q @ Vector((s.x * x / 2, s.y * y / 2, s.z * z / 2)) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
            out.append((Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])))
        return out
    struct = [(n, boxes_of(m), m["kind"]) for n, m in members.items() if m["kind"] in STRENGTH]
    for i, (na, ba, ka) in enumerate(struct):
        if ka == "pier" and any(lo.z < .1 for lo, hi in ba):
            join(na, "bearing", (ba[0][0] + ba[0][1]) / 2 - Vector((0, 0, (ba[0][1].z - ba[0][0].z) / 2)), 7200)
        for nb, bb, kb in struct[i + 1:]:
            hit = None
            for la, ha in ba:
                for lb, hb in bb:
                    if all(la[k] <= hb[k] + .08 and lb[k] <= ha[k] + .08 for k in range(3)):
                        hit = Vector([(max(la[k], lb[k]) + min(ha[k], hb[k])) / 2 for k in range(3)]); break
                if hit: break
            if hit: join(na, nb, hit, min(STRENGTH[ka], STRENGTH[kb]))
    palette = [(.95, .35, .25), (.2, .75, .8), (.95, .8, .3), (.55, .4, .95), (.35, .9, .45), (.95, .5, .8)]
    for i, (name, m) in enumerate(members.items()):
        pc = coll(name + " proxies", PROXY)
        mat = bpy.data.materials.new(name + " proxy"); mat.use_nodes = True
        mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*palette[i % 6], 1)
        for k, (c, s, q) in enumerate(m["proxies"]):
            bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
            for v in bm.verts: v.co = q @ Vector((v.co.x * s.x, v.co.y * s.y, v.co.z * s.z)) + c
            me = bpy.data.meshes.new(f"{name} proxy {k}"); bm.to_mesh(me); bm.free()
            o = bpy.data.objects.new(f"{name} proxy {k}", me); o.data.materials.append(mat)
            o.display_type = "WIRE"; o.hide_render = True; pc.objects.link(o)
    def U(v): return {"x": round(v.x, 4), "y": round(v.z, 4), "z": round(v.y, 4)}
    data = {"source": title + ".blend", "axes": "Unity (x, y = Blender z, z = Blender y)", "members": [], "joints": []}
    for name, m in members.items():
        vol = sum(s.x * s.y * s.z for _, s, _ in m["proxies"])
        mass = round(max(3, min(90, vol * (140 if m["kind"] == "chain" else 40))), 1); m["coll"]["mass"] = mass
        cen = sum((c for c, _, _ in m["proxies"]), Vector()) / max(1, len(m["proxies"]))
        data["members"].append({"name": name, "kind": m["kind"], "mass": mass, "p": U(cen),
            "boxes": [{"p": U(c), "size": {"x": round(s.x, 4), "y": round(s.y, 4), "z": round(s.z, 4)},
                       "forward": U(q @ Vector((0, 0, 1))), "up": U(q @ Vector((0, 1, 0)))} for c, s, q in m["proxies"]]})
    data["joints"] = [{**j, "at": U(Vector(j["at"]))} for j in joints]
    json.dump(data, open(os.path.join(OUT, title + " physics.json"), "w"), indent=1)
    shots = [cam(*s) for s in shots_spec]
    QUICK = os.environ.get("TOWER_QUICK")
    if QUICK: shots = [c for c in shots if c.name[:2] in QUICK.split(",")]
    s = scene; s.render.engine = "CYCLES"; s.cycles.samples = 24 if QUICK else 32; s.cycles.use_denoising = True
    s.render.resolution_x, s.render.resolution_y = 1360, 906
    for c in shots:
        s.camera = c; s.render.filepath = os.path.join(OUT, prefix + c.name.replace(" • ", " ") + ".png")
        bpy.ops.render.render(write_still=True); print("TOWER rendered", s.render.filepath)
    if QUICK: return
    for o in bpy.data.objects:
        if o.users_collection and o.users_collection[0].name.endswith("proxies"): o.hide_render = False
        elif o.type == "MESH" and o.name != "Black violet tar": o.hide_render = True
    s.camera = shots[0]; s.render.filepath = os.path.join(OUT, prefix + "Physics proxies.png"); bpy.ops.render.render(write_still=True)
    for o in bpy.data.objects:
        if o.users_collection and o.users_collection[0].name.endswith("proxies"): o.hide_render = True
        elif o.type == "MESH": o.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, title + ".blend"), compress=True)
    print("TOWER members", len(members), "joints", len(joints), "objects", len(bpy.data.objects))
