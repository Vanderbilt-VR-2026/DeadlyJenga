# Trailer Studio Sound acceptance run

Date: 2026-11-18 18:56  
Unity: 6000.3.23f1  
Map: sparse-viaduct-study

| Test | Result | Detail |
|------|--------|--------|
| a. A whole-set recording keeps its sound with its world take | PASS | World take 4d3ed2f3: sound at audio/takes/4d3ed2f3/tape.wav, 192000 sample frames at 48000 Hz, 2 channels (expected 192000 = 120 frames / 30 fps). Loudest mix sample 18073, World stem (block impacts) loudest 18194, 7 stems. AudioFile points at it: True. Temporary recording folder left behind: False. Recording took 8.5 s. |
| b. A branch's sound is the old take's sound up to the branch frame, then new | PASS | Branch at frame 60 (first new frame 61, sample 97600). New take da355a77: 192000 sample frames (expected 192000). Samples before the branch identical to the old take's: True (195200 compared). After it, 188384 samples differ; the first difference is 2.033 s (the extra push is at 2.3 s). World stem joined the same way: True. Sound note: "". The branch with sound took 3.2 s. |
| c. Switching the active world take switches which sound is used | PASS | With take 4d3ed2f3 active the sound is 4d3ed2f3\tape.wav (take id 4d3ed2f3); with take da355a77 active it is da355a77\tape.wav (take id da355a77). |
| d. A performed take keeps what the performer heard | PASS | Ears used: "View Camera". Unity called the tap 188 times during the take (output 48000 Hz, batch mode True). Ada take 7cdc97de: sound 192000 sample frames (expected 192000), 2 channels, loudest sample 13652. Recorded 4.01 s of sound for 4.00 s of tape; the take took 4.3 s. Sound note: "". No stems for performed takes (one mix of everything the performer heard; game music muted while recording). |
| e. The export adds each performed take on its own lane and writes one mixed file (opening it in an editor was NOT done) | PASS | Two pieces: game sound under 2, performed sound under 2 (1 performed take heard; 0 once Ada's track is muted). cut-sound.wav: 166400 sample frames (expected 166400 for 52 frames at 15 fps), loudest sample 19168. Exporter notes: none. |
| Runtime errors during the Studio tests | PASS | None. |

Failures: 0
