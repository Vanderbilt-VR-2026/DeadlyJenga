using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Play Mode check of the Blender physics import of Gingerbread Kingdom:
    // untouched settle, collapse under gameplay pushes and pulls, spawn/respawn, climbing and rendered materials.
    // Run: DeadlyJenga.DevTools.Editor.GingerbreadBlenderChecks.Run
    [InitializeOnLoad]
    public static class GingerbreadBlenderChecks
    {
        const string Key = "GingerbreadBlenderChecks.Active", Folder = "Logs/Captures/GingerbreadBlender";
        static int phase;
        static float clock;
        static double deadline;
        static GingerbreadKingdom map;
        static Vector3[] initial;
        static Camera camera;
        static string report;
        static bool valid;
        static Rigidbody benTop, keepTop;
        static Vector3 benTopBefore, keepTopBefore;
        static int jointsBefore;
        static GingerbreadPiece pulled;
        static Vector3 grab;
        static List<GingerbreadPiece> pullList;
        static int pullIndex, shots;
        static Vector3 benBase, keepBase;

        static GingerbreadBlenderChecks() { EditorApplication.playModeStateChanged += State; }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode");
            Directory.CreateDirectory(Folder);
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                GingerbreadKingdom.UseBlenderPhysicsTown = true;
                MatchSetup.MapId = "mode-gingerbread-kingdom"; MatchSetup.ModeId = "demo"; MatchSetup.FromLobby = false;
                phase = 0; valid = true; report = ""; deadline = EditorApplication.timeSinceStartup + 300;
                EditorApplication.update += Step;
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            GingerbreadKingdom.UseBlenderPhysicsTown = false;
            SessionState.EraseBool(Key);
            int code = SessionState.GetInt(Key + ".result", 2);
            if (string.IsNullOrEmpty(SessionState.GetString("DJWarm.id", ""))) EditorApplication.Exit(code);
            else { Directory.CreateDirectory("Logs/Warm"); File.WriteAllText("Logs/Warm/exit.txt", code.ToString()); }
        }

        static void Check(bool ok, string label) { report += (ok ? "PASS " : "FAIL ") + label + "\n"; valid &= ok; }
        static void Note(string line) => report += line + "\n";

        static void End()
        {
            File.WriteAllText(Folder + "/checks.txt", report);
            Debug.Log("GingerbreadBlenderChecks\n" + report);
            SessionState.SetInt(Key + ".result", valid ? 0 : 2);
            EditorApplication.update -= Step;
            EditorApplication.ExitPlaymode();
        }

        static void Shot(string name, Vector3 eye, Vector3 look, float fov)
        {
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye)); camera.fieldOfView = fov;
            string path = Path.GetFullPath(Folder + "/" + name + ".png");
            MapLiveCapture.RenderNow(camera, 1440, 960, path);
            Debug.Log("CAPTURE FILE " + path);
        }

        static void Street(string name) => Shot(name, new Vector3(-4.8f, 1.6f, -33.2f), new Vector3(-6, 6, -5), 70);
        static void BigBen(string name) => Shot(name, new Vector3(-3, 9, -44), new Vector3(-9, 22, -14), 62);
        static void Castle(string name) => Shot(name, new Vector3(8.6f, 9, -2), new Vector3(8.6f, 14, 33), 60);
        static void Overview(string name) => Shot(name, new Vector3(22, 30, -78), new Vector3(2, 8, 0), 55);

        static IEnumerable<GingerbreadPiece> Pieces => map.Blocks.Select(b => b.GetComponent<GingerbreadPiece>()).Where(p => p);

        static void Step()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("phase " + phase + " after " + (Time.fixedTime - clock) + " s of physics");
                if (Services.Session?.SurvivorBots == null) return;
                if (phase == 0)
                {
                    Services.Session.HandBots?.Clear(); Services.Session.SurvivorBots.AutoFill = false; Services.Session.SurvivorBots.Clear();
                    MatchSetup.ApplyMap(); map = Services.World as GingerbreadKingdom;
                    if (!map) throw new InvalidOperationException("Game did not select Gingerbread Kingdom");
                    if (!map.BuiltFromBlender) map.Rebuild();
                    Check(map.BuiltFromBlender, "map built from the Blender physics files");
                    initial = map.Blocks.Select(b => b.position).ToArray();
                    jointsBefore = map.GetComponentsInChildren<FixedJoint>().Length;
                    Note("Bodies " + initial.Length + ", box shapes " + map.GetComponentsInChildren<BoxCollider>().Length +
                         ", hull shapes " + map.GetComponentsInChildren<MeshCollider>().Count(c => c.convex) + ", glue joints " + jointsBefore + ", starting overlaps ignored " + map.StartingOverlapCount);
                    Check(initial.Length > 500, "imported body count " + initial.Length);
                    camera = new GameObject("Gingerbread review camera").AddComponent<Camera>();
                    camera.enabled = false; camera.farClipPlane = 500; camera.nearClipPlane = .05f;
                    deadline = EditorApplication.timeSinceStartup + 300;
                    clock = Time.fixedTime; phase++; return;
                }
                float dt = Time.fixedTime - clock;
                if (phase == 1 && dt > .05f) { Street("01-authored-street"); phase++; return; }
                if (phase == 2 && dt > 10f) { Settled(); clock = Time.fixedTime; phase++; return; }
                if (phase == 3) { if (Pull(dt)) { clock = Time.fixedTime; phase++; } return; }
                if (phase == 4) { if (dt >= shots * .5f) { Blasts(); shots++; } if (shots >= 6) { clock = Time.fixedTime; phase++; } return; }
                if (phase == 5) { Quake(); clock = Time.fixedTime; phase++; return; }
                if (phase == 6 && dt > 14f) { Collapsed(); Respawn(); End(); }
            }
            catch (Exception e)
            {
                Check(false, "exception: " + e.Message);
                Debug.LogException(e);
                End();
            }
        }

        static void Settled()
        {
            var bodies = map.Blocks;
            var travel = bodies.Select((b, i) => (b, d: Vector3.Distance(b.position, initial[i]), drop: initial[i].y - b.position.y)).ToList();
            int fell = travel.Count(t => t.drop > .5f);
            float worst = travel.Max(t => t.d);
            float median = travel.Select(t => t.d).OrderBy(d => d).ElementAt(travel.Count / 2);
            Check(fell == 0, "untouched 10 s: bodies dropped over 0.5 m = " + fell);
            Check(worst < 1f, "untouched 10 s: maximum body travel " + worst.ToString("F3") + " m (median " + median.ToString("F4") + ")");
            foreach (var t in travel.OrderByDescending(t => t.d).Take(8)) Note("  moved " + t.d.ToString("F3") + "  " + t.b.name);
            int joints = map.GetComponentsInChildren<FixedJoint>().Length;
            Check(joints >= jointsBefore * .97f, "untouched 10 s: glue joints kept " + joints + " of " + jointsBefore);
            Street("02-settled-street"); BigBen("03-settled-bigben"); Castle("04-settled-castle"); Overview("05-settled-overview");
            Materials();
            Spawn("spawn", map.SafeSpawnPoint());
            Climb("town house wall", Pieces.Where(p => !p.name.StartsWith("Big Ben") && p.transform.parent.name.EndsWith("Town")), 1.0f);
            Climb("castle wall", Pieces.Where(p => p.transform.parent.name.EndsWith("Castle")), 1.0f);
            Climb("Big Ben base", Pieces.Where(p => p.name.StartsWith("Big Ben")), 1.5f);
            // Collapse: a hand pulls out Big Ben's lowest pieces, then pushes knock the castle keep's base.
            var ben = Pieces.Where(p => p.name.StartsWith("Big Ben")).ToList();
            benTop = ben.OrderByDescending(p => p.Body.position.y).First().Body; benTopBefore = benTop.position;
            var keep = Pieces.Where(p => p.name.StartsWith("Great keep")).ToList();
            keepTop = keep.Count > 0 ? keep.OrderByDescending(p => p.Body.position.y).First().Body : null;
            if (keepTop) keepTopBefore = keepTop.position;
            pullList = ben.OrderBy(p => p.Body.worldCenterOfMass.y).Take(4).ToList();
            pullIndex = 0; pulled = null; shots = 0;
            benBase = ben.OrderBy(p => p.Body.worldCenterOfMass.y).First().Body.worldCenterOfMass;
            benBase.y = 1.2f; benBase += Vector3.back * 2.5f;
            keepBase = keep.Count > 0 ? keep.OrderBy(p => p.Body.worldCenterOfMass.y).First().Body.worldCenterOfMass : benBase;
            keepBase += Vector3.back * 2.5f;
            jointsBefore = map.GetComponentsInChildren<FixedJoint>().Length;
        }

        static bool Pull(float dt)
        {
            if (pulled == null)
            {
                if (pullIndex >= pullList.Count)
                {
                    return true;
                }
                pulled = pullList[pullIndex]; grab = pulled.Body.worldCenterOfMass; clock = Time.fixedTime;
                Check(pulled.TryStartGrab(camera.gameObject, grab), "hand grabs Big Ben support " + pulled.name);
                return false;
            }
            pulled.Hold(grab + Vector3.back * Mathf.Min(6, dt * 4));
            if (dt < 1.5f) return false;
            pulled.Release();
            Note("  pulled " + pulled.name + " moved " + Vector3.Distance(grab, pulled.Body.worldCenterOfMass).ToString("F2") + " m");
            pulled = null; pullIndex++; clock = Time.fixedTime;
            return false;
        }

        // One blaster shot: the same push and falloff MatchModeRuntime.Blaster gives every block in range.
        static void Blast(Vector3 point, float radius)
        {
            foreach (var p in Pieces)
            {
                var delta = p.Body.worldCenterOfMass - point; float distance = delta.magnitude;
                if (distance > radius) continue;
                p.Push((delta.normalized + Vector3.up * .2f) * Mathf.Lerp(4, 1, distance / radius), camera.gameObject);
            }
        }
        // A player shooting a tower aims along its height, so Big Ben takes shots at 1, 12 and 22 m.
        static void Blasts()
        {
            Blast(benBase, 6); Blast(keepBase, 6);
            Blast(new Vector3(benBase.x, 12, benBase.z), 6); Blast(new Vector3(benBase.x, 22, benBase.z), 6);
        }

        // The match's own earthquake disaster, launched the way the demo menu launches it.
        static void Quake()
        {
            var demo = Services.Session.Demo;
            int index = -1;
            for (int i = 0; demo != null && i < demo.DisasterCount; i++)
                if (demo.DisasterLabel(i).IndexOf("quake", StringComparison.OrdinalIgnoreCase) >= 0) { index = i; break; }
            Check(index >= 0, "earthquake disaster available");
            if (index >= 0) Note("  launched " + demo.DisasterLabel(index) + ": " + demo.LaunchDisaster(index));
        }

        static void Collapsed()
        {
            float benDrop = benTopBefore.y - benTop.position.y;
            Check(benDrop > 1f, "Big Ben top falls after hand pulls, 6 blaster volleys up the tower and an earthquake: drop " + benDrop.ToString("F2") + " m");
            if (keepTop)
            {
                float keepDrop = keepTopBefore.y - keepTop.position.y;
                Check(keepDrop > 1f, "castle keep top falls after 6 blaster shots and an earthquake: drop " + keepDrop.ToString("F2") + " m");
            }
            else Check(false, "castle keep pieces present");
            int joints = map.GetComponentsInChildren<FixedJoint>().Length;
            Check(joints < jointsBefore, "glue joints broke in the collapse: " + (jointsBefore - joints));
            int moved = map.Blocks.Select((b, i) => Vector3.Distance(b.position, initial[i])).Count(d => d > .5f);
            Note("Bodies moved over 0.5 m after collapse: " + moved);
            BigBen("06-collapse-bigben"); Castle("07-collapse-castle"); Overview("08-collapse-overview");
        }

        static void Respawn()
        {
            map.Rebuild();
            Physics.SyncTransforms();
            Check(map.BuiltFromBlender && map.Blocks.Count == initial.Length, "rebuild restores " + map.Blocks.Count + " bodies");
            Spawn("respawn after rebuild", map.SafeSpawnPoint());
        }

        static void Spawn(string label, Vector3 at)
        {
            bool floor = Physics.SphereCast(at + Vector3.up * .6f, .2f, Vector3.down, out var hit, 1.2f, ~(1 << Layers.Survivor), QueryTriggerInteraction.Ignore) && hit.normal.y > .8f;
            Check(floor, label + ": flat floor under " + at + (floor ? " (" + hit.collider.name + ")" : ""));
            bool clear = !Physics.CheckCapsule(at + Vector3.up * .45f, at + Vector3.up * 1.6f, .3f, ~(1 << Layers.Survivor), QueryTriggerInteraction.Ignore);
            Check(clear, label + ": standing room at spawn");
            var survivor = Services.Session.Player?.gameObject.GetComponent<CastleSurvivor>();
            if (!survivor) { Check(false, label + ": desktop survivor present"); return; }
            survivor.Place(at, Quaternion.identity);
            survivor.externallyDriven = true;
            for (int i = 0; i < 40; i++) survivor.Simulate(Vector2.up, false, .02f);
            var p = survivor.transform.position;
            Check(p.y > map.WaterHeight(p) + 3 && survivor.Grounded, label + ": survivor walks and stays grounded at " + p);
            survivor.externallyDriven = false;
        }

        static void Climb(string label, IEnumerable<GingerbreadPiece> candidates, float needRise)
        {
            var survivor = Services.Session.Player?.gameObject.GetComponent<CastleSurvivor>();
            if (!survivor) { Check(false, label + ": survivor present"); return; }
            // Tall box faces that start near the street and face the open town.
            var faces = candidates.SelectMany(p => p.Shapes.OfType<BoxCollider>().Select(c => (p, c)))
                .Where(x => x.c.bounds.min.y < 1.2f && x.c.bounds.size.y > 1.2f)
                .OrderBy(x => Vector3.Distance(x.c.bounds.center, map.SafeSpawnPoint())).Take(40).ToList();
            float best = 0; string where = "none"; int hits = 0, tries = 0, latches = 0;
            survivor.InputSuspended = false;
            foreach (var (piece, col) in faces)
            {
                foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    var start = new Vector3(col.bounds.center.x, col.bounds.min.y + .9f, col.bounds.center.z) - dir * 8;
                    if (!col.Raycast(new Ray(start, dir), out var hit, 10) || Mathf.Abs(hit.normal.y) > .3f) continue;
                    hits++;
                    var outward = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                    var feet = hit.point + outward * (survivor.Capsule.radius + .065f) - Vector3.up * (survivor.Height * .64f);
                    if (Physics.Raycast(feet + Vector3.up * .5f, Vector3.down, out var floor, 2f, ~((1 << Layers.Survivor) | (1 << Layers.Surface)), QueryTriggerInteraction.Ignore))
                        feet.y = Mathf.Max(feet.y, floor.point.y);
                    tries++;
                    survivor.Place(feet, Quaternion.LookRotation(-outward));
                    bool latched = false; float top = feet.y;
                    for (int i = 0; i < 300; i++)
                    {
                        survivor.Simulate(Vector2.up, false, .02f, true);
                        latched |= survivor.Climbing;
                        top = Mathf.Max(top, survivor.transform.position.y);
                    }
                    if (latched) latches++;
                    if (latched && top - feet.y > best) { best = top - feet.y; where = piece.name; }
                    if (best >= needRise) break;
                }
                if (best >= needRise) break;
            }
            Note("  " + label + ": " + faces.Count + " faces, " + hits + " wall hits, " + tries + " tries, " + latches + " latched");
            Check(best >= needRise, label + ": survivor latched and climbed " + best.ToString("F2") + " m on " + where);
            // Climbing moves pieces slightly; put the survivor back on the street.
            survivor.Place(map.SafeSpawnPoint(), Quaternion.identity);
        }

        static void Materials()
        {
            var renderers = map.GetComponentsInChildren<MeshRenderer>();
            int bad = renderers.Count(r => r.sharedMaterials.Any(m => !m || !m.shader || !m.shader.isSupported || m.shader.name.Contains("Error")));
            int empty = map.GetComponentsInChildren<MeshFilter>().Count(f => !f.sharedMesh || f.sharedMesh.vertexCount == 0);
            Check(renderers.Length > 900 && bad == 0 && empty == 0, "rendered materials: " + renderers.Length + " renderers, " + bad + " bad materials, " + empty + " empty meshes");
            var finishes = renderers.SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.name).Distinct().ToArray();
            Note("Finishes in use: " + string.Join(", ", finishes));
        }
    }
}
