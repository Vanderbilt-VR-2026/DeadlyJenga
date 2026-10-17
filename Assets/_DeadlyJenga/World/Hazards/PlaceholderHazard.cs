using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.World.Hazards
{
    public abstract class PlaceholderHazard : MapHazard
    {
        protected sealed override void BeginHazard(IWorldQuery world, Bounds bounds, float strength, float seconds)
        {
            try { RunPlaceholder(world, bounds, strength, seconds); }
            finally { End(); }
        }

        protected abstract void RunPlaceholder(IWorldQuery world, Bounds bounds, float strength, float seconds);

        protected void ReportPlaceholder()
        {
            Debug.Log($"{Type.name}: placeholder triggered. No disaster effects yet.", this);
        }

    }
}
