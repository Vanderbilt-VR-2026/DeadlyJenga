"""Export Skyline landmark render meshes and the stair/wall boxes used in Unity."""
from pathlib import Path
import bpy
import gzip
import json
import math
import sys
from mathutils import Matrix, Vector

out,destination=map(Path,sys.argv[sys.argv.index('--')+1:])
data=json.loads((out/'gingerbread-skyline-layout.json').read_text())
deps=bpy.context.evaluated_depsgraph_get()
palette={
    'honey':1,'biscuit':0,'toast':2,'cocoa':2,'icing':3,'snow':4,'rose':5,
    'lavender':6,'berry':7,'fir':8,'mint':8,'gold':10,'floor':2,'Amber sugar glass':9,
    'Hot golden lantern panes':9,'Candle gold':9,'Marshmallow icing':3,
    'Rose icing':5,'Marzipan':1,'Pistachio icing':8,'Raspberry boiled sugar':7,
}
def quant(v):
    return round(v*1000)

def export_objects(objects,center,inverse):
    vertices=[]
    faces=[[] for i in range(12)]
    for obj in objects:
        if obj.type not in ('MESH','FONT'):
            continue
        ev=obj.evaluated_get(deps)
        mesh=obj.data if obj.type=='MESH' else ev.to_mesh()
        mesh.calc_loop_triangles()
        mats=[palette.get(m.name,1) if m else 1 for m in mesh.materials]
        local_map={}
        for tri in mesh.loop_triangles:
            mat=mats[tri.material_index] if mats else 3
            indices=[]
            for idx in tri.vertices:
                if idx not in local_map:
                    p=inverse@(ev.matrix_world@mesh.vertices[idx].co-center)
                    local_map[idx]=len(vertices)//3
                    vertices.extend([quant(p.x),quant(p.z),quant(p.y)])
                indices.append(local_map[idx])
            a,b,c=indices
            faces[mat].extend([a,c,b])
        if obj.type=='FONT':ev.to_mesh_clear()
    return dict(vertices=vertices,groups=[dict(finish=i,indices=inds) for i,inds in enumerate(faces) if inds])

def box_data(objects,center,inverse):
    ret=[]
    for obj in objects:
        verts=list(obj.data.vertices)
        assert len(verts)%8==0
        for j in range(0,len(verts),8):
            local=[inverse@(obj.matrix_world@v.co-center) for v in verts[j:j+8]]
            lo=[min(p[i] for p in local) for i in (0,2,1)]
            hi=[max(p[i] for p in local) for i in (0,2,1)]
            assert all(hi[i]-lo[i]>.004 for i in range(3))
            ret.extend(quant(x) for x in [*(.5*(u+v) for u,v in zip(lo,hi)),*(v-u for u,v in zip(lo,hi))])
    return ret

clock_collection=bpy.data.collections['Big Ben | gingerbread clocktower connector']
cx,cy=data['clock_center']
identity=Matrix.Identity(3)
clock=export_objects([obj for obj in clock_collection.objects if not obj.name.startswith('Clock Court survivor')],
                     Vector((cx,cy,0)),identity)
buildings=[]
for tower in data['towers']:
    name=tower['name']
    col=bpy.data.collections[name+' | stacked houses']
    center=Vector(tower['origin'])
    inv=Matrix.Rotation(-tower['angle'],3,'Z')
    shell=[o for o in col.all_objects if ' | solid shell ' in o.name]
    roof=[o for o in col.objects if ' | baked roof peak' in o.name]
    body=[o for o in col.all_objects if o not in roof and not o.name.endswith(' | entry pavers')]
    buildings.append(dict(name=name,width=tower['width'],depth=tower['depth'],
        floors=tower['floors'],door=tower['door'], shell=export_objects(body,center,inv),
        roof=export_objects(roof,center,inv),boxes=box_data(shell,center,inv)))
result=dict(clock=clock,houses=buildings)
with destination.open('wb') as target:
    with gzip.GzipFile(filename='',mode='wb',fileobj=target,compresslevel=8,mtime=0) as archive:
        archive.write(json.dumps(result,separators=(',',':')).encode('utf-8'))
for item in buildings:
    print(item['name'],len(item['boxes'])//6,'boxes',len(item['shell']['vertices'])//3,'shell vertices',flush=True)
print(destination,destination.stat().st_size,'bytes',flush=True)
