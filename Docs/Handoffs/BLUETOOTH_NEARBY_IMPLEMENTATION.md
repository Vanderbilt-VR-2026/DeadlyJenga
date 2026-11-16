# Direct Bluetooth nearby rooms - implementation and validation

Source branch: codex/bluetooth-nearby-approval. Integration target: origin/Evan_Sprint_4. Base: 1359ca474fb8a0bc86cdd72dca7dab5e3e631a71. Approved additive Core dependency: 6c6d5269ff40c68750808ec3dcbc7d844e2a78ba (PR #169). Feature PR #170 is stacked on that contract. Tested implementation: ac9476ccb108a9cd9749a3586237d5d111747be7, verified pushed. No shared integration or headset acceptance.

## Behavior

GameSession/Session owns an Android BLE GATT server/client and persistent room service. Advertising contains a service UUID; discovered metadata contains an ephemeral room ID, player name, capacity and protocol, never the Photon address. A connected requester receives its code only after host approval. Pending requests expire after 60 seconds; approved responses after 15 seconds. Disconnect, room change and disabled discovery clear requests. This gates Bluetooth code sharing, not manual joining by someone who already knows the regular room code. Names are not authenticated accounts; no encrypted-identity claim is made.

Presentation/Visuals provides Home room browsing, Room tablet approve/deny, a pending-request notice, and a Wardrobe shortcut to the name keyboard. Discovery can remain enabled during a round. Automatic Wi-Fi advertisement is suppressed while Bluetooth nearby is enabled; explicitly requested Wi-Fi invites remain available. Permission denial persists the disabled state; granting permission resumes the requested scan. JNI/configuration polling is bounded to five times per second.

PlayerPrefs stores the chosen name. It survives restart and compatible in-place APK updates, not uninstall/data clearing, a different package or another device. Direct Android APIs do not use Meta Colocation Discovery/account APIs. Sideload installation still depends on headset setup. Gameplay still uses Photon connectivity; Bluetooth shares the room address.

## Actual checks

- Offline Unity Core/GameSession/Presentation/GameSession.Editor compilation passed; separate Android-defined GameSession compilation passed. Native Java and Windows helper compile.
- Actual Unity validation passed all 10 profile/import/contract checks, including Android plugin inclusion and no code field in discovered room rows.
- Desktop Play Mode at ac9476ccb108a9cd9749a3586237d5d111747be7 passed all 30 NearbyUiChecks assertions with zero runtime errors during checks and successful cleanup. Production Home and tablet UI components used a clearly labeled simulated provider/session. Commands were invoked through UI bindings, not physical input. Cases cover scan-on-open, room ID selection, paging, cancellation, empty rows, notification text, approve/deny targeting, keyboard edits, save, recreated-provider name reload and cancel. The fixture restores the original preference and scene state.
- Three actual Unity-rendered captures were inspected. The first run exposed Home buttons overlapping the tagline; f088048a fixes their layout and adds a bounds assertion. The first fixture cleanup failed restoring an empty initial scene setup; ac9476cc fixes that, and the corrected run completed successfully. First-run partial passes are not substituted for the corrected result.
- A full Quest APK at 88a5fa002ecb6e8de0b4c1849b6113ab851ab562 built with Unity 6000.3.23f1: zero errors, 656 warnings, 297489563 bytes. Final APK inspection verified SCAN/ADVERTISE/CONNECT permissions, native Java DEX bridge inclusion, signature v2, com.deadlyjenga.quest3 versionCode 20261115, arm64-v8a, min API32 and target API35. SHA256: 8c93e119613a5346b4edf08911903e225a6b9d89945515600eb367a67bf3e4de. This artifact predates the Home layout fix.
- The corrected full APK at ac9476ccb108a9cd9749a3586237d5d111747be7 succeeded: 0 errors, 654 warnings, 297510183 bytes; SHA256 a5e8102a0d2ef4559095977846574c61ea04595af9f967f0eae45cbe83a907eb. Actual APK inspection passed Bluetooth permissions, native bridge DEX inclusion, signature, package/version and ARM64/API32-35 checks. The merged manifest has one optional BLE feature entry. The owned test slot was restored clean and returned. This supersedes the older APK above. Sevaan hit LFS disk exhaustion before Unity launch and explicitly held the job; one HP fallback reused the existing Android cache. No duplicate active build.

The Windows adapter reports BLE central/peripheral roles, but its actual advertising probe aborted. A 12-second scan found no matching headset. No radio exchange passed. The separate 24979-byte Bluetooth diagnostic APK passed packaging/signature checks (SHA256 83f50ecfff4a6a68cd0de761aaf69da2e9bbc8b5c8f27c899d25dced61852150); it includes the same native bridge but no Unity UI or Photon. Reproduction tools are in Tools/BluetoothProbe and Tools/BluetoothPeer.

## Limits and readiness

Checkpoint only. No Quest runtime, OS permission interaction, physical VR controller input, full lobby/Wardrobe journey, independent Photon host/guest regression, or Quest-to-Quest Bluetooth test has passed for this candidate. The name check recreates the provider and reads the saved preference; it is not an uninstall/update or separate-process persistence test. Headset tests were explicitly deferred by the user. No package additions or production scene/settings changes. No additional Core contract requested.

Before integration, run the required solo Game scene and independent two-player networking regression, then the deferred device tests. APK inclusion and desktop UI evidence do not substitute for those acceptance gates.
