# Unity-faithful gravity test for "Sphinx Junction - Paintover Match.blend" (Blender rigid bodies).
#   blender -b "Sphinx Junction - Paintover Match.blend" --python sj_physics2.py -- <out_dir> <test>
#   test = stand | trestle | saloon | pyramid
# Rules copied from the Unity map (SphinxJunctionMap*.cs, JunctionModel, JunctionMember):
# - every structure member is its own body; mass = clamp(volume * 2, 4, 115)
# - pieces that overlap where they were authored ignore each other (ExcludeAuthoredOverlaps); nothing is welded
# - members driven into the sand are piles: fixed to the ground, break at mass * 1500 N
# - named connections are Fasten joints: break at mass * 190 N
# - a pull is the hand drive: 100/s^2 per metre toward the target, damped, clamped at 120 m/s^2;
#   a pulled pile lets go once the grip is 0.45 m from the target
# Decorations that Unity does not import (lamps, props, lettering) are left out.
import bpy, bmesh, json, math, os, sys, time, functools
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
print = functools.partial(print, flush=True)

args = sys.argv[sys.argv.index('--') + 1:]
OUT, TEST = args[0], args[1]
os.makedirs(OUT, exist_ok=True)
t0 = time.time()
scene = bpy.context.scene
dg = bpy.context.evaluated_depsgraph_get()
FPS, SUB = 60, 2
STAND_S, PULL_S, AFTER_S = 6.0, 5.0, 5.0

# ---------------------------------------------------------------- which member owns each mesh
SKIP_COLLS = ('99', '11 |', '15 |', '08 ', '09 ', '10 ', '06 ', '21', '13 |', '16 |')
SKIP_NAMES = ('Locomotive', 'Coach', 'Tender', 'Passenger', 'Caboose', 'Train ', 'Far skyline', 'Quicksand display', 'Survivor', 'Street dirt pad', 'Dry quicksand', 'Reference proportion',
              'Copper Canyon locomotive', 'Coach', 'Tender', 'Railway | red signal', 'West street | lamps',
              'South annex | lamps', 'South annex | braziers')
STRUCT_COLLS = ('01 ', '02 ', '05 ', '12 |', '14 |')        # Codex mirrors of the Unity members, one member per object

def strip(n):
    return n.split('.')[0] if '.' in n and n.rsplit('.', 1)[1].isdigit() else n

def owner_of(name, cn, M, ob):
    if 'lettering' in name or name.startswith(SKIP_NAMES) or cn.startswith(SKIP_COLLS):
        return None, False
    # new art (linked asset files)
    if name.startswith('Great pyramid | course'):
        return 'BLOCKS', True                                   # every stone block is a member
    if name.startswith(('Great pyramid | podium', 'Hall of the Sun Line', 'Great pyramid | ground entrance')):
        return 'Pyramid podium and hall floor', True            # foundation: fixed like the island
    if name.startswith('Great pyramid | processional stair'):
        return 'Pyramid broad exterior stair', True
    if name.startswith('Great pyramid |'):
        return name.split(' (')[0], True
    if name.startswith('Treasury'):
        return 'Pyramid treasury', True
    for b in ('General store', 'Sheriff office', 'Livery stable', 'Bank', 'Blacksmith', 'Undertaker'):
        if name.startswith(b + ' |'):
            return b + ' frame', True
    if name.startswith(('West street | island strip', 'West street | causeway', 'South annex | pad', 'South annex | steps')):
        return name, True
    # Codex pieces, as ArtDirection/SphinxJunction/export_structure.py groups them
    if cn.startswith(('17 |', 'Lion', 'Torso', 'Head,', 'Hindq', 'Tail and', 'Under-chest', 'Carved trim')):
        n = name
        if n.startswith('Body course'):
            import re
            m = re.search(r'(\d+) \| curved voussoir (\d+)', n)
            if m:
                i, j = map(int, m.groups())
                if i == 1:
                    return ('Sphinx shoulder bearing 1' if j <= 3 else 'Sphinx shoulder bearing -1') if (j <= 3 or j >= 13) else 'Sphinx chest lintel', True
                return f'Sphinx torso bay {i:02}', True
        if n.startswith('Sphinx striped'):
            return 'Sphinx carved crown', True
        if n.startswith(('Foreleg', 'Hind leg', 'Hind paw', 'Tail')):
            return strip(n), True
        if n.startswith('Forepaw'):
            return ('Sphinx left forepaw' if (M @ Vector(ob.bound_box[0])).x < 0 else 'Sphinx right forepaw'), True
        if n.startswith('Shoulder route'):
            return ('Sphinx west stairs' if (M @ Vector(ob.bound_box[0])).x < 0 else 'Sphinx east stairs'), True
        return None, False                                      # sculpt trim is visual
    if cn.startswith('18 |'):
        import re
        m = re.match(r'Bridge (\d+)', name)
        return (f'Rail bridge {m.group(1)}', True) if m else (None, False)
    if cn.startswith('05 ') and not name.startswith(('Railway span', 'Timber trestle', 'Depot', 'Driven', 'Station', 'Water tower')):
        return None, False                                      # the train itself is not structure
    if name.startswith(('Copper Canyon depot', 'Depot |')):
        solid = any(k in name for k in ('wall', 'floor board', 'shade boards', 'clapboard', 'jamb', 'lintel', 'ticket counter'))
        return ('Copper Canyon ticket room', True) if solid else (None, False)   # as export_structure.py groups it
    if name.startswith('Saloon wraparound veranda ') and cn.startswith(STRUCT_COLLS):
        # each saloon floor is one member: the wraparound veranda rings its storey, so apart they lock together
        return 'Saloon timber storey ' + name.split()[-1], True
    if cn.startswith(STRUCT_COLLS):
        # Codex mirrors each Unity member as a plainly named object ('Plaza paving -2,3', 'Railway span 18');
        # 'Name | detail' objects are the non-solid trim that Unity draws without a collider
        return (None, False) if ' | ' in name else (name, True)
    return None, False

SKIP_IN_CODEX_STRUCT = ('Saloon | ', 'Saloon pendant', 'Saloon back bar', 'Saloon porch awning', 'Saloon balcony |',
                        'Saloon upper |', 'Depot | waiting-room lantern', 'Market canopy', 'Station | ribbed',
                        'Porch knee brace', 'Tack and feed', 'Chamber | teal lantern', 'Stone gallery |')

items, todo = [], []
for inst in dg.object_instances:
    ob = inst.object
    if ob.type != 'MESH':
        continue
    orig = ob.original
    if orig.hide_render:
        continue
    name = orig.name
    cn = orig.users_collection[0].name if orig.users_collection else ''
    if name.startswith(SKIP_IN_CODEX_STRUCT):
        continue
    M = inst.matrix_world.copy()
    own, solid = owner_of(name, cn, M, orig)
    if own:
        todo.append((orig, name, M, own))
print('PHYS meshes', len(todo), round(time.time() - t0))

def islands(verts, polys):
    up = list(range(len(verts)))
    def fv(i):
        while up[i] != i:
            up[i] = up[up[i]]; i = up[i]
        return i
    for p in polys:
        for j in p[1:]:
            up[fv(j)] = fv(p[0])
    comp = {}
    for p in polys:
        comp.setdefault(fv(p[0]), []).append(p)
    out = []
    for ps in comp.values():
        used = sorted({j for p in ps for j in p}); rm = {j: n for n, j in enumerate(used)}
        out.append(([verts[j] for j in used], [tuple(rm[j] for j in p) for p in ps]))
    return out

PYR_M = Matrix.Translation((36.0, 16.5, 0)) @ Matrix.Rotation(math.radians(-40), 4, 'Z'); PINV = PYR_M.inverted()
members = {}
sources = {}
for orig, name, M, own in todo:
    ev = orig.evaluated_get(dg)
    me = ev.to_mesh()
    if not me or not me.polygons:
        ev.to_mesh_clear(); continue
    verts = [M @ v.co for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    ev.to_mesh_clear()
    parts = islands(verts, polys)
    if own == 'BLOCKS':
        # stone blocks are drawn 6-11 cm short of the 1.35 m course for the seams; their collider is the full
        # course height so each course bears on the one below (the Unity port uses the same block colliders)
        for k, (V, P) in enumerate(parts):
            z0 = min(v.z for v in V); zc = z0 + (max(v.z for v in V) - z0) / 2
            V = [Vector((v.x, v.y, z0 + 1.35 - 0.002)) if v.z > zc else v for v in V]
            # ... and the full length of its slot: the 7 cm drawn seams close, so each course works as a ring in
            # compression (stones that step inward over the hall only stand by bearing on their neighbours)
            L = [PINV @ v for v in V]
            lo2 = [min(q[i] for q in L) for i in (0, 1)]; hi2 = [max(q[i] for q in L) for i in (0, 1)]
            for ax in (0, 1):                     # seams along the face, and the 5 cm gap at the corners
                mid = (lo2[ax] + hi2[ax]) / 2
                for q in L:
                    q[ax] += 0.034 if q[ax] > mid else -0.034
            V = [PYR_M @ q for q in L]
            members.setdefault(f'{name} block {k}', []).append((V, P))
            sources.setdefault(f'{name} block {k}', []).append((orig, M, k))
    else:
        members.setdefault(own, []).extend(parts)
        sources.setdefault(own, []).append((orig, M, None))
# Unity gives each rail span one box collider, 4.04 x 0.31 x (length - 0.06), centred on its segment
# (SphinxJunctionMap.Railway.cs); the Blender model's mitred ends would shift its balance off the bent.
_audit = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'audit', 'audit.json')))
NODES = [Vector(n) for n in _audit['route_nodes_blender']]
SC = 1.103
for i in range(len(NODES)):
    n = f'Railway span {i}'
    if n not in members:
        continue
    a_, b_ = NODES[i], NODES[(i + 1) % len(NODES)]
    fwd = Vector((b_.x - a_.x, b_.y - a_.y, 0)); L = fwd.length; fwd.normalize()
    right = Vector((fwd.y, -fwd.x, 0)); mid = (a_ + b_) / 2
    z0 = min(v.z for Vp, _ in members[n] for v in Vp); h = 0.31 * SC
    hw, hl = 4.04 * SC / 2, (L - 0.06 * SC) / 2
    V = [Vector((mid.x, mid.y, z0)) + right * sx * hw + fwd * sy * hl + Vector((0, 0, sz * h)) for sx in (-1, 1) for sy in (-1, 1) for sz in (0, 1)]
    members[n] = [(V, [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)])]
# Unity builds the forepaw extension piers and paving in code (SphinxJunctionMap.Blender.cs); the Blender scene
# has no copy, so add their paving as fixed boxes at the Unity positions (the piers under them are piles).
FR = bpy.data.objects['Reference proportion rig | map width and height'].matrix_world
def unity_box(cx, cy, cz, w, h, d):
    out = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                out.append(FR @ Vector((cx + sx * w / 2, cz + sz * d / 2, cy + sy * h / 2)))
    return out, [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
for side in (-1, 1):
    V, P = unity_box(side * 6.2, 2.0, -16.61, 5.15, 1.0, 3.18)
    V2, P2 = unity_box(side * 6.2, 0.0 - 0.65, -16.61, 2.8, 4.3, 2.58)
    members[f'Forepaw extension paving {side}'] = [(V, P)]
    members[f'Forepaw extension driven pier {side}'] = [(V2, P2)]
print('PHYS members', len(members), round(time.time() - t0))
if os.environ.get('SJ_DIAG'):
    print('PHYS forepaw members', [n for n in members if 'orepaw' in n])

# ---------------------------------------------------------------- designed colliders
# Unity's port gives each member colliders that do not interpenetrate (Railway.cs spans sit exactly on their
# bents). Where the authored meshes sink more than 2.1 cm into each other, shrink the smaller loose member's
# collider toward each piece's centre until the pair only touches. Pieces resting on top of others keep their
# contact. (Blender's disable-collision constraints proved unreliable at this scale, so no pair is ignored.)
def is_fixed(n, parts):
    return n == 'Pyramid podium and hall floor' or n.startswith('Forepaw extension paving') or min(v.z for p in parts for v in p[0]) < 0.3
def shrink_parts(parts, sh, sz=0.0):
    # sh shrinks sideways only, so a shrunk member keeps bearing on what is under it; sz shrinks height
    out = []
    for Vp, Pp in parts:
        lo = Vector([min(v[i] for v in Vp) for i in range(3)]); hi = Vector([max(v[i] for v in Vp) for i in range(3)])
        c = (lo + hi) / 2; h = (hi - lo) / 2
        k = [max(0.002, h[i] - (sh if i < 2 else sz)) / h[i] if h[i] > 1e-6 else 1.0 for i in range(3)]
        out.append(([Vector((c.x + (v.x - c.x) * k[0], c.y + (v.y - c.y) * k[1], c.z + (v.z - c.z) * k[2])) for v in Vp], Pp))
    return out
cols = []
for n, parts in members.items():
    if is_fixed(n, parts):
        for k, part in enumerate(parts):
            cols.append(dict(name=n, parts=[part], fixed=True, key=(n, k)))
    else:
        cols.append(dict(name=n, parts=parts, fixed=False, key=(n, None)))
for c in cols:
    V = [v for p in c['parts'] for v in p[0]]
    c['lo'] = Vector([min(v[i] for v in V) for i in range(3)]); c['hi'] = Vector([max(v[i] for v in V) for i in range(3)])
    c['c0'] = (c['lo'] + c['hi']) / 2; c['vol'] = math.prod(max(0.01, c['hi'][i] - c['lo'][i]) for i in range(3))
SHRINK = {}
_tc = {}
def ctree(c):
    sh = SHRINK.get(c['key'], 0.0)
    key = (id(c), sh)
    if key not in _tc:
        V, P, off = [], [], 0
        for Vp, Pp in shrink_parts(c['parts'], sh + 0.0105, 0.0105):
            V += Vp; P += [tuple(off + j for j in p) for p in Pp]; off += len(Vp)
        _tc[key] = BVHTree.FromPolygons(V, P)
    return _tc[key]
def c_inside(A, B):
    c = A['c0']
    if any(c[k] <= B['lo'][k] or c[k] >= B['hi'][k] for k in range(3)):
        return False
    t = ctree(B); n = 0; o = c.copy(); d = Vector((0.0123, 0.0071, 1.0)).normalized()
    for _ in range(64):
        loc, nrm, idx, dist = t.ray_cast(o, d)
        if loc is None:
            break
        n += 1; o = loc + d * 1e-4
    return n % 2 == 1
def clash(A, B):
    return bool(ctree(A).overlap(ctree(B))) or c_inside(A, B) or c_inside(B, A)
cols.sort(key=lambda c: c['lo'].x)
LEVELS = [0.03, 0.06, 0.12, 0.25, 0.5]
rested = shrunk_n = stuck = 0
STUCK = []
TRIM = {}
for i, A in enumerate(cols):
    for B in cols[i + 1:]:
        if B['lo'].x > A['hi'].x - 0.021:
            break
        if any(A['lo'][k] > B['hi'][k] - 0.021 or B['lo'][k] > A['hi'][k] - 0.021 for k in (1, 2)):
            continue
        if (A['fixed'] and B['fixed']) or A['name'] == B['name'] or not clash(A, B):
            continue
        dbg = os.environ.get('SJ_DIAG') and any(k in A['name'] + B['name'] for k in ('Saloon timber storey 1', 'Saloon timber storey 0'))
        if A['lo'].z > B['hi'].z - 0.15 or B['lo'].z > A['hi'].z - 0.15:
            up_, low = (A, B) if A['lo'].z > B['hi'].z - 0.15 else (B, A)
            d = low['hi'].z - up_['lo'].z
            if d > 0.021 and not up_['fixed']:
                TRIM[up_['key']] = max(TRIM.get(up_['key'], 0.0), d + 0.002)   # seat it exactly on its support
            rested += 1
            if dbg: print('PHYS clash rest', A['name'][:40], '|', B['name'][:40])
            continue
        # who gives way: fixed pieces first, then ordinary members, then stone blocks, and rail supports last
        def rank(c):
            if c['fixed']: return 0
            if c['name'].startswith(('Timber trestle bent', 'Railway span', 'Great pyramid | ground entrance',
                                     'Great pyramid | front railway', 'Great pyramid | rear railway', 'Pyramid treasury')): return 3   # lintels and supports
            if c['name'].startswith('Great pyramid | course'): return 2
            return 1
        v = min((A, B), key=lambda c: (rank(c), c['vol']))
        if dbg: print('PHYS clash shrink', v['name'][:40], 'between', A['name'][:40], '|', B['name'][:40], 'stuck' if (A['name'], B['name']) in STUCK else '')
        # a loose member may lose at most a quarter of its width; then the other gives way; then they merge
        def give_way(c):
            cap = 1e9 if c['fixed'] else 0.25 * min(c['hi'].x - c['lo'].x, c['hi'].y - c['lo'].y)
            for lv in LEVELS:
                if lv > cap:
                    return False
                if SHRINK.get(c['key'], 0.0) >= lv:
                    continue
                SHRINK[c['key']] = lv
                if not clash(A, B):
                    return True
            return False
        other = B if v is A else A
        if not (give_way(v) or (rank(other) < 3 and give_way(other))):
            stuck += 1; STUCK.append((A['name'], B['name']))
        shrunk_n += 1
for (n, k), d in TRIM.items():
    out = []
    cut = min(v.z for Vp, _ in members[n] for v in Vp) + d       # measured from the member's own bottom
    for Vp, Pp in members[n]:
        top = max(v.z for v in Vp)
        if top - 0.02 <= cut:
            out.append((Vp, Pp)); continue
        out.append(([Vector((v.x, v.y, max(v.z, cut))) for v in Vp], Pp))
    members[n] = out
print('PHYS seated', len(TRIM), 'members on their supports')
for (n, k), sh in SHRINK.items():
    if k is None:
        members[n] = shrink_parts(members[n], sh)
    else:
        members[n][k] = shrink_parts([members[n][k]], sh)[0]
# pairs nested too deeply to separate (sculpted sphinx courses, blocks buried in the stair) become one member,
# as Unity covers the sphinx with dedicated collision hulls rather than the sculpt meshes
up = {}
def root(n):
    while up.get(n, n) != n:
        n = up[n]
    return n
for a, b in STUCK:
    ra, rb_ = root(a), root(b)
    if ra == rb_:
        continue
    if is_fixed(rb_, members[rb_]) or (not is_fixed(ra, members[ra]) and len(members[rb_]) > len(members[ra])):
        ra, rb_ = rb_, ra
    up[rb_] = ra
merged = 0
for n in list(members):
    r = root(n)
    if r != n:
        members[r] = members[r] + members.pop(n); merged += 1
        sources[r] = sources.get(r, []) + sources.pop(n, [])
print('PHYS merged nested members', merged)
print('PHYS designed colliders: shrunk', len(SHRINK), 'members over', shrunk_n, 'clashes; resting kept', rested, '; still clashing', stuck, round(time.time() - t0))

# small trim (bolts, slats, mouldings) is drawn but carries no collider, in Blender and in Unity alike
for n in list(members):
    big = [pp for pp in members[n] if max(max(v[i] for v in pp[0]) - min(v[i] for v in pp[0]) for i in range(3)) >= 0.25]
    if big:
        members[n] = big
if os.environ.get('SJ_EXPORT'):
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import sj_unity_export
    sj_unity_export.export(os.environ['SJ_EXPORT'], members, sources, FR, dg, islands)
    sys.exit(0)

# ---------------------------------------------------------------- bodies
for o in list(scene.objects):
    o.hide_render = True
col = bpy.data.collections.new('Physics test'); scene.collection.children.link(col)
bpy.ops.rigidbody.world_add()
rbw = scene.rigidbody_world
rbw.substeps_per_frame = SUB
rbw.solver_iterations = 20
scene.render.fps = FPS
FRAMES = int((STAND_S + (0 if TEST == 'stand' else PULL_S + AFTER_S)) * FPS)
rbw.point_cache.frame_start, rbw.point_cache.frame_end = 1, FRAMES
scene.frame_start, scene.frame_end = 1, FRAMES
rbw.collection = bpy.data.collections.new('RigidBodyWorld')
rbw.constraints = bpy.data.collections.new('RigidBodyConstraints')

def mesh_obj(name, V, P, parent=None):
    lo = Vector([min(v[i] for v in V) for i in range(3)]); hi = Vector([max(v[i] for v in V) for i in range(3)])
    c = (lo + hi) / 2
    me = bpy.data.meshes.new(name); me.from_pydata([v - c for v in V], [], P); me.validate()
    o = bpy.data.objects.new(name[:60], me); col.objects.link(o)
    o.location = c if parent is None else c - parent.location
    if parent is not None:
        o.parent = parent
    return o, lo, hi

def rb(o, kind, shape, mass=1.0):
    rbw.collection.objects.link(o)
    r = o.rigid_body
    r.type = kind; r.collision_shape = shape; r.mass = mass
    r.use_margin = True; r.collision_margin = 0.003
    r.friction = 0.6; r.restitution = 0.0
    r.linear_damping, r.angular_damping = 0.04, 0.1
    r.use_deactivation = True
    r.deactivate_linear_velocity, r.deactivate_angular_velocity = 0.04, 0.08
    return r

ground_me = bpy.data.meshes.new('ground')
gv = [(x * 300, y * 300, z * 1.0) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]   # BOX shapes are centred on the origin
ground_me.from_pydata(gv, [], [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)])
ground = bpy.data.objects.new('Test ground', ground_me); col.objects.link(ground); ground.location = (0, 0, -4)
rb(ground, 'PASSIVE', 'BOX')

bodies = {}
for name, parts in members.items():
    V = [v for p in parts for v in p[0]]
    lo = Vector([min(v[i] for v in V) for i in range(3)]); hi = Vector([max(v[i] for v in V) for i in range(3)])
    vol = sum(math.prod(max(0.02, max(v[i] for v in p[0]) - min(v[i] for v in p[0])) for i in range(3)) for p in parts) * 0.45   # JunctionModel.AddBakedPart: bounds volume * .45 per solid part
    mass = min(115.0, max(4.0, vol * 2.0))
    fixed = name == 'Pyramid podium and hall floor' or name.startswith('Forepaw extension paving')
    pile = (lo.z < 0.3) and not fixed
    # Unity piles are FixedJoints to the world that only fail when pulled; Blender's solver lets loaded
    # pile joints creep, so a pile is a fixed body here. None of the designed pulls grabs a pile.
    fixed = fixed or pile
    if len(parts) == 1:
        o, _, _ = mesh_obj(name, *parts[0])
        rb(o, 'PASSIVE' if fixed else 'ACTIVE', 'MESH' if fixed else 'CONVEX_HULL', mass)
        kids = []
    else:
        allP, off = [], 0
        for Vp, Pp in parts:
            allP += [tuple(off + j for j in p) for p in Pp]; off += len(Vp)
        if fixed:
            # one passive body per piece, so an authored overlap only switches off that piece (Unity ignores per collider)
            objs = []
            for k, (Vp, Pp) in enumerate(parts):
                po, _, _ = mesh_obj(f'{name} piece {k}', Vp, Pp)
                rb(po, 'PASSIVE', 'MESH'); objs.append(po)
            o, kids = objs[0], objs[1:]
        else:
            o, _, _ = mesh_obj(name, V, allP)
            rb(o, 'ACTIVE', 'COMPOUND', mass)
            o.hide_render = True
            kids = []
            for k, (Vp, Pp) in enumerate(parts):
                if len(Pp) < 2:
                    continue
                kd, _, _ = mesh_obj(f'{name} part {k}', Vp, Pp, parent=o)
                rb(kd, 'ACTIVE', 'CONVEX_HULL', 1.0)
                kids.append(kd)
    bodies[name] = dict(ob=o, kids=kids, lo=lo, hi=hi, c0=(lo + hi) / 2, dims=hi - lo, mass=mass, fixed=fixed, pile=pile,
                        verts=V, parts=parts)
print('PHYS bodies', len(bodies), 'fixed', sum(b['fixed'] for b in bodies.values()),
      'piles', sum(b['pile'] for b in bodies.values()), round(time.time() - t0))

def constraint(name, a, b, kind, threshold=None, no_collide=True):
    e = bpy.data.objects.new(name, None); col.objects.link(e)
    e.location = (a.matrix_world.translation + b.matrix_world.translation) / 2
    rbw.constraints.objects.link(e)
    c = e.rigid_body_constraint
    c.type = kind; c.object1 = a; c.object2 = b; c.disable_collisions = no_collide
    if threshold is not None:
        c.use_breaking = True; c.breaking_threshold = threshold
    return c

IMPULSE = 1.0 / FPS                  # force -> Blender impulse threshold, as the shared Fossil Basin test converts it

# piles and Fasten joints
piles, joints = {}, []
for n, b in bodies.items():
    if b['pile'] and not b['fixed']:
        piles[n] = constraint('pile ' + n, b['ob'], ground, 'FIXED', None if os.environ.get('SJ_NOBREAK') else b['mass'] * 1500 * IMPULSE)
FASTEN = [('Sphinx chest lintel', 'Sphinx shoulder bearing -1'), ('Sphinx chest lintel', 'Sphinx shoulder bearing 1'),
          ('Sphinx carved crown', 'Sphinx chest lintel')]
tail = sorted(n for n in bodies if n.startswith('Tail'))
prev = 'Sphinx torso bay 13'
for n in tail:
    FASTEN.append((n, prev)); prev = n
for a, b in FASTEN:
    a, b = root(a), root(b)                       # follow members merged with their nested neighbours
    if a != b and a in bodies and b in bodies:
        joints.append(constraint(f'fasten {a}', bodies[a]['ob'], bodies[b]['ob'], 'FIXED',
                                 bodies[a]['mass'] * 190 * IMPULSE))

# Stone blocks are mortared: each block is fastened to every block it touches (Unity Fasten joints,
# breaking at mass * 190 N, the lighter block's mass), so the hollow stepped shell stands as one masonry
# body until a pull, impact or disaster breaks the joints. Unity builds the same joints at load.
MASONRY = ((('Great pyramid | course', 'Great pyramid | ground entrance', 'Great pyramid | front railway',
             'Great pyramid | rear railway', 'Great pyramid | teal band'), 0.03),      # the pyramid's stone shell
           (('Sphinx', 'Tail', 'Hind', 'Foreleg'), 0.15))                           # the carved sphinx (looser sculpt joints)
mortar = 0
for group, tol in MASONRY:
    blocks = sorted((n for n in bodies if n.startswith(group) and not bodies[n]['fixed']), key=lambda n: bodies[n]['lo'].x)
    for i, a_ in enumerate(blocks):
        A = bodies[a_]
        for b_ in blocks[i + 1:]:
            B = bodies[b_]
            if B['lo'].x > A['hi'].x + tol:
                break
            if all(A['lo'][k] < B['hi'][k] + tol and B['lo'][k] < A['hi'][k] + tol for k in range(3)):
                m = min(A['mass'], B['mass'])
                joints.append(constraint('mortar', A['ob'], B['ob'], 'FIXED', m * 190 * IMPULSE, no_collide=False))
                mortar += 1
print('PHYS mortar joints', mortar)
ignored = 0
print('PHYS piles', len(piles), 'fasten', len(joints), 'ignored pairs', ignored, round(time.time() - t0))

# ---------------------------------------------------------------- designed pulls
def pick_pull():
    if TEST == 'trestle':
        return 'Timber trestle bent 14', Vector((0, 1, 0))
    if TEST == 'saloon':
        return 'Saloon timber storey 1', Vector((1, 0, 0))   # haul the upper floor off eastward, toward the open market side
    if TEST == 'pyramid':
        # designed release point: a survivor hauls the gilded capstone off the summit, east and down the face
        ex = Vector((math.cos(math.radians(-40)), math.sin(math.radians(-40)), 0))
        cap = [n for n in bodies if n.startswith('Great pyramid | teal band') and not bodies[n]['fixed']]
        return (cap[0], (ex + Vector((0, 0, 0.35))).normalized()) if cap else (None, None)
    return None, None
PULL, PDIR = pick_pull()
hand = None
if PULL:
    assert PULL in bodies, PULL
    pb = bodies[PULL]
    hme = bpy.data.meshes.new('hand'); hme.from_pydata([(0, 0, 0), (0.1, 0, 0), (0, 0.1, 0)], [], [(0, 1, 2)])
    hand = bpy.data.objects.new('Hand', hme); col.objects.link(hand)
    hand.location = pb['c0']
    r = rb(hand, 'ACTIVE', 'CONVEX_HULL', 1.0); r.kinematic = True; r.collision_collections[0] = False; r.collision_collections[19] = True
    sp = constraint('hand spring', hand, pb['ob'], 'GENERIC_SPRING', no_collide=True)
    sp.spring_type = 'SPRING2'
    for ax in 'xyz':
        setattr(sp, f'use_spring_{ax}', True)
        setattr(sp, f'spring_stiffness_{ax}', 100.0 * pb['mass'])
        setattr(sp, f'spring_damping_{ax}', 12.0)
    print('PHYS pull', PULL, 'mass', round(pb['mass'], 1), 'dir', tuple(round(v, 2) for v in PDIR))

# ---------------------------------------------------------------- meteor strikes (the game's MeteorShower impact)
# BasicDisaster.MeteorImpact gives every member within max(2, 0.8 * size) of the hit a velocity change of up to
# `force` (12 m/s by default for a 3 m meteor). Here a kinematic sphere of that radius sweeps through each hit
# point at about that speed. Strikes land on the upper pyramid faces over two seconds.
METEORS = []
if TEST == 'meteor':
    import random as _r
    rng = _r.Random(7)
    PMm = Matrix.Translation((36.0, 16.5, 0)) @ Matrix.Rotation(math.radians(-40), 4, 'Z')
    for k in range(8):
        z = rng.uniform(16, 34); side = k % 4
        h = 18.0 - 0.43 * (z - 2.7)
        u = rng.uniform(-0.6, 0.6) * h
        loc = {0: (u, -h, z), 1: (h, u, z), 2: (u, h, z), 3: (-h, u, z)}[side]
        nrm = {0: (0, 1, 0), 1: (-1, 0, 0), 2: (0, -1, 0), 3: (1, 0, 0)}[side]
        hit = PMm @ Vector(loc)
        d = (PMm.to_3x3() @ Vector(nrm) + Vector((0, 0, -1))).normalized()   # falling in from the sky
        sm = bpy.data.meshes.new('meteor')
        bm_ = bmesh.new(); bmesh.ops.create_icosphere(bm_, subdivisions=2, radius=2.4); bm_.to_mesh(sm); bm_.free()
        mo = bpy.data.objects.new(f'Meteor {k}', sm); col.objects.link(mo)
        mo.location = (0, 0, -200)
        r = rb(mo, 'ACTIVE', 'SPHERE', 1.0); r.kinematic = True
        t0f = int(STAND_S * FPS) + 1 + k * 15                  # one strike every quarter second
        METEORS.append((mo, hit, d, t0f))
    PULL_F_M = int(STAND_S * FPS) + 8 * 15 + 30
    print('PHYS meteors', len(METEORS))

# ---------------------------------------------------------------- simulate
STAND_F = int(STAND_S * FPS); PULL_F = STAND_F + int(PULL_S * FPS)
def world_c(n):
    return bodies[n]['ob'].evaluated_get(bpy.context.evaluated_depsgraph_get()).matrix_world.translation.copy()
grip0 = None
for f in range(1, FRAMES + 1):
    if hand is not None and STAND_F < f <= PULL_F:
        g = world_c(PULL)
        if grip0 is None:
            grip0 = g.copy(); target = grip0 + PDIR * 9.0
        lead = target - g
        if lead.length > 0.45 and PULL in piles:
            piles[PULL].enabled = False                         # Unity destroys the pile once the grip lags 0.45 m
        if lead.length > 1.2:
            lead = lead.normalized() * 1.2                      # 100/s^2 per metre, clamped at 120 m/s^2
        hand.location = g + lead
    elif hand is not None and f == PULL_F + 1:
        sp.enabled = False
    for mo, hit, d, t0f in METEORS:
        if t0f <= f < t0f + 12:                                 # 12 frames: 4.8 m sweep at 12 m/s
            mo.location = hit - d * 2.4 + d * (f - t0f) * (12.0 / FPS)
        elif f == t0f + 12:
            mo.location = (0, 0, -200)
    scene.frame_set(f)
    if f == STAND_F:
        stand_pos = {n: world_c(n) for n in bodies if not bodies[n]['fixed']}
    if os.environ.get('SJ_DIAG') and f == 10:
        mv = sorted(((world_c(n) - bodies[n]['c0']).length, n) for n in bodies if not bodies[n]['fixed'])
        print('PHYS early movers', [(round(d, 2), n[:45]) for d, n in mv[-12:] if d > 0.15])
    if f % 60 == 0 or f in (5, 20):
        pz = sorted((world_c(n).z - bodies[n]['c0'].z, n) for n in piles)
        print('PHYS frame', f, '/', FRAMES, round(time.time() - t0), 'piles sinking', sum(1 for d, _ in pz if d < -0.05),
              [(round(d, 2), n[:40], round(bodies[n]['mass'])) for d, n in pz[:4]])
        if os.environ.get('SJ_DIAG') and f >= 60:
            mv = sorted(((world_c(n) - bodies[n]['c0']).length, n) for n in bodies if not bodies[n]['fixed'])
            ff = [n for n in bodies if not bodies[n]['fixed'] and -5.3 < world_c(n).z - bodies[n]['c0'].z < -4.3]
            print('PHYS diag freefall', len(ff), ff[:6])
            import collections
            cat = collections.Counter(n.split(' |')[0].split(' block')[0][:28] for d, n in mv if d > 0.1)
            print('PHYS diag by kind', cat.most_common(14))
            hist = collections.Counter(min(int(d * 10), 20) for d, n in mv)
            print('PHYS diag move histogram (0.1 m bins)', sorted(hist.items()))
            print('PHYS diag over 10cm', sum(1 for d, _ in mv if d > 0.1), [(round(d, 2), n[:45]) for d, n in mv[-8:]])
            sys.exit(0)

dgf = bpy.context.evaluated_depsgraph_get()
def move_of(n, ref=None):
    b = bodies[n]
    M = b['ob'].evaluated_get(dgf).matrix_world
    h = b['dims'] / 2
    base = b['c0'] if ref is None else ref
    start = Matrix.Translation(b['c0'])
    worst = 0.0
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                p = Vector((sx * h.x, sy * h.y, sz * h.z))
                worst = max(worst, (M @ p - (b['c0'] + p)).length)
    return worst

def disp(n):
    return [round(v, 3) for v in (bodies[n]['ob'].evaluated_get(dgf).matrix_world.translation - bodies[n]['c0'])]
res = sorted(((round(move_of(n), 3), n, [round(v, 1) for v in bodies[n]['c0']], disp(n)) for n in bodies if not bodies[n]['fixed']), reverse=True)
stand_moves = sorted(((round((stand_pos[n] - bodies[n]['c0']).length, 3), n) for n in stand_pos), reverse=True)
summary = dict(test=TEST, resting_overlaps_kept=rested, shrunk_members=len(SHRINK), still_clashing=stuck, members=len(bodies), fixed=sum(b['fixed'] for b in bodies.values()), piles=sum(b['pile'] for b in bodies.values()),
               fasten=len(joints), ignored_pairs=ignored, pull=PULL,
               stand_over_10cm=sum(1 for m, _ in stand_moves if m > 0.10), stand_worst=stand_moves[:15],
               end_over_50cm=sum(1 for r in res if r[0] > 0.5), end_over_10cm=sum(1 for r in res if r[0] > 0.1), end_worst=res[:400],
               piles_broken=sum(1 for c in piles.values() if not c.enabled), seconds=round(time.time() - t0),
               pyramid_stones_over_50cm=sum(1 for r in res if r[0] > 0.5 and r[1].startswith('Great pyramid | course')),
               pyramid_stones=sum(1 for n in bodies if n.startswith('Great pyramid | course')))
if PULL:
    summary['pulled_member_moved'] = round(move_of(PULL), 2)
json.dump(summary, open(os.path.join(OUT, f'unity-faithful-{TEST}.json'), 'w'), indent=1)
print('PHYS DONE', json.dumps({k: v for k, v in summary.items() if k not in ('end_worst', 'stand_worst')}))

# ---------------------------------------------------------------- picture
if os.environ.get('SJ_NORENDER'):
    sys.exit(0)
for n, b in bodies.items():
    m = 0 if b['fixed'] else move_of(n)
    colr = (0.35, 0.45, 0.6, 1) if b['fixed'] else (0.92, 0.88, 0.8, 1) if m < 0.1 else (1, 0.55, 0.1, 1) if m < 0.5 else (0.9, 0.05, 0.05, 1)
    if n == PULL:
        colr = (0.2, 0.8, 0.3, 1)
    for o in [b['ob']] + b['kids']:
        o.color = colr
    if b['kids'] and not b['fixed']:
        b['ob'].hide_render = True
    else:
        b['ob'].hide_render = False
    for k in b['kids']:
        k.hide_render = False
ground.hide_render = True
if hand:
    hand.hide_render = True
scene.render.engine = 'BLENDER_WORKBENCH'
sh = scene.display.shading
sh.light = 'STUDIO'; sh.color_type = 'OBJECT'; sh.show_shadows = True; sh.show_cavity = True
scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = 1600, 800, 100
scene.view_settings.view_transform = 'Standard'
cams = [o for o in bpy.data.objects if o.type == 'CAMERA' and o.name.startswith('00 | Paintover matched overview')]
if cams:
    scene.camera = cams[0]
    scene.render.filepath = os.path.join(OUT, f'unity-faithful-{TEST}.png')
    bpy.ops.render.render(write_still=True)
# close-ups of the pyramid: the rear (left) face and the hall interior looking at the east wall
PM = Matrix.Translation((36.0, 16.5, 0)) @ Matrix.Rotation(math.radians(-40), 4, 'Z')
for tag, eye, look, lens in (('rear', (-48, 6, 30), (0, 0, 16), 40), ('front', (8, -55, 22), (0, 0, 14), 40),
                             ('hall-east', (-7, -6, 9), (12, 2, 11), 18), ('hall-west', (7, -2, 9), (-12, 2, 13), 18)):
    cd = bpy.data.cameras.new(tag); cd.lens = lens; cd.clip_start = 0.05
    co = bpy.data.objects.new('cam ' + tag, cd); col.objects.link(co)
    e = PM @ Vector(eye); l = PM @ Vector(look)
    co.location = e; co.rotation_euler = (l - e).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = co
    scene.render.filepath = os.path.join(OUT, f'unity-faithful-{TEST}-{tag}.png')
    bpy.ops.render.render(write_still=True)
for tag, e, l in (('saloon', (-10, -22, 13), (-25.7, -2.7, 6)), ('tail', (24, 32, 12), (10, 18, 3.5))):
    cd = bpy.data.cameras.new(tag); cd.lens = 32
    co = bpy.data.objects.new('cam ' + tag, cd); col.objects.link(co)
    co.location = e; co.rotation_euler = (Vector(l) - Vector(e)).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = co
    scene.render.filepath = os.path.join(OUT, f'unity-faithful-{TEST}-{tag}.png')
    bpy.ops.render.render(write_still=True)
print('PHYS RENDERED', round(time.time() - t0))
