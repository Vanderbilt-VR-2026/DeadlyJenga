# Stage 4: style kits and the first full-detail building

Goal: a repeatable way to turn LoD2 + measured facades into clean LoD3
buildings, proven on Kirkland Hall at LoD3+.

## Build

### Style kits (Blender)
- One kit per architecture family seen on campus. Starting set (Stage 2's
  shot list confirms): Victorian Gothic (Kirkland), Collegiate Gothic,
  Georgian/Neoclassical (Peabody), 20th-century brick institutional, and
  modern glass/concrete.
- Each kit: parametric pieces built with Geometry Nodes or Python (window
  types with frame, sill and lintel; doors; arches; columns and pilasters
  with base and capital; cornice and string-course profiles; buttresses;
  dormers; chimneys; balustrades; steps), each with UV0, material IDs and
  LoD versions.
- Pieces are sized from measurements, not fixed: a window piece takes width,
  height, arch type and depth.

### Facade assembler (`blender/assemble_building.py`)
- Inputs: the LoD2 mesh, `facade_layout.json` per facade, the chosen kit,
  optional dense reference mesh.
- Cuts openings, places kit pieces at measured positions, extrudes
  projections and recesses by measured depth, adds roof features (dormers,
  chimneys, tower details) from LiDAR and photos.
- Applies the merge rule (STANDARDS.md §4) and writes every merge to the
  ledger.
- Outputs the authored `.blend` and LoD3 → LoD2 → LoD1 → impostor exports,
  plus `model.json` with accuracy numbers.

### Hand finishing for LoD3+
- Hero detail the kit can't make (Kirkland's tower, tracery, entrance
  carving) is modeled by hand in Blender, scripted through the Blender MCP
  or headless Python, over the photos projected from their aligned cameras.
- Still follows the 5 cm merge rule and the ledger.

### Accuracy check (`pipeline/check_building.py`)
- Projects the finished model's edges into every aligned photo and measures
  the offset to image edges and to the reviewed features; reports median
  and 95th percentile per facade (STANDARDS.md §5).
- Produces side-by-side images: photo, model overlay, model render from the
  same camera.

### Into Unity
- Kirkland replaces its LoD2 version in its tile; LOD group set up;
  colliders simplified (walls, ledges and steps climbable, tracery not).
- Presentation: palette material shows the IDs; baked AO and a lightmap
  pass for the tile.

## Done when
1. Kirkland Hall is LoD3+ in Unity, within 0.1 m on LoD3 features and
   0.05 m on hero trim (or every exception listed with its reason).
2. Its ledger and sources are complete, and every surface has UV0 and a
   material ID (an automated check).
3. The Quest budget from the Alumni Lawn viewpoint passes in the editor.
4. Matched-camera comparison images (≥ 6 viewpoints) are in the results
   file.
5. The kits and the assembler build a second, non-hero building with no
   hand work, to prove they repeat.
6. Solo Play Mode on Game.unity through the coordinator: climb Kirkland's
   steps and a ledge.
