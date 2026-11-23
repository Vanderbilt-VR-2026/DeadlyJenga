using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    internal sealed class StudioSoundActivity
    {
        const float BucketWidth = 5;
        static readonly Color MatchColor = StudioColors.Camera;
        static readonly Color BreakColor = StudioColors.Effects;

        [Serializable]
        sealed class SoundFrame
        {
            public AudioEventTrack.Sound[] sounds;
        }

        sealed class Activity
        {
            public int first, last, hits, matches, breaks;
            public float peak;
            public HashSet<int> cues;

            public void Add(Activity other)
            {
                last = other.last;
                hits += other.hits;
                matches += other.matches;
                breaks += other.breaks;
                peak = Mathf.Max(peak, other.peak);
                if (other.cues != null)
                {
                    cues ??= new HashSet<int>();
                    cues.UnionWith(other.cues);
                }
            }
        }

        readonly List<Activity> frames = new List<Activity>();
        readonly Dictionary<int, Activity> buckets = new Dictionary<int, Activity>();
        readonly int fps;
        readonly float opacity;
        float bucketOrigin = float.NaN, bucketScale = float.NaN;
        public string Summary { get; }

        public StudioSoundActivity(StudioTrack track, int fps, bool suppressedBySolo = false)
        {
            this.fps = Mathf.Max(1, fps);
            string state = track.mode == TrackMode.Muted ? "Muted. " : suppressedBySolo ? "Another track is soloed. " :
                track.mode == TrackMode.Live ? "Track is live. " : "";
            opacity = state.Length == 0 ? 1 : .3f;
            var take = track.ActiveTake;
            if (take == null)
            {
                Summary = state + "No active sound event take.";
                return;
            }

            int unreadable = 0, total = 0;
            for (int i = 0; i < take.frames.Count; i++)
            {
                var data = take.frames[i];
                if (data == null || data.Length == 0) continue;
                SoundFrame sample;
                try { sample = JsonUtility.FromJson<SoundFrame>(Encoding.UTF8.GetString(data)); }
                catch (ArgumentException) { unreadable++; continue; }
                if (sample?.sounds == null) { unreadable++; continue; }

                var activity = new Activity { first = take.start + i, last = take.start + i };
                foreach (var sound in sample.sounds)
                {
                    if (sound == null) continue;
                    switch (sound.kind)
                    {
                        case 0:
                            activity.hits++;
                            activity.peak = Mathf.Max(activity.peak, sound.strength);
                            break;
                        case 1:
                            activity.matches++;
                            activity.cues ??= new HashSet<int>();
                            activity.cues.Add(sound.cue);
                            break;
                        case 2:
                            activity.breaks++;
                            break;
                    }
                }
                int count = activity.hits + activity.matches + activity.breaks;
                if (count == 0) continue;
                frames.Add(activity);
                total += count;
            }
            Summary = state + (total == 0
                ? "No sound events in the active take."
                : $"{total} recorded events. Green: block hits. Gold: match cues. Violet: recorded block breaks (not replayed).");
            if (unreadable > 0) Summary += $" Could not read {unreadable} audio frame(s).";
        }

        void UpdateBuckets(Func<float, float> frameToX)
        {
            float origin = frameToX(0), scale = frameToX(1) - origin;
            if (origin == bucketOrigin && scale == bucketScale) return;
            bucketOrigin = origin;
            bucketScale = scale;
            buckets.Clear();
            foreach (var sample in frames)
            {
                int bucket = Mathf.FloorToInt(frameToX(sample.first) / BucketWidth);
                if (!buckets.TryGetValue(bucket, out var group))
                    buckets.Add(bucket, group = new Activity { first = sample.first });
                group.Add(sample);
            }
        }

        public void Draw(MeshGenerationContext mesh, Func<float, float> frameToX)
        {
            float height = mesh.visualElement.contentRect.height, width = mesh.visualElement.contentRect.width;
            if (height <= 11 || width < 3) return;
            UpdateBuckets(frameToX);
            var painter = mesh.painter2D;
            foreach (var pair in buckets)
            {
                var activity = pair.Value;
                float pixel = (pair.Key + .5f) * BucketWidth;
                if (pixel < 0 || pixel >= width + BucketWidth / 2) continue;
                float x = Mathf.Clamp(pixel - 1.5f, 0, width - 3);
                if (activity.hits > 0)
                {
                    float density = Mathf.Clamp01(Mathf.Log(activity.hits + 1, 2) / 7);
                    Fill(x, height - 2 - Mathf.Lerp(2, height - 11, density), 3, height - 2, StudioColors.Audio);
                }
                if (activity.matches > 0) Fill(x, 2, 3, 5, MatchColor);
                if (activity.breaks > 0) Fill(x, 6, 3, 9, BreakColor);
            }

            void Fill(float x, float top, float size, float bottom, Color color)
            {
                color.a *= opacity;
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, top));
                painter.LineTo(new Vector2(x + size, top));
                painter.LineTo(new Vector2(x + size, bottom));
                painter.LineTo(new Vector2(x, bottom));
                painter.ClosePath();
                painter.Fill();
            }
        }

        public void AttachHover(VisualElement lane, Func<float, float> frameToX)
        {
            lane.tooltip = Summary;
            float x = float.NaN;
            lane.RegisterCallback<PointerEnterEvent>(e => x = e.localPosition.x);
            lane.RegisterCallback<PointerMoveEvent>(e => x = e.localPosition.x);
            lane.RegisterCallback<PointerLeaveEvent>(_ => x = float.NaN);
            lane.RegisterCallback<TooltipEvent>(e =>
            {
                UpdateBuckets(frameToX);
                e.tooltip = !float.IsNaN(x) && buckets.TryGetValue(Mathf.FloorToInt(x / BucketWidth), out var activity)
                    ? Describe(activity) : Summary;
                e.rect = lane.worldBound;
                e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
        }

        string Describe(Activity activity)
        {
            string at = StudioUi.Time(activity.first, fps);
            if (activity.last > activity.first) at += "–" + StudioUi.Time(activity.last, fps);
            var lines = new List<string> { at };
            if (activity.hits > 0) lines.Add($"{activity.hits} block hit(s) · strongest {activity.peak:0.##}");
            if (activity.matches > 0)
            {
                var names = activity.cues.OrderBy(cue => cue).Select(cue => ObjectNames.NicifyVariableName(((MatchSoundCue)cue).ToString()));
                lines.Add($"{activity.matches} match cue(s) · {string.Join(", ", names)}");
            }
            if (activity.breaks > 0) lines.Add($"{activity.breaks} recorded block break(s)");
            if (opacity < 1) lines.Add(Summary);
            return string.Join("\n", lines);
        }
    }
}
