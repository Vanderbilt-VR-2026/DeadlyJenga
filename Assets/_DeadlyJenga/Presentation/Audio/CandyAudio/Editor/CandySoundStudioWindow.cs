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
    public sealed class CandySoundStudioWindow : EditorWindow
    {
        [SerializeField] CandyAudioBank bank;
        [SerializeField] int selectedId = -1;
        [SerializeField] string section = "All sounds", search = "";
        List<CandySoundEntry> entries = new(), visible = new();
        readonly List<string> live = new();
        ListView sounds;
        ScrollView sections, details;
        Label status, summary, liveText;
        ToolbarSearchField searchField;
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
            Stop();
        }
        void Invalidate() { invalidated = true; SetStatus("Project changed · refreshes when you return to this window, or choose Refresh."); }
        void OnFocus() { if (invalidated && sounds != null && !EditorApplication.isCompiling && !EditorApplication.isUpdating) Refresh(); }
        void PlayModeChanged(PlayModeStateChange state)
        {
            Stop();
            if (state == PlayModeStateChange.ExitingPlayMode) EndTrial();
            CandyAudioEngine.SoundPlayed -= Heard;
            if (watching && state == PlayModeStateChange.EnteredPlayMode) CandyAudioEngine.SoundPlayed += Heard;
            Refresh();
        }
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("sound-studio");
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(ProjectPath.Find("CandySoundStudio.uss")));
            var header = new VisualElement(); header.AddToClassList("studio-header");
            header.Add(Text("SOUND STUDIO", "studio-title"));
            header.Add(Text("Browse the game’s sounds. Hear every take. Author the next one.", "muted"));
            rootVisualElement.Add(header);
            var toolbar = new Toolbar();
            var bankField = new ObjectField("Bank") { objectType = typeof(CandyAudioBank), allowSceneObjects = false };
            if (!bank) bank = Resources.Load<CandyAudioBank>("CandyAudio/Default");
            bankField.SetValueWithoutNotify(bank);
            bankField.RegisterValueChangedCallback(e => { bank = e.newValue as CandyAudioBank; Refresh(); });
            toolbar.Add(bankField);
            toolbar.Add(new ToolbarButton(() => { CandySoundCatalog.RefreshSourceIndex(); Refresh(); }) { text = "Refresh" });
            toolbar.Add(new ToolbarButton(Save) { text = "Save project" });
            toolbar.Add(new ToolbarButton(ApplyToGame) { text = "Apply to running game" });
            toolbar.Add(new ToolbarButton(NewSound) { text = "+ Sound + trigger" });
            toolbar.Add(new ToolbarButton(ShowLibrary) { text = "Clip library" });
            toolbar.Add(new ToolbarButton(ShowMix) { text = "Mix / budgets" });
            rootVisualElement.Add(toolbar);
            var searchRow = new VisualElement(); searchRow.AddToClassList("search-row");
            searchField = new ToolbarSearchField(); searchField.SetValueWithoutNotify(search);
            searchField.RegisterValueChangedCallback(e => { search = e.newValue; Filter(); });
            searchRow.Add(searchField);
            summary = Text("", "muted"); searchRow.Add(summary);
            rootVisualElement.Add(searchRow);
            var columns = new VisualElement(); columns.AddToClassList("studio-columns");
            sections = new ScrollView(); sections.AddToClassList("studio-sections"); columns.Add(sections);
            sounds = new ListView { fixedItemHeight = 57, selectionType = SelectionType.Single };
            sounds.AddToClassList("studio-sounds");
            sounds.makeItem = () => { var row = new VisualElement(); row.AddToClassList("sound-row"); row.Add(new Label { name = "name" }); row.Add(new Label { name = "meta" }); return row; };
            sounds.bindItem = (row, i) => {
                var entry = visible[i]; var authored = CandySoundCatalog.Authored(bank, entry.id);
                row.Q<Label>("name").text = entry.title;
                row.Q<Label>("meta").text = entry.category + "  ·  " + (authored != null ? authored.enabled ? "Authored" : "Muted" : "Default");
                row.Q<Label>("meta").AddToClassList("muted");
            };
            sounds.selectionChanged += items => { if (items.FirstOrDefault() is CandySoundEntry entry) { selectedId = (int)entry.id; Show(entry); } };
            columns.Add(sounds);
            details = new ScrollView(); details.AddToClassList("studio-details"); columns.Add(details);
            rootVisualElement.Add(columns);
            var bottom = new VisualElement(); bottom.AddToClassList("studio-bottom");
            var watch = new Toggle("Watch actual played cues (Editor Play Mode only)"); watch.SetValueWithoutNotify(watching);
            watch.RegisterValueChangedCallback(e => { watching = e.newValue; CandyAudioEngine.SoundPlayed -= Heard; if (watching) CandyAudioEngine.SoundPlayed += Heard; });
            bottom.Add(watch);
            bottom.Add(new Button(() => { live.Clear(); liveText.text = "No recorded cues."; }) { text = "Clear history" });
            bottom.Add(new Button(ShowHistory) { text = "Live history" });
            liveText = Text("", "muted"); bottom.Add(liveText);
            rootVisualElement.Add(bottom);
            status = Text("", "studio-status"); rootVisualElement.Add(status);
            Refresh();
        }
        static Label Text(string text, string style = null) { var label = new Label(text); if (style != null) label.AddToClassList(style); return label; }
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
            details.Clear(); details.Add(Text("Actual playback history", "detail-title"));
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
            sections.Clear();
            AddSection("All sounds");
            foreach (var group in entries.GroupBy(e => e.category.Split('/')[0].Trim()))
            {
                var foldout = new Foldout { text = group.Key, value = section.StartsWith(group.Key, StringComparison.Ordinal) };
                foreach (string category in group.Select(e => e.category).Distinct())
                {
                    string captured = category;
                    var button = new Button(() => { section = captured; Filter(); }) { text = category.Contains('/') ? category.Substring(category.IndexOf('/') + 1).Trim() : category };
                    button.AddToClassList("section-button"); foldout.Add(button);
                }
                sections.Add(foldout);
            }
            Filter();
            SetStatus($"{entries.Count} sound routes · catalog refreshed · no background scans or automated tests.");
        }
        void AddSection(string name) { sections.Add(new Button(() => { section = name; Filter(); }) { text = name }); }
        void Filter()
        {
            if (sounds == null) return;
            visible = entries.Where(e => (section == "All sounds" || e.category == section) &&
                (string.IsNullOrWhiteSpace(search) || (e.title + " " + e.category + " " + e.trigger + " " + e.symbol).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            sounds.itemsSource = visible; sounds.Rebuild(); summary.text = $"{visible.Count} routes · {section}";
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
                Stop(); details.Unbind(); details.Clear();
                details.Add(Text("No matching sounds", "detail-title"));
                details.Add(Text("Clear the search or choose another section.", "callout"));
            }
        }
        void Show(CandySoundEntry entry)
        {
            Stop(); take = 0; details.Unbind(); details.Clear();
            var authored = CandySoundCatalog.Authored(bank, entry.id);
            resolved = authored ?? entry.fallback?.Invoke();
            details.Add(Text(entry.title, "detail-title")); details.Add(Text(entry.category + " · ID " + (int)entry.id, "muted"));
            details.Add(Text(entry.trigger, "callout"));
            if (entry.direct) details.Add(Text("Dedicated source: clip and volume overrides are supported. Restart this scene to hear a changed clip. Pooled voice limits and event cooldown do not control this source.", "muted"));
            if (resolved == null) details.Add(new HelpBox("No clip/default was found for this route. Create an override and assign a recording. Source references may describe an unused or legacy route.", HelpBoxMessageType.Warning));
            var previewRow = new VisualElement(); previewRow.AddToClassList("button-row");
            previewRow.Add(new Button(() => Audition(resolved, take)) { text = "▶ Preview take" });
            previewRow.Add(new Button(Stop) { text = "■ Stop" });
            previewRow.Add(new Button(() => { if (resolved?.variants?.Length > 0) { take = (take + 1) % resolved.variants.Length; Audition(resolved, take); } }) { text = "Next variation" });
            details.Add(previewRow);
            details.Add(Text("Preview plays the raw recording. The game adds mixer gain, spatial falloff, intensity and voice limits.", "muted"));
            if (resolved?.variants != null) for (int i = 0; i < resolved.variants.Length; i++)
            {
                int index = i; AudioClip clip = resolved.variants[i];
                var row = new VisualElement(); row.AddToClassList("take-row");
                row.Add(new Button(() => { take = index; Audition(resolved, take); }) { text = "▶ " + (i + 1) });
                var field = new ObjectField { objectType = typeof(AudioClip), allowSceneObjects = false, value = clip }; field.SetEnabled(false); row.Add(field);
                row.Add(Text(clip ? $"{clip.length:0.00}s · {clip.frequency / 1000f:0.#} kHz · {clip.channels} ch" : "Missing clip", "muted")); details.Add(row);
            }
            var actions = new VisualElement(); actions.AddToClassList("button-row");
            actions.Add(new Button(() => CopyBrief(entry)) { text = "Copy brief for Codex" });
            actions.Add(new Button(() => ImportClip(entry)) { text = "Import recording…" });
            details.Add(actions);
            if (authored == null)
                details.Add(new Button(() => { try { MakeOverride(entry); Refresh(); } catch (Exception error) { SetStatus(error.Message); Debug.LogException(error); } }) { text = "Create editable override" });
            else
            {
                details.Add(Text("Project override · Undo supported · Save project to persist", "section-title"));
                var serialized = new SerializedObject(bank);
                int index = Array.FindIndex(bank.cues, c => c != null && c.sound == entry.id);
                var property = serialized.FindProperty("cues").GetArrayElementAtIndex(index);
                foreach (var name in new[] { "enabled", "displayName", "category", "triggerNotes", "variants", "volume" }) AddProperty(property, name);
                if (!entry.direct) foreach (var name in new[] { "bus", "output", "pitch", "loop", "cooldown", "maxVoices", "priority", "minDistance", "maxDistance" }) AddProperty(property, name);
                details.Bind(serialized);
                if (entry.fallback?.Invoke() != null) details.Add(new Button(() => {
                    if (!EditorUtility.DisplayDialog("Restore default sound?", "Remove this project override and use the original default. Your imported files are kept.", "Restore default", "Cancel")) return;
                    Undo.RecordObject(bank, "Restore sound default"); bank.cues = bank.cues.Where(c => c == null || c.sound != entry.id).ToArray(); EditorUtility.SetDirty(bank); Refresh();
                }) { text = "Restore default" });
            }
            var bindingFoldout = new Foldout { text = "Additional event bindings", value = false };
            details.Add(bindingFoldout);
            if (bank && bank.bindings != null)
            {
                var so = new SerializedObject(bank);
                for (int i = 0; i < bank.bindings.Length; i++) if (bank.bindings[i] != null && bank.bindings[i].sound == entry.id)
                {
                    int index = i;
                    var bindingPanel = new VisualElement();
                    var property = so.FindProperty("bindings").GetArrayElementAtIndex(i);
                    foreach (var name in new[] { "enabled", "trigger", "local", "localPlayerOnly", "filter", "minimumStrength", "intensity" })
                        bindingPanel.Add(new PropertyField(property.FindPropertyRelative(name)));
                    bindingPanel.Bind(so); bindingFoldout.Add(bindingPanel);
                    bindingFoldout.Add(new Button(() => { Undo.RecordObject(bank, "Remove sound trigger"); bank.bindings = bank.bindings.Where((b, n) => n != index).ToArray(); EditorUtility.SetDirty(bank); Show(entry); }) { text = "Remove this binding" });
                }
            }
            bindingFoldout.Add(Text("Existing game triggers are listed above and in source. Adding a binding layers another request; it does not replace existing gameplay logic.", "muted"));
            bindingFoldout.Add(new Button(() => AddBinding(entry)) { text = "Add event binding" });
            var sources = new Foldout { text = $"Source references ({entry.uses.Count}) · definitions and call sites", value = false };
            details.Add(sources);
            foreach (var use in entry.uses)
            {
                var captured = use;
                sources.Add(new Button(() => AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(captured.path), captured.line)) { text = Path.GetFileName(use.path) + ":" + use.line });
                sources.Add(Text(use.text, "source-line"));
            }
        }
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
        public CandyAudioCue MakeOverride(CandySoundEntry entry)
        {
            if (!bank) throw new InvalidOperationException("Choose the game's audio bank first.");
            var existing = CandySoundCatalog.Authored(bank, entry.id); if (existing != null) return existing;
            var cue = CandyAudioDefaults.Copy(entry.fallback?.Invoke()) ?? new CandyAudioCue { sound = entry.id };
            cue.sound = entry.id; cue.displayName = entry.title; cue.category = entry.category; cue.triggerNotes = entry.trigger;
            CandyStudioAssets.PersistGeneratedClips(bank, cue);
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
            details.Unbind(); details.Clear(); details.Add(Text("Mix and voice budgets", "detail-title"));
            details.Add(Text("These are the game's existing buses and bounded voice pool. No additional mixer, package or per-frame scanning is installed.", "callout"));
            if (!bank) return;
            var so = new SerializedObject(bank);
            foreach (var name in new[] { "master", "world", "characters", "ambience", "music", "ui", "oneShotVoices", "loopVoices", "eventsPerSecond" }) details.Add(new PropertyField(so.FindProperty(name)));
            details.Bind(so);
            details.Add(new Button(() => { var issues = CandyStudioAssets.Validate(bank); SetStatus(issues.Count == 0 ? "Bank checks passed." : string.Join(" | ", issues)); }) { text = "Check bank" });
        }
        void NewSound()
        {
            details.Unbind(); details.Clear(); details.Add(Text("New sound and trigger", "detail-title"));
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
        void ImportClip(CandySoundEntry entry)
        {
            string file = EditorUtility.OpenFilePanelWithFilters("Import sound", "", new[] { "Audio", "wav,ogg,mp3,aif,aiff" });
            if (string.IsNullOrEmpty(file)) return;
            string folder = CandyStudioAssets.RecordingFolder(bank);
            string target = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Path.GetFileName(file));
            File.Copy(file, target); AssetDatabase.ImportAsset(target);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(target);
            if (!clip) { SetStatus("Unity could not import this recording; see Console."); return; }
            var cue = MakeOverride(entry); Undo.RecordObject(bank, "Add recording variation");
            cue.variants = cue.variants.Concat(new[] { clip }).ToArray(); EditorUtility.SetDirty(bank); Show(entry);
        }
        void ShowLibrary()
        {
            details.Unbind(); details.Clear(); details.Add(Text("Imported clip library", "detail-title"));
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
                var cue = MakeOverride(entry); Undo.RecordObject(bank, "Replace sound recordings"); cue.variants = new[] { chosen }; EditorUtility.SetDirty(bank); Show(entry);
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
