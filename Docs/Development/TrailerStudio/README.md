# Trailer Studio: spec

Trailer Studio grows Gameplay Theatre
(`Assets/_DeadlyJenga/DevTools/Editor/GameplayTheatre.*`) into a full tool for
staging, performing and filming scripted moments for Deadly Jenga trailers.
It uses only in-game content: production maps, characters, the hand, disasters,
weapons and effects.

Goal: one person can make any kind of trailer moment (huge collapses, crowd
escapes, hand ambushes, combat in the rubble, quiet beauty shots) and get it to
play out exactly as intended. The person performs each character in turn and
everything already recorded plays back around them.

## Files

For the current prompt-to-render workflow, two camera editing, and the human
comparison plan, see [Directing Deadly Jenga from a prompt](HUMAN-DIRECTION.md).

| File | What it covers |
|------|----------------|
| [README.md](README.md) | Vision, core ideas, glossary, rules for the builder |
| [UI.md](UI.md) | The interface: layout, modes, VR panel, design rules |
| [RESEARCH.md](RESEARCH.md) | How other studios and games do this, and what we borrow |
| [Creator workflows](CREATOR-WORKFLOWS.md) | Current capabilities, production references, and a prioritized plan for cast direction, camera coverage and editing |
| [PHASE-1-Sets-and-Tapes.md](PHASE-1-Sets-and-Tapes.md) | Sets, marks, the universal tape, playback, the Studio window |
| [PHASE-2-Performance-Passes.md](PHASE-2-Performance-Passes.md) | Layered recording as survivor or hand, desktop and VR, takes |
| [PHASE-3-World-Direction.md](PHASE-3-World-Direction.md) | Disasters, aimed destruction, physics branches, variation runs |
| [PHASE-4-Performance-Polish.md](PHASE-4-Performance-Polish.md) | Editing recorded motion, limb overrides, reactions, crowds |
| [PHASE-5-Cameras-and-Finishing.md](PHASE-5-Cameras-and-Finishing.md) | Camera tracks, VR camera, time remap, cinema render, export |
| [BUILDER-PROMPT.md](BUILDER-PROMPT.md) | The prompt to give the model that builds this |

Three builder chats build it (see [BUILDER-PROMPT.md](BUILDER-PROMPT.md)):
chat A builds Phases 1 and 2, chat B builds 3 and 5, chat C builds 4.
Every phase must pass its acceptance tests before the next one starts. Evan
reviews at the end of each chat; each phase's review stop lists what to show
him then.

Phase 1 must also leave **extension points** so later phases don't edit the
same files: a fixed tape format with room for new track types, and a way for
each mode tab, track type and inspector to register itself with the Studio
window.

## Core ideas

1. **Perform, don't program.** Moments are made by playing the game, not by
   typing coordinates. JSON stays as the save format, but the person never
   needs to edit it.
2. **Everything recorded is playback.** A recorded world or actor plays back
   exactly the same every time. Live things react to playback. Playback
   never reacts to live things, unless a pass is marked as live physics.
3. **Layers, like a music multitrack.** Each actor, the hand, the world and
   each camera is a track. Tracks can be muted, soloed, locked, re-recorded,
   punched in, given alternate takes or offset in time.
4. **Branch, don't restart.** Any recorded physics can be branched from a
   frame: keep everything before it, re-simulate after it with a change. The
   same goes for performances: punch in and redo the rest.
5. **Never lose a take.** Every recording is kept until the person deletes it.
   Undo works for every edit.
6. **Real game systems.** Cues go through the production gameplay paths
   (`RequestPunch`, equipment commands, `DemoDisasters`, `IGrabbable`), as
   Theatre does today, so what you film is what the game does.
7. **Simple surface, deep inside.** The window shows only what the current
   mode needs. Advanced settings are one click away, never in the way.
8. **Shoot cameras last.** Cameras are added over a finished tape and can be
   changed forever without re-performing anything.

## Glossary

- **Set**: a saved scene setup. Map, time of day, weather, marks, props,
  starting block state, cast list.
- **Mark**: a spawn point for one actor (position, facing, role, outfit).
- **Actor**: one survivor, the hand, or a bot in a Set.
- **Tape**: the recording of a Set over time. It holds a track per actor, a
  world track (all blocks and dynamic objects), hazard tracks, effect tracks,
  an audio event track and camera tracks.
- **Pass**: one recording session that writes one or more tracks while the
  rest of the tape plays back.
- **Take**: one attempt at a pass. A track can hold many takes; one is active.
- **Ghost**: an earlier recorded actor shown during a pass.
- **Branch**: a new version of the world track that matches the old one up
  to a frame and then re-simulates.
- **Shot**: a camera track with its own lens and moves.
- **Edit**: the order of shots and time ranges that make the final cut.

## Rules for whoever builds this

- Follow the repository `AGENTS.md`. Studio code lives in DevTools
  (`Assets/_DeadlyJenga/DevTools/`), which may read every box but nothing may
  depend on it.
- Changes in Core, Characters, World, Presentation or GameSession are
  pre-approved by Evan for this project: make them without stopping. Keep
  each in its own `[TrailerStudio cross-box]` commit and log it in
  `progress/CROSS-BOX-CHANGES.md`; Evan keeps or drops them at the end.
  Core changes still only ADD. Each phase file lists the changes it expects.
- Builder chats never stop to ask Evan anything mid-chat. They decide,
  log the decision in `progress/DECISIONS.md`, and keep going.
- Every Unity run goes through the Deadly Jenga Unity coordinator
  (`deadlyjenga-worktree-pool` skill). Never run Unity on the central
  checkout. Work on a task branch in a worktree.
- Keep scripts small and readable, with plain comments.
- Keep Gameplay Theatre recipes and takes loadable, or provide a one-click
  importer.
