using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The command palette (Ctrl+K: reach anything by name) and the shortcut sheet (?).
    public sealed class StudioPalette
    {
        readonly StudioContext context;
        readonly VisualElement layer;
        VisualElement box;
        TextField search;
        ScrollView results;
        List<(string name, Action run)> commands;
        List<(string name, Action run)> shown = new List<(string, Action)>();
        int highlighted;

        public StudioPalette(StudioContext context, VisualElement layer) { this.context = context; this.layer = layer; }

        public bool IsOpen => box != null;

        void OpenBox()
        {
            Close();
            layer.pickingMode = PickingMode.Position;
            box = new VisualElement();
            box.AddToClassList("ts-palette");
            layer.Add(box);
            // Clicking outside the box closes it.
            layer.RegisterCallback<PointerDownEvent>(ClickedOutside);
        }

        void ClickedOutside(PointerDownEvent e) { if (e.target == layer) Close(); }

        public void Close()
        {
            if (box == null) return;
            layer.UnregisterCallback<PointerDownEvent>(ClickedOutside);
            box.RemoveFromHierarchy();
            box = null; search = null;
            results = null;
            commands = null;
            shown.Clear();
            layer.pickingMode = PickingMode.Ignore;
        }

        // ---- Commands ----

        public void ShowCommands() => ShowCommands("");

        void ShowCommands(string query)
        {
            OpenBox();
            commands = AllCommands().ToList();
            search = new TextField { tooltip = "Search commands, authored cues and camera keys." };
            search.SetValueWithoutNotify(query);
            search.RegisterValueChangedCallback(_ => { highlighted = 0; Filter(); });
            box.Add(search);
            results = new ScrollView();
            box.Add(results);
            box.Add(StudioUi.Hint("Search by cue, camera name or timestamp. Use arrows and Enter to jump. Esc closes."));
            highlighted = 0;
            Filter();
            var field = search;
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectRange(query.Length, query.Length);
            });
        }

        IEnumerable<(string name, Action run)> AllCommands()
        {
            var window = context.window;
            foreach (var mode in StudioExtensions.Modes.Where(m => m.Available))
            {
                var captured = mode;
                yield return ("Go to " + mode.Title + " mode", () => window.ShowMode(captured.Title));
            }
            yield return (StudioRun.State == StudioState.Closed ? "Open stage" : "Close stage", () =>
            {
                if (StudioRun.State == StudioState.Closed) StudioRun.OpenStage(context.Set); else StudioRun.CloseStage();
            });
            yield return ("Record", window.PressRecord);
            yield return ("Play or pause", window.TogglePlay);
            yield return ("Go to start", () => window.Scrub(0));
            yield return ("Show shortcuts", ShowShortcuts);
            if (context.Set != null)
            {
                yield return ("Find authored cues…", () => ShowCommands("Jump cue "));
                yield return ("Find camera keys…", () => ShowCommands("Jump camera key "));
            }
            yield return ("Import a Gameplay Theatre recipe", window.ImportRecipe);
            yield return ("Import a Gameplay Theatre take folder", window.ImportTake);
            yield return (StudioExamples.Enabled ? "Hide example extensions" : "Show example extensions", () =>
            {
                StudioExamples.Enabled = !StudioExamples.Enabled;
                context.Rebuild();
            });
            if (StudioRun.State == StudioState.Closed)
                foreach (string id in StudioLibrary.SetIds())
                {
                    string captured = id;
                    yield return ("Open set: " + (StudioLibrary.Load(id)?.title ?? id), () => { window.LoadSet(captured); context.Rebuild(); });
                }
            foreach (var command in StudioExtensions.Commands)
            {
                var captured = command;
                if (command.Available(context)) yield return (command.Name, () => captured.Run(context));
            }
            if (context.Set != null)
                foreach (var command in TimelineCommands(context.Set))
                    yield return command;
        }

        IEnumerable<(string name, Action run)> TimelineCommands(StudioSet set)
        {
            for (int i = 0; i < set.cues.Count; i++)
            {
                var cue = set.cues[i];
                string label = StudioCues.Get(cue.action)?.Label ?? cue.action;
                if (!string.IsNullOrWhiteSpace(cue.hazard)) label += " · " + cue.hazard;
                string time = StudioUi.Time(Mathf.RoundToInt(cue.at * set.fps), set.fps);
                bool linked = !string.IsNullOrEmpty(cue.link);
                if (linked) time = $"trigger + {time}";
                yield return ($"Jump cue · {label} · {time} · Cue {i + 1}", () =>
                {
                    if (!CanJump(set)) return;
                    if (!set.cues.Contains(cue)) { context.Say("This cue was removed."); return; }
                    if (string.IsNullOrEmpty(cue.link)) Jump("Set", cue, cue.at);
                    else
                    {
                        context.window.ShowMode("Direct");
                        context.Select(cue);
                        context.Say("This cue follows a trigger. Its delay is measured from that trigger.");
                    }
                });
            }

            var duplicateNames = new HashSet<string>(set.cameras.GroupBy(c => c.name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1).Select(group => group.Key), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < set.cameras.Count; i++)
            {
                var camera = set.cameras[i];
                string label = duplicateNames.Contains(camera.name) ? $"{camera.name} (camera {i + 1})" : camera.name;
                for (int j = 0; j < camera.keys.Count; j++)
                {
                    var key = camera.keys[j];
                    string time = StudioUi.Time(Mathf.RoundToInt(key.time * set.fps), set.fps);
                    yield return ($"Jump camera key · {label} · {time} · Key {j + 1}", () =>
                    {
                        if (!CanJump(set)) return;
                        if (!set.cameras.Contains(camera) || !camera.keys.Contains(key))
                        {
                            context.Say("This camera key was removed.");
                            return;
                        }
                        Jump("Shoot", camera, key.time);
                    });
                }
            }
        }

        bool CanJump(StudioSet set)
        {
            if (!ReferenceEquals(context.Set, set))
            {
                context.Say("Open the command palette again to search the current Set.");
                return false;
            }
            if (StudioRun.Busy || StudioHandheld.Recording)
            {
                context.Say("Stop the current playback or recording before jumping.");
                return false;
            }
            return true;
        }

        void Jump(string mode, object selection, float seconds)
        {
            var window = context.window;
            if (window.Mode?.Title != mode) window.ShowMode(mode);
            int frame = Mathf.RoundToInt(seconds * context.Set.fps);
            if (window.Timeline.RangeZoom && (frame < window.InFrame || frame > window.OutFrame))
                window.Timeline.SetRangeZoom(false);
            window.Scrub(frame);
            context.Select(selection);
        }

        void Filter()
        {
            string query = (search?.value ?? "").Trim();
            string[] words = query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            const int limit = 40;
            var matches = commands.AsEnumerable();
            if (query.Length > 0)
            {
                bool Phrase(string name) => name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                matches = commands.Where(command => Phrase(command.name)).Concat(commands.Where(command =>
                    !Phrase(command.name) && words.All(word => command.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)));
            }
            shown = matches.Take(limit + 1).ToList();
            bool truncated = shown.Count > limit;
            if (truncated) shown.RemoveAt(limit);
            highlighted = Mathf.Clamp(highlighted, 0, Mathf.Max(0, shown.Count - 1));
            results.Clear();
            for (int i = 0; i < shown.Count; i++)
            {
                var command = shown[i];
                var item = new Button(() => Run(command.run)) { text = command.name, tooltip = command.name };
                item.AddToClassList("ts-small"); item.AddToClassList("ts-palette-item");
                item.EnableInClassList("ts-palette-item--on", i == highlighted);
                results.Add(item);
            }
            if (shown.Count == 0) results.Add(StudioUi.Hint("Nothing by that name."));
            if (truncated) results.Add(StudioUi.Hint("More matches. Add a name or timestamp to narrow the search."));
            results.scrollOffset = Vector2.zero;
        }

        void MoveHighlight(int direction)
        {
            if (shown.Count == 0) return;
            results.contentContainer[highlighted].RemoveFromClassList("ts-palette-item--on");
            highlighted = Mathf.Clamp(highlighted + direction, 0, shown.Count - 1);
            var next = results.contentContainer[highlighted];
            next.AddToClassList("ts-palette-item--on");
            results.ScrollTo(next);
        }

        void Run(Action action)
        {
            Close();
            try { action(); } catch (Exception error) { context.Say(error.Message); }
        }

        // ---- Shortcut sheet ----

        static readonly (string keys, string what)[] Shortcuts =
        {
            ("Space", "Play or pause"), ("R", "Record, or stop recording"), ("J  K  L", "Back 1 s, pause, forward 1 s"),
            ("Left  Right", "One frame back or forward"), ("Home  End", "Start or end of the range"),
            ("[  ]  \\", "Set range start, range end, clear the range"), ("Tab", "Next mode (Shift+Tab: previous)"),
            ("1 to 9", "Pick a take of the selected actor or track"), ("Delete", "Delete the selected mark or cue"),
            ("Ctrl+Z  Ctrl+Y", "Undo, redo"), ("Ctrl+K", "Search commands, cues and camera keys"), ("?", "This sheet"),
            ("Drag (viewport)", "Orbit. Right-drag slides, wheel zooms"), ("Drag a mark", "Move it along the surface"),
            ("Shift+drag a mark", "Turn the actor"), ("Alt+drag a hand mark", "Raise or lower the hand"), ("Esc", "Close this, or cancel placing a mark")
        };

        public void ShowShortcuts()
        {
            OpenBox();
            box.Add(StudioUi.Heading("SHORTCUTS"));
            var list = new ScrollView();
            box.Add(list);
            foreach (var (keys, what) in Shortcuts)
            {
                var row = new VisualElement();
                row.AddToClassList("ts-sheet-row");
                var key = new Label(keys); key.AddToClassList("ts-key");
                row.Add(key); row.Add(new Label(what));
                list.Add(row);
            }
            box.Add(StudioUi.Hint("Click outside or press Esc to close."));
        }

        // Returns true when the palette used the key.
        public bool HandleKey(KeyDownEvent e)
        {
            if (box == null)
            {
                if (e.keyCode != KeyCode.Escape || !context.window.Viewport.Placing.HasValue) return false;
                context.window.Viewport.CancelPlacing();
                return true;
            }
            if (e.keyCode == KeyCode.Escape) { Close(); e.StopPropagation(); return true; }
            if (search == null) return true;   // the shortcut sheet swallows other keys
            if (e.keyCode == KeyCode.DownArrow) { MoveHighlight(1); e.StopPropagation(); return true; }
            if (e.keyCode == KeyCode.UpArrow) { MoveHighlight(-1); e.StopPropagation(); return true; }
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                if (shown.Count > 0) Run(shown[Mathf.Clamp(highlighted, 0, shown.Count - 1)].run);
                e.StopPropagation();
                return true;
            }
            return true;   // typing goes to the search field
        }
    }
}
