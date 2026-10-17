using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.World.Hazards
{
    [DisallowMultipleComponent]
    public sealed class WaterResistance : MonoBehaviour, IWaterResistance
    {
        [Tooltip("1 = normal, 2 = half the push/lift. Overrides automatic mass-based resistance.")]
        [Min(.1f)] public float multiplier = 1;
        float IWaterResistance.WaterResistance => multiplier;
    }
}
