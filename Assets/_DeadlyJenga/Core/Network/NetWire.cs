using System.IO;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // Reading and writing basic values for the network, with checks against bad data.
    public static class NetWire
    {
        public static void Write(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        public static void Write(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
        public static void Write(BinaryWriter w, Pose p) { Write(w, p.position); Write(w, p.rotation); }

        public static float ReadNumber(BinaryReader r)
        {
            float f = r.ReadSingle();
            if (float.IsNaN(f) || float.IsInfinity(f) || Mathf.Abs(f) > 1000000) throw new InvalidDataException("Invalid network number");
            return f;
        }
        public static Vector3 ReadVector(BinaryReader r) => new Vector3(ReadNumber(r), ReadNumber(r), ReadNumber(r));
        public static Quaternion ReadRotation(BinaryReader r)
        {
            var q = new Quaternion(ReadNumber(r), ReadNumber(r), ReadNumber(r), ReadNumber(r));
            float length = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (length < .01f || length > 4) throw new InvalidDataException("Invalid network rotation");
            return q.normalized;
        }
        public static Pose ReadPose(BinaryReader r) => new Pose(ReadVector(r), ReadRotation(r));
        public static int ReadCount(BinaryReader r, int max)
        {
            int count = r.ReadInt32();
            if (count < 0 || count > max) throw new InvalidDataException("Invalid network count");
            return count;
        }
    }
}
