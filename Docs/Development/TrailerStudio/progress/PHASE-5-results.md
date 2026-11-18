# Phase 5 results: cameras and finishing

Run through the coordinator: job `3acc1d19` on commit `a9df875d`, batch
editor, map `sparse-viaduct-study`. Method
`StudioAcceptance.RunPhase5`. Full report: [evidence/phase5/report.md](evidence/phase5/report.md).

| # | Acceptance test | Result | What was measured |
|---|-----------------|--------|-------------------|
| 1 | 5-camera shoot renders all cameras; replaying gives identical frames | Pass | 5 cameras, 60 frames each. 300 pictures compared byte for byte between two renders: 0 differ. Ocean, sky and birds were held still for this check. |
| 2 | Speed ramp to 0.25x and back is smooth | Pass | Largest change of speed between two frames 0.037x (a step would be 0.75x). In-between frames are real positions (0.00 mm from the halfway point). |
| 3 | A VR handheld camera take records and plays back as an editable track | Partly | A camera pose fed in every frame became 19 keys that play back within 1.0 cm of the performed path and can be edited. **Not run with a real VR controller, the in-headset viewfinder or the desktop fly camera.** |
| 4 | FCPXML opens in DaVinci Resolve with shots and audio in place | Partly | The file is well-formed, has 3 shots, 1 dissolve and sound under all shots, and every file it points at exists. **Not opened in Resolve or Premiere.** |
| - | Runtime errors | Pass | None. |

## Window tour (the Studio window on screen)

Interactive editor, job `20f26f19` on commit `957f70f4`,
`StudioAcceptance.RunWindowTour`. Report: [evidence/window/report.md](evidence/window/report.md).
A script opens the window, uses every tab and saves 12 screenshots read back
from the display. 0 failures: all tabs draw, exactly one audio listener is
active on the stage, undo and redo step one Set edit at a time. No person
has used the window by hand yet.

## Found and fixed while testing
- Follow, ride, orbit and two-shot cameras lost an actor who fell (aim was a
  plain 0.25 s delay). Fixed in `f6248cb3`; decision 50.
- The first five pictures of a render depended on what was on screen before
  it. Fixed in `76c85dca`; decision 51.
- The stage had no active audio listener, so the recorded sound was eight
  silent files and test 4 still passed. Fixed in `957f70f4`; the test now
  fails on silence (`a9df875d`).

## Open defect: impact sounds are missing from the recorded sound

After the listener fix the mix has signal (loudest sample 1727 of 32767),
but it is only the Music stem. The World, Character, Ambience and other
stems are still all zeros, although the tape has a collapse at 1.0 s and the
sound-events track replays block hits (`Tracks/AudioEventTrack.cs`). Not
investigated further. It may be the batch editor, the replay, or the stem
routing in Theatre's audio capture, which the Studio reuses.

## Not covered
- MP4 and ProRes output (needs `ffmpeg`); only PNG sequences were rendered.
- EXR output and the cinema quality preset were not rendered in a test.
- Anything in a headset.

## Look at
- One frame from each camera at 2.0 s, one second after the collapse:
  [Wide](evidence/phase5/render-1-Wide-00030.png),
  [Follow Ada](evidence/phase5/render-1-Follow-Ada-00030.png),
  [Ride Ben](evidence/phase5/render-1-Ride-Ben-00030.png),
  [Orbit](evidence/phase5/render-1-Orbit-00030.png),
  [Two shot](evidence/phase5/render-1-Two-shot-00030.png).
- [Acceptance-cut.fcpxml](evidence/phase5/Acceptance-cut.fcpxml) and
  [Acceptance-cut.edl](evidence/phase5/Acceptance-cut.edl).
- The window: [08-shoot-cameras.png](evidence/window/08-shoot-cameras.png),
  [10-cut.png](evidence/window/10-cut.png),
  [06-polish-layer.png](evidence/window/06-polish-layer.png).
