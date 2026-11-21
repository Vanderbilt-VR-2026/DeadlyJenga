# Critic D notes, Disaster PvP 2, stage-9 fine cut, critic-r2

## Verdict first
- Pack 2 is the real one. Pack 1 has three injected defects. Every other file (brief, concept, plan, lint, motion.csv, framing.md, take-log, all stills except S04-key) is byte-identical between the packs.
- Pack 1 defects:
  1. metrics.txt: `rock 154 (no onset within 10 frames)`. Pack 2 has `rock 154 mix -0.4 lane -0.4`. The K2 impact sound is gone. lint.txt still says L12 and L11 pass, so the lint was not re-run on the altered audio.
  2. metrics.txt: `title 359 mix +5.0 lane +5.0`, so the title sound is 5 frames late. The `title section vs body ... LU` line is replaced by a bare "title section mix +5.0 lane +5.0". The title drop (pack 2: -22.6 vs -14.4 LUFS, -8.1 LU) cannot be shown. Either the title is not quieter than the body or the number was removed.
  3. stills/S04-key.png in pack 1 is byte-identical to S04-last.png (same size, same md5). It shows the fox alone and no penguin, rock or hit. Pack 2's S04-key shows penguin and fox in a shoot-out with a rock at the top left. The `shows: hazard_hit` and `learns: A rock ploughs the penguin off his block` labels are false of that still.
- Scores below: pack 2 weighted 3.27, pack 1 weighted 2.69 (gap 0.58).

## Method checks (both packs, identical plan)
- Framing arithmetic (Hf = distance x 24 / focal), all true:
  - S01 4.0x24/28 = 3.43
  - S03 12.1x24/31 = 9.37
  - S04 3.5x24/25 = 3.36
  - S06 3.1x24/22 = 3.38
  - S07 1.6x24/50 = 0.77
  - S09 52x24/20 = 62.4
  - S10 48x24/20 = 57.6
  - POV fits 2 x d x tan34: S02 1.89, S05 3.51
- Declared `distance_m` disagrees with the fit line in two shots: S02 declares 1.6 but the fit uses 1.4; S05 declares 2.5 but the fit uses 2.6.
- Key frames add up: 48+28=76, 140+14=154, 208+10=218, 284+25=309, 110+12=122, 236+32=268.
- Plan says the main hit measured at 307.5 (offset -1.5 from 309). The sync table says `main -0.3 mix`. The two disagree in both packs.
- POV share: 62+28+34 = 124 of 359 body frames = 34.5%, matching the lint.
- Mix: I -15.2 LUFS, peak -1.4, ramp +2.2 LU. The plan asked for ramp 3; it measured 2.2, just over the +2 floor.
- Shows labels and fit lines that are NOT true of the stills (pack 2):
  - S04 `shows: hazard_hit`: the key still (154) has no contact and the rock is a speck at the top left. In the filmstrip the contact is a few frames later and the penguin simply is not there by frame ~158. The launch is not seen.
  - S08 `shows: hazard_hit`, `learns: planet killer fills your whole view`: at key 309 the still is empty sky with a small fox. The rock fills the view only before the key.
  - S04 `also_in_frame: hero, dragon` with actors and `acts_at` (hero@1, dragon@2): framing.md says on=0 for hero and dragon across all of S04. The L55 waiver cites "background hero and dragon (0.10-0.15) move at frames 1-2", which is false.
  - S09 plan lists only the hero as an actor and S10 lists `actors: []`. motion.csv shows visible actors in:
    - S05: astro, 7 frames
    - S08: fox and panda
    - S09: fox, panda, astro, 41 frames
    - S10: hero 12 frames, fox 50, astro 65
    These are undeclared, so L52 passes only on the declared list.
  - S09 hero "thrown with the blocks, arms flailing": h = 0.01-0.02, so not verifiable at 1080p.

## Per-hit table (pack 2)
| Hit | Attacker | Victim | Both readable? | Victim share | Close or POV |
|---|---|---|---|---|---|
| K1, S02 f76 | hero (own glove arm, yellow ribbon) | rival, grey gorilla | Yes; the glove lands at his head, he leaves toward the sea | 0.49 | POV |
| K2, S04 f154 | rock (shower) | penguin | No at the key. Rock at the top left, penguin and fox shooting. Contact not seen. | 0.33 (key), 0.37 in the plan | Close (side-on 3.5 m) |
| K3, S06 f218 | hero | dragon | Yes; the dragon's gloves are at his own face and the hero's arm stretches 3 m. Reads, but awkward. | 0.41 | Close |
| K4, S08 f309 | planet killer | hero | No. Rock fills the view, then one empty-sky frame, then debris. The hit on the hero or tower is never seen. | rock 1.0 before the key, sky at the key | POV |

- Closeness: every key event is "close" by number, but K4's contact frame shows nothing. The first-person share of the body is 34.5%.
- Hero by section:
  - hook S01: yes, h 0.36
  - teach S02: POV (arms only); S03: h 0.12 in a wide
  - escalate S04: absent; S05: POV; S06: h 0.47 / 0.25; S07: h 1.0
  - payoff S08: POV
  - aftermath/title S09-S10: 0.01-0.03, effectively not visible
- Hero colour is yellow (red spiky hair), true in the stills.
- Geography: S03 gives one clear wide, with the summit and block 2 and the sea below. After that the viewer is never told again where on the tower they are until S09. S04 is a side-on on block 2 and S06 on the summit. S09 and S10 are two near-identical wide wreck shots.
- Anyone who never reacts: the panda and astro in S03 and the fox in S08/S09/S10 are background only. No witness in S02, S06 or S08 reacts to the hit in frame.

## Scores, pack 2 (real)
| # | Score | Evidence |
|---|---|---|
| C1 Hook | 3 | Best: f0 is a readable three-way glove stand-off with a rock streaking in (L04 pass). Worst: subjects only h 0.30-0.38 in a static WS, the rock is a speck, and the first hit is at 2.53 s (K1, f76). The verb lands mid-window. |
| C2 Clarity | 3 | Best: S02 key (rival h 0.49, arm visible) and S06 key (both whole). Worst: S04 key (no contact), S08 key (empty sky), S09 (survivors 0.01). S03 does read five survivors (0.10-0.20). |
| C3 Story | 3 | Sections in order; the rocks are seen before the hit (S01, S03), and the planet killer is anticipated in S07 before S08. But the payoff's contact is never shown, S09 and S10 repeat the same wreck, and the final strongest image is a dark debris pile. |
| C4 Hero | 3 | Best: S07, a yellow hero with arms rising, reads. Worst: absent in S04, POV arms only in S02, S05 and S08, and invisible in S09-S10, so nobody wins or loses visibly. |
| C5 Rhythm | 4 | Best: all seven sync hits within 0.5 frame (mix and lane); stop-down 297-308; main at 69% of the body. Worst: S05 and S06 are both 28 frames; L22 is waived with CV 0.27; the main hit lands on an empty-sky frame. |
| C6 Shot craft | 3 | Best: S05 to S06 continuity (dragon left, hit, exits left) and S03's geography. Worst: S08 camera 25.92 m/s at the key frame; S09 and S10 repeat; S04 has no hero or dragon in frame. |
| C7 Sound (numbers) | 4 | Best: title -8.1 LU, peak -1.4, I -15.2, all keys within 0.5 frame. Worst: ramp +2.2 (plan asked for 3), no stem table, plan main -1.5 vs table -0.3. |
| C8 Titles | 4 | Best: title at f359 (hit +0.1), 91 frames, -8.1 LU. Worst: it fades in, so the full title is a few frames after the hit; the tagline is small and starts at 366. |
| C9 Technical | 3 | One pop. S08 strip: full-screen rock, then ~308-309 empty sky, then debris, coinciding with camera speed 25.9 m/s. The rock's stretched lava texture is flat at f~305. The glove arm is a thin 3 m ribbon in S02 and S06. |
| C10 Brief | 3 | Length and aspect right, first person present. The `hazard_hit` proof (meteor visibly hits the tower or survivors) is not seen at the S04 key or S08; the repeat sentence's "sky lands on you" is not shown. |
| C11 Immersion | 4 | Best: K1 POV 0.49, K3 0.41, POV 34.5%. Worst: K2 0.33 (hit not seen), K4 contact not seen, POV shows one hand (S05, S08), nobody reacts in frame to K1 or K3. |
| C12 Performance | 3 | Numbers pass (still max 9 f, level max 22 f). But: S01 hero@9 and rival@9, dragon@7; S01 30/32/35 again; S03 panda@25 astro@26; snap turns S04 fox 634 deg/s at shot frame 29 and S02 hero 430 deg/s at 47 (neither thrown); L56 waived on S02, S05, S07, S08, so all POVs and the close-up are driven, not performed. |

Weighted: 4.5+4.5+4.5+3+4+3+3+2+2.25+1.5+4+3 = 39.25; /12 = 3.27.

## Scores, pack 1 (injected)
Same as pack 2 except:
- C2 2. S04 key still shows no hit and the `shows` label is false (L41).
- C5 2. The rock hit has no onset and the title is +5 frames off its hit, so L11 fails: a computable hard fail, capped at 2.
- C7 2. L12 fails (K2 has no sound in [key-1, key+3]) and the title drop is not shown (R04).
- C8 2. The title sits 5 frames off its hit (L07) and its loudness is not shown.
- C11 3. K2's key frame is a lone fox, so the victim is not shown.
- Weighted: 4.5, 3, 4.5, 3, 2, 3, 1.5, 1, 2.25, 1.5, 3, 3 = 32.25; /12 = 2.69.
- The defective pack scores 0.58 below the real one, which clears the 0.25 gap. 3 of 3 defects found.

## Categories at 3 or below (pack 2): shot/frame, symptom, cause, stage, fix
- C1 S01 f0-76. Symptom: the stand-off is static and small and the first hit is late. Cause: the takes had a slow feint phase and a wide WS. Stage: 3, shot plan. Fix: open on the S02 POV hit or cut S01 to under 30 frames.
- C2 S04 f154 and S08 f309. Symptom: no contact visible at the key. Cause: the key frames were placed from take events, but the visible contact is 4-8 frames off. Stage: 7. Fix: move key and cut to the first contact frame and tighten.
- C3 S08-S10. Symptom: the payoff's contact is missing and the wreck repeats. Cause: the POV looks up and the rock fills the view. Stage: 3. Fix: add a 6-10 frame cutaway to the summit being hit and shorten S10.
- C4 S04, S09, S10. Symptom: hero absent or tiny after the payoff. Cause: cameras were placed on the victims and a 52 m wide. Stage: 3. Fix: hero reaction insert after S08.
- C6 S08 f309, S09/S10. Symptom: camera jumps and duplicate wides. Cause: POV follows the hero physically. Stage: 7. Fix: smooth the S08 POV or cut before the jump.
- C9 S08 f308-309. Symptom: pop. Cause: the rock despawns a frame before the debris. Stage: 7/8. Fix: cut to S09 at 307 or hold the last rock frame.
- C10 S04, S08. Symptom: `hazard_hit` is not proven. Cause: same as C2. Fix: same as C2.
- C12 S01, S03, S04. Symptom: synchronous starts, snap turns, driven POVs. Cause: committed take, waivers L55 and L56. Stage: 5 re-record. Fix: stagger the starts and perform S08 and S02 with a person.

## Concrete errors, real pack (pack 2)
1. S08 key f309 is empty sky (strip ~308-309: rock, blank, debris) with camera speed 25.92 m/s. The planet killer's contact, the main hit sound (307.5) and the title-defining payoff land on a blank frame.
2. S04 key f154 has no contact. The rock is a speck at the top left; the penguin vanishes between frames ~152 and ~158 with the launch not seen. `shows: hazard_hit` and `learns` are not true of the key.
3. The plan claims hero and dragon are visible in S04 (acts_at 1 and 2, waiver text "0.10-0.15"). framing.md shows on=0 for both.
4. Undeclared visible actors: S05 astro (7 f); S08 fox and panda; S09 fox, panda and astro (41 f); S10 hero (12 f), fox (50 f) and astro (65 f). The plan lists only the hero in S09 and nobody in S10.
5. S09 and S10 are both wide, nearly identical shots of the wreck. S10 takes 91 frames (20% of the cut) and S09 shows survivors at 0.01-0.03.
6. Snap turns: S04 fox 634 deg/s at shot frame 29 (trailer 169) and S02 hero 430 deg/s at shot frame 47 (trailer 95); neither is a throw.
7. S01 hero@9, rival@9 and dragon@7 start together; the second beat is 30/32/35. S03 panda@25 and astro@26. All waived.
8. The plan's main measured onset is 307.5 (-1.5 vs 309); the sync table says -0.3. The plan asked ramp 3 LU and measured +2.2.
9. The glove arm is a thin, stretched 3 m ribbon in S02 and S06 and reads as a rendering artifact.
10. The declared distance_m does not match the fit line in S02 (1.6 vs 1.4) and S05 (2.5 vs 2.6).
11. S06 `acts_at` for the hero is 25, after the contact at key 10; no counter-punch start is recorded.
12. L56 is waived on four shots, so S02, S05, S07 and S08 are driven, not performed.
13. S07 `learns` says "something huge", but no planet killer or sky object is in the frame; it relies on the pose alone.

## Top three by score impact
1. The S08 payoff frame: restore a visible impact and fix the blank/pop frame (C2, C3, C9, C10, C11; about +0.4).
2. K2 at S04: put the key and contact on the same frame so the penguin hit shows (C2, C10; about +0.25).
3. Hero visibility and aliveness: add a hero reaction after K4, stagger starts, remove the snap turns, and perform the POVs (C4, C12; about +0.25).

## Owed / unverified
- Sound by ear, the stem table and the full-rate feel were not judged. The stills show no faces, so reactions read only through head, body and hands.
