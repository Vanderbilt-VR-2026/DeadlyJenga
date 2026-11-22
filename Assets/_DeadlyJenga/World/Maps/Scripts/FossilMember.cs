using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class FossilMember : MonoBehaviour, IClimbable, IGrabbable, IPushable, IHazardMotion
    {
        internal Rigidbody rigid;
        internal readonly List<Collider> shapes = new();
        internal Mesh mesh;
        internal Transform art;
        Vector3 grabOffset, target;
        bool held;
        public Rigidbody Body => rigid;
        public Transform Visual => art;
        public Transform Anchor => transform;
        public float Grip => 1;
        public bool CanClimb => isActiveAndEnabled;
        public float Weight => rigid.mass;
        public Vector3 HazardVelocity => rigid.linearVelocity;
        bool Authoritative => !rigid.isKinematic && !(Services.Session is IWorldAuthority a && !a.HasWorldAuthority);
        public bool CanGrab(GameObject hand) => isActiveAndEnabled && !held && Authoritative;
        public bool TryStartGrab(GameObject hand, Vector3 position)
        {
            if (!CanGrab(hand)) return false;
            grabOffset = transform.InverseTransformPoint(position);
            target = position;
            held = true;
            rigid.WakeUp();
            return true;
        }
        public void Hold(Vector3 position) => target = position;
        public void Release() => held = false;
        void FixedUpdate()
        {
            if (!held || !Authoritative) return;
            var at = transform.TransformPoint(grabOffset);
            var pull = (target - at) * 180 - rigid.GetPointVelocity(at) * 14;
            rigid.AddForceAtPosition(Vector3.ClampMagnitude(pull, 190), at, ForceMode.Acceleration);
        }
        public void Push(Vector3 force, GameObject source)
        {
            if (!Authoritative) return;
            rigid.AddForce(force, ForceMode.VelocityChange);
            GameEvents.RaisePushed(gameObject, force);
        }
        void OnDisable() => held = false;
        void OnDestroy()
        {
            foreach (var shape in shapes) Climbables.Unregister(shape);
            if (mesh) Destroy(mesh);
        }
    }
}
