# Review rubric and critic protocol

Open at stage 4 (plan review) and stages 7-9 (critic rounds). Source: report 05 (Lieu's critique and
workflow posts, Pixar's Braintrust note form, CutClaw's reviewer agent, Huang et al. on
self-correction, LLM-judge bias studies, ShotBench and VEU-Bench on what video models can judge).
Weights, thresholds and round caps are inferred defaults; they exist because a stop rule is needed.

## 1. Why a separate critic

- Models do not reliably fix their own reasoning without outside feedback (Huang et al., ICLR 2024).
  The critic works from the evidence pack and lints, not from the maker's reasoning.
- Judges prefer their own outputs, the first of two options and longer answers; rubric label order and
  earlier scores in context shift scores. Hence: a fresh agent, a different model if possible, shuffled
  anchors, swapped pair order, no previous scores in context.
- Video models are weak at camera motion and editing across shots (ShotBench below 60 %, VEU-Bench
  judging below 2 of 5) and sample video sparsely (about 1 frame per second by default in common APIs).
  So: geometry from the engine, sound from DSP, content and composition from full-size stills, and
  judgement on top.

## 2. The evidence pack (built for every render from stage 7 on)

Per shot, each image labelled with shot id, cut frame and tape frame:
1. **Filmstrip**: a frame every 6 frames (0.2 s at 30 fps), at least 5 per shot, 320 px wide. Does the
   action progress? Anything pop in or out (ghosts, see-through survivors as in F9)? Does the subject
   stay in frame? Is the camera static where the plan says it moves?
2. **First, key and last stills** at full preview size, plus key -2 and key +2. One question at a time,
   each answered yes or no plus one sentence: Is the subject visible and recognisable? Is it where the
   eye goes first (name the grid cell, compare with `frame_at_key.grid`)? Is the `learns` line true of
   this frame? Does the hero's reaction read through head direction, body and hands (the avatars
   have no drawn face)? For every hit: are the attacker and the victim both readable, and can you
   say who did what to whom from key -2, key and key +2? Is the hero's colour the one the plan names?
   Anything ugly (clipping, camera in geometry, grey ghosts, a white survivor from motion blur, a
   survivor sunk into a tilted block)?
3. **Subject overlay**: the key still with the engine's projected box, area %, occlusion % and the 3 x 3
   grid, plus the attacker's and victim's boxes on every hit. If the still looks empty but the overlay
   says the subject is 30 % of frame, look again and report the disagreement (that was F5).
4. **Motion strip**: camera speed from the track keys (m/s, deg/s), subject screen speed, mean optical
   flow, frame-difference score, with lines at the key frame and the cuts. Is there a motion peak at the
   key frame? Any frame-difference spike that is not a cut (a pop)?
5. **Eye-trace pair**: last frame of shot n beside the first of n+1, each with the subject box.

For the whole cut:
6. **Cut contact sheet**: one key still per shot, in order, with its `learns` line under it. Read only
   this: is the story clear?
7. **Audio plot**: mix waveform, momentary and short-term loudness, beat grid, cut marks, detected
   onsets, planned sound events.
8. **Sync table**: for every cut, hit and key moment, the nearest onset and the offset in frames, in
   two columns: the full mix and that event's own sound lane (`tools/assemble.py`). Trust the lane
   column; every voice line is marked "needs ears" whatever it reads.
9. **Stem table**: integrated loudness and peak per lane (music, take, designed SFX, voice). Attempt 1's
   music stem sat near -40 dB RMS against SFX near -10 dB peak (F3).
10. **Delivery numbers** and the `profile.py check` output (`measured-reference.md`).

Anything that depends on hearing (does the music fit, does the boom satisfy, is anything harsh) is
marked **unverified** and goes to the human pass.

## 3. Setup

- The critic is a fresh agent. It gets: `brief.yaml`, `concept.md`, `plan.yaml`, lint output, the
  evidence pack, the previous round's notes (from round 2), and the previous version's pack for a
  pairwise comparison. It does not get the maker's reasons, except `waive` entries, and not the
  previous round's scores.
- Use a different model from the maker when one is available. If no critic can be started, use the
  fallback in section 3a.
- At stage 4 (plan review) the "evidence pack" is the text animatic and the lint output. A plan
  written in plain language or without some schema fields is still reviewable: see "Fields the
  format lacks" below.
- Prompt skeleton:

```text
You are a strict trailer critic. You did not make this trailer and owe it nothing.
Inputs: brief, concept, plan, lint output, evidence pack [, previous notes, previous pack].
1. Answer every evidence-pack question yourself, citing shot and frame or a table row.
2. If there is a previous version, judge each category A/B and then B/A before any score.
3. Score each rubric category 1-5 using the anchors given (order shuffled). Every score cites
   evidence; a 4 cites the best and the worst instance; a 5 cites a reference trailer measure.
4. A hard lint that you can compute from what is given and that fails caps its category at 2.
   If the plan's format lacks the field a lint needs (no measured onsets, no `learns` lines, no
   smoothing values), write "cannot check (format)" for that lint. It is not a failure and caps
   nothing; list it under "owed".
5. Check the geometry yourself: for every shot with numbers, compute the frame height at the
   subject (distance x 24 / focal) and say whether the subject and its action fit. Check that no
   shot needs something the game or the Studio cannot do (a punch cue, a tsunami cue that pushes
   survivors, an elimination on the Studio stage: `game-content.md` section 9) and that every
   `shows` label is true of the shot as described.
5b. Mayhem: for every hit, name the attacker and the victim and say whether both read; name the
   hero in each section; say whether each new disaster gets its own beat and whether you always
   know where on the tower you are.
6. For every category at 3 or below write: shot/frame, symptom, cause, stage to return to,
   optional fix.
7. End with a numbered list of concrete errors found, then the top three problems, ranked by how
   much fixing each would raise the weighted score.
Do not praise. Do not suggest re-concepting unless Hook or Story scores 1.
```

### Reading a critic's score

In skill test 1 the strict critics scored clean, competent plans 2.8-3.0 weighted, while
editor-style graders gave the same plans about 4.5 of 5. Both were useful: the critics found real
errors the graders missed (a 135 mm crown frame that could not hold a mid-tower wave, a 1.7 m full
shot that could not hold a running jump, a shot with two giant hands). So:
- The **errors-found list is the main output.** Fix every concrete error before looking at the number.
- The **absolute score is strict** and not comparable to a human editor's. Use it to compare
  versions reviewed by the same critic set-up, and to apply the stop rules.
- A low score made of "cannot check (format)" entries is a format problem: supply the fields or a
  plan-to-YAML step, do not re-concept.

## 3a. When no independent critic can run

Use this when the session may not spawn agents (or none can be started). It is weaker; say so.
1. Head the review **SELF-REVIEW (weaker than an independent critic)**.
2. Score every rubric category against the anchors, each with a cited shot and frame or number,
   exactly as the critic prompt asks. Score as a stranger: assume each claim is false until a number
   or line in the plan supports it.
3. **Defect sweep** (the planted-defect idea, adapted): for each defect kind below, find the shot
   where it would most likely hide in *your* plan and write the check you made and its number. A
   defect kind you cannot check is listed as owed.
   - payoff key off the main hit by more than 2 frames; title off its hit or earlier than its window;
   - the hero's reaction placed after the full reveal; a duplicated or near-duplicate shot;
   - a subject that would be hidden or facing away at its key frame; an impact with no sound;
   - a frame whose `fit` cannot contain its action; a capability the game or Studio lacks (a punch
     cue, a sea drawback, a drawn face, a pushing `tsunami` cue); a `shows` label that is not true of
     the shot; an example's tagline, hero, twist or numbers;
   - mayhem: a hit whose attacker is off frame and not in the shot before; the hero missing from the
     build or the payoff; two disasters first shown in the same beat; a knock-off cut before the
     victim lands; a crowd wide with nobody at h ≥ 0.08; a reaction shot that relies on a face.
4. List at least three real problems with fixes. If you find fewer, look again at the framing and
   the capability list before concluding.
5. End with a **critic owed** block for whoever orchestrates the work: the files to give an
   independent critic (brief, concept, plan, lint table, animatic), the prompt in section 3, and the
   planted-defect recipe in section 6 (for a text plan: move the payoff 5 frames off its hit, move
   the reaction after the reveal, take a hit's attacker out of its frame; skill test 1's critics
   found 3 of 3 such defects in both copies).
6. Mark the stage-4 gate "passed on self-review". In production mode an independent critic must
   still review the plan or the preview before stage 8.

## 4. Rubric

| # | Category | Weight | What it asks | Hard lints that cap it at 2 |
|---|----------|--------|--------------|------------------------------|
| C1 | Hook | 1.5 | Do the first 2 s show something only this game has, and is the core verb readable by its deadline? | L04, L05 |
| C2 | Clarity and readable chaos | 1.5 | Does every shot read at preview size: one subject, one idea, the `learns` line and `shows` labels true, the subject visible? In every hit, can a viewer say who did what to whom? Are several survivors readable at once where claimed? Muted: is the rule readable (a caption where needed)? | L02, L09, L10, L41, R01 |
| C3 | Story, cause and escalation | 1.5 | Sections in order, each beat raising something, a staged reveal or a cold open whose consequences escalate, every effect after its cause, disasters added one layer at a time, both halves of mayhem shown, ending on the strongest beat | L03, L08, L40 |
| C4 | A hero through the chaos | 1.0 | One hero we can follow (by colour) through the hook, the build and the payoff; her reaction reads through head, body and hands; someone fails; the hero wins, loses or nearly falls | L14, L42 |
| C5 | Rhythm and sync | 1.0 | The declared shape met; payoff and title on hits; barrage on hits; pace that builds; not gridded | L06, L07, L11, R03 |
| C6 | Shot craft and geography | 1.0 | Framing that can contain its action and the knockback path (`fit`), scale references, declared moves eased, no jump cuts, continuity, eye trace, the tower's geography clear (where is the hero, which way is down to the sea) | L15, L16, L17, L38 |
| C7 | Sound design and mix (by numbers) | 0.75 | Every key moment has a sound; lanes balanced; ramp; title drop; delivery targets | L12, L13, R04 |
| C8 | Titles | 0.5 | Readable, in the window, on a hit, saying something only this game could say | L07 |
| C9 | Technical | 0.75 | No pops, ghosts, clipping cameras, black frames, flashes, reused footage | R04, R05 |
| C10 | Brief fit | 0.5 | Must-show list proven as the brief defines it (multiplayer on screen, not implied), the concept type the brief asked for, length, aspect, tone, nothing copied from an example | L18, L19, L36, L39 |

Weighted mean = sum(score x weight) / 10.0.

### Anchors (shared meaning)

| Score | Meaning |
|-------|---------|
| 1 | Broken: a viewer notices the failure at once |
| 2 | Weak: a clear failure in at least one important shot, or a hard lint failing |
| 3 | Acceptable amateur: works, with several noticeable problems |
| 4 | Professional: problems only a trained eye would note |
| 5 | Reference quality: matches a measured reference on this category's numbers and reads perfectly |

### Anchors by category (examples for 1, 3 and 5)

| Category | 1 | 3 | 5 |
|----------|---|---|---|
| C1 Hook | opens on a slow establishing shot; no disaster and no fight in the first 5 s | the hook appears by 2 s but is hard to read; the verb lands late in its window | frame 0 is a readable piece of mayhem; the first hit or hazard hit reads in one look well before its deadline |
| C2 Clarity | two or more shots where the subject cannot be found, or a brawl where nobody can say who hit whom | every subject found; one hit needs a second look to see its attacker | every contact-sheet frame reads alone; every hit's attacker and victim read at once; the eye is always where the plan says |
| C3 Story | a list of events in captured order, no turn; every disaster at once | sections in order but one beat repeats without raising anything | each beat sets up the next; disasters stack one layer at a time; every effect after its cause; ends on the strongest image |
| C4 Hero | no person to follow (attempt 1 F16); strangers in every shot | a hero, but she disappears in the build or her reaction does not read | a hero we pick out by colour in 1 s, in danger in every section, whose body tells us how she feels |
| C5 Rhythm | 5-6 even shots neither montage nor take; the payoff misses the main hit by over 6 frames | shape met, payoff on its hit, but no build into it | barrage on hits, payoff and title on hits within 2 frames, a breath before the drop |
| C6 Shot craft | subjects blocked or facing away; unmotivated drifts; no idea where on the tower we are | sound framing, one jump cut, a lost scale reference or one confusing jump in height | every frame composed, every move eased and motivated, the geography always clear |
| C7 Sound | key moments silent; slow motion with real-speed sound (F4) | every key has a sound, but a lane is buried or the title does not drop | every accent an onset; ramp +2 to +6 LU; title -6 LU or more; peaks within spec |
| C8 Titles | 2.3 s title louder than the body (attempt 1) | in the window but busy picture behind | clean, on its hit, held long enough, says what only this game is |
| C9 Technical | ghost survivors, a camera inside a block, a flash frame | one minor pop | nothing to fix |
| C10 Brief | must-show items missing | all items, one weakly | all items, exact length and aspect, tone right |

## 5. Evidence rules

- Every score cites at least one evidence item (shot and frame, or a number from the sync table,
  overlay or lints). A score with no citation is invalid and counts as 2.
- A 4 cites the best and the worst instance in the category. A 5 cites a reference measure (from
  `measured-reference.md` or a reference trailer the brief names); without one, 4 is the ceiling.
- Notes for every category at 3 or below: `shot/frame`, `symptom`, `cause` (name the cause, not only the
  fix, as Pixar's Braintrust does), `stage` to return to, optional `fix`.
- Top three problems last, ranked by expected gain.

## 6. Guarding against a lenient critic

1. **Planted defects every round from stage 7; recommended at stage 4** (on a text plan, plant
   plan-level defects: payoff off its hit, title moved early, reaction after the reveal, a
   duplicated shot, a hidden hero, a hit whose attacker is out of frame). `tools/pack.py --plant`
   builds the render-level copy. A script makes a copy of the evidence pack with three injected
   defects: one cut moved 5 frames off its hit in the sync table, one key still replaced by a frame where
   the subject is hidden, one impact sound removed from the audio plot and stem table, or one shot
   duplicated. The critic gets both packs in random order and does not know which is which. If it finds
   fewer than 2 of 3, or scores the defective pack within 0.25 of the real one, discard its review and
   rerun once with a warning; if it fails again, send the round to the human.
2. **Calibrate on attempt 1** the first time the skill is used on a machine: the critic scores attempt 1
   (`attempts/attempt-1/`). Its known faults: no hand (F5, F6), the hero facing away behind a block (F16,
   shot B), the unreadable pull (F17), sound under the ramp (F4), a 2.3 s title louder than the body, no
   close shot. A critic that scores it above 3.0 weighted or misses two of those is miscalibrated.
3. **Pairwise first.** From round 2, judge each category A/B and B/A before absolute scores; a category
   where the orders disagree is "no change".
4. **Shuffle the anchor order** each run.
5. **No previous scores in context**, only previous notes, to check whether each was fixed.
6. **Lints overrule.** A failing hard lint caps its category at 2 (table in section 4).
7. **Inflation check.** If the weighted mean rises by more than 0.75 in one round while fewer than half
   of the previous top three were fixed, a second fresh critic re-scores.

## 6b. When critic runs are capped

A critic run with the planted-defect check costs two packs and a fresh agent. If the budget is
smaller than the rounds above:
- **Two runs:** stage 7 (the preview: every fix is still cheap) and stage 9 (the fine cut: the
  release gate). Stage 4 and stage 8 are labelled self-reviews (section 3a). This is what
  hand-reveal-2 did, and both critics found 3 of 3 planted defects.
- **One run:** stage 7. Self-review the fine cut and say in the hand-off that it had no
  independent critic.
- **None:** self-review every round (section 3a), and the "critic owed" block goes to the human
  with the files and prompt ready.
- Never drop the planted-defect check from a critic run to save a run: a critic you cannot trust
  costs more than the run.

## 7. Re-cuts, disputes and stopping

- **Re-cut** when any of: a hard lint fails; a category is at 2 or below; the weighted mean is under 3.5
  at rough cut or under 4.0 at fine cut; a top-three note is in C1, C2 or C3.
- **Return to the stage the note names.** Only a 1 in C1 or C3 sends the work back to concept or beat
  sheet.
- **Disputes.** The maker may contest a note once with evidence; the critic answers once. Measurable
  questions go to the lint or the measurement. Taste goes to a second fresh critic; if the two critics
  split, the maker's choice stands and the point goes on the human-pass list.
- **Stop** when: *accept* (no hard lint failing, every category ≥ 3, weighted ≥ 4.0, planted check
  passed); *plateau* (less than 0.2 gain over two rounds: ship the best and report what blocked it);
  *round cap* (3 rough-cut and 2 fine-cut rounds); or *blocked* (a needed Studio feature has no
  workaround: report it, do not hide it).
- After stopping, the human pass is still required. The report always lists what could not be judged:
  sound by ear, motion feel at full frame rate, VR comfort.

## 8. What to hand the human

- The mp4, and the previous version's mp4 if there is one.
- One line per shot: time, section, `learns`.
- The sync table and the loudness numbers (integrated, true peak, ramp, title drop).
- The critic's last top three and any split decisions.
- The unverified list, as questions: Does the music fit the candy-peril tone? Does the main hit land on
  the payoff? Can you tell who hit whom in every fight? Do you follow the hero, and does her body
  read? Is every voice line in sync (the meters cannot tell)? Is the title drop-out too abrupt? Is
  anything harsh, muddy or out of sync? Is any shot too fast to follow at full speed?
- Save their notes in the version folder (R07).
