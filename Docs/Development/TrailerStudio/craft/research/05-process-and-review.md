# 05: Production process and review

Research topic 5 of 5 for the Trailer Studio craft playbook. It covers how professionals go from
brief to finished trailer, and how an AI agent that cannot watch motion or hear sound can run
that process and check its own work.

Written 2026-11-19 from web research and the attempt-1 record
(`Docs/Development/TrailerStudio/attempts/attempt-1/FINDINGS.md`, findings F1-F21), the Studio
handoff (`progress/HANDOFF.md`, `progress/AGENT-FINDINGS.md`) and the attempt-1 driver
(`Assets/_DeadlyJenga/DevTools/Editor/TrailerStudio/Attempts/TrailerAttempt1.cs`). Where I
reason from these sources rather than report them, the text says **(inference)**.

What attempt 1 shows for this topic (my reading of FINDINGS.md):
- The concept and a six-shot list existed, but the list had no timing grid, no stated purpose for
  each shot, and nothing that could be checked before a render. Every framing question was found
  out by a 5-14 minute render (F10). The key beat, the giant hand, never appeared (F5, F6). The
  hero faced away (F16). The collapse had no single readable impact (F17).
- The agent's best tool was a take report it wrote itself (F11). Its review was contact sheets
  every 0.5 s plus loudness numbers. It had no listening (F18) and no second opinion.
- The plan was changed after each render by the same agent that made it. Nothing outside the agent
  said when the clip was good enough or when to stop.

The design below adds gates before rendering (a checkable plan, lints, a cheap animatic and a
feasibility scout), puts the evidence in a form the agent can read, and adds a separate critic
whose scores need evidence.

---

## 1. Pipeline

### 1.1 How professionals do it (summary of sources)

- **Brief.** Agencies and freelance editors start from a one-page brief: goal, audience, release
  date, game overview, mood, must-show features, and the client's assets. The editor also gets
  cheats and debug commands: hide the HUD, free the camera, god mode, music off (Game Developer,
  "What assets to provide"). The aim is a document a non-player can work from. One capture
  director writes a "What Does It Feel Like to Play" note for teammates (Aftermath).
- **Concept and script.** Writers draft several scripts. The client picks one. The capture
  director corrects each script for what the build can actually show (Aftermath). Story-heavy
  trailers get a storyboard. Simpler ones may go straight to a text outline (Game Developer).
- **Assembly, with placeholders.** Derek Lieu starts a timeline much longer than the final cut. He
  blocks it with text cards for each beat, temp music, the mandatory title and logo time, and any
  public footage as a placeholder ("Basic Game Trailer Editing Workflow"). Next he makes a
  "garbage cut": the cards are replaced with any game clips at all. The aim is to learn how long
  each action really takes, what is missing or repeated, and whether the clips teach the mechanic
  ("Garbage Cut"). This is the game-trailer version of an animatic.
- **Capture.** Lieu treats capture as acting, directing and camera operation, not just playing
  well (GDC 2018, "Capturing Great Footage"). A capture director builds the shot list from the
  chosen script and finds the debug tools. Capture is solo or with a second player as performer.
  Footage goes to editorial every day, and the director recaptures after notes and after build
  changes until picture lock (Aftermath).
- **Rough cut, then fine cut.** In the rough cut, real capture replaces the placeholders, card
  timing is fixed to the real action, the order is checked so that early sections supply what
  later ones need, and a first music draft goes in. In the fine cut, shot order, dialogue and
  pacing are locked, final music goes in, cuts are checked against the beats, motion-graphics
  titles are added, and capture is refined (choreography, framing, HUD, bugs). Lieu says this is
  the stage with the most client feedback ("Basic Game Trailer Editing Workflow").
- **Finishing ("online").** Sound mix, sometimes a colour grade, and platform versions with their
  own logos, legal lines and encodes (Lieu). In film, picture lock is the point after which
  editorial changes stop and sound, colour and VFX start, because late changes are costly
  (Frame.io workflow guide).
- **Review cadence.** Agencies plan a fixed number of revision rounds (two to three is a common
  figure in agency pages I saw only in search snippets). They name one person to consolidate
  notes and agree turnaround times per milestone. Lieu asks for consolidated notes, makes them
  frame-accurate, works them from the end of the timeline backwards so earlier changes do not
  shift later timecodes, and keeps every version sent to the client ("Professional video editing
  tidbits").
- **Previs in film.** Previs tests staging, camera placement, movement and editing before money is
  spent. Its fidelity ranges from grey shapes to game-like images (Wikipedia, "Previsualization").
  The Third Floor separates pitchvis (sell the idea), previs (explore action and angles, a
  stress test), techvis (how it can physically be shot: lens, camera distance, speeds) and
  postvis (rough composite of real footage to preview the cut). StudioBinder points out that a
  word-only shot list is the most common form of previs, and that full animatics cost a lot of
  work in live action. Rough is acceptable as long as it answers the question asked.

### 1.2 The pipeline for an AI in Trailer Studio

Each stage produces a file, and the gate question must be answered "yes" with evidence before the
next stage starts. Stages 1-5 cost no render. Stage 5 needs only stills.

| # | Stage | Artifact (file) | Acceptance question | Checked by |
|---|-------|-----------------|---------------------|------------|
| 0 | Brief | `brief.yaml`: goal, audience, length, aspect, must-show list, tone words, the one sentence a viewer should repeat, map, constraints (render budget, hand performed or bot) | Could a stranger write the logline and the must-show list from this alone? | Critic reads it back as a two-line summary. The maker confirms or fixes. |
| 1 | Concept | `concept.md`: logline, the hook (what makes this game different, per Lieu's hook versus anchor), the emotional arc in three words, the end title text, 2-3 alternative concepts with one reason each was rejected | Is the hook something only this game has, and does it appear in the first 2 s? | Critic, plus lint L06 once the beat sheet exists |
| 2 | Music and grid | `music.yaml`: track, BPM, downbeat offset, bar map, sections, named hits (main hit, title hit, button) with times measured from the file | Are all hits measured from the audio (onset list), not guessed? | DSP: beat and onset detection, waveform plot |
| 3 | Beat sheet and shot list | `plan.yaml` (schema in section 2) | Does the plan pass every hard lint? | Lint runner (section 3), no Unity needed |
| 4 | Text animatic | `animatic.mp4` plus `animatic-sheet.png`: one card per shot (shot id, size, subject, the "learns" line) timed to the music, with the music. Lieu's "beat as text cards". | Read in order, do the cards tell the story, and do the cuts land on the hits? | Critic (story, rhythm). L01-L05 confirmed on the rendered file by shot-boundary detection. |
| 5 | Techvis scout | `scout/`: for each shot, stills at its first, key and last tape times from the planned camera on a grey or low-quality pass; subject visibility and occlusion numbers; a dry run of each physics cue (does the push move the block, how far) | Is each subject visible and unblocked at its key moment, and does each cue do what the plan needs? | Numbers from the engine (visibility %, occlusion %, block displacement), then the critic on the stills |
| 6 | Capture | committed take bundle plus `take-report.json` (events, block moves, actor heights, falls, hazard and hand positions, cue results) | Did every planned action cue happen within its window, and did the hero and the hand do what the plan says? | Plan-to-take check: every `action_cue` matched to a take event ±0.25 s |
| 7 | Garbage cut / preview | 640x360 render of every shot, cut together with music; evidence pack (section 4) | Does every shot show its subject doing its action, readable at preview size? | Evidence pack, then critic round 1 |
| 8 | Rough cut | full cut at preview size with sound and temp titles | Critic rubric: no category at 2 or below | Critic rounds (section 5) |
| 9 | Fine cut / lock | the locked plan (`plan.yaml` tagged `locked`) and a 1080p render | Weighted score ≥ 4.0, no hard lint failing, change since last round below the stop threshold | Critic plus lints. After lock, no shot changes, only sound, titles and grade. |
| 10 | Finish | `trailer.mp4` (picture, mix, titles), loudness report, final evidence pack | Loudness and true peak in range; titles readable; no technical defects | DSP plus critic's technical category |
| 11 | Human pass | Evan's (or a teammate's) notes | Does it sound right? Nothing in this pipeline can hear (F18). | A person. Required before anything is called finished. |

Rules that apply to the whole pipeline:
- **Plan first, then sound.** Lieu tells editors "You should do audio first" (PC Gamer
  interview). He builds on temp music from assembly onward. For an agent the music is also the
  only clock it can check, so stage 2 comes before the shot list **(inference)**.
- **Go back one gate, not to the start.** A critic note names the stage it belongs to. "Wrong
  hook" returns to stage 1. "Subject blocked" returns to stage 5. "Cut lands 3 frames late"
  returns to stage 8.
- **Versioning.** Every round is a new numbered folder (`v01`, `v02`...) holding the plan, the
  evidence and the critic report. Nothing is overwritten. This is Lieu's practice of keeping every
  version sent to a client. It also lets the critic compare versions pairwise.
- **Lock.** After stage 9, camera and cut edits are refused unless a hard lint fails. This is
  picture lock. It stops the agent from re-cutting forever.

---

## 2. Shot-list schema

### 2.1 Design choices

- **Frames are the true unit, and beats are the plan unit.** Every time is stored as an integer
  frame at the Set's fps, and every cut also carries a beat label (`bar.beat` or
  `bar.beat+sub/4`). Seconds are derived. A lint checks that frame and beat agree.
- **Cut time and tape time are separate.** `cut_in` and `cut_out` are positions in the trailer.
  `tape_in` and `speed` say which part of the recorded take is shown. A speed ramp makes them
  differ, and attempt 1's slow-motion shot was exactly where sound went wrong (F4).
- **One subject, one idea per shot.** `subject` is a single target. `learns` and `feels` are short
  sentences. This comes from Lieu's clarity priority and from Murch's ranking: emotion first, then
  story, then rhythm, then eye trace.
- **Fields map onto the Studio.** `camera` maps to `StudioCameraTrack` (behaviour, keys, focal,
  aperture, focus, speeds). `action_cue` maps to a `StudioCue` id. `cut` maps to `StudioCut`
  (dissolve). Fields the Studio cannot honour yet are marked `needs:` with the feature from
  section 7. The lint runner reports those as blocked, not as silently ignored.
- **Every shot names its evidence**, so the critic knows what to look for.

### 2.2 Fields

Trailer header:

| Field | Type / unit | Allowed values / notes |
|-------|-------------|------------------------|
| `id`, `version` | string, int | |
| `length_frames`, `fps` | int | fps from the Set: 24, 25, 30, 50, 60 |
| `aspect` | enum | `16:9`, `9:16`, `1:1`, `2.39:1` (StudioAspects) |
| `music` | object | `file`, `bpm` (float), `meter` (4), `downbeat_frame` (int, first downbeat), `sections` [{name, from_bar, to_bar}], `hits` [{name: main / title / button / accent, frame, bar_beat, measured_onset_frame}] |
| `hook` | string | the one distinctive thing (Lieu: hook, not anchor) |
| `must_show` | list of ids | from the brief; every id must appear in some shot's `shows` |
| `hero` | actor mark id | the human the audience follows |
| `titles` | list | {text, frame_in, frame_out, style: card / over_picture, position} |

Per shot:

| Field | Type / unit | Allowed values / notes |
|-------|-------------|------------------------|
| `id` | string | `S01`...; also the fixed camera id (F14) |
| `section` | enum | `hook`, `setup`, `build`, `breath`, `climax`, `aftermath`, `title` |
| `cut_in`, `cut_out` | frame (int) + `beat` label | `cut_out` of shot n = `cut_in` of shot n+1 |
| `tape_in` | frame in the take | where the shown action starts in the recording |
| `speed` | list of {at_frame (shot-relative), speed} | 1 = real time. Maps to `StudioSpeedMark`. |
| `subject` | {kind, id} | kind: `actor`, `block`, `blocks`, `hazard`, `hand`, `point`, `tower`. Exactly one. |
| `also_in_frame` | list | context only; the shot is not about them |
| `shows` | list of must_show ids | |
| `learns` | string, ≤ 12 words | what the viewer learns. One idea, no "and". |
| `feels` | one word or short phrase | target emotion |
| `size` | enum, ordered | `EWS` < `WS` < `FS` < `MS` < `MCU` < `CU` < `ECU`; plus `INSERT` (object close-up, counts as CU) |
| `angle` | enum | `eye`, `low`, `high`, `overhead`, `ground`, `dutch` |
| `cam_height_m` | float | metres above the subject's feet (or the object) |
| `side` | enum + yaw | `front`, `3q_front`, `profile`, `3q_back`, `back`; `yaw_deg` relative to the subject's facing |
| `move` | enum | `static`, `push_in`, `pull_out`, `pan`, `tilt`, `track`, `crane_up`, `crane_down`, `orbit`, `follow`, `ride`, `handheld`, `dolly_zoom` |
| `move_params` | object | `distance_m`, `speed_mps` or `deg_per_s`, `ease` (Studio ease 0-4), `noise` (subtle / walking / running / impact), `lag_s` |
| `lens` | object | `focal_mm`, `aperture` (f-number), `focus` (`subject` or metres) |
| `frame_at_key` | object | subject position on a 3x3 grid (`TL` ... `BR`), `min_area_pct` (subject's share of the frame), `max_occlusion_pct` |
| `action_cue` | object | `cue_id` (StudioCue), `tape_frame` (when it happens), `key_frame` (shot-relative frame where the action peaks) |
| `transition_in` | enum | `cut`, `match_cut`, `smash_cut`, `dissolve` (frames), `whip`, `cut_to_black` |
| `connects_from_prev` | object | `type`: `contrast_size`, `match_motion`, `match_shape`, `eye_trace`, `reaction`, `cause_effect`, `new_beat`; `screen_direction`: `L>R`, `R>L`, `up`, `down`, `toward`, `away`, `none`; `eye_exit` and `eye_enter` grid cells |
| `sound` | list | {kind: `music_hit`, `sfx`, `ambience`, `silence`, `vo`; `name`; `frame` (shot-relative); `source`: `take`, `library`, `designed`; `gain_db`} |
| `hook_count` | int or null | which appearance of the hook this is (1, 2, 3) |
| `evidence` | list | what the reviewer must see: `still@key`, `strip`, `overlay`, `flow`, `audio@key` |
| `risk`, `fallback` | string | what may fail and what to shoot instead |
| `needs` | list of feature ids | Studio features this shot requires that do not exist yet (section 7) |

### 2.3 Example: 15 s giant-hand reveal

Music: 120 BPM in 4/4, so one beat is 0.5 s (15 frames at 30 fps) and one bar is 2 s. Bars are
numbered from 1 at frame 0. The main hit is at 10.0 s (bar 6, beat 1, frame 300). The title hit
is at 12.0 s (bar 7.1). The button is at 14.0 s (bar 8.1). Shot lengths shorten through the build
(1.5, 1.5, 1.5, 1.5, 1.0, 1.0, 0.5, 0.5 s). One flagged 1.0 s breath comes before the hit, then a
2 s reveal and a 3 s title. The hand appears three times, two hints and then the full reveal (the
rule of threes in Lieu's article of that name).

```yaml
id: hand-reveal-15
version: 1
fps: 30
length_frames: 450            # 15.000 s
aspect: "16:9"
hook: "A giant hand pulls the blocks people are standing on"
hero: dev                     # actor mark id; performed in VR, not a bot (F16)
must_show: [hand, tower_over_ocean, human_on_top, block_pulled, fall]
music:
  file: Music/trailer_120bpm_cue.wav
  bpm: 120
  meter: 4
  downbeat_frame: 0
  sections:
    - {name: hook,  from_bar: 1, to_bar: 1}
    - {name: setup, from_bar: 1, to_bar: 3}
    - {name: build, from_bar: 4, to_bar: 5}
    - {name: drop,  from_bar: 6, to_bar: 6}
    - {name: tail,  from_bar: 7, to_bar: 8}
  hits:
    - {name: open_stab, frame: 0,   bar_beat: "1.1", measured_onset_frame: null}   # filled by stage 2
    - {name: main,      frame: 300, bar_beat: "6.1", measured_onset_frame: null}
    - {name: title,     frame: 360, bar_beat: "7.1", measured_onset_frame: null}
    - {name: button,    frame: 420, bar_beat: "8.1", measured_onset_frame: null}
titles:
  - {text: "DEADLY JENGA", frame_in: 360, frame_out: 450, style: over_picture, position: center}
  - {text: "Every block counts.", frame_in: 390, frame_out: 450, style: over_picture, position: lower_third}

shots:
  - id: S01
    section: hook
    cut_in: {frame: 0, beat: "1.1"}
    cut_out: {frame: 45, beat: "1.4"}
    tape_in: 120
    speed: [{at_frame: 0, speed: 1}]
    subject: {kind: block, id: "block:212"}
    also_in_frame: [hand_fingertips]
    shows: [block_pulled, hand]
    learns: "Something huge is pulling blocks out of the tower"
    feels: jolt
    size: INSERT
    angle: eye
    cam_height_m: 0.1
    side: {face: profile, yaw_deg: 90}
    move: static
    move_params: {noise: impact, ease: 4}
    lens: {focal_mm: 50, aperture: 2.8, focus: subject}
    frame_at_key: {grid: MC, min_area_pct: 12, max_occlusion_pct: 10}
    action_cue: {cue_id: hand_pull_1, tape_frame: 135, key_frame: 15}   # the yank peaks 0.5 s in
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: R>L, eye_exit: ML, eye_enter: null}
    sound:
      - {kind: music_hit, name: open_stab, frame: 0, source: library, gain_db: 0}
      - {kind: sfx, name: wood_scrape_long, frame: 13, source: designed, gain_db: -3}
      - {kind: sfx, name: tower_creak, frame: 30, source: take, gain_db: -6}
    hook_count: 1
    evidence: [still@key, strip, overlay]
    risk: "Fingertips may read as brown squares (F5)"
    fallback: "Block alone shooting out of frame, motion blur on, scrape SFX carries it"
    needs: [F-HAND-PATH, F-CAM-TARGET-BLOCK, F-HAND-DRAWN]

  - id: S02
    section: setup
    cut_in: {frame: 45, beat: "1.4"}
    cut_out: {frame: 90, beat: "2.3"}
    tape_in: 150
    speed: [{at_frame: 0, speed: 1}]
    subject: {kind: tower, id: spire}
    also_in_frame: [survivors_on_top, ocean, islands]
    shows: [tower_over_ocean]
    learns: "A candy tower stands alone over the ocean"
    feels: scale
    size: EWS
    angle: low
    cam_height_m: 3
    side: {face: 3q_front, yaw_deg: 35}
    move: push_in
    move_params: {distance_m: 2.0, speed_mps: 1.33, ease: 1}
    lens: {focal_mm: 18, aperture: 11, focus: subject}
    frame_at_key: {grid: MC, min_area_pct: 25, max_occlusion_pct: 0}   # whole spire, top inside frame (attempt 1 cut it off)
    action_cue: {cue_id: tower_sway, tape_frame: 165, key_frame: 20}
    transition_in: cut
    connects_from_prev: {type: contrast_size, screen_direction: none, eye_exit: MC, eye_enter: MC}
    sound:
      - {kind: ambience, name: ocean_wind, frame: 0, source: take, gain_db: -12}
      - {kind: sfx, name: deep_tower_groan, frame: 20, source: designed, gain_db: -4}
    hook_count: null
    evidence: [still@first, still@last, strip]
    risk: "Crown not in frame if the stage anchor is 4 m low (F15)"
    fallback: "Measure the crown height from the take report and frame from that"
    needs: []

  - id: S03
    section: setup
    cut_in: {frame: 90, beat: "2.3"}
    cut_out: {frame: 135, beat: "3.2"}
    tape_in: 195
    speed: [{at_frame: 0, speed: 1}]
    subject: {kind: actor, id: dev}
    also_in_frame: [sky]
    shows: [human_on_top]
    learns: "A person stands on the very top"
    feels: unease
    size: MS
    angle: low
    cam_height_m: -0.8
    side: {face: 3q_front, yaw_deg: 30}     # face visible (F16)
    move: push_in
    move_params: {distance_m: 0.5, speed_mps: 0.33, ease: 1}
    lens: {focal_mm: 35, aperture: 2.8, focus: subject}
    frame_at_key: {grid: MR, min_area_pct: 15, max_occlusion_pct: 10}
    action_cue: {cue_id: dev_looks_up, tape_frame: 210, key_frame: 15}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: up, eye_exit: MC, eye_enter: MR}
    sound:
      - {kind: sfx, name: cloth_shift, frame: 12, source: designed, gain_db: -10}
      - {kind: music_hit, name: accent_low, frame: 0, source: library, gain_db: -6}
    hook_count: null
    evidence: [still@key, overlay]
    risk: "Block in the foreground hides the subject (attempt 1 shot B)"
    fallback: "Use the scout's clearest of 8 yaw angles around the subject"
    needs: [F-PREVIEW-STILLS]

  - id: S04
    section: setup
    cut_in: {frame: 135, beat: "3.2"}
    cut_out: {frame: 180, beat: "4.1"}
    tape_in: 240
    speed: [{at_frame: 0, speed: 1}]
    subject: {kind: actor, id: dev}
    also_in_frame: [other_survivors, shadow]
    shows: []
    learns: "The whole top lurches under them"
    feels: dread
    size: FS
    angle: high
    cam_height_m: 4
    side: {face: 3q_back, yaw_deg: 150}
    move: crane_down
    move_params: {distance_m: 1.5, speed_mps: 1.0, ease: 3}
    lens: {focal_mm: 28, aperture: 5.6, focus: subject}
    frame_at_key: {grid: BC, min_area_pct: 6, max_occlusion_pct: 20}
    action_cue: {cue_id: top_lurch, tape_frame: 255, key_frame: 15}
    transition_in: cut
    connects_from_prev: {type: reaction, screen_direction: none, eye_exit: MR, eye_enter: BC}
    sound:
      - {kind: sfx, name: blocks_grind, frame: 15, source: take, gain_db: -3}
      - {kind: sfx, name: survivor_gasp, frame: 18, source: designed, gain_db: -8}
    hook_count: null
    evidence: [strip, flow]
    risk: "Shadow of the hand depends on sun direction; not guaranteed"
    fallback: "Drop the shadow; the lurch alone carries the beat"
    needs: []

  - id: S05
    section: build
    cut_in: {frame: 180, beat: "4.1"}
    cut_out: {frame: 210, beat: "4.3"}
    tape_in: 285
    speed: [{at_frame: 0, speed: 1}]
    subject: {kind: block, id: "block:231"}
    also_in_frame: [dev_feet]
    shows: [block_pulled]
    learns: "The block under their feet is sliding out"
    feels: panic
    size: INSERT
    angle: ground
    cam_height_m: 0.2
    side: {face: profile, yaw_deg: 90}
    move: follow
    move_params: {lag_s: 0.1, noise: subtle}
    lens: {focal_mm: 40, aperture: 2.8, focus: subject}
    frame_at_key: {grid: MC, min_area_pct: 15, max_occlusion_pct: 10}
    action_cue: {cue_id: hand_pull_2, tape_frame: 295, key_frame: 10}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: R>L, eye_exit: BC, eye_enter: MC}
    sound:
      - {kind: sfx, name: wood_scrape_short, frame: 8, source: designed, gain_db: -2}
    hook_count: null
    evidence: [still@key, overlay, flow]
    risk: "A pull moves the block too little to read (F7: 0.7 m)"
    fallback: "Dry-run the pull in the scout; need >= 1.5 m travel in 1 s"
    needs: [F-CAM-TARGET-BLOCK, F-PUSH-DRYRUN]

  - id: S06
    section: build
    cut_in: {frame: 210, beat: "4.3"}
    cut_out: {frame: 240, beat: "5.1"}
    tape_in: 315
    speed: [{at_frame: 0, speed: 1}]
    subject: {kind: actor, id: dev}
    also_in_frame: []
    shows: [human_on_top]
    learns: "They turn to run"
    feels: panic
    size: MCU
    angle: eye
    cam_height_m: 0
    side: {face: 3q_front, yaw_deg: 20}
    move: handheld
    move_params: {noise: running, lag_s: 0.2}
    lens: {focal_mm: 50, aperture: 2.0, focus: subject}
    frame_at_key: {grid: ML, min_area_pct: 20, max_occlusion_pct: 5}
    action_cue: {cue_id: dev_turns, tape_frame: 320, key_frame: 6}
    transition_in: cut
    connects_from_prev: {type: reaction, screen_direction: R>L, eye_exit: MC, eye_enter: ML}
    sound:
      - {kind: sfx, name: footstep_candy, frame: 7, source: take, gain_db: -6}
      - {kind: music_hit, name: snare_build_start, frame: 0, source: library, gain_db: -3}
    hook_count: null
    evidence: [still@key, overlay]
    risk: "Turn happens before the shot starts"
    fallback: "Shift tape_in by the take report's turn time minus 0.2 s"
    needs: []

  - id: S07
    section: build
    cut_in: {frame: 240, beat: "5.1"}
    cut_out: {frame: 255, beat: "5.2"}
    tape_in: 330
    speed: [{at_frame: 0, speed: 1}]
    subject: {kind: hand, id: hand}
    also_in_frame: ["block:240"]
    shows: [hand]
    learns: "Giant fingers pinch the next block"
    feels: threat
    size: ECU
    angle: high
    cam_height_m: 1
    side: {face: profile, yaw_deg: 90}
    move: static
    move_params: {noise: subtle}
    lens: {focal_mm: 65, aperture: 2.8, focus: subject}
    frame_at_key: {grid: TR, min_area_pct: 20, max_occlusion_pct: 10}
    action_cue: {cue_id: hand_pinch, tape_frame: 337, key_frame: 7}
    transition_in: smash_cut
    connects_from_prev: {type: cause_effect, screen_direction: R>L, eye_exit: ML, eye_enter: TR}
    sound:
      - {kind: sfx, name: knuckle_creak_giant, frame: 6, source: designed, gain_db: -4}
    hook_count: 2
    evidence: [still@key, overlay]
    risk: "Hand not drawn in playback (F5)"
    fallback: "Performed hand take in VR, rendered live rather than played back"
    needs: [F-HAND-PATH, F-HAND-DRAWN]

  - id: S08
    section: build
    cut_in: {frame: 255, beat: "5.2"}
    cut_out: {frame: 270, beat: "5.3"}
    tape_in: 345
    speed: [{at_frame: 0, speed: 1}]
    subject: {kind: actor, id: dev}
    also_in_frame: [falling_blocks]
    shows: [fall]
    learns: "They leap off the falling block"
    feels: desperation
    size: FS                           # a WS of 0.5 s with a new idea fails L09
    angle: low
    cam_height_m: -2
    side: {face: profile, yaw_deg: 80}
    move: track
    move_params: {distance_m: 1.5, speed_mps: 3.0, ease: 0}
    lens: {focal_mm: 28, aperture: 5.6, focus: subject}
    frame_at_key: {grid: MC, min_area_pct: 8, max_occlusion_pct: 15}
    action_cue: {cue_id: dev_jump, tape_frame: 351, key_frame: 7}
    transition_in: cut
    connects_from_prev: {type: match_motion, screen_direction: R>L, eye_exit: TR, eye_enter: MC}
    sound:
      - {kind: sfx, name: jump_effort, frame: 6, source: take, gain_db: -6}
    hook_count: null
    evidence: [strip, flow, overlay]
    risk: "The jump is too fast to read in 0.5 s"
    fallback: "Take one frame of S09's ramp earlier, or merge S08 into S09"
    needs: []

  - id: S09
    section: breath                    # the one allowed long shot inside the build
    cut_in: {frame: 270, beat: "5.3"}
    cut_out: {frame: 300, beat: "6.1"}
    tape_in: 352
    speed: [{at_frame: 0, speed: 0.35}]
    subject: {kind: actor, id: dev}
    also_in_frame: [sky_darkening]
    shows: []
    learns: "In mid-air, they look up at something"
    feels: suspension
    size: MCU
    angle: low
    cam_height_m: -0.5
    side: {face: 3q_front, yaw_deg: 25}
    move: ride
    move_params: {lag_s: 0.05}
    lens: {focal_mm: 50, aperture: 2.0, focus: subject}
    frame_at_key: {grid: BL, min_area_pct: 18, max_occlusion_pct: 5}
    action_cue: {cue_id: dev_airborne, tape_frame: 355, key_frame: 18}
    transition_in: cut
    connects_from_prev: {type: match_motion, screen_direction: up, eye_exit: MC, eye_enter: BL}
    sound:
      - {kind: silence, name: music_dropout, frame: 0, source: library, gain_db: -60}
      - {kind: sfx, name: wind_rush_slowed, frame: 0, source: designed, gain_db: -8}   # designed, not take sound (F4)
      - {kind: sfx, name: riser_reverse, frame: 10, source: library, gain_db: -4}
      - {kind: sfx, name: sharp_breath_in, frame: 18, source: designed, gain_db: -6}   # the key moment (L15)
    hook_count: null
    evidence: [still@key, overlay, audio@cut_out]
    risk: "Take sound plays at real speed under the ramp (F4)"
    fallback: "Mute take sound for this shot; designed sound only"
    needs: [F-RAMP-SOUND]

  - id: S10
    section: climax
    cut_in: {frame: 300, beat: "6.1"}    # on the main hit
    cut_out: {frame: 360, beat: "7.1"}
    tape_in: 360
    speed: [{at_frame: 0, speed: 1.0}, {at_frame: 30, speed: 0.6}]
    subject: {kind: hand, id: hand}
    also_in_frame: [tower, falling_survivors, ocean]
    shows: [hand, tower_over_ocean, fall]
    learns: "The hand belongs to a giant player above the tower"
    feels: awe
    size: EWS
    angle: ground
    cam_height_m: -20                    # from the waterline looking up
    side: {face: front, yaw_deg: 0}
    move: pull_out
    move_params: {distance_m: 6, speed_mps: 3, ease: 3}
    lens: {focal_mm: 20, aperture: 8, focus: subject}
    frame_at_key: {grid: TC, min_area_pct: 20, max_occlusion_pct: 10}
    action_cue: {cue_id: hand_lift_block, tape_frame: 362, key_frame: 3}
    transition_in: smash_cut
    connects_from_prev: {type: eye_trace, screen_direction: up, eye_exit: TC, eye_enter: TC}   # hero looked up; we cut to what they saw
    sound:
      - {kind: music_hit, name: main, frame: 0, source: library, gain_db: 0}
      - {kind: sfx, name: sub_boom, frame: 0, source: designed, gain_db: -2}
      - {kind: sfx, name: blocks_cascade, frame: 12, source: designed, gain_db: -4}   # designed: the shot ramps to 0.6x (L16)
    hook_count: 3
    evidence: [still@first, still@key, still@last, strip, overlay, audio@cut_in]
    risk: "Hand missing or tiny (F5); hand not where planned (F6)"
    fallback: "No fallback keeps this trailer: block the shot until F-HAND-PATH and F-HAND-DRAWN exist or a person performs the hand"
    needs: [F-HAND-PATH, F-HAND-DRAWN, F-PREVIEW-STILLS]

  - id: S11
    section: title
    cut_in: {frame: 360, beat: "7.1"}
    cut_out: {frame: 450, beat: "8.3"}    # 15.0 s, end of file
    tape_in: 430
    speed: [{at_frame: 0, speed: 1}]
    subject: {kind: tower, id: spire}
    also_in_frame: [hand_silhouette]
    shows: []
    learns: "This is Deadly Jenga"
    feels: resolve
    size: WS
    angle: high
    cam_height_m: 25
    side: {face: 3q_front, yaw_deg: 40}
    move: crane_up
    move_params: {distance_m: 3, speed_mps: 1.0, ease: 1}
    lens: {focal_mm: 24, aperture: 8, focus: subject}
    frame_at_key: {grid: MC, min_area_pct: 10, max_occlusion_pct: 0}
    action_cue: {cue_id: last_block_drops, tape_frame: 480, key_frame: 60}   # the button
    transition_in: cut
    connects_from_prev: {type: contrast_size, screen_direction: none, eye_exit: TC, eye_enter: MC}
    sound:
      - {kind: music_hit, name: title, frame: 0, source: library, gain_db: 0}
      - {kind: sfx, name: single_block_clack, frame: 60, source: designed, gain_db: -3}   # the button on bar 8.1
      - {kind: ambience, name: ocean_wind, frame: 0, source: take, gain_db: -14}
    hook_count: null
    evidence: [still@first, still@last, title_safe]
    risk: "Busy picture behind the title"
    fallback: "Darken the picture under the title"
    needs: [F-EXPORT-CUT-TITLES]
```

I checked this example by hand against the lints in section 3. All cuts are on beats, and the
lengths add up to 450 frames. The build shortens (30, 30, 15, 15 frames), the climax starts on the
main hit, the hand count is 1-2-3, and every key moment has a sound within 3 frames. Neither
ramped shot carries take sound. The first draft failed four lints: an S01 sound 5 frames off its
key moment, S07/S08 key frames too close to the cut, a 0.5 s wide shot carrying a new idea (L09),
and take sound under S10's ramp (L16). Those are the kind of errors the lint runner should
catch. S01, S07 and S10 stay BLOCKED (L21) until the hand features in section 7 exist or a person
performs the hand.

---

## 3. Plan lints

Lints run on `plan.yaml` alone, or on `plan.yaml` plus `take-report.json` and the scout numbers
(marked †). **Hard** lints block the next stage. **Soft** lints must be fixed or waived in the plan
with a `waive: {lint, reason}` entry, which the critic sees. Where a threshold is my own choice and
not from a source, it says (inference). Every threshold is meant to be tuned in the experiments in
section 6.

| ID | Sev. | Rule | How it is computed | Source / reason |
|----|------|------|--------------------|-----------------|
| L01 | hard | Every cut on the grid | `cut_in` within ±1 frame of a beat or half-beat, unless `transition_in` is `dissolve` or waived | Lieu's beat-matching in the fine cut. His note that 2 frames off can look better is why waivers exist. |
| L02 | hard | Timeline is continuous and exact | shot n `cut_out` = shot n+1 `cut_in`; last `cut_out` = `length_frames`; all integers | Bookkeeping |
| L03 | hard | One subject, one idea | exactly one `subject`; `learns` ≤ 12 words, contains no " and " / ";" | Lieu's clarity priority. Word limit is my choice (inference). |
| L04 | hard | Hook in the first 2 s | some shot with `hook_count` ≥ 1 has `cut_in + key_frame` < 2.0 s; frame 0 is not a logo or black | Lieu: hook as soon as genre is read, logos lose attention. 2 s for a 15 s cut is scaled from his "first third" (inference). |
| L05 | hard | Climax on the main hit | the `climax` shot's `cut_in` = `hits.main.frame` ±1, and its `key_frame` ≤ 4 frames | Sync of picture peak and music peak (Lieu: cut to rhythm) |
| L06 | hard | Hits measured | every `hits[*].measured_onset_frame` set and within ±2 frames of `frame` | Stops guessed beats (inference from F18) |
| L07 | hard | Adjacent shots differ | for shots n and n+1 with the same subject: size differs by ≥ 2 steps OR camera yaw differs by ≥ 30° OR angle class differs | The 30-degree rule and shot-size contrast (standard editing practice; not from one fetched source) |
| L08 | soft | Build shortens | in `build`, each length ≤ the previous (±1 frame); mean of the last third ≤ 0.7 × mean of the first third. One `breath` shot right before `climax` is exempt. | Rising intensity in Lieu's and GMTK's intensity graphs; ratio is my choice (inference) |
| L09 | hard | Minimum readable length | EWS/WS ≥ 15 frames at 30 fps (0.5 s) and ≥ 30 frames if the shot carries a `learns` that is new; MS/FS ≥ 12; CU/ECU/INSERT ≥ 9 | Wider shots need more reading time (Lieu's shot-length range 0.8-4.25 s). Numbers are my choice (inference). |
| L10 | hard | Action inside the window | `key_frame` ≥ 6 frames after `cut_in` (except climax/hook on a hit) and ≥ 6 frames before `cut_out` | Viewer must see the lead-in and the result (inference). Attempt 1's pull did not read. |
| L11 | soft | Rule of threes | `hook_count` values are 1..k with 2 ≤ k ≤ 3 | Lieu, "The Rule of Threes" |
| L12 | soft | Screen direction | for consecutive shots of the same movement, `screen_direction` equal, unless `connects_from_prev.type` is `contrast_size` / `new_beat` or waived | Continuity practice (standard; not fetched) |
| L13 | soft | Eye trace | `eye_exit` of n and `eye_enter` of n+1 are the same or adjacent grid cells, unless the transition is a `smash_cut` or `new_beat` | Murch's eye trace (rule of six) |
| L14 | soft | No three in a row | no three consecutive shots with the same size class or the same angle class | Contrast between shots, Lieu's "waves" (inference) |
| L15 | hard | Every key moment has a sound | each `action_cue.key_frame` has a `sound` event within ±3 frames | Lieu treats SFX as a percussion layer. Attempt 1 had no impact sounds. |
| L16 | hard | Slow motion has designed sound | any shot with `speed` ≠ 1 has no `source: take` sound, or the Studio's ramp-following sound is on | F4 |
| L17 | soft | No mid-trailer music hole | a `silence` event longer than 1 beat is allowed only in the shot right before `climax` | Lieu's critique: music pauses broke momentum |
| L18 | hard | Title timing | each title: on screen ≥ 0.3 s × words + 1.0 s; starts on a hit or a beat; no title overlaps a shot's `key_frame` ±6 frames except the title shot; final title holds ≥ 1.0 s after the last title appears | Lieu blocks 3-5 s for titles in long trailers. Reading formula is my choice (inference). |
| L19 | hard | Must-show covered | each `must_show` id is in some shot's `shows` | Brief |
| L20 | soft | Human face | at least one shot with `subject.kind = actor` (the hero), `side.face` in front / 3q_front, size MS or tighter | F16, Lieu's "player perspective" |
| L21 | hard | Blocked features | any shot with `needs` that the Studio lacks is flagged BLOCKED with its fallback, and the plan says which fallback it uses | Makes F5/F6-type gaps visible before capture |
| L22 † | hard | Subject visible | scout: at `key_frame`, subject screen area ≥ `min_area_pct` and occlusion ≤ `max_occlusion_pct`; subject's projected box inside the frame | F10, F15, attempt 1 shot B |
| L23 † | hard | Action happened | take report has the cue's event within ±8 frames of `tape_frame`, with the planned effect (e.g. block travel ≥ 1.5 m) | F7, F12 |
| L24 † | soft | Planned grid cell | subject centroid at `key_frame` in the planned grid cell or a neighbour | Composition plan |
| L25 † | hard | No reused footage | no two shots show overlapping tape ranges from similar camera positions (< 2 m apart, < 20° apart) | Lieu: duplicate-frame check |
| L26 | soft | Sequence of ideas | no shot's `learns` depends on an idea that only appears later (the maker tags dependencies as `after: Sxx`) | Lieu's rough-cut order check |

Lints L22-L25 need engine numbers. The Studio knows where every actor and block is on every
frame, so these are exact projections and raycasts, not guesses from images. That is the agent's
biggest advantage over watching video **(inference)**.

---

## 4. Seeing and hearing by proxy

### 4.1 What the research says models can and cannot judge

- **Stills: content and composition, fairly well. Camera motion and lens, badly.** ShotBench
  (eight cinematography dimensions, films judged by experts) found even the best model below 60%
  average accuracy. Camera movement was the weakest dimension, with over half of the models below
  40%. Models confuse neighbouring shot sizes (medium versus medium close-up), push-in versus zoom,
  and tilt versus boom.
- **Editing: recognised in part, judged badly.** VEU-Bench found that video LLMs do better on
  elements inside a frame than on elements across shots. Cut-type recognition peaked at about
  42/100 (GPT-4o). Judging scores averaged below 2 out of 5. Some models scored below chance on
  shot speed, which the authors blame on uniform frame sampling. They also found that models can
  describe editing concepts in words but cannot apply them to what they see.
- **Camera motion needs geometry.** CameraBench found that structure-from-motion handles geometric
  camera motion better, and VLMs handle semantic motion better (project page and abstract only).
- **Video input is sparse.** The Gemini API samples video at 1 frame per second by default and
  warns that fast action loses detail. Audio in video is processed at a low bitrate (official docs
  plus a Google forum reply). A 0.5 s shot can fall between two sampled frames.
- **Text-only pipelines miss what captions miss.** LAVE captioned one frame per second. Users found
  its trims wrong when the request referred to brightness or motion that the captions did not
  describe.
- **Automatic quality scores measure the wrong thing.** DOVER scores technical and aesthetic
  quality of user video. VBench scores generated video on 16 dimensions (subject consistency,
  motion smoothness, flicker and others). These catch artefacts and flicker. Neither is trained on
  trailer craft (inference from what they were trained on).
- **Shot boundaries can be measured.** PySceneDetect's ContentDetector reached F1 about 73 on its
  own benchmark at the best threshold. TransNetV2 is stronger on gradual transitions (an
  independent check put it at about 87% versus 65% for PySceneDetect). For the agent's own cut,
  the EDL already says where cuts are, so detection is a check that the render matches the plan.

Design rule that follows: **engine data for geometry and motion, DSP for sound, the vision model
for content and composition in stills, and the critic for judgement only on top of those
numbers.**

| Question | Best source | Not |
|----------|-------------|-----|
| Is the subject in frame, how big, how hidden? | Engine projection plus ID-buffer or raycast (exact) | VLM guess |
| Is the subject readable (face, pose, what it is)? | VLM on a full-size still at `key_frame` | Engine |
| What is the camera doing? | The camera track's own keys (speed m/s, deg/s) | VLM (ShotBench, CameraBench) |
| How much moves on screen, and where? | Optical flow (OpenCV Farneback or RAFT) plus the subject's screen velocity from the engine | VLM |
| Are cuts where planned, any flash frames or pops? | Shot-boundary detection plus frame-difference spikes on the rendered MP4 | VLM |
| Do impacts land on cuts and hits? | Onset detection on the mix versus cut frames (table of offsets) | Ears (not available) |
| Is the mix balanced and loud enough? | EBU R128 momentary (0.4 s), short-term (3 s), integrated, true peak; per-stem levels | — |
| Is it good? | Critic, citing the above | Any single number |

### 4.2 The evidence pack (made automatically for every preview render)

Per shot, all images labelled with shot id, cut frame and tape frame:

1. **Filmstrip.** Frames at a fixed interval of 6 frames (0.2 s at 30 fps), at least 5 per shot,
   at 320 px wide, in one row per shot. Questions: Does the action progress from first to last
   frame? Does anything appear or disappear without a reason (pops, ghosts as in F9)? Does the
   subject stay in frame the whole time? Is the shot static where the plan says it moves?
2. **First, key and last frames at full preview size**, plus key-2 and key+2 frames. Questions
   (asked one at a time, each answered yes/no plus one sentence): Is `subject` visible and
   recognisable? Is it what the eye goes to first? (The model points to the grid cell it looks at
   first and this is compared with `frame_at_key.grid`.) Is the `learns` sentence true of this
   frame? Is the face visible where the plan says so? Anything ugly: clipping, a camera inside
   geometry, grey see-through actors, blank sky where something should be?
3. **Subject overlay.** The same key frame with the engine's projected box for the subject,
   occlusion % and screen area %, and the 3x3 grid. Questions: do the numbers pass L22/L24? If the
   VLM said "not visible" but the overlay says 30% unoccluded, look again; the disagreement goes
   into the report (it was the actual failure in F5).
4. **Motion strip.** A plot over the shot's frames: camera speed (m/s and deg/s from the track),
   subject screen speed (% of width per second), mean optical-flow magnitude, frame-difference
   score; vertical lines at `key_frame` and at cuts. Questions: Is there a motion peak at
   `key_frame`? Is the camera moving faster than the subject in a shot that should feel steady
   (inference: a common amateur look)? Across a cut, do motion levels match for `match_motion` and
   contrast for `contrast_size`? Any frame-difference spike that is not a cut (a pop)?
5. **Eye-trace pair.** The last frame of shot n beside the first frame of shot n+1, each with the
   subject box. Question: Where is the eye at the cut, and is the jump intended (L13)?

For the whole cut:

6. **Cut contact sheet.** One key frame per shot, in order, with its `learns` line under it. This
   is the storyboard of the actual cut. Question for the critic: read only these, in order; is the
   story clear and does each frame show its line? (This is the animatic check again, on real
   pictures.)
7. **Audio plot.** The waveform of the mix; momentary and short-term loudness curves; beat grid;
   cut marks; detected onsets; planned `sound` events. Below it, a **sync table**: for each cut
   and each planned hit or key moment, the nearest onset and the offset in frames. Questions: Is
   every planned hit within ±2 frames of an onset? Are there big onsets that do not match any cut
   or key moment (noise in the wrong place)? Is there a loudness drop where the plan has no
   `silence`? Does short-term loudness rise into the climax?
8. **Stem table.** Integrated loudness and peak per lane (music, take/world, designed SFX, VO).
   Attempt 1's music stem sat around -40 dB RMS against SFX near -10 dB peak (F3). Question: is
   any lane far below where the plan's `gain_db` would put it?
9. **Spectrogram strip** of the climax ±2 s (inference: a low-frequency hit should show as a
   broad vertical band at the main hit; a missing band means the boom did not land).
10. **Delivery numbers.** Duration, frames, resolution, fps, integrated loudness, true peak,
    loudness range. Lieu's and broadcast targets differ. EBU R128 is -23 LUFS for broadcast with a
    -1 dBTP ceiling. Online trailers are usually mastered louder (attempt 1: -15.1 LUFS). The
    target is a brief field, not a fixed rule (inference).

Rules for using the pack:
- The agent answers each question in writing before it judges the shot. It cites the image and
  frame. No "looks good".
- A preview is never judged from a video file alone (1 fps sampling would miss 0.5 s shots).
- Anything that depends on hearing (does the music fit, is the boom satisfying, is anything
  harsh) is listed as **unverified** and goes to the human pass (stage 11).

---

## 5. Critic rubric and protocol

### 5.1 Why a separate critic, and its known failure modes

- LLMs do not reliably fix their own reasoning without outside feedback, and sometimes get worse
  (Huang et al., "Large Language Models Cannot Self-Correct Reasoning Yet"). The critic must work
  from the evidence pack and lints (outside feedback), not from re-reading the maker's reasoning.
- LLM judges prefer their own outputs (self-preference), favour the first of two options
  (position bias) and longer answers (verbosity). This is known from Zheng et al.'s MT-Bench
  study, which I did not re-fetch, and from more recent judge-bias papers. Rubric scoring also
  shifts with the order of score labels, and earlier scores in context anchor later ones (2026
  preprints seen in search abstracts).
- Pixar's Braintrust gives notes that name the cause of a problem and have no power to dictate the
  fix. Lieu's critique of "Ascent of Ashes" is a list of concrete questions, each with a found
  problem and a proposed fix (hook, genre clarity, generic text, time to first gameplay, player
  perspective, shot-text match, show versus tell, purpose, ordering, momentum).
- CutClaw, a 2026 multi-agent editor, uses a separate Reviewer agent that accepts or rejects each
  clip on protagonist presence, rhythm-grid fit, non-overlap and perceptual quality, and sends
  rejections back to the Editor. That is the same shape as this protocol, at clip level.

### 5.2 Setup

- The critic is a **fresh agent** with no maker conversation. It gets: `brief.yaml`,
  `concept.md`, `plan.yaml`, lint results, the evidence pack, the previous round's critic report
  (from round 2 on), and the previous version's evidence for pairwise comparison. It does not get
  the maker's notes on why choices were made, except `waive` reasons.
- Where possible the critic runs on a **different model** from the maker, to reduce
  self-preference (inference: Opus maker, a different model family or at least a different model
  as critic).
- The critic answers the evidence-pack questions itself before scoring.

### 5.3 Rubric

Ten categories. Weights follow Murch's ordering (emotion and story above rhythm and eye trace) and
Lieu's priorities (art and music, then the sequence of ideas, then clarity, then features). The
numbers are my choice (inference).

| # | Category | Weight | What it asks |
|---|----------|--------|--------------|
| C1 | Hook | 1.5 | Does the first 2 s show something only this game has? |
| C2 | Clarity | 1.5 | Does every shot read at preview size: one subject, one idea, the `learns` line true? |
| C3 | Story and order | 1.5 | Do ideas arrive in an order where each sets up the next, with escalation in waves, ending on the strongest? |
| C4 | Emotion and human stakes | 1.0 | Is there a person to care about, with a readable face and reaction? |
| C5 | Rhythm and sync | 1.0 | Do cuts, key moments and hits line up; does pace rise into the climax? |
| C6 | Shot craft | 1.0 | Framing, composition, size and angle contrast, continuity, eye trace |
| C7 | Sound design and mix (by numbers) | 0.75 | Does every key moment have a sound; are lanes balanced; are loudness targets met? |
| C8 | Titles | 0.5 | Readable, timed to hits, saying something about the game (Lieu: benefits, not generic features) |
| C9 | Technical | 0.75 | No pops, ghosts, clipping cameras, black frames, blocked subjects, duplicate footage |
| C10 | Brief fit | 0.5 | Must-show list, length, aspect, tone |

Anchors (shared wording, with category-specific examples in the playbook):

| Score | Meaning | Example for C2 Clarity | Example for C5 Rhythm |
|-------|---------|------------------------|------------------------|
| 1 | Broken: the category fails in a way a viewer would notice at once | Two or more shots where the subject cannot be found (attempt 1 hand shot: empty sky) | Climax cut more than 6 frames off the main hit; cuts ignore the grid |
| 2 | Weak: a clear failure in at least one important shot | The key beat does not read (attempt 1's 0.7 m pull seen from 11 m) | Most cuts on the grid, but the climax or title misses its hit |
| 3 | Acceptable amateur: works, with several noticeable problems | All subjects found, but two shots need a second look | All cuts on the grid; pace flat, no rise into the climax |
| 4 | Professional: works; problems only a trained eye would note | Every shot reads on first view; one minor framing issue | Cuts and hits within 2 frames; clear shortening into the climax; one off-grid cut that hurts |
| 5 | Reference quality: comparable to the reference trailers the brief names | Every frame of the contact sheet reads alone, and the eye is always where the plan says | Every accent is an onset or a cut; the drop and the main hit hit hard; nothing to change |

### 5.4 Required evidence

- Every score cites at least one item from the evidence pack (shot id plus frame, or a number from
  the sync table, overlay or lints). A score with no citation is **invalid** and counts as 2.
- A 4 needs a citation of the best and the worst instance in the category.
  A 5 also needs a side-by-side with a reference frame or measure from the brief's reference
  trailers. Without references, 4 is the ceiling.
- Every category at 3 or below needs a **note** in this form: `shot/frame`, `symptom` (what is
  seen or measured), `cause` (why, per Braintrust: name the cause), `stage` to return to
  (section 1.2), and optionally `suggested fix`.
- The critic ends with the **top three problems**, ranked by how much fixing each would raise the
  weighted score. The maker fixes those first. This matches the agency practice of one
  consolidated, prioritised note list.

### 5.5 Guarding against a lenient critic

1. **Planted defects (calibration each round).** Before scoring the real cut, the critic is given
   a variant of the same evidence pack with 3 known defects injected by a script: e.g. one cut
   shifted 5 frames off the grid, one shot's key frame swapped for a frame where the subject is
   hidden, an impact SFX muted, a duplicated shot. The critic does not know which variant is which
   (it gets both in random order). If it finds fewer than 2 of 3 planted defects, or scores the
   defective variant within 0.25 of the real one, its review is thrown out and rerun once with a
   warning. If it fails again, the round goes to the human.
2. **Known references.** On first use of the playbook, the critic scores attempt 1. Its known
   faults are listed in FINDINGS.md (no hand F5/F6, blocked edge shot, unreadable pull F17, no
   human moment F16, sound under ramp F4). A critic that gives attempt 1 above 3.0 weighted, or
   misses two of those faults, is miscalibrated.
3. **Pairwise first, then absolute.** From round 2 on, the critic first states which version is
   better per category, judging both orders (A/B and B/A). Only then does it give absolute scores.
   A category where the two orders disagree is marked "no change". Pairwise judgements are more
   stable than absolute scores (inference based on judge-bias literature; position swapping is a
   standard mitigation).
4. **Rubric order shuffled.** The score anchors are listed in a random order each run. This
   follows the 2026 finding that judges favour score labels in certain list positions.
5. **No scores in context.** The previous round's scores are withheld until the critic has scored
   this round (anchoring). It sees the previous notes only to check whether each was fixed.
6. **Lints overrule.** If a hard lint fails, the related category cannot score above 2, whatever
   the critic thinks.
7. **Score inflation check.** If the weighted score rises by more than 0.75 in one round while
   fewer than half of the previous round's top-three notes were fixed, the round is re-scored by a
   second fresh critic (inference).

### 5.6 Disagreement and re-cut rules

- **Re-cut triggers** (any one): a hard lint fails; any category ≤ 2; weighted mean < 3.5 at rough
  cut or < 4.0 at fine cut; a top-three note in C1, C2 or C3.
- **Maker may contest** a note once, with evidence (a frame, a number). The critic answers once.
  If they still disagree: measurable questions go to the lint or measurement. Taste questions go
  to a second fresh critic as tie-breaker. If the two critics also split, the maker's choice stands
  and the note goes in the human-pass list.
- **Return to the stage named in the note**, not further back, unless the note is in C1 or C3 and
  scores 1 (then back to concept or beat sheet).

### 5.7 When to stop

Stop iterating when any of these holds:
- **Accept:** no hard lint failing, all categories ≥ 3, weighted mean ≥ 4.0, and the planted-defect
  check passed this round.
- **Plateau:** weighted mean improved by less than 0.2 over two consecutive rounds. Ship the best
  version and report what blocked it.
- **Round cap:** 3 rough-cut rounds and 2 fine-cut rounds. This is near the agencies' 2-3 planned
  revision rounds. It also bounds render cost under the 10-minute coordinator limit (F19).
- **Blocked:** a fix needs a Studio feature that does not exist (an L21 item that the fallback
  cannot replace, e.g. S10 without a hand). Stop and report the feature. Do not hide the gap.

After stopping, the human pass is still required. The report always lists what could not be
judged: sound by ear, motion feel at full frame rate, VR comfort if relevant.

---

## 6. Testing the playbook

### 6.1 Questions

1. Does a plan made with the playbook beat a plan made without it, before any render?
2. Does a clip made with it beat a clip made without it, with the same render budget?
3. Does the critic agree with people, and does it catch defects?
4. Which parts matter: schema plus lints, evidence pack, critic?

### 6.2 Experiments

**E1: plan-only (cheap, no Unity).** 6 briefs (giant-hand reveal; tsunami; survivor climb race;
tornado; team mode; the title card only). For each brief, the same agent and model makes plans in
4 conditions: (a) tool docs only (attempt-1 conditions); (b) plus schema and lints; (c) plus
critic; (d) full playbook. 3 runs per cell to see variance, 72 plans in total. Measures: hard-lint
pass rate; number of `needs`/BLOCKED items found before capture; blind expert ranking of the plans
(the shot lists turned into text animatics, condition labels hidden, order randomised).

**E2: clip-level (expensive).** 3 briefs × 2 conditions (tool docs only versus full playbook) × 2
runs, same render budget (e.g. 6 coordinator jobs and the same total minutes each), same Studio
commit, same committed take where possible (F12). Measures:
- **Blind pairwise preference.** Evan plus at least 2 teammates watch pairs side by side, sides
  randomised, file names hidden, with sound. For each pair: overall preference and one per rubric
  category.
- **Recall test.** After one viewing a viewer answers "what is this game, and what do you do in
  it?" Score: whether the answer mentions the hook (giant hand pulling blocks under people).
  Close to Lieu's measure of whether the trailer communicates what is special.
- **Process cost.** Renders to accept, renders wasted on framing failures (the attempt-1 waste),
  wall time, number of hand-made workarounds outside the Studio (attempt 1 needed `assemble.py`
  and drawtext).

**E3: critic validation.** (a) Planted defects: 20 cuts, each with 0-3 injected defects; measure
recall and false alarms. (b) Agreement: correlation between critic weighted scores and the human
mean on the E2 clips plus attempt 1, and the share of pairs where critic and humans prefer the same
clip. (c) Leniency: average critic score minus average human score on the same 1-5 rubric.

**E4: ablation.** From E1/E2 data, compare (b) and (c) against (d) to see whether lints or the
critic carry most of the gain. Remove whatever does not help.

### 6.3 What counts as success (set before running)

- E1: the full playbook passes all hard lints on ≥ 90% of plans (baseline expected well below),
  finds every BLOCKED feature before capture, and its plans are ranked first by the blind expert on
  at least 4 of 6 briefs.
- E2: playbook clips win ≥ 70% of blind pairwise judgements overall, with no brief where the
  baseline wins most judgements. Wasted framing renders drop by ≥ 50% against attempt 1's rate
  (about 3 of 5 renders spent finding lines of sight). The recall test names the hook more often
  for playbook clips.
- E3: planted-defect recall ≥ 80% with ≤ 1 false alarm per cut; critic-human rank correlation
  ≥ 0.5; critic-human same-preference ≥ 70%; mean leniency ≤ +0.5 points.
- Sample size: with 3 raters × 3 briefs × 2 runs = 18 pairwise judgements, 13 of 18 (72%) gives a
  one-sided sign-test p ≈ 0.05. Fewer judgements cannot separate a real effect from noise. More
  raters are better than more briefs here (inference).
- If E2 fails but E1 succeeds, the plan is good and the capture or tool is the bottleneck. Then
  the section 7 features come before more playbook work.

---

## 7. Tool features needed (ranked by value to this process)

| Rank | Feature id | What | Why (attempt-1 finding) | Pipeline stage it enables |
|------|-----------|------|-------------------------|---------------------------|
| 1 | F-PREVIEW-STILLS | Batch stills per camera at listed tape times, low resolution, without a full render, plus the subject's projected box, screen area % and occlusion % (ID-buffer or raycasts) | F10: most iterations went on lines of sight; F15 | 5 (scout), L22/L24, evidence items 2-3 |
| 2 | F-TAKE-REPORT | `take-report.json` per take: cue results and times, blocks moved (distance, time of first hit), actor heights and falls, hand and hazard positions per frame | F11 (the agent's most useful thing, home-made), F7, F12 | 6, L23 |
| 3 | F-PLAN-IMPORT + lint runner | Read `plan.yaml` into cameras, cuts, speeds and titles; run lints L01-L21 without Unity and L22-L25 with the take report and scout | No checkable plan in attempt 1; F14 (fixed ids come from shot ids) | 3, 4, 5 |
| 4 | F-EXPORT-CUT-TITLES | Export the cut as one MP4: picture, mixed sound with music lane, per-lane gains, fades, titles | F1, F2, F3 | 7-10 |
| 5 | F-MUSIC-GRID | Music lane with BPM, downbeat offset, measured onsets and named hits; cut snapping to beats; beat grid on the timeline | F3, F18; schema `music` | 2, L01, L05, L06 |
| 6 | F-EVIDENCE-PACK | Filmstrips, overlays, motion strips (camera speed from keys plus optical flow), audio plot with cut marks and onsets, sync table, stem table, delivery numbers; written per render to a numbered version folder | F10, F18; section 4 | 7-10, critic |
| 7 | F-HAND-PATH + F-HAND-DRAWN | Directable hand: a path with timed grabs on named blocks; the played-back hand always drawn in renders (and a check that it is) | F5, F6: the trailer's key beat was lost | S01, S07, S10 in the example |
| 8 | F-CAM-TARGET-BLOCK | Camera subject can be a block, a point or a hazard, not only an actor | F8 | S01, S05 |
| 9 | F-TAKE-PACKAGE | Import/export of takes; batch "render this saved take"; stable camera ids; a repeatability report that says when a take will differ | F12, F13, F14 | 6-9, E2 |
| 10 | F-PUSH-DRYRUN | Quick physics dry run of a cue from a tape frame, reporting how far each block moves | F7 | 5, L23 |
| 11 | F-RAMP-SOUND | Sound that follows speed ramps (or auto-mutes take sound under a ramp) | F4 | L16 |
| 12 | F-RESUMABLE-RENDER | Renders that split or resume by shot to fit the 10-minute job limit | F19, F20 | 7-9 |
| 13 | F-ANCHORS | Named stage anchors (crown, base, waterline) measured from the map | F15 | 3, 5 |
| 14 | F-GHOST-FIX | No see-through grey survivors in playback renders | F9 | C9 technical |

Ranks 1-3 come first because they move checks from after a render to before it. Attempt 1 spent
most of its five renders on questions those checks answer in seconds **(inference from the render
log in FINDINGS.md section 3)**. Rank 7 matters most for this particular trailer (the hand is the
hook), but it is a gameplay-side change. Any hand-centred brief is BLOCKED (L21) until it exists or
a person performs the hand.

---

## 8. Sources

| # | Title | Author / org | URL | What I took |
|---|-------|--------------|-----|-------------|
| 1 | Basic Game Trailer Editing Workflow | Derek Lieu | https://derek-lieu-yrd2.squarespace.com/blog/2022/10/24/basic-game-trailer-editing-workflow | Four stages (assembly with text cards and temp music; rough cut; fine cut, "lots of client feedback"; finishing/online). Title and logo time blocks. |
| 2 | Garbage Cut: How to Start a New Trailer Timeline | Derek Lieu | https://derek-lieu-yrd2.squarespace.com/blog/2022/7/25/garbage-cut-how-to-start-a-new-trailer-timeline | A deliberately bad first cut as a diagnostic: real timing, missing and repeated content, does it teach. Basis for stages 4 and 7. |
| 3 | How to Hook the Audience and How Quickly to Do it | Derek Lieu | https://derek-lieu-yrd2.squarespace.com/blog/2022/10/24/how-to-hook-the-audience-and-how-quickly-to-do-it | Hook versus anchor; genre in 1-2 shots; hook within the first third, ideally 10-15 s in a long trailer. Scaled for L04. |
| 4 | Editing a Game Trailer in Waves | Derek Lieu | https://derek-lieu-yrd2.squarespace.com/blog/2023/1/14/editing-a-game-trailer-in-waves | Wave ordering instead of a linear build; contrast between shots. C3, L14. |
| 5 | The Rule of Threes in Game Trailers | Derek Lieu | https://derek-lieu-yrd2.squarespace.com/blog/2022/11/14/the-rule-of-threes-in-game-trailers | Show the core hook 2-3 times. L11, `hook_count`. |
| 6 | How to Critique Game Trailers - Ascent of Ashes | Derek Lieu | https://derek-lieu-yrd2.squarespace.com/blog/2023/3/19/how-to-critique-game-trailers-ascent-of-ashes | A worked critique: hook, genre clarity, generic text, player perspective, shot-text match, ordering, music pauses. Rubric categories and L17. |
| 7 | More Trailer Editing is Not Better Trailer Editing | Derek Lieu | https://derek-lieu-yrd2.squarespace.com/blog/2023/4/23/more-trailer-editing-is-not-better-trailer-editing | Clarity over flash; average shot lengths range about 0.8-4.25 s by game. L09. |
| 8 | Professional video editing tidbits | Derek Lieu | https://derek-lieu-yrd2.squarespace.com/blog/2024/5/5/professional-video-editing-tidbits | Consolidated, frame-accurate notes worked from the end; keep every version; short audio fades; ducking. Review protocol and versioning. |
| 9 | Capturing Great Footage for Game Trailers (GDC 2018) | Derek Lieu | https://gdcvault.com/play/1025079/contactUs | Capture as acting, directing and camera work, using debug tools (session overview only). |
| 10 | Trailer Made: What Makes a Successful and Memorable Game Trailer (GDC 2019) | Derek Lieu | https://gdcvault.com/play/1026358/Trailer-Made-What-Makes-a | Session exists; content only via secondary coverage (no logos up front; title card then "show the thing"). Not watched. |
| 11 | The art of making effective game trailers | PC Gamer | https://www.pcgamer.com/the-art-of-making-effective-game-trailers | Lieu: "do audio first"; listing the game's verbs before capture (from search summary). |
| 12 | How to make a game trailer: start with a chronological structure | premortem.games | https://premortem.games/2022/07/15/how-to-make-a-game-trailer-start-with-a-chronological-structure/ | Chronological skeleton, core loop first, escalate, varied payoff at the end. |
| 13 | Article on a game-trailer capture director | Aftermath (author not recorded) | https://aftermath.site/video-game-trailer-capture-director-maker | Script correction for the build, shot list from the chosen script, daily footage to editorial, recapture until lock, ambitious V1. |
| 14 | What assets to provide when starting your game trailer production | Game Developer | https://www.gamedeveloper.com/business/what-assets-to-provide-when-starting-your-game-trailer-production | Brief contents; cheats and debug list; A/V script; storyboard only for story-heavy trailers. Stage 0. |
| 15 | Film & TV services | The Third Floor | https://thethirdfloorinc.com/film-tv/ | Pitchvis, previs, techvis, postvis: what each is for. Stage 5 named after techvis. |
| 16 | Previsualization | Wikipedia | https://en.wikipedia.org/wiki/Previsualization | Previs tests staging, camera, lighting and editing; fidelity ranges from grey shapes up; temp sound. |
| 17 | What is Previs | StudioBinder | https://www.studiobinder.com/blog/what-is-previs-previsualization-in-film/ | Shot lists as the commonest previs; animatics costly for live action. |
| 18 | Shot list template | Boords | https://boords.com/shot-list-template | The 14 professional shot-list columns (scene, setup, shot, description, camera, lens, equipment, cast, movement, angle, size, audio, time estimate, best take). Base of section 2. |
| 19 | Editing stages | Frame.io workflow guide | https://workflow.frame.io/guide/editing-stages | Assembly, rough, fine, director's and producer's cuts, picture lock and why late changes cost. |
| 20 | Walter Murch's Rule of Six (summaries of *In the Blink of an Eye*) | StudioBinder / No Film School / UT Austin wiki | https://www.studiobinder.com/blog/walter-murch-rule-of-six | Ordering emotion > story > rhythm > eye trace > 2D > 3D, with Murch's own weightings. Rubric weights and L13. |
| 21 | Inside the Pixar Braintrust | Fast Company (excerpt of Catmull, *Creativity, Inc.*) | https://www.fastcompany.com/3027135/inside-the-pixar-braintrust | Notes name causes, not remedies; the group has no authority. Note format. (Seen through search summaries.) |
| 22 | CutClaw | GVCLab et al., arXiv 2603.29664 | https://arxiv.org/html/2603.29664v1 | Planner / Editor / Reviewer agents; Reviewer checks protagonist presence, rhythm grid, non-overlap, quality; PySceneDetect and beat analysis; AV-harmony offset metric; small benchmark. |
| 23 | LAVE: LLM-Powered Agent Assistance and Language Augmentation for Video Editing | Wang et al., arXiv 2402.10294 | https://arxiv.org/html/2402.10294v1 | Plan-then-execute with approval; video seen through 1 fps captions; fails on what captions miss (brightness, motion). |
| 24 | VEU-Bench | Li et al., arXiv 2504.17828 | https://arxiv.org/html/2504.17828v1 | Video LLMs weak on cross-shot editing, judging below 2/5, cut-type recognition about 42; uniform frame sampling hurts speed perception. |
| 25 | ShotBench | arXiv 2506.21356 | https://arxiv.org/html/2506.21356v2 | Best models below 60% on cinematography; camera movement and lens weakest; push-in versus zoom confusion. |
| 26 | Towards Understanding Camera Motions in Any Video (CameraBench) | Lin et al., arXiv 2504.15376 | https://arxiv.org/abs/2504.15376 | SfM better at geometric motion, VLMs at semantic motion (abstract and project page only). |
| 27 | VEFX-Bench | arXiv 2604.16272 | https://arxiv.org/pdf/2604.16272 | Generic VLM judges not specialised for edit quality; a dedicated reward model (search summary only). |
| 28 | DOVER | Wu et al., ICCV 2023, arXiv 2211.04894 | https://github.com/vqassessment/DOVER | Aesthetic and technical video quality scores for user video; useful for artefacts, not trailer craft. |
| 29 | VBench | Huang et al., CVPR 2024, arXiv 2311.17982 | https://arxiv.org/abs/2311.17982 | 16 disaggregated video-quality dimensions (subject consistency, motion smoothness, flicker). |
| 30 | PySceneDetect benchmarks; TransNetV2 | Breakthrough; Souček and Lokoč | https://www.scenedetect.com/benchmarks/ ; https://pypi.org/project/transnetv2-pytorch/ | Shot-boundary F1 figures; TransNetV2 better on gradual transitions (CineTrans paper's comparison, arXiv 2508.11484). |
| 31 | EBU R 128 and EBU Tech 3341 | EBU | https://tech.ebu.ch/docs/tech/tech3341.pdf | Momentary 0.4 s, short-term 3 s, integrated loudness; -23 LUFS broadcast target; LRA not advised under a minute. |
| 32 | Video understanding (Gemini API docs) | Google | https://ai.google.dev/gemini-api/docs/video-understanding | Default 1 fps sampling; fast action loses detail; low-bitrate audio. |
| 33 | Large Language Models Cannot Self-Correct Reasoning Yet | Huang et al., ICLR 2024, arXiv 2310.01798 | https://arxiv.org/abs/2310.01798 | Self-correction without outside feedback does not reliably help. Basis for an evidence-fed separate critic. |
| 34 | Judge-bias preprints: rubric position bias (arXiv 2602.02219), anchoring (arXiv 2608.25869) | various | https://arxiv.org/html/2602.02219 ; https://arxiv.org/pdf/2608.25869 | Shuffle rubric order; keep prior scores out of context (abstracts via search only). |
| 35 | Judging LLM-as-a-Judge with MT-Bench and Chatbot Arena | Zheng et al., 2023, arXiv 2306.05685 | https://arxiv.org/abs/2306.05685 | Position, verbosity and self-enhancement biases; position swapping. From prior knowledge, not re-fetched this session. |
| 36 | Agency revision-round practice | Prolific Studio and others | https://prolificstudio.co/gaming-trailer/ | 2-3 planned revision rounds, one decision-maker for notes (search snippets only). |

Tools named in section 4 that I did not fetch this session and cite from general knowledge:
OpenCV Farneback optical flow, RAFT (Teed and Deng, 2020, arXiv 2003.12039), librosa
beat and onset detection, ffmpeg `ebur128` and `showwavespic` filters.

---

## 9. Uncertain or unverified

- **Lieu's newsletter process and checklist** are paywalled or not public. I worked from his public
  blog posts. I did not watch either GDC talk; their content here comes from session pages and
  secondary coverage.
- **No source gives numbers for 15 s trailers.** Lieu's figures (hook within the first third,
  ideally within 10-15 s; title cards 3-5 s) are for trailers of about 90-120 s. The 2 s hook,
  minimum shot lengths, the build-shortening ratio, title reading time and rubric weights are my
  inferences. Section 6 is meant to tune them.
- **Revision-round counts** (2-3) come from agency pages seen only as search snippets.
- **The 30-degree rule, screen direction and eye-trace thresholds** are standard editing practice.
  I did not fetch a primary source for them. The grid-cell version of eye trace is my
  simplification.
- **Judge-bias findings** after 2023 come from abstracts and search summaries, not full papers.
  The MT-Bench citation is from prior knowledge. Whether pairwise judging is more reliable than
  absolute scores for video critique specifically is untested. I am extending text-domain
  findings.
- **CameraBench, VEFX-Bench, the BEAT trailer paper and CutClaw's claims** were read at abstract or
  summary level only (CutClaw in more depth). No published work I found tests a VLM critic on
  game trailers, or an agent directing a game engine camera for a trailer. That is the gap this
  playbook fills, and the reason for section 6.
- **Whether a VLM can answer the evidence-pack questions reliably on Studio renders** is unknown.
  ShotBench and VEU-Bench used film footage. Stylised candy-coloured game art could be easier or
  harder. E3 measures this.
- **Loudness target for online trailers:** EBU R128's -23 LUFS is a broadcast standard. I found no
  authoritative figure for YouTube or Steam trailers. Attempt 1's -15.1 LUFS is plausible for web
  use but unverified.
- **Feasibility of ID-buffer occlusion and the hand path** in the current Studio code was not
  checked. Those are design asks, not claims about what is easy.
- Nothing here was run in Unity. No lint, pack or critic exists yet.
