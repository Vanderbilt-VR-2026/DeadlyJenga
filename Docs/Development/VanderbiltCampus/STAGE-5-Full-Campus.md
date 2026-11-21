# Stage 5: the full campus

Goal: every captured building at LoD3 (hero spots LoD3+), props and trees in
place, streaming and lighting working, full Deadly Jenga gameplay, the candy
look, and proof that realism can be added later from the archive.

## Build

### Batch production
- `pipeline/build_all.py`: for each building with aligned captures, in
  shot-list priority order: measure (Stage 3), assemble (Stage 4), check,
  export. Buildings that fail accuracy go to a review list instead of the
  game. Buildings without captures stay LoD2 and get a "needs shots" entry
  in `EVAN-TODO.md`.
- Re-runnable: new captures upgrade buildings; nothing older is deleted
  (new versions are added in the archive).

### Props and trees
- Prop library (benches, lamp posts, bins, bike racks, bollards, planters,
  sign posts with `signage_blank`, fountain parts, statues as simplified
  forms), modeled once with LoDs and IDs, placed at positions measured from
  aligned photos or OSM.
- Trees: stylized species models (oak, magnolia, elm, and so on, as
  identified from photos where possible), sized from the LiDAR; a few
  famous trees placed individually.

### Streaming, lighting, budgets
- Tile streaming around the player; the LoD1 ring is always loaded.
- Lightmaps or baked AO per tile, one realtime sun.
- Budget check from the busiest spot in every tile (STANDARDS.md §10).

### Gameplay
- Where the Jenga towers and destructible block structures go (lawns and
  plazas), chosen so routes and sightlines work; spawns, team areas and
  objectives in `MapMatchLayout`; water height and kill floor; climbable
  surfaces marked; `IWorldQuery` values (`Center`, `Radius`, `TopHeight`,
  `PlayableBounds`), `IMapFootprint`.
- Validation: route connectivity from every spawn, capsule clearance at
  doors and steps, and a bot smoke test (as the Christmas Candy Town checks
  do).

### Looks (Presentation)
- **Candy** (default): palette per material ID in the Deadly Jenga
  candy/plastic direction, sky and backdrop.
- **Plain/realistic-color** palette: the same IDs with true-to-life flat
  colors, to show how the campus reads without stylization.
- Switchable in the editor; World unchanged by either.

### Realism readiness test (proves no data was lost)
- On one facade of Kirkland, run a **CPU low-resolution bake**: project the
  aligned archived photos onto its UV0, blend them, apply the gray-card
  exposure correction, and render it in Unity next to the palette version.
  It doesn't need to look great; it proves the archive, UVs and alignment
  are enough for Stage 6.
- A script that checks every LoD3 building for UV0, material IDs, a ledger,
  sources and registered photos, and lists anything missing.

## Done when
1. Every captured building is LoD3 within targets, or on the review list
   with reasons; uncaptured buildings are LoD2 with shot needs listed.
2. Game.unity plays solo on the campus through the coordinator, with towers,
   spawns and at least two modes started; bot smoke test passes.
3. The budget check passes for every tile in the editor (headset run listed
   as not done unless it happened).
4. Both palettes render; comparison renders at every hero spot are in the
   results file.
5. The realism readiness test image and the readiness checker report are in
   the results file, with zero missing items for LoD3 buildings.
6. `manifest.py verify` passes on the whole archive.
