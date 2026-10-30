using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // Everything currently active that has a movement state. Characters add themselves
    // when enabled and remove themselves when disabled; anyone can read the lists.
    public static class Tracked
    {
        public static readonly List<IMovementState> Movers = new List<IMovementState>();
        public static readonly List<IFlyingState> Flyers = new List<IFlyingState>();
        public static readonly List<IWaveState> Waves = new List<IWaveState>();
        public static readonly List<IHandLook> HandLooks = new List<IHandLook>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Movers.Clear(); Flyers.Clear(); Waves.Clear(); HandLooks.Clear(); }
    }
}
