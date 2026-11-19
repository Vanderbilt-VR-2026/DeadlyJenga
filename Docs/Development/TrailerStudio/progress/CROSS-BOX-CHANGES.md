# Changes outside DevTools

Every Trailer Studio change in Core, World, Characters, Presentation or
GameSession goes here, one row per commit, for Evan to keep or drop.

| Commit | Box and files | What was added | Why | What breaks in the Studio if dropped |
|--------|---------------|----------------|-----|--------------------------------------|
| `2fe6abf1` | Core: `Core/Network/NetworkActors.cs`. Characters: `Characters/Network/SurvivorNetworkView.cs`, `HandNetworkView.cs` | Core interface `INetworkActorSnap { void SnapToState(); }`, implemented by the two remote-character copies. Survivor: `frame = target`. Hand: shown bones set to the last received bones. | A remote copy eases toward each new state to hide network lag. Playback needs the copy exactly on the recorded pose, also when scrubbing to a single frame. | Nothing stops compiling: the Studio falls back to calling `Render` with a huge time step. Played-back survivors then land on the right spot but their walk animation is fed a 1000-second step every frame, so legs jitter; scrubbed poses are less exact. |
| `aa5331a4` | Core: `Core/Data/IHazard.cs`. World: `World/Hazards/BasicDisaster.cs` | Core interface `IDirectedHazard { float? Heading { get; set; } }`, implemented by `BasicDisaster`. When set before `Begin()`, the tornado arrives from that side of the map. Null (the default) keeps the game's random choice and the same random calls. | Phase 3 asks for a tornado cue with a set heading; the tornado's route was random only. | The "Choose where it comes from" switch on disaster cues disappears (the Studio checks for the interface) and tornadoes arrive from a random side. Everything else works. |
| `8753dd61` | Characters: `Characters/Player/CandyPlayers/CandyPlayerRig.cs`, `Characters/Network/SurvivorNetworkView.cs` | `CandyPlayerRig.GaitState` and `CandyPlayerRig.Gait` (get/set the walk cycle's memory: last feet position, stride direction, walk phase, sprint blend, speed). `SurvivorNetworkView.Gait` passes it through. No Core change. | The avatar advances its walk only when time passes, from whatever state it was left in. Shown frame by frame (scrub, render) the legs stood still or depended on what was shown before. Review finding A1. | The Studio stops compiling (`Tracks/ActorTrack.cs` uses it). Without it there is no clean way to make rendered actors walk. |
| `05c5a153` | Characters: `Characters/Network/HandNetworkView.cs` | `SnapToState` (from `2fe6abf1`) now also sets the hand bodies' physics pose, not only their transforms. | `Render` read the stale physics pose after a snap. Review finding C11. | Nothing breaks; a played-back hand may show the previous pose for one physics step after a jump. |
| `c419d1fd` | Characters: `Characters/Player/CandyPlayers/CandyPlayerRig.cs` | `CandyPlayerRig.StepGait(gait, frame, dt)`: one step of the walk cycle as a pure function. It repeats the sum in `ApplyFrame` (without the moving-floor part). `ApplyFrame` is not changed. | The Studio needs the walk state for every frame of a take. Posing a real copy for every frame was slow and raised every emote event in the take. Review round 2, finding A1. | The Studio stops compiling. Note: two copies of the walk sum now exist; a later tidy-up could make `ApplyFrame` call `StepGait`. |
| `c737b172` | Characters: `Characters/Player/CandyPlayers/CandyPlayerRig.cs` | `StepGait` uses `frame.gaitPhase` when `frame.hasGaitPhase` is set, the same as `ApplyFrame`. | So the two copies of the walk sum really are the same. Review round 3. | Nothing visible today; the copies would differ for frames that carry their own phase. |
| `2496f88d` | Characters: `Characters/Player/Scripts/CastleSurvivor.cs` | In the ground probe, the moving-floor support is `hit.collider.transform` instead of `hit.transform`. One line. **This changes the live game.** | Survivors stand on per-block surface copies parented to one shared body that never moves, so the existing carry step was always zero and nobody rode a moving block. The copy's own transform follows its block. Now a standing survivor (and a bot) is carried: Phase 1 test 3 measures 0.71 m for a 0.70 m block. Side effect: climb grip (`SupportAtRest`) now sees the real block, so it refills only on a block at rest, as its comment says. Maps with unified collision are unchanged. | Phase 1 test 3 fails again (nobody is carried, live or in playback). Nothing else in the Studio depends on it. |
| `841b9b98` | Characters: `Characters/Player/CandyPlayers/CandyPlayerRig.cs` | `ApplyFrame` and `StepGait` both call one private `AdvanceGait`. Public signatures unchanged. Meant to change nothing in the game. | Two copies of the walk sum could drift apart. | Nothing breaks; the sum exists twice again. |

## Notes

- Phase 1 needed no other change. The two Core additions the spec expected
  (`IWorldPlayback`, `IRecordableHazard`) were avoided by using contracts that
  already exist; see decisions 4 and 5 in `DECISIONS.md`.

## Wanted from other boxes (not changed; for Evan to decide)

- **Characters: head tracking freezes when a rig is re-enabled while the
  headset is already tracked.** `CastleSurvivor.ConfigureHeadTracking` uses
  a pass-through tracking-state action. Found by the emulated-XR suite; see
  `AGENT-FINDINGS.md` item 17. Not changed: it needs a Quest to check.
- **GameSession: a public way to give an actor equipment.**
  `Engine/StudioCast.cs` calls the private `MatchModeRuntime.GiveEquipmentTo`
  by reflection (Theatre does the same). A rename would fail only with a
  warning at runtime.
- **Characters: emotes follow game time.** `SurvivorNetworkView` measures an
  emote's age with `Time.time`, so a scrubbed frame always shows the start
  of the emote and a render that begins mid-emote starts it from zero.
- **Characters: `CandyAvatarFrame.airborne` and `.sprinting` are internal.**
  "Lock feet" would like to know when an actor is in the air; it guesses
  from vertical speed instead.
- **Characters: more of the avatar follows game time.** Body turning toward
  the head, landing bends and the ground drop in `CandyPlayerRig` use
  `Time.deltaTime` and `Time.time` directly. They are small, but they are
  why two routes to the same tape frame can still differ by a little.
