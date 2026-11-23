using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    internal static class StudioReactionLibrary
    {
        const string Preference = "DeadlyJenga.TrailerStudio.ReactionPresets";

        [Serializable]
        internal sealed class Preset
        {
            public string name, reaction;
            public float duration = .65f, strength = 1, spread;
            public bool mirror, preserveGrip = true;
        }

        [Serializable]
        sealed class SavedPresets { public List<Preset> presets = new List<Preset>(); }

        static SavedPresets Load()
        {
            try
            {
                var saved = JsonUtility.FromJson<SavedPresets>(EditorPrefs.GetString(Preference, "")) ?? new SavedPresets();
                saved.presets ??= new List<Preset>();
                return saved;
            }
            catch (ArgumentException) { return new SavedPresets(); }
        }

        internal static List<StudioOverride> Make(StudioSet set, StudioTape tape, StudioMark selected,
            Preset preset, int startFrame, bool wholeGroup, int seed)
        {
            if (selected.role == StudioRole.Hand) throw new InvalidOperationException("These reactions require a survivor rig. Use a hand performance for the flying hand.");
            if (!StudioOverrides.Reactions.Contains(preset.reaction)) throw new InvalidOperationException("Choose a reaction.");
            var targets = wholeGroup && !string.IsNullOrEmpty(selected.crowdGroup)
                ? set.marks.Where(m => m.crowdGroup == selected.crowdGroup && !m.heroLocked)
                : new[] { selected }.AsEnumerable();
            var random = new System.Random(seed);
            var layers = new List<StudioOverride>();
            foreach (var target in targets)
            {
                var track = tape.Find(target.id);
                if (target.role == StudioRole.Hand || track?.locked != false || !PerformMode.HasPerformance(track)) continue;
                int delay = Mathf.RoundToInt((float)random.NextDouble() * Mathf.Max(0, preset.spread) * set.fps);
                int at = Mathf.Clamp(startFrame + delay, 0, set.Frames - 1);
                if (!track.ActiveTake.Covers(at)) continue;
                float from = at / (float)set.fps;
                float duration = Mathf.Clamp(preset.duration, 1f / set.fps, Mathf.Min(set.Frames, track.ActiveTake.End) / (float)set.fps - from);
                layers.Add(new StudioOverride
                {
                    actor = target.id, kind = "reaction", name = preset.reaction, from = from, to = from + duration,
                    strength = Mathf.Clamp01(preset.strength), blendIn = Mathf.Min(.05f, .2f * duration),
                    blendOut = Mathf.Min(.16f, .3f * duration), libraryReaction = true,
                    mirrorReaction = preset.mirror, preserveGrip = preset.preserveGrip
                });
            }
            return layers;
        }

        internal static void Add(VisualElement panel, StudioContext context, StudioMark selected)
        {
            if (selected == null) return;
            var fold = new Foldout { text = "Reaction library", value = false };
            panel.Add(fold);
            if (selected.role == StudioRole.Hand)
            {
                fold.Add(StudioUi.Hint("Survivor body reactions need a head and arms. Use a recorded hand performance for this role."));
                return;
            }

            var saved = Load();
            var defaults = StudioOverrides.Reactions.Select((reaction, index) => new Preset
            {
                name = StudioOverrides.ReactionLabels[index], reaction = reaction
            }).ToList();
            var choices = defaults.Concat(saved.presets).ToList();
            var pick = new DropdownField("Preset", choices.Select(p => p.name).ToList(), 0);
            var duration = new FloatField("Duration (s)");
            var strength = new Slider("Strength", 0, 1) { showInputField = true };
            var spread = new Slider("Timing spread (s)", 0, 2) { showInputField = true };
            var mirror = new Toggle("Mirror left and right");
            var gear = new Toggle("Keep drawn equipment grip");
            var group = new Toggle("Apply to unlocked group members");
            var seed = new IntegerField("Timing seed") { value = context.Set.seed };
            var name = new TextField("Save as");
            void Read()
            {
                var preset = choices[pick.index];
                duration.value = preset.duration;
                strength.value = preset.strength;
                spread.value = preset.spread;
                mirror.value = preset.mirror;
                gear.value = preset.preserveGrip;
            }
            Preset Current() => new Preset
            {
                name = name.value.Trim(), reaction = choices[pick.index].reaction, duration = duration.value,
                strength = strength.value, spread = spread.value, mirror = mirror.value, preserveGrip = gear.value
            };
            pick.RegisterValueChangedCallback(_ => Read());
            Read();
            fold.Add(pick); fold.Add(duration); fold.Add(strength); fold.Add(mirror); fold.Add(gear);
            if (!string.IsNullOrEmpty(selected.crowdGroup)) fold.Add(group);
            fold.Add(spread); fold.Add(seed);
            fold.Add(StudioUi.Small("Apply at playhead", () =>
            {
                if (StudioRun.Busy || StudioHandheld.Recording) return;
                var layers = Make(context.Set, context.Tape, selected, Current(), context.Frame, group.value, seed.value);
                if (layers.Count == 0) { context.Say("Pick an unlocked survivor whose take covers the playhead."); return; }
                context.Edit("Apply reaction preset", () => context.Set.overrides.AddRange(layers));
                if (layers.Count == 1) context.Select(layers[0]);
                context.Say($"Added {layers.Count} editable reaction layers.");
            }));
            fold.Add(name);
            var feedback = StudioUi.Hint("");
            fold.Add(StudioUi.Small("Save preset", () =>
            {
                var preset = Current();
                if (preset.name.Length == 0 || defaults.Any(p => p.name == preset.name))
                { feedback.text = "Enter a name different from the built-in presets."; return; }
                saved.presets.RemoveAll(p => p.name == preset.name);
                saved.presets.Add(preset);
                EditorPrefs.SetString(Preference, JsonUtility.ToJson(saved));
                choices = defaults.Concat(saved.presets).ToList();
                pick.choices = choices.Select(p => p.name).ToList();
                pick.index = choices.Count - 1;
                feedback.text = $"Saved {preset.name} to this editor's reaction library.";
            }));
            fold.Add(feedback);
        }
    }
}
