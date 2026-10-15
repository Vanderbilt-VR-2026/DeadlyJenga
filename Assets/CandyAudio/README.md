# Candy Audio

## Friendly sound editor

In desktop Play Mode, click **Sound studio** at the top right or press **F8**. The studio has eight illustrated categories, descriptions for all 35 sound sets, search, live bus faders and an edit popup. Each popup has a waveform, individual takes, preview/stop, next variation, clip replacement from the library, an optional 3D distance/direction audition, pitch/volume/range controls and reset. **Apply to game** refreshes already-playing loops; ordinary level changes are live. **Quiet game mix** ducks gameplay during preview. Escape closes the current popup, then the studio.

The editor works on a cloned session bank, including cloned cue settings. Project assets are not edited. **Save preset / Load preset** use `Application.persistentDataPath/CandyAudio/studio-preset.json`; loading is explicit. Presets store clip names and mix/settings, not audio bytes. Missing clips keep the existing takes and report a warning. Put additional imported clips in `Assets/Resources/CandyAudio/Clips` (or reference them in the source bank), then use Refresh library. One dedicated preview AudioSource is separate from the capped gameplay pool.

This first interface is for the desktop Game view, not a headset world-space UI. Maximize the Game view for comfortable editing. An editor menu also opens it: Tools > Candy Audio > Open Sound Studio (Play Mode).

Original cartoon sound palette and a bounded Unity audio dispatcher. Runtime audio is presentation only: it observes existing motors, contacts and emotes; it adds no colliders, forces, joints, rigidbodies, physics materials or gameplay movement. No scene/prefab edits are required. The bootstrap discovers CastlePlaySession and attaches observers to current generated blocks, survivors and hands, including later spawns/rebuilds.

## Swap/edit sounds

Select `Assets/CandyAudio/Resources/CandyAudio/Default.asset`. Each cue has replaceable clip variants, enable/mute, gain, pitch range, cooldown, voice limit, distance range, priority and optional AudioMixerGroup. Drag your own mono effects into Variants. The dispatcher avoids selecting the previous variant twice when multiple clips exist. Music, world, characters, ambience and UI have independent gains. These values are editable during Play Mode; persist asset edits after stopping as appropriate for Unity.

Duplicate the bank to create another sound style. Assign it to `CandyAudioEngine.bank` at runtime, or call Configure(newBank). Replacing a bank stops old voices and rebuilds the bounded pool. Existing gains are live; new one-shots use edited clips/pitch/range settings. Call Configure(bank) to restart already-playing loops with changed clips/settings or to change pool size. Default startup loads Resources/CandyAudio/Default.

Use `CandyAudioEmitter` on a future prefab/button and wire its Play method to a UnityEvent. Code can call `CandyAudioEngine.Instance.Play(CandySound.UiClick, position, intensity, ownerId, pitch, local)`. Spatial sounds use `local:false`; local music/UI use true. A repeated Loop request fades out after requests stop. Future XR UI should invoke UiClick on actual activation; the existing desktop demo currently has a general menu-click cue.

Optional `CandyAudioSurface` defines a sound surface/resonance without changing the physics material. Crates use the candy footstep; other ground defaults to sand. A future environment can set the appropriate surface tag or extend the observer's surface selection.

## Included coverage

- Light knocks, medium impacts, heavy smashes; normal contact speed and impulse-per-mass affect loudness, contact count/size affect pitch. Contact count approximates contact extent, not measured area. Duplicate crate-pair callbacks are suppressed.
- Low-volume rolling/sliding scrapes; rubber friction and touch squeaks at the giant hand's contact location, aggregated across its fingers.
- Cartoon timber/lean creaks grouped into 12m cells with 3s cooldowns; ocean splashes grouped into 5m cells. Splash detection crosses the ocean's mean shader surface; it does not simulate wave-fluid contact.
- Candy/sand footsteps, jumps, soft/hard landings, falling airflow, spawn/respawn cues.
- Four effort variants each for vaulting/climbing; suction-pop climbing hand placements, release pops and low-grip warnings. Plant timing is inferred from the public animated grip poses settling, without changing the climb motor.
- Four distinct nonverbal variants for each of eight emotes: greeting, smug celebration, relief, confusion, golf clap, chilling, panic and victory dance.
- Exposed beach wind, filtered/sheltered wind, nearby surf, giant-hand flight airflow, and an original ~34-second repeating mallet/bass/percussion soundtrack. Collapses gently duck the music.

## Runtime budget

Default maximum: 28 one-shot sources + 12 loop sources. Four loops are reserved for local surf, wind, music and falling airflow. Extra world sounds are culled rather than allocating more sources. One-shots are limited to 22 starts/second (world chatter stops when fewer than four tokens remain, leaving capacity for character/UI cues), per-cue concurrency/cooldowns and distance culling. Priorities protect important effort/reaction cues; rubber contact loops may replace lower-priority hand-flight wind when channels fill. Block-pair sounds are deduplicated; resting tower settling is suppressed for the first 1.5s after observer creation. Each hand uses one friction loop, not one per joint.

Environment exposure uses five rays about twice/second for the active listener only. This is a cheap shelter estimate, not acoustic propagation. Native spatialBlend/linear distance attenuation supplies positional stereo; a platform HRTF spatializer can be added later. No global audio/physics settings or player listeners are replaced. Real Quest profiling and listening tests remain necessary; bounded sources are not a performance guarantee.

## Verify and reapply

In Edit Mode: Tools > Candy Audio > Validate Bank. In Play Mode after one second: Tools > Candy Audio > Check Runtime. These write reports in ArtDirection/CandyAudio. The runtime smoke test intentionally produces a brief, bounded impact burst. Tools > Candy Audio > Export Audio Module creates a unitypackage; the source ZIP also includes this directory, generation source, clip manifest and notes. Import/copy the module into this project near the end of development to reapply; keep the CastlePrototype/CastleBots/CandyPhysicsHands types available. This is an internal project module, not an unrelated-project UPM package.

Tools > Candy Audio > Run Checks And Return To Edit Mode runs bank validation, enters the saved demo, checks real clip/loop playback and collision-storm culling, then returns to Edit Mode and exports. It is an explicit command, not a file watcher or automatic test on startup. Latest verification: 239 bank checks and 14 runtime smoke checks passed; both runtime/editor assemblies compile. File checks also passed for generated waveforms, unique variants and provenance hashes. Perceived mix quality, all individual physical triggers, Quest frame time and network events are not certified by this smoke test.

Generation source: ArtDirection/CandyAudio/generate_audio.py (Python + numpy). All 119 WAVs are original deterministic synthesis; no Mario/Nintendo music, samples, voices or external recordings are used. Existing files and edited bank settings are preserved by default. `--force` replaces generated clips/settings and should only be used deliberately. `CLIP_MANIFEST.json` records provenance and hashes. The WAVs can be edited in any audio editor/DAW.

## Integration limits / next sound passes

This module observes the local demo. Networked one-shot event transport is not implemented here; future authoritative multiplayer events should call the same dispatcher once per client and disable any duplicate local observer for those events. Additional future cues worth adding with their game rules: hand near-miss whooshes, elimination/round-result stingers, low-health breathing, timer countdown and gentle off-screen danger warnings. Avoid adding constant alarm sounds until the survival rules exist.

Design references: Unity Collision.impulse, AudioSource.spatialBlend/priority and AudioMixer documentation; GDC “Materials vs. Audio: Object Interaction Sound Design in Halo.” The effect library is a synthesized first pass, not a claim of final studio Foley or recorded character performances.
