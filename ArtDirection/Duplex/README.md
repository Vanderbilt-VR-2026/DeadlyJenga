# Duplex candy rifle and shotgun

The Blender revision named `Duplex compact optic clearance.blend` is the approved art source.
Its SHA-256 is `f2c625db4381205ff57edc3d3f4dd803b45048dde0cdc00396bb874a5aeeaa24`.

The Unity resource has six mesh sections. Opaque and glass sections are separate for
the receiver and its rotating carriage. Each mode label has its own mesh. Blender
Y-forward and Z-up coordinates are converted to Unity Z-forward and Y-up. The
runtime keeps the existing placement, rotation and sight socket names.

The Unity material reproduces carbon weave and candy buildup in object space.
Its splatter distribution is a runtime approximation of the Blender study.
Muzzle sparks, tinted travel and syrup marks reuse the existing cosmetic
shot events, trails and surface contacts.

To regenerate the mesh, open the approved source in Blender 5.3 and run:

```sh
blender -b "Duplex compact optic clearance.blend" --python ArtDirection/Duplex/export_duplex.py -- Assets/_DeadlyJenga/Characters/Player/CandyPlayers/Resources/DuplexVisual.bytes
```

Candidate branch: `codex/duplex-approved-art`, based on
`ee970756f9c18c212b1768b65fa6e30e29295664` from `origin/Evan_FINAL_Sprint`.
The focused Unity capture method is
`DeadlyJenga.DevTools.Editor.WeaponShowcaseCapture.RunDuplexAndClaws`.
Laser Claws are included for reference only. Their art is unchanged.
