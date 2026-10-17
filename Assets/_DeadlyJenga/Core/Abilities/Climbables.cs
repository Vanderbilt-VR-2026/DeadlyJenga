using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // Finds the IClimbable behind a collider, including colliders on separate helper objects.
    public static class Climbables
    {
        static readonly Dictionary<Collider, IClimbable> registered = new Dictionary<Collider, IClimbable>();

        public static void Register(Collider collider, IClimbable climbable) => registered[collider] = climbable;
        public static void Unregister(Collider collider) => registered.Remove(collider);

        public static bool TryGet(Collider collider, out IClimbable climbable)
        {
            climbable = null;
            if (!collider) return false;
            if (!registered.TryGetValue(collider, out climbable)) climbable = collider.GetComponentInParent<IClimbable>();
            return Alive(climbable);
        }

        // Interfaces can't use Unity's "destroyed object" check, so use this instead.
        public static bool Alive(object thing) => thing is Object unityObject ? unityObject : thing != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => registered.Clear();
    }
}
