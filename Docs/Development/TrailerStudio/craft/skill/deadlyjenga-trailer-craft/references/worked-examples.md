# Worked examples D, E and B (and how to use any example)

Open at stage 1 (to see how different concepts can be) and stage 3 (to see how a plan is written
and linted). This file holds the two disaster-PvP examples, **D** (15 s mayhem montage, staged
opening, title over picture) and **E** (30 s mayhem single take, cold open, planning-only, smash
cut to the card), and **B** (15 s disaster single take). The legacy hand examples A and C are in
`legacy-hand.md`. Every example passes every plan lint as revised on 2026-11-20 (revision 3):
D, E and B were run through `tools/lint.py` (0 hard failures, every soft lint passing or n/a); the
check tables below each plan restate that run.

## 0. Examples are not templates

Skill test 1 (`../../SKILL-TEST-1.md`) found that all four planners cloned the one example: the same
80 m / 135 mm collapse, the same one-hand hang, the same hero name, the same tagline. Two of those
copied frames could not show what their new shot needed. So:

- **Do not reuse** an example's tagline, caption, hero id or cast ids (lint L39, hard). Reserved:
  - taglines: "Don't be the one who falls." (A), "The tower was the easy part." (B),
    "One friend pulls. Everyone else climbs." (C), "Your friends are a disaster too." (D),
    "Survive the sky. Then survive your friends." (E)
  - captions: "THE HAND IS ANOTHER PLAYER" (A), "STAY ON THE TOWER" (B), "YOUR FRIEND PLAYS THE HAND" (C),
    "SHOVE FRIENDS. DODGE DISASTERS." (D), "WEAPONS. DISASTERS. ONE TOWER." (E)
  - hero and cast ids: mint, rook (A); coral, finn, ivo (B); plum, gus, tam, rin (C);
    jolt, brick, pip, dune (D); vela, kestrel, okra, tansy, sprout (E)
- **Do not copy camera numbers.** Every shot's `fit` line derives its frame from what that shot must
  show (lint L38). If your derivation lands on an example's numbers, fine, but the `fit` line must
  show the arithmetic for your subject (L39 warns on identical distance, height and focal).
- **Copy the method, not the spine.** D: audience-knows opening, a fight, a reaction, the wave's
  impact, a glove barrage, the disaster settling the fight. E: a disaster already raining at frame 0,
  more disasters stacking inside one follow, a weapon in each beat, the hero's win undone by a
  friend. B: a threat at frame 0, consequences inside one pull-out, a face after, a card. A fourth
  mayhem plan should differ from D and E in at least two of shape, opening, payoff and ending.
- **Do not copy the twists either.** D's "the disaster punishes the bully" and E's "a friend ends
  the winner" are spent. Find this brief's own turn (`structure-and-templates.md` section 1, step 5).

Everything here assumes the attempt-1 castle map (a 56 m tower on a 25 m castle, the wall walk 25 m
above the sea) unless a line says otherwise. On any other map re-derive every `fit` line from that
map's measured heights. Every Studio feature named in `needs` is graded against
`game-content.md` section 9 (2026-11-20).

---

## 1. Example D: "Push Comes to Shove" (15 s, mayhem, montage, staged)

Brief (short): 15 s social, muted-first, 16:9. Show friends fighting each other, a disaster hitting
the castle, someone knocked off, someone into the sea, several players at once, a reaction. No hand.

Brief proofs (`brief.yaml`): `pvp_hit` = one survivor visibly strikes another and the victim moves,
both in frame; `hazard_hit` = the wave visibly moves blocks; `knock_off` = a survivor leaves the
structure because of a named attacker; `into_sea` = a survivor enters the water in frame;
`multiplayer` = 3 or more survivors acting independently in one frame (`readable_survivors` ≥ 3);
`survivors_react` = a survivor's head and hands turn toward the threat.

Concept: four friends brawl on the castle wall while a 40 m wave rises behind them; the viewer sees
it, they do not; the bully who launched a friend off the wall is the one the wave takes. Arc:
*scrap, see, swept*. Hook in one line: your friends and the weather are both trying to knock you off.

Memorable-image candidates: the crest rising behind a fistfight (chosen, S01); a glove launch that
sends a friend sailing over the surf (chosen, S05-S06); the hero stepping one block up as the crest
passes under her feet (chosen, S07-S08); two survivors still fighting while riding a tilting block;
a survivor using a meteor crater as cover from a rocket.

Rejected: (1) a single take on the wall from frame 0 that follows the fight into the wave: it
changes shape and opening, but the glove launch and the drop need angles one camera cannot reach in
11 s; (2) a cold open on the wave hitting the base, then the fight: it changes opening and payoff,
but the wave then has no audience-knows tension and the fight reads as an afterthought.

What the Studio gives this plan (`game-content.md` section 9): the `hazard` cue for a tsunami that
pushes survivors (the `tsunami` cue would not), `gear` for Brick's glove and dagger, `fire` for each
strike with Brick and Pip as live bots, a `scream` reaction layer on Jolt. What it lacks: a punch
cue, a chosen side for the `hazard` tsunami, and a repeatable world; so S02, S04 and S08 are
`verify` items with fallbacks, and the take is chosen from variation runs.

```yaml
id: push-comes-15
version: 1
example: true                       # worked example D: lint L39 skips itself
mode: production
concept_type: mayhem
opening: staged
threats: ["hazard:tsunami"]
fps: 30
length_frames: 450
class: 15
aspect: "16:9"
shape: montage
hook: "Friends shove each other off a tower while a disaster hits it"
hero: jolt
cast: [jolt, brick, pip, dune]
must_show: [pvp_hit, hazard_hit, knock_off, into_sea, multiplayer, survivors_react]
capture: {hand: none, survivors: "live bots for brick and pip (real hits), jolt performed or a bot", take: "bundle/push-comes-take-01"}
camera_sensor_mm: 24
music:
  file: Music/push_comes_100.wav
  bpm: 100                          # one beat = 18 frames
  meter: 4
  downbeat_frame: 0
  hits:
    - {name: open,  role: open,   frame: 0,   measured_onset_frame: 1}
    - {name: punch, role: accent, frame: 70,  measured_onset_frame: 71}
    - {name: wave,  role: accent, frame: 165, measured_onset_frame: 164}
    - {name: main,  role: main,   frame: 264, measured_onset_frame: 263}
    - {name: title, role: title,  frame: 345, measured_onset_frame: 346}
  stopdowns:
    - {from: 244, to: 264}          # 0.67 s: music out, the crest's hiss left
titles:
  - {text: "SHOVE FRIENDS. DODGE DISASTERS.", role: caption, frame_in: 100, frame_out: 180, style: over_picture, position: lower_third, closing: false}
  - {text: "DEADLY JENGA", role: title, frame_in: 345, frame_out: 450, style: over_picture, position: center, closing: true}
  - {text: "Your friends are a disaster too.", role: tagline, frame_in: 360, frame_out: 450, style: over_picture, position: lower_third, closing: false}
hazard_set: {type: tsunami, cue: hazard, height_m: 40, warning_s: 0, offshore_m: 150, speed_m_s: 15}
mix: {target_lufs: -15, true_peak_max: -1.0, title_vs_body_lu: -7, ramp_lu: 3}
studio: {ramp_sound: false}

shots:
  - id: S01
    section: hook
    kind: gameplay
    cut_in: 0
    cut_out: 40
    tape_in: 300
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: jolt}
    also_in_frame: [brick, pip, dune, "castle:wall_walk", "hazard:tsunami"]
    shows: [multiplayer]
    readable_survivors: 4
    tower_level: base
    learns: "Four friends scrap on the castle wall"
    reveal: knows
    threat: "hazard:tsunami"
    size: WS
    angle: eye
    side: {face: 3q_front, yaw_deg: 35}
    camera: {move: static, purpose: "the audience sees the wave they ignore", ease: none,
             distance_m: 6.0, height_m: 0.8, focal_mm: 24, aperture: 8, focus: subject}
    fit: "Hf = 6.0 x 24/24 = 6.0 m: survivors 0.16 (WS by the 0.08-0.20 rule), four across the 10.7 m wide frame.
          Lens on the wall walk 25 m above the sea, looking out to sea. Crest 40 m above the sea = 15 m above the
          walk, about 90 m out at this tape time: 9 deg above the lens, so at y 0.67 behind their heads."
    key_frame: 20                   # dune shoves pip; the crest still rising behind
    frame_at_key: {grid: MC, min_area_pct: 3, max_occlusion_pct: 15, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: none, eye_enter: null, eye_exit: MC}
    sound:
      - {at: 0,  kind: music_hit, name: open, source: library, gain_db: 0, sync: open}
      - {at: 0,  kind: sfx, name: far_sea_roar_bed, source: designed, gain_db: -14, lead_in: true}
      - {at: 20, kind: sfx, name: scuffle_thump, source: take, gain_db: -6}
    hook_count: 1
    needs: [{feature: tsunami_full_height_offshore, status: verify}]
    fallback: "Cut S01 to the group alone and let S03's look carry the reveal; anticipation then 50 frames"

  - id: S02
    section: teach
    kind: gameplay
    cut_in: 40
    cut_out: 100
    tape_in: 330
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: jolt}
    also_in_frame: [brick, "castle:wall_walk"]
    shows: [pvp_hit]
    attacker: brick
    victim: jolt
    tower_level: base
    learns: "Brick knocks Jolt back toward the edge"
    reveal: none
    size: WS
    angle: low
    side: {face: 3q_front, yaw_deg: -25}
    camera: {move: handheld, purpose: "the scrap from inside it, low and from her other side", ease: none,
             distance_m: 3.2, height_m: 0.4, focal_mm: 24, aperture: 5.6, focus: subject, params: {noise: handheld_calm}}
    fit: "Hf = 3.2 x 24/24 = 3.2 m: Jolt 0.29 (WS); frame 5.7 m wide. Jolt at x 0.65, Brick at x 0.82; a knockback of
          2-3 m to the left ends at x 0.13-0.30, inside frame. Both bodies whole at the strike."
    key_frame: 30                   # the strike lands
    action_cue: {cue_id: brick_strikes_jolt, tape_frame: 360, expect: {victim_moves_m: 1.5}}
    frame_at_key: {grid: MC, min_area_pct: 6, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: R>L, eye_enter: MC, eye_exit: ML}
    sound:
      - {at: 30, kind: music_hit, name: punch, source: library, gain_db: 0, sync: punch}
      - {at: 30, kind: sfx, name: candy_punch_hit, source: take, gain_db: -3}
    hook_count: null
    needs: [{feature: melee_hit_through_fire_cue, status: verify}, {feature: live_bot_victim, status: verify}]
    fallback: "A person performs Brick's punch; else Jolt recorded with a knockback reaction layer at the strike"

  - id: S03
    section: teach
    kind: gameplay
    cut_in: 100
    cut_out: 150
    tape_in: 372
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: jolt}
    also_in_frame: [sky]
    shows: [survivors_react]
    tower_level: base
    learns: "Jolt sees the wave over Brick's shoulder"
    reveal: reaction
    threat: "hazard:tsunami"
    reaction: "scream layer from tape 380 to 420, head turned toward the sea"
    size: MCU
    angle: low
    side: {face: 3q_front, yaw_deg: 25}
    camera: {move: static, purpose: "her head and hands read the fear", ease: none,
             distance_m: 1.4, height_m: 0.6, focal_mm: 85, aperture: 2.8, focus: subject}
    fit: "Hf = 1.4 x 24/85 = 0.40 m: chest up (MCU 0.35-0.48). Lens 0.6 m up, under her 0.85 m head; she looks up-left
          past the lens, toward the sea. No drawn face: the read is the head tipping up and both hands rising."
    key_frame: 25                   # the scream pose peaks
    frame_at_key: {grid: ML, min_area_pct: 15, max_occlusion_pct: 5, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: reaction, screen_direction: none, eye_enter: ML, eye_exit: TL}
    sound:
      - {at: 25, kind: vo, name: "voice: short gasp", source: library, gain_db: -6}
    hook_count: null
    needs: [{feature: reaction_layer_scream, status: available}]
    fallback: "cower layer instead of scream"

  - id: S04
    section: escalate
    raises: threat
    kind: gameplay
    cut_in: 150
    cut_out: 200
    tape_in: 470
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: hazard, id: "hazard:tsunami"}
    also_in_frame: ["castle:wall", jolt, brick]
    shows: [hazard_hit]
    tower_level: base
    learns: "The wave slams into the castle wall"
    reveal: whole
    threat: "hazard:tsunami"
    size: EWS
    angle: low
    side: {face: profile, yaw_deg: 90}
    camera: {move: tilt_down, purpose: "ride the crest down to the impact", ease: in_out,
             distance_m: 55, height_m: 2.0, focal_mm: 24, aperture: 8, focus: subject, params: {tilt_deg: 20, over_s: 1.2}}
    fit: "Hf = 55 x 24/24 = 55 m at the wall, side-on to the wave: the 25 m wall 0.45, the 40 m crest 0.73;
          survivors 0.017 (EWS, not readable: the wave is the subject, the wall the scale). Impact at the wall's
          foot sits in the lower third at the key."
    key_frame: 15                   # water hits the wall's lower blocks
    action_cue: {cue_id: tsunami_hazard_cue, tape_frame: 485, expect: {reaches_wall_at_tape_frame: 485}}
    frame_at_key: {grid: BC, min_area_pct: 30, max_occlusion_pct: 10, breaks_frame: true}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: R>L, eye_enter: TC, eye_exit: BC}
    sound:
      - {at: 15, kind: music_hit, name: wave, source: library, gain_db: 0, sync: wave}
      - {at: 15, kind: sfx, name: water_impact_blocks, source: designed, gain_db: -2}
    hook_count: 2
    needs: [{feature: hazard_cue_tsunami_heading, status: verify}, {feature: tsunami_reaches_wall_walk, status: verify}]
    fallback: "none: BLOCKED if no take puts the 40 m wave onto the wall walk from the planned side"

  - id: S05
    section: escalate
    raises: stakes
    kind: gameplay
    cut_in: 200
    cut_out: 222
    tape_in: 490
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: pip}
    also_in_frame: [brick, jolt, "castle:wall_walk"]
    shows: [pvp_hit, knock_off, weapon]
    attacker: brick
    victim: pip
    tower_level: base
    learns: "Brick's giant glove launches Pip off the wall"
    reveal: none
    size: WS
    angle: eye
    side: {face: 3q_front, yaw_deg: 50}
    camera: {move: static, purpose: "the glove's hit, plainly", ease: none,
             distance_m: 3.0, height_m: 0.8, focal_mm: 24, aperture: 5.6, focus: subject}
    fit: "Hf = 3.0 x 24/24 = 3.0 m: Pip 0.31 (WS) at x 0.6, Brick at x 0.3; Jolt 2 m further back by a block, Hf 5.0 m,
          0.19. The glove's launch (22 m/s horizontal) carries Pip out of frame right within 6 frames of the key."
    key_frame: 10                   # glove contact
    action_cue: {cue_id: brick_glove_fire, tape_frame: 500}
    frame_at_key: {grid: MR, min_area_pct: 6, max_occlusion_pct: 10, breaks_frame: true}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: L>R, eye_enter: BC, eye_exit: MR}
    sound:
      - {at: 0,  kind: sfx, name: glove_whoosh, source: designed, gain_db: -6}
      - {at: 10, kind: sfx, name: glove_boom, source: take, gain_db: -1}
    hook_count: null
    needs: [{feature: gear_cue_boxing_glove, status: available}, {feature: melee_hit_through_fire_cue, status: verify}]
    fallback: "Brick fires the PvP dagger instead; or Pip recorded with a knockback plus tumble layer"

  - id: S06
    section: escalate
    raises: scale
    kind: gameplay
    cut_in: 222
    cut_out: 240
    tape_in: 504
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: pip}
    also_in_frame: [surf_below]
    shows: [fall]
    tower_level: air
    learns: "Pip sails out over the surf"
    reveal: consequence
    cause_in: prev
    size: WS
    angle: high
    side: {face: profile, yaw_deg: 80}
    camera: {move: static, purpose: "the drop under her", ease: none,
             distance_m: 10.4, height_m: 1.0, focal_mm: 24, aperture: 8, focus: subject, params: {pitch_deg: -30}}
    fit: "Lens at the wall's edge looking out and down 30 deg. At the key Pip is 10 m out and 3 m down: distance 10.4 m,
          Hf 10.4 m, Pip 0.09 (WS by the 0.08-0.20 rule); the surf 25 m below fills the lower third."
    key_frame: 9                    # she crosses frame centre
    frame_at_key: {grid: MC, min_area_pct: 1, max_occlusion_pct: 5, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: L>R, eye_enter: MR, eye_exit: BR}
    sound:
      - {at: 0, kind: sfx, name: wind_rush, source: designed, gain_db: -6}
      - {at: 9, kind: vo, name: "voice: far yelp", source: library, gain_db: -10}
    hook_count: null
    needs: []
    fallback: "Hold S05 four frames longer and cut straight to S07"

  - id: S07
    section: breath
    kind: gameplay
    cut_in: 240
    cut_out: 252
    tape_in: 530
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: jolt}
    also_in_frame: [brick, "hazard:tsunami"]
    shows: []
    tower_level: base
    learns: "Jolt steps up as the crest towers over them"
    reveal: knows
    threat: "hazard:tsunami"
    size: FS
    angle: low
    side: {face: 3q_front, yaw_deg: 30}
    camera: {move: static, purpose: "the held beat before the crest", ease: none,
             distance_m: 1.9, height_m: 0.4, focal_mm: 35, aperture: 4, focus: subject}
    fit: "Hf = 1.9 x 24/35 = 1.30 m: Jolt and Brick 0.72 (FS), 1 m apart in a 2.3 m wide frame; low lens, the crest's
          face filling the top of frame behind them."
    key_frame: 6                    # her foot lands on the higher block
    frame_at_key: {grid: MC, min_area_pct: 20, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: none, eye_enter: MC, eye_exit: MC}
    sound:
      - {at: 0, kind: sfx, name: block_step, source: take, gain_db: -8}
      - {at: 6, kind: sfx, name: single_creak, source: designed, gain_db: -6}
    hook_count: null
    needs: []

  - id: S08
    section: payoff
    kind: gameplay
    cut_in: 252
    cut_out: 345
    tape_in: 536
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: hazard, id: "hazard:tsunami"}
    also_in_frame: [jolt, brick, "castle:wall_walk", sea]
    shows: [knock_off, into_sea]
    attacker: "hazard:tsunami"
    victim: brick
    tower_level: base
    learns: "The crest sweeps Brick into the sea"
    reveal: payoff
    threat: "hazard:tsunami"
    cause_in: self
    size: WS
    angle: high
    side: {face: 3q_front, yaw_deg: 30}
    camera: {move: static, purpose: "locked-off truth shot of the sweep", ease: none,
             distance_m: 8.5, height_m: 6.0, focal_mm: 24, aperture: 8, focus: subject, params: {pitch_deg: -45}}
    fit: "From the tower's first ledge 6 m above the walk and 6 m back, down 45 deg: 8.5 m to the pair, Hf 8.5 m,
          survivors 0.11 (WS), frame 15 m wide. The crest enters top right at the key; Brick's slide of about 8 m to
          the walk's far edge and his drop into the surf stay in the lower left; Jolt on her block 2 m higher stays centre."
    key_frame: 12                   # the crest hits them: the main hit
    action_cue: {cue_id: tsunami_hazard_cue, tape_frame: 548, expect: {carries_brick: true, jolt_holds: true}}
    frame_at_key: {grid: MC, min_area_pct: 2, max_occlusion_pct: 15, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: R>L, eye_enter: MC, eye_exit: BL}
    sound:
      - {at: 0,  kind: sfx, name: crest_hiss, source: designed, gain_db: -8}
      - {at: 12, kind: music_hit, name: main, source: library, gain_db: 0, sync: main}
      - {at: 12, kind: sfx, name: water_slam_sub, source: designed, gain_db: -2}
      - {at: 40, kind: vo, name: "voice: Brick's yell, fading", source: library, gain_db: -8}
    hook_count: 3
    needs: [{feature: survivor_holds_against_wave, status: verify}, {feature: tsunami_carries_live_bot, status: verify}]
    fallback: "Tune the wave's height so its top passes under Jolt's block (she stands 2 m above Brick)"

  - id: S09
    section: title
    kind: gameplay
    cut_in: 345
    cut_out: 450
    tape_in: 629
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: jolt}
    also_in_frame: ["castle:wall_walk", foam]
    shows: []
    tower_level: base
    learns: "Jolt is the last one on the wall"
    reveal: none
    size: WS
    angle: high
    side: {face: 3q_front, yaw_deg: 40}
    camera: {move: pull_out, purpose: "leave her alone in the foam under the title", ease: in_out,
             distance_m: 4.0, height_m: 2.0, focal_mm: 24, aperture: 8, focus: subject, params: {to_distance_m: 14, rise_m: 3, over_s: 3.5}}
    fit: "Hf from 4 m to 14 m over 3.5 s; at the key (about 6 m) Jolt 0.16 (WS), at the end 0.07; the title sits over the
          sky in the top third, the foam around the wall below."
    key_frame: 30                   # a last block topples into the foam
    frame_at_key: {grid: MC, min_area_pct: 1, max_occlusion_pct: 0, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: contrast_size, screen_direction: none, eye_enter: MC, eye_exit: MC}
    sound:
      - {at: 0,  kind: music_hit, name: title, source: library, gain_db: 0, sync: title}
      - {at: 30, kind: sfx, name: block_splash_far, source: designed, gain_db: -10}
    hook_count: null
    needs: [{feature: export_titles, status: fallback}]
    fallback: "Burn the titles in at assembly; darken the sky 1 stop under them"
```

### D against every lint

Body = frames 0-345 (11.5 s). Body-shot lengths: 40, 60, 50, 50, 22, 18, 12, 93.

| Lint | Check | Result |
|------|-------|--------|
| L01 | 0-40-100-150-200-222-240-252-345-450 | pass |
| L02 | one subject per shot; longest `learns` 9 words; no " and " | pass |
| L03 | hook, teach x2, escalate x3 (threat, stakes, scale), breath, payoff, title | pass |
| L04 | S01 gameplay at 0 | pass |
| L05 | mayhem: first core verb is S02's `pvp_hit`, key 70 = 2.33 s ≤ 5.0, Brick and Jolt both in frame | pass |
| L06 | median 45 f (1.5 s); shortest 12; longest 93 ≥ 60; window [150, 300) holds 150, 200, 222, 240, 252 = 5 | pass |
| L07 | closing title 345 (11.5 s) in [330, 360]; title plus card 105 in [90, 120] | pass |
| L08 | staged: first whole view of the wave S04 at 150; before it S01 `knows` 40 + S03 `reaction` 50 = 90 ≥ 21. Causes: S06 prev = S05 (`knock_off`), S08 self | pass |
| L09 | first core verb S02: 60 f, key 30 (30 and 30 from the ends), attacker and victim in frame | pass |
| L10 | keys from start/end: 20/20, 30/30, 25/25, 15/35, 10/12, 9/9, 6/6, 12/81, 30/75 | pass |
| L11 | onsets 1, 71, 164, 263, 346 within 2; S08 key 252 + 12 = 264 = main; title 345 = title hit; syncs at 0, 70, 165, 264, 345 | pass |
| L12 | every key has a sound at the key frame | pass |
| L13 | no ramps | pass |
| L14 | S03 MCU; hero Jolt, 3q_front, yaw 25, `reaction` = the scream layer | pass |
| L15 | statics and handheld `none`; S04 tilt_down and S09 pull_out `in_out` | pass |
| L16 | S01 and S02 are both Jolt in WS: S01 eye, yaw 35; S02 low, yaw -25 (65° apart) | pass (the linter caught a jump cut in the first draft, S02 at eye and yaw 40) |
| L17 | no POV | pass |
| L18 | pvp_hit S02, S05; hazard_hit S04; knock_off S05, S08; into_sea S08; multiplayer S01; survivors_react S03 | pass |
| L19 | seven `verify` items to the scout list; S04 BLOCKED if the wave cannot be put on the wall walk; nothing the game cannot do | pass |
| L20 | caption 4 words needs 66, has 80; title 105 ≥ 48; tagline 6 words needs 84, has 90 | pass |
| L21 | escalate (50, 22, 18) median 22 ≤ hook + teach (40, 60, 50) median 50 | pass |
| L22 | mean 43.1, std 24.8, CV 0.58 | pass |
| L23 | longest even run S03-S04 (2) | pass |
| L24 | 100 BPM grid (18 f): only 252 on a beat = 1 of 8 | pass |
| L25 | hook_count 1 (S01), 2 (S04), 3 (S08) | pass |
| L26 | S05 > S06 both L>R; the wave R>L in S04 and S08 | pass |
| L27 | MC>MC, ML>ML, TL>TC (new beat at S04), BC>BC, MR>MR, BR>MC, MC>MC, BL>MC: all within 1 | pass |
| L28 | main 264 ≥ 0.6 x 345 = 207 | pass |
| L29 | wide 7 of 8 = 88 %; close 0 % | pass |
| L30 | designed moves S04, S09 = 2 of 9 = 22 % | pass |
| L31 | class 15: optional (one 20-frame stop-down) | pass |
| L32 | one rule caption, 4 words; tagline 6 words; no superlatives | pass |
| L33 | S05 (22 f) > S06 sound at 0; S06 (18 f) > S07 sound at 0; S07 (12 f) > S08 sound at 0 | pass |
| L34 | knock_off 2, every other id 1 (core verbs excluded) | pass |
| L35 | WS ≥ 18, FS 12, MCU 50, EWS 50 | pass |
| L36, L37 | two-role only | n/a |
| L38 | every gameplay shot has a `fit` line | pass |
| L39 | this is the example itself | n/a |
| L40 | first hazard_hit 165, first pvp_hit 70, both ≤ 207 | pass |
| L41 | S02 Brick > Jolt, S05 Brick > Pip, both in frame; S08 the wave > Brick, `cause_in: self` | pass |
| L42 | Jolt in S01 (hook), S05 (escalate, behind), S08 (payoff) | pass |
| L43 | one hazard | n/a |
| L44 | S01 and S02 are wides with the wall walk and Jolt in the first third; every shot has `tower_level` | pass |
| L45 | S01: four survivors at 0.16 | pass |
| L46 | S05's launch holds 12 frames, then S06 (`cause_in: prev`) follows Pip; S08 holds 81 frames after the crest | pass |

What it still owes before capture: R01 on every shot (Pip at 0.09 in S06 must read as a figure;
the 0.11 survivors in S08 must stay separable in the foam); R02 on the wave (it reaches the wall
walk from the planned side within 8 frames: run variations of the `hazard` cue and keep the take
that does), the glove launch (Pip leaves frame within 6 frames), Brick swept and Jolt kept (or the
wave's height tuned below her block); the melee-through-`fire` check; Jolt's colour in a still.

---

## 2. Example E: "Everything at Once" (30 s, mayhem, single take, planning-only)

Brief (short): 30 s for YouTube and store pages, 16:9. Show several disasters escalating, weapons,
survivors fighting, someone knocked off, a crowd, a reaction. No music file and no engine access
yet: **planning-only mode**.

Brief proofs: as D, plus `weapon` = a weapon visibly fired or swung with its effect in frame, and
`several_survivors` = 3 or more survivors readable at once (`readable_survivors` ≥ 3).

Concept: one unbroken follow on Vela across the crown while the sky stacks disasters on it (meteors
already falling at frame 0, then an earthquake, then a tornado) and the other climbers use weapons
on each other; she outlasts it all, cheers, and her friend shoves her off the stump. Arc: *rain,
shake, rip, gotcha*. Hook in one line: every disaster and every friend is out to get you, at once.

Memorable-image candidates: meteors already falling as the cut begins (chosen); a rocket blowing
the block out from under the hero mid-run (chosen); the funnel taking the rocket shooter (chosen
payoff); the winner's cheer undone by one poke (chosen ending); a survivor surfing a block down the
tower in the earthquake.

Rejected: (1) the same story as a 30 s montage from many angles with a staged meteor reveal: it
changes shape and opening, and loses the one-camera "no escape" feeling that sells the stacking;
(2) end on Vela alone on the stump under the title: it changes the ending and payoff, but it is D's
ending and wastes the PvP turn.

What the Studio gives this plan: a `hazard` cue per disaster with its own settings, an aimed
tornado (pilot branch), `gear` and `fire` for the glove and the rocket, a `follow` camera, a
`cheer` reaction layer. What it lacks: a punch cue, aimed meteors, a directable bot, and a way to
make a performer's route repeatable; Vela must be performed by a person, and each beat is picked
from the take, so every hazard beat is a `verify` item.

```yaml
id: everything-at-once-30
version: 1
example: true                       # worked example E: lint L39 skips itself
mode: planning_only                 # no music file, no engine yet
concept_type: mayhem
opening: cold_open
cold_open_threat: "hazard:meteor_shower"
threats: ["hazard:meteor_shower", "hazard:earthquake", "hazard:tornado"]
fps: 30
length_frames: 900
class: 30
aspect: "16:9"
shape: single_take
hook: "Disasters stack up while friends fight on the same tower"
hero: vela
cast: [vela, kestrel, okra, tansy, sprout]
must_show: [hazard_hit, pvp_hit, knock_off, weapon, several_survivors, survivors_react]
capture: {hand: none, survivors: "Vela performed by a person; the others live bots with weapons from their marks", take: null}
camera_sensor_mm: 24
music:
  file: null
  bpm: 140                          # target tempo; one beat about 12.9 frames
  meter: 4
  downbeat_frame: 0
  hits:
    - {name: open,   role: open,   frame: 0,   measured_onset_frame: unmeasured}
    - {name: glove,  role: accent, frame: 120, measured_onset_frame: unmeasured}
    - {name: quake,  role: accent, frame: 270, measured_onset_frame: unmeasured}
    - {name: funnel, role: accent, frame: 420, measured_onset_frame: unmeasured}
    - {name: main,   role: main,   frame: 480, measured_onset_frame: unmeasured}
    - {name: shove,  role: accent, frame: 690, measured_onset_frame: unmeasured}
    - {name: title,  role: title,  frame: 750, measured_onset_frame: unmeasured}
  stopdowns:
    - {from: 462, to: 480}          # 0.6 s: music out, only the funnel's roar
titles:
  - {text: "WEAPONS. DISASTERS. ONE TOWER.", role: caption, frame_in: 60, frame_out: 150, style: over_picture, position: lower_third, closing: false}
  - {text: "DEADLY JENGA", role: title, frame_in: 750, frame_out: 900, style: card, position: center, closing: true}
  - {text: "Survive the sky. Then survive your friends.", role: tagline, frame_in: 780, frame_out: 900, style: card, position: lower_third, closing: false}
hazard_set:
  - {type: MeteorShower, cue: hazard, at_tape_s: 0, values: "defaults (3 m meteors, 12 per second)"}
  - {type: Earthquake, cue: hazard, at_tape_s: 7, values: "defaults (8 m/s2 at 2 Hz)"}
  - {type: Tornado, cue: hazard, aimed: true, heading_deg: 200, at_tape_s: 11, values: "defaults (65 m tall, 16 m funnel)"}
mix: {target_lufs: -15, true_peak_max: -1.0, title_vs_body_lu: -8, ramp_lu: 4}
studio: {ramp_sound: false}

shots:
  - id: S01
    section: hook                   # the take; its beats are moments
    kind: gameplay
    cut_in: 0
    cut_out: 570
    subject: {kind: actor, id: vela}
    also_in_frame: [kestrel, okra, tansy, sprout, "tower:crown", "hazard:meteor_shower", "hazard:earthquake", "hazard:tornado"]
    shows: [several_survivors, hazard_hit, pvp_hit, knock_off, weapon, survivors_react]
    readable_survivors: 5
    tower_level: top
    learns: "One climber survives everything the sky throws"
    size: WS
    angle: eye
    side: {face: 3q_back, yaw_deg: 140}
    camera: {move: follow, purpose: "stay with Vela through the whole storm", ease: damped,
             distance_m: 3.0, height_m: 1.2, focal_mm: 24, aperture: 8, focus: subject,
             params: {lag_pos_s: 0.7, lag_rot_s: 0.45, offset_grows_to_m: 9, grow_from_frame: 380, grow_to_frame: 470, aim_offset_y_m: 0.85}}
    fit: "Start: 3.0 m behind-left, Hf = 3.0 m, Vela 0.31 (WS); the 6 m crown block under her and the others within
          8 m (Hf at 8 m = 8 m, survivors 0.12, all five at h >= 0.08). A 3 m meteor 8 m out is 0.38 of frame. From 380
          the offset grows to 9 m: at the key (480) Hf 9 m, Vela 0.10 (WS), frame 16 m wide; the 16 m funnel 25 m out
          fills the right half and Kestrel's lift toward it stays inside the top right."
    key_frame: 480
    moments:
      - {at: 0,   section: hook,     reveal: whole, threat: "hazard:meteor_shower", hook_count: 1, shows: [several_survivors],
         what: "Meteors already rain on five climbers", sound_at: 0}
      - {at: 45,  section: teach,    reveal: consequence, cause_in: self, shows: [hazard_hit],
         what: "A meteor smashes the block beside Vela", sound_at: 46}
      - {at: 120, section: teach,    reveal: none, shows: [pvp_hit, knock_off, weapon], attacker: vela, victim: okra,
         what: "Vela's giant glove launches Okra off the crown", sound_at: 120}
      - {at: 190, section: escalate, raises: scale, reveal: consequence, cause_in: self, shows: [hazard_hit],
         what: "Meteor holes spread across the crown floor", sound_at: 191}
      - {at: 230, section: escalate, raises: threat, reveal: effect, threat: "hazard:earthquake",
         what: "Blocks start to chatter under their feet", sound_at: 229}
      - {at: 270, section: escalate, raises: threat, reveal: whole, threat: "hazard:earthquake", hook_count: 2, shows: [hazard_hit],
         what: "An earthquake shakes the whole tower", sound_at: 270}
      - {at: 330, section: escalate, raises: stakes, reveal: consequence, cause_in: self, shows: [weapon, survivors_react],
         what: "Kestrel's rocket blows Vela's block out from under her", sound_at: 331}
      - {at: 380, section: escalate, raises: scale, reveal: effect, threat: "hazard:tornado",
         what: "Debris starts lifting toward the sky", sound_at: 380}
      - {at: 420, section: escalate, raises: count, reveal: whole, threat: "hazard:tornado", shows: [hazard_hit],
         what: "A tornado drags loose blocks off the crown", sound_at: 420}
      - {at: 480, section: payoff, reveal: payoff, threat: "hazard:tornado", cause_in: self, hook_count: 3, shows: [knock_off],
         attacker: "hazard:tornado", victim: kestrel, what: "The funnel rips Kestrel off the crown", sound_at: 480}
      - {at: 530, section: aftermath, reveal: consequence, cause_in: prev,
         what: "Vela clings to what is left", sound_at: 531}
    frame_at_key: {grid: MC, min_area_pct: 1, max_occlusion_pct: 20, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: none, eye_enter: null, eye_exit: MC}
    sound:
      - {at: 0,   kind: music_hit, name: open, source: library, sync: open}
      - {at: 0,   kind: sfx, name: meteor_rain_bed, source: take, gain_db: -10}
      - {at: 46,  kind: sfx, name: meteor_impact_near, source: take, gain_db: -3}
      - {at: 120, kind: music_hit, name: glove, source: library, sync: glove}
      - {at: 120, kind: sfx, name: glove_boom, source: take, gain_db: -2}
      - {at: 191, kind: sfx, name: floor_crunch, source: take, gain_db: -5}
      - {at: 229, kind: sfx, name: low_rumble, source: designed, gain_db: -8}
      - {at: 270, kind: music_hit, name: quake, source: library, sync: quake}
      - {at: 270, kind: sfx, name: tower_groan, source: designed, gain_db: -4}
      - {at: 331, kind: sfx, name: rocket_blast, source: take, gain_db: -2}
      - {at: 360, kind: sfx, name: wind_rise, source: designed, gain_db: -10, lead_in: true}
      - {at: 380, kind: sfx, name: debris_whirl, source: designed, gain_db: -8}
      - {at: 420, kind: music_hit, name: funnel, source: library, sync: funnel}
      - {at: 420, kind: sfx, name: funnel_roar, source: designed, gain_db: -4}
      - {at: 462, kind: silence, name: music_dropout, source: library}
      - {at: 480, kind: music_hit, name: main, source: library, sync: main}
      - {at: 480, kind: sfx, name: crown_rip_sub, source: designed, gain_db: -2}
      - {at: 531, kind: sfx, name: block_settle, source: take, gain_db: -8}
    needs:
      - {feature: performed_survivor_route, status: verify}         # a person plays Vela; a bot will not follow the route
      - {feature: three_hazards_in_one_take, status: verify}
      - {feature: meteor_near_hero, status: verify}                  # meteors are not aimed: pick the take's nearest impact
      - {feature: earthquake_visible_on_blocks, status: verify}
      - {feature: tornado_heading, status: available}               # IDirectedHazard: the tornado reads its heading
      - {feature: tornado_lifts_live_bot, status: verify}
      - {feature: gear_cue_boxing_glove, status: available}
      - {feature: melee_hit_through_fire_cue, status: verify}
      - {feature: fire_cue_rocket, status: available}
    fallback: "none: BLOCKED if two of the three hazards cannot run in one take; then re-plan as a montage (rejected concept 1)"

  - id: S02
    section: aftermath
    kind: gameplay
    cut_in: 570
    cut_out: 630
    subject: {kind: actor, id: vela}
    also_in_frame: [sky]
    shows: [survivors_react]
    tower_level: top
    learns: "Vela cheers at the empty sky"
    reveal: none
    reaction: "cheer layer from tape 19.3 s to 21.0 s; head up, both arms raised"
    size: MCU
    angle: low
    side: {face: front, yaw_deg: 15}
    camera: {move: static, purpose: "her body reads the win", ease: none,
             distance_m: 0.9, height_m: 0.5, focal_mm: 50, aperture: 2.8, focus: subject}
    fit: "Hf = 0.9 x 24/50 = 0.43 m: chest up (MCU 0.35-0.48), lens under her head so her raised arms leave the top
          edge; no drawn face, so the read is head tipped back and arms up."
    key_frame: 20                   # arms peak
    frame_at_key: {grid: MC, min_area_pct: 20, max_occlusion_pct: 5, breaks_frame: true}
    transition_in: cut
    connects_from_prev: {type: reaction, screen_direction: none, eye_enter: MC, eye_exit: TC}
    sound:
      - {at: 20, kind: vo, name: "voice: whoop", source: library, gain_db: -6}
    needs: [{feature: reaction_layer_cheer, status: available}]
    fallback: "the emote 'Tiny victory dance' through an emote layer"

  - id: S03
    section: aftermath
    kind: gameplay
    cut_in: 630
    cut_out: 750
    subject: {kind: actor, id: vela}
    also_in_frame: [tansy, "tower:stump", sea]
    shows: [pvp_hit, knock_off]
    attacker: tansy
    victim: vela
    tower_level: top
    learns: "Tansy shoves Vela off the stump"
    reveal: none
    size: WS
    angle: eye
    side: {face: 3q_front, yaw_deg: 45}
    camera: {move: static, purpose: "the joke plays wide and straight", ease: none,
             distance_m: 4.0, height_m: 1.0, focal_mm: 24, aperture: 8, focus: subject}
    fit: "Hf = 4.0 x 24/24 = 4.0 m: Vela 0.24 (WS) centre, Tansy climbing in at the left edge from 20; frame 7.1 m wide,
          so the 2-3 m shove and Vela's drop past the stump's edge stay in frame; the sea in the lower third."
    key_frame: 60                   # the shove
    frame_at_key: {grid: MC, min_area_pct: 4, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: L>R, eye_enter: MC, eye_exit: BR}
    sound:
      - {at: 60, kind: music_hit, name: shove, source: library, sync: shove}
      - {at: 60, kind: sfx, name: dagger_poke, source: take, gain_db: -4}
      - {at: 100, kind: sfx, name: splash_far, source: designed, gain_db: -8}
    needs: [{feature: melee_hit_through_fire_cue, status: verify}]
    fallback: "Vela recorded with knockback then fall layers at frame 60; Tansy's swing through the fire cue with the PvP dagger"

  - id: S04
    section: title
    kind: text
    cut_in: 750
    cut_out: 900
    subject: {kind: point, id: card}
    shows: []
    learns: "This is Deadly Jenga"
    reveal: none
    size: WS
    angle: eye
    camera: {move: static, purpose: "a smash cut to the card on the splash", ease: none}
    fit: "Card: no camera."
    key_frame: 0
    transition_in: smash_cut
    connects_from_prev: {type: contrast_size, screen_direction: none, eye_enter: MC, eye_exit: MC}
    sound:
      - {at: 0, kind: music_hit, name: title, source: library, sync: title}
    needs: [{feature: export_titles, status: fallback}]
    fallback: "Card built in the ffmpeg assembly step"
```

Fields omitted for space (`tape_in`, `speed: 1.0`, `action_cue`) are written once a take exists.

### E against every lint

Body = frames 0-750 (25.0 s). Body shots: S01 570 (the take), S02 60, S03 120.

| Lint | Check | Result |
|------|-------|--------|
| L01 | 0-570-630-750-900 | pass |
| L02 | one subject per shot; longest `learns`/`what` 9 words; no " and " | pass |
| L03 | moments hook, teach x2, escalate x6 (scale, threat, threat, stakes, scale, count), payoff, aftermath; then S02, S03 aftermath, S04 title | pass |
| L04 | S01 gameplay at 0 | pass |
| L05 | mayhem: first core verb is the meteor's `hazard_hit` at 45 = 1.5 s ≤ 8.0, tower and cast in frame | pass |
| L06 | single take: S01 570 / 750 = 76 % ≥ 70 %; cuts inside the body 570, 630 | pass |
| L07 | closing title 750 (83 %) in [720, 780]; title plus card 150 in [120, 180] | pass |
| L08 | cold open on the meteors at 0 (declared), held uncut 570 f; consequence escalates at 190 (scale) and 330 (stakes) differ. Earthquake first whole at 270 after the 40-frame effect at 230; tornado at 420 after the 40-frame effect at 380. Causes: 45, 190, 330, 480 self; 530 prev = the payoff | pass |
| L09 | first core verb at 45: 45 frames from the take's start, 525 from its end | pass |
| L10 | S02 20/40, S03 60/60; S04 is a card | pass |
| L11 | planning-only: measured-onset clause **deferred**; target syncs open 0, glove 120, quake 270, funnel 420, main 480, shove 690, title 750; payoff 480 = main | pass (targets) |
| L12 | every moment's `sound_at` within [at-1, at+3]; S02 20/20, S03 60/60, S04 0/0 | pass |
| L13 | no ramps | pass |
| L14 | S02 MCU; hero Vela, front, yaw 15, `reaction` = the cheer layer | pass |
| L15 | S01 follow `damped`; statics `none` | pass |
| L16 | S01 > S02 > S03 are all Vela but WS, MCU, WS: sizes change at every cut | pass |
| L17 | no POV | pass |
| L18 | hazard_hit 45, 190, 270, 420; pvp_hit 120, S03; knock_off 120, 480, S03; weapon 120, 330; several_survivors 0; survivors_react S02 | pass |
| L19 | six `verify` items on the scout list; the take is BLOCKED if two of the three hazards cannot run in one take; nothing the game cannot do | pass |
| L20 | caption 4 words needs 66, has 90; title 150 ≥ 48; tagline 7 words needs 93, has 120 | pass |
| L21-L24, L30, L33 | montage only | n/a |
| L25 | hook_count 1 (0), 2 (270), 3 (480) | pass |
| L26 | no cause-effect cut flips direction | pass |
| L27 | S01 > S02 MC>MC; S03 is a new beat; the card is a smash cut | pass |
| L28 | main 480 ≥ 0.6 x 750 = 450 | pass |
| L29 | wide 2 of 3 = 67 %; close 0 % | pass |
| L31 | stop-down 462-480 = 18 f ≥ 9 | pass |
| L32 | one caption, 4 words; tagline 7 words; no superlatives | pass |
| L34 | knock_off 3, weapon 2, others 1 (core verbs excluded) | pass |
| L35 | WS 570 and 120, MCU 60 | pass |
| L36, L37 | two-role only | n/a |
| L38 | every gameplay shot has a `fit` line; S04 is a card | pass |
| L39 | this is the example itself | n/a |
| L40 | first hazard_hit 45, first pvp_hit 120, both ≤ 450 | pass |
| L41 | 120 Vela > Okra and S03 Tansy > Vela, both in frame; 480 the tornado > Kestrel, `cause_in: self` | pass |
| L42 | Vela through the take (hook to payoff) and in S02, S03 (aftermath) | pass |
| L43 | meteors at 0 (hook), earthquake at 230 (escalate), tornado at 380 (escalate): in order, each later one in an escalate beat | pass |
| L44 | S01 is a wide with the crown and Vela from frame 0; every shot has `tower_level` | pass |
| L45 | S01: five survivors within 8 m (0.12) | pass |
| L46 | 120 holds 70 frames before the next beat; 480 holds 50; S03 holds 60 after the shove | pass |

### What planning-only mode leaves owed (from E)

Deferred, not failed: stage 2 (no music file), L11's measured-onset clause, every R-lint, stage 5
onward. **To verify at scout:** a person performing Vela's route; three hazards in one take (the
scheduler's cap of two active does not apply to cues, but nobody has run three: verify); a meteor
landing near Vela (pick the nearest impact in the take, shift the moment); the earthquake reading on
the blocks; the tornado lifting a live bot (Kestrel); the glove and the dagger through the `fire`
cue; the rocket's blast removing Vela's block; Vela's colour in a still. **Before any render:**
(1) choose or cut a track to the target hits and map it (`tools/music_map.py`); (2) move the
picture to the measured hits, never the hits to the picture; (3) re-run `tools/lint.py`; (4) scout
every `verify` item, applying fallbacks or declaring BLOCKED; (5) re-plan and re-lint after the
scout (SKILL.md, the scout loop), and re-run the stage-4 review if any key moved more than 6
frames or any shot fell back.

---

## 3. Example B: "Already Here" (15 s, disaster, single take, cold open)

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
example: true
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
    readable_survivors: 3           # at the start (0.31); 0.063 at the key, below the 0.08 crowd line
    tower_level: mid
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
    tower_level: sea
    reaction: "performed or getUp layer: head breaks the surface, arms reach for a block"
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
| L14 | S02 MCU; hero coral, front, yaw 10, reaction (head breaks the surface, arms reach) | pass |
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
| L40 | mayhem only | n/a |
| L41 | no PvP hit or knock-off tagged | pass |
| L42 | Coral in the take (hook to payoff) and S02 (aftermath) | pass |
| L43 | one hazard | n/a |
| L44 | S01 holds the lower section and Coral from frame 0; every shot has `tower_level` | pass |
| L45 | S01: three survivors at 0.31 at the start | pass |
| L46 | no knock-off tagged | pass |

What it still owes before capture: R01 (climbers at 0.063 must be visible as figures, not
specks, at preview size; if not, end the pull-out at 12 m), R02 (the wave reaches the section at
tape frame 200 within 8 frames), and S02's floating check. Studio note (revision 3): the `tsunami`
cue sets `affectPlayers = false`, so it never throws the climbers; stage this wave with the
`hazard` cue (random side: run variations) or fake the throws with reaction layers.
