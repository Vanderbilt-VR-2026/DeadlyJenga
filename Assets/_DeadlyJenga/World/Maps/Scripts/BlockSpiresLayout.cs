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
        static readonly Vector3Int[] Satellites = {
            new(-7,14,-2), new(-6,10,-5), new(-4,17,-6), new(-1,8,-7),
            new(2,18,-6), new(5,11,-5), new(7,15,-3), new(8,8,0),
            new(6,21,2), new(7,12,5), new(4,17,6), new(2,25,5),
            new(-2,20,6), new(-4,26,4), new(-6,22,2), new(-8,16,1),
            new(-5,7,-1), new(4,9,-2)
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
            void Stack(float x, float z, int height)
            {
                uint state = 1031u + (uint)column++ * 7919u;
                for (int y = 0; y < height; y++)
                {
                    state = unchecked(state * 1664525u + 1013904223u);
                    var section = (CastleSection)((state >> 16) % 3);
                    var center = new Vector3(x * Pitch, (y + .5f) * Edge, z * Pitch);
                    blocks.Add(new CastlePlacement(center, Quaternion.identity, section, y, Vector3.one * Edge));
                }
            }
            for (int z = 0; z < Core.GetLength(0); z++)
                for (int x = 0; x < Core.GetLength(1); x++)
                    if (Core[z,x] > 0) Stack(x - 1.5f, z - 1.5f, Core[z,x]);
            foreach (var needle in Satellites) Stack(needle.x, needle.z, needle.y);
            return blocks;
        }
    }
}
