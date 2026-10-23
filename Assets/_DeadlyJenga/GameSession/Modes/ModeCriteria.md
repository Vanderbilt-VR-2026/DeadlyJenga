# Match criteria and default profiles

GameSession owns cumulative totals, team outcomes and mode defaults. Presentation reads those facts. A personal contribution ranking never substitutes for a team victory.

| Mode | Live priority | Default individual criterion | Results | Recommended rules |
| --- | --- | --- | --- | --- |
| Sandbox | Tools and current activity; standings on request | Active status | Eliminations, deaths | No timer; automatic disasters available |
| Last Survivor | Remaining survivors, clock and hazard warning; status roster on request | Time alive, used for final results | Time alive, survival status, average height, eliminations | 5 minutes; last survivor wins immediately; automatic disasters |
| King of the Hill | Hill marker, scoring radius, clock and hill-time standings | Grounded seconds inside hill | Hill time, eliminations, deaths | 5 minutes; hill refresh every 30 seconds; safe island; health/punching |
| Color Control | Team points, ownership and claim progress | Team points | Team points, personal completed claims, eliminations, deaths | 5 minutes; safe island; health/punching |
| Capture the Flag | Flag locations/status, home delivery condition and team capture score | Personal captures | Captures, returns, eliminations, deaths | First to 3; 10-minute limit; win-by-two off; safe island |
| Last Team Standing | Team respawns and active roster | Personal eliminations | Eliminations and deaths | 30 shared reserve respawns/team; 10-minute limit; timeout compares active plus reserve lives; safe island |
| Infection | Remaining survivors, infected threats and clock; status roster on request | Survivor time, used for final results | Survivor time and actual infections caused | Initial infected enabled; 5-minute survivor escape deadline |
| Murder Mystery | Private role instructions, public bodies/reports and round clock | Active status; no public live kill ranking | Reports, revives, eliminations | 8 minutes; safe island; normal low-damage punches; automatic disasters off |

All values remain configurable through existing host rules. Restore recommended defaults in the Modes panel deliberately resets that mode's settings; custom metric selections remain available. Custom metrics are added to the report rather than replacing the mode's normal contribution columns. Average height remains a diagnostic collector and a survival result, not a generic competitive leaderboard.

## Authoritative accounting

- Capture/return counters advance only on successful flag delivery. Captures belong to the carrier, not everyone on the scoring team.
- Completed claims credit the grounded contributors on that block when ownership actually changes. Holding a friendly block does not repeatedly grant claims.
- Hill time has its own accumulator, so changing the display metric cannot corrupt hill progress.
- Deaths/credited eliminations advance in the validated elimination path. Self-deaths do not grant eliminations. Actual Infection conversions credited to infected attackers have a separate counter from ordinary kills.
- Mystery reports count accepted first reports; revives count completed authoritative revives. Live Mystery snapshot kill/death totals and ranking are redacted because they can identify hidden roles. Round conclusion exposes final contributions.
- Cumulative contribution totals are reset for each new round and restored on authority migration. Abandoned territory claims preserve their decay clock rather than receiving a fresh ownership interval. Mystery still intentionally restarts after host migration because secret roles are not replicated.
- Ticket-mode timeout compares active lives plus reserve respawns; its live respawn display continues to show the reserve pool alone.
- Infection timeout awards the survivor faction, rather than an arbitrary individual selected by elapsed survivor time. Individual score ties at a timed finish produce a draw rather than an actor-id win.

## Validation

`Tools > Deadly Jenga > Validation > Check Mode Criteria`, or an explicit `Logs/Validation/ModeCriteria.request`, exercises the actual simulation model, shipped defaults, CTF capture/return/ownership accounting, deaths, authority restoration, resets, hill accounting under metric changes, infection timeout and Mystery snapshot secrecy. This is model evidence, not headset or physical interaction evidence.

The integration owner must compile affected boxes, run this gate and targeted real Game/HUD scenarios after integrating the worktree. Real Quest comfort/readability, controllers and two-device replication remain separate runtime checks.
