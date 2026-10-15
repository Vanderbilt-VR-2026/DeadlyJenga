# Simple VR avatar

An original, minimal avatar inspired by the project's colorful rounded characters: blank head, tapered floating torso, and two mitten hands. No legs, face, fingers, textures, arm IK, or animation skeleton.

## Files

- `SimpleVRAvatar.blend`: editable Blender source. The first scene contains the avatar only; `ColorPreview` contains three color examples and studio lighting.
- `AvatarPreview.png`: rendered color examples, not a game texture.
- `../../Assets/Avatars/SimpleVRAvatar/SimpleVRAvatar.fbx`: the game model, exported at meter scale, with separate head, torso, and wrist-pivot hands.
- `../../Assets/Avatars/SimpleVRAvatar/SimpleVRAvatar.prefab`: Unity prefab, using one shared URP material and `SimpleAvatarView`.
- `mesh_stats.json`: model complexity. The avatar has 6,260 triangles across four meshes.

Keep Blender source outside `Assets` so teammates do not need Blender installed to import the Unity project. The FBX is the game asset. Studio lights, cameras, preview copies, and floor are excluded from the FBX.

## Try it in Unity

1. Drag `SimpleVRAvatar.prefab` into a test scene. If the prefab has not been generated yet, use **Tools > Deadly Jenga > Create Simple Avatar Prefab**. The menu preserves an existing prefab.
2. On `SimpleAvatarView`, assign **Head Target** to the XR camera and **Left/Right Hand Target** to the appropriate tracked controller transforms.
3. For the owning player's view, enable **Local View** to hide their own head and torso. Use the mitten hands or the existing controller visuals, not both at once.
4. Set **Player Color** per instance or call `SetPlayerColor(Color)`. This uses renderer property blocks, so changing one avatar does not recolor every player sharing its material.
5. Enter Play mode. Head and hands follow their targets; the torso follows head position and horizontal heading while remaining upright. Missing targets leave the corresponding parts in their modeled poses.
6. Calibrate hand position/rotation offsets against the actual controller poses on the headset. The mesh pivots are wrists, whereas controller poses may originate at a grip or aim point. This is a visual asset, not a replacement for XR colliders or interaction components.

The torso follows headset yaw as a simple starting approximation. No body tracking or arm/leg solver is required. Use unit scale on the avatar and tracking rig.

## Lobby integration

`Assets/Scenes/Lobby.unity` contains a `Lobby Avatar Setup` object. It binds the visual prefab to the local XR camera and controller visual transforms. The local player sees mitten hands immediately, before joining a room; their own head and torso are hidden to avoid camera clipping. Original controller meshes are hidden while the setup is active and restored when it is disabled. Interaction rays, colliders, input, and teleportation components remain in place.

`LobbyNetworkAvatar.prefab` is a separate network player asset with the existing Fusion root/hand transforms and a tracked head transform. `PlayerSpawner` chooses this prefab only when the current scene has `LobbyAvatarScene`. Main has no such component and continues to use the original player prefab. The original network player prefab and Main scene are unchanged.

`HandPoseInput` now includes headset position and rotation. `LocalHandInput` collects them, and the optional head reference on `NetworkHands` applies them on the Lobby prefab. Remote visual instances follow the replicated transforms; they never bind to another player's local hardware rig. All players must use this version of the project together because the network input layout and prefab table have changed.

Colors are assigned consistently from Fusion's player ID using the six-color palette on `Lobby Avatar Setup`; colors repeat after six IDs. This is automatic assignment, not a color-selection UI. Change the palette in the scene, or change the hand-fit offsets on `SimpleVRAvatar.prefab` after checking controller alignment on the headset.

## Validation and headset check

Unity 6000.3.23f1 imported the model, compiled the scripts, and completed Fusion code generation. **Tools > Deadly Jenga > Validate Lobby Avatar** checks model scale/handedness, pose following, upright torso, fit offsets, independent colors, visibility, scene references, and the network prefab bindings.

**Tools > Deadly Jenga > Run Lobby Avatar Smoke Test** runs an offline Play-mode test, then exits Play mode. It passed local visibility and hardware input checks, registered-prefab spawning, simulated Fusion head/hand input, remote visuals, despawning, and controller visibility restoration. The test does not create an online room. Fusion can print startup warnings for the deliberately scene-less test runner.

Not yet verified on hardware: wrist fit, perceived size, network interpolation between two physical headsets, and Quest frame rate. Build the same revision for both devices, host/join the Lobby, check each other's head/hands and distinct colors, then start the game and confirm Main still uses its original player. No APK build or two-device test was performed for this change.

## Editing and exporting

Edit the four named meshes in Blender, preserving their origins and names. Keep one material slot (`PlayerColor`). Export only `Avatar_Model` as FBX with meter units, Y up / -Z forward, no animation, and mesh/empty objects only. The supplied generator uses baked axis conversion for these unskinned meshes.

`build_avatar.py` reproduces the source model, FBX, stats, and preview when run through Blender in background mode. It overwrites those generated files, so preserve any manual modeling edits before rerunning it. The Unity prefab remains unchanged.

Blender generation, render review, finite geometry checks, and triangle-budget checks also passed. The FBX is approximately 125 KB; no textures or animation clips are required.
