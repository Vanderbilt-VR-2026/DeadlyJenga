# Reveals, readable chaos and moments

Open at stage 1 (concept) and stage 3 (when filling `reveal`, `cause_in`, `attacker`, `victim` and
the hero shots). Sources: report 04 (Derek Lieu's trailer breakdowns, Hitchcock and Truffaut via
Bordwell and No Film School, analyses of Jaws, Jurassic Park, Godzilla 2014, Cloverfield, Attack on
Titan, Shadow of the Colossus, and coverage of Evolve, Dead by Daylight, Goat Simulator, Fall Guys,
TABS) and report 01 (rules 20 and 40 on readable chaos: Lieu's Dear Passengers and Skin Deep posts,
Salters on "soupiness"). Report 04 watched no trailers, and report 06 did not measure reveal timing,
so every timing here is a default (inferred). Skill test 1 added one finding: the best-scoring hook
of eight plans was a cold open, a wave standing up at frame 0. Revision 3 (2026-11-20) moved the
hand's reveal to `legacy-hand.md` and added section 2 on readable chaos.

## 1. Rules

### Anticipation and payoff
0. **Choose the opening: staged or cold open.** Both are allowed; name the choice (`opening`).
   - *Staged* (default): a disaster's first full view comes after anticipation (rules 1-4). Required
     for any threat first shown after frame 0, including a second or third disaster in the same cut.
   - *Cold open*: frame 0 *is* the threat (the wave already standing, meteors already falling). Then
     the escalation comes from consequences: hold the opening view at least 1.0 s, then two or more
     escalate beats showing what it does, each raising something different (lint L08).
   - A fight needs no reveal: start inside it. What gets staged is the disaster that interrupts it.
   [sourced: Hitchcock, Lieu; skill test 1 for the cold open; 1.0 s inferred]
1. **Effect before cause.** The first sign of a disaster is what it does: blocks chattering before
   the quake, debris lifting before the funnel, a meteor's shadow, a tentacle tip breaking the
   water, the wave growing on the horizon. (The tsunami's height is zero during its warning in the
   code, so it grows as it comes in; what the warning shows on screen is unconfirmed, and there is
   no sea drawback.) Jurassic Park's water ripple, Jaws' barrels. [sourced]
2. **Sound before sight.** Start the threat's sound 0.5-1.5 s before its first image. This lead-in
   is its own sound event (`lead_in: true`); the 1-frame rule (L12, R03) binds only the sound that
   marks a key moment. [sourced; timing inferred]
3. **Let the audience know first (Hitchcock's bomb).** A shot where the viewer sees the threat and the
   survivors do not, for at least 1 s, gives seconds of suspense: friends brawling with the wave
   rising behind them. `reveal: knows`. [sourced]
4. **Reveal in stages: partial, reaction, whole.** A tentacle tip, then a survivor turning, then the
   Kraken over the tower with survivors for scale. [sourced]
5. **Keep withholding short.** In 15-30 s each disaster is seen whole by the midpoint of its own
   stretch; Lieu warns against hiding the real game. [sourced; midpoint inferred]
6. **A held beat before the payoff.** 0.3-1 s of near-silence or a single sustained sound, then the
   hit. [sourced; length inferred]
7. **Cause and effect in one frame or across one clean cut.** Every fall, knock-off, collapse or
   splash has its cause in frame or in the shot before (lint L08). Lieu moves objects closer than in
   the real game so the eye need not travel. [sourced]
8. **Show the mechanic plainly first, then vary it.** The first hit and the first hazard hit are
   uncut for 1.5 s or more (lint L09); twists come after. [sourced: Lieu, Sheepherds]

### Stakes and a person to care about
9. **One hero, established fast.** Name your own hero (example ids are reserved, lint L39): a
   brightly coloured survivor, front or three-quarter, doing something small within the first 3 s,
   and the same hero in the build and the payoff (L42). Confirm the colour in a still: the Studio's
   mark colour is a palette index. [sourced: Lieu on Arranger and Hades II; colour choice inferred]
10. **Vulnerability by position.** Put the hero on an edge or the top block with the sea below in
    frame. [sourced: Jaws, Shadow of the Colossus; application inferred]
11. **Give the outcome doubt.** A near miss, a win snatched away, a betrayal: suspense needs an
    outcome in doubt, and the reaction shot gets a payoff. [inferred]
12. **Someone must fail on screen.** At least one survivor is knocked off or goes into the sea.
    [sourced: Lieu]
13. **A reaction needs a body-sized shot.** The avatars have no drawn eyes or mouth. A reaction reads
    through head direction (looking up at the wave, turning to the attacker), body (cowering,
    flinching, arms up) and both hands, so frame MS or tighter with the head and both hands in, and
    carry it with a Studio reaction layer (`scream`, `cower`, `cheer`, `knockback`, `stagger`), an
    emote (the bots' "nope nope nope", "what was that", fist pump) or a performed pose. A hero at a
    fifth of frame height shows nothing (the pilot's 0.17). [sourced: pilot report; hand-reveal-2]

### Scale
14. **Every frame of a disaster holds a scale reference**: survivors at a similar depth or blocks
    (lint L09 for the first hazard hit; `camera.md` section 2). [sourced]
15. **Giant low, small high; giant slow, small fast.** [sourced practice; generalised]

### Comedy
16. **From real physics, not staged gags.** Captured moments beat manufactured dances (Lieu on Dear
    Passengers vs Banana Airways); TABS sold itself with GIFs of genuine wobble. [sourced]
17. **Setup, turn, hold, sound, cut.** Confidence, the shove, a held beat on the result, a tiny sound
    exactly on the visual, then cut. [sourced: Edgar Wright analysis; form inferred]
18. **Play danger straight, then undercut it.** Serious framing on candy peril (Goat Simulator's Dead
    Island parody); Fall Guys' cute look made its mayhem funnier. [sourced]

### Endings
19. **End on the strongest beat.** The button after the title is rare (2 of 22 measured); if there
    is a final gag, put it under the card. [measured]
20. **Cards: a few plain words, no superlatives.** "Epic" invites doubt. [sourced: Lieu]

### Reject the plan if
- the hook first appears after the first third; a threat is fully shown in the first second and
  nothing escalates from its consequences; no hero is ever followed; an effect has no cause; a hit
  has no visible attacker; every shot has the same intensity; a shot goes on with nothing new
  happening. [sourced: Lieu]

## 2. Readable chaos (revision 3)

The game's selling point is many things going wrong at once. Report 01 (rule 40): "unclear chaos is
usually a weakness. It becomes an asset only when the goal is clear and the chain of systems reads";
the test is that "in each chaos shot a viewer can say what caused what". The rules below make that
checkable. Every threshold is an estimate.

1. **Who did what to whom.** Every hit names its attacker and victim (`attacker`, `victim`), both
   readable in the frame, or the attacker in the shot just before (L41). A hit with no visible
   attacker reads as the game glitching.
2. **One focal hit per shot.** Other survivors may brawl in the background, but one hit (or one
   hazard hit) is in focus, on a thirds point, with its sound. [report 01 rule 20]
3. **Cause, then effect.** Glove, then flight, then splash; wave, then sweep. Never the splash first
   unless the cause comes in the next shot as a deliberate reveal, and then tag it. (L08)
4. **One new layer per beat.** A second disaster arrives on its own beat, with its own anticipation,
   after the first has been read (L43). The barrage is where things overlap, never the opening.
5. **The hero thread.** Through all of it the camera keeps finding the hero (L42). The viewer may
   lose track of the others; never of her.
6. **Geography.** One early wide shows where she is on the structure (L44); later shots keep one
   height cue (the sea below, the crown above). A jump in height is cut on a motion that carries
   the eye.
7. **Several readable at once.** A crowd shot counts only survivors at h ≥ 0.08 (L45); stagger them
   in depth; one of them is the focus.
8. **Knock-offs and eliminations read.** Hold 0.6 s after the hit, or pick the victim up in the next
   shot travelling the same way (L46); end on the splash or the candy burst, not on an empty edge.
   In the Studio deaths are off: plan the splash or a `death` reaction layer.

## 3. Recipes (defaults)

### Disaster reveal, staged (tsunami, tornado, Kraken)
Wrong calm, or better a fight (survivors busy, 1 s) → effect (the wave growing on the horizon, debris
lifting, a tentacle tip; a low roar under the music, 1-1.5 s) → a survivor notices: head and body
turn (0.7 s) → wide with the tower for scale (1.5 s) → held beat just before contact (0.5 s) →
impact and consequence (1.5-2 s). The core verb of a disaster cut is the impact moving blocks or
survivors, due by 5 s at 15 s: count backwards from it.

### Disaster reveal, cold open
The threat at frame 0, already huge (worked example B: the wave standing over three climbers; E:
meteors already falling) → the survivors react (0.5-1 s) → it grows or closes (scale) → it hits the
blocks under them (stakes, the verb) → more go into the sea (count) → the payoff on the main hit →
a reaction afterwards.

### The disaster as referee (D's twist: spent)
Two survivors fight → the bully wins a round (a knock-off) → the audience sees the disaster coming,
they do not → it takes the bully, spares the hero. Find a new version: a different hazard, a
different winner, a reversal.

### The betrayal (E's ending: spent)
The hero survives everything → a beat of triumph (a cheer layer) → a friend's one poke ends her.
The comedy is in the size difference between what she survived and what got her.

### The stack
One disaster per beat, each answered by the survivors (dodge, fight on, use it as a weapon): meteor
→ the hero dodges; earthquake → a rival falls; tornado → the rival with the rocket goes up. Each
arrival gets its own anticipation (L08, L43).

### Weapon meets disaster
A weapon's effect and a disaster's in one frame: a rocket fired into the funnel and thrown back, a
glove launch into the incoming wave. Check both are possible in one take (`game-content.md` section
9) before you fall in love with it.

### Comedy fail
A survivor takes a confident leap (1 s) → a meteor or a rival's shove takes exactly that block (0.5
s) → the full arc into the sea, wide, played straight (1-1.5 s) → near-silence (0.4-0.6 s) → a tiny
"plip" on the splash frame, cut (0.3 s).

### Other endings to choose from
The last survivor alone on the stump (D); the winner undone (E); the hero surfacing among floating
blocks (B); a gag under the card; the next round's countdown starting; the disaster still coming as
the title lands. Whatever the ending, plan one closing frame where the disaster (or the winner) and
the tower read together.

The giant hand's reveal, pull and two-role recipes: `legacy-hand.md`.

## 4. Five Deadly Jenga moments (game scale)

Camera numbers come from `camera.md` (survivor 0.94 m, block 6 x 2 x 2 m). They are starting
points: derive each shot's own `fit` line for your map and what the shot must contain (L38), and
check each cue in `game-content.md` section 9.

1. **The glove launch (2.5 s).** Two-shot at 3 m, 24 mm: the attacker draws the giant boxing glove
   (`gear`) → the hit (`fire`, a 22 m/s launch) → the next shot from the edge looking down: the
   victim sails out over the surf (h 0.09 at 10 m) → the splash on a hit.
2. **Tsunami over a brawl (7.5 s).** Low on the wall walk: friends fighting, the wave growing behind
   them (knows) → a survivor's head turns, a scream layer (reaction) → side-on wide from the beach:
   the wave hits the wall (whole) → held beat → the crest sweeps the fighters; one steps up a block
   and is spared. Use the `hazard` cue: the `tsunami` cue does not push survivors.
3. **The rocket miss (3.0 s).** Over the shooter's shoulder (35-50 mm): the hero across the crown →
   the rocket flies → she leaps; the blast (8 m) blows out her block → she lands on the next one, a
   "totally meant that" emote.
4. **Earthquake shove (4.0 s).** The quake starts (blocks chatter: effect) → two survivors stagger
   at the edge → one uses the stagger to shove the other off → the fall, tilt down to the splash.
5. **Tornado payoff (5.0 s).** A wide that holds the funnel and the crown (60-80 m) → debris lifts
   (effect) → the funnel crosses the crown; a survivor is pulled off (verify: lifting a live bot) →
   the hero clinging to the stump, a cower layer → title over the departing funnel.

## 5. Pre-render checklist

Answer each yes or no with the shot id. "Hard" items are lints; a "no" blocks rendering.

| # | Question | Lint |
|---|----------|------|
| 1 | Is gameplay on screen by 2 s, with no logo or black first? | L04 (hard) |
| 2 | Is the core verb (the first hit or hazard hit, with its scale or both parties in frame) readable by the class deadline? | L05 (hard) |
| 3 | For each disaster: anticipation before its first full view, or a declared cold open with consequences escalating after it? | L08 (hard) |
| 4 | Does every effect have its cause in frame or in the previous shot? | L08 (hard) |
| 5 | Is the first core-verb shot uncut for 1.5 s with a scale reference? | L09 (hard) |
| 6 | Does the payoff's key moment sit on the main hit? | L11 (hard) |
| 7 | Does the hero's reaction read through head, body and hands (MS or closer, facing within 60°, a named reaction)? | L14 (hard) |
| 8 | Does every hit name its attacker and victim, both readable? | L41 (hard) |
| 9 | Is the hero in the opening, the build and the payoff? | L42 (hard) |
| 10 | Mayhem: are a disaster hit and a PvP hit both shown by 60 % of the body? | L40 (hard) |
| 11 | Does each new disaster arrive on its own beat? | L43 (soft) |
| 12 | Does an early wide show where the hero is on the structure? | L44 (soft) |
| 13 | Do crowd shots keep three survivors readable? | L45 (soft) |
| 14 | Is every knock-off held or picked up by the next shot? | L46 (soft) |
| 15 | Does someone fail on screen, and does the hero's reaction get a payoff? | critic C4 |
| 16 | Does each disaster's sound start before its first image? | critic C7 |
| 17 | Is there a held beat (drop-out) before the payoff? | L31 (soft) for 30-60 s |
| 18 | Are POV shots smoothed? | L17 (hard) |
| 19 | Is there one held "screenshot" frame (hero small, disaster huge, a hit at its peak)? | critic C1 |
| 20 | Are cards a few plain words with no superlatives? | L32 (soft) |
| 21 | Is the tone both candy-bright and genuinely perilous? | human pass |
| 22 | Does any shot need something the game or Studio cannot do (a punch cue, a pushing `tsunami` cue, a drawn face, a sea drawback) without a `verify` and a fallback? | L19 (hard) |
