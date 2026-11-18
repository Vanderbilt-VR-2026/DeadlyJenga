using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Small per-track settings kept in the tape (track.meta): how a played-back actor looks to the
    // performer, and whether its take went stale.
    [Serializable] public sealed class StudioTrackNotes
    {
        public int ghost;                 // 0 = normal character, 1 = see-through ghost, 2 = hidden
        public bool passThrough;          // true = the live player walks through this actor
        public int staleFrame = -1;       // tape frame where the world first differs from the one this take was performed in

        public static StudioTrackNotes Of(StudioTrack track)
        {
            if (string.IsNullOrEmpty(track.meta)) return new StudioTrackNotes();
            try { return JsonUtility.FromJson<StudioTrackNotes>(track.meta) ?? new StudioTrackNotes(); }
            catch { return new StudioTrackNotes(); }
        }

        public static void Update(StudioTrack track, Action<StudioTrackNotes> change)
        {
            var notes = Of(track);
            change(notes);
            track.meta = JsonUtility.ToJson(notes);
        }
    }

    // Making takes out of other takes: punch-in joins, comps and clones. Nothing here deletes a take.
    public static class StudioTakes
    {
        [Serializable] sealed class ActorTakeMeta { public string world = ""; }

        public static string WorldMeta(string worldTakeId) => JsonUtility.ToJson(new ActorTakeMeta { world = worldTakeId });
        public static string WorldOf(StudioTake take)
        {
            if (string.IsNullOrEmpty(take?.meta)) return "";
            try { return JsonUtility.FromJson<ActorTakeMeta>(take.meta)?.world ?? ""; } catch { return ""; }
        }

        static bool IsActor(StudioTrack track) => track.type == SurvivorTrack.Id || track.type == HandTrack.Id;

        // Punch-in: "fresh" was recorded from the punch frame. Put the old take's frames in front of it
        // and ease from the old pose into the new one over a few frames, so the join does not pop.
        public static void JoinAfterPunch(string typeId, StudioTake old, StudioTake fresh, int blendFrames)
        {
            int punch = fresh.start;
            var joined = new List<byte[]>();
            for (int frame = old.start; frame < punch; frame++)
                joined.Add(old.Covers(frame) ? old.frames[frame - old.start] : old.frames[old.frames.Count - 1]);
            for (int i = 0; i < fresh.frames.Count; i++)
            {
                int frame = punch + i;
                if (i < blendFrames && old.Covers(frame))
                {
                    float weight = (i + 1f) / (blendFrames + 1f);
                    weight = weight * weight * (3 - 2 * weight);   // ease in and out
                    joined.Add(ActorFrame.Blend(ActorFrame.Read(typeId, old.frames[frame - old.start]), ActorFrame.Read(typeId, fresh.frames[i]), weight).Write());
                }
                else joined.Add(fresh.frames[i]);
            }
            fresh.start = Mathf.Min(old.start, punch);
            fresh.frames.Clear();
            fresh.frames.AddRange(joined);
        }

        // Comp: a new take that is the active take up to "frame" and "other" from there on.
        public static StudioTake Comp(StudioTrack track, StudioTake other, int frame, int blendFrames)
        {
            var first = track.ActiveTake;
            if (first == null || other == null || !IsActor(track)) throw new InvalidOperationException("Pick an actor with at least two takes.");
            if (!first.Covers(Mathf.Max(first.start, frame - 1)) || !other.Covers(frame))
                throw new InvalidOperationException("Both takes must cover the playhead.");
            // Start with the second part, then let the punch-in join put the first part in front of it.
            var comp = new StudioTake { start = frame, keyEvery = 1, meta = other.meta, name = $"Comp: {first.name} then {other.name}" };
            for (int f = frame; f < other.End; f++) comp.frames.Add(other.frames[f - other.start]);
            JoinAfterPunch(track.type, first, comp, blendFrames);
            track.takes.Add(comp);
            track.active = track.takes.Count - 1;
            return comp;
        }

        public sealed class CloneOptions
        {
            public Vector3 offset = new Vector3(1.2f, 0, 0);   // metres, stage space
            public float turn;                                  // degrees around the actor's first position
            public float delay;                                 // seconds later (negative = earlier)
            public bool mirror;
            public float jitter;                                // 0 = exact copy; 1 = up to half a metre and a third of a second of difference
            public int seed = 1;
            public bool dress;                                  // true = wear 'look' and 'color' instead of the source's outfit
            public DeadlyJenga.Core.AvatarAppearance look;
            public int color = -1;
        }

        // Clone: copy an actor's active take onto a new actor, moved, turned, delayed or mirrored.
        // One performance becomes several people.
        public static StudioMark Clone(StudioSet set, StudioTape tape, StudioMark source, CloneOptions options)
        {
            var from = tape.Find(source.id);
            var take = from?.ActiveTake;
            if (take == null) throw new InvalidOperationException(source.actor + " has no take to clone yet.");
            var random = new System.Random(options.seed);
            float Spread() => (float)(random.NextDouble() * 2 - 1) * options.jitter;
            Vector3 offset = options.offset + new Vector3(Spread() * .5f, 0, Spread() * .5f);
            float delay = options.delay + Spread() * .33f;

            var mark = set.AddMark(source.role, source.position + offset);
            mark.actor = source.actor + " clone " + (set.marks.Count(m => m.actor.StartsWith(source.actor + " clone")) + 1);
            mark.yaw = source.yaw + options.turn;
            mark.color = (source.color + set.marks.Count) % 8;
            mark.customLook = source.customLook; mark.appearance = source.appearance; mark.equipment = source.equipment;
            if (options.dress) { mark.customLook = true; mark.appearance = options.look; }
            if (options.color >= 0) mark.color = options.color % 8;

            var first = ActorFrame.Read(from.type, take.frames.FirstOrDefault(f => ActorFrame.Read(from.type, f).visible) ?? take.frames[0]);
            Vector3 pivot = first.Position;
            Quaternion turn = Quaternion.Euler(0, options.turn, 0);
            // Mirror across the plane that runs through the actor from back to front.
            Vector3 normal = Quaternion.Euler(0, source.yaw, 0) * Vector3.right;
            int shift = Mathf.RoundToInt(delay * set.fps);
            var copy = new StudioTake { keyEvery = 1, meta = take.meta, name = "Clone of " + source.actor };
            int start = take.start + shift, skip = Mathf.Max(0, -start);
            copy.start = Mathf.Max(0, start);
            for (int i = skip; i < take.frames.Count && copy.start + (i - skip) < set.Frames; i++)
            {
                var frame = ActorFrame.Read(from.type, take.frames[i]);
                frame.Transform(pivot, turn, offset, options.mirror, normal);
                if (!frame.isHand) { frame.color = (frame.color & ~0xFF) | (mark.color & 0xFF); if (mark.customLook) frame.look = mark.appearance; }
                copy.frames.Add(frame.Write());
            }
            var track = tape.GetOrAdd(mark.id, from.type, mark.actor);
            track.takes.Add(copy);
            track.active = track.takes.Count - 1;
            return mark;
        }

        public sealed class CrowdOptions
        {
            public int count = 20;
            public float radius = 5;          // how far from the performers the crowd spreads, in metres
            public float spacing = .9f;       // closest two people may start to each other
            public float timeSpread = .8f;    // people start up to this many seconds earlier or later
            public bool mirror = true, outfits = true;
            public int seed = 1;
        }

        // Crowd: turn a few performed takes into a group. Each person is a clone of one of the sources with
        // their own place, timing, facing, mirroring and outfit, so no two move in step.
        public static List<StudioMark> Crowd(StudioSet set, StudioTape tape, List<StudioMark> sources, CrowdOptions options, out List<string> notes)
        {
            notes = new List<string>();
            sources = sources.Where(s => tape.Find(s.id)?.ActiveTake != null).ToList();
            if (sources.Count == 0) throw new InvalidOperationException("Pick at least one actor that has a take.");
            var random = new System.Random(options.seed);
            float Next() => (float)random.NextDouble();
            var made = new List<StudioMark>();
            var taken = set.marks.Select(m => m.position).ToList();
            // People copied from the same take get evenly spread delays, so their steps never line up.
            int perSource = Mathf.CeilToInt(options.count / (float)sources.Count);
            float step = Mathf.Max(.17f, options.timeSpread / Mathf.Max(1, (perSource + 1) / 2));
            var used = new int[sources.Count];
            int nudged = 0;
            for (int i = 0; i < options.count; i++)
            {
                int pick = i % sources.Count;
                var source = sources[pick];
                // Find a free spot: far enough from everyone already placed. Widen the search if it is crowded.
                Vector3 place = source.position; float reach = options.radius; bool found = false;
                for (int attempt = 0; attempt < 200 && !found; attempt++)
                {
                    float angle = Next() * Mathf.PI * 2, distance = Mathf.Sqrt(Next()) * reach;
                    place = source.position + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                    found = taken.All(p => Vector3.Distance(p, place) >= options.spacing);
                    if (attempt % 40 == 39) { reach *= 1.3f; nudged++; }
                }
                if (!found) notes.Add($"Person {i + 1} could not find a free spot; they overlap someone.");
                taken.Add(place);
                int slot = used[pick]++;
                // Never zero: one step later, one step earlier, two steps later... so nobody matches the performer either.
                float delay = (slot / 2 + 1) * step * (slot % 2 == 0 ? 1 : -1) + (Next() - .5f) * .04f;
                var look = new DeadlyJenga.Core.AvatarAppearance();
                if (options.outfits)
                {
                    T Any<T>() where T : Enum { var values = (T[])Enum.GetValues(typeof(T)); return values[random.Next(values.Length)]; }
                    look.skin = Any<DeadlyJenga.Core.AvatarSkin>(); look.hair = Any<DeadlyJenga.Core.AvatarHair>();
                    look.eyewear = Any<DeadlyJenga.Core.AvatarEyewear>(); look.hairShade = Any<DeadlyJenga.Core.AvatarHairShade>();
                    look.clothing = Any<DeadlyJenga.Core.AvatarClothing>();
                }
                var clone = Clone(set, tape, source, new CloneOptions
                {
                    offset = place - source.position, turn = (Next() - .5f) * 50, delay = delay, mirror = options.mirror && i % 2 == 1,
                    jitter = 0, seed = options.seed * 131 + i, dress = options.outfits, look = look, color = random.Next(8)
                });
                clone.actor = "Crowd " + (made.Count + 1);
                tape.Find(clone.id).name = clone.actor;
                made.Add(clone);
            }
            if (nudged > 0) notes.Add("The area was tight, so some people were placed a little farther out.");
            return made;
        }

        // After the world was recorded again: flag every actor take that was performed against an older world,
        // and say at which frame the two worlds first differ.
        public static void MarkStale(StudioTape tape, StudioTake newWorld, int fps, ICollection<string> justRecorded)
        {
            var worldTrack = tape.FindType(WorldTrack.Id);
            var differences = new Dictionary<string, int>();
            foreach (var track in tape.tracks.Where(IsActor))
            {
                var take = track.ActiveTake;
                if (take == null || justRecorded.Contains(track.id)) continue;
                string against = WorldOf(take);
                if (against == newWorld.id) continue;
                if (!differences.TryGetValue(against, out int frame))
                {
                    var old = worldTrack?.takes.FirstOrDefault(t => t.id == against);
                    frame = old == null ? 0 : WorldTrack.FirstDifference(old, newWorld, 10);
                    differences[against] = frame;
                }
                if (frame < 0) { take.meta = WorldMeta(newWorld.id); continue; }   // same world after all
                StudioTrackNotes.Update(track, n => n.staleFrame = frame);
                track.badge = $"Stale: the world was re-recorded and first differs at {frame / (float)fps:0.00} s (frame {frame}). Keep it, re-record it, or punch in from there.";
            }
        }

        // "Keep": the person looked and the old performance still works.
        public static void AcceptStale(StudioTape tape, StudioTrack track)
        {
            var world = tape.FindType(WorldTrack.Id)?.ActiveTake;
            if (track.ActiveTake != null && world != null) track.ActiveTake.meta = WorldMeta(world.id);
            track.badge = "";
            StudioTrackNotes.Update(track, n => n.staleFrame = -1);
        }
    }
}
