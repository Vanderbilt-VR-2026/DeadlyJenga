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
- The checked Windows development build of `da62f2419262e216626f866fc58dcdfe02e8e0f3` timed out while compiling a forward shader pass. It produced no verified player. Compare standalone FPS after a successful build. Independent Editor peers can exercise the same flow with `CrossProcessBuild.RunLobbyHandoffBatch` and the same fixture flags.

## 52-bot desktop Editor stress

Exact revision `36ad212f40bb1a88e4174150e1e0798cec856d9f` completed `DeadlyJenga.DevTools.Editor.DenseWavePerformance.RunTycoonStressBatch` with zero runtime errors. This is local Play Mode on the actual Tycoon map with voices off. The three disasters start in sequence in the same simulation, so the later windows also include prior damage to the map. The report is `Logs/Validation/DenseWave-tycoon-stress.txt`.

| Window | Frames | Frame p50 / p95 / p99 / max (ms) | Blocks displaced over 1 m |
| --- | ---: | --- | ---: |
| 52 bots | 34 | 165.26 / 321.69 / 341.17 / 341.17 | — |
| Tsunami | 128 | 129.47 / 352.61 / 679.21 / 793.01 | 31 |
| Tornado | 149 | 129.27 / 184.88 / 292.93 / 357.96 | 7 |
| Planet Killer | 136 | 137.32 / 227.77 / 310.73 / 636.85 | 6 |

At least 26 bots travelled over one metre by the end of the tsunami window. The spikes exceed a playable frame budget in these Editor samples. The standalone target remains open.

## Catwalks and nearby checks

Actual Catwalks at `edea476590174206f86be9020d4b451aa93c8ff0` reached purple roof to red, red bridge to purple roof, and purple roof to lower purple. The reverse ascent took two jumps and four climbs. Red to green failed in this four-bot sample. Its recovery moved the interim target from the corner to a stage about 20 m along the wall and did not return to the original goal in the window. The fixture returned a failure and recorded zero runtime errors.

The same revision passed `DeadlyJenga.DevTools.Editor.BotPipelineChecks.RunCrossMapBatch` on the structurally different CTF map. With one controlled travel objective and ordinary movement, the bot crossed midfield from approximately (-40, 0.4, -12), travelled 75.45 m, and held within 0.6 m of the target (30, 0.4, 0). It recorded no unexplained idle time and no scored capture. The window used 55 seconds of simulated time at a fixed capture rate, so its timings are for fixture operation.

The two-process Editor invite attempt at `da62f2419262e216626f866fc58dcdfe02e8e0f3` opened a real Fusion Marina room. Admission delayed the guest, and the host timed out before sending the invite. The guest's subsequent scan occurred after that timeout. The readiness guard at `edea476590174206f86be9020d4b451aa93c8ff0` waits for the guest's actual Home session before advertising. The failed peer and Catwalks fixtures exited directly from Play Mode, leaving GDK-generated assets in their leased projects. `5670d21e4509b03671caf10fc72f43d347cb948b` routes their next run through Edit Mode so the registered GDK cleanup runs.

Integration status is pending MAIN review. Further checks include the nearby invitation and discovery rerun, the live ten-slot cohort, host migration, sand escape, narrow slabs, post-collapse goal pursuit and headset testing.

## November 15 Windows multiplayer continuation

Unity 6000.3.23f1 successfully packaged the exact `d19cd0d1036c474561d74a3ad9abb5c08e855344` Windows development player. Two independent processes ran over a real Fusion connection on one HP at 960×540 with Direct3D 11.

- **Nearby invitation and 10-slot Marina passed.** The guest received the UDP invitation, selected it without entering a room code, and joined the same Fusion room. Both clients saw the other human and the ten-slot cohort of two live clients plus eight lobby bots. Both activated the normal Play ready control.
- **GO to Play remains open.** The host entered Catwalks. The guest loaded into Game, then Fusion reported 107 input-buffer overflow warnings before the world-response watchdog disconnected it. This establishes where the loss was observed. The fixture now samples both peers' room roster, scene version, phase, world readiness, transfer counts and largest frame intervals, and requires both humans to remain connected before declaring the transition successful. This diagnostic revision awaits its first runtime check.
- **In-round migration passed.** The two developer-route peers synchronized Castle Survival. The original host left during the 20-second round. The promoted guest kept the room code, actor identity and running timer, then reached results and returned to the same Marina. Both player exit codes were zero.
- **Tycoon solo passed.** On the same build, 24 bots produced 37.9 ms p50 and 90.2 ms p95 before the wave, then 44.0 ms p50 and 98.9 ms p95 during the seeded wave. The wave median was 22.7 FPS and at least 12 blocks moved more than one metre. The owner paused the next test window before Tycoon host and guest could launch. A matched FPS and replication comparison remains open.

For the next approved window, use identical hashed Windows players on the HP host and a second laptop guest when available. Give both processes a shared fixture rendezvous directory while keeping the real UDP invitation and Fusion join paths. Run the normal Play transition diagnostic first. For Tycoon, record solo on each machine at the same resolution and rendering settings, then measure the two peer roles with the same 24-bot workload and wave settings. Compare each role to its own machine's solo baseline and retain world agreement, actor observations and raw frame samples. Keep physical headset verification separate.
