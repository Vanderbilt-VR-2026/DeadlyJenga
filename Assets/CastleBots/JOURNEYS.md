# Survivor trips and gap traversal

This replaces the original one-hop, height-rewarded wandering with trips through a live surface graph. Nine sample points per stable crate include launch/landing positions near its edges. A frame-sliced search joins walk, jump, jump-and-vault, lower drop, and drop-and-catch links; it can take intermediate sideways steps and replan a useful partial route rather than requiring a whole-castle route in one search.

Vault links check a physical face at an airborne intercept, including capsule clearance before the catch and clearance for the actual lift-over motion. A face does not have to be within vault reach while the bot is still on its starting platform. Downward links can walk off under a low ceiling, cross a gap and catch a lower lip. Execution still uses the original survivor movement/jump inputs and its real TryFindLedge query. No new jump, flight, position teleport or easier vault mechanic is granted to bots.

Trips pick different locations and height bands, with recent-visit penalties and no unconditional score reward for moving up. Personalities have Journey Drive, Vertical Variety, Hide Preference, Travel Seconds and Rest Seconds. Bots start a trip after spawning, take a safe local step while longer planning is in progress, and may take bounded strategic/chill rests between trips. Immediate danger cancels those rests. Careful bots can still descend an ordinary 2m course; caution is a cost/risk preference rather than a blanket ban on leaving the roof.

Existing personality assets retain their saved values for existing fields. The four shipped assets now include journey settings: Careful takes longer rests, Daredevil makes longer trips with a wider height range, and Goofy sits between those extremes. Custom assets use script defaults for fields they do not yet serialize. Tune the actual assets in Resources/CastleBots to customize an existing project.

The brain exposes Activity, TripDestination, RemainingWaypoints and SetJourneyDestination(destination, seconds) for inspection and controlled scenarios. For an isolated traversal test, set the destination explicitly rather than assuming that a free-roaming bot will always choose the highest nearby crate. The previous autonomous single-crate assertion should use that hook now that height seeking is deliberately optional.

## Verification boundary

The changed runtime code was compiled separately with the project's Unity compiler/reference response files, writing only into Temp/SideBotRoutes. That does not refresh Unity, save a scene, enter Play Mode or alter the running main-thread tests.

Actual route execution in the castle remains to be verified. An explicit Tools > Castle Bots > Check Gap Links (Play Mode) menu checks isolated physics links for a low-ceiling edge approach, a 1.6m gap to a ledge 2m higher, controlled descent, unreachable gaps and blocked headroom. It does not run automatically. These link checks are not a substitute for observing bot execution on the interior purple-ring route in both directions, height diversity over a longer session, and frame time with nine bots.

Tools > Castle Bots > Check Gap Execution (Play Mode) additionally spawns one temporary survivor and uses its normal motor to attempt the fixture route in both directions. It requires a free NPC slot and writes JourneyExecution.txt, including movement decisions and explicit success/failure lines. Temporary fixtures and the test bot are removed afterward; existing bots and the saved scene are preserved. This action is manual only, with no queued request or automatic editor callback.

Launch staging now checks for safe footing only as far as the current waypoint and approaches within 0.12m. The old forward probe could extend past an otherwise safe staging point and reject the gap beyond it. Frame-sliced graph edges also consistently use their recorded source position while a local fallback is moving the bot.

The first castle trace exposed repeated local loops despite substantial horizontal travel. Local fallback scoring now rewards progress toward the selected trip and penalizes recently reached locations for 18 seconds. Covered/invalid landing samples no longer consume the link-attempt limit, and the bounded planner can evaluate up to 256 real links/48 expansions while retaining two sample evaluations per update. These follow-up changes compile, but need a fresh observation before claiming the loops are resolved.

The planner is bounded and reactive, not a proof that every visually plausible route is connected. Moving/destroyed blocks invalidate landings and routes. New traversal mechanics still require an adapter; existing jump/vault dimensions are read from the shared motor. No hand AI, UI, physics settings, scene or population-factory file was edited for this change.
