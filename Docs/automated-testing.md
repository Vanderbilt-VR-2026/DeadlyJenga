# Automated Unity testing

A small "bridge" on the team's Windows test PC (Evan's HP laptop) watches GitHub. When a
test branch gets a new commit, it runs the Unity tests on that exact commit and pushes the
results to the `test-results` branch, where people and AI agents (ChatGPT's GitHub connector)
can read them as ordinary files.

```
push to chatgpt/** | ci/** | test/**
  -> bridge on the test PC notices it (checks every 90 s)
  -> checks out that exact commit in C:\DeadlyJengaCI\repo
  -> Unity 6000.3.23f1 batch mode, PlayMode tests in DeadlyJenga.DevTools.Tests
  -> pushes results/<branch>/latest.md + latest.json to the test-results branch
```

Why not GitHub Actions: a self-hosted runner needs a repo-admin registration token. The bridge
only needs normal push access, opens no ports, and gives the same loop.

## Reading results
On branch `test-results`, branch names use `__` instead of `/`
(`chatgpt/fix-x` -> `results/chatgpt__fix-x/`):
- `latest.md` — readable report: commit, pass/fail counts, failed test names and messages, log excerpt.
- `latest.json` — machine-readable result.
- `latest.xml` — NUnit XML.
- `history/<commit>.json` — one file per tested commit.

Always check that `commit_sha` matches the commit you pushed; a newer push may still be waiting.

`latest.json` fields: `commit_sha`, `branch`, `unity_version`, `runner`,
`runner_status` (`completed`, `compilation_failure`, `infrastructure_failure`, `timeout`,
`test_execution_failure`), `test_status` (`passed`, `failed`, `not_run`), `tests_total/passed/failed/skipped`,
`duration_seconds`, `failed_tests[] {name, message}`, `log_excerpt[]`.
A run that produced no test results is never reported as passed.

## Tests
Assembly `DeadlyJenga.DevTools.Tests` in `Assets/_DeadlyJenga/DevTools/Tests/` (DevTools may
reference every box; the `UNITY_INCLUDE_TESTS` constraint keeps it out of builds).
- `PhysicsSmokeTests` — generic Unity physics: falling boxes at several heights/speeds and
  collision modes do not tunnel and come to rest on the floor, masses, a 3-block stack, a frozen axis.
- `CastleBlockTests` — the real `CastleBlock` (World/Blocks): layer setup, climbable,
  `Push` velocity change + `GameEvents.Pushed`, resting on a floor.

Add tests as `[Test]` / `[UnityTest]` classes in that folder. Keep them deterministic: fixed
start positions and explicit tolerances.

Not covered (needs a headset or live XR runtime): hand/controller input, comfort and turning,
Quest performance, multiplayer between devices.

## Run tests by hand
Close any Unity Editor that has the same folder open, then:
```powershell
.\Tools\UnityTesting\Run-UnityTests.ps1 -ProjectPath (Get-Location).Path -ResultsDir $env:TEMP\dj-results
```
Exit code 0 = passed, 1 = tests failed, 2 = could not run (compile error, Unity missing, timeout).

## The bridge (test PC)
- Folder `C:\DeadlyJengaCI`: `repo\` (clone used only for testing, keeps its own `Library\`),
  `results\` (worktree of `test-results`), `bin\` (trusted copy of the scripts),
  `runs\` (raw output incl. `unity.log`), `state.json` (last tested commit per branch), `bridge.log`.
- Runs as Evan's Windows user through scheduled task **DeadlyJenga Test Bridge**, started at
  logon. It works only while that user is logged in and the PC is awake.
- Install / update scripts: `Tools\UnityTesting\Install-Bridge.ps1`. Remove: `-Remove`.
- Test once by hand: `powershell -File C:\DeadlyJengaCI\bin\Watch-Branches.ps1 -Once`
- Stop now: `Stop-ScheduledTask 'DeadlyJenga Test Bridge'`. Disable: `Disable-ScheduledTask 'DeadlyJenga Test Bridge'`.
- Re-test a commit: delete its branch's entry from `state.json`.
- Stuck Unity: stop the `Unity.exe` whose command line contains `DeadlyJengaCI`; the bridge's
  timeout (120 min) also kills it and reports `timeout`.

### Safety
- Only branches of this repository are watched; fork PRs are never run.
- The bridge runs its own trusted copy in `bin\`, not the scripts in the tested commit.
- It runs fixed commands only. Text in commits, PRs or logs is never executed.
- One run at a time (bridge lock + workspace lock). It never merges or pushes to any branch
  except `test-results`.
- Known risk: anyone who can push a watched branch runs C# (the tests) on the test PC as Evan.
  Keep repo write access to the team.

## Protocol for ChatGPT / coding agents
1. Branch `chatgpt/<short-task>` from the latest `main`. Commit the code **and** its tests.
2. Push. Within ~2 minutes the bridge starts; a run takes ~6 minutes (longer after package changes).
3. Read `results/chatgpt__<short-task>/latest.md` on branch `test-results`. Confirm `commit_sha`
   is your latest commit; if not, wait and read again.
4. `test_status: failed` -> fix the code (change a test only if the test itself is wrong) and push.
   `compilation_failure` -> fix using the log excerpt. `infrastructure_failure` / `timeout` -> tell a
   human; do not keep retrying.
5. Ready for review = `passed` on your latest commit. Open a PR into `main` and describe the change.
   Never merge; a human reviews and tests on the headset.
