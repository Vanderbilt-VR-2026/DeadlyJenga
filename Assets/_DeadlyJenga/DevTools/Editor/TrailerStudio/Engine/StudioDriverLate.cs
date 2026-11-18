using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Runs after every other script each frame: records what the frame ended up looking like.
    [DefaultExecutionOrder(32000)]
    public sealed class StudioDriverLate : MonoBehaviour
    {
        void LateUpdate() => StudioRun.FrameEnd();
    }
}
