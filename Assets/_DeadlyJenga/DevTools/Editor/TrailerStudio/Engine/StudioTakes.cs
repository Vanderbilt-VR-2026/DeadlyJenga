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
        [Serializable] sealed class ActorTakeMeta { public string world = "", previousCrowdTake = ""; }

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
            if (take == null || take.frames.Count == 0) throw new InvalidOperationException(source.actor + " has no take to clone yet.");
            var random = new System.Random(options.seed);
            float Spread() => (float)(random.NextDouble() * 2 - 1) * options.jitter;
            Vector3 offset = options.offset + new Vector3(Spread() * .5f, 0, Spread() * .5f);
            float delay = options.delay + Spread() * .33f;

            var mark = set.AddMark(source.role, source.position + offset);
            if (tape.Find(mark.id) != null) mark.id = set.UniqueId("clone-" + Guid.NewGuid().ToString("N"));
            mark.actor = source.actor + " clone " + (set.marks.Count(m => m.actor.StartsWith(source.actor + " clone")) + 1);
            mark.yaw = source.yaw + options.turn;
            mark.color = (source.color + set.marks.Count) % 8;
            mark.customLook = source.customLook; mark.appearance = source.appearance; mark.equipment = source.equipment;
            if (options.dress) { mark.customLook = true; mark.appearance = options.look; }
            if (options.color >= 0) mark.color = options.color % 8;

            var copy = CopyPerformance(set, source, mark, from, take, options.turn, delay, options.mirror, offset);
            var track = tape.GetOrAdd(mark.id, from.type, mark.actor);
            track.takes.Add(copy);
            track.active = track.takes.Count - 1;
            return mark;
        }

        static StudioTake CopyPerformance(StudioSet set, StudioMark source, StudioMark mark, StudioTrack from,
            StudioTake take, float heading, float delay, bool mirror, Vector3 offset)
        {
            var first = ActorFrame.Read(from.type, take.frames.FirstOrDefault(f => ActorFrame.Read(from.type, f).visible) ?? take.frames[0]);
            Vector3 pivot = first.Position;
            Quaternion turn = Quaternion.Euler(0, heading, 0);
            // Mirror across the plane that runs through the actor from back to front.
            Vector3 normal = Quaternion.Euler(0, source.yaw, 0) * Vector3.right;
            int shift = Mathf.RoundToInt(delay * set.fps);
            var copy = new StudioTake { keyEvery = 1, meta = take.meta, name = "Clone of " + source.actor };
            int start = take.start + shift, skip = Mathf.Max(0, -start);
            copy.start = Mathf.Max(0, start);
            for (int i = skip; i < take.frames.Count && copy.start + (i - skip) < set.Frames; i++)
            {
                var frame = ActorFrame.Read(from.type, take.frames[i]);
                frame.Transform(pivot, turn, offset, mirror, normal);
                if (!frame.isHand) { frame.color = (frame.color & ~0xFF) | (mark.color & 0xFF); if (mark.customLook) frame.look = mark.appearance; }
                copy.frames.Add(frame.Write());
            }
            // A copy that starts later or ends earlier than its source would pop in or out. Hold its first
            // pose before it starts and its last pose after it ends, so it is there for as long as the source.
            if (copy.frames.Count > 0)
            {
                int wantStart = Mathf.Max(0, take.start), wantEnd = Mathf.Min(set.Frames, take.End);
                if (copy.start > wantStart)
                {
                    copy.frames.InsertRange(0, Enumerable.Repeat(copy.frames[0], copy.start - wantStart));
                    copy.start = wantStart;
                }
                while (copy.start + copy.frames.Count < wantEnd) copy.frames.Add(copy.frames[copy.frames.Count - 1]);
            }
            return copy;
        }

        public sealed class CrowdOptions
        {
            public int count = 20;
            public float radius = 5;          // how far from the performers the crowd spreads, in metres
            public float spacing = .9f;       // closest two people may start to each other
            public float timeSpread = .8f;    // people start up to this many seconds earlier or later
            public bool mirror = true, outfits = true;
            public int seed = 1;
            public string group = "";
            public bool directed;
            public Vector3 start, destination;
            public float cueTime;
            // With the stage open: the height of the ground at a spot (stage space), or null where there is nothing to stand on.
            // People are only placed where there is ground, and start standing on it.
            public Func<Vector3, float?> ground;
        }

        // Crowd: turn a few performed takes into a group. Each person is a clone of one of the sources with
        // their own place, timing, facing, mirroring and outfit, so no two move in step.
        public static List<StudioMark> Crowd(StudioSet set, StudioTape tape, List<StudioMark> sources, CrowdOptions options, out List<string> notes)
        {
            notes = new List<string>();
            sources = sources.Where(s => tape.Find(s.id)?.ActiveTake?.frames.Count > 0).ToList();
            if (sources.Count == 0) throw new InvalidOperationException("Pick at least one actor that has a take.");
            if (options.directed && (options.ground == null ||
                Vector3.ProjectOnPlane(options.destination - options.start, Vector3.up).sqrMagnitude < .04f))
                throw new InvalidOperationException("Hold the stage and place a destination away from the starting area.");
            var draftSet = new StudioSet { fps = set.fps, length = set.length, marks = new List<StudioMark>(set.marks) };
            var draftTape = new StudioTape();
            draftTape.tracks.AddRange(tape.tracks);
            var random = new System.Random(options.seed);
            float Next() => (float)random.NextDouble();
            var made = new List<StudioMark>();
            var taken = set.marks.Select(m => m.position).ToList();
            // People copied from the same take get evenly spread delays, so their steps never line up.
            int perSource = Mathf.CeilToInt(options.count / (float)sources.Count);
            float step = options.timeSpread / Mathf.Max(1, (perSource + 1) / 2);
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
                    var origin = options.directed ? options.start : source.position;
                    place = origin + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                    found = taken.All(p => Vector3.Distance(p, place) >= options.spacing);
                    if (found && options.ground != null)
                    {
                        float? height = options.ground(place);
                        // Not over a drop, and not up on something much higher or lower than the performer stood on.
                        if (!height.HasValue || Mathf.Abs(height.Value - origin.y) > 1.2f) found = false;
                        else place.y = height.Value;
                    }
                    if (found) found = taken.All(p => Vector3.Distance(p, place) >= options.spacing);
                    if (!options.directed && attempt % 40 == 39) { reach *= 1.3f; nudged++; }
                }
                if (!found)
                {
                    if (options.directed)
                    {
                        notes.Add($"Person {i + 1} had no valid starting position. No group was created.");
                        return new List<StudioMark>();
                    }
                    notes.Add($"Person {i + 1} had no valid starting position and was skipped.");
                    continue;
                }
                int slot = used[pick]++;
                // Never zero: one step later, one step earlier, two steps later... so nobody matches the performer either.
                float delay = (slot / 2 + 1) * step * (slot % 2 == 0 ? 1 : -1) + (Next() - .5f) * Mathf.Min(step * .2f, .04f);
                delay = Mathf.Clamp(delay, -options.timeSpread, options.timeSpread);
                var look = new DeadlyJenga.Core.AvatarAppearance();
                if (options.outfits)
                {
                    T Any<T>() where T : Enum { var values = (T[])Enum.GetValues(typeof(T)); return values[random.Next(values.Length)]; }
                    look.skin = Any<DeadlyJenga.Core.AvatarSkin>(); look.hair = Any<DeadlyJenga.Core.AvatarHair>();
                    look.eyewear = Any<DeadlyJenga.Core.AvatarEyewear>(); look.hairShade = Any<DeadlyJenga.Core.AvatarHairShade>();
                    look.clothing = Any<DeadlyJenga.Core.AvatarClothing>();
                }
                float turn = (Next() - .5f) * 50;
                bool mirror = options.mirror && i % 2 == 1;
                var sourceTrack = tape.Find(source.id);
                var sourceTake = sourceTrack.ActiveTake;
                if (options.directed)
                {
                    Vector3 travel = ActorFrame.Read(sourceTrack.type, sourceTake.frames[sourceTake.frames.Count - 1]).Position -
                        ActorFrame.Read(sourceTrack.type, sourceTake.frames[0]).Position;
                    if (mirror) travel = Vector3.Reflect(travel, Quaternion.Euler(0, source.yaw, 0) * Vector3.right);
                    Vector3 direction = options.destination - place;
                    travel.y = direction.y = 0;
                    if (travel.sqrMagnitude > .04f && direction.sqrMagnitude > .04f)
                        turn = Vector3.SignedAngle(travel, direction, Vector3.up);
                    else
                    {
                        notes.Add($"{source.actor} has insufficient travel to aim person {i + 1}. No group was created.");
                        return new List<StudioMark>();
                    }
                    delay += options.cueTime - sourceTake.start / (float)set.fps;
                }
                int shift = Mathf.RoundToInt(delay * set.fps);
                if (sourceTake.start + shift >= set.Frames || sourceTake.End + shift <= 0)
                {
                    if (options.directed)
                    {
                        notes.Add($"Person {i + 1}'s timing fell outside the set. No group was created.");
                        return new List<StudioMark>();
                    }
                    notes.Add($"Person {i + 1}'s timing fell outside the set and was skipped.");
                    continue;
                }
                taken.Add(place);
                var clone = Clone(draftSet, draftTape, source, new CloneOptions
                {
                    offset = place - source.position, turn = turn, delay = delay, mirror = mirror,
                    jitter = 0, seed = options.seed * 131 + i, dress = options.outfits, look = look, color = random.Next(8)
                });
                clone.crowdGroup = options.group?.Trim() ?? "";
                clone.crowdSource = source.id;
                clone.crowdTurn = turn;
                clone.crowdDelay = delay;
                clone.crowdMirror = mirror;
                clone.actor = (clone.crowdGroup.Length > 0 ? clone.crowdGroup : "Crowd") + " " + (made.Count + 1);
                draftTape.Find(clone.id).name = clone.actor;
                made.Add(clone);
            }
            if (nudged > 0) notes.Add("The area was tight, so some people were placed a little farther out.");
            set.marks.AddRange(made);
            tape.tracks.AddRange(made.Select(m => draftTape.Find(m.id)));
            return made;
        }

        public static int RetakeCrowd(StudioSet set, StudioTape tape, string group, float timingSpread, int seed)
        {
            string world = tape.FindType(WorldTrack.Id)?.ActiveTake?.id;
            if (string.IsNullOrEmpty(world)) throw new InvalidOperationException("Record and select a world take before replacing a group's performances.");
            var random = new System.Random(seed);
            var drafts = new List<(StudioTrack track, StudioTake take)>();
            foreach (var mark in set.marks.Where(m => m.crowdGroup == group && !m.heroLocked))
            {
                var target = tape.Find(mark.id);
                if (target == null || target.locked) continue;
                var source = set.marks.Find(m => m.id == mark.crowdSource);
                var from = tape.Find(mark.crowdSource);
                var active = from?.ActiveTake;
                if (source == null || active == null || active.frames.Count == 0 || WorldOf(active) != world)
                    throw new InvalidOperationException($"{mark.actor}'s source needs a performance recorded against the active world take.");
                float variation = ((float)random.NextDouble() * 2 - 1) * timingSpread;
                var fresh = CopyPerformance(set, source, mark, from, active, mark.crowdTurn, mark.crowdDelay + variation,
                    mark.crowdMirror, mark.position - source.position);
                if (fresh.frames.Count == 0) throw new InvalidOperationException($"{mark.actor}'s timing puts the take outside the set.");
                fresh.name = $"{group} retake · seed {seed}";
                fresh.meta = JsonUtility.ToJson(new ActorTakeMeta { world = world, previousCrowdTake = target.ActiveTake?.id ?? "" });
                drafts.Add((target, fresh));
            }
            foreach (var draft in drafts)
            {
                draft.track.takes.Add(draft.take);
                draft.track.active = draft.track.takes.Count - 1;
            }
            CheckStale(tape, set.fps);
            return drafts.Count;
        }

        public static int RestoreCrowdTakes(StudioSet set, StudioTape tape, string group)
        {
            var restored = new List<(StudioTrack track, int index)>();
            foreach (var mark in set.marks.Where(m => m.crowdGroup == group && !m.heroLocked))
            {
                var track = tape.Find(mark.id);
                if (track == null || track.locked || string.IsNullOrEmpty(track.ActiveTake?.meta)) continue;
                ActorTakeMeta meta;
                try { meta = JsonUtility.FromJson<ActorTakeMeta>(track.ActiveTake.meta); }
                catch { continue; }
                if (string.IsNullOrEmpty(meta?.previousCrowdTake)) continue;
                int index = track.takes.FindIndex(t => t.id == meta.previousCrowdTake && !t.trashed);
                if (index < 0) throw new InvalidOperationException($"Restore {mark.actor}'s previous take from the trash first.");
                restored.Add((track, index));
            }
            foreach (var target in restored) target.track.active = target.index;
            CheckStale(tape, set.fps);
            return restored.Count;
        }

        public static (int frame, string message) RehearseCrowd(StudioStage stage, string group, float spacing)
        {
            var actors = stage.set.marks.Where(m => m.role != StudioRole.Hand)
                .Select(m => (mark: m, track: stage.tape.Find(m.id)))
                .Where(a => a.track?.ActiveTake != null && a.track.mode != TrackMode.Muted).ToArray();
            var occupied = new List<(StudioMark mark, Vector3 position)>();
            int stride = Mathf.Max(1, stage.Fps / 10);
            for (int frame = 0; frame < stage.Frames; frame += stride)
            {
                occupied.Clear();
                foreach (var actor in actors)
                {
                    var take = actor.track.ActiveTake;
                    if (!take.Covers(frame) || !ActorTrack.TryReadPosition(actor.track.type, take.frames[frame - take.start], out var point)) continue;
                    if (actor.mark.crowdGroup == group)
                    {
                        Vector3 feet = stage.ToStage(point);
                        float? ground = stage.GroundAt(feet);
                        if (!ground.HasValue || Mathf.Abs(ground.Value - feet.y) > 1.2f)
                            return (frame, $"{actor.mark.actor} may have lost the floor");
                    }
                    foreach (var other in occupied)
                        if ((actor.mark.crowdGroup == group || other.mark.crowdGroup == group) &&
                            (other.position - point).sqrMagnitude < spacing * spacing)
                            return (frame, $"{actor.mark.actor} passes close to {other.mark.actor}");
                    occupied.Add((actor.mark, point));
                }
            }
            return (-1, "No close passes or possible floor losses in the sampled routes.");
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
                if (frame < 0)
                {
                    var meta = JsonUtility.FromJson<ActorTakeMeta>(take.meta) ?? new ActorTakeMeta();
                    meta.world = newWorld.id;
                    take.meta = JsonUtility.ToJson(meta);
                    continue;
                }
                StudioTrackNotes.Update(track, n => n.staleFrame = frame);
                StudioBadges.Set(track, StudioBadges.Stale, $"the world was re-recorded and first differs at {frame / (float)fps:0.00} s (frame {frame}). Keep it, re-record it, or punch in from there.");
            }
        }

        // An active take changed (the world's, or an actor's): work out again which actor takes no longer
        // match the active world, and clear the badge on those that do.
        public static void CheckStale(StudioTape tape, int fps)
        {
            var worldTrack = tape?.FindType(WorldTrack.Id);
            var world = worldTrack?.ActiveTake;
            if (world == null) return;
            var differences = new Dictionary<string, int>();
            foreach (var track in tape.tracks.Where(IsActor))
            {
                var take = track.ActiveTake;
                string against = take == null ? "" : WorldOf(take);
                int frame = -1;
                if (against.Length > 0 && against != world.id && !differences.TryGetValue(against, out frame))
                {
                    var old = worldTrack.takes.FirstOrDefault(t => t.id == against);
                    frame = old == null ? 0 : WorldTrack.FirstDifference(old, world, 10);
                    differences[against] = frame;
                }
                StudioTrackNotes.Update(track, n => n.staleFrame = frame);
                if (frame >= 0) StudioBadges.Set(track, StudioBadges.Stale, $"performed against a different world, which first differs at {frame / (float)fps:0.00} s (frame {frame}). Keep it, re-record it, or punch in from there.");
                else StudioBadges.Clear(track, StudioBadges.Stale);
            }
        }

        // "Keep": the person looked and the old performance still works.
        public static void AcceptStale(StudioTape tape, StudioTrack track)
        {
            var world = tape.FindType(WorldTrack.Id)?.ActiveTake;
            if (track.ActiveTake != null && world != null) track.ActiveTake.meta = WorldMeta(world.id);
            StudioBadges.Clear(track, StudioBadges.Stale);
            StudioTrackNotes.Update(track, n => n.staleFrame = -1);
        }
    }
}
