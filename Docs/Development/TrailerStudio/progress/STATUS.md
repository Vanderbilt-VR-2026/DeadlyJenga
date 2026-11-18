# Where builder chat A stopped (2026-11-18)

Stopped mid Phase 1 because the chat's usage limit was reached. Nothing here
is ready for review yet.

## Done (committed on `claude/trailer-studio-a`, not pushed)

- `7f2b4e7c` spec files and Phase 1 plan.
- `fb67fe5f` engine: Sets, v5 tape, track types, record/play/scrub, drift
  check, Theatre import, batch acceptance test.
- `4d4cfbd7` Studio window (UI Toolkit), extension points, progress notes.

Both code commits compile (in Unity for `fb67fe5f`; local Roslyn check for
`4d4cfbd7`). No change outside DevTools.

## Not done

- **Phase 1 acceptance has not passed.** Coordinator job
  `9a807cd2a00049cd9b567e32b78202ae` (commit `fb67fe5f`, slot 2) failed at the
  first step: "The stage did not open". The log
  (`.codex/deadlyjenga-worktree-pool/runs/9a807cd2…/…/unity.log`) shows
  Game.unity opened but Play Mode was never entered before
  `StudioAcceptance.Tick` step 2 gave up. Not diagnosed yet. First things to
  check: whether `EditorApplication.EnterPlaymode()` is delayed in batch mode
  so `isPlayingOrWillChangePlaymode` is briefly false (make step 2 wait for
  its deadline instead of failing at once), and whether `StudioRun.State` was
  reset. Check that the job's slot was returned.
- The window has never been opened in Unity.
- Tests 1 to 6 have no results; no `PHASE-1-results.md`; no review material.
- Phase 2 not started. Design notes for it are only in the chat.
- Branch not pushed.
