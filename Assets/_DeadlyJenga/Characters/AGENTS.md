# Characters (level 2)

Everything that moves through the world, human or AI.
May use: Core only. Ask the map through Services.World (IWorldQuery), never World classes.

## Lanes
- Player: CastleSurvivor (+ Climbing, Stamina, VR, Abilities), LocalHandInput,
  CandyPlayers (avatar), CastleSurvivor prefab.
- Hand: CastleHandActor, HandDemo, CandyPhysicsHands, CastleFlyingHand prefab.
- Bots: CastleBots (survivor + hand AI, personalities, navigation, BotWorld), SocialBotDriver.
- Network: remote copies (SurvivorNetworkView, HandNetworkView), CharacterWire (state format),
  CharacterNetwork + CharacterServices (register with Core on load).

## What Characters provides
- CastleSurvivor (players and bots): IPushable, IDamageable, IAffectable, ITeleportable.
  Reactions to specific effects go in CastleSurvivor.Abilities.cs > Apply().
- CastleSurvivor also: IMovementState, IPlayerView, IPlayableCharacter, ISurvivorSettings.
- CastleHandActor: ITeleportable, IFlyingState, IPlayableCharacter.
- CandyPlayerAvatar and CandyFlyingHandBridge: INetworkActor (pack their own network state).
- Bot populations: ISurvivorBotPopulation / IBotPopulation, created through BotSystems.
- SocialBotDriver: ISocialBot (registered with SocialBots). The social layer tunes a bot's
  personality and nudges its walking through it; the bot still does all the moving.
- CandyTrackingSource: IFingerTips (fingertip positions for pressing things).
- Climbing and vaulting use IClimbable via Climbables.TryGet, never CastleBlock.

## Rules
- Never call HUD, audio or session code. Raise GameEvents or expose read-only values.
- Never draw UI. Settings are exposed as values (ISurvivorSettings.CurrentRules) and
  Presentation draws them. Reading XR Interaction Toolkit interactor state (e.g. a ray hovering
  UI, to pose the hand as pointing) is fine; that is the body reacting, not drawing UI.
- A character may set the materials and colors of its own model (its body); everything
  else seen or heard belongs to Presentation.
- Test benches and lab scenes go in DevTools (e.g. DevTools/Scenes/CandyHandContactLab).
- Unknown effects are ignored, never an error.
- If you change how a survivor or hand moves, check bots too, and multiplayer if it changes network state.
- LocalHandInput and CandyHandSkeleton are networked types; this assembly is woven by Photon.

## Next up
- Hands grab blocks through IGrabbable once World implements it.
- Multiplayer: hands don't move on other players' screens (existing bug); start in HandNetworkView.
