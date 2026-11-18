# Extension points

How Phases 2 to 5 add to Trailer Studio **without editing Phase 1 files**.
Every extension is a class in a new file under
`Assets/_DeadlyJenga/DevTools/Editor/TrailerStudio/`. The Studio finds classes
by the interface they implement (`StudioRegistry`, which uses Unity's
`TypeCache`), so there is no list to add yourself to.

Rules for every extension class:

- `public sealed class`, with a constructor that takes no arguments.
- One instance is made per script reload and reused. Keep per-run state in the
  recorder/player objects you return, not in the type class.
- Add a `.meta` file for each new file (see any existing `.cs.meta`).

Working examples, each in one file: `Tests/ExampleNoteTrack.cs` (a track
type) and `Tests/ExampleNotesMode.cs` (a mode tab). They only show while
"Show example extensions" is on (Ctrl+K in the Studio).

## 1. Tape format v5 (`*.djtape`)

Code: `Data/StudioTape.cs`. Tapes live in
`Recordings/TrailerStudio/<set id>/main.djtape`.

```
int32   magic 0x354A5444 ("DTJ5")
gzip {
  string  header JSON (TapeHeader: setId, map, fps, frames, stage, blocks, created, notes)
  int32   track count
  per track:
    string  type id        e.g. "world", "survivor", "camera"
    string  track id       e.g. "world", a mark id, "cam-a"
    int32   chunk length in bytes
    bytes   chunk
}
```

Strings are .NET `BinaryWriter` strings (length-prefixed UTF-8).

**The chunk has the same layout for every track type**, so the Studio can
manage takes for types it has never heard of:

```
string name, bool locked, bool solo, int32 mode (0 playback, 1 live, 2 muted),
int32 active take index (-1 = none), string meta JSON, string badge,
int32 take count
per take:
  string id, string name, int64 created (UTC ticks), int32 start frame,
  int32 keyEvery, bool trashed, bool starred, string meta JSON,
  int32 frame count
  per frame: int32 length, bytes
```

Only the bytes of one frame belong to the track type. Frame `i` of a take is
tape frame `start + i`.

What this guarantees:

- A reader that does not know a type id skips the chunk by its length
  (`StudioTape.Load(file, only)` does exactly that).
- A full load keeps unknown tracks in memory and writes them back unchanged,
  so an older Studio never destroys a newer phase's data. The timeline shows
  them greyed with their type id.
- New track types need **no change** to `StudioTape.cs`.

If your type stores deltas, set `keyEvery` (frames `0, keyEvery, 2*keyEvery…`
of a take must stand alone) and decode forward from the nearest key frame.
`WorldTrack.cs` is the example. If every frame stands alone, leave it at 1.

Never change the meaning of an existing type id's bytes. If a format must
change, use a new type id (for example `world2`) and keep reading the old one.

### Built-in type ids

| Type id | Track id | Frame bytes |
|---------|----------|-------------|
| `world` | `world` | bool key; if not key, one bit per block "changed"; then per changed block: flags (1 active, 2 sleeping, 4 destroyed), and if active int32 x, y, z in millimetres + 6-byte smallest-three rotation |
| `survivor` | mark id | bool visible, int32 length + the survivor's own network state (`INetworkActor.WriteState`), float health, string equipment JSON |
| `hand` | mark id | same envelope, holding the hand's network state |
| `hazards` | `hazards` | UTF-8 JSON `{ wave: WaveFrame, disasters: DisasterFrame[] }`, or empty when no hazard is active |
| `effects` | `effects` | string weapons JSON (blaster, projectiles, actions, equipment, epoch, sequence), then particle samples (same bytes as Theatre v4) |
| `audio-events` | `audio-events` | UTF-8 JSON list of sounds (kind, position, velocity, strength, cue), or empty |

Later phases ended up needing no new track types: branches and variation
runs are ordinary `world` takes with notes in `take.meta`, cameras live in
the Set, and motion edits are ordinary actor takes. The id `camera` stays
reserved in case camera moves are ever recorded per frame.

Notes kept in the free `meta` strings (all JSON, all optional):

| Where | Class | Holds |
|-------|-------|-------|
| `track.meta` of an actor track | `StudioTrackNotes` | ghost look, walk-through, stale frame |
| `take.meta` of an actor take | (in `StudioTakes`) | id of the world take it was performed against |
| `take.meta` of a world take | `StudioBranch.WorldNote` | block count, parent take, branch frame, label, variation group, scores |

## 2. Track types: `IStudioTrackType`

Code: `Tracks/StudioTrackTypes.cs`.

```csharp
public sealed class CameraTrack : IStudioTrackType
{
    public string TypeId => "camera";
    public string Label => "Cameras";
    public Color Color => StudioColors.Camera;
    public int Order => 60;                       // lower runs first each frame
    public void EnsureTracks(StudioSet set, StudioTape tape) { /* tape.GetOrAdd(id, TypeId, name) per track you want */ }
    public StudioRecorder CreateRecorder(StudioStage stage, StudioTrack track) => ...;   // null = nothing live to record
    public StudioPlayer CreatePlayer(StudioStage stage, StudioTrack track, StudioTake take) => ...;
}
```

- `StudioRecorder.Capture(frame, key)` returns the bytes for one frame. It is
  called once per tape frame, after every script's `LateUpdate`.
- `StudioPlayer.Show(frame, running)` shows one exact frame. It is called
  before other scripts' `Update` while running, and whenever the user scrubs.
  `FixedStep(time)` is for things that must move through physics.
  `AfterShow()` runs after every track has shown the frame.
  `CheckDrift(frame, report)` adds to the drift report.
- Orders in use: world 0, hazards 10, effects 15, hand 20, survivor 30,
  audio 40. Effects run before actors because actors draw weapons from the
  state the effects track puts in the match frame.
- `StudioStage` gives you the session, the world, the blocks, the stage
  anchor, `live` (live characters by mark id) and `playback` (played-back
  actor objects by mark id; follow these with cameras).

## 3. The frame loop: `StudioRun`

Code: `Engine/StudioRun.cs`, `Engine/StudioRunPass.cs`.

- `StudioRun.OpenStage(set)` / `CloseStage()`: enter and leave Play Mode.
- `StudioRun.Record(new StudioPass { record = { trackIds… }, liveWorld,
  fixedClock, from, to, speed })`: record those tracks while every other
  track plays back. Phase 2's performance passes and Phase 3's branches are
  passes.
- `StudioRun.Play(from, to, realTime, speed)`, `Scrub(frame)`, `Stop()`,
  `Refresh()` (after changing mute/solo/active take), `UseTape(tape)`.
- `StudioRun.FrameShown` fires for every finished running frame, after
  animation and before rendering. Renderers and tests hook this.
- `StudioRun.Changed` fires when anything the window shows may have changed.
- With `fixedClock` every rendered frame is exactly one tape frame (repeatable,
  used for whole-set recordings and renders). Without it the game runs in real
  time and tape frames follow the clock (used for performing).
- "Physical playback" (`stage.physicalPlayback`) is on while something live
  shares the scene with a played-back world: blocks then move through physics
  so characters can ride them. With nothing live, playback is exact.

## 4. Cues: `IStudioCueType`

Code: `Engine/StudioCues.cs`. `Action` is saved in the Set; `Run` does it
through the real gameplay path and returns a note for the cue log (throw if
gameplay refuses). The Set mode's "Add cue" menu and the cue inspector list
every registered cue. Add inspector fields for a new cue with an
`IStudioInspector` that handles `StudioCue` selections of your action.

## 5. The window

Code: `UI/StudioExtensions.cs`. Four interfaces:

| Interface | Adds | Notes |
|-----------|------|-------|
| `IStudioMode` | a tab in the top bar | `Order`: Set 10, Perform 20, Direct 30, Shoot 40, Cut 50. `Build` fills the panel above the inspector. `TryGetRecord` lets the mode decide what the record button says and does ("Record Mia, take 3"). |
| `IStudioInspector` | the settings shown for a selection | First inspector whose `Handles(selection, context)` is true wins. Selections today: `StudioMark`, `StudioCue`, `StudioTrack`, or null (the Set). Select your own objects with `context.Select(thing)`. |
| `IStudioOverlay` | drawing over the viewport | `painter.Line/Dot/Path` take world points. `PathOverlay` in `UI/SetMode.cs` is the example. |
| `IStudioCommand` | an entry in the command palette (Ctrl+K) | `Name`, `Available(context)`, `Run(context)`. |

`StudioContext` is what extensions get: `Set`, `Tape`, `Stage`, `Frame`,
`Selection`, `Select`, `Edit(label, change, structural)` (changes the Set with
undo, saves it, redraws), `Say(message)` (status line) and `Rebuild()`.

Shared looks: build panels with `StudioUi` (headings, hints, cards, big and
small buttons, the Advanced fold) and `StudioFields` (text, number, slider,
choice, enum, switch: all undoable). Colors and spacing are tokens at the top
of `UI/TrailerStudio.uss`; do not hard-code colors in C#.

## 6. Changing a recorded pose on its way to the screen: `IActorFrameFilter`

Code: `Tracks/ActorTrack.cs` (interface), `Engine/StudioOverrides.cs` (the
one built-in filter). A filter gets the opened-up actor frame
(`ActorFrame`: feet, head, hands, outfit, equipment) for one actor at one
tape time and may change it. The take is never touched. `Wants` must be a
quick check, so actors with nothing layered on them are not decoded every
frame. Phase 4's look-ats, reaches, poses, reactions, emotes and outfit
changes all go through this.

## 7. Using the viewport your own way: `IStudioViewportTool`

A mode class can also implement `IStudioViewportTool`. `Down` returns true
to take a drag (pick blocks, aim an arrow, bend a path); returning false
leaves the viewport's own orbit and mark dragging in charge.
`DirectMode` and `PolishMode` are the examples.

## 8. Set data

`Data/StudioSet.cs` is JSON through Unity's `JsonUtility`. Adding a field is
safe (old files load with the default). Do not rename or remove fields.
Later phases that need their own Set data should add one clearly named field
or list each (for example `public List<StudioCameraShot> cameras`), in a
commit that touches only that line of `StudioSet.cs`, so two chats merging
never conflict on more than adjacent lines.
