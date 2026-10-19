# Local AI giant hands

Open the demo menu and choose **Spawn AI giant hand** repeatedly. The next-hand personality button cycles editable assets, and **Clear AI hands** removes their actors and physical contact rigs. Manual hand spawning is uncapped. These are opt-in opponents; multiplayer authority and replication are owned by the session integration.

The factory instantiates `CastlePlaySession.handPrefab`, disables its cameras/listeners/XR origin/follow script, and supplies external input. It uses the same `FlyingHandController`, `CandyFlyingHandBridge`, physical prefab, mesh, size profile, material, solver settings and bounded drives as the human hand. Human input and hand tracking are not read by AI actors. Changing the shared assets affects their next spawn or contact-rig rebuild, just as for the human rig. Runtime force/friction profile adjustments retain the existing hand module's live-tuning behavior. Geometry/mass changes require a released rebuild; **Rebuild castle** restarts the AI hand rigs safely.

The brain selects exposed block faces at varied heights on its side of the castle. It reserves nearby attack areas so other hands choose different blocks, briefly settles an open palm, follows through with a bounded physical shove, and withdraws. It does not wait for a perfectly motionless hand. Push speed, push distance and settle time are editable on the personality, alongside attack and recovery duration. A successful push requires measured displacement. Sustained contact during a shove is not treated as an approach obstruction; failed approaches and unsuccessful pushes still time out and choose other targets.

AI hands steer away from each other and ignore each other's contact colliders. They still collide with blocks and survivors. Finger joints retain bounded anatomical drives; the wrist does not gain a new oversized collision shape. Blocks move through hand collider forces only: no artificial target impulse, attachment, object parenting or target pose write. This is effective palm pushing rather than a genuine pinch-and-pull planner.

AI contact rigs enable `JointDrivenFingers`: finger joints have angular limits, and the fingers follow the palm through their joints instead of each pulling toward an independent world-space position. Finger curls change gradually and preserve phalanx lengths. The local player's tracked fingers retain their existing drive mode.

## Personalities and future actions

`Resources/CastleHandBots` contains Playful, Gentle, Tactical and Relentless. Assets are discovered at scene startup, so additional profiles need no selector code change. Knobs include aggression, goofiness, preference for survivor-level targets, flight speed, attack duration and recovery time. The selector affects subsequent spawns; existing brains keep their selected profile. Shared physical force settings remain on the existing hand settings asset, independent of these behavioral knobs.

`FlyingHand.IFlyingHandInputSource` supplies world movement intent and yaw to the ordinary flight motor. `ICandyHandPoseSource` supplies a complete world wrist pose and unscaled wrist-relative skeleton to the ordinary contact bridge. The human path remains the default when `externallyDriven` is false. This keeps future changes to skinning, physics and input filtering in the shared implementation rather than copying them into AI.

For a future human hand action, add an `ICastleHandBotAbility` component to the hand prefab. Its `TryAct` method can provide flight and pose commands for a selected target. Abilities are discovered on spawn. A new action still needs this small adapter; the AI does not infer arbitrary mechanics by itself. Do not manipulate the target Rigidbody directly in the adapter if the human mechanic uses hand contact.

## Population and survivor reactions

AI hands count toward `targetPopulation`: one local human + one AI hand + eight NPC survivors = ten. Spawning a hand removes excess filler survivors; clearing it replenishes them if survivor auto-fill is on. `SetHumanPlayerCount` also trims excess hand participants when humans join. Survivor bots include AI hand positions in their threat scoring. Manual hand counts are uncapped; maximumHands is a legacy field. Higher counts require device performance testing.

All decisions and actors are local. Multiplayer requires one authoritative host running these brains and spawning/replicating the actors. Existing human count hooks provide population accounting, not a finished network transport. Device frame rate, real headset UI operation, long collapse sessions and arbitrary future prefab replacements still require their own testing. A replacement hand must retain or adapt the shared flight/contact interfaces.

## Reapply

Run **Tools > Deadly Jenga > Validate Flying Hand Tracking** for the current player and AI regression suite. It tests a real AI hand pushing a dynamic block, an immovable target, target removal, and a ten-hand, 90-second attack on the loaded castle in Game.unity. The castle test requires actual successful pushes and multiple displaced blocks, verifies independent targets and ignored AI-to-AI contact pairs, and measures finger reach. An attempted attack alone is not a passing result. Reports and rendered contact frames go to `Logs/Validation`; the test scenes and fixtures are not saved. Older export archives do not contain these changes.

The combined `CastleBots.unitypackage` and reapply archive include survivor AI, hand AI, their current integration sources, profiles and the current shared hand module. Import into this compatible project with its Fusion/XR/URP dependencies installed. Review integration-file overwrites when importing over later work; older art/hand archives remain snapshots of their earlier versions. Run **Tools > Castle Bots > Install Profiles And Verify** on the saved CastlePhysicsTest scene for the integration checks. Test fixtures exist only in Play Mode and the authored scene is not saved by verification.

## Contact regression verification (2026-10-17)

The editor suite passed 68 checks, including tracked-player camera/menu lifecycle and a loaded castle attacked by ten Playful AI hands for 90 simulated seconds. The sustained run recorded 37 attacks, 31 successful pushes, and 217 blocks displaced more than one metre. All ten hands attempted attacks. The isolated fixture measured 1.09 metres of block displacement, approximately 17 degrees maximum relative finger error, and 0.0025 metres maximum joint-anchor separation. These are editor observations, not performance or balance guarantees on Quest. Device comfort, sustained headset performance, and multiple real network peers remain unverified for this revision.
