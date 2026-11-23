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
elif mode == "staff":
    root = bpy.data.objects["Saw Wheel Staff | Natural branch"]
elif mode == "chaingun":
    root = bpy.data.objects["Stock Chaingun"]
elif mode in {"alien", "sheriff", "sword", "rpg", "axe", "shooter", "marshmallow"}:
    name = {"alien": "Alien Blaster", "sheriff": "Blue Sheriff",
            "sword": "Stock Murderer Sword", "rpg": "RPG", "axe": "Laser Axe",
            "shooter": "Stock Saw Wheel Shooter", "marshmallow": "Stock Flaming Marshmallow"}[mode]
    root = bpy.data.objects[name]
else:
    root = next(obj.parent for obj in scene.objects
                if obj.name.startswith("Continuous rounded receiver") and not obj.hide_render)
matrix = root.matrix_world.inverted()
if mode in {"launcher", "staff", "chaingun", "alien", "sheriff", "sword", "rpg", "axe"}:
    for obj in root.children_recursive:
        if obj.type != "MESH" or obj.hide_render or len(obj.data.polygons) < 1200:
            continue
        modifier = obj.modifiers.new("Runtime polygon budget", "DECIMATE")
        modifier.ratio = 0.40 if mode == "launcher" else 0.25
    bpy.context.view_layer.update()
graph = bpy.context.evaluated_depsgraph_get()
parts = collections.defaultdict(lambda: ({}, []))

def part_of(obj):
    name = obj.name
    if mode in {"alien", "sword", "rpg", "marshmallow", "axe"}:
        return 0
    if mode in {"sheriff", "shooter"}:
        ancestor = obj
        while ancestor and ancestor != root:
            if mode == "shooter" and ancestor.name.startswith("Serrated saw wheel"):
                return 2
            if ancestor.name.startswith("Animated ammunition assembly"):
                return 1
            ancestor = ancestor.parent
        return 0
    if mode == "launcher":
        if name.startswith("Charge cell "):
            return 1 + int(name.split()[2].split(".")[0])
        return int(name.startswith("Drum end shield"))
    ancestor = obj
    if mode in {"staff", "chaingun"}:
        while ancestor and ancestor != root:
            name = ancestor.name
            if mode == "staff" and name.startswith("Wheel |"):
                return 1
            if mode == "chaingun":
                if name.startswith(("Belt round", "Belt link", "Animated ammunition assembly")):
                    return 1
                if name.startswith("Foundry six-barrel rotor"):
                    return 2
                if name.startswith("Recessed heat bar"):
                    suffix = name.rpartition(".")[2]
                    return 3 + (int(suffix) if suffix.isdigit() else 0)
            ancestor = ancestor.parent
        return 0
    while ancestor and ancestor != root:
        name = ancestor.name
        if name.startswith(("Extended lime power cell", "Brass cell collar")):
            return 1
        if name.startswith("Crystal "):
            return 1 + int(name.split()[1].split(".")[0])
        ancestor = ancestor.parent
    return 0

def surface(material):
    if not material:
        return 0, (.16, .17, .18, 1), (.3, .6, 0, 0)
    if mode == "chaingun" and material.name.startswith("Heat meter segment "):
        critical = int(material.name.split()[-1]) >= 9
        color = (1, .06, .025, 1) if critical else (1, .47, .035, 1)
        return 0, color, (.1, .72, .9, 0)
    if mode == "staff":
        name = material.name.lower()
        if "bark" in name:
            return 0, (.16,.086,.04,1), (0,.28,0,0)
        if "wood" in name or "grain" in name:
            return 0, (.44,.26,.13,1), (0,.34,0,0)
        if "saw disc" in name:
            return 0, (.26,.23,.20,1), (.85,.5,0,0)
        if "sleeve" in name:
            return 0, (.23,.22,.22,1), (.66,.42,0,0)
        return 0, (.078,.073,.07,1), (.60,.45,0,0)
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
    if obj.type not in {"MESH", "FONT", "CURVE"} or obj.hide_render:
        continue
    if mode == "marshmallow" and any(word in obj.name.lower() for word in ("flame", "smoke", "fleck")):
        continue
    model = obj.evaluated_get(graph)
    mesh = model.to_mesh()
    mesh.calc_loop_triangles()
    transform = matrix @ obj.matrix_world
    normal_transform = transform.to_3x3().inverted_safe().transposed()
    part = part_of(obj)
    if mode == "staff" and part == 1:
        transform.translation.y -= .88
    if mode == "chaingun" and part == 2:
        transform = bpy.data.objects["Foundry six-barrel rotor"].matrix_world.inverted() @ obj.matrix_world
    if mode == "shooter" and part == 2:
        transform = bpy.data.objects["Serrated saw wheel - forged carbide"].matrix_world.inverted() @ obj.matrix_world
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

assert {part for part, _ in parts} == set(range({"launcher": 8, "spikers": 7, "staff": 2, "chaingun": 13, "alien": 1, "sheriff": 2, "sword": 1, "rpg": 1, "axe": 1, "shooter": 3, "marshmallow": 1}[mode]))
with Path(destination).open("wb") as output:
    output.write(struct.pack("<4sI", b"WART", len(parts)))
    for (part, layer), (vertices, triangles) in sorted(parts.items()):
        print(mode, part, layer, len(vertices), len(triangles) // 3, flush=True)
        output.write(struct.pack("<BBII", part, layer, len(vertices), len(triangles)))
        for vertex in vertices:
            output.write(struct.pack("<14f", *vertex))
        output.write(struct.pack(f"<{len(triangles)}I", *triangles))
