# Flying-hand camera follow

Owner: Camera, task `Flying hand camera follow fixes`.

Source branch: `codex/flyinghand-camera-follow-20261106`.
Integration target: `origin/Evan_Sprint_4`, applied serially by its current maintainer.
Base: `8c03e7be22f89ff5bc4f5cf73317dabe818930b4`.

## Change

Dual-controller VR previously smoothed the observer yaw after the motor had
already interpolated its rotation. At a constant 90 degrees per second, the
extra response of 4 trails by about 22 degrees at common frame rates. That
figure is a calculation from the old formula, not a headset measurement.

The observer now follows the actual motor heading directly during dual-stick
VR flight. It does not integrate joystick yaw separately. Physical headset
tracking, the physical palm position target, pitch routing, desktop smoothing,
single-controller snap turns and the delayed-first-pose alignment remain intact.
No Core, input, controller, bridge, prefab, package or settings changes.

The input owner handles controller eligibility, bindings and physical contact
readiness. Missing motor rotation must be diagnosed there rather than hidden by
a camera-only turn. This slice needs no additional Core contract.

## Checks and evidence

Status: checkpoint for source review; combined Unity runtime acceptance pending.

The fresh-checkout Characters source and all 183 DevTools Editor source files
compiled with Unity 6000.3.23f1 bundled Roslyn and retained Unity/package
references. This checks C# compilation, not Unity import, shader compilation or
gameplay. The initial cached QA response file referenced absent QA-only overlay
helpers; rebuilding the input list from the clean checkout removed those
non-source inputs without copying or altering them. Whitespace checks passed.
No Unity editor was launched and no headset result is claimed.

`FlyingHandCameraFollowChecks` is an explicitly constructed, disposable helper
for an existing QA scenario. It records real active-camera URP render callbacks,
motor yaw and input/contact state, physical palm position, observer pose and
tracked camera pose. It never generates input, poses or renders. Always save
its report and dispose it in the scenario's `finally` block.

QA should run these on the exact combined revision after integration:

1. With normal contact readiness and both controllers tracked, turn at least
   360 degrees in each direction, separately for left and right dominance.
   Construct the capture before each segment and check actual motor and observer
   unwrapped yaw with `CheckDualTurn(check, 360, direction)`. Hold the actual input long enough
   to record a full turn after capture admission. Per-render
   observer/motor yaw error must stay within 0.5 degrees. A missing motor turn
   fails its own prerequisite first.
2. Move the physical hand straight with neutral look and a stationary head.
   Capture at least 0.5 metres away from the water clamp and use
   `CheckTranslation(check, .5f)`. Both motor input and actual palm movement
   must be real. Do not substitute a directly assigned pose.
3. Combine translation and yaw/pitch, cross 0/360 in both directions, then release
   input. Inspect movement, grip and role/menu holds. Reacquire the second
   controller and repeat; check single-controller snap policy separately.
4. Reuse the delayed-first-valid-render and next-lean checks. Retain their 2 cm
   threshold, physical lean once, baseline retention and cleanup requirements.
5. Capture front, left, rear and right orientations plus one continuous full-turn
   and combined-movement clip in the actual selected map. Include motor, palm
   and observer state with the motion proof. Static pictures alone cannot prove
   rotation or input routing.

Do not treat synthetic XR as physical Quest stereo, comfort or performance
acceptance. Record the actual tested combined commit and publish the resulting
reports/captures in the integration PR when available. Source integration,
runtime acceptance and APK inclusion remain separate facts.

## Dependencies and handoff

Requires the stated base and the coordinated input owner's current producer
fixes for complete device behavior. No teammate's unmerged branch is a source
dependency. This patch does not supersede the input owner's changes and must
not replace their controller or bridge files.

Candidate and verified remote SHAs are supplied by the containing branch tip
and the integration PR. The commit carrying this record is the initial scoped
checkpoint. No earlier candidate for this November 6 camera slice exists.
