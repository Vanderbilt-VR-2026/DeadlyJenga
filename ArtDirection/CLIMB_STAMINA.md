# Candy climbing stamina

Presentation is separate from movement and block physics. No scene rebuild, block prefab, collider, mass, joint or collision-layer edits are required.

## Live tuning

During desktop play: Esc → **Live climbing stamina settings**. Sliders and curve buttons affect the local survivor immediately. Inspector fields on `CastleSurvivor` work during desktop or VR play too; play-session edits are temporary. To retain defaults, edit the survivor prefab outside Play Mode.

- `climbGripSeconds`: capacity in effort units; default 4.
- `climbDrainPerSecond`: effort units/second; default 1, zero pauses consumption. Moving consumes full effort; stationary clinging consumes 85%.
- `climbRecoveryPerSecond`: fraction of the bar recovered per second; default .65 = 65%/second on the linear curve. Zero disables recovery.
- `climbRecoveryDelay`: secure-footing rest before regeneration; default .45 seconds.
- `climbDrainCurve` / `climbRecoveryCurve`: Linear, PowerIn, PowerOut, Exponential.
- `climbDrainDegree` / `climbRecoveryDegree`: 1–6; higher values bend the response more strongly. PowerIn starts slowly; PowerOut starts quickly; Exponential accelerates.

Curves describe cumulative consumption/recovery over normalized time. Full moving-climb duration is capacity / drain rate. Full recovery from empty is 1 / recovery rate after the rest delay. Both remain finite for all curves. Partial bars advance by inverse-curve phase, so live changes preserve the current fraction and don't grant stamina. Different curves redistribute effort/recovery over that duration.

Recovery still requires secure, reasonably level, slowly moving footing. Airborne release, changing blocks and relatching never refill stamina. Existing forced releases on falling, thrown, spinning or impacted blocks remain intact.

## Presentation

`CandyStaminaHud` builds one lightweight rounded UI bar for the human survivor. Lavender shell, glossy mint juice, warm low-stamina color. Full opacity while climbing; 46% while recovering or awaiting safe footing; fades away at full. Desktop bottom-center and VR head-relative below the view. No raycast blocker or new input bindings.

`CandyPlayerAudio` requests Exertion, Recovery or Exhausted breathing loops for the local player only. Existing pooled voices, Character bus, master gain and focus fade apply. Stops/fades at full, menus, role changes or disable. Inspector `staminaBreathingVolume` changes its level.

Three original synthesized airy huff/puff loops are installed into `CandyAudio/Audio/Breathing` and appended to the default Audio Bank. Replace those cue variants with recorded performances later without changing gameplay. Runtime synthesis is a small cached fallback when these bank cues are absent. Existing cue indices and recordings are preserved.

## Reapply / verify

Keep the survivor's three partial files, `CastleSurvivor.Stamina.cs`, `CandyStaminaHud.cs`, `CastlePlaySession.cs`, and the audio sources/bank/clips. No authored scene changes are necessary. For another layout, use the shared survivor prefab/motor; the bar follows stamina, not castle coordinates.

Editor menu **Tools → Castle Prototype → Install Stamina Breathing** installs only missing cues, preserving authored replacements. For the opt-in regression run, write `stamina` to `ArtDirection/SlopeChecks.request` while Unity is stopped and refreshed. Reports: `StaminaChecks.txt`, `ClimbChecks.txt`, `CandyAudio/BankValidation.txt`; screenshots: `StaminaClimbing.png`, `StaminaRecovery.png`.

Physical headset readability/comfort and subjective breathing quality still need a device playtest.
