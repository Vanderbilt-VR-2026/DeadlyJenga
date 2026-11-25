# Level 1 shark skeleton for the Fossil Exhibit Tower (run inside build_exhibit_tower.py).
# ===================== Level 1: the shark =====================
HEAD_X, TAIL_X, SZ = 6.2, -12.5, 12.4
def sp(t):
    return Vector((HEAD_X + (TAIL_X - HEAD_X) * t, .8 * math.sin(math.pi * 1.1 * t),
                   SZ + .5 * math.sin(math.pi * t) + (9 * (t - .82) ** 1.2 if t > .82 else 0)))
def tangent(t): return (sp(min(1, t + .01)) - sp(max(0, t - .01))).normalized()
def girth(t): return math.sin(math.pi * min(t / .62, 1)) ** .55 if t < .62 else 0
def cr(t): return .72 * (1 - .7 * t)
SEG = 8
def seg(t): return min(SEG - 1, int(t * SEG))
spine = [member(f"Shark spine and ribs {s}", SHARK, "bone") for s in range(SEG)]
column = {s: [] for s in range(SEG)}
N = 42
for i in range(N):
    t = i / (N - 1); current = spine[seg(t)]; globals()["current"] = current
    p = sp(t); r = cr(t); T = tangent(t); half = 19 / N * .42
    loft(f"Shark centrum {i}", [p - T * half, p + T * half],
         lambda u, r=r: ell(r * (1 - .18 * math.sin(math.pi * u)), r * (1 - .18 * math.sin(math.pi * u)), 14), per=3, up=(0, 0, 1))
    column[seg(t)].append((p, r))
    if t < .9:
        h = 1.7 * girth(t) + .55 * (1 - t)
        base = p + Vector((0, 0, r * .7))
        loft(f"Shark neural spine {i}", [base, base + Vector((-.25 * h, 0, .55 * h)), base + Vector((-.62 * h, 0, h))],
             lambda u, h=h: ell(.11 * (1 - .6 * u) + .02, (.32 - .18 * u) * (1 + .4 * math.exp(-(u / .15) ** 2)), 8), up=(1, 0, 0), per=4)
        for side in (-1, 1):
            loft(f"Shark transverse process {i}", [p + Vector((0, side * r * .6, 0)), p + Vector((-.15, side * (r + .35 + .3 * girth(t)), -.1))],
                 taper(.13, .05, .6, 7), up=(1, 0, 0), per=3)
    if .58 < t < .97:
        for side in (-1, 1):
            loft(f"Shark chevron {i}", [p - Vector((0, 0, r * .6)), p + Vector((-.2, side * .25, -.5 * (.9 * (1 - t) + .5))),
                                        p + Vector((-.45, side * .08, -(.9 * (1 - t) + .5)))], taper(.1, .03, .7, 7), up=(1, 0, 0), per=3)
for s, pts in column.items():
    current = spine[s]; globals()["current"] = current
    a, b = pts[0][0], pts[-1][0]; r = max(p[1] for p in pts); q, n = frame(a, b)
    proxy((a + b) / 2, (2 * r, 2 * r, n + 2 * r), q)
for k in range(18):
    t = .07 + .5 * k / 17; current = spine[seg(t)]; globals()["current"] = current
    p = sp(t); g = girth(t)
    for side in (-1, 1):
        ctrl = [p + Vector((0, side * .55, -.1)), p + Vector((-.15, side * (1.9 * g + .8), -.35 * g)),
                p + Vector((-.4, side * (3.0 * g + .5), -1.6 * g)), p + Vector((-.65, side * (3.1 * g + .3), -3.0 * g)),
                p + Vector((-.85, side * (2.2 * g + .25), -4.2 * g)), p + Vector((-.95, side * (1.0 * g + .2), -4.75 * g))]
        loft(f"Shark rib {k} {'L' if side < 0 else 'R'}", ctrl,
             lambda u: ell(.16 * (1 - .45 * u) + .03 * math.exp(-(u / .08) ** 2), .34 * (1 - .5 * u), 10), up=(1, 0, 0), per=5, px=True)

member("Shark dorsal fin", SHARK, "bone")
d0 = sp(.22) + Vector((.4, 0, 2.1)); d1 = sp(.36) + Vector((0, 0, 1.6))
fin("Shark dorsal", d0, d1, [d0 + Vector((1.6, 0, 4.6)), d0 + Vector((.6, 0, 4.9)), d0 + Vector((-.6, 0, 4.4)),
    d0 + Vector((-1.7, 0, 3.4)), d0 + Vector((-2.7, 0, 2.3)), d0 + Vector((-3.4, 0, 1.2)), d1 + Vector((-.8, 0, .2))], (0, 1, 0))
proxy((d0 + d1) / 2 + Vector((-.3, 0, 2.3)), (4.8, .35, 4.8))

member("Shark tail fin", SHARK, "bone")
tip = sp(1); up_ = Vector((-.62, 0, .8)).normalized()
loft("Shark caudal column", [tip, tip + up_ * 1.8, tip + up_ * 3.8 + Vector((-.3, 0, 0))], taper(.24, .05, 1, 12), per=5)
fin("Shark upper lobe", tip + Vector((.2, 0, -.2)), tip + up_ * 3.8, [tip + Vector((-1.4, 0, -.2)), tip + up_ * 1.0 + Vector((-1.6, 0, 0)),
    tip + up_ * 2.0 + Vector((-1.6, 0, .1)), tip + up_ * 3.0 + Vector((-1.4, 0, .2)), tip + up_ * 4.6 + Vector((-.9, 0, .1))], (0, 1, 0))
lr0, lr1 = sp(.88) + Vector((0, 0, -.3)), sp(.98) + Vector((0, 0, -.2))
fin("Shark lower lobe", lr0, lr1, [lr0 + Vector((.6, 0, -2.7)), lr0 + Vector((-.5, 0, -3.3)), lr0 + Vector((-1.6, 0, -3.0)),
    lr0 + Vector((-2.4, 0, -2.2)), lr1 + Vector((-1.2, 0, -.6))], (0, 1, 0))
proxy(tip + Vector((-1.2, 0, 1.4)), (3.6, .4, 6.4)); proxy(lr0 + Vector((-.8, 0, -1.6)), (3.4, .4, 3.4))

for side, label in ((-1, "left"), (1, "right")):
    member(f"Shark {label} flipper", SHARK, "bone"); s = side
    bone("Shark scapula", [(5.9, s * 1.2, 12.3), (5.8, s * 2.0, 11.0), (5.5, s * 2.6, 9.7)], .32, .26, flat=2.2, up=(1, 0, 0))
    bone("Shark humerus", [(5.5, s * 2.65, 9.55), (5.0, s * 3.5, 9.0), (4.4, s * 4.3, 8.5)], .42, .34, knob=1.6)
    for dz in (-.24, .24):
        bone("Shark forearm", [(4.35, s * 4.35, 8.5 + dz), (3.85, s * 4.85, 8.15 + dz), (3.3, s * 5.3, 7.75 + dz)], .19, .17, knob=1.5, px=dz < 0)
    for k, end in enumerate(((2.3, 6.7, 7.5), (1.6, 6.5, 7.0), (1.0, 6.1, 6.6), (.55, 5.6, 6.3))):
        a = Vector((3.25 - .08 * k, s * (5.25 + .05 * k), 7.75 - .1 * k)); e = Vector((end[0], s * end[1], end[2]))
        for j in range(3):
            p0, p1 = a.lerp(e, j / 3), a.lerp(e, (j + 1) / 3)
            bone(f"Shark phalanx {k}-{j}", [p0, p1], .16 - .03 * j, .13 - .03 * j, flat=.7, knob=1.35, px=False)
    fin(f"Shark {label} flipper web", (3.3, s * 5.3, 7.75), (2.2, s * 5.0, 7.4),
        [(2.3, s * 6.7, 7.5), (1.6, s * 6.5, 7.0), (1.0, s * 6.1, 6.6), (.55, s * 5.6, 6.3)], (.25, .1, 1), rays=False, th=.045)
    proxy((4.0, s * 4.6, 8.4), (2.6, 2.6, 1.4)); proxy((1.9, s * 6.0, 7.0), (2.8, 1.8, .7))

# Skull: lofted wedge, cut sockets, brow ridges, cheek arches and curved fangs.
member("Shark skull and upper teeth", SHARK, "bone")
ST = [(6.0, 1.5, 1.3, 1.1, 12.6), (7.0, 1.85, 1.75, 1.25, 12.7), (8.2, 1.95, 1.6, 1.2, 12.6), (9.4, 1.65, 1.35, .98, 12.4),
      (10.8, 1.28, 1.0, .82, 12.2), (12.2, .92, .74, .66, 12.0), (13.3, .62, .52, .52, 11.9), (13.95, .3, .3, .36, 11.85)]
def sk_at(x):
    for a, b in zip(ST, ST[1:]):
        if a[0] <= x <= b[0]:
            f = (x - a[0]) / (b[0] - a[0]); f = f * f * (3 - 2 * f)
            return [a[i] + (b[i] - a[i]) * f for i in range(5)]
    return list(ST[-1])
xs = [6.0 + (13.95 - 6.0) * i / 30 for i in range(31)]
skull = loft("Shark skull", [(x, 0, sk_at(x)[4]) for x in xs[::3]] + [(13.95, 0, 11.85)],
             lambda u: rrect(sk_at(6.0 + 7.95 * u)[1], sk_at(6.0 + 7.95 * u)[2], sk_at(6.0 + 7.95 * u)[3], .55, 24), per=4)
proxy((7.6, 0, 12.75), (3.2, 3.8, 2.9)); proxy((11.6, 0, 12.1), (4.8, 2.6, 1.8))
cut = cutters("Shark skull", [((9.75, s * 1.55, 13.0), (1.0, 1.05, .82)) for s in (-1, 1)] +
              [((7.3, s * 2.0, 12.55), (.9, .85, .65)) for s in (-1, 1)] +
              [((11.75, s * 1.05, 12.3), (.65, .5, .34)) for s in (-1, 1)] +
              [((13.35, s * .48, 12.1), (.28, .2, .16)) for s in (-1, 1)] +
              [((6.0, 0, 12.6), (.45, .55, .5))])
weather(skull, .11); carve(skull, cut)
for sd in (-1, 1):
    loft("Shark snout ridge", [(9.0, sd * .95, 13.75), (10.8, sd * .78, 13.25), (12.4, sd * .5, 12.75), (13.6, sd * .25, 12.2)], taper(.16, .07, .7, 8), per=5)
    loft("Shark maxilla ridge", [(8.0, sd * 1.88, 11.7), (10.0, sd * 1.55, 11.55), (12.0, sd * 1.02, 11.42), (13.6, sd * .55, 11.38)], taper(.22, .12, .8, 9), per=5, grit=.04)
loft("Shark crown crest", [(6.2, 0, 14.0), (7.6, 0, 14.35), (9.4, 0, 13.75), (11.0, 0, 13.1)], taper(.26, .08, .6, 10), per=6, grit=.05)
for s in (-1, 1):
    loft("Shark brow ridge", [(8.5, s * 1.42, 13.62), (9.6, s * 1.5, 13.8), (10.9, s * 1.08, 13.15)], taper(.3, .14, .6, 10), per=6, grit=.05)
    loft("Shark cheek arch", [(6.5, s * 1.85, 11.75), (7.6, s * 2.15, 11.8), (8.9, s * 1.95, 11.6), (10.4, s * 1.42, 11.45)], taper(.32, .22, .8, 10), per=6)
    loft("Shark postorbital bar", [(8.6, s * 1.75, 13.5), (8.9, s * 1.98, 12.6), (9.0, s * 1.95, 11.7)], taper(.24, .2, .8, 9), per=4)
    for i in range(19):
        f = i / 18; x = 7.7 + 6.0 * f; w, top, bot, zc = sk_at(x)[1:]
        size = (.55 + .55 * math.sin(math.pi * min(1, f * 1.12)) ** .8) * random.uniform(.8, 1.15)
        loft(f"Shark tooth root {i}", [(x - .18, s * w * .9, zc - bot * .82), (x + .18, s * w * .9, zc - bot * .82)], taper(.2 * size, .2 * size, .8, 8), per=2, sub=1)
        fang(f"Shark upper fang {i}", (x, s * w * .86, zc - bot * .9), (0, -s * .2, -1), (0, s, 0), size * .42, size, .2)
for y in (-.22, 0.0, .22):
    fang("Shark front fang", (13.8, y, 11.6), (.35, 0, -1), (1, 0, 0), .3, .8, .25)

member("Shark lower jaw", SHARK, "bone")
H = Vector((7.0, 0, 11.15)); R = Matrix.Rotation(math.radians(30), 3, "Y")
def J(x, y, z): return H + R @ Vector((x, y, z))
jaw_up = R @ Vector((0, 0, 1))
jaws = []
for s in (-1, 1):
    jaws.append(loft("Shark mandible", [J(-.2, s * 1.85, -.45), J(1.6, s * 1.78, -.42), J(3.6, s * 1.35, -.38), J(5.4, s * .8, -.33), J(6.5, s * .15, -.3)],
                     lambda u: rrect(.3 - .06 * u, .32, 1.15 - .6 * u, .6, 16), up=jaw_up, per=5, px=True, grit=.06))
    for i in range(18):
        f = i / 17; x = .7 + 5.6 * f; y = s * (1.82 - 1.55 * f ** 1.3)
        size = (.5 + .5 * math.sin(math.pi * min(1, f * 1.1)) ** .8) * random.uniform(.8, 1.15)
        fang(f"Shark lower fang {i}", J(x, y, -.1), R @ Vector((0, -s * .15, 1)), R @ Vector((0, s, 0)), size * .42, size, .2)
jcut = cutters("Shark jaw", [(J(2.0, s * 1.95, -.95), (.75, .32, .42)) for s in (-1, 1)])
for j in jaws: carve(j, jcut)
loft("Shark jaw hinge", [J(-.25, -1.9, -.5), J(-.25, 1.9, -.5)], taper(.42, .42, .9, 12), up=jaw_up, per=3)

join("Shark skull and upper teeth", "Shark spine and ribs 0", sp(0), 7000)
join("Shark lower jaw", "Shark skull and upper teeth", H, 3300)
for s in range(1, SEG): join(f"Shark spine and ribs {s}", f"Shark spine and ribs {s-1}", sp(s / SEG), 6000)
join("Shark dorsal fin", f"Shark spine and ribs {seg(.25)}", sp(.25) + Vector((0, 0, 1.5)), 3600)
join("Shark tail fin", f"Shark spine and ribs {SEG-1}", sp(1), 4300)
for label in ("left", "right"): join(f"Shark {label} flipper", "Shark spine and ribs 0", (5.9, (-1 if label == "left" else 1) * 1.2, 12.3), 3600)
