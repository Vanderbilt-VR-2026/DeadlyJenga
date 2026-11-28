using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Play Mode checks for the Shrine City physics preview (mode-shrine-city).
    // Entry: DeadlyJenga.DevTools.Editor.ShrineCityChecks.Run. Every PASS/FAIL line goes to unity.log and to
    // Logs/Validation/ShrineCity.txt; pictures go next to the report.
    [InitializeOnLoad]
    public static class ShrineCityChecks
    {
        const string Key = "ShrineCityChecks";
        static readonly string[] Targets = { "Worship Hall", "West Pagoda", "East Bell Tower", "Small Pagoda", "Small Bell Tower",
                                             "Twin Towers", "Market Square", "Training Grounds" };
        static int errors; static bool failed;
        static string Report => ProjectPath.Report("ShrineCity.txt");

        static ShrineCityChecks() { EditorApplication.playModeStateChanged += State; }

        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Shrine City physics preview in Game.unity\n");
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                errors = 0; failed = false; Application.logMessageReceived += Log;
                var go = new GameObject("Shrine City check driver"); Object.DontDestroyOnLoad(go);
                go.AddComponent<ShrineCityChecksDriver>().StartCoroutine(Guard());
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false); Application.logMessageReceived -= Log;
                int exitCode = errors == 0 && !failed ? 0 : 2;
                if (SessionState.GetString("DJWarm.id", "") != "")
                {
                    Directory.CreateDirectory("Logs/Warm");
                    File.WriteAllText("Logs/Warm/exit.txt", exitCode.ToString());
                }
                else EditorApplication.Exit(exitCode);
            }
        }

        static void Log(string value, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (value.StartsWith("FAIL ")) return;
            errors++; File.AppendAllText(Report, "ERROR " + value + "\n" + stack + "\n");
        }

        static IEnumerator Guard()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Checks());
            while (stack.Count > 0)
            {
                bool next = false; object value = null;
                try { next = stack.Peek().MoveNext(); if (next) value = stack.Peek().Current; }
                catch (Exception e) { failed = true; Line("FAIL " + e.Message); File.AppendAllText(Report, e + "\n"); break; }
                if (!next) { stack.Pop(); continue; }
                if (value is IEnumerator child) { stack.Push(child); continue; }
                yield return value;
            }
            Line($"COMPLETE failed={failed} runtimeErrors={errors}");
            EditorApplication.ExitPlaymode();
        }

        static void Line(string text)
        {
            File.AppendAllText(Report, text + "\n");
            if (text.StartsWith("FAIL")) Debug.LogError(text); else Debug.Log(text);
        }
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Line("PASS " + message);
        }
        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator LoadMap()
        {
            MatchSetup.MapId = "mode-shrine-city";
            MatchSetup.ModeId = "demo";
            MatchSetup.ApplyMap();
            yield return Wait(1);
        }
        static CastleGenerator Map() => Object.FindObjectsByType<CastleGenerator>(FindObjectsSortMode.None)
            .FirstOrDefault(g => g.settings.layoutPreset == ShrineCityLayout.Preset && g.Bodies.Count > 0);

        static IEnumerator Checks()
        {
            Application.runInBackground = true; AudioListener.volume = 0;
            float until = Time.realtimeSinceStartup + 45; CastlePlaySession session;
            while (!(session = Object.FindFirstObjectByType<CastlePlaySession>()) && Time.realtimeSinceStartup < until) yield return null;
            Check(session, "Game.unity solo session exists");
            yield return Wait(4);
            if (Services.Session?.SurvivorBots != null) { Services.Session.SurvivorBots.AutoFill = false; Services.Session.SurvivorBots.Clear(); }

            // 1. Build
            yield return LoadMap();
            var map = Map();
            Check(map, "Shrine City map loads from the lobby catalog (mode-shrine-city)");
            Check(map.Bodies.Count == ShrineCityLayout.BodyCount, $"All {ShrineCityLayout.BodyCount} authored bodies spawned");
            int joints = map.GetComponentsInChildren<FixedJoint>().Length;
            Check(joints > 1000, $"Timber joints built ({joints})");

            // 2. Stand untouched
            var start = map.Bodies.Select(b => b.position).ToArray();
            Capture(map, "ShrineCity-Stand-Start.png");
            yield return Wait(8);
            var moved = map.Bodies.Select((b, i) => b ? Vector3.Distance(b.position, start[i]) : 999f).ToArray();
            int fell = moved.Count(m => m > 1), shifted = moved.Count(m => m > .1f);
            int worst = Array.IndexOf(moved, moved.Max());
            Line($"STAND bodies={moved.Length} moved>0.1m={shifted} fell>1m={fell} worst={moved[worst]:F2}m ({ShrineCityLayout.Building(worst)} / {ShrineCityLayout.BodyId(worst)})");
            Capture(map, "ShrineCity-Stand-8s.png");
            Check(fell == 0, "The town stands untouched for 8 s (no body falls more than 1 m)");

            // 3. Collapse: knock out each target building's ground floor (upright bodies taller than 1 m near its base)
            var bounds = map.Bodies.Select(b => BodyBounds(b)).ToArray();
            var baseOf = new Dictionary<string, float>();
            for (int i = 0; i < bounds.Length; i++)
            {
                string b = ShrineCityLayout.Building(i);
                if (!baseOf.TryGetValue(b, out var low) || bounds[i].min.y < low) baseOf[b] = bounds[i].min.y;
            }
            var removed = new HashSet<int>();
            for (int i = 0; i < bounds.Length; i++)
            {
                string b = ShrineCityLayout.Building(i);
                if (Targets.Contains(b) && bounds[i].min.y < baseOf[b] + 2.5f && bounds[i].size.y > 1) removed.Add(i);
            }
            var before = map.Bodies.Select(b => b.position).ToArray();
            int jointsBefore = map.GetComponentsInChildren<FixedJoint>().Length;
            foreach (int i in removed) map.Bodies[i].gameObject.SetActive(false);
            foreach (var b in map.Bodies) if (b && b.gameObject.activeInHierarchy) b.WakeUp();
            yield return Wait(6);
            int collapsed = 0;
            for (int i = 0; i < map.Bodies.Count; i++)
                if (!removed.Contains(i) && map.Bodies[i] && Vector3.Distance(map.Bodies[i].position, before[i]) > 1) collapsed++;
            int broken = jointsBefore - map.GetComponentsInChildren<FixedJoint>().Length;
            Line($"COLLAPSE removed={removed.Count} fell>1m={collapsed} jointsBroken={broken}");
            Capture(map, "ShrineCity-Collapse-6s.png");
            Check(collapsed >= 50 && broken >= 30, "Knocking out the ground floors collapses the buildings (50+ bodies fall, 30+ joints snap)");

            // 4. Spawn on a fresh town, then drown and respawn
            yield return LoadMap();
            yield return Wait(3);
            map = Map();
            var player = session.Player;
            float water = Services.World.WaterHeight(map.transform.position);
            var spawn = session.Modes.SpawnPoint(-1, 0, "survivor", session.Modes.LocalActorId);
            player.Place(spawn, Quaternion.identity);
            yield return Wait(2);
            Check(NetworkActors.Alive(player) && player.transform.position.y > water + .2f,
                $"Survivor spawns alive and dry at the match spawn ({spawn})");
            bool died = false;
            void OnDeath(GameObject who, GameObject source) { if (who == player.gameObject) died = true; }
            GameEvents.Eliminated += OnDeath;
            try
            {
                player.Place(map.transform.TransformPoint(new Vector3(0, -1.2f, -150)), Quaternion.identity);
                for (float end = Time.time + 6; Time.time < end && !died;) yield return null;
                Check(died, "Falling into the open lake eliminates the survivor");
                for (float end = Time.time + 15; Time.time < end && !NetworkActors.Alive(session.Player);) yield return null;
                yield return Wait(1);
                var back = session.Player;
                Check(NetworkActors.Alive(back) && back.transform.position.y > water + .2f, "Survivor respawns alive and dry");
            }
            finally { GameEvents.Eliminated -= OnDeath; }

            // 5. Transfer: the layout round-trips through the multiplayer save and rebuild
            string saved = map.SaveLayout();
            bool loaded = map.LoadLayout(saved, map.Bodies.Count, new Pose(map.transform.position, map.transform.rotation), map.transform.lossyScale);
            yield return Wait(1);
            Check(loaded && map.Bodies.Count == ShrineCityLayout.BodyCount, "Layout transfer: SaveLayout/LoadLayout rebuilds the same bodies");
        }

        static Bounds BodyBounds(Rigidbody body)
        {
            var cs = body.GetComponentsInChildren<Collider>().Where(c => c.enabled && c.gameObject.layer == body.gameObject.layer).ToArray();
            if (cs.Length == 0) return new Bounds(body.position, Vector3.zero);
            var b = cs[0].bounds; foreach (var c in cs) b.Encapsulate(c.bounds); return b;
        }

        static void Capture(CastleGenerator map, string file)
        {
            var go = new GameObject("Shrine City capture"); var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 50; cam.farClipPlane = 3000;
            var centre = map.transform.position + Vector3.up * 10;
            go.transform.position = centre + new Vector3(110, 120, -190);
            go.transform.LookAt(centre);
            var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply(); RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Report), file), tex.EncodeToPNG());
            cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(tex); Object.Destroy(go);
            Line("CAPTURE " + file);
        }
    }

    public sealed class ShrineCityChecksDriver : MonoBehaviour { }
}
