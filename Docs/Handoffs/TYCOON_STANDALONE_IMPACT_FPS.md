# Tycoon standalone tsunami frame times

Current overhaul status: +50% typical FPS is not met, and neither desktop Editor smoothness nor replication freshness is accepted. Source changes are isolated on the task branch; no shared integration or APK claim.

## Checkpoint a7fa560d and native-world experiment

The exact tested source was 7fa560ded398ebefc0c59706ec8498113550dc7. All ten independent Fusion processes passed the round, original-host departure, single replacement authority, original timer continuation, results and lobby return. Host and one guest rendered; eight clients were headless. This is same-machine functional evidence, not ten-rendered-client performance or cross-device evidence.

Actual rendered Editor Play Mode at 960x540 with 52 bots measured 4.53 typical FPS during tsunami impact (37 moving-block frames, 26 blocks displaced over one metre). Median frame time was 220.701 ms, p95 287.245 ms. The desktop Editor target fails. The standalone figures must not be presented as Editor results.

An opt-in 1 KB native Networked-state probe received 486 atomic snapshots with 53.876 ms mean spacing and 165.222 ms maximum spacing. That probe did not transport real gameplay state. The next opt-in native-world experiment uses the existing decoder and a bounded 64 KiB native snapshot, with separate room protocol and legacy fallback. Compilation, late join, migration, large-world fallback and actual impact freshness remain pending for that experiment.

Nearby UDP invite, joining without a code and entering the same round passed; full round completion timed out. The fixture needs timer/mode diagnostics and the shader fallback needs a new runtime check. Nearby discovery remains pending.

A separate 139-frame whole-CPU/GPU diagnostic at source 644e3970 measured median CPU frame time 55.616 ms and GPU frame duration 55.171 ms. These recording-overhead samples are not FPS acceptance; GPU frame duration alone does not establish GPU execution as the bottleneck.

## Current a14239fc evidence

Native player build passed 810 finger, 4,617 deformation and 11 compression checks. The compressed native-world peer trial still failed: migration buffer assertions persisted and the guest timed out waiting for the tsunami visual. Host impact was 16.62 FPS with errors, so it is diagnostic only. This opt-in transport is not ready.

The rendered Editor CPU/GPU diagnostic had application focus on all sampled frames. It measured 3.66 typical FPS with recording overhead, 30 moving-block impact samples and 22 blocks displaced over one metre. Median simulated physics advancement was 260 ms per sample; this is not a background-focus explanation. Exclusive main-thread costs included GetComponentNullErrorMessage (17.179 ms mean, 7.692 ms median), OnGUI (14.083 / 13.576 ms), Tsunami (12.974 / 4.896 ms), and collision callback dispatch (8.072 / 7.942 ms). The next export resolves those markers to parent call paths. These diagnostic figures are not a matched before/after FPS result.

Installed SDK inspection found that WriteHostMigrationData asserts its existing target array is at most 32 KiB, but can grow that array to twice the encoded object data size. The next call can then fail. No vendor binary/package changes were made. Per-message-kind traffic instrumentation is being added because the experimental run still submitted over 1 MB through the reliable bundle path despite moving world payloads to native state.

## Native-world failure and follow-up

The d9544896 experiment compiled and passed the existing equivalence checks, but failed real peer acceptance. Host impact was 15.57 FPS (121 moving-block frames, 28 blocks over one metre); the guest observed zero eligible collapse frames. The 64 KiB state also exceeded Fusion's 32 KiB host-migration transfer buffer. These are failed diagnostic results, not accepted performance gains. Native state is still opt-in.

The follow-up bounds its native state to 24 KiB, losslessly compresses the existing world payload, keeps the original heap configuration, and budgets 16 native game-channel sends. Oversized packets fall back to the existing transport. New validation covers byte-for-byte round trips and invalid expansion lengths. Actual freshness, bandwidth, FPS, late joining and migration remain required.

A separate Characters/Player candidate snapshots the rig's matrices and rotation once per pose, avoiding repeated Unity transform queries for limbs/feet. It preserves the pose calculations and needs the existing finger equivalence and impact retests. Frame logs now include application focus and simulated physics time to diagnose the poor Editor result.

## Native checkpoint 644e3970

Exact source and verified task-branch remote: `644e397068ea6be16c1e96a6a4147b1265ea4bd2`. Native build succeeded; 810 finger-pose and 4,617 deformation position/normal/bounds checks passed. Core and packages are unchanged. No shared-target integration or APK inclusion is claimed.

The 52-bot Planet Killer run with Burst deformation measured 19.99 typical FPS, p95 135.898 ms and worst 205.561 ms, versus the previous 18.39 FPS, p95 212.794 ms and worst 457.949 ms. This is one exploratory comparison, not a repeated acceptance result. All 130 sampled impact frames had moving blocks, and 36 blocks moved more than one metre. The 50% target remains unmet.

Ten independent processes joined the same real Fusion lobby with nine remote avatars each and loaded Game. Following host departure, exactly one replacement authority continued the original timer; all nine remaining clients reached results and returned to the same lobby. Host and the rendered guest exited successfully. Eight headless guests failed the clean-log assertion because results portraits/podiums called Camera.Render without a graphics device. The next candidate skips only those unavailable headless renders; rendered content remains enabled.

A separate 52-bot peer experiment requested reliable streaming at 60 Hz, clamped by the installed Fusion runtime to 32 Hz. Host measured 16.66 FPS and guest 53.81 FPS, but guest world age remained 694.412 ms median and 1694.557 ms maximum. This fails replication freshness. Inspection of the installed socket implementation shows streaming uses a separate channel from GameReliableChannelSendCount, explaining why the earlier capacity experiment did not address streaming throughput. An opt-in native Networked-state probe is the next diagnostic; it does not replace gameplay replication and its FPS cannot be used as a matched production benchmark.

Nearby invite reached a shared real lobby over UDP without a typed code and both peers entered the Play round. Full-round completion is pending. This run also exposed a missing dynamically located particle shader in the player; the next candidate falls back to the existing resource-backed glow material so the effect remains visible.

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

### Transport experiment, heavier impacts, and ten-client failure

Exact tested scripts: `1209a703056c44403c822a6619d3d9f48922202e`. Build/810 pose checks passed. Increasing runtime reliable send capacity from 1 to 8 did not improve freshness: 52-bot tsunami host 16.39 FPS, guest 54.43 FPS; guest world receipt age p50 625.305 ms, p95 1221.210 ms, max 1346.024 ms. Only 19 of 411 guest frames showed moving blocks. This experiment is rejected; the next source checkpoint removes the override. Native Fusion RTT is added to subsequent diagnostic traffic reports to distinguish transport delay from application acknowledgment/queue delay.

Planet Killer at diameter 90 passed the unchanged workload gate with 30 blocks displaced over a metre: 18.39 typical FPS, p95 212.794 ms, p99 297.384 ms, max 457.949 ms. Combined tsunami/tornado/Planet Killer now overlaps actual local wave targets, tornado activity and planet land contact; 54 moving-block frames and 30 displaced blocks, 14.44 FPS, p95 432.067 ms, max 478.504 ms. Neither has a matched old-script baseline yet. A separate raw Planet Killer profile is retained for attribution, excluded from FPS results.

Ten separate Fusion processes (two rendered, eight headless; functional test only) failed before entering Game. Eight guests joined the host; the ninth guest could not obtain spawn clearance and disconnected. Host reported `A player or moving object occupies this lobby slot; waiting. humans=9 avatars=8`. No ten-client migration pass is claimed. Next checkpoint exposes the blocking collider only through an internal diagnostic property and records it on fixture failure. Headless portrait rendering also produced RenderTexture errors; the next checkpoint skips the portrait camera only when there is no graphics device.

The installed Fusion runtime and player Fusion.Runtime.dll hashes match. Metadata inspection confirms that the existing static unreliable-RPC helper's woven CheckInvokeRpc call targets a non-public runtime method. The earlier native MethodAccessException remains unresolved; this path stays disabled, with no vendor/package patch or permission bypass.

Additional requested investigation now includes Jobs/Burst for suitable snapshot math, shared incremental navigation, bounded tactical/search scheduling, unnecessary collision/query work, and whole-frame reconciliation. Existing shared geometry, traversal caching and planning budgets are retained; reducing bot capability or spectacle is not an accepted optimization. The next profile reader exports every exclusive marker, all CPU threads and per-thread root/exclusive residuals. Parallel thread time must not be added to main-thread time; waits are not assumed removable computation or proof of GPU cost. GPU execution timing, allocations/GC, render preparation, complete physics/worker synchronization, networking scaling and dynamic-map traversal remain explicit open measurements.

### Whole-frame reconciliation and next candidate

The 128-frame Planet Killer raw trace from tested scripts `1209a703056c44403c822a6619d3d9f48922202e` reconciles all main-thread roots to exclusive samples with zero residual (rounding aside): mean 62.933 ms, median 52.035 ms. Exclusive main-thread waits average 2.372 ms; profiler/probe work 1.941 ms; GC collection 0.311 ms. The CPU-only recording does not establish GPU execution time. Death deformation contributes 2.691 ms averaged over all frames but runs on 13/128 frames, about 26.5 ms per active frame. Bot destination selection averages 2.443 ms, active on 47/128 frames. These overlapping-inclusive and separate-thread values must not be added to this exclusive budget.

`95bcbcfb216f5f3f04291a2eb6f0ffb87e21c60b` built and its 52-bot peer run measured host 16.02 FPS, guest 56.74 FPS; guest world age median 696.766 ms and max 1488.285 ms. Native RTT was roughly 64 ms at baseline start. Thus the long world update interval is not explained by basic connection RTT. These remain exploratory single runs, with no established performance acceptance.

Its actual ten-client repeat identified the blocker: the host's `Persistent lobby survivor` CharacterController stood at grid slot 2, while reservation logic consumed slot 0 and returned the host's arbitrary current position for that slot. The next source candidate reserves the host's actual grid slot when already there, offers every other slot to guests, and requires each returned pose to match its real slot. This preserves the current host position on the grid and retains occupancy/floor checks. The same allocation applies after host migration. Native ten-client retest remains pending.

The next Presentation candidate moves large death-mesh vertex/normal deformation to a Burst parallel job with disjoint batches, persistent native buffers and deterministic bounds reduction. Unity object access, matrix snapshots and mesh upload stay on the main thread. Small meshes retain the original serial path. No bones, vertices, effects, collisions or bot decisions are removed. Existing Burst/Collections dependencies are referenced; packages and Core are unchanged. Build preflight adds 4,617 serial-reference position/normal/bounds comparisons over 769 vertices, four batches and three matrix sets; these checks and runtime performance are pending until built. Schedule, wait and upload timings are separately marked.

A separate opt-in GPU-profile flag enables the supported profiler GPU area and records GPU frame time when available; ordinary FPS runs keep it off. An opt-in diagnostic reliable-data send-rate parameter enables an isolated API-rate experiment without changing production defaults. Neither is an accepted optimization without measurements.
