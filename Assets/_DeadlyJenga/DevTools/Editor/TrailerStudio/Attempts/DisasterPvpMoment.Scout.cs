using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The scout: what happened in the take, what four cameras see every half second, and the motion blur colour check.
    public static partial class DisasterPvpMoment
    {
        [Serializable] sealed class HazardSample { public DisasterFrame[] disasters; }

        // Roles from the marks of a loaded Set (mark.actor holds the role).
        static void CastFromSet(StudioStage stage, Plan plan)
        {
            foreach (var mark in stage.set.marks)
            {
                if (!plan.cast.TryGetValue(mark.actor, out var who)) plan.cast[mark.actor] = who = new Actor { role = mark.actor };
                who.id = mark.id; who.feet = stage.ToWorld(mark.position); who.yaw = mark.yaw; who.color = mark.color; who.weapon = mark.equipment; who.bot = mark.role == StudioRole.Bot;
            }
            // The rocks as the take really had them (re-working them from the map can order the spare spots differently).
            plan.meteors.Clear(); plan.meteorFrom.Clear();
            foreach (var cue in stage.set.cues.Where(c => c.action == AimedMeteorCue.Id && c.hazard != "PlanetKillerMeteor").OrderBy(c => c.at))
            {
                Vector3 target = stage.ToWorld(cue.point);
                int arrive = Mathf.RoundToInt((cue.at + AimedMeteorCue.ArrivalDelay(cue) - TapeAtZero) * Fps);
                var near = plan.cast.Values.Where(c => c.role != "hero" && c.role != "rival").OrderBy(c => Vector3.Distance(c.feet, target)).FirstOrDefault();
                plan.meteors.Add((near != null && Vector3.Distance(near.feet, target) < 2.5f ? near.role : "spare", arrive, target));
                plan.meteorFrom.Add(cue.direction.normalized);
            }
        }

        // ---- The take report ----
        static void Report(StudioStage stage, Plan plan)
        {
            var csv = new StringBuilder("frame,role,visible,x,y,z,health,equipment\n");
            var events = new StringBuilder("Take report (trailer frames):\n");
            foreach (var who in plan.cast.Values)
            {
                var start = StudioActorPoses.At(stage.tape, who.id, TapeF(0), Fps);
                int left = -1; float lowest = 0, farthest = 0;
                var track = stage.tape.Find(who.id);
                for (int f = 0; f <= LengthFrames; f += 3)
                {
                    var pose = StudioActorPoses.At(stage.tape, who.id, TapeF(f), Fps);
                    float health = 0; string kit = "";
                    var take = track?.ActiveTake;
                    int tf = Mathf.RoundToInt(TapeF(f) * Fps);
                    if (take != null && take.Covers(tf)) { var frame = ActorFrame.Read(track.type, take.frames[tf - take.start]); health = frame.health; kit = frame.equipment.Length > 0 ? JsonUtility.FromJson<EquipmentState>(frame.equipment).kind.ToString() : ""; }
                    if (!pose.found || !start.found) { csv.Append($"{f},{who.role},0,,,,{health:0},{kit}\n"); continue; }
                    csv.Append($"{f},{who.role},1,{pose.feet.x:0.00},{pose.feet.y:0.00},{pose.feet.z:0.00},{health:0},{kit}\n");
                    float drop = start.feet.y - pose.feet.y, away = Vector3.Distance(Flat2(pose.feet), Flat2(start.feet));
                    lowest = Mathf.Max(lowest, drop); farthest = Mathf.Max(farthest, away);
                    if (left < 0 && (drop > 2.5f || away > 3f)) left = f;
                }
                events.Append($"- {who.role}: {(left >= 0 ? "left its spot at f" + left : "stayed")}, fell up to {lowest:0.0} m, moved up to {farthest:0.0} m sideways\n");
            }
            File.WriteAllText(Path.Combine(Folder, "take-actors.csv"), csv.ToString());

            // Meteors and impacts, from the hazard track.
            var hazards = stage.tape.FindType(HazardTrack.Id)?.ActiveTake;
            var seen = new HashSet<string>();
            var impacts = new StringBuilder("Impacts (frame, kind, where, size, nearest actor):\n");
            int maxRocks = 0, firstRock = -1;
            if (hazards != null)
                for (int f = 0; f <= LengthFrames; f++)
                {
                    int tf = Mathf.RoundToInt(TapeF(f) * Fps);
                    if (!hazards.Covers(tf)) continue;
                    var data = hazards.frames[tf - hazards.start];
                    if (data == null || data.Length == 0) continue;
                    var sample = JsonUtility.FromJson<HazardSample>(Encoding.UTF8.GetString(data));
                    foreach (var d in sample?.disasters ?? Array.Empty<DisasterFrame>())
                    {
                        if (d == null || d.points == null) continue;
                        if (!d.residualOnly)
                        {
                            int near = d.points.Count(p => Vector3.Distance(p.position, plan.arena.centre) < 60);
                            if (near > maxRocks) maxRocks = near;
                            if (firstRock < 0 && d.points.Length > 0) firstRock = f;
                            continue;
                        }
                        foreach (var p in d.points)
                        {
                            if (!seen.Add(d.kind + p.id)) continue;
                            var nearest = plan.cast.Values.Select(c => (c.role, pose: StudioActorPoses.At(stage.tape, c.id, TapeF(f), Fps))).Where(c => c.pose.found)
                                .OrderBy(c => Vector3.Distance(c.pose.feet, p.position)).FirstOrDefault();
                            float dist = Vector3.Distance(p.position, plan.arena.centre);
                            if (dist < 30) impacts.Append($"- f{f} {d.kind} at {V(p.position)} ({dist:0.0} m from the summit), {p.size:0.0} m, nearest {nearest.role} {(nearest.role != null ? Vector3.Distance(nearest.pose.feet, p.position).ToString("0.0") : "-")} m\n");
                        }
                    }
                }
            events.Append($"Rocks in the air near the tower: first at f{firstRock}, at most {maxRocks} at once.\n");

            // Blocks: how many moved, every half second.
            var world = stage.tape.FindType(WorldTrack.Id)?.ActiveTake;
            if (world != null && WorldTrack.Positions(world, Mathf.RoundToInt(TapeF(0) * Fps), out var first, out _))
            {
                events.Append("Blocks moved more than 0.3 m from the start (every 15 frames): ");
                for (int f = 0; f <= LengthFrames; f += 15)
                {
                    if (!WorldTrack.Positions(world, Mathf.Min(Mathf.RoundToInt(TapeF(f) * Fps), world.End - 1), out var now, out var on)) break;
                    int moved = 0; for (int i = 0; i < now.Length && i < first.Length; i++) if (on[i] && Vector3.Distance(now[i], first[i]) > .3f) moved++;
                    events.Append($"f{f}:{moved} ");
                }
                events.Append("\n");
                if (WorldTrack.Positions(world, Mathf.RoundToInt(TapeF(PlanetFrame - 3) * Fps), out var pre, out var preOn))
                    events.Append($"Summit block at f{PlanetFrame - 3}: moved {Vector3.Distance(pre[plan.arena.summit], first[plan.arena.summit]):0.0} m, still there: {preOn[plan.arena.summit]}\n");
            }
            Note(events.ToString());
            Note(impacts.ToString());
        }

        // ---- Scout cameras ----
        static StudioCameraTrack Cam(string id, int fromFrame, int toFrame, float focal)
        {
            var c = StudioMoves.Camera(id, TapeF(fromFrame), TapeF(toFrame), focal);
            c.id = id; c.sensorHeight = 24; c.keys[0].focal = focal;
            return c;
        }

        static Vector3 PickSpot(IEnumerable<Vector3> spots, params Vector3[] targets)
        {
            Vector3 best = spots.First(); int bestSeen = -1;
            foreach (var spot in spots)
            {
                if (!Clear(spot)) continue;
                int seen = targets.Count(t => !Physics.Linecast(spot, t, (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore));
                if (seen == targets.Length) return spot;
                if (seen > bestSeen) { bestSeen = seen; best = spot; }
            }
            return best;
        }

        static List<StudioCameraTrack> ScoutCameras(StudioStage stage, Plan plan)
        {
            var a = plan.arena; Vector3 up = Vector3.up, C = a.centre;
            var hero = plan["hero"]; var rival = plan["rival"];
            Vector3 heroHead = hero.feet + up * .8f, mid = Vector3.Lerp(hero.feet, rival.feet, .5f) + up * .6f;
            var list = new List<StudioCameraTrack>();
            Dir(a, 30, out var wideDir);
            var w = Cam("W-wide", 0, 345, 22);
            StudioMoves.LockedOff(w, w.start, stage.ToStage(C + wideDir * 75 + up * -22), P(stage, C - up * 9));
            list.Add(w);
            var arena = Cam("A-arena", 0, 345, 20);
            Vector3 at = PickSpot(new[] { 0f, 20f, -20f, 40f }.Select(d => C + Quaternion.AngleAxis(d, up) * (a.side * 8 + a.along * 2.5f) + up * 4.5f), C + up * .5f, hero.feet + up * .5f);
            StudioMoves.LockedOff(arena, arena.start, stage.ToStage(at), P(stage, C - up * 1.3f));
            list.Add(arena);
            var hook = Cam("H-hook", 0, 120, 28);
            at = PickSpot(new[] { 3.2f, 3.8f, 2.6f }.SelectMany(d => new[] { mid + a.side * d + up * .25f, mid - a.side * d + up * .25f }), heroHead, rival.feet + up * .7f);
            StudioMoves.LockedOff(hook, hook.start, stage.ToStage(at), P(stage, mid));
            list.Add(hook);
            var low = Cam("L-low", 0, 345, 18);
            at = PickSpot(new[] { 0f, 25f, -25f, 50f }.Select(d => C + Quaternion.AngleAxis(d, up) * (a.side * 9 - a.along * 2) - up * 7), C + up * .5f);
            StudioMoves.LockedOff(low, low.start, stage.ToStage(at), P(stage, C + up * 1f));
            list.Add(low);
            foreach (var c in list) foreach (var who in plan.cast.Values) c.measure.Add("actor:" + who.id);
            Note("Scout cameras: " + string.Join("; ", list.Select(c => $"{c.id} at {V(stage.ToWorld(c.keys[0].position))} {c.keys[0].focal} mm{(Clear(stage.ToWorld(c.keys[0].position)) ? "" : " INSIDE GEOMETRY")}")));
            return list;
        }

        static void Dir(Arena a, float degrees, out Vector3 dir) => dir = (a.side * Mathf.Cos(degrees * Mathf.Deg2Rad) + a.along * Mathf.Sin(degrees * Mathf.Deg2Rad)).normalized;

        static IEnumerable<int> RenderScout(StudioStage stage, Plan plan)
        {
            var cameras = ScoutCameras(stage, plan);
            var settings = new StudioRenderSettings { width = 480, height = 270, fps = 2, subFrames = 1, format = "png", cinema = false, filmstrip = 24 };
            string renders = Path.Combine(Folder, "scout");
            var clock = Stopwatch.StartNew();
            StudioRender.Start(cameras, settings, renders);
            foreach (int wait in UntilRendered()) yield return wait;
            Note($"Scout render: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s. " + string.Join("; ", StudioRender.Results.Select(r => $"{r.cameraId} {r.frames} frames {r.error}")));
            foreach (var r in StudioRender.Results)
            {
                string mp4 = r.movie; if (!string.IsNullOrEmpty(mp4) && File.Exists(mp4)) File.Delete(mp4);
            }
        }

        // ---- Motion blur colour check ----
        static IEnumerable<int> BlurCheck(StudioStage stage, Plan plan)
        {
            Diagnostics(stage, plan);
            var hero = plan["hero"];
            int frame = 12;
            var heroPose = StudioActorPoses.At(stage.tape, hero.id, TapeF(frame), Fps);
            Vector3 face = Flat(Quaternion.Euler(0, hero.yaw, 0) * Vector3.forward);
            Vector3 head = heroPose.head;
            Vector3 at = PickSpot(new[] { 40f, -40f, 60f, -60f }.Select(d => head + Quaternion.AngleAxis(d, Vector3.up) * face * 1.6f + Vector3.up * .1f), head);
            var cam = Cam("blur", frame, frame + 1, 45);
            StudioMoves.LockedOff(cam, cam.start, stage.ToStage(at), P(stage, head - Vector3.up * .25f));
            cam.end = cam.start + 1.5f / Fps;
            string folder = Path.Combine(Folder, "colour-check");
            Directory.CreateDirectory(folder);
            var results = new StringBuilder("Motion blur colour check (hero body colour, mean of a 9x9 patch at the chest, 0-255):\n");
            var originals = new Dictionary<Renderer, Material[]>();
            var motion = new Dictionary<Renderer, (MotionVectorGenerationMode mode, bool skinned)>();
            Material lit = null;
            var cast = plan.cast.Values.Select(c => StudioTargets.Object(stage, "actor:" + c.id)).Where(o => o).ToList();
            ScriptableRendererFeature ssao = FindFeature("ScreenSpaceAmbientOcclusion");
            var variants = new (string label, int width, float blur, int sub, Action before, Action after)[]
            {
                ("A-noblur-1080", 1920, 0, 1, null, null),
                ("B-blur-1080", 1920, .3f, 1, null, null),
                ("C-blur-540", 960, .3f, 1, null, null),
                ("D-blur-cameraonly-1080", 1920, .3f, 1, () => StudioRender.BlurMode = MotionBlurMode.CameraOnly, () => StudioRender.BlurMode = MotionBlurMode.CameraAndObjects),
                ("E-blur-no-object-vectors-1080", 1920, .3f, 1, () =>
                {
                    foreach (var r in cast.SelectMany(o => o.GetComponentsInChildren<Renderer>(true)))
                    { motion[r] = (r.motionVectorGenerationMode, r is SkinnedMeshRenderer s && s.skinnedMotionVectors); r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera; if (r is SkinnedMeshRenderer k) k.skinnedMotionVectors = false; }
                }, () => { foreach (var p in motion) if (p.Key) { p.Key.motionVectorGenerationMode = p.Value.mode; if (p.Key is SkinnedMeshRenderer k) k.skinnedMotionVectors = p.Value.skinned; } motion.Clear(); }),
                ("F-blur-no-ssao-1080", 1920, .3f, 1, () => { if (ssao) ssao.SetActive(false); }, () => { if (ssao) ssao.SetActive(true); }),
                ("G-blur-lit-body-1080", 1920, .3f, 1, () =>
                {
                    lit = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Blur check lit body", hideFlags = HideFlags.HideAndDontSave };
                    foreach (var r in cast.SelectMany(o => o.GetComponentsInChildren<Renderer>(true)))
                    {
                        var mats = r.sharedMaterials;
                        if (!mats.Any(m => m && m.shader && m.shader.name == "Candy Tropical/Avatar Health")) continue;
                        originals[r] = mats;
                        r.sharedMaterials = mats.Select(m => m && m.shader && m.shader.name == "Candy Tropical/Avatar Health" ? lit : m).ToArray();
                    }
                }, () => { foreach (var p in originals) if (p.Key) p.Key.sharedMaterials = p.Value; originals.Clear(); if (lit) Object.DestroyImmediate(lit); }),
                ("H-subframes4-1080", 1920, 0, 4, null, null),
            };
            foreach (var v in variants)
            {
                var copy = JsonUtility.FromJson<StudioCameraTrack>(JsonUtility.ToJson(cam));
                copy.id = v.label; copy.name = v.label; copy.motionBlur = v.blur;
                try { v.before?.Invoke(); } catch (Exception e) { results.Append($"  {v.label}: setup failed: {e.Message}\n"); }
                StudioRender.Start(new[] { copy }, new StudioRenderSettings { width = v.width, height = v.width * 9 / 16, fps = Fps, subFrames = v.sub, format = "png", cinema = false, filmstrip = 0 }, Path.Combine(Folder, "renders-colour"));
                foreach (int wait in UntilRendered()) yield return wait;
                try { v.after?.Invoke(); } catch (Exception e) { results.Append($"  {v.label}: restore failed: {e.Message}\n"); }
                var done = StudioRender.Results.FirstOrDefault();
                string png = done != null ? Path.Combine(done.folder, "00000.png") : "";
                if (File.Exists(png))
                {
                    File.Copy(png, Path.Combine(folder, v.label + ".png"), true);
                    results.Append($"  {v.label}: {Sample(stage, copy, png, heroPose)}\n");
                }
                else results.Append($"  {v.label}: no picture ({StudioRender.Progress})\n");
            }
            Note(results.ToString());
            string temp = Path.Combine(Folder, "renders-colour");
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }

        // The colour of the hero's chest in a rendered picture.
        static string Sample(StudioStage stage, StudioCameraTrack shot, string png, StudioActorPoses.Pose pose)
        {
            var picture = new Texture2D(2, 2);
            picture.LoadImage(File.ReadAllBytes(png));
            var temp = new GameObject("colour probe").AddComponent<Camera>();
            temp.enabled = false; temp.aspect = picture.width / (float)picture.height;
            StudioCameras.Apply(temp, StudioCameras.Evaluate(shot, stage.set, stage.tape, stage.anchor, shot.start, shot.start));
            Vector3 chest = Vector3.Lerp(pose.feet, pose.head, .45f);
            Vector3 vp = temp.WorldToViewportPoint(chest);
            Object.DestroyImmediate(temp.gameObject);
            int cx = Mathf.RoundToInt(vp.x * picture.width), cy = Mathf.RoundToInt(vp.y * picture.height);
            Color sum = Color.black; int n = 0;
            for (int y = cy - 4; y <= cy + 4; y++) for (int x = cx - 4; x <= cx + 4; x++)
                if (x >= 0 && y >= 0 && x < picture.width && y < picture.height) { sum += picture.GetPixel(x, y); n++; }
            Object.DestroyImmediate(picture);
            if (n == 0) return $"chest off screen ({vp})";
            sum /= n;
            return $"chest at ({cx},{cy}) rgb ({sum.r * 255:0},{sum.g * 255:0},{sum.b * 255:0})";
        }

        static ScriptableRendererFeature FindFeature(string name)
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!asset) return null;
            var list = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(asset) as ScriptableRendererData[];
            return list?.Where(d => d).SelectMany(d => d.rendererFeatures).FirstOrDefault(f => f && f.GetType().Name.Contains(name));
        }

        static void Diagnostics(StudioStage stage, Plan plan)
        {
            var line = new StringBuilder("Render set-up:\n");
            line.Append($"- quality level {QualitySettings.GetQualityLevel()} '{QualitySettings.names[QualitySettings.GetQualityLevel()]}', pipeline {GraphicsSettings.currentRenderPipeline?.name}\n");
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)
            {
                line.Append($"- HDR {asset.supportsHDR}, MSAA {asset.msaaSampleCount}, render scale {asset.renderScale}\n");
                var list = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(asset) as ScriptableRendererData[];
                foreach (var data in list ?? Array.Empty<ScriptableRendererData>())
                {
                    if (!data) continue;
                    string mode = data is UniversalRendererData u ? $"{u.renderingMode}, depth priming {u.depthPrimingMode}, copy depth {u.copyDepthMode}" : data.GetType().Name;
                    line.Append($"- renderer {data.name}: {mode}; features {string.Join(", ", data.rendererFeatures.Where(f => f).Select(f => f.name + (f.isActive ? "" : " (off)")))}\n");
                }
            }
            var heroObject = StudioTargets.Object(stage, "actor:" + plan.Id("hero"));
            if (heroObject)
                foreach (var r in heroObject.GetComponentsInChildren<Renderer>(true).Where(r => r is SkinnedMeshRenderer || r is MeshRenderer))
                {
                    var block = new MaterialPropertyBlock(); r.GetPropertyBlock(block);
                    line.Append($"- hero renderer {r.name} ({r.GetType().Name}, on {r.enabled && r.gameObject.activeInHierarchy}, motion {r.motionVectorGenerationMode}{(r is SkinnedMeshRenderer s ? ", skinned vectors " + s.skinnedMotionVectors : "")}, block colour {(block.HasColor("_BaseColor") ? block.GetColor("_BaseColor").ToString() : "-")}): ");
                    foreach (var m in r.sharedMaterials.Where(m => m))
                        line.Append($"[{m.name} / {m.shader.name}: passes {string.Join(",", Enumerable.Range(0, m.passCount).Select(i => m.GetPassName(i)))}; MotionVectors {m.FindPass("MotionVectors") >= 0}, DepthOnly {m.FindPass("DepthOnly") >= 0}, DepthNormals {m.FindPass("DepthNormals") >= 0}] ");
                    line.Append("\n");
                }
            else line.Append("- hero playback object not found\n");
            Note(line.ToString());
        }
    }
}
