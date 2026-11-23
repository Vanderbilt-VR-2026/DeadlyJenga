# Sphinx Junction Unity structure handoff

This checkpoint ports the current editable town and pyramid scene into the existing Sphinx Junction map entry. Source Blender 5.3 file: **Sphinx Junction - Town and Pyramid.blend**, SHA-256 `F9A6E3FD008B14CB69174F44F0C0AB2C221C6A10EB9AB1F8660E7BC46B55F45C`. It is retained in the Sphinx Junction shared Blender-Map-Art-Pass Workboard folder. The broad full-height pyramid and two-sided street proposed by the later paintover are future art work.

The source export has 97 owner groups and 1,540 mesh pieces. Its map-local coordinates remove the Blender review scale and exchange Blender Z-up with Unity Y-up. Imported meshes use small separately simulated structure members. Attach trim to the corresponding authored body wherever the Unity map already has structural collision. The station and tack store have independent compound structure, and the modeled front stair is a separate walkable body. Four exterior bridge bents replace the original supports at the sculpture. The train follows the existing route as its elevation rises across the sphinx. Fixed joints represent connections at the front vault, crown, roof and tail. Blender silhouette overlaps between bodies are excluded only while those specific collider pairs intersect from the authored pose. The collider contact is restored once the pair separates. Player collision remains enabled.

To regenerate, run Blender in background mode on the named source, then pass the output path to the exporter. Keep the existing Unity asset metadata.

```text
blender -b "Sphinx Junction - Town and Pyramid.blend" --python-exit-code 2 --python ArtDirection/SphinxJunction/export_structure.py -- Assets/_DeadlyJenga/World/Maps/Resources/ModeMaps/SphinxJunctionStructure.bytes
```

Export SHA-256 `d499b72a1c03f8c7ba03fdef72129d1f34473062ffed9d633b521e512be3c82d`. Base candidate `3dd161ba99beadaf70cf89b42664d27c47f25f72` on `codex/sphinx-junction`. Integration target `origin/Evan_FINAL_Sprint`. No Core changes.

The targeted `DeadlyJenga.CastlePrototype.Editor.SphinxJunctionChecks.Run` fixture opens Game.unity in Demo and checks settling, train travel and riding, corridor clearance, bot and quicksand behavior, separate stone and timber support pulls, and reset. It records the report under `Logs/Validation/SphinxJunction.txt` and desktop captures under `Logs/Captures/SphinxJunction`. This is a candidate checkpoint subject to exact-revision desktop validation. Two-peer validation and Quest remain pending. See [draft PR #192](https://github.com/Vanderbilt-VR-2026/DeadlyJenga/pull/192) for the current candidate and test status.
