using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // What happened in the take (the event table every camera and layer is timed from), and how alive every actor is.
    public static partial class DisasterPvp2
    {
        [Serializable] sealed class HazardSample { public DisasterFrame[] disasters; }

        // Tape frames of the key events, found in the take (not the plan).
        sealed class Events
        {
            public int punch1 = -1, punch2 = -1, wideRock = -1, rock2 = -1, planet = -1;
            public readonly Dictionary<string, int> respawn = new Dictionary<string, int>();   // first frame back on the tower after a fall
            public readonly Dictionary<string, int> gone = new Dictionary<string, int>();      // first frame 25 m below the summit
            public readonly List<(int frame, string kind, Vector3 at, float size)> impacts = new List<(int, string, Vector3, float)>();
        }

        static ActorFrame Shown(StudioStage stage, string id, int tapeFrame, bool layers = true)
        {
            var track = stage.tape.Find(id); var take = track?.ActiveTake;
            if (take == null || !take.Covers(tapeFrame)) return null;
            var pose = ActorFrame.Read(track.type, take.frames[tapeFrame - take.start]);
            if (layers) foreach (var filter in ActorFrameFilters.For(stage, track)) filter.Apply(stage, track, tapeFrame / (float)Fps, pose);
            return pose;
        }

        static Events FindEvents(StudioStage stage, Plan plan)
        {
            var e = new Events();
            foreach (string line in cueLog)
            {
                if (!line.Contains("drive hero: punch")) continue;
                if (!float.TryParse(line.Substring(0, line.IndexOf('s')), NumberStyles.Float, CultureInfo.InvariantCulture, out float t)) continue;
                int f = Mathf.RoundToInt(t * Fps);
                if (line.Contains("REJECTED") || line.Contains("refused")) { Note("Punch not landed: " + line); continue; }
                if (e.punch1 < 0) e.punch1 = f; else if (e.punch2 < 0) e.punch2 = f;
            }
            var hazards = stage.tape.FindType(HazardTrack.Id)?.ActiveTake;
            var seen = new HashSet<string>();
            if (hazards != null)
                for (int tf = hazards.start; tf < hazards.End; tf++)
                {
                    var data = hazards.frames[tf - hazards.start];
                    if (data == null || data.Length == 0) continue;
                    var sample = JsonUtility.FromJson<HazardSample>(Encoding.UTF8.GetString(data));
                    foreach (var d in sample?.disasters ?? Array.Empty<DisasterFrame>())
                        if (d != null && d.points != null && d.residualOnly)
                            foreach (var p in d.points)
                                if (seen.Add(d.kind + p.id)) e.impacts.Add((tf, d.kind, p.position, p.size));
                }
            int Near(Vector3 where, float within, int plannedTape, bool planet = false) =>
                e.impacts.Where(i => (planet ? i.kind.Contains("Planet") : !i.kind.Contains("Planet")) && Vector3.Distance(i.at, where) < within && Mathf.Abs(i.frame - plannedTape) < 45)
                    .Select(i => i.frame).DefaultIfEmpty(-1).Min();
            e.wideRock = Near(plan.wideRock, 5, TapeFrame(WideRockFrame));
            e.rock2 = Near(plan.rock2, 5, TapeFrame(Rock2Frame));
            e.planet = e.impacts.Where(i => i.kind.Contains("Planet")).Select(i => i.frame).DefaultIfEmpty(-1).Min();
            // Falls and respawns: deaths are off, so a survivor who falls in the sea comes back on the tower.
            foreach (string role in Roles)
            {
                var track = stage.tape.Find(role); var take = track?.ActiveTake;
                if (take == null) continue;
                Vector3? last = null;
                for (int tf = take.start; tf < take.End; tf++)
                {
                    var pose = ActorFrame.Read(track.type, take.frames[tf - take.start]);
                    if (!pose.visible) { last = null; continue; }
                    if (!e.gone.ContainsKey(role) && pose.body.feet.y < plan.arena.top - 25) e.gone[role] = tf;
                    if (last.HasValue && pose.body.feet.y - last.Value.y > 4 && !e.respawn.ContainsKey(role)) e.respawn[role] = tf;
                    last = pose.body.feet;
                }
            }
            Note($"Events (tape frames; trailer = tape - {TapeFrame(0)} before shifts): punch1 {e.punch1} (plan {TapeFrame(Punch1Frame)}), wide rock {e.wideRock} (plan {TapeFrame(WideRockFrame)}), " +
                 $"rock2 {e.rock2} (plan {TapeFrame(Rock2Frame)}), punch2 {e.punch2} (plan {TapeFrame(Punch2Frame)}), planet {e.planet} (plan {TapeFrame(PlanetFrame)}). " +
                 $"Fell 25 m: {string.Join(", ", e.gone.Select(g => g.Key + " " + g.Value))}. Respawned: {string.Join(", ", e.respawn.Select(g => g.Key + " " + g.Value))}.");
            return e;
        }

        static (float pitch, float yaw, float roll) Angles(Quaternion q)
        {
            var eu = q.eulerAngles;
            return (Mathf.DeltaAngle(0, eu.x), eu.y, Mathf.DeltaAngle(0, eu.z));
        }

        static void Report(StudioStage stage, Plan plan, Events e)
        {
            var csv = new StringBuilder("tape_frame,trailer_frame,role,visible,feet_x,feet_y,feet_z,head_x,head_y,head_z,head_pitch,head_yaw,head_roll,left_x,left_y,left_z,right_x,right_y,right_z,health\n");
            var lines = new StringBuilder("## Take report\n\n");
            foreach (string role in Roles)
            {
                if (stage.tape.Find(role) == null) continue;
                float lastHealth = -1; var drops = new List<string>();
                Vector3 start = default; bool started = false; int left = -1;
                for (int tf = TapeFrame(0) - 30; tf <= TapeFrame(LengthFrames); tf++)
                {
                    var p = Shown(stage, role, tf, false);
                    if (p == null) continue;
                    if (!p.visible) { csv.Append($"{tf},{tf - TapeFrame(0)},{role},0,,,,,,,,,,,,,,,,{p.health:0}\n"); continue; }
                    var (pitch, yaw, roll) = Angles(p.body.headRotation);
                    var b = p.body;
                    csv.Append(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},1,{3:0.000},{4:0.000},{5:0.000},{6:0.000},{7:0.000},{8:0.000},{9:0.0},{10:0.0},{11:0.0},{12:0.000},{13:0.000},{14:0.000},{15:0.000},{16:0.000},{17:0.000},{18:0}\n",
                        tf, tf - TapeFrame(0), role, b.feet.x, b.feet.y, b.feet.z, b.head.x, b.head.y, b.head.z, pitch, yaw, roll, b.left.position.x, b.left.position.y, b.left.position.z, b.right.position.x, b.right.position.y, b.right.position.z, p.health));
                    if (!started) { start = b.feet; started = true; }
                    if (left < 0 && (start.y - b.feet.y > 2.5f || Vector3.Distance(Flat0(b.feet), Flat0(start)) > 3.5f)) left = tf;
                    if (lastHealth >= 0 && p.health < lastHealth - .5f) drops.Add($"tape {tf} (trailer {tf - TapeFrame(0)}): {lastHealth:0} -> {p.health:0}");
                    lastHealth = p.health;
                }
                lines.Append($"- {role}: {(left >= 0 ? $"left its spot at tape {left} (trailer {left - TapeFrame(0)})" : "stayed on its spot")}; health drops: {(drops.Count > 0 ? string.Join("; ", drops) : "none")}" +
                    $"{(e.respawn.TryGetValue(role, out int rs) ? $"; respawned at tape {rs}" : "")}\n");
            }
            File.WriteAllText(Path.Combine(Folder, "take-actors.csv"), csv.ToString());
            lines.Append("\nImpacts within 30 m of the summit (tape frame, trailer frame, kind, where, size, nearest actor):\n");
            foreach (var i in e.impacts.Where(i => Vector3.Distance(i.at, plan.arena.C) < 30))
            {
                var nearest = Roles.Select(r => (r, pose: StudioActorPoses.At(stage.tape, r, i.frame / (float)Fps, Fps))).Where(x => x.pose.found).OrderBy(x => Vector3.Distance(x.pose.feet, i.at)).FirstOrDefault();
                lines.Append($"- tape {i.frame} (trailer {i.frame - TapeFrame(0)}) {i.kind} at {V(i.at)} ({Vector3.Distance(i.at, plan.arena.C):0.0} m from the summit), {i.size:0.0} m, nearest {nearest.r} {(nearest.r != null ? Vector3.Distance(nearest.pose.feet, i.at).ToString("0.0") : "-")} m\n");
            }
            var world = stage.tape.FindType(WorldTrack.Id)?.ActiveTake;
            if (world != null && WorldTrack.Positions(world, TapeFrame(0), out var first, out _))
            {
                lines.Append("\nBlocks moved more than 0.3 m since trailer frame 0 (every 15 frames): ");
                for (int f = 0; f <= LengthFrames; f += 15)
                {
                    if (!WorldTrack.Positions(world, Mathf.Min(TapeFrame(f), world.End - 1), out var now, out var on)) break;
                    int moved = 0; for (int i = 0; i < now.Length && i < first.Length; i++) if (on[i] && Vector3.Distance(now[i], first[i]) > .3f) moved++;
                    lines.Append($"f{f}:{moved} ");
                }
                foreach (int f in new[] { Punch2Frame, PlanetFrame - 4 })
                    if (WorldTrack.Positions(world, TapeFrame(f), out var pre, out var preOn))
                        lines.Append($"\nSummit block at trailer f{f}: moved {Vector3.Distance(pre[plan.arena.summit], first[plan.arena.summit]):0.00} m; block 2 moved {Vector3.Distance(pre[plan.arena.side2], first[plan.arena.side2]):0.00} m.");
                lines.Append("\n");
            }
            Note(lines.ToString());
        }

        // ---- How alive each visible actor is, per shot (L53, L55): measured on what is drawn (layers and alive applied) ----
        static void MotionTable(StudioStage stage, List<Shot> shots, List<StudioCameraTrack> cameras)
        {
            var table = new StringBuilder("shot,actor,visible_frames,longest_still_f,longest_level_f,peak_head_deg_s,action_starts\n");
            var md = new StringBuilder("\n### Per-actor motion (measured on the drawn poses, layers and alive applied)\n\n| shot | actor | visible f | longest still f | longest level-head f | peak head deg/s | action starts (shot frames) |\n|---|---|---|---|---|---|---|\n");
            var probe = new GameObject("motion probe").AddComponent<Camera>();
            probe.enabled = false; probe.aspect = 16f / 9;
            try
            {
                foreach (var shot in shots)
                {
                    var cam = cameras.FirstOrDefault(c => c.id == shot.id);
                    if (cam == null) continue;
                    foreach (string role in Roles)
                    {
                        if (stage.tape.Find(role) == null) continue;
                        var frames = new List<ActorFrame>();
                        var visible = new List<bool>();
                        for (int f = shot.cutIn; f < shot.cutOut; f++)
                        {
                            int tf = TapeFrame(f) + shot.shift;
                            var pose = Shown(stage, role, tf);
                            bool seen = false;
                            if (pose != null && pose.visible)
                            {
                                if (cam.behavior == "firstPerson") seen = cam.subject == role;
                                if (!seen)
                                {
                                    float t = tf / (float)Fps;
                                    StudioCameras.Apply(probe, StudioCameras.Evaluate(cam, stage.set, stage.tape, stage.anchor, t, t));
                                    foreach (var point in new[] { pose.body.head, pose.body.feet + Vector3.up * .45f })
                                    {
                                        var vp = probe.WorldToViewportPoint(point);
                                        if (vp.z > .1f && vp.x > -.02f && vp.x < 1.02f && vp.y > -.02f && vp.y < 1.02f && Sees(probe.transform.position, point)) { seen = true; break; }
                                    }
                                }
                            }
                            frames.Add(pose); visible.Add(seen);
                        }
                        int count = visible.Count(v => v);
                        if (count == 0) continue;
                        int still = 0, level = 0, levelRun = 0; float peak = 0; var starts = new List<int>();
                        int quiet = 99;
                        for (int i = 0; i < frames.Count; i++)
                        {
                            if (!visible[i]) { levelRun = 0; quiet = 99; continue; }
                            var b = frames[i].body;
                            var (pitch, _, roll) = Angles(b.headRotation);
                            levelRun = Mathf.Abs(pitch) <= 3 && Mathf.Abs(roll) <= 1 ? levelRun + 1 : 0;
                            level = Mathf.Max(level, levelRun);
                            // The longest run from frame i during which nothing moved past the still limits.
                            int j = i + 1;
                            while (j < frames.Count && visible[j])
                            {
                                var c = frames[j].body;
                                if (Vector3.Distance(c.feet, b.feet) >= .05f || Quaternion.Angle(c.headRotation, b.headRotation) >= 2 ||
                                    Vector3.Distance(c.left.position, b.left.position) >= .02f || Vector3.Distance(c.right.position, b.right.position) >= .02f) break;
                                j++;
                            }
                            still = Mathf.Max(still, j - i);
                            if (i > 0 && visible[i - 1])
                            {
                                var p0 = frames[i - 1].body;
                                float head = Quaternion.Angle(p0.headRotation, b.headRotation) * Fps;
                                float root = Vector3.Distance(p0.feet, b.feet) * Fps;
                                float hand = Mathf.Max(Vector3.Distance(p0.left.position - p0.feet, b.left.position - b.feet), Vector3.Distance(p0.right.position - p0.feet, b.right.position - b.feet)) * Fps;
                                peak = Mathf.Max(peak, head);
                                bool moving = head > 45 || root > .4f || hand > .7f;
                                if (moving && quiet >= 4) starts.Add(i);
                                quiet = moving ? 0 : quiet + 1;
                            }
                        }
                        table.Append($"{shot.id},{role},{count},{still},{level},{peak:0},{string.Join(" ", starts)}\n");
                        md.Append($"| {shot.id} | {role} | {count} | {still} | {level} | {peak:0} | {string.Join(" ", starts)} |\n");
                    }
                }
            }
            finally { Object.DestroyImmediate(probe.gameObject); }
            File.WriteAllText(Path.Combine(Folder, "motion.csv"), table.ToString());
            Note(md.ToString());
        }
    }
}
