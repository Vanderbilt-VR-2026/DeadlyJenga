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
- Demo disasters come from the Core HazardCatalog resource named DisasterCatalog. Validate
  through IMapHazard before starting; never bypass missing map bounds or instantiate at an
  arbitrary fallback position. Keep host authority and use the existing command path.
- Adding/removing a disaster changes the catalog, not a hard-coded UI list. Follow
  World/Hazards/AGENTS.md for prefab registration and validation.
- Tsunami visuals replicate through Core WaveFrame in the demo snapshot. The host's remote
  survivor proxies forward impacts to owners; clients never run the wave's world physics.
  Leaving or migrating cancels disasters and clears queued impacts.
- Raise RoundStarted / RoundEnded when rounds exist.
- Photon is the only multiplayer path. CastleLanSession holds the snapshot code
  Photon uses; do not delete it until that is split out.
- This assembly is woven by Photon.

## Multiplayer contract
### World (done)
- The snapshot code reaches the map only through Core: `Services.World` (IWorldQuery)
  for block bodies, center, top height and radius, and `ISyncedWorld` for layout,
  origin, revision and replica mode.
- Never name CastleGenerator, CastleSettings or CastleLayout in networking code.
  A new map syncs by implementing ISyncedWorld; networking does not change.

### Actors (planned, for Luke; not built yet)
- Today CastleLanSession and CastleLanActorView read CandyPlayerAvatar and
  CandyPhysicalHand directly, and the snapshot's Actor type lists their fields.
- Plan: a Core interface each synced character implements:
  `string Kind { get; }`, `void WriteState(BinaryWriter w)`, `void ReadState(BinaryReader r)`.
  The real character writes its own state; the copy on other machines reads it.
  Networking ships bytes and creates remote copies by Kind from a prefab list.
- This changes the network format and remote drawing, so it needs a two-player
  test on both host and client before merging. Do not start it without Luke.
