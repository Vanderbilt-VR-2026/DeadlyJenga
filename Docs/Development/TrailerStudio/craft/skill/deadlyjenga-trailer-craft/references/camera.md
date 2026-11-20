# Camera at Deadly Jenga scale

Open when placing cameras (stage 3) and when reading scout numbers (stage 5). Main sources:
report 03 (Lieu on capture and freecams, Nesky's camera mistakes, Gartner on VR, Cinemachine docs,
RED on pan speed, kaiju and disaster practice), rescaled here from its assumed 1.7 m person to the
real 0.94 m survivor. Measured shares are from report 06. Numbers with no tag are inferred starting
values: tune them on reviewed shots.

## 1. Sizes and lenses

| Thing | Size | Notes |
|-------|------|-------|
| Survivor | 0.94 m tall, eyes about 0.85 m | 0.55 x the 1.7 m person report 03 assumed |
| Hand | about 4 m across; assume about 3 m of visible height side-on | measure in the scene; every hand distance below depends on it |
| Block | 6 x 2 x 2 m | twice a survivor's height; three times longer than the hand is tall |
| Tower (attempt 1 map) | 56 m on a 25 m castle | crown section for framing: about 14 m |

Physical-camera lenses (24 mm sensor height): vertical FOV = 2 atan(12 / f). Frame height at
distance d = d x `k`, where `k = sensor height / focal length`. Every table here assumes a 24 mm
sensor. **Trailer Studio's camera tracks default to an 18.66 mm sensor** (Super 35): set
`sensorHeight = 24` on each track (the plan header says `camera_sensor_mm: 24`), or recompute with
`k = 18.66 / f` (35 mm then gives k = 0.533, not 0.686).

| Focal | vFOV | k = 2 tan(vFOV/2) |
|-------|------|-------------------|
| 18 mm | 67.4° | 1.333 |
| 24 mm | 53.1° | 1.000 |
| 35 mm | 37.8° | 0.686 |
| 50 mm | 27.0° | 0.480 |
| 85 mm | 16.1° | 0.282 |
| 135 mm | 10.2° | 0.178 |
| 200 mm | 6.9° | 0.120 |

Distance for a wanted frame height `Hf`: `d = Hf / k`. For a subject of height H at fraction h:
`Hf = H / h`.

### Survivor shot sizes (distance to the survivor, metres)

| Size | h (survivor height / frame) | Frame height Hf | 24 mm | 35 mm | 50 mm | 85 mm |
|------|-----------------------------|-----------------|-------|-------|-------|-------|
| EWS | ≤ 0.08 | ≥ 11.8 m | ≥ 11.8 | ≥ 17 | ≥ 25 | ≥ 42 |
| WS | 0.20-0.45 | 2.1-4.7 m | 2.1-4.7 | 3.0-6.9 | 4.4-9.8 | - |
| FS | 0.70-0.90 | 1.04-1.34 m | 1.0-1.3 | 1.5-2.0 | 2.2-2.8 | - |
| MS (waist up) | - | 0.50-0.60 m | - | 0.73-0.87 | 1.04-1.25 | 1.8-2.1 |
| MCU (chest up) | - | 0.35-0.48 m | - | - | 0.73-1.0 | 1.24-1.7 |
| CU (face) | - | 0.18-0.28 m | - | - | 0.38-0.58 | 0.64-0.99 |

Close survivor shots put the camera under a metre away. Set the near clip plane to 0.05 m or less
and the camera collision sphere to 0.10-0.15 m (report 03's 0.2-0.3 m, scaled). Prefer 50-85 mm for
MS and closer so the camera does not sit inside the tower's blocks.

### Hand, crown and tower

| Framing | Hf | Distance |
|---------|----|----------|
| Hand whole, h 0.5-0.75 (FS) | 4-6 m | 4-6 m at 24 mm; 6-9 m at 35 mm |
| Hand breaking the frame, h 0.8-1.2 | 2.5-3.8 m | 2.5-3.8 m at 24 mm |
| Crown (top 9-12 m of the tower) with room around it | 14 m | 80 m at 135 mm; 50 m at 85 mm; 20 m at 35 mm |
| Whole tower, h 0.6-0.9 | 62-93 m | 47-70 m at 18 mm; 62-93 m at 24 mm |

In a whole-tower frame the hand is about 4-6 % of frame height and survivors about 1 %: neither
reads. Use whole-tower frames only for establishing scale and the ocean, never for the hand.

The crown row frames a topple of the top 9-12 m only. An impact lower down (a wave striking
mid-tower, a pull near the base) needs a frame derived for that height: in skill test 1 a copied
80 m / 135 mm crown frame could not contain the wave its plan described.

### 1b. The framing check (`fit`, hard lint L38)

Write one line per shot, derived for that shot:
1. `Hf = distance x 24 / focal` at the subject's distance (16:9 frame width = 1.78 x Hf).
2. The subject's fraction `h = subject height / Hf`; it must match the declared `size` (tables above;
   POV and non-survivor subjects use the subject-fraction rule in `plan-schema-and-lints.md`).
3. Every other thing the shot claims at the same time, at its own distance: the survivor for scale
   (and the ≥ 0.6 x hand-distance rule), the impact point, the block's travel (≥ 15 % of frame width),
   both threats if two are claimed, the face if it must read.
4. Where the action ends: if the payoff exits frame (the pilot's block and hand left toward the lens),
   move the camera or choose a frame that holds the end point.
5. Nothing the game cannot do: one giant hand, not two.

Example: "Hf = 6.5 x 24/28 = 5.6 m: hand 0.54. Survivor at 4.5 m (≥ 3.9): Hf 3.9 m, h 0.24 (WS).
Slide 2 m of a 9.9 m wide frame (20 %)." If the arithmetic fails, change the distance or lens, not
the claim. Reaching an example's numbers is fine only if the derivation for your shot gets there.

### 1c. POV shots

Size by what fills the frame, using the subject-fraction rule (a survivor seen tiny from the hand's
view is WS or EWS by its own height fraction). `side` is null (no yaw), so the jump-cut check skips
POV pairs. Hold 1-2 s by default; inside a barrage a POV may be 0.5-1.0 s if the same POV was held
≥ 1 s earlier. A survivor POV sits at eye height 0.85 m; where the hand player's eye sits relative to
the hand is unknown: verify it at scout.

## 2. Scale rules for this game

1. **People set the size.** The hand is only about 4 times a survivor. It looks giant only when a
   survivor at a similar depth is in the same frame. On-screen ratio = (4 m / 0.94 m) x
   (survivor distance / hand distance). Keep the survivor's camera distance ≥ 0.6 x the hand's,
   which keeps the ratio ≥ 2.5. A survivor much nearer the lens (forced perspective) shrinks the
   hand: an over-the-shoulder 1 m behind a survivor with the hand 6 m away makes them look almost
   the same size. [inferred, geometry]
2. **Blocks are the second yardstick.** A block is 2 m tall and 6 m long. The hand lifting a block
   that a survivor stood on says "giant" better than the hand alone. [inferred]
3. **Low camera for the giant, high camera for the small.** Camera 0.3-0.9 m above the survivor's
   surface (about survivor eye level) tilted up at the hand; camera above and behind a survivor
   looking down at the drop for vulnerability. [sourced: Godzilla 2014, Pacific Rim, Shadow of the
   Colossus; heights scaled]
4. **Shoot from where a person could stand:** a ledge, the water, the beach. A camera floating
   beside the hand at its own height makes it look normal-sized. [sourced: ILM on Pacific Rim]
5. **Slow giants, fast small things.** If the hand's motion looks quick, slow the hand shot to
   0.5-0.7 x or pick an angle where it moves toward or away from camera. [sourced practice; numbers
   inferred]
6. **Falls are metre-true.** 10 m takes 1.4 s, 20 m 2.0 s, the 56 m tower 3.4 s (t = sqrt(2h/g)).
   Real time already reads big. If falls look toy-fast because viewers read survivors as 1.7 m
   people, 0.75 x on scale-shot falls is the suggested fix (sqrt of 0.94/1.7). [inferred]
7. **Avoid the miniature look on wides:** deep focus (f/8 or smaller aperture) on wide and
   establishing shots; shallow focus only on close shots. A high top-down wide with shallow focus
   reads as a tabletop model. The candy look may want a mild toy feel in some shots; decide on
   purpose. [sourced: tilt-shift research]
8. **Atmosphere for distance:** light haze so the far ocean and far tower lose 10-30 % contrast.
   [sourced: aerial perspective; percentage inferred]

## 3. Move library (game-scale parameters)

Every shot names one move and its purpose (hard rule 10). Render 0.5-1 s of handle before and
after every move; cut inside the move, not at its ends. Measured: most gameplay shots are static
or tracking; designed moves are a median 11 % of shots (21 % in reveals) and belong on the
opener, the reveal, the payoff and the title.

| Move | Purpose | Parameters at game scale | Ease | Common failure |
|------|---------|--------------------------|------|----------------|
| `static` | let the action move; the "truth" shot | zero motion; optional 0.05-0.1° breathing noise | none | a tiny drift that reads as an error |
| `push_in` | tension, attention | distance down 15-35 % over 2-4 s; survivor shots 0.15-0.8 m/s, tower shots 1-5 m/s | in_out | constant speed; pushing into a block |
| `pull_out` | reveal context ("on a tower in the ocean") | distance x3-x10 over 3-6 s, 10-30° of rise | in_out, longer ease-out | too fast to read; reveals nothing new |
| `tilt_up` | make something huge | camera 0.3-0.9 m above the surface, 24-35 mm, 30-70° over 2-4 s, at most 15-20°/s | in_out, slow start | starts already on the giant |
| `tilt_down` | follow a drop, show the height | 30-70° over 1.5-3 s | in_out | loses the subject at the bottom |
| `crane_up` / `crane_down` | lift from human level to the world | survivor scale: rise 1.5-8 m at 0.5-1.5 m/s; tower scale: 10-40 m at 3-8 m/s; damping 1.5-3 s | in_out | rotation faster than the rise; horizon tilts |
| `orbit` | 3D shape, hero moment | 15-45° of arc over 3-5 s (3-12°/s), fixed radius and elevation | in_out on angle | constant-speed 360 (Lieu: "game-y") |
| `truck` | parallax, depth | 0.15-0.8 m/s near survivors (2-6 m/s at tower scale); a foreground block 0.5-2 m from the lens | in_out | no foreground, so no parallax |
| `pan` | follow attention sideways | background at most 0.18 frame widths per second at 30 fps (0.36 at 60) | in_out | judder from a fast pan |
| `follow` | stay with a moving survivor | side or three-quarter at 1.6-4.4 m; subject at x 0.33 moving right (0.67 left); lookahead 0.2-0.5 s; dead zone 0.1-0.2; damping 0.5-1.0 s position, 0.3-0.6 s rotation | damped | copying every animation jitter |
| `ride` | camera fixed to a moving actor (Studio) | offset 0.5-1.5 m; lag 0.05-0.2 s | damped | nausea; subject leaves frame |
| `handheld` | panic, "you are there" | rotational noise in three layers (0.1-0.5, 0.8-1.5, 3-4 Hz); calm 0.3-0.6° low + 0.05-0.1° high; panic 1-2° + 0.2-0.4°; halve below vFOV 20°; position noise 0.5-1.5 cm | continuous | on every shot; noise above about 6 Hz |
| `whip` | energetic transition | 90-180° in 6-12 frames at 30 fps; cut hidden mid-blur | hard S curve | too slow; no blur |
| `crash_zoom` | comic or shock emphasis | FOV narrows 2-4 x in 0.15-0.3 s, 3-5 % overshoot, settle 0.2 s | fast ease-out | combined with camera motion |
| `dolly_zoom` | the ground drops away | keep `d x tan(FOV/2)` constant; FOV change 20-40° over 1.5-3 s | in_out | subject size pulses |
| `pov` | the survivor's or hand player's eyes | smoothed position 0.2-0.4 s, rotation 0.3-0.6 s; vFOV 55-70°; eye height 0.85 m for survivors; hands high in frame; hold 1-2 s | smoothed | raw head-locked jitter (never) |
| `fall_with` | ride a fall | start 1.1-2.2 m beside and slightly below the survivor; accelerate at 80-95 % of g so the survivor drifts up in frame; 1-2 s | gravity ease-in | constant speed (subject leaves frame); passing through blocks |

Shake on impacts: rotation only, amplitude = trauma² x max angle, decaying over 0.3-1.5 s; max
1.5-3° for a block slam near camera, 3-5° for a tsunami hit; start within 0-2 frames of the
impact, never before it. [sourced: Eiserloh; angles inferred]

Shutter and blur: 180° equivalent (1/60 s at 30 fps). URP has no Recorder accumulation blur; use
the URP Motion Blur override (Camera and Objects) or render at high frame rate and average frames
offline; never both. Slow motion in whole ratios (1/2, 1/4), only on impacts and peaks of falls.
[sourced]

## 4. Framing metrics (computed from the engine, per frame, reported per shot)

| Metric | How |
|--------|-----|
| `h` | projected subject bounds height / screen height, clipped to screen |
| area % | subject pixels (ID buffer) or projected box area / screen area |
| thirds error | distance from the key point (survivor head, hand fingertips, active block) to the nearest thirds intersection |
| facing `phi` | angle between the subject's flattened forward and the subject-to-camera vector; 0 = facing camera |
| occlusion % | blocked rays / total from the camera to a 5 x 5 grid on the subject bounds (ignore the subject, spray, particles) |
| clip | share of the subject's box outside the 90 % action-safe area |
| roll | degrees off level |
| camera speed | m/s and deg/s from the camera track |
| jerk | peak second difference of velocity in the first and last 0.3 s of a move |

Pass ranges (median in range, 10th/90th percentile at most one band outside):

| Shot | Subject size | Placement | `phi` | Occlusion max | Other |
|------|--------------|-----------|-------|---------------|-------|
| EWS tower | tower 0.5-0.9; survivors 0.02-0.08 | tower centre on x 0.5 or a vertical third; horizon y 0.25-0.40 or 0.60-0.75 | - | 10 % | roll ≤ 0.5°; under 6°/s |
| EWS crown (payoff) | crown 0.65-0.85; hand 0.2-0.3; survivors 0.05-0.08 | break point within 0.15 of a thirds point | - | 10 % on the break point | locked or under 4°/s before impact |
| WS survivor | 0.20-0.45 | thirds error < 0.08 | 0-100° | 15 % | foreground element preferred |
| FS survivor | 0.70-0.90 | head 0.05-0.15 below top | 0-80° | 10 % | feet and head in frame |
| MS / MCU survivor | frame height per the size table | eyes at y 0.62-0.70 | 0-60° (face shot) | 5 % | lead room 0.55-0.70 if moving |
| CU face | frame height 0.18-0.28 m | eyes at y 0.60-0.68 | 0-45° | 2 % | shallow focus on the eyes |
| Giant reveal | hand 0.5-1.2 (may break the top edge); survivor 0.15-0.30 | survivor on a lower third, hand in the top two thirds (when the hand grips a block *below* the survivor, invert: survivor upper, hand lower) | survivor may look up and away | survivor 20 %, hand 30 % | camera ≤ 0.9 m above the survivor's surface, pitch up 15-60°; survivor distance ≥ 0.6 x hand distance; hand and survivor apart on screen by at least the hand's angular radius + 4° (pilot) |
| Pull (close-medium) | block 0.4-0.8; block travel ≥ 15 % of frame width | fingertips on an upper thirds point, block centre | survivor in frame 0-60° if present | 10 % on the block | static during the yank; impulse shake 1-2°, 0.4 s |
| Follow | as its size | lead room 0.58-0.70; subject screen speed < 0.15 frame/s | 0-110° | 15 % (spikes under 0.5 s ignored) | background speed exempt |
| Fall with | 0.15-0.45 | subject y 0.35-0.65 throughout | any | 20 % | camera vertical accel 0.6-1.0 g |
| POV | - | target within 0.2 of centre | - | 20 % | under 30°/s; smoothed |

Universal: roll within ±0.5° unless tagged Dutch (then 10-20°, constant); background pan speed
≤ 0.18 frame widths/s at 30 fps except whips and follows; peak jerk at move ends ≤ 2 x the move's
median jerk; no camera inside geometry (collision sphere 0.10-0.15 m); no near-plane clipping of the
subject; shake RMS under 0.3° outside impacts unless handheld.

Adjacent shots: the only hard check is the jump-cut guard (same subject, size, angle class and less
than 30° of yaw: fail). Same-size cuts between different subjects or places are normal (measured:
59 % of reference cuts). Keep all cameras of one beat on one side of the action line (the pull
direction, the wave direction).

## 5. Recipes

### The hand
1. **Arrival (tilt-up).** Camera on a survivor's ledge 0.3-0.9 m above the surface, 2.5-4 m behind
   and to the side of one or two survivors, 24-28 mm. Start on the survivors in the lower third with
   sky above; the hand's shadow or a light change arrives first; tilt up 30-50° over 2.5-3.5 s as the
   hand enters and breaks the top edge. Keep the hand 4-6 m from camera so the survivors stay at
   ≥ 0.6 of its distance. (Report 03 put the camera 1-1.6 m up and 2-4 m behind a 1.7 m person;
   both are rescaled, and the distance is raised to protect the scale ratio.)
2. **Silhouette.** Put the hand between the camera and a bright sky; cut fill so it reads as a dark
   shape with a bright rim. [sourced: Godzilla 2014]
3. **Long-lens menace.** From 50-100 m on the water at 85-200 mm, frame the crown (12-14 m tall)
   with the hand above it; locked off or a very slow push. Compression stacks hand and tower.
4. **The pull.** Camera at block height, 4-7 m from the block's side, 35 mm (frame 2.7-4.8 m high);
   fingertips on an upper thirds point, the block in the centre, a survivor on the opposite third
   facing within 60°. Slow push-in over the grab (2-3 s) is optional; hold still for the yank; impulse
   shake 1-2°, decay 0.4 s. The block must travel ≥ 2 m (≥ 15 % of frame width). Report 03's 1-3 m
   assumed a smaller block; at 6 m long that distance shows only the block's face.
5. **Hand POV (hand player).** Smoothed `pov`, the player's hands reaching down at the tiny tower,
   hands high in frame, actions slowed and held 1-2 s.

### The tower
1. **Base-up establishing.** On the beach or water 10-30 m from the base, camera 0.3-0.9 m up,
   18-24 mm, pitch up 20-35°; a survivor or two at the base for scale; horizon low (y 0.2-0.3).
2. **Crane up the face.** Rise 1-3 m/s, 2.2-4.4 m out, the climbing survivor on a lower third; ease
   in over 1 s, out over 1.5 s; blocks passing at the frame edge for parallax.
3. **Top-down vulnerability.** 0.5-1.0 m above and behind a survivor at the top, looking down past
   them to the water; 24-35 mm; deep focus.

### A collapse (re-render the same seeded take from several cameras)
- A: locked-off long lens, 40-80 m, 85-135 mm, the whole crown and the water. The truth shot and the
  main slow-motion source.
- B: low wide (18-24 mm) on a lower ledge looking up as debris falls toward camera; impulse shake on
  landing.
- C: survivor-level MS or FS (1-2 m, 35-50 mm), survivor reacting, debris passing between camera and
  survivor.
- D: tilt down with a falling block at its accelerating speed, ending on the splash.
Lock the camera 0.5-1.0 s before the break with the weak point on a thirds point. Ramp to 0.25-0.5 x
for 0.5-1.5 s around the break, back to real time before the cut. Shake starts within 0-2 frames of
the impact, decays 0.4-1.0 s.

### A survivor fall
Show the cause in the previous shot or the first 0.5 s. Then: (A) stay on the ledge and tilt down
as the survivor shrinks toward the water (sells height); (B) `fall_with` from 1.1-2.2 m beside, light
handheld, 1-2 s, cut before or to a wide splash; or (C) from the water at 24 mm, looking up as the
survivor falls toward the lens.

### A tsunami
1. **Horizon threat:** from the crown, 85-135 mm, a survivor's head and shoulder 0.3-0.8 m from the
   lens in soft foreground, the wave as a rising line; hold or very slow push 2-4 s.
2. **Wall of water:** camera 0.3-1.0 m above the water, 18-24 mm, pitch up 10-25°; wave fills the
   top two thirds; tower and survivors in the lower third.
3. **Impact:** locked-off side wide (fixes screen direction for the whole sequence), then a handheld
   survivor-level shot with 3-5° shake decaying over 1-1.5 s.
4. **Aftermath:** high crane or drone looking down at what is left.

## 6. VR to flat screen

- Never use a raw head-locked view. Smooth every POV (position 0.2-0.4 s, rotation 0.3-0.6 s),
  exaggerate head turns, keep hands high, slow hand actions. [sourced: Gartner, Lieu, Road to VR]
- Measured: 6 of 8 VR trailers are mostly first person. For Deadly Jenga, third person carries the
  scale (the selling point), and smoothed POV carries "you are there": the survivor looking up at
  the hand, the hand player reaching down. Default POV share: under half the shots.
- Brighten for monitors and phones. Capture at 60 fps where the render budget allows.
- Live action of a headset player (Gorilla Tag, Job Simulator) is a suggestion only; the Studio
  cannot capture it.

## 7. Mistakes

| Symptom | Fix |
|---------|-----|
| Nothing seems to happen | pick a move with a purpose; the subject at the size for its shot type; one action across the frame |
| Subject tiny or lost | set the size first, then `d = Hf / k` |
| Subject faces away | place the camera from the subject's forward vector; facing ≤ 60° for reactions |
| Blocks in front of the subject | occlusion check in the scout; choose the clearest of 8 yaw angles |
| Hand looks normal-sized | survivor at a similar depth in frame, low camera, slow hand, crown framing |
| Hand looks like a smooth white glove with a cut stump (pilot) | silhouette against bright sky, wrist out of frame or behind blocks, contrast from rim light; ask the human pass |
| Hero too small to read (pilot: 0.17 of frame) | a separate MS-or-tighter cut for the reaction |
| Hand hides the hero (pilot: hand, hero and lens in one line) | separate them on screen by the hand's angular radius + 4°; do not put the hero's facing straight at the lens with the hand behind |
| Payoff leaves frame toward the lens (pilot) | place the frame so the action's end point stays inside; check the frame after the key |
| Copied frame cannot hold the new action (skill test 1) | the `fit` line (section 1b) |
| Robotic motion | ease every move; damping 0.5-3 s; jerk check |
| Judder | pan speed limit; 180° shutter; fixed timestep |
| Toy look | deep focus on wides; low camera; consider 0.75 x on falls |
| Camera inside blocks | collision sphere 0.10-0.15 m; near clip ≤ 0.05 m |
