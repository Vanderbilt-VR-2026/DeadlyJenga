using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Runs before every other script each frame: shows the recorded frame, so live things react to it.
    [DefaultExecutionOrder(-32000)]
    public sealed class StudioDriverEarly : MonoBehaviour
    {
        public static void Ensure()
        {
            if (FindFirstObjectByType<StudioDriverEarly>()) return;
            var host = new GameObject("Trailer Studio driver");
            host.AddComponent<StudioDriverEarly>();
            host.AddComponent<StudioDriverLate>();
        }

        void FixedUpdate() => StudioRun.FixedStep();
        void Update() => StudioRun.FrameStart();
    }
}
