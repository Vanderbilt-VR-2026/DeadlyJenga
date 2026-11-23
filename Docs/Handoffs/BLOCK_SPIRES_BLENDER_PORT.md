# Block Spires Blender port

The normal map entry remains Block Spires (`block-spires`). The map uses Game.unity, the existing survivor respawn and island/ocean rules, and independent magnetic supports. The candidate is on `codex/block-spires` targeting `Evan_FINAL_Sprint`. The map-specific edits build on the owned revision `cf0fe9f6b6ca28c0d1df85506e47c24fd1731685`. The published sprint branch was `58f487891d63cdc5f7606559c92188f127c8a412` when checked. No combined-branch acceptance is claimed.

## Art source

The source is `Blender-Map-Art-Pass/block-spires/Studio Release Art/Block Spires - Studio Release.blend` in Evan's Workboard, saved with Blender 5.3.0. SHA-256 `9351de3701f7b11bbc35a2fced36bb2c92bf949fa5ba7a1d8bbfe58787fba5cf`. The export takes the 710 structural cube objects and the sandbank. Staged scale figures, flying cubes, and cinematic offshore scenery remain part of the Blender artwork. The Unity assets in this patch carry the structure and playable coast without the Blender file as a runtime dependency.

The authored 37 columns include 18 detached needles and the north-face 1, 5, 12, 19 and 26 m route. The crown reaches 27 m. Coordinates and color choices were checked against the Blender export, including the authored crown palette. The mesh collider follows the 1,840-triangle sandbank and the Unity sand surface samples that collider. The runtime uses the project's tropical sky, ocean and shore shaders. The molded near cube is a shared 2,350-triangle reduction of the authored shell and rivets. The crate renderer uses the existing procedural geometry farther from the camera. Production candy materials approximate the Blender glaze.

## Physics and evidence

Each separate cube owns its visual, box collider and breakaway support. Removing the second cube on the eastern needle at Unity x 8.3, z 0.5 releases the cubes above it. Nearby columns retain independent support. The sandbank preserves the existing lethal-ground tag. The pre-export staging check found zero overlapping columns and found every footing within 0.011 m of its authored support surface.

The focused desktop fixture is `DeadlyJenga.DevTools.Editor.BlockSpiresChecks.Run`. It checks the exact imported triangle counts, all 37 footings, the 710 supported climbable cubes, the north ascent, local breakaway and adjacent supports, session rebuild, lethal sand and ocean respawn. It writes its report and PNGs under Logs/Validation and mirrors assertions to the editor log. The current request, candidate SHA and results are tracked in PR #197. Multiplayer and Quest coverage remain open.
