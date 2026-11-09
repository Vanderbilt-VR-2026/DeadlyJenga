# Pocket TTS cast and dialogue novelty

Pocket TTS supplies both the recorded cast and the live voice provider.
The same bot ordinal selects the same reference voice in both modes. Common
contextual reactions use the recorded fast bank when both phrases are
available. New lines use live CPU synthesis with the same voice. Bundled
reactions remain available when the live pack is absent.

Run `python Tools/OnDeviceConversation/prepare.py` to build native adapters and
prepare the pack in `Tools/OnDeviceConversation/.models/pocket`. To retain
an existing language model and recognizer, run `prepare_realistic_cast.py`
with `--base-pack`, `--pocket-model` and `--out`. The Pocket directory must
contain the model graphs, LICENSE and eight numbered reference WAVs in the
same slot order as the bundled banks. The migration verifies reused files
and leaves the base intact. Live conversation requires model format 2 and
the matching adapters.

On-device dialogue checks a recent history for the individual bot and for
shared match speech. Case and punctuation changes do not count as a new
line. Near-copies are filtered while preserving changes in direction and
negation. Contextual lines try alternative wording. If every eligible line
is recent, the bot stays quiet. Private replies stay outside shared history.
Bundled speech checks recent and queued public speech across bots.

The history is bounded to 65 seconds and a small number of lines. A useful
line can return later. The fixture stages a collapse exchange grounded in
what each bot could see or hear and tests two distinct live replies to
repeated damage.

## Desktop verification

Unity 6000.3.23f1 opened Game and played four non-silent lines through the
actual voice AudioSources. The staged block event gave one bot a visual
fact and the other a sound-only fact. The witness spoke at 0.98 seconds.
The listener responded to the witness's direction at 3.44 seconds. Both
lines used the Pocket fast bank. Two actual damage calls gave the same
bot two different live Pocket replies at 1.79 and 1.13 seconds. Preparation
took 1.56 and 0.96 seconds respectively.

In a one-thread desktop dry run, Pocket produced the first measurable
signal on the same collapse line after about 1.0 second and the complete
clip after 1.8 seconds. Kokoro INT8 took 11 to 15 seconds for complete
clips in a separate busy-desktop run. These timings measure the tested
exports and workloads. Auditioning is the way to compare their sound.
Windows and Android ARM64 adapters compiled. Quest runtime and a rebuilt
APK are pending.
