# 03 - Cinematography and camera work for Deadly Jenga trailers

Researcher topic: camera moves, framing, lenses, motion quality, optics, destruction/fall
coverage, VR-to-flat presentation, and measurable framing checks an engine can compute.

Audience: an AI agent that drives Unity cameras (Cinemachine 3.x or hand-rolled) by code and
cannot watch the result. Everything below is written so it can be turned into parameters and
pass/fail checks.

Conventions used here
- "Source" tags point to the numbered list in section 6.
- **[Inference]** marks a number or rule I derived (math, or a reasoned extension of a source)
  rather than one a source states. Treat these as starting values to test.
- Screen coordinates: x and y run 0..1 from bottom-left. Thirds lines are at 0.333 and 0.667.
- FOV means Unity's vertical field of view unless stated. Unity's default is 60 degrees.
- "Subject" means the thing the shot is about (a survivor, the hand, the tower, the wave).

---

## 1. Rules

Each rule: what to do, why, source. Numbers are the defaults the agent should start from.

### A. Before rolling: capture discipline

1. **Never ship a raw, unsmoothed head-mounted (HMD) view. Film VR play from a smoothed
   third-person or spectator camera.** Viewers do not see the head's micro-motions in the
   headset, but on a flat screen they read as shake and can cause nausea; third-person
   footage carries action, scale and emotion better than first-person. If first-person is
   unavoidable, run it through a separately smoothed "HMD camera" (position and rotation
   smoothed independently). Sources: 2, 3, 5, 6, 7.
2. **Strip the frame: HUD, cursors, debug text, boundary/guardian lines, nameplates and
   object highlights off unless the shot is about that UI.** Clutter distracts from the one
   idea per shot. Sources: 1a, 2.
3. **One idea per shot, staged big.** Pick the most dramatic verb (block yanked out,
   survivor leaping a gap, wave hitting) and make it cross a large part of the frame. Small
   or subtle actions vanish in a fast-cut trailer; "clarity is king". Sources: 1c, 1d.
4. **Make every shot repeatable.** Script the action (seeded bots, timed hand pulls, saved
   camera paths, fixed timestep), so a bad take is re-rendered, not re-played. Lieu lists
   reset/rewind, spawn-on-cue, time freeze and camera position copy/paste as the tools that
   make good capture possible. Sources: 1b, 1e, 7.
5. **Capture at 60 fps or higher at a locked timestep, and at more pixels than you deliver
   (4K master for a 1080p/4K delivery, about 100 Mbps).** 60 fps reads closer to the
   headset experience and gives headroom for slow motion and stabilisation; reframing and
   stabilisation crop and resample the image. Sources: 5, 6, 9.
6. **Brighten for monitors and phones.** Headset-tuned exposure looks too dark on flat
   screens; raise exposure or grade up in post. Source: 5.

### B. Shot design and editing grammar

7. **Every shot has one camera behaviour from the move library (section 2) and a stated
   purpose (establish, follow, reveal, react, impact).** No "free drift". Random slow
   drifting with no motivation is the main symptom of the first attempt. Sources: 1b, 14.
8. **Sequence shot sizes: establish (extreme wide/wide), then medium, then close, and change
   size by at least one step between adjacent shots of the same subject.** Size contrast is
   how the edit signals change; a medium shot buffers between wide and close. Source: 16.
9. **When cutting between two shots of the same subject, move the camera at least 30
   degrees around the subject or change shot size clearly (prefer both).** Smaller changes
   read as a jump cut or a mistake. Sources: 14, 17, 18.
10. **Keep screen direction: define the action line (for example, survivor's run direction,
    or the line from hand to tower) and keep all cameras of one beat on one side of it.**
    Crossing it flips left/right and confuses who is moving where. If you must cross, show
    the camera crossing in one continuous move, or cut through a neutral head-on shot.
    Sources: 14, 17.
11. **Prefer motivated movement over cuts inside a beat, and plain cuts over flashy
    transitions between beats.** Nesky treats cuts as a last resort inside gameplay cameras
    because they break orientation; trailer editors use cuts freely but between clearly
    different setups (rules 8-9). Sources: 14, 17.

### C. Composition

12. **Place the subject's key point on a thirds intersection, not dead centre, unless the
    shot is a symmetric "hero" or tower-establishing shot.** For people the key point is the
    eyes/head; for the hand it is the fingertips doing the pulling; for the tower it is the
    active block or the top. Composition should be balanced, without a heavy empty side.
    Sources: 1d, 20, 21.
13. **Headroom: in medium and closer shots put the eyes on the upper thirds line
    (y about 0.62-0.70). In full and wide shots leave 5-15% of frame height above the
    head.** Too little headroom looks cramped; too much makes the subject sink. Sources: 20,
    21.
14. **Lead/look room: put 55-67% of frame width in front of a moving or facing subject.**
    A runner at x = 0.33 moving right; a survivor looking left sits at x = 0.67. Cinemachine
    expresses this as Screen Position and Lookahead Time. Sources: 10, 20, 21.
15. **Show faces/fronts: the subject's forward vector should point within about 60 degrees
    of the camera (three-quarter view or better) in any shot where the subject's reaction or
    action matters.** A survivor seen from behind reads as anonymous; the first attempt's
    "facing away" problem. Exceptions: over-the-shoulder shots that show what the subject
    sees, and "walking into the unknown" shots. Sources: 2, 13 (OTS for scale), inference
    for the 60-degree number.
16. **Keep the line of sight clear.** The subject must not be more than about 20% hidden by
    blocks or other geometry; thin foreground objects passing briefly are fine. Use a
    raycast check (section 3) and a Deoccluder-style correction. Sources: 12, 14.
17. **Layer depth: every wide and medium shot gets a foreground element (block edge, candy
    piece, another survivor, spray), the subject in the midground, and something far behind
    (sky, ocean horizon, the hand).** Layered foreground/background is how Godzilla (2014)
    built depth and scale. Sources: 13, 26.
18. **Horizon level and off-centre.** Keep roll within plus or minus 0.5 degrees unless the
    shot is a deliberate Dutch angle (then at least 10 degrees, so it reads as intended).
    Put the ocean horizon on a thirds line: low (y about 0.33) to emphasise the tower's
    height and sky, high (y about 0.67) to emphasise the ground/water rushing below.
    Sources: 20, 21; roll tolerance is inference.

### D. Lens and scale

19. **Choose focal length on purpose and log it. Physical-camera conversion (24 mm sensor
    height, Unity vertical FOV):** 14 mm = 81 deg, 18 mm = 67 deg, 24 mm = 53 deg,
    35 mm = 38 deg, 50 mm = 27 deg, 85 mm = 16 deg, 135 mm = 10 deg, 200 mm = 7 deg.
    Unity's default 60 deg is about a 21 mm lens. Formula: vFOV = 2 * atan(12 / f).
    Sources: 23 (Unity physical camera); numbers computed [Inference: math].
20. **Wide (vFOV 50-80 deg) close to the action for speed, chaos and "inside it" shots;
    normal (30-45 deg) for human-eye storytelling; long (8-20 deg) from far away for
    compression, to stack the hand, tower and wave together and make the background loom.**
    Long lenses compress space and let the camera stay far from a blast; wide lenses make
    near things move fast across frame. Sources: 13, 28, 25.
21. **To make something feel giant: (a) put a human-scale reference (a survivor) in the same
    frame, ideally near the lens or at the giant's base; (b) camera low, at survivor eye
    height, tilted up; (c) let the giant break the frame edge so it is too big to see whole;
    (d) move the giant slowly with heavy anticipation and follow-through; (e) add
    atmosphere (haze, spray, dust) so its far parts lose contrast; (f) show it in
    silhouette against bright sky.** Each item is from kaiju practice. Sources: 13, 24, 25,
    26, 27, 28, 29.
22. **Shoot the giant from where a person could stand.** Godzilla (2014) kept the camera at
    a person's eye-level rather than a floating overhead view, and ILM on Pacific Rim
    avoided "impossible" camera positions; a camera that floats freely at the giant's own
    scale makes it look like a normal-sized object. In Deadly Jenga terms: put the camera
    on a survivor's ledge, on the water, or on the beach, not hovering beside the hand.
    Sources: 13, 28.
23. **Avoid miniature cues unless you want the toy look: shallow depth of field on a wide
    downward view and high top-down angles both make a scene read as a tabletop model.**
    For the "giant" read use deep focus on wides and keep shallow focus for close shots.
    Sources: 30, 31.
24. **Time huge things slowly.** A giant robot's punch in Pacific Rim takes 1-2 s against
    about 0.5 s for a human punch; Kong/Godzilla motions are animated long and then shaped
    with slow lift-off and fast step-down. If the hand's pull animation is quick, consider
    0.5x-0.7x slow motion on the hand only, or prefer shots where the hand moves slowly.
    Sources: 25, 26, 29.

### E. Motion quality

25. **Every camera move eases in and out; no linear starts or stops.** Use ease-in-out
    (smoothstep or cubic) on position and rotation, or critically damped smoothing. Lieu
    asks for high interpolation/damping so moves build gradually from zero and back;
    "sudden, jerky camera movements will look like mistakes". Sources: 1b, 1e.
26. **Damping values mean time to settle.** In Cinemachine, damping time is roughly the
    seconds to remove 99% of the error (residual 1%). Start: follow damping 0.5-1.0 s
    position, 0.3-0.6 s rotation; heavy "crane" feel 1.5-3 s. Lower values feel
    responsive, higher feel heavy. Sources: 10, 11.
27. **Respect pan speed limits to avoid judder.** RED's rule: at 24 fps with a 180 deg
    shutter, a pan should take at least 7 s to cross one frame width. At 30 fps scale to
    about 5.6 s per frame width; at 60 fps about 2.8 s [Inference: proportional to frame
    rate]. Example: at vFOV 40 deg (hFOV about 66 deg at 16:9) that is under 9.4 deg/s at
    24 fps, under 12 deg/s at 30 fps. The limit concerns the background; tracking a
    subject so the subject stays still is fine. Swish/whip pans are exempt because they
    are meant to blur. Source: 19.
28. **Camera noise is rotational first, layered, and lens-scaled.** Cinemachine guidance:
    three layers per axis, low 0.1-0.5 Hz, mid 0.8-1.5 Hz, high 3-4 Hz; rotation noise is
    most convincing; wider lenses need larger amplitudes, telephoto smaller; nothing near
    or above the frame rate. Handheld energy in real cameras sits mostly at 1-15 Hz with
    peaks 3-9 Hz, larger amplitude at lower frequency. Starting amplitudes
    [Inference]: "steadicam" 0.1-0.2 deg low-layer; "handheld calm" 0.3-0.6 deg low +
    0.05-0.1 deg high; "handheld panic" 1-2 deg low + 0.2-0.4 deg high, at vFOV 40-60 deg;
    halve them at vFOV under 20 deg. Sources: 11, 32.
29. **Impact shake is rotation-only, scaled by trauma squared, and decays within
    0.3-1.5 s.** Eiserloh recommends rotational shake (not translation) in 3D because
    translation pushes the camera into walls; the common implementation stores trauma 0..1,
    shakes by trauma^2 times Perlin noise times a max angle, and decays trauma linearly
    (example 0.5 per second, so full trauma fades in 2 s). Starting max angle for a 3D
    trailer [Inference]: 1.5-3 deg for a block slam near camera, 3-5 deg for a tsunami
    hit; a 2D example uses 10 deg and calls it high. Delay shake by the travel time of the
    impact (Cinemachine's propagating impulse uses 343 m/s by default). Sources: 11, 33,
    34.
30. **Do not let the camera react to every jitter of the subject.** Use a dead zone
    (Cinemachine Position Composer: fraction of screen where the target can move without
    camera reaction, start 0.1-0.2 wide) and a soft zone; set hard limits so the subject
    never leaves frame. Lookahead 0.2-0.5 s shifts framing toward where a mover is going;
    reduce it if animation is noisy. Sources: 10, 35.

### F. Optics, exposure, frame rate

31. **Use a 180-degree shutter equivalent: exposure = half the frame interval (1/60 s at
    30 fps, 1/120 s at 60 fps).** Smaller angles look crisp but choppy (useful only for
    deliberate chaos); larger look smeared. Sources: 19, 36.
32. **Unity URP does not support Recorder accumulation motion blur (HDRP only).** Options:
    URP Motion Blur override in "Camera and Objects" mode (Intensity 0-1, Clamp 0-0.2,
    default 0.05), or render at a high frame rate and average sub-frames offline
    [Inference: render 240 fps, average 4 consecutive frames out of every 8 to get 30 fps
    with a 180 deg shutter; ffmpeg `tmix` then `select`/`fps`]. Never stack both. Sources:
    37, 38.
33. **Depth of field: deep focus on establishing and scale shots; shallow focus on close
    shots and reactions, with the focal plane on the subject's eyes or the hand's
    fingertips; rack focus only between two meaningful subjects.** Lieu lists DOF,
    aperture and focal length as part of the cinematic look. Sources: 1e, 7, 30.
34. **Slow motion: use whole-ratio speeds (1/2, 1/4, 1/8), use it on impacts and peaks of
    falls, and use it sparingly so it does not misrepresent gameplay.** Explosions and
    falls look bigger when slowed. Sources: 1b, 39.

### G. VR to flat screen

35. **Default VR coverage: third-person spectator cameras around the player avatar; medium
    and waist-up framing when lower-body IK is unreliable; wide shots only where full-body
    pose looks good; over-the-shoulder to show what the player sees.** Sources: 2, 3, 7.
36. **If any VR first-person shot is used: smooth heavily, exaggerate head turns, keep
    hands high in frame (the monitor view is narrower than the headset view), and slow
    hand actions so each registers.** Source: 7.

---

## 2. Move library

Durations assume a 30 fps delivery and a trailer cut of 1.5-4 s per shot; render each move
with 0.5-1 s of handle before and after (editors cut inside the move, not at its ends). All
speeds are starting values. Distances are in metres in Deadly Jenga world units, assuming a
survivor is roughly 1.7-1.8 m tall in-engine [assumption: check the real avatar height and
scale everything by it].

| Name | Purpose | When to use | Parameters (typical) | Easing | Common failure |
|---|---|---|---|---|---|
| Locked-off (static) | Let the action provide all motion; reads as objective, heavy | Big physics events, collapse wides, comedic beats, before a reveal | Zero motion; optional "breathing" noise 0.05-0.1 deg at 0.2 Hz [Inference]; hold 2-4 s | n/a | Nothing moves in frame, so it looks like a screenshot; or tiny drift that reads as error |
| Push-in (dolly in) | Build tension, focus attention, enter a character's moment | Survivor notices the hand; before a pull; reaction | Distance change 15-35% of start distance over 2-4 s; speed 0.3-1.5 m/s; FOV fixed | Ease-in-out cubic, or slow-in only if cut before stop | Constant speed, starting and stopping abruptly; pushing into an occluder |
| Pull-out reveal | Reveal context: "he is on a tower in the middle of an ocean" | After a close shot; ending beats | Start MCU/MS, end wide/EWS; distance x3-x10 over 3-6 s; add 10-30 deg of rise | Ease-in-out; ease-out longer than ease-in | Reveal too fast to read; reveal shows nothing new |
| Crane / boom up | Lift from human level to show height and the world | Tower establishing, after a survivor reaches the top, hand arrival | Rise 3-15 m over 3-6 s at 1-3 m/s, tilt down to keep subject; or boom down to settle onto subject | Ease-in-out; heavy damping 1.5-3 s | Camera rotates faster than it rises; horizon tilts |
| Tilt-up reveal | Make something huge by making the viewer look up | Hand looming above survivors; tower from base; wave rising | Camera low (0.5-1.7 m above survivor's surface), lens 24-35 mm (vFOV 38-53); tilt 30-70 deg over 2-4 s, at most about 15-20 deg/s | Ease-in-out; slow start (anticipation) | Starting already looking at the giant (no reveal); tilting so fast it judders |
| Orbit / arc | Show a subject's shape and 3D-ness; hero moment | Tower establishing (slow), survivor hero pose, hand hovering | Radius fixed; 15-45 deg of arc over 3-5 s (3-12 deg/s); keep elevation constant; screen position fixed | Ease-in-out on angular speed | Full constant-speed 360 orbit reads "game-y" (Lieu); orbit centre off the subject so it wobbles |
| Tracking / follow with lead room | Stay with a moving survivor; convey speed | Running across blocks, climbing, fleeing a wave | Side or 3/4 follow at 3-8 m; subject at x 0.33 (moving right) or 0.67; Lookahead 0.2-0.5 s; dead zone 0.1-0.2; damping 0.5-1 s | Critically damped follow | Camera locked rigidly to the character (copies every animation jitter); subject drifting to frame edge; lead room on the wrong side |
| Lateral truck / slide | Parallax that shows depth and scale; elegant establishing | Past foreground blocks toward the tower; opening shot | 0.3-1.5 m/s lateral, foreground object 1-4 m from camera passing through frame; 3-6 s | Ease-in-out | No foreground element, so no parallax; pan speed rule broken by fast foreground |
| Whip / swish pan | Energetic transition, sudden attention shift ("what was that?") | Cut from survivors to the hand or to the incoming wave | 90-180 deg in 0.2-0.4 s (6-12 frames at 30 fps); strong motion blur; cut hidden mid-blur | Ease-in hard, ease-out hard (S curve) | Too slow (reads as bad pan); no blur, so it strobes; landing off-subject |
| Handheld | Panic, documentary immediacy, "you are there" | Tsunami hitting, survivor falling, chaos beats; contrast with smooth shots | Layered rotational noise (rule 28), amplitude by intensity; position noise small (1-3 cm) | Noise is continuous; base move still eased | Used everywhere (no contrast); noise too fast (above about 6 Hz) looks digital |
| Dolly zoom (vertigo) | Unease, the world stretching; "the ground drops away" | Survivor at the edge looks down; the moment a supporting block is pulled | Keep subject width constant: distance d and FOV satisfy d * tan(FOV/2) = const; change FOV by 20-40 deg over 1.5-3 s; Cinemachine Follow Zoom (Width, Min/Max FOV) does this | Ease-in-out | Subject size pulses because distance and FOV are not linked; overused |
| Crash zoom | Comic or shock emphasis | Hand grabbing a block, "oh no" survivor | FOV narrows 2-4x in 0.15-0.3 s, then holds; optional small overshoot 3-5% and settle in 0.2 s | Fast ease-out, slight overshoot | Too slow (becomes a creep zoom); combined with camera movement and becomes mush |
| Drone flyover | Scope of the map: tower, island, ocean | Openers, map reveals, end card backgrounds | 5-15 m/s at 20-80 m from tower; gentle bank 5-15 deg in turns; path a smooth spline | Ease-in-out on spline speed; rotation damping 1-2 s | Flying too close and too fast; constant altitude with no reveal; banking by roll noise |
| POV (first person) | Put the viewer in a survivor's or the hand's eyes | Very brief: looking down from the top, the hand reaching toward camera | Smoothed position 0.2-0.4 s, rotation 0.3-0.6 s; vFOV 55-70 deg; hold 1-2 s | Smoothed | Raw HMD jitter; held too long; nausea |
| Over-the-shoulder (OTS) | Scale and stakes: survivor in foreground, giant beyond | Survivor watching the hand or the wave | Camera 0.5-1.5 m behind and beside shoulder; survivor fills 25-40% of frame on one third; giant on the other two thirds; lens 35-50 mm | Gentle push or locked | Shoulder blocks too much; giant too small to matter |
| "Falling with" | Ride a fall: speed, height, loss of control | Survivor knocked off the tower; block toppling | Camera matches subject's fall velocity minus 10-30% so subject drifts upward slowly in frame; or camera stays at fall start and tilts down; 1-2.5 s; add slight handheld | Camera accelerates with gravity (ease-in), not linear | Camera falls at constant speed while subject accelerates (subject leaves frame); camera passes through blocks |
| Rack focus | Switch attention without moving | Survivor in foreground to the hand behind, or reverse | Focus distance change over 0.5-1.5 s; aperture shallow enough that unfocused subject is clearly soft | Ease-in-out | Both subjects equally sharp (no effect) or focus "hunts" |
| Speed ramp (in edit or engine timescale) | Emphasise the instant of impact | Block hits survivor, wave lands, tower top falls | 1.0x to 0.25x over 3-6 frames, hold 0.5-1.5 s, ramp back | Ease on time scale | Ramping during camera noise makes noise look underwater |

Notes on parameters
- Push-in, crane and orbit ranges are synthesis from practitioner guidance (moves must start
  and stop gradually, slow moves read as cinematic, speed limits keep fast moves under
  control) rather than numbers any source prescribes [Inference]. Sources: 1b, 1e, 13, 19.
- Whip pan frame counts come from editing tutorials (7-10 frames at 24 fps, about 20 at
  60 fps). Source: 40.
- Lieu calls orbiting "game-y". Use small, eased arcs, not constant-speed full orbits.
  Source: 1b.

---

## 3. Framing metrics (computable per frame, aggregated per shot)

Compute per rendered frame, then report per shot as median, 10th and 90th percentile. A shot
passes when the median is in range and the 10th/90th percentiles are not more than one
"band" outside. Ranges are **[Inference]** calibrations built from the shot-size definitions,
thirds/headroom conventions and the occlusion thresholds used in camera-control research
(Sources 16, 20, 21, 22); tune them on reviewed shots.

### 3.1 Definitions

| Metric | How to compute |
|---|---|
| Subject height fraction `h` | Project the subject's renderer bounds (or 8 corners of a tight capsule) to the screen; `h` = (max y - min y) / screen height, clipped to the screen. For people use the whole body capsule. |
| Subject area fraction `a` | Projected bounding rectangle area / screen area; better: count subject pixels from an ID/stencil buffer. |
| Thirds error `e3` | Distance in normalised screen units from the subject key point (head for survivors, fingertips for the hand, active block for the tower) to the nearest of the 4 thirds intersections. 0 is perfect; 0.236 is the distance from a thirds point to the centre. |
| Headroom `hr` | 1 - (top of the subject's head in screen y). Only for people. |
| Eye line `ye` | Screen y of the eyes. |
| Lead room `lr` | For a subject moving or facing horizontally: fraction of frame width on the side it faces/moves (1 - x if moving right, x if left). |
| Facing angle `phi` | Angle between the subject's forward vector (flattened to horizontal) and the vector from subject to camera. 0 = facing camera, 90 = profile, 180 = back. |
| Occlusion `occ` | Cast rays from the camera to a 5x5 grid of points on the subject's bounds (or 25 sampled mesh points); `occ` = blocked rays / total, ignoring the subject's own colliders and layers marked transparent (spray, particles). Pixel-ID count is more accurate if available. |
| Frame clipping `clip` | Fraction of the subject's projected box outside the 90% action-safe rectangle. |
| Foreground present `fg` | True if non-subject geometry covers at least 3% of pixels at depth under 0.5x the subject distance (depth buffer or sparse raycasts). |
| Camera linear speed `v` | Metres/second from frame-to-frame position. |
| Camera angular speed `w` | Degrees/second of the forward vector. |
| Background screen speed `sb` | `w` / horizontal FOV, in frame widths per second (pans), or optical flow of the background if available. |
| Jerk `j` | Second difference of velocity per frame; flags robotic starts/stops. Measure peak jerk in the first and last 0.3 s of a move. |
| Horizon roll `r` | Angle between the camera's right vector and the world horizontal plane, in degrees. |
| Horizon height `yh` | Screen y where the ocean plane at far distance projects (if visible). |
| Subject screen speed `ss` | Movement of the subject key point in screen units per second. |
| Shake energy | RMS of the rotational noise component (deg) over the shot. |

### 3.2 Pass ranges by shot type

`h` is for a survivor (person) unless stated. For the hand or the tower, use the "giant"
rows.

| Shot type | `h` (subject height / frame height) | Key point placement | `phi` (deg) | `occ` max | Other |
|---|---|---|---|---|---|
| Extreme wide / establishing (tower, island) | Tower 0.5-0.9 of frame height; any survivor 0.02-0.08 (visible as a figure) | Tower centre within 0.1 of x = 0.5 (symmetric) or on a vertical third; `yh` 0.25-0.40 or 0.60-0.75 | n/a | Tower 0.1 | `fg` true preferred; `r` within 0.5; `w` under 6 deg/s |
| Wide (WS) | 0.20-0.45 | `e3` under 0.08 | 0-100 | 0.15 | `fg` true; `hr` 0.15-0.4 allowed (wide shots leave space) |
| Full shot (FS) | 0.70-0.90 | Head 0.05-0.15 below top edge | 0-80 | 0.10 | `clip` 0 (feet and head in frame) |
| Medium wide (knees up) | Visible body is about 70% of height; head height 0.12-0.16 | `ye` 0.62-0.72 | 0-70 | 0.10 | `lr` 0.55-0.70 if moving |
| Medium (waist up) | Head 0.16-0.22 | `ye` 0.62-0.70 | 0-60 | 0.05 | `lr` 0.55-0.70 |
| Medium close (chest up) | Head 0.22-0.32 | `ye` 0.62-0.70 | 0-50 | 0.05 | Shallow DOF allowed |
| Close-up (face) | Head 0.40-0.70 | `ye` 0.60-0.68 | 0-45 | 0.02 | Shallow DOF on eyes |
| Over-the-shoulder | Foreground shoulder/head 0.25-0.50 of frame height, on outer third, cropped by the edge | Target (hand/wave) on opposite third | Foreground subject 120-170 (seen from behind); target faces camera 0-60 | Target 0.15 | Foreground deliberately blurred if DOF on |
| Giant reveal (hand, tilt-up) | Hand 0.6-1.5 (allowed to exceed frame: `clip` 0.1-0.5 intended); survivor 0.03-0.15 in the same frame | Survivor on a lower third; hand on upper two thirds | Survivor 90-180 (looking up at it) is fine | Survivor 0.2; hand 0.3 | Camera height under 2x survivor height above survivor's surface; pitch up 15-60 deg |
| Tracking/follow | Per its size class above | `lr` 0.58-0.70; `ss` under 0.15 frame/s (subject stays put) | 0-110 | 0.15 (short spikes under 0.5 s ignored) | Background `sb` exempt (it is a follow) |
| Impact / collapse wide | Falling mass 0.3-0.8 | Impact point within 0.15 of a thirds point or centre | n/a | 0.1 on impact point | Locked or slow: `w` under 4 deg/s before impact; shake starts within 2 frames of impact |
| Fall ("falling with") | 0.15-0.45 | Subject y 0.35-0.65 throughout | any | 0.2 | `ss` under 0.4 frame/s; camera vertical acceleration 0.6-1.0 g |
| POV | n/a (no subject body) | Target of attention within 0.2 of centre | n/a | 0.2 | `w` under 30 deg/s; rotation smoothed (jerk check) |

Universal checks (all shot types)
- Roll `r` within plus or minus 0.5 deg, unless the shot is tagged Dutch (then 10-20 deg and
  constant).
- Pan speed: `sb` at most 0.18 frame widths/s at 30 fps for non-follow pans, at most
  0.36 at 60 fps [Inference from Source 19]; whip pans tagged and exempt.
- Smooth starts: peak jerk in the first and last 0.3 s of a move no more than 2x the
  median jerk of the move [Inference].
- No camera inside geometry: a sphere of radius 0.2-0.3 m at the camera overlaps nothing
  (Nesky's camera collision sphere; Cinemachine Deoccluder Camera Radius). Sources: 12, 14.
- Near clip: no subject pixels clipped by the near plane.
- Subject in frame: `clip` at most 0.05 except shots tagged as "breaks frame".
- Shake RMS under 0.3 deg outside impact windows, unless tagged handheld.
- Adjacent-shot checks (edit level): angle between consecutive camera directions on the same
  subject at least 30 deg OR `h` ratio at least 1.6x; both cameras on the same side of the
  action line (sign of the cross product of the line direction and camera offset matches).
  Sources: 14, 17, 18.

---

## 4. Scale and destruction recipes

Shared principles (Sources 13, 24-29, 30, 31): human reference in frame; camera where a person
could be; low angle looking up for "giant", high angle looking down for "vulnerable"; slow
apparent motion of huge things; atmosphere and silhouette; partial framing; deep focus on wides.

### 4.1 The giant hand

1. **Arrival reveal (tilt-up).** Camera on a survivor's ledge at 1.0-1.6 m above the
   surface, 2-4 m behind and to the side of one or two survivors, lens 24-28 mm
   (vFOV 46-53). Start with the survivors in the lower third and sky above; the hand's
   shadow or the light change arrives first, then tilt up 30-50 deg over 2.5-3.5 s as the
   hand descends into frame. The hand breaks the top edge. Survivors turn toward it (back to
   camera is fine here). Sources: 13 (tilt-up from humans, eye level), 27 (delay the reveal).
2. **Silhouette.** Put the camera so the hand is between camera and sun or a bright sky;
   reduce fill so the hand reads as a dark shape with a bright rim. Godzilla was frequently
   played in silhouette with backlight and smoke. Source: 13.
3. **Long-lens compression for menace.** From 40-100 m away on the water, lens 135-200 mm
   (vFOV 7-10), frame the tower top with the hand above it; compression stacks them so
   the hand looms. Very slow push or locked-off. Sources: 13, 28 [Inference for distances].
4. **The pull (close-medium).** Camera at block height, 1-3 m from the block being pulled,
   lens 35 mm. Fingertips on the upper-right thirds point, the block in the centre, a
   survivor on the left third reacting (front visible, `phi` under 60). Slow push-in
   during the grab (2-3 s), then hold still for the yank; small impulse shake on release
   (1-2 deg max, decay 0.4 s). Sources: 1c, 10, 33.
5. **Timing.** If the hand animation is fast, slow the hand shot to 0.5-0.7x or choose
   angles where its motion crosses the frame slowly (moving toward or away from camera).
   Keep survivors and particles at real speed in that shot if possible (only the giant is
   slowed). Sources: 24, 25, 26.

### 4.2 The tall tower

1. **Base-up establishing.** Camera on the beach or water at 0.5-1.7 m, 10-30 m from the
   base, lens 18-24 mm (vFOV 53-67), pitch up 20-35 deg. Converging verticals emphasise
   height. A survivor or two at the base for scale (`h` 0.03-0.08). Horizon low
   (y about 0.2-0.3).
2. **Crane up the face.** Rise along the tower face at 1-3 m/s, 4-8 m out, keeping the
   climbing survivor on a lower third; ease in over 1 s, ease out over 1.5 s. Foreground
   blocks pass at the frame edge for parallax.
3. **Top-down vulnerability.** From just above a survivor at the top, look down past them to
   the water far below; lens 24-35 mm; survivor on a lower third, the drop filling the rest.
   This is the "small and vulnerable" counterpart to rule 21. Keep focus deep; a shallow-
   focus downward view risks the miniature look. Sources: 30, 31.
4. **Atmosphere.** Add light haze/fog so the far ocean and far side of the island lose
   10-30% contrast and drift toward the sky colour; the tower top in the far wide should be
   slightly lower contrast than the base [Inference for percentages]. Source: 41.
5. **Detail density.** Visible small details (other survivors, candy studs, seams) on the
   tower surface tell the eye it is big; Death Star-style "greebles" are a standard scale
   cue. Source: 25.

### 4.3 A collapse (blocks pulled, tower section falls)

1. **Anticipation framing.** Lock or nearly lock the camera 0.5-1.0 s before the collapse
   with the weak point on a thirds point and survivors on it. Viewers must see what is
   about to fail. Sources: 1d (timing: slow setup, fast payoff), 26 (anticipation for big
   moves).
2. **Coverage (re-render the same seeded event several times).**
   - A: locked-off wide, long lens from 40-80 m (vFOV 10-20) showing the whole section and
     the water. This is the "truth" shot and the main slow-motion source.
   - B: low wide (vFOV 55-70) at the base or on a lower ledge, looking up as debris falls
     toward camera; impulse shake on landing.
   - C: survivor-level medium, survivor reacting, debris passing between camera and
     subject (the foreground element).
   - D: "follow the debris": camera tilts down with a falling block at the block's
     accelerating speed, ending on the splash.
   Multiple simultaneous angles on one take is standard practice for one-shot destruction
   (The Impossible used 10 cameras on its single tank wave). Sources: 39, 42.
3. **Slow motion on the peak.** Ramp to 0.25-0.5x for 0.5-1.5 s around the moment the
   section separates or lands; return to real time before the cut. Source: 1b, 39.
4. **Shake timing.** Start shake within 0-2 frames of the impact at the camera's location
   (delay by distance / propagation speed if using a propagating impulse); decay 0.4-1.0 s.
   No shake before the impact. Sources: 11, 33.
5. **Scale-correct fall speed.** If the world's apparent scale is bigger than its physics
   scale, falls look too quick ("toy"). Miniature practice runs the camera at
   24 x sqrt(scale factor) fps, i.e. slow motion by sqrt(scale factor). In-engine
   equivalent: playback rate = 1 / sqrt(apparent size / simulated size). If physics already
   runs at the size the viewer should believe, use real time. Sources: 43 (practitioner
   formula); engine translation is [Inference].

### 4.4 A survivor fall

1. **Readable cause.** Show the push/pull that causes the fall in the previous shot (or the
   first 0.5 s of this one).
2. **Option A: stay and watch.** Camera remains at the ledge, tilts down to follow the
   falling survivor; the survivor shrinks against the distance and the water. This sells
   height. Long free fall times sell height: 10 m takes 1.43 s, 20 m 2.0 s, 40 m 2.9 s
   (t = sqrt(2h/g)).
3. **Option B: fall with.** Camera starts 2-4 m beside and slightly below the survivor,
   accelerates down with gravity at 80-95% of the survivor's acceleration so the survivor
   drifts slowly upward in frame, tower face rushing by in the background (parallax gives
   speed). Light handheld noise. 1-2 s. Cut before the splash or cut to a wide splash.
4. **Option C: from below.** Camera at water level looking up, survivor falling toward the
   lens, lens 24 mm; pairs with a splash and water-on-lens moment.
5. Avoid: camera at constant speed (survivor leaves frame), passing through blocks
   (Deoccluder on), nauseating spin (keep roll fixed).

### 4.5 A tsunami wave

1. **Horizon threat (long lens).** From the tower top, lens 85-135 mm (vFOV 10-16) at the
   horizon: the wave as a thin line rising; survivors' heads or shoulders in soft
   foreground (OTS). Hold or very slow push 2-4 s.
2. **Wall of water (low wide).** Camera at or just above water level on the island side,
   lens 18-24 mm, pitch up 10-25 deg; wave fills the upper two thirds; tower and survivors
   in the lower third for scale. Slow push toward the wave for tension or a slow pull-back
   as the wave advances.
3. **Impact.** Locked-off wide from the side (perpendicular to the wave direction, which also
   fixes screen direction: the wave always travels the same way across screen in every shot
   of the sequence), then a handheld survivor-level shot with high shake (3-5 deg max,
   1-1.5 s decay) as water hits.
4. **Overhead aftermath.** A high crane/drone shot looking down at what is left of the tower
   and the scattered survivors; The Impossible used overhead shots to convey wreckage after
   its wave. Source: 42.
5. **Atmosphere.** Spray and mist in front of the camera for depth; let spray partially veil
   far tower parts (contrast loss = distance). Sources: 28, 41.

---

## 5. Mistakes (symptom, cause, fix)

| Symptom | Cause | Fix |
|---|---|---|
| Shot feels like nothing is happening | Static or slowly drifting camera with no motivated move and a small subject | Pick a library move with a purpose; subject `h` at least the range for its shot type; one clear action crossing the frame (Sources 1c, 1d) |
| Subject tiny or lost | Camera too far, FOV too wide, no shot-size plan | Set the shot size first, then solve distance from FOV: d = (subject height / `h`) / (2 tan(vFOV/2)) [Inference: math] |
| Subject facing away | Camera placed by path, not by subject orientation | Place the camera relative to the subject's forward vector; check `phi` under 60 for reaction shots |
| Blocks in front of the subject | No occlusion check in a cluttered tower | Raycast grid occlusion metric; Deoccluder with Preserve Camera Height or Distance; let thin obstacles pass briefly (Sources 12, 14) |
| Robotic motion | Linear interpolation; instant start/stop | Ease-in-out curves; damping 0.5-3 s; jerk check at move ends (Sources 1b, 1e, 10) |
| Judder / stutter on pans | Pan too fast for frame rate; no motion blur; variable frame time | Pan speed limit (rule 27); 180-degree shutter equivalent; fixed-timestep rendering (Sources 19, 36) |
| Everything looks like a toy | Shallow DOF on wide views; high top-down camera; fast falls | Deep focus on wides, low camera, scale-correct slow motion (Sources 30, 31, 43) |
| Giant hand feels normal-sized | Camera floats at the hand's own height; no human reference; hand moves fast | Camera at survivor eye level, tilt up, survivors in frame, slow hand, silhouette (Sources 13, 25, 28) |
| Nauseating footage | Raw HMD view; high-frequency noise; roll | Third-person cameras; smoothing; rotation noise under 4 Hz; roll locked (Sources 2, 6, 7, 11) |
| Shake looks fake or hides the action | Translation shake; constant shake; too large on a long lens | Rotation-only trauma^2 shake with decay; scale amplitude with FOV; only on impacts (Sources 11, 33) |
| Cut feels like a glitch | Under 30 deg angle change and same size; crossed the action line | Adjacent-shot checks (section 3.2) (Sources 14, 17, 18) |
| Cluttered frame | HUD, guardian lines, debug text, too many simultaneous events | Debug toggles; one idea per shot (Sources 1a, 1d) |
| Camera clips into blocks / near-plane cuts the character | No camera collider | Camera sphere radius 0.2-0.3 m; Deoccluder; keep subject collider larger than near-plane distance (Sources 12, 14) |
| Flat, depthless images | No foreground, no atmosphere, subject against a busy same-tone background | Foreground element; haze; rim/backlight; silhouette against sky (Sources 13, 41) |
| Dark footage on phones | Headset-tuned exposure | Raise exposure/grade for flat screens (Source 5) |
| Shots unrepeatable; best take had a glitch | Live play capture without determinism | Seeded, scripted events, saved camera paths, rewind/reset tools (Sources 1b, 1e) |
| Slow motion makes the game look misleading | Overuse | Only on peaks, whole ratios, short (Source 1b) |
| Over-designed orbit | Constant-speed 360 around a subject | 15-45 deg eased arc; or replace with a truck past a foreground element (Source 1b) |

---

## 6. Sources

Practitioners: game trailers and capture
- 1a. Derek Lieu, "The Best Debug Tools For Game Capture" (2018). https://www.derek-lieu.com/blog/2018/5/1/the-best-debug-tools-for-game-trailer-capture
- 1b. Derek Lieu, "How to Make a Freecam or Marketing Camera for Game Trailers" (2022). https://www.derek-lieu.com/blog/2022/11/19/how-to-make-a-freecam-or-marketing-camera-for-game-trailers
- 1c. Derek Lieu, "Capture For the Backseats" (2019). https://www.derek-lieu.com/blog/2019/11/25/capture-for-the-backseats
- 1d. Derek Lieu, "How to Select Good Game Capture" (2020). https://www.derek-lieu.com/blog/2020/4/13/how-to-select-good-game-capture
- 1e. Derek Lieu, "What Makes Game Capture So Much Work?" (2023). https://www.derek-lieu.com/blog/2023/4/30/what-makes-game-capture-so-much-work
- 1f. Derek Lieu, GDC 2018 "Capturing Great Footage for Game Trailers" (listing only; slides not read). https://www.gdcvault.com/play/1025079
- 1g. PC Gamer, "The art of making effective game trailers" (Lieu interview; controller and dead zones for smooth pans). https://www.pcgamer.com/the-art-of-making-effective-game-trailers
- 2. Kert Gartner, "Virtual Cinematography for VR Trailers" (repost). https://www.gamedeveloper.com/business/virtual-cinematography-for-vr-trailers
- 3. Road to VR, Kert Gartner's handheld virtual camera rig. https://roadtovr.com/real-virtual-camera-htc-vive-vr-cinematography-kert-gartner/ (via search summary)
- 4. GameAnim / GamesBeat coverage of Gartner's mixed-reality trailers (Fantastic Contraption, Space Pirate Trainer). https://gamesbeat.com/heres-how-hard-it-is-to-make-a-mixed-reality-trailer-for-htc-vive-vr-games/
- 5. Road to VR, "VR Design Unpacked: The Formula for Great VR Game Trailers" (smoothing, 60 fps, 4K, 100 Mbps, brighten). https://roadtovr.com/inside-xr-design-make-great-vr-trailer-without-mixed-reality-capture/2/
- 6. Meta developer blog, "Bringing a Trailer to Life for VR with Defense Grid 2" (60 fps, jib rig, capture above delivery). https://developers.meta.com/horizon/blog/guest-blog-bringing-a-trailer-to-life-for-vr-with-defense-grid-2/
- 7. Derek Lieu, "Making a Trailer for Cosmic Trip - Tips For 1st Person VR Game Capture" (2016) and Luna trailer post (2017). https://www.derek-lieu.com/blog/2016/06/07/making-a-trailer-for-cosmic-trip-tips-for-1st-person-vr-capture ; https://derek-lieu-yrd2.squarespace.com/blog/2017/11/22/luna
- 8. Game Developer, "The VR industry can learn from filmmaking to portray immersion". https://www.gamedeveloper.com/extended-reality/the-vr-industry-can-learn-from-filmmaking-to-portray-immersion
- 9. Road to VR (as 5), quality targets.

Virtual cameras (Unity/Cinemachine, game camera design)
- 10. Unity, Cinemachine 3.1 Position Composer (screen position, dead zone, hard limits, damping, lookahead). https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachinePositionComposer.html
- 11. Unity, Cinemachine noise profiles and Basic Multi Channel Perlin (frequency bands, rotation first, lens-scaled amplitude); Damper API (damp time to 1% residual); Impulse Source (shapes, propagation 343 m/s). https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineNoiseProfiles.html ; https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/api/Unity.Cinemachine.Damper.html ; https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineImpulseSource.html
- 12. Unity, Cinemachine Deoccluder. https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineDeoccluder.html
- 14. John Nesky (thatgamecompany), GDC 2014 "50 Camera Mistakes" (translated summary, items 1-25). https://habr.com/en/post/272401 ; GDC listing https://gdcvault.com/play/1020460/50-Camera
- 15. Unity, Cinemachine Spline Dolly and Follow Zoom. https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineSplineDolly.html ; https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineFollowZoom.html
- 35. 80.lv, "Procedural Camera Systems in Homeworld: Shipbreakers" (Myhill/Orban: composer, dead zones, decoupled operator, noise matched from film). https://80.lv/articles/procedural-camera-systems-in-homeworld-shipbreakers
- 33. Squirrel Eiserloh, GDC 2016 "Juicing Your Cameras With Math" (recap: rotational shake only in 3D). https://gdcvault.com/play/1023146/Math-for-Game-Programmers-Juicing ; recap https://blogs.jccc.edu/dpatter/2016/03/15
- 34. Bevy engine screen-shake example (trauma, exponent 2, decay 0.5/s, 10 deg max in 2D). https://bevy.org/examples/camera/2d-screen-shake/
- 23. Unity, Physical Cameras (focal length, sensor size, gate fit). https://docs.unity3d.com/Manual/PhysicalCameras-introduction.html
- 37. Unity Recorder, accumulation motion blur (HDRP only). https://docs.unity3d.com/Packages/com.unity.recorder@5.1/manual/RecordingAccumulation.html
- 38. Unity URP Motion Blur override. https://docs.unity.com/en-us/engine/6000.0/manual/post-processing-and-full-screen-effects/urp/effect-list/motion-blur
- 22. Ranon, Christie, Urli et al., camera-control composition and visibility measurement (size and occlusion satisfaction thresholds, rule-of-thirds scoring). https://www.irisa.fr/mimetic/GENS/mchristi/Publications/2011/ACSLO11/Advanced-Composition-ACLO-SG11.pdf (via search summary)

Cinematography fundamentals
- 16. StudioBinder, shot sizes guide. https://www.studiobinder.com/blog/types-of-camera-shots-sizes-in-film/
- 17. No Film School, "The 30-Degree Rule". https://nofilmschool.com/30-degree-rule-dps-editors
- 18. Wikipedia, 30-degree rule (20 mm / 30 degree variant). https://en.wikipedia.org/wiki/30-degree_rule
- 19. RED Digital Cinema, "Panning Speed Best Practices" (7 s per frame width at 24 fps, 180 deg). https://www.reddigitalcinema.com/red-101/camera-panning-speed
- 20. Wikipedia, Headroom (photographic framing). https://en.wikipedia.org/wiki/Headroom_(photographic_framing)
- 21. Videomaker, "Framing Good Shots" / ASU video basics (eyes on upper third, lead room). https://www.videomaker.com/article/f5/9231-framing-good-shots
- 36. StudioBinder, "What is the 180 degree shutter rule". https://www.studiobinder.com/blog/what-is-the-180-degree-shutter-rule/
- 40. No Film School and editing tutorials on whip pans (frame counts). https://nofilmschool.com/how-to-do-a-whip-pan
- 44. StudioBinder, dolly zoom. https://www.studiobinder.com/blog/dolly-zoom/
- 41. Wikipedia, Aerial perspective (contrast, saturation, blue shift with distance). https://en.wikipedia.org/wiki/Aerial_perspective

Scale: kaiju, disaster, miniatures
- 13. American Cinematographer, "King of the Monsters" (Godzilla 2014, Seamus McGarvey). https://theasc.com/articles/king-of-the-monsters-2014 ; plus Inverse retrospective and RedShark coverage via search
- 24. Befores & Afters, Godzilla x Kong walk-speed and scale animation. https://beforesandafters.com/2024/05/01/for-godzillas-walk-we-put-a-speedometer-on-his-feet-just-to-see-how-fast-they-go/
- 25. PremiumBeat, "Creating the Illusion of Scale" (low angle, reference, slow motion, partial framing, haze; Pacific Rim punch 1-2 s). https://www.premiumbeat.com/blog/cinematography-tip-creating-the-illusion-of-scale/
- 26. VFX Voice, "Weta Digital gives weight to the giant monsters of Rampage" (build-up, gravity reference). https://www.vfxvoice.com/weta-digital-gives-weight-to-the-giant-monsters-of-rampage/
- 27. No Film School, "You Can Sell Kaiju Scale Without a Blockbuster Budget" (darkness, delayed reveal, environment reactions, small in frame next to humans). https://nofilmschool.com/indie-monster-movie-vfx-scale
- 28. ILM / Animation Magazine / CGW coverage of Pacific Rim (camera position and lens sell scale; avoid impossible camera angles; disturbed atmosphere). https://www.ilm.com/vfx/pacific-rim ; https://www.cgw.com/Publications/CGW/2013/Volume-36-Issue-5-July-August-2013-/Go-Large.aspx (via search summary)
- 29. Kong: Skull Island coverage (pan speed and lens planned for Kong's scale; 35 mm checks at 300 ft). https://www.motionpictures.org/2017/03/how-kong-skull-island-cinematographer-channeled-apocalypse-now/ (via search summary)
- 30. Wikipedia, Miniature faking / tilt-shift (shallow DOF and high angle read as miniature). https://en.wikipedia.org/wiki/Miniature_faking
- 31. Held, Cooper, O'Brien, Banks (2010), "Using Blur to Affect Perceived Distance and Size", ACM TOG (cited via search summary; full text blocked). https://pmc.ncbi.nlm.nih.gov/articles/PMC3088122
- 39. Practitioner accounts on filming explosions/demolitions (long lens from distance, multiple cameras, overcranking). https://cinematography.net/edited-pages/C4ExplosionsCapturedOnHDFilm.htm ; https://www.premiumbeat.com/blog/capture-stunts-safely-with-long-lens
- 42. Coverage of The Impossible's tsunami (10 cameras on a single take, overhead aftermath). https://www.fxguide.com/?p=38748 ; https://www.timesfreepress.com/news/2013/jan/11/wizardry-behind-impossibles-terrifying-wave/ (via search summary)
- 43. cinematography.net, "Miniature Formula" (24 x sqrt(scale) fps; test). https://www.cinematography.net/edited-pages/MiniatureFormula.htm
- 32. Camera image-stabilisation patents (hand-shake energy 1-15 Hz, peaks 3-9 Hz; body sway under 3 Hz). Example: https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/6233009 (via search summary)

---

## 7. Uncertain or disputed

- **Pan speed at 30/60 fps.** RED's 7-second rule is stated for 24 fps with a 180 deg
  shutter. Scaling it linearly with frame rate (5.6 s at 30, 2.8 s at 60) is my inference;
  RED only says higher frame rates reduce judder. Also, most viewers see YouTube at the
  upload rate, but platforms may serve lower rates. Test on a real upload.
- **Shake and handheld amplitudes in degrees.** No authoritative source gives degree values
  for "realistic handheld" or impact shake in 3D. My ranges are inferences from Cinemachine's
  frequency guidance, the 2D 10-degree example and stabiliser frequency data. Calibrate by
  eye on reviewed clips.
- **Shot size to subject-fraction mapping.** Shot sizes are defined by which body part fills
  the frame, not by a number. The `h` ranges are my conversion and should be tuned.
- **Shot size change vs 30-degree rule.** Sources disagree whether a clear size change alone
  avoids a jump cut. Safer: change both angle and size.
- **Depth of field and scale.** Shallow DOF is a known miniature cue (tilt-shift), yet
  Gareth Edwards' low-budget monster film (before Godzilla 2014) used shallow DOF to blur backgrounds (Source 27), and close-ups
  conventionally use shallow focus. The rule here (deep on wides, shallow on close shots)
  is a reconciliation, not a cited rule. Deadly Jenga is candy/toy styled: a mild
  miniature look may be on-brand for some shots; this is a creative decision.
- **Orbiting.** Lieu calls orbiting "game-y"; it is still a staple in many trailers. Treat
  small eased arcs as safe, constant 360s as risky.
- **Slow motion honesty.** Lieu avoids slow motion as a style because it can misrepresent the
  game; film practice uses it freely for impact. Use sparingly and on clearly cinematic
  beats.
- **Motion blur in URP.** URP motion blur parameters do not map to a shutter angle; whether
  "Camera and Objects" mode looks right on fast physics debris is unverified. The
  high-frame-rate render plus frame averaging approach is my inference (standard in offline
  rendering) and costs render time.
- **Miniature formula in an engine.** The sqrt(scale) rule is for real miniatures under real
  gravity, from practitioner forums, with "test" as the main advice. Whether Deadly Jenga's
  physics scale differs from its apparent scale must be checked in-engine (block size,
  gravity value, survivor height).
- **Kert Gartner's own pages** (kertgartner.com) were not reachable; his points come from a
  repost and press coverage. Lieu's GDC talk slides and Nesky items 26-50 were not read.
- **Defaults not found:** Cinemachine Deoccluder and Impulse numeric defaults, and any
  published numeric "facing angle" threshold. The 60-degree facing rule is my inference.
