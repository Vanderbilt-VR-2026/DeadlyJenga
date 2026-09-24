# DeadlyJenga

A multiplayer VR project targeting Meta Quest. The current starting scene is `Assets/Scenes/Lobby.unity`.

See [Project context](PROJECT_CONTEXT.md) for the proposed goals, gameplay, and scope. That document describes the design proposal, not a list of implemented features.

## First-time setup

1. Install **Unity Hub** and **Unity 6000.3.23f1**. Use the version recorded in `ProjectSettings/ProjectVersion.txt`; coordinate upgrades with the team.
2. In Unity Hub, install that editor's **Android Build Support**, including **Android SDK & NDK Tools** and **OpenJDK**.
3. Install [Git LFS](https://git-lfs.com/), then clone the project:

   ```sh
   git lfs install
   git clone https://github.com/Vanderbilt-VR-2026/DeadlyJenga.git
   cd DeadlyJenga
   git lfs pull
   ```

   If you already cloned the repository, run `git lfs install` and `git lfs pull` inside it. Images and other binary assets use LFS; cloning with Git is preferable to downloading a source ZIP.

4. In Unity Hub, add the cloned project folder and open it. Allow the initial asset import and package installation to finish.
5. Open `Assets/Scenes/Lobby.unity`.

GitHub repository membership and Unity Cloud project membership are separate. If Unity warns that you are not a member of the linked Cloud project, ask the project owner for an invitation when you need its cloud services. A local APK can be built without those services; do not relink the shared project to a personal Cloud project just to dismiss the warning.

## Build a Quest APK

1. Save your scene and exit Play mode.
2. Open **File > Build Profiles**, select **Android**, and switch to that platform if needed.
3. Use the shared scene list, with **Lobby** enabled as the first scene. Do not add the deleted `SampleScene` or asset-pack demo scenes.
4. Under **Project Settings > XR Plug-in Management**, select the Android tab and verify that **OpenXR** and **Initialize XR on Startup** are enabled.
5. Under Android **OpenXR** settings, verify **Meta Quest Support** and the controller profile matching the test headset. The project currently enables Quest Touch Plus and Touch Pro profiles; check the profile when testing different controllers.
6. Under **Player > Other Settings**, verify **IL2CPP** and **ARM64**. Use **XR Plug-in Management > Project Validation** for Android to check remaining device-specific requirements.
7. Build an **APK**, with **Build App Bundle** and **Export Project** disabled. Choose **Build** to create a file for later installation; **Build And Run** additionally requires a connected device.
8. Save the output under `Builds/`, for example `Builds/DeadlyJenga.apk`. This directory is ignored by Git.
9. Install and test the APK on the target headset. Verify launch into Lobby, head tracking, controller tracking, and any movement/interactions included in your change.

Editor Play mode and an APK build are separate checks. A successful build does not establish that headset tracking or controls work.

## Working together

- Start each feature on a new branch from up-to-date `main`. Save and commit existing work before switching branches; do not discard local changes to update.
- Commit `Assets/` **with its `.meta` files**, `Packages/manifest.json`, `Packages/packages-lock.json`, and intentional `ProjectSettings/` changes.
- Move and rename assets inside Unity so their metadata and references stay together.
- Keep `Library/`, `Temp/`, `Logs/`, `UserSettings/`, and build outputs out of Git. The repository already ignores them.
- Keep **Visible Meta Files** and **Force Text** serialization enabled. These are already configured.
- Coordinate who edits a shared scene. Put reusable objects into separate prefabs to reduce overlapping scene edits. Do not resolve scene/prefab conflicts by blindly accepting an entire file from one side.
- Review the diff before committing. Unity may change settings during imports and builds; include changes relevant to your PR rather than staging everything automatically.
- Open a PR and ask a teammate to review it. Describe what changed and which scene to open, and record Play mode, APK build, and headset test results. Mark tests that were not run explicitly.

## Before merging a setup change

- A teammate can open the branch using the documented editor version and Git LFS assets.
- There are no new script compilation errors or missing references in the starting scene.
- The intended scene is enabled in the shared build scene list.
- An Android APK builds successfully.
- Headset testing is recorded separately from build success.
- The PR includes required settings and assets, without unrelated package installs or generated files.
