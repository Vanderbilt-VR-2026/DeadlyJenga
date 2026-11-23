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
        static Rigidbody loosePanel, looseWall;
        static string[] originalPieces;
        static Rigidbody[] creamWalls;
        static Quaternion[] creamWallRotations;
        static Vector3 initialPanelPosition, initialWallPosition, initialTrainPosition;
        static int blockCount, revision;
        static double renderReady;
        static string captureDirectory;
        static Rigidbody bridgeLid;
        static Rigidbody[] railBodies;
        static Vector3 initialBridgePosition;
        static Rigidbody hitRail;
        static Vector3 initialRailPosition;
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

        public static void CheckBuildingModes()
        {
            Begin(false);
            SessionState.SetBool("TreeBuildingModes",true);
        }

        static void CheckBuildings()
        {
            map=Services.World as UnderneathTreeMap;
            if(!map || Time.frameCount<15)return;
            var buildings=map.GetComponentsInChildren<UnderneathTreeBuilding>();
            Require(buildings.Length==3,"three registered landmark buildings");
            if(phase==0)
            {
                foreach(var building in buildings)
                {
                    var hull=building.GetComponent<UnderneathTreeToy>().captureHull;
                    var floor=building.GetComponentsInChildren<MeshCollider>().Single(c=>c.name==hull);
                    var start=building.CapturePoint;
                    Require(building.SetTogether(true) && building.GetComponentsInChildren<Rigidbody>().Length==1,"one intact body on "+building.name);
                    Require(floor.attachedRigidbody==building.GetComponentInChildren<Rigidbody>() &&
                        Vector3.Distance(start,building.CapturePoint)<.03f,"unchanged floor pose on grouped collider");
                    Require(building.SetTogether(false) && building.GetComponentsInChildren<Rigidbody>().Length>=4,"section bodies restored");
                    Require(!building.Retired,"standing floor is valid");
                }
                buildings[0].SetTogether(true);
                var panel=buildings[0].GetComponentInChildren<UnderneathTreeGiftPanel>();
                panel.Push(Vector3.right*4,null);
                Require(!panel.Body.isKinematic && buildings[0].SetTogether(false),"moving shell splits");
                Require(buildings[0].GetComponentsInChildren<Rigidbody>().All(b=>!b.isKinematic),"split restores dynamic sections");
                Require(buildings[1].Demolish(Vector3.up) && buildings[1].Retired,"scripted wipe retires zone immediately");
                buildings[2].SetTogether(true);
                var body=buildings[2].GetComponentInChildren<Rigidbody>();
                body.rotation=Quaternion.AngleAxis(80,Vector3.forward)*body.rotation;
                impactFixedTime=Time.fixedTime;phase=1;
            }
            else if(Time.fixedTime-impactFixedTime>2.2f)
            {
                Require(buildings[2].Retired,"sustained floor tilt retires zone");
                Finish(true);
            }
        }

        public static void CaptureFalconSummit()
        {
            Begin(false);
            SessionState.SetBool("TreeFalconSummit", true);
        }

        public static void CapturePremiereStudios()
        {
            Begin(false);
            SessionState.SetBool("TreePremiereStudios", true);
        }

        public static void CaptureToyLighting()
        {
            Begin(false);
            SessionState.SetBool("TreeToyLighting", true);
            SessionState.SetBool("TreeCrystalCrag", true);
            SessionState.SetBool("TreePremiereStudios", true);
        }

        public static void CheckSmallToys()
        {
            Begin(false);
            SessionState.SetBool("TreeSmallToys", true);
        }

        public static void CaptureCrystalCrag()
        {
            Begin(false);
            SessionState.SetBool("TreeCrystalCrag", true);
        }

        static void Begin(bool references)
        {
            SessionState.SetBool("TreeSmallToys", false);
            SessionState.SetBool("TreeFalconSummit", false);
            SessionState.SetBool("TreePremiereStudios", false);
            SessionState.SetBool("TreeCrystalCrag", false);
            SessionState.SetBool("TreeToyLighting", false);
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run this check in a checked batch editor.");
            if (!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))
                throw new InvalidOperationException("Disaster validation menu is unavailable.");
            SessionState.SetBool("TreePriorAsync", ShaderUtil.allowAsyncCompilation);
            ShaderUtil.allowAsyncCompilation = false;
            SessionState.SetBool(References, references);
            SessionState.SetBool("TreeBuildingModes",false);
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
                if(SessionState.GetBool("TreeBuildingModes",false)){CheckBuildings();return;}
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
                    if (SessionState.GetBool("TreeSmallToys", false))
                    {
                        var toys = map.GetComponentsInChildren<UnderneathTreeToy>();
                        foreach (string label in new[] { "Race Car", "Retro Speaker", "Plush Bear", "Robot",
                            "Retro Console", "Airmail Bear", "Alpine Rally", "Mint Pickup", "Club Roadster",
                            "House", "Superhero", "Brick Stack", "Football", "Frosted Pinecone", "Stocking",
                            "Puzzle Cube", "Fashion Doll", "Nutcracker", "Blaster", "Candy Bag", "Rag Doll",
                            "Board Game", "Clay Tub", "Pastry Box", "Wind-up Walker", "Drum", "Soldier", "Classic Nutcracker", "Berry Twists", "Chocolate Bar",
                            "Cymbal Monkey", "Hopping Frog", "Marching Drummer", "Tin Locomotive", "Pecking Hen" })
                        {
                            var matches = toys.Where(t => t.source && t.source.name == "Toy " + label).ToArray();
                            Require(matches.Length > 0 && matches.All(t =>
                                t.GetComponentsInChildren<Rigidbody>().Length == 1 &&
                                t.GetComponentsInChildren<Collider>().Length == 1 && t.GetComponent<BoxCollider>() &&
                                t.GetComponentsInChildren<MeshRenderer>().Length == 1),
                                label + " loaded with one visual mesh, one box collider and one body");
                        }
                        var football = toys.First(t => t.source && t.source.name == "Toy Football");
                        var bounds = football.GetComponent<BoxCollider>().bounds;
                        Shot(player.ViewCamera, "soccer-ball", bounds.center + new Vector3(0.7f, 0.8f, -2f) * bounds.size.y,
                            bounds.center, 38f);
                        var bear = toys.Single(t => t.source && t.source.name == "Toy Airmail Bear");
                        var aim = bear.GetComponent<BoxCollider>().bounds.center;
                        Shot(player.ViewCamera, "airmail-bear", aim + bear.transform.rotation *
                            new Vector3(4f, 3f, -12f), aim, 40f);
                        Finish(true);
                        return;
                    }
                    if (SessionState.GetBool("TreeCrystalCrag", false))
                    {
                        var toy = map.GetComponentsInChildren<UnderneathTreeToy>().Single(t => t.name.StartsWith("Toy Wizard Tower "));
                        var pieces = toy.Pieces();
                        Require(pieces.Count == 4 && pieces.All(p => p.name.Contains("Crystal Crag") &&
                            p.TryGetComponent<Rigidbody>(out var body) && body.isKinematic), "four intact Crystal Crag sections");
                        var hulls = toy.GetComponentsInChildren<MeshCollider>();
                        Require(hulls.Count(c => c.name.StartsWith("Hull Spiral tread")) == 66, "66 spiral stair colliders");
                        var entry = hulls.First(c => c.name.StartsWith("Hull Entry step")).bounds.center;
                        var front = Vector3.ProjectOnPlane(entry - toy.transform.position, Vector3.up).normalized;
                        var aim = toy.transform.position + Vector3.up * 8f;
                        Shot(player.ViewCamera, "crystal-crag", aim + front * 31f + Vector3.up * 7.4f, aim, 37f, true);
                        if (!SessionState.GetBool("TreeToyLighting", false))
                        {
                            Finish(true);
                            return;
                        }
                        var gems = toy.GetComponentsInChildren<Light>();
                        Require(gems.Length == 6 && gems.All(l => l.enabled && l.type == LightType.Point &&
                            l.transform.parent.name.StartsWith("Practical Gem ")), "six active gem lights on wizard tower");
                    }
                    if (SessionState.GetBool("TreePremiereStudios", false))
                    {
                        var toy = map.GetComponentsInChildren<UnderneathTreeToy>().Single(t => t.name.StartsWith("Toy Doll Mansion "));
                        var pieces = toy.Pieces();
                        Require(pieces.Count == 4 && pieces.All(p => p.name.Contains("Premiere Studios") &&
                            p.TryGetComponent<Rigidbody>(out var body) && body.isKinematic), "four intact Premiere Studios sections on Mansion Deck");
                        var shapes = toy.GetComponentsInChildren<MeshCollider>();
                        if (SessionState.GetBool("TreeToyLighting", false))
                        {
                            var lamps = toy.GetComponentsInChildren<Light>();
                            Require(lamps.Count(l => l.enabled && l.type == LightType.Point && l.color.r > l.color.b &&
                                l.color.b > l.color.g) == 6, "six pink mansion room lights");
                            Require(lamps.Count(l => l.enabled && l.type == LightType.Spot &&
                                l.transform.parent.name == "Practical Cyan beacon") == 1 &&
                                lamps.Count(l => l.enabled && l.type == LightType.Spot &&
                                l.transform.parent.name == "Practical Violet beacon") == 1, "cyan and violet rooftop party beacons");
                        }
                        var floor = shapes.Single(c => c.name == "Hull Toy plinth").bounds.center;
                        var stage = shapes.Single(c => c.name == "Hull Stage deck").bounds.center;
                        var front = Vector3.ProjectOnPlane(floor - stage, Vector3.up).normalized;
                        var right = Vector3.Cross(front, Vector3.up);
                        var aim = floor + Vector3.up * 11f;
                        Shot(player.ViewCamera, "premiere-mansion", aim + front * 45f + right * 5f + Vector3.up * 8f, aim, 43f, true);
                        Shot(player.ViewCamera, "premiere-soundstage", floor + front * 6f + Vector3.up * 7.6f,
                            stage + Vector3.up * 3.7f, 63f, true);
                        Finish(true);
                        return;
                    }
                    if (SessionState.GetBool("TreeFalconSummit", false))
                    {
                        var toy = map.GetComponentsInChildren<UnderneathTreeToy>().Single(t => t.name.StartsWith("Toy Superhero Base "));
                        var pieces = toy.Pieces();
                        Require(pieces.Count == 6 && pieces.All(p => p.name.Contains("Falcon Summit") &&
                            p.TryGetComponent<Rigidbody>(out var body) && body.isKinematic), "six intact Falcon Summit storeys on Hero Deck");
                        var shapes = toy.GetComponentsInChildren<MeshCollider>();
                        var ramp = shapes.Single(c => c.name.Contains("launch ramp"));
                        var pad = shapes.Single(c => c.name.Contains("Cantilever flight pad"));
                        var forward = Vector3.ProjectOnPlane(ramp.bounds.center - toy.transform.position, Vector3.up).normalized;
                        var right = Vector3.ProjectOnPlane(pad.bounds.center - toy.transform.position, Vector3.up).normalized;
                        var aim = toy.transform.position + Vector3.up * 14f;
                        Shot(player.ViewCamera, "falcon-summit", aim + forward * 57f + right * 11f + Vector3.up * 14f, aim, 49f, true);
                        Finish(true);
                        return;
                    }
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
                    // Toys and toy buildings are blocks too, but not six- or ten-piece present shells.
                    var gifts = map.Blocks.Except(railBodies).Except(walkways)
                        .Where(body => !body.GetComponentInParent<UnderneathTreeToy>()).GroupBy(body => body.transform.parent.name)
                        .ToDictionary(group => group.Key, group => group.ToArray());
                    originalPieces = PieceOrder();
                    Require(gifts.Count >= 30 && map.Blocks.All(b => b && b.isKinematic && b.useGravity),
                        gifts.Count + " intact shells and " + blockCount + " independently registered physical pieces");
                    Require(gifts.Values.All(parts => parts.Length == 6 || parts.Length == 10) &&
                        gifts.Values.All(parts => parts.Count(b => b.name == "Bottom") == 1 && parts.Count(b => b.name == "Lid") == 1),
                        "every rectangular and cylindrical present has a physical base, walls and lid");
                    CheckSpawn();
                    CheckRoutes(gifts);
                    hitRail = railBodies[railBodies.Length / 2];
                    var train = map.GetComponentInChildren<UnderneathTreeTrain>();
                    Require(train, "tinplate train imported");
                    initialTrainPosition = train.transform.position;
                    Require(map.Root.GetComponentsInChildren<MeshRenderer>().Any(r =>
                        r.name.StartsWith("Skirt skull") && r.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0),
                        "skirt motifs are present");
                    var cream = gifts["Entrance / Cream Catch"];
                    var courtyard = gifts["Courtyard / Ruby Hop"];
                    creamWalls = cream.Where(b => b.name != "Bottom" && b.name != "Lid").ToArray();
                    creamWallRotations = creamWalls.Select(b => b.rotation).ToArray();
                    loosePanel = cream.Single(b => b.name == "Lid");
                    looseWall = courtyard.First(b => b.name == "Left");
                    initialPanelPosition = loosePanel.position;
                    initialWallPosition = looseWall.position;
                    cream.Single(b => b.name == "Bottom").GetComponent<IPushable>().Push(Vector3.up * 4f, null);
                    looseWall.GetComponent<IPushable>().Push(Vector3.up * 6.5f, null);
                    Require(cream.All(b => !b.isKinematic) && courtyard.All(b => !b.isKinematic),
                        "base removal breaks a rectangular gift and a heavy wall impact breaks a courtyard gift");
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
                    Require(Vector3.Distance(initialWallPosition, looseWall.position) > .1f,
                        "released courtyard wall is displaced by PhysX");
                    Require(Vector3.Distance(initialTrainPosition, map.GetComponentInChildren<UnderneathTreeTrain>().transform.position) > .2f,
                        "train advances on the track during Play Mode");
                    initialRailPosition = hitRail.position;
                    hitRail.GetComponentInChildren<BoxCollider>().GetComponentInParent<IPushable>().Push(Vector3.up * 6f, null);
                    phase = 2;
                    impactFixedTime = Time.fixedTime;
                    return;
                }
                if (phase == 2)
                {
                    Require(!hitRail.isKinematic && Vector3.Distance(initialRailPosition, hitRail.position) > .2f,
                        "a direct hit on a rail deck releases and moves its rigidbody");
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
                Services.Session.ResetMap();
                Require(map.Revision > revision && map.Blocks.Count == blockCount && map.Blocks.All(b => b.isKinematic),
                    "normal map reset restores the original intact pieces");
                Require(PieceOrder().SequenceEqual(originalPieces), "map reset preserves deterministic replica body order");
                var capturedLid = map.Blocks.Single(b => b.transform.parent.name == "Courtyard / Ruby Hop" && b.name == "Lid");
                map.SetReplica(true);
                capturedLid.position += Vector3.up * .4f;
                map.SetReplica(false);
                Require(!capturedLid.isKinematic && map.Blocks.Count(b => !b.isKinematic) == 1,
                    "restoring authority keeps the replica piece displaced by the host loose");
                CheckSpawn();
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
                Require(map.SaveLayout() == "underneath-the-tree-v7-building-shells", "approved underpass layout loaded");
                Require(map.Blocks.All(b => b && b.isKinematic && b.useGravity), "all gift pieces and rails intact with gravity configured");
                Require(map.Blocks.Where(b => b.gameObject.activeSelf).All(b => b.GetComponent<IClimbable>()?.CanClimb == true), "all registered gift and track bodies climbable");
                blockCount = map.Blocks.Count;
                revision = map.Revision;
                originalPieces = PieceOrder();
                var finish = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .FirstOrDefault(b => b.GetType().Name == "UnderneathTreeFinish");
                Require(finish && finish.isActiveAndEnabled && map.Root.Find("Christmas Eve Art and Lighting"),
                    "production Christmas finishing layer is attached and enabled after event reset");
                File.AppendAllText(Report, "BODIES " + blockCount + " RENDERERS " + map.Root.GetComponentsInChildren<Renderer>().Length + "\n");
                bridgeLid = map.Blocks.Single(b => b.name == "Lid" && b.transform.parent.name == "Gift Bridge / Navy Span");
                var bridgePaper = bridgeLid.transform.Find("Visual").GetComponent<Renderer>().sharedMaterial;
                var paperTexture = bridgePaper.GetTexture("_BaseMap");
                File.AppendAllText(Report, "BRIDGE MATERIAL " + bridgePaper.name + " SHADER " + bridgePaper.shader.name + " TEXTURE " + (paperTexture ? paperTexture.name : "missing") + "\n");
                Require(paperTexture && paperTexture.name == "Gold Snowflake Foil", "bridge uses approved fine foil texture");
                var rug = map.Root.GetComponentsInChildren<Renderer>().First(r => r.name.Contains("kill floor"));
                var rugTexture = rug.sharedMaterial.GetTexture("_BaseMap");
                File.AppendAllText(Report, "RUG MATERIAL " + rug.sharedMaterial.name + " TEXTURE " + (rugTexture ? rugTexture.name : "missing") + "\n");
                Require(rugTexture && rugTexture.name == "Cranberry Rug Weave", "rug uses chunky cranberry wool texture");
                var bridgeBase = map.Blocks.Single(b => b.name == "Bottom" && b.transform.parent.name == "Gift Bridge / Navy Span");
                var baseCollider = bridgeBase.GetComponent<BoxCollider>();
                Require(baseCollider.bounds.min.y > 14f, "navy bridge has tall ground passage");
                var tunnel = map.Blocks.Single(b => b.name == "Bottom" && b.transform.parent.name == "Train Tunnel / Gold Lintel");
                Require(tunnel.GetComponent<BoxCollider>().bounds.min.y > 13f, "train tunnel lintel clears railway elevation");
                railBodies = map.Blocks.Where(b => b.name.StartsWith("Gift-supported rail ")).ToArray();
                Require(railBodies.Length == 16 && railBodies.All(b => b.GetComponentsInChildren<BoxCollider>().All(c => WorkingDeck(c,b))),
                    "sixteen supported breakaway rail sections have valid climbable colliders");
                Require(railBodies.All(b => b.GetComponentsInChildren<BoxCollider>().Count(c => c.name == "Rail tie") == 8),
                    "each detachable rigid section contains eight physical rungs");
                var clearanceHits = railBodies.SelectMany(b => b.GetComponentsInChildren<BoxCollider>())
                    .Where(deck => deck.name == "Rail tie")
                    .SelectMany(deck => Physics.OverlapBox(deck.bounds.center + Vector3.up * 4.7f,
                        new Vector3(2.2f, 2.2f, .6f), deck.transform.rotation, 1 << Layers.Block, QueryTriggerInteraction.Ignore))
                    .Where(c => c.attachedRigidbody && !railBodies.Contains(c.attachedRigidbody)).Distinct().ToArray();
                var train = map.GetComponentInChildren<UnderneathTreeTrain>();
                Require(train, "train uses the new continuous course");
                initialTrainPosition = train.transform.position;
                Require(Physics.Raycast(map.SafeSpawnPoint() + Vector3.up * .3f, Vector3.down, out _, 1f, 1 << Layers.Block),
                    "spawn has a physical gift landing");
                File.AppendAllText(Report, "CAMERA far=" + camera.farClipPlane + " near=" + camera.nearClipPlane + " mask=" + camera.cullingMask + "\n");
                Shot(camera, "approved-overview", new Vector3(38f, 96f, -208f), new Vector3(-91f, 24f, -94f), 60f, true);
                Shot(camera, "approved-hero", new Vector3(40f, 78f, -215f), new Vector3(-95f, 23f, -105f), 55f, true);
                Shot(camera, "approved-overhead", new Vector3(-90f, 155f, -111f), new Vector3(-90f, 5f, -110f), 80f);
                Shot(camera, "approved-player-bridge", new Vector3(-64.3f, 3f, -132.6f), new Vector3(-86f, 25f, -106f), 72f);
                Shot(camera, "reverse-room", new Vector3(-160f, 72f, -35f), new Vector3(-82f, 32f, -155f), 72f);
                Shot(camera, "reverse-piles", new Vector3(-175f, 48f, -15f), new Vector3(-100f, 20f, -115f), 65f);
                Shot(camera, "east-side", new Vector3(55f, 48f, -85f), new Vector3(-90f, 24f, -110f), 70f);
                Shot(camera, "west-side", new Vector3(-235f, 45f, -115f), new Vector3(-90f, 22f, -110f), 70f);
                Shot(camera, "rear-player", new Vector3(-145f, 4f, -62f), new Vector3(-90f, 12f, -150f), 80f);
                Shot(camera, "front-room", new Vector3(-80f, 65f, -175f), new Vector3(-90f, 55f, -260f), 85f);
                var penetrations = new System.Collections.Generic.HashSet<string>();
                foreach (var shape in railBodies.SelectMany(b => b.GetComponentsInChildren<BoxCollider>()))
                foreach (var other in Physics.OverlapBox(shape.bounds.center, shape.bounds.extents, Quaternion.identity,
                    1 << Layers.Block, QueryTriggerInteraction.Ignore))
                {
                    if (!other.attachedRigidbody || railBodies.Contains(other.attachedRigidbody)) continue;
                    if (Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > .035f)
                        penetrations.Add(shape.attachedRigidbody.name + " into " + other.transform.parent.name + "/" + other.name);
                }
                foreach (string collision in penetrations) File.AppendAllText(Report, "RAIL PENETRATION " + collision + "\n");
                Require(penetrations.Count == 0, "physical rails and rungs do not penetrate presents");
                var giftContacts = new System.Collections.Generic.HashSet<string>();
                foreach (var body in map.Blocks.Where(b => !railBodies.Contains(b)))
                foreach (var shape in body.GetComponents<Collider>())
                foreach (var other in Physics.OverlapBox(shape.bounds.center, shape.bounds.extents, Quaternion.identity,
                    1 << Layers.Block, QueryTriggerInteraction.Ignore))
                {
                    var otherBody = other.attachedRigidbody;
                    if (!otherBody || railBodies.Contains(otherBody) || body.transform.parent == otherBody.transform.parent) continue;
                    if (Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > .10f)
                    {
                        var pair = new[] { body.transform.parent.name, otherBody.transform.parent.name };
                        System.Array.Sort(pair, StringComparer.Ordinal);
                        giftContacts.Add(string.Join(" / ", pair));
                    }
                }
                foreach (string collision in giftContacts) File.AppendAllText(Report, "GIFT PENETRATION " + collision + "\n");
                Require(giftContacts.Count == 0, "separate present shells do not penetrate one another");
                var shaders = map.Root.GetComponentsInChildren<Renderer>()
                    .SelectMany(r => r.sharedMaterials).Where(m => m && m.shader && m.shader.name.StartsWith("DeadlyJenga/Christmas"))
                    .Select(m => m.shader).Distinct();
                foreach (var shader in shaders)
                    Require(!ShaderUtil.GetShaderMessages(shader).Any(message => message.severity.ToString() == "Error"),
                        "rendered Christmas shader compiles: " + shader.name);
                Require(clearanceHits.Length == 0, "train clearance above every rail sample: " +
                    string.Join(", ", clearanceHits.Select(c => c.transform.parent.name + "/" + c.name)));
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
                railBodies[8].GetComponent<IPushable>().Push(Vector3.up * 4f, null);
                impactFixedTime = Time.fixedTime;
                phase = 1;
                return;
            }
            if (Time.fixedTime - impactFixedTime < 1f) return;
            Require(!bridgeLid.isKinematic && Vector3.Distance(initialBridgePosition, bridgeLid.position) > .2f,
                "removing bridge support releases and moves the navy span through PhysX");
            Require(!hitRail.isKinematic && Vector3.Distance(initialRailPosition, hitRail.position) > .2f,
                "removing rail gift releases and moves its track section");
            Require(!railBodies[8].isKinematic && railBodies[8].GetComponentsInChildren<BoxCollider>().Count(c => c.name == "Rail tie") == 8,
                "direct impact detaches one complete eight-rung rail section");
            Shot(Services.Session.Player.ViewCamera, "rail-and-bridge-release", new Vector3(38f, 96f, -208f), new Vector3(-91f, 24f, -94f), 60f);
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
            var names = new[] { "Entrance / Cream Catch", "Entrance / Green Step", "Left Summit / Wizard Cap",
                "Right Summit / Sapphire Crown", "Gift Bridge / Navy Span", "Train Tunnel / Gold Lintel" };
            foreach (var name in names)
            {
                Require(gifts.TryGetValue(name, out var parts) &&
                    parts.All(body => body.GetComponent<IClimbable>()?.CanClimb == true),
                    name + " has climbable physics pieces");
            }
            Require(railBodies.Length == 16 && railBodies.All(body =>
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
            Require(spanShape && Mathf.Max(spanShape.size.x, spanShape.size.z) > 45f && Mathf.Min(spanShape.size.x, spanShape.size.z) > 8f && span.position.y > 10f,
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

        static void CheckSpawn()
        {
            var origin = map.SafeSpawnPoint() + Vector3.up * .2f;
            Require(Physics.Raycast(origin, Vector3.down, out var landing, 1f, 1 << Layers.Block) &&
                landing.collider.attachedRigidbody && landing.collider.attachedRigidbody.name == "Lid" &&
                landing.normal.y > .92f && Climbables.TryGet(landing.collider, out var top) && top.CanClimb,
                "normal spawn has a stable climbable present lid underneath");
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
