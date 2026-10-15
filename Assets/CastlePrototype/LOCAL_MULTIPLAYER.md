# Local demo multiplayer

The `CastlePhysicsTest` demo starts local discovery automatically through
`CastlePlaySession`. No Photon account, relay, cloud lobby, or hosted server is
used by this path. Existing Photon scenes remain separate.

## Try it

1. Build the updated demo onto both headsets and connect them to the same Wi-Fi.
   For editor testing, restart Play mode once after installing these scripts.
2. Open the demo menu (left Y, tracked left-hand menu gesture, or desktop Esc).
   In VR choose **Local multiplayer / invites**; desktop controls are already
   inside the menu.
3. Cycle **Nearby** to select the other demo. Choose **Request to join selected
   demo**, then have its runner choose **Accept**.
4. Alternatively, the runner selects the other demo and chooses **Invite
   selected demo here**. The recipient accepts; that accepted invite authorizes
   their return join request without a second host approval.
5. The running demo continues. The joiner receives the current block positions,
   bot population, human players and physical hands. A stable, clear block top
   is chosen for a survivor; if none remains, the joiner becomes a flying hand.

Incoming requests/invites open the menu and show a NEW indicator on its local
multiplayer page. Invitations and requests expire after 30 seconds. **Leave /
end local session** also cancels a pending join. Discovery uses UDP port 47771;
the gameplay connection uses a host-advertised dynamic TCP port. Windows test
hosts may need the normal firewall allowance for Unity/the built game. Guest
Wi-Fi that isolates devices prevents local connections.

## Simulation and joining

- The host owns block physics, remote giant-hand contact physics and all bots.
  Clients receive full current snapshots at 15 Hz and blend block poses between
  updates. Inputs/avatar poses return at 30 Hz. Full snapshots make late joining
  and replacing an unsent update independent of earlier state messages.
- Each client's own survivor locomotion and head tracking remain local. Their
  giant-hand visual is predicted locally without collision forces; the host
  runs its physical counterpart and broadcasts actual palm/finger poses to the
  other players. Contact may visibly differ from the owner's preview under load.
- Network reads/writes run off the Unity main thread. Each connection retains
  only the newest unsent and received state, plus a bounded control queue.
  This avoids accumulating frames behind slow peers. TCP can still pause
  updates during packet loss; this is a LAN demo transport, not an Internet
  latency or competitive movement system.
- Clients cannot rebuild the castle or change the host's bots. A host rebuild
  changes the generation number and replicates the replacement world.
- Human joins are independent of the bot population's ten-slot setting. There
  is no ten-player gameplay cap; practical capacity depends on headset physics,
  rendering, Wi-Fi and payload limits. The decoder has a defensive 512-actor limit.
- Missing hand input releases that remote hand after one second. Disconnected
  peers are removed. If the host disappears, each client resumes its last
  received castle locally; there is no host migration or preserved shared session.

## Scope

“Nearby” currently means the same reachable LAN. Bluetooth distance discovery
and Meta Shared Spatial Anchors are not installed or implemented. Players share
virtual game coordinates; this does **not** align the castle to the same physical
spot in a room. This is appropriate for the existing locomotion-based VR demo.

The Android build hook adds Internet/Wi-Fi multicast permissions to Unity's
generated manifest while preserving the OpenXR activity configuration. An
Android multicast lock is acquired for discovery and released on teardown.

## Verification

- `Tests/LocalMultiplayer/Run-TransportChecks.ps1`: 15 passing real loopback socket
  checks, including multiple clients, bidirectional messages, a 160 KB snapshot,
  rapid state replacement, malformed frame rejection and independent disconnects.
- **Tools > Castle Prototype > Validate Local Multiplayer Protocol**: snapshot
  and finger-pose round trips, malformed inputs, 24 actor records, physical-hand
  authority transitions, and (on Android editor target) manifest merge checks.
  Eleven checks passed in an isolated Unity 6000.3.23f1 batch project. The default
  568-block castle plus 24 survivor records serialized to 31,056 bytes.
- Runtime and editor scripts compile in Unity. No two-Quest play session or
  on-device frame-rate/latency measurement has been performed.

Before presenting, test both request and invite paths with two Quests, join
after moving/collapsing blocks and after spawning bots, push blocks as either
player's giant hand, rebuild as host, join after all usable spawn surfaces are
gone, then disconnect/rejoin. Confirm both views agree and measure frame rate
during a collapse. Use identical builds on all devices.
