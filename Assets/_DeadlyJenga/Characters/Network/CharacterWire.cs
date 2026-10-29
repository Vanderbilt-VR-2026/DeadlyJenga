using System;
using System.IO;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CharacterNetwork
{
    // How survivors and hands write themselves for the network. Only Characters reads this format.
    public static class CharacterWire
    {
        public static void WriteHandPose(BinaryWriter w, CandyHandPose p)
        {
            w.Write(p.tracked); w.Write(p.skeletonTracked); NetWire.Write(w, p.position); NetWire.Write(w, p.rotation);
            for (int i = 0; i < 20; i++) NetWire.Write(w, p.skeleton.Joint(i));
        }
        public static CandyHandPose ReadHandPose(BinaryReader r)
        {
            var p = new CandyHandPose { tracked = r.ReadBoolean(), skeletonTracked = r.ReadBoolean(), position = NetWire.ReadVector(r), rotation = NetWire.ReadRotation(r) };
            for (int i = 0; i < 20; i++) p.skeleton.SetJoint(i, NetWire.ReadVector(r));
            return p;
        }

        // Survivor: body frame plus color.
        public static void WriteSurvivor(BinaryWriter w, CandyAvatarFrame f, int color)
        {
            NetWire.Write(w, f.feet); NetWire.Write(w, f.head); NetWire.Write(w, f.headRotation); w.Write(f.scale);
            WriteHandPose(w, f.left); WriteHandPose(w, f.right); w.Write(color);
        }
        public static void ReadSurvivor(BinaryReader r, out CandyAvatarFrame frame, out int color)
        {
            frame = new CandyAvatarFrame { feet = NetWire.ReadVector(r), head = NetWire.ReadVector(r), headRotation = NetWire.ReadRotation(r), scale = NetWire.ReadNumber(r), left = ReadHandPose(r), right = ReadHandPose(r) };
            if (frame.scale < .1f || frame.scale > 2) throw new InvalidDataException("Invalid avatar scale");
            color = r.ReadInt32();
        }

        // Hand: handedness, size, finger target and (when simulated) the 16 bone poses.
        public static void WriteHand(BinaryWriter w, bool left, float size, CandyHandPose target, Rigidbody[] bodies, int look = 0)
        {
            w.Write(left); w.Write(size); WriteHandPose(w, target); w.Write((byte)Mathf.Clamp(look, 0, 255));
            int count = bodies != null ? bodies.Length : 0; w.Write(count);
            for (int i = 0; i < count; i++) NetWire.Write(w, new Pose(bodies[i].position, bodies[i].rotation));
        }
        public static void WriteHand(BinaryWriter w, bool left, float size, CandyHandPose target, Pose[] bones, int look = 0)
        {
            w.Write(left); w.Write(size); WriteHandPose(w, target); w.Write((byte)Mathf.Clamp(look, 0, 255));
            int count = bones != null ? bones.Length : 0; w.Write(count);
            for (int i = 0; i < count; i++) NetWire.Write(w, bones[i]);
        }
        public static void ReadHand(BinaryReader r, out bool left, out float size, out CandyHandPose target, out Pose[] bones) => ReadHand(r, out left, out size, out target, out bones, out _);
        public static void ReadHand(BinaryReader r, out bool left, out float size, out CandyHandPose target, out Pose[] bones, out int look)
        {
            left = r.ReadBoolean(); size = NetWire.ReadNumber(r);
            if (size < .1f || size > 100) throw new InvalidDataException("Invalid hand size");
            target = ReadHandPose(r);
            look = r.ReadByte();
            bones = new Pose[NetWire.ReadCount(r, 16)];
            if (bones.Length != 0 && bones.Length != 16) throw new InvalidDataException("Invalid hand bones");
            for (int i = 0; i < bones.Length; i++) bones[i] = NetWire.ReadPose(r);
        }
    }
}
