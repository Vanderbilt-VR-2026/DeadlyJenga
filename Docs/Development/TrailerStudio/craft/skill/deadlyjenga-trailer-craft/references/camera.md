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
| Giant hand (legacy) | drawn 2.4 x 2.3 x 1.8 m (revision 2's "4 m" was wrong) | framing it: `legacy-hand.md` section 3 |
| Block | 6 x 2 x 2 m | twice a survivor's height |
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

### Fights, crowds, disasters and the tower

| Framing | Hf | Distance |
|---------|----|----------|
| Two survivors fighting, both whole, room for a 3 m knockback (each h 0.25-0.35) | 2.7-3.8 m | 2.7-3.8 m at 24 mm; 3.9-5.5 m at 35 mm |
| A crowd: 3-5 survivors readable (each h ≥ 0.08, ideally ≥ 0.12) | ≤ 11.75 m (≤ 7.8 m) | ≤ 11.75 m at 24 mm |
| A survivor sailing off the structure (h 0.08-0.2 through the flight) | 4.7-11.75 m | follow the arc: a glove launch is 22 m/s sideways |
| Tsunami wall (40 m above the sea) with the castle (25 m) for scale | 50-60 m | 50-60 m at 24 mm, side-on |
| Tornado funnel (65 m tall, 16 m wide) beside the crown | 60-80 m whole; 20-30 m for the funnel wall | measure the funnel in the scout |
| Crown (top 9-12 m of the tower) with room around it | 14 m | 80 m at 135 mm; 50 m at 85 mm; 20 m at 35 mm |
| Whole tower, h 0.6-0.9 | 62-93 m | 47-70 m at 18 mm; 62-93 m at 24 mm |

In a whole-tower frame survivors are about 1 % of frame height: nobody reads, so no hit reads. Use
whole-tower frames for establishing scale, the sea and a disaster's size, never for a fight. Every
number here is a starting point (estimate for the fight and crowd rows); derive each shot's own.

The crown row frames a topple of the top 9-12 m only. An impact lower down (a wave striking
mid-tower, a pull near the base) needs a frame derived for that height: in skill test 1 a copied
80 m / 135 mm crown frame could not contain the wave its plan described.

### 1b. The framing check (`fit`, hard lint L38)

Write one line per shot, derived for that shot:
1. `Hf = distance x 24 / focal` at the subject's distance (16:9 frame width = 1.78 x Hf).
2. The subject's fraction `h = subject height / Hf`; it must match the declared `size` (tables above;
   POV and non-survivor subjects use the subject-fraction rule in `plan-schema-and-lints.md`).
3. Every other thing the shot claims at the same time, at its own distance: each survivor counted as
   readable (h ≥ 0.08), the attacker and the victim of a hit, the victim's knockback path (a punch
   shoves about `sqrt(2 g d) x 0.65` m/s, d = the mode's punch distance; the glove launches 22 m/s),
   the impact point, both threats if two are claimed, the hero's head and hands if her reaction must read.
4. Where the action ends: if the payoff exits frame (the pilot's block and hand left toward the lens),
   move the camera or choose a frame that holds the end point.
5. Nothing the game or the Studio cannot do (`game-content.md` section 9).

Example: "Hf = 3.2 x 24/24 = 3.2 m: the victim 0.29 (WS), frame 5.7 m wide; she starts at x 0.65 and
a 2-3 m knockback to the left ends at x 0.13-0.30; the attacker at x 0.82, both whole at the strike."
If the arithmetic fails, change the distance or lens, not the claim. Reaching an example's numbers is fine only if the derivation for your shot gets there.

### 1c. POV shots

Size by what fills the frame, using the subject-fraction rule (a survivor seen tiny from the hand's
view is WS or EWS by its own height fraction). `side` is null (no yaw), so the jump-cut check skips
POV pairs. Hold 1-2 s by default; inside a barrage a POV may be 0.5-1.0 s if the same POV was held
≥ 1 s earlier. A survivor POV sits at eye height 0.85 m. A first-person hit (the glove filling the lower
frame, the victim flying off) is a strong "you are there" beat; smooth it.

> **First-person camera fix in progress, see commit note.** Today the Studio's `firstPerson` behavior
> is a raw head-locked view (`StudioCameras.cs:186-188`: camera at the recorded head, head rotation,
> no smoothing). Another agent is adding smoothing, the actor's own hands and weapon in view and the
> own head hidden, and will write the how-to here and in `studio-tooling.md`. Until then plan every
> POV with `camera.pov_of`, `pov_smoothing`, a `needs: first_person_camera_with_hands, verify` item and
> a close over-the-shoulder fallback (0.3-0.5 m behind and 0.1-0.2 m above the head). When first
> person is required is decided by section 1d and SKILL.md rule 18, not by this note.

### 1d. Close on every key event (hard lints L47, L48)

A **key event** is every hit that lands (`pvp_hit`), every knock-off, elimination and fall into the
sea, every disaster hit with a named survivor victim, and the payoff. Each one needs at least one
shot, on screen when it happens, where **the affected survivor is ≥ 0.25 of frame height** (or, for
a payoff with no survivor, the impact point is), **or the event is seen in first person** (`pov`).
0.25 is an estimate: it is the bottom of the fight two-shot band (0.25-0.35) in section 1; the
critics of disaster-pvp-1 called its victims at 0.07-0.15 too small, and "a person doing
something" reads from about 0.2 (section 2).

Distance for h = 0.25 (frame height at the survivor 3.76 m): 24 mm ≤ 3.8 m; 35 mm ≤ 5.5 m; 50 mm ≤
7.8 m; 85 mm ≤ 13.3 m. Aim for 0.3-0.5 on the hit itself. A long lens from far away meets the number
but flattens the moment; for immersion prefer a lens within about 6 m, low, on the victim's side of
the line, or the attacker's or victim's own eyes.

**Wides are for geography and scale.** An EWS or WS of the tower, the wave or the crowd may show a key
event too, but it may never be its only coverage. Pair it with a close shot or a POV of the same
event (the same `take_ref.event`), across one cut or as a cutaway inside a single take.

**Derive the camera from the tape, not the plan.** Each key beat carries
`take_ref: {event, take_frame, subject_h, source}`: the event id from `key_events`, the tape frame
where the take report puts it, the subject's height share at that frame, and where the number came
from (`fit` before a take exists, `tape` when computed from the take's positions, `metrics` when
measured in a render). Once `capture.take_status: committed`, `fit` is not accepted.
`record-and-layer.md` section 3, step 6 gives the placement method.

## 2. Scale rules for this game

1. **People set the size.** A survivor reads as a person at h ≥ 0.08 and as a person doing
   something (a punch, a flinch) at about h ≥ 0.2 (estimate). A disaster looks huge only with
   survivors at a similar depth in frame: a wave behind four fighters, a funnel beside the crown.
   [inferred, geometry]
2. **Blocks are the second yardstick.** A block is 2 m tall and 6 m long. A disaster moving a block
   a survivor stood on says "huge" better than the disaster alone. [inferred]
3. **Low camera for the giant, high camera for the small.** Camera 0.3-0.9 m above the survivor's
   surface tilted up at the wave or funnel; camera above and behind a survivor looking down at the
   drop for vulnerability and for a knock-off's fall. [sourced: Godzilla 2014, Pacific Rim, Shadow
   of the Colossus; heights scaled]
4. **Shoot from where a person could stand:** a ledge, the water, the beach. A camera floating
   beside a funnel at its own height makes it look small. [sourced: ILM on Pacific Rim]
5. **Slow giants, fast small things.** A big disaster moving fast looks small: slow its shot to
   0.5-0.7 x or pick an angle where it comes toward the lens. Fights stay at real speed. [sourced
   practice; numbers inferred]
5b. **One fight, one line.** In a fight keep the camera on one side of the line between the two
   fighters for the whole beat, so the attacker stays on the same side of frame (30-degree and
   180-degree practice). A hit across a cut keeps the victim's travel in the same screen direction.
   [sourced: continuity practice]
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
| EWS crown (payoff) | crown 0.65-0.85; survivors 0.05-0.08 | break point within 0.15 of a thirds point | - | 10 % on the break point | locked or under 4°/s before impact |
| Fight (two-shot) | attacker and victim each 0.20-0.45, both whole at the key | the victim's knockback path inside frame (or picked up by the next shot) | 0-100° | 15 % each | camera on one side of the fighters' line |
| Crowd | 3 or more survivors at ≥ 0.08 | one focal survivor (the hero or the hit) on a thirds point | - | 20 % each | static or a slow move |
| WS survivor | 0.20-0.45 | thirds error < 0.08 | 0-100° | 15 % | foreground element preferred |
| FS survivor | 0.70-0.90 | head 0.05-0.15 below top | 0-80° | 10 % | feet and head in frame |
| MS / MCU survivor | frame height per the size table | eyes at y 0.62-0.70 | 0-60° (face shot) | 5 % | lead room 0.55-0.70 if moving |
| CU face | frame height 0.18-0.28 m | eyes at y 0.60-0.68 | 0-45° | 2 % | shallow focus on the eyes |
| Giant hazard reveal (wave, funnel, Kraken) | hazard 0.5-1.2 (may break the top edge); survivor 0.15-0.30 | survivor on a lower third, hazard in the top two thirds | survivor may look up and away | survivor 20 % | camera ≤ 0.9 m above the survivor's surface, pitch up 15-60° |
| Hand (legacy) | see `legacy-hand.md` section 3 | | | | |
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

### A fight
1. **The two-shot.** 24 mm at 2.7-3.8 m, camera 0.4-0.9 m up, on one side of the fighters' line;
   attacker on one third, victim on the other with room on her far side for the knockback. Static
   or calm handheld; hold the strike 12+ frames before and after.
2. **The hit's result.** If the victim leaves frame (a glove launch, a knock-off), the next shot
   picks her up within one cut, travelling the same screen direction (L46).
3. **Over the attacker's shoulder.** 35-50 mm, 0.3-0.8 m behind the attacker's shoulder: the victim
   readable at 0.25-0.4, so the viewer is the attacker for a beat.
4. **The weapon.** A rocket or grenade: frame the shooter and the impact in one wide (blast 8 m and
   3.5 m), or cut shooter → impact on the hit with the projectile's direction kept.

### A knock-off into the sea
Show the cause (the hit) → the victim leaving the edge with the sea below in frame (camera high,
looking down) → the splash or the victim small against the water. A 25 m fall takes 2.3 s: hold
it or cut to the splash on a hit. Real eliminations are off on the Studio stage: plan the splash.

### Several survivors at once
Frame ≤ 11.75 m at the farthest survivor you count (h ≥ 0.08); stagger them in depth so no one
hides another; the hero brightest-coloured and on a thirds point; one action in focus, the rest as
background busyness. Gang Beasts and Goose hold one fixed wide game camera for most shots (91 % and
100 % same-size cuts): a fixed wide is an honest party-game camera. [measured]

### Geography
Early in the cut, one wide with the structure and the hero on it (L44); afterwards keep one height
cue in every shot (the sea below, the crown above, a known block), and tag `tower_level`. When the
action jumps a level, cut on a motion (a fall, a launch) that carries the eye down or up.

### The giant hand (legacy)
`legacy-hand.md` section 3.

### The tower
1. **Base-up establishing.** On the beach or water 10-30 m from the base, camera 0.3-0.9 m up,
   18-24 mm, pitch up 20-35°; a survivor or two at the base for scale; horizon low (y 0.2-0.3).
2. **Crane up the face.** Rise 1-3 m/s, 2.2-4.4 m out, the climbing survivor on a lower third; ease
   in over 1 s, out over 1.5 s; blocks passing at the frame edge for parallax.
3. **Top-down vulnerability.** 0.5-1.0 m above and behind a survivor at the top, looking down past
   them to the water; 24-35 mm; deep focus.

### A collapse (render the same committed take from several cameras)
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
- Measured: 6 of 8 VR trailers are mostly first person (VR column first-person share 0.67,
  `measured-reference.md`). Deadly Jenga is a VR game, so **first person is required, not an option**
  (hard lint L49; rule 18): a montage (15, 30, 60 s) has at least 25 % of its body frames in first
  person (estimate), in at least two POV shots, and at least one POV is on a key event. A single take
  or a moment has at least one POV of 1 s or more on a key event (or is itself a POV take). Third
  person still carries scale and the readability of a fight; POV carries "you are there": a survivor
  looking up at the wave, a first-person glove hit, a friend's shove seen from the friend's eyes.
  Above 60 % POV, declare `pov_led: true` (soft lint L50). Revision 3 said only "under half", with no
  floor and "third person carries the scale", so agents left first person out; that is why.
- Brighten for monitors and phones. Capture at 60 fps where the render budget allows.
- Live action of a headset player (Gorilla Tag, Job Simulator) is a suggestion only; the Studio
  cannot capture it.

## 7. Mistakes

| Symptom | Fix |
|---------|-----|
| Nothing seems to happen | pick a move with a purpose; the subject at the size for its shot type; one action across the frame |
| Subject tiny or lost | set the size first, then `d = Hf / k` |
| A key hit seen only from far away (disaster-pvp-1 victims 0.07-0.15; example B's revision-3 pull-out 0.063) | a close shot (h ≥ 0.25) or a POV on the event, placed from the victim's tape position at the event frame (section 1d) |
| Camera placed from where the victim stood before the hit | read the position at the event frame from the take (`StudioActorPoses.At`) |
| No first person in a VR game's trailer | L49's floor: 25 % of a montage's body, a POV on a key event |
| Subject faces away | place the camera from the subject's forward vector; facing ≤ 60° for reactions |
| Blocks in front of the subject | occlusion check in the scout; choose the clearest of 8 yaw angles |
| Who hit whom is unclear | both fighters whole in a two-shot at the key; one side of their line |
| The victim flies off and is lost | frame the knockback path, or pick her up in the next shot, same direction |
| A crowd where nobody reads | frame ≤ 11.75 m at the farthest counted survivor |
| The disaster looks small | survivors at a similar depth, low camera, slow the disaster shot |
| Hero too small to read (pilot: 0.17 of frame) | a separate MS-or-tighter cut for the reaction, framing head and both hands |
| The hero renders white (motion blur) | render without URP motion blur; check a colour still |
| The hand (legacy) looks normal-sized or hides the hero | `legacy-hand.md` sections 3-4 |
| Payoff leaves frame toward the lens (pilot) | place the frame so the action's end point stays inside; check the frame after the key |
| Copied frame cannot hold the new action (skill test 1) | the `fit` line (section 1b) |
| Robotic motion | ease every move; damping 0.5-3 s; jerk check |
| Judder | pan speed limit; 180° shutter; fixed timestep |
| Toy look | deep focus on wides; low camera; consider 0.75 x on falls |
| Camera inside blocks | collision sphere 0.10-0.15 m; near clip ≤ 0.05 m |
