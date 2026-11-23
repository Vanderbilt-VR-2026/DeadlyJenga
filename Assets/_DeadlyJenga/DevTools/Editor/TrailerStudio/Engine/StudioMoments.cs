using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    [Serializable] public sealed class StudioMoment
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "";
        public int frame, first, end;
        public int referenceFrame = -1;
        public string pov = "", witness = "";
        public StudioMomentSource source;
        public int ReferenceFrame => referenceFrame < 0 ? frame : referenceFrame;
    }

    [Serializable] public sealed class StudioMomentSource
    {
        public string setId, map, recordedAt;
        public int fps;
        public string[] tracks, takes;
        public int[] modes, starts, ends;
        public bool[] solo;

        public static StudioMomentSource Read(StudioTape tape)
        {
            var tracks = tape.tracks.Where(t => t.ActiveTake != null && !t.ActiveTake.trashed)
                .OrderBy(t => t.id, StringComparer.Ordinal).ToArray();
            return new StudioMomentSource
            {
                setId = tape.header.setId, map = tape.header.map, recordedAt = tape.header.created, fps = tape.header.fps,
                tracks = tracks.Select(t => t.id).ToArray(), takes = tracks.Select(t => t.ActiveTake.id).ToArray(),
                modes = tracks.Select(t => (int)t.mode).ToArray(), solo = tracks.Select(t => t.solo).ToArray(),
                starts = tracks.Select(t => t.ActiveTake.start).ToArray(), ends = tracks.Select(t => t.ActiveTake.End).ToArray()
            };
        }

        public bool Matches(StudioTape tape) => tape != null && JsonUtility.ToJson(this) == JsonUtility.ToJson(Read(tape));
    }

    public static class StudioMoments
    {
        [Serializable] public sealed class Review
        {
            public int referenceFrame, frames, fps;
            public StudioMomentSource source;
            public StudioCameraTrack pov, witness;
            public string povImage, witnessImage;
            public string settingsKey;
        }

        [Serializable] sealed class ReviewSettings
        {
            public List<StudioMark> cast;
            public List<StudioOverride> overrides;
            public List<StudioDrive> drives;
            public StudioAliveSettings alive;
            public StudioRenderSettings render;
            public string weather;
            public float timeOfDay;
        }

        internal static string SettingsKey(StudioSet set) => Hash128.Compute(JsonUtility.ToJson(new ReviewSettings
        {
            cast = set.marks, overrides = set.overrides, drives = set.drives, alive = set.alive,
            render = set.render, weather = set.weather, timeOfDay = set.timeOfDay
        })).ToString();

        public static bool Covers(StudioTake take, int frame) =>
            take != null && !take.trashed && take.Covers(frame);

        public static StudioMoment Create(StudioSet set, StudioTape tape, int frame, StudioMark actor, StudioCameraKey witnessKey)
        {
            var world = tape.FindType(WorldTrack.Id)?.ActiveTake;
            var body = tape.Find(actor.id)?.ActiveTake;
            if (actor.role == StudioRole.Hand || frame < 0 || frame >= set.Frames ||
                tape.header.setId != set.id || tape.header.fps != set.fps || tape.header.map != set.map ||
                !Covers(world, frame) || !Covers(body, frame))
                throw new InvalidOperationException("Choose a recorded survivor and a frame covered by both their take and the world take.");

            int fps = set.fps;
            var moment = new StudioMoment
            {
                name = $"Moment {set.moments.Count + 1}", frame = frame, source = StudioMomentSource.Read(tape),
                first = Mathf.Max(frame - fps, world.start, body.start),
                end = Mathf.Min(frame + 2 * fps, set.Frames, world.End, body.End)
            };
            float start = moment.first / (float)fps, end = moment.end / (float)fps;
            var pov = new StudioCameraTrack
            {
                name = moment.name + " · POV", start = start, end = end, behavior = "firstPerson", subject = actor.id,
                fpSmoothMove = 0, fpSmoothTurn = 0, fpLevel = false, color = set.cameras.Count % 8,
                keys = { new StudioCameraKey { time = frame / (float)fps, aperture = 16 } }
            };
            witnessKey.time = frame / (float)fps;
            var witness = new StudioCameraTrack
            {
                name = moment.name + " · Witness", start = start, end = end, keys = { witnessKey },
                color = (set.cameras.Count + 1) % 8
            };
            moment.pov = pov.id;
            moment.witness = witness.id;
            set.cameras.Add(pov);
            set.cameras.Add(witness);
            set.moments.Add(moment);
            return moment;
        }

        public static List<(float tape, float camera)> ReviewSamples(StudioMoment moment, int fps, bool moving,
            float earliest = 0, float latest = float.MaxValue)
        {
            var samples = new List<(float, float)>();
            float first = (moving ? moment.first : moment.ReferenceFrame) / (float)moment.source.fps;
            if (moving) first = Mathf.Max(first, earliest);
            float end = Mathf.Min(moment.end / (float)moment.source.fps, latest);
            int count = moving ? Mathf.Max(1, Mathf.CeilToInt((end - first) * fps - .0001f)) : 1;
            for (int i = 0; i < count; i++)
            {
                float sample = first + i / (float)fps;
                samples.Add((sample, sample));
            }
            return samples;
        }

        public static bool AddToCut(StudioSet set, StudioMoment moment, StudioCameraTrack camera)
        {
            int fps = set.render.fps;
            var timeline = StudioCameras.Timeline(camera, set, fps);
            float firstTime = moment.first / (float)moment.source.fps;
            float endTime = moment.end / (float)moment.source.fps;
            int first = timeline.FindIndex(t => t.tape >= firstTime - .0001f && t.tape < endTime - .0001f);
            if (first < 0) return false;
            int end = timeline.FindIndex(first, t => t.tape >= endTime - .0001f);
            if (end < 0) end = timeline.Count;
            set.cuts.Add(new StudioCut { camera = camera.id, from = first / (float)fps, to = end / (float)fps });
            return true;
        }

        public static string Folder(StudioSet set, StudioMoment moment) =>
            Path.Combine(StudioLibrary.TapeFolder(set.id), "moments", moment.id);

        public static bool CoversReference(StudioSet set, StudioMoment moment, StudioCameraTrack camera)
        {
            float time = moment.ReferenceFrame / (float)moment.source.fps;
            return time >= camera.start && time < camera.End(set);
        }

        internal static float AngleScore(StudioFraming.Sample sample)
        {
            if (!sample.onScreen || sample.occluded >= .85f) return 0;
            float visible = 1 - (sample.occluded < 0 ? .5f : sample.occluded);
            float size = 1 - Mathf.Clamp01(Mathf.Abs(sample.height - .35f) / .65f);
            float facing = sample.facing < 0 ? .5f : 1 - sample.facing / 180;
            return visible * (.6f + .3f * size + .1f * facing);
        }

        public static (StudioCameraTrack camera, string reason) Recommend(StudioStage stage, StudioMoment moment, StudioCameraTrack pov)
        {
            var set = stage.set;
            float time = moment.ReferenceFrame / (float)moment.source.fps;
            var probe = new GameObject("Trailer Studio framing probe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = probe.AddComponent<Camera>();
                camera.enabled = false;
                camera.aspect = set.render.width / (float)Mathf.Max(1, set.render.height);
                camera.nearClipPlane = .08f;
                StudioCameraTrack best = null;
                StudioFraming.Sample framing = null;
                float score = 0;
                foreach (var angle in set.cameras.Where(a => a.behavior != "firstPerson" && CoversReference(set, moment, a)))
                {
                    var lens = StudioCameras.Evaluate(angle, set, stage.tape, stage.anchor, time, time);
                    camera.transform.SetPositionAndRotation(lens.position, lens.rotation);
                    camera.fieldOfView = lens.fieldOfView;
                    var sample = StudioFraming.MeasureOne(camera, "actor:" + pov.subject, time);
                    float value = AngleScore(sample);
                    if (value <= score) continue;
                    best = angle;
                    framing = sample;
                    score = value;
                }
                if (best == null) return (null, "No saved witness angle has a clear view of this actor at the reference frame.");
                string visibility = framing.occluded < 0 ? "visibility uncertain" : $"{1 - framing.occluded:P0} clear";
                string direction = framing.facing < 0 ? "facing unknown" : $"{framing.facing:0}° from facing camera";
                return (best, $"{visibility} · {framing.height:P0} of frame height · {direction}");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }

        public static string ReviewFile(string root, bool moving) => Path.Combine(root, moving ? "motion.json" : "review.json");

        public static Review ReadReview(StudioSet set, StudioMoment moment, StudioTape tape, StudioCameraTrack pov,
            StudioCameraTrack witness, bool moving = false)
        {
            string root = Folder(set, moment);
            string file = ReviewFile(root, moving);
            if (!File.Exists(file) || !moment.source.Matches(tape)) return null;
            var review = JsonUtility.FromJson<Review>(File.ReadAllText(file));
            if (review?.source == null || !review.source.Matches(tape) || review.referenceFrame != moment.ReferenceFrame ||
                review.settingsKey != SettingsKey(set) ||
                JsonUtility.ToJson(review.pov) != JsonUtility.ToJson(pov) || JsonUtility.ToJson(review.witness) != JsonUtility.ToJson(witness))
                return null;
            return File.Exists(Path.Combine(root, review.povImage)) && File.Exists(Path.Combine(root, review.witnessImage)) ? review : null;
        }

        public static void RenderReview(StudioSet set, StudioTape tape, StudioMoment moment, StudioCameraTrack pov,
            StudioCameraTrack witness, bool moving, Action changed)
        {
            int reference = moment.ReferenceFrame;
            if (!moment.source.Matches(tape) || reference < 0 || reference >= set.Frames ||
                !CoversReference(set, moment, pov) || !CoversReference(set, moment, witness) ||
                !Covers(tape.Find(pov.subject)?.ActiveTake, reference) ||
                !StudioActorPoses.At(tape, pov.subject, reference / (float)set.fps, set.fps).found)
                throw new InvalidOperationException("This frame is unavailable on the recorded takes.");

            string root = Folder(set, moment), run = Guid.NewGuid().ToString("N");
            string folder = Path.Combine(root, run);
            float scale = (moving ? 320f : 640f) / Mathf.Max(1, Mathf.Max(set.render.width, set.render.height));
            int width = Mathf.Max(64, Mathf.RoundToInt(set.render.width * scale));
            int height = Mathf.Max(64, Mathf.RoundToInt(set.render.height * scale));
            int fps = moving ? 12 : set.fps;
            var samples = ReviewSamples(moment, fps, moving, Mathf.Max(pov.start, witness.start), Mathf.Min(pov.End(set), witness.End(set)));
            var povFrame = JsonUtility.FromJson<StudioCameraTrack>(JsonUtility.ToJson(pov));
            var witnessFrame = JsonUtility.FromJson<StudioCameraTrack>(JsonUtility.ToJson(witness));
            // Sparse proxy samples would otherwise create misleading streaks in the preview.
            povFrame.motionBlur = witnessFrame.motionBlur = 0;
            var review = new Review
            {
                source = moment.source, referenceFrame = reference, settingsKey = SettingsKey(set), fps = fps, frames = samples.Count,
                pov = JsonUtility.FromJson<StudioCameraTrack>(JsonUtility.ToJson(pov)),
                witness = JsonUtility.FromJson<StudioCameraTrack>(JsonUtility.ToJson(witness)),
                povImage = Path.Combine(run, StudioRender.ShotFolder(povFrame), "00000.png"),
                witnessImage = Path.Combine(run, StudioRender.ShotFolder(witnessFrame), "00000.png")
            };
            StudioRender.Start(new[] { povFrame, witnessFrame }, new StudioRenderSettings
            {
                fps = fps, width = width, height = height, cinema = false, freezeAmbient = true, filmstrip = 0
            }, folder, samples);
            StudioRender.Changed += Complete;

            void Complete()
            {
                if (StudioRender.Running) return;
                StudioRender.Changed -= Complete;
                if (review.source.Matches(tape) && review.settingsKey == SettingsKey(set) &&
                    StudioRender.Results.Count == 2 && StudioRender.Results.All(r => r.frames == review.frames) &&
                    File.Exists(Path.Combine(root, review.povImage)) && File.Exists(Path.Combine(root, review.witnessImage)))
                    File.WriteAllText(ReviewFile(root, moving), JsonUtility.ToJson(review, true));
                changed?.Invoke();
            }
        }
    }
}
