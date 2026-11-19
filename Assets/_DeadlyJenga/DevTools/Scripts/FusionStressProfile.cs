#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Fusion;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Explicit stress-test switch. The normal player and shipping APK never enable it.
    static class FusionStressProfile
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Enable()
        {
            if (!Array.Exists(Environment.GetCommandLineArgs(), value => value == "--dj-network-stress")) return;
            var config = NetworkProjectConfig.Global;
            var conditions = config.NetworkConditions;
            conditions.Enabled = true;
            conditions.DelayMin = conditions.DelayMax = .075f;
            conditions.AdditionalJitter = .02f;
            conditions.LossChanceMin = conditions.LossChanceMax = .02f;
            config.NetworkConditions = conditions;
            Debug.Log("Deadly Jenga Fusion stress profile: configured 75 ms delay, 20 ms additional jitter, 2% loss.");
        }
    }
}
#endif
