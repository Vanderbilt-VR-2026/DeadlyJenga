using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal sealed class ShrineTownLayout
    {
        public const string Preset = "shrine-town";
        readonly List<CastlePlacement> pieces = new List<CastlePlacement>(360);

        public static List<CastlePlacement> Build()
        {
            var town = new ShrineTownLayout();
            // Street runs north from the gate to the five-bay worship hall.
            town.Box(0, 0, 20, 37, 4, 22, CastleSection.Keep);
            for (int i = 0; i < 8; i++)
                town.Box(0, 0, 1.5f + i, 11, (i + 1) * .5f, 1, CastleSection.Keep);
            town.Hall(0, 20, 32, 17, 4, 8, 2, 6);
            town.Hall(-32, 27, 10, 10, 0, 5, 3, 2, true);
            town.Hall(30, 20, 10, 10, 0, 4, 4, 2);
            town.Hall(-30, -2, 16, 12, 0, 4, 2, 3);
            town.Hall(-10, -2, 9, 9, 0, 4, 2, 2);
            // Bridge ends bear on both first-storey decks.
            town.Box(-18.75f, 5.6f, -2, 9, .5f, 1.6f, CastleSection.Keep);
            for (int side = -1; side <= 1; side += 2)
            {
                town.Hall(side * 10, -29, 9, 8, 0, 3, 1, 2);
                town.Hall(side * 11, -15, 10, 8, 0, 3, 2, 2);
                town.Hall(side * 32, -30, 12, 9, 0, 3, 1, 3);
                town.Hall(side * 32, -16, 9, 8, 0, 3, 1, 2);
            }
            town.Hall(29, 0, 14, 10, 0, 4, 2, 3);
            town.Hall(10, -2, 8, 8, 0, 3, 1, 2);
            town.Hall(-30, 12, 8, 6, 0, 3.6f, 1, 2);
            town.Hall(31, -42, 8, 6, 0, 3.8f, 1, 2);
            town.Hall(-19, -38, 7, 6, 0, 3.4f, 1, 2);
            town.Hall(19, 4, 7, 7, 0, 3.6f, 1, 2);
            return town.pieces;
        }

        // Each pitched roof is one moving body. Both slopes travel with the deck beneath them.
        public static void AddRoofColliders(Rigidbody body, Vector3 size)
        {
            if (Mathf.Abs(size.y - 1.25f) > .01f && Mathf.Abs(size.y - 1.35f) > .01f && Mathf.Abs(size.y - 1.4f) > .01f) return;
            var frame = new GameObject("Roof collision").transform;
            frame.SetParent(body.transform, false);
            frame.localScale = new Vector3(1 / size.x, 1 / size.y, 1 / size.z);
            float run = size.z / 2, rise = run * .44f;
            for (int side = -1; side <= 1; side += 2)
            {
                var slope = new GameObject("Roof slope");
                slope.layer = body.gameObject.layer;
                slope.transform.SetParent(frame, false);
                slope.transform.localPosition = new Vector3(0, (size.y + rise) / 2, side * run / 2);
                slope.transform.localRotation = Quaternion.Euler(side * Mathf.Atan(.44f) * Mathf.Rad2Deg, 0, 0);
                slope.AddComponent<BoxCollider>().size = new Vector3(size.x, .22f, Mathf.Sqrt(run * run + rise * rise));
            }
        }

        void Box(float x, float bottom, float z, float width, float height, float depth, CastleSection section, float yaw = 0)
        {
            pieces.Add(new CastlePlacement(new Vector3(x, bottom + height / 2, z),
                Quaternion.Euler(0, yaw, 0), section, Mathf.FloorToInt(bottom / 2), new Vector3(width, height, depth)));
        }

        void Hall(float x, float z, float width, float depth, float y, float height, int levels, int columns, bool bell = false)
        {
            for (int level = 0; level < levels; level++)
            {
                float span = width - 2, reach = depth - 2;
                for (int side = -1; side <= 1; side += 2)
                {
                    float row = z + side * reach / 2;
                    for (int bay = 0; bay < columns; bay++)
                        Box(x + span * (bay / (columns - 1f) - .5f), y, row, 1, height, 1, CastleSection.Wall);
                    Box(x, y + height, row, width, .6f, 1, CastleSection.Wall);
                }
                if (!bell || level + 1 < levels)
                {
                    float bayWidth = span / (columns - 1);
                    bool main = x == 0;
                    float panel = main || bell || levels == 4 ? .44f : level == 0 ? .41f : .42f;
                    for (int bay = 0; bay < columns - 1; bay++)
                    {
                        float center = x - span / 2 + (bay + .5f) * bayWidth;
                        bool sanctuary = main && bay == (columns - 2) / 2 && level == 0;
                        Box(center, y, z + reach / 2, bayWidth - 1.05f, height,
                            sanctuary ? .43f : .42f, CastleSection.Keep);
                        if (sanctuary) continue;
                        // A recessed shop front leaves a doorway beside the end post.
                        Box(center - (columns == 2 ? .55f : 0), y, z - reach / 2 + .75f,
                            bayWidth - (columns == 2 ? 2.2f : 1.05f), height, panel, CastleSection.Keep);
                    }
                    for (int side = -1; side <= 1; side += 2)
                        Box(x + side * span / 2, y, z, reach - 1.1f, height, .42f, CastleSection.Keep, 90);
                }
                y += height + .6f;
                bool last = level + 1 == levels;
                float cap = last ? (bell ? 1.4f : levels == 4 ? 1.35f : 1.25f) : 1;
                Box(x, y, z, width + 1, cap, depth + 1, CastleSection.Corner);
                y += cap;
                width *= .80f;
                depth *= .80f;
            }
        }
    }
}
