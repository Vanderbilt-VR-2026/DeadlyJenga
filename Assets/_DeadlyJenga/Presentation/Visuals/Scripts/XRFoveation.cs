using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace DeadlyJenga.Presentation
{
    // Turns on foveated rendering on the headset: the edges of each lens render at lower resolution.
    // Needs the OpenXR "Foveated Rendering" feature with the SRP foveation method (set in project settings).
    public sealed class XRFoveation : MonoBehaviour
    {
        [Range(0, 1)] public float level = 1;   // Quest: 0.25 low, 0.5 medium, 0.75 high, 1 high-top
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        float nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor || FindFirstObjectByType<XRFoveation>()) return;
            var go = new GameObject("XR foveation");
            DontDestroyOnLoad(go);
            go.AddComponent<XRFoveation>();
        }

        // Re-applied every couple of seconds: XR can restart (pause, focus loss) and reset it.
        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 2;
            SubsystemManager.GetSubsystems(displays);
            foreach (var d in displays)
            {
                if (!d.running) continue;
                if (!Mathf.Approximately(d.foveatedRenderingLevel, level)) d.foveatedRenderingLevel = level;
                d.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.GazeAllowed;
            }
        }
    }
}
