# Bundled bot dialogue

Regular matches use a bank of pre-rendered Pocket TTS reactions. Each bot
identity has a stable voice slot. The first eight identities have distinct
voices. Presentation plays the same PCM format through the existing voice
stream.

The same voice slots feed the optional live Pocket TTS engine. Voice sources
and slot order are listed in [Pocket-Cast.txt](../../../../../../Tools/OnDeviceConversation/licenses/Pocket-Cast.txt).
The [Sherpa model archive](https://k2-fsa.github.io/sherpa/onnx/tts/pocket.html)
contains the CPU graphs. The game uses Sherpa 1.13.8 to run the model.

Run `generate_bundled_voices.py` with `--model-dir` pointing to the prepared
`Tools/OnDeviceConversation/.models/pocket/voice` directory and `--out`
pointing to `Resources/CastleSocial/BundledBotVoices.bytes`. For the common
contextual banter bank, add `--line-list fast-banter-lines.txt` and output to
`Resources/CastleSocial/FastBanterVoices.bytes`. Use a separate, fresh
`--staging` file outside Assets on the same drive for each bake. The renderer
uses NumPy, Sherpa 1.13.8 and a compatible ONNX Runtime. It trims and
level-matches audio and stores mono 16 kHz PCM.

The bundled path uses those recordings without loading the live models.
The desktop check entry point is
`DeadlyJenga.CastleSocial.Editor.OnDeviceConversationChecks.RunBundledBatch`.

The regular bank has 74 phrases and 592 verified takes. Its SHA-256 is
`154ee65c0ee352bfb4d7179aa1d7fa47c0f389ee9c81b5eed6b914072e12a53e`.
The contextual bank has 30 phrases and 240 takes. Its SHA-256 is
`9ccd13a75ba3a2689ea43db83e61773b3e8407fdbffb1756a2cd363b40abef43`.
Each phrase has eight distinct, non-silent takes.
