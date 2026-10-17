# Core (level 0)

The shared vocabulary every box uses to talk to every other box.
Core uses nothing. Namespace: DeadlyJenga.Core.

## Folders
- Abilities/  IPushable, IDamageable, IAffectable, ITeleportable, IClimbable,
              IGrabbable, Climbables (find the IClimbable behind a collider).
- Data/       EffectType and HazardType (ScriptableObject assets), Effect,
              IHazard, IMapHazard, IWaveTuning, HazardCatalog, Layers, DisasterControls
              (IDemoHazard, DisasterFrame, IDisasterState, DisasterStates), WaveFrame (IWaveState,
              IWorldAuthority, IHazardMotion, IWaterResistance).
- Services/   Services (Session, World), ISessionInfo, IWorldQuery, ISyncedWorld,
              IRebuildable, IMapFootprint.
- State/      IMovementState, IFlyingState, IPlayerView, IFingerTips, IVaultTargetProvider, Tracked (Movers, Flyers, Waves).
- Session/    IPlayableCharacter, ISurvivorSettings, MatchRules, MatchOptions, bot contracts
              (IBotPopulation, BotSystems), IMultiplayerControls, ILocalPlayerInput,
              ILobbyControls (island lobby terminals), MatchChoices, IDemoControls + ISocialControls
              (DemoContracts), ISocialBot + SocialBots, IVoicePlayer + IVoiceMix + VoicePlayback.
- Network/    INetworkActor, INetworkActorView, NetworkActors, NetworkKinds, NetWire, INetworkPreview.
- Events/     GameEvents (SessionStarted: match menus attach; LobbyStarted: lobby terminals attach).
- Testing/    TestDummy, FakeWorldQuery, FakeSession (stand-ins for testing).
- Editor/     ProjectPath (editor tools find assets by name).

## Rules
- Only interfaces, events, constants, plain data and test stand-ins. No gameplay.
- No drawing methods in contracts (no Draw*, OnGUI). Expose values and commands instead.
- Never reference any other box.
- Only ADD. Never rename or change an existing member; other boxes depend on it.
- Remove by marking [System.Obsolete] first, delete once nothing uses it.
- Every change here is its own small PR (branch core/<name>) the whole team sees.
- Keep it small. If only one box needs something, it belongs in that box.
- IWorldQuery.PlayableBounds is required: finite world-space bounds with positive size on
  every axis. No default bounds that hide incomplete new maps.
- IMapHazard adds setup validation to IHazard. HazardCatalog is data only; World validates
  prefab authoring and GameSession uses Core contracts to trigger the registered prefabs.
- WaveFrame/IWaveState expose tsunami visual state through Tracked.Waves. IWorldAuthority
  identifies the physics host. IHazardMotion and IWaterResistance let water query target motion
  and resistance without naming character classes. These contracts contain no gameplay logic.

## Not built yet (add when the feature that needs it starts)
- Round info on ISessionInfo (round time, survivors left, hand count).
- IClaimable / points events for team modes.
