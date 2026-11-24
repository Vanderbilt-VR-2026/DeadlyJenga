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
elif mode == "spikers-right":
    root = bpy.data.objects["Dual Spikes.001"]
elif mode in {"alien", "sheriff", "sword", "rpg", "axe", "shooter", "marshmallow"}:
    name = {"alien": "Alien Blaster", "sheriff": "Blue Sheriff",
            "sword": "Stock Murderer Sword", "rpg": "RPG", "axe": "Laser Axe",
            "shooter": "Stock Saw Wheel Shooter", "marshmallow": "Stock Flaming Marshmallow"}[mode]
    root = bpy.data.objects[name]
else:
    root = next(obj.parent for obj in scene.objects
                if obj.name.startswith("Continuous rounded receiver") and not obj.hide_render)
matrix = root.matrix_world.inverted()
alien_visible = set()
alien_body = None
if mode == "alien":
    for frame in (scene.frame_start, 84):
        scene.frame_set(frame)
        alien_visible.update(obj.name for obj in root.children_recursive if not obj.hide_render)
    shell = bpy.data.objects["Detailed alien blaster surface 0"]
    alien_body = bpy.data.meshes.new_from_object(shell.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    hatch = bpy.data.objects["Pop-up cooling tab"]
    for modifier in list(hatch.modifiers):
        if modifier.type == "BEVEL":
            hatch.modifiers.remove(modifier)
    scene.frame_set(scene.frame_start)
if mode in {"launcher", "staff", "chaingun", "alien", "sheriff", "sword", "rpg", "axe"}:
    for obj in root.children_recursive:
        if mode == "axe" and obj.type == "CURVE":
            obj.data.bevel_resolution = min(obj.data.bevel_resolution, 1)
            obj.data.resolution_u = min(obj.data.resolution_u, 4)
        belt = mode == "chaingun" and (
            obj.name.startswith("Belt link") or (obj.parent and obj.parent.name.startswith("Belt round"))
        )
        if mode == "alien" and obj.name == "Pop-up cooling tab":
            continue
        budget = 60 if belt else 300 if mode == "axe" else 1200
        if obj.type != "MESH" or obj.hide_render or len(obj.data.polygons) < budget:
            continue
        modifier = obj.modifiers.new("Runtime polygon budget", "DECIMATE")
        modifier.ratio = 0.32 if belt else 0.40 if mode == "launcher" else 0.25
    bpy.context.view_layer.update()
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
        if name.startswith("Charge meter segment"):
            suffix = name.removeprefix("Charge meter segment").lstrip(".")
            return 8 + int(suffix or 0) % 6
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
        if name.startswith("Crystal ") and name.split()[1].split(".")[0].isdigit():
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
    base = values["Base Color"]
    color = tuple(material.diffuse_color if mode == "sword" and base.is_linked else base.default_value)
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

def collect(only_part=None, target_part=None):
    graph = bpy.context.evaluated_depsgraph_get()
    for obj in sorted(root.children_recursive, key=lambda item: item.name):
        if obj.type not in {"MESH", "FONT", "CURVE"} or (obj.hide_render if mode != "alien" else obj.name not in alien_visible or obj.name.startswith(("Cooling vapor", "Blue charge segment"))):
            continue
        if mode == "marshmallow" and any(word in obj.name.lower() for word in ("flame", "smoke", "fleck")):
            continue
        if only_part is not None and part_of(obj) != only_part:
            continue
        model = obj.evaluated_get(graph)
        frozen_body = mode == "alien" and obj.name == "Detailed alien blaster surface 0"
        mesh = alien_body if frozen_body else model.to_mesh()
        mesh.calc_loop_triangles()
        transform = matrix @ obj.matrix_world
        normal_transform = transform.to_3x3().inverted_safe().transposed()
        part = part_of(obj) if target_part is None else target_part
        if mode == "staff" and part == 1:
            transform.translation.y -= .88
        if mode == "chaingun" and part == 2:
            transform = bpy.data.objects["Foundry six-barrel rotor"].matrix_world.inverted() @ obj.matrix_world
        if mode == "shooter" and part == 2:
            transform = bpy.data.objects["Serrated saw wheel - forged carbide"].matrix_world.inverted() @ obj.matrix_world
        morph = mode == "alien" and obj.name == "Pop-up cooling tab"
        triangles_to_export = ((poly.material_index, (poly.loop_start, poly.loop_start + i, poly.loop_start + i + 1))
                               for poly in mesh.polygons for i in range(1, poly.loop_total - 1)) if morph else (
                                   (triangle.material_index, triangle.loops) for triangle in mesh.loop_triangles)
        for material_index, loops in triangles_to_export:
            material = mesh.materials[material_index]
            layer, color, finish = surface(material)
            if mode == "launcher" and part >= 8:
                layer, color, finish = 0, (.12, .78, .35, 1), (.1, .66, 1.35, 0)
            vertices, triangles = parts[(part, layer)]
            for loop_index in reversed(loops):
                point = transform.translation if mode == "alien" and obj.hide_render else transform @ mesh.vertices[mesh.loops[loop_index].vertex_index].co
                normal = (normal_transform @ mesh.corner_normals[loop_index].vector).normalized()
                key = tuple(round(x, 6) for x in (*xyz(point), *xyz(normal), *color, *finish))
                if mode == "alien":
                    # Keep a stable vertex order across both authored hatch poses.
                    identity = (obj.name, mesh.loops[loop_index].vertex_index, *color, *finish) if morph else (obj.name, mesh.loops[loop_index].vertex_index,
                                *tuple(round(x, 5) for x in mesh.corner_normals[loop_index].vector), *color, *finish)
                    value = vertices.setdefault(identity, (len(vertices), key))
                    triangles.append(value[0])
                else:
                    triangles.append(vertices.setdefault(key, len(vertices)))
        if not frozen_body:
            model.to_mesh_clear()


collect()
if mode == "chaingun":
    for part, frame in enumerate((3, 5, 7, 9, 11), 13):
        scene.frame_set(frame)
        collect(only_part=1, target_part=part)
elif mode == "alien":
    scene.frame_set(84)
    collect(target_part=1)

assert {part for part, _ in parts} == set(range({"launcher": 14, "spikers": 7, "spikers-right": 7, "staff": 2, "chaingun": 18, "alien": 2, "sheriff": 2, "sword": 1, "rpg": 1, "axe": 1, "shooter": 3, "marshmallow": 1}[mode]))
with Path(destination).open("wb") as output:
    output.write(struct.pack("<4sI", b"WART", len(parts)))
    for (part, layer), (vertices, triangles) in sorted(parts.items()):
        print(mode, part, layer, len(vertices), len(triangles) // 3, flush=True)
        output.write(struct.pack("<BBII", part, layer, len(vertices), len(triangles)))
        for vertex in (item[1] for item in vertices.values()) if mode == "alien" else vertices:
            output.write(struct.pack("<14f", *vertex))
        output.write(struct.pack(f"<{len(triangles)}I", *triangles))
