# Giant-hand observer camera

The camera follows the visible physics palm in translation, using `ThirdPersonVRFollow.positionTarget`, while the original flight target supplies heading. A blocked or slower physical hand can diverge from the flight motor; following only that motor made the observer appear detached. Wrist twist never feeds camera rotation.

Castle hands enable compact framing: 3.5 metres behind the wrist and 1.25 metres above it, with at most 1.2 metres of follow lag. Hand meshes, collisions, forces and tracking are unchanged. The standalone HandDemo retains its authored distance/height unless compact mode is enabled.

Height refers to the observer's eye, not just its XR-origin root. Desktop following compensates the camera-floor parent as well, eliminating an accidental extra 1.6 metres of elevation.

In VR, the parent observer rig moves while tracked camera-local position/rotation remain untouched. A captured head baseline accounts for the camera floor offset at entry/recentering, preserving subsequent natural leaning. The horizon remains upright. Default VR heading changes are 30-degree snaps instead of continuous automatic camera yaw; desktop following remains smooth and mouse pitch remains independent. SnapToTarget explicitly recenters the observer; it does not write the tracked head pose.

Public tuning fields: compactHandView, handViewDistance, handViewHeight, maximumFollowLag, vrSnapYaw, vrYawStep, positionSmoothTime. `target` controls heading and fallback position; `positionTarget` overrides translation only. CandyFlyingHandBridge assigns the currently spawned physical palm so later hand rebuilds inherit the camera behavior.

The synthetic VR checks exercise tracked-local pose preservation, natural lean, snap heading, stable horizon and bounded following when the flight target is far away. They do not substitute for headset comfort testing. Regression reports: ArtDirection/SeamCameraChecks.txt. Run by writing `seams` to ArtDirection/SlopeChecks.request after compilation in Edit Mode.

Design reference: Meta locomotion best practices, https://developers.meta.com/vr/design/locomotion-best-practices/ and Meta comfort guidance, https://github.com/meta-quest/agentic-tools/blob/main/skills/hz-immersive-designer/references/comfort-guidelines.md . Compact distances and physical-palm anchoring are this project's design choices, not universal comfort guarantees.
