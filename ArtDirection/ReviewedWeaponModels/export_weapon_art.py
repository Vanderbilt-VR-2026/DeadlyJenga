import collections
import struct
import sys
from pathlib import Path

import bpy

mode, destination = sys.argv[sys.argv.index("--") + 1:]
scene = bpy.context.scene
scene.frame_set(scene.frame_start)
bpy.context.view_layer.update()
if mode == "launcher":
    root = bpy.data.objects["Forged graphite trigger"].parent
else:
    root = next(obj.parent for obj in scene.objects
                if obj.name.startswith("Continuous rounded receiver") and not obj.hide_render)
matrix = root.matrix_world.inverted()
if mode == "launcher":
    for obj in root.children_recursive:
        if obj.type != "MESH" or obj.hide_render or len(obj.data.polygons) < 1200:
            continue
        modifier = obj.modifiers.new("Runtime polygon budget", "DECIMATE")
        modifier.ratio = 0.40
    bpy.context.view_layer.update()
graph = bpy.context.evaluated_depsgraph_get()
parts = collections.defaultdict(lambda: ({}, []))

def part_of(obj):
    name = obj.name
    if mode == "launcher":
        if name.startswith("Charge cell "):
            return 1 + int(name.split()[2].split(".")[0])
        return int(name.startswith("Drum end shield"))
    ancestor = obj
    while ancestor and ancestor != root:
        name = ancestor.name
        if name.startswith(("Extended lime power cell", "Brass cell collar")):
            return 1
        if name.startswith("Crystal "):
            return 1 + int(name.split()[1].split(".")[0])
        ancestor = ancestor.parent
    return 0

def surface(material):
    node = next((node for node in material.node_tree.nodes
                 if node.type == "BSDF_PRINCIPLED"), None) if material.node_tree else None
    if not node:
        return 0, (*material.diffuse_color[:3], 1), (0, .65, 0, 0)
    values = node.inputs
    color = tuple(values["Base Color"].default_value)
    alpha = values["Alpha"].default_value
    metal = values["Metallic"].default_value
    smoothness = 1 - values["Roughness"].default_value
    strength = values["Emission Strength"].default_value
    if "prismatic power cell" in material.name.lower():
        return 0, (.24, .46, .89, 1), (.12, .92, .72, 0)
    if "charged crystal" in material.name.lower():
        return 0, (*color[:3], 1), (.15, .91, .85, 0)
    layer = int(alpha < .96 or "containment glass" in material.name.lower())
    if layer and alpha >= .96:
        alpha = .22
    return layer, (*color[:3], alpha), (metal, smoothness, min(2, strength), 0)

def xyz(v):
    return (v.x, v.z, v.y)

for obj in sorted(root.children_recursive, key=lambda item: item.name):
    if obj.type not in {"MESH", "FONT"} or obj.hide_render:
        continue
    model = obj.evaluated_get(graph)
    mesh = model.to_mesh()
    mesh.calc_loop_triangles()
    transform = matrix @ obj.matrix_world
    normal_transform = transform.to_3x3().inverted_safe().transposed()
    part = part_of(obj)
    for triangle in mesh.loop_triangles:
        material = mesh.materials[triangle.material_index]
        layer, color, finish = surface(material)
        vertices, triangles = parts[(part, layer)]
        for loop_index in reversed(triangle.loops):
            point = transform @ mesh.vertices[mesh.loops[loop_index].vertex_index].co
            normal = (normal_transform @ mesh.corner_normals[loop_index].vector).normalized()
            key = tuple(round(x, 6) for x in (*xyz(point), *xyz(normal), *color, *finish))
            triangles.append(vertices.setdefault(key, len(vertices)))
    model.to_mesh_clear()

assert {part for part, _ in parts} == (set(range(8)) if mode == "launcher" else set(range(7)))
with Path(destination).open("wb") as output:
    output.write(struct.pack("<4sI", b"WART", len(parts)))
    for (part, layer), (vertices, triangles) in sorted(parts.items()):
        print(mode, part, layer, len(vertices), len(triangles) // 3, flush=True)
        output.write(struct.pack("<BBII", part, layer, len(vertices), len(triangles)))
        for vertex in vertices:
            output.write(struct.pack("<14f", *vertex))
        output.write(struct.pack(f"<{len(triangles)}I", *triangles))
