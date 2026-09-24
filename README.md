# DeadlyJenga

A multiplayer VR project targeting Meta Quest. The current starting scene is `Assets/Scenes/Lobby.unity`.

See [Project context](PROJECT_CONTEXT.md) for the proposed goals, gameplay, and scope. That document describes the design proposal, not a list of implemented features.

## Build a Quest APK

1. Build an **APK**, with **Build App Bundle** and **Export Project** disabled. Choose **Build** to create a file for later installation; **Build And Run** additionally requires a connected device.
2. Save the output under `Builds/`, for example `Builds/DeadlyJenga.apk`. This directory is ignored by Git.
3. Install and test the APK on the target headset. Verify launch into Lobby, head tracking, controller tracking, and any movement/interactions included in your change.
