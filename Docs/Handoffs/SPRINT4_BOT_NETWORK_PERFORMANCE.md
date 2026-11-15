# Sprint 4 Tycoon bot and multiplayer performance

Source branch `codex/sprint4-bot-network-performance` targets `origin/Evan_Sprint_4` through MAIN. The base is `cdac1fd7fbb801c615808593588d25469c26e1ba`. The bot change is `6c66e9727ef38c6d8d023062fc30d6a2d535d78c`. The planner reads one live transform per body while ranking contiguous samples, with the same scoring and a live landing check. Scope is Characters/Bots and DevTools. There is no Core contract change.

## Local Play Mode comparison

Unity 6000.3.23f1, Windows, catalog Tycoon in the actual Game scene, 24 bots, voices off, and the same seeded catalog tsunami. The 6-second quiet and 21-second tsunami windows are wall-clock windows. Profiler values shown here are aggregate time per recorded frame. Compare the source-only commissioning commit `0acb94cb0fb8d6cca15d56b11b6a87869777db66` with the tested candidate `b44ed25c784da2d78f77ac1f27a7f6aa759b8da6`. The latter includes the ranking change and fixes isolated batch exit after the profiling windows. The baseline completed its gameplay checks and then threw restoring an empty initial scene setup. The candidate exited cleanly.

| Window | Revision | Frames | Frame p50 / p95 / p99 / max (ms) | Destination marker mean / max (ms) |
| --- | --- | ---: | --- | --- |
| 24 bots | Baseline | 73 | 79.99 / 115.80 / 152.37 / 152.37 | 0.72 / 9.70 |
| 24 bots | Candidate | 61 | 97.63 / 142.21 / 195.51 / 195.51 | 0.87 / 8.34 |
| 24 bots and tsunami | Baseline | 215 | 91.59 / 138.73 / 267.00 / 605.34 | 0.81 / 15.26 |
| 24 bots and tsunami | Candidate | 229 | 89.03 / 130.69 / 151.02 / 223.94 | 0.43 / 7.65 |

The wave moved 31 baseline and 34 candidate blocks more than one metre. At least half the bots travelled over a metre in both runs, with zero in-game errors. These are two Editor measurements on a shared host with other Unity activity and variable physics trajectories. The destination marker fell during the wave. Whole-frame end-to-end effect and the 1–3 FPS multiplayer target still need controlled standalone measurements.

Run each revision with Unity’s `-batchmode -force-d3d11 -executeMethod DeadlyJenga.DevTools.Editor.DenseWavePerformance.RunTycoonNavigationBatch` on an isolated checkout and keep the report in `Logs/Validation/DenseWave-tycoon-navigation.txt`. The baseline batch needs manual graceful exit after leaving Play Mode if it began without an active scene. The candidate exits itself.

## Standalone and stress follow-up

- `da62f2419262e216626f866fc58dcdfe02e8e0f3` extends the opt-in DevTools standalone fixture. Use `DeadlyJenga.CastlePrototype.Editor.CrossProcessBuild.RunBatch` with `--dj-tycoon` to build a development player. In a fresh results directory per group, pass `--dj-smoke-dir=<dir>`, `--dj-smoke=<solo|host|guest>` and `--dj-tycoon`. Run solo alone, then start independent host and guest processes. Keep display settings identical. Raw times and workload assertions are written per process. Replica wave observation can lag the authoritative start. The displaced-block report evaluates an instantaneous host sample within a two-second guest observation window and records tolerance agreement.
- In fresh result directories, pair host and guest with `--dj-nearby-lobby` for discovery, then add `--dj-nearby-invite` for the invite path. The ten-slot Marina cohort means two independent clients plus eight lobby bots. A round with ten human devices needs separate testing.
- Run the independent migration fixture with `--dj-full-round --dj-migrate-round`. Validate continued phase and timer, result, and lobby return on the promoted host.
- `36ad212f40bb1a88e4174150e1e0798cec856d9f` adds `DeadlyJenga.DevTools.Editor.DenseWavePerformance.RunTycoonStressBatch` for 52 bots with tsunami, tornado and Planet Killer in sequence. The report is `Logs/Validation/DenseWave-tycoon-stress.txt`.

Integration status is pending MAIN review. Further checks include distinct-map goal pursuit, long Catwalks routes, sand escape, narrow slabs, collapse recovery and headset testing. The build and peer measurements must carry their own tested SHA and results.
