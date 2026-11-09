#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static class FusionMotionWireTrace
    {
        const int Capacity = 16384;
        static readonly bool enabled = Array.IndexOf(Environment.GetCommandLineArgs(), "--dj-motion-wire") >= 0;
        static readonly Event[] events = enabled ? new Event[Capacity] : null;
        static int count;
        static int dropped;
        public static long Now => enabled ? Stopwatch.GetTimestamp() : 0;

        public enum Phase { Capture, Context, Encode, Queued, Submitted, Received, Decode, Applied, Ack, Render }

        struct Event
        {
            public long clock, started;
            public int frame, kind, serial, bytes;
            public Phase phase;
        }

        public static void Record(Phase phase, int kind, int serial, int bytes = 0, long started = 0)
        {
            if (!enabled) return;
            if (count == events.Length) { dropped++; return; }
            events[count++] = new Event
            {
                clock = Stopwatch.GetTimestamp(), started = started, frame = Time.frameCount,
                kind = kind, serial = serial, bytes = bytes, phase = phase
            };
        }

        public static void Save(string path)
        {
            if (!enabled) return;
            using var writer = new StreamWriter(path);
            writer.WriteLine($"# stopwatchFrequency={Stopwatch.Frequency}, dropped={dropped}");
            writer.WriteLine("clock,started,frame,phase,kind,serial,bytes");
            for (int i = 0; i < count; i++)
            {
                var item = events[i];
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6}",
                    item.clock, item.started, item.frame, item.phase, item.kind, item.serial, item.bytes));
            }
        }
    }
}
#endif
