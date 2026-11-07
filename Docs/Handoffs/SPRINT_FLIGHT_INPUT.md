# Sprint and flying-hand input fixes

Owner: D5 / 01a1797a-ac39-7fb2-9403-cf77bbd734ba. Base: `8c03e7be22f89ff5bc4f5cf73317dabe818930b4`. Task branch: `codex/sprint-flight-input-fixes-20261106`. Integration owner: MAIN, target `origin/Evan_Sprint_4`.

## Behavior

The installed OpenXR Touch and Quest Touch Plus layouts expose `thumbstickClicked`; `Primary2DAxisClick` is a usage, not that control's name or alias. The old name-only lookup returned no button on those layouts. The resolver now supports their real button and the older named controls. Survivor sprint uses that same resolver for fresh presses and held state. The non-dominant movement controller remains the sprint controller; keyboard Shift remains its existing toggle. Menu neutral checks also see the real held click, preventing automatic sprint activation on menu close.

Flying-hand movement uses the same stick-capable controller selector as its menu neutral checks. The general controller selector still supports tracked controllers without sticks for avatar/equipment tracking. The bridge supplies the selector to the independent HandDemo assembly through a delegate; no assembly/Core changes are needed.

Hand trigger/grip lift and menu movement checks share a 0.12 deadzone. Tiny analog noise no longer requests lift or indefinitely blocks menu resume; deliberate held controls still block the session's normal release gate. Controller identity/pair changes reset snap arming, and absent controllers cannot count as neutral. Single-controller/mixed tracking keeps its existing modifier plus neutral-rearmed snap comfort. Dual controllers retain continuous motor yaw and dominant-stick pitch. Explicit keyboard movement and Q/E lift work before the XR controller branches, including when simulator controllers are tracked.

No camera, network serialization, GameSession, contact-force/profile or externally-driven bot logic changed. Camera follow has a separate owner and candidate; this branch does not claim to solve camera lag by changing the requested input heading.

## Checks and limitations

- Source-only Roslyn compilation of the three current HandDemo sources and 120 current Characters sources passes with zero diagnostics using retained Unity references. This is not Unity import, weaving, gameplay or device evidence.
- An initial cached response-file compile omitted the newer comfort-preferences source and failed; current assembly source enumeration corrected the check. That failure is preserved in the external evidence, not treated as a gameplay defect.
- Diff whitespace check passes. The new `SprintFlightInputChecks.CheckBindings(Action<bool,string>)` iterator also compiles with zero diagnostics against those fresh runtime DLLs. The existing QA driver must advance it from the normal PlayerLoop; its installed-layout binding/lift checks have not run natively. It creates untracked, unassigned test devices so they cannot take over real gameplay. No automatic runner or new framework is installed.
- Actual Unity movement, camera, multiplayer and Quest behavior remain pending. MAIN integrates serially; QA alone runs the combined current snapshot. A pushed checkpoint does not establish APK inclusion.

## Required combined runtime acceptance

Use real installed OpenXR Touch/Quest Touch Plus layouts through normal PlayerLoop input, with actual alive survivor, correct role, menu closed and genuine focus/arrival gates. Do not force health, focus, pose, suspension or contact rules to create a pass.

Sprint: record effective play style, dominance and selected device; compare walk displacement with released-click sprint displacement and stamina drain, then toggle off. Cover both dominance settings and eligible seated, standing, dual-controller and mixed tracking paths. Preserve dominant look/jump/shoot and non-dominant movement; check keyboard Shift separately. Capture 3-4 useful frames with state/timing.

Flying hand: record actual motor/palm/camera position and orientation, selected devices, input axes, menu/focus/suspension, profile confirmed/force and contact-palm eligibility. Accumulate unwrapped motor yaw beyond 360 degrees in both directions with dual sticks; look must not add translation. Capture four actual orientations and a short motion sequence. For single/mixed controls require a genuine live neutral sample before each snap, one snap per rearm, and no held-input snap on reconnection. Verify 0.01 lift noise permits normal menu release, deliberate held lift remains blocked, and keyboard W/Q/E reaches the motor with XR active. Preserve physical HMD tracking and existing contact-force rules.

This acceptance belongs to the exact combined source and fixture identity. Any actual headset comfort result requires testing on the headset.
