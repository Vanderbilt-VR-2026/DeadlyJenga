# Tycoon standalone tsunami frame times

The 24-bot Windows player improved from 21.4 to 26.1 typical FPS while the tsunami was moving blocks, a 22% gain across two runs per revision. The 50% goal for this setup was about 32.2 FPS.

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
