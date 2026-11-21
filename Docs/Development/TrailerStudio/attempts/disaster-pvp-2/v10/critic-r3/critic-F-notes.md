# Critic F, Disaster PvP 2b, stage 9 (r3). Packs 1 and 2.

Verdict first: pack-1 is the real pack. Pack-2 has three injected defects. Sound judged by numbers only. I did not open `critic-r3` files outside the two packs.

## 0. Which pack is defective (pack-2)

1. `metrics.txt` sync row `punch1 76`: pack-1 reads mix -0.4 / lane +0.0, pack-2 reads mix +5.0 / lane +5.0. The sound sits 5 frames after the punch; the hit is on a cut-adjacent key.
2. `metrics.txt` row `main 309`: pack-1 reads mix -0.3 / lane +0.0, pack-2 reads "(no onset within 10 frames)". The payoff impact has no sound.
3. `stills/S06-key.png` in pack-2 is byte-identical in size to `S06-last.png` (1,679,643 bytes) and shows the hero alone with no dragon. The key still has been swapped for a later frame where the victim is gone. Pack-1's S06-key shows the dragon airborne beside the hero's glove. The S06 strip is identical in both packs, so it contradicts pack-2's key still.

Also: pack-2's `lint.txt` is byte-identical to pack-1's and still says L11 PASS ("every hit measured within 2 frames") and L12 PASS ("keys without a sound: none"). Both are false against pack-2's own metrics, so the lint was not recomputed. Every other file (brief, concept, plan, motion.csv, framing, take-log, contact, filmstrip, strips, other stills) is identical across the packs.

## 1. Scores

Weighted mean = sum(score x weight) / 12.

| Cat | Wt | Pack-1 (real) | Pack-2 |
|---|---|---|---|
| C1 Hook | 1.5 | 3 | 3 |
| C2 Clarity | 1.5 | 3 | 2 |
| C3 Story | 1.5 | 3 | 3 |
| C4 Hero | 1.0 | 3 | 3 |
| C5 Rhythm | 1.0 | 4 | 2 (cap, L11) |
| C6 Craft | 1.0 | 3 | 3 |
| C7 Sound | 0.75 | 4 | 2 (cap, L12) |
| C8 Titles | 0.5 | 3 | 3 |
| C9 Technical | 0.75 | 3 | 3 |
| C10 Brief | 0.5 | 3 | 3 |
| C11 Immersion | 1.0 | 4 | 3 |
| C12 Performance | 1.0 | 3 | 3 |
| **Weighted** | | **3.23** | **2.81** |

Gap 0.42, above the 0.25 planted-check threshold. Pack-1 is under the 4.0 fine-cut bar: re-cut required (C2, C3 are top-three areas).

Pack-2 differences from pack-1: C2 2 (K3 victim absent from the key still, "who hit whom" unreadable there); C5 2 and C7 2 (lints computable from its own metrics); C11 3 (K3 key shows no victim).

## 2. Evidence by category (pack-1; pack-2 identical unless stated)

**C1 = 3.** L04/L05 pass; frame 0 is gameplay and the first hit is at 2.53 s (deadline 5 s). Best: S02 POV hit lands at f76 with victim h 0.55. Worst: S01 frames 0-48 read as three guarded poses on a ledge (gorilla cut by the right edge in the first 3 tiles of the strip, dragon only h 0.20-0.24, meteors are specks); the verb is not in the first 2 s.

**C2 = 3.** Hits: K1 hero glove to gorilla, both readable (S02 key: glove, gorilla h 0.55). K3 hero to dragon, both readable (S06 key h 0.39 / 0.29). K2 rock to penguin: key still (f154) shows no rock; the rock appears at the left edge at about shot f12 and the penguin is carried onto the fox and out of frame by f18, so attacker and contact are seen for 2-3 frames. K4 planet killer to hero readable. Label problems are in section 3.

**C3 = 3.** Order brawl, meteors, doom holds and L03/L08/L40 pass. Beats that do not raise: S05 (dragon never visibly charges), S03 (one rock, not "raining on every fight"), S07 (the anticipation of the planet killer does not read because the hero never looks up). S09 throws the hero but he rides a block (see section 4).

**C4 = 3.** Hero is yellow with red hair; L42 passes by count. S04 has no hero; S10/S11 hero h 0.01-0.02, not readable. The only yellow-on-yellow confusion: the panda in S03 key is also yellow, centre of frame, and reads as the hero before the red-haired one at the right. S07 reaction does not read (below).

**C5 = 4 (pack-2 = 2).** Best: seven measured onsets within 0.6 frames (feint +0.2, punch1 0.0 lane, wide -0.5, rock -0.4, punch2 0.0, main 0.0, title +0.1); title on hit f359; stop-down 297-308. Worst: S09 and S10 are 20 and 21 frames, a rush after the payoff, and the plan's `measured_onset_frame` for main is 307.5 against a 309 key (1.5 frames) while metrics say -0.3, an internal mismatch. Pack-2: punch1 +5.0 and main with no onset fail L11 (cap 2).

**C6 = 3.** Fit arithmetic checked (section 5): all true except S05. S08 has a blank-sky frame before the burst (strip tile at f308 shows empty sky and the fox, rock gone, then debris at f311); the edit note says source 309-310 were cut, but a rock-less frame still shows at 308. S07 camera is behind and left of the hero with the glove filling the lower-left third and the sky out of frame.

**C7 = 4 (pack-2 = 2), by numbers.** I -15.2 LUFS (target -15), peak -1.2 (limit -1.0), ramp +2.4 LU (plan 3; floor 2), title -8.6 LU vs body, no silence over 0.25 s, no black. Best: title drop -8.6 and all keys have an onset. Worst: ramp only just above the floor, and no stem table or per-lane loudness is in the pack (owed: cannot check). Voice line "WhatWasThat_02" is needs-ears. Pack-2: main has no onset (L12 fail), punch1 5 frames late.

**C8 = 3.** Stills are pre-title (S11-last has no text), so I judged from `filmstrip.png`: title in at f359 on the hit, held 91 frames (window minimum 90), readable. Worst: tagline is tiny and low-contrast against the dark overlay, thin for a phone feed.

**C9 = 3.** S08 blank frame at 308 (a pop of 1-2 frames before the burst). S02 distorted yellow arm (below). S09 hero standing on a rising block rather than flying. No black frames.

**C10 = 3.** All five must-show ids are present, 450 frames, 16:9, no giant hand. `survivors_react` proof says the hero "looks up at the planet killer and screams": S07 shows a raised arm and a head that does not tilt up; there is no scream visible.

**C11 = 4 (pack-2 = 3).** POV 124 of 359 body frames = 35% (S02 62, S05 28, S08 34). Victim height shares: K1 0.55 (POV, own glove in frame), K2 0.33 (third person, side-on), K3 0.39 (S06 key; dragon 0.39, hero 0.29), K4 POV (hero is the camera, only a glove at the bottom). Best: K1. Worst: K2 and the witnesses: fox is 0.12 and 0.008 area in S08, its S04 flinch is at shot f28, 13 frames after contact, and the panda/astro never react visibly. Pack-2: the K3 key still has no victim.

**C12 = 3.** Hero never performed by a person (waived). Best: S06 hero steps in and throws with a visible lunge. Worst: S09 hero holds one fixed pose (glove arm straight out, legs together) for all 20 frames; S05 dragon stands facing away at the block end for about 24 of 28 frames (strip: wings and stance unchanged until the last 4 tiles). The motion.csv (S05 dragon still run 5, level 0; S09 hero still run 1) disagrees with the strips, so the "measured on drawn poses" columns are not trustworthy for hands and body.

## 3. Shows labels and fit lines (true of the stills?)

- S01 `shows: several_survivors`, 3 readable: true (0.32 / 0.43 / 0.24, all over 0.08). `learns` "circle, feinting": they stand guard with small sways, weakly true.
- S02 `pvp_hit`, `knock_off`: true. Fit "0.48 at the contact frame": metrics key is 0.55 (first 0.51). 0.94 m / 1.9 m = 0.49, so the maker's number matches arithmetic but not the measured key.
- S03 `several_survivors`: true by numbers (hero .12, dragon .12, fox .13, penguin .20, panda .10). `learns` "Meteors start raining on every fight": false; one specked rock at the key and one large rock entering at the right edge in the last tiles.
- S04 `hazard_hit`: only true in the last third; the key still is pre-hit with no rock.
- S05 `learns` "The dragon charges you down the block": false. Key still (f194): dragon has his back to the camera, fox is firing a beam past him; dragon h goes 0.41, 0.34, 0.47 (first, key, last) because the POV turns, not because he approaches. Fit line says "0.47 at the key (metrics)": metrics key is 0.34; 0.47 is the last frame. 0.94 / 3.5 = 0.27, not 0.47. This fit line is false.
- S06 true; arm length natural.
- S07 `learns` "looks up, shading his eyes": false. His left arm is raised straight above the head (a wave), not a hand to the brow; head is a blank sphere seen from behind and left, with no readable direction. Plan says `face: 3q_front, yaw 30`; the still is a back three-quarter view.
- S08 `learns` true (rock fills the view from about f302). Plan says hero hands rising over his head: not seen; only the glove at the bottom.
- S09 `learns` "blast throws the hero off among the rocks": he is airborne at the key, but strip tiles 6-12 show him on top of a purple block moving with the camera.
- S10, S11 true. Hf: S10 52 x 24/20 = 62.4 (survivors 0.015: ok); S11 48 x 24/20 = 57.6: ok.

Hf checked: S01 2.9 (ok), S02 1.89 (ok), S03 12.1 x 24/31 = 9.37 (ok), S04 3.5 x 24/25 = 3.36 (ok), S06 3.1 x 24/22 = 3.38 (ok), S07 0.75 (ok), S09 3.4 (ok).

## 4. Specific questions

- **Punching arms stretch (S02, S06)?** S06: no, arm is natural length. S02: yes, apparently. The key still and strip tiles show a long, thin, tapered yellow arm running from the bottom edge through the glove's cuff ring, much thinner than the oversized foreground glove; it reads as a noodle. Part of this is perspective at 1.4 m, but it looks stretched, not natural.
- **S01 reads as three different people in motion, out of step?** Partly. Heads/arms sway at different times (rival action at f34, hero f12; peaks 237, 211, 325 deg/s at f28, f16, f26), so they are out of step. But the strip shows three guarded poses with small sways; the gorilla enters cropped, and the dragon is small. It reads as three stiff fighters, not as people already moving.
- **S07 reads head and eye direction?** No. Blank sphere head, seen from behind; the hair cap makes the rear visible, no tilt toward the sky; the raised arm reads as a wave or cheer.
- **Dragon visibly charges (S05)?** No (see section 3). He cocks the glove and turns only in the last few frames.
- **POV left hand varies?** Barely. S05: open palm in nearly the same shape in the key and strip (position drifts with the camera). S02: arm motion varies in the later tiles. S08: hand not visible.
- **Head turns over about 350 deg/s without being thrown?** Yes: S04 fox, 378 deg/s at shot f18, right after the rock hit (a snap, not thrown). Others over 350 are thrown actors: S02 rival 624, S04 penguin 623, S06 dragon 556. Under the line: S01 dragon 325, S03 astro 314.
- **Every hit, attacker and victim:** K1 hero glove to gorilla (both readable); K2 meteor to penguin (rock enters 1-2 frames before the penguin leaves; fox present); K3 hero to dragon (both readable); K4 planet killer to hero (hero is the camera).
- **Key event victim height / close or POV:** K1 0.55 POV; K2 0.33 close third person; K3 0.39 close third person; K4 POV (victim is the camera). No key event is covered by only a wide.
- **First-person share:** 35% of body frames (124/359), three shots, on K1 and K4.

## 5. Notes for categories at 3 or below (pack-1)

| Cat | Shot/frame | Symptom | Cause | Stage | Fix |
|---|---|---|---|---|---|
| C1 | S01 f0-48 | static guarded poses, specks for meteors | no pre-hit action in the take; wide lens | 5 (take) / 7 | start the feint earlier; frame the dragon larger |
| C2 | S05 key f194; S07 key f268 | label false; head unreadable | take: dragon walks away facing out; hero arm layer replaces the look | 5/6 | re-time the dragon's turn; drop the arm overhead, tilt the head up |
| C2 | S04 key f154 | no rock at key | key set before the hit | 6 | move key to f17-18 |
| C3 | S03, S05, S07 | beats do not raise | one rock in S03; no charge; no look-up | 5 | take with a visible rain and a charge |
| C4 | S04, S10-11; S03 | hero missing/tiny; yellow panda | casting | 5 | recolor the panda or add the hero |
| C6 | S05 fit; S08 f308 | false fit; blank frame | maker copied last-frame h; source drop of 309-310 incomplete | 8 | correct fit; drop 307-308 or trim |
| C9 | S02 arm; S08 f308; S09 | stretched arm; pop; hero on block | punch layer length; edit; physics | 7-8 | cap arm length; see above |
| C10 | S07 | no look-up or scream | layer | 6 | rebuild the reaction |
| C8 | S11 | tagline tiny | size | 9 | enlarge about 1.5x |
| C12 | S09, S05, S04 | frozen hero pose, standing dragon, near-still fox with a snap-turn | pose layer, no alive layer on thrown actor; csv not reflecting body | 5-6 | animate in flight; add the dragon's walk; stagger the fox |

## 6. Numbered concrete errors (pack-1, the real pack)

1. S05: the dragon does not visibly charge (key f194 shows his back to the camera); `learns` is false.
2. S05 fit line says dragon h 0.47 at the key; metrics give 0.34 (0.47 is the last frame); arithmetic gives 0.27.
3. S07: head and eye direction unreadable; the arm is overhead instead of shading the eyes; the planet killer is anticipated by nothing; `learns` false.
4. S02: the punching arm looks stretched into a thin noodle at the glove cuff.
5. S08: a rock-less sky frame at about f308 before the burst despite the edit note; a 1-2 frame pop.
6. S09: hero holds one pose for 20 frames and appears to stand on a rising block, not thrown.
7. S04: key f154 is before the rock; the contact is seen for about 3 frames; the fox flinches 13 frames late; the fox has a 378 deg/s non-thrown head snap at f18.
8. S03: `learns` "raining on every fight" is not true of the frames; six tiny survivors at 0.10-0.13.
9. S03: yellow panda reads as the hero.
10. motion.csv contradicts the strips (S05 dragon, S09 hero), so L53 PASS is unsound.
11. Plan `measured_onset_frame` for main is 307.5 against metrics -0.3; plan and metrics disagree.
12. Tagline too small; title window at its minimum 91 frames.
13. No stem table in the pack (C7 owed); ramp +2.4 is near the floor.
14. S01 reads as three poses, not people already in motion; gorilla cropped at the frame edge.

## 7. Top three problems by score impact

1. S05 and S07 (C2/C3/C12/C10): fix the dragon's charge and the hero's look-up, shading hand and scream; these touch four categories and clear two false labels.
2. S09, S04 and S01 actor aliveness (C12/C4/C11): hero frozen and on a block in S09; the fox's late flinch and snap-turn in S04; the static opening.
3. S08 f308 blank frame, S02 arm length and the S05 fit line (C6/C9): small fixes that clear the technical and fit lines.

## 8. Unverified

Sound by ear (all voice lines, punch and boom satisfaction), motion feel at 30 fps, VR comfort of POV turns (S05 49.7 deg/s camera turn, S09 150.9 deg/s at the last frame).
