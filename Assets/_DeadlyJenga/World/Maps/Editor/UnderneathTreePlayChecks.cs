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
        static Rigidbody[] creamWalls;
        static Quaternion[] creamWallRotations;
        static Vector3 initialPanelPosition, initialDrumPosition, initialTrainPosition;
        static int blockCount, revision;
        static double renderReady;
        static string captureDirectory;
        static Rigidbody bridgeLid;
        static Rigidbody[] railBodies;
        static Vector3 initialBridgePosition;

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run this check in a checked batch editor.");
            if (!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))
                throw new InvalidOperationException("Disaster validation menu is unavailable.");
            File.WriteAllText(Report, "Underneath the Tree, clean solo Game.unity load and runtime checks\n");
            SessionState.SetString("UnderneathTree.CaptureDirectory", "UnderneathTree-" + Guid.NewGuid().ToString("N"));
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
                    renderReady = double.PositiveInfinity;
                    captureDirectory = SessionState.GetString("UnderneathTree.CaptureDirectory", "UnderneathTree");
                    deadline = EditorApplication.timeSinceStartup + 135;
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
                    if (double.IsPositiveInfinity(renderReady))
                        renderReady = now + 1.7;
                    if (now < renderReady)
                        return;
                    blockCount = map.Blocks.Count;
                    revision = map.Revision;
                    railBodies = map.Blocks.Where(body => body.name.StartsWith("Gift-supported rail ", StringComparison.Ordinal)).ToArray();
                    var gifts = map.Blocks.Except(railBodies).GroupBy(body => body.transform.parent.name)
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
                    CheckRoutes(gifts);
                    var train = map.GetComponentInChildren<UnderneathTreeTrain>();
                    Require(train, "tinplate train imported");
                    initialTrainPosition = train.transform.position;
                    Shot(player.ViewCamera, "christmas-room", new Vector3(44.175f, 81f, -158.15f),
                        new Vector3(-58f, 11f, -68f), 64f, true);
                    Shot(player.ViewCamera, "bridge-approach", new Vector3(64f, 27f, -109f),
                        new Vector3(16.0f, 5.8f, -70.5f), 55f, true);
                    Shot(player.ViewCamera, "bridge-tunnel", new Vector3(31.5f, 2.05f, -77.15f),
                        new Vector3(10f, 3.25f, -67.9f), 72f);
                    Require(map.Root.GetComponentsInChildren<MeshRenderer>().Any(r =>
                        r.name.StartsWith("Skirt skull") && r.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0),
                        "skirt motifs are present");
                    var cream = gifts["Cream Landing"];
                    var drum = gifts["Present 01"];
                    creamWalls = cream.Where(b => b.name != "Bottom" && b.name != "Lid").ToArray();
                    creamWallRotations = creamWalls.Select(b => b.rotation).ToArray();
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
                if (phase == 1)
                {
                    Require(Vector3.Distance(initialPanelPosition, loosePanel.position) > .1f,
                        "released rectangular lid is displaced by PhysX");
                    Require(creamWalls.Where((wall, i) => Quaternion.Angle(wall.rotation, creamWallRotations[i]) > 24f).Count() >= 2,
                        "at least two rectangular walls visibly tip as the shell falls apart");
                    Require(Vector3.Distance(initialDrumPosition, looseDrum.position) > .1f,
                        "released curved wall is displaced by PhysX");
                    Require(Vector3.Distance(initialTrainPosition, map.GetComponentInChildren<UnderneathTreeTrain>().transform.position) > .2f,
                        "train advances on the track during Play Mode");
                    bridgeLid = map.Blocks.Single(body => body.name == "Lid" && body.transform.parent.name == "Gift Bridge / Navy Span");
                    initialBridgePosition = bridgeLid.position;
                    map.Blocks.Single(body => body.name == "Bottom" && body.transform.parent.name == "Gift Bridge / Pine Bearing")
                        .GetComponent<IPushable>().Push(Vector3.left * 6f, null);
                    foreach (var bearing in map.Blocks.Where(body => body.name == "Bottom" &&
                        body.transform.parent.name.StartsWith("Rail Gift ", StringComparison.Ordinal)))
                        bearing.GetComponent<IPushable>().Push(Vector3.left * 6f, null);
                    phase = 2;
                    impactFixedTime = Time.fixedTime;
                    return;
                }
                Require(!bridgeLid.isKinematic && Vector3.Distance(initialBridgePosition, bridgeLid.position) > .2f,
                    "destroying a bearing gift releases and moves the bridge");
                Require(railBodies.Any(body => !body.isKinematic),
                    "losing gift support releases real rail sections");
                Shot(Services.Session.Player.ViewCamera, "fallen-bridge", new Vector3(64f, 27f, -109f),
                    new Vector3(16.0f, 5.8f, -70.5f), 55f);
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

        static void Shot(Camera camera, string id, Vector3 eye, Vector3 target, float fieldOfView, bool ultraHd = false)
        {
            int width = ultraHd ? 3840 : 1920;
            int height = ultraHd ? 2160 : 1080;
            var directory = ProjectPath.Report(captureDirectory);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, id + ".png");
            var priorTexture = camera.targetTexture;
            var priorActive = RenderTexture.active;
            var priorPosition = camera.transform.position;
            var priorRotation = camera.transform.rotation;
            var priorFieldOfView = camera.fieldOfView;
            var priorAspect = camera.aspect;
            var previousMSAA = camera.allowMSAA;
            var previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            var render = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.Default, RenderTextureReadWrite.Default, ultraHd ? 2 : 4);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
                camera.targetTexture = render;
                camera.allowMSAA = true;
                camera.aspect = (float)width / height;
                camera.fieldOfView = fieldOfView;
                // Keep cold shader variants from dropping dressings out of the first view.
                ShaderUtil.allowAsyncCompilation = false;
                if (id == "christmas-room")
                    camera.Render();
                camera.Render();
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path + ".tmp", image.EncodeToPNG());
                File.Move(path + ".tmp", path);
                using var sha = System.Security.Cryptography.SHA256.Create();
                var digest = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
                var receipt = new ShotReceipt
                {
                    view = id,
                    map = MatchChoices.MapId,
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    builder = map.GetType().FullName,
                    project = Directory.GetCurrentDirectory(),
                    pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                    frame = Time.frameCount,
                    camera = camera.name,
                    position = eye.ToString("F3"),
                    rotation = camera.transform.rotation.eulerAngles.ToString("F3"),
                    fov = fieldOfView,
                    width = width,
                    height = height,
                    sha256 = digest,
                    utc = DateTime.UtcNow.ToString("O"),
                    classification = "Runtime Unity render. Actual survivor camera, staged pose and FOV. World-space HUD may be present."
                };
                File.WriteAllText(path + ".json", JsonUtility.ToJson(receipt, true));
                File.AppendAllText(Report, "CAPTURE " + path + " " + digest + "\n");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
                camera.targetTexture = priorTexture;
                camera.allowMSAA = previousMSAA;
                camera.fieldOfView = priorFieldOfView;
                camera.aspect = priorAspect;
                camera.transform.SetPositionAndRotation(priorPosition, priorRotation);
                RenderTexture.active = priorActive;
                RenderTexture.ReleaseTemporary(render);
                UnityEngine.Object.Destroy(image);
            }
        }

        [Serializable]
        sealed class ShotReceipt
        {
            public string view, map, scene, builder, project, camera, position, rotation, sha256, utc, classification;
            public int pid, frame, width, height;
            public float fov;
        }

        static void CheckRoutes(System.Collections.Generic.Dictionary<string, Rigidbody[]> gifts)
        {
            var names = new[] { "Sofa Approach / Cream Step", "Tree-side Stack / Green Perch",
                "Skirt Landing / Navy Tread", "Bridge Junction / Low Step", "Gift Bridge / Navy Span",
                "Gift Bridge / Red Tread", "Gift Bridge / Red Shelf" };
            foreach (var name in names)
            {
                Require(gifts.TryGetValue(name, out var parts) &&
                    parts.All(body => body.GetComponent<IClimbable>()?.CanClimb == true),
                    name + " has climbable physics pieces");
            }
            Require(railBodies.Length > 5 && map.Root.Find("Underneath the Tree Layout/Independent railway trestles") == null,
                "the toy train has breakaway sections and no independent trestles");
            var trunk = map.Root.Find("Underneath the Tree Layout/Fir trunk collision");
            Require(trunk && railBodies.SelectMany(body => body.GetComponentsInChildren<BoxCollider>())
                .All(deck =>
                {
                    var upright = new Vector3(trunk.position.x, deck.bounds.center.y, trunk.position.z);
                    return Vector3.Distance(upright, deck.ClosestPoint(upright)) > 4.8f;
                }), "the trunk clears every piece of rail deck");
            var span = gifts["Gift Bridge / Navy Span"].Single(body => body.name == "Lid");
            var spanShape = span.GetComponent<BoxCollider>();
            Require(spanShape && spanShape.size.z > 45f && spanShape.size.x > 8f && span.position.y > 10f,
                "the widened and raised navy span is present");
            var center = span.transform.position;
            var floor = new Vector3(center.x, 1.65f, center.z);
            Require(!Physics.Raycast(floor - span.transform.right * 4f, span.transform.right, 8f, 1 << Layers.Block),
                "the floor-level bridge passage is open");
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
