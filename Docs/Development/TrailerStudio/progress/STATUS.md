# Where the builder chat stopped (2026-11-18, about 08:25)

Stopped because the chat's usage limit was reached. All five phases are
written and committed on `claude/trailer-studio-a` (not pushed). Not all of
it has passed in Unity yet. Nothing has been handed to the "Fable 5.2" chat.

## Unity results so far

| Phase | Last finished run | Result |
|-------|-------------------|--------|
| 1 | job `6cb4148b` on `fa00dea7` | All checks pass except "live survivor rides a played-back block" (the test's own scene pushed the block too fast). Test scene changed in `4f257e50`; rerun `7b4a9891` was in progress and passing so far. |
| 2 | job `5c1a12cd` on `28ca15e1` | 11 of 11 pass. Report and pictures in `evidence/phase2/`. |
| 3 | job `defe025d` on `fa00dea7` | Variation run (20 tries) passed earlier. Tornado, ripple and branch failed for reasons fixed in `4f257e50` (test read the wrong field; test picked far-apart blocks; a branch let physics run before recording). Rerun `92fd13b6` was in progress. |
| 4 | none | First run `0670313e` was queued. Never run in Unity. |
| 5 | none | First run `fc6ddf7e` (on `fa00dea7`) had been stuck entering Play Mode in slot 6 for ten minutes. Never run in Unity. |

Read results with `coordinator.py job --owner claude-builder-prompt --id <id>`
and the `[TrailerStudio acceptance]` lines of each run's `unity.log`.

## Not done

- Phases 4 and 5 have no Unity result. Expect first-run failures to fix.
- The window has only been built in batch mode (tabs listed, no errors). It
  has never been seen on screen. `StudioAcceptance.RunWindowTour` (written,
  compiles, never run) screenshots every tab; it needs an interactive editor
  from a manual coordinator receipt, not a batch job.
- No `PHASE-n-results.md` files, no review material collected except Phase 2.
- Not pushed. No handoff sent to the "Fable 5.2" chat.
- Slot 6 was reported to the coordinator chat as held for review after job
  `5c1a12cd` (a line-ending-only rewrite of `Candy Head.asset`); check that
  it and the slots of the jobs above were returned.
- Known gaps by design are listed in `DECISIONS.md` (28, 30, 33, 35, 42, 44,
  48, 49). Nothing was tested in a headset.
