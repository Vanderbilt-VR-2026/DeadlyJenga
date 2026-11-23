# Directing Deadly Jenga from a prompt

The product target is a creator entering an ordinary brief, watching a playable cut, and asking for a precise change such as “hold on the rival until they regain their footing.” The creator should be able to keep the performance, change who the camera watches, and compare both versions immediately.

## Use the implemented path

In Unity open **Tools → Deadly Jenga → Gameplay Theatre → Direct a scene from a prompt**, enter the direction and select **Stage editable recipe**. Read the choices and remaining direction, then use the recipe controls and **Draft render**. Staging uses local Python 3 and the checked-out game catalogs. Native rendering requires Unity and FFmpeg. Each submitted direction gets its own folder under `Logs/GameplayTheatre/Directions`.

For the identical staging logic outside the editor, run:

```powershell
py -3 -B Docs/Development/TrailerStudio/tools/direct_scene.py prompt.txt new-scout-folder
```

`brief.json` retains the original request. `recipe.json` is a native Gameplay Theatre input. `staging.json` lists choices, attempted cues and outstanding direction. The accompanying [worked prompt](examples/castle-contact/prompt.txt) and [recipe](examples/castle-contact/recipe.json) are reproducible starting points. The engine's existing render and reframe workflow is documented in [Gameplay Theatre](../../../Assets/_DeadlyJenga/DevTools/Editor/GameplayTheatre.md).

After rendering, run `py -3 -B Docs/Development/TrailerStudio/tools/review_scene.py new-scout-folder TAKE_FOLDER --output review.json`. The [render audit](tools/review_scene.py) checks the actual encoded media, recipe identity, cue outcomes and required target contact. A technical pass sends the movie to creative review. The first Castle example on revision `b006f20f` illustrates the distinction: its five-second edit encoded successfully, but the game rejected the punch and the rival lost zero health.

For more open-ended language, [the optional semantic director](tools/local_director.py) accepts the same prompt text through an explicitly selected locally installed Ollama model. Run `py -3 -B Docs/Development/TrailerStudio/tools/local_director.py prompt.txt new-model-scout --model YOUR_LOCAL_MODEL`. The model proposes actors, moves, cameras, edits and unresolved clauses against a constrained schema. The program validates the response, then writes an ordinary recipe for **Open recipe**. Model quality and runtime depend on the selected weights and machine. [Ollama structured outputs](https://docs.ollama.com/capabilities/structured-outputs)

Add `--refine` to request bounded lens and position changes to an existing deterministic scout. This limits the cost of an incorrect model answer. In a one-thread CPU trial through llama.cpp, Qwen2.5-0.5B copied absolute positions into offset fields and missed the requested lens. The validator rejected the patch. A full-recipe trial with the same weights repeated a shot until it exhausted the output budget. Keep live model quality as a separate benchmark from schema validation.

For matched POV and witness captures, the [camera edit tool](attempts/disaster-pvp-2/tools/cut_take.py) produces a playable MP4, an editable OpenTimelineIO sequence and both source angles plus continuous production sound. Cuts use shot-relative frames. Audio uses the take-relative origin recorded at capture. Install the free Python package `opentimelineio` in an isolated environment. The exporter reopens the timeline, resolves its media and checks the encoded frame count.

## What the earlier attempts established

| Observation | Design consequence |
|---|---|
| In the rooftop rehearsal the first approach reached 4.60 m/s, the Blaster fired in the air and the survivor landed. The actor later ended 13.34 m below the intended route. | Score approach, action, landing and continued travel separately. A logged shot cannot certify the escape. |
| Close POV showed the weapon and lost geography. A distant witness included the geometry and made the rival hard to see. | Record simultaneous coverage. Test what viewers understand at normal playback speed, including the aftermath. |
| The published craft research counted 22 trailers with one rater and few short examples. | Use its timings to propose alternatives. Let matched playbacks and viewers determine pacing for this game. [Research reconciliation](craft/RECONCILIATION.md) |
| The historical current-match capture was an automated fixture. The four tracked attempt bundles have no verified human operator. | Collect a matched human take before labeling any performance human-equivalent. |
| Unseen phrasing changed actor ownership, confused shot times with runtime and silently changed location. | Preserve original clauses, explicit exclusions, actor roles and timestamps. Verify them again on the actual render. |

## Implementation and acceptance sequence

| Workstream | Concrete implementation | Evidence for advancing |
|---|---|---|
| 1. Interpret the brief | Expand from the current offline vocabulary to a typed sequence of actor, action, target, relationship, time window, camera, audio and prohibition. Preserve repeated actions and camera returns. Expose each clause as an editable row with its implementing track or an unresolved reason. Allow an author or locally running model to propose this same data. Validate all IDs, timing, capability and costs outside the model. | Fresh prompts with paraphrases, reordered clauses, negation, contradictory requests, unusual map names and partial revisions. Compare requested obligations to the compiled recipe. Record false promises and lost clauses per prompt. |
| 2. Direct the performers | Capture a human playing the hero at normal speed. Keep the real take and record a rival against its replay. Use nearby support, reachable marks and sightlines to rehearse alternative paths. Add head, hands, equipment and recovery layers only where contact or placing changes. Preserve original takes and protect intentional pauses. | Compare on the same map, lens and timing. Show full-speed and slow-motion contact, foot support, lead-in, gaze and continued movement. Verify an actual hit separately from a trigger pull. |
| 3. Make causes visible | At each beat save a state snapshot. Branch a matching rehearsal with the cue omitted, delayed or aimed elsewhere. Preserve the same set, seed and inputs as far as the game permits. Log actual divergence and first physical contact. | The intended outcome changes as predicted in the control, or the brief/report labels the relationship unproven. A viewer can point out the threat, decision and consequence without seeing the log. |
| 4. Cover the action | Derive candidate camera marks from both actors and their intended path. Reject intersecting views. Score screen-space actor size, occluded frames, aim line and headroom at action time. Keep a short first-person shot for agency and a witness long enough to show recovery. Try alternatives on one frozen tape. | Timecoded images and full-speed cuts on a quiet scene, a pursuit, a crowded collapse and a hazard scene. Check the harder subject, typically the receiver of a hit. Report camera failures even when the sim passes. |
| 5. Edit and mix | Assemble by lifting source frame ranges. Maintain exact sound perspective and sync across cuts. Let an editor mute a delivery, retain WAV stems, trim a hold, switch lens or swap a take. For multi-beat pieces compare two distinct edits of the same material. | Decode exports after moving the edit package. Check event transients against pictures, listen on headphones and speakers, and open the timeline in a second editor. OTIO stores cut information and refers to external media. [OpenTimelineIO](https://opentimelineio.readthedocs.io/en/v0.18.1/) |
| 6. Direct revisions safely | Introduce clip, event and actor IDs across changes. “Give the landing another half-second” should affect only the selected range and downstream offsets. Keep undo/redo, import previous saves, and simulate a renderer crash before publish. | Round-trip a legacy set and reopen the edited version. Compare unchanged source hashes, replay the revised cut and recover the last complete edit after an interrupted render. |
| 7. Prove generalization | Reserve a set of unseen prompts, maps, seeds and actor counts that the implementing agents never see. Include a quiet environment, comic reaction, POV encounter, crowded escape, subtle sound cue and conflicting brief. Record creator time, interventions and unusable attempts across the whole set. | Compare against human-directed clips of the same briefs with equal runtime, output resolution and access to inputs. Keep failures in the denominator and report uncertainty by genre. |

### Human evaluation

Before rendering, write down the visible and audible facts that distinguish success for each brief. For the rooftop example these include who noticed the threat, who jumped, whether the shot hit, where the survivor landed and whether they kept moving. A viewer may answer “I couldn't tell.” Treat that as an observation about the cut.

For a first formative test, use three independent viewers unfamiliar with the takes and a human maker familiar with the game. Use the existing [counterbalanced packet tool](attempts/disaster-pvp-2/tools/blind_review.py). Half the packets lead with each cut where group size permits. Ask for unaided recall first, timestamped issues second, preference last. Measure direction fidelity, action readability, physical believability, sound and pacing separately. Three people can expose failures. A broader human-quality claim needs more independent viewers and held-out briefs, confidence intervals, and advance agreement on the acceptable quality gap and creator time. Report raw counts as well as percentages.

Use the same display, viewing distance, sound level and instructions for each pair and keep observer order in the results. ITU-R BT.500 describes controlled subjective picture assessment and stimulus comparison. Narrative recall and acting are additional game-specific questions in this protocol. [ITU-R BT.500](https://www.itu.int/rec/R-REC-BT.500-15-202305-I/en)

### Parallel ownership

Use one Sol agent for each independent bottleneck, subject to available slots. Producers own interpretation and asset lookup, performers own motion and map rehearsal, cinematographers work from locked takes, and editorial owns cuts and sound. Independent reviewers receive the brief and anonymized movies. A single integrator owns the runnable revision and evidence record. Reassign a slot when its artifact is usable. Stop generating takes when symptoms point to a single fix that can be tested on an existing recording.

All proposed production methods can use the installed game, local Python, FFmpeg and open-source editorial tools. An optional local model needs a measured hardware budget and a redistribution/license check before it becomes a dependency. Paid inference and generation are unnecessary for the implemented path.

## Verified branch checkpoint

The owned branch is `codex/cutscene-editor-tool`, based on `abd1fc9f8b8f1aeeced118194f93c862fee8e90e` from `origin/Evan_FINAL_Sprint`. The latest tested implementation is `186dad73d97ca585d54d0023f1341893b54536cd`. Scope is DevTools and its companion studio tooling. Integration into the shared target, broader held-out human comparisons and headset verification remain open.

| Exact source | Reproducible check | Observed result |
|---|---|---|
| `186dad73` | Stage the Castle contact example, render with `GameplayTheatre.RunDirectedExample`, audit the take against the example. | 75 frames at 15 fps. The camera cut at 1.60 s. At 1.867 s the named rival alone received a push and its health went from 100 to 82. The approach passed 45 support probes with a 0.002 m spread in support height. The close view still shows a visible separation between bodies. Whether the fist reads as making convincing contact needs viewer review. |
| `3eec56c4` | Render `GameplayTheatre.RunQuietExample` and audit the quiet Castle example. | Four seconds, 60 decoded frames at 360×640. The cast was empty and the decoded delivery audio was silent. |
| `fff7ee69` | Run `TrailerStudio.CrowdRunScout.RunRooftopTsunami` and inspect both synchronized views. | 150 frames per angle at 30 fps. The recorded Blaster shot was airborne on tape frame 66 and the first landing was on frame 94. The new camera follows the roof crossing before revealing the tower base. The rival remains small and the hero later falls below the planned route. |

Run `py -3 -B -m unittest discover -s Docs/Development/TrailerStudio/tools` plus `direct_prompt.py --self-test` for the offline checks. Compare scene revisions on the same prompts, using the same input footage when testing only camera or editorial changes. For the next milestone obtain a human-operated take with the same actor, route and framing and collect blind, timecoded viewer responses.
