# Survivor VR view and comfort settings

The camera always tracks physical head position and yaw/pitch/roll. Turning options affect the navigation origin, not the camera's local tracked pose. Opening a menu must not freeze looking. No camera smoothing, forced world-up correction of head roll, or head-yaw feedback into the origin is used.

## Controls

Left stick moves. Right stick turns. In the headset menu, choose **VR movement / turning**; desktop demo Settings also exposes these controls. Inspector values on `CastleSurvivor` work at runtime. Session changes are temporary; set prefab defaults in Edit Mode to persist them.

- Default: head-relative movement and 30° snap turn.
- **Stationary / seated**: body/rig-relative movement plus snap turn. Looking sideways does not redirect forward movement. Physical looking and leaning remain tracked.
- **Fluid / head-directed**: head-relative movement plus smooth turn (60°/second by default). The shared candy avatar already follows tracked head/body facing.
- **PhysicalOnly**: no artificial stick yaw; turn physically.
- Snap angle adjustable 15–90°. Smooth rate adjustable 15–180°/second. Snap requires stick release before another turn; a dead zone prevents drift.

Both presets allow physical looking at all times. Stationary means stable navigation controls, not a frozen VR camera. Smooth turn is optional; snap remains the default. Tilting the head does not introduce vertical movement. Joystick yaw pivots around the current eye position so leaning away from the origin does not make the eyes orbit.

## Implementation / diagnosis

The original survivor prefab listened only to `centerEyePosition`/`centerEyeRotation`. `CastleSurvivor.ConfigureHeadTracking` now repairs the existing runtime driver and future builder output with PassThrough actions supporting both the standard `devicePosition`/`deviceRotation` and center-eye controls. Quaternion value arbitration is avoided. The TrackedPoseDriver remains the only head pose writer and updates during input Update and BeforeRender. Invalid tracking holds the last valid pose; recovery resumes tracking. Resizing during VR no longer resets camera-local rotation.

Movement uses a projected horizontal basis selected by `vrMovement`. The locomotion root is never rotated to copy headset yaw; that would apply the physical turn twice. Climbing retains its wall-relative input, and vaulting, stamina, block collision isolation and hand tracking are unchanged.

Sources: [Unity TrackedPoseDriver](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/api/UnityEngine.InputSystem.XR.TrackedPoseDriver.html), [Unity XR Origin](https://docs.unity.com/en-us/engine/6000.6/manual/xr/configuring-project-for/scene-setup/origin), [official XR locomotion example](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/Documentation/LocomotionSetup.md). Installed Input System driver source was also inspected.

## Verification

Opt-in: write any text to `ArtDirection/VRViewChecks.request` with Unity in Edit Mode. `CastleVRViewChecks` injects synthetic HMD input through the actual Unity Input System and TrackedPoseDriver, checks head-only turning/leaning, both pose control paths, menu tracking, tracking loss/recovery, movement bases, snap/smooth pivot behavior and desktop switching. It then runs the seam/camera and climbing regressions. Reports: `VRViewChecks.txt`, `SeamCameraChecks.txt`, `ClimbChecks.txt`.

No physical headset was connected for these checks. Device-specific OpenXR tracking and comfort need an actual headset run.
