# Phase 2 results: performance passes

Run through the coordinator: job `fb31c231` on commit `60471386` (first passed as job `5c1a12cd` on `28ca15e1`; the numbers below are from that run, the linked report is the latest), batch
editor, map `sparse-viaduct-study`. Method
`StudioAcceptance.RunPhase2`. Full report: [evidence/phase2/report.md](evidence/phase2/report.md).

| # | Acceptance test | Result | What was measured |
|---|-----------------|--------|-------------------|
| 1 | Record actor A, then B while A plays back | Pass | A's playback was 0.000 mm from her take on all 180 frames while B was recorded. A's take was not changed. |
| 2 | Twelve actors play back at once | Pass | 12 actors on screen; 6 s of tape took 6.17 s in the batch editor. |
| 3 | Punch in at 2.0 s; the join does not pop | Pass | Before 2.0 s the new take equals the old one (0.00 mm). Largest one-frame move 31.2 cm before the join, 33.3 cm across it. |
| 4 (XR) | The same in an emulated XR session (`RunXr`, job `c897c8cf`) | Pass, 1 finding | Real-time clock in VR; a VR take stores head and both hands 0.00 mm from the emulated devices; the wrist panel shows 3-2-1 and is pressed with the index fingertip (fixed: it tested the palm). Finding: the head freezes after the rig is re-enabled while the headset reports steady tracking (Characters, not fixed). |
| 4 | A pass in the headset records head and hands; wrist panel works | Partly | Head and both hands are stored separately in each frame; the wrist panel's keys work. **Not run in the XR simulator or a headset** (the batch editor has no XR session). |
| 5 | Live-physics pass re-records the world; old takes get a stale badge | Pass | The two worlds first differ at frame 76; the Studio reported frame 76 and flagged 12 tracks. |
| - | Comp from two takes | Pass | Source takes untouched. |
| - | A recorded hand plays back (added after review) | Pass | The hand bot's take was drawn on 179 of 180 frames; recorded path 13.8 m, hand on screen travelled 13.8 m. |
| - | Slow-motion performing | Pass | Same jump height at half world speed; takes stored at real speed. Climbing in slow motion not exercised. |
| - | Runtime errors | Pass | None. |

## Not covered
- The scripted performer could not climb (no climbable wall near the mark),
  so climbing was not recorded in this run.
- Everything in a real headset: comfort, countdown readability, the wrist
  panel at arm's length, hand tracking.

## Look at
- [two-actors-00030.png](evidence/phase2/two-actors-00030.png): A played back while B performs.
- [twelve-00025.png](evidence/phase2/twelve-00025.png): twelve actors in one playback.
