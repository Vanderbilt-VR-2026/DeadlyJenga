using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // How the survivor responds to the shared abilities in Core.
    // Teleport(position, rotation) already exists in CastleSurvivor.cs.
    public sealed partial class CastleSurvivor : IPushable, IDamageable, IAffectable, ITeleportable, IMovementState, IPlayerView, IHazardMotion
    {
        [Header("Health")]
        public float maxHealth = 100;
        float health = -1;
        bool eliminated;
        readonly Dictionary<EffectType, float> effectsEndAt = new Dictionary<EffectType, float>();
        readonly List<EffectType> expired = new List<EffectType>();

        public float MaxHealth => maxHealth;
        public Vector3 HazardVelocity => slopeVelocity + Vector3.up * verticalSpeed;
        public float Health => health < 0 ? maxHealth : health;

        public void Push(Vector3 force, GameObject source)
        {
            ApplyTraversalImpulse(force);
            GameEvents.RaisePushed(gameObject, force);
        }

        public void Damage(float amount, GameObject source)
        {
            if (eliminated || amount <= 0) return;
            health = Mathf.Max(0, Health - amount);
            GameEvents.RaiseDamaged(gameObject, amount);
            if (health <= 0) Eliminate(source);
        }

        public void Eliminate(GameObject source)
        {
            if (eliminated) return;
            eliminated = true;
            health = 0;
            GameEvents.RaiseEliminated(gameObject, source);
        }

        // Called by whoever respawns this survivor.
        public void Revive()
        {
            eliminated = false;
            health = maxHealth;
        }

        public void Apply(Effect effect)
        {
            if (!effect.type) return;
            bool isNew = !effectsEndAt.ContainsKey(effect.type);
            effectsEndAt[effect.type] = effect.duration > 0 ? Time.time + effect.duration : float.PositiveInfinity;
            if (isNew) GameEvents.RaiseEffectStarted(gameObject, effect.type);
            // Reactions to specific effects (slippery, low gravity...) get added here.
        }

        public void Remove(EffectType type)
        {
            if (type && effectsEndAt.Remove(type)) GameEvents.RaiseEffectEnded(gameObject, type);
        }

        public bool Has(EffectType type) => type && effectsEndAt.ContainsKey(type);

        // IMovementState (Grounded, Climbing, Vaulting, VerticalSpeed, VaultCount already exist)
        public float Stamina => GripRemaining;
        public float Height => StandingHeight;
        public float Scale => playerScale;
        public bool IsLocal => !externallyDriven && viewCamera && viewCamera.isActiveAndEnabled;
        public bool InputPaused => InputSuspended;

        // IPlayerView (IsVR, StaminaExhausted, StaminaRecovering already exist)
        public Camera ViewCamera => viewCamera;
        public bool ControlledHere => !externallyDriven;
        public bool TryGetGrip(bool left, out Pose grip) => TryGetClimbGrip(left, out grip);

        void LateUpdate()
        {
            if (effectsEndAt.Count == 0) return;
            expired.Clear();
            foreach (var pair in effectsEndAt) if (Time.time >= pair.Value) expired.Add(pair.Key);
            foreach (var type in expired) Remove(type);
        }
    }
}
