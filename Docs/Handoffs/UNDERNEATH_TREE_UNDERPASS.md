# Underneath the Tree: approved train-underpass map

Owner: underneath-the-tree. Source branch: `codex/underneath-tree-sprint4`.
Integration target: `origin/Evan_Sprint_4` through its designated integration owner.
Base: `1359ca474fb8a0bc86cdd72dca7dab5e3e631a71`.
Gameplay-checked candidate: `af79cba0986fc333b1d02cab12eef0dd9690a55e` (published and remote SHA verified).
Status: work in progress. Candidate `af79cba0986fc333b1d02cab12eef0dd9690a55e` passed the exact desktop fixture, including actual material texture bindings. Visual fidelity remains below the approved concept; no final art approval claimed. Not integrated or included in an APK.

## Intended result

Map ID `workshop-under-the-tree`, layout `underneath-the-tree-v6-underpass`.
An irregular raised train circuit rests on presents and passes through the left gift tunnel. A long navy walking bridge connects asymmetric stepped towers above the courtyard. The tree remains outside and close to the loop. Seventy presents provide varied approaches, ledges and upper perches; their shells and sixteen rail sections are registered climbable physics bodies.

World/Maps owns the layout, physical shells, support-loss propagation and train path. Presentation/Visuals owns rounded wrapping, bows, foil, fabric, tree, room finishes and lighting. No Core changes, package changes or saved project-setting changes are required. The room uses a temporary copy of a map-owned URP profile (active-pipeline fallback) for room-scale shadow coverage and HDR, restoring the prior quality pipeline on teardown.

## Validation and remaining work

Run `DeadlyJenga.CastlePrototype.Editor.UnderneathTreePlayChecks.RunApproved` in a checked batch editor against the exact candidate. It loads solo Game.unity, verifies the layout and climbable bodies, captures overview/overhead/player-height views, checks Christmas shader compilation and clearance over the full rail course, removes bridge and rail supports, and verifies physics displacement plus deterministic reset.

- `a523dd756f16b77aacd90f347d52a86581bd2030`: desktop support loss, train movement, climbing registration and reset passed. An isolated art render confirmed the replacement scenery is present.
- `f1442159e88a545fbd3ebb243945c67e2446f595`: gameplay checks passed, but visual validation failed on a wrapping shader compiler error. Superseded; do not accept its successful gameplay result as art approval.
- `15db26426906ebf7ffd94655f6df68ed9b684828`: the new route sweep found five parcels intruding into the train corridor. Superseded by the narrowed/repositioned parcels in the current candidate.
- `4c7a7b5a23e16efc56c03ee8a13a80dd3f168af6`: desktop runtime passed all 440-body climbing/intact checks, every sampled rail corridor, both Christmas shader compiles, train motion, bridge/rail support collapse and deterministic reset. Overview, overhead and player-height captures were inspected. Art acceptance remains pending; the next refinement softens shadows and makes ribbon widths continuous across shell faces. Desktop captures are staged camera views of actual runtime geometry, not headset evidence.

- `5b1f3b5d7356df234cbf03ed86be78fe5dc84b75`: all desktop gameplay and shader checks passed again. Overview and corrected overhead captures inspected. Course and tunnel are visible, but fabric/foil detail and overall visual match remain below the approved reference. No visual approval claimed.

- `af79cba0986fc333b1d02cab12eef0dd9690a55e`: actual fine-foil and chunky-wool texture bindings, both shaders, all climbing registrations, train sweep/motion, bridge and rail collapse, and reset passed. Actual overview and player-height captures inspected. Gold mask clarity, rug pile and floor/canopy lighting improved. Tree, couch and overall image fidelity remain unfinished.

The user explicitly deferred headset performance work while the image match is developed. Quest 3 rendering/performance, human VR route playthrough, two-player destruction sync, central integration and APK inclusion remain unverified. This is not a publish-ready claim.
