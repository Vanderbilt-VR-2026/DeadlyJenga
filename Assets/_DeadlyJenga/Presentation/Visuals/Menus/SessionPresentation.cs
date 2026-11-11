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
        CandyDemoHud demoHud;
        bool shown, lastHand, lastVr;
        IslandLobbyView lobbyView;
        Camera lastCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.SessionStarted += Attach;

        static void Attach(GameObject sessionObject)
        {
            var info = sessionObject.GetComponent<ISessionInfo>();
            if (info == null) return;
            if (!sessionObject.GetComponent<DeadlyJenga.NinjaArt.NinjaLanternView>()) sessionObject.AddComponent<DeadlyJenga.NinjaArt.NinjaLanternView>();
            if (!sessionObject.GetComponent<CandyDemoHud>()) sessionObject.AddComponent<CandyDemoHud>();
            if (!sessionObject.GetComponent<DeadlyJenga.CandyArt.ScaffoldPresentation>()) sessionObject.AddComponent<DeadlyJenga.CandyArt.ScaffoldPresentation>();
            if (info.Player != null && !info.Player.gameObject.GetComponent<CandyStaminaHud>()) info.Player.gameObject.AddComponent<CandyStaminaHud>();
            if(info.Player!=null&&!info.Player.gameObject.GetComponent<PowerupHud>())info.Player.gameObject.AddComponent<PowerupHud>();
            var driver = sessionObject.GetComponent<SessionPresentation>();
            if (!driver) driver = sessionObject.AddComponent<SessionPresentation>();
            driver.Init(info);
        }

        internal static void AttachLobbyTablet(IslandLobbyView lobby, ISessionInfo info)
        {
            var driver=lobby.GetComponent<SessionPresentation>();
            if(!driver) driver=lobby.gameObject.AddComponent<SessionPresentation>();
            driver.lobbyView=lobby;
            driver.Init(info);
        }

        void Init(ISessionInfo info)
        {
            session = info;
            demoHud=GetComponent<CandyDemoHud>();
            if (!worldMenu) { worldMenu = new GameObject("Headset player tablet").AddComponent<CastleWorldMenu>(); worldMenu.Build(session); }
        }

        internal bool TabletVisible => shown && worldMenu && worldMenu.isActiveAndEnabled;
        void LateUpdate()
        {
            if (session == null || !worldMenu) return;
            bool lobbyAllows=lobbyView ? !lobbyView.WardrobeOpen :
                (session as ILobbyMatchVisitControls)?.VisitingLobby!=true && MatchRuntime.Current?.Frame?.phase!=RoundPhase.Results;
            bool vr = session.IsVR;
            bool show = lobbyAllows && session.MenuOpen && (vr || !lobbyView) &&
                (vr || !demoHud || !demoHud.StudioOpen) &&
                !DeadlyJenga.Presentation.MenuAim.InputConsumed &&
                !DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session) &&
                !DeadlyJenga.Presentation.GameExperiencePanel.Showing(session) && !CharacterPicker.Showing(session) &&
                !DeadlyJenga.Presentation.CandyComfortPanel.Showing(session);
            var view = session.ActiveCamera;
            if (show != shown || show && (view != lastCamera || session.PlayingHand != lastHand || vr != lastVr))
            {
                worldMenu.Show(show && view, view);
                shown = show && view; lastCamera = view; lastHand = session.PlayingHand; lastVr = vr;
            }
            if (show && view) worldMenu.Point(view, session.MenuSelectPressed, session.MenuSelectHeld);
        }

        void OnDestroy() { if (worldMenu) Destroy(worldMenu.gameObject); }
    }
}
