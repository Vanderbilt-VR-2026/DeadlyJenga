using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // When a session starts, Presentation adds its menu, HUD and stamina bar, and runs the VR menu.
    // GameSession never names any of these; it only raises GameEvents.SessionStarted.
    public sealed class SessionPresentation : MonoBehaviour
    {
        ISessionInfo session;
        CastleWorldMenu worldMenu;
        bool shown, lastHand;
        Camera lastCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.SessionStarted += Attach;

        static void Attach(GameObject sessionObject)
        {
            var info = sessionObject.GetComponent<ISessionInfo>();
            if (info == null) return;
            if (!sessionObject.GetComponent<CandyDemoHud>()) sessionObject.AddComponent<CandyDemoHud>();
            if (info.Player != null && !info.Player.gameObject.GetComponent<CandyStaminaHud>()) info.Player.gameObject.AddComponent<CandyStaminaHud>();
            var driver = sessionObject.GetComponent<SessionPresentation>();
            if (!driver) driver = sessionObject.AddComponent<SessionPresentation>();
            driver.Init(info);
        }

        void Init(ISessionInfo info)
        {
            session = info;
            if (!worldMenu) { worldMenu = new GameObject("Headset demo menu").AddComponent<CastleWorldMenu>(); worldMenu.Build(session); }
        }

        // The VR menu appears in front of the active camera while the menu is open in VR.
        void LateUpdate()
        {
            if (session == null || !worldMenu) return;
            bool show = session.MenuOpen && session.IsVR;
            var view = session.ActiveCamera;
            if (show != shown || show && (view != lastCamera || session.PlayingHand != lastHand))
            {
                worldMenu.Show(show && view, view);
                shown = show; lastCamera = view; lastHand = session.PlayingHand;
            }
            if (show && view) worldMenu.Point(view, session.MenuSelectPressed);
        }

        void OnDestroy() { if (worldMenu) Destroy(worldMenu.gameObject); }
    }
}
