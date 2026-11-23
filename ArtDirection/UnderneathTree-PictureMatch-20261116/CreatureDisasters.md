# Christmas creature disasters

Both use the existing four disaster controls in game. Warning delays contact while the animal is visible. Force controls physical impulses, damage controls damage per contact, and duration limits active time.

## Cat Attack

Select the CatAttack prefab in World/Hazards/CatAttack. Set Passes (1–3), Heading (degrees clockwise from world forward), Crouch Seconds, Run Speed or Pounce Height. Each pass crosses the middle of the play area. Further passes return along the same route. The cat compresses its legs, runs, springs on approach and lands before the middle. A target can take one contact per pass. Short durations compress travel to fit the requested passes.

## Evil Santa

Select the EvilSanta prefab in World/Hazards/EvilSanta. Choose Alternate, Grab Only or Slam Only. Seconds Per Strike sets the whole motion from ready through recovery. Reach limits how far from the resting mitten Santa can select a block. Lift Height changes the lift and slam wind-up. Santa alternates hands and selects one nearby attached block per beat. A grab frees and lifts the block; a slam hits the area once. The off-hand rests.

## Art and playback

Edit the Blender source in models/Christmas creatures.blend. Run Blender in background with models/export_christmas_creatures.py to regenerate the two FBXs and their shared palette. ChristmasCreaturePose applies the procedural poses to the rig in Unity. The authoritative hazard publishes the strike timing, positions and cat leap envelope. Presentation uses those samples for the visual rig; the mesh bones add no physics bodies.

The focused desktop fixture is DeadlyJenga.CastlePrototype.Editor.TreeDisasterChecks.RunCreatures. It exercises the production disaster entry point, captures the cat crouch/pounce and Santa lift/raised fist, and checks contact, block release and cleanup.
