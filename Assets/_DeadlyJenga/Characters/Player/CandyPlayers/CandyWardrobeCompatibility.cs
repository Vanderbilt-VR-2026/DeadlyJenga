using System;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyPlayers
{
    // Resolve visible layers without changing the saved outfit.
    internal static class CandyWardrobeCompatibility
    {
        static readonly AvatarClothing[] OuterPriority={AvatarClothing.PufferVest,AvatarClothing.SailorShirt,AvatarClothing.BomberJacket,AvatarClothing.Hoodie,AvatarClothing.Tuxedo,AvatarClothing.MonkRobe};
        const AvatarAccessory ClosedHeadAccessories=AvatarAccessory.PumpkinHelmet|AvatarAccessory.SkeletonMask|AvatarAccessory.PantherHead;
        const AvatarAccessory FaceAccessories=AvatarAccessory.Glasses|AvatarAccessory.LongNose|AvatarAccessory.StrongJaw;
        const AvatarClothing BodyOutfit=AvatarClothing.MonkRobe|AvatarClothing.Diaper|AvatarClothing.FootballPads|
            AvatarClothing.GrassSkirt|AvatarClothing.Tuxedo|AvatarClothing.Hoodie|AvatarClothing.BowTie|
            AvatarClothing.BomberJacket|AvatarClothing.SailorShirt|AvatarClothing.Overalls|AvatarClothing.PufferVest;
        static readonly AvatarAccessory[] HeadCoverings={
            AvatarAccessory.PantherHead,AvatarAccessory.PumpkinHelmet,AvatarAccessory.SkeletonMask,
            AvatarAccessory.TopHat,AvatarAccessory.Beanie,AvatarAccessory.CowboyHat,AvatarAccessory.PirateHat,
            AvatarAccessory.ChefHat,AvatarAccessory.VikingHelmet,AvatarAccessory.PartyHat,AvatarAccessory.PropellerCap
        };
        [Flags] internal enum Tags { None=0, IntrinsicHeadCovering=1, IntrinsicSpikes=2, ClosedHead=4, BuiltInOutfit=8 }
        [Flags] internal enum Reason { None=0, CompetingHeadCovering=1, CoveredCrownHair=2, DuplicateIntrinsicHair=4, CompetingOuterGarment=8, BuiltInOutfit=16, ClosedFace=32, BuiltInBack=64 }

        internal struct Resolution
        {
            internal AvatarAppearance effective;
            internal AvatarAccessory hiddenAccessories;
            internal AvatarClothing hiddenClothing;
            internal bool hiddenHair;
            internal Reason reasons;
        }

        internal static Tags SkinTags(AvatarSkin skin)
        {
            switch(skin)
            {
                case AvatarSkin.Snowman: case AvatarSkin.Witch: case AvatarSkin.MushroomSprite: case AvatarSkin.Pineapple:
                    return Tags.IntrinsicHeadCovering;
                case AvatarSkin.Santa: case AvatarSkin.MrsClaus: case AvatarSkin.Elf: case AvatarSkin.StormWizard:
                case AvatarSkin.SpacePirate: case AvatarSkin.CandyCorn:
                    return Tags.IntrinsicHeadCovering|Tags.BuiltInOutfit;
                case AvatarSkin.JackOLantern:
                    return Tags.IntrinsicHeadCovering|Tags.ClosedHead;
                case AvatarSkin.HeadlessHorseman: case AvatarSkin.Astronaut: case AvatarSkin.CyberSamurai:
                case AvatarSkin.DeepSeaDiver: case AvatarSkin.CrystalKnight: case AvatarSkin.NeonRobot: case AvatarSkin.CandyKnight:
                    return Tags.IntrinsicHeadCovering|Tags.ClosedHead|Tags.BuiltInOutfit;
                case AvatarSkin.Ghost:
                    return Tags.BuiltInOutfit;
                case AvatarSkin.Frankenstein: case AvatarSkin.CyclopsMonster:return Tags.IntrinsicSpikes;
                default:return Tags.None;
            }
        }

        internal static Resolution Resolve(AvatarAppearance desired)
        {
            var result=new Resolution{effective=desired};
            var tags=desired.poweredArmor
                ?Tags.IntrinsicHeadCovering|Tags.ClosedHead|Tags.BuiltInOutfit
                :SkinTags(desired.skin);
            bool occupied=(tags&Tags.IntrinsicHeadCovering)!=0;
            // Fixed precedence for legacy saves, which contain no selection chronology.
            CoverClothing(ref result,AvatarClothing.FootballHelmet,ref occupied);
            CoverClothing(ref result,AvatarClothing.HockeyHelmet,ref occupied);
            foreach(var covering in HeadCoverings)CoverAccessory(ref result,covering,ref occupied);

            bool closedHead=(tags&Tags.ClosedHead)!=0||(result.effective.accessories&ClosedHeadAccessories)!=0;
            if(closedHead)
            {
                HideAccessories(ref result,FaceAccessories|AvatarAccessory.Mohawk,Reason.ClosedFace);
                if(desired.eyewear!=AvatarEyewear.None)
                {
                    result.effective.eyewear=AvatarEyewear.None;
                    result.reasons|=Reason.ClosedFace;
                }
            }
            if(desired.hair!=AvatarHair.None&&(closedHead||HairOverlapsCovering(result.effective,tags,desired.hair)))
            {
                result.effective.hair=AvatarHair.None;result.hiddenHair=true;result.reasons|=Reason.CoveredCrownHair;
            }
            else if((tags&Tags.IntrinsicSpikes)!=0&&desired.hair==AvatarHair.Spikes)
            {
                result.effective.hair=AvatarHair.None;result.hiddenHair=true;result.reasons|=Reason.DuplicateIntrinsicHair;
            }
            if((tags&Tags.BuiltInOutfit)!=0)
            {
                var covered=result.effective.clothing&BodyOutfit;
                if(covered!=0)
                {
                    result.effective.clothing&=~covered;
                    result.hiddenClothing|=covered;
                    result.reasons|=Reason.BuiltInOutfit;
                }
            }
            if((desired.skin==AvatarSkin.Dragon||desired.skin==AvatarSkin.Gargoyle)&&!desired.poweredArmor)
                HideAccessories(ref result,AvatarAccessory.DragonWings,Reason.BuiltInBack);

            AvatarClothing outer=0;
            foreach(var item in OuterPriority)
            {
                if((result.effective.clothing&item)==0)continue;
                if(outer==0)outer=item;
                else {result.effective.clothing&=~item;result.hiddenClothing|=item;result.reasons|=Reason.CompetingOuterGarment;}
            }
            return result;
        }
        internal static AvatarWardrobeResolution Evaluate(AvatarAppearance desired)
        {
            var result=Resolve(desired);
            string reason="";
            if((result.reasons&Reason.BuiltInOutfit)!=0)reason=desired.poweredArmor?"Powered armor covers outfit pieces. ":"This skin has a built-in outfit. ";
            else if((result.reasons&Reason.ClosedFace)!=0)reason="The head shell covers face pieces. ";
            else if((result.reasons&Reason.CompetingHeadCovering)!=0)reason="Head coverings share the same space. ";
            else if((result.reasons&Reason.CompetingOuterGarment)!=0)reason="Outer garments share the same space. ";
            else if((result.reasons&Reason.BuiltInBack)!=0)reason="This skin already has dragon wings. ";
            else if(result.hiddenHair)reason=(result.reasons&Reason.DuplicateIntrinsicHair)!=0
                ?"This skin already has spikes. ":"The head covering overlaps this hair. ";
            if(reason.Length>0)reason+="Hidden items stay saved.";

            return new AvatarWardrobeResolution{effective=result.effective,hiddenAccessories=result.hiddenAccessories,hiddenClothing=result.hiddenClothing,hiddenHair=result.hiddenHair,reason=reason};
        }
        static bool HairOverlapsCovering(AvatarAppearance effective,Tags skinTags,AvatarHair hair)
        {
            bool authoredCover=(skinTags&Tags.IntrinsicHeadCovering)!=0||
                (effective.clothing&(AvatarClothing.HockeyHelmet|AvatarClothing.FootballHelmet))!=0;
            foreach(var covering in HeadCoverings)authoredCover|=(effective.accessories&covering)!=0;
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
        static void HideAccessories(ref Resolution result,AvatarAccessory mask,Reason reason)
        {
            var hidden=result.effective.accessories&mask;
            if(hidden==0)return;
            result.effective.accessories&=~hidden;
            result.hiddenAccessories|=hidden;
            result.reasons|=reason;
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
