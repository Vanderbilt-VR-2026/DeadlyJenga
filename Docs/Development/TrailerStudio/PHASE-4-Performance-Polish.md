# Phase 4: Polishing performances

Goal: fix and improve recorded performances without re-performing, and add
the beats normal gameplay doesn't produce.

## Features

### Editing recorded motion
- Select a range on an actor track and: trim, slide in time, retime (speed
  up or slow down that range), smooth (remove jitter), or offset in space.
- Blend between two takes over a range.
- Show the root path in the viewport with time ticks; drag a point to bend
  the path, and nearby frames follow smoothly.
- Fix foot sliding after edits: lock feet to the surface they were standing
  on.

### Layered overrides (on top of a recorded take)
- **Look-at**: head and eyes turn toward a target (actor, block, camera)
  over a range.
- **Reach**: a hand reaches to a point or grabs a ledge, with IK.
- **Pose layer**: crouch, lean, flinch, arm raise, keyed over time and
  blended in and out (Theatre's pose track, made visual).
- Overrides never change the take underneath; they can be muted.

### Reactions
- A reaction library for beats gameplay doesn't make well today: hit
  stagger, knockback, fall, tumble, death, get-up, cheer, scream, cower.
- Each reaction can be dropped on an actor track at a time, triggered by
  a cue (when the tornado reaches them), or by contact (when a recorded block
  hits them).
- **Physics reactions**: a ragdoll or partial ragdoll on the candy avatar that
  blends back to the performance. If the avatar has no ragdoll, build one as
  an editor-time setup in DevTools, applied only in the Studio.
- **Stunt clips**: `TheatreStuntImport` stays the way to bring in mocap or
  Blender animation. Show stunt clips in a library and drop them onto a
  track like a reaction.

### Expressive extras
- Emotes and voice lines from the game, placed on a track.
- Equipment swaps, outfit changes or color flashes at a time.
- Hit pause: freeze a contact for a few frames (applied to playback only).

### Crowds
- Turn one performed take into a group: duplicate with offsets, mirroring,
  time jitter and outfit randomizing, with a preview of the whole group.
- Avoid clones overlapping (a simple spacing check that warns or nudges).

## Expected changes outside DevTools (pre-approved: own commit, log in CROSS-BOX-CHANGES.md)
- Applying a full-body pose override and look-at on the avatar from outside
  Characters. `CandyPlayerAvatar.OverridePose` already exists; check whether
  it covers what's needed before adding more.
- Ragdoll setup on the survivor avatar, if it must live in the Characters
  prefab instead of being added at runtime by DevTools.

## Acceptance tests
1. Retime 1 s of a run to half speed; feet don't slide.
2. A recorded block hits an actor and triggers a knockback that blends back
   into the take.
3. A look-at override on 5 actors toward the incoming tornado.
4. A 20-person crowd from 3 performed takes, with no two clones visibly in
   sync.

## Review stop
Evan reviews a crowd escape with reactions and signs off on how bodies look.
