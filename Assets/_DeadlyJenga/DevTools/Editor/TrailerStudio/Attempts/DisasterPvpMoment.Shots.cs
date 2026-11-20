using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The shots of the cut (filled in from the plan after the scout) and the run itself.
    public static partial class DisasterPvpMoment
    {
        sealed class Shot
        {
            public string id, what;
            public int cutIn, cutOut, key;
            public int tapeShift;   // trailer frames added to the tape time (to reuse a moment), normally 0
            public int Length => cutOut - cutIn;
        }

        // Cut against the committed take (record 4, job 457d098f): the hero's summit is shaken loose at about f192 and he
        // tumbles 38 m down the tower face onto the castle wall (f238), where he lives to see the planet killer land (f307).
        static readonly Shot[] Shots =
        {
            new Shot { id = "S01", what = "hook: the glove punch knocks the rival off the top", cutIn = 0, cutOut = 46, key = 25 },
            new Shot { id = "S02", what = "the rival sails off the tower", cutIn = 46, cutOut = 64, key = 6 },
            new Shot { id = "S03", what = "teach: the whole top is a brawl", cutIn = 64, cutOut = 104, key = 26 },
            new Shot { id = "S04", what = "anticipation: low, rocks streak in over the fighters", cutIn = 104, cutOut = 134, key = 24 },
            new Shot { id = "S05", what = "core verb: a meteor throws the astronaut off", cutIn = 134, cutOut = 180, key = 19 },
            new Shot { id = "S06", what = "escalate: the hero's summit gives way, he tumbles down the tower", cutIn = 180, cutOut = 240, key = 16 },
            new Shot { id = "S07", what = "escalate: a rock throws the fox and the dragon", cutIn = 240, cutOut = 268, key = 9 },
            new Shot { id = "S08", what = "the hero on the castle wall looks up", cutIn = 268, cutOut = 292, key = 14 },
            new Shot { id = "S09", what = "payoff: the planet killer lands on the tower", cutIn = 292, cutOut = 330, key = 15 },
            new Shot { id = "S10", what = "aftermath: the hero cowers on the wall under the falling wreck", cutIn = 330, cutOut = 352, key = 8 },
            new Shot { id = "S11", what = "aftermath and title: the tower folds into the sea", cutIn = 352, cutOut = 450, key = 7 },
        };

        static Vector3 Feet(StudioStage stage, Plan plan, string role, int frame)
        {
            var pose = StudioActorPoses.At(stage.tape, plan.Id(role), TapeF(frame), Fps);
            return pose.found ? pose.feet : plan.cast.TryGetValue(role, out var a) ? a.feet : plan.arena.centre;
        }

        static StudioCameraTrack ShotCam(Shot shot, float focal)
        {
            var c = StudioMoves.Camera(shot.id, TapeF(shot.cutIn - Handle + shot.tapeShift), TapeF(shot.cutOut + Handle + shot.tapeShift), focal);
            c.id = shot.id; c.sensorHeight = 24; c.keys[0].focal = focal;
            c.vignette = .2f; c.grain = .03f; c.contrast = 10; c.saturation = 8;
            c.motionBlur = MotionBlur;
            return c;
        }

        // Record 2 preview: at 0.35 the 45 m/s rocks near the lens smeared into long brown worms.
        const float MotionBlur = .15f;

        static List<StudioCameraTrack> ShotCameras(StudioStage stage, Plan plan)
        {
            var a = plan.arena; Vector3 up = Vector3.up, C = a.centre;
            var cams = new List<StudioCameraTrack>();
            var lines = new System.Text.StringBuilder("Shot cameras:\n");
            Shot S(string id) => Shots.First(s => s.id == id);
            void Add(StudioCameraTrack c, Vector3 at, Vector3 subject, float subjectHeight)
            {
                float d = Vector3.Distance(at, subject), hf = d * 24 / c.keys[0].focal;
                lines.Append($"- {c.id} at {V(at)}{(Clear(at) ? "" : " INSIDE GEOMETRY")}, {c.keys[0].focal:0} mm, subject {d:0.0} m: Hf {hf:0.00} m, fraction {subjectHeight / hf:0.00}\n");
                foreach (var who in plan.cast.Values) c.measure.Add("actor:" + who.id);
                cams.Add(c);
            }
            string Actor(string role) => "actor:" + plan.Id(role);
            Vector3 hero0 = Feet(stage, plan, "hero", 0), rival0 = Feet(stage, plan, "rival", 0);

            // S01: the punch in profile from the sun side, low, both fighters full height; slow push in.
            {
                var c = ShotCam(S("S01"), 32);
                Vector3 mid = Vector3.Lerp(hero0, rival0, .5f) + up * .5f;
                Vector3 at = PickSpot(new[] { 3.0f, 3.5f, 2.6f }.Select(d => mid + a.side * d - a.along * .4f + up * .1f), hero0 + up * .8f, rival0 + up * .8f);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, mid + a.along * .3f));
                StudioMoves.PushIn(c, c.start, c.end, stage.ToStage(mid), .12f);
                Add(c, at, mid, .94f);
            }
            // S02: the rival in the air, panned from beside the summit's far end, sea and sky behind.
            {
                var s = S("S02"); var c = ShotCam(s, 28);
                Vector3 flying = Feet(stage, plan, "rival", s.cutIn + s.key);
                // Beside his path, 8 m from where he is at the key, a little above, so the sea is below him (record 1: 17 m, 0.05 of frame).
                Vector3 at = flying + a.side * 7.5f - a.along * 2.5f + up * 2.5f;
                at = PickSpot(new[] { at, at + up * 2, at + a.side * 2 }, flying + up * .5f);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), Actor("rival"), up * .5f);
                Add(c, at, flying, .94f);
            }
            // S03: the brawl, high three-quarter from the sun side, every fighter in frame.
            {
                var s = S("S03"); var c = ShotCam(s, 20);
                var fighters = new[] { "blue", "green", "purple", "pink", "cyan", "hero" }.Where(r => plan.cast.ContainsKey(r)).Select(r => Feet(stage, plan, r, s.cutIn)).ToList();
                Vector3 centroid = fighters.Aggregate(Vector3.zero, (x, y) => x + y) / fighters.Count;
                Vector3 at = PickSpot(new[] { 0f, 15f, -15f, 30f }.Select(t => centroid + Quaternion.AngleAxis(t, up) * a.side * 6f + up * 2.6f), fighters.Select(f => f + up * .5f).ToArray());
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, centroid + up * .4f));
                Add(c, at, centroid, .94f);
            }
            // S04: low under the fighters' level, looking up past them into the sky the rocks come from.
            {
                var s = S("S04"); var c = ShotCam(s, 18); c.motionBlur = 0;
                Vector3 target = Feet(stage, plan, "purple", s.cutIn);
                Vector3 at = PickSpot(new[] { 4.5f, 5.5f, 3.8f }.Select(d => target + a.side * d - up * 1.6f), target + up * .6f);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, target + up * 2.2f - a.side * 2));
                Add(c, at, target, .94f);
            }
            // S05..S07: each victim side-on to the rock's path, the throw crossing the frame.
            void Victim(string id, float focal, float distance)
            {
                var s = S(id); var c = ShotCam(s, focal);
                c.motionBlur = 0;   // rocks pass close to these lenses; blurred they smeared into worms (record 3)
                // The rock that lands nearest this shot's key, and whoever (or wherever) it lands beside.
                int key = s.cutIn + s.key;
                int meteorIndex = Enumerable.Range(0, plan.meteors.Count).OrderBy(i => Mathf.Abs(plan.meteors[i].arriveFrame - key)).FirstOrDefault();
                string role = plan.meteors.Count > 0 ? plan.meteors[meteorIndex].label : "";
                Vector3 who = plan.cast.TryGetValue(role, out var victim) ? victim.feet : plan.meteors.Count > 0 ? plan.meteors[meteorIndex].target : C;   // where they stood before the hit
                lines.Append($"  {id}: rock f{(plan.meteors.Count > 0 ? plan.meteors[meteorIndex].arriveFrame : -1)} beside {role}\n");
                Vector3 travel = meteorIndex >= 0 && meteorIndex < plan.meteorFrom.Count ? Flat(-plan.meteorFrom[meteorIndex]) : a.side;
                // From outside the tower (record 2: cameras beside the victims sat inside the lattice and saw purple walls),
                // a little behind the rock's path, so the rock comes in past the lens and the victim is thrown away across frame.
                Vector3 outward = Flat(who - C);
                Vector3 look = who + travel * 1.5f + up * .7f;
                Vector3 at = PickSpot(new[] { 0f, 15f, -15f, 30f }.SelectMany(t => new[] { distance, distance * 1.3f }.Select(d => who + Quaternion.AngleAxis(t, up) * (outward * d - travel * 2.5f) + up * 1.8f)), who + up * .6f, who + travel * 3 + up * 1.2f);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, look));
                StudioMoves.Shake(c, TapeF(s.cutIn + s.key) + 1f / Fps, 1.2f, .5f);
                Add(c, at, who, .94f);
            }
            Victim("S05", 20, 6.5f);
            // S06: falling with the hero (fall_with): the camera rides 5 m out from him on the outside of the tower face.
            {
                var s = S("S06"); var c = ShotCam(s, 22);
                Vector3 start = Feet(stage, plan, "hero", 192), end = Feet(stage, plan, "hero", 238);
                Vector3 dir = Flat(end - start), perp = Vector3.Cross(up, dir).normalized;
                if (Vector3.Dot(perp, Flat(end - C)) < 0) perp = -perp;
                Vector3 offset = perp * 5f + up * 1.6f - dir * 1.2f;
                StudioMoves.Follow(c, Actor("hero"), offset, 0);
                StudioMoves.Aim(c, c.start, Actor("hero"), up * .45f, 0);
                lines.Append($"  S06 rides at {V(offset)} from the hero, fall {V(start)} -> {V(end)}\n");
                Add(c, start + offset, start, .94f);
            }
            Victim("S07", 24, 6f);
            // The hero on the castle wall (record 4: cameras at his head height sat inside the red wall).
            Vector3 heroWall = Feet(stage, plan, "hero", 270);
            var heroPose = StudioActorPoses.At(stage.tape, plan.Id("hero"), TapeF(282), Fps);
            Vector3 head = heroPose.found ? heroPose.head : heroWall + up * .8f;
            Vector3 sky = plan.planetTarget + plan.planetFrom * 55;
            Vector3 toSky = Flat(sky - head), toTower = Flat(C - heroWall);
            bool Roomy(Vector3 p) => !Physics.CheckSphere(p, .6f, ~0, QueryTriggerInteraction.Ignore);
            Vector3 PickRoomy(IEnumerable<Vector3> spots, Vector3 target)
            {
                foreach (var p in spots) if (Roomy(p) && !Physics.Linecast(p, target, (1 << Layers.Block) | (1 << Layers.Surface) | 1, QueryTriggerInteraction.Ignore)) return p;
                return PickSpot(spots, target);
            }
            // S09: the impact, from 32 m out on the sun side and below the summit, the rock against the tower top.
            {
                var s = S("S09"); var c = ShotCam(s, 18); c.motionBlur = 0;
                Vector3 at = C + a.side * 32 + a.along * 6 - up * 6;
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, C + up * 6 - a.side * 8));
                StudioMoves.Shake(c, TapeF(PlanetFrame) + 1f / Fps, 2.2f, 1.2f);
                Add(c, at, C, 2);
            }
            // S10 and S08: he stands in a corridor of the castle wall facing along it (preview 2: a camera toward his
            // head direction fell back to behind him). Both look at him from the open end of the corridor in front of him:
            // S10 wide (24 mm, 3 m) as he cowers, S08 an MCU (75 mm) of his face tipped up, from the same side.
            var late = StudioActorPoses.At(stage.tape, plan.Id("hero"), TapeF(338), Fps);
            Vector3 hLate = late.found ? late.head : head;
            Vector3 fLate = late.found ? Flat(late.headRotation * Vector3.forward) : toSky;
            Vector3 front = PickRoomy(new[] { 35f, -35f, 55f, -55f, 0f, 90f, -90f, 180f }.SelectMany(t => new[] { 3f, 3.8f }.Select(d => hLate + Quaternion.AngleAxis(t, up) * fLate * d + up * .1f)), hLate);
            Vector3 toFront = Flat(front - hLate);
            {
                var s = S("S08"); var c = ShotCam(s, 75);   // MCU: head and shoulders
                Vector3 at = PickRoomy(new[] { 3.2f, 2.8f, 3.6f }.Select(d => head + toFront * d - up * .1f), head);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, head - up * .1f));
                foreach (var k in c.keys) { k.aperture = 4; k.focus = Vector3.Distance(at, head); }
                Add(c, at, head, .94f);
            }
            {
                var s = S("S10"); var c = ShotCam(s, 24);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(front), P(stage, hLate + up * .5f));
                StudioMoves.Shake(c, c.start, 1.2f, .8f);
                Add(c, front, hLate, .94f);
            }
            // S11: the aftermath and the title, wide from the sun side; the stump, the castle and the sea stay in frame.
            {
                var s = S("S11"); var c = ShotCam(s, 18);
                Vector3 at = C + a.side * 58 + a.along * 14 - up * 20;
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, C - up * 4 - a.side * 6));
                Add(c, at, C, 2);
            }
            cams.Sort((x, y) => string.CompareOrdinal(x.id, y.id));
            Note(lines.ToString());
            return cams;
        }

        static IEnumerator<int> Shoot(string mode)
        {
            var stage = StudioRun.Stage;
            Note($"# Disaster PvP 1, {mode} run, version {Version}\n");
            var plan = MakePlan(stage);
            if (Recording)
            {
                Direct(stage, plan);
                yield return 0;
                var clock = Stopwatch.StartNew();
                StudioRun.RecordWholeSet();
                foreach (int wait in UntilHeld()) yield return wait;
                StudioRun.SaveTape();
                Note($"Recorded in {clock.Elapsed.TotalSeconds:0} s. {StudioRun.Status}");
                Note("Cue log:\n- " + string.Join("\n- ", StudioRun.CueLog));
                SaveBundle(stage.set);
            }
            else
            {
                CastFromSet(stage, plan);
                stage.set.overrides.Clear();
                AddLayers(stage, plan);
                StudioLibrary.Save(stage.set);
                StudioRun.Refresh();
                yield return 0;
            }
            try { Report(stage, plan); } catch (Exception error) { Note("Take report failed: " + error); }

            if (mode == "scout")
            {
                foreach (int wait in BlurCheck(stage, plan)) yield return wait;
                foreach (int wait in RenderScout(stage, plan)) yield return wait;
                yield break;
            }
            foreach (int wait in RenderShots(stage, plan, mode)) yield return wait;
        }

        static IEnumerable<int> RenderShots(StudioStage stage, Plan plan, string mode)
        {
            var cameras = ShotCameras(stage, plan);
            if (cameras.Count == 0) { Note("No shots planned yet."); yield break; }
            bool final = mode.StartsWith("final");
            if (mode == "finalA") cameras = cameras.Take(4).ToList();
            if (mode == "finalB") cameras = cameras.Skip(3).Take(5).ToList();   // S04-S08 (S04 again: its first render had the green disc)
            if (mode == "finalC") cameras = cameras.Skip(8).ToList();
            var settings = final
                ? new StudioRenderSettings { width = 1920, height = 1080, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 12 }
                : new StudioRenderSettings { width = 640, height = 360, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 8 };
            string renders = Path.Combine(Folder, "renders");
            var clock = Stopwatch.StartNew();
            StudioRender.Start(cameras, settings, renders);
            var hidden = new HashSet<Renderer>();
            while (StudioRender.Running) { HideStrays(stage, plan, hidden); yield return 0; }
            Note($"Render {settings.width}x{settings.height}: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s.");
            KeepShots(StudioRender.Results.ToList());
            string audio = StudioRender.AudioFile(stage.set.id);
            if (File.Exists(audio)) File.Copy(audio, Path.Combine(Folder, "tape.wav"), true);
            File.WriteAllText(Path.Combine(Folder, "shots.txt"), string.Join("\n", Shots.Select(s => $"{s.id} {s.cutIn} {s.cutOut} {s.key} {s.tapeShift} {Handle}")) + "\n");
            if (Directory.Exists(renders)) Directory.Delete(renders, true);
        }

        // A bright green disc floated in front of the lens in several shots of the bundle renders (not in the renders made
        // right after recording). Anything near the tower that is not a block, the map's own scenery or a played-back
        // actor, roughly 0.3-8 m across and saturated green, is switched off for the render and logged, so the report
        // says what it was.
        static void HideStrays(StudioStage stage, Plan plan, HashSet<Renderer> hidden)
        {
            var actors = new HashSet<Transform>(stage.playback.Values.Where(o => o).Select(o => o.transform));
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r || hidden.Contains(r) || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy) continue;
                int layer = r.gameObject.layer;
                if (layer == Layers.Block || layer == Layers.Surface) continue;
                var b = r.bounds;
                if (Vector3.Distance(b.center, plan.arena.centre) > 25 || b.size.magnitude < .3f || b.size.magnitude > 12) continue;
                var m = r.sharedMaterial;
                if (!m) continue;
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.color : Color.white;
                if (r is ParticleSystemRenderer ps && ps.TryGetComponent<ParticleSystem>(out var system)) { var main = system.main; c = main.startColor.color * c; }
                if (!(c.g > .55f && c.r < .45f && c.b < .45f)) continue;
                Transform t = r.transform; bool actor = false;
                while (t) { if (actors.Contains(t)) { actor = true; break; } t = t.parent; }
                if (actor) continue;
                hidden.Add(r); r.forceRenderingOff = true;
                string path = r.name; for (var p = r.transform.parent; p; p = p.parent) path = p.name + "/" + path;
                Note($"Hidden stray green renderer: {path} ({r.GetType().Name}, material '{m.name}', shader '{m.shader.name}', colour {c}, size {b.size.magnitude:0.0} m at {V(b.center)})");
            }
        }

        // Keep each shot's movie, metrics, filmstrip and its first, key and last stills; the frames are deleted.
        static void KeepShots(List<StudioRender.Finished> results)
        {
            string shots = Path.Combine(Folder, "shots");
            Directory.CreateDirectory(shots);
            foreach (var result in results)
            {
                var shot = Shots.FirstOrDefault(s => s.id == result.cameraId);
                if (shot == null) continue;
                Note($"{shot.id}: {result.frames} frames in {result.seconds:0} s {result.error}");
                if (!string.IsNullOrEmpty(result.movie) && File.Exists(result.movie)) File.Copy(result.movie, Path.Combine(shots, shot.id + ".mp4"), true);
                foreach (string name in new[] { "metrics.csv", "metrics.json", "filmstrip.png" })
                    if (File.Exists(Path.Combine(result.folder, name))) File.Copy(Path.Combine(result.folder, name), Path.Combine(shots, shot.id + "-" + name), true);
                void Still(string label, int frame)
                {
                    string file = Path.Combine(result.folder, $"{Mathf.Clamp(frame, 0, result.frames - 1):D05}.png");
                    if (File.Exists(file)) File.Copy(file, Path.Combine(shots, $"{shot.id}-{label}.png"), true);
                }
                Still("first", Handle); Still("key", Handle + shot.key); Still("last", Handle + shot.Length - 1);
            }
        }
    }
}
