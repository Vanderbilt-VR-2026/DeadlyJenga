using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Open animation shared by the VR menus: a quick ease-out pop (scale .9 -> 1) and fade-in whenever the panel is shown.
    // Menus that rewrite their own scale every frame still work: a scale written by someone else becomes the new rest size.
    public sealed class MenuAppear : MonoBehaviour
    {
        const float Duration = .2f, StartScale = .9f;
        bool scale = true, fade = true;
        CanvasGroup group;
        Vector3 rest, applied;
        float started = -1;

        public static MenuAppear Attach(GameObject panel, bool scale = true, bool fade = true)
        {
            if (!panel) return null;
            var appear = panel.GetComponent<MenuAppear>();
            if (appear) return appear;
            bool wasActive = panel.activeInHierarchy;
            appear = panel.AddComponent<MenuAppear>();
            appear.scale = scale;
            appear.fade = fade && !panel.GetComponent<CanvasGroup>();
            if (appear.fade) appear.group = panel.AddComponent<CanvasGroup>();
            if (wasActive) appear.Replay();
            return appear;
        }

        public void Replay()
        {
            if (started < 0) rest = Vector3.zero;   // captured on the next LateUpdate, after the opener has placed and sized the panel
            started = Time.unscaledTime;
            if (group) group.alpha = 0;
        }

        void OnEnable() => Replay();

        void LateUpdate()
        {
            if (started < 0) return;
            var current = transform.localScale;
            if (rest == Vector3.zero || current != applied) rest = current;
            float t = Mathf.Clamp01((Time.unscaledTime - started) / Duration);
            float e = 1 - (1 - t) * (1 - t) * (1 - t);
            if (scale) transform.localScale = applied = rest * Mathf.Lerp(StartScale, 1, e);
            else applied = current;
            if (group) group.alpha = e;
            if (t >= 1) Finish();
        }

        void Finish()
        {
            if (started >= 0 && scale && rest != Vector3.zero) transform.localScale = rest;
            if (group) group.alpha = 1;
            started = -1;
        }

        void OnDisable() => Finish();
    }
}
