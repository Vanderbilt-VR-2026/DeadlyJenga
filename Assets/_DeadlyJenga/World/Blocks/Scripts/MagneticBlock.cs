using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    [RequireComponent(typeof(CastleBlock))]
    public sealed class MagneticBlock : MonoBehaviour, IDamageable
    {
        [Range(1, 3)] public int strength = 3;
        public bool foundationAnchor;
        public MagneticStructure Structure { get; internal set; }
        public int Index { get; internal set; }
        public bool Broken { get; internal set; }
        public bool Supported { get; internal set; }
        public float MaxHealth => strength == 1 ? 3 : strength == 2 ? 8 : 20;
        public float Health => Broken ? 0 : Mathf.Max(0, MaxHealth - damage);
        float damage;
        CastleBlock block;
        public Rigidbody Body => (block ? block : block = GetComponent<CastleBlock>()).Body;

        void Awake() { block = GetComponent<CastleBlock>(); block.RegisterMagnet(this); }

        public bool TryPush(Vector3 velocity)
        {
            if (!Structure) return false;
            if (Structure.Replica) return true;
            if (!Supported) return false;
            Damage(velocity.magnitude, null);
            if (!Supported) Body.AddForce(velocity, ForceMode.VelocityChange);
            return true;
        }
        public void Damage(float amount, GameObject source)
        {
            if (!Structure || Structure.Replica || Broken || !Supported || amount <= 0 || float.IsNaN(amount)) return;
            damage += amount;
            GameEvents.RaiseDamaged(gameObject, amount);
            if (damage >= MaxHealth) Structure.Break(this);
        }
        public void Eliminate(GameObject source) => Damage(MaxHealth, source);
        [ContextMenu("Break magnetic connections (Play Mode)")]
        void BreakFromInspector() { if (Application.isPlaying) Eliminate(gameObject); }
        void OnCollisionEnter(Collision collision) => ApplyContact(collision);
        void OnCollisionStay(Collision collision) => ApplyContact(collision);
        void ApplyContact(Collision collision)
        {
            if (!Supported || !collision.rigidbody || collision.rigidbody.isKinematic) return;
            // Convert contact impulse to the same velocity-change units used by IPushable.
            Damage(collision.impulse.magnitude / Mathf.Max(1, Body.mass), collision.gameObject);
        }
    }
}
