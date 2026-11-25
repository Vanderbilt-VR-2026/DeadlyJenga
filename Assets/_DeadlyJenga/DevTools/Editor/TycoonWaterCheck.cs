using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Solo Play Mode check of Tycoon's water network and its lazy river slice.
    // Starts stable, then cuts the river, a slide, the river pump, the wheel and the bucket,
    // and checks that water stops, drains or spills where it should. Writes Logs/TycoonWater.
    [InitializeOnLoad]
    public static class TycoonWaterCheck
    {
        const string Key = "TycoonWaterCheck";
        static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
        static int failures;
        static double startAt;
        static string Output => Path.GetFullPath("Logs/TycoonWater");

        static TycoonWaterCheck() { EditorApplication.playModeStateChanged += PlayStateChanged; }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "report.txt"), "Tycoon water network check in Game.unity\n");
            SessionState.SetInt(Key + "Errors", 0);
            SessionState.SetInt(Key + "Exit", 1);
            Select();
            if (MatchSetup.SelectedMap?.mapPrefab == null) throw new InvalidOperationException("Tycoon did not resolve through the lobby catalog.");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            SessionState.SetBool(Key, true);
            Application.logMessageReceived += OnLog;
            EditorApplication.LockReloadAssemblies();
            EditorApplication.EnterPlaymode();
        }

        static void Select()
        {
            MatchSetup.FromLobby = true;
            MatchSetup.StartLocalSession = false;
            MatchSetup.ModeId = "demo";
            MatchSetup.MapId = "mode-tycoon";
            MatchSetup.BotTarget = 1;
            MatchSetup.HandCount = 0;
        }

        static void OnLog(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            // The Unity AI Assistant relay fails offline on test machines; it is not part of the game.
            if (trace != null && trace.Contains("Unity.AI.")) return;
            SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1);
            Report($"ERROR {message}\n{trace}");
        }

        static void PlayStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Application.logMessageReceived -= OnLog;
                Application.logMessageReceived += OnLog;
                Select();
                startAt = EditorApplication.timeSinceStartup + 1.5;
                steps.Clear();
                failures = 0;
                steps.Push(Steps());
                EditorApplication.update += Advance;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                EditorApplication.UnlockReloadAssemblies();
                Application.logMessageReceived -= OnLog;
                EditorApplication.update -= Advance;
                int exit = SessionState.GetInt(Key + "Exit", 1), errors = SessionState.GetInt(Key + "Errors", 0);
                Report($"COMPLETE exit={exit}; errors={errors}");
                if (Application.isBatchMode) EditorApplication.Exit(errors == 0 ? exit : 1);
            }
        }

        static void Advance()
        {
            if (EditorApplication.timeSinceStartup < startAt) return;
            try
            {
                // Run nested waits in place: a yielded IEnumerator runs before its parent continues.
                while (steps.Count > 0)
                {
                    var top = steps.Peek();
                    if (!top.MoveNext()) { steps.Pop(); continue; }
                    if (top.Current is IEnumerator inner) { steps.Push(inner); continue; }
                    return;
                }
                SessionState.SetInt(Key + "Exit", failures == 0 ? 0 : 1);
            }
            catch (Exception error) { Report("FAIL " + error); }
            EditorApplication.update -= Advance;
            int exit = SessionState.GetInt(Key + "Exit", 1), errors = SessionState.GetInt(Key + "Errors", 0);
            Report(exit == 0 && errors == 0 ? "PASS Tycoon water network: stands still and every cut behaves" : $"FAIL Tycoon water network: exit={exit} errors={errors}");
            int code = errors == 0 ? exit : 1;
            if (SessionState.GetString("DJWarm.id", "") != "")
            {
                // An always-open test editor waits for this file instead of Unity closing.
                SessionState.SetBool(Key, false);
                EditorApplication.UnlockReloadAssemblies();
                Application.logMessageReceived -= OnLog;
                Report($"COMPLETE exit={exit}; errors={errors}");
                EditorApplication.ExitPlaymode();
                Directory.CreateDirectory("Logs/Warm");
                File.WriteAllText("Logs/Warm/exit.txt", code.ToString());
                return;
            }
            if (Application.isBatchMode)
            {
                // Leaving Play Mode can stall the batch editor; the report is written, so quit now.
                SessionState.SetBool(Key, false);
                Report($"COMPLETE exit={exit}; errors={errors}");
                EditorApplication.Exit(code);
                return;
            }
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator Wait(float simulatedSeconds)
        {
            float until = Time.time + simulatedSeconds;
            double guard = EditorApplication.timeSinceStartup + simulatedSeconds * 20 + 60;
            while (Time.time < until)
            {
                if (EditorApplication.timeSinceStartup > guard) throw new TimeoutException("Play Mode stopped advancing");
                yield return null;
            }
        }

        static IEnumerator Steps()
        {
            CastleGenerator world = null;
            double deadline = EditorApplication.timeSinceStartup + 60;
            while (true)
            {
                world = Services.World as CastleGenerator;
                if (Services.Session != null && world && world.settings.layoutPreset != TycoonLayout.Preset) { MatchSetup.ApplyMap(); yield return null; continue; }
                if (world && world.Bodies.Count > 0 && world.GetComponentInChildren<TycoonWater>()) break;
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Tycoon with its water network did not start");
                yield return null;
            }
            var water = world.GetComponentInChildren<TycoonWater>();
            var layout = CastleLayout.Build(world.settings);
            var bodies = world.Bodies.ToArray();
            if (layout.Count != bodies.Length) throw new InvalidOperationException("Authored layout and runtime blocks differ");
            var river = water.RiverBodies;
            Report($"Bodies={bodies.Length}; water nodes={water.NodeCount}; river panels={river.Count}; slide lanes={water.Lanes.Count}; pump body={water.RiverPumpBody}");
            if (river.Count < 30 || water.RiverPumpBody < 0) throw new InvalidOperationException("Lazy river loop is missing");
            if (water.Lanes.Count < 4) throw new InvalidOperationException("Expected the summit slides and the raft slide");
            if (!river.All(water.IsFed) || !water.Lanes.SelectMany(l => l).All(water.IsFed))
                throw new InvalidOperationException("Not every river panel and slide panel is fed at rest");
            if (water.ActiveSpills != 0) throw new InvalidOperationException($"{water.ActiveSpills} spills at rest");

            // Wading: a survivor-sized capsule fits in the trough and stands on its floor.
            Physics.SyncTransforms();
            var front = river.OrderBy(i => Vector3.Distance(bodies[i].position, world.transform.TransformPoint(new Vector3(0, 3.3f, -50)))).First();
            var floor = bodies[front].position + Vector3.up * .3f;
            if (!Physics.Raycast(floor + Vector3.up * 1.2f, Vector3.down, out var stand, 2, 1 << Layers.Surface, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("River floor has no survivor surface");
            if (Physics.CheckCapsule(stand.point + Vector3.up * .35f, stand.point + Vector3.up * 1.8f, .3f, 1 << Layers.Surface, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Survivor does not fit inside the river trough");
            Report($"River floor at {stand.point.y - world.transform.position.y:F2} m; wading clearance ok.");

            Capture(world, "01-River-front", new Vector3(14, 16, -78), new Vector3(0, 4, -48));
            Capture(world, "02-Raft-slide", new Vector3(-82, 26, -36), new Vector3(-46, 12, -10));
            Capture(world, "03-River-rear-pump", new Vector3(18, 22, 70), new Vector3(0, 4, 36));

            // Idle: nothing releases, nothing spills.
            var rest = bodies.Select(b => b.position).ToArray();
            yield return Wait(8);
            float drift = 0;
            for (int i = 0; i < bodies.Length; i++)
            {
                drift = Mathf.Max(drift, Vector3.Distance(rest[i], bodies[i].position));
                if (!bodies[i].isKinematic) throw new InvalidOperationException($"Unprovoked release of piece {i} at {layout[i].Position}");
            }
            if (water.ActiveSpills != 0) throw new InvalidOperationException("Spill appeared while idle");
            Report($"Idle 8 s: all {bodies.Length} pieces pinned, peak drift {drift:F4} m, no spills.");

            // 1. Knock a front river panel out. Both cut ends keep flowing from the pump and spill.
            Knock(world, bodies[front], -world.transform.forward * 9 + Vector3.up * 3);
            yield return WaitFor(bodies[front], () => !water.IsIntact(front), 6, "front river panel never left its place");
            water.Solve();
            int k = river.ToList().IndexOf(front);
            int left = river[(k - 1 + river.Count) % river.Count], right = river[(k + 1) % river.Count];
            Report($"River cut: neighbours fed={water.IsFed(left)},{water.IsFed(right)} intact={water.IsIntact(left)},{water.IsIntact(right)}; spills={water.ActiveSpills}");
            if (!water.IsFed(left) || !water.IsFed(right) || water.ActiveSpills < 2)
                Fail("River cut did not spill from both ends");
            yield return Wait(1.5f);
            Capture(world, "04-River-cut-spilling", new Vector3(16, 12, -72), new Vector3(0, 3, -49));

            // 2. Break the green slide mid-run: everything after the cut dries, the panel before spills.
            var green = water.Lanes[2];
            int cut = green[green.Count / 2];
            Knock(world, bodies[cut], Vector3.up * 4 + world.transform.right * 6);
            yield return WaitFor(bodies[cut], () => !water.IsIntact(cut), 6, "slide panel never left its place");
            yield return Wait(2.5f);
            water.Solve();
            var after = green.Skip(green.Count / 2 + 1).Where(water.IsIntact).ToList();
            int before = green[green.Count / 2 - 1];
            Report($"Slide cut: upstream fed={water.IsFed(before)}; downstream intact={after.Count} fed={after.Count(water.IsFed)} wet={after.Count(water.IsWet)}; spills={water.ActiveSpills}");
            if (!water.IsFed(before) || after.Any(water.IsFed) || after.Any(water.IsWet))
                Fail("Slide cut did not dry the downstream run");
            Capture(world, "05-Slide-cut", new Vector3(36, 40, -48), new Vector3(16, 34, -18));

            // 3. Break the river pump: the whole loop loses its supply and drains.
            int pump = water.RiverPumpBody;
            Knock(world, bodies[pump], world.transform.forward * 9 + Vector3.up * 3);
            yield return WaitFor(bodies[pump], () => !water.IsIntact(pump), 6, "pump panel never left its place");
            water.Solve();
            var standing = river.Where(water.IsIntact).ToList();
            if (standing.Any(water.IsFed)) Fail("River still fed after the pump broke");
            yield return Wait(11);
            water.Solve();
            float wettest = standing.Count > 0 ? standing.Max(water.Level) : 0;
            Report($"Pump lost: {standing.Count} standing panels unfed; highest level after 11 s={wettest:F2}; spills={water.ActiveSpills}");
            if (wettest > .1f) Fail("River did not drain after the pump broke");
            Capture(world, "06-River-drained", new Vector3(14, 16, -78), new Vector3(0, 4, -48));

            // 4. Knock the pail wheel off its stand: inlet stops, wheel spins down.
            var wheel = world.GetComponentInChildren<TycoonWheelMotion>();
            int stand_ = Array.IndexOf(bodies, wheel.GetComponentInParent<Rigidbody>());
            Knock(world, bodies[stand_], world.transform.right * 7 + Vector3.up * 2);
            yield return WaitFor(bodies[stand_], () => !water.IsIntact(stand_), 6, "wheel never left its stand");
            yield return Wait(4);
            Report($"Wheel cut: speed={wheel.Speed:F2}; inlet running={wheel.InletRunning}");
            if (wheel.Speed > .5f || wheel.InletRunning) Fail("Wheel kept running without supply");

            // 5. Knock the summit bucket deck: it finishes any dump in progress, then hangs empty.
            var bucket = world.GetComponentInChildren<TycoonBucketMotion>();
            int summit = Array.IndexOf(bodies, bucket.GetComponentInParent<Rigidbody>());
            Knock(world, bodies[summit], world.transform.forward * 6 + Vector3.up * 2);
            yield return WaitFor(bodies[summit], () => !water.IsIntact(summit), 6, "bucket deck never left its place");
            yield return Wait(8);
            Report($"Bucket cut: empty={bucket.Empty}");
            if (!bucket.Empty) Fail("Bucket kept cycling without supply");
            Capture(world, "07-After-cuts-overview", new Vector3(5, 86, -130), new Vector3(0, 32, -9));
        }

        static void Knock(CastleGenerator world, Rigidbody body, Vector3 velocity)
        {
            var magnet = body.GetComponent<MagneticBlock>();
            if (magnet) magnet.Eliminate(world.gameObject);
            body.GetComponent<CastleBlock>().Push(velocity, world.gameObject);
        }

        static IEnumerator WaitFor(Rigidbody body, Func<bool> condition, float seconds, string failure)
        {
            var start = body.position;
            float until = Time.time + seconds;
            double guard = EditorApplication.timeSinceStartup + seconds * 20 + 60;
            while (!condition())
            {
                if (Time.time > until || EditorApplication.timeSinceStartup > guard)
                {
                    Fail(failure);
                    Probe(body, start);
                    yield break;
                }
                yield return null;
            }
        }

        static void Fail(string message)
        {
            failures++;
            Report("FAIL " + message);
        }

        // Why did a knocked piece not move? Log its state and what its colliders overlap.
        static void Probe(Rigidbody body, Vector3 start)
        {
            var magnet = body.GetComponent<MagneticBlock>();
            Report($"  probe {body.name}: kinematic={body.isKinematic} sleeping={body.IsSleeping()} broken={magnet?.Broken} supported={magnet?.Supported} " +
                   $"moved={Vector3.Distance(start, body.position):F3} m velocity={body.linearVelocity.magnitude:F2} constraints={body.constraints} mass={body.mass:F1}");
            var touching = new HashSet<string>();
            foreach (var collider in body.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody != body) continue;
                var box = collider as BoxCollider;
                Collider[] hits = box
                    ? Physics.OverlapBox(box.transform.TransformPoint(box.center), Vector3.Scale(box.size * .5f, box.transform.lossyScale), box.transform.rotation, ~0, QueryTriggerInteraction.Ignore)
                    : Physics.OverlapBox(collider.bounds.center, collider.bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                foreach (var hit in hits)
                {
                    if (hit.attachedRigidbody == body) continue;
                    if (Physics.GetIgnoreLayerCollision(collider.gameObject.layer, hit.gameObject.layer)) continue;
                    if (!Physics.ComputePenetration(collider, collider.transform.position, collider.transform.rotation, hit, hit.transform.position, hit.transform.rotation, out _, out float depth)) continue;
                    touching.Add($"{collider.name}[{LayerMask.LayerToName(collider.gameObject.layer)}] x {hit.name}[{LayerMask.LayerToName(hit.gameObject.layer)}] kin={hit.attachedRigidbody?.isKinematic} depth={depth:F2}");
                }
            }
            foreach (var line in touching.Take(20)) Report("    " + line);
            if (touching.Count == 0) Report("    no overlapping colliders");
        }

        static void Capture(CastleGenerator world, string name, Vector3 from, Vector3 at)
        {
            var source = Services.Session?.ActiveCamera ?? Camera.main;
            var rig = new GameObject("Tycoon water camera");
            var camera = rig.AddComponent<Camera>();
            if (source) camera.CopyFrom(source);
            camera.enabled = false;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = .07f;
            camera.farClipPlane = 1600f;
            camera.fieldOfView = 49;
            camera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 4);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.transform.position = world.transform.TransformPoint(from);
                camera.transform.LookAt(world.transform.TransformPoint(at));
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), pixels.EncodeToPNG());
                Report("Captured " + name);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixels);
                Object.Destroy(rig);
            }
        }

        static void Report(string line)
        {
            File.AppendAllText(Path.Combine(Output, "report.txt"), line + "\n");
            Debug.Log("[TycoonWater] " + line);
        }
    }
}
