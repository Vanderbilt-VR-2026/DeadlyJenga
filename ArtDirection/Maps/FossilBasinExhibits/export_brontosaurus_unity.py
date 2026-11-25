# Exports the placed brontosaurus (with its platforms, pylons, back stair and links to the arena)
# for the Unity Fossil Basin map: a JSON layout of members, collision boxes and joints, plus a
# gzip mesh file with one vertex-coloured mesh per member. Coordinates are Unity map space.
# Run: blender --background --python export_brontosaurus_unity.py -- "<Megalodon Hoist folder>" "<Unity Resources folder>"
import os, sys, gzip, struct
args = sys.argv[sys.argv.index("--") + 1:]
HERE, RES = args[0], args[1]
exec(open(os.path.join(HERE, "common.py"), encoding="utf-8").read(), globals())
def finish(*a, **k): pass
def make_tar(group): pass
LINKS = coll("08 • Expansion links")
BRONTO_AT = (-53.15, 7.25, -0.51)
transformed(lambda: exec(open(os.path.join(HERE, "build_brontosaurus.py"), encoding="utf-8").read(), globals()), BRONTO_AT, 0)
bridge("Link bridge T rex switchback to brontosaurus skull", (-30.8, 7.25), (-32.45, 7.25), 1.6, 18.55, LINKS)
join("Link bridge T rex switchback to brontosaurus skull", "map", (-30.9, 7.25, 18.35), 5200)
join("Link bridge T rex switchback to brontosaurus skull", "Brontosaurus skull", (-32.4, 7.25, 18.45), 1500)
bridge("Link causeway tide landing to brontosaurus", (-25.9, -7.4), (-46.1, 4.6), 1.8, 3.04, LINKS, piers=True)
join("Link causeway tide landing to brontosaurus", "map", (-26.0, -7.35, 2.84), 5200)
join("Link causeway tide landing to brontosaurus", "Front left foot platform strip 0", (-46.0, 4.55, 2.84), 5200)

# Structural joints by contact, as in finish().
STRENGTH = {"pier": 7200, "timber": 8000, "deck": 5200, "roof": 4000, "stair": 3900}
def boxes_of(m):
    out = []
    for c, s, q in m["proxies"]:
        pts = [c + q @ Vector((s.x * x / 2, s.y * y / 2, s.z * z / 2)) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
        out.append((Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])))
    return out
struct_ = [(n, boxes_of(m), m["kind"]) for n, m in members.items() if m["kind"] in STRENGTH and m["proxies"]]
for i, (na, ba, ka) in enumerate(struct_):
    if ka == "pier" and any(lo.z < .1 + BRONTO_AT[2] for lo, hi in ba):
        join(na, "bearing", (ba[0][0] + ba[0][1]) / 2 - Vector((0, 0, (ba[0][1].z - ba[0][0].z) / 2)), 7200)
    for nb, bb, kb in struct_[i + 1:]:
        hit = None
        for la, ha in ba:
            for lb, hb in bb:
                if all(la[k] <= hb[k] + .08 and lb[k] <= ha[k] + .08 for k in range(3)):
                    hit = Vector([(max(la[k], lb[k]) + min(ha[k], hb[k])) / 2 for k in range(3)]); break
            if hit: break
        if hit: join(na, nb, hit, min(STRENGTH[ka], STRENGTH[kb]))

# Game meshes: no subdivision, bones lightly decimated, so the Quest stays within budget.
for o in bpy.data.objects:
    for md in o.modifiers:
        if md.type == "SUBSURF": md.levels = 0
        if md.type == "DISPLACE": md.strength = 0
COLORS = [("Terracotta", (223, 100, 67)), ("teal", (42, 143, 145)), ("clay rim", (173, 73, 49)), ("beam", (98, 55, 44)),
          ("brass", (239, 171, 77)), ("bronze", (90, 60, 40)), ("socket interior", (70, 52, 40)), ("tooth", (250, 236, 205)),
          ("cartilage", (214, 186, 146)), ("bone", (238, 214, 172)), ("amber", (255, 190, 90)), ("Speckled", (230, 218, 190))]
def colour(mat_name):
    low = mat_name.lower()
    for key, c in COLORS:
        if key.lower() in low: return c
    return (238, 214, 172)

def U(v): return Vector((v.x, v.z, v.y))
dg = bpy.context.evaluated_depsgraph_get()
layout = {"members": [], "joints": []}
blob = bytearray(); total_tris = 0
names = [n for n, m in members.items() if m["proxies"]]
blob += struct.pack("<i", len(names))
for name in names:
    m = members[name]
    cen = sum((c for c, _, _ in m["proxies"]), Vector()) / len(m["proxies"])
    pu = U(cen)
    mass = round(min(30, 5 + 2 * len(m["proxies"])), 1) if m["kind"] == "bone" else round(max(3, min(90, sum(s.x * s.y * s.z for _, s, _ in m["proxies"]) * 40)), 1)
    layout["members"].append({"name": name, "kind": m["kind"], "mass": mass, "p": dict(zip("xyz", map(lambda f: round(f, 4), pu))),
        "boxes": [{"p": dict(zip("xyz", map(lambda f: round(f, 4), U(c)))), "size": {"x": round(s.x, 4), "y": round(s.y, 4), "z": round(s.z, 4)},
                   "forward": dict(zip("xyz", map(lambda f: round(f, 5), U(q @ Vector((0, 0, 1)))))),
                   "up": dict(zip("xyz", map(lambda f: round(f, 5), U(q @ Vector((0, 1, 0))))))} for c, s, q in m["proxies"]]})
    verts, idx = [], []
    for o in m["coll"].objects:
        if o.type != "MESH" or o.hide_render: continue
        if m["kind"] == "bone" and len(o.data.polygons) > 60 and not any(md.type == "BOOLEAN" for md in o.modifiers):
            d = o.modifiers.new("Game decimate", "DECIMATE"); d.ratio = .6
        ev = o.evaluated_get(dg); me = ev.to_mesh(); me.calc_loop_triangles()
        mw = o.matrix_world; nm = mw.to_3x3().inverted().transposed()
        cols = [colour(sl.material.name) if sl.material else (238, 214, 172) for sl in o.material_slots] or [(238, 214, 172)]
        cn = me.corner_normals
        for tri in me.loop_triangles:
            c = cols[min(tri.material_index, len(cols) - 1)]
            base = len(verts)
            for li in tri.loops:
                v = mw @ me.vertices[me.loops[li].vertex_index].co
                n = (nm @ cn[li].vector).normalized()
                verts.append((U(v) - pu, U(n), c))
            idx += [base, base + 2, base + 1]       # the axis swap mirrors, so flip winding
        ev.to_mesh_clear()
    total_tris += len(idx) // 3
    blob += struct.pack("<ii", len(verts), len(idx))
    for p, n, c in verts: blob += struct.pack("<6f4B", p.x, p.y, p.z, n.x, n.y, n.z, c[0], c[1], c[2], 255)
    blob += struct.pack(f"<{len(idx)}i", *idx)
layout["joints"] = [{"a": j["a"], "b": j["b"], "at": dict(zip("xyz", map(lambda f: round(f, 4), U(Vector(j["at"]))))), "breakForce": j["breakForce"]}
                    for j in joints if j["a"] in names and (j["b"] in names or j["b"] in ("bearing", "map"))]
json.dump(layout, open(os.path.join(RES, "FossilBasinBrontosaurus.json"), "w"), separators=(",", ":"))
open(os.path.join(RES, "FossilBasinBrontosaurusMeshes.bytes"), "wb").write(gzip.compress(bytes(blob), 9))
print("EXPORT members", len(names), "joints", len(layout["joints"]), "triangles", total_tris)
