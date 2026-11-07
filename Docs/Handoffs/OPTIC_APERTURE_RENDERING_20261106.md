# Physical binocular optic aperture rendering

Owner/task: MAIN delegated optic-aperture writer / Correct physical optic aperture magnification rendering.
Branch: `codex/optic-aperture-rendering-20261106`.
Base: `9a95a3975107d34d7a64f74c8d7a4c2ac729b223`.
Candidate identity: the Git commit carrying this handoff, on the owned branch above. Pin its full object ID before integration or QA; publication alone is not acceptance.
Status: source checkpoint; native shader import, current-source Game captures, actual stereo and Quest pending. No Core change.

## Trigger and resulting behavior

Automatic physical Aim already moved the actual gun, optic, rendered palm and shot muzzle correctly. Its magnified image was a separate radius0.6m disc two metres ahead of the camera. An opaque Overlay/ZTestAlways shader painted over the correctly centered physical optic rim, leaving the image floating over the gun. Correct optic-anchor telemetry did not establish visual acceptance.

Replacing that with the old narrow physical scope opening alone would restore the earlier close-one-eye problem: at the normal0.5 avatar scale, its14mm inner radius cannot contain forward rays from eyes32mm either side of the center. Ordinary planar texture UV would also put the reticle at the close lens surface, with stereo disparity.

The current change adds a compact matching binocular rear eyecup to the existing rifle, blaster and revolver optics. Its opening has55×25mm inner half-dimensions in world units, independent of avatar scale, and a12mm-deep rim centered20mm behind the original optic anchor. The existing `Optic lens` child describes that exact opening. Original optic anchors, gun roots, palms, muzzle calculations, automatic raise, draw/mode mechanics, RPG mirroring, supports, motion/feed, input, networking and physics are unchanged. The character owns this addition to its own visual model; Presentation reads the existing named visual socket without referencing Characters types.

The magnified surface is a unit disc inside that actual opening, with normal world-geometry depth testing and depth writes. The real rim, nearer obstructions and the surrounding weapon remain visible and occlude it normally. The surface is nearer than the original narrow rear ring, so the old ring cannot cut the new binocular opening into a one-eye slit.

The shader derives its content/reticle UV from the actual eye-to-surface ray, in the optic's world-space axes. Parallel forward rays from both eyes sample the same center despite intersecting different points of the physical surface. The rim has natural stereo parallax; the shared monoscopic zoom image and reticle remain angularly centered. Main HMD transforms, field of view and both stereo view/projection matrices are never modified. Shader stereo instance/eye macros are retained; Unity's installed URP `GetCameraPositionWS` resolves the selected eye after the stereo-eye setup macro. See [Unity 6000.3 stereo shader documentation](https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/stereo-rendering/single-pass-instancing).

Scope render aspect and vertical FOV now follow the actual oval aperture, current eye relief and requested magnification. Existing public `BinocularDistance`, `BinocularRadius` and `BinocularFov(float)` remain compatible for callers; the new live surface does not use their old overlay dimensions. Additive pure helpers `ApertureFov(float,float,float)` and `CommonApertureRadius(float,float,float)` support source math checks. A conservative80mm IPD bound, or measured larger actual stereo eye separation, prevents displaying a scope window with no common central field. Off-center/near-clipped views also suppress the surface rather than silently leaving one eye usable.

## Mathematical scope and actual validation

For oval half-dimensions `a=.055`, `b=.025`, half-IPD `c=.040`, the closest common circular pupil margin is `b*sqrt(1-c*c/(a*a-b*b)) = .01443m`. At the automatic Aim rear surface's0.160m relief, that is a shared circular half-field of about5.15°. Both forward rays are inside the physical opening and map to UV(0.5,0.5). The full per-eye peripheral opening is asymmetric, as expected for a physical nearby eyecup; this is not a promise of identical rim silhouettes or measured headset comfort.

Completed source checks:

- Full owned Characters and Presentation source Roslyn preflight against retained actual Unity6000.3.23f1/Fusion2.1.3 QA references: both exit0, existing unrelated warnings only.
- Source-derived math tests:12032 cases covering avatar scales0.25/0.5/1/2, IPDs52/64/72/80mm, forward-ray reticle coincidence, a360-direction common field, rolled/off-center eye-span rejection, magnification/aspect relationships and annulus topology/winding. These are mathematical checks, not an executed Unity shader.
- Shader/source review: regular geometry queue, LEqual/depth writes, actual per-eye camera position, same angular UV for texture and reticle, existing stereo macros, no main camera pose/projection changes, no Core/input/physics changes.
- Independent read-only review found no source blocker. Native runtime evidence is still required; no Unity process was launched by this source author.

QA should first take one useful current-source right-rifle raised capture and inspect the actual rim framing, depth occlusion and angular reticle before broadening. Then reuse the focused actual Aim adapter for both dominance/modes, unchanged controls/raise/release and camera invariance. Add real stereo-eye observations for left/right ray intersection and equal angular reticle center; mono screenshots alone cannot prove binocular usability. Verify blaster/revolver fit, holster/role/camera cleanup and a nearby occluder. Desktop/shotgun gates must remain intact. Actual Quest stereo fusion, eye-box comfort, natural wrist movement and target-device performance remain unverified.

Do not reuse a legacy fixture assertion that requires the lens to be camera-parented at localZ2; that is precisely the removed rendering behavior. Keep the previously passed physical-raise evidence separate from acceptance of this new eyecup/shader source.
