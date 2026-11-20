using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Marks one building's colliders as climbable. Sits on the object that holds the
    // building's COL_B_<id>_* colliders (never on terrain or the map root).
    public sealed class CampusClimbSurface : MonoBehaviour, IClimbable
    {
        public bool CanClimb => isActiveAndEnabled;
        public float Grip => 1;
        public Transform Anchor => transform;
        public Rigidbody Body => null;      // buildings never move
        public Transform Visual => null;
    }
}
