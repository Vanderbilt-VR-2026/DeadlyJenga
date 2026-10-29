using System;

namespace DeadlyJenga.Core
{
    [Flags]
    public enum AvatarAccessory : uint
    {
        None=0, Mohawk=1, Glasses=2, RabbitEars=4, SkeletonMask=8,
        TopHat=16, Cape=32, AntAntennae=64, UnicornHorn=128,
        DevilHorns=256, Broccoli=512, LongNose=1024, StrongJaw=2048,
        PantherHead=4096, PantherClaws=8192, PantherTail=16384, PumpkinHelmet=32768, Crown=65536, Halo=131072, CatEars=262144, Headphones=524288,
        CowboyHat=1048576, PirateHat=2097152, Beanie=4194304, ChefHat=8388608,
        VikingHelmet=16777216, PartyHat=33554432, PropellerCap=67108864, FlowerCrown=134217728,
        RobotAntenna=268435456, ButterflyWings=536870912, AngelWings=1073741824, DragonWings=2147483648
    }

    public enum AvatarSkin : byte { Default, Snowman, Penguin, Frankenstein, JackOLantern, Witch, Vampire, Fox, Koala, Panda, GoldenRetriever, Husky, CyclopsMonster, Leopard, Elephant, HoneyBear, Werewolf, HeadlessHorseman, Ghost, ScaryClown, FriendlyClown, Santa, Elf, MrsClaus, Reindeer, Gingerbread, CandyCane, CandyCorn, Horse, Bulldog, Inmate, Scarecrow, RobinHood, Gorilla, Monkey, Gargoyle, Mummy, Rabbit, Squirrel, Fish, Clownfish, Dragon, Cat, RagDoll, Sailor, TRex, Snake, Alligator, Zebra, Raccoon, Otter, Owl, Wolf, Lion, Parrot, Turtle, Axolotl, Walrus, Banana, Pineapple, Astronaut, CyberSamurai, DeepSeaDiver, CrystalKnight, NeonRobot, StormWizard, CandyKnight, SpacePirate, MushroomSprite, DiscoAlien }
    public enum AvatarHair : byte { None, Buzz, Bob, Curls, Afro, Ponytail, TwinTails, Spikes, SidePart, Bun, Braids, LongWaves, CrewCut, Dreadlocks, Pompadour, LibertySpikes }
    public enum AvatarEyewear : byte { None, Round, Square, Aviator, CatEye, Visor, Monocle, Star, Heart, SkiGoggles, VRVisor, CyberEye, Shutter, SafetyGoggles, RoundSunglasses, HalfMoon }
    [Flags] public enum AvatarClothing : uint { None=0, MonkRobe=1, Diaper=2, FootballPads=4, HockeyHelmet=8, FootballHelmet=16, GrassSkirt=32, Tuxedo=64, Hoodie=128, BowTie=256, BomberJacket=512, SailorShirt=1024, Overalls=2048, PufferVest=4096, RoyalMantle=8192 }
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
