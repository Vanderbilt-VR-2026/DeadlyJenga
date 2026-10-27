# Powerups, weapons and Murder Mystery behavior

## Random powerups

The previous system had manually spawned pickups and eight permanent test-map stations. The new host-owned spawner samples ordinary map blocks at every height. It rejects submerged, obstructed, steep and rapidly moving surfaces. It does not restrict selection to a map's highest surface.

Controls are available under Play → Random block powerup settings on desktop and Powerups → Random spawn settings in the VR menu. They cover enabled state, mean interval, timing variation, maximum active random pickups, uncollected lifetime and per-item weights. Percentages are normalized from the weights; zero disables an item, and all-zero weights pause spawning. Existing random items still expire when spawning is disabled. Manual items, stations and death drops do not use the random-item expiry timer.

Defaults are one attempt every 15 seconds, ±25% timing variation, at most eight random pickups and 90 seconds before expiry. Regular modes use speed 20%, jump 20%, blaster 15%, sword 7%, revolver 8%, rifle 15%, glove 10% and powered armor 5%. Murder Mystery defaults to speed/jump only so random weapons do not erase the role economy. The host can change that pool explicitly. A failed surface search does not accumulate a burst of overdue spawns.

Random pickups follow their supporting block. Their support index, local position and remaining lifetime travel in the existing match snapshot and are restored after authority migration. An inactive, submerged or overturned support removes its random pickup. Spawn work is capped at 32 candidate probes per attempt; the normal interval does not scan every block each frame.

## Bot decisions

The modular objective framework now has a bounded `pickups` query, `collect-pickup` interaction and authoritative `pickup-collected` completion fact. Every shipped mode definition includes the optional acquisition template. Arrival alone does not grant equipment or mark the task successful.

Bots notice nearby exposed pickups, mostly in front of them. A stable per-bot/per-item roll avoids the entire population pursuing every item, and avoids rerolling every frame until a decision changes. Interest also depends on the improvement over the current slot, distance and height difference. At most four candidates survive ranking. Carrying a flag, immediate danger, an infected/enemy role and protected Mystery role equipment prevent opportunistic collection. A pickup is rechecked for value and legality at collection, so a stale assignment cannot silently downgrade a newly acquired weapon.

Acquisition remains a low-priority task. Mode objectives and emergency refuge can interrupt it. Failed routes use the existing timeout and retry controls. Bots do not teleport to an item or receive equipment just because they saw it.

## Weapon execution

The shared Characters attack skill uses the existing host-authoritative weapon commands. It supports the blaster, dual rifle, sheriff revolver, sword, boxing glove and powered-suit plasma. Unarmed punching remains the fallback. Bots apply collected speed and jump effects through the same character effect path as the player.

Weapons have effective-distance limits. The rifle/revolver use shotgun mode nearby and precision mode farther away, with different enter/leave thresholds to prevent rapid switching around one distance. Bots wait for drawing/mode changes, react with different delays, use modest aim variation, lead moving targets and take brief pauses between bursts. These are CPU rules; no language model is called during play.

Bots holster for travel, climbing, vaulting, flag carrying and loss of a firing opportunity. From safe footing, a useful ranged shot can interrupt a planned approach rather than forcing a melee chase. They check cover and the first actor along the firing line. Shotgun fire is withheld when another actor is inside the spread corridor. Plasma is avoided at very short distance because of its splash. Host casts, cooldowns, damage, team rules and Mystery rules still decide the actual outcome.

This is deliberately simpler than a tactical shooter AI: it does not provide coordinated suppressive fire, detailed cover-peeking, ammunition management or learned aiming. Sight is sampled on a bounded cadence, and target selection comes from the existing mode director rather than a separate omniscient combat search.

## Murder Mystery assessment

### Murderer

The existing implementation knows its own role and considers observed potential victims. It lowers attack utility when witnesses are nearby and can investigate/roam when no attack is selected. Witness counting is now capped and distance-filtered before visibility work. Weapon execution now keeps the sword concealed during a long pursuit and draws it within usable reach; the previous mode tick could expose a special weapon merely because an attack assignment existed.

This is a working stealth-versus-opportunity heuristic, not human deception. There are no alibis, deliberate false reports, conversations used as evidence, coordinated distractions, persistent suspicion estimates or explicit escape plans after a witnessed murder. Reducing witness utility is not a guarantee that the murderer will always wait for perfect isolation.

### Sheriff

The sheriff reacts to witnessed murderer strikes with visibility checks, not by asking for another participant's secret role. Once a legal attack target exists, the new weapon skill can engage at revolver range instead of relying on a close melee swing. Grace, wrong-innocent penalties and role-issued weapon rules remain authoritative. A dead sheriff drops the actual revolver; an eligible innocent must collect it to inherit the role. Reporting the corpse alone cannot duplicate that weapon.

The sheriff does not currently infer a specific guilty player from a footprint color, interrogate players, escort innocents or maintain confidence in contradictory social evidence. Known identity and remembered position are separate: a remembered point does not provide a hidden live target transform.

### Innocents

Innocents already receive escape routes from witnessed threats, close-range emergency defense, corpse-reporting tasks, revive tasks and sheriff recovery. They can acquire useful movement powerups and, where the host allows weapons, use the shared weapon skill against legal attack assignments. They do not receive every hidden murderer as a hostile. A generic threat helper previously treated bystanders as enemies in Mystery; it now uses the mode's observed attack/evade facts.

They do not currently form trusted groups, reason about alibis, deliberately shadow the sheriff or exchange private knowledge socially. Panic/escape is geometric and rule-based rather than a model of fear or trust.

## Verification status

Static compilation uses Unity's actual Roslyn compiler inputs for Core, Characters, GameSession, Presentation and DevTools. Runtime evidence is recorded separately in `Logs/Validation/PowerupBots.txt`. The targeted suite checks low/middle/high fixture surfaces, weights, caps, moving supports, expiry, an actual director pickup completion, physical weapon damage, holstering, cover and a production Mystery witness scenario. Test results must be read from the completed report; this document does not turn an unrun fixture into evidence.

The completed editor run passed 71 checks with zero logged runtime errors. It verified spawning at three heights (including a grouped collider mapped to a logical block whose own collision body is disabled), weights, caps, support movement, expiry, real bot collection and director completion, actual speed/jump effects, physical damage with all six supported attack types, nearby shotgun selection, travel holstering, restored traversal permissions and holding fire through cover. Two actual Photon peers connected, transported three validated snapshots covering support movement and expiry, and shut down cleanly.

The production Murder Mystery scenario also passed: the sheriff did not target the unrevealed murderer, kept its weapon concealed, then drew and fired after witnessing a real strike and eliminated that murderer. A witnessing innocent received an escape assignment. This is controlled runtime evidence for those specific behaviors; it does not establish human deception or the quality of an entire natural match.

Five existing combat regressions also passed with zero failures and zero runtime errors: approach-face-hit, mystery-murder, ctf-contact, mystery-unknown-contact and mystery-repel. They preserve the ordinary motor windup/contact path, a fatal murderer sword attack, production CTF contact, restraint around an unrevealed murderer and defense after witnessing a real strike. Results are in `Logs/Validation/BotCombat.txt`.

The isolated physical target received 55 rifle damage, 85 revolver damage, 60 sword damage, 80 glove damage and roughly 33 plasma damage (falloff-dependent). Plasma from the previous weapon is explicitly drained before the next weapon test. This matters: an earlier fixture could incorrectly credit residual plasma to a later gun. A real integration bug also surfaced and was corrected: host bots use avatar.remoteDriven for externally supplied poses, so the equipment paths must distinguish them by the actual externally driven motor rather than excluding that avatar flag.

The independently recorded shared firearm fixture completed all four desktop zoom checks and verified menu FOV restoration and restoration of the transient input-settings fixture. Its report is `Logs/Validation/FirearmAimShared.txt`; it is separate from bot behavior acceptance.

Headset comfort, Quest rendering/CPU budgets, many-bot full matches and the subjective realism of all three Mystery roles still require their own playtests. Short editor fixtures cannot certify those outcomes.
