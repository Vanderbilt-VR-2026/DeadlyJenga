using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // One section of the Sinking Liner's hull. Sections never move on their own: they ride whichever
    // Rigidbody they are parented to (the whole ship at first, a broken-off piece after a split).
    // Each section floats through a grid of points along its keel and can take on water (flood 0..1).
    public sealed class LinerSegment : MonoBehaviour
    {
        public int index;               // 0 = stern ... last = bow
        public float zFrom, zTo;        // span along the ship, in ship space
        public float mass = 2000;
        public float flood;             // 0 = dry, 1 = no buoyancy left
        public float floodTarget;       // flood rises toward this
        public float floodPerSecond;
        public Vector3[] floatPoints;   // keel points, in this section's local space

        public bool Breached => floodTarget > flood || flood > 0;

        public void Breach(float target, float seconds)
        {
            floodTarget = Mathf.Clamp01(Mathf.Max(floodTarget, target));
            floodPerSecond = Mathf.Max(floodPerSecond, floodTarget / Mathf.Max(1, seconds));
        }

        public void StepFlood(float dt)
        {
            if (flood < floodTarget) flood = Mathf.Min(floodTarget, flood + floodPerSecond * dt);
        }
    }
}
