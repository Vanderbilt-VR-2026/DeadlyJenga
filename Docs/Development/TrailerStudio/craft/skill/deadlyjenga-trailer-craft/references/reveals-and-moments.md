# Reveals and moments

Open at stage 1 (concept) and stage 3 (when filling `reveal`, `cause_in` and the hero shots).
Sources: report 04 (Derek Lieu's trailer breakdowns, Hitchcock and Truffaut via Bordwell and No Film
School, analyses of Jaws, Jurassic Park, Godzilla 2014, Cloverfield, Attack on Titan, Shadow of the
Colossus, and coverage of Evolve, Dead by Daylight, Goat Simulator, Fall Guys, TABS). Report 04
watched no trailers, and report 06 did not measure reveal timing, so every timing here is a default
(inferred). The structure checks are hard rules 6 and 9. Skill test 1 (2026-11-20) added one
finding: the best-scoring hook of eight plans was a cold open, a wave standing up at frame 0.

## 1. Rules

### Anticipation and payoff
0. **Choose the opening: staged or cold open.** Both are allowed; name the choice (`opening`).
   - *Staged* (default): the threat's first full view comes after anticipation (rules 1-4). Required
     for any threat first shown after frame 0, including a second threat in the same cut.
   - *Cold open*: frame 0 *is* the threat (the wave already standing over the climbers, the fingers
     already closing). Then there is no anticipation to build, so the escalation must come from
     consequences: hold the opening view at least 1.0 s, then two or more escalate beats showing what
     the threat does, each raising something different (stakes, count, scale). Use it when the threat
     itself is the most striking image the game has and the cut is short (lint L08).
   [sourced: Hitchcock, Lieu; skill test 1 for the cold open; 1.0 s inferred]
1. **Effect before cause.** The first sign of the hand or a disaster is what it does: a shadow over
   the blocks (seen in the pilot render), a wobble, a creak, the wave rising in place during its
   warning time. Jurassic Park's water ripple, Jaws' barrels, Shadow of the Colossus' dust and
   stomps. (The sea drawing back before a tsunami is not in the game's code: unverified.) [sourced]
2. **Sound before sight.** Start the threat's sound 0.5-1.5 s before its first image. This lead-in
   is its own sound event (`lead_in: true`); the 1-frame rule (L12, R03) binds only the sound that
   marks a key moment, so the two do not conflict. [sourced; timing inferred]
3. **Let the audience know first (Hitchcock's bomb).** A shot where the viewer sees the threat and a
   survivor does not, for at least 1 s, gives seconds of suspense instead of one of surprise.
   `reveal: knows`. [sourced]
4. **Reveal in stages: partial, reaction, whole.** Fingertips at the top of frame or a block sliding
   on its own, then a face looking up, then the whole hand with a survivor for scale. Attack on
   Titan's hand on the wall, then the frozen onlookers. [sourced]
5. **Keep withholding short.** In 15-30 s the whole hand is seen by the midpoint. Godzilla's long
   delay split critics; Lieu warns against hiding the real game. [sourced; midpoint inferred]
6. **A held beat before the payoff.** 0.3-1 s of near-silence or a single sustained sound, then the
   hit. [sourced; length inferred]
7. **Cause and effect in one frame or across one clean cut.** Every fall, collapse or splash has its
   cause in frame or in the shot before (lint L08). Lieu even moves objects closer than in the real
   game so the eye need not travel. [sourced]
8. **Show the mechanic plainly first, then vary it.** The first pull is uncut for 1.5 s or more
   (lint L09); twists come after. [sourced: Lieu, Sheepherds]

### Stakes and a person to care about
9. **One hero, established fast.** Name your own hero (example ids are reserved, lint L39). A specific, brightly coloured survivor, three-quarter or front,
   doing something small, within the first 3 s; the same hero returns later (lint L14 checks the
   face). [sourced: Lieu on Arranger and Hades II; colour choice inferred]
10. **Vulnerability by position.** Put the hero on an edge or the top block with the ocean below in
    frame. [sourced: Jaws, Shadow of the Colossus; application inferred]
11. **A near miss the hero survives**: the block goes, she catches the edge. Suspense needs an
    outcome in doubt, and the face shot gets a payoff. [inferred]
12. **Someone must fail on screen.** At least one survivor falls. [sourced: Lieu]
13. **Give each side a point of view.** Somewhere in 30 s or more, the hand player's view and a
    survivor's view of the same block at the same moment. In a `two_role` cut both roles are seen as
    players before half the body (L36). [sourced: Evolve, Dead by Daylight]
13a. **A reaction needs a face-sized shot.** A hero at about a fifth of frame height (the pilot's
    0.17) shows no readable reaction; cut to MS or tighter for it. [sourced: pilot report]

### Scale
14. **Every frame of the hand holds a scale reference**: a survivor at a similar depth or a block
    (lint L09 for the first pull; `camera.md` section 2 for the geometry). [sourced]
15. **Giant low, small high; giant slow, small fast.** [sourced practice; generalised]

### Comedy
16. **From real physics, not staged gags.** Captured moments beat manufactured dances (Lieu on Dear
    Passengers vs Banana Airways); TABS sold itself with GIFs of genuine wobble. [sourced]
17. **Setup, turn, hold, sound, cut.** Confidence, the pull, a held beat on the result, a tiny sound
    exactly on the visual, then cut. [sourced: Edgar Wright analysis; form inferred]
18. **Play danger straight, then undercut it.** Serious framing on candy peril (Goat Simulator's Dead
    Island parody). [sourced]

### Endings
19. **End on the strongest beat.** The button after the title is rare (2 of 22 measured); if there
    is a final gag, put it under the card. [measured]
20. **Cards: a few plain words, no superlatives.** "Epic" invites doubt. [sourced: Lieu]

### Reject the plan if
- the hook first appears after the first third; the threat is fully shown in the first second and
  nothing escalates from its consequences afterwards; no face is ever seen; an effect has no cause; every shot has the same intensity; a shot goes
  on with nothing new happening. [sourced: Lieu]

## 2. Recipes (defaults)

### Giant-hand reveal
| Step | What | 15 s | 30 s |
|------|------|------|------|
| Partial or effect | a block slides on its own, fingertips cropped, or a shadow crosses the blocks; creak first | 1.0-1.5 s | 2.0 s |
| Reaction | the hero's face turns up; the music thins | 0.7-2.0 s | 1.2 s |
| Whole | the hand pulls a block beside the hero; hero on a lower third, hand breaking the top edge; low camera (0.3-0.9 m up), survivor distance ≥ 0.6 x hand distance | 1.5-2.0 s | 2.5 s |
| Escalate | the next pulls, each worse | 1.5-2.5 s | 4-6 s |
| Held beat | drop-out, one creak | 0.3-0.5 s | 0.8 s |
| Payoff | the crown topples on the main hit; locked-off long lens on the crown | 2.0-2.5 s | 2.5-3.0 s |
The worked example in `plan-schema-and-lints.md` is this recipe with exact frames.

### Disaster reveal, staged (tsunami or tornado)
Wrong calm (survivors busy, flat sea, 1 s) → effect (the wave rising in place on the horizon during
its warning time, a low roar under the music, 1-1.5 s) → one survivor notices and points (0.7 s) →
wide with the tower for scale (1.5 s) → held beat just before contact (0.5 s) → impact and
consequence (1.5-2 s). The core verb of a disaster cut is the impact moving blocks or survivors,
due by 5 s at 15 s: count backwards from it. In a `hand` trailer that also has a disaster, the hand
pulls a block while everyone watches the sea (audience-knows), and the hand gets its own partial
view first.

### Disaster reveal, cold open
The threat at frame 0, already huge (worked example B: the wave standing over three climbers) →
the climbers react (0.5-1 s) → it grows or closes (scale) → it hits the blocks under them (stakes,
the verb) → more go into the sea (count) → the payoff on the main hit → a face afterwards. No hand
unless the brief asks for one.

### Hero survives (one of several endings; example A uses it, so vary it)
Hero on a block, front view (1 s) → the hand grips that block (1 s) → the block goes; she drops and
catches an edge (1 s, 0.5 x optional; a ledge hang is unverified in the game) → held beat, someone else falls past (0.7 s) → she pulls up and
looks at the hand (1 s).

### Comedy fail
A survivor takes a confident leap (1 s) → the hand slides out exactly that block (0.5 s) → the full arc
into the ocean, wide, played straight (1-1.5 s) → near-silence (0.4-0.6 s) → a tiny "plip" on the
splash frame, cut (0.3 s).

### Two roles, one block
Hand POV, smoothed: the player's fingers pinch a block (1.5 s) → match cut on the same instant from a
survivor looking up at the same fingers (1.5 s) → third-person wide with both (1.5 s). Same block, same
moment, both views (`same_event`, L37). A POV inside a barrage may be 0.5-1.0 s if the same POV was
held ≥ 1 s earlier (L35). Worked example C uses the order survivor-then-hand.

### Other endings to choose from
The hero surfacing among floating blocks (B); the two players waving at each other (C); a gag under
the card; the stump with the hand still over it; the next round starting. Whatever the ending, plan
one closing frame where the hand (or hazard) and the tower read together (the pilot had none).

## 3. Five Deadly Jenga moments (game scale)

Camera numbers come from `camera.md` (survivor 0.94 m, hand about 4 m, block 6 x 2 x 2 m). They are
starting points: derive each shot's own `fit` line for your map and what the shot must contain
(L38), never copy them.

1. **The hand reveal (6.5-7.0 s).** Block slides out with fingertips cropped (INSERT, 4 m from the
   block, 35 mm) → hero MCU looks up (1.0 m, 50 mm) → tilt-up from the hero's ledge to the whole hand
   pulling the next block (camera 0.5 m up, hero 3.5 m away, hand 5.5 m, 24 mm) → wide of the crown with
   the hand over it (long lens from the water).
2. **Tsunami reveal (7.5 s).** Low over the water (0.5 m up, 24 mm): survivors climbing, flat sea → the
   wave rises in place on the horizon with a deep roar (a sea drawback is unverified) → a survivor stops and points, face visible → from behind the tower,
   the wall of water on the horizon (85-135 mm) → held beat before contact → impact; the hero clings to
   the top block.
3. **Hero survives (6.0 s).** Hero mid-tower, front → the hand grips her block (hero in frame) → yanked;
   she catches the block below → held on her dangling, another survivor falls past → she pulls up and
   looks straight up at the hand.
4. **Comedy fail (4.5 s).** A confident run and leap → the hand slides that block out mid-jump → wide, the
   whole arc into the ocean (about 2 s of fall from 20 m) → silence → "plip" → the hand holding the
   block, a small wave of a finger.
5. **Two roles and the end (7.5 s).** Hand POV pinching a block → survivor's view of the same fingers →
   third-person wide of both → the crown collapses; title over the splash → under the card, the hero
   bobbing in the water as one block plops beside her.

## 4. Pre-render checklist

Answer each yes or no with the shot id. "Hard" items are lints; a "no" blocks rendering.

| # | Question | Lint |
|---|----------|------|
| 1 | Is gameplay on screen by 2 s, with no logo or black first? | L04 (hard) |
| 2 | Is the concept's core verb (pull with a survivor, or the hazard moving blocks or survivors) readable by the class deadline? | L05 (hard) |
| 3 | For each threat: anticipation before its first full view, or a declared cold open at frame 0 with consequences escalating after it? | L08 (hard) |
| 4 | Does every effect have its cause in frame or in the previous shot? | L08 (hard) |
| 5 | Is the first core-verb shot uncut for 1.5 s with a scale reference? | L09 (hard) |
| 6 | Does the payoff's key moment sit on the main hit? | L11 (hard) |
| 7 | Is the hero's face readable (MS or closer, facing within 60°)? | L14 (hard) |
| 8 | Does someone fail on screen, and does the hero's face get a payoff (a near miss, a laugh, a loss)? | critic C4 |
| 9 | Does the threat's sound start before its first image? | critic C7 |
| 10 | Is there a held beat (drop-out) before the payoff? | L31 (soft) for 30-60 s |
| 11 | Is the whole hand seen by the midpoint? | critic C3 |
| 12 | Does every frame of the hand hold a survivor at a similar depth or a block? | R01 numbers, critic C6 |
| 13 | For 30 s and longer: are both roles shown as players? (`two_role`: by half the body) | L36 (hard for two_role), critic C3 |
| 14 | Are POV shots smoothed? | L17 (hard) |
| 15 | Is there one held "screenshot" frame (hero small, threat huge, action at its peak)? | critic C1 |
| 16 | Are cards a few plain words with no superlatives? | L32 (soft) |
| 17 | Is every shot showing something new (no shot goes on too long)? | critic C3 |
| 18 | Is the tone both candy-bright and genuinely perilous? | human pass |
| 19 | Does any shot show something the game cannot do (two hands, a sea drawback) without a `verify` and a fallback? | L19 (hard) |
