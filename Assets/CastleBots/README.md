# Local castle survivors

In CastlePhysicsTest, press Esc (or open the existing VR menu) and choose **Spawn AI survivor**. **Fill to 10 players** maintains nine NPCs alongside the local human. **Clear NPCs / stop auto-fill** removes them. No NPCs spawn automatically on scene startup.

These are local, reactive game bots, not trained neural networks. They run through the current CastleSurvivor controller and instantiate its current avatar prefab. Avatar mesh, materials, palette and vault animation changes carry through. Bots disable their prefab cameras, audio listeners, XR origin and human input; their bodies retain survivor/block collision isolation. Bot arms use a simple walking swing and occasional social gesture, while the shared avatar still animates actual vault contacts.

The planner samples nearby live crate tops, checks headroom, support and jump trajectories, and discards tipped or fast-moving landings. It prefers higher, stable footing, avoids predicted falling blocks and the giant hand, and tries alternate routes when stuck. It walks, jumps and sends a second jump press only when the normal vault query finds a real ledge. Knocked-out bots wait and respawn on a clear surviving top surface. Rebuilding the castle repositions the existing bots.

## Personalities

Edit `Resources/CastleBots/0.asset` through `3.asset`, or create a Bot Personality asset in that folder. On scene startup the selector discovers all personality assets there, sorted by display name; additional personalities need no code changes. Presets are Balanced, Careful, Daredevil and Goofy. Knobs include goofiness, bravery, sociability, exploration, survival priority, decision interval and pace. Individual NPCs receive a small deterministic reaction/pace variation. The demo selector affects new spawns; existing bots retain their selected asset. A custom asset can also be assigned to a running brain's Personality field.

## Shared movement and new abilities

`CastleSurvivor.externallyDriven` selects an `ICastleSurvivorDriver`, whose world-space movement, look and jump intent go through the ordinary CharacterController motor. Human input is unchanged when this flag is false. The planner reads jump height, gravity, walk speed, size, headroom and vault reach from that motor rather than baking fixed player dimensions into routes.

A completely new mechanic still needs an AI adapter; bots cannot infer its meaning automatically. Implement `ICastleBotTraversalAbility` on a component on the survivor prefab. `CanReach` advertises reachable destinations; `TryTraverse` issues the ability's own commands and returns movement intent. Adapters are discovered at spawn. Refresh the ability list by respawning after adding a component. Do not move the bot or blocks by teleport as part of an ability adapter unless that is also the human mechanic.

## Population and networking boundary

`CastlePlaySession.Bots` exposes `SpawnBot`, `FillToTarget`, `ClearBots`, `CyclePersonality`, `targetPopulation` and `SetHumanPlayerCount`. The default target is 10, including humans and AI giant hands, and automatic fill is opt-in. Joining humans replace excess fillers; leaving humans replenish them while auto-fill is on. A hard cap of 20 NPCs guards accidental large counts. Failed spawns report the lack of a stable clear surface and retry slowly during auto-fill. See AI_HANDS.md for hand spawning, personalities and the shared hand-control adapters.

This implementation is local. It does not yet network-spawn NPCs or elect a Fusion authority. For multiplayer, run the population/brains on the authoritative host only, call SetHumanPlayerCount from actual connection events, and replicate the existing avatar frame/colour through the game's network avatar path. Do not run independent filler populations on every client.

## Reapply and checks

No navigation or ML package is required. Source lives in Assets/CastleBots; integration edits are in CastleSurvivor, CastlePlaySession, CastleWorldMenu and CandyPlayerAvatar. Use **Tools > Castle Bots > Install Profiles And Verify** in the saved CastlePhysicsTest scene to create profiles and run local integration checks. The check does not save or rebuild the authored scene file. Results and an actual Unity capture are written to ArtDirection/CastleBots. **Tools > Castle Bots > Export Module** exports a Unity package for this compatible project.

Navigation is deliberately local and reactive. Bots may choose poorly during a sudden collapse, and cannot plan arbitrary multistep routes through the entire castle. Their mistakes are physical failures rather than hidden teleports. Quest frame rate, real headset UI operation and network play require separate device testing; desktop/editor simulation does not establish those results.
