# Campus (Vanderbilt LoD2) map

Box World, lane Maps. Lobby id `mode-vanderbilt`, player-facing title "Campus (LoD2)".
Prefab: `World/Maps/Resources/ModeMaps/Mode_vanderbilt.prefab` (GUID kept; contents rebuilt by the builder).

## Folders
- `Scripts/VanderbiltCampusMap.cs`: the map root (IWorldQuery, ISyncedWorld, IMapFootprint, IRebuildable).
  Static map: `Blocks` is empty, layout token `vanderbilt-campus:<layoutVersion>`, water = lowest ground - 3 m,
  anything 3 m below the water is eliminated.
- `Scripts/CampusClimbSurface.cs`: IClimbable on each building's collider group.
- `Models/T_<col>_<row>.fbx`: one per 100 m tile, written by the Blender pipeline (Git LFS).
- `Materials/mat_<id>.mat`: 27 gray URP/Lit fallback materials plus `mat_mixed` (merged HLOD meshes; vertex colors keep the IDs), made by the builder.

## Object names inside each tile FBX
| Name | Becomes |
|------|---------|
| `Terrain` / `Terrain_LOD1` | static visual ground, tile LOD0 / LOD1 |
| `COL_Terrain` | static non-convex MeshCollider (Surface layer), renderer removed. Backdrop ring tiles more than 20 m from campus have none (a note, not a problem) |
| `B_<id>_LOD0`, `B_<id>_LOD1` | building visual, tile LOD0 / LOD1 |
| `COL_B_<id>_<n>` | static convex MeshCollider under `Colliders/COL_B_<id>`, which carries `CampusClimbSurface` |
| `COLM_B_<id>` | one static non-convex MeshCollider for the whole building, under the same `Colliders/COL_B_<id>` (climbable); a tile may use this or the convex parts |
| `Trees` / `Trees_LOD1` | merged tree mesh per tile, tile LOD0 / LOD1 |
| `HLOD` (optional, from the exporter) | one merged far mesh (one submesh, material `mat_mixed`), tile LOD2 |
| `COL_Trees` | optional static non-convex MeshCollider (trunks) |
| `Ring` | backdrop boxes, visual only (no collider), tile LOD0 and LOD1 |
| `Spawn_<name>` | marker (moved under `Markers`); used as spawns only when the json has no spawns list |
| `Route_<nn>` | Alumni Lawn to Peabody walk, ordered by number (fallback when the json has no route) |
| `Climb_<id>` | point 0.6 m out from a wall of building `<id>`, +Z facing the wall (fallback for the json climb list) |

Each tile root gets ONE LODGroup (never nested): LOD0 detail to 35% screen height (~143 m from the tile center at 90 degrees, lodBias 1), LOD1 the LoD1 meshes to 20% (~250 m), LOD2 the HLOD (or the LoD1 meshes) never culled. Constants: `VanderbiltCampusBuilder.TileDetail/TileMid/TileFar`.
If the imported empties sit at the json points turned 180 degrees, the builder turns every tile back and reports `AXES` in the build report.
Visual meshes never get colliders. Material slots must be named `mat_<id>`
(`Tools/VanderbiltCampus/config/material_ids.json`); the vertex color R channel holds the material index.

## unity_map.json (written by the pipeline, read only by editor tools)
`Tools/VanderbiltCampus/config/unity_map.json`. Unity axes (X east, Y up, Z north), meters, map-root local.
Every key is optional.
```json
{
  "layout_version": 1,
  "local_area": {"center": [x, y, z], "size": [x, y, z]},
  "lowest_ground": 0.0,
  "spawns": [{"name": "Alumni Lawn", "pos": [x, y, z]}],
  "teams": [{"name": "North", "flag": [x, y, z], "spawns": [[x, y, z]]}],
  "hill": [x, y, z],
  "viewpoints": [{"name": "Kirkland", "pos": [x, y, z], "look": [x, y, z]}],
  "route": [[x, y, z]],
  "climb": [{"building": "B_123", "pos": [x, y, z], "forward": [x, y, z]}],
  "sheltered": [[x, y, z]]
}
```
- `local_area` becomes `PlayableBounds` (boundary plus margin). Missing: all visual bounds plus 10 m.
- `lowest_ground` missing: the lowest `Terrain` bound. Must be >= 0.
- `layout_version`: raise it whenever the geometry changes; the builder rewrites the match layout signature with it.
- `viewpoints` are the budget check's camera spots (missing: the 5 tile centers with the most triangles).
- `sheltered` (optional): points under a roof overhang; the play check requires `IsSheltered` there.

## Rebuild
1. Pipeline exports the tiles into `Models/` and writes `unity_map.json`.
2. `Tools > Deadly Jenga > Maps > Build Vanderbilt Campus` (batch: `DeadlyJenga.DevTools.Editor.VanderbiltCampusBuilder.BuildBatch`,
   or `BuildAndCheckBatch` to build and run step 3 in one session; exit 1 only for real build problems).
   Report: `Logs/Validation/VanderbiltCampusBuild.txt`. It also updates the lobby catalog entry and
   `GameSession/Modes/Resources/MapMatchLayouts/Campus Match Layout.asset`. With no tiles it changes nothing.
3. `Tools > Deadly Jenga > Validation > Check Vanderbilt Campus` (`VanderbiltCampusChecks.RunBatch`):
   mesh data, colliders, tile size, Quest budgets, overhead render. Report `VanderbiltCampusChecks.txt`.
4. `Tools > Deadly Jenga > Validation > Play Check Vanderbilt Campus` (`VanderbiltCampusPlayChecks.Run`, gray: `RunGray`).
   Report `VanderbiltCampus-play.txt` / `VanderbiltCampus-play-gray.txt`.

If `Presentation/.../Campus Visual.prefab` exists, the builder nests it as the child "Visual"; without it the map is gray and still plays.

## Stage 5: gameplay, streaming, looks, checks

### Play area and towers
- The whole campus is visible and walkable. Towers, spawns, team areas and objectives sit inside a **play area**
  (default: 150 m radius around Alumni Lawn). `PlayableBounds`, `Center`, `Radius` and `TopHeight` describe the
  play area, so bots (their NavMesh is baked over `PlayableBounds`) and disasters stay inside it.
  `IMapFootprint` still describes the whole campus.
- Towers use the existing castle blocks and `CastleLayout` (as `CastleGenerator` does): `kind: tower` is a single
  Jenga tower (`size` = courses), `kind: castle` is a small block castle (`size` = keep courses). Each stands on a
  flat static plinth. They are built at runtime only (`Rebuild()`); the prefab stores only the list.
- Layout token: `vanderbilt-campus:<layoutVersion>:towers<count>-<hash>`. Peers with different towers refuse the layout.

### gameplay.json (`Tools/VanderbiltCampus/config/gameplay.json`, read by the builder)
Campus meters on the ground plane (x east, y north = Unity x and z); heights are sampled from `COL_Terrain`.
Every key is optional; without the file the builder generates a default (castle + two towers on the flattest open
8 m lawn cells inside the play area, spawns from unity_map.json plus a ring, two team areas west/east).
```json
{
  "play_area": {"center": [x, y], "radius": 150},
  "towers": [{"id": "alumni_castle", "kind": "castle", "size": 12, "x": 0.3, "y": -1.3, "yaw": 58.5}],
  "spawns": [{"name": "AlumniLawn_E", "x": 30.6, "y": -8.6}],
  "team_areas": [{"name": "Northwest", "flag": [x, y], "spawns": [[x, y], ...]}],
  "objectives": [{"kind": "hill", "x": 12, "y": -24}]
}
```
Points outside the play area, without ground, on a tower plinth or blocked are skipped and listed as
`NOTE GAMEPLAY ...` in the build report. A blocked tower is nudged up to 12 m to open ground first.
Modes using it: `hill` (King of the Hill: `objectives` hill) and `ctf` (Capture the Flag: the first two `team_areas`).

### Streaming (`CampusTileStreamer`, on the map root, Play Mode only)
Tiles farther than `streamDistance` (300 m) from every character and the camera are forced to their far LOD level
(HLOD, or the LoD1 meshes incl. the backdrop ring) and their `Colliders` child is switched off. Tiles touching
the play area (+30 m) always stay loaded. Local only; never changes the layout token.

### Lighting
One realtime sun (checked in Play Mode). Baked lighting is not used. `VanderbiltCampusLighting.BakeProbeBatch`
is an optional CPU timing probe (off by default, not in the all-checks job; it deletes its scratch scene).

### Looks
`CampusPalette` (Presentation) already has the switch: its `look` field (Candy/Plain) and context menu.
`VanderbiltCampusChecks.LooksBatch` renders both palettes from every hero spot.

### Stage 5 checks (all write Logs/Validation, all exit with 0/1)
| Batch entry | What | Report |
|---|---|---|
| `VanderbiltCampusChecks.RunBatch` | structure, gameplay, viewpoint budgets, overhead renders | `VanderbiltCampusChecks.txt` |
| `VanderbiltCampusChecks.TileBudgetBatch` | busiest spot of every tile, lodBias 1 | `VanderbiltCampusTileBudget.txt` |
| `VanderbiltCampusChecks.RoutesBatch` | spawn-to-center connectivity, route capsule/step sweep | `VanderbiltCampusRoutes.txt` |
| `VanderbiltCampusChecks.LooksBatch` | candy/plain from every hero spot | `VanderbiltCampusLooks.txt`, `VanderbiltCampus/looks/*.png` |
| `VanderbiltCampusStage5PlayChecks.BotSmokeBatch` | towers, sun, streaming, 8 bots for 60 s | `VanderbiltCampus-stage5-play.txt` |
| `VanderbiltCampusStage5PlayChecks.ModesBatch` | King of the Hill + Capture the Flag, 20 s each | `VanderbiltCampus-stage5-play.txt` |
| `VanderbiltCampusStage5Checks.RunAllBatch` | all of the above in one job | `VanderbiltCampus-stage5.txt` |
