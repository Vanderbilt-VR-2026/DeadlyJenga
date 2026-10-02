# HandDemo in the tracked DeadlyJenga project

Open Unity at C:/Users/evanp/Downloads/DeadlyJenga, then open Assets/HandDemo/Scenes/FlyingHandQuest.unity. The nested DeadlyJenga/ folder is an untouched recovery copy, ignored by Git.

The host uses Unity 6000.3.23f1, URP 17.3.0, Input System 1.20.0, XR Core Utilities 2.6.0 and OpenXR 1.16.1. All were already present; no package manifest was copied or replaced. HandDemo runtime code has no URP API dependency. Only its three materials were converted to the host's existing URP Lit shader.

Auto mode selects VR when an XRDisplaySubsystem is running and returns to desktop when none is running. An input-only HMD does not select VR. Desktop: WASD move, E/Q ascend/descend, mouse X yaws, mouse Y pitches the view; Escape releases the cursor and clicking Game captures it. VR: left stick moves, right stick X yaws, right/left triggers ascend/descend. Head pose remains independent of the physics hand.

The scene includes one XR Origin, one Main Camera, one AudioListener, a physics hand and test crates. Open it by itself; do not add its rig alongside another scene's rig. No EventSystem or custom tags/layers are required. Null collider materials intentionally use Unity defaults. Input actions are embedded.

The root retains its renderer, Linear color space, input settings, Android XR loader, ARM64/IL2CPP, GameActivity and Lobby build scene. See QUEST_CONFIGURATION.md for the small Android settings changes. To build this demo alone, select the HandDemo scene in a dedicated Build Profile rather than replacing the Lobby scene or its gameplay.

Tools > Flying Hand > Run Complete Validation opens the demo and runs desktop then simulated XR Play Mode probes. Save your current scene first, disconnect headset displays, and wait for Temp/HandDemoRootValidation.txt to end with COMPLETE; any FAIL requires investigation. Individual probes and the scene/prefab audit remain available in the same menu. Synthetic input cannot certify headset activation, rendering, device performance or comfort.

SRPs own XR rendering. Forced Desktop during a live SRP XR session does not disable headset output. Normal Auto without a running display uses desktop input and disables tracked pose driving. Camera obstacle avoidance is not implemented.

See ROOT_TRANSFER.md for transfer scope and root-project validation. Earlier validation records describe the nested/source projects and are historical, not proof of validation in the root.
