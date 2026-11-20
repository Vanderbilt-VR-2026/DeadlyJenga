# Unity integration plan (Stage 1 onward)

Research result from a read-only study of the repo at base 9342eff8. Workers follow this.

## Facts found
- A starter map already exists: `World/Maps/Resources/ModeMaps/Mode_vanderbilt.prefab` (a CastleGenerator
  with a 44 m starter cube), catalog entry `mode-vanderbilt` titled "Vanderbilt" in
  `GameSession/Session/Resources/IslandLobby/Catalog.asset`, and `"mode-vanderbilt"` in
  `Presentation/Visuals/Menus/MapCategoryPicker.cs` ("In development" tab). **Keep the id and prefab
  path/GUID; replace the prefab contents; change the title to "Campus (LoD2)"** (STANDARDS §12).
- No GLB importer package is installed and packages may not be added: **export FBX only** (`.fbx` is in LFS).
- All `Mode_<family>.prefab` live in the World box. `MatchSetup.ApplyMap()` instantiates the catalog's
  `mapPrefab`, calls `IRebuildable.Rebuild()`, and expects `Services.World` to be set.
- `MatchModeRuntime.AuthoredLayout()` loads `Resources/MapMatchLayouts/*` and matches
  `layoutSignature == ISyncedWorld.SaveLayout()`. `groundSpawns: true` snaps spawns to ground.
- Presentation attaches by prefab nesting: the map prefab holds a nested Presentation prefab named "Visual"
  (the DevTools builder nests it). The visual hides the tropical sand/ocean through
  `CandyEnvironment.ForMap(this)` (see `ChristmasTownAtmosphere.cs`).
- Death rules: view at or below `WaterHeight + .25` drowns; standing on a collider with no rigidbody counts
  as "island" (keep `walkableIsland: true`); bots die at absolute `y < -6`. **Lowest walkable ground must be
  at y >= 0 in Unity.**
- Static maps report `Blocks` empty, `SaveLayout` a constant token, `LoadLayout` rejects `expectedBlocks != 0`.
- Climbing needs an `IClimbable` in the collider's parent chain (`Climbables.TryGet`); masks use
  `Layers.Surface`. Put the climb component on building collider roots only, never terrain or the map root.
- Survivor camera far clip 700 m; ocean is a 1600 m square; walk speed 4.6 m/s; largest existing map is
  about 360 m wide. Bots bake a NavMesh over `PlayableBounds` (huge for 1.2 km): Stage 1 uses demo mode,
  0 bots in tests, `recommendedPlayers = 1`.
- Assemblies: World references Core only. DevTools.Editor references everything. `ProjectPath`
  (`Core/Editor/ProjectPath.cs`): `Find(fileName)`, `FolderOf(fileName)`, `Report(fileName)`.
- No test in the repo supports the coordinator's `--warm` exit yet; ours adds it.

## Files per box
World/Maps (`DeadlyJenga.World`, namespace `DeadlyJenga.CastlePrototype`):
- `World/Maps/VanderbiltCampus/Scripts/VanderbiltCampusMap.cs` (IWorldQuery, ISyncedWorld, IMapFootprint,
  IRebuildable; pattern: `CantinaMap.cs`, kill floor from `EpicCityMap.FixedUpdate`), fields `localArea`,
  `lowestGround`, `spawns`; layout token `vanderbilt-campus-lod2:<n>`.
- `Scripts/CampusClimbSurface.cs` (IClimbable; copy `PowerupLabSurface.cs`; `Body => null`).
- `Models/T_<col>_<row>.fbx` (LFS), `Materials/mat_<id>.mat` (27 gray URP/Lit materials; FBX slots remapped).
- `VanderbiltCampus.md` lane note.

FBX naming the builder relies on: `Terrain`, `COL_Terrain`, `B_<id>` (LOD0/LOD1 children),
`COL_B_<id>_<n>` (boxes/convex), `Spawn_*`, `Route_<nn>` (Alumni Lawn to Peabody), `Climb_<id>`.
Colliders are made in the editor by the builder (static, `Layers.Surface`), never at runtime; visual meshes
get no colliders and `isReadable = false`.

Presentation/Visuals/VanderbiltCampus (`DeadlyJenga.CandyArt`): `CampusAtmosphere.cs` (hide sand and water,
fog under 650 m), `CampusPalette.cs` + shader/material reading vertex color R into a palette strip
(candy/plain), `Campus Visual.prefab`.

GameSession (cross-box): in `IslandLobbyCatalog.EnsureModeMaps()` before `maps=list.ToArray();` add
```csharp
AddMap(list,"vanderbilt","Campus (LoD2)","demo",1,scene,true);
var campus=list.Find(m=>m.id=="mode-vanderbilt");
if(campus!=null){campus.title="Campus (LoD2)";campus.supportedModes=new[]{"demo"};campus.walkableIsland=true;}
```
(`NetworkVersion` hashes ids and supportedModes, not titles, so this keeps the protocol hash.) Also set the
title in `Catalog.asset`. `GameSession/Modes/Resources/MapMatchLayouts/Campus Match Layout.asset` is written
by the builder (pattern: `ChristmasTownTools.MatchSetup.AuthorMatchFoundation`).

DevTools/Editor (`DeadlyJenga.DevTools.Editor`): `VanderbiltCampusBuilder.cs` (menu
`Tools/Deadly Jenga/Maps/Build Vanderbilt Campus`; pattern `BreakwaterVillageBuilder.Build`; re-assign the
catalog `mapPrefab` after `SaveAsPrefabAsset`), `VanderbiltCampusChecks.cs` (edit-mode accuracy + budget),
`VanderbiltCampusPlayChecks.cs` (batch Play Mode; pattern `BreakwaterVillagePlayChecks.Run`, survivor
driving from `CastleClimbChecks.Check`, with the DJWarm exit).

## Risks to handle
1. Bot NavMesh and hazard costs scale with `PlayableBounds`; a Core "bot area" addition may be needed later
   (report, don't build).
2. Vertical offset so lowest ground is at y >= 0; `WaterHeight = lowestGround - 3`; kill floor below that.
3. Draw calls: one submesh per material per tile adds up; merge by tile where possible (vertex color carries
   the ID).
4. Blender Z-up to Unity Y-up: verify a tile's bounds are 100 m after import.
5. Layout token and MapMatchLayout signature must change together.
