using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Cloud City physics pass in real Unity/PhysX, stepped by hand.
    // 1. Stand still: the untouched map settles and every piece stays where it was built.
    // 2. Fall apart: removing a ground floor or a mechanism's support makes what it holds fall.
    // CLI: -executeMethod DeadlyJenga.DevTools.Editor.CloudCityPhysicsChecks.Run
    // Report: the project's Logs/Validation/CloudCityPhysics.txt
    [InitializeOnLoad]
    public static class CloudCityPhysicsChecks
    {
        const string Key = "CloudCityPhysicsChecks.Pending";
        const string MapId = "epic-candy-sky-ring-city";
        const float Step = .02f;
        static readonly StringBuilder report = new();
        static int errors, failures;

        static CloudCityPhysicsChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
                SessionState.SetBool(Key, false);
                EditorApplication.delayCall += Execute;
            };
        }

        [MenuItem("Tools/Deadly Jenga/Validation/Cloud City Physics (stand still + fall apart)")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; report.AppendLine("ERROR " + message); }
        }

        static void Check(bool ok, string what)
        {
            report.AppendLine((ok ? "PASS " : "FAIL ") + what);
            if (!ok) failures++;
        }

        static void Simulate(float seconds)
        {
            for (int i = 0, n = Mathf.RoundToInt(seconds / Step); i < n; i++) Physics.Simulate(Step);
        }

        static string Short(Rigidbody b) => b.name.Length > 6 ? b.name.Substring(7) : b.name;

        static void Execute()
        {
            report.Clear(); errors = failures = 0;
            Application.logMessageReceived += Log;
            var mode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            GameObject root = null;
            try
            {
                var prefab = Resources.Load<GameObject>("ModeMaps/Mode_" + MapId);
                if (!prefab) throw new InvalidOperationException("Cloud City map prefab is missing.");
                root = Object.Instantiate(prefab);
                var map = root.GetComponentInChildren<EpicCityMap>();
                map.Rebuild(); Services.World = map;
                report.AppendLine($"Cloud City physics · Unity {Application.unityVersion} · {map.Blocks.Count} bodies · fixed step {Step}s, manual stepping.");
                Overlaps(map);
                StandStill(map);
                FallApart(map);
                Check(errors == 0, "no runtime errors");
            }
            catch (Exception e) { failures++; report.AppendLine("FAIL " + e); Debug.LogException(e); }
            finally
            {
                Physics.simulationMode = mode;
                Application.logMessageReceived -= Log;
                report.AppendLine($"COMPLETE failures={failures} runtimeErrors={errors}");
                string path = ProjectPath.Report("CloudCityPhysics.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, report.ToString());
                Debug.Log(report.ToString());
                if (root) Object.Destroy(root);
            }
            int exit = failures == 0 ? 0 : 2;
            if (SessionState.GetString("DJWarm.id", "") != "")
            {
                Directory.CreateDirectory("Logs/Warm");
                File.WriteAllText("Logs/Warm/exit.txt", exit.ToString());
                EditorApplication.ExitPlaymode();
            }
            else if (Application.isBatchMode) EditorApplication.Exit(exit);
            else EditorApplication.ExitPlaymode();
        }

        // ---------- 0. pieces built inside each other (they pop apart on the first step) ----------
        static void Overlaps(EpicCityMap map)
        {
            Physics.SyncTransforms();
            var found = new List<(float depth, string text)>();
            var seen = new HashSet<(int, int)>();
            var hits = new Collider[256];
            foreach (var body in map.Blocks.Where(b => b))
                foreach (var a in body.GetComponentsInChildren<Collider>())
                {
                    if (!a.enabled || a.isTrigger) continue;
                    var bb = a.bounds;
                    int n = Physics.OverlapBoxNonAlloc(bb.center, bb.extents, hits, Quaternion.identity, 1 << Layers.Block, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        var c = hits[i]; var other = c.attachedRigidbody;
                        if (!other || other == body || Physics.GetIgnoreCollision(a, c)) continue;
                        var key = a.GetInstanceID() < c.GetInstanceID() ? (a.GetInstanceID(), c.GetInstanceID()) : (c.GetInstanceID(), a.GetInstanceID());
                        if (!seen.Add(key)) continue;
                        if (!Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, c, c.transform.position, c.transform.rotation, out var dir, out float depth) || depth < .12f) continue;
                        found.Add((depth, $"  OVERLAP {depth:F2} m dir {dir.x:F1},{dir.y:F1},{dir.z:F1} at {bb.center.x:F1},{bb.center.y:F1},{bb.center.z:F1} · {Short(body)} [{a.name}] <-> {Short(other)} [{c.name}]"));
                    }
                }
            foreach (var f in found.OrderByDescending(f => f.depth).Take(60)) report.AppendLine(f.text);
            Check(found.Count == 0, $"no two parts that still collide start inside each other by 12 cm or more ({found.Count} overlaps)");
        }

        // ---------- 1. stand still ----------
        static void StandStill(EpicCityMap map)
        {
            var bodies = map.Blocks.Where(b => b).ToArray();
            var start = bodies.Select(b => b.position).ToArray();
            var startRot = bodies.Select(b => b.rotation).ToArray();
            Simulate(1);
            var early = bodies.Select((b, i) => (b, d: Vector3.Distance(b.position, start[i]))).Where(x => x.d > .05f).OrderByDescending(x => x.d).ToArray();
            foreach (var (b, d) in early.Take(40)) report.AppendLine($"  first-second jump {d:F2} m · {Short(b)}");
            Simulate(11);
            int moved = 0, moving = 0;
            foreach (var (b, i) in bodies.Select((b, i) => (b, i)))
            {
                float d = Vector3.Distance(b.position, start[i]), turn = Quaternion.Angle(b.rotation, startRot[i]);
                bool drift = d > .15f || turn > 3, live = !b.isKinematic && (b.linearVelocity.magnitude > .05f || b.angularVelocity.magnitude > .05f);
                if (drift) moved++;
                if (live) moving++;
                if (drift || live)
                    report.AppendLine($"  QUIET MOVER {Short(b)}: moved {d:F2} m, turned {turn:F1}°, speed {b.linearVelocity.magnitude:F2} m/s, y {start[i].y:F1}->{b.position.y:F1}");
            }
            var fell = bodies.Where(b => b.position.y < map.Center.y - 5).ToArray();
            Check(fell.Length == 0, $"no piece falls off the island while untouched ({fell.Length} fell)");
            Check(moved == 0, $"untouched map stands still for 12 s: every piece within 15 cm and 3° ({moved} moved)");
            Check(moving == 0, $"untouched map comes to rest ({moving} still moving after 12 s)");
            Check(bodies.Where(b => b.isKinematic).All(b => b.name.Contains(" · Fixed ")), "only \"Fixed\" pieces are held still; everything else is free to fall");
        }

        // ---------- 2. fall apart ----------
        static Rigidbody[] Named(EpicCityMap map, string part) => map.Blocks.Where(b => b && b.name.Contains(part)).ToArray();

        static void Reset(EpicCityMap map) { map.Rebuild(); Simulate(2); }

        // Remove "targets" (as if pulled out), then check "dependents" drop by at least "drop" metres.
        static void KnockOut(EpicCityMap map, string label, Func<Rigidbody[]> targets, Func<Rigidbody[]> dependents, float drop = .8f)
        {
            Reset(map);
            var t = targets(); var d = dependents();
            if (t.Length == 0 || d.Length == 0) { Check(false, $"{label}: pieces found ({t.Length} targets, {d.Length} dependents)"); return; }
            var before = d.Select(b => b.worldCenterOfMass.y).ToArray();
            foreach (var b in t) b.gameObject.SetActive(false);
            foreach (var b in d) b.WakeUp();
            Simulate(5);
            float most = d.Select((b, i) => before[i] - b.worldCenterOfMass.y).Max();  // centre of mass: hinged pieces swing about their origin
            Check(most >= drop, $"{label}: removing {t.Length} support(s) drops what they hold by {most:F2} m");
        }

        static void FallApart(EpicCityMap map)
        {
            // Every building: shove its first floor out; a floor above must come down.
            var sites = map.Blocks.Where(b => b && b.name.Contains(" floor ")).GroupBy(b => b.name.Split('·')[1].Trim()).OrderBy(g => g.Key);
            foreach (var site in sites)
            {
                string name = site.Key;
                KnockOut(map, name, () => Named(map, " · " + name + " · ").Where(b => b.name.EndsWith(" floor 1")).ToArray(),
                    () => Named(map, " · " + name + " · ").Where(b => b.name.Contains(" floor ") && !b.name.EndsWith(" floor 1")).ToArray());
            }
            KnockOut(map, "Drop-stop shoes", () => Named(map, "Removable drop shoe"), () => Named(map, "Two-floor dropping cassette"), .5f);
            KnockOut(map, "Keystone vault", () => Named(map, "Loose arch keystone"), () => Named(map, "Vault terrace deck"), .5f);
            KnockOut(map, "South falling route", () => Named(map, "Removable support"), () => Named(map, "Falling route"), .5f);
            KnockOut(map, "Bascule counterweights", () => Named(map, "Removable heel counterweight"), () => Named(map, "Counterweighted bascule leaf"), .3f);
            KnockOut(map, "Book wing foot", () => Named(map, "receiving foot"), () => Named(map, "Hinged inhabited book wing"), .5f);
            Reset(map);
        }
    }
}
