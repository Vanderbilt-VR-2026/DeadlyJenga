# Presentation (level 3)

What you see and hear. Nothing here may change gameplay.
May use: Core, World, Characters.

## Lanes
- Audio: CandyAudio (engine, bank, studio, block/player/hand audio).
- Visuals: CandyTropicalArt, Crate Visual, Block Appearance asset, materials,
  OceanFlow, skybox and texture folders, CandyHudVisuals, CandyStaminaHud.

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
- Audio still attaches components to players, hands and blocks by searching the scene
  and reading their state. Allowed by the levels, but new sounds should use GameEvents,
  and existing ones can move over time.
