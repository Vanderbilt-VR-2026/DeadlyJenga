# Setup, deployment and hardware

## Dependency-free playable mode

Use Unity 6000.3.23f1 and the existing project packages. Open the population studio,
create/select Default, generate a preview and run CastlePhysicsTest. Spawn regular
survivors from the existing menu. Offline mode produces meaningful social
decisions/captions and gameplay without pretending to synthesize natural speech.
No API key, model download or GPU is needed.

## Real speech on a Windows development host

Start in a terminal from the project root:

```powershell
python Tools/SocialVoiceBridge/server.py
```

Use a Python 3.10+ interpreter. The default server uses only its standard library
and installed Windows System.Speech voices. It starts on 127.0.0.1:8765; select
Local Development and that URL in the Unity studio, then apply to the running
population. Inspect `/health` and `/voices` for actual installed capabilities.
The Windows path is a basic preview, not a high-quality neural-voice substitute.

The service receives bot text and optional transient human PCM for configured
ASR; it does not retain recordings. Microphones are off until a player explicitly
enables them. Headphones are recommended: this baseline VAD transport does not
provide acoustic echo cancellation or production microphone noise suppression.

## Optional neural providers

Create a dedicated virtual environment before installing
Tools/SocialVoiceBridge/requirements-optional.txt. No installation happens from
the Unity menu. Check model licenses and notices for the exact version deployed.

```powershell
python Tools/SocialVoiceBridge/server.py --tts kokoro
python Tools/SocialVoiceBridge/server.py --tts kokoro --asr-model C:/Models/faster-whisper-small
```

Kokoro's upstream KPipeline may download weights on the first explicit neural
startup. Install its documented phonemization dependencies (including espeak-ng
where required). The ASR argument must point to an existing converted model;
ASR uses local_files_only and does not implicitly download weights. CPU int8 and
CUDA float16 ASR options are exposed; only the CPU path was tested here.

This checkout now has an ignored `.venv`, tiny.en model and Kokoro cache under
Tools/SocialVoiceBridge. They are local development artifacts, not Unity assets
or files that will arrive through Git. To run the tested combination on this machine:

```powershell
./Tools/SocialVoiceBridge/.venv/Scripts/python.exe Tools/SocialVoiceBridge/server.py --tts kokoro --asr-model Tools/SocialVoiceBridge/.models/tiny.en
```

For a fresh machine, create that virtual environment, install
requirements-tested.txt, explicitly download the public Systran/faster-whisper-tiny.en
repository to `.models/tiny.en`, then start the command. Model loading, voice-bank
loading and generation of fixed reaction clips occur before the service announces
readiness. First neural startup can download model/voice/spaCy assets. Later runs
reuse the ignored project-local `.cache`; no game startup implicitly installs them.
Both ASR and TTS default to two CPU threads; tune --asr-threads/--tts-threads after
profiling game performance. The default Windows mode still needs none of these
optional packages.

For optional generative replies, run a self-hosted Ollama-compatible service with
an independently licensed local model, then pass `--ollama-model <installed-name>`
and select Full mode. The service sends bounded event/personality context and
uses a short fallback reaction on timeout. No tool calling or admin actions are
exposed to generated text. Never put service credentials into a Quest APK.

## Quest / shared development service

For a trusted LAN test, explicitly bind the bridge to the host's LAN address:

```powershell
python Tools/SocialVoiceBridge/server.py --host <host-LAN-address>
```

Set that URL on the authoritative Unity host. Human voice is carried by the
existing approved Castle LAN session, not by separate per-Quest model instances.
Discovery uses UDP 47771; game/audio links use host-advertised TCP. All peers need
the same LAN-2 build. Existing Android permission merging is retained; microphone
permission is requested only on opt-in.

Unity's [insecure HTTP policy](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings-insecureHttpOption.html)
also applies to the bridge URL. This project currently disallows plaintext HTTP
in built players. For a trusted-LAN **Development Build**, choose the development-only
HTTP allowance in Player Settings, or use a properly trusted HTTPS endpoint.
The implementation does not weaken that project-wide policy automatically.
An APK's default localhost URL refers to the headset itself, not the laptop.

This lightweight HTTP bridge has no built-in Internet authentication/TLS. Keep
it on loopback or a trusted private network. A production/dedicated deployment
needs authenticated TLS termination, per-session isolation and a tested
WebRTC/Opus voice transport if Internet latency/scalability is required. Nothing
has been deployed or exposed outside the local test process by this work.

## Budgets and hardware

The simulation uses scheduled CPU work; Unity still bears ordinary physics,
rendering, avatar and route-planning costs. PCM16 mono at 16kHz is about 32KB/s
per active speaker per receiver. Audio queues/rings and per-session inference
slots are bounded. Model RAM/VRAM and latency depend on the chosen weights;
do not extrapolate CPU-only social-core benchmarks to Quest frame rate or neural
inference. Kokoro + CPU ASR ran in a separate process here; observed working set
was roughly 1.1GB and peak roughly 1.5GB during the combined test. Model startup,
cached reaction latency and uncached neural synthesis are separate measurements.
See ArtDirection/CastleSocial for actual results and generated listening samples.

## Validation

```powershell
./Tests/SocialSimulation/Run-Checks.ps1
./Tests/LocalMultiplayer/Run-TransportChecks.ps1
python Tools/SocialVoiceBridge/test_bridge.py
python Tools/SocialVoiceBridge/test_bridge.py --unity
./Tools/SocialVoiceBridge/.venv/Scripts/python.exe Tools/SocialVoiceBridge/test_bridge.py --tts kokoro --asr-model Tools/SocialVoiceBridge/.models/tiny.en --unity
```

The portable suite uses the .NET 8 SDK and compiles the exact shipped core files.
The Unity menu runs real movement, event, authority and multi-peer loopback checks;
the last Python command queues those checks with the test bridge available.
Stop Play Mode before queuing a new run; do not edit/recompile scripts mid-run.
The batch entry is DeadlyJenga.CastleSocial.Editor.SocialValidation.RunBatch.
If Auto Refresh is disabled, use Assets → Refresh before queuing a test. The
Python harness checks for current-suite assertions so stale loaded scripts cannot
be mistaken for validation of newer source files.

For consented movement demonstrations, enable the profile setting explicitly.
JSONL pose/event records are bounded to 30 minutes/session under
Application.persistentDataPath/ConsentedMovement. No voice is included. This is
an exportable demonstration format, not trained imitation learning.
