# Phase 3: Directing the world

Goal: the person can make the destruction do exactly what the moment needs,
and find great physics results quickly.

## Features

### Event cues on the timeline
- Every production disaster (every `DemoDisasters` prefab: tornado,
  tsunami and the rest) can be dropped on the timeline with its settings
  shown as friendly controls: direction (drawn as an arrow in the viewport),
  strength, size, speed, start and end.
- Collapse, drop deck and stop-hazards cues from Theatre stay.
- Weapon and explosive cues: fire from an actor or a point at a target or a
  point, through the real equipment path.

### Aimed destruction
- Click a block in the viewport to select it. Drag an arrow to set a push:
  direction, force, spin, time.
- Select many blocks with a box or a brush. Push them as a group or as a
  ripple (each one a little later than the last, by distance).
- **Chain reactions**: link pushes so that block B gets pushed when block A
  hits something, or after a delay.
- **Weak points**: mark blocks to be removed or loosened at a time (as if
  pulled), without the hand.
- **Guides**: an invisible helper shape that nudges falling blocks toward a
  spot (for "debris lands right next to the hero"). Shown only in the
  Studio, never rendered.

### Branching physics
- Pick a frame on the World track and press **Branch**. Everything before it
  stays from the tape; after it, physics runs live with any new or changed
  cues and is recorded as a new world take.
- The branch tree is shown as a simple list with thumbnails; switch between
  branches instantly.

### Variation runs (find the great take)
- Run the same physics range many times with small changes: seed, tiny
  force jitter, cue timing jitter. Runs happen in a queue, offline, at the
  fixed clock (through the Unity coordinator for batch runs).
- Results show as a thumbnail grid with a short preview each, plus simple
  scores the person can sort by: how many blocks fell, how far, how close to
  a target point, whether a chosen block landed in a chosen zone.
- One click makes a result the active world take.

### Time and environment
- Time of day, sky, weather and water level per Set, where the map and
  Presentation support them (read only through Core; if Presentation has no
  hook, add one).
- Hold or slow world time in a range while performing (pairs with slow
  performing in Phase 2).

## Expected changes outside DevTools (pre-approved: own commit, log in CROSS-BOX-CHANGES.md)
- Starting any hazard with explicit settings, not only by prefab ID. Today
  only the tsunami's fields are set directly. Smallest addition likely
  `IHazard`-side settings, e.g. `IConfigurableHazard { void Configure(HazardSettings s); }`
  with a plain settings struct in Core.
- A way for the Studio to set sky, time of day and weather, if Presentation
  exposes nothing today, e.g. `IEnvironmentControls` in Core.

## Acceptance tests
1. A tornado cue with a set heading tears through the tower; playback is
   identical every time.
2. Push block 214 at 1.5 s so it lands within 1 m of a mark, found with a
   variation run of 20 tries.
3. Branch at 3 s, change one push, and the frames before 3 s are unchanged.
4. A ripple push across 30 selected blocks looks like a wave of collapse.

## Review stop
Evan picks the best of a variation run and watches the result with his
Phase 2 actors performing on top.
