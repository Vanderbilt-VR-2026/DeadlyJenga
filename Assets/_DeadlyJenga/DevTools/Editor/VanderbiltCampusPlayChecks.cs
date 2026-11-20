using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Batch Play Mode check of the campus in the real Game.unity (pattern: BreakwaterVillagePlayChecks).
    // Run():     map loads, spawn is safe, a test survivor walks the Alumni Lawn -> Peabody route and climbs a building.
    // RunGray(): same map with the Presentation "Visual" child destroyed: spawn plus a 20 m walk (plays as gray box).
    // Reports: Logs/Validation/VanderbiltCampus-play.txt (and VanderbiltCampus-play-gray.txt).
    [InitializeOnLoad]
    public static partial class VanderbiltCampusPlayChecks
    {
        const string Key = "VanderbiltCampusPlayChecks";
        [Serializable] sealed class SavedScenes { public SceneSetup[] values; }
        static int errors;
        static bool Gray => SessionState.GetBool(Key + "Gray", false);
        static string Report => ProjectPath.Report(Gray ? "VanderbiltCampus-play-gray.txt" : "VanderbiltCampus-play.txt");
        static string ShotPrefix => Gray ? "VanderbiltCampus-play-gray-" : "VanderbiltCampus-play-";

        static VanderbiltCampusPlayChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    errors = 0; Application.logMessageReceived += Log;
                    ChooseMatch();
                    EditorApplication.delayCall += () => Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived -= Log; SessionState.SetBool(Key, false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key + "Choices", "")));
                    MatchSetup.FromLobby = SessionState.GetBool(Key + "Lobby", false); MatchSetup.StartLocalSession = SessionState.GetBool(Key + "Local", false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key + "Scenes", "")).values);
                    VanderbiltCampusData.Exit(SessionState.GetBool(Key + "Failed", true) ? 1 : 0);
                }
            };
        }

        [MenuItem("Tools/Deadly Jenga/Validation/Play Check Vanderbilt Campus")]
        public static void Run() => Start(false);

        [MenuItem("Tools/Deadly Jenga/Validation/Play Check Vanderbilt Campus (gray, no Visual)")]
        public static void RunGray() => Start(true);

        static void Start(bool gray)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            if (scenes.Any(s => s.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("Save edited scenes before the campus play check.");
            SessionState.SetBool(Key + "Gray", gray);
            SessionState.SetString(Key + "Scenes", JsonUtility.ToJson(new SavedScenes { values = scenes }));
            SessionState.SetString(Key + "Choices", JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key + "Lobby", MatchSetup.FromLobby); SessionState.SetBool(Key + "Local", MatchSetup.StartLocalSession);
            MatchSetup.FromLobby = false; MatchSetup.StartLocalSession = false; ChooseMatch();
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Failed", true);
            File.WriteAllText(Report, "Campus play check" + (gray ? " (gray: Visual removed)" : "") + ": actual Game.unity, demo mode, 0 bots, 0 hands. " +
                "Desktop editor only; no headset or two-player claim.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity")); EditorApplication.EnterPlaymode();
        }

        static void ChooseMatch()
        {
            MatchSetup.MapId = VanderbiltCampusBuilder.MapId; MatchSetup.ModeId = "demo";
            MatchSetup.BotTarget = 0; MatchSetup.HandCount = 0;
        }

        static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            { errors++; File.AppendAllText(Report, "ERROR " + message + "\n"); }
        }

        // Runs Check() and turns any exception into a FAIL line, then leaves Play Mode.
        static IEnumerator Guard()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Check()); bool failed = false;
            while (stack.Count > 0)
            {
                bool next = false; object current = null; Exception failure = null;
                try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; } catch (Exception e) { failure = e; }
                if (failure != null) { File.AppendAllText(Report, "FAIL " + failure.Message + "\n"); failed = true; break; }
                if (!next) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            if (staging) Object.Destroy(staging);
            File.AppendAllText(Report, "Runtime errors=" + errors + "\n" + (failed || errors > 0 ? "RESULT FAIL" : "RESULT PASS") + "\n");
            SessionState.SetBool(Key + "Failed", failed || errors > 0);
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator Check()
        {
            var session = Object.FindFirstObjectByType<CastlePlaySession>(); float deadline = Time.realtimeSinceStartup + 35;
            while ((session.Player == null || session.SurvivorBots == null) && Time.realtimeSinceStartup < deadline) yield return null;
            Require(session.Player != null, "Solo survivor initialized");
            session.SurvivorBots.Clear(); session.HandBots?.Clear(); if (session.Social != null) session.Social.VoiceEnabled = false;
            if (!(Services.World is VanderbiltCampusMap)) { ChooseMatch(); MatchSetup.ApplyMap(); yield return null; }
            var map = Services.World as VanderbiltCampusMap;
            Require(map, "Catalog entry " + VanderbiltCampusBuilder.MapId + " loads a VanderbiltCampusMap in Game");
            Require(MatchSetup.SelectedMap?.title == VanderbiltCampusBuilder.Title, "Lobby title is '" + MatchSetup.SelectedMap?.title + "'");
            Require(map.Blocks.Count == 0, "Static map reports no blocks");
            var data = VanderbiltCampusData.Load();
            foreach (var p in data.problems) File.AppendAllText(Report, "NOTE " + p + "\n");

            if (Gray)
            {
                var visual = map.transform.Find("Visual");
                File.AppendAllText(Report, "NOTE Visual child " + (visual ? "found and destroyed" : "was not present (already gray)") + "\n");
                if (visual) Object.Destroy(visual.gameObject);
                yield return null;
                Require(!map.transform.Find("Visual"), "Presentation Visual is gone");
            }

            // Spawn: the real survivor stands safely for 3 seconds.
            var spawn = map.SafeSpawnPoint();
            session.Player.Place(spawn, Quaternion.identity);
            yield return new WaitForSeconds(3);
            var body = session.Player.transform.position;
            Require(body.y > map.WaterHeight(body) + 1, $"Survivor stands above water after 3 s (y={body.y:F2}, water={map.WaterHeight(body):F2})");
            Require(Flat(body - spawn).magnitude < 2 && Mathf.Abs(body.y - spawn.y) < 1.5f, "Survivor stays at the spawn point (moved " + (body - spawn).magnitude.ToString("F2") + " m)");
            Shot("spawn", session.Player.transform.position, session.Player.transform.forward);

            var route = Route(map, data);
            Require(route.Count >= 2, route.Count + " route waypoints (unity_map.json route or Route_nn markers)");
            MakeTestSurvivor(session);
            if (Gray)
            {
                yield return Walk(map, route, 20);
                Require(walked >= 20, $"Gray map: test survivor walked {walked:F1} m of the route (needs 20 m)");
            }
            else
            {
                yield return Walk(map, route, float.PositiveInfinity);
                Require(reached == route.Count - 1, $"Walked the whole route: reached waypoint {reached + 1} of {route.Count} ({walked:F0} m)" + (failedAt.Length > 0 ? "; " + failedAt : ""));
                yield return Climb(map, data);
                CheckShelter(map, data);
            }
            Require(errors == 0, "No runtime errors");
            File.AppendAllText(Report, "COMPLETE desktop Game scene checks. Quest headset and two-player multiplayer remain unverified.\n");
        }

        static void CheckShelter(VanderbiltCampusMap map, VanderbiltCampusData data)
        {
            if (data.sheltered.Count == 0) { File.AppendAllText(Report, "NOTE No 'sheltered' points in unity_map.json; IsSheltered not checked.\n"); return; }
            foreach (var p in data.sheltered)
                Require(map.IsSheltered(map.transform.TransformPoint(p)), "IsSheltered is true under the roof at " + p.ToString("F1"));
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        static void Require(bool pass, string message)
        {
            File.AppendAllText(Report, (pass ? "PASS " : "FAIL ") + message + "\n");
            if (!pass) throw new InvalidOperationException(message);
        }

        // A third-person photo from behind and above a point, saved to Logs/Validation.
        static void Shot(string name, Vector3 target, Vector3 forward)
        {
            var go = new GameObject("Campus proof camera"); var camera = go.AddComponent<Camera>();
            try
            {
                camera.enabled = false; camera.fieldOfView = 70; camera.nearClipPlane = .05f; camera.farClipPlane = 700;
                var back = Flat(forward).sqrMagnitude > .01f ? Flat(forward).normalized : Vector3.forward;
                camera.transform.position = target - back * 6 + Vector3.up * 3.5f;
                camera.transform.LookAt(target + Vector3.up * 1.2f);
                VanderbiltCampusChecks.Capture(camera, 1400, 900, ShotPrefix + name + ".png");
                File.AppendAllText(Report, "PHOTO Logs/Validation/" + ShotPrefix + name + ".png\n");
            }
            finally { Object.Destroy(go); }
        }
    }
}
