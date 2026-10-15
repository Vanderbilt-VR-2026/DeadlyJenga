# Candy glove visual refinement

The shared `CandySmoothHandMesh` baker creates a softly squared palm, tapered wrist and rounded marshmallow cuff. Each digit still blends independently into the palm to avoid splayed-finger webbing. Surface normals come from the continuous field so glossy highlights do not expose the voxel grid. Geometry is baked in the editor, never regenerated per tracking frame.

Reapply using **Tools > Candy Hands > Rebuild Glove Visuals Only** in Edit Mode. This updates the saved player visual, giant physical-hand visual and calibrated bench visual without running the lab installer or changing the active scene. Both AI and human hands inherit their existing shared prefabs. Existing size and material settings remain editable.

The wrist/cuff is weighted to the palm bone; all existing tracked finger joints remain. This change does not modify tracking filters, collision shapes, mass, forces, friction, movement or hand scale. The cuff is decorative and has no separate collider.

`glove-preview.png` and `glove-palm.png` are Unity preview renders of the saved mesh, not hardware tracking captures. `GloveVisuals.txt` records the latest bake. Previously exported packages are older snapshots; this source and the baked assets are the current implementation.
