# Phase 4 results: performance polish

Run through the coordinator: job `6ee21374` on commit `3c323f74`, batch
editor, map `sparse-viaduct-study`. Method
`StudioAcceptance.RunPhase4`. Full report: [evidence/phase4/report.md](evidence/phase4/report.md).

| # | Acceptance test | Result | What was measured |
|---|-----------------|--------|-------------------|
| 1 | Retime 1 s of a walk to half speed; feet do not slide | Pass, with a caveat | Every slowed pose matches the original (0.0 mm). Body speed 1.61 to 0.80 m/s. After "Lock feet" the feet are 0.0 mm from the surface. See the caveat below. |
| 2 | A recorded block hits an actor and triggers a knockback | Pass | Thrown 1.25 m in the block's direction (alignment 0.96), back on the take at the end (0.0 mm off). The take was not changed. |
| 3 | Look-at override on 5 actors toward the tornado | Pass | 5 of 5 turned toward it, up to the 85 degree neck limit. |
| 4 | 20-person crowd from 3 takes, no two in step | Pass | 20 outfits, closest pair 1.06 m apart, 0 pairs move in step. |
| - | Runtime errors | Pass | None. |

## Caveat on test 1 (feet)
The test measures how fast the avatar's foot bones move along the ground.
That number follows the body's speed (1.66 m/s before, 0.80 m/s after), so
it shows the feet slow down with the body but does **not** prove a planted
foot stays still. The game's avatar steps procedurally from body speed, so
slowing the take slows the steps with it. Someone should watch
a retimed walk and judge it by eye.

## Not covered
- The crowd run was on `3c323f74`. The later change that keeps crowd
  members on ground (`04d5aca3`) has compiled and is covered by the same
  test, but that test has not been rerun since.
- Nothing here was seen in a headset.

## Look at
- [knockback-00045.png](evidence/phase4/knockback-00045.png),
  [knockback-00054.png](evidence/phase4/knockback-00054.png),
  [knockback-00066.png](evidence/phase4/knockback-00066.png).
- [look-at-005.png](evidence/phase4/look-at-005.png) and
  [look-at-030.png](evidence/phase4/look-at-030.png).
- [crowd-00020.png](evidence/phase4/crowd-00020.png).
