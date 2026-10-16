# Architecture and decisions

## Reconnaissance

Work began on Evan_Sprint_3 at 27c761c44. Uncommitted Tools/ArchitectureMap work
was present and is outside this implementation. Unity is 6000.3.23f1, URP 17.3,
XR Hands 1.7.3, Input System 1.20 and XRI 3.3.2. The enabled build is the standalone
CastlePhysicsTest scene; separate Lobby/Main assets use Photon Fusion. There was
no existing session voice transport or speech-model stack.

The castle already provides an authoritative host LAN session, replicated avatar
frames, live block snapshots, safe survivor surface proxies, a CharacterController
motor, stepping-stone/journey bot navigation, procedural candy avatars and emotes.
CastleLanSession disables local bot populations on joining clients. Reuse those
systems: a second generator, NavMesh, network framework or movement motor would
increase compatibility risk while losing dynamic-block traversal behavior.

## Boundaries

| Layer | Code | Responsibility |
| --- | --- | --- |
| Portable core | Core/SocialDistribution, SocialProfiles | Calibrated distributions, copula sampling, permanent identity and voice assignment, mappings |
| Portable simulation | Core/SocialSimulation | Events, emotions, bounded attention/memory, affinities, friend topics, speech bids/budgets |
| Portable audio | Core/SocialAudio | PCM validation, bounded jitter buffer, stateful microphone DSP |
| Host runtime | Runtime/SocialWorld | Explicit session owner, real event observations, authority gating, identity/voice routing |
| Gameplay adapter | Runtime/SocialBotAgent | Existing navigator/motor input; identity-based tuning and friend destinations |
| Audio | Runtime/SocialMicrophone, SocialVoicePlayback | Permission-gated capture/VAD, per-speaker sources, local mute/volume, spatial-ready output |
| Inference client | Runtime/SocialSpeechClient | Optional async/chunked speech and incremental transcription, bounded requests, stale cancellation |
| Service boundary | Tools/SocialVoiceBridge | Windows preview TTS; opt-in Kokoro/faster-whisper/Ollama without Unity dependencies |
| Tooling | Editor/SocialPopulationWindow, SocialValidation | Profiles, graphs, previews, scenario harness, real Unity/LAN checks |

The portable core has no Unity, HTTP, model or network dependency. Speech is text
intent plus identity/context, never executable gameplay commands. Voice transport
uses authenticated connection IDs and bounded extra channels on existing links.
Service replacement uses the same HTTP chunk/PCM contract or ISocialSpeechOutput;
no model is embedded in a Quest player.

## Major choices

**Utility-style social bids plus existing heuristic navigation.** This provides
explainable behavior with measurable trait effects and zero training dependency.
ML-Agents is a possible later policy adapter; consented pose/event demonstrations
are available without collecting voice. No learned policy has been trained.

**Calibrated quantile distributions rather than mislabeled normal sliders.** The
median is the true quantile midpoint and SD is solved against moments. Extreme
variance configurations and non-PSD correlation matrices fail validation. Skew
and bounds are explicit; the distribution shape is not marketed as scientific
personality validation.

**Host simulation, off-device inference.** Built-in gameplay continues when the
service is unavailable. Twenty heavy model instances on Quest would be both
wasteful and unreliable. A single optional speech bridge can serve the session,
with two inference slots, cached reactions and per-speaker stream routing.

**Extend LAN first; avoid adding LiveKit during this slice.** Existing mapping,
late joining and authority remain intact. PCM is inspectable and testable with no
native codec or server credential dependency. It costs more bandwidth and has
TCP head-of-line limitations; a future WebRTC/Opus implementation should preserve
participant IDs and the independent speaker interface.

**Offline captions and an honest basic speech backend.** Windows installed speech
can prove real HTTP → PCM → Unity output on this machine without downloading
weights. It does not establish natural neural prosody or a vast voice bank.
Kokoro and faster-whisper were subsequently installed in an isolated local
environment and exercised with actual Unity audio. Warm uncached neural speech
still takes several seconds on this laptop; short pre-generated reactions use a
separate bounded cache. Generative conversation and subjective naturalness still
need their own evaluation.

## License / dependency evaluation (checked October 16, 2026)

No third-party source/model is vendored by the new module. Optional components:

| Candidate | Evidence / license | Decision |
| --- | --- | --- |
| [LiveKit Unity](https://github.com/livekit/client-sdk-unity) | Apache-2.0 SDK; upstream lists Unity 6000.3 and Android support | Viable later voice transport; not installed or platform-certified here |
| [faster-whisper](https://github.com/SYSTRAN/faster-whisper) | MIT implementation; [tiny.en conversion](https://huggingface.co/Systran/faster-whisper-tiny.en) is tagged MIT | Tested CPU int8, local model directory; incremental windows are not native full duplex |
| [Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) | Model card: Apache-2.0, 82M parameters, approved voice bank | Tested locally on CPU and through Unity; dependencies have separate licenses |
| [Chatterbox](https://github.com/resemble-ai/chatterbox) | MIT repository; verify exact selected weights/model card before shipping | Optional expressive alternative; not installed/benchmarked; no real-person cloning |
| [Piper](https://github.com/rhasspy/piper) | Archived MIT project points to OHF-Voice/piper1-gpl | Avoid silent GPL dependency; any future use needs explicit licensing review of code and chosen voices |
| [Moshi](https://github.com/kyutai-labs/moshi) | Python MIT / Rust Apache; weights CC-BY-4.0; upstream documents substantial GPU needs and limited Windows support | Optional future full-duplex research; not a default Quest/host requirement |
| [ML-Agents](https://github.com/Unity-Technologies/ml-agents) | Apache-2.0 toolkit | Defer training until consented demonstrations and baseline comparison justify it |
| [uLipSync](https://github.com/hecomi/uLipSync) | Upstream open-source plugin; verify license/version when actually adding it | Not installed: current featureless candy faces have no authored mouth shapes |
| Windows System.Speech | Uses locally installed operating-system voices; not an open-source neural model | Development smoke-test backend; no voice files redistributed; review OS/voice usage terms before shipping generated assets |

Research support for model/platform capability is upstream evidence, not a claim
that the project has been tested with those optional engines. Commercial release
must audit selected versions and model/voice licenses; a package's code license
alone is not a license to arbitrary voice weights or real-person likenesses.

The tested optional English phonemization stack includes **phonemizer-fork GPLv3**
and [eSpeak NG's GPLv3 distribution](https://github.com/espeak-ng/espeak-ng/blob/master/COPYING).
Do not infer that the entire optional Python stack is permissively licensed from
Kokoro's Apache model license. It remains an independently run development service;
the environment/binaries are not bundled into Unity or committed. Any redistribution
of that service and its dependencies needs a component-specific license/notice
review; this is not a closed-source shipping clearance. Exact tested Python versions
are recorded in Tools/SocialVoiceBridge/requirements-tested.txt.
