# Handoff

Updated 2026-11-19 (after the PC restart at about 21:48). Owner: Vanderbilt-Main (one chat runs Stages 0-5).

## Branch
- Branch: `claude/vanderbilt-campus-062ef7` on `origin` (https://github.com/Vanderbilt-VR-2026/DeadlyJenga).
- Base: `Evan_Sprint_4` at `9342eff8eb88d3261265e72f694d9c0acdebffff` (rebased from `main` on request, before
  any push).
- Worktree: `.claude/worktrees/vanderbilt-campus-062ef7` under the central checkout (never the central
  checkout itself).
- Archive (outside git): `C:\Users\evanp\VanderbiltCampusData`. Tools (outside git):
  `C:\Users\evanp\VanderbiltCampusTools` (micromamba + PDAL, COLMAP 4.2.1 CPU, ExifTool).

## Done so far
- Spec package committed. Pipeline scaffold: `Tools/VanderbiltCampus` (manifest, material IDs, tool check).
- Tools installed and checked (`pipeline/check_tools.py`).
- Stage 0 downloads: OSM snapshot, campus boundary + ring, Metro footprints/canopy/2026 orthophoto, NAIP,
  D22 LiDAR as the USGS EPT crop, 2011 LiDAR EPT crop. `manifest.py verify`: 964 ok, 0 problems (after
  the restart).
- Unity code written and compile-checked (coordinator precheck), not yet run in Unity: World map component,
  Presentation palette/atmosphere, GameSession lobby title, DevTools builder and checks.

## In progress
- Campus frame, per-tile LiDAR, rasters, Stage 0 render and results file (worker resumed after restart).
- Public on-campus sample photos (Wikimedia Commons, KartaView) as the stand-in capture (worker resumed).
- Original D22 LAZ tiles: 1 of 10 downloaded; USGS rockyweb rate-limits (HTTP 429). Resume with
  `Tools/VanderbiltCampus/.venv/Scripts/python.exe Tools/VanderbiltCampus/pipeline/fetch_lidar.py --project TN_DavidsonCounty_D22 --pause 10`.

## Next
Stage 1 (terrain, buildings, trees, Blender export, Unity build + play check through the coordinator),
then Stages 2-5 in order.

## Re-run commands
See `Tools/VanderbiltCampus/README.md` (install, Stage 0 run order). Tests:
`Tools\VanderbiltCampus\.venv\Scripts\python -m pytest Tools/VanderbiltCampus/tests -q`.

## No Unity jobs have been submitted yet
Nothing was queued or running in the coordinator at the restart.
