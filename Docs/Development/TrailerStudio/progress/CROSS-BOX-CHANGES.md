# Changes outside DevTools

Every Trailer Studio change in Core, World, Characters, Presentation or
GameSession goes here, one row per commit, for Evan to keep or drop.

| Commit | Box and files | What was added | Why | What breaks in the Studio if dropped |
|--------|---------------|----------------|-----|--------------------------------------|
| `2fe6abf1` | Core: `Core/Network/NetworkActors.cs`. Characters: `Characters/Network/SurvivorNetworkView.cs`, `HandNetworkView.cs` | Core interface `INetworkActorSnap { void SnapToState(); }`, implemented by the two remote-character copies. Survivor: `frame = target`. Hand: shown bones set to the last received bones. | A remote copy eases toward each new state to hide network lag. Playback needs the copy exactly on the recorded pose, also when scrubbing to a single frame. | Nothing stops compiling: the Studio falls back to calling `Render` with a huge time step. Played-back survivors then land on the right spot but their walk animation is fed a 1000-second step every frame, so legs jitter; scrubbed poses are less exact. |
| `aa5331a4` | Core: `Core/Data/IHazard.cs`. World: `World/Hazards/BasicDisaster.cs` | Core interface `IDirectedHazard { float? Heading { get; set; } }`, implemented by `BasicDisaster`. When set before `Begin()`, the tornado arrives from that side of the map. Null (the default) keeps the game's random choice and the same random calls. | Phase 3 asks for a tornado cue with a set heading; the tornado's route was random only. | The "Choose where it comes from" switch on disaster cues disappears (the Studio checks for the interface) and tornadoes arrive from a random side. Everything else works. |

## Notes

- Phase 1 needed no other change. The two Core additions the spec expected
  (`IWorldPlayback`, `IRecordableHazard`) were avoided by using contracts that
  already exist; see decisions 4 and 5 in `DECISIONS.md`.
