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
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from premiere_export import export_mansion
from falcon_export import export_falcon
sets["Toy Doll Mansion"] = export_mansion(out)
sets["Toy Superhero Base"] = export_falcon(out)

from crag_export import export_crag
sets["Toy Wizard Tower"] = export_crag(out)

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
