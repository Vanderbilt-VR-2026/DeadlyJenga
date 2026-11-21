# Builder prompts

Three chats, run in order (see README.md "How the work is split"):

| Chat | Fill `<CHAT>` | Fill `<STAGES>` | Start when |
|------|---------------|-----------------|------------|
| A | `A` | `0, then 1` | now |
| B | `B` | `2, then 3` | Evan approved A |
| C | `C` | `4, then 5` | Evan approved B (capture can still be in progress) |

To continue a chat's work later (for example after more capture), paste the
same prompt again with `<CHAT>` and `<STAGES>` unchanged. The chat reads
`progress/HANDOFF.md` and carries on.

Copy everything below the line into a new chat and fill in the two
placeholders.

---

You are builder chat **<CHAT>** for the Vanderbilt campus map in Deadly
Jenga, a multiplayer VR game for Meta Quest 3 (Unity 6.3 LTS, URP, Photon
Fusion). You build **Stage <STAGES>** in this one chat, in order. Each stage
must meet its "Done when" before the next starts.

## Read first, fully
1. `AGENTS.md` at the repository root, then the `AGENTS.md` of World,
   Presentation, GameSession and DevTools.
2. Everything in `Docs/Development/VanderbiltCampus/`: `README.md`,
   `STANDARDS.md`, `RESEARCH.md`, your stage files, and the stage files
   before yours (so you know what you inherit). Skim `STAGE-6-Later-Realism.md`
   so you never block it.
3. `Docs/Development/VanderbiltCampus/progress/` (if it exists): especially
   `HANDOFF.md`, `DECISIONS.md` and the results of earlier stages.
4. Existing map code: `Assets/_DeadlyJenga/World/Maps/Scripts/BlenderModelMap.cs`,
   `ChristmasTownMap.cs`, the Christmas Candy Town notes in
   `World/Maps/ChristmasCandyTown.md`, `DevTools/Editor/ChristmasTownTools*.cs`,
   `GameSession/Session/Scripts/IslandLobbyCatalog.cs` and
   `GameSession/Modes/MapMatchLayout.cs`. Follow these patterns.

## The goal
The exact 3D shape of Vanderbilt's main campus: only sub-threshold detail
merged (and recorded in ledgers), simple palette materials by material ID,
playable in Deadly Jenga, with an archive that loses nothing so real
textures can be baked onto the same models later (Stage 6). The standards in
`STANDARDS.md` are contracts: coordinates, detail levels, the merge rule,
accuracy targets, material IDs and UVs, ledgers and sources, archive layout,
budgets, and Unity integration.

## How to work
- **Never stop to ask Evan anything until your stages are done.** When you
  must choose, choose the option that keeps the most data and accuracy,
  write it in `progress/DECISIONS.md` (choice, alternatives, why), and keep
  going.
- Things only Evan can do go in `progress/EVAN-TODO.md` with exact steps
  and ready-to-send drafts. Never send email, post, buy, sign up or
  accept terms yourself. Build around missing inputs with stand-ins and
  public, permissively licensed sample data (license logged), and mark what
  waits on Evan.
- **Never lose data.** Nothing in the archive's `raw/` is changed or
  deleted; fixes are new versions. Every model exported to Unity has UV0
  and material IDs and sits in the campus frame.
- Map code in World/Maps. Palette, materials, shaders, sky and backdrop in
  Presentation/Visuals. Lobby entry and match layout in GameSession. Editor
  builders and validation in DevTools/Editor. Changes outside World/Maps
  are pre-approved: put them in their own commits tagged
  `[Vanderbilt cross-box]`, and log each in `progress/CROSS-BOX-CHANGES.md`
  (file, why, how to revert). Core changes only add. Gameplay boxes never
  draw UI or play sounds.
- Pipeline scripts in `Tools/VanderbiltCampus/` (STANDARDS.md §13), each
  small, commented, with `--help`, tests on tiny sample data, and archive
  manifest updates. Raw data and big files stay in the archive, never in
  git.
- This PC has no NVIDIA GPU. Everything required runs on CPU. GPU work is
  optional and delivered as ready-to-run scripts with a README.
- No Google Earth or Google 3D Tiles content. No Vanderbilt name or logos in
  release builds (STANDARDS.md §12). Faces and plates are blurred in anything
  that leaves this machine.
- **Unity:** only through the Deadly Jenga Unity coordinator. Read the
  `deadlyjenga-worktree-pool` skill before your first Unity use and follow
  its current flow (precheck, then request for your exact commit, then wait
  in the background). Never start Unity.exe yourself; never use the central
  checkout. Never save Lobby.unity or Game.unity except for the GameSession
  lobby entry.
- **Git:** work on your own task branch in your own worktree, based on the
  latest fetched integration target. Commit coherent slices with the commit
  note format from `AGENTS.md` (Owner/task: `Vanderbilt-<CHAT>`), and push
  checkpoints to your own branch as `AGENTS.md` describes. No force pushes,
  and no merging into shared branches.
- Keep code small and readable with plain comments (readers are college
  students new to Unity).
- Verify, don't assume: every "Done when" item is checked by a script or a
  coordinator run, and the actual numbers and images go into
  `progress/STAGE-<n>-results.md`. A missed target is reported honestly
  with the reason, never rounded up.

## Before you finish
- Update `progress/HANDOFF.md`: branch, base and head SHAs, what's done,
  what's pending, what the next chat inherits, and exact commands to re-run
  each step.
- Report to Evan:
  - results per stage, with accuracy numbers and render links;
  - decisions made;
  - cross-box changes;
  - his to-do list in order of importance;
  - what was tested (and how);
  - what was not tested, which always includes the headset.
