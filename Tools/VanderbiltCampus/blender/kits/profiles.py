"""The five style kits: one shared piece library (pieces.py), different materials and profiles.

Profiles are (out, up) points, normalized to 0..1: out is scaled by the piece's projection
(depth_m) and up by its height, so the same profile fits any measured size. Points go around
the section, starting at the wall foot (0, 0); the wall side (out = 0) closes the shape.
Numbers are typical proportions for each family, not measurements of one building.
"""
import copy

# cornice / string-course sections (out, up), wall side at out = 0
SECTIONS = {
    "plain_band": [(0, 0), (1, 0), (1, 1), (0, 1)],
    "drip_course": [(0, 0), (0.75, 0), (1, 0.25), (1, 0.55), (0.35, 1), (0, 1)],          # sloped top sheds water
    "gothic_cornice": [(0, 0), (0.35, 0), (0.45, 0.3), (1, 0.55), (1, 0.8), (0.25, 1), (0, 1)],
    "classical_cornice": [(0, 0), (0.2, 0), (0.2, 0.25), (0.45, 0.4), (0.45, 0.6), (1, 0.72), (1, 1), (0, 1)],
    "coping": [(0, 0), (1, 0), (1, 0.6), (0.6, 1), (0, 1)],
    "flush_cap": [(0, 0), (1, 0), (1, 1), (0, 1)],
}

BASE = {
    "materials": {"wall": "brick_red", "trim": "limestone", "base": "stone_rough", "glass": "glass",
                  "frame": "window_frame", "door": "door_wood", "roof": "roof_slate", "steps": "limestone",
                  "metal": "metal_trim", "chimney": "brick_red", "cap": "limestone"},
    "window": {"arch": "none", "reveal": 0.22, "frame_w": 0.08, "frame_relief": 0.06, "sill_h": 0.10,
               "sill_proj": 0.07, "sill_ext": 0.08, "hood": "lintel", "hood_h": 0.25, "hood_relief": 0.03,
               "hood_ext": 0.10, "mullion_w": 0.08, "mullion_relief": 0.05, "mullion_min_width": 1.3,
               "transom_min_height": 2.4, "segments": 8},
    "door": {"arch": "none", "reveal": 0.30, "frame_w": 0.12, "frame_relief": 0.08, "transom_h": 0.6,
             "hood": "lintel", "hood_h": 0.30, "hood_relief": 0.05, "hood_ext": 0.15},
    "arch": {"band_w": 0.35, "band_relief": 0.10, "reveal": 0.60},
    "string_course": {"section": "drip_course", "height": 0.25, "depth": 0.12},
    "cornice": {"section": "gothic_cornice", "height": 0.70, "depth": 0.35},
    "column": {"sides": 8, "base_h": 0.10, "base_over": 0.15, "cap_h": 0.10, "cap_over": 0.18},
    "pilaster": {"base_h": 0.08, "base_over": 0.05, "cap_h": 0.08, "cap_over": 0.06},
    "buttress": {"stages": 2, "weathering": 0.35},
    "dormer": {"pitch_deg": 50.0, "cheek": 0.15, "window_frac": 0.6},
    "chimney": {"cap_h": 0.25, "cap_over": 0.06},
    "balustrade": {"rail_h": 0.15, "plinth_h": 0.15, "baluster_w": 0.12, "spacing": 0.30},
    "steps": {"riser": 0.165, "cheek_w": 0.60, "cheek_above": 0.85},
    "storey_h": 3.8,
    "bay_w": 3.4,
}


def _kit(name, changes):
    k = copy.deepcopy(BASE)
    k["name"] = name
    for key, val in changes.items():
        if isinstance(val, dict) and isinstance(k.get(key), dict):
            k[key].update(val)
        else:
            k[key] = val
    return k


KITS = {
    # brick with limestone trim, pointed and round arches, hood moulds, drip courses (Kirkland)
    "victorian_gothic": _kit("victorian_gothic", {
        "window": {"arch": "pointed", "hood": "label", "hood_h": 0.15, "hood_relief": 0.08, "reveal": 0.25},
        "door": {"arch": "pointed", "hood": "label", "hood_relief": 0.10, "reveal": 0.45},
        "cornice": {"section": "gothic_cornice", "height": 0.70, "depth": 0.35},
        "buttress": {"stages": 2, "weathering": 0.35},
        "steps": {"cheek_w": 0.9, "cheek_above": 0.9},
        "storey_h": 4.6, "bay_w": 3.8,
    }),
    # rough stone walls, limestone tracery frames, pointed arches, buttresses, battlements
    "collegiate_gothic": _kit("collegiate_gothic", {
        "materials": {"wall": "stone_rough", "trim": "limestone", "chimney": "stone_rough", "roof": "roof_slate"},
        "window": {"arch": "pointed", "hood": "label", "hood_h": 0.15, "hood_relief": 0.09, "reveal": 0.30,
                   "frame_w": 0.12, "mullion_w": 0.12, "mullion_relief": 0.08, "mullion_min_width": 1.0},
        "door": {"arch": "pointed", "hood": "label", "reveal": 0.50},
        "buttress": {"stages": 3, "weathering": 0.40},
        "storey_h": 4.0, "bay_w": 3.2,
    }),
    # red brick, limestone lintels and keystones, classical cornice, columns, balustrades
    "georgian": _kit("georgian", {
        "window": {"arch": "none", "hood": "lintel", "hood_h": 0.30, "hood_relief": 0.04, "frame_w": 0.10,
                   "mullion_min_width": 9.0},
        "door": {"arch": "round", "hood": "lintel", "reveal": 0.35},
        "cornice": {"section": "classical_cornice", "height": 0.90, "depth": 0.55},
        "string_course": {"section": "plain_band", "height": 0.30, "depth": 0.06},
        "column": {"sides": 16, "base_h": 0.12, "cap_h": 0.14},
        "materials": {"roof": "roof_slate"},
        "storey_h": 4.2, "bay_w": 3.0,
    }),
    # 20th-century brick: flat openings, concrete or limestone lintels and copings, flat roofs
    "brick_institutional": _kit("brick_institutional", {
        "materials": {"trim": "concrete", "cap": "concrete", "roof": "roof_flat", "door": "door_metal",
                      "base": "concrete", "steps": "concrete"},
        "window": {"arch": "none", "hood": "lintel", "hood_h": 0.20, "hood_relief": 0.02, "reveal": 0.18,
                   "frame_w": 0.06, "sill_proj": 0.05, "mullion_min_width": 1.6},
        "door": {"arch": "none", "hood": "none", "reveal": 0.20},
        "cornice": {"section": "coping", "height": 0.40, "depth": 0.08},
        "string_course": {"section": "plain_band", "height": 0.20, "depth": 0.03},
        "storey_h": 3.6, "bay_w": 3.0,
    }),
    # concrete / metal panel and glass: shallow reveals, metal frames, flush caps
    "modern": _kit("modern", {
        "materials": {"wall": "concrete", "trim": "metal_panel", "cap": "metal_panel", "frame": "window_frame",
                      "door": "door_metal", "roof": "roof_flat", "base": "concrete", "steps": "concrete",
                      "chimney": "metal_panel"},
        "window": {"arch": "none", "hood": "none", "reveal": 0.12, "frame_w": 0.05, "frame_relief": 0.03,
                   "sill_h": 0.05, "sill_proj": 0.03, "mullion_w": 0.06, "mullion_min_width": 1.5},
        "door": {"arch": "none", "hood": "none", "reveal": 0.15},
        "cornice": {"section": "flush_cap", "height": 0.30, "depth": 0.05},
        "string_course": {"section": "flush_cap", "height": 0.15, "depth": 0.02},
        "column": {"sides": 12, "base_h": 0.0, "cap_h": 0.0},
        "storey_h": 3.9, "bay_w": 1.5,
    }),
}


def get_kit(name):
    if name not in KITS:
        raise KeyError(f"unknown kit {name}; kits: {sorted(KITS)}")
    return KITS[name]
