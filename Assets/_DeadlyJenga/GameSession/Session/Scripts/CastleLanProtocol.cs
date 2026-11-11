using System;
using System.IO;
using System.Text;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public static class CastleLanProtocol
    {
        public const string Version = "DeadlyJenga-LAN-2";
        public const byte Hello = 1, Welcome = 2, World = 3, Player = 4, WorldDelta = 5;
        public struct Pose
        {
            public Vector3 position;
            public Quaternion rotation;
            public Pose(Vector3 p, Quaternion q) { position = p; rotation = q; }
        }
        // One character in a snapshot. Its state bytes are written and read by the character itself
        // (through Core's INetworkActor); networking never looks inside them.
        public sealed class Actor
        {
            public string id, kind;
            public bool active = true;
            public byte[] state = Array.Empty<byte>();
            public Vector3 position;   // not sent; kept to space out join spawns
        }
        public sealed class Snapshot
        {
            public int revision;
            public string settings;
            public Pose origin;
            public Vector3 originScale;
            public Pose[] blocks;
            public bool[] active;
            public Actor[] actors;
        }
        public static byte[] Encode(byte type, Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) { writer.Write(type); write(writer); }
            return stream.ToArray();
        }
        public static BinaryReader Reader(byte[] data, byte expected)
        {
            var reader = new BinaryReader(new MemoryStream(data, false), Encoding.UTF8);
            if (reader.ReadByte() != expected) { reader.Dispose(); throw new InvalidDataException("Unexpected LAN message"); }
            return reader;
        }
        // Numbers, vectors, rotations and poses use Core's NetWire, the same validated format characters use.
        public static int Count(BinaryReader r, int max) => NetWire.ReadCount(r, max);
        public static string Text(BinaryReader r, int max = 256)
        {
            string s = r.ReadString();
            if (s.Length > max) throw new InvalidDataException("LAN text too long");
            return s;
        }
        public static void Write(BinaryWriter w, Vector3 p) => NetWire.Write(w, p);
        public static Vector3 Vector(BinaryReader r) => NetWire.ReadVector(r);
        public static void Write(BinaryWriter w, Quaternion q) => NetWire.Write(w, q);
        public static Quaternion Rotation(BinaryReader r) => NetWire.ReadRotation(r);
        public static void Write(BinaryWriter w, Pose p) { Write(w, p.position); Write(w, p.rotation); }
        public static Pose ReadPose(BinaryReader r) => new Pose(Vector(r), Rotation(r));
        public static void Write(BinaryWriter w, Actor a)
        {
            w.Write(a.id); w.Write(a.kind); w.Write(a.active); w.Write(a.state.Length); w.Write(a.state);
        }
        public static Actor ReadActor(BinaryReader r)
        {
            var a = new Actor { id = Text(r), kind = Text(r, 32), active = r.ReadBoolean() };
            int length = Count(r, 65536);
            a.state = r.ReadBytes(length);
            if (a.state.Length != length) throw new InvalidDataException("Truncated actor state");
            return a;
        }
        // Packs a character (or a remote copy) through Core.
        public static Actor Capture(string id, INetworkActor source)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) source.WriteState(writer);
            return new Actor { id = id, kind = source.NetworkKind, active = source.NetworkActive, state = stream.ToArray(), position = source.NetworkPosition };
        }
        // Starting state for someone who just joined.
        public static Actor Spawn(string id, string kind, Pose spawn, NetworkContext context)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                NetworkActors.WriteSpawn(kind, writer, new UnityEngine.Pose(spawn.position, spawn.rotation), context);
            return new Actor { id = id, kind = kind, state = stream.ToArray(), position = spawn.position };
        }
        public static BinaryReader StateReader(Actor a) => new BinaryReader(new MemoryStream(a.state, false), Encoding.UTF8);
        public static byte[] EncodeWorld(Snapshot s) => Encode(World, w =>
        {
            w.Write(s.revision); w.Write(s.settings); Write(w, s.origin); Write(w, s.originScale);
            w.Write(s.blocks.Length);
            for (int i = 0; i < s.blocks.Length; i++) { w.Write(s.active[i]); Write(w, s.blocks[i]); }
            w.Write(s.actors.Length); foreach (var a in s.actors) Write(w, a);
        });
        // What one receiver last got from the host, so later world updates only carry blocks that moved.
        public sealed class WorldBaseline
        {
            public int revision = int.MinValue;
            public Pose[] blocks;
            public bool[] active;
            public void Reset() { revision = int.MinValue; blocks = null; active = null; }
        }
        const float MovedDistanceSqr = 0.0005f * 0.0005f;
        const float RotatedDot = 0.999999f;
        static bool Moved(Pose a, Pose b) =>
            (a.position - b.position).sqrMagnitude > MovedDistanceSqr || Mathf.Abs(Quaternion.Dot(a.rotation, b.rotation)) < RotatedDot;
        // Full world the first time (or after a rebuild), then only changed blocks plus all actors.
        public static byte[] EncodeWorldFor(Snapshot s, WorldBaseline baseline)
        {
            int count = s.blocks.Length;
            if (baseline.revision != s.revision || baseline.blocks == null || baseline.blocks.Length != count)
            {
                baseline.revision = s.revision;
                baseline.blocks = (Pose[])s.blocks.Clone();
                baseline.active = (bool[])s.active.Clone();
                return EncodeWorld(s);
            }
            return Encode(WorldDelta, w =>
            {
                w.Write(s.revision); w.Write(count);
                w.Flush(); long countAt = w.BaseStream.Position; w.Write(0);
                int changed = 0;
                for (int i = 0; i < count; i++)
                {
                    if (baseline.active[i] == s.active[i] && !Moved(baseline.blocks[i], s.blocks[i])) continue;
                    baseline.active[i] = s.active[i]; baseline.blocks[i] = s.blocks[i];
                    w.Write(i); w.Write(s.active[i]); Write(w, s.blocks[i]);
                    changed++;
                }
                w.Flush(); long end = w.BaseStream.Position;
                w.BaseStream.Position = countAt; w.Write(changed); w.Flush(); w.BaseStream.Position = end;
                w.Write(s.actors.Length); foreach (var a in s.actors) Write(w, a);
            });
        }
        public static bool IsWorldDelta(byte[] data) => data != null && data.Length > 0 && data[0] == WorldDelta;
        // Rebuilds a full snapshot from the last one this receiver applied plus a delta.
        public static Snapshot DecodeWorldDelta(byte[] data, Snapshot previous)
        {
            using var r = Reader(data, WorldDelta);
            int revision = r.ReadInt32(); int count = Count(r, 20000);
            if (previous == null || previous.revision != revision || previous.blocks.Length != count)
                throw new InvalidDataException("World update does not match the last received world");
            var s = new Snapshot { revision = revision, settings = previous.settings, origin = previous.origin, originScale = previous.originScale,
                blocks = (Pose[])previous.blocks.Clone(), active = (bool[])previous.active.Clone() };
            int changed = Count(r, count);
            for (int c = 0; c < changed; c++)
            {
                int i = r.ReadInt32();
                if (i < 0 || i >= count) throw new InvalidDataException("World update block out of range");
                s.active[i] = r.ReadBoolean(); s.blocks[i] = ReadPose(r);
            }
            s.actors = new Actor[Count(r, data.Length / 16)];
            for (int i = 0; i < s.actors.Length; i++) s.actors[i] = ReadActor(r);
            if (r.BaseStream.Position != r.BaseStream.Length) throw new InvalidDataException("Trailing LAN state data");
            return s;
        }
        public static Snapshot DecodeWorld(byte[] data)
        {
            using var r = Reader(data, World);
            var s = new Snapshot { revision = r.ReadInt32(), settings = Text(r, 4096), origin = ReadPose(r), originScale = Vector(r) };
            int count = Count(r, 20000); s.blocks = new Pose[count]; s.active = new bool[count];
            for (int i = 0; i < count; i++) { s.active[i] = r.ReadBoolean(); s.blocks[i] = ReadPose(r); }
            s.actors = new Actor[Count(r, data.Length / 16)];
            for (int i = 0; i < s.actors.Length; i++) s.actors[i] = ReadActor(r);
            if (r.BaseStream.Position != r.BaseStream.Length) throw new InvalidDataException("Trailing LAN state data");
            return s;
        }
    }
}
