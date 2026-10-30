using UnityEngine;

namespace DeadlyJenga.Core
{
    // Shared colliders retain the abilities of their original logical owner.
    public static class HitAbilities
    {
        public static T Get<T>(Collider collider) where T : class
        {
            if (!collider) return null;
            if (Climbables.TryGet(collider, out var owner))
            {
                if (owner is T ability) return ability;
                if (owner.Body)
                {
                    var component = owner.Body.GetComponent<T>();
                    if (Climbables.Alive(component)) return component;
                }
            }
            return collider.GetComponentInParent<T>();
        }
    }
}
