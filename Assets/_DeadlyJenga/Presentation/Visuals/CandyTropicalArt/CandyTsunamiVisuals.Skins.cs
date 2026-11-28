using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    // Look-only skins for the tsunami: same shape, physics and splash system, different material and spray.
    // Picked from the map (desert -> sand, snowy -> snow avalanche, volcanic -> lava); water everywhere else.
    public sealed partial class CandyTsunamiVisuals
    {
        public enum WaveSkin { Water, Sand, Snow, Lava }

        // DevTools captures set this to preview a skin on any map. Null = choose from the map.
        public static WaveSkin? SkinOverride;

        struct SkinLook
        {
            public Color shallow, deep, foam, reflection, horizon, spray, mist;
            public float shine, glints, foamStrength, opaque, lava, sprayGravity, mistGravity, mistLife;
        }

        public static WaveSkin SkinForMap(string mapId)
        {
            string id = (mapId ?? "").ToLowerInvariant();
            if (id.Contains("sphinx") || id.Contains("desert") || id.Contains("dune")) return WaveSkin.Sand;
            if (id.Contains("christmas") || id.Contains("snow") || id.Contains("frost") || id.Contains("ice")) return WaveSkin.Snow;
            if (id.Contains("caldera") || id.Contains("volcan") || id.Contains("lava")) return WaveSkin.Lava;
            return WaveSkin.Water;
        }

        static SkinLook Look(WaveSkin skin)
        {
            switch (skin)
            {
                case WaveSkin.Sand: return new SkinLook
                {
                    shallow = new Color(.80f, .62f, .38f), deep = new Color(.52f, .36f, .20f), foam = new Color(.93f, .82f, .62f),
                    reflection = new Color(.85f, .72f, .52f), horizon = new Color(.92f, .80f, .60f),
                    spray = new Color(.80f, .66f, .46f, .55f), mist = new Color(.86f, .74f, .55f, .62f),
                    shine = .12f, glints = 0, foamStrength = .5f, opaque = 1, lava = 0, sprayGravity = .3f, mistGravity = .12f, mistLife = 3.2f
                };
                case WaveSkin.Snow: return new SkinLook
                {
                    shallow = new Color(.93f, .96f, 1f), deep = new Color(.70f, .80f, .92f), foam = new Color(1f, 1f, 1f),
                    reflection = new Color(.82f, .90f, 1f), horizon = new Color(.88f, .93f, 1f),
                    spray = new Color(.96f, .98f, 1f, .6f), mist = new Color(.97f, .99f, 1f, .7f),
                    shine = .22f, glints = .5f, foamStrength = .7f, opaque = 1, lava = 0, sprayGravity = .35f, mistGravity = .15f, mistLife = 3f
                };
                case WaveSkin.Lava: return new SkinLook
                {
                    shallow = new Color(.13f, .05f, .03f), deep = new Color(.05f, .02f, .02f), foam = new Color(2.6f, .9f, .18f),
                    reflection = new Color(.35f, .12f, .05f), horizon = new Color(.45f, .18f, .08f),
                    spray = new Color(1f, .55f, .1f, .9f), mist = new Color(.16f, .13f, .12f, .65f),
                    shine = .35f, glints = 0, foamStrength = .35f, opaque = 1, lava = 1, sprayGravity = .8f, mistGravity = -.08f, mistLife = 3.4f
                };
                default: return new SkinLook
                {
                    shallow = new Color(.035f, .82f, .78f), deep = new Color(.015f, .32f, .65f), foam = new Color(.87f, 1f, .97f),
                    reflection = new Color(.32f, .68f, .95f), horizon = new Color(.48f, .86f, 1f),
                    spray = new Color(.87f, 1f, .97f, .55f), mist = new Color(.95f, 1f, 1f, .7f),
                    shine = .85f, glints = .3f, foamStrength = .2f, opaque = 0, lava = 0, sprayGravity = .55f, mistGravity = .9f, mistLife = 2f
                };
            }
        }

        WaveSkin appliedSkin = (WaveSkin)(-1);

        // Called every frame AFTER the material copies the ocean's properties (which would wipe the skin).
        // Water keeps the ocean's own colours; other skins override them. Particles change only on a skin switch.
        void ApplySkin()
        {
            if (!material) return;
            var skin = SkinOverride ?? SkinForMap(MatchChoices.MapId);
            var look = Look(skin);
            if (skin != WaveSkin.Water)
            {
                material.SetColor("_Shallow", look.shallow); material.SetColor("_Deep", look.deep); material.SetColor("_Foam", look.foam);
                material.SetColor("_Reflection", look.reflection); material.SetColor("_Horizon", look.horizon);
                material.SetFloat("_Shine", look.shine); material.SetFloat("_Glints", look.glints); material.SetFloat("_FoamStrength", look.foamStrength);
            }
            material.SetFloat("_Opaque", look.opaque); material.SetFloat("_Lava", look.lava);
            if (skin == appliedSkin) return;
            appliedSkin = skin;
            if (spray) { var main = spray.main; main.startColor = look.spray; main.gravityModifier = look.sprayGravity; }
            if (spindrift) { var main = spindrift.main; main.startColor = look.mist; main.gravityModifier = look.mistGravity; main.startLifetime = look.mistLife; }
        }    }
}
