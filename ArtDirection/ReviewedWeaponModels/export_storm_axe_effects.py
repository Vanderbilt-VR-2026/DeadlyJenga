import bpy
import struct
import sys
from pathlib import Path
from mathutils import Vector

output = Path(sys.argv[sys.argv.index("--") + 1])
scene = bpy.context.scene
axe = bpy.data.objects["Laser Axe"]
to_weapon = axe.matrix_world.inverted()
stages = (Vector((0,0,0)), Vector((.103,1.020,.306)), Vector((.022,1.48,.267)), Vector((.026,1.828,.261)))
finishes = {
    "Surging cyan", "Hot lightning core", "Blue energy edge", "Violet storm fringe",
    "Frozen vapor flecks", "Translucent electrical wake", "Sugar fracture interior"
}
with output.open("wb") as stream:
    stream.write(b"SAFX")
    for frame, origin in enumerate(stages, 1):
        scene.frame_set(frame)
        objects = [obj for obj in scene.objects if not obj.hide_render and obj.type in {"MESH", "CURVE"}
                   and any(material and material.name in finishes for material in obj.data.materials)]
        for obj in objects:
            if obj.type == "CURVE":
                obj.data.bevel_resolution = 0
        bpy.context.view_layer.update()
        graph = bpy.context.evaluated_depsgraph_get()
        vertices, indices = {}, []
        for obj in objects:
            proxy = obj.evaluated_get(graph)
            mesh = proxy.to_mesh()
            mesh.calc_loop_triangles()
            for triangle in mesh.loop_triangles:
                material = mesh.materials[triangle.material_index]
                if material.name not in finishes:
                    continue
                color = tuple(material.diffuse_color[:3])
                alpha = .24 if material.name == "Translucent electrical wake" else .92
                for vertex_id in reversed(triangle.vertices):
                    point = to_weapon @ (obj.matrix_world @ mesh.vertices[vertex_id].co - origin)
                    if frame != 1:
                        point = point - to_weapon.translation
                    item = tuple(round(x, 6) for x in (point.x, point.z, point.y, *color, alpha))
                    indices.append(vertices.setdefault(item, len(vertices)))
            proxy.to_mesh_clear()
        assert vertices and indices and len(vertices) < 65000
        stream.write(struct.pack("<II", len(vertices), len(indices)))
        for vertex in vertices:
            stream.write(struct.pack("<7f", *vertex))
        stream.write(struct.pack(f"<{len(indices)}H", *indices))
        print("STORM FX", frame, len(objects), len(vertices), len(indices)//3, flush=True)
