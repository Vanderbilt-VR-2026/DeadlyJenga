using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class DemoDisasterLifetime : MonoBehaviour
    {
        IHazard hazard;
        void Awake() => hazard = GetComponent<IHazard>();
        void Update() { if (hazard == null || !hazard.Active) Destroy(gameObject); }
    }
}
