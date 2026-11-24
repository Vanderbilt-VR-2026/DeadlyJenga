import bpy
import random
import sys
from pathlib import Path

destination = Path(sys.argv[sys.argv.index("--") + 1])
source = bpy.data.objects["Three-dimensional greenery around original boughs"]
mesh = source.data
assert len(mesh.vertices) % 6 == 0 and len(mesh.polygons) == len(mesh.vertices) // 6 * 8
rng = random.Random(724)
groups = [([], []) for _ in mesh.materials]
for index in range(len(mesh.vertices) // 6):
    face_start = index * 8
    slot = mesh.polygons[face_start].material_index
    assert all(mesh.polygons[i].material_index == slot for i in range(face_start, face_start + 8))
    if rng.randrange(12) != 0:
        continue
    vertices, faces = groups[slot]
    start = index * 6
    base = len(vertices)
    for vertex_index in range(start, start + 6):
        point = source.matrix_world @ mesh.vertices[vertex_index].co
        vertices.append((point.x / .145, point.y / .145, point.z / .185))
    for face_index in range(face_start, face_start + 8):
        faces.append(tuple(base + vertex - start for vertex in mesh.polygons[face_index].vertices))

bpy.ops.object.select_all(action="DESELECT")
for slot, (vertices, faces) in enumerate(groups):
    name = ("Fir Bough Depth Shade", "Fir Bough Depth Green", "Fir Bough Depth Tips", "Fir Bough Depth Twigs")[slot]
    assert faces and len(faces) < 33000
    part = bpy.data.meshes.new(name)
    part.from_pydata(vertices, [], faces)
    part.update()
    obj = bpy.data.objects.new(name, part)
    bpy.context.scene.collection.objects.link(obj)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.wm.obj_export(filepath=str(destination / (name + ".obj")), export_selected_objects=True,
        export_materials=False, forward_axis="NEGATIVE_Z", up_axis="Y")
    obj.select_set(False)
    print(name, len(vertices), len(faces), flush=True)
