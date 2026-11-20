# Cross-box changes

Every change outside World/Maps, in its own commit tagged `[Vanderbilt cross-box]`.

| File | Box | Why | How to revert |
|------|-----|-----|---------------|
| `Presentation/Visuals/VanderbiltCampus/*` (new folder) | Presentation | Palette shader/material by material ID, atmosphere, visual prefab builder | Delete the folder; the map falls back to gray World materials |
| `GameSession/Session/Scripts/IslandLobbyCatalog.cs` (3 lines in `EnsureModeMaps`) | GameSession | Lobby entry titled "Campus (LoD2)", demo mode, walkable island | Remove the three lines |
| `GameSession/Session/Resources/IslandLobby/Catalog.asset` (title of `mode-vanderbilt`) | GameSession | Player-facing name must not say Vanderbilt (STANDARDS §12) | Set `title: Vanderbilt` again |
| `DevTools/Editor/VanderbiltCampus*.cs` (new files) | DevTools | Builder, validation and Play Mode checks | Delete the files |
| `GameSession/Session/Scripts/IslandLobbyCatalog.cs` + `Catalog.asset` (campus `supportedModes` = demo, hill, ctf) | GameSession | Stage 5: two modes must start on the campus. Changes `NetworkVersion` | Set the list back to `demo` |
| `DevTools/Editor/VanderbiltCampus*` Stage 5 files (Gameplay, TileBudget, Routes, Looks, Stage5Checks, Stage5PlayChecks, Lighting) | DevTools | Gameplay placement from `gameplay.json`, per-tile budget, route, look, bot and mode checks | Delete the files |
