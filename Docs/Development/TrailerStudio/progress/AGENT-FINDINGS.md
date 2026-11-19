# Findings left open by the fix agents

Found while reading the window, timeline, viewport, tabs, cues, environment
and Theatre import (agent `uiread`) and while testing in an emulated XR
session (agent `xrnet`). Each was left because it needs a decision, a
person with a mouse, or a headset. Fixed findings are in the commits on
`claude/ts-uiread` and `claude/ts-xrnet`.

## Needs a decision
1. Edits during a run: Clone, Crowd, Polish edits and take Use, Trash and
   Empty trash are all allowed while a recording runs. Clone and Crowd add
   marks and tracks to the live stage, and the tape is saved mid-recording.
   Disable them, or queue them until the run ends.
2. `Engine/StudioRunPass.cs`: a cue linked to a cue that no longer exists is
   never started. The window now prevents and flags this; the engine could
   also treat it as an own-time cue.
3. `Engine/StudioCues.cs`: a hazard's "Ends after" stops every active hazard
   of that type, including ones started by other cues.
4. Deleting a cue that others wait for turns those into own-time cues (their
   delay becomes their start time). The alternative is deleting them too.
5. Theatre imports keep odd frame rates (12 to 60); new Sets offer only 24,
   25, 30, 50, 60. Snap imports to the nearest offered rate, or keep.
6. The Cut tab stores the music bed as an absolute path in the Set file.

## Layout and small defects
7. Timeline: when the track list scrolls (more than about seven tracks) the
   lanes are 13 px narrower than the ruler, so the end of the tape is cut
   off in the lanes.
8. `Engine/StudioDirector.cs`: "when its first block hits" watches the first
   block even if a weak point removed it; it then never fires.
9. A slider drag interrupted by a panel rebuild can leave an edit unsaved
   until the next edit (not sure).
10. A mark drag or arrow drag that loses the pointer leaves an edit open
    until the next click.
11. Polish: a layer's Mute button also selects the layer. "Remove the crowd"
    leaves overrides and camera references to the removed actors. A layer's
    "From" can push its end past the Set's length.
12. Trashing the active world take from the Set tab does not bring the
    hazard, effect and sound takes with it the way switching does.
13. Handheld camera: if the stage closes during a handheld take, the open
    edit may never be closed (not traced).
14. Changing an actor between Ghost, Character and Hidden during a run does
    not undo the old look until the run ends.
15. Camera overlay: about 40 camera evaluations per camera per repaint in
    Shoot and Cut.
16. Timeline and overlay colours are fixed for Unity's dark skin.

## In the game, outside DevTools (not changed)
17. `Characters/Player/Scripts/CastleSurvivor.cs`, `ConfigureHeadTracking`:
    the tracking-state action is pass-through, so when the rig is enabled
    while the headset already reports "tracked" the view does not follow
    the headset until tracking blips. The Studio's park-then-perform does
    exactly that. Suggested fix: make it a Value action. Needs a Quest.

## Promised in UI.md, not built
- Viewport: camera keys cannot be dragged in place; camera frustums and
  collapse or guide points cannot be selected there.
- Timeline: no branch markers on the World track, no punch-in marker, no
  zoom (a 2-minute Set at 60 fps cannot be clicked frame-exact; arrow keys
  still step one frame). A linked cue is drawn at its delay as if that were
  a time.
- Take thumbnails only on the world and variation cards; no filmstrip under
  shots; no waveform; no per-camera preview pictures in Shoot.
- J/K/L jump one second; they do not shuttle.

## Try these first by hand
1. Scrub, then press "Key here", "+ Push", "Add a layer", "Branch at": does
   the button's time update, and where does the result land?
2. Drag an unselected cue diamond on the timeline. Drag marks in the
   viewport, Shift+drag to turn, Alt+drag a hand mark. Undo and redo.
3. Click a side-panel button, then press Space, R or Delete: does the
   focused button fire as well? Try Delete while typing in a field.
4. Shoot: look through a camera, switch tab, come back, orbit, Key here.
5. Change sliders or press Clone or Crowd while the tape plays or records.
6. A Set with eight or more tracks: scrub near the end and look at the take
   bars.
