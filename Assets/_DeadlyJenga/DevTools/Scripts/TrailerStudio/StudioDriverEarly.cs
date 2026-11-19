#if UNITY_EDITOR
using UnityEngine;

namespace DeadlyJenga.DevTools.TrailerStudio
{
    // Runs before every other script each frame, so recorded things are in place before live things react.
    [DefaultExecutionOrder(-32000)]
    public sealed class StudioDriverEarly : MonoBehaviour
    {
        void FixedUpdate() => StudioHooks.FixedStep?.Invoke();
        void Update() => StudioHooks.FrameStart?.Invoke();
    }
}
#endif