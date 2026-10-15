using UnityEngine;
namespace DeadlyJenga.CandyPhysicsHands
{
    [CreateAssetMenu(menuName="Candy/Physical Hand Settings")]
    public sealed class CandyHandSettings : ScriptableObject
    {
        [Header("Geometry (independent of strength)")]
        [Min(.1f)] public float size=20;
        public Material skin;
        [Header("Bounded drives, Newtons and Newton metres")]
        [Min(.001f)] public float palmMass=30, fingerMass=2;
        [Min(0)] public float palmForce=8000, fingerForce=1200, palmTorque=2000, fingerTorque=150;
        [Min(0)] public float positionSpring=3000, positionDamping=120, rotationSpring=80, rotationDamping=8;
        [Min(.01f)] public float maxSpeed=8, maxAngularSpeed=12;
        [Range(8,64)] public int solverIterations=32;
        [Range(2,32)] public int solverVelocityIterations=16;
        [Range(0,2)] public float staticFriction=1.1f, dynamicFriction=.85f;
        [Header("Tracking transitions, seconds")]
        [Min(0)] public float stableTime=.12f, dropoutHold=.1f, releaseTime=.25f, recoveryTime=.35f;
        [Min(.001f)] public float jitterTime=.035f;
        [Min(.01f)] public float maxTargetSpeed=6, maxTargetAngularSpeed=540;
    }
}
