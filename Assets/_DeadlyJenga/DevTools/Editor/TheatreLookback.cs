using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    internal sealed class TheatreLookbackFrames
    {
        readonly Queue<byte[]> frames = new Queue<byte[]>();
        readonly int maxFrames, maxBytes;
        int bytes;

        public TheatreLookbackFrames(int maxFrames, int maxBytes)
        {
            this.maxFrames = maxFrames; this.maxBytes = maxBytes;
        }

        public int Count => frames.Count;

        public void Clear() { frames.Clear(); bytes = 0; }

        public bool Add(byte[] frame)
        {
            if (frame.Length > maxBytes) return false;
            while (frames.Count >= maxFrames || bytes + frame.Length > maxBytes)
                bytes -= frames.Dequeue().Length;
            frames.Enqueue(frame);
            bytes += frame.Length;
            return true;
        }

        public void Write(BinaryWriter output)
        {
            foreach (var frame in frames) output.Write(frame);
        }
    }

    internal sealed class TheatreLookback : IDisposable
    {
        public const int Fps = 12, Seconds = 10;
        const int Budget = 24 * 1024 * 1024;
        readonly TheatreLookbackFrames frames = new TheatreLookbackFrames(Fps * Seconds, Budget);
        readonly TheatreMotionTape recorder;
        readonly MemoryStream scratch = new MemoryStream();
        readonly BinaryWriter writer;
        readonly CandyPlayerAvatar[] actors;
        readonly (uint accessories, uint style)[] wardrobe;
        readonly TheatrePlan plan;
        readonly IWorldQuery world;
        readonly Rigidbody[] blocks;
        double nextSample, lastSample;

        public TheatreLookback(TheatrePlan plan, IWorldQuery world, Vector3 stage, CandyPlayerAvatar[] actors)
        {
            this.plan = plan;
            this.world = world;
            this.actors = actors;
            wardrobe = actors.Select(Look).ToArray();
            blocks = world.Blocks.ToArray();
            recorder = new TheatreMotionTape(stage, blocks, plan.cast.Select(track => track.id).ToArray(), actors);
            writer = new BinaryWriter(scratch);
        }

        public int Count => frames.Count;

        static (uint accessories, uint style) Look(CandyPlayerAvatar avatar) =>
            ((uint)avatar.Appearance.accessories, CandyPlayerAvatar.StyleBits(avatar.Appearance));

        public bool Matches(IWorldQuery liveWorld, string map, CandyPlayerAvatar[] liveActors) =>
            ReferenceEquals(world, liveWorld) && map == plan.map && actors.SequenceEqual(liveActors) && blocks.SequenceEqual(liveWorld.Blocks);

        public void Sample(double now)
        {
            if (now < nextSample) return;
            if (lastSample > 0 && now - lastSample > 2d / Fps) frames.Clear();
            for (int i = 0; i < actors.Length; i++)
            {
                if (!actors[i]) throw new InvalidOperationException("A recorded avatar left the match.");
                var look = Look(actors[i]);
                if (look == wardrobe[i]) continue;
                wardrobe[i] = look;
                frames.Clear();
            }
            lastSample = now;
            nextSample += 1d / Fps;
            if (nextSample <= now) nextSample = now + 1d / Fps;
            scratch.SetLength(0);
            scratch.Position = 0;
            recorder.WriteSample(writer);
            if (scratch.Length > Budget || !frames.Add(scratch.ToArray()))
                throw new InvalidOperationException("One frame exceeded the look-back memory budget.");
        }

        public void Save(string folder)
        {
            if (Count == 0) throw new InvalidOperationException("No look-back frames are buffered yet.");
            float length = Count / (float)Fps;
            var clip = new TheatrePlan
            {
                title = "Current-match look-back", fps = Fps, width = 640, height = 360,
                map = plan.map, cast = plan.cast, observedMatch = true, warmup = 0,
                shots = new[] { new TheatrePlan.TheatreShot { name = "Look-back", start = 0, duration = length } }
            };
            using (var output = new BinaryWriter(File.Open(Path.Combine(folder, "motion.djt"), FileMode.CreateNew)))
            {
                recorder.WriteHeader(output, plan.map, Fps, Count);
                frames.Write(output);
            }
            File.WriteAllText(Path.Combine(folder, "recipe.json"), JsonUtility.ToJson(clip, true));
            File.WriteAllText(Path.Combine(folder, "take.txt"),
                $"Map={plan.map}\nFrames={Count}\nFPS={Fps}\nOpt-in current-match look-back. Motion only. Nominal wall-clock sampling.\n");
        }

        public void Dispose()
        {
            writer.Dispose();
            recorder.Dispose();
            frames.Clear();
        }
    }
}
