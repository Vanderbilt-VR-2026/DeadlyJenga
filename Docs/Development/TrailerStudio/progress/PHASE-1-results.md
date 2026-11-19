# Phase 1 results: Sets and tapes

Run through the coordinator: job `9b95e35e` on commit `3608bd41`, batch
editor, map `sparse-viaduct-study`. Method
`StudioAcceptance.RunPhase1`. Full report: [evidence/phase1/report.md](evidence/phase1/report.md).

| # | Acceptance test | Result | What was measured |
|---|-----------------|--------|-------------------|
| 1 | Set with 3 marks and a collapse cue; record; play back 3 times; drift under 1 mm and 0.1 degrees | Pass | Three playbacks, 26070 samples each: worst 0.000 mm and 0.0000 degrees. 90 side-by-side frames (live left, playback right) differ by 0 of 255 on average. |
| 2 | Scrub to 10 random frames | Pass | 1446 block poses: worst 0.791 mm. 30 actor positions: worst 0.0 mm. |
| 3 | A live survivor on a moving block during playback rides it and is pushed by it | Pass (with the Characters fix `2496f88d`) | Block #58 moved 0.70 m. The survivor was carried 0.71 m in the live recording and 0.71 m against playback; the two paths are never more than 0.07 m apart. |
| 4 | A Theatre v4 take loads and plays | Pass | 180 frames converted to v5, played back with 0.000 mm drift. |
| 5 | Game.unity still plays solo with the Studio closed | Pass | 240 frames, local survivor active, no Studio objects in the scene, 0 exceptions. |
| 6 | A track type and a mode tab added in their own files | Pass | `ExampleNoteTrack.cs` recorded, saved and played; `ExampleNotesMode.cs` shows as a tab; unknown track types survive a load and save. |
| - | Runtime errors | Pass | None. |

## Test 3: how it came to pass

Until the fix agents, nobody rode anything: the live game did not carry a
standing survivor on a moving block (0.01 m for a 0.70 m slide), because
`CastleSurvivor` remembered the never-moving shared collision body as what
it stood on. Commit `2496f88d` (Characters, one line) makes it remember the
block's own surface copy. The test now requires the survivor to move at
least 60% as far as the block, live and against playback. This changes the
live game too: survivors and bots ride moving blocks on every map, and climb
grip refills only on a block at rest. It is listed in
`CROSS-BOX-CHANGES.md` for Evan to keep or drop; dropping it puts test 3
back to "not met".

## Look at
- [compare-00045.png](evidence/phase1/compare-00045.png): live recording next to playback.
- The window in use: [evidence/window/](evidence/window/) (12 screenshots,
  see `PHASE-5-results.md` for the tour).
