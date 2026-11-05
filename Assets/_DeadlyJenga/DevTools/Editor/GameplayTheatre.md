# Gameplay Theatre

Open **Tools → Deadly Jenga → Gameplay Theatre**. Choose Showcase, Duel or Disaster, select a map and apply it to the recipe. Draft renders at 640×360 and 15 fps. Full render uses the recipe's dimensions and frame rate. Open last gallery loads a browser preview.

Theatre runs a local desktop Game scene. Save or close edited scenes first. Set the recipe's FFmpeg field to an absolute executable path if FFmpeg is unavailable on PATH. Each take saves MP4s with the Unity game mix, a PCM master (take.wav), synchronized PCM stems, PNG frames, the recipe and cue log in a timestamped folder under Logs/GameplayTheatre. The mix uses a stationary stage listener, so a cut or reframe keeps the same audio perspective.

## Direct a take

1. Use the lobby map ID. The stage starts near the highest active block tier. Adjust stageOffset in world metres to place the scene.
2. Give each cast member a unique ID, color index, start offset and ordered moves. A move has a start time, duration, destination and optional jump lift. Cast members use production survivor rigs. Set live to true and leave the moves empty to let a bot perform its normal gameplay behavior. Set customLook to true to apply the recipe's appearance instead of the bot's usual outfit.
3. Add a shot with start, duration, lens, roll, from, to and look. Offsets are relative to the stage, a named actor or the midpoint of two actors. Overlapping shots record the same running simulation.
4. Add timed cues. A punch stages the hand motion and attempts the real gameplay punch, recording the target's health change. Gear equips the cast with a named weapon. Fire sends a real attack from an actor toward a target and records whether the weapon fired. A collapse applies impulses to nearby dynamic blocks. The tsunami cue runs the production wave with adjustable heading and height. The hazard cue triggers a production disaster by prefab ID. The stopHazards cue ends active disasters.
5. Use the shot and cue controls to adjust timing, camera offsets, field of view and roll. Avoid blocks enables camera collision checks. Autofocus follows the shot target. The optional poses track adds crouch, lean and individual hand offsets to scripted actors.
6. Render a draft and scrub its frames in the editor. Add cut segments to trim and order shots into an edit.mp4 with the matching audio. Open the gallery to review the clips and download the WAV.
7. Use **Load take for reframe** on a completed take. Keep the map, frame rate, cast order and recorded time range. Adjust the shots, then use **Preview saved take in 3D** to scrub the recorded world with those cameras. Close the preview before exporting. The replay uses saved avatar poses, equipment state, block motion, wave state, projectile state, weapon events and particle samples. The original audio and stems carry through to the reframe.

## Record the round already running

Open Theatre during desktop Play Mode with a local match active. In the shot controls, select a live survivor or use the stage as the target. Enable **Live shot viewport** and scrub **Camera time** to preview the rail against the running game. Click **Record current match** to record for the duration set in the recipe. This keeps the existing map, round and actors and ignores staged cues. Recording uses a fixed capture clock. The match continues after export. Multiplayer clients and VR sessions are outside this capture path.

## Audio and replay limits

The stem links in the gallery are World, Character, Ambience, Music, UI, Voice and Other. Empty buses export silence. All stems start at take time zero and use the same sample count as the master. Load them together in an editor at the same timecode. The mixer asset ships with Theatre. **Tools → Deadly Jenga → Install Theatre Stem Mixer** can recreate it if needed.

Particle capture is bounded to 64 visible systems and 8,192 particles per frame. The take notes record the peak and flag truncation. World-space position, size and color are sampled. Runtime-only materials and unusual render modules can look different in a reframe. Weapons and particles captured before this update remain limited to the data present in their original take.



For CLI use the editor method DeadlyJenga.DevTools.Editor.GameplayTheatre.RunBatch with graphics enabled. Optional arguments are -theatrePlan, -theatreOutput, -theatreFfmpeg and -theatreDraft. Use -theatreCurrentMatch for the active-scene smoke test or -theatrePreviewCheck with a reframe recipe to render three timeline positions. Omit the plan to run the showcase. The method closes its own batch editor.

The GameplayTheatreRecipes folder includes a portrait chase, a Viaduct collapse and an alien blaster exchange.

The workflow uses simulation lead-in, alternate camera coverage and editable takes. See Epic's [Sequencer workflow](https://dev.epicgames.com/documentation/unreal-engine/cinematic-workflow-tips-for-sequencer-in-unreal-engine) and [multiple camera workflow](https://dev.epicgames.com/documentation/unreal-engine/rendering-from-multiple-camera-angles-in-unreal-engine).
