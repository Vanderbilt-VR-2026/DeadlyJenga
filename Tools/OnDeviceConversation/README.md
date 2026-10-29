# On-device conversation demo

In the running castle demo, open **Settings → Voice** and turn on **On-device AI conversation + microphone**. In VR use **VR movement / voice → On-device conversation + mic**. Enable it on the host. It uses the survivors already spawned; it does not respawn them, change their traits, or spawn extra bots. New survivors use the same shared runtime. Joined humans enable their own microphone through the existing voice controls.

Wait for **Ready**, then talk. The Voice panel shows microphone state and the last recognized sentence under **Heard**. **Generated replies** counts successful language-model answers; **Fallbacks** counts canned replacements after a timeout or empty result. **Last speech** identifies generated answers versus scripted game reactions. Native mode hides stale errors from the optional external speech server. Address a bot by name to select it; otherwise the closest eligible survivor answers. The desktop text field also sends a real conversational turn without a microphone. Off cancels pending replies, stops bot audio, releases models on the worker, and restores the prior voice settings. A microphone enabled by this toggle is stopped; an already enabled microphone is preserved.

## Developer setup / rebuilding

Run `python Tools/OnDeviceConversation/prepare.py` once from the repository root. It downloads checksum-verified pinned native releases, model weights, headers, and a portable build compiler into ignored local caches. It builds Windows x64 and Android ARM64 adapters with Unity's existing Android NDK; no Unity packages are installed. The official Qwen Q8 weights are requantized to Q4_K_M. Downloads are pinned in `downloads.lock.json`.

The build hook includes the prepared pack through Unity's additional StreamingAssets paths; it does not modify scenes, prefabs, Packages, or ProjectSettings. Without a prepared pack, normal builds still work and the toggle clearly reports that conversation is unavailable. The pack is about 460 MiB uncompressed plus native libraries. On Android the first activation copies and verifies files into persistent storage, requiring a second copy of the model pack on disk. No laptop process, paid API, runtime model download, or internet connection is needed for inference. Fusion multiplayer still requires its normal connection.

Run `python Tools/OnDeviceConversation/check_native.py` for actual model/STT/TTS/cancellation checks. Unity: **Tools → Castle Social → Run On-device Conversation Checks** (save scene edits and stop Play Mode first). Reports are in `Logs/Validation`. The development checks never capture the user's microphone.

## Design and limits

- One shared CPU language model, one streaming recognizer, and one voice model per active host. One reply at a time; bounded input queues, context, reply length, timeouts, and cached reaction audio. Native computation runs on a background thread with one CPU inference thread by default. It never writes movement, block physics, or player controls.
- Qwen3 0.6B Q4_K_M handles short human replies. Sherpa Zipformer 20M handles streaming English recognition. Kitten nano handles local speech. Existing trait profiles affect prompts and voice choices. This tiny model can misunderstand speech, give bland answers, or invent game facts; this is a practical prototype, not cloud-model conversation quality.
- Human speech interrupts generation and bot playback. Completed transcripts take priority over ambient reactions; successive similar questions bypass the old canned-reaction deduplication. Short recent conversation is kept in memory only and cleared when switched off. Existing demonstration recording remains a separate explicit setting.
- The frame-time guard defers new reply inference when rolling frame time exceeds its editable limit. It is not a guarantee of zero frame impact: recognition, synthesis, memory bandwidth, loading, heat, and CPU contention still cost resources. It can be disabled with the desktop guard slider at zero for controlled tests.
- Existing human PCM and host-authored bot audio use the existing Fusion social transport. Provider switches share audio sequence numbers so remote clients do not reject the new provider's packets as stale. No Fusion format or Core contract changed. A newly promoted host must enable its own on-device toggle; model readiness is not migrated between devices.
- No dedicated acoustic echo canceller is implemented. Headset speakers feeding back into the microphone need a Quest test; headphones are useful for initial laptop testing. Actual Quest frame time, battery, thermal behavior, microphone permissions, multi-user latency, and Android dynamic CPU backend loading remain device acceptance checks.

## Important redistribution limitation

The upstream **Sherpa 1.13.8 prebuilt TTS binaries include eSpeak NG / piper-phonemize, with GPL licensing implications**. Do not describe this full binary stack as Apache-only or commercially cleared. This is documented upstream in https://github.com/k2-fsa/sherpa-onnx/issues/3731. The authors plan a future dependency removal; no unreleased compatibility was assumed here. Before distributing a closed-source/commercial APK, replace/rebuild that speech backend without GPL components or resolve its license obligations. This local demo does not settle that review. Engines are isolated behind `OnDeviceNative` and the C ABI so this replacement does not require rewriting personalities, UI, or multiplayer.

Qwen and Kitten model licenses, the speech-engine license, ONNX Runtime notices, llama.cpp MIT license, OpenMP license, and the eSpeak GPL text are collected in `licenses/` and included in prepared packs. Source references and pinned dependency versions are recorded alongside them. Review model and training-data rights independently before release.

## Validation recorded for this implementation

Desktop native checks exercised real recorded speech, generated answers, non-silent speech synthesis, and cancellation. Unity checks exercised the actual Game scene, existing identities, audible-stream AudioSources, no HTTP jobs, the frame guard, and repeated toggles. The portable social suite has 79 checks. These are not substitutes for Quest or two-device Fusion testing. No APK was published by this task.
