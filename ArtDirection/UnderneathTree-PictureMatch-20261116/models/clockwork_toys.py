"""Five spring-driven shelf toys, using the walker's pierced brass key."""
import math
from mathutils import Matrix, Vector
from shelf_toys import (BLACK, BLUE, BROWN, CREAM, GOLD, GREEN, NAVY, ORANGE,
                        RED, SKIN, TEAL, WHITE, box, drum, oval, parts, rod, stamped_key)

BRASS = "Stamped brass"


def key(socket, scale, angle=-90):
    first = len(parts)
    stamped_key()
    transform = (Matrix.Translation(Vector(socket)) @ Matrix.Rotation(math.radians(angle), 4, "Z")
                 @ Matrix.Scale(scale, 4) @ Matrix.Translation(Vector((0, -1.16, -2.69))))
    for obj in parts[first:]:
        obj.matrix_world = transform @ Matrix.LocRotScale(obj.location, obj.rotation_euler, obj.scale)


def eyes(x, y, z, radius):
    for side in (-1, 1):
        oval((side*x, y, z), (radius, radius*.65, radius*1.1), BLACK)
        oval((side*x-radius*.12, y-radius*.27, z+radius*.2), (radius*.25,)*3, WHITE)


def monkey():
    for side in (-1, 1):
        oval((side*.61,-.35,.32),(.83,1.35,.63),BROWN)
    oval((0,0,1.15),(1.82,1.33,1.97),RED)
    for z in (.98,1.32,1.66):
        oval((0,-.64,z),(.18,.13,.18),GOLD)
    oval((0,0,2.66),(1.94,1.44,1.68),BROWN)
    for side in (-1,1):
        oval((side*.94,-.04,2.77),(.64,.39,.74),BROWN)
        oval((side*1.04,-.21,2.77),(.34,.13,.44),SKIN)
        oval((side*.29,-.61,2.82),(.75,.33,.73),SKIN)
        rod((side*.75,-.04,1.81),(side*1.03,-.95,1.38),.22,BROWN)
        oval((side*1.03,-.99,1.42),(.43,.47,.46),SKIN)
        drum((side*.83,-1.15,1.43),(1.28,1.28,.09),BRASS,(0,side*72,0))
        oval((side*.90,-1.15,1.43),(.36,.43,.43),GOLD)
    oval((0,-.72,2.39),(1.1,.46,.52),SKIN)
    oval((0,-.91,2.58),(.35,.18,.19),BROWN)
    eyes(.29,-.779,2.88,.21)
    drum((0,.01,3.46),(.97,.94,.36),RED)
    drum((0,.01,3.31),(1.03,.99,.10),GOLD)
    key((.68,.18,1.27),.66)


def frog():
    oval((0,.05,.94),(2.34,2.52,1.47),GREEN)
    oval((0,-.87,1.21),(2.13,1.20,1.17), "Lime ABS")
    oval((0,-1.2,1.01),(1.75,.64,.33),CREAM)
    for s in (-1,1):
        oval((s*.62,-.80,1.78),(.78,.75,.81),GREEN)
        oval((s*.62,-1.06,1.86),(.55,.39,.58),CREAM)
        oval((s*1.01,.55,.60),(.84,1.80,.84),GREEN,(0,0,-s*18))
        oval((s*1.08,-.13,.20),(.63,1.55,.32), "Lime ABS")
        rod((s*.73,-.71,.83),(s*.88,-1.20,.23),.13,GREEN)
        for i in (-1,0,1):
            oval((s*.87+i*.145,-1.39,.15),(.18,.64,.21),"Lime ABS",(0,0,i*18))
        for i in range(3):
            oval((s*(.42+.12*i),.38+i*.24,1.59-i*.13),(.23,.31,.095),TEAL)
    eyes(.62,-1.245,1.88,.27)
    key((.73,.61,1.31),.65)


def drummer():
    for s in (-1,1):
        rod((s*.47,0,.29),(s*.47,0,1.35),.23,NAVY)
        oval((s*.47,-.19,.22),(.66,1.04,.44),BLACK)
    box((0,0,1.91),(1.57,1.04,1.48),BLUE,.24)
    oval((0,0,3.05),(1.37,1.22,1.29),SKIN)
    eyes(.27,-.558,3.15,.19)
    oval((0,-.64,2.99),(.27,.27,.29),SKIN)
    drum((0,.02,3.82),(1.39,1.28,.77),RED)
    drum((0,-.08,3.49),(1.51,1.50,.13),NAVY)
    drum((0,.02,4.19),(1.43,1.31,.10),GOLD)
    oval((0,-.61,3.87),(.32,.10,.43),GOLD)
    drum((0,-.98,1.51),(1.76,1.6,.87),RED)
    for z in (1.07,1.95):
        drum((0,-.98,z),(1.84,1.68,.11),GOLD)
    drum((0,-.98,2.014),(1.69,1.53,.04),CREAM)
    for i in range(12):
        a,b=i*math.tau/12,(i+1)*math.tau/12
        rod((.88*math.cos(a),-.98+.80*math.sin(a),1.12 if i%2 else 1.90),
            (.88*math.cos(b),-.98+.80*math.sin(b),1.90 if i%2 else 1.12),.032,CREAM)
    for s in (-1,1):
        oval((s*.78,0,2.38),(.52,.69,.22),GOLD)
        rod((s*.79,0,2.34),(s*.99,-.66,2.14),.19,BLUE)
        oval((s*.99,-.68,2.15),(.36,)*3,SKIN)
        rod((s*1.05,-.55,2.14),(s*.34,-1.27,2.40+s*.13),.055,BROWN)
        oval((s*.34,-1.27,2.40+s*.13),(.19,)*3,WOOD_COLOR)
    key((.69,.14,1.92),.68)


WOOD_COLOR = "Toy Wood"


def locomotive():
    box((0,0,.73),(1.77,3.52,.33),RED,.12)
    drum((0,-.49,1.39),(1.32,1.32,2.10),TEAL,(90,0,0))
    for y in (-1.49,-.98,-.02):
        drum((0,y,1.39),(1.38,1.38,.10),BRASS,(90,0,0))
    drum((0,-1.60,1.39),(1.13,1.13,.13),BLACK,(90,0,0))
    drum((0,-1.69,1.40),(.43,.43,.08),GOLD,(90,0,0))
    drum((0,-1.11,2.16),(.44,.44,.63),BLACK)
    drum((0,-1.11,2.49),(.68,.68,.15),BLACK)
    oval((0,-.26,2.03),(.63,.63,.54),GOLD)
    box((0,1.14,1.27),(1.55,1.11,.89),RED,.10)
    for s in (-1,1):
        for y in (.68,1.64):
            box((s*.68,y,1.98),(.15,.15,1.03),RED,.03)
        for y in (-1.09,0,1.09):
            drum((s*.85,y,.48),(.95,.95,.22),NAVY,(0,90,0))
            drum((s*.98,y,.48),(.71,.71,.07),RED,(0,90,0))
        rod((s*1.03,-1.09,.46),(s*1.03,1.09,.46),.068,BRASS)
        for y in (-1.09,0,1.09):
            oval((s*1.045,y,.46),(.085,.24,.24),GOLD)
    box((0,1.15,2.49),(1.90,1.40,.20),NAVY,.09)
    key((.73,.99,1.36),.62)


def hen():
    for s in (-1,1):
        rod((s*.49,.1,.85),(s*.49,-.05,.22),.12,ORANGE)
        for i in (-1,0,1):
            rod((s*.49,-.05,.15),(s*.49+i*.25,-.59+.08*abs(i),.11),.09,ORANGE)
    oval((0,.10,1.27),(1.77,2.29,1.66),CREAM)
    for s in (-1,1):
        oval((s*.73,.12,1.38),(.44,1.55,.90),TEAL,(17,0,0))
        for i in range(3):
            oval((s*.92,.03+i*.24,1.35+i*.04),(.085,.49,.13),NAVY,(23,0,0))
    for i in (-1,0,1):
        oval((i*.25,1.11,1.80),(.41,.55,1.28-abs(i)*.14),TEAL,(-31,i*19,0))
    oval((0,-.86,1.99),(1.04,1.05,1.19),CREAM)
    for i in range(3):
        oval((0,-.97+i*.21,2.56),(.28,.34,.38+.13*(i==1)),RED)
    oval((0,-1.26,1.78),(.38,.34,.44),RED)
    oval((0,-1.42,2.02),(.45,.66,.26),ORANGE)
    for s in (-1,1):
        oval((s*.419,-1.07,2.15),(.17,.19,.19),BLACK)
    key((.83,.28,1.30),.70)


DESIGNS = {"Cymbal Monkey": monkey, "Hopping Frog": frog, "Marching Drummer": drummer,
           "Tin Locomotive": locomotive, "Pecking Hen": hen}
