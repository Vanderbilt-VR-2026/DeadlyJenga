# Trailer Studio Phase3 acceptance run

Date: 2026-11-18 13:53  
Unity: 6000.3.23f1  
Map: sparse-viaduct-study

| Test | Result | Detail |
|------|--------|--------|
| 1. A tornado cue with a set heading tears through the tower; playback is identical every time | PASS | Disaster "Tornado" with Height=65m, Funnel diameter=16m, Ferocity=40m/s², Pull-in distance=28m, Warning time=0s, Duration=9s. Cue log: 0.200s hazard Tornado from heading 90 with its own settings. The hazard track holds 264 frames of disaster state; 140 of 145 blocks were moved. Asked to come from heading 90 (the +Z side); its first recorded position was 90 degrees around the map centre. Two playbacks: Drift check: 39090 samples, worst 0.000 mm and 0.0000 degrees. OK. / Drift check: 39090 samples, worst 0.000 mm and 0.0000 degrees. OK. Blocks and disaster state come from the same tape both times; the saved pictures' sizes were not byte-identical (the disaster's particles are drawn fresh each time). Pictures: tornado-live/, tornado-playback-1/, tornado-playback-2/. |
| 4. A ripple push across 30 selected blocks moves as a wave | PASS | 30 blocks picked, 30 moved (the rest are fixed parts of the structure that never move). Cue log: 0.500s push 30 blocks at 5 m/s, rippling over 2.65 s. Farther blocks started later in 390 of 398 pairs more than 1 m apart; first to last block started 2.67 s apart. Pictures: ripple/. |
| 3. Branch at 3 s with one changed push; the frames before 3 s are unchanged | PASS | New take "Branch at 3.00s": 150 frames. Frames 0 to 90 are byte-for-byte the old take's (91 checked). The two takes first differ by more than 5 mm at frame 91 (the new push is at 3.3 s = frame 99). At the branch frame the largest block offset from the old tape is 0.0 mm, and the largest move into the first new frame is 18.8 cm (no pop at the hand-over). Parent recorded: True; the old take is still on the tape; cue log: 3.300s push 2 blocks at 12 m/s. |
| 2. Push a block at 1.5 s so it lands within 1 m of a mark, found with a variation run of 20 tries | PASS | Block #58 pushed at 1.5 s. The plain push lands 1.60 m from the mark. 20 tries (each branching at 1.4 s, force changed by up to 35%): distances from the mark, best first: 0.60, 0.60, 0.64, 0.65, 0.72, 0.78, 0.83, 0.88 m… 8 tries landed within 1 m. The run left the original world active until one was picked: True. The best try was then made the active world with one call. 20 tries have thumbnails. |
| Runtime errors during the Studio tests | PASS | None. |

Failures: 0
