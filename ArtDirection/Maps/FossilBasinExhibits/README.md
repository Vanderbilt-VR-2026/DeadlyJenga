# Fossil Basin exhibit pieces (Blender build scripts)

These scripts build the Fossil Basin add-ons in Blender 5.3 from code:
- `build_exhibit_tower.py` builds the Fossil Exhibit Tower: a shark skeleton hung on level 1, a dinosaur egg nest on level 2, and two pterosaurs perched on the roof. A stair tower joins the floors.
- `build_brontosaurus.py` builds the Brontosaurus Stairway: the skeleton has a walkable ridge of flat-topped spines from the neck to the tail. Its feet stand on their own platforms, pylons support it, and a stair leads up to its back.
- `build_fossil_basin_expansion.py` places both pieces around the Faceoff arena, together with the links between them.
- `export_brontosaurus_unity.py` writes `Assets/_DeadlyJenga/World/Maps/Resources/FossilBasinBrontosaurus.json` and `FossilBasinBrontosaurusMeshes.bytes`, which FossilBasinMap.Brontosaurus.cs loads.

The build scripts load materials from `Fossil Basin Faceoff.blend`, which is expected in `../Concept Rebuild` relative to the output folder.

Run, for example:

    blender --background --python export_brontosaurus_unity.py -- <this folder> <Resources folder>
