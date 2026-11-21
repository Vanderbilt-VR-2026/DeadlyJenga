# Stage 1 concept, disaster-pvp-1

## What the game really has (read from the code, 2026-11-20)

- **Disasters** (World/Hazards, all `IHazard`, started by GameSession or the Studio's `hazard` cue): Tsunami,
  Tornado (heading settable), Earthquake, Fire, Meteor Shower (size, spread, rocks per second, force, warning,
  duration), Planet-Killer Meteor (one rock 10-150 m across, aimed at the map centre, force 28, radius about
  0.8 x its size), Kraken, Acid Rain, Anti-Gravity, Cat Attack, Evil Santa, Icey, Ornament Shower, Planet-Killer
  Ornament, Power Outage, Vampire. Meteors push and damage anything they land beside (`BasicDisaster.Affect`).
  Their rocks and impacts are published as `DisasterFrame`s, which the Studio's hazard track records and plays back.
- **PvP**: 23 equipment kinds (Core `EquipmentKind`). The Boxing Glove punch does 80 damage and pushes the victim
  at 22 m/s plus 6 m/s up (`MatchModeRuntime.Equipment.AttackEquipment`), enough to clear the tower. Blaster plasma
  bolts, Rocket Launcher and Dual Uzi projectiles (Arsenal) are recorded by the Studio's effects track and redrawn.
  The Studio's `fire` cue fires a live actor's weapon at another actor through the real equipment command.
- **Mode in the Studio**: Demo rules with punches and health on, death off (`StudioRun.OpenRoutine`), so a survivor
  knocked into the sea keeps falling instead of vanishing.
- **Bots**: `Bot` marks run the normal bot brain; `Survivor` marks are stand-ins that stand and obey physics.
- **Reaction layers** (playback only): look-at, reach, cheer, scream, cower, knockback, tumble, stagger, fall.

## Images people will remember (seven)

1. **Sucker punch**: a yellow survivor in a boxing glove punches a rival clean off the top of the tower.
2. **Duel interrupted**: two survivors trading blaster bolts when a meteor lands between them and both fly.
3. **King of the hill**: a crowd of armed survivors stacked on the spire's top levels, everyone shooting at someone.
4. **The taunt**: the puncher cheers on the summit, oblivious, while the sky behind him fills with falling rocks.
5. **Planet killer**: one rock the size of the tower drops onto the summit; everything goes.
6. **Tornado duel**: two duellists fight on as a funnel peels the tower apart block by block behind them.
7. **The wall**: a survivor stops mid-fight, looks out to sea, and a 40 m wave is already standing up.

## Chosen concept: "Bigger Problems"

- **Logline**: they are so busy fighting each other on top of the tower that nobody notices the sky falling - until
  the last rock is the size of the tower.
- **Concept type**: `disaster` (the meteors are the star threat). PvP is the hook and the human story.
- **Opening**: cold open on gameplay, frame 0: the hero (yellow, boxing glove) faces the rival on the summit and
  punches him off the tower (images 1 and 4). PvP is readable in the first second.
- **Shape**: montage, 15 s class, about 9 body shots, with a short barrage in the escalation.
- **Arc in three words**: petty, swarmed, erased.
- **Beats**: hook (punch-off and taunt) -> teach (the whole top is a brawl; the first meteor lands among them) ->
  escalate 1 (count: the rain thickens, survivors are thrown) -> escalate 2 (stakes: rocks land beside the hero,
  barrage of short shots) -> stop-down (the hero looks up) -> payoff on the music's drop (the planet killer drops
  onto the summit) -> title over the wreck.
- **Hook appearance count**: the hero (and his glove) in the hook, in escalate 2 and at the payoff.
- **Title text**: "DEADLY JENGA" with the tagline "FIGHT YOUR FRIENDS. SURVIVE THE SKY." (6 words).
- **Why it wins**: the image only this game has is the combination - a PvP brawl on a Jenga tower with a disaster
  on top. It needs only things the code does: a glove punch, weapon fire, meteors, a planet killer. Every effect has
  its cause in frame (the punch, the rocks), and the escalation is a single rising line (one fight, a crowd fight,
  a rain, one giant rock), so a muted viewer follows it.

## Rejected concepts

1. **"Tornado Duel"** - single take: a slow orbit around two duellists on the summit while a directed tornado eats
   the tower behind them; ends on an empty sky where the summit was.
   Differs in shape (single take vs montage), opening (staged notice vs cold-open punch), payoff (funnel swallows
   vs impact) and ending (empty sky vs wreck under a title). Lost because a single take cannot give the face shot,
   the crowd and the punch, and the tornado's look in a Studio take is unverified.
2. **"The Wall"** - staged opening: the crowd brawls, one survivor stops and stares out to sea, reverse to a tsunami
   already standing; the wave hits on the drop; ends on the hero surfacing in the debris.
   Differs in opening (staged audience-knows vs cold open), payoff (wave vs falling rock) and ending (survivor
   surfacing vs wreck). Lost because the attempt-1 trailer already ended on a tsunami, swimming after a fall is
   unknown in the engine, and the wave needs its own long build that eats the PvP.

## To verify at the scout (capability scout before the plan)

| Item | Why it matters | Fallback |
|------|----------------|----------|
| the `fire` cue works on stand-ins in a whole-set take (glove, blaster, rocket, uzi) | the hook | pose-only punch (reach layer) plus a push cue on the rival |
| the punched rival really flies off the tower | the hook's payoff | knockback/tumble layers |
| meteors and impacts are drawn in playback | the core verb | Tornado or Earthquake |
| aimed meteors land where and when planned | sync with music | random shower, cut to where they landed |
| the planet killer reads as a rock the size of the tower | payoff and ending image | a large aimed meteor (15 m) |
| motion blur colour bug: cause and fix | the look | no motion blur |
