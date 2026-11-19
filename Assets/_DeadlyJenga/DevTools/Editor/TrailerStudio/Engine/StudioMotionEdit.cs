using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Fixing a recorded performance without performing it again. Every edit makes a NEW take and makes it
    // active; the take it came from stays on the track, so any edit can be undone by picking the old take.
    // Ranges are tape frames: from (first frame changed) up to but not including to.
    public static class StudioMotionEdit
    {
        static bool IsActor(StudioTrack track) => track.type == SurvivorTrack.Id || track.type == HandTrack.Id;

        static StudioTake Source(StudioTrack track)
        {
            if (track == null || !IsActor(track)) throw new InvalidOperationException("Pick an actor first.");
            return track.ActiveTake ?? throw new InvalidOperationException(track.name + " has nothing recorded yet.");
        }

        static StudioTake Finish(StudioTrack track, StudioTake source, List<byte[]> frames, int start, string what)
        {
            var take = new StudioTake { start = Mathf.Max(0, start), keyEvery = 1, meta = source.meta, name = what + " of " + source.name };
            take.frames.AddRange(frames);
            track.takes.Add(take);
            track.active = track.takes.Count - 1;
            return take;
        }

        static ActorFrame Read(StudioTrack track, StudioTake take, int frame) =>
            ActorFrame.Read(track.type, take.frames[Mathf.Clamp(frame, take.start, take.End - 1) - take.start]);

        // A pose at a time between two frames.
        static ActorFrame Sample(StudioTrack track, StudioTake take, float frame)
        {
            int a = Mathf.FloorToInt(frame);
            float part = frame - a;
            var first = Read(track, take, a);
            return part < .001f ? first : ActorFrame.Blend(first, Read(track, take, a + 1), part);
        }

        // Trim: keep only the range.
        public static StudioTake Trim(StudioTrack track, int from, int to)
        {
            var source = Source(track);
            from = Mathf.Max(from, source.start); to = Mathf.Min(to, source.End);
            if (to - from < 2) throw new InvalidOperationException("The range is outside this take.");
            return Finish(track, source, source.frames.GetRange(from - source.start, to - from), from, "Trim");
        }

        // Slide in time: the whole take happens earlier or later.
        public static StudioTake Slide(StudioTrack track, int frames, int setFrames)
        {
            var source = Source(track);
            int start = source.start + frames, skip = Mathf.Max(0, -start);
            var kept = source.frames.Skip(skip).Take(Mathf.Max(0, setFrames - Mathf.Max(0, start))).ToList();
            if (kept.Count < 2) throw new InvalidOperationException("That would slide the take off the tape.");
            return Finish(track, source, kept, Mathf.Max(0, start), "Slide");
        }

        // Retime: the range plays at "speed" (0.5 = half speed, twice as long). Everything after it moves along.
        public static StudioTake Retime(StudioTrack track, int from, int to, float speed, int setFrames)
        {
            var source = Source(track);
            from = Mathf.Max(from, source.start); to = Mathf.Min(to, source.End);
            if (to - from < 2) throw new InvalidOperationException("The range is outside this take.");
            speed = Mathf.Clamp(speed, .1f, 8);
            var frames = new List<byte[]>();
            for (int f = source.start; f < from; f++) frames.Add(source.frames[f - source.start]);
            int stretched = Mathf.Max(1, Mathf.RoundToInt((to - from) / speed));
            for (int i = 0; i < stretched; i++) frames.Add(Sample(track, source, Mathf.Min(from + i * speed, to - 1)).Write());
            for (int f = to; f < source.End; f++) frames.Add(source.frames[f - source.start]);
            if (source.start + frames.Count > setFrames) frames.RemoveRange(setFrames - source.start, source.start + frames.Count - setFrames);
            var retimed = Finish(track, source, frames, source.start, speed < 1 ? "Slowed part" : "Sped-up part");
            // Everything after the range now happens at a different time than the world it was performed against.
            if (to < source.End) StudioBadges.Set(track, StudioBadges.Retimed, $"after {to / (float)Mathf.Max(1, StudioRun.Stage != null ? StudioRun.Stage.Fps : 30):0.0} s this take no longer lines up with the world.");
            return retimed;
        }

        // Smooth: average each pose with its neighbours to remove jitter. window = frames on each side.
        public static StudioTake Smooth(StudioTrack track, int from, int to, int window)
        {
            var source = Source(track);
            from = Mathf.Max(from, source.start); to = Mathf.Min(to, source.End);
            window = Mathf.Clamp(window, 1, 15);
            var frames = new List<byte[]>(source.frames);
            for (int f = from; f < to; f++)
            {
                var middle = Read(track, source, f);
                if (!middle.visible) continue;
                Vector3 feet = Vector3.zero, head = Vector3.zero, left = Vector3.zero, right = Vector3.zero, target = Vector3.zero; float total = 0;
                for (int n = -window; n <= window; n++)
                {
                    var near = Read(track, source, f + n);
                    if (!near.visible) continue;
                    // Nearer frames count more; and the ends of the range ease in, so the edit does not pop.
                    float weight = window + 1 - Mathf.Abs(n);
                    feet += near.body.feet * weight; head += near.body.head * weight; left += near.body.left.position * weight; right += near.body.right.position * weight;
                    target += near.target.position * weight; total += weight;
                }
                float edge = Mathf.Clamp01(Mathf.Min(f - from + 1, to - f) / (float)(window + 1));
                if (middle.isHand) middle.target.position = Vector3.Lerp(middle.target.position, target / total, edge);
                else
                {
                    middle.body.feet = Vector3.Lerp(middle.body.feet, feet / total, edge); middle.body.head = Vector3.Lerp(middle.body.head, head / total, edge);
                    middle.body.left.position = Vector3.Lerp(middle.body.left.position, left / total, edge); middle.body.right.position = Vector3.Lerp(middle.body.right.position, right / total, edge);
                }
                frames[f - source.start] = middle.Write();
            }
            return Finish(track, source, frames, source.start, "Smoothed");
        }

        // Offset in space, eased in and out over "ease" frames at both ends of the range.
        public static StudioTake Offset(StudioTrack track, int from, int to, Vector3 move, int ease)
        {
            var source = Source(track);
            from = Mathf.Max(from, source.start); to = Mathf.Min(to, source.End);
            var frames = new List<byte[]>(source.frames);
            for (int f = from; f < to; f++)
            {
                var pose = Read(track, source, f);
                if (!pose.visible) continue;
                float weight = ease <= 0 ? 1 : Mathf.Clamp01(Mathf.Min(f - from + 1, to - f) / (float)ease);
                weight = weight * weight * (3 - 2 * weight);
                pose.Transform(pose.Position, Quaternion.identity, move * weight, false, Vector3.right);
                frames[f - source.start] = pose.Write();
            }
            return Finish(track, source, frames, source.start, "Moved part");
        }

        // Bend the path: pull the pose at one frame by "move"; frames within "reach" follow less and less.
        public static StudioTake Bend(StudioTrack track, int frame, Vector3 move, int reach) =>
            Offset(track, frame - reach, frame + reach + 1, move, reach);

        // Blend between two takes over a range: the active take before it, "other" after it.
        public static StudioTake BlendTakes(StudioTrack track, StudioTake other, int from, int to)
        {
            var source = Source(track);
            if (other == null || other == source) throw new InvalidOperationException("Pick a second take to blend with.");
            from = Mathf.Max(from, Mathf.Max(source.start, other.start)); to = Mathf.Min(to, Mathf.Min(source.End, other.End));
            if (to - from < 1) throw new InvalidOperationException("Both takes must cover the range.");
            var frames = new List<byte[]>();
            int end = Mathf.Max(source.End, other.End);
            for (int f = source.start; f < end; f++)
            {
                if (f < from) frames.Add(source.Covers(f) ? source.frames[f - source.start] : other.frames[0]);
                else if (f >= to) frames.Add(other.Covers(f) ? other.frames[f - other.start] : frames[frames.Count - 1]);
                else
                {
                    float weight = (f - from + 1f) / (to - from + 1f);
                    frames.Add(ActorFrame.Blend(Read(track, source, f), Read(track, other, f), weight * weight * (3 - 2 * weight)).Write());
                }
            }
            return Finish(track, source, frames, source.start, "Blend");
        }

        // Fix foot sliding after edits: put the feet back on the surface below them, frame by frame,
        // and hold the feet still on frames where the performance had them planted.
        // The stage must be open: the Studio shows each tape frame and looks for the surface there.
        public static StudioTake LockFeet(StudioStage stage, StudioTrack track, int from, int to)
        {
            var source = Source(track);
            if (track.type != SurvivorTrack.Id) throw new InvalidOperationException("Only survivors have feet.");
            from = Mathf.Max(from, source.start); to = Mathf.Min(to, source.End);
            var frames = new List<byte[]>(source.frames);
            int mask = (1 << Layers.Block) | (1 << Layers.Surface) | 1;
            Vector3? planted = null;
            int was = StudioRun.Frame;
            for (int f = from; f < to; f++)
            {
                var pose = Read(track, source, f);
                if (!pose.visible) { planted = null; continue; }
                StudioRun.Scrub(f);
                Vector3 feet = pose.body.feet;
                // Standing still (slower than 10 cm a second): keep the feet exactly where they first stopped.
                var before = f > source.start ? Read(track, source, f - 1) : pose;
                float fps = StudioRun.Stage != null ? StudioRun.Stage.Fps : 30;
                Vector3 moved = before.visible ? feet - before.body.feet : Vector3.zero;
                bool still = before.visible && Vector3.ProjectOnPlane(moved, Vector3.up).magnitude * fps < .1f;
                // In the air (rising or falling faster than 40 cm a second): a jump or a drop, leave it alone.
                bool inAir = Mathf.Abs(moved.y) * fps > .4f;
                if (still && planted.HasValue) feet = new Vector3(planted.Value.x, feet.y, planted.Value.z);
                if (!inAir && Physics.Raycast(feet + Vector3.up * .7f, Vector3.down, out var hit, 1.0f, mask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - feet.y) < .25f)
                    feet.y = hit.point.y;
                planted = still ? (planted ?? feet) : (Vector3?)null;
                Vector3 move = feet - pose.body.feet;
                if (move.sqrMagnitude > 1e-8f)
                {
                    pose.Transform(pose.body.feet, Quaternion.identity, move, false, Vector3.right);
                    frames[f - source.start] = pose.Write();
                }
            }
            StudioRun.Scrub(was);
            return Finish(track, source, frames, source.start, "Feet locked");
        }
    }
}
