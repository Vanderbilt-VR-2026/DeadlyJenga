# Optional Mini voice cast and dialogue novelty

The prepared desktop checkout looks for a local realistic conversation pack
next to the existing pack. The selected cast uses the eight speaker slots of
[Kitten Mini 0.8](https://huggingface.co/KittenML/kitten-tts-mini-0.8).
[Sherpa's model page](https://k2-fsa.github.io/sherpa/onnx/tts/all/English/kitten-mini-en-v0_8.html)
documents the slot order and the model archive. The same bot ordinal selects
the same speaker for the matching pre-rendered audio and live synthesis.
A new cast cannot load a legacy phrase bank by accident. Regular bundled
matches continue to use the original Nano 0.2 bank.

The decision to try Mini on-device was based on the larger model, its
compatible voice slots and a matched render with two speaker slots. On the
same collapse line, the two Mini takes were 3.83 and 5.60 seconds long.
The legacy takes were 5.83 and 7.23 seconds. Mini took longer to render in
this desktop trial. The timings measure pacing and computation. Perceived
naturalness still needs listening. We also considered
[Kokoro](https://huggingface.co/hexgrad/Kokoro-82M) and
[Pocket TTS](https://github.com/kyutai-labs/pocket-tts). Those would use a
different voice backend and are outside this cast.

For an already prepared conversation pack, render an inventory of exact
phrases with generate_bundled_voices.py using --model-file model.onnx
and --line-list pointing to your inventory. The starter bank may be a
subset of fast-banter-lines.txt, with all eight speakers for each phrase.
Then run prepare_realistic_cast.py with --base-pack, --mini-model,
--phrase-bank and --out. The script creates a new verified pack and
preserves the base. Use a folder named realistic beside the default
Tools/OnDeviceConversation/.models/pack for the automatic editor selection.
A build can instead take a prepared cast through the existing default
pack staging path. Standalone headset performance is a separate test.

On-device dialogue keeps a recent history for the individual bot and for
shared match speech. Case and punctuation changes do not count as a new
line. Model near-copies also get filtered, with safeguards for changed
direction and negation. Authored contextual lines try alternative wording.
If every eligible line is recent, the bot stays quiet. Private human replies
remain outside the shared history. Default bundled speech also checks what
other bots have recently said and what is queued.

The history is bounded to 65 seconds and a small number of lines. It does
not prevent a line from ever being heard again. The standalone corpus
includes match-wide repeats, private reply isolation, near-copies,
direction changes and history expiry.

In the desktop Unity 6000.3.23f1 check, the default editor selection
loaded the Mini cast. Both staged collapse lines used the matching
starter bank. The first line played 1.02 seconds after the event and
the reply at 4.77 seconds. Two real hits against the same bot used
live Mini synthesis and distinct wording. They played after 3.12 and
4.41 seconds. The text corpus passed 149 assertions. The regular bank's
78 authored reactions still match its original audio inventory.

The prepared starter bank contains five phrases and 40 takes across
eight voices. Its SHA-256 is
bb822a9e112ab711a80cfaec189666578363b177785046bbf6b8c01689dac3cd.
The wider legacy bank still has all 18 pre-rendered phrases. Mini uses
live synthesis when a phrase is absent. These are controlled desktop
results and do not measure headset performance or subjective realism.
