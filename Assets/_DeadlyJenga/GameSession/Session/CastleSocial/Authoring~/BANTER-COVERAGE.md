# Contextual match banter

The default on-device reaction route composes a line from a trusted game
observation and the speaker's perspective. Short tags rotate by event family.
A close call, hit, weapon fire or elimination gets a plain first line. Jokes
make fun of the tower, the weather, or the speaking bot, and stay away from a
player who just got hurt. A second bot can react after playback and receives
the exact first line. It can ask about the witness's direction without claiming
the direction or event as its own observation.

Direct player speech still uses the local language model. The authored
improviseGameReactions setting can also send observed events to that model.
The fast route is bounded authored composition. It can recombine known facts
and responses, and does not offer arbitrary dialogue about every object.

| Situation | Input and response policy | Coverage |
| --- | --- | --- |
| Block breaks, crack or moving cluster | Prefer a witness. Use its relative direction. Sound-only bots get a crash or crack and may ask where. | Game events and block sampling. Compare collapse audio in Unity. |
| Drop, vault, shove and damage | Read a real action or confirmed effect. Use short concern for danger and celebrate a witnessed landing. | Source adapters and varied text checks. |
| Elimination, revive and respawn | Respect visibility and alive state. Respond to the event, never to unseen causes or identities. Cancel a speaking actor that becomes unavailable. | Event adapter, confirmed revive counter, lifecycle guard. |
| Hazard and warning | Name a seen hazard when the game supplies it. Read public warnings separately. Avoid a claim about a route or safe place. | Start/end events and public warning updates. |
| Shot, weapon swing, pickup and drop | Use nearby sound for a shot and line of sight for a visible action. Keep weapon inventory out of the prompt. | Confirmed equipment action frames outside Mystery. |
| Flag, return, claim, hill, survival and infection | Read the bot's own confirmed counter or milestone. A nearby teammate can react to what that bot actually says. | Match frame adapter and varied outcome text checks. |
| Round flow, rewind, visible effect and emote | Start and end on public round changes, clear pending speech on rewind, and react briefly to a visible effect or human pose. | Public or local event adapters. |
| Human question, interruption, silence and small talk | Use local ASR and language model for the human turn. Existing priority and mute gates still apply. Keep ambient replies sparse. | Existing human route and voice controls. |
| Hidden role, unconfirmed score, unseen attacker or unheard chat | Withhold private inputs. Allow uncertainty or silence. Do not infer player identity, action completion or a safe route from prose. | Input restrictions, visibility and live-state checks. |

Latency is measured from the block-break event to a non-silent Unity
AudioSource. Text-only check timings measure the composer separately and do not
include synthesis, audio buffering or frame time. One local worker still owns
the models. Common directional break callouts, selected replies and short match lines
start from exact pre-rendered phrases in the same eight Pocket voices.
A fact and a separate joke can be joined at a sentence boundary. Other
lines are synthesized locally. Completed authored audio is cached by
voice, delivery rate and exact line. Direct human and improvised outputs
are excluded from the audio cache. Audio packets use an absolute sample timeline with a short
initial buffer so frame delays do not accumulate per packet.

A playtest in this pass is a controlled desktop Game fixture. The broader
corpus varies facts, direction, tone and hearing permission. The coverage table
is not a claim that every weapon, map, network role, mode or headset has been
playtested.

## Scenario checks still worth running in game

- Compare a crowded demo tower, empty arena, and a 10/25/50 bot burst.
  Check that witnesses win over bystanders, humans can interrupt, and waiting
  for local audio does not create a second worker or a queue of old jokes.
- In capture the flag, separate pickup, return, reaching base and confirmed
  capture. In hill and survival, verify a milestone exactly once. In team
  tickets, verify round announcements without inventing who cost a ticket.
- In Mystery, swap hidden roles and private gear while holding public facts
  constant. Check equivalent observations, lines and eligibility. A visible
  action can be commented on without naming a hidden role or cause.
- Cover changes between turns: wall occlusion, distance, bot respawn, mute,
  a new round, rewind, authority loss, a disabled microphone, an absent model
  pack and native failure. Keep human speech first.
- Compare low-stakes banter, warnings and distress with listeners. Rate
  whether a joke feels like a player responding and whether it comes after
  useful information. Check exact repeats over a full match, not just the
  number of variants in the table.

Run the standalone corpus with PowerShell 7 and a .NET SDK.

    ./run_banter_corpus.ps1

The runner compiles the production composer against the current event enum
and a small observation stand-in. It uses no Unity scene, model or network.
For audible results, use the contextual collapse fixture described in
CONTEXTUAL-VOICES.md. Add timing-fixture on a separate request line to time
the production path from supplied observer facts with a wall and two
survivors. Use event-fixture to emit a BlockBroken event at the staged
position and exercise sight and hearing as well. The standard request still
attempts a physical break. The same
timing fixture checks cold and cached responses to real damage.

The fast phrase list is in fast-banter-lines.txt. Use the existing
generate_bundled_voices.py recipe in BUNDLED-VOICES.md with --line-list
pointing to that file and output FastBanterVoices.bytes. The bank uses
the same model and speaker IDs as the original authored voice assets.

## Measured desktop results

Unity 6000.3.23f1 played both first-use collapse turns with no model or
live synthesis on the audio path. The staged event exercised the real
sight, hearing, dialogue and playback path. The raw witness ray hit
decorative debris, but the bot still saw the collapse and the wall still
blocked the other bot. The first line became audible after 0.95 seconds.
The reply followed at 6.59 seconds from the event. The previous full
model fixture measured 7.13 and 17.47 seconds respectively. The original
fixture broke a physical block and this run emitted its event at a known
position. The comparison is between two controlled runs, not a tail
latency estimate. Cold and cached damage reactions measured 2.62 and
0.96 seconds in the final run.

The text corpus passed 140 assertions over 34 facts and all event kinds.
The phrase bank has 18 phrases across eight voices, totaling 144
non-silent clips and 10,979,155 bytes. SHA-256
41fc441a4f9a201022677ff83880930a54ff7a0ff88f63100a13637032b51061.
The results are from a prepared desktop checkout. Individual modes,
full-match repetition, Quest performance and independent peers are
separate validation cases.
