#if UNITY_EDITOR
using UnityEngine;

namespace DeadlyJenga.DevTools.TrailerStudio
{
    // Runs after every other script each frame, so the Studio records what the frame ended up looking like.
    [DefaultExecutionOrder(32000)]
    public sealed class StudioDriverLate : MonoBehaviour
    {
        void LateUpdate() => StudioHooks.FrameEnd?.Invoke();
        void OnGUI() => StudioHooks.Gui?.Invoke();
    }
}
#endif