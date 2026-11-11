using System;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Head-locked black veil while the lobby finishes its first heavy frames (island build, room hosting),
    // then a soft fade in. On Quest this hides the stutter instead of showing a jerky world.
    public sealed class LobbyArrivalFade : MonoBehaviour
    {
        const float MaxHold = 4, FadeTime = .45f, SmoothFrame = .045f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        Func<bool> settled;
        Material material;
        float started, fadeStart = -1;
        int smoothFrames;

        public static void Begin(Camera eye, Func<bool> ready)
        {
            if (!eye) return;
            var shared = Resources.Load<Material>("LobbyArrivalFade");
            if (!shared) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Lobby arrival fade";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(eye.transform, false);
            float d = Mathf.Max(eye.nearClipPlane * 1.5f, .06f);
            go.transform.localPosition = Vector3.forward * d;
            go.transform.localScale = Vector3.one * d * 8;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var fade = go.AddComponent<LobbyArrivalFade>();
            fade.material = r.material = new Material(shared);
            fade.settled = ready;
            fade.started = -1;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (started < 0) started = now;   // timed from the first live frame, not the stalled load frame
            if (fadeStart < 0)
            {
                smoothFrames = Time.unscaledDeltaTime < SmoothFrame ? smoothFrames + 1 : 0;
                bool ready = smoothFrames >= 20 && (settled == null || settled());
                if (ready || now - started > MaxHold) fadeStart = now;
                return;
            }
            float t = Mathf.Clamp01((now - fadeStart) / FadeTime);
            material.SetColor(BaseColor, new Color(0, 0, 0, 1 - t * t * (3 - 2 * t)));
            if (t >= 1) Destroy(gameObject);
        }

        void OnDestroy() { if (material) Destroy(material); }
    }
}
