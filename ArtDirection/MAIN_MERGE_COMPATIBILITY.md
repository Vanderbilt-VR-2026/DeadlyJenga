# Main merge compatibility — October 15, 2026

Merged `origin/main` at `7e6d1c84c` into `EP_Sprint_2`, after castle snapshot
`afb4f5a34`. Git merged the package manifest and lockfile without conflicts.

## Confirmed product choices

- Keep CastlePhysicsTest as the default and sole enabled build scene for now.
- Preserve the castle's contact/friction physics hands. Main's new load-based
  pulling, cooldowns and last-support lock stay in Main.
- Keep both required packages: XR Hands 1.7.3 and Multiplayer Play Mode 2.0.2.
  No XR, input, URP or Unity version upgrade was introduced by this merge.
- Lobby and Main retain their separate Photon flow. The castle's LAN transport
  remains separate; this merge does not connect the castle to the Photon lobby.

## Fresh verification

Unity 6000.3.23f1 imported and compiled the merged project successfully. Lobby,
Main and CastlePhysicsTest opened without missing scripts. Main's scene,
generator, block prefab, pulling rules and developer tools match origin/main.
The castle contains none of Main's generator, developer scaling or pulling-rule
components. Its saved scene remained unchanged by the regression run.

The existing Unity checks were rerun on the merged project:

| Report | Passing | Failing |
| --- | ---: | ---: |
| Assets/CastlePrototype/ReferenceValidation.txt | 16 | 0 |
| Assets/CastlePrototype/TraversalValidation.txt | 22 | 0 |
| ArtDirection/StaminaChecks.txt | 55 | 0 |
| ArtDirection/ClimbChecks.txt | 23 | 0 |
| ArtDirection/VRViewChecks.txt | 25 | 0 |
| ArtDirection/SeamCameraChecks.txt | 15 | 0 |
| ArtDirection/SlopeChecks.txt | 10 | 0 |

All 15 real loopback LAN transport checks passed. Unity's LAN protocol checks
also passed, including late-join snapshots, tracked fingers, authority changes,
24 actors, and idempotent Android manifest permission merging.

The first batch attempt exposed an audio voice whose cue had been released
before the audio thread stopped reporting playback. The mixer now stops/skips
released voices before reading cue volume. The subsequent regression log had
no NullReferenceException. Stamina tests now avoid screenshot-only end-of-frame
waits in batch mode; interactive screenshot capture remains available.

## Repeating the checks

Run the editor with `-batchmode -projectPath <project>` and
`-executeMethod DeadlyJenga.CastlePrototype.Editor.CastleMergeCompatibility.RunBatch`.
Do not pass `-quit`: the runner exits after its three Play Mode phases. Use a
graphics-capable editor for the existing camera-render check. Subsequent runs
write the combined report to ArtDirection/MergeCompatibility.txt. Individual
reports are regenerated, and the runner rejects stale reports or FAIL entries.
Run `Tests/LocalMultiplayer/Run-TransportChecks.ps1` separately for socket tests.

## Limits

Focused keyboard/mouse input cannot be certified by batch mode: Unity does not
provide its normal focused/locked Game-view cursor there. The initial attempted
desktop input check failed for that reason; its output was restored, and those
checks are explicitly excluded from the batch runner. Rerun Validate Input
Bindings interactively in the focused Game view before release.

No physical headset was connected; OpenXR reported the expected unavailable-HMD
errors and the synthetic VR regressions exercised head/controller input instead.
No two-Quest LAN session, Photon multiplayer session, Quest build/performance
benchmark or real hand-tracking certification was performed. These checks do
not turn separate local/LAN and Photon implementations into one game flow.

Concept artwork, trailers, nested project/cache files and archived exports remain
outside this merge. No push or pull request is part of these local merge checks.
