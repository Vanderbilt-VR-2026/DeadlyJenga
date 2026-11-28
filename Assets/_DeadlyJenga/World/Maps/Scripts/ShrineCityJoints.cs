using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Sets each Shrine City joint's break limits from the load it really carries.
    // For the first seconds after a build every joint is unbreakable while the town settles; the peak force and torque
    // each joint feels are recorded. Then each joint breaks at Multiplier x its own peak (at least the floor), so joints
    // that hold the town up stay strong and lightly loaded ones give way to disasters.
    public sealed class ShrineCityJoints : MonoBehaviour
    {
        struct Entry { public FixedJoint joint; public float peakForce, peakTorque, floorForce, floorTorque; }
        readonly List<Entry> entries = new List<Entry>();
        float settleUntil;
        public float SettleSeconds = 4;
        public float Multiplier = 3;
        public bool Calibrated { get; private set; }
        public int Count => entries.Count;

        public void Track(FixedJoint joint, float floorForce, float floorTorque)
        {
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
            entries.Add(new Entry { joint = joint, floorForce = floorForce, floorTorque = floorTorque });
        }

        void OnEnable() { settleUntil = Time.time + SettleSeconds; Calibrated = false; }

        void FixedUpdate()
        {
            if (Calibrated) return;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!e.joint) continue;
                e.peakForce = Mathf.Max(e.peakForce, e.joint.currentForce.magnitude);
                e.peakTorque = Mathf.Max(e.peakTorque, e.joint.currentTorque.magnitude);
                entries[i] = e;
            }
            if (Time.time < settleUntil) return;
            float scale = ShrineCityLayout.JointScale;
            foreach (var e in entries)
            {
                if (!e.joint) continue;
                e.joint.breakForce = Mathf.Max(Multiplier * e.peakForce, e.floorForce) * scale;
                e.joint.breakTorque = Mathf.Max(Multiplier * e.peakTorque, e.floorTorque) * scale;
            }
            Calibrated = true;
        }
    }
}
