# Physical VR weapon aiming

Owner: D4. Base: `8c03e7be22f89ff5bc4f5cf73317dabe818930b4`.
Task branch: `codex/physical-scope-aim-20261106`. Integration target: `origin/Evan_Sprint_4`, through the designated integration writer.

The user's explicit acceptance is automatic physical raising when AIM is pressed, not manual-only controller raising. Dominant controller grip remains the existing AIM binding; no grip is required simply to hold an equipped weapon. AIM now blends the actual rendered gun and appropriate hands to the centered eye line over 0.16 seconds. Releasing AIM blends back to the real controller/support-hand hip pose. Desktop right-click aiming remains available for supported ranged weapons. Menu, focus, traversal, pickup and emote gates prevent automatic aiming in those states.

For the three weapons with actual optics, the target grip is derived from the model's optic socket rather than a guessed grip offset or a one-eye lateral bias. The scope rear is placed 18 cm ahead of the tracked eye center; HMD pose and projection remain unchanged. The rifle/shotgun anchor is calculated from its own socket geometry at the selected carriage's settled rotation, so the receiver does not counter-rotate while its barrel assembly flips. Other ranged weapons retain their established raised aiming offsets, with the legacy one-eye bias removed. Support hands and muzzle calculations consume the same blended grip pose as the visible weapon.

The rifle eyepiece is 0.35 avatar units forward of the weapon grip, but the old scope activation range ended at 0.30. Even a wrist level with the head's forward plane could not enter that range. The physical sight gate now permits a practical forward wrist position, preserves a minimum face clearance, allows real human eye spacing independently of avatar size, and rejects lowered, rearward, distant, sideways and mispointed weapons. Magnification remains a centered binocular display; tracked HMD pose and projection are unchanged. The shotgun keeps its physical red dot and no magnifying lens. Scopeless guns retain their open-sight detection.

No Core, model geometry, camera pose, tracking calibration, muzzle calculation, network schema, package or project setting changes. Two-hand support and current model mounts remain unchanged.

## Checks

- Cached-reference Roslyn compilation of exact source Core, HandDemo.Runtime, Characters and Presentation, plus the new DevTools helper: passed. Existing Presentation warnings remain. This is not a Unity import or native Play Mode test.
- 27 directed physical-clearance and rejection cases passed by calling the actual freshly compiled `CandyGunSights.EyeBoxContains` from a .NET harness with UnityEngine managed vector math. This executes production math, not a Unity process or rendered sight test. The same cases are exposed by `WeaponPhysicalAimChecks.CheckEyeBox` for QA, at avatar scales 0.5, 1 and 1.5.
- `WeaponPhysicalAimChecks.Observe` is an explicitly invoked, read-only helper for QA's existing input/capture driver. It requires an actual living VR player, actual controller pointer pose, and a real rendered frame. AIM must be held in the raised phase and released in the hip phase. It checks automatic movement away from unchanged tracked hip input, actual optic centering and direction, rendered wrist/model-root agreement, physical sight alignment, and the live sight presenter's agreement; released AIM must restore the tracked hip pose. It returns JSON telemetry. It never injects input or moves the player/camera.
- Diff whitespace check: passed.

Native combined-source acceptance remains pending through the existing QA operator. Capture hip to AIM-button raised ADS to released/lowered motion while the controller's input pose remains at hip height, both rifle and shotgun modes, both handedness profiles, and support grip held. Controller-pose animation alone does not satisfy automatic raising. Allow the 0.16-second blend and 0.30-second mode carriage transition to settle before static anchor assertions. Inspect actual player sight views, exterior/side grip fit, physical optic line, muzzle direction and unobstructed target. Approximately six useful views plus one short motion sample suffice; do not repeat the old full weapon matrix. Bind reports/captures to the combined source actually loaded.

Physical Quest comfort and final APK inclusion are not established by source compilation or synthetic XR evidence. Old all-weapon ADS assertions are historical and do not prove this physical raising fix.

Earlier checkpoint `c482353d7b4c027a1b5d32f20220568ac2180b9c` is superseded: it removed head-locking for manual raising but did not satisfy the subsequent explicit automatic-raise answer. Do not apply that checkpoint alone as the final fix. No history was reset or overwritten.
