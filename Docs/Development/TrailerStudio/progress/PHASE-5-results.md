# Phase 5 results: cameras and finishing

Run through the coordinator: job `5bdecbe7` on commit `47fa54e6`, batch
editor, map `sparse-viaduct-study`. Method
`StudioAcceptance.RunPhase5`. Full report: [evidence/phase5/report.md](evidence/phase5/report.md).

| # | Acceptance test | Result | What was measured |
|---|-----------------|--------|-------------------|
| 1 | 5-camera shoot renders all cameras; replaying gives identical frames | Pass | 5 cameras, 60 frames each. 300 pictures compared byte for byte between two renders: 0 differ. Ocean, sky and birds were held still for this check. |
| 2 | Speed ramp to 0.25x and back is smooth | Pass | Largest change of speed between two frames 0.037x (a step would be 0.75x). In-between frames are real positions (0.00 mm from the halfway point). |
| 3 | A VR handheld camera take records and plays back as an editable track | Partly | A camera pose fed in every frame became 19 keys that play back within 1.0 cm of the performed path and can be edited. **Not run with a real VR controller, the in-headset viewfinder or the desktop fly camera.** |
| 4 | FCPXML opens in DaVinci Resolve with shots and audio in place | Partly | The file is well-formed, has 3 shots, 1 dissolve and sound under all shots, and every file it points at exists. **Not opened in Resolve or Premiere.** |
| - | A blurred render is as bright as a plain one (added after review) | Pass | Average brightness of the same frame: 120.2 of 255 plain, 120.2 with 2 in-between frames. |
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
  fails on silence and on a silent World stem.
- Review finding A1: recorded actors' legs did not move in renders or while
  scrubbing. Fixed in `8753dd61` and `fdac5f12`; decision 54. Phase 4 has the
  test for it.

## Sound: how it works now (was an open defect)

The first handoff said the recorded sound had music only. The cause, found
with the reviewer's help: the game plays ordinary block impacts straight
from physics collisions (`CandyBlockAudio`) and never announces them, and
played-back blocks do not collide. Replaying a tape can therefore never
contain them (the tape held 0 block hits).

Now a whole fixed-clock recording with nobody performing records the game
mix and its stems while the real physics runs, heard from head height on
the stage spot. Measured on this run: mix loudest sample 14447 of 32767,
World stem (block impacts) 15099. Before the fix the World stem was 0.

New sound is recorded into its own folder and only replaces the old sound
when the recording finished. A note beside it says which world take it was
recorded with, and the Cut tab warns when a different one is active.

Limits, by design for now:
- A take performed by a person in real time is not recorded this way (the
  capture would take the sound away from their speakers), so footsteps and
  weapon sounds of performed actors are not in the cut's sound.
- A branch re-records the world from the branch point; its new impacts are
  not in the sound until the whole Set is recorded again.
- "Record the tape's sound" in the Shoot tab replays the tape instead. It
  hears music, disasters and announced sound events only.

## Not covered
- The EDL writes a dissolve as one event and the FCPXML sound assets carry
  no duration (review round 2, C6 and C7). Unchanged, because no editor was
  available to check what it accepts.
- Cameras following overridden actors and renders holding hit pauses
  (review round 2, B1 and B2) are tested in Phase 4.
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
