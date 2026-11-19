# Trailer Studio handoff (builder chat to reviewer)

All five phases are built, tested through the Unity coordinator and pushed.
Nothing is merged. This file is the handoff record AGENTS.md asks for.

| | |
|---|---|
| Source branch | `claude/trailer-studio-a` on `origin` (Vanderbilt-VR-2026/DeadlyJenga) |
| Target branch | `Evan_Sprint_4` (not merged; merges cleanly with `25e76440`, checked with `git merge-tree`) |
| Base | `73339768bafdba50b5e42d9e7ced5e7c1c87a3e0` |
| Candidate | Code: `6047138620b2da5c73b02ca482779f18d2aebe64` (three reviews plus five fix-agent branches merged). The branch tip adds documentation only. |
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
| `2496f88d` | Characters `CastleSurvivor`: the moving-floor support is the block's own surface copy, so a standing survivor rides a moving block. **Changes the live game.** |
| `841b9b98` | Characters `CandyPlayerRig`: `ApplyFrame` and `StepGait` share one walk-cycle step. Meant to change nothing. |

Both only add. No ProjectSettings, Packages or `Assets/XR` change. No package
added. No scene or prefab saved.

## Test results (actual, through the coordinator)

After three review rounds, five fix agents (Opus) each worked on their own
branch and the builder merged them. Suites ran through the coordinator
(owner `claude-builder-prompt`), in batch editors unless noted. Most ran on
merge commit `3608bd41` (four branches); `60471386` adds the fifth branch
(wrist panel, handheld camera, the XR and network suites, one playback
line), and the suites that touch those ran on it.

| Suite | Job | Commit | Result | Honest caveats |
|-------|-----|--------|--------|----------------|
| Phase 1 | `9b95e35e` | `3608bd41` | 0 failures | Test 3 now passes for real: the survivor is carried 0.71 m with a 0.70 m block, live and against playback. Needs the Characters fix `2496f88d`. |
| Phase 2 | `fb31c231` | `60471386` | 0 failures | Scripted performer never climbed. |
| Phase 3 | `34cdab39` | `3608bd41` | 0 failures | Tornado particles are not replayed exactly. |
| Phase 4 | `82153512` | `3608bd41` | 0 failures | The foot-slide number follows body speed. |
| Phase 5 | `7696823d` | `3608bd41` | 0 failures | Export checked against the format rules, not opened in an editor. |
| Sound (new) | `cd11c6ed` | `3608bd41` | 0 failures | Proves performed sound was captured and lined up, not which sounds are in it. |
| Cut (new) | `e314cec3` | `3608bd41` | 0 failures | A variation made again from its recipe ends at most 41 mm from the original, not bit-identical. |
| UI (new) | `726a1133` | `3608bd41` | 0 failures | Batch mode: no real layout, no mouse. |
| XR (new) | `c897c8cf` | `60471386` | **1 failure, a finding** | Emulated headset and controllers, not a Quest. The failure is the head-tracking freeze in the game's Characters code, left for Evan (below). |
| Net (new) | `b3b6b94c` | `60471386` | 0 failures | Remote copies render the same as on the base commit. Not a two-player session. |
| Window tour | `304e877e` | `60471386` | 0 failures | Interactive editor, scripted. No person has used the window by hand. |
| Game's own checks | `228cd2b0` vs base `cb8073c5` | `3608bd41` vs `07073018` | identical | `SocialMergeValidation`: 156 pass, 56 fail on BOTH. The failures are that suite's own on this PC; the merged code changes none of them. |

Read a job with `coordinator.py job --owner claude-builder-prompt --id <id>`;
its Unity log is under the pool's `runs/<id>/`.

## Fix agents (after the reviews) and what they did

| Agent branch | What it did |
|--------------|-------------|
| `claude/ts-carry` | Characters: a standing survivor now rides a moving block (`2496f88d`); the walk-cycle sum exists once (`841b9b98`). Phase 1 test 3 requires riding. |
| `claude/ts-sound` | Sound per world take; a branch's sound is the parent's up to the branch frame plus the new part; takes performed in real time record what the performer heard; the export adds those lanes and a mixed `cut-sound.wav`. |
| `claude/ts-cutfix` | Variation recipes by cue id with final strength and seed; a cue on the branch frame is tested; FCPXML and EDL follow the format rules (asset durations, two-line dissolves); separate track warnings. Found and fixed: the collapse cue ignored variation jitter. |
| `claude/ts-xrnet` | Emulated-XR suite and network suite. Fixed: wrist-panel keys are pressed with the fingertip (they tested the palm, 3-5 cm away); the handheld camera keeps one pose per tape frame. |
| `claude/ts-uiread` | Read the files no reviewer had read (timeline, viewport, tabs, cues, environment, Theatre import, library) and fixed 13 defects; new UI suite. |

## Open after the agents (for Evan)

- **Head tracking freezes when the Studio puts you back in a body** while
  the headset is already tracked. `CastleSurvivor.ConfigureHeadTracking`
  gives the pose driver a pass-through tracking-state action, which reads 0
  until the value changes. Suggested one-word fix: make it a Value action.
  Not made: it touches head tracking for the whole game and cannot be
  checked without a Quest. The XR suite's one failure is this.
- **The carry fix changes the live game:** survivors and bots ride moving
  blocks on every map, and climb grip refills only on a block at rest.
- **Edits are allowed while a recording runs** (Clone, Crowd, Polish edits,
  take Use and Trash). Needs a decision: disable or queue.
- **Timeline lanes are 13 px narrower than the ruler** when the track list
  scrolls (more than about seven tracks), so the end of the tape is cut off.
- **A hazard cue's "Ends after" stops every hazard of that type**, not only
  its own.
- **Missing from `UI.md`:** timeline zoom, branch and punch-in markers,
  draggable camera keys in the viewport, take thumbnails beyond the world
  cards, a waveform, J/K/L shuttle, light-skin colours.
- Smaller unfixed findings from the UI read are in the agent's list, kept
  in `AGENT-FINDINGS.md`.

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

- **A real headset.** The VR paths now run in an emulated XR session
  (`StudioAcceptance.RunXr`): real-time clock, a VR take storing head and
  hands exactly, the wrist panel pressed with the fingertip, the handheld
  camera, playback in the body. Still untested: a Quest, OpenXR, comfort,
  reach, frame rate.
- **A person using the window.** Every tab is opened and used by a script;
  the drag fixes from the UI read need a mouse.
- **The export in an editing app.** FCPXML and EDL are checked against the
  format rules. Neither DaVinci Resolve nor Premiere is installed on this
  PC, so nothing was imported.
- **A real two-player session.** No game code calls the Studio's additions
  and remote copies render the same as on the base commit, but the game's
  own network checks need Photon's cloud and were not run. Multiplayer Play
  Mode 2.0.2 is installed and needs an interactive editor.
- MP4 and ProRes output were not rendered in a test (PNG sequences were).
- Foot sliding after a retime is measured, not proven.
- Sound: nobody has listened to it. Performed sound is one mix with no
  stems and drifts in slow motion.

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
