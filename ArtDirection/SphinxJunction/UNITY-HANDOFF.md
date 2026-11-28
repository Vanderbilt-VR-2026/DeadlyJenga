# Sphinx Junction Unity structure handoff

This checkpoint ports the current editable town and pyramid scene into the existing Sphinx Junction map entry. Source Blender 5.3 file: **Sphinx Junction - Town and Pyramid.blend**, SHA-256 `F9A6E3FD008B14CB69174F44F0C0AB2C221C6A10EB9AB1F8660E7BC46B55F45C`. It is retained in the Sphinx Junction shared Blender-Map-Art-Pass Workboard folder. The broad full-height pyramid and two-sided street proposed by the later paintover are future art work.

The source export has 97 owner groups and 1,540 mesh pieces. Its map-local coordinates remove the Blender review scale and exchange Blender Z-up with Unity Y-up. Imported meshes use small separately simulated structure members. Attach trim to the corresponding authored body wherever the Unity map already has structural collision. The station and tack store have independent compound structure, and the modeled front stair is a separate walkable body. Four exterior bridge bents replace the original supports at the sculpture. The train follows the existing route as its elevation rises across the sphinx. Fixed joints represent connections at the front vault, crown, roof and tail. Blender silhouette overlaps between bodies are excluded only while those specific collider pairs intersect from the authored pose. The collider contact is restored once the pair separates. Player collision remains enabled.

To regenerate, run Blender in background mode on the named source, then pass the output path to the exporter. Keep the existing Unity asset metadata.

```text
blender -b "Sphinx Junction - Town and Pyramid.blend" --python-exit-code 2 --python ArtDirection/SphinxJunction/export_structure.py -- Assets/_DeadlyJenga/World/Maps/Resources/ModeMaps/SphinxJunctionStructure.bytes
```

Export SHA-256 `d499b72a1c03f8c7ba03fdef72129d1f34473062ffed9d633b521e512be3c82d`. Base candidate `3dd161ba99beadaf70cf89b42664d27c47f25f72` on `codex/sphinx-junction`. Integration target `origin/Evan_FINAL_Sprint`. No Core changes.

The targeted `DeadlyJenga.CastlePrototype.Editor.SphinxJunctionChecks.Run` fixture opens Game.unity in Demo and checks settling, train travel and riding, corridor clearance, bot and quicksand behavior, separate stone and timber support pulls, and reset. It records the report under `Logs/Validation/SphinxJunction.txt` and desktop captures under `Logs/Captures/SphinxJunction`. This is a candidate checkpoint subject to exact-revision desktop validation. Two-peer validation and Quest remain pending. See [draft PR #192](https://github.com/Vanderbilt-VR-2026/DeadlyJenga/pull/192) for the current candidate and test status.

## Paintover Match physics-validation import (November 2026)

Physics validation only: this imports the unapproved Paintover Match art (full-height pyramid with the Hall of the Sun Line, the west street buildings and the south annex) so the map can be tested. Art promotion still needs Evan's approval.

Source: **Sphinx Junction - Paintover Match.blend** in the Workboard `Blender-Map-Art-Pass/sphinx-junction` folder, built by `paintover-match/build_paintover_match.py`. The original **Town and Pyramid.blend** is unchanged.

`sj_physics2.py` (copied here) tests the map in Blender under the Unity rules:
- one body per member, mass clamp(volume x 2, 4, 115); piles fixed
- designed bearing colliders: authored overlaps give way, supports are seated on what they rest on, small trim carries no collider
- the pyramid's stones and the carved sphinx are fastened to every stone they touch (mass x 190 N), as the map does at load
- hand pulls use the JunctionMember drive; meteor strikes use BasicDisaster.MeteorImpact's push

Run with `SJ_EXPORT=<path>` and it writes this resource through `sj_unity_export.py` instead of simulating:

```text
SJ_EXPORT=Assets/_DeadlyJenga/World/Maps/Resources/ModeMaps/SphinxJunctionStructure.bytes blender -b "Sphinx Junction - Paintover Match.blend" --python-exit-code 2 --python sj_physics2.py -- <out_dir> stand
```

The export holds each member's colliders as hidden parts (kind 2) and its Blender meshes as drawn parts (kind 0). When the structure has "Pyramid podium and hall floor", `SphinxJunctionMap.BuildBakedTown` builds every member from it. Only the railway and train are built in code. Trestle bents under the pyramid stand on the hall floor (map 2.57). Layout signature `sphinx-junction-8`.

Blender results on this export (Workboard `paintover-match/physics/unity-faithful-*.json/png`):
- Stand, 6 s: no member moves more than 0.5 m. 163 members settle over 10 cm, mostly pyramid stones bedding in.
- Trestle bent 14 hand pull: bent and span 14 fall.
- Saloon upper floor hauled east: it comes off and drops 3.2 m.
- Eight 3 m meteor strikes on the pyramid: 317 of 594 stones come down.
