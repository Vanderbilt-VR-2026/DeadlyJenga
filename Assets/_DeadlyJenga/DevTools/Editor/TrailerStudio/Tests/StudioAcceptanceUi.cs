using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Checks from the review of the window, the director, the environment and the importers:
    //   -executeMethod DeadlyJenga.DevTools.Editor.TrailerStudio.StudioAcceptance.RunUi
    // Batch mode has no mouse and no real layout, so the window part only checks what can be read or called.
    public static partial class StudioAcceptance
    {
        public static void RunUi() => Begin("Ui");

        static IEnumerator<int> UiTests()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            int fps = set.fps;

            // ---- U1. Timeline: frame <-> x, also before the lane is laid out ----
            {
                int frames = 180; float width = 900;
                bool roundTrip = Enumerable.Range(0, frames).All(f => StudioTimeline.XToFrame(StudioTimeline.FrameToX(f, frames, width), frames, width) == f);
                bool edges = StudioTimeline.XToFrame(-50, frames, width) == 0 && StudioTimeline.XToFrame(1e9f, frames, width) == frames - 1 &&
                    StudioTimeline.XToFrame(3, 0, width) == 0;
                bool unlaid = !float.IsNaN(StudioTimeline.FrameToX(90, frames, float.NaN)) && StudioTimeline.XToFrame(float.NaN, frames, width) == 0 &&
                    StudioTimeline.XToFrame(0, frames, float.NaN) == 0 && StudioTimeline.XToFrame(0, frames, 0) == 0;
                Result("U1. Timeline frame <-> x", roundTrip && edges && unlaid,
                    $"Every frame of 180 survives frame -> x -> frame on a 900 px lane: {roundTrip}. Off the ends clamps: {edges}. A lane with NaN or 0 width gives numbers, not NaN: {unlaid}.");
            }

            // ---- U2. Popups with repeated names ----
            {
                var names = StudioFields.Distinct(new List<string> { "Mia", "Ben", "Mia", null, "Mia (2)" });
                bool ok = names.Count == 5 && names.Distinct().Count() == 5 && names[0] == "Mia" && names[2] == "Mia (2)" && names[3] == "";
                Result("U2. Repeated names in a popup can each be picked", ok, "Mia, Ben, Mia, (null), Mia (2) became: " + string.Join(" | ", names));
            }

            // ---- U3. Cue chains: order, delays, loops and missing links ----
            var savedCues = set.cues;
            try
            {
                StudioCue Cue(string id, string link, float at, float strength) =>
                    new StudioCue { id = id, action = SlowTimeCue.Id, link = link, at = at, strength = strength, duration = 0 };
                var root = Cue("r", "", .5f, .9f);
                var a = Cue("a", "r", 0, .2f);
                var b = Cue("b", "a", .2f, .3f);
                var c = Cue("c", "b", 0, .4f);
                set.cues = new List<StudioCue> { root, a, b, c, Cue("x", "y", 0, .6f), Cue("y", "x", 0, .7f), Cue("d", "gone", 0, .8f), Cue("s", "s", 0, .85f) };

                bool waits = DirectCueFields.WaitsFor(set, c, "r") && !DirectCueFields.WaitsFor(set, root, "c") &&
                    DirectCueFields.WaitsFor(set, set.cues[4], "x") && DirectCueFields.WaitsFor(set, set.cues[7], "s") && !DirectCueFields.WaitsFor(set, set.cues[6], "r");

                // Drive the director the way the frame loop does: clock first, then a cue that is due, then its links.
                var log = new List<string>();
                StudioDirector.Begin(stage, 0);
                bool rootRan = false;
                for (int f = 0; f <= 60; f++)
                {
                    float t = f / 30f;
                    StudioDirector.Tick(stage, t, log);
                    if (!rootRan && t >= root.at - .00001f)
                    {
                        rootRan = true;
                        log.Add($"{t:F3}s {StudioCues.Run(stage, root)}");
                        StudioDirector.Triggered(stage, root, log);
                    }
                }
                // The log is written with the editor's number format, so compare in that format.
                int Index(float strength) => log.FindIndex(l => l.Contains("to " + strength.ToString("0.##") + "×"));
                bool order = log.Count == 4 && Index(.9f) == 0 && Index(.2f) == 1 && Index(.3f) == 2 && Index(.4f) == 3 &&
                    log[1].StartsWith(.5f.ToString("F3")) && log[2].StartsWith(.7f.ToString("F3")) && log[3].StartsWith(.7f.ToString("F3"));

                // Nothing scheduled survives End.
                bool ranAfterEnd = false;
                StudioDirector.Begin(stage, 0);
                StudioDirector.At(1, () => ranAfterEnd = true);
                StudioDirector.End();
                StudioDirector.Tick(stage, 5, log);

                // Slow-time ranges.
                set.cues = new List<StudioCue> { new StudioCue { action = SlowTimeCue.Id, at = 1, duration = 2, strength = .5f } };
                StudioDirector.Begin(stage, 0);
                bool slow = Mathf.Approximately(StudioDirector.SlowFactor(.5f), 1) && Mathf.Approximately(StudioDirector.SlowFactor(2), .5f) && Mathf.Approximately(StudioDirector.SlowFactor(3.01f), 1);
                StudioDirector.End();
                slow &= Mathf.Approximately(StudioDirector.SlowFactor(2), 1);

                Result("U3. Chain reactions: order, delay, loops, missing links", waits && order && !ranAfterEnd && slow,
                    $"WaitsFor follows chains and stops in loops: {waits}. Log ({log.Count} lines, wanted root, a, b at +0.2 s, c): {string.Join(" / ", log)}. " +
                    $"A loop, a missing link and a self-link never ran. Action scheduled before End ran later: {ranAfterEnd}. Slow-time ranges right and cleared by End: {slow}.");
            }
            finally { set.cues = savedCues; StudioDirector.End(); }

            // ---- U4. Deleting a cue frees the cues that waited for it ----
            {
                var other = new StudioSet();
                var first = new StudioCue { action = "collapse", at = 1 };
                var waiting = new StudioCue { action = "collapse", at = .3f, link = first.id, linkOnHit = true };
                other.cues.Add(first); other.cues.Add(waiting);
                TrailerStudioWindow.RemoveCue(other, first);
                Result("U4. Deleting a cue frees the cues that waited for it", other.cues.Count == 1 && waiting.link == "" && !waiting.linkOnHit,
                    $"Cues left: {other.cues.Count}; the waiting cue's link \"{waiting.link}\", on hit {waiting.linkOnHit}.");
            }

            // ---- U5. Slow time while performing in real time, and the clock after the run ----
            {
                var slowCue = new StudioCue { action = SlowTimeCue.Id, at = .5f, duration = 1, strength = .5f };
                set.cues.Add(slowCue);
                float before = -1, inside = -1;
                void Watch(int frame)
                {
                    float t = frame / (float)fps;
                    if (t < .4f) before = Time.timeScale;
                    if (t > .7f && t < 1.3f) inside = Time.timeScale;
                }
                StudioRun.FrameShown += Watch;
                try
                {
                    StudioRun.Record(new StudioPass { label = "Slow time check", liveWorld = false, fixedClock = false, from = 0, to = fps * 2 });
                    foreach (int wait in UntilHeld()) yield return wait;
                }
                finally { StudioRun.FrameShown -= Watch; }
                float afterRun = Time.timeScale;

                // Stopped in the middle of the slow range.
                bool stopped = false;
                void StopInside(int frame) { if (!stopped && frame / (float)fps > .8f) { stopped = true; StudioRun.Stop(); } }
                StudioRun.FrameShown += StopInside;
                try
                {
                    StudioRun.Record(new StudioPass { label = "Slow time stop", liveWorld = false, fixedClock = false, from = 0, to = fps * 2 });
                    foreach (int wait in UntilHeld()) yield return wait;
                }
                finally { StudioRun.FrameShown -= StopInside; set.cues.Remove(slowCue); }
                float afterStop = Time.timeScale;
                Result("U5. Slow time while performing, and the clock afterwards",
                    Mathf.Approximately(before, 1) && Mathf.Approximately(inside, .5f) && afterRun == 0 && stopped && afterStop == 0,
                    $"Real-time pass: time scale {before:0.##} before the range, {inside:0.##} inside it, {afterRun} when the stage holds again. " +
                    $"Stopped inside the range: {stopped}, time scale then {afterStop} (the stage holds time still; closing the stage puts the game's own value back).");
            }

            // ---- U6. Time of day and weather are put back ----
            {
                var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).ToArray();
                var rotations = lights.Select(l => l.transform.rotation).ToArray();
                var intensities = lights.Select(l => l.intensity).ToArray();
                var colors = lights.Select(l => l.color).ToArray();
                bool fog0 = RenderSettings.fog; float density0 = RenderSettings.fogDensity, ambient0 = RenderSettings.ambientIntensity;
                Color fogColor0 = RenderSettings.fogColor; FogMode mode0 = RenderSettings.fogMode;
                bool FogBack() => RenderSettings.fog == fog0 && Mathf.Approximately(RenderSettings.fogDensity, density0) && RenderSettings.fogColor == fogColor0 && RenderSettings.fogMode == mode0;
                bool SunsTurnedBack() => lights.Select((l, i) => !l || Quaternion.Angle(l.transform.rotation, rotations[i]) < .01f).All(x => x);
                bool LightsBack() => SunsTurnedBack() && lights.Select((l, i) => !l || Mathf.Approximately(l.intensity, intensities[i]) && l.color == colors[i]).All(x => x);

                var env = new StudioSet { timeOfDay = -1, weather = "Fog" };
                StudioEnvironment.Apply(env);
                bool fogged = RenderSettings.fog && Mathf.Approximately(RenderSettings.fogDensity, .03f);
                env.timeOfDay = 17; env.weather = "";
                StudioEnvironment.Apply(env);
                bool fogGone = FogBack();
                env.timeOfDay = -1; env.weather = "Storm";
                StudioEnvironment.Apply(env);
                bool sunGone = SunsTurnedBack();
                StudioEnvironment.Restore();
                bool allBack = FogBack() && LightsBack() && Mathf.Approximately(RenderSettings.ambientIntensity, ambient0);
                StudioEnvironment.Apply(set);
                Result("U6. Time of day and weather are put back", fogged && fogGone && sunGone && allBack,
                    $"{lights.Length} directional light(s). Fog applied: {fogged}. Weather back to the map's own while the hour stays set removes the fog: {fogGone}. " +
                    $"Hour off while the weather stays set turns the sun back: {sunGone}. Everything back after Restore: {allBack}.");
            }

            // ---- U7. Set files with odd names, another number format, damaged files ----
            {
                string oldOverride = StudioLibrary.SetsFolderOverride;
                var oldCulture = Thread.CurrentThread.CurrentCulture;
                bool roundTrip = false, twoIds = false, damaged = false, trashed = false; string detail = "";
                try
                {
                    StudioLibrary.SetsFolderOverride = Path.Combine(Folder, "odd sets é 名");
                    Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                    string title = "Café: ü / 名字 \"quoted\"";
                    var first = StudioLibrary.Create(title);
                    first.length = 2.5f; first.timeOfDay = 6.25f;
                    StudioLibrary.Save(first);
                    var second = StudioLibrary.Create(title);
                    var back = StudioLibrary.Load(first.id);
                    roundTrip = back != null && back.title == title && Mathf.Approximately(back.length, 2.5f) && Mathf.Approximately(back.timeOfDay, 6.25f);
                    twoIds = first.id != second.id && StudioLibrary.SetIds().Contains(first.id) && StudioLibrary.SetIds().Contains(second.id);
                    File.WriteAllText(StudioLibrary.SetPath("damaged"), "{ \"title\": \"cut sh");
                    try { damaged = StudioLibrary.Load("damaged") == null; } catch (Exception error) { detail += "Loading a damaged file threw: " + error.Message + ". "; }
                    var copy = StudioLibrary.Duplicate(back);
                    StudioLibrary.Trash(copy);
                    string trash = Path.Combine(StudioLibrary.TapeFolder(copy.id), "trash");
                    trashed = !File.Exists(StudioLibrary.SetPath(copy.id)) && Directory.Exists(trash) && Directory.GetFiles(trash).Length == 1;
                    Directory.Delete(StudioLibrary.TapeFolder(copy.id), true);
                    detail += $"Ids \"{first.id}\" and \"{second.id}\". ";
                }
                catch (Exception error) { detail += "Error: " + error.Message + ". "; }
                finally { Thread.CurrentThread.CurrentCulture = oldCulture; StudioLibrary.SetsFolderOverride = oldOverride; }
                Result("U7. Set files: odd names, German number format, damaged file, trash", roundTrip && twoIds && damaged && trashed,
                    detail + $"Saved and loaded back with a German number format in a folder with spaces and non-ASCII letters: {roundTrip}. " +
                    $"Same title twice gives two Sets: {twoIds}. A damaged file loads as missing instead of throwing: {damaged}. Trash keeps a copy beside the tapes: {trashed}.");
            }

            // ---- U8. A damaged Theatre take folder is reported and leaves nothing behind ----
            {
                string folder = Path.Combine(Folder, "theatre take with spaces é");
                Directory.CreateDirectory(folder);
                string recipe = Path.Combine(folder, "recipe.json"), motion = Path.Combine(folder, "motion.djt");
                File.WriteAllText(recipe, "{\"title\":\"Damaged import\",\"map\":\"" + Map + "\",\"fps\":30,\"cast\":[{\"live\":false}],\"cues\":[{\"at\":1.0}]}");
                void Header(BinaryWriter w)
                {
                    w.Write(0x344A5444); w.Write(Map); w.Write(30); w.Write(10); w.Write(0); w.Write(0);
                    w.Write(0f); w.Write(0f); w.Write(0f);
                }
                string Outcome()
                {
                    int before = StudioLibrary.SetIds().Count;
                    string said;
                    try { TheatreImport.ImportTakeFolder(folder); said = "imported"; }
                    catch (InvalidDataException error) { said = "reported: " + error.Message; }
                    catch (Exception error) { said = "threw " + error.GetType().Name + ": " + error.Message; }
                    return said + (StudioLibrary.SetIds().Count == before ? "" : " (A SET WAS LEFT BEHIND)");
                }
                using (var w = new BinaryWriter(File.Create(motion))) { Header(w); w.Write("{}"); }   // cut off inside the first frame
                string cut = Outcome();
                using (var w = new BinaryWriter(File.Create(motion))) Header(w);                      // no frames at all
                string empty = Outcome();
                File.Delete(motion);
                int count = StudioLibrary.SetIds().Count;
                StudioSet plain = null; string plainNote;
                try { plain = TheatreImport.ImportTakeFolder(folder); plainNote = "imported"; }
                catch (Exception error) { plainNote = error.Message; }
                bool plainOk = plain != null && StudioLibrary.SetIds().Count == count + 1 && plain.marks.Count == 1 && !string.IsNullOrEmpty(plain.marks[0].id) &&
                    !string.IsNullOrEmpty(plain.marks[0].actor) && plain.cues.Count == 1 && plain.cues[0].action != null;
                if (plain != null) File.Delete(StudioLibrary.SetPath(plain.id));
                string missing;
                try { TheatreImport.ImportTakeFolder(Path.Combine(Folder, "no such folder")); missing = "imported"; }
                catch (FileNotFoundException) { missing = "reported"; }
                catch (Exception error) { missing = "threw " + error.GetType().Name; }
                Result("U8. Damaged Theatre take folders are reported, not half imported",
                    cut.StartsWith("reported: ") && empty.StartsWith("reported: ") && plainOk && missing == "reported",
                    $"Tape cut off inside a frame: {cut}. Tape with no frames: {empty}. Recipe only, with an actor without id and a cue without action: {plainNote}, " +
                    $"mark \"{plain?.marks.FirstOrDefault()?.id}\", fine: {plainOk}. Folder without recipe: {missing}.");
            }

            // ---- U9. The window: camera rows select, deleting a cue, look-through, times that follow the playhead ----
            string windowNote;
            bool windowOk = false;
            StudioCameraTrack camera = null;
            TrailerStudioWindow window = null;
            try
            {
                window = EditorWindow.GetWindow<TrailerStudioWindow>();
                if (window.Set != set) window.LoadSet(set.id);
                window.RebuildAll();
                windowNote = "";
            }
            catch (Exception error) { windowNote = "The window could not be opened (" + error.Message + ")."; window = null; }
            if (window != null)
            {
                var context = window.Context;
                camera = ShootMode.AddCamera(context);
                context.Select(null);

                // A camera's row in the left list selects it.
                var row = window.rootVisualElement.Q<ScrollView>("cast").Query<VisualElement>(className: "ts-item").ToList()
                    .FirstOrDefault(r => r.Q<Label>(className: "ts-item-name")?.text == camera.name);
                if (row != null) using (var click = ClickEvent.GetPooled()) { click.target = row; row.SendEvent(click); }
                yield return 0;
                bool rowSelects = (window.Selection as StudioCameraTrack)?.id == camera.id;

                // Delete key / inspector button on a cue another cue waits for.
                var first = new StudioCue { action = SlowTimeCue.Id, at = .5f };
                var waiting = new StudioCue { action = SlowTimeCue.Id, at = .2f, link = first.id };
                context.Edit("Test cues", () => { window.Set.cues.Add(first); window.Set.cues.Add(waiting); });
                context.Select(window.Set.cues.First(cue => cue.id == first.id));
                window.DeleteSelection();
                var left = window.Set.cues.FirstOrDefault(cue => cue.id == waiting.id);
                bool unlinked = left != null && left.link == "" && window.Set.cues.All(cue => cue.id != first.id);
                context.Edit("Remove test cue", () => window.Set.cues.RemoveAll(cue => cue.id == waiting.id));

                // Looking through a shot camera ends when another tab opens.
                window.ShowMode("Shoot");
                ShootMode.LookThrough = camera.id;
                window.Viewport.Tick();
                window.ShowMode("Set");
                window.Viewport.Tick();
                bool lookEnds = ShootMode.LookThrough == "";

                // "Key here (time)" follows the playhead after scrubbing, without anything else rebuilding the window.
                window.ShowMode("Shoot");
                window.Scrub(0);
                context.Select(window.Set.cameras.First(cam => cam.id == camera.id));
                var pulse = typeof(TrailerStudioWindow).GetMethod("Pulse", BindingFlags.NonPublic | BindingFlags.Instance);
                string KeyLabel() => window.rootVisualElement.Query<Button>().ToList().FirstOrDefault(button => button.text.StartsWith("Key here"))?.text ?? "(no Key here button)";
                string atStart = KeyLabel();
                int target = set.Frames / 2;
                window.Scrub(target);
                double until = EditorApplication.timeSinceStartup + 3;
                while (EditorApplication.timeSinceStartup < until && !KeyLabel().Contains(StudioUi.Time(target, fps)))
                {
                    pulse?.Invoke(window, null);
                    yield return 0;
                }
                string afterScrub = KeyLabel();
                bool follows = atStart.Contains(StudioUi.Time(0, fps)) && afterScrub.Contains(StudioUi.Time(target, fps));

                context.Select(window.Set.cameras.First(cam => cam.id == camera.id));
                window.DeleteSelection();
                window.ShowMode("Set");
                windowOk = rowSelects && unlinked && lookEnds && follows;
                windowNote = $"Clicking the camera's row in the left list selects it: {rowSelects}{(row == null ? " (row not found)" : "")}. " +
                    $"Deleting a cue frees the cue that waited for it: {unlinked}. Look-through ends in another tab: {lookEnds}. " +
                    $"Key button before scrubbing \"{atStart}\", after scrubbing to frame {target}: \"{afterScrub}\".";
            }
            else windowOk = Application.isBatchMode;
            Result("U9. Window: camera rows, cue delete, look-through, playhead times", windowOk, windowNote);

            Result("Runtime errors during the UI checks", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioRun.SaveTape();
        }
    }
}
