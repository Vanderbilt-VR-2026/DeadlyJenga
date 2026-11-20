# Worked examples B and C (and how to use any example)

Open at stage 1 (to see how different concepts can be) and stage 3 (to see how a plan is written
and linted). Example A (15 s hand reveal, montage, staged) is in `plan-schema-and-lints.md` section 3.
This file holds B (15 s disaster cut, single take, cold open, ends on a card) and C (30 s two-role
montage, planning-only mode, ends on the hand player's view). All three pass every lint as revised
on 2026-11-20; the check tables are below each plan.

## 0. Examples are not templates

Skill test 1 (`../../SKILL-TEST-1.md`) found that all four planners cloned the one example: the same
80 m / 135 mm collapse, the same one-hand hang, the same hero name, the same tagline. Two of those
copied frames could not show what their new shot needed. So:

- **Do not reuse** an example's tagline, caption, hero id or cast ids (lint L39, hard). Reserved:
  - taglines: "Don't be the one who falls." (A), "The tower was the easy part." (B),
    "One friend pulls. Everyone else climbs." (C)
  - captions: "THE HAND IS ANOTHER PLAYER" (A), "STAY ON THE TOWER" (B), "YOUR FRIEND PLAYS THE HAND" (C)
  - hero and cast ids: mint, rook (A); coral, finn, ivo (B); plum, gus, tam, rin (C)
- **Do not copy camera numbers.** Every shot's `fit` line derives its frame from what that shot must
  show (lint L38). If your derivation lands on an example's numbers, fine, but the `fit` line must
  show the arithmetic for your subject (L39 warns on identical distance, height and focal).
- **Copy the method, not the spine.** A: partial, reaction, whole, barrage, long-lens collapse,
  near miss, crane-up title. B: threat at frame 0, consequences escalate inside one move, face after,
  hard cut to a card. C: first-person climb, audience-knows, same pinch from both sides, collapse
  toward the lens, title over the hand player's view. A fourth concept should differ from all three
  in at least two of: shape, opening, payoff, ending.

Everything here assumes the attempt-1 castle map (56 m tower on a 25 m castle) unless a line says
otherwise. On any other map re-derive every `fit` line from that map's measured heights.

---

## 1. Example B: "Already Here" (15 s, disaster, single take, cold open)

Brief (short): 15 s social, muted-first; the disaster is the star; must show the disaster hitting the
tower, survivors reacting, the consequence going into the sea, the title. No hand (the brief does not
ask for one, so none is added).

Concept: the wave is already standing when the trailer starts (cold open); one continuous camera
pulls back as it arrives, so every escalation is a consequence we watch grow. Arc: *already, closer,
gone*. Rejected: (1) a staged montage from a calm sea, effect, notice, then the wave: it spends 3 s
before the only image this brief cares about; (2) tornado lifting blocks off the crown: the tornado's
reach on this map is unknown and its funnel reads small at survivor scale.

Memorable-image candidates considered: a wall of water standing over three tiny climbers at frame 0
(chosen); a block surfing on the crest; the hero surfacing beside her own block; the tower's shadow
swallowed by the wave's; the wave stopping just short (rejected: false to the game).

```yaml
id: already-here-15
version: 1
mode: production                    # music file and engine available
concept_type: disaster
opening: cold_open                  # the threat itself at frame 0
cold_open_threat: "hazard:tsunami"
threats: ["hazard:tsunami"]
fps: 30
length_frames: 450
class: 15
aspect: "9:16"                      # social-first; reframe a 16:9 later per shot
shape: single_take
hook: "Disasters hit the tower while friends are still climbing it"
hero: coral
cast: [coral, finn, ivo]            # bots or performed survivors
must_show: [hazard_hit, survivors_react, into_sea]
camera_sensor_mm: 24
music:
  file: Music/already_here_90.wav
  bpm: 90                           # one beat = 20 frames
  meter: 4
  downbeat_frame: 0
  hits:
    - {name: open,  role: open,   frame: 0,   measured_onset_frame: 1}
    - {name: hit,   role: accent, frame: 140, measured_onset_frame: 141}
    - {name: main,  role: main,   frame: 220, measured_onset_frame: 219}
    - {name: title, role: title,  frame: 340, measured_onset_frame: 341}
  stopdowns:
    - {from: 205, to: 220}          # music out for 0.5 s; only the sea's roar
titles:
  - {text: "STAY ON THE TOWER", role: caption, frame_in: 40, frame_out: 110, style: over_picture, closing: false}
  - {text: "DEADLY JENGA", role: title, frame_in: 340, frame_out: 450, style: card, closing: true}
  - {text: "The tower was the easy part.", role: tagline, frame_in: 360, frame_out: 450, style: card, closing: false}
hazard_set: {type: tsunami, height_m: 40, warning_s: 0, offshore_m: 75, speed_m_s: 16}   # Tsunami parameters (defaults 22 m, 5 s, 160 m, 8 m/s); 75 m / 16 m/s = 4.7 s to arrive; verify at scout
mix: {target_lufs: -15, true_peak_max: -1.0, title_vs_body_lu: -6, ramp_lu: 4}
studio: {ramp_sound: false}

shots:
  - id: S01
    section: hook                   # the take; its beats are moments
    kind: gameplay
    cut_in: 0
    cut_out: 280
    tape_in: 60
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: hazard, id: "hazard:tsunami"}
    also_in_frame: [coral, finn, ivo, "tower:lower_section"]
    shows: [hazard_hit, survivors_react, into_sea]
    learns: "A wave takes the section they stand on"
    size: EWS                       # at the key frame (survivors 0.063 of frame height)
    angle: eye
    side: {face: 3q_back, yaw_deg: 150}
    camera: {move: pull_out, purpose: "start inside the danger, end on what it costs", ease: in_out,
             distance_m: 3.0, height_m: 0.7, focal_mm: 24, aperture: 8, focus: subject,
             params: {to_distance_m: 15, rise_m: 4, pitch_deg_from: 15, pitch_deg_to: -5, from_frame: 70, to_frame: 220}}
    fit: "Start: lens on a lower section about 10 m above the sea. Hf at the climbers = 3.0 x 24/24 = 3.0 m,
          climbers 0.31 (WS). Wave top 30 m above the lens at 75 m = 21.8 deg up; frame spans -11.5 to +41.5
          deg at pitch +15, so the crest sits at y 0.63, over them. Frame 140: lens about 8 m out,
          pitch about +5 deg, frame bottom about 1 m under their block, so the block beneath them is in
          frame. Key 220: 15 m out, Hf 15 m, climbers 0.063 (EWS), their 12 m section 0.8 of frame height."
    key_frame: 220
    moments:
      - {at: 0,   section: hook,     reveal: whole,       threat: "hazard:tsunami", hook_count: 1,
         what: "A wall of water already stands over three climbers", sound_at: 0}
      - {at: 50,  section: teach,    reveal: reaction,    threat: "hazard:tsunami", shows: [survivors_react],
         what: "The climbers turn to face it", sound_at: 51}
      - {at: 100, section: escalate, raises: scale,      reveal: none, hook_count: 2,
         what: "The crest grows taller as it closes in", sound_at: 100}
      - {at: 140, section: escalate, raises: stakes,     reveal: consequence, cause_in: self, shows: [hazard_hit], hook_count: 3,
         what: "It rips out the blocks beneath them", sound_at: 140}
      - {at: 180, section: escalate, raises: count,      reveal: consequence, cause_in: self, shows: [into_sea],
         what: "Two climbers are thrown into the sea", sound_at: 181}
      - {at: 220, section: payoff,   reveal: payoff,      threat: "hazard:tsunami", cause_in: self, shows: [into_sea],
         what: "Their whole block tips into the sea", sound_at: 220}
    action_cue: {cue_id: tsunami_set, tape_frame: 60, expect: {reaches_section_at_tape_frame: 200}}
    frame_at_key: {grid: MC, min_area_pct: 25, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: R>L, eye_enter: null, eye_exit: MC}
    sound:
      - {at: 0,   kind: music_hit, name: open, source: library, sync: open}
      - {at: 0,   kind: sfx, name: wave_roar_bed, source: designed, gain_db: -8}
      - {at: 51,  kind: sfx, name: small_gasps, source: designed, gain_db: -10}
      - {at: 100, kind: sfx, name: sea_surge, source: designed, gain_db: -6}
      - {at: 140, kind: music_hit, name: hit, source: library, sync: hit}
      - {at: 140, kind: sfx, name: water_impact_blocks, source: designed, gain_db: -2}
      - {at: 181, kind: sfx, name: yelps_far, source: designed, gain_db: -8}
      - {at: 205, kind: silence, name: music_dropout, source: library}
      - {at: 220, kind: music_hit, name: main, source: library, sync: main}
      - {at: 220, kind: sfx, name: sub_boom_splash, source: designed, gain_db: -2}
    needs:
      - {feature: tsunami_reaches_section, status: verify}     # height and timing in the scout dry run
      - {feature: survivors_thrown_by_wave, status: verify}    # TsunamiHazard pushes and lifts; check it in the take
    fallback: "none: BLOCKED if the wave cannot reach a section with survivors on it"

  - id: S02
    section: aftermath
    kind: gameplay
    cut_in: 280
    cut_out: 340
    tape_in: 380
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: coral}
    also_in_frame: [floating_blocks]
    shows: [survivors_react]
    learns: "She surfaces among the floating candy blocks"
    reveal: consequence
    cause_in: prev
    size: MCU
    angle: eye
    side: {face: front, yaw_deg: 10}
    camera: {move: static, purpose: "her face after the loss", ease: none,
             distance_m: 1.6, height_m: 0.15, focal_mm: 85, aperture: 2.8, focus: subject}
    fit: "Hf = 1.6 x 24/85 = 0.45 m: head and shoulders (MCU 0.35-0.48). Camera 0.15 m above the water,
          eyes at y about 0.65."
    key_frame: 15                   # she breaks the surface, gasping
    action_cue: {cue_id: coral_surfaces, tape_frame: 395}
    frame_at_key: {grid: MC, min_area_pct: 12, max_occlusion_pct: 5, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: none, eye_enter: MC, eye_exit: MC}
    sound:
      - {at: 15, kind: sfx, name: surface_gasp, source: designed, gain_db: -4}
    needs: [{feature: survivor_floats_in_water, status: verify}]
    fallback: "Coral clinging to the stump's lowest block, same lens, 1.6 m"

  - id: S03
    section: title
    kind: text
    cut_in: 340
    cut_out: 450
    subject: {kind: point, id: card}
    shows: []
    learns: "This is Deadly Jenga"
    reveal: none
    size: WS
    angle: eye
    camera: {move: static, purpose: "a clean card on the title hit", ease: none}
    fit: "Card: no camera."
    key_frame: 0
    transition_in: cut
    connects_from_prev: {type: contrast_size, screen_direction: none, eye_enter: MC, eye_exit: MC}
    sound:
      - {at: 0, kind: music_hit, name: title, source: library, sync: title}
      - {at: 0, kind: ambience, name: waves_lapping, source: library, gain_db: -16}
    needs: [{feature: export_titles, status: fallback}]
    fallback: "Card built in the ffmpeg assembly step"
```

### B against every lint

Body = frames 0-340 (11.33 s). Body shots: S01 280, S02 60.

| Lint | Check | Result |
|------|-------|--------|
| L01 | 0-280-340-450 | pass |
| L02 | one subject per shot; longest `learns`/`what` 9 words; no " and " | pass |
| L03 | moments hook, teach, escalate x3 (scale, stakes, count), payoff; then aftermath, title | pass |
| L04 | S01 gameplay at 0 | pass |
| L05 | disaster: first `hazard_hit` with tower and cast in frame: moment 140 = 4.67 s ≤ 5.0 | pass |
| L06 | single take: S01 280 / 340 = 82 % ≥ 70 %; cuts inside the body: 280 only | pass |
| L07 | closing title 340 in [330, 360]; card 110 in [90, 120] | pass |
| L08 | tsunami's first whole view at frame 0 with `opening: cold_open`: held in an uncut 280-frame take; escalate consequences at 140 (stakes) and 180 (count) differ. Causes: 140, 180, 220 self; S02 prev = S01 payoff | pass |
| L09 | disaster: first `hazard_hit` (140) is 140 frames from the take's start, 140 from its end | pass |
| L10 | S01 key 220 (60 from end); S02 15/45; S03 key 0 on the title hit | pass |
| L11 | onsets 1, 141, 219, 341 within 2; main moment 220 = main; title 340 = title hit; syncs at 0, 140, 220, 340 | pass |
| L12 | moments 0/0, 50/51, 100/100, 140/140, 180/181, 220/220; S02 15/15; S03 0/0 | pass |
| L13 | no ramps | pass |
| L14 | S02 MCU; hero coral, front, yaw 10 | pass |
| L15 | pull_out in_out with purpose; statics `none` | pass |
| L16 | subjects tsunami, coral, card | pass |
| L17 | no POV | pass |
| L18 | hazard_hit (140), survivors_react (50, S02), into_sea (180, 220) | pass |
| L19 | `verify` items have scout checks; S01's failure is BLOCKED, S02 has a fallback; nothing the game cannot do | pass |
| L20 | caption 4 words needs 66, has 70; title 110 ≥ 48; tagline 6 words needs 84, has 90 | pass |
| L21-L23, L24, L30, L33 | montage only | n/a |
| L25 | hook_count 1, 2, 3 | pass |
| L26 | one wave direction (R>L) inside one take | pass |
| L27 | S01 exit MC > S02 MC; S02 MC > S03 MC | pass |
| L28 | main 220 ≥ 0.6 x 340 = 204 | pass |
| L29 | coarse W/EW 1 of 2 body shots = 50 %; close 0 % | pass |
| L31 | class 15: optional (one 15-frame stop-down) | pass |
| L32 | caption 4 words, a rule; tagline 6 words; no superlatives | pass |
| L34 | hazard_hit 1, survivors_react 2, into_sea 1 | pass |
| L35 | EWS 280, MCU 60 | pass |
| L36, L37 | two-role only | n/a |
| L38 | every shot has a `fit` line deriving Hf for its subject | pass |
| L39 | new tagline, caption, hero, cast; no camera tuple equal to A's or C's | pass |

What it still owes before capture: R01 (climbers at 0.063 must be visible as figures, not
specks, at preview size; if not, end the pull-out at 12 m), R02 (the wave reaches the section at
tape frame 200 within 8 frames), and S02's floating check.

---

## 2. Example C: "Both Sides" (30 s, two-role montage, planning-only mode)

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
     size: MCU, angle: low, side: {face: 3q_front, yaw_deg: 20},
     camera: {move: static, purpose: "read the grin turn to fear", distance_m: 1.5, height_m: 0.3, focal_mm: 85},
     fit: "Hf = 1.5 x 24/85 = 0.42 m (MCU). Lens 0.3 m above her block, below her eyes, so she looks up past it.",
     key_frame: 30, sound: [{at: 30, kind: sfx, name: breath_catch, source: designed}],
     connects_from_prev: {type: reaction, eye_enter: MR, eye_exit: TR}, needs: []}

  - {id: S04, section: teach, cut_in: 165, cut_out: 255, subject: {kind: hand, id: hand}, also_in_frame: [gus, plum, "block:301"],
     shows: [hand, pull], learns: "The giant hand slides out the block under Gus", reveal: whole, threat: hand, hook_count: 1,
     size: WS, angle: low, side: {face: 3q_front, yaw_deg: 30},
     camera: {move: push_in, purpose: "close in as the fingers close", ease: in_out, distance_m: 6.5, height_m: 0.6, focal_mm: 28,
              params: {to_distance_m: 5.2, over_s: 3.0, hero_distance_m: 4.5, pitch_deg: 20}},
     fit: "Hf at the hand 6.5 x 24/28 = 5.6 m: hand (about 3 m visible) 0.54. Gus at 4.5 m (≥ 0.6 x 6.5 = 3.9): Hf 3.9 m, Gus 0.24 (WS); ratio about 2.9. The 2 m slide is 20 % of the 9.9 m frame width. End of push: hand 5.2 m, Gus 3.6 m (≥ 3.1).",
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
     fit: "POV up 70 deg. Hf at the fingers 4.0 x 24/21 = 4.6 m: the whole hand (3 m) 0.65 (FS by the subject-fraction rule), sun behind it.",
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
     fit: "From a ledge 15 m below the crown top, 4 m out, up 60 deg: Hf at 15 m = 15 x 24/18 = 20 m, so the 10 m crown, the hand above it (3 m) and sky fit. Survivors falling past about 3 m from the lens: Hf 4 m, h 0.24 (WS).",
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
| L14 | S03 MCU; hero Plum, 3q_front, yaw 20 | pass |
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

### What planning-only mode leaves owed (from C)

Deferred, not failed: stage 2 (no music file), L11's measured-onset clause, every R-lint, stage 5
onward. **To verify at scout:** survivor POV camera, hand-player POV camera (where the hand player's
eye sits is unknown), hand drawn in playback (fixed only on the pilot branch), survivor wave emote,
survivor floating in water, performed finger wave, camera collision for S13, the pull's 2 m travel.
**Before any render:** (1) choose or cut a track to the target hits and map it (stage 2); (2) move
the picture to the measured hits, never the hits to the picture; (3) re-run L11 with measured
onsets; (4) scout every `verify` item, applying fallbacks or declaring BLOCKED; (5) re-run the
stage-4 critic if any key moved more than 6 frames or any shot fell back.
