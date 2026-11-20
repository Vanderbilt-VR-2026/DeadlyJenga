# Skill test 1: does deadlyjenga-trailer-craft make better trailer plans?

Run 2026-11-20. Plan level only: nothing was rendered, no Unity, no git. All agents were fresh
`general-purpose` agents on Opus. Work folder (outside any repo):
`C:\Users\evanp\Documents\Codex\Deliverables\TrailerCraft-Reference\skill-test\` (plans, key,
feedback), with the blinded grading packet in `..\grading-packet\` and the critic packet in
`..\critic-packet\`.

## Result in one paragraph

**Experiment 1: PASS.** The skill plans won **12 of 12** blind pairwise judgements (threshold: 9),
and they were better on most of the objective checks tied to the measured reference data.
**Experiment 2: PASS.** The critic found **3 of 3** planted defects in both defective copies, made
**0** false alarms of the planted kinds on the clean copies, and scored the defective copies 0.42
and 0.60 lower. Several caveats weaken this. Brief a is the skill's own worked example. Graders
seem to reward the numeric precision the skill supplies, even where the numbers are wrong. All
four skill plans share one template. And the control plans did better on hook and clarity (see
"Where control beat skill").

---

## 1. Method

1. I wrote four briefs (below) and one game description (the text from the task, given verbatim
   to every planner and grader).
2. **Planners.** Each brief had two planners with identical prompts except for one line.
   - Control: "Plan this trailer as well as you can."
   - Skill: "Before planning, read and follow the skill at <SKILL.md> and the references it points
     to; follow its pipeline as far as planning (through the plan and text animatic; you cannot
     render)."
   - Both had to write the same plain deliverable: logline and hook; music/sound plan with main
     and title hits; a shot table (time in, time out, what we see, size, camera position and move,
     purpose, sound); title text and timing.
   - Plain words only: no schema, lint, rule or file references. Neither side was told an
     experiment was running. Output file names were neutral (`<brief>-p1.md` for control,
     `<brief>-p2.md` for skill).
3. **Interruption and restart.** The first run hit an account usage limit. All four control
   planners had finished. All four skill planners were cut off: one had written a deliverable,
   three had written only partial notes. I moved every partial skill output to
   `skill-test\aborted-run1\`, unused, so the restarted planners could not find them. I then
   restarted all four skill planners with their identical original prompts. All eight plans used
   in the test come from complete, uninterrupted runs.
4. **Leak check.** I read all eight plans and searched for lint, gate, rule, skill, playbook,
   schema, YAML, L/R rule ids, file names and "waive". There were no leaks, so no rewrites were
   needed and no content was edited.
   - Some style tells remain in the skill plans: LUFS and dBTP numbers, a "24 mm-tall sensor"
     note (d), the hero named "Mint", and the example tagline "Don't be the one who falls."
   - None of these names the skill, and the graders never saw the skill, so they could not trace
     the tells back to it.
5. **Blinding.** Coin flips (System.Random, seed 1795135757) assigned X/Y per brief. Plans were
   copied to `grading-packet\<brief>-X.md / -Y.md`. The key is in `skill-test\key\KEY.txt` and was
   never given to graders or critics.
6. **Grading.** Three grader agents, each told only: you are a senior game trailer editor reviewing
   two competing plans for the same brief.
   - Each got the game description and briefs, but not the skill and not what differs.
   - Each picked a winner per pair and scored each plan 1-5 on 9 criteria, with a two-sentence
     reason naming shots.
   - Orders differed by grader. G1 read a, b, c, d with X first. G2 read d, c, b, a with Y first.
     G3 read b, d, a, c with first-shown mixed (X first for b and a, Y first for d and c).
   - Every brief was seen Y-first by at least one grader. That gives 12 judgements.
7. **Objective checks.** I computed these from each shot table with the same script for both
   conditions (`scratchpad\obj.py`, logic restated below). The body is everything before the
   closing title.
8. **Critic experiment.** I took the two highest-scored skill plans (d 4.59, and c 4.48, tied with
   b; c was chosen so one 15 s and one 30 s plan were tested). For each I made a clean copy and a
   copy with three planted defects.
   - Four critics each got `review-rubric.md`, `plan-schema-and-lints.md`, the briefs and one copy
     under a neutral number. They were not told defects might exist.
   - Key: `skill-test\key\CRITIC-KEY.txt`.
9. **Usability.** After each skill planner's deliverable was saved, I sent it three follow-up
   questions. The answers are collected verbatim in the appendix.

## 2. The briefs

- **a. "The Hand" (15 s social).**
  - Goal: stop the scroll by revealing the giant hand.
  - Audience: Quest owners and VR-curious feed scrollers.
  - Platform: TikTok, Reels and Shorts, muted-first but designed with sound; 16:9 master.
  - Must show: the hand pulling a block, a survivor affected by it, the tower over the ocean, the
    title at the end.
- **b. Store-page gameplay trailer (30 s).**
  - Goal: explain what you do and drive wishlists or purchases.
  - Audience: Quest store browsers new to the game, sound on.
  - Must show: survivors climbing, the hand pulling blocks, at least one disaster, someone falling
    into the sea, that it is multiplayer, the title at the end.
- **c. Disaster cut (15 s social).**
  - Goal: one spectacular disaster moment that people share; the disaster is the star.
  - Audience: short-form viewers who like chaotic physics and party games.
  - Must show: one disaster (tsunami or tornado) hitting the tower, survivors reacting, the
    consequence going into the sea, the title at the end.
- **d. "Be the hand" (30 s).**
  - Goal: sell the asymmetric two-role idea, with both roles fun.
  - Audience: VR players with friends; YouTube pre-roll and socials.
  - Must show: the hand player's point of view or experience, the survivors' point of view or
    experience, a pull that affects a survivor, multiplayer with friends, the title at the end.

Full text: `skill-test\briefs.md`.

## 3. The X/Y key

| Brief | Coin | X | Y |
|---|---|---|---|
| a | heads | skill (a-p2) | control (a-p1) |
| b | tails | control (b-p1) | skill (b-p2) |
| c | tails | control (c-p1) | skill (c-p2) |
| d | tails | control (d-p1) | skill (d-p2) |

## 4. Pairwise results

**Skill wins: 12 of 12.**

| Grader (order, first shown) | a | b | c | d | Skill wins |
|---|---|---|---|---|---|
| G1 (a, b, c, d; X first) | skill | skill | skill | skill | 4/4 |
| G2 (d, c, b, a; Y first) | skill | skill | skill | skill | 4/4 |
| G3 (b, d, a, c; mixed) | skill | skill | skill | skill | 4/4 |
| **Per brief** | 3/3 | 3/3 | 3/3 | 3/3 | **12/12** |

Presentation order made no difference: the skill plan won whether it was shown first or second.

### Per-criterion mean scores (1-5; 12 scores per condition per criterion)

| Criterion | Skill | Control | Difference |
|---|---|---|---|
| Hook in the first seconds | 4.00 | **4.25** | -0.25 |
| Clarity of what the game is | 4.17 | **4.33** | -0.17 |
| Structure and escalation | **4.92** | 3.33 | +1.58 |
| Rhythm and shot-length design | **4.33** | 3.17 | +1.17 |
| Reveal / anticipation and payoff | **4.92** | 3.33 | +1.58 |
| Camera work and scale | **5.00** | 3.08 | +1.92 |
| Sound and music design | **5.00** | 3.92 | +1.08 |
| Title / ending | **4.08** | 3.67 | +0.42 |
| Could be shot as written | **4.00** | 3.08 | +0.92 |
| **All criteria** | **4.49** | 3.57 | +0.92 |

### Per-plan mean of the 9 criteria (G1 / G2 / G3)

| Plan | G1 | G2 | G3 | Mean |
|---|---|---|---|---|
| a skill | 4.33 | 4.33 | 4.56 | 4.41 |
| a control | 3.56 | 3.78 | 3.22 | 3.52 |
| b skill | 4.44 | 4.44 | 4.56 | 4.48 |
| b control | 3.78 | 3.89 | 3.56 | 3.74 |
| c skill | 4.44 | 4.44 | 4.56 | 4.48 |
| c control | 4.00 | 3.67 | 3.33 | 3.67 |
| d skill | 4.56 | 4.56 | 4.67 | 4.59 |
| d control | 3.44 | 3.33 | 3.33 | 3.37 |

What the graders' reasons have in common:
- Skill wins were credited to the staged reveal, the barrage of short cuts into a payoff on the
  main hit, and "exact lenses and positions".
- Control plans were marked down for:
  - (b, d) cramming tsunami, tornado and fire into 1-1.5 s shots;
  - (c) spending the main hit at 5.0 s and then deflating;
  - (a) a 3.0-5.0 s establishing arc that stalls;
  - vaguely staged moves ("whip pan following the survivor", "orbiting close").

Grader files: `grading-packet\grades-g1.md`, `-g2.md`, `-g3.md`.

## 5. Objective checks

Columns are control / skill for each brief. The skill's own measured or extrapolated target is in
brackets. "Pull + effect" means the first moment a hand pull and a survivor visibly affected by it
are shown, in the same shot or the next.

| Check | a ctrl | a skill | b ctrl | b skill | c ctrl | c skill | d ctrl | d skill |
|---|---|---|---|---|---|---|---|---|
| Body shots (+1 title shot) | 8 | 8 | 16 | 16 | 10 | 9 | 18 | 16 |
| Median body shot (s) [1.0-2.2] | 1.50 | 1.25 | 1.65 | 1.50 | 1.50 | 1.40 | 1.50 | 1.50 |
| Shortest (s) [≤ 1.0] | 1.00 | 0.50 | 0.50 | 0.50 | 0.50 | 0.50 | 1.00 | 0.60 |
| Longest (s) [≥ 2.0 at 15 s, ≥ 3.0 at 30 s] | 2.00 | 2.40 | **2.50 ✗** | 3.00 | **1.50 ✗** | 2.20 | **2.00 ✗** | 3.70 |
| Shot-length CV (std/mean) [≥ 0.35] | **0.17 ✗** | 0.48 | **0.30 ✗** | 0.50 | **0.26 ✗** | 0.47 | **0.24 ✗** | 0.48 |
| Most cuts in any 5 s [≥ 4] | 4 | 5 | 4 | 5 | 4 | 6 | 4 | 6 |
| Cuts per minute of body | 35 | 38 | 35 | 36 | 42 | 44 | 38 | 36 |
| First gameplay (s) [≤ 2.0] | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| Pull + effect on a survivor (s) [≤ 5 at 15 s, ≤ 8 at 30 s] | **6.5 ✗** (both in one frame 8.0) | 4.5 (hero beside the pull), 6.0 (Rook falls) | **9.0 ✗** (never both in one frame) | 4.5 (hero beside), 8.8 (survivors leap clear) | no hand (not required) | 4.9 (hand added; effect at 8.5) | **5.2-6.0** | 7.0 |
| Survivor face or reaction shot | yes 0.0 | yes 1.5 | yes 0.0 | yes 1.8 | yes 1.5 | yes 1.6 | yes 2.0 | yes 1.6 |
| Threat anticipated before its full view | yes (shadow 0.8 s) | yes (partial + reaction, 3.5 s) | hand yes / tsunami no | hand yes / tsunami yes (sea drains, reaction) | **no** (wave at frame 0) | yes (drain + reaction, 3.1 s) | **no** (hand POV at frame 0) | yes (shadow + reaction, 3.0 s) |
| Title start, fraction of runtime [measured 80-95 %] | 80 % | **73 %** | 87 % | 83 % | 87 % | **73 %** | 88 % | 83 % |
| Title plus card length (s) [3.5-5.0 at 15 s, 4-6 at 30 s] | **3.0 ✗** | 4.0 | 4.0 | 5.0 | **2.0 ✗** | 4.0 | **3.5 ✗** | 5.0 |
| Main hit named | yes 8.0 | yes 8.0 | yes 19.5 | yes 21.4 | yes 5.0 | yes 8.5 | yes 12.5 | yes 20.0 |
| Payoff placed on the main hit | yes | yes | partly (wave impact on the hit; biggest image, the collapse, is on the title hit) | yes | yes | yes | partly (pull on the hit; collapse at 23.5 on no hit) | yes |
| Main hit, % of body [soft: ≥ 60 %] | 67 % | 73 % | 75 % | 86 % | **38 %** | 77 % | **47 %** | 80 % |
| Every shot has a camera position and move | yes | yes | yes (loose) | yes | yes (loose) | yes | yes (loose) | yes |
| Shots with a numeric distance or lens | 1 of 9 | 9 of 9 | 0 of 17 | 17 of 17 | 1 of 11 | 10 of 10 | 0 of 19 | 15 of 17 (POV shots have none) |
| Single subject per shot (my reading) | mostly; 3.0-5.0 tower + hand + survivors | yes | mostly; 11.0 group + hand | mostly | yes | mostly; S4 hand + backs + wave (the critic also flagged it) | yes | mostly |

How to read the table:
- **Skill better:** shot-length spread, longest-shot floor, card length, anticipation, main-hit
  position, camera numbers, and the verb-by-deadline rule in a and b.
- **Control equal or better:**
  - Title start: control's 80-88 % sits inside the measured 80-95 % band. The skill's 15 s rule
    (10.0-11.5 s, 67-77 %) is an extrapolation that sits below it.
  - Pull + effect in d: control 5.2-6.0 s, skill 7.0 s.
  - Shot median: equal.
- **On the checks tied to the measured data, the skill plans are better** (spread, longest shot,
  card length, main-hit placement). The title-start fraction is the exception, and the cause is
  the skill's own 15 s rule.

## 6. Critic results (Experiment 2)

| Plan | Copy | Planted defect 1 | Planted defect 2 | Planted defect 3 | Found | Weighted score |
|---|---|---|---|---|---|---|
| d skill | clean (plan-17) | - | - | - | - | 2.95 (uncapped 3.20) |
| d skill | defective (plan-42) | hero reaction moved after the full hand reveal: **found** ("the reaction comes after the reveal", C4; "time loops", #8) | shot 18.7-19.5 duplicated from 18.0-18.7: **found** (L16, #2, top problem 1) | collapse moved to 21.0 s, off the 20.0 s main hit: **found** (L11, #1, top problem 2) | **3/3** | 2.53 |
| c skill | clean (plan-58) | - | - | - | - | 2.78 |
| c skill | defective (plan-23) | hero shot changed to "seen from behind, partly hidden by a block": **found** (C4, #6) | shot 7 duplicated from shot 6: **found** (L16, #2) | game title moved to 0.0-1.0 s: **found** (L07, L11, L18, top problem 1) | **3/3** | 2.18 |

- **Score gap, clean minus defective:** d 0.42, c 0.60. Both are above the skill's own 0.25 minimum.
- **False alarms on clean copies:** 0 of the planted kinds. Neither clean review claimed a
  duplicated shot, a misplaced title, a payoff off its hit, a hidden hero, or a reaction after the
  reveal.
- **Inconsistent "hard fail" calls:**
  - The d-clean critic marked three hard lints as failed: L02 (no one-idea lines), L11 (no
    measured onsets) and L17 (no smoothing values).
  - All three come from fields a plain-language plan cannot contain. They are true by the letter
    of the lints, but they are format artifacts, not defects.
  - The c-clean critic marked the same kind of gaps "cannot check".
  - I do not count these as false alarms. A second rater could, which would put d-clean at 3 and
    fail the "≤ 1" rule.
- **Threshold:** ≥ 2 of 3 found in both defective copies, and ≤ 1 false alarm per clean copy.
  **PASS** (3/3, 3/3, 0, 0).

Two side findings matter more than the pass:
1. **The critics scored the clean skill plans below the skill's own plan bar** (2.95 and 2.78; the
   rough-cut bar is a weighted mean of 3.5). The blind editors gave the same plans about 4.5 of 5.
   - The critics found real errors the editors missed:
     - (c) the payoff frame (80 m out, 135 mm, top 14 m of the tower) cannot contain a wave that
       "slams into the middle of the tower";
     - (c) the 0.4 m-high 24 mm hook frame cannot contain the climbers it describes;
     - (d) a 1.7 m / 35 mm full shot cannot hold a running jump, a 6 m block and the hand;
     - (d) a shot that shows two giant hands, though the game has only one.
   - Several of these come from camera numbers copied from the worked example without re-checking.
2. The lint-heavy rubric makes much of the score depend on fields the deliverable format did not
   have. The critic is good at spotting planted defects, but its absolute scores are not comparable
   to a human editor's.

Files: `critic-packet\review-17.md`, `review-42.md`, `review-58.md`, `review-23.md`.

## 7. Planner feedback (Experiment 3)

All four answers are reproduced verbatim in the appendix. Points raised by several planners:

| Issue | Raised by |
|---|---|
| The music stage cannot pass without a track; hits had to be invented on a 120 BPM grid, which the skill itself forbids | a, b, c, d |
| The stage-4 independent critic clashes with the harness rule "do not spawn agents unless asked"; all four self-reviewed | a, b, c, d |
| The versioned multi-file pipeline does not fit a one-file deliverable; no light planning path | a, b, c, d |
| Lints checked by hand, error-prone (no runner usable at plan time) | a, b, c, d |
| Caption limit of 4 words vs tagline or card text: undefined | a, b, d |
| Shot size undefined for non-survivor subjects and for POV shots | a, b, d |
| L21 (build shortens) is ambiguous, or punishes the long payoff shot | a, c |
| L34 (idea count ≤ 3 shots) fights hand-centred trailers, leading to relabelling to pass | b, d |
| The hand rules (L05, L08, L09) force a hand into a disaster brief that did not ask for one | c |
| The worked example matches brief a so closely the shot lengths came out identical ("the output may show the example's quality, not the skill's") | a |
| No 30 s worked example or 30 s "be the hand" template | b, d |
| Sound-before-sight vs "never more than 1 frame before the key": reads as a contradiction | d |
| Engine features assumed but unknown: sea drawback, one-hand hang, floating survivors, hand shadow, 2 m pulls, hand drawn in playback | a, b, c, d |

Rules broken or waived, as admitted by the planners:
- stage 2 (all four);
- stage 4 critic and the planted-defect check (all four);
- file layout (all four);
- engine proof (all four, by brief);
- L34 relabelling and an L08 cause changed "for the lint" (b);
- a 0.8 s POV under the 1-2 s hold, a 1.8 s aftermath, and grips relabelled to stay under 3 pull
  shots (d);
- a 0.7 s near miss cut short to pass L21, and a hand added against the brief (c).

## 8. My reading

The result is a clear plan-level win for the skill, but smaller and more qualified than 12-0 and a
0.92-point gap suggest.

What the skill really added:
- **A staged reveal.** Partial or effect, then reaction, then whole.
- **A barrage of 0.5-0.7 s shots into a payoff that sits on the main hit.** It came after a
  stop-down every time.
- **A held hero face with a near-miss payoff.**
- **Late-weighted structure.**
- **Concrete, mostly shootable camera numbers.**

These are exactly the categories where the graders' scores moved most (structure, reveal, camera:
+1.6 to +1.9). The control plans were competent but had the failures attempt 1 had: even shot
lengths (CV 0.17-0.30), short title cards, a main hit spent early (c at 38 % of body), and
disasters crammed in as a list. The skill fixes the list-of-events problem it was built for.

Why the margin is overstated:
- **Brief a is the skill's worked example.** The skill planner reproduced it frame for frame. That
  pair tests the example, not the method.
- **Precision is persuasive.** Graders praised "exact lenses and positions" and "can actually be
  shot". Yet the critics, checking geometry, found that several of those exact frames cannot show
  what they claim. Some of the "shootable" and "camera" credit is for confident specificity, not
  correctness.
- **The control was a single-line prompt.** A control told to "specify lens, distance and timing
  for every shot and stage the reveal" might close much of the camera and sound gap. This test
  shows the skill beats no guidance, not that it beats a good generic prompt.
- **Same model family planned, graded and critiqued.**

Overall: the skill works at plan level for the problem it targets. It is also over-constrained and
homogenizing (section 10), and that will show up as soon as briefs move away from the default hand
reveal.

## 9. Where control plans beat skill plans, and why

1. **Hook (4.25 vs 4.00).**
   - c control opened on the tsunami standing up out of the sea at frame 0 and got hook 5 from all
     three graders. The skill plan opened on the sea draining away, which is subtler and generic
     to tsunamis; the critic said it is "not something only this game has".
   - The skill's anticipation rule (≥ 0.7 s before the full threat) trades the frame-0 punch for
     staging. In a 15 s social cut that is sometimes the wrong trade.
2. **Clarity (4.33 vs 4.17).**
   - Control plans used in-body captions to explain the rules: "ONE PLAYER IS THE HAND" (a); "CLIMB
     TO SURVIVE / OR BE THE HAND / PLAY WITH FRIENDS ONLINE" (b); "ONE FRIEND IS THE HAND" (d).
   - All three graders credited the a control caption for muted viewers.
   - The skill's defaults (0-1 captions in 15 s, ≤ 4 words) pushed the skill planners toward no
     caption in a and c.
3. **Multiplayer proof.**
   - b control showed four survivors taunting the hand, with voice chat ("Over here!", laughter).
   - G1 and G2 both noted that the b skill plan only implies multiplayer. The skill's must-show
     vocabulary has no multiplayer or friends item.
4. **Fidelity to brief c.**
   - The skill's hard rules forced a hand pull into the "disaster is the star" cut. Its planner
     admitted this may be the wrong call; G3 and the c critics flagged it.
   - The control kept the disaster alone.
5. **Inventive ideas the skill plans lacked.**
   - d control: a lobby role-swap shot ("TAKE TURNS. GET EVEN.") praised by two graders, and a
     "Not fair!" wave exchange.
   - b control: a fingertip flick that topples the tower on the title hit.
   - c control: a survivor still holding a toppling block as a closing gag.
6. **Title start position.** Control titles at 80-88 % of runtime sit in the measured 80-95 %
   band. The skill's 15 s window put both 15 s skill titles at 73 %.

## 10. Signs the skill made plans worse

- **All four plans are alike.** The spine repeats in every skill plan:
  1. partial or effect at frame 0;
  2. hero MCU reaction at about 1.6 s (1.0 or 0.9 m, 0.6 m above the block, 50 mm, three-quarter
     front);
  3. whole reveal (a, b and c reuse "hero 3.5 m, hand 5.5 m, 0.5 m up, 24 mm");
  4. a barrage of 0.5-0.7 s shots;
  5. music drop-out with "one creak";
  6. an EWS crown collapse from **80 m out, 1.5 m up, 135 mm, top 14 m, half speed**, identical in
     all four;
  7. the hero hanging by one hand (MS, high, 1.2 m, 50 mm), all four;
  8. a closing crane up about 6 m over 4 s, all four.
- **Copied details.** The hero is "Mint" in all four. Three of four reuse the example tagline
  "Don't be the one who falls."
- **Copied numbers broke some shots.** The 80 m / 135 mm crown frame does not fit a wave hitting
  mid-tower (c). A three-layer tower crown is assumed on every map.
- **Over-constrained.**
  - The planners report bending shots to satisfy lints: a 0.7 s near miss to pass L21 (c),
    relabelling to dodge L34 (b, d), a cause changed "for the lint" (b), and a hand inserted
    against the brief (c).
  - Lint-passing became an aim in itself, separate from making the cut better.
- **Less inventive.**
  - The skill plans contain no idea comparable to the control's lobby role swap or fingertip-flick
    title.
  - Their originality sits in the brief-specific beats: the c drain-and-sly-pull concept, and the
    d same-pinch-from-both-sides cut, which graders liked.
- **Captions suppressed.** This cost clarity for muted social viewers.
- **Brief a reproduced the example.** The planner's own words: "the output may show the example's
  quality, not the skill's."

## 11. Recommended changes to the skill (prioritized; not applied)

1. **Add a planning-only mode.**
   - Allow "target hits", marked unmeasured, with a re-time step once a track exists.
   - Defer the stage-2 gate instead of failing it.
   - Give a single-file deliverable path for plan-only requests.
   - All four planners hit this wall.
2. **Fix the critic step against the harness.**
   - In SKILL.md, state the fallback when spawning a critic is not allowed: a self-review with a
     written checklist, flagged as weaker, plus a request to the user for a critic.
   - Say whether a planted-defect check applies at stage 4 (this test shows it works on text plans).
3. **Make the core verb concept-specific.** Make L05, L08 and L09 apply to hand concepts only. Add
   a disaster-cut verb: the first full hazard view with a survivor in frame for scale by 5 s at
   15 s. Never force the hand into a brief that does not ask for it.
4. **Break the template monoculture.**
   - Label the 15 s giant-hand plan as one example, not the default.
   - Add a 30 s two-role example and a 15 s disaster example built on different spines.
   - Require the concept stage to list two alternatives that differ in opening, payoff and ending.
   - Forbid reusing the example's tagline, hero name and camera numbers verbatim.
5. **Re-derive camera numbers for each shot.** Add a one-line framing check, for example frame
   height = distance x 24 / focal length, and require each shot to state that its subject and its
   impact point fit. The critics caught three copied frames that cannot show their action.
6. **Loosen captions for novice audiences.**
   - Store pages and muted social cuts should get one rule caption by default ("One player is the
     hand").
   - Separate in-body captions from card and tagline text in L32; three planners asked.
7. **Add brief-driven must-show items** such as `multiplayer`, `friends`, `disaster`, `climbing`,
   `pov_hand` and `pov_survivor`, so a multiplayer brief cannot pass on implication.
8. **Revisit the 15 s title window.** 10.0-11.5 s puts the title at 67-77 %, below the measured
   80-95 % band, and graders did not reward it. Consider 11.5-12.0 s with a 3.0-3.5 s card, or say
   plainly that the trade is deliberate.
9. **Clarify the lints the planners stumbled on.**
   - L21: how the last third is counted, and an exemption for a held payoff.
   - L34: let the core verb repeat in hand trailers, or count only shots whose subject is the pull.
   - POV shots: size and yaw.
   - Define pull vs grip.
   - Sound-before-sight vs the 1-frame rule.
   - Stop-down vs breath placement at 30 s.
10. **Add a rule against gaming the lints:** "If you change a label to pass a lint, waive the lint
    instead and say why."
11. **Add a game-capability list**, with "unknown / verify" against each item: sea drawback,
    one-hand hang, floating survivors, a single hand only, hand shadow, pull distance, hand drawn in
    playback. Planners guessed all of these, and one plan showed two hands.
12. **Calibrate the critic to the deliverable format.** Missing schema fields should read "cannot
    check", not "hard fail". Alternatively, ship a plan-to-YAML step so the lints are computed,
    not argued.

## 12. Limits

- Plan level only: nothing was rendered, captured or heard, and no plan was checked against the
  engine.
- The graders are AI agents, not human trailer editors, and they appear to reward numeric
  specificity.
- Only 4 briefs, and brief a is essentially the skill's worked example.
- The same model family (Opus) wrote, graded and critiqued every plan.
- The control was a one-line prompt, not a strong generic trailer prompt.
- The skill planners were re-run after a usage-limit interruption. Their partial first-run outputs
  were set aside unused.
- The critic test used two plans, 8 planted defects of an obvious kind, and one critic per copy.
  The "false alarm" count depends on how format-caused lint failures are treated.
- The objective checks were computed by me from my reading of each table. Single-subject and
  "pull + effect" involve judgement.

---

## Appendix: planner feedback, verbatim

The planners answered three questions: (1) what was unclear, contradictory or impossible at
planning time; (2) which rules were broken or waived, and why; (3) what had to be guessed. The
four answers follow.

### Brief a skill planner

### a-p2 feedback on the deadlyjenga-trailer-craft skill

#### 1. What was unclear, contradictory or impossible at planning time

- **The music gate can't be met before a track exists.** The skill says map the music first, and the stage 2 gate needs every hit taken from the onset list of the real file. The brief had no track. So the hit check and the "move the plan to the real hits" step can only pass on hits I planned myself. The skill never says how to plan with no track: should you write target hits for a composer, or stop? I wrote target hits and marked the stage 2 gate as open.
- **The critic rule clashes with the harness.** Stage 4 asks for a fresh, independent critic agent. My session's instructions say not to spawn agents unless the user asks. The skill only allows self-review "if no second agent can be started". Here one could be started; it just wasn't permitted. The skill doesn't cover that case.
- **The planted-defect check doesn't say which stages it covers.** It is written for evidence packs (renders). At stage 4 the "pack" is the text animatic. It is unclear whether a planted-defect copy of the animatic is expected.
- **The "build shortens" lint (L21) is ambiguous.** It says "last third of body shots (by count, rounded down)". With 8 body shots, rounding down gives 2. The worked example uses 3. I had to check my plan both ways.
- **Short captions vs. the tagline.** One lint says captions are 4 words or fewer. The default tagline "Don't be the one who falls." is 6 words. That only works if the tagline counts as a title, not a caption, and the skill never says so.
- **Shot size is hard to apply when the subject isn't a person.** Size follows the nearest survivor when one is in frame, otherwise the subject's height fraction. A hand shot with a survivor in it (S03) and the crown wide (S07) each need a judgement call about which rule wins.
- **Its own currency check needs git.** studio-tooling.md says to check HANDOFF.md, DECISIONS.md and the git log before relying on it. I was told not to run git or touch the project. So I used the dated table as-is and could not confirm it is still current.
- **The file layout doesn't match a one-file brief.** The pipeline wants brief.yaml, concept.md, music.yaml, plan.yaml, lint.txt and an animatic, each in its own versioned folder. The brief wanted one clean Markdown file. The skill gives no guidance for squeezing the pipeline into a single deliverable.
- **The worked example pulls you into copying it.** This brief is almost exactly the skill's default 15 s concept, and the worked example already passes every lint. The skill gives no guidance on when or how to depart from it. My final shot lengths are identical to the example (45, 60, 60, 30, 18, 15, 72, 30 frames). I did try my own timings (for example a 12-frame shot 6). They failed or nearly failed the beat-snapping lint (L24) and the build-shortens lint (L21), and I moved back to the example. For a test of the skill this matters: the output may show the example's quality, not the skill's.

#### 2. Rules I broke or waived, and why

- **Stage 2 gate:** not met. There is no music file, so the hits are planned, not measured. The sync lint (L11) passes only on those planned hits.
- **Stage 4 review:** self-review only. There was no independent critic, no different model and no planted-defect check, because I was not allowed to spawn agents. My Hook, Clarity and Story scores of 4 were graded by the plan's author, so they are weak evidence.
- **Pipeline files:** condensed. I wrote three Markdown working files in work\a-p2\v01\ instead of the named YAML files, and no lint.txt from a lint runner. I computed the lints by hand, so they are error-prone.
- **Engine checks:** not done. Hard rule 11 and the stage 5 checks (survivor and hand sizes, crown height profile, whether the hand is drawn, the pull dry run) were not possible. This brief allowed planning only.
- **Tooling table:** not confirmed as current (no git).
- **Platform line:** I suggested an optional platform line ("Meta Quest" badge) that the skill does not cover, and flagged it as unconfirmed.

#### 3. What I had to guess

- **The music:** character, 120 BPM, 4/4, and every hit position (0, 4.5, 8.0, 11.0 s). I took these from the skill's example, not from any real track.
- **Hero names and colours** ("Mint", mint-green; "Rook", orange). I got them from the example's ids, not from the game's real character roster.
- **That a person can fly the hand** in a headset for the record take.
- **That the hand renders in playback.** This is still an open finding (F5), and the whole plan is blocked without it.
- **That a pull moves a block 2 m in 1.5 s.** The attempt 1 dry runs moved blocks 0.5 to 0.7 m.
- **That a survivor can hang one-handed from an edge.** I don't know if the game has a hang or catch state at all. The shot may need a fallback.
- **That a shadow will visibly cross Mint's face** (it depends on lighting and sun angle).
- **That survivors fall with the crown** in a usable take.
- **That half speed reads well,** and that Rook's 1 s fall reads from above.
- **That the map is attempt 1's** 56 m tower on a 25 m base, with the crown at the top 14 m.
- **The camera numbers** (80 m out, 135 mm, about 33 degree pitch, 3.5 m and 5.5 m distances). I took them from the skill's tables without seeing the scene.
- **The mix targets** (-15 LUFS, title 6 to 7 dB down). These are the skill's defaults, not measured on anything.


### Brief b skill planner

### b-p2 feedback on the trailer-craft skill (30 s store trailer plan)

#### 1. Unclear, contradictory or impossible at planning time

- **Stage 2 could not be done.** The gate says every hit must come from the onset list of the actual music file, but there was no file. I made up a 120 BPM grid and placed "planned" hits on it. The skill has no "no track yet" mode for planning, such as planned hits that get re-timed later. So the stage 2 gate either fails or gets faked.
- **The stage 4 critic clashes with the agent rules.** SKILL.md makes a fresh critic agent part of the gate. My harness told me not to start agents unless the user asks. Only the rubric file (section 3) allows self-review, and SKILL.md's pipeline table does not mention that fallback. I did a self-review. It is weaker, and I said so in the working file.
- **There is no 30 s worked example.** The giant-hand recipe and the fully linted plan are both 15 s. The 30 s beat sheet is five rows, so most of the 30 s structure had to be worked out by me.
- **Brief items have no lint ids.** "Multiplayer", "disaster" and "climbing" are not in the skill's vocabulary. I invented ids for them, so the must-show check only proves that my own labels exist.
- **L05 does not test what rule 4 means.** My opening partial shot (a block sliding out, fingertips cropped, at 0.9 s) passes L05 because a block is the subject and a survivor is in frame. The real "verb readable" moment is at 4.5 s. The lint is too easy to pass with a partial reveal.
- **L08's "hazard id" is undefined.** It does not say whether the `shows` value has to be the hazard's actor id or a must-show label like "disaster". I had to change a cause to `self` to make it pass.
- **Hook count and L34 pull against each other.** In a game about a hand pulling blocks, "pull" appears all the time. L34 caps any `shows` id at 3 shots, so I took visible content out of `shows` (climbing in S05, the hero's face in S11) only to pass. A lint that pushes you to under-label is a bad sign.
- **The size rules break down for some shots.** "Size follows the nearest survivor" does not work for a first-person view of the hand, or for a tsunami at horizon distance over someone's shoulder. I had to choose sizes for those by judgement.
- **The disaster sound-before-sight rule is ambiguous.** The "first image" could be the effect shot (the sea pulling back) or the wave itself.
- **Stop-down placement is only clear for 15 s.** For 15 s it is "0.3-0.5 s before the main hit". For 30 s the template puts the breath at 16-17 s with wave 2 after it. I put it 2.8 s before the main hit, ahead of a separate wave-impact accent. It is unclear whether that matches the intent.
- **Captions and card text are mixed up in L32.** It says captions are 4 words or fewer. My tagline (6 words) and call to action (5 words) are card text, and I assumed they were exempt.
- **The store-label rule could not be checked.** The skill flags "Captured on Meta Quest 3" from search excerpts but gives no rule for in-engine free-camera footage, which is the case here.
- **The designed-moves share sits right on its edge.** 10-30 % counted over all shots, including the title shot, put me at 29 %. One more eased move would fail it. The window is narrow for a 17-shot cut.

#### 2. Rules I broke or waived, and why

- **Stage 2 gate (hits from measured onsets):** broken, because there was no track. Every hit is a planned time, and the deliverable tells the editor to move the picture to the real hits.
- **Stage 4 independent critic:** not run (see above). I self-reviewed the text animatic instead.
- **Plan schema:** I wrote a compact plan.yaml without tape frames, action cues, framing targets, cut connections, eye trace, per-shot needs, evidence or fallbacks. That means L19, L26 and L27 were only partly checked. The lints were checked by hand, not by a tool.
- **L34:** I first wrote a waiver, then removed it by relabelling `shows` (see section 1). That is gaming the lint, not fixing the plan.
- **L08:** I changed S14's cause from the previous shot to "self" (spray in frame) to pass. It is defensible, but it was done for the lint.
- **L26 screen direction:** left as a note, not fixed (the wave direction needs a directed hazard cue).
- **Hard rule 11 (prove it in the engine) and stages 5 onward:** not done. I was told to stop at planning and not touch Unity, so the plan is not proven.
- **Rule 1, versioned stage files:** only a single v01 folder with merged files (animatic and review in one).

#### 3. What I had to guess

- **Music:** tempo (120 BPM), structure, where the track's biggest hit falls, and that a track can be cut to put hits at 4.5, 12.4, 16.6, 19.2, 21.4 and 25.0 s.
- **Tsunami height:** whether a tsunami can physically reach and fold the crown, which is about 50 m above the sea. The payoff depends on it, and I did not check it.
- **Fall time:** whether the S06-S07 fall reads in 1.8 s. That is only about 16 m of drop, which may not match where on the tower the jump happens.
- **Hand behaviour:** that the hand can hold a pulled block while the crown falls, can pinch a block from a first-person view, and that a person performs the hand for the record take.
- **Survivor moves:** that survivors can climb a sheer face, scramble while the tower shakes, and hang by one hand and haul up. I do not know these animations exist.
- **Distances and framing:** the low water shot at 30 m, the island impact shot at 40 m, and the tsunami at horizon distance on 85 mm.
- **Cast:** survivor colours and how many players there are (I never state a player count).
- **Store text:** the call to action "Wishlist now on Meta Quest". I do not know the game's release state.
- **Defaults:** half speed in the payoff, loudness targets, and lens choices were taken from the skill's defaults, not checked against this map.


### Brief c skill planner

### c-p2 feedback on the trailer-craft skill (disaster cut, 15 s)

#### 1. Unclear, contradictory or impossible at planning time

- **The skill assumes every trailer is a hand trailer.** L05 is a hard rule: a hand pull with a survivor in frame by 5.0 s. L08 and L09 are also written around the pull. This brief said "the disaster is the star" and never asked for the hand. Hard lints cannot be waived, so I had to add a hand shot (shot 4) that the brief did not ask for. The skill has no "disaster cut" core verb (for example, the first full view of the hazard with a survivor for scale by X s).
- **Stage 2 is impossible without a track.** Stage 2 says "every hit taken from the onset list of the actual file", and L11 needs measured onset frames. There was no file. I planned against a hypothetical 120 BPM grid, which the skill itself warns against ("never from a guessed grid"). The skill needs to say what a planning-only run should do. My suggestion: declare intended hit times, tag them "unmeasured", and re-plan once the track exists.
- **Stage 4 needs a fresh critic agent, but the agent tooling says not to spawn agents unless the user asks.** I did a self-review instead. The skill only allows this sideways ("if no second agent can be started"), and it doesn't say what to do when a second agent *can* be started but shouldn't be.
- **L21 ("build shortens") fights the long payoff.** With 8 body shots, thirds of 2 shots each meant the 2.2 s payoff shot pushed the last-third median above the first. I had to add a ninth shot and cut the aftermath to 0.7 s to pass. The worked example only passes because its lengths happen to work out. The lint seems to punish exactly the structure the beat sheet recommends.
- **L05 and L09 squeeze the timeline.** The pull shot must be at least 45 frames, with its key at least 12 frames in and at or before 150. The disaster reveal recipe also wants wrong calm, effect, notice and whole before it. Fitting all of that in 4.5 s left almost no slack (my key lands at 4.93 s).
- **Reveal tags for a "sly" hand beat.** The hand's first appearance in shot 4 is a whole view with no partial or reaction of its own. L08 passes only because the hazard's reveal came first. It is unclear whether a second threat needs its own staged reveal.
- **The sound plan wants frame-level placement against a track that doesn't exist.** The ±1 and ±3 frame rules can't be checked or meaningfully satisfied at planning time.
- **Deliverable vs. pipeline.** The skill wants brief.yaml, concept.md, music.yaml, plan.yaml, lint.txt and an animatic as separate versioned files. The caller wanted one plain-language file. I collapsed the stages into one working-notes file. The skill gives no lighter path for a planning-only run.

#### 2. Rules broken or waived, and why

- **Separate stage files (pipeline):** collapsed into one working-notes file (work\c-p2\v01-working-notes.md), to keep it light.
- **Stage 2 (hits from a real file):** broken. No file exists, so the hits are planned, not measured.
- **Stage 4 independent critic and the planted-defect check:** not done. Self-review only; I flagged it as weaker in the notes.
- **Lints run by a tool:** none. I checked every lint by hand, and arithmetic slips are possible.
- **Brief vs. L05:** I bent the brief slightly to satisfy the skill by adding a hand pull. I judged a hard lint the stronger constraint, but this is arguably the wrong call for a "disaster is the star" brief.
- **Shot readability:** shots 6 and 9 are 0.5 s and 0.7 s. They pass the minimum frame counts, but a 0.7 s near miss is short for the "hero survives" recipe (about 1 s). I accepted this to pass L21.
- **Shot 4 has no fallback.** If the hand isn't drawn in playback, the plan is BLOCKED, as the skill demands. But that means a disaster trailer can be blocked by the hand, which is odd.

#### 3. What I had to guess

- **Tsunami scale and behaviour in the engine:** whether the crest can reach the middle of a 56 m tower, and whether a draining-sea effect exists at all. Shot 1's whole hook depends on the water pulling back, which the skill's tooling notes never mention.
- **Whether the hazard can be directed or repeated** (screen direction, timing). The notes only mention tornado direction.
- **Whether a crown collapse along the pulled-block gap is something the physics will actually give us.**
- **Tempo, hit times and track character:** entirely invented.
- **The hero's colour and name**, and whether bots will scramble or point on cue (shot 7).
- **Camera distances for wave shots:** the camera notes give a tsunami recipe but no distances to the wave or sizes for it, so I guessed the shot sizes.
- **Whether a "plip" block gag under the title fits the "no button" default:** I read it as allowed under the card.
- **Loudness targets for the title** (-4 to -10 LU for 15 s): the notes call the evidence weak, so I just restated the range.


### Brief d skill planner

### d-p2 feedback on the trailer-craft skill ("Be the hand", 30 s)

#### 1. Unclear, contradictory or impossible at planning time

- **Stage 2 cannot pass without a music file.** The gate asks whether every hit comes from the measured onsets of the actual file. There is no file, so every sync point (0, 4.0, 7.0, 12.0, 20.0, 25.0 s) is a placeholder. The skill gives no "planning without a track" mode. It should say: pick target hit times, then choose or edit the track to fit them.
- **Stage 4 requires a fresh independent critic.** My tool rules say not to start agents unless the user asks, so the two instructions conflict. I self-reviewed. The rubric does allow for that and says so, but the pipeline table presents the critic as mandatory.
- **"Pull" vs "grip" is not defined.** The cause-and-effect check accepts a previous shot only if it shows a pull, a hazard, or a whole or payoff reveal. The idea-count check caps any shown item at 3 shots. The rule of threes caps hook appearances at 3. A trailer about being the hand needs more than 3 hand actions. To fit, I had to call some of them "grips", which the cause check may not accept as causes. That meant reordering the shots so that every consequence directly follows a pull.
- **Size and angle for POV shots are undefined.** I could not tell whether a hand-player POV looking down at tiny survivors counts as WS, or whether a survivor POV of the fingers filling the sky counts as WS, CU or INSERT. That choice changes the close-shot share and the jump-cut check. Yaw is also undefined for a POV, so the 30-degree jump-cut check cannot be applied to POV followed by third person.
- **POV hold of 1-2 s vs barrage shots of 0.4-0.9 s.** Putting a hand POV in the barrage (my S14, 0.8 s) breaks the POV guidance. The skill does not say which rule wins.
- **"Sound before sight" (0.5-1.5 s early) vs "never more than 1 frame before the key frame".** The two are compatible only if the early threat sound is attached to a different shot's key. That is not stated, so it reads like a contradiction.
- **The breath section vs the stop-down.** The 30 s beat sheet puts a breath at 16-17 s and also a stop-down before the main hit. It is unclear whether a single 0.5 s drop-out inside the payoff shot satisfies both. I merged them, and added a separate comic drop-out at 11.2 s.
- **Brief vocabulary doesn't map.** The brief asked for "hand POV", "survivor POV" and "multiplayer with friends". The skill's must-show ids (pull, hand, survivor_face, fall, tower_over_ocean) have no friends or social item, and nothing covers voice chat as trailer sound beyond "duck music under VO".
- **No 30 s "Be the hand" template.** The only "Be the hand" concept is a 15 s single take. Its 30 s equivalent (two roles, same block) exists only as a short recipe in another file.
- **The aftermath default (0.8-1.5 s) doesn't fit the 30 s layout.** With the title window at 24-26 s and the main hit placed late, the space left over pushed my aftermath to 1.8 s.
- **The "BLOCKED if the hand is not drawn" rule cannot be checked while planning.** Every hand shot (8 of 17) carries an unresolved verify item. That is honest, but it means the plan's feasibility is unknown at the stage-4 gate.

#### 2. Rules I broke or waived, and why

- **Hits not taken from a measured file**, because there was no track (see above).
- **No independent critic and no planted-defect check.** The tool rules said not to start agents. I did a self-review only, and this was noted in the working file.
- **Artifacts not produced as specified.** There is no brief.yaml, concept.md, music.yaml or plan.yaml, and no v01 folder. Everything went into one working-notes file, because the task asked for a single plain Markdown deliverable and a free work folder. The plan table there has frames, keys, sizes and moves, but not the full schema per shot.
- **Lints run by hand, not by a tool.** There is no lint runner, so the self-lint may contain arithmetic or interpretation errors.
- **Two hand POV shots are shorter than the 1-2 s POV hold:** S08 (1.2 s is inside the range) and S14 (0.8 s is below it). I kept them for barrage energy.
- **Aftermath of 1.8 s**, longer than the default.
- **The end-card tagline has 7 words.** The 4-word cap is for captions, and I treated the end line as a card. The two in-body captions are 3 words each.
- **Possible stretch of the idea-count check:** I labelled three hand actions as grips rather than pulls to stay at 3 pull shots.

#### 3. What I had to guess

- **Whether the game can do these things:** the hand casting a visible shadow, a hand-player POV that shows the player's own giant hands, a survivor POV camera, a finger wave, holding a block up "like a trophy", a dance emote, and one survivor catching another's hand.
- **Whether a performed hand can time a pull to slide a block away mid-jump**, and whether pulls move blocks 2 m or more (attempt 1 managed 0.7 m).
- **Map numbers:** the top-section height (about 14 m) and the 80 m water position were taken from the attempt-1 map, not from the map this trailer would use.
- **The hand's visible height side-on.** The skill itself says "assume about 3 m".
- **Survivor colours and roles.** I used the mint hero and an orange friend from the worked example's naming.
- **Voice-chat lines.** These are invented, and capturing clean voice from a real session is assumed to be possible.
- **Music:** the tempo (120 BPM), the character, and that a track with hits at those times exists or can be edited to fit.
- **The call-to-action wording** ("Play on Meta Quest"). Release status and store wording are unknown.
- **Slow-motion length:** about a 1.7 s real fall stretched to about 3 s at half speed, assumed to cover the payoff.

