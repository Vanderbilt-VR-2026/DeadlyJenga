using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    // Acid rain looks: acid (default), lava rain (volcanic maps) and dry-ice rain (snowy maps).
    // The rain still does exactly what acid rain does; only the drops and their landings change.
    // Landings: a few raycasts per frame find where drops hit, and Blender-rendered sprites play there:
    //  acid    - AcidSplash splat + an AcidFume wisp that rises and sways off the burn spot
    //  lava    - EmberBurst splat
    //  dry ice - DryIceFog that spreads low and drifts
    public sealed partial class CandyDisasterVisuals
    {
        public enum RainSkin { Acid, Lava, DryIce }
        public static RainSkin? RainSkinOverride;   // DevTools captures preview a skin on any map

        struct Landing { public Vector3 position; public float born, life, size; public int seed; public bool fume; }
        const int MaxLandings = 96;
        readonly List<Landing> landings = new List<Landing>(MaxLandings);
        readonly List<Matrix4x4> landingPuffs = new List<Matrix4x4>(MaxLandings), fumePuffs = new List<Matrix4x4>(MaxLandings);
        Material acidRain, lavaRain, iceRain, acidSplash, acidFume, emberSplash, iceFog, rainMaterial;
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

        Material Sprite(string name, string texture, Color color)
        {
            var m = new Material(stormMist) { name = name };
            var tex = Resources.Load<Texture2D>("CandyScenery/" + texture);
            if (tex) { m.SetTexture("_CloudTexture", tex); m.SetFloat("_UseCloudTexture", 1); }
            m.SetColor("_BaseColor", color); return m;
        }

        void BuildRainSkins()
        {
            if (lavaRain || !stormMist) return;
            acidRain = ObjectMaterial("CandyDisasterSmoke", new Color(.5f, 1f, .12f, .95f));    // brighter toxic green
            lavaRain = ObjectMaterial("CandyDisasterSmoke", new Color(1f, .42f, .06f, .95f));
            iceRain = ObjectMaterial("CandyDisasterSmoke", new Color(.86f, .94f, 1f, .9f));
            acidSplash = Sprite("Acid rain splat", "AcidSplash", new Color(.75f, 1f, .45f, .95f));
            acidFume = Sprite("Acid burn fume", "AcidFume", new Color(.78f, 1f, .62f, .55f));
            emberSplash = Sprite("Lava rain splat", "EmberBurst", new Color(1f, .6f, .25f, .95f));
            iceFog = Sprite("Dry-ice ground fog", "DryIceFog", new Color(.93f, .97f, 1f, .75f));
        }

        Material RainDropMaterial(RainSkin skin)
        {
            BuildRainSkins();
            return skin == RainSkin.Lava ? lavaRain : skin == RainSkin.DryIce ? iceRain : acidRain ? acidRain : acid;
        }

        // Drop size per skin: lava falls as thick glowing streaks, dry ice as short frosty pellets.
        void RainDropShape(RainSkin skin, float random, out float width, out float length)
        {
            switch (skin)
            {
                case RainSkin.Lava: width = .2f + random * .1f; length = 1.2f + random * .9f; break;
                case RainSkin.DryIce: width = .16f + random * .08f; length = .45f + random * .35f; break;
                default: width = .15f + random * .08f; length = 1.55f + random * 1.15f; break;
            }
        }

        void RainLandings(DisasterFrame f, RainSkin skin)
        {
            BuildRainSkins();
            if (skin != landingSkin) { landings.Clear(); landingSkin = skin; }
            int casts = Application.isMobilePlatform ? 3 : 6;
            int mask = (1 << Layers.Block) | (1 << Layers.Surface);
            for (int i = 0; i < casts && landings.Count < MaxLandings - 1; i++)
            {
                int seed = landingSeed++;
                var top = f.center + new Vector3((Hash(seed) - .5f) * f.size.x, f.size.y * .5f + 20, (Hash(seed + 977) - .5f) * f.size.z);
                if (!Physics.Raycast(top, Vector3.down, out var hit, f.size.y + 60, mask, QueryTriggerInteraction.Ignore)) continue;
                if (Services.World != null && hit.point.y <= Services.World.WaterHeight(hit.point) + .15f) continue;   // drops into the sea: no splat
                switch (skin)
                {
                    case RainSkin.Lava:
                        landings.Add(new Landing { position = hit.point, born = Time.time, seed = seed, life = .6f + Hash(seed + 5) * .3f, size = 1.4f + Hash(seed + 7) * 1.0f });
                        break;
                    case RainSkin.DryIce:
                        landings.Add(new Landing { position = hit.point, born = Time.time, seed = seed, life = 3f + Hash(seed + 5) * 1.5f, size = 2.2f + Hash(seed + 7) * 1.6f });
                        break;
                    default:
                        landings.Add(new Landing { position = hit.point, born = Time.time, seed = seed, life = .5f + Hash(seed + 5) * .3f, size = 1.3f + Hash(seed + 7) * .9f });
                        if (Hash(seed + 11) < .6f)   // most hits leave a fume curling up off the burn spot
                            landings.Add(new Landing { position = hit.point, born = Time.time + .15f, seed = seed + 1, life = 1.8f + Hash(seed + 13) * 1.2f, size = 1.5f + Hash(seed + 17) * 1.1f, fume = true });
                        break;
                }
            }
        }

        void RenderRainLandings()
        {
            landingPuffs.Clear(); fumePuffs.Clear();
            for (int i = landings.Count - 1; i >= 0; i--)
            {
                var landing = landings[i];
                float t = (Time.time - landing.born) / landing.life;
                if (t >= 1) { landings.RemoveAt(i); continue; }
                if (t < 0) continue;
                if (landing.fume)
                {
                    // Rises and widens, swaying, then thins away.
                    float rise = t * landing.size * 1.6f, h = landing.size * 1.6f * (1 - t * .3f), w = landing.size * .55f * (1 + t);
                    var sway = new Vector3(Mathf.Sin(landing.seed + t * 3f), 0, Mathf.Cos(landing.seed * 1.3f + t * 2.4f)) * t * .4f;
                    Puff(fumePuffs, landing.position + sway + Vector3.up * (h * .5f + rise * .4f), w * (1 - t * t * .6f), h, Mathf.Sin(landing.seed) * 10);
                }
                else if (landingSkin == RainSkin.DryIce)
                {
                    // Low fog that spreads, drifts a little and thins out by shrinking in height.
                    float grow = Mathf.SmoothStep(0, 1, Mathf.Min(1, t * 2.5f));
                    float width = landing.size * (.5f + grow * 1.1f), height = landing.size * .85f * (1 - t * .6f) * grow;   // a full low bank, not a flat streak
                    var drift = new Vector3(Mathf.Sin(landing.seed * 1.7f), 0, Mathf.Cos(landing.seed * 2.3f)) * t * 1.2f;
                    Puff(landingPuffs, landing.position + drift + Vector3.up * height * .32f, width, height, 0);
                }
                else
                {
                    // Splat: burst open fast, then shrink away.
                    float s = landing.size * Mathf.Sin(Mathf.Min(1, t * 1.6f) * Mathf.PI * .5f) * (1 - t * t);
                    Puff(landingPuffs, landing.position + Vector3.up * s * .35f, s, s, landing.seed * 37);
                }
            }
            var splash = landingSkin == RainSkin.Lava ? emberSplash : landingSkin == RainSkin.DryIce ? iceFog : acidSplash;
            if (landingPuffs.Count > 0 && splash) DrawObjects(landingPuffs, quad, splash);
            if (fumePuffs.Count > 0 && acidFume) DrawObjects(fumePuffs, quad, acidFume);
        }

        void DestroyRainSkins()
        {
            foreach (var m in new[] { acidRain, lavaRain, iceRain, acidSplash, acidFume, emberSplash, iceFog }) if (m) Destroy(m);
        }
    }
}
