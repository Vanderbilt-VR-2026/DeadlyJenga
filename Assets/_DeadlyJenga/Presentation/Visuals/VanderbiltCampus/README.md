# Campus look (Presentation / Visuals)

The campus map (World) arrives in gray: its models use materials named `mat_<id>`, and every vertex
stores its material ID in vertex color R (`index / 255`). This folder gives it color and weather.

- `CampusPalette.shader`: one opaque URP material for the whole campus. It looks the vertex's ID up in a
  32x1 palette strip (pixel i = color of ID i, alpha = extra gloss), lights it with the sun, ambient or a
  baked lightmap, shadows, a small candy highlight and fog. Has shadow, depth and lightmap-bake passes.
- `CampusPalette.cs`: in Play mode swaps every `mat_*` material under the map for that one material and
  puts the gray ones back when disabled. `look` picks Candy or Plain.
- `CampusPaletteColors.cs`: the 27 candy and 27 plain colors, copied from
  `Tools/VanderbiltCampus/config/material_ids.json`. Builds the strips at runtime if the PNGs are missing.
- `CampusAtmosphere.cs`: hides the tropical sand and ocean, sets a candy day sky, one sun and fog
  (250 to 620 m). Restores everything when disabled and steps aside for power-outage and vampire disasters.
- `Editor/CampusVisualBuilder.cs`: menu Tools > Deadly Jenga > Maps > Build Campus Visual. Writes
  `CampusPalette.mat`, `CampusPaletteCandy.png`, `CampusPalettePlain.png` and `Campus Visual.prefab` here.

Switch looks: select the Visual object under the map, set `Campus Palette > Look` (works live in Play
mode), use its right-click menu "Use plain look", or call `SetLook(CampusPalette.Look.Plain)`.
