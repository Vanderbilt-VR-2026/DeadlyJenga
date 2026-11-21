# Structure and templates

Open at stages 0-3 (brief, concept, beat sheet). Sources: report 01 (Derek Lieu's blog and Game
Developer posts, M. Joshua Cauller, Kert Gartner, Steamworks and Meta store docs), timed against the
measured data in report 06. Templates are inferred unless a line says measured.

## 1. Before cutting: decide the job

1. **Write one goal and one pitch line** before choosing any shot, in your own words for this
   brief (example A's tagline is reserved; lint L39). [sourced: Lieu]
2. **Name the hook by crossing out.** List the game's features (`game-content.md`); cross out every
   one another game has (co-op, physics, VR, candy art, a battle royale). What is left is the hook:
   friends knock each other off a collapsing block tower while disasters tear it apart, and the sea
   is out. A disaster's hook is what it does to *this* tower and *these* tiny players, not the
   disaster alone (a big wave is generic); a fight's hook is that the floor itself is falling.
   [sourced: Lieu, "Finding the hook"; skill test 1 critic; the owner's direction, 2026-11-20]
3. **Pick the concept type from the brief** (`mayhem` by default, `disaster`, `pvp`; legacy `hand`
   and `two_role` in `legacy-hand.md`). The brief decides whether the hand appears; it never does
   by default.
4. **Copy the must-show list from the brief, with proofs.** One id per thing asked for, each with
   one line saying what in frame proves it. Suggested ids:

   | id | proof |
   |----|-------|
   | `hazard_hit` | the hazard visibly moves blocks or survivors |
   | `pvp_hit` | one survivor visibly strikes, shoots or shoves another and the victim moves, both in frame |
   | `knock_off` | a survivor leaves the structure because of a named attacker (a survivor or a hazard) |
   | `elimination` | the victim is out: the candy burst with "OUT!", or into the sea (Studio: verify, deaths are off) |
   | `weapon` | a weapon fired or swung with its effect in frame |
   | `several_survivors` | 3 or more survivors at h ≥ 0.08 in one frame |
   | `escalation` | a second (third...) disaster arriving on top of the first, in frame |
   | `survivor_face`, `survivors_react` | a readable face; a survivor visibly turns, runs, points or flinches |
   | `fall`, `into_sea` | a survivor leaves the tower and drops; tower pieces or survivors enter the water in frame |
   | `climbing` | a survivor climbs or jumps up a block |
   | `multiplayer` | 3 or more survivors readable and acting independently in one frame, or players visibly answering each other |
   | `friends` | two players visibly answering each other (a wave, a point, a voice line) |
   | `pov_survivor` | smoothed first-person at survivor eye height 0.85 m |
   | `tower_over_ocean` | the tower with the sea around or below it |
   | legacy: `pull`, `grip`, `hand`, `pov_hand` | `legacy-hand.md` section 2 |

5. **Generate before choosing.** Write at least five one-line "image people will remember" ideas,
   from different sources: a game rule turned against a player (into the sea is out), a betrayal, a
   disaster settling a fight, a weapon meeting a disaster (a rocket into a tornado), a scale joke, a
   social moment between friends, an ending gag, a disaster's signature on this tower. Spent ideas
   (do not reuse): a lobby role swap, a fingertip flick on the title hit, a survivor clinging to a
   toppling block, the wave taking the bully (D), the winner shoved off by a friend (E). Then pick. Rules constrain how a shot is made, never which idea wins: if the best
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
- Show the goal, not a task list: "stay on the tower and knock the others off", the mode's
  objective if the cut shows one (the hill, the flag, the lantern).
- Teach one hit and one disaster plainly before stacking them: a viewer who has seen one shove
  land and one wave hit can read the third thing at once. [sourced: Cauller; report 01 rule 40]
- Keep one hero through the chaos and let every other survivor be a rival or a victim we can name
  by colour. [sourced: Lieu on one hero]
- Extras (cosmetics, modes, map lists) only after the midpoint. [sourced: Lieu]
- Flashiest material last; simpler readable moments early. [sourced: Lieu]
- One main idea per shot; the `learns` line must be true when watched. [sourced]
- Make it read muted: picture carries the verb. Store pages and muted-first social cuts get one
  plain rule caption by default (a rule of play such as "the sea is out", ≤ 6 words, no
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
| 1.5-5.0 | teach | the hero's reaction (head, body, hands); the core verb readable by 5.0 s (h): the first hit or hazard hit, both parties in frame |
| 5.0-8.5 | escalate | 2-4 shots, each raising one thing (stakes, threat, count); the barrage lives here: ≥ 4 cuts in some 5 s window (h) |
| 7.5-8.5 | breath (optional) | a 0.3-0.5 s drop-out with one sound before the main hit |
| 7.5-10.5 | payoff | the key moment on the main hit (h), in the last 40 % of the body; the longest body shot (≥ 2.0 s, h) often lives here |
| ≤ 1.5 s | aftermath (optional) | what the payoff cost the hero |
| 11.0-12.0 → 15.0 | title | closing title starts in the window (h; default 11.5 s); title plus card 3.0-4.0 s (h); music drops ≥ 3 LU (h) |

The 15 s title window trades card length against position: the measured 80-95 % band would leave a
0.75-3.0 s card, shorter than 21 of 22 measured cards. 11.0-12.0 s (73-80 %) keeps a 3.0-4.0 s card.
Extrapolated: no 15 s montage was measured.

A button, if any, is picture under the card (a last block plopping into the water), not extra time.

### Moment (5-10 s, class `moment`)

The whole piece is the body; no title card unless the brief asks (then it comes after the payoff).
Sections: `hook`, optional `teach` and `escalate`, `payoff` at 50 % of the length or later, then
`aftermath` or `breath`. 1-6 shots, a median of 0.8-2.5 s, the longest ≥ 1.5 s, no barrage
(estimates). The core verb is still due by 5 s and still held 1.5 s. Example of the shape:
hand-reveal-2 (8.8 s: effect, reaction, whole, payoff at 57 %, consequence, final image), which
needed four waivers as a "15 s" and none as a `moment`.

### 15 s single take

One continuous camera carries hook, teach, escalate and payoff as `moments`; at most 2 cuts in the
body; the take covers ≥ 70 % of the body (h). Use one cut for the hero's face if the take cannot
reach it (hard rule 9). Title laid over the take or a cut to the card in the title window. Measured:
2 of 4 short spots are single takes (Human Fall Flat VR, Beat Saber).

### 30 s montage (body 24-26 s, 12-18 body shots)

| Time | Section | Content |
|------|---------|---------|
| 0-2 | hook | gameplay by 2 s; the hook partial or mid-action |
| 2-8 | teach | the hero; one hit and one disaster readable by 8 s |
| 8-16 | escalate (wave 1) | each shot raises; one failure; one shot ≥ 3.0 s somewhere in the body (h) |
| 14-17 | breath (optional) | a held shot under a stop-down |
| 17-24 | escalate (wave 2), payoff | barrage of ≥ 4 cuts in 5 s (h); payoff on the main hit after 60 % of the body |
| 24-26 → 30 | title | closing title start in 24-26 s (h); card 4-6 s (h) |

Stop-down vs breath: the stop-down is a music event (≥ 0.3 s, ≥ 12 LU dip; L31 wants one at 30 s);
the breath is a picture section. One drop-out 0.3-0.8 s before the main hit satisfies L31 by itself;
a breath section may carry it or sit elsewhere. A 30 s mayhem single take: worked example E.

### 60 s (body 53-55 s, 22-32 body shots)

| Time | Section | Content |
|------|---------|---------|
| 0-6 | hook | gameplay by 2 s; a self-contained cold open that raises a question |
| 6-12 | teach | the core verb by 12 s (h; default by 8 s); hero; genre and perspective clear |
| 12-24 | teach / escalate | why the hook matters; weapons; the first knock-off |
| 24-40 | escalate in waves | disasters as layers (one new threat per beat); variety after 30 s; shots shortening into the climax (default) |
| 40-42 | breath | stop-down (default 1-2 in the cut) |
| 42-53 | escalate, payoff | barrage; the payoff on the main hit; loudest section (+2 to +6 LU over the first quarter) |
| 53-55 → 60 | title | closing title in 53-55 s (h); card 5-7 s (h); one call to action |

Structural models to choose from (write the choice in `concept.md`): rising action (default for
reveal and launch), slow burn (teasers), music-video montage (simplest to most complex; suits a
physics game read at a glance), bullet point (card states a point, play proves it). [sourced: Lieu]

## 5. Concept sketches (15 s), not defaults

Three sketches to widen the field, beside the worked examples (D, E and B in `worked-examples.md`;
the legacy hand examples in `legacy-hand.md`). None is the default: start from the brief and your
own image ideas (section 1, step 5). Re-derive every number for your map and shot, and check every
cue against `game-content.md` section 9.

### A. "Hot Potato" (montage, `pvp`, staged)
A dropped rocket launcher lies on the crown. Hook: four survivors see it at once (0.0-1.5) → the
hero grabs it first (1.5-3.0) → she fires, the blast knocks a rival off (verb at 3.8 s, held 1.5 s)
→ barrage: the rival's friend punches her, she drops it, it changes hands twice (5.5-8.0, each
hit's attacker and victim in frame) → held beat: the last holder aims at the hero → payoff on the
main hit: the rocket misses and blows out the block under the shooter (8.5) → title over the hero
picking it up again. Needs: `gear` and `fire` (available); a weapon lying on the ground is missing on the
stage (pickup bubbles are off and no cue places one), so open with it already in a survivor's hands
(`StudioMark.equipment`) or verify; the hand-offs between bots (verify).

### B. "Slow Fall" (single take, `mayhem`, cold open on anti-gravity)
Anti-gravity is already on at frame 0 (18 % gravity, falls capped at 4 m/s): survivors leap in long
slow arcs between blocks (cold open, held 1.0 s) → the hero shoves a rival, who drifts slowly out
over the sea, a knock-off that reads for seconds (verb) → meteors start (a second threat, with its
own anticipation: a shadow on the blocks) → each impact sends blocks floating loose (count, stakes)
→ the payoff: anti-gravity's duration ends on the main hit and everything drops at once. One cut to
the hero's body reaction (a cower layer), the title over the splashes. Needs: two hazards in one
take, how anti-gravity reads on survivors (verify).

### C. "Kraken Referee" (montage, `mayhem`, staged)
A King of the Hill shove-fight on the top block (hook) → the hero holds the top (teach: two hits)
→ a tentacle rises behind them, unseen (knows) → it slams across the top level on the main hit and
everyone but the hero is swept off (payoff) → she looks around, alone and triumphant (reaction) →
a second tentacle curls up behind her (button under the card). Needs: the Kraken's side
(not directable: run variations), the King of the Hill HUD (not on the Studio stage: leave it out).

## 6. Mistakes

| Symptom | Cause | Fix |
|---------|-------|-----|
| A flat list of events | no goal, no sections, clips in captured order | goal and hook first; beat sheet with sections and `raises` |
| Viewers leave in the first seconds | logos, slow flythroughs, setup before play | gameplay at frame 0; logos only on the end card |
| "I don't get what I'd do" | the verb cut before it completes; too many ideas per shot | hold the first pull; one idea per shot |
| Looks like every other game | generic features shown | lead with the hook |
| Repetitive | the same action more than three times | three at most, each with a twist |
| Game looks trivial | only perfect play | someone falls |
| Chaos reads as noise | no focal point; hits with no visible attacker | one focal hit per shot; attacker and victim in frame or across one cut; cause, then effect (L41) |
| Nobody to follow | the camera jumps between strangers | one hero by colour, in the hook, the build and the payoff (L42) |
| Everything at once | all disasters in the opening | one new disaster per beat (L43) |
| Ends limp | no payoff, title buried, several calls to action | payoff on the main hit, title, one call to action |
