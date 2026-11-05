using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    static class EmoteIcons
    {
        // Coordinates describe each pose on a 12 by 12 grid, starting at the bottom left.
        public static Sprite Create(string emote)
        {
            var texture = new Texture2D(48, 48, TextureFormat.RGBA32, false);
            texture.name = emote + " wheel icon";
            texture.SetPixels32(new Color32[48 * 48]);

            void Dot(int x, int y, int radius)
            {
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                        if (dx * dx + dy * dy <= radius * radius && x + dx >= 0 && x + dx < 48 && y + dy >= 0 && y + dy < 48)
                            texture.SetPixel(x + dx, y + dy, Color.white);
            }
            void Line(int x, int y, int toX, int toY)
            {
                int steps = Mathf.Max(1, Mathf.Max(Mathf.Abs(toX - x), Mathf.Abs(toY - y)) * 4);
                for (int i = 0; i <= steps; i++)
                    Dot(Mathf.RoundToInt(Mathf.Lerp(x * 4, toX * 4, (float)i / steps)),
                        Mathf.RoundToInt(Mathf.Lerp(y * 4, toY * 4, (float)i / steps)), 2);
            }
            void Pose(int leftX, int leftY, int rightX, int rightY, int leftFoot, int rightFoot, int rightFootY = 1)
            {
                Dot(24, 39, 5);
                Line(6, 8, 6, 4);
                Line(leftX, leftY, 6, 7);
                Line(6, 7, rightX, rightY);
                Line(leftFoot, 1, 6, 4);
                Line(6, 4, rightFoot, rightFootY);
            }

            switch (emote)
            {
                case "FriendlyWave": Pose(3, 4, 10, 9, 4, 8); Line(10, 10, 11, 11); break;
                case "TotallyMeantThat": Pose(3, 9, 9, 9, 4, 8); break;
                case "Phew": Pose(3, 4, 7, 10, 4, 8); Line(9, 9, 10, 8); break;
                case "WhatWasThat": Pose(2, 7, 10, 7, 4, 8); break;
                case "GolfClap": Pose(8, 7, 9, 6, 4, 8); Line(10, 8, 11, 9); break;
                case "JustChilling": Pose(4, 10, 8, 10, 3, 7); break;
                case "NopeNopeNope": Pose(2, 9, 10, 9, 3, 9); Line(1, 10, 2, 11); Line(10, 11, 11, 10); break;
                case "TinyVictoryDance": Pose(3, 9, 10, 8, 4, 10, 3); break;
                case "IrishDance": Pose(5, 4, 7, 4, 7, 3, 4); break;
                case "Breakdance":
                    Dot(35, 31, 5);
                    Line(8, 7, 5, 5); Line(5, 5, 7, 1); Line(8, 7, 11, 6);
                    Line(5, 5, 2, 4); Line(2, 4, 1, 2); Line(5, 5, 4, 8); Line(4, 8, 2, 8);
                    break;
                case "FingerWag":
                    Line(6, 2, 6, 10); Line(3, 9, 2, 8); Line(9, 9, 10, 8);
                    Line(4, 4, 6, 2); Line(6, 2, 8, 4); Line(8, 4, 8, 6);
                    break;
                case "ThumbsDown":
                    Line(4, 9, 9, 9); Line(9, 9, 10, 5); Line(10, 5, 6, 5);
                    Line(6, 5, 5, 2); Line(5, 2, 4, 2); Line(4, 2, 4, 9);
                    break;
                case "SlowClap": Pose(8, 6, 9, 5, 4, 8); Line(10, 6, 11, 6); break;
                case "PointAndLaugh": Pose(3, 4, 11, 8, 4, 9); Line(8, 10, 10, 11); break;
            }

            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, 48, 48), new Vector2(.5f, .5f));
        }
    }
}
