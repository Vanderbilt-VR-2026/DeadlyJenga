"""Export the approved Blender sculpts as one palette-textured skin per disaster."""
import bpy
import math
import struct
import zlib
from mathutils import Vector
from pathlib import Path

here = Path(__file__).resolve().parent
out = here.parents[2] / 'Assets/_DeadlyJenga/Presentation/Visuals/Resources/UnderneathTreeFinish'
colors = {}
def swatch(color):
    rgb = tuple(round(min(1, max(0, v)) ** (1 / 2.2) * 63) * 4 for v in color[:3])
    i = colors.setdefault(rgb, len(colors))
    assert i < 4096
    return ((i % 64 + .5) / 64, (i // 64 + .5) / 64)

def unit(x):
    return max(0, min(1, x))

for cat in (True, False):
    bpy.ops.wm.open_mainfile(filepath=str(here / 'Christmas creatures.blend'))
    collection = next(c for c in bpy.data.collections if c.name.startswith('Cat Attack' if cat else 'Evil Santa'))
    objects = [o for o in collection.objects if o.type == 'MESH']
    root = next(o for o in collection.objects if o.type == 'EMPTY')
    inverse = root.matrix_world.inverted()
    rig = bpy.data.armatures.new('Creature skeleton')
    arm = bpy.data.objects.new('Creature',rig)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    def bone(n, p, parent=None):
        b=rig.edit_bones.new(n); b.head=p;b.tail=Vector(p)+Vector((0,0,.08))
        if parent: b.parent=rig.edit_bones[parent]
    bone('Body',(0,0,0));bone('Forward',(0,-1,0),'Body');bone('Up',(0,0,1),'Body')
    bone('Head',(0,-.65,.91) if cat else (0,-.11,2.16),'Body')
    if cat:
        for k in range(4):
            side=1 if k%2 else -1
            bone('Leg'+str(k),(side*.25,-.42 if k<2 else .66,.70),'Body')
            bone('Shin'+str(k),(side*.28,-.49 if k<2 else .55,.42),'Leg'+str(k))
        for j,p in enumerate([(.06,.92,.94),(.20,1.36,1.12),(.28,1.70,1.36)]):
            bone('Tail'+str(j),p,'Body' if j==0 else 'Tail'+str(j-1))
    else:
        bone('Cap',(-.11,-.09,2.98),'Head')
        for i,s in enumerate((-1,1)):
            bone('Shoulder'+str(i),(s*.51,-.04,1.83),'Body')
            bone('Elbow'+str(i),(s*(.91 if i else .78),-.78 if i else -.51,1.69 if i else 1.32),'Body')
    bpy.ops.object.mode_set(mode='OBJECT')
    vs=[];faces=[];uv=[];weights=[];smooth=[]
    def skin(name,p):
        x,y,z=p
        if cat:
            if name.startswith('Continuous'):
                if y>.86 and z>.85:
                    t=unit((y-.86)/.18);j=0 if y<1.4 else 1 if y<1.76 else 2
                    prev='Body' if j==0 else 'Tail'+str(j-1)
                    mix=t if j==0 else unit((y-(1.4 if j==1 else 1.76))/.10)
                    return [(prev,1-mix),('Tail'+str(j),mix)]
                if z<.78 and (y<-.27 or y>.24):
                    k=(1 if x>0 else 0)+(2 if y>.24 else 0);t=unit((.78-z)/.22);shin=unit((.50-z)/.15)
                    return [('Body',1-t),('Leg'+str(k),t*(1-shin)),('Shin'+str(k),t*shin)]
                t=unit((-.56-y)/.24)
                return [('Body',1-t),('Head',t)]
            return [('Body' if any(q in name for q in ('collar','ell','Tag keeper')) else 'Head',1)]
        i=1 if x>0 else 0
        if name.startswith('Shaped sleeve'):
            a=Vector(((-1,1)[i]*(.88 if i else .84),-.19,1.55));t=unit((-.17-y)/(.52 if i else .30))
            return [('Shoulder'+str(i),1-t),('Elbow'+str(i),t)]
        if any(q in name for q in ('gauntlet','Mitten','fingers','Thumb')):return [('Elbow'+str(i),1)]
        if name.startswith(('Folded velvet cap','Fluffy pompom','Pompom tuft')):
            t=unit((-x-.18)/.23);return [('Head',1-t),('Cap',t)]
        if name.lower().startswith(('head','ear','cheek','deep lid','warm sclera','amber iris','pupil','eye glint','swept brow','lower eyelid','nose','broad cold','nostril','beard','carved curling','handlebar','hat brim','brim fleece','holly')):return [('Head',1)]
        return [('Body',1)]
    for obj in objects:
        # The stored sculpt has subdivision and dense rounded trim. Reduce each visible part before joining.
        for mod in obj.modifiers:
            if mod.type=='SUBSURF':mod.levels=min(1,mod.levels)
        dec=obj.modifiers.new('Runtime mesh reduction','DECIMATE')
        dec.ratio=.28 if obj.name.startswith('Continuous') else .24
        dg=bpy.context.evaluated_depsgraph_get();eo=obj.evaluated_get(dg);me=eo.to_mesh();me.calc_loop_triangles()
        matrix=inverse@obj.matrix_world;offset=len(vs)
        for v in me.vertices:
            p=matrix@v.co;vs.append(p);weights.append(skin(obj.name,p))
        tinted=me.color_attributes.get('Tabby')
        for tri in me.loop_triangles:
            inds=tuple(tri.vertices);faces.append(tuple(offset+i for i in inds));smooth.append(me.polygons[tri.polygon_index].use_smooth)
            if tinted:color=tuple(sum(tinted.data[i].color[c] for i in inds)/3 for c in range(3))
            else:color=me.materials[tri.material_index].diffuse_color
            uv.append(swatch(color))
        eo.to_mesh_clear()
    name='ChristmasTabby' if cat else 'ChristmasSanta'
    me=bpy.data.meshes.new(name);me.from_pydata(vs,[],faces);me.update()
    layer=me.uv_layers.new(name='Palette')
    for f,p in enumerate(me.polygons):
        p.use_smooth=smooth[f]
        for l in p.loop_indices:layer.data[l].uv=uv[f]
    obj=bpy.data.objects.new(name,me);bpy.context.scene.collection.objects.link(obj)
    obj.parent=arm;groups={b.name:obj.vertex_groups.new(name=b.name) for b in rig.bones}
    for i,ws in enumerate(weights):
        for n,w in ws:
            if w>.0001:groups[n].add([i],w,'REPLACE')
    mod=obj.modifiers.new('Skeleton','ARMATURE');mod.object=arm
    bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(out/(name+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
    print(name,len(faces),'triangles',len(rig.bones),'bones')
# PNG rows run top to bottom while texture coordinates run bottom to top.
pixels=[(64,64,64)]*4096
for c,i in colors.items():pixels[(63-i//64)*64+i%64]=c
raw=b''.join(b'\0'+bytes(ch for c in pixels[y*64:(y+1)*64] for ch in c) for y in range(64))
def chunk(k,b):return struct.pack('>I',len(b))+k+b+struct.pack('>I',zlib.crc32(k+b)&0xffffffff)
(out/'ChristmasCreaturePalette.png').write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>2I5B',64,64,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))
