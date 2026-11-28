# Writes SphinxJunctionStructure.bytes from the members that sj_physics2.py tests, for the Unity map
# (SphinxJunctionMap.BuildBakedTown). Called from sj_physics2.py when SJ_EXPORT names the output file.
# Each member gets its designed bearing colliders (part kind 2: solid, not drawn) and its Blender meshes
# (kind 0: drawn) in map-local metres. Decorations go to the member they sit on or beside.
import bpy, struct, hashlib, math
from mathutils import Vector

CODE_BUILT = ('Railway span', 'Timber trestle bent', 'Depot platform timber bent', 'Depot boarding platform')
NO_VISUAL_COLLS = ('99', '11 |', '15 |', '08 ', '05 ')
NO_VISUAL_NAMES = ('Far skyline', 'Quicksand display', 'Dry quicksand', 'Survivor', 'Reference proportion',
                   'Locomotive', 'Coach', 'Tender', 'Copper Canyon locomotive')


def srgb(x):
    return round(max(0, min(1, (1.055 * x ** (1 / 2.4) - .055) if x > .0031308 else x * 12.92)) * 255)


def export(path, members, sources, frame_matrix, dg, islands):
    inv = frame_matrix.inverted()

    def enc(v):
        q = inv @ v
        return (q.x, q.z, q.y)

    print('EXPORT frame check: pyramid centre (36, 16.5, 2.7) ->', tuple(round(x, 3) for x in enc(Vector((36.0, 16.5, 2.7)))),
          'hall floor 2.78 ->', round(enc(Vector((36.0, 16.5, 2.78)))[1], 3), flush=True)
    owners = {n: {'col': parts, 'vis': []} for n, parts in members.items() if not n.startswith(CODE_BUILT)}
    # member boxes for placing decorations
    boxes = {}
    for n, o in owners.items():
        V = [v for Vp, _ in o['col'] for v in Vp] or [Vector()]
        boxes[n] = (Vector([min(v[i] for v in V) for i in range(3)]), Vector([max(v[i] for v in V) for i in range(3)]))

    def nearest(c):
        best, bd = None, 1e9
        for n, (lo, hi) in boxes.items():
            d = Vector([max(lo[i] - c[i], 0, c[i] - hi[i]) for i in range(3)]).length
            vol = math.prod(max(0.05, hi[i] - lo[i]) for i in range(3))
            score = d * 1000 + vol * 1e-3
            if score < bd:
                bd, best = score, n
        return best if bd < 3500 else None

    used = set()
    def visual_tris(orig, M, island=None):
        ev = orig.evaluated_get(dg)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        mats = [tuple(srgb(c) for c in m.diffuse_color[:3]) if m else (230, 166, 88) for m in me.materials] or [(230, 166, 88)]
        keep = None
        if island is not None:
            polys = [tuple(p.vertices) for p in me.polygons]
            up = list(range(len(me.vertices)))
            def fv(i):
                while up[i] != i:
                    up[i] = up[up[i]]; i = up[i]
                return i
            for p in polys:
                for j in p[1:]:
                    up[fv(j)] = fv(p[0])
            order = []
            seen = {}
            for k, p in enumerate(polys):
                r = fv(p[0])
                if r not in seen:
                    seen[r] = len(order); order.append(r)
            keep = {k for k, p in enumerate(polys) if seen[fv(p[0])] == island}
        verts = [enc(M @ v.co) for v in me.vertices]
        tris = []
        for t in me.loop_triangles:
            if keep is not None and t.polygon_index not in keep:
                continue
            a, b, c = t.vertices
            col = mats[min(t.material_index, len(mats) - 1)]
            tris.append((a, c, b, col))
        ev.to_mesh_clear()
        return verts, tris

    for n, srcs in sources.items():
        if n not in owners:
            continue
        for orig, M, island in srcs:
            owners[n]['vis'].append(visual_tris(orig, M, island))
            used.add(orig.name)

    placed = dropped = 0
    for inst in dg.object_instances:
        ob = inst.object
        if ob.type != 'MESH':
            continue
        orig = ob.original
        if orig.hide_render or orig.name in used:
            continue
        cn = orig.users_collection[0].name if orig.users_collection else ''
        if cn.startswith(NO_VISUAL_COLLS) or orig.name.startswith(NO_VISUAL_NAMES) or orig.name.startswith(CODE_BUILT):
            continue
        M = inst.matrix_world.copy()
        pts = [M @ Vector(b) for b in ob.bound_box]
        c = sum(pts, Vector()) / 8
        n = nearest(c)
        if n is None:
            dropped += 1
            continue
        owners[n]['vis'].append(visual_tris(orig, M))
        used.add(orig.name)
        placed += 1

    def write_part(f, kind, verts, tris):
        # split to the format's 16-bit limits, re-indexing each chunk
        CH = 20000
        for s0 in range(0, max(1, len(tris)), CH):
            chunk = tris[s0:s0 + CH]
            if not chunk:
                continue
            idx = {}
            pts = []
            out = []
            for a, b, c, col in chunk:
                ids = []
                for v in (a, b, c):
                    if v not in idx:
                        idx[v] = len(pts); pts.append(verts[v])
                    ids.append(idx[v])
                out.append((*ids, col))
            f.write(struct.pack('<BHH', kind, len(pts), len(out)))
            for p in pts:
                f.write(struct.pack('<fff', *p))
            for a, b, c, col in out:
                f.write(struct.pack('<HHHBBB', a, b, c, *col))
            yield 1

    names = sorted(owners)
    with open(path, 'wb') as f:
        def string(s):
            a = s.encode('utf-8'); f.write(struct.pack('<H', len(a))); f.write(a)
        f.write(struct.pack('<IH', 0x4a53424c, len(names)))
        npieces = 0
        for n in names:
            o = owners[n]
            string(n)
            count_pos = f.tell(); f.write(struct.pack('<H', 0))
            count = 0
            for Vp, Pp in o['col']:
                verts = [enc(v) for v in Vp]
                tris = []
                for p in Pp:
                    for j in range(1, len(p) - 1):
                        tris.append((p[0], p[j + 1], p[j], (0, 0, 0)))
                # the kinematic foundation keeps exact (concave) colliders, as the Blender test's passive meshes
                count += sum(write_part(f, 3 if n == 'Pyramid podium and hall floor' else 2, verts, tris))
            for verts, tris in o['vis']:
                count += sum(write_part(f, 0, verts, tris))
            end = f.tell(); f.seek(count_pos); f.write(struct.pack('<H', count)); f.seek(end)
            npieces += count
    data = open(path, 'rb').read()
    print('EXPORT', len(names), 'members', npieces, 'pieces', len(data), 'bytes', 'decor placed', placed, 'dropped', dropped,
          'sha256', hashlib.sha256(data).hexdigest(), flush=True)
