# Sound Studio

Open **Tools → Candy Audio → Sound Studio** in the Unity Editor. This is the project authoring tool. It works outside Play Mode and uses the existing Candy Audio engine, not another audio middleware package.

The left column groups sounds by movement, climbing, combat and weapon, disaster, wildlife, map ambience, music and UI. Search also matches trigger explanations and source symbols. The middle column lists sound routes; the right column shows the trigger, recordings, editable settings and source references. Some routes are legacy or reserved actions: listing one does not mean gameplay currently emits it. Check the linked call sites and Play Mode history.

## Listen and replace

1. Select a section and sound. **Play sound**, the take dropdown and **Next variation** play raw recordings without entering the game. Stop ends loops. Game mixer gain, intensity and spatial falloff are not part of this raw preview.
2. Choose **Customize this sound** for a generated/default sound. Generated samples become ordinary WAV assets in `StudioRecordings` beside the audio Resources folder (outside Resources, so unused takes do not automatically inflate builds). Nothing is generated or written just by opening the catalog.
3. Use **Replace recording**, **+ Variation**, or **Browse recordings**. You can also drag AudioClips into **Sound settings > Recordings & variations**. Multiple variants randomize through the existing engine.
4. Adjust enabled, volume, pitch, distances, bus, output mixer, cooldown and simultaneous voices. Dedicated music/ambient sources expose only supported clip/volume controls. Their volume is a multiplier of the original mix; their clip changes require a scene restart.
5. **Save changes** validates and saves the bank. Unity Undo/Redo works for bank edits. Newly imported recordings remain on disk after Undo or restoring a default. Advanced controls remain in **Mix, distance & playback limits**, **Additional triggers**, and **Source & tools**.

The default bank is the Resources asset `CandyAudio/Default`. Changes are included in future builds made from this checkout. Editing a different bank does not change the game unless that bank is assigned at runtime. Core enums and game rules are untouched.

## Hear it in context

Enter Play Mode normally, then **Try in game**. This installs a temporary copy and restarts pooled voices. Closing the studio restores the previous session bank; exiting Play Mode also discards the trial. Saved project edits remain. The older F8 in-game audition panel is separate and its device presets are not project authoring.

**Library > Live playback history** has an optional **Record played sounds in Play Mode** toggle and shows the latest 80 accepted pooled starts and engine counters. Culled requests and dedicated music/ambient sources are not included. This monitoring is Editor-only and absent from APKs. Bank selection, mix budgets and all recordings remain under **Library**.

## See the trigger

The large preview card shows a clearly labelled illustration until gameplay footage is supplied. Imported 1–5 second clips offer Watch/Pause, scrubbing and **Show event**. They run only while this window is focused and do not add assets or work to the APK. **Capture brief** supplies contextual source/action/target/listener instructions for that sound.

The other capture chat should follow [the full capture and upload guide](SOUND_PREVIEW_CAPTURE_GUIDE.md). **Library > Export all capture briefs** creates the current batch; **Library > Import trigger previews** explicitly imports reviewed packages. **Library > Check preview setup** runs a short synthetic importer/UI check, not gameplay capture or a broad game test.

## Add a sound and trigger

**+ New sound** creates a stable custom ID, category, description and optional one-shot event binding. No code is needed for round start/end, hazard start/end, player damage/death, emotes, block impacts/breaks, rewind, departures, match feedback and effect start/end.

Use an optional exact asset name (hazard/effect), emote name or MatchSoundCue name as the filter. Filters ignore case and surrounding whitespace. `Local player only` applies to events with an actor; global round/hazard events have no actor. `Local` chooses non-spatial playback. Minimum strength filters damage amount, impact speed or announced duration depending on the event. Additional bindings layer a sound; they do not remove existing observers. Continuous loops need an observer that refreshes a bounded loop while the state remains active, not a one-shot binding.

For a genuinely new condition, select **Source & tools > Copy sound-design brief**, paste it into the chat and describe the desired trigger or recording. The brief includes the sound ID, bank, existing source locations and recordings. Codex can implement an observer of existing Core state/events or create/import approved audio. The button does not send anything, run paid generation, enable microphones or start a voice service.

## Keep it current without a background job

The catalog reads the bank, named cues, weapon action definitions, emotes and registered dedicated routes. Project changes mark it for refresh when you return to the window. Source locations are cached once per script reload; **Library > Refresh catalog** explicitly rebuilds that index. No filesystem watcher, scheduled agent, per-frame asset scan or recurring test is installed.

When adding audio, agents must add its catalog route/trigger description or bank metadata, preserve its stable ID and expose any dedicated source override. Read `Presentation/AGENTS.md`. `Clip library` lists imported recordings even if an agent has not yet assigned them. Unused clips are not assumed to be gameplay triggers. Streamed voice chat/conversations are not static clips. The studio does not initialize or reconfigure those services; their existing game settings are unchanged.

**Tools → Candy Audio → Run Sound Studio Checks** is an explicit focused fixture for catalog IDs, representative defaults, generated WAV persistence, Undo/Redo and real event dispatch in Play Mode. It uses a temporary bank and removes only its own scratch assets. It does not run automatically or test networking/Quest audio output.
