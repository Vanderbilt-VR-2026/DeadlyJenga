# Reconciliation of the six trailer-craft reports

Written 2026-11-20. Inputs: `research/01-structure.md` to `research/06-measured-trailers.md`,
`tools/data/tables.md`, `tools/data/reference_profile.json`, and attempt 1's
`attempts/attempt-1/FINDINGS.md`. Output: the decisions the skill
`skill/deadlyjenga-trailer-craft/` is built on. **Revision 2 (2026-11-20) supersedes the
decisions above where they differ** (notably conflict 5, the 15 s title window, and conflicts 19
and 21), and **Revision 3 (2026-11-20, at the end) supersedes both** where they differ (the hand as
the hook, the hand's size, conflict 15's 0.6 rule, the face rule).

## How evidence is weighed

| Tag | Meaning | Weight |
|-----|---------|--------|
| [measured] | Counted in report 06: 22 official trailers, one rater, shot sizes and moves judged from stills, beats from librosa. | Wins over inference. Thin for short cuts: only 4 trailers of 32 s or less, and none of them a montage. |
| [sourced] | A working editor, a standard, a platform spec, or attempt 1's documented failure (reports 01-05). | Wins over inference. Weighed honestly against measurement when they disagree. |
| [inferred] | A number or rule a report author derived. Marked "[I]" or "(inference)" in 01-05. | Becomes a default or a suggestion, never a hard rule on its own. |

A rule becomes **hard** only if it is checkable from the plan or the render and rests on measured
or sourced evidence. A hard rule may carry an inferred threshold when the rule itself is sourced;
those are listed at the end under "unsure". Numbers for 15 s cuts are extrapolated from the
short and mid classes in 06 and say so.

Game scale used throughout (from the brief and `CastleLayout.blockSize`): a survivor is about
0.94 m tall (eyes about 0.85 m), the hand is about 4 m across, a block is 6 x 2 x 2 m, and attempt
1's map had a 56 m tower on a 25 m castle. Verify the survivor and hand numbers in the scene before
trusting distances.

---

## The conflicts

### 1. Cutting on the beat
- **02 (inferred):** 40-75 % of cuts within 1 frame of a beat; section changes on downbeats; no
  more than 4 consecutive beat cuts. Lieu (sourced) "tries to make picture cuts land on the beat"
  but also prefers cutting to actions.
- **05 (inferred):** lint L01, every cut within 1 frame of a beat or half-beat, hard.
- **06 (measured):** pooled over 599 body cuts, 25.0 % land within 2 frames of a tracked beat
  against 24.9 % chance (p = 0.47); no trailer passes the phase test (0 of 21). Cuts do land on
  audio onsets more than chance (40.9 % vs 36.5 %, p = 0.014), and cuts ending shots under 0.8 s
  land on strong onsets at twice chance (12.8 % vs 6.7 %, p = 0.004). TABS' final barrage lands
  six cuts within 0-3 frames of a beat or strong onset. Caveat: librosa on the full mix, which
  could miss phrase-level or sound-design cutting.
- **Decision:** Drop beat-gridding as a rule. Hard: the hits are measured from the audio file;
  the payoff (a cut or an in-shot key moment) lands on the main hit within 2 frames; the closing
  title lands on a hit; every planned sync point shows up as an onset in the render within 2
  frames. Default: plan 3-5 sync points in 15 s, 5-8 in 30 s, 8-14 in 60 s; land barrage cuts on
  onsets (designed SFX count). Soft lint: warn when more than 80 % of cuts sit on beats
  (mechanical snapping, which Lieu warns against).
- **Confidence:** measured that gridding is not what references do; sourced that the payoff and
  title sit on hits (Lieu, Violet Recording); the sync-point counts are inferred.

### 2. Adjacent shots must change size or angle
- **03/05 (sourced as standard practice, thresholds inferred):** change size by a step or move 30
  degrees between adjacent shots; 05 made "size ≥ 2 steps or yaw ≥ 30° or angle class" hard (L07).
- **06 (measured):** 59 % of cuts join two shots of the same size (mid IQR 54-92 %); wide is the
  most common size in 14 of 21; close shots are 10 % or less in 15 of 21 (median 5 %). Gameplay
  montages vary the place or action, not the size. Alternation is a cinematic-trailer habit
  (reveals 48 % same-size).
- **Decision:** Size alternation is not required. Hard only against the true jump cut: two
  consecutive shots of the same subject with the same size, angle class and less than 30° of yaw
  change fail unless tagged a deliberate jump cut. Default: wide is the house size (40 % or more of
  body shots); close shots (CU, ECU, INSERT) at most 25 % in 15 s and 15 % in 30-60 s.
- **Confidence:** measured for sizes; sourced (30-degree rule) for the narrow jump-cut guard.

### 3. A button after the title
- **01/04 (sourced via secondary summaries; length inferred):** add a 1-3 s gag or tease after the
  title.
- **06 (measured):** 2 of 22 have one (Alyx, Job Simulator); Content Warning puts its joke just
  before the logo.
- **Decision:** Suggestion only. If a 15 s cut has a button, it lives inside the title window as
  picture under the card, not as extra seconds after it.
- **Confidence:** measured.

### 4. Shots shorten through the build
- **02/05 (inferred):** mean shot length falls section by section into the peak; 05's L08 wants
  the last third's mean at 0.7 x the first third's.
- **06 (measured):** the last quarter's median shot is shorter in 12 of 19 (median ratio 0.85).
  Reveals (0.39) and long trailers (0.57) shorten strongly; mid-length party trailers do not
  (1.06); five end on a held gag or hero shot.
- **Decision:** Soft lint (default, waivable with a reason such as "ends on a held gag"): the last
  third of body shots has a median length no longer than the first third's. What is hard instead
  is the measured spread and barrage (conflict 7).
- **Confidence:** measured as a tendency only.

### 5. Title timing and end-card length, and what a 15 s cut can afford
- **01/02/05 templates (inferred):** title at 11.5-13.5 s in a 15 s, end card 2-3.5 s, plus a
  1-1.5 s button; 02's 15 s cut list gives 3 s to the logo.
- **06 (measured):** closing title at 80-95 % of runtime in 18 of 22 (median 90 %); title plus end
  card 4-8 s in 15 of 22 (median 6.2 s; mid IQR 5.2-6.4 s; short spots 4.6-6.0 s, median 5.1 s).
  Attempt 1 gave the title 2.3 s and fails. Short class (n=4, 18-31 s): title at 62-87 % (IQR),
  card 4.0-5.7 s (IQR).
- **The squeeze:** in 15 s, 80-95 % leaves 0.75-3 s for the card, below every measured card. The
  two measured bands cannot both hold at 15 s. The short class (closest in length) has a later
  card share: its IQRs allow a title at 62-87 % with a 4-5.7 s card.
- **Decision (extrapolated, hard):** 15 s: closing title starts at 10.0-11.5 s, title plus card
  3.5-5.0 s (default 11.0 s and 4.0 s). 30 s: title at 24-26 s, card 4-6 s. 60 s: title at 53-55 s,
  card 5-7 s. The game name does not appear earlier unless laid over gameplay. A title laid over a
  still-moving gameplay shot counts as card time (Human Fall Flat VR does this).
- **Confidence:** measured for 30-60 s; extrapolated for 15 s, inside the short class's IQRs.

### 6. First gameplay
- **01 (sourced):** 2-3 s for store and social, up to 5 s for showcases (Zukowski 2-5 s, Steam
  "under 10 s"). **04:** inside 2 s. **02:** frame 0 not black or silent.
- **06 (measured):** first gameplay within 2 s in 13 of 21, within 8 s in 17 of 21; party and
  physics trailers median 0.3 s; short spots 0.2-2.0 s (3 of 3).
- **Decision (hard):** gameplay on screen by 2.0 s at every length; frame 0 is not a logo, black
  or a text card; no more than 0.25 s of silence at the start.
- **Confidence:** measured and sourced agree.

### 7. Shot length, spread, cuts per minute, the 15 s shape
- **02 (inferred):** section table (setup 2.5-4 s, build 1.2-2 s, climax 0.5-1 s); CV of shot
  lengths ≥ 0.35; a 15 s example of 11 shots, mean 1.36 s. **01/04/05 (inferred):** 15 s templates
  of 6-11 shots.
- **06 (measured):** median picture shot 1.0-2.2 s in 16 of 18 mid and long trailers (mid median
  1.60, IQR 1.44-1.71; reveals 1.28). Spread: 10th-percentile shot ≤ 1.0 s in 13 of 18; at least one
  shot ≥ 4 s in 13 of 18. One barrage: mid median 4 cuts in the busiest 5 s (IQR 4-5.5). Cuts per
  minute mid IQR 25-38. Short spots are single takes or graphic pieces; no short montage was
  measured. 06's S1: do not make a 15 s of 5-6 shots of 2-3.5 s; attempt 1 was exactly that (median
  2.4 s, shortest 2.0 s, 2 cuts in its busiest 5 s).
- **Decision (hard):** declare a shape. *Montage:* body median shot 1.0-2.2 s, shortest body shot
  ≤ 1.0 s, longest ≥ 2.0 s (15 s), ≥ 3.0 s (30 s), ≥ 3.3 s (60 s), and at least one 5 s window with
  4 or more cuts. *Single take:* one picture shot covers ≥ 70 % of the body, at most 2 cuts in the
  body. Defaults: 6-9 body shots in 15 s, 12-18 in 30 s, 22-32 in 60 s; CV ≥ 0.35 as a soft lint.
  02's section table becomes guidance in the references only.
- **Confidence:** measured for 30-60 s; the 15 s and 30 s longest-shot floors are extrapolated.

### 8. Loudness target
- **02 (sourced):** online master -14 LUFS ±1, true peak ≤ -1 dBTP, short-term max ≤ -10 LUFS
  (Shepherd; YouTube turns louder files down to about -14 and does not raise quieter ones).
- **06 (measured):** integrated median -16.0 LUFS (IQR -19.6 to -13.0); 13 of 22 between -20 and
  -12; 11 of 22 above -1 dBTP true peak (platform re-encodes; codec overshoot). Attempt 1: -15.1 LUFS,
  -0.6 dBTP.
- **Decision:** hard window -20 to -12 LUFS integrated and true peak ≤ -1.0 dBTP (a delivery rule,
  not a sample norm). Target -15 ±1 LUFS: inside the measured IQR and close enough to -14 that
  YouTube does not turn it down much. Short-term max ≤ -10 LUFS stays a default.
- **Confidence:** measured window; sourced ceiling and target direction.

### 9. Title quieter than the body
- **01/02:** title on a music hit; nothing about level. **06 (measured):** closing title at least 6 LU
  quieter than the body in 16 of 21 (median -8.9 LU); short class n=4 only -2.9 LU median (IQR -13 to
  +3.4). Attempt 1 was +1.3 LU (title louder).
- **Decision (hard):** the title section is at least 3 LU quieter than the body (the profile rule).
  Default aim -6 to -12 LU (-4 to -10 LU in 15 s): a sting on the title hit, then the music drops away.
- **Confidence:** measured across all classes; weak for 15 s (see "unsure").

### 10. Loudness arc
- **02 (inferred):** short-term loudness rises ≥ 6 LU from the first quarter to the loudest quarter;
  contrast ≥ 10 LU. **06 (measured):** the last quarter of the body is louder than the first in 16 of
  20 (median +3.7 LU; reveals +10.4).
- **Decision:** hard: last quarter of the body ≥ first quarter (profile rule). Default +2 to +6 LU.
  02's ≥ 6 LU and ≥ 10 LU contrast become suggestions.
- **Confidence:** measured.

### 11. Stop-downs
- **01/02/04 (sourced: Lieu, Violet Recording; lengths inferred):** a pause of 0.3-1.0 s before the
  biggest hit; 02 rations 1 per 15 s. **06 (measured):** 14 of 22 have at least one (≥ 0.3 s, ≥ 12 LU
  dip); 7 of 22 put one in the 4 s before the title; the 4 short spots have none.
- **Decision:** default one stop-down in 30 s and 60 s (soft lint); optional in 15 s, where a 0.3-0.5 s
  drop-out before the main hit is the usual form.
- **Confidence:** measured frequency; sourced purpose.

### 12. Where the biggest hit goes
- **02 (inferred):** 60-85 % of runtime, 50-70 % for 15 s. **06:** S5 says the last 10-20 % before
  the card, from one reference (Lethal V85 at 92 %); the biggest audio hit's position in the body is
  spread widely (mid median 35 % of body, IQR 25-78 %); the biggest hit sits within 3 frames of a
  cut in only 7 of 22.
- **Decision:** soft lint: the main hit falls in the last 40 % of the body. The hard part is conflict
  1: whatever its position, the payoff is on it.
- **Confidence:** inferred position; sourced pairing.

### 13. Camera moves on every shot
- **03 (sourced: Lieu on freecams, Nesky):** every shot has one behaviour from the move library and a
  purpose; no free drift; moves ease in and out. **06 (measured):** most gameplay shots are static or
  tracking game cameras (flat trailers static ≥ 50 % in 10 of 13); designed moves (push, pan,
  follow, orbit) median 11 %, reveals 21 %.
- **Decision:** these agree once "static" counts as a library move. Hard: every shot declares one move
  from the library and its purpose; moving cameras ease (or are damped); no unmotivated drift.
  Default: designed moves on 10-30 % of shots, spent on the opener, the reveal, the payoff and the
  title. Attempt 1's slow pushes everywhere are the failure this blocks.
- **Confidence:** measured share; sourced rule.

### 14. Close shots and the human face
- **04 (sourced: Kuleshov, Hitchcock; Lieu's Hades II note):** a hero with a readable face, a
  reaction shot near the reveal. **06 (measured):** close shots are rare but most trailers have one
  (median 5 %); attempt 1 had none and no face (F16).
- **Decision (hard):** at least one shot of MCU or tighter in the cut, and at least one shot where
  the hero survivor's face is readable (facing within 60° of the camera, MS or tighter). The 60°
  number is inferred.
- **Confidence:** measured presence; sourced purpose; inferred angle.

### 15. Distances assume a 1.7 m person
- **03:** every distance and camera height in the move library and scale recipes assumes a
  1.7-1.8 m survivor. Real: 0.94 m. **Decision:** multiply survivor-relative distances, heights and
  speeds by 0.55 (camera eye-level 0.3-0.9 m above the surface, OTS 0.3-0.8 m behind the shoulder,
  follow at 1.6-4.4 m, collision sphere 0.10-0.15 m, near clip ≤ 0.05 m). Tower- and hand-relative
  numbers are recomputed from their own sizes. Angles, speeds in degrees per second, shake and
  frame fractions do not scale.
- **New consequence, not in any report:** the hand (about 4 m) is only about 4 times a survivor and
  smaller than a block's 6 m length. It reads as giant only next to survivors at a similar depth, and
  through what it does to blocks. Forced perspective (survivor much nearer the camera than the hand)
  shrinks it. Rule (inferred, geometry): in the scale frame, keep the survivor's camera distance at
  least 0.6 x the hand's, so the on-screen ratio stays at about 2.5:1 or more. In a whole-tower wide
  the hand is about 5 % of frame height and does not read; frame the crown section (about 14 m tall)
  instead.
- **Confidence:** sourced scale facts; inferred geometry.

### 16. VR first person
- **01/03/04 (sourced: Gartner, Lieu, Road to VR):** never raw head-locked footage; prefer smoothed
  spectator or third-person cameras. **06 (measured):** 6 of 8 VR trailers with picture are mostly
  first person; 2 of 9 intercut live action of the player.
- **Decision:** these agree once "raw" and "smoothed" are separated. Hard: no raw head-locked view;
  any POV shot is smoothed (position 0.2-0.4 s, rotation 0.3-0.6 s) with hands kept high and slowed.
  Default for Deadly Jenga: third person carries scale (the selling point is the size difference), with
  smoothed POV for "you are there" beats (the survivor looking up, the hand reaching down). Live action
  of a headset player is a suggestion; the Studio cannot capture it.
- **Confidence:** measured that POV is normal; sourced that raw POV is not.

### 17. Hook timing in a short cut
- **04/05 (inferred from Lieu's "first third"):** hook in the first 2 s (05 L04) or 5 s (04).
  **06 (measured):** "what is this game" lands by 12 s in 15 of 21 (mid median 4.3 s, IQR 2.3-8.8);
  short spots median 2.0 s.
- **Decision (hard):** the core verb (the hand pulls a block and a survivor is affected, both in
  frame) is readable by 5 s in 15 s, 8 s in 30 s, 12 s in 60 s. A partial glimpse of the hook can
  open the cut at frame 0 (it doubles as first gameplay).
- **Confidence:** measured for 60 s; extrapolated for 15 s and 30 s.

### 18. Logos up front
- **01 (sourced):** no logos first. **06 (measured):** 4 of 22 open on a developer or platform logo,
  all from known studios or platforms. **Decision:** no logo before the closing title. Agreement.

### 19. Text inside the body
- **01 (sourced):** cards only for game-specific claims, a few plain words, title-safe.
  **06 (measured):** 11 of 22 use captions; gameplay ≥ 70 % of runtime in 16 of 22.
- **Decision:** default: a caption only when a rule cannot be read from picture in 2 s; at most 4
  words; on screen ≥ 0.3 s per word + 1.0 s (inferred reading time). Soft lint.

### 20. Hold the first appearance of the mechanic
- **01/04 (sourced: Lieu, Sheepherds; number inferred):** first appearance of a mechanic at least
  1.5-3 s, uncut. **06:** not measured (timing inside shots was not measured).
- **Decision (hard, inferred threshold):** the first shot that shows the pull is at least 1.5 s
  (45 frames at 30 fps) with its key moment at least 12 frames from each end. Attempt 1's unreadable
  pull (F17) is the reason it is hard.

### 21. Staged reveal
- **04 (sourced: Hitchcock-Truffaut, Jaws, Jurassic Park, Godzilla 2014, Attack on Titan; timings
  inferred):** effect before cause, sound before sight, reaction shot, partial then whole, whole by
  the midpoint in a short piece. **06:** not measured.
- **Decision (hard, as a structure check):** before the first full view of the threat, the plan has
  at least 0.7 s of anticipation (an effect, a partial view, a reaction or an audience-knows shot),
  and every effect shot has its cause in frame or in the shot before. Default: the full hand is seen
  by the midpoint. The recipe timings are defaults.

### 22. One "wow" frame per 15 s, comedy timing, hero near-miss
- **04 (inferred):** one wow per 15 s; setup-turn-hold-sound-cut comedy form; the hero survives a
  near miss. **Decision:** suggestions in the reveals reference. The hero near-miss is the default
  ending beat for hand concepts because it gives the face shot a payoff.

### 23. Miniature look and slow motion
- **03 (sourced practice, engine translation inferred):** falls in a too-small world look fast; slow
  by sqrt(scale). Deadly Jenga's blocks are metre-true (a 56 m tower falls in about 3.4 s), so real
  time already reads big. If viewers read survivors as 1.7 m people, falls look 1.35 x fast; 0.75 x on
  scale-shot falls is the suggested fix. **Decision:** suggestion; Lieu (sourced) warns that slow
  motion misrepresents play, so keep it to the payoff and in whole ratios (1/2, 1/4).

### 24. Process gates and the critic
- **05 (sourced: Lieu's garbage cut, previs practice, Huang et al. on self-correction, judge-bias
  literature; thresholds inferred):** gated pipeline, text animatic, scout stills, separate critic,
  planted defects, stop rules. Nothing in 06 bears on it. Attempt 1 spent about 3 of 5 renders on
  line-of-sight questions a scout answers in seconds (F10).
- **Decision:** keep the pipeline and the rule that nothing is rendered until the plan passes. The
  critic thresholds (accept at weighted ≥ 4.0, all categories ≥ 3; planted defects ≥ 2 of 3; round
  caps 3 + 2) are inferred defaults, used as stop rules because some stop rule is needed.

---

## What the evidence could not settle

1. **15 s montage rhythm.** No short montage was measured. The 15 s shape rules borrow the mid class
   (n=11). The first real 15 s cuts should be measured with `tools/` and the numbers revisited.
2. **Title loudness in very short cuts.** Short spots dropped only about 3 LU; mid trailers about 9.
   The hard floor (-3 LU) may be too weak or too strong at 15 s.
3. **Whether cuts follow musical phrases.** librosa beats on the full mix cannot see phrase-level or
   sound-design-led cutting; "not beat-locked" could hide that.
4. **What makes a trailer succeed.** 06 describes acclaimed trailers; no views or wishlists were used.
   None of these numbers proves a trailer will work.
5. **Shot sizes and moves are one rater's labels** from small stills; push vs zoom and orbit vs pan are
   uncertain.
6. **The hand's on-screen size.** The 4 m figure and the "hand visible height about 3 m" assumption
   need a measurement in the scene; every hand distance in the camera reference depends on it.
7. **Whether a VLM critic can judge Studio renders.** Untested (05 section 9). The planted-defect check is
   the only guard until the experiments in 05 section 6 run.
8. **Whether the Studio can show the hand in a playback render at all** (F5). Unknown cause; any hand
   concept is gated on it.

## Rules I was unsure whether to make hard

- Title ≥ 3 LU quieter (strong for 30-60 s, weak for 15 s).
- The 15 s title window (10.0-11.5 s, card 3.5-5.0 s): extrapolated.
- Longest-shot floors for 15 s (2.0 s) and 30 s (3.0 s): extrapolated.
- Barrage ≥ 4 cuts in 5 s for a 15 s montage: measured only for mid-length montages.
- First-pull hold 1.5 s and key moments ≥ 6 frames from the cut (readability thresholds: sourced
  rule, inferred numbers).
- Anticipation ≥ 0.7 s before the full reveal (sourced practice, inferred length).
- Facing within 60° for the face shot (inferred angle).
- Pull travel ≥ 2 m (one third of a block) as a scout gate (inferred from attempt 1's 0.7 m failing).

---

## Revision 2 (2026-11-20)

Trigger: skill test 1 (`SKILL-TEST-1.md`): the skill won 12 of 12 blind judgements, but the control
plans beat it on hook (4.25 vs 4.00) and clarity (4.33 vs 4.17), all four skill plans cloned the
worked example, planners bent shots to pass lints, and critics found copied frames that could not
contain their action. Also folded in: the hand-reveal pilot (`attempts/pilot-hand-reveal/PILOT-REPORT.md`
in the DeadlyJenga repo, branch `claude/trailer-hand-reveal-pilot`). "Sourced" now also covers skill
test 1's documented findings, as it already covered attempt 1's. No new measurement was made.

### Changes, with the test evidence behind each

| # | Change | Where | Evidence |
|---|--------|-------|----------|
| 1 | **Planning-only mode.** Target hits marked `unmeasured`; stage 2 deferred, not failed; deferred gates listed (stage 2 onsets, L11's measured clause, R01-R07, stages 5-11); engine items go to a "to verify at scout" list; one-file `plan.md` layout; a "before any render" list. Example C is written in this mode. | SKILL.md "Two modes"; `rhythm-and-sound.md` step 0b; lints L11, L19; `worked-examples.md` §2 | all four planners: no track, so hits were invented on a grid the skill forbids; no light path for a one-file plan |
| 2 | **Critic fallback.** When no agent may be spawned: a labelled SELF-REVIEW against the rubric, a defect sweep adapted from the planted-defect check, at least three problems, and a "critic owed" block handing the step to the orchestrator; gate "passed on self-review"; an independent critic still required before stage 8 in production. Planted-defect check recommended at stage 4 (it worked on text plans). | SKILL.md protocol steps 4-5; `review-rubric.md` §3a, §6 | all four planners self-reviewed because the harness forbade spawning; critics found 3 of 3 planted defects in both text copies |
| 3 | **Concept types** (`hand`, `disaster`, `two_role`) set the core verb (L05), the first hold (L09) and what BLOCKS (L19). Disaster verb: the hazard visibly moves blocks or survivors with scale, by the same deadlines. Two-role: both roles as players before half the body (new hard L36) and one event from both sides (soft L37). Must-show ids come from the brief with written proofs (L18), with a suggested vocabulary including `multiplayer`, `friends`, `pov_hand`, `pov_survivor`, `hazard_hit`. | SKILL.md "Concept types"; `structure-and-templates.md` §1; lints | brief c planner forced a hand into a "disaster is the star" brief; two graders noted brief b's multiplayer only implied |
| 4 | **Cold open allowed.** Anticipation (≥ 0.7 s) stays hard for every threat first shown after frame 0, including a second threat. Opening on the threat at frame 0 is a named choice (`opening: cold_open`): hold ≥ 1.0 s, then ≥ 2 escalate consequence beats with different `raises`. | hard rule 6; L08; `reveals-and-moments.md` rule 0 | brief c control's wave at frame 0 got hook 5 from all three graders; the skill's drain opening was called generic |
| 5 | **Break the template.** Two new fully worked examples (B: 15 s disaster single take, cold open, card ending; C: 30 s two-role montage, planning-only, POV ending), each linted by hand. Example A relabelled one example of three. Concept stage requires five image ideas and two rejected concepts that differ in two of shape, opening, payoff, ending. New hard L39: no example tagline, caption, hero or cast ids (camera tuples warn). New hard L38: a per-shot `fit` line deriving the frame (`Hf = d x 24 / f`) for what that shot must show, one hand only. | `worked-examples.md`; SKILL.md rules 11, 14; `camera.md` §1b; lints | all four plans shared the spine, the 80 m / 135 mm collapse, the one-hand hang, "Mint", and (3 of 4) the tagline; critics found copied frames that could not show their action and a shot with two hands |
| 6 | **Captions.** One plain rule caption by default for store and muted social cuts, ≤ 6 words; captions separated from tagline (≤ 8 words) and CTA (≤ 6) in L32 and the `titles.role` field. | SKILL.md defaults; L32; `structure-and-templates.md` §3 | control rule captions ("ONE PLAYER IS THE HAND") credited by all three graders for muted viewers; three planners asked how the 4-word cap applied to taglines |
| 7 | **15 s title window re-derived**: 11.0-12.0 s (73-80 %), title plus card 3.0-4.0 s, default 11.5 s and 3.5 s (L07: [330, 360], [90, 120]). Example A's title moved from 330 to 345 (payoff shot lengthened 72 → 87 frames); A's lint table recomputed. | hard rule 5; L07; `measured-reference.md` §4; beat sheet | both 15 s skill titles sat at 73 %, below the measured band, and graders did not reward it; control titles at 80-88 % |
| 8 | **Clarified rules.** L21 now compares escalate shots with hook + teach shots (payoff, aftermath excluded). L34 exempts the core verb (repeats must escalate). `hook_count` = featured appearances. Pull vs grip defined; L08 accepts a grip as cause. POV size by subject fraction, yaw null, L16 skips POV pairs, POV may be 0.5-1.0 s in a barrage if held earlier. Lead-in sounds separated from the key's sound (L12). Stop-down vs breath. A survivor at h 0.08-0.20 is WS. | lints; `camera.md` §1c; `rhythm-and-sound.md` | planner feedback: L21 rounding and punishing a held payoff (a, c); L34 relabelling (b, d); undefined POV sizes (a, b, d); pull vs grip (d); sound-before-sight "contradiction" (d); stop-down placement (b, d) |
| 9 | **No gaming rule** (hard rule 14): never relabel, drop a true label, shorten a beat or swap a cause to pass; waive in writing; report a hard rule that fights the concept. | SKILL.md; lints preamble; rubric C2, C10 | planners admitted relabelling `shows` (b, d), changing a cause "for the lint" (b), a 0.7 s near miss to pass L21 (c) |
| 10 | **Can / cannot list** with unknowns marked: one hand; ledge hang, floating, emotes, voice in a take, hand-player eye position unknown; sea drawback not in the tsunami code; the game's 16 hazards listed (from `World/Hazards`), tsunami parameters (height 1-100 m, warning time, offshore distance, speed). | SKILL.md | planners guessed every one of these; one plan showed two hands |
| 11 | **Critic calibration.** "Cannot check (format)" for fields a plan lacks, never a hard fail; critics compute geometry and capability themselves; an errors-found list precedes the top three; the 2.8-3.0 vs about 4.5 gap explained (read errors first, compare scores only within one critic set-up). | `review-rubric.md` §3; SKILL.md step 6 | d-clean critic hard-failed three format-caused lints while c-clean said "cannot check"; critics 2.78-2.95 vs graders 4.48-4.59 on the same plans |
| 12 | **Room for invention.** An explicit idea step (five or more memorable-image ideas from different sources, then choose); "rules constrain craft, not the idea" stated in SKILL.md and the concept stage. | `structure-and-templates.md` §1 step 5; SKILL.md intro | the inventive moments (lobby role swap, fingertip flick on the title hit, survivor holding a toppling block) were all in control plans |
| + | **Studio tooling** updated with the pilot: entry points (`RunPreview`, `RunFinal`, `RunChecks`), directed hand cue, camera targets, move library, framing metrics, filmstrip, camera-spot search, the `ActorFrame.Blend` fix, the branch, and what is missing (no sound cues; directed hand silent and not live while recording; world physics not repeatable; one bad frame silently stops an actor; long editor start-up). | `studio-tooling.md` | pilot report |
| + | **Pilot craft lessons** in the mistakes tables: hero too small to read a reaction (0.17 of frame); payoff exiting toward the lens; untextured-looking hand; no closing frame with hand and tower; hand, hero and lens in one line. | SKILL.md; `camera.md` §7; `reveals-and-moments.md` | pilot report |

New inferred thresholds (defaults resting on sourced rules): cold-open hold 1.0 s; caption ≤ 6
words, tagline ≤ 8, CTA ≤ 6; two-role views before half the body; a barrage POV 0.5-1.0 s.

### Chosen not to change, and why

- **The anticipation rule for mid-trailer reveals, the 1.5 s first hold, the 2 m pull, the shape
  rules, the mix rules, the human pass.** These carried the categories where the skill won most
  (structure +1.58, reveal +1.58, camera +1.92, sound +1.08); nothing in the test argues against them.
- **The 30 s and 60 s title windows.** Both test 30 s skill plans sat at 83 %, inside the band.
- **The critic thresholds** (stage 4 Hook, Clarity, Story ≥ 3; rough cut 3.5; fine cut 4.0). They
  are stop rules for one critic set-up. Lowering them to match editor scores would mix scales; the
  fix is how agents read the score (change 11).
- **The planted-defect check stays optional at stage 4.** It worked on text, but it costs a second
  critic run; required from stage 7 as before.
- **No plan-to-YAML lint runner.** Recommended by the test (12); it is code, outside this file-only
  revision. The lints file now suggests computing the arithmetic lints with a few lines of Python.
- **L21's definition departs from the measurement.** Report 06 compared time quarters; the skill now
  compares sections so a held payoff is not penalised (the planners' complaint). It stays soft.
- **"One hand" is stated from the game description and the critics' reading.** The code has
  left/right hand objects with one chosen to fly (`useLeftHand`), so "one giant hand on screen" was
  not proven in the engine; I listed it as a rule and the hand player's other hand as unknown.
- **Nothing in the can/cannot list was checked in the engine** (no Unity in this revision): entries
  come from the code, the pilot and attempt 1, and unknowns are marked unknown.
- **The hero-near-miss default** stays in the recipes but is now one ending among several, because
  all four test plans used it.

---

## Revision 3 (2026-11-20)

Triggers: (1) the owner's direction the same day: the flying hand "is like not an important part of
our game at all anymore. i need you to have one focused on epic disaster pvp mayhem"; (2) the
hand-reveal-2 field test (`attempts/hand-reveal-2/REPORT.md` in the DeadlyJenga repo, section
"Feedback on the skill", nine fixes). No Unity was run for this revision. Game facts were read from
the central checkout's code (`Evan_Sprint_4`, HEAD `9342eff8`, read only) and the Studio's code on
`claude/trailer-hand-reveal-pilot` (HEAD `164e7115`, read only, another agent working there).
Nothing new was measured in trailers; "estimate" marks every new number.

### Changes

| # | Change | Where | Measured / sourced / estimated / untested |
|---|--------|-------|-------------------------------------------|
| 1 | **Re-centred on disaster-PvP mayhem.** New default concept type `mayhem` (core verb: the first of `hazard_hit` and `pvp_hit`), plus `disaster` and `pvp`; `hand` and `two_role` demoted to legacy. New pitch line, must-show ids (`pvp_hit`, `knock_off`, `elimination`, `weapon`, `several_survivors`, `escalation`), recipes, sketches, moments and mistakes rows. Frontmatter no longer names the hand first. | SKILL.md; `structure-and-templates.md`; `reveals-and-moments.md`; `camera.md` | sourced: the owner's direction; game facts from code (row 2) |
| 2 | **What the game can show**, from the code with file references: 16 disasters and their settings, 21 weapons and 3 powers, punch rules by mode, bots and monsters, drowning elimination, the candy-burst "OUT!", modes, `GameEvents`, maps; and what the Studio can cue for each. Findings that changed the skill: the flying hand is a 30 s power-up; AI hand bots are uncapped (so "one hand" is a Studio fact, not a game fact); **the tsunami's height is zero during its warning** (revision 2 said it rose in place); the Studio's `tsunami` cue never pushes survivors (`affectPlayers = false`); the stage runs Demo with deaths off; no punch cue; the tornado's settable side exists only on the pilot branch. | `references/game-content.md` (new); SKILL.md can/cannot table | sourced (code read); **untested in the engine**: every "unconfirmed" row |
| 3 | **Readable-mayhem rules and lints.** Hard: L40 (mayhem shows both halves by 60 % of the body), L41 (every hit names attacker and victim, both readable), L42 (a hero in the opening, build and payoff). Soft: L43 (disasters escalate one per beat), L44 (geography: an early wide with the structure and hero; `tower_level`), L45 (crowd shots: ≥ 3 survivors at h ≥ 0.08), L46 (a knock-off held 0.6 s or picked up next shot). New hard rule 16. Rubric C2, C3, C4, C6 and their anchors reworked; mayhem defects added to the self-review sweep and planted-defect recipes. | SKILL.md; `plan-schema-and-lints.md`; `review-rubric.md`; `reveals-and-moments.md` §2 | sourced: report 01 rules 20 and 40, Salters, Lieu, Cauller; the 0.6, 0.6 s, h 0.08 crowd line and "3 survivors" are **estimates** (0.08 is the measured EWS size line reused) |
| 4 | **Two disaster-PvP worked examples**: D (15 s montage, staged audience-knows opening, title over picture) and E (30 s single take, cold open on meteors, planning-only, smash cut to the card after a betrayal). B kept. A and C moved to `legacy-hand.md`. Reserved ids, taglines and captions extended; twists marked spent. | `worked-examples.md`; `legacy-hand.md` | linted by `tools/lint.py` (0 hard failures; the linter caught a real jump cut in D's first draft); geometry derived on the assumed castle map; **untested in the engine** |
| 5 | **Fix 1: class `moment`** (5-10 s): no title needed, the whole piece is the body, payoff ≥ 50 %; L03 pattern with optional teach, escalate and title; L07 and L11's title clause n/a; L28 at 0.5; moment shape limits; montage-only soft lints n/a. | SKILL.md defaults and rules 2, 3, 5; lints | sourced: hand-reveal-2 needed four waivers; tested: its `v08/plan.yaml` linted as `moment` needs none of them; the moment shape numbers are **estimates** |
| 6 | **Fix 2: the hand's drawn size is 2.4 x 2.3 x 1.8 m.** Every hand framing number re-derived for 2.3 m (FS 3.1-4.6 m frames; a 14 m crown frame leaves the hand at 0.16; whole tower 2.5-3.7 %); the 0.6 x distance rule (from conflict 15) replaced by ≥ 0.8 x (ratio ≥ 2.0); examples A and C re-derived (A's S03 hero 3.5 → 4.5 m; C's S04 Gus 4.5 → 6.0 m). | `legacy-hand.md`; `camera.md` | measured: the 2.4 x 2.3 x 1.8 box (hand-reveal-2); the 0.8 is an **estimate** from geometry |
| 7 | **Fix 3: no drawn face.** L14 and rubric C4 now ask for a reaction that reads through head direction, body and hand pose, named in a new `reaction` field (Studio layers `scream`, `cower`, `cheer`, `knockback`..., an emote or a performed pose). | rule 9; L14; C4; `reveals-and-moments.md` rule 13 | sourced: hand-reveal-2 critics; reaction list from `StudioOverrides.cs:19` |
| 8 | **Fix 4: the white hero is URP motion blur, not bloom**; another agent is fixing it; check with a with/without colour still. `StudioMark.color` is a palette index (index 6 rendered yellow): confirm colours in a still. | `studio-tooling.md`; SKILL.md self-review table | measured: hand-reveal-2's `v08/colour-check.png`; the pilot's washed-out hero is **likely** the same cause, not proven |
| 9 | **Fix 5: riding a tilting block** sinks a survivor unless he stands at its middle (0.36 m off-centre, 0.27 m slide at the middle). | SKILL.md table; `game-content.md`; `studio-tooling.md` | measured in hand-reveal-2 |
| 10 | **Fix 6: capped critic budget**: keep stage 7 (preview) and stage 9 (fine cut), each with the planted-defect check; stage 4 and 8 as labelled self-reviews; with one run, stage 7. | SKILL.md pipeline rules; `review-rubric.md` §6b | sourced: what hand-reveal-2 did (3 of 3 planted defects found twice); the one-run choice is **inferred** |
| 11 | **Fix 7: the scout loop** is a pipeline stage (5b): a new plan version, every lint re-run, the stage-4 review re-run when keys move > 6 frames, a shot falls back or a label changes; loop until a scout changes nothing that matters. | SKILL.md pipeline | sourced: hand-reveal-2's scout changed the design, the spot and every camera |
| 12 | **Fix 8: shipped tools** in `tools/`: `lint.py` rewritten for revision 3 (all L-lints, moments, classes, concept types); `music_map.py` (`--fps`, `--lag`); `assemble.py` (no hard-wired worktree, tape offset, fps or loudness; sound root by flag, config or env; per-lane sync); `pack.py` (planted defects chosen on the command line); `sound_events.example.json`. | `tools/`; `studio-tooling.md` §7 | tested: every `--help`; `lint.py` on A-E and on hand-reveal-2's plan; `music_map`, `assemble` (preview) and `pack --plant` end to end on a synthetic job. **Untested**: a 1080p assembly, real Studio job output, real game sounds, a real metrics CSV |
| 13 | **Fix 9: sync lane by lane.** `assemble.py`'s sync table has a full-mix and an own-lane column and marks voice lines NEEDS EARS; rule 15, R03 and the rubric say to trust the lane column and send voices to the human. | SKILL.md rule 15; `rhythm-and-sound.md` step 7; R03 note; rubric pack item 8 | sourced: hand-reveal-2's +17.3-frame false reading; tested on synthetic sound (+9.4 mix vs -0.5 lane) |
| + | **Other tooling findings recorded**: the directed hand's fixed ease (most flight in the first 20 %; the yank's motion peak 15-25 frames after the hit); no dust or debris at a directed yank; no Studio sound cues (recorded game sound events do replay); no live view (each look costs a job); takes differ slightly (finals A and B; the hero within about 5 cm); hand-reveal-2's added tooling (`carryTime`, `BakeLook`, per-shot grade, the multi-shot driver). | `studio-tooling.md` §2, §3, §6 | measured or observed in hand-reveal-2 |

### Chosen not to change, and why

- **The gated pipeline, planning-only mode, the waiver rule and the planted-defect protocol** stay
  as they were; the field test credited all four.
- **The measured numbers** (title windows, shape rules, mix targets, first gameplay, the 1.5 s first
  hold) are unchanged; the party / physics column of the profile is now pointed to for mayhem cuts.
- **Hard lints still cannot be waived away.** `lint.py` prints a waived hard lint as WAIVED-HARD and
  the skill still says to report it to the human; the moment class removes the need in the common case.
- **"One giant hand"** is no longer stated as a game rule (AI hand bots are uncapped in code); a
  second hand needs `verify`.
- **No new trailer measurement.** No mayhem trailer was annotated for "who did what to whom"; the
  readable-chaos numbers stay estimates until one is.

### Unsure

- Whether L40's 60 % and L46's 0.6 s are the right lines; whether three readable survivors is the
  right crowd floor.
- Whether a drowning elimination (and its candy burst) fires for actors on the Studio stage, where
  deaths are off (`MatchModeRuntime.World.cs:209` does not check the rule).
- Whether the `fire` cue swings melee weapons (glove, dagger, bat) or only fires ranged ones.
- Whether three disasters run together in one Studio take.
- Example D and E camera numbers assume the attempt-1 castle (the wall walk at 25 m); neither was
  scouted.

---

## Revision 4 (2026-11-20)

Triggers: the owner's three questions the same day: (1) "why dont the trained agents etc include any
first person stuff and why is overly-reliant on distant shots esp the super distant ones during
important events when a much closer one with a good angtle would be so much more immersive?";
(2) whether agents record one real physics event, then make every scene follow it, and perform each
actor one at a time against those already recorded, with a first-person view that shows the player's
hands and weapon; (3) "can you ensure everyone including anyone looking at the skill knows to be
doing these iterative things etc can how to do them". Field reports read: `attempts/disaster-pvp-1/REPORT.md`
and `attempts/hand-reveal-2/REPORT.md` (DeadlyJenga repo). Studio code read only, on
`claude/trailer-hand-reveal-pilot` at `9e099413` (another agent is editing its first-person camera).
No Unity was run. Every new number is an estimate.

### What the code says (verified by reading, file references in `TrailerStudio/`)

- Record once, play back with no physics: `StudioRun.RecordWholeSet` (`Engine/StudioRunPass.cs:41-48`)
  records every unlocked track live on a fixed clock; playback and renders pose blocks kinematically
  (`SetWorldKinematic`, line 254).
- Cues run only in live-world passes (`StudioRunPass.cs:435`; `TrailerStudio.md`).
- Who is live: marks in `pass.record` other than the performer; `Survivor` marks are brainless
  stand-ins, `Bot` marks run the bot brain (`Engine/StudioCast.cs:43-79`). Played-back actors are
  posed from the tape and do not react to hits; a played-back disaster does not push a live performer.
- Performing (`Engine/StudioPerform.cs`): countdown, pre-roll, ½ and ¼ speed, loop, punch-in (and
  back 3 s), floor paths of the others and your last take (`Engine/StudioPaths.cs`, only while a
  person performs), ghosts. Live physics records the performer plus the world from a fresh map and
  nobody else (`StudioPerform.cs:56-63`); older actor takes are badged stale (`StudioTakes.MarkStale`).
  A driver can build a pass with a performer and other live actors (`StudioRun.Record` accepts any
  `StudioPass`); **untested**.
- Branches keep the tape before a frame and re-simulate after it (`StudioRunPass.cs:344-357`).
- Overlays (`Engine/StudioOverrides.cs`): look-at, reach, pose, nine reactions, emote, outfit, colour
  flash, weapon, hit pause; targets `actor:`, `block:`, `camera:`, `hazard` or an empty target with
  `point`; triggers `contact` (blocks over 1.5 m/s within 1 m of the chest) and `cue:<id>` (cue time,
  or a disaster's centre within `max(4, strength x 8)` m), resolved by `StudioOverrides.Resolve`.
  Cameras, including first person, read actors with their overlays (`Engine/StudioCameras.cs:33-40`).
- **Found while reading:** an overlay with `target: "point:..."` is silently ignored (`Target` has no
  `point:` case); disaster-pvp-1's "everyone looks up at the planet killer" layers used it
  (`Attempts/DisasterPvpMoment.Direction.cs:287-294`), so those look-ups never happened.
- Motion edit (`Engine/StudioMotionEdit.cs`): trim, slide, retime, smooth, offset, bend, blend takes,
  lock feet; `StudioTakes`: comp, clone, crowd. Every edit is a new take.
- New cues from disaster-pvp-1: `glovePunch` (`Engine/StudioGlovePunchCue.cs`) and `aimedMeteor`
  (`Engine/StudioAimedMeteorCue.cs`). The motion-blur ghosting fix `StudioRender.NoSkinnedVectors`
  (`Engine/StudioRender.cs:201, 276`).
- disaster-pvp-1's driver saved and loaded one bundle for every job (`DisasterPvpMoment.SaveBundle`,
  `LoadBundle`), wrote a take report (`DisasterPvpMoment.Scout.cs:42-120`), and placed victim cameras
  6-8 m out at 20-24 mm from the victims' *pre-hit* positions (`DisasterPvpMoment.Shots.cs`, `Victim`).
- `firstPerson` today: camera at the recorded head with its rotation, no smoothing
  (`StudioCameras.cs:186-188`).

### Changes

| # | Change | Where | Verified / estimated / untested |
|---|--------|-------|---------------------------------|
| 1 | **Record once, then direct against the tape** as hard rule 17 and a numbered procedure with the real method names (record, report, re-time, commit the bundle, place cameras from the take, load the bundle in every later job, a folder per job mode), when to re-record and what that invalidates, branches as the cheaper re-record. Placed in the frontmatter, a "Start here" section at the top of SKILL.md, and the new `references/record-and-layer.md`. | SKILL.md; `record-and-layer.md` §2-3; `studio-tooling.md`; pipeline stages 5 and 6 | verified in code (above); the loop is disaster-pvp-1's "record, scout the take, re-plan" |
| 2 | **Layered actors**: the pass order (world, causes, victims, witnesses, crowd); what a person can do (Perform options) and what an agent can do alone (cues in one live pass, overlays timed from the take report, motion edit, triggers); the rule that everyone who touches anyone is live in one pass; a `layers` plan field and soft lint L51. | `record-and-layer.md` §4; schema; examples | verified in code; the driver-built performer-plus-live-actors pass is **untested** |
| 3 | **Tooling gaps for agent-performed actors**, each with the smallest feature: `move` cue; unarmed `punch` cue; `hit` trigger from recorded health drops; contact triggers for hazard points and actors; a Studio take report; `point:` overlay targets; scripted performer input; a `hide` overlay or "no respawn"; keep chosen actors live while performing; first-person fix (in progress); playback colours. | `record-and-layer.md` §6; `studio-tooling.md` §6 | from the code; none built |
| 4 | **Stage facts** from disaster-pvp-1: respawn after the sea (deaths off), every body playing back yellow, left-over impact visuals, the rocket's 8 m blast, block visuals on the Default layer, a fixed summit rule, rock paths 4 m from the hero, bots walking off, one job deleting another's output, plus the `point:` overlay bug found in this revision. | `record-and-layer.md` §5 | sourced: the report; `StudioRun.cs:223` (deaths off) and the code paths named; the colour cause is **not traced** |
| 5 | **Closeness as a hard rule** (rule 18, L47): every key event (hit, knock-off, elimination, fall into the sea, disaster hit on a named survivor, the payoff) has a shot on screen at its frame with the affected survivor or impact at h ≥ 0.25, or a first-person view; wides are for geography and scale and never the only coverage. Render twin R08 from `metrics.csv`. Distances for 0.25 in `camera.md` §1d. | SKILL.md; `camera.md` §1d; lints; rubric | 0.25 is an **estimate** (the bottom of the fight two-shot band; disaster-pvp-1 victims at 0.07-0.15 were judged too small) |
| 6 | **Camera derived from the tape** (L48): `key_events` in the header and a `take_ref` {event, take_frame, subject_h, source} on every key beat; once the take is committed, `source` must be `tape` or `metrics`. | schema; lints; examples | the owner's direction; the field shape is new and **untested on a real plan** |
| 7 | **First person required** (L49): a montage has ≥ 25 % of body frames in POV in 2+ shots, one on a key event; a single take or moment has a POV of 1 s or more on a key event; L50 (soft) caps POV at 60 % unless `pov_led`. Revision 3's "under half" and "third person carries the scale" are replaced. | `camera.md` §6; SKILL.md defaults and rule 18; lints | measured: 6 of 8 VR trailers mostly first person (VR first-person share 0.67); 25 % and 60 % are **estimates** |
| 8 | **First-person how-to left to the other agent**: a marked placeholder ("first-person camera fix in progress, see commit note") in `camera.md` §1c and the `pov` row of `studio-tooling.md` §4. Until it lands: `pov_of`, smoothing, a `verify` item and an over-the-shoulder fallback 0.3-0.5 m behind the head. | `camera.md`; `studio-tooling.md` | the raw head-locked view is verified in code; the fix is not mine |
| 9 | **Rubric**: new category C11 "Immersion and closeness" (weight 1.0; weighted mean now / 11.0), evidence item 11 (the motion around every key event: the clip or a strip of every 2nd frame from -10 to +20, with an ffmpeg command), critic step 5c, closeness defects in the self-review sweep and planted-defect lists, two human-pass questions. | `review-rubric.md` | the weight is **inferred**; `pack.py` does not build item 11 yet |
| 10 | **Mistakes rows**: key hit from far away; no first person; cast as props; two halves from two takes; cameras from pre-hit marks; the `point:` overlay. Some Studio-specific rows folded into one row pointing at the can/cannot table to keep SKILL.md near 400 lines. | SKILL.md; `camera.md` §7; `reveals-and-moments.md` checklist | - |
| 11 | **`tools/lint.py`**: L47-L51 added (hard for mayhem, disaster, pvp; warnings for legacy hand and two_role), L06's 15 s longest-shot floor lowered from 60 to 45 frames. | `tools/lint.py`; schema | tested (below) |
| 12 | **Worked examples rewritten** so they pass and really use close and first-person coverage: D (montage) now sees the glove launch from Brick's eyes, the payoff from Jolt's, and the splash from the waterline (26 % POV); E's follow closes to 3.2 m on Kestrel for the payoff and the betrayal is Tansy's view; B became a first-person single take (`pov_led`), with revision 3's 15 m pull-out kept as its third rejected concept. All three gained `key_events`, `take_ref`, `layers`. | `worked-examples.md` | linted; geometry derived on the assumed castle map; **not scouted** |
| 13 | **L06 at 15 s: 45 frames, not 60.** The 60-frame floor was extrapolated, not measured (15 s references are single takes); L09 already forces one 45-frame hold of the first core verb; disaster-pvp-1 reported the 60 forced a restructure (it dropped S02 to stretch S01 to 64 frames). The 30 s and 60 s floors are unchanged. | SKILL.md rule 3; lints | decision; the 45 is the existing L09 number reused |

### Tests run (no Unity)

- `python tools/lint.py` on D, E and B as revised: 0 hard failures, no soft warning (n/a where a lint
  does not apply). On their revision-3 versions: each failed L47, L48 and L49 (L48 and L47 for the
  missing fields first).
- Planted variants of D and B: D's payoff turned back into a far wide (h 0.11) fails L47 and L49; D with
  `take_status: committed` and `fit` sources fails L48; D with no POV fails L49 only (its key events are
  still close); D with a key event's only `take_ref` moved to a shot not on screen at the event fails
  L47 and L48; D without Dune's layer warns L51; B as revision 3's pull-out (h 0.063, no POV) fails L47
  and L49.
- A and C (legacy) print L47-L51 as soft warnings and still have 0 hard failures.

### Chosen not to change, and why

- **The gated pipeline, planning-only mode, the waiver rule and the critic protocol** stay; the
  record-once loop sits inside stages 5-6 rather than adding a stage.
- **L29's wide share (≥ 40 %)** stays: a WS fight two-shot at h 0.25-0.45 counts as wide and as close
  coverage, so the two rules do not fight (D passes both at 89 % wide).
- **`pack.py`** is unchanged; the key-event strip is made with ffmpeg until it is added.
- **The legacy examples A and C** are not rewritten (they warn); the hand is not the default.

### Unsure

- Whether 0.25 is the right closeness floor and 25 % the right first-person floor; both should be
  checked against a measured VR or party trailer for key-event sizes.
- Whether a driver-built pass with a performer and other live actors works as the code suggests.
- Whether the fixed first-person camera will show hands and weapons for played-back actors (the other
  agent's work), which every POV in the examples depends on (each has a fallback).
- Why every body played back yellow in disaster-pvp-1.
