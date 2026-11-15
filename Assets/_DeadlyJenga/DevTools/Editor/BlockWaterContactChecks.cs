using System;
using System.Collections;
using System.IO;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
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
        static string Report => ProjectPath.Report("BlockWaterContact.txt");
        static int failures;

        static BlockWaterContactChecks() => EditorApplication.playModeStateChanged += OnPlayMode;

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run in a reserved batch editor.");

            File.WriteAllText(Report, "Unity Play Mode, production CastleGenerator and CastleBlock prefab, isolated contact geometry.\n");
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
            var checks = Checks();
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
