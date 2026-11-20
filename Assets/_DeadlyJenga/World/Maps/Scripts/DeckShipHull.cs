using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Put this on the root of a Deck Ship hull prefab. It tells the map where the open cargo deck is.
    // Everything under this object rocks with the ship: colliders (Default layer = solid, Surface layer =
    // climbable), art, flags. Do not mark anything Static and do not add Rigidbodies; the map adds one.
    // Check a build with Tools > Deadly Jenga > Maps > Deck Ship > Check Ship Build.
    public sealed class DeckShipHull : MonoBehaviour
    {
        [Tooltip("Center of the open cargo deck, placed ON the deck surface. Its up arrow is the deck's up, its forward arrow points to the bow.")]
        public Transform cargoArea;
        [Tooltip("More cargo bays of the same size (a big ship can carry several submaps at once). Same rules as the cargo area.")]
        public Transform[] extraCargoAreas;
        [Tooltip("Half the open cargo deck: x = across the ship, y = along it. Must be flat and empty.")]
        public Vector2 cargoHalfSize = new Vector2(DeckShipCargo.FieldHalfWidth, DeckShipCargo.FieldMaxZ);
        [Tooltip("Height of the raised square hatch in the middle of the cargo deck (0 = no hatch).")]
        public float hatchHeight = DeckShipCargo.HatchHeight;
        public float hatchHalf = DeckShipCargo.HatchHalf;
        [Tooltip("Clear space above the cargo deck.")]
        public float headroom = 16;
        [Tooltip("Where players appear. Keep them off the cargo deck so new cargo cannot land on them.")]
        public Transform[] spawnPoints;

        // Every cargo bay: the main cargo area first, then the extra ones.
        public IEnumerable<Transform> Bays
        {
            get
            {
                if (cargoArea) yield return cargoArea;
                if (extraCargoAreas != null) foreach (var bay in extraCargoAreas) if (bay) yield return bay;
            }
        }

        void OnDrawGizmos()
        {
            foreach (var bay in Bays)
            {
                Gizmos.matrix = Matrix4x4.TRS(bay.position, bay.rotation, Vector3.one);
                Gizmos.color = new Color(.2f, 1, .3f, .9f);
                Gizmos.DrawWireCube(Vector3.up * headroom * .5f, new Vector3(cargoHalfSize.x * 2, headroom, cargoHalfSize.y * 2));
                Gizmos.color = new Color(.2f, 1, .3f, .25f);
                Gizmos.DrawCube(Vector3.up * .02f, new Vector3(cargoHalfSize.x * 2, .04f, cargoHalfSize.y * 2));
                if (hatchHeight > 0)
                {
                    Gizmos.color = new Color(1, .7f, .2f, .6f);
                    Gizmos.DrawWireCube(Vector3.up * hatchHeight * .5f, new Vector3(hatchHalf * 2, hatchHeight, hatchHalf * 2));
                }
                Gizmos.matrix = Matrix4x4.identity;
            }
            if (spawnPoints == null) return;
            Gizmos.color = new Color(.3f, .6f, 1, .9f);
            foreach (var spawn in spawnPoints)
                if (spawn) { Gizmos.DrawWireSphere(spawn.position + Vector3.up * .45f, .3f); Gizmos.DrawLine(spawn.position, spawn.position + Vector3.up * .85f); }
        }
    }
}
