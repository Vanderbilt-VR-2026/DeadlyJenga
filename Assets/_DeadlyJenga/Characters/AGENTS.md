# Characters (level 2)

Everything that moves through the world, human or AI.
May use: Core, World.

## Lanes
- Player: CastleSurvivor (+ Climbing, Stamina, VR parts), LocalHandInput,
  CandyPlayers (avatar), CastleSurvivor prefab.
- Hand: CastleHandActor, HandDemo, CandyPhysicsHands, CastleFlyingHand prefab.
- Bots: CastleBots (survivor + hand AI, personalities, navigation).

## Contracts
- Hand pulls blocks only through IPullable, never CastleBlock directly.
- Player exposes stamina as public read-only values for the HUD to read.
- Raise GameEvents for jump, land, climb, vault, grab, release, emote.
- Player and hand state reach networking through ISyncState.
- Bots read game state through ISessionInfo.

## Sandbox
Characters/Sandbox.unity: a few gray blocks, one player, one hand,
a fake ISessionInfo, a cube implementing IPullable.

## Rules
- Never call HUD, audio, or session code directly. Raise events or
  expose values; other boxes read them.
- Bots copy human controls through the driver interfaces. If player or hand
  movement changes, check bots in the sandbox before finishing.

## Known fixes
- Remove Player's direct call to CandyStaminaHud.
- Replace Bots' use of CastlePlaySession with ISessionInfo.