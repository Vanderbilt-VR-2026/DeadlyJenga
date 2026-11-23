using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // The negative-Z approach is open through three transverse galleries.
    public static class PrismaticCitadelLayout
    {
        public const string Preset = "prismatic-citadel";
        public const int HeightCourses = 43;
        const float CrownRadius = 3.2f;
        const float GreenRadius = 2.35f;
        const float FrontRadius = 3.1f;
        const float FrontZ = -21f;
        const float GalleryGap = .04f;

        public static Vector2 Size(CastleSettings settings) =>
            new Vector2(58.4f, 53.1f) * (settings.blockSize.z * .5f);

        public static List<CastlePlacement> Build(CastleSettings settings)
        {
            var result = new List<CastlePlacement>(1200);
            var widthScale = settings.blockSize.z * .5f;
            var courseHeight = settings.blockSize.y;

            void Block(float x, float z, float bottom, float width, float depth, float courses, CastleSection section, float yaw = 0)
            {
                result.Add(new CastlePlacement(
                    new Vector3(x * widthScale, (bottom + courses * .5f) * courseHeight, z * widthScale),
                    Quaternion.Euler(0, yaw, 0), section, Mathf.FloorToInt(bottom),
                    new Vector3(width * widthScale, courses * courseHeight, depth * widthScale)));
            }

            void Tower(float x, float z, float radius, int platformCourse, CastleSection section, float merlons = 2f)
            {
                float pier = radius * .65f;
                float offset = radius - pier * .5f;
                float clear = 2f * (radius - pier);

                void Piers(int course, float courses)
                {
                    for (int sideX = -1; sideX <= 1; sideX += 2)
                    for (int sideZ = -1; sideZ <= 1; sideZ += 2)
                        Block(x + sideX * offset, z + sideZ * offset, course, pier, pier, courses, section);
                }

                void Deck(int course)
                {
                    float stripWidth = 2f * radius / 3f;
                    for (int strip = -1; strip <= 1; strip++)
                        Block(x + strip * stripWidth, z, course, stripWidth, 2f * radius, 1, section);
                }

                for (int course = 0; course < platformCourse; course += 5)
                {
                    Deck(course);
                    Piers(course + 1, 3);

                    // Each lintel spans two piers, and each deck strip above
                    // spans both lintels. Leaving the side bays open aids traversal.
                    for (int side = -1; side <= 1; side += 2)
                        Block(x, z + side * offset, course + 4, 2f * radius, pier, 1, section);
                }

                if (merlons == 0)
                    return;

                Deck(platformCourse);
                Piers(platformCourse + 1, merlons);
                for (int side = -1; side <= 1; side += 2)
                {
                    Block(x, z + side * offset, platformCourse + 1, clear, pier, .4f, section);
                    Block(x + side * offset, z, platformCourse + 1, pier, clear, .4f, section);
                }
            }

            void Gallery(bool alongX, float fixedAxis, float from, float to, int first, int last, bool redFeet = false)
            {
                float start = Mathf.Min(from, to) + GalleryGap;
                float end = Mathf.Max(from, to) - GalleryGap;
                float span = end - start;
                const float deckWidth = 3.1f;
                float bearing = Mathf.Min(1.65f, span * .23f);
                var feet = redFeet ? CastleSection.Wall : CastleSection.Corner;

                void Piece(float axis, float across, float course, float length, float width, float courses, CastleSection section)
                {
                    if (alongX) Block(axis, fixedAxis + across, course, length, width, courses, section);
                    else Block(fixedAxis + across, axis, course, width, length, courses, section);
                }

                void Posts(int bottom, int courses)
                {
                    for (int endSide = -1; endSide <= 1; endSide += 2)
                    {
                        float axis = (start + end) * .5f + endSide * (span - bearing) * .5f;
                        for (int edge = -1; edge <= 1; edge += 2)
                            Piece(axis, edge * .96f, bottom, bearing, 1.18f, courses, feet);
                    }
                }

                Posts(0, first);
                for (int course = first; course <= last; course += 5)
                {
                    float middle = (start + end) * .5f;
                    Piece(middle, 0, course, span, deckWidth, 1, CastleSection.Wall);
                    if (course < last)
                        Posts(course + 1, 4);

                    // Rails sit directly on the deck. The break before each
                    // bearing leaves a way between the deck and the window bays.
                    float railLength = span - 2f * bearing;
                    for (int edge = -1; edge <= 1; edge += 2)
                        Piece(middle, edge * 1.29f, course + 1, railLength, .35f, .55f, CastleSection.Wall);
                }
            }

            void CentralKeep()
            {
                const float centerZ = 6f;
                const float shoulderWidth = 8.4f;
                const float shaftRadius = 2.9f;
                const float thickness = 1.05f;
                const float shoulderCourse = 20f;
                const float facetLength = 1.90f;
                const float pierLength = .56f;
                const float pierOffset = (facetLength - pierLength) * .5f;
                Tower(0, centerZ, CrownRadius, (int)shoulderCourse, CastleSection.Keep, 0);
                for (int strip = -1; strip <= 1; strip++)
                    Block(strip * shoulderWidth / 3f, centerZ, shoulderCourse, shoulderWidth / 3f,
                        shoulderWidth, 1, CastleSection.Keep);

                for (int face = 0; face < 8; face++)
                {
                    void Facet(float along, float course, float width, float courses)
                    {
                        // The next sill straddles two adjacent lintels, tying the
                        // facets to each other instead of making eight freestanding walls.
                        int tier = Mathf.FloorToInt((course - shoulderCourse - 1) / 4);
                        float yaw = face * 45f + (tier % 2) * 22.5f;
                        float radians = yaw * Mathf.Deg2Rad;
                        float sine = Mathf.Sin(radians);
                        float cosine = Mathf.Cos(radians);
                        Block(shaftRadius * sine + along * cosine, centerZ + shaftRadius * cosine - along * sine,
                            course, width, thickness, courses, CastleSection.Keep, yaw);
                    }

                    // Each faceted wall is built from short sills and lintels
                    // with a pair of posts under every lintel.
                    for (int course = (int)shoulderCourse + 1; course < HeightCourses - 2; course += 4)
                    {
                        Facet(0, course, facetLength, 1);
                        Facet(-pierOffset, course + 1, pierLength, 2);
                        Facet(pierOffset, course + 1, pierLength, 2);
                        Facet(0, course + 3, facetLength, 1);
                    }

                    Facet(0, HeightCourses - 2, facetLength, .7f);
                    Facet(0, HeightCourses - 1.3f, .84f, 1.3f);
                }
            }

            CentralKeep();

            // Breakable paving carries survivors across the sand beneath the gates.
            const float pavementStart = -26.5f;
            const float pavementEnd = 2.78f;
            const int pavingBlocks = 10;
            float pavingLength = (pavementEnd - pavementStart) / pavingBlocks;
            for (int slab = 0; slab < pavingBlocks; slab++)
                Block(0, pavementStart + (slab + .5f) * pavingLength, 0, 2.8f, pavingLength - .04f,
                    .2f, slab % 2 == 0 ? CastleSection.Keep : CastleSection.Wall);
            foreach (int side in new[] { -1, 1 })
            {
                Tower(side * 17, 21, CrownRadius, 35, CastleSection.Keep);
                Tower(side * 26, -8, CrownRadius, 30, CastleSection.Keep);
                Tower(side * 26, 8, GreenRadius, 30, CastleSection.Corner);
                Tower(side * 26, 21, GreenRadius, 30, CastleSection.Corner);
                Tower(side * 13, -8, GreenRadius, 30, CastleSection.Corner);
                Tower(side * 13, 6, GreenRadius, 30, CastleSection.Corner);
                Tower(side * 16.5f, FrontZ, GreenRadius, 25, CastleSection.Corner, 4);
                Tower(side * 8.5f, FrontZ, FrontRadius, 15, CastleSection.Wall, .55f);

                Gallery(true, 21, side * (17 + CrownRadius), side * (26 - GreenRadius), 5, 30);
                Gallery(false, side * 26, -8 + CrownRadius, 8 - GreenRadius, 5, 30);
                Gallery(false, side * 26, 8 + GreenRadius, 21 - GreenRadius, 5, 30);
                Gallery(true, -8, side * (13 + GreenRadius), side * (26 - CrownRadius), 5, 25);
                Gallery(false, side * 13, -8 + GreenRadius, 6 - GreenRadius, 5, 30);
                Gallery(true, 6, side * (13 - GreenRadius), side * 4.2f, 5, 30);
                Gallery(true, FrontZ, side * (8.5f + FrontRadius), side * (16.5f - GreenRadius), 5, 15, true);
                Gallery(false, side * 16.5f, FrontZ + GreenRadius, -8 - GreenRadius, 5, 20);

                // Longitudinal runs meet the ends of all three axial gates.
                // The central opening stays clear down to the foundation.
                Gallery(false, side * 3.25f, FrontZ + 1.55f, -11 - 1.55f, 3, 13, true);
                Gallery(false, side * 3.25f, -11 + 1.55f, -1 - 1.55f, 3, 18, true);

                // Four freestanding risers connect the pavers to the first cross deck.
                for (int step = 0; step < 4; step++)
                    Block(side * 2.19f, -26.5f + (step + .5f) * .975f, 0, 1.36f, .95f,
                        (step + 1) * .85f, CastleSection.Wall);

                Block(side * 8.5f, -25.55f, 0, 2.1f, 2f, .75f, CastleSection.Wall);
                Block(side * 8.5f, -24.35f, 0, 2.1f, .4f, 1.5f, CastleSection.Wall);
            }

            Gallery(true, FrontZ, -8.5f + FrontRadius, 8.5f - FrontRadius, 3, 13);
            Gallery(true, -11, -5.4f, 5.4f, 3, 18, true);
            Gallery(true, -1, -5.4f, 5.4f, 3, 23, true);
            return result;
        }
    }
}
