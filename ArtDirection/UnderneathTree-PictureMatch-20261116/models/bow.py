"""Gift bow model: two puffy loops, two smaller back loops, two notched tails and a knot.

Run:  blender -b -P bow.py -- <out.obj> <preview.png>
Blender frame: z up, the bow sits on z = 0, loops run along x, tails fall toward -y.
"""
import bpy, bmesh, math, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
out_obj, out_png = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)

verts, faces, uvs = [], [], []


def grid(rows, cols, point):
    """Adds a rows x cols sheet; point(i, j) returns (position, uv)."""
    first = len(verts)
    for i in range(rows + 1):
        for j in range(cols + 1):
            p, uv = point(i / rows, j / cols)
            verts.append(p)
            uvs.append(uv)
    for i in range(rows):
        for j in range(cols):
            a = first + i * (cols + 1) + j
            faces.append((a, a + 1, a + cols + 2, a + cols + 1))


def loop(side, length, height, z0, half_knot, half_tip, lean, yaw, droop=.15):
    centre = Vector((length * .5, 0, z0))
    c, s = math.cos(yaw), math.sin(yaw)

    def point(t, u):
        a = 2 * math.pi * t
        x = length * (1 - math.cos(a)) / 2
        z = z0 + height * math.sin(a) * (.35 + .65 * (1 - math.cos(a)) / 2)
        z -= droop * (x / length) ** 2
        e = u * 2 - 1
        swell = math.sin(math.pi * t)
        half = half_knot + (half_tip - half_knot) * swell ** .7
        out = Vector((x, 0, z)) - centre
        out.normalize()
        puff = .13 * swell * (1 - e * e)
        p = Vector((x, e * half + lean * swell, z)) + out * puff
        # A gentle ruffle along each edge keeps the satin from looking moulded.
        p.z += .035 * e * math.sin(a * 2.5) * swell
        p = Vector((side * (c * p.x - s * p.y), s * p.x + c * p.y, p.z))
        return p, (t, u)
    grid(36, 6, point)


def tail(side):
    def point(t, u):
        e = u * 2 - 1
        t2 = t * (1 - .16 * (1 - abs(e)))          # V notch at the end
        cx = .06 + 1.0 * t2 + .10 * math.sin(t2 * 5)
        cy = -.08 - 1.25 * t2
        dx, dy = 1.0 + .5 * math.cos(t2 * 5), -1.25
        n = math.hypot(dx, dy)
        half = .15 + .12 * t2
        x = cx + e * half * (-dy / n)
        y = cy + e * half * (dx / n)
        z = .34 * (1 - t2) ** 2.2 + .035 + .07 * math.sin(t2 * 9) * (1 - t2) * t2 * 3
        z += .05 * (1 - e * e) + .03 * e * math.sin(t2 * 7)
        return Vector((side * x, y, max(z, .03))), (t, u)
    grid(22, 4, point)


def knot():
    def point(t, u):
        lat, lon = math.pi * (t - .5), 2 * math.pi * u
        r = 1 + .06 * math.cos(lon * 2)
        p = Vector((.15 * r * math.cos(lat) * math.cos(lon), .21 * math.cos(lat) * math.sin(lon),
                    .47 + .19 * math.sin(lat)))
        return p, (u, t)
    grid(10, 14, point)


for side in (-1, 1):
    loop(side, 1.30, .56, .50, .15, .56, .10, math.radians(8))           # main loops
    loop(side, .92, .40, .66, .14, .36, -.26, math.radians(-30), .02)    # smaller loops behind
    tail(side)
knot()

mesh = bpy.data.meshes.new("Gift Bow")
mesh.from_pydata([tuple(v) for v in verts], [], faces)
layer = mesh.uv_layers.new(name="UVMap")
for poly in mesh.polygons:
    for li in poly.loop_indices:
        layer.data[li].uv = uvs[mesh.loops[li].vertex_index]
for poly in mesh.polygons:
    poly.use_smooth = True
obj = bpy.data.objects.new("Gift Bow", mesh)
bpy.context.scene.collection.objects.link(obj)
bm = bmesh.new()
bm.from_mesh(mesh)
bmesh.ops.triangulate(bm, faces=bm.faces)
bm.to_mesh(mesh)
bm.free()
print("BOW tris", len(mesh.polygons), "verts", len(mesh.vertices))

bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.wm.obj_export(filepath=out_obj, export_selected_objects=True, forward_axis="NEGATIVE_Z", up_axis="Y",
                      export_materials=False, export_uv=True, export_normals=True, export_triangulated_mesh=True)

# preview
m = bpy.data.materials.new("satin")
m.use_nodes = True
b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
b.inputs["Base Color"].default_value = (.95, .62, .16, 1)
b.inputs["Roughness"].default_value = .28
b.inputs["Metallic"].default_value = .55
mesh.materials.append(m)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, -1.5))
box = bpy.context.object
box.scale = (4.2, 4.2, 3)
bm2 = bpy.data.materials.new("paper")
bm2.use_nodes = True
next(n for n in bm2.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (.03, .10, .5, 1)
box.data.materials.append(bm2)
scene = bpy.context.scene
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(40), 0, math.radians(30)))
bpy.context.object.data.energy = 3
world = bpy.data.worlds.new("w")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (.35, .35, .45, 1)
scene.world = world
cd = bpy.data.cameras.new("c")
co = bpy.data.objects.new("c", cd)
scene.collection.objects.link(co)
co.location = (2.6, -5.0, 3.4)
co.rotation_euler = (Vector((0, 0, .3)) - co.location).to_track_quat("-Z", "Y").to_euler()
scene.camera = co
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = 900, 700
scene.render.filepath = out_png
bpy.ops.render.render(write_still=True)
