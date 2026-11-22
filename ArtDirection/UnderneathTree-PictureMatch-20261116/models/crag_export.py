"""Export Crystal Crag with its real stairwell and individually climbable steps."""
from pathlib import Path
import sys
import bpy
sys.path.insert(0, str(Path(__file__).resolve().parent))
from premiere_export import export_playset
from falcon_export import slab

SOLIDS = ("Entry step", "Spiral tread", "Landing", "Stair newel", "Ashlar", "Castle parapet",
          "Low battlement", "Outcrop footing", "Interlocking wall stone", "Fluted pillar",
          "Divination desk", "Four poster mattress", "Cast iron cauldron", "Bookcase upright")


def floor_hulls(obj, parent):
    # Each pair of radial edges bounds a convex slice outside the stair opening.
    vs = [obj.matrix_world @ v.co for v in obj.data.vertices]
    assert len(vs) == 256
    for i in range(64):
        a = 4*i
        b = 4*((i+1)%64)
        slab("Hull gallery", [(vs[j].x,vs[j].y) for j in [a,a+1,b+1,b]], vs[a].z, vs[a+2].z, parent)


def export_crag(out):
    return export_playset(out, "Crystal Crag.blend", "Toy Wizard Tower", "Crystal Crag",
                          ("alchemy", "divination", "archive", "terrace"), [-.13,5.2,10.2,15.2,15.65],
                          (0,6.3), 0, SOLIDS, "Studio backdrop", None, floor_hulls,
                          lamps=[(0,"Gem footing left",(-6.75,-4.22,2.53)),
                                 (0,"Gem footing right",(6.75,-4.22,2.53)),
                                 (1,"Gem pier left",(-7.45,-2.18,6.90)),
                                 (1,"Gem pier right",(7.45,-2.18,6.90)),
                                 (2,"Gem crown left",(-6.84,-2.85,11.78)),
                                 (2,"Gem crown right",(6.84,-2.85,11.78))])


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    export_crag(sys.argv[-1])
