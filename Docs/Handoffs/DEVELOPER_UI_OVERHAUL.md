# Developer UI overhaul

Owner: developer-ui-overhaul. Target: origin/Evan_Sprint_4.
Source branch: codex/developer-ui-overhaul.
Base: e57fad972b6ba89de03af560fb8bbcbdf49ba6b6 (intentional dependency on the nearby Bluetooth/name UI, PR #170 and its Core dependency #169).
Approved Core: d8848a0f, isolated branch core/developer-god-mode. Evan approved exactly IDeveloperGodMode { bool GodModeEnabled { get; } bool CanSetGodMode { get; } void SetGodMode(bool enabled); } and requested damage/death protection plus easy slow/fast flight.

## Behavior

Developer root is three columns by four rows: Weapons / Bots / Disasters; Map / Mode / Third person; God mode / disabled Coming soon / disabled Coming soon; Rebuild / Spectate / More tools.
Bots and More tools use expandable sections. Map offers a flat preview list, status filters and pointer-operated name search. Mode uses common rules, value choices, and advanced rules. Disaster tuning starts collapsed. The root owns rebuild and third-person controls. Room owns networking. Child tool Back returns to its owning page.

God Mode is default off and currently restricted to the local authoritative player in permitted Demo mode. It blocks character damage/elimination and the world drowning/elimination path. Look and move to fly, hold sprint for fast travel (3 vs 18 scaled units/second); release movement to brake. Jump rises; desktop Ctrl descends. Looking down also descends. It is not enabled for remote guests or competitive modes; guest authority/replication requires a separate verified extension. Losing permission disables it.

## Validation

Focused entry point: DeadlyJenga.DevTools.Editor.DeveloperUiChecks.Run. It opens the actual Game scene with Castle/demo configuration, operates production menu bindings and scripted pointer rays, captures rendered PNGs, checks navigation and God Mode protection, and restores scene/setup state.
First candidate 02e5203d647f87db2ed4b96393ae939a5a121008 compiled production assemblies but failed test-harness compilation (missing SessionState default arguments). Corrected in ae07d3b215a42868f862b28527eb38b583468c36. Runtime evidence pending; no headset, guest, APK or integration claim.

UI is Presentation/Visuals; flight/health is Characters/Player; world elimination guard is GameSession/Modes; the approved contract is Core/Session. DevTools owns the explicit fixture. No scene, package, project setting, or existing asset moves.

Status: checkpoint, not merge-ready. Final screenshots and exact runtime result must be recorded before handoff.
