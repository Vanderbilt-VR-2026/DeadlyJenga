using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.CandyAudio.Editor
{
    public sealed partial class CandySoundStudioWindow : EditorWindow
    {
        [SerializeField] CandyAudioBank bank;
        [SerializeField] int selectedId = -1;
        [SerializeField] string section = "All sounds", search = "";
        List<CandySoundEntry> entries = new(), visible = new();
        readonly List<string> live = new();
        ListView sounds;
        ScrollView sections, details;
        Label status, summary, liveText;
        TextField searchField;
        bool invalidated, watching;
        CandyAudioBank trial, beforeTrial;
        CandyAudioEngine trialEngine;
        AudioClip previewClip;
        int take;
        CandyAudioCue resolved;
        static readonly Type AudioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        static readonly MethodInfo Preview = AudioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        static readonly MethodInfo StopPreview = AudioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        [MenuItem("Tools/Candy Audio/Sound Studio")]
        public static void Open()
        {
            bool alreadyOpen = HasOpenInstances<CandySoundStudioWindow>();
            var window = GetWindow<CandySoundStudioWindow>();
            if (!alreadyOpen) window.position = new Rect(80,80,1280,800);
            if (window.selectedId < 0) { window.selectedId = (int)CandySound.FootstepCandy; window.section = "Player / Movement"; window.Refresh(); }
            window.titleContent = new GUIContent("Sound Studio");
            window.minSize = new Vector2(1080, 640);
            window.Show();
        }
        void OnEnable()
        {
            EditorApplication.projectChanged += Invalidate;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Undo.undoRedoPerformed += Invalidate;
        }
        void OnDisable()
        {
            EditorApplication.projectChanged -= Invalidate;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            Undo.undoRedoPerformed -= Invalidate;
            CandyAudioEngine.SoundPlayed -= Heard;
            EndTrial();
            previewPlayer?.SetPlaying(false);
            Stop();
        }
        void Invalidate() { invalidated = true; CandySoundPreview.Invalidate(); SetStatus("Project changed · refreshes when you return to this window, or choose Refresh."); }
        void OnFocus() { previewPlayer?.SetFocused(true); if (invalidated && sounds != null && !EditorApplication.isCompiling && !EditorApplication.isUpdating) Refresh(); }
        void OnLostFocus() => previewPlayer?.SetFocused(false);
        void PlayModeChanged(PlayModeStateChange state)
        {
            Stop();
            if (state == PlayModeStateChange.ExitingPlayMode) EndTrial();
            CandyAudioEngine.SoundPlayed -= Heard;
            if (watching && state == PlayModeStateChange.EnteredPlayMode) CandyAudioEngine.SoundPlayed += Heard;
            Refresh();
        }
        public void CreateGUI() => BuildStudio();
        static Label Text(string text, string style = null) { var label = new Label(text); if (style != null) label.AddToClassList(style); if (FriendlyFont && (style == "studio-title" || style == "detail-title" || style == "section-title" || style == "preview-empty-title")) label.style.unityFontDefinition = FontDefinition.FromFont(FriendlyFont); return label; }
        void SetStatus(string text) { if (status != null) status.text = text; }
        void Heard(CandySound id, AudioClip clip)
        {
            var entry = entries.Find(e => e.id == id);
            live.Insert(0, $"{Time.unscaledTime:0.0}s · {entry?.title ?? id.ToString()} · {(clip ? clip.name : "missing clip")}");
            if (live.Count > 80) live.RemoveAt(live.Count - 1);
            if (liveText != null) liveText.text = live[0];
        }
        void ShowHistory()
        {
            ClearDetails(); details.Add(Text("Live playback", "detail-title"));
            var watch = new Toggle("Record played sounds in Play Mode") { value = watching };
            watch.RegisterValueChangedCallback(e => { watching = e.newValue; CandyAudioEngine.SoundPlayed -= Heard; if (watching) CandyAudioEngine.SoundPlayed += Heard; }); details.Add(watch);
            details.Add(Action("Clear history", () => { live.Clear(); ShowHistory(); }, "subtle"));
            details.Add(Text("Only accepted pooled playback is captured. Dedicated music/ambient sources and culled requests are not counted here. History is bounded to 80 entries.", "callout"));
            var engine = CandyAudioEngine.Instance;
            if (engine) details.Add(Text($"Active voices: {engine.ActiveVoices} · played: {engine.Played} · culled: {engine.Culled}", "muted"));
            foreach (var line in live) details.Add(Text(line, "source-line"));
        }
        public void Refresh()
        {
            if (sounds == null) return;
            invalidated = false;
            Stop(); entries = CandySoundCatalog.Build(bank);
            BuildSections();
            tryGame?.SetEnabled(EditorApplication.isPlaying);
            Filter();
            SetStatus($"{entries.Count} sounds · Ready");
        }
        void AddSection(string name) { sections.Add(new Button(() => { section = name; Filter(); }) { text = name }); }
        void Filter()
        {
            if (sounds == null) return;
            visible = entries.Where(e => (!string.IsNullOrWhiteSpace(search) || section == "All sounds" || e.category == section) &&
                (string.IsNullOrWhiteSpace(search) || (e.title + " " + e.category + " " + e.trigger + " " + e.symbol).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            sounds.itemsSource = visible; sounds.Rebuild(); summary.text = $"{visible.Count} sounds · " + (string.IsNullOrWhiteSpace(search) ? section : "Search all");
            int index = visible.FindIndex(e => (int)e.id == selectedId);
            if (index < 0 && visible.Count > 0) index = 0;
            if (index >= 0)
            {
                selectedId = (int)visible[index].id;
                sounds.SetSelectionWithoutNotify(new[] { index });
                Show(visible[index]);
            }
            else
            {
                Stop(); ClearDetails();
                details.Add(Text("No matching sounds", "detail-title"));
                details.Add(Text("Clear the search or choose another section.", "callout"));
            }
        }
        void Show(CandySoundEntry entry) => ShowSound(entry);
        void AddProperty(SerializedProperty parent, string name) => details.Add(new PropertyField(parent.FindPropertyRelative(name)));
        public void Audition(CandyAudioCue cue, int index)
        {
            if (cue?.variants == null || cue.variants.Length == 0) { SetStatus("No clip assigned. Import a recording or add a variant."); return; }
            previewClip = cue.variants[Mathf.Clamp(index, 0, cue.variants.Length - 1)];
            if (!previewClip) { SetStatus("This variation is missing its clip."); return; }
            if (Preview == null) { SetStatus("Unity preview API unavailable. Select the clip to use its Inspector preview."); Selection.activeObject = previewClip; return; }
            StopPreview?.Invoke(null, null);
            Preview.Invoke(null, new object[] { previewClip, 0, cue.loop });
            SetStatus("Previewing " + previewClip.name + (cue.loop ? " · loop · Stop to end" : ""));
        }
        void Stop() { StopPreview?.Invoke(null, null); previewClip = null; }
        public CandyAudioCue MakeOverride(CandySoundEntry entry, bool preserveRecordings = true)
        {
            if (!bank) throw new InvalidOperationException("Choose the game's audio bank first.");
            var existing = CandySoundCatalog.Authored(bank, entry.id); if (existing != null) return existing;
            var cue = CandyAudioDefaults.Copy(entry.fallback?.Invoke()) ?? new CandyAudioCue { sound = entry.id };
            cue.sound = entry.id; cue.displayName = entry.title; cue.category = entry.category; cue.triggerNotes = entry.trigger;
            if (preserveRecordings) CandyStudioAssets.PersistGeneratedClips(bank, cue); else cue.variants = Array.Empty<AudioClip>();
            Undo.RecordObject(bank, "Author sound override");
            bank.cues = (bank.cues ?? Array.Empty<CandyAudioCue>()).Concat(new[] { cue }).ToArray(); EditorUtility.SetDirty(bank);
            return cue;
        }
        void Save()
        {
            if (!bank) return;
            var issues = CandyStudioAssets.Validate(bank);
            if (issues.Count > 0) { SetStatus(string.Join(" | ", issues.Take(4))); return; }
            AssetDatabase.SaveAssetIfDirty(bank); SetStatus("Saved project audio bank. Included in the next build from this checkout.");
        }
        void ApplyToGame()
        {
            var engine = CandyAudioEngine.Instance;
            if (!EditorApplication.isPlaying || !engine || !bank) { SetStatus("Start Play Mode first. Previewing individual clips does not require it."); return; }
            var issues = CandyStudioAssets.Validate(bank);
            if (issues.Count > 0) { SetStatus(string.Join(" | ", issues.Take(4))); return; }
            if (engine.GetComponent<CandyAudioStudio>() is CandyAudioStudio legacy && legacy.IsOpen) legacy.Close();
            EndTrial();
            beforeTrial = engine.bank; trialEngine = engine;
            var copy = Instantiate(bank); trial = copy; copy.name = bank.name + " (Sound Studio trial)";
            engine.Configure(copy);
            SetStatus("Applied a session copy. Pooled loops restart; dedicated music clips update on scene restart. Closing this studio restores the prior session mix. Project changes still need Save.");
        }
        void EndTrial()
        {
            if (trialEngine && trialEngine.bank == trial) trialEngine.Configure(beforeTrial);
            if (trial) DestroyImmediate(trial);
            trial = beforeTrial = null; trialEngine = null;
        }
        void ShowMix()
        {
            ClearDetails(); details.Add(Text("Mix and voice budgets", "detail-title"));
            if (!bank) return;
            var so = new SerializedObject(bank);
            var levels = Card("Volume balance"); levels.style.maxWidth = 820;
            foreach (var name in new[] { "master", "world", "characters", "ambience", "music", "ui" })
                levels.Add(new Slider(name == "ui" ? "Interface" : ObjectNames.NicifyVariableName(name), 0, 1) { bindingPath = name, showInputField = true });
            details.Add(levels);
            var limits = Card("Playback limits"); limits.style.maxWidth = 820;
            limits.Add(Text("Caps simultaneous sounds and new starts to keep the mix clear.", "body-copy"));
            limits.Add(new SliderInt("One-shot sounds", 8, 48) { bindingPath = "oneShotVoices", showInputField = true });
            limits.Add(new SliderInt("Looping sounds", 4, 24) { bindingPath = "loopVoices", showInputField = true });
            limits.Add(new SliderInt("Starts per second", 4, 40) { bindingPath = "eventsPerSecond", showInputField = true });
            details.Add(limits);
            details.Bind(so);
            details.Add(new Button(() => { var issues = CandyStudioAssets.Validate(bank); SetStatus(issues.Count == 0 ? "Bank checks passed." : string.Join(" | ", issues)); }) { text = "Check bank" });
        }
        void NewSound()
        {
            ClearDetails(); details.Add(Text("New sound and trigger", "detail-title"));
            details.Add(Text("Adds a one-shot sound to an existing event. A new gameplay condition still requires a small observer in code; Copy brief supplies the details for Codex.", "callout"));
            var name = new TextField("Name") { value = "New sound" };
            var category = new TextField("Section / subsection") { value = "Custom / New sounds" };
            var notes = new TextField("Trigger explanation") { multiline = true };
            var clip = new ObjectField("Recording") { objectType = typeof(AudioClip), allowSceneObjects = false };
            var trigger = new EnumField("When", CandyAudioTrigger.Manual);
            var filter = new TextField("Exact name filter (optional)");
            details.Add(name); details.Add(category); details.Add(notes); details.Add(clip); details.Add(trigger); details.Add(filter);
            details.Add(new Button(() => {
                if (!bank || string.IsNullOrWhiteSpace(name.value)) { SetStatus("Choose a bank and enter a name."); return; }
                var ids = new HashSet<int>(entries.Select(e => (int)e.id)); int id = 2000000; while (ids.Contains(id)) id++;
                var cue = new CandyAudioCue { sound = (CandySound)id, displayName = name.value.Trim(), category = category.value.Trim(), triggerNotes = notes.value,
                    variants = clip.value ? new[] { (AudioClip)clip.value } : Array.Empty<AudioClip>(), bus = CandyBus.Character };
                Undo.RecordObject(bank, "Add sound and trigger"); bank.cues = (bank.cues ?? Array.Empty<CandyAudioCue>()).Concat(new[] { cue }).ToArray();
                if ((CandyAudioTrigger)trigger.value != CandyAudioTrigger.Manual)
                    bank.bindings = (bank.bindings ?? Array.Empty<CandyAudioBinding>()).Concat(new[] { new CandyAudioBinding { sound = cue.sound, trigger = (CandyAudioTrigger)trigger.value, filter = filter.value } }).ToArray();
                EditorUtility.SetDirty(bank); selectedId = id; section = "All sounds"; search = ""; searchField.SetValueWithoutNotify(""); Refresh();
            }) { text = "Create sound" });
        }
        void AddBinding(CandySoundEntry entry)
        {
            MakeOverride(entry); Undo.RecordObject(bank, "Add sound trigger");
            bank.bindings = (bank.bindings ?? Array.Empty<CandyAudioBinding>()).Concat(new[] { new CandyAudioBinding { sound = entry.id } }).ToArray();
            EditorUtility.SetDirty(bank); Show(entry);
        }
        void ImportClip(CandySoundEntry entry, bool replace = false)
        {
            string file = EditorUtility.OpenFilePanelWithFilters("Import sound", "", new[] { "Audio", "wav,ogg,mp3,aif,aiff" });
            if (string.IsNullOrEmpty(file)) return;
            string folder = CandyStudioAssets.RecordingFolder(bank);
            string target = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Path.GetFileName(file));
            File.Copy(file, target); AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(target);
            if (!clip) { SetStatus("Unity could not import this recording; see Console."); return; }
            var cue = MakeOverride(entry, !replace); Undo.RecordObject(bank, replace ? "Replace recording" : "Add recording variation");
            cue.variants = replace ? new[] { clip } : (cue.variants ?? Array.Empty<AudioClip>()).Concat(new[] { clip }).ToArray(); EditorUtility.SetDirty(bank); Show(entry);
        }
        void ShowLibrary()
        {
            ClearDetails(); details.Add(Text("Imported clip library", "detail-title"));
            details.Add(Text("Search project recordings, preview, or assign a clip to the selected sound. Generated defaults appear in their sound routes instead.", "callout"));
            var paths = AssetDatabase.FindAssets("t:AudioClip").Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            var filtered = new List<string>(paths);
            var find = new ToolbarSearchField(); details.Add(find);
            var list = new ListView { fixedItemHeight = 46, itemsSource = filtered }; list.AddToClassList("clip-library");
            list.makeItem = () => new Label(); list.bindItem = (element, index) => { ((Label)element).text = Path.GetFileName(filtered[index]); element.tooltip = filtered[index]; };
            AudioClip chosen = null;
            list.selectionChanged += values => { if (values.FirstOrDefault() is string path) { chosen = AssetDatabase.LoadAssetAtPath<AudioClip>(path); Audition(new CandyAudioCue { variants = new[] { chosen } }, 0); } };
            find.RegisterValueChangedCallback(e => { filtered = paths.Where(p => p.IndexOf(e.newValue, StringComparison.OrdinalIgnoreCase) >= 0).ToList(); list.itemsSource = filtered; list.Rebuild(); });
            details.Add(list);
            details.Add(new Button(Stop) { text = "Stop preview" });
            details.Add(new Button(() => {
                var entry = entries.Find(e => (int)e.id == selectedId);
                if (entry == null || !chosen) { SetStatus("Select a sound route first, then a clip."); return; }
                var cue = MakeOverride(entry, false); Undo.RecordObject(bank, "Replace sound recordings"); cue.variants = new[] { chosen }; EditorUtility.SetDirty(bank); Show(entry);
            }) { text = "Replace selected sound's recordings with this clip" });
        }
        void CopyBrief(CandySoundEntry entry)
        {
            var text = new StringBuilder();
            text.AppendLine("Deadly Jenga sound design / integration request");
            text.AppendLine("Sound: " + entry.title + " | ID " + (int)entry.id + " | " + entry.category);
            text.AppendLine("Bank: " + AssetDatabase.GetAssetPath(bank)); text.AppendLine("Trigger: " + entry.trigger);
            text.AppendLine("Wanted change: [describe the sound, reference, or new trigger here]");
            text.AppendLine("Keep the candy/plastic/ocean style, existing event timing and bounded voice budget. Preserve unrelated work. New event conditions belong in a Presentation observer of Core; no gameplay changes or Core edits by default.");
            if (resolved?.variants != null) foreach (var clip in resolved.variants) if (clip) text.AppendLine("Take: " + (AssetDatabase.Contains(clip) ? AssetDatabase.GetAssetPath(clip) : clip.name + " (procedural)"));
            foreach (var use in entry.uses.Take(12)) text.AppendLine("Source: " + use.path + ":" + use.line + " " + use.text);
            text.AppendLine("Update the source-backed catalog description when trigger meaning changes. Validate the affected cue and report real audition/runtime evidence separately.");
            GUIUtility.systemCopyBuffer = text.ToString(); SetStatus("Sound brief copied. Paste into Codex and describe the change; no paid generation was started.");
        }
    }
}
