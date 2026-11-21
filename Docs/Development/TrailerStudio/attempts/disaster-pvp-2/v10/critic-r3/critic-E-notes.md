# Critic E: Disaster PvP 2b, stage 9 fine cut (critic-r3)

Method: review-rubric.md sections 3-6; game-content.md section 9. Read-only.

## How the two packs differ (byte comparison)
These files are identical in both packs: brief, concept, plan, lint, motion.csv, framing.md, take-log (empty), contact sheet, filmstrip, stills-10 and all strips. Only three items differ:
1. metrics.txt sync `punch1 76`: pack-1 shows mix -0.4 and lane +0.0. Pack-2 shows mix +5.0 and lane +5.0.
2. metrics.txt sync `main 309`: pack-1 shows mix -0.3 and lane +0.0. Pack-2 shows "(no onset within 10 frames)".
3. stills/S06-key.png: in pack-1 the dragon is in the air, just knocked away by the hero's glove. In pack-2 the hero punches empty air. The dragon (the victim of K3) is missing from the key frame, even though framing.md still lists the dragon at h 0.39 on screen at that key.

**Pack-2 has the injected defects** (3 of 3 found): (a) the K1 punch cut is 5 frames off its hit; (b) the payoff impact sound is removed at 309; (c) the S06 key still is replaced by one where the K3 victim is hidden. **Pack-1 is real.**

## Evidence-pack answers (pack-1; pack-2 identical except where noted)

### Framing arithmetic (distance x 24 / focal; POV: 2 d tan(vFOV/2))
| Shot | Computed Hf | Fit line vs framing.md and stills |
|---|---|---|
| S01 | 2.9 x 24/24 = 2.90 m | Correct: gorilla 0.43, hero 0.32, dragon 0.24 at key. But the gorilla is cut by the right edge on the first and last frames (x 0.92). |
| S02 | 2 x 1.4 x tan34 = 1.89 m | The fit says 0.48 at contact; framing says 0.55. "The gorilla leaves frame in about 6 frames" is **false**: he stays in frame as a speck for about 20 frames (strip row 2 tile 3 to row 3 tile 1; last h 0.04, still on screen). |
| S03 | 12.1 x 24/31 = 9.37 m; width 16.7 m | Heights match (0.12/0.12/0.13/0.20/0.10). The fit says the astro is 0.07-0.08 and "not counted", but at the key he is 0.09. |
| S04 | 3.5 x 24/25 = 3.36 m | Penguin 0.33 is correct; the fit says fox 0.35, framing says 0.34. A "4 m rock" in a 3.36 m frame cannot be shown whole, and it arrives filling the left edge (strip row 2 tile 1). |
| S05 | 2 x 2.6 x tan34 = 3.51 m | **False**: the fit says dragon 0.47 at the key; framing shows 0.34 at the key, facing 153 deg (back to camera). He reaches 0.47 only at the last frame. |
| S06 | 3.1 x 24/22 = 3.38 m | Dragon 0.39 and hero 0.29 are correct (the take_ref says 0.41). The arm is at natural length in S06. |
| S07 | 1.5 x 24/48 = 0.75 m | Hf is correct, but "the glove held low at his chest" is **false**: the glove is out at shoulder height filling the left third (key still). "Left hand rises to his brow" is **false**: the hand goes above his head, palm open, a wave. |
| S08 | 1.68 d; at 40 m = 67 m | A 45 m rock at 40 m would be 0.67 of frame, not the stated "0.45 at cut-in" (that implies about 60 m). "Hands rising over his head" is **false**: no hand rises in the POV; only the glove shows at the bottom right in the last 4 tiles. |
| S09 | 3.4 x 24/24 = 3.40 m | 0.34 is correct. The fit says "riding 3.2 m" but the declared distance is 3.4 m. |
| S10 | 52 x 24/20 = 62.4 m | Correct. |
| S11 | 48 x 24/20 = 57.6 m | Correct. |

### Shows labels and learns lines that the stills contradict
- S05 learns "The dragon charges you down the block": **false**. For about two thirds of the shot he stands at the far end facing away (facing 144-153 deg at first and key). He turns and walks in only for the last 3 strip tiles. No run, no visible charge.
- S07 learns "looks up, shading his eyes" and the brief's survivors_react proof ("looks up at the planet killer and screams"): **not met**. The raised hand reads as a wave or cheer, the head pitch is slight, and there is no scream (only a "WhatWasThat" VO line).
- S04 shows hazard_hit: the key still (f154) has **no rock in frame**. The rock enters about 13 frames into the shot, oversized at the left edge, and the penguin is flung into and through the fox (row 2 tile 2), not "out of frame".
- S01 learns "Three gloved survivors circle, feinting": nobody circles. The hero stands in place and lowers his glove late. The gorilla and dragon hold almost the same raised-glove pose.

### Specific checks asked
- **Punch arm stretch.** S02 (K1, POV): **yes**. At contact the hero's glove arm is a thin, twisted, taffy-like yellow strand. It runs from the bottom edge through the hollow glove cuff (S02-key, centre-right), narrower than the left arm and pinched in the middle. The left arm is also long and S-curved in S02 strip row 3 tiles 1-2. S06: no stretch; the arm is at natural length (S06-key pack-1). But the hero then holds the extended punch pose frozen for the rest of the shot (strip rows 2-3, about 18 frames). The same rigid extended arm carries through all of S09.
- **S01 opening: three different people already in motion, out of step?** Partly. Action starts are hero 12, rival 34, dragon none, so they are not in step. But they read as three figures posing, not moving. The gorilla and dragon hold matching raised-glove guards, the dragon's head is level for 13 frames, nobody travels, and the gorilla is cropped by the right frame edge at first and last.
- **S07: hero's head and eye direction?** No. The character has no drawn eyes, and the head pitches up only slightly (the hair shell tilts back in the key). The raised forearm crosses in front of the head (key still), the oversized glove dominates the left third, and the hand gesture reads as a wave. Gaze direction is not readable.
- **Does the dragon visibly charge?** No (S05, see above). In S06 he lunges only in the first tiles.
- **POV left hand varies?** Barely. In S05 it is the same flat open palm at the bottom left for all 28 frames, moving only a little. In S02 the same flat palm (rows 1-2), then an elongated arm swing after the hit. In S08 the left hand is absent.
- **Head turns over about 350 deg/s not caused by being thrown:** S04 fox **378 deg/s at shot frame 18**. That is a snap flinch; the fox is not thrown. The other values over 350 are thrown victims: S02 rival 624 at f30 (hit f28), S04 penguin 623 at f16 (hit f14), S06 dragon 556 at f12 (hit f10). Near misses: S01 dragon 325, S03 dragon 312, S03 astro 314, S10 fox 334.

### Every hit (5b)
| Event | Attacker | Victim | Both readable? |
|---|---|---|---|
| K1 S02 f76 | hero's own glove (POV) | gorilla h 0.55 | The attacker reads, but with a stretched, twisted arm. The glove covers the gorilla's head and upper body at contact (S02-key), so the victim reads only by his legs and the flight afterward. |
| K2 S04 f154 | meteor | penguin h 0.33 | The victim reads. The attacker is absent from the key frame and arrives cropped and oversized at the frame edge. Contact is a blur of rock and penguin overlapping the fox. |
| K3 S06 f218 | hero h 0.29 | dragon h 0.39 | Yes, side-on (pack-1). The contact is in the frame before the key; the key is just after contact with a gap. **Pack-2: the victim is missing from the key still.** |
| K4 S08 f309 | planet killer | hero (POV) h 1.0 | The attacker reads. The victim's body never reacts in the POV (no hands). |

Hero by section: hook S01 (yellow, centre); teach S02 POV, S03 tiny at 0.12 with a **second yellow survivor (the panda) at 0.10 in the frame centre** that splits the colour key; escalate S05 POV, S06, S07; payoff S08 POV; aftermath S09. New disasters: the meteor shower gets S03/S04 and the planet killer gets S07/S08, so each has its own beat. Tower position is clear through S03, S10 and S11; S05 and S06 are only "on a pink block".

### Closeness and first person (5c)
- K1: POV, victim 0.55. K2: close, 0.33. K3: close, 0.39 (pack-2 key: victim absent). K4: POV, 1.0. No key event is covered only by a wide.
- First-person share: S02 62 + S05 28 + S08 34 = 124 of 359 body frames = **34.5 %**.
- Cast acting like props: the fox in S08 holds one raised pose through 8 tiles. The astro and panda in S03 barely change across the strip. The gorilla never reacts before contact; he stands upright in a static guard (S02 row 1).

### Performance (5d)
- motion.csv claims no still run over 14 frames and no level-head run over 22. The strips contradict this for poses: S06 hero holds the punch extension frozen for about 18 frames, then S09 hero is rigid (arm horizontal, legs straight) for all 20 frames while drifting. Root translation keeps the "still" metric low while the pose is frozen.
- S05 hero POV level-head run is 22 frames.
- Pairs within 4 frames: S03 panda 25 and astro 26 (waived).
- Punches thrown without looking: S06. The hero faces camera-right while the dragon flies off to the left; he never "watches the dragon go".
- No person performed any hero close-up or POV (L56 waived for S02, S05, S07 and S08).

### Sound by numbers
Pack-1:
- I -15.2 LUFS (target -15). Sample peak -1.2 dBFS; true peak is not reported, so the -1.0 dBTP check is owed.
- Ramp +2.4 LU: inside +2..+6 but under the plan's 3.
- Title -8.6 LU (plan -5): the drop is met.
- Every sync is within 0.5 frames.
- The planned stop-down at 297-308 (0.37 s) does not appear under "silence >0.25 s: none", so the stop-down is not actually silent, or it is unmeasured.

Pack-2:
- punch1 is +5.0 frames in both the mix and the lane, which fails L11 (every hit within 2 frames).
- main 309 has no onset within 10 frames, so the payoff key has no sound. That fails L12 and L11's payoff clause.

## Scores, pack-1 (real)
| # | Cat | W | Score | Evidence |
|---|---|---|---|---|
| C1 | Hook | 1.5 | 3 | Frame 0 shows three gloved survivors on the summit with small meteors (S01 first). The first hit is at 2.53 s (f76). The opening 2 s is a static stand-off; the meteors are specks. |
| C2 | Clarity | 1.5 | 2 | Learns lines false in S05 (no charge) and S07 (wave, not shading). S04 key f154 has no attacker in frame. S03 has two yellow survivors. K1's glove hides the victim's head at contact. |
| C3 | Story | 1.5 | 3 | Sections in order; brawl, meteor, counter, planet killer, wreck all escalate, and every effect follows its cause. The S07 reaction does not read as dread, so the turn is weak. Ends on a settling wide, not the strongest beat. |
| C4 | Hero | 1.0 | 3 | The yellow hero is in every section. The second yellow survivor (S03 panda) splits the colour key. His reaction (S07) reads as a wave. Head, eyes and body do not carry fear. |
| C5 | Rhythm | 1.0 | 4 | Best: punch2 and main lane +0.0, title +0.1. Worst: S02 at 62 frames is the longest shot, in the teach section. S07 at 48 frames stalls the escalate section. The build is modest. |
| C6 | Shot craft | 1.0 | 3 | Gorilla cropped by the frame edge at S01 first and last. The S04 rock is larger than its frame. S08 camera on a faceted rock face (strip row 2 tile 4). Three fit lines false (S02, S05, S07). |
| C7 | Sound (numbers) | 0.75 | 4 | Best: every sync within 0.5 frames, title -8.6 LU. Worst: ramp +2.4 (under plan's 3), stop-down not measured as silence, true peak unreported. |
| C8 | Titles | 0.5 | 4 | Best: title on 359 (+0.1), held 91 frames over the darkened wreck, game-specific tagline. Worst: tagline about a third of the title height, small at preview size. |
| C9 | Technical | 0.75 | 2 | S02 glove arm twisted and stretched through a hollow cuff. S07 hair shell gapped off the head (key). S08 camera nearly inside the planet killer, showing flat facets (row 2 tile 4). S08 strip tile 9 (about f308-309) shows empty sky where the rock should be, a possible leftover hidden-rock frame at the main hit. |
| C10 | Brief | 0.5 | 3 | pvp_hit, knock_off, hazard_hit and several_survivors are met. survivors_react ("looks up... screams") is not met: wave, no scream. 15 s, 16:9. |
| C11 | Immersion | 1.0 | 3 | Every key event is close or POV (0.55/0.33/0.39/1.0) and POV is 34.5 %. But the POV hands are dead (same flat palm in S05, none in S08), the fox snap-flinches without moving his body, and the fox and gorilla stand like props. |
| C12 | Performance | 1.0 | 2 | The S06 hero freezes in the punch extension about 18 frames and never looks at the dragon. S09 hero rigid for 20 frames. S05 dragon stands with his back turned for most of the shot. S04 fox snap at 378 deg/s. S01 gorilla and dragon mirror poses. Nothing was performed by a person. |

Weighted: (3x1.5 + 2x1.5 + 3x1.5 + 3 + 4 + 3 + 4x0.75 + 4x0.5 + 2x0.75 + 3x0.5 + 3 + 2) / 12 = 35.0 / 12 = **2.92**

## Scores, pack-2 (defective)
These are the same as pack-1 except:
- **C5 2** (cap L11: punch1 +5.0 frames).
- **C7 2** (cap L12: the payoff at 309 has no onset).
- **C11 2** (the K3 key frame shows no victim).
- C2 stays 2, and is worse in substance: the K3 attacker punches empty air in the key still, which contradicts framing.md's dragon at 0.39.

Weighted: (4.5 + 3 + 4.5 + 3 + 2 + 3 + 1.5 + 2 + 1.5 + 1.5 + 2 + 2) / 12 = 30.5 / 12 = **2.54**. Gap to pack-1: 0.38.

## Notes for every category at 3 or below (pack-1)
- **C1** S01 f0-48.
  - Symptom: a stand-off with specks of meteors; no verb until f76.
  - Cause: S01 is an idle preamble and the take's first contact is late.
  - Stage: fine cut / plan.
  - Fix: open nearer K1 (start S02's approach earlier, or trim S01 to about 30 frames), or put a rock close to camera in S01.
- **C2** S05 f180-208.
  - Symptom: the dragon faces away and does not charge.
  - Cause: the driven goTo starts too late (action start 12) and runs at walk pace.
  - Stage: shoot (layer pass).
  - Fix: start the charge before the cut-in at jog speed, or re-label the learns line.
- **C2** S07 f236-284.
  - Symptom: the hand-to-brow reads as a wave.
  - Cause: the reach target sits above the head, not at the brow.
  - Stage: layer pass.
  - Fix: move the reach target to the brow and add a crouch or cower.
- **C2** S04 f154.
  - Symptom: no rock at the key frame; contact is at the frame edge.
  - Cause: the key is placed before the contact frame, and the 4 m rock overfills a 3.36 m frame.
  - Stage: plan/camera.
  - Fix: widen to about 5 m Hf or move the camera back, and put the key on the contact frame.
- **C2** S03.
  - Symptom: the panda is yellow, so two "yellow" figures appear.
  - Cause: mark colour choice.
  - Stage: plan/casting.
  - Fix: recolour the panda.
- **C3** S07 to S11.
  - Symptom: the turn to dread does not land, and the cut ends on a settle.
  - Cause: the S07 reaction misreads.
  - Stage: layer pass.
  - Fix: fix S07. The ending is acceptable once the turn reads.
- **C4** S03 and S07.
  - Symptom: the hero colour key is split, and his reaction does not read.
  - Cause: the same two problems as in C2.
  - Stage: casting / layer pass.
  - Fix: recolour the panda and redo the S07 reaction (see C2).
- **C6** S01 first/last.
  - Symptom: the gorilla is cropped by the right edge.
  - Cause: the push-in is framed for the key, not the ends.
  - Stage: camera.
  - Fix: pan left 0.05 or start wider.
- **C6** S08 at about f300.
  - Symptom: a flat faceted polygon fills the frame.
  - Cause: the rock's low-poly mesh is too near the lens.
  - Stage: camera/edit.
  - Fix: cut 2-3 frames earlier into the burst.
- **C6** fit lines S02, S05, S07, S08.
  - Symptom: the lines are untrue of the stills.
  - Cause: fits were written from the plan, not re-read from the render.
  - Stage: fine cut paperwork.
- **C9** S02 f76.
  - Symptom: the arm is stretched and twisted through the glove cuff.
  - Cause: the punch IK or reach layer overextends the arm to the glove's punch position in first person.
  - Stage: layer pass / Studio arm IK.
  - Fix: clamp the reach to arm length and let the body lunge instead.
- **C9** S07.
  - Symptom: the hair shell has a gap off the head.
  - Cause: the outfit attachment offset when the head pitches up.
  - Stage: shoot.
  - Fix: fix the attachment.
- **C9** S08 at about f308-309.
  - Symptom: empty sky where the rock should be.
  - Cause: the game hides the rock before its burst, and the frame-drop edit appears incomplete.
  - Stage: fine cut.
  - Fix: verify frame by frame and drop any frame with neither rock nor burst.
- **C10** S07.
  - Symptom: the survivors_react proof (scream) is missing.
  - Cause: a VO line was used instead of a scream layer.
  - Stage: layer pass / mix.
  - Fix: add the scream reaction and a scream SFX.
- **C11** S05 and S08.
  - Symptom: the POV hands are inert or absent, and the threat does not feel like playing.
  - Cause: no person in the headset; the alive layer only jitters a fixed palm.
  - Stage: shoot.
  - Fix: perform the POVs with a person, or drive the left hand: raise the guard in S05, and raise both hands up into view in S08.
- **C12** S06 f218-236 and S09.
  - Symptom: after the hit the body freezes in the punch pose and never tracks the victim.
  - Cause: the punch layer holds its end pose, and no lookAt is set to the flying dragon. S09 is physics-only with a frozen pose.
  - Stage: layer pass.
  - Fix: release the punch after about 6 frames, add a lookAt on the dragon, and give S09 a tumble or flail reaction.
- **C12** S04 f18.
  - Symptom: the fox's head snaps at 378 deg/s.
  - Cause: the flinch layer's lookAt has no easing.
  - Stage: layer pass.
  - Fix: ease the turn to 200 deg/s or less, and lead with the shoulders.

## Injected defects (pack-2)
1. The K1 cut and punch moved 5 frames off its hit: punch1 +5.0 in both mix and lane (metrics.txt). This fails L11.
2. The payoff impact sound was removed: main 309 has "no onset within 10 frames". This fails L12 and L11's payoff clause.
3. The S06 key still was replaced by a frame where the victim (dragon) is hidden: the hero punches empty air. It contradicts framing.md, which has the dragon at h 0.39 at the key.

## Concrete errors in pack-1 (real)
1. S02 f76 (K1, POV): the hero's glove arm is stretched and twisted through the hollow cuff, longer and thinner than natural.
2. S02 f76: the glove covers the gorilla's head at contact; the victim reads only by his legs.
3. S02 fit: "gorilla leaves frame in about 6 frames" is false; he stays a speck for about 20 frames. Fit 0.48 vs measured 0.55.
4. S05: the dragon does not charge. He stands with his back to camera (facing 144-153) for about 2/3 of the shot; the learns line is false.
5. S05 fit: 0.47 at the key is false; measured 0.34.
6. S05: the POV left hand is the same flat palm for all 28 frames; the hero's head is level for 22 frames.
7. S07: the hand goes above the head (a wave), not to the brow. The head pitch is slight, there are no eyes, and the forearm crosses the head. Gaze is not readable.
8. S07 fit: the "glove held low at his chest" is false; it is out at shoulder height, filling the left third.
9. S07: the hair shell gaps off the head (key still).
10. S07/brief: no scream. survivors_react is not met as the brief defines it.
11. S04 f154 (K2): no rock in the key frame. The rock arrives oversized at the left edge, and the penguin is flung through the fox.
12. S04 f18: the fox head-snaps at 378 deg/s without being thrown.
13. S06: after contact the hero freezes in the punch extension for about 18 frames and never looks at the departing dragon.
14. S09: the hero is a rigid mannequin (arm horizontal, legs straight) for 20 frames. "Thrown" reads as drifting, and he ends over a block.
15. S08: hands never rise in the POV, contrary to the plan. The camera nearly enters the low-poly rock (flat facets about f300). A possible empty-sky frame at about f308-309.
16. S08 fit: a 45 m rock at the declared 40 m would be 0.67 of frame, not 0.45.
17. S03: the panda is yellow, a second yellow survivor that splits the hero's colour key. The astro is 0.09 at the key, contrary to the "0.07-0.08, not counted" note.
18. S01: the gorilla is cropped by the right frame edge at the first and last frames. Nobody "circles". The gorilla and dragon hold mirrored raised-glove guards.
19. Mix: the ramp of +2.4 LU is under the plan's +3. The stop-down at 297-308 is not measured as silence. True peak is not reported.
20. No hero close-up or POV was performed by a person (L56 waived on S02, S05, S07 and S08).

## Top three problems (by weighted gain)
1. **Performance and reaction layer pass (C12, C2, C4, C10):**
   - S06: release the punch pose and add a lookAt on the flying dragon.
   - S09: give the hero a tumble.
   - S07: move the reach to the brow, add a cower and a scream.
   - S05: start the dragon's charge before the cut at jog speed.
   - Fixing these could raise C2 to 3, C12 to 3, C4 to 4 and C10 to 4: about +0.33 weighted.
2. **First-person body (C9, C11, C2):**
   - S02: clamp the punch arm to natural length and lunge the body instead.
   - Drive the left hand in S05 and S08 (guard up, hands rising at the rock).
   - Keep the glove off the victim's head at contact.
   - Gain about +0.15 to +0.25.
3. **Hazard-hit framing and the payoff frames (C2, C6, C9):**
   - S04: widen the frame and put the key on the rock's contact frame.
   - S08: remove the low-poly close-up and any empty-sky frame at the main hit.
   - S01: un-crop the gorilla.
   - Re-measure every fit line from the render.
   - Gain about +0.15 to +0.2.
