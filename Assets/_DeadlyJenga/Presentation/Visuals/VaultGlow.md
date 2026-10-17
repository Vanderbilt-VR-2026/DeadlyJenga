# Vault ledge glow

The CastleSurvivor prefab carries CandyVaultGlow. It reads Core's IVaultTargetProvider and IMovementState; it never starts or changes a vault. Only the local player's reachable target is displayed. Grounded, paused, climbing and already-vaulting players do not get a jump-to-vault hint.

On the prefab, adjust CandyVaultGlow:

- Tint (including alpha), Brightness and Shimmer control the faint candy-blue finish.
- Edge Span is the highlighted length along the perimeter, in world metres. It continues around corners.
- Top Depth and Face Depth control the ribbon across the top and down the side.
- Surface Offset separates the ribbon from the surface; Fade Seconds controls appearing/disappearing.

On CastleSurvivor, Vault Detection Width controls the full sideways query span (five rays, centre first; zero restores one centre ray). Vault Reach is forward detection depth. Vault Landing Depth is the inset beyond the player's capsule radius. The same checks and dimensions are used for the actual vault, including body and path clearance.

The rim follows BoxCollider top boundaries, including rotated rectangular blocks. Unsupported collider shapes receive no outline rather than an inaccurate bounding-box outline. This is a surface ribbon with a soft alpha falloff and a bright edge, so it does not require scene bloom or a real-time light. It uses depth testing and stereo shader macros.

Editor batch validation: DeadlyJenga.CastlePrototype.Editor.VaultTargetValidation.RunBatch. Run in a dedicated Unity process, without -quit, when the project is not open elsewhere. It opens Game without saving it, enters Play Mode, checks vault queries and exits. Results: Logs/Validation/VaultTargetValidation.txt.

Headset readability, performance, and art alignment should still be checked on Quest 3.
