using System;
using System.Collections;
using System.IO;
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
        const string SandKey = RunKey + "Sand";
        static string Report => ProjectPath.Report(SessionState.GetBool(SandKey, false) ? "BlockSandContact.txt" : "BlockWaterContact.txt");
        static int failures;

        static BlockWaterContactChecks() => EditorApplication.playModeStateChanged += OnPlayMode;

        public static void Run() => Begin(false);
        public static void RunSand() => Begin(true);

        static void Begin(bool sand)
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run in a reserved batch editor.");

            SessionState.SetBool(SandKey, sand);
            File.WriteAllText(Report, "Unity Play Mode, production Castle block and contact components, isolated contact geometry.\n");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
            var checks = SessionState.GetBool(SandKey, false) ? SandChecks() : Checks();
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
            SessionState.EraseBool(SandKey);
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void RecordError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
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

        static Color RenderMarker(Camera camera, RenderTexture target)
        {
            var previous = RenderTexture.active;
            var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixel.ReadPixels(new Rect(32, 32, 1, 1), 0, 0);
                return pixel.GetPixel(0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(pixel);
            }
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
