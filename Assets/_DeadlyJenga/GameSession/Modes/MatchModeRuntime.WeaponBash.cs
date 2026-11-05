using DeadlyJenga.Core;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        readonly struct WeaponBashProfile
        {
            public readonly float range, damage, cooldown, push;
            public WeaponBashProfile(float range, float damage, float cooldown, float push)
            {
                this.range = range;
                this.damage = damage;
                this.cooldown = cooldown;
                this.push = push;
            }
        }

        static bool UsesWeaponBash(EquipmentState state)
        {
            if (!state.drawn || state.kind < EquipmentKind.Blaster || state.kind == EquipmentKind.PoweredArmor ||
                EquipmentSpecifications.Get(state.kind).power) return false;
            return !EquipmentSpecifications.Get(state.kind).melee ||
                state.shotgun && (state.kind == EquipmentKind.Chainsaw || state.kind == EquipmentKind.LaserAxe);
        }

        static WeaponBashProfile BashProfile(EquipmentKind kind) => kind switch
        {
            EquipmentKind.Blaster => new WeaponBashProfile(.95f, 20, .85f, .8f),
            EquipmentKind.SheriffHammer => new WeaponBashProfile(1.0f, 24, .9f, .9f),
            EquipmentKind.DualRifle => new WeaponBashProfile(1.3f, 28, 1.05f, 1.1f),
            EquipmentKind.RocketLauncher or EquipmentKind.ChainGun or EquipmentKind.GrenadeLauncher =>
                new WeaponBashProfile(1.25f, 30, 1.15f, 1.15f),
            EquipmentKind.DualUzi or EquipmentKind.StockPistol => new WeaponBashProfile(.95f, 20, .85f, .8f),
            EquipmentKind.WristLaser => new WeaponBashProfile(.85f, 18, .8f, .75f),
            EquipmentKind.NeedleDrill => new WeaponBashProfile(1.0f, 22, .95f, .9f),
            _ => new WeaponBashProfile(1.3f, 28, 1.1f, 1.05f)
        };
    }
}
