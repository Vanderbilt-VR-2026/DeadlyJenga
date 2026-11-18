using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        static TrailerStudioWindow TourWindow()
        {
            var window = EditorWindow.GetWindow<TrailerStudioWindow>("Trailer Studio");
            window.position = new Rect(40, 60, 1520, 900);
            window.Show();
            window.Focus();
            return window;
        }

        // Copy the window's pixels from the screen. The window must be visible and not covered.
        static bool Shoot(TrailerStudioWindow window, string name)
        {
            try
            {
                window.Repaint();
                float scale = EditorGUIUtility.pixelsPerPoint;
                var area = window.position;
                int width = Mathf.RoundToInt(area.width * scale), height = Mathf.RoundToInt(area.height * scale);
                var pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(new Vector2(area.x * scale, area.y * scale), width, height);
                var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                picture.SetPixels(pixels);
                picture.Apply(false);
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), picture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(picture);
                // A real picture has many different colours; a blank one means the window was not on screen.
                return pixels.Where((_, i) => i % 997 == 0).Select(c => ((Color32)c).r / 8 * 1024 + ((Color32)c).g / 8 * 32 + ((Color32)c).b / 8).Distinct().Count() > 12;
            }
            catch (Exception error) { Debug.LogWarning("[TrailerStudio acceptance] screenshot failed: " + error.Message); return false; }
        }

        static IEnumerable<int> Settle(TrailerStudioWindow window, int frames = 12)
        {
            for (int i = 0; i < frames; i++) { window.Repaint(); yield return 0; }
        }

        static IEnumerator<int> WindowTour()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            int fps = set.fps;
            var window = TourWindow();
            foreach (int wait in Settle(window, 30)) yield return wait;
            var context = window.Context;
            var shots = new List<string>(); int good = 0;
            void Take(string name) { shots.Add(name); if (Shoot(window, name)) good++; }

            // Set: marks on the surface, a cue, a mark selected.
            foreach (var mark in set.marks)
                if (StudioStage.SurfaceBelow(stage.ToWorld(mark.position), out var surface)) mark.position = stage.ToStage(surface);
            var hand = set.AddMark(StudioRole.Hand, new Vector3(-4, 6, -6));
            StudioLibrary.Save(set); StudioRun.Refresh();
            window.ShowMode("Set"); context.Select(null);
            foreach (int wait in Settle(window)) yield return wait;
            Take("01-set-stage-open");
            context.Select(set.marks[0]);
            foreach (int wait in Settle(window)) yield return wait;
            Take("02-set-mark-selected");

            // Record the whole Set; a picture while it rolls, and one after.
            StudioRun.RecordWholeSet();
            int rolled = 0;
            while (StudioRun.State != StudioState.Held)
            {
                rolled++;
                if (StudioRun.Recording && StudioRun.Frame == set.Frames / 2) { window.Repaint(); Take("03-recording"); }
                if (rolled > 20000) throw new TimeoutException("The recording did not finish.");
                yield return 0;
            }
            window.Scrub(set.Frames / 2);
            foreach (int wait in Settle(window)) yield return wait;
            Take("04-recorded-timeline");

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

            // Direct: blocks picked, a push cue with its arrow.
            window.ShowMode("Direct");
            DirectMode.Picked.Clear();
            DirectMode.Picked.AddRange(Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeSelf)
                .OrderBy(i => (stage.blocks[i].position - stage.anchor).sqrMagnitude).Take(6));
            var push = new StudioCue { at = 2, action = PushCue.Id, blocks = new List<int>(DirectMode.Picked), direction = new Vector3(1, .3f, .4f), strength = 9, ripple = .1f };
            context.Edit("Add push", () => set.cues.Add(push));
            context.Select(push);
            foreach (int wait in Settle(window)) yield return wait;
            Take("07-direct-push");

            // Shoot: two cameras, one selected; then looking through it.
            window.ShowMode("Shoot");
            var wide = ShootMode.AddCamera(context);
            StudioCamera.Orbit(60, -6); StudioCamera.Zoom(.6f);
            context.Edit("Camera key", () => StudioCameras.AddKey(wide, StudioCameras.KeyFromView(stage, wide, set.length * .8f)));
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

            // Command palette and shortcut sheet.
            window.ShowMode("Set");
            window.ShowPalette(true);
            foreach (int wait in Settle(window)) yield return wait;
            Take("11-command-palette");
            window.ShowPalette(false);
            foreach (int wait in Settle(window)) yield return wait;
            Take("12-shortcuts");
            window.ClosePalette();

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

            var tabs = window.rootVisualElement.Query<Button>(className: "ts-tab").ToList().Select(b => b.text).ToList();
            Result("Window tour: every tab used in a real editor window", good == shots.Count && tabs.Count >= 6,
                $"Tabs: {string.Join(", ", tabs)}. {good} of {shots.Count} screenshots show real content ({string.Join(", ", shots)}). " +
                "The pictures are the window as drawn on screen, read back from the display.");
            Result("Undo and redo for Set edits", added == marksBefore + 1 && undone == marksBefore && redone == marksBefore + 1,
                $"Marks: {marksBefore}, after adding one {added}, after undo {undone}, after redo {redone} (Unity's own undo stack).");
            Result("Runtime errors during the tour", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioRun.SaveTape();
        }
    }
}
