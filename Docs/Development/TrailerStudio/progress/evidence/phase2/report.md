# Trailer Studio Phase2 acceptance run

Date: 2026-11-18 07:53  
Unity: 6000.3.23f1  
Map: sparse-viaduct-study

| Test | Result | Detail |
|------|--------|--------|
| Setup: world recorded | PASS | 145 blocks, 180 frames. Cues: 4.500s collapse pushed 10 blocks |
| 1a. Record actor A through the local player (scripted run, jump, climb) | PASS | Ada take: 180/180 frames, moved 10.95 m, left the ground: True, highest point 1.82 m above the mark, climbing state seen: False (no climbable wall within 3.5 m of the mark, so the script could not climb). |
| 1b. Record actor B while A plays back; A matches its take | PASS | Ben take: 180/180 frames. Ada's played-back position was compared on 180 frames: worst 0.000 mm from her take, worst 0.0 mm from where she actually stood when recorded. Ada's take was not changed: True. Pictures: two-actors/. |
| 3. Punch in at 2.0 s on a 6 s take; the join does not pop | PASS | New take 180 frames from frame 0. Before 2.0 s it equals the old take (largest difference 0.00 mm). Largest move in one frame: 31.2 cm before the join, 33.3 cm across it. After the join the performance is new (ends 9.24 m from the old ending). The old take is still there: True; takes on the track: 3. |
| Takes: comp from two takes at 3.0 s | PASS | Comp "Comp: Take 3 then Take 2": 180 frames, now active; source takes untouched (4 takes on the track). |
| 2. Twelve actors (two performed, ten cloned) play back | PASS | 12 actor tracks with takes; 12 actors on screen at once. A real-time playback of the 6 s tape took 6.17 s in this batch editor. No slow-playback warning was needed. Pictures: twelve/. |
| 5. Live-physics pass re-records the world; older survivor takes get the stale badge at the first difference | PASS | World takes: 2. Hand bot take: 180 frames, the hand travelled 13.7 m. Comparing the two runs block by block, they first differ by more than 12 mm at frame 76; the Studio reported frame 76. Ada's badge: "Stale: the world was re-recorded and first differs at 2.53 s (frame 76). Keep it, re-record it, or punch in from there.". Tracks flagged stale: 12. |
| Slow-motion performing: a jump at half world speed | PASS | Jump height 1.82 m at real speed and 1.82 m at half speed. Both takes are stored at real speed: 91 and 91 frames for the same 3 s. Climbing at reduced speed was not exercised by this script. |
| 4. A pass records head and hands; the wrist panel works (no headset in this run) | PASS | Recorded frame holds a head 0.80 m above the feet and two separate hand poses (left (1.09, 18.70, 0.20), right (1.32, 18.70, 0.20)), so tracked head and hands are stored as they are. Wrist panel built with 5 keys; touching the slow-motion key changed the speed: True. NOT covered here: the XR simulator and a real headset. This batch editor has no XR session, so the rig ran in desktop mode with the VR panel forced on. |
| Runtime errors during the Studio tests | PASS | None. |

Failures: 0
