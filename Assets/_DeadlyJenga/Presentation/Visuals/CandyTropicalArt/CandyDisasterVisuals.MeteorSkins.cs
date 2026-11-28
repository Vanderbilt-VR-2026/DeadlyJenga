using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    // Look-only icy skin for meteors and the Planet Killer (snowy maps): pale glassy ice with frost and
    // cyan glowing cracks, a white-blue vapour trail instead of fire, frost instead of smoke, ice chips.
    public sealed partial class CandyDisasterVisuals
    {
        public enum MeteorSkin { Rock, Ice }
        public static MeteorSkin? MeteorSkinOverride;   // DevTools captures preview a skin on any map

        Material iceStone, iceChip, iceTrail, frostMist;

        public static MeteorSkin MeteorSkinForMap(string mapId)
        {
            string id = (mapId ?? "").ToLowerInvariant();
            return id.Contains("christmas") || id.Contains("snow") || id.Contains("frost") || id.Contains("ice") ? MeteorSkin.Ice : MeteorSkin.Rock;
        }

        bool IcyMeteors() => (MeteorSkinOverride ?? MeteorSkinForMap(MatchChoices.MapId)) == MeteorSkin.Ice;

        void BuildMeteorSkins()
        {
            if (iceStone) return;
            iceStone = ObjectMaterial("CandyMeteorStone", new Color(.62f, .8f, .95f, 1));   // pale glacier blue so frost blends, not spots
            iceStone.SetColor("_GlowColor", new Color(.25f, 1.3f, 2.4f, 1)); iceStone.SetFloat("_Frost", 1);
            iceChip = ObjectMaterial("CandyMeteorStone", new Color(.6f, .78f, .95f, 1));
            iceChip.SetColor("_GlowColor", new Color(.3f, 1.2f, 2f, 1)); iceChip.SetFloat("_Frost", 1); iceChip.SetFloat("_Heat", .4f);
            iceTrail = ObjectMaterial("CandyDisasterSmoke", new Color(.72f, .9f, 1f, .78f));
            frostMist = ObjectMaterial("CandyDisasterSmoke", new Color(.88f, .95f, 1f, .42f));
        }

        // Swap the meteor materials at draw time; the lists and motion are shared with the rock look.
        void MeteorMaterials(out Material rockMaterial, out Material chips, out Material trail, out Material trailSmoke)
        {
            if (IcyMeteors())
            {
                BuildMeteorSkins();
                rockMaterial = iceStone; chips = iceChip; trail = iceTrail; trailSmoke = frostMist;
                return;
            }
            rockMaterial = stone; chips = chipMaterial; trail = flame; trailSmoke = smoke;
        }

        void DestroyMeteorSkins()
        {
            foreach (var m in new[] { iceStone, iceChip, iceTrail, frostMist }) if (m) Destroy(m);
        }
    }
}
