# Creator toolkit

## Art and camera pilot

Run the Blender generator with an explicit output directory. Keep Blender source outside Assets and import the FBX for Unity.

    blender --background --python-exit-code 1 --python build_tycoon_pilot.py -- --out <output-directory>

The included TycoonPilot.fbx is the generated review model. Use **Tools > Deadly Jenga > Creator Toolkit > Run Tycoon Flume Pilot** in an isolated Editor. The batch entry point is DeadlyJenga.DevTools.Editor.TycoonCreatorPilot.StartPreview. Pass -creatorOutput with a new directory through the project's checked Unity launcher.

The review uses five Blender mesh parts, eight simple box colliders for the flume and one for the terrace, an independent rigidbody release, a Cinemachine camera, and a two-bone contact check using Animation Rigging. Outputs include stills, the probe path, and editor frame samples. The steering probe checks the route geometry with an assisted physics body. Use Gameplay Theatre and the production map tests for character movement, disaster physics, and actual match capture.

For staging a performance, use the existing **Stunt Motion Import** window to bake a rigged animation onto the Theatre cast. For manual shot authoring use Cinemachine cameras on Timeline, Animation Rigging constraints for contact poses, and Unity Recorder for output. The pilot's scene is created on demand. Use **Record Tycoon Flume Pilot** to write a 720p, 24 fps MP4 via Unity Recorder in the interactive Editor.

**TycoonFlumeLanding.json** is a separate two-shot Theatre recipe for the production Tycoon map, with authored rider poses at the west runout. Open it in Gameplay Theatre or pass it to GameplayTheatre.RunBatch as -theatrePlan. Add -theatreDraft for a short preview. Use -theatreFfmpeg with the path to FFmpeg when it is unavailable on PATH.

## Warm Editor

Use `unity pipeline editors` to confirm the expected Editor is running with Pipeline loaded. Pass --project-path with the exact checkout to unity command. Follow the same worktree ownership and admission rules as the checked batch launcher.

OpenAI Game Studio is for browser prototypes. Keep them outside the Unity project and use the official Unity Codex plugin for Unity work.

## Profiling

Use the Unity Profiler with a development APK. Save matched baseline and stress captures from the same headset and compare them with Profile Analyzer. Take Memory Profiler snapshots outside the measured interval. Measure frame pacing again on the release APK with Meta Quest Developer Hub and OVR Metrics. Use the Performance Analyzer in Meta Quest Developer Hub to record a Perfetto trace when the timeline needs system or GPU scheduling context.

Collect-QuestEvidence.ps1 captures device identity, package, process memory and recent Logcat from an authorized ADB connection. Run it after the workload. It requires an explicit ADB executable and a new output directory, plus a serial if more than one authorized device is attached. Keep the output local since Logcat can contain other apps' messages.

## Network stress

Launch both development players using the existing CrossProcessSmoke flags and append --dj-network-stress. The opt-in profile configures Fusion's local simulation for 75 ms delay, 20 ms additional jitter and 2% packet loss. It requires the Fusion debug DLLs. Compare it with the same peer test and map under ordinary network conditions. Check that both peers enter the same map and return to the same room. The player log confirms the profile was configured. Measure actual pose gaps before drawing conclusions about delivered latency.
