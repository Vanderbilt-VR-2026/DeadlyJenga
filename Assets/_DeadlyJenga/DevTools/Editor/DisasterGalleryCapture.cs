using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Pictures of every catalog disaster in the real Game scene (Candy Castle, demo mode, no bots).
    // For each disaster: launch with no warning, then a wide shot early, and a wide and a close shot later.
    // Run in batch: -executeMethod DeadlyJenga.DevTools.Editor.DisasterGalleryCapture.RunBatch
    // Pictures go to Logs/Captures/<time>-Disaster-Gallery with a contact sheet. Changes nothing in the project.
    [InitializeOnLoad]
    public static class DisasterGalleryCapture
    {
        const string Key = "DisasterGalleryCapture";
        static IEnumerator run;
        static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
        static CaptureOutput output;
        static bool failed;
        static string Report => ProjectPath.Report("DisasterGallery.txt");

        static DisasterGalleryCapture() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += State; }

        [MenuItem("Tools/Deadly Jenga/Captures/Disaster Gallery (Play Mode)")]
        public static void RunBatch() => Start("");

        // Only the tsunami, with extra framed shots (side profile, low front, three-quarter). Fits a short job.
        [MenuItem("Tools/Deadly Jenga/Captures/Tsunami Shape (Play Mode)")]
        public static void RunTsunamiBatch() => Start("Tsunami");

        static void Start(string only)
        {
            SessionState.SetString(Key + "Only", only);
            SessionState.SetBool(Key, true);
            File.WriteAllText(Report, "Disaster gallery: Game.unity, castle map, demo mode. " + DateTime.Now.ToString("O") + "\n");
            MatchSetup.MapId = "castle"; MatchSetup.ModeId = "demo"; MatchSetup.BotTarget = 1; MatchSetup.HandCount = 0;
            MatchSetup.FromLobby = true; MatchSetup.StartLocalSession = false;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                failed = false; AudioListener.volume = 0; Application.runInBackground = true;
                run = Capture();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 2 : 0);
            }
        }

        static void Tick()
        {
            if (run == null) return;
            try
            {
                if (steps.Count == 0) steps.Push(run);
                while (steps.Count > 0)
                {
                    var step = steps.Peek();
                    if (!step.MoveNext()) { steps.Pop(); continue; }
                    if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                    return;
                }
                File.AppendAllText(Report, "COMPLETE\n");
            }
            catch (Exception error) { failed = true; File.AppendAllText(Report, "FAIL " + error + "\n"); }
            steps.Clear(); run = null;
            output?.Finish(false);
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator Capture()
        {
            float deadline = Time.realtimeSinceStartup + 65;
            while ((Services.Session?.Demo == null || Services.World == null) && Time.realtimeSinceStartup < deadline) yield return null;
            var session = Object.FindFirstObjectByType<CastlePlaySession>();
            if (!session || Services.World == null || !session.ActiveCamera) throw new InvalidOperationException("Game session or camera is unavailable.");
            if (session.Network) throw new InvalidOperationException("Refusing to run in a network session.");
            session.SurvivorBots.AutoFill = false; session.SurvivorBots.Clear(); session.HandBots?.Clear();
            if (session.Social != null) session.Social.VoiceEnabled = false;
            session.SetRole(true); session.SetMenu(false); session.Player.InputSuspended = true; session.Hand.InputSuspended = true;
            string only = SessionState.GetString(Key + "Only", "");
            output = new CaptureOutput(only == "" ? "Disaster Gallery" : only + " Shape");
            int index = 0;
            foreach (var prefab in DemoDisasters.Prefabs)
            {
                index++;
                if (only != "" && prefab.name != only) continue;
                string name = index.ToString("00") + "-" + prefab.name.Replace(" ", "");
                DemoDisasters.StopAll(session); session.ResetMap();
                yield return new WaitForSecondsRealtime(1.5f);
                var parameters = DemoDisasters.Parameters(prefab);
                for (int i = 0; i < parameters.Count; i++) if (parameters[i].id == "warning") DemoDisasters.SetValue(session, prefab, i, 0);
                string result = DemoDisasters.Request(session, prefab);
                File.AppendAllText(Report, name + ": " + result + "\n");
                yield return new WaitForSecondsRealtime(3f);
                Shot(session, name + "-a-wide-3s", false);
                yield return new WaitForSecondsRealtime(5f);
                if (prefab.name == "Tsunami")
                {
                    float until = Time.realtimeSinceStartup + 20;
                    while (Time.realtimeSinceStartup < until && !Tracked.Waves.Any(w => w.Wave.active && w.Wave.height > 18)) yield return null;
                }
                Shot(session, name + "-b-wide-8s", false);
                Shot(session, name + "-c-close-8s", true);
                if (prefab.name == "Tsunami") yield return TsunamiShots(session, name);
            }
            DemoDisasters.StopAll(session);
        }

        static IEnumerator TsunamiShots(CastlePlaySession session, string name)
        {
            var source = Tracked.Waves.FirstOrDefault(w => w.Wave.active);
            if (source == null) { File.AppendAllText(Report, "  no active wave for tsunami shots\n"); yield break; }
            for (int round = 0; round < 2; round++)
            {
                var w = source.Wave;
                Vector3 across = Vector3.Cross(Vector3.up, w.direction).normalized, up = Vector3.up, mid = w.center + up * w.height * .5f;
                float span = Mathf.Max(w.height * 2.2f, w.depth * 1.6f);
                View(session, name + "-t" + round + "-side-profile", mid + across * span, mid, 50);
                View(session, name + "-t" + round + "-front-low", w.center + w.direction * (w.depth * .5f + w.height * 1.4f) + up * 2 + across * w.height * .4f, w.center + up * w.height * .65f, 70);
                View(session, name + "-t" + round + "-three-quarter", w.center + (w.direction * .9f + across * .7f).normalized * span * 1.4f + up * w.height * .6f, mid, 55);
                yield return new WaitForSecondsRealtime(3f);
            }
        }

        static void View(CastlePlaySession session, string file, Vector3 position, Vector3 target, float fov)
        {
            var camera = session.ActiveCamera;
            camera.transform.position = position; camera.transform.LookAt(target); camera.fieldOfView = fov; camera.farClipPlane = 2500;
            Render(session, file, position, target);
        }

        // The disaster's own center (tornado, kraken, wave) when it publishes one, else the map center.
        static Vector3 Focus(out bool found)
        {
            found = false;
            var wave = Tracked.Waves.FirstOrDefault(w => w.Wave.active);
            if (wave != null) { found = true; return wave.Wave.center + Vector3.up * wave.Wave.height * .4f; }
            foreach (var state in DisasterStates.Active)
            {
                var frame = state?.Capture();
                if (frame != null && frame.center != Vector3.zero) { found = true; return frame.center; }
            }
            return Services.World.Center;
        }

        static void Shot(CastlePlaySession session, string file, bool close)
        {
            var world = Services.World;
            var focus = Focus(out bool found);
            var center = world.Center + Vector3.up * world.TopHeight * .45f;
            var target = found ? Vector3.Lerp(center, focus, .5f) : center;
            float span = Mathf.Max(world.Radius * 2, Vector3.Distance(center, focus) * 2, 30);
            Vector3 position;
            if (close) position = world.Center + new Vector3(.75f, 0, -1).normalized * world.Radius * 1.25f + Vector3.up * world.TopHeight * .7f;
            else position = target + new Vector3(.7f, .5f, -1).normalized * span * 1.5f;
            var camera = session.ActiveCamera;
            camera.transform.position = position; camera.transform.LookAt(close ? (found ? focus : center) : target);
            camera.fieldOfView = 60; camera.farClipPlane = 2500;
            Render(session, file, position, focus);
        }

        static void Render(CastlePlaySession session, string file, Vector3 position, Vector3 focus)
        {
            var camera = session.ActiveCamera;
            foreach (var art in Object.FindObjectsByType<CandyDisasterVisuals>(FindObjectsSortMode.None))
                typeof(CandyDisasterVisuals).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(art, null);
            foreach (var art in Object.FindObjectsByType<CandyTsunamiVisuals>(FindObjectsSortMode.None))
                typeof(CandyTsunamiVisuals).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(art, null);
            var rt = RenderTexture.GetTemporary(1280, 720, 24); var old = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                string path = output.PathFor(file + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                output.Add(path, file, "image", 1280, 720);
                File.AppendAllText(Report, "  shot " + file + " camera=" + position + " focus=" + focus + "\n");
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.Destroy(texture); }
        }
    }
}
