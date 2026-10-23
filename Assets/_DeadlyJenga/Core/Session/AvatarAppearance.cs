using System;

namespace DeadlyJenga.Core
{
    [Flags]
    public enum AvatarAccessory : uint
    {
        None=0, Mohawk=1, Glasses=2, RabbitEars=4, SkeletonMask=8,
        TopHat=16, Cape=32, AntAntennae=64, UnicornHorn=128,
        DevilHorns=256, Broccoli=512, LongNose=1024, StrongJaw=2048,
        PantherHead=4096, PantherClaws=8192, PantherTail=16384, PumpkinHelmet=32768
    }

    public enum AvatarSkin : byte { Default, Snowman, Penguin, Frankenstein, JackOLantern, Witch, Vampire }
    public enum AvatarHair : byte { None, Buzz, Bob, Curls, Afro, Ponytail, TwinTails, Spikes, SidePart, Bun, Braids, LongWaves, CrewCut }
    public enum AvatarEyewear : byte { None, Round, Square, Aviator, CatEye, Visor, Monocle, Star }
    [Flags] public enum AvatarClothing : uint { None=0, MonkRobe=1, Diaper=2, FootballPads=4, HockeyHelmet=8, FootballHelmet=16, GrassSkirt=32, Tuxedo=64, Hoodie=128, BowTie=256 }
    public interface IAvatarSurfacePalette { }

    [Serializable]
    public struct AvatarAppearance
    {
        public AvatarAccessory accessories;
        public AvatarSkin skin;
        public AvatarHair hair;
        public AvatarEyewear eyewear;
        public AvatarClothing clothing;
        public bool poweredArmor;
    }

    // Optional cosmetic posing while inspecting the existing avatar.
    public interface IAvatarShowcase
    {
        bool Showcasing { get; set; }
    }

    public interface IAvatarAppearance
    {
        AvatarAppearance Appearance { get; }
        void ApplyAppearance(AvatarAppearance appearance);
    }
}
