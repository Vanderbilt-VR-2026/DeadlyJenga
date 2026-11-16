using DeadlyJenga.Core;

namespace DeadlyJenga.CandyPlayers
{
    // Preferences survive an incompatible skin or a conflicting legacy outfit.
    internal static class CandyWardrobeCompatibility
    {
        const AvatarAccessory Headgear = AvatarAccessory.TopHat | AvatarAccessory.Crown | AvatarAccessory.Beanie |
            AvatarAccessory.CowboyHat | AvatarAccessory.PirateHat | AvatarAccessory.ChefHat |
            AvatarAccessory.VikingHelmet | AvatarAccessory.PartyHat | AvatarAccessory.PropellerCap |
            AvatarAccessory.FlowerCrown | AvatarAccessory.PumpkinHelmet | AvatarAccessory.SkeletonMask |
            AvatarAccessory.PantherHead | AvatarAccessory.RabbitEars | AvatarAccessory.CatEars |
            AvatarAccessory.AntAntennae | AvatarAccessory.UnicornHorn | AvatarAccessory.DevilHorns |
            AvatarAccessory.Broccoli | AvatarAccessory.Halo | AvatarAccessory.Headphones | AvatarAccessory.RobotAntenna;
        const AvatarAccessory Back = AvatarAccessory.Cape | AvatarAccessory.ButterflyWings |
            AvatarAccessory.AngelWings | AvatarAccessory.DragonWings;
        const AvatarAccessory Extras = AvatarAccessory.LongNose | AvatarAccessory.StrongJaw;
        const AvatarAccessory ClosedHeads = AvatarAccessory.PumpkinHelmet | AvatarAccessory.SkeletonMask |
            AvatarAccessory.PantherHead;
        const AvatarAccessory PantherParts = AvatarAccessory.PantherClaws | AvatarAccessory.PantherTail;
        const AvatarClothing Outfits = AvatarClothing.MonkRobe | AvatarClothing.Diaper | AvatarClothing.FootballPads |
            AvatarClothing.GrassSkirt | AvatarClothing.Tuxedo | AvatarClothing.BomberJacket |
            AvatarClothing.SailorShirt | AvatarClothing.Overalls | AvatarClothing.PufferVest;
        const AvatarClothing SportsHelmets = AvatarClothing.HockeyHelmet | AvatarClothing.FootballHelmet;
        const AvatarClothing ExtraClothing = AvatarClothing.Hoodie | AvatarClothing.BowTie;
        const AvatarClothing OuterLayers = AvatarClothing.MonkRobe | AvatarClothing.Tuxedo |
            AvatarClothing.BomberJacket | AvatarClothing.SailorShirt | AvatarClothing.PufferVest;
        const AvatarAccessory Hats = AvatarAccessory.TopHat | AvatarAccessory.Beanie | AvatarAccessory.CowboyHat |
            AvatarAccessory.PirateHat | AvatarAccessory.ChefHat | AvatarAccessory.VikingHelmet |
            AvatarAccessory.PartyHat | AvatarAccessory.PropellerCap;

        internal static AvatarWardrobeResolution Resolve(AvatarAppearance desired)
        {
            var effective = desired;
            string reason;
            if (desired.poweredArmor || desired.skin != AvatarSkin.Default)
            {
                effective.accessories = AvatarAccessory.None;
                effective.hair = AvatarHair.None;
                effective.eyewear = AvatarEyewear.None;
                effective.clothing = AvatarClothing.None;
                reason = desired.poweredArmor ? "Powered armor covers wardrobe items." :
                    "Switch to Default to equip wardrobe items.";
            }
            else
            {
                LimitCategories(ref effective);
                bool limited = DifferentItems(desired, effective);
                ApplyFit(ref effective);
                reason = limited ? "One item per wardrobe category." :
                    DifferentItems(desired, effective) ? "Some wardrobe items overlap." : "";
            }

            if (reason.Length > 0 && DifferentItems(desired, effective)) reason += " Hidden items stay saved.";
            return new AvatarWardrobeResolution
            {
                effective = effective,
                hiddenAccessories = desired.accessories & ~effective.accessories,
                hiddenClothing = desired.clothing & ~effective.clothing,
                hiddenHair = desired.hair != effective.hair,
                reason = reason
            };
        }

        static void LimitCategories(ref AvatarAppearance look)
        {
            // Older saves have no selection order. Sports helmets keep their
            // existing priority, and the other categories use stable item IDs.
            if ((look.clothing & SportsHelmets) != 0)
            {
                var helmet = (look.clothing & AvatarClothing.FootballHelmet) != 0
                    ? AvatarClothing.FootballHelmet : AvatarClothing.HockeyHelmet;
                look.clothing = (look.clothing & ~SportsHelmets) | helmet;
                look.accessories &= ~Headgear;
            }
            else
            {
                var candidates = look.accessories & ClosedHeads;
                if (candidates == 0) candidates = look.accessories & Hats;
                if (candidates == 0) candidates = look.accessories & Headgear;
                look.accessories = (look.accessories & ~Headgear) | (AvatarAccessory)FirstChoice((uint)candidates);
            }

            if (look.hair != AvatarHair.None) look.accessories &= ~AvatarAccessory.Mohawk;
            if (look.eyewear != AvatarEyewear.None) look.accessories &= ~AvatarAccessory.Glasses;

            look.accessories = (look.accessories & ~(Back | Extras)) |
                (AvatarAccessory)(FirstChoice((uint)(look.accessories & Back)) |
                    FirstChoice((uint)(look.accessories & Extras)));
            if ((look.accessories & Back) != 0) look.clothing &= ~AvatarClothing.RoyalMantle;
            if ((look.accessories & Extras) != 0) look.clothing &= ~ExtraClothing;
            look.clothing = (look.clothing & ~(Outfits | ExtraClothing)) |
                (AvatarClothing)(FirstChoice((uint)(look.clothing & Outfits)) |
                    FirstChoice((uint)(look.clothing & ExtraClothing)));

            // The enemy's paws and tail are components of the single Panther look.
            if ((look.accessories & AvatarAccessory.PantherHead) == 0) look.accessories &= ~PantherParts;
        }

        static void ApplyFit(ref AvatarAppearance look)
        {
            if ((look.accessories & ClosedHeads) != 0)
            {
                look.hair = AvatarHair.None;
                look.eyewear = AvatarEyewear.None;
                look.accessories &= ~(AvatarAccessory.Mohawk | AvatarAccessory.Glasses | Extras);
            }

            bool sportsHelmet = (look.clothing & SportsHelmets) != 0;
            if (sportsHelmet || (look.accessories & Hats) != 0)
            {
                if (look.hair == AvatarHair.Afro || look.hair == AvatarHair.Pompadour ||
                    look.hair == AvatarHair.LibertySpikes || look.hair == AvatarHair.Spikes ||
                    (look.hair == AvatarHair.Bun && (sportsHelmet || (look.accessories & AvatarAccessory.Beanie) != 0)))
                    look.hair = AvatarHair.None;
            }

            if ((look.clothing & OuterLayers) != 0) look.clothing &= ~AvatarClothing.Hoodie;
        }

        static bool DifferentItems(AvatarAppearance first, AvatarAppearance second) =>
            first.accessories != second.accessories || first.clothing != second.clothing ||
            first.hair != second.hair || first.eyewear != second.eyewear;

        static uint FirstChoice(uint choices)
        {
            // Isolate the lowest set bit, including the high-bit Dragon Wings ID.
            return choices & unchecked(0u - choices);
        }
    }
}
