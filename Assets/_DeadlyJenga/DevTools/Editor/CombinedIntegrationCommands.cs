using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class CombinedIntegrationCommands
    {
        static CombinedIntegrationCommands() { EditorApplication.update += Poll; }

        static void Poll()
        {
            string request = ProjectPath.Report("CombinedIntegration.request");
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            string owner = ProjectPath.Report("CombinedIntegration.owner");
            string project = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            if (!File.Exists(owner) || !string.Equals(project, Path.GetFullPath(File.ReadAllText(owner).Trim()), StringComparison.OrdinalIgnoreCase)) return;
            string[] command = File.ReadAllText(request).Trim().Split('|');
            if (command.Length != 2 || command[0] != System.Diagnostics.Process.GetCurrentProcess().Id.ToString()) return;
            File.Delete(request);
            try
            {
                switch (command[1])
                {
                    case "tower":
                        if (!ValidationRunGate.TryAcquire("MegablockTower")) throw new InvalidOperationException("Another validation is running.");
                        MegablockTowerChecks.Run();
                        break;
                    case "quit":
                        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                            throw new InvalidOperationException("Unsaved scenes left untouched.");
                        EditorApplication.Exit(0);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown integration command.");
                }
                File.AppendAllText(ProjectPath.Report("CombinedIntegrationCommands.txt"), DateTime.UtcNow.ToString("O") + " " + command[1] + " accepted\n");
            }
            catch (Exception error)
            {
                ValidationRunGate.Release("MegablockTower");
                File.AppendAllText(ProjectPath.Report("CombinedIntegrationCommands.txt"), "FAIL " + error + "\n");
            }
        }
    }
}
