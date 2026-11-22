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
        VisualElement results;
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
            layer.pickingMode = PickingMode.Ignore;
        }

        // ---- Commands ----

        public void ShowCommands()
        {
            OpenBox();
            search = new TextField();
            search.RegisterValueChangedCallback(_ => { highlighted = 0; Filter(); });
            box.Add(search);
            results = new ScrollView();
            box.Add(results);
            box.Add(StudioUi.Hint("Type to search.  Use the arrows to choose. Enter runs the selection.  Esc closes."));
            highlighted = 0;
            Filter();
            search.schedule.Execute(() => search.Focus());
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
        }

        void Filter()
        {
            string[] words = (search?.value ?? "").ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            shown = AllCommands().Where(c => words.All(w => c.name.ToLowerInvariant().Contains(w))).Take(40).ToList();
            highlighted = Mathf.Clamp(highlighted, 0, Mathf.Max(0, shown.Count - 1));
            results.Clear();
            for (int i = 0; i < shown.Count; i++)
            {
                var command = shown[i];
                var item = new Button(() => Run(command.run)) { text = command.name };
                item.AddToClassList("ts-small"); item.AddToClassList("ts-palette-item");
                item.EnableInClassList("ts-palette-item--on", i == highlighted);
                results.Add(item);
            }
            if (shown.Count == 0) results.Add(StudioUi.Hint("Nothing by that name."));
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
            ("Ctrl+Z  Ctrl+Y", "Undo, redo"), ("Ctrl+K", "Command palette: reach anything by name"), ("?", "This sheet"),
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
            if (e.keyCode == KeyCode.DownArrow) { highlighted++; Filter(); e.StopPropagation(); return true; }
            if (e.keyCode == KeyCode.UpArrow) { highlighted--; Filter(); e.StopPropagation(); return true; }
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
