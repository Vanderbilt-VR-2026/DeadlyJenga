# Trailer Studio: interface

The interface should feel like a calm, modern creative app (think DaVinci
Resolve's page tabs, a music DAW's tracks and Dreams' friendliness), not a
debug panel.

Build it with **UI Toolkit** (UXML + USS) as an editor window, not IMGUI. Use
one stylesheet with named color and spacing tokens so the whole tool can be
re-themed in one place. Support Unity's light and dark editor skins.

## Layout

```
┌──────────────────────────────────────────────────────────────────────┐
│  Set: "Viaduct Ambush" ▾     [ Set ] [ Perform ] [ Direct ] [ Shoot ] [ Cut ]   ● REC   ⟲ ⟳ │
├───────────────┬──────────────────────────────────────┬───────────────┤
│  Cast & World │            Viewport                  │   Inspector   │
│  (left list)  │  (live Scene/Game view with          │  (only the    │
│               │   overlays: marks, ghosts, paths,    │   selected    │
│  ▸ World      │   block picks, camera frustums)      │   thing)      │
│  ▸ Hand       │                                      │               │
│  ▸ Survivors  │                                      │               │
│  ▸ Cameras    │                                      │               │
├───────────────┴──────────────────────────────────────┴───────────────┤
│  Transport:  |◀  ◀◀  ▶  ▶▶  ▶|   00:04.12 / 00:12.00   speed 1×  loop │
│  Timeline (tracks, like a DAW)                                       │
│  World   ████████▓▓▓▓▓▓▓▓░░░░  (branch marker ◆ at 4.0s)             │
│  Hand    ░░░░████████░░░░░░░░  take 3 of 5 ▾                         │
│  Mia     ██████████████░░░░░░  take 2 of 2 ▾   M S 🔒                │
│  Cues    ◆ tornado   ◆ push block 214                                │
│  Cam A   ──────▬▬▬▬▬▬────────                                        │
└──────────────────────────────────────────────────────────────────────┘
```

- **Top bar:** Set picker, mode tabs, one big record button, undo/redo.
- **Left list:** everything in the Set, grouped. Each row has mute (M),
  solo (S), lock, color dot and a take dropdown.
- **Viewport:** the scene itself with overlays. Click things in it to select
  them. Drag marks, block arrows and camera keys in place.
- **Inspector:** settings for the selection only. Common settings shown, an
  "Advanced" fold for the rest.
- **Timeline:** one shared time ruler across all tracks. Scrub anywhere and
  the viewport shows that exact moment. Takes show as colored bars; cues as
  diamonds; branches as markers on the World track.

## Modes (tabs)

Each mode shows only what it needs. The timeline stays in every mode.

1. **Set**: pick map, time of day, weather, place marks, choose outfits and
   roles, set length. Big friendly "Add survivor", "Add hand", "Add bots".
2. **Perform**: choose who to play, which tracks play back as ghosts, then
   press record. Shows countdown, pre-roll, take list, punch-in point.
3. **Direct**: world events. Disasters, aimed pushes, hand pulls, physics
   branches, variation runs, slow-down zones. Click a block to aim it.
4. **Shoot**: cameras over the finished tape. Lenses, moves, VR handheld
   camera, previews per camera.
5. **Cut**: order shots, trim, time remap, render queue, export.

## Design rules

- **One obvious next step.** Empty states say what to do ("Place a mark to
  add your first actor"). The record button tells you what it will record
  ("Record Mia, take 3").
- **Show, don't list.** Paths drawn in the viewport, take thumbnails, a
  filmstrip under each shot, a waveform on the audio track.
- **Progressive disclosure.** Defaults that just work; "Advanced" folds for
  power settings; a command palette (Ctrl+K) to reach anything by name.
- **Keyboard-first for repeat work.** Space play/pause, R record, J/K/L
  shuttle, [ and ] set in/out, Ctrl+Z undo, Tab cycle modes, number keys
  pick takes. A shortcut sheet is one key away (?).
- **Never block.** Long work (variation runs, renders) runs in a queue with
  progress chips in the top bar. The window stays usable.
- **Safe by default.** Deleting a take moves it to a trash list that empties
  only on request. Overwrites ask first, or make a new take instead.
- **Honest status.** If playback drifted, a cue was rejected by gameplay, or a
  branch invalidated later takes, a clear badge on that track says so, with
  a one-click fix (re-record, re-branch, accept).
- **Readable at a glance.** Consistent colors per actor across the list,
  timeline, ghosts and paths. Clear type sizes; no walls of fields.

## In-headset panel (VR performing)

Shown only while performing in VR. Small, on the off-hand wrist, readable
without squinting:

- Big record / stop, retake, and "back 3 s and punch in".
- Ghosts on/off, slow-motion performing (½×, ¼×), time left.
- A floating countdown before recording, and a soft line on the floor
  showing your previous take's path.
- Voice commands are a bonus ("record", "cut", "again").

This panel is DevTools UI (allowed to draw). It must not ship in player
builds.
