using System;
using System.IO;
using System.Text;
using UnityEngine;
using DeadlyJenga.CandyPlayers;

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
        public sealed class Actor
        {
            public string id;
            public bool hand, active = true, left;
            public int color;
            public float size;
            public CandyAvatarFrame avatar;
            public CandyHandPose target;
            public Pose[] bones = Array.Empty<Pose>();
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
        public static int Count(BinaryReader r, int max)
        {
            int count = r.ReadInt32();
            if (count < 0 || count > max) throw new InvalidDataException("Invalid LAN object count");
            return count;
        }
        public static string Text(BinaryReader r, int max = 256)
        {
            string s = r.ReadString();
            if (s.Length > max) throw new InvalidDataException("LAN text too long");
            return s;
        }
        static float Number(BinaryReader r)
        {
            float f = r.ReadSingle();
            if (float.IsNaN(f) || float.IsInfinity(f) || Mathf.Abs(f) > 1000000) throw new InvalidDataException("Invalid LAN coordinate");
            return f;
        }
        public static void Write(BinaryWriter w, Vector3 p) { w.Write(p.x); w.Write(p.y); w.Write(p.z); }
        public static Vector3 Vector(BinaryReader r) => new Vector3(Number(r), Number(r), Number(r));
        public static void Write(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
        public static Quaternion Rotation(BinaryReader r)
        {
            var q = new Quaternion(Number(r), Number(r), Number(r), Number(r));
            float length = q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w;
            if (length < .01f || length > 4) throw new InvalidDataException("Invalid LAN rotation");
            return q.normalized;
        }
        public static void Write(BinaryWriter w, Pose p) { Write(w, p.position); Write(w, p.rotation); }
        public static Pose ReadPose(BinaryReader r) => new Pose(Vector(r), Rotation(r));
        static void Write(BinaryWriter w, CandyHandPose p)
        {
            w.Write(p.tracked); w.Write(p.skeletonTracked); Write(w, p.position); Write(w, p.rotation);
            for (int i = 0; i < 20; i++) Write(w, p.skeleton.Joint(i));
        }
        static CandyHandPose Hand(BinaryReader r)
        {
            var p = new CandyHandPose { tracked = r.ReadBoolean(), skeletonTracked = r.ReadBoolean(), position = Vector(r), rotation = Rotation(r) };
            for (int i = 0; i < 20; i++) p.skeleton.SetJoint(i, Vector(r));
            return p;
        }
        public static void Write(BinaryWriter w, Actor a)
        {
            w.Write(a.id); w.Write(a.hand); w.Write(a.active); w.Write(a.color);
            if (a.hand)
            {
                w.Write(a.left); w.Write(a.size); Write(w, a.target); w.Write(a.bones.Length);
                foreach (var p in a.bones) Write(w, p);
            }
            else
            {
                var f = a.avatar;
                Write(w, f.feet); Write(w, f.head); Write(w, f.headRotation); w.Write(f.scale);
                Write(w, f.left); Write(w, f.right);
            }
        }
        public static Actor ReadActor(BinaryReader r)
        {
            var a = new Actor { id = Text(r), hand = r.ReadBoolean(), active = r.ReadBoolean(), color = r.ReadInt32() };
            if (a.hand)
            {
                a.left = r.ReadBoolean(); a.size = Number(r);
                if (a.size < .1f || a.size > 100) throw new InvalidDataException("Invalid hand size");
                a.target = Hand(r); a.bones = new Pose[Count(r, 16)];
                if (a.bones.Length != 0 && a.bones.Length != 16) throw new InvalidDataException("Invalid hand bones");
                for (int i = 0; i < a.bones.Length; i++) a.bones[i] = ReadPose(r);
            }
            else
            {
                a.avatar = new CandyAvatarFrame { feet = Vector(r), head = Vector(r), headRotation = Rotation(r), scale = Number(r), left = Hand(r), right = Hand(r) };
                if (a.avatar.scale < .1f || a.avatar.scale > 2) throw new InvalidDataException("Invalid avatar scale");
            }
            return a;
        }
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
