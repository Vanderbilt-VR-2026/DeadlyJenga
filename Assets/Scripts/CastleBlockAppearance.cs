using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    [CreateAssetMenu(menuName = "Castle/Block Appearance")]
    public sealed class CastleBlockAppearance : ScriptableObject
    {
        [Tooltip("Visual-only prefab, authored inside a unit cube. Scaled to crate dimensions by the physics root.")]
        public GameObject visualPrefab;
        [Tooltip("Optional material override for every crate. Leave empty to retain section colors. Texture edits update live.")]
        public Material materialOverride;
    }
}
