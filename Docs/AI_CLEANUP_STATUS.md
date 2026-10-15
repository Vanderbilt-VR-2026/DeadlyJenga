# AI cleanup — 2026-10-15

Work stays on `ai-cleanup`. Existing uncommitted work, the Unity MCP package,
recovery scenes, and old gameplay assets were preserved. Nothing was committed,
merged, or published by this pass.

## Implemented

- [x] Fusion rooms now attach `CastleFusionSession` when `Game` starts.
- [x] Reuse the castle snapshot/actor implementation without starting LAN sockets.
- [x] Host simulates the castle and AI. Clients use kinematic castle replicas and
  receive the host's world state, bot poses, and remote hand poses.
- [x] Client hand targets reach physical remote hands on the host. Client preview
  hands do not apply independent forces to the local castle replica.
- [x] Readiness/welcome acknowledgement precedes world streaming. A client waits
  for its initial world before enabling gameplay. Host stamina rules travel in
  the welcome packet; VR comfort settings remain local.
- [x] Compress snapshots and allow only one unacknowledged world and player pose
  per peer. Remove departed actors, suspend stale hands, and time out stalled peers.
- [x] Host rebuilds are propagated by the existing revision/snapshot mechanism.
- [x] Game menus offer Fusion Leave / Close, rather than LAN discovery controls.
- [x] Lobby pages expose stamina capacity, drain, recovery, delay, curve, and degree.
- [x] Add an explicit AI-hand count and use its selected personality when spawning.
  Hands consume population slots before survivor bots fill the remaining capacity.
- [x] Add lobby audio volume/preview controls and a desktop studio launcher. Audio
  uses a runtime copy of the bank so project assets are not modified by mixing.
- [x] Solo practice is separate from multiplayer and refuses to load over a live room.
- [x] Stop the old lobby hand-input path in `Game`; clean up session-created local
  players and hands when the session object is destroyed.

No additional Inspector wiring is required for this bridge: `CastlePlaySession`
attaches it to the active runner automatically. Start testing from Lobby; use the
same updated build on both headsets. The old LAN implementation remains in source
for reference/validation, but the lobby no longer starts it.

## Verification actually completed

- Unity runtime C# compilation succeeded with the installed SDK references.
- Unity editor compiled and executed `Tools > Castle Prototype > Validate Cleanup Networking`.
- Existing protocol validation passed: snapshot round trip, malformed/truncated
  input rejection, and client preview hands unable to apply world forces.
- New checks passed: compressed snapshot round trip, expanded-size limit, and
  host stamina-rule round trip.
- Synthetic 568-block snapshot measured 16,690 raw bytes / 9,090 compressed bytes.
  This is a serialization sample, not a network-performance benchmark.
- Script diff whitespace checks passed.

## Not verified / remaining work

- [ ] Two-headset test: host, join, countdown, both enter the same castle, push a
  block with a remote giant hand, compare results, rebuild, leave/rejoin, close host.
- [ ] Verify late join while the castle is already moving, different scene load
  times, dropped connections, and return-to-lobby/rehost on actual devices.
- [ ] Profile Quest CPU, bandwidth, and contact latency. This bridge sends full
  snapshots at up to 10 Hz and poses at up to 30 Hz, each throttled by acknowledgements.
  It is an initial integration, not predicted Fusion physics. Slow connections
  lower the effective update rate; delta replication/prediction may be needed.
- [ ] Verify the world-space lobby panel and controller targeting in the headset.
  UI construction and compilation are checked, visual placement is not.
- [ ] The full sound studio remains a desktop IMGUI authoring tool. Its lobby
  launcher and VR mix controls are present; the full editor has not been ported to VR.
- [ ] Legacy `Main.unity`, recovery scenes, texture imports, and feature asset
  folders are retained. They need an asset-reference audit before deletion or
  relocation. `Main` remains disabled; Lobby and Game are the enabled build scenes.
- [ ] Archive old prototype reports after updating the validation tools that write
  those paths. Do not move only the reports: the next validation would recreate them.

The Mac Play attempt crashed in native OpenXR simulator graphics initialization
(`openxr_simulator::XrApiLayerRenderingMetalOnVulkan::createVulkanObjects`) before
Lobby ran. No successful Play-mode or headset result is claimed. Further local
Play attempts were stopped at the user's request.

## Implementation notes

`CastleLanSession` currently contains the shared world/actor logic as well as the
legacy LAN path. `InitializeFusion` selects a socket-free mode; the public Fusion
adapter methods reuse serialization, reconstruction, and avatar rendering. A
later naming/extraction cleanup can separate those responsibilities without
changing the packet contract. No NetworkObjects need to be added to every castle
block for this snapshot-based bridge.

Data-stream API reference: https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-streaming
