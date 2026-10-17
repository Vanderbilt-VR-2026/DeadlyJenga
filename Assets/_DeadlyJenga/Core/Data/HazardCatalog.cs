using UnityEngine;

namespace DeadlyJenga.Core
{
    [CreateAssetMenu(menuName = "Deadly Jenga/Hazard Catalog")]
    public sealed class HazardCatalog : ScriptableObject
    {
        public GameObject[] prefabs = new GameObject[0];
    }
}
