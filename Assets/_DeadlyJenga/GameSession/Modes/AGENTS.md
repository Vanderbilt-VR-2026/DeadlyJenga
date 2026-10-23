# Bot objectives: authoring contract

When adding or changing a mode's bot behavior, first read [BotObjectives/README.md](BotObjectives/README.md). This contract supplements GameSession/AGENTS.md and the root box rules.

## Required extension order

1. Express intent in a version-1 definition in Resources/BotModes. Reuse registered queries, conditions and the six existing skills first.
2. If a fact or target is new, add its bounded typed query/condition in the GameSession adapter. Do not add a game-mode branch to Characters or the generic director.
3. If an interaction is new, use Core's IBotObjectiveInteraction on the target. The target validates actor, authority, range and legality. Advertise only supported interaction ids. An accepted request is not completion.
4. Change Characters only for a genuinely new physical execution capability. State why Travel/Guard/Escort/Contest/Attack/Evade plus target interaction cannot express it; preserve normal player mechanics. Core changes remain additive and require task scope.

## Definition requirements

- Write an accurate description of mode intent and identify the authoritative completion fact. README prose alone is not executable behavior.
- Use stable template and target ids. Query results must be finite, unique and bounded to 32. Keep each actor below 128 eligible candidates; rank/narrow large sets in the adapter. Never silently truncate the director's allocation.
- Distinguish public, observed and remembered information. Never attach a live hidden-enemy GameObject to a remembered position. Preserve Mystery role secrecy and explicit public radar rules.
- Use participant limits deliberately. minimum is a staffing request, maximum is a hard cap per team/template/target, personal separates each actor's capacity. Higher tiers can displace lower-tier minima. Starvation or insufficient actors does not fabricate extra bots.
- Give finite timeouts and retry cooldowns. No teleporting to finish, fake scores, permanent route waits or per-frame remote AI calls.
- Arrival is not success. Pickups, captures, claims, revives and deliveries require their actual authoritative state transition. Hold/escort objectives may remain ongoing.
- Attack subjects must be legal observed targets or explicitly public radar subjects. Remembered points cannot grant a hidden live transform. Keep physical reach, facing, windup, shielding and safe positioning in the shared Characters skill; preserve specialist query utility. A motor-accepted punch is not proof of damage or elimination. Changes to combat need Check Bot Combat plus a production mode acceptance run.
- Adding a GameModeKind still requires gameplay/network/catalog integration elsewhere; this framework does not create scoring or replicate new rules from prose.

## Required evidence before saying done

1. Run Tools > Deadly Jenga > Validation > Check Bot Objective Contracts. It must pass. Builds run this gate automatically.
2. Add meaningful fake-facts cases for new queries/conditions, lifecycle transitions, illegal actors and information restrictions. Extend malformed-definition tests for schema changes.
3. Run a short real Game scene test of the changed mode, then a targeted real objective completion. Report actual scores/outcomes separately from movement and model fixtures.
4. Inspect assignment history for oversubscription, reassignment loops, hidden-information leaks, timeouts and feedback from obsolete tokens.
5. Compile all affected boxes. Confirm Game enters/exits Play Mode and restores its scene setup. Networking changes also need the root-required two-player test; Quest claims require a real headset.
6. Report changed lanes, additive Core contracts, tests, known limitations and untested cases. Never call a model test a runtime interaction test or describe this contract as proof of perfect behavior.

Do not weaken the validator, limits or test assertions merely to make a new definition pass. Do not restore the old mode switch as a silent fallback when a definition is invalid. The contract failure must identify what needs repair.
