import bpy,sys,re,struct,hashlib,math
from pathlib import Path
from mathutils import Vector
s=bpy.context.scene;dst=Path(sys.argv[-1])
frame=bpy.data.objects['Reference proportion rig | map width and height']
bpy.context.view_layer.update()
inv=frame.matrix_world.inverted()
blocks={}
def add(owner,obj,solid):
 if obj.type not in ('MESH','FONT') or obj.hide_render:return
 blocks.setdefault(owner,[]).append((obj,solid))
lion=bpy.data.collections['17 | Reclining Sphinx']
for obj in sorted(lion.all_objects,key=lambda o:o.name):
 n=obj.name
 if n.startswith('Body course'):
  i,j=map(int,re.search(r'(\d+) \| curved voussoir (\d+)',n).groups())
  owner=(('Sphinx shoulder bearing 1' if j<=3 else 'Sphinx shoulder bearing -1') if j<=3 or j>=13 else 'Sphinx chest lintel') if i==1 else f'Sphinx torso bay {i:02}'
  add(owner,obj,True)
 elif n.startswith('Sphinx striped'):add('Sphinx carved crown',obj,False)
 elif n.startswith('Foreleg'):add(n,obj,True)
 elif n.startswith('Forepaw'):
  q=inv@obj.matrix_world@Vector(obj.bound_box[0]);add('Sphinx left forepaw' if q.x<0 else 'Sphinx right forepaw',obj,'rounded toe' in n)
 elif n.startswith(('Hind leg','Hind paw')):add(n,obj,True)
 elif n.startswith('Tail'):add(n,obj,True)
 elif n.startswith('Shoulder route'):
  q=inv@obj.matrix_world@Vector(obj.bound_box[0]);add('Sphinx west stairs' if q.x<0 else 'Sphinx east stairs',obj,True)
 else:
  q=inv@obj.matrix_world@Vector(obj.bound_box[0]) if obj.type=='MESH' else Vector((0,0,0))
  j=min(13,max(1,int((q.y+4.6)/2)+1))
  add('Sphinx chest lintel' if j==1 else f'Sphinx torso bay {j:02}',obj,False)
for obj in sorted(bpy.data.collections['18 | Rail bridge over the sphinx span'].all_objects,key=lambda o:o.name):
 n=obj.name;i=re.match(r'Bridge (\d+)',n).group(1)
 add(f'Rail bridge {i}',obj,'cap beam' in n or 'exterior post' in n)
for obj in sorted(bpy.data.collections['19 | Compact Western town'].all_objects,key=lambda o:o.name):
 n=obj.name
 if n.startswith('Tack'):
  roof='roof' in n or 'covered boardwalk tin' in n or 'sign' in n
  solid=any(k in n for k in ('floor','siding','post','sill','roof','tin','counter'))
  add('Tack shop roof' if roof else 'Tack shop frame',obj,solid)
 elif n.startswith(('Copper Canyon depot','Depot |')):
  solid=any(k in n for k in ('wall','floor board','shade boards','clapboard','jamb','lintel','ticket counter'))
  add('Copper Canyon ticket room',obj,solid)
 elif n.startswith('Saloon balcony'):add('Saloon wraparound veranda 1',obj,False)
 elif n.startswith('Saloon upper'):add('Saloon timber storey 1',obj,False)
 elif n.startswith('Saloon'):add('Saloon timber storey 0',obj,False)
 elif n.startswith('Corral'):add('Stock corral',obj,False)
for obj in sorted(bpy.data.collections['20 | Great terraced pyramid'].all_objects,key=lambda o:o.name):
 n=obj.name
 if obj.type not in ('MESH','FONT'):continue
 q=inv@obj.matrix_world@Vector((0,0,0));z=q.z
 side=1 if -(q.x-20)*math.sin(math.radians(40))+(q.y-10)*math.cos(math.radians(40))>0 else -1
 if n.startswith('Bore'):key='Pyramid portal vault'
 elif n.startswith('Summit'):key='Open summit pavilion'
 elif n.startswith('Great stair'):key='Pyramid broad forecourt flight'
 else:
  level=min(3,max(0,int((z-2.5)/3.18)))
  key=f'Pyramid gallery {level}/{side}'
 add(key,obj,False)
# Include the modelled broad stair as a separate walking and breakaway member.
for obj in sorted(s.objects,key=lambda o:o.name):
 if obj.name.startswith('Pyramid | broad continuous stone flight'):add('Pyramid broad exterior stair',obj,True)
 elif obj.name.startswith('Pyramid | stair cheek course'):add('Pyramid broad exterior stair',obj,False)
# Match the unique Blender false front while keeping its existing body name.
for obj in sorted(s.objects,key=lambda o:o.name):
 if obj.name.startswith(('Saloon | arched','Saloon | front cornice','Saloon | roof behind','Saloon | false-front','Saloon | side parapet','Saloon | end copper','Saloon | dark hand-painted')):
  add('Saloon false front and cornice',obj,False)
def enc(v):
 x,y,z=v
 return (x,z,y)
def srgb(x):
 return round(max(0,min(1,(1.055*x**(1/2.4)-.055) if x>.0031308 else x*12.92))*255)
# The front crown retains base geometry and source colors. Concavity is represented
# by the map's dedicated collision hulls. All exported decorations are visual only.
deps=bpy.context.evaluated_depsgraph_get()
dst.parent.mkdir(exist_ok=True,parents=True)
with dst.open('wb') as f:
 def string(s):
  a=s.encode('utf-8');f.write(struct.pack('<H',len(a)));f.write(a)
 f.write(struct.pack('<IH',0x4a53424c,len(blocks)))
 for owner,objects in blocks.items():
  string(owner);f.write(struct.pack('<H',len(objects)))
  for ob,solid in objects:
   ev=ob.evaluated_get(deps)
   # Use base meshes for collision; the modest bevel on props survives the evaluated render.
   me=(ob.data if ob.type=='MESH' else ev.to_mesh())
   me.calc_loop_triangles()
   m=inv@ob.matrix_world
   vertices=[enc(m@v.co) for v in me.vertices]
   materials=[tuple(srgb(x) for x in mat.diffuse_color[:3]) if mat else (230,166,88) for mat in me.materials]
   f.write(struct.pack('<?HH',solid,len(vertices),len(me.loop_triangles)))
   for v in vertices:f.write(struct.pack('<fff',*v))
   for t in me.loop_triangles:
    a,b,c=t.vertices
    f.write(struct.pack('<HHHBBB',a,c,b,*((materials[t.material_index]) if materials else (230,166,88))))
   if ob.type!='MESH':ev.to_mesh_clear()
print(len(blocks),'owners',sum(map(len,blocks.values())),'mesh pieces',dst.stat().st_size,'bytes',hashlib.sha256(dst.read_bytes()).hexdigest(),flush=True)
