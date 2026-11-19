# Phase 5 plan: cameras and finishing

Built after Phase 3, before Phase 4, as Evan asked.

## Files

| File | Job |
|------|-----|
| `Data/StudioCameraData.cs` | Camera tracks, keys, speed marks, cuts, render settings, aspect presets (all saved in the Set) |
| `Engine/StudioCameras.cs` | Camera pose and lens at a tape time: keys, spline, easing, even pace, behaviors, handheld noise, block avoidance; speed ramps and freeze frames; actor poses read from the tape |
| `Engine/StudioRender.cs` | The render queue, sub-frame motion blur, cinema quality, per-shot look, FFmpeg movies, the sound pass |
| `Engine/StudioHandheld.cs` | Handheld takes: desktop fly camera, VR controller, or a supplied pose |
| `Engine/StudioExport.cs` | FCPXML and EDL, beat snapping |
| `Tracks/*` (changed) | `ShowBetween`: in-between tape frames for the world and for actors; `StudioRun.ShowExact` |
| `UI/ShootMode.cs`, `UI/CutMode.cs` | The two tabs, camera inspector, camera overlay with frustums and safe areas |
| `Tests/StudioAcceptance5.cs` | The four acceptance tests |

## Data

`StudioSet` gains `cameras`, `cuts`, `render`, `music`, `bpm`, `beatOffset`.
Old Set files load with empty lists. Renders go to
`Recordings/TrailerStudio/<set>/renders/<time>/<camera>/`, with a `shot.json`
per camera that the cut export reads.

## Changes outside DevTools

None.