"""Twitch report from joints.csv: per variant (tape / layers / all), how jerky each joint is.
Usage: python twitch.py joints.csv [detail]"""
import csv, sys, math
from collections import defaultdict

FPS = 30.0
rows = defaultdict(list)   # (variant, shot, actor) -> frames
for r in csv.DictReader(open(sys.argv[1])):
    rows[(r['variant'], r['shot'], r['actor'])].append(r)

def v(r, p): return (float(r[p + 'x']), float(r[p + 'y']), float(r[p + 'z']))
def q(r, p): return (float(r[p + 'x']), float(r[p + 'y']), float(r[p + 'z']), float(r[p + 'w']))
def sub(a, b): return tuple(x - y for x, y in zip(a, b))
def norm(a): return math.sqrt(sum(x * x for x in a))
def dot(a, b): return sum(x * y for x, y in zip(a, b))
def qangle(a, b):
    d = abs(sum(x * y for x, y in zip(a, b)))
    return math.degrees(2 * math.acos(min(1.0, d)))
def qinv_rot(qr, vec):
    # rotate vec by inverse of yaw-only part of qr (head frame, flat)
    x, y, z, w = qr
    yaw = math.atan2(2 * (w * y + x * z), 1 - 2 * (y * y + x * x))
    c, s = math.cos(-yaw), math.sin(-yaw)
    return (c * vec[0] + s * vec[2], vec[1], -s * vec[0] + c * vec[2])

def series(frames):
    """Per frame: root position, head rotation, hands relative to the head in the head's flat frame."""
    out = []
    for r in frames:
        if r['visible'] != '1': out.append(None); continue
        head, hq = v(r, 'h'), q(r, 'hq')
        out.append(dict(root=v(r, 'f'), head=hq, headpos=sub(head, v(r, 'f')),
                        left=qinv_rot(hq, sub(v(r, 'l'), head)), right=qinv_rot(hq, sub(v(r, 'r'), head)),
                        lq=q(r, 'lq'), rq=q(r, 'rq'), lw=v(r, 'l'), rw=v(r, 'r')))
    return out

def measure(vel, lo, pop):
    """vel: list of velocity vectors (or None). Returns moves per second, reversals per second, pops, moving share, rms accel."""
    n = len([x for x in vel if x is not None])
    if n < 4: return None
    speed = [norm(x) if x is not None else None for x in vel]
    moving = sum(1 for s in speed if s is not None and s > lo)
    starts = 0; was = False; rev = 0; pops = 0; acc = []
    for i, s in enumerate(speed):
        if s is None: was = False; continue
        now = s > lo
        if now and not was: starts += 1
        was = now
        if i > 0 and vel[i - 1] is not None:
            a = norm(sub(vel[i], vel[i - 1])) * FPS; acc.append(a)
            if s > lo and speed[i - 1] > lo and dot(vel[i], vel[i - 1]) < 0: rev += 1
            if i + 1 < len(vel) and vel[i + 1] is not None:
                # a pop: one frame much faster than both neighbours
                if s > pop and s > 3 * max(speed[i - 1], speed[i + 1], lo): pops += 1
                # a step: speed jumps and stays (discontinuity in position is a 1-frame spike, covered above)
    secs = n / FPS
    return dict(starts=starts / secs, rev=rev / secs, pops=pops, moving=moving / n, rms=math.sqrt(sum(a * a for a in acc) / max(1, len(acc))))

def vels(s, key, rot=False):
    out = [None]
    for i in range(1, len(s)):
        if s[i] is None or s[i - 1] is None: out.append(None); continue
        if rot:
            out.append((qangle(s[i][key], s[i - 1][key]) * FPS, 0, 0))
        else:
            out.append(tuple(x * FPS for x in sub(s[i][key], s[i - 1][key])))
    return out[1:]

JOINTS = [('head turn', 'head', True, 12, 150), ('head bob', 'headpos', False, .04, .5), ('left hand', 'left', False, .06, .9), ('right hand', 'right', False, .06, .9),
          ('left wrist', 'lq', True, 20, 250), ('right wrist', 'rq', True, 20, 250), ('root', 'root', False, .08, 2.5)]

detail = len(sys.argv) > 2
total = defaultdict(lambda: defaultdict(lambda: [0, 0, 0, 0, 0, 0]))
for (variant, shot, actor), frames in sorted(rows.items()):
    s = series(frames)
    for name, key, rot, lo, pop in JOINTS:
        m = measure(vels(s, key, rot), lo, pop)
        if not m: continue
        secs = len([x for x in s if x]) / FPS
        t = total[variant][name]
        t[0] += m['starts'] * secs; t[1] += m['rev'] * secs; t[2] += m['pops']; t[3] += m['moving'] * secs; t[4] += m['rms'] ** 2 * secs; t[5] += secs
        if detail and (m['pops'] or m['starts'] > 1.5):
            print(f"{variant:6} {shot} {actor:8} {name:11} moves/s {m['starts']:.2f} rev/s {m['rev']:.2f} pops {m['pops']} moving {m['moving']:.0%} rms acc {m['rms']:.1f}")

print("\nvariant  joint        moves/s  reversals/s  pops  moving  rms accel")
for variant in ('tape', 'layers', 'all'):
    for name, *_ in JOINTS:
        t = total[variant][name]
        if t[5] == 0: continue
        print(f"{variant:7}  {name:11}  {t[0] / t[5]:6.2f}  {t[1] / t[5]:10.2f}  {t[2]:4d}  {t[3] / t[5]:5.0%}  {math.sqrt(t[4] / t[5]):8.1f}")
