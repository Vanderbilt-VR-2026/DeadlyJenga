using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The four ways a new file can add to the Studio window without editing it.
    // Implement one of these interfaces in a new file; the window finds the class by itself.
    // See progress/EXTENSION-POINTS.md.

    // A tab in the top bar (Set, Perform, Direct, Shoot, Cut...).
    public interface IStudioMode
    {
        string Title { get; }
        int Order { get; }                // tabs are sorted by this: Set 10, Perform 20, Direct 30, Shoot 40, Cut 50
        bool Available { get; }           // false hides the tab
        // Fill the panel on the right, above the inspector. Called when the tab opens and when the Set changes.
        void Build(VisualElement panel, StudioContext context);
        // What the big record button does in this mode. Return false to use the default (record the whole Set).
        bool TryGetRecord(StudioContext context, out string label, out Action start);
    }

    // A mode can also implement this to use the viewport its own way (pick blocks, aim arrows, place cameras).
    // Down returns true to take the drag; otherwise the viewport does its usual orbit and mark dragging.
    public interface IStudioViewportTool
    {
        bool Down(PointerDownEvent e, StudioViewport viewport, StudioContext context);
        void Move(PointerMoveEvent e, StudioViewport viewport, StudioContext context);
        void Up(PointerUpEvent e, StudioViewport viewport, StudioContext context);
    }

    // The settings shown for the selected thing (a mark, a cue, a track...).
    public interface IStudioInspector
    {
        bool Handles(object selection, StudioContext context);
        void Build(VisualElement panel, object selection, StudioContext context);
    }

    // Lines and dots drawn over the viewport (paths, block picks, camera frustums).
    public interface IStudioOverlay
    {
        void Draw(StudioOverlayPainter painter, StudioContext context);
    }

    // An entry in the command palette (Ctrl+K).
    public interface IStudioCommand
    {
        string Name { get; }
        bool Available(StudioContext context);
        void Run(StudioContext context);
    }

    // What an extension may read and do. One instance per window.
    public sealed class StudioContext
    {
        public TrailerStudioWindow window;

        public StudioSet Set => window.Set;
        public StudioTape Tape => window.Tape;
        public StudioStage Stage => StudioRun.Stage;
        public bool StageOpen => StudioRun.StageOpen;
        public int Frame => window.Frame;
        public object Selection => window.Selection;

        public void Select(object thing) => window.Select(thing);
        // Change the Set with undo. structural = marks or tracks were added, removed or renamed.
        public void Edit(string label, Action change, bool structural = false) => window.Edit(label, change, structural);
        public void Say(string message) => window.Say(message);
        public void Rebuild() => window.RebuildAll();
    }

    public static class StudioExtensions
    {
        static List<IStudioMode> modes;
        static List<IStudioInspector> inspectors;
        static List<IStudioOverlay> overlays;
        static List<IStudioCommand> commands;

        public static IReadOnlyList<IStudioMode> Modes => modes ??= StudioRegistry.Create<IStudioMode>().OrderBy(m => m.Order).ToList();
        public static IReadOnlyList<IStudioInspector> Inspectors => inspectors ??= StudioRegistry.Create<IStudioInspector>();
        public static IReadOnlyList<IStudioOverlay> Overlays => overlays ??= StudioRegistry.Create<IStudioOverlay>();
        public static IReadOnlyList<IStudioCommand> Commands => commands ??= StudioRegistry.Create<IStudioCommand>().OrderBy(c => c.Name).ToList();
    }

    // The examples in Tests/ only show up while this is on (command palette: "Show example extensions").
    public static class StudioExamples
    {
        const string Key = "DeadlyJenga.TrailerStudio.Examples";
        public static bool Enabled
        {
            get => UnityEditor.SessionState.GetBool(Key, false);
            set => UnityEditor.SessionState.SetBool(Key, value);
        }
    }

    // Small helpers so every panel looks the same.
    public static class StudioUi
    {
        // Small line icons share the tab foreground in both editor skins.
        public static VisualElement ModeGlyph(string mode)
        {
            var glyph = new VisualElement { pickingMode = PickingMode.Ignore };
            glyph.AddToClassList("ts-mode-glyph");
            glyph.generateVisualContent += mesh =>
            {
                var pen = mesh.painter2D;
                pen.strokeColor = glyph.resolvedStyle.color;
                pen.lineWidth = 1.5f;
                pen.lineCap = LineCap.Round;
                void Line(float x, float y, float endX, float endY)
                {
                    pen.BeginPath(); pen.MoveTo(new Vector2(x, y)); pen.LineTo(new Vector2(endX, endY)); pen.Stroke();
                }
                void Circle(float x, float y, float radius)
                {
                    pen.BeginPath(); pen.Arc(new Vector2(x, y), radius, 0, 360); pen.Stroke();
                }
                switch (mode)
                {
                    case "Set":
                        Circle(9, 6, 4); Line(6, 9, 9, 15); Line(9, 15, 12, 9);
                        break;
                    case "Perform":
                        Circle(9, 5, 3); pen.BeginPath(); pen.MoveTo(new Vector2(3, 15));
                        pen.BezierCurveTo(new Vector2(3, 8), new Vector2(15, 8), new Vector2(15, 15)); pen.Stroke();
                        break;
                    case "Polish":
                        Line(2, 5, 7, 5); Circle(9, 5, 2); Line(11, 5, 16, 5);
                        Line(2, 13, 4, 13); Circle(6, 13, 2); Line(8, 13, 16, 13);
                        break;
                    case "Direct":
                        Line(4, 16, 4, 2); Line(4, 2, 15, 4); Line(15, 4, 11, 9); Line(11, 9, 4, 8);
                        break;
                    case "Shoot":
                        Line(2, 5, 11, 5); Line(11, 5, 11, 13); Line(11, 13, 2, 13); Line(2, 13, 2, 5);
                        Line(11, 8, 16, 5); Line(16, 5, 16, 13); Line(16, 13, 11, 10);
                        break;
                    case "Cut":
                        Circle(4, 5, 2); Circle(4, 13, 2); Line(6, 6, 15, 14); Line(6, 12, 15, 4);
                        break;
                }
            };
            return glyph;
        }

        public static Label Heading(string text) { var label = new Label(text); label.AddToClassList("ts-heading"); return label; }
        public static Label Hint(string text) { var label = new Label(text); label.AddToClassList("ts-hint"); return label; }
        public static Label Warning(string text) { var label = new Label(text); label.AddToClassList("ts-warning"); return label; }

        public static Button Big(string text, Action click, string tooltip = "")
        {
            var button = new Button(click) { text = text, tooltip = tooltip };
            button.AddToClassList("ts-big");
            return button;
        }

        public static Button Small(string text, Action click, string tooltip = "")
        {
            var button = new Button(click) { text = text, tooltip = tooltip };
            button.AddToClassList("ts-small");
            return button;
        }

        public static VisualElement Row(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.AddToClassList("ts-row");
            foreach (var child in children) row.Add(child);
            return row;
        }

        public static VisualElement Card(params VisualElement[] children)
        {
            var card = new VisualElement();
            card.AddToClassList("ts-card");
            foreach (var child in children) card.Add(child);
            return card;
        }

        // A fold for the settings most people never need.
        public static Foldout Advanced(params VisualElement[] children)
        {
            var fold = new Foldout { text = "Advanced", value = false };
            fold.AddToClassList("ts-advanced");
            foreach (var child in children) fold.Add(child);
            return fold;
        }

        public static VisualElement Dot(Color color)
        {
            var dot = new VisualElement();
            dot.AddToClassList("ts-dot");
            dot.style.backgroundColor = color;
            return dot;
        }

        public static string Time(int frame, int fps)
        {
            float seconds = frame / (float)Mathf.Max(1, fps);
            return $"{Mathf.FloorToInt(seconds / 60):00}:{seconds % 60:00.00}";
        }
    }
}
