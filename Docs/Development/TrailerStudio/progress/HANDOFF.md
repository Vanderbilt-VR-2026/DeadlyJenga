# Trailer Studio handoff (builder chat to reviewer)

All five phases are built, tested through the Unity coordinator and pushed.
Nothing is merged. This file is the handoff record AGENTS.md asks for.

| | |
|---|---|
| Source branch | `claude/trailer-studio-a` on `origin` (Vanderbilt-VR-2026/DeadlyJenga) |
| Target branch | `Evan_Sprint_4` (not merged; merges cleanly with `25e76440`, checked with `git merge-tree`) |
| Base | `73339768bafdba50b5e42d9e7ced5e7c1c87a3e0` |
| Candidate | CANDIDATE_SHA |
| Scope | DevTools (editor + runtime `TrailerStudio` folders), two small cross-box commits, docs under `Docs/Development/TrailerStudio/` |
| Depends on | nothing outside this branch |
| Approval | none yet. Evan pre-approved cross-box changes in the builder prompt; each is still his to keep or drop. |
| Order built | 1, 2, then 3, 5, 4 (Evan's change of plan), by one chat |

## Where things are

- Code: `Assets/_DeadlyJenga/DevTools/Editor/TrailerStudio/` (Data, Tracks,
  Engine, UI, Tests) and `Assets/_DeadlyJenga/DevTools/Scripts/TrailerStudio/`
  (three small runtime components). About 11,700 lines in 62 scripts.
- Open it in Unity: menu **Tools > Deadly Jenga > Trailer Studio**. User guide:
  `Assets/_DeadlyJenga/DevTools/Editor/TrailerStudio/TrailerStudio.md`.
- Plans, decisions and results: this folder. Start with `DECISIONS.md`
  (53 short entries) and `EXTENSION-POINTS.md`.

## Changes outside DevTools (for Evan to keep or drop)

Full table with consequences: [CROSS-BOX-CHANGES.md](CROSS-BOX-CHANGES.md).

| Commit | What |
|--------|------|
| `2fe6abf1` | Core `INetworkActorSnap`, implemented by `SurvivorNetworkView` and `HandNetworkView` (Characters). Lets playback put a remote copy exactly on a recorded pose. |
| `aa5331a4` | Core `IDirectedHazard`, implemented by `BasicDisaster` (World). Lets a cue choose which side a tornado comes from. Null keeps the game's random choice. |

Both only add. No ProjectSettings, Packages or `Assets/XR` change. No package
added. No scene or prefab saved.

## Test results (actual, through the coordinator)

RESULTS_TABLE

Per-phase detail: `PHASE-1-results.md` to `PHASE-5-results.md`. Pictures and
reports: `evidence/`.

## What was NOT tested

- **Nothing in a headset or the XR simulator.** Performing in VR, the wrist
  panel at arm's length, the handheld VR camera's viewfinder, comfort and
  hand tracking all still need a person in a Quest 3.
- **A person using the window.** Every tab was opened and used by a script in
  a real editor window (screenshots in `evidence/window/`), but nobody has
  dragged a mark, scrubbed with the mouse or performed a take by hand.
- **FCPXML in DaVinci Resolve or Premiere.** The file is well-formed and its
  media exists; it has not been imported.
- **Two-player multiplayer.** The two cross-box commits add an interface and
  a few lines to the network views and the disaster. AGENTS.md asks for a
  two-player Play Mode test for networking changes; that was not run.
  Solo `Game.unity` with the Studio closed was run (Phase 1 test 5).
- MP4 and ProRes output need `ffmpeg` on the PATH; only PNG sequences were
  rendered in tests.
- Foot sliding after a retime is measured, not proven (see
  `PHASE-4-results.md`).

## Known gaps by design

Listed in `DECISIONS.md`: 28, 30, 33, 35, 42, 44, 48, 49. The larger ones:
no physics ragdoll for reactions, anamorphic squeeze stored but not applied,
the Cut tab is a list and not a second timeline, no stunt clips, voice lines
or eye direction in Phase 4.

## For the reviewer

Things I would check first, because I am least sure of them:
1. `Tracks/WorldTrack.cs` physical playback (`FixedStep`) and the rider
   result in `PHASE-1-results.md`.
2. `Engine/StudioRunPass.cs` `RecordRoutine`: punch-in, branch and stale
   marking share one routine and it is the longest method in the Studio.
3. The two cross-box commits: is anything in multiplayer affected?
4. Whether the acceptance tests in `Tests/` really test what the phase files
   ask, or pass too easily. Tests 4.1 (feet) and 2.4 (headset) are the
   weakest.
5. Code readability for students: comments, names, file sizes.

## Next action

Reviewer (the "Fable 5.2" chat) reads this, reviews the branch and reports
problems to the builder chat. Evan then decides whether to merge and which
cross-box commits to keep.
