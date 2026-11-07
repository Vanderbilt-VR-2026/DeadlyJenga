# Physical VR weapon aiming

Owner: D4. Base: `8c03e7be22f89ff5bc4f5cf73317dabe818930b4`.
Task branch: `codex/physical-scope-aim-20261106`. Integration target: `origin/Evan_Sprint_4`, through the designated integration writer.

Holding the dominant grip previously replaced the tracked weapon position and its two-hand rotation with a fixed pose relative to the head. Consequently gripping a weapon prevented physically raising and steering it. VR now preserves the real dominant controller position and existing support-hand aiming. Desktop right-click aiming remains available for all supported ranged weapons.

The rifle eyepiece is 0.35 avatar units forward of the weapon grip, but the old scope activation range ended at 0.30. Even a wrist level with the head's forward plane could not enter that range. The physical sight gate now permits a practical forward wrist position, preserves a minimum face clearance, allows real human eye spacing independently of avatar size, and rejects lowered, rearward, distant, sideways and mispointed weapons. Magnification remains a centered binocular display; tracked HMD pose and projection are unchanged. The shotgun keeps its physical red dot and no magnifying lens. Scopeless guns retain their open-sight detection.

No Core, model geometry, camera pose, tracking calibration, muzzle calculation, network schema, package or project setting changes. Two-hand support and current model mounts remain unchanged.

## Checks

- Cached-reference Roslyn compilation of exact source Core, HandDemo.Runtime, Characters and Presentation, plus the new DevTools helper: passed. Existing Presentation warnings remain. This is not a Unity import or native Play Mode test.
- 27 directed physical-clearance and rejection cases passed by calling the actual freshly compiled `CandyGunSights.EyeBoxContains` from a .NET harness with UnityEngine managed vector math. This executes production math, not a Unity process or rendered sight test. The same cases are exposed by `WeaponPhysicalAimChecks.CheckEyeBox` for QA, at avatar scales 0.5, 1 and 1.5.
- `WeaponPhysicalAimChecks.Observe` is an explicitly invoked, read-only helper for QA's existing input/capture driver. It requires an actual living VR player, held dominant grip, actual controller pointer pose, and a real rendered frame. It checks tracked grip, rendered wrist/model-root agreement, physical sight alignment, and the live sight presenter's agreement. It returns JSON telemetry. It never injects input or moves the player/camera.
- Diff whitespace check: passed.

Native combined-source acceptance remains pending through the existing QA operator. Capture hip to raised ADS to lowered motion, both rifle and shotgun modes, both handedness profiles, dominant and support grip held. Inspect actual player sight views, exterior/side grip fit, physical optic line, muzzle direction and unobstructed target. Approximately six useful views plus one short motion sample suffice; do not repeat the old full weapon matrix. Bind reports/captures to the combined source actually loaded.

Physical Quest comfort and final APK inclusion are not established by source compilation or synthetic XR evidence. Old all-weapon ADS assertions are historical and do not prove this physical raising fix.
