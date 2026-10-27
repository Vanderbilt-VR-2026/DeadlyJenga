# Candy players: reusable visual and tracking module



## Tower player VR controls



Choose Standing or Seated from the lobby Controls button or the in-game VR controls page. The mode is remembered locally. Standing uses left-stick movement relative to horizontal head direction, physical turning, and a right-hand index pinch to jump; hold the gesture to climb and release to drop. A fist can replace pinch through the gesture button or `CastleSurvivor.jumpGesture`. Thresholds are real hand metres / normalized finger curl, independent of avatar scale. Releasing is required after a mode change, menu, focus loss or tracking loss before jumping again. Left-hand gestures never override stick movement.



Seated uses left-stick movement relative to the rig, continuous right-stick yaw at 60 degrees/second, and right A to jump / hold to climb. Edit the serialized `seatedJump` Input Action binding to change the button. Looking up/down remains headset-tracked. The mode calibrates seated eye height from a valid pose; the controls page also offers manual recalibration. Turning pivots around the eyes, preserving tracked local pose. Mode changes and joystick turning do not interrupt active climbs/vaults.



Local VR walking defaults to 2.5 m/s with a remapped 0.18 stick deadzone. VR vaults use 0.85 seconds with a short preparation phase, a clear vertical lift and a collision-swept traverse. Climbing accelerates at 10 m/s². These fields are editable on CastleSurvivor. Desktop and externally driven bots retain their previous movement speeds and vault duration. Traversal never adds camera bob, roll, forced look direction or landing shake.



Design references: [Meta locomotion preferences](https://developers.meta.com/vr/design/locomotion-user-preferences/) supports distinct physical/artificial turning, adjustable smooth-turn speed, direction mappings, real-time mode switches and remembered preferences. [Meta best practices](https://developers.meta.com/vr/design/locomotion-best-practices/) recommends limiting acceleration and controlling camera elevation. The numerical settings here are game-specific tuning choices, not platform standards or a guarantee of comfort.



Run `DeadlyJenga.CastlePrototype.Editor.SurvivorControlValidation.RunBatch` explicitly in Unity batch mode. It opens Game without saving it, checks controls with synthetic poses, tests collision-based vault/climb fixtures and the external motor path, and writes `Logs/Validation/SurvivorControls.txt`. Quest hybrid hand/controller availability, gesture reliability, seated calibration/recentering, stereo appearance and comfort require hardware playtesting.



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



## Avatar attachments



The lobby wardrobe pod is behind the initial spawn. Walk into the glowing ring to open it. Desktop shows the existing player in third person; VR keeps the tracked camera and shows that third-person image on the wardrobe screen. Drag the desktop preview or use the arrow keys to orbit smoothly; scroll or +/- zooms. VR uses hold-to-orbit/zoom buttons below the mirror. The existing avatar stands with relaxed arms and waves briefly every six seconds through optional IAvatarShowcase. Done (or F on desktop) restores locomotion. Step outside the ring before entering again.



All options are attachments to the existing 44-bone rig, including the skeleton face mask and broccoli crown. They never replace the avatar, its head mesh, hands, movement or collision. Head attachments follow the head bone; the cape follows the torso. They use the body's shared material and color. The wardrobe permits combinations for testing, including overlapping hats. Each feature's mesh is cached and shared across avatars. No new body colliders, rigidbodies or materials are created per accessory.



Presentation reads Core's IAvatarAppearance. ApplyAppearance(new AvatarAppearance { accessories = AvatarAccessory.Glasses | AvatarAccessory.Cape }) works on either a player or a spawned bot's CandyPlayerAvatar. Local human choices are saved under CandyAvatar.Accessories.v1 and loaded on the next lobby/game avatar. Bot and remote choices do not overwrite that preference. Normal bots begin with their authored appearance, rather than the local human's wardrobe. Corpses retain attachments. First-person cameras hide head/cape attachments while retaining their shadows; other cameras see them normally.



The survivor snapshot carries a validated appearance extension. Legacy states without an extension have no attachments. The Fusion HandPoseInput / NetworkHands path carries the same accessory bits. Lobby compatibility now includes the new appearance/enemy/plasma formats; all peers must use the updated project.



## Blaster testing



In Demo, open Esc > Play > Blaster testing, then Draw blaster. V draws/holsters and left click fires on desktop; Space still jumps. Controller A/X on the chosen dominant hand draws/holsters, its trigger fires, and the other hand's trigger jumps while testing is enabled, even when holstered. The UI can swap the dominant hand. Hand tracking keeps its existing jump gesture; controller firing requires tracked controller poses. Opening a menu, traversal, death and disabling testing prevent firing. Traversal holsters the gun.



CandyBlaster exposes Core's IBlasterControls; GameSession owns authoritative shots and impacts through IBlasterRuntime. The weapon has three rings and shared body/ring/vent meshes. It does not add hand or weapon collision. Desktop aims the offset muzzle toward the reticle; VR aims along the tracked dominant hand. Plasma moves at 14 metres/second, has a 0.12 metre sweep radius and expires after five seconds. Impact radius is 2.5 metres, with 35 maximum damage and distance falloff. Solid cover blocks splash. Shots are rate limited to one per 0.45 seconds. Self splash is possible. Host UI enables this prototype only in Demo; it does not change competitive mode weapon rules. Disabling testing or losing authority clears live shots.



Tools > Deadly Jenga > Validation > Check Avatar Attachments and Panther Enemies runs the targeted lobby/travel, enemy and blaster checks and an independent Photon lobby client check. Reports and real mesh/UI renders go to Logs/Validation. These checks do not certify headset comfort, controller alignment or Quest performance.





## Shared equipment

CandyEquipment supplies weapon grip poses to CandyPlayerAvatar.ReadFrame before the

body renders or writes its network pose. CandyWeaponVisual uses those same wrists

for local and remote models. Models contain no colliders. The blaster's grip is

offset inside its model so the palm wraps the handle rather than the receiver.



Equipment is one slot: speed, jump, alien blaster, murderer sword, sheriff revolver,

dual rifle or giant boxing glove. The session owns that slot; the character reads

it and applies the current movement power. Replacing a power removes its effect.



Desktop: V draws/stows, left click attacks, R changes rifle mode. Near an occupied

pickup, E confirms a swap and N keeps the current item.

Controllers: dominant trigger draws a holstered weapon, fires when drawn, and

stows when the hand is near the thigh/back holster. The support-hand trigger

changes rifle mode while drawn; with other weapons it remains the jump trigger.

Hold the support grip to aim the rifle using the line between the controllers.

There is no two-handed scaling. Drawing a rifle blocks jump, vault and climb at

the motor entry points as well as through the session's rules. Stowing restores

the mode's normal permissions. Controller-free hand tracking does not invent a

gun gesture; use the existing menu controls.



The blaster remains on the dominant thigh when stowed. The rifle is slung on the

back. Concealed Murder Mystery role weapons remain hidden until drawn so the

host's local bot visuals cannot reveal secret roles.





Powered armor is an equipment powerup; the wardrobe armor option remains cosmetic. Its runtime armor overlay does not overwrite saved appearance. Activation now takes off automatically and holds altitude when no altitude command is given. On desktop, V activates/stows, WASD steers, Space climbs, Ctrl descends, and left mouse fires palm plasma. In mixed tracking, the optically tracked hand pinches to fire through its palm; holding the controller trigger away from the thigh and pointing commands flight velocity/altitude, releasing it hovers, and triggering near the thigh stows to land. Two-controller fallback uses the opposite trigger for plasma. Selecting the controller side updates the replicated jet placement. While the suit is active, jump/climb gesture recognition yields to flight and plasma inputs. The existing camera and capsule collision motor remain in charge of movement.



The revolver keeps the legacy SheriffHammer asset/network ID. Its actual model is a blue gunmetal western six-shooter; dominant trigger fires, other trigger changes its shotgun/pistol mode while drawn. When holstered, the other trigger remains available for jumping. Desktop hold-right-mouse aligns the gun with the camera target; bullets still originate at the actual muzzle and respect cover. Presentation owns zoom and reticles.
