using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // The sprint 4 APK report: 100 bots ran well until a very destructive tornado, then frames took about two seconds.
    // This plays that in the real Game.unity (demo mode, 100 bots, a tornado with every setting at its maximum) and
    // measures real frame times. It is an editor CPU measurement for comparing two revisions, not a Quest frame rate.
    // Runs only when asked. Saves nothing.
    [InitializeOnLoad]
    public static class TornadoCrowdCheck
    {
        const string Key = "TornadoCrowdCheck";
        const int Bots = 100;
        static readonly Stack<IEnumerator> work = new Stack<IEnumerator>();
        static string Report => ProjectPath.Report("TornadoCrowd.txt");
        static TornadoCrowdCheck() { EditorApplication.playModeStateChanged += Changed; }

        [MenuItem("Tools/Deadly Jenga/Performance/Tornado Through 100 Bots")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            File.WriteAllText(Report, "Tornado through " + Bots + " bots: Game.unity, demo mode, every tornado setting at maximum. Editor frame times.\n");
            SessionState.SetInt(Key + "Failures", 0); SessionState.SetInt(Key + "Checks", 0); SessionState.SetInt(Key, 1);
            MatchSetup.FromLobby = false; MatchSetup.StartLocalSession = false;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if (SessionState.GetInt(Key, 0) == 0) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Application.logMessageReceived += Log;
                work.Push(Play());
                EditorApplication.update += Tick;
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            Application.logMessageReceived -= Log;
            int failures = SessionState.GetInt(Key + "Failures", 0);
            if (SessionState.GetInt(Key + "Checks", 0) == 0) { failures++; Line("FAIL: Play Mode ended before any checks ran."); }
            SessionState.SetInt(Key, 0);
            Line(failures > 0 ? "FAILED (" + failures + ")" : "COMPLETE: editor measurement; Quest hardware not tested.");
            if (Application.isBatchMode) EditorApplication.Exit(failures > 0 ? 1 : 0);
        }
        static void Line(string text) => File.AppendAllText(Report, text + "\n");
        static void Fail() => SessionState.SetInt(Key + "Failures", SessionState.GetInt(Key + "Failures", 0) + 1);
        static int errors;
        static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            // Log the first few; a storm of the same error must not bury the timings.
            if (errors++ < 5) { Fail(); Line("FAIL Unity: " + message + "\n" + stack); }
        }
        static void Tick()
        {
            try
            {
                if (work.Count == 0) { EditorApplication.update -= Tick; EditorApplication.ExitPlaymode(); return; }
                if (measuring) Measure();
                var next = work.Peek();
                if (!next.MoveNext()) { work.Pop(); return; }
                if (next.Current is IEnumerator nested) work.Push(nested);
            }
            catch (Exception e) { Fail(); Line("FAIL " + e); work.Clear(); }
        }
        static void Check(bool condition, string message)
        {
            SessionState.SetInt(Key + "Checks", SessionState.GetInt(Key + "Checks", 0) + 1);
            if (!condition) Fail();
            Line((condition ? "PASS " : "FAIL ") + message);
        }
        static IEnumerator WaitReal(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        // ---------- frame timing ----------
        static bool measuring; static int lastFrame; static float lastReal, gameTimeAtStart, realAtStart;
        static readonly List<float> frames = new List<float>();
        static int mostGhosts, fewestAlive; static ISurvivorBotPopulation crowd;
        static void Begin()
        {
            frames.Clear(); measuring = true; lastFrame = Time.frameCount; lastReal = Time.realtimeSinceStartup;
            gameTimeAtStart = Time.time; realAtStart = lastReal; mostGhosts = 0; fewestAlive = int.MaxValue;
        }
        // Called from the editor's own update, so one game frame can be seen more than once or not at all; only real
        // frame changes are counted, and the time is spread over the frames that passed.
        static void Measure()
        {
            int frame = Time.frameCount; float now = Time.realtimeSinceStartup;
            if (frame == lastFrame) return;
            float each = (now - lastReal) / (frame - lastFrame);
            for (int i = lastFrame; i < frame; i++) frames.Add(each);
            lastFrame = frame; lastReal = now;
            if (frame % 10 == 0)
            {
                mostGhosts = Mathf.Max(mostGhosts, Object.FindObjectsByType<CandyMelt>(FindObjectsSortMode.None).Length);
                if (crowd != null) fewestAlive = Mathf.Min(fewestAlive, crowd.Members.Count());
            }
        }
        static void End(string what)
        {
            measuring = false;
            if (frames.Count == 0) { Check(false, what + ": no frames were measured"); return; }
            var sorted = frames.OrderBy(f => f).ToList();
            float Percentile(float p) => sorted[Mathf.Clamp(Mathf.FloorToInt(sorted.Count * p), 0, sorted.Count - 1)] * 1000;
            float real = Time.realtimeSinceStartup - realAtStart, game = Time.time - gameTimeAtStart;
            Line($"TIMING {what}: {frames.Count} frames in {real:F1} s ({frames.Count / Mathf.Max(.001f, real):F0} fps average); " +
                $"median {Percentile(.5f):F0} ms, 95% under {Percentile(.95f):F0} ms, 99% under {Percentile(.99f):F0} ms, worst {sorted[sorted.Count - 1] * 1000:F0} ms; " +
                $"frames over 100 ms: {frames.Count(f => f > .1f)}, over 500 ms: {frames.Count(f => f > .5f)}, over 1 s: {frames.Count(f => f > 1)}; " +
                $"game time advanced {game:F1} s; most death ghosts at once {mostGhosts}; fewest bots alive {(fewestAlive == int.MaxValue ? -1 : fewestAlive)}");
            worst = sorted[sorted.Count - 1]; overHalfSecond = frames.Count(f => f > .5f);
        }
        static float worst; static int overHalfSecond;

        static IEnumerator Play()
        {
            float until = Time.realtimeSinceStartup + 60;
            while (!(Services.Session is CastlePlaySession) && Time.realtimeSinceStartup < until) yield return null;
            var session = Services.Session as CastlePlaySession;
            Check(session, "Game.unity started a play session");
            if (!session) yield break;
            if (session.Social != null) session.Social.VoiceEnabled = false;
            MatchSetup.ModeId = "demo"; MatchSetup.FromLobby = false; MatchSetup.ApplyMap();
            yield return WaitReal(1);
            int vsync = QualitySettings.vSyncCount, rate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            try
            {
                session.SurvivorBots.Clear(); session.HandBots.Clear(); session.Demo.Run(DemoCommand.StopDisasters);
                UnityEngine.Random.InitState(4127); session.ResetMap();
                yield return WaitReal(3);
                crowd = session.SurvivorBots; crowd.AutoFill = false;
                int spawned = 0;
                for (int i = 0; i < Bots; i++) { if (crowd.SpawnOne()) spawned++; if (i % 10 == 9) yield return null; }
                Check(spawned >= Bots * .9f, spawned + " of " + Bots + " bots spawned");
                // Watch from outside the map, as a spectating player would.
                var bounds = Services.World.PlayableBounds; var camera = session.ActiveCamera;
                if (camera) { camera.transform.position = bounds.center + new Vector3(bounds.size.x * .8f, bounds.size.y * .7f, -bounds.size.z); camera.transform.LookAt(bounds.center); }
                yield return WaitReal(8);

                Begin(); yield return WaitReal(8); End("100 bots, calm");
                float calmWorst = worst;

                var demo = session.Demo; int tornado = -1;
                for (int i = 0; i < demo.DisasterCount; i++) if (demo.DisasterLabel(i).IndexOf("Tornado", StringComparison.OrdinalIgnoreCase) >= 0) tornado = i;
                Check(tornado >= 0, "The tornado is in the disaster list");
                if (tornado < 0) yield break;
                var settings = demo.DisasterParameters(tornado); string used = "";
                for (int p = 0; p < settings.Count; p++)
                {
                    // Everything at its most violent, no warning, a short run so it all happens at once.
                    float value = settings[p].id == "warning" ? settings[p].min : settings[p].id == "duration" ? 25 : settings[p].max;
                    demo.SetDisasterValue(tornado, p, value); used += settings[p].id + "=" + value + " ";
                }
                Line("TORNADO settings: " + used);
                string status = demo.LaunchDisaster(tornado);
                Line("TORNADO launched: " + status);

                Begin(); yield return WaitReal(35); End("100 bots, maximum tornado, 35 s");
                Check(fewestAlive >= 0 && fewestAlive < Bots * .7f, "The tornado really did kill or throw off a crowd (fewest alive " + fewestAlive + ")");
                Check(overHalfSecond == 0, "No frame took over half a second (" + overHalfSecond + " did; worst " + (worst * 1000).ToString("F0") + " ms, calm worst " + (calmWorst * 1000).ToString("F0") + " ms)");
                Check(worst < 1, "No frame took a full second");

                Begin(); yield return WaitReal(10); End("after the tornado, 10 s");
            }
            finally { QualitySettings.vSyncCount = vsync; Application.targetFrameRate = rate; measuring = false; crowd = null; }
        }
    }
}
