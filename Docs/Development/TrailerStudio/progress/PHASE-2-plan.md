# Phase 2 plan: performance passes

Same branch and worktree as Phase 1. Everything is in DevTools.

## Files

| File | Job |
|------|-----|
| `Engine/StudioRunPass.cs` (changed) | A pass can now have a performer, a countdown, pre-roll, punch-in, loop, slow motion and live physics |
| `Engine/StudioCast.cs` (changed) | Puts the local player (survivor rig or flying hand) on a mark; hand bot; stepping in and out of a body |
| `Engine/StudioPerform.cs` | Builds the pass from the Perform tab's choices; record, retake, "back 3 s and punch in" |
| `Engine/StudioTakes.cs` | Punch-in join, comps, clones, stale takes; per-track notes (ghost look, pass-through, stale frame) |
| `Engine/StudioPaths.cs` | Path lines on the floor in the game world |
| `Engine/StudioHud.cs`, `Engine/StudioWristPanel.cs` | What the performer sees: desktop prompts and keys, VR wrist panel and floating countdown |
| `Engine/StudioDrivers.cs` + `DevTools/Scripts/TrailerStudio/*` | The game-loop hooks (runtime components, editor only) |
| `Tracks/ActorFrames.cs` | Opens a recorded actor frame so it can be blended, moved, mirrored, re-dressed |
| `Tracks/StudioGhostLook.cs` | Character / see-through ghost / hidden, and pass-through |
| `UI/PerformMode.cs` | The Perform tab |
| `Tests/StudioAcceptance2.cs` | Batch run of the Phase 2 acceptance tests |

## How a performance pass runs

1. The Perform tab (or F9, or the wrist panel) builds a `StudioPass` with a
   `performer` mark id and calls `StudioRun.Record`.
2. `StudioCast.Perform` switches the session to the right rig with the
   session's own `SetRole`, places it on the mark with `Place`, and applies
   the mark's color and outfit. The rig becomes the "live" object for that
   mark, so the normal actor recorder records it.
3. Everything else follows its track mode. The world plays back through
   physics so the performer can stand on it (see Phase 1, decision 7).
4. Countdown: time stands still for three seconds. Pre-roll: the tape plays
   for a moment before recording starts.
5. Punch-in: the performer rides along the old take (placed on its pose every
   frame, input off) until the punch frame, then gets control. The new take is
   the old frames, a 0.2 s blend, then the new frames. Old takes are untouched.
6. Stop keeps the take. With loop on, the pass starts again with a new take.
7. The performer stays in the body between takes (needed in VR) until
   "Step out of the body" or a normal Play.

## Data

No tape format change. Per-track notes are JSON in the existing `track.meta`
string. Actor takes remember which world take they were performed against in
the existing `take.meta` string. `StudioSet` gains one field, `handBot`.

## Changes outside DevTools

- `2fe6abf1`: `INetworkActorSnap.SnapToState()` (Core interface, two
  Characters implementations). Logged in `CROSS-BOX-CHANGES.md`.
- The spec expected more (`IPuppetable`, `IRecordableHand`, a spawn-as-actor
  hook). They were not needed: the multiplayer copy is already a solid
  puppet, hands already pack and unpack themselves, and
  `ISessionInfo.SetRole` + `IPlayableCharacter.Place` already put the local
  player anywhere as either rig.

## Order of work

1. Engine: performer, countdown, pre-roll, punch-in, loop, stale marking.
2. Take tools: join, comp, clone.
3. Ghost looks, path lines, prompts, wrist panel, Perform tab.
4. Acceptance tests with scripted input standing in for a person.
5. Results, review material.
