# Builds "Brontosaurus Stairway.blend" for Fossil Basin: a 42 m brontosaurus skeleton whose four
# feet stand on their own map-style platforms, two tail support pylons, and a switchback stair
# tower with a gangway to the top of its back.
# Run: blender --background --python build_brontosaurus.py -- "<output folder>"
import os, sys
HERE = sys.argv[sys.argv.index("--") + 1]
if "finish" not in globals(): exec(open(os.path.join(HERE, "common.py"), encoding="utf-8").read(), globals())

BRONTO = coll("01 • Brontosaurus breakaway members")
PADS = coll("02 • Foot platforms and pylons")
STAIR = coll("03 • Stair tower and gangway")

REAR_Z, FRONT_Z = 1.55, 3.2      # platform tops under the rear and front feet

# ---------- Foot platforms and tail pylons ----------
def pad(label, cx, cy, top):
    floor(label, top, cx - 1.875, cx + 1.875, cy - 1.755, cy + 1.755, 1, PADS)
    use(f"{label} strip 0"); lantern(cx + 1.6, cy + (1.5 if cy > 0 else -1.5), top)
    for dx in (-1.2, 1.2):
        for dy in (-1.1, 1.1): pier(f"{label} pier {dx:+.0f}/{dy:+.0f}", cx + dx, cy + dy, -.03, top - TILE[2], PADS)
pad("Rear left foot platform", -4.2, -2.6, REAR_Z)
pad("Rear right foot platform", -4.2, 2.6, REAR_Z)
pad("Front left foot platform", 5.2, -2.6, FRONT_Z)
pad("Front right foot platform", 5.2, 2.6, FRONT_Z)

# ---------- Skeleton ----------
HIP, SHOULDER = Vector((-4.0, 0, 9.4)), Vector((5.0, 0, 9.8))
def back_pt(t):    # t = 0 at the hips, 1 at the shoulders; the back arches up in the middle
    return HIP.lerp(SHOULDER, t) + Vector((0, 0, .75 * math.sin(math.pi * t) + .9))
NECK = [Vector(p) for p in ((5.8, 0, 10.9), (9.4, 0, 12.9), (13.2, 0, 15.4), (16.8, 0, 17.6), (19.4, 0, 18.3))]
TAIL = [Vector(p) for p in ((-4.6, 0, 10.2), (-8.5, 0, 9.8), (-13.0, 0, 8.3), (-19.0, .8, 6.4), (-25.0, 2.2, 5.3), (-31.0, 3.6, 4.7), (-36.0, 4.6, 4.5))]
neck_pts, tail_pts = _catmull(NECK, 8), _catmull(TAIL, 10)
def along(pts, f):
    i = min(len(pts) - 2, int(f * (len(pts) - 1))); r = f * (len(pts) - 1) - i
    return pts[i].lerp(pts[i + 1], r), (pts[i + 1] - pts[i]).normalized()

WALK = []       # tops of the flared spine caps in the current member, for its walking proxy

def walk_cap(name, top, t_dir, width, length, th=.14):
    # A broad, flat-topped bone flare on a spine tip. A row of them makes a walkable bony ridge.
    f = Vector((t_dir.x, t_dir.y, 0)).normalized(); lat = Vector((-f.y, f.x, 0)); z = Vector((0, 0, 1))
    hw, hl = width / 2, length / 2
    pts = [top + f * x + lat * y for x, y in ((hl, hw * .7), (hl * .7, hw), (-hl * .7, hw), (-hl, hw * .7), (-hl, -hw * .7), (-hl * .7, -hw), (hl * .7, -hw), (hl, -hw * .7))]
    plate(name, pts, z, th, BONE)
    WALK.append((top + z * th, width))

def walk_proxy():
    if len(WALK) > 1:
        a, b = WALK[0][0], WALK[-1][0]; w = sum(x[1] for x in WALK) / len(WALK)
        proxy((a + b) / 2, (w, .3, (b - a).length + .9), frame(a, b)[0])
    WALK.clear()

def vertebra(name, p, t_dir, r, spine_h, bifid=False, ribs=None, length=None, cap=None):
    length = length or r * 1.6
    loft(f"{name} centrum", [p - t_dir * length / 2, p + t_dir * length / 2],
         lambda u: ell(r * (1 - .2 * math.sin(math.pi * u)), r * (1 - .2 * math.sin(math.pi * u)), 14), per=3)
    up = Vector((0, 0, 1)) - t_dir * t_dir.z; up.normalize()
    base = p + up * r * .8
    tips = [Vector((0, s * .28 * spine_h / 2, 0)) for s in (-1, 1)] if bifid else [Vector()]
    for k, o in enumerate(tips):
        loft(f"{name} neural spine {k}", [base, base + up * spine_h * .55 + o * .6 - t_dir * .1, base + up * spine_h + o - t_dir * .2],
             lambda u: ell(.1 + .06 * (1 - u), .28 * (1 - .5 * u) + .05, 8), up=t_dir, per=4)
    if cap: walk_cap(f"{name} walk cap", base + up * spine_h - t_dir * .2, t_dir, *cap)
    for s in (-1, 1):
        loft(f"{name} transverse process", [p + Vector((0, s * r * .6, r * .3)), p + Vector((0, s * (r + .55), r * .5)) - t_dir * .1], taper(.14, .06, .6, 7), up=t_dir, per=3)
    if ribs:
        depth, width = ribs
        for s in (-1, 1):
            ctrl = [p + Vector((0, s * (r + .3), r * .3)), p + Vector((-.1, s * (width * .85), -depth * .15)), p + Vector((-.25, s * width, -depth * .5)),
                    p + Vector((-.4, s * width * .85, -depth * .82)), p + Vector((-.5, s * width * .45, -depth))]
            loft(f"{name} rib {'L' if s < 0 else 'R'}", ctrl, lambda u: ell(.15 * (1 - .4 * u) + .02, .3 * (1 - .45 * u), 10), up=(1, 0, 0), per=5, px=True)
        # Belly ribs (gastralia) close the cage underneath and give a ladder to climb.
        belly = [p + Vector((-.5, -width * .45, -depth)), p + Vector((-.55, 0, -depth - .3)), p + Vector((-.5, width * .45, -depth))]
        loft(f"{name} gastralia", belly, lambda u: ell(.08, .1, 8), up=(1, 0, 0), per=4)

# Back: three segments of dorsal vertebrae with deep ribs.
for sgi in range(3):
    member(f"Brontosaurus back {sgi}", BRONTO, "bone")
    for i in range(4):
        k = sgi * 4 + i; t = 1 - k / 11
        p = back_pt(t); d = (back_pt(t + .01) - back_pt(t - .01)).normalized()
        vertebra(f"Brontosaurus dorsal {k}", p, d, .55, 1.3 + 1.0 * (1 - t), bifid=True, ribs=(4.3 * (.55 + .45 * math.sin(math.pi * min(1, t * 1.15))), 2.7), length=.85, cap=(1.5, .95))
    walk_proxy()
    a, b = back_pt(1 - sgi * 4 / 11), back_pt(1 - (sgi * 4 + 3) / 11)
    proxy((a + b) / 2, (1.4, 1.4, (b - a).length + 1), frame(a, b)[0])

member("Brontosaurus hips", BRONTO, "bone")
for s in (-1, 1):
    plate("Brontosaurus ilium", [HIP + Vector((x, s * 1.45, z)) for x, z in ((-2.2, .6), (-1.4, 1.5), (.2, 1.7), (1.8, 1.2), (2.1, .2), (.9, -.7), (-.8, -.8), (-1.9, -.2))], (0, 1, 0), .16, BONE)
    bone("Brontosaurus pubis", [HIP + Vector((.3, s * 1.4, -.6)), HIP + Vector((1.3, s * .9, -2.4)), HIP + Vector((1.6, s * .3, -3.2))], .32, .2, knob=1.5)
    bone("Brontosaurus ischium", [HIP + Vector((-.5, s * 1.4, -.6)), HIP + Vector((-1.6, s * .9, -2.2)), HIP + Vector((-2.2, s * .3, -2.8))], .28, .16, knob=1.5)
for i in range(5):
    p = HIP + Vector((-1.6 + .8 * i, 0, 1.0)); vertebra(f"Brontosaurus sacral {i}", p, Vector((1, 0, 0)), .55, 2.3, bifid=True, cap=(1.7, .9))
walk_proxy()
proxy(HIP + Vector((0, 0, .6)), (4.6, 3.2, 3.4))

for s, side in ((-1, "left"), (1, "right")):
    member(f"Brontosaurus {side} hind leg", BRONTO, "bone")
    hip_j = HIP + Vector((0, s * 1.65, -.1))
    knee = Vector((-3.4, s * 1.85, 5.6)); ankle = Vector((-3.95, s * 2.05, REAR_Z + .75))
    bone("Brontosaurus femur", [hip_j, hip_j.lerp(knee, .5) + Vector((.15, 0, 0)), knee], .55, .48, knob=1.45)
    for o in (-.22, .22):
        bone("Brontosaurus shin", [knee + Vector((0, o, 0)), knee.lerp(ankle, .5) + Vector((0, o * 1.1, 0)), ankle + Vector((0, o, 0))], .3 if o < 0 else .2, .26 if o < 0 else .17, knob=1.4, px=o < 0)
    loft("Brontosaurus ankle", [ankle - Vector((.35, 0, .1)), ankle + Vector((.35, 0, -.1))], taper(.42, .4, .7, 12), per=3)
    for k in range(5):
        a = ankle + Vector((-.1 + .12 * k, s * (-.35 + .17 * k), -.25))
        tip = Vector((-3.0 + .12 * k, s * (1.55 + .22 * k), REAR_Z + .15))
        bone(f"Brontosaurus metatarsal {k}", [a, a.lerp(tip, .6)], .16, .13, knob=1.3, px=False)
        fang(f"Brontosaurus toe claw {k}", a.lerp(tip, .6), (tip - a).normalized() + Vector((0, 0, -.5)), (0, 0, 1), .22, .55, .35, BONE)
    member(f"Brontosaurus {side} foreleg", BRONTO, "bone")
    sh = SHOULDER + Vector((0, s * 1.9, -.2))
    plate("Brontosaurus scapula", [sh + Vector(v) for v in ((-.4, 0, .2), (-1.6, 0, 1.6), (-2.2, 0, 2.2), (-1.7, 0, 2.5), (-.6, 0, 1.2), (.6, 0, .3), (.4, 0, -.4))], (0, 1, 0), .14, BONE)
    elbow = Vector((5.45, s * 2.0, 6.5)); wrist = Vector((5.05, s * 2.1, FRONT_Z + 1.25))
    bone("Brontosaurus humerus", [sh, sh.lerp(elbow, .5) + Vector((.2, 0, 0)), elbow], .5, .44, knob=1.5)
    for o in (-.2, .2):
        bone("Brontosaurus forearm", [elbow + Vector((o, 0, 0)), elbow.lerp(wrist, .5) + Vector((o, 0, 0)), wrist + Vector((o, 0, 0))], .25, .22, knob=1.4, px=o < 0)
    for k in range(5):     # sauropod hands stand on a horseshoe of upright metacarpals
        ang = math.radians(-70 + 35 * k); base_ = wrist + Vector((math.cos(ang) * .4 + .05, s * math.sin(ang) * .4, -.2))
        bone(f"Brontosaurus metacarpal {k}", [base_, Vector((base_.x + .05, base_.y, FRONT_Z + .1))], .14, .15, knob=1.3, px=False)
        if k == 0:   # the sauropod thumb claw
            fang("Brontosaurus thumb claw", Vector((base_.x + .1, base_.y, FRONT_Z + .45)), (.8, -s * .5, -.3), (0, 0, 1), .32, .95, .45, BONE)
    proxy(wrist - Vector((0, 0, .6)), (1.2, 1.2, 1.4))

for sgi in range(3):
    member(f"Brontosaurus neck {sgi}", BRONTO, "bone")
    for i in range(5):
        k = sgi * 5 + i; f = (k + .5) / 15
        p, d = along(neck_pts, f); r = .55 - .3 * f
        vertebra(f"Brontosaurus cervical {k}", p, d, r, .45 - .2 * f, bifid=True, length=1.0, cap=(1.5 - .75 * f, 1.15))
        for s in (-1, 1):
            loft(f"Brontosaurus cervical rib {k}", [p + Vector((0, s * r * .9, -r * .6)), p - d * 1.2 + Vector((0, s * r * .7, -r * .9)), p - d * 2.1 + Vector((0, s * r * .5, -r))],
                 taper(.08, .03, .7, 6), up=(0, 0, 1), per=3)
    walk_proxy()
    a, _ = along(neck_pts, sgi / 3); b, _ = along(neck_pts, (sgi + 1) / 3)
    proxy((a + b) / 2, (1.3, 1.3, (b - a).length), frame(a, b)[0])

member("Brontosaurus skull", BRONTO, "bone")
SK0, SKD = Vector((19.5, 0, 18.4)), Vector((1, 0, -.35)).normalized()
skull = loft("Brontosaurus skull", [SK0 + SKD * f for f in (0, .7, 1.4, 2.1)],
             lambda u: rrect(.48 * (1 - .35 * u), .46 * (1 - .3 * u), .38 * (1 - .2 * u), .65, 18), per=4)
carve(skull, cutters("Brontosaurus skull", [((19.95, s * .45, 18.55), (.28, .2, .24)) for s in (-1, 1)] +
                     [((20.35, 0, 18.88), (.36, .24, .2))] + [((20.9, s * .4, 18.1), (.3, .16, .17)) for s in (-1, 1)]))
weather(skull, .02); proxy(SK0 + SKD * 1.05, (2.4, 1.0, 1.0))
walk_cap("Brontosaurus crown", Vector((20.1, 0, 18.82)), Vector((1, 0, 0)), 1.2, 1.5, .12); walk_proxy()
for s in (-1, 1):
    for i in range(7):
        p = SK0 + SKD * (1.2 + .13 * i) + Vector((0, s * (.36 - .03 * i), -.36))
        fang(f"Brontosaurus peg tooth {i}", p, (.1, -s * .1, -1), (0, s, 0), .08, .2, .05)
member("Brontosaurus lower jaw", BRONTO, "bone")
for s in (-1, 1):
    loft("Brontosaurus dentary", [SK0 + Vector((.1, s * .4, -.5)), SK0 + SKD * 1.2 + Vector((0, s * .32, -.55)), SK0 + SKD * 2.0 + Vector((0, s * .05, -.45))],
         lambda u: rrect(.07, .08, .18 - .06 * u, .6, 10), per=4, px=True)

for sgi in range(5):
    member(f"Brontosaurus tail {sgi}", BRONTO, "bone")
    for i in range(9):
        k = sgi * 9 + i; f = (k + .5) / 45
        p, d = along(tail_pts, f); r = max(.07, .52 * (1 - f) ** 1.3)
        loft(f"Brontosaurus caudal {k}", [p - d * .45, p + d * .45], lambda u, r=r: ell(r * (1 - .18 * math.sin(math.pi * u)), r * (1 - .18 * math.sin(math.pi * u)), 12), per=3)
        if f < .6:
            up = Vector((0, 0, 1))
            sh = r + 1.0 * (1 - f / .6) + .15
            loft(f"Brontosaurus caudal spine {k}", [p + up * r * .8, p + up * sh - d * .3], taper(.12 * (1 - f) + .03, .06, 2.2, 7), up=d, per=3)
            if f < .5: walk_cap(f"Brontosaurus caudal walk cap {k}", p + up * sh - d * .3, d, 1.3 * (1 - f) + .35, .95, .12)
            loft(f"Brontosaurus chevron {k}", [p - up * r * .7, p - up * (r + .9 * (1 - f / .6) + .1) - d * .35], taper(.1 * (1 - f) + .03, .03, 2.0, 7), up=d, per=3)
            for s in (-1, 1):
                if f < .35: loft(f"Brontosaurus caudal rib {k}", [p + Vector((0, s * r * .7, 0)), p + Vector((0, s * (r + .6 * (1 - f / .35)), 0)) - d * .15], taper(.1, .04, .6, 6), up=d, per=3)
    walk_proxy()
    a, _ = along(tail_pts, sgi / 5); b, _ = along(tail_pts, (sgi + 1) / 5)
    rr = max(.15, .55 * (1 - sgi / 5) ** 1.3)
    proxy((a + b) / 2, (rr * 2.4, rr * 2.4, (b - a).length), frame(a, b)[0])

# Museum-style supports: map piers with a brass cradle under the tail and the long neck.
for label, pts, f, carried, rad in (("Tail pylon 0", tail_pts, .22, "Brontosaurus tail 1", lambda f: max(.07, .52 * (1 - f) ** 1.3)),
                                     ("Tail pylon 1", tail_pts, .5, "Brontosaurus tail 2", lambda f: max(.07, .52 * (1 - f) ** 1.3)),
                                     ("Neck pylon", neck_pts, .45, "Brontosaurus neck 1", lambda f: .55 - .3 * f)):
    p, _ = along(pts, f); r = rad(f)
    floor(f"{label} cap", p.z - r - 1.3, p.x - 1.25, p.x + 1.25, p.y - 1.17, p.y + 1.17, 1, PADS)
    for dx in (-.6, .6):
        pier(f"{label} pier {dx:+.0f}", p.x + dx, p.y, -.03, p.z - r - 1.3 - TILE[2], PADS)
    member(f"{label} cradle", PADS, "timber")
    for s in (-1, 1):
        shaft("Brass cradle arm", (p.x, p.y + s * .5, p.z - r - 1.3), (p.x, p.y + s * (r + .15), p.z - r * .2), .12, .12, "Oil-dark brass", px=True)
    box("Brass cradle saddle", (p.x, p.y, p.z - r - .55), (.25, .9, 1.3), "Oil-dark brass", bevel=.03, px=True)
    join(carried, f"{label} cradle", p - Vector((0, 0, r)), 6000)

# Joints along the skeleton and down to the platforms.
join("Brontosaurus back 0", "Brontosaurus back 1", back_pt(1 - 3.5 / 11), 7000)
join("Brontosaurus back 1", "Brontosaurus back 2", back_pt(1 - 7.5 / 11), 7000)
join("Brontosaurus back 2", "Brontosaurus hips", back_pt(0), 8000)
join("Brontosaurus neck 0", "Brontosaurus back 0", NECK[0], 7000)
join("Brontosaurus neck 1", "Brontosaurus neck 0", along(neck_pts, 1 / 3)[0], 6000)
join("Brontosaurus neck 2", "Brontosaurus neck 1", along(neck_pts, 2 / 3)[0], 6000)
join("Brontosaurus skull", "Brontosaurus neck 2", SK0, 4000)
join("Brontosaurus lower jaw", "Brontosaurus skull", SK0 + Vector((.1, 0, -.5)), 2500)
join("Brontosaurus tail 0", "Brontosaurus hips", TAIL[0], 7000)
for k in range(1, 5): join(f"Brontosaurus tail {k}", f"Brontosaurus tail {k-1}", along(tail_pts, k / 5)[0], 5000)
for s, side in ((-1, "left"), (1, "right")):
    join(f"Brontosaurus {side} hind leg", "Brontosaurus hips", HIP + Vector((0, s * 1.65, -.1)), 9000)
    join(f"Brontosaurus {side} foreleg", "Brontosaurus back 0", SHOULDER + Vector((0, s * 1.9, -.2)), 9000)
    join(f"Brontosaurus {side} hind leg", f"Rear {side} foot platform strip 0", (-3.95, s * 2.2, REAR_Z), 9000)
    join(f"Brontosaurus {side} foreleg", f"Front {side} foot platform strip 0", (5.1, s * 2.1, FRONT_Z), 9000)

# ---------- Stair tower and gangway to the top of the back ----------
TOP = 13.3           # gangway height, level with the walkable hip spine caps
SX0, SY0 = -6.0, 5.6       # on the far (north) side of the body, behind it from the main view
x_end, y1, zs = stair_tower("Back stair", SX0, SY0, REAR_Z, TOP, 2, STAIR, open_edges=(0, 2))
gx = SX0 + 2.5
member("Back gangway", STAIR, "deck")
gy0, gy1 = SY0, 1.0
n = int((gy0 - gy1) / .5)
for i in range(n):
    box("Gangway plank", (gx, gy0 - (i + .5) * (gy0 - gy1) / n, TOP - .1), (2.0, (gy0 - gy1) / n + .02, .2), "Oxide-stained beam", bevel=.02)
proxy((gx, (gy0 + gy1) / 2, TOP - .1), (2.0, gy0 - gy1, .2))
for s in (-1, 1):
    shaft("Gangway rail", (gx + s, gy0, TOP + 1.0), (gx + s, gy1, TOP + 1.0), .08, .08, "Oil-dark brass", bevel=.01)
    for y in (gy0, (gy0 + gy1) / 2, gy1): box("Gangway post", (gx + s, y, TOP + .5), (.1, .1, 1.0), "Oxide-stained beam", bevel=.02)
box("Gangway end rail", (gx, gy1 + .05, TOP + 1.0), (2.0, .08, .08), "Oil-dark brass", bevel=0)
pier("Gangway support", gx, 4.98, -.03, TOP - .2, STAIR, size=.8)
join("Back gangway", "Back stair landing 2 strip 0", (gx, gy0, TOP - .2), 5200)
join("Back gangway", "Brontosaurus hips", (gx, gy1, TOP - .2), 1500)
# Entry: a short bridge from the rear right foot platform to the bottom landing.
bridge("Back stair entry bridge", (-4.6, 4.3), (-4.6, SY0 + .1), 1.6, REAR_Z, STAIR)
join("Back stair entry bridge", "Back stair landing 0 strip 0", (-4.6, SY0, REAR_Z - .2), 5200)
join("Back stair entry bridge", "Rear right foot platform strip 0", (-4.6, 4.3, REAR_Z - .2), 5200)

make_tar(PADS)
finish("Brontosaurus Stairway", [
    ("01 • Brontosaurus overview", (34, -52, 26), (-4, -4, 8), 30),
    ("02 • Side elevation", (-6, -80, 12), (-6, 0, 10), 28),
    ("03 • From the gangway", (-3.5, 1.6, TOP + 1.7), (14, -1, 15.5), 22),
    ("04 • At the front feet", (13, -10, 4.2), (3, 0, 9), 22),
    ("05 • Stair tower", (16, 36, 11), (-3.5, 12, 7), 28)], prefix="Bronto ")
