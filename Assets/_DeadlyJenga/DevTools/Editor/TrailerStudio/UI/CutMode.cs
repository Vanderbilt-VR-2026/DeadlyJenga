using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The Cut tab: put rendered shots in order, trim them, and hand the cut to an editing app.
    // The real finishing (titles, grade, sound mix) happens in Resolve or Premiere.
    public sealed class CutMode : IStudioMode
    {
        public string Title => "Cut";
        public int Order => 50;
        public bool Available => true;
        public bool TryGetRecord(StudioContext context, out string label, out Action start) { label = null; start = null; return false; }

        static bool snap = true;

        public void Build(VisualElement panel, StudioContext context)
        {
            panel.Add(StudioUi.Heading("CUT"));
            var set = context.Set;
            if (set == null) { panel.Add(StudioUi.Hint("Choose or make a Set first.")); return; }
            if (set.cameras.Count == 0) { panel.Add(StudioUi.Hint("Add cameras in the Shoot tab first. The cut is made of pieces of their shots.")); return; }
            int fps = set.render.fps;

            panel.Add(StudioUi.Big("+  Add a shot to the cut", () =>
            {
                var menu = new GenericMenu();
                foreach (var camera in set.cameras)
                {
                    var captured = camera;
                    menu.AddItem(new GUIContent(camera.name), false, () => context.Edit("Add to cut", () =>
                        set.cuts.Add(new StudioCut { camera = captured.id, from = 0, to = StudioCameras.OutputLength(captured, set, fps) })));
                }
                menu.ShowAsContext();
            }));
            if (set.cuts.Count == 0)
            {
                panel.Add(StudioUi.Small("Use every camera once, in order", () => context.Edit("Build cut", () =>
                {
                    foreach (var camera in set.cameras.OrderBy(c => c.start)) set.cuts.Add(new StudioCut { camera = camera.id, from = 0, to = StudioCameras.OutputLength(camera, set, fps) });
                })));
                panel.Add(StudioUi.Hint("The cut is empty."));
            }

            float at = 0;
            for (int i = 0; i < set.cuts.Count; i++)
            {
                var cut = set.cuts[i]; int index = i;
                var camera = set.cameras.FirstOrDefault(c => c.id == cut.camera);
                var card = StudioUi.Card();
                if (camera == null)
                {
                    card.Add(StudioUi.Warning("This piece's camera was deleted."));
                    card.Add(StudioUi.Small("Remove", () => context.Edit("Remove from cut", () => set.cuts.RemoveAt(index))));
                    panel.Add(card);
                    continue;
                }
                float length = StudioCameras.OutputLength(camera, set, fps);
                var name = new Label($"{i + 1}. {camera.name}") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
                name.AddToClassList("ts-item-name");
                var up = StudioUi.Small("Up", () => context.Edit("Reorder cut", () => { set.cuts.RemoveAt(index); set.cuts.Insert(index - 1, cut); }));
                up.SetEnabled(i > 0);
                var down = StudioUi.Small("Down", () => context.Edit("Reorder cut", () => { set.cuts.RemoveAt(index); set.cuts.Insert(index + 1, cut); }));
                down.SetEnabled(i < set.cuts.Count - 1);
                card.Add(StudioUi.Row(StudioUi.Dot(StudioColors.For(camera.color)), name, up, down, StudioUi.Small("x", () => context.Edit("Remove from cut", () => set.cuts.RemoveAt(index)))));
                // Trim handles: in and out, inside the rendered shot. With snapping on, the piece's end lands on a beat of the cut.
                float start = at;
                card.Add(StudioFields.Slide(context, "In (s)", cut.from, 0, Mathf.Max(.1f, length - .1f), v => cut.from = Mathf.Min(v, cut.to - .1f)));
                card.Add(StudioFields.Slide(context, "Out (s)", Mathf.Min(cut.to, length), .1f, length, v =>
                {
                    float wanted = Mathf.Max(v, cut.from + .1f);
                    if (snap && set.bpm > 0) wanted = Mathf.Clamp(StudioExport.SnapToBeat(set, start + wanted - cut.from) - start + cut.from, cut.from + .1f, length);
                    cut.to = wanted;
                }));
                if (i > 0) card.Add(StudioFields.Slide(context, "Dissolve in (s)", cut.dissolve, 0, 2, v => cut.dissolve = v));
                float piece = Mathf.Max(0, Mathf.Min(cut.to, length) - cut.from);
                card.Add(StudioUi.Hint($"Plays from {at:0.00} s to {at + piece:0.00} s of the cut."));
                at += piece;
                panel.Add(card);
            }
            if (set.cuts.Count > 0) panel.Add(StudioUi.Hint($"The cut runs {at:0.00} s."));

            // Music bed and beats
            panel.Add(StudioUi.Heading("MUSIC"));
            panel.Add(StudioUi.Row(new Label(string.IsNullOrEmpty(set.music) ? "No music bed" : Path.GetFileName(set.music)) { style = { flexGrow = 1 } },
                StudioUi.Small("Choose…", () =>
                {
                    string file = EditorUtility.OpenFilePanel("Music bed", "", "wav,mp3,ogg,aiff");
                    if (file.Length > 0) context.Edit("Music bed", () => set.music = file);
                }),
                StudioUi.Small("None", () => context.Edit("Music bed", () => set.music = ""))));
            panel.Add(StudioFields.Number(context, "Beats per minute", set.bpm, v => set.bpm = Mathf.Clamp(v, 0, 300)));
            panel.Add(StudioFields.Number(context, "First beat at (s)", set.beatOffset, v => set.beatOffset = Mathf.Max(0, v)));
            var snapping = new Toggle("Snap cuts to the beat") { value = snap };
            snapping.RegisterValueChangedCallback(e => snap = e.newValue);
            panel.Add(snapping);

            // Export
            panel.Add(StudioUi.Heading("EXPORT"));
            string folder = StudioRender.LastFolder;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                // After a restart, use the newest render of this Set.
                string renders = StudioRender.RendersFolder(set.id);
                folder = Directory.Exists(renders) ? Directory.GetDirectories(renders).OrderBy(d => d).LastOrDefault() ?? "" : "";
            }
            if (string.IsNullOrEmpty(folder)) { panel.Add(StudioUi.Hint("Render the cameras in the Shoot tab first. The export points at those files.")); return; }
            panel.Add(StudioUi.Hint("Using the render in " + Path.GetFileName(folder) + "."));
            string audio = StudioRender.AudioFile(set.id);
            if (!File.Exists(audio)) panel.Add(StudioUi.Hint("No game sound recorded yet (Shoot tab, \"Record the tape's sound\"). The cut will export without it."));
            string captured = folder;
            var export = StudioUi.Big("Export for Resolve or Premiere", () =>
            {
                try
                {
                    string xml = StudioExport.WriteFcpxml(set, captured, audio, out var problems);
                    string edl = StudioExport.WriteEdl(set, captured, out _);
                    context.Say(problems.Count > 0 ? "Exported with gaps: " + problems[0] : "Exported " + Path.GetFileName(xml) + " and " + Path.GetFileName(edl) + ".");
                    EditorUtility.RevealInFinder(xml);
                }
                catch (Exception error) { context.Say(error.Message); }
            }, "Writes an FCPXML file and an EDL next to the rendered shots.");
            export.AddToClassList("ts-big--primary");
            export.SetEnabled(set.cuts.Count > 0);
            panel.Add(export);
        }
    }
}
