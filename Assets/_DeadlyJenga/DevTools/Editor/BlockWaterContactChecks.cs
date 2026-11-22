using System;
using System.Collections;
using System.IO;
using System.Reflection;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class BlockWaterContactChecks
    {
        const string RunKey = nameof(BlockWaterContactChecks);
        const string FixtureKey = RunKey + "Fixture";
        static string Fixture => SessionState.GetString(FixtureKey, "Water");
        static string Report => ProjectPath.Report("Block" + Fixture + "Contact.txt");
        static int failures;

        static BlockWaterContactChecks() => EditorApplication.playModeStateChanged += OnPlayMode;

        public static void Run() => Begin("Water");
        public static void RunSand() => Begin("Sand");
        public static void RunGameViews() => Begin("GameView");

        static void Begin(string fixture)
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run in a reserved batch editor.");

            SessionState.SetString(FixtureKey, fixture);
            if (fixture == "GameView")
            {
                MatchSetup.FromLobby = false;
                MatchSetup.StartLocalSession = false;
                MatchSetup.ModeId = "demo";
                MatchSetup.MapId = "castle";
                MatchSetup.BotTarget = 0;
                MatchSetup.HandCount = 0;
                File.WriteAllText(Report, "Game.unity in desktop Unity Play Mode; controlled role-camera poses and bot drops onto island support.\n");
                EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            }
            else
            {
                File.WriteAllText(Report, "Unity Play Mode, production Castle block and contact components, isolated contact geometry.\n");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            SessionState.SetBool(RunKey, true);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunKey, false)) return;

            SessionState.EraseBool(RunKey);
            failures = 0;
            Application.logMessageReceived += RecordError;
            EditorApplication.delayCall += () =>
                new GameObject(RunKey).AddComponent<BlockWaterContactDriver>().StartCoroutine(Guard());
        }

        static IEnumerator Guard()
        {
            var checks = Fixture switch
            {
                "Sand" => SandChecks(),
                "GameView" => GameViewChecks(),
                _ => Checks()
            };
            while (true)
            {
                object next;
                try
                {
                    if (!checks.MoveNext()) break;
                    next = checks.Current;
                }
                catch (Exception error)
                {
                    Check(false, error.ToString());
                    break;
                }
                yield return next;
            }

            Application.logMessageReceived -= RecordError;
            Log($"COMPLETE failures={failures}");
            SessionState.EraseString(FixtureKey);
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void RecordError(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message.StartsWith("connection.state_change", StringComparison.Ordinal) &&
                stack.Contains("Unity.Relay.Editor.RelayService"))
            {
                Log("EDITOR NOTE unrelated Unity assistant relay connection: " + message);
                return;
            }
            Check(false, message + "\n" + stack);
        }

        static IEnumerator Checks()
        {
            var root = new GameObject("Block contact fixture");
            var map = root.AddComponent<CastleGenerator>();
            map.generateOnStart = false;
            map.waterHeight = 0;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleBlock.prefab"));
            if (!prefab) throw new InvalidOperationException("CastleBlock prefab was not found.");

            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "Static shoreline shelf";
            shelf.transform.SetParent(root.transform);
            shelf.transform.position = new Vector3(0, -.55f, 20);
            shelf.transform.localScale = new Vector3(6, 1, 8);

            var fixedStep = new WaitForFixedUpdate();
            for (int round = 1; round <= 2; round++)
            {
                var drops = new[]
                {
                    AddDrop(map, prefab, "small ordinary", new Vector3(-10, 2, 0), new Vector3(.65f, .45f, .65f), 4),
                    AddDrop(map, prefab, "large ordinary", new Vector3(-5, 3, 0), new Vector3(2.4f, 1.8f, 1.3f), 4),
                    AddDrop(map, prefab, "small fast", new Vector3(0, 3, 0), new Vector3(.65f, .45f, .65f), 40),
                    AddDrop(map, prefab, "large fast", new Vector3(5, 3, 0), new Vector3(2.4f, 1.8f, 1.3f), 40),
                    AddDrop(map, prefab, "shore supported", new Vector3(0, 2, 20), new Vector3(1, .8f, 1), 4),
                };
                Physics.SyncTransforms();
                float end = Time.fixedTime + 5;
                while (Time.fixedTime < end)
                {
                    foreach (var drop in drops) drop.Observe();
                    yield return fixedStep;
                }

                foreach (var drop in drops)
                {
                    if (drop.Body.name == "shore supported")
                    {
                        Check(drop.Body.gameObject.activeSelf && !drop.Wet && drop.Shape.bounds.min.y > -.15f,
                            $"round {round}, supported shoreline remains dry at {drop.Shape.bounds.min.y:F2} m");
                        continue;
                    }

                    Check(drop.Wet && drop.EntryBottom < 0 && drop.EntryTop > 0 && drop.EntrySpeed <= 2.5f,
                        $"round {round}, {drop.Body.name}: wet entry bottom/top={drop.EntryBottom:F2}/{drop.EntryTop:F2} m, down speed={drop.EntrySpeed:F2} m/s");
                    Check(drop.StraddlingTime >= .08f, $"round {round}, {drop.Body.name}: wet at surface for {drop.StraddlingTime:F2} s");
                    Check(!drop.Body.gameObject.activeSelf && drop.LastTop <= -2.8f &&
                          Mathf.Approximately(drop.Body.linearDamping, drop.DryLinear) &&
                          Mathf.Approximately(drop.Body.angularDamping, drop.DryAngular),
                        $"round {round}, {drop.Body.name}: retired={!drop.Body.gameObject.activeSelf}, last live top={drop.LastTop:F2} m, dry damping restored");
                }

                foreach (var drop in drops) Object.Destroy(drop.Body.gameObject);
                map.environmentOnly = true;
                map.Rebuild();
                yield return fixedStep;
            }

            Object.Destroy(root);
            yield return null;
            Check(Services.World == null, "World registration released on fixture teardown");
        }

        static Drop AddDrop(CastleGenerator map, GameObject prefab, string name, Vector3 position, Vector3 size, float speed)
        {
            var instance = Object.Instantiate(prefab, position, Quaternion.identity, map.transform);
            instance.name = name;
            instance.transform.localScale = size;
            var body = instance.GetComponent<Rigidbody>();
            body.linearDamping = .15f;
            body.angularDamping = .25f;
            body.linearVelocity = Vector3.down * speed;
            map.Bodies.Add(body);
            return new Drop(body);
        }

        static IEnumerator SandChecks()
        {
            var root = new GameObject("Sand contact fixture");
            var map = root.AddComponent<CastleGenerator>();
            map.generateOnStart = false;
            map.waterHeight = 0;
            root.AddComponent<CandyWaterSandEffects>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleBlock.prefab"));
            var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CandyArtProfile>(ProjectPath.Find("Candy Tropical.asset")));
            profile.islandRadius = 6;
            profile.sandRelief = 0;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Sand fixture static support";
            floor.transform.SetParent(root.transform);
            floor.transform.position = Vector3.down * .35f;
            floor.transform.localScale = new Vector3(12, 1, 12);
            floor.GetComponent<MeshRenderer>().enabled = false;

            var terrain = new GameObject("Generated sand");
            terrain.transform.SetParent(root.transform);
            terrain.transform.position = Vector3.up * .15f;
            var filter = terrain.AddComponent<MeshFilter>();
            terrain.AddComponent<MeshRenderer>().sharedMaterial = profile.sand;
            var sand = terrain.AddComponent<CandySandbar>();
            sand.profile = profile;
            sand.Refresh();
            var mesh = filter.sharedMesh;
            Check(mesh, "Production terrain generated");
            var before = mesh.vertices;
            var fixedStep = new WaitForFixedUpdate();

            var eyeObject = new GameObject("Contact fixture camera");
            eyeObject.transform.SetParent(root.transform);
            eyeObject.tag = "MainCamera";
            var eye = eyeObject.AddComponent<Camera>();
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = Color.black;
            var target = new RenderTexture(64, 64, 24);
            eye.targetTexture = target;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.layer = 28;
            eye.cullingMask = 1 << marker.layer;
            marker.GetComponent<Collider>().enabled = false;
            marker.transform.SetParent(eye.transform, false);
            marker.transform.localPosition = Vector3.forward * 2;
            var markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            markerMaterial.SetColor("_BaseColor", Color.red);
            marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;

            eye.transform.position = new Vector3(15, .6f, 0);
            yield return null;
            var dry = RenderMarker(eye, target);
            Check(dry.r > .75f, $"dry camera renders red marker {dry}");
            eye.transform.position = new Vector3(15, -.35f, 0);
            yield return new WaitForSeconds(.45f);
            var shallow = RenderMarker(eye, target);
            Check(shallow.r < dry.r * .7f && shallow.b > dry.b + .02f, $"shallow open water filters scene {shallow}");
            eye.transform.position = new Vector3(15, .6f, 0);
            yield return new WaitForSeconds(.35f);
            var surfaced = RenderMarker(eye, target);
            Check(surfaced.r > dry.r * .95f, $"surfacing clears the water filter {surfaced}");
            eye.transform.position = new Vector3(0, -.07f, 0);
            yield return new WaitForSeconds(.32f);
            var landing = RenderMarker(eye, target);
            Check(landing.r > dry.r * .95f, $"near-water sand landing pose stays dry {landing}");
            eye.transform.position = new Vector3(0, -2, 0);
            yield return null;
            yield return null;
            var hidden = RenderMarker(eye, target);
            Check(hidden.r < .14f && hidden.b > .02f, $"under-map camera occludes near scenery {hidden}");
            eye.targetTexture = null;
            Object.Destroy(eyeObject);
            Object.Destroy(markerMaterial);
            Object.Destroy(target);

            var spray = root.transform.Find("Water splash droplets").GetComponent<ParticleSystem>();
            int smallSpray = 0;
            for (int trial = 0; trial < 2; trial++)
            {
                var size = trial == 0 ? new Vector3(.65f, .45f, .65f) : new Vector3(2.4f, 1.8f, 1.3f);
                var drop = AddDrop(map, prefab, "Splash size fixture", new Vector3(15, 3, 0), size, trial == 0 ? 5 : 25);
                Physics.SyncTransforms();
                float timeout = Time.time + 2;
                while (Time.time < timeout && spray.particleCount == 0) yield return null;
                int firstSpray = spray.particleCount;
                int peakSpray = firstSpray;
                timeout = Time.time + .55f;
                while (Time.time < timeout)
                {
                    yield return null;
                    peakSpray = Mathf.Max(peakSpray, spray.particleCount);
                }
                Check(firstSpray > 0 && firstSpray == peakSpray,
                    $"splash {trial + 1}: {firstSpray} droplets on entry, {peakSpray} peak during immersion");
                if (trial == 0) smallSpray = firstSpray;
                else Check(firstSpray > smallSpray, $"large fast splash emits more droplets ({firstSpray} vs {smallSpray})");
                drop.Body.gameObject.SetActive(false);
                Object.Destroy(drop.Body.gameObject);
                map.Bodies.Clear();
                spray.Clear();
                yield return fixedStep;
            }

            for (int round = 1; round <= 2; round++)
            {
                var debrisRoot = new GameObject("Sand debris controller");
                debrisRoot.transform.SetParent(root.transform);
                var debris = debrisRoot.AddComponent<CastleDebris>();
                debris.ShatterOnImpact = false;
                debris.VisualFastDebris = false;
                var left = AddDrop(map, prefab, "left heavy block", new Vector3(-.7f, 2.8f, 0), Vector3.one, 0).Body;
                var right = AddDrop(map, prefab, "right heavy block", new Vector3(.7f, 2.8f, 0), Vector3.one, 0).Body;
                left.mass = 55;
                right.mass = 70;
                left.useGravity = right.useGravity = false;
                Physics.SyncTransforms();
                debris.Initialize(map.Bodies, null);

                yield return new WaitForSeconds(2.1f);
                left.useGravity = right.useGravity = true;
                left.linearVelocity = right.linearVelocity = Vector3.down * 6;
                float deadline = Time.fixedTime + 2.5f;
                while (Time.fixedTime < deadline && debris.SinkingCount < 2) yield return fixedStep;
                Check(debris.SinkingCount == 2, $"round {round}: both heavy blocks enter the production sand sink");
                yield return new WaitForSeconds(.85f);

                var during = mesh.vertices;
                float leftCut = CutDepth(before, during, -.7f);
                float rightCut = CutDepth(before, during, .7f);
                float joinCut = CutDepth(before, during, 0);
                Check(leftCut > .1f && rightCut > .1f && joinCut > .01f,
                    $"round {round}: terrain drops {leftCut:F2}/{rightCut:F2} m, overlap {joinCut:F2} m");
                var ripple = root.transform.Find("Sand ripples")?.GetComponent<ParticleSystem>();
                Check(ripple && ripple.particleCount > 0, $"round {round}: sand ripple particles emitted");

                if (round == 1)
                {
                    var hazard = new HazardFixture();
                    DisasterStates.Active.Add(hazard);
                    yield return fixedStep;
                    Check(!left.isKinematic && !right.isKinematic && debris.SinkingCount == 0,
                        "active hazard releases both sinking blocks to physics");
                    DisasterStates.Active.Remove(hazard);
                    left.gameObject.SetActive(false);
                    right.gameObject.SetActive(false);
                }
                else
                {
                    deadline = Time.fixedTime + 6;
                    while (Time.fixedTime < deadline && (left.gameObject.activeSelf || right.gameObject.activeSelf))
                        yield return fixedStep;
                    Check(!left.gameObject.activeSelf && !right.gameObject.activeSelf && debris.SunkCount == 2,
                        "both repeat-round blocks fully sink and retire");
                }

                yield return new WaitForSeconds(3.8f);
                var restored = mesh.vertices;
                Check(CutDepth(before, restored, -.7f) < .001f && CutDepth(before, restored, .7f) < .001f,
                    $"round {round}: original sand heights restored after creep settles");
                Object.Destroy(left.gameObject);
                Object.Destroy(right.gameObject);
                Object.Destroy(debrisRoot);
                map.Bodies.Clear();
                yield return fixedStep;
            }

            Object.Destroy(root);
            Object.Destroy(profile);
            yield return null;
            Check(Services.World == null, "World registration released on fixture teardown");
        }

        static float CutDepth(Vector3[] original, Vector3[] deformed, float x)
        {
            float cut = 0;
            for (int i = 0; i < original.Length; i++)
                if (Mathf.Abs(original[i].x - x) < .26f && Mathf.Abs(original[i].z) < .4f)
                    cut = Mathf.Max(cut, original[i].y - deformed[i].y);
            return cut;
        }

        static Color RenderMarker(Camera camera, RenderTexture target, string capture = null)
        {
            var previous = RenderTexture.active;
            var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixel.ReadPixels(new Rect(32, 32, 1, 1), 0, 0);
                if (capture != null)
                {
                    var frame = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    try
                    {
                        frame.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Report), capture + ".png"), frame.EncodeToPNG());
                    }
                    finally { Object.Destroy(frame); }
                }
                return pixel.GetPixel(0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(pixel);
            }
        }

        static IEnumerator GameViewChecks()
        {
            float deadline = Time.realtimeSinceStartup + 45;
            while (Time.realtimeSinceStartup < deadline && (!(Services.Session is CastlePlaySession ready) ||
                   !ready.Player?.ViewCamera || !ready.Hand?.ViewCamera || (Services.World?.Blocks.Count ?? 0) <= 0))
                yield return null;

            var session = Services.Session as CastlePlaySession;
            var world = Services.World;
            if (!session || world is not CastleGenerator map || !session.Player?.ViewCamera || !session.Hand?.ViewCamera)
                throw new InvalidOperationException("Game.unity did not initialize both roles on CastleGenerator.");
            var effects = session.GetComponent<CandyWaterSandEffects>();
            if (!effects) throw new InvalidOperationException("Production water/sand presenter did not attach.");
            var room = IslandRoomConnection.Instance;
            Check(room && !room.Connected && RoomGameExperience.Current.Experience == GameExperience.Demo,
                $"local Demo ready: {world.Blocks.Count} blocks; nominal water {world.WaterHeight(world.Center):F2} m");

            session.SetMenu(false);
            session.SurvivorBots.AutoFill = false;
            session.SurvivorBots.Clear();
            var target = new RenderTexture(64, 64, 24);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.layer = 28;
            marker.GetComponent<Collider>().enabled = false;
            var markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            markerMaterial.SetColor("_BaseColor", Color.red);
            marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;

            var openWater = world.Center + Vector3.right * (world.Radius + 85);
            var dry = new Vector3(openWater.x, world.WaterHeight(openWater) + .6f, openWater.z);
            var shallow = new Vector3(openWater.x, world.WaterHeight(openWater) - .35f, openWater.z);
            var below = world.Center;
            below.y = world.WaterHeight(below) - 4.5f;

            // The real match guard substitutes its own above-water camera. Hold it for these injected views.
            session.Modes.enabled = false;
            for (int role = 0; role < 2; role++)
            {
                bool hand = role == 1;
                session.SetRole(hand);
                session.SetMenu(false);
                yield return null;
                var actor = hand ? session.Hand : session.Player;
                var eye = actor.ViewCamera;
                string label = hand ? "flying hand" : "survivor";
                Check(session.PlayingHand == hand && eye && eye.enabled && session.ActiveCamera == eye,
                    $"{label} owns the real active camera: playingHand={session.PlayingHand}, active={session.ActiveCamera?.name}, enabled={eye && eye.enabled}");

                if (!eye || session.ActiveCamera != eye) continue;
                var savedTexture = eye.targetTexture;
                int savedMask = eye.cullingMask;
                var eyeTransform = eye.transform;
                var savedParent = eyeTransform.parent;
                var savedPosition = eyeTransform.localPosition;
                var savedRotation = eyeTransform.localRotation;
                var savedScale = eyeTransform.localScale;
                var follow = hand ? session.HandActor().follow : null;
                bool followEnabled = follow && follow.enabled;
                var poses = new[] { dry, shallow, below, dry };
                var colors = new Color[poses.Length];

                // Keep the actual camera active while the actor's motion moves its usual parent.
                try
                {
                    if (follow) follow.enabled = false;
                    eyeTransform.SetParent(null, false);
                    eyeTransform.localScale = Vector3.one;
                    eye.targetTexture = target;
                    eye.cullingMask = 1 << marker.layer;
                    marker.transform.SetParent(eyeTransform, false);
                    marker.transform.SetLocalPositionAndRotation(Vector3.forward * 2, Quaternion.identity);

                    for (int i = 0; i < poses.Length; i++)
                    {
                        eyeTransform.SetPositionAndRotation(poses[i], Quaternion.identity);
                        deadline = Time.unscaledTime + .55f;
                        int heldFrames = 0;
                        do
                        {
                            yield return null;
                            if (Vector3.Distance(eyeTransform.position, poses[i]) > .01f)
                                throw new InvalidOperationException($"{label} eye moved during the controlled view sample.");
                            heldFrames++;
                        } while (Time.unscaledTime < deadline || heldFrames < 3);
                        var capture = $"{(hand ? "hand" : "survivor")}-{i}";
                        LogViewSample(capture, poses[i], session, eye, effects, marker, target, world);
                        colors[i] = RenderMarker(eye, target, capture);
                    }
                }
                finally
                {
                    marker.transform.SetParent(null, false);
                    eye.targetTexture = savedTexture;
                    eye.cullingMask = savedMask;
                    eyeTransform.SetParent(savedParent, false);
                    eyeTransform.SetLocalPositionAndRotation(savedPosition, savedRotation);
                    eyeTransform.localScale = savedScale;
                    if (follow) follow.enabled = followEnabled;
                }

                Check(colors[0].r > .65f, $"{label} dry reference renders the red marker {colors[0]}");
                Check(colors[0].r > .65f && colors[1].r < colors[0].r * .75f && colors[1].b > colors[0].b + .02f,
                    $"{label} dry and shallow-water samples {colors[0]} / {colors[1]}");
                Check(colors[2].r < .22f && colors[2].b > .02f, $"{label} cannot see beneath the map {colors[2]}");
                Check(colors[0].r > .65f && colors[3].r > .65f && colors[3].r > colors[0].r * .9f, $"{label} clears the filter after resurfacing {colors[3]}");
            }
            session.Modes.enabled = true;
            Object.Destroy(marker);
            Object.Destroy(markerMaterial);
            Object.Destroy(target);
            session.SetRole(false);
            yield return null;

            var rules = session.Modes.Rules.Copy();
            rules.island = IslandRule.Sink;
            rules.sinkGraceSeconds = 10;
            rules.countdownSeconds = 0;
            rules.automaticDisasters = false;
            rules.fallDamage = false;
            session.Modes.Configure(rules);
            session.Modes.StartRound();
            session.SurvivorBots.Running = false;
            var bot = session.Bots()?.SpawnBot();
            if (!bot || !bot.Motor) throw new InvalidOperationException("Production survivor bot did not spawn.");
            var motor = bot.Motor;
            motor.InputSuspended = true;
            var sand = (ISandState)motor;
            var visual = motor.GetComponent<IModeActor>()?.Visual;
            if (!visual) throw new InvalidOperationException("Production bot did not expose an avatar visual.");
            var shore = FindIslandSupport(map);
            var safe = world.SafeSpawnPoint();

            var survivor = session.Survivor();
            survivor.InputSuspended = true;
            survivor.Teleport(shore + Vector3.up * 12f, Quaternion.identity);
            var releasedEye = survivor.ViewCamera.transform.position;
            float initialClearance = releasedEye.y - world.WaterHeight(releasedEye);
            float lowestEye = initialClearance;
            deadline = Time.time + 3;
            while (!survivor.Grounded && Time.time < deadline)
            {
                var position = survivor.ViewCamera.transform.position;
                lowestEye = Mathf.Min(lowestEye, position.y - world.WaterHeight(position));
                yield return null;
            }
            var landedEye = survivor.ViewCamera.transform.position;
            lowestEye = Mathf.Min(lowestEye, landedEye.y - world.WaterHeight(landedEye));
            var feet = survivor.transform.position;
            var horizontalError = Vector3.ProjectOnPlane(feet - shore, Vector3.up).magnitude;
            var supportHeightError = Mathf.Abs(feet.y - shore.y);
            float landingEyeOffset = landedEye.y - feet.y;
            Check(survivor.Grounded && horizontalError < 1f && supportHeightError < .5f &&
                  initialClearance - lowestEye > 10f && landingEyeOffset > 0 && landingEyeOffset < survivor.StandingHeight + .25f &&
                  lowestEye > 0 && session.ActiveCamera == survivor.ViewCamera,
                $"survivor drops 12 m onto actual sand; clearance {initialClearance:F2} to {lowestEye:F2} m, eye offset {landingEyeOffset:F2} m, feet {feet:F2}, intended support {shore:F2}");
            survivor.Teleport(safe, Quaternion.identity);

            for (int trial = 0; trial < 2; trial++)
            {
                bool sustained = trial == 1;
                float duration = sustained ? 1.35f : .35f;
                motor.Teleport(shore + Vector3.up * .55f, Quaternion.identity);
                deadline = Time.time + 2.5f;
                while (!sand.InSand && Time.time < deadline) yield return null;
                Check(sand.InSand, $"bot reaches sand through real island contact at {shore}");
                float entered = Time.time;
                while (sand.InSand && Time.time - entered < duration) yield return null;
                float observed = Time.time - entered;
                float sampled = CoatValue(effects, motor, "sandSeconds");
                Check(sand.InSand && sand.SandDepth > 0 && observed >= duration &&
                    (sustained ? sampled >= 1f : sampled < 1f && !float.IsNaN(sampled)),
                    $"bot maintains {(sustained ? "sustained" : "short")} sand contact: observed {observed:F3}s, presenter sampled {sampled:F3}s, depth {sand.SandDepth:F3}");
                motor.Teleport(safe, Quaternion.identity);
                // Teleport clears the motor immediately. Wait for match and support updates to agree.
                deadline = Time.time + 2.5f;
                int drySupportFrames = 0;
                do
                {
                    yield return null;
                    drySupportFrames = motor.Grounded && !sand.InSand ? drySupportFrames + 1 : 0;
                } while (drySupportFrames < 2 && Time.time < deadline);
                Check(drySupportFrames >= 2,
                    $"bot settles clear of sand after {(sustained ? "sustained" : "short")} contact: " +
                    $"grounded={motor.Grounded}, inSand={sand.InSand}, feet={motor.transform.position:F2}, safe={safe:F2}");
                if (!sustained) Check(!FindCoat(visual), "short contact leaves no coating");
            }
            float appliedAt = CoatValue(effects, motor, "coatedAt");
            var coat = FindCoat(visual);
            Check(coat && appliedAt >= 0 && coat.GetFloat("_CoatTop") > coat.GetFloat("_CoatBottom") &&
                  coat.GetFloat("_Coverage") >= .95f,
                $"bot exits sustained sand with a full lower-body coat: material={coat != null}, appliedAt={appliedAt:F3}");

            if (coat && appliedAt >= 0)
            {
                while (Time.time - appliedAt < 18.5f) yield return null;
                float coverage = coat ? coat.GetFloat("_Coverage") : 0;
                Check(coverage > 0 && coverage < .9f,
                    $"bot coating is dusting off at age {Time.time - appliedAt:F2}s, coverage={coverage:F3}");
                while (Time.time - appliedAt < 20.5f) yield return null;
                Check(!FindCoat(visual), $"bot coating is removed at age {Time.time - appliedAt:F2}s");
            }

            // A forced drowning pose can start a death view. Run it after the other checks.
            var survivorEye = session.Player.ViewCamera;
            survivorEye.transform.position = shallow;
            yield return null;
            var guardedEye = session.ActiveCamera;
            Check(guardedEye && guardedEye != survivorEye && guardedEye.transform.position.y >= world.WaterHeight(shallow),
                "normal match view hands off to an above-water camera during survival immersion");
        }

        static float CoatValue(CandyWaterSandEffects effects, Component owner, string field)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            if (typeof(CandyWaterSandEffects).GetField("coats", flags)?.GetValue(effects) is IDictionary coats &&
                coats[owner.GetInstanceID()] is object state &&
                state.GetType().GetField(field)?.GetValue(state) is float value)
                return value;
            return float.NaN;
        }

        static void LogViewSample(string name, Vector3 expected, CastlePlaySession session, Camera eye,
            CandyWaterSandEffects effects, GameObject marker, RenderTexture target, IWorldQuery world)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(CandyWaterSandEffects);
            var point = eye.transform.position;
            var sampledEye = type.GetField("waterEye", flags)?.GetValue(effects) as Camera;
            var wet = type.GetField("underwater", flags)?.GetValue(effects);
            var concealed = type.GetField("hiddenDepth", flags)?.GetValue(effects);
            var dipped = type.GetField("dippedFor", flags)?.GetValue(effects);
            var localSand = session.Player.gameObject.GetComponent<ISandState>();
            Log($"PROBE {name}: expected={expected:F2}, eye={point:F2}, depth={world.WaterHeight(point) - point.y:F3}, " +
                $"active={session.ActiveCamera == eye}, sampledEye={sampledEye == eye}, wet={wet}, concealed={concealed}, " +
                $"dippedFor={dipped}, inSand={localSand?.InSand}, menu={session.MenuOpen}, cursor={Cursor.lockState}, " +
                $"markerViewport={eye.WorldToViewportPoint(marker.transform.position):F2}, eyeEnabled={eye.enabled}, " +
                $"targetMatched={eye.targetTexture == target}, mask={eye.cullingMask:X8}, rect={eye.rect}, clips={eye.nearClipPlane:F3}/{eye.farClipPlane:F1}");
        }

        static Vector3 FindIslandSupport(CastleGenerator map)
        {
            var floor = map.foundation ? map.foundation.GetComponent<Collider>() : null;
            if (!floor) throw new InvalidOperationException("The Castle map has no foundation collider.");
            var bounds = floor.bounds;
            int mask = 1 | (1 << Layers.Block) | (1 << Layers.Surface);
            foreach (float x in new[] { -.86f, .86f, -.72f, .72f })
            foreach (float z in new[] { -.86f, .86f, -.72f, .72f })
            {
                var point = new Vector3(bounds.center.x + bounds.extents.x * x, map.TopHeight + 5,
                    bounds.center.z + bounds.extents.z * z);
                if (Physics.Raycast(point, Vector3.down, out var hit, point.y - bounds.min.y + 1,
                        mask, QueryTriggerInteraction.Ignore) && hit.collider == floor) return hit.point;
            }
            throw new InvalidOperationException("No bare island support was found outside the tower.");
        }

        static Material FindCoat(Transform visual)
        {
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                    if (material && material.shader && material.shader.name == "DeadlyJenga/Sand Coating") return material;
            return null;
        }

        sealed class HazardFixture : IDisasterState
        {
            public DisasterFrame Capture() => null;
        }

        static void Check(bool condition, string message)
        {
            if (!condition) failures++;
            Log((condition ? "PASS " : "FAIL ") + message);
        }

        static void Log(string line) => File.AppendAllText(Report, line + "\n");

        sealed class Drop
        {
            public readonly Rigidbody Body;
            public readonly Collider Shape;
            public readonly float DryLinear, DryAngular;
            public bool Wet;
            public float EntryBottom, EntryTop, EntrySpeed, StraddlingTime, LastTop;

            public Drop(Rigidbody body)
            {
                Body = body;
                Shape = body.GetComponent<Collider>();
                DryLinear = body.linearDamping;
                DryAngular = body.angularDamping;
            }

            public void Observe()
            {
                if (!Body.gameObject.activeSelf) return;
                var bounds = Shape.bounds;
                LastTop = bounds.max.y;
                if (Mathf.Approximately(Body.linearDamping, DryLinear)) return;

                if (!Wet)
                {
                    Wet = true;
                    EntryBottom = bounds.min.y;
                    EntryTop = bounds.max.y;
                    EntrySpeed = -Body.linearVelocity.y;
                }
                if (bounds.min.y < 0 && bounds.max.y > 0) StraddlingTime += Time.fixedDeltaTime;
            }
        }
    }

    public sealed class BlockWaterContactDriver : MonoBehaviour { }
}
