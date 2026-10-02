HISTORICAL NESTED-PROJECT AUDIT. See ROOT_TRANSFER.md for validation of the tracked root project. Do not follow the nested path below.

# HandDemo portability audit

Scope: `C:/Users/evanp/Downloads/DeadlyJenga/DeadlyJenga/Assets/HandDemo` (the imported copy in the open DeadlyJenga Unity project). Branch remains EP_Hand_Demo. No commit or push. The nested project was already untracked in the outer Git repository; review its intended repository layout before staging.

## Changes

- ThirdPersonVRFollow.cs: removed UniversalAdditionalCameraData dependency; preserved parent-only VR following and tracked pose ownership, desktop pitch, and restoration on mode changes. Built-in stereo eye targeting is guarded against SRP use. Added local camera discovery and clear missing-reference diagnostics.
- FlyingHandController.cs: retained XR-display-based Auto mode and keyboard/mouse fallback; repairs absent embedded actions/bindings and safely disposes them. Prevents duplicate controller components.
- Runtime/Editor asmdefs: removed URP and XRI references. Existing Input System and XR Core Utilities suffice.
- FlyingHandQuest.unity: removed unavailable URP camera/light components; refreshed HandDemo assembly identifiers. FlyingHandPlayer.prefab: refreshed assembly identifier without changing GUIDs.
- Three materials: replaced missing URP Lit shaders and URP editor subassets with Built-in Standard, preserving colors and roughness.
- FlyingHandSceneBuilder.cs: uses the host pipeline default material or Standard, restricts modifications to HandDemo scenes, guards duplicate cameras/origins/players and supplies a light when needed.
- Smoke tests: replaced XRI simulator types with minimal Input System devices in HandDemoTestInput.cs; added menu commands, prerequisites and temporary input settings for VR testing.
- HandDemoPortabilityAudit.cs: repeatable scene/prefab Missing Script, broken reference, shader/material, pose action, external asset and duplicate rig checks.
- Documentation: current setup instructions/package inventory replace source-project assumptions; old test reports are explicitly historical.

## Static checks

PASS: every non-built-in serialized asset GUID resolves to HandDemo or an installed Input System/Core Utilities package.
PASS: every local fileID in scene/prefab resolves; no dangling component, transform or Inspector reference.
PASS: scene uses Default layer only, one MainCamera tag and otherwise Untagged; no custom tag/layer dependency.
PASS: one camera, one XR Origin and one AudioListener; no EventSystem or UI requiring one.
PASS: runtime code has no Meta SDK, Android-only, XRI, URP/HDRP, external asset path or original-project path dependency.
PASS: embedded move/yaw/trigger and head/controller pose actions are included; no external .inputactions asset is required.
PASS: null collider physics materials intentionally select Unity defaults; textures/meshes/skybox need no omitted feature assets.
PASS: isolated HandDemo.Runtime/HandDemo.Editor assemblies and FlyingHand namespace have no duplicate declarations in the destination.
PASS: Unity 6000.3.23f1 matches the source's Unity 6 APIs. Older versions are not claimed supported.
PASS: host Input System 1.20.0, Core Utilities 2.6.0, XR Management 4.7.0 and OpenXR 1.16.1 are already resolved; no package installation needed.

## Unity validation

PASS: Unity compiled both HandDemo assemblies including the new audit helper with no compiler errors. The demo scene opened and entered Play Mode with no headset and selected Desktop automatically. The live structural audit found no Missing Scripts, dangling references, external Assets dependencies or unsupported materials; all three shaders resolve to Standard. Pose action bindings and camera/controller/target references passed. The 13 desktop probe checks passed (see DesktopValidation.txt). All 14 VR binding/physics probe checks passed (see VRValidation.txt). These use synthetic Input System devices, not physical headset hardware.

## Remaining host/hardware requirements

The subsequent user-authorized Quest setup is recorded in QUEST_CONFIGURATION.md. Android is active, with OpenXR/Meta Quest support configured and no blocking Android or Standalone OpenXR validation issues. Real HMD stereo rendering, tracking, physical controller input, APK build/deployment and comfort remain checks on the target hardware.

The camera code does not depend on a render-pipeline package. Shipped materials target the current Built-in renderer. If moved to an SRP, convert materials; forced Desktop during a running SRP XR session does not disable that pipeline's HMD output. Auto without a display uses desktop input and disables pose driving.



