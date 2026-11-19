# Phase 3 results: world direction

Run through the coordinator: job `34cdab39` on commit `3608bd41` (first passed as job `4309fa48` on `3c323f74`; the numbers below are from that run, the linked report is the latest), batch
editor, map `sparse-viaduct-study`. Method
`StudioAcceptance.RunPhase3`. Full report: [evidence/phase3/report.md](evidence/phase3/report.md).

| # | Acceptance test | Result | What was measured |
|---|-----------------|--------|-------------------|
| 1 | Tornado cue with a set heading; identical playback | Pass | Asked for heading 90, first recorded position was at 90 degrees. 145 of 145 blocks moved. Two playbacks: 38871 samples each, worst 0.000 mm. |
| 2 | Push a block to land within 1 m of a mark, with a 20-try variation run | Pass | Plain push lands 1.60 m away. 8 of 20 tries land within 1 m, best 0.61 m. The original world stayed active until one was picked. |
| 3 | Branch at 3 s with one changed push | Pass | Frames 0 to 90 are byte-for-byte the old take. 0.0 mm offset at the branch frame, 15.8 cm move into the first new frame. |
| 4 | Ripple push across 30 blocks moves as a wave | Pass | 27 of 30 moved (3 are fixed parts). Farther blocks start later in 289 of 294 pairs. |
| - | Runtime errors | Pass | None. |

## Honest notes
- A cue placed exactly on the branch frame used to be dropped by a branch
  (review round 3). Fixed by reading the code; test 3's changed push is at
  3.3 s, so no test places a cue on the branch frame itself.
- A variation now keeps the cue times and strengths it used, and its take
  card can write them into the Set. Not exercised by a test.
- A cue exactly on the branch frame fires on the first new frame of the
  branch. That is one tape frame later than the same cue fires in a
  whole-set recording. Expected; not a bug to chase.
- "Put its cue timings in the Set" matches cues by their place in the list
  and only checks the count. If cues were reordered or swapped since the
  variation ran, the timings land on the wrong cues without a message, and
  pressing it twice strengthens the cues twice. Known and not fixed.
- The tornado's blocks and disaster state replay exactly, but its particles
  are drawn fresh each time, so two playbacks' pictures are not
  byte-identical.
- A branch taken while a disaster is already running does not carry the
  disaster into the new physics (decision in `DECISIONS.md`). Branch before
  the disaster starts.
- The heading control needed a Core addition (`IDirectedHazard`), listed in
  `CROSS-BOX-CHANGES.md`.

## Look at
- [tornado-live-00040.png](evidence/phase3/tornado-live-00040.png) and
  [tornado-playback-1-00135.png](evidence/phase3/tornado-playback-1-00135.png).
- [ripple-00012.png](evidence/phase3/ripple-00012.png),
  [ripple-00020.png](evidence/phase3/ripple-00020.png),
  [ripple-00024.png](evidence/phase3/ripple-00024.png): the wave moving out.

## After the fix agents
- A cue exactly on the branch frame is now tested (Cut suite, job
  `e314cec3`): the push fires on the first new frame; frames up to and
  including the branch frame are byte-for-byte the old take's; a variation
  run from that frame varies that cue.
- Variation recipes match cues by id and store the final strength and the
  seed, so applying one twice changes nothing. A try from a fresh map,
  recorded again as the whole set with its recipe, ends a mean of 1.4 mm and
  at most 41 mm from the try: close, not bit-identical.
- Found and fixed: the collapse cue ignored a variation's strength change.
