using System;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    [Serializable]
    public sealed class VillageConnection
    {
        public VillagePiece target;
        public Vector3 anchor;
        public float force = 12000, torque = 10000;
        [NonSerialized] public FixedJoint joint;
        [NonSerialized] public bool broken, created;
    }

    [RequireComponent(typeof(CastleBlock))]
    public sealed class VillagePiece : MonoBehaviour, IDamageable
    {
        public string building, purpose;
        public bool structural;
        public Vector3 homePosition;
        public Quaternion homeRotation = Quaternion.identity;
        public VillageConnection[] connections = Array.Empty<VillageConnection>();
        public float durability = 14;
        public float Health => Mathf.Max(0, durability - damage);
        public float MaxHealth => durability;
        public float AccumulatedDamage => damage;
        public Rigidbody Body => body ? body : body = GetComponent<Rigidbody>();
        public int BrokenConnections
        {
            get
            {
                int n = 0;
                foreach (var c in connections) if (c.broken || (c.created && !c.joint)) n++;
                return n;
            }
        }
        Rigidbody body;
        readonly List<FixedJoint> retired = new List<FixedJoint>();
        float damage;
        bool replica;
        public event Action<VillagePiece> Fractured;
        public event Action<VillagePiece, float> ConnectionFailed;

        public void ClearConnectionsForRebuild()
        {
            // Rebuild runs outside contact callbacks. Clear solver history and pending removals atomically.
            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.isKinematic = true;
            foreach (var c in connections)
            {
                if (c.joint) DestroyImmediate(c.joint);
                c.joint = null; c.created = false;
            }
            foreach (var joint in retired) if (joint) DestroyImmediate(joint);
            retired.Clear();
        }

        public void RestorePose(bool isReplica)
        {
            replica = isReplica; damage = 0;
            transform.localPosition = homePosition; transform.localRotation = homeRotation;
            Body.position = transform.position; Body.rotation = transform.rotation;
            Body.isKinematic = isReplica;
            if (!isReplica) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; Body.WakeUp(); }
            Body.collisionDetectionMode = isReplica ? CollisionDetectionMode.Discrete : CollisionDetectionMode.ContinuousSpeculative;
            foreach (var c in connections) c.broken = false;
            GetComponent<CastleBlock>().SyncSurface();
        }
        public void RestoreConnections()
        {
            foreach (var c in connections)
            {
                if (replica || c.broken) { Remove(c); continue; }
                if (!c.joint) c.joint = gameObject.AddComponent<FixedJoint>();
                var j = c.joint;
                j.autoConfigureConnectedAnchor = false;
                j.connectedBody = c.target ? c.target.Body : null;
                j.anchor = c.anchor;
                Vector3 point = transform.TransformPoint(c.anchor);
                j.connectedAnchor = c.target ? c.target.transform.InverseTransformPoint(point) : point;
                j.breakForce = c.force; j.breakTorque = c.torque;
                j.enableCollision = false; j.enablePreprocessing = true;
                c.created = true;
            }
        }
        public void SetReplica(bool value)
        {
            RefreshBreaks();
            replica = value;
            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            if (value && !Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            Body.isKinematic = value;
            if (!value) Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            RestoreConnections();
        }
        public void Damage(float amount, GameObject source)
        {
            if (replica || !Finite(amount) || amount <= 0 || Health <= 0) return;
            if (Services.Session is IWorldAuthority authority && !authority.HasWorldAuthority) return;
            damage = Mathf.Min(durability, damage + amount); GameEvents.RaiseDamaged(gameObject, amount);
            if (Health <= 0) Fractured?.Invoke(this);
        }
        public void Eliminate(GameObject source) => Damage(MaxHealth, source);
        public void CutConnectionsTo(VillagePiece piece)
        {
            bool changed = false;
            foreach (var c in connections)
                if (!c.broken && (this == piece || c.target == piece)) { c.broken = true; Remove(c); changed = true; }
            if(changed || this==piece)Body.WakeUp();
        }
        public void RestoreDamage(float value) => damage=value;
        public void RefreshBreaks()
        { foreach (var c in connections) if (c.created && !c.joint) c.broken = true; }
        public void ApplyBroken(bool[] state, ref int cursor)
        { foreach (var c in connections) { c.broken = state[cursor++]; if (c.broken) Remove(c); } }
        void Remove(VillageConnection c)
        {
            if (c.joint) { c.joint.breakForce = 0; c.joint.breakTorque = 0; if (Application.isPlaying) { retired.Add(c.joint); Destroy(c.joint); } else DestroyImmediate(c.joint); }
            c.joint = null;
            c.created = false;
        }
        void OnJointBreak(float force) { ConnectionFailed?.Invoke(this,force);RefreshBreaks(); }
        void OnCollisionEnter(Collision hit)
        {
            // Only substantial secondary impacts damage connections; resting support is not impact damage.
            if (replica || hit.relativeVelocity.sqrMagnitude < 36 || hit.impulse.sqrMagnitude < 400) return;
            Damage(Mathf.Min(12, hit.impulse.magnitude / Mathf.Max(5, Body.mass) * .4f), hit.gameObject);
        }
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
}
