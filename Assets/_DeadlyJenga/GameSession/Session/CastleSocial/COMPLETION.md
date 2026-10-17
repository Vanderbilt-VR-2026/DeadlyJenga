Current integration note (2026-10-16): the implementation and benchmarks below
record the pre-main baseline. The project now launches through main's Photon
lobby; social voice is connected to Fusion, and the authored local-development
voice settings are preserved. See ARCHITECTURE.md and Logs/Validation for the
current merge validation. Hardware and multi-headset checks remain separate.

# Sprint 3 implementation status

This is a working integrated baseline, **not a claim of complete human-level
conversation or finished multiplayer voice production readiness**. The regular
survivor population receives the new system. Flying-hand bot AI is unchanged.

## Implemented in the playable castle

| Area | Actual behavior |
| --- | --- |
| Population authoring | Native Unity studio with ten pages, saved ScriptableObject profiles, JSON import/export, deterministic seeds, distribution graphs, per-identity inspection and relationship preview |
| Personality generation | Eight extensible traits, calibrated median/SD/bounds/tail shape, validated copula correlations, independent skill/age/demographic assignment, voice probability and whole friend groups |
| Gameplay integration | Generated traits tune the existing legal navigator; safe walking acceleration varies with skill; fear changes risk; friend seeking submits ordinary route goals; no teleport or new block force authority |
| Shared context | Tower motion, falls, vault success, greetings and speech feed bounded emotion, attention, relationship and memory state used by movement and speech |
| Conversation | Contextual reaction composition, relevance bids, typical 1–3 respondents, cooldowns, bounded queues, stale cancellation, independent friend topics, bounded bot follow-ups and human VAD priority |
| Voice transport | Opt-in microphone, authenticated host relay, independent global AudioSources, client captions/AI identities, bounded audio queues, per-bot mute/volume and global human/bot controls |
| Local speech | Optional HTTP streaming bridge with actual Windows speech synthesis; CPU/GPU ASR and neural TTS/local language-model adapters are separate opt-ins |
| Failure behavior | Offline captions/gameplay, timeouts, cancellation, explicit missing-provider errors and bounded budgets; invalid live configuration restores a valid population |
| Demonstrations | Explicit-consent 10Hz pose/locomotion-event JSONL, capped at 30 minutes/session; no player voice training data |

The default remains Offline with microphone capture disabled. Starting a model
service or choosing a profile does not grant microphone permission. Runtime
settings use a copy of the authored asset. All participants need the same LAN-2
build; the separate Photon Lobby/Main flow is not migrated.

## Evidence and practical limits

Reports live in ArtDirection/CastleSocial. The portable suite exercises the exact
shipping core source, with 50,000-sample distribution tests and 10,000-identity
population tests. The transport suite uses actual sockets. Unity integration
uses the real castle, six moving survivor bots, a separate flying hand, two
loopback human peers, real UDP requests/invitations and TCP world/audio delivery.
The optional speech run exercises HTTP → PCM → Unity playback rather than a
mocked provider. See the reports for current counts and measurements.

October 16 validation: **66 portable checks, 50 real Unity integration assertions,
18 socket transport checks**, plus the existing headset view (25), seam/camera
(15), climb (23) and slope (10) checks passed. Neural bridge checks also passed
with actual Kokoro synthesis and tiny.en CPU ASR. These are automated engineering
checks, not subjective human-believability ratings.

The standalone social core measured 0.065ms/tick for 100 identities over 3,000
ticks in the saved run. Neural uncached first audio measured 3.20–3.39 seconds;
the pre-generated acknowledgment returned its first HTTP PCM chunk in 0.005s.
That cached number excludes network playback buffering and is not fresh dialogue
generation latency. Unity's cached speech test measured 0.185s to the first audio
frame. Warm final-window ASR measured about 0.8s. Model startup and the first-ever
asset download are additional costs. Measurements vary with machine load.

The 100-bot benchmark measures only the standalone social core. It is **not**
a 100-avatar Unity/Quest frame-rate benchmark. Physical bot capacity keeps the
existing demo limits. No claim is made about a headset microphone, multi-device
Wi-Fi reliability, packet-loss latency, acoustic echo cancellation or Quest
render/physics/inference performance until those are measured on hardware.

Windows preview voices come from this machine's two installed voices, with
per-identity rate/pitch/mic variation. Eight configured neural voice IDs do not
mean eight distinct installed Windows voices. The isolated neural service actually
loads eight Kokoro voices and 48 pre-generated short reaction clips; two generated
listening samples are saved with the reports. Naturalness and expressive quality
still need listening tests. Genuine scream/laughter recordings and a native
full-duplex model are not bundled.

## Partial or future work

- Dynamic topics/attention and friend affinities are lightweight heuristics,
  not a learned social cognition model. Merging/splitting discussions, sustained
  inside jokes, strategic deception, reconciliation and long-term grudges need
  richer policies and human evaluation. Session memory is bounded; cross-session
  identity/history persistence is not enabled.
- Human priority defers queued statements and ducks active speech consistently
  across peers. It does not yet reconstruct and resume an interrupted sentence
  with new prosody. Incremental recognition windows are not full-duplex speech.
- The optional local language-model path receives bounded current-event,
  personality, relationship and retrieved-memory context. No LLM is installed
  or silently downloaded by the game. General conversational quality remains
  unvalidated until an actual model is selected and tested.
- Body language reuses the existing avatar/emote system and legal input driver.
  There are no new facial blendshapes, phoneme lip animation or trained gestures.
- Demonstration export is working infrastructure, not a trained imitation policy
  or a completed learned-versus-heuristic study.
- LAN PCM preserves the project's connection model and avoids a competing
  network stack. Production Internet voice needs tested Opus/WebRTC transport,
  echo cancellation, secure service authentication and session isolation.
- DedicatedService selects an external inference endpoint; it does not create
  a separate headless game server or move navigation authority away from the
  castle host. ReducedResource suppresses inference/audio requests, rather than
  dynamically detecting device load and training/selecting a smaller policy.
- Basic text filtering prevents common instruction leakage/obvious abuse; it
  is not comprehensive moderation. AI remains labeled in player controls.

## Believability evaluation still required

Use matched short sessions with the original navigator and the generated social
adapter, identical tower seeds and equal bot counts. Randomize condition order
and have consenting raters score hesitation, context relevance, repetition,
friend coherence, interruption quality and overall believability on 1–5 scales.
Record run seed, build, voice backend, hardware, latency, failures and aggregate
scores; keep AI disclosure in actual player-facing sessions. Do not claim a
blinded result from an automated unit test. Tests/SocialSimulation/BELIEVABILITY.csv provides a blank
rating template; no invented participant ratings are supplied.

## Saved direction for future conversation work — October 16, 2026

Keep standalone Quest gameplay, ordinary bot decisions and lightweight cached
reactions available. Prefer an optional nearby-computer service for richer speech
recognition, language generation and synthesis, avoiding recurring API/token fees.
Do not assume full on-headset conversation is performant alongside castle physics.
If revisiting headset-only inference, benchmark one shared quantized model around
0.5–1.5B parameters, short context/replies and one generation at a time; that is
an experiment, not a verified Quest configuration. Preserve replaceable service
boundaries and evaluate frame time, memory and thermals on actual Quest hardware.

## Preservation

The authored CastlePhysicsTest scene, block physics, player movement implementation,
avatar meshes, flying-hand behavior, XR packages and build flow were retained.
Only the normal bot-emote random source received a domain-reload robustness fix.
Unrelated ArchitectureMap work was not edited or staged. No commit, push, merge
or deployment was performed.
