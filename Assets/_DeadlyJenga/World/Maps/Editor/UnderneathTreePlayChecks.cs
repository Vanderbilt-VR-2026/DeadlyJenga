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
        static float impactFixedTime;
        static int phase;
        static UnderneathTreeMap map;
        static Rigidbody loosePanel, looseDrum;
        static string[] originalPieces;
        static Vector3 initialPanelPosition, initialDrumPosition, initialTrainPosition;
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
                if (phase == 0)
                {
                    map = Services.World as UnderneathTreeMap;
                    var player = Services.Session?.Player;
                    if (!map || player == null || !player.ViewCamera || Time.frameCount < 10)
                        return;
                    blockCount = map.Blocks.Count;
                    revision = map.Revision;
                    var gifts = map.Blocks.GroupBy(body => body.transform.parent.name)
                        .ToDictionary(group => group.Key, group => group.ToArray());
                    originalPieces = PieceOrder();
                    Require(gifts.Count >= 30 && map.Blocks.All(b => b && b.isKinematic && b.useGravity),
                        gifts.Count + " intact shells and " + blockCount + " independently registered physical pieces");
                    Require(gifts.Values.All(parts => parts.Length == 6 || parts.Length == 10) &&
                        gifts.Values.All(parts => parts.Count(b => b.name == "Bottom") == 1 && parts.Count(b => b.name == "Lid") == 1),
                        "every rectangular and cylindrical present has a physical base, walls and lid");
                    Require(gifts.ContainsKey("Present 00") && gifts.ContainsKey("Present 206") &&
                        gifts.ContainsKey("Cream Landing") && gifts.ContainsKey("Green Climb"),
                        "original route supports and both junction gifts can break");
                    Require(Physics.Raycast(map.SafeSpawnPoint() + Vector3.up * .2f, Vector3.down,
                        out var spawn, 1f, 1 << Layers.Block) && spawn.collider.transform.parent.name == "Present 206",
                        "normal spawn is above the movable junction approach lid");
                    CheckJunction();
                    var train = map.GetComponentInChildren<UnderneathTreeTrain>();
                    Require(train, "tinplate train imported");
                    initialTrainPosition = train.transform.position;
                    Require(map.Root.GetComponentsInChildren<MeshRenderer>().Any(r =>
                        r.name.StartsWith("Skirt skull") && r.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0),
                        "skirt motifs are present");
                    var cream = gifts["Cream Landing"];
                    var drum = gifts["Present 01"];
                    loosePanel = cream.Single(b => b.name == "Lid");
                    looseDrum = drum.First(b => b.name.StartsWith("Curved wall"));
                    initialPanelPosition = loosePanel.position;
                    initialDrumPosition = looseDrum.position;
                    cream.Single(b => b.name == "Bottom").GetComponent<IPushable>().Push(Vector3.up * 4f, null);
                    looseDrum.GetComponent<IPushable>().Push(Vector3.up * 6.5f, null);
                    Require(cream.All(b => !b.isKinematic) && drum.All(b => !b.isKinematic),
                        "base removal breaks a rectangular gift and a heavy wall impact breaks a round gift");
                    impactFixedTime = Time.fixedTime;
                    phase++;
                    return;
                }

                if (Time.fixedTime - impactFixedTime < .75f)
                    return;
                Require(Vector3.Distance(initialPanelPosition, loosePanel.position) > .1f,
                    "released rectangular lid is displaced by PhysX");
                Require(Vector3.Distance(initialDrumPosition, looseDrum.position) > .1f,
                    "released curved wall is displaced by PhysX");
                Require(Vector3.Distance(initialTrainPosition, map.GetComponentInChildren<UnderneathTreeTrain>().transform.position) > .2f,
                    "train advances on the track during Play Mode");
                Services.Session.ResetMap();
                Require(map.Revision > revision && map.Blocks.Count == blockCount && map.Blocks.All(b => b.isKinematic),
                    "normal map reset restores the original intact pieces");
                Require(PieceOrder().SequenceEqual(originalPieces), "map reset preserves deterministic replica body order");
                var capturedLid = map.Blocks.Single(b => b.transform.parent.name == "Present 01" && b.name == "Lid");
                map.SetReplica(true);
                capturedLid.position += Vector3.up * .4f;
                map.SetReplica(false);
                Require(!capturedLid.isKinematic && map.Blocks.Count(b => !b.isKinematic) == 1,
                    "restoring authority keeps the replica piece displaced by the host loose");
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

        static string[] PieceOrder() => map.Blocks.Select(body => body.transform.parent.name + "/" + body.name).ToArray();

        static void CheckJunction()
        {
            var cream = map.Blocks.Single(b => b.transform.parent.name == "Cream Landing" && b.name == "Lid")
                .GetComponent<BoxCollider>();
            var green = map.Blocks.Single(b => b.transform.parent.name == "Green Climb" && b.name == "Left")
                .GetComponent<BoxCollider>();
            var center = cream.bounds.center;
            center.y = cream.bounds.max.y;
            Require(Physics.Raycast(center + Vector3.up, Vector3.down, out var landing, 2f,
                1 << Layers.Block) && landing.collider == cream, "cream gift catches a downward landing probe");
            Require(Physics.Raycast(center + Vector3.up * .55f, cream.transform.right,
                out var side, 5f, 1 << Layers.Block) && side.collider == green &&
                Climbables.TryGet(green, out var climbable) && climbable.CanClimb && climbable.Grip > 0f,
                "green shell has a reachable wall and registers normal climbing");
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
