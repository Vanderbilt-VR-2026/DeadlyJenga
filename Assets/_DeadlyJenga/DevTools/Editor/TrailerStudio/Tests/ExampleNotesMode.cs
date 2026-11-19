using System;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // EXAMPLE: a whole mode tab in one new file. Nothing else in the Studio was edited to add it.
    // It only shows while examples are on (Ctrl+K, "Show example extensions").
    // Copy this file as the starting point for a real mode (Direct, Shoot, Cut...).
    public sealed class ExampleNotesMode : IStudioMode
    {
        public string Title => "Notes";
        public int Order => 900;
        public bool Available => StudioExamples.Enabled;

        public void Build(VisualElement panel, StudioContext context)
        {
            panel.Add(StudioUi.Heading("EXAMPLE MODE"));
            panel.Add(StudioUi.Hint("This tab comes from Tests/ExampleNotesMode.cs. It shows what the example track recorded for the current frame."));
            panel.Add(StudioUi.Card(new Label(string.IsNullOrEmpty(ExampleNoteTrack.LastShown) ? "Nothing shown yet. Record, then scrub." : ExampleNoteTrack.LastShown)));
        }

        // This mode keeps the default record button (record the whole Set).
        public bool TryGetRecord(StudioContext context, out string label, out Action start)
        {
            label = null; start = null;
            return false;
        }
    }
}
