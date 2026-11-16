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
Tested source and verified remote candidate: 62c45a9e76afd63a0ff6f425bdc50a0d4e91f6e5. Windows Standalone desktop Play Mode job 45654986319d419f9fdd78d6f55f970d passed all 66 assertions with zero runtime errors and restored initial scene/preferences. It produced 20 full-resolution PNGs. The capture camera temporarily frames the actual world-locked production tablet; these are staged inspection views, not headset eye-placement measurements. Earlier duplicate-tablet, clipped and hidden/overlapping disaster-selector captures are superseded. Final disaster choices use a separated three-column thumbnail grid with foreground materials; the launch button takes priority over its enclosing card.

Checks cover twelve tiles, three columns/four rows, two disabled placeholders, actual page bindings and pointer targets, child Back, top-tab route reset, map search empty/reset states, disaster selector hiding and foreground materials, damage/elimination protection, actual character flight fast/slow distance ratio above five, braking, paused movement, damage restoration and permission revocation. No broad disaster performance claim is made.

Reproduce with the explicit fixture entry point above in a Windows Standalone editor. Output: Logs/Validation/DeveloperUi/report.txt and 01-developer-root.png through 20-stamina.png (numbered captures). The final report ends COMPLETE UI assertions passed with no runtime errors and CLEANUP restored name preference and initial scene state. git diff --check also passed.

An earlier Android-target editor run at e4e3d7f0fcd23d3b2e0e2885c9d9912706b24919 passed UI assertions but failed the runtime error gate with repeated First two bone weights should have non-zero sum errors. Windows runs did not reproduce this; platform/slot correlation is not a proven cause. Android/Quest rendering remains unresolved. Physical controller interaction, headset comfort/fit, multiplayer guests, APK inclusion and combined target integration are not validated here.

UI is Presentation/Visuals; flight/health is Characters/Player; world elimination guard is GameSession/Modes; the approved contract is Core/Session. DevTools owns the explicit fixture. No scene, package, project setting, or existing asset moves.

Review: Core PR #172 (core/developer-god-mode), implementation PR #173 (codex/developer-ui-overhaul). The implementation PR is intentionally stacked on the exact approved Core branch; that branch depends on the Bluetooth/name candidate above. Intended final target remains origin/Evan_Sprint_4, last tested baseline 1359ca474fb8a0bc86cdd72dca7dab5e3e631a71. No shared target merge or APK build performed. Documentation-only descendants of the tested source do not change the tested runtime code.

Status: desktop implementation and rendered review complete; draft retained for integration/platform follow-up. Next action: designated integrator reviews the dependency stack, validates the combined target, and schedules Quest interaction/rendering validation. No additional Core approval is needed for this exact contract.

## Icon and Weapons capture follow-up

At user request, Map reuses the existing PlayerTabletArt/MapMode tower artwork. No assets were moved or replaced. Candidate 9af83fb86bd2b13a7571c956774571975e89f7a4 passed 68 assertions with zero runtime errors in Windows desktop Play Mode job 7ccf2cb5ed464337b0c909146bc82b04. The fixture now captures 21-weapons.png after opening the actual Weapons page, plus the restored Map icon on the root. The existing weapon-wheel layout still has small/clipped labels; this capture documents its current state, not a completed weapon-page redesign. Five generated icon proposals remain review-only outside game assets. No new Core changes, integration or APK claim.

## Approved illustrated icon revision

The subsequent user approved God Mode and Third Person for installation without embedded labels, requested default-character torsos for Bots, and an Alien Blaster crossed with an in-game melee weapon for Weapons. Source 47c6fa8d63e426ba84b5b4410b69582d63f72fb8 adds GodMode.png, ThirdPerson.png, BotsV2.png and WeaponsV2.png plus metadata in Presentation/Visuals/Resources/PlayerTabletArt. Original Bots/Weapons textures remain intact. The label-to-art mapping uses these variants; button text remains separate.

Art was generated with the built-in image tool. Prompt direction: retain the approved purple figure/cyan camera-orbit and shield/halo flight designs without text; use three default faceless spherical-head character busts for Bots; cross the original red Alien Blaster with the existing MurderSword silhouette. References were the approved concept sheet, an actual default-character render, the original Alien Blaster render and the repository MurderSword icon. Generated files have genuine transparent alpha (0-255), use the existing 256px UI import settings and are read-only art. The More tools toolbox/chevron/hover-outline design remains a proposal outside production. No Mode icon approval was inferred.

Desktop visual check: job c1e6bf9c101540baac2a7ec48f50f06b at the exact source above passed 68 assertions with zero runtime errors and restored scene/preferences. The actual production Developer root was inspected at its 72px icon sizing: all four textures render with transparent backgrounds and separate labels. No Core, gameplay, scene or package changes; no integration/APK/headset claim.
