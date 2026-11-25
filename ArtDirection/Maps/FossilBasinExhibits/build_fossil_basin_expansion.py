# Places the Fossil Exhibit Tower east of the Faceoff arena and the Brontosaurus Stairway west of it,
# with walkable links, inside a copy of "Fossil Basin Faceoff.blend".
# Run: blender --background "<...>/Concept Rebuild/Fossil Basin Faceoff.blend" --python build_fossil_basin_expansion.py -- "<Megalodon Hoist folder>"
# Map coordinates here are Blender's: x = Unity x, y = Unity z, z = height (tar at z = 0).
import os, sys
HERE = sys.argv[sys.argv.index("--") + 1]
os.environ["FB_COMPOSE"] = "1"
exec(open(os.path.join(HERE, "common.py"), encoding="utf-8").read(), globals())
def finish(*args, **kwargs): pass          # the pieces export their own physics files when built alone
def make_tar(group): pass                   # the arena already has its tar
LINKS = coll("08 • Expansion links")

# East: the exhibit tower. Its west deck edge sits at x = 33 (tower deck spans y -12.2 to 4.2).
TOWER_AT = (48.6, -4.0, 0.0)
transformed(lambda: exec(open(os.path.join(HERE, "build_exhibit_tower.py"), encoding="utf-8").read(), globals()), TOWER_AT, 0)
# A stair from the outer Triceratops landing (C5, height 6.40) down to the tower deck (1.55),
# and a low causeway from the eastern sample wharf (A4, height 1.83).
flight("Link stair Triceratops landing to tower", (25.9, -2.9, 6.40), (33.0, -2.9, 1.55), 2.4, LINKS)
bridge("Link causeway wharf to tower", (13.5, -16.9), (33.0, -10.5), 1.8, 1.69, LINKS, piers=True)

# West: the brontosaurus, head toward the T. rex outer switchback (R5, height 18.55),
# so a short bridge lands on its flat skull crown and survivors can walk down the neck ridge.
BRONTO_AT = (-53.15, 7.25, -0.51)
transformed(lambda: exec(open(os.path.join(HERE, "build_brontosaurus.py"), encoding="utf-8").read(), globals()), BRONTO_AT, 0)
bridge("Link bridge T rex switchback to brontosaurus skull", (-30.8, 7.25), (-32.45, 7.25), 1.6, 18.55, LINKS)
# A low causeway from the west tide stair landing (B1, height 3.38) to the front left foot platform.
bridge("Link causeway tide landing to brontosaurus", (-25.9, -7.4), (-46.1, 4.6), 1.8, 3.04, LINKS, piers=True)

shots = [cam("Expansion 01 • Overview", (0, -178, 72), (-10, 0, 8), 30),
         cam("Expansion 02 • East tower link", (66, -62, 28), (32, -6, 9), 28),
         cam("Expansion 03 • West brontosaurus link", (-14, -50, 30), (-40, 5, 13), 28),
         cam("Expansion 04 • Stepping onto the brontosaurus", (-29.2, 7.25, 20.25), (-52, 7.0, 13.5), 22)]
s = scene; s.render.engine = "CYCLES"; s.cycles.samples = 32; s.cycles.use_denoising = True
s.render.resolution_x, s.render.resolution_y = 1600, 900
OUTDIR = os.path.dirname(HERE)
QUICK = os.environ.get("TOWER_QUICK")
for c in shots:
    if QUICK and c.name[10:12] not in QUICK.split(","): continue
    s.camera = c; s.render.filepath = os.path.join(OUTDIR, "Expansion", c.name.replace(" • ", " ") + ".png")
    bpy.ops.render.render(write_still=True); print("TOWER rendered", s.render.filepath)
if not QUICK:
    s.camera = shots[0]
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUTDIR, "Expansion", "Fossil Basin Expansion.blend"), compress=True)
    print("TOWER saved expansion", len(bpy.data.objects))
