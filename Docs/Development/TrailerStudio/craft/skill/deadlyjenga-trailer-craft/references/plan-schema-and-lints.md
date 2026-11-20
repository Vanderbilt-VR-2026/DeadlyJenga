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

Checking by hand is error-prone (all four test planners said so). **Run `tools/lint.py plan.yaml`**
(revision 3): it computes every L-lint below from the plan and prints PASS, FAIL, WAIVED or N/A
with the numbers. It cannot tell whether a `shows` label, `learns` line, brief `proof` or `fit`
line is true of the shot; you and the critic still check that. A plan written as a table in
`plan.md` can be copied into YAML for the run.

## 1. Fields

### Header

| Field | Type | Values / notes |
|-------|------|----------------|
| `id`, `version` | string, int | one folder per version: `v01/`, `v02/` ... (planning-only mode may be one file) |
| `mode` | enum | `production` (default) or `planning_only` (no music file or no engine; see SKILL.md) |
| `concept_type` | enum | `mayhem` (default: disasters plus PvP), `disaster`, `pvp`; legacy `hand`, `two_role` (`legacy-hand.md`). Sets the core verb (L05), the first hold (L09), what BLOCKS (L19) and the mayhem lints (L40). |
| `opening` | enum | `staged` (default) or `cold_open`; with `cold_open`, `cold_open_threat` names the threat shown at frame 0 |
| `threats` | list | threat ids: `hazard:<name>` (e.g. `hazard:tsunami`), `hand` (legacy). A rival survivor is not a threat id: PvP hits are tagged per shot with `attacker` and `victim` |
| `fps`, `length_frames` | int | fps 30 or 60 |
| `class` | enum | `moment` (about 5-10 s: no title card needed, the whole piece is the body, payoff at 50 % or later), `15`, `30`, `60`: picks the numeric limits (a 20 s cut uses `15` limits scaled by length; say so in a waiver) |
| `aspect` | enum | `16:9`, `9:16`, `1:1` |
| `shape` | enum | `montage` or `single_take` |
| `hook` | string | the one thing only this game has |
| `hero` | actor id | the survivor the audience follows; never an example's id (L39) |
| `cast` | list of actor ids | every performed or bot survivor named in shots |
| `must_show` | list of ids | copied from the brief, one id per thing it asks for; each id has a one-line `proof` in `brief.yaml` saying what in frame proves it (L18) |
| `capture` | object | `survivors`: who is performed, who is a live bot, who is a recorded stand-in; `hand`: `performed` / `directed` / `bot` / `none`; `take`: committed bundle path |
| `camera_sensor_mm` | float | 24 (all distances in `camera.md` assume it; the Studio's default is 18.66, so set each track's `sensorHeight`) |
| `music` | object | `file` (null in planning-only mode), `bpm`, `meter`, `downbeat_frame`, `sections` [{name, from_frame, to_frame}], `hits` [{name, role: `open` / `reveal` / `main` / `title` / `accent`, frame, bar_beat, measured_onset_frame (a frame, or `unmeasured`)}], `stopdowns` [{from, to}] |
| `titles` | list | {text, `role`: `title` / `tagline` / `cta` / `caption`, frame_in, frame_out, style: `card` / `over_picture`, position, closing: bool}. Exactly one entry has `closing: true` (role `title`). A `caption` is in-body text over gameplay; `tagline` and `cta` belong to the end card. |
| `hazard_set` | object or list | each disaster's cue (`hazard` or `tsunami`), its settings in the disaster's own order, its tape time, `aimed`/heading where the Studio allows it (`game-content.md` section 9) |
| `example` | bool | true only in the skill's worked examples (L39 skips them) |
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
| `also_in_frame` | list | ids; actor ids here count as survivors in frame; `hazard:<name>` when the hazard is visible |
| `attacker`, `victim` | actor or hazard id | required when `shows` has `pvp_hit`, `knock_off` or `elimination` (L41): who did it, to whom. A hazard can be the attacker (`hazard:tornado`) |
| `tower_level` | enum | `top`, `upper`, `mid`, `base`, `sea`, `air`, `island`: where on the structure the shot is (L44, geography) |
| `readable_survivors` | int | survivors at h ≥ 0.08 at the key frame, from the `fit` line (L45) |
| `reaction` | string | for the hero's reaction shot: the Studio reaction layer, emote or performed pose that carries it (L14). Avatars have no drawn eyes or mouth |
| `shows` | list | every must-show id this shot truly proves (do not drop a true label to pass L34) |
| `learns` | string | ≤ 12 words, one idea, no " and " and no ";" |
| `feels` | string | optional: target emotion, one word |
| `raises` | enum | required in `escalate`: `stakes`, `scale`, `threat`, `count`, `speed`, `novelty` |
| `reveal` | enum | `none`, `knows` (audience sees a threat the character does not), `effect`, `partial`, `reaction`, `whole`, `consequence`, `payoff` |
| `threat` | threat id | which threat a `knows`/`effect`/`partial`/`reaction`/`whole`/`payoff` tag is about; may be omitted when `threats` has one entry |
| `cause_in` | enum | `self` or `prev`; required when `reveal` is `consequence` or `payoff` |
| `role_view` | enum | `survivor`, `neutral` (default), or legacy `hand`: whose side of the game the shot puts us on. Two-role cuts need both (L36). |
| `same_event` | string | optional: tags shots that show one event from different sides (L37) |
| `size` | enum | `EWS` < `WS` < `FS` < `MS` < `MCU` < `CU` < `ECU`, plus `INSERT` (counts as close). **Third-person with a cast survivor in frame:** size follows the nearest survivor (table in `camera.md`: EWS h ≤ 0.08, WS 0.20-0.45, FS 0.70-0.90; MS / MCU / CU by frame height). A survivor at h 0.08-0.20 is WS. **Otherwise, and for every POV shot:** judge by the subject's height fraction `h`: EWS < 0.1, WS 0.1-0.45, FS 0.45-0.9 (whole subject in frame), MS / MCU / CU when cropped to 50-70 / 35-50 / under 35 % of it; `INSERT` when a part of an object (a block end, fingertips) fills ≥ 50 % of frame height. |
| `angle` | enum | `eye`, `low`, `high`, `overhead`, `ground`, `dutch` (for a POV, the class of its pitch) |
| `side` | {face, yaw_deg} | face: `front`, `3q_front`, `profile`, `3q_back`, `back`; yaw 0 = facing camera. POV shots: `null`, `null`. |
| `camera` | object | `move` (library below), `purpose` (string), `ease` (`in_out`, `in`, `out`, `damped`, `none`), `distance_m` to subject, `height_m` above the subject's standing surface (POV: eye height), `focal_mm`, `aperture`, `focus` (`subject` or metres), `params` (move-specific), `pov_smoothing` {pos_s, rot_s} for POV |
| `fit` | string | **the framing check** (L38): frame height at the subject `Hf = distance x 24 / focal_mm`, the subject's fraction of it, and why the action point (impact, block travel, face, both threats) fits. Derived for this shot, never copied. |
| `key_frame` | shot frame | where the action peaks |
| `moments` | list | single-take only: [{at, section, reveal, threat, raises, cause_in, shows, attacker, victim, what, sound_at, hook_count}] treated as beats by L03, L05, L08, L09, L12, L40-L46 (`what` follows the `learns` rules; the take's `also_in_frame` counts for every moment) |
| `action_cue` | object | `cue_id`, `tape_frame`, `expect` (e.g. `block_travel_m: 2.0`) |
| `frame_at_key` | object | `grid` (`TL` ... `BR`), `min_area_pct`, `max_occlusion_pct`, `breaks_frame` (bool) |
| `transition_in` | enum | `cut`, `match_cut`, `smash_cut`, `whip`, `dissolve` (needs a waiver), `cut_to_black` |
| `connects_from_prev` | object | `type` (`new_beat`, `cause_effect`, `reaction`, `eye_trace`, `match_motion`, `contrast_size`), `screen_direction` (`L>R`, `R>L`, `up`, `down`, `toward`, `away`, `none`), `eye_enter` (grid cell where the eye lands in this shot), `eye_exit` (where it leaves this shot) |
| `sound` | list | [{at (shot frame), kind: `music_hit` / `sfx` / `ambience` / `silence` / `vo`, name, source: `take` / `library` / `designed`, gain_db, sync (hit name, optional), `lead_in` (bool: a sound that deliberately starts before what it announces)}] |
| `hook_count` | int or null | which featured appearance of the hook this is (see L25) |
| `jump_cut` | enum | `deliberate` to waive L16 |
| `evidence` | list | optional: what the critic must see: `still@key`, `strip`, `overlay`, `flow`, `audio@key` |
| `risk`, `fallback` | string | what may fail, what to shoot instead; `fallback: "none"` means BLOCKED if it fails |
| `needs` | list | [{feature, status: `available` / `verify` / `fallback` / `missing`}]; every capability the shot relies on that SKILL.md's can/cannot table or `game-content.md` marks unknown or unconfirmed is listed here as `verify` |

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
- **Core verb** (by `concept_type`): `mayhem`: whichever comes first of `hazard_hit` and `pvp_hit`
  (L40 then needs both). `disaster`: `hazard_hit` (the hazard visibly moves blocks or survivors).
  `pvp`: `pvp_hit` (one survivor visibly strikes, shoots or shoves another and the victim moves or
  falls, both in frame). Legacy `hand` and `two_role`: `pull` (`legacy-hand.md`).
- **PvP ids**: `pvp_hit` (a hit that lands and moves the victim), `knock_off` (the victim leaves the
  structure because of a named attacker, survivor or hazard), `elimination` (the victim is out: the
  candy burst or the sea), `weapon` (a weapon fired or swung with its effect in frame). Label each
  truthfully: a rocket that only blows out a block is `weapon`, not `pvp_hit`.
- **Causes** (L08 `cause_in: prev`): the previous beat shows `hazard_hit`, `pvp_hit`, `knock_off`,
  `weapon`, `pull` or `grip`, or is a `whole` or `payoff` reveal.
- **Anticipation reveals**: `knows`, `effect`, `partial`, `reaction`.

## 2. Lints

### Plan lints (no Unity)

| ID | Sev | Rule | How it is computed | Basis |
|----|-----|------|--------------------|-------|
| L01 | hard | Timeline exact | all frames integers; shot n `cut_out` = n+1 `cut_in`; first `cut_in` = 0; last `cut_out` = `length_frames` | bookkeeping |
| L02 | hard | One subject, one idea | exactly one `subject`; `learns` (and each moment's `what`) ≤ 12 words, contains no " and " or ";" | [sourced] Lieu, clarity |
| L03 | hard | Structure | the list of sections (shots, or `moments` in a single take) matches `hook+ teach+ (escalate|breath)+ payoff+ (aftermath|breath)* title+` with at least one `escalate` before the first `payoff`; class `moment`: `hook+ teach* (escalate|breath)* payoff+ (aftermath|breath)* title*`; every `escalate` has `raises` | [sourced] Lieu, Cauller; moment pattern from hand-reveal-2 |
| L04 | hard | Gameplay first | first shot `kind: gameplay`; min `cut_in` of gameplay shots ≤ 60 frames (2.0 s) | [measured] 13 of 21 |
| L05 | hard | Core verb early | the first shot or moment whose `shows` has the core verb, with its scale reference (`hazard_hit`: the structure or a cast actor in frame; `pvp_hit`: attacker and victim both in frame; `pull`: a block and a cast actor): `(cut_in + key_frame) / fps` (or the moment's `at`) ≤ 5.0 s (moment, 15), 8.0 s (30), 12.0 s (60) | [measured] mid median 4.3 s, party/physics 3.7 s; 15 and 30 extrapolated |
| L06 | hard | Shape | `montage`: median body-shot length 30-66 frames (1.0-2.2 s); shortest ≤ 30 frames; longest ≥ 60 (15) / 90 (30) / 99 (60) frames; some window `[a, a+150)` inside the body holds ≥ 4 cuts. `single_take`: one body shot ≥ 70 % of the body; ≤ 2 cuts inside the body. Class `moment` montage: median 24-75 frames, longest ≥ 45, no barrage needed | [measured] 16 of 18; floors for 15 and 30 extrapolated; moment numbers estimated |
| L07 | hard | Title timing | closing `frame_in` in [330, 360] (15), [720, 780] (30), [1590, 1650] (60); `length_frames - frame_in` in [90, 120] / [120, 180] / [150, 210]; no other title with the game name before it unless `style: over_picture` on a gameplay shot. Class `moment`: no closing title needed; if one is used it starts after the payoff's key | [measured] 18 of 22 at 80-95 %, 15 of 22 cards 4-8 s; the 15 s window trades card length for position, extrapolated |
| L08 | hard | Staged reveal or cold open | for each threat T: let R = the first shot or moment with `reveal` in {`whole`, `payoff`} about T. **Staged** (R after frame 0): the summed length of earlier shots (or moment spans) with an anticipation reveal about T ≥ 21 frames (0.7 s). **Cold open** (R at frame 0, `opening: cold_open`, `cold_open_threat: T`): R is uncut for ≥ 30 frames, and the body has ≥ 2 `escalate` beats with `reveal: consequence` whose `raises` values differ. A second threat first shown after frame 0 always needs its own anticipation. Every `consequence` or `payoff` has `cause_in`; if `prev`, the previous beat shows a cause (derived terms) or has `reveal` `whole` or `payoff` | [sourced] Hitchcock, Jaws, Lieu cause-effect; skill test 1 (frame-0 wave scored best on hook); 0.7 s and 1.0 s inferred |
| L09 | hard | Hold the first core verb | the first shot (or moment) whose `shows` has the core verb: length ≥ 45 frames; `key_frame` ≥ 12 and `length - key_frame` ≥ 12 (a moment: ≥ 12 frames from the take's ends and from any cut); a scale reference in frame (`hazard_hit`: the structure or an actor; `pvp_hit`: attacker and victim both in frame; `pull`: a block or an actor) | [sourced] Lieu (hold new mechanics); 1.5 s inferred; attempt 1 F17 |
| L10 | hard | Action inside the shot | `key_frame` ≥ 6 and `length - key_frame` ≥ 6; exception: `key_frame` ≤ 4 when `cut_in` is within 2 frames of a hit (impact on the cut) | [inferred], attempt 1 F17 |
| L11 | hard | Hits and sync | every hit has `measured_onset_frame` within 2 frames of `frame` (**deferred** in planning-only mode, where it reads `unmeasured`); the first `payoff` shot's `cut_in + key_frame` (or the payoff moment) within 2 frames of the `main` hit; closing `frame_in` within 2 frames of the `title` hit (n/a for a `moment` with no title); every sound with `sync: X` sits within 2 frames of hit X (absolute frame `cut_in + at`) | [sourced] Lieu, Violet Recording; [measured] lean to hits |
| L12 | hard | Sound at every key moment | each shot (and moment) has a sound with `key_frame - 1` ≤ `at` ≤ `key_frame + 3`. Other sounds may sit anywhere: a `lead_in` (a creak or roar 0.5-1.5 s before a threat appears) is not the key's sound and is not bound by the 1-frame rule | [sourced] Lieu; ITU-R BT.1359 |
| L13 | hard | Ramps carry no take sound | any shot with a `speed` ≠ 1 has no sound with `source: take`, unless `studio.ramp_sound: true` | [sourced] attempt 1 F4 |
| L14 | hard | A close shot and a reaction that reads | some shot has size in {MCU, CU, ECU, INSERT}; some shot has subject = `hero`, `side.face` in {`front`, `3q_front`}, abs(`yaw_deg`) ≤ 60, size in {MS, MCU, CU, ECU}, and a `reaction` naming the layer, emote or performed pose that carries it (the avatars have no drawn eyes or mouth: the read is head direction, body and hands; the Studio's `scream`, `cower`, `cheer`, `knockback` layers are the stand-ins) | [measured] + [sourced]; 60° inferred; hand-reveal-2 (no face to read) |
| L15 | hard | Camera declared | `camera.move` in the library; `purpose` non-empty; `ease` is not `none` unless the move is `static`, `whip`, `crash_zoom`, `handheld` or `pov` | [sourced] Lieu, Nesky |
| L16 | hard | No jump cut | for consecutive shots with the same subject, neither a POV: fail if size, angle and abs(yaw difference) < 30° all match, unless `jump_cut: deliberate` | [sourced] 30-degree rule |
| L17 | hard | No raw VR view | `move: pov` has `pov_smoothing.pos_s` in [0.2, 0.4] and `rot_s` in [0.3, 0.6]; no `raw_hmd: true` anywhere | [sourced] Gartner, Lieu, Road to VR |
| L18 | hard | Must-show covered | every `must_show` id is in some shot's `shows`, and the brief's `proof` line for it is true of that shot (no proof by implication: "multiplayer" needs players on screen) | brief; skill test 1 (multiplayer only implied) |
| L19 | hard | Features exist | every `needs` entry has status `available`, `verify` (R01 or R02 must confirm before capture), or `fallback` (and `fallback` text says what is shot instead). `missing` fails, and so does any shot that needs something SKILL.md or `game-content.md` lists as "cannot" or "missing" marked `available` (a punch cue, mode objectives in the Studio, a sea drawback, a drawn face). A `verify` item with `fallback: "none"` that fails in the scout makes the plan BLOCKED: for `disaster` and `mayhem`, the hazard not reaching or not being visible; for `pvp` and `mayhem`, no hit landing on a victim in the scout; for legacy `hand`, the hand not drawn | [sourced] attempt 1 F5, F6; skill test 1 (a shot with two hands) |
| L36 | hard | Both roles are players | `two_role` only: some shot with `role_view: hand` and some with `role_view: survivor`, both with `cut_in` < half the body | [sourced] Evolve, Dead by Daylight; brief d; half inferred |
| L38 | hard | Framing derived | every gameplay shot has a `fit` line with `Hf = distance x 24 / focal` at its subject, the subject's fraction (matching `size`), and its action point inside the frame (impact point, the victim's knockback path, block travel ≥ 15 % of width for a pull, both threats if both are claimed) and nothing the game cannot do | [sourced] skill test 1: critics found copied frames that could not contain their action; formula from `camera.md` |
| L39 | hard | No example reuse | no tagline, caption, hero id or cast id from the worked examples A-E (list in `worked-examples.md` section 0). Soft part: a shot whose (distance, height, focal) equals an example shot's warns; its `fit` line must show the derivation | [sourced] skill test 1: 4 of 4 plans cloned the example |
| L20 | soft | Titles readable | each title, tagline and caption on screen ≥ `fps * (0.3 * words + 1.0)` frames | [inferred] |
| L21 | soft | Build shortens | montage only: median length of `escalate` shots ≤ median of `hook` + `teach` shots. Payoff, aftermath, breath and title shots are not counted, so a held payoff is never penalised | [measured] tendency 12 of 19 (by time quarters); section form inferred |
| L22 | soft | Varied lengths | montage only: CV (population std / mean) of body-shot lengths ≥ 0.35 | [inferred] |
| L23 | soft | No even run | montage only: no more than 3 consecutive body shots within 2 frames of each other's length | [sourced] Lieu |
| L24 | soft | Not gridded | montage only: share of cuts within 1 frame of a beat (`downbeat_frame + k * fps * 60 / bpm`) ≤ 80 % | [sourced] Lieu; [measured] |
| L25 | soft | Rule of threes | max `hook_count` is 2 or 3. `hook_count` counts featured appearances of the hook (shots built around it), not every shot where the hand or hazard is visible | [sourced] Lieu |
| L26 | soft | Screen direction | consecutive shots of one movement (pull, fall, wave) share `screen_direction` unless `connects_from_prev.type` is `new_beat` or `contrast_size` | [sourced] continuity practice |
| L27 | soft | Eye trace | `eye_exit` of n and `eye_enter` of n+1 are the same or touching cells (Chebyshev distance ≤ 1), unless `smash_cut` or `new_beat` | [sourced] Murch |
| L28 | soft | Peak late | `main` hit frame ≥ 0.6 x body length (class `moment`: ≥ 0.5) | [inferred] |
| L29 | soft | Sizes | coarse W + EW ≥ 40 % of body shots; coarse C ≤ 25 % (15) or 15 % (30, 60) | [measured] |
| L30 | soft | Moves | montage only: designed moves on 10-30 % of all shots | [measured] median 11 %, reveals 21 % |
| L31 | soft | Stop-down | class 30 or 60: at least one `music.stopdowns` entry ≥ 9 frames. One drop-out just before the main hit satisfies it; a `breath` section is optional and separate | [measured] 14 of 22 |
| L32 | soft | Text | captions (`role: caption`): ≤ 6 words, state a rule or fact of play, default count 1 for store and muted social cuts (otherwise 0-1 / 0-2 / 0-4 by class). Tagline ≤ 8 words, CTA ≤ 6 words, end card only. Everywhere: no superlatives ("epic", "insane", "ultimate"); the line would be false for another game | [sourced] Lieu; [measured] 11 of 22 use captions; skill test 1 (rule captions scored better on clarity); word counts inferred |
| L33 | soft | Barrage on hits | montage only: every cut that ends a body shot shorter than 24 frames (0.8 s) has a sound with `at` 0-2 in the next shot, or a hit within 2 frames | [measured] 12.8 % vs 6.7 % chance |
| L34 | soft | Idea count | no `shows` id other than the core verb appears in more than 3 shots. The core verb may repeat when each repeat after the third has a different `raises` | [sourced] Lieu |
| L35 | soft | Readable length | EWS, WS ≥ 15 frames; FS, MS, MCU ≥ 12; CU, ECU, INSERT ≥ 9; a POV ≥ 30 unless it repeats a POV already held ≥ 30 | [inferred] |
| L37 | soft | One event, both sides | `two_role` only: two shots with the same `same_event`, one `role_view: hand` and one `survivor`, adjacent or one shot apart | [sourced] Evolve; recipe in `reveals-and-moments.md` |

### Mayhem lints (revision 3)

Grounding: readable chaos is sourced practice (report 01 rule 40: "a viewer can say what caused
what"; rule 20: one focal point; Salters' "soupiness"; Lieu on party trailers). Every number in
these rows is an estimate until a mayhem cut is measured.

| ID | Sev | Rule | How it is computed | Basis |
|----|-----|------|--------------------|-------|
| L40 | hard | Both halves of mayhem | `mayhem` only: the first `hazard_hit` and the first `pvp_hit` both have their key ≤ 0.6 x the body | the owner's direction (2026-11-20); 0.6 estimated |
| L41 | hard | Who did what to whom | every beat whose `shows` has `pvp_hit`, `knock_off` or `elimination` names `attacker` and `victim`; the victim is the subject or in `also_in_frame`; the attacker is too, or is in the previous beat with `cause_in: prev`, or is a hazard with `cause_in: self` | [sourced] report 01 rules 20, 40 |
| L42 | hard | A hero to follow | the `hero` is the subject or in `also_in_frame` in some hook or teach beat, some escalate beat (if any) and some payoff or aftermath beat | [sourced] Lieu (one hero), report 04 rule 15 |
| L43 | soft | Disasters escalate | with two or more hazards: each first appears later than the one before, in a distinct beat, and every hazard after the first arrives in an `escalate` or `payoff` beat | [sourced] Lieu, Cauller (one new layer per beat) |
| L44 | soft | Geography | some EWS or WS shot in the first third of the body holds the structure and the hero (where she is on it); every gameplay shot has `tower_level` | [sourced] continuity practice; first third estimated |
| L45 | soft | Several readable at once | every shot with `multiplayer` or `several_survivors` has `readable_survivors` ≥ 3 (survivors at h ≥ 0.08, the EWS line) | [measured] EWS line from `camera.md`; 3 from the `multiplayer` proof |
| L46 | soft | Knock-offs read | a beat with `knock_off` or `elimination` holds ≥ 18 frames (0.6 s) after its key, or the next beat (`cause_in: prev`) keeps the victim in frame | [inferred] estimate; attempt 1 F17 (an unreadable result) |

(L36-L39 are from revision 2; L40-L46 from revision 3. Montage-only soft lints (L21-L24, L30, L33)
and L25 do not apply to class `moment`.)

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

R02 for PvP: a hit cue (`fire`, a performed punch) lands within 8 frames of plan and moves its
victim at least as far as the plan's `expect`; a hazard reaches the planned survivors. R03 for
voices: an onset check on the full mix misreads soft first syllables (hand-reveal-2: +17.3 frames on
a line that was in sync), so measure each sound lane alone (`tools/assemble.py` does) and mark every
voice line "needs ears" for R07.

In planning-only mode every R-lint is **deferred**: list them under "before any render".

## 3. Worked examples

The examples moved in revision 3. Disaster-PvP examples D (15 s montage) and E (30 s single take,
planning-only) and the disaster example B are in `worked-examples.md`; the legacy hand examples A
(15 s hand reveal, formerly here) and C (30 s two-role) are in `legacy-hand.md`, their hand numbers
re-derived for the 2.3 m drawn hand. All five pass `tools/lint.py` with no hard failure.
