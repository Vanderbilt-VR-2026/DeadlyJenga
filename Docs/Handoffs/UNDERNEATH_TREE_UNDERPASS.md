# Underneath the Tree: approved train-underpass map

Owner: underneath-the-tree. Source branch: `codex/underneath-tree-sprint4`.
Integration target: `origin/Evan_Sprint_4` through its designated integration owner.
Base: `1359ca474fb8a0bc86cdd72dca7dab5e3e631a71`.
Gameplay-checked candidate: `cf6288c57c8ece233240aefefce5e243d78d61dd` (published and remote SHA verified; exact desktop runtime passed).
Status: work in progress. The broader collision audit supersedes earlier narrow-clearance acceptance. Visual fidelity remains below the approved concept; no final art approval claimed. Not integrated or included in an APK.

## Intended result

Map ID `workshop-under-the-tree`, layout `underneath-the-tree-v6-underpass`.
An irregular raised train circuit rests on presents and passes through the left gift tunnel. A long navy walking bridge connects asymmetric stepped towers above the courtyard. The tree remains outside and close to the loop. Eighty-two presents provide varied approaches, ledges and upper perches; their shells and sixteen eight-rung rail sections are registered climbable physics bodies. Bodies remain kinematic while intact and become dynamic on impact or bearing loss; they are not freely simulated stacks at rest.

World/Maps owns the layout, physical shells, support-loss propagation and train path. Presentation/Visuals owns rounded wrapping, bows, foil, fabric, tree, room finishes and lighting. No Core changes, package changes or saved project-setting changes are required. The room uses a temporary copy of a map-owned URP profile (active-pipeline fallback) for room-scale shadow coverage and HDR, restoring the prior quality pipeline on teardown.

## Validation and remaining work

Run `DeadlyJenga.CastlePrototype.Editor.UnderneathTreePlayChecks.RunApproved` in a checked batch editor against the exact candidate. It loads solo Game.unity, verifies the layout and climbable bodies, captures matched overview, closer hero, overhead and player-height views, checks Christmas shader compilation and clearance over the full rail course, removes bridge and rail supports, and verifies physics displacement plus deterministic reset.

- `a523dd756f16b77aacd90f347d52a86581bd2030`: desktop support loss, train movement, climbing registration and reset passed. An isolated art render confirmed the replacement scenery is present.
- `f1442159e88a545fbd3ebb243945c67e2446f595`: gameplay checks passed, but visual validation failed on a wrapping shader compiler error. Superseded; do not accept its successful gameplay result as art approval.
- `15db26426906ebf7ffd94655f6df68ed9b684828`: the new route sweep found five parcels intruding into the train corridor. Superseded by the narrowed/repositioned parcels in the current candidate.
- `4c7a7b5a23e16efc56c03ee8a13a80dd3f168af6`: desktop runtime passed all 440-body climbing/intact checks, every sampled rail corridor, both Christmas shader compiles, train motion, bridge/rail support collapse and deterministic reset. Overview, overhead and player-height captures were inspected. Art acceptance remains pending; the next refinement softens shadows and makes ribbon widths continuous across shell faces. Desktop captures are staged camera views of actual runtime geometry, not headset evidence.

- `5b1f3b5d7356df234cbf03ed86be78fe5dc84b75`: all desktop gameplay and shader checks passed again. Overview and corrected overhead captures inspected. Course and tunnel are visible, but fabric/foil detail and overall visual match remain below the approved reference. No visual approval claimed.

- `af79cba0986fc333b1d02cab12eef0dd9690a55e`: actual fine-foil and chunky-wool texture bindings, both shaders, all climbing registrations, train sweep/motion, bridge and rail collapse, and reset passed. Actual overview and player-height captures inspected. Gold mask clarity, rug pile and floor/canopy lighting improved. Tree, couch and overall image fidelity remain unfinished.

- `ed9a7a0e8a6b219f44569bb89bde0bc683f9f435`: quality pass adds eight supported upper parcels (488 registered climbable bodies), fuller sculpted bows, braided wool geometry, crisp alpha-cutout fir foliage, damask albedo/normal, cushion seam deformation, tailored shade, walnut drawer table and richer bulb colors. Exact desktop fixture passed material bindings, shader compilation, full rail sweep, train movement, support-collapse displacement and deterministic reset. Actual hero and player-height renders inspected. Palette classification error found in intermediate `e95cf284` was fixed in `c8bd7b07`. Generated texture sources are 1254-square assets with provenance; captures are 3840x2160, not a claim that every texture is native 4K. Final visual approval is pending.

The user explicitly deferred headset performance work while the image match is developed. Quest 3 rendering/performance, human VR route playthrough, two-player destruction sync, central integration and APK inclusion remain unverified. This is not a publish-ready claim.

## Full-room and collision refinement

`a5ea0f2d3930f2e0375ef1237b8c1ba6e862ee93` replaces the broad invisible railway deck with actual rail and rung colliders and adds reverse-room, reverse-pile, east/west and player-height captures. Its physical penetration check found six intersections on four presents. These were corrected; a subsequent train clearance check caught an overhanging upper parcel. A temporary capture-code CS0103 in `c5eb6ae2` was corrected in `c117b9c2`.

`4c3878f8cadf144b3f9c9518e9e2b2e65b1020fc` passed rail penetration, train corridor/motion, bridge and rail support release, direct eight-rung rail release, shader compilation and reset. Its new diagnostic ALSO found widespread inter-gift shell overlaps. That result is not whole-map collision acceptance. Reverse captures exposed stretched duplicate wall finishes, which this revision replaces on all four sides. It adds warm terrace/blue window light, softer couch patterning, bridge-end terraces and tapered hearth flames.

`79204e0557a6972b62089c0343869cd256d1f81d` separates the overlapping approach steps and upper shelves and shortens/narrows rail-bearing presents. The fixture now requires zero inter-gift penetrations above 0.1 world units as well as rail penetrations below 0.035. The exact desktop fixture passed both penetration checks, train clearance/motion, climbing registration, shader compilation, support-loss displacement, direct eight-rung detachment and deterministic reset. Ten intact camera views and one destruction view were captured. Capture evidence is staged desktop Game.unity, not headset playthrough. A following visual-only refinement suppresses bows buried by upper parcels; it does not change colliders or physics.

`cf6288c57c8ece233240aefefce5e243d78d61dd` passed the same complete fixture after buried-bow cleanup, with 512 registered climbable bodies. Actual overview, reverse, side, overhead, player-height and release frames were inspected. Remaining visible gaps include tree silhouette and foliage quality, fireplace detail, floor appearance, room proportions and the overall reference lighting/material fidelity. This is a checked improvement candidate, not final art acceptance or a publish-ready map. Reproduce via the fixture named above; no private tooling is required to understand its assertions.

## Team Kill and Natural Disaster mode setup

Mode-setup source candidate: `41d5281c6d1d3f4818254a7a443fe63d8925c723` (pushed; remote SHA verified), on the same owned branch and integration target above. Depends on the approved-underpass map lineage through `2827f788e1eb65075d9e2babe8aa701d2638a21f`.

GameSession/Session adds this map to the normal Play pairs for `teamtickets` (Team Kill) and `survival` (Natural Disaster). It marks the living-room ground walkable and preserves that choice in the Team Kill Play profile. Existing team assignment, combat, tickets, equipment, population, round transitions and disaster scheduling remain the existing modes' responsibility.

GameSession/Modes adds an authored layout matching `underneath-the-tree-v6-underpass`: eight separated approach groups of sixteen positions, interleaved for distributed Survival starts. Initial spawns and ticket respawns share the existing occupied-space and reservation checks. This layout opts into a bounded two-unit static-ground projection so rug/wood height differences do not invalidate starts. Existing layouts retain their authored heights. Spawn locations are outside the rail route and clear of the gift terraces; the ground remains available after presents collapse.

No custom disaster prefabs, map disaster profile, timing or rigging was authored. No Core, networking transport, package or project settings changes. The current generic disaster behavior is unchanged; custom disaster acceptance is deferred to the user's design.

Validation entry point: `DeadlyJenga.CastlePrototype.Editor.UnderneathTreeModeChecks.Run`. It loads the real Game scene separately in each mode, audits every authored start against live colliders, verifies actual spawn resolution, opposing team assignment, live bots and player survival after setup. It does not drive player inputs or claim a human navigation/respawn playthrough, peer synchronization, headset or APK result. Mode setup has not been integrated into the shared target yet.

Runtime result: both modes passed on this exact candidate in checked job `47d11fe077bc45e48acce0c7dc1aa68f`. All 128 positions have support and clearance, both runtime spawn resolvers use authored starts, opposing teams are assigned in Team Kill, and real players/bots remain alive after setup. See [the retained test report](UNDERNEATH_TREE_MODES_CHECKS.md). Prior runs caught rug/floor height differences and terrace/room-edge obstructions; these were corrected rather than accepted as generic spawn fallbacks.

Custom disaster follow-up: [Tree disaster implementation and validation](UNDERNEATH_TREE_DISASTERS.md) supersedes the earlier custom-rigging deferral. Six events now pass desktop runtime; creature visual acceptance, peers, headset and integration remain open.
