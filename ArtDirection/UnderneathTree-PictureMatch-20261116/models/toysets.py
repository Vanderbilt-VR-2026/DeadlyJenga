"""Builds the three map-sized, walk-in, collapsible toy buildings for Underneath the Tree:
a doll mansion, a superhero base and a wizard tower, styled like children's play sets with one
side left open so you can see and play inside.

Each building is one FBX. Its parts are separate objects named "Piece ..." and the game gives each
one its own body, so every part must be a simple solid shape. Decoration (windows, trim, rugs,
screens, books) is attached to a piece as a child named "Detail ...": it moves with that piece but
has no collision, so it costs nothing in physics. Furniture big enough to climb on is a piece.
Every piece must rest on a piece below it (nothing hangs off a wall or balances on a point): the
game stacks the pieces onto each other when it builds them, so floors can be made thicker, thinner
or bigger here without the building bursting apart, but a piece with nothing under it still falls.
After changing this file, re-export and run Tools > Deadly Jenga > Underneath the Tree > Check Toy
Building Stability in Unity: it frees every piece and reports any that slide, tip or float.
Scale: players are 0.85 units tall and step up 1.2, so storeys are 4.5, doors 2.6 wide and stairs
rise 1.0 a step. Run: blender -b -P toysets.py -- <output folder> [preview.png]
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
    "Toy Wood": (.6, .38, .2), "Toy Green": (.05, .55, .12), "Toy Black": (.02, .02, .025),
    "Toy Teal": (.0, .55, .55), "Toy Orange": (1., .4, .05), "Toy Brown": (.35, .18, .08),
    # Glowing paints (the game makes these emit light).
    "Toy Window Glow": (1., .78, .45), "Toy Screen Glow": (.2, .9, 1.), "Toy Magic Glow": (.75, .3, 1.),
}
GAP = .04
STOREY, SLAB, WALL = 4.5, .5, .6
DOOR_W = 2.6
pieces = []

def paint(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*PAINT[name], 1)
    if "Glow" in name:
        bsdf.inputs["Emission Color"].default_value = (*PAINT[name], 1)
        bsdf.inputs["Emission Strength"].default_value = 3.0
    m.diffuse_color = (*PAINT[name], 1)
    return m

def shape(kind, size, seg=32):
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
    return o

def place(o, loc, mat, yaw=0.0, tilt=(0, 0)):
    o.location = loc
    o.rotation_euler = (math.radians(tilt[0]), math.radians(tilt[1]), math.radians(yaw))
    o.data.materials.append(paint(mat))
    return o

def part(name, kind, loc, size, mat, yaw=0.0, tilt=(0, 0), seg=32):
    """A separately falling part. Its turn stays on the object so its collision shape stays tight."""
    o = place(shape(kind, size, seg), loc, mat, yaw, tilt)
    o.name = "Piece %03d %s" % (len(pieces) + 1, name)
    pieces.append(o)
    return o

def detail(owner, name, kind, loc, size, mat, yaw=0.0, tilt=(0, 0), seg=24):
    """Decoration fixed to a piece: it moves with it but has no collision."""
    o = place(shape(kind, size, seg), loc, mat, yaw, tilt)
    o.name = "Detail %s" % name
    bpy.context.view_layer.update()
    o.parent = owner
    o.matrix_parent_inverse = owner.matrix_world.inverted()
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

def ring_piece(name, z, height, r0, r1, a0, a1, mat):
    """A short curved strip of floor between two radii (solid and convex because it is short)."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    pts = [(math.cos(a) * r0, math.sin(a) * r0) for a in (a1, a0)] + [(math.cos(a) * r1, math.sin(a) * r1) for a in (a0, a1)]
    bottom = [bm.verts.new((x, y, -height / 2)) for x, y in pts]
    top = [bm.verts.new((x, y, height / 2)) for x, y in pts]
    bm.faces.new(bottom[::-1]); bm.faces.new(top)
    for i in range(len(pts)):
        j = (i + 1) % len(pts)
        bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
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
        for c in p.children:
            c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, name + ".fbx"), use_selection=True,
        axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True)
    done, pieces = list(pieces), []
    return done

# ---------- walls and rooms

def wall_x(label, x0, x1, y, z, h, door, mat, door_w=DOOR_W):
    """Wall along x; with a door it is two parts with a full-height opening between. Returns the parts."""
    if not door:
        return [part(label, "box", ((x0 + x1) / 2, y, z + h / 2), (x1 - x0 - GAP, WALL, h), mat)]
    mid = (x0 + x1) / 2
    return [part(label + " left", "box", ((x0 + mid - door_w / 2) / 2, y, z + h / 2), (mid - door_w / 2 - x0 - GAP, WALL, h), mat),
            part(label + " right", "box", ((mid + door_w / 2 + x1) / 2, y, z + h / 2), (x1 - mid - door_w / 2 - GAP, WALL, h), mat)]

def wall_y(label, x, y0, y1, z, h, door, mat):
    if not door:
        return [part(label, "box", (x, (y0 + y1) / 2, z + h / 2), (WALL, y1 - y0 - GAP, h), mat)]
    mid = (y0 + y1) / 2
    return [part(label + " near", "box", (x, (y0 + mid - DOOR_W / 2) / 2, z + h / 2), (WALL, mid - DOOR_W / 2 - y0 - GAP, h), mat),
            part(label + " far", "box", (x, (mid + DOOR_W / 2 + y1) / 2, z + h / 2), (WALL, y1 - mid - DOOR_W / 2 - GAP, h), mat)]

def window(owner, x, y, z, normal, frame="Toy White", glass="Toy Window Glow", w=2.6, h=2.6, yaw=0.0):
    """A painted-on window: a frame with a glowing pane, on the outside face of a wall."""
    nx, ny = normal
    off = WALL / 2 + .06
    yaw = yaw if yaw else (0 if ny else 90)
    detail(owner, "window frame", "box", (x + nx * off, y + ny * off, z), (w + .5, .12, h + .5), frame, yaw=yaw)
    detail(owner, "window pane", "box", (x + nx * (off + .07), y + ny * (off + .07), z), (w, .06, h), glass, yaw=yaw)
    detail(owner, "window bar", "box", (x + nx * (off + .12), y + ny * (off + .12), z), (w, .05, .18), frame, yaw=yaw)

flights = {}   # where earlier stair flights stand, so a flight above one can stand on it

def stair_flight(label, x, y, z, along_x, mat, steps=5, run=1.6, width=3.0):
    """Solid stepped blocks, each 1.0 taller than the last. Upper floors leave a hole over the flight
    below, so a flight above another one is built as columns standing on the steps under it
    (nothing in a set may hang in the air: every part rests on something)."""
    below = flights.get((round(x, 2), round(y, 2)))
    flights[(round(x, 2), round(y, 2))] = z
    for k in range(steps):
        top = z + 1.0 * (k + 1) - GAP
        bottom = below + 1.0 * (k + 1) if below is not None else z
        h = top - bottom
        off = run * (k + .5)
        loc = (x + off, y, bottom + h / 2) if along_x else (x, y + off, bottom + h / 2)
        size = (run - GAP, width, h) if along_x else (width, run - GAP, h)
        part("%s step %d" % (label, k + 1), "box", loc, size, mat)

def grid_building(prefix, ox, oy, cols, rows, cell, floors, outer, inner, floor_mat, back_doors=(),
                  stair_cell=(0, 0), roof_access=False, z0=0.0, base=True, open_front=True,
                  furnish=None, windows_on=True, side_doors=()):
    """A play-set building on a grid of square rooms. The front (y0 side) is left open so you can
    see in. Doorways join every room; one stair room per floor goes up; floors cover every room
    except above the stairs. furnish(level, i, j, cx, cy, z) can add furniture to each room."""
    W, D = cols * cell, rows * cell
    x0, y0 = ox - W / 2, oy - D / 2
    z = z0
    if base:
        slab = part(prefix + " ground slab", "box", (ox, oy, z + SLAB / 2), (W + 1, D + 1, SLAB), floor_mat)
        z += SLAB + GAP
    for level in range(floors):
        wall_out = outer(level) if callable(outer) else outer
        for j in range(rows + 1):
            for i in range(cols):
                outside = j in (0, rows)
                if j == 0 and open_front:
                    continue
                door = (not outside) or (level == 0 and j == rows and i in back_doors)
                walls = wall_x("%s L%d wall x%d-%d" % (prefix, level, i, j), x0 + i * cell, x0 + (i + 1) * cell,
                               y0 + j * cell, z, STOREY, door, wall_out if outside else inner)
                if outside and windows_on and not door:
                    window(walls[0], x0 + (i + .5) * cell, y0 + j * cell, z + STOREY * .55, (0, 1 if j else -1))
        for i in range(cols + 1):
            for j in range(rows):
                outside = i in (0, cols)
                door = (not outside) or (level == 0 and (i, j) in side_doors)
                walls = wall_y("%s L%d wall y%d-%d" % (prefix, level, i, j), x0 + i * cell,
                               y0 + j * cell + WALL / 2 + GAP, y0 + (j + 1) * cell - WALL / 2 - GAP, z, STOREY, door,
                               wall_out if outside else inner)
                if outside and windows_on and not door:
                    window(walls[0], x0 + i * cell, y0 + (j + .5) * cell, z + STOREY * .55, (1 if i else -1, 0))
        going_up = level < floors - 1 or roof_access
        sc, sr = stair_cell
        if going_up:
            stair_flight("%s L%d stair" % (prefix, level), x0 + sc * cell + WALL + .3, y0 + sr * cell + cell / 2,
                         z, True, "Toy Wood")
        if furnish:
            for i in range(cols):
                for j in range(rows):
                    if going_up and (i, j) == (sc, sr):
                        continue
                    furnish(level, i, j, x0 + (i + .5) * cell, y0 + (j + .5) * cell, z)
        z += STOREY + GAP
        if going_up:
            # Floor round the stair hole: strips either side of the flight and past its top step,
            # so there is somewhere to stand at every level (needed in one-room towers).
            cx0, cy0 = x0 + sc * cell, y0 + sr * cell
            hole_x1 = cx0 + WALL + .3 + 5 * 1.6 + .6
            mid = cy0 + cell / 2
            for (ax, bx, ay, by) in ((cx0, cx0 + cell, cy0, mid - 2.1), (cx0, cx0 + cell, mid + 2.1, cy0 + cell),
                                     (hole_x1, cx0 + cell, mid - 2.1, mid + 2.1)):
                part("%s L%d landing" % (prefix, level), "box", ((ax + bx) / 2, (ay + by) / 2, z + SLAB / 2),
                     (bx - ax - GAP, by - ay - GAP, SLAB), floor_mat)
            # The strip past the top step has a wall under its far end only; a post holds the near end.
            part("%s L%d landing post" % (prefix, level), "box", (hole_x1 + .6, mid, z - (STOREY + GAP) / 2 - GAP / 2),
                 (.8, .8, STOREY - GAP), "Toy White")
        for i in range(cols):
            for j in range(rows):
                if going_up and (i, j) == (sc, sr):
                    continue
                tile = part("%s L%d floor %d-%d" % (prefix, level, i, j), "box",
                            (x0 + (i + .5) * cell, y0 + (j + .5) * cell, z + SLAB / 2), (cell - GAP, cell - GAP, SLAB), floor_mat)
                # Trim band along the open edge so the floors read as storeys of a toy set.
                if j == 0 and open_front:
                    detail(tile, "floor edge trim", "box", (x0 + (i + .5) * cell, y0 - .1, z + SLAB / 2), (cell, .3, SLAB + .2), "Toy White")
        z += SLAB + GAP
    return z, (x0, y0, W, D)

def rug(owner, cx, cy, z, w, d, mat):
    detail(owner, "rug", "box", (cx, cy, z + .05), (w, d, .08), mat)

sets = {}

# ======================= Doll mansion =======================
CELL = 15
def mansion_room(level, i, j, cx, cy, z):
    kind = [["living", "kitchen", "dining"], ["hall", "music", "library"]][j][i] if level == 0 else \
           [["bedroom", "bathroom", "nursery"], ["bedroom2", "dressing", "playroom"]][j][i] if level == 1 else \
           [["attic", "studio", "attic2"], ["sunroom", "closet", "games"]][j][i]
    back = cy + CELL / 2 - 3.2     # against the back wall of the room
    if kind in ("living", "sunroom"):
        sofa = part("mansion sofa", "box", (cx, back, z + .9), (7, 2.6, 1.8), "Toy Pink")
        detail(sofa, "sofa back", "box", (cx, back + 1.1, z + 2.2), (7, .6, 1.4), "Toy Pink")
        for s in (-1, 1):
            detail(sofa, "sofa arm", "box", (cx + s * 3.3, back, z + 1.9), (.7, 2.6, .9), "Toy Pink")
            detail(sofa, "cushion", "box", (cx + s * 1.6, back - .1, z + 2.0), (2.6, 1.6, .4), "Toy White")
        rug(sofa, cx, cy - 1, z, 8, 6, "Toy Purple")
        detail(sofa, "coffee table", "box", (cx, cy - 1, z + .6), (3.5, 2, .4), "Toy White")
        detail(sofa, "tv", "box", (cx, cy - CELL / 2 + 2.5, z + 2.4), (4, .3, 2.4), "Toy Screen Glow")
    elif kind == "kitchen":
        counter = part("mansion counter", "box", (cx, back + .4, z + 1.2), (9, 2.2, 2.4), "Toy White")
        detail(counter, "counter top", "box", (cx, back + .4, z + 2.45), (9.2, 2.4, .2), "Toy Pink")
        detail(counter, "stove", "box", (cx - 2.5, back + .4, z + 2.6), (2, 1.6, .1), "Toy Black")
        detail(counter, "fridge", "box", (cx + 5.6, back + .2, z + 2.0), (2.2, 2.4, 4.0), "Toy White")
        detail(counter, "fridge handle", "box", (cx + 5.0, back - 1.05, z + 2.4), (.2, .1, 1.4), "Toy Silver")
    elif kind == "dining":
        table = part("mansion dining table", "box", (cx, cy, z + 1.0), (6, 3.5, 2.0), "Toy Wood")
        for s in (-1, 1):
            for k in (-1.8, 0, 1.8):
                detail(table, "chair", "box", (cx + k, cy + s * 2.5, z + .8), (1.2, 1.2, 1.6), "Toy Pink")
        detail(table, "cake", "cyl", (cx, cy, z + 2.4), (1.4, 1.4, .8), "Toy White")
    elif kind in ("bedroom", "bedroom2"):
        bed = part("mansion bed", "box", (cx, back - .6, z + .8), (5, 6, 1.6), "Toy White")
        detail(bed, "headboard", "box", (cx, back + 2.5, z + 2.2), (5.4, .5, 2.8), "Toy Pink")
        detail(bed, "blanket", "box", (cx, back - 1.4, z + 1.65), (5.1, 4.2, .2), "Toy Purple" if level == 1 else "Toy Pink")
        detail(bed, "pillow", "box", (cx, back + 1.4, z + 1.8), (3.6, 1.2, .5), "Toy White")
        detail(bed, "wardrobe", "box", (cx + 5.4, back + .8, z + 2.6), (2.4, 2, 5.0 - .2), "Toy White")
    elif kind == "bathroom":
        tub = part("mansion bathtub", "box", (cx, back, z + .9), (6, 3, 1.8), "Toy White")
        detail(tub, "water", "box", (cx, back, z + 1.82), (5.4, 2.4, .05), "Toy Glass")
        detail(tub, "mirror", "box", (cx + 4, back + 2.6, z + 3), (2.2, .2, 2.4), "Toy Silver")
    elif kind in ("nursery", "playroom", "games"):
        toybox = part("mansion toy box", "box", (cx, back, z + 1.0), (4, 2.6, 2.0), "Toy Yellow")
        detail(toybox, "lid", "box", (cx, back, z + 2.1), (4.2, 2.8, .2), "Toy Red")
        for k, c in enumerate(["Toy Red", "Toy Blue", "Toy Green", "Toy Yellow"]):
            detail(toybox, "block", "box", (cx - 3 + k * 1.6, cy - 1, z + .6), (1.2, 1.2, 1.2), c, yaw=k * 17)
        rug(toybox, cx, cy - 1, z, 7, 6, "Toy Teal")
    elif kind in ("music",):
        piano = part("mansion piano", "box", (cx, back, z + 1.6), (6, 2.6, 3.2), "Toy Black")
        detail(piano, "keys", "box", (cx, back - 1.5, z + 2.1), (5.4, .6, .2), "Toy White")
        detail(piano, "stool", "cyl", (cx, back - 3.2, z + .7), (1.4, 1.4, 1.4), "Toy Pink")
    elif kind in ("library", "studio", "dressing", "closet"):
        shelf = part("mansion shelf", "box", (cx, back + 1, z + 2.2), (8, 1.4, 4.4), "Toy Wood")
        for r in range(3):
            for k in range(8):
                c = ["Toy Red", "Toy Blue", "Toy Green", "Toy Yellow", "Toy Purple"][(k + r) % 5]
                detail(shelf, "book" if kind == "library" else "dress", "box",
                       (cx - 3.4 + k * .95, back + .25, z + 1.0 + r * 1.4), (.6, .5, 1.0), c)
    elif kind in ("attic", "attic2"):
        chest = part("mansion chest", "box", (cx, back, z + .9), (4, 2.4, 1.8), "Toy Wood")
        detail(chest, "chest band", "box", (cx, back - 1.25, z + .9), (4.1, .1, .3), "Toy Gold")
        rug(chest, cx, cy, z, 6, 5, "Toy Cream")
    elif kind == "hall":
        pass

top, (x0, y0, W, D) = grid_building("mansion", 0, 0, 3, 2, CELL, 3, "Toy Pink", "Toy Cream", "Toy White",
    back_doors=(1,), side_doors=((0, 1),), stair_cell=(0, 1), furnish=mansion_room)
ridge = top + (D / 2) * math.tan(math.radians(28))
# The roof is one solid gable-shaped part sitting flat on the top floor, so it cannot slide off;
# the pink slopes, tiles and ridge are decoration on it.
mesh = bpy.data.meshes.new("mansion roof")
bm = bmesh.new()
hx, hy, rh = (W + 2) / 2, D / 2 + .5, (D / 2 + .5) * math.tan(math.radians(28))
ends = []
for x in (-hx, hx):
    ends.append([bm.verts.new((x, -hy, 0)), bm.verts.new((x, hy, 0)), bm.verts.new((x, 0, rh))])
bm.faces.new(ends[0][::-1]); bm.faces.new(ends[1])
for i in range(3):
    j = (i + 1) % 3
    bm.faces.new((ends[0][i], ends[0][j], ends[1][j], ends[1][i]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(mesh); bm.free()
roof_block = bpy.data.objects.new("Piece %03d mansion roof" % (len(pieces) + 1), mesh)
bpy.context.collection.objects.link(roof_block)
roof_block.location = (0, 0, top)
roof_block.data.materials.append(paint("Toy Pink"))
pieces.append(roof_block)
ridge = top + rh
for s in (-1, 1):
    for k in range(4):   # roof tile stripes
        t = (k + .5) / 4
        detail(roof_block, "roof stripe", "box", (0, s * hy * t, top + hy * (1 - t) * math.tan(math.radians(28)) + .08),
               (W + 2.1, .4, .1), "Toy White", tilt=(s * -28, 0))
detail(roof_block, "roof ridge", "box", (0, 0, ridge), (W + 2.4, 1.0, .6), "Toy White")
for x in (-W / 3, W / 3):
    ch = part("mansion chimney", "box", (x, D / 4, top + 4.2), (2.4, 2.4, 8.4), "Toy White")
    detail(ch, "chimney cap", "box", (x, D / 4, top + 8.5), (2.9, 2.9, .4), "Toy Pink")
# Front porch: columns under the open front of the first floor.
for x in (-W / 2 + 1, W / 2 - 1):
    col = part("mansion porch column", "cyl", (x, y0 + 1, SLAB + GAP + STOREY / 2), (1.2, 1.2, STOREY), "Toy White")
    detail(col, "column cap", "box", (x, y0 + 1, SLAB + STOREY - .1), (1.6, 1.6, .3), "Toy Gold")
sets["Toy Doll Mansion"] = export("Toy Doll Mansion")

# ======================= Superhero base =======================
def hero_room(level, i, j, cx, cy, z):
    back = cy + CELL / 2 - 3.2
    kind = [["garage", "garage2", "workshop"], ["gym", "stairs", "cells"]][j][i] if level == 0 else \
           [["command", "briefing", "armory"], ["bunks", "stairs", "lab"]][j][i]
    if kind in ("garage", "garage2"):
        car = part("hero car", "box", (cx, cy + 1, z + 1.2), (5, 9, 2.4), "Toy Black" if kind == "garage" else "Toy Red")
        detail(car, "car cabin", "box", (cx, cy + 2, z + 2.9), (4, 3.6, 1.2), "Toy Screen Glow")
        detail(car, "car fin", "box", (cx, cy + 5, z + 3.2), (.4, 2, 1.6), "Toy Yellow")
        for sx in (-1, 1):
            for sy in (-1, 1):
                detail(car, "wheel", "cyl", (cx + sx * 2.6, cy + 1 + sy * 3, z + .9), (1.8, 1.8, .8), "Toy Gray", tilt=(0, 90))
        detail(car, "garage stripe", "box", (cx, cy - 5.5, z + .03), (6, .5, .06), "Toy Yellow")
    elif kind == "workshop":
        bench = part("hero workbench", "box", (cx, back, z + 1.3), (8, 2.4, 2.6), "Toy Gray")
        for k in range(4):
            detail(bench, "tool", "box", (cx - 3 + k * 2, back + .3, z + 2.9), (.3, .3, 1.2), "Toy Silver")
        detail(bench, "spare mask", "ball", (cx + 3, back - .2, z + 3.2), (1.2, 1.0, 1.2), "Toy Blue")
    elif kind == "gym":
        weights = part("hero weights rack", "box", (cx, back, z + 1.0), (6, 2, 2.0), "Toy Gray")
        for s in (-1, 1):
            detail(weights, "weight plate", "cyl", (cx + s * 2.6, back - 1.2, z + 2.4), (1.6, 1.6, .4), "Toy Black", tilt=(90, 0))
        detail(weights, "bar", "cyl", (cx, back - 1.2, z + 2.4), (.3, .3, 5.6), "Toy Silver", tilt=(0, 90))
        rug(weights, cx, cy - 1, z, 7, 6, "Toy Blue")
    elif kind == "cells":
        cell = part("hero cell block", "box", (cx, back, z + .4), (7, 3, .8), "Toy Gray")
        for k in range(8):
            detail(cell, "bar", "cyl", (cx - 3.2 + k * .9, back - 1.4, z + 2.6), (.2, .2, 3.6), "Toy Silver")
    elif kind == "command":
        desk = part("hero console", "box", (cx, back, z + 1.1), (9, 2.6, 2.2), "Toy Navy")
        for k in range(3):
            detail(desk, "screen", "box", (cx - 3 + k * 3, back + 1.6, z + 3.4), (2.6, .2, 1.8), "Toy Screen Glow")
        detail(desk, "buttons", "box", (cx, back - 1, z + 2.25), (7, .4, .1), "Toy Red")
        detail(desk, "chair", "cyl", (cx, back - 3, z + .8), (1.6, 1.6, 1.6), "Toy Red")
    elif kind == "briefing":
        table = part("hero round table", "cyl", (cx, cy, z + 1.0), (6, 6, 2.0), "Toy Navy", seg=24)
        detail(table, "hologram", "cyl", (cx, cy, z + 2.6), (2.4, 2.4, 1.2), "Toy Screen Glow")
        detail(table, "emblem", "cyl", (cx, cy, z + 2.02), (4, 4, .05), "Toy Yellow", seg=6)
    elif kind == "armory":
        rack = part("hero armory rack", "box", (cx, back + .8, z + 2.0), (8, 1.4, 4.0), "Toy Gray")
        for k in range(3):
            detail(rack, "shield", "cyl", (cx - 2.6 + k * 2.6, back, z + 2.4), (2.0, 2.0, .2), ["Toy Red", "Toy Blue", "Toy Yellow"][k], tilt=(90, 0))
    elif kind == "bunks":
        bunk = part("hero bunk", "box", (cx, back, z + 1.0), (6, 3, 2.0), "Toy Gray")
        detail(bunk, "top bunk", "box", (cx, back, z + 3.2), (6, 3, .4), "Toy Gray")
        detail(bunk, "blanket", "box", (cx, back, z + 2.05), (5.6, 2.6, .15), "Toy Red")
    elif kind == "lab":
        lab = part("hero lab bench", "box", (cx, back, z + 1.2), (7, 2.4, 2.4), "Toy White")
        for k in range(4):
            detail(lab, "flask", "cyl", (cx - 2.4 + k * 1.6, back, z + 2.9), (.7, .7, 1.0), ["Toy Screen Glow", "Toy Magic Glow", "Toy Green", "Toy Orange"][k])

top, (x0, y0, W, D) = grid_building("hero", 0, 0, 3, 2, CELL, 2, lambda level: "Toy Blue" if level == 0 else "Toy Navy",
    "Toy Gray", "Toy Gray", back_doors=(0, 2), stair_cell=(1, 1), roof_access=True, furnish=hero_room)
# Helipad on the roof tile in front of the tower.
ttop, _ = grid_building("hero tower", x0 + 7.5 + 15, y0 + 7.5 + 15, 1, 1, 15, 3, "Toy Red", "Toy Red", "Toy Gray",
    back_doors=(), side_doors=((0, 0),), stair_cell=(0, 0), z0=top, base=False, open_front=True)
pad = part("hero helipad", "cyl", (x0 + 7.5, y0 + 7.5, top + .15), (11, 11, .3), "Toy Gray", seg=32)
detail(pad, "helipad ring", "cyl", (x0 + 7.5, y0 + 7.5, top + .32), (10, 10, .05), "Toy Yellow", seg=32)
detail(pad, "helipad centre", "cyl", (x0 + 7.5, y0 + 7.5, top + .35), (8, 8, .05), "Toy Gray", seg=32)
for dx in (-1.2, 1.2):
    detail(pad, "H bar", "box", (x0 + 7.5 + dx, y0 + 7.5, top + .38), (.6, 4, .05), "Toy Yellow")
detail(pad, "H cross", "box", (x0 + 7.5, y0 + 7.5, top + .38), (2.4, .6, .05), "Toy Yellow")
mast = part("hero radar mast", "box", (x0 + 22.5, y0 + 22.5, ttop + 1.5), (1.6, 1.6, 3.0), "Toy Silver")
dish = detail(mast, "radar dish", "cyl", (x0 + 22.5, y0 + 22.5, ttop + 3.0 + GAP + .4), (7, 7, .6), "Toy Silver", tilt=(25, 0))
detail(dish, "dish light", "ball", (x0 + 22.5, y0 + 22.5, ttop + 4.4), (.8, .8, .8), "Toy Red")
sign = detail(bpy.data.objects[next(n for n in bpy.data.objects.keys() if n.endswith("hero tower L1 wall x0-1"))],
    "emblem sign", "cyl", (x0 + 22.5, y0 + 22.5 + 7.8, top + STOREY * 2.2), (7, 7, .4), "Toy Yellow", tilt=(90, 0), seg=6)
detail(sign, "emblem star", "cyl", (x0 + 22.5, y0 + 22.5 + 8.05, top + STOREY * 2.2), (3.6, 3.6, .1), "Toy Red", tilt=(90, 0), seg=5)
sets["Toy Superhero Base"] = export("Toy Superhero Base")

# ======================= Wizard tower =======================
R, PANELS, WEDGES = 12.0, 12, 8
OPEN = range(7, 12)          # panels left out on the open side (they face the viewer once placed)
part("tower ground slab", "cyl", (0, 0, SLAB / 2), (2 * R + 2, 2 * R + 2, SLAB), "Toy Gray", seg=48)
z = SLAB + GAP
floor_top = {}       # top of the last floor slice built at each slice position
pillar_below = {}    # slice positions with a pillar under their edge
width = 2 * R * math.tan(math.pi / PANELS) - GAP * 3
for level in range(5):
    mat = "Toy Gray" if level < 2 else "Toy Purple"
    for k in range(PANELS):
        if k in OPEN:
            continue
        a = k * 360 / PANELS
        r = math.radians(a)
        wall = part("tower L%d wall %d" % (level, k), "box", (math.cos(r) * R, math.sin(r) * R, z + STOREY / 2),
                    (WALL, width, STOREY), mat, yaw=a)
        # Arched window outside, bookshelf inside.
        if k % 2 == 0:
            window(wall, math.cos(r) * R, math.sin(r) * R, z + STOREY * .55, (math.cos(r), math.sin(r)),
                   frame="Toy Gold", glass="Toy Magic Glow", w=1.6, h=2.6, yaw=a + 90)
        else:
            ir = R - WALL / 2 - .5
            shelf_c = (math.cos(r) * ir, math.sin(r) * ir)
            detail(wall, "bookshelf", "box", (shelf_c[0], shelf_c[1], z + 1.8), (.9, width - .6, 3.6), "Toy Wood", yaw=a)
            for row in range(3):
                detail(wall, "books", "box", (shelf_c[0] - math.cos(r) * .2, shelf_c[1] - math.sin(r) * .2, z + .8 + row * 1.1),
                       (.6, width - 1.0, .8), ["Toy Red", "Toy Blue", "Toy Green"][row], yaw=a)
        detail(wall, "stone band", "box", (math.cos(r) * (R + .35), math.sin(r) * (R + .35), z + STOREY - .2),
               (.15, width, .35), "Toy Gold", yaw=a)
    stair = level * 90.0 + 45.0
    if level < 4:
        sr = math.radians(stair)
        for k in range(5):
            h = 1.0 * (k + 1) - GAP
            d = 3.0 + 1.55 * (k + .5)      # ends short of the rim round the stair hole
            part("tower L%d step %d" % (level, k + 1), "box", (math.cos(sr) * d, math.sin(sr) * d, z + h / 2),
                 (1.55 - GAP, 3.4, h), "Toy Wood", yaw=stair)
    # Room furniture opposite the stairs.
    fa = math.radians(stair + 180 + 12)    # off the seam between two floor slices
    fx, fy = math.cos(fa) * 6, math.sin(fa) * 6
    if level == 0:
        pot = part("tower cauldron", "cyl", (fx, fy, z + 1.2), (3.6, 3.6, 2.4), "Toy Black", seg=24)
        detail(pot, "brew", "cyl", (fx, fy, z + 2.42), (3.0, 3.0, .1), "Toy Magic Glow", seg=24)
        for k in range(3):
            detail(pot, "bubble", "ball", (fx + k * .5 - .5, fy, z + 2.8 + k * .3), (.5, .5, .5), "Toy Magic Glow")
    elif level == 1:
        desk = part("tower potion table", "box", (fx, fy, z + 1.2), (5, 2.6, 2.4), "Toy Wood", yaw=stair)
        for k in range(4):
            detail(desk, "potion", "cyl", (fx + math.cos(fa + 1.57) * (k - 1.5) * 1.1, fy + math.sin(fa + 1.57) * (k - 1.5) * 1.1, z + 2.9),
                   (.6, .6, 1.0), ["Toy Magic Glow", "Toy Screen Glow", "Toy Green", "Toy Red"][k])
    elif level == 2:
        bed = part("tower bed", "box", (fx, fy, z + .8), (5, 3.4, 1.6), "Toy Purple", yaw=stair)
        detail(bed, "star blanket", "box", (fx, fy, z + 1.65), (4.6, 3.0, .15), "Toy Navy", yaw=stair)
        detail(bed, "moon pillow", "cyl", (fx, fy, z + 1.9), (1.4, 1.4, .5), "Toy Yellow")
    elif level == 3:
        orb = part("tower orb stand", "cyl", (fx, fy, z + 1.2), (1.6, 1.6, 2.4), "Toy Gold", seg=16)
        detail(orb, "crystal ball", "ball", (fx, fy, z + 3.2), (2.2, 2.2, 2.2), "Toy Magic Glow")
    else:
        scope = part("tower telescope stand", "cyl", (fx, fy, z + 1.4), (2.0, 2.0, 2.8), "Toy Wood", seg=16)
        detail(scope, "telescope", "cyl", (fx, fy, z + 3.4), (1.0, 1.0, 4.4), "Toy Gold", tilt=(0, 60))
    # Floors are pie slices: a column in the middle and the walls round the edge hold each one up.
    floor_z = z + STOREY + GAP
    part("tower L%d centre column" % level, "cyl", (0, 0, z + STOREY / 2), (2.8, 2.8, STOREY), "Toy Gold", seg=24)
    # Slices on the open side have no wall under their edge: a pillar goes down to the nearest floor.
    for k in range(WEDGES):
        a0, a1 = k * math.tau / WEDGES, (k + 1) * math.tau / WEDGES
        mid = (a0 + a1) / 2
        # A wall must sit under the middle of the slice's edge, not just at one corner of it.
        walls_under = [p for p in range(PANELS) if p not in OPEN and
                       abs((math.degrees(mid) - p * 360 / PANELS + 180) % 360 - 180) < 16]
        hole = level < 4 and abs((math.degrees(mid) - stair + 180) % 360 - 180) < 30
        pillar_below[k] = not walls_under
        if not walls_under:
            bottom = floor_top.get(k, SLAB + GAP)
            h = floor_z - GAP - bottom
            part("tower L%d pillar %d" % (level, k), "cyl", (math.cos(mid) * (R - 1.2), math.sin(mid) * (R - 1.2), bottom + h / 2),
                 (1.2, 1.2, h), "Toy Gold", seg=16)
    z += STOREY + GAP
    for k in range(WEDGES):
        a0, a1 = k * math.tau / WEDGES, (k + 1) * math.tau / WEDGES
        mid_r = (a0 + a1) / 2
        mid = math.degrees(mid_r)
        if level < 4 and abs((mid - stair + 180) % 360 - 180) < 30:
            # A hole above the stairs, but keep a rim round the edge for the walls above to stand on
            # (in short pieces so each stays a solid, nearly straight shape).
            for part_k in range(3):
                b0 = a0 + (a1 - a0) * part_k / 3
                b1 = a0 + (a1 - a0) * (part_k + 1) / 3
                piece_mid = math.degrees((b0 + b1) / 2)
                # Only where it sits mostly over a wall (the open side has none above, so needs no rim).
                if not any(abs((piece_mid - p * 360 / PANELS + 180) % 360 - 180) < 10 for p in range(PANELS) if p not in OPEN):
                    continue
                # Centred over the wall line so its weight sits on the wall, not past it.
                ring_piece("tower L%d rim %d-%d" % (level, k, part_k), z, SLAB, R - 1.0, R + 1.3, b0 + .004, b1 - .004,
                           "Toy Gray" if level < 4 else "Toy Purple")
            # On the open side a pillar stands under this slice's edge: give the pillar above a pad
            # to stand on, so every pillar is one storey tall and keeps pace if floors change.
            if pillar_below.get(k):
                pr = R - 1.2
                pad = part("tower L%d pillar pad %d" % (level, k), "box",
                           (math.cos(mid_r) * pr, math.sin(mid_r) * pr, z + SLAB / 2), (2.0, 2.0, SLAB),
                           "Toy Gray" if level < 4 else "Toy Purple", yaw=math.degrees(mid_r))
                floor_top[k] = z + SLAB + GAP
            continue
        # Reaches past the walls' outer corners so every wall stands fully on a floor (a ledge outside).
        wedge("tower L%d floor %d" % (level, k), z, SLAB, R + 1.3, a0 + .004, a1 - .004,
              "Toy Gray" if level < 4 else "Toy Purple")
        floor_top[k] = z + SLAB + GAP
    z += SLAB + GAP
for k in range(12):
    if k in OPEN:
        continue
    a = math.radians(k * 30 + 15)
    part("tower battlement %d" % k, "box", (math.cos(a) * (R - .2), math.sin(a) * (R - .2), z + 1.0),
         (1.6, 2.4, 2.0), "Toy Gray", yaw=k * 30 + 15)
roofp = part("tower roof", "cone", (0, 0, z + 9), (2 * R - 4, 2 * R - 4, 18), "Toy Blue", seg=48)
for k in range(10):
    a = k * .9
    hgt = 2 + k * 1.4
    rr = (R - 2) * (1 - hgt / 18) + .2
    detail(roofp, "roof star", "ball", (math.cos(a) * rr, math.sin(a) * rr, z + hgt), (.7, .7, .7), "Toy Yellow")
moon = detail(roofp, "tower star", "ball", (0, 0, z + 18 + GAP + 1.4), (2.8, 2.8, 2.8), "Toy Yellow")
detail(moon, "flag pole", "cyl", (0, 0, z + 21.6), (.2, .2, 2.0), "Toy Gold")
detail(moon, "flag", "box", (.9, 0, z + 22.2), (1.6, .1, 1.0), "Toy Purple")
sets["Toy Wizard Tower"] = export("Toy Wizard Tower")

print("SETS", {k: len(v) for k, v in sets.items()})

if preview:
    off = {"Toy Wizard Tower": -62, "Toy Superhero Base": 0, "Toy Doll Mansion": 62}
    for name, ps in sets.items():
        for p in ps:
            if not p.parent:
                p.location.x += off[name]
    bpy.ops.object.light_add(type="SUN", rotation=(math.radians(55), 0, math.radians(-20)))
    bpy.context.active_object.data.energy = 3
    bpy.ops.object.camera_add(location=(0, -140, 70), rotation=(math.radians(66), 0, 0))
    bpy.context.scene.camera = bpy.context.active_object
    w = bpy.data.worlds.new("w"); w.color = (.15, .15, .2); bpy.context.scene.world = w
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            bpy.context.scene.render.engine = engine
            break
        except TypeError:
            pass
    bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = 1800, 900
    bpy.context.scene.render.filepath = preview
    bpy.ops.render.render(write_still=True)
