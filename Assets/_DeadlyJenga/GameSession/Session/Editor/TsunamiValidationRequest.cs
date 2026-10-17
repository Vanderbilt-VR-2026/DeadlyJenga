using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.GameSession.Editor
{
    // Explicit request-file bridge for checks when the editor already owns the project.
    public static class TsunamiValidationRequest
    {
        [InitializeOnLoadMethod]
        static void Schedule()
        {
            if (File.Exists("Logs/Validation/TsunamiChecks.request")) EditorApplication.delayCall += RunRequested;
        }
        static void RunRequested()
        {
            const string request = "Logs/Validation/TsunamiChecks.request";
            if (!File.Exists(request)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += RunRequested; return; }
            File.Delete(request);
            var messages = new List<string>(); bool failed = false;
            Application.LogCallback capture = (message, trace, type) =>
            { messages.Add(message); if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true; };
            var previous = SceneManager.GetActiveScene(); Scene temporary = default;
            var oldSession = DeadlyJenga.Core.Services.Session;
            Application.logMessageReceived += capture;
            try
            {
                if (EditorApplication.isPlaying) throw new InvalidOperationException("Checks require Edit Mode; the running game was left untouched.");
                temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(temporary); DeadlyJenga.Core.Services.Session = null;
                foreach (var menu in new[] { "Test Disaster Map Contract", "Test Tsunami Forces", "Capture Tsunami Preview" })
                    if (!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/" + menu)) throw new Exception("Missing check: " + menu);
            }
            catch (Exception error) { failed = true; messages.Add(error.ToString()); }
            finally
            {
                Application.logMessageReceived -= capture;
                if (temporary.IsValid()) EditorSceneManager.CloseScene(temporary, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                DeadlyJenga.Core.Services.Session = oldSession;
                File.WriteAllLines("Logs/Validation/TsunamiChecks.txt", new[] { failed ? "FAILED" : "PASSED" });
                File.AppendAllLines("Logs/Validation/TsunamiChecks.txt", messages);
            }
        }
    }
}


