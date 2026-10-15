# Candy tropical art module

Bare-bones visual pass for the castle prototype. Tested with Unity 6000.3.23f1 and URP 17.3.0. The artwork uses procedural shared meshes and shaders; no texture downloads or extra Unity packages are required.

## Apply, defer, reapply

Use **Tools → Candy Tropical**:

1. **Build Reusable Assets** creates missing assets and preserves existing asset GUIDs and tuning.
2. **Apply To Selected Castle** styles the selected `CastleGenerator` (or the only generator when unambiguous), saves its scene, and captures its previous appearance and lighting. Repeated application updates one installation. It does not rebuild the castle.
3. **Restore Previous Appearance** restores captured assignments, original water/foundation renderer visibility, ambient light, sky, and sun. It saves the scene and removes the art environment. All art assets remain available for later application.
4. **Export Unity Package** creates `ArtDirection/CandyTropical/Exports/CandyTropicalArt.unitypackage`, with referenced dependencies.
5. **Apply And Verify Art Pass** applies/restores/reapplies in `CastlePhysicsTest`, compares physics states, and runs the existing Play Mode reference/traversal/physics checks. It saves reports and screenshots under `ArtDirection/CandyTropical/Results`.
6. **Capture Art Views** captures the scene plus an isolated material swatch. The swatch is temporary and has no physics components.
7. **Upgrade Rolling Ocean** installs the GPU wave surface on the environment prefab and currently loaded candy environments, saves the scene, checks physics preservation, captures ocean views, and exports the package. Apply this in Edit Mode.

Apply works on selected castle generators in other saved scenes too. The prototype integration currently recognizes its named `Ocean (visual only)` root and its generator foundation; inspect those references when integrating into a different scene. Multiple independently lit arenas need a scene lighting policy because Unity RenderSettings are global to the active scene. Restore currently restores every loaded installation; use one target scene at a time.

## Live tuning and scaling

Select **Candy Tropical.asset**. Its shared profile exposes bevel width, panel depth, corner buttons, regional materials, water/sand heights, island dimensions, ocean extent, and sunlight.

- **Bevel and panel measurements are in world metres.** The visual geometry adapts to block size, remains inside the existing box collider, and shares one mesh per matching size/style. Changing a configuration does not require authoring a new crate model. Large numbers of unique sizes/styles increase mesh count.
- Change **Red / Green / Purple** material properties directly during Play Mode. Region material replacement references take effect on Apply or the next generator rebuild; editing their colors/smoothness/textures updates current renderers immediately.
- The existing **Candy Appearance.asset** also supports live visual-prefab or global material swaps. Leave its Material Override empty for regional colors. Its visual model contains no collider or Rigidbody.
- **Fit Island To Foundation** derives island radius from the actual foundation size, including later generator changes. Disable it for explicit radius control. Island aspect permits alternate footprints.
- **Water Height** and **Sand Height** are independent offsets from the environment root. Defaults are -0.12m and -0.32m: 0.20m of submersion. These are configurable artistic starting values, not gameplay water-contact heights.
- Water material exposes shallow/deep colors, ripple scale/speed, and glint strength. Sky material exposes blue/horizon/cloud colors and cloud coverage.
- Shader animation uses Unity time. The environment updates renderer property blocks, not shared water-material island coordinates, so separate environments can have different island positions and sizes.

For an alternate art direction, duplicate the profile and its materials, assign it on a replacement visual prefab and `CandyEnvironment`, and use that appearance profile on the intended blocks. The supplied Apply command uses the default profile; its integration can be extended to select a profile for each arena. A new profile's region assignments must be applied through the generator or per-block section material.

## Physics boundary

The module changes artwork and renderer/lighting assignments. Existing block positions, rotations, scales, colliders, Rigidbody settings, physics materials, player collision proxies, input, spawn, and vault scripts are retained. The exported physics block prefab is a separate copy of the source prefab with the candy appearance assigned. The source prefab is preserved.

The sandbar and ocean have no colliders. The original foundation collider remains active even though its gray renderer is hidden. Gameplay water death/respawn thresholds are not adjusted by the art water height.

Graphics still affect frame time. Sharing meshes and instanced regional materials reduces duplication, but headset performance and visual comfort need hardware validation. Water has no screen-space refraction, shore splash simulation, or fluid simulation.

## Rolling ocean upgrade

`CandyOceanSurface` supplies one cached static mesh: a detailed central grid and a cheap outer ring to the horizon. Three Gerstner waves displace vertices in the shader; analytic wave normals, small flowing ripples, glossy stylized sky reflections, sun highlights, shallow caustics and sparse foam provide the candy finish. Reflections are procedural rather than extra reflection-camera renders. Ripple detail fades with distance and screen-space derivatives to limit sparkling at distance.

Tune **Water Resolution**, **Wave Extent**, **Wave Height**, **Wave Speed** and **Wave Chop** on the shared profile. Default resolution 96 has 10,185 vertices / 19,200 triangles over a 144m detailed square. Use resolution 48 or 64 for a cheaper grid. Resolution changes rebuild topology once; animation never uploads a deformed mesh each frame. Waves fade to flat before the outer ring, and distant water blends to the sky's horizon color. In the shallows, amplitude drops to preserve sandbar submersion with the default heights. Increasing wave height or reducing sand depth may expose sand, so tune those together.

The water renderer remains purely visual: no colliders, buoyancy, gameplay water threshold changes, or block/player physics changes. Colors, gloss, foam and caustic intensity stay editable on the shared water material. Duplicate the profile/material for other arenas and assign the profile on `CandyEnvironment`; its ocean surface receives the same profile automatically. Apply/Restore and the portable Unity package retain this upgrade for later development.

## Portable handoff

Read `ArtDirection/CandyTropical/ART_PASS_HANDOFF.md` for reference images, original requirements, and the resume prompt. Import the exported Unity package into a compatible project, inspect the dependency list in Unity's import dialog, then apply explicitly. No project scene is automatically opened or restyled on import.

The editor verification helper responds to the optional local `ArtDirection/CandyTropical/run-art-pass.request` file for this development workflow. No pending request is included in the delivered archive. Normal use is through the Tools menu.

## Smooth player and solid sandbar refinement (current)

Use Tools → Candy Tropical → Smooth Players And Solid Lagoon to explicitly install the coordinated refinement and run its checks. This command rebakes the player visual meshes, tunes the lagoon, installs solid terrain on the environment prefab and loaded environments, preserves the original physics foundation, exports both modules and runs the avatar/traversal/input validation. For later custom profiles, use the normal Apply/Restore commands and edit the profile instead of rerunning the default polish tuning.

The sandbar now has a level central support plateau, rounded shoulder, sloping seabed and closed bottom. Its top follows the existing foundation collider's top, which is the initial base of the blocks. It adds no colliders or Rigidbody components. Radius follows the foundation's longest side times .92; Sand Plateau, Sand Depth and Sand Segments control terrain shape. It has approximately 2,702 vertices / 5,256 triangles at the default 72 segments. Water sits at .08m above the environment root; on the normal foundation this leaves the support area shallowly submerged. The legacy ocean and gray foundation renderers stay hidden, while the original static collider remains active. The broad visual seabed outside that original support collider does not create additional walkable ground.

Water has a new clear-lagoon material treatment: depth-based turquoise/blue tint, fine flowing surface normals, candy gloss, restrained sun glints and gentle crest foam. Caustics shade the terrain beneath it rather than painting the old pattern onto a flat water sheet. Both effects use procedural shaders without texture downloads or reflection cameras. Detailed GPU waves retain the existing grid and quality settings. The wave cache discards unused meshes during edits and disable/reload so repeated quality tuning does not retain every old grid.

The player torso is closed, limbs are smoothly skinned, and hands use continuous baked surfaces with all 44 body/finger bones retained. The first-person camera sits ahead of the neck/body instead of inside the torso opening. Bone orientation and finger weighting reduce twisting and unrelated-finger influence. Read Assets/CandyPlayers/README.md for independent installation, rebuilding, deferral, dependencies and network integration.

Results/PolishValidation.txt covers unchanged physics, unchanged static support, shader compilation, 3-D terrain, flush support height, legacy renderer visibility and preserved joint skinning. CandyPlayers/Results contains controller, tracking, vault, traversal/input checks and actual rendered body/hand screenshots. candy-terrain-cutaway.png temporarily hides water only during capture to expose the real sandbar geometry; normal gameplay still renders the water.

## Current natural-island revision

This section supersedes the earlier radial/plateau terrain settings. The sandbar now fits the castle's initial layout using a gently irregular, soft oval outline rather than a broad disk or a rectangular foundation-shaped pad. Defaults: Sand Margin 2.35m, shore width 1.5m, relief .8m, depth 4m and terrain resolution 128. The full mesh bounds, including its submerged skirt, are approximately 43.42 × 42.62m for the 30.32m castle footprint; the visible shoreline is smaller and irregular. It provides modest sandy shoulders around the walls and sufficient support at the corners.

The flat areas directly beneath the initial bottom-block footprints remain at foundation-top height. Between them, rounded relief is cut down from that support level so neither sand hills nor visual geometry intrude into block/player colliders. Sand grain and warm, softly lit shading create the molded Play-Doh appearance. The closed static mesh has 17,154 vertices and 34,304 triangles. A generated 129 × 129 RFloat height map lets water depth/color follow the real terrain rather than an old radial approximation. The mesh and map rebuild only when shape/layout settings change; their references are reused during ordinary wave animation. No terrain Rigidbody or new collider is added.

The crisscross caustic pattern is removed entirely. Thresholded white crest noise and crossed sparkle patterns are also removed. The ocean retains GPU waves, smooth filtered ripple normals and broad restrained sun reflection. Sand grain fades with distance and screen-space derivatives to avoid turning fine detail into a pixel pattern.

Use Tools → Candy Tropical → Fit Molded Sand And Clear Water to explicitly install the latest default environment refinement, verify unchanged physics/initial support, capture views, export the art module and run its brief Play Mode check. It does not rebuild or restyle the player or blocks. Old Sand Plateau / Sand Segments fields remain serialized for earlier profiles; the current generator uses Terrain Resolution, Sand Margin, Sand Relief, Sand Shore Width and Sand Depth instead. The current implementation targets the prototype's unrotated, unit-scale generator; adapting a different world transform requires matching the terrain coordinates and height-map bounds.

Results/LagoonValidation.txt and LagoonPlayValidation.txt cover this revision. candy-molded-sand.png temporarily hides water only during the diagnostic capture to reveal the terrain surface; ordinary screenshots and gameplay render the water. Actual headset GPU performance remains unverified.
