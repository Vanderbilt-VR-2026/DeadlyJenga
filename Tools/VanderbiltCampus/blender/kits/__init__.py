"""Style kits for Stage 4: one parametric piece library (pieces.py) shared by five kits
(profiles.py). Pure NumPy + shapely, so it runs in the pipeline venv; the assembler hands
the result to headless Blender only to write the authored .blend.

    import sys; sys.path.insert(0, "Tools/VanderbiltCampus/blender")
    from kits import get_kit, pieces, geom
    kit = get_kit("victorian_gothic")
    m = geom.Merger("3+", where="F01")
    p = pieces.window(kit, m, (1.0, 4.0, 2.2, 7.0), arch="pointed")
    mesh = p.mesh.finish()      # VC mesh dict; m.entries = ledger entries for merged parts
"""
from . import geom, pieces, profiles  # noqa: F401
from .profiles import KITS, get_kit  # noqa: F401
