using System;
using System.Collections.Generic;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // ISessionInfo.Demo: bots and disasters for the menus (Presentation). Joined players' requests go to
    // the host through CastleDemoCommands; their counts come from the host's shared demo state.
    public sealed class SessionDemoControls : IDemoControls
    {
        readonly CastlePlaySession session;
        public SessionDemoControls(CastlePlaySession owner) { session = owner; }

        CastleFusionSession Net => session.GetComponent<CastleFusionSession>();
        CastleFusionSession.DemoState Remote { get { var net = Net; return net && !net.IsHost ? net.Demo : null; } }

        public bool DemoMode => MatchSetup.ModeId == "demo";
        public bool CanCommand => session.CanEditWorld;
        public string Status => DemoDisasters.Status;

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
        public int HandCount => Remote != null ? Remote.hands : ((ISessionInfo)session).HandBots?.Count ?? 0;
        public string HandPersonality => Remote != null ? Remote.handName : ((ISessionInfo)session).HandBots?.SelectedName ?? "";
        public string HandStatus => ((ISessionInfo)session).HandBots?.Status ?? "";

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
