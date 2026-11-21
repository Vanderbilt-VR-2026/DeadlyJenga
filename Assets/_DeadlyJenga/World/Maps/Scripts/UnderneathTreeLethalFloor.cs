using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Marks the wood room floor as deadly: in island modes, touching it eliminates at once.
    // The carpet stays ordinary sinking ground.
    public sealed class UnderneathTreeLethalFloor : MonoBehaviour, ILethalGround { }
}
