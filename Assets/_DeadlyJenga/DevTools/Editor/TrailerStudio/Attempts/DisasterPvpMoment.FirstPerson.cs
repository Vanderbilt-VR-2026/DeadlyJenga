using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Proof renders for the first-person camera fix, on the committed disaster take (it replays exactly):
    //   ...DisasterPvpMoment.RunFirstPersonPunch   the hero's glove punch through his eyes, old and new camera,
    //                                              plus the third-person S01 and S07 shots unchanged (regression)
    //   ...DisasterPvpMoment.RunFirstPersonMeteor  a survivor looks up as a rock comes in and throws him, old and new
    // Output: Logs/ClaudeDisasterPvp/<mode>/ (mp4s, every 10th frame, checks.md).
    public static partial class DisasterPvpMoment
    {
        public static void RunFirstPersonPunch() => Begin("fpPunch");
        public static void RunFirstPersonMeteor() => Begin("fpMeteor");

        // A first-person camera on an actor. raw = the old behavior (straight head lock, no own-body view).
        static StudioCameraTrack FirstPersonCam(string id, string mark, int fromFrame, int toFrame, bool raw)
        {
            var c = StudioMoves.Camera(id, TapeF(fromFrame), TapeF(toFrame), 24);
            c.id = id; c.sensorHeight = 24;
            c.behavior = "firstPerson"; c.subject = mark;
            c.vignette = .15f; c.grain = .03f; c.contrast = 10; c.saturation = 8;
            c.motionBlur = 0;   // the game has none; blurred hands next to the lens read as a smear
            if (raw)
            {
                c.fpSmoothMove = 0; c.fpSmoothTurn = 0; c.fpLevel = false; c.fpOwnBody = false;
                c.fpFieldOfView = 62;   // same width as the new camera, so the two can be compared frame for frame
            }
            return c;
        }

        static IEnumerable<int> FirstPersonProof(StudioStage stage, Plan plan, string mode)
        {
            var cameras = ShotCameras(stage, plan);
            stage.set.cameras.Clear(); stage.set.cameras.AddRange(cameras);
            var shots = new List<StudioCameraTrack>();
            var checks = new StringBuilder($"# First-person proof, {mode}\n\n");
            string hero = plan.Id("hero");
            int from, to;
            string subject;
            if (mode == "fpPunch")
            {
                subject = hero; from = 4; to = 109;
                shots.Add(cameras.First(c => c.id == "S01"));
                shots.Add(cameras.First(c => c.id == "S07"));
            }
            else
            {
                // Who sees a rock coming: green or pink (the fox and the dragon, thrown at f249), whichever a rock
                // passes closest to. The rock is found in the recorded hazard track.
                var (who, lookAt, contact, rockSeen) = IncomingRock(stage, plan, new[] { "green", "pink" }, 196, 258);
                if (who == null) throw new InvalidOperationException("No rock passes near green or pink between f196 and f258.");
                subject = plan.Id(who);
                from = Mathf.Max(contact - 48, 150); to = from + 96;
                // Direction for this proof only: the survivor looks up at the rock's path (the take had him looking at nobody).
                stage.set.overrides.RemoveAll(o => o.actor == subject && o.kind == "lookAt" && o.to > TapeF(from - 10) && o.from < TapeF(contact + 3));
                stage.set.overrides.Add(new StudioOverride { actor = subject, kind = "lookAt", target = P(stage, lookAt), from = TapeF(from - 10), to = TapeF(contact + 3), blendIn = .3f, blendOut = .3f });
                StudioLibrary.Save(stage.set);
                StudioRun.Refresh();
                checks.Append($"Meteor moment: {who} ({subject}); the rock is first seen at f{rockSeen}, passes closest at f{contact}. Added a lookAt layer f{from - 10}-f{contact + 3} toward {V(lookAt)} (a point on the rock's path).\n");
                shots.Add(cameras.First(c => c.id == "S07"));
                yield return 0;
            }
            string tag = mode == "fpPunch" ? "punch" : "meteor";
            var before = FirstPersonCam("FP-" + tag + "-before", subject, from, to, true);
            var after = FirstPersonCam("FP-" + tag + "-after", subject, from, to, false);
            shots.Add(before); shots.Add(after);
            stage.set.cameras.Add(before); stage.set.cameras.Add(after);
            checks.Append($"First-person window: f{from}-f{to} on {subject}.\n\n");
            MeasureEyes(stage, before, after, from, to, checks);

            // Which rig each camera lent itself to, checked while the render runs.
            var lentSeen = new Dictionary<string, string>();
            var hidden = new HashSet<Renderer>();
            StudioCameraTrack current = null;
            void Watch(Camera camera)
            {
                HideStrays(stage, plan, hidden);
                current = StudioRender.Results.Count < shots.Count ? shots[StudioRender.Results.Count] : null;
                if (current == null || lentSeen.ContainsKey(current.id)) return;
                var rig = StudioFirstPerson.LentTo(camera);
                lentSeen[current.id] = rig ? rig.transform.root.name + $" (near clip {camera.nearClipPlane:0.00})" : $"none (near clip {camera.nearClipPlane:0.00})";
            }
            var settings = new StudioRenderSettings { width = 1920, height = 1080, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 12 };
            string renders = Path.Combine(Folder, "renders");
            var clock = Stopwatch.StartNew();
            StudioRender.BeforeFrame += Watch;
            StudioRender.Start(shots, settings, renders);
            try { while (StudioRender.Running) yield return 0; }
            finally { StudioRender.BeforeFrame -= Watch; }
            Note($"Render {settings.width}x{settings.height}: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s.");

            checks.Append("\n## Own-body view during the render (the rig each camera looked out of)\n");
            foreach (var s in shots) checks.Append($"- {s.id}: {(lentSeen.TryGetValue(s.id, out var r) ? r : "not seen")}\n");
            bool lentOk = lentSeen.TryGetValue(after.id, out var a) && !a.StartsWith("none") && shots.Where(s => s != after).All(s => lentSeen.TryGetValue(s.id, out var o) && o.StartsWith("none"));
            checks.Append(lentOk ? "PASS only the new first-person camera drew the actor as its own player\n" : "FAIL the own-body view was not lent to exactly the new first-person camera\n");

            // Keep each shot's movie, filmstrip and every 10th frame (plus the last).
            string kept = Path.Combine(Folder, "shots");
            Directory.CreateDirectory(kept);
            foreach (var done in StudioRender.Results.ToList())
            {
                if (!string.IsNullOrEmpty(done.movie) && File.Exists(done.movie)) File.Copy(done.movie, Path.Combine(kept, done.cameraId + ".mp4"), true);
                if (File.Exists(Path.Combine(done.folder, "filmstrip.png"))) File.Copy(Path.Combine(done.folder, "filmstrip.png"), Path.Combine(kept, done.cameraId + "-filmstrip.png"), true);
                var shot = shots.FirstOrDefault(s => s.id == done.cameraId);
                int startFrame = shot != null ? Mathf.RoundToInt((shot.start - TapeAtZero) * Fps) : 0;
                for (int n = 0; n < done.frames; n++)
                {
                    if (n % 10 != 0 && n != done.frames - 1) continue;
                    string file = Path.Combine(done.folder, $"{n:D05}.png");
                    if (File.Exists(file)) File.Copy(file, Path.Combine(kept, $"{done.cameraId}-f{startFrame + n:D03}.png"), true);
                }
                checks.Append($"- {done.cameraId}: {done.frames} frames from trailer frame f{startFrame}, {done.seconds:0} s {done.error}\n");
            }
            File.WriteAllText(Path.Combine(Folder, "checks.md"), checks.ToString());
            Note(checks.ToString());
            if (Directory.Exists(renders)) Directory.Delete(renders, true);
            if (!lentOk) throw new InvalidOperationException("The own-body view check failed (see checks.md).");
        }

        // How the old and new eyes move over the window: roll, turning speed, how far from the head.
        static void MeasureEyes(StudioStage stage, StudioCameraTrack before, StudioCameraTrack after, int from, int to, StringBuilder checks)
        {
            var set = stage.set;
            float maxRollBefore = 0, maxRollAfter = 0, turnBefore = 0, turnAfter = 0, jerkBefore = 0, jerkAfter = 0, farthest = 0;
            Quaternion? lastB = null, lastA = null; float lastSpeedB = 0, lastSpeedA = 0;
            float Roll(Quaternion q) { var right = q * Vector3.right; return Mathf.Abs(Mathf.Asin(Mathf.Clamp(right.y, -1, 1)) * Mathf.Rad2Deg); }
            for (int f = from; f <= to; f++)
            {
                float t = TapeF(f);
                var b = StudioCameras.Evaluate(before, set, stage.tape, stage.anchor, t, t);
                var a = StudioCameras.Evaluate(after, set, stage.tape, stage.anchor, t, t);
                maxRollBefore = Mathf.Max(maxRollBefore, Roll(b.rotation)); maxRollAfter = Mathf.Max(maxRollAfter, Roll(a.rotation));
                farthest = Mathf.Max(farthest, Vector3.Distance(a.position, b.position));
                if (lastB.HasValue)
                {
                    float sb = Quaternion.Angle(lastB.Value, b.rotation) * Fps, sa = Quaternion.Angle(lastA.Value, a.rotation) * Fps;
                    turnBefore = Mathf.Max(turnBefore, sb); turnAfter = Mathf.Max(turnAfter, sa);
                    jerkBefore += Mathf.Abs(sb - lastSpeedB); jerkAfter += Mathf.Abs(sa - lastSpeedA);
                    lastSpeedB = sb; lastSpeedA = sa;
                }
                lastB = b.rotation; lastA = a.rotation;
            }
            int n = Mathf.Max(1, to - from - 1);
            checks.Append("## The eye, measured every tape frame\n");
            checks.Append($"- Largest roll: old {maxRollBefore:0.0} deg, new {maxRollAfter:0.0} deg\n");
            checks.Append($"- Fastest turn: old {turnBefore:0} deg/s, new {turnAfter:0} deg/s\n");
            checks.Append($"- Average change of turning speed per frame (shake): old {jerkBefore / n:0.0}, new {jerkAfter / n:0.0} deg/s\n");
            checks.Append($"- New eye at most {farthest * 100:0} cm from the raw head\n");
            var lens = StudioCameras.Evaluate(after, set, stage.tape, stage.anchor, TapeF(from), TapeF(from));
            checks.Append($"- New lens: vertical field of view {lens.fieldOfView:0.0} deg, aperture f/{lens.aperture:0}, own body \"{lens.ownBody}\"\n");
            checks.Append((maxRollAfter < .5f ? "PASS" : "FAIL") + " the new eye keeps the horizon level\n");
            checks.Append((jerkAfter < jerkBefore ? "PASS" : "FAIL") + " the new eye shakes less than the raw head\n");
            checks.Append((farthest < .125f ? "PASS" : "FAIL") + " the new eye stays on the head (within 12 cm)\n\n");
        }

        // Among some roles, the one a recorded rock passes closest to between two frames: the role, a point on the
        // rock's path about 0.4 s before it is closest, the closest frame and the frame the rock is first seen.
        static (string role, Vector3 lookAt, int contact, int seen) IncomingRock(StudioStage stage, Plan plan, string[] roles, int fromFrame, int toFrame)
        {
            var hazards = stage.tape.FindType(HazardTrack.Id)?.ActiveTake;
            if (hazards == null) return (null, default, 0, 0);
            var paths = new Dictionary<string, SortedDictionary<int, Vector3>>();
            var contacts = new List<(int frame, Vector3 at)>();
            var seenContacts = new HashSet<string>();
            for (int f = fromFrame - 60; f <= toFrame; f++)
            {
                int tf = Mathf.RoundToInt(TapeF(f) * Fps);
                if (!hazards.Covers(tf)) continue;
                var data = hazards.frames[tf - hazards.start];
                if (data == null || data.Length == 0) continue;
                var sample = JsonUtility.FromJson<HazardSample>(Encoding.UTF8.GetString(data));
                foreach (var d in sample?.disasters ?? Array.Empty<DisasterFrame>())
                {
                    if (d == null || d.points == null) continue;
                    if (d.residualOnly)
                    {
                        foreach (var p in d.points) if (seenContacts.Add(d.kind + p.id)) contacts.Add((f, p.position));
                        continue;
                    }
                    foreach (var p in d.points)
                    {
                        string key = d.kind + p.id;
                        if (!paths.TryGetValue(key, out var path)) paths[key] = path = new SortedDictionary<int, Vector3>();
                        path[f] = p.position;
                    }
                }
            }
            // The landing (contact) nearest one of the roles inside the window.
            string bestRole = null; int contactFrame = 0; Vector3 contactAt = default; float bestDistance = 6f;
            foreach (string role in roles)
            {
                string id = plan.Id(role);
                if (id.Length == 0) continue;
                foreach (var (frame, at) in contacts)
                {
                    if (frame < fromFrame || frame > toFrame) continue;
                    var pose = StudioActorPoses.At(stage.tape, id, TapeF(frame), Fps);
                    if (!pose.found) continue;
                    float distance = Vector3.Distance(pose.feet, at);
                    if (distance < bestDistance) { bestDistance = distance; bestRole = role; contactFrame = frame; contactAt = at; }
                }
            }
            if (bestRole == null) return (null, default, 0, 0);
            // The rock that made it: a recorded flight path ending near the landing, else the aimed rock's own direction.
            foreach (var path in paths.Values)
            {
                int last = path.Keys.Where(k => k <= contactFrame + 1).DefaultIfEmpty(-1).Max();
                if (last < 0 || contactFrame - last > 3 || Vector3.Distance(path[last], contactAt) > 5) continue;
                int earlier = path.Keys.Where(k => k <= last - 12).DefaultIfEmpty(path.Keys.First()).Max();
                if (Vector3.Distance(path[earlier], contactAt) < 4) continue;
                return (bestRole, path[earlier], contactFrame, path.Keys.First());
            }
            int nearest = Enumerable.Range(0, plan.meteors.Count).OrderBy(i => Vector3.Distance(plan.meteors[i].target, contactAt)).DefaultIfEmpty(-1).First();
            Vector3 from = nearest >= 0 ? plan.meteorFrom[nearest] : Vector3.up;
            return (bestRole, contactAt + from * 15, contactFrame, contactFrame - 40);
        }
    }
}
