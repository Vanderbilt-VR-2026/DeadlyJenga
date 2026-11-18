# Phase 5: Cameras and finishing

Goal: film a finished tape with any number of cameras and export
trailer-grade footage. Evan has said camera quality can come last, so this
phase is scheduled after the scene tools are solid.

## Features

### Camera tracks
- Any number of cameras per tape. Each is a timeline track with keyframes
  (position, rotation, lens, focus, roll), drawn as a path with frustums in
  the viewport.
- Easing per key, splines between keys, constant-speed option (dolly feel).
- Follow and frame behaviors: follow an actor with lag, keep two actors in
  frame, orbit, crane, dolly zoom. Can use Cinemachine 3 (already installed)
  under the hood.
- Handheld noise presets (subtle, walking, running, impact shake) with
  strength curves.
- Camera collision avoidance (Theatre's "avoid blocks").
- Lens: focal length and sensor (not just field of view), aperture, focus
  distance keys and focus-on-target, anamorphic squeeze option.
- First-person camera from any actor's head (VR takes look great here).

### VR handheld camera
- Evan holds a controller as the camera and films the tape playing back,
  like a real camera operator. Recorded as a camera track he can then edit.
- Viewfinder panel in the headset, zoom on the thumbstick, record/stop.
- Desktop equivalent: fly-camera with mouse and keyboard, recorded live.

### Time remap
- Speed ramps per shot: markers on the shot that change playback speed
  (slow motion into an impact, back to real speed after), with eased ramps.
- Freeze frame with camera still moving.
- Slow motion renders sub-frames from the tape (positions interpolated), so
  any speed works after recording.

### Render
- Render queue: pick shots and settings, run in the background or as a batch
  job through the coordinator.
- Cinema quality preset applied only during render: Windows build target,
  PC quality tier, high-res shadows and longer shadow distance, highest LODs,
  anti-aliasing.
- Motion blur and smooth edges by rendering several sub-frames per output
  frame and averaging them.
- Resolution up to 4K; aspect presets 16:9, 9:16 (vertical), 1:1, 2.39:1
  with safe-area overlays.
- Output: draft MP4 for review, 16-bit PNG or EXR sequence, or ProRes for
  grading. Stems as today.
- Per-shot look: LUT, grain, vignette, bloom, chromatic aberration.

### Cut
- Order shots, trim with handles, simple cuts and dissolves, a music bed
  with beat markers to snap cuts to.
- Export an edit list (EDL or FCPXML) plus media so Resolve or Premiere can
  open the cut directly.
- Title cards and logo overlays are optional; the main finishing happens in
  the editing app.

## Expected changes outside DevTools
- None expected. Cinema quality changes are applied by the Studio during
  render and restored afterward; ProjectSettings are not edited.

## Acceptance tests
1. A 5-camera shoot over one tape renders all cameras; replaying produces
   identical frames.
2. A speed ramp to 0.25× and back is smooth (no stepping).
3. A VR handheld camera take records and plays back as an editable track.
4. FCPXML export opens in DaVinci Resolve with shots and audio in place.

## Review stop
Evan cuts a 30-second test trailer from Phase 2–4 scenes.
