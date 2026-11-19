# Trailer Studio Phase5 acceptance run

Date: 2026-11-18 18:54  
Unity: 6000.3.23f1  
Map: sparse-viaduct-study

| Test | Result | Detail |
|------|--------|--------|
| Setup: a finished tape to film | PASS | 120 frames, two actors, a collapse at 1.0 s. |
| 1. A five-camera shoot over one tape renders every camera; rendering again gives identical frames | PASS | 5 cameras (Wide 60 frames, Follow Ada 60 frames, Ride Ben 60 frames, Orbit 60 frames, Two shot 60 frames); expected 60 frames each at 15 fps. 300 pictures compared byte for byte between the two renders: 0 differ. 5 of 5 cameras show a different middle frame. Ocean and sky were held still for this check (with them moving, two renders differ in the water and clouds only). Pictures: render-1/, render-2/. |
| 2. A speed ramp to 0.25x and back is smooth (no stepping) | PASS | The 4 s tape plays for 7.70 s of film (231 frames). Playback speed goes from 1.00x down to 0.25x and back, always forward; the largest change of speed from one frame to the next is 0.037x (a step would be 0.75x). In-between frames are real: block #59 moves 32.7 cm between tape frames 42 and 43, and at 42.5 it is 0.00 mm from the halfway point. Rendered with 4 in-between frames per picture for motion blur: 230 pictures. Pictures: render-ramp/. |
| A blurred render is as bright as a plain one | PASS | Average brightness of the first Wide frame: 129.0 of 255 with no in-between frames, 129.0 with 2. |
| 3. A handheld camera take records and plays back as an editable track (no headset in this run) | PASS | A camera pose was fed in every tape frame while the tape played in real time, the way a tracked controller or the desktop fly camera feeds it. The take became 21 keys on "Handheld" covering 0.00 to 3.97 s; played back, it stays within 1.8 cm of the path that was performed. Raising one key by 2 m moved the camera there by 2.00 m. NOT covered here: reading a real VR controller and the in-headset viewfinder, and flying with mouse and keyboard; both need a person. |
| 4. The cut exports as FCPXML and EDL with shots and sound in place, and both pass the format checks (opening them in Resolve was NOT done) | PASS | FCPXML is well-formed XML with 3 shots on the main storyline, 1 cross dissolve, game sound under all 3 shots, sequence length 74/15s; all 4 media files it points at exist (0 missing). FCPXML structure and timing check: no faults. EDL has 3 events on 4 lines (the dissolve is a cut line and a D line); CMX 3600 check: no faults. The tape's sound was recorded: 750 KB with 7 stems; loudest sample 14726 of 32767; block impacts (World stem) loudest sample 14745. Notes from the exporter: none. This sound was recorded during the live whole-set recording, not during a playback. Application focused: False. NOT run: importing the file into DaVinci Resolve or Premiere. That needs one of those apps and a person; the files are in render-1/. |
| Runtime errors during the Studio tests | PASS | None. |

Failures: 0
