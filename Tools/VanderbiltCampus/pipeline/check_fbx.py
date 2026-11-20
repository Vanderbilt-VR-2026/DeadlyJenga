"""Check exported tile FBX files against the VC mesh inputs they were built from.

    python pipeline/check_fbx.py --plan PLAN.json [--tiles T_00_00 ...] [--jobs 4]

Run with the pipeline's Python, it starts headless Blender (path from
config/local.json) on this same file, in parallel slices. Inside Blender it
reads each FBX twice:
  1. raw, with Blender's own FBX parser (io_scene_fbx.parse_fbx), so the
     numbers are what Unity will read: axis header, unit scale, object
     transforms, vertex bounds turned into Unity axes, the vertex color R of
     every triangle corner against the triangle's material slot;
  2. through Blender's FBX importer: object names, material slot names
     (mat_<id>, no ".001"), UV map "UV0" first, color bytes (R = index
     exactly), triangle counts.
Expected values come from the npz files listed in the plan.

Writes one JSON per tile next to the tile's export stats
(derived/export/v1/check/<tile>.json) and prints "CHECK <tile> PASS|FAIL".
Exit code 1 if any tile fails.
"""
import argparse
import json
import math
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parent
sys.path.insert(0, str(TOOLS / "blender"))

try:
    import bpy  # noqa: F401
    IN_BLENDER = True
except ImportError:
    IN_BLENDER = False

import numpy as np  # noqa: E402
import vc_fbx  # noqa: E402

TOL = 0.01          # meters, for bounds and marker positions


# ---- raw FBX reading (Blender's parser) -----------------------------------

def _props70(elem):
    out = {}
    for sub in elem.elems:
        if sub.id == b"Properties70":
            for p in sub.elems:
                if p.id == b"P":
                    out[p.props[0].decode()] = p.props[4:]
    return out


def _child(elem, name):
    for sub in elem.elems:
        if sub.id == name:
            return sub
    return None


def read_raw(path):
    from io_scene_fbx import parse_fbx
    root, _version = parse_fbx.parse(str(path))
    top = {e.id: e for e in root.elems}
    header = {k: v[0] for k, v in _props70(top[b"GlobalSettings"]).items()}
    models, geoms, mats = {}, {}, {}
    for e in top[b"Objects"].elems:
        name = e.props[1].split(b"\x00\x01")[0].decode()
        if e.id == b"Model":
            models[e.props[0]] = {"name": name, "type": e.props[2].decode(), "props": _props70(e)}
        elif e.id == b"Geometry":
            geoms[e.props[0]] = e
        elif e.id == b"Material":
            mats[e.props[0]] = name
    model_geom, model_mats = {}, {}
    for c in top[b"Connections"].elems:
        if c.props[0] != b"OO":
            continue
        child, parent = c.props[1], c.props[2]
        if child in geoms and parent in models:
            model_geom[parent] = child
        elif child in mats and parent in models:
            model_mats.setdefault(parent, []).append(mats[child])
        elif child in models and parent != 0:
            models[child]["parent"] = parent
    return header, models, geoms, model_geom, model_mats


def raw_geometry(g):
    verts = np.asarray(_child(g, b"Vertices").props[0], float).reshape(-1, 3)
    pvi = np.asarray(_child(g, b"PolygonVertexIndex").props[0], np.int64)
    out = {"verts": verts, "polys": int((pvi < 0).sum()), "corners": len(pvi),
           "triangles_only": bool(len(pvi) % 3 == 0 and (pvi[2::3] < 0).all() and (pvi[0::3] >= 0).all())}
    lc = _child(g, b"LayerElementColor")
    if lc is not None:
        colors = np.asarray(_child(lc, b"Colors").props[0], float).reshape(-1, 4)
        index = np.asarray(_child(lc, b"ColorIndex").props[0], np.int64)
        out["corner_rgba"] = colors[index]
        out["color_name"] = _child(lc, b"Name").props[0].decode()
    lm = _child(g, b"LayerElementMaterial")
    if lm is not None:
        out["poly_slot"] = np.asarray(_child(lm, b"Materials").props[0], np.int64)
        out["mat_mapping"] = _child(lm, b"MappingInformationType").props[0].decode()
    uvs = [e for e in g.elems if e.id == b"LayerElementUV"]
    out["uv_names"] = [_child(u, b"Name").props[0].decode() for u in uvs]
    return out


# ---- the checks for one tile ------------------------------------------------

def check_tile(plan, names):
    problems, info = [], {}
    fbx = Path(plan["fbx"])
    if not fbx.exists():
        return {"tile": plan["tile"], "pass": False, "problems": [f"missing {fbx}"]}
    header, models, geoms, model_geom, model_mats = read_raw(fbx)

    # Step 1 + 5 of the axis notes: header and unit.
    for k, want in vc_fbx.EXPECTED_HEADER.items():
        got = header.get(k)
        if got is None or abs(float(got) - float(want)) > 1e-9:
            problems.append(f"GlobalSettings {k} = {got}, expected {want}")
    info["header"] = {k: header.get(k) for k in vc_fbx.EXPECTED_HEADER}

    by_name = {m["name"]: (mid, m) for mid, m in models.items()}
    expected_meshes = {}
    for obj in plan["objects"]:
        mesh = vc_fbx.load_merged(obj["src"])
        if len(mesh["faces"]):
            expected_meshes[obj["name"]] = (obj, mesh)
    expected_names = set(expected_meshes) | {e["name"] for e in plan.get("empties", [])}
    got_names = set(by_name)
    if got_names != expected_names:
        problems.append(f"object names differ: missing {sorted(expected_names - got_names)[:10]}, "
                        f"extra {sorted(got_names - expected_names)[:10]}")
    for name, (mid, m) in by_name.items():
        if m.get("parent"):
            problems.append(f"{name} is not a direct child of the root")

    objects = {}
    for name, (obj, mesh) in expected_meshes.items():
        if name not in by_name:
            continue
        mid, m = by_name[name]
        exp = vc_fbx.expected_object(mesh)
        rec = {"triangles": exp["triangles"]}
        props = m["props"]
        for key, ident in (("Lcl Translation", 0.0), ("Lcl Rotation", 0.0), ("Lcl Scaling", 1.0)):
            if key in props and max(abs(float(x) - ident) for x in props[key][:3]) > 1e-6:
                problems.append(f"{name} {key} = {props[key][:3]} (needs identity)")
        g = raw_geometry(geoms[model_geom[mid]])
        if g["polys"] != exp["triangles"] or not g["triangles_only"]:
            problems.append(f"{name}: {g['polys']} polygons in FBX, expected {exp['triangles']} triangles")
        used = np.unique(mesh["faces"])
        vu = vc_fbx.fbx_to_unity(g["verts"])
        lo, hi = vu.min(0), vu.max(0)
        rec["unity_min"], rec["unity_max"] = lo.round(3).tolist(), hi.round(3).tolist()
        rec["fbx_min"], rec["fbx_max"] = g["verts"].min(0).round(3).tolist(), g["verts"].max(0).round(3).tolist()
        if len(used) and (np.abs(lo - exp["unity_min"]).max() > TOL or np.abs(hi - exp["unity_max"]).max() > TOL):
            problems.append(f"{name}: Unity bounds {rec['unity_min']}..{rec['unity_max']}, "
                            f"expected {exp['unity_min']}..{exp['unity_max']}")
        if obj.get("collider"):
            objects[name] = rec
            continue
        want_mats = [names[i] for i in exp["mat_indices"]]
        got_mats = model_mats.get(mid, [])
        rec["materials"], rec["uv_layers"] = got_mats, g["uv_names"]
        if "corner_rgba" in g:
            rec["color_r_bytes"] = sorted({int(round(v)) for v in np.unique(g["corner_rgba"][:, 0] * 255.0)})
        if sorted(got_mats) != sorted(want_mats):
            problems.append(f"{name}: materials {got_mats}, expected {want_mats}")
        if g["uv_names"][:1] != ["UV0"]:
            problems.append(f"{name}: UV layers {g['uv_names']} (first must be UV0)")
        if "corner_rgba" not in g:
            problems.append(f"{name}: no vertex colors")
        elif "poly_slot" in g and len(got_mats):
            r = g["corner_rgba"][:, 0] * 255.0
            if np.abs(r - np.round(r)).max() > 1e-3 or np.abs(g["corner_rgba"][:, 1:] - 1).max() > 1e-6:
                problems.append(f"{name}: color R not an exact byte, or G/B/A != 1")
            slot = g["poly_slot"] if g["mat_mapping"] == "ByPolygon" else np.zeros(g["polys"], np.int64)
            index_of_slot = np.array([int(next(i for i, n in names.items() if n == mn)) for mn in got_mats])
            want_r = np.repeat(index_of_slot[slot], 3)
            bad = int((np.round(r).astype(int) != want_r).sum())
            if bad:
                problems.append(f"{name}: {bad} corners whose color R disagrees with their mat_ slot")
            hist = np.bincount(index_of_slot[slot], minlength=256)
            if {str(i): int(hist[i]) for i in exp["mat_indices"]} != exp["faces_per_index"]:
                problems.append(f"{name}: triangles per material differ from the npz")
        objects[name] = rec

    # Empties: Unity position and (for climb markers) forward.
    for item in plan.get("empties", []):
        if item["name"] not in by_name:
            continue
        _mid, m = by_name[item["name"]]
        props = m["props"]
        t = np.array([float(x) for x in props.get("Lcl Translation", (0, 0, 0))[:3]])
        pos_u = vc_fbx.fbx_to_unity(t)
        erec = info.setdefault("empties", {})[item["name"]] = {
            "fbx_translation": t.round(4).tolist(), "unity_pos": pos_u.round(4).tolist(),
            "fbx_rotation": [round(float(a), 4) for a in props.get("Lcl Rotation", (0, 0, 0))[:3]]}
        want_u = vc_fbx.campus_to_unity(item["pos"])
        if np.abs(pos_u - want_u).max() > TOL:
            problems.append(f"{item['name']}: Unity position {pos_u.round(3).tolist()}, expected {want_u.round(3).tolist()}")
        if item.get("forward"):
            rx, ry, rz = (math.radians(float(a)) for a in props.get("Lcl Rotation", (0, 0, 0))[:3])
            fwd_f = _euler_xyz(rx, ry, rz) @ np.array([0.0, 0.0, 1.0])
            fwd_u = vc_fbx.fbx_to_unity(fwd_f)
            erec["unity_forward"] = fwd_u.round(5).tolist()
            d = np.array([item["forward"][0], 0.0, item["forward"][1]])
            d /= np.linalg.norm(d)
            if np.abs(fwd_u - d).max() > 1e-3:
                problems.append(f"{item['name']}: Unity forward {fwd_u.round(4).tolist()}, expected {d.round(4).tolist()}")

    # The same file through Blender's importer: names, slots, UV0, color bytes.
    more, seen, notes = check_with_importer(fbx, expected_meshes, names)
    problems += more
    info["notes"] = notes
    for name, b in seen.items():
        objects.setdefault(name, {})["import_color_bytes"] = b
    return {"tile": plan["tile"], "pass": not problems, "problems": problems, "info": info,
            "objects": objects, "fbx_bytes": fbx.stat().st_size}


def _euler_xyz(rx, ry, rz):
    """FBX eEulerXYZ: rotate about X, then Y, then Z (matrix Rz @ Ry @ Rx)."""
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    Rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    Ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    Rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return Rz @ Ry @ Rx


def check_with_importer(fbx, expected_meshes, names):
    import bpy
    problems, seen, notes = [], {}, []
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me, do_unlink=True)
    for ma in list(bpy.data.materials):
        bpy.data.materials.remove(ma)
    bpy.ops.import_scene.fbx(filepath=str(fbx), colors_type="SRGB", use_custom_normals=False)
    index_of = {n: i for i, n in names.items()}
    for name, (obj, mesh) in expected_meshes.items():
        ob = bpy.data.objects.get(name)
        if ob is None or ob.type != "MESH":
            problems.append(f"importer: no mesh object {name}")
            continue
        me = ob.data
        if len(me.polygons) != len(mesh["faces"]):
            # Blender's importer drops degenerate / duplicate triangles while building its mesh;
            # the raw FBX count (checked above) is what Unity reads, so this is only a note.
            notes.append(f"importer kept {len(me.polygons)} of {len(mesh['faces'])} triangles of {name} "
                         "(degenerate or duplicate triangles in the input)")
        if obj.get("collider"):
            continue
        slots = [m.name if m else None for m in me.materials]
        if any(s not in index_of for s in slots):
            problems.append(f"importer: {name} slot names {slots} (need exact mat_<id>)")
            continue
        if not me.uv_layers or me.uv_layers[0].name != "UV0":
            problems.append(f"importer: {name} first UV map is not UV0")
        if not me.color_attributes:
            problems.append(f"importer: {name} has no color attribute")
            continue
        col = me.color_attributes[0]
        rgba = np.empty(len(col.data) * 4, np.float32)
        col.data.foreach_get("color_srgb", rgba)
        r = np.round(rgba.reshape(-1, 4)[:, 0] * 255).astype(int)
        mi = np.empty(len(me.polygons), np.int32)
        me.polygons.foreach_get("material_index", mi)
        loop_start = np.empty(len(me.polygons), np.int32)
        me.polygons.foreach_get("loop_start", loop_start)
        slot_index = np.array([index_of[s] for s in slots])
        want = slot_index[mi]
        if col.domain == "CORNER":
            got = r[loop_start]
        else:
            vi = np.empty(len(me.loops), np.int32)
            me.loops.foreach_get("vertex_index", vi)
            got = r[vi[loop_start]]
        seen[name] = sorted(int(x) for x in np.unique(got))
        bad = int((got != want).sum())
        if bad:
            problems.append(f"importer: {name} {bad} faces whose color byte R != material index")
        if sorted(np.unique(want).tolist()) != sorted(np.unique(mesh["face_mat"]).tolist()):
            problems.append(f"importer: {name} material indices differ from the npz")
    return problems, seen, notes


def blender_main():
    p = argparse.ArgumentParser()
    p.add_argument("--plan", required=True)
    p.add_argument("--tiles", nargs="*")
    args = p.parse_args(vc_fbx.blender_argv(sys.argv))
    names = vc_fbx.material_names(TOOLS / "config" / "material_ids.json")
    plans = vc_fbx.load_plan(args.plan)["tiles"]
    if args.tiles:
        plans = [t for t in plans if t["tile"] in set(args.tiles)]
    failed = 0
    for plan in plans:
        try:
            res = check_tile(plan, names)
        except Exception as e:
            res = {"tile": plan["tile"], "pass": False, "problems": [f"checker crashed: {e!r}"]}
        res["signature"] = plan.get("signature")
        # The FBX must come from this plan: its stats carry the same signature.
        try:
            stats_sig = vc_fbx.load_plan(plan["stats"]).get("signature") if plan.get("stats") else plan.get("signature")
        except Exception:
            stats_sig = None
        if stats_sig != plan.get("signature"):
            res["pass"] = False
            res.setdefault("problems", []).append("export stats missing or from another plan (export failed?)")
        if plan.get("check"):
            vc_fbx.write_json(plan["check"], res)
        failed += not res["pass"]
        print(f"CHECK {plan['tile']} {'PASS' if res['pass'] else 'FAIL'} " + "; ".join(res["problems"][:5]), flush=True)
    sys.exit(1 if failed else 0)


# ---- pipeline-Python side: start Blender --------------------------------------

def blender_exe():
    sys.path.insert(0, str(HERE))
    from common import local_config
    return local_config()["blender"]


def run_blender(script, plan, tiles, log=None):
    """Start one headless Blender on a script with a plan and tile slice. Returns the Popen."""
    cmd = [blender_exe(), "-b", "--factory-startup", "--python-exit-code", "1",
           "--python", str(script), "--", "--plan", str(plan)]
    if tiles:
        cmd += ["--tiles", *tiles]
    out = open(log, "w", encoding="utf-8") if log else None
    return subprocess.Popen(cmd, stdout=out or subprocess.PIPE, stderr=subprocess.STDOUT, text=True), out


def slices(items, n):
    n = max(1, min(n, len(items)))
    return [items[i::n] for i in range(n)]


def run_parallel(script, plan, tiles, jobs, log_dir=None):
    """Run a Blender script over tile slices in parallel; return (ok, printed lines)."""
    procs = []
    for k, part in enumerate(slices(list(tiles), jobs)):
        log = Path(log_dir) / f"{Path(script).stem}_{k}.log" if log_dir else None
        if log:
            log.parent.mkdir(parents=True, exist_ok=True)
        procs.append((run_blender(script, plan, part, log), log))
    ok, lines = True, []
    for (proc, fh), log in procs:
        text = proc.communicate()[0]
        if fh:
            fh.close()
            text = Path(log).read_text(encoding="utf-8", errors="replace")
        lines += [ln for ln in text.splitlines() if ln.startswith(("CHECK", "EXPORTED", "FAILED", "DONE", "Error", "Traceback"))]
        ok &= proc.returncode == 0
    return ok, lines


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--plan", required=True, help="plan JSON written by export_all.py")
    p.add_argument("--tiles", nargs="*", help="only these tiles")
    p.add_argument("--jobs", type=int, default=4, help="parallel Blender processes")
    args = p.parse_args()
    plan = vc_fbx.load_plan(args.plan)
    tiles = args.tiles or [t["tile"] for t in plan["tiles"]]
    ok, lines = run_parallel(Path(__file__), args.plan, tiles, args.jobs)
    for ln in lines:
        print(ln)
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    blender_main() if IN_BLENDER else main()
