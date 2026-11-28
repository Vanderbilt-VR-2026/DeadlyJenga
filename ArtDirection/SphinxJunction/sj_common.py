# Shared builders for Sphinx Junction asset files (pyramid, western storefronts).
# Used by build_assets.py and build_paintover_match.py. Runs inside Blender.
import bpy, math, random
from mathutils import Vector, Matrix

GROUND = 2.7          # map plaza height; the pyramid asset keeps map heights (base at z 2.7)
PC = Vector((36.0, 16.5, 0.0))
PH = 18.0
PROT = math.radians(-40.0)
HEIGHT = 40.5
COURSE = 1.35
SLOPE = PH / HEIGHT
SHELL = 2.8
STAIR_X, STAIR_W = 7.0, 5.6
PAV_Z = 20.0
PAV_X0, PAV_X1 = STAIR_X - 5.2, STAIR_X + 5.2
PORTAL_BASE, PORTAL_TOP, PORTAL_HW = 10.9, 20.2, 3.9
REAR_TOP, REAR_HW = 23.0, 6.4
TREASURY = (-4.5, 6.5, -7.0, 4.5, 20.0, 26.5)
FACE = math.radians(70)          # storefront facade direction in the map
STORE_START = Vector((-42.6, -2.8, 0))
ROWDIR = Vector((math.cos(FACE), math.sin(FACE), 0))
STORES = [  # (asset name, offset along row)
    ('West | general store', 0.0),
    ('West | sheriff office', 9.2),
    ('West | livery stable', 19.4),
]


ANNEX = [  # south annex on its own pad, facing north toward the town: (asset, facade x, facade y, turn deg)
    ('West | bank', -43.0, -28.2, 180.0),
    ('West | blacksmith', -32.6, -28.2, 180.0),
    ('West | undertaker', -22.8, -28.2, 180.0),
]


def annex_placement(x, y, deg):
    return Matrix.Translation((x, y, GROUND)) @ Matrix.Rotation(math.radians(deg), 4, 'Z')


def pyramid_placement():
    return Matrix.Translation((PC.x, PC.y, 0)) @ Matrix.Rotation(PROT, 4, 'Z')


def store_placement(offset):
    p = STORE_START + ROWDIR * offset
    return Matrix.Translation((p.x, p.y, GROUND)) @ Matrix.Rotation(FACE, 4, 'Z')


def half_at(z):
    return PH - SLOPE * (z - GROUND)


# ------------------------------------------------------------------ helpers
def srgb(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def mat(name, color=None, rough=0.8, emit=None, strength=0.0, metal=0.0):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        b.inputs['Base Color'].default_value = (*color, 1)
        b.inputs['Roughness'].default_value = rough
        b.inputs['Metallic'].default_value = metal
        if emit:
            b.inputs['Emission Color'].default_value = (*emit, 1)
            b.inputs['Emission Strength'].default_value = strength
        m.diffuse_color = (*color, 1)
    return m


def coll(name, parent=None, hide=False):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        if parent is not None:
            parent.children.link(c)
    c.hide_render = hide
    c.hide_viewport = hide
    return c


def T(x, y, z, rz=0.0):
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(rz, 4, 'Z')


class Mesh:
    def __init__(self):
        self.v, self.f, self.mi, self.mats = [], [], [], []

    def _m(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def poly(self, verts, faces, m):
        o = len(self.v); idx = self._m(m)
        self.v += [tuple(p) for p in verts]
        for fc in faces:
            self.f.append(tuple(o + i for i in fc)); self.mi.append(idx)

    def box(self, M, sx, sy, sz, m):
        hx, hy, hz = sx / 2, sy / 2, sz / 2
        pts = [M @ Vector((x, y, z)) for z in (-hz, hz) for y in (-hy, hy) for x in (-hx, hx)]
        self.poly(pts, [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)], m)

    def aabb(self, x0, x1, y0, y1, z0, z1, m):
        self.box(T((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0, m)

    def prism(self, M, r, h, m, n=12, r2=None):
        r2 = r if r2 is None else r2
        pts = [M @ Vector((rr * math.cos(2 * math.pi * i / n), rr * math.sin(2 * math.pi * i / n), z))
               for z, rr in ((0, r), (h, r2)) for i in range(n)]
        faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))]
        faces += [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
        self.poly(pts, faces, m)

    def extrude(self, M, profile, x0, x1, m):
        n = len(profile)
        pts = [M @ Vector((x, y, z)) for x in (x0, x1) for (y, z) in profile]
        faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))]
        faces += [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
        self.poly(pts, faces, m)

    def build(self, name, collection):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.v, [], self.f)
        for m in self.mats:
            me.materials.append(m)
        me.polygons.foreach_set('material_index', self.mi)
        me.validate(); me.update()
        ob = bpy.data.objects.new(name, me)
        collection.objects.link(ob)
        return ob


def light(name, loc, energy, collection, color=(1.0, 0.62, 0.28), radius=0.2, kind='POINT'):
    d = bpy.data.lights.new(name, kind); d.energy = energy; d.color = color; d.shadow_soft_size = radius
    o = bpy.data.objects.new(name, d); o.location = loc
    collection.objects.link(o)
    return o


def bbox(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return (Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)]))


class Mats:
    def __init__(self):
        self.HONEY = mat("Pyramid | golden sandstone", srgb("#D69A4A"), 0.85)
        self.LIGHT = mat("Pyramid | pale gold sandstone", srgb("#E3B266"), 0.85)
        self.AMBER = mat("Pyramid | amber sandstone", srgb("#C98A3F"), 0.85)
        self.OCHRE = mat("Pyramid | deep ochre sandstone", srgb("#B8783A"), 0.85)
        self.INNER = mat("Pyramid | interior dressed stone", srgb("#C99353"), 0.9)
        self.TEAL = mat('Patinated turquoise', srgb('#2E8F8A'))
        self.BRASS = mat('Hammered brass', srgb('#B8862F'), 0.4, metal=1.0)
        self.GILT = mat('Gilt brass', srgb('#E0A93A'), 0.3, metal=1.0)
        self.FLAME = mat('Amber flame', srgb('#FF9A33'), 0.5, srgb('#FF8A22'), 8.0)
        self.IRON = mat('Blackened iron', srgb('#2A2420'))
        self.GLASS = mat('Amber lantern glass', srgb('#FFC070'), 0.3, srgb('#FFA040'), 4.0)
        self.HONEY_WOOD = mat('Honey oiled timber', srgb('#B9844B'))
        self.TERRA_WOOD = mat('Terracotta painted timber', srgb('#A2492E'))
        self.DARK_WOOD = mat('Dark oiled timber', srgb('#4A2E1E'))
        self.RED_WOOD = mat('Oiled redwood', srgb('#7E3A24'))
        self.TIN_TEAL = mat('Tin roof turquoise', srgb('#3C9C93'))
        self.AGED_TEAL = mat('Aged turquoise', srgb('#3A8A84'))
        self.SHADOW = mat('Tunnel | deep shadow stone', srgb('#3A2416'), 1.0)
        self.BARN = mat('West | barn red paint', srgb('#9A3322'), 0.85)
        self.SAGE = mat('West | sheriff sage paint', srgb('#5F8466'), 0.85)
        self.CREAM = mat('West | sign cream', srgb('#EAD3A0'), 0.7)
        self.GREYWOOD = mat('West | weathered grey timber', srgb('#8C7A66'), 0.9)
        self.SHINGLE = mat('West | shingle brown', srgb('#5A3826'), 0.9)
        self.WINDOW = mat('West | lit window', srgb('#FFB561'), 0.4, srgb('#FFA040'), 3.0)
        self.CRATE = mat('West | crate pine', srgb('#B9814A'), 0.85)
        self.GRANITE = mat('Pyramid | rose granite', srgb('#8E4A3E'), 0.45)
        self.LAPIS = mat('Pyramid | lapis inlay', srgb('#1F4FA0'), 0.35)
        self.COIN = mat('Pyramid | heaped gold', srgb('#F2B53C'), 0.25, srgb('#FFB030'), 0.4, metal=1.0)
        self.POOL = mat('Pyramid | sun-pool water', srgb('#36B7B0'), 0.05, srgb('#2FD0C0'), 0.6)


# ------------------------------------------------------------------ storefronts
def storefront(name, collection, kind, M):
    """Western storefront with its own interior. Local origin = facade centre at street level,
    facade faces local -y. kind: general_store | sheriff_office | livery_stable."""
    spec = {
        'general_store': (8.0, 7.0, 7.2, 2.4, M.AGED_TEAL, M.HONEY_WOOD, M.TIN_TEAL, 'crate', M.CREAM, False),
        'sheriff_office': (7.2, 6.6, 4.6, 2.2, M.SAGE, M.CREAM, M.SHINGLE, 'star', M.TERRA_WOOD, False),
        'livery_stable': (9.0, 8.0, 5.0, 0.0, M.BARN, M.CREAM, M.SHINGLE, 'horseshoe', M.CREAM, True),
    }[kind]
    W, D, H, fh, wall, trim, roof, sign_kind, sign_mat, barn = spec
    me = Mesh(); hw = W / 2; M0 = Matrix.Identity(4)
    me.box(T(0, D / 2, 0.15), W, D, 0.3, M.HONEY_WOOD)
    me.box(T(0, D - 0.15, H / 2), W, 0.3, H, wall)
    for s in (-1, 1):
        me.box(T(s * (hw - 0.15), D / 2, H / 2), 0.3, D, H, wall)
    door_w, door_h = (3.4, 4.2) if barn else (1.7, 2.7)
    me.box(T(-(hw + door_w / 2) / 2, 0.15, H / 2), hw - door_w / 2, 0.3, H, wall)
    me.box(T((hw + door_w / 2) / 2, 0.15, H / 2), hw - door_w / 2, 0.3, H, wall)
    me.box(T(0, 0.15, (H + door_h) / 2), door_w, 0.3, H - door_h, wall)
    for zz in [0.6 + 0.45 * i for i in range(int((H - 0.6) / 0.45))]:
        me.box(T(0, -0.02, zz), W, 0.04, 0.05, trim if barn else M.DARK_WOOD)
    for s in (-1, 1):
        me.box(T(s * (door_w / 2 + 0.08), -0.05, door_h / 2), 0.16, 0.2, door_h, trim)
        leaf = T(s * (door_w / 2), -0.05, door_h / 2 + 0.2) @ Matrix.Rotation(s * 1.2, 4, 'Z') @ T(-s * door_w / 4, 0, 0)
        me.box(leaf, door_w / 2 - 0.05, 0.08, door_h - 0.4, wall if not barn else M.BARN)
        if barn:
            me.box(leaf @ Matrix.Rotation(0.9, 4, 'Y'), 0.14, 0.1, door_h * 0.9, M.CREAM)
    me.box(T(0, -0.05, door_h + 0.08), door_w + 0.3, 0.2, 0.16, trim)
    wins = [(-hw * 0.55, 1.9), (hw * 0.55, 1.9)]
    if H > 6:
        wins += [(-hw * 0.55, H - 2.0), (0, H - 2.0), (hw * 0.55, H - 2.0)]
    for wx, wz in wins:
        if barn and wz < 3:
            continue
        me.box(T(wx, -0.01, wz), 1.1, 0.06, 1.3, M.WINDOW)
        me.box(T(wx, -0.06, wz), 1.3, 0.1, 0.12, trim)
        me.box(T(wx, -0.06, wz + 0.7), 1.4, 0.12, 0.14, trim)
        me.box(T(wx, -0.06, wz - 0.7), 1.4, 0.14, 0.12, trim)
        me.box(T(wx, -0.06, wz), 0.08, 0.1, 1.3, trim)
    if barn:
        rh = 3.0
        for s in (-1, 1):
            ang = math.atan2(rh, hw); L = math.hypot(rh, hw) + 0.7
            me.box(T(s * hw / 2, D / 2, H + rh / 2 + 0.1) @ Matrix.Rotation(-s * ang, 4, 'Y'), L, D + 1.2, 0.22, roof)
        gable = [(-hw, 0), (hw, 0), (0, rh)]
        pts = [Vector((x, y, H + zz)) for y in (0.0, D) for (x, zz) in gable]
        me.poly(pts, [(2, 1, 0), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)], wall)
        me.box(T(0, -0.04, H + 0.9), 1.4, 0.08, 1.2, M.GREYWOOD)
        for i in range(9):
            a = math.radians(-40 + i * 32.5)
            me.box(T(0.62 * math.sin(a), -0.1, H + 2.0 + 0.62 * math.cos(a)) @ Matrix.Rotation(-a, 4, 'Y'), 0.3, 0.1, 0.2, M.GILT)
        me.box(T(0, -0.04, H - 0.25), W + 0.2, 0.2, 0.3, M.CREAM)
        # stalls inside
        for i, sx in enumerate((-hw + 1.4, hw - 1.4)):
            for j in range(3):
                me.box(T(sx, 1.6 + j * 2.0, 0.75), 2.4, 0.12, 1.2, M.GREYWOOD)
            me.box(T(sx, D - 1.0, 0.5), 1.2, 0.6, 0.6, M.CRATE)
        me.box(T(0, D - 1.2, 1.0), 2.0, 1.2, 1.4, mat('West | hay', srgb('#D8B45A'), 0.95))
    else:
        me.box(T(0, 0.15, H + fh / 2), W + 0.2, 0.3, fh, wall)
        me.box(T(0, -0.05, H + fh + 0.12), W + 0.7, 0.5, 0.24, trim)
        me.box(T(0, -0.05, H + 0.05), W + 0.4, 0.4, 0.2, trim)
        me.box(T(0, D / 2, H - 0.1), W + 0.3, D + 0.3, 0.25, roof)
        for s in (-1, 1):
            me.box(T(s * (hw + 0.05), -0.08, (H + fh) / 2), 0.35, 0.35, H + fh, trim)
        sz = H + fh * 0.45
        me.box(T(0, -0.12, sz), W * 0.62, 0.12, fh * 0.62, sign_mat)
        me.box(T(0, -0.1, sz), W * 0.62 + 0.3, 0.1, fh * 0.62 + 0.3, trim)
        if sign_kind == 'crate':
            me.box(T(0, -0.35, sz), 1.3, 0.3, 1.0, M.CRATE)
            for d in (-0.35, 0.35):
                me.box(T(d, -0.52, sz), 0.1, 0.05, 1.0, M.DARK_WOOD)
            me.box(T(0, -0.52, sz) @ Matrix.Rotation(0.66, 4, 'Y'), 0.1, 0.05, 1.5, M.DARK_WOOD)
        else:
            pts = []
            for i in range(10):
                rr = 0.95 if i % 2 == 0 else 0.4
                a = math.pi / 2 + i * math.pi / 5
                pts.append((rr * math.cos(a), rr * math.sin(a)))
            v = [Vector((x, yy, sz + zz)) for yy in (-0.4, -0.2) for (x, zz) in pts]
            me.poly(v + [Vector((0, -0.4, sz)), Vector((0, -0.2, sz))],
                    [(20, i, (i + 1) % 10) for i in range(10)] + [(21, 10 + (i + 1) % 10, 10 + i) for i in range(10)]
                    + [(i, 10 + i, 10 + (i + 1) % 10, (i + 1) % 10) for i in range(10)], M.GILT)
        if kind == 'general_store':
            for j in range(3):
                me.box(T(-hw + 0.5, 1.5 + j * 1.6, 1.6), 0.5, 1.4, 2.6, M.HONEY_WOOD)
                for k in range(3):
                    me.box(T(-hw + 0.6, 1.5 + j * 1.6, 0.9 + k * 0.75), 0.35, 1.2, 0.3, M.CRATE)
            me.box(T(hw - 1.2, 1.4, 0.4), 0.9, 0.9, 0.8, M.CRATE)
            me.prism(T(hw - 1.0, 2.6, 0.3), 0.4, 0.9, M.RED_WOOD, 12, 0.36)
        else:
            # sheriff: desk, jail cell bars
            me.box(T(-hw + 1.8, 2.2, 0.75), 2.0, 1.0, 0.1, M.DARK_WOOD)
            me.box(T(-hw + 1.8, 2.2, 0.4), 1.8, 0.8, 0.7, M.RED_WOOD)
            for i in range(12):
                me.box(T(0.4 + i * 0.25, D - 2.2, 1.5), 0.05, 0.05, 2.9, M.IRON)
            me.box(T(1.8, D - 2.2, 2.95), 3.0, 0.08, 0.08, M.IRON)
    pd = 2.4
    me.box(T(0, -pd / 2, 0.2), W + 0.6, pd, 0.4, M.HONEY_WOOD)
    for i in range(int(W / 0.5)):
        me.box(T(-W / 2 + 0.25 + i * 0.5, -pd / 2, 0.41), 0.04, pd, 0.02, M.DARK_WOOD)
    aw_h = 3.4 if not barn else 4.8
    for s in (-1, 1):
        me.box(T(s * (hw + 0.1), -pd + 0.15, aw_h / 2 + 0.2), 0.22, 0.22, aw_h, trim)
    if not barn:
        me.box(T(0, -pd / 2, aw_h + 0.35) @ Matrix.Rotation(-0.22, 4, 'X'), W + 0.8, pd + 0.4, 0.14, roof)
        for s in (-1, 1):
            me.box(T(s * hw * 0.5, -pd + 0.4, aw_h - 0.2) @ Matrix.Rotation(0.6, 4, 'X'), 0.12, 0.12, 0.9, trim)
    me.box(T(0, D * 0.55, 0.85), W * 0.6, 0.7, 1.1, M.DARK_WOOD)
    me.box(T(0, D - 0.5, 1.8), W * 0.8, 0.4, 2.6, M.HONEY_WOOD)
    for i in range(3):
        me.box(T(-hw + 0.9 + i * 0.95, D * 0.3, 0.7), 0.8, 0.8, 0.8, M.CRATE)
    me.build(name + ' | timber body, porch and interior', collection)
    light(name + ' | interior glow', (0, D * 0.45, min(H - 0.6, 3.2)), 260, collection)
    light(name + ' | porch lantern', (0, -0.6, aw_h - 0.2), 70, collection)
    return dict(W=W, D=D, H=H + fh)


# ------------------------------------------------------------------ pyramid
def track_local(spans_world):
    """Railway span centres (world) -> pyramid-local, keyed by span index."""
    inv = pyramid_placement().inverted()
    return {k: inv @ Vector((v.x, v.y, 0)) for k, v in spans_world.items()}


def face_crossings(local_spans):
    keys = sorted(local_spans)
    h = half_at(12.0)
    out = {}
    for a, b in zip(keys, keys[1:]):
        p, q = local_spans[a], local_spans[b]
        ina = abs(p.x) < h and abs(p.y) < h
        inb = abs(q.x) < h and abs(q.y) < h
        if ina == inb:
            continue
        for i in range(400):
            r = p.lerp(q, i / 399)
            if (abs(r.x) < h and abs(r.y) < h) != ina:
                break
        if abs(r.y + h) < 1.5:
            out['front'] = r
        elif abs(r.x + h) < 1.5:
            out['left'] = r
    return out


def build_pyramid(root, cross, M, seed=11):
    rnd = random.Random(seed)
    C_FRONT = coll('Pyramid | stone courses, front face (hide for cutaway)', root)
    C_REST = coll('Pyramid | stone courses, sides and back', root)
    C_EXT = coll('Pyramid | exterior: capstone, podium, portals, stair, pavilion', root)
    C_HALL = coll('Pyramid | interior: Hall of the Sun Line', root)
    C_TREAS = coll('Pyramid | interior: treasury chamber', root)
    C_LIGHT = coll('Pyramid | lanterns and lights', root)
    cut = []      # objects hidden for the cutaway review

    fx = cross['front'].x if 'front' in cross else -6.4
    ly = cross['left'].y if 'left' in cross else -0.7
    door = (-3.7, -0.3)   # ground entrance on the front face (local x)

    def portal_hole(x0, x1, z0, z1, cx, hw=PORTAL_HW, top=PORTAL_TOP):
        if z1 <= PORTAL_BASE or z0 >= top:
            return False
        w = hw * (top - max(z0, PORTAL_BASE)) / (top - PORTAL_BASE) + 0.3
        return x1 > cx - w and x0 < cx + w

    carves = [
        (door[0], door[1], -PH - 1, -half_at(GROUND) + SHELL + 0.6, GROUND, 7.6),
        (TREASURY[0], TREASURY[1], TREASURY[2], TREASURY[3], TREASURY[4], TREASURY[5]),
        (4.4, 6.6, -12.0, TREASURY[2] + 0.5, PAV_Z, 23.2),
        (-1.3, 1.3, 3.8, 9.6, 15.3, 22.9),
    ]

    def carved(a):
        for c in carves:
            if a[0] < c[1] and a[1] > c[0] and a[2] < c[3] and a[3] > c[2] and a[4] < c[5] and a[5] > c[4]:
                return True
        return False

    stone_w = [(M.HONEY, 0.68), (M.AMBER, 0.2), (M.LIGHT, 0.12)]

    def pick():
        r = rnd.random(); acc = 0
        for m, w in stone_w:
            acc += w
            if r <= acc:
                return m
        return M.HONEY

    z = GROUND; k = 0
    while True:
        h = half_at(z)
        if h < 2.0:
            cap_z = z; break
        mf, mr = Mesh(), Mesh()
        t = min(SHELL, h)
        stagger = (k % 2) * 1.1
        # lintels: on the first course above an opening, one long stone spans it with 0.9 m bearing each side
        openings = [(c[0], c[1], c[2], c[3], c[5]) for c in carves]
        openings += [(fx - 1.2, fx + 1.2, -PH - 1, -PH + SHELL + 1, PORTAL_TOP),      # front portal: front wall only
                     (-PH - 1, -PH + SHELL + 1, ly - 1.2, ly + 1.2, REAR_TOP),       # rear portal: left wall only
                     (PAV_X0, PAV_X1, -PH - 1, -half_at(PAV_Z) + SHELL, PAV_Z + 4.6)]
        bands = [(0, -h, h, -h, -h + t), (2, -h, h, h - t, h), (1, h - t, h, -h + t, h - t), (3, -h, -h + t, -h + t, h - t)]
        lintels = []
        for (ox0, ox1, oy0, oy1, otop) in openings:
            if not (z - 0.05 <= otop < z + COURSE - 0.05) or otop <= GROUND + 0.1:
                continue
            for (sd, bx0, bx1, by0, by1) in bands:
                ix0, ix1, iy0, iy1 = max(ox0, bx0), min(ox1, bx1), max(oy0, by0), min(oy1, by1)
                if ix1 - ix0 < 0.2 or iy1 - iy0 < 0.2:
                    continue
                if sd in (0, 2):
                    lintels.append((sd, max(bx0, ix0 - 0.9), min(bx1, ix1 + 0.9), by0, by1))
                else:
                    lintels.append((sd, bx0, bx1, max(by0, iy0 - 0.9), min(by1, iy1 + 0.9)))

        def under_lintel(x0, x1, y0, y1):
            return any(x0 < l[2] and x1 > l[1] and y0 < l[4] and y1 > l[3] for l in lintels)
        for side in range(4):
            # corners alternate course by course (masonry bond), so every corner stone bears on both walls below
            owns = (side in (0, 2)) == (k % 2 == 0)
            L0, L1 = (-h, h) if owns else (-h + t, h - t)
            u = L0 - stagger if owns else L0
            while u < L1 - 0.05:
                blen = rnd.uniform(2.0, 3.3)
                a, b = max(u, L0), min(u + blen, L1)
                u += blen
                if b - a < 0.4:
                    continue
                z0, z1 = z, z + COURSE
                if side == 0:
                    if portal_hole(a, b, z0, z1, fx):
                        continue
                    if z1 > PAV_Z + 0.1 and z0 < PAV_Z + 4.6 and b > PAV_X0 and a < PAV_X1:
                        continue
                if side == 3 and 'left' in cross and portal_hole(-b, -a, z0, z1, -ly, REAR_HW, REAR_TOP):
                    continue
                out = rnd.uniform(0.0, 0.07)
                mid = (a + b) / 2; ln = b - a - 0.07; dep = t - 0.05
                hz = COURSE - 0.06 - rnd.uniform(0, 0.05)
                off = -h + t / 2 - out / 2
                if side == 0: cx, cy, sx, sy = mid, off, ln, dep + out
                elif side == 2: cx, cy, sx, sy = mid, -off, ln, dep + out
                elif side == 1: cx, cy, sx, sy = -off, mid, dep + out, ln
                else: cx, cy, sx, sy = off, mid, dep + out, ln
                if carved((cx - sx / 2, cx + sx / 2, cy - sy / 2, cy + sy / 2, z0, z0 + hz)):
                    continue
                if under_lintel(cx - sx / 2, cx + sx / 2, cy - sy / 2, cy + sy / 2):
                    continue
                (mf if side == 0 else mr).box(T(cx, cy, z + hz / 2), sx, sy, hz, pick())
        hz = COURSE - 0.06
        for (sd, x0, x1, y0, y1) in lintels:
            (mf if sd == 0 else mr).box(T((x0 + x1) / 2, (y0 + y1) / 2, z + hz / 2), x1 - x0 - 0.07, y1 - y0 - 0.05, hz, M.LIGHT)
        if z >= TREASURY[5] - 0.1 and h - t > 0.2:
            mr.box(T(0, 0, z + COURSE - 0.15), 2 * (h - t) + 0.1, 2 * (h - t) + 0.1, 0.3, M.SHADOW)
        if mf.v:
            cut.append(mf.build(f'Great pyramid | course {k:02d} | front face', C_FRONT))
        mr.build(f'Great pyramid | course {k:02d} | sides and back', C_REST)
        z += COURSE; k += 1

    # capstone
    apex = GROUND + HEIGHT
    me = Mesh()
    hb = half_at(cap_z)
    me.box(T(0, 0, cap_z + 0.3), 2 * hb, 2 * hb, 0.6, M.TEAL)
    cz = cap_z + 0.6; hc = half_at(cz)
    pts = [Vector(p) for p in ((-hc, -hc, cz), (hc, -hc, cz), (hc, hc, cz), (-hc, hc, cz), (0, 0, apex + 0.4))]
    me.poly(pts, [(3, 2, 1, 0), (0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)], M.LIGHT)
    ez = cz + 1.0; ey = -half_at(ez) - 0.05
    me.prism(T(0, ey, ez) @ Matrix.Rotation(math.pi / 2, 4, 'X'), 0.42, 0.12, M.GILT, 16)
    for s in (-1, 1):
        me.box(T(s * 0.95, ey - 0.02, ez + 0.05) @ Matrix.Rotation(s * 0.25, 4, 'Y'), 1.2, 0.1, 0.32, M.GILT)
    me.build('Great pyramid | teal band and gilded capstone', C_EXT)

    # podium
    me = Mesh(); ph = PH + 1.2
    for side in range(4):
        if side in (0, 2):
            y = -ph + 0.9 if side == 0 else ph - 0.9
            me.box(T(0, y, 0.6), 2 * ph, 1.8, 4.2, M.AMBER)
            me.box(T(0, y + (-0.92 if side == 0 else 0.92), 2.45), 2 * ph, 0.08, 0.35, M.TEAL)
        else:
            x = -ph + 0.9 if side == 3 else ph - 0.9
            me.box(T(x, 0, 0.6), 1.8, 2 * ph - 3.6, 4.2, M.AMBER)
            me.box(T(x + (-0.92 if side == 3 else 0.92), 0, 2.45), 0.08, 2 * ph - 3.6, 0.35, M.TEAL)
    me.box(T(0, 0, GROUND - 0.25), 2 * ph - 3.4, 2 * ph - 3.4, 0.5, M.AMBER)
    me.build('Great pyramid | podium plinth over quicksand', C_EXT)

    # railway portals (frame + short lining through the shell; the hall opens behind)
    def portal(cx, rotz, name, hw=PORTAL_HW, zt=PORTAL_TOP):
        F = Matrix.Rotation(rotz, 4, 'Z')
        me = Mesh()
        zb = PORTAL_BASE
        fy = -half_at((zb + zt) / 2) - 0.35
        ang = math.atan2(zt - zb, hw); blen = math.hypot(zt - zb, hw) + 1.4
        for s in (-1, 1):
            mx, mz = cx + s * hw / 2, (zb + zt) / 2
            R = Matrix.Rotation(-s * (math.pi / 2 - ang), 4, 'Y')
            me.box(F @ T(mx + s * 0.55, fy, mz) @ R, 1.25, 1.1, blen, M.TEAL)
            me.box(F @ T(mx + s * 0.05, fy - 0.45, mz) @ R, 0.22, 0.4, blen - 1.2, M.GILT)
        me.box(F @ T(cx, fy, zb - 0.45), 2 * hw + 3.2, 1.1, 0.9, M.TEAL)
        ez = zt + 0.9
        me.prism(F @ T(cx, fy - 0.7, ez) @ Matrix.Rotation(math.pi / 2, 4, 'X'), 0.55, 0.15, M.GILT, 16)
        for s in (-1, 1):
            me.box(F @ T(cx + s * 1.3, fy - 0.68, ez + 0.1) @ Matrix.Rotation(s * 0.22, 4, 'Y'), 1.7, 0.12, 0.42, M.GILT)
        f0 = -half_at(zb)
        me.box(F @ T(cx, f0 + 1.8, zb - 0.3), 2 * hw + 0.8, 4.0, 0.6, M.INNER)
        ob = me.build(f'Great pyramid | {name}', C_EXT)
        for i, dd in enumerate((1.0, 3.2)):
            for s in (-1, 1):
                light(f'{name} | lantern {i}{"LR"[s > 0]}', F @ Vector((cx + s * (hw * 0.55), f0 + dd, zb + 3.0)), 60, C_LIGHT)
        return ob
    cut.append(portal(fx, 0.0, 'front railway portal'))
    if 'left' in cross:
        portal(-ly, -math.pi / 2, 'rear railway portal', REAR_HW, REAR_TOP)

    # ground door on the front face
    me = Mesh()
    fy0 = -PH - 0.3
    for s in door:
        me.box(T(s + (-0.35 if s < -2 else 0.35), fy0, GROUND + 2.6), 0.8, 1.0, 5.2, M.TEAL)
    me.box(T(sum(door) / 2, fy0, GROUND + 5.5), door[1] - door[0] + 2.4, 1.0, 0.9, M.TEAL)
    me.box(T(sum(door) / 2, fy0 - 0.5, GROUND + 5.5), door[1] - door[0] + 1.2, 0.1, 0.4, M.GILT)
    me.prism(T(sum(door) / 2, fy0 - 0.55, GROUND + 6.4) @ Matrix.Rotation(math.pi / 2, 4, 'X'), 0.45, 0.12, M.GILT, 16)
    for s in (-1, 1):
        me.box(T(sum(door) / 2 + s * 1.1, fy0 - 0.55, GROUND + 6.45) @ Matrix.Rotation(s * 0.22, 4, 'Y'), 1.4, 0.1, 0.36, M.GILT)
    me.aabb(door[0], door[1], -PH - 0.2, -half_at(GROUND) + SHELL + 0.6, GROUND - 0.02, GROUND + 0.05, M.INNER)
    me.aabb(door[0] - 0.4, door[1] + 0.4, -PH - 0.2, -half_at(GROUND) + SHELL + 0.6, GROUND + 4.9, GROUND + 5.0, M.INNER)
    for s in (-1, 1):
        bx = sum(door) / 2 + s * 2.6
        me.prism(T(bx, fy0 - 1.0, GROUND), 0.16, 0.7, M.BRASS, 8)
        me.prism(T(bx, fy0 - 1.0, GROUND + 0.7), 0.25, 0.3, M.BRASS, 12, 0.5)
        me.prism(T(bx, fy0 - 1.0, GROUND + 0.95), 0.3, 0.45, M.FLAME, 8, 0.05)
        light(f'Ground door brazier {"LR"[s > 0]}', (bx, fy0 - 1.0, GROUND + 1.9), 70, C_LIGHT)
    cut.append(me.build('Great pyramid | ground entrance on the front face', C_EXT))

    # front stair and pavilion
    me = Mesh()
    rise = 0.3
    n_steps = int(round((PAV_Z - GROUND) / rise))
    proj = (PAV_Z - GROUND) - SLOPE * (PAV_Z - GROUND)
    y_foot = -PH - proj
    x0, x1 = STAIR_X - STAIR_W / 2, STAIR_X + STAIR_W / 2
    def face_y(z):   # outer face of the course that contains height z (blocks are vertical-faced)
        cs = GROUND + math.floor(max(0.0, z - 0.001 - GROUND) / COURSE) * COURSE
        return -half_at(cs) - 0.09

    def back_steps(zmax):
        pts, z = [], GROUND
        while z < zmax - 0.01:
            z1 = min(z + COURSE, zmax)
            pts += [(face_y(z), z), (face_y(z), z1)]
            z = z1
        return pts
    for i in range(n_steps):
        top = GROUND + (i + 1) * rise
        yf = y_foot + i * rise; yb = face_y(top)
        zb_ = max(GROUND, top - 1.2)
        me.box(T((x0 + x1) / 2, (yf + yb) / 2, (top + zb_) / 2), STAIR_W, yb - yf, top - zb_, M.LIGHT if i % 2 else M.HONEY)
    fz = PAV_Z - 1.2
    fill = [(y_foot, GROUND)] + back_steps(fz) + [(y_foot + (fz - GROUND), fz)]
    me.extrude(Matrix.Identity(4), fill, x0, x1, M.AMBER)
    for (a, b) in ((x0 - 0.8, x0), (x1, x1 + 0.8)):
        cheek = [(y_foot - 0.3, GROUND)] + back_steps(PAV_Z + 0.6) + [(-half_at(PAV_Z) - 3.4, PAV_Z + 0.6), (y_foot - 0.3, GROUND + 0.9)]
        me.extrude(Matrix.Identity(4), cheek, a, b, M.AMBER)
        me.extrude(Matrix.Identity(4), [(y, zz + 0.62) for (y, zz) in [(y_foot - 0.3, GROUND + 0.9), (-half_at(PAV_Z) - 3.4, PAV_Z + 0.6),
                   (-half_at(PAV_Z) - 3.4, PAV_Z + 0.78), (y_foot - 0.3, GROUND + 1.08)]], a - 0.05, b + 0.05, M.TEAL)
    fyp = -half_at(PAV_Z)
    me.box(T((PAV_X0 + PAV_X1) / 2, fyp - 1.2, PAV_Z - 0.55), PAV_X1 - PAV_X0 + 1.0, 8.0, 1.1, M.HONEY)
    # back wall of the pavilion with the doorway into the treasury passage
    for (a, b) in ((PAV_X0, 4.4), (6.6, PAV_X1)):
        me.aabb(a, b, fyp + 2.9, fyp + 3.5, PAV_Z, PAV_Z + 4.6, M.LIGHT)
    me.aabb(4.4, 6.6, fyp + 2.9, fyp + 3.5, PAV_Z + 3.2, PAV_Z + 4.6, M.LIGHT)
    for s in (4.15, 6.85):
        me.aabb(s - 0.25, s + 0.25, fyp + 2.7, fyp + 3.6, PAV_Z, PAV_Z + 3.5, M.TEAL)
    me.aabb(4.4, 6.6, fyp + 3.5, TREASURY[2] + 0.5, PAV_Z - 0.3, PAV_Z, M.INNER)
    me.aabb(4.1, 4.4, fyp + 3.5, TREASURY[2], PAV_Z, PAV_Z + 3.2, M.INNER)
    me.aabb(6.6, 6.9, fyp + 3.5, TREASURY[2], PAV_Z, PAV_Z + 3.2, M.INNER)
    me.aabb(4.1, 6.9, fyp + 3.5, TREASURY[2], PAV_Z + 3.2, PAV_Z + 3.5, M.INNER)
    for s in (-1, 1):
        me.box(T(STAIR_X + s * 5.0, fyp + 0.8, PAV_Z + 2.3), 0.6, 4.5, 4.6, M.LIGHT)
    for i in range(4):
        cxp = PAV_X0 + 1.0 + i * (PAV_X1 - PAV_X0 - 2.0) / 3
        me.prism(T(cxp, fyp - 3.6, PAV_Z), 0.38, 3.7, M.LIGHT, 12, 0.32)
        me.box(T(cxp, fyp - 3.6, PAV_Z + 3.8), 0.95, 0.95, 0.25, M.TEAL)
        me.box(T(cxp, fyp - 3.6, PAV_Z + 0.12), 0.95, 0.95, 0.25, M.AMBER)
    me.box(T((PAV_X0 + PAV_X1) / 2, fyp - 1.2, PAV_Z + 4.15), PAV_X1 - PAV_X0 + 0.8, 7.4, 0.45, M.TEAL)
    me.box(T((PAV_X0 + PAV_X1) / 2, fyp - 1.2, PAV_Z + 4.6), PAV_X1 - PAV_X0 + 1.3, 7.9, 0.5, M.LIGHT)
    braz = [(x0 - 0.4, y_foot - 0.1, GROUND + 1.1), (x1 + 0.4, y_foot - 0.1, GROUND + 1.1),
            (PAV_X0 - 0.2, fyp - 4.8, PAV_Z), (PAV_X1 + 0.2, fyp - 4.8, PAV_Z)]
    for bx, by, bz in braz:
        me.prism(T(bx, by, bz), 0.16, 0.6, M.BRASS, 8)
        me.prism(T(bx, by, bz + 0.6), 0.25, 0.32, M.BRASS, 12, 0.5)
        me.prism(T(bx, by, bz + 0.85), 0.3, 0.45, M.FLAME, 8, 0.05)
    cut.append(me.build('Great pyramid | processional stair and summit pavilion', C_EXT))
    for i, (bx, by, bz) in enumerate(braz):
        light(f'Pyramid brazier {i}', (bx, by, bz + 1.3), 90, C_LIGHT)

    build_hall(C_HALL, C_LIGHT, M, rnd, door, cut)
    build_treasury(C_TREAS, C_LIGHT, M, rnd, cut)
    return dict(cut=cut, y_foot=y_foot)


def inner_half(z):
    return half_at(z) - SHELL


def frieze(me, M, rnd, z0, skip):
    """Painted glyph band on the four inner walls at height z0 (panels ~1 m)."""
    hi = inner_half(z0 + 0.6) + 0.02
    for side in range(4):
        u = -hi + 0.6
        i = 0
        while u < hi - 0.6:
            if side == 0: p = (u, -hi); R = 0.0
            elif side == 1: p = (hi, u); R = math.pi / 2
            elif side == 2: p = (-u, hi); R = math.pi
            else: p = (-hi, -u); R = -math.pi / 2
            if not any(a <= p[0] <= b and c <= p[1] <= d for (a, b, c, d) in skip):
                F = T(p[0], p[1], z0) @ Matrix.Rotation(R, 4, 'Z')
                me.box(F @ T(0, 0.04, 0.6), 0.96, 0.08, 1.2, M.TEAL if i % 2 == 0 else M.LIGHT)
                g = i % 4
                gm = M.GILT if i % 2 == 0 else M.LAPIS
                if g == 0:      # sun disc
                    me.prism(F @ T(0, 0.08, 0.62) @ Matrix.Rotation(-math.pi / 2, 4, 'X'), 0.26, 0.05, gm, 12)
                elif g == 1:    # ankh
                    me.box(F @ T(0, 0.09, 0.45), 0.1, 0.05, 0.55, gm)
                    me.box(F @ T(0, 0.09, 0.72), 0.36, 0.05, 0.09, gm)
                    me.prism(F @ T(0, 0.09, 0.9) @ Matrix.Rotation(-math.pi / 2, 4, 'X'), 0.13, 0.05, gm, 8)
                elif g == 2:    # bird / eye bar
                    me.box(F @ T(0, 0.09, 0.62), 0.6, 0.05, 0.12, gm)
                    me.box(F @ T(0.12, 0.09, 0.45) @ Matrix.Rotation(0.6, 4, 'Y'), 0.08, 0.05, 0.3, gm)
                else:           # reeds
                    for dx in (-0.2, 0, 0.2):
                        me.box(F @ T(dx, 0.09, 0.62), 0.07, 0.05, 0.7, gm)
                me.box(F @ T(0, 0.06, 0.02), 1.0, 0.12, 0.08, M.GILT)
                me.box(F @ T(0, 0.06, 1.2), 1.0, 0.12, 0.08, M.GILT)
            u += 1.0; i += 1


def brazier(me, M, x, y, z, s=1.0):
    me.prism(T(x, y, z), 0.2 * s, 0.9 * s, M.BRASS, 8, 0.12 * s)
    me.prism(T(x, y, z + 0.9 * s), 0.3 * s, 0.35 * s, M.BRASS, 12, 0.6 * s)
    me.prism(T(x, y, z + 1.15 * s), 0.42 * s, 0.6 * s, M.FLAME, 8, 0.05)


def build_hall(C, CL, M, rnd, door, cut):
    me = Mesh()
    gi = inner_half(GROUND)
    # paved floor with a teal and gold sun ring under the barque
    x = -gi
    while x < gi - 0.1:
        y = -gi
        while y < gi - 0.1:
            me.box(T(x + 1.0, y + 1.0, GROUND + 0.04), 1.94, 1.94, 0.08, M.LIGHT if (int(x) + int(y)) % 4 else M.HONEY)
            y += 2.0
        x += 2.0
    DAIS = Vector((2.2, -4.2))
    for r, m in ((5.2, M.TEAL), (4.8, M.GILT), (4.6, M.HONEY)):
        me.prism(T(DAIS.x, DAIS.y, GROUND + 0.08), r, 0.03 if r > 4.7 else 0.04, m, 32)
    # stepped dais, sun-pool and the gilded solar barque
    for i, (r, h) in enumerate(((3.6, 0.35), (3.0, 0.35), (2.4, 0.35))):
        me.aabb(DAIS.x - r, DAIS.x + r, DAIS.y - r * 0.7, DAIS.y + r * 0.7, GROUND + 0.1 + i * 0.35, GROUND + 0.1 + (i + 1) * 0.35, M.LIGHT if i % 2 else M.HONEY)
    top = GROUND + 1.15
    me.aabb(DAIS.x - 2.3, DAIS.x + 2.3, DAIS.y - 1.5, DAIS.y + 1.5, top - 0.05, top + 0.02, M.POOL)
    # barque hull: tapered slices rising at bow and stern
    for i in range(13):
        t = (i - 6) / 6.0
        w = 0.75 * (1 - t * t) + 0.12
        zc = top + 0.55 + 0.9 * t ** 4
        me.box(T(DAIS.x + t * 2.6, DAIS.y, zc), 0.42, 2 * w, 0.45 + 0.3 * (1 - abs(t)), M.GILT)
    for s in (-1, 1):
        me.box(T(DAIS.x + s * 2.95, DAIS.y, top + 1.75) @ Matrix.Rotation(-s * 0.35, 4, 'Y'), 0.2, 0.2, 1.0, M.GILT)
        me.prism(T(DAIS.x + s * 3.15, DAIS.y, top + 2.2), 0.18, 0.25, M.LAPIS, 10)
    me.box(T(DAIS.x, DAIS.y, top + 1.25), 1.6, 0.9, 0.8, M.TEAL)        # shrine cabin
    me.box(T(DAIS.x, DAIS.y, top + 1.7), 1.8, 1.0, 0.12, M.GILT)
    me.prism(T(DAIS.x, DAIS.y, top + 2.3) @ Matrix.Rotation(math.pi / 2, 4, 'X') @ T(0, 0, -0.06), 0.55, 0.12, M.GILT, 20)
    for i, s in enumerate((-1, 1)):
        brazier(me, M, DAIS.x + s * 3.9, DAIS.y - 2.9, GROUND, 1.2)
        brazier(me, M, DAIS.x + s * 3.9, DAIS.y + 2.9, GROUND, 1.2)

    # papyrus columns with lotus capitals
    cols = [(-9.0, -9.0), (-9.0, 8.8), (5.0, 8.6), (6.8, 4.2), (-9.5, -5.0), (2.0, 8.6)]
    for (cx, cy) in cols:
        me.prism(T(cx, cy, GROUND), 1.25, 0.6, M.AMBER, 16, 1.1)
        me.prism(T(cx, cy, GROUND + 0.6), 0.95, 7.0, M.HONEY, 16, 0.85)
        for b, m in ((2.2, M.TEAL), (2.5, M.GILT), (5.4, M.TEAL), (5.7, M.GILT)):
            me.prism(T(cx, cy, GROUND + b), 0.99 - b * 0.01, 0.22, m, 16)
        me.prism(T(cx, cy, GROUND + 7.6), 0.85, 1.4, M.TEAL, 16, 1.5)
        me.prism(T(cx, cy, GROUND + 9.0), 1.5, 0.25, M.GILT, 16)
        me.box(T(cx, cy, GROUND + 9.55), 2.4, 2.4, 0.6, M.LIGHT)
    # gallery ledge at z 16 on corbels, with a teal and gilt railing
    GZ = 16.0
    gh = inner_half(GZ) + 0.3
    w = 2.4
    for side in range(4):
        R = Matrix.Rotation(side * math.pi / 2, 4, 'Z')
        me.box(R @ T(0, -gh + w / 2 - 0.2, GZ - 0.3), 2 * gh, w, 0.6, M.LIGHT)
        for u in [(-gh + 1.2) + 2.0 * j for j in range(int((2 * gh - 2.4) / 2.0) + 1)]:
            me.box(R @ T(u, -gh + 0.5, GZ - 0.78), 0.5, 1.0, 0.36, M.AMBER)
        for u in [(-gh + w + 0.2) + 1.2 * j for j in range(int((2 * gh - 2 * w - 0.4) / 1.2) + 1)]:
            if side == 2 and abs(u) < 1.4:
                continue
            me.box(R @ T(u, -gh + w - 0.1, GZ + 0.5), 0.12, 0.12, 1.0, M.TEAL)
        for seg in ((-gh + w, -1.4), (1.4, gh - w)) if side == 2 else ((-gh + w, gh - w),):
            me.box(R @ T(sum(seg) / 2, -gh + w - 0.1, GZ + 1.05), seg[1] - seg[0], 0.18, 0.12, M.GILT)
    # right-hand stair: floor -> gallery (x 7.3..9.3, rising +y)
    sx0, sx1 = 7.2, 9.3
    n = int((GZ - GROUND) / 0.3)
    y0 = -10.2
    for i in range(n):
        ztop = GROUND + (i + 1) * 0.3
        me.aabb(sx0, sx1, y0 + i * 0.3, y0 + i * 0.3 + 0.32, ztop - 0.3, ztop, M.LIGHT if i % 2 else M.HONEY)
    L = math.hypot(GZ - GROUND, GZ - GROUND)
    for xx in (sx0 + 0.15, sx1 - 0.15):   # open stair: two stone stringers
        me.box(T(xx, y0 + (GZ - GROUND) / 2, (GROUND + GZ) / 2 - 0.35) @ Matrix.Rotation(-math.pi / 4, 4, 'X'), 0.3, 0.5, L, M.AMBER)
    for xx in (sx0 - 0.12,):
        for i in range(0, n, 3):
            me.box(T(xx, y0 + i * 0.3 + 0.15, GROUND + i * 0.3 + 1.2), 0.1, 0.1, 1.0, M.TEAL)
        L = math.hypot(GZ - GROUND, GZ - GROUND)
        me.box(T(xx, y0 + (GZ - GROUND) / 2, (GROUND + GZ) / 2 + 1.0) @ Matrix.Rotation(-math.pi / 4, 4, 'X'), 0.14, 0.14, L, M.GILT)
    # station platform beside the railway (track runs north at local x ~ -6)
    PZ = 11.55
    me.aabb(-3.6, -0.6, -10.9, -4.6, PZ - 0.6, PZ, M.LIGHT)
    me.aabb(-3.7, -3.5, -10.9, -4.6, PZ - 0.05, PZ + 0.05, M.TEAL)
    for py in (-8.2, -5.4):
        me.prism(T(-1.6, py, GROUND), 0.55, PZ - 0.6 - GROUND, M.HONEY, 12, 0.5)
    me.aabb(-1.4, -0.8, -8.0, -5.4, PZ, PZ + 0.5, M.DARK_WOOD)      # bench
    me.prism(T(-1.0, -4.9, PZ), 0.07, 3.0, M.IRON, 8)
    me.box(T(-1.0, -4.9, PZ + 2.6), 1.6, 0.1, 0.7, M.CREAM)            # station board
    me.box(T(-1.0, -4.96, PZ + 2.6), 1.3, 0.05, 0.35, M.TEAL)
    for py in (-10.4, -7.0):
        me.prism(T(-0.9, py, PZ), 0.06, 2.6, M.IRON, 8)
        me.box(T(-0.9, py, PZ + 2.6), 0.35, 0.35, 0.45, M.GLASS)
    # platform stair down to the floor, heading +x along y -10.9..-9.1
    n2 = int((PZ - GROUND) / 0.3)
    for i in range(n2):
        ztop = PZ - i * 0.3
        me.aabb(-0.6 + i * 0.3, -0.28 + i * 0.3, -10.9, -9.1, ztop - 0.3, ztop, M.LIGHT if i % 2 else M.HONEY)
    L2 = math.hypot(PZ - GROUND, PZ - GROUND)
    for yy in (-10.75, -9.25):
        me.box(T(-0.6 + (PZ - GROUND) / 2, yy, (PZ + GROUND) / 2 - 0.35) @ Matrix.Rotation(math.pi / 4, 4, 'Y'), L2, 0.3, 0.5, M.AMBER)
    # gallery -> treasury stair (x -1.2..1.2, rising -y from y 8.3 to the treasury door)
    n3 = int((TREASURY[4] - GZ) / 0.3)
    for i in range(n3):
        ztop = GZ + (i + 1) * 0.3
        me.aabb(-1.2, 1.2, 8.3 - (i + 1) * 0.3, 8.6 - i * 0.3, ztop - 0.5, ztop, M.LIGHT if i % 2 else M.HONEY)
    # hall ceiling = treasury floor, with stairwell and oculus
    cz = TREASURY[4]
    ch = inner_half(cz - 0.6) + 0.4
    holes = [(-1.3, 1.3, 3.8, 9.6), (DAIS.x - 1.4, DAIS.x + 1.4, DAIS.y - 1.4, DAIS.y + 1.4)]
    step = 1.0
    xx = -ch
    while xx < ch - 0.01:
        yy = -ch
        while yy < ch - 0.01:
            a = (xx, xx + step, yy, yy + step)
            if not any(a[0] < h[1] - 0.01 and a[1] > h[0] + 0.01 and a[2] < h[3] - 0.01 and a[3] > h[2] + 0.01 for h in holes):
                me.aabb(xx, xx + step, yy, yy + step, cz - 0.6, cz, M.INNER)
            yy += step
        xx += step
    # coffered ceiling beams with gold stars under the slab
    for j in range(-6, 7, 2):
        me.aabb(-ch, ch, j - 0.15, j + 0.15, cz - 0.9, cz - 0.6, M.TEAL)
    for j in range(-6, 7, 2):
        for k2 in range(-6, 7, 2):
            me.box(T(j + 1.0, k2 + 1.0, cz - 0.63), 0.25, 0.25, 0.04, M.GILT)
    # wall friezes and sconces
    frieze(me, M, rnd, GROUND + 3.2, skip=[(door[0] - 0.8, door[1] + 0.8, -30, 0), (sx0 - 1, 30, -12, 4)])
    frieze(me, M, rnd, GROUND + 6.4, skip=[(sx0 - 1, 30, -12, 6)])
    for (bx, by) in [(-13.0, -12.5), (-13.0, 12.5), (12.5, 12.5), (12.8, -12.8), (-6.0, 13.0), (-13.3, -5.0)]:
        brazier(me, M, bx, by, GROUND, 1.3)
    me.build('Hall of the Sun Line | floor, columns, barque, gallery, stairs, station', C)
    lights = [((DAIS.x, DAIS.y, GROUND + 4.0), 500), ((-6.5, -9.0, 8.0), 220), ((-6.0, 6.0, 8.0), 220),
              ((7.5, 7.5, 7.0), 200), ((8.0, -7.0, 7.0), 200), ((0.0, 0.0, 17.5), 300), ((-2.0, -9.0, 14.0), 150)]
    for i, (p, e) in enumerate(lights):
        light(f'Hall of the Sun Line | warm fill {i}', p, e, CL, radius=1.0)
    for i, (bx, by) in enumerate([(-13.0, -12.5), (-13.0, 12.5), (12.5, 12.5), (12.8, -12.8), (-6.0, 13.0), (-13.3, -5.0)]):
        light(f'Hall brazier {i}', (bx, by, GROUND + 2.6), 120, CL)
    sp = light('Hall of the Sun Line | oculus sunbeam', (DAIS.x, DAIS.y, TREASURY[4] + 4.0), 4000, CL,
               color=(1.0, 0.78, 0.45), radius=0.6, kind='SPOT')
    sp.data.spot_size = math.radians(24); sp.data.spot_blend = 0.4


def build_treasury(C, CL, M, rnd, cut):
    x0, x1, y0, y1, z0, z1 = TREASURY
    me, mf = Mesh(), Mesh()
    t = 0.5
    # walls (front wall is its own object for the cutaway)
    mf.aabb(x0, 4.4, y0 - t, y0, z0, z1, M.INNER)
    mf.aabb(6.6, x1, y0 - t, y0, z0, z1, M.INNER)
    mf.aabb(4.4, 6.6, y0 - t, y0, z0 + 3.2, z1, M.INNER)
    me.aabb(x0, -1.2, y1, y1 + t, z0, z1, M.INNER)
    me.aabb(1.2, x1, y1, y1 + t, z0, z1, M.INNER)
    me.aabb(-1.2, 1.2, y1, y1 + t, z0 + 2.8, z1, M.INNER)
    me.aabb(x0 - t, x0, y0 - t, y1 + t, z0, z1, M.INNER)
    me.aabb(x1, x1 + t, y0 - t, y1 + t, z0, z1, M.INNER)
    # corbelled ceiling
    for i in range(4):
        d = i * 0.7
        me.aabb(x0 - t + d, x1 + t - d, y0 - t + d, y1 + t - d, z1 + i * 0.5, z1 + (i + 1) * 0.5, M.LIGHT if i % 2 else M.HONEY)
    # floor inlay and oculus railing
    me.aabb(x0, x1, y0, y1, z0 - 0.02, z0 + 0.02, M.HONEY)
    ox, oy = 2.2, -4.2
    for s in (-1, 1):
        me.aabb(ox - 1.6, ox + 1.6, oy + s * 1.5 - 0.1, oy + s * 1.5 + 0.1, z0, z0 + 1.0, M.TEAL)
        me.aabb(ox + s * 1.5 - 0.1, ox + s * 1.5 + 0.1, oy - 1.6, oy + 1.6, z0, z0 + 1.0, M.TEAL)
    for s in (-1, 1):
        me.aabb(ox - 1.7, ox + 1.7, oy + s * 1.5 - 0.15, oy + s * 1.5 + 0.15, z0 + 1.0, z0 + 1.12, M.GILT)
        me.aabb(ox + s * 1.5 - 0.15, ox + s * 1.5 + 0.15, oy - 1.7, oy + 1.7, z0 + 1.0, z0 + 1.12, M.GILT)
    # wall bands
    for zb, m in ((z0 + 2.6, M.TEAL), (z0 + 2.75, M.GILT), (z0 + 4.9, M.LAPIS), (z0 + 5.05, M.GILT)):
        me.aabb(x0 + 0.01, x1 - 0.01, y1 - 0.06, y1, zb, zb + 0.15, m)
        me.aabb(x0, x0 + 0.06, y0, y1, zb, zb + 0.15, m)
        me.aabb(x1 - 0.06, x1, y0, y1, zb, zb + 0.15, m)
    # rose granite sarcophagus with a gilded lid figure
    sx, sy = -1.4, 1.6
    me.aabb(sx - 1.4, sx + 1.4, sy - 0.75, sy + 0.75, z0, z0 + 0.25, M.HONEY)
    me.aabb(sx - 1.25, sx + 1.25, sy - 0.6, sy + 0.6, z0 + 0.25, z0 + 1.35, M.GRANITE)
    me.aabb(sx - 1.3, sx + 1.3, sy - 0.65, sy + 0.65, z0 + 1.35, z0 + 1.55, M.GRANITE)
    me.aabb(sx - 0.9, sx + 0.7, sy - 0.3, sy + 0.3, z0 + 1.55, z0 + 1.75, M.GILT)
    me.prism(T(sx + 0.95, sy, z0 + 1.55), 0.3, 0.35, M.GILT, 12, 0.24)
    me.aabb(sx + 0.75, sx + 1.25, sy - 0.32, sy + 0.32, z0 + 1.55, z0 + 1.9, M.LAPIS)
    # heaps of gold, chests, jars
    for (gx, gy) in ((-3.4, -5.8), (-3.5, 3.6), (5.5, 3.5), (5.6, -2.0)):
        for k in range(5):
            r = 1.1 - k * 0.2
            me.prism(T(gx, gy, z0 + k * 0.18), r, 0.2, M.COIN, 14, r * 0.85)
        for k in range(4):
            me.box(T(gx + rnd.uniform(-0.8, 0.8), gy + rnd.uniform(-0.8, 0.8), z0 + 0.2) @ Matrix.Rotation(rnd.uniform(0, 3), 4, 'Z'),
                   0.5, 0.22, 0.14, M.GILT)
    for (cx, cy) in ((-3.8, -2.4), (4.0, 1.0)):
        me.aabb(cx - 0.6, cx + 0.6, cy - 0.4, cy + 0.4, z0, z0 + 0.7, M.DARK_WOOD)
        me.aabb(cx - 0.62, cx + 0.62, cy - 0.42, cy + 0.42, z0 + 0.7, z0 + 0.85, M.GILT)
    for i in range(4):
        me.prism(T(-4.0, -4.2 + i * 0.5, z0), 0.17, 0.55, M.LIGHT, 10, 0.1)
        me.prism(T(-4.0, -4.2 + i * 0.5, z0 + 0.55), 0.12, 0.14, M.GILT, 10)
    # twin teal obelisks flanking the sarcophagus
    for ox_ in (-4.0, 1.4):
        me.aabb(ox_ - 0.35, ox_ + 0.35, sy + 1.6, sy + 2.3, z0, z0 + 3.6, M.TEAL)
        pts = [Vector((ox_ + a, sy + 1.95 + b, z0 + 3.6)) for a, b in ((-0.35, -0.35), (0.35, -0.35), (0.35, 0.35), (-0.35, 0.35))]
        me.poly(pts + [Vector((ox_, sy + 1.95, z0 + 4.2))], [(3, 2, 1, 0), (0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)], M.GILT)
    for (bx, by) in ((x0 + 0.6, y0 + 0.6), (x1 - 0.6, y1 - 0.6), (x0 + 0.6, y1 - 0.6)):
        brazier(me, M, bx, by, z0, 1.0)

    # painted panels on the treasury walls
    for i in range(10):
        u = x0 + 0.7 + i * 1.05
        if -1.4 < u < 1.4:
            continue
        me.aabb(u - 0.45, u + 0.45, y1 - 0.08, y1 - 0.02, z0 + 3.0, z0 + 4.7, M.TEAL if i % 2 else M.LAPIS)
        me.prism(T(u, y1 - 0.1, z0 + 3.85) @ Matrix.Rotation(math.pi / 2, 4, 'X'), 0.22, 0.04, M.GILT, 12)
    for i in range(10):
        v = y0 + 0.7 + i * 1.08
        for xw, sg in ((x0 + 0.02, 1), (x1 - 0.02, -1)):
            me.aabb(xw - 0.03 + 0.03 * sg, xw + 0.03 + 0.03 * sg, v - 0.42, v + 0.42, z0 + 3.0, z0 + 4.7, M.LAPIS if i % 2 else M.TEAL)
            me.aabb(xw + 0.06 * sg - 0.02, xw + 0.06 * sg + 0.02, v - 0.08, v + 0.08, z0 + 3.3, z0 + 4.4, M.GILT)
    # black-and-gold jackal guardians on plinths
    for (jx, jy, rot) in ((3.2, -5.6, 0.0), (-0.8, 3.4, math.pi)):
        J = T(jx, jy, z0) @ Matrix.Rotation(rot, 4, 'Z')
        me.box(J @ T(0, 0, 0.45), 1.0, 1.6, 0.9, M.SHADOW)
        me.box(J @ T(0, 0, 0.92), 1.04, 1.64, 0.06, M.GILT)
        me.box(J @ T(0, 0.15, 1.3), 0.55, 1.1, 0.7, M.IRON)
        me.box(J @ T(0, -0.45, 1.9), 0.4, 0.4, 0.75, M.IRON)
        me.box(J @ T(0, -0.6, 2.35), 0.36, 0.6, 0.3, M.IRON)
        for s in (-1, 1):
            me.box(J @ T(s * 0.12, -0.45, 2.7), 0.08, 0.12, 0.35, M.IRON)
        me.box(J @ T(0, -0.42, 1.95), 0.5, 0.12, 0.1, M.GILT)
    # stacked gold bars
    for k in range(12):
        bx = 4.2 + (k % 4) * 0.55; by = -6.2 + (k // 4) * 0.3
        me.box(T(bx, by, z0 + 0.08 + (k // 8) * 0.16), 0.5, 0.24, 0.15, M.GILT)
    me.build('Treasury | walls, ceiling, sarcophagus and gold', C)
    cut.append(mf.build('Treasury | front wall (hide for cutaway)', C))
    for i, (p, e) in enumerate((((0.0, 0.0, z1 - 0.8), 220), ((-3.0, 2.5, z0 + 2.2), 140), ((5.0, -4.5, z0 + 2.2), 140))):
        light(f'Treasury | lamp {i}', p, e, CL, radius=0.6)
