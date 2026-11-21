using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // A scout, not a trailer: can the Studio record a crowd, and can a driven hero sprint, leap and shoot in the air?
    //   - about 40 players near the top of the map, all the game's own bots (scout 1 compared them with scripted pairs);
    //   - one hero who plans his own route of runs and jumps on the loaded map, and fires in the air on every jump.
    // Run: DeadlyJenga.DevTools.Editor.TrailerStudio.CrowdRunScout.Run
    // Output: Logs/ClaudeCrowdRunScout/ (notes.md, hero.csv, crowd.csv, shots/*.mp4).
    [InitializeOnLoad]
    public static class CrowdRunScout
    {
        const int Fps = 30, Crowd = 40;
        const float Length = 9f, RunAt = 1.2f;
        const string Key = "DeadlyJenga.CrowdRunScout", SetId = "crowd-run-scout";

        static int Step { get => SessionState.GetInt(Key, 0); set => SessionState.SetInt(Key, value); }
        static double Deadline { get => SessionState.GetFloat(Key + ".Deadline", 0); set => SessionState.SetFloat(Key + ".Deadline", (float)value); }
        static string Failure { get => SessionState.GetString(Key + ".Failure", ""); set => SessionState.SetString(Key + ".Failure", value); }

        static IEnumerator<int> routine;
        static int lastTick = -1;
        static readonly StringBuilder notes = new StringBuilder();

        public static string Folder => Path.Combine(StudioLibrary.ProjectRoot, "Logs", "ClaudeCrowdRunScout");

        static CrowdRunScout() { if (Step != 0) Hook(); }
        static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

        public static void Run()
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            notes.Clear();
            string tapes = StudioLibrary.TapeFolder(SetId);
            if (Directory.Exists(tapes)) Directory.Delete(tapes, true);
            Failure = ""; Step = 1;
            Deadline = EditorApplication.timeSinceStartup + 240;
            Hook();
        }

        static void Note(string line)
        {
            notes.AppendLine(line);
            Debug.Log("[CrowdRunScout] " + line);
            try { File.WriteAllText(Path.Combine(Folder, "notes.md"), notes.ToString()); } catch { }
        }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                switch (Step)
                {
                    case 1:
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        StudioLibrary.SetsFolderOverride = Path.Combine(Folder, "sets");
                        StudioRun.OpenStage(NewSet());
                        Step = 2; Deadline = EditorApplication.timeSinceStartup + 570;
                        break;
                    case 2:
                        if (routine == null)
                        {
                            if (StudioRun.State == StudioState.Held) routine = Shoot().GetEnumerator();
                            else if (EditorApplication.timeSinceStartup > Deadline) throw new TimeoutException("The stage never opened: " + StudioRun.Status);
                            return;
                        }
                        if (EditorApplication.timeSinceStartup > Deadline) { StudioRender.Cancel(); throw new TimeoutException("Out of time inside the stage."); }
                        if (Time.frameCount == lastTick) return;
                        lastTick = Time.frameCount;
                        if (routine.MoveNext()) return;
                        routine = null;
                        Step = 3;
                        StudioRun.CloseStage();
                        break;
                    case 3:
                        if (EditorApplication.isPlayingOrWillChangePlaymode || StudioRun.State != StudioState.Closed) return;
                        Finish();
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Failure = error.Message;
                Note("FAIL stopped at step " + Step + ": " + error);
                routine = null;
                if (StudioRun.State != StudioState.Closed && Step < 3) { Step = 3; StudioRun.CloseStage(); }
                else Finish();
            }
        }

        static void Finish()
        {
            bool failed = Failure.Length > 0;
            Step = 0;
            StudioLibrary.SetsFolderOverride = "";
            EditorApplication.update -= Tick;
            Note(failed ? "FAIL crowd run scout: " + Failure : "PASS crowd run scout finished, output in " + Folder);
            if (SessionState.GetString("DJWarm.id", "") != "")
            {
                Directory.CreateDirectory(Path.Combine("Logs", "Warm"));
                File.WriteAllText(Path.Combine("Logs", "Warm", "exit.txt"), failed ? "1" : "0");
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                return;
            }
            bool startedForThis = Environment.GetCommandLineArgs().Any(a => a.Contains("CrowdRunScout.Run"));
            if (Application.isBatchMode || startedForThis) EditorApplication.Exit(failed ? 1 : 0);
        }

        static StudioSet NewSet()
        {
            var set = new StudioSet
            {
                id = SetId, title = "Crowd run scout", map = "castle", fps = Fps, length = Length, seed = 2031,
                handBot = false, notes = "Made by CrowdRunScout. Safe to delete."
            };
            StudioLibrary.Save(set);
            return set;
        }

        static IEnumerable<int> UntilHeld()
        {
            float started = Time.realtimeSinceStartup;
            while (StudioRun.State != StudioState.Held)
            {
                if (StudioRun.State == StudioState.Closed) throw new InvalidOperationException("The stage closed: " + StudioRun.Status);
                if (Time.realtimeSinceStartup - started > 300) throw new TimeoutException("A run took more than five minutes: " + StudioRun.Status);
                yield return 0;
            }
        }

        // ---- Geometry helpers ----

        static string V(Vector3 v) => v.ToString("F2");
        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized; }
        static Vector3 Flat0(Vector3 v) { v.y = 0; return v; }
        static int Ground => (1 << Layers.Block) | (1 << Layers.Surface) | 1;
        static int Solid => (1 << Layers.Block) | (1 << Layers.Surface);
        static bool Clear(Vector3 at, float r = .35f) => !Physics.CheckSphere(at, r, Ground, QueryTriggerInteraction.Ignore);
        static bool Sees(Vector3 from, Vector3 to) => !Physics.Linecast(from, to, Solid, QueryTriggerInteraction.Ignore);
        static float TopOf(Rigidbody body) => body.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).Select(c => c.bounds.max.y).DefaultIfEmpty(body.position.y).Max();
        static string P(StudioStage stage, Vector3 world) => StudioTargets.Point(stage.ToStage(world));

        // The surface under 'p', looking from 1 m above height 'y' to 5 m below it.
        static bool GroundAt(Vector3 p, float y, out float found)
        {
            found = 0;
            if (!Physics.Raycast(new Vector3(p.x, y + 1f, p.z), Vector3.down, out var hit, 6f, Ground, QueryTriggerInteraction.Ignore)) return false;
            found = hit.point.y;
            return true;
        }

        // Somewhere a survivor can stand at height 'y'.
        static bool Level(Vector3 p, float y)
        {
            if (!GroundAt(p, y, out float found) || Mathf.Abs(found - y) > .12f) return false;
            Vector3 feet = new Vector3(p.x, y, p.z);
            return !Physics.CheckCapsule(feet + Vector3.up * .45f, feet + Vector3.up * 1.35f, .22f, Solid, QueryTriggerInteraction.Ignore);
        }

        static List<Vector3> FindSpots(StudioStage stage, out float top)
        {
            var spots = new List<Vector3>();
            var blocks = stage.blocks.Where(b => b && b.gameObject.activeInHierarchy).ToList();
            var tops = blocks.ToDictionary(b => b, TopOf);
            float highest = top = tops.Values.Max();
            foreach (var body in blocks.Where(b => tops[b] > highest - 12f))
            {
                var bs = StudioHandDirection.BlockSize(body);
                Vector3 longLocal = bs.x >= bs.z ? Vector3.right : Vector3.forward, wideLocal = bs.x >= bs.z ? Vector3.forward : Vector3.right;
                float length = Mathf.Max(bs.x, bs.z), width = Mathf.Min(bs.x, bs.z);
                for (float t = -length / 2 + .45f; t <= length / 2 - .45f + .001f; t += .6f)
                    foreach (float w in width >= 1.5f ? new[] { -.45f, 0, .45f } : new[] { 0f })
                    {
                        Vector3 p = body.position + body.rotation * (longLocal * t + wideLocal * w);
                        p.y = tops[body];
                        if (Level(p, p.y)) spots.Add(p);
                    }
            }
            Note($"Castle top y {top:0.00}. Free standing spots in the top 12 m: {spots.Count}.");
            return spots;
        }

        // ---- The hero's route ----
        // The hero finds his own way on whatever map is loaded: straight runs joined by jumps. Each jump is flown
        // here first with his real jump (measured in scout 1: 4.9 m/s run, 1.9 m high, 0.86 s in the air on the flat),
        // so he only jumps where he will land. This is the Studio's own planner; the game's bot navigation is not used.
        const float RunSpeed = 4.9f, JumpSpeed = 8.9f, Gravity = 20.7f, RouteTarget = 34f;

        sealed class Leg { public Vector3 from, takeoff, land, dir; }
        sealed class Route { public List<Leg> legs = new List<Leg>(); public Vector3 start, tail, dir; public float length; }

        static Route bestRoute; static int budget;

        static bool SimJump(Vector3 takeoff, Vector3 d, out Vector3 land)
        {
            land = takeoff;
            Vector3 up = Vector3.up, prev = takeoff + up * .12f;
            for (float t = .05f; t < 1.9f; t += .05f)
            {
                Vector3 p = takeoff + d * RunSpeed * t + up * (JumpSpeed * t - .5f * Gravity * t * t + .12f);
                if (p.y < takeoff.y - 3.6f) return false;
                if (Physics.Linecast(prev + up * 1.4f, p + up * 1.4f, Solid, QueryTriggerInteraction.Ignore)) return false;   // his head would hit something
                if (Physics.Linecast(prev, p, out var hit, Ground, QueryTriggerInteraction.Ignore))
                {
                    if (JumpSpeed - Gravity * t > 0 || hit.normal.y < .7f) return false;   // into a wall
                    land = hit.point;
                    return true;
                }
                prev = p;
            }
            return false;
        }

        // No floor somewhere between two points: a real gap, not a hop on the flat.
        static bool GapBetween(Vector3 a, Vector3 b)
        {
            float distance = Flat0(b - a).magnitude;
            for (float s = .3f; s < distance - .2f; s += .3f)
            {
                Vector3 p = Vector3.Lerp(a, b, s / distance);
                if (!GroundAt(p, a.y, out float f) || f < a.y - .6f) return true;
            }
            return false;
        }

        static void Extend(Vector3 p, Vector3 d, List<Leg> legs, float length, Vector3 start, Vector3 startDir)
        {
            int n = 0;
            while (n < 22 && Level(p + d * (n + 1) * .4f, p.y)) n++;
            float walk = n * .4f;
            float total = length + Mathf.Min(walk, 4f);
            if (legs.Count > 0 && (bestRoute == null || total > bestRoute.length))
                bestRoute = new Route { legs = new List<Leg>(legs), start = start, tail = p + d * Mathf.Min(walk, 4f), length = total, dir = startDir };
            if (legs.Count >= 8 || length >= RouteTarget || budget <= 0) return;
            float minRun = legs.Count == 0 ? 2.4f : 1.2f;
            int tried = 0;
            for (float s = walk; s >= minRun - .001f && tried < 4; s -= .4f)
            {
                budget--;
                Vector3 takeoff = p + d * s;
                if (!SimJump(takeoff, d, out var land)) continue;
                if (!Level(land, land.y) || !Level(land + d * .7f, land.y)) continue;
                if (Mathf.Abs(land.y - takeoff.y) < .4f && !GapBetween(takeoff, land)) continue;
                // Never back onto ground this route has already used (no hopping to and fro between two blocks).
                if (legs.Any(l => Vector3.Distance(l.land, land) < 2.5f || Vector3.Distance(l.takeoff, land) < 2.5f || Vector3.Distance(l.from, land) < 2f)) continue;
                tried++;
                legs.Add(new Leg { from = p, takeoff = takeoff, land = land, dir = d });
                float add = s + Flat0(land - takeoff).magnitude;
                foreach (float turn in new[] { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 90f, -90f, 130f, -130f, 180f })
                {
                    Extend(land, Quaternion.Euler(0, turn, 0) * d, legs, length + add, start, startDir);
                    if (bestRoute != null && bestRoute.length >= RouteTarget) break;
                }
                legs.RemoveAt(legs.Count - 1);
                if (bestRoute != null && bestRoute.length >= RouteTarget) return;
            }
        }

        static Route FindRoute(List<Vector3> spots, float top)
        {
            bestRoute = null; int used = 0, roots = 0;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            foreach (var q in spots.Where(spot => spot.y > top - 6f).OrderByDescending(spot => spot.y))
            {
                for (int k = 0; k < 24; k++)
                {
                    Vector3 d = Quaternion.Euler(0, k * 15, 0) * Vector3.forward;
                    budget = 600; roots++;
                    Extend(q, d, new List<Leg>(), 0, q, d);
                    used += 600 - budget;
                    if (bestRoute != null && bestRoute.length >= RouteTarget || used > 100000) break;
                }
                if (bestRoute != null && bestRoute.length >= RouteTarget || used > 100000) break;
            }
            if (bestRoute == null) throw new InvalidOperationException($"No route with a jump was found on this map top ({roots} starts, {used} jumps tried).");
            var r = bestRoute;
            Note($"Route found in {clock.Elapsed.TotalSeconds:0.0} s ({roots} starts, {used} jumps tried): {r.length:0.0} m with {r.legs.Count} jumps, from {V(r.start)} to {V(r.tail)}.");
            foreach (var leg in r.legs)
                Note($"- run {Flat0(leg.takeoff - leg.from).magnitude:0.0} m, jump {Flat0(leg.land - leg.takeoff).magnitude:0.0} m, {leg.land.y - leg.takeoff.y:+0.0;-0.0} m in height, lands {V(leg.land)}");
            return r;
        }

        static List<Vector3> Line(Route route)
        {
            var points = new List<Vector3> { route.start };
            foreach (var leg in route.legs) { points.Add(leg.takeoff); points.Add(leg.land); }
            points.Add(route.tail);
            return points;
        }

        static float LineDistance(Vector3 p, List<Vector3> line)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var d = line[i + 1] - line[i]; float t = Mathf.Clamp01(Vector3.Dot(p - line[i], d) / Mathf.Max(.0001f, d.sqrMagnitude));
                best = Mathf.Min(best, Vector3.Distance(p, line[i] + d * t));
            }
            return best;
        }

        // ---- The cast ----

        static readonly List<string> crowdIds = new List<string>();
        static readonly Dictionary<string, Vector3> startAt = new Dictionary<string, Vector3>();
        static float heroStops = Length - .3f;

        static void Direct(StudioStage stage, List<Vector3> spots, Route route)
        {
            var set = stage.set;
            set.marks.Clear(); set.drives.Clear(); set.overrides.Clear(); set.cues.Clear();
            crowdIds.Clear(); startAt.Clear();
            var random = new System.Random(4711);
            var line = Line(route);

            var hero = set.AddMark(StudioRole.Survivor, stage.ToStage(route.start));
            hero.id = "hero"; hero.actor = "hero"; hero.yaw = StudioHuman.Yaw(route.dir); hero.color = 6; hero.customLook = true;
            hero.appearance = new AvatarAppearance { hair = AvatarHair.Spikes, hairShade = AvatarHairShade.Match, skin = AvatarSkin.Default };
            startAt["hero"] = route.start;

            // The crowd: the game's own bots, on spots beside the route (never on it), nearest first, 1.4 m apart.
            var free = spots.Select(p => (p, d: LineDistance(p, line))).Where(x => x.d > 1.8f && x.d < 10f).OrderBy(x => x.d).Select(x => x.p).ToList();
            var chosen = new List<Vector3>();
            foreach (var p in free)
            {
                if (chosen.Count >= Crowd) break;
                if (chosen.All(c => Vector3.Distance(c, p) > 1.4f)) chosen.Add(p);
            }
            Vector3 mid = line[line.Count / 2];
            for (int i = 0; i < chosen.Count; i++)
            {
                var mark = set.AddMark(StudioRole.Bot, stage.ToStage(chosen[i]));
                mark.id = "bot" + i.ToString("00"); mark.actor = mark.id;
                mark.yaw = StudioHuman.Yaw(Flat0(mid - chosen[i])) + (float)random.NextDouble() * 80 - 40; mark.color = i % 8;
                mark.equipment = i % 3 == 0 ? "BoxingGlove" : "Blaster";
                crowdIds.Add(mark.id); startAt[mark.id] = chosen[i];
            }
            Note($"Crowd: {chosen.Count} of {Crowd} wanted, all game bots, from {free.Count} free spots within 10 m of the route.");

            var h = new StudioDrive { actor = "hero", seed = 11, human = .7f };
            h.steps.Add(new StudioDriveStep { action = "gear", at = .3f, weapon = "Blaster" });
            var path = new StudioDriveStep { action = "path", at = RunAt, pace = "run", edge = true, duration = Length, shoot = "auto", shootAfter = .12f };
            foreach (var p in line.Skip(1)) path.points.Add(stage.ToStage(p));
            foreach (var leg in route.legs) path.jumps.Add(stage.ToStage(leg.takeoff));
            h.steps.Add(path);
            set.drives.Add(h);

            set.alive.smooth = .12f;
            set.EnsureCueIds();
            var problems = StudioPerformer.Problems(set);
            if (problems.Count > 0) Note("Drive problems: " + string.Join(" | ", problems));
            StudioLibrary.Save(set);
            StudioRun.Refresh();
        }

        // ---- Shooting ----

        static IEnumerable<int> Shoot()
        {
            var stage = StudioRun.Stage;
            yield return 0; yield return 0;
            Note($"# Crowd run scout, {DateTime.Now:yyyy-MM-dd HH:mm}\n");
            var spots = FindSpots(stage, out float top);
            if (spots.Count == 0) throw new InvalidOperationException("No standing spots on the map top.");
            var route = FindRoute(spots, top);
            Direct(stage, spots, route);
            yield return 0;

            var clock = System.Diagnostics.Stopwatch.StartNew();
            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.SaveTape();
            var cueLog = StudioRun.CueLog.ToList();
            Note($"Recorded {stage.set.marks.Count} players, {Length:0} s of tape, in {clock.Elapsed.TotalSeconds:0} s of real time. {StudioRun.Status}");
            File.WriteAllLines(Path.Combine(Folder, "cuelog.txt"), cueLog);
            var heroLog = cueLog.Where(l => l.Contains("drive hero")).ToList();
            Note($"Hero's log ({heroLog.Count(l => l.EndsWith(": jump"))} jumps of {route.legs.Count} planned, {heroLog.Count(l => l.Contains("fire on the move"))} shots in the air):\n- " + string.Join("\n- ", heroLog));

            try { Report(stage, route); } catch (Exception error) { Note("Report failed: " + error); }

            var cameras = Cameras(stage, route);
            stage.set.cameras.Clear(); stage.set.cameras.AddRange(cameras);
            StudioLibrary.Save(stage.set);
            StudioRun.Refresh();
            yield return 0;
            var settings = new StudioRenderSettings { width = 640, height = 360, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 12 };
            string renders = Path.Combine(Folder, "renders");
            clock.Restart();
            StudioRender.Start(cameras, settings, renders);
            while (StudioRender.Running) yield return 0;
            Note($"Render 640x360: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s.");
            string dir = Path.Combine(Folder, "shots");
            Directory.CreateDirectory(dir);
            foreach (var result in StudioRender.Results.ToList())
            {
                Note($"{result.cameraId}: {result.frames} frames in {result.seconds:0} s {result.error}".TrimEnd());
                if (!string.IsNullOrEmpty(result.movie) && File.Exists(result.movie)) File.Copy(result.movie, Path.Combine(dir, result.cameraId + ".mp4"), true);
                string strip = Path.Combine(result.folder, "filmstrip.png");
                if (File.Exists(strip)) File.Copy(strip, Path.Combine(dir, result.cameraId + "-filmstrip.png"), true);
            }
            if (Directory.Exists(renders)) Directory.Delete(renders, true);
        }

        static void Report(StudioStage stage, Route route)
        {
            int frames = Mathf.RoundToInt(Length * Fps);
            StudioActorPoses.Pose At(string id, int f) => StudioActorPoses.At(stage.tape, id, f / (float)Fps, Fps);
            var line = Line(route);

            // The hero, frame by frame.
            var hero = new StringBuilder("frame,time,x,y,z,speed,offRoute\n");
            Vector3 last = Vector3.zero; bool have = false; float maxSpeed = 0, covered = 0, worstOff = 0; int lastFast = 0, slowFrames = 0;
            int first = Mathf.RoundToInt((RunAt + .8f) * Fps);
            for (int f = 6; f < frames - 2; f++)
            {
                var p = At("hero", f);
                if (!p.found) { hero.Append($"{f},{f / (float)Fps:0.000},,,,,\n"); have = false; continue; }
                float speed = have ? Flat0(p.feet - last).magnitude * Fps : 0;
                float off = LineDistance(new Vector3(p.feet.x, 0, p.feet.z), line.Select(v => new Vector3(v.x, 0, v.z)).ToList());
                if (have && speed < 12) { maxSpeed = Mathf.Max(maxSpeed, speed); covered += speed / Fps; }
                if (speed > 2.5f && speed < 12) lastFast = f;
                worstOff = Mathf.Max(worstOff, off);
                hero.Append($"{f},{f / (float)Fps:0.000},{p.feet.x:0.00},{p.feet.y:0.00},{p.feet.z:0.00},{speed:0.00},{off:0.00}\n");
                last = p.feet; have = true;
            }
            for (int f = first; f < lastFast; f++)
            {
                var a = At("hero", f); var b = At("hero", f + 1);
                if (a.found && b.found && Flat0(b.feet - a.feet).magnitude * Fps < 2f) slowFrames++;
            }
            File.WriteAllText(Path.Combine(Folder, "hero.csv"), hero.ToString());
            heroStops = Mathf.Clamp(lastFast / (float)Fps - .1f, RunAt + 1.5f, Length - .3f);
            var end = At("hero", frames - 4);
            Note($"Hero: covered {covered:0.0} m of a {route.length:0.0} m route, top ground speed {maxSpeed:0.0} m/s, still running until {lastFast / (float)Fps:0.00} s, " +
                 $"{slowFrames} frames slower than 2 m/s in the middle of the run, farthest off the planned line {worstOff:0.0} m. " +
                 (end.found ? $"At the end he is {Flat0(end.feet - route.tail).magnitude:0.0} m from the planned end and {end.feet.y - route.tail.y:+0.00;-0.00} m against its height." : "At the end he is NOT on the tape (fell out or was eliminated)."));

            // The crowd: who moved, who is still there.
            var table = new StringBuilder("id,travelled,drop,present_at_end\n");
            int n = 0, gone = 0, fell = 0, moved = 0; float travelled = 0;
            foreach (string id in crowdIds)
            {
                n++;
                float path = 0; Vector3 prev = Vector3.zero; bool seen = false; Vector3 begin = Vector3.zero, final = Vector3.zero;
                for (int f = 6; f < frames - 2; f += 3)
                {
                    var p = At(id, f);
                    if (!p.found) continue;
                    if (seen) path += Vector3.Distance(p.feet, prev); else begin = p.feet;
                    prev = p.feet; final = p.feet; seen = true;
                }
                bool present = At(id, frames - 4).found;
                float drop = seen ? begin.y - final.y : 0;
                if (!present) gone++; else if (drop > 3) fell++;
                if (path > 1.5f) moved++;
                travelled += path;
                table.Append($"{id},{path:0.0},{drop:0.0},{present}\n");
            }
            Note($"Crowd: {n} game bots, {moved} moved more than 1.5 m, average path {(n > 0 ? travelled / n : 0):0.0} m, {fell} ended more than 3 m lower, {gone} no longer on the tape at the end.");
            File.WriteAllText(Path.Combine(Folder, "crowd.csv"), table.ToString());
        }

        static StudioCameraTrack Cam(string id, float start, float end, float focal)
        {
            var c = StudioMoves.Camera(id, start, end, focal);
            c.id = id; c.name = id; c.sensorHeight = 24; c.keys[0].focal = focal; c.keys[0].aperture = 11; c.keys[0].focus = 6;
            c.motionBlur = 0;
            c.measure.Add("actor:hero");
            return c;
        }

        static List<StudioCameraTrack> Cameras(StudioStage stage, Route route)
        {
            var cams = new List<StudioCameraTrack>();
            float from = RunAt - .3f, to = heroStops;   // the shots end while he is still running

            var pov = Cam("POV", from, to, 24);
            pov.behavior = "firstPerson"; pov.subject = "hero"; pov.fpSmoothMove = .2f; pov.fpSmoothTurn = .32f; pov.fpLevel = true; pov.fpFieldOfView = 72; pov.fpOwnBody = true;
            cams.Add(pov);

            var chase = Cam("CHASE", from, to, 24);
            StudioMoves.Follow(chase, "actor:hero", -route.dir * 3.4f + Vector3.up * 1.7f + Vector3.Cross(Vector3.up, route.dir) * .9f, .25f);
            cams.Add(chase);

            // A wide from outside that sees as much of the route and the crowd as possible.
            var see = Line(route).Concat(startAt.Values).ToList();
            Vector3 centre = Vector3.zero; foreach (var p in see) centre += p;
            centre /= Mathf.Max(1, see.Count);
            Vector3 best = centre + Vector3.up * 7 - route.dir * 16; int bestSeen = -1; float bestDistance = 16;
            for (int k = 0; k < 24; k++)
                foreach (float distance in new[] { 16f, 22f })
                    foreach (float height in new[] { 5f, 9f })
                    {
                        Vector3 at = centre + Quaternion.Euler(0, k * 15, 0) * Vector3.forward * distance + Vector3.up * height;
                        if (!Clear(at, .5f)) continue;
                        int seen = see.Count(s => Sees(at, s + Vector3.up * .7f));
                        if (seen > bestSeen) { bestSeen = seen; best = at; bestDistance = distance; }
                    }
            var wide = Cam("WIDE", from, to, Mathf.Clamp(24 * bestDistance / 15f, 16, 60));
            StudioMoves.LockedOff(wide, wide.start, stage.ToStage(best), P(stage, centre));
            cams.Add(wide);
            Note($"Cameras {from:0.00}-{to:0.00} s: POV (hero, 72 deg), CHASE, WIDE at {V(best)} seeing {bestSeen} of {see.Count} points.");
            return cams;
        }
    }
}
