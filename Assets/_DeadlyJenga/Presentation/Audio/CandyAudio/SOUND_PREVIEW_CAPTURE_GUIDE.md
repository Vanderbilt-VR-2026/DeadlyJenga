# Sound Studio: capture the action that makes the sound

This is the handoff guide for the agent making Sound Studio trigger GIFs. Each preview is a 1–5 second piece of **actual game footage** that helps Evan imagine a replacement sound. It is not a character portrait, concept render, sound demo over a still, or generic disaster montage.

Open **Tools > Candy Audio > Sound Studio**. Choose **Library > Export all capture briefs** for current cue IDs, categories, caller references and per-action framing. Choose **Capture brief** on an individual sound to copy its brief. Treat those briefs as a starting point: read the current call site before filming. Some routes are reserved and never emitted.

## The five questions before recording

Write one plain sentence identifying all five:

1. **Source:** What physical object or process produces the sound? A tentacle, bullet contact, boot, water surface, UI control, or nonphysical score confirmation?
2. **Action:** What changes at the audible instant? Strikes, ruptures, scrapes, surfaces, ignites, accepts a click?
3. **Target:** What receives that action? The tower block, victim body, floor, water, or UI state? Use “environment” or “none” explicitly for ambience/music.
4. **Listener:** Whose version is this? Attacker, victim, nearby observer, local UI user, or ambient listener? Network routes often describe the same action from different listeners.
5. **Audible moment:** Which event/state transition actually emits this cue, and where is that instant in the recording?

Example: “A Kraken tentacle swings down into the tower; the contact breaks blocks; the nearby survivor hears the slam at first contact.” Frame the **arm AND contact region**, with enough of the tower visible to understand the scale. A beautiful wide view of the Kraken standing in the ocean does not explain a slam.

Example: “A projectile strikes the victim's torso; the impact reaction is visible; the attacker receives HitConfirm.” Show the target/contact. A muzzle flash alone demonstrates firing, not the impact. HitConfirm is a nonphysical confirmation layered over that hit; do not invent a second physical sound source.

## Trace meaning rather than guessing from the cue name

Follow Sound Studio's **Source & tools** references to the call site. If it consumes an event, follow that event to its producer and read the nearby condition. For a dedicated AudioSource, inspect its start/stop condition. Confirm the source, target, listener and timing against current code and actual gameplay. Do not add gameplay dependencies to Presentation to obtain a prettier clip.

Current traps worth explicitly checking:

| Route | What to film | What would be wrong |
|---|---|---|
| Weapon Shot | The actual discharge, muzzle flash and visible projectile/recoil; weapon readable | A distant victim-only shot when demonstrating firing |
| Dagger/other melee `Shot` signature | Read equipment action first: often the swing/stab, not gunfire | Assuming all `Shot` names mean bullets |
| Weapon Impact / HitConfirm | Projectile or weapon reaching the victim/material, with contact and reaction | Only showing the firing gun; generic recoil |
| PersonalShot / NearbyShot | **Received projectile damage**, viewed by the victim or nearby observer | Calling these firing sounds; they are emitted by successful damage routing |
| PersonalHit / NearbyHit | Received non-projectile/melee hit and reaction | Unconnected air swing |
| KillConfirm | The actual elimination following a successful hit | Any ordinary hit or a scoreboard still |
| Reload / Draw / Holster | Hands and weapon performing that exact operation | Reusing a generic held-weapon shot |
| Footstep / land / climb | Foot-to-surface or hand-to-edge contact, relevant material readable | Camera motion alone with no contact context |
| UI click / accept / reject | Actual control under pointer and immediate response | Decorative terminal shot without the interaction |
| Music / ocean ambience | An established active environment; describe why this bed is playing | Fabricating a discrete impact at the event marker |

### Kraken phases are different actions

These are current observations from `KrakenPresentation`; recheck after source changes.

| Cue | Required view/action | Current emission condition to verify |
|---|---|---|
| Rise | Body emerges through the waterline, including displaced water | Rise crosses roughly 0.05 |
| Idle | Surfaced Kraken breathing/moving in its resting state | Loop while surfaced |
| Surface | A specific arm breaks the water surface, waterline in frame | Arm enters Resting |
| Reach | Arm extends toward a specific target | Arm enters Reaching |
| Grip | Fingers/tentacle contact and hold a block; target visible | Arm enters Holding |
| Drag | Held target visibly moves with the arm | Arm enters Dragging |
| Windup | Arm draws back before the strike, target still understandable | Arm enters Rearing |
| Slam | Arm hits tower/blocks; show contact and immediate aftermath | Slamming progress/overlap gate |
| Submerge | The arm descends below the waterline | Previously visible arm enters Sinking |
| Retreat | Body sinks away, showing the water closing over it | Remaining lifetime expires after surfacing |

The slam currently uses progress/overlap checks (progress >= .68, or >= .15 with a nearby block/survivor). That may precede visually exact contact. If audio fires early, record the discrepancy with both timestamps and report it as a timing bug. **Never shift footage, fake a cue event or choose a different action to hide the mismatch.**

## Capture recipe

1. Verify checkout path, branch, full source commit and dirty state. Record a patch/snapshot digest if uncommitted source affects the capture. Use your assigned warm editor and current project rules; preserve other agents' work and don't launch screenshot-only editors or seize someone else's editor.
2. Export fresh briefs. Work through one category/state together to avoid repeated loads. Start with Kraken Rise/Slam, projectile hit, melee hit and movement: get these distinctions right before batching.
3. Reproduce through real gameplay. Existing deterministic scenario/theatre controls may stage positioning, but the **actual action and actual cue route must execute**. Label staged recordings `staged-gameplay`. Triggering an audio ID over an unrelated animation is not a valid reproduction.
4. Use a stable nearby gameplay camera with a clear silhouette and contact area. Usually player height or a modest three-quarter view is best. Avoid inside meshes, fogged-out subjects, giant empty wide shots and frantic zooms. Preserve original aspect ratio; never stretch.
5. Record an external MP4 at 1080p and 30/60 fps if practical. Keep approximately 0.3–1 second of anticipation, the action and enough aftermath to understand it. Total final excerpt: **1–5 seconds**. Long actions can show the relevant onset/phase rather than speed up time.
6. Hide developer overlays unless they are the subject. Keep game feedback that helps explain the trigger. Audio may stay in the raw MP4 for timing review; GIF/atlas previews are silent and Sound Studio auditions the selectable sound separately.
7. Use the editor-only played-history watch or a bounded diagnostic trace to identify the emitted cue ID and time. Disable the watch after recording. For dedicated sources without history, document the source transition and timestamp evidence. Do not claim a source trace you did not observe.
8. Inspect the raw recording and final GIF. Check first frame, event frame and final frame. Confirm action is recognizable at 512×288, not just at full-screen resolution. Record source/action/target/listener and relative event time in the context JSON.

For ambience/continuous loops, the event marker is a representative active moment, explicitly described as such. For rare or dormant routes, record `blocked: <reason>` in the batch report. Do not populate their slot with generic footage.

### Reuse rules

Different audio variants of the **same cue/action/listener** share one trigger preview. Two cue IDs may reuse footage only after verifying their source, action, target, listener and timing genuinely match; create separate manifests and explain reuse. Kraken Rise and Slam must never share a generic Kraken shot. Attacker and victim feedback should normally have different camera viewpoints, even when depicting the same hit.

## Make the import package

The studio uses an Editor-only texture atlas, so it does not need a GIF decoder, paid service, network polling or a runtime video player. The helper creates both a looped GIF for human review/upload and its matching atlas for Unity. Only the selected preview loads; animation starts when Watch is pressed and pauses when the window loses focus. Preview files remain outside player builds.

Use the existing FFmpeg installation; do not install packages into Unity. Keep raw footage and packages outside Assets. Example PowerShell (replace every placeholder with observed data):

```powershell
python "<checkout>/Assets/_DeadlyJenga/Presentation/Audio/CandyAudio/Editor/build_trigger_preview.py" `
  --source "<captures>/kraken-slam-raw.mp4" `
  --cue-id <ID_FROM_EXPORTED_BRIEF> `
  --context "<captures>/kraken-slam-context.json" `
  --start 8.2 --duration 3.0 --event 1.1 `
  --output "<captures>/Cue-ID-KrakenSlam"
```

`--event` is seconds **inside the trimmed excerpt**, not the raw recording. The default output is 512×288, 12 fps, maximum 60 frames. If FFmpeg isn't on PATH, add `--ffmpeg "<existing executable>"`. Existing nonempty package folders are never overwritten. The tool checks dimensions/timing/context and hashes bytes; it cannot judge whether a tentacle actually hit something. That review is your responsibility.

Context template (fill it from your capture; mark verified only AFTER reviewing):

```json
{
  "title": "Kraken Slam",
  "sourceCommit": "<full tested commit, plus recorded draft digest if applicable>",
  "scene": "<actual scene>",
  "map": "<actual map and configuration>",
  "mode": "<actual mode>",
  "captureKind": "staged-gameplay",
  "source": "<specific arm/source>",
  "action": "<what visibly happens>",
  "target": "<contact object/location>",
  "listenerRole": "<attacker/victim/nearby/ambient/local UI>",
  "triggerEvidence": "<observed cue ID, trace/report and raw timestamp; note timing discrepancy if any>",
  "semanticReview": "pending",
  "reviewer": "<capturing agent/task identity>",
  "viewerUrl": ""
}
```

Change `semanticReview` to `verified` after source/action/target/timing inspection. This is an accountable attestation, not an automatic semantic test. Keep any mismatch in the handoff and do not mark it accepted until resolved. Technical importer acceptance alone is not creative approval.

The package contains `preview.json`, `preview.gif`, `atlas.png`. Preserve all three. JSON records cue ID, dimensions, duration, event marker, raw source hash, GIF/atlas hashes and provenance. **Library > Import trigger previews** accepts a folder of packages, checks each independently, and reports rejected ones without silently accepting them. Reimporting the same cue replaces its preview while preserving the registry asset GUID. Hash-addressed atlases preserve prior image bytes; old atlases can be reviewed and removed later using Unity asset operations, never shell deletion. No import runs on editor startup.

Verify in the studio: select the cue, Watch/Pause, scrub, **Show event**, and inspect capture provenance under **Source & tools**. Ensure event frame shows the intended moment. A pending slot stays visibly pending until imported. Do not import synthetic test footage as a real cue preview.

## Upload without losing identity or privacy

Evan asked another chat to capture and upload these. Use the **existing private media destination Evan designates**, not the APK folder and not a newly invented public destination. Look for an already authorized project media folder; ask only if the destination cannot be established. Do not make files public or change sharing permissions just to obtain a link.

1. Upload the GIF and a ZIP of its three-file package. Keep the high-quality raw MP4 locally; upload it too when requested or needed for review. Use the connected Drive tools and their large-file route when necessary, not a knowingly size-limited upload.
2. Read back file metadata: exact name, file ID, intended parent folder, byte size/checksum where supported, and private viewer URL. Confirm upload completion and inherited private audience. A queued upload or tool receipt without readback is not delivery.
3. Add the observed GIF viewer URL to `preview.json`'s `viewerUrl`; reimport to attach the **Open uploaded GIF** link in Sound Studio. The local atlas works offline regardless of the remote viewer. Re-zip/update the package if its manifest changes, then record final hashes in the receipt.
4. Deliver representative stills directly plus private GIF/video links, and clearly distinguish captured, reviewed, imported, committed and uploaded. Do not claim phone playback was tested unless someone actually opened it there.

Report each cue as one row: `cue ID | title | source/action/target | listener | source revision | raw file+hash | duration/event time | review result | local package | Unity import result | Drive file IDs/links | blocker`.

## Keeping this current cheaply

- New/changed sound route: update the catalog/trigger explanation, brief semantics when needed, and invalidate/replace its preview if the visible action changed. Stable cue IDs are never renumbered.
- Audio-only replacement: the action GIF remains valid unless timing or trigger meaning changes. No recapture for a volume tweak.
- Geometry/animation changes: recapture only affected views/actions, not the entire catalog.
- On-demand export/import/checks only. No recurring capture agent, polling loop, background transcode or paid API call is required.
- The other chat owns capture/upload. The Sound Studio implementation does not silently start that work or authorize unrelated source changes.

## Ready-to-use handoff prompt

> Read SOUND_PREVIEW_CAPTURE_GUIDE.md and export fresh Sound Studio briefs from the assigned checkout. Capture actual 1–5 second gameplay previews for the agreed cue batch. For each cue, trace the event producer and identify source, action, target, listener and audible moment before filming. Kraken Slam must show arm/tower contact; PersonalShot is victim damage, not firing. Use a clear gameplay camera, inspect raw and GIF event frames, retain exact source and trace evidence, and report dormant/mistimed routes instead of fabricating them. Package with build_trigger_preview.py, import and verify Watch/scrub/Show event, upload GIFs and packages to Evan's authorized existing private media folder with metadata readback, then hand off the per-cue receipt table and representative images. Preserve other chats' editors/work; no broad automations or unrequested gameplay edits.
