"""Shared helpers for the tile exporter and the FBX checker (numpy only, no bpy).

Both run inside Blender's own Python, which has numpy but not the pipeline's
other packages, so this file must not import shapely, pyproj and so on.

AXES (read this before changing anything here)
------------------------------------------------
Campus frame: X east, Y north, Z up, meters, right-handed (frame.py).
Unity wants:  x = X, y = Z, z = Y (frame.campus_to_unity), left-handed.

CALIBRATED against a real Unity import on 2026-11-20 (Unity 6.3, the builder's
ModelImporter settings: bakeAxisConversion = true, useFileScale = true). The
first version of this file assumed Unity negates raw x and pre-rotated the data
180 degrees; that import came out turned 180 degrees about up (Unity position =
(-X, Z, -Y)), which shows Unity negates raw **z** for this header. The rules
below follow the measured behaviour.

1. We export with axis_up='Y', axis_forward='-Z'. For that pair Blender's FBX
   writer declares UpAxis=+Y, FrontAxis=+Z, CoordAxis=+X
   (fbx_utils.RIGHT_HAND_AXES[('Y', '-Z')]), FBX's standard right-handed Y-up
   system, so the importer's axis conversion is only the handedness flip and
   object transforms stay identity.
2. With bake_space_transform=True Blender multiplies vertex data by its
   global matrix G: Blender (x, y, z) -> FBX (x, z, -y), and writes identity
   object transforms (empties get G @ M @ G^-1: the same rotation seen in FBX
   space).
3. Unity (measured): Unity (x, y, z) = (x_fbx, y_fbx, -z_fbx). Mirroring one
   axis is the right-handed -> left-handed change; Unity flips the triangle
   order with it, so faces keep facing outward.
4. So Blender data = campus coordinates unchanged (BLENDER_FROM_CAMPUS = 1):
   campus (X, Y, Z) -> FBX (X, Z, -Y) -> Unity (X, Z, Y) = campus_to_unity.
5. Scale: apply_scale_options='FBX_SCALE_ALL' with apply_unit_scale=True and
   unit scale 1 writes UnitScaleFactor = 100 (1 file unit = 1 m) and leaves the
   numbers in meters; Unity's file scale is UnitScaleFactor / 100 = 1.
6. Climb markers: Unity's play check uses transform.forward (+Z). By 2-4,
   Unity's local +Z is the empty's Blender local +Y (G maps +Y to -Z, step 3
   mirrors -Z to +Z). Blender rotation about Z by t turns +Y into
   (-sin t, cos t), which must equal the campus direction (dX, dY) toward the
   wall: t = atan2(-dX, dY) (climb_angle). (This is the same angle as before:
   the old x-mirror plus 180 degree turn and the new z-mirror agree on
   directions; only positions were wrong.)

check_fbx.py reads the raw FBX numbers back and verifies steps 1, 2, 4, 5, 6
with fbx_to_unity below. Changing the import settings means re-calibrating.
"""
import json
import math
from pathlib import Path

import numpy as np

# Campus -> Blender data (unchanged, see step 4).
BLENDER_FROM_CAMPUS = np.array([1.0, 1.0, 1.0])

FBX_SETTINGS = dict(
    use_selection=False,
    object_types={"MESH", "EMPTY"},
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    use_mesh_modifiers=False,
    mesh_smooth_type="OFF",
    use_mesh_edges=False,
    use_tspace=False,
    use_triangles=False,          # inputs are already triangles
    use_custom_props=False,
    colors_type="SRGB",           # see COLOR NOTE below
    prioritize_active_color=True,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="STRIP",
    embed_textures=False,
    batch_mode="OFF",
)
# COLOR NOTE: the color attribute is BYTE_COLOR with byte R = material index.
# Blender's FBX writer reads "color_srgb" in SRGB mode, which for a byte
# attribute is exactly byte / 255 (no conversion). LINEAR mode would read
# "color", which Blender converts sRGB -> linear, and the index would be lost.
# So SRGB here is the "write the raw bytes" setting.

# Header values the checker expects (step 1 and 5).
EXPECTED_HEADER = {"UpAxis": 1, "UpAxisSign": 1, "FrontAxis": 2, "FrontAxisSign": 1,
                   "CoordAxis": 0, "CoordAxisSign": 1, "UnitScaleFactor": 100.0}


def campus_to_unity(p):
    p = np.asarray(p, float)
    return np.stack([p[..., 0], p[..., 2], p[..., 1]], -1)


def unity_to_campus(p):
    p = np.asarray(p, float)
    return np.stack([p[..., 0], p[..., 2], p[..., 1]], -1)


def fbx_to_unity(p):
    """Raw FBX numbers -> Unity (step 3, measured 2026-11-20): z is negated."""
    p = np.asarray(p, float)
    return np.stack([p[..., 0], p[..., 1], -p[..., 2]], -1)


def climb_angle(dx, dy):
    """Blender Z rotation (radians) that makes Unity +Z point along campus (dx, dy)."""
    return math.atan2(-dx, dy)


def load_npz(path):
    with np.load(path, allow_pickle=False) as z:
        return {"verts": np.asarray(z["verts"], np.float64).reshape(-1, 3),
                "faces": np.asarray(z["faces"], np.int64).reshape(-1, 3),
                "face_mat": np.asarray(z["face_mat"], np.int64).reshape(-1),
                "uv0": np.asarray(z["uv0"], np.float32).reshape(-1, 3, 2)}


def load_merged(paths):
    """Load and join several VC mesh files (one object can come from several)."""
    parts = [load_npz(p) for p in paths]
    if len(parts) == 1:
        return parts[0]
    off, faces = 0, []
    for m in parts:
        faces.append(m["faces"] + off)
        off += len(m["verts"])
    return {"verts": np.vstack([m["verts"] for m in parts]),
            "faces": np.vstack(faces),
            "face_mat": np.concatenate([m["face_mat"] for m in parts]),
            "uv0": np.vstack([m["uv0"] for m in parts])}


def expected_object(mesh):
    """What the checker expects for one mesh object: counts, materials, Unity bounds."""
    used = np.unique(mesh["face_mat"]).astype(int).tolist()
    hist = np.bincount(mesh["face_mat"], minlength=256)
    vu = campus_to_unity(mesh["verts"][np.unique(mesh["faces"])]) if len(mesh["faces"]) else np.zeros((1, 3))
    return {"triangles": int(len(mesh["faces"])), "mat_indices": used,
            "faces_per_index": {str(i): int(hist[i]) for i in used},
            "unity_min": vu.min(0).round(3).tolist(), "unity_max": vu.max(0).round(3).tolist()}


def load_plan(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def material_names(material_ids_path):
    """index -> "mat_<id>" from config/material_ids.json."""
    with open(material_ids_path, "r", encoding="utf-8") as f:
        ids = json.load(f)["ids"]
    return {int(m["index"]): "mat_" + m["id"] for m in ids}


def blender_argv(argv):
    """Arguments after '--' (Blender passes its own arguments first)."""
    return argv[argv.index("--") + 1:] if "--" in argv else []


def write_json(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    with open(tmp, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    tmp.replace(path)
