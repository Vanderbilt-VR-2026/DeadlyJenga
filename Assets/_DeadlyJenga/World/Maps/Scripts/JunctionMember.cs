using DeadlyJenga.Core;
using System.Collections;
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
        Vector3 localGrip, targetGrip;
        Coroutine pulling;
        static readonly WaitForFixedUpdate PhysicsStep = new();
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
            targetGrip = point;
            Body.WakeUp();
            pulling = StartCoroutine(DriveGrip());
            return true;
        }
        public void Hold(Vector3 point)
        {
            if (!held || !Authority) return;
            targetGrip = point;
        }
        IEnumerator DriveGrip()
        {
            while (held && Authority)
            {
                var at = transform.TransformPoint(localGrip);
                if (Vector3.Distance(targetGrip,at)>.45f && TryGetComponent<FixedJoint>(out var pile)) Destroy(pile);
                var pull = (targetGrip - at) * 100 - Body.GetPointVelocity(at) * 12;
                Body.AddForceAtPosition(Vector3.ClampMagnitude(pull, 120), at, ForceMode.Acceleration);
                yield return PhysicsStep;
            }
            held = false;
            pulling = null;
        }
        public void Release()
        {
            held = false;
            if(pulling != null) StopCoroutine(pulling);
            pulling = null;
        }
        void OnDisable() => Release();
        public void Push(Vector3 force, GameObject source)
        {
            if (!Authority || Body.isKinematic) return;
            Body.AddForce(force, ForceMode.VelocityChange);
            GameEvents.RaisePushed(gameObject, force);
        }
    }
}
