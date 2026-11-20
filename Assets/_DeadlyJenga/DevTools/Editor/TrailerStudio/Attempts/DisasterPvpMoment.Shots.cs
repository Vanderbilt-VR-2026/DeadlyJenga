using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

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

        static readonly Shot[] Shots =
        {
            new Shot { id = "S01", what = "hook: the glove punch, rival knocked off, hero taunts", cutIn = 0, cutOut = 42, key = 25 },
            new Shot { id = "S02", what = "the rival sails off the tower", cutIn = 42, cutOut = 60, key = 9 },
            new Shot { id = "S03", what = "teach: the whole top is a brawl (blaster, rocket)", cutIn = 60, cutOut = 104, key = 30 },
            new Shot { id = "S04", what = "anticipation: low, rocks streak in over the fighters", cutIn = 104, cutOut = 134, key = 20 },
            new Shot { id = "S05", what = "core verb: a meteor throws purple, then green", cutIn = 134, cutOut = 200, key = 20 },
            new Shot { id = "S06", what = "escalate: pink thrown", cutIn = 200, cutOut = 232, key = 18 },
            new Shot { id = "S07", what = "escalate: cyan thrown, rocks behind", cutIn = 232, cutOut = 262, key = 12 },
            new Shot { id = "S08", what = "escalate: the hero, rocks landing behind him", cutIn = 262, cutOut = 284, key = 8 },
            new Shot { id = "S09", what = "the planet killer looms over the tower", cutIn = 284, cutOut = 294, key = 5 },
            new Shot { id = "S10", what = "stop-down: the hero looks up", cutIn = 294, cutOut = 308, key = 6 },
            new Shot { id = "S11", what = "payoff and title: the planet killer lands on the summit", cutIn = 308, cutOut = 450, key = 6 },
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
            Victim("S06", 26, 5.5f);
            Victim("S07", 26, 5.5f);
            // S08: the hero, medium shot from the sun side, the rocks landing behind him.
            {
                var s = S("S08"); var c = ShotCam(s, 35);
                Vector3 hero = Feet(stage, plan, "hero", s.cutIn);
                Vector3 at = PickSpot(new[] { 0f, 20f, -20f }.Select(t => hero + Quaternion.AngleAxis(t, up) * a.side * 2.4f + up * .7f), hero + up * .8f);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, hero + up * .55f - a.side * .3f));
                Add(c, at, hero, .94f);
            }
            // S09: wide from the sun side, the planet killer coming over the tower top.
            {
                var s = S("S09"); var c = ShotCam(s, 18); c.motionBlur = 0;
                Vector3 at = C + a.side * 32 + a.along * 6 - up * 6;
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, C + up * 9 - a.side * 8));
                Add(c, at, C, 2);
            }
            // S10: the hero's face tipped up at it, from in front and below.
            {
                var s = S("S10"); var c = ShotCam(s, 40);
                var pose = StudioActorPoses.At(stage.tape, plan.Id("hero"), TapeF(s.cutIn + s.key), Fps);
                Vector3 head = pose.found ? pose.head : hero0 + up * .8f;
                Vector3 toRock = Flat(plan.planetFrom);
                Vector3 at = PickSpot(new[] { 25f, -25f, 45f, -45f }.Select(t => head + Quaternion.AngleAxis(t, up) * toRock * 1.4f - up * .35f), head);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, head + up * .05f));
                foreach (var k in c.keys) { k.aperture = 4; k.focus = Vector3.Distance(at, head); }
                Add(c, at, head, .45f);
            }
            // S11: the impact and the wreck, wide from the sun side; the title goes over it.
            {
                var s = S("S11"); var c = ShotCam(s, 18);
                // Aimed lower than record 1 (which ended on empty sky once the top fell): the stump and the sea stay in frame.
                Vector3 at = C + a.side * 58 + a.along * 14 - up * 20;
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, C - up * 4 - a.side * 6));
                StudioMoves.Shake(c, TapeF(PlanetFrame) + 1f / Fps, 2.2f, 1.4f);
                Add(c, at, C, 2);
            }
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
            if (mode == "finalB") cameras = cameras.Skip(4).Take(4).ToList();
            if (mode == "finalC") cameras = cameras.Skip(8).ToList();
            var settings = final
                ? new StudioRenderSettings { width = 1920, height = 1080, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 12 }
                : new StudioRenderSettings { width = 640, height = 360, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 8 };
            string renders = Path.Combine(Folder, "renders");
            var clock = Stopwatch.StartNew();
            StudioRender.Start(cameras, settings, renders);
            foreach (int wait in UntilRendered()) yield return wait;
            Note($"Render {settings.width}x{settings.height}: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s.");
            KeepShots(StudioRender.Results.ToList());
            string audio = StudioRender.AudioFile(stage.set.id);
            if (File.Exists(audio)) File.Copy(audio, Path.Combine(Folder, "tape.wav"), true);
            File.WriteAllText(Path.Combine(Folder, "shots.txt"), string.Join("\n", Shots.Select(s => $"{s.id} {s.cutIn} {s.cutOut} {s.key} {s.tapeShift} {Handle}")) + "\n");
            if (Directory.Exists(renders)) Directory.Delete(renders, true);
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
