#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using DeadlyJenga.Core;
namespace DeadlyJenga.CastlePrototype
{
    public sealed class DisasterProbe : MonoBehaviour, IPushable, IDamageable
    {
        public Vector3 impulse;
        public float Health { get; private set; }=100;
        public float MaxHealth=>100;
        public void Push(Vector3 force,GameObject source)=>impulse+=force;
        public void Damage(float amount,GameObject source)=>Health=Mathf.Max(0,Health-amount);
        public void Eliminate(GameObject source)=>Health=0;
    }
}
#endif
