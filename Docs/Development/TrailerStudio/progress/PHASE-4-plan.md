# Phase 4 plan: polishing performances

Built last, as Evan asked.

## Files

| File | Job |
|------|-----|
| `Data/StudioOverrideData.cs` | A layer on an actor: kind, range, easing, target, trigger (saved in the Set) |
| `Engine/StudioMotionEdit.cs` | Trim, slide, retime, smooth, offset, bend, blend takes, lock feet: each makes a new take |
| `Engine/StudioOverrides.cs` | The frame filter that applies layers; look-at, reach, pose, nine reactions; cue and contact triggers read from the tape; hit pause |
| `Engine/StudioTakes.cs` (crowd added) | A crowd from a few takes, placed on the ground, never in step |
| `Tracks/ActorTrack.cs` (changed) | `IActorFrameFilter`; recorded weapons drawn by the Studio |
| `UI/PolishMode.cs` | The Polish tab, path bending in the viewport, layer inspector, path overlay |
| `Tests/StudioAcceptance4.cs` | The four acceptance tests |

## Data

`StudioSet` gains `overrides`. No tape format change: edits are ordinary
actor takes.

## Changes outside DevTools

None. `CandyPlayerAvatar.OverridePose` was not needed because layers change
the recorded frame before the multiplayer copy reads it. A ragdoll on the
survivor prefab was not added (see decision 48).