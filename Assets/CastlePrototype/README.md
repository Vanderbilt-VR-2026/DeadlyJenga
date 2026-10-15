# Castle physics prototype

Open `Scenes/CastlePhysicsTest.unity`, select the Game tab, and enter Play Mode.
You spawn as a survivor on the highest surviving upright block, with desktop or XR controls
selected automatically from whether an XR display is running. The scene is independent of Lobby and Photon.
It is currently the default and sole enabled build scene. Lobby/Main remain in the
project with their separate Photon flow; they are disabled in the build scene list.

## Controls

### Play demo (default)

| Action | Desktop | Headset / Touch controllers |
| --- | --- | --- |
| Survivor move/look | WASD / mouse | Left stick / tracked head |
| Turn | Mouse | Right stick, 30-degree snap |
| Jump, then vault near a ledge | Press Space again | Press right A again |
| Switch survivor / flying hand | Tab, or Esc menu button | Right B, or world menu |
| Open / close menu | Esc | Left Y |
| Select world menu button | Mouse on desktop menu | Look at button, right trigger |
| Hand horizontal flight / yaw | WASD / mouse X | Left stick / right stick X |
| Hand rise / descend | E / Q | Right trigger / left trigger |

The menu also respawns the survivor on top or rebuilds the castle. Falling below the ocean
respawns the survivor automatically. Changing roles disables the other actor and its camera/colliders;
switching back to survivor respawns on the current highest block. This is local demo possession.

The survivor now uses Player Scale 0.5: 0.85m tall, a 0.12m capsule radius, and a 0.8m desktop eye height. Tracked headset movement is scaled by the same factor. The prefab's Player Scale field can change this later. It retains 4.6m/s movement and a 2.03m jump
against the default 2m-high blocks. Automatic stepping is only 0.025m at half size. A second airborne press
can mantle a vertical face within 1.05m if its top is no more than 1.25m above the feet.
It requires a clear landing capsule and lift/traverse path. Empty air never gives another jump.
The vault lasts 0.28s, follows the target block, and cancels if that block disappears or tips too far.
These values are editable on the survivor prefab for feel tuning.

`Prefabs/CastleSurvivor.prefab` owns movement and its first-person XR camera.
`Prefabs/CastleFlyingHand.prefab` owns a nested `Assets/HandDemo/Prefabs/FlyingHandPlayer.prefab`
and a sibling follow-camera rig. Swap `HandVisual` independently of `HandColliders` in the
nested hand prefab, or assign another complete `CastleHandActor` prefab on `Castle Play Session`.
The flying-hand runtime was copied from the separate hand demo; that source project is unchanged.
The host session owns cursor/menu suspension; the hand motor still owns its flight bindings.

OpenXR and Touch profiles are configured for Standalone and Android through
Tools → Castle Prototype → Configure Headset Support. Desktop play requires no headset.
For PC VR, start the headset's OpenXR runtime / Link connection before entering Play Mode;
a USB device or an input device alone is deliberately insufficient to select VR. Actual headset
rendering, controller hardware, comfort, and Quest performance still need hardware testing.

### Original physics laboratory

To use the original orbit/push laboratory, disable `Castle Play Session` in Edit Mode before
pressing Play. Its overview camera and original controls remain in the scene:

- Right mouse drag: orbit; middle drag: pan; wheel: zoom.
- Left click a block: small push; Shift + click: stronger impact.
- After clicking a block, onscreen buttons also provide a strong impact and removal.
- X while pointing at a block: remove that support.
- R / Reset castle: rebuild; F: frame the structure.
- Onscreen buttons switch between a single paired tower and the complete castle,
  or wake every block to expose settling issues.
- Counters show remaining blocks, awake bodies, smoothed editor frame time, and
  last build time. These are observations, not a Quest performance benchmark.

## Construction

All blocks use identical 6m length × 2m height × 2m width dimensions. The survivor is now 0.85m tall (half of its original 1.7m height).
The central keep has four crates on cardinal sides per layer, then four on diagonal sides
45 degrees away on the next layer. A/B repeats without accumulating rotation. Crate ends
bear on the previous layer; placement creates the eight-sided outline, windows, and exposed tops.
Outer towers use two parallel separated crates per layer and turn the next pair 90 degrees.
The default five-module perimeter plus 30-layer central keep contains 568 dynamic crates.
The foundation is fixed. Spacing and course counts remain prototype tuning values.

Select `Castle Layout (local prototype)` to change dimensions, course counts, module spacing,
and mass, then use Rebuild in Play Mode. At least five modules per side are needed around
the octagonal keep. Play Mode scene changes revert on exit.

## Block artwork and one-way player collision

`Prefabs/CastleBlock.prefab` has a Rigidbody and unit BoxCollider on its root. Its `Visual`
child contains artwork only. `Materials/Block Appearance.asset` references the shared
`Prefabs/Crate Visual.prefab` and an optional material override.

- Change the appearance asset's **Material Override** during Play Mode to update every crate.
  Edit that material's base map, normal map, tiling, or color to tune textures live.
- Change **Visual Prefab** to swap the model live without rebuilding or resetting physics.
  Author the model inside a unit cube; the root scales it to the configured crate dimensions.
- Leave Material Override empty for the section materials on the generator. Changing those
  shared materials also updates their existing renderers immediately.
- Colliders on replacement artwork are disabled, and artwork rigidbodies cannot collide.
  The root collider remains the physics shape regardless of visual changes.
- Appearance assets/material edits can persist after Play Mode; scene instance edits revert.

Survivors collide with independent kinematic copies of the crate boxes. These follow the
real crates but cannot transfer forces back into them. The giant hand and other crates collide
with the original dynamic bodies. Layers 8/9/10 are named Castle Survivor / Castle Block /
Castle Survivor Surface. Runtime layer exclusions and per-collider exclusions isolate them. The saved project
physics collision matrix is unchanged. Survivor ground/vault queries use the copies, so movement follows
falling/rotating crates. This is local prototype behavior and must be integrated with network
authority later. Removing/disabling a crate also removes/disables its survivor surface.

## Integration contract

`CastleLayout.Build(settings)` returns deterministic local positions, rotations,
section identifiers, and course numbers. It has no networking or scene dependency.
`CastleGenerator` is just the local Instantiate adapter and test preview.

For multiplayer, have the host call the layout builder from the existing scene
load flow and use `Runner.Spawn` for each placement, instead of invoking the local
generator. Transform each pose through the arena root. Use a registered network
block prefab and set its dimensions before spawn; all peers need matching collider
and visual dimensions. Prefer a unit-size network prefab variant rather than
rescaling the existing 5×3×15 block without accounting for its current scale.
Replicate dimensions explicitly if they can change between rounds. Track spawned
NetworkObjects and use Runner.Despawn for reset/removal. Keep authority and physics
configuration consistent with the existing Fusion physics runner.

Do not run both generators in the same arena. The local prototype has no Fusion
components and cannot be treated as a networked level without this adapter.
The survivor spawn, vault, and hand possession are local implementations. Network authority,
networked locomotion, damage, rounds, and replay remain separate gameplay integration tasks.
Reference-style traversal still needs player feedback before considering it a finished VR course.

## Validation

Tools → Castle Prototype → Validate Layout and Physics runs an isolated Unity
PhysicsScene in Play Mode, checks initial penetrations, simulates ten seconds of gravity,
and removes a corner's bottom pair to test loss of support. Results are saved in
`Validation.txt`. From Edit Mode it opens the test scene and enters Play Mode;
it leaves Play Mode running for manual inspection. It does not edit gameplay scenes
or global physics settings. Check that report after changes; passing the default layout does not
validate every possible Inspector configuration.

Tools → Castle Prototype → Open Test Scene opens the laboratory. The editor
builder creates the assets once on first import if the test scene is absent.

Tools → Castle Prototype → Validate Player and Vault checks real CharacterController collision,
jump height, vault landing/rejection, top spawn, and possession; see `TraversalValidation.txt`.
Validate Input Bindings (Play Mode) exercises keyboard, mouse, and synthetic XR devices in
the actual player loop; see `InputValidation.txt`. Both restore survivor mode afterward.
Run these in the default test scene without an active physical headset. They do not validate multiplayer.

Validate Confirmed Structure (Play Mode) checks the approved alternating layers, scale, player contact against a deliberately light 1kg dynamic crate, and live artwork swaps. See ReferenceValidation.txt.


