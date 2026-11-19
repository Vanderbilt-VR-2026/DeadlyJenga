# Avatar and hand fixes — November 5, 2026

Owner: Characters/Player and hand visuals. This note is a documentation checkpoint, not a declaration that all VR behavior has passed.

## Changes already integrated

- `CandySmoothHandMesh.SeparateDigitWeights` no longer increments the retained influence slot twice. It preserves valid palm and same-finger influences while removing influences belonging to another finger. The six-case native regression is `DeadlyJenga.DevTools.Editor.HandWeightRegression.Run()`; its report is `Logs/Validation/HandWeightRegression.txt`.
- The baked body mesh replaces only the two disconnected hand components. It has 37,803 vertices and 74,872 triangles, 44 bind poses, and 5,259 retained non-hand vertices. Native generation checked retained vertex bytes, triangle order and bind-pose bits. The flying hand has 16,262 vertices, 32,520 triangles and its original 16-bone mapping. The detected distal cross-finger triangles fell from 162 in the body and 82 in the flying hand to zero. The original asset GUIDs and local IDs were retained.
- `CandyPlayerRig` no longer applies a second fixed hand calibration after equipment has positioned the hands. The tracked and equipment wrist anchors remain shared by local and remote views. Its shoulder and elbow pole also account for reaching across the chest without moving the wrist target.
- `CandyPlayerAvatar` retains the natural-hand motion cache, room-scale horizontal body placement and equipment/emote composition. Tracked optical joints are not replaced by the desktop fallback pose.
- Normal avatar posture is upright for both seated and standing comfort controls. The normal avatar frame no longer sets its legacy seated flag from VR mode, and the rig no longer forces the visual head to height 1.27 or projects the feet forward by 0.29. The compatibility property and wire field remain present. Head tracking, the yaw-only body root, floor-relative placement, scale compensation, crouching, airborne/climbing/vaulting poses, emotes and downed poses retain their existing paths. This does not clamp or move the HMD camera.

The hand meshes are `Candy Body.asset` and `Continuous Hand.asset` under Characters. Do not rebuild the entire avatar or replace their metadata when updating their geometry.

The published Sprint 4 source also includes the supplied weapon-model revisions in `CandyWeaponVisual.Foundry.cs` and `CandyWeaponVisual.Arsenal.cs`, with the matching support-anchor path. Those files are not an outstanding source-integration gap. Their inclusion is separate from the still-required current rendered grip and finger-fit checks below. The earlier RPG images and 23 passing baseline checks used the older `c1b62820` source and must not be relabelled as acceptance of later model or support changes.

## What the evidence establishes

- Six native weight-filter checks passed. This tests weight retention, normalization, source preservation and cache reuse; it does not establish rendered hand quality.
- The native scratch mesh/rig regression passed 135 checks. The generated candidates were also inspected in matched open-hand, curled-hand, complete stock-XR-simulator pinch, flying-hand and mirrored fixture renders before installation. The stock pinch capture passed 43 checks and contained complete tracked skeletons rather than edited tip positions.
- Installation compared native vertex streams, index buffers, submesh descriptors, weights, bind poses and bounds with the accepted candidates, and checked the unchanged GUIDs, local IDs and metadata. Both installed meshes had zero detected distal bridges.
- A fresh-editor test passed 21 checks with ordinary Game-camera rendering of a real spawned bot at near, far and near distances. The full body was visibly rendered at both near stages, and the reduced-detail body was visibly rendered at the far stage.

These are separate results. The older 135-check pose report predates the upright posture change and must not be used as proof that the new standing poses are correct. The mirrored flying-hand scratch is not evidence of physical left-hand tracking. No physical Quest test is claimed here.

## Live mesh replacement and skinning errors

The editor that received the live mesh replacement later logged 50 skinning size/stride errors during a weapon test, and 132 during the focused bot diagnostic. Those failures were retained. In the focused case, the raw full-detail mesh failed near the camera, the reduced-detail renderer worked at distance, and returning to full detail reproduced the error.

Restarting that same editor/project/cache and running the exact same source and fixture removed the errors. The full-detail mesh then rendered normally; CPU mesh layout and skin data were unchanged. This supports stale editor-native skinning state after live replacement, rather than an established persistent geometry defect.

If this recurs, preserve the failing report and compare the same source and fixture in a fresh process before changing valid geometry. Inspect the actual enabled Game-camera renderer at full detail and after LOD transitions. CPU buffer inspection, an Edit Mode preview, or `BakeMesh` alone does not prove that the live renderer is healthy. Do not add a per-frame renderer reset or undo the topology fix merely to conceal a warm-editor error.

## Checks still required

- Actual first-person look-down and external upright-body views in both comfort modes, including extreme head pitch/roll, floor/hip/foot alignment and flying-hand-to-survivor transitions. For a normal living survivor, verify the feet remain below both the tracked head and rendered head, and the body root stays upright. Exclude deliberate emote, crouch, climb, vault and ragdoll poses from neutral-stance assertions.
- Current independent-peer rendered hands, body stance and held weapons. A same-process serializer or API test is not a substitute.
- Actual latched two-handed RPG grip, left/right aiming parity, finger wrapping and dual-Uzi muzzle/shot alignment. The earlier RPG baseline used the dominant aiming grip, not the opposite support grip, so its passing checks do not close the support-hand request.
- Physical Quest controller/optical tracking, stereo presentation, comfort and final-build acceptance.

The existing cached `CandyWeaponIcons.Get` sprites cover all 20 weapon kinds and the shotgun alternate graphic. Their earlier import/HUD-binding checks do not establish the readability or input behavior of a new tablet or weapon wheel.
