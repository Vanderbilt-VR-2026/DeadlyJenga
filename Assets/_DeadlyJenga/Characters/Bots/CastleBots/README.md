# Survivor bot pipeline

Bots run on the authoritative host and send ordinary movement, jump, vault and climb intent through CastleSurvivor. They do not receive stronger traversal mechanics or teleport to finish routes. Voice is independent of this pipeline. Social following and hesitation cannot override movement in competitive modes.

## Decisions and actions

Normal modes now use the modular GameSession objective director and Resources/BotModes definitions. Read GameSession/Modes/AGENTS.md and GameSession/Modes/BotObjectives/README.md before extending mode behavior. Characters executes assigned Core goals and optionally reports token-checked progress through IBotObjectiveExecution; arrival does not manufacture gameplay success. Legacy IBotObjectives providers remain compatible.

GameSession supplies Core BotObjective values. CastleBotObjectives chooses using urgency, personality, distance, commitment and teammate assignment pressure. Moving targets are compared with the actual trip destination, so slow cumulative motion updates pursuit. Removing an objective cancels its old route.

CastleBotActions owns action transitions, movement classification, progress monitoring and recovery. Temporary transit goals use a stack: child detours suspend the parent destination and its deadline, then resume it after arrival. CurrentAction, MovementState, StationaryReason, PlannerAge, Recoveries, MovingSeconds, UnexplainedIdleSeconds and LongestIdle are available for inspection. Guarding, contesting and escorting can deliberately hold within their assigned area; competitive objectives never use exploration naps or joke jumps. The player motor still enforces round, life and flag-carrying restrictions.

The mode supplies CTF roles based on proximity: a bounded group defends or recovers, up to two escorts support a friendly carrier, and remaining bots attack or recover a stolen home flag. Close attackers seek valid rear/side footing around a shielded carrier instead of repeatedly punching its frontal shield. Positions within guard/escort/contest areas spread teammates across valid footing. Enemy perception includes a broad vision cone, short recognition delay and six-second last-seen memory; explicit public game information remains public.

## Navigation

BotWalkingRoutes builds a shared runtime NavMesh from stable collision geometry. Partial paths on the walkable island lead to a traversal frontier such as the base of a stair. It only supplies physically validated walking waypoints; it never moves a character or writes an asset. Changed geometry queues an asynchronous rebuild. Local collision and support checks remain authoritative while it rebuilds. Runtime navigation data is removed when the population is destroyed or its world changes.

CastleBotJourneyPlanner handles block jumps, vaults, climbing and controlled drops. It returns partial routes after bounded searches and has a two-second simulation-time deadline. Flag carriers can explicitly stage a descending departure and commit to a discovered ascending stair approach. Ledge catches must match the intended landing height; safe drop limits follow the match's fall-damage rules. BotTraversalCache shares recent landing and link checks between equivalent movement capabilities. Cache entries expire after half a second, and actual waypoint adoption/execution revalidates geometry. Different sizes, speeds, jump dimensions, grip, carrying restrictions and teams do not share incompatible cached links.

The population services pending planners in age/urgency order. BotPlanningBudget gives each owner simulation-time credits, with shared operation caps and a measured work-time ceiling per rendered frame. Rendering frequency no longer determines the nominal number of graph checks per simulation second. CPU overload can still delay work; telemetry exposes pending time.

Ordinary walking uses corridor look-ahead, acceleration and bounded teammate separation. Each adjusted step must remain supported and collision-clear. Traversal staging and airborne commands bypass grounded separation. Recovery retries a safe local approach and remembers recent stops; it does not invent a path across an unreachable gap.

## Personality

Resources/CastleBots contains Balanced, Careful, Daredevil and Goofy assets, discovered and sorted by display name. Existing bots retain their chosen asset; a social tuning adapter may clone and customize it. Bravery, survival preference, exploration, pace and journey/rest preferences change choices. They do not grant extra movement abilities. New mechanics need an ICastleBotTraversalAbility adapter.

## Population

Auto-fill reconciles survivor count against target minus connected humans, adding missing bots and trimming excess fillers. Manual spawn is not capped by the population class; callers must choose a sensible count. Initial placement and map rebuilding use clear supported surfaces. Match rules own deaths and respawns when a match runtime exists.

## Validation

Tools > Deadly Jenga > Validation > Check Bot Pipeline runs the actual Game scene and Mode_ctf geometry with populations 1, 10 and 50. One bot receives a controlled cross-map objective; `"carrier":true` instead places it once on the enemy flag and requires a real scored return. In that controlled fixture, later movement, pickup and scoring are ordinary gameplay. groups use ordinary CTF objectives. No scores or captures are fabricated. Reports and per-bot CSV go to Logs/Validation/BotPipeline.txt and BotPipeline.csv. A passing movement smoke test is not proof of a completed CTF round or Quest performance.

For an already-open idle Editor, write Logs/Validation/BotPipeline.request.json with, for example:

    {"seconds":45,"fps":60,"populations":[1,10,50]}

Wait for compilation/import to finish before submitting. Write the complete JSON atomically. BotPipeline.cancel cancels an active check. Save edited scenes first; the original scene setup and match choices are restored afterward.

Record Matches also writes bots.csv (one sample per bot per video second) and simulation.txt beside each MP4. These distinguish deliberate holds, route waits, physical traversal and blocked movement. Video frame rate controls sampling; simulation uses a whole-number multiple at least 60 Hz.

Required follow-up coverage includes carrier returns, both directions through changed geometry, crowd bottlenecks, multiple seeds, real multiplayer peers and Quest CPU/thermal profiling. Do not infer these from an isolated traversal check or successful encoding.

Directed Guard and Contest assignments use the same ascent recovery as Travel. Climb input is held during the approach so a narrow ledge does not have to be crossed before gripping. Distant elevated destinations do not reverse a descending transit into premature ascent. Carriers require several legal ascending links toward their destination before committing to a staging point. Bot identity exists at spawn, before delayed social attachment.

Traversal progress is measured from displacement every half simulation second. An airborne or climbing flag alone cannot hide a stationary bot: after 2.5 seconds without progress, ordinary blocked recovery applies and proximity cannot renew an execution lease. Ground probes allow normal capsule skin penetration at supported edges; actual air and vertical walls still fail grounding checks.

## Close combat

CastleBotCombat executes only an assigned Attack skill with a currently visible live subject. Mode rules own hostility, private-role knowledge and special attacks. Remembered points can be searched by navigation but cannot be punched. Disabled punching, carrying a flag, a downed target, friendly team, climbing, vaulting or lack of grounding suppress close combat. Existing player punch rules and animation still own the strike.

The local states are Approach, Flank, Face, Windup and Recover. The controller seeks about 1.2 metres of horizontal spacing, backs out of body overlap, faces the target through the motor's normal turn speed, waits a short reaction/windup, then requests one ordinary punch and recovers for at least the configured cooldown. Each windup is revalidated; a lost target, obsolete assignment, obstruction or escape cancels it. Yaw alignment uses horizontal facing because externally driven survivors turn their body without pitching their head. Strike aim uses the same torso point and fixed world-space reach as the authoritative action, with a small range margin.

Close approach samples eight supported candidate points at most once per .24 simulation seconds. Each must have room and a walkable connection; the controller never replaces a gap, change of floor or active climb with straight-line pursuit. Observed movement is sampled at .12 seconds, with horizontal lead capped at .6 metres. Carrier approaches penalize the front shield and reject paths through the target's body; crowd cost spreads attackers around accessible space. This is local positioning, not exclusive attack slots or a rule that enemies must take turns.

The actual strike lane is checked before winding up and again before requesting the punch. A nearer actor in the authoritative cone causes a withheld punch and a safe reposition attempt, including when friendly-fire damage is disabled but a friendly push would still be possible. The nonallocating collider buffer is bounded to 64; saturation withholds the strike. PunchesAccepted means the motor accepted a request, not that it hit. Real damage, push and mode-state transitions are separate evidence.

Tools > Deadly Jenga > Validation > Check Bot Combat (or Logs/Validation/BotCombat.request in an idle Editor) exercises controlled initial poses and assignments in the real Game scene. Reports and detailed samples are BotCombat.txt/.csv; BotCombat.cancel stops it. The fixtures cover approach, close spacing, repeated cooldown/recovery, relevant mode outcomes, walls, allies, disabled punching, flag restrictions/shields, cancellation and an escaping target. All-mode acceptance runs separately exercise production target selection. Neither fixture assignments nor contract assertions establish human-equivalent play, Quest performance or real-peer multiplayer behavior.

The request can contain a focused case list, for example {"cases":["unleased-target","carrier-flank"]}. An opted-in execution provider must grant the policy before the objective can be selected. Older providers without the optional interface remain supported. The focused suite checks both policy refusal before binding and cancellation during windup. CSV samples are throttled to ten per simulation second; test assertions still observe every frame. The Unity Profiler marker DeadlyJenga.Bots.Combat isolates the local controller for further CPU profiling.

Assignment cancellation clears combat immediately. A valid in-progress physical step can finish on safe footing before navigation chooses another destination; cancelling a search or losing a visible target must not make the bot release its current climbing grip. The cancel-climb fixture assigns one initial physical Climb goal on a controlled four-metre climbable surface, revokes the lease after the real motor grips, then requires an ordinary completed mantle with no punches. It failed before this continuity fix. Geometry loss, motor permissions, grip limits and traversal recovery still apply.

A reachable unshielded opponent can be struck from existing safe footing even if there is no safe approach point, including across a small gap or while the opponent jumps. Movement still needs its supported path. The airborne-opponent fixture requires a real damage event while the victim is over .65 metres above the attacker; gap-reach requires the attacker to remain grounded on its original narrow platform. Neither grants extra reach or permits an airborne attacker.

The shared Player ground probe also recognizes support beneath the rounded capsule base. A nearby-contact gate precedes at most nine footprint rays, including diagonals, only when the primary cast misses during descent. The upward-facing surface and distance checks still reject air and vertical walls. This handles outer lip contacts that the smaller sphere cast and old four-direction footprint missed. The ground-lip fixture tests the actual capsule against controlled physical surfaces; full matches separately exercise map geometry.

Design references: [Michael Dawe's discussion of combat positioning and attack capacity](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter28_Beyond_the_Kung-Fu_Circle_A_Flexible_System_for_Managing_NPC_Attacks.pdf) informed the separation of assignment limits from physical spacing; this implementation uses local crowd-aware candidates rather than its grid manager. [Unity's physics performance guidance](https://unity.com/how-to/enhanced-physics-performance-smooth-gameplay) supports bounded, reusable nonallocating query buffers. No third-party combat package or copied framework code was added.
