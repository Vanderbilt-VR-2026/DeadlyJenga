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

## PAUSED 2026-11-20 about 15:30 (at Evan's request). Resume here.
All background jobs of this build were stopped; no Unity slot or job is held; everything written so far is
committed and pushed (the generated FBX tiles/prefab are not: see "Not yet in git").

Interrupted step: the game-weight building meshes and the full tile re-export.
- `pipeline/buildings_game.py` ran for all 375 buildings (archive `reports/stage1/buildings_game.json`).
  Result at 0.05 m visual / 0.15 m collider tolerance: visual 2,646,210 -> 1,958,584 triangles (target about
  900,000), colliders 1,747,038 -> 1,334,913 (target about 350,000). Both targets missed, and the largest
  measured visual deviation is 0.18 m, so the step needs another pass (a looser second level, or fixing the
  roof splitting in `roof_heightfield.py`) before the export is worth running. The worker had not reported;
  treat its code as unreviewed.
- `export_all.py` was being extended to also export trees v2, props (`Props`, `Props_LOD1`, `COL_Props`) and
  the LoD3 buildings from `derived/buildings/lod3/index.json`. Check the diff before trusting it; the full
  export has not been re-run (the FBX in the worktree are from the morning export).

On resume, first (shared flow update relayed by the Unity coordinator chat on 2026-11-21): run
`py -3 $env:USERPROFILE/.codex/skills/deadlyjenga-worktree-pool/scripts/shared_flow.py brief` once, and submit
scripted Play Mode tests and captures through `shared_flow.py submit --id <stable job id> ...`. Quest APKs go
through the established builder, never ad hoc builds. Read the guide it points to before the first Unity run.

Then, in order:
1. Finish the game meshes, run `export_all.py --jobs 6` and `check_fbx.py`.
2. Local test commit (never pushed) -> coordinator `VanderbiltCampusBuilder.BuildAndCheckBatch` -> copy the
   generated assets back -> `VanderbiltCampusPlayChecks.Run` and `.RunGray` -> write `STAGE-1-results.md`.
3. Stage 5 Unity checks (`VanderbiltCampusStage5Checks.RunAllBatch`: towers, hill + ctf, bots, both palettes,
   per-tile budgets). None of the Stage 5 Unity code has run in Unity yet: expect fixes.
4. Write `STAGE-4-results.md` and `STAGE-5-results.md`; commit the final FBX/prefab/match layout/visual
   prefab (generated Presentation/GameSession assets as `[Vanderbilt cross-box]`); push.
5. Archive: `manifest.py verify` shows 20 stale records (18 `buildings/<id>/sources.json` rows from an
   earlier alignment trial, 2 re-written `game_*.npz`, 1 COLMAP work database). None is under `raw/`. Remove
   or re-record them, then verify again.
6. Final report (see BUILDER-PROMPT.md).

Known misses to report, not blockers: building accuracy gate 41.3 % strict (target 95 %); Stage 3 targets on
the stand-in street photos; no campus facade photo-measured; Kirkland and Tibbot LoD3 are estimates; nothing
tested on the headset.

## State by stage
- **Stage 0: done.** `STAGE-0-results.md`. All 10 original LiDAR tiles are now in `raw/`.
- **Stage 1: built, gates partly missed.** Terrain, trees, ring LoD1, buildings v2, Blender export, Unity
  builder, edit and play checks exist. Unity build #2 passed (job 3878fd2c); play check: spawn and Kirkland
  climb passed, the walk failed at waypoint 40 of 86 (route fixed in data since, not re-run yet).
  Buildings: 155 of 375 pass the strict accuracy gate (41.3 %, target 95 %), see
  `stage1-buildings-accuracy.md`. In progress: game-weight building meshes (`buildings_game.py`), then a full
  re-export and Unity re-run, then `STAGE-1-results.md`.
- **Stage 2: built on stand-ins.** `STAGE-2-results.md`. No on-site capture exists yet (EVAN-TODO item 1).
- **Stage 3: built.** `STAGE-3-results.md`. Two of four targets missed on the stand-in photos.
- **Stage 4: built, not in Unity yet.** Kits, assembler, checks; Kirkland LoD3+ and Tibbot LoD3 from estimated layouts.
- **Stage 5: code written, nothing run in Unity.** Play area, towers, modes, streaming, props, tree species,
  batch build, readiness check, realism bake (see `stage5-readiness.md`, `NEEDS-SHOTS.md`).

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
