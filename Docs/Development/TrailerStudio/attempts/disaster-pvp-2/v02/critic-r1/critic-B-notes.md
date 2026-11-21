# Critic B notes: Disaster PvP 2, v02, stage-7 preview (640x360)

Method: review-rubric.md sections 3-6; capabilities from game-content.md section 9. Sound is judged only from metrics.txt. I did not open `planted.txt`. take-log.md is empty (0 bytes) in both packs.

## 0. What differs between the packs (byte comparison)

brief, concept, plan, lint, motion.csv, framing.md, take-log, contact, filmstrip, stills-10 and all ten strips are byte-identical. Only these differ:
- metrics.txt sync table: pack-1 `punch2 218 (no onset within 10 frames)` and `main 309 mix +5.0 lane +5.0`; pack-2 `punch2 218 mix +0.1 lane +0.0` and `main 309 mix -0.3 lane +0.0`.
- stills/S06-key.png: pack-1 shows the hero alone on the block holding his glove, no dragon. Pack-2 shows the dragon in the air at the hero's outstretched glove.

Pack-1's S06 key contradicts its own framing.md (dragon on, h 0.41 at key) and its own S06 strip (contact in tiles 3-4). Its sync rows contradict L11 in the shared lint.txt ("every hit measured within 2 frames: True"). **Pack-1 is the one with injected defects. Pack-2 is the real pack.**

Injected defects in pack-1 (3 of 3 found):
1. **Payoff cut moved 5 frames off its hit:** `main 309 mix +5.0 lane +5.0`. The planet-killer impact sound lands 5 frames after the picture.
2. **Impact sound removed:** `punch2 218 (no onset within 10 frames)`. K3, the counter-punch on the dragon, is silent.
3. **Key still replaced by a frame where the subject is gone:** S06-key.png has the hero alone, with no victim, at K3's contact frame (f218).

No revision-5 plant (frozen actor or synced starts) is present: motion.csv is identical in both packs, and its longest still run is 15 frames.

---

## 1. Shared checks (true of both packs; the evidence is identical)

### 1a. Framing arithmetic per shot (Hf = d x 24 / f; POV Hf = 2 d tan 34 = 1.35 d)

| Shot | Plan | Computed | Shows label true of the stills? | Notes |
|---|---|---|---|---|
| S01 | 4.0 m, 28 mm | 3.43 m, 6.1 m wide | several_survivors: three readable (h 0.33-0.38 at key). **Weak:** all three start acting at f7-9 (motion.csv), so they are not clearly "acting independently". | Implied survivor height about 1.2 m. The three stand about 2.5 m apart in a line across the frame (x 0.10, 0.53, 0.88). That reads as a line-up, not a brawl. The rival sits at the right edge (x 0.92 at last). |
| S02 POV | 1.4 m (fit), 1.6 m (camera block) | 1.89 m | pvp_hit and knock_off: true. Own arm in frame, gorilla h 0.48 at contact, gorilla flies (strip tiles 5-8). | The plan's distance fields disagree (1.6 vs 1.4). h 0.48 implies the gorilla is about 1.9 m away. The fit says "eyes follow him out over the sea". **False:** the view pans down to the floor and the glove at 103 deg/s (framing last row; strip tiles 9-10), and the splash is never seen. |
| S03 | 12.1 m, 31 mm | 9.37 m, 16.7 m wide | hazard_hit: **false.** No block or survivor is hit inside the shot. A rock trail skims past the summit at f15-29 and nothing moves. several_survivors: true for five. The astro is at the bottom edge (x 0.08, y 0.04, h 0.07 at first and last), so "six readable" is overstated. | The BlockSmash sfx at f12 (122) plays with no visible impact. |
| S04 | 3.5 m, 25 mm | 3.36 m | hazard_hit: true but muddy. The penguin (h 0.33) is behind a row of eight identical rock spheres at the key (S04-key). | The fit says the rock "comes in low from frame right ... throws the penguin out of frame left". **False:** the rock arrives as a diagonal trail from top right plus a horizontal chain of copies. The penguin vanishes into dust; he is visible for only 20 of 40 frames and is never seen exiting left. |
| S05 POV | 2.6 m (fit), 2.5 m (camera) | 3.51 m | shows []. | The dragon is h 0.43, then 0.47, then 0.63, so he is nearer than 2.6 m (about 1.9 m at key). "Charges" is false: he walks (strip tiles 2-10). The plan says the hero "turns his head from the fallen fox"; it was the penguin that fell. |
| S06 | 3.1 m, 22 mm | 3.38 m | pvp_hit and knock_off: true in pack-2 (dragon h 0.41 at the glove). **False in pack-1**: the key still has no dragon. | The fit's "hero 0.47" is the bounding box inflated by the stretched arm. In the still the hero's body is about 0.24 of frame height (px 150-238 of 360), so he stands about 4.5-5 m from the lens, not 3.1 m. The fit says the arm crosses a "1 m gap"; in strip tile 4 it extends about 2+ m, past where the dragon was. |
| S07 | 1.6 m, 50 mm | 0.77 m, MS | survivors_react: true. The head tips back past the lens and both hands go to the head (strip tiles 5-8). "Screams" cannot read, because there is no face. | Fits. |
| S08 POV | 40 m, rock 45 m | 1.35 d | hazard_hit: true. The rock fills the frame by tile 6. | The fit says "the lowered glove at the bottom edge". **False at cut-in:** the glove covers the right half of the frame at f0 (strip tile 1). |
| S09 | 52 m, 20 mm | 62.4 m | shows []. | The fit says "the top 30 m of the tower". **False:** the whole tower down to the green and red castle base is in frame (strip tiles 1-10). |
| S10 | 48 m, 20 mm | 57.6 m | shows []. | The fit says "the title sits in the sky above". **False:** a high-angle shot of the wreck and sea, with no sky; the title sits over the water. |

### 1b. Every hit: attacker, victim, readable?
- **K1, S02 f28 (76).** Attacker: the hero, POV own arm, yellow. Victim: the gorilla, h 0.48. Both read. Faults: the arm renders as a long, thin stretched yellow beam (tile 5). After contact the huge glove swings across the lens and covers about 40% of the frame while the gorilla flies (tile 6).
- **K2, S04 f14 (154).** Attacker: a meteor. Victim: the penguin, h 0.33. The attacker reads only as a brown "caterpillar" of duplicated rocks. The victim is partly hidden behind the sphere row at contact and disappears in dust. It needs a second look.
- **K3, S06 f10 (218).** Attacker: the hero, body about 0.24. Victim: the dragon, h 0.41, side-on. Both read in pack-2; the victim is absent from pack-1's key still. The dragon leaves the frame up-left within about 4-6 frames. Then 28 frames of the hero alone, small, in a frame that is 40% block (strip tiles 5-10).
- **K4, S08 f25 (309).** Attacker: the planet killer, filling the frame. Victim: the hero, POV, hands at the bottom edge (tiles 3, 8-10). Reads.

### 1c. Hero in each section
- Hook S01: centre, h 0.36.
- Teach S02 (POV arm) and S03 (h 0.12). A second yellow survivor stands mid-frame in S03 (x 0.47, the panda by framing), which weakens identification by colour.
- Escalate:
  - S04: h 0.10, occlusion 0.22 at key and 0.44 at last, behind the fox's tail.
  - S05: POV.
  - S06: about 0.24.
  - S07: MS.
- Payoff S08: POV.
- Aftermath S09: h 0.01-0.02, occlusion 1.00 at key (not visible).

### 1d. Geography
S01, S03 and S09 establish the summit over the sea. **Line crossed between S03 and S04:**
- S03 order, left to right: penguin x 0.10, fox 0.28, dragon 0.60, hero 0.81.
- S04 order: dragon 0.05, hero 0.19, fox 0.22, penguin 0.62.

The block-2 pair and the summit pair both reverse sides across the cut.

### 1e. Closeness and first person (pack item 11)

| Key | Coverage | Victim h | Close or POV |
|---|---|---|---|
| K1 | S02 POV | 0.48 | POV ✓ |
| K2 | S04 static, side-on | 0.33 (partly occluded) | close ✓ (muddy) |
| K3 | S06 static, side-on WS | dragon 0.41, hero body about 0.24 | close by the victim ✓; pack-1 key still: victim 0 ✗ |
| K4 | S08 POV | 1.0 (rock) | POV ✓ |

- **First-person share:** S02 62 + S05 28 + S08 34 = 124 of 359 body frames = 34.5% (floor 25%), on K1 and K4.
- **Cast who never visibly react:**
  - The panda: his K2+11 flinch and his look-up are off screen (S04 occlusion 1.00; S08 on screen for 1 frame; S09 h 0.01).
  - The astro: his K2+19 look is off screen (S04 occlusion 1.00); in S03 he is cut at the bottom edge.
  - The rival never reacts to anything before he is hit (he feints, then is punched).

### 1f. Performance (pack item 12, motion.csv)
- **Still or level-headed over 30 frames:** none. The maxima are still 15 (S01 hero) and level 22 (S05 hero).
- **Pairs starting within 4 frames.** Starts at shot frame 1 are cut-in artefacts and are listed only for completeness.
  - S01: hero 9, rival 9, dragon 7. **All three fighters start within 2 frames, in the hook.**
  - S01: rival 32 and dragon 30.
  - S03: panda 25 and astro 26.
  - S09: panda 4 against hero, fox and astro at 1.
  - Shot-start coincidences: S02 hero and rival at 1; S03 hero, fox, penguin and astro at 1, dragon at 3; S04 hero, fox and penguin at 1, dragon at 2; S05 hero and dragon at 1; S06 hero and dragon at 1; S08 hero and fox at 1; S10 all three at 1.
- **Snap turns (over about 400 deg/s, not thrown):**
  - **S07 hero 887 deg/s at f13 (trailer 261).** This is the hero's reaction close-up. The head flips from forward to fully back between strip tiles 4 and 5, with no impact.
  - **S04 fox 634 deg/s at f29.** That is 15 frames after the rock, a look-round that snaps.

  These are thrown and so excluded: S02 rival 717 at f29 (K1 at f28), S04 penguin 840 at f15 (K2 at f14), S06 dragon 643 at f11 (K3 at f10).
- **Punches without a look first:** none. K1 has the lookAt layer and the gorilla is centred in the POV from f0. K3: the S05 POV holds on the dragon for 28 frames. The dragon faces the hero.
- **Other aliveness faults:**
  - Rubber arms that stretch to about 2x length: S02 tile 5, S06 tile 4, S07 tile 10 (left arm stretched to the bottom-left while "cowering"), S08 tiles 8-10 (a banana-shaped hand).
  - The dragon's "charge" is a walk.
  - No person performed any hero close-up or POV (L56 waived for S02, S05, S07 and S08).

---

## 2. Pack-2 (judged real): scores

| # | Cat | W | Score | Evidence |
|---|---|---|---|---|
| C1 | Hook | 1.5 | 3 | Frame 0: three gloved survivors on a summit, meteor streaks small at the top right (about 10 px, filmstrip row 1). There is no contact in the first 2 s, only a glove feint at f25. The verb (POV punch) lands at 2.53 s. The first visible hazard impact is at 5.1 s (S04), because S03 has none. "Appears by 2 s but hard to read." |
| C2 | Clarity | 1.5 | 3 | Best: S02 and S06 (pack-2 key) read who hit whom at once. Worst: S03's hazard_hit label is false (no impact); S04's victim is hidden behind the ghost-rock row at the key; the astro in S03 is cut at the bottom edge. K2 needs a second look. |
| C3 | Story | 1.5 | 3 | Sections in order, staged reveal (S07 reaction, then S08), clear turn. S03 is a non-event beat. K3 is smaller than K1 (a side wide with the hero at about 0.24, after a POV at 0.48), so the second fight does not raise anything. |
| C4 | Hero | 1.0 | 4 | Best: S07 (MS, head back, both hands up; wins then loses). Worst: S04 (h 0.10, occlusion 0.44), S03 (a second yellow survivor at x 0.47), S09 (occlusion 1.00). |
| C5 | Rhythm | 1.0 | 4 | Best: main -0.3 and title +0.1; every hit within 0.5 frame. Worst: S06's 28-frame tail with the hero alone after K3 before S07; length CV 0.24 (L22 waived). The stop-down (297-308) cannot be confirmed from "silence >0.25 s: none". |
| C6 | Shot craft and geography | 1.0 | 3 | Fits compute. Faults: line crossed S03→S04; the glove clips the lens at every POV cut-in (S02 f0, S05 f0, S08 f0); S02's POV drops to the floor at 103 deg/s after the hit; S06 subjects small with 40% of the frame block; four false fit lines (S04, S06, S09, S10). |
| C7 | Sound (numbers) | 0.75 | 3 | Every key has a sound (sync table all within 0.5). I -15.1 LUFS. Ramp +2.2 LU (planned 3). **Title vs body -3.1 LU against the planned -5** (rubric 5 needs -6). True peak not reported (sample -1.3 dBFS). S03's BlockSmash plays with no visible impact. |
| C8 | Titles | 0.5 | 4 | Best: title at 359 (+0.1), held 91 frames, clean darkened plate. Worst: the tagline is about 8 px tall at preview; the title fades in (filmstrip row 8, tile 5) instead of landing; the drop is only -3.1 LU. |
| C9 | Technical | 0.75 | 2 | Meteors render as a chain of duplicated rocks: S03 f15-29, S04 f4-30, eight identical spheres in a row at the S04 key. It reads as a playback ghost trail. Rubber-stretched arms at S02 f~24-28, S06 f~12-16, S07 last frames and S08 tail. The camera clips into its own glove at three POV cut-ins. |
| C10 | Brief fit | 0.5 | 4 | Best: 450 f, 16:9, no hand, POV 35%, all five must-shows covered. Worst: survivors_react's "screams" cannot read without a face; S01's "acting independently" is undercut by starts at 7-9. |
| C11 | Immersion | 1.0 | 3 | All four keys are close or POV (table 1e). But K2's victim is obscured; the panda and astro never visibly react; the rival does not react before he is hit; the glove blocks the lens at POV cut-ins; the S02 POV looks at the floor instead of the falling victim. |
| C12 | Performance | 1.0 | 3 | No still runs over 30 frames (max 15/22). Worst: the hero snaps his head at 887 deg/s in S07 f13; the hook trio starts on f7-9; rubber arms; the dragon walks his "charge"; no performer. Best: the S04 fox flinch and hands-to-head (strip tiles 6-8). |

Weighted: (3x1.5 + 3x1.5 + 3x1.5 + 4 + 4 + 3 + 3x0.75 + 4x0.5 + 2x0.75 + 4x0.5 + 3 + 3) / 12 = 38.25 / 12 = **3.19**. That fails the stage-8 gate (C9 at 2; mean below 3.5).

## 3. Pack-1 (judged planted): scores

Same as pack-2, except for four categories capped by computable hard lints:
- **C2 = 2:** the S06 key still has no victim, so L41 (who did what to whom, both readable) fails at K3.
- **C5 = 2:** `main +5.0`, so L11 (every hit within 2 frames) fails; the payoff is 5 frames late.
- **C7 = 2:** `punch2 no onset within 10 frames`, so L12 (key without a sound) fails; K3 is silent.
- **C11 = 2:** at K3's key still the victim h is 0, so L47 fails for K3.

C1 3, C3 3, C4 4, C6 3, C8 4, C9 2, C10 4, C12 3 are unchanged.

Weighted: (4.5 + 3 + 4.5 + 4 + 2 + 3 + 1.5 + 2 + 1.5 + 2 + 2 + 3) / 12 = 33.0 / 12 = **2.75**. The gap to pack-2 is 0.44 (over 0.25).

## 4. Notes for every category at 3 or below (pack-2)

Pack-1 additionally carries its three plants: re-sync main to 309, restore the K3 impact, and use the true S06 key frame.

**C1 Hook (3)**
- Where: S01 f0-48.
- Symptom: a static line-up 2.5 m apart; rocks are specks; no contact before 2.5 s.
- Cause: a WS from 4 m with the fighters spread across 6 m, and the streaking rocks aimed high and far.
- Return to: stage 3 (plan) for the S01 camera, stage 7 for re-placement from the take.
- Fix: open tighter on the rival's feint at the hero (two survivors at 0.5+), with one rock streaking through the frame in the upper third by f15.

**C2 Clarity (3)**
- Where: S03 f12 (122); S04 f14 (154).
- Symptom: no hazard hit in S03; the penguin hidden behind ghost rocks at K2.
- Cause: S03's aimed rock misses everything; meteor playback draws per-frame copies.
- Return to: stage 6 (capture: aim the S03 rock onto a block) and the C9 ghost fix.
- Fix: retarget the S03 rock to hit block 2's far end in frame, or relabel S03 `shows: [several_survivors]` and drop the BlockSmash.

**C3 Story (3)**
- Where: S03 (110-140) and S06 (208-248).
- Symptom: the S03 beat raises nothing; K3 is smaller than K1.
- Cause: S03 is a geography wide with no event; K3 is covered from a side wide with the hero at 0.24.
- Return to: stage 3.
- Fix: make K3 bigger than K1. For example, a reverse POV from the dragon's eyes catching the hero's glove, or a low 35 mm two-shot with the hero at 0.5. Give S03 a real impact.

**C6 Shot craft (3)**
- Where: the S03→S04 cut; S02 f0, S05 f0, S08 f0; S02 f50-62.
- Symptoms:
  - The screen order reverses across S03→S04.
  - The glove fills 40% of the lens at POV cut-ins.
  - The S02 POV pans to the floor.
- Causes:
  - The S04 camera was placed on the opposite side of block 2.
  - The first-person rig carries the glove up near the face at those frames.
  - The driven look target drops after the punch.
- Return to: stage 7 (camera placement) and stage 6 (look layer).
- Fixes:
  - Put the S04 camera on the S03 side (yaw 80 deg mirrored).
  - Start each POV 3-6 frames later, or add a lowered-glove pose layer.
  - Keep a lookAt on the falling gorilla until he hits the water.

**C7 Sound (3)**
- Where: the title section, 359-450; S03 f12.
- Symptom: title only -3.1 LU below the body; a smash with no picture.
- Cause: the title music is not ducked enough; the sfx was placed on a planned impact that never happened.
- Return to: stage 7 (assembly mix).
- Fix:
  - Pull the title section to about -6 LU.
  - Raise the body ramp to about +3 LU.
  - Report true peak.
  - Measure the 297-308 stop-down dip (needs at least 12 LU).
  - Cut or move S03's BlockSmash.

**C9 Technical (2)**
- Where: S03 f15-29; S04 f4-30 (key f14); S02 f~24-28; S06 f~12-16; S07 f32-36; S08 f26-34.
- Symptom: rock "caterpillars" (duplicated rocks), stretched rubber arms, the camera inside its own glove.
- Cause: likely meteor playback leaving a copy each frame (or a trail VFX replayed as geometry), and the reach layer extending the arm IK past arm length. Verify both against live gameplay.
- Return to: stage 6 (Studio playback fix and capture), then re-render at stage 7.
- Fix: replay the rocks with one transform per frame; clamp the reach layer to arm length (the glove's real reach), or confirm that the extending glove is the game's actual weapon look.

**C11 Immersion (3)**
- Where: S04 (the panda and astro at occlusion 1.00); S02 f34-62; S04 f14.
- Symptom: two cast members react off screen; the POV abandons the victim; K2's victim is obscured.
- Cause: their reaction layers were placed where no camera sees them; the look target drops after the hit; the ghost rocks.
- Return to: stage 7.
- Fix: bring the panda or astro into S04 or S08 at h ≥ 0.08 on their reaction frame, or cut their layers; hold the POV on the gorilla to the splash.

**C12 Performance (3)**
- Where: S07 f13 (261); S01 f7-9; S04 f29; S05.
- Symptoms:
  - The hero's head snaps at 887 deg/s in his own reaction close-up.
  - The hook trio starts in step.
  - The fox snaps at 634 deg/s.
  - The dragon strolls when he should charge.
- Causes:
  - The lookAt layer runs with no ease or too short a blend.
  - All three actors are driven off the same streak cue.
  - The dragon's goTo runs at walk speed.
- Return to: stage 6 (layers; the take stays committed) and stage 7.
- Fixes:
  - Ease the S07 lookAt over 8-12 frames (at most about 300 deg/s), led by the eyes.
  - Offset the S01 reactions by attention (hero 9, rival 16, dragon 24).
  - Ease the fox's look.
  - Use a jog or run on the dragon's goTo.
  - Have a person perform S07 and S08 if one is free (L56).

## 5. Which pack has injected defects

**Pack-1.** Its three defects:
1. `main 309 mix +5.0 lane +5.0`: the payoff is off its hit by 5 frames.
2. `punch2 218 (no onset within 10 frames)`: the K3 impact sound was removed.
3. `stills/S06-key.png`: the victim (dragon) is missing at the K3 contact frame. This contradicts framing.md (dragon h 0.41 at key) and the S06 strip.

## 6. Concrete errors in the real pack (pack-2)

1. S03's `shows: hazard_hit` is false. No rock hits a block or a survivor in 110-140; the BlockSmash at f12 plays over nothing.
2. Meteors render as duplicated-rock "caterpillars" (S03 f15-29; S04 f4-30, eight identical spheres at the key f14).
3. K2's victim (the penguin) is partly hidden by those rocks at contact and is never seen leaving; visible for only 20 of 40 frames.
4. S04's fit line is false. The rock does not come "low from frame right" and the penguin is not seen leaving frame left.
5. Line crossed S03→S04: the penguin, fox and hero order reverses (0.10/0.28/0.81 becomes 0.62/0.22/0.19).
6. Hero snap turn: 887 deg/s at S07 f13, unprovoked, in his reaction close-up.
7. Fox snap turn: 634 deg/s at S04 f29, 15 frames after the hit.
8. The hook trio starts acting within 2 frames (S01 f7, f9, f9); the rival and dragon again at f30 and f32.
9. The glove clips or fills the lens at every POV cut-in (S02 f0, S05 f0, S08 f0), and after K1 (S02 f~30-34).
10. The S02 POV drops to the floor at 103 deg/s after K1. The knocked-off gorilla's fall and splash are never shown, against the plan's "eyes follow him out over the sea".
11. Rubber-stretched arms: S02 at contact, S06 tile 4 (about 2+ m), S07 last frames (stretched to the bottom-left while cowering), S08 tail.
12. S06's fit claims hero h 0.47. His body is about 0.24 (the bounding box includes the stretched arm), so he is about 4.5-5 m from the lens, not 3.1 m. The K3 attacker is small.
13. S06 holds 28 frames on a small hero alone after the dragon exits; dead air before S07.
14. S05: the dragon walks rather than charges. The plan also says "the fallen fox"; it was the penguin.
15. The panda and astro never visibly react. Their layers fall in frames where they are occluded (S04 occlusion 1.00) or tiny (S09 h 0.01).
16. In S03 the astro is cut at the bottom edge (y 0.04, h 0.07 at first and last), so "six readable" is really five. A second yellow survivor (x 0.47) blurs the hero's colour identity.
17. In S04 the hero is h 0.10 with occlusion 0.22 at key and 0.44 at last; in S09 he is not visible (occlusion 1.00 at key).
18. Title vs body is -3.1 LU against the planned -5. Ramp +2.2 LU against the planned 3. True peak not reported.
19. The stop-down 297-308 is not confirmable ("silence >0.25 s: none"; the dip depth is unmeasured).
20. False fit lines: S09 ("top 30 m": the whole tower to the castle is visible), S10 ("title in the sky": no sky in frame), S08 ("lowered glove": it covers the right half at f0). The S02 and S05 distance fields disagree with their own fit lines.
21. take-log.md is empty: no record of the take for the critic.

## 7. Top three problems, by expected weighted gain

1. **Meteor ghost-rock rendering and the rubber arms (C9 2→4, plus C2 and C11 via K2).** Fixing the rock playback alone lifts C9 by about +0.125, and makes K2 readable (C2 3→4: +0.125; C11 3→4: +0.083). About +0.33.
2. **S03's false hazard hit and the weak, small K3 (C2, C3, C1).** A real block hit in S03 and a closer, bigger K3 coverage would lift C3 3→4 (+0.125) and C2 (shared with item 1). A tighter, contact-led S01 would lift C1 3→4 (+0.125). About +0.25-0.3.
3. **Performance and POV handling (C12, C11, C6):**
   - Ease the S07 lookAt and stagger the S01 starts.
   - Run the dragon.
   - Hold the S02 POV on the falling gorilla.
   - Start the POVs after the glove leaves the lens.
   - Fix the S03→S04 line cross.

   C12 3→4 (+0.083), C6 3→4 (+0.083), C11 (shared). About +0.2.
