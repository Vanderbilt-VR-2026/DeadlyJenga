using System;
using System.Collections.Generic;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    public static class BotSpawnAppearance
    {
        static readonly AvatarAppearance[] choices=Build();
        static readonly Random random=new Random();
        public static AvatarAppearance Pick()=>choices[random.Next(choices.Length)];
        static AvatarAppearance[] Build()
        {
            var values=new List<AvatarAppearance>();
            foreach(AvatarAccessory item in Enum.GetValues(typeof(AvatarAccessory)))
                if(Single((uint)item)&&item!=AvatarAccessory.PantherHead&&item!=AvatarAccessory.PantherClaws&&item!=AvatarAccessory.PantherTail)
                    values.Add(new AvatarAppearance{accessories=item});
            foreach(AvatarSkin item in Enum.GetValues(typeof(AvatarSkin)))if(item!=AvatarSkin.Default)values.Add(new AvatarAppearance{skin=item});
            foreach(AvatarHair item in Enum.GetValues(typeof(AvatarHair)))if(item!=AvatarHair.None)values.Add(new AvatarAppearance{hair=item});
            foreach(AvatarEyewear item in Enum.GetValues(typeof(AvatarEyewear)))if(item!=AvatarEyewear.None)values.Add(new AvatarAppearance{eyewear=item});
            foreach(AvatarClothing item in Enum.GetValues(typeof(AvatarClothing)))if(Single((uint)item))values.Add(new AvatarAppearance{clothing=item});
            return values.ToArray();
        }
        static bool Single(uint value)=>value!=0&&(value&(value-1))==0;
    }
}
