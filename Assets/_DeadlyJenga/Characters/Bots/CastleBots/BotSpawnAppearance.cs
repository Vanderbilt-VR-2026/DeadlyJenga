using System;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    public static class BotSpawnAppearance
    {
        [Serializable]
        sealed class Catalog
        {
            public AvatarAppearance[] skins, hair, headgear, eyewear, outfits, back, extras;
        }

        const AvatarAccessory EnemyParts = AvatarAccessory.PantherHead |
            AvatarAccessory.PantherClaws | AvatarAccessory.PantherTail;
        const AvatarAccessory ClosedHeads = AvatarAccessory.PumpkinHelmet | AvatarAccessory.SkeletonMask;
        const AvatarAccessory FaceExtras = AvatarAccessory.LongNose | AvatarAccessory.StrongJaw;
        const AvatarClothing SportsHelmets = AvatarClothing.HockeyHelmet | AvatarClothing.FootballHelmet;

        static Catalog catalog;
        static AvatarAppearance[] themedSkins, headgear;
        static readonly System.Random random = new();

        public static AvatarAppearance Pick()
        {
            if (catalog == null) LoadCatalog();
            if (themedSkins.Length > 0 && random.Next(3) == 0)
                return new AvatarAppearance { skin = PickItem(themedSkins).skin };

            var look = new AvatarAppearance();
            var head = random.Next(2) == 0 ? PickItem(headgear) : default;
            Add(ref look, head);
            if (head.accessories == 0 && head.clothing == 0 && random.Next(3) != 0)
                Add(ref look, PickItem(catalog.hair));

            bool coveredFace = (head.accessories & ClosedHeads) != 0 || (head.clothing & SportsHelmets) != 0;
            if (!coveredFace && random.Next(2) == 0) Add(ref look, PickItem(catalog.eyewear));
            if (random.Next(4) != 0) Add(ref look, PickItem(catalog.outfits));
            bool hasOutfit = (look.clothing & ~SportsHelmets) != 0;
            if (random.Next(5) == 0) Add(ref look, PickItem(catalog.back));
            if ((look.clothing & AvatarClothing.Hoodie) == 0 && random.Next(4) == 0)
            {
                var extra = PickItem(catalog.extras);
                bool hasEyewear = look.eyewear != AvatarEyewear.None ||
                    (look.accessories & AvatarAccessory.Glasses) != 0;
                bool overlapsFace = (coveredFace || hasEyewear) && (extra.accessories & FaceExtras) != 0;
                if (!overlapsFace && !(hasOutfit && extra.clothing == AvatarClothing.Hoodie))
                    Add(ref look, extra);
            }
            if (look.accessories == 0 && look.clothing == 0 && look.hair == 0 && look.eyewear == 0)
                Add(ref look, PickItem(catalog.outfits));
            return CandyWardrobeCompatibility.Resolve(CandyPlayerAvatar.SanitizeAppearance(look)).effective;
        }

        static void LoadCatalog()
        {
            var asset = Resources.Load<TextAsset>("WardrobeCatalog");
            catalog = (asset ? JsonUtility.FromJson<Catalog>(asset.text) : null) ?? new Catalog();
            themedSkins = (catalog.skins ?? Array.Empty<AvatarAppearance>())
                .Where(x => x.skin != AvatarSkin.Default && x.skin != AvatarSkin.MushroomSprite).ToArray();
            headgear = (catalog.headgear ?? Array.Empty<AvatarAppearance>())
                .Where(x => (x.accessories & EnemyParts) == 0).ToArray();
        }

        static AvatarAppearance PickItem(AvatarAppearance[] options) =>
            options != null && options.Length > 0 ? options[random.Next(options.Length)] : default;

        static void Add(ref AvatarAppearance look, AvatarAppearance item)
        {
            look.accessories |= item.accessories;
            look.clothing |= item.clothing;
            if (item.hair != AvatarHair.None) look.hair = item.hair;
            if (item.eyewear != AvatarEyewear.None) look.eyewear = item.eyewear;
        }
    }
}
