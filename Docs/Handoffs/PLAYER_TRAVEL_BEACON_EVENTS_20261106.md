# Local player travel beacon lifecycle

Baseline: `8c03e7be22f89ff5bc4f5cf73317dabe818930b4`.
Owner: MAIN, GameSession Session lane; the one Modes change routes solo return through the same Session command.

## Behavior and presentation contract

The managed Game flow previously loaded scenes directly, skipping the existing departure announcement. Both managed and Demo/solo scene travel now use the same 1.05-second unscaled departure interval before requesting a scene load. The host announces that interval to following clients on the existing departure channel. This is collective scene travel, not an effect for each joining/leaving peer.

- `GameEvents.DepartureStarted(float seconds)` starts the existing local departure effect. Session holds its transition flags for the real interval; Presentation owns the visuals. Lobby movement preparation stays with the existing lobby departure code. A match departure does not fabricate lobby movement.
- `GameEvents.Emoted(room.gameObject, "DepartureCancelled")` cancels the effect when the request fails or becomes stale. Session clears the hold flags and restores lobby movement. A failed load also resumes an already parked lobby when no active match exists.
- `GameEvents.Emoted(localPlayer.gameObject, "PlayerArrival")` is a **visual-only** lobby arrival cue after an accepted finite reserved spawn has actually been placed and the local lobby/player is active. It does not trigger descent, teleport, input suspension or a loading curtain. It is announced once per lobby arrival, not per floor revision, occupancy retry or respawn placement. Beginning a departure arms the next arrival; cancelling before travel restores the old announcement state.
- Existing `SpawnArrival` / `SpawnArrivalSettled` remain the physical Game arrival/descent contract. They are unchanged.

Exterior captures mean an exterior view of the **same local player**. There is no new per-peer effect, networking poll, Core API, shader or gameplay dependency on Presentation. In-place mid-match `VisitLobby` / `ReturnToMatch` and Home-screen travel are outside this actual-scene-transition change.

## Wire compatibility and cancellation

Existing reliable channel: `0x44455054`. Existing kind 1 payload remains exactly one four-byte `BitConverter` float containing the duration. Updated sends put the host departure sequence in key `id` and host connection generation in key `attempt`. This changes no pose/world protocol or SDK.

Kind 2 is a cancellation with an empty payload and the same positive sequence / host generation as kind 1. The receiver accepts it only while that exact remote departure is active. It rejects duplicate/stale IDs, mismatched generations, nonempty cancellation data and unidentified zero-ID cancellation. Connection-local identity history resets only when the receiving connection generation changes. A newer identified start supersedes the completed high-water identity; an old start cannot restart a finished effect.

The existing `TrustedServerData(runner, source, generation)` gate is reused, including its current Fusion source convention, runner identity, generation, migration and transport checks. The receive route now works in both local Lobby and local Game sessions. The SDK convention still needs the native two-peer acceptance below; source compilation is not proof of network delivery.

Legacy kind 1 keys with `id=0, attempt=0` remain accepted, with finite duration constrained to 0.3–3 seconds and exactly four payload bytes. Identified connections cannot downgrade to legacy zero IDs. Old binaries ignore kind 2, so failure recovery for a mixed-version old client is **not** claimed. Zero-ID starts cannot provide the stale-request guarantees of updated IDs.

## Checks completed

- Full candidate GameSession source preflight: 114 current runtime source files against 292 cached references from the installed Unity 6000.3.23f1 / Fusion 2.1.3 environment; compiler exit 0. This is an unwoven source compile, not a Unity import or native run.
- 43 focused assertions compile and execute the actual production `IslandRoomConnection.Travel.cs` with fake engine/transport objects: no load before the 1.05-second yield; map/lobby destinations; repeat rejection; cancellation, changed connection and stale operation; old coroutine isolation; failure callback once; parked-lobby recovery; exact legacy payload size; start/cancel identity; stale, malformed and duplicate cancellation; completed-start replay rejection; generation reset; finite duration validation; legacy compatibility.
- `git diff --check` passed. No Unity editor was launched for this task. No Core, SDK, package, scene or asset changes.

## Required native acceptance after integration

1. Solo and hosted Lobby → Game → Lobby: capture the first useful departure and settled arrival, in local-eye and exterior views of that same player, including a short motion segment. Check one stream per arrival and no repeat from a lobby floor revision or spawn retry.
2. Managed Game and Demo/solo Return to Lobby use the same visible interval and leave movement/menu state usable. Re-enter after a cancelled transition. Physical Game descent remains unchanged.
3. Two **updated** peers: verify guest receives the authoritative host departure in both Lobby and Game, then inject/reproduce one bounded host scene failure. Both peers must release the effect/hold; a delayed stale or duplicate cancellation must not stop a newer transition. Test invalid source/old connection through the existing native receiver gate.
4. Confirm no per-peer join/leave effects were added and the visual-only lobby cue does not move the player, suspend input or raise a curtain. Quest/headset behavior remains unverified until a device run.

This candidate is a source checkpoint pending the native acceptance above, not a claim that the combined feature, APK or headset is verified.
