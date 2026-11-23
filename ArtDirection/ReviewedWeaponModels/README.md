# Reviewed weapon models

The editable sources stay in the weapon review gallery. These are the source scenes and export modes for the Unity assets.

| Mode | Blender source |
| --- | --- |
| launcher | arc-06-action/inner-labels/Sugar Corps Open Hood - inner markings.blend |
| spikers | dual-spikers-needle-v2/Dual Spikers needle effects.blend |
| staff | saw-wheel-staff-4k/Saw Wheel Staff 4K.blend |
| chaingun | chaingun-looped-belt/Chaingun looped ammo belt.blend |
| alien | alien-dark-sight-saddle/Alien Blaster dark sight base.blend |
| sheriff | sheriff-gloss-cobalt/Blue Sheriff gloss cobalt.blend |
| sword | murderer-sword-garnet/Murderer Sword garnet.blend |
| rpg | single-barrel-polish/olive-charcoal/RPG charcoal.blend |
| axe | storm-battleworn/Battle-worn storm axe.blend |
| shooter | saw-wheel-shooter-garage/Garage Saw Wheel Shooter.blend |
| marshmallow | flaming-marshmallow-stock/roasted-detail/Flaming Marshmallow roasted.blend |

Run Blender in background mode with export_weapon_art.py and pass the mode and destination bytes file. The export consolidates the authored geometry and material colors, separates moving groups, and excludes the studio cameras, lighting and effects from the game. Unity uses the material finishes supported by its weapon shader and presents effects from accepted equipment actions.
