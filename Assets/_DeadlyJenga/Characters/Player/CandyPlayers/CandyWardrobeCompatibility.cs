using System;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyPlayers
{
    // Source-authored prototype; fit still needs captures. Desired preferences stay intact.
    internal static class CandyWardrobeCompatibility
    {
        static readonly AvatarClothing[] OuterPriority={AvatarClothing.PufferVest,AvatarClothing.SailorShirt,AvatarClothing.BomberJacket,AvatarClothing.Hoodie,AvatarClothing.Tuxedo,AvatarClothing.MonkRobe};
        internal enum Slot { Unreviewed, HeadCovering }
        [Flags] internal enum Tags { None=0, CrownVolume=1, IntrinsicHeadCovering=2, IntrinsicSpikes=4 }
        [Flags] internal enum Reason { None=0, CompetingHeadCovering=1, CoveredCrownHair=2, DuplicateIntrinsicHair=4, CompetingOuterGarment=8, ArmorPads=16 }

        internal struct Resolution
        {
            internal AvatarAppearance effective;
            internal AvatarAccessory hiddenAccessories;
            internal AvatarClothing hiddenClothing;
            internal bool hiddenHair;
            internal Reason reasons;
        }

        internal static Slot AccessorySlot(AvatarAccessory item)
        {
            return item==AvatarAccessory.TopHat||item==AvatarAccessory.Beanie?Slot.HeadCovering:Slot.Unreviewed;
        }
        internal static Slot ClothingSlot(AvatarClothing item)
        {
            return item==AvatarClothing.HockeyHelmet||item==AvatarClothing.FootballHelmet?Slot.HeadCovering:Slot.Unreviewed;
        }
        internal static Tags HairTags(AvatarHair hair)
        {
            switch(hair)
            {
                case AvatarHair.Afro: case AvatarHair.Pompadour: case AvatarHair.LibertySpikes:
                case AvatarHair.Spikes: case AvatarHair.Bun: return Tags.CrownVolume;
                default:return Tags.None;
            }
        }
        internal static Tags SkinTags(AvatarSkin skin)
        {
            switch(skin)
            {
                case AvatarSkin.Snowman: case AvatarSkin.Witch:return Tags.IntrinsicHeadCovering;
                case AvatarSkin.Frankenstein: case AvatarSkin.CyclopsMonster:return Tags.IntrinsicSpikes;
                default:return Tags.None;
            }
        }

        internal static Resolution Resolve(AvatarAppearance desired)
        {
            var result=new Resolution{effective=desired};
            var tags=SkinTags(desired.skin);
            bool occupied=(tags&Tags.IntrinsicHeadCovering)!=0;
            // Fixed precedence for legacy saves, which contain no selection chronology.
            CoverClothing(ref result,AvatarClothing.FootballHelmet,ref occupied);
            CoverClothing(ref result,AvatarClothing.HockeyHelmet,ref occupied);
            CoverAccessory(ref result,AvatarAccessory.TopHat,ref occupied);
            CoverAccessory(ref result,AvatarAccessory.Beanie,ref occupied);
            if(HairOverlapsCovering(result.effective,tags,desired.hair))
            {
                result.effective.hair=AvatarHair.None;result.hiddenHair=true;result.reasons|=Reason.CoveredCrownHair;
            }
            else if((tags&Tags.IntrinsicSpikes)!=0&&desired.hair==AvatarHair.Spikes)
            {
                result.effective.hair=AvatarHair.None;result.hiddenHair=true;result.reasons|=Reason.DuplicateIntrinsicHair;
            }
            AvatarClothing outer=0;
            foreach(var item in OuterPriority)
            {
                if((result.effective.clothing&item)==0)continue;
                if(outer==0)outer=item;
                else {result.effective.clothing&=~item;result.hiddenClothing|=item;result.reasons|=Reason.CompetingOuterGarment;}
            }
            if(desired.poweredArmor&&(result.effective.clothing&AvatarClothing.FootballPads)!=0)
            {result.effective.clothing&=~AvatarClothing.FootballPads;result.hiddenClothing|=AvatarClothing.FootballPads;result.reasons|=Reason.ArmorPads;}
            return result;
        }
        internal static AvatarWardrobeResolution Evaluate(AvatarAppearance desired)
        {
            var result=Resolve(desired);
            string reason="Reviewed head-covering rules only; other combinations need fit review.";
            if(result.hiddenAccessories!=0||result.hiddenClothing!=0)
                reason="Temporarily hidden layers: "+result.hiddenAccessories+" / "+result.hiddenClothing+". Head coverings share the crown; fitted outer garments use the same body surface. Remove a covering layer to restore preferred pieces.";
            if((result.reasons&Reason.ArmorPads)!=0)reason+=" Powered armor chest and shoulder shells occupy the football-pad envelope; pads restore after armor removal.";
            if(result.hiddenHair)reason+=" Hair "+desired.hair+" is covered by the selected crown or duplicates built-in spikes; the saved hair is retained.";
            return new AvatarWardrobeResolution{effective=result.effective,hiddenAccessories=result.hiddenAccessories,hiddenClothing=result.hiddenClothing,hiddenHair=result.hiddenHair,reason=reason};
        }
        static bool HairOverlapsCovering(AvatarAppearance effective,Tags skinTags,AvatarHair hair)
        {
            bool authoredCover=(skinTags&Tags.IntrinsicHeadCovering)!=0||
                (effective.accessories&(AvatarAccessory.TopHat|AvatarAccessory.Beanie))!=0||
                (effective.clothing&(AvatarClothing.HockeyHelmet|AvatarClothing.FootballHelmet))!=0;
            if(!authoredCover)return false;
            switch(hair)
            {
                case AvatarHair.Afro: case AvatarHair.Pompadour:
                case AvatarHair.LibertySpikes: case AvatarHair.Spikes:return true;
                // The bun can sit inside a tall hat; do not infer that pair from a tag.
                case AvatarHair.Bun:return (effective.accessories&AvatarAccessory.Beanie)!=0||
                    (effective.clothing&(AvatarClothing.HockeyHelmet|AvatarClothing.FootballHelmet))!=0;
                default:return false;
            }
        }
        static void CoverAccessory(ref Resolution result,AvatarAccessory item,ref bool occupied)
        {
            if((result.effective.accessories&item)==0)return;
            if(occupied){result.effective.accessories&=~item;result.hiddenAccessories|=item;result.reasons|=Reason.CompetingHeadCovering;}
            else occupied=true;
        }
        static void CoverClothing(ref Resolution result,AvatarClothing item,ref bool occupied)
        {
            if((result.effective.clothing&item)==0)return;
            if(occupied){result.effective.clothing&=~item;result.hiddenClothing|=item;result.reasons|=Reason.CompetingHeadCovering;}
            else occupied=true;
        }
    }
}
