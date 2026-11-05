# Marina arrival and spawn recovery checkpoint

The lobby now reserves the host slot first and assigns authenticated joining players to stable first-free slots near the terminals. Duplicate Ready packets resend the same reservation. Receiving the same accepted reservation does not teleport a player back after they have started walking.

Placement checks the current controller/head envelope, floor and static blockers, other reservations and temporary occupants. Temporary setup or occupancy returns a wait result. Invalid or permanently unsafe placements are rejected rather than forced. A changed floor requests a fresh trusted spawn.

Migration preserves live reservations until the new authenticated roster and adopted floor are ready, then commits one new allocation boundary. Leaving or timing out releases the corresponding slot; full disconnect clears reservations. Parking the lobby for a match does not clear them. Busy arrivals defer one stamped trusted pose; scene or departure boundaries request a fresh one after resuming.

## Checks and limitations

This integrates the unchanged D5 V3 seven-file helper with the reviewed MAIN root callbacks. The first root draft was held because duplicate Spawn packets could reteleport a walking client and direct receive paths lacked transition guards. The revised root passed 46 executed extracted callback assertions and 20 source guards; D5 independently reran the assertions and accepted the corrected pair for native testing. Stand-ins represented SDK, helpers and physics in these tests.

The combined revised Marina and bounded Game pose-window source compiled all 113 GameSession runtime sources against installed Unity/Fusion references, with no warnings. This is not native collision, migration, headset or performance acceptance. Actual two-peer join/leave/rejoin, physical lobby clearance/floor adoption and final combined Game tests remain required. The earlier c2dd native run proved both signed checkpoint caches, public weapon grant, first Game, Results and return to Lobby; it stopped before leave/rejoin because the test navigated a hidden legacy terminal.

No Core contracts, SDK packages or project settings were changed. The room content discriminator separates this placement/window revision from older development builds.
