# Phase 3 plan: directing the world

Same branch and worktree. Built by the same chat after Phases 1 and 2.

## Files

| File | Job |
|------|-----|
| `Data/StudioSet.cs` (fields added to `StudioCue`) | blocks, direction, spin, duration, ripple, link, linkOnHit, radius, values, jitter, aimed |
| `Engine/StudioDirector.cs` | Delayed and chained actions, guides, hit detection, slow-time ranges; the push, weak-point, guide, slow-time, gear and fire cues |
| `Engine/StudioCues.cs` (changed) | Disaster cue takes the disaster's own settings, an end time and a heading |
| `Engine/StudioBranch.cs` | Start physics from a tape frame; join earlier frames; notes, scores and thumbnail for world takes |
| `Engine/StudioVariations.cs` | The queue of tries, scoring, activating a result |
| `Engine/StudioEnvironment.cs` | Time of day and weather through the scene's sun and fog |
| `UI/DirectMode.cs` | The Direct tab, block picking and arrow dragging in the viewport, overlays, extra cue fields |
| `Tests/StudioAcceptance3.cs` | The four acceptance tests |

## Data

No tape format change. A branch or a variation try is a normal `world` take;
its `take.meta` holds `StudioBranch.WorldNote` (parent, branch frame, label,
group, scores). World key frames now sit on whole multiples of `keyEvery` in
tape frames (plus the take's first frame), so a take made by joining two
recordings still decodes.

## Changes outside DevTools

- `aa5331a4`: `IDirectedHazard.Heading` (Core), implemented by
  `BasicDisaster` (World) for the tornado. Logged in `CROSS-BOX-CHANGES.md`.
- Not needed: a hazard settings struct (`DemoDisasters.Execute` already takes
  values), and `IEnvironmentControls` (nothing in Presentation to implement
  it; the Studio drives the scene's sun and fog itself).