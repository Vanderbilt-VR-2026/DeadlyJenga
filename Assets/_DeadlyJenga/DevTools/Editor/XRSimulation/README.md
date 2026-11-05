# Desktop XR regression fixtures

These editor-only tools feed synthetic input into the real Game scene. They do
not run the Android APK or establish physical Quest tracking, comfort, stereo,
haptics or device performance. Nothing runs a scenario automatically on import.
No Core or gameplay change is included in this branch.

## Prerequisites

Use the repository Unity version and existing packages. The imported XRI 3.3.2
XR Interaction Simulator sample must already be present. The fixture discovers
its prefab and hand-controls asset by name. Do not add or upgrade packages to
make a test pass. Use an isolated checkout with no physical XR devices or other
active hand provider. Preserve existing Play sessions and unsaved scenes.

Manual inspection: open the existing Game scene, enter Play Mode, then open
**Deadly Jenga > Testing > Simulated VR Controls** and attach. The window lists
stock keyboard controls and its explicitly synthetic mixed-controller fixture.
Detach restores temporary settings and preferences. Automated tests require a
fresh Edit Mode run; do not attach this window at the same time.

## Automated entry points

All names below are in namespace `DeadlyJenga.XRSimulation.Editor`.

| Type | Coverage |
| --- | --- |
| `UprightPostureScenario` | Both comfort profiles and dominance preferences; stock keyboard head heights 1.70/1.15m, pitch +/-75 and roll +/-60; actual TPD consumption, world-up root, rendered HeadAnchor, hips, ankles, deformed foot mesh, support floor, exterior/lookdown captures and hand-to-survivor role return. |
| `FlyingCameraFirstRenderScenario` | Two delayed-first-valid cases, one per preference, then subsequent lean and held-lean checks. Uses the existing labelled stock simulator state seam; the actual TPD and production callbacks consume it. No direct gameplay camera write. |
| `SimulatorScenario` | Controller routing, HMD loss/recovery, calibration, optical expressions, wrist offset, mixed input, tracking transitions and role return. |
| `SimulatorTailScenario` | Remaining optical-expression/mixed-input/role tail with the original strict assertions, without rerunning the controller prefix. |
| `HandExpressionDiagnosticScenario` | Per-player-frame action/expression/raw-joint and normalized-joint diagnosis. |
| `HandPoseCaptureScenario` | Raw and production hand-pose evidence. |
| `SeaSpiritScenario` | Existing focused sea-spirit input and lifecycle checks. |
| `FlyingCameraReadinessScenario` | Full camera matrix; use the focused first-render entry when only that fix changed. |
| `FlyingCameraFiniteScenario` | Inactive non-XR private finite-input unit seam; not real tracking timing evidence. |

The included `AgentPlaytest.Runner.StartFromFile(absolutePlanPath)` can be called
through your existing editor scripting/automation connection. It needs no Codex
chat, personal skill, Workboard or laptop-specific tooling. Supply a JSON plan
with the actual project root, existing Game scene path and a new output folder:

```json
{
  "schemaVersion": 1,
  "id": "upright-unique-run-id",
  "project": "<absolute checkout root>",
  "output": "<absolute new evidence folder outside source/cache>",
  "scene": "<existing Game scene project-relative path>",
  "scenario": "DeadlyJenga.XRSimulation.Editor.UprightPostureScenario",
  "timeoutSeconds": 120,
  "width": 960,
  "height": 540,
  "mute": true
}
```

The same runner accepts an atomically written `Library/AgentPlaytest/inbox.json`
plan with `targetPid` set to the verified existing editor PID. Check the exact
project/PID before submitting. Do not reuse a nonempty evidence folder, overwrite
an existing request or start another editor to bypass a busy owner. Local owner
admission rules still apply on machines which have them; this example does not
replace those rules. No batch launch is necessary for the warm-editor route.

## Evidence and acceptance

Read `report.json`, scenario JSON, actual PNG captures and cleanup receipts.
A successful import or Game startup is not a gameplay pass. Input-sensitive
observations come from the real player loop after script LateUpdate; do not
replace them with Editor input-buffer reads or forced InputSystem updates.
The finite-only unit seam is explicitly separate. Check report failures and
errors even when some assertions passed. Require owned observer/hook cleanup
while Play Mode remains active, then confirm return to stable Edit Mode.

For posture, inspect the actual exterior/lookdown images as well as the strict
ankles/max-foot-geometry < hips < tracked AND rendered-head test and floor/root
metrics. For the camera, inspect the FIRST non-explicit rendered position-valid
frame; a good later frame must not erase a first-render failure. Preserve failed
attempts. Open/Fist reference repair is temporary on the owned simulator
instance; original reader references and borrowed action enabled states must
restore. No imported prefab/action/meta GUID rewrite is part of the repair.

Record tested production commit or complete dirty snapshot, fixture commit,
loaded assembly identity, actual mode/map, process identity and report/capture
artifacts with any result. Share those artifacts through your team's accessible
location; a private machine-local tracker is not sufficient review evidence.

## Checkpoint status (2026-11-05)

The initial code checkpoint is `34eb1b3fa41e7e4a2d480c15fe94c2088dc23dbd`,
based on main `f0e1fd06c95e1fa34f76fbcf7183ea29ae51c18d`. All fixture and
included harness C# compiled with Unity-bundled Roslyn against cached assemblies;
eight CLI refusal/safety checks passed. This does not prove native Editor import
or Game execution for this branch. Focused posture/camera acceptance is pending
against production `d21e13c57421192579f3531404fc2bdee9cd445f`, which contains
the separately owned upright-avatar and first-render fixes. Those production
fixes are not copied onto this branch. Without them, these regressions may fail.

Earlier different-snapshot native results were hand43/43 and sea264/264; the old
camera matrix was104/106 with TWO actual delayed-first-render failures. Those
historical results are not acceptance of this checkpoint or physical Quest.
The remaining expression/mixed-input tail and focused posture/camera need exact
native reports and captures before integration readiness can be claimed.

Keep integration serial. When applying into a checkout with an existing test
harness, preserve its accepted owner changes and review exact file preimages;
do not replace the harness wholesale. This branch is a recoverable test-tools
checkpoint, not a declaration that the production fixes or APK are verified.
