---
name: deadlyjenga-trailer-craft
description: The required craft playbook for Deadly Jenga trailers - a gated pipeline (with a planning-only mode when there is no music file or engine), hard rules by concept type (hand, disaster, two-role), numeric defaults, three differing worked examples, a lintable shot-list schema and a critic protocol. Use it any time an agent plans, shoots, cuts, scores or reviews a Deadly Jenga trailer, teaser, social clip or cinematic shot, before touching Trailer Studio or any render.
---

# Deadly Jenga trailer craft

Attempt 1 (15 s, 2026-11) was a flat list of six events: no sections, shots of 2-3.4 s that were
neither a montage nor a take, music laid under afterwards with its biggest hit on the opening
shot, no hand on screen, no face, a 2.3 s title louder than the body. This skill exists so that
cannot happen again. Structure, rhythm, reveal and camera work are hard rules with lints.

Skill test 1 (`../../SKILL-TEST-1.md`, 2026-11-20) showed the skill wins (12 of 12 blind
judgements) but that its plans cloned the one example, forced a hand into a disaster brief, buried
the muted viewer without a caption, and bent shots to pass lints. Revision 2 fixes those. **Rules
constrain craft, not the idea**: they say how a shot is made and checked, never which idea to pick.

Tags on every hard rule: **[measured]** = counted in 22 official trailers (report 06),
**[sourced]** = working editors, standards, platform specs, attempt 1's documented failures, or
skill test 1's documented findings, **[inferred]** = derived; inferred numbers are defaults, never
hard on their own. Decisions and their evidence: `../../RECONCILIATION.md` (Revision 2 at the end).

## Which reference to open when

| Stage | Open |
|-------|------|
| Brief, concept, beat sheet | `references/structure-and-templates.md` |
| How different plans can look (and what not to copy) | `references/worked-examples.md` (B, C) and `references/plan-schema-and-lints.md` section 3 (A) |
| Music map, target hits, sync plan, sound, mix | `references/rhythm-and-sound.md` |
| Camera placement, moves, the framing check | `references/camera.md` |
| Reveal or cold open, stakes, the hero, comedy | `references/reveals-and-moments.md` |
| Writing and linting the plan | `references/plan-schema-and-lints.md` |
| Critic rounds, or self-review when no critic can run | `references/review-rubric.md` |
| Measuring a render against the 22 references | `references/measured-reference.md` |
| What Trailer Studio can do today (2026-11-20) | `references/studio-tooling.md` (read first, every time) |

## Game facts that set every number

- Survivor about 0.94 m tall, eyes about 0.85 m. Hand about 4 m across (about 3 m of visible
  height side-on). Block 6 x 2 x 2 m, twice a survivor's height. Attempt 1's map: 56 m tower on a
  25 m castle. Maps differ (the pilot's castle spire ends in one block, not a crown): measure the
  map you use.
- The hand is only about 4 times a survivor and shorter than a block. It reads as giant only next to
  survivors at a similar depth and through what it does to blocks. In a whole-tower wide it is about
  5 % of frame height and disappears. Frame a section about 14 m tall instead.
- Free fall: 10 m in 1.4 s, 20 m in 2.0 s, the full 56 m in 3.4 s.
- Lens numbers assume a 24 mm sensor height: frame height at the subject `Hf = distance x 24 /
  focal`. Studio tracks default to 18.66 mm: set `sensorHeight = 24` on every track.
- The hook, in one line: one player is a giant hand pulling blocks out from under the others.

### What the game can and cannot do (verify anything marked unknown before it carries a shot)

| Thing | Status (2026-11-20) |
|-------|---------------------|
| Giant hands on screen | **one**. The hand player flies one hand with both controllers. Never plan two giant hands (a test plan did). |
| Survivors climb, jump, fall into the sea; ride a block the hand moves | can (riding: commit `2496f88d`) |
| Survivor catching a ledge, hanging one-handed | unknown: ledge finding exists for climbing; no hang pose seen in a render |
| Survivor floating or swimming after a fall | unknown |
| Hand's shadow on blocks and survivors | can (seen in the pilot render) |
| Hand drawn in Studio renders | can only on branch `claude/trailer-hand-reveal-pilot` (not merged); verify on any other commit |
| Pull distance | performed and bot pulls moved 0.5-0.7 m in attempt 1; the pilot's directed hand pulled 3.5 m (kinematic) |
| Hand gestures (finger wave, flick) | unknown: a performed hand's fingers are tracked; the directed hand is a fixed grab |
| Disasters in the game | Tsunami (height 1-100 m, default 22; a warning time in which it rises in place; offshore distance and speed settable), Tornado (side settable), Earthquake, Fire, Meteor Shower, Planet-Killer Meteor, Kraken, Acid Rain, Anti-Gravity, Cat Attack, Evil Santa, Icey, Ornament Shower, Planet-Killer Ornament, Power Outage, Vampire. How each looks in a Studio take: unknown except the tsunami and tornado notes in `studio-tooling.md` |
| The sea drawing back before a tsunami | unknown, not found in the tsunami code: treat as cannot until seen |
| Emotes, waves between survivors | bots have emotes; a performed survivor's wave: unknown |
| Voice chat in a take | the game has voice; clean voice captured in a Studio take: unknown (record lines separately) |
| Several real players in one take | unknown; bots stand in for survivors |
| Hand-player or survivor first-person camera in the Studio | `firstPerson` behaviour exists; where the hand player's eye sits relative to the hand: unknown |
| Live action of a person in a headset | cannot (the Studio cannot capture it) |
| Sound cues in the Studio; the directed hand making game sound | cannot today (sound is laid in at assembly) |

A shot that needs an unknown item lists it in `needs` as `verify` with a fallback (lint L19).

## Two modes

**Production** (default): a real music file and engine access through the coordinator. Every gate
applies.

**Planning-only**: use it when there is no music file, no engine access, or the request is for a
plan only. Then:
- **Music (stage 2) is deferred, not failed.** Write target hits (`frame`, with
  `measured_onset_frame: unmeasured`) and a short music brief (tempo range, where the main hit,
  stop-down and title hit fall) for choosing or cutting a track.
- **Deferred gates:** stage 2's onset question; L11's measured-onset clause; every R-lint (R01-R07);
  stages 5-11. Every engine-proof item (hand drawn, pull distance, hazard reach, anything unknown in
  the table above) becomes a `needs: verify` item on a **to verify at scout** list.
- **Not deferred:** stage 0, stage 1 (with its idea and alternatives steps), every plan lint except
  L11's measured clause, the framing check (L38), and the stage-4 review (critic, or the fallback in
  the self-review protocol). Without engine access, name the map you assume (attempt 1's castle
  numbers by default) and derive the `fit` lines from it; the scout re-derives them from measured
  heights. `studio-tooling.md` is used as dated, and the plan says it was not re-checked.
- **Output may be one file**, `plan.md`, with these sections in order: Brief (with must-show proofs);
  Concept (memorable-image ideas, chosen concept, two rejected); Music targets; Plan table (every
  lint field, plus `fit`); Lint table (each lint pass / fail / waived / deferred); Text animatic;
  Review (critic or labelled self-review); To verify at scout; Before any render.
- **Before any render** (copy this list into the plan): (1) choose or cut a track and map it
  (stage 2); (2) move the picture to the measured hits, never the hits to the picture; (3) re-run
  L11; (4) scout every `verify` item, then apply fallbacks or declare BLOCKED; (5) re-run the
  stage-4 review if any key moved more than 6 frames or any shot fell back.
- Example C in `references/worked-examples.md` is a planning-only plan.

## The pipeline (gated)

Each stage writes a file into a numbered version folder (`v01/`, `v02/` ...). Never overwrite a
version. A stage starts only when the previous gate is answered "yes" with evidence (or is
listed as deferred in planning-only mode). **Nothing is rendered until the plan passes stage 4.**
Stage 5 may make scout stills only.

| # | Stage | Artifact | Gate question (must be "yes") |
|---|-------|----------|-------------------------------|
| 0 | Brief | `brief.yaml`: goal, audience, length, aspect, platform (muted-first?), must-show ids each with a one-line proof, one sentence a viewer should repeat, map, hand performed or directed, render budget | Could a stranger write the logline and must-show list from it? |
| 1 | Concept | `concept.md`: five or more one-line "image people will remember" ideas; the chosen concept (logline, concept type, opening, shape, arc in three words, title text); two rejected concepts | Is the hook something only this game has, is the shape declared, and does each rejected concept differ from the chosen one in at least two of shape, opening, payoff, ending? |
| 2 | Music map | `music.yaml`: file, tempo, beats, onsets, named hits (`open`, `reveal`, `main`, `title`), stop-downs, loudness curve | Is every hit taken from the onset list of the actual file? (planning-only: deferred; target hits written) |
| 3 | Plan | `plan.yaml` (schema below) and `lint.txt` | Do all hard plan lints pass, is every soft failure fixed or waived with a reason, and does every shot have a `fit` line? |
| 4 | Animatic and plan review | text animatic (one card per shot, timed to the music) and a fresh critic's notes (or the labelled fallback) | Read only the cards: can the critic name the hook, the payoff and where the title lands, and does it score Hook, Clarity and Story at 3 or more? |
| 5 | Scout | stills at first, key and last frame of every shot from the planned camera; subject area and occlusion from the engine; a dry run of every physics and hazard cue | Is every subject visible at its key frame (R01) and does every cue do what the plan needs (R02)? |
| 6 | Capture | committed take and `take-report.json` | Did every action cue happen within 8 frames of plan, and is the hand (or hazard) drawn? |
| 7 | Preview | 640x360 render of the whole cut with sound; evidence pack | Does every shot show its subject doing its action at preview size? |
| 8 | Rough cut | full cut with temp titles; critic round | No rubric category at 2 or below, weighted mean ≥ 3.5, no hard lint failing |
| 9 | Fine cut, lock | 1080p render; critic round; plan tagged `locked` | Weighted score ≥ 4.0, all categories ≥ 3, planted-defect check passed |
| 10 | Finish | `trailer.mp4`, loudness report, profile check | Render lints R03-R05 pass |
| 11 | Human pass | Evan's or a teammate's notes | Has a person watched and listened? |

Rules for the pipeline:
- Go back one gate, not to the start. Each critic note names the stage it belongs to.
- After lock (stage 9), only sound, titles and grade change, unless a hard lint fails.
- If a stage needs a Studio feature that does not exist, check `references/studio-tooling.md` for
  the workaround. If there is none, the plan is BLOCKED: stop and report the feature.

## Concept types

Set `concept_type` in the plan. The core-verb, hold and BLOCKED rules follow it. Never add the hand
to a brief that does not ask for it, and never drop it from one that does.

| | `hand` | `disaster` | `two_role` |
|---|---|---|---|
| Core verb (rule 4), its `shows` id | `pull`: the hand moves a block out of its slot with a survivor in frame | `hazard_hit`: the hazard visibly moves blocks or survivors, with the tower or a survivor for scale | `pull`, as `hand` |
| First hold (rule 6) | first pull shot | first `hazard_hit` shot | first pull shot |
| Opening | staged (partial, reaction, knows) or cold open on the hand | staged (effect, notice) or cold open on the hazard (frame 0) | staged usually; each role seen as a player by mid-body |
| Extra hard rule | - | - | both roles shown as players before half the body (L36) |
| BLOCKED if | the hand is not drawn in the scout | the hazard cannot be staged to reach or be seen | the hand is not drawn |
| Example | A | B | C |

The type follows what the brief makes the star. A hand cut with a disaster in it is `hand`; a
disaster-led cut whose brief also asks for the hand stays `disaster` (the hand's shots then carry
their own verify and fallback, and the hand gets its own reveal). Each threat gets its own reveal
(rule 6).

**Must-show items come from the brief, not from a fixed list.** Copy one id per thing the brief
asks for (multiplayer, friends, a disaster, each side's point of view, climbing, a fall), and write
what in frame proves it. "Multiplayer" is proven by players on screen acting independently or
answering each other, never by implication (a test plan lost marks for that). Suggested ids and
proofs: `structure-and-templates.md` section 1.

## Hard rules

Each is checked by the lint named in brackets (formulas in `references/plan-schema-and-lints.md`).

1. **Plan before pixels.** No render of any shot until the plan passes every hard plan lint and
   the stage-4 review. [sourced: Lieu's garbage cut, previs practice; attempt 1 spent about 3 of 5
   renders finding lines of sight] (pipeline gate)
2. **Structure, not a list.** Sections run in this order: `hook`, `teach`, `escalate` (one or
   more), `payoff`, then `title`; an optional `breath` may sit among the escalate beats or after the
   payoff, and an optional `aftermath` after the payoff. Every `escalate` beat names what it raises
   over the last: stakes, scale, threat, count, speed or novelty. [sourced: Lieu, Cauller "teach,
   test, escalate"] (L03)
3. **Declare a shape and meet it.** *Montage:* median body shot 1.0-2.2 s, shortest body shot ≤ 1.0 s,
   longest ≥ 2.0 s (15 s) / 3.0 s (30 s) / 3.3 s (60 s), and some 5 s window with 4 or more cuts.
   *Single take:* one picture shot covers ≥ 70 % of the body, at most 2 cuts in the body. Never 5-6
   even shots of 2-3 s. [measured: 16 of 18 mid and long montages; short refs are single takes;
   15 s and 30 s floors extrapolated] (L06)
4. **Gameplay first, core verb early.** Gameplay on screen by 2.0 s; frame 0 is not black, a logo or
   a card. The concept's core verb (table above) is readable by 5 s (15 s), 8 s (30 s), 12 s (60 s).
   [measured: 13 of 21 by 2 s; 15 of 21 by 12 s; 15 s and 30 s extrapolated] (L04, L05)
5. **Title late, card long enough.** Closing title starts at 11.0-12.0 s (15 s), 24-26 s (30 s),
   53-55 s (60 s); title plus end card lasts 3.0-4.0 s / 4-6 s / 5-7 s. Default 15 s: title at
   11.5 s (77 %), card 3.5 s. No game name earlier unless laid over gameplay. At 15 s the measured
   bands cannot both hold (80-95 % position leaves a 0.75-3.0 s card; 21 of 22 measured cards are
   3.5 s or longer), so the window trades a little position for a readable card; it is extrapolated.
   [measured: title at 80-95 % in 18 of 22, at 75 % or later in 20 of 22; card 4-8 s in 15 of 22]
   (L07)
6. **Stage the reveal, or open cold on purpose.** For each threat (the hand, each hazard): if its
   first full view comes after frame 0, at least 0.7 s of anticipation about that threat comes
   first (an effect, a partial view, a reaction or an audience-knows shot). Opening on the threat
   itself at frame 0 is an allowed, named choice (`opening: cold_open`): hold that view uncut for
   at least 1.0 s, and let escalation come from consequences (two or more escalate beats showing
   what it does, each raising something different). Every effect (fall, collapse, splash) has its
   cause in frame or in the shot before. The first shot of the core verb is uncut for at least
   1.5 s with its key moment 12 or more frames from each end and a scale reference in frame.
   [sourced: Hitchcock, Jaws, Lieu's cause-effect and hold rules; skill test 1: the best-scoring
   hook was a wave standing up at frame 0; thresholds inferred] (L08, L09)
7. **Land the payoff and the title on hits.** Hits come from onset detection on the music file,
   never from a guessed grid (planning-only: target hits, marked unmeasured, re-timed before any
   render). The payoff's key moment is within 2 frames of the main hit; the closing title starts
   within 2 frames of the title hit; every planned sync point appears as an onset in the rendered
   mix within 2 frames. Do not snap every cut to the beat: the references do not (25.0 % on beats
   vs 24.9 % chance); they lean to hits. [sourced: Lieu, Violet Recording; measured lean to onsets,
   p = 0.004 in fast passages] (L11, R03)
8. **Every key moment has a sound.** A sound event within 3 frames after each key frame and never
   more than 1 frame before it. A lead-in (a roar or creak 0.5-1.5 s before a threat appears) is a
   separate sound and is allowed. A speed-ramped shot carries no real-speed take sound. [sourced:
   Lieu on SFX as percussion; ITU-R BT.1359; attempt 1 F4] (L12, L13)
9. **A face and a close shot.** At least one shot of MCU or tighter, and at least one shot where the
   hero survivor's face reads (facing within 60° of camera, MS or tighter). A hero at a fifth of
   frame height in a wide has no readable reaction (the pilot's hero at 0.17): cut to the face.
   [measured: most references have a close shot; sourced: Kuleshov reaction; attempt 1 F16; pilot
   report; 60° inferred] (L14)
10. **Real camera work.** Every shot declares one move from the library in `references/camera.md`
    and its purpose; moving cameras ease in and out (or are damped); no unmotivated drift. No jump
    cut: two consecutive shots of the same subject must not share size, angle class and yaw within
    30°. No raw head-locked VR view; every POV is smoothed. [sourced: Lieu, Nesky, 30-degree rule,
    Gartner; measured: POV is normal in VR trailers when smoothed] (L15, L16, L17)
11. **Derive every frame.** Every shot's `fit` line computes the frame height at its subject
    (`Hf = distance x 24 / focal`), the subject's fraction of it (matching its size), and shows that
    the action point fits: the impact, the block's travel, the face, both threats if both are
    claimed. Numbers are derived for this shot, never copied from an example or another shot, and
    nothing in frame exceeds what the game can do (one hand). [sourced: skill test 1, where critics
    found copied frames that could not contain their action and a shot with two hands] (L38, L19)
12. **Prove it in the engine before rendering.** At each key frame the subject's screen area is at
    least `min_area_pct` and its occlusion at most `max_occlusion_pct` (from the pass table in
    `references/camera.md`; never above 20 % for a person or 30 % for the hand). A pull moves its
    block at least 2 m within 1.5 s in the dry run. If a hand or two-role concept cannot show the
    hand, or a disaster concept cannot stage its hazard, it is BLOCKED: stop and report; never ship
    it without its subject. In planning-only mode these items go on the to-verify-at-scout list.
    [sourced: attempt 1 F5-F7, F10; 2 m inferred] (L19, R01, R02)
13. **Deliver a correct mix.** Integrated -20 to -12 LUFS (target -15 ±1); true peak ≤ -1.0 dBTP;
    the body's last quarter at least as loud as its first; the title section at least 3 LU quieter
    than the body; no more than 3 flashes in any 1 s; no black or silence over 0.25 s at frame 0.
    [measured: 13 of 22 in the window, 16 of 20 rising, 16 of 21 title ≥ 6 LU down; sourced:
    true-peak delivery spec, ITU-R BT.1702] (R04)
14. **Rules serve the idea; never game them.** Never relabel a shot, drop a true `shows` label,
    shorten a beat or change a cause just to pass a lint. If a rule fights the concept, keep the
    concept and write a waiver with the reason; if a hard rule fights it, report that to the human.
    Never reuse an example's tagline, caption, hero or cast ids. [sourced: skill test 1, where
    planners admitted relabelling and bending shots, and 4 of 4 plans cloned the example] (L39;
    critic C2 and C10 check labels against the shot)
15. **A person listens.** Nothing is called finished until a human has watched and listened (stage
    11). You cannot hear; meters are not ears. [sourced: attempt 1 F18] (R07)

## Defaults by length

Use these unless the brief says otherwise. Soft lints warn when they are broken; waive with a reason.

| | 15 s | 30 s | 60 s | Tag |
|---|---|---|---|---|
| Body shots (montage) | 6-9 | 12-18 | 22-32 | measured mid, scaled |
| Median body shot | 1.2-1.6 s | 1.3-1.7 s | 1.4-1.7 s | measured |
| Cuts per minute of body | 30-45 | 25-40 | 25-38 | measured mid IQR, 15 s scaled |
| Shot-length CV | ≥ 0.35 | ≥ 0.35 | ≥ 0.35 | inferred |
| Sync points (incl. main and title) | 3-5 | 5-8 | 8-14 | inferred |
| Stop-down (≥ 0.3 s, ≥ 12 LU dip) | optional, 0.3-0.5 s before main hit | 1 | 1-2 | measured 14 of 22 |
| Main hit position | last 40 % of body | last 40 % of body | last 40 % of body | inferred |
| Loudness ramp, last vs first quarter | +2 to +6 LU | +2 to +6 LU | +2 to +6 LU | measured |
| Title vs body | -4 to -10 LU | -6 to -12 LU | -6 to -12 LU | measured (weak for 15 s) |
| Wide (EWS, WS, FS) share of body shots | ≥ 40 % | ≥ 40 % | ≥ 40 % | measured |
| Close (CU, ECU, INSERT) share | ≤ 25 % | ≤ 15 % | ≤ 15 % | measured |
| Designed moves (montage) | 10-30 % of shots | same | same | measured median 11 %, reveals 21 % |
| Hook appearances (featured shots) | 2-3 | 2-3 | 3 | sourced (rule of threes) |
| In-body captions | 1 plain rule caption for store and muted social cuts, else 0-1; ≤ 6 words | 1 for store and muted, else 0-2 | 0-4 | measured 11 of 22 use some; test: rule captions read better muted |
| End-card text | tagline ≤ 8 words, call to action ≤ 6 words | same | same | inferred |
| Button after title | none; a gag under the card if any | optional | optional | measured 2 of 22 |
| Escalate shots shorter than hook + teach shots | yes | same | same | measured tendency 12 of 19 |

Also by default: straight cuts (no crossfades without a stated reason), master short-term max
≤ -10 LUFS, stereo 48 kHz AAC, picture at 30 or 60 fps, brighter than the headset look.

## Three worked examples, three different shapes

All three pass every lint (check tables beside each). They exist to show the method, not a spine
to fill in. A fourth plan should differ from all three in at least two of shape, opening, payoff and
ending, and must re-derive every camera number (rule 11).

| | A "The Pull" | B "Already Here" | C "Both Sides" |
|---|---|---|---|
| Where | `plan-schema-and-lints.md` §3 | `worked-examples.md` §1 | `worked-examples.md` §2 |
| Length, type, mode | 15 s, `hand`, production | 15 s, `disaster`, production | 30 s, `two_role`, planning-only |
| Shape | montage, 8 body shots | single take (82 % of body) plus a face | montage, 15 body shots |
| Opening | staged: block slides, fingertips cropped (partial) | cold open: the wave already standing at frame 0 | survivor first-person climb; audience-knows |
| Core verb at | 4.5 s (pull beside the hero) | 4.7 s (wave rips out the blocks) | 7.2 s (pull under a friend) |
| Payoff | long-lens crown topple on the main hit | the section tips into the sea at the end of a pull-out | crown folds toward a low lens |
| Ending | crane up over the stump, title over picture | the hero surfacing, then a hard cut to a card | the two friends waving, title over the hand's view |
| Title, card | 11.5 s, 3.5 s | 11.33 s, 3.67 s | 25.2 s, 4.8 s |

## Shot-list schema (compact)

Full field list, allowed values and the lints: `references/plan-schema-and-lints.md`.

```yaml
id: my-cut-15
mode: production        # production | planning_only
concept_type: hand      # hand | disaster | two_role
opening: staged         # staged | cold_open (+ cold_open_threat)
threats: [hand]
fps: 30
length_frames: 450
class: 15               # 15 | 30 | 60 (sets the numeric limits)
shape: montage          # montage | single_take
hero: <your id>         # never an example's id
must_show: [...]        # from the brief, each with a proof in brief.yaml
music: {file: ..., bpm: ..., hits: [{name: main, role: main, frame: 240, measured_onset_frame: unmeasured}]}
titles: [{text: "DEADLY JENGA", role: title, frame_in: 345, frame_out: 450, closing: true}]
shots:
  - {id: S03, section: teach, cut_in: 105, cut_out: 165,  # trailer frames; cut_out of n = cut_in of n+1
     subject: {kind: hand, id: hand}, also_in_frame: [<hero>, "block:207"],
     shows: [hand, pull],               # everything it truly proves
     learns: "...",                     # <= 12 words, one idea, no "and"
     raises: null, reveal: whole, threat: hand, role_view: neutral,
     size: WS, angle: low, side: {face: 3q_front, yaw_deg: 30},
     camera: {move: tilt_up, purpose: "...", ease: in_out, distance_m: ..., height_m: ..., focal_mm: ...},
     fit: "Hf = d x 24 / f at the subject; subject fraction; why the action point is in frame",
     key_frame: 30, sound: [{at: 30, kind: music_hit, name: reveal, sync: reveal}],
     needs: [{feature: hand_drawn_in_playback, status: verify}], fallback: "none: BLOCKED if no hand"}
```

## Self-review protocol

You cannot watch motion or hear sound. Judge from evidence, in this split:

| Question | Judge it from | Never from |
|----------|---------------|------------|
| Is the subject in frame, how big, how hidden? | engine projection, raycast or ID buffer | your look at a still |
| Is the subject readable (face, pose, what it is)? | full-size still at the key frame | numbers |
| What is the camera doing? | the camera track's own keys (m/s, deg/s) | stills (models confuse push, zoom, tilt) |
| Do cuts, hits and impacts line up? | sync table: onsets in the mix vs cut and key frames | anything else |
| Is the mix right? | EBU R128 numbers, per-lane levels, the loudness plot | anything else |
| Does it hold together? | the critic, citing the above | any single number |

1. **Build the evidence pack** after every preview or later render (`references/review-rubric.md`
   lists the ten items). Never judge a cut from a video file alone. At stage 4 the pack is the text
   animatic and the lint table.
2. **Answer the pack's questions in writing yourself first**, citing shot and frame. No "looks good".
3. **Run an independent strict critic.** A fresh agent (a different model if you can) gets only the
   brief, concept, plan, lint output, evidence pack, and the previous round's notes (not scores). It
   scores the rubric with evidence and ends with the top three problems, each naming a stage. A hard
   lint that is computed and fails caps its category at 2; a field the plan's format does not have
   is "cannot check", never a failure.
4. **If you cannot start a critic** (spawning agents is not allowed or not possible), do the
   fallback in `references/review-rubric.md` section 3a: a written self-critique against the rubric,
   a defect sweep adapted from the planted-defect check, at least three problems found, the whole
   thing headed **SELF-REVIEW (weaker than an independent critic)**, and a "critic owed" block that
   hands the critic step, with its inputs and prompt, to whoever orchestrates. The gate is then
   "passed on self-review"; in production mode an independent critic must still review before stage 8.
5. **Planted-defect check.** Required every critic round from stage 7; recommended at stage 4,
   where it worked on text plans in skill test 1 (3 of 3 defects found in both copies). Give the
   critic two packs in random order: the real one and a copy with three injected defects. If it
   finds fewer than 2 of 3, or scores the defective copy within 0.25 of the real one, discard the
   review and rerun once; if it fails again, the round goes to the human.
6. **Read the critic's errors before its number.** In skill test 1, strict critics scored clean plans
   2.8-3.0 while editor-style graders gave the same plans about 4.5. The critic's absolute score is
   strict and not comparable to a human editor's; its list of concrete errors (frames that cannot
   contain their action, a shot with two hands) is what matters. Fix the errors; compare scores only
   between rounds of the same critic set-up.
7. **Fix the top three notes first**, from the end of the timeline backwards, and re-run the lints.
8. **Stop** when any holds:
   - *Accept:* no hard lint failing, every category ≥ 3, weighted mean ≥ 4.0, planted check passed.
   - *Plateau:* weighted mean up by less than 0.2 over two rounds. Ship the best version and say what
     held it back.
   - *Round cap:* 3 rough-cut rounds and 2 fine-cut rounds.
   - *Blocked:* a fix needs a Studio feature with no workaround. Stop and report it.
9. **Hand it to a human** (always, after stopping). Give them: the mp4; the plan's beat list (one line
   per shot: time, section, `learns`); the sync table; the loudness numbers; the critic's last top
   three; and questions only ears and eyes can answer: Does the music fit the candy peril tone? Does
   the main hit land on the payoff? Is the title's drop-out too abrupt? Does the hand read as a giant
   player's hand? Is anything harsh, muddy or out of sync? Record their notes; that is R07.

## Common ways an AI gets this wrong, and the fix

| Mistake | Fix |
|---------|-----|
| Events in a row with no sections, no rises, no turn (attempt 1) | Write the beat sheet first; every escalate beat names what it raises (rule 2) |
| Six shots of 2.0-3.4 s: too slow for a montage, too cut for a take (attempt 1) | Declare the shape; for a montage, add a barrage and one short shot ≤ 1.0 s (rule 3) |
| Music laid under the finished picture; the biggest hit fell on the establishing shot (attempt 1) | Map the music first (or write target hits); payoff on the main hit; the title on its own hit (rule 7) |
| The hand never appeared, and the trailer shipped anyway (attempt 1) | Scout the hand at stage 5; if it is not drawn, a hand concept is BLOCKED (rule 12) |
| The pull moved a block 0.7 m seen from 11 m (attempt 1) | Dry-run the cue; need ≥ 2 m; frame so the travel crosses ≥ 15 % of frame width; hold 1.5 s (rules 6, 12) |
| The hero faced away and stood behind a block (attempt 1) | Place the camera from the subject's facing; check facing ≤ 60° and occlusion in the scout (rules 9, 12) |
| No close shot, no face, no human moment (attempt 1) | One MCU-or-tighter face shot and a beat that pays it off (rule 9) |
| Slow motion with real-speed take sound (attempt 1) | Designed sound only under ramps (rule 8) |
| Title held 2.3 s and louder than the body (attempt 1) | 15 s: title at 11.0-12.0 s, card 3.0-4.0 s; drop the music at least 3 LU (rules 5, 13) |
| True peak -0.6 dBTP (attempt 1) | Limit to ≤ -1.0 dBTP (rule 13) |
| Framing found out by full renders (attempt 1, 3 of 5 renders) | Scout stills and engine numbers before any render (rule 1) |
| The stage anchor sat 4 m below the crown (attempt 1) | Measure the top from a height profile; never trust "highest block near the middle" |
| Re-recording for each fix; takes differ every time | Commit one take; plan against its take report |
| Judged from contact sheets and meters, by the maker alone | Evidence pack, independent critic, planted defects, human pass |
| A grey see-through survivor in two shots | Technical check in the filmstrip; fix or cut the shot (critic C9) |
| Four plans, one spine: same collapse, same hang, same hero, same tagline (test) | Five image ideas first; two rejected concepts that really differ; never reuse an example's text or ids (rules 11, 14) |
| Copied camera numbers that could not show the new action (test) | A `fit` line per shot, derived for what that shot must show (rule 11) |
| A shot with two giant hands (test) | Check the can-do list; one hand (rule 11) |
| A hand forced into a disaster-led brief (test) | Set `concept_type: disaster`; the hand appears only if the brief asks (concept types) |
| Multiplayer only implied (test) | Must-show from the brief with a proof: players on screen answering each other |
| No caption, so muted viewers missed the rule (test) | One plain rule caption by default in store and muted social cuts |
| A planned subtle opening beat a frame-0 threat (test) | Consider a cold open; it is allowed when escalation comes from consequences (rule 6) |
| Labels changed, a beat cut short, a cause swapped "for the lint" (test) | Waive in writing instead (rule 14) |
| Hero too small to read a reaction (pilot: 0.17 of frame height) | Cut to an MS-or-tighter face shot; a wide cannot carry the reaction (rule 9) |
| The payoff left frame toward the lens (pilot) | In the `fit` line, place the payoff's end point inside frame; score the frame after the action, not only at the key |
| The hand looked like an untextured rubber glove with a cut stump (pilot) | Light it as a silhouette against bright sky, keep the wrist out of frame or behind blocks, and ask the human pass whether it reads as a hand |
| No closing frame held the hand and tower together (pilot) | Plan a final frame with the hand and the tower (or stump) both readable, before or under the title |
| Hand, hero and lens in one line, so the hand hid the hero (pilot) | Keep them apart on screen by at least the hand's angular radius plus 4°; check occlusion through the grab |
| Over-correcting: every cut snapped to a beat | Only sync points and barrage cuts need hits; keep beat cuts under 80 % |
| Over-correcting: forcing wide-medium-close on every cut | Same-size cuts are normal in gameplay; change the place or action instead |
| Over-correcting: a slow move on every shot | Static is a move; designed moves on 10-30 % of shots |
