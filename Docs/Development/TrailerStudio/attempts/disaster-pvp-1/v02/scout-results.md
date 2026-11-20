# Capability scout and records 1-2 (stage 5 loop)

Order note: the capability scout ran before the plan because five of the six things the concept needs were
unverified in a Studio take (weapons on stand-ins, a knock-off, meteors drawn, aimed meteors, the planet killer).
No shot of the cut was rendered for judgement before the plan's verify list was answered.

## Scout (job 92cf957b, commit 8b4073d2, slot 4, about 6 min)
| Verify item | Result | Evidence |
|---|---|---|
| `fire` on stand-ins | FAIL then fixed: every shot "fired None" (equipment at spawn refused: the stand-in was not yet a match actor) | cue log |
| meteors drawn in playback | PASS: dark rocks with orange cracks and short trails; impacts recorded as contact frames | `v01/scout-job/*-sheet.png` |
| aimed meteors land on time | PASS: 0-3 frames early (sphere front touches first) | impacts list |
| meteors move things | FAIL: steep rocks pushed blocks down into the tower; nothing moved > 0.3 m; survivors 2 m away untouched | blocks-moved line |
| planet killer | PASS, spectacular, but 65 frames late: its default 3 s warning | impact f374, wide sheet |
| bot brains | unusable: both walked off the tower by f39 | take report |
| motion blur bug | cause found (below) | `colour-check-quad.png` |

## Motion blur finding (TOOL-BROKEN, fixed in DevTools)
Chest colour of the yellow hero at 1080p (9x9 patch): no blur (191,184,39); blur 0.3 (181,137,196);
blur at 540p (181,137,196); camera-only blur (190,183,34); blur with the cast's object/skinned motion vectors off
(190,183,34); blur without SSAO (181,138,196); blur with the body on URP Lit (181,137,196); 4 sub-frames (189,182,33).
The skinned body ghosted, the rigid hair on the same actor did not. Played-back survivors are posed from the tape,
so a skinned mesh's previous-frame pose is stale and URP's per-object vectors smear the whole body across the
background. Not resolution, SSAO, bloom or the shader. Fix: `StudioRender.NoSkinnedVectors` switches
`skinnedMotionVectors` off on playback actors in blurred shots (their transform motion still blurs). Verified at
preview size in records 1-2: solid yellow bodies, the punched rival blurs correctly.

## Record 1 (job 65368dc4, commit a82e3398)
Real glove punch (equipment command "fired BoxingGlove: True"), rival off the tower at f30; blaster, rocket and uzi
fired. The rocket's 8 m blast at f117 threw hero, blue, purple and cyan off together. Every body recorded yellow.
Planet killer contact f310 (planned 308).

## Record 2 (job a5b91792, commit aba720db)
No rocket. Planet killer contact **f308** (on the drop). Rocks coming across the tower top clipped the summit ring,
tipped the summit and the hero slid into the tower from f218. A playback colour override did nothing (removed).
Motion blur 0.35 smeared close rocks into worms. Cameras beside victims sat inside the lattice.

## Changes for record 3 (commit e7422414)
Rocks skim tangent to the tower onto the outer ledge; cast in game skins (Gorilla rival, Penguin, Dragon, Astronaut,
Fox, Panda; the hero plain yellow with spiky hair and the glove); blur 0.15; victim cameras outside the tower.
