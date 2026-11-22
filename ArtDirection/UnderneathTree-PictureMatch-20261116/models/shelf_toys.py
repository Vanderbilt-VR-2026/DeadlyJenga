"""Sculpt and export the sixteen small gift-shelf collectibles in toy-local coordinates."""
import math
from pathlib import Path

import bpy
from mathutils import Vector

CREAM, WHITE, NAVY, BLACK = "Toy Cream", "Toy White", "Toy Navy", "Toy Black"
GOLD, RED, TEAL, PINK = "Toy Gold", "Toy Red", "Toy Teal", "Toy Pink"
WOOD, BROWN, BLUE, SKIN = "Toy Wood", "Toy Brown", "Toy Blue", "Toy Skin"
ORANGE, FROST, GREEN, PURPLE = "Toy Orange", "Toy Frost", "Toy Green", "Toy Purple"
parts = []


def part(obj, color):
    obj.data.materials.append(bpy.data.materials[color])
    parts.append(obj)
    return obj


def solid(shape, pos, size, color, bevel=0, rotation=(0, 0, 0)):
    if shape == "box":
        bpy.ops.mesh.primitive_cube_add(size=1)
    elif shape == "oval":
        bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=.5)
    else:
        bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=.5, depth=1)
    obj = bpy.context.object
    for vert in obj.data.vertices:
        vert.co = tuple(vert.co[i] * size[i] for i in range(3))
    if shape == "oval":
        for face in obj.data.polygons:
            face.use_smooth = True
    obj.location = pos
    obj.rotation_euler = tuple(math.radians(a) for a in rotation)
    if bevel:
        edge = obj.modifiers.new("Molded edge", "BEVEL")
        edge.width, edge.segments = bevel, 3
        obj.modifiers.new("Face normals", "WEIGHTED_NORMAL")
    return part(obj, color)


def box(pos, size, color, bevel=.05, rotation=(0, 0, 0)):
    return solid("box", pos, size, color, bevel, rotation)


def oval(pos, size, color, rotation=(0, 0, 0)):
    return solid("oval", pos, size, color, rotation=rotation)


def drum(pos, size, color, rotation=(0, 0, 0)):
    return solid("drum", pos, size, color, .035, rotation)


def rod(a, b, radius, color):
    a, b = Vector(a), Vector(b)
    o = drum((a+b)/2, (2*radius, 2*radius, (b-a).length), color)
    o.rotation_euler = (b-a).to_track_quat("Z", "Y").to_euler()


def letters(pos, word, size, color, flat=False):
    curve = bpy.data.curves.new(word, "FONT")
    curve.body, curve.size, curve.align_x, curve.align_y = word, size, "CENTER", "CENTER"
    curve.extrude = .012
    o = bpy.data.objects.new(word, curve)
    bpy.context.collection.objects.link(o)
    o.location, o.rotation_euler.x = pos, 0 if flat else math.pi/2
    part(o, color)


def profile(rings, color, sides=32, flutes=0):
    # Each horizontal ring is (height, horizontal radii, center x, center y).
    vertices = []
    for z, rx, ry, x, y in rings:
        for i in range(sides):
            a = math.tau*i/sides
            r = 1 + flutes*math.cos(8*a)
            vertices.append((x+rx*r*math.cos(a), y+ry*r*math.sin(a), z))
    faces = [tuple(reversed(range(sides)))]
    for row in range(len(rings)-1):
        for i in range(sides):
            j = (i+1)%sides
            faces.append((row*sides+i,row*sides+j,(row+1)*sides+j,(row+1)*sides+i))
    faces.append(tuple(range(len(vertices)-sides,len(vertices))))
    mesh = bpy.data.meshes.new("Formed shell")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    o = bpy.data.objects.new("Formed shell", mesh)
    bpy.context.collection.objects.link(o)
    for face in mesh.polygons[1:-1]:
        face.use_smooth = True
    return part(o, color)


def face(x, y, z, gap, scale):
    for side in (-1, 1):
        oval((x+side*gap,y,z), (.24*scale,.13*scale,.28*scale), BLACK)
        oval((x+side*gap+.035*scale,y-.06*scale,z+.05*scale), (.065*scale,)*3, WHITE)
    oval((x,y-.06*scale,z-.27*scale), (.23*scale,.15*scale,.22*scale), SKIN)


def house():
    # Alternate the courses half a diameter so their projecting ends interlock.
    for i in range(8):
        z=.40+.67*i
        for x in (-3.25,3.25):
            rod((x,-3.67,z),(x,3.67,z),.43,WOOD if i%3==1 else BROWN)
        for y in (-2.91,2.91):
            runs=[(-4.08,4.08)] if y>0 or i>5 else [(-4.08,-.96),(.96,4.08)]
            for a,b in runs:rod((a,y,z+.32),(b,y,z+.32),.43,BROWN if i%3==1 else WOOD)
    for j in range(5):
        w=3.19-j*.57
        for y in (-2.92,2.92):rod((-w,y,5.79+j*.48),(w,y,5.79+j*.48),.31,WOOD)
    for s in (-1,1):
        for j in range(8):
            box((s*1.93,-3.26+j*.94,7.21),(4.72,.92,.31),GREEN,.065,(0,s*33,0))
        rod((s*3.90,-3.78,5.99),(0,-3.78,8.54),.18,RED)
    rod((0,-3.83,8.52),(0,3.88,8.52),.19,RED)
    box((0,-2.87,2.38),(1.74,.25,4.04),TEAL,.10)
    oval((.60,-3.05,2.24),(.25,)*3,GOLD)
    for s in (-1,1):
        box((s*2.05,-3.34,3.49),(1.50,.18,1.60),CREAM)
        box((s*2.05,-3.46,3.49),(1.20,.13,1.27),NAVY)
        box((s*2.05,-3.57,3.49),(.12,.09,1.30),CREAM)
        box((s*2.05,-3.57,3.49),(1.23,.09,.12),CREAM)


def superhero():
    drum((0,0,.25),(3.42,3.07,.5),NAVY)
    for s in (-1,1):
        rod((s*.57,0,.74),(s*.46,.03,3.37),.39,BLUE)
        oval((s*.62,-.22,.89),(.98,1.36,1.1),RED)
        oval((s*.50,-.03,2.1),(.82,.83,.65),NAVY)
        oval((s*1.03,-.02,5.30),(1.12,1.05,1.12),RED)
        rod((s*1.26,0,5.13),(s*1.55,-.19,4.05),.35,BLUE)
        oval((s*1.58,-.23,3.98),(.83,.84,.94),GOLD)
    profile([(3.05,.75,.57,0,0),(3.55,.83,.63,0,0),(5.36,1.22,.76,0,0),(5.68,.76,.55,0,0)],RED)
    profile([(1.7,1.52,.30,0,.84),(3.5,1.33,.31,0,.77),(5.44,.98,.29,0,.38)],GOLD,28,.10)
    box((0,-.69,4.87),(.70,.14,.70),GOLD,.06,(0,45,0))
    letters((0,-.79,4.88),"V",.53,NAVY)
    oval((0,-.04,6.42),(1.53,1.33,1.81),SKIN)
    oval((0,.02,6.94),(1.60,1.38,.92),BLUE)
    box((0,-.64,6.56),(1.39,.20,.38),NAVY,.12)
    for s in (-1,1):
        oval((s*.35,-.75,6.57),(.35,.10,.15),WHITE)
    drum((0,0,3.45),(1.78,1.45,.30),GOLD)


def bricks():
    for s in (-1,1):
        rail=[(-3.22,.91),(-2.90,.50),(-1.86,.16),(0,.04),(1.86,.16),(2.90,.50),(3.22,.91),
              (3.10,1.08),(2.70,.82),(1.75,.52),(0,.39),(-1.75,.52),(-2.70,.82),(-3.10,1.08)]
        panel("Bentwood rocker",rail,.30,RED).location.x=s*.96
        for y in (-1.16,1.16):
            rod((s*.93,y*1.44,.55),(s*.66,y*.86,2.70),.19,WOOD)
    for y in (-1.72,1.72):rod((-.96,y,.59),(.96,y,.59),.13,GOLD)
    silhouette=[(-2.95,3.93),(-2.70,3.62),(-2.29,3.65),(-1.83,3.97),(-1.67,3.06),
                (-1.33,2.40),(-.38,2.18),(1.24,2.34),(1.78,2.74),(1.85,3.26),
                (1.41,3.63),(.37,3.61),(-.47,3.53),(-.94,4.36),(-1.48,4.91),
                (-1.55,5.46),(-1.86,5.10),(-2.16,5.36),(-2.13,4.81),(-2.48,4.49)]
    panel("Carved horse",silhouette,1.16,WOOD,.16)
    oval((0,.36,3.52),(1.60,1.78,.45),TEAL)
    for s in (-1,1):
        oval((s*.60,-1.95,4.56),(.14,.24,.23),BLACK)
        oval((s*.60,-2.65,4.03),(.11,.18,.12),BROWN)
        for i in range(6):oval((s*.42,-1.29+i*.14,4.64-i*.19),(.46,.42,.57),CREAM)
        box((s*.61,-2.29,4.16),(.12,.20,.76),RED,.055,(-35,0,0))
        oval((s*.61,.40,3.19),(.25,.86,.92),TEAL)
        oval((s*.74,.40,3.19),(.08,.48,.48),GOLD)
    for j in range(3):oval(((j-1)*.22,1.91,2.69),(.32,.53,1.35),CREAM,(-25,0,0))
    rod((-.97,-1.60,4.19),(.97,-1.60,4.19),.12,RED)


def football():
    # A truncated icosahedron has twelve pentagons and twenty hexagons.
    import bmesh
    bm=bmesh.new()
    bmesh.ops.create_icosphere(bm,subdivisions=1,radius=1)
    for f in bm.faces:
        vs=[v.co.copy() for v in f.verts]
        panel=[]
        for a,b in zip(vs,vs[1:]+vs[:1]):
            panel += [(2*a+b).normalized(),(a+2*b).normalized()]
        ball_panel(panel,CREAM)
    for v in bm.verts:
        n=v.co.normalized()
        e=n.orthogonal().normalized()
        t=n.cross(e)
        panel=sorted([(2*v.co+edge.other_vert(v).co).normalized() for edge in v.link_edges],key=lambda p:math.atan2(p.dot(t),p.dot(e)))
        ball_panel(panel,NAVY)
    bm.free()
    oval((0,0,3),(5.58,)*3,BROWN)


def ball_panel(points,color):
    n=sum(points,Vector())/len(points)
    verts=[(0,0,0)]
    rings=3
    for i in range(1,rings+1):
        for p in points:
            v=n.lerp(p,.973*i/rings).normalized()*3.035
            verts.append((v.x,v.y,v.z+3.035))
    peak=n.normalized()*3.05;verts[0]=(peak.x,peak.y,peak.z+3.035)
    count=len(points)
    faces=[(0,1+i,1+(i+1)%count) for i in range(count)]
    for row in range(rings-1):
        for i in range(count):
            a=1+row*count+i;b=1+row*count+(i+1)%count
            faces.append((a,a+count,b+count,b))
    mesh=bpy.data.meshes.new("Stitched panel");mesh.from_pydata(verts,[],faces);mesh.update()
    o=bpy.data.objects.new("Leather panel",mesh);bpy.context.collection.objects.link(o)
    for f in mesh.polygons:
        if Vector(f.normal).dot(f.center-Vector((0,0,3.035)))<0:f.flip()
        f.use_smooth=True
    part(o,color)


def pinecone():
    oval((0,0,3.57),(2.66,2.66,6.86),BROWN)
    for row in range(8):
        r=.42+1.25*math.sin(math.pi*(row+.67)/8.8)
        for j in range(9):
            a=j*math.tau/9+row*.33;z=.30+row*.79
            # Each bract folds along a ridge into an outward-pointing tip.
            oval((r*math.cos(a),r*math.sin(a),z+.22),(.93,.82,1.09),WOOD if row%2 else BROWN,(0,34,math.degrees(a)))
            coords=[(r-.53,-.28,z+.18),(r-.53,.28,z+.18),(r+.25,.48,z+.29),
                    (r+.62,0,z+.82),(r+.25,-.48,z+.29),(r+.32,0,z+.48)]
            verts=[(x*math.cos(a)-y*math.sin(a),x*math.sin(a)+y*math.cos(a),h) for x,y,h in coords]
            mesh=bpy.data.meshes.new("Carved scale");mesh.from_pydata(verts,[],[(0,1,2,5),(2,3,5),(3,4,5),(4,0,5),(0,4,3,2,1)]);mesh.update()
            o=bpy.data.objects.new("Frosted scale",mesh);bpy.context.collection.objects.link(o);part(o,WOOD if row%2 else BROWN)
            o.data.materials.append(bpy.data.materials[FROST])
            if (row+j)%3:
                tip=Vector(verts[3])
                left=tip.lerp(Vector(verts[2]),.40)
                right=tip.lerp(Vector(verts[4]),.40)
                ridge=tip.lerp(Vector(verts[5]),.46)
                snow=bpy.data.meshes.new("Snow tip");snow.from_pydata([v+Vector((0,0,.013)) for v in (tip,left,ridge,right)],[],[(0,1,2),(0,2,3)]);snow.update()
                cap=bpy.data.objects.new("Snow tip",snow);bpy.context.collection.objects.link(cap);part(cap,FROST)
    rod((0,0,6.25),(.24,0,7.28),.17,WOOD)
    for s in (-1,1):oval((s*.41,0,6.65),(1.14,.36,.27),GREEN,(0,-s*27,0))
    oval((0,-.28,6.67),(.43,)*3,RED)


def stocking():
    # Contoured twin fabric panels; the stuffed toe bends at the heel.
    outline=[(-1.31,-3.71),(1.27,-3.71),(1.16,-2.96),(.96,-1.52),(.76,-.15),
             (.85,.63),(1.41,1.14),(2.41,1.47),(2.89,1.92),(2.98,2.47),
             (2.67,2.95),(2.04,3.25),(1.15,3.35),(.08,3.14),(-.87,2.76),
             (-1.56,2.33),(-1.79,1.68),(-1.72,.87),(-1.50,-.43),(-1.42,-2.12)]
    # Rings bulge toward the center and meet in a narrow welt around the edge.
    rings=[(.03,.84),(.12,.97),(.25,1),(.40,.98),(.57,.88),(.69,.55)]
    verts=[(x*s,y*s,z+.06*math.sin(2*x+y)) for z,s in rings for x,y in outline]
    n=len(outline);faces=[tuple(reversed(range(n)))]
    for k in range(len(rings)-1):
        for i in range(n):
            a=k*n+i;b=k*n+(i+1)%n;faces.append((a,b,b+n,a+n))
    faces.append(tuple(range(len(verts)-n,len(verts))))
    m=bpy.data.meshes.new("Sewn stocking");m.from_pydata(verts,[],faces);m.update()
    o=bpy.data.objects.new("Velvet stocking",m);bpy.context.collection.objects.link(o);part(o,"Stocking velvet")
    for f in m.polygons:f.use_smooth=True
    for i in range(n):
        x,y=outline[i];u,v=outline[(i+1)%n]
        rod((x,y,.27),(u,v,.27),.036,RED)
    box((0,-3.19,.49),(2.96,1.27,.88),"Stocking fleece",.32,(0,0,-3))
    oval((0,-3.79,.52),(2.30,.17,.39),BROWN)
    for i in range(14):oval((-1.31+i*.20,-3.34,.77),(.22,.80,.32),"Stocking fleece")
    # Raised chevron knit and ribbed fleece read on the exported mesh under the tree lights.
    for row in range(8):
        y=-2.36+row*.37
        for col in range(5):
            x=-.85+col*.31
            if abs(x+.25)<.65 and abs(y+.53)<.68:continue
            z=.67+.045*math.sin(2*x+y)
            for sign in (-1,1):rod((x+sign*.074,y-.075,z),(x,y+.064,z+.018),.026,"Stocking velvet")
    for i in range(12):
        oval((-1.29+i*.23,-3.17,.91),(.24,1.14,.19),"Stocking fleece")
    for a in range(6):
        ang=a*math.tau/6
        rod((-.25,-.53,.72),(-.25+.59*math.cos(ang),-.53+.59*math.sin(ang),.70),.042,CREAM)
    for j in range(14):
        a=j*math.tau/14;b=(j+1)*math.tau/14
        rod((-1.36+.35*math.cos(a),-3.55+.56*math.sin(a),.38),(-1.36+.35*math.cos(b),-3.55+.56*math.sin(b),.38),.082,RED)


def puzzle():
    colors=[RED,ORANGE,BLUE,GREEN,"Toy Yellow",WHITE]
    for i in (-1,0,1):
        for j in (-1,0,1):
            for k in (-1,0,1):
                if (i,j,k)!=(0,0,0):
                    box((i*1.68,j*1.68,2.52+k*1.68),(1.64,)*3,BLACK,.14)
    # Round the tile in its own plane before turning it onto the cube face.
    for axis,rot in enumerate([(0,90,0),(90,0,0),(0,0,0)]):
        others=[i for i in range(3) if i!=axis]
        for side in (-1,1):
            for i in (-1,0,1):
                for j in (-1,0,1):
                    p=[0.,0.,0.];p[axis]=side*2.51;p[others[0]]=i*1.68;p[others[1]]=j*1.68;p[2]+=2.52
                    box(p,(1.43,1.43,.10),colors[axis*2+(side>0)],.046,rot)


def fashion():
    drum((0,0,.21),(2.87,2.36,.42),GOLD)
    for s in (-1,1):
        rod((s*.35,0,.56),(s*.34,0,4.61),.21,SKIN)
        oval((s*.36,-.13,.76),(.57,.92,.77),PINK)
    profile([(3.25,1.49,1.16,0,0),(3.47,1.52,1.18,0,0),(5.19,.75,.59,0,0),(6.24,.47,.39,0,0),(7.48,.69,.48,0,0),(7.62,.46,.33,0,0)],PINK,48,.10)
    profile([(3.33,1.55,1.22,0,0),(3.49,1.54,1.21,0,0)],CREAM,48,.10)
    drum((0,0,6.26),(1.06,.94,.21),GOLD)
    for s in (-1,1):
        rod((s*.65,0,7.36),(s*.92,-.08,6.65),.19,SKIN)
        rod((s*.92,-.08,6.65),(s*(1.29 if s==1 else .92),-.32,7.16 if s==1 else 5.75),.16,SKIN)
        oval((s*.67,0,7.46),(.58,.65,.61),PINK)
    rod((0,0,7.42),(0,0,8.50),.24,SKIN)
    oval((0,-.03,8.89),(1.43,1.30,1.64),SKIN)
    oval((0,.29,9.30),(1.58,1.38,1.24),GOLD)
    for s in (-1,1):
        oval((s*.63,.13,8.51),(.57,.89,1.73),GOLD,(0,s*10,0))
        oval((s*.65,-.28,8.48),(.21,)*3,PINK)
    face(0,-.631,9.02,.28,.84)
    oval((0,-.674,8.68),(.33,.075,.10),RED)
    for j in range(3):oval((-.33+j*.32,-.38,9.71+(.18 if j==1 else 0)),(.31,.28,.47),GOLD)

def nutcracker():
    for s in (-1,1):
        drum((s*.68,0,3.51),(1.03,1.07,3.52),CREAM)
        oval((s*.68,-.22,.75),(1.25,1.78,1.45),BLACK)
        drum((s*.68,0,1.54),(1.20,1.22,1.54),BLACK)
        drum((s*.68,0,2.27),(1.23,1.27,.18),GOLD)
    profile([(4.94,1.42,1.15,0,0),(5.28,1.56,1.23,0,0),(6.25,1.38,1.14,0,0),(8.99,1.58,1.24,0,0),(9.46,1.20,1.02,0,0)],RED)
    drum((0,0,6.06),(3.04,2.46,.48),BLACK)
    box((0,-1.25,6.06),(.74,.20,.47),GOLD)
    for j in range(3):
        z=6.93+j*.67
        rod((-.74,-1.06,z+.09),(0,-1.25,z-.11),.065,GOLD)
        rod((0,-1.25,z-.11),(.74,-1.06,z+.09),.065,GOLD)
        for s in (-1,1):oval((s*.77,-1.07,z+.08),(.22,)*3,GOLD)
    for s in (-1,1):
        drum((s*1.73,0,7.25),(.85,.87,3.32),RED)
        drum((s*1.73,0,8.93),(1.12,1.10,.42),GOLD)
        drum((s*1.73,0,5.83),(.93,.94,.52),NAVY)
        oval((s*1.73,-.05,5.39),(.86,.88,.78),SKIN)
    drum((0,0,10.73),(2.54,2.39,2.63),SKIN)
    for s in (-1,1):
        oval((s*1.14,0,10.21),(.72,1.16,1.67),CREAM)
        oval((s*.58,-1.12,10.51),(.84,.31,.31),BLACK,(0,-s*18,0))
    box((0,-1.15,9.92),(1.49,.36,.65),BROWN,.12)
    for i in range(5):box((-.53+i*.265,-1.36,10.03),(.236,.09,.27),WHITE,.03)
    face(0,-1.16,11.06,.49,1.25)
    drum((0,0,12.95),(2.83,2.75,2.64),NAVY)
    for z in (11.72,14.22):drum((0,0,z),(2.93,2.85,.21),GOLD)
    oval((0,-1.35,13.11),(.79,.21,.94),RED)
    box((0,-1.48,13.11),(.44,.10,.44),GOLD,.03,(0,45,0))
    oval((.12,.0,14.60),(.68,.70,1.0),RED,(0,13,0))


def blaster():
    # A 1950s tin space-toy silhouette with finned back and a candy-colored comet on each side.
    panel("Swept grip",[(.55,2.43),(1.67,2.61),(1.94,2.19),(2.36,.57),(2.15,.22),
                        (1.41,.20),(1.14,.51),(.83,1.49),(.36,1.94)],.89,RED,.16)
    for s in (-1,1):
        for j in range(4):
            rod((s*.46,1.22+j*.10,1.45-j*.21),(s*.46,1.86+j*.09,1.52-j*.21),.042,GOLD)
    oval((0,.13,2.93),(2.06,5.39,1.87),RED)
    # A clear captive-marble chamber extends beyond the enamel body.
    drum((0,-2.98,2.92),(1.36,1.36,1.70),"Cellophane",(90,0,0))
    for i in range(4):
        a=i*math.pi/2+math.pi/4
        x=.69*math.cos(a);z=2.92+.69*math.sin(a)
        rod((x,-2.16,z),(x,-3.85,z),.073,"Toy Silver")
    for i,c in enumerate((PINK,"Toy Yellow",TEAL)):
        oval((0,-2.47-i*.48,2.92),(.59,.50,.59),c)
    for y,r in [(-2.19,1.68),(-3.85,1.57),(-4.06,1.30)]:
        drum((0,y,2.91),(r,r,.17),TEAL,(90,0,0))
    oval((0,-4.23,2.92),(1.01,.57,1.01),ORANGE)
    for a,b in [((0,-1.27,2.28),(0,-.94,1.54)),((0,-.94,1.54),(0,-.24,1.14)),((0,-.24,1.14),(0,1.23,1.02))]:
        rod(a,b,.13,GOLD)
    # A dial occupies the broad flank of the toy; raised ticks catch the map lights.
    for side in (-1,1):
        drum((side*.94,.93,3.06),(.98,.98,.19),GOLD,(0,90,0))
        drum((side*1.05,.93,3.06),(.79,.79,.09),CREAM,(0,90,0))
        for tick in range(7):
            angle=.22+tick*.46
            yy=.93+.30*math.cos(angle);zz=3.06+.30*math.sin(angle)
            oval((side*1.11,yy,zz),(.035,.072,.072),NAVY)
        rod((side*1.12,.93,3.04),(side*1.12,1.10,3.29),.042,RED)
    for s in (-1,1):
        o=panel("Rocket fin",[(.93,3.04),(1.71,3.21),(2.75,3.91),(2.61,3.02),(1.94,2.73)],.16,TEAL)
        o.location.x=s*.74
        oval((s*1.01,-.09,2.96),(.17,1.79,.75),NAVY)
        oval((s*1.12,-.49,2.96),(.075,.40,.40),GOLD)
        for j in range(3):
            rod((s*1.11,-.26,2.83+j*.11),(s*1.11,.46-j*.13,2.84+j*.11),.043,CREAM)
    o=panel("Dorsal tail",[(.75,3.53),(1.40,3.83),(2.44,4.41),(2.36,3.42)],.23,RED)
    for i in range(3):oval((0,-.70+i*.42,3.82),(.32,.36,.22),TEAL)


def candy():
    film="Cellophane"
    profile([(.12,1.91,1.55,0,0),(.45,2.24,1.81,0,0),(.77,2.28,1.87,0,0)],RED,32,.045)
    profile([(.71,2.28,1.87,0,0),(1.32,2.48,1.99,0,0),(3.76,2.35,1.90,0,0),(4.78,1.66,1.34,0,0),(5.32,.68,.56,0,0)],film,32,.05)
    profile([(5.21,.66,.53,0,0),(5.84,1.12,.89,0,0),(6.30,1.49,1.17,0,0)],film,32,.13)
    for z,x,y,c in [(1.13,-1.22,-.48,PINK),(1.13,.55,-.75,TEAL),(1.32,.84,.65,ORANGE),
                    (2.13,-.89,.62,GREEN),(2.18,-.06,-.94,RED),(2.55,1.16,-.39,PURPLE),
                    (3.16,-1.11,-.59,ORANGE),(3.45,.50,-.43,GREEN),(3.93,-.38,.13,PINK)]:
        oval((x,y,z),(1.22,1.00,1.01),c)
        for s in (-1,1):
            oval((x+s*.67,y,z+s*.08),(.46,.49,.47),CREAM,(0,s*30,0))
        box((x,y-.49,z),(.22,.07,.74),CREAM,.03,(0,-26,0))
    drum((0,0,5.29),(1.69,1.43,.31),GOLD)
    for s in (-1,1):oval((s*.50,-.64,5.33),(1.08,.40,.61),RED,(0,-s*22,0))
    box((1.04,-.97,4.66),(1.29,.16,.76),CREAM,.09,(0,-17,0))
    letters((1.04,-1.08,4.66),"SWEETS",.28,RED)


def rag_doll():
    for s in (-1,1):
        for j in range(7):
            drum((s*.55,-.12,.85+j*.22),(.60,.62,.24),RED if j%2==0 else WHITE)
        oval((s*.55,-.30,.51),(.91,1.32,.83),BLACK)
    profile([(2.05,1.65,1.32,0,0),(2.31,1.72,1.40,0,0),(3.58,.96,.76,0,0),(4.40,.73,.58,0,0)],BLUE,48,.07)
    profile([(2.02,1.64,1.32,0,0),(2.15,1.69,1.37,0,0)],WHITE,48,.08)
    for s in (-1,1):
        oval((s*.94,0,4.01),(.94,.98,.99),BLUE)
        oval((s*1.21,-.11,3.52),(.62,.62,1.14),CREAM,(0,-s*19,0))
        box((s*.45,-.48,4.09),(.20,.17,.83),WHITE,.07,(0,s*13,0))
        for j in range(5):
            h=2.32+j*.24
            oval((s*(1.33-.14*j),-.66,h),(.13,.13,.13),WHITE)
    # Front apron follows the flared skirt; its hem and bib sit proud of every fold.
    rows=[(2.25,1.11,1.52),(2.52,1.08,1.44),(3.12,.92,1.25),(3.63,.67,.94),(4.10,.57,.77)]
    verts=[]
    for h,w,reach in rows:
        for j in range(9):
            u=(j-4)/4
            verts.append((w*u,-reach*(1-.29*u*u),h))
    quads=[(i*9+j,i*9+j+1,(i+1)*9+j+1,(i+1)*9+j) for i in range(4) for j in range(8)]
    apron=bpy.data.meshes.new("Pinafore");apron.from_pydata(verts,[],quads);apron.update()
    bib=bpy.data.objects.new("White pinafore",apron);bpy.context.collection.objects.link(bib);part(bib,WHITE)
    thick=bib.modifiers.new("Cloth edge","SOLIDIFY");thick.thickness=.07
    for j in range(11):
        u=(j-5)/5
        oval((1.07*u,-1.54*(1-.29*u*u),2.25),(.22,.12,.16),WHITE)
    oval((0,-.11,5.52),(2.42,1.85,2.08),CREAM)
    for s in (-1,1):
        drum((s*.44,-.98,5.68),(.32,.32,.15),BLACK,(90,0,0))
        oval((s*.44+.04,-1.055,5.73),(.08,)*3,WHITE)
        oval((s*.73,-.83,5.29),(.38,.12,.22),PINK)
        for j in range(3):
            rod((s*(.32+j*.13),-.99,5.84),(s*(.30+j*.17),-.99,5.96),.025,BROWN)
    nose=panel("Triangle nose",[(-.20,0),(.20,0),(0,-.31)],.13,RED,.035)
    nose.rotation_euler.z=math.pi/2;nose.location=(0,-1.053,5.54)
    for i in range(8):
        x=(i-4)*.107;u=x+.107
        rod((x,-1.010,5.08+.88*x*x),(u,-1.010,5.08+.88*u*u),.030,BLACK)
    # Short lengths of looped scarlet yarn around the cloth face.
    for i in range(25):
        a=math.radians(-28+i*9.8)
        x=1.17*math.cos(a);z=5.54+1.00*math.sin(a)
        oval((x,.08,z),(.49,1.13,.62),RED,(0,90-math.degrees(a),0))
        for j in (-1,1):
            rod((x+j*.063,-.53,z),((x*1.15)+j*.07,-.53,z+(z-5.50)*.19-.08),.052,RED)


def board_game():
    box((0,0,.91),(11.93,5.94,1.82),TEAL,.16)
    box((0,0,1.87),(12.03,6.00,.42),NAVY,.13)
    box((0,0,2.09),(11.57,5.57,.07),GOLD)
    box((0,0,2.14),(11.34,5.34,.07),TEAL)
    for s in (-1,1):
        for j in range(11):box(((j-5)*.98,s*2.21,2.20),(.82,.51,.07),(CREAM,RED,GOLD,BLUE)[(j+(s>0))%4])
        for j in range(3):box((s*5.14,(j-1)*1.04,2.20),(.57,.83,.07),CREAM)
    box((0,0,2.23),(8.8,2.82,.12),NAVY,.21)
    letters((0,.38,2.31),"TINSEL TOWN",1.04,CREAM,True)
    letters((0,-.66,2.31),"THE HOLIDAY PROPERTY GAME",.36,GOLD,True)
    letters((0,-2.99,1.01),"TINSEL TOWN",.73,CREAM)


def clay():
    # Open tub, a finger-impressed clay swirl and its matching lid tipped beside it.
    profile([(.04,1.73,1.73,0,0),(.30,1.80,1.80,0,0),(3.42,2.06,2.06,0,0),(3.59,2.03,2.03,0,0)],"Toy Yellow")
    drum((0,0,3.64),(4.19,4.19,.23),"Toy Yellow")
    drum((0,0,3.76),(3.79,3.79,.085),BROWN)
    oval((0,0,3.76),(3.65,3.58,.79),PINK)
    for j in range(36):
        a=j*.34;r=1.32-j*.034
        b=a+.34;t=r-.034
        oval((r*math.cos(a),r*math.sin(a),3.97+j*.013),(.44,)*3,PINK)
        rod((r*math.cos(a),r*math.sin(a),3.97+j*.013),(t*math.cos(b),t*math.sin(b),3.97+(j+1)*.013),.22,PINK)
    box((0,-1.94,1.99),(3.27,.39,1.79),RED,.19)
    letters((0,-2.16,2.34),"MODELING",.53,CREAM)
    letters((0,-2.17,1.64),"CLAY",.95,WHITE)
    # A fluted snap lid leans against the back edge, exposing its molded underside.
    lid=drum((0,1.84,2.20),(4.18,4.18,.35),RED,(57,0,0))
    rim=drum((0,1.69,2.29),(3.63,3.63,.13),PINK,(57,0,0))
    for j in range(24):
        a=j*math.tau/24
        p=lid.rotation_euler.to_matrix()@Vector((2.04*math.cos(a),2.04*math.sin(a),0))+lid.location
        oval(p,(.18,.18,.22),RED)


def pastry():
    box((0,0,3.96),(5.99,2.2,7.92),TEAL,.14)
    box((0,-1.12,6.71),(5.59,.13,1.92),NAVY,.15)
    letters((0,-1.22,7.02),"POLAR",.78,CREAM)
    letters((0,-1.23,6.28),"POCKETS",.83,CREAM)
    box((0,-1.17,3.60),(3.22,.32,3.92),WOOD,.14,(0,-11,0))
    box((0,-1.36,3.62),(2.71,.12,3.37),PINK,.055,(0,-11,0))
    for j in range(12):
        x=-.85+(j%3)*.84+.13*math.sin(j);z=2.50+(j//3)*.66
        box((x,-1.45,z),(.35,.07,.12),(CREAM,GOLD,TEAL)[j%3],.026,(0,j*31,0))
    box((0,-1.15,.69),(5.59,.13,.87),RED,.08)
    letters((0,-1.24,.70),"BERRY FROST",.60,CREAM)
    for z in (1.67,3.93,6.19):box((3.02,0,z),(.07,1.66,1.51),NAVY,.03)


def walker():
    # A tin penguin with painted bib, webbed feet and a twin-loop winding key.
    for s in (-1,1):oval((s*.76,-.52,.43),(1.44,1.95,.86),ORANGE)
    oval((0,0,2.57),(3.36,2.88,3.99),TEAL)
    oval((0,-1.16,2.36),(2.31,.71,2.65),CREAM)
    for s in (-1,1):
        oval((s*1.51,-.05,2.51),(.65,1.23,2.30),NAVY,(0,-s*14,0))
        oval((s*.51,-1.15,3.54),(.68,.42,.75),CREAM)
        oval((s*.51,-1.37,3.56),(.29,.14,.33),BLACK)
    oval((0,-1.48,3.20),(.91,.75,.43),ORANGE)
    for s in (-1,1):oval((s*.28,-1.38,2.91),(.65,.24,.41),RED,(0,-s*19,0))
    rod((0,.99,2.68),(0,2.23,2.68),.19,GOLD)
    for s in (-1,1):
        bpy.ops.mesh.primitive_torus_add(major_segments=20,minor_segments=8,major_radius=.49,minor_radius=.15,location=(s*.48,2.27,2.72),rotation=(math.pi/2,0,0))
        part(bpy.context.object,GOLD)


def marching_drum():
    drum((0,0,2.49),(6.83,6.83,4.89),RED)
    for z in (.24,4.77):
        drum((0,0,z),(7.24,7.24,.43),NAVY)
        for oz in (-.17,.17):drum((0,0,z+oz),(7.28,7.28,.105),GOLD)
    drum((0,0,5.01),(6.84,6.84,.13),CREAM)
    for i in range(12):
        a=i*math.tau/12;b=(i+.5)*math.tau/12
        tip=(3.51*math.cos(b),3.51*math.sin(b),.61)
        for c in (a,a+math.tau/12):
            rod((3.51*math.cos(c),3.51*math.sin(c),4.44),tip,.082,CREAM)
        oval(tip,(.27,)*3,GOLD)
    for s in (-1,1):
        rod((s*2.25,1.10,5.19),(-s*1.68,-1.26,5.24+(.18 if s==1 else 0)),.097,WOOD)
        oval((-s*1.82,-1.36,5.26+(.18 if s==1 else 0)),(.46,.43,.36),GOLD)


def soldier():
    drum((0,0,.24),(3.59,3.43,.48),NAVY)
    drum((0,0,.48),(3.56,3.40,.12),GOLD)
    for s in (-1,1):
        drum((s*.56,0,2.60),(.89,.94,3.04),NAVY)
        oval((s*.56,-.18,1.04),(1.11,1.46,1.01),BLACK)
        box((s*.56,-.49,2.48),(.14,.11,2.21),GOLD)
    profile([(3.77,1.29,.98,0,0),(4.22,1.21,.96,0,0),(6.79,1.26,.93,0,0),(7.31,.81,.67,0,0)],BLUE)
    box((0,-.93,5.62),(.37,.14,3.05),CREAM,.06,(0,-27,0))
    for s in (-1,1):
        oval((s*1.16,0,6.69),(.87,.94,.67),GOLD)
        rod((s*1.26,0,6.46),(s*1.45,-.20,5.23),.32,BLUE)
        rod((s*1.45,-.20,5.23),(s*1.01,-.94,5.78),.26,BLUE)
        oval((s*1.01,-.94,5.82),(.58,)*3,CREAM)
    oval((0,-.05,8.06),(1.98,1.86,2.10),SKIN)
    face(0,-.93,8.28,.36,.94)
    drum((0,0,9.91),(2.19,2.12,2.19),RED)
    drum((0,0,8.92),(2.28,2.22,.26),GOLD)
    oval((0,-.78,8.88),(2.18,1.27,.22),BLACK)
    box((0,-1.09,9.97),(.54,.14,.54),GOLD,.06,(0,45,0))
    oval((.06,.06,11.24),(.61,.72,1.23),CREAM,(0,9,0))
    # Parade horn with a flared bell and three brass valves.
    rod((-1.04,-1.14,5.89),(1.22,-1.14,5.89),.16,GOLD)
    for i in range(3):rod((-.33+i*.31,-1.17,5.83),(-.33+i*.31,-1.17,6.26),.08,GOLD)
    rod((-.76,-1.14,5.87),(-.63,-1.14,5.47),.12,GOLD)
    rod((-.63,-1.14,5.47),(.64,-1.14,5.47),.12,GOLD)
    rod((.64,-1.14,5.47),(.75,-1.14,5.87),.12,GOLD)
    for x,r in [(1.21,.28),(1.38,.44),(1.56,.66)]:
        drum((x,-1.14,5.89),(r,r,.22),GOLD,(0,90,0))
    drum((1.68,-1.14,5.89),(.48,.48,.04),BROWN,(0,90,0))


def panel(name, outline, depth, color, bevel=.06):
    """Extrude a toy's side silhouette across its width; outline points are (y,z)."""
    count=len(outline)
    verts=[(x,y,z) for x in (-depth/2,depth/2) for y,z in outline]
    faces=[tuple(reversed(range(count))),tuple(range(count,count*2))]
    faces += [(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)]
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    import bmesh
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    if bevel:
        mod=obj.modifiers.new("Soft edges","BEVEL");mod.width=bevel;mod.segments=3
        obj.modifiers.new("Weighted normals","WEIGHTED_NORMAL")
    return part(obj,color)


def classic_nutcracker():
    drum((0,0,.24),(3.62,2.93,.48),GREEN)
    for s in (-1,1):
        drum((s*.67,0,3.47),(.94,1.04,3.71),CREAM)
        drum((s*.67,0,1.70),(1.10,1.22,2.46),BLACK)
        oval((s*.67,-.31,.77),(1.24,1.78,.85),BLACK)
        drum((s*.67,0,2.83),(1.14,1.25,.17),GOLD)
    profile([(5.06,1.39,1.10,0,0),(5.40,1.48,1.22,0,0),(6.12,1.33,1.08,0,0),(8.70,1.52,1.16,0,0),(9.03,1.18,.97,0,0)],RED)
    drum((0,0,5.95),(2.88,2.28,.42),BLACK)
    box((0,-1.17,5.97),(.73,.12,.48),GOLD)
    for s in (-1,1):
        drum((s*1.68,0,7.27),(.80,.84,3.08),RED)
        drum((s*1.68,0,8.73),(1.11,1.07,.39),GOLD)
        drum((s*1.68,0,5.90),(.88,.93,.43),BLACK)
        oval((s*1.68,-.02,5.55),(.75,.79,.74),SKIN)
        for j in range(3):
            z=6.59+j*.67
            rod((s*.76,-1.02,z+.14),(0,-1.15,z-.09),.062,GOLD)
            oval((s*.77,-1.05,z+.14),(.19,)*3,GOLD)
    drum((0,0,10.15),(2.49,2.36,2.48),SKIN)
    for s in (-1,1):
        for j in range(3):oval((s*(1.11+j*.12),.02,10.28-j*.33),(.51,1.16,1.29),WHITE)
        oval((s*.51,-1.10,10.53),(.49,.13,.34),WHITE)
        oval((s*.51,-1.20,10.52),(.20,.10,.25),BLUE)
        oval((s*.51,-1.26,10.50),(.094,.06,.16),BLACK)
        box((s*.51,-1.12,10.89),(.67,.15,.14),BLACK,.05,(0,-s*14,0))
        oval((s*.78,-.98,10.00),(.44,.13,.29),PINK)
        oval((s*.43,-1.24,9.93),(.99,.34,.30),BLACK,(0,-s*13,0))
        oval((s*.87,-1.20,9.97),(.46,.22,.18),BLACK,(0,-s*32,0))
    oval((0,-1.19,10.29),(.38,.49,.62),SKIN)
    box((0,-1.16,9.26),(1.31,.42,.84),BROWN,.12)
    for j in range(6):box((-.49+j*.198,-1.40,9.45),(.181,.10,.27),WHITE,.023)
    for j in range(7):oval(((j-3)*.16,-1.09,8.74-.27*(1-abs(j-3)/3)),(.32,.46,.97),WHITE)
    box((0,1.17,8.74),(.65,.43,2.51),WOOD,.12,(-12,0,0))
    # Tall turned shako with gold trim, cockade and a braided chin chain.
    drum((0,0,12.30),(2.63,2.53,2.49),BLACK)
    for z in (11.11,13.47):drum((0,0,z),(2.77,2.67,.20),GOLD)
    oval((0,-.63,11.09),(2.55,1.86,.23),BLACK)
    drum((0,-1.25,12.26),(.77,.77,.14),GOLD,(90,0,0))
    box((0,-1.34,12.26),(.40,.10,.40),RED,.03,(0,45,0))
    for j in range(13):
        a=math.pi*j/12
        oval((1.19*math.cos(a),-1.01-.15*math.sin(a),11.79-.43*math.sin(a)),(.19,)*3,GOLD)
    oval((0,.02,13.94),(.65,.65,1.04),RED)


DESIGNS = {"Classic Nutcracker":classic_nutcracker, "Drum":marching_drum, "Soldier":soldier, "House":house, "Superhero":superhero, "Brick Stack":bricks,
           "Football":football, "Frosted Pinecone":pinecone, "Stocking":stocking,
           "Puzzle Cube":puzzle, "Fashion Doll":fashion, "Nutcracker":nutcracker,
           "Blaster":blaster, "Candy Bag":candy, "Rag Doll":rag_doll,
           "Board Game":board_game, "Clay Tub":clay, "Pastry Box":pastry,
           "Wind-up Walker":walker}


def export_shelf(output):
    built = []
    for label, build in DESIGNS.items():
        build()
        bpy.ops.object.select_all(action="DESELECT")
        for obj in parts:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = parts[0]
        bpy.ops.object.convert(target="MESH")
        bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = "Toy "+label
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        vs=[v.co for v in obj.data.vertices]
        lo=Vector(map(min,zip(*vs)));hi=Vector(map(max,zip(*vs)))
        origin=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
        for v in obj.data.vertices:
            v.co-=origin
        obj.data.update()
        bpy.ops.export_scene.fbx(filepath=str(Path(output)/(obj.name+".fbx")),use_selection=True,
                                 axis_forward="-Z",axis_up="Y",apply_scale_options="FBX_SCALE_ALL",
                                 bake_space_transform=True,bake_anim=False,mesh_smooth_type="FACE")
        parts.clear()
        built.append(obj)
    return built
