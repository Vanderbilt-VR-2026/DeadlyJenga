# What the game can show, and what the Studio can direct (read 2026-11-20)

Open at stage 0-1 (what to pitch), stage 3 (every `needs` entry) and stage 5 (what to dry-run).
Read from code only, nothing seen in the engine for this file. Game: the central checkout
(`Documents\Codex\Projects\DeadlyJenga`, branch `Evan_Sprint_4`, HEAD `9342eff8`), read only; paths
are relative to `Assets/_DeadlyJenga/`. Studio: worktree `deadlyjenga-trailer-pilot`, branch
`claude/trailer-hand-reveal-pilot`, HEAD `164e7115`, paths relative to
`Assets/_DeadlyJenga/DevTools/Editor/TrailerStudio/`. **Unconfirmed** means the code suggests it
and nobody has seen it in a Studio take; treat it as `needs: verify`. Re-read the code when the
commit is newer and date your changes here.

## 1. The pitch the code supports

Survivors (players and bots) on a block tower or structure over the sea. Disasters arrive on a
timer or by hand and stack. Survivors punch, shove, shoot and launch each other off. Falling into
the sea eliminates a survivor. Weapons and power-ups lie on the map. The giant flying hand is now a
**30-second power-up** a survivor can pick up (`FLYING HAND · 30 SECONDS`,
`Core/Session/EquipmentSpecifications.cs:38`; `GameSession/Modes/MatchModeRuntime.Equipment.cs:150`
turns the picker into a hand for 30 s), plus AI hand bots and a developer "Become Hand" tile. Lead
with disasters and player-versus-player mayhem; the hand is an optional extra.

## 2. Disasters

Every disaster is a prefab with an `IHazard` script and a `HazardType` asset `<Name>.asset` in its
folder under `World/Hazards/`; `World/Hazards/Resources/DisasterCatalog.asset` lists them. Settings
are numbers in the order the disaster lists them (`BasicDisaster.cs:41-80` for most).

| Disaster | What the code does | Settings (default) | File | Doubts |
|----------|--------------------|--------------------|------|--------|
| Tsunami | a wave grows over `riseTime` after the warning while it approaches, pushes and lifts players and blocks, passes and falls | height 1-100 m (22), offshore 160 m, warning 5 s, rise 4 s, speed 8 m/s, push 9 m/s², lift 5 m/s², carried speed ≤ 12 m/s, damage 0/s | `World/Hazards/Tsunami/TsunamiHazard.cs:12-38, 114-131, 212-213` | **Correction:** its height is zero during the warning (lines 123-124), so it does not "rise in place" before it moves; what the warning looks like is unconfirmed. No sea drawback in the code |
| Tornado | a funnel travels a route, pulls blocks and players in | height 10-160 m (65), funnel 3-50 m (16), ferocity 0-40 m/s² (7), pull-in 5-120 m (28), warning 3 s, duration 25 s | `World/Hazards/BasicDisaster.cs:57-61`, `TickTornadoBlocks`, `TickTornadoPlayers` | a settable arrival side (`IDirectedHazard.Heading`) exists **only on the pilot branch** (its `BasicDisaster.cs:286-295`); the central checkout picks a random side. Lifting a survivor into the funnel: unconfirmed |
| Earthquake | shakes the blocks; riders move with their support | accel 0-25 m/s² (8), 0.2-8 Hz (2), vertical 0-1 (0.25) | `BasicDisaster.cs:67-70, 340` (`TickEarthquake`) | how big the shake reads on screen: unconfirmed |
| Meteor Shower | meteors fall over the area and hit blocks and survivors | diameter 0.5-15 m (3 ±1), 0.5-40 per s (12), impact 0-40 m/s (12) | `BasicDisaster.cs:51-55`, `BasicDisaster.Meteors.cs` | not aimed: impacts land where they land |
| Planet-Killer Meteor | one huge meteor | diameter 10-150 m (45), no duration | `BasicDisaster.cs:56` | excluded from automatic picks |
| Ornament Shower, Planet-Killer Ornament | the meteor code dressed as Christmas ornaments | ornament 4 m, 2 per s, 18 s | `World/Hazards/OrnamentShower/OrnamentShowerHazard.cs` | - |
| Fire | burning blocks damage survivors on contact and spread | 0-50 HP/s (8), spread after 2 s of contact, 8 seed blocks, burn 30 s, smoke 20 s | `BasicDisaster.cs:62-65`, `BasicDisaster.Fire.cs`, `Fire/FireScars.cs` | - |
| Acid Rain | damage to exposed survivors | 0-40 HP/s (6), 180 drops/s | `BasicDisaster.cs:71-73` | shelter matters (`IsSheltered`) |
| Anti-Gravity | most gravity removed, slow falls | gravity 0.05-1 x (0.18), fall ≤ 4 m/s | `BasicDisaster.cs:74-76, 376` | - |
| Icey | blocks turn slippery (a toggle) | slipperiness 0.5-1 | `BasicDisaster.cs:66` | - |
| Power Outage | lights out, some neon blocks glow | 24 neon blocks, rim glow 9 m | `BasicDisaster.cs:44-46` | any gameplay effect beyond lighting: unconfirmed |
| Vampire | sunlight damages exposed survivors | 0-40 HP/s (9) after 0.55 s exposure, sun elevation 45° | `BasicDisaster.cs:47-50` | excluded from automatic picks |
| Kraken | a candy octopus rises beside the tower; tentacles GRAB a block and drag it into the sea, or SLAM across one level, knocking players and blocks flying | - | `World/Hazards/Kraken/KrakenHazard.cs:7-13, 139, 157-169, 209-254` | which side it rises on: not directable |
| Cat Attack | a giant cat crosses the island on a fixed line; a 22 m box knocks blocks and survivors | force 12 m/s, damage 25, warning 5 s, 24 s | `World/Hazards/CatAttack/CatAttackHazard.cs`, `TreeCreatureHazard.cs:12-15, 52-70` | direction fixed in code |
| Evil Santa | two grabbers lift blocks, then slam | force 12, damage 25 | `World/Hazards/EvilSanta/EvilSantaHazard.cs:34-50` | the look: unconfirmed |
| Time Slip | retired (the tower jumps back a few seconds) | - | `World/RetiredDisasters/TimeSlip/` | not in the catalog |

Scheduling: Survival starts disasters automatically every 20 s (`GameSession/Modes/ModeSimulation.cs:62`);
Demo and Survival share a scheduler with an 8 s warning and at most two active, gentle ones first,
fire, meteors and acid rain later, Planet Killer and Vampire never picked automatically
(`GameSession/Modes/README.md`). In Murder Mystery the sheriff vetoes one of three disasters and the
murderer picks one (README). Other modes have the `automaticDisasters` rule field, off by default.

## 3. Survivors against survivors

| Thing | Code fact | File |
|-------|-----------|------|
| Punch / shove | on in every mode except Survival and Demo by default; damage 18 (Mystery 8, Ninja 22); the shove is about `sqrt(2 g d) x 0.65` sideways + 0.8 m/s up, d = 3 m by default (CTF 1.8, Mystery 0.7, Ninja 2.2) | `GameSession/Modes/MatchModeRuntime.Actions.cs:52-59`, `Core/Session/ModeContracts.cs:23-26`, `ModeSimulation.cs:58-72` |
| Built-in PvP dagger | King of the Hill, Block Control, CTF, Team Tickets, Ninja | `ModeSimulation.cs:71` |
| Weapons (one slot, pickups, swap, drop on death) | Alien Blaster (35 dmg, 28 m); Sheriff Revolver (85, 90 m, or 7 pellets); Dual Rifle / Shotgun (55, 65 m); **Giant Boxing Glove** (80, a 22 m/s sideways launch + 6 m/s lift); Powered Iron Suit (thrust); **Rocket Launcher** (160, 8 m blast, push 32); Dual Alien Spikers; Chain Gun; Grenade Launcher (3.5 m blast); Wrist Laser; Needle Drill; Stock Pistol; Saw Wheel Staff; Barbed Bat (push 10); Chainsaw / Wheel Launcher; Laser Claws; Flaming Marshmallow; Titan Laser Axe (beam mode 35 m); PvP Dagger; Murderer Sword (Mystery role) | `Core/Session/EquipmentSpecifications.cs:16-38`; glove impulse: `GameSession/Modes/README.md` |
| Powers | 2x Speed, 3x Jump, Flying Hand (30 s) | same file |
| Pickups on the map | weapon and power-up bubbles spawn on surfaces | `MatchModeRuntime.PowerupSpawns.cs` |
| Melee bash with a held weapon | yes | `MatchModeRuntime.WeaponBash.cs` |
| Bots fight | bots punch (`CastleBotCombat.cs`) and aim and fire weapons (`CastleBotWeapons.cs`); personalities Balanced, Careful, Daredevil, Goofy | `Characters/Bots/CastleBots/` |
| Monsters | panther, jaguar, mushroom (gets dual spikers outside Survival), longhand, gum-brute; Infection's infected are "Panther" participants | `CastleBotPopulation.Enemies.cs:12`, `MatchModeRuntime.Equipment.cs:230`, `ModeSimulation.cs:31` |
| Fall damage | optional rule | `ModeContracts.cs:23`, `MatchModeRuntime.Health.cs:49-66` |

## 4. Eliminations and the round

- **Into the sea = out.** A survivor whose eyes go below the water (+0.25 m) is eliminated by
  drowning (`MatchModeRuntime.World.cs:209`). On the island: `Sink` (out after a grace), `Lethal`
  or `Walkable` by mode (`World.cs:210-214`).
- Health to zero is a kill when `deathEnabled` (`MatchModeRuntime.Health.cs:39`). Outcomes:
  respawn, become the flying hand, infected, spectator, revive (`ModeSimulation.cs:8`).
- The look: the survivor bursts into candy pieces with a shockwave ring and a flash, a wisp rises
  and "OUT!" floats up (`Presentation/Visuals/Emotes/EliminationEffects.cs:11-13`, raised through
  `GameEvents.BodyBurst` by `Characters/Player/CandyPlayers/SurvivorMoments.cs`). Sounds: hit and
  kill confirms, player, island and drowning deaths (`Core/Events/GameEvents.cs:6-10`).
- Round: waiting, a 3 s countdown, playing, a 5 s winner reveal, results (`ModeContracts.cs:10`,
  README). Murder Mystery shows a 10 s replay of the kill at the end (README).

## 5. Modes (`Core/Session/ModeContracts.cs:7`, defaults `ModeSimulation.cs:58-72`)

| Mode | Trailer-useful core | PvP | Disasters |
|------|---------------------|-----|-----------|
| Survival | stay alive while the tower is destroyed | punch off by default | automatic, every 20 s |
| King of the Hill | hold the highest region | punch + dagger | rule field, off |
| Block Control | stand on blocks to claim them for your team | punch + dagger | off |
| Capture the Flag | steal the lollipop flag; carriers slow, shielded in front | punch + dagger | off |
| Infection | the eliminated become infected hunters | punch | off |
| Team Tickets | exhaust the other team's respawns | punch + dagger | off |
| Murder Mystery | find the murderer; sheriff's revolver; murderer picks disasters | punch, role weapons | murderer's picks |
| Ninja (Shadow Lantern) | one lantern, free-for-all; any hit knocks it loose | punch 22 + dagger | off |
| Demo | host sandbox | configurable | scheduler on |

## 6. Events a cut can be keyed to (`Core/Events/GameEvents.cs:15-38`)

`BlockHit`, `Pushed`, `Damaged`, `Eliminated`, `EffectStarted/Ended`, `HazardStarted/Ended`,
`Emoted`, `RoundStarted/Ended`, `BlockCracked`, `BlockBroken`, `BodyBurst`, `MatchSound`
(hit confirm, kill confirm, deaths), `RewindStarted/Ended`. A take report can list them per frame.

## 7. Characters, maps and other facts

- Survivor about 0.94 m. **Avatars have no drawn eyes or mouth** (hand-reveal-2 critics; not
  re-checked in code): a reaction reads through head direction, body and hand pose only.
- Bot emotes (8): friendly wave, "totally meant that" fist pump, phew, what was that, golf clap,
  just chilling, nope nope nope, tiny victory dance (`Characters/Bots/CastleBots/EMOTES.md`).
- Survivor colours by name: Red, Blue, Yellow, Purple, Orange, Cyan, Pink, Green, Mint, Coral, Gold,
  Violet (`GameSession/Modes/ModeNames.cs:15-20`). The Studio's mark colour index does not follow
  this list in an obvious way (section 9).
- Maps: about 50 mode maps (`World/Maps/Resources/ModeMaps/Mode_*.prefab`): castle variants, candy
  and epic cities, Christmas town, Underneath the Tree, ninja temple, tower exhibition, Mayhem
  Foundry, Tycoon and more. Blocks crack and break (`BlockCracked`, `BlockBroken`). Heights differ:
  measure the map you use.
- AI giant hands: manual spawning is uncapped (`Characters/Bots/CastleBots/AI_HANDS.md`), so more
  than one giant hand can exist in a match. A human hand player flies one.
- A survivor riding a lifted, tilting block is carried by the block's position only: off its middle
  he sinks into it (0.36 m measured), at its middle he still slides (0.27 m) (hand-reveal-2 REPORT).

## 8. Not confirmed (each is a `verify` item until seen in a take)

Tsunami warning visuals; a survivor surviving a wave by standing higher; the tornado lifting a
survivor; how readable the earthquake is; Kraken side; whether a drowning elimination and its candy
burst fire for actors in a Studio take (section 9); bots choosing to fight on cue; several disasters
at once in one Studio take; power-outage gameplay; how each weapon's hit reads at survivor scale.

## 9. What Trailer Studio can direct for disasters and PvP (pilot branch, 2026-11-20)

| Need | Studio today | File | Gap or workaround |
|------|--------------|------|-------------------|
| Start any disaster, with its own settings and a length | `hazard` cue by prefab name, `values` in the disaster's order, `duration` | `Engine/StudioCues.cs:58-99` | none |
| Choose the side a disaster comes from | `aimed` + `heading`, only for disasters with `IDirectedHazard` (the tornado, pilot branch only) | `StudioCues.cs:68-85`; pilot `World/Hazards/BasicDisaster.cs:286-295` | tsunami: use the `tsunami` cue (below) or run variations; meteors, Kraken, cat, Santa: no aim |
| A tsunami from a chosen side and height | `tsunami` cue: heading, height, speed; warning 0, offshore 0, rise 0.8 s | `StudioCues.cs:102-121` | **it sets `affectPlayers = false`: this wave never pushes survivors.** For a wave that sweeps people, use the `hazard` cue (random side; verify) |
| Stop disasters | `stopHazards` | `StudioCues.cs:124-133` | - |
| Wreck the structure | `collapse`, `push` (direction, force, spin, ripple), `weaken` (loosen or remove), `guide` (steer debris), `slowTime` | `StudioCues.cs:37-55`; `Engine/StudioDirector.cs:140-215` | - |
| Give a survivor a weapon | `gear` cue to a live actor or everyone; or `StudioMark.equipment` at spawn | `StudioDirector.cs:217-237`; `Data/StudioSet.cs:22` | recorded actors cannot be handed things |
| A survivor fires at another | `fire` cue: a live shooter, a target actor or point, through the real equipment command | `StudioDirector.cs:239-268` | melee weapons through `fire`: unconfirmed; the target must be live for the hit to move it |
| A punch or shove on cue | **missing**: no punch cue | - | a person performs the punch; a live bot may punch on its own (not on cue); or fake it with reaction layers |
| Survivors that act | `Bot` marks run the normal bot brain; unperformed survivor marks stand and obey gravity; the local player performs one mark at a time | `Engine/StudioCast.cs:10-20` | bots are not directable and not repeatable: use variation runs and pick a take |
| Damage on, deaths off | the Studio's round is Demo with punch and health on and **deaths off**, pickup bubbles off, AI voice off | `Engine/StudioRun.cs:219-228` | a real elimination (candy burst, "OUT!") is not expected; drowning goes through `World.cs:209`, which does not check the deaths rule: unconfirmed. Stand-in: the `death`, `fall` or `tumble` reaction layer plus the splash |
| Hit reactions on a recorded actor | override layers: lookAt, reach, pose (crouch, lean, flinch, arm raise), reaction (stagger, knockback, fall, tumble, death, getUp, cheer, scream, cower), emote, outfit, colorFlash, equipment, hitPause; triggered by time, a cue or contact | `Engine/StudioOverrides.cs:19`; `Data/StudioOverrideData.cs:12-30` | reactions are head, hands and feet; no ragdoll (`TrailerStudio.md:74`) |
| Weapons, projectiles and particles in playback | recorded per frame and replayed | `Tracks/EffectsTrack.cs:14-22` | particles from a directed (baked) hand never happen: it is not live |
| Game sounds in playback | block hits, match sounds, block breaks recorded and raised again | `Tracks/AudioEventTrack.cs:9-29` | no sound placed on a cue; designed sound still goes in at assembly |
| A disaster in playback | its frame description replays the look and sound | `Tracks/HazardTrack.cs` | a played-back disaster does not push or hurt a live performer (`TrailerStudio.md:72`) |
| Mode objectives (hill, flags, lantern, scores, HUD) | **missing**: the stage always runs Demo | `StudioRun.cs:221` | shoot them in a real match capture, outside the Studio, or leave them out |
| Several real players | **missing** in one take; bots stand in | - | - |
| Mark colours | `StudioMark.color` is an index into a palette | `Data/StudioSet.cs:17, 151-158` | index 6 rendered **yellow** in hand-reveal-2; check the colour in a still before you name a hero by it |
