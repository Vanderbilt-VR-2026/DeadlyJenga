"""Blender lab: grey-box the layout and render it from the approved picture's camera.

Run:  blender -b -P render.py -- <layout.json> <out_dir> [cam.json]
Course frame (x right, z back, y up) maps to Blender (x, y=z, z=y).
"""
import bpy, json, math, os, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
data = json.load(open(argv[0]))
out_dir = argv[1]
cam_cfg = dict(pos=[0, -112, 52], look=[0, 2, 4], fov=52.0)
if len(argv) > 2:
    cam_cfg.update(json.load(open(argv[2])))

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
mats = {}


def mat(name, rgb, rough=.5, metal=0.0, emit=0.0):
    if name in mats:
        return mats[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = (*rgb, 1)
        b.inputs["Emission Strength"].default_value = emit
    mats[name] = m
    return m


def box(name, x, y, z, sx, sy, sz, yaw, material):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z), rotation=(0, 0, yaw))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(material)
    return o


R = data["rug_radius"]
RZ = data.get("rug_z", 0.0)
# floor, rug, border
bpy.ops.mesh.primitive_plane_add(size=700, location=(0, 0, -.5))
bpy.context.object.data.materials.append(mat("floor", (.10, .045, .03), .25))
bpy.ops.mesh.primitive_cylinder_add(radius=R, depth=.5, vertices=96, location=(0, RZ, -.25))
bpy.context.object.data.materials.append(mat("rug", (.45, .02, .05), .9))
bpy.ops.mesh.primitive_torus_add(major_radius=R + 1.2, minor_radius=1.6, major_segments=96,
                                 minor_segments=10, location=(0, RZ, -.1))
bpy.context.object.data.materials.append(mat("border", (.85, .78, .68), .9))

# room proxies (picture: sofa back left, lamp table, tree back right, window on right wall)
for p in data.get("props", []):
    o = box(p["name"], p["x"], p["z"], p["y"] + p["h"] / 2, p["w"], p["d"], p["h"],
            -math.radians(p.get("turn", 0)), mat("prop " + p["name"], p["rgb"], .7))
    if p.get("cone"):
        bpy.data.objects.remove(o)
        bpy.ops.mesh.primitive_cone_add(radius1=p["w"] / 2, radius2=1, depth=p["h"],
                                        location=(p["x"], p["z"], p["y"] + p["h"] / 2))
        bpy.context.object.data.materials.append(mat("prop " + p["name"], p["rgb"], .7))

colors = data["colors"]
for g in data["gifts"]:
    yaw = -math.radians(g["turn"])
    wrap = mat("wrap " + g["color"], colors[g["color"]], .45)
    rib = mat("ribbon " + g["ribbon"], colors[g["ribbon"]], .3, .6)
    cx, cy, cz = g["x"], g["z"], g["y"] + g["h"] / 2
    if g.get("shape") == "round":
        bpy.ops.mesh.primitive_cylinder_add(radius=g["w"] / 2, depth=g["h"], vertices=40, location=(cx, cy, cz))
        bpy.context.object.data.materials.append(wrap)
    else:
        box(g["name"], cx, cy, cz, g["w"], g["d"], g["h"], yaw, wrap)
    rw = min(g["w"], g["d"]) * .14
    if g.get("shape") != "round":
        box("r1", cx, cy, cz, g["w"] + .08, rw, g["h"] + .08, yaw, rib)
        box("r2", cx, cy, cz, rw, g["d"] + .08, g["h"] + .08, yaw, rib)
    if g.get("bow"):
        s = min(g["w"], g["d"]) * .32
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1, location=(cx, cy, g["y"] + g["h"] + s * .35), segments=12, ring_count=6)
        bpy.context.object.scale = (s, s * .7, s * .45)
        bpy.context.object.rotation_euler = (0, 0, yaw)
        bpy.context.object.data.materials.append(rib)

# rails
brass = mat("brass", (.85, .58, .22), .25, .9)
tie = mat("tie", (.16, .07, .03), .6)
tr = data["track"]
H = data["track_height"]
for i, (u, v) in enumerate(tr):
    u2, v2 = tr[(i + 1) % len(tr)]
    yaw = math.atan2(v2 - v, u2 - u)
    ln = math.hypot(u2 - u, v2 - v)
    mx, my = (u + u2) / 2, (v + v2) / 2
    nx, ny = -math.sin(yaw), math.cos(yaw)
    box("tie", mx, my, H - .25, .43, 3.7, .2, yaw, tie)
    for s in (-1.32, 1.32):
        box("rail", mx + nx * s, my + ny * s, H - .14, ln + .05, .19, .28, yaw, brass)

# light + world
bpy.ops.object.light_add(type="SUN", location=(0, 0, 100), rotation=(math.radians(35), 0, math.radians(25)))
bpy.context.object.data.energy = 2.2
world = bpy.data.worlds.new("w")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (.10, .13, .30, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 1.6
scene.world = world


def camera(name, pos, look, fov, ortho=None):
    cd = bpy.data.cameras.new(name)
    co = bpy.data.objects.new(name, cd)
    scene.collection.objects.link(co)
    co.location = pos
    direction = Vector(look) - Vector(pos)
    co.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cd.sensor_fit = "HORIZONTAL"
    cd.angle = math.radians(fov)
    cd.clip_end = 2000
    if ortho:
        cd.type = "ORTHO"
        cd.ortho_scale = ortho
    return co


scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = 1600, 900
scene.view_settings.view_transform = "Standard"

views = {"ref": camera("ref", cam_cfg["pos"], cam_cfg["look"], cam_cfg["fov"]),
         "top": camera("top", (0, RZ, 300), (0, RZ + 0.01, 0), 30, ortho=2.4 * R * 16 / 9)}
want = argv[3].split(",") if len(argv) > 3 else list(views)
for name in want:
    scene.camera = views[name]
    scene.render.filepath = os.path.join(out_dir, name + ".png")
    bpy.ops.render.render(write_still=True)
print("RENDERED", want)
