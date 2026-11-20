using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // One coordinator job for every Stage 5 campus check: the edit-mode checks (structure + viewpoint budget +
    // renders, per-tile budget, routes, looks) in order, then the Play Mode suite (bots + both modes) in one
    // Play Mode session. It exits once, with 1 if any part failed. Summary: Logs/Validation/VanderbiltCampus-stage5.txt
    public static class VanderbiltCampusStage5Checks
    {
        static string Summary => ProjectPath.Report("VanderbiltCampus-stage5.txt");

        [MenuItem("Tools/Deadly Jenga/Validation/Vanderbilt Campus Stage 5 (all checks)")]
        public static void RunAllMenu() => RunAllBatch();

        public static void RunAllBatch()
        {
            var lines = new StringBuilder("Campus Stage 5 checks " + DateTime.Now.ToString("u") + "\n");
            bool editFailed = false;
            void Step(string name, Func<bool> run, string report)
            {
                bool ok;
                try { ok = run(); } catch (Exception e) { ok = false; lines.AppendLine("  " + name + " threw " + e.Message); }
                if (!ok) editFailed = true;
                lines.AppendLine((ok ? "PASS " : "FAIL ") + name + " -> Logs/Validation/" + report);
                File.WriteAllText(Summary, lines.ToString());
            }
            Step("edit-mode structure, viewpoint budget, renders", VanderbiltCampusChecks.Run, "VanderbiltCampusChecks.txt");
            Step("per-tile budget", VanderbiltCampusChecks.RunTileBudget, "VanderbiltCampusTileBudget.txt");
            Step("routes and clearance", VanderbiltCampusChecks.RunRoutes, "VanderbiltCampusRoutes.txt");
            Step("looks (candy and plain)", VanderbiltCampusChecks.RunLooks, "VanderbiltCampusLooks.txt");
            lines.AppendLine("Play Mode: bots + King of the Hill + Capture the Flag -> Logs/Validation/VanderbiltCampus-stage5-play.txt (its RESULT line decides the rest of the exit code)");
            File.WriteAllText(Summary, lines.ToString());
            try { VanderbiltCampusStage5PlayChecks.Start("all", editFailed); }
            catch (Exception e)
            {
                File.AppendAllText(Summary, "FAIL could not start Play Mode: " + e.Message + "\n");
                VanderbiltCampusData.Exit(1);
            }
        }
    }
}
