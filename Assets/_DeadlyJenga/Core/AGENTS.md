# Core (level 0)

The shared vocabulary every box uses to talk to every other box.
Core uses nothing. Namespace: DeadlyJenga.Core.

## Folders
- Abilities/  IPushable, IDamageable, IAffectable, ITeleportable, IClimbable,
              IGrabbable, Climbables (find the IClimbable behind a collider).
- Data/       EffectType and HazardType (ScriptableObject assets), Effect,
              IHazard, Layers.
- Services/   Services (Session, World, Audio), ISessionInfo, IWorldQuery, ISyncedWorld,
              IRebuildable, IMapFootprint.
- State/      IMovementState, IFlyingState, IPlayerView, Tracked.
- Session/    IPlayableCharacter, ISurvivorSettings, MatchRules, MatchOptions, bot contracts
              (IBotPopulation, BotSystems), IMultiplayerControls, IAudioControls, ILocalPlayerInput.
- Network/    INetworkActor, INetworkActorView, NetworkActors, NetworkKinds, NetWire, INetworkPreview.
- Events/     GameEvents.
- Testing/    TestDummy, FakeWorldQuery, FakeSession (stand-ins for testing).
- Editor/     ProjectPath (editor tools find assets by name).

## Rules
- Only interfaces, events, constants, plain data and test stand-ins. No gameplay.
- Never reference any other box.
- Only ADD. Never rename or change an existing member; other boxes depend on it.
- Remove by marking [System.Obsolete] first, delete once nothing uses it.
- Every change here is its own small PR (branch core/<name>) the whole team sees.
- Keep it small. If only one box needs something, it belongs in that box.

## Not built yet (add when the feature that needs it starts)
- Round info on ISessionInfo (round time, survivors left, hand count).
- IClaimable / points events for team modes.
