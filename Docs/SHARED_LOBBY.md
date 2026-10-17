# Shared lobby screen

The local XR rig creates its CandyPlayerAvatar when Lobby starts, before hosting
or joining. Fusion continues to spawn remote avatars; the existing NetworkHands
owner handling hides the duplicate network visual on its owner's device.

LobbyMatchSetup (Presentation/Visuals/Menus/Lobby) is now the main menu. It includes Host, Join, Solo practice,
match rules, stamina, and the local comfort/audio choices. Join opens the existing
room-code entry panel so its Quest keyboard bindings are retained. The old Home
and Host panels are hidden at runtime; keep them and the Lobby Countdown object
in the scene because serialized references and the join/back wiring still use them.

Once connected, the existing Lobby Countdown NetworkObject replicates the screen
position, rotation, page, and match rules through LobbyCountdown (GameSession). The menu
never talks to LobbyCountdown directly: it publishes its page and placement and reads the
shared state through Services.Lobby (Core's ILobbyControls), which LobbyConnection provides. Each device
renders that state into its local world-space Canvas. Only state authority writes
shared rules. Clients cannot change pages or settings or start/cancel the game;
they can still leave. The host can start, cancel, and close the room from this
screen. Room code and countdown occupy the heading. Late joiners receive current
networked state. Local comfort/audio controls are available before connecting and
are not overwritten by another player's preferences.

No extra Inspector wiring or NetworkTransform is required. Build both headsets
from the updated project because LobbyCountdown's network schema has changed.

## Verification

- Unity compiled the scripts and Fusion postprocessing successfully.
- Fresh desktop Play showed the avatar hands before joining and the consolidated
  menu, with zero errors in the Console at startup.
- Confirmed the old Host/Join controls no longer overlap the new main menu.
- Two-device replication, host-only input, late join, countdown/cancel, leave/rejoin,
  and Quest controller/keyboard interaction still need device testing.
