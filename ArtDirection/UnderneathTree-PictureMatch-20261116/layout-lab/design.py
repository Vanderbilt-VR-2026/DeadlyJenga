"""Builds the approved-picture layout and writes layout.json for the Blender lab.

Course frame: x right, z back, origin = centre of the train loop on the rug.
"""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
from layout import *

L = Layout()
course = track()
N = SAMPLES
RUG_Z = 17.0


def tangent_turn(i):
    a, b = course[(i - 1) % N], course[(i + 1) % N]
    return math.degrees(math.atan2(b[0] - a[0], b[1] - a[1]))


def jit(amount=3.0):
    return L.rand.uniform(-amount, amount)


def stack(prefix, x, z, tiers, turn=0.0, bow_top=True, on=None):
    """tiers = [(label, w, h, d, color, dx, dz)] bottom to top; each rests on the one below."""
    below = on
    for k, (label, w, h, d, color, dx, dz) in enumerate(tiers):
        last = k == len(tiers) - 1
        below = L.add(prefix + " / " + label, x + dx, z + dz, w, h, d, color,
                      turn=turn + jit(), on=below, bow=last and bow_top)
    return below


# --- 1. Roadbed: one bearing gift under each of the 16 rail sections --------------------
bed_colors = ["cream", "green", "red", "cream", "navy", "cream", "red", "green"]
BED_H = TRACK_HEIGHT - .35
for s in range(N // PER_SECTION):
    first = s * PER_SECTION
    mid = first + PER_SECTION // 2
    a, b = course[first], course[(first + PER_SECTION) % N]
    bend = max(abs((tangent_turn(mid) - tangent_turn((mid + d) % N) + 180) % 360 - 180)
               for d in (-PER_SECTION, PER_SECTION))
    length = math.hypot(b[0] - a[0], b[1] - a[1]) - 7.6 * math.tan(math.radians(bend) / 2) - .3
    L.add("Rail Gift %d" % (s + 1), course[mid][0], course[mid][1], 7.6, BED_H, length,
          bed_colors[s % len(bed_colors)], turn=tangent_turn(mid), bow=False, role="rail")

# --- 2. Navy bridge across the courtyard ------------------------------------------------
BZ, DECK = -3.0, 8.6
L.add("Gift Bridge / Pine Bearing", -21.4, BZ, 6.5, DECK, 8.0, "green", "gold", bow=False)
L.add("Gift Bridge / Cranberry Bearing", 21.5, BZ, 6.5, DECK, 8.0, "red", "ivory", bow=False)
L.add("Gift Bridge / Navy Span", 1.4, BZ, 52.0, 3.2, 9.6, "navy", "ivory", y=DECK, on="Gift Bridge / Pine Bearing")
L.by_name["Gift Bridge / Navy Span"]["on2"] = "Gift Bridge / Cranberry Bearing"

# --- 3. Left tower: straddles the rail; the train runs through its tunnel ----------------
PIER = 13.4
for k, z in enumerate((-9.0, -0.5, 8.0)):
    rail_x = min(u for u, v in course if abs(v - z) < 4.5)      # leftmost rail centre beside this pier
    rail_in = max(u for u, v in course if abs(v - z) < 4.5 and u < 0)
    outer_x = rail_x - 3.8 - 1.0 - 3.2
    stack("Left Tower / Outer Pier %d" % (k + 1), outer_x, z, [
        ("Base", 6.4, 7.2, 8.0, ["cream", "green", "red", "navy"][k], 0, 0),
        ("Cap", 6.0, PIER - 7.2, 7.6, ["red", "cream", "green", "cream"][k], 0, 0)], bow_top=False)
    inner_x = rail_in + 3.8 + 2.2 + 2.7
    stack("Left Tower / Inner Pier %d" % (k + 1), inner_x, z, [
        ("Base", 5.4, 6.4, 8.0, ["green", "cream", "navy", "red"][k], 0, 0),
        ("Cap", 5.2, PIER - 6.4, 7.6, ["cream", "red", "cream", "green"][k], 0, 0)], bow_top=False)
    name = L.add("Left Tower / Lintel %d" % (k + 1), (outer_x + inner_x) / 2, z,
                 inner_x - outer_x + 5.6, 3.0, 8.2, ["cream", "navy", "red", "green"][k],
                 y=PIER, on="Left Tower / Outer Pier %d / Cap" % (k + 1), bow=False)
    L.by_name[name]["on2"] = "Left Tower / Inner Pier %d / Cap" % (k + 1)
ROOF = PIER + 3.0
stack("Left Summit", -42.5, -2.5, [
    ("Broad Cream", 13.0, 4.4, 8.0, "cream", 0, 0),
    ("Ruby Shoulder", 9.6, 3.6, 9.0, "red", .6, .8),
    ], on="Left Tower / Lintel 2")
L.add("Left Summit / Ivory Crown", -41.8, -1.6, 7.6, 6.2, 7.6, "cream", "gold",
      on="Left Summit / Ruby Shoulder", shape="round")
L.add("Left Summit / Emerald Front", -44.6, -10.3, 8.4, 4.2, 5.0, "green", "gold", turn=4, on="Left Tower / Lintel 1")
L.add("Left Summit / Ruby Front", -35.4, -10.2, 5.0, 2.6, 5.0, "red", "gold", turn=-6, on="Left Tower / Lintel 1")
L.add("Left Summit / Sapphire Rear", -43.0, 8.3, 10.0, 3.8, 6.0, "navy", "ivory", turn=-3, on="Left Tower / Lintel 3")
L.add("Left Summit / Cream Rear", -43.6, 8.3, 6.0, 3.0, 5.0, "cream", "berry", turn=5, on="Left Summit / Sapphire Rear")
L.add("Left Summit / Green Ledge", -33.2, 0.4, 4.2, 3.0, 5.6, "green", "ivory", turn=3, on="Left Tower / Lintel 2")
# steps from the courtyard and the bridge up the inside of the left tower
L.add("Left Stair / Low Navy", -19.0, -14.5, 7.0, 3.2, 6.0, "navy", "ivory", turn=8)
L.add("Left Stair / Mid Cream", -19.6, 7.6, 7.0, 6.0, 7.0, "cream", "berry", turn=-5)
L.add("Left Stair / High Ruby", -19.2, 15.6, 6.5, 10.0, 6.0, "red", "gold", turn=4)
L.add("Bridge Terrace / Left Ruby", -20.0, BZ, 7.0, 3.2, 7.6, "red", "gold", turn=-2, on="Gift Bridge / Navy Span")
# outside left: steps up from the rug
L.add("Left Foot / Navy Toe", -33.0, -31.0, 6.4, 3.0, 5.4, "navy", "gold", turn=30)
L.add("Left Foot / Ruby Step", -39.5, -27.5, 6.0, 6.4, 5.6, "red", "gold", turn=24)
L.add("Left Foot / Cream Step", -45.2, -23.2, 5.6, 9.6, 4.6, "cream", "berry", turn=16)
L.add("Left Foot / Green Side", -56.4, -5.0, 4.0, 5.0, 7.0, "green", "ivory", turn=2)
L.add("Left Foot / Cream Side", -57.0, 4.6, 4.6, 8.4, 7.0, "cream", "gold", turn=-3)
L.add("Left Foot / Ruby Rear", -53.0, 19.4, 7.0, 4.6, 6.0, "red", "ivory", turn=-22)

# --- 4. Right tower: inside the right end of the loop, with outer terraces ----------------
stack("Right Summit", 25.6, 9.8, [
    ("Sapphire Base", 12.0, 8.0, 12.0, "navy", 0, 0),
    ("Cream Shelf", 10.6, 5.4, 10.0, "cream", .8, .8),
    ("Ruby Perch", 8.4, 4.0, 8.0, "red", 1.0, 1.2),
    ("Sapphire Crown", 6.4, 4.6, 6.2, "navy", 1.2, 1.4)], turn=4)
L.add("Right Stair / Low Green", 21.4, -13.4, 7.0, 3.0, 6.0, "green", "ivory", turn=-8)
L.add("Right Stair / Cream One", 29.6, -5.0, 6.4, 5.6, 6.4, "cream", "gold", turn=3)
L.add("Right Stair / Cream Two", 15.8, 9.0, 4.4, 11.6, 7.0, "cream", "berry", turn=-3)
L.add("Bridge Terrace / Right Cream", 22.6, BZ, 8.0, 3.0, 7.6, "cream", "gold", turn=2, on="Gift Bridge / Navy Span")
stack("Right Terrace", 53.0, 2.0, [
    ("Ruby Foundation", 8.0, 7.6, 10.0, "red", 0, 0),
    ("Emerald Shelf", 7.0, 4.6, 8.0, "green", 0, .6),
    ("Ivory Perch", 5.6, 3.6, 6.0, "cream", 0, 1.0)], turn=-2)
L.add("Right Foot / Navy Toe", 41.5, -23.5, 6.0, 2.8, 5.0, "navy", "gold", turn=-28)
L.add("Right Foot / Cream Step", 51.4, -11.6, 6.6, 5.2, 7.0, "cream", "berry", turn=-10)
L.add("Right Foot / Green Step", 54.0, 13.6, 6.0, 9.6, 7.0, "green", "ivory", turn=8)
L.add("Right Foot / Ruby Rear", 49.6, 22.6, 7.0, 5.0, 6.0, "red", "gold", turn=24)
L.add("Right Foot / Cream Rear", 43.0, 27.4, 6.4, 3.0, 5.6, "cream", "gold", turn=34)

# --- 5. Courtyard hops and the back row ---------------------------------------------------
L.add("Courtyard / Ruby Hop", -12.0, -14.6, 6.4, 3.4, 5.4, "red", "gold", turn=10)
L.add("Courtyard / Green Hop", 6.0, -16.0, 7.6, 2.4, 5.0, "green", "gold", turn=-7)
L.add("Courtyard / Cream Hop", 13.6, -9.6, 5.0, 4.6, 4.6, "cream", "berry", turn=14)
L.add("Courtyard / Navy Hop", -8.0, 9.0, 7.0, 3.0, 6.0, "navy", "ivory", turn=-9)
L.add("Courtyard / Ruby Rear", 6.6, 12.0, 6.0, 5.0, 5.6, "red", "ivory", turn=6)
L.add("Courtyard / Green Rear", -1.0, 17.6, 6.6, 2.6, 5.0, "green", "gold", turn=-4)
back = [(-21.0, 39.0, 7.0, 5.6, 6.0, "red", 6), (-13.0, 40.6, 7.4, 4.0, 6.0, "green", -4),
        (-5.0, 41.0, 6.4, 6.6, 6.0, "red", 3), (2.6, 41.0, 7.0, 4.4, 6.0, "cream", -5),
        (10.6, 39.6, 6.6, 5.4, 6.0, "navy", 4), (-31.5, 33.4, 6.0, 3.6, 5.6, "cream", 30)]
for k, (x, z, w, h, d, color, turn) in enumerate(back):
    L.add("Back Row / Gift %d" % (k + 1), x, z, w, h, d, color, turn=turn)
L.add("Back Row / Top Green", -12.6, 40.6, 5.0, 3.4, 4.6, "green", "gold", turn=8, on="Back Row / Gift 2")
L.add("Back Row / Top Cream", 2.8, 41.0, 4.6, 3.0, 4.4, "cream", "berry", turn=-10, on="Back Row / Gift 4")

# --- 6. Front apron: the low gifts players start from --------------------------------------
L.add("Entrance / Cream Catch", 9.0, -40.6, 9.0, 2.2, 6.0, "cream", "berry", turn=-6)
L.add("Entrance / Ruby Step", 13.6, -41.0, 6.0, 2.2, 4.6, "red", "gold", turn=4, on="Entrance / Cream Catch")
L.add("Entrance / Green Step", -0.6, -43.0, 5.6, 3.0, 4.4, "green", "ivory", turn=12)
L.add("Entrance / Navy Step", -20.0, -39.0, 6.6, 3.4, 5.0, "navy", "ivory", turn=22)
L.add("Entrance / Cream Side", 29.0, -34.6, 6.4, 4.0, 5.0, "cream", "gold", turn=-30)

props = [
    dict(name="trunk", x=20.5, z=39.8, y=0, w=9, d=9, h=30, rgb=(.25, .13, .06)),
    dict(name="tree", x=20.5, z=39.8, y=24, w=64, d=64, h=130, rgb=(.05, .30, .10), cone=True),
    dict(name="sofa", x=-62, z=96, y=0, w=110, d=42, h=34, rgb=(.45, .03, .06), turn=-8),
    dict(name="sofa back", x=-62, z=112, y=34, w=110, d=12, h=26, rgb=(.40, .03, .05), turn=-8),
    dict(name="table", x=6, z=112, y=0, w=20, d=18, h=26, rgb=(.30, .15, .07)),
    dict(name="lamp", x=6, z=112, y=26, w=10, d=10, h=30, rgb=(.95, .70, .40)),
    dict(name="back wall", x=0, z=140, y=0, w=520, d=2, h=260, rgb=(.05, .08, .30)),
    dict(name="right wall", x=150, z=0, y=0, w=2, d=400, h=260, rgb=(.05, .08, .30)),
]
for g in L.gifts:
    g["z"] += 0.0
out = dict(rug_radius=RUG_RADIUS, rug_z=RUG_Z, track_height=TRACK_HEIGHT, track=course,
           gifts=L.gifts, colors=COLORS, props=props)
json.dump(out, open(os.path.join(os.path.dirname(__file__), "layout.json"), "w"), indent=1)


def corridor_hits():
    """Gifts that poke into the space the train needs above the rail."""
    hits = set()
    for i, (u, v) in enumerate(course):
        probe = dict(x=u, z=v, w=5.2, d=2.4, turn=tangent_turn(i))
        for g in L.gifts:
            if g["role"] == "rail":
                continue
            if g["y"] + g["h"] <= BED_H + .01 and overlap_2d(probe, g):
                continue          # sits beside or under the rail bed height: checked as an overlap instead
            if g["y"] < TRACK_HEIGHT + 6.6 and g["y"] + g["h"] > BED_H and overlap_2d(probe, g):
                hits.add(g["name"])
    return sorted(hits)


off = [g["name"] for g in L.gifts if max(math.hypot(cx, cz - RUG_Z) for cx, cz in corners(g)) > RUG_RADIUS - 1.0]
print("off the rug:", off)
print(len(L.gifts), "gifts")
print("overlaps:", overlaps(L.gifts))
print("unsupported:", [b for b in supported(L.gifts) if "on2" not in L.by_name[b[0]]])
print("train corridor:", corridor_hits())
