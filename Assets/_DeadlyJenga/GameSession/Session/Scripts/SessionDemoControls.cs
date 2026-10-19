using System;
using System.Collections.Generic;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // ISessionInfo.Demo: bots and disasters for the menus (Presentation). Joined players' requests go to
    // the host through CastleDemoCommands; their counts come from the host's shared demo state.
    public sealed class SessionDemoControls : IDemoControls, IBuildUpControls, IDemoMapControls, ISpectatorControls
    {
        readonly CastlePlaySession session;
        public SessionDemoControls(CastlePlaySession owner) { session = owner; }

        CastleFusionSession Net => session.GetComponent<CastleFusionSession>();
        CastleFusionSession.DemoState Remote { get { var net = Net; return net && !net.IsHost ? net.Demo : null; } }

        public bool DemoMode => MatchSetup.ModeId == "demo";
        public bool CanSpectate => session.Modes && (DemoMode || session.CanEditWorld);
        public bool Spectating => session.Modes && session.Modes.DemoSpectating;
        public bool FourViews => session.Modes && session.Modes.DemoFourViews;
        public bool CanUseFourViews => CanSpectate && !session.IsVR;
        public string WatchedName(int slot) => session.Modes ? session.Modes.DemoWatchedName(slot) : "";
        public UnityEngine.Transform WatchedRoot(UnityEngine.Camera camera) => session.Modes ? session.Modes.SpectatorRoot(camera) : null;
        public void SetSpectating(bool active) { if (!active || CanSpectate) session.Modes?.SetDemoSpectating(active); }
        public void SetFourViews(bool active) { if (!active || CanUseFourViews) session.Modes?.SetDemoFourViews(active); }
        public void CycleWatched(int step) => session.Modes?.CycleDemoWatched(step);
        public bool CanCommand => session.CanEditWorld;
        IslandLobbyCatalog MapCatalog => IslandRoomConnection.Instance && IslandRoomConnection.Instance.Catalog ? IslandRoomConnection.Instance.Catalog : IslandLobbyCatalog.Runtime(UnityEngine.Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog"));
        public IReadOnlyList<LobbyOption> Maps
        {
            get
            {
                var result = new List<LobbyOption>();
                if (MapCatalog) foreach (var map in MapCatalog.maps)
                    if (map != null) result.Add(new LobbyOption { id = map.id, title = map.title, available = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(map.scenePath) >= 0 });
                return result;
            }
        }
        public string CurrentMapId => MatchSetup.MapId;
        public bool CanSelectMap => session.HasWorldAuthority && (!IslandRoomConnection.Instance || (!IslandRoomConnection.Instance.Busy && !IslandRoomConnection.Instance.SwitchingMap));
        public string MapStatus => IslandRoomConnection.Instance && IslandRoomConnection.Instance.SwitchingMap ? "Waiting for connected players..." : "Changing maps restarts the map with the current mode. Host only.";
        public void SelectMap(string id)
        {
            if (!CanSelectMap || id == CurrentMapId || !MapCatalog) return;
            var map = Array.Find(MapCatalog.maps, value => value.id == id);
            if (map == null || UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(map.scenePath) < 0) return;
            var room = IslandRoomConnection.Instance;
            if (room && room.Connected) { room.SwitchGameMap(id); return; }
            if (Net && Net.IsHost) return;
            MatchSetup.MapId = id; MatchSetup.FromLobby = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(map.scenePath));
        }
        public string Status => DemoDisasters.Status;
        IBuildUpControls BuildUp => Services.World as IBuildUpControls;
        public bool Available => DemoMode && BuildUp?.Available == true;
        public int Stage => BuildUp?.Stage ?? 0;
        public int StageCount => BuildUp?.StageCount ?? 0;
        public bool Delivering => BuildUp?.Delivering ?? false;
        public float ClimbSecondsRemaining => BuildUp?.ClimbSecondsRemaining ?? 0;
        public bool CanAdvance => Available && CanCommand && !Delivering && ClimbSecondsRemaining <= 0 && Stage < StageCount - 1;
        public void AdvanceStage() { if (CanAdvance) CastleDemoCommands.Run(session, CastleDemoAction.AdvanceScaffold); }

        public void Run(DemoCommand command)
        {
            switch (command)
            {
                case DemoCommand.Rebuild: session.ResetMap(); break;
                case DemoCommand.AddSurvivor: CastleDemoCommands.Run(session, CastleDemoAction.AddSurvivor); break;
                case DemoCommand.FillSurvivors: CastleDemoCommands.Run(session, CastleDemoAction.FillSurvivors); break;
                case DemoCommand.NextSurvivor: CastleDemoCommands.Run(session, CastleDemoAction.NextSurvivor); break;
                case DemoCommand.ClearSurvivors: CastleDemoCommands.Run(session, CastleDemoAction.ClearSurvivors); break;
                case DemoCommand.AddHand: CastleDemoCommands.Run(session, CastleDemoAction.AddHand); break;
                case DemoCommand.NextHand: CastleDemoCommands.Run(session, CastleDemoAction.NextHand); break;
                case DemoCommand.ClearHands: CastleDemoCommands.Run(session, CastleDemoAction.ClearHands); break;
                case DemoCommand.StopDisasters: CastleDemoCommands.Run(session, CastleDemoAction.StopDisasters); break;
            }
        }

        public int SurvivorCount => Remote != null ? Remote.survivors : session.SurvivorBots?.Count ?? 0;
        public int SurvivorTarget => Remote != null ? Remote.target : session.SurvivorBots?.TargetPopulation ?? 0;
        public string SurvivorPersonality => Remote != null ? Remote.survivorName : session.SurvivorBots?.SelectedName ?? "";
        public string SurvivorStatus => session.SurvivorBots?.Status ?? "";
        public int HandCount => Remote != null ? Remote.hands : session.HandBots?.Count ?? 0;
        public string HandPersonality => Remote != null ? Remote.handName : session.HandBots?.SelectedName ?? "";
        public string HandStatus => session.HandBots?.Status ?? "";

        public void ApplyStaminaRules(string json) => CastleDemoCommands.Run(session, CastleDemoAction.Stamina, json);

        public int DisasterCount => DemoDisasters.Prefabs.Length;
        UnityEngine.GameObject Prefab(int index) { var all = DemoDisasters.Prefabs; return index >= 0 && index < all.Length ? all[index] : null; }
        public string DisasterLabel(int index) => DemoDisasters.Label(Prefab(index));
        public bool DisasterIsToggle(int index) { var p = Prefab(index); return p && (p.GetComponent<IDemoHazard>()?.Toggle ?? false); }
        public IReadOnlyList<DisasterParameter> DisasterParameters(int index) { var p = Prefab(index); return p ? DemoDisasters.Parameters(p) : Array.Empty<DisasterParameter>(); }
        public float[] DisasterValues(int index) { var p = Prefab(index); return p ? DemoDisasters.Values(session, p) : new float[0]; }
        public void SetDisasterValue(int index, int parameter, float value) { var p = Prefab(index); if (p) DemoDisasters.SetValue(session, p, parameter, value); }
        public string LaunchDisaster(int index) { var p = Prefab(index); return p ? DemoDisasters.Request(session, p) : "Missing disaster."; }
    }
}
