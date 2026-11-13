using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.CandyAudio.Editor
{
    public sealed partial class CandySoundStudioWindow
    {
        CandyPreviewPlayer previewPlayer;
        Button tryGame;
        static Font friendlyFont;
        static Font FriendlyFont => friendlyFont ? friendlyFont : friendlyFont = Resources.Load<Font>("Fonts/Fredoka-Bold");

        void BuildStudio()
        {
            rootVisualElement.Clear(); rootVisualElement.AddToClassList("sound-studio");
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(ProjectPath.Find("CandySoundStudio.uss")));
            var header = new VisualElement(); header.AddToClassList("studio-header");
            var mark = new Label("♪"); mark.AddToClassList("studio-mark"); header.Add(mark);
            var title = new VisualElement(); title.AddToClassList("brand-copy");
            title.Add(Text("DEADLY JENGA", "eyebrow")); title.Add(Text("Sound Studio", "studio-title")); header.Add(title);
            var libraryPanel = Row("library-drawer"); libraryPanel.style.display = DisplayStyle.None;
            var library = Action("Library  ▾", () => libraryPanel.style.display = libraryPanel.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None); library.AddToClassList("library-menu");
            void LibraryAction(string label, Action callback) => libraryPanel.Add(Action(label, () => { libraryPanel.style.display = DisplayStyle.None; callback(); }, "secondary"));
            LibraryAction("All recordings", ShowLibrary);
            LibraryAction("Mix & voice budgets", ShowMix);
            LibraryAction("Live playback history", ShowHistory);
            LibraryAction("Project audio bank", ShowBank);
            LibraryAction("Import trigger previews…", ImportPreviews);
            LibraryAction("Export all capture briefs…", ExportBriefs);
            LibraryAction("Capture guide", () => OpenGuide());
            LibraryAction("Check preview setup", CandySoundPreviewChecks.Run);
            LibraryAction("Refresh catalog", () => { CandySoundCatalog.RefreshSourceIndex(); CandySoundPreview.Invalidate(); Refresh(); });
            header.Add(library);
            header.Add(Action("+ New sound", NewSound, "secondary"));
            tryGame = Action("Try in game", ApplyToGame, "secondary"); tryGame.SetEnabled(EditorApplication.isPlaying); header.Add(tryGame);
            header.Add(Action("Save changes", Save, "primary")); rootVisualElement.Add(header); rootVisualElement.Add(libraryPanel);
            if (!bank) bank = Resources.Load<CandyAudioBank>("CandyAudio/Default");

            var searchRow = new VisualElement(); searchRow.AddToClassList("search-row");
            searchRow.Add(Text("Find a sound", "search-caption"));
            searchField = new TextField(); searchField.AddToClassList("studio-search"); searchField.SetValueWithoutNotify(search);
            searchField.tooltip = "Search names, sections, actions and trigger descriptions";
            searchField.RegisterValueChangedCallback(e => { search = e.newValue; Filter(); }); searchRow.Add(searchField);
            summary = Text("", "muted"); searchRow.Add(summary); rootVisualElement.Add(searchRow);
            var columns = new VisualElement(); columns.AddToClassList("studio-columns");
            sections = new ScrollView(); sections.AddToClassList("studio-sections"); columns.Add(sections);
            sounds = new ListView { fixedItemHeight = 76, selectionType = SelectionType.Single }; sounds.AddToClassList("studio-sounds");
            sounds.makeItem = () => {
                var row = new VisualElement(); row.AddToClassList("sound-row");
                var icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit }; icon.AddToClassList("sound-icon"); row.Add(icon);
                var copy = new VisualElement(); copy.AddToClassList("sound-row-copy"); copy.Add(new Label { name = "name" });
                var meta = new Label { name = "meta" }; meta.AddToClassList("muted"); copy.Add(meta); row.Add(copy); return row;
            };
            sounds.bindItem = (row, index) => {
                var entry = visible[index]; var cue = CandySoundCatalog.Authored(bank, entry.id);
                row.Q<Label>("name").text = entry.title;
                row.Q<Label>("meta").text = (cue != null && !cue.enabled ? "Muted" : entry.category.Split('/').Last().Trim()) + (CandySoundPreview.Has((int)entry.id) ? "  ·  clip" : "");
                row.Q<Image>("icon").image = Art(entry);
            };
            sounds.selectionChanged += items => { if (items.FirstOrDefault() is CandySoundEntry entry) { selectedId = (int)entry.id; Show(entry); } };
            columns.Add(sounds); details = new ScrollView(); details.AddToClassList("studio-details"); columns.Add(details); rootVisualElement.Add(columns);
            status = Text("", "studio-status"); rootVisualElement.Add(status);
            liveText = new Label(); Refresh();
        }
        static Button Action(string text, Action callback, string kind = null)
        {
            var button = new Button(callback) { text = text }; if (kind != null) button.AddToClassList(kind);
            return button;
        }
        static VisualElement Row(string style = "button-row") { var row = new VisualElement(); row.AddToClassList(style); return row; }
        static VisualElement Card(string title = null)
        {
            var card = new VisualElement(); card.AddToClassList("studio-card"); if (title != null) card.Add(Text(title, "section-title")); return card;
        }
        static Texture2D Art(CandySoundEntry entry)
        {
            if (entry.category.StartsWith("Disasters / "))
            {
                string name = entry.category.Split('/').Last().Trim().ToLowerInvariant().Replace(" ", "");
                if (name == "ice") name = "icey";
                if (name == "meteorsandplanet") name = "meteorplanetkiller";
                if (name == "rewind") name = "timeslip";
                var hazard = Resources.Load<Texture2D>("DisasterWarnings/" + name); if (hazard) return hazard;
            }
            if (entry.category.StartsWith("Combat / "))
            {
                string name = entry.category.Substring(9).Replace(" ", "");
                var weapon = Resources.Load<Texture2D>("WeaponIcons/" + name); if (weapon) return weapon;
                return Resources.Load<Texture2D>("PlayerTabletArt/Weapons");
            }
            string group = entry.category.Split('/')[0].Trim();
            return Resources.Load<Texture2D>("PlayerTabletArt/" + (group == "Disasters" ? "Disasters" :
                group == "World" ? "Rebuild" : group == "Player" || group == "Wildlife" ? "Bots" : group == "UI" ? "Spectate" : "MapMode"));
        }
        void ClearDetails() { previewPlayer?.SetPlaying(false); previewPlayer = null; details.Unbind(); details.Clear(); }
        void SelectSection(string category)
        {
            section = category; search = ""; searchField.SetValueWithoutNotify(""); Filter();
            sections.Query<Button>().ForEach(b => b.EnableInClassList("is-current", b.userData as string == section));
        }
        void BuildSections()
        {
            sections.Clear(); sections.Add(Text("YOUR SOUND LIBRARY", "nav-title"));
            var all = Action("All sounds    " + entries.Count, () => SelectSection("All sounds")); all.userData = "All sounds";
            all.EnableInClassList("is-current", section == "All sounds"); sections.Add(all);
            foreach (var group in entries.GroupBy(e => e.category.Split('/')[0].Trim()))
            {
                var fold = new Foldout { text = group.Key, value = section.StartsWith(group.Key, StringComparison.Ordinal) };
                foreach (var category in group.GroupBy(e => e.category))
                {
                    string value = category.Key;
                    var button = Action(value.Split('/').Last().Trim() + "   " + category.Count(), () => SelectSection(value));
                    button.userData = value; button.AddToClassList("section-button"); button.EnableInClassList("is-current", section == value); fold.Add(button);
                }
                sections.Add(fold);
            }
        }
        void ShowSound(CandySoundEntry entry)
        {
            Stop(); take = 0; ClearDetails();
            var authored = CandySoundCatalog.Authored(bank, entry.id); resolved = authored ?? entry.fallback?.Invoke();
            details.Add(Text(entry.category.ToUpperInvariant(), "eyebrow"));
            var heading = Row("detail-heading"); heading.Add(Text(entry.title, "detail-title"));
            var badge = Text(authored != null && !authored.enabled ? "MUTED" : authored != null ? "PROJECT SOUND" : "ORIGINAL SOUND", "state-badge"); heading.Add(badge); details.Add(heading);

            var hero = Row("sound-hero"); var visual = Card(); visual.AddToClassList("visual-card");
            var preview = CandySoundPreview.Find((int)entry.id);
            if (preview && preview.atlas)
            {
                previewPlayer = new CandyPreviewPlayer(preview); previewPlayer.SetFocused(focusedWindow == this); visual.Add(previewPlayer);
                var tag = Text(preview.packet.captureKind == "staged-gameplay" ? "STAGED GAMEPLAY" : "IN-GAME CAPTURE", "preview-tag"); visual.Add(tag);
                visual.Add(Text(preview.packet.action, "preview-caption"));
            }
            else
            {
                var stage = new VisualElement(); stage.AddToClassList("preview-empty");
                var art = new Image { image = Art(entry), scaleMode = ScaleMode.ScaleToFit }; art.AddToClassList("preview-illustration"); stage.Add(art);
                stage.Add(Text("Trigger clip coming next", "preview-empty-title"));
                stage.Add(Text("Illustration · gameplay preview not captured", "muted")); visual.Add(stage);
            }
            var clipActions = Row(); clipActions.Add(Action("Capture brief", () => { GUIUtility.systemCopyBuffer = CandySoundCaptureBriefs.Text(entry); SetStatus("Capture brief copied — source, action, target, camera and exact moment."); }, "subtle"));
            clipActions.Add(Action("Import clip…", ImportPreviews, "subtle"));
            if (previewPlayer != null) clipActions.Add(Action("Show event", () => previewPlayer?.ShowEvent(), "subtle"));
            visual.Add(clipActions); hero.Add(visual);

            var listen = Card("Listen"); listen.AddToClassList("listen-card");
            var waveform = new CandySoundWaveform(); waveform.SetClip(resolved?.variants?.FirstOrDefault()); listen.Add(waveform);
            var play = Row(); play.Add(Action("▶  Play sound", () => Audition(resolved, take), "primary")); play.Add(Action("■  Stop", Stop, "secondary")); listen.Add(play);
            if (resolved?.variants?.Length > 0)
            {
                var names = resolved.variants.Select((c, i) => "Take " + (i + 1) + " · " + (c ? c.name : "Missing recording")).ToList();
                var choice = new DropdownField(names, 0); choice.AddToClassList("take-picker");
                choice.RegisterValueChangedCallback(e => { take = choice.index; waveform.SetClip(resolved.variants[take]); }); listen.Add(choice);
                var info = Text("", "muted");
                void UpdateInfo() { var c = resolved.variants[take]; info.text = c ? $"{c.length:0.00}s · {c.frequency / 1000f:0.#} kHz · {(resolved.loop ? "Loop" : "One shot")}" : "Missing recording"; }
                choice.RegisterValueChangedCallback(_ => UpdateInfo()); UpdateInfo(); listen.Add(info);
                listen.Add(Action("Next variation  →", () => { choice.index = (choice.index + 1) % names.Count; Audition(resolved, take); }, "subtle"));
            }
            else listen.Add(Text("No recording assigned", "muted"));
            listen.Add(Text("Raw sound preview", "tiny-label"));
            var replace = Row(); replace.Add(Action("Replace recording…", () => ImportClip(entry, true), "secondary"));
            replace.Add(Action("+ Variation", () => ImportClip(entry, false), "subtle")); listen.Add(replace);
            listen.Add(Action("Browse recordings", ShowLibrary, "subtle")); hero.Add(listen); details.Add(hero);

            var b = CandySoundCaptureBriefs.For(entry);
            var when = Card("When it plays"); when.Add(Text(b.verifyCaller ? entry.trigger : b.action, "trigger-summary"));
            var explanation = new Foldout { text = "Trigger details", value = false }; explanation.Add(Text(entry.trigger, "body-copy")); when.Add(explanation); details.Add(when);
            if (entry.direct) details.Add(Text("Scene audio · new recordings take effect when this scene restarts.", "inline-note"));
            if (authored == null)
                details.Add(Action("Customize this sound", () => { try { MakeOverride(entry); Refresh(); } catch (Exception ex) { SetStatus(ex.Message); } }, "secondary"));
            else
            {
                var settings = Card("Sound settings"); var so = new SerializedObject(bank);
                var property = so.FindProperty("cues").GetArrayElementAtIndex(Array.FindIndex(bank.cues, c => c != null && c.sound == entry.id));
                settings.Add(new PropertyField(property.FindPropertyRelative("enabled"), "Sound enabled"));
                settings.Add(new PropertyField(property.FindPropertyRelative("volume"), entry.direct ? "Volume multiplier" : "Volume"));
                var recordings = new Foldout { text = "Recordings & variations", value = false }; recordings.Add(new PropertyField(property.FindPropertyRelative("variants"), "Recordings")); settings.Add(recordings);
                if (!entry.direct)
                {
                    var mix = new Foldout { text = "Mix, distance & playback limits", value = false };
                    foreach (string name in new[] { "bus", "output", "pitch", "loop", "cooldown", "maxVoices", "priority", "minDistance", "maxDistance" }) mix.Add(new PropertyField(property.FindPropertyRelative(name)));
                    settings.Add(mix);
                }
                var naming = new Foldout { text = "Name, category & notes", value = false };
                foreach (string name in new[] { "displayName", "category", "triggerNotes" }) naming.Add(new PropertyField(property.FindPropertyRelative(name)));
                settings.Add(naming); settings.Bind(so); details.Add(settings);
            }
            AddBindings(entry);
            var more = new Foldout { text = "Source & tools", value = false }; more.AddToClassList("advanced-card");
            more.Add(Text("Stable cue ID: " + (int)entry.id, "muted"));
            more.Add(Action("Copy sound-design brief", () => CopyBrief(entry), "subtle"));
            foreach (var use in entry.uses)
            {
                var captured = use;
                more.Add(Action(Path.GetFileName(use.path) + ":" + use.line, () => AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(captured.path), captured.line), "source-button"));
                more.Add(Text(use.text, "source-line"));
            }
            if (preview)
            {
                more.Add(Text("Capture: " + preview.packet.sourceCommit + " · " + preview.packet.map + "\n" + preview.packet.triggerEvidence, "source-line"));
                if (!string.IsNullOrEmpty(preview.packet.viewerUrl)) more.Add(Action("Open uploaded GIF", () => Application.OpenURL(preview.packet.viewerUrl), "subtle"));
            }
            if (authored != null && entry.fallback?.Invoke() != null) more.Add(Action("Restore original sound", () => {
                if (!EditorUtility.DisplayDialog("Restore original?", "Remove this sound override? Imported recordings will be kept.", "Restore", "Cancel")) return;
                Undo.RecordObject(bank, "Restore sound default"); bank.cues = bank.cues.Where(c => c == null || c.sound != entry.id).ToArray(); EditorUtility.SetDirty(bank); Refresh();
            }, "subtle"));
            details.Add(more);
        }
        void AddBindings(CandySoundEntry entry)
        {
            var fold = new Foldout { text = "Additional triggers", value = false }; fold.AddToClassList("advanced-card"); details.Add(fold);
            if (bank && bank.bindings != null)
            {
                var so = new SerializedObject(bank);
                for (int i = 0; i < bank.bindings.Length; i++) if (bank.bindings[i] != null && bank.bindings[i].sound == entry.id)
                {
                    int index = i; var panel = Card(); var property = so.FindProperty("bindings").GetArrayElementAtIndex(i);
                    foreach (string name in new[] { "enabled", "trigger", "local", "localPlayerOnly", "filter", "minimumStrength", "intensity" }) panel.Add(new PropertyField(property.FindPropertyRelative(name)));
                    panel.Bind(so); fold.Add(panel);
                    fold.Add(Action("Remove this trigger", () => { Undo.RecordObject(bank, "Remove sound trigger"); bank.bindings = bank.bindings.Where((v, n) => n != index).ToArray(); EditorUtility.SetDirty(bank); Show(entry); }, "subtle"));
                }
            }
            fold.Add(Action("+ Add event trigger", () => AddBinding(entry), "secondary"));
            fold.Add(Text("Adds another event request. Existing game triggers remain in place.", "muted"));
        }
        void ShowBank()
        {
            ClearDetails(); details.Add(Text("Project audio bank", "detail-title"));
            var field = new ObjectField("Bank") { objectType = typeof(CandyAudioBank), allowSceneObjects = false, value = bank };
            field.RegisterValueChangedCallback(e => { bank = e.newValue as CandyAudioBank; Refresh(); }); details.Add(field);
            details.Add(Text("The game uses CandyAudio/Default unless another bank is assigned. Save project edits before building.", "body-copy"));
        }
        void ImportPreviews()
        {
            string path = EditorUtility.OpenFolderPanel("Choose the folder containing preview.json packages", "", ""); if (string.IsNullOrEmpty(path)) return;
            int imported = 0; var errors = new System.Collections.Generic.List<string>();
            foreach (string file in Directory.EnumerateFiles(path, "preview.json", SearchOption.AllDirectories))
                try { CandySoundPreview.Import(file, bank); imported++; } catch (Exception ex) { errors.Add(Path.GetFileName(Path.GetDirectoryName(file)) + ": " + ex.Message); }
            Refresh(); SetStatus($"Imported {imported} trigger previews." + (errors.Count > 0 ? " Rejected: " + string.Join(" | ", errors.Take(3)) : ""));
            if (errors.Count > 0) Debug.LogWarning(string.Join("\n", errors));
        }
        void ExportBriefs()
        {
            string folder = EditorUtility.OpenFolderPanel("Export capture briefs", "", ""); if (string.IsNullOrEmpty(folder)) return;
            CandySoundCaptureBriefs.Export(folder, bank); SetStatus("Capture briefs exported with stable cue IDs and contextual shot requirements.");
        }
        static void OpenGuide() => AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ProjectPath.Find("SOUND_PREVIEW_CAPTURE_GUIDE.md")));
    }

    sealed class CandySoundWaveform : VisualElement
    {
        readonly float[] peaks = new float[48]; bool readable;
        public CandySoundWaveform() { AddToClassList("sound-waveform"); generateVisualContent += Draw; }
        public void SetClip(AudioClip clip)
        {
            Array.Clear(peaks, 0, peaks.Length); readable = clip && clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.loadState == AudioDataLoadState.Loaded;
            if (readable)
            {
                int count = Mathf.Min(128, clip.samples); var samples = new float[count * clip.channels];
                for (int i = 0; i < peaks.Length; i++)
                {
                    int offset = Mathf.RoundToInt((clip.samples - count) * i / (float)(peaks.Length - 1));
                    if (!clip.GetData(samples, offset)) { readable = false; break; }
                    foreach (float value in samples) peaks[i] = Mathf.Max(peaks[i], Mathf.Abs(value));
                }
            }
            tooltip = readable ? "Sampled waveform of the selected recording" : "Waveform unavailable for this clip's loading format"; MarkDirtyRepaint();
        }
        void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height; if (w < 1 || h < 1) return;
            var p = context.painter2D; p.lineCap = LineCap.Round; p.lineWidth = Mathf.Max(2, w / 90);
            float maximum = Mathf.Max(.01f, peaks.Max());
            for (int i = 0; i < peaks.Length; i++)
            {
                float x = 8 + (w - 16) * i / (peaks.Length - 1), size = readable ? Mathf.Max(2, peaks[i] / maximum * (h - 20) * .45f) : 1;
                p.strokeColor = i < 18 ? new Color(.96f,.39f,.47f) : new Color(.23f,.75f,.68f);
                p.BeginPath(); p.MoveTo(new Vector2(x,h/2-size)); p.LineTo(new Vector2(x,h/2+size)); p.Stroke();
            }
        }
    }
}
