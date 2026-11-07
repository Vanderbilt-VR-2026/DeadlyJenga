# Deadly Jenga — rules for AI agents

Multiplayer VR survival game for Meta Quest 3.
Unity 6.3 LTS (6000.3.23f1), URP, XR Interaction Toolkit + OpenXR only
(no Meta XR SDK / Oculus packages), Photon Fusion networking.
Start scene: Assets/_DeadlyJenga/GameSession/Session/Scenes/Lobby.unity

## Boxes
All team code lives in Assets/_DeadlyJenga/. It is split into boxes.
Each box has its own assembly definition (DeadlyJenga.<Box>).
A box may only use boxes at a LOWER level. Never the same level or higher.
Today every box reaches the others ONLY through Core: World, Characters, Presentation and
GameSession assemblies reference Core and no other box (DevTools may reference everything).

| Level | Box          | Lanes (subfolders)          | May use                 |
|-------|--------------|-----------------------------|-------------------------|
| 0     | Core         | -                           | nothing                 |
| 1     | World        | Blocks, Maps                | Core                    |
| 2     | Characters   | Player, Hand, Bots          | Core                    |
| 3     | Presentation | Audio, Visuals              | Core                    |
| 4     | GameSession  | Session, Modes              | Core                    |
| side  | DevTools     | -                           | all boxes; nothing may use DevTools |

- World: blocks, physics, pull rules, map layouts, island/ocean/sky objects.
- Characters: player movement/climbing, avatar, flying hand, bots.
- Presentation: sounds, HUD, every menu (in-game and lobby), materials, textures, shaders,
  skybox, environment art.
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
- Services.Session (ISessionInfo): menu, role (survivor/hand), Player and Hand (IPlayableCharacter),
  IsVR, ActiveCamera, CanEditWorld, ResetMap, SurvivorBots/HandBots, Multiplayer, Demo
  (IDemoControls: bots and disasters for the menus), Social (ISocialControls: AI voices, mic).
  Set by GameSession (CastlePlaySession in a match, IslandLobby in the lobby).
- Services.World (IWorldQuery): WaterHeight, IsSheltered, SafeSpawnPoint,
  Blocks, Center, TopHeight, Radius, StepHeight. Set by the active map.
- IRebuildable: Rebuild(). The active map implements it (the reset-castle button).
- ISyncedWorld: Root, Revision, SaveLayout, LoadLayout, SetReplica. The active map
  implements it so multiplayer can copy it; networking never names a map class.
- IMapFootprint: the map's ground outline, for art shaped around it (works in edit mode).

State (Core/State) - read-only views for sound, HUD and bots:
- IMovementState (walking/climbing characters), IFlyingState (flying hands).
- IPlayerView: the local player's camera, VR flag and stamina status (for HUDs).
- IFingerTips: where a tracked index fingertip is (pressing the lobby's giant keys).
- Tracked.Movers / Tracked.Flyers: characters add themselves in OnEnable, remove in OnDisable.

Session (Core/Session) - what the session, menus and lobby use:
- IPlayableCharacter: the local survivor or hand (camera, VR, input pause, Place, status).
- ISurvivorSettings: comfort/turning, CurrentRules + ApplyMatchRules (VR and stamina values),
  FitJump, TryStandOn.
- MatchRules + MatchOptions: rule values and option lists; Characters fills the lists on load.
- MatchChoices: what the lobby menus chose (rules, population, AI hands, presets);
  GameSession applies it when Game loads.
- ILobbyControls: the island lobby's three terminals (join, host, map + mode): values and
  commands. IslandLobby (GameSession) provides it and raises GameEvents.LobbyStarted;
  IslandLobbyView (Presentation) draws the terminals and keyboard. Collision stays with the
  lobby: the view hands over shapes (SetFloor, AddBlocker).
- IDemoControls / ISocialControls: the demo and voice pages' values and commands.
- ISocialBot + SocialBots.Attach: a survivor bot as the social layer sees it (Characters
  provides it). IVoicePlayer / IVoiceMix + VoicePlayback.Create: voice streams are played by
  Presentation; the social layer only hands over samples.
- IBotPopulation / ISurvivorBotPopulation + BotSystems.Create: bots without naming bot classes
  (spawn, clear, presets, Members, AutoFill, ThreatCost).
- IMultiplayerControls (state and commands for the menus' multiplayer page),
  ILocalPlayerInput (lobby Photon input).

Network (Core/Network) - characters pack their own network state:
- INetworkActor (NetworkKind, NetworkActive, NetworkPosition, WriteState) on local characters;
  NetworkActors.Local lists them. INetworkActorView = a remote copy (ReadState, Render, Suspend).
- NetworkActors.Register/CreateView/WriteSpawn: Characters registers one maker per NetworkKinds kind.
- NetWire: validated read/write helpers. INetworkPreview: joined machines preview their own hand.

Events (Core/Events/GameEvents): BlockHit, Pushed, Damaged, Eliminated,
EffectStarted, EffectEnded, HazardStarted, HazardEnded, Emoted, RoundStarted, RoundEnded,
SessionStarted (Presentation attaches menus/HUD here).
Whoever causes something raises it (GameEvents.RaiseX). Whoever cares subscribes
in OnEnable and unsubscribes in OnDisable/OnDestroy.

Test stand-ins (Core/Testing): TestDummy (implements the character abilities and
logs), FakeWorldQuery, FakeSession. Use them to test against something that doesn't exist yet.

### Changing Core
- Only ADD. Never rename or change an existing member.
- To remove something: mark it [System.Obsolete] first; delete it once nothing uses it.
- Do not edit Core unless the task says so. If you need something that is not
  there, stop and report the smallest addition (name + signature) that would help.
- Core holds no gameplay logic.

### Gameplay announces, Presentation shows
- World, Characters and GameSession never play sounds, spawn particles, call the HUD,
  or draw any UI (no OnGUI, GUILayout, Canvas, TMP, UnityEngine.UI). Their assemblies
  don't reference UI packages, so the compiler catches it.
- They raise GameEvents or expose values and commands through Core. Presentation listens,
  reads and draws.
- Core contracts never contain drawing methods. A setting is exposed as a value
  (e.g. ISurvivorSettings.CurrentRules) and Presentation draws the control for it.
- Menus are allowed to send the player's requests through Core commands
  (session.SetRole, lobby.Host, multiplayer.LeaveSession). Presentation passes
  on the request; the box that owns the rule decides. That is not gameplay in Presentation.
- A thing owns its own body: a block or character may set the materials and colors of its
  own model. Presentation owns everything else that is seen or heard: UI, sound, the
  environment, and reactions to events.
- Gameplay prefabs keep art in a child named "Visual". Gameplay never depends on it.
- Scenes are containers: UI in a scene is a Presentation prefab or is built by Presentation at runtime
  (e.g. the island lobby's terminals), and
  game objects are never nested inside a UI canvas.

## Working rules
1. Only change files inside the box and lane named in your task.
2. Need something from another box? Do not reach across. Stop and report
   what you need and the smallest Core addition that would provide it.
3. Lower boxes only ADD to what they expose. Never rename or remove a public
   member another box uses unless the task says so.
4. If a feature you depend on does not exist yet, build against a Core
   interface and test with a stand-in (Core/Testing).
5. Before merging, Game.unity still plays solo; networking changes also get a
   two-player Multiplayer Play Mode test.

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
- Edit only prefabs of your task's box (and your own scratch scenes).
- Never save Lobby.unity or Game.unity unless it is a GameSession task.
- Do not run builder/installer menu commands that regenerate scenes or
  prefabs unless the task asks for it.
- No headset is assumed. Verify in the editor (desktop/XR simulator) and say
  what was NOT verified on device.

## Git: contributor branches and shared handoffs

This is the repository-wide workflow for contributors and their coding agents,
on any machine. Each contributor works on their own branches. Shared Git history
and pull requests carry the handoff; no particular laptop, chat, private Workboard,
worktree pool or agent product is required. Machine-local instructions may add
local safety checks but cannot replace information teammates need in the repo/PR.

Agents are expected to commit and push owned, approved work routinely using the
contributor's authorized Git access. Respect that contributor's explicit limits,
repository permissions, branch protections and required approvals. This is not
permission to publish another person's branch, bypass Core approval, force push,
delete branches, change remotes/permissions, or publish releases.

### Work and publish on your own branch
- Verify repository, current branch/HEAD, index/dirty state and configured remote
  URLs. Identify the intended integration target explicitly; do not assume it is
  `main` or a branch used on another machine. In a fork, the contributor's push
  remote and upstream target may legitimately differ; verify both.
- Preserve existing agreed contributor branches. For new tasks, prefer one task
  branch with a distinct owner/task name, based on the latest fetched intended
  target. Do not build on a teammate's unmerged branch unless that dependency is
  intentional and recorded. Never switch or reset a teammate's working checkout.
- Commit a coherent, reviewable slice after relevant cheap checks, before handing
  off, switching tasks or stopping. During longer work, aim for a safe checkpoint
  about every 30 minutes. Do not interrupt useful editor work, create empty commits
  or commit broken partial changes just to meet a timer. Clearly label pending QA.
- Stage explicit owned paths/hunks and corresponding Unity metadata. Inspect the
  staged diff and run `git diff --cached --check`; do not sweep unrelated edits
  into a commit with blanket staging. Keep credentials, personal instructions,
  machine-private state and generated output out of commits.
- Changes awaiting explicit approval, including Core/dependent patches and gated
  prototypes, remain uncommitted/unpushed where those rules require it. Never
  commit an inherited test-only overlay as your own work. Preserve a sealed draft
  with its blocker; share only through an authorized location accessible to the
  reviewer. Publication does not substitute for approval.
- Push eligible checkpoints promptly to the exact contributor-owned branch on
  the verified authorized remote. Inspect the full outgoing range, including
  inherited commits, and every push URL. Reject unintended destinations or mirror
  mode; disable automatic tag/submodule publication. For example:
  `git -c push.followTags=false -c push.recurseSubmodules=no push <push-remote> HEAD:refs/heads/<owned-branch>`.
  Never use force/all-branch/mirror pushes or delete branches under this policy.
- Verify the remote branch SHA after pushing. If authentication or a rejected
  non-fast-forward blocks publication, preserve the local commits and report the
  exact blocker. Fetch and inspect divergence; reconcile with the branch owner
  rather than resetting, overwriting history or retrying indefinitely. Do not
  auto-pull into dirty/shared open checkouts. No change means no commit/push spam.

### Leave notes teammates can use
Use a concrete commit subject. Explain the trigger, resulting behavior and why
in the body; keep detail proportional to the change. Include actual checks and
pending checks, important dependencies and whether this is a checkpoint or a
merge-ready slice. A useful body can be as short as:

```text
Fix <specific behavior> in <box/lane>

<What changed and why; important files or compatibility consequences.>

Owner/task: <contributor and task ID>
Tests: <actual checks/results; pending runtime/network/headset checks>
Depends-on: <full required commit hashes, or none>
Evidence: <team-accessible report/artifact/PR reference, or none>
Status: <checkpoint-not-ready or ready-for-review; known risks>
```

At handoff, update the existing PR description, or a repository-visible handoff
record if there is no PR. Record source branch, target branch/remote, full base
and candidate/verified remote SHAs, scope, dependencies, superseded candidates,
actual tested revision, evidence, approval state, unresolved failures and next
action. Use links teammates can access; a local Windows path, private chat or
private Workboard alone is insufficient. Keep shared notes current when the
candidate changes. Separate Git notes are not the handoff mechanism.

Open/update a draft PR for work in progress when using the PR workflow; mark it
ready when the slice is ready for review, and merge only after required reviews/
checks pass. A pushed checkpoint
is recoverable source, not proof of merge readiness, integration or APK inclusion.
Local dashboards may mirror these facts but are not the only source of context.

### Merge branches without losing other contributors' work
- Contributors/agents may prepare integration in their own branches. Updates to
  a shared target are serialized through its designated maintainer/integration
  owner or merge queue and obey branch protections. No particular local chat is
  the universal integrator. Automatic merging requires the target owner's
  authorization and its required checks/reviews; task-branch push permission
  alone does not grant permission to push or merge into the shared target.
- Fetch the latest target before final integration; inspect ancestry, changed
  paths and explicit dependencies. Already-ancestor commits are included;
  squashed/cherry-picked work needs verified equivalence before applying again.
  Never merge `IGNORE_EvanBranchForTransferBetweenLaptops` into main or open its PR.
- Resolve conflicts with the affected owners when behavior/ownership is unclear.
  Do not blindly choose whole ours/theirs sides or overwrite newer code. Preserve
  existing branch history and unfinished work. Keep asset moves separate from
  logic changes and preserve Unity metadata.
- Validate the combined result: game code compiles and Game.unity enters solo
  Play Mode; networking also needs two-player validation. Reuse applicable exact
  evidence when dependencies are unchanged. Documentation-only work needs diff/
  link checks, not a Unity import. Record failures and pending checks honestly.
- Update the shared PR/handoff with the actual resulting merge/integration SHA.
  Keep source integration, runtime validation and APK source/artifact identity
  separate. A commit appearing on a remote branch does not prove it shipped.

## Code style
- Keep comments minimal and plain. Readers are college students new to Unity.
- Prefer small, readable scripts over clever ones.

## When done, report
- Which box and lane you changed, and why.
- Any Core change you need (do not make it yourself).
- What you tested, and what still needs testing in the headset.