using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Small helpers for human-looking motion that is the same every time it is played:
    // seeded random numbers (no Random state), smooth wobble, and the easing a head or hand moves with.
    public static class StudioHuman
    {
        // A stable number for a piece of text (mark ids), the same on every machine.
        public static int SeedOf(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in text ?? "") { hash ^= c; hash *= 16777619; }
                return (int)hash;
            }
        }

        static uint Mix(uint a)
        {
            unchecked
            {
                a ^= a >> 16; a *= 0x7feb352d;
                a ^= a >> 15; a *= 0x846ca68b;
                a ^= a >> 16;
                return a;
            }
        }

        // A random number from 0 to 1 that depends only on (seed, index, salt).
        public static float Rand(int seed, int index, int salt)
        {
            unchecked
            {
                uint h = Mix((uint)seed ^ Mix((uint)index * 0x9E3779B9u + (uint)salt * 0x85EBCA6Bu));
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        public static float Range(int seed, int index, int salt, float min, float max) => Mathf.Lerp(min, max, Rand(seed, index, salt));

        // Minimum-jerk ease: how a person's head or hand moves from one place to another (slow start, slow end).
        public static float Ease(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * u * (10 - 15 * u + 6 * u * u);
        }

        // Smooth wandering between -1 and 1, about 'hz' times a second, never repeating exactly.
        public static float Wobble(int seed, int salt, float time, float hz)
        {
            float a = Rand(seed, 1, salt) * 6.283f, b = Rand(seed, 2, salt) * 6.283f, c = Rand(seed, 3, salt) * 6.283f;
            float w = 6.283f * hz;
            return (Mathf.Sin(time * w + a) + .5f * Mathf.Sin(time * w * 1.73f + b) + .25f * Mathf.Sin(time * w * 2.61f + c)) / 1.75f;
        }

        // Yaw (degrees, 0 = +z) of a flat direction.
        public static float Yaw(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        // Pitch (degrees, positive = looking down, as Unity's x rotation) from 'from' toward 'to'.
        public static float Pitch(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float flat = new Vector2(d.x, d.z).magnitude;
            return -Mathf.Atan2(d.y, Mathf.Max(.001f, flat)) * Mathf.Rad2Deg;
        }
    }
}
