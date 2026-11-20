import bpy,sys,struct,bmesh
bpy.ops.wm.read_factory_settings(use_empty=True)
a=sys.argv[sys.argv.index('--')+1:];src,out=a[0],a[1]
L=60.0;cx=-2.2;cy=-15.0  # boat centre on the ground, length ~60 mm
for n,target in (('white',2600),('red',2000)):
    bpy.ops.wm.stl_import(filepath=f"{src}/{n}.stl")
    o=bpy.context.selected_objects[0];bpy.context.view_layer.objects.active=o
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.remove_doubles(threshold=0.01);bpy.ops.object.mode_set(mode='OBJECT')
    r=o.modifiers.new('r','REMESH');r.mode='VOXEL';r.voxel_size=0.45;bpy.ops.object.modifier_apply(modifier='r')
    m=o.modifiers.new('d','DECIMATE');m.ratio=min(1,target/len(o.data.polygons))
    bpy.ops.object.modifier_apply(modifier='d')
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=bm.faces);bm.to_mesh(o.data);bm.free()
    me=o.data
    with open(f"{out}/Benchy{n.capitalize()}.bytes","wb") as f:
        f.write(struct.pack('<ii',len(me.vertices),len(me.polygons)))
        for v in me.vertices:
            bx,by,bz=v.co
            # Blender (X bow, Z up) -> Unity (Z bow, Y up, X right); 1 unit = boat length.
            f.write(struct.pack('<fff',-(by-cy)/L,bz/L,(bx-cx)/L))
        for p in me.polygons:
            i=p.vertices;f.write(struct.pack('<iii',i[0],i[2],i[1]))  # handedness flip reverses winding
    print("OUT",n,len(me.vertices),len(me.polygons))
