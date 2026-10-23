using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    // A separate view leaves the avatar, its camera and headset tracking untouched.
    [DefaultExecutionOrder(900)]
    public sealed class CandyThirdPersonInspection : MonoBehaviour
    {
        ISessionInfo session;
        IPlayableCharacter player;
        Camera orbit, original;
        RenderTexture texture;
        GameObject panel;
        readonly RectTransform[] controls = new RectTransform[7];
        readonly RaycastHit[] obstacles = new RaycastHit[32];
        bool originalEnabled, suspended, vr;
        InputAction mirrorSelect;
        bool selectArmed;
        readonly CandyOrbitView orbitView = new CandyOrbitView();
        float yaw { get => orbitView.Yaw; set => orbitView.Yaw = value; }
        float pitch { get => orbitView.Pitch; set => orbitView.Pitch = value; }
        float distance { get => orbitView.Distance; set => orbitView.Distance = value; }
        public bool Inspecting { get; private set; }
        public Camera InspectionCamera => orbit;

        public static CandyThirdPersonInspection For(ISessionInfo owner)
        {
            if (!(owner is Component component)) return null;
            var driver = component.GetComponent<CandyThirdPersonInspection>();
            if (!driver) driver = component.gameObject.AddComponent<CandyThirdPersonInspection>();
            driver.session = owner;
            return driver;
        }

        public bool CanInspect => session?.Demo?.DemoMode == true && !session.PlayingHand &&
            session.Player != null && (session.Demo as ISpectatorControls)?.Spectating != true &&
            MatchRuntime.Current?.Frame.phase != RoundPhase.Results;

        public void Begin()
        {
            if (Inspecting || !CanInspect) return;
            player = session.Player; original = player.ViewCamera;
            if (!original) return;
            session.SetMenu(false);
            suspended = player.InputSuspended; player.InputSuspended = true;
            vr = session.IsVR; originalEnabled = original.enabled;
            orbitView.Reset(original.transform.eulerAngles.y + 180);
            var go = new GameObject("Visual - third person inspection camera");
            orbit = go.AddComponent<Camera>(); orbit.CopyFrom(original);
            // Camera.CopyFrom does not copy URP's additional component settings.
            orbit.GetUniversalAdditionalCameraData().allowXRRendering=false;
            orbit.fieldOfView = 45; orbit.nearClipPlane = .025f;
            orbit.cullingMask &= ~(1 << 5); // The mirror never photographs itself or menu canvases.
            texture = new RenderTexture(vr ? 1024 : Mathf.Min(1920, Mathf.Max(640, Screen.width)),
                vr ? 768 : Mathf.Min(1080, Mathf.Max(480, Screen.height)), 24);
            texture.name = "Third person inspection"; texture.Create();
            orbit.targetTexture = texture; orbit.aspect = (float)texture.width / texture.height; orbit.enabled = true;
            if (vr)
            {
                mirrorSelect=new InputAction("Inspection mirror select",InputActionType.Button,"<XRController>{RightHand}/trigger");
                mirrorSelect.Enable();selectArmed=false;BuildMirror();
            }
            else original.enabled = false;
            Inspecting = true;
        }

        public void End()
        {
            if (!Inspecting && !orbit) return;
            Inspecting = false;
            bool spectating=(session?.Demo as ISpectatorControls)?.Spectating==true;
            if (original && !vr && ReferenceEquals(session?.ActiveCamera,original) && session?.PlayingHand!=true && !spectating)
                original.enabled = originalEnabled;
            if (player != null && NetworkActors.Alive(player))
                player.InputSuspended = suspended || session?.MenuOpen == true || spectating;
            if (orbit) { orbit.targetTexture = null; Destroy(orbit.gameObject); }
            if (panel) Destroy(panel);
            if (texture) { texture.Release(); Destroy(texture); }
            mirrorSelect?.Dispose();mirrorSelect=null;
            orbit = null; texture = null; panel = null; player = null; original = null;
        }

        void Update()
        {
            if (!Inspecting) return;
            if (!CanInspect || session.MenuOpen || session.IsVR != vr ||
                !NetworkActors.Alive(player) || !ReferenceEquals(player, session.Player) || original != player.ViewCamera)
            { End(); return; }
            var runtime = MatchRuntime.Current;
            if (runtime?.Frame != null) foreach (var row in runtime.Frame.players)
                if (row.id == runtime.LocalActorId && (!row.alive || row.dying || row.role == "spectator")) { End(); return; }
            player.InputSuspended = true;
            if (vr) { if(!MirrorSelectHeld)selectArmed=true;return; }
            var keys = Keyboard.current; var mouse = Mouse.current;
            if (keys?.vKey.wasPressedThisFrame == true) { End(); return; }
            orbitView.ReadDesktop();
        }

        void LateUpdate()
        {
            if (!Inspecting || !orbit || player == null) return;
            pitch = Mathf.Clamp(pitch, -30, 70); distance = Mathf.Clamp(distance, 1.1f, 7);
            float height = Mathf.Clamp(original.transform.position.y - player.transform.position.y, .45f, 3);
            Vector3 target = player.transform.position + Vector3.up * height * .58f;
            Vector3 desired = orbitView.Position(target, height, out var rotation);
            Vector3 direction = (desired - target).normalized;
            float radius = Vector3.Distance(target, desired);
            // Only solid scenery can shorten the view; never snap to an avatar trigger or the mirror.
            int hitCount=Physics.SphereCastNonAlloc(target,.04f,direction,obstacles,radius,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<hitCount;i++)
            {
                var hit=obstacles[i];
                if (!hit.transform.IsChildOf(player.transform))
                    radius = Mathf.Min(radius, Mathf.Max(.035f, hit.distance - .06f));
            }
            orbit.transform.SetPositionAndRotation(target + direction * radius, Quaternion.LookRotation(-direction));
            if (vr) PointMirror();
        }

        void BuildMirror()
        {
            panel = new GameObject("Visual - third person VR mirror", typeof(RectTransform), typeof(Canvas));
            panel.layer = 5;
            var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = original;
            var rect = panel.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1024, 900); rect.localScale = Vector3.one * .0013f;
            Vector3 forward = Vector3.ProjectOnPlane(original.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            panel.transform.SetPositionAndRotation(original.transform.position + forward * 1.65f, Quaternion.LookRotation(forward));
            var image = new GameObject("Mirror", typeof(RectTransform), typeof(RawImage)); image.layer = 5; image.transform.SetParent(panel.transform, false);
            var preview = image.GetComponent<RawImage>(); preview.texture = texture; preview.raycastTarget = false;
            preview.rectTransform.sizeDelta = new Vector2(1024, 768); preview.rectTransform.anchoredPosition = new Vector2(0, 55);
            string[] names = { "Left", "Right", "Up", "Down", "Closer", "Further", "Done" };
            for (int i = 0; i < names.Length; i++)
            {
                var button = new GameObject(names[i], typeof(RectTransform), typeof(Image)); button.layer = 5; button.transform.SetParent(panel.transform, false);
                var area = button.GetComponent<RectTransform>(); area.sizeDelta = new Vector2(138, 66); area.anchoredPosition = new Vector2(-444 + i * 148, -375); controls[i] = area;
                button.GetComponent<Image>().color = CandyHudVisuals.Ink;
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text)); label.layer = 5; label.transform.SetParent(button.transform, false);
                var text = label.GetComponent<Text>(); text.text = names[i]; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 26; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
                text.rectTransform.sizeDelta = area.sizeDelta;
            }
        }

        void PointMirror()
        {
            foreach(var control in controls)control.GetComponent<Image>().color=CandyHudVisuals.Ink;
            var ray = new Ray(original.transform.position, original.transform.forward);
            var plane = new Plane(panel.transform.forward, panel.transform.position);
            if (!plane.Raycast(ray, out float enter) || enter > 4) return;
            Vector3 point = panel.transform.InverseTransformPoint(ray.GetPoint(enter));
            for (int i = 0; i < controls.Length; i++)
            {
                bool hover = controls[i].rect.Contains((Vector2)point - controls[i].anchoredPosition);
                controls[i].GetComponent<Image>().color = hover ? CandyHudVisuals.Mint : CandyHudVisuals.Ink;
                if (!hover) continue;
                if(!selectArmed)return;
                if (i == 6) { if (mirrorSelect?.WasPressedThisFrame()==true||player.SelectPressed) End(); return; }
                if (!MirrorSelectHeld) return;
                float step = Time.unscaledDeltaTime;
                if (i < 2) yaw += (i == 0 ? -1 : 1) * 65 * step;
                else if (i < 4) pitch += (i == 2 ? 1 : -1) * 40 * step;
                else distance += (i == 4 ? -1 : 1) * step;
                return;
            }
        }
        bool MirrorSelectHeld=>mirrorSelect?.IsPressed()==true||player?.SelectHeld==true;

        void OnGUI()
        {
            if (!Inspecting || vr || !texture) return;
            int old = GUI.depth; GUI.depth = -90;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texture, ScaleMode.ScaleToFit);
            GUI.Box(new Rect(18, 18, Mathf.Min(Screen.width - 36, 760), 54), "THIRD PERSON  ·  Drag mouse / arrows: orbit  ·  Scroll / +/-: zoom  ·  V: return  ·  Esc: menu");
            GUI.depth = old;
        }
        void OnDisable() => End();
        void OnDestroy() => End();
        void OnApplicationPause(bool paused) { if(paused)End(); }
    }
}
