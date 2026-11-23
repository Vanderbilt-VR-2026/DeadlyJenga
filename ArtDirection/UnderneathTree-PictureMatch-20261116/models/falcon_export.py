"""Export Falcon Summit from the editable Blender playset, excluding its render studio."""
import math
import sys
from collections import defaultdict
from pathlib import Path

import bpy

PAINT = {
    "Amber target": "Toy Window Glow", "Canopy | orange resin": "Toy Orange",
    "Cognac upholstery": "Toy Brown", "Enamel | deep lagoon": "Toy Teal",
    "Enamel | feather edges": "Toy Teal", "Enamel | launch orange": "Toy Orange",
    "Enamel | warm ivory": "Toy Cream", "Glazing | cyan": "Toy Glass",
    "Ice blue instrumentation": "Toy Screen Glow", "Markings | honey": "Toy Gold",
    "Smoked sky glazing": "Falcon Clear Glass", "Socket | graphite": "Toy Black",
    "Structural | anodized silver": "Toy Silver", "Structural | midnight": "Toy Black Enamel",
    "Walnut": "Toy Wood", "Woven oxblood": "Toy Red Enamel",
    **{f"Basalt {i}": "Toy Black" if i % 2 == 0 else "Toy Gray" for i in range(4)}
}


def mesh_object(name, vertices, faces, parent=None):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    return obj


def slab(name, points, bottom, top, parent=None):
    n = len(points)
    vertices = [(x, y, z) for z in (bottom, top) for x, y in points]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
    faces += [(i, (i+1)%n, n+(i+1)%n, n+i) for i in range(n)]
    return mesh_object(name, vertices, faces, parent)


def export_falcon(out):
    source = Path(__file__).with_name("Falcon Summit.blend")
    with bpy.data.libraries.load(str(source)) as (available, loaded):
        loaded.objects = list(available.objects)
    objects = [o for o in loaded.objects if o]
    for obj in objects:
        bpy.context.collection.objects.link(obj)
        for mod in obj.modifiers:
            if mod.type == "BEVEL":
                mod.segments = 1
    bpy.context.view_layer.update()
    graph = bpy.context.evaluated_depsgraph_get()
    batches = defaultdict(lambda: ([], []))
    # The rear spine is the support mesh for each breakaway storey. Hull children
    # give the same rigidbody its stairs, walls and open-sided galleries.
    bases = [0, 5.51, 11.01, 16.51, 22.01, 27.41]
    tops = [5.50, 11, 16.50, 22, 27.40, 27.69]
    parents = [slab(f"Piece {i+1:03} Falcon Summit {label}",
                    [(-.55,9.85),(.55,9.85),(.55,10.4),(-.55,10.4)], lo, hi)
               for i,(label,lo,hi) in enumerate(zip(("cave","hangar","signals","lounge approach","penthouse","crown"),bases,tops))]
    for parent in parents:
        parent.data.materials.append(bpy.data.materials.new("Falcon spine") if not bpy.data.materials.get("Falcon spine") else bpy.data.materials["Falcon spine"])

    def sector(i, name, inner, outer, a, b, lo, hi):
        def point(r,angle):
            t=math.radians(angle)
            return (r*math.cos(t),2+r*math.sin(t))
        points=[point(outer,a),point(outer,b),point(inner,b),point(inner,a)]
        slab("Hull "+name, points,lo,hi,parents[i])

    for level in range(4):
        for k in range(18):
            a=-90+20*k
            h=.5+5.5*(level+(k+1)/18)
            sector(level,"stair",1.85,5.42,a,a+20,h-.37,h)
    for level in range(1,5):
        h=.5+5.5*level
        for k in range(24):
            sector(level,"gallery",5.30,9.22,k*15,(k+1)*15,h-.45,h)
    for a,b,r in [(-90,127,5.42),(127,270,1.72)]:
        for k in range(12):
            sector(4,"penthouse floor",.001,r,a+(b-a)*k/12,a+(b-a)*(k+1)/12,22.27,22.51)

    solids=("Hangar apron","launch ramp","Wing bay mezzanine","Cantilever flight pad","Flight bridge",
            "Lift-off roof","Observation sail","swept wing shell","hooked ivory beak","falcon face shell",
            "Carved basalt","Portal ceiling","Flight deck","Service turntable","Briefing table brightwork",
            "Saddle cushion","Coffee table","Drinks cabinet","Situation panel","Club cushion","Club back")
    for obj in objects:
        if obj.type not in {"MESH","CURVE","FONT"} or obj.hide_render or "Studio ground" in obj.name:
            continue
        evaluated=obj.evaluated_get(graph)
        mesh=evaluated.to_mesh()
        if not mesh or not mesh.polygons:
            evaluated.to_mesh_clear()
            continue
        coords=[tuple(evaluated.matrix_world @ v.co) for v in mesh.vertices]
        height=(min(v[2] for v in coords)+max(v[2] for v in coords))/2
        group=min(5,max(0,int((height-.50)/5.50)))
        if "Walkaround gallery" in obj.name or "deck molding" in obj.name:
            group=int(round((height-.50)/5.50))
        if any(s in obj.name for s in ("Wing bay mezzanine","Cantilever flight pad","Flight bridge","Penthouse infill","Arrival well inside","Lift-off roof")):
            group=1 if "Wing bay" in obj.name else 5 if "Lift-off" in obj.name else 4
        if any(s in obj.name for s in solids) or any(s in obj.name for s in ("Panoramic window","Faceted lagoon curtainwall")):
            # Use the un-beveled source geometry for cheap convex cooking.
            raw=obj.data
            if any(s in obj.name for s in ("Panoramic window","Faceted lagoon curtainwall")):
                raw=mesh  # includes the glazing thickness
            vs=[tuple(obj.matrix_world @ v.co) for v in raw.vertices]
            mesh_object("Hull "+obj.name,vs,[p.vertices[:] for p in raw.polygons],parents[group])
        mesh.calc_loop_triangles()
        remap={}
        for face in mesh.loop_triangles:
            material=mesh.materials[face.material_index]
            paint=PAINT[material.name.split(".")[0]]
            verts,faces=batches[group,paint]
            indices=[]
            for v in face.vertices:
                key=(paint,v)
                if key not in remap:
                    remap[key]=len(verts)
                    verts.append(coords[v])
                indices.append(remap[key])
            faces.append(indices)
        evaluated.to_mesh_clear()
    for obj in objects:
        bpy.data.objects.remove(obj,do_unlink=True)
    for (group,paint),(verts,faces) in batches.items():
        obj=mesh_object("Finish "+paint,verts,faces,parents[group])
        obj.data.materials.append(bpy.data.materials.get(paint) or bpy.data.materials.new(paint))
    bpy.ops.object.select_all(action="DESELECT")
    for parent in parents:
        parent.select_set(True)
        for child in parent.children:
            child.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(Path(out)/"Toy Superhero Base.fbx"), use_selection=True,
                             axis_forward="-Z",axis_up="Y",apply_scale_options="FBX_SCALE_ALL",
                             bake_space_transform=True,bake_anim=False)
    return parents


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    export_falcon(sys.argv[-1])
