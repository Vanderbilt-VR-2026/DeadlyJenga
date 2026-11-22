using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // A tour of the real Studio window, for review pictures. It needs an editor with a window on
    // screen (not -batchmode):
    //   -executeMethod DeadlyJenga.DevTools.Editor.TrailerStudio.StudioAcceptance.RunWindowTour
    // It opens the window, uses each tab the way a person would, and saves a screenshot of the window
    // at every step to Logs/Validation/TrailerStudio-Window/. Then the editor closes itself.
    public static partial class StudioAcceptance
    {
        public static void RunWindowTour() => Begin("Window");
        public static void RunEditingControls() => Begin("EditingControls");

        static TrailerStudioWindow TourWindow()
        {
            var window = EditorWindow.GetWindow<TrailerStudioWindow>("Trailer Studio");
            window.position = new Rect(40, 60, 1280, 760);
            window.Show();

            return window;
        }

        // Read Unity's own view buffer so other desktop windows never enter a review image.
        static bool Shoot(TrailerStudioWindow window, string name)
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var host = typeof(EditorWindow).GetField("m_Parent", members)?.GetValue(window);
            var viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
            var grab = viewType?.GetMethod("GrabPixels", members);
            if (host == null || grab == null) throw new InvalidOperationException("Editor window capture is unavailable.");
            var size = window.position.size * EditorGUIUtility.pixelsPerPoint;
            int width = Mathf.RoundToInt(size.x), height = Mathf.RoundToInt(size.y);
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var bounds = new Rect(0, 0, width, height);
                viewType.GetMethod("RepaintImmediately", members)?.Invoke(host, null);
                grab.Invoke(host, new object[] { target, bounds });
                RenderTexture.active = target;
                picture.ReadPixels(bounds, 0, 0);
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    var pixels = picture.GetPixels32();
                    var row = new Color32[width];
                    for (int y = 0; y < height / 2; y++)
                    {
                        int low = y * width, high = (height - 1 - y) * width;
                        Array.Copy(pixels, low, row, 0, width);
                        Array.Copy(pixels, high, pixels, low, width);
                        Array.Copy(row, 0, pixels, high, width);
                    }
                    picture.SetPixels32(pixels);
                }
                picture.Apply();
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), picture.EncodeToPNG());
                return picture.GetPixels32().Where((_, i) => i % 997 == 0)
                    .Select(c => c.r / 8 * 1024 + c.g / 8 * 32 + c.b / 8).Distinct().Count() > 12;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(picture);
            }
        }

        static IEnumerable<int> Settle(TrailerStudioWindow window, int frames = 12)
        {
            for (int i = 0; i < frames; i++) { window.Repaint(); yield return 0; }
        }

        static readonly string[] ProductionModes = { "Set", "Perform", "Polish", "Direct", "Shoot", "Cut" };

        static void SubmitButton(Button button)
        {
            if (button == null) return;
            using var submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        static void PressEscape(TrailerStudioWindow window)
        {
            var root = window.rootVisualElement.Q("root");
            using var escape = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None);
            escape.target = root;
            root.SendEvent(escape);
        }

        static List<Button> ModeButtons(TrailerStudioWindow window) =>
            window.rootVisualElement.Q("mode-panel").Query<Button>().ToList();

        static void InventoryButtons(TrailerStudioWindow window, string state, List<string> rows, Button target = null, string coverage = "observed")
        {
            string Clean(string text) => (text ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
            int ordinal = 0;
            foreach (var button in window.rootVisualElement.Query<Button>().ToList())
            {
                ordinal++;
                if (target != null && button != target) continue;
                string label = button.text;
                if (string.IsNullOrWhiteSpace(label)) label = button.Q<Label>()?.text ?? button.name;
                if (string.IsNullOrWhiteSpace(label)) label = "(unlabeled)";
                var group = button.parent;
                while (group != null && string.IsNullOrEmpty(group.name)) group = group.parent;
                bool laidOut = button.visible && button.worldBound.width > 0 && button.worldBound.height > 0;
                rows.Add(string.Join("\t", state, window.Mode.Title, ordinal.ToString(), Clean(group?.name), Clean(button.name), Clean(label),
                    coverage, button.enabledInHierarchy ? "enabled" : "disabled", laidOut ? "laid out" : "hidden or zero size", Clean(button.tooltip)));
            }
        }

        static bool ContainsLayout(Rect outer, Rect inner)
        {
            const float tolerance = 1;
            return inner.width > 0 && inner.height > 0 &&
                inner.xMin >= outer.xMin - tolerance && inner.xMax <= outer.xMax + tolerance &&
                inner.yMin >= outer.yMin - tolerance && inner.yMax <= outer.yMax + tolerance;
        }

        static bool SelectedTabFullyVisible(VisualElement root, string mode)
        {
            var tabs = root.Q("tabs");
            var selected = tabs.Query<Button>(className: "ts-tab--on").ToList();
            if (selected.Count != 1) return false;
            var tab = selected[0];
            var title = tab.Q<Label>();
            var caption = tab.Q<Label>(className: "ts-tab-caption");
            if (!tab.visible || !tab.enabledInHierarchy || title?.text != mode || caption == null ||
                !ContainsLayout(root.worldBound, tab.worldBound) || !ContainsLayout(tabs.worldBound, tab.worldBound))
                return false;
            return ContainsLayout(tab.worldBound, title.worldBound) && ContainsLayout(tab.worldBound, caption.worldBound);
        }

        static IEnumerable<int> ReviewWindowLayouts(TrailerStudioWindow window, Action<string> take, Action<string, Button> activate)
        {
            var root = window.rootVisualElement.Q("root");
            Rect original = window.position;
            string originalMode = window.Mode.Title;
            bool originalLight = root.ClassListContains("ts-light");
            try
            {
                foreach (var size in new[] { new Vector2(1280, 760), new Vector2(900, 560) })
                {
                    window.position = new Rect(original.position, size);
                    foreach (bool light in new[] { false, true })
                    {
                        root.EnableInClassList("ts-light", light);
                        var clipped = new List<string>();
                        foreach (string mode in ProductionModes)
                        {
                            var tab = root.Q("tabs").Query<Button>(className: "ts-tab").ToList()
                                .FirstOrDefault(button => button.Q<Label>()?.text == mode);
                            activate($"select {mode} at {size.x:0}×{size.y:0} {(light ? "light" : "dark")}", tab);
                            foreach (int wait in Settle(window, 4)) yield return wait;
                            if (window.Mode.Title != mode || !SelectedTabFullyVisible(root, mode)) clipped.Add(mode);
                        }
                        var actual = root.layout.size;
                        bool sizeMatched = Mathf.Abs(actual.x - size.x) <= 2 && Mathf.Abs(actual.y - size.y) <= 2;
                        bool themeMatched = (root.resolvedStyle.backgroundColor.grayscale > .5f) == light;
                        string sizeName = size.x == 900 ? "compact" : "broad";
                        string themeName = light ? "light" : "dark";
                        Result($"Production tabs at {size.x:0}×{size.y:0} in {themeName} theme",
                            sizeMatched && themeMatched && clipped.Count == 0,
                            $"Studio layout {actual.x:0}×{actual.y:0}. Theme resolved: {themeMatched}. " +
                            (clipped.Count == 0 ? "All six buttons selected their tab and fit." : "Selection or layout failed: " + string.Join(", ", clipped) + "."));
                        take($"10-cut-{sizeName}-{themeName}");
                    }
                }
            }
            finally
            {
                root.EnableInClassList("ts-light", originalLight);
                window.position = original;
                window.ShowMode(originalMode);
            }
        }

        static IEnumerator<int> WindowTour()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            int fps = set.fps;
            var window = TourWindow();
            foreach (int wait in Settle(window, 30)) yield return wait;
            window.LoadSet(set.id);
            window.RebuildAll();
            var context = window.Context;
            var shots = new List<string>(); int good = 0;
            var inventory = new List<string> { "Fixture or check\tTab\tPosition in tree\tGroup\tName\tButton\tCoverage\tAvailability\tLayout\tTooltip" };
            void Activate(string check, Button button)
            {
                if (button != null) InventoryButtons(window, check, inventory, button, "submitted");
                SubmitButton(button);
            }
            void Guard(string check, Button button)
            {
                if (button != null) InventoryButtons(window, check, inventory, button, "availability checked");
            }
            Button Transport(string text) => window.rootVisualElement.Q("transport").Query<Button>().ToList()
                .FirstOrDefault(button => button.text.StartsWith(text, StringComparison.Ordinal));
            Button Inspector(string text) => window.rootVisualElement.Q("inspector").Query<Button>().ToList()
                .FirstOrDefault(button => button.text.StartsWith(text, StringComparison.Ordinal));
            void Take(string name)
            {
                shots.Add(name);
                InventoryButtons(window, name, inventory);
                File.WriteAllLines(Path.Combine(Folder, "button-inventory.tsv"), inventory);
                if (Shoot(window, name)) good++;
            }

            // Set: marks on the surface, a cue, a mark selected.
            foreach (var mark in set.marks)
                if (StudioStage.SurfaceBelow(stage.ToWorld(mark.position), out var surface)) mark.position = stage.ToStage(surface);
            var hand = set.AddMark(StudioRole.Hand, new Vector3(-4, 6, -6));
            StudioLibrary.Save(set); StudioRun.Refresh();
            window.ShowMode("Set"); context.Select(null);
            foreach (int wait in Settle(window)) yield return wait;
            Take("01-set-stage-open");
            int marksBeforePlacement = set.marks.Count;
            var canceledRoles = new List<StudioRole>();
            foreach (var role in new[] { StudioRole.Survivor, StudioRole.Hand, StudioRole.Bot })
            {
                var button = ModeButtons(window).FirstOrDefault(item => item.text.EndsWith("Add " + role.ToString().ToLowerInvariant(), StringComparison.Ordinal));
                Activate($"arm {role}", button);
                bool armed = window.Viewport.Placing == role;
                PressEscape(window);
                if (armed && !window.Viewport.Placing.HasValue && set.marks.Count == marksBeforePlacement)
                    canceledRoles.Add(role);
            }
            Result("Set placement buttons arm the selected role and Escape cancels", canceledRoles.Count == 3,
                $"Canceled with no added mark: {string.Join(", ", canceledRoles)}.");
            context.Select(set.marks[0]);
            foreach (int wait in Settle(window)) yield return wait;
            Take("02-set-mark-selected");
            var colorMark = set.marks[0];
            int originalColor = colorMark.color;
            int newColor = (originalColor + 1) % 8;
            Button ColorButton(int index) => window.rootVisualElement.Q("inspector").Query<Button>(className: "ts-dot").ToList().ElementAtOrDefault(index);
            Activate("apply a different cast color", ColorButton(newColor));
            bool colorApplied = colorMark.color == newColor && StudioLibrary.Load(set.id)?.Mark(colorMark.id)?.color == newColor;
            Activate("restore cast color", ColorButton(originalColor));
            Result("Set color swatches save a cosmetic change and restore it", colorApplied && colorMark.color == originalColor,
                $"Applied and reloaded from disk: {colorApplied}. Original color restored: {colorMark.color == originalColor}.");

            window.ShowMode("Perform");
            var firstActor = set.marks[0];
            var nextActor = ModeButtons(window).FirstOrDefault(button => button.text == "Next unrecorded actor");
            Activate("advance to an unrecorded performer", nextActor);
            var chosen = window.Selection as StudioMark;
            bool advance = chosen != null && chosen != firstActor && StudioPerform.Actor == chosen.id &&
                !PerformMode.HasPerformance(tape.Find(chosen.id));
            Result("Perform advances to a different unrecorded actor", advance, $"Started at {firstActor.actor}. Selected {chosen?.actor ?? "(none)"}.");
            window.ShowMode("Set");

            // Record the whole Set; a picture while it rolls, and one after.
            StudioRun.RecordWholeSet();
            int rolled = 0;
            bool soundCueSent = false;
            while (StudioRun.State != StudioState.Held)
            {
                rolled++;
                if (!soundCueSent && StudioRun.Recording && StudioRun.Frame >= fps)
                {
                    DeadlyJenga.Core.GameEvents.RaiseBlockHit(stage.anchor, 6f);
                    soundCueSent = true;
                }
                if (StudioRun.Recording && StudioRun.Frame == set.Frames / 2) { window.Repaint(); Take("03-recording"); }
                if (rolled > 20000) throw new TimeoutException("The recording did not finish.");
                yield return 0;
            }
            window.Scrub(set.Frames / 2);
            foreach (int wait in Settle(window)) yield return wait;
            Take("04-recorded-timeline");

            window.SetReviewRange(fps * 2, fps / 2);
            var rangeButton = Transport("Range");
            Guard("review range control available while held", rangeButton);
            bool rangeAvailable = rangeButton?.enabledInHierarchy == true;
            Activate("go to range In", Transport("Start"));
            foreach (int wait in Settle(window)) yield return wait;
            bool wentToIn = window.Frame == window.InFrame;
            Activate("go to range Out", Transport("End"));
            foreach (int wait in Settle(window)) yield return wait;
            bool wentToOut = window.Frame == window.OutFrame;
            bool wasLooping = Transport("Loop")?.ClassListContains("ts-small--on") == true;
            Activate("toggle loop", Transport("Loop"));
            bool nowLooping = Transport("Loop")?.ClassListContains("ts-small--on") == true;
            Activate("restore loop setting", Transport("Loop"));
            bool loopRestored = (Transport("Loop")?.ClassListContains("ts-small--on") == true) == wasLooping;
            Result("Transport uses the review bounds and toggles loop", rangeAvailable && wentToIn && wentToOut && wasLooping != nowLooping && loopRestored,
                $"Range available: {rangeAvailable}. Start reached In: {wentToIn}. End reached Out: {wentToOut}. Loop toggled and restored: {wasLooping != nowLooping && loopRestored}.");
            window.Timeline.ShowLanes(StudioTimeline.Lanes.Sound);
            window.Timeline.SetRangeZoom(true);
            window.Scrub(fps);
            foreach (int wait in Settle(window)) yield return wait;
            var visibleRows = window.rootVisualElement.Q("timeline").Query<VisualElement>(className: "ts-lane-row").ToList();
            bool hasEvents = visibleRows.Any(row => row.Q(className: "ts-lane")?.tooltip?.Contains("recorded events") == true);
            Result("Review a sound in a focused range", hasEvents && soundCueSent && window.InFrame == fps / 2 &&
                window.OutFrame == fps * 2 && window.Frame == fps && window.Timeline.RangeZoom && visibleRows.Count == 2,
                $"Sound-event and cue rows visible: {visibleRows.Count}; inclusive range {window.InFrame}–{window.OutFrame}; playhead {window.Frame}.");
            Take("04b-sound-range");
            window.SetReviewRange(0, set.Frames - 1);
            window.Timeline.ShowLanes(StudioTimeline.Lanes.All);

            var recordedActor = set.marks.FirstOrDefault(mark => PerformMode.HasPerformance(tape.Find(mark.id)));
            var recordedTrack = recordedActor == null ? null : tape.Find(recordedActor.id);
            var cast = window.rootVisualElement.Q("cast");
            if (recordedActor != null)
            {
                context.Select(null);
                var castPicker = cast.Query<Button>(className: "ts-row-picker").ToList()
                    .FirstOrDefault(button => button.Q<Label>(className: "ts-item-name")?.text == recordedActor.actor ||
                        button.Q<Label>(className: "ts-item-name")?.text == recordedActor.actor + "  (bot)");
                Activate("select a recorded cast row", castPicker);
            }
            bool pickedCast = recordedActor != null && window.Selection == recordedActor;
            Button CastFlag(string text) => cast.Q(className: "ts-item--selected")?.Query<Button>().ToList()
                .FirstOrDefault(button => button.text == text);
            bool CheckCastFlag(string on, string off, Func<bool> state)
            {
                if (!pickedCast) return false;
                bool before = state();
                Activate("toggle cast " + on.ToLowerInvariant(), CastFlag(before ? off : on));
                bool toggled = before != state();
                Activate("restore cast " + on.ToLowerInvariant(), CastFlag(before ? on : off));
                return toggled && state() == before;
            }
            bool muteChanged = CheckCastFlag("Mute", "Unmute", () => recordedTrack.mode == TrackMode.Muted);
            bool soloChanged = CheckCastFlag("Solo", "Unsolo", () => recordedTrack.solo);
            bool lockChanged = CheckCastFlag("Lock", "Unlock", () => recordedTrack.locked);
            Result("Cast row selects an actor and its track flags change and restore", pickedCast && muteChanged && soloChanged && lockChanged,
                $"Selected: {pickedCast}. Mute: {muteChanged}. Solo: {soloChanged}. Lock: {lockChanged}.");

            // Perform: an actor chosen, the others listed.
            window.ShowMode("Perform"); context.Select(set.marks[1]);
            foreach (int wait in Settle(window)) yield return wait;
            Take("05-perform");

            // Polish: a layer on an actor.
            window.ShowMode("Polish");
            var layer = new StudioOverride { actor = set.marks[0].id, kind = "reaction", name = "knockback", from = 1, to = 2.5f };
            context.Edit("Add layer", () => set.overrides.Add(layer));
            context.Select(layer);
            foreach (int wait in Settle(window)) yield return wait;
            Take("06-polish-layer");
            var layerOn = ModeButtons(window).FirstOrDefault(button => button.text == "On");
            Activate("mute motion layer", layerOn);
            bool muted = layer.muted && ModeButtons(window).Any(button => button.text == "Muted");
            Activate("unmute motion layer", ModeButtons(window).FirstOrDefault(button => button.text == "Muted"));
            Result("Polish mute button toggles a motion layer", muted && !layer.muted,
                $"Muted after first activation: {muted}. Restored after second activation: {!layer.muted}.");

            // Direct: blocks picked, a push cue with its arrow.
            DirectMode.Picked.Clear();
            window.ShowMode("Direct");
            Button PushButton() => window.rootVisualElement.Q("mode-panel").Query<Button>().ToList()
                .FirstOrDefault(button => button.text.EndsWith("Push the picked blocks", StringComparison.Ordinal));
            bool unavailableBeforePicking = PushButton()?.enabledInHierarchy == false;
            Guard("cannot push an empty pick", PushButton());
            DirectMode.Picked.AddRange(Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeSelf)
                .OrderBy(i => (stage.blocks[i].position - stage.anchor).sqrMagnitude).Take(6));
            window.RebuildAll();
            bool availableAfterPicking = PushButton()?.enabledInHierarchy == true;
            Result("Push availability follows the viewport selection", unavailableBeforePicking && availableAfterPicking,
                $"Disabled with an empty selection: {unavailableBeforePicking}. Enabled with {DirectMode.Picked.Count} picked blocks: {availableAfterPicking}.");
            int cueCount = set.cues.Count;
            Activate("push picked blocks", PushButton());
            var push = window.Selection as StudioCue;
            bool pushAdded = set.cues.Count == cueCount + 1 && push?.action == PushCue.Id &&
                push.blocks.SequenceEqual(DirectMode.Picked);
            Result("Direct Push adds a cue for the picked blocks", pushAdded, $"Cues before {cueCount}, after {set.cues.Count}.");
            if (!pushAdded)
            {
                push = new StudioCue { action = PushCue.Id, blocks = new List<int>(DirectMode.Picked) };
                context.Edit("Prepare push fixture", () => set.cues.Add(push));
            }
            context.Edit("Frame push fixture", () => { push.at = 2; push.direction = new Vector3(1, .3f, .4f); push.strength = 9; push.ripple = .1f; });
            context.Select(push);
            foreach (int wait in Settle(window)) yield return wait;
            Take("07-direct-push");

            // Shoot: two cameras, one selected; then looking through it.
            window.ShowMode("Shoot");
            int cameraCount = set.cameras.Count;
            var addCamera = ModeButtons(window).FirstOrDefault(button => button.text.EndsWith("Camera from this view", StringComparison.Ordinal));
            Activate("create camera from viewport", addCamera);
            var wide = window.Selection as StudioCameraTrack;
            bool cameraAdded = set.cameras.Count == cameraCount + 1 && wide?.keys.Count == 1 && set.cameras.Contains(wide);
            Result("Shoot creates and selects a keyed camera from the view", cameraAdded,
                $"Cameras before {cameraCount}, after {set.cameras.Count}. Selected camera keys: {wide?.keys.Count ?? 0}.");
            if (!cameraAdded) wide = ShootMode.AddCamera(context);
            StudioCamera.Orbit(60, -6); StudioCamera.Zoom(.6f);
            int keyFrame = Mathf.Clamp(Mathf.RoundToInt(set.Frames * .8f), 0, set.Frames - 1);
            if (wide.keys.Any(key => Mathf.RoundToInt(key.time * fps) == keyFrame)) keyFrame = 0;
            window.Scrub(keyFrame);
            foreach (int wait in Settle(window)) yield return wait;
            int keysBefore = wide.keys.Count;
            Activate("save a camera key at the playhead", Inspector("Key here"));
            bool addedKey = wide.keys.Count == keysBefore + 1 && wide.keys.Any(key => Mathf.RoundToInt(key.time * fps) == keyFrame);
            Result("Shoot adds an editable key at the playhead", addedKey, $"Expected frame {keyFrame}. Keys before {keysBefore}, after {wide.keys.Count}.");
            if (!addedKey) context.Edit("Prepare camera key fixture", () => StudioCameras.AddKey(wide, StudioCameras.KeyFromView(stage, wide, keyFrame / (float)fps)));

            int camerasBeforeDuplicate = set.cameras.Count;
            Activate("duplicate the selected camera", Inspector("Duplicate"));
            var duplicate = window.Selection as StudioCameraTrack;
            bool duplicated = duplicate != null && duplicate.id != wide.id && set.cameras.Count == camerasBeforeDuplicate + 1 &&
                duplicate.keys.Count == wide.keys.Count && !ReferenceEquals(wide.keys, duplicate.keys);
            if (duplicated) Activate("remove the disposable duplicate", Inspector("Delete camera"));
            bool duplicateRemoved = duplicated && !set.cameras.Contains(duplicate) && set.cameras.Contains(wide);
            Result("Shoot duplicates the selected camera and can remove the copy", duplicated && duplicateRemoved,
                $"Independent keyed copy: {duplicated}. Only the copy was removed: {duplicateRemoved}.");
            context.Select(wide);
            StudioCamera.Orbit(-110, 10);
            var second = ShootMode.AddCamera(context);
            context.Edit("Camera setup", () => { second.behavior = "follow"; second.subject = set.marks[0].id; second.noise = "subtle"; });
            context.Select(wide);
            foreach (int wait in Settle(window)) yield return wait;
            Take("08-shoot-cameras");
            ShootMode.LookThrough = wide.id;
            window.Scrub(set.Frames / 3);
            foreach (int wait in Settle(window)) yield return wait;
            Take("09-shoot-through-the-camera");
            ShootMode.LookThrough = "";

            // Cut.
            window.ShowMode("Cut");
            context.Edit("Build cut", () =>
            {
                set.cuts.Add(new StudioCut { camera = wide.id, from = 0, to = 2 });
                set.cuts.Add(new StudioCut { camera = second.id, from = 1, to = 3, dissolve = .3f });
            });
            context.Select(null);
            foreach (int wait in Settle(window)) yield return wait;
            Take("10-cut");
            var up = ModeButtons(window).Where(button => button.text == "Up").ToList();
            var down = ModeButtons(window).Where(button => button.text == "Down").ToList();
            var originalCutOrder = set.cuts.ToArray();
            bool reorderGuards = up.Count == 2 && down.Count == 2 && !up[0].enabledInHierarchy && up[1].enabledInHierarchy &&
                down[0].enabledInHierarchy && !down[1].enabledInHierarchy;
            if (up.Count == 2) Guard("first shot cannot move up", up[0]);
            if (down.Count == 2) Guard("last shot cannot move down", down[1]);
            if (reorderGuards) Activate("move first shot down", down[0]);
            bool movedDown = set.cuts.SequenceEqual(originalCutOrder.Reverse());
            if (movedDown) Activate("restore first shot to the top", ModeButtons(window).LastOrDefault(button => button.text == "Up"));
            bool cutOrderRestored = set.cuts.SequenceEqual(originalCutOrder);
            Result("Cut reorders a shot and guards the first and last positions", reorderGuards && movedDown && cutOrderRestored,
                $"Boundary guards: {reorderGuards}. First shot moved down: {movedDown}. Original order restored: {cutOrderRestored}.");
            if (!cutOrderRestored) context.Edit("Restore the cut fixture", () => { set.cuts.Clear(); set.cuts.AddRange(originalCutOrder); });
            var cutIn = window.rootVisualElement.Q("mode-panel").Query<Slider>().ToList().FirstOrDefault(slider => slider.label == "In (s)");
            bool trimmed = false;
            if (cutIn != null)
            {
                float originalIn = originalCutOrder[0].from;
                cutIn.Focus();
                cutIn.value = originalIn + .25f;
                window.rootVisualElement.Q<Button>("commands").Focus();
                yield return 0;
                trimmed = Mathf.Approximately(set.cuts[0].from, originalIn + .25f);
                context.Edit("Restore the trim fixture", () => set.cuts[0].from = originalIn);
            }
            Result("Cut's In trim updates the selected piece", trimmed, $"The rendered trim slider changed the first piece: {trimmed}.");
            foreach (int wait in ReviewWindowLayouts(window, Take, Activate)) yield return wait;

            // Command palette and shortcut sheet.
            window.ShowMode("Set");
            var findCommand = window.rootVisualElement.Q<Button>("commands");
            bool activeBefore = findCommand?.ClassListContains("ts-activated") == true;
            Activate("open command palette and flash button", findCommand);
            bool flashed = findCommand?.ClassListContains("ts-activated") == true;
            bool paletteOpened = window.rootVisualElement.Q(className: "ts-palette") != null;
            double feedbackDeadline = EditorApplication.timeSinceStartup + 2;
            while (findCommand?.ClassListContains("ts-activated") == true && EditorApplication.timeSinceStartup < feedbackDeadline)
                yield return 0;
            bool flashCleared = findCommand?.ClassListContains("ts-activated") == false;
            Result("Find command opens through its button and gives temporary feedback",
                !activeBefore && flashed && paletteOpened && flashCleared,
                $"Feedback shown: {flashed}. Palette opened: {paletteOpened}. Feedback cleared: {flashCleared}.");
            if (!paletteOpened) window.ShowPalette(true);
            foreach (int wait in Settle(window)) yield return wait;
            Take("11-command-palette");
            var palette = window.rootVisualElement.Q(className: "ts-palette");
            var search = palette.Q<TextField>();
            search.value = wide.name;
            bool foundKey = palette.Query<Button>().ToList().Any(button =>
                button.text.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0 &&
                button.text.IndexOf(wide.name, StringComparison.OrdinalIgnoreCase) >= 0);
            Result("Search current camera keys", foundKey, $"Searched for {wide.name}.");
            Take("11b-find-camera-key");
            using (var enter = KeyDownEvent.GetPooled('\n', KeyCode.Return, EventModifiers.None))
            {
                enter.target = search;
                search.SendEvent(enter);
            }
            Result("Jump from the command palette", window.Selection == wide && window.Mode.Title == "Shoot" &&
                window.Frame == Mathf.Clamp(Mathf.RoundToInt(wide.keys[0].time * fps), 0, set.Frames - 1),
                $"Selected {window.Mode.Title} at frame {window.Frame}.");

            window.ShowPalette(true);
            PressEscape(window);
            bool escapeClosed = window.rootVisualElement.Q(className: "ts-palette") == null;
            Result("Escape closes the command palette", escapeClosed, $"Palette closed: {escapeClosed}.");
            if (!escapeClosed) window.ClosePalette();
            window.ShowPalette(false);
            foreach (int wait in Settle(window)) yield return wait;
            Take("12-shortcuts");
            window.ClosePalette();

            // Sound needs exactly one set of ears on the stage.
            var ears = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Result("One audio listener on the stage", ears.Count(e => e.isActiveAndEnabled) == 1,
                string.Join("; ", ears.Select(e => $"{e.name}: enabled {e.enabled}, active {e.gameObject.activeInHierarchy}")));

            // Undo really undoes a Set edit.
            int marksBefore = set.marks.Count;
            context.Edit("Add survivor", () => set.AddMark(StudioRole.Survivor, new Vector3(2, 0, 2)), true);
            int added = window.Set.marks.Count;
            Undo.PerformUndo();
            foreach (int wait in Settle(window, 6)) yield return wait;
            int undone = window.Set.marks.Count;
            Undo.PerformRedo();
            foreach (int wait in Settle(window, 6)) yield return wait;
            int redone = window.Set.marks.Count;

            var tabs = window.rootVisualElement.Query<Button>(className: "ts-tab").ToList().Select(b => b.Q<Label>()?.text ?? b.text).ToList();
            Result("Window tour: every tab used in a real editor window", good == shots.Count && ProductionModes.All(tabs.Contains),
                $"Tabs: {string.Join(", ", tabs)}. {good} of {shots.Count} screenshots show real content ({string.Join(", ", shots)}). " +
                "The pictures were read from the Unity editor view buffer.");
            Result("Undo and redo for Set edits", added == marksBefore + 1 && undone == marksBefore && redone == marksBefore + 1,
                $"Marks: {marksBefore}, after adding one {added}, after undo {undone}, after redo {redone} (Unity's own undo stack).");
            Result("Runtime errors during the tour", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            File.WriteAllLines(Path.Combine(Folder, "button-inventory.tsv"), inventory);
            File.WriteAllText(Path.Combine(Folder, "button-coverage.md"),
                "# Button coverage\n\nThe TSV inventories each Button in the captured window states and identifies individual buttons submitted or checked for availability. " +
                "Position in tree is one-based within the current window state. The acceptance report records the outcome assertions. " +
                "Buttons conditional on unvisited states may be absent.\n\n" +
                "The tour does not open external folders or file dialogs, export files, start renders, purge takes, or confirm destructive prompts. " +
                "Recording in this tour uses the disposable Set through the runtime entry point.\n");
            StudioRun.SaveTape();
        }

        static void ClickStagePicture(StudioViewport viewport)
        {
            var target = viewport.Host;
            Vector2 point = target.LocalToWorld(viewport.PictureRect().center);
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = point }))
            {
                down.target = target;
                target.SendEvent(down);
            }
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = point }))
            {
                up.target = target;
                target.SendEvent(up);
            }
        }

        static IEnumerator<int> EditingControlsTour()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            var window = TourWindow();
            foreach (int wait in Settle(window, 20)) yield return wait;
            window.LoadSet(set.id);
            window.ShowMode("Set");
            foreach (int wait in Settle(window)) yield return wait;
            var context = window.Context;
            var inventory = new List<string> { "Fixture or check\tTab\tPosition in tree\tGroup\tName\tButton\tCoverage\tAvailability\tLayout\tTooltip" };
            var pendingMenus = new List<string>();
            Button ModeButton(string text) => ModeButtons(window).FirstOrDefault(button => button.text.Contains(text));
            VisualElement Inspector() => window.rootVisualElement.Q("inspector");
            Button InspectButton(string text) => Inspector().Query<Button>().ToList().FirstOrDefault(button => button.text == text);
            bool Run(string check, Button button)
            {
                if (button == null || !button.enabledInHierarchy) return false;
                InventoryButtons(window, check, inventory, button, "submitted");
                SubmitButton(button);
                return true;
            }
            void NoteMenu(string name, Button button)
            {
                bool available = button?.enabledInHierarchy == true;
                if (button != null) InventoryButtons(window, name, inventory, button, "availability checked; native menu pending");
                Result(name + " is available", available, "Native menu items require a separate interactive check.");
                pendingMenus.Add(name);
            }

            // Set placement and deletion use the real button, viewport and inspector against this disposable Set.
            int marks = set.marks.Count;
            bool armed = Run("arm a new bot mark", ModeButton("Add bot")) && window.Viewport.Placing == StudioRole.Bot;
            if (armed) ClickStagePicture(window.Viewport);
            var placed = window.Selection as StudioMark;
            bool placedBot = set.marks.Count == marks + 1 && placed?.role == StudioRole.Bot && !window.Viewport.Placing.HasValue;
            bool markRemoved = placedBot && Run("delete the new bot mark", InspectButton("Delete mark")) &&
                set.marks.Count == marks && set.Mark(placed.id) == null;
            Result("Set places a bot on the stage and deletes its mark", armed && placedBot && markRemoved,
                $"Placement armed: {armed}. Bot added and selected: {placedBot}. Mark removed: {markRemoved}.");
            if (window.Viewport.Placing.HasValue) PressEscape(window);
            NoteMenu("Set Add cue", ModeButton("Add cue"));

            // Direct cue buttons are deterministic. The later Set checks delete the exact cue these controls create.
            window.ShowMode("Direct");
            NoteMenu("Direct Disaster", ModeButton("Disaster"));
            int beforeCues = set.cues.Count;
            Run("add a guide", ModeButton("+  Guide"));
            var guide = window.Selection as StudioCue;
            bool guideAdded = guide?.action == GuideCue.Id && set.cues.Count == beforeCues + 1;
            string feedback = window.rootVisualElement.Q<Label>("last-pressed")?.text;
            window.RebuildAll();
            bool heldFeedback = feedback?.Contains("Guide") == true && window.rootVisualElement.Q<Label>("last-pressed")?.text == feedback;
            Run("add slow time", ModeButton("Slow time"));
            var slow = window.Selection as StudioCue;
            bool slowAdded = slow?.action == SlowTimeCue.Id && set.cues.Count == beforeCues + 2;
            DirectMode.Picked.Clear();
            DirectMode.Picked.AddRange(Enumerable.Range(0, stage.blocks.Length).Where(index => stage.blocks[index]).Take(2));
            window.RebuildAll();
            bool cleared = Run("clear selected blocks", ModeButton("Clear the pick")) && DirectMode.Picked.Count == 0 &&
                ModeButton("Push the picked blocks")?.enabledInHierarchy == false;
            Result("Direct Guide and Slow time add the correct cues; Clear disables Push", guideAdded && slowAdded && cleared,
                $"Guide added: {guideAdded}. Slow time added: {slowAdded}. Pick cleared and Push disabled: {cleared}.");
            Result("Footer keeps the last activated button through rebuild", heldFeedback,
                $"Footer after the Guide action and rebuild: {feedback ?? "(missing)"}.");
            window.ShowMode("Set");
            if (guideAdded) context.Select(guide);
            bool cueRemoved = guideAdded && Run("delete the Guide cue from Set", InspectButton("Delete cue")) && !set.cues.Contains(guide);
            if (slowAdded)
            {
                context.Select(slow);
                cueRemoved &= Run("delete the Slow time cue from Set", InspectButton("Delete cue")) && !set.cues.Contains(slow);
            }
            Result("Set deletes the selected disposable cues", cueRemoved, $"Cue count returned to {set.cues.Count}; initial count {beforeCues}.");

            // Record the disposable Set once to exercise real take controls and clones.
            var previousPass = StudioRun.LastPass;
            bool recordSubmitted = Run("record the disposable Set", window.rootVisualElement.Q<Button>("record"));
            bool recordStarted = recordSubmitted && StudioRun.State == StudioState.Running &&
                StudioRun.LastPass != previousPass && StudioRun.LastPass?.label == "Whole set";
            Result("Set record button starts recording the disposable Set", recordStarted,
                $"Record submitted: {recordSubmitted}. Entered recording: {recordStarted}.");
            if (!recordStarted && StudioRun.State == StudioState.Held) StudioRun.RecordWholeSet();
            double recordDeadline = EditorApplication.timeSinceStartup + 60;
            while (StudioRun.State != StudioState.Held && EditorApplication.timeSinceStartup < recordDeadline) yield return 0;
            if (StudioRun.State != StudioState.Held) throw new TimeoutException("The disposable recording did not finish.");
            var actor = set.marks.FirstOrDefault(mark => mark.role != StudioRole.Hand && PerformMode.HasPerformance(tape.Find(mark.id)));
            var track = actor == null ? null : tape.Find(actor.id);
            var take = track?.ActiveTake;
            context.Select(actor);
            bool trashed = take != null && Run("trash a recorded actor take", InspectButton("Trash")) &&
                take.trashed && track.ActiveTake != take && track.takes.Contains(take);
            var trash = Inspector().Query<Foldout>().ToList().FirstOrDefault(fold => fold.text.StartsWith("Trash (", StringComparison.Ordinal));
            if (trash != null) trash.value = true;
            bool restored = trashed && Run("restore the recorded actor take", InspectButton("Restore")) &&
                !take.trashed && track.Kept.Contains(take);
            Result("Set moves a take to Trash and restores the same take", trashed && restored,
                $"Trashed without erasing: {trashed}. Same take available again: {restored}.");
            if (take != null && take.trashed) { take.trashed = false; track.active = track.takes.IndexOf(take); window.TapeChanged(); }

            // Keep the fixture going after a missing take so other tabs still report their results.
            window.ShowMode("Perform");
            context.Select(actor);
            var cloneFold = window.rootVisualElement.Q("mode-panel").Query<Foldout>().ToList()
                .FirstOrDefault(fold => fold.text == "Clone this performance");
            if (cloneFold != null) cloneFold.value = true;
            var prior = new HashSet<string>(set.marks.Select(mark => mark.id));
            bool invokedClone = Run("create a clone of the selected performance", ModeButton("Make clones"));
            var copies = set.marks.Where(mark => !prior.Contains(mark.id)).ToList();
            bool cloned = invokedClone && copies.Count > 0 && copies.All(mark => PerformMode.HasPerformance(tape.Find(mark.id))) &&
                PerformMode.HasPerformance(track);
            Result("Perform makes independent actors with copied performances", cloned, $"{copies.Count} new actor mark(s) with independent identifiers.");

            window.ShowMode("Polish");
            context.Select(actor);
            NoteMenu("Polish Add layer", ModeButton("Add a layer"));
            var layer = new StudioOverride { actor = actor?.id ?? set.marks[0].id, kind = "reaction", name = "Disposable layer", from = .5f, to = 1 };
            var otherLayer = new StudioOverride { actor = layer.actor, kind = "reaction", name = "Preserved layer", from = 1, to = 2 };
            context.Edit("Seed disposable layers", () => { set.overrides.Add(layer); set.overrides.Add(otherLayer); });
            context.Select(layer);
            bool layerDeleted = Run("delete the selected layer", InspectButton("Delete layer")) &&
                !set.overrides.Contains(layer) && set.overrides.Contains(otherLayer) && window.Selection == set.Mark(layer.actor);
            Result("Polish deletes just the selected layer and reselects its actor", layerDeleted,
                $"Selected layer present: {set.overrides.Contains(layer)}. Neighbor present: {set.overrides.Contains(otherLayer)}.");

            window.ShowMode("Shoot");
            var camera = ShootMode.AddCamera(context);
            window.Scrub(set.fps);
            foreach (int wait in Settle(window)) yield return wait;
            float at = window.Frame / (float)set.fps;
            bool speedAdded = Run("add a speed mark at the playhead", InspectButton("Speed change at the playhead")) &&
                camera.speeds.Any(mark => Mathf.Approximately(mark.time, at) && Mathf.Approximately(mark.speed, .25f));
            int beforeSpeedDelete = camera.speeds.Count;
            var speedDelete = Inspector().Query<Button>().ToList()
                .LastOrDefault(button => button.text == "x" && button.tooltip != "Delete this key");
            bool speedDeleted = speedAdded && Run("remove the playhead speed mark", speedDelete) &&
                camera.speeds.Count == beforeSpeedDelete - 1 && camera.speeds.All(mark => !Mathf.Approximately(mark.time, at));
            int beforeKeyDelete = camera.keys.Count;
            var keyDelete = Inspector().Query<Button>().ToList().FirstOrDefault(button => button.tooltip == "Delete this key");
            bool keyDeleted = Run("remove a camera key from the disposable camera", keyDelete) && camera.keys.Count == beforeKeyDelete - 1;
            Result("Shoot adds and removes a speed mark and removes a camera key", speedAdded && speedDeleted && keyDeleted,
                $"Speed mark added: {speedAdded}. Speed mark removed: {speedDeleted}. Camera key removed: {keyDeleted}.");
            ShootMode.AddCamera(context);

            window.ShowMode("Cut");
            NoteMenu("Cut Add a shot", ModeButton("Add a shot"));
            var inOrder = set.cameras.OrderBy(item => item.start).Select(item => item.id).ToArray();
            bool populated = Run("populate the cut with every camera", ModeButton("Use every camera once")) &&
                set.cuts.Select(cut => cut.camera).SequenceEqual(inOrder);
            var remove = ModeButtons(window).FirstOrDefault(button => button.text == "x");
            bool removed = populated && Run("remove the first cut piece", remove) &&
                set.cuts.Select(cut => cut.camera).SequenceEqual(inOrder.Skip(1));
            Result("Cut populates with every camera in order and removes one piece", populated && removed,
                $"Cut created in order: {populated}. First piece removed: {removed}. Remaining pieces: {set.cuts.Count}.");

            File.WriteAllLines(Path.Combine(Folder, "button-inventory.tsv"), inventory);
            File.WriteAllLines(Path.Combine(Folder, "button-coverage.md"), new[] {
                "# Editing controls acceptance scope", "",
                "Submitted means the fixture sent a Unity UI Toolkit NavigationSubmitEvent to the real enabled Button. Assertions are in report.md.",
                "",
                "Pending native menu item selection. The controls were checked for availability and the menus were left closed.", ""
            }.Concat(pendingMenus.Select(menu => "- " + menu)).Concat(new[] {
                "", "Other excluded effects include external applications and file dialogs, rendering or export, and irreversible take purges.",
                "The fixture deletes only the marks, cues, layers, camera keys and cut pieces belonging to its disposable Set."
            }));
            Result("No exceptions during the editing-controls tour", exceptions == 0,
                exceptions == 0 ? "No exceptions." : string.Join(" / ", errorLines));
            StudioRun.SaveTape();
        }
    }
}
