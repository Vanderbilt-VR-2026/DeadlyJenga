# GameSession (level 4)

The rules of the match and who is playing. May use every box.

## Lanes
- Session: CastlePlaySession, CastleFusionSession, CastleFusionWire,
  CastleLanSession / Protocol / Transport / ActorView, LobbyCountdown,
  LobbyRoomCode, LobbyMatchSetup, MatchSetup, PlayerSpawner,
  NetworkHands, CastleWorldMenu, scenes Lobby.unity and Game.unity.
- Modes: game modes (classic, teams, disasters). One subfolder per mode.

## Contracts
- Implements ISessionInfo for every other box.
- Networking syncs whatever implements ISyncState. Do not reach into
  block or character internals to serialize them.
- Raises RoundStarted, RoundEnded, PlayerEliminated.

## Rules
- Only GameSession may edit or save Lobby.unity and Game.unity.
- Game.unity is a container: it places prefabs from other boxes.
  Change those prefabs in their own box, not as scene overrides.
- Each mode lives in Modes/<ModeName>/ and plugs into the round flow.
  A mode must not require another mode.
- CastleLanSession still holds the shared snapshot code used by Photon.
  Do not delete the LAN files until that code is split out.
- Photon is the only multiplayer path. Do not add LAN startup back.

## Known fixes
- MatchSetup moves here from its old spot; its "apply to player" code
  belongs here, not in Core.
- Remove leftover references to TowerGenerator (legacy).