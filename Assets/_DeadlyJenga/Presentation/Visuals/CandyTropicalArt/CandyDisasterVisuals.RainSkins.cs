using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    // Look-only skins for acid rain: lava rain (volcanic maps) and dry-ice rain (snowy maps).
    // The rain still does exactly what acid rain does; only the drops and their landings change.
    // Landings: a few raycasts per frame find where drops hit, and a Blender-rendered sprite plays there
    // (EmberBurst for lava, DryIceFog rolling low for dry ice).
    public sealed partial class CandyDisasterVisuals
    {
        public enum RainSkin { Acid, Lava, DryIce }
        public static RainSkin? RainSkinOverride;   // DevTools captures preview a skin on any map

        struct Landing { public Vector3 position; public float born, life, size; public int seed; }
        const int MaxLandings = 72;
        readonly List<Landing> landings = new List<Landing>(MaxLandings);
        readonly List<Matrix4x4> landingPuffs = new List<Matrix4x4>(MaxLandings);
        Material lavaRain, iceRain, emberSplash, iceFog, rainMaterial;
        RainSkin landingSkin;
        int landingSeed;

        public static RainSkin RainSkinForMap(string mapId)
        {
            string id = (mapId ?? "").ToLowerInvariant();
            if (id.Contains("caldera") || id.Contains("volcan") || id.Contains("lava")) return RainSkin.Lava;
            if (id.Contains("christmas") || id.Contains("snow") || id.Contains("frost") || id.Contains("ice")) return RainSkin.DryIce;
            return RainSkin.Acid;
        }

        RainSkin CurrentRainSkin() => RainSkinOverride ?? RainSkinForMap(MatchChoices.MapId);

        void BuildRainSkins()
        {
            if (lavaRain || !stormMist) return;
            lavaRain = ObjectMaterial("CandyDisasterSmoke", new Color(1f, .42f, .06f, .95f));
            iceRain = ObjectMaterial("CandyDisasterSmoke", new Color(.86f, .94f, 1f, .9f));
            emberSplash = new Material(stormMist) { name = "Lava rain splat" };
            iceFog = new Material(stormMist) { name = "Dry-ice ground fog" };
            var ember = Resources.Load<Texture2D>("CandyScenery/EmberBurst");
            var fog = Resources.Load<Texture2D>("CandyScenery/DryIceFog");
            if (ember) { emberSplash.SetTexture("_CloudTexture", ember); emberSplash.SetFloat("_UseCloudTexture", 1); }
            if (fog) { iceFog.SetTexture("_CloudTexture", fog); iceFog.SetFloat("_UseCloudTexture", 1); }
            emberSplash.SetColor("_BaseColor", new Color(1f, .6f, .25f, .95f));
            iceFog.SetColor("_BaseColor", new Color(.93f, .97f, 1f, .75f));
        }

        // Drop size per skin: lava falls as thick glowing streaks, dry ice as short frosty pellets.
        void RainDropShape(RainSkin skin, float random, out float width, out float length)
        {
            switch (skin)
            {
                case RainSkin.Lava: width = .2f + random * .1f; length = 1.2f + random * .9f; break;
                case RainSkin.DryIce: width = .16f + random * .08f; length = .45f + random * .35f; break;
                default: width = .14f + random * .075f; length = 1.55f + random * 1.15f; break;
            }
        }

        void RainLandings(DisasterFrame f, RainSkin skin)
        {
            if (skin == RainSkin.Acid) return;
            BuildRainSkins();
            if (skin != landingSkin) { landings.Clear(); landingSkin = skin; }
            int casts = Application.isMobilePlatform ? 3 : 6;
            int mask = (1 << Layers.Block) | (1 << Layers.Surface);
            for (int i = 0; i < casts && landings.Count < MaxLandings; i++)
            {
                int seed = landingSeed++;
                var top = f.center + new Vector3((Hash(seed) - .5f) * f.size.x, f.size.y * .5f + 20, (Hash(seed + 977) - .5f) * f.size.z);
                if (!Physics.Raycast(top, Vector3.down, out var hit, f.size.y + 60, mask, QueryTriggerInteraction.Ignore)) continue;
                bool lava = skin == RainSkin.Lava;
                landings.Add(new Landing
                {
                    position = hit.point, born = Time.time, seed = seed,
                    life = lava ? .55f + Hash(seed + 5) * .3f : 3f + Hash(seed + 5) * 1.5f,
                    size = lava ? .9f + Hash(seed + 7) * .8f : 2.2f + Hash(seed + 7) * 1.6f
                });
            }
        }

        void RenderRainLandings()
        {
            landingPuffs.Clear();
            for (int i = landings.Count - 1; i >= 0; i--)
            {
                var landing = landings[i];
                float t = (Time.time - landing.born) / landing.life;
                if (t >= 1) { landings.RemoveAt(i); continue; }
                if (landingSkin == RainSkin.Lava)
                {
                    // Burst open fast, then shrink away.
                    float s = landing.size * Mathf.Sin(Mathf.Min(1, t * 1.6f) * Mathf.PI * .5f) * (1 - t * t);
                    Puff(landingPuffs, landing.position + Vector3.up * s * .35f, s, s, landing.seed * 37);
                }
                else
                {
                    // Low fog that spreads, drifts a little and thins out by shrinking in height.
                    float grow = Mathf.SmoothStep(0, 1, Mathf.Min(1, t * 2.5f));
                    float width = landing.size * (.5f + grow * 1.1f), height = landing.size * .42f * (1 - t * .7f) * grow;
                    var drift = new Vector3(Mathf.Sin(landing.seed * 1.7f), 0, Mathf.Cos(landing.seed * 2.3f)) * t * 1.2f;
                    Puff(landingPuffs, landing.position + drift + Vector3.up * height * .32f, width, height, 0);
                }
            }
            if (landingPuffs.Count > 0) DrawObjects(landingPuffs, quad, landingSkin == RainSkin.Lava ? emberSplash : iceFog);
        }

        void DestroyRainSkins()
        {
            foreach (var m in new[] { lavaRain, iceRain, emberSplash, iceFog }) if (m) Destroy(m);
        }
    }
}
