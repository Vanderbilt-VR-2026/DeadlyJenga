"""Builds the three map-sized, walk-in, collapsible toy buildings for Underneath the Tree:
a doll mansion, a superhero base and a wizard tower. Each is one FBX whose parts are separate
objects named "Piece ..." so the game gives every part its own body. Every part is a simple solid
shape (the game wraps a collision shape round each one); parts sit on each other with a small gap.
Scale: players are 0.85 units tall and step up 1.2, so storeys are 4.5, doors 2.6 wide and
stairs rise 1.0 a step.
Run: blender -b -P toysets.py -- <output folder> [preview.png]
Generic designs, no brands."""
import bpy, bmesh, math, sys, os

out = sys.argv[sys.argv.index("--") + 1]
preview = sys.argv[sys.argv.index("--") + 2] if len(sys.argv) > sys.argv.index("--") + 2 else None
os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

PAINT = {
    "Toy Red": (.75, .03, .04), "Toy Blue": (.04, .18, .75), "Toy Navy": (.03, .06, .30),
    "Toy Yellow": (1., .78, .08), "Toy White": (.92, .92, .9), "Toy Gold": (1., .68, .2),
    "Toy Silver": (.75, .77, .8), "Toy Pink": (1., .45, .65), "Toy Purple": (.35, .1, .6),
    "Toy Gray": (.3, .31, .33), "Toy Glass": (.6, .85, 1.), "Toy Cream": (.96, .9, .78),
    "Toy Wood": (.6, .38, .2),
}
GAP = .04
STOREY, SLAB, WALL = 4.5, .5, .6
DOOR_W = 2.6
pieces = []

def paint(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (*PAINT[name], 1)
    m.diffuse_color = (*PAINT[name], 1)
    return m

def part(name, kind, loc, size, mat, yaw=0.0, tilt=(0, 0), seg=32):
    """One separately falling part. Its turn stays on the object so its shape stays tight."""
    ops = bpy.ops.mesh
    if kind == "box": ops.primitive_cube_add(size=1)
    elif kind == "cyl": ops.primitive_cylinder_add(vertices=seg, radius=.5, depth=1)
    elif kind == "cone": ops.primitive_cone_add(vertices=seg, radius1=.5, radius2=0, depth=1)
    elif kind == "ball": ops.primitive_uv_sphere_add(segments=16, ring_count=8, radius=.5)
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if kind != "box":
        bpy.ops.object.shade_smooth()
    o.location = loc
    o.rotation_euler = (math.radians(tilt[0]), math.radians(tilt[1]), math.radians(yaw))
    o.data.materials.append(paint(mat))
    o.name = "Piece %03d %s" % (len(pieces) + 1, name)
    pieces.append(o)
    return o

def wedge(name, z, height, r, a0, a1, mat):
    """A pie-slice floor tile from the centre out to radius r (a solid, convex shape)."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    pts = [(0, 0)] + [(math.cos(a) * r, math.sin(a) * r) for a in (a0, (a0 + a1) / 2, a1)]
    bottom = [bm.verts.new((x, y, -height / 2)) for x, y in pts]
    top = [bm.verts.new((x, y, height / 2)) for x, y in pts]
    bm.faces.new(bottom[::-1]); bm.faces.new(top)
    for i in range(len(pts)):
        j = (i + 1) % len(pts)
        bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
    bm.to_mesh(mesh); bm.free()
    o = bpy.data.objects.new("Piece %03d %s" % (len(pieces) + 1, name), mesh)
    bpy.context.collection.objects.link(o)
    o.location = (0, 0, z + height / 2)
    o.data.materials.append(paint(mat))
    pieces.append(o)
    return o

def export(name):
    global pieces
    bpy.ops.object.select_all(action="DESELECT")
    for p in pieces:
        p.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, name + ".fbx"), use_selection=True,
        axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True)
    done, pieces = list(pieces), []
    return done

def wall_x(label, x0, x1, y, z, h, door, mat, door_w=DOOR_W):
    """Wall running along x. With a door it is two parts with a full-height opening between."""
    if not door:
        part(label, "box", ((x0 + x1) / 2, y, z + h / 2), (x1 - x0 - GAP, WALL, h), mat)
        return
    mid = (x0 + x1) / 2
    part(label + " left", "box", ((x0 + mid - door_w / 2) / 2, y, z + h / 2), (mid - door_w / 2 - x0 - GAP, WALL, h), mat)
    part(label + " right", "box", ((mid + door_w / 2 + x1) / 2, y, z + h / 2), (x1 - mid - door_w / 2 - GAP, WALL, h), mat)

def wall_y(label, x, y0, y1, z, h, door, mat):
    if not door:
        part(label, "box", (x, (y0 + y1) / 2, z + h / 2), (WALL, y1 - y0 - GAP, h), mat)
        return
    mid = (y0 + y1) / 2
    part(label + " near", "box", (x, (y0 + mid - DOOR_W / 2) / 2, z + h / 2), (WALL, mid - DOOR_W / 2 - y0 - GAP, h), mat)
    part(label + " far", "box", (x, (mid + DOOR_W / 2 + y1) / 2, z + h / 2), (WALL, y1 - mid - DOOR_W / 2 - GAP, h), mat)

def stair_flight(label, x, y, z, along_x, mat, steps=5, run=1.6, width=3.0):
    """Solid stepped blocks standing on the floor, each 1.0 taller than the last."""
    for k in range(steps):
        h = 1.0 * (k + 1) - GAP
        off = run * (k + .5)
        loc = (x + off, y, z + h / 2) if along_x else (x, y + off, z + h / 2)
        size = (run - GAP, width, h) if along_x else (width, run - GAP, h)
        part("%s step %d" % (label, k + 1), "box", loc, size, mat)

def grid_building(prefix, ox, oy, cols, rows, cell, floors, wall_mat, floor_mat, front_doors, back_doors=(),
                  stair_cell=(0, 0), roof_access=False, front_door_w=DOOR_W, z0=0.0, base=True):
    """A building on a grid of square rooms: walls on every room edge, doorways between all rooms,
    one stair room per floor going up, floor tiles over every room except above the stairs."""
    W, D = cols * cell, rows * cell
    x0, y0 = ox - W / 2, oy - D / 2
    z = z0
    if base:
        part(prefix + " ground slab", "box", (ox, oy, z + SLAB / 2), (W + 1, D + 1, SLAB), floor_mat)
        z += SLAB + GAP
    for level in range(floors):
        mat = wall_mat(level) if callable(wall_mat) else wall_mat
        for j in range(rows + 1):
            for i in range(cols):
                outside = j in (0, rows)
                door = (not outside) or (level == 0 and ((j == 0 and i in front_doors) or (j == rows and i in back_doors)))
                width = front_door_w if (level == 0 and j == 0 and i in front_doors) else DOOR_W
                wall_x("%s L%d wall x%d-%d" % (prefix, level, i, j), x0 + i * cell, x0 + (i + 1) * cell,
                       y0 + j * cell, z, STOREY, door, mat, width)
        for i in range(cols + 1):
            for j in range(rows):
                outside = i in (0, cols)
                wall_y("%s L%d wall y%d-%d" % (prefix, level, i, j), x0 + i * cell,
                       y0 + j * cell + WALL / 2 + GAP, y0 + (j + 1) * cell - WALL / 2 - GAP, z, STOREY, not outside, mat)
        going_up = level < floors - 1 or roof_access
        sc, sr = stair_cell
        if going_up:
            stair_flight("%s L%d stair" % (prefix, level), x0 + sc * cell + WALL + .3, y0 + sr * cell + cell / 2,
                         z, True, "Toy Wood")
        z += STOREY + GAP
        for i in range(cols):
            for j in range(rows):
                if going_up and (i, j) == (sc, sr):
                    continue
                part("%s L%d floor %d-%d" % (prefix, level, i, j), "box",
                     (x0 + (i + .5) * cell, y0 + (j + .5) * cell, z + SLAB / 2),
                     (cell - GAP + (WALL if i in (0, cols - 1) else 0) * 0, cell - GAP, SLAB), floor_mat)
        z += SLAB + GAP
    return z, (x0, y0, W, D)

sets = {}

# ---------------- Doll mansion: three floors of six rooms, pitched pink roof, chimneys.
top, (x0, y0, W, D) = grid_building("mansion", 0, 0, 3, 2, 15, 3,
    lambda level: "Toy Pink", "Toy White", front_doors=(1,), back_doors=(1,), stair_cell=(0, 1))
for s in (-1, 1):
    part("mansion roof half", "box", (0, s * D / 4, top + D / 4 * math.tan(math.radians(28)) / 2 + .3),
         (W + 2, D / 2 / math.cos(math.radians(28)) + 1, .6), "Toy Pink", tilt=(s * -28, 0))
for x in (-W / 3, W / 3):
    part("mansion chimney", "box", (x, D / 4, top + 4.2), (2.4, 2.4, 8.4), "Toy White")
for x in (-W / 2 + 7.5, W / 2 - 7.5):
    part("mansion porch column", "cyl", (x, y0 - 3, SLAB + GAP + STOREY / 2), (1.2, 1.2, STOREY), "Toy White")
sets["Toy Doll Mansion"] = export("Toy Doll Mansion")

# ---------------- Superhero base: two floors with a wide garage door, stairs to the roof deck,
# and a three-storey red tower standing on the roof.
top, (x0, y0, W, D) = grid_building("hero", 0, 0, 3, 2, 15, 2,
    lambda level: "Toy Blue" if level == 0 else "Toy Navy", "Toy Gray", front_doors=(0, 2), stair_cell=(1, 1),
    roof_access=True, front_door_w=8)
# Roof rails, then the tower in one corner of the roof (it starts on the roof tiles).
ttop, _ = grid_building("hero tower", x0 + 7.5 + 15, y0 + 7.5, 1, 1, 15, 3,
    "Toy Red", "Toy Gray", front_doors=(0,), stair_cell=(0, 0), z0=top, base=False)
part("hero emblem", "cyl", (x0 + 22.5, y0 - .35, top + STOREY * 2.2), (6, 6, .3), "Toy Yellow", tilt=(90, 0), seg=6)
part("hero radar mast", "cyl", (x0 + 22.5, y0 + 7.5, ttop + 1.5), (.8, .8, 3.0), "Toy Silver")
part("hero radar dish", "cyl", (x0 + 22.5, y0 + 7.5, ttop + 3.0 + GAP + .4), (7, 7, .6), "Toy Silver", tilt=(25, 0))
sets["Toy Superhero Base"] = export("Toy Superhero Base")

# ---------------- Wizard tower: five round floors, inside stairs, battlements and a cone roof.
R, PANELS, WEDGES = 12.0, 12, 8
z = 0
part("tower ground slab", "cyl", (0, 0, SLAB / 2), (2 * R + 2, 2 * R + 2, SLAB), "Toy Gray", seg=48)
z = SLAB + GAP
width = 2 * R * math.tan(math.pi / PANELS) - GAP * 3
for level in range(5):
    mat = "Toy Gray" if level < 2 else "Toy Purple"
    for k in range(PANELS):
        if level == 0 and k == 9:          # doorway on the ground floor
            continue
        a = k * 360 / PANELS
        r = math.radians(a)
        part("tower L%d wall %d" % (level, k), "box", (math.cos(r) * R, math.sin(r) * R, z + STOREY / 2),
             (WALL, width, STOREY), mat, yaw=a)
    # Stairs climb outward along one direction, turning a quarter each floor.
    stair = level * 90.0
    if level < 4:
        sr = math.radians(stair)
        for k in range(5):
            h = 1.0 * (k + 1) - GAP
            d = 3.2 + 1.6 * (k + .5)
            part("tower L%d step %d" % (level, k + 1), "box", (math.cos(sr) * d, math.sin(sr) * d, z + h / 2),
                 (1.6 - GAP, 3.4, h), "Toy Wood", yaw=stair)
    z += STOREY + GAP
    for k in range(WEDGES):
        a0, a1 = k * math.tau / WEDGES, (k + 1) * math.tau / WEDGES
        mid = math.degrees((a0 + a1) / 2)
        if level < 4 and abs((mid - stair + 180) % 360 - 180) < 50:
            continue                       # leave a hole above the stairs
        wedge("tower L%d floor %d" % (level, k), z, SLAB, R + .5, a0 + .004, a1 - .004,
              "Toy Gray" if level < 4 else "Toy Purple")
    z += SLAB + GAP
for k in range(12):
    a = math.radians(k * 30 + 15)
    part("tower battlement %d" % k, "box", (math.cos(a) * (R - .2), math.sin(a) * (R - .2), z + 1.0),
         (1.6, 2.4, 2.0), "Toy Gray", yaw=k * 30 + 15)
part("tower roof", "cone", (0, 0, z + 9), (2 * R - 4, 2 * R - 4, 18), "Toy Blue", seg=48)
part("tower star", "ball", (0, 0, z + 18 + GAP + 1.4), (2.8, 2.8, 2.8), "Toy Yellow")
sets["Toy Wizard Tower"] = export("Toy Wizard Tower")

print("SETS", {k: len(v) for k, v in sets.items()})

if preview:
    off = {"Toy Wizard Tower": -60, "Toy Superhero Base": 0, "Toy Doll Mansion": 60}
    for name, ps in sets.items():
        for p in ps:
            p.location.x += off[name]
    bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(30)))
    bpy.context.active_object.data.energy = 3
    bpy.ops.object.camera_add(location=(0, -150, 80), rotation=(math.radians(64), 0, 0))
    bpy.context.scene.camera = bpy.context.active_object
    w = bpy.data.worlds.new("w"); w.color = (.15, .15, .2); bpy.context.scene.world = w
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            bpy.context.scene.render.engine = engine
            break
        except TypeError:
            pass
    bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = 1600, 900
    bpy.context.scene.render.filepath = preview
    bpy.ops.render.render(write_still=True)
