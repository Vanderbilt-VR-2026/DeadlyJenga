"""Bake the reviewed hall into body-owned Unity art, run with Blender --background."""
import bpy as b
import gzip, struct, sys
from pathlib import Path
from mathutils import Vector as V
repo=Path(sys.argv[-1]); D=b.data
root=D.collections['02 Art revision • Worship Hall']
def pos(o):return o.matrix_world.translation
def group(o):
 n=o.name; p=pos(o)
 if n.startswith(('Stair tread','Basalt nosing','Stair cheek stone')):
  return 1+max(0,min(21,round((-p.y-1.08)/.36)))
 if n.startswith(('Upper gallery','Upper timber','Upper hall','Upper indigo','Front crest','Visible heraldic')):return 25
 if p.z>=14.8 and not n.startswith('Lower'):return 25
 if n.startswith(('Lower hipped','Ridge cap','Eave fascia','Raised ceramic','Gable edge','Closed gable')):
  # Curve and box objects above the lower roof center belong to the upper roof.
  if max((o.matrix_world@V(v)).z for v in o.bound_box)>19.65:return 25
  return 24
 if n.startswith(('Vermilion bearing column','Basalt column shoe','Stepped bracket','Bronze column binding','Cedar bracket inset')):
  row=min(range(3),key=lambda i:abs(p.y-(-13-7*i)))
  col=min(range(6),key=lambda i:abs(p.x-(-16,-9.5,-3.1,3.1,9.5,16)[i]))
  return 26+row*6+col
 if n.startswith(('Long cedar tie beam','Exposed ceiling rafter','Decorative eave block')):
  if n.startswith('Long cedar'):return 44+min(range(3),key=lambda i:abs(p.y-(-13-7*i)))
  return 24
 if n.startswith(('Altar dais tread','Sanctuary dais')):return 23
 return 0
# Only the hall, not the proxy town or the duplicate Blender sky and water.
dg=b.context.evaluated_depsgraph_get();groups={}
def u(p):return p.x,p.z,-p.y
for o in root.all_objects:
 if o.type!='MESH' or o.hide_render:continue
 g=group(o); buf=groups.setdefault(g,bytearray()); obj=o.evaluated_get(dg); me=obj.to_mesh()
 me.calc_loop_triangles()
 m=o.matrix_world; normal=m.to_3x3().inverted_safe().transposed()
 for t in me.loop_triangles:
  mat=me.materials[t.material_index];p=mat.node_tree.nodes.get('Principled BSDF')
  tint=mat.diffuse_color[:3];em=p.inputs['Emission Strength'].default_value
  rgb=[max(0,min(255,round(c**.83*255))) for c in tint]
  rgba=(*rgb,round(255*(1-min(.72,em*.41))))
  # Unity winding with the existing NinjaMesh shader conventions.
  for i,l in zip(t.vertices,t.loops):
   v=u(m@me.vertices[i].co);n=u((normal@me.corner_normals[l].vector).normalized())
   buf.extend(struct.pack('<6f4B',*v,*n,*rgba))
 obj.to_mesh_clear()
dest=repo/'Assets/_DeadlyJenga/Presentation/Visuals/NinjaTemple/Resources/ShrineReviewedHall.bytes'
with dest.open('wb') as raw, gzip.GzipFile(fileobj=raw,filename='',mode='wb',mtime=0) as f:
 f.write(struct.pack('<4sI',b'SHR2',len(groups)))
 for g,buf in sorted(groups.items()):
  f.write(struct.pack('<II',g,len(buf)//28));f.write(buf)
print(f'{dest.name}: {len(groups)} sections, {sum(len(v)//84 for v in groups.values())} triangles',flush=True)
