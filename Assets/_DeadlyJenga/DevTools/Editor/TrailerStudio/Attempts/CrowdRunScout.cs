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
    //   - about 24 players on the top of the castle: half are the game's own bots (Bot marks), half are driven by
    //     small generated scripts, so the two kinds of crowd can be compared in one take;
    //   - one hero who runs along the top, jumps off an edge to the next platform, fires in the air and runs on.
    // Run: DeadlyJenga.DevTools.Editor.TrailerStudio.CrowdRunScout.Run
    // Output: Logs/ClaudeCrowdRunScout/ (notes.md, hero.csv, crowd.csv, shots/*.mp4).
    [InitializeOnLoad]
    public static class CrowdRunScout
    {
        const int Fps = 30, Crowd = 24;
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
            foreach (var body in blocks.Where(b => tops[b] > highest - 7f))
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
            Note($"Castle top y {top:0.00}. Free standing spots in the top 7 m: {spots.Count}.");
            return spots;
        }

        sealed class RunPlan
        {
            public bool gap;                         // true = a real edge and a platform beyond it
            public Vector3 start, takeoff, landing, end, dir;
            public float run, drop, width;
        }

        // A straight run-up to an edge with a platform beyond it (a little lower is better: a leap, not a hop).
        static RunPlan FindRun(List<Vector3> spots, float top)
        {
            RunPlan best = null; float bestScore = float.MinValue; int candidates = 0;
            foreach (var q in spots.Where(spot => spot.y > top - 4.5f))
                for (int k = 0; k < 24; k++)
                {
                    Vector3 d = Quaternion.Euler(0, k * 15, 0) * Vector3.forward;
                    // The edge: the first place ahead with no floor (a wall or a step up is not an edge).
                    float edge = -1; bool wall = false;
                    for (float s = .3f; s <= 1.5f && edge < 0 && !wall; s += .1f)
                    {
                        if (!GroundAt(q + d * s, q.y, out float f) || f < q.y - .5f) edge = s;
                        else if (f > q.y + .12f) wall = true;
                    }
                    if (edge < 0 || wall) continue;
                    // The run-up behind it.
                    int steps = 0;
                    while (steps < 20 && Level(q - d * (steps + 1) * .4f, q.y)) steps++;
                    float run = steps * .4f;
                    if (run < 4f) continue;
                    // The platform beyond: starts 0.8 to 2.4 m past the edge and goes on for 3.2 m.
                    for (float r = edge + .8f; r <= edge + 2.4f; r += .2f)
                    {
                        Vector3 l = q + d * r;
                        if (!GroundAt(l, q.y, out float f)) continue;
                        float drop = q.y - f;
                        if (drop < -.15f || drop > 3.2f) continue;
                        bool ok = true;
                        for (int j = 0; j <= 8 && ok; j++) ok = Level(l + d * j * .4f, f);
                        if (!ok) continue;
                        Vector3 from = q + Vector3.up * 1.3f, to = new Vector3(l.x, f + 1.3f, l.z);
                        if (!Sees(from, to) || !Sees(from + Vector3.up * .6f, to + Vector3.up * .6f)) continue;
                        candidates++;
                        float gapWidth = r - edge;
                        float score = Mathf.Min(run, 8) + (drop > .6f && drop < 2.6f ? 2 : 0) + (gapWidth >= 1.2f ? 1.5f : 0) + (q.y - (top - 4.5f)) * .5f;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = new RunPlan
                            {
                                gap = true, dir = d, run = Mathf.Min(run, 7.2f), drop = drop, width = gapWidth,
                                start = q - d * Mathf.Min(run, 7.2f), takeoff = q + d * (edge - .45f),
                                landing = new Vector3(l.x, f, l.z) + d * .6f, end = new Vector3(l.x, f, l.z) + d * 3f
                            };
                        }
                        break;
                    }
                }
            if (best != null)
            {
                Note($"Run found ({candidates} candidates): {best.run:0.0} m run-up from {V(best.start)}, take-off {V(best.takeoff)}, gap {best.width:0.0} m, drop {best.drop:0.0} m, lands {V(best.landing)}, ends {V(best.end)}.");
                return best;
            }
            // No edge with a platform beyond: the longest straight floor, with a jump in the middle of it.
            float longest = 0; Vector3 a = spots[0], dirBest = Vector3.forward;
            foreach (var q in spots.Where(spot => spot.y > top - 4.5f))
                for (int k = 0; k < 24; k++)
                {
                    Vector3 d = Quaternion.Euler(0, k * 15, 0) * Vector3.forward;
                    int steps = 0;
                    while (steps < 30 && Level(q + d * (steps + 1) * .4f, q.y)) steps++;
                    if (steps * .4f > longest) { longest = steps * .4f; a = q; dirBest = d; }
                }
            Note($"NO edge with a platform beyond it was found. Falling back to the longest flat run: {longest:0.0} m from {V(a)}.");
            return new RunPlan { gap = false, dir = dirBest, run = longest * .5f, start = a, takeoff = a + dirBest * longest * .5f, landing = a + dirBest * longest * .75f, end = a + dirBest * (longest - .3f) };
        }

        static float SegmentDistance(Vector3 p, Vector3 x0, Vector3 x1)
        {
            var d = x1 - x0; float t = Mathf.Clamp01(Vector3.Dot(p - x0, d) / Mathf.Max(.0001f, d.sqrMagnitude));
            return Vector3.Distance(p, x0 + d * t);
        }

        // ---- The cast ----

        static readonly List<string> crowdIds = new List<string>();
        static readonly Dictionary<string, Vector3> startAt = new Dictionary<string, Vector3>();
        static string victim = "", second = "";

        static StudioDriveStep S(string action, float at = -1, string target = "", float duration = 0, string pace = "walk", float reach = 1.0f, string weapon = "") =>
            new StudioDriveStep { action = action, at = at, target = target, duration = duration, pace = pace, reach = reach, weapon = weapon };

        static void Direct(StudioStage stage, List<Vector3> spots, RunPlan run)
        {
            var set = stage.set; Vector3 up = Vector3.up;
            set.marks.Clear(); set.drives.Clear(); set.overrides.Clear(); set.cues.Clear();
            crowdIds.Clear(); startAt.Clear();
            var random = new System.Random(4711);
            float Rand(float a, float b) => a + (float)random.NextDouble() * (b - a);

            // The hero.
            var hero = set.AddMark(StudioRole.Survivor, stage.ToStage(run.start));
            hero.id = "hero"; hero.actor = "hero"; hero.yaw = StudioHuman.Yaw(run.dir); hero.color = 6; hero.customLook = true;
            hero.appearance = new AvatarAppearance { hair = AvatarHair.Spikes, hairShade = AvatarHairShade.Match, skin = AvatarSkin.Default };
            startAt["hero"] = run.start;

            // The crowd: spots off the hero's line, nearest to it first, at least 1.5 m apart.
            Vector3 mid = Vector3.Lerp(run.start, run.end, .55f);
            var free = spots.Where(p => SegmentDistance(p, run.start, run.end) > 1.7f && Mathf.Abs(p.y - mid.y) < 5f)
                .OrderBy(p => Vector3.Distance(p, mid)).ToList();
            var chosen = new List<Vector3>();
            foreach (var p in free)
            {
                if (chosen.Count >= Crowd) break;
                if (chosen.All(c => Vector3.Distance(c, p) > 1.5f)) chosen.Add(p);
            }
            var skins = ((AvatarSkin[])Enum.GetValues(typeof(AvatarSkin))).Where(s => s != AvatarSkin.Default).ToArray();
            for (int i = 0; i < chosen.Count; i++)
            {
                bool bot = i % 2 == 0;
                var mark = set.AddMark(bot ? StudioRole.Bot : StudioRole.Survivor, stage.ToStage(chosen[i]));
                mark.id = (bot ? "bot" : "drv") + i.ToString("00"); mark.actor = mark.id;
                mark.yaw = StudioHuman.Yaw(Flat0(mid - chosen[i])) + Rand(-40, 40); mark.color = i % 8;
                if (bot) mark.equipment = i % 4 == 0 ? "Blaster" : "BoxingGlove";
                else
                {
                    mark.customLook = true;
                    mark.appearance = new AvatarAppearance { hair = AvatarHair.None, hairShade = AvatarHairShade.Match, skin = skins.Length > 0 ? skins[(i / 2) % skins.Length] : AvatarSkin.Default };
                }
                crowdIds.Add(mark.id); startAt[mark.id] = chosen[i];
            }
            Note($"Crowd: {chosen.Count} of {Crowd} wanted ({crowdIds.Count(c => c.StartsWith("bot"))} game bots, {crowdIds.Count(c => c.StartsWith("drv"))} driven), {free.Count} free spots near the run.");

            // Who the hero shoots: in the air, whoever is most nearly straight ahead of the take-off; after landing, whoever is nearest the end.
            victim = crowdIds.Where(id => { float dist = Vector3.Distance(startAt[id], run.takeoff); return dist > 3.5f && dist < 16; })
                .OrderBy(id => Vector3.Angle(Flat0(startAt[id] - run.takeoff), run.dir)).FirstOrDefault() ?? crowdIds.FirstOrDefault() ?? "";
            second = crowdIds.Where(id => id != victim).OrderBy(id => Vector3.Distance(startAt[id], run.end)).FirstOrDefault() ?? victim;
            if (victim.Length > 0) Note($"Hero shoots {victim} in the air ({Vector3.Distance(startAt[victim], run.takeoff):0.0} m, {Vector3.Angle(Flat0(startAt[victim] - run.takeoff), run.dir):0} deg off his line), then {second} after landing.");

            var h = new StudioDrive { actor = "hero", seed = 11, human = .7f };
            h.steps.Add(S("gear", .3f, weapon: "Blaster"));
            h.steps.Add(new StudioDriveStep { action = "jump", at = RunAt, target = P(stage, run.takeoff), duration = 8 });
            h.steps.Add(new StudioDriveStep
            {
                action = "path", pace = "run", edge = true, duration = 6.5f, shoot = victim.Length > 0 ? "actor:" + victim : "", shootAfter = .12f,
                points = new List<Vector3> { stage.ToStage(run.takeoff), stage.ToStage(run.landing), stage.ToStage(run.end) }
            });
            if (second.Length > 0) { h.steps.Add(S("fire", -1, "actor:" + second)); h.steps.Add(S("look", -1, "actor:" + second, 2)); }
            set.drives.Add(h);

            // The driven half of the crowd: each has a rival (the nearest other player) and a weapon.
            for (int i = 1; i < chosen.Count; i += 2)
            {
                string id = crowdIds[i];
                var others = crowdIds.Where(o => o != id).OrderBy(o => Vector3.Distance(startAt[o], chosen[i])).ToList();
                string rival = others[0], other = others.Count > 1 ? others[1] : others[0];
                bool glove = i % 4 == 1;
                var d = new StudioDrive { actor = id, seed = 100 + i, human = 1 };
                d.steps.Add(S("gear", Rand(.3f, .7f), weapon: glove ? "BoxingGlove" : "Blaster"));
                d.steps.Add(S("look", -1, "actor:" + rival));
                float t = Rand(1.0f, 2.6f);
                if (glove)
                {
                    d.steps.Add(S("punch", t, "actor:" + rival, 3.5f, "jog"));
                    d.steps.Add(S("look", -1, "actor:" + other, 1.2f));
                    d.steps.Add(S("punch", t + Rand(2.6f, 3.4f), "actor:" + other, 3f, "jog"));
                }
                else
                {
                    Vector3 side = Vector3.Cross(up, Flat(startAt[rival] - chosen[i])) * (i % 8 < 4 ? .45f : -.45f);
                    d.steps.Add(S("fire", t, "actor:" + rival));
                    if (Level(chosen[i] + side, chosen[i].y)) d.steps.Add(new StudioDriveStep { action = "goTo", point = stage.ToStage(chosen[i] + side), pace = "walk", duration = 1.1f });
                    d.steps.Add(S("fire", t + Rand(1.5f, 2f), "actor:" + rival));
                    d.steps.Add(S("look", -1, "actor:hero", .9f));
                    d.steps.Add(S("fire", t + Rand(3.3f, 4f), "actor:" + other));
                }
                set.drives.Add(d);
            }

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
            if (spots.Count == 0) throw new InvalidOperationException("No standing spots on the castle top.");
            var run = FindRun(spots, top);
            Direct(stage, spots, run);
            yield return 0;

            var clock = System.Diagnostics.Stopwatch.StartNew();
            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.SaveTape();
            var cueLog = StudioRun.CueLog.ToList();
            Note($"Recorded {stage.set.marks.Count} players, {Length:0} s of tape, in {clock.Elapsed.TotalSeconds:0} s of real time. {StudioRun.Status}");
            File.WriteAllLines(Path.Combine(Folder, "cuelog.txt"), cueLog);
            Note("Hero's log:\n- " + string.Join("\n- ", cueLog.Where(l => l.Contains("drive hero"))));

            try { Report(stage, run); } catch (Exception error) { Note("Report failed: " + error); }

            var cameras = Cameras(stage, run);
            stage.set.cameras.Clear(); stage.set.cameras.AddRange(cameras);
            StudioLibrary.Save(stage.set);
            StudioRun.Refresh();
            yield return 0;
            var settings = new StudioRenderSettings { width = 640, height = 360, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 10 };
            string renders = Path.Combine(Folder, "renders");
            clock.Restart();
            StudioRender.Start(cameras, settings, renders);
            while (StudioRender.Running) yield return 0;
            Note($"Render 640x360: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s.");
            string dir = Path.Combine(Folder, "shots");
            Directory.CreateDirectory(dir);
            foreach (var result in StudioRender.Results.ToList())
            {
                Note($"{result.cameraId}: {result.frames} frames in {result.seconds:0} s {result.error}");
                if (!string.IsNullOrEmpty(result.movie) && File.Exists(result.movie)) File.Copy(result.movie, Path.Combine(dir, result.cameraId + ".mp4"), true);
                string strip = Path.Combine(result.folder, "filmstrip.png");
                if (File.Exists(strip)) File.Copy(strip, Path.Combine(dir, result.cameraId + "-filmstrip.png"), true);
            }
            if (Directory.Exists(renders)) Directory.Delete(renders, true);
        }

        static void Report(StudioStage stage, RunPlan run)
        {
            int frames = Mathf.RoundToInt(Length * Fps);
            StudioActorPoses.Pose At(string id, int f) => StudioActorPoses.At(stage.tape, id, f / (float)Fps, Fps);

            // The hero, frame by frame.
            var hero = new StringBuilder("frame,time,x,y,z,speed,alongRun,heightAboveTakeoff\n");
            Vector3 last = Vector3.zero; bool have = false; float maxSpeed = 0, peak = float.MinValue, lowest = float.MaxValue;
            for (int f = 6; f < frames - 2; f++)
            {
                var p = At("hero", f);
                if (!p.found) { hero.Append($"{f},{f / (float)Fps:0.000},,,,,,\n"); have = false; continue; }
                float speed = have ? Flat0(p.feet - last).magnitude * Fps : 0;
                maxSpeed = Mathf.Max(maxSpeed, speed); peak = Mathf.Max(peak, p.feet.y); lowest = Mathf.Min(lowest, p.feet.y);
                hero.Append($"{f},{f / (float)Fps:0.000},{p.feet.x:0.00},{p.feet.y:0.00},{p.feet.z:0.00},{speed:0.00},{Vector3.Dot(p.feet - run.start, run.dir):0.00},{p.feet.y - run.takeoff.y:0.00}\n");
                last = p.feet; have = true;
            }
            File.WriteAllText(Path.Combine(Folder, "hero.csv"), hero.ToString());
            var end = At("hero", frames - 4);
            float planned = Vector3.Dot(run.end - run.start, run.dir);
            Note($"Hero: top ground speed {maxSpeed:0.0} m/s, highest feet {peak - run.takeoff.y:+0.00;-0.00} m against the take-off, lowest {lowest - run.takeoff.y:+0.00;-0.00} m. " +
                 (end.found ? $"At the end he is {Vector3.Dot(end.feet - run.start, run.dir):0.0} m along a {planned:0.0} m run, {end.feet.y - run.end.y:+0.00;-0.00} m against the planned end height, {Flat0(end.feet - run.end).magnitude:0.0} m from the planned end." : "At the end he is NOT on the tape (fell out or was eliminated)."));

            // The crowd: who moved, who is still there.
            var table = new StringBuilder("id,kind,travelled,drop,present_at_end\n");
            foreach (string kind in new[] { "bot", "drv" })
            {
                int n = 0, gone = 0, fell = 0, moved = 0; float travelled = 0;
                foreach (string id in crowdIds.Where(c => c.StartsWith(kind)))
                {
                    n++;
                    float path = 0; Vector3 prev = Vector3.zero; bool seen = false; Vector3 first = Vector3.zero, final = Vector3.zero;
                    for (int f = 6; f < frames - 2; f += 3)
                    {
                        var p = At(id, f);
                        if (!p.found) continue;
                        if (seen) path += Vector3.Distance(p.feet, prev); else first = p.feet;
                        prev = p.feet; final = p.feet; seen = true;
                    }
                    bool present = At(id, frames - 4).found;
                    float drop = seen ? first.y - final.y : 0;
                    if (!present) gone++; else if (drop > 3) fell++;
                    if (path > 1.5f) moved++;
                    travelled += path;
                    table.Append($"{id},{kind},{path:0.0},{drop:0.0},{present}\n");
                }
                Note($"{(kind == "bot" ? "Game bots" : "Driven crowd")}: {n} players, {moved} moved more than 1.5 m, average path {(n > 0 ? travelled / n : 0):0.0} m, {fell} fell more than 3 m, {gone} no longer on the tape at the end.");
            }
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

        static List<StudioCameraTrack> Cameras(StudioStage stage, RunPlan run)
        {
            var cams = new List<StudioCameraTrack>();
            float from = RunAt - .4f, to = Length - .3f;

            var pov = Cam("POV", from, to, 24);
            pov.behavior = "firstPerson"; pov.subject = "hero"; pov.fpSmoothMove = .2f; pov.fpSmoothTurn = .32f; pov.fpLevel = true; pov.fpFieldOfView = 72; pov.fpOwnBody = true;
            cams.Add(pov);

            var chase = Cam("CHASE", from, to, 24);
            StudioMoves.Follow(chase, "actor:hero", -run.dir * 3.4f + Vector3.up * 1.7f + Vector3.Cross(Vector3.up, run.dir) * .9f, .25f);
            cams.Add(chase);

            // A wide from outside that sees as much of the crowd as possible.
            Vector3 centre = Vector3.zero; foreach (var p in startAt.Values) centre += p;
            centre /= Mathf.Max(1, startAt.Count);
            Vector3 best = centre + Vector3.up * 6 - run.dir * 14; int bestSeen = -1; float bestDistance = 14;
            for (int k = 0; k < 24; k++)
                foreach (float distance in new[] { 13f, 17f })
                    foreach (float height in new[] { 4f, 7f })
                    {
                        Vector3 p = centre + Quaternion.Euler(0, k * 15, 0) * Vector3.forward * distance + Vector3.up * height;
                        if (!Clear(p, .5f)) continue;
                        int seen = startAt.Values.Count(s => Sees(p, s + Vector3.up * .7f));
                        if (seen > bestSeen) { bestSeen = seen; best = p; bestDistance = distance; }
                    }
            var wide = Cam("WIDE", from, to, Mathf.Clamp(24 * bestDistance / 11f, 16, 60));
            StudioMoves.LockedOff(wide, wide.start, stage.ToStage(best), P(stage, centre));
            cams.Add(wide);
            Note($"Cameras: POV (hero, 72 deg), CHASE, WIDE at {V(best)} seeing {bestSeen} of {startAt.Count} starting spots.");
            return cams;
        }
    }
}
