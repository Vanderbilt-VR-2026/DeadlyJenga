using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    // Batch entry point. Runs existing regressions without saving authored scenes.
    [InitializeOnLoad]
    public static class CastleMergeCompatibility
    {
        const string Key = "CastleMergeCompatibilityPhase";
        const string Report = "ArtDirection/MergeCompatibility.txt";
        const string Castle = "Assets/Scenes/Game.unity";
        static CastleMergeCompatibility()
        {
            EditorApplication.update += () =>
            {
                if (SessionState.GetInt(Key, 0) != 0 && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "Deadline", float.MaxValue))
                    Fail(new TimeoutException("Unity regression phase timed out; inspect the batch log."));
            };
            EditorApplication.playModeStateChanged += state =>
            {
                int phase = SessionState.GetInt(Key, 0);
                if (phase == 0) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                    EditorApplication.delayCall += () => UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(RunPhase(phase));
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += () => CompletePhase(phase);
            };
        }
        public static void RunBatch()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Unity merge compatibility checks\n");
            try
            {
                var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                Require(enabled.Length == 1 && enabled[0] == Castle, "Castle remains the default build, as requested.");
                foreach (string path in new[] { "Assets/Scenes/Lobby.unity", "Assets/Scenes/Main.unity", Castle })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int missing = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                        .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    Require(missing == 0, path + " loads without missing scripts.");
                }
                Require(UnityEngine.Object.FindFirstObjectByType<TowerGenerator>() == null &&
                    UnityEngine.Object.FindFirstObjectByType<DevTools>() == null &&
                    UnityEngine.Object.FindFirstObjectByType<PullRules>() == null,
                    "Main's generator, scaling tools and pulling rules are absent from the castle scene.");
                CastleLanValidation.Run();
                File.AppendAllText(Report, File.ReadAllText("Temp/LanValidation/editor-results.txt"));
                SessionState.SetInt(Key, 1);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Fail(e); }
        }
        static IEnumerator RunPhase(int phase)
        {
            SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 240);
            SessionState.SetString(Key + "Started", DateTime.UtcNow.Ticks.ToString());
            yield return null;
            var session = UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();
            session.SetMenu(true);
            if (phase == 1)
            {
                yield return CastleReferenceValidation.Validate(session);
                yield return CastleTraversalValidation.Validate(session);
                File.AppendAllText(Report, "INFO Focus-dependent desktop input checks require an interactive Game view; not run in batch mode.\n");
                yield return CastleStaminaChecks.Check(); // Also runs climbing and exits Play Mode.
            }
            else
            {
                var type = phase == 2 ? typeof(CastleVRViewChecks) : typeof(CastleSlopeChecks);
                yield return (IEnumerator)type.GetMethod("Check", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            }
        }
        static void CompletePhase(int phase)
        {
            try
            {
                string[] paths = phase == 1 ? new[] { "Assets/CastlePrototype/ReferenceValidation.txt", "Assets/CastlePrototype/TraversalValidation.txt", "ArtDirection/StaminaChecks.txt", "ArtDirection/ClimbChecks.txt" }
                    : phase == 2 ? new[] { "ArtDirection/VRViewChecks.txt", "ArtDirection/SeamCameraChecks.txt", "ArtDirection/ClimbChecks.txt" }
                    : new[] { "ArtDirection/SlopeChecks.txt" };
                foreach (var path in paths)
                {
                    Require(File.GetLastWriteTimeUtc(path).Ticks >= long.Parse(SessionState.GetString(Key + "Started", "0")), path + " was regenerated in this run.");
                    var lines = File.ReadAllLines(path);
                    Require(lines.Any(l => l.StartsWith("PASS ")) && !lines.Any(l => l.StartsWith("FAIL ")), path + " passed.");
                    File.AppendAllText(Report, string.Join("\n", lines) + "\n");
                }
                if (phase == 3) { SessionState.SetInt(Key, 0); EditorApplication.Exit(0); return; }
                SessionState.SetInt(Key, phase + 1);
                EditorSceneManager.OpenScene(Castle, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Fail(e); }
        }
        static void Require(bool ok, string message)
        {
            File.AppendAllText(Report, (ok ? "PASS " : "FAIL ") + message + "\n");
            if (!ok) throw new InvalidOperationException(message);
        }
        static void Fail(Exception e)
        {
            File.AppendAllText(Report, "FAIL " + e + "\n");
            SessionState.SetInt(Key, 0); Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
}
