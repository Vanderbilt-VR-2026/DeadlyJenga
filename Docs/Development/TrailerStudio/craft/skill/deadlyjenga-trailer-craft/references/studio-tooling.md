# Trailer Studio: what it can and cannot do (as of 2026-11-20, revision 4)

**Read `record-and-layer.md` first**: record the world once, commit the take, and direct every camera
and actor layer against it. This file lists what each piece does; that file says in which order.

**Dated 2026-11-20.** Based on attempt 1's findings (`Docs/Development/TrailerStudio/attempts/attempt-1/FINDINGS.md`,
F1-F21), the Studio handoff (`progress/HANDOFF.md`, candidate `60471386`, branch
`claude/trailer-studio-a`), `StudioCameraData.cs`, and the hand-reveal pilot
(`Docs/Development/TrailerStudio/attempts/pilot-hand-reveal/PILOT-REPORT.md`, branch
`claude/trailer-hand-reveal-pilot`, final commit `249cf4b05658046aa9379253debe8802c071f398`, not
pushed as of the report), and the hand-reveal-2 field test (`attempts/hand-reveal-2/REPORT.md` in the
DeadlyJenga repo; same branch, commit `164e7115`, 13 coordinator jobs). For what the Studio can cue
for **disasters and PvP**, cue by cue with file references, read `game-content.md` section 9 first. Before relying on any row, check `HANDOFF.md`, `DECISIONS.md` and the git
log for newer work, and update this file with the date when a row changes. In planning-only mode
(no git, no engine) use the table as written and say in the plan that it was not re-checked.

## 1. Ground rules for running it

- Every Unity use goes through the shared coordinator (`deadlyjenga-worktree-pool` skill). Never start
  Unity yourself and never run it on the central checkout. Commit the plan and driver to your own branch
  first, then request a job for that commit.
- A job has a 10-minute limit. Record plus a 1080p render of six shots did not fit (465-489 s for the
  render alone; one run timed out). Plan the work as separate jobs: scout, record, render part 1, render
  part 2, export. (F19)
- **Most of a job is editor overhead.** In the pilot a preview's own work was about 15 s of recording
  and 15 s of rendering inside a 3-5 minute job; a 1080p final was a 74 s render in a 4.6 minute job;
  a cold editor start took 123 s. Batch every check you can into one job and request `--warm`.
- "Pool lock exists" refusals happened on about 1 request in 4 in the pilot (transient; retry after
  about 5 s). A job can end `needs-review` from a pool file error while its output is fine: read the
  output before re-running.
- The Studio is driven from an editor script (attempt 1:
  `Assets/_DeadlyJenga/DevTools/Editor/TrailerStudio/Attempts/TrailerAttempt1.cs`; pilot:
  `Attempts/PilotHandReveal.cs`). There is no live view for an agent; every look costs a job. (F10)

## 2. Pilot tooling (branch `claude/trailer-hand-reveal-pilot` only, until merged)

All of it lives in `Assets/_DeadlyJenga/DevTools/Editor/TrailerStudio/`. Static entry points, run only
through the coordinator, for example:

```text
coordinator.py request --owner <you> --task "<what>" --candidate <SHA> \
  --method DeadlyJenga.DevTools.Editor.TrailerStudio.PilotHandReveal.RunPreview --warm
```

| Entry point or piece | What it does | Output |
|----------------------|--------------|--------|
| `PilotHandReveal.RunPreview` | records the take, renders 480x270 at 15 fps | `preview.mp4`, `filmstrip.png` (12 tiles), `metrics.json/csv`, `notes.md` (the plan, the chosen camera spot, a take log every 0.5 s, a hand timeline saying where the hand is on screen) in the slot's `Logs/ClaudeHandRevealPilot/` |
| `PilotHandReveal.RunFinal` | the same at 1920x1080, 30 fps, with motion blur and the cinema look | `hand-reveal.mp4` (H.264 + AAC) |
| `PilotHandReveal.RunChecks` | acceptance checks: bones filled, hand take repeats, block path repeats, world and hero repeat, pull distance, hand drawn (pixel difference with and without the hand), metrics, filmstrip | pass/fail list |
| Shot constants | timings, lens, hand distance, pull distance, calm drop, hero weight: constants at the top of `Attempts/PilotHandReveal.cs`; the plan is worked out from the map (top block, pull block, grab, hero beside the grab) | - |
| `SolveCamera` (camera-spot search) | scores 576 camera spots outside the tower (hand size, hero size, payoff in frame, hand and hero apart on screen, no camera inside geometry, 0.5 m sphere) and keeps the best | the chosen spot in `notes.md` |
| `StudioHandDirection` (directed hand cue) | a `handPull` cue with a fixed grab; the hand take is baked from a path; draws the hand from a baked mesh posed from each frame's bones (`DrawBaked`) | a hand that is drawn and repeats exactly |
| `StudioTargets` (camera targets) | aim at `point:`, `actor:`, `block:` and more, not only actor marks; bounds skip hidden renderers | fixes F8 for the pilot's shot |
| `StudioMoves` (move library) | `LockedOff`, `PushIn`, `TiltTo`, `Shake`, `CraneUp`, `Orbit`, `Follow` | camera keys |
| `StudioFraming` (framing metrics) | per frame: on-screen share, height, x/y, area, occlusion, facing for the hero and the hand; camera speed and turn rate | `metrics.json/csv` |
| Fix: `ActorFrame.Blend` aliasing | the real reason the hand was never seen in renders (in-between poses blended toward the origin, the reader threw, the actor froze) | the hand renders on this branch |
| `HandRevealMoment` (hand-reveal-2) | a multi-shot driver: `RunScout`, `RunPreview`, `RunFinalA` / `RunFinalB` (the final split into two jobs), `RunFinal` (one job); a clear-spot camera search per shot, a shadow-timing solver, a take log, per-shot stills and metrics, design comparison stills, a colour check | `Attempts/HandRevealMoment.cs`; writes the `shots.txt` and `shots/<id>*.{mp4,png,csv}` that `tools/assemble.py` and `tools/pack.py` read |
| Added by hand-reveal-2 | `StudioCue.carryTime` (how long the hand carries a block), `StudioHandDirection.BakeLook` (a game hand design on a directed take), `StudioCameraTrack.exposure / contrast / saturation` (a per-shot grade, `StudioRender.ApplyLook`) | not yet covered by `StudioAcceptance` after these changes |

How to use it for a new shot: copy `PilotHandReveal.cs` into a new attempt file on your own branch,
change the constants and the target ids, keep `RunPreview` as the scout (it gives the filmstrip, the
metrics and the hand timeline in one job), and read `metrics.json` against the pass table in
`camera.md` yourself.

## 3. Capabilities

| Need | Today | Do this instead | Finding |
|------|-------|-----------------|---------|
| See a framing before rendering | pilot branch: `RunPreview` gives a 12-tile filmstrip and per-frame metrics for its shot. Elsewhere: no preview stills from code | on the pilot branch, use `RunPreview` as the scout; elsewhere render 320-640 px stills at each shot's first, key and last tape time and print subject bounds and an occlusion raycast grid from your driver | F10, pilot |
| Judge framing automatically | no: metrics exist (pilot) but nothing checks them against the pass ranges | read `metrics.json` against `camera.md` section 4 yourself and write the numbers into the evidence pack | pilot |
| Know what happened in a take | no Studio take report; disaster-pvp-1's driver writes one (`DisasterPvpMoment.Report`: actor positions, health and equipment every 3 frames, who left their spot, meteor impacts with the nearest actor, blocks moved) | copy it and add health drops (the hits), respawns and the cue log (`record-and-layer.md` section 3, step 4) | F11, pilot, disaster-pvp-1 |
| Repeatable takes | the directed hand and the pulled block repeat exactly (pilot: 0.0 mm); **world physics does not** (9.3 cm by frame 35 before the yank, hero 5.5 cm, up to 36 cm after) | record once, commit the take bundle (tape, sound, Set JSON) and plan against that take; do not rely on a re-record matching | F12, F13, pilot |
| Render a saved take in batch | no entry point in the Studio; the drivers do it | `DisasterPvpMoment.SaveBundle` / `LoadBundle` (set.json, main.djtape, take.txt, tape.wav); every scout, preview and final loads the same bundle | F13, disaster-pvp-1 |
| Stable camera ids | random GUIDs | set each `StudioCameraTrack.id` to the plan's shot id (`S01` ...) | F14 |
| Cameras on blocks, points, hazards | pilot branch: `StudioTargets` (`block:`, `point:`, `actor:`); elsewhere actor marks only | on other commits, key a static camera from the block's bounds | F8, pilot |
| A directable hand | pilot branch: `StudioHandDirection` directed pull with a fixed grab. Elsewhere: a person performs it, or the bot flies it | `capture.hand: directed` on the pilot branch; `performed` for gestures | F6, pilot |
| The directed hand during recording | **not live**: its take is baked afterwards, so it makes no game sound, raises no game events and never touches the world; the pulled block is moved kinematically | lay all hand sounds in at assembly; expect the tower's reaction to come from the kinematic block, not the hand | pilot |
| The recorded hand drawn in renders | pilot branch: drawn (5.6 % of pixels change with and without it). Other commits: **not reliable** (F5) | on any other commit, keep `hand_drawn_in_playback` as `verify`; if it fails and no fix exists, a hand concept is BLOCKED | F5, pilot |
| One bad frame | **silently stops an actor** for the rest of a render, with only a warning, and the render still "passes" | read the hand timeline and the log warnings after every render; a frozen actor means a bad frame | pilot |
| Will a pull move a block? | performed or bot: 0.5-0.7 m measured (F7); directed pull: 3.5 m kinematic (pilot) | dry-run each pull cue in the scout job and measure travel; need ≥ 2 m within 1.5 s | F7, pilot |
| The tower after a pull | the pilot's spire lurched heavily (about 90 blocks moved > 5 cm); not controlled | plan the lurch as a possible bonus, not a cue | pilot |
| Stage anchor at the top | "highest block near the middle" sat about 4 m below the crown | print a height profile of the map and place marks and cameras from the measured top | F15 |
| Export the cut as one file | per-camera PNG/MP4, FCPXML, EDL and `cut-sound.wav`, never one playable cut | assemble with ffmpeg (attempt 1's `assemble.py`) | F1 |
| Titles | none | burn in with ffmpeg `drawtext` or a PNG overlay in the assembly step | F2 |
| **Sound cues** | **none**: the Studio cannot place a sound on a cue; the pilot and hand-reveal-2 laid every sound in at assembly. Recorded game sound events (block hits, match sounds, block breaks) do replay in playback (`Tracks/AudioEventTrack.cs`) | lay every designed sound in at assembly from the plan's or take report's times (`tools/assemble.py`); check each lane's sync alone | pilot, hand-reveal-2 |
| Music in the mix, lane gains | `set.music` only reaches an FCPXML lane; `cut-sound.wav` has no music; no per-lane gain | mix music and cut sound in the assembly step by meter, then normalise (`rhythm-and-sound.md`) | F3, F18 |
| Sound under speed ramps | plays at real speed | `studio.ramp_sound: false`; designed sound only on ramped shots (L13) | F4 |
| Motion blur | `subFrames` > 1 roughly doubles render time; URP motion blur ghosted played-back survivors (stale skinned motion vectors) | fixed on the pilot branch: `StudioRender.NoSkinnedVectors` (`StudioRender.cs:201, 276`) on blurred shots (disaster-pvp-1, verified at 640 and 1080); blur 0 on shots with fast rocks near the lens | attempt 1, pilot, hand-reveal-2, disaster-pvp-1 |
| Ghost survivors | a survivor sometimes renders see-through grey | look for it in every filmstrip; cut or re-frame | F9 |
| Washed-out (white) hero at 1080p | **the cause is URP motion blur, not bloom** (revision 2 blamed bloom). hand-reveal-2's colour test: the same 1080p frame is yellow without motion blur and white with it (`v08/colour-check.png`); very likely the pilot's washed-out hero too. Fixed since by `NoSkinnedVectors` (row above); disaster-pvp-1 still saw one unexplained lavender-white body in a scream layer | render finals with `motionBlur = 0` until the fix lands; after any render-setting change, render one 1080p frame of the hero with and without the setting and compare the hero's colour in the two stills before trusting the final | hand-reveal-2 |
| Mark colours | `StudioMark.color` is an **index into a palette** (`Data/StudioSet.cs:17`), not a colour name; index 6 rendered **yellow** in hand-reveal-2 (not the orange the agent expected) | choose by index, then confirm the colour in a full-size still before the plan, the caption or the critic names the hero by it | hand-reveal-2 |
| The directed hand's motion | its path runs on a **fixed ease**: most of the flight happens in the first 20 %, so it hangs nearly still afterwards, and the yank starts slow (its motion peak 15-25 frames after the cue's hit); there is no front-loaded pull option | measure the motion peak in the metrics and put the music hit on it, not on the cue time; keep flights short | hand-reveal-2 |
| Dust, debris, spray | **none** at a directed yank (the directed hand is not live, so no game effects fire); recorded game particles do replay (`Tracks/EffectsTrack.cs`) | let real collisions make the debris (a `push` or a live hazard), or add it in the grade; say it is missing in the human-pass questions | hand-reveal-2 |
| Riding a tilting block | a survivor on a lifted, tilting block is carried by the block's position only: off its middle he sinks into it (0.36 m), at its middle he still slides 0.27 m | stand him at the block's middle; check his feet in the scout stills | hand-reveal-2 |
| Looking at a framing | **no live view**: every look costs a job (hand-reveal-2 spent one scout and seven preview or final looks; one job ran 613 s, one was deferred for memory headroom) | batch every camera candidate into one scout job; use the clear-spot search; keep a list of what each look must answer | F10, hand-reveal-2 |
| Repeating a take | takes differ (world physics): hand-reveal-2's finals A and B were separate takes, the hero about 5 cm apart. **That was the wrong way.** | record once, commit the bundle, and render every half from it (rule 17) | hand-reveal-2 |
| Disasters and PvP on cue | `hazard`, `tsunami`, `stopHazards`, `collapse`, `push`, `weaken`, `guide`, `slowTime`, `gear`, `fire`, `glovePunch`, `aimedMeteor` (the last two added by disaster-pvp-1), reaction layers; **no unarmed punch cue**; the `tsunami` cue never pushes survivors; the stage runs Demo with deaths off | `game-content.md` section 9; `record-and-layer.md` section 6 | code read 2026-11-20 |
| Repeatable renders | `freezeAmbient` holds ocean, sky and birds still | turn it on when re-rendering one shot to compare versions | `StudioRenderSettings` |
| Directed hazards | a cue can choose which side a tornado comes from (`IDirectedHazard`, `aa5331a4`); the tsunami's height, warning time, offshore distance and speed are parameters | use them to fix screen direction and arrival time; dry-run the arrival in the scout | HANDOFF, TsunamiHazard |
| Riding blocks | standing survivors ride a moving block (`2496f88d`) | plan the lurch shot around that | HANDOFF |
| Head tracking when re-entering a body | freezes in the headset (open finding) | for performed VR takes, enter the body before the headset tracks, or ask Evan | HANDOFF |

## 4. Plan fields to Studio fields

| Plan | Studio | Notes |
|------|--------|-------|
| shot `id` | `StudioCameraTrack.id` and `name` | fixed ids (F14) |
| `tape_in`, shot length, `speed` | `start`, `end` (tape seconds), `speeds` (`StudioSpeedMark`) | convert from frames |
| `camera.move` static, push_in, pull_out, pan, tilt, truck, crane | `keys` (`StudioCameraKey`: time, position, euler, focal, focus, aperture, shake, ease); `spline`; `constantSpeed`; pilot: `StudioMoves` helpers | positions are metres from the stage spot |
| `camera.ease` | key `ease`: 0 straight, 1 smooth (in_out), 2 slow start (in), 3 slow end (out), 4 hold then cut | `none` = 0 on a static shot |
| follow, ride, orbit, dolly_zoom, pov | `behavior`: `follow`, `ride`, `orbit`, `dollyZoom`, `firstPerson`; `subject`, `lag`, `aimOffset` | set `aimOffset` y to about 0.85 m (survivor eyes), not the default 1.1 m. `pov`: `behavior = "firstPerson"`, `subject` = the survivor's mark; keys only set start/end; `fpSmoothMove` 0.25, `fpSmoothTurn` 0.45, `fpLevel` true, `fpFieldOfView` 62, `fpOwnBody` true are the defaults (own arms, hands and weapon drawn for that lens only, head/hair/hat/upper torso shadow-only, no depth of field, near clip 4 cm); all off = old raw head lock. Shoot-tab inspector shows them. Limits and how-to: `camera.md` 1c |
| handheld, impact shake | `noise`: `subtle`, `walking`, `running`, `impact`; `noiseStrength`; per-key `shake`; pilot `StudioMoves.Shake` (rotation only) | check angles in the metrics |
| `focal_mm`, `aperture`, `focus` | key `focal`, `aperture`, `focus`; `focusOnSubject` | |
| sensor | `sensorHeight`, default **18.66 mm** | set `sensorHeight = 24` on every track |
| `subject` of kind block, point | pilot `StudioTargets` ids `block:`, `point:` | other commits: keyed camera |
| `action_cue` | a `StudioCue` (push, weaken, hazards, collapse); pilot `handPull` | dry-run it (F7) |
| cut list | `StudioCut` (camera, from, to, dissolve) | dissolves need a waiver |
| aspect | `StudioAspects`: 16:9, 9:16, 1:1, 2.39:1 | |
| render | `StudioRenderSettings`: width, height, fps, `subFrames`, `shutter`, format, `freezeAmbient` | |
| titles, music, every sound, loudness | not in the Studio | assembly step with ffmpeg |

## 5. A job plan that fits the limits (15 s, about 9 shots)

0. **Rehearsal and record jobs first** (`record-and-layer.md` section 3): record, report, re-time,
   record the take that counts, commit its bundle. Every job below loads that bundle and never records.
1. **Scout job:** load the map and committed take (or a test record), print the top's height
   profile, render scout stills or a `RunPreview`-style filmstrip for every camera, print subject
   area and occlusion, dry-run every pull and hazard cue. Check R01 and R02 before going on.
2. **Record job** (only if no committed take fits): record with the hand performed or directed;
   write the take log; commit the bundle.
3. **Preview job:** 640x360 render of all shots from the committed take; read the hand timeline and
   warnings for frozen actors.
4. **Render jobs:** 1080p in two or three parts by shot id; never more than about 6 minutes of
   expected render per job.
5. **Assembly (no Unity):** ffmpeg joins the shots by the cut list, lays in every sound, mixes
   music, burns in titles, normalises loudness; then the tools in `measured-reference.md` and the
   evidence pack.

## 6. Still missing (ranked by how much it would help an agent)

0. For agents acting against the tape: the gaps in `record-and-layer.md` section 6 (a `move` cue, a
   `hit` trigger for overlays, a Studio take report, `point:` targets for overlays, a `hide` overlay,
   keeping chosen actors live while performing). First person is done (`490c0bbb`, see `camera.md`
   1c); still missing there: a look-at that turns the body when the neck limit is reached, and a
   first-person own-body view for the giant hand.
1. For mayhem: an unarmed **punch / shove cue** for a chosen attacker and victim; a tsunami cue that can push
   survivors; real eliminations on the stage (deaths on, as an option); a chosen
   side for every disaster, not only the tornado.
2. Motion blur that keeps survivor colours: done (`NoSkinnedVectors`); playback colours that follow `StudioMark.color` (every body played back yellow in disaster-pvp-1).
3. Sound cues in the Studio, and a directed hand that is live while recording (so it makes game
   sound, events, dust and debris and touches the world); a front-loaded pull ease.
4. Repeatable world physics between takes, and directable bots (go here, hit that survivor).
5. A render that fails (or skips one frame) when an actor's frame is bad, instead of silently
   freezing the actor.
6. An automatic framing judge: `metrics.json` checked against the pass ranges; a live view or a
   persistent editor for a session of scouts.
7. Merge the pilot's hand fix (`ActorFrame.Blend`) and a Characters-side "replay copy" flag so the
   hand draws without the baked-mesh workaround.
8. A take report per take (F11) with every `GameEvents` hit, knock-off and splash; plan import (the
   lint runner now exists as `tools/lint.py`).
9. Export the cut as one MP4 with music lane, gains, fades and titles (F1-F3); `tools/assemble.py`
   does this outside the Studio today.
10. A music grid lane with measured hits and cut snapping; an automatic evidence pack per render.
11. Batch render of a saved take, stable ids, a repeatability report (F12-F14); resumable renders
    (F19); named stage anchors (F15); no ghost survivors (F9); ramp-following sound (F4).

## 7. The shipped tools (`tools/`, revision 3; `lint.py` revision 4)

Copied from hand-reveal-2 (`attempts/hand-reveal-2/tools/`) and generalised where it was small and
safe. All need Python 3; `assemble.py`, `music_map.py` need ffmpeg/ffprobe, numpy, soundfile and
librosa; `lint.py` needs PyYAML. Each prints its usage with `--help`. Run them with `python -I` when
their inputs come from somewhere you do not trust.

| Tool | What it does | Use it at | Limits |
|------|--------------|-----------|--------|
| `lint.py <plan.yaml>` | computes every plan lint (L01-L51; L47-L51 added in revision 4) and prints PASS / FAIL / WAIVED / N/A with numbers; exit 1 on an unwaived hard failure | stage 3, after every plan change, after the scout loop | cannot judge truth (labels, proofs, `fit` lines), R-lints, or plans that are not in the schema |
| `music_map.py <cue> <start_s> <length_s> <out> [--fps --lag]` | tempo, beats, onsets with strength and rise, candidate hits, the biggest hit, stop-downs, loudness; writes `music.yaml` and the excerpt | stage 2 | onsets on the full mix, not phrases; hits are candidates; check the downbeat yourself |
| `assemble.py <job> <out> <preview|final> <events.json> [--sound-root]` | cuts the picture by `shots.txt`, mixes music, take sound and every event at its frame (a clip's first attack placed on the frame), normalises to -15 LUFS / -1.5 dBTP, writes `cut.mp4`, `mix.wav`, filmstrip, contact sheet, stills and `metrics.txt` with a **two-column sync table (full mix, own lane)** and NEEDS EARS on voice lines | stages 7-10 | needs the driver's `shots.txt` format and per-shot mp4s with handles; no titles; does not judge lane balance |
| `pack.py <job> <asm> <pack> [--plant ...]` | the evidence pack from the job and the assembly, and a planted-defect copy (a sync line moved 5 frames, an impact removed, a still swapped) with `planted.txt` beside the packs | every critic round from stage 7 | no subject overlay, motion strip or audio plot (items 3, 4, 7) |
| `sound_events.example.json` | hand-reveal-2's real sound plan, as a format example | - | its music path and sound names are that clip's |

Tested on 2026-11-20 (no Unity): each tool's `--help`; `lint.py` on worked examples A-E (0 hard
failures) and on hand-reveal-2's `v08/plan.yaml` (as class `moment` its four waivers are no longer
needed; it fails the new L14 because it has no `reaction` field); `music_map.py`, `assemble.py`
(preview) and `pack.py --plant` end to end on a synthetic two-shot job with test-pattern video and
generated sounds, where a soft-attack "voice" read +9.4 frames in the full mix and -0.5 frames in its
own lane. Revision 4 (2026-11-20, no Unity): `lint.py` with L47-L51 on D, E and B as revised (0 hard
failures, no soft warnings) and on their revision-3 versions (each failed L47, L48 and L49, as it
should); A and C (legacy) print L47-L51 as soft warnings. Not tested: a `final` (1080p) assembly, real Studio job output, real game sound files, and
`pack.py` against a real metrics CSV.
