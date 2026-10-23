# Modular bot objectives

GameSession decides what needs doing. Characters decides how to do it with the ordinary player motor. Core contains their shared vocabulary. No AI model runs during a match.

## Pipeline

Resources/BotModes/<GameModeKind>.json -> strict JSON and semantic validation -> MatchBotFacts -> BotObjectiveDirector -> Core BotObjective + BotExecutionPolicy -> Characters skill/movement -> Core feedback -> director history.

Every shipped mode has a JSON definition. Demo supplies hazard refuge only and retains free exploration/social movement while unassigned. Directed safety assignments take precedence over social steering. The runtime samples eligible authoritative bot actors at most every 0.5 simulation seconds. Definitions are loaded once per mode/session configuration. Runtime errors log an explicit contract failure and stop issuing assignments; there is no hidden legacy-mode fallback. Movement's ordinary unassigned exploration remains available.

The director has no mode enum switch and references no character classes. MatchBotFacts is the mode-state adapter. To support a new kind of objective, add a registered query/condition there (or supply another IBotModeFacts to a director). To support a new physical interaction, implement IBotObjectiveInteraction on the target. Existing scoring and permission checks remain in gameplay code.

## Definition fields

Root: schemaVersion (currently 1), stable id, description, objectives (1..32 templates). Strict parsing rejects unknown/duplicate fields, wrong types, missing required fields, invalid JSON and documents larger than 128 KiB. Semantic validation rejects unknown registries, invalid skills, contradictory require/exclude conditions, NaN/infinity, invalid participant limits and invalid timing. Runtime facts must also satisfy the target contract.

| Template field | Meaning |
| --- | --- |
| id | Unique stable id, no slash; combined with target id to identify the opportunity |
| query | Registered typed target provider |
| skill | Travel, Guard, Escort, Contest, Attack or Evade |
| require / exclude | All named conditions must hold / none may hold |
| completeWhen | Optional authoritative success condition; empty means ongoing until cancelled or timed out |
| interaction | Optional registered target interaction id; empty means ordinary movement/skill only |
| tier | 0 ordinary, 1 urgent, 2 critical personal task, 3 immediate safety; higher tiers allocate first |
| minimum / fraction / maximum | Requested minimum, desired team fraction and hard cap for this opportunity |
| personal | Separate capacity group for each actor; useful for carrying or escaping |
| utility | Template 0..10 plus bounded query-specific target utility (-10..10), then travel cost and commitment inside a tier |
| radius | Arrival/hold radius; a target can supply an authoritative dynamic radius, e.g. hill settings |
| timeout / retry / commitment | Simulation seconds; timeout bounds execution, retry penalizes failure, commitment reduces switching |

Defaults: minimum 0, maximum 50, fraction 1, utility 1, radius 1, timeout 120, retry 10, commitment 3, tier 0, personal false. Specify important values explicitly. Required template fields are id, query and skill. Conditions are AND lists, not executable expressions.

## Queries and facts

Production queries: hazard-refuge, hill, unowned-blocks, high-ground, midfield, infection-search, hostiles, home, enemy-bases, enemy-flags, own-dropped-flag, return-enemy-flag, return-own-flag, friendly-carriers, hostile-carriers, stolen-flag-search, mystery-travel, mystery-guard, mystery-attack, mystery-evade.

Conditions: carrying, infected, own-flag-missing, has-hostile, carrying-enemy, carrying-own, scored, own-flag-home, block-owned, target-dead.

Queries supply stable id, permitted position, optional subject, baseline value, bounded utility and optional radius. The director preserves the initial target baseline while refreshing the current target position. CTF success checks the actual actor's capture counter, not another teammate's score or arrival at home. Actor capture counters are local authoritative bot-lifecycle bookkeeping; assignments are cancelled and recreated on host restoration. They are not additional network state.

Enemy queries return a live subject only when currently visible (or explicitly public radar allows it). Memory supplies the last known position without a live transform. Mystery still uses its existing role/witness/body logic through an adapter; converting that specialist logic into generic data is not required for modular movement.

## Allocation and lifecycle

For each tier, fill requested minima, desired fractions, then remaining capacity using utility/travel/commitment order and stable actor/id tie breaks. Maximum is enforced per team/template/target, including during emergencies. Personal opportunities have separate groups per actor. Insufficient actors or higher-priority work can leave lower-tier minima unfilled. This is a bounded greedy allocator, not a globally optimal assignment solver.

Assignments have a monotonically increasing token within the director, start time and initial completion baseline. Compatible moving targets preserve their token. Reassignment, actor departure, death, team change, mode/round reset and authority loss invalidate previous work. Characters reports progress at most twice per simulation second; feedback from an old token or objective is ignored.

Arrived records proximity and continues waiting for the mode's completion condition. Sustained Blocked feedback (five seconds after the first blocked report), explicit Unavailable/Failed or the objective deadline causes failure. Actor/target failure cooldown increases with repeated failures, up to 120 seconds. Other targets remain eligible. Failure records expire after 180 seconds; caches and the 256-event journal are bounded. Cancelling for a new assignment does not imply success or navigation failure.

Guarding, escorting and contesting are ongoing tasks. Arrival can legitimately mean holding; zero speed is not automatically blockage. Characters keeps local traversal recovery, safe steering and normal player movement constraints. New assignments resume through safe motor execution rather than teleporting.

For ongoing objectives without completeWhen, real Arrived feedback renews the execution lease. This prevents legitimate holding from producing a false deadline failure. Objectives with an authoritative completion condition keep their original deadline even after arrival. Session sampling also services the director when no bot is requesting goals, so dead/departed actors cannot retain stale assignments. Tokens remain unique across director replacement within the scripting domain.

## New mode recipe

1. Implement the gameplay rules and expose only permitted state to a facts adapter.
2. Write the mode description and define success/failure observations.
3. Compose a version-1 JSON definition from existing skills and queries.
4. Add narrowly scoped typed queries or conditions only where missing. Register supported ids; unrecognized names must fail.
5. If an objective needs an action, add a target-owned IBotObjectiveInteraction and advertise its id. CanInteract/TryInteract must validate all legal gameplay constraints. Calls may repeat once per second while waiting; make requests safe to repeat.
6. Add contract and real gameplay acceptance coverage. Follow ../AGENTS.md.

For example, a delivery mode can query available parcels and delivery zones. A collect objective uses Travel plus collect-parcel, and completes when carrying becomes true. A delivery objective requires carrying, uses Travel plus deliver-parcel, and completes when the authoritative delivery record advances. The existing director and Characters code do not change. The contract suite contains this example with fake facts; it is not a playable delivery mode or proof of physical interaction.

## Validation and diagnostics

Tools > Deadly Jenga > Validation > Check Bot Objective Contracts runs invalid-definition, capacity, emergency, lifecycle, stale feedback and independent parcel-flow checks, then validates every shipped definition against the production registry. Check Demo Bot Shelter runs the real motor into acid-rain refuge on a controlled physical platform; BotShelter.request invokes that targeted check in an idle Editor. Logs/Validation/BotObjectiveContracts.txt is the report. The same suite runs before builds and rejects the build on failure.

For an idle already-open Editor, create Logs/Validation/BotObjectiveContracts.request. This explicitly requests tests; it does not create or overwrite assets when Unity loads. BotPipelineChecks runs real Game/CTF movement and carrier fixtures and writes director assignment history alongside its results. A passing contract suite is not proof of movement, scoring in every mode, real multiplayer or Quest performance.

AI agents must read ../AGENTS.md and this document before changing objectives. Tests and runtime contracts enforce specific invariants; neither can promise perfect behavior on every map or future mechanic.

## Panther enemy foundation

Core's IEnemyPopulation is an optional capability of the existing survivor bot population. SpawnEnemy(new EnemySpawnSettings()) requests a panther; hitDamage defaults to 20 and is limited to 1..99. Unsupported archetypes, invalid numbers, non-authority calls and populations over 32 enemies are rejected. The desktop Survivors page and VR Powerups / enemies page expose spawn and clear commands for the authority.

Panthers instantiate the same survivor prefab, CastleBotBrain and movement/combat skills as ordinary bots. Characters adds a separate IEnemyActor identity and head/claw/tail mesh attachments. Appearance alone never determines enemy status. GameSession runs a separate director using the current Resources/BotModes/Infection definition and the same facts adapter, treating the enemy as infected for pursuit/search. Updating that definition updates both infected and enemy tactics. Characters contains no game-mode switch for enemies. Ordinary bot hostile queries can also observe enemies.

Enemy observations live in a separate ModeSimulation collection. They participate in combat but never enter the player roster, team balancing, flags, territory claims, survival counts or scoreboards. They are not converted players. Outside Infection, killing a player follows that mode's normal rules rather than infecting them. Enemy death despawns it. New rounds clear enemies; there is no automatic wave or respawn system yet. The existing survivor snapshot transports NPC metadata and appearance to observers. A host ignores NPC metadata supplied by a connected human. Encounters are currently transient through host migration; persistent PvE waves are future work.

Spawns test bounded perimeter rings against the active map's physical support and body clearance. There is no arbitrary fallback position if the map has no clear perimeter surface. Rebuilds use the same perimeter placement. Enemy count is separate from the ordinary fill target, and ClearEnemies preserves normal bots. Voice identities are not attached to enemies.

## Shipped mode tactics

| Mode | Bot responsibilities | Real completion/ongoing fact |
| --- | --- | --- |
| Demo | Explore freely; shelter or evacuate for active hazards | Shelter remains an ongoing safety assignment |
| Survival | Prefer supported elevated ground; avoid unsafe water/footing; shelter from hazards | Survive; hold suitable ground while it stays useful |
| King of Hill | Reach the current hill, remain within its actual radius, fight observed intruders | Authoritative grounded hill time |
| Block Control | Claim clear supported block tops, finish partial claims, contest opponents, revisit decaying ownership | Actual owner changes and accumulated team points |
| Capture the Flag | Pick up/return flags, restore own flag, intercept observed thieves, search last sightings, defend home, escort/protect carriers | Actual actor captures or own flag restored |
| Team Tickets | Search midfield, pursue observed enemies, retreat at critical health or an outnumbered final life | Actual eliminations and ticket consumption |
| Infection | Infected search/pursue survivors; survivors take safe escape routes or elevated refuge | Actual target conversion, not arrival |
| Murder Mystery | Murderers prefer fewer observed witnesses; sheriffs pursue witnessed murderers; innocents flee known threats; report bodies, revive and recover badges | Actual deaths, reports, revives and sheriff promotion |

Map queries rank a bounded set of physical destinations. They preserve query-specific utility instead of losing witness, claim-progress or safety rankings in the adapter. Escape candidates need supported, clear, water-safe footing and must improve distance from known threats. Patrol completion records actual proximity/dwell and briefly excludes recently visited areas. Holding, searching and fleeing do not grant damage, infection, ownership or score.

Additional queries: hill-threats, block-threats, home-threats, escort-threats, close-threats, owned-blocks, escape-routes, regroup, patrol, mystery-refuge, mystery-revive, mystery-bodies, mystery-badge.
Additional conditions: threat-near, needs-retreat, visited, target-infected, target-revived, body-reported, sheriff-promoted, block-refreshed.

Murder Mystery uses only the actor's own role, observed special strikes, visible/remembered actors, public bodies and nearby public trails. Unknown remote actors are not attached to remembered targets. Trail suspicion, reporting, revival, badge pickup and disaster choices go through the existing validated mode commands. An unavailable badge is remembered per actor/body rather than retried forever.

## All-mode live acceptance run

Tools > Deadly Jenga > Validation > Check All Mode Bot Behaviors runs real bots on the shipped mode maps in the Game scene. The run saves/restores scene setup and match choices and never saves scene assets. It writes Logs/Validation/BotModeBehaviors.txt and BotModeBehaviors.csv. An idle Editor also accepts BotModeBehaviors.request.json with bots (2..50), maxSeconds (20..300) and numeric GameModeKind modes. BotModeBehaviors.cancel stops the run safely. Do not run multiple editor test suites simultaneously.

The run controls initial Mystery role preferences and one initial infected bot, then requires ordinary motors/combat/scoring. Mystery uses the KOTH map because no dedicated Mystery map is shipped. Automatic disasters are disabled to isolate objectives; Check Demo Bot Shelter separately exercises real hazard refuge. This suite samples actual outcomes and records assignment history, travel and unexplained idle time. It is desktop evidence, not Quest or two-machine multiplayer verification, and does not exhaust every rare Mystery interaction or every custom rule combination.

Territory maintenance completes only from authoritative grounded occupancy of the correct friendly block with its decay clock reset. Being nearby does not count as maintenance. Departure cancels an enemy pursuit rather than fabricating a death. Point ids, tuple visit keys and per-sample public Mystery facts are cached; local defense queries avoid unrelated perception work.

The optional mysteryInteractions request field prepares two controlled Mystery stages (modes [7,7], at least four bots): an initial downed victim, then an initial murdered sheriff body. Helpers must perform actual revives, reports and badge recovery. The fixture uses a supported physical platform; initial setup is explicitly logged. Corpse placement is checked near its death point. This exercises existing gameplay commands, rather than injecting a successful result. ascentFixture is a separate controlled summit approach test. Normal all-mode runs default to up to 300 simulation seconds per mode; their capture requirement remains strict.

Spectator outcomes leave eliminated bots eliminated. The Player corpse provider honors IMysteryBody.Follow's position, including cloned rigidbody poses, so body reports and badges remain at the real death location. These support fixes add no Core members or network fields.

Physical target queries use the actor's effective movement rules, including per-role walkable-island permissions. Motor state is supplemented by measured displacement, so a stalled climb cannot indefinitely report healthy traversal or renew a holding lease. Check Demo Bot Shelter also exercises real capsule grounding at a supported edge, in air and against a vertical wall before its hazard route.

Before the close-combat expansion, the 2026-10-22 desktop acceptance run passed all eight modes with ten bots, zero runtime errors, and scene/settings restoration. Opposed CTF required 240.767 simulated seconds and an actual home-flag restoration before its capture. Infection required 256.417 seconds. A separate 50-bot/45-second movement run moved all fifty bots, with forty-five crossing midfield and none mostly blocked. These are targeted outcome checks for that earlier version, not exhaustive match or human-equivalence guarantees. Historical evidence is in Logs/Validation/BotBehaviorDevelopment.txt; rerun the tools after map, movement or rule changes.

## Attack targets and physical combat

The facts adapter ranks at most 24 hostile opportunities from the bounded participant population. Nearby and visible threats receive preference; visible weakened opponents and flag carriers receive modest additional weight. Hidden target health and flag status do not influence remembered-position ranking. Existing assignment commitment and staffing limits still apply. Legacy specialist goal priorities, including Mystery's observed witness weighting, are preserved as target utility when entering the director.

Attack remains a mode-independent Characters skill. Modes choose legal targets: hill/territory defenders, opposing ticket teams, observed CTF thieves or carrier threats, infected pursuing survivors, and Mystery's own-role/witness rules. Survival/Demo defaults do not initiate combat. No global scan inside Characters grants new hostility or hidden role knowledge. Special strikes still pass through the existing Mystery command/rule path; movement and punching do not award kills, conversion or score.

The close-threats query supplies at most four permitted, currently visible opponents within 2.2 metres. Competitive profiles compose it as a personal tier-2 repel objective, capped at one assignment per actor and excluded while carrying a flag. It lets claimers, flag raiders, retreating players and Infection survivors respond at contact instead of remaining passive in a travel/escape assignment. Immediate hazard safety stays at tier 3. Once contact ends, normal mode work resumes; ending contact does not fabricate a kill or objective success. Survival's response is active only when its punching option is enabled; Demo retains its sandbox behavior.

Mystery's contact candidates come from the existing role/witness-filtered Attack and Evade goals, never the generic all-opponents list. An innocent can repel a witnessed murderer at contact range, then flee; a nearby unknown murderer is not an attack permission. Production contact fixtures use the real director after .9 seconds of ordinary initial observation and explicitly check both unknown and witnessed cases. Role preferences and the initial visible special strike are controlled setup; observer knowledge and subsequent attacks use normal gameplay processing.

Read Characters/Bots/CastleBots/README.md for the physical combat states and Tools > Deadly Jenga > Validation > Check Bot Combat for focused live coverage. The all-mode CSV also records combat state/reason, attempts, motor-accepted requests and withheld strikes. Damage/push and authoritative mode outcomes must be checked separately.

The combat expansion passed 29 live mechanics/production-policy fixtures and 120 contract assertions on 2026-10-22. A real fifty-bot Tickets check passed with all fifty moving, twelve bot eliminations and 107 damage events in twenty simulated seconds. The final ten-bot natural match suite passed seven modes, including a real CTF capture, but Territory failed its movement-idle gate for one actor despite thirteen owned blocks. Its contact/knockback fixture passes; the navigation failure remains separate and unresolved. Local evidence and exact limitations are recorded in Logs/Validation/BotCombatDevelopment.txt. Quest and real-peer multiplayer performance were not tested.
