# Automated Unity testing

Pushes to certain branches run DeadlyJenga's Unity tests on a Windows PC (the
"runner") and post the results back to GitHub, where people and AI agents can read them.

```
push to chatgpt/** | ci/** | test/**   (or a PR into main from this repo)
        -> GitHub Actions workflow  .github/workflows/unity-tests.yml
        -> self-hosted runner on the HP laptop (label deadlyjenga-unity)
        -> checkout of the exact commit -> Unity 6000.3.23f1 batch mode, PlayMode tests
        -> results.json + report comment + job summary + artifact
```

## Where results appear
1. **Report comment** (best for ChatGPT): if the branch has an open PR, one comment
   starting with `<!-- deadlyjenga-unity-tests -->` is edited on every run. With no PR,
   a comment is added to the tested commit.
2. **Job summary** on the workflow run page (same text).
3. **Artifact** `unity-test-results`: `results.xml` (NUnit), `unity.log`, `results.json`, `report.md`.
   Artifacts are zip downloads; do not assume an AI connector can open them.

`results.json` fields: `commit_sha`, `branch`, `runner_status`
(`completed`, `compilation_failure`, `infrastructure_failure`, `timeout`, `test_execution_failure`),
`test_status` (`passed`, `failed`, `not_run`), counts, `failed_tests[] {name, message}`, `log_excerpt[]`.
A missing results file is never treated as a pass.

## Tests
Assembly `DeadlyJenga.DevTools.Tests` in `Assets/_DeadlyJenga/DevTools/Tests/` (DevTools may
reference every box; the `UNITY_INCLUDE_TESTS` constraint keeps it out of builds).
- `PhysicsSmokeTests` — generic Unity physics: floor penetration at several heights/speeds and
  detection modes, masses, a 3-block stack, rotation constraints. No game code.
- `CastleBlockTests` — the real `CastleBlock` (World/Blocks): layer setup, climbable, `Push`
  velocity change + `GameEvents.Pushed`, resting on a floor.
Add a test: put a `[Test]` or `[UnityTest]` class in that folder (or a new test asmdef and
pass it with `-Assemblies`). Keep tests deterministic: fixed starting positions, explicit tolerances.

Not covered (needs a headset or a live XR runtime): hand tracking, controller input,
comfort/turning feel, Quest performance, multiplayer between devices.

## Run tests by hand
Close any Unity Editor that has the same project folder open, then:
```powershell
.\Tools\UnityTesting\Run-UnityTests.ps1 -ProjectPath (Get-Location).Path -ResultsDir $env:TEMP\dj-results
```
Exit code 0 = passed, 1 = tests failed, 2 = could not run (compile error, Unity missing, timeout).

## The runner
- Files: `C:\actions-runner\deadlyjenga` (official GitHub runner). Work folder: `_work` inside it.
  The checkout keeps `Library/` between runs, so only the first run does a full import.
- Starts at logon through the scheduled task **DeadlyJenga Unity Runner**, as Evan's Windows
  user (Unity's license is per user). It only runs while that user is logged in.
- Install / remove: `Tools\UnityTesting\Install-Runner.ps1` (asks for a token from a repo admin).
- Stop now: `Stop-ScheduledTask 'DeadlyJenga Unity Runner'; Get-Process Runner.Listener | Stop-Process`
- Disable automation: `Disable-ScheduledTask 'DeadlyJenga Unity Runner'`, or disable the workflow in
  GitHub (Actions > Unity tests > ... > Disable workflow).
- Stuck Unity: `Get-Process Unity | ? { $_.Path -like '*6000.3.23f1*' }` and stop the one whose
  command line contains `_work`; delete `_work\DeadlyJenga\DeadlyJenga\Temp\dj-test-runner.lock`.

### Safety
- Only branches in this repo trigger it; fork PRs are skipped by the job `if:`.
- The workflow never merges and has no permission beyond comments and reading code.
- One run at a time (`concurrency` group + a workspace lock file). Timeouts: Unity 120 min, job 150 min.
- Risk to know about: anyone who can push a `chatgpt/**`, `ci/**` or `test/**` branch can run code
  on this laptop as Evan. Keep repo write access to the team. A separate Windows account would
  isolate it better but needs its own Unity license activation.

## Antigravity (optional)
Antigravity 2.32.1 ships a CLI: `%USERPROFILE%\.gemini\antigravity\bin\agentapi.bat`
(`new-conversation`, `send-message`, `get-conversation-metadata`). It only works when
`ANTIGRAVITY_LS_ADDRESS` points to a running Antigravity agent server; it is set inside Antigravity's
own terminals, not for the runner. So automatic diagnostics are **off** by default.
To try: set the repo variable `DJ_ANTIGRAVITY_DIAGNOSTICS=true`, and make `ANTIGRAVITY_LS_ADDRESS`
available to the runner. `Invoke-LocalDiagnostics.ps1` then asks for a read-only report that is
added to the PR comment as advisory text. It never edits files or fails the build.
Manual handoff: open the artifact folder in Antigravity and ask it to diagnose `results.json`.

## Protocol for ChatGPT / coding agents
1. Make a branch named `chatgpt/<short-task>` from the latest `main`. Commit code **and** tests.
2. Push it. That triggers the run. Open a draft PR into `main` so results land in one comment.
3. Wait for the "Unity tests" check, then read the PR comment marked `<!-- deadlyjenga-unity-tests -->`
   (or the commit comment if there is no PR). Check `Commit` matches your latest SHA.
4. `test_status: failed` -> fix the code (not the test's tolerance, unless it is genuinely wrong) and push again.
   `compilation_failure` -> read the log excerpt. `infrastructure_failure`/`timeout` -> report to a human;
   do not loop on it.
5. Ready for review = check green on the latest commit and the PR describes what changed.
   Never merge; a human reviews and merges. Headset behavior still needs a human test.
