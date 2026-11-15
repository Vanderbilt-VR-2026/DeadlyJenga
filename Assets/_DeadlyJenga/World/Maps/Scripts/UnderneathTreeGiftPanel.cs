using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // The panel stays in its wrapping until an impact knocks it free.
    public sealed class UnderneathTreeGiftPanel : MonoBehaviour, IClimbable, IPushable, IHazardMotion
    {
        public bool CanClimb => isActiveAndEnabled;
        public float Grip => 1f;
        public Transform Anchor => transform;
        public Rigidbody Body { get; private set; }
        public Transform Visual { get; private set; }
        public Vector3 HazardVelocity => Body ? Body.linearVelocity : Vector3.zero;

        bool replica;
        bool loose;

        public void SetUp(Rigidbody body, Transform visual)
        {
            Body = body;
            Visual = visual;
        }

        public void SetReplica(bool value)
        {
            replica = value;
            if (value)
                Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.isKinematic = value || !loose;
            if (!Body.isKinematic)
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        public void Push(Vector3 force, GameObject source)
        {
            if (replica || !Body || !isActiveAndEnabled ||
                (Services.Session is IWorldAuthority authority && !authority.HasWorldAuthority))
                return;
            if (!loose && force.sqrMagnitude < 1.4f * 1.4f)
                return;
            if (!loose)
            {
                loose = true;
                SetReplica(false);
            }
            Body.AddForce(force, ForceMode.VelocityChange);
            GameEvents.RaiseBlockHit(transform.position, force.magnitude);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (loose || !collision.rigidbody || collision.rigidbody.isKinematic ||
                collision.relativeVelocity.sqrMagnitude < 9f)
                return;
            if (collision.rigidbody.TryGetComponent<UnderneathTreeGiftPanel>(out var panel) && !panel.loose)
                return;
            Push(Vector3.ClampMagnitude(collision.relativeVelocity * .35f, 8f), collision.gameObject);
        }
    }
}
