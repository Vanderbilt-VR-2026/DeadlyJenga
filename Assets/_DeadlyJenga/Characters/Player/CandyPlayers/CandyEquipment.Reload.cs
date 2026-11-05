using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyEquipment
    {
        Pose PresentedReloadGrip(Pose pose, EquipmentState state, bool handLeft)
        {
            if (motor && motor.IsVR || !state.drawn || state.reloadRemaining <= 0 ||
                state.kind == EquipmentKind.WristLaser) return pose;
            var spec = EquipmentSpecifications.Get(state.kind);
            if (spec.reloadSeconds <= 0 || spec.melee && !state.shotgun) return pose;
            float phase = 1 - Mathf.Clamp01(state.reloadRemaining / spec.reloadSeconds);
            float tilt = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .18f, phase)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.78f, 1, phase)));
            float side = handLeft ? -1 : 1;
            return new Pose(pose.position, pose.rotation * Quaternion.Euler(17 * tilt, 0, side * 23 * tilt));
        }
    }
}
