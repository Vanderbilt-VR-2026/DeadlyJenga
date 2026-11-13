#if UNITY_EDITOR
using UnityEngine;

namespace DeadlyJenga.DevTools
{
    public sealed class LivePlayPreview : MonoBehaviour
    {
        public string Message { get; set; }
        public Color Tint { get; set; }
        public float StartedAt { get; private set; }

        void Awake()
        {
            StartedAt = Time.realtimeSinceStartup;
            ApplyLook();
        }

        public void ApplyLook()
        {
            Message = "LIVE PREVIEW / BASELINE";
            Tint = new Color(.44f, .25f, .85f);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var rect = new Rect(18, Screen.height - 60, 280, 38);
            Color previous = GUI.color;
            GUI.color = Tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 12, rect.y + 6, rect.width - 24, 26), Message);
            GUI.color = previous;
        }
    }
}
#endif
