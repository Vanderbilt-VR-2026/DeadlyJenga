using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    public enum EquipmentAction { Shot, Draw, Holster, Pickup, Swap, Drop, ModeSwitch, Impact, Reload }
    [Serializable] public sealed class EquipmentActionFrame
    {
        public int id;
        public string actor;
        public EquipmentKind kind;
        public EquipmentAction action;
        public bool shotgun;
        public Vector3 position;
        public Vector3[] endpoints=Array.Empty<Vector3>();
        public float remaining;
    }
    public enum EquipmentKind { None, Speed, Jump, Blaster, MurderSword, SheriffHammer, DualRifle, BoxingGlove, PoweredArmor, RocketLauncher, DualUzi, ChainGun, GrenadeLauncher, WristLaser, NeedleDrill, StockPistol, SawStaff, BarbedBat, Chainsaw, LaserClaws, FlamingMarshmallow, LaserAxe, Dagger, FlyingHand }
    [Serializable] public sealed class EquipmentState
    {
        public string actor;
        public EquipmentKind kind;
        public bool drawn,leftHanded,shotgun,roleIssued;
        public bool thrust;
        public bool builtIn,triggerHeld;
        public EquipmentKind activePower;
        public float charge;
        public int rounds;
        public bool magazineInitialized;
        public float reloadRemaining;
        public string embeddedVictim;
        public Vector3 embeddedPoint;
        public float embedRemaining;
        public float remaining;
        public EquipmentState Copy()=>(EquipmentState)MemberwiseClone();
    }
    [Serializable] public sealed class EquipmentPickup
    {
        public int id;
        public int rounds=-1;
        public EquipmentKind kind;
        public Vector3 position;
        public float remaining=20,availableIn;
        public bool station;
        public bool randomSpawn;
        public int supportBlock=-1;
        public Vector3 supportLocal;
        public float expiresIn;
    }
    public interface IEquipmentRuntime
    {
        EquipmentState EquipmentFor(string actor);
        bool RequestPickup(int pickup,bool swap);
        bool RequestEquipment(bool drawn,bool leftHanded,bool shotgun);
        bool RequestEquipmentAttack(Pose muzzle);
        bool SpawnEquipment(EquipmentKind kind);
        int RegisterPickup(IEquipmentPickupSource source);
    }
    public interface IHeldEquipmentRuntime
    {
        bool RequestEquipmentTrigger(bool held,Pose muzzle);
    }
    [Serializable] public sealed class EquipmentProjectileFrame
    {
        public int id;
        public EquipmentKind kind;
        public string owner;
        public Vector3 position,velocity;
        public float remaining;
    }
    public interface IPoweredEquipmentRuntime
    {
        bool RequestThrust(bool active);
    }
    public interface IWeaponAim
    {
        Pose WeaponAim { get; }
    }
    public interface IEquipmentControls
    {
        EquipmentState Equipment {get;}
        bool BlocksTraversal {get;}
        void SetEquipped(bool drawn);
        void SetWeaponHand(bool left);
        void ToggleRifleMode();
        int PendingPickup {get;}
        void ConfirmSwap(bool accept);
    }
    // Authored pickups register their identity and position; the session owns collection.
    public interface IEquipmentPickupSource
    {
        EquipmentKind Kind {get;}
        Vector3 Position {get;}
        float Duration {get;}
    }
}
