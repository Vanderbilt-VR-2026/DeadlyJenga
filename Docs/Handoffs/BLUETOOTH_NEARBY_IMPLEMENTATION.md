# Direct Bluetooth nearby rooms — implementation checkpoint

Owner/task: bluetooth-nearby-approval. Branch: codex/bluetooth-nearby-approval. Target: origin/Evan_Sprint_4. Base: 1359ca474fb8a0bc86cdd72dca7dab5e3e631a71. Approved additive Core dependency: 6c6d5269ff40c68750808ec3dcbc7d844e2a78ba (core/nearby-room-contract).

GameSession/Session owns an Android BLE GATT server/client and persistent room service. Native code advertises only a service UUID; a room read returns an ephemeral room ID, player name, capacity and protocol. A client requests approval on a live GATT connection, polls its own response, and receives a Photon address only after approval. Requests expire after 60 seconds, approved responses after 15 seconds, and disconnect/room changes/disabled discovery clear them. Bluetooth code sharing does not block manual joining by someone who already knows the normal room code; display names are not authenticated accounts. BLE is not presented as encrypted identity verification.

The provider lives across lobby and game scenes. Presentation/Visuals adds the Home Bluetooth room browser, Room tablet approval/list pages, persistent name keyboard and a Wardrobe name shortcut. Existing desktop Room controls gain equivalent controls. Pending requests produce a short HUD notice. Automatic Wi-Fi room advertising is suppressed while Bluetooth discovery is enabled; existing explicitly requested Wi-Fi invites remain available.

Names are stored in local PlayerPrefs and survive restarts and in-place updates with the same package/signing identity. Uninstall, clearing data, different packages or another headset do not retain this local profile. The native path uses Android Bluetooth APIs, not Meta Colocation Discovery/account APIs. Sideload installation itself remains subject to headset OS setup requirements.

Validation so far: Java compiles against installed Android API 36. Source diff checks. Unity compilation, APK packaging/manifest/native-code inspection, actual rendered UI, real host/guest regression and Bluetooth radio tests pending. No Quest attached. Windows reports a Realtek Bluetooth adapter; role support has not yet been verified. A one-Quest/PC test can provide partial radio evidence, not final Quest-to-Quest proof.

Status: checkpoint-not-ready. No shared integration or hardware success claim. No package additions or SDK replacement. Generated-build settings changes stay in the leased test slot, not source.
