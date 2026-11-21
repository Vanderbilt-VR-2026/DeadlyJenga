"""Build one FBX per 100 m tile from the pipeline's VC mesh (.npz) files. Runs inside Blender.

    blender -b --factory-startup --python blender/export_tiles.py -- --plan PLAN.json [--tiles T_00_00 ...]

PLAN.json is written by pipeline/export_all.py (it decides which files go into
which object). Each tile plan lists:
  objects: name, role, src (npz files merged into one object), collider (bool)
  empties: name, pos (campus X, Y, Z), forward (campus dX, dY or null)
  fbx:     output file;  stats: export stats JSON (archive derived/export/v1)

Every object is a direct child of the scene root with an identity transform;
vertices are campus coordinates (unchanged; see vc_fbx.py for the axis reasoning,
calibrated against a real Unity import on 2026-11-20), so every tile lands at the Unity map root.

Visual meshes get:
  - one material slot per material id used, named exactly mat_<id> (one shared
    material per id, so there are never ".001" copies),
  - a UV map named UV0 (first UV layer) from the npz uv0,
  - a byte color attribute "Col" (face corner) with R = material index, G = B = A = 255,
  - flat shading (faceted, suits the candy look).
Collider meshes (COL_*) get geometry only: Unity strips their renderer.
HLOD (plan "single_material": "mat_mixed") gets ONE slot for every face; color R
still carries each face's material index, UV0 is copied per source (charts of
different sources overlap; it is a far-only mesh and is never baked).
"""
import argparse
import math
import os
import sys
import time
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import vc_fbx  # noqa: E402

TOOLS = Path(__file__).resolve().parent.parent
MATERIAL_IDS = TOOLS / "config" / "material_ids.json"
MIXED = "mat_mixed"


def clear_scene():
    """Remove every object and mesh, keep the shared materials."""
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me, do_unlink=True)


def shared_materials():
    """One Blender material per material id, named mat_<id>."""
    names = vc_fbx.material_names(MATERIAL_IDS)
    for m in list(bpy.data.materials):          # factory file's "Material" etc.
        if m.name not in names.values():
            bpy.data.materials.remove(m)
    names = dict(names)
    names[MIXED] = MIXED                          # the one HLOD slot
    mats = {}
    for index, name in names.items():
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        if mat.name != name:
            raise RuntimeError(f"material name clash: wanted {name}, got {mat.name}")
        mats[index] = mat
    return mats


def build_mesh_object(name, mesh, mats, collider, single=None):
    """Make one Blender mesh object from a merged VC mesh dict."""
    v = (mesh["verts"] * vc_fbx.BLENDER_FROM_CAMPUS).astype(np.float32)
    f = mesh["faces"].astype(np.int32)
    n = len(f)
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(v))
    me.vertices.foreach_set("co", v.ravel())
    me.loops.add(3 * n)
    me.loops.foreach_set("vertex_index", f.ravel())
    me.polygons.add(n)
    me.polygons.foreach_set("loop_start", np.arange(0, 3 * n, 3, dtype=np.int32))
    me.update(calc_edges=True)
    if len(me.polygons) != n:
        raise RuntimeError(f"{name}: Blender kept {len(me.polygons)} of {n} triangles")

    if not collider:
        if single:
            # One slot for the whole mesh (HLOD); the per-face material stays in color R.
            me.materials.append(mats[single])
            me.polygons.foreach_set("material_index", np.zeros(n, np.int32))
        else:
            used = sorted(int(i) for i in np.unique(mesh["face_mat"]))
            slot_of = np.zeros(256, np.int32)
            for slot, index in enumerate(used):
                me.materials.append(mats[index])
                slot_of[index] = slot
            me.polygons.foreach_set("material_index", slot_of[mesh["face_mat"]])
        uv = me.uv_layers.new(name="UV0")
        uv.data.foreach_set("uv", mesh["uv0"].astype(np.float32).ravel())
        # Material index in R as a raw byte: color_srgb on a byte attribute is byte/255 exactly.
        col = me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
        rgba = np.ones((n, 3, 4), np.float32)
        rgba[:, :, 0] = (mesh["face_mat"].astype(np.float32) / 255.0)[:, None]
        col.data.foreach_set("color_srgb", rgba.ravel())
        me.color_attributes.active_color = col
        me.color_attributes.render_color_index = 0
    me.polygons.foreach_set("use_smooth", np.zeros(n, bool))   # faceted
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if ob.name != name:
        raise RuntimeError(f"object name clash: wanted {name}, got {ob.name}")
    return ob


def add_empty(item):
    ob = bpy.data.objects.new(item["name"], None)
    ob.empty_display_type = "ARROWS"
    ob.empty_display_size = 0.5
    ob.location = tuple(np.asarray(item["pos"], float) * vc_fbx.BLENDER_FROM_CAMPUS)
    fwd = item.get("forward")
    if fwd:
        ob.rotation_euler = (0.0, 0.0, vc_fbx.climb_angle(fwd[0], fwd[1]))
    bpy.context.scene.collection.objects.link(ob)
    if ob.name != item["name"]:
        raise RuntimeError(f"empty name clash: {item['name']}")
    return ob


def export_tile(plan, mats):
    t0 = time.time()
    clear_scene()
    stats = {"tile": plan["tile"], "zone": plan.get("zone"), "fbx": plan["fbx"], "signature": plan.get("signature"),
             "made": time.strftime("%Y-%m-%dT%H:%M:%S"), "objects": [], "empties": []}
    if plan.get("stats") and Path(plan["stats"]).exists():
        Path(plan["stats"]).unlink()      # never leave old stats behind if this export fails
    for obj in plan["objects"]:
        mesh = vc_fbx.load_merged(obj["src"])
        if not len(mesh["faces"]):
            stats.setdefault("skipped", []).append(obj["name"] + " (no faces)")
            continue
        build_mesh_object(obj["name"], mesh, mats, obj.get("collider", False), obj.get("single_material"))
        stats["objects"].append({"name": obj["name"], "role": obj["role"], "triangles": int(len(mesh["faces"])),
                                 "vertices": int(len(mesh["verts"])), "src": obj["src"],
                                 "materials": [] if obj.get("collider") else
                                 sorted({int(i) for i in np.unique(mesh["face_mat"])}),
                                 "single_material": obj.get("single_material"),
                                 "note": obj.get("note")})
    for item in plan.get("empties", []):
        add_empty(item)
        stats["empties"].append(item["name"])

    out = Path(plan["fbx"])
    out.parent.mkdir(parents=True, exist_ok=True)
    tmp = out.with_name(out.stem + ".tmp.fbx")
    result = bpy.ops.export_scene.fbx(filepath=str(tmp), **vc_fbx.FBX_SETTINGS)
    if "FINISHED" not in result:
        raise RuntimeError(f"FBX export returned {result}")
    os.replace(tmp, out)
    stats["fbx_bytes"] = out.stat().st_size
    stats["seconds"] = round(time.time() - t0, 2)
    stats["settings"] = {k: (sorted(v) if isinstance(v, set) else v) for k, v in vc_fbx.FBX_SETTINGS.items()}
    stats["axes"] = "Blender data = campus; raw FBX = (X, Z, -Y); Unity = (X, Z, Y) (calibrated 2026-11-20, see blender/vc_fbx.py)"
    if plan.get("stats"):
        vc_fbx.write_json(plan["stats"], stats)
    return stats


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--plan", required=True, help="plan JSON from pipeline/export_all.py")
    p.add_argument("--tiles", nargs="*", help="only these tiles of the plan")
    args = p.parse_args(vc_fbx.blender_argv(sys.argv))

    plans = vc_fbx.load_plan(args.plan)["tiles"]
    if args.tiles:
        plans = [t for t in plans if t["tile"] in set(args.tiles)]
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    mats = shared_materials()
    failures = 0
    for plan in plans:
        try:
            s = export_tile(plan, mats)
            tris = sum(o["triangles"] for o in s["objects"])
            print(f"EXPORTED {plan['tile']} objects={len(s['objects'])} empties={len(s['empties'])} "
                  f"tris={tris} bytes={s['fbx_bytes']} {s['seconds']}s", flush=True)
        except Exception as e:  # keep going with the other tiles, report at the end
            failures += 1
            print(f"FAILED {plan['tile']}: {e!r}", flush=True)
    print(f"DONE tiles={len(plans)} failures={failures}", flush=True)
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
