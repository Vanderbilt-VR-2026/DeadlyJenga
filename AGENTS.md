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
| 2     | Characters   | Player, Hand, Bots          | Core, World             |
| 3     | Presentation | Audio, Visuals              | Core, World, Characters |
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

## Core
Core holds only shared definitions: interfaces, events, constants, plain data.
No gameplay logic.
Current contracts: IPullable, ISessionInfo, ISyncState, GameEvents, Layers.
Do not edit Core unless the task explicitly says so.

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