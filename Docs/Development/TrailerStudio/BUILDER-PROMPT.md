# Prompts for the builder chats

Three builder chats, in this order:

| Chat | Builds | Starts when | Ends with |
|------|--------|-------------|-----------|
| A | Phase 1, then Phase 2 | now | Evan's review of Phases 1 and 2 together |
| B | Phase 3, then Phase 5 | Evan approves chat A's work | Evan's review of Phases 3 and 5 |
| C | Phase 4 | Evan approves chat A's work (may run alongside B) | Evan's review of Phase 4 |

Inside a chat, a phase must pass all its acceptance tests before the next
phase starts. Evan reviews at the end of each chat, not between phases in
the same chat. B and C both start from chat A's approved branch. If they run
at the same time, they must keep to their own files and plug into the
Studio window and tape format through the extension points Phase 1 created.

Copy everything below the line into a new chat, filling in `<CHAT>` and
`<PHASES>` from the table (for example `A` and `1, then 2`).

---

You are building **Trailer Studio** for Deadly Jenga, a Unity 6.3 URP VR game.
Trailer Studio grows the existing Gameplay Theatre tool into a full studio
for performing and filming scripted trailer moments using only in-game
content.

You are builder chat **<CHAT>**. You build **Phase <PHASES>**, in that order,
in this one chat.

## Read first, in this order
1. `AGENTS.md` (repository rules: boxes, Core, lanes, git) and
   `Assets/_DeadlyJenga/DevTools/AGENTS.md` if present.
2. `Docs/Development/TrailerStudio/README.md`, `UI.md`, `RESEARCH.md`,
   and this file's table above.
3. The phase files you build, plus all earlier phase files for context.
4. The existing tool: `Assets/_DeadlyJenga/DevTools/Editor/GameplayTheatre.cs`,
   `GameplayTheatre.md`, `TheatreMotionTape.cs`, `TheatreParticleTape.cs`,
   `TheatreAudioCapture.cs`, `TheatreStuntImport.cs`, and the recipes in
   `GameplayTheatreRecipes/`.
5. If you are chat B or C: `progress/` notes and results from earlier
   chats, and the approved branch they name.

## How to work
- **Never stop to ask Evan anything until the chat is finished.** Make
  reasonable decisions yourself, write them down in `progress/DECISIONS.md`,
  and keep going.
- Before writing code for each phase, write a short plan in
  `Docs/Development/TrailerStudio/progress/PHASE-<n>-plan.md`: the files you
  will add or change, the data formats, the order of work, and every change
  outside DevTools with its signature.
- **Changes outside DevTools (Core, Characters, World, Presentation,
  GameSession) are pre-approved by Evan for this project.** Make them
  yourself; don't wait. This replaces the AGENTS.md "stop and report" and
  "keep Core changes uncommitted until approved" steps for Trailer Studio
  only. The other AGENTS.md rules still apply: Core changes only ADD (never
  rename or change existing members), boxes only use lower boxes, gameplay
  doesn't draw UI, and Game.unity must still play solo.
- Put every change outside DevTools in **its own commit** (no DevTools
  files mixed in), with a subject starting `[TrailerStudio cross-box]`, and
  add it to `progress/CROSS-BOX-CHANGES.md`: commit hash, box and files,
  what was added, why, and what breaks in the Studio if it's dropped. Evan
  decides on all of them at the end.
- Work on your own task branch in a git worktree (chat B and C branch from
  chat A's approved branch). Never edit or run Unity on the central checkout.
- Every Unity use (compile check, Play Mode, batch run, render) goes through
  the Deadly Jenga Unity coordinator (`deadlyjenga-worktree-pool` skill).
  Commit first, then request a slot or job for that commit.
- Build in small slices that each compile and run. Commit each slice with a
  message in the AGENTS.md format. Push to your task branch.
- Put the UI in UI Toolkit following `UI.md`. Make it look and feel good:
  spacing, clear type, consistent actor colors, friendly empty states, and
  no walls of fields.
- Keep scripts small and readable, with plain comments. Readers are college
  students new to Unity.
- If this chat gets long, keep `progress/` notes current so the work can be
  picked up from them.

## Finishing a phase (before starting the next one in this chat)
- Run every acceptance test in the phase file through the coordinator and
  record the actual results (pass, fail, not run, and why) in
  `progress/PHASE-<n>-results.md`, with links to renders or captures.
- Fix failures before moving on. If a test truly can't pass (for example it
  needs a real headset), record why and move on.
- Prepare that phase's review material (renders, a short screen capture, a
  list of exactly what to look at) but don't wait for Evan; go on to the
  next phase.

## Finishing the chat
- Present the review material for all your phases together.
- Report: what changed and where, the full list from
  `CROSS-BOX-CHANGES.md` for Evan to keep or drop, the decisions in
  `DECISIONS.md`, what was tested, and what still needs a headset check.
- Stop. The next chat starts only after Evan approves.
