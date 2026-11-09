# Bundled bot dialogue

Game selects bundled voices by default. Each social identity's ordinal selects
one of eight English voices for every line it says. The first eight bot
identities get distinct voices. The bank contains authored game reactions.
Gameplay sends samples through the existing voice stream and Presentation
handles playback and mixing.

The clips were synthesized with KittenML's Apache-2.0
[Kitten TTS Nano 0.2](https://huggingface.co/KittenML/kitten-tts-nano-0.2) through
[sherpa-onnx 1.13.8](https://github.com/k2-fsa/sherpa-onnx/releases/tag/v1.13.8).
The voice order is the model's
[eight published speaker IDs](https://k2-fsa.github.io/sherpa/onnx/tts/all/English/kitten-nano-en-v0_2.html).
The shipped bank contains generated dialogue only. The model and authoring
dependencies run on the authoring machine.

To regenerate after changing a reaction, run `generate_bundled_voices.py` with
the published `kitten-nano-en-v0_2-fp16` model directory and the
`Resources/CastleSocial/BundledBotVoices.bytes` output. Pass `--staging` with
a temporary file outside Assets on the same drive. Use NumPy and sherpa-onnx
1.13.8 with a compatible ONNX Runtime. The generator trims and level-matches
the clips, then stores mono 16 kHz PCM for the existing voice stream.

| Input | SHA-256 |
| --- | --- |
| model.fp16.onnx | f24264d818087e643828da7a992e892e703c412bf147e152b825da567031d5ce |
| voices.bin | 42a40a24a352a38657d6cb86ceee51bbc2b7780b29e04fb60bcdf959adccea01 |
| tokens.txt | 934a4188addc7665dd3410256bb622169242357fbb99d840d9351209b486dabb |

The desktop smoke check entry point is
`DeadlyJenga.CastleSocial.Editor.OnDeviceConversationChecks.RunBundledBatch`.
It opens Game, sends reactions through the existing director and checks the
resulting AudioSource output and captions.

Validation: Unity 6000.3.23f1 opened Game and verified non-silent AudioSource
output for two bots and a second reaction from the first bot. It also checked
matching captions, release of the speech slots, and no speech-service requests.
The bank check verified all 624 takes, with eight distinct recordings per line.
Bank SHA-256: `9a4f26ac9402b259f2061ed85c334d10449fb0d785ac9c9d8a0994e557b1dcb9`.

Test base: `2427cf43ac83dabbe5f58376dbbd2a52ee88ffdf`. Intended integration
target: `origin/Evan_Sprint_4`. No external model, service, or Core dependency.
Quest, a new APK, and audio between independent Fusion peers were not tested.
