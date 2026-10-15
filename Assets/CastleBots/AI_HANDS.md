# Local AI giant hands

Open the demo menu and choose **Spawn AI giant hand** repeatedly. The next-hand personality button cycles editable assets, and **Clear AI hands** removes their actors and physical contact rigs. The default limit is nine AI hands, sharing the ten-participant budget with the local human and any survivor NPCs. These are opt-in local opponents, not network-spawned players.

The factory instantiates `CastlePlaySession.handPrefab`, disables its cameras/listeners/XR origin/follow script, and supplies external input. It uses the same `FlyingHandController`, `CandyFlyingHandBridge`, physical prefab, mesh, size profile, material, solver settings and bounded drives as the human hand. Human input and hand tracking are not read by AI actors. Changing the shared assets affects their next spawn or contact-rig rebuild, just as for the human rig. Runtime force/friction profile adjustments retain the existing hand module's live-tuning behavior. Geometry/mass changes require a released rebuild; **Rebuild castle** restarts the AI hand rigs safely.

The first brain selects exposed crates near survivor height, approaches, visibly winds up, drives its fingers into a swipe/jab and backs off. Crates move through collider forces only; there is no attachment, parenting, object teleport or artificial block impulse. Finger pose generation preserves phalanx lengths. These actions are physical swipes, not a completed strategic pinch-and-pull planner.

## Personalities and future actions

`Resources/CastleHandBots` contains Playful, Gentle, Tactical and Relentless. Assets are discovered at scene startup, so additional profiles need no selector code change. Knobs include aggression, goofiness, preference for survivor-level targets, flight speed, attack duration and recovery time. The selector affects subsequent spawns; existing brains keep their selected profile. Shared physical force settings remain on the existing hand settings asset, independent of these behavioral knobs.

`FlyingHand.IFlyingHandInputSource` supplies world movement intent and yaw to the ordinary flight motor. `ICandyHandPoseSource` supplies a complete world wrist pose and unscaled wrist-relative skeleton to the ordinary contact bridge. The human path remains the default when `externallyDriven` is false. This keeps future changes to skinning, physics and input filtering in the shared implementation rather than copying them into AI.

For a future human hand action, add an `ICastleHandBotAbility` component to the hand prefab. Its `TryAct` method can provide flight and pose commands for a selected target. Abilities are discovered on spawn. A new action still needs this small adapter; the AI does not infer arbitrary mechanics by itself. Do not manipulate the target Rigidbody directly in the adapter if the human mechanic uses hand contact.

## Population and survivor reactions

AI hands count toward `targetPopulation`: one local human + one AI hand + eight NPC survivors = ten. Spawning a hand removes excess filler survivors; clearing it replenishes them if survivor auto-fill is on. `SetHumanPlayerCount` also trims excess hand participants when humans join. Survivor bots include AI hand positions in their threat scoring. Hand counts default to nine; maximumHands can be configured up to twenty, still subject to the total population budget. Higher counts require device performance testing.

All decisions and actors are local. Multiplayer requires one authoritative host running these brains and spawning/replicating the actors. Existing human count hooks provide population accounting, not a finished network transport. Device frame rate, real headset UI operation, long collapse sessions and arbitrary future prefab replacements still require their own testing. A replacement hand must retain or adapt the shared flight/contact interfaces.

## Reapply

The combined `CastleBots.unitypackage` and reapply archive include survivor AI, hand AI, their current integration sources, profiles and the current shared hand module. Import into this compatible project with its Fusion/XR/URP dependencies installed. Review integration-file overwrites when importing over later work; older art/hand archives remain snapshots of their earlier versions. Run **Tools > Castle Bots > Install Profiles And Verify** on the saved CastlePhysicsTest scene for the integration checks. Test fixtures exist only in Play Mode and the authored scene is not saved by verification.
