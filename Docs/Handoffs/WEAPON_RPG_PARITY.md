# Left-handed RPG model parity

Task: weapon holding closeout, November 5, 2026. Owner: Weapon Animation,
Characters/Player.

## Change

The RPG's asymmetric model and optic stayed right-handed when the aiming
placement moved to the left shoulder. `CandyWeaponVisual.Present` now mirrors
the RPG model's local X scale for the left hand, using the existing glove
mirroring route. The right-handed model and existing glove behavior are unchanged.

Only `Assets/_DeadlyJenga/Characters/Player/CandyPlayers/CandyWeaponVisual.cs`
changes. Hand positions, weapon rotation, authoritative muzzle, ammunition,
HMD tracking and the RPG support target are unchanged. The support target has
local X zero, so this visual mirror does not move it. No Core, scene, package,
shader or Unity metadata change is required.

This corrects handedness parity. It does not establish that the large weapon's
screen coverage or every finger contact is acceptable.

## Portable verification

Use Unity 6000.3.23f1 and an isolated local Demo match. Equip the Rocket Launcher
through the game's equipment controls. Use two tracked controllers or the
project's existing XR simulation setup, and record which was used.

1. Set right dominance. Record player-eye and external held views at hip and
   while holding the dominant grip to aim. Keep triggers released for comparison.
2. Set left dominance and repeat with the same head pose, scale and framing.
   The model's asymmetric parts should mirror with the shoulder placement.
   The tracked hand, aiming direction and muzzle should still follow their
   original gameplay poses.
3. For the support-hand check, hold **both** the dominant aiming grip and the
   non-dominant support grip. Repeat for both dominance settings. Holding only
   the dominant grip does not test the support-hand correction.
4. The RPG support wrist target is `(0, -0.015, 0.69)` in `GripPose` coordinates,
   normalized by character scale. Check the actual support action is held and
   the rendered wrist reaches that target. Inspect the fingers in the external
   view separately: wrist alignment alone does not prove fingers wrap the handle.
5. Holster and redraw, then change dominance again. Check model visibility,
   handedness and support placement return correctly.

Record source commit, scene/mode, input method, character scale, camera and
screenshots with the result. A desktop or simulated-controller result is not
physical Quest stereo, comfort or performance validation.

## Evidence and integration state

- Source checkpoint: `d679cad49ed94af4d8b64978447ae15dcf5eb047`.
- The source tree passed direct C# compilation for Core, HandDemo.Runtime,
  Characters, GameSession and Presentation with six existing warnings.
- The earlier c1 Game baseline passed 23 assertions with zero errors and showed
  that the RPG was rendered. It held only the dominant aiming grip and therefore
  did **not** validate latched support or finger fit.
- Matching native after-views and the both-grips support check remain pending.
  Compilation and baseline assertions do not replace those checks.
- The integration writer recorded the same one-line correction in
  `83133b9e5a144431c8f34924e74ca48ae8355067`; inclusion was checked by exact file
  content, not assumed from branch ancestry. Shared publication and build
  inclusion must still be checked against the actual target.

The powered-suit pause/flight/fire corrections are a separate integration slice;
they are not dependencies of this model correction. No new Core approval applies.
