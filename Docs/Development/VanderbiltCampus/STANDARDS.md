# Vanderbilt campus: standards

Every stage follows these. If a stage needs to change one, it **adds** a
version (for example `material-ids v2`), records why in
`progress/DECISIONS.md`, and keeps old files readable.

## 1. Campus frame (coordinates)

- One local frame for everything: **meters, Y up in Unity**, origin at a
  fixed survey point near the center of campus (Stage 0 picks it, for
  example the center of Alumni Lawn, and records its lat/long and
  elevation).
- Source systems: LiDAR in its delivered projected system (record the EPSG
  code from the tile metadata), OSM in WGS84 (EPSG:4326). Stage 0 writes the
  exact transforms in `Tools/VanderbiltCampus/config/frame.json`:
  source EPSG → campus frame (translation, rotation to grid north, vertical
  datum offset), plus a test file of 10 known points and their expected
  campus-frame values.
- Blender: Z up, same meters; the export script converts axes. Unity: the
  map root sits at the world origin; tiles keep their campus-frame offsets.
- Photos and 360 frames are registered into the same frame (Stage 3). A
  model and the photos that show it must line up when both are viewed in the
  campus frame.

## 2. Area and tiles

- **Boundary**: a GeoJSON polygon of main campus (Stage 0 draws it from OSM
  `amenity=university` plus streets; includes Peabody campus). Stored at
  `config/boundary.geojson`. A 150 m **backdrop ring** outside it is built
  only at LoD1 (simple boxes) for the skyline.
- **Tiles**: 100 m × 100 m squares in the campus frame, named `T_<col>_<row>`
  (for example `T_03_-02`). A building belongs to the tile holding its
  footprint centroid.

## 3. Detail levels

| Level | Contents | Where |
|-------|----------|-------|
| LoD1 | Footprint extruded to height | Backdrop ring; far LOD |
| LoD2 | Exact footprint, eave and ridge heights, roof shape (gable, hip, flat, mansard, towers), major roof volumes | Every building, Stage 1 |
| LoD3 | LoD2 plus: windows and doors as real openings or recessed panels, columns, porticos, pilasters, cornices and string courses, buttresses, chimneys, dormers, balconies, steps and ramps, arcades | Every captured building, Stages 4–5 |
| LoD3+ | LoD3 plus: tracery, carved trim, balustrades, railings, statues, fountains, lamp heads, detailed entrances | Hero spots |

**Hero spots (starting list; Stage 2 may add):** Kirkland Hall and tower,
Alumni Lawn, Central Library and Library Lawn, Peabody Esplanade and Cohen
Memorial Hall, Furman Hall, Benson Hall, Wyatt Center, Commons, Rand Hall and
Wall, Engineering and Science Building, Featheringill/Jacobs Hall, Student
Life Center, Branscomb Quad, Memorial Gymnasium exterior. Stage 2 confirms
names against OSM and Vanderbilt's map.

## 4. The merge rule (consolidation)

- Thresholds: **LoD3 15 cm**, **LoD3+ 5 cm**, LoD2 50 cm. A feature whose
  largest dimension *off the surface* (depth or relief) is under the
  threshold is merged into its parent surface. A feature narrower than the
  threshold in both surface directions is also merged.
- Repeated small features become one shape: a dentil row becomes a molding
  strip; brick coursing becomes a flat wall; mullions under the threshold
  become a flat window with the frame kept.
- Every merge writes **one ledger entry** (section 6). The rule is the
  floor, not a cap: a builder may model more when the Quest budget allows,
  but never less.

## 5. Accuracy targets

| What | Target | Checked by |
|------|--------|-----------|
| Footprint corners | ≤ 0.3 m from OSM/Metro footprint (or LiDAR edge where they disagree, decision logged) | Stage 1 script |
| Eave and ridge heights | ≤ 0.2 m from LiDAR | Stage 1 script |
| Windows, doors, columns (LoD3) | ≤ 0.1 m from photo-measured positions | Stage 4/5 script: reprojection into aligned photos |
| Hero trim (LoD3+) | ≤ 0.05 m | Same |
| Terrain | ≤ 0.15 m from LiDAR ground points | Stage 1 script |
| Tree position / height | ≤ 1 m / ≤ 1.5 m | Stage 1 script |

Each stage's results file reports the median and 95th-percentile error per
building and lists every building over target.

## 6. Ledgers and source maps (JSON, one file per building)

Building ID: `B_<osm_id>` (or `B_new_<n>` if not in OSM). Stored in the
archive at `buildings/<id>/` and mirrored (small text only) in git at
`Tools/VanderbiltCampus/buildings/<id>/`.

`ledger.json`:
```json
{
  "building": "B_123456",
  "version": 1,
  "entries": [
    {
      "id": "L0001",
      "type": "dentil_row",
      "merged_into": "cornice_south_01",
      "center": [12.40, 18.75, -3.10],
      "size": [9.6, 0.08, 0.06],
      "count": 64,
      "material": "limestone",
      "photos": ["2026-12-02/IMG_0412.dng", "2026-12-02/IMG_0413.dng"],
      "restore_hint": "normal_map"
    }
  ]
}
```
`restore_hint` is one of `normal_map`, `geometry`, `decal`, `texture_only`.

`sources.json`: for each facade (`north`, `east`, ..., or named faces),
the photo IDs, 360 frames and scans that see it, with coverage percentage
and mean viewing distance. Stage 3 writes this; later stages only add.

`model.json`: the building's name, style kit, LoD reached, accuracy numbers,
material IDs used, file names of the authored `.blend` and exported meshes.

## 7. Material IDs (v1)

Each ID has a candy palette color (Stage 0 picks colors in the Deadly Jenga
candy/plastic direction; Presentation owns the final values). Do not reuse an
ID for a different material. Add new IDs only by appending.

| ID | Use |
|----|-----|
| `brick_red` | Red/brown brick walls |
| `brick_buff` | Tan or light brick |
| `limestone` | Light stone trim, columns, facades |
| `stone_rough` | Rough-cut or rubble stone |
| `concrete` | Modern concrete, plinths |
| `stucco` | Painted render |
| `metal_panel` | Modern cladding |
| `glass` | Windows and glass walls |
| `window_frame` | Frames and mullions |
| `door_wood` | Wooden doors |
| `door_metal` | Metal and glass doors |
| `roof_slate` | Slate or dark shingle roofs |
| `roof_copper` | Copper and green-patina roofs, domes |
| `roof_flat` | Flat membrane roofs |
| `roof_tile` | Clay tile |
| `metal_trim` | Railings, gutters, lamp posts |
| `paving_brick` | Brick walks |
| `paving_concrete` | Concrete walks |
| `asphalt` | Roads and parking |
| `curb` | Curbs and edging |
| `grass` | Lawns |
| `planting` | Beds and shrubs |
| `bark` | Tree trunks |
| `foliage` | Tree canopies |
| `water` | Fountains and pools |
| `wood` | Benches, decks |
| `signage_blank` | Sign faces (always blank in release builds) |

Mesh rule: every mesh exported to Unity has **UV0** (unwrapped, no overlaps,
texel density about 256 px/m for LoD3 and 512 px/m for LoD3+ at bake time)
and stores the material ID as a submesh named `mat_<id>`. For the palette
look it also has **UV1** pointing at the ID's swatch, or vertex color
carrying the ID index (Stage 0 picks one, logged). UV0 is reserved for the
future bake and must never be repurposed.

## 8. Archive (outside git)

Root: the path in `Tools/VanderbiltCampus/config/local.json` (`data_root`;
the file is git-ignored, with a committed `local.example.json`). Default
suggestion: an external drive or `D:\VanderbiltCampusData`.

```
data_root/
  manifest.sqlite            every file: path, sha256, size, source, date, stage, license
  raw/
    lidar/                   original LAZ tiles, untouched
    osm/                     dated snapshots
    metro/                   footprints, canopy, orthophotos as downloaded
    capture/<date>/<device>/ originals straight off the camera/phone, untouched
    gis/                     anything Vanderbilt GIS shares, with their terms
  derived/
    capture/<date>/          blurred copies, masks, frame extracts, quality flags
    alignment/<set>/v<n>/    COLMAP/RealityScan exports, registration to LiDAR
    dense/<building>/v<n>/   optional dense meshes / splats (GPU jobs)
    terrain/ buildings/ trees/
  buildings/<id>/            .blend, exports, ledger, sources, model.json
  reports/                   stage reports, renders, error maps
```

- Files under `raw/` are written once and never changed. Any fix is a new
  file under `derived/`.
- The manifest is updated by every script that writes to the archive. A
  `verify` command re-hashes and reports missing or changed files.
- Backup: the to-do list tells Evan to keep a second copy (another drive or
  cloud). Git holds only scripts, configs, small JSON and final Unity
  assets.

## 9. Capture requirements (for the future bake)

- Photos: RAW (DNG) plus JPEG if the device allows, else highest-quality
  JPEG/HEIC; fixed exposure and white balance per set; no filters, no HDR
  merging, no digital zoom; 70–80% overlap; overcast or even light
  preferred; a gray card (or color checker) in the first frame of each set.
- 360 video: highest resolution, fixed exposure, walking about 1 m/s, two
  passes at about 1.0 m and 1.7 m camera height where practical.
- Keep original files; never let the phone or app "optimize" or delete
  them.
- Faces and plates are blurred in `derived/`, never in `raw/`. `raw/` never
  leaves Evan's machine or his backup.

## 10. Quest 3 budgets (editor-checked; headset not assumed)

Per view, from the busiest spot in each tile:
- ≤ 250k visible triangles, ≤ 150 draw calls, ≤ 1 realtime shadowed light.
- Texture memory for the whole campus look ≤ 128 MB (palette look should be
  far below this).
- LoD chain per building: LoD3/3+ → LoD2 → LoD1 → impostor card, switching
  by screen size. Colliders are separate simple shapes, never the visual
  mesh.
- Tiles stream in and out around the player beyond a radius set in Stage 5.

## 11. Unity integration

- **World/Maps** (the map lane): a `VanderbiltCampusMap` component on the
  map root implementing `IWorldQuery` (including `PlayableBounds`),
  `ISyncedWorld`, `IMapFootprint` and `IRebuildable`, following the patterns
  in `BlenderModelMap.cs` and `ChristmasTownMap.cs`. Static geometry is
  kinematic or static colliders, per World's rules. The map must play as
  gray boxes with Presentation removed (material IDs fall back to plain
  gray).
- Map meshes and colliders live in a World/Maps folder for the campus;
  `.blend` source files stay in the archive, and exported FBX/GLB go into
  Unity.
- **Presentation/Visuals**: the palette atlas, campus materials and shaders,
  sky, backdrop and environment art, in a `VanderbiltCampus` folder there.
  Presentation reads material IDs from submesh names; World never names
  Presentation classes.
- **GameSession**: a lobby entry in `IslandLobbyCatalog` and a
  `MapMatchLayout` asset (spawns, team areas, objectives), as Christmas Candy
  Town does. Cross-box, pre-approved.
- **DevTools/Editor**: a `Tools > Deadly Jenga > Maps > Build Vanderbilt
  Campus` menu that imports the exported tiles and rebuilds prefabs on
  request (never on load, never saves Lobby.unity or Game.unity), plus
  validation menus (accuracy, budgets, route connectivity).
- Editor tools find assets by name (`ProjectPath.Find`), not hard-coded
  paths. No runtime `Assets/...` paths.

## 12. Names and marks

- The map's player-facing name is a placeholder (Stage 0 picks a neutral
  one, for example "Campus") until Evan records a trademark decision.
- Signs, banners and logos use `signage_blank`. Real sign text and logos may
  exist only behind a dev-only switch that release builds strip.
- Building names from OSM are fine in data and editor tools.

## 13. Tools folder

Pipeline scripts live in `Tools/VanderbiltCampus/` at the repository root:
```
Tools/VanderbiltCampus/
  README.md            how to install and run each step
  requirements.txt     pinned Python packages
  config/              frame.json, boundary.geojson, material_ids.json,
                       local.example.json (local.json is git-ignored)
  pipeline/            one small script per step, each runnable alone
  blender/             headless Blender scripts and the style kits (.blend
                       kits are stored in the archive if over a few MB)
  buildings/<id>/      small JSON mirrors of ledger/sources/model
  tests/               fast checks with tiny sample data
```
Every script: plain comments, `--help`, reads config, writes to the archive
and the manifest, and exits non-zero on failure.
