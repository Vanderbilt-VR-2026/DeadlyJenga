"""Run with Blender --background --python Art/Avatars/build_avatar.py.
Rebuilds only the generated SimpleVRAvatar files, not existing gameplay assets.
"""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Art/Avatars'
OUT = ROOT / 'Assets/Avatars/SimpleVRAvatar'
ART.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.name = 'Avatar — editable source'
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
collection = bpy.data.collections.new('Avatar_Model')
scene.collection.children.link(collection)

def move_collection(obj, destination):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    destination.objects.link(obj)

def material(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = .32
    return m

base = material('PlayerColor', (.035, .43, .8))
root = bpy.data.objects.new('SimpleVRAvatar', None)
collection.objects.link(root)
root['description'] = 'Meters; blank head, torso and wrist-pivot mitten hands. No legs or skeleton.'
root['tracking'] = 'Head pivot at approximate eye center; hands at wrists. Controller offsets are calibrated in Unity.'

def finish(obj, name, origin):
    obj.name = name
    move_collection(obj, collection)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    scene.cursor.location = origin
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    obj.parent = root
    obj.data.materials.clear()
    obj.data.materials.append(base)
    for p in obj.data.polygons: p.use_smooth = True
    obj.select_set(False)
    return obj

def ellipsoid(location, scale, segments=32, rings=20):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.scale = scale
    return obj

# The view direction is Blender -Y. The head pivot is forward of the skull center.
head = finish(ellipsoid((0, 0, 1.63), (.185, .175, .19)), 'Head', (0, -.075, 1.63))

# Rounded, closed torso with broad shoulders and a gently narrowed floating base.
profile = [(.86,.015,.012),(.875,.065,.05),(.90,.12,.083),(.95,.15,.105),
           (1.03,.16,.118),(1.12,.18,.132),(1.22,.212,.142),(1.30,.23,.142),
           (1.35,.222,.135),(1.39,.18,.115),(1.42,.12,.086),(1.44,.058,.054),
           (1.465,.045,.043),(1.48,.012,.012)]
verts=[]; faces=[]; n=32
for z,rx,ry in profile:
    for i in range(n):
        a=2*math.pi*i/n
        verts.append((rx*math.cos(a),ry*math.sin(a),z))
for j in range(len(profile)-1):
    for i in range(n):
        a=j*n+i; b=j*n+(i+1)%n
        faces.append((a,b,b+n,a+n))
faces.append(tuple(reversed(range(n))))
faces.append(tuple((len(profile)-1)*n+i for i in range(n)))
mesh=bpy.data.meshes.new('TorsoMesh'); mesh.from_pydata(verts,[],faces); mesh.update()
torso=bpy.data.objects.new('Torso',mesh); collection.objects.link(torso)
bpy.ops.object.select_all(action='DESELECT');torso.select_set(True);bpy.context.view_layer.objects.active=torso
mod=torso.modifiers.new('Rounded silhouette','SUBSURF');mod.levels=1
bpy.ops.object.modifier_apply(modifier=mod.name)
torso=finish(torso,'Torso',(0,0,1.18))

# A fused palm + thumb gives a clear hand silhouette without fingers or rigging.
hands=[]
for side,name in [(1,'LeftHand'),(-1,'RightHand')]:
    wrist=Vector((side*.34,-.055,1.20))
    components=[]
    for offset,scale in [((0,-.076,0),(.053,.086,.035)),
                         ((-side*.047,-.067,-.01),(.027,.047,.027)),
                         ((0,-.008,0),(.033,.035,.027))]:
        obj=ellipsoid(wrist+Vector(offset),scale,24,16)
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        components.append(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for o in components:o.select_set(True)
    bpy.context.view_layer.objects.active=components[0]
    bpy.ops.object.join(); hand=bpy.context.object
    remesh=hand.modifiers.new('Seamless mitten','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.007
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    smooth=hand.modifiers.new('Soft surface','SMOOTH');smooth.factor=1.0;smooth.iterations=4
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    decimate=hand.modifiers.new('Mobile mesh','DECIMATE');decimate.ratio=.28
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    hands.append(finish(hand,name,wrist))

meshes=[head,torso,*hands]
stats={}
for obj in meshes:
    obj.data.calc_loop_triangles()
    stats[obj.name]={'vertices':len(obj.data.vertices),'triangles':len(obj.data.loop_triangles)}
    assert all(math.isfinite(v) for vert in obj.data.vertices for v in vert.co)
    assert all(abs(v-1)<1e-6 for v in obj.scale)
stats['total_triangles']=sum(v['triangles'] for v in stats.values())
assert stats['total_triangles']<10000
(ART/'mesh_stats.json').write_text(json.dumps(stats,indent=2)+'\n')
bpy.ops.object.select_all(action='DESELECT')
for obj in [root,*meshes]:obj.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=str(OUT/'SimpleVRAvatar.fbx'),use_selection=True,
    object_types={'MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',axis_up='Y',bake_space_transform=True,add_leaf_bones=False,
    bake_anim=False,use_custom_props=True,mesh_smooth_type='FACE')

# A separate presentation scene demonstrates the same geometry in three colors.
preview=bpy.data.scenes.new('ColorPreview')
bpy.context.window.scene=preview
preview.world=bpy.data.worlds.new('Studio World');preview.world.use_nodes=True
preview.world.node_tree.nodes['Background'].inputs[0].default_value=(.055,.075,.11,1)
preview.world.node_tree.nodes['Background'].inputs[1].default_value=.35
colors=[('Sunshine',(.95,.57,.025)),('Ocean',(.035,.43,.8)),('Orchid',(.55,.06,.65))]
for index,(name,color) in enumerate(colors):
    mat=material(name,color)
    for src in meshes:
        obj=src.copy();obj.data=src.data.copy();obj.parent=None
        preview.collection.objects.link(obj)
        obj.name=name+'_'+src.name
        obj.location=src.location+Vector(((index-1)*.95,0,0))
        obj.data.materials.clear();obj.data.materials.append(mat)
        if src.name=='RightHand' and index==2:
            obj.location.z+=.28
            obj.rotation_euler.x=math.radians(-60)

floor_mat=material('StudioFloor',(.065,.09,.135))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,.69))
floor=bpy.context.object;floor.name='Studio floor';floor.data.materials.append(floor_mat)
def aim(obj,point):obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size in [('Key',(-3,-4,6),650,4),('Fill',(4,-2,3),450,3),('Rim',(0,3,4),850,3)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    obj=bpy.data.objects.new(name,data);preview.collection.objects.link(obj);obj.location=loc;aim(obj,(0,0,1.3))
camera=bpy.data.objects.new('Preview Camera',bpy.data.cameras.new('Preview Camera'))
preview.collection.objects.link(camera);camera.location=(2.1,-6.5,2.7);aim(camera,(0,0,1.29))
camera.data.type='ORTHO';camera.data.ortho_scale=3.55;preview.camera=camera
preview.render.engine='CYCLES';preview.cycles.samples=24
preview.cycles.use_denoising=True
preview.render.resolution_x=1500;preview.render.resolution_y=900;preview.render.resolution_percentage=100
preview.render.image_settings.file_format='PNG';preview.render.filepath=str(ART/'AvatarPreview.png')
preview.view_settings.view_transform='AgX'
bpy.ops.render.render(write_still=True)

# Open the editable single avatar, with useful material colors and framing.
bpy.context.window.scene=scene
scene.cursor.location=(0,0,0)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.region_3d.view_location=(0,0,1.3)
            area.spaces.active.region_3d.view_distance=2.4
            area.spaces.active.region_3d.view_rotation=Quaternion((.866,.5,0,0))
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'SimpleVRAvatar.blend'))
print('AVATAR_COMPLETE',json.dumps(stats))
