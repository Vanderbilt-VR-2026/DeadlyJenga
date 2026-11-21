# Legacy: the giant hand (demoted in revision 3)

**Read this only when a brief asks for the hand.** On 2026-11-20 Evan said the flying hand "is
like not an important part of our game at all anymore"; the default is disaster and PvP mayhem
(SKILL.md). Nothing here was deleted because it stopped being true: the rules part is short, and
the two hand worked examples (A, C) follow it with their hand numbers re-derived.

Revision 4 applies here too: record once and layer the actors (`record-and-layer.md`), and see
every key event close or in first person (the hand player's own view counts). For the legacy
`hand` and `two_role` types `tools/lint.py` prints L47-L51 as warnings, not failures; A and C
predate them and warn. A new hand plan should still pass them. Revision 5 (`performance.md`) is the
same: every visible survivor alive in every shot; L52-L56 warn for these types, and A and C (with no
`actors` lists) warn.

## 1. What the hand is now (code, read 2026-11-20)

- A **30-second power-up**: `FLYING HAND · 30 SECONDS` (`Core/Session/EquipmentSpecifications.cs:38`);
  picking it up turns a survivor into the hand for 30 s (`GameSession/Modes/MatchModeRuntime.Equipment.cs:150`,
  `ModeSimulation.cs:297-302`). Also the lobby role, the developer tablet's "Become Hand" tile, and
  AI hand bots that palm-shove blocks (`Characters/Bots/CastleBots/AI_HANDS.md`; manual spawning
  uncapped, so a match can hold more than one giant hand).
- **Drawn size 2.4 x 2.3 x 1.8 m** (measured box of the drawn hand, hand-reveal-2). Revision 2's
  "about 4 m across, about 3 m visible" was 1.6 x too generous; every number below uses 2.3 m of
  height. The hand is only about 2.4 times a survivor's 0.94 m and shorter than a 6 m block.

## 2. Concept types `hand` and `two_role`

| | `hand` | `two_role` |
|---|---|---|
| Core verb (`shows` id) | `pull`: the hand moves a block out of its slot with a survivor in frame | `pull` |
| First hold | first pull shot | first pull shot |
| Extra hard rule | - | both roles shown as players before half the body (L36); one event from both sides (soft L37) |
| BLOCKED if | the hand is not drawn in the scout | the hand is not drawn |
| Example | A (below) | C (below) |

A hand cut with a disaster in it is `hand`. Never add the hand to a brief that does not ask for it.
`pull` = the block leaves its slot in this shot; `grip` = the hand closes on a block that has not
moved yet. Hand ids: `pull`, `grip`, `hand` (readable as a hand, with a survivor or block for
scale), `pov_hand`.

## 3. Framing the hand (re-derived for 2.3 m)

| Framing | Hf | Distance |
|---------|----|----------|
| Hand whole, h 0.5-0.75 (FS) | 3.1-4.6 m | 3.1-4.6 m at 24 mm; 4.5-6.7 m at 35 mm |
| Hand breaking the frame, h 0.8-1.2 | 1.9-2.9 m | 1.9-2.9 m at 24 mm |
| Crown (top 9-12 m) with the hand over it | 14 m | 80 m at 135 mm: the hand is only 0.16 of frame |
| Whole tower, h 0.6-0.9 | 62-93 m | the hand is 2.5-3.7 % of frame: it does not read |

- **Scale ratio.** On screen, hand / survivor = (2.3 / 0.94) x (survivor distance / hand distance)
  = 2.45 x d_s / d_h. Revision 2's "survivor ≥ 0.6 x the hand's distance" now gives only 1.5:1.
  Keep the survivor at **≥ 0.8 x** the hand's distance (ratio ≥ 2.0), or at or behind the hand's
  depth (≥ 2.45). [inferred, geometry; the 0.8 is an estimate]
- Giant-reveal pass row (`camera.md` section 4): hand 0.5-1.2 of frame, survivor 0.15-0.30, camera
  ≤ 0.9 m above the survivor's surface pitched up 15-60°, survivor distance ≥ 0.8 x hand distance,
  hand and hero apart on screen by at least the hand's angular radius + 4° (pilot).
- Recipes: arrival tilt-up (camera on a ledge 0.3-0.9 m up, the hand 3-5 m away at 24-28 mm, the
  survivors at ≥ 0.8 of its distance); silhouette against bright sky; long-lens menace (a 14 m crown
  frame leaves the hand at 0.16, so prefer 9-10 m frames: 50-60 m at 135 mm); the pull at block
  height 4-7 m from the block's side, 35 mm, travel ≥ 2 m (≥ 15 % of frame width), hold 1.5 s; hand
  POV smoothed.
- Staged hand reveal (defaults): partial (a block slides on its own, fingertips cropped, a shadow)
  1.0-1.5 s → reaction (the hero's head turns up; a scream or cower layer) 0.7-2.0 s → whole (the
  pull beside the hero) 1.5-2.0 s → escalating pulls → a held beat → the payoff on the main hit.

## 4. Pilot and field-test lessons about the hand

| Mistake | Fix |
|---------|-----|
| The hand never appeared, and the trailer shipped (attempt 1) | scout the hand; a hand concept with no drawn hand is BLOCKED |
| The pull moved a block 0.7 m seen from 11 m (attempt 1) | dry-run the cue; ≥ 2 m; travel ≥ 15 % of frame width; hold 1.5 s |
| The hand looked like an untextured rubber glove with a cut stump (pilot) | a real hand design (`BakeLook`; hand-reveal-2 used Lava), silhouette against bright sky, wrist out of frame, ask the human pass |
| No closing frame held hand and tower together (pilot) | plan a final frame with both readable |
| Hand, hero and lens in one line, so the hand hid the hero (pilot) | keep them apart by the hand's angular radius + 4° |
| The directed hand hung nearly still, then yanked late (hand-reveal-2) | its path is on a fixed ease (most of the flight in the first 20 %, the yank's motion peak 15-25 frames after the hit): put the hit on the motion peak you measure, not on the cue time |
| A shadow that stops at the hero's feet (hand-reveal-2) | aim the shadow solver at the hero's body, not the hand's middle |
| Two hands planned (skill test 1) | a human hand player flies one hand; AI hand bots can add more: plan a second hand only with `needs: verify` |

Studio support for the hand (directed `handPull` cue with carry, `BakeLook` designs, the baked
mesh, no live hand while recording): `studio-tooling.md` sections 2-3.

---

## A. Worked example A: 15 s giant-hand reveal (legacy; passes the revision-2 lints)

Moved here from `plan-schema-and-lints.md` in revision 3. Hand numbers re-derived for the drawn hand
(2.3 m tall, not 3 m): S03's hero moved from 3.5 m to 4.5 m; S03, S05, S07 and S09 fit lines
changed. Do not copy its tagline, caption, hero or camera numbers (L39).

Music: a 120 BPM cue, 4/4, so one beat is 15 frames and one bar 60. Bars count from 1 at frame 0.
The `measured_onset_frame` values stand for what stage 2 measures on the real file; replace them
with the real ones and shift the plan to the real hits, never the other way round.

```yaml
id: hand-reveal-15
version: 1
example: true
mode: production
concept_type: hand
opening: staged
threats: [hand]
fps: 30
length_frames: 450
class: 15
aspect: "16:9"
shape: montage
hook: "A giant hand pulls the blocks people are standing on"
hero: mint
cast: [mint, rook]
must_show: [pull, hand, survivor_face, fall, tower_over_ocean]
capture: {hand: performed, take: "bundle/hand-reveal-take-01"}   # a person flies the hand in VR (F6)
camera_sensor_mm: 24                # set sensorHeight = 24 on every Studio track
music:
  file: Music/trailer_cue_120.wav
  bpm: 120
  meter: 4
  downbeat_frame: 0
  sections:
    - {name: intro, from_frame: 0,   to_frame: 120}
    - {name: build, from_frame: 120, to_frame: 228}
    - {name: drop,  from_frame: 240, to_frame: 345}
    - {name: tail,  from_frame: 345, to_frame: 450}
  hits:
    - {name: open,   role: open,   frame: 0,   bar_beat: "1.1", measured_onset_frame: 1}
    - {name: reveal, role: reveal, frame: 135, bar_beat: "3.2", measured_onset_frame: 136}
    - {name: main,   role: main,   frame: 240, bar_beat: "5.1", measured_onset_frame: 239}
    - {name: title,  role: title,  frame: 345, bar_beat: "6.4", measured_onset_frame: 346}
  stopdowns:
    - {from: 228, to: 240}          # 0.4 s music drop-out, one creak left
titles:
  - {text: "THE HAND IS ANOTHER PLAYER", role: caption, frame_in: 150, frame_out: 228, style: over_picture, position: lower_third, closing: false}
  - {text: "DEADLY JENGA", role: title, frame_in: 345, frame_out: 450, style: over_picture, position: center, closing: true}
  - {text: "Don't be the one who falls.", role: tagline, frame_in: 360, frame_out: 450, style: over_picture, position: lower_third, closing: false}
mix: {target_lufs: -15, true_peak_max: -1.0, title_vs_body_lu: -7, ramp_lu: 3}
studio: {ramp_sound: false}

shots:
  - id: S01
    section: hook
    kind: gameplay
    cut_in: 0
    cut_out: 45
    tape_in: 118
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: block, id: "block:212"}
    also_in_frame: [hand_fingertips]
    shows: [pull]
    learns: "Something huge is sliding blocks out of the tower"
    feels: jolt
    raises: null
    reveal: partial
    threat: hand
    cause_in: null
    size: INSERT
    angle: eye
    side: {face: profile, yaw_deg: 90}
    camera: {move: static, purpose: "let the sliding block carry the shot", ease: none,
             distance_m: 4.0, height_m: 1.0, focal_mm: 35, aperture: 4, focus: subject, params: {}}
    fit: "Hf = 4.0 x 24/35 = 2.74 m: the 2 m block end fills 0.73 (INSERT); frame width 4.9 m, so the
          2 m slide crosses 41 %; fingertips cropped at the top edge."
    key_frame: 22                   # the block starts to leave its slot
    action_cue: {cue_id: hand_pull_1, tape_frame: 140, expect: {block_travel_m: 2.0}}
    frame_at_key: {grid: MC, min_area_pct: 30, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: R>L, eye_exit: TC, eye_enter: null}
    sound:
      - {at: 0,  kind: music_hit, name: open, source: library, gain_db: 0, sync: open}
      - {at: 22, kind: sfx, name: candy_scrape_long, source: designed, gain_db: -3}
    hook_count: 1
    evidence: [still@key, strip, overlay]
    risk: "Fingertips not drawn in playback (F5)"
    fallback: "Block alone sliding out, scrape carries it; still a partial reveal"
    needs: [{feature: hand_drawn_in_playback, status: verify}]

  - id: S02
    section: teach
    kind: gameplay
    cut_in: 45
    cut_out: 105
    tape_in: 200
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: mint}
    also_in_frame: [sky, crown_blocks]
    shows: [survivor_face]
    learns: "A tiny climber on top feels it"
    feels: unease
    reaction: "scream layer: head tips up, both hands rise"   # avatars have no drawn face
    raises: null
    reveal: reaction
    threat: hand
    cause_in: null
    size: MCU
    angle: low
    side: {face: 3q_front, yaw_deg: 25}
    camera: {move: static, purpose: "read her face as she looks up", ease: none,
             distance_m: 1.0, height_m: 0.6, focal_mm: 50, aperture: 2.8, focus: subject, params: {}}
    fit: "Hf = 1.0 x 24/50 = 0.48 m: chest up (MCU 0.35-0.48); lens 0.6 m up, under her 0.85 m eyes."
    key_frame: 30                   # she looks up
    action_cue: {cue_id: mint_looks_up, tape_frame: 230}
    frame_at_key: {grid: MR, min_area_pct: 15, max_occlusion_pct: 5, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: reaction, screen_direction: none, eye_exit: MR, eye_enter: MR}
    sound:
      - {at: 18, kind: sfx, name: deep_tower_creak, source: designed, gain_db: -6, lead_in: true}   # sound before sight
      - {at: 30, kind: sfx, name: sharp_breath_in, source: designed, gain_db: -8}
    hook_count: null
    evidence: [still@key, overlay]
    risk: "A block edge hides her face (attempt 1 shot B)"
    fallback: "Use the scout's clearest of 8 yaw angles within 60 degrees of her facing"
    needs: [{feature: preview_stills, status: fallback}]   # workaround in studio-tooling.md

  - id: S03
    section: teach
    kind: gameplay
    cut_in: 105
    cut_out: 165
    tape_in: 300
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: hand, id: hand}
    also_in_frame: [mint, "block:207"]
    shows: [hand, pull]
    learns: "A giant hand pulls the block beside her"
    feels: awe
    raises: null
    reveal: whole
    threat: hand
    cause_in: null
    size: WS
    angle: low
    side: {face: 3q_front, yaw_deg: 30}
    camera: {move: tilt_up, purpose: "reveal the hand above the climber", ease: in_out,
             distance_m: 5.5, height_m: 0.5, focal_mm: 24, aperture: 8, focus: subject,
             params: {tilt_deg: 35, over_s: 1.5, hero_distance_m: 4.5}}   # hero 4.5 m >= 0.8 x 5.5 m
    fit: "Hf at the hand 5.5 m: drawn hand (2.3 m) 0.42. Mint at 4.5 m (>= 0.8 x 5.5 = 4.4): Hf 4.5 m, h 0.21
          (WS); on-screen ratio 2.0. Block travel 2 m of a 9.8 m wide frame at the block (20 %)."
    key_frame: 30                   # the block slides out
    action_cue: {cue_id: hand_pull_2, tape_frame: 330, expect: {block_travel_m: 2.0}}
    frame_at_key: {grid: TC, min_area_pct: 20, max_occlusion_pct: 30, breaks_frame: true}
    transition_in: cut
    connects_from_prev: {type: eye_trace, screen_direction: R>L, eye_exit: TC, eye_enter: BR}
    sound:
      - {at: 10, kind: sfx, name: giant_knuckle_creak, source: designed, gain_db: -5}
      - {at: 30, kind: music_hit, name: reveal, source: library, gain_db: 0, sync: reveal}
      - {at: 30, kind: sfx, name: block_pop, source: designed, gain_db: -2}
    hook_count: 2
    evidence: [still@first, still@key, still@last, overlay]
    risk: "Hand not drawn (F5) or too small"
    fallback: "none"                # BLOCKED if the scout shows no hand
    needs: [{feature: hand_drawn_in_playback, status: verify}]

  - id: S04
    section: escalate
    kind: gameplay
    cut_in: 165
    cut_out: 195
    tape_in: 357
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: rook}
    also_in_frame: [gap, ocean_below]
    shows: [fall]
    learns: "The gap throws another climber off"
    feels: shock
    raises: stakes
    reveal: consequence
    cause_in: prev
    size: FS
    angle: high
    side: {face: 3q_front, yaw_deg: 45}
    camera: {move: static, purpose: "watch the slip from above", ease: none,
             distance_m: 1.7, height_m: 1.5, focal_mm: 35, aperture: 5.6, focus: subject, params: {}}
    fit: "Hf = 1.7 x 24/35 = 1.17 m: Rook 0.80 (FS); looking down, the gap below him in frame."
    key_frame: 15                   # he slips off the edge
    action_cue: {cue_id: rook_slips, tape_frame: 372}
    frame_at_key: {grid: MC, min_area_pct: 8, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: down, eye_exit: BC, eye_enter: MC}
    sound:
      - {at: 15, kind: sfx, name: small_yelp, source: designed, gain_db: -6}
    hook_count: null
    evidence: [strip, still@key]
    risk: "Bot does not step into the gap"
    fallback: "Pick the take report's nearest fall within 2 s; shift tape_in"
    needs: [{feature: take_report, status: fallback}]

  - id: S05
    section: escalate
    kind: gameplay
    cut_in: 195
    cut_out: 213
    tape_in: 446
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: hand, id: hand}
    also_in_frame: ["block:188"]
    shows: [hand, grip]
    learns: "The hand pinches a lower block"
    feels: threat
    raises: threat
    reveal: none
    cause_in: null
    size: FS
    angle: eye
    side: {face: profile, yaw_deg: 80}
    camera: {move: static, purpose: "show the next pull plainly", ease: none,
             distance_m: 7.0, height_m: 0.5, focal_mm: 35, aperture: 5.6, focus: subject, params: {}}
    fit: "Hf = 7.0 x 24/35 = 4.8 m: the whole drawn hand (2.3 m) 0.48 (FS) with the 2 m block under its fingers."
    key_frame: 9                    # the fingers close
    action_cue: {cue_id: hand_pinch_3, tape_frame: 455}
    frame_at_key: {grid: MC, min_area_pct: 20, max_occlusion_pct: 30, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: R>L, eye_exit: MC, eye_enter: MC}
    sound:
      - {at: 9, kind: sfx, name: giant_knuckle_creak_short, source: designed, gain_db: -4}
    hook_count: 3
    evidence: [still@key, overlay]
    risk: "Hand not drawn (F5)"
    fallback: "none"
    needs: [{feature: hand_drawn_in_playback, status: verify}]

  - id: S06
    section: escalate
    kind: gameplay
    cut_in: 213
    cut_out: 228
    tape_in: 463
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: mint}
    also_in_frame: ["block:209"]
    shows: [survivor_face]
    learns: "Her block starts to slide"
    feels: panic
    raises: stakes
    reveal: consequence
    cause_in: prev
    size: FS
    angle: eye
    side: {face: 3q_front, yaw_deg: 40}
    camera: {move: handheld, purpose: "panic as her footing goes", ease: none,
             distance_m: 1.7, height_m: 0.5, focal_mm: 35, aperture: 4, focus: subject,
             params: {noise: handheld_calm}}
    fit: "Hf = 1.7 x 24/35 = 1.17 m: Mint 0.80 (FS), feet and block edge in frame."
    key_frame: 7                    # the block lurches under her
    action_cue: {cue_id: mint_block_lurch, tape_frame: 470}
    frame_at_key: {grid: MC, min_area_pct: 10, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: R>L, eye_exit: MC, eye_enter: MC}
    sound:
      - {at: 0, kind: sfx, name: block_knock, source: designed, gain_db: -4}   # onset on the barrage cut
      - {at: 7, kind: sfx, name: candy_grind, source: designed, gain_db: -3}
    hook_count: null
    evidence: [strip, still@key]
    risk: "Lurch too small to read in 0.5 s"
    fallback: "Merge into S07's first frames"
    needs: []

  - id: S07
    section: payoff
    kind: gameplay
    cut_in: 228
    cut_out: 315
    tape_in: 468
    speed: [{at: 0, speed: 1.0}, {at: 15, speed: 0.5}]
    subject: {kind: tower, id: crown}
    also_in_frame: [hand, mint, rook, ocean]
    shows: [tower_over_ocean, fall, hand]
    learns: "The whole top topples into the sea"
    feels: awe
    raises: null
    reveal: payoff
    threat: hand
    cause_in: self                  # the hand lifts the block in frame
    size: EWS                       # survivors about 0.07 of frame height; hand about 0.16
    angle: low
    side: {face: front, yaw_deg: 0}
    camera: {move: static, purpose: "locked-off truth shot of the collapse", ease: none,
             distance_m: 80, height_m: 1.5, focal_mm: 135, aperture: 8, focus: subject,
             params: {pitch_deg: 33, frame_height_m: 14}}
    fit: "Hf = 80 x 24/135 = 14.2 m: crown top 10 m plus the hand above it (2.3 m) = 12.3 m fits; the break
          point is in the crown, inside frame; survivors 0.07. Only valid for a topple of the crown: an
          impact lower on the tower needs a new frame."
    key_frame: 12                   # the crown breaks free: on the main hit (frame 240)
    action_cue: {cue_id: crown_breaks, tape_frame: 480}
    frame_at_key: {grid: TC, min_area_pct: 25, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: R>L, eye_exit: BC, eye_enter: TC}
    sound:
      - {at: 0,  kind: silence, name: music_dropout, source: library, gain_db: -60}
      - {at: 0,  kind: sfx, name: single_creak, source: designed, gain_db: -6}     # onset on the barrage cut
      - {at: 12, kind: music_hit, name: main, source: library, gain_db: 0, sync: main}
      - {at: 12, kind: sfx, name: sub_boom, source: designed, gain_db: -2}
      - {at: 18, kind: sfx, name: blocks_cascade, source: designed, gain_db: -4}  # designed: shot is ramped
    hook_count: null
    evidence: [still@first, still@key, still@last, strip, overlay, audio@key]
    risk: "Hand missing or the crown framed too wide (hand under 5 % of frame)"
    fallback: "none"
    needs: [{feature: hand_drawn_in_playback, status: verify}]

  - id: S08
    section: aftermath
    kind: gameplay
    cut_in: 315
    cut_out: 345
    tape_in: 528
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: mint}
    also_in_frame: [ocean_below]
    shows: [survivor_face]
    learns: "She hangs on by one hand"
    feels: relief
    raises: null
    reveal: consequence
    cause_in: prev
    size: MS
    angle: high
    side: {face: 3q_front, yaw_deg: 30}
    camera: {move: static, purpose: "her near miss over the drop", ease: none,
             distance_m: 1.2, height_m: 1.3, focal_mm: 50, aperture: 4, focus: subject, params: {}}
    fit: "Hf = 1.2 x 24/50 = 0.58 m: waist up (MS), the drop below her in the lower third."
    key_frame: 12                   # she catches the edge
    action_cue: {cue_id: mint_grabs_edge, tape_frame: 540}
    frame_at_key: {grid: MC, min_area_pct: 15, max_occlusion_pct: 5, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: eye_trace, screen_direction: down, eye_exit: MC, eye_enter: MC}
    sound:
      - {at: 12, kind: sfx, name: grab_thud, source: take, gain_db: -4}
    hook_count: null
    evidence: [still@key, overlay]
    risk: "No survivor catches an edge in the take (a hang pose is unverified)"
    fallback: "Mint clinging flat to the stump's top block, same lens"
    needs: [{feature: survivor_ledge_hang, status: verify}]

  - id: S09
    section: title
    kind: gameplay
    cut_in: 345
    cut_out: 450
    tape_in: 570
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: tower, id: stump}
    also_in_frame: [hand, ocean]
    shows: []
    learns: "This is Deadly Jenga"
    feels: resolve
    raises: null
    reveal: none
    cause_in: null
    size: WS
    angle: high
    side: {face: 3q_front, yaw_deg: 40}
    camera: {move: crane_up, purpose: "lift away from the wreck under the title", ease: in_out,
             distance_m: 25, height_m: 20, focal_mm: 24, aperture: 8, focus: subject,
             params: {rise_m: 6, over_s: 3.5}}
    fit: "Hf = 25 m at the stump: stump about 0.4, hand 0.09 (too small to
          carry the shot; the stump does), sea around them; title over the sky."
    key_frame: 30                   # the last loose block splashes
    action_cue: {cue_id: last_block_splash, tape_frame: 600}
    frame_at_key: {grid: MC, min_area_pct: 10, max_occlusion_pct: 0, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: contrast_size, screen_direction: none, eye_exit: MC, eye_enter: MC}
    sound:
      - {at: 0,  kind: music_hit, name: title, source: library, gain_db: 0, sync: title}
      - {at: 0,  kind: ambience, name: ocean_wind, source: take, gain_db: -14}
      - {at: 30, kind: sfx, name: splash_far, source: designed, gain_db: -8}
    hook_count: null
    evidence: [still@first, still@last, title_safe]
    risk: "Busy picture behind the title"
    fallback: "Burn the titles in with ffmpeg drawtext at assembly; darken the picture 1 stop under them"
    needs: [{feature: export_titles, status: fallback}]   # see studio-tooling.md
```

### Example A against every lint

Body = frames 0-345 (11.5 s). Body-shot lengths (frames): 45, 60, 60, 30, 18, 15, 87, 30.

| Lint | Check | Result |
|------|-------|--------|
| L01 | 0-45-105-165-195-213-228-315-345-450; ends at 450 | pass |
| L02 | longest `learns` 9 words; none has "and" | pass |
| L03 | hook, teach, teach, escalate x3 (stakes, threat, stakes), payoff, aftermath, title | pass |
| L04 | S01 gameplay at frame 0 | pass |
| L05 | hand: S03 shows pull, subject hand, mint in frame, key at 135 = 4.5 s ≤ 5.0 | pass |
| L06 | median (30+45)/2 = 37.5 f = 1.25 s; shortest 15 f; longest 87 f ≥ 60; window [165, 315) holds cuts 165, 195, 213, 228 = 4 | pass |
| L07 | closing title at 345 (11.5 s, 77 %) in [330, 360]; card 105 f = 3.5 s in [90, 120] | pass |
| L08 | staged: first whole/payoff about the hand = S03 at 105; before it S01 partial 45 + S02 reaction 60 = 105 f ≥ 21. Causes: S04 prev = S03 (pull), S06 prev = S05 (grip), S07 self, S08 prev = S07 (payoff) | pass |
| L09 | first pull S01: 45 f, key 22 (22 and 23 from the ends), subject is a block | pass |
| L10 | keys from start / end: 22/23, 30/30, 30/30, 15/15, 9/9, 7/8, 12/75, 12/18, 30/75 | pass |
| L11 | onsets 1, 136, 239, 346 all within 2; S07 key 228 + 12 = 240 = main; title 345 = title hit; syncs at 0, 135, 240, 345 | pass |
| L12 | every key has a sound at the same frame; S02's creak is a `lead_in` | pass |
| L13 | S07 is ramped; its sounds are library or designed | pass |
| L14 | S01 INSERT, S02 MCU; S02 is the hero, 3q_front, yaw 25, MCU, reaction layer scream | pass |
| L15 | every move in the library with a purpose; `ease: none` only on static and handheld; S03 and S09 `in_out` | pass |
| L16 | no two consecutive shots share a subject | pass |
| L17 | no POV shots | pass |
| L18 | pull S01, hand S03, survivor_face S02, fall S04, tower_over_ocean S07 | pass |
| L19 | `verify` items (hand drawn, ledge hang) go to R01; `fallback` items have workarounds; no `missing`; one hand | pass (S03, S05, S07 BLOCKED if R01 fails) |
| L20 | caption 5 words needs 75 f, has 78; "DEADLY JENGA" needs 48 f, has 105; tagline 6 words needs 84 f, has 90 | pass |
| L21 | hook + teach (45, 60, 60) median 60; escalate (30, 18, 15) median 18 | pass |
| L22 | mean 43.1 f, std 23.1 f, CV 0.54 | pass |
| L23 | longest equal run: S02-S03 (2 shots) | pass |
| L24 | cuts on beats: 45, 105, 165, 195, 315, 345 = 6 of 8 = 75 % | pass |
| L25 | hook_count 1, 2, 3 | pass |
| L26 | pulls and the topple all R>L | pass |
| L27 | TC>MR, MR>BR, TC>MC, BC>MC (new_beat at S05), MC>MC, MC>TC, BC>MC, MC>MC: all distance ≤ 1 | pass |
| L28 | main hit 240 ≥ 0.6 x 345 = 207 | pass |
| L29 | coarse W/EW: S03, S04, S05, S06 (W) and S07 (EW) = 5 of 8 = 62 %; C: S01 = 12.5 % | pass |
| L30 | designed moves: S03 tilt_up, S09 crane_up = 2 of 9 = 22 % | pass |
| L31 | class 15: not required (a 12-frame stop-down exists anyway) | pass |
| L32 | one rule caption, 5 words; tagline 6 words; specific to the game | pass |
| L33 | S05 (18 f) ends at 213: knock at S06 frame 0; S06 (15 f) ends at 228: creak at S07 frame 0 | pass |
| L34 | pull 2 (core verb), hand 3, survivor_face 3, fall 2, grip 1, tower_over_ocean 1 | pass |
| L35 | WS 60, EWS 87, FS 30/18/15, MS 30, MCU 60, INSERT 45 | pass |
| L36, L37 | two-role only | n/a |
| L38 | every shot has a `fit` line | pass |
| L39 | this is the example itself | n/a |
| L40-L46 | revision 3, not applied to this legacy example (L40-L42 are mayhem or PvP only) | n/a |

What it still owes before capture: R01 on every shot (the hand must be visible in S03, S05, S07),
R02 on every cue (pulls of at least 2 m), the ledge-hang check for S08, and a person flying the
hand for the record take (or the directed hand from the pilot tooling).

---

## C. Worked example C: "Both Sides" (30 s, two-role montage, planning-only; legacy)

Moved here from `worked-examples.md` in revision 3; S04, S08 and S13 re-derived for the 2.3 m drawn hand.

Brief (short): 30 s, YouTube pre-roll and socials, VR players with friends. Must show the hand
player's point of view, the survivors' point of view, a pull that affects a survivor, multiplayer
with friends, the title. No music file and no engine access yet: **planning-only mode**.

Brief proofs (from `brief.yaml`): `pov_hand` = smoothed first-person from the hand player with its
fingers in frame; `pov_survivor` = smoothed first-person at survivor eye height (0.85 m);
`pull` = the hand moves a block out of its slot with a survivor on or beside it; `multiplayer` = 3 or
more survivors acting independently in one frame; `friends` = two players visibly answering each
other (a wave, a point, a voice line).

Concept: you climb as a survivor, then learn the giant hand is your friend; the same pinch is shown
from under it and from behind it; the cut ends with the two of them waving at each other. Arc:
*climb, pinch, wave*. Rejected: (1) open on the hand player's view at frame 0 and stay with the hand
for 10 s: sells one role first and makes the survivors props; (2) split screen of both views for the
whole cut: halves the picture on a phone and the Studio cannot composite it.

Memorable-image candidates: the same pinch from both sides (chosen); a tiny survivor waving up at
the hand under the title (chosen for the ending); a survivor riding the pulled block like a lift; the
hand politely pushing a fallen friend back onto the stump; a survivor's view of fingers closing over
the sun (used in S08).

```yaml
id: both-sides-30
version: 1
example: true
mode: planning_only                 # no music file, no engine: see the deferred list below
concept_type: two_role
opening: staged
threats: [hand]
fps: 30
length_frames: 900
class: 30
aspect: "16:9"
shape: montage
hook: "One friend is a giant hand pulling blocks from under the others"
hero: plum
cast: [plum, gus, tam, rin]
must_show: [pov_hand, pov_survivor, pull, multiplayer, friends]
camera_sensor_mm: 24
music:
  file: null                        # target hits for choosing and editing a track
  bpm: 100                          # target tempo; one beat = 18 frames
  meter: 4
  downbeat_frame: 0
  hits:
    - {name: open,   role: open,   frame: 0,   measured_onset_frame: unmeasured}
    - {name: reveal, role: reveal, frame: 216, measured_onset_frame: unmeasured}
    - {name: fall,   role: accent, frame: 270, measured_onset_frame: unmeasured}
    - {name: pinch,  role: accent, frame: 495, measured_onset_frame: unmeasured}
    - {name: main,   role: main,   frame: 594, measured_onset_frame: unmeasured}
    - {name: title,  role: title,  frame: 756, measured_onset_frame: unmeasured}
  stopdowns:
    - {from: 435, to: 456}          # breath: music out under the survivor's look up
titles:
  - {text: "YOUR FRIEND PLAYS THE HAND", role: caption, frame_in: 300, frame_out: 380, style: over_picture, closing: false}
  - {text: "DEADLY JENGA", role: title, frame_in: 756, frame_out: 900, style: over_picture, closing: true}
  - {text: "One friend pulls. Everyone else climbs.", role: tagline, frame_in: 786, frame_out: 900, style: over_picture, closing: false}
mix: {target_lufs: -15, true_peak_max: -1.0, title_vs_body_lu: -8, ramp_lu: 4}
studio: {ramp_sound: false}

shots:
  - {id: S01, section: hook, cut_in: 0, cut_out: 60, subject: {kind: blocks, id: tower_face}, also_in_frame: [plum_hands],
     shows: [pov_survivor, climbing], role_view: survivor, learns: "You climb a candy tower", reveal: none,
     size: MS, angle: low, side: {face: null, yaw_deg: null},
     camera: {move: pov, purpose: "be the climber", distance_m: 0.6, height_m: 0.85, focal_mm: 21, pov_smoothing: {pos_s: 0.3, rot_s: 0.45}},
     fit: "POV, vFOV 60 deg (21 mm). Hf at the block face 0.6 x 24/21 = 0.69 m: the top edge and her hands fill the upper half (block face cropped to about 60 %: MS).",
     key_frame: 36, sound: [{at: 0, kind: music_hit, name: open, sync: open}, {at: 36, kind: sfx, name: climb_grunt, source: designed}],
     connects_from_prev: {type: new_beat, eye_exit: TC}, needs: [{feature: survivor_pov_camera, status: verify}],
     fallback: "Follow at 1.8 m behind her shoulder, 35 mm (then pov_survivor moves to S08 only)"}

  - {id: S02, section: teach, cut_in: 60, cut_out: 114, subject: {kind: actor, id: tam}, also_in_frame: [plum, gus, rin, hand_fingertips],
     shows: [multiplayer, friends], learns: "Friends goof around on top", reveal: knows, threat: hand,
     size: WS, angle: eye, side: {face: 3q_front, yaw_deg: 35},
     camera: {move: static, purpose: "the audience sees the fingers; they do not", distance_m: 3.8, height_m: 1.0, focal_mm: 24},
     fit: "Hf at tam = 3.8 m: survivors 0.25 (WS), four of them across the frame. The fingertips 12 m behind them sit at the top edge (hand about 0.15 of frame), hazed.",
     key_frame: 27, sound: [{at: 27, kind: vo, name: "voice: made it!", source: designed, gain_db: -6}, {at: 20, kind: sfx, name: distant_knuckle_creak, source: designed, gain_db: -14}],
     connects_from_prev: {type: new_beat, eye_enter: TC, eye_exit: MC}, needs: [{feature: survivor_wave_emote, status: verify}],
     fallback: "Any visible gesture between two survivors from the take report"}

  - {id: S03, section: teach, cut_in: 114, cut_out: 165, subject: {kind: actor, id: plum}, also_in_frame: [sky],
     shows: [survivor_face], learns: "Plum's grin drops as she looks up", reveal: reaction, threat: hand,
     size: MCU, angle: low, side: {face: 3q_front, yaw_deg: 20}, reaction: "scream layer as the grin drops",
     camera: {move: static, purpose: "read the grin turn to fear", distance_m: 1.5, height_m: 0.3, focal_mm: 85},
     fit: "Hf = 1.5 x 24/85 = 0.42 m (MCU). Lens 0.3 m above her block, below her eyes, so she looks up past it.",
     key_frame: 30, sound: [{at: 30, kind: sfx, name: breath_catch, source: designed}],
     connects_from_prev: {type: reaction, eye_enter: MR, eye_exit: TR}, needs: []}

  - {id: S04, section: teach, cut_in: 165, cut_out: 255, subject: {kind: hand, id: hand}, also_in_frame: [gus, plum, "block:301"],
     shows: [hand, pull], learns: "The giant hand slides out the block under Gus", reveal: whole, threat: hand, hook_count: 1,
     size: WS, angle: low, side: {face: 3q_front, yaw_deg: 30},
     camera: {move: push_in, purpose: "close in as the fingers close", ease: in_out, distance_m: 6.5, height_m: 0.6, focal_mm: 28,
              params: {to_distance_m: 5.2, over_s: 3.0, hero_distance_m: 6.0, pitch_deg: 20}},
     fit: "Hf at the hand 6.5 x 24/28 = 5.6 m: drawn hand (2.3 m) 0.41. Gus at 6.0 m, just behind the hand (≥ 0.8 x 6.5 = 5.2): Hf 5.1 m, Gus 0.18 (WS); on-screen ratio 2.3. The 2 m slide is 20 % of the 9.9 m frame width. End of push: hand 5.2 m, Gus 4.7 m (≥ 4.2).",
     key_frame: 51, action_cue: {cue_id: hand_pull_gus, expect: {block_travel_m: 2.0}},
     sound: [{at: 40, kind: sfx, name: knuckle_creak, source: designed}, {at: 51, kind: music_hit, name: reveal, sync: reveal}, {at: 51, kind: sfx, name: block_scrape_pop, source: designed}],
     connects_from_prev: {type: eye_trace, screen_direction: L>R, eye_enter: TR, eye_exit: MR},
     needs: [{feature: hand_drawn_in_playback, status: verify}], fallback: "none: BLOCKED if the scout shows no hand"}

  - {id: S05, section: escalate, raises: stakes, cut_in: 255, cut_out: 300, subject: {kind: actor, id: gus}, also_in_frame: [gap, ocean_below],
     shows: [fall], learns: "Gus drops through the gap", reveal: consequence, cause_in: prev,
     size: FS, angle: high, side: {face: 3q_front, yaw_deg: 40},
     camera: {move: static, purpose: "the drop below him", distance_m: 1.8, height_m: 1.6, focal_mm: 35},
     fit: "Hf = 1.8 x 24/35 = 1.23 m: Gus 0.76 (FS), looking down 50 deg so the sea is under him.",
     key_frame: 15, sound: [{at: 15, kind: music_hit, name: fall, sync: fall}, {at: 15, kind: sfx, name: yelp, source: designed}],
     connects_from_prev: {type: cause_effect, screen_direction: down, eye_enter: MC, eye_exit: BC}, needs: [],
     fallback: "Nearest fall in the take report within 2 s"}

  - {id: S06, section: escalate, raises: novelty, cut_in: 300, cut_out: 345, subject: {kind: hand, id: hand}, also_in_frame: [gus, "block:301"],
     shows: [pov_hand], role_view: hand, learns: "The hand is a player watching Gus fall", reveal: none, hook_count: 2,
     size: MS, angle: high, side: {face: null, yaw_deg: null},
     camera: {move: pov, purpose: "now be the hand", distance_m: 2.5, height_m: 3.0, focal_mm: 19, pov_smoothing: {pos_s: 0.3, rot_s: 0.45}},
     fit: "POV, vFOV 65 deg (19 mm). Hf at the fingers 2.5 x 24/19 = 3.2 m: fingers and the 2 m block fill the upper half (hand cropped to about 60 %: MS); Gus about 15 m below at about 0.05.",
     key_frame: 20, sound: [{at: 20, kind: sfx, name: block_release_whoosh, source: designed}],
     connects_from_prev: {type: match_motion, screen_direction: down, eye_enter: BC, eye_exit: TC},
     needs: [{feature: hand_player_pov_camera, status: verify}], fallback: "Over-the-wrist follow, 3 m behind the fingers, 24 mm"}

  - {id: S07, section: escalate, raises: count, cut_in: 345, cut_out: 435, subject: {kind: actor, id: rin}, also_in_frame: [tam, plum, hand],
     shows: [multiplayer, climbing], learns: "Everyone scatters down the tower", reveal: none,
     size: WS, angle: eye, side: {face: profile, yaw_deg: 80},
     camera: {move: pan, purpose: "follow Rin's jump down a level", ease: in_out, distance_m: 6.5, height_m: 2.0, focal_mm: 35, params: {pan_deg: 25, over_s: 3.0}},
     fit: "Hf = 6.5 x 24/35 = 4.5 m: survivors 0.21 (WS). Pan 25 deg in 3 s on a 62.8 deg wide frame = 0.13 frame widths/s (limit 0.18).",
     key_frame: 45, sound: [{at: 45, kind: sfx, name: landing_thump, source: take}],
     connects_from_prev: {type: new_beat, eye_enter: MC, eye_exit: ML}, needs: []}

  - {id: S08, section: breath, cut_in: 435, cut_out: 480, subject: {kind: hand, id: hand}, also_in_frame: [sky],
     shows: [pov_survivor], role_view: survivor, same_event: pinch_plum, learns: "Fingers close over the sky above Plum", reveal: none,
     size: FS, angle: low, side: {face: null, yaw_deg: null},
     camera: {move: pov, purpose: "the hand from underneath", distance_m: 4.0, height_m: 0.85, focal_mm: 21, pov_smoothing: {pos_s: 0.3, rot_s: 0.45}},
     fit: "POV up 70 deg. Hf at the fingers 4.0 x 24/21 = 4.6 m: the whole drawn hand (2.3 m) 0.50 (FS by the subject-fraction rule), sun behind it.",
     key_frame: 20, sound: [{at: 0, kind: silence, name: music_dropout}, {at: 20, kind: sfx, name: single_creak, source: designed}],
     connects_from_prev: {type: eye_trace, eye_enter: MC, eye_exit: TC}, needs: [{feature: survivor_pov_camera, status: verify}]}

  - {id: S09, section: escalate, raises: stakes, cut_in: 480, cut_out: 510, subject: {kind: actor, id: plum}, also_in_frame: [hand, "block:288"],
     shows: [pov_hand, grip], role_view: hand, same_event: pinch_plum, learns: "From above, the fingers pinch her block", reveal: none,
     size: WS, angle: high, side: {face: null, yaw_deg: null},
     camera: {move: pov, purpose: "the same pinch from the hand's side", distance_m: 6.0, height_m: 3.0, focal_mm: 19, pov_smoothing: {pos_s: 0.3, rot_s: 0.45}},
     fit: "POV down. Hf at Plum 6.0 x 24/19 = 7.6 m: Plum 0.12 (WS by the subject-fraction rule), the fingers on her block at the top third.",
     key_frame: 15, sound: [{at: 15, kind: music_hit, name: pinch, sync: pinch}, {at: 15, kind: sfx, name: grip_squeak, source: designed}],
     connects_from_prev: {type: match_cut, eye_enter: TC, eye_exit: BC}, needs: [{feature: hand_player_pov_camera, status: verify}]}

  - {id: S10, section: escalate, raises: speed, cut_in: 510, cut_out: 534, subject: {kind: actor, id: plum}, also_in_frame: ["block:288"],
     shows: [], learns: "Plum leaps clear as her block slides", reveal: consequence, cause_in: prev,
     size: FS, angle: eye, side: {face: 3q_front, yaw_deg: 30},
     camera: {move: handheld, purpose: "panic", distance_m: 1.9, height_m: 0.5, focal_mm: 35, params: {noise: handheld_panic}},
     fit: "Hf = 1.9 x 24/35 = 1.30 m: Plum 0.72 (FS); her block exits frame right.",
     key_frame: 12, sound: [{at: 12, kind: sfx, name: jump_whoosh, source: designed}],
     connects_from_prev: {type: cause_effect, screen_direction: L>R, eye_enter: MC, eye_exit: BR}, needs: []}

  - {id: S11, section: escalate, raises: threat, cut_in: 534, cut_out: 555, subject: {kind: block, id: "block:276"},
     shows: [], learns: "Her block knocks the next one loose", reveal: consequence, cause_in: self,
     size: INSERT, angle: eye, side: {face: profile, yaw_deg: 90},
     camera: {move: static, purpose: "the chain reaction", distance_m: 3.5, height_m: 1.0, focal_mm: 50},
     fit: "Hf = 3.5 x 24/50 = 1.68 m: the 2 m block end overfills the height (INSERT).",
     key_frame: 9, sound: [{at: 9, kind: sfx, name: block_knock, source: designed}],
     connects_from_prev: {type: cause_effect, screen_direction: L>R, eye_enter: MR, eye_exit: MC}, needs: []}

  - {id: S12, section: escalate, raises: count, cut_in: 555, cut_out: 573, subject: {kind: actor, id: tam}, also_in_frame: [rin],
     shows: [fall], learns: "Tam slides down the tilting crown", reveal: consequence, cause_in: self,
     size: WS, angle: high, side: {face: 3q_front, yaw_deg: 45},
     camera: {move: handheld, purpose: "the crown tilts under them", distance_m: 3.4, height_m: 1.5, focal_mm: 24, params: {noise: handheld_panic}},
     fit: "Hf = 3.4 m: Tam 0.28 (WS); the tilting block edge crosses the frame.",
     key_frame: 8, sound: [{at: 0, kind: sfx, name: grind_hit, source: designed}, {at: 8, kind: sfx, name: yelp_two, source: designed}],
     connects_from_prev: {type: new_beat, eye_enter: MC, eye_exit: BC}, needs: []}

  - {id: S13, section: payoff, cut_in: 573, cut_out: 660, subject: {kind: tower, id: crown}, also_in_frame: [hand, tam, rin],
     shows: [hand], learns: "The crown folds over toward us", reveal: payoff, threat: hand, cause_in: self, hook_count: 3,
     size: WS, angle: low, side: {face: front, yaw_deg: 0}, transition_in: smash_cut,
     camera: {move: static, purpose: "debris falls toward the lens", distance_m: 15, height_m: 0.5, focal_mm: 18, params: {pitch_deg: 60, ledge_out_m: 4}},
     fit: "From a ledge 15 m below the crown top, 4 m out, up 60 deg: Hf at 15 m = 15 x 24/18 = 20 m, so the 10 m crown, the hand above it (2.3 m) and sky fit. Survivors falling past about 3 m from the lens: Hf 4 m, h 0.24 (WS).",
     key_frame: 21, action_cue: {cue_id: hand_pulls_last_support, expect: {block_travel_m: 2.0}},
     sound: [{at: 0, kind: sfx, name: crack, source: designed}, {at: 21, kind: music_hit, name: main, sync: main}, {at: 21, kind: sfx, name: sub_boom, source: designed}, {at: 30, kind: sfx, name: debris_rush, source: designed}],
     connects_from_prev: {type: new_beat, screen_direction: toward, eye_enter: TC, eye_exit: BC},
     needs: [{feature: hand_drawn_in_playback, status: verify}, {feature: camera_collision_off_for_debris, status: verify}], fallback: "none"}

  - {id: S14, section: aftermath, cut_in: 660, cut_out: 705, subject: {kind: actor, id: plum}, also_in_frame: [floating_blocks, hand],
     shows: [friends], learns: "Plum laughs up at the hand from the water", reveal: consequence, cause_in: prev,
     size: MS, angle: eye, side: {face: front, yaw_deg: 15},
     camera: {move: static, purpose: "she is fine and wants a rematch", distance_m: 0.8, height_m: 0.2, focal_mm: 35},
     fit: "Hf = 0.8 x 24/35 = 0.55 m: waist up (MS); her pointing arm leads to the top edge.",
     key_frame: 20, sound: [{at: 20, kind: vo, name: "voice: no way!", source: designed, gain_db: -6}],
     connects_from_prev: {type: cause_effect, eye_enter: MC, eye_exit: TC},
     needs: [{feature: survivor_floats_in_water, status: verify}], fallback: "Plum on the stump's lowest block, same lens"}

  - {id: S15, section: aftermath, cut_in: 705, cut_out: 756, subject: {kind: actor, id: gus}, also_in_frame: [tam, stump],
     shows: [climbing], learns: "Gus climbs straight back up the stump", reveal: none,
     size: WS, angle: low, side: {face: 3q_back, yaw_deg: 140},
     camera: {move: static, purpose: "they go again", distance_m: 3.0, height_m: 1.0, focal_mm: 24},
     fit: "Hf = 3.0 m: Gus 0.31 (WS), two blocks of the stump in frame.",
     key_frame: 20, sound: [{at: 20, kind: sfx, name: climb_grip, source: take}],
     connects_from_prev: {type: new_beat, eye_enter: MC, eye_exit: TC}, needs: []}

  - {id: S16, section: title, cut_in: 756, cut_out: 900, subject: {kind: actor, id: plum}, also_in_frame: [hand, gus, tam, stump],
     shows: [friends, pov_hand], role_view: hand, learns: "The two friends wave at each other", reveal: none,
     size: WS, angle: overhead, side: {face: null, yaw_deg: null},
     camera: {move: pov, purpose: "end on the hand's side of the friendship", distance_m: 5.0, height_m: 5.0, focal_mm: 19, pov_smoothing: {pos_s: 0.3, rot_s: 0.45}},
     fit: "POV straight down 5 m: Hf 5 x 24/19 = 6.3 m: Plum 0.15 (WS by the subject-fraction rule) at the centre, the fingers across the top third; title sits over the sea, below the stump.",
     key_frame: 30, sound: [{at: 0, kind: music_hit, name: title, sync: title}, {at: 30, kind: sfx, name: finger_wiggle_creak, source: designed, gain_db: -10}],
     connects_from_prev: {type: contrast_size, eye_enter: MC, eye_exit: MC},
     needs: [{feature: performed_finger_wave, status: verify}, {feature: export_titles, status: fallback}],
     fallback: "Plum waves alone; the hand hovers"}
```

Fields omitted above for space (`kind: gameplay`, `tape_in`, `speed: 1.0`, `ease: none` on static,
`frame_at_key`) take their defaults; a real plan writes them once the take exists.

### C against every lint

Body = frames 0-756 (25.2 s). Body-shot lengths: 60, 54, 51, 90, 45, 45, 90, 45, 30, 24, 21, 18,
87, 45, 51.

| Lint | Check | Result |
|------|-------|--------|
| L01 | 0-60-114-165-255-300-345-435-480-510-534-555-573-660-705-756-900 | pass |
| L02 | one subject each; longest `learns` 9 words; none has " and " | pass |
| L03 | hook, teach x3, escalate x2, escalate, breath, escalate x4, payoff, aftermath x2, title; every escalate has `raises` | pass |
| L04 | S01 gameplay at 0 | pass |
| L05 | two-role: S04 shows pull, subject hand, Gus in frame, key 165 + 51 = 216 = 7.2 s ≤ 8.0 | pass |
| L06 | median 45 f (1.5 s); shortest 18; longest 90 ≥ 90; window [480, 630) holds 480, 510, 534, 555, 573 = 5 | pass |
| L07 | closing title 756 (84 %) in [720, 780]; card 144 in [120, 180] | pass |
| L08 | hand first whole S04 at 165: earlier S02 knows 54 + S03 reaction 51 = 105 ≥ 21. Causes: S05 prev S04 (pull); S10 prev S09 (grip); S11, S12, S13 self; S14 prev S13 (payoff) | pass |
| L09 | first pull S04: 90 f, key 51 (51 and 39 from the ends), Gus in frame | pass |
| L10 | keys from start/end: 36/24, 27/27, 30/21, 51/39, 15/30, 20/25, 45/45, 20/25, 15/15, 12/12, 9/12, 8/10, 21/66, 20/25, 20/31, 30/114 | pass |
| L11 | planning-only: measured-onset clause **deferred**; target syncs: open 0, reveal 216, fall 270, pinch 495, main 573 + 21 = 594, title 756 | pass (targets) |
| L12 | every key has a sound at the key frame | pass |
| L13 | no ramps | pass |
| L14 | S03 MCU; hero Plum, 3q_front, yaw 20, reaction layer scream | pass |
| L15 | moves in the library with purposes; push_in and pan `in_out`; POVs smoothed | pass |
| L16 | consecutive same subject only S09-S10 (Plum), and S09 is a POV: skipped | pass |
| L17 | POV smoothing 0.3 / 0.45 on S01, S06, S08, S09, S16 | pass |
| L18 | pov_hand S06, pov_survivor S01, pull S04, multiplayer S02, friends S02 (wave + voice) | pass |
| L19 | all `verify` items go to the scout list; no `missing`; one hand only | pass |
| L20 | caption 5 words needs 75, has 80; title 144 ≥ 48; tagline 6 words needs 84, has 114 | pass |
| L21 | hook + teach (60, 54, 51, 90) median 57; escalate (45, 45, 90, 30, 24, 21, 18) median 30 | pass |
| L22 | mean 50.4 f, std 22.7 f, CV 0.45 | pass |
| L23 | longest run within 2 frames: S05-S06 (2) | pass |
| L24 | cuts on the 18-frame grid: only 756 = 1 of 15 | pass |
| L25 | hook_count 1, 2, 3 | pass |
| L26 | S04 > S10 > S11 slide L>R; S05 > S06 fall down | pass |
| L27 | all pairs distance ≤ 1, except S06 > S07 (new_beat), S12 > S13 (smash_cut) | pass |
| L28 | main 594 ≥ 0.6 x 756 = 454 | pass |
| L29 | W/EW: S02, S04, S05, S07, S08, S09, S10, S12, S13, S15 = 10 of 15 = 67 %; close S11 = 7 % | pass |
| L30 | designed moves S04 push_in, S07 pan = 2 of 16 = 12.5 % | pass |
| L31 | stop-down 435-456 = 21 f ≥ 9 | pass |
| L32 | caption 5 words, a rule; tagline 6 words; no superlatives | pass |
| L33 | S11 (21 f) ends at 555: S12 sound at 0; S12 (18 f) ends at 573: S13 sound at 0 | pass |
| L34 | pov_hand 3, friends 3, climbing 3, others ≤ 2; pull 1 | pass |
| L35 | WS ≥ 18, FS ≥ 24, MS ≥ 45, MCU 51, INSERT 21 | pass |
| L36 | hand-player view S06 at 300 and survivor view S01 at 0, both before 378 (half the body) | pass |
| L37 | same pinch from both sides: S08 (survivor) and S09 (hand) adjacent | pass |
| L38 | every shot has a `fit` line | pass |
| L39 | new tagline, caption, hero, cast; no camera tuple equal to A's or B's | pass |
| L40-L46 | revision 3, not applied to this legacy example | n/a |

### What planning-only mode left owed (from C)

Deferred, not failed: stage 2 (no music file), L11's measured-onset clause, every R-lint, stage 5
onward. **To verify at scout:** survivor POV camera, hand-player POV camera (where the hand player's
eye sits is unknown), hand drawn in playback (fixed only on the pilot branch), survivor wave emote,
survivor floating in water, performed finger wave, camera collision for S13, the pull's 2 m travel.
**Before any render:** (1) choose or cut a track to the target hits and map it (stage 2); (2) move
the picture to the measured hits, never the hits to the picture; (3) re-run L11 with measured
onsets; (4) scout every `verify` item, applying fallbacks or declaring BLOCKED; (5) re-run the
stage-4 critic if any key moved more than 6 frames or any shot fell back.
