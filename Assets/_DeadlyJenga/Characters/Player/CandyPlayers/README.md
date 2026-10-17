# Candy players: reusable visual and tracking module

Unity 6000.3.23f1 / URP 17.3.0. Runtime dependencies: the project's Fusion integration, Input System 1.20.0, XR Core Utils, OpenXR 1.16.1 and XR Hands 1.7.3. Install those through the project package manifest; a Unity asset package does not install Package Manager dependencies.

The Resources/CandyPlayerVisual prefab contains baked shared meshes and 44 pose bones. The body uses smoothly skinned limb tubes and a closed torso. Each hand is baked from a smooth union of its palm and finger shapes, then skinned to the existing palm and 15 finger-segment bones. No voxel meshing runs during normal play. All 20 tracked finger nodes still determine the segments, and the same visible hands are used for first-person and outside views. The owner's head casts shadows but is hidden from its own camera. Eyes are placed ahead of the body and head's center to avoid viewing an open neck when looking down.

CandyTrackingSource prefers XR Hands wrist/joint data and falls back to controller poses. Desktop uses an animated default hand pose. CandyPlayerAvatar supplies the shared local pose, chooses a bright palette color on spawn, and briefly locks palms onto vault contacts before returning to tracking. NetworkHands and HandPoseInput carry the head, wrists, finger skeletons and color for the existing Fusion integration. Editor validation checks simulated controller data; actual headset hand tracking and two-peer networking remain hardware tests.

## Apply or defer

- Tools → Candy Players → Build And Install creates missing visual assets, installs the avatar on CastleSurvivor, and enables the OpenXR hand-tracking feature for Android and Standalone. It retains the approved jump/vault settings.
- Rebuild Smooth Visual Meshes explicitly regenerates the baked model while preserving mesh asset GUIDs and the shared style material. Normal play never regenerates these meshes.
- Export Reusable Player Package writes ArtDirection/CandyPlayers/CandyPlayers.unitypackage.
- Install And Verify runs the local tracking, palm-contact, jump/vault, reference and input checks and captures screenshots.

For art-only integration later, assign the CandyPlayerVisual prefab to CandyPlayerAvatar on the desired actor, bind its camera/tracking origin, and retain the actor's existing physics. To defer the visuals, disable CandyPlayerAvatar or remove its rendered child; retain these assets and the handoff. In the legacy network rig, restore its original controller renderer visibility if disabling the candy replacement.

Edit Candy Player Style.asset to change the palette, controller offsets, shared material, smoothness or vault ledge-lock preference. Swap the visual prefab for a later authored model while keeping the same tracking/pose interface. Meshes and materials are shared across instances; there are no new body, arm, hand or finger colliders or Rigidbody components.

Validation and real Unity screenshots are under ArtDirection/CandyPlayers/Results. Full joint tracking, controller position, moving-ledge contact, vault release, no midair double jump, and normal role switching are covered by the automated checks. Hardware comfort and target-headset frame rates are not certified by those checks.
