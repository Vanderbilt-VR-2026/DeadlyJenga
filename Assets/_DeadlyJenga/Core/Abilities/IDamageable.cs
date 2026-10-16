using UnityEngine;

namespace DeadlyJenga.Core
{
    // Can be hurt or eliminated.
    public interface IDamageable
    {
        float Health { get; }
        float MaxHealth { get; }
        void Damage(float amount, GameObject source);
        void Eliminate(GameObject source);
    }
}
