# Deadly Jenga — rules for AI agents

Multiplayer VR survival game for Meta Quest 3.
Unity 6.3 LTS (6000.3.23f1), URP, XR Interaction Toolkit + OpenXR only
(no Meta XR SDK / Oculus packages), Photon Fusion networking.
Start scene: Assets/_DeadlyJenga/GameSession/Session/Scenes/Lobby.unity

## Boxes
All team code lives in Assets/_DeadlyJenga/. It is split into boxes.
Each box has its own assembly definition (DeadlyJenga.<Box>).
A box may only use boxes at a LOWER level. Never the same level or higher.

| Level | Box          | Lanes (subfolders)          | May use                 |
|-------|--------------|-----------------------------|-------------------------|
| 0     | Core         | -                           | nothing                 |
| 1     | World        | Blocks, Maps                | Core                    |
| 2     | Characters   | Player, Hand, Bots          | Core                    |
| 3     | Presentation | Audio, Visuals              | Core                    |
| 4     | GameSession  | Session, Modes              | all boxes               |
| side  | DevTools     | -                           | all boxes; nothing may use DevTools |

- World: blocks, physics, pull rules, map layouts, island/ocean/sky objects.
- Characters: player movement/climbing, avatar, flying hand, bots.
- Presentation: sounds, HUD, materials, textures, shaders, skybox, block art.
  The game must still play (gray-box) if Presentation is removed.
- GameSession: lobby, Photon, spawning, round flow, match settings (Session);
  game modes such as classic, teams, disasters (Modes).
- Each box has its own AGENTS.md. Read it before working in that box.

## Lanes
Lanes are subfolders inside a box. They divide work, not code.
- Edit only files in the lane named in your task.
- You may read and call code from other lanes in the same box, but do not
  edit their files unless the task says so.

## Core: the shared vocabulary
Boxes talk to each other ONLY through Core. Never name another box's class.
Ask what something CAN DO, not what it IS:
`hit.GetComponentInParent<IPushable>()?.Push(force, gameObject)`

Namespace: DeadlyJenga.Core. Folder: Assets/_DeadlyJenga/Core/.

Abilities (Core/Abilities) - any object may implement any of these:
- IPushable      Push(force, source). force = velocity change in m/s.
- IDamageable    Health, MaxHealth, Damage(amount, source), Eliminate(source).
- IAffectable    Apply(Effect), Remove(EffectType), Has(EffectType).
                 Ignore effects you don't understand.
- ITeleportable  Teleport(position, rotation).
- IClimbable     CanClimb, Grip, Anchor, Body, Visual.
                 Use Climbables.TryGet(collider, out climbable) to find one.
- IGrabbable     Weight, CanGrab, TryStartGrab, Hold, Release (hands pulling blocks).

Data (Core/Data):
- EffectType / HazardType: ScriptableObject assets. New effects and hazards
  are NEW ASSETS, not new code (right-click > Create > Deadly Jenga).
- Effect: { type, strength, duration, source }.
- IHazard: Type, Active, Begin(), End(). Every disaster prefab implements it;
  game modes start/stop hazards without knowing what they are.
- Layers: Survivor, Block, Surface.

Services (Core/Services):
- Services.Session (ISessionInfo): MenuOpen, SetMenu. Set by GameSession.
- Services.World (IWorldQuery): WaterHeight, IsSheltered, SafeSpawnPoint,
  Blocks, Center, TopHeight, Radius, StepHeight. Set by the active map.
- IRebuildable: Rebuild(). The active map implements it (the reset-castle button).
- ISyncedWorld: Root, Revision, SaveLayout, LoadLayout, SetReplica. The active map
  implements it so multiplayer can copy it; networking never names a map class.
- IMapFootprint: the map's ground outline, for art shaped around it (works in edit mode).

State (Core/State) - read-only views for sound, HUD and bots:
- IMovementState (walking/climbing characters), IFlyingState (flying hands).
- IPlayerView: the local player's camera, VR flag and stamina status (for HUDs).
- Tracked.Movers / Tracked.Flyers: characters add themselves in OnEnable, remove in OnDisable.

Events (Core/Events/GameEvents): BlockHit, Pushed, Damaged, Eliminated,
EffectStarted, EffectEnded, HazardStarted, HazardEnded, Emoted, RoundStarted, RoundEnded.
Whoever causes something raises it (GameEvents.RaiseX). Whoever cares subscribes
in OnEnable and unsubscribes in OnDisable/OnDestroy.

Test stand-ins (Core/Testing): TestDummy (implements the character abilities and
logs), FakeWorldQuery, FakeSession. Use them in Sandbox scenes.

### Changing Core
- Only ADD. Never rename or change an existing member.
- To remove something: mark it [System.Obsolete] first; delete it once nothing uses it.
- Do not edit Core unless the task says so. If you need something that is not
  there, stop and report the smallest addition (name + signature) that would help.
- Core holds no gameplay logic.

### Presentation never gets called by gameplay
- Gameplay code never plays sounds, spawns particles or calls the HUD.
  It raises GameEvents or exposes read-only values. Presentation listens/reads.
- Gameplay prefabs keep art in a child named "Visual". Gameplay never depends on it.

## Working rules
1. Only change files inside the box and lane named in your task.
2. Need something from another box? Do not reach across. Stop and report
   what you need and the smallest Core addition that would provide it.
3. Lower boxes only ADD to what they expose. Never rename or remove a public
   member another box uses unless the task says so.
4. If a feature you depend on does not exist yet, build against a Core
   interface and test with a stand-in in your box's Sandbox scene.
5. Every box must run in its own Sandbox scene with stand-ins for anything above it.

## Unity and files
- Move/rename assets only through the Unity Editor (or Unity MCP asset tools),
  never with shell mv/rm. Every asset must keep its .meta file.
- No hard-coded "Assets/..." paths in runtime code. Use serialized references
  or Resources folders.
- Never edit ProjectSettings/, Packages/, or Assets/XR/ unless the task says so.
- Never add, remove, or upgrade packages.
- Never commit Library/, Temp/, Logs/, Builds/, UserSettings/, or Assets/_Recovery/.
- Never add editor scripts that create or overwrite assets automatically when Unity loads.
- Editor tools find assets by name (ProjectPath.Find / FolderOf), never by hard-coded path.
- Each box's Editor folder needs an editor-only assembly definition (or an .asmref
  to the box's existing one). Do not leave Editor scripts inside a runtime assembly.
- New networked code (NetworkBehaviour, INetworkStruct) must live in an assembly listed
  in Photon's NetworkProjectConfig "Assemblies To Weave" (Characters, GameSession).

## Unity MCP
- Edit only prefabs and the Sandbox scene of your task's box.
- Never save Lobby.unity or Game.unity unless it is a GameSession task.
- Do not run builder/installer menu commands that regenerate scenes or
  prefabs unless the task asks for it.
- No headset is assumed. Verify in the editor (desktop/XR simulator) and say
  what was NOT verified on device.

## Git
- Branch from the latest main. Name: <box>/<lane>-<task>,
  e.g. world/blocks-pull-weights, characters/hand-grab.
- Never base work on another person's unmerged branch.
- Before finishing: merge latest main into the branch, confirm the project
  compiles, and confirm Game.unity enters Play Mode without errors.
- One task per branch. Moving files and changing logic go in separate PRs.

## Code style
- Keep comments minimal and plain. Readers are college students new to Unity.
- Prefer small, readable scripts over clever ones.

## When done, report
- Which box and lane you changed, and why.
- Any Core change you need (do not make it yourself).
- What you tested, and what still needs testing in the headset.