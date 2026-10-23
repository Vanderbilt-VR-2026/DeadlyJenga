# Beacon scaffold build-up prototype

Choose **Beacon Scaffold Ascent** and **Demo** on the lobby map/mode terminal. In the match open **Play → Deliver next scaffold tier**. The VR demo menu has the same action below Natural disasters.

Each advance sends two complete magnetic building prefabs down to the next tier in four seconds. All scaffold grates are player-solid during delivery and the following 20-second climb window. A countdown appears during that window. They then become pass-through beacons again. Delivered buildings remain solid and their magnetic connections remain breakable. Advance is disabled during delivery/countdown and after the last tier. Rebuild resets the sequence.

## Authoring

- `Scaffold.prefab`: fixed support collider on Block; separately toggled Survivor-only collider. `gridOrigin` is the minimum corner in grid cells, not the center. `dimensions` counts cells; scale stays one. Rotation snaps to quarter turns. Default cell width is 2 m.
- `IScaffold.SetPlayerSolid(bool)`: the state command. Solid also enables normal climb/vault queries. Beacon never removes block support. Presentation only changes the visible mesh/material.
- `BuildUp_0/1/2.prefab`: open watchtower, arch gallery and stair court. Each uses the shared magnetic cube prefab and castle candy appearance. Keep module block ordering stable for networking.
- Module `socket` is its minimum corner in grid cells relative to the map origin. Footprints are 4 × 4 cells. Buildings are authored as prefab children, not generated anonymous blocks at delivery time.
- `Mode_buildup.prefab`: cell size, tier count, tier height, climb time and delivery time are configurable. Prototype tiers are 16 m apart, with four climb pillars and rest collars. Changing cell size alone does not rescale authored module cubes; keep 2 m until corresponding module variants are authored.
- `Tools → Deadly Jenga → Maps → Build Scaffold Demo` explicitly recreates only these new assets. It never regenerates Lobby or Game. Do not use it after manually editing these prefabs unless you intend to replace those edits.

The host owns delivery/phase changes. The existing world snapshot path sends all preallocated module block poses plus stage, phase timing and broken magnet states. Replicas do not simulate delivery or magnetic damage. Restoring authority resumes the phase. The editor regression passes 42 checks, including two independent Photon runners exchanging actual production snapshot packets in waiting, delivery and climb-window phases. This tests transport and map state, not two full headset players. Separate-machine/headset comfort and performance still need testing.

Presentation uses cached grate meshes with an oil-film shader and scrolling transparent beacon light. Removing Presentation leaves the rectangular gray-box scaffold and all gameplay working.

Run **Tools → Deadly Jenga → Validation → Check Scaffold Build Up** to repeat the integrated checks. The report is written to Logs/Validation/ScaffoldBuildUp.txt and Unity returns to the scene it started from in Edit Mode.

