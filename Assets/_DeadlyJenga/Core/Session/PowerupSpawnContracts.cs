using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    [Serializable] public sealed class PowerupSpawnWeight
    {
        public EquipmentKind kind;
        public float weight;
    }
    [Serializable] public sealed class PowerupSpawnSettings
    {
        public bool enabled;
        public float interval=15,jitter=.25f,lifetime=90;
        public int maximum=8;
        public PowerupSpawnWeight[] types=Array.Empty<PowerupSpawnWeight>();
    }
    public interface IPowerupSpawnControls
    {
        PowerupSpawnSettings SpawnSettings {get;}
        int RandomPickupCount {get;}
        float NextSpawnIn {get;}
        bool ConfigurePowerupSpawns(PowerupSpawnSettings settings);
    }
    public interface IBotEquipmentRuntime
    {
        bool SetBotWeapon(GameObject bot,bool drawn,bool shotgun);
        bool FireBotWeapon(GameObject bot,Pose muzzle);
    }
}
