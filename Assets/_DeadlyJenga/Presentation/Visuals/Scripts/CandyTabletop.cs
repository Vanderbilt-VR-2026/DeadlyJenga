using System.Collections.Generic;
using DeadlyJenga.Core;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;

namespace DeadlyJenga.Presentation
{
    // Reprojects the local observer, never the shared map or its physics.
    [DefaultExecutionOrder(1200)]
    public sealed class CandyTabletop : MonoBehaviour
    {
        public static CandyTabletop Current { get; private set; }
        public bool Enabled { get; private set; }
        public bool Placement { get; private set; }
        public bool Passthrough { get; private set; }
        public float Width = 1.2f, Height = .75f, Forward = .8f, Side;
        public string Status { get; private set; } = "Tabletop is off";
        XROrigin origin; Camera camera; ARCameraManager cameraManager;
        ARSession arSession; Vector3 oldScale,oldPosition;Quaternion oldRotation;bool ownsManager,oldManagerEnabled; CameraClearFlags oldClear; Color oldBackground;
        readonly List<Renderer> hidden = new List<Renderer>();
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => GameEvents.SessionStarted += Attach;
        static void Attach(GameObject root) { if (!root.GetComponent<CandyTabletop>()) root.AddComponent<CandyTabletop>(); }
        void Awake() => Current = this;
        public void Begin()
        {
            var spirit = Services.Session as ISeaSpiritControls;
            if (spirit == null) { Status = "Sea spirit controls unavailable"; return; }
            spirit.SetSeaSpirit(true);
            if(!spirit.SeaSpiritActive){Status="Host permission and a living survivor are required for sea spirit.";return;}
            camera = Services.Session.ActiveCamera;
            origin = camera ? camera.GetComponentInParent<XROrigin>() : null;
            if (!origin) { Status = "Tracked observer origin unavailable"; return; }
            if (!Enabled) { oldScale = origin.transform.localScale;oldPosition=origin.transform.position;oldRotation=origin.transform.rotation; oldClear = camera.clearFlags; oldBackground = camera.backgroundColor; }
            Enabled = Placement = true;
            SetPassthrough(true);
        }
        public void Confirm() { if (!Enabled) return; Placement = false; Status = "Board placed · recenter after changing your tracking origin"; }
        public void SetPassthrough(bool on)
        {
            if (!camera) return;
            displays.Clear(); SubsystemManager.GetSubsystems(displays);
            bool running = displays.Exists(d => d.running);
            Passthrough = on && running && Application.platform == RuntimePlatform.Android;
            if (Passthrough)
            {
                if (!arSession) arSession = new GameObject("Tabletop AR session").AddComponent<ARSession>();
                cameraManager = camera.GetComponent<ARCameraManager>();
                if (!cameraManager){cameraManager = camera.gameObject.AddComponent<ARCameraManager>();ownsManager=true;}else oldManagerEnabled=cameraManager.enabled;
                cameraManager.enabled = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                Status = "Passthrough requested · adjust board height and position, then confirm";
            }
            else
            {
                if (cameraManager) cameraManager.enabled = oldManagerEnabled;
                camera.clearFlags = oldClear; camera.backgroundColor = oldBackground;
                Status = on ? "Passthrough requires a Quest Android build; tabletop preview remains available" : "Game background";
            }
            HideOcean(Passthrough);
        }
        void HideOcean(bool hide)
        {
            foreach (var r in hidden) if (r) r.enabled = true; hidden.Clear();
            if (!hide) return;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.enabled && r.name.IndexOf("Ocean", System.StringComparison.OrdinalIgnoreCase) >= 0)
                { r.enabled = false; hidden.Add(r); }
        }
        public void End()
        {
            SetPassthrough(false); Enabled = Placement = false;
            if (origin){origin.transform.localScale = oldScale;origin.transform.SetPositionAndRotation(oldPosition,oldRotation);}
            if(ownsManager&&cameraManager){Destroy(cameraManager);cameraManager=null;ownsManager=false;}
            if (arSession) Destroy(arSession.gameObject);
            Status = "Tabletop is off";
        }
        void LateUpdate()
        {
            if (!Enabled || !origin || Services.World == null) return;
            if (!(Services.Session is ISeaSpiritControls s) || !s.SeaSpiritActive) { End(); return; }
            float diameter = Mathf.Max(Services.World.PlayableBounds.size.x, Services.World.PlayableBounds.size.z);
            float scale = Mathf.Clamp(diameter / Mathf.Max(.3f, Width), 1, 1000);
            origin.transform.localScale = Vector3.one * scale;
            var physicalAnchor = new Vector3(Side, Height, Forward);
            origin.transform.position = Services.World.Center - origin.transform.rotation * (physicalAnchor * scale);
        }
        public static void Draw()
        {
            var t = Current; if (!t) return;
            if (GUILayout.Button(t.Enabled ? "Exit tabletop placement" : "Place island on a table (sea spirit)")) { if (t.Enabled) t.End(); else t.Begin(); }
            if (!t.Enabled) return;
            GUILayout.Label(t.Status);
            if (GUILayout.Button(t.Passthrough ? "Use game background" : "Use Quest passthrough")) t.SetPassthrough(!t.Passthrough);
            GUILayout.Label("Board width " + t.Width.ToString("0.0") + " m"); t.Width = GUILayout.HorizontalSlider(t.Width, .4f, 3);
            GUILayout.Label("Table height " + t.Height.ToString("0.00") + " m"); t.Height = GUILayout.HorizontalSlider(t.Height, 0, 1.8f);
            GUILayout.Label("Forward distance"); t.Forward = GUILayout.HorizontalSlider(t.Forward, .2f, 3);
            GUILayout.Label("Left / right"); t.Side = GUILayout.HorizontalSlider(t.Side, -2, 2);
            if (t.Placement && GUILayout.Button("Confirm board placement")) t.Confirm();
        }
        void OnDisable(){if(Enabled)End();}
        void OnDestroy() { if (Enabled) End(); if (Current == this) Current = null; }
    }
}
