# GameSession (level 4)

The rules of the match and who is playing. Uses only Core: the map (Services.World, IRebuildable, ISyncedWorld), characters (IPlayableCharacter, ISurvivorSettings, INetworkActor), bots (BotSystems, IBotPopulation), sound (Services.Audio). Menus and HUDs live in Presentation and attach on GameEvents.SessionStarted.

## Lanes
- Session: CastlePlaySession, CastleFusionSession, CastleFusionWire, CastleLan* (shared
  snapshot code), Lobby*, MatchSetup, PlayerSpawner, scenes Lobby.unity and Game.unity.
  (The menus moved to Presentation/Visuals/Menus; NetworkHands to Characters/Player.)
- Modes: game modes (classic, teams, random disasters). One subfolder per mode.

## What GameSession provides
- CastlePlaySession implements ISessionInfo and registers as Services.Session.
- Listens to GameEvents.Eliminated (currently: respawn the local survivor).
- Creates bots through BotSystems.Create and raises GameEvents.SessionStarted.
- CastleLanSession implements IMultiplayerControls for the menus.

## Rules
- Only GameSession may edit or save Lobby.unity and Game.unity.
- Game.unity is a container. Change other boxes' prefabs in their own box.
- Modes start and stop hazards only through IHazard, from a list of hazard prefabs.
  A mode never knows what a hazard does. A mode must not require another mode.
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

### Actors (done)
- Each character packs its own state (INetworkActor.WriteState); networking sends
  `id + kind + active + bytes` and makes remote copies with NetworkActors.CreateView.
- Never name avatar, hand or bot classes in networking code. A new character syncs by
  implementing INetworkActor and registering its kind (Characters/Network/CharacterNetwork).
