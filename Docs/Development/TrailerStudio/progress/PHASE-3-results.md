# Phase 3 results: world direction

Run through the coordinator: job `4309fa48` on commit `3c323f74`, batch
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
