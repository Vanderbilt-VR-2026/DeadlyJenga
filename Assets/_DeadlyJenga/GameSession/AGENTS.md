# GameSession (level 4)

The rules of the match and who is playing. Uses only Core: the map (Services.World, IRebuildable, ISyncedWorld), characters (IPlayableCharacter, ISurvivorSettings, INetworkActor), bots (BotSystems, IBotPopulation). It draws nothing and plays no sound: menus, HUDs and the lobby menu live in Presentation (in-game menus attach on GameEvents.SessionStarted).

## Lanes
- Session: CastlePlaySession, CastleFusionSession, CastleFusionWire, CastleLan* (shared
  snapshot code), LobbyConnection, LobbyCountdown, MatchSetup, PlayerSpawner,
  scenes Lobby.unity and Game.unity.
- Modes: game modes (classic, teams, random disasters). One subfolder per mode.

## What GameSession provides
- CastlePlaySession implements ISessionInfo and registers as Services.Session.
- Listens to GameEvents.Eliminated (currently: respawn the local survivor).
- Creates bots through BotSystems.Create and raises GameEvents.SessionStarted.
- CastleLanSession implements IMultiplayerControls (state and commands) for the menus.
- LobbyConnection implements ILobbyControls as Services.Lobby; it adds itself to the
  FusionBootstrap object when a scene with a LobbyCountdown loads.
- MatchSetup applies Core's MatchChoices (set by the lobby menu) when Game loads.

## Rules
- Only GameSession may edit or save Lobby.unity and Game.unity.
- Game.unity is a container. Change other boxes' prefabs in their own box.
- Modes start and stop hazards only through IHazard, from a list of hazard prefabs.
  A mode never knows what a hazard does. A mode must not require another mode.
- Raise RoundStarted / RoundEnded when rounds exist.
- Photon is the only multiplayer path. CastleLanSession holds the snapshot code
  Photon uses; do not delete it until that is split out.
- This assembly is woven by Photon.
- No UI or sound code. The assembly does not reference TMP, UI or XRI.
- Scene buttons call the Presentation menu component, never GameSession or Fusion components.

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

## Multiplayer Play Mode: Player 2 can't load Photon's settings
Symptom: Player 2 shows "Failed to load NetworkProjectConfigAsset" and "Asset Database is set to
Read Only, but it has found out-of-date assets", and can't join. It usually happens after changing
network prefabs or networked scripts. The virtual player is read-only and needs Photon's
dependency values from the main editor; it only gets them reliably while it is running.
Fix (no restart or Reimport All needed):
1. Stop Play mode. Turn Player 2 ON and wait until it is idle.
2. With Player 2 running, in the main editor Reimport GameSession/Session/Prefabs/VR Player Variant
   and GameSession/Session/Scripts/LobbyCountdown.cs; wait for the recompile.
3. Clear Player 2's console, then Play, host and join.
