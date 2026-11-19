using Unity.Pipeline.MethodReload;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    public sealed class LivePlayLook
    {
        [MethodReload]
        public void Apply(LivePlayPreview marker)
        {
            marker.Message = "LIVE PREVIEW / BASELINE";
            marker.Tint = new Color(.44f, .25f, .85f);
        }
    }

    public static class LivePlayPreviewMenu
    {
        const string Toggle = "Tools/Deadly Jenga/Live Play/Toggle preview marker";

        [MenuItem(Toggle, true)]
        static bool CanToggle() => EditorApplication.isPlaying;

        [MenuItem(Toggle)]
        public static void ToggleMarker()
        {
            if (!EditorApplication.isPlaying) return;
            var current = Object.FindFirstObjectByType<LivePlayPreview>();
            if (current)
            {
                Object.Destroy(current.gameObject);
                return;
            }
            var marker = new GameObject("Live Play Preview");
            Object.DontDestroyOnLoad(marker);
            marker.AddComponent<LivePlayPreview>();
        }
    }
}
