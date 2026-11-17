"""Christmas fir model: barked trunk, layered drooping boughs, baubles and a light string.

Run:  blender -b -P fir.py -- <out folder> <preview.png> <sprig texture.png>
Blender frame: z up, trunk base at the origin. Units match the map (the tree is 138 tall).
Writes one OBJ per material plus "Fir Lights.txt" (bulb and sparkle positions in Unity axes).
"""
import bpy, math, random, sys, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
out_dir, out_png, sprig_png = argv[0], argv[1], argv[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
rand = random.Random(241215)

TOP, LOW = 138.0, 36.0          # tree top and lowest whorl on the trunk
TRUNK = 4.75                    # trunk radius where players climb it
FLOOR_CLEAR = 28.5              # nothing hangs lower than this (gifts near the tree reach 26)


class Part:
    def __init__(self, name):
        self.name, self.v, self.f, self.uv, self.n = name, [], [], [], []

    def vert(self, p, uv, n=None):
        self.v.append(Vector(p))
        self.uv.append(uv)
        self.n.append(Vector(n).normalized() if n is not None else None)
        return len(self.v) - 1

    def face(self, ids, out):
        """Adds a face wound so its normal points along `out`."""
        a, b, c = self.v[ids[0]], self.v[ids[1]], self.v[ids[2]]
        if (b - a).cross(c - a).dot(out) < 0:
            ids = ids[::-1]
        self.f.append(tuple(ids))

    def tube(self, points, radii, sides, v_scale=.1):
        rings = []
        for k, p in enumerate(points):
            p = Vector(p)
            ahead = Vector(points[min(k + 1, len(points) - 1)]) - Vector(points[max(k - 1, 0)])
            ahead.normalize()
            side = ahead.cross(Vector((0, 0, 1)) if abs(ahead.z) < .9 else Vector((1, 0, 0))).normalized()
            up = side.cross(ahead)
            ring = []
            for i in range(sides + 1):
                a = 2 * math.pi * i / sides
                d = side * math.cos(a) + up * math.sin(a)
                ring.append(self.vert(p + d * radii[k], (i / sides, k * v_scale), d))
            rings.append(ring)
        for k in range(len(rings) - 1):
            for i in range(sides):
                ids = [rings[k][i], rings[k][i + 1], rings[k + 1][i + 1], rings[k + 1][i]]
                self.face(ids, self.n[ids[0]])

    def ball(self, centre, radius, uv, rows=8, cols=12, squash=1.0):
        centre = Vector(centre)
        grid = []
        for r in range(rows + 1):
            lat = math.pi * (r / rows - .5)
            row = []
            for c in range(cols + 1):
                lon = 2 * math.pi * c / cols
                d = Vector((math.cos(lat) * math.cos(lon), math.cos(lat) * math.sin(lon), math.sin(lat)))
                row.append(self.vert(centre + Vector((d.x, d.y, d.z * squash)) * radius, uv, d))
            grid.append(row)
        for r in range(rows):
            for c in range(cols):
                ids = [grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]]
                if r == 0:
                    ids = ids[1:]
                elif r == rows - 1:
                    ids = ids[:3]
                self.face(ids, self.n[ids[1]])

    def build(self):
        mesh = bpy.data.meshes.new(self.name)
        mesh.from_pydata([tuple(p) for p in self.v], [], self.f)
        layer = mesh.uv_layers.new(name="UVMap")
        for poly in mesh.polygons:
            poly.use_smooth = True
            for li in poly.loop_indices:
                layer.data[li].uv = self.uv[mesh.loops[li].vertex_index]
        if all(n is not None for n in self.n):
            mesh.normals_split_custom_set_from_vertices([tuple(n) for n in self.n])
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def envelope(z):
    """How far the bough tips reach at height z."""
    t = min(1, max(0, (z - 29) / (TOP - 29)))
    return 47.0 * (1 - t) ** .74 + 1.2


def trunk_centre(z):
    t = z / TOP
    return Vector((math.sin(t * 3.5) * 1.3 * t, math.sin(t * 6) * .9 * t, z))


# ---------------------------------------------------------------- trunk
trunk = Part("Fir Trunk")
rings, around = 40, 36
rows = []
for r in range(rings + 1):
    z = TOP * (r / rings) ** 1.35
    base = TRUNK * (1 - .96 * max(0, (z - 30) / (TOP - 30)) ** .8) if z > 30 else TRUNK
    flare = 3.6 * math.exp(-z / 2.2)                      # roots spread on the rug
    row = []
    for i in range(around + 1):
        a = 2 * math.pi * i / around
        ridge = .5 + .5 * math.sin(a * 9 + z * .16 + 1.3 * math.sin(z * .21))
        ridge = ridge ** 1.6
        lobe = .5 + .5 * math.sin(a * 6 + .4)
        radius = base * (.93 + .15 * ridge) + flare * (.45 + .75 * lobe)
        d = Vector((math.cos(a), math.sin(a), 0))
        row.append(trunk.vert(trunk_centre(z) + d * radius, (2 * i / around, z / 18), d))
    rows.append(row)
for r in range(rings):
    for i in range(around):
        ids = [rows[r][i], rows[r][i + 1], rows[r + 1][i + 1], rows[r + 1][i]]
        trunk.face(ids, trunk.n[ids[0]])

# ---------------------------------------------------------------- boughs
shade, light = Part("Fir Boughs Shade"), Part("Fir Boughs Light")
branches = []        # (points along the spine, length, tier fraction)


def spine(root, angle, length, slope, curl):
    axis = Vector((math.cos(angle), math.sin(angle), 0))
    side = Vector((-axis.y, axis.x, 0))
    sway = rand.uniform(-.07, .07)
    pts = []
    for k in range(13):
        s = k / 12
        rise = length * (slope * s + curl * s ** 3 - .05 * math.sin(math.pi * s))
        pts.append(root + axis * (length * s) + side * (length * sway * s * s) + Vector((0, 0, rise)))
    return pts, axis, side


def along(pts, s):
    x = min(max(s, 0), .9999) * (len(pts) - 1)
    k = int(x)
    return pts[k].lerp(pts[k + 1], x - k), (pts[k + 1] - pts[k]).normalized()


def spray(part, stem, heading, up, length, width, droop):
    """One fir spray: a bent, slightly folded card with the stem at v = 0."""
    heading = heading.normalized()
    across = heading.cross(up).normalized()
    up = across.cross(heading).normalized()
    rows_ = []
    for k in range(4):
        v = k / 3
        centre = stem + heading * (length * v) - Vector((0, 0, 1)) * (droop * length * v * v)
        half = width * .5
        row = []
        for j in range(3):
            e = j - 1
            p = centre + across * (e * half) - up * (abs(e) * width * .07)
            out = Vector((p.x, p.y, 0))
            out = out.normalized() if out.length > .01 else Vector((1, 0, 0))
            n = Vector((0, 0, 1)) * .62 + out * .50 + up * .35
            row.append(part.vert(p, (j / 2, v), n))
        rows_.append(row)
    for k in range(3):
        for j in range(2):
            part.face([rows_[k][j], rows_[k][j + 1], rows_[k + 1][j + 1], rows_[k + 1][j]], up)


def bough(root, angle, length, tier, fill=False):
    slope = -.30 + .78 * tier ** 1.25                  # low boughs hang, high boughs reach up
    pts, axis, side = spine(root, angle, length, slope, .13)
    low = min(p.z for p in pts)
    if low < FLOOR_CLEAR + 1.5:                         # lift a bough that would sweep the gifts
        lift = FLOOR_CLEAR + 1.5 - low
        pts = [p + Vector((0, 0, lift * (k / 12))) for k, p in enumerate(pts)]
    branches.append((pts, length, tier))
    girth = max(.12, length * .013)
    trunk.tube(pts, [girth * (1 - .9 * k / 12) + .05 for k in range(13)], 5)
    size = min(13.0, max(5.0, length * .32))
    s = .14 if fill else .22
    flip = rand.choice((-1, 1))
    while s < .93:
        here, tangent = along(pts, s)
        local = size * (1 - .42 * s)
        for sign in (flip, -flip):
            heading = tangent * math.cos(math.radians(52)) + side * (sign * math.sin(math.radians(52)))
            heading.z += rand.uniform(-.10, .08)
            up = Vector((0, 0, 1)) + side * (sign * rand.uniform(-.25, .1)) + axis * rand.uniform(-.1, .1)
            part = light if rand.random() < .16 + .5 * s * s else shade
            spray(part, here, heading, up, local * rand.uniform(.9, 1.15), local * rand.uniform(.85, 1.0),
                  rand.uniform(.06, .16))
            flip = -flip
        # A raised spray gives the bough depth when seen from the side.
        if rand.random() < .7:
            heading = tangent + Vector((0, 0, rand.uniform(.30, .55))) + side * rand.uniform(-.3, .3)
            spray(shade if rand.random() < .6 else light, here, heading, Vector((0, 0, 1)) - tangent * .5,
                  local * .8, local * .72, .10)
        s += .42 * local / length + .02
    tip, tangent = along(pts, .90)
    spray(light, tip, tangent + Vector((0, 0, .06)), Vector((0, 0, 1)), size * .78, size * .66, .05)


whorls = 17
for w in range(whorls):
    t = w / (whorls - 1)
    z = LOW + (TOP - 7 - LOW) * (1 - (1 - t) ** 1.22)
    count = round(16 - 9 * t)
    phase = w * .47
    for i in range(count):
        angle = phase + 2 * math.pi * i / count + rand.uniform(-.12, .12)
        root = trunk_centre(z + rand.uniform(-1.2, 1.2))
        slope = -.30 + .78 * t ** 1.25
        tip_z = root.z + slope * envelope(root.z) * .9
        length = envelope(max(29, min(TOP, tip_z))) * rand.uniform(.90, 1.08)
        bough(root, angle, length, t)
    # Shorter boughs between the whorls keep the trunk from showing through.
    if w < whorls - 2:
        z2 = z + 3.2
        for i in range(max(4, count - 4)):
            angle = phase + .6 + 2 * math.pi * i / max(4, count - 4) + rand.uniform(-.25, .25)
            root = trunk_centre(z2 + rand.uniform(-1.5, 1.5))
            bough(root, angle, envelope(root.z) * rand.uniform(.50, .72), min(1, t + .12), fill=True)

# Leader shoot at the very top.
top = trunk_centre(TOP - 4)
for i in range(5):
    a = i * 2 * math.pi / 5
    spray(light, top, Vector((math.cos(a) * .35, math.sin(a) * .35, 1)), Vector((math.cos(a), math.sin(a), .2)),
          7.5, 5.0, 0)

# ---------------------------------------------------------------- dark core
core = Part("Fir Core")
c_rows = []
for r in range(25):
    z = 37.5 + (TOP - 12 - 37.5) * r / 24
    row = []
    for i in range(25):
        a = 2 * math.pi * i / 24
        lump = 1 + .16 * math.sin(a * 5 + z * .31) + .10 * math.sin(a * 11 - z * .5)
        radius = max(.4, envelope(z) * .34 * lump) * (.45 if r == 0 else 1)
        d = Vector((math.cos(a), math.sin(a), .35)).normalized()
        row.append(core.vert(trunk_centre(z) + Vector((math.cos(a), math.sin(a), 0)) * radius, (i / 24, r / 24), d))
    c_rows.append(row)
for r in range(24):
    for i in range(24):
        ids = [c_rows[r][i], c_rows[r][i + 1], c_rows[r + 1][i + 1], c_rows[r + 1][i]]
        core.face(ids, core.n[ids[0]])

# ---------------------------------------------------------------- baubles
baubles, brass, wire = Part("Fir Baubles"), Part("Fir Brass"), Part("Fir Wire")
placed = []
order = list(range(len(branches)))
rand.shuffle(order)
colour = 0
for index in order:
    pts, length, tier = branches[index]
    if length < 9 or len(placed) >= 64:
        continue
    radius = rand.uniform(2.5, 3.5) * (1 - .25 * tier)
    hang, _ = along(pts, rand.uniform(.80, .93))
    centre = hang - Vector((0, 0, radius + rand.uniform(1.3, 2.2)))
    if centre.z - radius < FLOOR_CLEAR or any((centre - c).length < r + radius + 4.5 for c, r in placed):
        continue
    placed.append((centre, radius))
    baubles.ball(centre, radius, ((colour % 4 + .5) / 4, .5), rows=12, cols=18)
    colour += 1
    cap = centre + Vector((0, 0, radius * .96))
    brass.tube([cap, cap + Vector((0, 0, radius * .26))], [radius * .27, radius * .23], 10)
    brass.tube([cap + Vector((0, 0, radius * .26)), cap + Vector((0, 0, radius * .27))], [radius * .23, .01], 10)
    ring = [cap + Vector((math.cos(a) * radius * .13, 0, radius * .36 + math.sin(a) * radius * .13))
            for a in [k * math.pi / 4 for k in range(9)]]
    brass.tube(ring, [radius * .035] * 9, 4)
    wire.tube([hang, cap + Vector((0, 0, radius * .45))], [.05, .05], 4)

# Star on the leader.
star_c = trunk_centre(TOP + 1.5)
for face_y in (-.9, .9):
    hub = brass.vert(star_c + Vector((0, face_y, 0)), (.5, .5), (0, face_y, 0))
    rim = []
    for k in range(10):
        a = math.pi / 2 + k * math.pi / 5
        r = 6.2 if k % 2 == 0 else 2.6
        rim.append(brass.vert(star_c + Vector((math.cos(a) * r, 0, math.sin(a) * r)), (.5, .5),
                              (math.cos(a) * .6, face_y, math.sin(a) * .6)))
    for k in range(10):
        brass.face([hub, rim[k], rim[(k + 1) % 10]], Vector((0, face_y, 0)))

# ---------------------------------------------------------------- light string
lights = []          # (kind, position, colour index)
turns = 10.5
path = []
steps = 2600
for k in range(steps + 1):
    u = k / steps
    z = 31.5 + (TOP - 9 - 31.5) * (1 - (1 - u) ** 1.55)
    a = turns * 2 * math.pi * u * (1 + .25 * u)
    r = envelope(z) * (.90 + .05 * math.sin(a * 3.1)) + .3
    c = trunk_centre(z)
    path.append(Vector((c.x + math.cos(a) * r, c.y + math.sin(a) * r, z + 1.1 * math.sin(a * 5.3))))
resampled, travelled, next_at = [path[0]], 0.0, 1.6
bulb_at, bulb_index = 3.0, 0
for k in range(1, len(path)):
    step = (path[k] - path[k - 1]).length
    travelled += step
    if travelled >= next_at:
        resampled.append(path[k])
        next_at += 1.6
    if travelled >= bulb_at:
        socket = path[k]
        lights.append(("B", socket - Vector((0, 0, 1.25)), bulb_index % 4))
        wire.tube([socket, socket - Vector((0, 0, .7))], [.26, .30], 5)
        bulb_index += 1
        bulb_at += rand.uniform(5.6, 7.4)
wire.tube(resampled, [.085] * len(resampled), 4)

for _ in range(520):
    z = rand.uniform(31, TOP - 6)
    a = rand.uniform(0, 2 * math.pi)
    r = envelope(z) * rand.uniform(.62, .98)
    c = trunk_centre(z)
    lights.append(("S", Vector((c.x + math.cos(a) * r, c.y + math.sin(a) * r, max(FLOOR_CLEAR, z - r * .08))), 0))

# ---------------------------------------------------------------- export
os.makedirs(out_dir, exist_ok=True)
objects = {}
for part in (trunk, shade, light, core, baubles, brass, wire):
    obj = part.build()
    objects[part.name] = obj
    print("FIR", part.name, "tris", sum(len(f) - 2 for f in part.f), "verts", len(part.v))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.wm.obj_export(filepath=os.path.join(out_dir, part.name + ".obj"), export_selected_objects=True,
                          forward_axis="NEGATIVE_Z", up_axis="Y", export_materials=False, export_uv=True,
                          export_normals=True, export_triangulated_mesh=True)
# Unity reads the OBJ with x mirrored, so a Blender point (x, y, z) lands at (-x, z, -y).
with open(os.path.join(out_dir, "Fir Lights.txt"), "w") as f:
    for kind, p, c in lights:
        f.write("%s %.2f %.2f %.2f %d\n" % (kind, -p.x, p.z, -p.y, c))
print("FIR bulbs", sum(1 for l in lights if l[0] == "B"), "baubles", len(placed))

# ---------------------------------------------------------------- preview
def material(name, colour, rough=.5, metal=0, emit=None, image=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*colour, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = (*colour, 1)
        b.inputs["Emission Strength"].default_value = emit
    if image:
        tex = m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(image)
        mix = m.node_tree.nodes.new("ShaderNodeMixRGB")
        mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 1
        mix.inputs[2].default_value = (*colour, 1)
        m.node_tree.links.new(tex.outputs["Color"], mix.inputs[1])
        m.node_tree.links.new(mix.outputs["Color"], b.inputs["Base Color"])
        m.node_tree.links.new(tex.outputs["Alpha"], b.inputs["Alpha"])
    return m


objects["Fir Trunk"].data.materials.append(material("bark", (.36, .20, .10), .9))
objects["Fir Boughs Shade"].data.materials.append(material("shade", (1.0, 1.25, .9), .8, image=sprig_png))
objects["Fir Boughs Light"].data.materials.append(material("light", (1.5, 1.7, 1.1), .8, image=sprig_png))
objects["Fir Core"].data.materials.append(material("core", (.012, .04, .018), 1))
objects["Fir Baubles"].data.materials.append(material("glass", (.8, .08, .08), .12, .7))
objects["Fir Brass"].data.materials.append(material("brass", (.85, .58, .2), .3, 1))
objects["Fir Wire"].data.materials.append(material("wire", (.03, .06, .03), .6))
bulb = material("bulb", (1, .5, .12), .4, 0, 18)
for kind, p, c in lights:
    if kind == "B":
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=.8, location=p)
        bpy.context.object.data.materials.append(bulb)
bpy.ops.mesh.primitive_plane_add(size=400)
bpy.context.object.data.materials.append(material("rug", (.25, .03, .04), .9))
scene = bpy.context.scene
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(200)))
bpy.context.object.data.energy = 3.2
world = bpy.data.worlds.new("w")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (.10, .12, .28, 1)
scene.world = world
cam_data = bpy.data.cameras.new("c")
cam_data.lens = 50
cam_data.clip_end = 2000
cam = bpy.data.objects.new("c", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = 1000, 1300
for label, eye, look in (("far", (0, 300, 120), (0, 0, 70)), ("under", (0, 70, 4), (0, 0, 60))):
    cam.location = eye
    cam.rotation_euler = (Vector(look) - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 50 if label == "far" else 22
    scene.render.filepath = out_png.replace(".png", "-" + label + ".png")
    bpy.ops.render.render(write_still=True)
