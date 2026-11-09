using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyHitConfirm : MonoBehaviour
    {
        const float Duration = .22f;
        const int MarkerCount = 16;

        sealed class Marker
        {
            public CanvasGroup view;
            public Vector3 point;
            public float expires;
        }

        readonly Marker[] markers = new Marker[MarkerCount];
        Canvas canvas;
        Camera eye;
        bool vr;
        int nextMarker;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.SessionStarted += go =>
        {
            if (!go.GetComponent<CandyHitConfirm>()) go.AddComponent<CandyHitConfirm>();
        };

        void OnEnable() => GameEvents.MatchSound += Confirm;

        void OnDisable()
        {
            GameEvents.MatchSound -= Confirm;
            if (canvas) canvas.enabled = false;
            foreach (var marker in markers)
                if (marker != null) marker.expires = 0;
        }

        void OnDestroy()
        {
            if (canvas) Destroy(canvas.gameObject);
        }

        void Confirm(MatchSoundCue cue,Vector3 point)
        {
            if(cue!=MatchSoundCue.HitConfirm&&cue!=MatchSoundCue.KillConfirm)return;
            if (!canvas) BuildMarkers();
            var marker = markers[nextMarker];
            nextMarker = (nextMarker + 1) % markers.Length;
            marker.point = point;
            marker.expires = Time.unscaledTime + Duration;
        }

        void BuildMarkers()
        {
            var root = new GameObject("Hit confirmations", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.sortingOrder = 68;
            for (int i = 0; i < markers.Length; i++)
            {
                var go = new GameObject("Hit X", typeof(RectTransform), typeof(CanvasGroup));
                go.transform.SetParent(root.transform, false);
                go.SetActive(false);
                markers[i] = new Marker { view = go.GetComponent<CanvasGroup>() };
                Stroke(go.transform, 45);
                Stroke(go.transform, -45);
            }
        }

        static void Stroke(Transform parent, float angle)
        {
            var go = new GameObject("Stroke", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(23, 2.5f);
            rect.localRotation = Quaternion.Euler(0, 0, angle);
            go.GetComponent<Image>().raycastTarget = false;
        }

        void LateUpdate()
        {
            if (!canvas) return;
            var session = Services.Session;
            var camera = session?.ActiveCamera;
            canvas.enabled = camera && camera.isActiveAndEnabled && !session.MenuOpen;
            if (!canvas.enabled) return;
            if (eye != camera || vr != session.IsVR)
            {
                eye = camera;
                vr = session.IsVR;
                CandyHudLayout.Configure(canvas, transform, eye, vr);
            }

            var root = (RectTransform)canvas.transform;
            foreach (var marker in markers)
            {
                float remaining = marker.expires - Time.unscaledTime;
                Vector2 position = default;
                bool visible = remaining > 0 && Project(marker.point, root, out position);
                if (marker.view.gameObject.activeSelf != visible) marker.view.gameObject.SetActive(visible);
                if (!visible) continue;
                ((RectTransform)marker.view.transform).anchoredPosition = position;
                marker.view.alpha = Mathf.Clamp01(remaining / .11f);
            }
        }

        bool Project(Vector3 point, RectTransform root, out Vector2 position)
        {
            position = default;
            var viewport = eye.WorldToViewportPoint(point);
            if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1)
                return false;
            if (vr)
            {
                var local = eye.transform.InverseTransformPoint(point);
                position = (Vector2)local * (root.localPosition.z / (local.z * root.localScale.x));
            }
            else
            {
                position = (eye.WorldToScreenPoint(point) - new Vector3(Screen.width, Screen.height) * .5f) /
                    canvas.scaleFactor;
            }
            return true;
        }
    }
}
