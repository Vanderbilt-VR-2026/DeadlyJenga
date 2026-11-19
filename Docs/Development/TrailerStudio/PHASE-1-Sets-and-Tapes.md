# Phase 1: Sets, tapes and the Studio window

Goal: a Set can be built, recorded once and played back exactly the same,
scrubbed anywhere, from the new Studio window.

## Starting point

Gameplay Theatre already has: a JSON recipe (`TheatrePlan`), a fixed capture
clock, a seed, and a motion tape (`TheatreMotionTape`, format v4) that records
avatar poses, block motion, wave, projectiles, weapon events and particles
and replays them for reframing. Read `GameplayTheatre.md` and the Theatre*
files first. Reuse what works.

## Features

### Sets
- A Set asset (JSON or ScriptableObject, saved under a Studio folder found
  by name, not a hard-coded path): map ID, stage anchor, length, frame rate,
  time of day, weather or sky preset if the map supports one, cast list,
  marks, shot notes.
- **Marks**: place, drag and rotate in the viewport. Each mark has an actor
  name, role (survivor, hand, bot), color, outfit (`AvatarAppearance`), and
  optional starting equipment.
- Snap marks to the surface below. Warn if a mark is inside a block or over
  water.
- Starting world state: "as the map loads" or "from tape frame N of another
  Set" (start where a previous moment ended).
- Duplicate a Set, rename, notes field, thumbnail.

### Universal tape (format v5)
- One file per tape with a header and named tracks. Each track has an ID,
  type, active take, takes list and a lock flag.
- Track types: World (every dynamic rigidbody: position, rotation, plus
  active/sleeping/destroyed), Survivor actor (full avatar pose, root,
  equipment state, health, status), Hand actor (pose, grip state, held
  block), Hazard (each hazard's own state, e.g. wave or tornado position and
  strength), Effects (particles, projectiles, weapon events), Audio events
  (which sound played where and when, plus the existing PCM stems),
  Camera (Phase 5).
- Record at the capture frame rate; store floats compactly (quantize
  positions to 1 mm and rotations to smallest-three). Show tape size.
- Read v4 Theatre tapes (convert on load).

### Playback
- Any track can be **Live** (simulated), **Playback** (from the tape) or
  **Muted** (absent).
- World playback moves blocks as **kinematic rigidbodies** with
  `MovePosition`/`MoveRotation`, so live characters can stand on them and be
  pushed. Per-block velocity is set too, so gameplay that reads velocity
  (impact damage, sounds) still works.
- Hazard and effect playback shows the same visuals and plays the same
  sounds as the original.
- Scrub to any frame instantly (keyframes every second plus deltas).
- **Drift check**: after a playback run, compare sampled positions against
  the tape and report any mismatch.

### Studio window (shell)
- UI Toolkit window with the layout in `UI.md`: top bar, mode tabs, left
  list, viewport overlays, inspector and timeline.
- This phase needs the Set mode, the transport, the timeline with track rows
  (mute, solo, lock), scrubbing and a basic Record button that records the
  whole Set live once.
- Undo/redo for Set edits.
- Command palette (Ctrl+K) and shortcut sheet.

### Extension points (so later chats don't collide)
- Tape: a documented format where new track types can be added without
  changing existing readers (each track tagged with a type ID and length, so
  unknown tracks are skipped).
- Studio window: mode tabs, track types, inspectors and viewport overlays
  each register themselves (for example through an interface plus a small
  registry), so Phases 2–5 add new files instead of editing the window.
- Write both down in `progress/EXTENSION-POINTS.md`.

### Migration
- Import a Gameplay Theatre recipe into a Set (cast become marks, shots kept
  for Phase 5, cues become timeline cues).
- Keep the old Theatre window working until the Studio replaces it.

## Expected changes outside DevTools (pre-approved: own commit, log in CROSS-BOX-CHANGES.md)
- A supported way to put a block world into kinematic playback and back
  (today Theatre reaches into blocks directly). Likely a small addition to
  `ISyncedWorld` or a new Core interface, e.g.
  `IWorldPlayback { void SetPlayback(bool on); }`.
- Hazard state read/write for playback, e.g. a Core interface hazards may
  implement: `IRecordableHazard { void Write(BinaryWriter w); void Read(BinaryReader r, float time); }`.
  Only add it to hazards that need it (tsunami already has `IWaveState`).

## Acceptance tests
1. Build a Set on `sparse-viaduct-study` with 3 marks and a collapse cue,
   record it, then play it back 3 times. Drift check reports no block more
   than 1 mm or 0.1° off.
2. Scrub to 10 random frames; each matches the recorded frame.
3. Play back the world while a live survivor walks onto a falling block; the
   survivor rides it and is pushed by it.
4. An existing Theatre take (v4) loads and plays.
5. Game.unity still plays solo with the Studio closed.
6. A test track type and a test mode tab, each added in its own new file,
   show up in the Studio and in a saved tape without editing existing files.

## Review stop
Evan watches: a playback render of test 1 next to the live recording, and
a short screen capture of the Studio window in use.
