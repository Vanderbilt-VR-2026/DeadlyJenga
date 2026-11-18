# Trailer Studio handoff (builder chat to reviewer)

All five phases are built, tested through the Unity coordinator and pushed.
Nothing is merged. This file is the handoff record AGENTS.md asks for.

| | |
|---|---|
| Source branch | `claude/trailer-studio-a` on `origin` (Vanderbilt-VR-2026/DeadlyJenga) |
| Target branch | `Evan_Sprint_4` (not merged; merges cleanly with `25e76440`, checked with `git merge-tree`) |
| Base | `73339768bafdba50b5e42d9e7ced5e7c1c87a3e0` |
| Candidate | Code: `47fa54e649d6e43ed1f3818fc587c4b4bfb6abd5`. The branch tip adds documentation only. |
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
  (65 short entries) and `EXTENSION-POINTS.md`.

## Changes outside DevTools (for Evan to keep or drop)

Full table with consequences: [CROSS-BOX-CHANGES.md](CROSS-BOX-CHANGES.md).

| Commit | What |
|--------|------|
| `2fe6abf1` | Core `INetworkActorSnap`, implemented by `SurvivorNetworkView` and `HandNetworkView` (Characters). Lets playback put a remote copy exactly on a recorded pose. |
| `aa5331a4` | Core `IDirectedHazard`, implemented by `BasicDisaster` (World). Lets a cue choose which side a tornado comes from. Null keeps the game's random choice. |
| `8753dd61` | Characters `CandyPlayerRig.Gait` and `SurvivorNetworkView.Gait`: save and restore the walk cycle, so rendered and scrubbed actors walk. |
| `05c5a153` | Characters `HandNetworkView.SnapToState` also sets the physics bodies. |
| `c419d1fd` | Characters `CandyPlayerRig.StepGait`: the walk cycle's step as a pure function (a copy of the sum in `ApplyFrame`, which is unchanged). |
| `c737b172` | Characters: `StepGait` uses a frame's recorded walk phase when it has one, like `ApplyFrame`. |

Both only add. No ProjectSettings, Packages or `Assets/XR` change. No package
added. No scene or prefab saved.

## Test results (actual, through the coordinator)

Every suite ran on the same code commit, `47fa54e6`, each as its own
coordinator job (owner `claude-builder-prompt`); the phases in a batch
editor, the window tour in an interactive one.

| Suite | Job | Result | Honest caveats |
|-------|-----|--------|----------------|
| Phase 1 | `133a871c` | 0 failures | Spec test 3 ("rides a moving block") is **not met**: the live game does not carry a standing survivor, and playback reproduces that. See `PHASE-1-results.md`. |
| Phase 2 | `a73b1036` | 0 failures | Headset test covered only as data and wrist-panel logic, no XR session. Scripted performer never climbed. |
| Phase 3 | `d00573e3` | 0 failures | Tornado particles are not replayed exactly; blocks and hazard state are. No test puts a cue exactly on the branch frame. |
| Phase 4 | `52d89f82` | 0 failures | The foot-slide number follows body speed; it does not prove planted feet stay still. |
| Phase 5 | `5bdecbe7` | 0 failures | Handheld camera fed by script, not a controller. FCPXML not opened in Resolve. |
| Window tour | `d0acc8e3` | 0 failures | Scripted: every tab opened and used, 12 screenshots, undo and redo, one audio listener. No person has used the window by hand. |

Read a job with `coordinator.py job --owner claude-builder-prompt --id <id>`;
its Unity log is under the pool's `runs/<id>/`.

## Review round 1 (Fable 5.2 chat) and what was done

| Finding | Done |
|---------|------|
| A1 legs frozen in renders and scrubbing | Fixed: `8753dd61` (Characters addition) and `fdac5f12`. New test in Phase 4. |
| B1 rider not carried | Game behaviour. Not changed; listed for Evan in `CROSS-BOX-CHANGES.md`. Phase 1 test 3 now reported as not met. |
| B2 music-only sound | Both suggested causes fixed in `fdac5f12`, but the real cause was a third: impacts are never events. Sound is now recorded with the live world, `54403bcd`. |
| C1, C2, C3, C4, C5, C6, C7, C8, C10 | Fixed in `fdac5f12`. |
| C9 reflection on `GiveEquipmentTo` | Listed as a wanted public method. |
| C11 hand snap | Fixed: `05c5a153` (Characters). |
| C12 heading stays set | No change needed (one-shot instances). |

Per-phase detail: `PHASE-1-results.md` to `PHASE-5-results.md`. Pictures and
reports: `evidence/`.

## Review round 2 (Fable 5.2 chat) and what was done

| Finding | Done |
|---------|------|
| A1 walk-state build was slow and raised emote events | Fixed: `c419d1fd` (Characters addition `StepGait`) and `f925b683`. |
| A2, A3, A4 live sound could be lost, mismatched or left open | Fixed in `f925b683` and `97cdb6c0`. |
| B1 hit pause missing in renders, B2 cameras ignore overrides | Fixed in `f925b683`. No test covers them yet. |
| B3 render leaves quality settings changed after a crash | Old values kept in SessionState and restored after a reload. The render still changes the pipeline asset's values in memory while it runs. |
| B4 lock feet, B5 fall then get up, B6 slow contact search, B7 shared render folders, B8 window work in the game loop | Fixed in `f925b683`. |
| C2, C3, C4, C5, C8, C9, C11, C12 | Fixed in `f925b683`. |
| C10 blurred renders darker? | Checked with a new test: they were already equal. A conversion added on suspicion made them brighter and was removed in `97cdb6c0`. |
| C1 emotes on game time | Listed as wanted from Characters. |
| C6, C7 export details | Not changed; untested without an editor. |

## Review round 3 (Fable 5.2 chat) and what was done

| Finding | Done |
|---------|------|
| `StepGait` ignored a recorded walk phase | Fixed: `c737b172` (Characters). |
| Any later reaction ended a fall | Only a get-up does now. |
| B1 cue on the branch frame never fired | Fixed. No test for it. |
| B2 stale badges only updated on recording | `StudioTakes.CheckStale` runs on every take switch. |
| B3 number keys switched the world take alone | They bring the hazard, effect and sound takes of that run. |
| B4 crowd people popped in and out | Copies hold their first and last pose. |
| B5 closing the stage left a variation run "running" | Fixed. |
| B6 a chosen variation could not be made again | Each variation keeps its cue times and strengths; its card can write them into the Set. |
| B7 empty frames after a branch | Checked: the hazard, effects and sound players all accept an empty frame; the world take always reaches the branch point. No change. |
| B8 one badge text per track | Not changed (low). |
| Tests for round 2's B1, B2, B4, B5 | Added to Phase 4; all pass. |

The reviewer's summary after three rounds: "the engine (tape, playback,
recording, render) has now been read end to end and its faults found so far
are fixed and tested; the remaining risk is in the window by hand, in VR,
and in an editor opening the FCPXML. None of that needs another reading
pass."

## What the review covered

The Fable 5.2 chat closed its review at tip `39752776` with nothing further
found. In its words: three reading passes over the tape, tracks, run loop,
render, export, cameras, overrides, motion edit, takes, branch and
variations, plus the window shell and the cross-box commits. It did **not**
read the timeline, viewport, the tab files beyond the calls they make, the
director, handheld camera, wrist panel or Theatre import, and it ran
nothing in Unity. Every pass and fail number here comes from the builder
chat's own coordinator runs.

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
- Sound of actors performed by a person, and of branched worlds, is not in
  the cut's sound (see `PHASE-5-results.md`).

## Known gaps by design

Listed in `DECISIONS.md`: 28, 30, 33, 35, 42, 44, 48, 49. The larger ones:
no physics ragdoll for reactions, anamorphic squeeze stored but not applied,
the Cut tab is a list and not a second timeline, no stunt clips, voice lines
or eye direction in Phase 4.

## For the reviewer

Things I would check first, because I am least sure of them:
1. `Tracks/WorldTrack.cs` physical playback (`FixedStep`) and the rider
   finding in `PHASE-1-results.md`: why is a standing survivor not carried
   by a sliding block, live or in playback?
   Also the missing impact sounds in `PHASE-5-results.md`.
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
