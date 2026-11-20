# Plan schema and lints

Open at stage 3. `plan.yaml` (or the plan table in a planning-only `plan.md`) is the single source
of truth for a trailer. Lints run on it alone (L-series), or on it plus scout numbers, the take
report and the render (R-series). Hard lints block the next stage. Soft lints must be fixed or
waived in the plan with `waive: [{lint, shot, reason}]`; the critic sees every waiver.

Frames are the true unit (integers at the plan's fps). Seconds are derived: `t = frame / fps`.
All limits below are written for 30 fps; scale frame counts by `fps / 30`.

**Never bend a shot or a label to pass a lint** (hard rule 14). `shows`, `reveal`, `size` and
`learns` say what the shot really is. If a rule fights the concept, keep the concept and write a
waiver with the reason; a hard lint that fights the concept is reported to the human, not dodged.
Skill test 1's planners admitted relabelling `shows` to dodge L34, changing a cause "for the lint",
and cutting a near miss to 0.7 s to pass L21; each made the plan worse.

Checking by hand is error-prone (all four test planners said so). If you can run Python, put the
shot table in a list and compute L01, L06, L07, L21, L22, L24, L28 and L29 with a few lines rather
than by mental arithmetic.

## 1. Fields

### Header

| Field | Type | Values / notes |
|-------|------|----------------|
| `id`, `version` | string, int | one folder per version: `v01/`, `v02/` ... (planning-only mode may be one file) |
| `mode` | enum | `production` (default) or `planning_only` (no music file or no engine; see SKILL.md) |
| `concept_type` | enum | `hand`, `disaster`, `two_role`. Sets the core verb (L05), the first hold (L09) and what BLOCKS (L19). A hand cut with a hazard in it is `hand`. |
| `opening` | enum | `staged` (default) or `cold_open`; with `cold_open`, `cold_open_threat` names the threat shown at frame 0 |
| `threats` | list | threat ids: `hand`, `hazard:<name>` (e.g. `hazard:tsunami`) |
| `fps`, `length_frames` | int | fps 30 or 60 |
| `class` | enum | `15`, `30`, `60`: picks the numeric limits (a 20 s cut uses `15` limits scaled by length; say so in a waiver) |
| `aspect` | enum | `16:9`, `9:16`, `1:1` |
| `shape` | enum | `montage` or `single_take` |
| `hook` | string | the one thing only this game has |
| `hero` | actor id | the survivor the audience follows; never an example's id (L39) |
| `cast` | list of actor ids | every performed or bot survivor named in shots |
| `must_show` | list of ids | copied from the brief, one id per thing it asks for; each id has a one-line `proof` in `brief.yaml` saying what in frame proves it (L18) |
| `capture` | object | `hand`: `performed` / `directed` / `bot` / `none`; `take`: committed bundle path |
| `camera_sensor_mm` | float | 24 (all distances in `camera.md` assume it; the Studio's default is 18.66, so set each track's `sensorHeight`) |
| `music` | object | `file` (null in planning-only mode), `bpm`, `meter`, `downbeat_frame`, `sections` [{name, from_frame, to_frame}], `hits` [{name, role: `open` / `reveal` / `main` / `title` / `accent`, frame, bar_beat, measured_onset_frame (a frame, or `unmeasured`)}], `stopdowns` [{from, to}] |
| `titles` | list | {text, `role`: `title` / `tagline` / `cta` / `caption`, frame_in, frame_out, style: `card` / `over_picture`, position, closing: bool}. Exactly one entry has `closing: true` (role `title`). A `caption` is in-body text over gameplay; `tagline` and `cta` belong to the end card. |
| `hazard_set` | object | the hazard's parameters when a disaster is staged (e.g. tsunami `height_m`, `warning_s`, `offshore_m`, `speed_m_s`) |
| `mix` | object | `target_lufs` (-15), `true_peak_max` (-1.0), `title_vs_body_lu` (target), `ramp_lu` (target) |
| `studio` | object | features known available on this commit, e.g. `ramp_sound: false` |
| `waive` | list | {lint, shot, reason} |

### Per shot

| Field | Type | Values / notes |
|-------|------|----------------|
| `id` | string | `S01` ...; also the fixed camera id (avoids attempt 1's F14) |
| `section` | enum | `hook`, `teach`, `escalate`, `breath`, `payoff`, `aftermath`, `title` |
| `kind` | enum | `gameplay` (default), `text`, `black`, `logo` |
| `cut_in`, `cut_out` | frame | trailer frames; `cut_out` of shot n = `cut_in` of shot n+1 |
| `tape_in` | frame | where the shown action starts in the take (unknown until a take exists) |
| `speed` | list | [{at (shot frame), speed}]; 1 = real time |
| `subject` | {kind, id} | kind: `actor`, `block`, `blocks`, `hand`, `hazard`, `tower`, `point`. Exactly one. |
| `also_in_frame` | list | ids; actor ids here count as survivors in frame |
| `shows` | list | every must-show id this shot truly proves (do not drop a true label to pass L34) |
| `learns` | string | ≤ 12 words, one idea, no " and " and no ";" |
| `feels` | string | optional: target emotion, one word |
| `raises` | enum | required in `escalate`: `stakes`, `scale`, `threat`, `count`, `speed`, `novelty` |
| `reveal` | enum | `none`, `knows` (audience sees a threat the character does not), `effect`, `partial`, `reaction`, `whole`, `consequence`, `payoff` |
| `threat` | threat id | which threat a `knows`/`effect`/`partial`/`reaction`/`whole`/`payoff` tag is about; may be omitted when `threats` has one entry |
| `cause_in` | enum | `self` or `prev`; required when `reveal` is `consequence` or `payoff` |
| `role_view` | enum | `hand`, `survivor` or `neutral` (default): whose side of the game the shot puts us on. Two-role cuts need both (L36). |
| `same_event` | string | optional: tags shots that show one event from different sides (L37) |
| `size` | enum | `EWS` < `WS` < `FS` < `MS` < `MCU` < `CU` < `ECU`, plus `INSERT` (counts as close). **Third-person with a cast survivor in frame:** size follows the nearest survivor (table in `camera.md`: EWS h ≤ 0.08, WS 0.20-0.45, FS 0.70-0.90; MS / MCU / CU by frame height). A survivor at h 0.08-0.20 is WS. **Otherwise, and for every POV shot:** judge by the subject's height fraction `h`: EWS < 0.1, WS 0.1-0.45, FS 0.45-0.9 (whole subject in frame), MS / MCU / CU when cropped to 50-70 / 35-50 / under 35 % of it; `INSERT` when a part of an object (a block end, fingertips) fills ≥ 50 % of frame height. |
| `angle` | enum | `eye`, `low`, `high`, `overhead`, `ground`, `dutch` (for a POV, the class of its pitch) |
| `side` | {face, yaw_deg} | face: `front`, `3q_front`, `profile`, `3q_back`, `back`; yaw 0 = facing camera. POV shots: `null`, `null`. |
| `camera` | object | `move` (library below), `purpose` (string), `ease` (`in_out`, `in`, `out`, `damped`, `none`), `distance_m` to subject, `height_m` above the subject's standing surface (POV: eye height), `focal_mm`, `aperture`, `focus` (`subject` or metres), `params` (move-specific), `pov_smoothing` {pos_s, rot_s} for POV |
| `fit` | string | **the framing check** (L38): frame height at the subject `Hf = distance x 24 / focal_mm`, the subject's fraction of it, and why the action point (impact, block travel, face, both threats) fits. Derived for this shot, never copied. |
| `key_frame` | shot frame | where the action peaks |
| `moments` | list | single-take only: [{at, section, reveal, threat, raises, cause_in, shows, what, sound_at, hook_count}] treated as beats by L03, L05, L08, L09, L12 (`what` follows the `learns` rules) |
| `action_cue` | object | `cue_id`, `tape_frame`, `expect` (e.g. `block_travel_m: 2.0`) |
| `frame_at_key` | object | `grid` (`TL` ... `BR`), `min_area_pct`, `max_occlusion_pct`, `breaks_frame` (bool) |
| `transition_in` | enum | `cut`, `match_cut`, `smash_cut`, `whip`, `dissolve` (needs a waiver), `cut_to_black` |
| `connects_from_prev` | object | `type` (`new_beat`, `cause_effect`, `reaction`, `eye_trace`, `match_motion`, `contrast_size`), `screen_direction` (`L>R`, `R>L`, `up`, `down`, `toward`, `away`, `none`), `eye_enter` (grid cell where the eye lands in this shot), `eye_exit` (where it leaves this shot) |
| `sound` | list | [{at (shot frame), kind: `music_hit` / `sfx` / `ambience` / `silence` / `vo`, name, source: `take` / `library` / `designed`, gain_db, sync (hit name, optional), `lead_in` (bool: a sound that deliberately starts before what it announces)}] |
| `hook_count` | int or null | which featured appearance of the hook this is (see L25) |
| `jump_cut` | enum | `deliberate` to waive L16 |
| `evidence` | list | optional: what the critic must see: `still@key`, `strip`, `overlay`, `flow`, `audio@key` |
| `risk`, `fallback` | string | what may fail, what to shoot instead; `fallback: "none"` means BLOCKED if it fails |
| `needs` | list | [{feature, status: `available` / `verify` / `fallback` / `missing`}]; every capability the shot relies on that SKILL.md's "what the game can do" list marks unknown is listed here as `verify` |

Camera move library (`camera.move`): `static`, `push_in`, `pull_out`, `pan`, `tilt_up`,
`tilt_down`, `truck`, `crane_up`, `crane_down`, `orbit`, `follow`, `ride`, `handheld`, `whip`,
`crash_zoom`, `dolly_zoom`, `pov`, `fall_with`. **Designed moves** (counted by L30): `push_in`,
`pull_out`, `pan`, `tilt_up`, `tilt_down`, `truck`, `crane_up`, `crane_down`, `orbit`, `follow`,
`dolly_zoom`. Parameters for each: `camera.md`.

Derived terms used by the lints:
- **Closing title**: the `titles` entry with `closing: true`. **Body**: frames `[0, closing.frame_in)`.
- **Body shots**: shots with `cut_in` < closing `frame_in` and `kind: gameplay`. Their lengths are
  `cut_out - cut_in`, clipped to the body.
- **Cuts**: `cut_in` of every shot except the first. **Cuts inside the body**: cuts with
  `0 < cut_in < closing.frame_in`.
- **Coarse size**: EWS -> EW; WS, FS -> W; MS, MCU -> M; CU, ECU, INSERT -> C (matches report 06).
- **Core verb** (by `concept_type`): `hand` and `two_role`: `pull` (the hand moves a block out of its
  slot). `disaster`: `hazard_hit` (the hazard visibly moves blocks or survivors).
- **Pull vs grip**: `pull` = the block leaves its slot in this shot. `grip` (or pinch) = the hand
  closes on a block that has not moved yet. Both are real actions; label each truthfully.
- **Anticipation reveals**: `knows`, `effect`, `partial`, `reaction`.

## 2. Lints

### Plan lints (no Unity)

| ID | Sev | Rule | How it is computed | Basis |
|----|-----|------|--------------------|-------|
| L01 | hard | Timeline exact | all frames integers; shot n `cut_out` = n+1 `cut_in`; first `cut_in` = 0; last `cut_out` = `length_frames` | bookkeeping |
| L02 | hard | One subject, one idea | exactly one `subject`; `learns` (and each moment's `what`) ≤ 12 words, contains no " and " or ";" | [sourced] Lieu, clarity |
| L03 | hard | Structure | the list of sections (shots, or `moments` in a single take) matches `hook+ teach+ (escalate|breath)+ payoff+ (aftermath|breath)* title+` with at least one `escalate` before the first `payoff`; every `escalate` has `raises` | [sourced] Lieu, Cauller |
| L04 | hard | Gameplay first | first shot `kind: gameplay`; min `cut_in` of gameplay shots ≤ 60 frames (2.0 s) | [measured] 13 of 21 |
| L05 | hard | Core verb early | the first shot or moment whose `shows` has the core verb, with the tower, a block or a `cast` actor as subject or in `also_in_frame` (`hand`/`two_role`: the subject is `hand` or `block` and a cast actor is in frame): `(cut_in + key_frame) / fps` (or the moment's `at`) ≤ 5.0 s (15), 8.0 s (30), 12.0 s (60) | [measured] mid median 4.3 s; 15 and 30 extrapolated |
| L06 | hard | Shape | `montage`: median body-shot length 30-66 frames (1.0-2.2 s); shortest ≤ 30 frames; longest ≥ 60 (15) / 90 (30) / 99 (60) frames; some window `[a, a+150)` inside the body holds ≥ 4 cuts. `single_take`: one body shot ≥ 70 % of the body; ≤ 2 cuts inside the body | [measured] 16 of 18; floors for 15 and 30 extrapolated |
| L07 | hard | Title timing | closing `frame_in` in [330, 360] (15), [720, 780] (30), [1590, 1650] (60); `length_frames - frame_in` in [90, 120] / [120, 180] / [150, 210]; no other title with the game name before it unless `style: over_picture` on a gameplay shot | [measured] 18 of 22 at 80-95 %, 15 of 22 cards 4-8 s; the 15 s window trades card length for position, extrapolated |
| L08 | hard | Staged reveal or cold open | for each threat T: let R = the first shot or moment with `reveal` in {`whole`, `payoff`} about T. **Staged** (R after frame 0): the summed length of earlier shots (or moment spans) with an anticipation reveal about T ≥ 21 frames (0.7 s). **Cold open** (R at frame 0, `opening: cold_open`, `cold_open_threat: T`): R is uncut for ≥ 30 frames, and the body has ≥ 2 `escalate` beats with `reveal: consequence` whose `raises` values differ. A second threat first shown after frame 0 always needs its own anticipation. Every `consequence` or `payoff` has `cause_in`; if `prev`, the previous shot `shows` `pull`, `grip` or a `hazard_hit`, or has `reveal` `whole` or `payoff` | [sourced] Hitchcock, Jaws, Lieu cause-effect; skill test 1 (frame-0 wave scored best on hook); 0.7 s and 1.0 s inferred |
| L09 | hard | Hold the first core verb | the first shot (or moment) whose `shows` has the core verb: length ≥ 45 frames; `key_frame` ≥ 12 and `length - key_frame` ≥ 12 (a moment: ≥ 12 frames from the take's ends and from any cut); a scale reference in frame (`hand`/`two_role`: subject kind `block` or `also_in_frame` holds an actor or a block; `disaster`: the tower or an actor in frame) | [sourced] Lieu (hold new mechanics); 1.5 s inferred; attempt 1 F17 |
| L10 | hard | Action inside the shot | `key_frame` ≥ 6 and `length - key_frame` ≥ 6; exception: `key_frame` ≤ 4 when `cut_in` is within 2 frames of a hit (impact on the cut) | [inferred], attempt 1 F17 |
| L11 | hard | Hits and sync | every hit has `measured_onset_frame` within 2 frames of `frame` (**deferred** in planning-only mode, where it reads `unmeasured`); the first `payoff` shot's `cut_in + key_frame` (or the payoff moment) within 2 frames of the `main` hit; closing `frame_in` within 2 frames of the `title` hit; every sound with `sync: X` sits within 2 frames of hit X (absolute frame `cut_in + at`) | [sourced] Lieu, Violet Recording; [measured] lean to hits |
| L12 | hard | Sound at every key moment | each shot (and moment) has a sound with `key_frame - 1` ≤ `at` ≤ `key_frame + 3`. Other sounds may sit anywhere: a `lead_in` (a creak or roar 0.5-1.5 s before a threat appears) is not the key's sound and is not bound by the 1-frame rule | [sourced] Lieu; ITU-R BT.1359 |
| L13 | hard | Ramps carry no take sound | any shot with a `speed` ≠ 1 has no sound with `source: take`, unless `studio.ramp_sound: true` | [sourced] attempt 1 F4 |
| L14 | hard | Face and close shot | some shot has size in {MCU, CU, ECU, INSERT}; some shot has subject = `hero`, `side.face` in {`front`, `3q_front`}, abs(`yaw_deg`) ≤ 60, size in {MS, MCU, CU, ECU} | [measured] + [sourced]; 60° inferred |
| L15 | hard | Camera declared | `camera.move` in the library; `purpose` non-empty; `ease` is not `none` unless the move is `static`, `whip`, `crash_zoom`, `handheld` or `pov` | [sourced] Lieu, Nesky |
| L16 | hard | No jump cut | for consecutive shots with the same subject, neither a POV: fail if size, angle and abs(yaw difference) < 30° all match, unless `jump_cut: deliberate` | [sourced] 30-degree rule |
| L17 | hard | No raw VR view | `move: pov` has `pov_smoothing.pos_s` in [0.2, 0.4] and `rot_s` in [0.3, 0.6]; no `raw_hmd: true` anywhere | [sourced] Gartner, Lieu, Road to VR |
| L18 | hard | Must-show covered | every `must_show` id is in some shot's `shows`, and the brief's `proof` line for it is true of that shot (no proof by implication: "multiplayer" needs players on screen) | brief; skill test 1 (multiplayer only implied) |
| L19 | hard | Features exist | every `needs` entry has status `available`, `verify` (R01 or R02 must confirm before capture), or `fallback` (and `fallback` text says what is shot instead). `missing` fails, and so does any shot that needs something SKILL.md lists as "cannot" (e.g. two giant hands). A `verify` item with `fallback: "none"` that fails in the scout makes the plan BLOCKED: for `hand` and `two_role` that includes the hand not being drawn; for `disaster`, the hazard not reaching or not being visible | [sourced] attempt 1 F5, F6; skill test 1 (a shot with two hands) |
| L36 | hard | Both roles are players | `two_role` only: some shot with `role_view: hand` and some with `role_view: survivor`, both with `cut_in` < half the body | [sourced] Evolve, Dead by Daylight; brief d; half inferred |
| L38 | hard | Framing derived | every gameplay shot has a `fit` line with `Hf = distance x 24 / focal` at its subject, the subject's fraction (matching `size`), and its action point inside the frame (impact point, block travel ≥ 15 % of width for a pull, both threats if both are claimed) | [sourced] skill test 1: critics found copied frames that could not contain their action; formula from `camera.md` |
| L39 | hard | No example reuse | no tagline, caption, hero id or cast id from the worked examples (list in `worked-examples.md` section 0). Soft part: a shot whose (distance, height, focal) equals an example shot's warns; its `fit` line must show the derivation | [sourced] skill test 1: 4 of 4 plans cloned the example |
| L20 | soft | Titles readable | each title, tagline and caption on screen ≥ `fps * (0.3 * words + 1.0)` frames | [inferred] |
| L21 | soft | Build shortens | montage only: median length of `escalate` shots ≤ median of `hook` + `teach` shots. Payoff, aftermath, breath and title shots are not counted, so a held payoff is never penalised | [measured] tendency 12 of 19 (by time quarters); section form inferred |
| L22 | soft | Varied lengths | montage only: CV (population std / mean) of body-shot lengths ≥ 0.35 | [inferred] |
| L23 | soft | No even run | montage only: no more than 3 consecutive body shots within 2 frames of each other's length | [sourced] Lieu |
| L24 | soft | Not gridded | montage only: share of cuts within 1 frame of a beat (`downbeat_frame + k * fps * 60 / bpm`) ≤ 80 % | [sourced] Lieu; [measured] |
| L25 | soft | Rule of threes | max `hook_count` is 2 or 3. `hook_count` counts featured appearances of the hook (shots built around it), not every shot where the hand or hazard is visible | [sourced] Lieu |
| L26 | soft | Screen direction | consecutive shots of one movement (pull, fall, wave) share `screen_direction` unless `connects_from_prev.type` is `new_beat` or `contrast_size` | [sourced] continuity practice |
| L27 | soft | Eye trace | `eye_exit` of n and `eye_enter` of n+1 are the same or touching cells (Chebyshev distance ≤ 1), unless `smash_cut` or `new_beat` | [sourced] Murch |
| L28 | soft | Peak late | `main` hit frame ≥ 0.6 x body length | [inferred] |
| L29 | soft | Sizes | coarse W + EW ≥ 40 % of body shots; coarse C ≤ 25 % (15) or 15 % (30, 60) | [measured] |
| L30 | soft | Moves | montage only: designed moves on 10-30 % of all shots | [measured] median 11 %, reveals 21 % |
| L31 | soft | Stop-down | class 30 or 60: at least one `music.stopdowns` entry ≥ 9 frames. One drop-out just before the main hit satisfies it; a `breath` section is optional and separate | [measured] 14 of 22 |
| L32 | soft | Text | captions (`role: caption`): ≤ 6 words, state a rule or fact of play, default count 1 for store and muted social cuts (otherwise 0-1 / 0-2 / 0-4 by class). Tagline ≤ 8 words, CTA ≤ 6 words, end card only. Everywhere: no superlatives ("epic", "insane", "ultimate"); the line would be false for another game | [sourced] Lieu; [measured] 11 of 22 use captions; skill test 1 (rule captions scored better on clarity); word counts inferred |
| L33 | soft | Barrage on hits | montage only: every cut that ends a body shot shorter than 24 frames (0.8 s) has a sound with `at` 0-2 in the next shot, or a hit within 2 frames | [measured] 12.8 % vs 6.7 % chance |
| L34 | soft | Idea count | no `shows` id other than the core verb appears in more than 3 shots. The core verb may repeat when each repeat after the third has a different `raises` | [sourced] Lieu |
| L35 | soft | Readable length | EWS, WS ≥ 15 frames; FS, MS, MCU ≥ 12; CU, ECU, INSERT ≥ 9; a POV ≥ 30 unless it repeats a POV already held ≥ 30 | [inferred] |
| L37 | soft | One event, both sides | `two_role` only: two shots with the same `same_event`, one `role_view: hand` and one `survivor`, adjacent or one shot apart | [sourced] Evolve; recipe in `reveals-and-moments.md` |

(L36-L39 are new in revision 2 and listed beside the hard lints they belong with.)

### Scout, take and render lints

| ID | Sev | Rule | How it is computed | Basis |
|----|-----|------|--------------------|-------|
| R01 | hard | Subject visible | scout at `key_frame`: subject screen area ≥ `min_area_pct`; occlusion (raycast grid or ID buffer) ≤ `max_occlusion_pct`; projected box inside the 90 % action-safe area unless `breaks_frame`. Set the plan's limits from the pass table in `camera.md` (2-15 % for people by shot size); never above 20 % for a person or 30 % for the hand | [sourced] attempt 1 F10, F5 |
| R02 | hard | Cue happened | take report has the cue's event within 8 frames of `tape_frame`, with its `expect` met; a pull moves its block ≥ 2.0 m within 45 frames | [sourced] F7, F12; 2 m inferred |
| R03 | hard | Render matches plan | detected cuts within 1 frame of planned `cut_in`; every hit and `sync` sound has an onset in the final mix within 2 frames, never more than 1 frame before the picture event | [sourced]; tools in `measured-reference.md` |
| R04 | hard | Delivery | integrated loudness -20 to -12 LUFS; true peak ≤ -1.0 dBTP; body last quarter short-term mean ≥ first quarter; closing-title section ≥ 3 LU below the body; ≤ 3 large luminance flashes in any 1 s; no black or silence longer than 0.25 s from frame 0; duration = `length_frames` | [measured] + [sourced] |
| R05 | hard | No reused footage | no two shots show overlapping tape ranges from cameras closer than 20 % of their subject distance and 20° apart | [sourced] Lieu |
| R06 | soft | Profile check | every OUT verdict from `profile.py check` is explained in the version notes | [measured] |
| R07 | hard | Human pass | a person's notes are saved in the version folder before the trailer is called finished | [sourced] F18 |

In planning-only mode every R-lint is **deferred**: list them under "before any render".

## 3. Worked example A: 15 s giant-hand reveal (passes every lint)

One of three worked examples (B and C, which differ in shape, opening and ending, are in
`worked-examples.md`). Do not copy its tagline, caption, hero or camera numbers (L39).

Music: a 120 BPM cue, 4/4, so one beat is 15 frames and one bar 60. Bars count from 1 at frame 0.
The `measured_onset_frame` values stand for what stage 2 measures on the real file; replace them
with the real ones and shift the plan to the real hits, never the other way round.

```yaml
id: hand-reveal-15
version: 1
mode: production
concept_type: hand
opening: staged
threats: [hand]
fps: 30
length_frames: 450
class: 15
aspect: "16:9"
shape: montage
hook: "A giant hand pulls the blocks people are standing on"
hero: mint
cast: [mint, rook]
must_show: [pull, hand, survivor_face, fall, tower_over_ocean]
capture: {hand: performed, take: "bundle/hand-reveal-take-01"}   # a person flies the hand in VR (F6)
camera_sensor_mm: 24                # set sensorHeight = 24 on every Studio track
music:
  file: Music/trailer_cue_120.wav
  bpm: 120
  meter: 4
  downbeat_frame: 0
  sections:
    - {name: intro, from_frame: 0,   to_frame: 120}
    - {name: build, from_frame: 120, to_frame: 228}
    - {name: drop,  from_frame: 240, to_frame: 345}
    - {name: tail,  from_frame: 345, to_frame: 450}
  hits:
    - {name: open,   role: open,   frame: 0,   bar_beat: "1.1", measured_onset_frame: 1}
    - {name: reveal, role: reveal, frame: 135, bar_beat: "3.2", measured_onset_frame: 136}
    - {name: main,   role: main,   frame: 240, bar_beat: "5.1", measured_onset_frame: 239}
    - {name: title,  role: title,  frame: 345, bar_beat: "6.4", measured_onset_frame: 346}
  stopdowns:
    - {from: 228, to: 240}          # 0.4 s music drop-out, one creak left
titles:
  - {text: "THE HAND IS ANOTHER PLAYER", role: caption, frame_in: 150, frame_out: 228, style: over_picture, position: lower_third, closing: false}
  - {text: "DEADLY JENGA", role: title, frame_in: 345, frame_out: 450, style: over_picture, position: center, closing: true}
  - {text: "Don't be the one who falls.", role: tagline, frame_in: 360, frame_out: 450, style: over_picture, position: lower_third, closing: false}
mix: {target_lufs: -15, true_peak_max: -1.0, title_vs_body_lu: -7, ramp_lu: 3}
studio: {ramp_sound: false}

shots:
  - id: S01
    section: hook
    kind: gameplay
    cut_in: 0
    cut_out: 45
    tape_in: 118
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: block, id: "block:212"}
    also_in_frame: [hand_fingertips]
    shows: [pull]
    learns: "Something huge is sliding blocks out of the tower"
    feels: jolt
    raises: null
    reveal: partial
    threat: hand
    cause_in: null
    size: INSERT
    angle: eye
    side: {face: profile, yaw_deg: 90}
    camera: {move: static, purpose: "let the sliding block carry the shot", ease: none,
             distance_m: 4.0, height_m: 1.0, focal_mm: 35, aperture: 4, focus: subject, params: {}}
    fit: "Hf = 4.0 x 24/35 = 2.74 m: the 2 m block end fills 0.73 (INSERT); frame width 4.9 m, so the
          2 m slide crosses 41 %; fingertips cropped at the top edge."
    key_frame: 22                   # the block starts to leave its slot
    action_cue: {cue_id: hand_pull_1, tape_frame: 140, expect: {block_travel_m: 2.0}}
    frame_at_key: {grid: MC, min_area_pct: 30, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: R>L, eye_exit: TC, eye_enter: null}
    sound:
      - {at: 0,  kind: music_hit, name: open, source: library, gain_db: 0, sync: open}
      - {at: 22, kind: sfx, name: candy_scrape_long, source: designed, gain_db: -3}
    hook_count: 1
    evidence: [still@key, strip, overlay]
    risk: "Fingertips not drawn in playback (F5)"
    fallback: "Block alone sliding out, scrape carries it; still a partial reveal"
    needs: [{feature: hand_drawn_in_playback, status: verify}]

  - id: S02
    section: teach
    kind: gameplay
    cut_in: 45
    cut_out: 105
    tape_in: 200
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: mint}
    also_in_frame: [sky, crown_blocks]
    shows: [survivor_face]
    learns: "A tiny climber on top feels it"
    feels: unease
    raises: null
    reveal: reaction
    threat: hand
    cause_in: null
    size: MCU
    angle: low
    side: {face: 3q_front, yaw_deg: 25}
    camera: {move: static, purpose: "read her face as she looks up", ease: none,
             distance_m: 1.0, height_m: 0.6, focal_mm: 50, aperture: 2.8, focus: subject, params: {}}
    fit: "Hf = 1.0 x 24/50 = 0.48 m: chest up (MCU 0.35-0.48); lens 0.6 m up, under her 0.85 m eyes."
    key_frame: 30                   # she looks up
    action_cue: {cue_id: mint_looks_up, tape_frame: 230}
    frame_at_key: {grid: MR, min_area_pct: 15, max_occlusion_pct: 5, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: reaction, screen_direction: none, eye_exit: MR, eye_enter: MR}
    sound:
      - {at: 18, kind: sfx, name: deep_tower_creak, source: designed, gain_db: -6, lead_in: true}   # sound before sight
      - {at: 30, kind: sfx, name: sharp_breath_in, source: designed, gain_db: -8}
    hook_count: null
    evidence: [still@key, overlay]
    risk: "A block edge hides her face (attempt 1 shot B)"
    fallback: "Use the scout's clearest of 8 yaw angles within 60 degrees of her facing"
    needs: [{feature: preview_stills, status: fallback}]   # workaround in studio-tooling.md

  - id: S03
    section: teach
    kind: gameplay
    cut_in: 105
    cut_out: 165
    tape_in: 300
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: hand, id: hand}
    also_in_frame: [mint, "block:207"]
    shows: [hand, pull]
    learns: "A giant hand pulls the block beside her"
    feels: awe
    raises: null
    reveal: whole
    threat: hand
    cause_in: null
    size: WS
    angle: low
    side: {face: 3q_front, yaw_deg: 30}
    camera: {move: tilt_up, purpose: "reveal the hand above the climber", ease: in_out,
             distance_m: 5.5, height_m: 0.5, focal_mm: 24, aperture: 8, focus: subject,
             params: {tilt_deg: 35, over_s: 1.5, hero_distance_m: 3.5}}   # hero 3.5 m >= 0.6 x 5.5 m
    fit: "Hf at the hand 5.5 m: hand (about 3 m) 0.55. Mint at 3.5 m: Hf 3.5 m, h 0.27 (WS); ratio 2.7.
          Block travel 2 m of a 9.8 m wide frame at the block (20 %). One hand only."
    key_frame: 30                   # the block slides out
    action_cue: {cue_id: hand_pull_2, tape_frame: 330, expect: {block_travel_m: 2.0}}
    frame_at_key: {grid: TC, min_area_pct: 20, max_occlusion_pct: 30, breaks_frame: true}
    transition_in: cut
    connects_from_prev: {type: eye_trace, screen_direction: R>L, eye_exit: TC, eye_enter: BR}
    sound:
      - {at: 10, kind: sfx, name: giant_knuckle_creak, source: designed, gain_db: -5}
      - {at: 30, kind: music_hit, name: reveal, source: library, gain_db: 0, sync: reveal}
      - {at: 30, kind: sfx, name: block_pop, source: designed, gain_db: -2}
    hook_count: 2
    evidence: [still@first, still@key, still@last, overlay]
    risk: "Hand not drawn (F5) or too small"
    fallback: "none"                # BLOCKED if the scout shows no hand
    needs: [{feature: hand_drawn_in_playback, status: verify}]

  - id: S04
    section: escalate
    kind: gameplay
    cut_in: 165
    cut_out: 195
    tape_in: 357
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: rook}
    also_in_frame: [gap, ocean_below]
    shows: [fall]
    learns: "The gap throws another climber off"
    feels: shock
    raises: stakes
    reveal: consequence
    cause_in: prev
    size: FS
    angle: high
    side: {face: 3q_front, yaw_deg: 45}
    camera: {move: static, purpose: "watch the slip from above", ease: none,
             distance_m: 1.7, height_m: 1.5, focal_mm: 35, aperture: 5.6, focus: subject, params: {}}
    fit: "Hf = 1.7 x 24/35 = 1.17 m: Rook 0.80 (FS); looking down, the gap below him in frame."
    key_frame: 15                   # he slips off the edge
    action_cue: {cue_id: rook_slips, tape_frame: 372}
    frame_at_key: {grid: MC, min_area_pct: 8, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: down, eye_exit: BC, eye_enter: MC}
    sound:
      - {at: 15, kind: sfx, name: small_yelp, source: designed, gain_db: -6}
    hook_count: null
    evidence: [strip, still@key]
    risk: "Bot does not step into the gap"
    fallback: "Pick the take report's nearest fall within 2 s; shift tape_in"
    needs: [{feature: take_report, status: fallback}]

  - id: S05
    section: escalate
    kind: gameplay
    cut_in: 195
    cut_out: 213
    tape_in: 446
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: hand, id: hand}
    also_in_frame: ["block:188"]
    shows: [hand, grip]
    learns: "The hand pinches a lower block"
    feels: threat
    raises: threat
    reveal: none
    cause_in: null
    size: FS
    angle: eye
    side: {face: profile, yaw_deg: 80}
    camera: {move: static, purpose: "show the next pull plainly", ease: none,
             distance_m: 7.0, height_m: 0.5, focal_mm: 35, aperture: 5.6, focus: subject, params: {}}
    fit: "Hf = 7.0 x 24/35 = 4.8 m: the whole hand (3 m) 0.62 (FS) with the 2 m block under its fingers."
    key_frame: 9                    # the fingers close
    action_cue: {cue_id: hand_pinch_3, tape_frame: 455}
    frame_at_key: {grid: MC, min_area_pct: 20, max_occlusion_pct: 30, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: new_beat, screen_direction: R>L, eye_exit: MC, eye_enter: MC}
    sound:
      - {at: 9, kind: sfx, name: giant_knuckle_creak_short, source: designed, gain_db: -4}
    hook_count: 3
    evidence: [still@key, overlay]
    risk: "Hand not drawn (F5)"
    fallback: "none"
    needs: [{feature: hand_drawn_in_playback, status: verify}]

  - id: S06
    section: escalate
    kind: gameplay
    cut_in: 213
    cut_out: 228
    tape_in: 463
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: mint}
    also_in_frame: ["block:209"]
    shows: [survivor_face]
    learns: "Her block starts to slide"
    feels: panic
    raises: stakes
    reveal: consequence
    cause_in: prev
    size: FS
    angle: eye
    side: {face: 3q_front, yaw_deg: 40}
    camera: {move: handheld, purpose: "panic as her footing goes", ease: none,
             distance_m: 1.7, height_m: 0.5, focal_mm: 35, aperture: 4, focus: subject,
             params: {noise: handheld_calm}}
    fit: "Hf = 1.7 x 24/35 = 1.17 m: Mint 0.80 (FS), feet and block edge in frame."
    key_frame: 7                    # the block lurches under her
    action_cue: {cue_id: mint_block_lurch, tape_frame: 470}
    frame_at_key: {grid: MC, min_area_pct: 10, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: R>L, eye_exit: MC, eye_enter: MC}
    sound:
      - {at: 0, kind: sfx, name: block_knock, source: designed, gain_db: -4}   # onset on the barrage cut
      - {at: 7, kind: sfx, name: candy_grind, source: designed, gain_db: -3}
    hook_count: null
    evidence: [strip, still@key]
    risk: "Lurch too small to read in 0.5 s"
    fallback: "Merge into S07's first frames"
    needs: []

  - id: S07
    section: payoff
    kind: gameplay
    cut_in: 228
    cut_out: 315
    tape_in: 468
    speed: [{at: 0, speed: 1.0}, {at: 15, speed: 0.5}]
    subject: {kind: tower, id: crown}
    also_in_frame: [hand, mint, rook, ocean]
    shows: [tower_over_ocean, fall, hand]
    learns: "The whole top topples into the sea"
    feels: awe
    raises: null
    reveal: payoff
    threat: hand
    cause_in: self                  # the hand lifts the block in frame
    size: EWS                       # survivors about 0.07 of frame height; hand about 0.2-0.3
    angle: low
    side: {face: front, yaw_deg: 0}
    camera: {move: static, purpose: "locked-off truth shot of the collapse", ease: none,
             distance_m: 80, height_m: 1.5, focal_mm: 135, aperture: 8, focus: subject,
             params: {pitch_deg: 33, frame_height_m: 14}}
    fit: "Hf = 80 x 24/135 = 14.2 m: crown top 10 m plus the hand above it (3 m) = 13 m fits; the break
          point is in the crown, inside frame; survivors 0.07. Only valid for a topple of the crown: an
          impact lower on the tower needs a new frame."
    key_frame: 12                   # the crown breaks free: on the main hit (frame 240)
    action_cue: {cue_id: crown_breaks, tape_frame: 480}
    frame_at_key: {grid: TC, min_area_pct: 25, max_occlusion_pct: 10, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: cause_effect, screen_direction: R>L, eye_exit: BC, eye_enter: TC}
    sound:
      - {at: 0,  kind: silence, name: music_dropout, source: library, gain_db: -60}
      - {at: 0,  kind: sfx, name: single_creak, source: designed, gain_db: -6}     # onset on the barrage cut
      - {at: 12, kind: music_hit, name: main, source: library, gain_db: 0, sync: main}
      - {at: 12, kind: sfx, name: sub_boom, source: designed, gain_db: -2}
      - {at: 18, kind: sfx, name: blocks_cascade, source: designed, gain_db: -4}  # designed: shot is ramped
    hook_count: null
    evidence: [still@first, still@key, still@last, strip, overlay, audio@key]
    risk: "Hand missing or the crown framed too wide (hand under 5 % of frame)"
    fallback: "none"
    needs: [{feature: hand_drawn_in_playback, status: verify}]

  - id: S08
    section: aftermath
    kind: gameplay
    cut_in: 315
    cut_out: 345
    tape_in: 528
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: actor, id: mint}
    also_in_frame: [ocean_below]
    shows: [survivor_face]
    learns: "She hangs on by one hand"
    feels: relief
    raises: null
    reveal: consequence
    cause_in: prev
    size: MS
    angle: high
    side: {face: 3q_front, yaw_deg: 30}
    camera: {move: static, purpose: "her near miss over the drop", ease: none,
             distance_m: 1.2, height_m: 1.3, focal_mm: 50, aperture: 4, focus: subject, params: {}}
    fit: "Hf = 1.2 x 24/50 = 0.58 m: waist up (MS), the drop below her in the lower third."
    key_frame: 12                   # she catches the edge
    action_cue: {cue_id: mint_grabs_edge, tape_frame: 540}
    frame_at_key: {grid: MC, min_area_pct: 15, max_occlusion_pct: 5, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: eye_trace, screen_direction: down, eye_exit: MC, eye_enter: MC}
    sound:
      - {at: 12, kind: sfx, name: grab_thud, source: take, gain_db: -4}
    hook_count: null
    evidence: [still@key, overlay]
    risk: "No survivor catches an edge in the take (a hang pose is unverified)"
    fallback: "Mint clinging flat to the stump's top block, same lens"
    needs: [{feature: survivor_ledge_hang, status: verify}]

  - id: S09
    section: title
    kind: gameplay
    cut_in: 345
    cut_out: 450
    tape_in: 570
    speed: [{at: 0, speed: 1.0}]
    subject: {kind: tower, id: stump}
    also_in_frame: [hand, ocean]
    shows: []
    learns: "This is Deadly Jenga"
    feels: resolve
    raises: null
    reveal: none
    cause_in: null
    size: WS
    angle: high
    side: {face: 3q_front, yaw_deg: 40}
    camera: {move: crane_up, purpose: "lift away from the wreck under the title", ease: in_out,
             distance_m: 25, height_m: 20, focal_mm: 24, aperture: 8, focus: subject,
             params: {rise_m: 6, over_s: 3.5}}
    fit: "Hf = 25 m at the stump: stump and hand 0.3-0.5, sea around them; title over the sky."
    key_frame: 30                   # the last loose block splashes
    action_cue: {cue_id: last_block_splash, tape_frame: 600}
    frame_at_key: {grid: MC, min_area_pct: 10, max_occlusion_pct: 0, breaks_frame: false}
    transition_in: cut
    connects_from_prev: {type: contrast_size, screen_direction: none, eye_exit: MC, eye_enter: MC}
    sound:
      - {at: 0,  kind: music_hit, name: title, source: library, gain_db: 0, sync: title}
      - {at: 0,  kind: ambience, name: ocean_wind, source: take, gain_db: -14}
      - {at: 30, kind: sfx, name: splash_far, source: designed, gain_db: -8}
    hook_count: null
    evidence: [still@first, still@last, title_safe]
    risk: "Busy picture behind the title"
    fallback: "Burn the titles in with ffmpeg drawtext at assembly; darken the picture 1 stop under them"
    needs: [{feature: export_titles, status: fallback}]   # see studio-tooling.md
```

### Example A against every lint

Body = frames 0-345 (11.5 s). Body-shot lengths (frames): 45, 60, 60, 30, 18, 15, 87, 30.

| Lint | Check | Result |
|------|-------|--------|
| L01 | 0-45-105-165-195-213-228-315-345-450; ends at 450 | pass |
| L02 | longest `learns` 9 words; none has "and" | pass |
| L03 | hook, teach, teach, escalate x3 (stakes, threat, stakes), payoff, aftermath, title | pass |
| L04 | S01 gameplay at frame 0 | pass |
| L05 | hand: S03 shows pull, subject hand, mint in frame, key at 135 = 4.5 s ≤ 5.0 | pass |
| L06 | median (30+45)/2 = 37.5 f = 1.25 s; shortest 15 f; longest 87 f ≥ 60; window [165, 315) holds cuts 165, 195, 213, 228 = 4 | pass |
| L07 | closing title at 345 (11.5 s, 77 %) in [330, 360]; card 105 f = 3.5 s in [90, 120] | pass |
| L08 | staged: first whole/payoff about the hand = S03 at 105; before it S01 partial 45 + S02 reaction 60 = 105 f ≥ 21. Causes: S04 prev = S03 (pull), S06 prev = S05 (grip), S07 self, S08 prev = S07 (payoff) | pass |
| L09 | first pull S01: 45 f, key 22 (22 and 23 from the ends), subject is a block | pass |
| L10 | keys from start / end: 22/23, 30/30, 30/30, 15/15, 9/9, 7/8, 12/75, 12/18, 30/75 | pass |
| L11 | onsets 1, 136, 239, 346 all within 2; S07 key 228 + 12 = 240 = main; title 345 = title hit; syncs at 0, 135, 240, 345 | pass |
| L12 | every key has a sound at the same frame; S02's creak is a `lead_in` | pass |
| L13 | S07 is ramped; its sounds are library or designed | pass |
| L14 | S01 INSERT, S02 MCU; S02 is the hero, 3q_front, yaw 25, MCU | pass |
| L15 | every move in the library with a purpose; `ease: none` only on static and handheld; S03 and S09 `in_out` | pass |
| L16 | no two consecutive shots share a subject | pass |
| L17 | no POV shots | pass |
| L18 | pull S01, hand S03, survivor_face S02, fall S04, tower_over_ocean S07 | pass |
| L19 | `verify` items (hand drawn, ledge hang) go to R01; `fallback` items have workarounds; no `missing`; one hand | pass (S03, S05, S07 BLOCKED if R01 fails) |
| L20 | caption 5 words needs 75 f, has 78; "DEADLY JENGA" needs 48 f, has 105; tagline 6 words needs 84 f, has 90 | pass |
| L21 | hook + teach (45, 60, 60) median 60; escalate (30, 18, 15) median 18 | pass |
| L22 | mean 43.1 f, std 23.1 f, CV 0.54 | pass |
| L23 | longest equal run: S02-S03 (2 shots) | pass |
| L24 | cuts on beats: 45, 105, 165, 195, 315, 345 = 6 of 8 = 75 % | pass |
| L25 | hook_count 1, 2, 3 | pass |
| L26 | pulls and the topple all R>L | pass |
| L27 | TC>MR, MR>BR, TC>MC, BC>MC (new_beat at S05), MC>MC, MC>TC, BC>MC, MC>MC: all distance ≤ 1 | pass |
| L28 | main hit 240 ≥ 0.6 x 345 = 207 | pass |
| L29 | coarse W/EW: S03, S04, S05, S06 (W) and S07 (EW) = 5 of 8 = 62 %; C: S01 = 12.5 % | pass |
| L30 | designed moves: S03 tilt_up, S09 crane_up = 2 of 9 = 22 % | pass |
| L31 | class 15: not required (a 12-frame stop-down exists anyway) | pass |
| L32 | one rule caption, 5 words; tagline 6 words; specific to the game | pass |
| L33 | S05 (18 f) ends at 213: knock at S06 frame 0; S06 (15 f) ends at 228: creak at S07 frame 0 | pass |
| L34 | pull 2 (core verb), hand 3, survivor_face 3, fall 2, grip 1, tower_over_ocean 1 | pass |
| L35 | WS 60, EWS 87, FS 30/18/15, MS 30, MCU 60, INSERT 45 | pass |
| L36, L37 | two-role only | n/a |
| L38 | every shot has a `fit` line | pass |
| L39 | this is the example itself | n/a |

What it still owes before capture: R01 on every shot (the hand must be visible in S03, S05, S07),
R02 on every cue (pulls of at least 2 m), the ledge-hang check for S08, and a person flying the
hand for the record take (or the directed hand from the pilot tooling).
