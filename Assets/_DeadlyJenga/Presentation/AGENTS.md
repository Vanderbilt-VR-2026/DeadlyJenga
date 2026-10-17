# Presentation (level 3)

What you see and hear. Nothing here may change gameplay.
May use: Core only. Reads characters through IMovementState, IFlyingState and IPlayerView, the map through Services.World and IMapFootprint.

## Lanes
- Audio: CandyAudio (engine, bank, studio, block/player/hand audio).
- Visuals: CandyTropicalArt, Crate Visual, Block Appearance asset, materials,
  OceanFlow, skybox and texture folders, CandyHudVisuals, CandyStaminaHud.
  Menus/: CandyDemoHud (desktop menu + hints), CastleWorldMenu (VR menu), SessionPresentation
  (attaches menus, HUD and stamina bar on GameEvents.SessionStarted).

## How it plugs in
- Listen to GameEvents (Pushed, Damaged, Eliminated, EffectStarted/Ended,
  HazardStarted/Ended, BlockHit...) and read values (Health, stamina, Services.Session).
- Map each EffectType / HazardType to its sound and visuals in a lookup table owned
  here. A type with no entry simply has no art yet; gameplay still works.
- Replace gameplay prefabs' "Visual" child to change their look. Never touch colliders,
  rigidbodies, mass or placement.

## Rules
- Deleting this box must leave a playable gray-box game.
- Keep originals in "Assets For Texture (BETA)". Make new variants instead.

## Next up
- Audio attaches itself to anything in Tracked.Movers / Flyers and Services.World.Blocks
  and reads Core state. New sounds should listen to GameEvents (Pushed, Damaged and the
  hazard events have no listener yet).
