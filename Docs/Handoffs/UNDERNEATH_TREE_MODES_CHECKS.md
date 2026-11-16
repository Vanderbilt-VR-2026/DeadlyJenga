# Underneath the Tree mode setup: desktop runtime evidence

Source and tested revision: `41d5281c6d1d3f4818254a7a443fe63d8925c723`.
Checked job: `47d11fe077bc45e48acce0c7dc1aa68f`.
Fixture: `DeadlyJenga.CastlePrototype.Editor.UnderneathTreeModeChecks.Run`.
Actual Game.unity loaded separately for each mode, with the real map and bots.
This is setup validation, not a human combat/respawn playthrough, custom disaster test, Fusion peer test, Quest test or APK acceptance.

```text
Real Game.unity: Tree Team Kill and Disaster Survival setup
PASS teamtickets: approved map loaded
PASS teamtickets: requested mode playing
PASS teamtickets: rug and house floor remain walkable
PASS teamtickets: catalog permits mode
PASS teamtickets: 128 supported clear authored spawns across eight groups
PASS teamtickets: spawn resolver uses authored group 0
PASS teamtickets: spawn resolver uses authored group 1
PASS teamtickets: live participants assigned opposing teams
PASS teamtickets: local player and real bots registered
PASS teamtickets: local player survives initial setup
PASS teamtickets: bots survive initial setup
NOT VERIFIED: custom disasters, human navigation, network peers, Quest.
PASS survival: approved map loaded
PASS survival: requested mode playing
PASS survival: rug and house floor remain walkable
PASS survival: catalog permits mode
PASS survival: 128 supported clear authored spawns across eight groups
PASS survival: spawn resolver uses authored group 0
PASS survival: spawn resolver uses authored group 1
PASS survival: local player and real bots registered
PASS survival: local player survives initial setup
PASS survival: bots survive initial setup
NOT VERIFIED: custom disasters, human navigation, network peers, Quest.
```
