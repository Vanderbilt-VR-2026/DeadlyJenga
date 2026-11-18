# Phase 1 results: Sets and tapes

Run through the coordinator: job `d2481953` on commit `97cdb6c0`, batch
editor, map `sparse-viaduct-study`. Method
`StudioAcceptance.RunPhase1`. Full report: [evidence/phase1/report.md](evidence/phase1/report.md).

| # | Acceptance test | Result | What was measured |
|---|-----------------|--------|-------------------|
| 1 | Set with 3 marks and a collapse cue; record; play back 3 times; drift under 1 mm and 0.1 degrees | Pass | Three playbacks, 26070 samples each: worst 0.000 mm and 0.0000 degrees. 90 side-by-side frames (live left, playback right) differ by 0 of 255 on average. |
| 2 | Scrub to 10 random frames | Pass | 1446 block poses: worst 0.791 mm. 30 actor positions: worst 0.0 mm. |
| 3 | A live survivor on a moving block during playback rides it and is pushed by it | **Not met** (a game limit, not a Studio bug) | Playback matches the live game exactly, but the live game does not carry a standing survivor. See below. |
| 4 | A Theatre v4 take loads and plays | Pass | 180 frames converted to v5, played back with 0.000 mm drift. |
| 5 | Game.unity still plays solo with the Studio closed | Pass | 240 frames, local survivor active, no Studio objects in the scene, 0 exceptions. |
| 6 | A track type and a mode tab added in their own files | Pass | `ExampleNoteTrack.cs` recorded, saved and played; `ExampleNotesMode.cs` shows as a tab; unknown track types survive a load and save. |
| - | Runtime errors | Pass | None. |

## Test 3: the spec line is not met

The test shoves the roof block the Set's survivor stands on (0.70 m
sideways at 3.6 m/s) and records it live. Then the same survivor stands
there again while the world is only played back.

- Live recording: the survivor was carried **0.01 m**.
- Against playback: **0.00 m**. The two paths are never more than 0.01 m
  apart over 180 frames.

So playback treats a live survivor the way the real physics did. That is
what the automated test checks, and it passes. But nobody rides anything
([rider-00029.png](evidence/phase1/rider-00029.png),
[rider-00045.png](evidence/phase1/rider-00045.png)), so the spec's "rides it
and is pushed by it" is **not shown**.

Why (found by the reviewer, the Fable 5.2 chat): `CastleSurvivor` remembers
what it stands on as `hit.transform`, which is the rigidbody's transform. On
maps without unified collision a survivor stands on a per-block proxy
collider that belongs to one shared body that never moves, so the carry is
always zero. It is the same for the local player in a normal match. The fix
is one line in Characters and changes how every survivor stands on moving
blocks, so it is listed for Evan in `CROSS-BOX-CHANGES.md` and was not made.
Once it is made, physical playback (`WorldTrack.FixedStep`) should carry a
performer, and this test will show it without changes.

An earlier version of the test saw a stand-in move 0.20 m while its block
moved 0.70 m; that was most likely the block's edge pushing the capsule.

## Look at
- [compare-00045.png](evidence/phase1/compare-00045.png): live recording next to playback.
- The window in use: [evidence/window/](evidence/window/) (12 screenshots,
  see `PHASE-5-results.md` for the tour).
