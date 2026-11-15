# Tycoon standalone tsunami frame times

Historical first checkpoint: the 24-bot Windows player improved from 21.4 to 26.1 typical FPS while the tsunami was moving blocks, a 22% gain across two runs per revision. The 50% goal for this setup was about 32.2 FPS.

## Builds and measurement

- Integration target `origin/Evan_Sprint_4`, base `cdac1fd7fbb801c615808593588d25469c26e1ba`.
- Benchmark baseline `151249891dca7dec5dc6d9ce972bb4fbf777cfbc`. Measured gameplay candidate `582a96089adee9969a9f7f013b51dd0e8d49f328` on `codex/tycoon-impact-profile`. Both descend from the base. Core is unchanged.
- Unity 6000.3.23f1 Windows development players, AMD Ryzen 7 7730U / Radeon integrated graphics, D3D11 at 960×540. Both used a 90 FPS cap and disabled VSync. The candidate rebuilt scripts against the baseline player output. Packed scene and resource hashes matched. The two GUIDs in `boot.config` changed.
- Start from Home, enter Developer, and select Tycoon. Verify the 775-block world and the actual requested survivor population. Allow four seconds to settle, then sample eight seconds before the wave. Start the catalog tsunami with seed 71293, speed 23 m/s, height 27 m and a one-second warning.
- Begin the impact window once at least two displaced blocks are moving and the wave is active. Authority also requires local tsunami targets. Record eight seconds from that point and verify real displacement. Report the reciprocal of the median frame time. For the 24-bot repeated case, take the median of the two run medians before conversion to FPS.

## CPU trace and candidate

The standalone diagnostic trace recorded 126 impact frames while blocks moved. Typical inclusive main-thread time was 13.6 ms in `CastleSurvivor.Update`, 4.2 ms in `CastleBotBrain.LateUpdate` and 1.4 ms in `TsunamiHazard.FixedUpdate`. The render thread spent most of the measured time waiting. The diagnostic trace has recording overhead and is separate from the FPS runs.

The Characters/Player candidate avoids repeated collider layer writes, unused desktop preference reads for driven bots, zero-displacement support moves for bots, and ground casts while a survivor is rising. It reuses capsule geometry within each ground query and keeps carry on moving supports. New profiler markers split survivor time into bot input, motor and ground queries.

The 24-bot baselines included 145 and 146 moving-block frames and peaks of 35 and 28 blocks displaced more than one metre. The equivalent candidate runs included 199 and 206 moving-block frames and peaks of 25 and 26 blocks. Pre-wave FPS ranged from 31.8 to 33.4 before and from 28.0 to 35.1 after. Those short pre-wave trials do not establish a general FPS gain.

## 52-bot stress and multiplayer

The controlled 52-bot solo sample measured 15.27 typical FPS before and 13.31 after during the impact window, with 120 and 99 moving-block frames and peaks of 34 and 31 blocks displaced more than a metre. The pre-wave samples were 19.10 and 21.87 FPS. No competing Unity Editors were observed in those two runs. Two other candidate stress runs overlapped unrelated Unity jobs and are excluded from this comparison. The one controlled pair shows a stress regression and warrants investigation before integration.

The final 24-bot pair measured 19.95 to 21.09 typical impact-window FPS on the host and 60.35 to 60.45 on the guest. In the subset of guest frames with observed replica motion, the medians were 57.9 and 54.4 FPS, from 33 and 29 frames. One unrelated Editor started in the last few seconds of the baseline impact sample, so the small differences do not establish a multiplayer FPS gain.

Both candidate peers loaded the world in the same Fusion room and independently confirmed return to the lobby. The guest observed the replicated wave and at least 29 blocks displaced over one metre. The two clients shared one Windows PC. Cross-device and Quest performance remain unverified.

The source is a benchmarked checkpoint. Integration remains pending. In the 52-bot impact window, the aggregate script update median rose from 36.0 to 39.1 ms and physics simulation from 4.4 to 6.3 ms. Those counters describe the regression without identifying its cause.

## Reproduction

Build the Windows development player using `DeadlyJenga.CastlePrototype.Editor.CrossProcessBuild.RunBatch` with `--dj-tycoon`. Start it with `--dj-tycoon --dj-tycoon-impact --dj-bots=24 --dj-smoke=solo --dj-smoke-dir=<result-directory>`. Use 52 for the solo stress case. For peers, start independent `host` and `guest` processes with the same result directory and bot count. They join a real Fusion room by code and verify remote displacement and normal return to the lobby. Add `--dj-impact-raw-profile` to a separate diagnostic run for the binary Unity Profiler trace.


## November 15 overhaul acceptance (in progress)

The follow-up request expands this checkpoint into sustained CPU, navigation and multiplayer work. The original results above remain historical; instrumentation and new source are not proof that a target passed.

- First pass: at least 1.5 times typical FPS in matched 24- and 52-bot active-impact cases. Then attempt twice baseline, then 60 FPS. Typical FPS is 1000 / median completed-frame milliseconds; also retain total frames / elapsed and p95/p99/max milliseconds. No substitution of worst-frame wins for typical performance.
- Compare solo and independent host/guest with identical map, population, settings, resolution and active disaster phase. Target at most 3 FPS additional loss; retain source, artifact hashes, phase alignment and competing-process evidence. Local two-process results are not separate-device performance.
- Exercise tsunami, Planet Killer and tornado, plus simultaneous heavy-disaster stress with 52 bots. Count active disasters, moving/displaced blocks, actual surviving bots and bot progress so removed workload cannot manufacture improvement. Include desktop Editor Play Mode and Windows standalone separately. Quest comes later.
- Measure peer update arrival intervals and state age, latest pose/world sequence, block displacement agreement, bot/player movement and disaster state. Rendering fast while receiving stale state fails acceptance.
- Exercise discovery -> visible invite -> acceptance -> actual same Fusion lobby, ten occupied human slots, host departure/migration during a round, and normal completion. Synthetic socket/codec tests are preliminary only.
- Test travel on structurally different maps, physical high-tower descent, jumps, wall catches, lower-route drops, sand escape and tight-space recovery. Mutate/remove/add supported geometry after initial navigation construction, then command destinations across the reachable map. Record arrival, blocked duration, retries and physical movement. No teleports to count as traversal, no map-specific hard-coded route substitute. Physically disconnected destinations must report unreachable/retry appropriately rather than claim success.
- Preserve full debris, collisions, visuals and bot reactions. Core additions require approval of the exact additive contract. Each change requires evidence before integration; a regression leaves the candidate unaccepted.

Current diagnostic revision: `dde089c01dd802165a38b702374da638c8a95f03`, based on current Sprint 4 `e6d1bf0909f8a32bbf871cca39423df3dd3b56d4` plus the prior unaccepted motor checkpoint. The new scopes distinguish motor synchronization, capsule sweep, bot movement decision, objectives and steering. Runtime results pending. Existing packed baseline assets are retained for the script-only timing experiment; this is not a newly packaged current-art release.


### Fresh CPU investigation and repeat baselines

All entries below are separate-process Windows development players with the settings above. Each impact sample contains real moving blocks; no raw-profiler run is used for FPS comparisons. These are single runs per cell, not a statistically established gain.

| Scripts | 24 bots impact FPS | 52 bots impact FPS | Evidence |
|---|---:|---:|---|
| Original `151249891dca7dec5dc6d9ce972bb4fbf777cfbc`, refreshed | 24.77 | 15.72 | Original executable, packed assets and task DLL hashes reverified against original run manifest |
| Finger candidate `aeea2111272f243a277cb578812c56cd38eaabe5` | 28.91 | 18.41 | 810 native finger world-pose equivalence checks; ordinary lobby return |
| Spatial candidate `e82517cbbc2e696764bbaebcc60a3f274fb878fd` | 27.17 | 20.11 | Exhaustive/spatial candidate lists match for first/middle/last bots before and after collapse; ordinary lobby return |

The spatial candidate is approximately +10% at 24 bots and +28% at 52 against the fresh baseline, not +50%. Its 52-bot impact p50 is 49.739 ms, p95 70.746 ms and max 102.113 ms. All 153 sampled impact frames contain moving blocks, with at least 28 blocks displaced over one metre. The 24-bot run has 199 moving-block frames and 26 displaced over one metre, p50 36.802 ms, p95 70.127 ms and max 111.994 ms. There is no claim that spikes are eliminated.

The original refreshed 24-bot baseline (40.374 ms) is materially faster than the historical 45.6–47.6 ms baselines. Do not select the old denominator to claim a larger improvement. The refreshed 52-bot baseline is 63.595 ms. Repeated matched trials remain necessary.

The new 52-bot raw CPU trace identifies exclusive median costs of 2.85 ms in the motor, 2.82 ms in avatar posing, 2.31 ms in match visuals, and 2.24 ms in population/planning work. Finger inheritance removed repeated digit world-transform work; spatial queries avoid inspecting every block for local fallback candidates. No bot update-rate, collision, debris-count or visual-content reduction was used.

Candidate `0244039b8e4946618513592a75cd75b0535f93ad` reuses neighbor-sort distances and reach parameters, avoids unchanged full-health dry-overlay writes, adds visual cost markers, and extends the fixture to production tornado, Planet Killer and combined impact. Compilation passed; selected runtime results follow. Planet Killer timing must latch actual land contact, not its approach; aftermath remains eligible after the meteor hazard ends. Combined tests require active wave and tornado plus recorded planet land contact and moving blocks. Reproduce with `--dj-impact-hazard=Tornado`, `PlanetKillerMeteor` or `Combined`; omission retains the tsunami scenario.

Source is on `codex/tycoon-impact-profile`, intended target `origin/Evan_Sprint_4`, integration base `e6d1bf0909f8a32bbf871cca39423df3dd3b56d4`. `e82517cbbc2e696764bbaebcc60a3f274fb878fd` was pushed and remote-SHA verified. No shared-target integration, current-art repackaging, Editor Play Mode stress acceptance or Quest/APK claim is implied. All expanded acceptance gates above remain open.


### Route/health checkpoint and replication failure

`0244039b8e4946618513592a75cd75b0535f93ad` compiled, passed the 810 pose checks, and completed 52-bot standalone tsunami and two-process 52-bot tsunami runs. Solo impact was 19.63 FPS (50.940 ms median); host 18.48 FPS (54.123 ms); guest 59.32 FPS (16.857 ms). The 1.15 FPS host gap is a single same-PC comparison, not general acceptance.

Replication is explicitly failing: guest world receipt age during impact was 581.909 ms median, 1167.953 ms p95 and 1299.362 ms maximum. Guest human pose application age was 260.208 ms median and 1024.803 ms maximum. The guest showed block motion on just 21 of 450 impact frames despite receiving 23 displaced blocks. High render FPS does not make this responsive. Both peers completed ordinary return to the same lobby.

The standalone tornado scenario used height 65 m, funnel 16 m, force 7, reach 28 m, warning 1 s, duration 45 s. It passed with all 168 impact frames moving, 42 blocks displaced over a metre, 21.68 FPS, p95 65.486 ms and maximum 94.216 ms. No before/after tornado improvement is established yet.

Two fixture failures are retained. A 45 m Planet Killer landed and moved blocks on 144 frames, but displaced only 11 by over a metre, below the 12-block stress gate. The first combined attempt started the meteor too late: planet land contact arrived at 16.34 s, after the tsunami's last local target at 14.62 s, so the overlap gate correctly rejected the run. The next stress configuration uses a 90 m meteor and a one-second warning for all hazards; the acceptance gate remains intact.

The installed Fusion configuration allows one reliable-channel packet per network send. The next isolated candidate increases the runtime copy to eight, logs submitted traffic and pose acknowledgements, and adds ten actual process roles with individual lobby/round/migration/result/return barriers. It does not change project settings or packages. The earlier unreliable RPC experiment (`6bd7171e6d2d8dcb24101821557b266b4c99f95c`) was recovered by `e5c52a5081e787bc4bcd6b05da79c3beb6b16d8d` because native RPC invocation threw MethodAccessException; it has not been silently reinstated.
