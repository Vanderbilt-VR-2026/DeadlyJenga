using System.Collections;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.World.Hazards
{
    // Time Slip: the whole tower jumps back a few seconds.
    public sealed class TimeSlipHazard : MapHazard
    {
        [SerializeField, Min(.5f)] float rewindSeconds = 4;

        protected override void BeginHazard(IWorldQuery world, Bounds bounds, float strength, float seconds)
        {
            var rewind = Services.Rewind;
            if (rewind == null || !rewind.Rewind(rewindSeconds * strength)) { End(); return; }
            StartCoroutine(EndWhenDone());
        }

        IEnumerator EndWhenDone()
        {
            while (Services.Rewind != null && Services.Rewind.Rewinding) yield return null;
            End();
        }
    }
}
