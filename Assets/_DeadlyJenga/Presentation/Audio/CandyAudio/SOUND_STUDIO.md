# Sound Studio

Open **Tools → Candy Audio → Sound Studio** in the Unity Editor. This is the project authoring tool. It works outside Play Mode and uses the existing Candy Audio engine, not another audio middleware package.

The left column groups sounds by movement, climbing, combat and weapon, disaster, wildlife, map ambience, music and UI. Search also matches trigger explanations and source symbols. The middle column lists sound routes; the right column shows the trigger, recordings, editable settings and source references. Some routes are legacy or reserved actions: listing one does not mean gameplay currently emits it. Check the linked call sites and Play Mode history.

## Listen and replace

1. Select a section and sound. **Preview take**, **Next variation** and the numbered buttons play raw recordings without entering the game. Stop ends loops. Game mixer gain, intensity and spatial falloff are not part of this raw preview.
2. Choose **Create editable override** for a generated/default sound. Generated samples become ordinary WAV assets in `StudioRecordings` beside the audio Resources folder (outside Resources, so unused takes do not automatically inflate builds). Nothing is generated or written just by opening the catalog.
3. Drag AudioClips into **Variants**, choose **Import recording**, or use **Clip library** to preview and assign existing project recordings. Multiple variants randomize through the existing engine.
4. Adjust enabled, volume, pitch, distances, bus, output mixer, cooldown and simultaneous voices. Dedicated music/ambient sources expose only supported clip/volume controls. Their volume is a multiplier of the original mix; their clip changes require a scene restart.
5. **Save project** validates and saves the bank. Unity Undo/Redo works for bank edits. Newly imported recordings remain on disk after Undo or restoring a default.

The default bank is the Resources asset `CandyAudio/Default`. Changes are included in future builds made from this checkout. Editing a different bank does not change the game unless that bank is assigned at runtime. Core enums and game rules are untouched.

## Hear it in context

Enter Play Mode normally, then **Apply to running game**. This installs a temporary copy and restarts pooled voices. Closing the studio restores the previous session bank; exiting Play Mode also discards the trial. Saved project edits remain. The older F8 in-game audition panel is separate and its device presets are not project authoring.

**Watch actual played cues** records accepted pooled starts, with name, recording and time. **Live history** shows the latest 80 events and engine counters. Culled requests and dedicated music/ambient sources are not included. This monitoring is Editor-only, optional, and is absent from APKs.

## Add a sound and trigger

**+ Sound + trigger** creates a stable custom ID, category, description and optional one-shot event binding. No code is needed for round start/end, hazard start/end, player damage/death, emotes, block impacts/breaks, rewind, departures, match feedback and effect start/end.

Use an optional exact asset name (hazard/effect), emote name or MatchSoundCue name as the filter. Filters ignore case and surrounding whitespace. `Local player only` applies to events with an actor; global round/hazard events have no actor. `Local` chooses non-spatial playback. Minimum strength filters damage amount, impact speed or announced duration depending on the event. Additional bindings layer a sound; they do not remove existing observers. Continuous loops need an observer that refreshes a bounded loop while the state remains active, not a one-shot binding.

For a genuinely new condition, select **Copy brief for Codex**, paste it into the chat and describe the desired trigger or recording. The brief includes the sound ID, bank, existing source locations and recordings. Codex can implement an observer of existing Core state/events or create/import approved audio. The button does not send anything, run paid generation, enable microphones or start a voice service.

## Keep it current without a background job

The catalog reads the bank, named cues, weapon action definitions, emotes and registered dedicated routes. Project changes mark it for refresh when you return to the window. Source locations are cached once per script reload; the Refresh button can explicitly rebuild that index. No filesystem watcher, scheduled agent, per-frame asset scan or recurring test is installed.

When adding audio, agents must add its catalog route/trigger description or bank metadata, preserve its stable ID and expose any dedicated source override. Read `Presentation/AGENTS.md`. `Clip library` lists imported recordings even if an agent has not yet assigned them. Unused clips are not assumed to be gameplay triggers. Streamed voice chat/conversations are not static clips. The studio does not initialize or reconfigure those services; their existing game settings are unchanged.

**Tools → Candy Audio → Run Sound Studio Checks** is an explicit focused fixture for catalog IDs, representative defaults, generated WAV persistence, Undo/Redo and real event dispatch in Play Mode. It uses a temporary bank and removes only its own scratch assets. It does not run automatically or test networking/Quest audio output.
