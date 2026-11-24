"""Patch the reviewed Blender scenes and export the meshes used by Unity."""
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

source, output, resources = map(Path, sys.argv[sys.argv.index("--") + 1:])
output.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0


def open_review(name):
    bpy.ops.wm.open_mainfile(filepath=str(source / name))


def save(name):
    bpy.context.scene.frame_set(bpy.context.scene.frame_start)
    bpy.ops.wm.save_as_mainfile(filepath=str(output / (name + ".blend")), compress=True)


def mesh_object(name, vertices, faces):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    for face in mesh.polygons:
        face.use_smooth = True
    item = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(item)
    return item


def write_obj(name, vertices, faces):
    # Write Unity mesh axes. These meshes are symmetric about local X.
    lines = ["o " + name]
    lines.extend("v %.7f %.7f %.7f" % (x, z, y) for x, y, z in vertices)
    lines.append("s 1")
    lines.extend("f " + " ".join(str(i + 1) for i in reversed(face)) for face in faces)
    (resources / (name + ".obj")).write_text("\n".join(lines) + "\n", encoding="ascii")


def cavity():
    # Mouth, lined wall and rounded closed floor behind the cuff.
    section = [(-.242, .106, .088), (-.211, .106, .089), (-.139, .097, .085),
               (-.086, .075, .064), (-.060, .036, .029), (-.054, .0005, .0005)]
    sides = 40
    vertices = [(rx * math.cos(i * math.tau / sides), depth, ry * math.sin(i * math.tau / sides))
                for depth, rx, ry in section for i in range(sides)]
    faces = []
    for j in range(len(section) - 1):
        for i in range(sides):
            k = j * sides + i
            l = j * sides + (i + 1) % sides
            faces.append((k, l, l + sides, k + sides))
    faces.append(tuple(range(len(vertices) - sides, len(vertices))))
    return vertices, faces


open_review("boxing-glove-hit/Boxing Glove contact preview.blend")
vertices, faces = cavity()
liner = mesh_object("Padded wrist cavity", vertices, faces)
liner.parent = bpy.data.objects["Giant Boxing Glove | stock mesh"]
lining = bpy.data.materials.new("Dark oxblood lining")
lining.diffuse_color = (.08, .021, .027, 1)
liner.data.materials.append(lining)
write_obj("BoxingGloveLining", vertices, faces)
save("Boxing Glove - padded cuff")

open_review("single-barrel-polish/round-lollipop/random-reloads/RPG candy color reloads.blend")
stick = bpy.data.objects["Bore-fit white paper stick"]
dimensions = stick.dimensions.copy()
axis = max(range(3), key=lambda i: dimensions[i])
stick.dimensions = Vector(tuple(.584 if index == axis else .045 for index in range(3)))
# The candy is forward along Y. The tip stays in the head during the whole reload.
stick.location.y -= .168
save("RPG - long bore-fit lollipop")

open_review("needle-drill-shot/Needle Drill shot preview.blend")
chuck = bpy.data.objects["Rotating drill chuck surface 7"]
chuck_mesh = chuck.data
chuck_mesh.calc_loop_triangles()
# The machined bit occupies the narrow center forward of the chuck jaws.
bounds = [v.co.y for v in chuck_mesh.vertices]
print("BIT SOURCE DEPTH", min(bounds), max(bounds))
indices = {}
vertices = []
faces = []
for triangle in chuck_mesh.loop_triangles:
    if not all(chuck_mesh.vertices[i].co.y >= .110 for i in triangle.vertices):
        continue
    face = []
    for index in triangle.vertices:
        if index not in indices:
            indices[index] = len(vertices)
            vert = chuck_mesh.vertices[index].co
            vertices.append((vert.x, vert.y - .377, vert.z))
        face.append(indices[index])
    faces.append(face)
assert len(faces) > 40 and max(v[1] for v in vertices) > -.024
write_obj("EjectedDrillBit", vertices, faces)
chuck_edit = bmesh.new()
chuck_edit.from_mesh(chuck_mesh)
launched_faces = [face for face in chuck_edit.faces if all(v.co.y >= .110 for v in face.verts)]
bmesh.ops.delete(chuck_edit, geom=launched_faces, context="FACES")
chuck_edit.to_mesh(chuck_mesh)
chuck_edit.free()
# A short native preview stages the bit travel, rapid replacement and a battery swap.
bit = mesh_object("Ejected bit - authored helical steel", vertices, faces)
bit.data.materials.append(chuck_mesh.materials[0])
bit.parent = bpy.data.objects["Needle Drill | Overhauled cordless tool"]
replacement = bit.copy()
replacement.name = "Indexed spare drill bit"
bpy.context.collection.objects.link(replacement)
for frame, y in ((1, .46), (5, .46), (10, .693), (100, .693)):
    replacement.location = (0, y, .145)
    replacement.keyframe_insert(data_path="location", frame=frame)
battery = bpy.data.objects["Removable battery pack"]
bpy.context.scene.frame_end = 100
for frame, y in ((1, .693), (4, .693), (9, 1.59), (10, -3), (100, -3)):
    bit.location = (0, y, .145)
    bit.keyframe_insert(data_path="location", frame=frame)
for frame, y, z in ((1, 0, 0), (40, 0, 0), (52, -.30, -.045), (60, -.39, -.25),
                    (67, -.30, -.045), (85, 0, 0), (100, 0, 0)):
    battery.location = (0, y, z)
    battery.keyframe_insert(data_path="location", frame=frame)
for name, frame in (("Bit ejection", 4), ("Bit indexed", 9), ("Battery out", 52), ("Battery latched", 85)):
    bpy.context.scene.timeline_markers.new(name, frame=frame)
save("Needle Drill - bit ejection and battery swap")

open_review("dual-spikers-needle-v2/Dual Spikers needle effects.blend")
scene = bpy.context.scene
for side, suffix in enumerate(("", ".001")):
    for i in range(1, 6):
        crystal = bpy.data.objects[f"Crystal {i}{suffix}"]
        home = crystal.location.copy()
        base_scale = crystal.scale.copy()
        departure = 32 + i * 4 + side * 6
        arrival = 70 + i * 4 + side * 8
        for frame, offset, visibility in (
            (1, Vector((0, 0, 0)), 1), (departure, Vector((0, 0, 0)), 1),
            (departure + 3, Vector((.08 if side else -.08, -.025, .18)), .2),
            (departure + 6, Vector((.12 if side else -.12, -.07, .26)), .001),
            (arrival - 1, Vector((0, -.075, .18)), .001),
            (arrival, Vector((0, -.075, .18)), 1),
            (arrival + 8, Vector((0, 0, 0)), 1),
        ):
            crystal.location = home + offset
            crystal.scale = base_scale * visibility
            crystal.keyframe_insert(data_path="location", frame=frame)
            crystal.keyframe_insert(data_path="scale", frame=frame)
scene.frame_end = 112
scene.timeline_markers.new("Crystal cascade", frame=36)
scene.timeline_markers.new("Crystal reseat", frame=74)
save("Dual Spikers - crystal discharge and refill")
source_path = "alien-dark-sight-saddle/Alien Blaster dark sight base.blend"
open_review(source_path)
blaster = bpy.data.objects["Alien Blaster"]
for index in range(5):
    bpy.ops.mesh.primitive_cube_add(size=1)
    segment = bpy.context.object
    segment.name = f"Blue charge segment {index + 1}"
    segment.parent = blaster
    segment.location = (0, .280 + index * .025, .222)
    segment.scale = (.045, .015, .008)
    material = bpy.data.materials.new(segment.name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    for frame, intensity, color in (
        (1, 3, (.025, .44, 1, 1)), (25 + index * 6, 3, (.025, .44, 1, 1)),
        (32 + index * 6, .07, (.025, .44, 1, 1)), (65, 3, (1, .018, .009, 1)),
        (103, 3, (1, .018, .009, 1)), (124, 3, (.025, .44, 1, 1)),
    ):
        shade = shader.inputs["Emission Color"]
        shade.default_value = color
        shade.keyframe_insert("default_value", frame=frame)
        strength = shader.inputs["Emission Strength"]
        strength.default_value = intensity
        strength.keyframe_insert("default_value", frame=frame)
save("Alien Blaster - charge and vent sequence")
