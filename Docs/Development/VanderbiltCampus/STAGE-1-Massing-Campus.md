# Stage 1: LoD2 campus (playable)

Goal: the whole campus at the exact outline, height and roof shape of every
building, walkable and climbable in Deadly Jenga, built only from LiDAR and
open map data. This version keeps working forever as the far LOD and as the
fallback for buildings without photos.

## Build

### Terrain
- Classify LiDAR ground (use the delivered classes; re-run SMRF/CSF in PDAL
  where classes are missing or wrong).
- Terrain mesh per tile: 0.5 m grid inside the boundary, 2 m in the ring;
  curbs, steps and retaining walls kept as real breaks where the LiDAR shows
  them (≥ 15 cm). UV0 + material IDs from OSM surfaces (grass, paving,
  asphalt, planting).
- Accuracy check against ground points (STANDARDS.md §5).

### Buildings
- Footprints: OSM, cross-checked with Metro footprints and LiDAR edges.
  Disagreements over 0.3 m are resolved toward the LiDAR and logged.
- Heights and roofs: the LiDAR roof points inside each footprint →
  plane detection (CGAL Shape Detection / RANSAC) → roof reconstruction
  (PolyFit/City3D style, or a simple custom fitter for gable, hip, flat,
  mansard and towers). Multi-part buildings split into parts with their own
  heights.
- Output per building: watertight LoD2 mesh, UV0, walls `brick_red` or the
  best guess (logged as a guess in `model.json`), roofs by detected type.
  LoD1 extrusions for the ring.
- `model.json` per building (STANDARDS.md §6), with accuracy numbers.

### Trees and ground features
- Tree positions, heights and crown widths from the canopy points
  (local-maximum detection on a canopy height model). Placeholder stylized
  trees in three or four sizes, `bark` + `foliage`.
- OSM paths, lawns, roads, parking, fountains, as surface material regions
  on the terrain.

### Into Unity
- Blender headless script: assemble each tile, export FBX/GLB with submeshes
  named `mat_<id>` and LoD groups (LoD2, LoD1, impostor optional for now).
- World/Maps: `VanderbiltCampusMap` (STANDARDS.md §11), tile prefabs, simple
  colliders (boxes/convex per building part; terrain mesh collider per tile).
- Presentation/Visuals: palette atlas and one campus material reading
  material IDs (cross-box, pre-approved). Without Presentation, it's gray.
- DevTools: `Build Vanderbilt Campus` menu and an accuracy/budget validation
  menu writing to Logs/Validation.
- GameSession: lobby entry (placeholder name) and a first `MapMatchLayout`
  with spawns on lawns and open plazas, so it can be chosen and played.
- Spawns safe, water height below the lowest ground, `PlayableBounds` = the
  boundary plus a margin.

## Done when
1. Game.unity enters solo Play Mode on the campus through the coordinator;
   the survivor can walk Alumni Lawn to Peabody and climb at least one
   building.
2. Footprints within 0.3 m and heights within 0.2 m for at least 95% of
   buildings; every outlier listed with the reason.
3. An overhead render lines up with the orthophoto (overlay image in the
   results file).
4. Budget check from the five busiest viewpoints reported.
5. Map plays with Presentation disabled (gray).
