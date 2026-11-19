# Trailer Studio

Open **Tools → Deadly Jenga → Trailer Studio**. It stages, performs and films
trailer moments using only the game itself. The full spec is in
`Docs/Development/TrailerStudio/`.

## The idea in one minute

1. A **Set** is one scene: a map, who is in it (marks) and what the world
   does (cues).
2. **Open stage** loads the Set's map in Play Mode. Save or close edited
   scenes first.
3. **Record** makes a tape. Everything on the tape plays back exactly the
   same every time, and you can scrub to any frame.
4. Perform the actors one at a time. Everyone already recorded plays back
   around you.
5. Direct the world: disasters, aimed pushes, branches, variation runs.
6. Add cameras last, over the finished tape. Render them, cut them, export.

Nothing you record is thrown away. Every recording is a new **take**; pick
the active take from the list, with number keys, or in the inspector. Deleted
takes go to a trash list that only empties when you say so.

## The tabs

| Tab | What you do there |
|-----|-------------------|
| **Set** | Pick the map, length and frame rate. Add survivors, a hand, bots. Drag marks in the viewport. Add simple cues. |
| **Perform** | Click an actor, press record, play them in the Game view (or the headset). Countdown, pre-roll, punch-in, slow motion, loop record, live physics. Ghost looks for the others. Comps and clones. |
| **Polish** | Fix a take without performing again: retime, slide, smooth, offset, bend the path, lock feet. Add layers on top: look-at, reach, pose, reactions, emotes, outfit, weapon, hit pause. Make a crowd. |
| **Direct** | Pick blocks and push them (drag the arrow), ripples, weak points, guides, any disaster with its settings, slow time. Branch the physics at the playhead. Run variations and pick the best. |
| **Shoot** | Cameras. Look around in the viewport and press "Key here"; move the playhead and do it again. Behaviors (follow, ride, two-shot, orbit, dolly zoom, first person), handheld shake, lens, speed ramps, freeze frames. Render. |
| **Cut** | Put shots in order, trim, dissolve, snap to the music's beat. Export FCPXML and EDL for Resolve or Premiere. |

## Keys (in the Studio window)

Space play or pause · R record or stop · J / L back or forward a second ·
Left / Right one frame · Home / End · [ and ] set the range, \ clears it ·
Tab next tab · 1 to 9 pick a take · Delete removes the selected mark, cue,
camera or layer · Ctrl+Z / Ctrl+Y undo and redo · Ctrl+K command palette ·
? this list.

Viewport: drag to orbit, right-drag to slide, wheel to zoom. Drag a mark to
move it along the surface; Shift+drag turns it; Alt+drag raises a hand mark.

While performing (in the Game view): F9 record or stop · F10 again ·
F8 back 3 s and punch in · F7 slow motion.

## Where things are saved

- Sets: `Assets/_DeadlyJenga/DevTools/Editor/TrailerStudio/Sets/<id>.set.json`
  (small; fine to commit).
- Tapes: `Recordings/TrailerStudio/<set id>/main.djtape` (large; not
  committed). Renders, thumbnails and the tape's sound are in the same
  folder.

## Coming from Gameplay Theatre

The Set picker (top left) has "Import a Gameplay Theatre recipe" and "Import a
Gameplay Theatre take folder". A recipe becomes a Set: cast become marks, cues
become cues, shots become cameras (Shoot tab, "Turn Theatre shots into
cameras"). A take folder also converts its motion tape, so it plays in the
Studio. The Theatre window still works.

## Good to know

- The viewport is the Studio's own director camera, not the Scene view. Marks
  can only be dragged with the stage open, because the maps are built when
  the game starts.
- Cues only run when the world is recorded live ("Record the world", a
  branch, a variation run, or a live-physics performance).
- A played-back disaster looks and sounds right but does not push or hurt a
  live performer.
- Reactions are built from head, hands and feet. There is no ragdoll.
- MP4 and ProRes need FFmpeg on the machine. Without it you get the image
  sequence.
- For tests from the command line:
  `-executeMethod DeadlyJenga.DevTools.Editor.TrailerStudio.StudioAcceptance.RunPhase1`
  (to `RunPhase5`). Reports go to `Logs/Validation/TrailerStudio-Phase<n>/`.
