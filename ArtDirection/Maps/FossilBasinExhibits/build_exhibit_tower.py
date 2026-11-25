# Builds "Fossil Exhibit Tower.blend" for Fossil Basin.
# Level 1: a monstrous shark skeleton hung from the ceiling beams.
# Level 2: an open platform with a giant dinosaur egg nest.
# Roof: a ridge walkway where a pterodactyl and a Quetzalcoatlus stand perched, facing off.
# A two-lane switchback stair tower with even 0.2 m steps joins the deck and level 2.
# Run: blender --background --python build_exhibit_tower.py -- "<output folder>"
import os, sys
HERE = sys.argv[sys.argv.index("--") + 1]
if "finish" not in globals(): exec(open(os.path.join(HERE, "common.py"), encoding="utf-8").read(), globals())
exec(open(os.path.join(HERE, "pterosaurs.py"), encoding="utf-8").read(), globals())

SHARK = coll("01 • Shark skeleton breakaway members")
PTERO = coll("02 • Roof pterosaurs breakaway members")
HALL = coll("03 • Hall frame, floors and roof")
CHAINS = coll("04 • Hoist chains")
STAIR = coll("05 • Stair tower")
NEST = coll("06 • Egg nest")

# The shark is built facing +X, then turned round so its jaws face west (toward the main map).
transformed(lambda: exec(open(os.path.join(HERE, "shark_hq.py"), encoding="utf-8").read(), globals()), (0, 0, 0), 180)
def F(v): return Vector((-v[0], -v[1], v[2]))

L1, L2 = 1.55, 23.4
BEAM = .62
XS, YE = 15.6, 8.2

def level(n, base, beam_z):
    for x in (-15.0, 0.0, 15.0):
        for y in (-7.6, 7.6): pier(f"L{n} column {x:+.0f}/{y:+.0f}", x, y, base, beam_z - BEAM / 2, HALL, size=1.1)
    for x in (-15.0, 0.0, 15.0):
        member(f"L{n} crossbeam {x:+.1f}", HALL, "timber")
        box("Crossbeam timber", (x, 0, beam_z), (.75, 2 * YE - .2, BEAM), "Oxide-stained beam", bevel=.06, px=True)
    for y in (-7.6, 7.6):
        member(f"L{n} edge beam {y:+.0f}", HALL, "timber")
        box("Edge beam timber", (0, y, beam_z), (2 * XS - .4, .7, BEAM), "Oxide-stained beam", bevel=.06, px=True)
        for x, d in ((-15.0, 1), (0.0, 1), (0.0, -1), (15.0, -1)):
            shaft("Knee brace", (x + .6 * d, y, beam_z - 2.6), (x + 2.6 * d, y, beam_z - .3), .26, .26, "Oxide-stained beam")
    member(f"L{n} ridge beam", HALL, "timber")
    box("Ridge beam timber", (0, 0, beam_z), (2 * XS - .4, .8, BEAM), "Oxide-stained beam", bevel=.06, px=True)

floor("L1 deck", L1, -XS, XS, -YE, YE, 4, HALL)
for x in (-14.2, -4.7, 4.7, 14.2):
    for y in (-6.8, 0, 6.8): pier(f"L1 deck pier {x:+.1f}/{y:+.1f}", x, y, -.03, L1 - TILE[2], HALL)
B1 = L2 - TILE[2] - BEAM / 2
level(1, L1, B1)
floor("L2 floor", L2, -XS, XS, -YE, YE, 4, HALL)
B2 = 37.95
level(2, L2, B2)
use("L1 deck strip 0")
for x, y in ((-15.2, -7.8), (-15.2, 7.8)): lantern(x, y, L1)
use("L1 deck strip 3")
lantern(15.2, 7.8, L1)
box("Shark plaque post", (3.2, -7.9, L1 + .5), (.16, .16, 1.0), "Oxide-stained beam")
box("Shark plaque", (3.2, -7.98, L1 + 1.05), (1.3, .08, .6), "Oil-dark brass", Quaternion(Vector((1, 0, 0)), math.radians(-20)), .02)
for s in range(4):
    use(f"L2 floor strip {s}")
    x0 = -XS + 2 * XS * s / 4
    for k in range(4):
        x = x0 + .4 + 1.95 * k
        for y in (-YE + .25, YE - .25): box("Railing post", (x, y, L2 + .55), (.14, .14, 1.1), "Oxide-stained beam", bevel=.02)
    box("Railing rope", (x0 + 3.9, -YE + .25, L2 + 1.0), (7.8, .06, .06), "Dark bronze", bevel=0)
    box("Railing rope", (x0 + 3.9, YE - .25, L2 + 1.0), (7.8, .06, .06), "Dark bronze", bevel=0)
use("L2 floor strip 0"); box("Railing rope", (-XS + .2, 0, L2 + 1.0), (.06, 2 * YE - .5, .06), "Dark bronze", bevel=0)
use("L2 floor strip 3"); box("Railing rope", (XS - .2, 1.0, L2 + 1.0), (.06, 2 * YE - 2.5, .06), "Dark bronze", bevel=0)
for x, y in ((-7.8, -7.8), (7.8, 7.8)):
    use(f"L2 floor strip {1 if x < 0 else 2}"); lantern(x, y, L2)

# Roof: rafters, terracotta slabs, ridge cap, and a plank walkway along the ridge for the perched skeletons.
RZ, RIDGE = B2 + BEAM / 2, B2 + 4.4
for x in (-15.0, -7.5, 0.0, 7.5, 15.0):
    member(f"Roof truss {x:+.1f}", HALL, "roof")
    for s in (-1, 1): shaft("Rafter", (x, s * (YE + .5), RZ), (x, 0, RIDGE), .32, .32, "Oxide-stained beam", px=True)
    shaft("King post", (x, 0, RZ), (x, 0, RIDGE), .28, .28, "Oxide-stained beam")
    shaft("Tie beam", (x, -YE, RZ + .1), (x, YE, RZ + .1), .26, .26, "Oxide-stained beam")
slope = math.atan2(RIDGE - RZ, YE + .5)
for s in (-1, 1):
    for half in (-1, 1):
        member(f"Roof panel {'south' if s < 0 else 'north'} {'west' if half < 0 else 'east'}", HALL, "roof")
        q = Quaternion(Vector((1, 0, 0)), -s * slope)
        length = math.hypot(RIDGE - RZ, YE + .5) + .4
        for i in range(7):
            x = half * (1.17 + 2.34 * i)
            for j in range(4):
                along = (j + .5) / 4 * length
                c = Vector((x, s * (YE + .7) - s * math.cos(slope) * along, RZ + .3 + math.sin(slope) * along))
                box("Roof slab", c, (2.3, length / 4 - .05, .3), random.choice(CLAYS), q, .06)
        proxy(Vector((half * 8.2, s * (YE + .7) / 2, (RZ + .3 + RIDGE + .3) / 2)), (16.4, length, .3), q)
member("Roof ridge", HALL, "roof")
box("Ridge cap", (0, 0, RIDGE + .45), (2 * XS + 1.2, .9, .45), "Weathered clay rim", bevel=.08, px=True)
for x in (-16.2, 16.2): box("Ridge finial", (x, 0, RIDGE + .95), (.5, .5, .6), "Oil-dark brass", bevel=.06)
WZ = RIDGE + 1.05
member("Roof perch walkway", HALL, "deck")
for i in range(60):
    box("Walkway plank", (-14.75 + i * .5, 0, WZ - .1), (.48, 3.2, .2), "Oxide-stained beam", bevel=.02)
proxy((0, 0, WZ - .1), (30, 3.2, .2))
for x in (-14.0, -7.0, 0.0, 7.0, 14.0):
    for y in (-1.4, 1.4):
        foot = RIDGE + .3 - abs(y) * math.tan(slope)
        box("Walkway post", (x, y, (WZ - .2 + foot) / 2), (.22, .22, WZ - .2 - foot), "Oxide-stained beam", bevel=.02)

# Level 2: a giant egg nest on a low terracotta dais.
egg_mat = bpy.data.materials.new("Speckled dinosaur egg"); egg_mat.use_nodes = True
nt = egg_mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
vor = nt.nodes.new("ShaderNodeTexVoronoi"); vor.inputs["Scale"].default_value = 9
ramp = nt.nodes.new("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].position = .08; ramp.color_ramp.elements[0].color = (.22, .13, .08, 1)
ramp.color_ramp.elements[1].position = .16; ramp.color_ramp.elements[1].color = (.86, .80, .66, 1)
nt.links.new(vor.outputs["Distance"], ramp.inputs["Fac"]); nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = .55; M["Speckled dinosaur egg"] = egg_mat
for name, rgb in (("Nest straw", (.55, .40, .18)), ("Nest twigs", (.20, .12, .07))):
    m = bpy.data.materials.new(name); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1); M[name] = m
member("Nest dais", NEST, "deck")
for i in range(-4, 5):
    for j in range(-5, 6):
        if (i * 1.25) ** 2 + (j * 1.17) ** 2 > 5.4 ** 2: continue
        box("Dais stone", (i * 1.25, j * 1.17, L2 + .16), (1.22, 1.14, .32), random.choice(CLAYS), bevel=.06)
proxy((0, 0, L2 + .16), (11.0, 10.5, .32))
D0 = L2 + .32
member("Egg nest", NEST, "prop")
rnd = random.Random(5)
for i in range(280):
    a = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(2.8, 4.6); h = rnd.uniform(0, 1.5) * max(0, 1 - abs(r - 3.7) / 1.8)
    c = Vector((math.cos(a) * r, math.sin(a) * r, D0 + .1 + h))
    tang = (Vector((-math.sin(a), math.cos(a), rnd.uniform(-.25, .25))) + Vector((rnd.uniform(-.4, .4), rnd.uniform(-.4, .4), 0))).normalized()
    L = rnd.uniform(1.8, 3.4); bend = Vector((math.cos(a), math.sin(a), 0)) * rnd.uniform(-.15, .15)
    loft("Nest twig", [c - tang * L / 2, c + bend, c + tang * L / 2], taper(rnd.uniform(.07, .12), .03, 1, 6), "Nest twigs", up=(0, 0, 1), per=3, sub=1, grit=0)
plate("Nest straw bed", [(math.cos(2 * math.pi * k / 18) * 3.4, math.sin(2 * math.pi * k / 18) * 3.4, D0 + .15) for k in range(18)], (0, 0, 1), .1, "Nest straw")
proxy((0, 0, D0 + .8), (9.4, 9.4, 1.6))
def egg(name, centre, axis, angle, a=.82, b=1.15):
    member(name, NEST, "egg")
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=1)
    q = Quaternion(Vector(axis), angle)
    for v in bm.verts:
        z = v.co.z; k = 1 - .16 * z
        v.co = q @ Vector((v.co.x * a * k, v.co.y * a * k, z * b)) + Vector(centre)
    o = mesh_obj(name, bm, "Speckled dinosaur egg"); o.data.shade_smooth()
    sm = o.modifiers.new("Smooth", "SUBSURF"); sm.levels = 1; sm.render_levels = 1
    proxy(centre, (2 * a, 2 * a, 2 * b), q)
    join(name, "Egg nest", Vector(centre) - Vector((0, 0, b * .9)), 600)
for k in range(6):
    ang = 2 * math.pi * k / 6 + .3
    egg(f"Dinosaur egg {k}", (math.cos(ang) * 1.75, math.sin(ang) * 1.75, D0 + 1.25), (-math.sin(ang), math.cos(ang), 0), math.radians(18))
egg("Dinosaur egg 6", (0, 0, D0 + 1.3), (1, 0, 0), 0)
join("Egg nest", "Nest dais", (0, 0, D0), 3000)
use("Nest dais")
box("Nest plaque post", (-6.2, -4.6, L2 + .5), (.16, .16, 1.0), "Oxide-stained beam")
box("Nest plaque", (-6.2, -4.68, L2 + 1.05), (1.3, .08, .6), "Oil-dark brass", Quaternion(Vector((1, 0, 0)), math.radians(-20)), .02)

# Roof pterosaurs, perched on the ridge walkway and facing each other.
transformed(lambda: build_pterosaur("Quetzalcoatlus", QUETZAL, PTERO), (5.6, 0, WZ), -90)
transformed(lambda: build_pterosaur("Pterodactyl", PTERODACTYL, PTERO), (-8.2, 0, WZ), 90, .85)
for label, limbs in (("Quetzalcoatlus", ("leg", "arm")), ("Pterodactyl", ("leg",))):
    for side in ("left", "right"):
        for limb in limbs:
            low = min((cc for cc, _, _ in members[f"{label} {side} {limb}"]["proxies"]), key=lambda v: v.z)
            join(f"{label} {side} {limb}", "Roof perch walkway", (low.x, low.y, WZ), 5000)

# Shark hoist chains.
TOP1 = B1 - BEAM / 2
chain("shark skull", (-7.6, 0, TOP1), (-7.6, 0, 14.6), "L1 ridge beam", "Shark skull and upper teeth", .17, CHAINS)
for t, label in ((.5, "shark back"), (.72, "shark tail")):
    p = F(sp(t)); top_of = p + Vector((0, 0, cr(t) + 1.2 * girth(t) + .9))
    chain(label, (p.x, 0, TOP1), top_of, "L1 ridge beam", f"Shark spine and ribs {seg(t)}", .17, CHAINS)
chain("shark snout guy", (-15.0, 6.0, TOP1 - .2), (-12.8, 0, 12.6), "L1 crossbeam -15.0", "Shark skull and upper teeth", .12, CHAINS)
chain("shark tail guy", (15.0, -6.0, TOP1 - .2), F(sp(.95)) + Vector((0, 0, .4)), "L1 crossbeam +15.0", "Shark tail fin", .12, CHAINS)

# Stair tower on the east end: deck (landing 0) to level 2 (landing 4), bridges on the south landings.
SX0, SY0 = 16.5, -9.4
stair_tower("Stair", SX0, SY0, L1, L2, 4, STAIR, openings=(0, 4))
for z, deck, land, label in ((L1, "L1 deck strip 3", 0, "Stair bridge to deck"), (L2, "L2 floor strip 3", 4, "Stair bridge to level 2")):
    bridge(label, (XS - .1, -7.7), (SX0 + .1, -7.7), 1.6, z, STAIR)
    join(label, deck, (XS, -7.7, z - .2), 5200)
    join(label, f"Stair landing {land} strip 0", (SX0, -7.7, z - .2), 5200)

make_tar(HALL)
finish("Fossil Exhibit Tower", [
    ("01 • Tower overview", (48, -60, 30), (0, 0, 24), 30),
    ("02 • Under the shark jaws", (-15.3, -5.0, L1 + 1.7), (-10.5, 0, 10.5), 17),
    ("03 • Level 1 hall", (13.5, -6.2, L1 + 1.7), (-4, 1.5, 11), 18),
    ("04 • Level 2 egg nest", (10.5, -6.8, L2 + 1.8), (0, 0, L2 + 1.0), 22),
    ("05 • Stair tower", (34, -22, 9), (19, -1, 11), 26),
    ("06 • Roof perch", (22, -30, 52), (-1, 0, 48), 30),
    ("07 • Shark skull close", (-14.6, -5.6, 13.6), (-8.8, 0, 11.9), 26),
    ("08 • Side elevation", (0, -86, 26), (0, 0, 26), 30)])
