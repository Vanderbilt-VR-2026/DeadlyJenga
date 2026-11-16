using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    internal static class TerminalUiStyle
    {
        static Sprite pill;
        internal static void Apply(Transform root)
        {
            if (!pill)
            {
                const int size = 128;
                const float radius = 40;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { name = "Terminal smooth corners", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    var q = new Vector2(Mathf.Abs(x + .5f - 64), Mathf.Abs(y + .5f - 64)) - Vector2.one * 24;
                    float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - d));
                }
                texture.SetPixels32(pixels); texture.Apply(false, true);
                pill = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f,.5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(40,40,40,40));
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.sprite == DeadlyJenga.Presentation.CandyHudVisuals.Pill)
                {
                    image.sprite = pill; image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier *= 4;
                }
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                text.fontStyle = FontStyle.Normal;
                foreach (var shadow in text.GetComponents<Shadow>()) shadow.enabled = false;
            }
        }
    }
}
