# Worked examples D, E and B (and how to use any example)

Open at stage 1 (to see how different concepts can be) and stage 3 (to see how a plan is written
and linted). This file holds the two disaster-PvP examples, **D** (15 s mayhem montage, staged
opening, title over picture) and **E** (30 s mayhem single take, cold open, planning-only, smash
cut to the card), and **B** (15 s disaster single take). The legacy hand examples A and C are in
`legacy-hand.md`. Every example passes every plan lint as revised on 2026-11-20 (revision 4):
D, E and B were run through `tools/lint.py` (0 hard failures, every soft lint passing or n/a); the
check tables below each plan restate that run. Revision 4 rewrote their key moments so that every
key event is seen close or in first person, added `key_events`, `take_ref` and `layers`, and made B
a first-person take. Revision 5 added an `actors` list to every shot (each visible actor's `doing`,
how it is achieved, `spot`, planned still and level-head runs, action starts) and made Coral in B a
person in the headset; the three were re-run through `tools/lint.py` with L52-L56 (0 hard failures;
L56 waived in writing in D and E).

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
- **Do copy the coverage habit.** Every key event in D, E and B has a close shot (h ≥ 0.25) or a
  first-person view placed from the take (`take_ref`), and every cast member has a layer. That is
  the method; their particular POV choices (the bully's glove, the betrayer's eyes, the victim's
  own take) are not. The same goes for the `actors` lists (revision 5): every visible actor wants
  something in every shot, and nobody's action starts on another's frame (`performance.md`).
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

Revision 4 (record once, close and first person): all four survivors are live in **one** world
pass; every later layer is an overlay timed from that take's report (`layers` below). The two
knock-offs are seen from inside: S05 is Brick's own view as his glove launches Pip, and the payoff
S08 is Jolt's view from her higher block as the crest takes Brick. The revision-3 payoff was a
locked-off wide with Brick at 0.11 of frame; it is now the aftermath, re-shot from the waterline so
Brick's splash is close too.

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
capture: {hand: none, survivors: "all four live in one world pass, each driven through the game's controls (go to, run, look, jump, punch); brick's glove strikes still by fire cues", take: "bundle/push-comes-take-01", take_status: planned}
motion_source: plan                 # revision 5: still and level-head runs are the beat sheets' intent until the take is committed
waive:
  - {lint: L56, shot: "S03, S05, S08", reason: "no person booked for this 15 s social cut, and all four touch each other in one live pass; Jolt and Brick are driven with explicit layers and the alive layer. Re-shoot S08 with a person performing Jolt if one is free: it is the payoff and her head is the camera"}
key_events:                         # trailer frames; take frames come from the take report after capture
  - {id: K1, frame: 70,  kind: pvp_hit,  attacker: brick, victim: jolt, what: "Brick's strike knocks Jolt back"}
  - {id: K2, frame: 202, kind: knock_off, attacker: brick, victim: pip, what: "Brick's glove launches Pip off the wall"}
  - {id: K3, frame: 264, kind: knock_off, attacker: "hazard:tsunami", victim: brick, what: "the crest takes Brick (payoff)"}
  - {id: K4, frame: 322, kind: into_sea, victim: brick, what: "Brick drops into the surf"}
layers:                             # the pass order: the world first, then each actor against the tape
  - {order: 1, kind: world, actor: world, how: "RecordWholeSet: the hazard tsunami cue, gear at 0.3 s, a fire cue per strike; all four live and driven (Brick runs at Jolt then Pip, Jolt climbs to the higher block at 536, Dune shoves Pip at 320)", gives: "blocks, wave, hits, every body"}
  - {order: 2, kind: actor, actor: brick, how: "overlays: lookAt actor:jolt, then actor:pip, each 12-30 frames before its wind-up; reach toward each target 6 frames either side of its fire cue; the alive layer between", reacts_to: [K1, K2]}
  - {order: 3, kind: actor, actor: pip, how: "overlays: knockback from K2's take frame, then scream through the flight; head turned back at the wall", reacts_to: [K2]}
  - {order: 4, kind: actor, actor: dune, how: "driven: edge-peek loop (walk to the edge, look down, step back); overlays: lookAt actor:pip at K2 + 11 frames (she was looking at the sea); cower from the crest's first frame", reacts_to: [K2, K3]}
  - {order: 5, kind: actor, actor: jolt, how: "overlays: lookAt hazard from tape 372; scream 380-420; flinch at K3 + 6 frames (she watched it come); the alive layer between (her first-person view follows these)", reacts_to: [K1, K3]}
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
    actors:                         # revision 5: every visible actor, what they want, how it is achieved (performance.md)
      - {id: jolt,  doing: "circles Brick with her guard up, eyes on his hands", how: [driven, alive], spot: wall_walk, still_f: 5, level_f: 12, acts_at: [3, 26]}
      - {id: brick, doing: "steps in on Jolt, swinging, his back to the sea", how: [driven, alive], spot: wall_walk, still_f: 4, level_f: 9, acts_at: [8, 31]}
      - {id: pip,   doing: "windmills for balance after Dune's shove", how: [driven, physics, layer], spot: wall_walk, still_f: 0, level_f: 6, acts_at: [21]}
      - {id: dune,  doing: "shoves Pip, then checks over her shoulder at the sea", how: [driven, alive], spot: wall_walk, still_f: 6, level_f: 10, acts_at: [14, 36]}
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
    needs: [{feature: tsunami_full_height_offshore, status: verify}, {feature: performer_driver, status: verify}, {feature: alive_layer, status: verify}]
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
    actors:
      - {id: jolt,  doing: "backs toward the edge, guard up, eyes on Brick's hands", how: [driven, physics, layer, alive], spot: wall_walk, still_f: 6, level_f: 14, acts_at: [5, 31, 48]}
      - {id: brick, doing: "closes on Jolt and throws a hook, watching it land", how: [driven, alive], spot: wall_walk, still_f: 4, level_f: 9, acts_at: [10, 22, 30, 41]}
    reveal: none
    size: WS
    angle: low
    side: {face: 3q_front, yaw_deg: -25}
    camera: {move: handheld, purpose: "the scrap from inside it, low and from her other side", ease: none,
             distance_m: 3.2, height_m: 0.4, focal_mm: 24, aperture: 5.6, focus: subject, params: {noise: handheld_calm}}
    fit: "Hf = 3.2 x 24/24 = 3.2 m: Jolt 0.29 (WS); frame 5.7 m wide. Jolt at x 0.65, Brick at x 0.82; a knockback of
          2-3 m to the left ends at x 0.13-0.30, inside frame. Both bodies whole at the strike."
    key_frame: 30                   # the strike lands
    take_ref: {event: K1, take_frame: 360, subject_h: 0.29, source: fit}   # after capture: Jolt's tape position at 360, then the metrics
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
    actors:
      - {id: jolt,  doing: "glances back at Brick, then her head snaps up to the sea; hands rise", how: [driven, layer, alive], spot: wall_walk, still_f: 4, level_f: 8, acts_at: [2, 14, 25]}
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
    cut_out: 192
    tape_in: 470
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: hazard, id: "hazard:tsunami"}
    also_in_frame: ["castle:wall", jolt, brick]
    shows: [hazard_hit]
    tower_level: base
    learns: "The wave slams into the castle wall"
    actors:                         # tiny here (0.017), still alive
      - {id: jolt,  doing: "scrambles toward the higher block", how: [driven, alive], spot: wall_walk, still_f: 3, level_f: 10, acts_at: [6]}
      - {id: brick, doing: "still swinging at her, his back to the wave", how: [driven, alive], spot: wall_walk, still_f: 5, level_f: 12, acts_at: [12]}
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
    cut_in: 192
    cut_out: 222
    tape_in: 482
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: pip}
    also_in_frame: [brick, jolt, "castle:wall_walk"]   # brick: his glove arm in the lower right (first person)
    shows: [pvp_hit, knock_off, weapon]
    attacker: brick
    victim: pip
    tower_level: base
    learns: "Brick's giant glove launches Pip off the wall"
    actors:
      - {id: brick, doing: "swings the glove from his guard at Pip, head on her", how: [driven, layer], spot: wall_walk, still_f: 0, level_f: 10, acts_at: [0, 10]}
      - {id: pip,   doing: "turns toward the glove too late, then launches off", how: [driven, physics, layer], spot: wall_walk, still_f: 0, level_f: 4, acts_at: [5, 11]}
      - {id: jolt,  doing: "climbs for the higher block, looking back at the fight", how: [driven, alive], spot: wall_walk, still_f: 4, level_f: 8, acts_at: [17]}
    reveal: none
    size: FS                        # POV: by Pip's own height fraction
    angle: eye
    side: null
    camera: {move: pov, pov_of: brick, purpose: "be the bully: the glove is yours", ease: none,
             distance_m: 1.5, height_m: 0.85, focal_mm: 21, aperture: 8, focus: subject, pov_smoothing: {pos_s: 0.3, rot_s: 0.45}}
    fit: "First person at Brick's eyes, 21 mm (k = 24/21 = 1.14, vFOV 59.5 deg): Pip 1.5 m ahead, Hf 1.71 m, Pip 0.55
          (FS by subject fraction); the glove fills the lower right as it swings; Jolt a block back at 4 m, Hf 4.6 m, 0.20.
          The 22 m/s launch carries Pip out of frame right within 3 frames of the key; S06 picks her up (L46)."
    key_frame: 10                   # glove contact
    take_ref: {event: K2, take_frame: 492, subject_h: 0.55, source: fit}
    action_cue: {cue_id: brick_glove_fire, tape_frame: 492}
    frame_at_key: {grid: MR, min_area_pct: 20, max_occlusion_pct: 10, breaks_frame: true}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: L>R, eye_enter: BC, eye_exit: MR}
    sound:
      - {at: 0,  kind: sfx, name: glove_whoosh, source: designed, gain_db: -6}
      - {at: 10, kind: sfx, name: glove_boom, source: take, gain_db: -1}
    hook_count: null
    needs: [{feature: gear_cue_boxing_glove, status: available}, {feature: glove_punch_cue, status: verify},
            {feature: first_person_camera_with_hands, status: verify}]
    fallback: "Over Brick's shoulder: ride 0.4 m behind and 0.15 m above his head, 35 mm; Pip still 0.35. No glove: the PvP dagger"

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
    actors:
      - {id: pip,   doing: "flails through the air, head turned back at the wall", how: [physics, layer], spot: air_over_surf, arrived_by: physics, still_f: 0, level_f: 0, acts_at: [3]}
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
    actors:
      - {id: jolt,  doing: "jumps onto the higher block, eyes up on the crest", how: [driven, alive], spot: wall_walk, still_f: 0, level_f: 0, acts_at: [0]}
      - {id: brick, doing: "hunts for Pip's gap, head swinging, his back to the wave", how: [driven, alive], spot: wall_walk, still_f: 2, level_f: 6, acts_at: [5]}
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
    needs: [{feature: performer_driver_jump, status: verify}]
    fallback: "No driven jump: Jolt's mark starts on the higher block's edge and she steps up with a climb; never place her there between shots"

  - id: S08
    section: payoff
    kind: gameplay
    cut_in: 252
    cut_out: 310
    tape_in: 536
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: brick}
    also_in_frame: [jolt, "hazard:tsunami", "castle:wall_walk"]   # jolt: her hands and feet on the block edge (first person)
    shows: [knock_off]
    attacker: "hazard:tsunami"
    victim: brick
    tower_level: base
    learns: "From Jolt's block, the crest takes Brick"
    actors:
      - {id: jolt,  doing: "crouches on her block as the crest passes, then looks down after Brick", how: [driven, layer, alive], spot: higher_block, arrived_by: driven, still_f: 5, level_f: 0, acts_at: [3, 18, 32]}
      - {id: brick, doing: "looks up at the crest one beat too late, then slides off the walk", how: [driven, physics, layer], spot: wall_walk, still_f: 0, level_f: 7, acts_at: [8, 12, 40]}
    reveal: payoff
    threat: "hazard:tsunami"
    cause_in: self
    size: WS                        # POV: by Brick's own height fraction
    angle: high
    side: null
    camera: {move: pov, pov_of: jolt, purpose: "you are the one who stepped up in time", ease: none,
             distance_m: 2.8, height_m: 0.85, focal_mm: 21, aperture: 8, focus: subject, pov_smoothing: {pos_s: 0.3, rot_s: 0.5}}
    fit: "First person at Jolt's eyes on her block 2 m above the walk, looking down 35 deg: Brick 2.8 m away (slant),
          Hf 2.8 x 24/21 = 3.2 m, Brick 0.29 (WS by subject fraction). The crest enters top right 8 frames before the key
          and passes under her block; Brick's slide toward the walk's edge stays in the lower left for 30 frames; her
          flinch layer at the key tips the view back."
    key_frame: 12                   # the crest hits Brick: the main hit
    take_ref: {event: K3, take_frame: 548, subject_h: 0.29, source: fit}
    action_cue: {cue_id: tsunami_hazard_cue, tape_frame: 548, expect: {carries_brick: true, jolt_holds: true}}
    frame_at_key: {grid: BL, min_area_pct: 6, max_occlusion_pct: 15, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: R>L, eye_enter: MC, eye_exit: BL}
    sound:
      - {at: 0,  kind: sfx, name: crest_hiss, source: designed, gain_db: -8}
      - {at: 12, kind: music_hit, name: main, source: library, gain_db: 0, sync: main}
      - {at: 12, kind: sfx, name: water_slam_sub, source: designed, gain_db: -2}
      - {at: 40, kind: vo, name: "voice: Brick's yell, fading", source: library, gain_db: -8}
    hook_count: 3
    needs: [{feature: survivor_holds_against_wave, status: verify}, {feature: tsunami_carries_live_bot, status: verify},
            {feature: first_person_camera_with_hands, status: verify}]
    fallback: "Over Jolt's shoulder, 0.4 m behind her head, 28 mm: Brick still 0.27. Wave too high: tune it under her block"

  - id: S09
    section: aftermath
    kind: gameplay
    cut_in: 310
    cut_out: 345
    tape_in: 594
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: brick}
    also_in_frame: [sea, "castle:wall"]
    shows: [into_sea]
    victim: brick
    tower_level: sea
    learns: "Brick drops into the surf"
    actors:
      - {id: brick, doing: "hits the water, arms thrashing, head fighting up to the air", how: [physics, layer], spot: surf_below_wall, arrived_by: physics, still_f: 0, level_f: 6, acts_at: [12, 22]}
    reveal: consequence
    cause_in: prev
    size: WS
    angle: low
    side: {face: profile, yaw_deg: 70}
    camera: {move: static, purpose: "the splash from where it lands, not from the sky", ease: none,
             distance_m: 3.5, height_m: 0.6, focal_mm: 24, aperture: 8, focus: subject, params: {pitch_deg: 25}}
    fit: "Lens on the rocks 0.6 m above the surf, 3.5 m from where the take has Brick land, pitch up 25 deg:
          Hf 3.5 x 24/24 = 3.5 m, Brick 0.27 at the splash (WS); the wall's 25 m face rises out of the top of frame,
          so the height of the drop reads."
    key_frame: 12                   # he hits the water
    take_ref: {event: K4, take_frame: 606, subject_h: 0.27, source: fit}
    frame_at_key: {grid: MC, min_area_pct: 5, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: down, eye_enter: ML, eye_exit: MC}
    sound:
      - {at: 12, kind: sfx, name: big_splash, source: take, gain_db: -3}
    hook_count: null
    needs: [{feature: tsunami_carries_live_bot, status: verify}]
    fallback: "Hold S08 to 345; Brick's drop stays in its lower left"

  - id: S10
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
    actors:
      - {id: jolt,  doing: "breathes hard on her block, looks down at the foam, then out to sea", how: [driven, alive], spot: higher_block, still_f: 18, level_f: 20, acts_at: [10, 52, 80]}
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

`tools/lint.py` run on 2026-11-20 (revision 5): 0 hard failures, every soft lint passing or n/a
except L56, waived in writing. Body = frames 0-345 (11.5 s). Body-shot lengths: 40, 60, 50, 42, 30, 18, 12, 58, 35.

| Lint | Check | Result |
|------|-------|--------|
| L01 | 0-40-100-150-192-222-240-252-310-345-450 | pass |
| L02 | one subject per shot; longest `learns` 9 words; no " and " | pass |
| L03 | hook, teach x2, escalate x3 (threat, stakes, scale), breath, payoff, aftermath, title | pass |
| L04 | S01 gameplay at 0 | pass |
| L05 | mayhem: first core verb is S02's `pvp_hit`, key 70 = 2.33 s ≤ 5.0, Brick and Jolt both in frame | pass |
| L06 | median 40 f; shortest 12; longest 60 ≥ 45; window [150, 300) holds 150, 192, 222, 240, 252 = 5 | pass |
| L07 | closing title 345 (11.5 s) in [330, 360]; title plus card 105 in [90, 120] | pass |
| L08 | staged: first whole view of the wave S04 at 150; before it S01 `knows` 40 + S03 `reaction` 50 = 90 ≥ 21. Causes: S06 prev = S05 (`knock_off`), S08 self, S09 prev = the payoff | pass |
| L09 | first core verb S02: 60 f, key 30 (30 and 30 from the ends), attacker and victim in frame | pass |
| L10 | keys from start/end: 20/20, 30/30, 25/25, 15/27, 10/20, 9/9, 6/6, 12/46, 12/23, 30/75 | pass |
| L11 | onsets 1, 71, 164, 263, 346 within 2; S08 key 252 + 12 = 264 = main; title 345 = title hit | pass |
| L12 | every key has a sound at the key frame | pass |
| L13 | no ramps | pass |
| L14 | S03 MCU; hero Jolt, 3q_front, yaw 25, `reaction` = the scream layer | pass |
| L15 | statics, handheld and POV `none`; S04 tilt_down and S10 pull_out `in_out` | pass |
| L16 | S01 and S02 are both Jolt in WS: S01 eye, yaw 35; S02 low, yaw -25 (65° apart); POV pairs skipped | pass (the linter caught a jump cut in the first draft, S02 at eye and yaw 40) |
| L17 | S05 POV 0.3 / 0.45 s, S08 0.3 / 0.5 s | pass |
| L18 | pvp_hit S02, S05; hazard_hit S04; knock_off S05, S08; into_sea S09; multiplayer S01; survivors_react S03 | pass |
| L19 | nine `verify` items to the scout list; S04 BLOCKED if the wave cannot be put on the wall walk; nothing the game cannot do | pass |
| L20 | caption 4 words needs 66, has 80; title 105 ≥ 48; tagline 6 words needs 84, has 90 | pass |
| L21 | escalate (42, 30, 18) median 30 ≤ hook + teach (40, 60, 50) median 50 | pass |
| L22 | CV 0.41 | pass |
| L23 | no two neighbours within 2 frames | pass |
| L24 | 100 BPM grid (18 f): only 252 on a beat = 1 of 9 | pass |
| L25 | hook_count 1 (S01), 2 (S04), 3 (S08) | pass |
| L26 | S05 > S06 both L>R; the wave R>L in S04 and S08 | pass |
| L27 | every exit and next entry within one cell (S08 BL > S09 ML) | pass |
| L28 | main 264 ≥ 0.6 x 345 = 207 | pass |
| L29 | wide 8 of 9 = 89 % (POV shots sized by subject fraction: S05 FS, S08 WS); close 0 % | pass |
| L30 | designed moves S04, S10 = 2 of 10 = 20 % | pass |
| L31 | class 15: optional (one 20-frame stop-down) | pass |
| L32 | one rule caption, 4 words; tagline 6 words; no superlatives | pass |
| L33 | S06 (18 f) > S07 sound at 0; S07 (12 f) > S08 sound at 0 | pass |
| L34 | knock_off 2, every other id 1 (core verbs excluded) | pass |
| L35 | first POV S05 30 ≥ 30; WS ≥ 18, FS 12, MCU 50, EWS 42 | pass |
| L36, L37 | two-role only | n/a |
| L38 | every gameplay shot has a `fit` line | pass |
| L39 | this is the example itself | n/a |
| L40 | first hazard_hit 165, first pvp_hit 70, both ≤ 207 | pass |
| L41 | S02 Brick > Jolt, S05 Brick > Pip (Brick's glove arm in frame), S08 the wave > Brick, `cause_in: self` | pass |
| L42 | Jolt in S01 (hook), S05 (escalate, behind Pip), S08 (payoff, her own view) | pass |
| L43 | one hazard | n/a |
| L44 | S01 and S02 are wides with the wall walk and Jolt in the first third; every shot has `tower_level` | pass |
| L45 | S01: four survivors at 0.16 | pass |
| L46 | S05 holds 20 frames after the launch, then S06 (`cause_in: prev`) follows Pip; S08 holds 46 | pass |
| L47 | K1 S02 Jolt 0.29; K2 S05 first person (Pip 0.55); K3 S08 first person (Brick 0.29); K4 S09 Brick 0.27 at the splash | pass (revision 3's payoff had Brick at 0.11 and would fail) |
| L48 | every key beat has `take_ref` (event, take frame, subject size, source `fit` until a take is committed) | pass |
| L49 | POV 30 + 58 = 88 of 345 body frames = 26 % ≥ 25 %; two POV shots, both on key events (K2, K3); `pov_of` brick and jolt | pass |
| L50 | POV 26 % ≤ 60 % | pass |
| L51 | `layers` starts with the world pass; all four cast members have a layer | pass |
| L52 | 19 visible-actor entries across S01-S10, each with a `doing` and a `how` (S06 and S09: physics plus a layer) | pass |
| L53 | planned runs (`motion_source: plan`): longest still 18 and level-head 20, both Jolt under the title (S10) | pass (re-measure from the take report after capture) |
| L54 | Jolt wall walk > higher block in S08 `arrived_by: driven` (her jump is on screen in S07); Pip into the air (S06) and Brick into the surf (S09) by `physics` | pass |
| L55 | closest starts between two actors 5 frames (S01, S02, S05, S07); S02's 30/31 and S05's 10/11 are the contact frames of K1 and K2 | pass (the linter caught Brick at 10 and Pip at 6 in S05 in the first draft) |
| L56 | S03 (hero MCU), S05 and S08 (POV) not performed | waived: no person booked; all four in one contact pass |

What it still owes before capture: R01 on every shot (Pip at 0.09 in S06 must read as a figure);
R02 on the wave (it reaches the wall walk from the planned side within 8 frames: run variations of
the `hazard` cue and keep the take that does), the glove launch (Pip leaves frame within 3 frames),
Brick swept and Jolt kept (or the wave's height tuned below her block); the melee-through-`fire`
check; the first-person camera showing Brick's glove arm and Jolt's hands (else the over-the-shoulder
fallbacks); Jolt's colour in a still. After capture: every `take_ref` re-derived from the committed
take (Brick's real landing point moves S09's lens), `take_status: committed`, and the lints re-run.

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

Revision 4: the follow no longer pulls back to 9 m for the payoff (Kestrel would have been about
0.1 of frame); it hands its aim to Kestrel and closes to 3.2 m, so the funnel takes her large in
frame. The betrayal is now Tansy's own view: the viewer is the friend who shoves. Because a person
performs Vela, the pass order matters: the world and the bots first, Vela performed against that
playback with live physics, then overlays timed from the take.

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
capture: {hand: none, survivors: "Vela performed by a person; the others live in the same pass, driven through the game's controls (bots alone wander off the crown), weapons from their marks", take: null, take_status: planned}
motion_source: plan
waive:
  - {lint: L56, shot: S03, reason: "Tansy's shove touches Vela, whom the person performs in the same live pass; one person cannot perform both marks. Tansy is driven (climb, go to, punch) with a lookAt layer and the alive layer"}
key_events:
  - {id: K1, frame: 120, kind: knock_off, attacker: vela, victim: okra, what: "Vela's glove launches Okra off the crown"}
  - {id: K2, frame: 480, kind: knock_off, attacker: "hazard:tornado", victim: kestrel, what: "the funnel rips Kestrel off (payoff)"}
  - {id: K3, frame: 690, kind: knock_off, attacker: tansy, victim: vela, what: "Tansy shoves Vela off the stump"}
layers:
  - {order: 1, kind: world, actor: world, how: "rehearsal world pass with Vela as a stand-in: the three hazard cues, gear and fire for the bots; read its take report for where each disaster lands", gives: "the timings Vela's performer rehearses against"}
  - {order: 2, kind: actor, actor: vela, how: "the take that counts: one live pass with a person performing Vela AND every bot live (a StudioPass with performer vela, liveWorld, every track in record); the Perform tab's Live physics records only her and the world, so Okra would be a playback copy her glove cannot move", reacts_to: [K1]}
  - {order: 3, kind: actor, actor: okra, how: "driven in pass 2: a shove loop with Sprout at the stump; overlays: knockback and scream from K1's take frame", reacts_to: [K1]}
  - {order: 4, kind: actor, actor: kestrel, how: "overlays: lookAt hazard from the funnel's first frame; scream from K2", reacts_to: [K2]}
  - {order: 5, kind: actor, actor: sprout, how: "driven in pass 2: the shove loop with Okra; overlays: lookAt actor:okra at K1 + 8 frames; cower during the quake; the alive layer between", reacts_to: [K1]}
  - {order: 6, kind: actor, actor: tansy, how: "live in pass 2 with a fire cue (PvP dagger) aimed at vela at K3's frame; then an overlay lookAt actor:vela, which her first-person view follows", reacts_to: [K3]}
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
    actors:                         # revision 5: action starts in shot frames, staggered by attention
      - {id: vela,    doing: "dodges meteors across the crown, hunts Okra with the glove, then clings on", how: [performed], spot: crown, still_f: 8, level_f: 18, acts_at: [10, 48, 112, 120, 275, 333, 385, 490]}
      - {id: okra,    doing: "fights Sprout for the stump until Vela's glove launches him", how: [driven, physics, layer], spot: crown, still_f: 6, level_f: 14, acts_at: [20, 60, 121]}
      - {id: kestrel, doing: "takes cover, lines up a rocket on Vela, then looks up at the funnel", how: [driven, layer, alive], spot: crown, still_f: 12, level_f: 20, acts_at: [30, 300, 325, 392, 481]}
      - {id: tansy,   doing: "climbs the north face, peeks over the edge, waits for her chance", how: [driven, alive], spot: crown_north_face, still_f: 10, level_f: 12, acts_at: [0, 140, 260, 410]}
      - {id: sprout,  doing: "trades shoves with Okra, then cowers through the quake", how: [driven, layer, alive], spot: crown, still_f: 9, level_f: 16, acts_at: [15, 66, 128, 280]}
    size: WS
    angle: eye
    side: {face: 3q_back, yaw_deg: 140}
    camera: {move: follow, purpose: "stay with Vela through the whole storm", ease: damped,
             distance_m: 3.0, height_m: 1.2, focal_mm: 24, aperture: 8, focus: subject,
             params: {lag_pos_s: 0.7, lag_rot_s: 0.45, aims: "vela to 380, then kestrel", swing_deg: 40, close_to_m: 3.2,
                      swing_from_frame: 380, swing_to_frame: 470, aim_offset_y_m: 0.85}}
    fit: "Start: 3.0 m behind-left, Hf = 3.0 m, Vela 0.31 (WS); the 6 m crown block under her and the others within
          8 m (Hf at 8 m = 8 m, survivors 0.12, all five at h >= 0.08). A 3 m meteor 8 m out is 0.38 of frame. At 120 Okra
          is 3.5 m from the lens: Hf 3.5 m, 0.27. From 380 the camera swings 40 deg and hands its aim to Kestrel, closing to
          3.2 m from her: at the key (480) Hf 3.2 m, Kestrel 0.29, Vela 1.5 m beyond her at 0.20; the 16 m funnel 25 m out
          (frame 25 m tall there) fills the top right, and Kestrel's lift toward it leaves through the top edge."
    key_frame: 480
    moments:
      - {at: 0,   section: hook,     reveal: whole, threat: "hazard:meteor_shower", hook_count: 1, shows: [several_survivors],
         what: "Meteors already rain on five climbers", sound_at: 0}
      - {at: 45,  section: teach,    reveal: consequence, cause_in: self, shows: [hazard_hit],
         what: "A meteor smashes the block beside Vela", sound_at: 46}
      - {at: 120, section: teach,    reveal: none, shows: [pvp_hit, knock_off, weapon], attacker: vela, victim: okra,
         what: "Vela's giant glove launches Okra off the crown", sound_at: 120,
         take_ref: {event: K1, take_frame: planned, subject_h: 0.27, source: fit}}
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
         attacker: "hazard:tornado", victim: kestrel, what: "The funnel rips Kestrel off the crown", sound_at: 480,
         take_ref: {event: K2, take_frame: planned, subject_h: 0.29, source: fit}}
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
    actors:
      - {id: vela,    doing: "cheers at the empty sky, arms up, head thrown back", how: [performed, layer], spot: crown_stump, arrived_by: performed, still_f: 6, level_f: 0, acts_at: [3, 20]}
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
    also_in_frame: [tansy, "tower:stump", sea]   # tansy: her dagger hand in the lower right (first person)
    shows: [pvp_hit, knock_off]
    attacker: tansy
    victim: vela
    tower_level: top
    learns: "Tansy shoves Vela off the stump"
    actors:
      - {id: tansy,   doing: "climbs onto the stump behind Vela, eyes on her back, lunges with the dagger", how: [driven, layer], spot: crown_stump, arrived_by: driven, still_f: 0, level_f: 8, acts_at: [0, 48, 60]}
      - {id: vela,    doing: "catches her breath facing the sea, turns her head too late", how: [performed, physics], spot: crown_stump, still_f: 10, level_f: 12, acts_at: [10, 54, 61]}
    reveal: none
    size: FS                        # POV: by Vela's own height fraction
    angle: eye
    side: null
    camera: {move: pov, pov_of: tansy, purpose: "you are the friend who ends it", ease: none,
             distance_m: 1.4, height_m: 0.85, focal_mm: 21, aperture: 8, focus: subject, pov_smoothing: {pos_s: 0.3, rot_s: 0.45}}
    fit: "First person at Tansy's eyes climbing onto the stump, 21 mm (k 1.14): Vela 1.4 m ahead facing the sea,
          Hf 1.6 m, Vela 0.59 (FS by subject fraction); the dagger hand enters lower right at 40; after the poke at 60
          she tips over the edge and drops out of the bottom of frame by about 80; the sea fills the lower third."
    key_frame: 60                   # the shove
    take_ref: {event: K3, take_frame: planned, subject_h: 0.59, source: fit}
    frame_at_key: {grid: MC, min_area_pct: 20, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: L>R, eye_enter: MC, eye_exit: BC}
    sound:
      - {at: 60, kind: music_hit, name: shove, source: library, sync: shove}
      - {at: 60, kind: sfx, name: dagger_poke, source: take, gain_db: -4}
      - {at: 100, kind: sfx, name: splash_far, source: designed, gain_db: -8}
    needs: [{feature: melee_hit_through_fire_cue, status: verify}, {feature: first_person_camera_with_hands, status: verify}]
    fallback: "Over Tansy's shoulder, 0.4 m behind her head, 28 mm: Vela 0.45. No melee through fire: knockback then fall layers on Vela at 60"

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

`tools/lint.py` run on 2026-11-20 (revision 5): 0 hard failures; L56 waived in writing. Body = frames 0-750 (25.0 s). Body shots: S01 570 (the take), S02 60, S03 120.

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
| L16 | S01 > S02 are Vela in WS then MCU; S03 is a POV (skipped) | pass |
| L17 | S03 POV 0.3 / 0.45 s | pass |
| L18 | hazard_hit 45, 190, 270, 420; pvp_hit 120, S03; knock_off 120, 480, S03; weapon 120, 330; several_survivors 0; survivors_react S02 | pass |
| L19 | six `verify` items on the scout list; the take is BLOCKED if two of the three hazards cannot run in one take; nothing the game cannot do | pass |
| L20 | caption 4 words needs 66, has 90; title 150 ≥ 48; tagline 7 words needs 93, has 120 | pass |
| L21-L24, L30, L33 | montage only | n/a |
| L25 | hook_count 1 (0), 2 (270), 3 (480) | pass |
| L26 | no cause-effect cut flips direction | pass |
| L27 | S01 > S02 MC>MC; S03 is a new beat; the card is a smash cut | pass |
| L28 | main 480 ≥ 0.6 x 750 = 450 | pass |
| L29 | wide 2 of 3 = 67 % (S03 POV is FS by Vela's fraction); close 0 % | pass |
| L31 | stop-down 462-480 = 18 f ≥ 9 | pass |
| L32 | one caption, 4 words; tagline 7 words; no superlatives | pass |
| L34 | knock_off 3, weapon 2, others 1 (core verbs excluded) | pass |
| L35 | WS 570, MCU 60, POV 120 ≥ 30 | pass |
| L36, L37 | two-role only | n/a |
| L38 | every gameplay shot has a `fit` line; S04 is a card | pass |
| L39 | this is the example itself | n/a |
| L40 | first hazard_hit 45, first pvp_hit 120, both ≤ 450 | pass |
| L41 | 120 Vela > Okra, both in frame; S03 Tansy > Vela (her dagger hand in frame); 480 the tornado > Kestrel, `cause_in: self` | pass |
| L42 | Vela through the take (hook to payoff) and in S02, S03 (aftermath) | pass |
| L43 | meteors at 0 (hook), earthquake at 230 (escalate), tornado at 380 (escalate): in order, each later one in an escalate beat | pass |
| L44 | S01 is a wide with the crown and Vela from frame 0; every shot has `tower_level` | pass |
| L45 | S01: five survivors within 8 m (0.12) | pass |
| L46 | 120 holds 70 frames before the next beat; 480 holds 50; S03 holds 60 after the shove | pass |
| L47 | K1 Okra 0.27 at 3.5 m; K2 Kestrel 0.29 at 3.2 m (revision 3 had about 0.1 at 9 m and would fail); K3 Tansy's first person, Vela 0.59 | pass |
| L48 | every key beat has `take_ref`; take frames `planned` (planning-only, no take) | pass |
| L49 | single take: S03 is a 120-frame POV on key event K3, `pov_of: tansy` | pass |
| L50 | POV 16 % ≤ 60 % | pass |
| L51 | `layers` starts with the world; every cast member has a layer | pass |
| L52 | five actors in the take, Vela in S02, Tansy and Vela in S03; Vela `performed` throughout | pass |
| L53 | planned runs; longest still 12 (Kestrel in cover), level-head 20 | pass (planning-only: measured at capture) |
| L54 | Vela crown > stump (S02) `performed`, Tansy north face > stump (S03) `driven` | pass |
| L55 | closest starts 5 frames (Vela 10 / Sprout 15 / Okra 20; Vela 275 / Sprout 280); 120/121 is K1's contact | pass |
| L56 | S02 Vela performed; S03 is Tansy's POV, not performed | waived: Tansy's shove touches the performed Vela in one pass |

### What planning-only mode leaves owed (from E)

Deferred, not failed: stage 2 (no music file), L11's measured-onset clause, every R-lint, stage 5
onward. **To verify at scout:** a person performing Vela's route; three hazards in one take (the
scheduler's cap of two active does not apply to cues, but nobody has run three: verify); a meteor
landing near Vela (pick the nearest impact in the take, shift the moment); the earthquake reading on
the blocks; the tornado lifting a live bot (Kestrel); the glove and the dagger through the `fire`
cue; the rocket's blast removing Vela's block; Vela's colour in a still; a performed pass with
every bot live beside the performer (a driver-built `StudioPass`, `record-and-layer.md` section 3);
the first-person camera showing Tansy's dagger hand. **Before any render:**
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

Concept (revision 4): the wave is already standing when the trailer starts (cold open), and we see
it through Coral's eyes in one unbroken first-person take, so every escalation is a consequence that
happens to *us*: the blocks ripped out ahead, Finn and Ivo thrown past, our own block tipping into the
sea. Then a cut to her face as she surfaces. Arc: *already, closer, gone*. Rejected: (1) a staged
montage from a calm sea, effect, notice, then the wave: it spends 3 s before the only image this
brief cares about; (2) tornado lifting blocks off the crown: the tornado's reach on this map is
unknown and its funnel reads small at survivor scale; (3) **revision 3's version of this plan**: the
same take as a third-person pull-out to 15 m, which left the climbers at 0.063 of frame height for
both key events (two thrown into the sea, the payoff). That is the "super distant shot during the
important event" the owner named; it now fails L47.

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
pov_led: true                       # a first-person-led cut: L50's 60 % cap does not apply
capture: {hand: none, survivors: "one world pass: a person performs coral in the headset while finn and ivo are live and driven (a driver-built pass, untested); fallback: all three driven", take: null, take_status: planned}
motion_source: plan
key_events:
  - {id: K1, frame: 180, kind: into_sea, victim: finn, what: "Finn and Ivo are thrown into the sea"}
  - {id: K2, frame: 220, kind: into_sea, victim: coral, what: "the block tips; Coral goes in with it (payoff)"}
layers:
  - {order: 1, kind: world, actor: world, how: "RecordWholeSet: the hazard tsunami cue (it pushes survivors; the tsunami cue does not); all three stand-ins live", gives: "the wave, the blocks, the throws"}
  - {order: 2, kind: actor, actor: coral, how: "performed in the world pass (she must be live for the wave to throw her): her own head, hands and feet; rehearse against the rehearsal playback first. Fallback if no person: driven, with lookAt hazard from frame 0, scream at 50, lookAt actor:finn at K1, flinch at 146 (she watched it come) and the alive layer", reacts_to: [K1, K2]}
  - {order: 3, kind: actor, actor: finn, how: "driven in the world pass: a climb toward the top face; overlays: lookAt hazard at 44 (he was looking up), scream from the wave's contact (cue trigger on the hazard cue)", reacts_to: [K1]}
  - {order: 4, kind: actor, actor: ivo, how: "driven in the world pass: the edge loop (walk to the edge, look down, back away); overlays: cower at 100; scream from the hazard cue trigger", reacts_to: [K1]}
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
    also_in_frame: [coral, finn, ivo, "tower:lower_section"]   # coral: her hands and feet (first person)
    shows: [hazard_hit, survivors_react, into_sea]
    readable_survivors: 2           # Finn and Ivo ahead of her at 0.35-0.41
    tower_level: mid
    learns: "A wave takes the section they stand on"
    actors:                         # revision 5: Coral is a person in the headset; her head is the camera for 9 s
      - {id: coral, doing: "watches the wave come, glances at Finn and Ivo, looks down at her footing, braces, grabs as the block tips", how: [performed], spot: lower_section, still_f: 6, level_f: 10, acts_at: [6, 50, 92, 136, 193, 214]}
      - {id: finn,  doing: "climbs for the top face, stops to stare at the wave, then is thrown", how: [driven, physics, layer], spot: lower_section, still_f: 8, level_f: 12, acts_at: [0, 44, 142, 180]}
      - {id: ivo,   doing: "peeks over the edge at the surf, backs away from the crest, cowers", how: [driven, physics, layer], spot: lower_section, still_f: 10, level_f: 6, acts_at: [12, 58, 100, 186]}
    size: WS                        # POV: Finn and Ivo, the nearest survivors, at 0.41
    angle: eye
    side: null
    camera: {move: pov, pov_of: coral, purpose: "you are on the section when the wave comes", ease: none,
             distance_m: 2.0, height_m: 0.85, focal_mm: 21, aperture: 8, focus: subject, pov_smoothing: {pos_s: 0.3, rot_s: 0.5}}
    fit: "First person at Coral's eyes, 0.85 m above a lower section about 10 m above the sea, 21 mm (k 1.14, vFOV
          59.5 deg). Finn and Ivo 2.0 m ahead: Hf 2.3 m, each 0.41. Frame 0: wave top 30 m above the lens at 75 m =
          21.8 deg up; with her head pitched +12 deg the frame spans -18 to +42 deg, so the crest sits at y 0.67 over
          them. 140: the water strikes the blocks 3 m ahead and below, the impact about half the frame height. 180:
          Finn and Ivo thrown at 2-3 m, 0.35-0.41 as they go. 220: the block tips; her head pitches down 40 deg onto
          the sea rushing up, her hands in the lower corners."
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
         what: "Two climbers are thrown into the sea", sound_at: 181,
         take_ref: {event: K1, take_frame: 240, subject_h: 0.38, source: fit}}
      - {at: 220, section: payoff,   reveal: payoff,      threat: "hazard:tsunami", cause_in: self, shows: [into_sea],
         what: "Their whole block tips into the sea", sound_at: 220,
         take_ref: {event: K2, take_frame: 280, subject_h: 0.5, source: fit}}
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
      - {feature: first_person_camera_with_hands, status: verify}
      - {feature: performer_with_live_actors, status: verify}   # a person performs coral with finn and ivo live (record-and-layer.md 4a)
      - {feature: performer_driver, status: verify}
    fallback: "none: BLOCKED if the wave cannot reach a section with survivors on it. No person or no mixed pass: Coral driven with layers. If only the first-person camera
               fails: ride 0.4 m behind and 0.2 m above Coral's head at 24 mm (over the shoulder); Finn and Ivo stay 0.35+"

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
    actors:
      - {id: coral, doing: "breaks the surface gasping, looks for the tower, reaches for a block", how: [performed], spot: sea, arrived_by: physics, still_f: 0, level_f: 8, acts_at: [15, 30, 44]}
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

`tools/lint.py` run on 2026-11-20 (revision 5): 0 hard failures, no soft warning. Body = frames 0-340 (11.33 s). Body shots: S01 280 (first person), S02 60.

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
| L15 | POV and static `none`, each with a purpose | pass |
| L16 | subjects tsunami, coral, card; S01 is a POV | pass |
| L17 | S01 POV 0.3 / 0.5 s | pass |
| L18 | hazard_hit (140), survivors_react (50, S02), into_sea (180, 220) | pass |
| L19 | `verify` items have scout checks; S01's failure is BLOCKED, S02 has a fallback; nothing the game cannot do | pass |
| L20 | caption 4 words needs 66, has 70; title 110 ≥ 48; tagline 6 words needs 84, has 90 | pass |
| L21-L23, L24, L30, L33 | montage only | n/a |
| L25 | hook_count 1, 2, 3 | pass |
| L26 | one wave direction (R>L) inside one take | pass |
| L27 | S01 exit MC > S02 MC; S02 MC > S03 MC | pass |
| L28 | main 220 ≥ 0.6 x 340 = 204 | pass |
| L29 | coarse W/EW 1 of 2 body shots = 50 % (S01 WS by Finn and Ivo at 0.41); close 0 % | pass |
| L31 | class 15: optional (one 15-frame stop-down) | pass |
| L32 | caption 4 words, a rule; tagline 6 words; no superlatives | pass |
| L34 | hazard_hit 1, survivors_react 2, into_sea 1 | pass |
| L35 | POV 280, MCU 60 | pass |
| L36, L37 | two-role only | n/a |
| L38 | every shot has a `fit` line deriving Hf for its subject | pass |
| L39 | this is the example itself | n/a |
| L40 | mayhem only | n/a |
| L41 | no PvP hit or knock-off tagged | pass |
| L42 | Coral in the take (hook to payoff) and S02 (aftermath) | pass |
| L43 | one hazard | n/a |
| L44 | S01 holds the lower section and Coral from frame 0; every shot has `tower_level` | pass |
| L45 | no crowd claim (two climbers ahead at 0.41) | pass |
| L46 | no knock-off tagged | pass |
| L47 | K1 (180) and K2 (220, the payoff) both inside Coral's first-person take; Finn and Ivo 0.35-0.41 | pass (revision 3's pull-out had them at 0.063 and would fail) |
| L48 | both key moments carry `take_ref` (tape 240, 280; source `fit`) | pass |
| L49 | single take: S01 is a 280-frame POV on both key events, `pov_of: coral` | pass |
| L50 | POV 82 % of the body, `pov_led: true` | pass |
| L51 | `layers` starts with the world pass; all three cast members have a layer | pass |
| L52 | Coral `performed`; Finn and Ivo driven, then physics plus layers | pass |
| L53 | planned runs; longest still 10 (Ivo peeking), level-head 12 | pass (re-measure after capture) |
| L54 | Coral lower section > sea (S02) by `physics` (the block tips, on screen) | pass |
| L55 | closest starts 6 frames (Coral 6 / Finn 0 / Ivo 12; Coral 136 / Finn 142) | pass |
| L56 | S01 (POV) and S02 (hero MCU) both carried by a person performing Coral | pass |

What it still owes before capture: R01 (Finn and Ivo visible ahead of her at every key; the
wave's crest in frame at 0 with the head pitch the lookAt layer gives), R02 (the wave reaches the
section at tape frame 200 within 8 frames and throws Finn and Ivo), the first-person camera showing
Coral's hands without her own head in the way (else the over-the-shoulder fallback), S02's
floating check, and a person performing Coral while Finn and Ivo are live in one pass (untested;
fallback: all three driven, Coral with the revision-4 look-at layers and the alive layer). Studio note (revision 3): the `tsunami` cue sets `affectPlayers = false`, so it never
throws the climbers; stage this wave with the `hazard` cue (random side: run variations) or fake the
throws with reaction layers.
