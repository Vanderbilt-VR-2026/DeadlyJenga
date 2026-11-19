# Phase 2: Performance passes

Goal: Evan performs every actor himself, one after another, on desktop or in
VR, while everything already recorded plays back around him.

## Features

### Recording a pass
- Pick an actor in the left list, press Record. Evan spawns on that actor's
  mark and controls it with the normal game controls (survivor movement,
  climbing, punching, weapons; or the flying hand).
- **Countdown and pre-roll**: 3-2-1, and an optional pre-roll that plays a
  few seconds of the tape before recording begins so you can get moving.
- Everything else follows its track mode (Phase 1): Playback, Live or Muted.
- Stop records the take. The take is kept even if it is bad.

### Desktop and VR
- Desktop: editor Play Mode with keyboard and mouse.
- VR: editor Play Mode through Quest Link with the real XR rig, so head and
  hand tracking are recorded as-is. Wrist panel from `UI.md`.
- The same take can be started on one and continued (punch-in) on the other.

### Ghosts
- Earlier actors play back as normal characters (default), as transparent
  ghosts, or hidden. Per actor.
- Optional path lines on the floor for each ghost, and for your own previous
  take.
- Ghosts are solid to the live player by default (you can bump them); they
  never react.

### Takes
- Every pass makes a new take. Pick the active take from a dropdown or with
  number keys. Rename, star, delete to trash.
- **Punch-in**: choose a frame (or "back 3 s" in VR), playback runs up to it
  with your old take, then control switches to you and records from there.
  The two parts join with a short blend.
- **Comping**: build the active take from parts of several takes on the
  timeline (take 2 until 3.4 s, then take 5).
- **Slow-motion performing**: perform at ½× or ¼× world speed; the take is
  stored at real speed. Movement code must scale correctly (check jump
  heights and climbing at reduced time scale).
- **Loop record**: keep re-recording the same range automatically, a new take
  each loop, until stopped.

### Filling the crowd (all performed by Evan)
- **Clone a take**: copy a track onto another actor with a position offset,
  rotation, time offset, mirror, and its own outfit. Lets one performance
  become several people.
- Clone with variation: small random time and position jitter so clones do
  not look identical.

### The hand
- **Performed**: Evan flies the hand (VR or desktop hand controls) and its
  pulls go through the real `IGrabbable` path.
- **Automated**: a hand path drawn in the viewport plus "grab block X at
  time T, pull along this arrow", or a hand bot running its normal brain.
- Per Set, choose either.

### Live-physics passes
- A pass can be marked **live physics**. The world track becomes Live during
  it and is re-recorded as a new world take. Use it when this actor causes
  destruction (hand pulls, punches, explosions).
- Tracks recorded against an older world take get a **stale** badge. The
  person can keep them, re-record them, or punch in from where the world
  first differs (the Studio shows that frame).

### Bots
- A bot actor can record a pass using its normal gameplay brain, for
  background life. Evan said he performs all key actors himself, so this is
  a helper, not the main path.

## Expected changes outside DevTools (pre-approved: own commit, log in CROSS-BOX-CHANGES.md)
- Letting a recorded survivor be driven from a pose stream with the motor
  off but colliders kept on (so ghosts can be solid). May need a
  Characters hook, e.g. `IPlayableCharacter`-side or a new Core
  `IPuppetable { void SetPuppet(bool on); void ApplyFrame(...); }`.
- Spawning the local player as a specific actor at a mark, and switching
  which actor the local player controls between passes.
- Recording and replaying the hand: read/write of hand pose and grip state.
  Today `IFlyingState` is read-only. Smallest addition likely a recorder
  interface on the hand, e.g. `IRecordableHand { HandFrame Read(); void Apply(HandFrame f); }`.
- Reduced time scale support in survivor movement, if movement is not
  already time-scale safe.

## Acceptance tests
1. Record actor A on desktop (run, climb, jump). Record actor B while A plays
   back; A matches its take exactly.
2. Record 12 actors in one Set (some cloned). Playback stays at real speed
   in the editor on Evan's PC, or the Studio says it is rendering offline.
3. Punch-in at 2.0 s on a 6 s take; the join is not visible at normal speed.
4. A VR pass in the XR simulator records head and hands. (Headset check is
   separate; say so.)
5. A live-physics hand pull re-records the world; older survivor tracks get
   the stale badge with the right first-difference frame.

## Review stop
Evan records a 3-actor scene himself and watches the playback. He signs off
on how performing feels before Phase 3.
