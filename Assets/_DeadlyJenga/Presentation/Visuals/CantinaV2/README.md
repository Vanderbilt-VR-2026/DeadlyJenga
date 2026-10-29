# Amber Cantina — map and future lobby foundation

Choose **Demo → Amber Cantina** at the lobby mode/map terminal, then launch. The existing island lobby remains the entry scene. The separate `Cantina V2 Preview` scene is for environment review, not a replacement session scene.

The World prefab is `Mode_cantina`, with static collision and the existing world/layout interfaces. Its child `Visual` is a nested Presentation prefab. Removing that child leaves walkable gray-box collision. No Core changes or extra packages are required.

The starting layout is the user-supplied Cantina V2 reconstruction: six numbered bays, two auxiliary arches, a west-side bar, twin east-side tanks, seven seating groups, a recessed stage and its adjacent shuttered passage. Coordinates are metres, Y up, numbered doors at positive Z. The source floor is approximately 28 × 21 m. The map root is uniformly scaled to 0.5 to match Deadly Jenga's existing 0.85 m-tall avatar: the playable floor is 14 × 10.5 m, ceiling underside is 2.49 m and table tops are 0.56 m high. The source geometry is retained at its delivered scale. The scale and unseen connections are inferred rather than surveyed.

The roof, south closure and rear alcove/passage closures are authored additions. Doors are decorative and closed. No level-selection, hosting, terminal or wardrobe interactions are attached yet. Each major landmark remains a named group for later lobby placement. Demo-only selection avoids implying competitive-mode objectives have been authored for this interior.

Supplied native mesh data is preserved unchanged under Source. The original ZIP/GLB, rebuild scripts, guides and 27 reference screenshots are retained outside Assets at `Documents/Codex/CantinaHandoff/CantinaV2-Unity-Handoff`. Imported textures are the supplied independently generated 512 px PBR starters, with URP material hookup, mipmaps and compression. Reference screenshots are not used as textures.

The build adds continuous flooring, smoothed curved surfaces, secondary UVs, separate simple furniture collision, warm practical pools, cyan tank accents, magenta stage light, lightweight clear tank shells and an authored reflection capture. Lighting uses ambient fill and bounded realtime lights; it is not a finished indirect-light bake. VR limits practical lights to the closest four and uses no realtime shadows. This is a budget choice, not a measured Quest performance claim.

Tools → Deadly Jenga → Maps contains explicit authoring and capture commands. Build only creates a new map when it does not exist; edit the existing assets after that. Nothing is generated merely by loading Unity. Validation reports go to Logs/Validation. The separate runtime check uses the actual Game scene and survivor, records a natural player view plus five game-camera landmark views (including a roof-off cutaway), and preserves saved scene setup and match choices. It refuses to replace unsaved scenes.
