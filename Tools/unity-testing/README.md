# DeadlyJenga Autonomous Unity Testing Pipeline

This directory contains the automation, testing, and diagnostic pipeline connecting **ChatGPT**, **GitHub**, **Google Antigravity Desktop**, and the local **Unity Editor** for `Vanderbilt-VR-2026/DeadlyJenga`.

---

## 1. Architecture Overview

```
USER
  │
  ▼
CHATGPT (Feature branch push / PR)
  │
  ▼
GITHUB REPOSITORY (Vanderbilt-VR-2026/DeadlyJenga)
  │
  ├─► GitHub Commit Status: `pending` (context: unity/tests)
  ▼
WINDOWS EXECUTION RUNNER (`Invoke-DeadlyJengaPipeline.ps1`)
  │
  ├─► Syncs isolated worktree (`DeadlyJenga-QuestBuilder\project`)
  ├─► Executes Unity batch-mode tests (`Run-UnityTests.ps1`)
  │     ├── EditMode Tests (SmokeTests.cs, settings, core types)
  │     └── PlayMode Tests (FloorPenetrationTests.cs, physics)
  │
  ├──► On Test Failure:
  │     └─► Triggers Antigravity AI (`Invoke-AntigravityDiagnostic.ps1`)
  │           └─► Deep log analysis, root cause, suggested fix
  │
  ▼
GITHUB INTEGRATION
  ├─► Sets Commit Status (`success` or `failure`)
  └─► Posts/updates PR Comment with Markdown report + machine-readable `test-summary.json`
  │
  ▼
CHATGPT GITHUB CONNECTOR
  └─► Inspects results, applies fixes, and pushes new commits
```

---

## 2. Components

### `Run-UnityTests.ps1`
Executes Unity in headless batchmode (`-batchmode -nographics -runTests`), parses the resulting NUnit XML into structured `test-summary.json` and human-readable `test-report.md`.
- **Supports:** `EditMode`, `PlayMode`, or `Both`.
- **Exit Codes:**
  - `0`: All tests passed (`PASS`).
  - `1`: One or more tests failed (`FAIL`).
  - `2`: Unity execution error or crash (`ERROR`).

### `Invoke-AntigravityDiagnostic.ps1`
When tests fail, this script connects to Google Antigravity Desktop via the local `AgentAPI` interface (`agentapi.bat new-conversation`).
- Packages the failed test details, stack traces, and relevant Unity editor log lines.
- Asks Antigravity to perform a root-cause diagnosis and recommend exact C# code changes.
- Monitors the agent session until completion and embeds the diagnosis into `test-summary.json` and `test-report.md`.

### `Invoke-DeadlyJengaPipeline.ps1`
The pipeline coordinator. Can run either as:
- **Direct Dispatch:** `.\Invoke-DeadlyJengaPipeline.ps1 -Branch <name> -CommitSha <sha> -PullRequest <num>`
- **Polling Daemon:** `.\Invoke-DeadlyJengaPipeline.ps1 -Poll -IntervalSeconds 30 -MaxRuns 10`

---

## 3. Quickstart Commands

### Run tests locally:
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-testing\Run-UnityTests.ps1 -TestPlatform Both
```

### Run pipeline against a specific PR:
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-testing\Invoke-DeadlyJengaPipeline.ps1 -Branch "codex/my-feature" -PullRequest 175
```

### Start continuous polling daemon:
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-testing\Invoke-DeadlyJengaPipeline.ps1 -Poll -IntervalSeconds 30
```
