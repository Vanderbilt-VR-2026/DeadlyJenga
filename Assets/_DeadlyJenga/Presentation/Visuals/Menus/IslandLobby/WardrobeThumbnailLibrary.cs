using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Pictures of every wardrobe item, rendered by the DevTools "Wardrobe Thumbnails" rig.
    // Layout (inside any Resources folder):
    //   WardrobeThumbs/manifest.json                 body colours the pictures were rendered in
    //   WardrobeThumbs/<Slot>/<Item>_c<colour>.png   one per item per body colour
    //   WardrobeThumbs/Skin/<Skin>.png               skins: one picture each, default body
    // The rig and the wardrobe both use these helpers, so their names always match.
    public static class WardrobeThumbnailLibrary
    {
        public static readonly string[] Slots = { "Skin", "Hair", "Headgear", "Eyewear", "Outfit", "Back", "Extras" };
        public const string Folder = "WardrobeThumbs";

        [Serializable] public sealed class Manifest { public Color[] colors = Array.Empty<Color>(); public int size; public string backdrop = ""; }

        static Manifest manifest;
        static bool manifestLoaded;
        static readonly Dictionary<string, Texture2D> cache = new();

        // The catalog entry's own enum name, e.g. "CowboyHat", "Bob", "Astronaut".
        public static string ItemKey(int slot, AvatarAppearance entry)
        {
            if (entry.accessories != 0) return entry.accessories.ToString();
            if (entry.clothing != 0) return entry.clothing.ToString();
            if (slot == 0) return entry.skin.ToString();
            if (slot == 1) return entry.hair.ToString();
            return entry.eyewear.ToString();
        }

        // Path relative to Resources, without extension.
        public static string ResourcePath(int slot, AvatarAppearance entry, int colorIndex) =>
            Folder + "/" + Slots[slot] + "/" + ItemKey(slot, entry) + (slot == 0 ? "" : "_c" + colorIndex);

        public static Manifest Current
        {
            get
            {
                if (manifestLoaded) return manifest;
                manifestLoaded = true;
                var asset = Resources.Load<TextAsset>(Folder + "/manifest");
                manifest = asset ? JsonUtility.FromJson<Manifest>(asset.text) : null;
                return manifest;
            }
        }

        public static int NearestColor(Color body)
        {
            var colors = Current?.colors;
            if (colors == null || colors.Length == 0) return 0;
            int best = 0; float bestDistance = float.MaxValue;
            for (int i = 0; i < colors.Length; i++)
            {
                float d = ((Vector4)colors[i] - (Vector4)body).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        // Null when that picture has not been rendered yet.
        public static Texture2D Load(int slot, AvatarAppearance entry, Color body)
        {
            string path = ResourcePath(slot, entry, NearestColor(body));
            if (!cache.TryGetValue(path, out var texture))
            {
                texture = Resources.Load<Texture2D>(path);
                cache[path] = texture;
            }
            return texture;
        }

        // Call after re-rendering pictures while the game is running.
        public static void ClearCache() { cache.Clear(); manifestLoaded = false; manifest = null; }
    }
}
