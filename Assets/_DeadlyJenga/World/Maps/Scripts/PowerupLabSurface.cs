using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class PowerupLabSurface : MonoBehaviour,IClimbable
    {
        public bool CanClimb=>isActiveAndEnabled;
        public float Grip=>1;
        public Transform Anchor=>transform;
        public Rigidbody Body=>GetComponent<Rigidbody>();
        public Transform Visual=>null;
    }
}
