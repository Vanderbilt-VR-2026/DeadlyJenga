using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Proof for the alive layer, the driven actor and the body-follows-head look-at.
    //   ...AliveActorsProof.RunRecord    record the take (idle group, a driven hero and his rival), checks, numbers,
    //                                    a 640x360 preview of every proof shot, and the take bundle
    //   ...AliveActorsProof.RunRenderA   load the bundle; proof A at 1920x1080: the idle group, alive off and on
    //   ...AliveActorsProof.RunRenderBC  load the bundle; proof B (driven run and glove punch, close and first person)
    //                                    and proof C (a look behind, the body turning after the head) at 1920x1080
    // Output: Logs/ClaudeAliveActors/<mode>/ (notes.md, checks, numbers, renders).
    [InitializeOnLoad]
    public static class AliveActorsProof
    {
        const int Fps = 30;
        const float Length = 6.2f;
        const string SetId = "alive-actors";
        const string Key = "DeadlyJenga.AliveActorsProof";

        static int Step { get => SessionState.GetInt(Key, 0); set => SessionState.SetInt(Key, value); }
        static string Mode { get => SessionState.GetString(Key + ".Mode", "record"); set => SessionState.SetString(Key + ".Mode", value); }
        static double Deadline { get => SessionState.GetFloat(Key + ".Deadline", 0); set => SessionState.SetFloat(Key + ".Deadline", (float)value); }
        static string Failure { get => SessionState.GetString(Key + ".Failure", ""); set => SessionState.SetString(Key + ".Failure", value); }
        static int Fails { get => SessionState.GetInt(Key + ".Fails", 0); set => SessionState.SetInt(Key + ".Fails", value); }

        static IEnumerator<int> routine;
        static int lastTick = -1;
        static readonly StringBuilder notes = new StringBuilder();

        public static string Folder => Path.Combine(StudioLibrary.ProjectRoot, "Logs", "ClaudeAliveActors", Mode);
        static string Bundle => Path.Combine(StudioLibrary.ProjectRoot, "Docs", "Development", "TrailerStudio", "attempts", "alive-actors", "bundle");

        static AliveActorsProof() { if (Step != 0) Hook(); }
        static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

        public static void RunRecord() => Begin("record");
        public static void RunRenderA() => Begin("renderA");
        public static void RunRenderBC() => Begin("renderBC");

        static void Begin(string mode)
        {
            Mode = mode;
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            notes.Clear();
            string tapes = StudioLibrary.TapeFolder(SetId);
            if (Directory.Exists(tapes)) Directory.Delete(tapes, true);
            Failure = ""; Fails = 0; Step = 1;
            Deadline = EditorApplication.timeSinceStartup + 240;
            Hook();
        }

        static void Note(string line)
        {
            notes.AppendLine(line);
            Debug.Log("[AliveActorsProof] " + line);
            try { File.WriteAllText(Path.Combine(Folder, "notes.md"), notes.ToString()); } catch { }
        }

        static void Check(string name, bool passed, string detail)
        {
            if (!passed) Fails++;
            Note($"- {(passed ? "PASS" : "FAIL")} {name}: {detail}");
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
                        StudioRun.OpenStage(Mode == "record" ? NewSet() : LoadBundle());
                        Step = 2; Deadline = EditorApplication.timeSinceStartup + 560;
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
                Note("FAIL stopped at step " + Step + ": " + error.Message);
                routine = null;
                if (StudioRun.State != StudioState.Closed && Step < 3) { Step = 3; StudioRun.CloseStage(); }
                else Finish();
            }
        }

        static void Finish()
        {
            bool failed = Failure.Length > 0 || Fails > 0;
            Step = 0;
            StudioLibrary.SetsFolderOverride = "";
            EditorApplication.update -= Tick;
            Note(failed ? $"FAIL alive actors {Mode}: {(Failure.Length > 0 ? Failure : Fails + " checks failed")}" : "PASS alive actors " + Mode + ", output in " + Folder);
            if (SessionState.GetString("DJWarm.id", "") != "")
            {
                Directory.CreateDirectory(Path.Combine("Logs", "Warm"));
                File.WriteAllText(Path.Combine("Logs", "Warm", "exit.txt"), failed ? "1" : "0");
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                return;
            }
            bool startedForThis = Environment.GetCommandLineArgs().Any(a => a.Contains("AliveActorsProof.Run"));
            if (Application.isBatchMode || startedForThis) EditorApplication.Exit(failed ? 1 : 0);
        }

        static StudioSet NewSet()
        {
            var set = new StudioSet { id = SetId, title = "Alive actors proof", map = "castle", fps = Fps, length = Length, seed = 4242, handBot = false, notes = "Made by AliveActorsProof. Safe to delete." };
            StudioLibrary.Save(set);
            return set;
        }

        static StudioSet LoadBundle()
        {
            if (!Directory.Exists(Bundle)) throw new DirectoryNotFoundException("No recorded bundle at " + Bundle);
            var set = JsonUtility.FromJson<StudioSet>(File.ReadAllText(Path.Combine(Bundle, "set.json")));
            string tape = StudioLibrary.TapePath(SetId);
            Directory.CreateDirectory(Path.GetDirectoryName(tape));
            File.Copy(Path.Combine(Bundle, "main.djtape"), tape, true);
            string take = File.ReadAllText(Path.Combine(Bundle, "take.txt")).Trim();
            string audio = StudioRender.TakeAudioFile(SetId, take);
            Directory.CreateDirectory(Path.GetDirectoryName(audio));
            if (File.Exists(Path.Combine(Bundle, "tape.wav"))) File.Copy(Path.Combine(Bundle, "tape.wav"), audio, true);
            StudioLibrary.Save(set);
            return set;
        }

        static void SaveBundle(StudioSet set)
        {
            string bundle = Path.Combine(Folder, "bundle");
            Directory.CreateDirectory(bundle);
            File.WriteAllText(Path.Combine(bundle, "set.json"), JsonUtility.ToJson(set, true));
            File.Copy(StudioLibrary.TapePath(set.id), Path.Combine(bundle, "main.djtape"), true);
            var world = StudioRun.Stage.tape.FindType(WorldTrack.Id)?.ActiveTake;
            File.WriteAllText(Path.Combine(bundle, "take.txt"), world?.id ?? "");
            string audio = StudioRender.AudioFile(set.id);
            if (File.Exists(audio)) File.Copy(audio, Path.Combine(bundle, "tape.wav"), true);
            Note($"Bundle saved: tape {new FileInfo(StudioLibrary.TapePath(set.id)).Length / 1024} KB.");
        }

        static IEnumerable<int> UntilHeld()
        {
            float started = Time.realtimeSinceStartup;
            while (StudioRun.State != StudioState.Held)
            {
                if (StudioRun.State == StudioState.Closed) throw new InvalidOperationException("The stage closed: " + StudioRun.Status);
                if (Time.realtimeSinceStartup - started > 240) throw new TimeoutException("A run took more than four minutes: " + StudioRun.Status);
                yield return 0;
            }
        }

        static string V(Vector3 v) => v.ToString("F2");
        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        static float YawTo(Vector3 from, Vector3 to) => StudioHuman.Yaw(Flat(to - from));
        static string P(StudioStage stage, Vector3 world) => StudioTargets.Point(stage.ToStage(world));
        static int Mask => (1 << Layers.Block) | (1 << Layers.Surface) | 1;

        // ---- Where everyone stands ----

        sealed class Plan
        {
            public readonly List<Vector3> group = new List<Vector3>();
            public Vector3 hero, rival, jumpAt;
            public bool step;
        }

        static float TopOf(Rigidbody body) => body.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).Select(c => c.bounds.max.y).DefaultIfEmpty(body.position.y).Max();

        static bool Standable(Vector3 p) =>
            StudioStage.SurfaceBelow(p + Vector3.up * .05f, out var hit) && Mathf.Abs(hit.y - p.y) < .12f &&
            !Physics.CheckCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.2f, .22f, (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore);

        static Plan MakePlan(StudioStage stage)
        {
            var plan = new Plan();
            var blocks = stage.blocks.Where(b => b && b.gameObject.activeInHierarchy).ToList();
            float highest = blocks.Max(TopOf);
            var spots = new List<Vector3>();
            foreach (var body in blocks)
            {
                float top = TopOf(body);
                if (top < highest - 7) continue;
                var size = StudioHandDirection.BlockSize(body);
                Vector3 longLocal = size.x >= size.z ? Vector3.right : Vector3.forward, wideLocal = size.x >= size.z ? Vector3.forward : Vector3.right;
                float length = Mathf.Max(size.x, size.z), width = Mathf.Min(size.x, size.z);
                for (float t = -length / 2 + .4f; t <= length / 2 - .4f + .001f; t += .5f)
                    foreach (float w in width >= 1.4f ? new[] { -.4f, 0, .4f } : new[] { 0f })
                    {
                        Vector3 p = body.position + body.rotation * (longLocal * t + wideLocal * w);
                        p.y = top;
                        if (!Standable(p)) continue;
                        // Open sky above, so cameras can see them.
                        if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.up, 10, (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore)) continue;
                        spots.Add(p);
                    }
            }
            Note($"Top of the map {highest:0.00} m; {spots.Count} free standing spots with open sky in the top 7 m.");
            if (spots.Count < 7) throw new InvalidOperationException("Not enough free spots on the tower top.");

            // The idle group: five people on one level, close enough to be a group, not shoulder to shoulder.
            List<Vector3> best = null;
            foreach (var centre in spots)
            {
                var near = spots.Where(s => Mathf.Abs(s.y - centre.y) < .45f && Vector3.Distance(Flat(s), Flat(centre)) < 3.2f)
                    .OrderBy(s => Vector3.Distance(Flat(s), Flat(centre))).ToList();
                var chosen = new List<Vector3>();
                foreach (var s in near) if (chosen.All(c => Vector3.Distance(c, s) > 1.0f)) { chosen.Add(s); if (chosen.Count == 5) break; }
                if (best == null || chosen.Count > best.Count || chosen.Count == best.Count && centre.y > best[0].y + .3f) best = chosen;
            }
            plan.group.AddRange(best);
            Vector3 middle = best.Aggregate(Vector3.zero, (a, b) => a + b) / best.Count;

            // The hero and his rival, away from the group, with one step or small gap early on the run between them,
            // and room after it to land (a jump carries a running survivor about 3 m) before the rival.
            float bestScore = float.MinValue;
            var free = spots.Where(s => best.All(g => Vector3.Distance(Flat(g), Flat(s)) > 2.2f)).ToList();
            const float sample = .2f;
            var why = new int[4];
            foreach (var h in free)
                foreach (var r in free)
                {
                    float d = Vector3.Distance(Flat(h), Flat(r));
                    if (d < 5f || d > 8.5f || Mathf.Abs(h.y - r.y) > 1.3f) continue;
                    // The ground along the run, every 20 cm.
                    var ground = new List<float>();
                    for (float x = .3f; x < d - .3f; x += sample)
                    {
                        Vector3 p = Vector3.Lerp(h, r, x / d);
                        ground.Add(Physics.Raycast(new Vector3(p.x, Mathf.Max(h.y, r.y) + 1.6f, p.z), Vector3.down, out var hit, 4f, Mask, QueryTriggerInteraction.Ignore) ? hit.point.y : float.NaN);
                    }
                    bool Hole(float y) => float.IsNaN(y) || y < Mathf.Min(h.y, r.y) - .7f;
                    int change = ground.FindIndex(y => Hole(y) || Mathf.Abs(y - h.y) > .25f);
                    if (change < 0) { why[0]++; continue; }
                    float at = .3f + change * sample;
                    if (at < .5f || at > 3f || d - at < 3.8f) { why[1]++; continue; }
                    // After the change: at most 1.1 m of gap, then (almost) solid ground to the rival.
                    int gap = 0, holes = 0; bool landed = false;
                    for (int i = change; i < ground.Count; i++)
                    {
                        if (Hole(ground[i])) { if (!landed) gap++; else holes++; }
                        else landed = true;
                    }
                    if (gap * sample > 1.1f || holes > (ground.Count - change) * .12f) { why[2]++; continue; }
                    // Nothing in the way of a running body (a step up is fine: it is jumped).
                    float top = Mathf.Max(h.y, r.y) + .9f;
                    if (Physics.Linecast(new Vector3(h.x, top, h.z), new Vector3(r.x, top, r.z), (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore)) { why[3]++; continue; }
                    float score = 10 - Mathf.Abs(d - 6.5f) - Mathf.Abs(at - 1.6f) + (gap > 0 ? 1 : 0) + h.y * .1f;
                    if (score > bestScore) { bestScore = score; plan.hero = h; plan.rival = r; plan.step = true; plan.jumpAt = Vector3.Lerp(h, r, Mathf.Max(.15f, at - .5f) / d); }
                }
            Note($"Run search: no change of ground {why[0]}, change too early or late {why[1]}, gap too long or holes after {why[2]}, something in the way {why[3]}.");
            if (bestScore == float.MinValue)
            {
                // No step anywhere: a straight run, and no jump (said in the notes).
                foreach (var h in free)
                    foreach (var r in free)
                    {
                        float d = Vector3.Distance(Flat(h), Flat(r));
                        if (d < 4f || d > 7f || Mathf.Abs(h.y - r.y) > .3f) continue;
                        if (Physics.CapsuleCast(h + Vector3.up * .5f, h + Vector3.up * 1.1f, .2f, Flat(r - h).normalized, d - .4f, (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore)) continue;
                        bool solid = true;
                        for (float x = .3f; x < d - .3f && solid; x += sample)
                        {
                            Vector3 p = Vector3.Lerp(h, r, x / d);
                            solid = Physics.Raycast(p + Vector3.up * .5f, Vector3.down, out var hit, 1.1f, Mask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - h.y) < .3f;
                        }
                        if (!solid) continue;
                        float score = -Mathf.Abs(d - 5.5f);
                        if (score > bestScore) { bestScore = score; plan.hero = h; plan.rival = r; plan.step = false; }
                    }
                if (bestScore == float.MinValue) throw new InvalidOperationException("No clear run for the hero and the rival.");
            }
            Note($"Group of {best.Count} around {V(middle)}: {string.Join(" ", best.Select(V))}. Hero {V(plan.hero)}, rival {V(plan.rival)} ({Vector3.Distance(Flat(plan.hero), Flat(plan.rival)):0.00} m), step or gap on the way: {plan.step}" + (plan.step ? $", jump at {V(plan.jumpAt)} ({Vector3.Distance(Flat(plan.hero), Flat(plan.jumpAt)):0.00} m from the hero)." : ", so no jump."));
            return plan;
        }

        static readonly string[] GroupNames = { "Turner", "Bea", "Cal", "Dot", "Eli" };
        static readonly AvatarSkin[] Skins = { AvatarSkin.Default, AvatarSkin.Penguin, AvatarSkin.Fox, AvatarSkin.Panda, AvatarSkin.Astronaut };

        static void Direct(StudioStage stage, Plan plan)
        {
            var set = stage.set;
            set.marks.Clear(); set.drives.Clear(); set.overrides.Clear(); set.cues.Clear();
            Vector3 middle = plan.group.Aggregate(Vector3.zero, (a, b) => a + b) / plan.group.Count;
            for (int i = 0; i < plan.group.Count; i++)
            {
                var mark = set.AddMark(StudioRole.Survivor, stage.ToStage(plan.group[i]));
                mark.actor = GroupNames[i]; mark.id = set.UniqueId(GroupNames[i].ToLowerInvariant());
                // A loose circle: roughly toward the middle, never exactly.
                mark.yaw = YawTo(plan.group[i], middle) + (i % 2 == 0 ? 25 : -30) * (i == 0 ? .3f : 1);
                mark.color = i + 1; mark.customLook = true;
                mark.appearance = new AvatarAppearance { hair = i == 0 ? AvatarHair.Spikes : AvatarHair.None, hairShade = AvatarHairShade.Match, skin = Skins[i] };
            }
            var hero = set.AddMark(StudioRole.Survivor, stage.ToStage(plan.hero));
            hero.actor = "Hero"; hero.id = "hero"; hero.color = 6; hero.customLook = true;
            hero.appearance = new AvatarAppearance { hair = AvatarHair.Pompadour, hairShade = AvatarHairShade.Match, skin = AvatarSkin.Default };
            // He starts turned away a little, so his head has to find the rival first.
            hero.yaw = YawTo(plan.hero, plan.rival) + 75;
            var rival = set.AddMark(StudioRole.Survivor, stage.ToStage(plan.rival));
            rival.actor = "Rival"; rival.id = "rival"; rival.color = 0; rival.customLook = true;
            rival.appearance = new AvatarAppearance { hair = AvatarHair.None, hairShade = AvatarHairShade.Match, skin = AvatarSkin.Gorilla };
            rival.yaw = YawTo(plan.rival, plan.hero) + 20;

            // The hero's script, in tape seconds and stage metres.
            var drive = new StudioDrive { actor = "hero", seed = 7, human = 1, muted = true };
            drive.steps.Add(new StudioDriveStep { action = "gear", at = .3f, weapon = "BoxingGlove" });
            drive.steps.Add(new StudioDriveStep { action = "look", at = .75f, target = "actor:rival" });
            drive.steps.Add(new StudioDriveStep { action = "wait", duration = .5f });
            if (plan.step) drive.steps.Add(new StudioDriveStep { action = "jump", target = P(stage, plan.jumpAt) });
            drive.steps.Add(new StudioDriveStep { action = "punch", target = "actor:rival", pace = "jog", reach = 1.0f });
            drive.steps.Add(new StudioDriveStep { action = "wait", duration = 1.4f });
            drive.steps.Add(new StudioDriveStep { action = "look", target = "" });
            drive.steps.Add(new StudioDriveStep { action = "wait", duration = 2 });
            set.drives.Add(drive);
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            Note("Cast: " + string.Join("; ", set.marks.Select(m => $"{m.actor} ({m.id}) at {V(m.position)} yaw {m.yaw:0}")));
            Note("Hero drive: " + JsonUtility.ToJson(drive));
        }

        // ---- Cameras ----

        static StudioCameraTrack Cam(string id, float start, float end, float focal)
        {
            var c = StudioMoves.Camera(id, start, end, focal);
            c.id = id; c.sensorHeight = 24; c.keys[0].focal = focal; c.keys[0].aperture = 11; c.keys[0].focus = 5;
            c.vignette = .15f; c.grain = .02f; c.contrast = 8; c.saturation = 6;
            return c;
        }

        static bool Sees(Vector3 from, Vector3 to) => !Physics.Linecast(from, to, Mask, QueryTriggerInteraction.Ignore);

        // A clear camera spot looking at 'centre' from about 'distance', seeing every point in 'must'.
        static Vector3 Spot(Vector3 centre, float distance, float height, IEnumerable<Vector3> must, Vector3 prefer) => Spot(centre, distance, height, must, prefer, null, out _);

        // As above; 'avoid' = other people who must not stand between the camera and 'centre'.
        static Vector3 Spot(Vector3 centre, float distance, float height, IEnumerable<Vector3> must, Vector3 prefer, List<Vector3> avoid, out float bestScore)
        {
            Vector3 best = centre + Vector3.back * distance + Vector3.up * height; bestScore = float.MinValue;
            var points = must.ToList();
            float Blocked(Vector3 p)
            {
                if (avoid == null) return 0;
                Vector3 a = p, b = centre + Vector3.up * .5f, d = b - a;
                float worst = 0;
                foreach (var o in avoid)
                {
                    Vector3 q = o + Vector3.up * .5f;
                    float t = Mathf.Clamp01(Vector3.Dot(q - a, d) / d.sqrMagnitude);
                    float gap = Vector3.Distance(q, a + d * t);
                    if (gap < .7f) worst = Mathf.Max(worst, 1 - gap / .7f);
                }
                return worst;
            }
            for (int a = 0; a < 24; a++)
                foreach (float k in new[] { 1f, 1.2f, .85f, 1.45f })
                    foreach (float up in new[] { height, height + .6f, height - .3f, height + 1.2f })
                    {
                        Vector3 dir = Quaternion.Euler(0, a * 15, 0) * Vector3.forward;
                        Vector3 p = centre + dir * distance * k + Vector3.up * up;
                        if (Physics.CheckSphere(p, .4f, Mask, QueryTriggerInteraction.Ignore)) continue;
                        int seen = points.Count(t => Sees(p, t + Vector3.up * .6f) && Sees(p, t + Vector3.up * .3f));
                        float score = seen * 10 + Vector3.Dot(dir, prefer) * 2 - Mathf.Abs(k - 1) * 3 - Mathf.Abs(up - height) - Blocked(p) * 12;
                        if (score > bestScore) { bestScore = score; best = p; }
                    }
            return best;
        }

        static List<StudioCameraTrack> Cameras(StudioStage stage, Plan plan)
        {
            var set = stage.set;
            var list = new List<StudioCameraTrack>();
            var sun = RenderSettings.sun ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).OrderByDescending(l => l.intensity).FirstOrDefault();
            Vector3 toSun = Flat(sun ? -sun.transform.forward : new Vector3(-.5f, 0, -.5f)).normalized;

            // A: the idle group, the same camera twice (alive off for the shot, then on).
            Vector3 middle = plan.group.Aggregate(Vector3.zero, (a, b) => a + b) / plan.group.Count;
            float spread = plan.group.Max(g => Vector3.Distance(Flat(g), Flat(middle)));
            float aDistance = Mathf.Max(3.4f, (spread + .7f) / Mathf.Tan(34 * Mathf.Deg2Rad));
            Vector3 aSpot = Spot(middle, aDistance, .9f, plan.group, toSun);
            foreach (bool on in new[] { false, true })
            {
                var c = Cam(on ? "A-on" : "A-off", .5f, 5.5f, 30);
                StudioMoves.LockedOff(c, .5f, stage.ToStage(aSpot), P(stage, middle + Vector3.up * .45f));
                c.aliveOff = !on;
                foreach (var m in set.marks.Take(plan.group.Count)) c.measure.Add("actor:" + m.id);
                list.Add(c);
            }
            Note($"A camera at {V(aSpot)}, {Vector3.Distance(aSpot, middle):0.0} m from the group, 30 mm.");

            // B: the run and the punch, close and from the side; and the hero's own eyes.
            Vector3 run = Flat(plan.rival - plan.hero).normalized, side = Vector3.Cross(Vector3.up, run);
            // Beside the end of the run, where the punch lands, so the hit is close.
            Vector3 mid = Vector3.Lerp(plan.hero, plan.rival, .72f);
            Vector3 bSpot = Spot(mid, 3.1f, .6f, new[] { plan.hero, mid, plan.rival }, Vector3.Dot(side, toSun) >= 0 ? side : -side);
            var b = Cam("B-close", .4f, 3.9f, 28);
            StudioMoves.LockedOff(b, .4f, stage.ToStage(bSpot), "actor:hero", new Vector3(0, .45f, 0));
            b.aims[0].blend = 0;
            b.measure.Add("actor:hero"); b.measure.Add("actor:rival");
            list.Add(b);
            var fp = Cam("B-firstPerson", .4f, 3.9f, 24);
            fp.behavior = "firstPerson"; fp.subject = "hero"; fp.motionBlur = 0;
            list.Add(fp);
            Note($"B camera at {V(bSpot)}.");

            // C: the look behind, close on one of the group from in front, with nobody in between.
            StudioMark turner = null; Vector3 cSpot = default; float cBest = float.MinValue;
            foreach (var m in set.marks.Take(plan.group.Count))
            {
                Vector3 at = stage.ToWorld(m.position);
                var others = set.marks.Where(o => o != m).Select(o => stage.ToWorld(o.position)).ToList();
                Vector3 front = Quaternion.Euler(0, m.yaw + 30, 0) * Vector3.forward;
                var spot = Spot(at, 2.3f, .5f, new[] { at }, front, others, out float score);
                if (score > cBest) { cBest = score; turner = m; cSpot = spot; }
            }
            var cc = Cam("C-lookBehind", .8f, 4.8f, 35);
            cc.subject = turner.id;   // who looks behind (LookBehind reads it)
            StudioMoves.LockedOff(cc, .8f, stage.ToStage(cSpot), "actor:" + turner.id, new Vector3(0, .45f, 0));
            cc.measure.Add("actor:" + turner.id);
            list.Add(cc);
            Note($"C camera at {V(cSpot)} on {turner.actor}.");
            return list;
        }

        // The look-behind layer for proof C: one of the group looks at a point straight behind him (a point: target, fix 4).
        static StudioMark Turner(StudioStage stage) =>
            stage.set.Mark(stage.set.cameras.FirstOrDefault(c => c.id == "C-lookBehind")?.subject ?? "") ?? stage.set.marks[0];

        static StudioOverride LookBehind(StudioStage stage)
        {
            var turner = Turner(stage);
            Vector3 feet = stage.ToWorld(turner.position);
            Vector3 behind = feet + Quaternion.Euler(0, turner.yaw + 172, 0) * Vector3.forward * 8 + Vector3.up * 1.2f;
            return new StudioOverride { actor = turner.id, kind = "lookAt", target = P(stage, behind), from = 1.4f, to = 4.2f, blendIn = .3f, blendOut = .5f };
        }

        // ---- Numbers ----

        // Longest time an actor holds perfectly still (head under 3 deg/s and feet under 2 cm/s), and the head's top speed.
        static (float still, float peak, float peakAt, float fastest) Stillness(StudioStage stage, string id, float from, float to)
        {
            int fps = stage.Fps;
            float longest = 0, run = 0, peak = 0, peakAt = 0, fastest = 0;
            StudioActorPoses.Pose last = default;
            for (int f = Mathf.RoundToInt(from * fps); f <= Mathf.RoundToInt(to * fps); f++)
            {
                var now = StudioActorPoses.At(stage.tape, id, f / (float)fps, fps);
                if (!now.found) { last = default; run = 0; continue; }
                if (last.found)
                {
                    float turn = Quaternion.Angle(last.headRotation, now.headRotation) * fps;
                    float move = Vector3.Distance(last.feet, now.feet) * fps;
                    if (turn > peak) { peak = turn; peakAt = f / (float)fps; }
                    fastest = Mathf.Max(fastest, move);
                    if (turn < 3 && move < .02f) { run += 1f / fps; longest = Mathf.Max(longest, run); } else run = 0;
                }
                last = now;
            }
            return (longest, peak, peakAt, fastest);
        }

        // Every shown frame of these actors, as bytes, hashed; played forward and backward must give the same.
        static string Replay(StudioStage stage, IEnumerable<string> ids, float from, float to, out bool same)
        {
            same = true;
            var lines = new StringBuilder();
            int fps = stage.Fps;
            foreach (string id in ids)
            {
                var track = stage.tape.Find(id); var take = track?.ActiveTake;
                if (take == null) continue;
                int a = Mathf.Max(take.start, Mathf.RoundToInt(from * fps)), b = Mathf.Min(take.End - 1, Mathf.RoundToInt(to * fps));
                byte[] Shown(int f)
                {
                    var pose = ActorFrame.Read(track.type, take.frames[f - take.start]);
                    foreach (var filter in ActorFrameFilters.For(stage, track)) filter.Apply(stage, track, f / (float)fps, pose);
                    return pose.Write();
                }
                var forward = new List<byte[]>();
                for (int f = a; f <= b; f++) forward.Add(Shown(f));
                int differ = 0;
                for (int f = b; f >= a; f--) if (!forward[f - a].SequenceEqual(Shown(f))) differ++;
                ulong hash = 1469598103934665603UL;
                foreach (var bytes in forward) foreach (byte x in bytes) { hash ^= x; hash *= 1099511628211UL; }
                if (differ > 0) same = false;
                lines.Append($"{id}: {forward.Count} frames, hash {hash:x16}, frames that differ played backward {differ}; ");
            }
            return lines.ToString();
        }

        static void Numbers(StudioStage stage, string label)
        {
            var set = stage.set;
            var group = set.marks.Where(m => m.id != "hero" && m.id != "rival").Select(m => m.id).ToList();
            bool was = set.alive.on;
            var table = new StringBuilder($"\n### Numbers ({label}), idle group, tape 0.5-5.5 s\n\n| actor | still OFF (s) | OFF peak head / feet | still ON (s) | peak head ON (deg/s) | ON peak feet (m/s) |\n|---|---|---|---|---|---|\n");
            float worstOff = 0, worstOn = 0, peakOn = 0;
            foreach (string id in group)
            {
                set.alive.on = false; var off = Stillness(stage, id, .5f, 5.5f);
                set.alive.on = true; var on = Stillness(stage, id, .5f, 5.5f);
                worstOff = Mathf.Max(worstOff, off.still); worstOn = Mathf.Max(worstOn, on.still); peakOn = Mathf.Max(peakOn, on.peak);
                table.Append($"| {set.Mark(id).actor} | {off.still:0.00} | {off.peak:0} deg/s / {off.fastest:0.00} m/s | {on.still:0.00} | {on.peak:0} at {on.peakAt:0.00} s | {on.fastest:0.00} |\n");
            }
            set.alive.on = was;
            Note(table.ToString());
            Note($"Longest still stretch of any idle actor: alive OFF {worstOff:0.00} s, alive ON {worstOn:0.00} s. Peak head turn speed with alive ON {peakOn:0} deg/s.");
            var hero = Stillness(stage, "hero", .5f, 5.5f);
            var rival = Stillness(stage, "rival", .5f, 2.2f);
            Note($"Hero (driven, alive on for hands and breath): longest still {hero.still:0.00} s, peak head {hero.peak:0} deg/s at {hero.peakAt:0.00} s. Rival before the punch: longest still {rival.still:0.00} s.");
            string replay = Replay(stage, set.marks.Select(m => m.id), 0, Length - .1f, out bool same);
            Check("Replay of the same seed is frame-identical (every actor, every frame, played forward then backward)", same, replay);
        }

        // ---- The run ----

        static IEnumerable<int> Shoot()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            yield return 0; yield return 0;
            Note($"# Alive actors proof, {Mode}, {DateTime.Now:yyyy-MM-dd HH:mm}\n");
            if (Mode == "record") { foreach (int w in Record(stage)) yield return w; yield break; }

            StudioOverrides.Resolve(set, stage.tape, stage.anchor);
            Numbers(stage, Mode);
            var cameras = set.cameras;
            var settings = new StudioRenderSettings { width = 1920, height = 1080, fps = Fps, subFrames = 1, format = "png", cinema = true, filmstrip = 12 };
            if (Mode == "renderA")
            {
                foreach (int w in Render(stage, cameras.Where(c => c.id.StartsWith("A-")).ToList(), settings, "renders")) yield return w;
                yield break;
            }
            foreach (int w in Render(stage, cameras.Where(c => c.id.StartsWith("B-")).ToList(), settings, "renders")) yield return w;
            set.overrides.Add(LookBehind(stage));
            StudioOverrides.Resolve(set, stage.tape, stage.anchor);
            foreach (int w in TurnLog(stage)) yield return w;
            foreach (int w in Render(stage, cameras.Where(c => c.id.StartsWith("C-")).ToList(), settings, "renders-c")) yield return w;
        }

        static IEnumerable<int> Render(StudioStage stage, List<StudioCameraTrack> shots, StudioRenderSettings settings, string folder)
        {
            var clock = Stopwatch.StartNew();
            var rows = new StringBuilder("time,head_yaw,rig_yaw\n");
            string turner = Turner(stage).id;
            void Watch(Camera _)
            {
                if (!shots.Any(s => s.id.StartsWith("C-"))) return;
                float t = StudioRun.Frame / (float)stage.Fps;
                var pose = StudioActorPoses.At(stage.tape, turner, t, stage.Fps);
                var rig = stage.playback.TryGetValue(turner, out var shown) && shown ? shown.GetComponentInChildren<CandyPlayerRig>() : null;
                rows.Append(string.Format(CultureInfo.InvariantCulture, "{0:0.000},{1:0.0},{2:0.0}\n", t, pose.headRotation.eulerAngles.y, rig ? rig.transform.eulerAngles.y : -1));
            }
            StudioRender.BeforeFrame += Watch;
            StudioRender.Start(shots, settings, Path.Combine(Folder, folder));
            try { while (StudioRender.Running) yield return 0; }
            finally { StudioRender.BeforeFrame -= Watch; }
            if (shots.Any(s => s.id.StartsWith("C-"))) File.WriteAllText(Path.Combine(Folder, "c-rig-yaw.csv"), rows.ToString());
            foreach (var r in StudioRender.Results) Note($"Rendered {r.camera}: {r.frames} frames {r.width}x{r.height} -> {r.movie} {r.error}");
            Note($"Render took {clock.Elapsed.TotalSeconds:0} s. {StudioRender.Progress}");
        }

        // Proof C numbers: how far the head turns from where the body faced, and when it gets there.
        static IEnumerable<int> TurnLog(StudioStage stage)
        {
            var turner = Turner(stage);
            var layer = stage.set.overrides.Last();
            StudioOverrides.Target(stage.set, stage.tape, stage.anchor, layer, 2.5f, out var point);
            float facing = turner.yaw;
            var rows = new StringBuilder("time,head_yaw_from_mark,target_yaw_from_mark\n");
            float reached = -1, most = 0;
            for (float t = .8f; t <= 4.8f; t += 1f / Fps)
            {
                var pose = StudioActorPoses.At(stage.tape, turner.id, t, Fps);
                float head = Mathf.DeltaAngle(facing, pose.headRotation.eulerAngles.y);
                float want = Mathf.DeltaAngle(facing, StudioHuman.Yaw(Flat(point - pose.head)));
                most = Mathf.Max(most, Mathf.Abs(head));
                if (reached < 0 && Mathf.Abs(Mathf.DeltaAngle(head, want)) < 12) reached = t;
                rows.Append(string.Format(CultureInfo.InvariantCulture, "{0:0.000},{1:0.0},{2:0.0}\n", t, head, want));
            }
            File.WriteAllText(Path.Combine(Folder, "c-head-yaw.csv"), rows.ToString());
            Check("C: a look at a point behind turns the head the whole way round (past the old 85 degree neck limit)", most > 150 && reached > 0,
                $"head turned up to {most:0} deg from the mark's facing; within 12 deg of the target from {reached:0.00} s (layer starts 1.40 s)");
            yield return 0;
        }

        static IEnumerable<int> Record(StudioStage stage)
        {
            var set = stage.set;
            var plan = MakePlan(stage);
            Direct(stage, plan);

            // Pass 1: the world and every mark as a plain stand-in (the hero's drive is muted).
            StudioRun.RecordWholeSet();
            foreach (int w in UntilHeld()) yield return w;
            StudioRun.SaveTape();
            Note("Pass 1 (whole set, stand-ins): " + StudioRun.Status);

            // Pass 2: the hero driven, with the rival live so the punch can land, against the played-back world.
            set.drives[0].muted = false;
            StudioLibrary.Save(set);
            var pass = StudioPerformer.Pass(stage, new[] { "hero", "rival" }, false);
            StudioRun.Record(pass);
            foreach (int w in UntilHeld()) yield return w;
            var log1 = StudioRun.CueLog.ToList();
            Note("Pass 2 (drive layer): " + StudioRun.Status + "\n" + string.Join("\n", log1.Select(l => "  - " + l)));
            var first = stage.tape.Find("hero").ActiveTake;

            // Pass 3: the same drive again, to see whether the driven take repeats.
            StudioRun.Record(StudioPerformer.Pass(stage, new[] { "hero", "rival" }, false));
            foreach (int w in UntilHeld()) yield return w;
            var second = stage.tape.Find("hero").ActiveTake;
            float apart = 0, turned = 0; int n = Mathf.Min(first.frames.Count, second.frames.Count);
            for (int i = 0; i < n; i++)
            {
                var x = ActorFrame.Read(SurvivorTrack.Id, first.frames[i]); var y = ActorFrame.Read(SurvivorTrack.Id, second.frames[i]);
                if (!x.visible || !y.visible) continue;
                apart = Mathf.Max(apart, Vector3.Distance(x.body.feet, y.body.feet));
                turned = Mathf.Max(turned, Quaternion.Angle(x.body.headRotation, y.body.headRotation));
            }
            Note($"Pass 3 (the same drive again): hero takes differ by up to {apart * 100:0.0} cm and {turned:0.0} deg over {n} frames.");
            StudioRun.SaveTape();

            // What the drive did.
            var hero = stage.tape.Find("hero").ActiveTake; var rival = stage.tape.Find("rival").ActiveTake;
            var path = hero.frames.Select(f => ActorFrame.Read(SurvivorTrack.Id, f)).ToList();
            var rivalPath = rival.frames.Select(f => ActorFrame.Read(SurvivorTrack.Id, f)).ToList();
            var log = StudioRun.CueLog.ToList();
            Note("Pass 3 log:\n" + string.Join("\n", log.Select(l => "  - " + l)));
            string punchLine = log.FirstOrDefault(l => l.Contains("punch "));
            float punchAt = punchLine != null && float.TryParse(punchLine.Substring(0, punchLine.IndexOf('s')), NumberStyles.Float, CultureInfo.InvariantCulture, out float p) ? p : -1;
            int pf = Mathf.Clamp(Mathf.RoundToInt(punchAt * Fps) - rival.start, 0, rivalPath.Count - 1);
            int later = Mathf.Clamp(pf + 15, 0, rivalPath.Count - 1);
            float thrown = punchAt < 0 ? 0 : Vector3.Distance(rivalPath[later].body.feet, rivalPath[pf].body.feet);
            float travelled = Vector3.Distance(Flat(path[0].body.feet), Flat(path[Mathf.Clamp(Mathf.RoundToInt(punchAt * Fps) - hero.start, 0, path.Count - 1)].body.feet));
            float lowest = path.Take(Mathf.Clamp(Mathf.RoundToInt((punchAt + .6f) * Fps) - hero.start, 1, path.Count)).Min(f => f.body.feet.y);
            // Head before feet: the frame the head first turned 20 degrees, and the frame the feet first moved 10 cm.
            int headFirst = -1, feetFirst = -1;
            for (int i = 1; i < path.Count; i++)
            {
                if (headFirst < 0 && Quaternion.Angle(path[0].body.headRotation, path[i].body.headRotation) > 20) headFirst = i;
                if (feetFirst < 0 && Vector3.Distance(Flat(path[0].body.feet), Flat(path[i].body.feet)) > .1f) feetFirst = i;
            }
            Check("B: the drive's punch went through the real glove (equipment command)", punchLine != null && punchLine.Contains("real glove punch"), punchLine ?? "no punch in the log");
            Check("B: the hero ran to the rival through the game's movement", travelled > 2.5f, $"{travelled:0.00} m from his mark to where he punched");
            Check("B: the rival was thrown by the punch", thrown > 1.0f, $"{thrown:0.00} m in the half second after the punch");
            Check("B: the hero stayed on the tower until after the punch", lowest > plan.hero.y - .6f, $"lowest feet {lowest:0.00} m (mark {plan.hero.y:0.00} m)");
            Check("B: the head moves before the feet", headFirst > 0 && feetFirst > 0 && headFirst < feetFirst, $"head turned 20 deg at {(hero.start + headFirst) / (float)Fps:0.00} s, feet moved 10 cm at {(hero.start + feetFirst) / (float)Fps:0.00} s");

            // The other fixes.
            var check = new StudioOverride { actor = set.marks[0].id, kind = "lookAt", target = "point:1,2", from = 1, to = 2 };
            set.overrides.Add(check);
            bool loud = false;
            try { StudioOverrides.Resolve(set, stage.tape, stage.anchor); } catch (InvalidOperationException error) { loud = error.Message.Contains("point:1,2"); }
            check.target = "nowhere:3";
            bool loud2 = StudioOverrides.TargetProblems(set).Any(x => x.Contains("nowhere:3"));
            set.overrides.Remove(check);
            Check("4: a broken or unknown override target stops Resolve and is listed in the Set's problems", loud && loud2, $"\"point:1,2\" stops Resolve: {loud}; \"nowhere:3\" listed: {loud2}");
            var behind = LookBehind(stage);
            set.overrides.Add(behind);
            var turnerTrack = stage.tape.Find(set.marks[0].id);
            var plain = ActorFrame.Read(SurvivorTrack.Id, turnerTrack.ActiveTake.frames[Mathf.RoundToInt(3.5f * Fps) - turnerTrack.ActiveTake.start]);
            var turned2 = ActorFrame.Read(SurvivorTrack.Id, turnerTrack.ActiveTake.frames[Mathf.RoundToInt(3.5f * Fps) - turnerTrack.ActiveTake.start]);
            StudioOverrides.Apply(set, stage.tape, stage.anchor, behind, 3.5f, behind.Weight(3.5f), turned2);
            StudioOverrides.Target(set, stage.tape, stage.anchor, behind, 3.5f, out var behindPoint);
            float error2 = Vector3.Angle(Flat(turned2.body.headRotation * Vector3.forward), Flat(behindPoint - plain.body.head));
            Check("4 and 3: a point: target works, and a look behind turns all the way", error2 < 10, $"head is {error2:0.0} deg from the point behind (was {Vector3.Angle(Flat(plain.body.headRotation * Vector3.forward), Flat(behindPoint - plain.body.head)):0} deg before the layer)");
            // The alive layer yields to an explicit look: at full weight the head goes where the look says.
            var looked = ActorFrame.Read(SurvivorTrack.Id, turnerTrack.ActiveTake.frames[Mathf.RoundToInt(3.5f * Fps) - turnerTrack.ActiveTake.start]);
            foreach (var filter in ActorFrameFilters.For(stage, turnerTrack)) filter.Apply(stage, turnerTrack, 3.5f, looked);
            float yieldError = Quaternion.Angle(looked.body.headRotation, turned2.body.headRotation);
            Check("2: the alive layer yields to an explicit look-at", yieldError < 3, $"with alive on, the head is {yieldError:0.0} deg from the look-at alone");
            set.overrides.Remove(behind);
            // Switches: per actor and per shot give exactly the alive-off pose.
            var probe = turnerTrack; float at = 2.2f;
            byte[] Pose() { var f = ActorFrame.Read(SurvivorTrack.Id, probe.ActiveTake.frames[Mathf.RoundToInt(at * Fps) - probe.ActiveTake.start]); foreach (var filter in ActorFrameFilters.For(stage, probe)) filter.Apply(stage, probe, at, f); return f.Write(); }
            set.alive.on = false; var offPose = Pose();
            set.alive.on = true; var onPose = Pose();
            set.alive.actors.Add(new StudioAliveActor { actor = probe.id, off = true }); var actorOff = Pose(); set.alive.actors.Clear();
            Check("2: the alive layer changes the pose, and switches off per actor", !onPose.SequenceEqual(offPose) && actorOff.SequenceEqual(offPose), $"on differs from off: {!onPose.SequenceEqual(offPose)}; actor switch gives the off pose: {actorOff.SequenceEqual(offPose)}");

            // Cameras, numbers, bundle, preview.
            set.cameras.Clear(); set.cameras.AddRange(Cameras(stage, plan));
            StudioLibrary.Save(set); StudioRun.SaveTape(); StudioRun.Refresh();
            Numbers(stage, "record");
            SaveBundle(set);
            var preview = new StudioRenderSettings { width = 640, height = 360, fps = 10, subFrames = 1, format = "png", cinema = false, filmstrip = 12 };
            foreach (int w in Render(stage, set.cameras.Where(c => c.id == "A-on" || c.id.StartsWith("B-")).ToList(), preview, "preview")) yield return w;
            set.overrides.Add(LookBehind(stage));
            StudioOverrides.Resolve(set, stage.tape, stage.anchor);
            foreach (int w in TurnLog(stage)) yield return w;
            foreach (int w in Render(stage, set.cameras.Where(c => c.id.StartsWith("C-")).ToList(), preview, "preview-c")) yield return w;
            set.overrides.Clear();
        }
    }
}
