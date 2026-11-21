# Record once, then layer the actors (revision 4)

Open this before you write a driver, before stage 5 (scout) and before stage 6 (capture). It is the
working method of Trailer Studio, and it is required: SKILL.md hard rules 17 and 18, lints L47-L51.
How the actors move between and around the key events (intentions, glances, driven travel, the
alive layer, human performance) is in `performance.md` (rule 19, lints L52-L56, stage 6b).

Code read on 2026-11-20 from `claude/trailer-hand-reveal-pilot` (HEAD `9e099413`), folder
`Assets/_DeadlyJenga/DevTools/Editor/TrailerStudio/`. File references below are relative to it.
Nothing here was run for this revision; every "untested" is marked.

## 1. Why this exists

The owner, 2026-11-20: "arent they doing isolated iterative takes where first the do one real physics
event where they fully record the block movements and position of everything ... and then
subsequently every scene will follow what happened there exactly", and "why ... is [it] overly-reliant
on distant shots esp the super distant ones during important events".

What the agents did instead:
- **hand-reveal-2** rendered its two halves from two separate recordings (finals A and B). World
  physics does not repeat (9.3 cm apart by frame 35 in the pilot), so the halves drifted about 5 cm.
- **disaster-pvp-1** did record once and cut every shot against one committed bundle, which is right.
  But it placed the victim cameras from where each victim *stood before* the hit, 6-8 m out at 20-24 mm
  (`Attempts/DisasterPvpMoment.Shots.cs`, `Victim`), so the victims were 0.07-0.15 of frame height
  at the very frames the take report said they were hit.
- Both used brainless stand-ins and cues. Nobody looked at the rock that landed beside them, flinched
  on the frame it hit, or acted against anyone already recorded. The cast behaved like props.

The tape knows exactly where and when every hit happens. Distance is never an excuse.

## 2. What the Studio actually does (verified in code)

- **A take is a tape.** `StudioRun.RecordWholeSet()` (`Engine/StudioRunPass.cs:41-48`) records every
  unlocked track (world blocks, hazards, effects, sound events, every mark) with live physics on a
  fixed clock. Afterwards `StudioRun.Play`, `Scrub`, `ShowExact` and every render play the tape back
  with the blocks kinematic (`SetWorldKinematic`, line 254): no physics, same result every time.
- **Cues only run in a live-world pass** (`pendingCues` is filled only when `pass.liveWorld`,
  `StudioRunPass.cs:435`; `TrailerStudio.md`, "Good to know"): `RecordWholeSet`, a branch, a
  variation run, or a performance with Live physics.
- **Only live bodies touch each other.** A recorded actor is posed from the tape; a hit on it does not
  move it. "A played-back disaster looks and sounds right but does not push or hurt a live
  performer" (`TrailerStudio.md`). So **every actor who hits or is hit must be live in the same pass
  as the thing that hits them.**
- **Who is live in a pass** (`Engine/StudioCast.cs:43-79`): every mark in `pass.record` except the
  performer. A `Survivor` mark becomes a stand-in (no brain, no input: it stands on its mark and obeys
  gravity); a `Bot` mark runs the normal bot brain; the performer is the local player in that mark's
  body. Everything else plays back.
- **Performing** (`Engine/StudioPerform.cs`): "you play one actor while everything already recorded
  plays back around you". Options: `Actor`, `Countdown` (3-2-1), `PreRoll`, `Speed` (1, ½, ¼ while
  performing), `Loop`, `PunchIn` (keep the old take up to the playhead; `PunchBack` = back 3 s and
  punch in), `Paths` (floor lines of every other actor's path and your own last take,
  `Engine/StudioPaths.cs`, drawn only while someone performs), ghosts on or off. Keys in the Game view:
  F9 record, F10 again, F8 back 3 s and punch in, F7 slow motion.
- **Live physics while performing re-records the world**: `BuildPass` with `LivePhysics` records the
  performer plus the non-actor tracks from a fresh map (`StudioPerform.cs:56-63`). The other actors are
  not in `record`, so they play back and cannot be hit. The new world take makes older actor takes
  stale: `StudioTakes.MarkStale` badges each one with the frame where the two worlds first differ.
- **A branch** (`StudioPass.branchFrom`, `StudioRunPass.cs:344-357`) keeps the world tape before that
  frame exactly and re-simulates from the tape's pose and speed after it.
- **Overlays** (`Engine/StudioOverrides.cs`, `Data/StudioOverrideData.cs`) change only what is drawn,
  never the take, and can be muted. The first-person camera and every camera behavior read the actor
  *with* its overlays (`StudioActorPoses.At`, `Engine/StudioCameras.cs:33-40`), so a look-at overlay
  also turns that actor's first-person view.

## 3. Procedure A: record once, then direct against the tape

Write a driver in `Attempts/` on your own branch (`DisasterPvpMoment*.cs` is the working model) and
run every step through the coordinator.

1. **Set up one Set for the whole sequence.** Marks with `StudioSet.AddMark` (role `Survivor` for a
   stand-in, `Bot` for a brain), `customLook` and a skin per actor (section 5). Weapons by a `gear` cue
   at about 0.3 s, not `mark.equipment` (disaster-pvp-1's scout: weapons given at spawn were refused).
   Every action that touches anyone is a cue in this pass: `fire`, `glovePunch`, `aimedMeteor`,
   `hazard`, `push`, `collapse`, `weaken`, `guide` (section 6 lists them).
2. **Rehearse: record, report, re-time.** `StudioRun.RecordWholeSet()`; wait for `StudioState.Held`;
   `StudioRun.SaveTape()`; keep `StudioRun.CueLog` (it prints `REJECTED` and `FALLBACK` lines).
   Write the take report (step 4). Move cues to what the take did: an aimed meteor's `at` is its
   arrival minus `AimedMeteorCue.ArrivalDelay(cue)`; disaster-pvp-1 landed the planet killer 2 frames
   late in record 1 and sent it 2 frames earlier. Plan two or three rehearsals; disaster-pvp-1 needed four.
3. **Record the take that counts, and commit it.** Save the bundle as `DisasterPvpMoment.SaveBundle`
   does: `set.json` (`JsonUtility.ToJson(set)`), `main.djtape` (from `StudioLibrary.TapePath(set.id)`),
   `take.txt` (the world take's id, `tape.FindType(WorldTrack.Id).ActiveTake.id`) and `tape.wav`
   (`StudioRender.AudioFile(set.id)`). Commit it to your branch (about 7 MB in disaster-pvp-1) and set
   `capture.take: <bundle path>`, `capture.take_status: committed` in the plan.
4. **Inspect what happened: the take report.** The Studio has no report of its own; copy
   `DisasterPvpMoment.Report` (`Attempts/DisasterPvpMoment.Scout.cs:42-120`) and extend it:
   - per actor every 3 frames: visible, feet x/y/z (`StudioActorPoses.At(tape, id, t, fps)`), health and
     equipment (`ActorFrame.Read` on the track's `ActiveTake`), to `take-actors.csv`;
   - per actor: the frame it left its spot (dropped over 2.5 m or moved over 3 m), how far it fell;
     every **health drop** (who was hurt, on which frame: the hit), every **respawn** (a jump back up);
   - every impact from the hazard track (`DisasterFrame` points with `residualOnly`): frame, kind, place,
     size, nearest actor and distance;
   - blocks moved more than 0.3 m every 15 frames (`WorldTrack.Positions`), and any named block's travel;
   - the cue log.
5. **Scout the take and re-plan.** Cut the story to what the take did, not to what the plan hoped
   (disaster-pvp-1: the hero's summit was undermined in all four records, so the cut became his 38 m
   tumble). Write `key_events` in the plan, each with the take frame, who, to whom and where. Re-lint
   and bump the plan version (stage 5b).
6. **Place every camera from the take's known positions and times.** For each key event read the
   victim's (or impact's) position *at the event frame* from the tape, not the mark. Choose the size
   first (`h ≥ 0.25` for a key event, `camera.md` section 1d), then `d = (0.94 / h) x focal / 24`.
   Aim with `StudioTargets` (`actor:`, `head:`, `block:`, `point:`). Add every subject to
   `StudioCameraTrack.measure` (`"actor:<id>"`) so the render writes `metrics.csv` with each subject's
   height share per frame (`Engine/StudioFraming.cs`); copy that number into the shot's `take_ref`
   with `source: metrics`.
7. **Every later job loads the bundle and never records.** `DisasterPvpMoment.LoadBundle`: copy
   `main.djtape` to `StudioLibrary.TapePath(set.id)` and `tape.wav` to
   `StudioRender.TakeAudioFile(setId, takeId)`, then `StudioRun.OpenStage(set)`, add overlays,
   `StudioOverrides.Resolve(set, tape, anchor)`, and `StudioRender.Start(cameras, settings, folder)`.
   Scouts, previews and finals all do this. **Give each job mode its own output folder**: in
   disaster-pvp-1 final C deleted final B's output on the same slot until each mode got
   `Logs/<attempt>/<mode>/`.
8. **Never re-record between shots of one sequence**, and never render two halves of one moment from
   two takes.

**When to re-record**, and only then: a key event did not happen or happened wrong (R02 fails); a cue
or a mark must change; a new live actor must touch the world or another actor. **What it
invalidates:** every camera placed from the old positions; every `take_ref`; every overlay whose time
you read off the old report (trigger-based overlays re-resolve with `StudioOverrides.Resolve`, fixed
times do not); every render; the take's sound (`tape.wav` belongs to one world take); every actor take
performed against the old world (`MarkStale` flags them). **Prefer a branch** (`branchFrom` = the first
frame that must change): only what follows it is invalid. After any re-record: report, re-place,
re-lint, new plan version.

## 4. Procedure B: layered actors (the pass order)

**The order:** the world event first; then the actors who cause things (attackers, and the hero's
own moves when they change the world); then the actors who receive them (victims); then witnesses
(who look, flinch, cower); then the crowd. Each later layer is timed from everything before it, so
it reacts to what really happened. Contact decides what goes in one pass: anyone who hits or is hit
is live in the same pass as the thing that hits them (section 2).

### 4a. With a person performing

1. Rehearsal world pass with stand-ins on every mark; read its take report.
2. The performer whose action hits someone performs it with every touched actor live. The Perform
   tab's Live physics leaves the others as playback, so build the pass in a driver: a `StudioPass`
   with `performer = <mark>`, `liveWorld = true`, `fixedClock = false` and every track id in `record`,
   passed to `StudioRun.Record(pass)` (supported by `StudioCast.SpawnLive`; **untested**). In the headset
   the clock is always real time (`MustBeRealTime`).
3. Witnesses perform one at a time without Live physics: they see everyone already recorded and the
   floor paths, so they know where to look and when. Fix a moment with punch-in (F8) instead of a whole
   new take; perform hard moves at ½ or ¼ speed.
4. Overlays on top (4b, step 4).

### 4b. An agent with no person (what it can do today)

1. **One live pass holds every contact**: hits by `fire` / `glovePunch`, meteors by `aimedMeteor`, the
   disaster by `hazard`. Victims and hitters are stand-ins (bots wander off: in disaster-pvp-1 both bots
   walked off the tower within 1.3 s). Time each cue from the rehearsal report (Procedure A, step 2).
2. **Commit the take; write the event table** from its report: event id, tape frame, attacker, victim,
   place. Every later layer is timed from this table.
3. **Fix an actor's timing without re-recording** (`Engine/StudioMotionEdit.cs`, each makes a new take
   and keeps the old): `Slide`, `Retime` (flags "no longer lines up with the world" after the range),
   `Trim`, `Smooth`, `Offset`, `Bend`, `BlendTakes`, `LockFeet`; and `StudioTakes.Comp`, `Clone`,
   `Crowd` for more bodies from one take. Never slide an actor who touches the world at a key event.
4. **Overlays, in the pass order, every time read from the event table:**
   - attacker: `lookAt` `actor:<victim>` from about 1 s before the hit; `reach` to the victim 6 frames
     either side of the hit frame;
   - victim: `reaction` `knockback`, `stagger` or `tumble` starting on the hit frame (set `point` to the
     push direction, or use `trigger: "contact"`), then `scream`; `fall` or `death` hold until a `getUp`;
   - witnesses: `lookAt` the victim, the falling `block:<index>` or `hazard` from the frame it first
     shows; `pose` `flinch` on the impact frame; `cower` while it lasts. Look at the rock that actually
     lands, and flinch on the frame it hits;
   - the hero: the reaction that carries rule 9 (`scream`, `cheer`, `cower`), plus `lookAt` so the head
     points at the threat (her first-person view turns with it);
   - triggers: `trigger: "contact"` starts on the first frame a recorded block moving over 1.5 m/s
     comes within 1 m of the actor's chest, and sets the push direction; `trigger: "cue:<cue id>"` starts
     at the cue's time, or for a `hazard`/`tsunami` cue when the disaster's centre first comes within
     `max(4, strength x 8)` m. Run `StudioOverrides.Resolve(set, tape, anchor)` after loading the take.
5. **Check the layers in motion**: render a dense strip around every key event (`review-rubric.md`
   item 11) and read each actor's `facing` in `metrics.csv`: a witness's head must turn to the event.
6. **Never move the world to suit a layer.** Move the layer.

Write the order in the plan as `layers` (lint L51): the world first, then one entry per cast member
with what it does and which events it reacts to. A cast member with no entry is a prop.

Layers handle the key events. Everything between them (travel under the actor's own power, glances,
business, the alive layer filling every gap) is the performance pass, stage 6b: `performance.md`
section 12. A reaction layer that ends with the body standing frozen fails L53.

**Can a later scripted actor be timed against an earlier recorded one?** Yes, as an overlay: targets
(`actor:`, `block:`, `hazard`) and triggers read the committed tape. A cue in a *later* live pass can
also aim at a played-back actor (`FireCue` looks in `stage.playback`, `Engine/StudioDirector.cs:251`),
but that actor will not react (it is playback), and the later live pass re-simulates the world from a
fresh map unless it is a branch. So: contact in one pass, reactions as overlays.

## 5. Stage facts (disaster-pvp-1, `attempts/disaster-pvp-1/REPORT.md`)

| Fact | Do this |
|------|---------|
| Deaths are off on the stage (`StudioRun.cs:223`): a survivor who falls into the sea respawns on the tower (the punched rival reappeared beside the hero two shots later) | find each respawn in the take report; keep the respawn point out of later frames, or end that actor's shots before it; there is no hide overlay (gap 8) |
| Every body played back in the same yellow; a colour override had no effect (cause not traced) | give each actor a skin (`AvatarAppearance.skin`: Gorilla, Penguin, Dragon...) and the hero a plain body; check a full-size still |
| Left-over uzi and blaster impact views ("Visual - plasma impact radius", "Visual - steaming neon spike") drew as a big green disc in loaded takes | hide them on `StudioRender.BeforeFrame`, as `DisasterPvpMoment.HideStrays` does |
| The rocket launcher's 8 m blast threw four survivors at once, the hero included | keep a rocket's impact 9 m or more from anyone who must stay; or drop the rocket |
| Block visuals are on the Default layer | include layer 0 in line-of-sight and camera-clearance masks (`(1 << Layers.Block) \| (1 << Layers.Surface) \| 1`) |
| The four top blocks tie in height within millimetres, so "the highest block" changed between takes (#564 vs #567) | pick the summit by a fixed rule: among blocks within 5 cm of the top, the largest x + z (`FindArena`) |
| Aimed rocks threw blocks that undermined the summit; the hero was knocked off in all four records | plan around the physics you get; keep every rock's path 4 m or more from the hero's head |
| Bots walked off the tower within 1.3 s | stand-ins, not bots, for anyone who must stay put |
| Motion blur on played-back bodies smeared them (ghosting) | fixed: `StudioRender.NoSkinnedVectors` (`Engine/StudioRender.cs:201, 276`) on blurred shots; still use blur 0 on shots with 45 m/s rocks near the lens |
| An override aimed at `target: "point:x,y,z"` does nothing: `StudioOverrides.Target` knows only `""`, `actor:`, `block:`, `camera:` and `hazard` (disaster-pvp-1's "everyone looks up at the planet killer" layers used `point:` and were silently ignored) | leave `target` empty and set `point` (metres from the stage spot); or aim at `hazard` |
| One render job deleted another's output on the same slot | one output folder per job mode |

## 6. What an agent can cue, and the gaps

Cues (all need a live-world pass): `gear` (weapon to a live actor or everyone), `fire` (a live
shooter at an actor, live or playback, or a point; `optional`), `glovePunch` (live actor at live
target: the real glove through `fire`, else a push of 22 m/s along the punch plus 6 m/s up and 80
damage, logged as FALLBACK; `Engine/StudioGlovePunchCue.cs`), `aimedMeteor` (a meteor added to the
game's meteor disaster at a point, direction, distance, speed and size; the cue time is when it
appears; `Engine/StudioAimedMeteorCue.cs`), `hazard` (any disaster with its own `values`, `duration`,
and `aimed` + `heading` where the disaster allows), `tsunami` (never pushes survivors), `stopHazards`,
`push`, `collapse`, `weaken`, `guide`, `slowTime` (only while performing in real time), and chains
(`link`, `linkOnHit`).

What a person can do that an agent cannot, and the smallest feature that would close each gap:

| # | Gap | Smallest feature |
|---|-----|------------------|
| 1 | Walk, run, jump, climb or dodge on purpose: stand-ins only stand; bots wander | a `move` cue: actor, path points, speed, jump at points; drives the survivor's `InputDriver` for its length. **Being built** (revision 5): a scripted performer driver, `performance.md` tool how-to |
| 2 | An unarmed punch or shove on cue (`glovePunch` needs the glove; otherwise a raw push) | a `punch` cue that sends the mode's own punch command for a live actor at a target |
| 3 | A victim layer that starts on the frame the hit landed, found from the tape | a `trigger: "hit"` for overlays, resolved from the recorded health drop (`ActorFrame.health` is on the tape) |
| 4 | Reactions to projectiles, meteors and other actors (`contact` sees blocks only) | extend `StudioOverrides.Resolve` to hazard points and actor positions |
| 5 | Know what happened without writing a report | `StudioTakeReport.Write(stage, folder)`: `DisasterPvpMoment.Report` plus health drops, respawns and the cue log |
| 6 | Aim an overlay at a fixed point by `point:` | accept `point:x,y,z` in `StudioOverrides.Target` (`StudioTargets.TryPoint` already parses it). **Being built** (revision 5) |
| 7 | Perform against playback in real time (timing by eye, natural head and hands) | a scripted input driver for the performer rig (keyed head, hands and movement), so an agent can "perform" a mark. **Being built** (revision 5), with an always-on alive layer and body-follows-head: `performance.md` |
| 8 | Keep a knocked-off survivor gone (deaths are off, so they respawn) | an overlay kind `hide` from a tape time, or a Set option "no respawn" |
| 9 | Perform with Live physics while chosen others stay live (to hit them) | a Perform-tab option "keep these actors live", adding their tracks to `BuildPass` |
| 10 | Hold the first-person camera steady and show your own hands and weapon | **first-person camera fix in progress, see commit note** (another agent, pilot branch) |
| 11 | Tell actors apart by colour in playback | trace why playback ignores the colour (unknown cause) |
