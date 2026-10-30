# Megablock Tower

New independent map, based on the approved giant enclosed tower and interior concepts in `ArtDirection/RectangularTowerConcepts-20261026`.

- 40 m × 30 m footprint; 30 storeys at 4.8 m; roof about 145 m high.
- Four movable windowed corner assemblies per storey. Large floor and wall pieces are batched into each assembly, with exact primitive collision around real openings.
- Thirty separate supported switchback stair assemblies. Each contains two 4.2 m wide flights, broad landings and bearing feet; no hinge joints, magnetic supports or fixed core.
- Four independently movable roof quarters, with the final landing integrated into one roof quarter. Two entrance ramps.
- 156 rigidbodies total. Collision shapes still have a cost; grouping does not turn the structure into 156 simple boxes.
- Aligned box colliders share their rigidbody's GameObject using collider centers. Only the 60 sloped ramp boxes need separate transforms. The existing three-argument structural builder retains its original hierarchy for other maps.
- Fast-moving sections switch to continuous dynamic collision detection above 12 m/s and return to discrete collision below 4 m/s. Replica bodies keep their authoritative kinematic behavior.
- Two entrances, flush outer walls, four windows across each long face, two across each short face, and a partitioned room on every floor. Window holes have solid sills and lintels.
- Sills are 0.45 m high for the game's default 0.85 m avatar. Stair landings sit in recessed bearing seats so floors and ramps meet without raised steps.
- Each ramp leaves a small joint clearance at either end so its tilted box end cannot wedge the next independent landing upward.
- Shallow bevelled landing noses and overlapping outgoing ramps tolerate small differences between independently settled floors without a vertical trip edge.
- A central shaft makes vertically separated floors visible and leaves space for falling sections. Floor slabs stay within the outer wall envelope.

The stair assembly is a fabricated structural unit. Individual walls/floor blocks within a corner assembly do not detach from it. Damage and pushes can topple complete corners and staircase sections. This deliberately trades fine brick-by-brick rubble for fewer active bodies.

Runtime code uses only World and Core. The map supplies the existing world, footprint, rebuilding, original-collider traversal and deterministic layout contracts. Reset recreates the complete authored layout. Replica mode makes bodies kinematic for the existing authoritative world synchronization.

Explicit editor tool: `Tools > Deadly Jenga > Maps > Build Megablock Tower`. It creates the new prefab, materials, shore collision and lobby catalog entry without replacing another map. Existing materials and maps remain separate. The map appears in the structures category of the lobby and demo map pickers.

Developer validation is in `MegablockTowerChecks`: actual Game scene load, gravity settling, window collision, production survivor motor route to roof, baseline/bot/tsunami samples, off-centre impact, reset and layout validation. Reports and actual captures go to `Logs/Validation/MegablockTower-*`. These desktop checks do not certify Quest performance, XR comfort or multiplayer gameplay.
