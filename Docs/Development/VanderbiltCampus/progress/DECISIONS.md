# Decisions

Each entry: choice, alternatives, why. Newest at the bottom. Owner: Vanderbilt-Main.

## D001 Archive location (Stage 0)
- Choice: `C:\Users\evanp\VanderbiltCampusData` (set in `config/local.json`).
- Alternatives: `D:\VanderbiltCampusData` (no D: drive on this PC); inside the repo (forbidden).
- Why: only C: exists, with 314 GB free. Moving to a drive with >= 500 GB free is on EVAN-TODO.

## D002 One chat runs all stages
- Choice: this chat (Main) runs Stages 0-5 in order without stopping between them; Evan reviews at the end.
- Why: Evan's instruction. Stage "Done when" checks still gate each next stage.

## D003 Palette encoding: vertex color index (Stage 0)
- Choice: every exported vertex carries its material index in vertex color R (`index/255`, G/B/A = 1);
  submeshes are also named `mat_<id>`. Index list in `Tools/VanderbiltCampus/config/material_ids.json`.
- Alternatives: UV1 pointing at a palette swatch (STANDARDS §7 allows either).
- Why: Unity uses UV1 (mesh `uv2`) for lightmaps, and Stage 4/5 bake lightmaps per tile. Vertex color keeps
  UV0 for the bake and UV1 for lightmaps, with nothing repurposed. Submesh names still let Presentation pick
  materials directly if it prefers.

## D004 Starting palette (Stage 0)
- Choice: two colors per ID in `material_ids.json`: `candy` (Deadly Jenga glossy candy/plastic, the default
  look) and `plain` (true-to-life flat colors for the Stage 5 comparison look). Gray fallback `#9A9A9A` when
  Presentation is removed. Presentation owns the final values.

## D005 Player-facing map name (Stage 0)
- Choice: "Campus" (lobby title "Campus (LoD2)" while massing only). Data and editor tools may use
  "Vanderbilt" and OSM building names; nothing player-facing does until Evan records a trademark decision
  (STANDARDS §12).
