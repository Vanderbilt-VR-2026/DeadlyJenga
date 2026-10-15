# NPC emotes

Eight cosmetic emotes auto-attach to survivor bots, including future spawns, within one second:

- Friendly wave: occasional greeting on meeting a human or NPC.
- Totally meant that: double fist pump after a scare or successful vault/landing.
- Phew: forehead wipe after surviving trouble.
- What was that: confused shrug/head tilt.
- Golf clap: deliberately tiny applause.
- Just chilling: relaxed overhead stretch.
- Nope nope nope: panicky arm flap near mayhem.
- Tiny victory dance: alternating hands and head bob while standing still.

Context nudges the selection, but does not guarantee a reaction. Quiet ambient opportunities occur every 12–30 seconds, with a probability influenced by the bot personality's goofiness. Separate randomized initial delays and cooldowns reduce synchronized reactions. Meetings are proximity cues, not physical collisions. A near miss is a heuristic based on danger falling away, a hard landing, or completion of a vault—not proof of a perfectly dodged impact.

They never issue movement/jump commands, stop the brain, add physics, move the feet, or animate the human player's tracked hands. Idle poses cancel if the bot starts moving. All emotes cancel during a jump, vault or knockout, preserving actual vault contact animation. Hand poses blend in and out and use the current shared candy rig and scale.

Tune Frequency, Minimum Cooldown and Contextual Reactions on CastleBotEmotes. Frequency zero disables new emotes. The component's Play Mode context menu previews individual emotes when the bot is safely grounded (idle emotes require a stationary bot). TryPlay is also available for future gameplay events. Disable the component to stop its overlay.

The runtime registry is intentionally separate from the bot factory and brain so this side-conversation addition does not conflict with the main thread's hand work. It scans once per second. New source files still require Unity import/compilation and in-editor visual verification; no Unity refresh or Play Mode run was initiated from this side conversation.
