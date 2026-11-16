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
        const string References = "UnderneathTreePlayChecks.References";
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
        static Rigidbody hitRail, hitWalkway;
        static Vector3 initialRailPosition, initialWalkwayPosition;
        static int referenceIndex;

        // High views cover the room perimeter, then the routes and their supports.
        static readonly (string name, Vector3 eye, Vector3 target, float fov)[] ReferenceViews =
        {
            ("01-room-overview", new Vector3(44.175f, 81f, -158.15f), new Vector3(-58f, 11f, -68f), 64f),
            ("02-reverse-three-quarter", new Vector3(-143f, 76f, 19f), new Vector3(-31f, 12f, -72f), 71f),
            ("03-overhead-layout", new Vector3(-31f, 155f, -113f), new Vector3(-43f, 4f, -62f), 64f),
            ("04-sofa-side", new Vector3(-165f, 60f, -112f), new Vector3(-42f, 12f, -58f), 69f),
            ("05-window-side", new Vector3(45f, 61f, 24f), new Vector3(-50f, 14f, -68f), 72f),
            ("06-front-along-rug", new Vector3(-28f, 46f, -158f), new Vector3(-37f, 10f, -61f), 67f),
            ("07-rear-along-rug", new Vector3(-71f, 68f, 33f), new Vector3(-39f, 10f, -77f), 69f),
            ("08-sofa-end-diagonal", new Vector3(-150f, 52f, -155f), new Vector3(-30f, 15f, -59f), 65f),
            ("09-window-end-diagonal", new Vector3(63f, 54f, -10f), new Vector3(-49f, 11f, -79f), 65f),
            ("10-low-room-wide", new Vector3(63f, 34f, -148f), new Vector3(-54f, 16f, -58f), 68f),
            ("11-tree-and-window", new Vector3(-115f, 53f, -125f), new Vector3(-27f, 53f, -20f), 62f),
            ("12-track-from-left", new Vector3(-132f, 50f, -85f), new Vector3(-62f, 10f, -74f), 62f),
            ("13-track-from-front", new Vector3(-60f, 51f, -124f), new Vector3(-66f, 12f, -56f), 61f),
            ("14-track-and-trunk", new Vector3(-110f, 37f, -29f), new Vector3(-56f, 8f, -51f), 61f),
            ("15-bridge-approach", new Vector3(64f, 27f, -109f), new Vector3(16f, 5.8f, -70.5f), 55f),
            ("16-bridge-reverse", new Vector3(-32f, 24f, -62f), new Vector3(17f, 6f, -70f), 59f),
            ("17-bridge-top-and-stairs", new Vector3(37f, 50f, -81f), new Vector3(20f, 4f, -73f), 57f),
            ("18-rug-edge-stacks", new Vector3(14f, 22f, -144f), new Vector3(-7f, 3f, -116f), 58f),
            ("19-sofa-side-stacks", new Vector3(-83f, 17f, -168f), new Vector3(-108f, 3f, -144f), 59f),
            ("20-tree-side-stacks", new Vector3(-124f, 18f, -6f), new Vector3(-100f, 4f, -24f), 59f),
            ("21-first-person-spawn", new Vector3(6.98f, 6.62f, -27.46f), new Vector3(6f, 4f, -19.5f), 72f),
            ("22-first-person-cream-drop", new Vector3(4.2f, 6.53f, -24.8f), new Vector3(4.9f, 4.05f, -20f), 72f),
            ("23-first-person-green-climb", new Vector3(5.15f, 5.7f, -20.45f), new Vector3(7.8f, 6.22f, -18.95f), 73f),
            ("24-first-person-tunnel", new Vector3(31.5f, 2.05f, -77.15f), new Vector3(10f, 3.25f, -67.9f), 72f),
            ("25-first-person-bridge-stairs", new Vector3(23f, 1.72f, -107f), new Vector3(15f, 7f, -86f), 72f),
            ("26-first-person-bridge-deck", new Vector3(12f, 13.2f, -80f), new Vector3(24f, 11.55f, -52f), 68f),
            ("27-first-person-rug-step", new Vector3(-.1f, 2.92f, -121.7f), new Vector3(-7.3f, 4.7f, -115.9f), 74f),
            ("28-first-person-ribbon-ramp", new Vector3(3.8f, 7f, -33.4f), new Vector3(3.8f, 11.1f, -46.9f), 72f),
        };

        public static void Run() { SessionState.SetBool("TreeApproved", false); Begin(false); }

        public static void RunApproved() { SessionState.SetBool("TreeApproved", true); Begin(false); }

        public static void RunReferences() => Begin(true);

        static void Begin(bool references)
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run this check in a checked batch editor.");
            if (!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))
                throw new InvalidOperationException("Disaster validation menu is unavailable.");
            SessionState.SetBool("TreePriorAsync", ShaderUtil.allowAsyncCompilation);
            ShaderUtil.allowAsyncCompilation = false;
            SessionState.SetBool(References, references);
            File.WriteAllText(Report, references
                ? "Underneath the Tree, intact solo Demo reference angles\n"
                : "Underneath the Tree, clean solo Game.unity load and runtime checks\n");
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
                    referenceIndex = 0;
                    renderReady = double.PositiveInfinity;
                    captureDirectory = SessionState.GetString("UnderneathTree.CaptureDirectory", "UnderneathTree");
                    deadline = EditorApplication.timeSinceStartup + (SessionState.GetBool(References, false) ? 480 : 135);
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
                if (SessionState.GetBool("TreeApproved", false))
                {
                    CheckApproved(now);
                    return;
                }
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
                    if (SessionState.GetBool(References, false))
                    {
                        if (Services.Session.Demo == null)
                            return;
                        if (referenceIndex == 0)
                            Require(map.Blocks.Count > 300 && map.Blocks.All(body => body && body.isKinematic),
                                "local Demo has an intact gift and rail layout");
                        var view = ReferenceViews[referenceIndex];
                        Shot(player.ViewCamera, view.name, view.eye, view.target, view.fov, true);
                        if (++referenceIndex == ReferenceViews.Length)
                            Finish(true);
                        return;
                    }
                    blockCount = map.Blocks.Count;
                    revision = map.Revision;
                    railBodies = map.Blocks.Where(body => body.name.StartsWith("Gift-supported rail ", StringComparison.Ordinal)).ToArray();
                    var walkways = map.Blocks.Where(body => body.transform.parent.name == "Present Walk to the Train Loop").ToArray();
                    var gifts = map.Blocks.Except(railBodies).Except(walkways).GroupBy(body => body.transform.parent.name)
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
                    Require(walkways.Length == 3 && walkways.All(body =>
                        WorkingDeck(body.GetComponent<BoxCollider>(), body)), "all three ribbon routes have physical, climbable decks");
                    hitRail = railBodies[railBodies.Length / 2];
                    hitWalkway = walkways.Single(body => body.name == "Ribbon approach");
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
                    initialRailPosition = hitRail.position;
                    initialWalkwayPosition = hitWalkway.position;
                    hitRail.GetComponentInChildren<BoxCollider>().GetComponentInParent<IPushable>().Push(Vector3.up * 6f, null);
                    hitWalkway.GetComponent<BoxCollider>().GetComponentInParent<IPushable>().Push(Vector3.up * 6f, null);
                    phase = 2;
                    impactFixedTime = Time.fixedTime;
                    return;
                }
                if (phase == 2)
                {
                    Require(!hitRail.isKinematic && Vector3.Distance(initialRailPosition, hitRail.position) > .2f,
                        "a direct hit on a rail deck releases and moves its rigidbody");
                    Require(!hitWalkway.isKinematic && Vector3.Distance(initialWalkwayPosition, hitWalkway.position) > .2f,
                        "a direct hit on a ribbon ramp releases and moves its rigidbody");
                    bridgeLid = map.Blocks.Single(body => body.name == "Lid" && body.transform.parent.name == "Gift Bridge / Navy Span");
                    initialBridgePosition = bridgeLid.position;
                    railBodies = railBodies.Where(body => body.isKinematic).ToArray();
                    map.Blocks.Single(body => body.name == "Bottom" && body.transform.parent.name == "Gift Bridge / Pine Bearing")
                        .GetComponent<IPushable>().Push(Vector3.left * 6f, null);
                    foreach (var bearing in map.Blocks.Where(body => body.name == "Bottom" &&
                        body.transform.parent.name.StartsWith("Rail Gift ", StringComparison.Ordinal)))
                        bearing.GetComponent<IPushable>().Push(Vector3.left * 6f, null);
                    phase = 3;
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

        static void CheckApproved(double now)
        {
            if (phase == 0)
            {
                map = Services.World as UnderneathTreeMap;
                var camera = Services.Session?.Player?.ViewCamera;
                if (!map || !camera || Time.frameCount < 20) return;
                if (double.IsPositiveInfinity(renderReady)) renderReady = now + 3;
                if (now < renderReady) return;
                File.AppendAllText(Report, "ASSEMBLY " + typeof(UnderneathTreeMap).Assembly.ManifestModule.ModuleVersionId + "\n");
                Require(map.SaveLayout() == "underneath-the-tree-v6-underpass", "approved underpass layout loaded");
                Require(map.Blocks.All(b => b && b.isKinematic && b.useGravity), "all gift pieces and rails intact with gravity configured");
                Require(map.Blocks.All(b => b.GetComponent<IClimbable>()?.CanClimb == true), "all registered gift and track bodies climbable");
                blockCount = map.Blocks.Count;
                revision = map.Revision;
                originalPieces = PieceOrder();
                var finish = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .FirstOrDefault(b => b.GetType().Name == "UnderneathTreeFinish");
                Require(finish && finish.isActiveAndEnabled && map.Root.Find("Christmas Eve Art and Lighting"),
                    "production Christmas finishing layer is attached and enabled after event reset");
                File.AppendAllText(Report, "BODIES " + blockCount + " RENDERERS " + map.Root.GetComponentsInChildren<Renderer>().Length + "\n");
                bridgeLid = map.Blocks.Single(b => b.name == "Lid" && b.transform.parent.name == "Gift Bridge / Navy Span");
                var bridgeBase = map.Blocks.Single(b => b.name == "Bottom" && b.transform.parent.name == "Gift Bridge / Navy Span");
                var baseCollider = bridgeBase.GetComponent<BoxCollider>();
                Require(baseCollider.bounds.min.y > 14f, "navy bridge has tall ground passage");
                var tunnel = map.Blocks.Single(b => b.name == "Bottom" && b.transform.parent.name == "Train Tunnel / Gold Lintel");
                Require(tunnel.GetComponent<BoxCollider>().bounds.min.y > 13f, "train tunnel lintel clears railway elevation");
                railBodies = map.Blocks.Where(b => b.name.StartsWith("Gift-supported rail ")).ToArray();
                Require(railBodies.Length == 16 && railBodies.All(b => b.GetComponentsInChildren<BoxCollider>().All(c => WorkingDeck(c,b))),
                    "sixteen supported breakaway rail sections have valid climbable colliders");
                var train = map.GetComponentInChildren<UnderneathTreeTrain>();
                Require(train, "train uses the new continuous course");
                initialTrainPosition = train.transform.position;
                Require(Physics.Raycast(map.SafeSpawnPoint() + Vector3.up * .3f, Vector3.down, out _, 1f, 1 << Layers.Block),
                    "spawn has a physical gift landing");
                File.AppendAllText(Report, "CAMERA far=" + camera.farClipPlane + " near=" + camera.nearClipPlane + " mask=" + camera.cullingMask + "\n");
                Shot(camera, "approved-overview", new Vector3(38f, 96f, -208f), new Vector3(-91f, 24f, -94f), 60f, true);
                Shot(camera, "approved-overhead", new Vector3(-90f, 245f, -135f), new Vector3(-90f, 5f, -110f), 64f);
                Shot(camera, "approved-player-bridge", new Vector3(-64.3f, 3f, -132.6f), new Vector3(-86f, 25f, -106f), 72f);
                impactFixedTime = Time.fixedTime;
                phase = 10;
                return;
            }
            if (phase == 10)
            {
                if (Time.fixedTime - impactFixedTime < 1f) return;
                Require(Vector3.Distance(initialTrainPosition, map.GetComponentInChildren<UnderneathTreeTrain>().transform.position) > .1f,
                    "train advances on the intact course before support removal");
                initialBridgePosition = bridgeLid.position;
                hitRail = railBodies[0];
                initialRailPosition = hitRail.position;
                map.Blocks.Single(b => b.name == "Bottom" && b.transform.parent.name == "Gift Bridge / Pine Bearing")
                    .GetComponent<IPushable>().Push(Vector3.left * 6f, null);
                map.Blocks.Single(b => b.name == "Bottom" && b.transform.parent.name == "Rail Gift 1")
                    .GetComponent<IPushable>().Push(Vector3.left * 6f, null);
                impactFixedTime = Time.fixedTime;
                phase = 1;
                return;
            }
            if (Time.fixedTime - impactFixedTime < 1f) return;
            Require(!bridgeLid.isKinematic && Vector3.Distance(initialBridgePosition, bridgeLid.position) > .2f,
                "removing bridge support releases and moves the navy span through PhysX");
            Require(!hitRail.isKinematic && Vector3.Distance(initialRailPosition, hitRail.position) > .2f,
                "removing rail gift releases and moves its track section");
            Services.Session.ResetMap();
            Require(map.Revision > revision && map.Blocks.Count == blockCount && map.Blocks.All(b => b.isKinematic),
                "reset restores approved layout intact");
            Require(PieceOrder().SequenceEqual(originalPieces), "reset preserves deterministic network body order");
            Finish(true);
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
            Require(railBodies.Length > 5 && railBodies.All(body =>
            {
                var decks = body.GetComponentsInChildren<BoxCollider>();
                return decks.Length > 0 && decks.All(deck => WorkingDeck(deck, body));
            }) && map.Root.Find("Underneath the Tree Layout/Independent railway trestles") == null,
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

        static bool WorkingDeck(BoxCollider deck, Rigidbody body) =>
            deck && deck.enabled && deck.attachedRigidbody == body && deck.gameObject.layer == Layers.Block &&
            deck.GetComponentInParent<IPushable>() != null && Climbables.TryGet(deck, out var climbable) && climbable.CanClimb;

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
            ShaderUtil.allowAsyncCompilation = SessionState.GetBool("TreePriorAsync", true);
            SessionState.SetBool(Failed, !passed);
            File.AppendAllText(Report, passed ? "PASSED\n" : "FAILED\n");
            EditorApplication.ExitPlaymode();
        }
    }
}
