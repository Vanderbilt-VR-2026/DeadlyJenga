# Survivor slope traversal

The shared `CastleSurvivor` motor now distinguishes foot support from full traction. Desktop, VR and externally driven survivors use the same movement implementation.

- Up to 38 degrees: normal traction, movement projected along the incline, grounded jumping.
- From 38 to 75 degrees: smoothly increasing downhill slide, progressively weaker uphill movement, retained sideways control and grounded jumping to escape.
- Above 75 degrees: no foot support or refreshed jump; ordinary falling and existing ledge vaulting apply.

Slide velocity accelerates rather than switching instantly, brakes on grippy ground and carries horizontal momentum briefly into a jump. Surface probes recover actual face normals around box edges. Moving block surface proxies remain the source of platform carry. These changes do not modify block rigidbodies, collision isolation, jump height, vault height or avatar physics.

Tune `slopeGripAngle`, `slopeSupportAngle`, `slopeSlideSpeed` and `slopeSlideAcceleration` on the survivor motor. Defaults are 38 degrees, 75 degrees, 7 m/s and 14 m/s². The maximum support angle also controls the CharacterController slope limit when the control mode is configured.

The opt-in editor harness `CastleSlopeChecks` exercises isolated 0/30/55/70/84 degree ramps through the actual CharacterController. Place `run` in `ArtDirection/SlopeChecks.request` in Edit Mode after compilation to run it; the report is `ArtDirection/SlopeChecks.txt`. Fixtures are discarded on exiting Play Mode and the authored scene is not saved.
