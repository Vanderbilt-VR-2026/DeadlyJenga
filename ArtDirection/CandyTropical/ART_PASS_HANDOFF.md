# Candy tropical environment — reusable art-pass handoff

Recorded 2026-10-13. The bare-bones art pass is implemented and applied to the local prototype. Read this file, `Assets/CandyTropicalArt/README.md`, and the bundled References before resuming; do not rely on conversation memory.

## User intent and scope

Create a cohesive sunny tropical candy/plastic look using the five concept illustrations as visual references. Ignore their structure, crate proportions, player placement, and other physical details. Preserve the approved playable structure. The first requested budget is a bare-bones 30-minute pass using at most 40 percentage points of the five-hour allowance; this is a target, not a guaranteed completion estimate. Check current usage again when starting; the earlier 60% used reading is historical. Monitor elapsed time and usage, reserve time to save and verify, and report any scope left unfinished. Model recommendation was GPT-6.1 Sol with default reasoning; actual choice belongs to the user.

## Visual targets

- Crates: glossy molded candy/plastic, broad clean highlights, rounded/beveled edges, simple raised panel/edge detailing. Retain regional red, green, and lavender/purple colors. Avoid gritty metal, heavy scratches, noisy roughness, or an overall material override that erases region colors.
- Sky: saturated azure blue, soft bright white clouds, sunny tropical horizon.
- Ocean: clear turquoise shallows, deeper blue toward the horizon, restrained animated ripples and bright caustic-like detail. Keep readability from the tower top and from inside the structure.
- Sandbar: pale warm cream, visible through the water, just submerged. Expose water level and sand surface height independently so the exact depth can be tuned; no fixed depth was specified by the user.
- Finish: consistent sunlight, ambient light, reflections, exposure, and restrained color grading. Evaluate the entire scene together.

Initial pass includes simple crate bevels/materials, basic water animation, submerged sand, sky/light tuning, and in-engine screenshots. Detailed bolts, expensive water/refraction/foam, custom cloud image generation, character art, hand lightning, destruction splash VFX, and verified headset optimization are later work.

## Physics and geometry contract

All crates are identical rectangular 6 × 2 × 2 metre blocks. The survivor was originally 1.7 metres tall; the user's subsequent half-size request changes it to 0.85 metres with a 0.8 metre eye height (Player Scale 0.5). Movement speed and jump height stay unchanged. The central keep alternates four cardinal crates with four diagonal crates at 45 degrees, repeating A/B without accumulated twist. Outer towers alternate separated parallel pairs rotated 90 degrees each course. Gaps and traversal ledges come from placement. Do not replace this with modeled hollow crates or redesign it from the concept images.

Ordinary players must not push the dynamic crates. Hands and other crates retain physical interaction. Preserve Rigidbody mass, collider dimensions, placement, survivor collision proxies, input, spawn, and vault behavior. Visual changes must follow moving blocks and support live appearance swaps without resetting their physics.

## Existing integration points

Project: Unity 6000.3.23f1, Universal Render Pipeline 17.3.0 at recording time. Recheck installed versions before implementation.

- Scene: `Assets/CastlePrototype/Scenes/CastlePhysicsTest.unity`.
- Physics prefab: `Assets/CastlePrototype/Prefabs/CastleBlock.prefab`.
- Current visual prefab: `Assets/CastlePrototype/Prefabs/Crate Visual.prefab`.
- Appearance profile: `Assets/CastlePrototype/Materials/Block Appearance.asset`.
- Contract: `Assets/CastlePrototype/Scripts/CastleBlockAppearance.cs` and `CastleBlock.cs`.
- Regional materials are assigned through `CastleGenerator` (`wallMaterial`, `cornerMaterial`, `keepMaterial`); inspect actual assignments before editing.
- Existing user artwork: `Assets/Assets For Texture (BETA)/`, including `Candy_Ocean.mat`, `Candy_Sky.mat`, `skybox-day.png`, and `y2k_water_texture.png`. Preserve the originals and create new variants.
- Project behavior and validation instructions: `Assets/CastlePrototype/README.md`.

The appearance profile supplies a visual-only prefab inside a centered unit cube. Its physics root scales it to 6 × 2 × 2 metres. Leave `materialOverride` empty to retain regional colors. `CastleBlock` applies regional materials to visual renderers, so use compatible materials and verify any intended multi-material details. The replacement art must have no active physics components.

Nonuniform root scaling also stretches bevels. Generate the visual shape in final crate proportions, then normalize positions into the unit cube (divide X by 6, Y and Z by 2), so bevel widths appear consistent after root scaling. Keep its bounding box and ledge positions aligned to the existing collider.

## Reproducible implementation and deferred application

1. Record current scene, appearance-profile references, material assignments, skybox, water objects, render settings, and lighting. Preserve user edits. Use a separate look-development scene or reversible prefab/profile variants.
2. Put all new assets under `Assets/CandyTropicalArt/`: Editor builder, visual prefab/mesh, three regional material variants, water shader/material, sand material, and environment prefab/settings. Keep gameplay code outside this art module.
3. Provide an explicit editor command to build/rebuild these assets. It must be deterministic, preserve GUIDs when updating existing assets, and never auto-apply on import.
4. Provide a separate explicit Apply command for the selected scene, plus a Restore command using a saved baseline. Apply swaps appearance and region materials and configures the art environment; it must not rebuild or reposition the castle. Store the baseline once and avoid overwriting it on repeated Apply.
5. Keep water/sand heights, colors, smoothness, ripple speed/scale, and sun settings configurable. Keep new environment objects beneath one identifiable root and make repeated application update that root instead of duplicating it.
6. Verify in Unity, save assets, and export `CandyTropicalArt.unitypackage` including required dependencies and `.meta` files. Include this brief, reference images, implementation notes, and screenshots in the handoff archive. Label the Unity package with the tested editor/URP versions.
7. To defer until late development, restore the captured baseline but retain the art module and export. Later import the package, inspect the target scene's integration points, then explicitly Apply. A changed gameplay scene may require adapting the integration references; do not overwrite it with the old demo scene.

Do not run `BuildTestAssets` or `Apply Confirmed Structure` as part of the art pass: those commands can regenerate prototype defaults and overwrite authored scene choices. Do not alter the separate nested `DeadlyJenga/` project. Inspect current user changes before any migration.

## Thirty-minute execution target

- First 5 minutes: inspect current scene and capture a baseline; establish reversible assets.
- Next 10 minutes: beveled crate visual and candy regional materials.
- Next 10 minutes: simple water, sand level, sky and lighting integration.
- Last 5 minutes: compile/render/play verification, screenshots, save and update the handoff with actual results. If implementation takes longer, reduce decorative detail and leave a clearly reported checkpoint.

These are planning allocations. Unity compilation and rendering can change the available time. Do not claim an untested feature or a guaranteed subscription cost.

## Acceptance checks and evidence

- No shader compilation errors, missing/pink materials, or missing dependencies.
- Correct red/green/purple regions in exterior, interior, and close-up views.
- Bevels and highlights visible at player scale; openings and usable ledges remain legible.
- Sand visible beneath shallow water without z-fighting; independently adjustable water/sand levels.
- Water animates in Play Mode; sky and horizon remain consistent across cameras.
- Player spawn, jump/vault, role switching, and hand impact still work. Run relevant existing reference/traversal checks if shared assets or gameplay are touched.
- Swapping appearance during live physics leaves body poses, mass, collider shape, and velocity intact.
- Apply twice does not duplicate assets/environment; Restore returns captured assignments/settings.
- Save exterior, interior, crate close-up, and shallow-water screenshots. Actual headset performance remains unverified unless tested on hardware.

## Resume prompt

“Read ArtDirection/CandyTropical/ART_PASS_HANDOFF.md and its References. Inspect current repo/user changes, then implement the bounded candy tropical art pass with reversible assets and explicit Apply/Restore. Preserve approved geometry and physics. Update the brief with implemented assets, exact settings, validation results, remaining work, and export location. Capture in-engine screenshots and export a dependency-complete Unity package so the visuals can be applied later.”

## Current delivery status

The implemented module is `Assets/CandyTropicalArt/`. Tools → Candy Tropical provides Build, Apply, Restore, Export, verification, and capture commands. `Exports/CandyTropicalArt.unitypackage` is the Unity import package; `Results/` contains actual in-engine screenshots and validation reports. The companion `CandyTropical-ArtBrief.zip` is a portable archive of this brief, references, results and export, not itself a Unity import package.

Default palette: Cherry Red (.94,.115,.17), Apple Green (.12,.78,.35), Lavender (.64,.37,.91), all nonmetallic with smoothness .65–.70. Warm Sand (.94,.85,.63). Bevel .12m, panel depth .035m. Water -0.12m; sand -0.32m; island auto-fits the foundation at radius .73 times its longest side, with aspect (1,.92). Ocean spans 1600m. Sun intensity 1.65, warm tint (1,.94,.83), Euler (48,-32,0). Colors are editable material values rather than frozen texture artwork.

Six art-specific checks passed, including unchanged state for all 568 crates, repeat-Apply idempotence and baseline restoration. Existing reference, traversal, spawn and isolated physics checks also passed after application. The tower still had zero initial penetrations over 1mm, no crates drifting more than .25m in ten seconds, and a 1.994m fall after removing the first corner's bottom pair. Actual headset performance and multiplayer integration remain unverified.

Future refinement: cloud shapes, richer molded edge/buttons, texture variation, shore foam/refraction and target-device optimization. This is the deliberately bounded first pass, not final reference-illustration fidelity. Recheck all results against the current scene and project versions when reapplying late in development.

Subsequent player-scale update: `CastleSurvivor.playerScale` is 0.5. Root transform stays at unit scale; the controller is 0.85m tall with 0.12m radius, desktop eye is 0.8m, and headset translation is scaled by 0.5. Ground probes, step offset and skin width follow body size. Walk speed and jump height retain their previous values. Seven scale/spawn checks plus 16 reference, 21 traversal and 13 input checks passed (57 total). The spawn check suspends user input while waiting so movement cannot invalidate its result. See `Results/PlayerScaleValidation.txt` and the corresponding reference/traversal/input reports. Hardware headset scale and comfort still require physical testing.

## Rolling ocean upgrade (current)

The ocean now uses GPU Gerstner wave displacement, analytic wave normals, flowing detail ripples, glossy procedural sky reflections, sun highlights, shallow caustics, sparse shore foam and horizon haze matched to the sky material. The default central 144m square uses resolution 96 (10,185 vertices / 19,200 triangles including the outer horizon ring), wave height .32m, speed .9 and chop .4. The fixed mesh is shared and waves fade before the distant ring. No reflection camera, screen-space refraction, CPU deformation, colliders, buoyancy or fluid simulation were introduced.

Use Tools → Candy Tropical → Upgrade Rolling Ocean in Edit Mode for an existing art installation, or Apply normally for a new one. The environment prefab includes the ocean component. Profile quality/motion and shared material colors remain independently editable; Restore removes the environment and preserves assets for later reapplication. Future arenas can use duplicated profiles and materials without rebuilding physics blocks.

Six installation checks and two Play Mode checks passed. All 568 block transforms, BoxColliders and Rigidbody settings were unchanged. Fixed-camera renders changed over time while CPU mesh vertices and the mesh reference stayed unchanged. The short editor run measured a median 19.83 ms frame interval with the whole demo running (about 50 FPS); this is not an isolated water/GPU benchmark, and headset performance remains unverified. See OceanValidation.txt, OceanPlayValidation.txt and candy-ocean-*.png in Results. The exported art package contains this upgrade.

The player-scale reports above describe the earlier scale change. Later candy avatar and jump/vault work lives in Assets/CandyPlayers and is separate from this ocean upgrade; its outstanding validation is not claimed as completed by these ocean checks.

## Current polish delivery

The coordinated refinement closes the player's torso, uses blended continuous limb skin and continuous palm/finger meshes, places the first-person eyes ahead of the neck/body, and preserves all tracked joints and the existing ledge-contact vault. All 44 bones remain; source/controller and 1mm finger precision checks pass. The terrain is now a closed 3-D sandbar with a level support plateau flush with the foundation top, rounded shoreline and sloping seabed. The original static support collider remains; no terrain Rigidbody or new physics colliders were added. Legacy ocean/foundation rendering is disabled. Water now uses clear depth-based lagoon color, flowing normal detail, gloss/glints and sparse crest foam, with caustics on the actual terrain.

Use Assets/CandyPlayers/README.md and the current IMPLEMENTATION_GUIDE.md for independent deferral/reapplication and configuration. The coordinated editor helper is source-project integration tooling, kept separately under Integration/CandyPolishUpgrade.cs in this handoff; it is excluded from the art-only Unity package so importing art does not introduce a player-module dependency. The player package targets this existing Fusion/VR project and its castle module; it is not a standalone game/project template. A Unity asset package does not install Package Manager dependencies. Import into a clean copy of the current project first and inspect the dependency/overwrite list when reapplying late in development.

Actual Unity screenshots cover the repaired first-person body, smooth tracked hand surface, outside character, vault hand contact, lagoon and terrain. candy-terrain-cutaway.png temporarily hides water during capture to expose the real terrain geometry, then restores it; gameplay retains water rendering. All 568 block transforms, colliders and Rigidbody settings, as well as the existing static foundation, passed preservation checks. Current automated avatar/reference/traversal/input checks passed. Headset comfort, hardware hand tracking, two-peer network behavior and target-device GPU performance still need hardware validation; the earlier ocean editor frame-time observation predates this refinement and is not a current performance guarantee.

## Latest user correction: natural grainy sand and clean water

Remove the repeating grid-like caustics and pixel/camouflage white crest patches. Keep the approved candy block/player appearance. The shoreline must extend modestly beyond the castle with a naturally rounded, uneven shape; a foundation-shaped snug pad and the original oversized disk are both superseded. The sand must have actual rounded relief and visible grain, with a molded Play-Doh finish. Preserve the initial block support height and all existing physics.

Implemented in CandySandbar, CandyEnvironment, CandySand.shader and CandyOcean.shader. The terrain now follows initial layout footprints, with flat support under blocks and softly sculpted dips/ridges between bases; it stays below support level to avoid interfering with players or blocks. A static height map makes water depth agree with the terrain. The revised softly oval outline is asymmetric and gently lobed, with modest room at the shoulders. All shape/quality controls remain in the shared profile and normal Apply/Restore remains available. Use Tools → Candy Tropical → Fit Molded Sand And Clear Water for the explicit latest default installation. Read the current section in IMPLEMENTATION_GUIDE.md for exact dimensions, mesh counts and limitations. The art Unity package includes this revision; players were not modified by it.

Nine installation checks and one Play Mode reuse check passed. Every initial bottom-block footprint corner met sand at foundation-top height; all 568 block transforms, colliders and Rigidbody settings and the original support collider were preserved. No new terrain physics components were added. Read LagoonPlayValidation.txt for the short whole-demo editor frame interval; it is not a headset or isolated GPU benchmark. Latest screenshots: candy-lagoon-tight, candy-clear-shallows, candy-water-from-above and candy-molded-sand. The final diagnostic sand image hides water only during capture, then restores it.
