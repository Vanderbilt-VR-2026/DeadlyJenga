# Direct Bluetooth nearby rooms — implementation checkpoint

Owner/task: bluetooth-nearby-approval. Branch: codex/bluetooth-nearby-approval. Target: origin/Evan_Sprint_4. Base: 1359ca474fb8a0bc86cdd72dca7dab5e3e631a71. Approved additive Core dependency: 6c6d5269ff40c68750808ec3dcbc7d844e2a78ba (core/nearby-room-contract).

GameSession/Session owns an Android BLE GATT server/client and persistent room service. Native code advertises only a service UUID; a room read returns an ephemeral room ID, player name, capacity and protocol. A client requests approval on a live GATT connection, polls its own response, and receives a Photon address only after approval. Requests expire after 60 seconds, approved responses after 15 seconds, and disconnect/room changes/disabled discovery clear them. Bluetooth code sharing does not block manual joining by someone who already knows the normal room code; display names are not authenticated accounts. BLE is not presented as encrypted identity verification.

The provider lives across lobby and game scenes. Presentation/Visuals adds the Home Bluetooth room browser, Room tablet approval/list pages, persistent name keyboard and a Wardrobe name shortcut. Existing desktop Room controls gain equivalent controls. Pending requests produce a short HUD notice. Automatic Wi-Fi room advertising is suppressed while Bluetooth discovery is enabled; existing explicitly requested Wi-Fi invites remain available.

Names are stored in local PlayerPrefs and survive restarts and in-place updates with the same package/signing identity. Uninstall, clearing data, different packages or another headset do not retain this local profile. The native path uses Android Bluetooth APIs, not Meta Colocation Discovery/account APIs. Sideload installation itself remains subject to headset OS setup requirements.

Validation so far: Java compiles against installed Android API 36. Source diff checks. Unity compilation, APK packaging/manifest/native-code inspection, actual rendered UI, real host/guest regression and Bluetooth radio tests pending. No Quest attached. Windows reports a Realtek Bluetooth adapter; role support has not yet been verified. A one-Quest/PC test can provide partial radio evidence, not final Quest-to-Quest proof.

Status: checkpoint-not-ready. No shared integration or hardware success claim. No package additions or SDK replacement. Generated-build settings changes stay in the leased test slot, not source.

## Native/offline validation update

Core, GameSession, Presentation and GameSession.Editor compiled successfully using the installed Unity Roslyn compiler and cached dependency references, with current owned source lists. A separate GameSession compile with Android defines passed, covering JNI/permission code. These checks are not Unity import/IL2CPP/APK or runtime proof. The check caught and corrected a uGUI Text property typo. Java API compilation passed.

Tools/BluetoothPeer builds successfully for Windows .NET 9. Adapter reports central/peripheral support; the actual GATT advertising probe aborted (Windows status Aborted, error Success), so PC-host support is NOT accepted. A 12-second scan completed without a matching headset; no remote radio exchange was tested. No driver or system permissions changed.

Tools/BluetoothProbe produces a separate diagnostic Android APK using the exact same native bridge. The initial diagnostic APK passed signature verification, all three Bluetooth-permission checks and DEX bridge inclusion; SHA-256 83f50ecfff4a6a68cd0de761aaf69da2e9bbc8b5c8f27c899d25dced61852150, 24,979 bytes. It does not include Unity UI or Photon, and has not run on Quest. Build scripts and one-headset test instructions are repository-visible under Tools.

Full Unity launch was refused by the two-task-editor admission limit. Initial manual job 9c86fb54505c43e98baf05a011ce460f was returned cancelled without an Editor launch. A transient external Unity CLI process also prevented check-in twice; later check-in succeeded without modifying or stopping another task. Full game APK, rendered UI and actual host/guest regression remain pending.

## Permission and menu follow-up

Resume the requested scan after the Android permission dialog, preserve disabled state after denial, and throttle native permission/configuration polling to five times per second. Desktop lobby Wardrobe can now open the name/Room tablet pages. Current source passed offline Core, GameSession, Presentation, GameSession.Editor and Android-defined GameSession compilation. These remain compilation checks, not gameplay tests.

Full APK attempt at dd75740b6f24c36eeac9d932e1b1612aeab9d089 crashed in Unity D3D12 asset import before the validation method ran. Its clean slot was returned; no full game APK was produced. A D3D11 retry is planned for this follow-up revision. Headset validation is deferred at the user's request.

