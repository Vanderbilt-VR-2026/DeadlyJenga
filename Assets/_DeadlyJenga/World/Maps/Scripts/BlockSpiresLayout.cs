using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static class BlockSpiresLayout
    {
        public const string Preset = "block-spires";
        public const int Summit = 27;
        public const float Edge = 1f;
        public const float Pitch = 1.025f;

        // Each entry is a complete column, measured in cubes from the sand.
        // The lower north face has three landings for recovering grip.
        static readonly int[,] Core = {
            { 23, 24, 24, 22 },
            { 25, 27, 26, 24 },
            { 24, 26, 26, 25 },
            { 22, 24, 19, 22 },
            {  0,  0, 12,  0 },
            {  0,  0,  5,  0 },
            {  0,  0,  1,  0 }
        };
        // Independent needles. The tallest are behind the crown in the wide view.
        static readonly Vector3[] Satellites = {
            new(-8.4f, 17, 1.9f), new(-8.2f, 12, -3.8f), new(-6.8f, 19, -6.3f),
            new(-2.9f, 8, -7.2f), new(2.6f, 18, -7), new(6, 14, -5.8f),
            new(8.5f, 18, -3.9f), new(8.3f, 10, 0.5f), new(7.5f, 23, 3.1f),
            new(6.5f, 15, 5.6f), new(4.3f, 20, 6.6f), new(2.6f, 25, 6.4f),
            new(-1.8f, 20, 6.7f), new(-4.7f, 26, 5.8f), new(-6.4f, 24, 3.9f),
            new(-9.4f, 20, -0.8f), new(-5.7f, 9, -1.7f), new(5.3f, 11, -1.2f),
        };

        internal static MagneticStructure BuildSupports(Transform root, List<CastlePlacement> layout,
            IReadOnlyList<Rigidbody> bodies)
        {
            var structure = root.gameObject.AddComponent<MagneticStructure>();
            structure.UseBatchedBreakaway();
            for (int i = 0; i < bodies.Count; i++)
            {
                bool baseCube = layout[i].Course == 0;
                var support = bodies[i].gameObject.AddComponent<MagneticBlock>();
                support.foundationAnchor = baseCube;
                support.breakawayResistance = baseCube ? 9 : 5;
                structure.Add(support);
                if (!baseCube) structure.Connect(i - 1, i);
            }
            structure.Recompute();
            return structure;
        }

        public static List<CastlePlacement> Build()
        {
            var blocks = new List<CastlePlacement>(760);
            int column = 0;
            void Stack(float x, float z, int height, bool core)
            {
                uint state = 1031u + (uint)column++ * 7919u;
                for (int y = 0; y < height; y++)
                {
                    state = unchecked(state * 1664525u + 1013904223u);
                    var section = (CastleSection)((state >> 16) % 3);
                    if (core && y == height - 1 && height >= 25)
                        section = height == 25 ? CastleSection.Corner : height == Summit ? CastleSection.Wall : CastleSection.Keep;
                    var center = new Vector3(x, (y + .5f) * Edge, z);
                    blocks.Add(new CastlePlacement(center, Quaternion.identity, section, y, Vector3.one * Edge));
                }
            }
            for (int z = 0; z < Core.GetLength(0); z++)
                for (int x = 0; x < Core.GetLength(1); x++)
                    if (Core[z,x] > 0) Stack((x - 1.5f) * Pitch, (z - 1.5f) * Pitch, Core[z,x], true);
            foreach (var needle in Satellites) Stack(needle.x, needle.z, (int)needle.y, false);
            return blocks;
        }
    }
}
