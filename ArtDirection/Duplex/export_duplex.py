"""Export the approved Blender visual in the runtime's local coordinate system."""
import argparse
import math
import struct
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

parser = argparse.ArgumentParser()
parser.add_argument("destination", type=Path)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
root = bpy.data.objects["Dual Rifle Shotgun"]
rotor = bpy.data.objects["Rotating 180 degree barrel carriage"]
graph = bpy.context.evaluated_depsgraph_get()
groups = {(part, layer): {} for part in range(4) for layer in range(2)}

def appearance(mat):
    name = mat.name.lower()
    if "barrel tubes" in name:
        return 1, (.21,.43,.56,.24), (0,.92,0,0)
    if "barrel inner wall" in name:
        return 1, (.23,.45,.55,.29), (0,.88,0,1)
    if "clear ice-blue" in name:
        return 1, (.41,.63,.71,.16), (0,.94,0,0)
    if "optic glass" in name:
        return 1, (.07,.24,.32,.34), (0,.9,0,0)
    if "porcelain" in name:
        return 0, (.90,.91,.92,1), (.08,.89,0,0)
    if "carbon" in name:
        return 0, (.039,.048,.058,1), (.24,.72,0,1)
    if "rubber" in name:
        return 0, (.027,.033,.037,1), (0,.39,0,0)
    if "rifle cyan" in name:
        return 0, (.05,.69,1,1), (0,.50,1.6,0)
    if "shotgun amber" in name:
        return 0, (1,.40,.04,1), (0,.50,1.55,0)
    nodes = mat.node_tree.nodes if mat.node_tree else ()
    shader = next((node for node in nodes if node.type == "BSDF_PRINCIPLED"), None)
    if shader:
        return 0, tuple(shader.inputs["Base Color"].default_value), (
            shader.inputs["Metallic"].default_value, 1-shader.inputs["Roughness"].default_value,
            shader.inputs["Emission Strength"].default_value, 0)
    return 0, tuple(mat.diffuse_color), (0,.55,0,0)

def unity(v):
    return (v.x, v.z, v.y)

def add_triangle(group, triangle):
    for key in triangle:
        if key not in group[0]:
            group[0][key] = len(group[0])
        group[1].append(group[0][key])

for obj in root.children_recursive:
    if obj.hide_render or obj.type not in {"MESH", "FONT"}:
        continue
    part = 1 if obj.parent == rotor else 0
    obj_matrix = (rotor if part else root).matrix_world.inverted() @ obj.matrix_world
    if obj.type == "FONT":
        part = 3 if obj.name.endswith("SHOTGUN") else 2
        obj_matrix = Matrix.Translation(obj.parent.location) @ obj.matrix_basis
    model = obj.evaluated_get(graph)
    mesh = model.to_mesh()
    mesh.calc_loop_triangles()
    normal_matrix = obj_matrix.to_3x3().inverted_safe().transposed()
    for tri in mesh.loop_triangles:
        layer, color, factors = appearance(mesh.materials[tri.material_index])
        group = groups[(part, layer)].setdefault("mesh", ({}, []))
        corners = []
        for i in reversed(tri.loops):
            vertex = mesh.vertices[mesh.loops[i].vertex_index]
            position = obj_matrix @ vertex.co
            normal = (normal_matrix @ mesh.corner_normals[i].vector).normalized()
            corners.append(tuple(round(v, 6) for v in (*unity(position), *unity(normal), *color, *factors)))
        add_triangle(group, corners)
    model.to_mesh_clear()

# Bake the masked emissive engraving into small strips on the two exterior faces.
# Procedural carbon and inner-wall splatter remain object-space in the game shader.
for name, lines in [
    ("Chamfered receiver and collar shoulder", [
        ((origin-.013,.061),(origin,.095)) for origin in (.131,.149,.167)
    ]),
    ("Sculpted stock shell", [
        ((-.426,.034),(-.404,.061)), ((-.407,.038),(-.385,.065))
    ]),
]:
    target = bpy.data.objects.get(name)
    if not target and "stock" in name.lower():
        target = next(o for o in root.children if o.type == "MESH" and any(m and m.name == "Gloss porcelain stock" for m in o.data.materials)
                      and not o.hide_render)
    if not target:
        raise ValueError(name)
    target = target.evaluated_get(graph)
    for side in (-1, 1):
        for first, last in lines:
            (a,b),(c,d) = first,last
            dy,dz = c-a,d-b
            size = math.hypot(dy,dz)
            dy,dz = -dz/size*.0013,dy/size*.0013
            prior = None
            for index in range(17):
                t = index/16
                row = []
                for edge in (-1,1):
                    y,z = a+t*(c-a)+edge*dy,b+t*(d-b)+edge*dz
                    inverse = target.matrix_world.inverted()
                    ray = inverse @ Vector((side*.22,y,z))
                    hit, position, normal, _ = target.ray_cast(ray, inverse.to_3x3() @ Vector((-side,0,0)))
                    if not hit:
                        break
                    position = target.matrix_world @ position
                    position.x += side*.00032
                    row.append((*unity(position), side,0,0, .02,.65,.94,1, 0,.60,1.15,0))
                if len(row) != 2:
                    prior = None
                    continue
                if prior:
                    group = groups[(0,0)]["mesh"]
                    corners = (prior[0],row[0],row[1],prior[1])
                    if side < 0:
                        corners = corners[::-1]
                    add_triangle(group,(corners[0],corners[1],corners[2]))
                    add_triangle(group,(corners[0],corners[2],corners[3]))
                prior = row

args.destination.parent.mkdir(parents=True, exist_ok=True)
with args.destination.open("wb") as output:
    active = [(part, layer, *item["mesh"]) for (part, layer), item in groups.items() if item]
    assert {(part, layer) for part, layer, _, _ in active} == {(0,0),(0,1),(1,0),(1,1),(2,0),(3,0)}
    output.write(struct.pack("<4sI", b"DPLX", len(active)))
    for part, layer, vertices, triangles in active:
        print(part, layer, len(vertices),len(triangles)//3, flush=True)
        output.write(struct.pack("<BBII", part, layer, len(vertices), len(triangles)))
        for vertex in vertices:
            output.write(struct.pack("<14f", *vertex))
        output.write(struct.pack(f"<{len(triangles)}I", *triangles))
