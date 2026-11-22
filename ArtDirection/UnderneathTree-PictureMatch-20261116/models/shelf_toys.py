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
    box((0,0,3),(7.8,6.8,5.9),CREAM,.16)
    profile([(5.77,3.91,3.37,0,0),(8.48,.05,3.37,0,0)],CREAM,4)
    for s in (-1,1):
        box((s*2.05,0,7.1),(5.3,7.8,.58),RED,.12,(0,s*36,0))
        box((s*2.05,0,7.42),(5.26,7.94,.23),FROST,.10,(0,s*36,0))
        for y in (-2,0,2):
            box((s*3.95,y,3.2),(.13,1.24,1.9),TEAL)
        for z in (1.25,3.55,5.6):
            box((s*3.79,-3.46,z),(.46,.22,.35),WOOD)
        box((s*2.48,-3.45,3.65),(1.82,.23,2.38),TEAL,.12)
        box((s*2.48,-3.60,3.65),(1.4,.10,1.91),GOLD)
        box((s*2.48,-3.70,3.65),(.12,.12,1.94),CREAM)
        box((s*2.48,-3.70,3.65),(1.45,.12,.12),CREAM)
        box((s*2.48,-3.65,2.44),(2.1,.55,.22),FROST)
    box((0,-3.48,1.68),(1.93,.3,3.3),RED,.22)
    oval((.59,-3.7,1.65),(.23,)*3,GOLD)
    box((0,-3.64,4.60),(3.26,.26,.74),TEAL,.10)
    letters((0,-3.80,4.61),"POST OFFICE",.46,CREAM)
    box((2.44,1.3,8.42),(1.25,1.15,3.1),BROWN)
    box((2.44,1.3,9.96),(1.51,1.42,.35),FROST)


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
    def block(x,y,z,n,m,color):
        box((x,y,z+.54),(n*1.01-.045,m*1.01-.045,1.04),color,.11)
        for i in range(n):
            for j in range(m):
                drum((x+(i-(n-1)/2)*1.01,y+(j-(m-1)/2)*1.01,z+1.11),(.64,.64,.24),color)
    # Build a miniature drawbridge castle from studded bricks.
    block(0,0,0,6,3,TEAL)
    for s in (-1,1):
        block(s*1.99,0,1.10,2,3,RED)
        block(s*1.99,0,2.20,2,3,CREAM)
        block(s*1.99,0,3.30,2,3,BLUE)
        block(s*2.47,0,4.40,1,2,GOLD)
        box((s*1.99,-1.53,2.75),(.46,.10,.68),NAVY,.14)
    block(0,.50,3.30,2,2,CREAM)
    box((0,-1.24,1.25),(1.72,1.35,.16),WOOD,.06,(12,0,0))


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
    oval((0,0,3.65),(2.65,2.65,6.50),BROWN)
    for row in range(8):
        z=.77+row*.77
        r=.55+1.14*math.sin(math.pi*(row+.65)/8.7)
        for j in range(9):
            a=math.tau*j/9+row*.33
            loc=(r*math.cos(a),r*math.sin(a),z)
            rot=(0,31,math.degrees(a))
            oval(loc,(1.11,.89,1.44),WOOD if row%3==0 else BROWN,rot)
            oval(((r+.24)*math.cos(a),(r+.24)*math.sin(a),z+.38),(.76,.75,.46),FROST,rot)
    rod((0,0,6.5),(.25,.03,7.48),.18,WOOD)
    for s in (-1,1):
        oval((s*.48,0,6.84),(1.27,.44,.37),GREEN,(0,-s*28,0))
    oval((0,-.29,6.84),(.52,)*3,RED)


def stocking():
    oval((0,.1,1.08),(2.53,7.95,2.12),RED)
    oval((.90,3.12,1.05),(4.48,2.75,2.07),RED,(0,0,-14))
    oval((2.06,3.45,1.08),(1.86,2.38,1.91),CREAM)
    drum((0,-3.54,1.19),(2.84,2.32,1.41),FROST,(90,0,0))
    for i in range(14):
        a=math.tau*i/14
        oval((1.27*math.cos(a),-3.62,1.20+.95*math.sin(a)),(.54,1.40,.53),CREAM)
    for y in (-1.88,-.60,.68):
        for s in (-1,1):
            rod((s*.76,y-.24,1.86),(s*.35,y+.12,2.06),.075,WHITE)
            rod((s*.35,y+.12,2.06),(0,y-.12,2.14),.075,WHITE)
    oval((0,0,2.14),(.33,.33,.13),GOLD)


def puzzle():
    colors=[RED,ORANGE,BLUE,GREEN,WHITE,"Toy Yellow"]
    for i in (-1,0,1):
        for j in (-1,0,1):
            for k in (-1,0,1):
                box((i*1.67,j*1.67,2.53+k*1.67),(1.65,)*3,NAVY,.20)
    for axis in range(3):
        for sign in (-1,1):
            others=[i for i in range(3) if i!=axis]
            for i in range(3):
                for j in range(3):
                    p=[0,0,0];p[axis]=sign*2.51;p[others[0]]=(i-1)*1.67;p[others[1]]=(j-1)*1.67;p[2]+=2.53
                    size=[1.42]*3;size[axis]=.14
                    box(p,size,colors[(i*2+j+axis+(sign>0)*2+(i==j))%6],.058)
    drum((0,0,5.15),(.76,.76,.12),NAVY)
    letters((0,0,5.23),"X",.51,CREAM,True)


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
    # An inert bubble-ray play prop with a saucer-shaped nose and a bubble dial.
    oval((0,-.12,2.38),(1.95,5.16,2.24),TEAL)
    box((0,1.33,1.12),(1.18,1.38,2.23),ORANGE,.31,(-19,0,0))
    for y,r in [(-2.25,1.94),(-2.89,1.58),(-3.45,1.32)]:
        drum((0,y,2.52),(r,r,.49),GOLD,(90,0,0))
    oval((0,-3.96,2.54),(1.28,1.56,1.28),"Toy Glass")
    for s in (-1,1):
        drum((s*.92,-.16,2.49),(1.24,1.24,.18),NAVY,(0,90,0))
        oval((s*1.04,-.16,2.49),(.17,.79,.79),PINK)
        for j in range(3):oval((s*.85,.72+j*.35,2.67),(.22,.20,.50),CREAM)
    profile([(2.74,.48,.63,0,2.09),(3.23,.36,.55,0,2.32),(3.71,.18,.33,0,2.65)],ORANGE)
    for j in range(3):oval((0,-1.02+j*.46,3.41),(.48,.48,.45),PINK)


def candy():
    profile([(.05,1.74,1.48,0,0),(.45,2.38,1.84,0,0),(1.2,2.55,2.03,0,0),(3.73,2.32,1.83,0,0),(4.65,1.76,1.41,0,0),(5.34,.89,.72,0,0),(6.32,1.54,1.24,0,0)],TEAL,48,.085)
    drum((0,0,5.29),(2.16,1.78,.32),GOLD)
    for s in (-1,1):
        oval((s*.57,-.79,5.38),(1.21,.47,.69),RED,(0,-s*24,0))
    oval((0,-1.10,5.34),(.53,)*3,GOLD)
    box((0,-2.06,2.68),(3.51,.46,1.77),CREAM,.20)
    letters((0,-2.31,2.96),"NORTH POLE",.51,RED)
    letters((0,-2.31,2.37),"BONBONS",.61,TEAL)
    for x,z,c in [(-1.2,6.08,RED),(.11,6.48,PINK),(1.06,6.20,GOLD)]:
        oval((x,0,z),(.94,.96,1.15),c)
        for s in (-1,1):oval((x+s*.51,0,z+s*.21),(.47,.49,.50),CREAM)


def rag_doll():
    for s in (-1,1):
        oval((s*.53,-.14,.62),(.78,1.27,1.20),BROWN)
        oval((s*.53,0,1.37),(.64,.66,1.40),CREAM)
        for z in (1.16,1.51):drum((s*.52,0,z),(.66,.68,.16),RED)
    profile([(1.62,1.64,1.41,0,0),(1.85,1.72,1.45,0,0),(3.66,.80,.72,0,0),(4.55,.65,.6,0,0)],TEAL,40,.08)
    oval((0,-1.13,2.84),(1.79,.27,1.56),CREAM,(-19,0,0))
    box((0,-1.44,2.53),(.79,.14,.54),PINK,.12)
    for s in (-1,1):
        oval((s*1.03,0,4.03),(.98,.88,1.05),TEAL)
        oval((s*1.32,-.09,3.58),(.60,.66,1.16),SKIN,(0,-s*21,0))
    oval((0,-.10,5.6),(2.18,1.86,2.05),SKIN)
    for s in (-1,1):
        drum((s*.40,-.97,5.73),(.34,.34,.10),BROWN,(90,0,0))
        for j in (-1,1):oval((s*.40+j*.055,-1.034,5.73),(.066,)*3,CREAM)
        oval((s*.69,-.84,5.32),(.35,.16,.22),PINK)
        for j in range(3):oval((s*(.99+.16*j),.12,5.78-.43*j),(.69,.79,.74),ORANGE)
        oval((s*1.23,-.04,5.16),(.79,.70,.33),RED)
    for j in range(9):
        a=math.pi*j/8
        oval((.98*math.cos(a),-.20,5.96+.78*math.sin(a)),(.69,.93,.80),ORANGE)
    for j in range(5):
        x=(j-2)*.12
        box((x,-.99,5.27+.11*abs(j-2)),(.12,.07,.05),BROWN)


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
    profile([(.06,1.72,1.72,0,0),(.25,1.82,1.82,0,0),(3.32,2.06,2.06,0,0),(3.51,1.99,1.99,0,0)],GOLD)
    drum((0,0,3.62),(4.34,4.34,.54),TEAL)
    for i in range(28):
        a=math.tau*i/28
        oval((2.13*math.cos(a),2.13*math.sin(a),3.63),(.16,.16,.39),TEAL)
    oval((0,-1.91,1.85),(2.91,.31,1.82),CREAM)
    letters((0,-2.09,2.08),"SQUISH",.64,TEAL)
    letters((0,-2.10,1.51),"CLAY",.52,RED)
    # A molded clay snail perched on the lid.
    oval((0,-.04,4.03),(2.62,1.22,.67),GREEN)
    oval((-.18,.04,4.62),(1.58,.94,1.53),PINK)
    for j in range(24):
        a=j*.39;r=.05+j*.020
        oval((-.18+r*math.cos(a),-.43,4.66+r*math.sin(a)),(.21,.15,.21),PURPLE)
    oval((1.02,-.08,4.34),(.78,.83,.83),GREEN)
    for s in (-1,1):
        rod((1.07,s*.22,4.55),(1.25,s*.31,4.91),.075,GREEN)
        oval((1.25,s*.31,4.94),(.22,)*3,BLACK)


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


DESIGNS = {"Drum":marching_drum, "Soldier":soldier, "House":house, "Superhero":superhero, "Brick Stack":bricks,
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
