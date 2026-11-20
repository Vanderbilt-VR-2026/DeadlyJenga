using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Lets players climb the Deck Ship's walls, wheelhouse and mast ladder. Climbers move with the hull.
    public sealed class DeckShipHullClimb : MonoBehaviour, IClimbable
    {
        public readonly List<Collider> Faces = new List<Collider>();
        public Rigidbody Body { get; set; }
        public bool CanClimb => isActiveAndEnabled;
        public float Grip => 1;
        public Transform Anchor => transform;
        public Transform Visual => null;
    }
}
