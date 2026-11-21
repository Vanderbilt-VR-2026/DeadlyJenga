# Performance: actors who read as people playing the game (revision 5)

Open this at stage 3 (when you write each shot's `actors`) and at stage 6b, the performance pass,
after the world take is committed and before any final camera is placed. It is required: SKILL.md
hard rule 19, lints L52-L56, critic category C12.

Numbers here are frames at 30 fps (scale by `fps / 30`). Tags as in SKILL.md: **[sourced]** =
animation and motor-control practice or the owner's direction, **estimate** = nobody has measured
it on this game. Every threshold in a lint is an estimate.

## 1. Why this exists

The owner, 2026-11-20: "I don't want them to be moving robotically. I want them to be using the
controls like a human and constantly doing something humanlike."

What the clips showed (disaster-pvp-1, hand-reveal-2): stand-ins stood frozen on their marks between
scripted reactions; heads stayed dead level; a reaction layer fired, ended, and the body froze again;
nobody walked anywhere under their own power (stand-ins only stand, bots wander off); two or three
witnesses turned on the same frame. A survivor has no drawn face, so the body, head and hands are
the whole performance. If they stop, the survivor is a prop.

The rule: **every visible actor, in every frame of every shot, is doing something a person playing
the game would do, for a reason.** Key events are the minority of frames; aliveness is about all the
others.

## 2. What "alive" means, in numbers you can check

| Term | Definition (estimate) | Limit |
|------|-----------------------|-------|
| **Still** | over the run, the root moves under 5 cm, the head turns under 2°, and each hand moves under 2 cm | longest run per visible actor per shot ≤ 30 frames (L53) |
| **Level-headed** | over the run, head pitch stays within ±3° of horizontal and roll within ±1° | longest run ≤ 30 frames (L53) |
| **In sync** | two actors in one shot start a visible action (turn, step, strike, glance, flinch) within 4 frames of each other | never, except the contact frame of a hit between its attacker and victim (L55, soft) |
| **Teleported** | an actor is somewhere new between two appearances with no travel that the tape recorded | never (L54) |

Write the measured runs in the shot's `actors` entry: `still_f` and `level_f` (the longest run in
frames), with `motion_source` saying where they came from: `plan` (your beat sheet's intent, only
before the take is committed), `take_report` (computed from the per-actor rows of the take report)
or `metrics` (measured in a render). The take report today lists feet positions every 3 frames
(`record-and-layer.md` §3 step 4); add head yaw, pitch and roll and both hand positions per frame,
then count the runs. A run that crosses a cut counts only inside the shot.

A frozen actor in a render can also be a bad tape frame: "One bad frame silently stops an actor"
(`studio-tooling.md` §3). The still-run check catches that too.

## 3. Every actor has an intention

Before any motion, write one line per actor per shot: **where they are going, who they are
watching, or what they fear.** That line is the shot's `doing`. It is a goal, not a pose.

| Weak `doing` (a pose) | Strong `doing` (an intention) |
|-----------------------|-------------------------------|
| "stands near the edge" | "edges away from the gap, eyes on Brick's glove" |
| "reacts to the meteor" | "ducks the meteor, then looks for where it came from" |
| "idle" | "catches her breath, checks over her shoulder for the rival" |
| "looks at the wave" | "freezes on the wave, then looks down for a higher block" |

A good line names a target (a person, a block, the drop, the threat) and, where the survivor is in
danger, a fear. Two actors in one shot never have the same line (L55 warns). A background actor's
line can be its loop (section 9).

## 4. Head leads body

People look before they move. Order every change of direction as: **eyes and head, then torso,
then feet or hands** [sourced: animation practice, "overlapping action"; numbers estimate].

- **The head goes first.** The head starts turning 3-6 frames before the torso and 6-12 before the
  feet step. A hand reaches only after the head is on the target for at least 6 frames.
- **Before a strike,** the attacker's head is on the victim for 12-30 frames (0.4-1 s) before the
  wind-up starts. A punch thrown without looking reads as a machine.
- **Turn speed.** A casual 90° look takes 10-15 frames; a startled snap toward a loud event 5-7
  frames; nothing turns 90° in under 4 frames (an instant turn reads as a glitch). Ease in and out:
  the head accelerates, overshoots 2-5°, settles.
- **Body follows head past the comfortable neck angle.** Up to about 45-60° of yaw the head turns
  alone; past that the shoulders and then the feet come round within 6-15 frames. The Studio's
  look-at stops at the neck's 85° limit (`camera.md` §1c); a target further round needs the body to
  turn, or the look never lands.
- **Snap turning is a game control, not a camera move.** Real VR players snap-turn 30-45° at a time;
  on screen it reads as a pop. Use smooth turning or a turn with head and feet.

Worked example (a witness, Pip, hears a meteor land 8 m behind-left at frame 100):

| Frame | What moves |
|-------|------------|
| 100 | impact |
| 108 | head starts left (she was not watching: 8 frames, section 6) |
| 114 | head at 55°, shoulders start round |
| 120 | head at 80° and settling on the crater, overshoot 3° |
| 124 | left foot steps round; hands come up to guard |
| 140 | she holds on the crater (a 16-frame fixation, head drifting 1-2°) |
| 156 | head flicks right toward Brick (who is she next to?) |

## 5. Anticipation and follow-through

Every big action has a wind-up before and a settle after [sourced: animation practice; frame counts
estimate]. A survivor in VR swings real arms through real controllers, so the arm path is a human arm
path: from the guard, out, and back.

| Action | Anticipation | Action | Follow-through |
|--------|-------------|--------|----------------|
| Punch or shove | head on target 12-30 f; weight shifts back, the arm draws back 4-8 f | 2-4 f to contact | arm extends past contact 3-6 f, comes back to guard over 8-15 f; head stays on the victim to see the result |
| Glove or weapon swing | as a punch, but the draw-back 8-12 f | 3-5 f | the swing carries the torso round 10-20 f |
| Jump | head looks at the landing spot 6-15 f before; a crouch 4-8 f | air time from the game | knees take the landing 4-8 f; the head dips 5-10° and comes up |
| Climb | head looks up at the next hold 6-12 f before each reach | the reach | a pause of 3-10 f with a glance down or across before the next reach |
| Taking a hit | the victim may see it coming (section 6): a flinch or a raised guard 4-10 f before | contact; the knockback is physics | head snaps away from the hit; within 10-20 f after the stagger, the head comes back to find the attacker |
| Being thrown or falling | - | physics | arms and legs reach for anything (a block, the edge); head turns toward the ground or water |

## 6. Glances and reactions

**Glances.** A person holds a look (a fixation) for **0.4-2 s (12-60 frames)**, then moves the
head. Draw each length from that spread so it is **never metronomic**: no two consecutive
fixations of one actor within 4 frames of the same length, and no fixed cycle. During a fixation the
head is not frozen: it drifts 1-3° with breathing and balance. **Never all actors in sync:** stagger
any two actors' glances by at least 5 frames, and give each actor a different set of targets.

Who looks where, by default: at a threat (most of the time when there is one), at the nearest rival,
at their footing near an edge, at their own hands or weapon after a change, over the shoulder.

**Reactions are staggered by distance and attention** [sourced: reaction time is about 0.2-0.3 s
for a seen event and longer when attention is elsewhere; frame counts estimate]:

| Who | Starts reacting after the event | How big |
|-----|---------------------------------|---------|
| Watching it come (it was their fixation target) | before it: a brace or a guard 4-10 f early; a flinch 0-3 f after | smallest: they were ready |
| Looking roughly toward it | 6-9 f | medium |
| Looking elsewhere, close (under 10 m) | 9-15 f (they hear or feel it, then turn: section 4) | biggest: a startle, then the head finds it |
| Looking elsewhere, far (over 20 m) | 15-25 f, or not at all | small; may just glance |
| The victim | 0-2 f (contact) | the physics plus the arms |

Sound travels 1 m in about 0.1 frame, so distance alone adds little under 30 m; attention is what
staggers people.

Worked example: a planet-killer meteor lands on the crown at frame 300.

| Actor | Before | Reaction starts | Shape |
|-------|--------|-----------------|-------|
| Hero, 6 m away, watching the sky | tracked it from 270 | braces at 292 | crouches, hands up; head stays on it |
| Rival, 4 m away, fighting the hero's friend | fixation on his opponent | 311 | startle: shoulders jump, head snaps round at 314-320 |
| Friend, 4 m away, fighting the rival | her fixation was the rival's hands | 308 | turns head first, then backs off |
| Climber, 22 m below, mid-reach | looking up at his next hold | 324 | grips, looks up, holds the look 40 f |

## 7. VR-player tells: a person is in there

These are what separate "a VR player" from "an NPC" [sourced: the owner's direction; observed in
VR gameplay footage; numbers estimate].

- **Hands always up and moving.** Controllers sit at chest height in a loose guard, never hanging
  still at the sides for more than 1 s. Small motion (2-5 cm) even when nothing happens: gripping,
  adjusting, a little flex.
- **Head bob and sway while moving.** Travelling under the stick, the head still bobs 1-3 cm and
  sways side to side, irregularly, and dips a few degrees when the player starts or stops.
- **Looking at their own hands or weapon.** After picking up or switching a weapon, a glance down
  of 30-50° for 0.5-1 s; again after a hit lands, as if checking.
- **Over the shoulder.** Under threat, a look back of 100-150° (the body helping, section 4)
  every 2-5 s, never on a timer.
- **Looking down at the drop.** Near an edge, pitch -40° to -70° for 0.4-1 s before stepping
  closer or away. On a tower game this is the single most "I am up here" tell.
- **Restless feet.** Small steps and weight shifts (5-20 cm) every 1-3 s while standing.
- **Players play.** They taunt (an emote toward a rival), they peek over the edge, they poke at a
  loose block, they back off and come again. Give them that business.

## 8. Travel under their own power

An actor who changes place **got there on camera or off it by moving like a player**: walking,
running, jumping, climbing, being knocked or carried. Never by being placed.

- **Drive travel through the game's controls.** The scripted performer driver (section 12) feeds
  the actor the same inputs a player gives, so the game's own movement code moves it at the game's
  own speeds, with its jumps and climbs. Do not plan speeds; read them from a rehearsal take report:
  `time = distance / measured speed`.
- **Placement is only for frame 0 of the take**, and every shot that uses an actor starts at least
  1 s (30 frames) after that actor started moving, so nobody starts from a dead stand on the cut.
- **Who must be in the world pass:** travel that touches the world or another actor (stepping on a
  block that tips, running into a rival, being in a hazard's path) is driven **in the world pass**
  (record-once rule 17: everything that touches is live together). Travel that touches nothing (a
  witness walking to the edge, a climber on a far face) is driven in a later pass against the
  committed playback, like a performer without Live physics.
- **Between shots** each `actors` entry names a `spot` (the block or stretch the actor is on at the
  shot's first frame, about one block or 6 m). When it changes from the actor's last appearance, the
  entry says `arrived_by`: `driven`, `performed`, `bot`, `physics` (knocked, fell, carried by a
  block or the wave), `start` (an earlier tape time, at the actor's starting mark), or `cut` (the
  move happened off screen and the actor was not in the shot before). L54 fails anything else.
- **No sliding feet.** A driven actor's feet plant; if a motion edit (`Slide`, `Offset`) or a
  layer moves the body, lock the feet (`LockFeet`) or do not do it.

## 9. Background actors: a loop of business

Every background actor gets a simple loop of 2-4 actions, each 1-4 s, with lengths drawn at random
(seeded, so the take repeats) and a different start phase per actor. Examples:

| Loop | Actions |
|------|---------|
| Fight | square up (head on rival) → shove or swing → stagger back → circle |
| Flee | run to cover → peek out at the threat → run again |
| Climb | look up → reach and pull → glance down at the drop → reach |
| Taunt | face a rival → emote → back off with the head still on him |
| Weapon | look down at the weapon → aim at someone → fire or lower it → look round |
| Edge | walk to the edge → look down at the sea → step back → look for the threat |

Two background actors never run the same loop in step. In a crowd made with `StudioTakes.Crowd` or
`Clone`, offset each copy by at least 10 frames and mirror none of them (a mirrored copy is obvious).

## 10. Never

| Never | Why | Lint |
|-------|-----|------|
| A frozen hold: still for over 1 s between reactions | the prop look the owner named | L53 |
| A dead-level head for over 1 s | people look down, up, round | L53 |
| A visible actor with no `doing` | nobody decided what they want | L52 |
| Mirrored or synchronized motion: two actors turning, flinching or stepping on the same frame | crowds of clones | L55 |
| An instant turn (90° in under 4 frames) or a snap turn on camera | a pop | critic C12 |
| Sliding feet | a skating doll | critic C12 |
| Teleport placement between shots | breaks the record-once tape | L54 |
| A reaction layer that ends with the body freezing | the alive layer must take over | L53 |
| A punch thrown without looking | a machine | critic C12 |

## 11. A human performing the actor

**The most natural result is a person performing the actor in the headset** through the Studio's
Perform mode (`record-and-layer.md` §2 and §4a): their head, hands and feet carry every tell in
section 7 for free. Scripted driving and layers get close; a person is better.

When it is worth a person's time (L56 asks you to consider it and waive in writing if not):
- **Every first-person shot.** The performer's head *is* the camera; scripted heads read as a
  camera on a stick.
- **Every hero close-up** (MCU or tighter, and the rule-9 reaction shot): the body is the face.
- **Any shot of 3 s or more** where one survivor is at h ≥ 0.25 and is the subject.

When it is not: background at h under 0.15, crowds, shots under 1 s, an actor whose contact with a
performed actor forces both into one pass (one person cannot perform two marks at once).

How, with a person: perform against the committed playback (the world and everyone before you
plays back around you), at ½ speed for hard moves, punch in (F8) to fix a moment instead of a whole
new take. If the performed actor touches the world or another actor, they must be live with them in
one pass (a driver-built pass; `record-and-layer.md` §4a, untested).

A waiver for L56 names the shot, why no person (none available, contact with another performed
actor, budget) and what carries the actor instead (driven travel, explicit layers, the alive layer).

## 12. The planning order (the performance pass, stage 6b)

1. **Draft beat sheets at rehearsal** for every actor who is live in the world pass (hitters,
   victims, anyone whose travel touches the world): their driven travel goes into that pass.
2. **Commit the world take** (stage 6, `record-and-layer.md` §3).
3. **Write each actor's beat sheet against the committed take**, from its take report and event
   table: one row per action with the tape frame, the intention, the action and how it is achieved.

   | Tape frame | Actor | Intention | Action | How |
   |-----------|-------|-----------|--------|-----|
   | 330-362 | brick | get to Jolt before she climbs | runs 4 m along the walk, head on her | driven: go to |
   | 352 | brick | hit her | head on her since 330; draws back 352-358 | driven: punch |
   | 360 | jolt | - | contact; knocked back | physics |
   | 361-380 | jolt | find who hit her | head snaps away, then back to Brick at 374 | layer + alive |

4. **Drive travel first** (go to, run, jump, climb): travel changes where everyone is, so every look
   and reaction after it aims from the new positions.
5. **Then the explicit reactions**: look-at, reach, reaction and pose layers, timed from the event
   table and staggered by attention (section 6). The attacker's look on the victim, the victim's
   knockback, the witnesses' turns.
6. **The alive layer fills everything else**: glances, weight shifts, hand fidget, breathing, seeded
   per actor (a different seed for each), yielding to any explicit layer while it runs.
7. **Measure**: from the take report (head and hands per frame) or the render's metrics, the longest
   still and level-head run of every visible actor in every shot; write `still_f`, `level_f` and
   `motion_source`; fix any over 30 frames by adding business, not by cutting around it.
8. **Only now place the final cameras** (stage 7), from the actors' positions in the performed tape.

## 13. A worked shot

D's S02 (`worked-examples.md` §1): 60 frames, Brick's strike lands at shot frame 30 (K1).

```yaml
actors:
  - {id: jolt, doing: "backs toward the edge, guard up, eyes on Brick's hands", how: [driven, physics, layer, alive],
     spot: wall_walk, still_f: 6, level_f: 14, acts_at: [5, 31, 48]}
  - {id: brick, doing: "closes on Jolt and throws a hook, watching it land", how: [driven, alive],
     spot: wall_walk, still_f: 4, level_f: 9, acts_at: [10, 22, 30, 41]}
```

Read it: Brick's head is on Jolt from frame 0; he steps in at 10, draws back at 22 (8 frames of
wind-up), lands at 30 and follows her at 41. Jolt shuffles back at 5, is hit at 31 (contact: shared
with Brick's 30, allowed), and her head comes back to find him at 48, 17 frames after the hit. No
two starts within 4 frames except the contact. Neither is still over 6 frames.

## Tool how-to

The tools landed on branch `claude/trailer-hand-reveal-pilot` (commits 7dadeee6 to 0ae74bfb, not merged
yet; check the branch you render from has `Engine/StudioPerformer.cs` and `Engine/StudioAlive.cs`).
Everything is authored in the Set JSON: `set.drives` (driven actors) and `set.alive` (the alive layer).
Proofs and numbers: `Docs/Development/TrailerStudio/attempts/alive-actors/`.

### Driven actor: `set.drives[]` (StudioDrive)
Feeds one Survivor mark a player's inputs during a recording pass, so the game's own movement code
moves the body and the result is recorded onto the take as a layer.

| Field | Default | Meaning |
|---|---|---|
| `actor` | "" | mark id; must be a Survivor mark |
| `muted` | false | true = plain stand-in again |
| `seed` | 1 | seeds the human touches (glances, path bend, overshoot, timing) |
| `human` | 1 | 0 = exact and robotic, 1 = normal person, 2 = sloppy |
| `safeEdges` | true | never walks off an edge unless a step allows it |
| `steps` | [] | list of steps, run in order |

Step fields: `action` (goTo, path, face, look, jump, punch, fire, gear, climb, crouch, stop, wait);
`at` (tape seconds it may start; -1 = when the previous step ends); `duration` (seconds; wait/stop/crouch/look
= how long, the others = give up after); `point` (stage metres); `points` (waypoints for `path`);
`target` (`actor:<id>`, `head:<id>`, `point:x,y,z`, `block:<index>`, `hazard`; punch and fire need one);
`pace` (walk, jog, run, or 0..1); `edge` (true lets this step go over an edge); `amount` (crouch depth 0..1);
`reach` (punch distance in metres, default 1.05); `weapon` (for gear: an EquipmentKind such as BoxingGlove, Blaster).

- `punch` closes in at `pace`, squares up, then swings: the real glove punch with a drawn BoxingGlove.
- `look` with an empty target clears the look.
- Record with `StudioPerformer.Pass(stage, new[]{"hero","rival"}, livePhysics)` then `StudioRun.Record(pass)`.
- **Gap jumps are experimental** (never seen landing in a take). Plan runs that need no gap.

```json
"drives": [{ "actor": "hero", "seed": 7, "human": 1, "safeEdges": true, "steps": [
  { "action": "gear",  "at": 0.3, "weapon": "BoxingGlove" },
  { "action": "look",  "at": 0.75, "target": "actor:rival" },
  { "action": "wait",  "duration": 0.5 },
  { "action": "punch", "target": "actor:rival", "pace": "jog", "reach": 1.0 },
  { "action": "wait",  "duration": 1.4 },
  { "action": "look",  "target": "" } ] }]
```

### Alive layer: `set.alive` (StudioAliveSettings)
On by default for every recorded survivor. Explicit layers win (lookAt takes the head, reach the hands,
pose what it moves, reaction everything). On a driven actor the alive head is off; hands, body and breath stay on.

| Field | Default | Meaning |
|---|---|---|
| `on` | true | false = off for the whole Set |
| `seed` | 0 | added to every actor's seed |
| `strength` | 1 | 0..2, scales everything |
| `head` / `body` / `hands` / `breath` | 1 | glances / weight shifts and small steps / hand states and arm swing / breathing |
| `glanceRange` | 14 | metres; nearer actors get looked at |
| `actors[]` | [] | per actor: `actor`, `off` (false), `strength` (1), `seed` (0) |

Per shot: `"aliveOff": true` on the camera track. Old Sets saved before this load with the layer ON.

```json
"alive": { "on": true, "strength": 1, "glanceRange": 14,
           "actors": [ { "actor": "rival", "off": true }, { "actor": "bea", "strength": 1.5, "seed": 3 } ] }
```

### Also new
- Overlay `target: "point:x,y,z"` (stage metres) works; an unknown target now stops the render.
- A lookAt past 85 degrees turns the torso and feet to follow, so looking behind works.
- First person is unchanged: `behavior: "firstPerson"`, `subject: <mark id>` (see `camera.md`).

### Known weak spots (measured on the proofs; fix or shoot around them)
- **The driven punch shows no arm throw in third person.** The glove stays at the face and the victim just
  flies. Add a `reach` overlay on the punching hand across the contact frame, or shoot the hit in first person.
- **The gloved guard hides the face** on a running actor. Lower it with a pose/reach layer on travel shots.
- **Head turns are too fast**: glances peak near 410 deg/s and a look-behind snaps back near 450 deg/s.
  Give lookAt layers a `blendOut` of 0.4 s or more and lower `head` strength in close-ups.
- **Legs look stiff** on a jog; prefer medium-wide or first person for long runs.
- The still/level-head numbers for L53 now come from the alive-actors metrics style of measurement:
  idle actors measured 1.13 s longest still with the layer on, 2.53 s with it off.
