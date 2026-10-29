# GameSession (level 4)

The rules of the match and who is playing. Uses only Core: the map (Services.World, IRebuildable, ISyncedWorld), characters (IPlayableCharacter, ISurvivorSettings, INetworkActor), bots (BotSystems, IBotPopulation, SocialBots). It draws nothing and plays no sound: menus, HUDs, the lobby terminals and voice playback live in Presentation (in-game menus attach on GameEvents.SessionStarted, the lobby's on GameEvents.LobbyStarted).

## Lanes
- Session: CastlePlaySession, CastleFusionSession (+ Disasters), CastleFusionWire, CastleLan*
  (shared snapshot code), IslandLobby (+ Controls, Session), IslandRoomConnection,
  IslandRoomAddress, IslandDiscovery, IslandLobbyCatalog, MatchSetup, SessionDemoControls,
  CastleDemoCommands, DemoDisasters, scenes Lobby.unity and Game.unity.
- Session/CastleSocial: the AI social layer (identities, director, speech, microphone capture,
  SocialBotAgent, SocialWorld, SocialControls). It plays no audio: voices go to Core's IVoicePlayer.
- Modes: game modes (classic, teams, random disasters). One subfolder per mode.

## What GameSession provides
- CastlePlaySession implements ISessionInfo and registers as Services.Session.
- Listens to GameEvents.Eliminated (currently: respawn the local survivor).
- Creates bots through BotSystems.Create and raises GameEvents.SessionStarted.
- CastleLanSession implements IMultiplayerControls (state and commands) for the menus.
- IslandLobby is ISessionInfo and IWorldQuery while Lobby.unity is open, and ILobbyControls
  for the terminals (IslandLobby.Controls.cs). It owns the room connection (IslandRoomConnection),
  the other players' remote views, AI voices and all lobby collision (a flat gray-box floor
  until Presentation hands over the sand shape). It raises GameEvents.LobbyStarted.
- CastlePlaySession provides IDemoControls (SessionDemoControls) and ISocialControls (SocialControls).
- SocialBotAgent gives survivor bots identities through Core's ISocialBot; it never names bot classes.
- MatchSetup applies Core's MatchChoices when Game loads.
- Photon's FusionBootstrap.cs may carry our connection-message and timeout fixes. Only GameSession
  reads it; check for those edits if Photon Fusion is ever upgraded.

## Rules
- Before adding or changing bot objectives for a mode, read Modes/AGENTS.md and Modes/BotObjectives/README.md. New objectives normally extend definitions/facts rather than the bot controller. Invalid definitions and contract checks must fail explicitly.
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
2. With Player 2 running, in the main editor Reimport GameSession/Session/Prefabs/VR Player Variant,
   (resends Fusion.PrefabsDependency) and Characters/Player/Scripts/NetworkHands.cs (resends
   Fusion.ScriptOrderDependency; it must be a NetworkBehaviour script, and it is the only one left);
   wait for the recompile.
3. Clear Player 2's console, then Play, host and join.
