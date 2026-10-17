using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.GameSession.Editor
{
    public static class DemoDisasterChecks
    {
        static double started;
        static bool failed;

        [InitializeOnLoadMethod]
        static void ResumeCheck()
        {
            if (!SessionState.GetBool("DemoDisasterChecks.Pending", false)) return;
            started = EditorApplication.timeSinceStartup;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            var prefabs = DemoDisasters.Prefabs;
            if (prefabs.Length == 0) throw new Exception("DisasterCatalog is empty or missing.");
            var names = new HashSet<string>();
            foreach (var prefab in prefabs)
            {
                if (!prefab) throw new Exception("DisasterCatalog contains a missing prefab.");
                var hazard = prefab.GetComponent<IMapHazard>();
                if (hazard == null || hazard.Type == null || !names.Add(hazard.Type.name))
                    throw new Exception("Invalid disaster prefab: " + prefab.name);
            }
            var scenes = AssetDatabase.FindAssets("Game t:Scene");
            string path = Array.Find(Array.ConvertAll(scenes, AssetDatabase.GUIDToAssetPath),
                item => System.IO.Path.GetFileNameWithoutExtension(item) == "Game");
            if (path == null) throw new Exception("Game scene not found.");
            EditorSceneManager.OpenScene(path);
            SessionState.SetBool("DemoDisasterChecks.Pending", true);
            SessionState.SetBool("DemoDisasterChecks.Failed", false);
            Application.logMessageReceived += OnLog;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                failed = true;
                SessionState.SetBool("DemoDisasterChecks.Failed", true);
            }
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 120) { Finish(false); return; }
            if (!EditorApplication.isPlaying || Time.timeSinceLevelLoad < 5) return;
            int began = 0, ended = 0;
            Action<HazardType, Vector3> onBegin = (_, __) => began++;
            Action<HazardType, Vector3> onEnd = (_, __) => ended++;
            GameEvents.HazardStarted += onBegin;
            GameEvents.HazardEnded += onEnd;
            try
            {
                var session = UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();
                if (!session) throw new Exception("Game has no CastlePlaySession.");
                foreach (var prefab in DemoDisasters.Prefabs)
                    if (!DemoDisasters.Execute(session, prefab.name)) throw new Exception("Trigger failed: " + prefab.name);
                if (DemoDisasters.Execute(session, "Tsunami")) throw new Exception("Duplicate tsunami was accepted.");
                DemoDisasters.StopAll(session);
                if (began != DemoDisasters.Prefabs.Length || ended != DemoDisasters.Prefabs.Length)
                    throw new Exception("Expected one start/end pair per disaster after stopping.");
                if (DemoDisasters.Execute(session, "unknown-disaster")) throw new Exception("Unknown disaster accepted.");
                Debug.Log("Demo disaster checks: catalog prefabs and trigger/event pairs passed; Game entered Play Mode.");
            }
            catch (Exception error) { failed = true; Debug.LogException(error); }
            finally
            {
                GameEvents.HazardStarted -= onBegin;
                GameEvents.HazardEnded -= onEnd;
                Finish(!failed && !SessionState.GetBool("DemoDisasterChecks.Failed", false));
            }
        }

        static void Finish(bool success)
        {
            SessionState.SetBool("DemoDisasterChecks.Pending", false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
