using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleSurvivor : ITimedEffects
    {
        static EffectType speedEffect,jumpEffect;
        static EffectType SpeedEffect=>speedEffect?speedEffect:speedEffect=Resources.Load<EffectType>("Powerups/Speed2x");
        static EffectType JumpEffect=>jumpEffect?jumpEffect:jumpEffect=Resources.Load<EffectType>("Powerups/Jump3x");
        public float SecondsRemaining(EffectType type)=>type&&effectsEndAt.TryGetValue(type,out float until)?Mathf.Max(0,until-Time.time):0;
        public float PowerupSpeedMultiplier=>Has(SpeedEffect)?2:1;
        public float EffectiveJumpHeight=>jumpHeight*(Has(JumpEffect)?3:1);
        public bool PowerupUnlimitedStamina=>Has(SpeedEffect)||Has(JumpEffect);
        void ClearMovementPowerups(){Remove(SpeedEffect);Remove(JumpEffect);}
    }
}
