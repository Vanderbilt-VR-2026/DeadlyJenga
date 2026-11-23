using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class JunctionMember : MonoBehaviour, IClimbable, IGrabbable, IPushable
    {
        public Rigidbody Body { get; private set; }
        public Transform Visual { get; private set; }
        public Transform Anchor => transform;
        public bool CanClimb => isActiveAndEnabled;
        public float Grip => 1;
        public float Weight => Body.mass;
        public bool Replica { get; set; }
        bool held;
        Vector3 localGrip;
        bool Authority => !Replica && !(Services.Session is IWorldAuthority a && !a.HasWorldAuthority);

        internal void Setup(Rigidbody body, Transform visual)
        {
            Body = body;
            Visual = visual;
            foreach (var c in GetComponentsInChildren<Collider>()) Climbables.Register(c, this);
        }
        void OnDestroy()
        {
            foreach (var c in GetComponentsInChildren<Collider>()) Climbables.Unregister(c);
            foreach(var hull in GetComponents<MeshCollider>())
                if(hull.sharedMesh) Destroy(hull.sharedMesh);
            if (Visual && Visual.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh) Destroy(f.sharedMesh);
        }
        public bool CanGrab(GameObject hand) => Authority && !Body.isKinematic && !held;
        public bool TryStartGrab(GameObject hand, Vector3 point)
        {
            if (!CanGrab(hand)) return false;
            held = true;
            localGrip = transform.InverseTransformPoint(point);
            Body.WakeUp();
            return true;
        }
        public void Hold(Vector3 point)
        {
            if (!held || !Authority) return;
            var at = transform.TransformPoint(localGrip);
            if (Vector3.Distance(point,at)>.45f && TryGetComponent<FixedJoint>(out var pile)) Destroy(pile);
            var pull = (point - at) * 100 - Body.GetPointVelocity(at) * 12;
            Body.AddForceAtPosition(Vector3.ClampMagnitude(pull, 120), at, ForceMode.Acceleration);
        }
        public void Release() => held = false;
        public void Push(Vector3 force, GameObject source)
        {
            if (!Authority || Body.isKinematic) return;
            Body.AddForce(force, ForceMode.VelocityChange);
            GameEvents.RaisePushed(gameObject, force);
        }
    }
}
