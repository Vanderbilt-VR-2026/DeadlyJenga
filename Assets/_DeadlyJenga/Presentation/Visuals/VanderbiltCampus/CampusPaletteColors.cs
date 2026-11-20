using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    // The campus colors, one per material ID, in the same order as
    // Tools/VanderbiltCampus/config/material_ids.json (array position = vertex color index).
    // Copied by hand because the game must not read Tools/ at runtime.
    // If the JSON changes, update these lists and run Tools > Deadly Jenga > Maps > Build Campus Visual
    // to regenerate the palette PNGs.
    public static class CampusPaletteColors
    {
        public const int Width = 32;                 // palette strip width (room to append new IDs)
        static readonly Color32 Gray = Hex(0x9A9A9A); // gray_fallback, used for unused slots

        public static readonly Color32[] Candy =
        {
            Hex(0xD9534F), //  0 brick_red
            Hex(0xF2C38B), //  1 brick_buff
            Hex(0xFFF1D6), //  2 limestone
            Hex(0xC8B8A6), //  3 stone_rough
            Hex(0xE3DDE8), //  4 concrete
            Hex(0xFFE3EC), //  5 stucco
            Hex(0x9FC9E8), //  6 metal_panel
            Hex(0x7FD8F0), //  7 glass
            Hex(0xFFFFFF), //  8 window_frame
            Hex(0xB5651D), //  9 door_wood
            Hex(0x6C7FD8), // 10 door_metal
            Hex(0x6B5B95), // 11 roof_slate
            Hex(0x5FD3B3), // 12 roof_copper
            Hex(0xBFC7D5), // 13 roof_flat
            Hex(0xF28C5B), // 14 roof_tile
            Hex(0x3D3D5C), // 15 metal_trim
            Hex(0xE8907A), // 16 paving_brick
            Hex(0xF5EBDD), // 17 paving_concrete
            Hex(0x5A5470), // 18 asphalt
            Hex(0xFFFFFF), // 19 curb
            Hex(0x7ED957), // 20 grass
            Hex(0x3FAF6C), // 21 planting
            Hex(0xA0522D), // 22 bark
            Hex(0x4CC46A), // 23 foliage
            Hex(0x4FC3F7), // 24 water
            Hex(0xE0A060), // 25 wood
            Hex(0xFFFFFF), // 26 signage_blank
        };

        public static readonly Color32[] Plain =
        {
            Hex(0x8E3B2E), //  0 brick_red
            Hex(0xC9A77C), //  1 brick_buff
            Hex(0xD8D0BE), //  2 limestone
            Hex(0x8F8577), //  3 stone_rough
            Hex(0xA7A5A0), //  4 concrete
            Hex(0xE6DCCB), //  5 stucco
            Hex(0x8C9196), //  6 metal_panel
            Hex(0x3E5A66), //  7 glass
            Hex(0xE8E6E0), //  8 window_frame
            Hex(0x5C3A21), //  9 door_wood
            Hex(0x4A4F55), // 10 door_metal
            Hex(0x4B4F56), // 11 roof_slate
            Hex(0x6FA58E), // 12 roof_copper
            Hex(0x8A8C8E), // 13 roof_flat
            Hex(0xA8553A), // 14 roof_tile
            Hex(0x2E3236), // 15 metal_trim
            Hex(0x9C5A48), // 16 paving_brick
            Hex(0xBDB8AE), // 17 paving_concrete
            Hex(0x3F4144), // 18 asphalt
            Hex(0xB9B6AF), // 19 curb
            Hex(0x5E8C3A), // 20 grass
            Hex(0x3E6B32), // 21 planting
            Hex(0x5A4632), // 22 bark
            Hex(0x3F6E34), // 23 foliage
            Hex(0x4D7F94), // 24 water
            Hex(0x8A6A48), // 25 wood
            Hex(0xF0F0F0), // 26 signage_blank
        };

        // Alpha = how shiny the ID is (multiplies the shader's gloss).
        public static byte GlossFor(int index)
        {
            switch (index)
            {
                case 7: case 24: return 255;          // glass, water
                case 6: case 8: case 12: return 153;  // metal_panel, window_frame, roof_copper
                default: return 64;                   // everything else (0.25)
            }
        }

        // A 32x1 strip: pixel i = color of ID i. Point filter, clamp, no mipmaps, sRGB.
        public static Texture2D CreateTexture(bool plain, bool keepReadable = false)
        {
            var colors = plain ? Plain : Candy;
            var pixels = new Color32[Width];
            for (int i = 0; i < Width; i++)
            {
                Color32 c = i < colors.Length ? colors[i] : Gray;
                c.a = GlossFor(i);
                pixels[i] = c;
            }
            var texture = new Texture2D(Width, 1, TextureFormat.RGBA32, false, false)
            {
                name = plain ? "Campus palette (plain)" : "Campus palette (candy)",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, !keepReadable);
            return texture;
        }

        static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
