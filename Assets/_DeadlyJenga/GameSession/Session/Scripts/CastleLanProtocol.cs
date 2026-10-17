using System;
using System.IO;
using System.Text;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public static class CastleLanProtocol
    {
        public const string Version = "DeadlyJenga-LAN-1";
        public const byte Hello = 1, Welcome = 2, World = 3, Player = 4;
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
        public static Snapshot DecodeWorld(byte[] data)
        {
            using var r = Reader(data, World);
            var s = new Snapshot { revision = r.ReadInt32(), settings = Text(r, 4096), origin = ReadPose(r), originScale = Vector(r) };
            int count = Count(r, 20000); s.blocks = new Pose[count]; s.active = new bool[count];
            for (int i = 0; i < count; i++) { s.active[i] = r.ReadBoolean(); s.blocks[i] = ReadPose(r); }
            s.actors = new Actor[Count(r, 512)];
            for (int i = 0; i < s.actors.Length; i++) s.actors[i] = ReadActor(r);
            if (r.BaseStream.Position != r.BaseStream.Length) throw new InvalidDataException("Trailing LAN state data");
            return s;
        }
    }
}
