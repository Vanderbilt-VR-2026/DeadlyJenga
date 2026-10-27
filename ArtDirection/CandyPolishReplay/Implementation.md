# Candy polish and Mystery replays

## Replay design
Presentation keeps a rolling 10-second transform/pose history at 10 Hz only during Murder Mystery. It records publicly visible actor poses and block transforms without rendering a camera, recording video, encoding files or simulating duplicate physics. Playback interpolates those samples, renders at up to 20 Hz into a 640 x 360 texture per view and shows a skippable desktop/VR screen. The headset view is never commandeered. During playback the normal eye camera renders UI only, avoiding a redundant live-world render behind the replay. Its original culling mask is restored on exit, interruption and camera changes.

GameSession selects the murderer and optional sheriff only after an authoritative death or completed round. A victim cue goes through the existing per-owner secret channel (15), not live public snapshots (12). The final cue is public only in Victory/Results. A sheriff kill produces two views. Holstered/hidden role information is not read by the recorder. Playback ends after the available history (up to ten seconds), on skip, on revival or a new round/mode/session. Short rounds naturally have less than ten seconds of history.

Approved additive Core vocabulary: MysteryReplayCue and IMysteryReplaySource, plus cue fields on the existing owner-private and public results packets. Zero sequence means no cue, including Unity JSON's inline-class default representation. Public live role-revealing cues are rejected by validation.

The recorder has limits of 4,096 transform nodes, 2,048 mesh draws and 102 samples. Pose data is about 18 MiB at the maximum node limit, plus visibility/eye data; a playing clip temporarily copies that history. Two textures add less than 3 MiB. Static decorative scenery is copied only at playback (up to 512 meshes). Materials/meshes are shared. No replay colliders, rigidbodies, particles, scripts or extra lights are cloned. Samples preserve transforms and skeletal bones rather than baking skinned meshes.

This is an economical reconstruction, not pixel-identical video: weather shaders use current animation time, incidental particles/audio are not recorded, and cosmetic changes made mid-clip use the recorded object's cached material. Very large scenes beyond the explicit limits omit excess visual detail. This is intentionally bounded rather than an unbounded Quest memory cost. Actual Quest comfort, memory and GPU cost remain untested.

## Controls and equipment
- Suit: activation takes off, then holds altitude. Desktop WASD steers, Space ascends, Ctrl descends, V shuts down to land. In VR the controller trigger plus pointing commands velocity/altitude; releasing holds a hover; trigger at the hip deactivates. Hand plasma input is preserved. Capsule collisions, input focus, pause/death and authority checks remain in use.
- Dual rifle: rapid shotgun fires every 0.20 seconds, seven pellets with a 0.15 directional spread and 0.10 m traces. Rifle remains 0.65 seconds with 0.075 m traces. Normal cover still blocks shots. Other weapons retain their cadence and traces.
- Both barrels/optics stay on a carriage that always turns clockwise 180 degrees. The rifle's knurled hollow optic also forms the underside pump when the shotgun is active. Shotgun red dot is nonmagnifying; rifle optic is 4x. Transition fire lock remains.
- Shared weapon visual paths give local/remote players the same diagonal upper-back rifle and upper-thigh pistol placement.

## Presentation
- Physical candy/plastic impact, brittle shell and soft settling sounds replace the previous vocal pain/death cues. Four original mono 32 kHz variations for pain, death and recovery; the synthesis source and peak/RMS report are in ArtDirection/CandyAudio. These are synthesized physical cues, not sampled human performances.
- Local damage has a soft red peripheral glow/streak texture, health response and existing haptics; restoration has green feedback and a short recovery sound. VR uses a wider peripheral panel without camera shake or postprocessing.
- Alien plasma uses pooled imperfect cloud shells and a stereo-capable procedural translucent shader, without collision particles, extra lights or downloaded textures.
- Pumpkin attachments have a closed ribbed shell with carved eyes and a jagged mouth exposing the original head only through those holes. The original avatar/body remains.
- Mode/host terminals are lower, wider and shorter. Layout dimensions are resized without stretching text or image proportions; touch hitboxes follow the resized buttons. Controls project out of vertical cabinet faces. Join is lowered to meet its keyboard, with larger button text. All three faces remain parallel.

## Evidence
Runtime.txt and the corresponding Logs/Validation files contain actual Game evidence, including a real murderer strike, private cue, rendered kill-cam, sheriff lethal shot and split-screen final replay. The lobby report/captures use the actual Lobby. Network evidence is explicitly a two-peer reliable-packet transport check; it is not two physical headsets or a perceptual comfort test. First failed fixture reports are retained for transparency.


Final verification: 55 replay/flight/equipment/health checks (including two independent Photon peers), 46 firearm regression checks, 11 actual lobby checks, 25 final appearance/feedback/rotation regression checks and 120 director contract assertions passed. All recorded runtime suites ended with zero runtime errors and restored scene setup. The empty-scene multiple-peer fixture diagnostic is called out explicitly in the network report.

Observed sampler-only mean: 0.684 ms for 571 transform nodes at 10 Hz in the small desktop fixture. This excludes discovery/playback rendering and is not an FPS uplift, large-scene guarantee or Quest measurement. New pumpkin capture also exposed and verified a fix for stale skin colors when the health overlay tears down and rebuilds.
