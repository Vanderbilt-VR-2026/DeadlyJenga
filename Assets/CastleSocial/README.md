# AI Player Population & Social Simulation

Sprint 3 adds social simulation to **regular castle survivor bots only**. Open
**Tools → Castle Social → Population & Social Simulation** for the authoring
studio. The existing Spawn AI Survivor and fill controls still work: new survivors
receive generated identities and a social adapter automatically. Flying hands
keep their existing personalities, input and physical interaction.

## Start here

1. Select the Default population asset in the studio, or create it there once.
2. Generate a deterministic preview. Inspect individual traits, skill, friend
   groups, demographic assignment and voice identity before entering Play Mode.
3. Open CastlePhysicsTest and enter Play Mode. Spawn ordinary survivors normally,
   or use the studio's Simulation page to apply the selected profile and spawn.
4. Offline mode is fully local: bots move, perceive events, become emotional,
   form social intentions, gesture and show captioned conversations. **It does
   not pretend that captions are audible synthesized dialogue.**
5. For audible speech, start the optional local voice bridge, select Local
   Development, set its URL and apply the profile. See SETUP.md.
6. Microphone voice chat is opt-in under the existing demo Settings menu. The
   headset's VR movement/turning page also exposes mic, bot mute/volume controls.

The default profile is a Resources asset so it works in built players. Runtime
settings use an instantiated copy; tuning a running session does not silently
overwrite the authored profile. No additional Unity package is required.

## Designer controls

The studio groups Population, Personalities, Age & identity, Friends, Gameplay,
Voice, Conversations, Memory, Mappings and Simulation. Profiles support duplicate,
JSON import/export, deterministic seeds, reset, previews and graphs. Invalid
distribution/correlation combinations are reported rather than silently repaired.

Traits are normalized 0–100 and independent of gameplay skill. The initial eight
dimensions are extensible. Curves map traits into risk, planning delay, social
interest, reaction intensity and cooperation. The existing navigator remains the
legal route and collision authority; personality changes its tuning, not the rules.

Each bounded distribution uses continuous quantile tails with an exact median.
Extent scaling and analytic moments calibrate SD; exceptionally wide spreads also
solve tail exponents numerically. This is **not** a truncated normal or a claim of
psychometric validity. Bounds are limits, not a promise samples fill the entire
range. Tail lean is not standardized skewness; the graph reports actual skewness.
Correlations are Gaussian-copula latent correlations; transformed Pearson values
will differ. Friend percentages are approximate because whole valid groups are
required. Age/gender do not determine intelligence, skill or voice-bank selection.

## Simulation behavior

The host owns identities, emotions, attention, relationship edges, memories and
speech selection. Threat, successful vault, fall/respawn and social events share
one decision context. Friends can seek each other using existing legal routes;
danger interrupts that interest. Body language reuses the existing avatar and
emote components. No gameplay frame calls an LLM.

Conversation bids consider relevance, social traits, relationship, attention and
time since last speech. Typical salient events select up to the configured 1–3
respondents, subject to voice assignment, cooldowns and a bounded speech queue.
Independent topics and friend exchanges can overlap within a global voice budget.
Bot responses create at most one further bot follow-up; bot audio never enters
the transcription path. Reactions use a compositional contextual phrase library;
optional Full mode requests a configured local language model instead.

Human VAD has a minimum duration before it takes priority. Full bot statements
wait while the human holds the floor; brief acknowledgments may overlap. Existing
speech is ducked, and queued/stale/cancelled requests are discarded. This is a
practical modular conversation system, not a tested full-duplex speech model or
universally human-indistinguishable behavior.

## Networking

The playable castle uses CastleLanSession; separate Lobby/Main scenes use Fusion.
This module extends **the castle LAN path**, not the Photon lobby. On joining,
clients already disable their independent bot populations; they receive the
host's poses, social identity labels, captions and independent audio streams.
Only the host runs social decisions/inference. Human audio is attributed to the
authenticated connection, not a client-supplied speaker ID.

Protocol version is now `DeadlyJenga-LAN-2`: all devices must use the same build.
Voice uses bounded PCM16/16kHz mono streams over the existing connection, separate
from latest world-state and reliable controls. This avoids a second multiplayer
framework but is a LAN baseline: TCP loss can delay audio. It is not an Internet
WebRTC/Opus substitute, and simultaneous speakers consume about 32KB/s each per
receiver before framing. Individual AudioSources are global by default and expose
spatialBlend for later positional routing.

## Testing and documentation

- ARCHITECTURE.md: integration boundaries and major decisions.
- SETUP.md: local speech, neural alternatives, deployment and hardware.
- COMPLETION.md: implemented, partial, optional and unverified requirements.
- Tools → Castle Social → Run Integrated Checks: actual Unity movement,
  authority, nearby requests/invites, late join, audio relay and hand regression.
- Tests/SocialSimulation/Run-Checks.ps1: portable statistical/social/DSP tests.
- Tools/SocialVoiceBridge/test_bridge.py: real local synthesis and failure checks;
  `--unity` also requests end-to-end Unity PCM playback.
- ArtDirection/CastleSocial: reports and measurements from this implementation.

No push, merge or deployment is performed as part of this implementation.
