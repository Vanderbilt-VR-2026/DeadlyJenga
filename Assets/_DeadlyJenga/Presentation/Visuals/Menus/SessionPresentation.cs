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
        IslandLobbyView lobbyView;
        Camera lastCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.SessionStarted += Attach;

        static void Attach(GameObject sessionObject)
        {
            var info = sessionObject.GetComponent<ISessionInfo>();
            if (info == null) return;
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
            if (!worldMenu) { worldMenu = new GameObject("Headset player tablet").AddComponent<CastleWorldMenu>(); worldMenu.Build(session); }
        }

        // The VR menu appears in front of the active camera while the menu is open in VR.
        void LateUpdate()
        {
            if (session == null || !worldMenu) return;
            bool lobbyAllows=lobbyView ? !lobbyView.WardrobeOpen :
                (session as ILobbyMatchVisitControls)?.VisitingLobby!=true && MatchRuntime.Current?.Frame?.phase!=RoundPhase.Results;
            bool show = lobbyAllows && session.MenuOpen && session.IsVR &&
                !DeadlyJenga.Presentation.MenuAim.InputConsumed &&
                !DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session) &&
                !DeadlyJenga.Presentation.GameExperiencePanel.Showing(session) && !CharacterPicker.Showing(session) &&
                !DeadlyJenga.Presentation.CandyComfortPanel.Showing(session);
            var view = session.ActiveCamera;
            if (show != shown || show && (view != lastCamera || session.PlayingHand != lastHand))
            {
                worldMenu.Show(show && view, view);
                shown = show; lastCamera = view; lastHand = session.PlayingHand;
            }
            if (show && view) worldMenu.Point(view, session.MenuSelectPressed, session.MenuSelectHeld);
        }

        void OnDestroy() { if (worldMenu) Destroy(worldMenu.gameObject); }
    }
}
