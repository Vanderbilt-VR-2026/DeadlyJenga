# Candy contact hand module

## Intent
An articulated, shiny continuous hand targets the tracked wrist and finger joints. Physics determines its visible pose when a block obstructs it. Block manipulation uses collider contact and friction only: no grab helper, object attachment joint, object parenting, object teleport or gesture-triggered holding.

Ordinary survivor/avatar hands remain visual-only. The separate flying-hand actor owns this physical interaction. The existing castle layout, block rigidbodies and survivor collision isolation are not rewritten by the installer.

## Files and use
- `Assets/CandyPhysicsHands/CandyHandContactLab.unity`: isolated one-block static bench. WASD translates the wrist, Q/E lowers/raises it, Space closes the synthetic thumb/index pose. With XR input, the right tracked skeleton replaces simulation.
- `Candy Physical Hand.prefab`: baked smooth skin and sixteen render bones. The runtime backend creates a palm and fifteen rigid finger segments, connected only to other hand bodies. Rounded proximal segments and broad distal finger pads provide real contact patches. The rendered fingers remain rounded.
- `Candy Bench Hand.prefab`: a separate skin bake calibrated to the synthetic bench pose. The shared generator accepts a reference skeleton and shape parameters; the previous player mesh defaults and existing player meshes remain unchanged. Live tracked lab input switches to the normal hand mesh and calibrates its physical link lengths from the incoming skeleton.
- `Flying Hand Settings.asset` and `Bench Hand Settings.asset`: independent geometry, mass, drive force/torque ceilings, spring/damping, friction and transition settings.
- `CandyFlyingHandBridge`: integrates the module into CastleFlyingHand. Tracking is mapped relative to the flying actor; the actor remains the locomotion control frame.
- `CandyHandTransition`: toolkit-independent wrist/skeleton smoothing and state machine. Can be retained if a different physics backend is installed later.
- `CandyHandInstaller`: rebuild, install, test, capture and export through Tools/Candy Hands.

Hand geometry is calibrated from the starting skeleton and the size setting. Size is a multiplier of wrist-relative joint metres; do not scale the actor transform to resize a Rigidbody rig. Rebuild an inactive rig to apply a new geometry size. Force/torque limits and friction can be tuned without rebuilding geometry. A larger hand is not automatically a stronger hand.

The flying bridge applies a changed size after contacts become inactive. Mass and solver settings are applied when building the bodies; changing those requires an inactive rebuild. Physics uses the existing 0.02-second step. Extra solver iterations apply only to the hand bodies, not the project-wide solver or castle blocks. The test bench uses a 50 g, 22 x 30 x 22 mm block. Passing it does not prove extraction of the castle's 40 kg crates.

The lab's XR origin maps the table to a comfortable physical height and drives the camera from the HMD. Without a running XR display, it keeps the fixed desktop bench camera. With a running display but no valid hand/controller input, it releases and hides the hand rather than enabling a desktop hand at an unknown position.

## Tracking transitions
Require a stable input source before activation. Use adaptive target smoothing, speed/rotation limits and a recovery strength ramp. Short loss reduces pressure; sustained loss opens the target and reduces pressure to zero, then disables contacts. Do not preserve an invisible frozen grip. Reacquisition must not teleport interacting bodies. Controller fallback supplies a pointing skeleton; desktop keeps keyboard/mouse flight. A headset with neither valid skeleton nor controller has no active contact hand.

Tracking supplies positions, not measured squeeze force. Drive strength approximates the user's pressure against obstructing geometry. Sufficient squeeze and friction should lift/pull a block; insufficient pressure, excessive load or opening should permit slip/release. This must be tested with the target headset and actual castle masses, not inferred from a lightweight bench object.

In the castle hand role, the menu now reads the active actor's tracking source. Left thumb-to-middle pinch opens/closes the VR menu; right thumb-to-index pinch selects the button under gaze. These gesture events are only UI input. They do not create a grip or attach a block. Existing controller and desktop controls remain available.

## Toolkit assessment
HPTK (https://github.com/jorgejgnz/HPTK) is MIT, modular, input/scale independent, but the upstream repository is archived and lists Unity 2022 support. It has not been installed or run in this project. Adding it would require an explicit Unity 6 compatibility pass and XR Hands input adapter.

Ultraleap Physical Hands (https://docs.ultraleap.com/xr-and-tabletop/xr/unity/plugin/features/physical-hands.html) provides physical contact, but its standard grabbing uses a Grab Helper and its input provider differs from this project's XR Hands source. It has not been installed or hardware tested. Do not claim its default grabs are friction-only.

The current backend uses built-in Unity Rigidbody/ConfigurableJoint contact physics and the already-installed XR Hands package. No additional vendor or paid asset dependency was installed. This isolates the test and leaves a backend replacement path rather than coupling gameplay to a particular toolkit.

## Reapply or defer
Keep this folder and the exported unitypackage. In this compatible DeadlyJenga project, import the package, then use Tools/Candy Hands/Install And Test Contact Hand in Edit Mode with the current scene saved. The installer modifies the flying-hand prefab, creates the separate lab, runs synthetic checks and restores the previous scene. It does not rebuild the castle. To defer, remove/disable CandyFlyingHandBridge on CastleFlyingHand; the original pointing hand and its colliders remain in the prefab. Keep the assets for later reinstallation.

Export includes referenced existing project modules and is not a standalone replacement for Fusion, XR packages or URP. Review imported dependencies in another project. Local validation reports and actual Unity captures are beside this file. A passing synthetic test is not a Quest performance, real tracking, multiplayer or full tower extraction certification.

The final report covers actual pinch/lift/release, a zero-friction slip comparison, loss while holding, overlap-safe reactivation, independent geometry resizing, rendering, desktop/controller fallback and scene preservation. The exported archive also contains source snapshots and hashes so the implementation can be reapplied later.

## Finger separation and tracked-source correction (2026-10-14)
Both player and giant hand meshes now blend each digit into the palm independently, with a shorter palm and narrower fingers. Adjacent fingers no longer use a shared smooth union. The player visual prefab is rebaked by the installer without changing locomotion or XR settings. Flight camera follows CameraFollowTarget rather than the physical wrist, so hand rotation does not steer the view. Switching between pointing geometry and a tracked skeleton releases contacts, recalibrates finger lengths from the incoming skeleton and ramps back in. A new synthetic full-size articulated-hand test measures positional and angular tracking error in clear space. Existing builds must be rebuilt to include these fixes; real headset testing remains necessary.

Finger joint rotation drives use desired child rotation relative to desired parent rotation, with the initial relative bind rotation. This avoids parent rotation being counted twice as the wrist and articulated chain turn. The giant-scale regression check covers combined wrist rotation and finger bending, in addition to contact tests.
The isolated bench profile uses 0.01 Nm finger torque, 0.1 kg finger bodies and a block centred at Z=0.072 m. Its continuous-collision setup is included in calibration. This is a repeatable narrow benchmark, not a claim that arbitrary objects or castle masses are tuned. Palm blend stops near the finger roots; joints within each individual digit retain smooth blending.
