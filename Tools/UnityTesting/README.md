# Unity testing tools

- `Watch-Branches.ps1` — the bridge: polls GitHub, tests new commits, pushes results to `test-results`.
- `Run-UnityTests.ps1` — run Unity tests in batch mode, write `results.json`.
- `Publish-TestSummary.ps1` — turn `results.json` into `report.md`.
- `Install-Bridge.ps1` — install the bridge as a logon task on the test PC.

Full guide: [Docs/automated-testing.md](../../Docs/automated-testing.md).
