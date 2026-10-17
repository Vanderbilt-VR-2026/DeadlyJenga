# Disaster authoring rules

These rules apply to every disaster in this folder, including placeholders.

## Required structure
- Keep each disaster's script, HazardType asset and root prefab in its own subfolder.
- Derive real disasters from MapHazard. Derive duds from PlaceholderHazard.
- Keep exactly one hazard component on the prefab root, enabled, with root scale (1, 1, 1).
- Assign a unique HazardType, a positive intensity multiplier and a positive duration in seconds.
- Do not replace Begin, End or TryValidate, or bypass them to start a disaster.
- Implement BeginHazard(IWorldQuery world, Bounds bounds, float strength, float seconds).
  Placeholders implement RunPlaceholder with those same arguments.
- Use the supplied world-space bounds for placement and coverage. Never hard-code a map center,
  assume the map is at the origin, or name a map class. Bounds are an axis-aligned envelope;
  irregular maps may need extra map queries before choosing safe targets.
- Keep coverage separate from intensity. A larger map must not automatically increase damage,
  force or duration. Scale child visuals deliberately, never the prefab root.
- Access shelter, water and targets through Core. Presentation listens to the existing events.
- End real disasters explicitly when their duration expires. Clean up in EndHazard. If overriding
  OnDisable, call base.OnDisable so the end event and cleanup still happen.
- The session calls Begin on the host. World hazards must respect IWorldAuthority. Tsunami
  publishes IWaveState through Tracked.Waves; GameSession replicates its visual frame and sends
  host-computed impacts to remote player owners. Clients must not also run the world hazard.

## Maps
- Every IWorldQuery implementation must provide finite world-space PlayableBounds with positive
  width, height and depth. The interface deliberately has no default implementation.
- CastleGenerator derives these bounds from its layout and transform. For a custom area, assign
  its Playable Area field an enabled, active trigger BoxCollider. Its bounds include rotation
  and scale. An invalid override must fail validation rather than silently use another area.
- The cyan selected-object gizmo shows the castle's area. Bounds describe the authored play
  area, not a shrinking envelope around falling blocks.
- FakeWorldQuery supplies a configurable area for sandbox tests.

## Adding or removing disasters
- Add the root prefab to the DisasterCatalog asset in Resources. Catalog order is menu order.
  Prefab names are command IDs: keep them unique and stable.
- The menu reads the catalog; do not add a hard-coded menu switch or a second list.
- On removal, remove the catalog reference and retire the prefab and its script/type together
  through Unity. Check scene, prefab and presentation references first. Preserve .meta files
  when moving assets. Never silently remove just the hazard component or its map contract.
- Every prefab under Hazards is a registered disaster root. Put supporting art/prop prefabs
  outside this folder so they are not mistaken for unregistered disasters.
- Run Tools > Deadly Jenga > Validate Disaster Setup. Builds also run this check and validate
  map bounds in processed scenes. Invalid catalog entries, missing contracts, missing scripts,
  duplicate IDs/types and unregistered prefabs stop the build.
- Test small and large maps at nonzero positions, rotated/scaled maps, invalid bounds and a
  missing Services.World. Then test Game in Play Mode and host/client/headset behavior.

All nine registered disasters have basic implementations. Shared tuning is exposed through Core IDemoHazard, and non-wave visual state through IDisasterState. Read Tsunami/AGENTS.md before
changing its gameplay or visual profile. Keep CPU and shader wave shapes in agreement.
