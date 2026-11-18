using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The Studio window: top bar, cast list, viewport, inspector, transport and timeline (see UI.md).
    // This file is the shell. Modes, inspectors, overlays and commands live in their own files and
    // register themselves (StudioExtensions).
    public sealed partial class TrailerStudioWindow : EditorWindow
    {
        [SerializeField] string setId = "";
        [SerializeField] string modeTitle = "Set";
        [SerializeField] string selectionKind = "", selectionId = "";
        [SerializeField] StudioUndoState undo;
        [SerializeField] int offlineFrame, inFrame, outFrame = -1;
        [SerializeField] bool loop;
        [SerializeField] float speed = 1;

        StudioSet loadedSet;
        StudioTape offlineTape;
        StudioContext context;
        object selection;
        StudioState shownState = StudioState.Closed;
        string shownSignature = "";
        bool playing, stopAsked;

        VisualElement root, tabs, chips, modePanel, inspector, transport, floating;
        Button setPicker, stageButton, recordButton;
        Label status, recordLabel, timeLabel;
        StudioViewport viewport;
        StudioTimeline timeline;
        StudioCastList cast;
        StudioPalette palette;
        string message = "";

        public StudioSet Set => StudioRun.Stage != null && StudioRun.Stage.set.id == setId ? StudioRun.Stage.set : loadedSet;
        public StudioTape Tape => StudioRun.Stage != null && StudioRun.Stage.set.id == setId ? StudioRun.Stage.tape : offlineTape;
        public int Frame => StudioRun.StageOpen ? StudioRun.Frame : offlineFrame;
        public object Selection => selection;
        public StudioContext Context => context;
        public StudioViewport Viewport => viewport;

        // Redraw only what follows a value while it is being dragged (no list rebuilds).
        public void LightRefresh()
        {
            viewport?.Tick();
            timeline?.Rebuild();
        }
        public IStudioMode Mode => StudioExtensions.Modes.FirstOrDefault(m => m.Available && m.Title == modeTitle)
            ?? StudioExtensions.Modes.FirstOrDefault(m => m.Available);
        public int InFrame => Mathf.Clamp(inFrame, 0, Mathf.Max(0, (Set?.Frames ?? 1) - 1));
        public int OutFrame => outFrame < 0 || Set == null ? (Set?.Frames ?? 1) - 1 : Mathf.Clamp(outFrame, InFrame, Set.Frames - 1);

        [MenuItem("Tools/Deadly Jenga/Trailer Studio")]
        public static void Open()
        {
            var window = GetWindow<TrailerStudioWindow>("Trailer Studio");
            window.minSize = new Vector2(900, 560);
        }

        void OnEnable()
        {
            context = new StudioContext { window = this };
            StudioRun.Changed += OnRunSignal;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            StudioRun.Changed -= OnRunSignal;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void CreateGUI()
        {
            context ??= new StudioContext { window = this };
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ProjectPath.Find("TrailerStudio.uxml"));
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProjectPath.Find("TrailerStudio.uss"));
            rootVisualElement.Clear();
            layout.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(style);
            root = rootVisualElement.Q("root");
            root.EnableInClassList("ts-light", !EditorGUIUtility.isProSkin);

            tabs = root.Q("tabs"); chips = root.Q("chips");
            modePanel = root.Q("mode-panel"); inspector = root.Q("inspector");
            transport = root.Q("transport"); floating = root.Q("floating");
            setPicker = root.Q<Button>("set-picker"); stageButton = root.Q<Button>("stage"); recordButton = root.Q<Button>("record");
            status = root.Q<Label>("status"); recordLabel = root.Q<Label>("record-label");

            setPicker.clicked += ShowSetMenu;
            stageButton.clicked += ToggleStage;
            recordButton.clicked += PressRecord;
            root.Q<Button>("undo").clicked += Undo.PerformUndo;
            root.Q<Button>("redo").clicked += Undo.PerformRedo;
            root.Q<Button>("help").clicked += () => palette.ShowShortcuts();

            cast = new StudioCastList(context, root.Q<ScrollView>("cast"));
            viewport = new StudioViewport(context, root.Q("viewport"));
            timeline = new StudioTimeline(context, root.Q("timeline"));
            palette = new StudioPalette(context, floating);
            BuildTransport();

            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            // Keep the picture and the playhead fresh while the stage is open.
            root.schedule.Execute(Pulse).Every(33);

            if (string.IsNullOrEmpty(setId) || StudioLibrary.Load(setId) == null)
                setId = StudioRun.Stage?.set.id ?? StudioLibrary.SetIds().FirstOrDefault() ?? "";
            LoadSet(setId);
            RebuildAll();
        }

        // ---- The Set ----

        public void LoadSet(string id)
        {
            setId = id ?? "";
            loadedSet = string.IsNullOrEmpty(setId) ? null : StudioLibrary.Load(setId);
            offlineTape = null;
            if (loadedSet != null)
            {
                string file = StudioLibrary.TapePath(loadedSet.id);
                try { if (StudioTape.IsTape(file)) offlineTape = StudioTape.Load(file); }
                catch (Exception error) { Say("The tape could not be read: " + error.Message); }
                offlineTape ??= StudioRun.LoadOrCreateTape(loadedSet, null);
                foreach (var type in StudioTrackTypes.All) type.EnsureTracks(loadedSet, offlineTape);
            }
            SyncUndo();
            RestoreSelection();
        }

        void SyncUndo()
        {
            if (!undo) { undo = CreateInstance<StudioUndoState>(); undo.hideFlags = HideFlags.HideAndDontSave; }
            undo.setId = Set?.id ?? "";
            undo.json = Set == null ? "" : JsonUtility.ToJson(Set);
        }

        // Change the Set in one undoable step, save it and redraw.
        public void Edit(string label, Action change, bool structural)
        {
            if (Set == null) return;
            BeginEdit(label);
            change();
            EndEdit(structural);
        }

        // For drags: BeginEdit once, change the Set freely while dragging, EndEdit on release.
        public void BeginEdit(string label)
        {
            if (!undo) SyncUndo();
            // Each edit is its own undo step, also when two edits happen without a click in between.
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(undo, label);
        }

        public void EndEdit(bool structural)
        {
            if (Set == null) return;
            undo.setId = Set.id; undo.json = JsonUtility.ToJson(Set);
            EditorUtility.SetDirty(undo);
            StudioLibrary.Save(Set);
            if (structural)
            {
                if (StudioRun.State == StudioState.Held) StudioRun.Refresh();
                else if (offlineTape != null) foreach (var type in StudioTrackTypes.All) type.EnsureTracks(Set, offlineTape);
            }
            RebuildAll();
        }

        void OnUndoRedo()
        {
            if (!undo || Set == null || undo.setId != Set.id || undo.json == JsonUtility.ToJson(Set) || string.IsNullOrEmpty(undo.json)) return;
            JsonUtility.FromJsonOverwrite(undo.json, Set);
            Set.id = undo.setId;
            StudioLibrary.Save(Set);
            RestoreSelection();
            if (StudioRun.State == StudioState.Held) StudioRun.Refresh();
            RebuildAll();
        }

        // ---- Selection ----

        public void Select(object thing)
        {
            selection = thing;
            selectionKind = thing is StudioMark ? "mark" : thing is StudioCue ? "cue" : thing is StudioTrack ? "track" : thing is StudioCameraTrack ? "camera" : thing is StudioOverride ? "override" : "";
            selectionId = thing is StudioMark mark ? mark.id : thing is StudioCue cue ? cue.id : thing is StudioTrack track ? track.id
                : thing is StudioCameraTrack camera ? camera.id : thing is StudioOverride layer ? layer.id : "";
            RebuildAll();
        }

        void RestoreSelection()
        {
            selection = selectionKind == "mark" ? Set?.Mark(selectionId)
                : selectionKind == "cue" ? Set?.cues.FirstOrDefault(c => c.id == selectionId)
                : selectionKind == "camera" ? Set?.cameras.FirstOrDefault(c => c.id == selectionId)
                : selectionKind == "override" ? Set?.overrides.FirstOrDefault(o => o.id == selectionId)
                : selectionKind == "track" ? (object)Tape?.Find(selectionId) : null;
        }

        // ---- Drawing ----

        public void Say(string text) { message = text ?? ""; if (status != null) status.text = message; }

        public void RebuildAll()
        {
            if (root == null) return;
            if (selection != null) RestoreSelection();   // objects are replaced by undo and by opening the stage
            shownState = StudioRun.State;
            shownSignature = Signature();
            RefreshTopBar();
            cast.Rebuild();
            modePanel.Clear();
            Mode?.Build(modePanel, context);
            inspector.Clear();
            var chosen = StudioExtensions.Inspectors.FirstOrDefault(i => i.Handles(selection, context));
            if (chosen != null) chosen.Build(inspector, selection, context);
            timeline.Rebuild();
            viewport.Rebuild();
            RefreshTransport();
        }

        // Changes when takes or tracks change, so the window knows to redraw its lists.
        string Signature() => Tape == null ? "" : string.Join(",", Tape.tracks.Select(t => t.id + ":" + t.takes.Count + ":" + t.active + ":" + t.badge));

        // The run reports changes from inside the game loop, on frames that are being recorded. Rebuilding
        // the window there would cost time on those frames, so the change is only noted and Pulse does the work.
        bool runChanged;
        void OnRunSignal() => runChanged = true;

        void OnRunChanged()
        {
            if (root == null) return;
            bool wasPlaying = playing && shownState == StudioState.Running && StudioRun.State == StudioState.Held;
            if (StudioRun.State != shownState || Signature() != shownSignature)
            {
                if (StudioRun.Stage != null && string.IsNullOrEmpty(setId)) setId = StudioRun.Stage.set.id;
                if (StudioRun.State == StudioState.Closed && shownState != StudioState.Closed) LoadSet(setId);
                RebuildAll();
            }
            if (!string.IsNullOrEmpty(StudioRun.Status)) Say(StudioRun.Status);
            RefreshLive();
            if (wasPlaying)
            {
                playing = false;
                if (loop && !stopAsked && StudioRun.State == StudioState.Held) StartPlay(InFrame);
            }
        }

        void Pulse()
        {
            if (root == null) return;
            if (StudioRun.StageOpen) viewport.Tick();
            if (runChanged || StudioRun.State != shownState) { runChanged = false; OnRunChanged(); }
        }

        void RefreshTopBar()
        {
            setPicker.text = Set == null ? "Choose a Set" : Set.title;
            tabs.Clear();
            var current = Mode;
            foreach (var mode in StudioExtensions.Modes.Where(m => m.Available))
            {
                var captured = mode;
                var tab = new Button(() => { modeTitle = captured.Title; RebuildAll(); }) { text = mode.Title };
                tab.AddToClassList("ts-tab");
                tab.EnableInClassList("ts-tab--on", mode == current);
                tabs.Add(tab);
            }
            stageButton.text = StudioRun.State == StudioState.Closed ? "Open stage" : StudioRun.State == StudioState.Opening ? "Opening…" : "Close stage";
            stageButton.SetEnabled(Set != null && StudioRun.State != StudioState.Opening);
            stageButton.tooltip = StudioRun.State == StudioState.Closed ? "Load this Set's map in Play Mode so you can place marks, record and play." : "Leave Play Mode. Your Set and takes are already saved.";
            RefreshLive();
        }

        // The parts that change every frame while running.
        void RefreshLive()
        {
            bool held = StudioRun.State == StudioState.Held, recording = StudioRun.Recording;
            string label = "Record whole set";
            if (Tape != null) label += ", take " + ((Tape.FindType(WorldTrack.Id)?.takes.Count ?? 0) + 1);
            if (Mode != null && Mode.TryGetRecord(context, out string modeLabel, out _)) label = modeLabel;
            recordLabel.text = recording ? "Stop  " + StudioUi.Time(StudioRun.Frame, Set?.fps ?? 30) : label;
            recordButton.EnableInClassList("ts-record--on", recording);
            recordButton.SetEnabled(held || StudioRun.State == StudioState.Running);
            recordButton.tooltip = held ? "Record (R)" : StudioRun.State == StudioState.Closed ? "Open the stage to record." : "Stop (R)";

            chips.Clear();
            void Chip(string text, string kind = "", string tip = "")
            {
                var chip = new Label(text) { tooltip = tip };
                chip.AddToClassList("ts-chip");
                if (kind.Length > 0) chip.AddToClassList("ts-chip--" + kind);
                chips.Add(chip);
            }
            if (recording) Chip("REC", "rec");
            else if (StudioRun.State == StudioState.Running) Chip("Playing");
            if (Tape != null && Tape.tracks.Any(t => t.takes.Count > 0))
            {
                long bytes = File.Exists(Tape.path) ? new FileInfo(Tape.path).Length : Tape.Bytes;
                Chip("Tape " + StudioTape.Size(bytes), "", "Size of this Set's tape on disk.");
            }
            var drift = StudioRun.LastDrift;
            if (drift != null && drift.samples > 0 && StudioRun.StageOpen)
                Chip(drift.Passed ? "No drift" : "Drifted", drift.Passed ? "good" : "warn", drift.ToString());
            if (StudioRender.Running) Chip(StudioRender.Progress, "", "Rendering in the background. The window stays usable.");
            if (StudioVariations.Running) Chip($"Variations {StudioVariations.Done}/{StudioVariations.Total}", "", "Variation runs are queued one after another.");
            if (StudioRun.RealTimeWarning && StudioRun.StageOpen) Chip("Slower than real time", "warn", "The last playback could not keep up on this machine. Renders are still exact.");
            int problems = Set?.Problems().Count ?? 0;
            if (problems > 0) Chip(problems + (problems == 1 ? " problem" : " problems"), "warn", string.Join("\n", Set.Problems()));

            RefreshTransport();
            timeline.MovePlayhead();
        }

        // ---- Stage, record, play ----

        void Try(Action action)
        {
            try { action(); }
            catch (Exception error) { Say(error.Message); }
        }

        void ToggleStage() => Try(() =>
        {
            if (StudioRun.State == StudioState.Closed) { if (Set != null) StudioRun.OpenStage(Set); }
            else StudioRun.CloseStage();
        });

        public void PressRecord() => Try(() =>
        {
            if (StudioRun.State == StudioState.Running) { stopAsked = true; StudioRun.Stop(); return; }
            if (StudioRun.State != StudioState.Held) { Say("Open the stage to record."); return; }
            playing = false;
            if (Mode != null && Mode.TryGetRecord(context, out _, out Action start) && start != null) start();
            else StudioRun.RecordWholeSet();
        });

        public void TogglePlay() => Try(() =>
        {
            if (StudioRun.State == StudioState.Running) { stopAsked = true; StudioRun.Stop(); return; }
            if (StudioRun.State != StudioState.Held) { Say("Open the stage to play."); return; }
            StartPlay(Frame >= OutFrame ? InFrame : Mathf.Max(Frame, InFrame));
        });

        void StartPlay(int from)
        {
            stopAsked = false; playing = true;
            StudioRun.Play(from, OutFrame, true, speed);
        }

        public void Scrub(int frame)
        {
            if (Set == null) return;
            frame = Mathf.Clamp(frame, 0, Set.Frames - 1);
            if (StudioRun.State == StudioState.Held) StudioRun.Scrub(frame);
            else if (!StudioRun.StageOpen) { offlineFrame = frame; RefreshLive(); }
        }

        // ---- Transport ----

        Button playButton, loopButton, speedButton;
        Label rangeLabel;

        void BuildTransport()
        {
            transport.Clear();
            transport.Add(StudioUi.Small("|<", () => Scrub(InFrame), "Go to the start (Home)"));
            transport.Add(StudioUi.Small("-1s", () => Scrub(Frame - (Set?.fps ?? 30)), "Back one second (J)"));
            playButton = StudioUi.Small("Play", TogglePlay, "Play or pause (Space)");
            playButton.style.minWidth = 54;
            transport.Add(playButton);
            transport.Add(StudioUi.Small("+1s", () => Scrub(Frame + (Set?.fps ?? 30)), "Forward one second (L)"));
            transport.Add(StudioUi.Small(">|", () => Scrub(OutFrame), "Go to the end (End)"));
            timeLabel = new Label(); timeLabel.AddToClassList("ts-time");
            transport.Add(timeLabel);
            speedButton = StudioUi.Small("1×", () =>
            {
                var menu = new GenericMenu();
                foreach (float choice in new[] { .25f, .5f, 1f, 2f })
                {
                    float captured = choice;
                    menu.AddItem(new GUIContent(SpeedName(choice)), Mathf.Approximately(speed, choice), () => { speed = captured; RefreshTransport(); });
                }
                menu.ShowAsContext();
            }, "Playback speed");
            transport.Add(speedButton);
            loopButton = StudioUi.Small("Loop", () => { loop = !loop; RefreshTransport(); }, "Play the range again and again");
            transport.Add(loopButton);
            rangeLabel = StudioUi.Hint("");
            rangeLabel.style.marginLeft = 12;
            transport.Add(rangeLabel);
        }

        static string SpeedName(float value) => Mathf.Approximately(value, .25f) ? "¼×" : Mathf.Approximately(value, .5f) ? "½×" : value.ToString("0.#") + "×";

        void RefreshTransport()
        {
            if (timeLabel == null) return;
            int fps = Set?.fps ?? 30, frames = Set?.Frames ?? 1;
            timeLabel.text = StudioUi.Time(Frame, fps) + " / " + StudioUi.Time(frames, fps);
            playButton.text = StudioRun.State == StudioState.Running && !StudioRun.Recording ? "Pause" : "Play";
            loopButton.EnableInClassList("ts-small--on", loop);
            speedButton.text = SpeedName(speed);
            bool ranged = inFrame > 0 || outFrame >= 0;
            rangeLabel.text = !StudioRun.StageOpen ? "Open the stage to play." :
                ranged ? $"Range {StudioUi.Time(InFrame, fps)} to {StudioUi.Time(OutFrame, fps)}  ([ and ] set it, \\ clears it)" : "";
        }

        // ---- Keyboard ----

        void OnKey(KeyDownEvent e)
        {
            if (palette.HandleKey(e)) return;
            // Do not steal keys while someone types in a field.
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            for (var element = focused; element != null; element = element.parent)
                if (element.ClassListContains("unity-base-text-field")) return;

            bool used = true;
            if (e.keyCode == KeyCode.K && (e.ctrlKey || e.commandKey)) palette.ShowCommands();
            else if (e.character == '?' || e.keyCode == KeyCode.Slash && e.shiftKey) palette.ShowShortcuts();
            else if (e.ctrlKey || e.commandKey || e.altKey) used = false;
            else if (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.K) TogglePlay();
            else if (e.keyCode == KeyCode.R) PressRecord();
            else if (e.keyCode == KeyCode.J) Scrub(Frame - (Set?.fps ?? 30));
            else if (e.keyCode == KeyCode.L) Scrub(Frame + (Set?.fps ?? 30));
            else if (e.keyCode == KeyCode.LeftArrow) Scrub(Frame - 1);
            else if (e.keyCode == KeyCode.RightArrow) Scrub(Frame + 1);
            else if (e.keyCode == KeyCode.Home) Scrub(InFrame);
            else if (e.keyCode == KeyCode.End) Scrub(OutFrame);
            else if (e.keyCode == KeyCode.LeftBracket) { inFrame = Frame; if (outFrame >= 0 && outFrame < inFrame) outFrame = -1; RebuildAll(); }
            else if (e.keyCode == KeyCode.RightBracket) { outFrame = Mathf.Max(Frame, inFrame); RebuildAll(); }
            else if (e.keyCode == KeyCode.Backslash) { inFrame = 0; outFrame = -1; RebuildAll(); }
            else if (e.keyCode == KeyCode.Tab) CycleMode(e.shiftKey ? -1 : 1);
            else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) DeleteSelection();
            else if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9) PickTake(e.keyCode - KeyCode.Alpha1);
            else used = false;
            if (used) { e.StopPropagation(); e.PreventDefault(); }
        }

        void CycleMode(int step)
        {
            var modes = StudioExtensions.Modes.Where(m => m.Available).ToList();
            if (modes.Count == 0) return;
            int index = Mathf.Max(0, modes.IndexOf(Mode));
            modeTitle = modes[(index + step + modes.Count) % modes.Count].Title;
            RebuildAll();
        }

        public void ShowMode(string title) { modeTitle = title; RebuildAll(); }

        // Open the command palette (true) or the shortcut sheet (false), and close either.
        public void ShowPalette(bool commands) { if (commands) palette.ShowCommands(); else palette.ShowShortcuts(); }
        public void ClosePalette() => palette.Close();

        // Number keys pick a take of the selected track (or the selected actor's track).
        void PickTake(int index)
        {
            var track = selection as StudioTrack ?? (selection is StudioMark mark ? Tape?.Find(mark.id) : null);
            if (track == null) return;
            var kept = track.Kept.ToList();
            if (index >= kept.Count) return;
            SetActiveTake(track, track.takes.IndexOf(kept[index]));
        }

        public void SetActiveTake(StudioTrack track, int index)
        {
            track.active = index;
            TapeChanged();
        }

        // A track setting changed (mute, solo, lock, active take, take names): save the tape and show it.
        public void TapeChanged()
        {
            Try(() =>
            {
                if (StudioRun.StageOpen) { StudioRun.SaveTape(); if (StudioRun.State == StudioState.Held) StudioRun.Refresh(); }
                else if (Tape != null && Tape.tracks.Any(t => t.takes.Count > 0)) Tape.Save(StudioLibrary.TapePath(Set.id));
            });
            RebuildAll();
        }

        void DeleteSelection()
        {
            if (selection is StudioMark mark) Edit("Delete " + mark.actor, () =>
            {
                Set.marks.Remove(mark);
                // Take away what pointed at this actor. Its takes stay on the tape, and come back if the delete is undone.
                Set.overrides.RemoveAll(o => o.actor == mark.id);
                foreach (var camera in Set.cameras)
                {
                    if (camera.subject == mark.id) { camera.subject = ""; if (camera.behavior != "twoShot") camera.behavior = ""; }
                    if (camera.second == mark.id) camera.second = "";
                    if (camera.behavior == "twoShot" && (camera.subject == "" || camera.second == "")) camera.behavior = "";
                }
                selection = null; selectionKind = "";
            }, true);
            else if (selection is StudioCue cue) Edit("Delete cue", () => { Set.cues.Remove(cue); selection = null; selectionKind = ""; }, false);
            else if (selection is StudioCameraTrack camera) Edit("Delete camera", () => { Set.cameras.Remove(camera); selection = null; selectionKind = ""; }, false);
            else if (selection is StudioOverride layer) Edit("Delete override", () => { Set.overrides.Remove(layer); selection = null; selectionKind = ""; }, false);
        }

        // ---- Set menu ----

        void ShowSetMenu()
        {
            var menu = new GenericMenu();
            bool closed = StudioRun.State == StudioState.Closed;
            foreach (string id in StudioLibrary.SetIds())
            {
                var entry = StudioLibrary.Load(id);
                string captured = id;
                if (closed) menu.AddItem(new GUIContent(entry?.title ?? id), id == setId, () => { LoadSet(captured); selection = null; RebuildAll(); });
                else menu.AddDisabledItem(new GUIContent(entry?.title ?? id), id == setId);
            }
            menu.AddSeparator("");
            if (closed)
            {
                menu.AddItem(new GUIContent("New set"), false, () => { LoadSet(StudioLibrary.Create("Untitled set").id); selection = null; RebuildAll(); Say("New Set made. Give it a name in the inspector."); });
                if (Set != null) menu.AddItem(new GUIContent("Duplicate this set"), false, () => { LoadSet(StudioLibrary.Duplicate(Set).id); RebuildAll(); });
                menu.AddItem(new GUIContent("Import a Gameplay Theatre recipe…"), false, ImportRecipe);
                menu.AddItem(new GUIContent("Import a Gameplay Theatre take folder…"), false, ImportTake);
                if (Set != null) menu.AddItem(new GUIContent("Move this set to the trash"), false, () =>
                {
                    StudioLibrary.Trash(Set);
                    LoadSet(StudioLibrary.SetIds().FirstOrDefault() ?? ""); selection = null; RebuildAll();
                    Say("Set moved to the trash folder beside its tapes. Its takes were kept.");
                });
            }
            else menu.AddDisabledItem(new GUIContent("Close the stage to switch, add or import Sets"));
            menu.DropDown(setPicker.worldBound);
        }

        public void ImportRecipe() => Try(() =>
        {
            string file = EditorUtility.OpenFilePanel("Gameplay Theatre recipe", ProjectPath.FolderOf("GameplayTheatre.md"), "json");
            if (file.Length == 0) return;
            var plan = JsonUtility.FromJson<TheatrePlan>(File.ReadAllText(file));
            if (plan == null) throw new InvalidDataException("That file is not a Theatre recipe.");
            var set = TheatreImport.RecipeToSet(plan);
            StudioLibrary.Save(set);
            LoadSet(set.id); selection = null; RebuildAll();
            Say($"Imported \"{set.title}\": {set.marks.Count} marks, {set.cues.Count} cues, {set.shots.Count} shots kept for the Shoot mode.");
        });

        public void ImportTake() => Try(() =>
        {
            string folder = EditorUtility.OpenFolderPanel("Finished Gameplay Theatre take (the folder with motion.djt)", Path.Combine(StudioLibrary.ProjectRoot, "Logs", "GameplayTheatre"), "");
            if (folder.Length == 0) return;
            var set = TheatreImport.ImportTakeFolder(folder);
            LoadSet(set.id); selection = null; RebuildAll();
            Say($"Imported \"{set.title}\" with its recorded motion. Open the stage to play it.");
        });
    }
}
