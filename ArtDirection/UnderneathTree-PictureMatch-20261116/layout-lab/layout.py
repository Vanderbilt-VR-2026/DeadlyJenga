"""Underneath the Tree gift layout, as data.

Course frame: x = right, z = back (away from the picture camera), y = up.
Origin = rug centre on the rug surface. Units = course units (map scales them later).
Every gift is an upright box that may be turned about the vertical axis.
"""
import math, random

RUG_RADIUS = 65.0
TRACK_HEIGHT = 6.5          # rail top above the rug
SAMPLES, PER_SECTION = 128, 8

COLORS = {
    "cream": (.93, .80, .58), "navy": (.03, .10, .45), "green": (.03, .33, .13),
    "red": (.62, .02, .05), "gold": (.95, .63, .18), "ivory": (.98, .89, .72),
    "berry": (.80, .03, .06),
}
RIBBON_FOR = {"cream": ["gold", "berry"], "navy": ["gold", "ivory", "berry"],
              "green": ["gold", "ivory"], "red": ["gold", "ivory"]}


def track_point(t):
    """Lopsided kid-built loop: wide egg with a small outward jog at the back right."""
    u = 41.0 * math.cos(t) + 1.6 * math.sin(3 * t)
    v = 30.0 * math.sin(t) + 1.4 * math.sin(2 * t) + 2.2 * math.exp(-((t - 0.95) / 0.35) ** 2)
    return u, v


def track():
    return [track_point(i * 2 * math.pi / SAMPLES) for i in range(SAMPLES)]


class Layout:
    def __init__(self, seed=7):
        self.gifts = []
        self.rand = random.Random(seed)
        self.by_name = {}

    def top(self, name):
        g = self.by_name[name]
        return g["y"] + g["h"]

    def add(self, name, x, z, w, h, d, color, ribbon=None, turn=0.0, on=None, y=None,
            bow=True, role="gift", shape="box"):
        if y is None:
            y = self.top(on) if on else 0.0
        if ribbon is None:
            ribbon = self.rand.choice(RIBBON_FOR[color])
        g = dict(name=name, x=x, z=z, y=y, w=w, h=h, d=d, turn=turn, color=color,
                 ribbon=ribbon, on=on, bow=bow, role=role, shape=shape)
        assert name not in self.by_name, name
        self.gifts.append(g)
        self.by_name[name] = g
        return name


def corners(g, grow=0.0):
    c, s = math.cos(math.radians(g["turn"])), math.sin(math.radians(g["turn"]))
    hw, hd = g["w"] / 2 + grow, g["d"] / 2 + grow
    return [(g["x"] + c * a + s * b, g["z"] - s * a + c * b)
            for a, b in ((-hw, -hd), (hw, -hd), (hw, hd), (-hw, hd))]


def overlap_2d(a, b, grow=0.0):
    pa, pb = corners(a, grow), corners(b, grow)
    for poly in (pa, pb):
        for i in range(4):
            ex, ez = poly[(i + 1) % 4][0] - poly[i][0], poly[(i + 1) % 4][1] - poly[i][1]
            nx, nz = -ez, ex
            la = [p[0] * nx + p[1] * nz for p in pa]
            lb = [p[0] * nx + p[1] * nz for p in pb]
            if max(la) <= min(lb) + 1e-6 or max(lb) <= min(la) + 1e-6:
                return False
    return True


def overlaps(gifts, gap=0.12):
    """Pairs of gifts whose volumes intersect (resting contact is allowed)."""
    bad = []
    for i, a in enumerate(gifts):
        for b in gifts[i + 1:]:
            if a["y"] + a["h"] <= b["y"] + 1e-4 or b["y"] + b["h"] <= a["y"] + 1e-4:
                continue
            if overlap_2d(a, b, -gap / 2):
                bad.append((a["name"], b["name"]))
    return bad


def supported(gifts):
    """Gifts above the rug whose footprint centre is not over the gift they rest on."""
    names = {g["name"]: g for g in gifts}
    bad = []
    for g in gifts:
        if g["y"] < 1e-4:
            continue
        base = names.get(g["on"]) if g["on"] else None
        if base is None or abs(base["y"] + base["h"] - g["y"]) > 1e-3:
            bad.append((g["name"], "no base at its height"))
            continue
        centre = dict(g, w=.2, d=.2)
        if not overlap_2d(centre, base):
            bad.append((g["name"], "centre hangs off " + base["name"]))
    return bad
