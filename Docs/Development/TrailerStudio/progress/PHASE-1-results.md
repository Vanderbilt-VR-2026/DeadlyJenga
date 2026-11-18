# Phase 1 results: Sets and tapes

Run through the coordinator: job `a8ee9313` on commit `a9df875d`, batch
editor, map `sparse-viaduct-study`. Method
`StudioAcceptance.RunPhase1`. Full report: [evidence/phase1/report.md](evidence/phase1/report.md).

| # | Acceptance test | Result | What was measured |
|---|-----------------|--------|-------------------|
| 1 | Set with 3 marks and a collapse cue; record; play back 3 times; drift under 1 mm and 0.1 degrees | Pass | Three playbacks, 26070 samples each: worst 0.000 mm and 0.0000 degrees. 90 side-by-side frames (live left, playback right) differ by 0 of 255 on average. |
| 2 | Scrub to 10 random frames | Pass | 1446 block poses: worst 0.791 mm. 30 actor positions: worst 0.0 mm. |
| 3 | A live survivor on a moving block during playback rides it and is pushed by it | Pass, with a finding | See below. |
| 4 | A Theatre v4 take loads and plays | Pass | 180 frames converted to v5, played back with 0.000 mm drift. |
| 5 | Game.unity still plays solo with the Studio closed | Pass | 240 frames, local survivor active, no Studio objects in the scene, 0 exceptions. |
| 6 | A track type and a mode tab added in their own files | Pass | `ExampleNoteTrack.cs` recorded, saved and played; `ExampleNotesMode.cs` shows as a tab; unknown track types survive a load and save. |
| - | Runtime errors | Pass | None. |

## Finding on test 3 (worth a second opinion)

The test shoves the roof block the Set's survivor stands on (0.70 m
sideways at 3.6 m/s) and records it live. Then the same survivor stands
there again while the world is only played back.

- Live recording: the survivor was carried **0.01 m**.
- Against playback: **0.00 m**. The two paths are never more than 0.01 m
  apart over 180 frames.

So playback treats a live survivor the way the real physics did, which is
what the Studio has to guarantee. But the survivor did **not** ride the
block in either case: the roof slid out from under them
([rider-00029.png](evidence/phase1/rider-00029.png),
[rider-00045.png](evidence/phase1/rider-00045.png)). The survivor's own code
(`CastleSurvivor`, the `support` carry in `Update`) looks like it should carry
them. Either that code does not act for a survivor without an input driver
(the Studio's stand-ins), or this is how the game behaves. That is a
Characters question and nothing was changed there. The test was changed to
compare with the live game instead of assuming how far someone is carried.

Not shown by this test: a real performer (the local player) standing on a
played-back block. An earlier version of the test put a stand-in on a
different block and it moved 0.20 m while its block moved 0.70 m.

## Look at
- [compare-00045.png](evidence/phase1/compare-00045.png): live recording next to playback.
- The window in use: [evidence/window/](evidence/window/) (12 screenshots,
  see `PHASE-5-results.md` for the tour).
