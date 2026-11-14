using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyPlayers
{
    // Each foot looks down for the ground. The pelvis drops so the lower foot stays planted,
    // and a planted foot tilts to the surface it lands on.
    public sealed partial class CandyPlayerRig
    {
        const float AnkleHeight = .075f, SwingLift = .12f, ProbeRise = .5f, ProbeDepth = 1.2f, MaxPelvisDrop = .22f;
        readonly Vector3[] feetLocal = new Vector3[2], feetTarget = new Vector3[2];
        readonly float[] feetPitch = new float[2], feetWeight = new float[2];
        readonly Quaternion[] feetTilt = { Quaternion.identity, Quaternion.identity };
        float groundDrop;

        // Works out where each foot should be. Returns how far the pelvis should drop (world units, zero or negative).
        float PlanFeet(bool planted, float scale, float stride, float run)
        {
            float drop = 0f;
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1, phase = walkPhase + (side == 0 ? 0 : Mathf.PI);
                feetLocal[side] = FootLocal(sign, phase, stride, run, out feetPitch[side]);
                feetWeight[side] = 0f;
                feetTilt[side] = Quaternion.identity;
                if (!planted) continue;
                Vector3 foot = transform.TransformPoint(feetLocal[side]);
                if (!ProbeGround(foot, scale, out Vector3 point, out Vector3 normal)) continue;
                // A swing foot is in the air, so it keeps the animated lift instead of snapping down.
                feetWeight[side] = 1f - Mathf.Clamp01((feetLocal[side].y - AnkleHeight) / SwingLift);
                feetTarget[side] = point + normal * AnkleHeight * scale;
                feetTilt[side] = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), feetWeight[side]);
                if (feetWeight[side] > .5f) drop = Mathf.Min(drop, feetTarget[side].y - foot.y);
            }
            return Mathf.Max(drop, -MaxPelvisDrop * scale);
        }

        // The stance foot travels back at floor height. The swing foot lifts and returns.
        Vector3 FootLocal(float sign, float phase, float stride, float run, out float footPitch)
        {
            Vector3 footLocal = new(sign * .12f, AnkleHeight + Mathf.Max(0, Mathf.Sin(phase)) * stride * .55f, Mathf.Cos(phase) * stride + .025f);
            footPitch = 0f;
            if (run > .001f)
            {
                float cycle = Mathf.Repeat(phase / (2f * Mathf.PI), 1f);
                float flight = Mathf.InverseLerp(.58f, 1f, cycle);
                float step = cycle < .58f ? Mathf.Lerp(.27f, -.29f, cycle / .58f)
                    : Mathf.SmoothStep(-.29f, .27f, flight);
                float lift = Mathf.Sin(flight * Mathf.PI);
                var sprintFoot = new Vector3(sign * .10f, AnkleHeight + lift * .19f, 0) + strideDirection * step;
                footLocal = Vector3.Lerp(footLocal, sprintFoot, run);
                footPitch = run * (cycle < .58f
                    ? Mathf.SmoothStep(0f, 24f, Mathf.InverseLerp(.31f, .58f, cycle))
                    : Mathf.Lerp(24f, -9f, flight) * (1f - flight));
            }
            return footLocal;
        }

        // Finds the ground under a foot. The ray starts above the ankle, so a foot over a drop still finds it.
        static bool ProbeGround(Vector3 foot, float scale, out Vector3 point, out Vector3 normal)
        {
            var origin = foot + Vector3.up * ProbeRise * scale;
            int mask = (1 << Layers.Block) | (1 << Layers.Surface);
            if (Physics.Raycast(origin, Vector3.down, out var hit, (ProbeRise + ProbeDepth) * scale, mask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }
            point = foot;
            normal = Vector3.up;
            return false;
        }
    }
}
