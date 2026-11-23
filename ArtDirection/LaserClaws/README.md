# Player-colored Laser Claws

Both equipped claw sets read the owning survivor rig's appearance color. The
plasma shader uses it for its body tint, hot core, and animated shed sparks.
The sparks use the blade mesh draw calls and expand only the visual bounds.
The existing between-finger blade geometry and draw motion are preserved.

This candidate changes Characters/Player, Characters/Network visual plumbing,
and the Presentation material shader. It does not add network fields or make
gameplay or Core changes.

Base: `origin/Evan_FINAL_Sprint` at `ee970756f9c18c212b1768b65fa6e30e29295664`.
Candidate branch: `codex/laser-claws-sparks`.
The retained-reference C# precheck passed 16 assemblies. Unity graphics,
local/remote visual comparison, and Quest checks are pending.

## Contact feedback

The existing Shot receipt drives three fading ribbons in the attacker's blade
color. An accepted Impact receipt adds a tight player-contact flash or, when a
nearby solid surface is resolved, a brighter burst, sugar chips, and a temporary
three-line surface mark. These effects reuse the bounded ribbon, ordnance
particle, and instanced surface-mark pools.

The existing WeaponEventChecks fixture covers accepted air, block, and survivor
attacks, including a mark on the block and no surface mark for the player.
Run through `DeadlyJenga.DevTools.Editor.FirearmUpgradeChecks.RunWeaponEventsOnly`.
The Blender comparison is an art reference. Unity Play Mode and Quest evidence
are pending.
