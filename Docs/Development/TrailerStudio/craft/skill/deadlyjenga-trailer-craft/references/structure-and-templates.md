# Structure and templates

Open at stages 0-3 (brief, concept, beat sheet). Sources: report 01 (Derek Lieu's blog and Game
Developer posts, M. Joshua Cauller, Kert Gartner, Steamworks and Meta store docs), timed against the
measured data in report 06. Templates are inferred unless a line says measured.

## 1. Before cutting: decide the job

1. **Write one goal and one pitch line** before choosing any shot, in your own words for this
   brief (example A's tagline is reserved; lint L39). [sourced: Lieu]
2. **Name the hook by crossing out.** List the game's features; cross out every one another game
   has (co-op, physics, VR, candy art). What is left is the hook: one player is a giant hand pulling
   blocks out from under the others. A disaster cut's hook is what the disaster does to *this*
   tower and *these* tiny players, not the disaster alone (a draining sea is generic to tsunamis).
   [sourced: Lieu, "Finding the hook"; skill test 1 critic]
3. **Pick the concept type from the brief** (`hand`, `disaster`, `two_role`; SKILL.md). The brief
   decides whether the hand appears. Never add the hand to a "disaster is the star" brief.
4. **Copy the must-show list from the brief, with proofs.** One id per thing asked for, each with
   one line saying what in frame proves it. Suggested ids:

   | id | proof |
   |----|-------|
   | `pull` | the hand moves a block out of its slot, a survivor on or beside it |
   | `grip` | the hand closes on a block that has not moved yet |
   | `hand` | the hand, readable as a hand, with a survivor or block for scale |
   | `hazard_hit` | the hazard visibly moves blocks or survivors |
   | `survivor_face`, `survivors_react` | a readable face; a survivor visibly turns, runs, points or flinches |
   | `fall`, `into_sea` | a survivor leaves the tower and drops; tower pieces or survivors enter the water in frame |
   | `climbing` | a survivor climbs or jumps up a block |
   | `multiplayer` | 3 or more survivors acting independently in one frame, or a hand and survivors reacting to each other |
   | `friends` | two players visibly answering each other (a wave, a point, a voice line) |
   | `pov_hand`, `pov_survivor` | smoothed first-person from that side (hand fingers in frame; survivor eye height 0.85 m) |
   | `tower_over_ocean` | the tower with the sea around or below it |

5. **Generate before choosing.** Write at least five one-line "image people will remember" ideas,
   from different sources: a game rule turned against a player, a role reversal, a scale joke, a
   social moment between friends, an ending gag, a disaster's signature on this tower. The test's
   control plans had the inventive moments (a lobby role swap, a fingertip flick toppling the tower
   on the title hit, a survivor still clinging to a toppling block); those three are now spent, so
   find your own. Then pick. Rules constrain how a shot is made, never which idea wins: if the best
   idea breaks a soft rule, keep it and waive the rule; if it breaks a hard rule, say so to the
   human. [sourced: skill test 1]
6. **Pick the type and length, then the shape** (montage or single take) and the opening (staged
   or cold open). Write them in `concept.md`. [sourced: Lieu on labels; measured: two shapes for
   short cuts]
7. **Write two rejected concepts** that really differ from the chosen one: each changes at least two
   of shape, opening, payoff and ending, with one reason it lost. Two near-copies of the chosen
   concept do not count. [sourced: skill test 1, where all four plans shared one spine]

## 2. Trailer types

| Type | Job | Length | Must achieve |
|------|-----|--------|--------------|
| Teaser | curiosity, mood | 10-60 s | one striking image or question, the name; may withhold play (Lethal Company V85 is 27 s of withholding) |
| Announce / reveal | first impression: hook plus genre | 60-90 s | strong hook, title last. Measured: reveals cut faster (median shot 1.28 s), use more designed moves (21 %), ramp louder (+10 LU) |
| Gameplay | how it plays | 60-120 s | core loop in learning order; HUD on if it helps |
| Launch | remove hesitation | 60-120 s | faster recap, scope, "available now". Measured: gameplay by 0.5 s, verb by 4.2 s |
| Store page (first Steam or Quest slot) | let a scrubbing shopper judge | 30 s-2 min | gameplay from second 0; its first 6 s feed Steam's auto micro-trailer (six 1 s clips) |
| Social 15 / 30 / 60 | stop the scroll | 15, 30, 60 s | hook in 1-2 s, native 9:16 version, end card with one call to action |

Re-structure each length; never trim a long cut into a short one. A 9:16 version is re-framed per
shot (subject inside the centre 9:16 safe area), not cropped blindly. [sourced: Salters, TikTok, YouTube]

Platform checks (re-verify before submitting; from search excerpts of the store pages):
- Meta Quest store: 16:9 filling the frame, 2 minutes or less, no other platforms' hardware, a
  "Captured on Meta Quest 3" style label if captured on Quest 3.
- Steam: first trailer mostly gameplay; may autoplay muted, so the opening must read with sound off.

## 3. Order of ideas

Viewers ask, in order: what genre and what do I control; what is special; why does it matter; how
does it challenge me; how much is there; what is it called. Answer in that order. [sourced: Lieu,
"Audience questions"]

- Teach the core verb first, then add one layer per beat: teach, test, escalate, test harder.
  [sourced: Cauller]
- Hold a new mechanic until it reads (the first pull, or the first hazard hit, ≥ 1.5 s uncut, hard rule 6); shorten it only after.
- Show failure: someone falls. Perfect play makes a game look trivial. [sourced: Lieu, Sheepherds]
- Show the goal, not a task list. For a survivor the goal is "stay on the tower"; for the hand,
  "bring it down".
- Both roles are players. Somewhere in 30 s or more, show the same event from the hand's view and a
  survivor's view (Evolve sold 4v1 that way). [sourced]
- Extras (cosmetics, modes, map lists) only after the midpoint. [sourced: Lieu]
- Flashiest material last; simpler readable moments early. [sourced: Lieu]
- One main idea per shot; the `learns` line must be true when watched. [sourced]
- Make it read muted: picture carries the verb. Store pages and muted-first social cuts get one
  plain rule caption by default ("the hand is another player" style: a rule of play, ≤ 6 words, no
  superlative); other cuts add a caption only if a rule cannot be read from picture in 2 s. In skill
  test 1 the control plans' rule captions scored better on clarity for muted viewers. Captions are
  separate from the end card's tagline (≤ 8 words) and call to action (≤ 6). [measured: 11 of 22
  use captions; sourced: Lieu, skill test 1]

## 4. Beat sheets adjusted to the measured data

The sections are the ones hard rule 2 requires. Windows marked (h) are hard rules; the rest are
defaults.

### 15 s montage (body 11.0-12.0 s, 6-9 body shots)

| Time | Section | Content |
|------|---------|---------|
| 0.0-1.5 | hook | gameplay at frame 0 (h); a partial or effect view of the hook in motion, or a cold open on the threat itself (h: held ≥ 1.0 s); an open hit at 0 |
| 1.5-5.0 | teach | the hero's face (reaction); the core verb readable by 5.0 s (h) |
| 5.0-8.5 | escalate | 2-4 shots, each raising one thing (stakes, threat, count); the barrage lives here: ≥ 4 cuts in some 5 s window (h) |
| 7.5-8.5 | breath (optional) | a 0.3-0.5 s drop-out with one sound before the main hit |
| 7.5-10.5 | payoff | the key moment on the main hit (h), in the last 40 % of the body; the longest body shot (≥ 2.0 s, h) often lives here |
| ≤ 1.5 s | aftermath (optional) | what the payoff cost the hero |
| 11.0-12.0 → 15.0 | title | closing title starts in the window (h; default 11.5 s); title plus card 3.0-4.0 s (h); music drops ≥ 3 LU (h) |

The 15 s title window trades card length against position: the measured 80-95 % band would leave a
0.75-3.0 s card, shorter than 21 of 22 measured cards. 11.0-12.0 s (73-80 %) keeps a 3.0-4.0 s card.
Extrapolated: no 15 s montage was measured.

A button, if any, is picture under the card (a last block plopping into the water), not extra time.

### 15 s single take

One continuous camera carries hook, teach, escalate and payoff as `moments`; at most 2 cuts in the
body; the take covers ≥ 70 % of the body (h). Use one cut for the hero's face if the take cannot
reach it (hard rule 9). Title laid over the take or a cut to the card in the title window. Measured:
2 of 4 short spots are single takes (Human Fall Flat VR, Beat Saber).

### 30 s montage (body 24-26 s, 12-18 body shots)

| Time | Section | Content |
|------|---------|---------|
| 0-2 | hook | gameplay by 2 s; the hook partial or mid-action |
| 2-8 | teach | hero face; core verb readable by 8 s; a second role glimpse |
| 8-16 | escalate (wave 1) | each shot raises; one failure; one shot ≥ 3.0 s somewhere in the body (h) |
| 14-17 | breath (optional) | a held shot under a stop-down |
| 17-24 | escalate (wave 2), payoff | barrage of ≥ 4 cuts in 5 s (h); payoff on the main hit after 60 % of the body |
| 24-26 → 30 | title | closing title start in 24-26 s (h); card 4-6 s (h) |

Stop-down vs breath: the stop-down is a music event (≥ 0.3 s, ≥ 12 LU dip; L31 wants one at 30 s);
the breath is a picture section. One drop-out 0.3-0.8 s before the main hit satisfies L31 by itself;
a breath section may carry it or sit elsewhere. Two-role 30 s cuts: worked example C.

### 60 s (body 53-55 s, 22-32 body shots)

| Time | Section | Content |
|------|---------|---------|
| 0-6 | hook | gameplay by 2 s; a self-contained cold open that raises a question |
| 6-12 | teach | the core verb by 12 s (h; default by 8 s); hero; genre and perspective clear |
| 12-24 | teach / escalate | why the hook matters; both roles as players; first failure |
| 24-40 | escalate in waves | disasters as layers (one new threat per beat); variety after 30 s; shots shortening into the climax (default) |
| 40-42 | breath | stop-down (default 1-2 in the cut) |
| 42-53 | escalate, payoff | barrage; the payoff on the main hit; loudest section (+2 to +6 LU over the first quarter) |
| 53-55 → 60 | title | closing title in 53-55 s (h); card 5-7 s (h); one call to action |

Structural models to choose from (write the choice in `concept.md`): rising action (default for
reveal and launch), slow burn (teasers), music-video montage (simplest to most complex; suits a
physics game read at a glance), bullet point (card states a point, play proves it). [sourced: Lieu]

## 5. Concept sketches (15 s), not defaults

Three sketches to widen the field, beside the three fully worked examples (A in
`plan-schema-and-lints.md`; B and C in `worked-examples.md`). None is the default: start from the
brief and your own image ideas (section 1, step 5). Re-derive every number for your map and shot.

### A. "The Pull" (montage, `hand`, staged) - worked example A
Partial (a block slides, fingertips cropped, 0.0-1.5) → the hero looks up (1.5-3.5) → the whole hand
pulls the block beside her, verb at 4.5 s (3.5-5.5) → a barrage: another survivor falls, the hand
pinches a lower block, her block lurches (5.5-7.6) → 0.4 s drop-out → the crown topples on the main
hit at 8.0 s (7.6-10.5) → she hangs on (10.5-11.5) → title at 11.5 s, card 3.5 s.

### B. "Then the Weather Turns" (montage, `hand` with a hazard, two staged threats)
Hook: a tsunami rising behind a tower full of climbers, while the hand keeps pulling. Each threat
gets its own anticipation (rule 6).

| Time | Section | Shot |
|------|---------|------|
| 0.0-1.5 | hook | low over the water: the wave rising behind the tower; survivors busy, unaware (knows, tsunami) |
| 1.5-2.5 | teach | MCU: the hero glances back over her shoulder at the sea (reaction, tsunami; face) |
| 2.5-3.2 | teach | INSERT: a huge shadow slides over the block beside her (partial, hand) |
| 3.2-5.0 | teach | WS low: while everyone watches the sea, the hand pulls that block (whole, hand; verb at 4.4 s) |
| 5.0-6.0 | escalate (threat) | the wall of water hits the base; lower blocks scatter |
| 6.0-6.6 | escalate (stakes) | FS: a climber knocked off |
| 6.6-7.2 | escalate (count) | FS: two more slide toward the edge |
| 7.2-7.6 | breath | drop-out; one creak; the hero alone near the top |
| 7.6-10.5 | payoff | WS from the water framing the section the wave strikes (derive it: a long-lens crown frame cannot hold a mid-tower impact): the weakened tower folds into the wave on the main hit (8.0 s) |
| 10.5-11.5 | aftermath | the hero clinging to what is left (floating is unverified) |
| 11.5-15.0 | title | title over the wreck; card 3.5 s |

### C. "Be the Hand" (single take, `hand`, cold open from the hand's side)

| Time | Section | Content |
|------|---------|---------|
| 0.0-9.0 | take (moments: hook, teach, escalate, payoff) | smoothed hand-player POV: the player's own fingers above the tower at frame 0 (cold open, held); survivors scatter (consequence, 1.5); the fingers pull a block beside a survivor (verb at 3.5 s, held 1.5 s); a second pull lower down (escalate, consequence, 5.5); the crown tips on the main hit (7.0 s) |
| 9.0-11.5 | aftermath (one cut) | MCU, third person: the hero looking straight up at the hand (face) |
| 11.5-15.0 | title | card 3.5 s, the music dropping away |

The take is 9.0 of 11.5 s of body (78 %), one cut in the body. Needs a person performing the hand,
the POV smoothed (position 0.2-0.4 s, rotation 0.3-0.6 s), and a check of where the hand player's
eye sits (unknown).

## 6. Mistakes

| Symptom | Cause | Fix |
|---------|-------|-----|
| A flat list of events | no goal, no sections, clips in captured order | goal and hook first; beat sheet with sections and `raises` |
| Viewers leave in the first seconds | logos, slow flythroughs, setup before play | gameplay at frame 0; logos only on the end card |
| "I don't get what I'd do" | the verb cut before it completes; too many ideas per shot | hold the first pull; one idea per shot |
| Looks like every other game | generic features shown | lead with the hook |
| Repetitive | the same action more than three times | three at most, each with a twist |
| Game looks trivial | only perfect play | someone falls |
| Chaos reads as noise | no focal point | one focal point; cause, then effect |
| Ends limp | no payoff, title buried, several calls to action | payoff on the main hit, title, one call to action |
