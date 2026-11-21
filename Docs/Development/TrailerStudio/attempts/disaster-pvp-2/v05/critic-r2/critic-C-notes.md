# Critic C, Disaster PvP 2, v05 fine cut (stage 9), round 2

Method: review-rubric.md sections 3-6, game-content.md section 9. Only the two packs were opened. Sound is judged from the numbers alone. Motion feel at full frame rate cannot be judged from strips.

## 0. What differs between the packs (byte comparison)
Every file is identical except two:
- `metrics.txt`: three lines differ.
  - Pack 1 has `title section mix +5.0 lane +5.0` where the title-vs-body loudness line should be. Pack 2 has `-22.6 vs -14.4 LUFS (-8.1 LU)`.
  - Pack 1 has `rock 154 (no onset within 10 frames)`. Pack 2 has `rock 154 mix -0.4 lane -0.4`.
  - Pack 1 has `title 359 mix +5.0 lane +5.0`. Pack 2 has `title 359 mix +0.1 lane +0.1`.
- `stills/S04-key.png`: pack 1's key still is byte-identical to its `S04-last.png` (frame 179). The penguin is gone from it and only the fox is left. Pack 2's key still shows both the penguin and the fox.

Pack 1's numbers also contradict its own plan (`measured_onset_frame` rock 153.6, title 359.1) and its own lint.txt (L11 and L12 PASS). **Pack 1 is the planted pack and pack 2 is real.** The defects are in section 3.

## 1. Pack 2 (real): evidence answers

### Framing arithmetic (each fit line checked against the stills)
| Shot | Arithmetic | Fit line or `shows` label against the stills |
|---|---|---|
| S01 | 4.0x24/28 = 3.43 m, 6.1 m wide: correct | The heights are true (0.36 / 0.33 / 0.38). **`side: 3q_front, yaw 40` is false**: the hero's facing is 87 deg at the key (profile), and the rival's is 84. The rival "feints in, then backs off" is not seen: he stays on his spot in all 12 strip tiles. |
| S02 | POV 2x1.4xtan34 = 1.89 m: correct | The rival is 0.49 at contact: true. **"The gorilla leaves frame in about 6 frames" is false**: he stays in frame as a speck (h 0.03 at the last frame). **focal_mm 20 on a 24 mm sensor gives a vertical FOV of 61.9 deg, not the 68 deg the fit uses** (the same in S05 and S08). The "pumps a fist" in the `doing` line is not visible: the last 4 tiles show only the glove cuff at the bottom right. |
| S03 | 12.1x24/31 = 9.37 m, 16.7 m wide: correct | The five survivors at h 0.10 or more are true. **"A 4 m rock crosses the frame above them" is false**: the rocks in frame are about 0.03-0.04 of frame height. A 4 m rock at the subject would be 0.43. |
| S04 | 3.5x24/25 = 3.36 m: correct | The penguin and fox shares are roughly true: the engine says 0.37 / 0.33 at the key, the key still measures about 0.26 / 0.29 in pixels. The penguin is out of frame about 6 frames after contact: true. **`also_in_frame: hero, dragon` is false**: framing.md has both off at first, key and last, and motion.csv has no row for either. The L55 waiver line about "S04's background hero and dragon" therefore describes actors who are not in the shot. |
| S05 | 2x2.6xtan34 = 3.51 m: correct | The dragon is 0.47: true. "Charges": in strip tiles 1-8 the dragon walks in place, and the growth to 0.63 comes mostly from the hero walking forward (camera 1.32 m/s at the last frame). |
| S06 | 3.1x24/22 = 3.38 m: correct | The dragon is 0.41 and the hero's body about 0.25: true. The dragon is out of frame within about 3 frames, and the shot holds 18 frames after the hit: true. |
| S07 | 1.6x24/50 = 0.77 m, head to waist: correct | "Both hands rise to his head" is true only for strip tiles 8-11. In the last frames (S07-last) the glove arm shoots straight out to the side next to a small meteor, so the cower ends in an arm-out pose. |
| S08 | 1.35 d | **"The 45 m rock is 0.3 of frame at the cut in" is false**: S08-first shows it at about 0.66. **"The hero's left hand and lowered glove sit at the bottom edge" is false for S08-first**: no hand and no glove are visible. **At the key (f309) the planet killer is not in frame at all**: S08-key is empty sky, with the fox and a yellow hand edge at the bottom. framing.md claims h 1.0 at the key and the camera moving at 25.9 m/s, so the camera is inside or past the mesh. This is the F5-type disagreement between overlay and still. |
| S09 | 52x24/20 = 62.4 m: correct | Survivors 0.01-0.03, not claimed readable: true. The 45 m planet killer appears in no frame of S09. |
| S10 | 48x24/20 = 57.6 m: correct | **"The title sits in the sky above" is false**: the shot is high-angle, with sky only in a thin strip at the top, and the title sits over the sea (filmstrip 360-444). **`move: pull_out` is barely there**: the camera moves 0.06-0.17 m/s. |

### Hits (attacker, victim, both readable?)
- **K1 (S02, f76, POV).** The attacker is the hero's own glove arm, coming in from the bottom centre. The victim is the gorilla at 0.49. Both read at contact (S02-key). Before contact, the hero's own glove covers half the gorilla (strip tile 5). After contact, the glove fills the right half of the frame for about 10 frames (tiles 7-8), so the start of the flight is hidden and the gorilla is a speck from tile 9 on. The arm reads as a stretched yellow pole running from the lens to the gorilla. It may be the game's glove, which cannot be checked from the pack, but it does not read as a punch.
- **K2 (S04, f154).** The attacker is the meteor and the victim the penguin, at 0.33 (engine) or about 0.26 (pixels). **At the key frame the striking rock is not in frame**: S04-key shows a different rock about 1,100 px away at the top left. The hit arrives from outside the left edge and is seen only as a cropped black-orange mass in one strip tile (tile 5). The penguin is then thrown right into the fox (tile 6) and disappears. The victim reads; the attacker barely does.
- **K3 (S06, f218).** The attacker is the hero and the victim the dragon, at 0.41. Both read at contact (S06-key). The dragon flies out of frame in about 3 frames, so the knock-off flight is a blink. For the 18 frames after the hit the hero holds the same pose and does not look after the dragon (tiles 7-12).
- **K4 (S08, f309, POV).** The attacker is the planet killer and the victim the hero. The rock fills the frame up to about f305. **The contact frame is blank sky** (S08-key, strip tile 9), and then come shattered fragments. The payoff impact itself is never seen, from inside or outside (S09 has no planet killer).

### Hero in each section
- Hook, S01: the yellow survivor with red hair, centre frame. Readable.
- Teach: in S02 only his arm (POV). In S03 he is 0.12 at the right, **and the panda is also plain yellow** (S03-key, x 0.47, h 0.10). That breaks the concept's "pick him out by colour".
- Escalate: **absent from S04**. POV arm only in S05. Readable in S06 and S07.
- Payoff, S08: POV only (hand edges).
- Aftermath and title (S09, S10): 0.01-0.03, unreadable. We never see what happens to him.

### Geography
- S03 is the only map of the tower top: the summit at right, block 2 at left, the lower level in the middle.
- S04 is a bare slab against islands, with nothing that places it relative to the summit.
- S09 gives scale: the tower on the castle over the sea.
- From S08 to S09 we jump from the summit POV to a 52 m side wide, and the hero is not trackable across that cut.

### Key events: closeness and first person
| Event | Victim height share | How it is seen |
|---|---|---|
| K1 | 0.49 | POV |
| K2 | 0.33 engine / about 0.26 pixels | close, but the attacker is off frame at the key |
| K3 | 0.41 | close |
| K4 | 1.0 by engine; empty frame in the still | POV, but the impact frame is unseen |

- First-person share: S02 62 + S05 28 + S08 34 = 124 of 359 body frames = 34.5 %.
- In every POV shot the POV left hand is the same flat, open, static palm: S02 tiles 1, 4 and 6; S05 tiles 1, 4, 5, 7, 8, 9 and 11; S08. It never grips, guards or braces.

### Anyone who never reacts
- **Rival, S01:** a rock lands at his feet at about shot frame 40 (strip tiles 11-12) and he keeps his guard.
- **Fox, S04:** no measured action start in the whole shot, so the "flinches as the rock hits him" in the plan is not there. His only big head move is a 634 deg/s turn at shot frame 29, 15 frames after the hit, turning his back to the camera.
- **Fox, S08:** visible for all 34 frames and not in the plan's actors. He stands calm with a hand at his face while the planet killer lands (S08-key, h 0.14).
- **Panda and astro:** their K2 reactions (K2+11 and K2+19) happen while they are off screen, and in every shot they are in they are unreadable or occluded. They act as props.

### Performance (motion.csv)
- **Still or level-headed over 30 frames:** none. The longest are the S05 hero at 22 level and the S01 hero at 20.
- **Starts within 4 frames:**
  - S01: hero 9, rival 9, dragon 7 (all three inside 2 frames), and again 35 / 32 / 30.
  - S03: panda 25, astro 26.
  - These are waived (L55), but they are real and visible: in S01 all three hold the same glove-at-face guard pose for the whole shot.
- **Snap turns over about 400 deg/s not caused by being thrown:**
  - S04 fox, 634 deg/s at shot frame 29. Not hit.
  - S02 hero, 430 deg/s at shot frame 47: a POV head whip after the punch.
  - The S02 rival (587) and the S04 penguin (623) peak while thrown, so they are excused.
- **Punches without a look:** in S01 the hero throws a low glove at about shot frame 38-47 (tiles 10-12) into empty air, more than 2 m short of the rival.
- **Unlisted visible actors (L52 holds on paper only):** the fox in S08 (34 frames), the astro in S05 (7 frames) and the panda in S05 have no `doing`.
- **Hero close-ups and POVs (S02, S05, S07, S08):** none performed by a person (waived, L56).

### Sound by numbers
- Integrated -15.2 LUFS (target -15).
- Sample peak -1.4 dBFS. True peak is not reported (target -1.0 dBTP), so it is unverified.
- Body ramp +2.2 LU: under the plan's +3, at the bottom of the rubric's +2 to +6.
- Title drop -8.1 LU (target -5): met.
- Sync: every event within 0.5 frames in its own lane, the payoff at 0.0 lane / -0.3 mix.
- No stem table in the pack, so lane balance cannot be checked (owed).
- The S07 voice line needs ears.

## 2. Pack 2 scores
| # | Score | Evidence |
|---|---|---|
| C1 Hook (1.5) | 3 | Frame 0 shows three gloved survivors in guard on the summit, with tiny streaking rocks at about 0.01 of frame. Nobody hits anybody in S01. The only blow is a jab into air (tiles 10-12). The first hit is at 2.53 s (L05 passes). It reads, but frame 0 is a standoff, not mayhem. |
| C2 Clarity (1.5) | 2 | The S08 payoff key frame is empty sky, with no subject (S08-key). At K2 the attacker is out of frame (S04-key). In S03 two yellow survivors make the hero ambiguous. In S02 the own glove hides the victim before and after contact. Several fit and `shows` lines are false (table above). |
| C3 Story (1.5) | 3 | Sections are in order, causes come before effects, and the meteor shower comes before the planet killer. But the payoff, the strongest beat, is unseen at contact (S08 f309). "Meteors start raining on every fight" (S03) shows one small rock. The two punches K1 and K3 are near-repeats. |
| C4 Hero (1.0) | 3 | He reads well in S01, S06 and S07. The panda shares his colour (S03). He is absent from S04 and unreadable in S09 and S10, so his fate is never shown. The S07 reaction reads in tiles 8-11 but ends in an arm-out pose (S07-last). |
| C5 Rhythm (1.0) | 4 | Best: title +0.1 lane and punch2 0.0 lane; the stop-down at 297-308 gives a breath before the drop; the escalate median is 34 against 48 earlier. Worst: the 48-frame S07 slows the run into the payoff, and only 1 of 9 cuts is on a beat. |
| C6 Shot craft (1.0) | 3 | The arithmetic is right, but these lines are false: S01 facing, S02 "leaves frame", S03 4 m rock, S08 cut-in size and hands, S10 "sky" and pull-out. The focal length and the 68 deg FOV disagree. S04 has no tower reference. The S08-to-S09 jump loses the hero. |
| C7 Sound (0.75) | 4 | Best: title drop -8.1 LU and every key within 0.5 frames in its lane. Worst: ramp +2.2 LU (plan +3), true peak not measured, no stem table. |
| C8 Titles (0.5) | 3 | The title is on its hit (+0.1), the card holds 91 frames and the tagline is game-specific. But it fades in over about 6 frames (filmstrip 360 at half opacity), so it is not hard on the hit. Debris is spilling behind it for the first second, and it sits over the sea, not the sky the plan names. |
| C9 Technical (0.75) | 2 | The planet killer vanishes on the payoff frame (S08 f309, strip tile 9): the camera is inside or past the mesh, a pop at the most important frame. Stretched pole-like glove arms appear in S02, S06, S07-last and S08 tiles 10-12. The own glove fills half the frame in S02. |
| C10 Brief (0.5) | 4 | Best: the S06 knock-off at contact, attacker and victim whole. Worst: `survivors_react` is proved only by the hero, with no threat in frame, and the fox in S08 ignores the planet killer. `hazard_hit` in S04 has its attacker off frame at the key. Length, aspect and type are met. |
| C11 Immersion (1.0) | 3 | Every key event is close or in POV on paper (L47), but K4's contact frame is empty and K2's attacker is off frame. The POV hand is a frozen flat palm in all three POV shots. Witnesses fail to react: rival S01, fox S04, fox S08. |
| C12 Performance (1.0) | 3 | No run over 30 frames (the maximum is a 22-frame level run). Problems: three-way synced starts in S01 at 7-9 and 30-35; snap turns by the S04 fox (634 deg/s) and the S02 hero (430 deg/s); a punch into air in S01; identical guard poses; a static POV hand; no person performed any of it. |

**Weighted mean, pack 2:** (4.5 + 3 + 4.5 + 3 + 4 + 3 + 3 + 1.5 + 1.5 + 2 + 3 + 3) / 12 = 36.0 / 12 = **3.00**

### Notes for categories at 3 or below (pack 2)
- **C1, S01 f0-47**
  - Symptom: a static standoff, with rocks too small to read and no contact.
  - Cause: the hook was staged as "square up" plus a lead-in whoosh, and the first contact was held for S02.
  - Stage: 3 (beat sheet / shot list).
  - Fix: open on a hit or a rock landing on the summit inside the frame (the rock that lands at the rival's feet at about f40 could move to f5-10 with the rival reacting), or start S01 with the rival already swinging.
- **C2, S08 f309; S04 f154; S03**
  - Symptom: the payoff frame is empty; the K2 attacker is off frame; there are two yellow survivors.
  - Cause: the POV camera rides the hero's head through the rock's mesh at 26 m/s. The S04 camera was placed for the victim, not for the rock's path. The panda's mark colour matches the hero's.
  - Stage: 6 (camera placement from the take) and 5 (cast colours).
  - Fix: cut S08 to S09 on the last frame where the rock fills the frame (about f305-307), or pull the POV origin back so the mesh stays in front of the lens. Widen or pan S04 so the rock's entry is in frame 4-6 frames before contact. Recolour the panda.
- **C3, S08 / S03**
  - Symptom: the strongest beat is unseen at contact, and the escalation in S03 is undersold.
  - Cause: the same camera clipping; the S03 rocks are shower rocks, not the aimed 4 m rock.
  - Stage: 6, then 8.
  - Fix: as for C2; time an aimed 4 m rock across S03's key.
- **C4, S04, S09, S10, S03**
  - Symptom: the hero is missing, tiny, or confusable.
  - Cause: the S04 framing excludes the summit, the aftermath is a scale wide only, and the panda shares his colour.
  - Stage: 5 (colour) and 6.
  - Fix: keep the hero in the back of S04 (he is on the summit beside block 2), and give the aftermath one closer shot of him being thrown (the plan has a fall layer).
- **C6, the fit lines in the table**
  - Symptom: labels and fit lines that the stills contradict.
  - Cause: the fit lines were written from the plan, not re-checked on the renders.
  - Stage: 8 (pack and labels).
  - Fix: re-write the fit lines from the stills, and fix focal_mm against the 68 deg FOV.
- **C8, f359-372**
  - Symptom: the title fades in over busy falling debris, over the sea.
  - Cause: S10 starts while the wreck is still spilling, and the fade-in style was chosen.
  - Stage: 8.
  - Fix: start S10 about 0.5 s later in the take, or cut the title in hard on 359.
- **C9, S08 f309 (and about f305-310)**
  - Symptom: the subject vanishes; the frame goes to sky.
  - Cause: the camera is inside the mesh.
  - Stage: 6.
  - Fix: as for C2.
  - Also: in S02, S06 and S07 the pole-like glove arms need a decision. If that is the game's glove, keep it but shorten its time on screen; if it is IK stretch, fix the reach layer.
- **C11, S04 / S08 / S01 / POV hands**
  - Symptom: witnesses ignore impacts, and the POV hand is frozen.
  - Cause: no look-at or flinch layers on the fox at K2 (none measured) or at K4, none on the rival for the S01 rock, and no alive or grip layer on the POV left hand.
  - Stage: 5 (layers).
  - Fix: add a fox flinch at K2+3 and a look-up before K4, a rival flinch at the S01 rock, and a guard or reach motion on the POV left hand.
- **C12, S01 f7-9 / f30-35; S04 fox f29; S02 hero f47; S01 jab**
  - Symptom: synced starts, snap turns, and a punch at nothing.
  - Cause: the take's driver was scripted on shared times; the eased looks are not capped.
  - Stage: 5 (re-record) or a layer fix.
  - Fix: stagger the S01 starts by 6-15 frames, cap head turns at 300 deg/s, aim the S01 jab at the rival within reach, and have a person perform the S08 POV.

## 3. Pack 1 (planted): scores and injected defects

Injected defects found:
1. **The title cut is moved 5 frames off its hit** in the sync table: `title 359 mix +5.0 lane +5.0` (real: +0.1).
2. **The impact sound is removed from K2**: `rock 154 (no onset within 10 frames)` (real: -0.4).
3. **The S04 key still is replaced by a frame where the victim is hidden**: S04-key is byte-identical to S04-last (f179), the penguin is gone and the fox shows his back.
4. In addition, the title-vs-body loudness line is replaced by `title section mix +5.0 lane +5.0`, so the title drop cannot be read.

The lint.txt in both packs is identical and still says L11 and L12 PASS, which pack 1's metrics contradict.

Pack 1 scores. Unchanged from pack 2 unless noted.
- C1 3; C3 3; C4 3; C6 3; C9 2; C12 3, as in pack 2.
- C2 2: the victim is hidden at the K2 key (R01/L41), on top of the S08 failure.
- **C5 2**: L11 fails (title +5.0 frames; every hit must be within 2), which caps it at 2.
- **C7 2**: L12 fails (a key without a sound in [key-1, key+3]), which caps it at 2, and the title drop is unmeasured.
- **C8 2**: the title is 5 frames off its hit (L07 on-hit fails).
- **C10 3**: the `hazard_hit` proof is not true of the S04 key still.
- **C11 2**: K2's key frame has no victim, so K2 is not seen close at its key.

**Weighted mean, pack 1:** (4.5 + 3 + 4.5 + 3 + 2 + 3 + 1.5 + 1 + 1.5 + 1.5 + 2 + 3) / 12 = 30.5 / 12 = **2.54**, which is 0.46 under pack 2.

## 4. Concrete errors in pack 2 (the real pack)
1. **S08 f309 (payoff key):** the planet killer is not in frame and the frame is blank sky. The camera is inside or past the rock (framing.md h 1.0 at 25.9 m/s). The payoff impact is never seen.
2. **S04 f154 (K2 key):** the striking rock is outside the left edge. The rock that is visible is about 1,100 px from the penguin. The attacker reads only in one strip tile as a cropped mass.
3. **S04:** `also_in_frame` lists the hero and the dragon, but neither is in the shot (framing.md off, no motion rows). The L55 waiver refers to them, and L42's "hero in frame through the cut" is false for S04.
4. **S03:** the panda is plain yellow like the hero (S03-key x 0.47), which breaks hero-by-colour.
5. **S03 fit:** the "4 m rock crosses the frame" is not there. The rocks in frame are about 0.03-0.04 of frame height.
6. **S08 fit:** the rock is about 0.66 of frame at the cut-in, not 0.3, and no hand or glove is at the bottom edge in S08-first.
7. **S10:** the title sits over the sea, not in the sky, and the "pull_out" moves at 0.06-0.17 m/s.
8. **S02:** the gorilla does not leave frame (h 0.03 at the last frame). The own glove covers him before contact (tile 5) and fills half the frame for about 10 frames after (tiles 7-8). The fist pump is not visible.
9. **S01:** the declared 3q_front is profile (facing 87 / 84). The rival never feints. The hero's jab hits air. The rival ignores a rock landing at his feet (tiles 11-12).
10. **S01:** the three action starts fall at 7 / 9 / 9 and 30 / 32 / 35, and the three guard poses are identical.
11. **S04:** the fox has no action start (no flinch), then snap-turns at 634 deg/s at shot frame 29.
12. **S02:** a 430 deg/s POV head whip at shot frame 47, not caused by being thrown.
13. **S08:** the fox (h 0.02-0.14, 34 frames) is an unlisted actor and stands calm through the impact. The astro and panda in S05 are also unlisted.
14. **POV shots S02, S05, S08:** the left hand is the same static flat palm throughout.
15. **S06:** the dragon is out of frame about 3 frames after contact, and the hero does not look after him for the 18-frame hold.
16. **S07-last:** the "cower" ends with the glove arm thrust straight out sideways.
17. **Pole-like stretched glove arms** in S02-key, S06-key, S07-last and S08 tiles 10-12. Whether this is the game's look cannot be checked from the pack.
18. **focal_mm 20 against a 68 deg vertical FOV** on a 24 mm sensor (that lens gives 61.9 deg) in S02, S05 and S08.
19. **Sound:** ramp +2.2 LU is under the plan's +3. True peak is not reported (sample peak -1.4 dBFS). The stem table is missing (owed).
20. **S09/S10:** the hero's fate is unreadable (0.01-0.03), and the planet killer appears in no outside shot.

## 5. Top three problems (by expected weighted gain)
1. **The S08 payoff frame is empty (camera inside the planet killer).** Fixing it raises C2, C9, C3 and C11, worth about +0.4 weighted. Return to stage 6: cut to S09 on the last frame where the rock fills the frame, or offset the POV so the mesh stays in front of the lens.
2. **Witnesses and performance: synced starts, snap turns, non-reactions, the frozen POV hand.** Fixing it raises C12 and C11, about +0.17. Return to stage 5: staggered re-record or layers (fox flinch at K2 and K4, rival flinch in S01, grip or guard on the POV hand, capped head speed).
3. **Hero readability: the yellow panda, the hero absent from S04, invisible in the aftermath.** Fixing it raises C4 and C2, about +0.2. Return to stage 5 (recolour the panda) and stage 6 (keep the hero in the back of S04; one closer aftermath shot of him thrown).

Not judged: sound by ear (including the S07 voice line), motion feel at 30 fps, VR comfort.
