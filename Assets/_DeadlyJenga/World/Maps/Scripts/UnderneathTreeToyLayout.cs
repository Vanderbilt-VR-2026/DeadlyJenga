using System;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Where every toy goes in Underneath the Tree. Edit it with
    // Tools > Deadly Jenga > Underneath the Tree > Place Toys, or by hand in the inspector.
    [CreateAssetMenu(menuName = "Deadly Jenga/Underneath the Tree Toy Layout")]
    public sealed class UnderneathTreeToyLayout : ScriptableObject
    {
        [Serializable]
        public struct Placement
        {
            public UnderneathTreeToy toy;
            [Tooltip("Name of the gift it sits on, e.g. \"Present 206\". Empty = stands on the rug.")]
            public string onGift;
            [Tooltip("Sideways and forward from the gift's centre (or the course centre on the rug).")]
            public Vector2 spot;
            [Tooltip("Turn in degrees, relative to the gift.")]
            public float turn;
            [Tooltip("1 = the prefab's own size.")]
            public float size;
        }

        public Placement[] toys = Array.Empty<Placement>();
    }
}
