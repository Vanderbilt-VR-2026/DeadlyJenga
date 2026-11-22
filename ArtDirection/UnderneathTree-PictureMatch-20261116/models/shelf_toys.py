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
    import random
    rng=random.Random(27)
    # A tapered woody core carries overlapping, cupped scales in two visible spirals.
    profile([(.10,.47,.46,0,0),(.62,.77,.73,0,0),(1.75,.94,.85,0,0),
             (3.70,.86,.80,0,0),(5.50,.53,.49,0,0),(6.43,.18,.18,0,0)],"Cone heart")
    for i in range(96):
        u=(i+.5)/96
        z=.20+6.16*u
        envelope=(math.sin(math.pi*(.038+.95*u))**.64)*(1.22-.43*u)
        r=.18+1.84*envelope
        a=i*2.399963+ .04*rng.uniform(-1,1)
        width=(.30+.61*envelope)*rng.uniform(.91,1.12)
        h=(.44+.71*envelope)*rng.uniform(.87,1.15)
        # A narrow shank broadens to a pentagonal rim and a raised, worn umbo.
        local=[(-1.22,-.19,-.62),(-1.22,.19,-.62),(-.03,.84,-.12),(.38,1,.40),
               (.55,.55,.70),(.60,0,.85),(.54,-.62,.67),(.36,-1,.39),(-.03,-.85,-.12),
               (.39,.02,-.04),(.64,.02,.40),(-.11,.02,-.14)]
        vs=[]
        for radial,tangent,lift in local:
            rr=r+radial*(.55+.42*envelope);tt=tangent*width
            vs.append((rr*math.cos(a)-tt*math.sin(a),.92*(rr*math.sin(a)+tt*math.cos(a)),z+lift*h))
        faces=[(0,1,11),(1,2,9,11),(2,3,10,9),(3,4,10),(4,5,10),(5,6,10),(6,7,10),(7,8,9,10),(8,0,11,9),(0,8,7,6,5,4,3,2,1)]
        mesh=bpy.data.meshes.new("Woody bract");mesh.from_pydata(vs,[],faces);mesh.update()
        import bmesh
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        o=bpy.data.objects.new("Overlapping scale",mesh);bpy.context.collection.objects.link(o);part(o,"Cone bark")
        for color in ("Cone rim","Cone heart"):mesh.materials.append(bpy.data.materials[color])
        for f in mesh.polygons:
            f.material_index=1 if f.index in (3,4,5,6) else 2 if f.index in (0,9) else 0
        # Place the small raised prickle on the broad end of the woody plate.
        start=Vector(vs[10]);tip=start+Vector((.15*math.cos(a),.15*math.sin(a),.11))
        cord("Scale umbo",[start,tip],.093,"Cone rim",[1,.10])
        edge=o.modifiers.new("Worn scale edges","BEVEL");edge.width=.038;edge.segments=2
    cord("Stem",[(0,0,.41),(.04,0,.16),(.18,.07,.05)],.18,"Cone heart",[1,.82,.64])


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
    # Molded exterior of the brightly colored twin-barrel toy.
    body=[(-1.06,1.28),(-.99,1.78),(.64,1.81),(1.08,1.62),(1.53,1.08),
          (2.40,.78),(3.56,.89),(3.90,.61),(3.97,.18),(3.75,.05),
          (2.61,.11),(1.55,.39),(.78,.86),(.39,1.08),(-.64,1.06)]
    panel("Yellow molded stock",body,.67,"Toy Yellow",.12)
    for side in (-1,1):
        rod((side*.22,-4.87,1.67),(side*.22,-.48,1.67),.22,ORANGE)
        rod((side*.22,-4.92,1.67),(side*.22,-4.76,1.67),.237,ORANGE)
    panel("Ribbed yellow fore-end",[(-3.06,1.46),(-2.90,1.01),(-2.67,.95),(-.99,.99),(-.79,1.40)],.81,"Toy Yellow",.13)
    for s in (-1,1):
        for j in range(12):
            rod((s*.40,-2.77+j*.144,1.12),(s*.40,-2.73+j*.144,1.35),.035,GOLD)
        oval((s*.34,-.29,1.40),(.10,1.13,.52),"Toy Yellow")
        oval((s*.407,-.64,1.43),(.058,.15,.15),GOLD)
        oval((s*.414,-.20,1.40),(.030,.71,.27),NAVY)
        letters((s*.438,-.20,1.40),"TWIN POP",.136,WHITE)
        parts[-1].rotation_euler.z=s*math.pi/2
        cord("Stock grip detail",[(s*.34,2.33,.63),(s*.34,2.78,.65),(s*.34,3.51,.63)],.035,GREEN)
    cap=panel("Lime butt plate",[(2.14,.72),(2.75,.94),(3.54,.96),(3.99,.65),(4.03,.13),(3.75,.03),(3.32,.06)],.74,GREEN,.12)
    for j in range(3):
        rod((.38,3.19+j*.14,.50),(.38,3.53+j*.12,.53),.032,GREEN)
    cord("Molded grip loop",[(.03,-.35,1.11),(.03,-.23,.65),(.03,.13,.47),(.03,.71,.49),(.03,.98,.85)],.078,"Toy Yellow")
    oval((0,.24,.93),(.20,.21,.39),ORANGE,(-23,0,0))


def candy():
    pouch_section("Gold pouch foot",[(.08,2.06,.48),(.35,2.29,.82),(.90,2.33,.94)],GOLD)
    pouch_section("Clear gummy window",[(.88,2.33,.94),(2.55,2.39,.99),(3.60,2.31,.88)],"Cellophane")
    pouch_section("Printed pouch",[(3.56,2.31,.88),(4.22,2.29,.73),(5.72,2.17,.32),(6.05,2.26,.13)],TEAL)
    box((0,0,6.07),(4.63,.28,.29),GOLD,.07)
    box((0,-.36,5.67),(4.24,.08,.13),GOLD,.03)
    letters((0,-.78,4.99),"BRUIN",.98,CREAM)
    letters((0,-.87,4.18),"FRUIT BEARS",.47,"Toy Yellow")
    letters((0,-.97,.53),"FOUR FRUITY FLAVORS",.24,NAVY)
    colors=["Gummy cherry","Gummy lime","Gummy amber","Gummy berry"]
    for row in range(3):
        for col in range(5):
            x=(col-2)*.79+.10*math.sin(row+col);z=1.04+row*.90+.08*math.sin(2*col)
            y=-.31+.12*math.cos(row+col);c=colors[(row*3+col)%4]
            first=len(parts)
            oval((x,y,z),(.51,.42,.56),c)
            oval((x,y-.01,z+.36),(.48,.39,.40),c)
            oval((x,y-.18,z+.33),(.22,.14,.15),c)
            for s in (-1,1):
                oval((x+s*.18,y,z+.55),(.19,.19,.19),c)
                oval((x+s*.24,y-.06,z+.09),(.18,.20,.27),c)
                oval((x+s*.15,y-.13,z-.24),(.25,.29,.18),c)
            from mathutils import Matrix
            pivot=Vector((x,y,z));tilt=.49*math.sin(2.5*col+1.8*row)
            rotation=Matrix.Rotation(tilt,3,"Y")
            for sweet in parts[first:]:
                sweet.location=pivot+rotation@(sweet.location-pivot)
                sweet.rotation_euler.y+=tilt



def rag_doll():
    for s in (-1,1):
        for j in range(8):drum((s*.54,-.04,.62+j*.20),(.59,.62,.21),RED if j%2==0 else WHITE)
        oval((s*.54,-.27,.44),(.97,1.21,.77),BROWN)
    profile([(1.89,1.48,1.20,0,0),(2.15,1.58,1.27,0,0),(3.28,.90,.73,0,0),(3.80,.68,.55,0,0)],BLUE,48,.075)
    profile([(1.88,1.46,1.19,0,0),(2.03,1.54,1.24,0,0)],CREAM,48,.08)
    for s in (-1,1):
        oval((s*.93,-.04,3.48),(.85,.92,.85),BLUE)
        oval((s*1.25,-.19,3.08),(.62,.63,1.01),CREAM,(0,-s*23,0))
        box((s*.42,-.49,3.51),(.23,.14,.66),WHITE,.06,(0,s*10,0))
        for j in range(4):
            for k in range(3):
                x=s*(1.25-.15*j+.08*math.cos(k*2.09));z=2.26+.26*j+.07*math.sin(k*2.09)
                oval((x,-.70,z),(.13,.12,.13),CREAM)
    apron=panel("Pinafore",[(-.91,0),(.91,0),(.73,.57),(.42,1.26),(.48,1.62),(-.48,1.62),(-.42,1.26),(-.73,.57)],.13,WHITE,.09)
    apron.rotation_euler=(0,math.radians(23),math.pi/2)
    apron.location=(0,-1.34,2.06)
    for side in (-1,1):
        cord("Pinafore strap",[(side*.39,-.74,3.44),(side*.43,-.65,3.73),(side*.48,-.41,3.82)],.088,WHITE)
        for j in range(5):oval((side*(.12+.16*j),-1.35,2.085),(.19,.12,.12),WHITE)
    oval((0,-.05,4.80),(2.47,1.79,2.12),"Stocking fleece")
    for s in (-1,1):
        # Small closed embroidered eyes, rosy cheeks and an upturned stitched smile.
        cord("Happy eyelid",[(s*.25,-.91,4.94),(s*.40,-.934,5.014),(s*.56,-.87,4.96)],.044,BROWN,[.58,1,.54])
        oval((s*.66,-.82,4.67),(.43,.105,.24),PINK)
        for j in range(3):oval((s*(.53+.09*j),-.89+.034*j,4.72+.035*(j%2)),(.044,)*3,BROWN)
    oval((0,-.99,4.80),(.24,.14,.16),RED)
    cord("Stitched smile",[(-.36,-.91,4.65),(-.19,-.97,4.54),(0,-.98,4.52),(.19,-.97,4.54),(.36,-.91,4.65)],.033,BROWN)
    for j in range(26):
        a=math.radians(-12+j*8.15)
        x=1.12*math.cos(a);z=4.90+.92*math.sin(a)
        cord("Yarn loop",[(x*.88,.40,z),(x*1.03,.05,z+.19),(x*1.11,-.36,z+.13),(x,-.62,z-.09)],.125,RED)
    for s in (-1,1):
        for j in range(7):
            x=s*(1.0+.08*j)
            cord("Yarn bunch",[(s*.98,-.03,5.04),(x,-.03,4.76),(x+s*.19,-.15,4.41+.11*math.sin(j)),(x+s*.12,-.24,4.29+.10*math.sin(j))],.105,RED)
        oval((s*1.13,-.49,4.79),(.52,.25,.22),BLUE,(0,s*20,0))


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
    profile([(.10,1.55,1.55,0,0),(.23,1.65,1.65,0,0),(.46,1.68,1.68,0,0),
             (3.48,2.02,2.02,0,0),(3.65,2.08,2.08,0,0)],"Toy Yellow")
    drum((0,0,3.66),(4.24,4.24,.28),RED)
    drum((0,0,3.82),(4.14,4.14,.17),RED)
    drum((0,0,3.91),(3.78,3.78,.08),RED)
    for r in (1.42,1.63):
        bpy.ops.mesh.primitive_torus_add(major_radius=r,minor_radius=.044,major_segments=48,minor_segments=8,location=(0,0,3.97))
        part(bpy.context.object,RED)
    letters((0,0,3.97),"Knead-O!",.55,"Toy Red",True)
    start=len(parts)
    # Overlapping printed lobes make a scalloped, bakery-style badge.
    for width,height,depth,material in [(1.0,1.0,-1.94,WHITE),(.94,.88,-1.99,RED)]:
        for j in range(5):
            x=(j-2)*.68*width
            oval((x,depth,2.18+.10*math.cos((j-2)*1.4)),(1.48*width,.065,(1.58-.12*abs(j-2))*height),material)
    letters((0,-2.055,2.17),"Knead-O!",.87,WHITE)
    letters((0,-1.88,1.02),"MODELLING DOUGH",.27,NAVY)
    wrap_label(start,1.88)


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
    # The flat butterfly lies in the same plane as its winding stem.
    rod((0,1.12,2.69),(0,2.13,2.69),.14,GOLD)
    drum((0,1.44,2.69),(.53,.53,.19),GOLD,(90,0,0))
    for side in (-1,1):
        path=[(0,1.90,2.69),(0,2.09,2.69+side*.30),(0,2.45,2.69+side*.68),
              (0,2.84,2.69+side*.66),(0,2.95,2.69+side*.35),(0,2.71,2.69+side*.13),(0,2.17,2.69)]
        cord("Flat brass butterfly",path,.11,GOLD,cyclic=True)


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
    drum((0,0,.23),(3.43,3.01,.46),RED)
    for s in (-1,1):
        drum((s*.59,0,4.25),(1.05,1.10,2.35),CREAM)
        drum((s*.59,0,1.96),(1.05,1.10,2.69),BLACK)
        oval((s*.59,-.25,.72),(1.14,1.47,.57),BLACK)
        drum((s*.59,0,3.35),(1.10,1.16,.44),GOLD)
        for j in (-1,0,1):
            cord("Boot flourish",[(s*.59,-.58,2.61),(s*.59+j*.14,-.60,2.70),(s*.59+j*.23,-.54,2.51)],.034,GOLD)
    profile([(5.12,1.27,1.0,0,0),(5.33,1.35,1.05,0,0),(6.53,1.19,.98,0,0),
             (8.36,1.33,1.03,0,0),(8.63,1.17,.91,0,0)],RED)
    box((0,-1.0,5.65),(.79,.13,.94),BLACK,.12)
    for s in (-1,1):
        cord("Coat hem braid",[(s*1.17,-.62,5.32),(s*.81,-.88,5.29),(s*.50,-1.05,5.58),(s*.21,-1.10,6.11)],.066,GOLD)
        drum((s*1.51,0,7.17),(.71,.78,2.85),RED)
        drum((s*1.51,0,8.52),(.92,.94,.23),GOLD)
        drum((s*1.51,0,8.36),(.84,.86,.18),BLACK)
        drum((s*1.51,0,5.89),(.78,.85,.43),BLACK)
        hand_z=6.26 if s<0 else 5.47
        oval((s*1.51,-.22,hand_z),(.78,.81,.81),SKIN)
        for j in range(3):
            z=7.05+j*.49
            cord("Scalloped gold braid",[(s*(.25+k*.14),-1.04+.24*(k/5)**2,z+.07*math.cos(k*math.pi)) for k in range(6)],.068,GOLD)
    drum((0,0,6.35),(2.49,2.08,.43),BLACK)
    box((0,-1.06,6.34),(.64,.12,.48),GOLD,.04)
    box((0,-1.14,6.34),(.42,.05,.29),BLACK,.02)
    rod((-1.53,-.55,.40),(-1.53,-.55,8.37),.103,"Cone heart")
    drum((-1.53,-.55,8.41),(.30,.30,.16),GOLD)
    profile([(8.48,.95,.82,0,0),(8.68,1.17,1.01,0,0),(9.56,1.25,1.05,0,0),(10.28,1.12,.99,0,0)],SKIN)
    for s in (-1,1):
        for j in range(7):
            x=s*(1.08+.10*math.sin(j));y=-.39+j*.15
            cord("Ivory hair",[(x*.94,y,10.20),(x*1.15,y-.06,9.74),(x*1.22,y-.04,9.16),(x*1.34,y+.03,8.66-.16*math.cos(j))],.13,WHITE,[.65,1.35,1.25,.24])
        oval((s*.49,-1.022,9.76),(.55,.16,.28),WHITE)
        oval((s*.51,-1.112,9.75),(.20,.07,.235),TEAL)
        oval((s*.51,-1.152,9.75),(.094,.040,.141),NAVY)
        cord("Black eyebrow",[(s*.22,-1.01,9.99),(s*.48,-1.00,10.09),(s*.76,-.87,9.99)],.093,BLACK,[.45,1,.23])
        oval((s*.80,-.82,9.32),(.39,.08,.31),PINK)
        cord("Painted moustache",[(s*.06,-1.10,9.10),(s*.33,-1.12,9.13),(s*.67,-1.04,9.04),(s*.95,-.83,9.18)],.143,BLACK,[.47,1.10,.74,.07])
    box((0,-1.10,9.58),(.23,.29,.64),ORANGE,.08)
    box((0,-1.04,8.81),(.73,.22,.34),RED,.09)
    for j in range(4):box((-.23+j*.155,-1.17,8.83),(.143,.07,.155),WHITE,.025)
    for j in range(11):
        x=(j-5)*.059
        cord("Long tapered beard",[(x,-1.15,8.69),(x*1.09,-1.22,8.27),(x*.78,-1.23,7.64),(x*.18,-1.19,6.80+.22*abs(j-5)/5)],.10,WHITE,[.9,1,.75,.12])
    box((0,.98,8.53),(.49,.35,2.06),WOOD,.10,(-12,0,0))
    profile([(10.29,1.03,.96,0,0),(10.49,1.11,1.02,0,0),(12.36,1.30,1.20,0,0),(12.59,1.22,1.13,0,0)],BLACK)
    drum((0,0,10.34),(2.48,2.30,.19),BLACK)
    drum((0,0,12.48),(2.59,2.42,.20),GOLD)
    for j in range(15):
        a=j*math.tau/15
        oval((1.25*math.sin(a),1.16*math.cos(a),12.37),(.44,.35,.30),GOLD)
    for j in (-1,0,1):oval((j*.16,-1.14,11.69+(.14 if j==0 else 0)),(.17,.12,.40),GOLD,(0,j*32,0))
    oval((0,-1.22,11.46),(.34,.17,.34),RED)
    for j in range(17):
        a=j*math.pi/16
        x=1.17*math.cos(a);z=11.78-.99*math.sin(a);y=-1.08*math.sin(a)
        curve=[(x+.091*math.cos(k*math.pi/4),y-.10,z+.061*math.sin(k*math.pi/4)+.055*math.cos(a)*math.cos(k*math.pi/4)) for k in range(8)]
        cord("Hat chain link",curve,.023,GOLD,cyclic=True)


def cord(name, points, radius, color, widths=None, cyclic=False):
    curve=bpy.data.curves.new(name,"CURVE")
    curve.dimensions="3D";curve.resolution_u=5;curve.bevel_depth=radius;curve.bevel_resolution=2
    curve.use_fill_caps=True
    line=curve.splines.new("BEZIER");line.bezier_points.add(len(points)-1)
    for i,(k,p) in enumerate(zip(line.bezier_points,points)):
        k.co=p;k.handle_left_type=k.handle_right_type="AUTO"
        if widths:k.radius=widths[i]
    line.use_cyclic_u=cyclic
    obj=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(obj)
    return part(obj,color)


def wrap_label(start,radius):
    """Bend the label's vertices around the front of a round package."""
    for i in range(start,len(parts)):
        obj=parts[i]
        bpy.ops.object.select_all(action="DESELECT");obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.object.convert(target="MESH")
        obj=bpy.context.object;parts[i]=obj
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        for v in obj.data.vertices:
            x,y,z=v.co
            angle=x/radius
            v.co=(math.sin(angle)*(-y),math.cos(angle)*y,z)
        obj.data.update()


def pouch_section(name,rings,color):
    # Flat faces, expanding gussets and narrow heat-sealed edges give the bag its shape.
    vertices=[]
    for z,w,d in rings:
        vertices.extend([(-w,0,z),(-w*.91,-d*.80,z),(-w*.57,-d,z),(0,-d*1.04,z),
                         (w*.57,-d,z),(w*.91,-d*.80,z),(w,0,z),(w*.88,d*.85,z),(0,d,z),(-w*.88,d*.85,z)])
    n=10;faces=[tuple(reversed(range(n)))]
    for k in range(len(rings)-1):
        for i in range(n):
            j=(i+1)%n;faces.append((k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i))
    faces.append(tuple(range(len(vertices)-n,len(vertices))))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);return part(obj,color)


def berry_twists():
    # A horizontal windowed sleeve contains long helically molded strawberry ropes.
    box((0,0,.63),(6.82,2.83,1.15),"Cellophane",.18)
    for i in range(5):
        y=(i-2)*.46
        rod((-3.18,y,.60),(3.18,y,.60),.16,"Gummy cherry")
        for k in range(3):
            pts=[]
            for j in range(33):
                a=j*.52+k*math.tau/3
                pts.append((-3.16+j*.1975,y+.14*math.cos(a),.60+.14*math.sin(a)))
            cord("Twisted licorice",pts,.071,"Gummy cherry")
    for s in (-1,1):
        box((s*3.28,0,.65),(.39,2.96,1.22),RED,.10)
        for j in range(7):box((s*3.43,-1.19+j*.39,1.22),(.12,.15,.08),PINK,.03)
    box((-1.72,0,1.21),(2.29,2.82,.13),RED,.06)
    letters((-1.73,.31,1.29),"LOOP",.64,CREAM,True)
    letters((-1.73,-.31,1.29),"ROPES",.52,CREAM,True)
    box((1.27,1.09,1.22),(2.74,.51,.12),PINK,.05)
    letters((1.27,1.09,1.31),"STRAWBERRY",.29,NAVY,True)


def chocolate_bar():
    box((0,0,.37),(7.24,2.89,.70),"Cocoa",.15)
    for i in range(6):
        for j in range(2):
            x=(i-2.5)*1.15;y=(j-.5)*1.35
            box((x,y,.74),(1.06,1.24,.32),"Cocoa",.12)
            box((x,y,.90),(.79,.97,.095),"Cocoa",.046)
            if i>3:letters((x,y,.958),"C",.50,"Cocoa",True)
    box((-.87,0,.52),(5.98,3.10,1.00),"Toy Silver",.10)
    box((-1.03,0,.53),(5.43,3.16,1.06),"Cocoa wrapper",.11)
    for y in (-1.26,1.26):box((-1.03,y,1.08),(5.12,.055,.04),GOLD,.018)
    letters((-1.03,.32,1.09),"COPPER MILL",.66,CREAM,True)
    letters((-1.03,-.45,1.09),"MILK CHOCOLATE",.31,GOLD,True)
    for j in range(9):
        box((-3.72,-1.35+j*.33,.97),(.28,.085,.10),"Toy Silver",.025)


DESIGNS = {"Berry Twists":berry_twists, "Chocolate Bar":chocolate_bar, "Classic Nutcracker":classic_nutcracker, "Drum":marching_drum, "Soldier":soldier, "House":house, "Superhero":superhero, "Brick Stack":bricks,
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
