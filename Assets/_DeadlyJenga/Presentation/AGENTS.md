# Presentation (level 3)

What you see and hear. Nothing here may change gameplay.
May use: Core, World, Characters.

## Lanes
- Audio: CandyAudio (engine, bank, studio, block/player/hand audio).
- Visuals: CandyTropicalArt, Crate Visual, Block Appearance asset,
  block and ocean materials, OceanFlow, skybox and texture folders,
  HUD (CandyDemoHud, CandyHudVisuals, CandyStaminaHud).

## Contracts
- Prefer listening to GameEvents over searching the scene for objects.
- HUD reads stamina from Player and game state from ISessionInfo.
- Supplies art to CastleBlockAppearance; never changes block colliders,
  mass, or placement.

## Sandbox
Presentation/Sandbox.unity: a few blocks and a player, plus buttons that
fire GameEvents by hand to test sounds and effects.

## Rules
- Deleting this box must leave a playable gray-box game.
- Never add colliders, rigidbodies, or gameplay logic.
- Keep originals in "Assets For Texture (BETA)". Make new variants instead.
- Do not run CandyArtBuilder / installer commands unless the task asks.

## Known fixes
- Replace Audio's and HUD's use of CastlePlaySession with ISessionInfo.
- Move audio triggers from scene searches to GameEvents over time.