# HandDemo transfer to the tracked root — final result

Project validated: C:/Users/evanp/Downloads/DeadlyJenga
Branch: EP_Hand_Demo. No staging, commit, push, deletion, or branch switch.

## Exactly what was copied

Copied all 68 files beneath DeadlyJenga/Assets/HandDemo into Assets/HandDemo, plus DeadlyJenga/Assets/HandDemo.meta into Assets/HandDemo.meta: 69 source files total. Every copied file initially matched its source SHA-256; every original .meta file remains byte-identical after root adaptation. No incoming GUID collisions were found.

ROOT_TRANSFER_FILES.csv lists each source file, destination and original source SHA-256. The nested project remains intact. No Library, Temp, Logs, obj, UserSettings, Packages, ProjectSettings or Assets/XR directory was copied.

## Adaptations in the root copy

- Converted only Materials/Ground Slate.mat, Materials/Matte White Hand.mat and Materials/Test Crates Orange.mat to the root's existing URP Lit shader, retaining material GUIDs and colors. No pipeline change.
- Kept runtime controller/camera code, scene and prefab from the validated transfer. No unrelated gameplay or scene edits.
- Corrected the two Editor smoke probes to time stages using simulation time and drain desktop input between stages, avoiding false failures during shader/import stalls.
- Corrected temporary InputSettings ownership in both probes for Input System 1.20: preserve the original transient settings when swapping to a probe clone, then restore their flags. This prevents editor validation exceptions after the probe.
- Added Editor/HandDemoRootValidation.cs and its meta: repeatable desktop + simulated XR validation across normal Play Mode reloads, with a report in Temp. Its explicit PrepareAndBegin command performed the initial HandDemo-only material conversion.
- Updated README.md, IMPORT_GUIDE.md, QUEST_CONFIGURATION.md, PORTABILITY_AUDIT.md and PackageDependencies.json to distinguish the real root from historical nested-project validation.
- Added this report, ROOT_TRANSFER_FILES.csv and RootValidation.txt, with Unity metadata.

## Exact existing-file changes outside HandDemo

1. .gitignore: added /DeadlyJenga/ to exclude the untouched nested recovery copy from staging.
2. Assets/XR/Settings/OpenXRPackageSettings.asset: enabled Android Oculus Touch Controller Profile alongside existing Touch Plus/Touch Pro profiles. Existing XR assets and their GUIDs retained.
3. ProjectSettings/ProjectSettings.asset: Android minimum API 25 -> 29; target API Automatic -> 34.

Android is the active local Editor target. Packages/manifest.json and packages-lock.json are unchanged. Existing URP, Linear color space, Input System, ARM64/IL2CPP, GameActivity, Android OpenXR loader and Meta Quest support, Vulkan-first graphics APIs, Windows support, and the Lobby build scene are retained. Do not replace the root manifest with the nested project's manifest.

## Final root validation

Unity 6000.3.23f1 compiled successfully. HandDemo opened and rendered with its URP materials. The final run has zero compiler errors or runtime exceptions. Earlier failed probe results and editor exceptions were fixed before this final run.

- All 13 desktop checks pass: Auto desktop without a running display, WASD/Q/E, mouse yaw/pitch, upright motion and mode changes.
- All 14 simulated XR checks pass: stick/trigger bindings, force-driven movement, collisions, stable hand orientation, camera follow, independent tracked head pose and bounded speed.
- The collided crate finished overturned (180 degrees); the hand remained upright.
- Scene/prefab audit passes: no Missing Scripts, missing/unsupported materials, broken serialized references or external Assets dependencies. One Camera/MainCamera, one XR Origin, one AudioListener and no duplicate EventSystems.
- Android OpenXR validation has zero errors. Optional recommendations remain for the host's existing SSAO feature and latency optimization; these shared renderer settings were not changed.
- Left the root HandDemo scene open, Play Mode stopped, scene not dirty, Android active.

See RootValidation.txt for the actual final output. There was no running physical XR display: automatic VR activation is code-reviewed, while actual headset activation/rendering/controller operation, APK deployment, performance and comfort remain device tests. Use a separate Build Profile for HandDemo rather than replacing Lobby in the shared build list.

## Git readiness

Ready for a scoped root-project commit review. git status shows the three modified files listed above, Assets/HandDemo.meta and Assets/HandDemo/. The nested recovery copy and all generated caches are excluded. No changes were staged, committed or pushed.
