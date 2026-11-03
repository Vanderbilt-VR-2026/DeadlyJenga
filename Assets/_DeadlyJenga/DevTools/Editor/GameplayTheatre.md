# Gameplay Theatre

Open **Tools → Deadly Jenga → Gameplay Theatre**. Choose Showcase, Duel or Disaster, select a map and apply it to the recipe. Draft renders at 640×360 and 15 fps. Full render uses the recipe's dimensions and frame rate. Open last gallery loads a browser preview.

Theatre runs a local desktop Game scene. Save or close edited scenes first. Set the recipe's FFmpeg field to an absolute executable path if FFmpeg is unavailable on PATH. Each take keeps silent MP4s, PNG frames, the recipe and an execution log in a timestamped folder under Logs/GameplayTheatre.

## Direct a take

1. Use the lobby map ID. The stage starts near the highest active block tier. Adjust stageOffset in world metres to place the scene.
2. Give each cast member a unique ID, color index, start offset and ordered moves. A move has a start time, duration, destination and optional jump lift. Cast members use production survivor rigs. Set live to true and leave the moves empty to let a bot perform its normal gameplay behavior. Set customLook to true to apply the recipe's appearance instead of the bot's usual outfit.
3. Add a shot with start, duration, lens, roll, from, to and look. Offsets are relative to the stage, a named actor or the midpoint of two actors. Overlapping shots record the same running simulation.
4. Add timed cues. A punch stages the hand motion and attempts the real gameplay punch, recording the target's health change. Gear equips the cast with a named weapon. Fire sends a real attack from an actor toward a target and records whether the weapon fired. A collapse applies impulses to nearby dynamic blocks. The tsunami cue runs the production wave with adjustable heading and height. The hazard cue triggers a production disaster by prefab ID. The stopHazards cue ends active disasters.
5. Review a draft, then adjust the recipe before rendering the full take.

For CLI use the editor method DeadlyJenga.DevTools.Editor.GameplayTheatre.RunBatch with graphics enabled. Optional arguments are -theatrePlan, -theatreOutput, -theatreFfmpeg and -theatreDraft. Omit the plan to run the showcase. The method closes its own batch editor.

The GameplayTheatreRecipes folder includes a portrait chase, a Viaduct collapse and an alien blaster exchange.

The workflow uses simulation lead-in, alternate camera coverage and editable takes. See Epic's [Sequencer workflow](https://dev.epicgames.com/documentation/unreal-engine/cinematic-workflow-tips-for-sequencer-in-unreal-engine) and [multiple camera workflow](https://dev.epicgames.com/documentation/unreal-engine/rendering-from-multiple-camera-angles-in-unreal-engine).
