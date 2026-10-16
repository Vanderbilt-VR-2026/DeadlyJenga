# GameSession (level 4)

The rules of the match and who is playing. May use every box.

## Lanes
- Session: CastlePlaySession, CandyDemoHud (in-game menu), CastleWorldMenu,
  CastleFusionSession, CastleFusionWire, CastleLan* (shared snapshot code),
  Lobby*, MatchSetup, PlayerSpawner, NetworkHands, scenes Lobby.unity and Game.unity.
- Modes: game modes (classic, teams, random disasters). One subfolder per mode.

## What GameSession provides
- CastlePlaySession implements ISessionInfo and registers as Services.Session.
- Listens to GameEvents.Eliminated (currently: respawn the local survivor).
- Creates bots and hands them a BotWorld.

## Rules
- Only GameSession may edit or save Lobby.unity and Game.unity.
- Game.unity is a container. Change other boxes' prefabs in their own box.
- Modes start and stop hazards only through IHazard, from a list of hazard prefabs.
  A mode never knows what a hazard does. A mode must not require another mode.
- Raise RoundStarted / RoundEnded when rounds exist.
- Photon is the only multiplayer path. CastleLanSession holds the snapshot code
  Photon uses; do not delete it until that is split out.
- This assembly is woven by Photon.
