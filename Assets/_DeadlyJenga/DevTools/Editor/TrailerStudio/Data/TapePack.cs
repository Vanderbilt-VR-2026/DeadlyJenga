using System.IO;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Small numbers for the tape: positions in whole millimetres, rotations in 6 bytes.
    public static class TapePack
    {
        public const float Millimetre = .001f;

        public static int Mm(float metres) => Mathf.RoundToInt(metres * 1000f);
        public static float Metres(int mm) => mm * Millimetre;

        public static void WritePosition(BinaryWriter w, Vector3 p)
        {
            w.Write(Mm(p.x)); w.Write(Mm(p.y)); w.Write(Mm(p.z));
        }

        public static Vector3 ReadPosition(BinaryReader r) =>
            new Vector3(Metres(r.ReadInt32()), Metres(r.ReadInt32()), Metres(r.ReadInt32()));

        // "Smallest three": drop the largest of x, y, z, w and store the other three in 15 bits each.
        // The dropped one is rebuilt from the fact that a rotation has length 1.
        const float Range = .70710678f;   // the three kept parts are never larger than 1/sqrt(2)
        const int Steps = 32767;

        public static ulong PackRotation(Quaternion q)
        {
            float[] v = { q.x, q.y, q.z, q.w };
            int largest = 0;
            for (int i = 1; i < 4; i++) if (Mathf.Abs(v[i]) > Mathf.Abs(v[largest])) largest = i;
            float sign = v[largest] < 0 ? -1 : 1;
            ulong packed = (ulong)largest;
            for (int i = 0; i < 4; i++)
            {
                if (i == largest) continue;
                float unit = Mathf.Clamp01((v[i] * sign / Range + 1) * .5f);
                packed = (packed << 15) | (ulong)Mathf.RoundToInt(unit * Steps);
            }
            return packed;
        }

        public static Quaternion UnpackRotation(ulong packed)
        {
            int largest = (int)(packed >> 45) & 3;
            var v = new float[4];
            float sum = 0;
            int shift = 30;
            for (int i = 0; i < 4; i++)
            {
                if (i == largest) continue;
                v[i] = (((packed >> shift) & 0x7FFF) / (float)Steps * 2 - 1) * Range;
                sum += v[i] * v[i];
                shift -= 15;
            }
            v[largest] = Mathf.Sqrt(Mathf.Max(0, 1 - sum));
            return new Quaternion(v[0], v[1], v[2], v[3]);
        }

        public static void WriteRotation(BinaryWriter w, ulong packed)
        {
            w.Write((ushort)(packed & 0xFFFF)); w.Write((ushort)((packed >> 16) & 0xFFFF)); w.Write((ushort)((packed >> 32) & 0xFFFF));
        }

        public static ulong ReadRotation(BinaryReader r) =>
            r.ReadUInt16() | ((ulong)r.ReadUInt16() << 16) | ((ulong)r.ReadUInt16() << 32);
    }
}
