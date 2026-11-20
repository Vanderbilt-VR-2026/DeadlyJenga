"""Shared helpers for the tile exporter and the FBX checker (numpy only, no bpy).

Both run inside Blender's own Python, which has numpy but not the pipeline's
other packages, so this file must not import shapely, pyproj and so on.

AXES (read this before changing anything here)
------------------------------------------------
Campus frame: X east, Y north, Z up, meters, right-handed (frame.py).
Unity wants:  x = X, y = Z, z = Y (frame.campus_to_unity), left-handed.

1. We export with axis_up='Y', axis_forward='-Z'. For that pair Blender's FBX
   writer declares the axis system UpAxis=+Y, FrontAxis=+Z, CoordAxis=+X
   (fbx_utils.RIGHT_HAND_AXES[('Y', '-Z')]). That is FBX's standard
   right-handed Y-up system (the one Maya and Unity's own FBX exporter
   use), so Unity has no axis conversion left to do; bakeAxisConversion has
   nothing to bake and object transforms stay identity. We pick this pair on
   purpose: whether or not an importer honors the declared front/coord signs,
   the result is the same.
2. With bake_space_transform=True Blender multiplies vertex data by its
   global matrix G: Blender (x, y, z) -> FBX (x, z, -y), and writes identity
   object transforms (empties get G @ M @ G^-1, i.e. the same rotation seen in
   FBX space).
3. Unity turns a right-handed Y-up FBX into its left-handed space by negating
   x: Unity (x, y, z) = (-x_fbx, y_fbx, z_fbx). (Unity's FBX Exporter package
   does the reverse, x -> -x, when it writes files.) Unity also flips the
   triangle order at the same time, so faces keep facing outward.
4. Steps 2 + 3 alone would give Unity (-X, Z, -Y): the map turned 180 degrees
   about the vertical axis (not mirrored, but north would point south).
   So before export we turn the data 180 degrees about Blender Z:
   Blender (x, y, z) = (-X, -Y, Z)   (BLENDER_FROM_CAMPUS below)
   FBX     (x, y, z) = (-X,  Z,  Y)
   Unity   (x, y, z) = ( X,  Z,  Y)   = campus_to_unity. East stays east,
   north becomes Unity +Z, no mirror (a rotation keeps handedness, and step 3
   is the one intended handedness change).
5. Scale: apply_scale_options='FBX_SCALE_ALL' with apply_unit_scale=True and
   unit scale 1 writes UnitScaleFactor = 100 (1 file unit = 100 cm = 1 m)
   and leaves the numbers in meters. Unity's useFileScale gives file scale
   UnitScaleFactor / 100 = 1, so 1 unit = 1 m. (If an importer ignored the
   unit, the numbers are still meters.)
6. Climb markers: Unity's play check uses the marker's transform.forward
   (+Z). A Blender rotation about Z by t becomes an FBX rotation about +Y by t
   (G is a rotation), whose +Z axis is (sin t, 0, cos t); Unity negates x:
   (-sin t, 0, cos t). We want Unity forward = (dX, 0, dY) (the campus
   direction toward the wall), so t = atan2(-dX, dY) (climb_angle below).

check_fbx.py reads the raw FBX numbers back and verifies steps 1, 2, 4, 5, 6.
"""
import json
import math
from pathlib import Path

import numpy as np

# Campus -> Blender data (180 degree turn about Z, see step 4).
BLENDER_FROM_CAMPUS = np.array([-1.0, -1.0, 1.0])

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
    """Raw FBX numbers -> Unity (step 3)."""
    p = np.asarray(p, float)
    return np.stack([-p[..., 0], p[..., 1], p[..., 2]], -1)


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
