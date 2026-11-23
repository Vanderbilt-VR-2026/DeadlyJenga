using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal sealed class WalledCastleLayout
    {
        public const string Preset = "walled-castle";
        const CastleSection Red = CastleSection.Wall, Lime = CastleSection.Corner,
            Purple = CastleSection.Keep, Yellow = CastleSection.Gold, Blue = CastleSection.Aqua;
        const float Storey = 3, Slab = .5f;
        readonly List<CastlePlacement> blocks = new List<CastlePlacement>();

        public static List<CastlePlacement> Build()
        {
            var fort = new WalledCastleLayout();
            foreach (int side in new[] { -1, 1 })
            {
                foreach (int end in new[] { -1, 1 })
                    fort.Gallery(side * 24, end * 30, 10, 10, 5, Lime, plinth: true);
                // Street stairs meet the inner corner of the front bastion gallery.
                fort.Steps(side * 20.7f, -21, 0, 1.9f, 8, Storey, -1, Lime, 20);
                fort.Curtain(side * 24, 0, 50, 10, true);
                fort.Curtain(side * 14, -30, 10, 2, false);
                fort.Gallery(side * 6, -30, 6, 8, 3, Yellow, plinth: true);
                fort.GateApron(side);
                for (int row = 0; row < 4; row++)
                    fort.Gallery(side * 16, -18 + row * 11, 5, 7, 1, Blue);
                for (int row = 0; row < 3; row++)
                    fort.Box(side * 16, 3, -12.5f + row * 11, 1.15f, .24f, 4.9f, Blue);
            }
            fort.Curtain(0, 30, 38, 8, false);
            // The crossing overlaps a narrow shelf on each inner gate face.
            foreach (int side in new[] { -1, 1 })
                fort.Box(side * 2.55f, 0, -30, .9f, 8.5f, 3, Yellow);
            fort.Box(0, 8.5f, -30, 6, .5f, 3, Yellow);
            fort.Box(0, 0, -30, 4.2f, .9f, 8, Lime);
            fort.Steps(0, -35.5f, 0, 4.2f, 3, .9f, 1, Lime, 5);
            fort.Steps(0, -24.5f, 0, 4.2f, 3, .9f, -1, Lime, 5);

            for (int strip = 0; strip < 4; strip++)
                fort.Box(-8.25f + strip * 5.5f, 0, -4, 5.5f, .9f, 21, Purple);
            fort.Steps(0, -16, 0, 6, 3, .9f, 1, Lime, 5);
            fort.Gallery(0, -4, 22, 21, 4, Purple, 2.6f, .9f);
            for (int floor = 0; floor < 4; floor++)
                foreach (int side in new[] { -1, 1 })
                {
                    float y = floor == 0 ? 0 : .9f + floor * Storey;
                    fort.Steps(side * (11.65f - floor * 1.3f), -4, y, 1.3f, 7,
                        floor == 0 ? 3.9f : Storey, floor % 2 == 0 ? 1 : -1, Purple, 12);
                }
            // A full-depth exterior flight leads from the east street to the keep.
            fort.Steps(8.5f, 16, 0, 2, 12, 4.5f, 1, Yellow, 30);
            fort.Box(8.5f, 0, 23, 2, 4.5f, 2, Yellow);
            fort.Gallery(0, 19, 15, 12, 3, Yellow, 1.8f, levelHeight: 4.5f);
            fort.RearCrown();
            // The second gallery meets the rear rampart at nine metres.
            fort.Box(0, 9, 25.8f, 1.65f, .24f, 5.8f, Yellow);
            return fort.blocks;
        }

        // The reviewed Blender scene uses the same metre scale. Blender Y is Unity Z.
        void GateApron(int side)
        {
            Box(side * 6, 0, -34.51f, 6.35f, .24f, 1.02f, Lime);
            Box(side * 6, .24f, -34.32f, 6.05f, .22f, .64f, Lime);
            Box(side * 8.34f, .46f, -34.4f, 1.1f, 1.28f, .46f, Yellow);
            Box(side * 8.34f, 1.74f, -34.4f, 1.1f, .20f, .46f, Yellow);
        }

        void RearCrown()
        {
            const float roof = 13.5f;
            const float upperDeck = 16.2f;
            foreach (int side in new[] { -1, 1 })
            {
                foreach (float z in new[] { 15.88f, 21.98f })
                    Box(side * 3, roof, z, .96f, 2.4f, .86f, Yellow);

                // Center each middle pier under its long deck panel.
                float middleX = side < 0 ? -2.25f : 3.25f;
                Box(middleX, roof, 18.8f, .96f, 2.4f, .86f, Yellow);
            }

            // The floor panels border an open stairwell. Both end landings rest on the outer piers.
            Box(-2.3f, 15.9f, 18.7f, 4.8f, .3f, 5.4f, Yellow);
            Box(3.25f, 15.9f, 18.7f, 2.9f, .3f, 5.4f, Yellow);
            Box(0, 15.9f, 15.7f, 9.4f, .3f, .6f, Yellow);
            Box(0, 15.9f, 22, 9.4f, .3f, 1.2f, Yellow);
            Steps(.95f, 18.7f, roof, 1.62f, 5.4f, upperDeck - roof, 1, Yellow, 18);

            for (int i = 0; i < 9; i++)
                foreach (float z in new[] { 15.63f, 22.36f })
                    Box(-4.2f + i * 1.05f, upperDeck, z, .56f, .57f, .47f, Yellow);
            foreach (int side in new[] { -1, 1 })
                foreach (float z in new[] { 16.62f, 17.8f, 19, 20.2f, 21.38f })
                    Box(side * 4.45f, upperDeck, z, .48f, .57f, .54f, Yellow);
        }

        void Box(float x, float bottom, float z, float width, float height, float depth, CastleSection color)
        {
            blocks.Add(new CastlePlacement(new Vector3(x, bottom + height / 2, z), Quaternion.identity,
                color, Mathf.RoundToInt(bottom / Storey), new Vector3(width, height, depth)));
        }

        void Curtain(float x, float z, float length, int bays, bool alongZ)
        {
            float span = length / bays;
            for (int i = 0; i < bays; i++)
            {
                float axis = (i + .5f) * span - length / 2;
                float cx = x + (alongZ ? 0 : axis), cz = z + (alongZ ? axis : 0);
                bool breachBay = !alongZ && cx > 16 && cz < 0;
                for (int course = 0; course < 6; course++)
                {
                    float bottom = course * 1.5f;
                    if (breachBay)
                    {
                        Box(cx, bottom, cz, span, 1.5f, 3.6f, Red);
                        continue;
                    }
                    foreach (int half in new[] { -1, 1 })
                    {
                        float offset = half * span / 4;
                        Box(cx + (alongZ ? 0 : offset), bottom, cz + (alongZ ? offset : 0),
                            alongZ ? 3.6f : span / 2, 1.5f, alongZ ? span / 2 : 3.6f, Red);
                    }
                }
                for (int edge = -1; edge <= 1; edge += 2)
                    Box(cx + (alongZ ? edge * 1.52f : 0), 9, cz + (alongZ ? 0 : edge * 1.52f),
                        alongZ ? .55f : span * .46f, .7f, alongZ ? span * .46f : .55f, Red);
            }
        }

        // Each stair flight is carried by the floor below. Alternate sides so an opening
        // over the arriving flight can share a landing with the next flight.
        void Gallery(float x, float z, float width, float depth, int floors, CastleSection color,
            float setback = 0, float baseY = 0, float levelHeight = Storey, bool plinth = false)
        {
            const float run = 4;
            float lane = Mathf.Min(1.6f, width * .20f);
            if (plinth)
            {
                for (int course = 0; course < 2; course++)
                {
                    float extent = width - .5f * course, length = depth - .5f * course;
                    foreach (int half in new[] { -1, 1 })
                        Box(x + half * extent / 4, course * .35f, z, extent / 2, .35f, length, Lime);
                }
            }
            for (int floor = 0; floor < floors; floor++)
            {
                float w = width - floor * setback, d = depth - floor * setback;
                float sill = plinth && floor == 0 ? .7f : 0;
                float rise = levelHeight - sill;
                float y = baseY + floor * levelHeight + sill, landing = (d - run) / 2;
                int side = floor % 2 == 0 ? -1 : 1;
                Steps(x + side * lane / 2, z, y, lane, run, rise, -side, color, Mathf.CeilToInt(rise / .3f));
                float deckY = y + rise - Slab;
                foreach (int end in new[] { -1, 1 })
                {
                    float cz = z + end * (run + landing) / 2;
                    Box(x, deckY, cz, w, Slab, landing, color);
                    int columns = Mathf.Max(2, Mathf.CeilToInt(w / 4));
                    for (int j = 0; j < columns; j++)
                    {

                        float postWidth = Mathf.Min(1.6f, w / columns * .38f);
                        float cx = x + Mathf.Lerp((-w + postWidth) / 2, (w - postWidth) / 2, (float)j / (columns - 1));
                        float postDepth = Mathf.Min(landing, 1.35f);
                        if (landing > 3.4f)
                        {
                            // Support both the street facade and the back edge of a deep gallery.
                            float offset = (landing - postDepth) / 2;
                            Box(cx, y, cz - offset, postWidth, rise - Slab, postDepth, color);
                            Box(cx, y, cz + offset, postWidth, rise - Slab, postDepth, color);
                        }
                        else Box(cx, y, cz, postWidth, rise - Slab, postDepth, color);
                    }
                }
                // Two side slabs border the rectangular stair opening.
                foreach (int edge in new[] { -1, 1 })
                {
                    float inner = edge == side ? lane : 0;
                    float panel = w / 2 - inner;
                    float center = x + edge * (inner + panel / 2);
                    Box(center, deckY, z, panel, Slab, run, color);
                    // Broad transverse pier supports the panel's centre while leaving both ends open.
                    float pier = w / 2 - lane;
                    Box(x + edge * (w / 2 - pier / 2), y, z, pier, rise - Slab, .9f, color);
                }
                if (floor + 1 == floors) Battlements(x, z, deckY + Slab, w, d, color);
            }
        }

        void Steps(float x, float z, float bottom, float width, float run, float rise,
            int direction, CastleSection color, int count)
        {
            float tread = run / count;
            for (int i = 0; i < count; i++)
                Box(x, bottom + i * rise / count, z + direction * i * tread / 2,
                    width, rise / count, run - i * tread, color);
        }

        void Battlements(float x, float z, float y, float w, float d, CastleSection color)
        {
            foreach (int edge in new[] { -1, 1 })
            {
                int count = Mathf.CeilToInt(w / 2.5f);
                for (int i = 0; i < count; i++)
                    Box(x - w / 2 + (i + .5f) * w / count, y, z + edge * (d / 2 - .3f),
                        w / count * .48f, .65f, .6f, color);
                count = Mathf.Max(1, Mathf.FloorToInt((d - 1.2f) / 2.5f));
                for (int i = 0; i < count; i++)
                    Box(x + edge * (w / 2 - .3f), y, z - d / 2 + .6f + (i + .5f) * (d - 1.2f) / count,
                        .6f, .65f, (d - 1.2f) / count * .48f, color);
            }
        }
    }
}
