# Phase 4 results: performance polish

Run through the coordinator: job `90544549` on commit `97cdb6c0` (first passed as job `6ee21374` on `3c323f74`; the numbers below are from that run, the linked report is the latest), batch
editor, map `sparse-viaduct-study`. Method
`StudioAcceptance.RunPhase4`. Full report: [evidence/phase4/report.md](evidence/phase4/report.md).

| # | Acceptance test | Result | What was measured |
|---|-----------------|--------|-------------------|
| 1 | Retime 1 s of a walk to half speed; feet do not slide | Pass, with a caveat | Every slowed pose matches the original (0.0 mm). Body speed 1.61 to 0.80 m/s. After "Lock feet" the feet are 0.0 mm from the surface. See the caveat below. |
| 2 | A recorded block hits an actor and triggers a knockback | Pass | Thrown 1.25 m in the block's direction (alignment 0.96), back on the take at the end (0.0 mm off). The take was not changed. |
| 3 | Look-at override on 5 actors toward the tornado | Pass | 5 of 5 turned toward it, up to the 85 degree neck limit. |
| 4 | 20-person crowd from 3 takes, no two in step | Pass | 20 outfits, closest pair 1.06 m apart, 0 pairs move in step. |
| - | Legs move when frames are shown one at a time, and a frame looks the same however it is reached (added after review) | Pass | Shown frame by frame like a render, the feet moved 8.6 cm between two frames. The same frame reached by playing up to it or by jumping to it: 0.0 cm apart. |
| - | Runtime errors | Pass | None. |

## Caveat on test 1 (feet)
The test measures how fast the avatar's foot bones move along the ground:
1.59 m/s before the retime, 0.80 m/s after. After review the test must
measure the feet and they must slow to under 75% (it used to pass without a
measurement). The number still follows body speed, because the game's avatar
steps procedurally from how fast the body moves. So it shows the legs slow
down with the body; it does **not** prove a planted foot stays still. The
spec says "run"; the scripted performer walks. Someone should watch a
retimed walk and judge it by eye.

## Not covered
- Nothing here was seen in a headset.

## Look at
- [knockback-00045.png](evidence/phase4/knockback-00045.png),
  [knockback-00054.png](evidence/phase4/knockback-00054.png),
  [knockback-00066.png](evidence/phase4/knockback-00066.png).
- [look-at-005.png](evidence/phase4/look-at-005.png) and
  [look-at-030.png](evidence/phase4/look-at-030.png).
- [crowd-00020.png](evidence/phase4/crowd-00020.png).
