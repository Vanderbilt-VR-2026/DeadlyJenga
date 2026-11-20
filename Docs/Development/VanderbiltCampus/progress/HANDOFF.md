# Handoff

Updated 2026-11-20 (afternoon). Owner: Vanderbilt-Main (one chat runs Stages 0-5).

## Branch
- Branch: `claude/vanderbilt-campus-062ef7` on `origin` (https://github.com/Vanderbilt-VR-2026/DeadlyJenga).
- Base: `Evan_Sprint_4` at `9342eff8eb88d3261265e72f694d9c0acdebffff` (rebased from `main` on request, before
  any push). Not merged anywhere; no PR.
- Worktree: `.claude/worktrees/vanderbilt-campus-062ef7` under the central checkout (never the central
  checkout itself).
- Archive (outside git): `C:\Users\evanp\VanderbiltCampusData`. Tools (outside git):
  `C:\Users\evanp\VanderbiltCampusTools` (micromamba + PDAL, COLMAP 4.2.1 CPU, ExifTool, Blender 5.3, ffmpeg).
- A local-only branch `claude/vanderbilt-campus-test` holds test commits with interim FBX tiles for Unity
  runs. It is never pushed.

## State by stage
- **Stage 0: done.** `STAGE-0-results.md`. All 10 original LiDAR tiles are now in `raw/`.
- **Stage 1: built, gates partly missed.** Terrain, trees, ring LoD1, buildings v2, Blender export, Unity
  builder, edit and play checks exist. Unity build #2 passed (job 3878fd2c); play check: spawn and Kirkland
  climb passed, the walk failed at waypoint 40 of 86 (route fixed in data since, not re-run yet).
  Buildings: 155 of 375 pass the strict accuracy gate (41.3 %, target 95 %), see
  `stage1-buildings-accuracy.md`. In progress: game-weight building meshes (`buildings_game.py`), then a full
  re-export and Unity re-run, then `STAGE-1-results.md`.
- **Stage 2: built on stand-ins.** `STAGE-2-results.md`. No on-site capture exists yet (EVAN-TODO item 1).
- **Stage 3: in progress** (alignment of the public street-photo stand-in set, then facade measurement).
- **Stage 4: in progress** (style kits, assembler, Kirkland LoD3+ from estimates, a second building).
- **Stage 5: not started.**

## Unity runs (through the coordinator only)
Owner name `claude-vanderbilt-campus`. Batch entries:
`DeadlyJenga.DevTools.Editor.VanderbiltCampusBuilder.BuildAndCheckBatch`,
`DeadlyJenga.DevTools.Editor.VanderbiltCampusPlayChecks.Run` and `.RunGray`.
Generated assets come back in the run's `test-output/files/Assets/`; copy them into the worktree to keep GUIDs.
Reports of past runs: archive `reports/stage1/unity_run*`.

## Re-run commands
See `Tools/VanderbiltCampus/README.md` (install and run order per stage). Tests:
`Tools\VanderbiltCampus\.venv\Scripts\python -m pytest Tools/VanderbiltCampus/tests -q`.
Full export: `Tools\VanderbiltCampus\.venv\Scripts\python.exe Tools\VanderbiltCampus\pipeline\export_all.py --jobs 6`.

## Not yet in git
The exported FBX tiles, the map prefab, match layout and visual prefab are generated and sit uncommitted in
the worktree until the Stage 1 re-run passes; then they are committed (FBX through Git LFS).
