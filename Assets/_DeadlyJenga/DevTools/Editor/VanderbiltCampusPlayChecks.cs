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
    // A failed check is written once and the run carries on, so one run reports every section.
    // Only missing prerequisites (no player, no campus map) stop it early.
    // Reports: Logs/Validation/VanderbiltCampus-play.txt (and VanderbiltCampus-play-gray.txt).
    [InitializeOnLoad]
    public static partial class VanderbiltCampusPlayChecks
    {
        const string Key = "VanderbiltCampusPlayChecks";
        const double WatchdogSeconds = 8 * 60;
        [Serializable] sealed class SavedScenes { public SceneSetup[] values; }
        static int errors, physxErrors, failures;
        static readonly List<string> physxMeshes = new List<string>();
        static string step = "starting";
        static double enteredAt;
        static bool finished;
        static bool Gray => SessionState.GetBool(Key + "Gray", false);
        static string Report => ProjectPath.Report(Gray ? "VanderbiltCampus-play-gray.txt" : "VanderbiltCampus-play.txt");
        static string ShotPrefix => Gray ? "VanderbiltCampus-play-gray-" : "VanderbiltCampus-play-";

        static VanderbiltCampusPlayChecks()
        {
            EditorApplication.update += Watchdog;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    errors = physxErrors = failures = 0; physxMeshes.Clear(); finished = false; step = "waiting for the session";
                    enteredAt = EditorApplication.timeSinceStartup;
                    Application.logMessageReceived += Log;
                    ChooseMatch();
                    EditorApplication.delayCall += () => Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());
                }
                if (state == PlayModeStateChange.EnteredEditMode) BackInEditMode();
            };
        }

        // Always ends with Exit(code), even if restoring the editor state fails.
        static void BackInEditMode()
        {
            try
            {
                Application.logMessageReceived -= Log; SessionState.SetBool(Key, false);
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key + "Choices", "")));
                MatchSetup.FromLobby = SessionState.GetBool(Key + "Lobby", false); MatchSetup.StartLocalSession = SessionState.GetBool(Key + "Local", false);
                // A fresh batch editor starts with no saved scene; only restore a setup that has a loaded active scene.
                var saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key + "Scenes", "{}"))?.values;
                if (saved != null && saved.Length > 0 && saved.Any(s => s.isActive && s.isLoaded) && saved.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(saved);
            }
            catch (Exception e) { Debug.LogWarning("[VanderbiltCampus] play check clean-up: " + e.Message); }
            finally { VanderbiltCampusData.Exit(SessionState.GetBool(Key + "Failed", true) ? 1 : 0); }
        }

        // Stops a run that hangs: after 8 minutes in Play Mode it writes the current step and leaves Play Mode.
        static void Watchdog()
        {
            if (finished || !SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup - enteredAt < WatchdogSeconds) return;
            finished = true;
            File.AppendAllText(Report, "FAIL watchdog timeout after 8 minutes during: " + step + "\nRESULT FAIL\n");
            SessionState.SetBool(Key + "Failed", true);
            EditorApplication.ExitPlaymode();
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

        // PhysX convex-hull errors (degenerate collider meshes) are counted and summarized once at the end.
        static void Log(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert && type != LogType.Warning) return;
            bool physx = message.Contains("[Physics.PhysX]") || message.Contains("Convex Mesh") || message.Contains("ConvexMesh");
            if (physx)
            {
                physxErrors++;
                int at = message.IndexOf("Source mesh name: ", StringComparison.Ordinal);
                if (at >= 0 && physxMeshes.Count < 10) physxMeshes.Add(message.Substring(at + 18).Trim());
                return;
            }
            if (type == LogType.Warning) return;
            errors++; File.AppendAllText(Report, "ERROR " + message + "\n");
        }

        // Runs Check(); an exception (a missing prerequisite or a bug) becomes one FAIL line, then Play Mode ends.
        static IEnumerator Guard()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Check()); bool crashed = false;
            while (stack.Count > 0 && !finished)
            {
                bool next = false; object current = null; Exception failure = null;
                try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; } catch (Exception e) { failure = e; }
                if (failure != null)
                {
                    if (!(failure is StopCheck)) File.AppendAllText(Report, "FAIL during " + step + ": " + failure.Message + "\n");
                    crashed = true; break;
                }
                if (!next) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            if (finished) yield break;   // the watchdog already ended the run
            finished = true;
            if (staging) Object.Destroy(staging);
            if (physxErrors > 0)
            {
                failures++;
                File.AppendAllText(Report, $"FAIL {physxErrors} PhysX convex-hull messages (degenerate collider meshes), e.g. {string.Join(", ", physxMeshes)}\n");
            }
            bool failed = crashed || failures > 0 || errors > 0;
            File.AppendAllText(Report, $"Runtime errors={errors} (PhysX convex={physxErrors}) failed checks={failures}\n" + (failed ? "RESULT FAIL" : "RESULT PASS") + "\n");
            SessionState.SetBool(Key + "Failed", failed);
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator Check()
        {
            step = "session start";
            var session = Object.FindFirstObjectByType<CastlePlaySession>(); float deadline = Time.realtimeSinceStartup + 35;
            while ((session.Player == null || session.SurvivorBots == null) && Time.realtimeSinceStartup < deadline) yield return null;
            Must(session.Player != null, "Solo survivor initialized");
            session.SurvivorBots.Clear(); session.HandBots?.Clear(); if (session.Social != null) session.Social.VoiceEnabled = false;
            step = "loading the campus map";
            if (!(Services.World is VanderbiltCampusMap)) { ChooseMatch(); MatchSetup.ApplyMap(); yield return null; }
            var map = Services.World as VanderbiltCampusMap;
            Must(map, "Catalog entry " + VanderbiltCampusBuilder.MapId + " loads a VanderbiltCampusMap in Game");
            Expect(MatchSetup.SelectedMap?.title == VanderbiltCampusBuilder.Title, "Lobby title is '" + MatchSetup.SelectedMap?.title + "'");
            Expect(map.Blocks.Count == 0, "Static map reports no blocks");
            var data = VanderbiltCampusData.Load();
            foreach (var p in data.problems) File.AppendAllText(Report, "NOTE " + p + "\n");

            if (Gray)
            {
                step = "removing Visual";
                var visual = map.transform.Find("Visual");
                File.AppendAllText(Report, "NOTE Visual child " + (visual ? "found and destroyed" : "was not present (already gray)") + "\n");
                if (visual) Object.Destroy(visual.gameObject);
                yield return null;
                Expect(!map.transform.Find("Visual"), "Presentation Visual is gone");
            }

            // Spawn: one chosen point (SafeSpawnPoint is random per call), the real survivor stands there for 3 seconds.
            step = "spawn";
            var spawn = map.SafeSpawnPoint();
            session.Player.Place(spawn, Quaternion.identity);
            File.AppendAllText(Report, "NOTE spawn point " + spawn.ToString("F2") + "\n");
            yield return new WaitForSeconds(3);
            var body = session.Player.transform.position;
            Expect(body.y > map.WaterHeight(body) + 1, $"Survivor stands above water after 3 s (y={body.y:F2}, water={map.WaterHeight(body):F2})");
            Expect(Flat(body - spawn).magnitude < 2 && Mathf.Abs(body.y - spawn.y) < 1.5f,
                $"Survivor stays at the chosen spawn {spawn:F1} (now {body:F1}, moved {(body - spawn).magnitude:F2} m)");
            Shot("spawn", body, session.Player.transform.forward);

            step = "route";
            var route = Route(map, data);
            if (Expect(route.Count >= 2, route.Count + " route waypoints (unity_map.json route or Route_nn markers)"))
            {
                MakeTestSurvivor(session);
                if (Gray)
                {
                    yield return Walk(map, route, 20);
                    Expect(walked >= 20, $"Gray map: test survivor walked {walked:F1} m of the route (needs 20 m)");
                }
                else
                {
                    yield return Walk(map, route, float.PositiveInfinity);
                    Expect(reached == route.Count - 1, $"Walked the whole route: reached waypoint {reached + 1} of {route.Count} ({walked:F0} m)" + (failedAt.Length > 0 ? "; " + failedAt : ""));
                    step = "climb";
                    yield return Climb(map, data);
                    step = "shelter";
                    CheckShelter(map, data);
                }
            }
            step = "finishing";
            Expect(errors == 0, "No runtime errors (other than PhysX convex messages, summarized below)");
            File.AppendAllText(Report, "COMPLETE desktop Game scene checks. Quest headset and two-player multiplayer remain unverified.\n");
        }

        static void CheckShelter(VanderbiltCampusMap map, VanderbiltCampusData data)
        {
            if (data.sheltered.Count == 0) { File.AppendAllText(Report, "NOTE No 'sheltered' points in unity_map.json; IsSheltered not checked.\n"); return; }
            foreach (var p in data.sheltered)
                Expect(map.IsSheltered(map.transform.TransformPoint(p)), "IsSheltered is true under the roof at " + p.ToString("F1"));
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        // A check that may fail without stopping the run. Written once; returns the result.
        static bool Expect(bool pass, string message)
        {
            File.AppendAllText(Report, (pass ? "PASS " : "FAIL ") + message + "\n");
            if (!pass) failures++;
            return pass;
        }

        sealed class StopCheck : Exception { public StopCheck(string m) : base(m) { } }

        // A prerequisite: when it fails nothing after it can run, so the run stops (the FAIL is still written once).
        static void Must(bool pass, string message)
        {
            if (!Expect(pass, message)) throw new StopCheck(message);
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
