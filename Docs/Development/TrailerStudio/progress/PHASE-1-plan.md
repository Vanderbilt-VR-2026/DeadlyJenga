# Phase 1 plan (builder chat A)

Branch `claude/trailer-studio-a`, based on `Evan_Sprint_4` at `73339768`.
Worktree: `Documents/Codex/Projects/deadlyjenga-claude-trailer-studio`.

## Where the code goes

Everything is in DevTools, in the existing editor assembly
(`DeadlyJenga.DevTools.Editor`), under
`Assets/_DeadlyJenga/DevTools/Editor/TrailerStudio/`:

| Folder | Files | Job |
|--------|-------|-----|
| `Data/` | `StudioSet.cs`, `StudioTape.cs`, `TapePack.cs`, `StudioLibrary.cs` | The Set (JSON), the tape (binary v5), number packing, where files live |
| `Tracks/` | `StudioTrackTypes.cs`, `WorldTrack.cs`, `ActorTrack.cs`, `HazardTrack.cs`, `EffectsTrack.cs`, `AudioEventTrack.cs` | One file per track type: how it records a frame and shows a frame |
| `Engine/` | `StudioStage.cs`, `StudioRun.cs`, `StudioDriver.cs`, `StudioCues.cs`, `StudioDrift.cs`, `TheatreImport.cs` | Play Mode stage, the frame loop, cues, drift check, Theatre import |
| `UI/` | `TrailerStudioWindow.cs`, `TrailerStudio.uxml`, `TrailerStudio.uss`, `StudioModes.cs`, `SetMode.cs`, `StudioTimeline.cs`, `StudioViewport.cs`, `StudioPalette.cs` | The window |
| `Tests/` | `StudioAcceptance.cs`, `ExampleNoteTrack.cs`, `ExampleNotesMode.cs` | Batch acceptance run and the two "added in its own file" examples |
| `Sets/` | `TrailerStudioSets.md`, saved `*.set.json` | Saved Sets (found by the marker file's name) |

Tapes are large binary files, so they are saved outside `Assets`, in
`Recordings/TrailerStudio/<set id>/` (already git-ignored).

## Data formats

**Set** (`<id>.set.json`, Unity `JsonUtility`): id, title, notes, map ID,
stage anchor (auto or map anchor + offset), length, fps, seed, warm-up,
time of day, weather, starting world state (map load, or frame N of another
Set's tape), marks, cues, shots (kept for Phase 5), shot notes, thumbnail.
Marks are stored relative to the stage anchor, like Theatre's `start`.

**Tape v5** (`*.djtape`): magic `DTJ5`, then a gzip body:
header JSON (set id, map, fps, frame count, stage, block count), then
tracks. Every track is written as `type id` + `track id` + `byte length` +
chunk, so a reader can skip a type it does not know. The chunk layout is the
same for every type (name, lock, mode, active take, takes; a take is a list
of frame blobs), so unknown tracks are also kept when the tape is re-saved.
Only the bytes inside a frame blob belong to the track type.

- World: key frame every second, deltas between. Per block: flags
  (active, sleeping, destroyed), position in whole millimetres, rotation as
  smallest-three (15 bits each).
- Survivor and Hand actors: the character's own network state
  (`INetworkActor.WriteState`) plus health and equipment.
- Hazards: `DisasterStates.Active` frames and the wave frame.
- Effects: equipment/projectile/weapon events and particle samples
  (same data as Theatre v4).
- Audio events: block hits, match sounds, block breaks, with position.

## Playback

- World: `ISyncedWorld.SetReplica(true)` makes the map kinematic (the same
  switch multiplayer guests use). Blocks are moved with
  `MovePosition`/`MoveRotation` in `FixedUpdate` and set exactly once per
  rendered frame, so a paused scrub and a running playback show the same pose.
- Actors: a remote copy made by `NetworkActors.CreateView`, fed the recorded
  state. It already has a solid capsule, weapons and emotes.
- Hazards: recorded frames are published through `DisasterStates` and
  `Tracked.Waves`, the same way a multiplayer guest sees a disaster.

## Order of work

1. Tape + Set data, track registry, track types (compile check).
2. Stage and frame loop in Play Mode: record whole Set, play back, scrub,
   drift check. Batch acceptance method.
3. Studio window (UI Toolkit): Set mode, viewport, cast list, timeline,
   transport, record, undo, command palette, shortcut sheet.
4. Theatre recipe and v4 tape import.
5. Extension examples, `EXTENSION-POINTS.md`, acceptance run, review material.

## Changes outside DevTools

None planned. The spec expected two Core additions (`IWorldPlayback`,
`IRecordableHazard`). Existing Core contracts already cover both
(`ISyncedWorld.SetReplica`, `IDisasterState`/`IWaveState`), so Phase 1 uses
those. If a test shows they are not enough, the change goes in its own
`[TrailerStudio cross-box]` commit and into `CROSS-BOX-CHANGES.md`.

One repository file outside the boxes: none (tapes use the existing
ignored `Recordings/` folder).
