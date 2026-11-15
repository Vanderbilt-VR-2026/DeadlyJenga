using System;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class UnderneathTreePlayChecks
    {
        const string Pending = "UnderneathTreePlayChecks.Pending";
        const string Failed = "UnderneathTreePlayChecks.Failed";
        const string MapId = "workshop-under-the-tree";
        static string Report => ProjectPath.Report("UnderneathTreePlay.txt");
        static double deadline;
        static double nextStep;
        static int phase;
        static UnderneathTreeMap map;
        static Rigidbody loosePanel;
        static Vector3 initialPanelPosition, initialTrainPosition;
        static int blockCount, revision;

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run this check in a checked batch editor.");
            if (!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))
                throw new InvalidOperationException("Disaster validation menu is unavailable.");
            File.WriteAllText(Report, "Underneath the Tree, clean solo Game.unity load and runtime checks\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Failed, true);
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SelectMap()
        {
            if (!SessionState.GetBool(Pending, false))
                return;
            MatchChoices.MapId = MapId;
            MatchChoices.ModeId = "demo";
            MatchChoices.BotTarget = 1;
            MatchChoices.HandCount = 0;
        }

        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Pending, false))
                    return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    SelectMap();
                    phase = 0;
                    deadline = EditorApplication.timeSinceStartup + 50;
                    EditorApplication.update += Check;
                }
                else if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.update -= Check;
                    SessionState.SetBool(Pending, false);
                    int result = SessionState.GetBool(Failed, true) ? 1 : 0;
                    EditorApplication.delayCall += () => EditorApplication.Exit(result);
                }
            };
        }

        static void Check()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now > deadline)
                    throw new InvalidOperationException("Runtime check timed out at step " + phase + ".");
                if (now < nextStep)
                    return;
                if (phase == 0)
                {
                    map = Services.World as UnderneathTreeMap;
                    var player = Services.Session?.Player;
                    if (!map || player == null || !player.ViewCamera || Time.frameCount < 10)
                        return;
                    blockCount = map.Blocks.Count;
                    revision = map.Revision;
                    Require(blockCount > 30 && map.Blocks.All(b => b && b.isKinematic),
                        blockCount + " intact, independently registered gift pieces");
                    Require(Physics.Raycast(map.SafeSpawnPoint() + Vector3.up * .2f, Vector3.down,
                        out var spawn, 1f, 1 << Layers.Surface) && spawn.collider.name == "Present 206 box",
                        "spawn is over the junction approach gift");
                    CheckJunction();
                    var train = map.GetComponentInChildren<UnderneathTreeTrain>();
                    Require(train, "tinplate train imported");
                    initialTrainPosition = train.transform.position;
                    Require(map.Root.GetComponentsInChildren<MeshRenderer>().Any(r =>
                        r.name.StartsWith("Skirt skull") && r.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0),
                        "skirt motifs are present");
                    loosePanel = map.Blocks.First(b => b.name == "Lid");
                    initialPanelPosition = loosePanel.position;
                    loosePanel.GetComponent<IPushable>().Push(Vector3.up * 5f, null);
                    Require(!loosePanel.isKinematic, "impact releases one real rigidbody");
                    nextStep = now + 1.0;
                    phase++;
                    return;
                }

                Require(Vector3.Distance(initialPanelPosition, loosePanel.position) > .1f,
                    "released panel is displaced by PhysX");
                Require(Vector3.Distance(initialTrainPosition, map.GetComponentInChildren<UnderneathTreeTrain>().transform.position) > .2f,
                    "train advances on the track during Play Mode");
                Services.Session.ResetMap();
                Require(map.Revision > revision && map.Blocks.Count == blockCount && map.Blocks.All(b => b.isKinematic),
                    "normal map reset restores the original intact pieces");
                CheckJunction();
                Finish(true);
            }
            catch (Exception error)
            {
                File.AppendAllText(Report, "FAIL " + error + "\n");
                Debug.LogException(error);
                Finish(false);
            }
        }

        static void CheckJunction()
        {
            var cream = map.Root.GetComponentsInChildren<BoxCollider>().Single(c => c.name == "Cream Landing");
            var green = map.Root.GetComponentsInChildren<BoxCollider>().Single(c => c.name == "Green Climb");
            var center = cream.bounds.center;
            center.y = cream.bounds.max.y;
            Require(Physics.Raycast(center + Vector3.up, Vector3.down, out var landing, 2f,
                1 << Layers.Surface) && landing.collider == cream, "cream gift catches a downward landing probe");
            Require(Physics.Raycast(center + Vector3.up * .55f, cream.transform.right,
                out var side, 5f, 1 << Layers.Surface) && side.collider == green &&
                Climbables.TryGet(green, out var climbable) && climbable.CanClimb && climbable.Grip > 0f,
                "green face is reachable and registered with normal climbing");
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }

        static void Finish(bool passed)
        {
            EditorApplication.update -= Check;
            SessionState.SetBool(Failed, !passed);
            File.AppendAllText(Report, passed ? "PASSED\n" : "FAILED\n");
            EditorApplication.ExitPlaymode();
        }
    }
}
