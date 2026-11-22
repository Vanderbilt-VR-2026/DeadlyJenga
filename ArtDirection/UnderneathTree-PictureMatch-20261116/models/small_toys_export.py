"""Export the reviewed Christmas toys. Each collectible has one mesh and a base-centered pivot."""
import sys
from pathlib import Path

import bpy
from mathutils import Vector

def export_small_toys(output):
    source = Path(__file__).with_name("Christmas small toys.blend")
    with bpy.data.libraries.load(str(source)) as (assets, loaded):
        loaded.collections = sorted(name for name in assets.collections if name.startswith("Toy "))
    built = []
    for collection in loaded.collections:
        bpy.context.scene.collection.children.link(collection)
        bpy.ops.object.select_all(action="DESELECT")
        parts = list(collection.objects)
        for obj in parts:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = parts[0]
        bpy.ops.object.convert(target="MESH")
        bpy.ops.object.join()
        toy = bpy.context.object
        toy.name = collection.name
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        coords = [v.co for v in toy.data.vertices]
        low = Vector(map(min, zip(*coords)))
        high = Vector(map(max, zip(*coords)))
        center = (low + high) / 2
        for v in toy.data.vertices:
            v.co -= Vector((center.x, center.y, low.z))
        toy.data.update()
        bpy.ops.export_scene.fbx(filepath=str(Path(output)/(toy.name+".fbx")), use_selection=True,
                                 axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL",
                                 bake_space_transform=True, bake_anim=False, mesh_smooth_type="FACE")
        built.append(toy)
    return built

if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    export_small_toys(sys.argv[-1])
