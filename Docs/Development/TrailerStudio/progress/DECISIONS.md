# Decisions made by the builder chat

Decisions the spec left open, in the order they were made. Evan can overturn
any of them at review.

## Phase 1

1. **One chat builds everything.** Evan pointed the chat at
   `BUILDER-PROMPT.md` without filling in `<CHAT>`; `progress/` did not exist,
   so it started as chat A (Phases 1 then 2). Evan then changed the plan: the
   same chat tests and polishes 1 and 2, builds 3, then 5, then 4, and hands
   everything to the "Fable 5.2" chat for review. There are no chats B and C.

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

19. **Game-loop components live in the DevTools runtime assembly.** Unity
    cannot attach scripts from an editor assembly to objects, so the two
    components that call the Studio every frame are in
    `DevTools/Scripts/TrailerStudio/` (wrapped in `#if UNITY_EDITOR`, so they
    are not in player builds). They only call back through `StudioHooks`.

20. **Unity Version Control cancels and restarts Play Mode entry.** The
    package does this on every entry. The Studio ignores the "exiting Play
    Mode" event that arrives before Play Mode was ever entered.

21. **No random pickups on the stage.** The Studio turns the match's power-up
    spawns off. Marks can still start with a weapon.

## Phase 2

22. **The performer is the real local player.** `ISessionInfo.SetRole` and
    `IPlayableCharacter.Place` already switch rigs and place them, so the
    spec's "spawn as a specific actor" hook was not needed. The performer
    stays in the body between takes (needed in VR) until "Step out of the
    body" or a normal Play.

23. **Punch-in rides the old take.** Before the punch frame the performer is
    placed on the old take's position and facing every frame with input off,
    then gets control. Head pitch and arm pose are not driven during the
    ride-along. The join blends over 0.2 s.

24. **Comps and clones make new takes.** A comp copies frames from two takes
    into a third. Nothing references other takes, so deleting a source take
    later cannot break a comp.

25. **A performance pass records only its actor**, unless "Live physics" is
    on. Then the world, hazards, effects and sounds are recorded again as new
    takes, and older actor takes are flagged stale with the first frame where
    the worlds differ by more than 1 cm.

26. **Recorded actors draw their own recorded weapon.** The multiplayer copy
    asks the live match what its actor holds; a recorded actor is not in the
    match. The Studio draws the weapon with the game's own
    `CandyWeaponVisual`, from the equipment state saved in each actor frame.

27. **Ghost look.** "Ghost" swaps the copy's materials for one see-through
    material in the actor's color. "Hidden" turns its renderers off. "Walk
    through" turns its colliders off. These only apply while someone is
    performing.

28. **The hand: performed or hand bot.** Automated hands use the game's own
    hand bot (`ISessionInfo.HandBots`), recorded like any actor. A hand path
    drawn in the viewport with timed grabs was not built; the Direct tab's
    weak-point and push cues cover "this block comes out at this time"
    without a visible hand.

29. **In-game keys while performing:** F9 record or stop, F10 again, F8 back
    3 s and punch in, F7 slow motion. The Studio window's own shortcuts only
    work when the window has focus, and the Game view has it while playing.

30. **VR wrist panel** is world-space boxes and text on the left wrist,
    pressed by touching with the other hand (no ray, no UI package). Voice
    commands were not built. Not tested in a headset.

## Phase 3

31. **Disaster settings need no Core change.** `DemoDisasters.Execute`
    already takes a list of values in the order each disaster lists its
    parameters; the cue inspector shows them as sliders with their real names
    and units. Only the *direction* needed a change (`IDirectedHazard`,
    commit `aa5331a4`). Today only the tornado uses it; the tsunami keeps its
    own cue with heading and height.

32. **A branch keeps the branch frame itself.** The tape is unchanged up to
    and including the branch frame; the first new frame is the next one. The
    blocks are handed to physics with the pose and speed the tape had, worked
    out from the frame before. Limits: a disaster already running on the tape
    does not carry over, and broken-joint or structural state that the tape
    does not store is whatever the map has at that moment.

33. **Variation runs are world takes.** Each try is a normal world take with
    a group id, scores and a thumbnail in its notes. The run leaves the
    original world active until one is picked. They run in the open editor, one
    after another, at the fixed clock. A separate coordinator batch entry for
    variation runs was not built; the acceptance suite shows they run in batch.

34. **Chain reactions detect a hit by speed change.** "When its first block
    hits something" means: that block has moved 30 cm and then its velocity
    changes by more than 1.5 m/s in one physics step.

35. **Time of day and weather are done by the Studio itself.** The game has
    no control for them. While the stage is open the Studio turns the scene's
    sun, tints it and sets fog, and restores everything on close. A sky that
    paints its own sun does not follow. Water level cannot be changed. No
    `IEnvironmentControls` was added to Core because nothing in Presentation
    would implement it yet.

36. **Slow-time cues** change the game's time scale while performing in real
    time. Fixed-clock recordings ignore them.

## Phase 5

37. **Cameras live in the Set, not on the tape.** They are a few keys each
    and are edited constantly; in the Set they get undo and survive tape
    changes. The `camera` tape type id stays reserved but unused.

38. **Camera keys are in tape time.** Speed ramps slow the tape, and the
    camera's move slows with it. During a freeze frame the camera keeps
    moving and stays that much ahead afterwards.

39. **Renders advance the game clock with the tape.** Each rendered
    (sub-)frame moves game time by the same amount as the tape, so ocean, sky
    and effects slow down in slow motion. Because those are driven by the
    game clock, two renders differ in water and clouds unless "Hold ocean and
    sky still" is on; with it on, renders are bit-for-bit repeatable.

40. **Cinema quality** during a render: the project's highest quality level,
    8x MSAA, SMAA on the camera, LOD bias 4, longer shadow distance, all put
    back afterwards. The build target is not switched (that reimports the
    project); the Windows editor already renders with the PC pipeline.

41. **Output formats.** PNG (8-bit) and EXR (16-bit float) sequences are
    written by Unity. MP4 and ProRes need FFmpeg on the machine; without it
    only the sequence is written, and the Studio says so. Unity cannot write
    16-bit PNG, so EXR is the high-depth format.

42. **Anamorphic squeeze is stored but not applied.** The field is on the
    camera for a later optical model.

43. **Sound** for the cut is one fixed-clock playback of the whole tape
    recorded with Theatre's capture (mix and stems), heard from the stage
    spot. Slow-motion shots get real-speed sound from their start time; the
    editor is where sound is finished.

44. **The Cut tab is a list, not a second timeline.** Order, trim, dissolve,
    beat snapping and export. Title cards and logo overlays were not built
    (the spec marks them optional).

## Phase 4

45. **A sixth tab, Polish**, sits between Perform and Direct. `UI.md` lists
    five tabs; Phase 4's tools did not fit any of them without crowding.

46. **Every motion edit makes a new take.** The edited take becomes active
    and the source stays. Undo for motion edits is "pick the old take".

47. **Layers change the pose on the way to the screen.** They run in
    `IActorFrameFilter` just before a recorded frame is shown, so takes are
    never rewritten and muting is instant.

48. **Reactions are procedural, not ragdoll.** They move the four things a
    performance is made of (feet, head, two hands); the game's avatar solves
    the body from those. No physics ragdoll was built; that needs a ragdoll
    on the survivor prefab and was left as a Characters task.

49. **Not built in Phase 4:** stunt clips on the layer list (Theatre's stunt
    import poses the live avatar class, not the playback copy), voice lines
    on a track, and eye direction (the avatar has no eye control in its
    network state).