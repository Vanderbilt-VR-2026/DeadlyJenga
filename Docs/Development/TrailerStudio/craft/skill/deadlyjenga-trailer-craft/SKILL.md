---
name: deadlyjenga-trailer-craft
description: The required craft playbook for Deadly Jenga trailers, built around disaster and player-versus-player mayhem on a collapsing block tower - a gated pipeline with a scout loop (and a planning-only mode when there is no music file or engine), hard rules by concept type (mayhem, disaster, PvP; the giant hand only as a legacy extra), a moment class for 5-10 s clips, numeric defaults, differing worked examples, a lintable shot-list schema with a shipped linter and assembly tools, and a critic protocol. Use it any time an agent plans, shoots, cuts, scores or reviews a Deadly Jenga trailer, teaser, social clip or cinematic moment, before touching Trailer Studio or any render.
---

# Deadly Jenga trailer craft

Attempt 1 (15 s, 2026-11) was a flat list of six events: no sections, shots of 2-3.4 s that were
neither a montage nor a take, music laid under afterwards with its biggest hit on the opening
shot, no face, a 2.3 s title louder than the body. This skill exists so that cannot happen again.
Structure, rhythm, reveal and camera work are hard rules with lints.

Skill test 1 (`../../SKILL-TEST-1.md`) showed the skill wins (12 of 12 blind judgements) but that
plans cloned the one example and bent shots to pass lints; revision 2 fixed that. **Revision 3
(2026-11-20) re-centres the skill on what the game is now.** Evan: the flying hand "is like not an
important part of our game at all anymore. i need you to have one focused on epic disaster pvp
mayhem." The default trailer is **disasters plus survivors fighting each other on a collapsing
tower**. The hand is a 30-second power-up in the game today; it appears only when a brief asks, and
its material lives in `references/legacy-hand.md`. **Rules constrain craft, not the idea**: they say
how a shot is made and checked, never which idea to pick.

Tags on every hard rule: **[measured]** = counted in 22 official trailers (report 06),
**[sourced]** = working editors, standards, platform specs, or the documented findings of attempt 1,
skill test 1 and the field tests, **[inferred]** = derived; inferred numbers are defaults, never
hard on their own; **estimate** marks a revision-3 number nobody has measured. Decisions and their
evidence: `../../RECONCILIATION.md` (Revision 3 at the end).

## Which reference to open when

| Stage | Open |
|-------|------|
| What the game has, what the Studio can direct (read first, every time) | `references/game-content.md` and `references/studio-tooling.md` |
| Brief, concept, beat sheet | `references/structure-and-templates.md` |
| How different plans can look (and what not to copy) | `references/worked-examples.md` (D, E, B) |
| Music map, target hits, sync plan, sound, mix | `references/rhythm-and-sound.md` |
| Camera placement, moves, the framing check, filming a fight | `references/camera.md` |
| Reveal or cold open, stakes, the hero, readable chaos, comedy | `references/reveals-and-moments.md` |
| Writing and linting the plan | `references/plan-schema-and-lints.md`, then `tools/lint.py` |
| Critic rounds, or self-review when no critic can run | `references/review-rubric.md` |
| Measuring a render against the 22 references | `references/measured-reference.md` |
| Assembling, packing evidence, mapping music | `tools/` (`studio-tooling.md` section 7) |
| A brief that asks for the giant hand | `references/legacy-hand.md` |

## Game facts that set every number

- Survivor about 0.94 m tall, eyes about 0.85 m. Block 6 x 2 x 2 m, twice a survivor's height.
  Attempt 1's map: 56 m tower on a 25 m castle. About 50 mode maps exist (cities, castles, Christmas
  town, Underneath the Tree...): measure the map you use.
- A survivor at h ≥ 0.08 of frame height is the smallest that reads as a person (the EWS line):
  frame height ≤ 11.75 m at that survivor. A wide of four fighters at 6 m (24 mm) puts each at 0.16.
- Free fall: 10 m in 1.4 s, 20 m in 2.0 s, 25 m (a castle wall) in 2.3 s, 56 m in 3.4 s.
- Lens numbers assume a 24 mm sensor height: frame height at the subject `Hf = distance x 24 /
  focal`. Studio tracks default to 18.66 mm: set `sensorHeight = 24` on every track.
- **The hook, in one line:** friends knock each other off a collapsing candy tower while disasters
  tear it apart. Into the sea is out.
- The giant hand (legacy): drawn size 2.4 x 2.3 x 1.8 m, only about 2.4 survivors tall. Framing it:
  `legacy-hand.md`.

### What the game can and cannot do (details and file references: `game-content.md`)

| Thing | Status (2026-11-20; unknown or unconfirmed = `needs: verify` with a fallback, lint L19) |
|-------|------------------------------------------------|
| Disasters | 16 in the catalog: tsunami, tornado, earthquake, meteor shower, planet-killer meteor, ornament shower, planet-killer ornament, fire, acid rain, anti-gravity, icey, power outage, vampire, kraken, cat attack, evil santa. How each looks in a Studio take: unconfirmed except tsunami and tornado |
| Tsunami | height 1-100 m (22), speed, offshore distance settable; **its height is zero during the warning**, then it grows while moving; no sea drawback in the code |
| Survivors hitting survivors | punch/shove (most modes), built-in PvP dagger, 21 weapons incl. giant boxing glove (a 22 m/s launch), rocket launcher (8 m blast), shotgun, chainsaw; bots punch and fire weapons |
| Into the sea | eliminates (drowning). The candy burst with "OUT!" marks an elimination |
| Modes | Survival (disasters every 20 s), King of the Hill, Block Control, CTF, Infection, Team Tickets, Murder Mystery, Ninja (lantern), Demo |
| Avatars' faces | **no drawn eyes or mouth**: a reaction reads through head direction, body and hand pose only |
| Riding a moved block | standing survivors ride it; on a lifted, **tilting** block a survivor is carried by the block's position only and **sinks into it unless he stands at its middle** (0.36 m off-centre, 0.27 m slide at the middle) |
| Several real players in one take | cannot today: bots stand in, one person performs at a time |
| Studio cues for mayhem | disasters (`hazard`, `tsunami`), block pushes and collapses, `gear` (give a weapon), `fire` (fire it at a target), reaction layers (knockback, tumble, fall, death, scream, cower, cheer...) |
| Missing in the Studio | a punch cue; mode objectives and HUD (the stage always runs Demo); real eliminations (deaths are off); aimed meteors; a tsunami cue that pushes survivors (`tsunami` sets `affectPlayers = false`); sound cues; repeatable physics; a live view |
| Giant hands | one per hand player; AI hand bots can add more; the flying hand is a 30 s pickup (legacy) |
| Live action of a person in a headset | cannot (the Studio cannot capture it) |

## Two modes

**Production** (default): a real music file and engine access through the coordinator. Every gate
applies.

**Planning-only**: use it when there is no music file, no engine access, or the request is for a
plan only. Then:
- **Music (stage 2) is deferred, not failed.** Write target hits (`frame`, with
  `measured_onset_frame: unmeasured`) and a short music brief (tempo range, where the main hit,
  stop-down and title hit fall) for choosing or cutting a track.
- **Deferred gates:** stage 2's onset question; L11's measured-onset clause; every R-lint (R01-R07);
  stages 5-11. Every engine-proof item (hazard reach, a hit landing, anything unconfirmed in
  `game-content.md`) becomes a `needs: verify` item on a **to verify at scout** list.
- **Not deferred:** stage 0, stage 1 (with its idea and alternatives steps), every plan lint except
  L11's measured clause, the framing check (L38), and the stage-4 review (critic, or the fallback in
  the self-review protocol). Name the map you assume (attempt 1's castle by default) and derive the
  `fit` lines from it. `game-content.md` and `studio-tooling.md` are used as dated; say so.
- **Output may be one file**, `plan.md`: Brief (with must-show proofs); Concept (image ideas, chosen
  concept, two rejected); Music targets; Plan table (every lint field, plus `fit`); Lint table (run
  `tools/lint.py` on the plan copied into YAML when Python runs); Text animatic; Review (critic or
  labelled self-review); To verify at scout; Before any render.
- **Before any render** (copy this list into the plan): (1) choose or cut a track and map it
  (`tools/music_map.py`); (2) move the picture to the measured hits, never the hits to the picture;
  (3) re-run the lints; (4) scout every `verify` item, then apply fallbacks or declare BLOCKED;
  (5) run the scout loop below.
- Example E in `references/worked-examples.md` is a planning-only plan.

## The pipeline (gated, with a scout loop)

Each stage writes a file into a numbered version folder (`v01/`, `v02/` ...). Never overwrite a
version. A stage starts only when the previous gate is answered "yes" with evidence (or is
listed as deferred in planning-only mode). **Nothing is rendered until the plan passes stage 4.**
Stage 5 may make scout stills only.

| # | Stage | Artifact | Gate question (must be "yes") |
|---|-------|----------|-------------------------------|
| 0 | Brief | `brief.yaml`: goal, audience, length class, aspect, platform (muted-first?), must-show ids each with a one-line proof, one sentence a viewer should repeat, map, mode rules assumed, who is performed or a bot, render and critic budget | Could a stranger write the logline and must-show list from it? |
| 1 | Concept | `concept.md`: five or more one-line "image people will remember" ideas; the chosen concept (logline, concept type, hero, opening, shape, arc in three words, title text); two rejected concepts | Is the hook something only this game has, is the shape declared, and does each rejected concept differ from the chosen one in at least two of shape, opening, payoff, ending? |
| 2 | Music map | `music.yaml` (`tools/music_map.py`): file, tempo, beats, onsets, named hits (`open`, `main`, `title`, accents), stop-downs, loudness curve | Is every hit taken from the onset list of the actual file? (planning-only: deferred; target hits written) |
| 3 | Plan | `plan.yaml` (schema below) and `lint.txt` (`tools/lint.py`) | Do all hard plan lints pass, is every soft failure fixed or waived with a reason, and does every shot have a `fit` line? |
| 4 | Animatic and plan review | text animatic (one card per shot, timed to the music) and a critic's notes (or the labelled fallback) | Read only the cards: can the critic name the hook, the hero, who hits whom, the payoff and where the title lands, and does it score Hook, Clarity and Story at 3 or more? |
| 5 | Scout | stills at first, key and last frame of every shot from the planned camera; subject area and occlusion from the engine; a dry run of every hazard and hit cue | Is every subject visible at its key frame (R01) and does every cue do what the plan needs (R02)? |
| 5b | **Re-plan** | `v0N+1/plan.yaml`, `lint.txt`, a change list | **The scout loop:** did the scout change any camera, spot, cue, actor or hazard setting? Then write the changed plan as a new version, re-run every lint, and re-run the stage-4 review if any key moved more than 6 frames, any shot fell back or any `shows` label changed. Loop 5 → 5b until a scout changes nothing that matters. |
| 6 | Capture | committed take and `take-report.json` | Did every action cue happen within 8 frames of plan, and are the hazards and hits in the take? |
| 7 | Preview | 640x360 render of the whole cut with sound (`tools/assemble.py`); evidence pack (`tools/pack.py`) | Does every shot show its subject doing its action at preview size? |
| 8 | Rough cut | full cut with temp titles; critic round | No rubric category at 2 or below, weighted mean ≥ 3.5, no hard lint failing |
| 9 | Fine cut, lock | 1080p render; critic round; plan tagged `locked` | Weighted score ≥ 4.0, all categories ≥ 3, planted-defect check passed |
| 10 | Finish | `trailer.mp4`, loudness report, profile check | Render lints R03-R05 pass |
| 11 | Human pass | Evan's or a teammate's notes | Has a person watched and listened? |

Rules for the pipeline:
- Go back one gate, not to the start. Each critic note names the stage it belongs to.
- The scout loop is normal, not a failure: in hand-reveal-2 the scout changed the hand design, the
  hero's spot and every camera. Plan for two or three scout passes.
- After lock (stage 9), only sound, titles and grade change, unless a hard lint fails.
- If a stage needs a Studio feature that does not exist, check `references/studio-tooling.md` for
  the workaround. If there is none, the plan is BLOCKED: stop and report the feature.
- **Critic budget.** If critic runs are capped, spend them on stage 7 (preview: fixes are cheap) and
  stage 9 (fine cut: the release gate), each with the planted-defect check; do stage 4 and stage 8
  as labelled self-reviews. With one run, use it at stage 7 and say the fine cut was self-reviewed.

## Concept types

Set `concept_type` in the plan. The core-verb, hold and BLOCKED rules follow it.

| | `mayhem` (default) | `disaster` | `pvp` |
|---|---|---|---|
| Core verb (rule 4), its `shows` id | the first of `hazard_hit` and `pvp_hit`; both by 60 % of the body (L40) | `hazard_hit`: the hazard visibly moves blocks or survivors, with the structure or a survivor for scale | `pvp_hit`: one survivor visibly strikes, shoots or shoves another and the victim moves or falls, both in frame |
| First hold (rule 6) | the first core-verb shot | the first `hazard_hit` shot | the first `pvp_hit` shot |
| Opening | staged (knows, effect, reaction) or cold open on a disaster | staged or cold open on the hazard | start inside a fight; any disaster in it is staged or a declared cold open |
| BLOCKED if | the hazard cannot be staged to reach the survivors, or no hit lands in the scout | the hazard cannot be staged to reach or be seen | no hit lands on a victim in the scout |
| Examples | D (15 s), E (30 s) | B (15 s) | - |

Legacy types `hand` and `two_role` (core verb `pull`) are in `legacy-hand.md`. Never add the hand to
a brief that does not ask for it; if a mayhem brief asks for it, keep `mayhem` and give the hand its
own `verify`, fallback and reveal. Each disaster gets its own reveal (rule 6).

**Must-show items come from the brief, not from a fixed list.** Copy one id per thing the brief
asks for and write what in frame proves it. "Multiplayer" is proven by three or more survivors
readable and acting independently in one frame, or players answering each other, never by
implication. PvP ids: `pvp_hit`, `knock_off`, `elimination`, `weapon`; mayhem ids:
`several_survivors`, `hazard_hit`, `into_sea`. Suggested ids and proofs: `structure-and-templates.md`
section 1.

## Hard rules

Each is checked by the lint named in brackets (formulas in `references/plan-schema-and-lints.md`;
run them with `tools/lint.py`).

1. **Plan before pixels.** No render of any shot until the plan passes every hard plan lint and
   the stage-4 review. [sourced: Lieu's garbage cut, previs practice; attempt 1 spent about 3 of 5
   renders finding lines of sight] (pipeline gate)
2. **Structure, not a list.** Sections run `hook`, `teach`, `escalate` (one or more), `payoff`, then
   `title`; an optional `breath` among the escalate beats or after the payoff, an optional
   `aftermath` after the payoff. Every `escalate` names what it raises: stakes, scale, threat,
   count, speed or novelty. Class `moment`: teach, escalate and title are optional. [sourced: Lieu,
   Cauller "teach, test, escalate"] (L03)
3. **Declare a shape and meet it.** *Montage:* median body shot 1.0-2.2 s, shortest ≤ 1.0 s,
   longest ≥ 2.0 s (15 s) / 3.0 s (30 s) / 3.3 s (60 s), and some 5 s window with 4 or more cuts.
   *Single take:* one picture shot covers ≥ 70 % of the body, at most 2 cuts in the body. *Moment
   montage:* median 0.8-2.5 s, longest ≥ 1.5 s, no barrage (estimate). Never 5-6 even shots of
   2-3 s. [measured: 16 of 18 mid and long montages; short refs are single takes] (L06)
4. **Gameplay first, core verb early.** Gameplay on screen by 2.0 s; frame 0 is not black, a logo or
   a card. The concept's core verb is readable by 5 s (moment, 15 s), 8 s (30 s), 12 s (60 s).
   [measured: 13 of 21 by 2 s; party/physics trailers first gameplay 0.3 s, "what is this" 3.7 s]
   (L04, L05)
5. **Title late, card long enough.** Closing title starts at 11.0-12.0 s (15 s), 24-26 s (30 s),
   53-55 s (60 s); title plus end card 3.0-4.0 s / 4-6 s / 5-7 s. Default 15 s: title at 11.5 s,
   card 3.5 s. A `moment` needs no title; if it has one, it comes after the payoff. [measured: title
   at 80-95 % in 18 of 22; card 4-8 s in 15 of 22; 15 s window extrapolated] (L07)
6. **Stage each threat, or open cold on purpose.** For each disaster (and the hand, if any): if its
   first full view comes after frame 0, at least 0.7 s of anticipation about it comes first (an
   effect, a partial view, a reaction or an audience-knows shot). Opening on it at frame 0 is a
   named choice (`opening: cold_open`): hold that view uncut ≥ 1.0 s, and let escalation come from
   consequences (two or more escalate beats showing what it does, each raising something
   different). Every effect (a fall, a collapse, a knock-off) has its cause in frame or in the shot
   before. The first core-verb shot is uncut ≥ 1.5 s with its key 12 or more frames from each end
   and its scale reference in frame. [sourced: Hitchcock, Jaws, Lieu; skill test 1's best hook was a
   wave at frame 0; thresholds inferred] (L08, L09)
7. **Land the payoff and the title on hits.** Hits come from onset detection on the music file,
   never from a guessed grid (planning-only: target hits, re-timed before any render). The payoff's
   key is within 2 frames of the main hit; the closing title within 2 frames of the title hit;
   every planned sync point appears as an onset within 2 frames, measured **lane by lane** (rule
   15). Do not snap every cut to the beat (25.0 % on beats vs 24.9 % chance); lean to hits.
   [sourced: Lieu, Violet Recording; measured lean to onsets, p = 0.004 in fast passages] (L11, R03)
8. **Every key moment has a sound.** A sound event within 3 frames after each key frame and never
   more than 1 frame before it. Every hit, launch and splash gets its own sound. A lead-in (a roar
   0.5-1.5 s before a disaster) is a separate sound and is allowed. A speed-ramped shot carries no
   real-speed take sound. [sourced: Lieu on SFX as percussion; ITU-R BT.1359; attempt 1 F4] (L12, L13)
9. **A close shot and a reaction that reads.** At least one shot of MCU or tighter, and one MS or
   tighter shot of the hero facing within 60° of the camera whose `reaction` names what carries it.
   The avatars have **no drawn eyes or mouth**: the reaction reads through head direction, body and
   hand pose, so give it a Studio reaction layer (`scream`, `cower`, `cheer`, `knockback`), an
   emote or a performed pose, and frame the head and both hands. A hero at a fifth of frame height
   in a wide has no readable reaction. [measured: most references have a close shot; sourced:
   Kuleshov; hand-reveal-2 critics saw "a featureless head"; 60° inferred] (L14)
10. **Real camera work.** Every shot declares one move from the library in `references/camera.md`
    and its purpose; moving cameras ease (or are damped); no unmotivated drift. No jump cut: two
    consecutive shots of the same subject must not share size, angle class and yaw within 30°. No
    raw head-locked VR view. [sourced: Lieu, Nesky, 30-degree rule, Gartner] (L15, L16, L17)
11. **Derive every frame.** Every shot's `fit` line computes `Hf = distance x 24 / focal` at its
    subject, the subject's fraction (matching its size), and shows that the action fits: the
    impact, the victim's knockback path, the face, each survivor claimed as readable. Derived for
    this shot, never copied, and nothing beyond what the game can do. [sourced: skill test 1, where
    critics found copied frames that could not contain their action] (L38, L19)
12. **Prove it in the engine before rendering.** At each key frame the subject's screen area is at
    least `min_area_pct` and occlusion at most `max_occlusion_pct` (pass table in `camera.md`; never
    above 20 % for a person). Every hit cue lands and moves its victim; every hazard reaches the
    planned survivors. If a mayhem or PvP concept cannot land a hit, or a disaster concept cannot
    stage its hazard, it is BLOCKED: stop and report. Planning-only: these go on the to-verify list.
    [sourced: attempt 1 F5-F7, F10] (L19, R01, R02)
13. **Deliver a correct mix.** Integrated -20 to -12 LUFS (target -15 ±1); true peak ≤ -1.0 dBTP;
    the body's last quarter at least as loud as its first; the title section at least 3 LU quieter
    than the body; no more than 3 flashes in any 1 s; no black or silence over 0.25 s at frame 0.
    [measured: 13 of 22 in the window, 16 of 20 rising, 16 of 21 title ≥ 6 LU down; sourced:
    true-peak spec, ITU-R BT.1702] (R04)
14. **Rules serve the idea; never game them.** Never relabel a shot, drop a true `shows` label,
    shorten a beat or change a cause just to pass a lint. If a rule fights the concept, keep the
    concept and write a waiver; if a hard rule fights it, report that to the human. Never reuse an
    example's tagline, caption, hero or cast ids. [sourced: skill test 1] (L39; critic C2, C10)
15. **A person listens; meters only measure.** Nothing is finished until a human has watched and
    listened (stage 11). Check sync **one sound lane at a time** (`tools/assemble.py` prints a mix
    column and a lane column), and list every voice line as "needs ears": an onset detector misreads
    soft first syllables (hand-reveal-2 read a line 17 frames late that was in sync). [sourced:
    attempt 1 F18; hand-reveal-2] (R03, R07)
16. **Readable mayhem.** Chaos sells only when a viewer can say who did what to whom. Every hit,
    knock-off and elimination names its attacker and victim, both readable in the frame or across
    one clean cut (L41). One hero is followed through the chaos, seen in the opening, the build and
    the payoff (L42). Cause, then effect, every time (L08). Disasters escalate one layer per beat
    (L43, soft); the viewer always knows where on the tower the action is (L44, soft); crowd shots
    keep three or more survivors at h ≥ 0.08 (L45, soft); a knock-off is held 0.6 s or picked up by
    the next shot (L46, soft). [sourced: report 01 rules 20 and 40, Salters' "soupiness", Lieu on
    party trailers; every number an estimate] (L40-L46)

## Defaults by length

Use these unless the brief says otherwise. Soft lints warn when they are broken; waive with a reason.

| | moment (5-10 s) | 15 s | 30 s | 60 s | Tag |
|---|---|---|---|---|---|
| Body shots (montage) | 1-6 | 6-9 | 12-18 | 22-32 | measured mid, scaled; moment estimate |
| Median body shot | 0.8-2.5 s | 1.2-1.6 s | 1.3-1.7 s | 1.4-1.7 s | measured; moment estimate |
| Cuts per minute of body | any | 30-45 | 25-40 | 25-38 | measured mid IQR, 15 s scaled |
| Sync points (incl. main and title) | 2-4 | 3-5 | 5-8 | 8-14 | inferred |
| Stop-down (≥ 0.3 s, ≥ 12 LU dip) | optional | optional, 0.3-0.5 s before the main hit | 1 | 1-2 | measured 14 of 22 |
| Main hit position | 50 % of the body or later | last 40 % of body | same | same | inferred |
| Loudness ramp, last vs first quarter | ≥ 0 LU | +2 to +6 LU | same | same | measured |
| Title vs body | no title | -4 to -10 LU | -6 to -12 LU | -6 to -12 LU | measured (weak for 15 s) |
| Wide (EWS, WS, FS) share of body shots | ≥ 40 % | ≥ 40 % | ≥ 40 % | ≥ 40 % | measured; party/physics 60 % |
| Close (CU, ECU, INSERT) share | ≤ 25 % | ≤ 25 % | ≤ 15 % | ≤ 15 % | measured |
| Designed moves (montage) | any | 10-30 % of shots | same | same | measured median 11 %, party 14 % |
| Disasters on screen | 1 | 1-2 | 2-3 | 3-5 | estimate |
| Distinct PvP hits | 1-2 | 2-3 | 3-5 | 5-8 | estimate |
| In-body captions | 0 | 1 plain rule caption for store and muted social, else 0-1; ≤ 6 words | 1 for store and muted, else 0-2 | 0-4 | measured 11 of 22 use some |
| End-card text | - | tagline ≤ 8 words, call to action ≤ 6 | same | same | inferred |

Also by default: straight cuts (no crossfades without a reason), master short-term max ≤ -10 LUFS,
stereo 48 kHz AAC, picture at 30 or 60 fps, brighter than the headset look, no motion blur in the
Studio's renders until it is fixed (`studio-tooling.md`).

## Worked examples, different shapes

All pass every plan lint (`tools/lint.py`, 0 hard failures; check tables beside each). They show the
method, not a spine to fill in. A new plan differs from the examples of its type in at least two of
shape, opening, payoff and ending, and re-derives every camera number (rule 11).

| | D "Push Comes to Shove" | E "Everything at Once" | B "Already Here" |
|---|---|---|---|
| Where | `worked-examples.md` §1 | `worked-examples.md` §2 | `worked-examples.md` §3 |
| Length, type, mode | 15 s, `mayhem`, production | 30 s, `mayhem`, planning-only | 15 s, `disaster`, production |
| Shape | montage, 8 body shots | single take (76 % of body) plus two cuts | single take (82 %) plus a face |
| Opening | staged: friends brawl while the wave rises behind them (audience knows) | cold open: meteors already raining at frame 0 | cold open: the wave already standing |
| Core verb at | 2.3 s (a strike lands) | 1.5 s (a meteor smashes a block) | 4.7 s (the wave rips out blocks) |
| Payoff | the crest sweeps the bully into the sea | the tornado takes the rocket shooter | the section tips into the sea |
| Ending | pull-out from the last survivor, title over picture | her friend shoves the winner off; smash cut to the card | the hero surfacing, hard cut to a card |
| Title, card | 11.5 s, 3.5 s | 25.0 s, 5.0 s | 11.33 s, 3.67 s |

Legacy hand examples A (15 s hand reveal) and C (30 s two-role) are in `legacy-hand.md`.

## Shot-list schema (compact)

Full field list, allowed values and the lints: `references/plan-schema-and-lints.md`.

```yaml
id: my-cut-15
mode: production        # production | planning_only
concept_type: mayhem    # mayhem | disaster | pvp  (legacy: hand | two_role)
opening: staged         # staged | cold_open (+ cold_open_threat)
threats: ["hazard:tsunami"]
fps: 30
length_frames: 450
class: 15               # moment | 15 | 30 | 60 (sets the numeric limits)
shape: montage          # montage | single_take
hero: <your id>         # never an example's id
must_show: [...]        # from the brief, each with a proof in brief.yaml
music: {file: ..., bpm: ..., hits: [{name: main, role: main, frame: 264, measured_onset_frame: unmeasured}]}
titles: [{text: "DEADLY JENGA", role: title, frame_in: 345, frame_out: 450, closing: true}]
shots:
  - {id: S02, section: teach, cut_in: 40, cut_out: 100,   # trailer frames; cut_out of n = cut_in of n+1
     subject: {kind: actor, id: <hero>}, also_in_frame: [<rival>, "castle:wall_walk"],
     shows: [pvp_hit], attacker: <rival>, victim: <hero>,  # who did what to whom (L41)
     tower_level: base, readable_survivors: 2,
     learns: "...",                     # <= 12 words, one idea, no "and"
     raises: null, reveal: none, size: WS, angle: low, side: {face: 3q_front, yaw_deg: -25},
     camera: {move: handheld, purpose: "...", ease: none, distance_m: ..., height_m: ..., focal_mm: ...},
     fit: "Hf = d x 24 / f at the subject; fractions; the knockback path stays in frame",
     key_frame: 30, sound: [{at: 30, kind: music_hit, name: punch, sync: punch}],
     needs: [{feature: melee_hit_through_fire_cue, status: verify}], fallback: "..."}
```

## Self-review protocol

You cannot watch motion or hear sound. Judge from evidence, in this split:

| Question | Judge it from | Never from |
|----------|---------------|------------|
| Is the subject in frame, how big, how hidden? | engine projection, raycast or ID buffer | your look at a still |
| Is the subject readable (pose, who it is, what it does)? | full-size still at the key frame | numbers |
| Who hit whom? | stills at key -2, key, key +2 with attacker and victim boxed | a contact sheet alone |
| Is the hero's colour what the plan says? | a full-size still | `StudioMark.color` (a palette index: index 6 rendered yellow) |
| What is the camera doing? | the camera track's own keys (m/s, deg/s) | stills |
| Do cuts, hits and impacts line up? | the sync table, lane by lane | the full-mix column alone |
| Is the mix right? | EBU R128 numbers, per-lane levels, the loudness plot | anything else |
| Does it hold together? | the critic, citing the above | any single number |

1. **Build the evidence pack** after every preview or later render (`tools/pack.py`;
   `references/review-rubric.md` lists the ten items). Never judge a cut from a video file alone. At
   stage 4 the pack is the text animatic and the lint output.
2. **Answer the pack's questions in writing yourself first**, citing shot and frame. No "looks good".
3. **Run an independent strict critic.** A fresh agent (a different model if you can) gets only the
   brief, concept, plan, lint output, evidence pack, and the previous round's notes (not scores). It
   scores the rubric with evidence and ends with the top three problems, each naming a stage. A hard
   lint that is computed and fails caps its category at 2; a field the plan's format lacks is
   "cannot check", never a failure.
4. **If you cannot start a critic**, or the critic budget is spent (pipeline rules), do the fallback
   in `references/review-rubric.md` section 3a: a written self-critique against the rubric, a defect
   sweep, at least three problems found, headed **SELF-REVIEW (weaker than an independent critic)**,
   and a "critic owed" block for whoever orchestrates. The gate is then "passed on self-review"; in
   production an independent critic must still review before stage 8.
5. **Planted-defect check.** Required in every critic round from stage 7; recommended at stage 4.
   Give the critic two packs in random order: the real one and a copy with three injected defects
   (`tools/pack.py --plant`). If it finds fewer than 2 of 3, or scores the defective copy within 0.25
   of the real one, discard the review and rerun once; if it fails again, the round goes to the
   human. (hand-reveal-2: 3 of 3 found in both rounds.)
6. **Read the critic's errors before its number.** Strict critics score clean work low (skill test
   1: 2.8-3.0 against editor-style 4.5; hand-reveal-2: 2.68 for a clip rated about 6/10 against the
   pilot). Fix the errors; compare scores only between rounds of the same critic set-up.
7. **Fix the top three notes first**, from the end of the timeline backwards, and re-run the lints.
8. **Stop** when any holds: *accept* (no hard lint failing, every category ≥ 3, weighted ≥ 4.0,
   planted check passed); *plateau* (less than 0.2 gain over two rounds: ship the best, say what held
   it back); *round cap* (3 rough-cut and 2 fine-cut rounds, or the critic budget); *blocked* (a fix
   needs a Studio feature with no workaround: report it).
9. **Hand it to a human** (always). Give them: the mp4; the beat list (time, section, `learns`); the
   sync table with both columns; the loudness numbers; the critic's last top three; and questions
   only ears and eyes can answer: Does the music fit the candy-peril tone? Does the main hit land on
   the payoff? Can you tell who hit whom in every fight? Do you follow the hero? Is every voice line
   in sync? Is anything harsh, muddy or too fast to follow? Record their notes; that is R07.

## Common ways an AI gets this wrong, and the fix

| Mistake | Fix |
|---------|-----|
| Events in a row with no sections, no rises, no turn (attempt 1) | Beat sheet first; every escalate beat names what it raises (rule 2) |
| Six shots of 2.0-3.4 s: too slow for a montage, too cut for a take (attempt 1) | Declare the shape; for a montage, a barrage and one short shot ≤ 1.0 s (rule 3) |
| Music laid under the finished picture; the biggest hit fell on the establishing shot (attempt 1) | Map the music first; payoff on the main hit; the title on its own hit (rule 7) |
| Chaos as noise: five things happen and nobody can say who did what | One focal hit per shot; attacker and victim both in frame or across one clean cut (rule 16, L41) |
| No hero: the camera hops between strangers | Name a hero by colour and stay with her in the hook, the build and the payoff (L42) |
| The effect without its cause: a survivor flies off and we never saw the glove | Cause in frame or in the shot before (rule 6, L08) |
| Every disaster at once from frame 0, nothing left to escalate | One new disaster per beat, each with its own anticipation or a declared cold open (L43) |
| Lost geography: the viewer cannot tell top from base | One early wide with the structure and the hero; `tower_level` on every shot (L44) |
| A crowd wide where nobody reads | Survivors at h ≥ 0.08 (frame ≤ 11.75 m at them), three or more (L45) |
| A knock-off cut away before the victim lands | Hold 0.6 s, or pick the victim up in the next shot (L46) |
| A "face" shot of a faceless avatar (hand-reveal-2) | Head direction, body and hands carry it: a reaction layer, an emote or a performed pose (rule 9) |
| Planned a punch on cue | The Studio has no punch cue: a person performs it, a live bot happens to, or `gear` + `fire`, or reaction layers fake it |
| Planned the `tsunami` cue to sweep people off | It never pushes survivors (`affectPlayers = false`); use the `hazard` cue and run variations |
| Promised an elimination burst from a Studio take | Deaths are off on the Studio stage: plan the splash or a `death` layer, and `verify` |
| Planned mode HUD, flags or scores | The stage runs Demo: capture them in a real match or leave them out |
| A survivor riding a tilting block sank into it (hand-reveal-2) | Stand him at the block's middle; check his feet in the scout |
| The hero rendered white at 1080p (pilot, hand-reveal-2) | The Studio's URP motion blur does it: render without it and check a colour still |
| The scout changed everything but the plan was not re-linted | Stage 5b: a new plan version, re-run the lints, re-review when keys move (pipeline) |
| A voice line measured 17 frames late that was in sync | Check each sound lane alone; voices need ears (rule 15) |
| Waivers for a 9 s clip's title rules (hand-reveal-2) | Use class `moment` |
| Re-recording for each fix; takes differ every time | Commit one take; plan against its take report |
| Judged from contact sheets and meters, by the maker alone | Evidence pack, independent critic, planted defects, human pass |
| A grey see-through survivor in two shots | Technical check in the filmstrip; fix or cut the shot (critic C9) |
| Four plans, one spine: same collapse, same hero, same tagline (skill test 1) | Five image ideas first; two rejected concepts that really differ; never reuse an example's text or ids |
| Copied camera numbers that could not show the new action (skill test 1) | A `fit` line per shot, derived for what that shot must show (rule 11) |
| The giant hand forced into a mayhem brief | Only if the brief asks; `legacy-hand.md` |
| Multiplayer only implied (skill test 1) | Three or more survivors readable and acting in one frame |
| No caption, so muted viewers missed the rule (skill test 1) | One plain rule caption by default in store and muted social cuts |
| Labels changed, a beat cut short, a cause swapped "for the lint" | Waive in writing instead (rule 14) |
| Over-correcting: every cut snapped to a beat | Only sync points and barrage cuts need hits; keep beat cuts under 80 % |
| Over-correcting: forcing wide-medium-close on every cut | Same-size cuts are normal in gameplay; change the place or action instead |
| Over-correcting: a slow move on every shot | Static is a move; designed moves on 10-30 % of shots |
