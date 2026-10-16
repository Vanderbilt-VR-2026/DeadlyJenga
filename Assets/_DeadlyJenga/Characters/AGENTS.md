# Characters (level 2)

Everything that moves through the world, human or AI.
May use: Core only. Ask the map through Services.World (IWorldQuery), never World classes.

## Lanes
- Player: CastleSurvivor (+ Climbing, Stamina, VR, Abilities), LocalHandInput,
  CandyPlayers (avatar), CastleSurvivor prefab.
- Hand: CastleHandActor, HandDemo, CandyPhysicsHands, CastleFlyingHand prefab.
- Bots: CastleBots (survivor + hand AI, personalities, navigation, BotWorld).

## What Characters provides
- CastleSurvivor (players and bots): IPushable, IDamageable, IAffectable, ITeleportable.
  Reactions to specific effects go in CastleSurvivor.Abilities.cs > Apply().
- CastleHandActor: ITeleportable.
- Climbing and vaulting use IClimbable via Climbables.TryGet, never CastleBlock.

## Rules
- Never call HUD, audio or session code. Raise GameEvents or expose read-only values.
- Unknown effects are ignored, never an error.
- If you change how a survivor or hand moves, check bots in the sandbox too.
- LocalHandInput and CandyHandSkeleton are networked types; this assembly is woven by Photon.

## Sandbox
Characters/Sandbox.unity: gray blocks, one player, one hand, FakeSession,
FakeWorldQuery, and buttons that Push / Damage / Apply effects to the player.

## Next up
- Hands grab blocks through IGrabbable once World implements it.
- Bots still use BotWorld (concrete classes inside this box and World). Fine for
  now; switch to Services.World where it helps.
