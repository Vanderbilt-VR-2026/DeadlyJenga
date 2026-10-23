# Evan Draft Map 2: magnetic towers

Open MapDrafterEvan2 and press Play. The scene's MagneticDraftMap builds four open
towers, three levels of thin bridges, and two stepped cantilevers from the cube
and catwalk prefabs. The castle and other draft scenes keep their existing layout.

Purple pieces withstand 20 accumulated velocity-change units, green 8, red 3.
These are gameplay thresholds, not a real magnetic field simulation. Pushes use
the existing IPushable path; impacts convert collision impulse by block mass.
IDamageable can also break a connection. Damage persists until map rebuild.

Supported pieces are kinematic: gravity does not sag bridges and there are no
joints continuously transmitting a bridge's force into its towers. The graph is
built once from face contacts. A hit that reaches a piece's threshold breaks all
of that piece's connections, then one breadth-first pass finds which pieces still
reach a foundation. Unsupported pieces permanently detach into normal physics.
Strong pieces cannot take support through weaker pieces. Removing one tower's
base therefore cannot leave it hanging from red bridges to a neighboring tower.

Use the demo's existing rebuild control to restore the structure. This prototype
does not attract or reattach fallen pieces. Disasters which explicitly skip
kinematic rigidbodies (including the current tsunami and global quake paths)
do not damage attached magnets; direct IPushable, IDamageable and physical impact
paths do. These disaster adapters need a separate follow-up in World/Hazards.

The existing ISyncedWorld path carries the layout and detached piece indices.
Every break updates its revision; replicas remain kinematic. Network snapshots
continue to carry block poses. Partial damage is not transferred on host migration.
Real multiplayer and Quest profiling are separate from the editor smoke check.

Runtime code is in World/Blocks and World/Maps. No Core additions are required.
Color materials are optional artwork; deleting the colors does not change support.
The builder and test are explicit menu/batch actions, never automatic installers.

Batch validation entry: DeadlyJenga.World.Blocks.Editor.MagneticDraftTools.InstallAndTest.
This saves Evan 2, enters Play Mode, runs destruction/reset checks and exits batch
Unity. Results go to Logs/Validation/MagneticEvan2.txt.
