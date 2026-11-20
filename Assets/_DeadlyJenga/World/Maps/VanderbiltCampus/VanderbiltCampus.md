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
