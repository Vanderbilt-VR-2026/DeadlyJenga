# Trailer Studio Phase1 acceptance run

Date: 2026-11-18 18:57  
Unity: 6000.3.23f1  
Map: sparse-viaduct-study

| Test | Result | Detail |
|------|--------|--------|
| Set built | PASS | sparse-viaduct-study: 145 blocks, 3 marks, collapse cue at 1.0 s, 180 frames at 30 fps. Mark warnings: Bot 1=ok, Bot 2=ok, Survivor 1=ok |
| 1a. Record the Set | PASS | World take 180/180 frames; actor takes 180/180/180; tape file 209 KB (1.2 MB before compression). Cues: 1.000s collapse pushed 10 blocks; 2.000s push 1 blocks at 3.6 m/s |
| 1b. The collapse was recorded | PASS | 11 of 145 blocks moved more than 10 cm between the first and last frame. |
| 1c. Playback 1 of 3, drift check | PASS | Drift check: 26070 samples, worst 0.000 mm and 0.0000 degrees. OK. |
| 1c. Playback 2 of 3, drift check | PASS | Drift check: 26070 samples, worst 0.000 mm and 0.0000 degrees. OK. |
| 1c. Playback 3 of 3, drift check | PASS | Drift check: 26070 samples, worst 0.000 mm and 0.0000 degrees. OK. |
| 1d. Review pictures: live recording next to playback | PASS | 90 side-by-side frames in compare/ (live left, playback right). Largest average colour difference between a pair: 0 of 255. |
| 2. Scrub to 10 random frames | PASS | Frames 7, 9, 65, 68, 118, 121, 151, 153, 156, 171. 1446 block poses compared with the live recording: worst 0.802 mm, 0.0000 degrees. 30 actor positions compared: worst 0.0 mm. |
| 6a. A track type added in its own file is recorded, saved and played | PASS | ExampleNoteTrack.cs only. Saved take has 180 frames; scrubbing to frame 12 showed "frame 12: 0 blocks awake". |
| 6b. Readers skip and keep track types they do not know | PASS | Loading only the world gave 1 track; a tape with an unknown type "made-in-a-later-phase" reloaded and re-saved with 180 frames intact. |
| 6c. A mode tab added in its own file shows up in the Studio | PASS | ExampleNotesMode.cs only. In the mode registry: True. Window tabs: Set, Perform, Polish, Direct, Shoot, Cut, Notes. |
| 3. Live survivor rides a played-back moving block | PASS | Block #58 moved 0.70 m in the tape with Survivor 1 standing on it. In the live recording Survivor 1 was carried 0.71 m (102% of the block, need 60%). Standing there again while the world only played back, Survivor 1 was carried 0.71 m (102%, need 60%); the two paths are never more than 0.07 m apart (limit 0.21 m, 180 frames compared). Pictures: rider/. |
| 4. A Theatre v4 take loads and plays | PASS | Theatre's own writer saved 180 frames (1.6 MB); converted to v5 with 8 tracks and 180 frames; played back with: Drift check: 26070 samples, worst 0.000 mm and 0.0000 degrees. OK.; 3/3 recorded survivors shown; blocks within 158.3 mm of the live run at a sample frame. |
| Runtime errors during the Studio tests | PASS | None. |
| 5. Game.unity still plays solo with the Studio closed | PASS | Session and map started (568 blocks), local survivor active: True, ran 240 frames, no Studio objects in the scene: True, exceptions: 0 |

Failures: 0
