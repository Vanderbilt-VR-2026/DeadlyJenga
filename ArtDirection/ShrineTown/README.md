# Shrine Town reviewed Blender port

Runtime map identity: `mode-ninja-temple`, layout preset `shrine-town`. The serialized match layout, spawns, hazard profile, water loss rules, and surrounding town placement come from the existing branch. The delivered Blender source is the reviewed hall and arrival. The outer town in the Blender study is context geometry, so the production market, towers, ocean, traversal and wildlife remain the owners of those areas.

Blender 5.3 source: [Shrine Town - Reviewed.blend](./Shrine%20Town%20-%20Reviewed.blend), SHA-256 `e88228a265b00c4197a1d893873d6b95c50070da08d6481fb94fb041d2e464c2`. Copied byte-for-byte from the owner review, Review 02. Source checkpoint before the port was `54516eb4357198e3f8c5d1ca30b6ad793a1a6177`.

From the repository root, bake with `blender -b "ArtDirection/ShrineTown/Shrine Town - Reviewed.blend" --python-exit-code 1 -P ArtDirection/ShrineTown/export_hall.py -- <repository-root>`. The 47 exported meshes use the canonical order in ShrineReviewedStructure.cs. They contain evaluated geometry, normals and vertex tint. Unity uses its existing lacquer shader and converts Blender's procedural materials to the runtime palette. Review lights and the art scene's duplicate lake are excluded.

The first 47 runtime bodies own the plinth, 22 stair treads, dais, lower roof, upper roof/gallery, 18 posts, and three independent beams. Each body carries its assigned art if it moves or breaks away. The existing timber-joint policy connects each beam to the outside posts. The hall floor bears the posts and dais. Compact survivor-grade surface proxies cover the hall routes and representative roof slopes. They approximate the tile curvature and omit individual decorative fittings.

The desktop fixture is `DeadlyJenga.DevTools.Editor.MayhemMarinaChecks.RunNinjaOverview`. It checks both map identities, settling, standard spawns, channel elimination, shoreline behavior, reviewed stair and aisle surfaces, open portal, body ownership, and roof failure after support removal. Multiplayer and Quest coverage are separate.
