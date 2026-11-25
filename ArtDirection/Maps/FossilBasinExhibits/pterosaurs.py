# Perched pterosaur skeletons (run after common.py). Each is built in a local frame:
# feet on z = 0, facing -Y. transformed() then places it on the roof walkway.

def _V(p): return Vector(p)

def build_pterosaur(label, P, group):
    def side(p, s): return Vector((p[0] * s, p[1], p[2]))
    base, bdir, L, w = _V(P["skull_base"]), _V(P["beak_dir"]).normalized(), P["beak_len"], P["skull_w"]
    lat = Vector((1, 0, 0)); upv = lat.cross(bdir).normalized() * -1
    if upv.z < 0: upv = -upv
    member(f"{label} skull", group, "bone")
    def prof(u):
        bulge = 1 + .3 * math.exp(-(u / .12) ** 2)
        return w * (1 - u) ** .85 * bulge + .015, w * .95 * (1 - u) ** .85 * bulge + .015, w * .85 * (1 - u) ** .8 * bulge + .02
    ctrl = [base + bdir * L * f for f in (0, .25, .5, .75, 1)]
    skull = loft(f"{label} skull", ctrl, lambda u: rrect(*prof(u), .7, 18), up=upv, per=4)
    proxy(base + bdir * L * .2, (w * 2.2, L * .45, w * 2.0), frame(base, base + bdir)[0]); proxy(base + bdir * L * .65, (w * 1.2, L * .55, w * 1.0), frame(base, base + bdir)[0])
    items = []
    for s in (-1, 1):
        items.append((base + bdir * L * .12 + lat * s * w * 1.05 + upv * w * .25, (w * .45, w * .5, w * .45)))
        items.append((base + bdir * L * .33 + lat * s * w * .78, (w * .32, L * .13, w * .38)))
    carve(skull, cutters(f"{label} skull", items)); weather(skull, .025)
    crest = P.get("crest")
    if crest: plate(f"{label} crest", [base + _V(c) for c in crest], (1, 0, 0), .07 * w / .7, BONE)
    if P.get("teeth"):
        for s in (-1, 1):
            for i in range(14):
                f = .16 + .7 * i / 13; p = base + bdir * L * f
                pw, pt, pb = prof(f)
                fang(f"{label} upper tooth {i}", p + lat * s * pw * .75 - upv * pb * .85, -upv * 1 + lat * s * .35 + bdir * .25, lat * s,
                     .1 * w / .7, (.5 - .26 * f) * w / .7, .3)
    member(f"{label} lower jaw", group, "bone")
    hinge = base + bdir * L * .06 - upv * w * .7
    R = Matrix.Rotation(math.radians(P.get("jaw_open", 20)), 3, lat)
    jdir = R @ bdir; jup = R @ upv
    for s in (-1, 1):
        pts = [hinge + lat * s * w * .8, hinge + jdir * L * .35 + lat * s * w * .55, hinge + jdir * L * .7 + lat * s * w * .22, hinge + jdir * L * .92]
        loft(f"{label} mandible", pts, lambda u: rrect((.15 - .08 * u) * w / .7, .14 * w / .7, (.34 - .22 * u) * w / .7, .6, 12), up=jup, per=4, px=True)
        if P.get("teeth"):
            for i in range(13):
                f = .12 + .7 * i / 12
                fang(f"{label} lower tooth {i}", hinge + jdir * L * f * .92 + lat * s * w * (.75 - .6 * f), jup + lat * s * .3 + jdir * .25, lat * s,
                     .09 * w / .7, (.44 - .22 * f) * w / .7, .3)
    member(f"{label} neck", group, "bone")
    neck = [_V(p) for p in P["neck"]]
    for i, (a, b) in enumerate(zip(neck, neck[1:])):
        bone(f"{label} cervical {i}", [a, b], P["neck_r"], P["neck_r"] * .9, knob=1.35)
        mid = a.lerp(b, .5); up_n = (b - a).cross(lat).normalized()
        if up_n.z < 0: up_n = -up_n
        loft(f"{label} neural blade {i}", [a.lerp(b, .3) + up_n * P["neck_r"] * .8, mid + up_n * P["neck_r"] * 1.5, a.lerp(b, .7) + up_n * P["neck_r"] * .8],
             taper(.05, .03, 3, 6), up=(b - a), per=3, px=False)
    member(f"{label} torso", group, "bone")
    b0, b1 = _V(P["back"][0]), _V(P["back"][1]); back = [b0.lerp(b1, i / 6) for i in range(7)]
    loft(f"{label} notarium", back, taper(P["body_r"], P["body_r"] * .85, 1, 12), per=3)
    proxy((b0 + b1) / 2, (P["body_r"] * 3, (b1 - b0).length + .4, P["body_r"] * 3), frame(b0, b1)[0])
    down = Vector((0, 0, -1)); depth = P["chest"]
    for i, p in enumerate(back[1:-1]):
        loft(f"{label} dorsal spine {i}", [p + Vector((0, 0, P["body_r"] * .8)), p + Vector((0, .1, P["body_r"] * 2.1))], taper(.08, .04, 2.5, 6), up=(0, 1, 0), per=3)
        for s in (-1, 1):
            loft(f"{label} rib {i}", [p + Vector((s * .22, 0, 0)), p + Vector((s * .85 * depth, .08, -.4 * depth)), p + Vector((s * .92 * depth, .15, -.95 * depth)),
                                      p + Vector((s * .5 * depth, .2, -1.4 * depth)), p + Vector((s * .12, .22, -1.55 * depth))], taper(.075, .04, .6, 8), up=(0, 1, 0), per=4)
    keel = (b0 + b1) / 2 + Vector((0, -.3, -1.3 * depth))
    plate(f"{label} sternum keel", [keel + Vector((0, -.9, .45)), keel + Vector((0, .9, .45)), keel + Vector((0, .5, -.3)), keel + Vector((0, -.6, -.7))], (1, 0, 0), .07, BONE)
    proxy(keel, (1.6, 1.9, 1.3))
    member(f"{label} hips and tail", group, "bone")
    hip = _V(P["hip"])
    loft(f"{label} pelvis", [hip + Vector((-.65, .05, -.2)), hip, hip + Vector((.65, .05, -.2))], lambda u: rrect(.35, .25, .3, .7, 14), up=(0, 0, 1), per=4, px=True)
    loft(f"{label} tail", [_V(p) for p in P["tail"]], taper(.15, .04, 1, 10), per=4, px=True)
    for s, sname in ((-1, "left"), (1, "right")):
        member(f"{label} {sname} leg", group, "bone")
        legs = P["leg"]
        bone(f"{label} femur", [side(legs[0], s), side(legs[1], s)], .16 * P["limb_r"], .13 * P["limb_r"], knob=1.6)
        bone(f"{label} tibia", [side(legs[1], s), side(legs[2], s)], .12 * P["limb_r"], .09 * P["limb_r"], knob=1.6)
        ankle = side(legs[2], s)
        for k, toe in enumerate(P["toes"]):
            t = side(toe, s)
            bone(f"{label} toe {k}", [ankle, t], .05 * P["limb_r"], .04 * P["limb_r"], knob=1.4, px=False)
            fang(f"{label} toe claw {k}", t, (t - ankle).normalized() + Vector((0, 0, -1.4)), (0, 0, 1), .08 * P["limb_r"], .3 * P["limb_r"], .45, BONE)
        member(f"{label} {sname} arm", group, "bone")
        a = [side(p, s) for p in P["arm"]]   # shoulder, elbow, wrist, (knuckle)
        bone(f"{label} coracoid", [side(P["back"][0], s) + Vector((s * .25, 0, -.4)), a[0]], .12 * P["limb_r"], .14 * P["limb_r"])
        bone(f"{label} humerus", [a[0], a[0].lerp(a[1], .5), a[1]], .27 * P["limb_r"], .2 * P["limb_r"], knob=1.7)
        off = (a[2] - a[1]).cross(Vector((0, 1, 0))).normalized() * .13 * P["limb_r"]
        for k, o in enumerate((off, -off)):
            bone(f"{label} forearm", [a[1] + o, a[1].lerp(a[2], .5) + o, a[2] + o], .13 * P["limb_r"], .11 * P["limb_r"], knob=1.55, px=k == 0)
        loft(f"{label} carpus", [a[2] - Vector((s * .2, 0, 0)), a[2] + Vector((s * .2, 0, 0))], taper(.3 * P["limb_r"], .28 * P["limb_r"], .9, 12), per=3)
        hand = a[2]
        if len(a) > 3:
            bone(f"{label} metacarpal", [a[2], a[3]], .14 * P["limb_r"], .12 * P["limb_r"], knob=1.5)
            hand = a[3]
        for k in range(3):
            kn = hand + Vector((s * (.1 + .12 * k), -.45, -.1 * k - (.0 if len(a) > 3 else .2)))
            bone(f"{label} hand finger", [hand, kn], .055 * P["limb_r"], .045 * P["limb_r"], knob=1.4, px=False)
            fang(f"{label} hand claw", kn, (0, -.6, -.8), (0, 1, 0), .1 * P["limb_r"], .42 * P["limb_r"], .5, BONE)
        member(f"{label} {sname} wing finger", group, "bone")
        wing = [side(p, s) for p in P["wing"]]
        for j, (p0, p1) in enumerate(zip(wing, wing[1:])):
            bone(f"{label} wing phalanx {j}", [p0, p0.lerp(p1, .5) + Vector((0, 0, .05)), p1], (.2 - .04 * j) * P["limb_r"], (.17 - .04 * j) * P["limb_r"], flat=.8, knob=1.3)
        join(f"{label} {sname} leg", f"{label} hips and tail", side(legs[0], s), 3000)
        join(f"{label} {sname} arm", f"{label} torso", a[0], 4500)
        join(f"{label} {sname} wing finger", f"{label} {sname} arm", wing[0], 4000)
    join(f"{label} neck", f"{label} torso", neck[0], 4500)
    join(f"{label} skull", f"{label} neck", neck[-1], 4500)
    join(f"{label} lower jaw", f"{label} skull", hinge, 2500)
    join(f"{label} hips and tail", f"{label} torso", b1, 4500)

Z0 = 3.4
PTERODACTYL = dict(
    skull_base=(0, -2.15, Z0 + 3.45), beak_dir=(0, -1, .08), beak_len=6.6, skull_w=.7, jaw_open=32, teeth=True,
    crest=[(0, .1, .4), (0, 1.1, 1.0), (0, 2.4, 2.4), (0, 3.0, 2.9), (0, 3.1, 2.7), (0, 2.2, 1.6), (0, 1.0, .5), (0, -.8, .55)],
    neck=[(0, -.4, Z0 + .75), (0, -.9, Z0 + 1.75), (0, -1.45, Z0 + 2.65), (0, -1.9, Z0 + 3.2), (0, -2.2, Z0 + 3.4)], neck_r=.3,
    back=[(0, -.4, Z0 + .7), (0, 1.7, Z0 - .1)], body_r=.27, chest=1.0, hip=(0, 1.8, Z0 - .25),
    tail=[(0, 2.1, Z0 - .25), (0, 3.0, Z0 - .6), (0, 3.7, Z0 - 1.0)],
    leg=[(.6, 1.9, Z0 - .45), (.85, 1.35, Z0 - 1.8), (.95, 2.0, .35)], toes=[(.72, 1.45, .06), (.9, 1.35, .05), (1.08, 1.4, .05), (1.25, 1.55, .06)],
    arm=[(.9, -.2, Z0 + .9), (2.3, .1, Z0 + 2.2), (4.5, -.6, Z0 + 4.3)],
    wing=[(4.65, -.5, Z0 + 4.4), (6.6, .4, Z0 + 6.0), (8.5, 1.5, Z0 + 6.8), (9.9, 2.6, Z0 + 6.6), (10.9, 3.6, Z0 + 5.8)], limb_r=1.0)
QUETZAL = dict(
    skull_base=(0, -4.45, 12.15), beak_dir=(0, -1, -.45), beak_len=7.4, skull_w=.62, jaw_open=8, teeth=False,
    crest=[(0, .5, .35), (0, 1.4, 1.1), (0, 1.9, 1.15), (0, 1.5, .5), (0, .2, -.1)],
    neck=[(0, -.9, 6.0), (0, -1.9, 7.8), (0, -2.85, 9.6), (0, -3.65, 10.95), (0, -4.3, 11.85), (0, -4.5, 12.15)], neck_r=.36,
    back=[(0, -.7, 5.75), (0, 1.6, 4.65)], body_r=.33, chest=1.15, hip=(0, 1.7, 4.45),
    tail=[(0, 2.0, 4.4), (0, 2.8, 4.0), (0, 3.4, 3.6)],
    leg=[(.6, 1.75, 4.25), (.95, 1.3, 2.5), (1.05, 2.05, .45)], toes=[(.85, 1.55, .06), (1.0, 1.45, .05), (1.15, 1.5, .05), (1.3, 1.65, .06)],
    arm=[(.85, -.55, 5.35), (1.55, .15, 4.4), (1.45, -1.45, 2.65), (1.35, -1.85, .32)],
    wing=[(1.55, -1.6, .62), (1.85, -.3, 1.85), (2.05, 1.4, 3.05), (1.95, 2.8, 4.0), (1.75, 3.8, 4.65)], limb_r=1.25)
