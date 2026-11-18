# Decisions made by builder chat A

Decisions the spec left open, in the order they were made. Evan can overturn
any of them at review.

## Phase 1

1. **This chat is builder chat A.** Evan pointed the chat at
   `BUILDER-PROMPT.md` without filling in `<CHAT>`. `progress/` did not exist,
   and only chat A "starts now", so this chat took A (Phases 1 then 2).

2. **Branch and worktree.** Branch `claude/trailer-studio-a` from
   `Evan_Sprint_4` at `73339768`, in the worktree
   `Documents/Codex/Projects/deadlyjenga-claude-trailer-studio`. The spec files
   were untracked on the central checkout; they are committed on this branch
   unchanged.

3. **Unity slot.** Coordinator owner name `claude-trailer-studio`, using the
   shared slots (no `--slot`). The "Fable 5.2" dedicated slot 3 belongs to
   the flying-hand chat, which was using it.

4. **No Core change for world playback.** The spec expected a new
   `IWorldPlayback`. Every map already implements
   `ISyncedWorld.SetReplica(bool)`, the switch multiplayer guests use to make
   the map kinematic. The Studio uses that.

5. **No Core change for hazards.** The spec expected `IRecordableHazard`.
   Hazards already publish what they look like each frame for multiplayer
   guests (`DisasterStates.Active` frames and the wave frame). The Studio
   records those and publishes them again on playback, so visuals and sounds
   come from the same Presentation code. Limit: a played-back hazard does not
   push or hurt a live character (on a real guest the host sends those hits).

6. **Actors are recorded as their own network state.** Survivors and hands
   already pack themselves for multiplayer (`INetworkActor.WriteState`) and
   already have a "someone else's character" copy
   (`NetworkActors.CreateView`). The Studio records the first and plays back
   with the second. That copy is solid, shows weapons and emotes, and needed
   no change in Characters. The copy normally eases toward new poses; the
   Studio calls its `Render` with a very long time step so it lands exactly on
   the recorded pose.

7. **Two kinds of world playback.**
   - *Exact*: with nothing live in the scene, each block's transform is set to
     the tape's value every frame. Scrubbing, drift checks and renders use it.
   - *Physical*: while a live character shares the scene, blocks are moved
     with `MovePosition`/`MoveRotation` every physics step toward where the
     tape puts them at that moment, with interpolation on. Live characters can
     stand on them and be pushed.
   The spec asked for per-block velocity to be set too. Unity does not let
   code set the velocity of a kinematic body, so gameplay that reads
   `Rigidbody.linearVelocity` from a played-back block sees zero. Contacts and
   pushing work because PhysX derives the kinematic velocity itself. Known
   side effect: a live actor recorded against physical playback can sit up to
   one physics step (20 ms) off the exact playback of the same block.

8. **Two clocks.** Whole-set recordings and checks use a fixed clock (one
   tape frame per rendered frame, like Theatre). Performing and the
   transport's Play use real time, and tape frames follow the game clock. A
   real-time playback that cannot keep up shows a "Slower than real time"
   chip.

9. **Tape rate.** Sets default to 30 fps and offer 24, 25, 30, 50, 60. The
   game's physics runs at 50 Hz, so 25 and 50 line up with physics exactly.

10. **Where files live.** Sets are small JSON files in
    `DevTools/Editor/TrailerStudio/Sets/` (found by the marker file's name;
    safe to commit). Tapes are in `Recordings/TrailerStudio/<set id>/`, which
    the repository already ignores. A deleted Set is copied to a `trash`
    folder beside its tapes; tapes are never deleted by the Studio.

11. **Marks are placed with the stage open.** The maps are built at runtime,
    so there is nothing to click in Edit Mode. The Studio's viewport is its
    own orbit camera drawn into the window, and marks are dragged there. This
    replaces "live Scene/Game view" in `UI.md`; the Scene and Game views still
    work alongside.

12. **Roles in a whole-set recording.** Bot marks use the normal bot brain.
    Survivor marks nobody has performed yet get a stand-in (a survivor with
    the brain off that stands on its mark and obeys gravity). Hand marks wait
    for the Perform mode.

13. **Cues.** Phase 1 runs `collapse`, `hazard`, `tsunami` and `stopHazards`.
    Theatre's `punch`, `fire`, `gear` and `dropDeck` cues are imported and
    kept but not run (they script actors; in the Studio actors are performed).
    The cue row and inspector say so.

14. **Time of day and weather.** The game has no time-of-day or weather
    control on any map. The Set keeps the two fields, and the inspector says
    plainly that there is nothing to drive yet.

15. **Undo** uses Unity's own Undo (Ctrl+Z, Edit menu, the two top-bar
    buttons). It covers Set edits (marks, cues, settings). Tape changes
    (takes, mute, solo, lock) are not undoable; trashed takes are restorable
    instead.

16. **Sound events.** Block hits and match sounds are recorded and raised
    again on playback, so Presentation plays the same sounds. Block-broken
    effects are recorded but not replayed, because that event needs the block
    object. PCM stems (Theatre's `TheatreAudioCapture`) are not wired into
    Studio recording yet; that belongs with the Phase 5 render path.

17. **Examples are hidden by default.** The example track type and example
    mode tab only appear after "Show example extensions" (Ctrl+K), so normal
    tapes and the tab bar stay clean.

18. **Icons are text.** The editor font lacks many symbols, so buttons use
    short words ("Play", "Undo", "M", "S", "L") instead of glyphs.
