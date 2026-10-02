# Root project Quest configuration

Project: C:/Users/evanp/Downloads/DeadlyJenga. Branch: EP_Hand_Demo.

Only these existing root settings were edited:
- ProjectSettings/ProjectSettings.asset: Android minimum API 25 -> 29; target API Automatic -> 34.
- Assets/XR/Settings/OpenXRPackageSettings.asset: enabled Oculus Touch Controller Profile for Android, alongside the existing Touch Plus and Touch Pro profiles.

Android was selected as the local active build target. The root already had Android OpenXR startup and Meta Quest Support, ARM64, IL2CPP, Input System (New), GameActivity, single-pass XR, Linear color space and Vulkan-first graphics APIs. These were retained, as were Windows support, URP, existing loader GUIDs and the Lobby build scene. The nested project's XR assets, settings, package manifest and package lockfile were not copied.

Use a dedicated Build Profile containing FlyingHandQuest to test this demo on Quest without replacing the Lobby build scene. Physical headset activation, controller input, stereo rendering, APK deployment, performance and comfort still require device testing.

See ROOT_TRANSFER.md for actual validation results. No commit or push was made.
