using UnityEngine;

namespace DeadlyJenga.Core
{
    public sealed class EquipmentSpecification
    {
        public string name;
        public float damage,cooldown,range,radius,push,spread,charge,projectileSpeed,gravity,blastRadius;
        public int pellets=1,burst=1,magazine;
        public float reloadSeconds;
        public bool power,melee,automatic,twoHanded,backHolster,modeSwitch;
    }
    // Shared authored data, never damage, input or physics execution.
    public static class EquipmentSpecifications
    {
        public static readonly EquipmentSpecification[] All={
            new EquipmentSpecification{name="EMPTY"},new EquipmentSpecification{name="2x SPEED",power=true},new EquipmentSpecification{name="3x JUMP",power=true},
            new EquipmentSpecification{name="ALIEN BLASTER",damage=35,cooldown=.45f,range=28},
            new EquipmentSpecification{name="MURDERER SWORD",melee=true,damage=60,cooldown=.9f,range=1.4f},
            new EquipmentSpecification{name="SHERIFF REVOLVER",magazine=6,reloadSeconds=2,damage=85,cooldown=.75f,range=90,modeSwitch=true},
            new EquipmentSpecification{name="DUAL RIFLE / SHOTGUN",damage=55,cooldown=.65f,range=65,twoHanded=true,backHolster=true,modeSwitch=true},
            new EquipmentSpecification{name="GIANT BOXING GLOVE",melee=true,damage=80,cooldown=.9f,range=1.4f},
            new EquipmentSpecification{name="POWERED IRON SUIT",damage=35,cooldown=.45f,range=28},
            new EquipmentSpecification{name="ROCKET LAUNCHER",damage=160,cooldown=1.8f,magazine=5,reloadSeconds=3.5f,range=80,radius=.12f,push=32,projectileSpeed=14,blastRadius=8,twoHanded=true,backHolster=true},
            new EquipmentSpecification{name="DUAL ALIEN SPIKERS",projectileSpeed=45,magazine=60,reloadSeconds=2.2f,damage=9,cooldown=.065f,range=38,radius=.035f,push=1,spread=.025f,automatic=true},
            new EquipmentSpecification{name="CHAIN GUN",magazine=150,reloadSeconds=3.6f,damage=10,cooldown=.055f,range=55,radius=.035f,push=1,spread=.035f,charge=.65f,automatic=true,twoHanded=true,backHolster=true},
            new EquipmentSpecification{name="GRENADE LAUNCHER",magazine=6,reloadSeconds=2.8f,damage=65,cooldown=.8f,range=50,radius=.11f,push=10,projectileSpeed=22,gravity=13,blastRadius=3.5f,twoHanded=true,backHolster=true},
            new EquipmentSpecification{name="WRIST LASER",magazine=80,reloadSeconds=2.4f,damage=6,cooldown=.1f,range=35,radius=.055f,push=.3f,automatic=true},
            new EquipmentSpecification{name="NEEDLE DRILL",magazine=40,reloadSeconds=2,damage=11,cooldown=.075f,range=45,radius=.025f,push=.6f,charge=1,automatic=true},
            new EquipmentSpecification{name="STOCK PISTOL",magazine=24,reloadSeconds=1.6f,spread=.018f,damage=13,cooldown=.1f,range=55,radius=.035f,push=1,automatic=true,modeSwitch=true,backHolster=true,burst=3},
            new EquipmentSpecification{name="SAW WHEEL STAFF",melee=true,damage=100,cooldown=2.1f,range=2.1f,radius=.25f,push=7,backHolster=true},
            new EquipmentSpecification{name="BARBED BASEBALL BAT",melee=true,damage=65,cooldown=.85f,range=1.8f,radius=.2f,push=10,backHolster=true},
            new EquipmentSpecification{name="CHAINSAW / WHEEL LAUNCHER",magazine=8,reloadSeconds=2.5f,melee=true,damage=100,cooldown=2.1f,range=1.6f,radius=.18f,push=2,automatic=true,modeSwitch=true,backHolster=true},
            new EquipmentSpecification{name="LASER CLAWS",melee=true,damage=60,cooldown=.7f,range=1.25f,radius=.18f,push=4},
            new EquipmentSpecification{name="FLAMING MARSHMALLOW",melee=true,damage=50,cooldown=.4f,range=1.5f,radius=.18f,push=3},
            new EquipmentSpecification{name="TITAN LASER AXE",magazine=8,reloadSeconds=2.5f,melee=true,damage=100,cooldown=2.4f,range=2.2f,radius=.3f,push=12,modeSwitch=true,twoHanded=true,backHolster=true},
            new EquipmentSpecification{name="PVP DAGGER",melee=true,damage=34,cooldown=.35f,range=1.4f,radius=.16f,push=4},
            new EquipmentSpecification{name="FLYING HAND · 30 SECONDS",power=true}
        };
        public static EquipmentSpecification Get(EquipmentKind kind)=>All[Mathf.Clamp((int)kind,0,All.Length-1)];
    }
}
