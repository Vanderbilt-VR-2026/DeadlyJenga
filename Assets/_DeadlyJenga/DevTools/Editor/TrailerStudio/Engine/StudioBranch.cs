using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Branching: keep the world tape up to a frame, then let physics run live from there and record
    // the result as a new world take. Nothing is restarted and no take is lost.
    public static class StudioBranch
    {
        // Saved with a world take (in take.meta, next to the block count the world track keeps there).
        [Serializable] public sealed class WorldNote
        {
            public int blocks;
            public string parent = "";    // the take this one branched from
            public int branchFrame = -1;
            public string label = "";
            public string group = "";     // variation runs share a group id
            public int fallen;            // scores, filled in when the take is made
            public float travelled, targetDistance = -1;
            public bool inZone;
        }

        public static WorldNote NoteOf(StudioTake take)
        {
            if (string.IsNullOrEmpty(take?.meta)) return new WorldNote();
            try { return JsonUtility.FromJson<WorldNote>(take.meta) ?? new WorldNote(); } catch { return new WorldNote(); }
        }

        public static string Thumbnail(StudioStage stage, StudioTake take) =>
            Path.Combine(StudioLibrary.TapeFolder(stage.set.id), "thumbs", take.id + ".png");
        public static string Thumbnail(string setId, StudioTake take) =>
            Path.Combine(StudioLibrary.TapeFolder(setId), "thumbs", take.id + ".png");

        // Pose every block as the tape has it on "frame", make the map live, and give each block the speed
        // it had on the tape (worked out from the frame before).
        public static void StartFromTape(StudioStage stage, StudioTake world, int frame)
        {
            var track = stage.tape.FindType(WorldTrack.Id);
            var type = StudioTrackTypes.Get(WorldTrack.Id);
            int count = stage.blocks.Length;
            var before = new Vector3[count]; var beforeTurn = new Quaternion[count];
            stage.Synced?.SetReplica(true);
            using (var player = type.CreatePlayer(stage, track, world))
            {
                player.Show(frame - 1, false);
                for (int i = 0; i < count; i++)
                    if (stage.blocks[i]) { before[i] = stage.blocks[i].transform.position; beforeTurn[i] = stage.blocks[i].transform.rotation; }
                player.Show(frame, false);
            }
            stage.Synced?.SetReplica(false);
            float fps = stage.Fps;
            for (int i = 0; i < count; i++)
            {
                var body = stage.blocks[i];
                if (!body || body.isKinematic || !body.gameObject.activeSelf) continue;
                Vector3 velocity = (body.transform.position - before[i]) * fps;
                (Quaternion.Inverse(beforeTurn[i]) * body.transform.rotation).ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180) angle -= 360;
                Vector3 spin = float.IsNaN(axis.x) ? Vector3.zero : beforeTurn[i] * axis * (angle * Mathf.Deg2Rad * fps);
                // A block that was resting stays resting; a moving one keeps moving.
                if (velocity.sqrMagnitude < .0004f && spin.sqrMagnitude < .0004f) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.Sleep(); continue; }
                body.linearVelocity = Vector3.ClampMagnitude(velocity, 80);
                body.angularVelocity = Vector3.ClampMagnitude(spin, 40);
                body.WakeUp();
            }
            Physics.SyncTransforms();
        }

        // The new take was recorded from the branch frame on. Put the frames from before the branch in front of it.
        public static void KeepEarlierFrames(StudioTrack track, StudioTake fresh, StudioPass pass)
        {
            var old = track.ActiveTake;
            if (old == null || old == fresh || old.start >= fresh.start) return;
            var joined = new List<byte[]>();
            for (int frame = old.start; frame < fresh.start; frame++)
                joined.Add(old.Covers(frame) ? old.frames[frame - old.start] : Array.Empty<byte>());
            joined.AddRange(fresh.frames);
            fresh.start = old.start;
            fresh.frames.Clear();
            fresh.frames.AddRange(joined);
        }

        // Label, parent, scores and a thumbnail for a freshly recorded world take.
        public static void Describe(StudioStage stage, StudioTake fresh, StudioTake parent, StudioPass pass, bool branched)
        {
            var note = NoteOf(fresh);
            note.blocks = stage.blocks.Length;
            if (branched && parent != null) { note.parent = parent.id; note.branchFrame = pass.branchFrom; }
            note.label = pass.note;
            var score = StudioVariations.Score(stage, fresh);
            note.fallen = score.fallen; note.travelled = score.travelled; note.targetDistance = score.targetDistance; note.inZone = score.inZone;
            if (!string.IsNullOrEmpty(StudioVariations.RunningGroup)) note.group = StudioVariations.RunningGroup;
            fresh.meta = JsonUtility.ToJson(note);
            if (branched) fresh.name = $"Branch at {pass.branchFrom / (float)stage.Fps:0.00}s" + (pass.note.Length > 0 ? " · " + pass.note : "");
            else if (pass.note.Length > 0) fresh.name = pass.note;
            try
            {
                // The picture is taken where the run stopped, which is usually the most telling moment.
                StudioCamera.SavePng(Thumbnail(stage, fresh));
            }
            catch (Exception error) { Debug.LogWarning("Trailer Studio: no thumbnail for the new world take. " + error.Message); }
        }
    }
}
