# Twenty tower prototypes on two islands

The selectable maps are **City Buildings 11–20** (`city-tower-exhibition`) and **Tower Laboratory 1–10** (`tower-lab-island`). Each uses one continuous, shore-matched island approximately 688 × 687 m, with ten independent sites, broad spaces between them, and numbered signs. Existing maps are preserved.

These are playable structural prototypes derived from the concept studies, not exact replicas of generated illustrations. Structural openings, stairs and bearings are real collision geometry. Different floor intervals and practical section heights replace illustration dimensions where needed. The maps use the existing candy materials/rendering system, beveled combined section meshes, original traversal colliders and shared geometry caches.

## Construction and counts

| Map | Moving structural bodies | Structural collision shapes | Joints |
|---|---:|---:|---:|
| City Buildings 11–20 | 100 | 1,380 | 0 |
| Tower Laboratory 1–10 | 104 | 1,878 | 6 |

The island adds one static collision mesh. A three-floor cassette normally has 4 m floor intervals, parallel 3 m wide return lanes across an 11 m run, an actual opening through the destination floor, and flat side landings. The stair opening covers both lanes; a full landing connects their ends without an overhead flight blocking the arriving player. Loose bridges have integral shallow entry ramps. The corner ribs extend beyond the floor edges, leaving designated climbing faces clear of projecting floor lips. Some special modules use wider or taller routes.

The first motor pass found that single-lane switchbacks blocked headroom near their ends. Parallel lanes fixed that. Further full-size capsule checks found and corrected low crown roofs, an obstructed waterworks carrier and catch-frame overhead clearance. The final geometry pass checked 11,132 ascent samples and 3,822 flat-turn samples with no obstruction. These are useful clearance checks, separate from the real movement tests.

One rigid section contains several floors and their bearing ribs. It can slide, topple and collide with other sections, but it does not break into individual window frames, stairs or bricks. This is the intentional destruction-granularity tradeoff. Fewer bodies alone do not establish a frame-rate improvement: contacts, shapes, bots, rendering and disasters still need the production measurements.

## City buildings

11. **Crownline Offices:** five setback sections, an open street podium and a violet crown. Upper piers bear on broad floor decks below.
12. **Lantern Court Apartments:** three independent wings around an open courtyard. Six narrow crossover bridges fit between the corner posts, rather than intersecting them. Separate base rafts meet without overlapping.
13. **Split Atrium Hotel:** two independently supported hotel wings and three loose atrium crossings.
14. **Switchback Parking Tower:** broad open decks with repeated stair/ramp slots and generous circulation surfaces. The first prototype uses shallow straight switchbacks rather than a vehicle-scale continuous spiral.
15. **Central Exchange:** two unequal towers above a sheltered concourse with broad mezzanine crossings. Each tower remains independently supported.
16. **Lantern City Hall:** a lower arcade, public crossing and taller lantern stack. The crown is an open architectural landmark; a detailed animated clock is not part of this structural prototype.
17. **Foundry Lofts:** wider work floors with three large sawtooth roof bays. Roof bearing height was corrected after the first penetration test.
18. **Skygarden Residences:** a tall central stack, lower shoulders and recessed gallery connections. The structural courts are implemented; decorative garden planting from the illustration is not simulated.
19. **Harbor Waterworks:** service tower and oversized hollow occupied crown. A broad integral carrier transfers the crown load onto the narrower tower instead of leaving the crown's outer columns unsupported. No water simulation.
20. **Gateway Headquarters:** two independent tall stacks with an inhabited, loose skybridge. Its lower bearing slab overlaps the roof seats; the narrower occupied part clears the towers' higher floors.

## Experimental towers

1. **Sky Tree:** six fabricated Y sections, each containing three inhabited wings and a central court connection. Three widely spaced real bearing lines support the repeated stack. Outer stair lanes avoid the crowded Y junction.
2. **Zipper Spire:** alternating cantilevered street edges around a centered bearing stack. The silhouette changes sides without relying on an unsupported offset load.
3. **Spiral Lantern:** faceted hollow shells, a real atrium, opposing ascent lanes and connected split galleries. The route is a practical sequence of switchbacks, rather than an expensive smooth helix.
4. **Cross-Block Relay:** six long inhabited bars alternate orientation. Four broad central bearing posts sit inside the actual crossing footprint, rather than expecting unsupported outer corners to carry the next bar.
5. **Cage Cathedral:** open faceted shells, large portals and integrated diagonal ribs. The diagrams' appearance is simplified to a small number of useful collision ribs.
6. **Drop-Stop Tower:** four catch frames, two-floor cassettes, removable shoes and an independent escape spine. Removing the first shoe pair produces about **1.499 m** of travel and a quiet stop on the backup geometry. Clearances prevent a tight sliding-guide mechanism.
7. **Saddle Beacon:** narrowing inhabited stages with inclined outer ribs and broad crown surfaces. An aligned inner bearing frame keeps the first prototype stable; the external ribs still become useful tilted surfaces after a fall.
8. **Chalice Stack:** six open sleeve sections with shallow side bowls and wide front/back spill openings. This deliberately avoids smooth closed bowls that trap debris.
9. **Bookfall Keep:** six inhabited hinged wings attach to a long movable spine, with their sites staggered along it. The initial vertically stacked wings jammed against each other; separating their sweep spaces fixed that. Each has a removable upper support and a lower physical catch. A released wing settles around **67.6°**, before the 80° joint limit, leaving its outer spine as an inclined surface. The receiving supports and hinge frames are dynamic bodies too.
10. **Kissing Towers:** two sheared stacks retain horizontal floors while their real ribs lean inward. A compression crown has broad upper shoulders so it cannot simply fall out of the narrowing opening. Removing it moved four other sections in the isolated test. This first compression prototype is lower than the illustration; stability and useful floors took priority over a speculative 72 m version.

## Evidence and scope

The isolated runner uses Unity 6000.3.23f1 native PhysX and the actual authored map/layout/member code, with the project's physics settings and a simple review ground/material setup. Its renders are explicitly labeled separately from the production Game scene. It is not a substitute for gameplay, bot-route, disaster or device testing.

The final isolated pass tested all twenty designs for 15 simulated seconds, and checked the ascent lanes and flat turns with a 1.7 m high, 0.24 m radius capsule. All had zero inter-body overlaps deeper than 0.04 m. Maximum settling displacement across the set was about 0.047 m; all bodies were sleeping at the end. The largest settling rotation was approximately 0.22° in the catch-floor assembly. Collider climbing registration, invalid layout-count rejection, replica kinematic mode and return to dynamic mode passed. Bot surface samples were checked for capsule clearance; those samples alone do not prove a complete route.

The production runner separately opens Game.unity, drives the real survivor motor through representative flights and landings, attempts a simulated VR climb, spawns 32 bots, and runs the real tsunami with default height, speed and force. It then resets the map for an independent demolition capture: one off-centre pulse per district, 22 m/s in X and 9 m/s in Z, applied 4 m above a lower section's centre of mass, followed by 14 seconds of ordinary physics. These are separate trials, not a claim that the normal tsunami always produces the demolition image.

The linked raw reports are the authority for production outcomes and timings. Desktop Editor FPS, if shown, is specific to this laptop, camera and run. No Quest headset or live two-player test is claimed. These large exhibition islands are exploration/profiling maps; they are not individually balanced finished maps for every game mode.

The focused production follow-up passed the office, Sky Tree and Kissing Towers routes. It uses 16 cm waypoint tolerance within landings several metres wide, gives the actor three setup frames, and selects an actual flight-bearing section instead of accidentally selecting Kissing Towers' raised base raft. Together with the full suite this covers ascent in all twenty designs. Both maps also passed a simulated VR climb across a floor band, with about 3.56 m of rise; this was not a tracked-headset test.

### Water-loading compatibility

The first default tsunami moved only 6/100 city sections and 1/104 experimental-tower sections. Investigation found that the existing wave divides acceleration by mass relative to a 40 kg reference block. A large fabricated section can expose the surface of many blocks while being treated as one heavy block, making it much too resistant for its surface area.

Only these new maps now attach `TowerSectionWaterLoad`, using the existing `IWaterResistance` contract. It calculates a cached projected envelope from the union of the section's shape rectangles in two directions; overlapping ribs do not repeatedly count the same projected patch. Mass-relative resistance is divided by exposed area relative to 12.25 m², with a minimum resistance of 0.65. Under the unchanged default 5 m/s² wave lift this floor keeps free-body lift below normal gravity. Real Rigidbody mass, gravity, contacts, support geometry and other maps are unchanged. There is no per-frame fluid solver or extra collider. This is a bounded gameplay approximation, not a calibrated hydrodynamics model.

The initial dry-movement and destruction evidence is retained in `Evidence/BeforeWater-*`. The final stress reports and tsunami pictures repeat the same default wave with the new cached exposure. Strong-impact pictures remain a separate trial. An ordinary wave is not expected to destroy every tall building.

The older raw idle timing includes its first phase-boundary sample, which can contain screenshot work from before the sampling window. It must not be treated as a reliable steady-state benchmark. The final stress runner omits that first sample; neither set is Quest certification or a controlled comparison against another map.

### Final production results

| Map | Bots spawned / moved | Normal wave completed | Sections moved over 0.5 m | Separate strong impact | Runtime errors |
|---|---:|---|---:|---:|---:|
| City Buildings 11–20 | 32 / 32 | Yes | 12 / 100 | 67 / 100 | 0 |
| Tower Laboratory 1–10 | 32 / 32 | Yes | 2 / 104 | 81 / 104 | 0 |

The exposure correction improved the original 6/100 and 1/104 normal-wave results, but it did not turn the ordinary wave into a skyscraper-flattening event. These are sturdy, heavy assemblies. The strong-impact boards demonstrate their physical collapse; the normal-tsunami captures show their actual more limited response. More disaster balance work would be needed if frequent collapse from the default wave is a requirement.

The final 32-bot Editor runs averaged approximately 15.9/14.9 FPS without the wave and 12.0/11.8 FPS during it (city/tower). Reducing moving-body count has not solved the whole game's frame-rate problem. These results are not Quest-ready performance, nor proof of a controlled improvement over older maps.

A final visual check corrected reversed ramp-face winding. It only permutes the existing convex points; signed volume changes sign while absolute volume, collision hull and physical mass remain identical. The native geometry, clearance, water lookup and compilation checks were repeated. Production stress images precede that small render-only correction; isolated bench images show the corrected normals. See `Evidence/RenderWinding.json` and the source hashes.

## Changed areas

- **World / Maps:** new deterministic layouts, cached section water loading and a map provider using the existing Core world, rebuild, traversal and synchronization contracts.
- **DevTools / Editor:** explicit map builder, isolated checks, production checks and reproducible camera captures.
- **Presentation / Visuals and Resources:** four materials, non-colliding numbered signs and map preview images.
- **GameSession / Session data:** two appended catalog entries. Gameplay scene files are not regenerated.
- **Core:** no change required.

No branch reset, merge, commit or push is part of this task; the existing shared working tree is preserved.
