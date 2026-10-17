# World (level 1)

The place you play in. May use: Core only.

## Lanes
- Blocks: CastleBlock (+ prefab), Castle Friction material, CastleBlockAppearance
  (the block's visual slot).
- Maps: CastleGenerator, CastleLayout, castle settings, island, ocean kill height.
- Hazards: map-aware disaster prefabs. Read Hazards/AGENTS.md before changing them.
  Each derives from MapHazard (PlaceholderHazard for duds) and is registered in DisasterCatalog.

## What World provides
- CastleBlock implements IClimbable and IPushable.
- CastleGenerator implements IWorldQuery and registers itself as Services.World.
  It also eliminates anything that falls below oceanKillHeight (IDamageable).

## Rules
- Must work as gray boxes. Never require Presentation art.
- Hazards act only through Core: IPushable, IDamageable, IAffectable, ITeleportable,
  Services.World, GameEvents. Never reference Characters or player classes.
- Raise GameEvents.HazardStarted/HazardEnded from hazards so Presentation can react.
- New effect or hazard kinds are new EffectType / HazardType assets.
- Maps must implement IWorldQuery.PlayableBounds. CastleGenerator calculates its area or uses
  its assigned trigger BoxCollider. Map size controls disaster coverage, not intensity.
- Run Tools > Deadly Jenga > Validate Disaster Setup when changing maps or disasters.

## Next up
- Implement IGrabbable on CastleBlock (port weight, cooldown and last-in-layer
  lock from _Legacy/TOWER), then hands use it.
