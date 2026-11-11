using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Moonlit pagodas: cross-stacked towers with wide roof decks and narrow beams between deck rings.
    public static class NinjaTempleLayout
    {
        public const string Preset = "ninja";
        public const float Ring = 24, DeckThickness = 1, BeamThickness = .5f, BeamWidth = 1.6f;
        public static readonly Vector2 Size = new Vector2(66, 66);
        public const int HeightCourses = 23;
        struct Tier { public int courses; public float deck; public Tier(int c, float d) { courses = c; deck = d; } }
        static readonly Tier[] Great = { new Tier(5, 13), new Tier(4, 11.5f), new Tier(4, 10), new Tier(3, 8.5f), new Tier(3, 7) };
        static readonly Tier[] Corner = { new Tier(5, 11), new Tier(4, 9.5f), new Tier(3, 8) };
        static readonly Tier[] Shrine = { new Tier(5, 10), new Tier(2, 8) };
        public static readonly Vector3 FinialSize = new Vector3(1.2f, 3, 1.2f);

        public static List<CastlePlacement> Build(CastleSettings s)
        {
            var result = new List<CastlePlacement>(360);
            Pagoda(result, s, Vector3.zero, Great, CastleSection.Wall);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2) Pagoda(result, s, new Vector3(x * Ring, 0, z * Ring), Corner, CastleSection.Wall);
            for (int i = 0; i < 4; i++) Pagoda(result, s, Quaternion.Euler(0, i * 90, 0) * new Vector3(0, 0, Ring), Shrine, CastleSection.Corner);
            float deckTop = Great[0].courses * s.blockSize.y + DeckThickness;
            int course = Great[0].courses + 1;
            for (int i = 0; i < 4; i++)
            {
                var axis = Quaternion.Euler(0, i * 90, 0);
                Beam(result, axis * Vector3.forward * (Great[0].deck * .5f - 1), axis * Vector3.forward * (Ring - Shrine[0].deck * .5f + 1), deckTop, course);
                for (int side = -1; side <= 1; side += 2)
                {
                    var shrine = axis * new Vector3(0, 0, Ring);var corner = axis * new Vector3(side * Ring, 0, Ring);
                    var dir = (corner - shrine).normalized;
                    Beam(result, shrine + dir * (Shrine[0].deck * .5f - 1), corner - dir * (Corner[0].deck * .5f - 1), deckTop, course);
                }
            }
            for (int q = 0; q < 4; q++) Stair(result, s, Quaternion.Euler(0, q * 90, 0), Great[0].deck * .5f, Great[0].courses);
            return result;
        }
        // Grounded block columns rising to the great pagoda's first roof ring, one per quadrant.
        static void Stair(List<CastlePlacement> output, CastleSettings s, Quaternion quadrant, float deckHalf, int courses)
        {
            for (int k = 0; k < courses; k++)
            {
                var center = new Vector3(deckHalf + s.blockSize.x * .5f, 0, 4.5f + k * s.blockSize.z);
                for (int c = 0; c < courses - k; c++)
                    output.Add(new CastlePlacement(quadrant * (center + Vector3.up * ((c + .5f) * s.blockSize.y)), quadrant, CastleSection.Keep, c));
            }
        }
        static void Pagoda(List<CastlePlacement> output, CastleSettings s, Vector3 center, Tier[] tiers, CastleSection section)
        {
            float y = 0, offset = (s.blockSize.x - s.blockSize.z) * .5f; int course = 0;
            foreach (var tier in tiers)
            {
                for (int c = 0; c < tier.courses; c++, course++)
                {
                    var r = Quaternion.Euler(0, (course % 2) * 90, 0);
                    for (int side = -1; side <= 1; side += 2)
                        output.Add(new CastlePlacement(center + Vector3.up * (y + s.blockSize.y * .5f) + r * new Vector3(0, 0, side * offset), r, section, course));
                    y += s.blockSize.y;
                }
                output.Add(new CastlePlacement(center + Vector3.up * (y + DeckThickness * .5f), Quaternion.identity, CastleSection.Keep, course++, new Vector3(tier.deck, DeckThickness, tier.deck)));
                y += DeckThickness;
            }
            output.Add(new CastlePlacement(center + Vector3.up * (y + FinialSize.y * .5f), Quaternion.identity, CastleSection.Corner, course, FinialSize));
        }
        static void Beam(List<CastlePlacement> output, Vector3 a, Vector3 b, float deckTop, int course)
        {
            var flat = b - a; float length = flat.magnitude;
            output.Add(new CastlePlacement((a + b) * .5f + Vector3.up * (deckTop + BeamThickness * .5f), Quaternion.LookRotation(flat / length) * Quaternion.Euler(0, 90, 0), CastleSection.Corner, course, new Vector3(length, BeamThickness, BeamWidth)));
        }
        public static bool IsDeck(Vector3 size) => Mathf.Abs(size.y - DeckThickness) < .01f && size.x > 6.5f && Mathf.Abs(size.x - size.z) < .01f;
        public static bool IsBeam(Vector3 size) => Mathf.Abs(size.y - BeamThickness) < .01f && Mathf.Abs(size.z - BeamWidth) < .01f;
        public static bool IsFinial(Vector3 size) => (size - FinialSize).sqrMagnitude < .0001f;
    }
}
