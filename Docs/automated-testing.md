# DeadlyJenga Automated Testing and Continuous Verification Guide

## 1. Overview

DeadlyJenga employs an automated test framework and continuous verification pipeline connecting **ChatGPT**, **GitHub**, **Google Antigravity Desktop**, and the local **Unity Editor**.

This allows AI and human contributors to write gameplay features, physics updates, and bug fixes on feature branches, and have tests automatically run locally in headless Unity batch mode, with automatic AI root-cause diagnosis for any failures.

---

## 2. Test Architecture

### 2.1 Assembly Structure
Automated tests are divided into two distinct assembly definitions to enforce clean separation between editor-time logic and runtime simulation:

| Test Platform | Assembly Name | Directory | References |
|---|---|---|---|
| **EditMode** | `DeadlyJenga.Tests.EditMode` | `Assets/Tests/EditMode/` | `DeadlyJenga.Core`, `TestAssemblies` |
| **PlayMode** | `DeadlyJenga.Tests.PlayMode` | `Assets/Tests/PlayMode/` | `DeadlyJenga.Core`, `DeadlyJenga.World`, `TestAssemblies` |

### 2.2 Test Inventory
- **EditMode Smoke Tests (`SmokeTests.cs`):**
  - `EditMode_Environment_GravityIsConfigured`: Validates that Unity's global `Physics.gravity` has a downward Y vector.
  - `EditMode_Core_TestDummyInitializesExpectedHealth`: Validates that `TestDummy` instantiates and sets default health to 100.
- **PlayMode Physics Tests (`FloorPenetrationTests.cs`):**
  - `FallingObject_DoesNotPenetrateFloor`: Instantiates a standard rigid cube 3 units above a static floor box collider, simulates physics for 2.0 seconds, and asserts that the object comes to rest without penetrating through the floor collider.
  - `HighVelocityFallingObject_DoesNotPenetrateFloor`: Tests high downward velocity (-25 m/s) with `ContinuousDynamic` collision detection to prevent tunneling.
  - `StackedBlocks_MaintainSupportStability`: Tests that a stacked Jenga block maintains stability on top of a lower block without clipping or falling through the floor.
- **PlayMode Smoke Tests (`PlayModeSmokeTests.cs`):**
  - `PlayMode_Smoke_ObjectSpawnsAndTicks`: Verifies GameObject lifecycle and frame stepping.

---

## 3. Tooling and Execution

All test scripts reside in `tools/unity-testing/`.

### 3.1 Running Tests Locally
To execute all tests locally in batch mode:
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-testing\Run-UnityTests.ps1 -TestPlatform Both
```

To run only PlayMode tests:
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-testing\Run-UnityTests.ps1 -TestPlatform PlayMode
```

### 3.2 Running the Pipeline Daemon
The pipeline daemon automatically polls GitHub for new commits on open pull requests or feature branches:
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-testing\Invoke-DeadlyJengaPipeline.ps1 -Poll -IntervalSeconds 30
```

---

## 4. Antigravity Intelligent Diagnostic Integration

When any automated test fails:
1. `Run-UnityTests.ps1` writes `test-summary.json` containing the failure message, stack trace, and relevant lines from `Editor.log`.
2. `Invoke-AntigravityDiagnostic.ps1` opens a new conversation with Google Antigravity Desktop via `agentapi.bat`.
3. The Antigravity AI inspects the code and log context, determines the root cause, and generates:
   - Root cause analysis
   - Affected source files and methods
   - Recommended C# code fix
4. The diagnosis is automatically appended to the GitHub PR comment so ChatGPT or developers can immediately fix the issue.

---

## 5. Guide for ChatGPT and Remote Contributors

When creating a feature or bug fix:
1. Create a branch: `feature/<name>` or `codex/<name>`.
2. Commit your code changes and add corresponding automated tests in `Assets/Tests/EditMode` or `Assets/Tests/PlayMode`.
3. Push to GitHub and open a Pull Request.
4. The local execution pipeline will automatically detect the commit, run the tests in Unity 6, and update the GitHub Commit Status (`unity/tests`).
5. If any test fails, inspect the PR comment:
   - Check the failure table.
   - Read the Antigravity Diagnostic Report for the exact root cause and suggested code changes.
   - Expand the `<details><summary>test-summary.json</summary>` block for structured JSON output.
6. Submit a fix commit to your branch. The pipeline will automatically re-run and verify the fix.
