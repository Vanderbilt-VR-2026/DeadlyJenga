# Trailer attempt 1: findings

Made by an AI agent (Claude Opus 5.5) driving Trailer Studio from an editor script, with no mouse
and no live view. Branch `claude/trailer-attempt-1` (from `claude/trailer-studio-a` f1eff424),
in the worktree `deadlyjenga-claude-trailer-studio`. Driver script:
`Assets/_DeadlyJenga/DevTools/Editor/TrailerStudio/Attempts/TrailerAttempt1.cs`.
Flattening script (outside the Studio): `assemble.py` in this folder.

Final clip: `trailer-attempt-1.mp4`. 15.000 s, 1920x1080, 30 fps (450 frames), H.264 + AAC
48 kHz stereo. Loudness -15.1 LUFS integrated, peak -0.6 dB, mean -17.0 dB. The first 0.35 s is
silent (the Studio's recorded sound starts quiet). Frames: `frames/` (10 stills).

## 1. Concept and planned shot list

Concept: "Every block counts." A calm candy tower over the ocean, survivors on top, the hand pulls
a block, the top gives way and people fall, then a tsunami hits the castle. End on the title.

| # | Planned shot | Tape time | Camera |
|---|--------------|-----------|--------|
| 1 | Establishing: the whole tower over the ocean, slow push in | 0 to 3 s | keyed dolly, 18-20 mm |
| 2 | Hero on the top, sky behind, shallow focus | 3 to 5 s | follow, 32 mm, f/2.8 |
| 3 | The giant hand pulls a block | 5 to 7 s | ride on the hand / keyed on the block |
| 4 | The top collapses, survivors fall, slow motion | 7 to 9 s | follow, speed ramp to 0.3x |
| 5 | The tsunami hits the castle | 9 to 12 s | wide, low over the water |
| 6 | Aftermath and title | 12 to 14 s | high wide crane-out |

Sound: the game sound recorded with the take, plus a music bed, title over the last 2.3 s.

## 2. What the final clip actually shows

| Clip time | Shot | What is on screen |
|-----------|------|-------------------|
| 0.0-2.8 | A Establish | Wide over the ocean: purple spire on a red and green candy castle, islands behind, slow push in. Works. |
| 2.8-5.2 | B Edge | Survivor "Dev" standing on the edge of the top, seen from behind and to the side; another survivor jumps in the background. Readable. A big block fills the right third. |
| 5.2-8.6 | D Collapse | Wider view of the top with three survivors. A block is loosened and slid (it only moves 0.7 m, so it reads poorly), then the top tilts and blocks fall. Slows to 0.35x. No single impact is clear. |
| 8.6-10.2 | C Falling | Camera falls with Dev past purple blocks with the ocean behind. Another survivor appears see-through and grey. Good energy. |
| 10.2-12.6 | E Wave | Low angle on the castle as a tsunami rolls over it and tears the green towers. The strongest shot. |
| 12.6-15.0 | F Finale + title | High view: water over the castle, the lower spire still standing, wreckage. "DEADLY JENGA / Every block counts." fades in from 12.7 s (added outside the Studio). |

Not in the clip: the giant hand (F5, F6), a hero who survives, a clear "hand pulls block" beat.
Sound under shot D is at real speed while the picture is slow (F4).

## 3. Render iterations

Five rendered looks reached the end. One render was lost to a timeout, one to the PC restart and one to an editor that quit.

| # | Job | What it was | What I saw | What changed next |
|---|-----|-------------|------------|-------------------|
| 1 | d721983b | Scout + record + 960x540 render of 6 cameras in one run (493 s of the 600 s limit) | Map facts (56 m tower; spire of 4 blocks per level on a 25 m castle). Establishing shot cut off the top; hero shot buried in blocks; "pull" block never moved; the hand bot flew down and away; collapse and tsunami worked | Split record and render (time); cameras from measured geometry; loosen-then-push pull |
| 2 | 7e2d28d9 | New record take + 640x360 preview; take saved as a bundle | Establishing good. Hero shot a purple wall. In this take the hand only appears after 8 s (at 1 s in take 1); hand shot empty sky. Pull moved 0.7 m | Bundle committed; subject "Dev" picked from the motion log (camera-side edge, 2-7 s) |
| 3 | 9469d4f4 | First 1080p render of the committed take | Edge shot: subject's back behind a block. Hand shot: no hand drawn, only brown flat squares. Finale inside the wave | Low angle edge shot; ride on the falling subject instead of the hand; finale higher; music bed |
| 4 | 00756ae3 | 1080p | Fall ride good; finale better; edge shot still blocked | Edge shot moved to the collapse shot's angle (clear line) |
| 5 | 422719b1 | 1080p | Timed out at the 10-minute limit after 2 of 6 shots (same code took 489 s in run 4) | Render split into two runs with fixed camera ids |
| 6a | 8b7f5e35 | Render part 1 | Killed by the PC restart (21:48) | Resubmitted |
| 6a | 504c195c | Render part 1, warm editor | Editor quit after shot A (log ends in an OpenXR "System Shutdown" report; cause unknown) | Resubmitted as a fresh batch job |
| 6a | 07363ae2 | Render part 1 (A, B, D), fresh batch | Good | Shot records committed for part 2 |
| 6b | 0513849f | Render part 2 (C, E, F) + full export | Good; final cut assembled from both halves | Delivered |

Every render was looked at (contact sheets every 0.5 s, full-size stills where in doubt) and its
sound measured (volumedetect, EBU R128, per-stem levels).

## 4. Findings log

Classes: TOOL-MISSING, TOOL-BROKEN, TOOL-HARD-FOR-AI, AI-JUDGMENT, ENVIRONMENT.

### Output and the cut
- **F1 TOOL-MISSING: no finished-video export.** The Studio writes one PNG sequence (and MP4)
  per camera, an FCPXML, an EDL and `cut-sound.wav`, never the cut as one playable file. I wrote
  `assemble.py` (ffmpeg) to join the pieces listed by `StudioExport.Resolve`, re-implementing the
  dissolve rule from the EDL code. Wanted: "Export cut as MP4" (picture + mixed sound).
- **F2 TOOL-MISSING: no titles or text overlays** (DECISIONS 44). The end title was burned in
  with ffmpeg drawtext.
- **F3 TOOL-MISSING: no music in the mixed sound, no gains.** `set.music` only goes on an FCPXML
  lane; `cut-sound.wav` has no music and there is no per-lane or per-stem gain. The recorded music
  stem was about -40 dB RMS (World and Character stems peak near -10 dB). I mixed the game's own
  `Music_01.wav` at -6 dB and the cut sound at +6 dB in `assemble.py` (limiter, end fade), by
  meter, not by ear (F18).
- **F4 TOOL-BROKEN (known, warned): sound under a speed ramp is at real speed.** The 3.4 s
  slow-motion shot carries 3.4 s of real-time sound from its start. Wanted: ramp-following sound.
- **F5 TOOL-BROKEN: the recorded hand is not drawn in renders of this take.** The hand bot's take
  has positions from 8 s (StudioActorPoses finds them) and a `ride` camera aimed straight at the
  hand showed open sky with three flat brown squares near the centre. In take 1 a white glove was
  visible in a wide shot. I could not tell whether the playback copy is hidden, missing or tiny,
  and had no way to look at the live scene. This cost the trailer's key beat.
- **F6 TOOL-MISSING: the hand cannot be directed.** It is performed by a person or flown by the
  game's hand bot with its own brain. In take 1 the bot dropped steadily away and grabbed nothing;
  in take 2 it appeared only after 8 s. DECISIONS 28: no hand path with timed grabs.
- **F7 TOOL-HARD-FOR-AI: no feedback on whether a push will do anything.** A 7 m/s `push` on a
  spire block moved it under 0.5 m; `weaken` then 9 m/s moved it 0.7 m. Found out only after a
  full record run.
- **F8 TOOL-MISSING: cameras can only follow actors.** No aim at a block or moving point ("stay on
  the block that slides out"). Overrides accept `block:<index>`; cameras do not.
- **F9 TOOL-BROKEN (minor): a survivor renders see-through and grey** in playback renders with
  nobody performing (shots C and D). Looks like the ghost look or a material leaking. Not traced.

### Operating it without eyes
- **F10 TOOL-HARD-FOR-AI: nothing can be seen without a Unity round trip.** Marks and cameras were
  placed from numbers (block bounds, height bands, a motion log I printed from the tape). A person
  orbits the viewport and presses "Key here". Each framing question cost a coordinator job (5 to 14
  minutes); the edge shot took three renders to get a clear line past one block.
- **F11 TOOL-HARD-FOR-AI: no machine-readable account of a take.** I wrote my own (actor positions
  per second, blocks moved per second, who fell furthest, pull distance). It was the most useful
  thing I had. Wanted: an event list per take.
- **F12 TOOL-HARD-FOR-AI: takes are not repeatable, and nothing says so.** Same Set, seed and cues:
  bots moved differently and the hand appeared at 1 s in one take, 8 s in the next. The only fix
  was to commit one take and stop re-recording (`bundle/` on the branch).
- **F13 TOOL-MISSING: no "render this saved take" for batch use.** Tapes live in an ignored folder
  of whichever slot ran the job. I copied tape, sound and Set JSON into the repo and loaded them
  back by hand (`LoadBundle`). Wanted: take import/export and a batch render entry point.
- **F14 TOOL-HARD-FOR-AI: camera ids are random GUIDs.** Splitting a render over two jobs only
  worked after forcing fixed ids, because shot folders and the export match on id.
- **F15 TOOL-HARD-FOR-AI: the stage anchor is not the top.** "Highest block near the middle" put
  the anchor about 4 m below the crown, so marks and cameras relative to it landed between blocks.
  Seen only after printing a height profile.

### Creative limits (mine)
- **F16 AI-JUDGMENT: no hero survives and there is no clear human moment.** The subject was chosen
  from a motion log, not performed, and faces away in shot B. A person would perform the hero.
- **F17 AI-JUDGMENT: the collapse lacks a single readable impact** (a slow tilt seen from 11 m). A
  close shot of the first block hitting would be better; finding it needs scrubbing.
- **F18 TOOL-HARD-FOR-AI: I cannot hear.** Music level, sync and whether impacts land on cuts were
  judged from loudness numbers only. Nobody has listened to this mix.
- No motion blur (1 sub-frame): 2 sub-frames would roughly double render time, and the render
  already hit the 10-minute limit once.

### Environment
- **F19 ENVIRONMENT: the 10-minute test limit shapes the work.** Record plus 1080p render did not
  fit in one run. The same 1080p render took 465-489 s (runs 3, 4) and timed out in run 5 on a
  busier PC; the render had to be split in two jobs.
- **F20 ENVIRONMENT: the PC restarted at 21:48** during render part 1 (job 8b7f5e35), killing the
  job and my wait. The warm editor then needed 723 s to start again, and that resubmitted warm run
  (504c195c) quit after one shot for an unknown reason. Cost: about 35-40 minutes and two extra
  jobs. Nothing was lost on disk (scratch outputs from runs 1-4 survived; an interim mp4 from run 4
  was delivered first as a safety net).
- **F21 ENVIRONMENT (minor):** one request was refused with "Pool lock exists"; resubmitting a
  moment later worked. The compile precheck never gave a false error (6 prechecks).

## 5. What would most improve an AI's results with this tool (ranked)

1. **Fast preview stills from code**: stills per camera at chosen times in seconds, with "subject
   visible: yes/no, % of frame" (F10). Most iterations went to finding a clear line of sight.
2. **A take report**: cue results, blocks moved, actor heights over time, who fell, hazard and hand
   positions (F11).
3. **Export the cut as one MP4** with mixed sound, music lane, gains, fades and titles (F1-F3).
4. **A directable hand** (path with timed grabs on named blocks) drawn reliably in renders (F5, F6).
5. **Take packages and a batch render entry point**, stable camera ids (F12-F14).
6. **Camera targets beyond actors**: blocks, points, hazards (F8).
7. **Sound that follows speed ramps** (F4).
8. **Faster or resumable renders** that fit the test budget without hand-splitting (F19).
