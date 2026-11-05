using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(900)]
    public sealed class EmoteOrbit : MonoBehaviour
    {
        readonly CandyOrbitView view = new CandyOrbitView();
        readonly RaycastHit[] hits = new RaycastHit[32];
        ISessionInfo session;
        IPlayableCharacter player;
        Camera original, orbit;
        GameObject mirror;
        RenderTexture preview;
        bool vr;
        float visualHeight;

        void LateUpdate()
        {
            var owner = Services.Session;
            var survivor = owner?.Player;
            var current = survivor?.ViewCamera;
            var emote = survivor as ILocalEmoteView;
            if (session != null && (!orbit || !ReferenceEquals(owner, session) || !ReferenceEquals(player, survivor) ||
                current != original || owner.IsVR != vr || emote?.EmoteActive != true))
                End();

            if (emote?.EmoteActive != true || !current || owner.ActiveCamera != current) return;
            if (!orbit)
            {
                if (!current.enabled) return;
                Begin(owner, survivor, current);
            }

            if (vr)
            {
                Vector2 look = emote.EmoteLook;
                if (look.sqrMagnitude > .04f)
                {
                    view.Yaw += look.x * 65 * Time.unscaledDeltaTime;
                    view.Pitch -= look.y * 40 * Time.unscaledDeltaTime;
                }
            }
            else view.ReadDesktop();

            Vector3 feet = player.transform.position;
            if (vr) feet = new Vector3(original.transform.position.x, feet.y, original.transform.position.z);
            float height = Mathf.Max(visualHeight, Mathf.Clamp(original.transform.position.y - feet.y, .45f, 2.5f));
            Vector3 target = feet + Vector3.up * height * .53f;
            Vector3 offset = view.Position(target, height, out var rotation) - target;
            float range = offset.magnitude;
            Vector3 direction = offset / range;
            int count = Physics.SphereCastNonAlloc(target, .06f, direction, hits, range, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!hit.transform.IsChildOf(player.transform))
                    range = Mathf.Min(range, Mathf.Max(.04f, hit.distance - .07f));
            }

            orbit.transform.SetPositionAndRotation(target + direction * range, rotation);
            bool clear = range > height * .45f;
            orbit.enabled = clear;
            if (vr) mirror.SetActive(clear);
            else original.enabled = !clear;
        }

        void Begin(ISessionInfo owner, IPlayableCharacter survivor, Camera camera)
        {
            session = owner;
            player = survivor;
            original = camera;
            vr = owner.IsVR;
            visualHeight = Mathf.Clamp(camera.transform.position.y - player.transform.position.y, .45f, 2.5f);
            var visual = player.gameObject.GetComponent<IModeActor>()?.Visual;
            if (visual) foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) || !renderer.enabled) continue;
                float top = renderer.bounds.max.y - player.transform.position.y;
                if (float.IsFinite(top)) visualHeight = Mathf.Max(visualHeight, top + .15f);
            }
            view.Reset(camera.transform.eulerAngles.y + 180, 2.65f);

            orbit = new GameObject("Local emote orbit").AddComponent<Camera>();
            orbit.CopyFrom(camera);
            orbit.GetUniversalAdditionalCameraData().allowXRRendering = false;
            orbit.fieldOfView = 45;
            orbit.nearClipPlane = .035f;
            orbit.cullingMask &= ~(1 << 5);
            if (vr) BuildMirror();
        }

        void BuildMirror()
        {
            preview = new RenderTexture(640, 480, 24) { name = "Local emote preview" };
            orbit.targetTexture = preview;
            orbit.aspect = 4f / 3f;

            mirror = new GameObject("Local emote VR mirror", typeof(RectTransform), typeof(Canvas));
            mirror.layer = 5;
            var canvas = mirror.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = original;
            var screen = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            screen.layer = 5;
            screen.transform.SetParent(mirror.transform, false);
            var image = screen.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = preview;
            image.rectTransform.sizeDelta = new Vector2(640, 480);
            var panel = (RectTransform)mirror.transform;
            panel.sizeDelta = image.rectTransform.sizeDelta;
            panel.localScale = Vector3.one * .00145f;
            Vector3 forward = Vector3.ProjectOnPlane(original.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = player.transform.forward;
            mirror.transform.SetPositionAndRotation(original.transform.position + forward * 1.25f, Quaternion.LookRotation(forward));
        }

        void End()
        {
            if (orbit)
            {
                orbit.enabled = false;
                orbit.targetTexture = null;
                Destroy(orbit.gameObject);
            }
            if (mirror) { mirror.SetActive(false); Destroy(mirror); }
            if (preview) { preview.Release(); Destroy(preview); }
            if (!vr && original && ReferenceEquals(session, Services.Session) &&
                session.ActiveCamera == original && !session.PlayingHand &&
                (session.Demo as ISpectatorControls)?.Spectating != true)
                original.enabled = true;
            orbit = original = null;
            mirror = null;
            preview = null;
            session = null;
            player = null;
        }

        void OnDisable() => End();
        void OnApplicationPause(bool paused) { if (paused) End(); }
    }
}
