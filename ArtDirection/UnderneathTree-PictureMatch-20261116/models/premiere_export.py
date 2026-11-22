"""Export the furnished dollhouse as four stacked sections with open interior routes."""
import sys
from collections import defaultdict
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from falcon_export import mesh_object, slab

PAINT = {
    "Raspberry lacquer": "Premiere Raspberry", "Petal enamel": "Toy Pink",
    "Lilac satin": "Premiere Lilac", "Blush paneling": "Premiere Blush",
    "Warm porcelain": "Toy Cream", "Brushed brass": "Toy Gold Trim",
    "Aubergine": "Toy Black Enamel", "Mulberry velvet": "Premiere Mulberry",
    "Honey frosted bulbs": "Toy Window Glow", "Twilight glazing": "Premiere Lilac",
    "Lagoon satin": "Toy Teal", "Projection ivory": "Toy White",
    "Pistachio porcelain": "Premiere Pistachio", "Midnight acoustic felt": "Toy Navy",
    "Guava velvet": "Toy Red", "Almond sponge": "Toy Wood", "Polished nickel": "Toy Silver",
}
SOLIDS = ("Toy plinth", "Gallery slab", "Runway bridge", "Apron landing", "Cross gallery",
          "Stair tread", "Upper stair landing", "Backstage roof stair", "Backstage landing",
          "Rear wing wall", "Rear dressing wall", "Fly tower return", "Cyclorama shell",
          "Stage house plinth", "Stage deck", "Revolve", "Fly gallery", "Production bridge",
          "Pilaster", "Window apron", "Window lintel", "Ivory colonnade", "Touring trunk",
          "Mix console", "Sofa seat", "Table top", "Curved theatre marquee")


def export_mansion(out):
    return export_playset(out, "Premiere Studios.blend", "Toy Doll Mansion", "Premiere Studios",
                          ("salon", "soundstage", "star suite", "cinema"), [-.215, 5.86, 11.56, 17.26, 17.55],
                          (0,16.5), -6, SOLIDS, "Studio floor", PAINT)


def export_playset(out, source, asset, title, labels, levels, spine, y_offset, solids, backdrop, paints, floor_hulls=None):
    with bpy.data.libraries.load(str(Path(__file__).with_name(source))) as (src, loaded):
        loaded.objects = list(src.objects)
    objects = [o for o in loaded.objects if o]
    for obj in objects:
        bpy.context.collection.objects.link(obj)
        for mod in obj.modifiers:
            if mod.type == "BEVEL":
                mod.segments = 1
    bpy.context.view_layer.update()
    graph = bpy.context.evaluated_depsgraph_get()
    parents = []
    for i, label in enumerate(labels):
        parent = slab(f"Piece {i+1:03} {title} {label}",
                      [(spine[0]+x,spine[1]+y) for x,y in [(-.2,0),(.2,0),(.2,.3),(-.2,.3)]], levels[i], levels[i+1])
        parent.data.materials.append(bpy.data.materials.get("Toy Navy") or bpy.data.materials.new("Toy Navy"))
        parents.append(parent)
    batches = defaultdict(lambda: ([], []))
    for obj in objects:
        if obj.type not in {"MESH","CURVE","FONT"} or obj.hide_render or obj.name.startswith(backdrop):
            continue
        evaluated = obj.evaluated_get(graph)
        mesh = evaluated.to_mesh()
        if not mesh or not mesh.polygons:
            evaluated.to_mesh_clear()
            continue
        coords = [(p.x,p.y+y_offset,p.z) for v in mesh.vertices for p in [evaluated.matrix_world @ v.co]]
        z = (max(p[2] for p in coords) + min(p[2] for p in coords))/2
        group = (sum(z >= boundary for boundary in levels[1:-1]) if floor_hulls
                 else max(0,min(3,int((z+.15)/5.7))))
        if floor_hulls and obj.name.startswith("Walkable floor with stairwell"):
            floor_hulls(obj,parents[group])
        if obj.name.startswith(solids) and not obj.name.startswith("Revolve cue"):
            raw = obj.data
            vs = [(p.x,p.y+y_offset,p.z) for v in raw.vertices for p in [obj.matrix_world @ v.co]]
            mesh_object("Hull "+obj.name,vs,[p.vertices[:] for p in raw.polygons],parents[group])
        mesh.calc_loop_triangles()
        remap = {}
        for face in mesh.loop_triangles:
            name = mesh.materials[face.material_index].name.split(".")[0]
            paint = paints[name] if paints else "Crag " + name
            verts, faces = batches[group,paint]
            indices = []
            for index in face.vertices:
                key = (paint,index)
                if key not in remap:
                    remap[key] = len(verts)
                    verts.append(coords[index])
                indices.append(remap[key])
            faces.append(indices)
        evaluated.to_mesh_clear()
    for obj in objects:
        bpy.data.objects.remove(obj,do_unlink=True)
    for (group,paint),(verts,faces) in batches.items():
        obj = mesh_object("Finish "+paint,verts,faces,parents[group])
        obj.data.materials.append(bpy.data.materials.get(paint) or bpy.data.materials.new(paint))
    bpy.ops.object.select_all(action="DESELECT")
    for parent in parents:
        for obj in (parent,*parent.children):
            obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(Path(out)/(asset+".fbx")),use_selection=True,
                             axis_forward="-Z",axis_up="Y",apply_scale_options="FBX_SCALE_ALL",
                             bake_space_transform=True,bake_anim=False)
    return parents


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    export_mansion(sys.argv[-1])
