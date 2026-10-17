using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core.Testing
{
    // Stand-in for any character. Put it on a capsule to test hazards without the real player.
    public class TestDummy : MonoBehaviour, IPushable, IDamageable, IAffectable, ITeleportable
    {
        public float maxHealth = 100;
        public float Health { get; private set; }
        public float MaxHealth => maxHealth;
        readonly HashSet<EffectType> effects = new HashSet<EffectType>();

        void Awake() => Health = maxHealth;

        public void Push(Vector3 force, GameObject source)
        {
            Debug.Log($"{name} pushed {force} by {(source ? source.name : "?")}", this);
            transform.position += force * 0.1f;
            GameEvents.RaisePushed(gameObject, force);
        }

        public void Damage(float amount, GameObject source)
        {
            if (Health <= 0) return;
            Health = Mathf.Max(0, Health - amount);
            Debug.Log($"{name} took {amount} damage, health {Health}", this);
            GameEvents.RaiseDamaged(gameObject, amount);
            if (Health <= 0) Eliminate(source);
        }

        public void Eliminate(GameObject source)
        {
            Health = 0;
            Debug.Log($"{name} eliminated", this);
            GameEvents.RaiseEliminated(gameObject, source);
        }

        public void Apply(Effect effect)
        {
            if (!effect.type || !effects.Add(effect.type)) return;
            Debug.Log($"{name} now has {effect.type.name}", this);
            GameEvents.RaiseEffectStarted(gameObject, effect.type);
        }

        public void Remove(EffectType type)
        {
            if (!effects.Remove(type)) return;
            GameEvents.RaiseEffectEnded(gameObject, type);
        }

        public bool Has(EffectType type) => effects.Contains(type);

        public void Teleport(Vector3 position, Quaternion rotation) => transform.SetPositionAndRotation(position, rotation);
    }
}
