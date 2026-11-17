"""Builds the Underneath the Tree toy set and exports one FBX per toy.
Run: blender -b -P toys.py -- <output folder> [preview.png]
Every toy stands on z = 0 and faces -Y. Material names are a shared paint list that Unity maps
to its own materials, so the colours can be tuned in one place. Generic toys only, no brands."""
import bpy, bmesh, math, sys, os, random
from mathutils import Vector, Matrix

out = sys.argv[sys.argv.index("--") + 1]
preview = sys.argv[sys.argv.index("--") + 2] if len(sys.argv) > sys.argv.index("--") + 2 else None
os.makedirs(out, exist_ok=True)
random.seed(7)
bpy.ops.wm.read_factory_settings(use_empty=True)

PAINT = {
    "Toy Red": (.75, .03, .04), "Toy Blue": (.04, .18, .75), "Toy Navy": (.03, .06, .30),
    "Toy Yellow": (1., .78, .08), "Toy Green": (.05, .55, .12), "Toy Teal": (.0, .55, .55),
    "Toy White": (.92, .92, .9), "Toy Black": (.02, .02, .025), "Toy Gold": (1., .68, .2),
    "Toy Silver": (.75, .77, .8), "Toy Brown": (.35, .18, .08), "Toy Pink": (1., .45, .65),
    "Toy Orange": (1., .4, .05), "Toy Purple": (.35, .1, .6), "Toy Skin": (.95, .75, .6),
    "Toy Cream": (.96, .9, .78), "Toy Plush": (.85, .62, .42), "Toy Frost": (.95, .97, 1.),
    "Toy Gray": (.3, .31, .33), "Toy Wood": (.6, .38, .2), "Toy Glass": (.6, .85, 1.),
}

def paint(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*PAINT[name], 1)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*PAINT[name], 1)
    return m

parts = []

def add(kind, loc, size, mat, rot=(0, 0, 0), bevel=0.0, seg=24):
    ops = bpy.ops.mesh
    if kind == "box": ops.primitive_cube_add(size=1)
    elif kind == "cyl": ops.primitive_cylinder_add(vertices=seg, radius=.5, depth=1)
    elif kind == "ball": ops.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=.5)
    elif kind == "cone": ops.primitive_cone_add(vertices=seg, radius1=.5, radius2=0, depth=1)
    elif kind == "torus": ops.primitive_torus_add(major_radius=.5, minor_radius=.12, major_segments=seg, minor_segments=10)
    o = bpy.context.active_object
    o.scale = size
    o.rotation_euler = [math.radians(a) for a in rot]
    o.location = loc
    o.data.materials.append(paint(mat))
    if bevel > 0:
        m = o.modifiers.new("b", "BEVEL"); m.width = bevel; m.segments = 3; m.limit_method = "ANGLE"
    if kind in ("ball", "cyl", "torus", "cone"):
        bpy.ops.object.shade_smooth()
    parts.append(o)
    return o

def finish(name):
    global parts
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
        bpy.context.view_layer.objects.active = p
        for m in list(p.modifiers):
            bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, name + ".fbx"), use_selection=True,
        axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE",
        bake_space_transform=True, use_mesh_modifiers=True)
    parts = []
    return o

built = []

# --- Toy house: walls, pitched roof, door, windows, chimney.
add("box", (0, 0, 3), (8, 7, 6), "Toy Cream", bevel=.15)
for s in (-1, 1):
    add("box", (s * 2.25, 0, 8.25), (5.6, 7.8, .5), "Toy Red", rot=(0, s * 38, 0), bevel=.1)
add("box", (0, -3.55, 1.7), (1.8, .2, 3.2), "Toy Brown", bevel=.06)
for x in (-2.6, 2.6):
    add("box", (x, -3.55, 4), (1.6, .2, 1.6), "Toy Glass", bevel=.05)
add("box", (2.4, 1.5, 9.4), (1.1, 1.1, 2.6), "Toy Brown", bevel=.06)
built.append(finish("Toy House"))

# --- Superhero figure: caped hero on a round stand.
add("cyl", (0, 0, .25), (3.2, 3.2, .5), "Toy Gray")
for x in (-.55, .55):
    add("cyl", (x, 0, 2.0), (.9, .9, 3.0), "Toy Blue")
add("cyl", (0, 0, 4.6), (2.3, 1.6, 2.6), "Toy Red")
add("box", (0, .55, 3.9), (2.6, .15, 4.2), "Toy Yellow", rot=(8, 0, 0), bevel=.05)
for x in (-1.45, 1.45):
    add("cyl", (x, 0, 4.5), (.6, .6, 2.2), "Toy Red")
add("ball", (0, 0, 6.6), (1.6, 1.6, 1.7), "Toy Skin")
add("box", (0, -.75, 6.7), (1.4, .2, .35), "Toy Black")
add("box", (0, -.82, 4.9), (1.0, .1, 1.0), "Toy Yellow", rot=(0, 45, 0))
built.append(finish("Toy Superhero"))

# --- Interlocking bricks: a little stepped stack.
def brick(x, y, z, w, d, mat):
    add("box", (x, y, z + .9), (w, d, 1.8), mat, bevel=.06)
    for i in range(int(w / 1.6)):
        for j in range(int(d / 1.6)):
            add("cyl", (x - w / 2 + .8 + i * 1.6, y - d / 2 + .8 + j * 1.6, z + 2.0), (.9, .9, .4), mat)
brick(0, 0, 0, 6.4, 3.2, "Toy Red")
brick(.8, 0, 1.8, 4.8, 3.2, "Toy Blue")
brick(-.8, 0, 3.6, 3.2, 3.2, "Toy Yellow")
brick(2.4, 0, 3.6, 1.6, 3.2, "Toy Green")
built.append(finish("Toy Brick Stack"))

# --- Football: white ball with dark patches.
add("ball", (0, 0, 3), (6, 6, 6), "Toy White", seg=32)
for i in range(12):
    phi = math.acos(1 - 2 * (i + .5) / 12); th = math.pi * (1 + 5 ** .5) * i
    d = Vector((math.sin(phi) * math.cos(th), math.sin(phi) * math.sin(th), math.cos(phi)))
    p = add("cyl", tuple(Vector((0, 0, 3)) + d * 2.93), (1.5, 1.5, .1), "Toy Black", seg=5)
    p.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
built.append(finish("Toy Football"))

# --- Robot: boxy body, antenna, claw arms.
for x in (-.9, .9):
    add("box", (x, 0, 1.4), (1.2, 1.6, 2.8), "Toy Silver", bevel=.1)
add("box", (0, 0, 4.6), (4, 2.6, 3.6), "Toy Silver", bevel=.2)
add("box", (0, -1.32, 4.8), (2.4, .1, 1.6), "Toy Teal", bevel=.05)
for x in (-.6, 0, .6):
    add("cyl", (x, -1.4, 4.8), (.35, .35, .1), "Toy Red", rot=(90, 0, 0))
for s in (-1, 1):
    add("cyl", (s * 2.5, 0, 4.4), (.7, .7, 2.6), "Toy Gray")
    add("box", (s * 2.5, -.2, 2.9), (.9, .5, .7), "Toy Red", bevel=.05)
add("box", (0, 0, 7.6), (2.8, 2.2, 2.2), "Toy Silver", bevel=.2)
for x in (-.6, .6):
    add("ball", (x, -1.12, 7.8), (.6, .3, .6), "Toy Yellow")
add("cyl", (0, 0, 9.2), (.15, .15, 1.2), "Toy Gray")
add("ball", (0, 0, 9.9), (.5, .5, .5), "Toy Red")
built.append(finish("Toy Robot"))

# --- Round plush bear: soft squashed body, ears, belly, little face.
add("ball", (0, 0, 3.6), (7.2, 6.6, 7.0), "Toy Plush", seg=32)
for x in (-2.2, 2.2):
    add("ball", (x, 0, 7.0), (1.8, 1.0, 1.8), "Toy Plush")
    add("ball", (x * 1.15, -1.4, .9), (2.0, 2.2, 1.6), "Toy Plush")
add("ball", (0, -2.9, 3.0), (4.0, 1.0, 3.6), "Toy Cream")
add("ball", (0, -3.2, 4.7), (1.5, .6, 1.0), "Toy Cream")
add("ball", (0, -3.5, 4.9), (.5, .3, .35), "Toy Black")
for x in (-1.2, 1.2):
    add("ball", (x, -3.1, 5.6), (.45, .3, .45), "Toy Black")
    add("ball", (x * 1.6, -3.0, 4.6), (.8, .2, .5), "Toy Pink")
built.append(finish("Toy Plush Bear"))

# --- Retro speaker: wood cabinet, two cones, grille trim.
add("box", (0, 0, 5), (6, 4.4, 10), "Toy Wood", bevel=.25)
for z, r in ((3.1, 3.8), (7.3, 2.4)):
    add("cyl", (0, -2.22, z), (r, r, .2), "Toy Black", rot=(90, 0, 0), seg=32)
    add("cyl", (0, -2.3, z), (r * .45, r * .45, .3), "Toy Gray", rot=(90, 0, 0), seg=32)
    add("torus", (0, -2.3, z), (r * 1.02, r * 1.02, 1), "Toy Silver", rot=(90, 0, 0), seg=32)
add("box", (0, -2.25, 9.3), (4.6, .2, .5), "Toy Gold", bevel=.05)
built.append(finish("Toy Retro Speaker"))

# --- Frosted pinecone: stacked scale rings, white tips.
for i in range(9):
    t = i / 8
    r = 4.2 * math.sin(math.pi * (.15 + .8 * t)) + .4
    for k in range(10):
        a = k / 10 * math.tau + i * .3
        p = add("cone", (math.cos(a) * r * .38, math.sin(a) * r * .38, .6 + i * .75), (r * .32, r * .2, 1.0),
                "Toy Brown" if i < 6 or k % 2 else "Toy Frost")
        p.rotation_euler = (math.radians(70), 0, a + math.pi / 2)
add("ball", (0, 0, 7.1), (1.1, 1.1, 1.4), "Toy Frost")
built.append(finish("Toy Frosted Pinecone"))

# --- Stocking lying down: leg, foot, fluffy cuff, stripes.
add("cyl", (0, 0, 1.2), (2.4, 2.4, 8), "Toy Red", rot=(90, 0, 0), seg=24)
add("ball", (1.2, 4.0, 1.2), (4.6, 3.0, 2.4), "Toy Red")
for y in (-1.8, .4, 2.4):
    add("cyl", (0, y, 1.2), (2.48, 2.48, .5), "Toy White", rot=(90, 0, 0))
add("cyl", (0, -4.4, 1.4), (2.9, 2.9, 1.6), "Toy Frost", rot=(90, 0, 0), seg=24)
built.append(finish("Toy Stocking"))

# --- Puzzle cube: 27 coloured cubies.
faces = ["Toy Red", "Toy Orange", "Toy Blue", "Toy Green", "Toy White", "Toy Yellow"]
for i in range(3):
    for j in range(3):
        for k in range(3):
            add("box", (-1.7 + i * 1.7, -1.7 + j * 1.7, .85 + k * 1.7), (1.62, 1.62, 1.62), "Toy Black", bevel=.12)
for f, (axis, sign) in enumerate([(0, 1), (0, -1), (1, 1), (1, -1), (2, 1), (2, -1)]):
    for i in range(3):
        for j in range(3):
            p = [0, 0, 0]
            p[axis] = sign * 2.56
            other = [a for a in range(3) if a != axis]
            p[other[0]] = -1.7 + i * 1.7; p[other[1]] = -1.7 + j * 1.7
            p[2] += 2.55
            s = [1.3, 1.3, 1.3]; s[axis] = .06
            add("box", tuple(p), tuple(s), faces[f], bevel=.05)
built.append(finish("Toy Puzzle Cube"))

# --- Toy race car: low body, cockpit, chunky wheels, flame stripe.
add("box", (0, 0, 1.3), (3.6, 8, 1.4), "Toy Orange", bevel=.4)
add("box", (0, .6, 2.3), (2.8, 3.2, 1.2), "Toy Glass", bevel=.35)
add("box", (0, 3.6, 2.1), (3.8, .5, .3), "Toy Black", bevel=.05)
for x in (-1.9, 1.9):
    for y in (-2.6, 2.6):
        add("cyl", (x, y, .9), (1.8, 1.8, .9), "Toy Black", rot=(0, 90, 0))
        add("cyl", (x * 1.06, y, .9), (.9, .9, .9), "Toy Silver", rot=(0, 90, 0))
add("box", (0, -2.5, 2.05), (1.0, 2.5, .05), "Toy Yellow")
built.append(finish("Toy Race Car"))

# --- Fashion doll: slim figure, long hair, dress, stand.
add("cyl", (0, 0, .2), (2.4, 2.4, .4), "Toy Pink")
for x in (-.35, .35):
    add("cyl", (x, 0, 2.6), (.5, .5, 4.4), "Toy Skin")
add("cone", (0, 0, 5.4), (3.0, 2.6, 3.4), "Toy Pink")
add("cyl", (0, 0, 7.5), (1.2, .9, 2.0), "Toy Pink")
for x in (-.85, .85):
    add("cyl", (x, 0, 7.2), (.32, .32, 2.8), "Toy Skin")
add("ball", (0, 0, 9.4), (1.3, 1.3, 1.5), "Toy Skin")
add("ball", (0, .25, 9.6), (1.5, 1.4, 1.7), "Toy Yellow")
add("box", (0, .6, 8.2), (1.5, .5, 2.8), "Toy Yellow", bevel=.2)
built.append(finish("Toy Fashion Doll"))

# --- Nutcracker: tall hat, beard, big jaw, jacket, boots.
for x in (-.7, .7):
    add("cyl", (x, 0, 1.0), (1.2, 1.4, 2.0), "Toy Black")
    add("cyl", (x, 0, 3.6), (1.0, 1.0, 3.2), "Toy White")
add("cyl", (0, 0, 7.4), (3.2, 2.6, 4.6), "Toy Red")
add("cyl", (0, 0, 6.0), (3.3, 2.7, .5), "Toy Gold")
for x in (-1.9, 1.9):
    add("cyl", (x, 0, 7.4), (.8, .8, 3.8), "Toy Red")
add("cyl", (0, 0, 10.6), (2.4, 2.4, 2.0), "Toy Skin")
add("box", (0, -1.1, 9.8), (1.8, .6, .9), "Toy White", bevel=.2)
add("cyl", (0, 0, 13.0), (2.6, 2.6, 2.8), "Toy Black")
add("cyl", (0, 0, 11.8), (2.7, 2.7, .3), "Toy Gold")
add("ball", (0, 0, 14.6), (.7, .7, .7), "Toy Gold")
built.append(finish("Toy Nutcracker"))

# --- Toy blaster: bright foam-dart style, chunky and obviously a toy.
add("box", (0, 0, 2.6), (1.6, 6.5, 2.0), "Toy Orange", bevel=.3)
add("cyl", (0, -4.0, 2.9), (1.1, 1.1, 2.4), "Toy Yellow", rot=(90, 0, 0))
add("cyl", (0, 1.2, 2.6), (2.0, 2.0, 1.8), "Toy Blue", rot=(0, 90, 0))
add("box", (0, 2.6, 1.2), (1.4, 1.6, 2.6), "Toy Blue", rot=(-18, 0, 0), bevel=.25)
add("box", (0, 3.9, 3.0), (1.3, 1.6, .9), "Toy Yellow", bevel=.2)
add("box", (0, .6, .55), (1.4, 5, 1.1), "Toy Orange", bevel=.2)
built.append(finish("Toy Blaster"))

# --- Retro games console: grey box, cartridge, two pads with cords.
add("box", (0, 0, 1.2), (8, 6, 2.4), "Toy Gray", bevel=.2)
add("box", (0, -1.2, 2.45), (6.8, 2.6, .1), "Toy Black")
add("box", (0, 1.4, 3.0), (3.0, .6, 2.0), "Toy Red", bevel=.1)
for x in (-2.8, -1.8):
    add("box", (x, -2.9, 1.4), (.6, .2, .4), "Toy Red", bevel=.05)
for x in (-3.0, 3.0):
    add("box", (x, -6.0, .4), (2.8, 1.6, .8), "Toy Gray", bevel=.25)
    add("box", (x - .7, -6.1, .85), (.7, .25, .1), "Toy Black")
    add("box", (x - .7, -6.1, .85), (.25, .7, .1), "Toy Black")
    for dx in (.5, 1.0):
        add("cyl", (x + dx, -6.1, .85), (.4, .4, .12), "Toy Red")
    add("cyl", (x * .7, -4.3, .2), (.2, .2, 2.6), "Toy Black", rot=(90, 0, 20 if x < 0 else -20))
built.append(finish("Toy Retro Console"))

# --- Candy bag: gathered bag with a ribbon and sweets peeking out.
add("cyl", (0, 0, 2.8), (5.4, 4.6, 5.6), "Toy Glass", seg=24)
add("cone", (0, 0, 6.3), (4.6, 3.8, 1.6), "Toy Glass")
add("torus", (0, 0, 6.0), (2.0, 1.6, 2.0), "Toy Red")
add("ball", (0, 0, 7.3), (1.6, 1.6, 1.0), "Toy Red")
for i in range(18):
    a = random.random() * math.tau; r = random.random() * 1.9
    add("ball", (math.cos(a) * r, math.sin(a) * r * .85, .8 + random.random() * 4.2), (1.1, 1.1, .8),
        random.choice(["Toy Red", "Toy Green", "Toy Yellow", "Toy Pink", "Toy Purple", "Toy Orange"]))
built.append(finish("Toy Candy Bag"))

# --- Rag doll with curly red hair and a red dress.
add("cone", (0, 0, 2.8), (3.6, 3.2, 4.0), "Toy Red")
add("cyl", (0, 0, 1.0), (3.65, 3.25, .3), "Toy White")
for x in (-.5, .5):
    add("cyl", (x, -.2, .45), (.6, .9, .9), "Toy Black")
for x in (-1.0, 1.0):
    add("cyl", (x, 0, 4.0), (.45, .45, 2.2), "Toy Skin", rot=(0, x * 25, 0))
add("ball", (0, 0, 5.8), (2.0, 2.0, 2.1), "Toy Skin")
for i in range(16):
    a = i / 16 * math.tau
    add("ball", (math.cos(a) * 1.05, math.sin(a) * 1.05 + .25, 6.0 + math.sin(i * 1.7) * .5), (1.0, 1.0, 1.0), "Toy Orange")
add("ball", (0, .2, 6.85), (2.0, 1.9, 1.0), "Toy Orange")
for x in (-.4, .4):
    add("ball", (x, -.95, 5.9), (.25, .15, .25), "Toy Black")
built.append(finish("Toy Rag Doll"))

# --- Property board game box: flat box, coloured lid band, stripe and corner badge.
add("box", (0, 0, 1.0), (12, 6, 2.0), "Toy White", bevel=.08)
add("box", (0, 0, 2.02), (11.6, 5.6, .06), "Toy Red")
add("box", (0, 0, 2.06), (8.0, 2.0, .04), "Toy White")
for i, c in enumerate(["Toy Green", "Toy Blue", "Toy Yellow", "Toy Orange", "Toy Pink", "Toy Purple"]):
    add("box", (-4.5 + i * 1.8, 2.3, 2.07), (1.6, .6, .04), c)
add("cyl", (4.6, -2.0, 2.07), (1.4, 1.4, .05), "Toy Gold")
built.append(finish("Toy Board Game"))

# --- Modelling clay tub: tub and bright lid with a swirl of clay on top.
add("cyl", (0, 0, 1.8), (4.0, 4.0, 3.6), "Toy Yellow", seg=32)
add("cyl", (0, 0, 3.8), (4.3, 4.3, .8), "Toy Blue", seg=32)
add("torus", (0, 0, 4.4), (2.2, 2.2, 2.6), "Toy Pink", seg=24)
add("ball", (0, 0, 4.5), (1.4, 1.4, .9), "Toy Green")
built.append(finish("Toy Clay Tub"))

# --- Toaster pastry box: tall carton with a frosted pastry on the front.
add("box", (0, 0, 4), (6, 2.2, 8), "Toy Blue", bevel=.06)
add("box", (0, -1.12, 4.4), (3.6, .1, 4.4), "Toy Cream", bevel=.3)
add("box", (0, -1.18, 4.4), (2.9, .08, 3.6), "Toy Pink", bevel=.3)
for i in range(8):
    add("box", (random.uniform(-1.1, 1.1), -1.24, random.uniform(3.0, 5.8)), (.5, .04, .15),
        random.choice(["Toy Yellow", "Toy Red", "Toy Teal"]), rot=(0, random.uniform(0, 180), 0))
add("box", (0, -1.12, 7.2), (5.2, .1, 1.0), "Toy Yellow", bevel=.05)
built.append(finish("Toy Pastry Box"))

# --- Wind-up toy: tin walker with a big key in its back.
add("ball", (0, 0, 2.8), (3.6, 3.6, 3.8), "Toy Teal")
add("cyl", (0, 0, 2.8), (3.7, 3.7, .4), "Toy Silver")
for x in (-.8, .8):
    add("box", (x, -.3, .55), (.8, 1.6, 1.1), "Toy Orange", bevel=.15)
for x in (-.65, .65):
    add("ball", (x, -1.65, 3.6), (.8, .4, .8), "Toy White")
    add("ball", (x, -1.85, 3.6), (.35, .2, .35), "Toy Black")
add("cyl", (0, 2.1, 2.9), (.3, .3, 1.2), "Toy Silver", rot=(90, 0, 0))
for x in (-.8, .8):
    add("ball", (x, 2.75, 2.9), (1.3, .25, 1.0), "Toy Gold")
built.append(finish("Toy Wind-up Walker"))

print("EXPORTED", len(built))

if preview:
    for o in bpy.context.scene.objects:
        o.hide_render = True
    cols = 5
    for i, o in enumerate(built):
        o.hide_render = False
        o.location = ((i % cols) * 18, -(i // cols) * 16 + 30, 0)
    bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(30)))
    bpy.context.active_object.data.energy = 3
    bpy.ops.object.camera_add(location=(36, -110, 75), rotation=(math.radians(55), 0, 0))
    cam = bpy.context.active_object
    cam.data.lens = 32
    bpy.context.scene.camera = cam
    w = bpy.context.scene.world or bpy.data.worlds.new("w")
    bpy.context.scene.world = w
    w.color = (.15, .15, .2)
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            bpy.context.scene.render.engine = engine
            break
        except TypeError:
            pass
    bpy.context.scene.render.resolution_x = 1600
    bpy.context.scene.render.resolution_y = 1000
    bpy.context.scene.render.filepath = preview
    bpy.ops.render.render(write_still=True)
