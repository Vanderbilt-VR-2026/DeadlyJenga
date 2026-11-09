# Local contextual bot dialogue

Enable the existing on-device conversation option in a match with a prepared
conversation pack. This feature shares the current native text and speech
worker across the bots. The normal match voices use the existing bundled
clips when on-device conversation is off or the local pack fails to load.

An observation contains the event and what this bot can know about it.
Block breaks and block motion include an event position. Sight checks use
facing, distance and line of sight through the map. An occluded bot within
hearing range gets the sound without the visual details. The block-break
event has no actor or cause, so it cannot establish who pulled the block.

The first speaker is an eligible nearby witness, or an eligible listener
if nobody saw the event. The optional second speaker must be near the
first speaker and receives its own observation and the exact line after
that line is sent for playback. Each exchange can have one follow-up.
The normal human speech interruption, mute and round invalidation apply.

The local model writes a short line which goes to Kitten speech synthesis
using the same speaker ordinal as bundled playback. For collapses, the
dialogue check filters copied lines, unsupported sight claims and common
unsupported causal claims. A failed line is replaced with a composition
from the observed facts and the words the partner actually spoke. The
runtime counters distinguish model output and composed fallback. This is
a limited output check, rather than a general factual verifier.

The adapters cover block breaks and moving clusters, sudden drops and
vaults, damage and knockback, eliminations, hazards and public warnings,
round changes, nearby shots and equipment pickups, plus confirmed personal
flag captures and returns, block claims, revives, infections and
eliminations. The objective adapter reads the bot's own completion
counters and the public match mode. Mystery excludes the equipment and
objective frame adapters. Hidden identities, private inventories, positions
of unseen opponents and unconfirmed objective outcomes stay outside prompts.
The existing local speech recognizer can supply a human turn separately.

To run the collapse fixture, place the absolute local pack path in
Logs/Validation/ContextualCollapse.request in the checked-out project.
Use the checked Unity batch launcher or an admitted warm editor. The
fixture opens Game, stages two actual survivor bots and a sight blocker,
breaks a staged instance of the map block through IBreakable, and checks captions and
non-silent AudioSources for both turns. The report shows elapsed time,
text and synthesis time and the route taken by each line. This fixture
runs on desktop and checks the block-break signal. Full structural
failure, individual mode scenarios, headset audio and performance are
separate validation cases.

The desktop Unity 6000.3.23f1 run passed with Qwen3 0.6B Q8 and Kitten TTS,
using two CPU threads and a disabled frame guard in the fixture. It verified
the break position, line of sight, sound-only context, matching captions,
and non-silent output from both actual bot sources. The witness's first audio
arrived at 7.13 seconds and the listener's at 17.47 seconds from the break.
Both were generated locally. Eight focused text checks passed separately.
These are single-run functional timings. The remaining event sources
compiled, and a prior attempt played an incidental elimination exchange.
Other modes, multiplayer, headset audio and frame time remain unverified.

Tested task base is d60a780fc4cab01882081d8c720ed4f0aa90f49a. Intended
integration target is origin/Evan_Sprint_4. The target already contains the
equivalent bundled-voice patch. There are no Core or package changes.
