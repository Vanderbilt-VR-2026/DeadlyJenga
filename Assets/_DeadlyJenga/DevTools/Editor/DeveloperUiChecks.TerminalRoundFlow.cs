using System;
using System.Collections;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt.Marina;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class DeveloperUiChecks
    {
        // Drives production bindings; this does not simulate headset/controller input.
        static void TerminalAction(MarinaScreen screen,string id)
        {
            Call(screen,"Refresh");
            var target=((IEnumerable)Field<object>(screen,"targets")).Cast<object>().Single(t=>Field<string>(t,"id")==id);
            Check(Field<Image>(target,"face").gameObject.activeInHierarchy,"Terminal action visible: "+id);
            Check(Field<Func<bool>>(target,"allowed")(),"Terminal action allowed: "+id);
            Field<Action>(target,"action")();
        }
        static IEnumerator TerminalUntil(Func<bool> ready,string label,float seconds=90)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!ready()&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(ready(),label);
        }
        static IEnumerator TerminalRoundFlowChecks()
        {
            var lobby=Services.Session as IslandLobby;
            Check(lobby,"Actual Marina lobby initializes");
            GameExperiencePanel.Resolve(lobby)?.SetExperience(GameExperience.Demo);
            Check(lobby.SelectMap("castle")&&lobby.SelectMode("demo"),"Developer lobby accepts initial map and mode");
            var setup=JsonUtility.FromJson<LobbySetup>(JsonUtility.ToJson(((ILobbyDetails)lobby).Setup));
            setup.population=1;setup.hands=0;setup.rules.automaticDisasters=false;setup.rules.victorySeconds=2;
            ((ILobbyDetails)lobby).ConfigureSetup(setup);
            var screen=lobby.GetComponent<MarinaScreen>();
            TerminalAction(screen,"marina-play");
            yield return TerminalUntil(()=>Services.Session is CastlePlaySession s&&s.Modes&&s.Modes.Frame.phase==RoundPhase.Playing,"Actual terminal launches a playable developer match");
            var session=(CastlePlaySession)Services.Session;
            var match=(IMatchRuntime)session.Modes;
            session.SetMenu(false);
            yield return TerminalUntil(()=>(match as IMatchReviewControls)?.CanEndGame==true&&session.CanVisitLobby,"Real developer policy authorizes End Match and lobby visit");
            session.VisitLobby();
            yield return TerminalUntil(()=>session.VisitingLobby,"Actual VisitLobby opens during the running match");
            var visit=IslandLobby.Persistent;
            screen=visit.GetComponent<MarinaScreen>();
            Call(screen,"Refresh");yield return null;
            Check(Field<Text>(screen,"mainHeading").text.StartsWith("LIVE MATCH"),"Running terminal shows live phase");
            CaptureTerminal(Field<RectTransform>(screen,"mainFace"),"80-developer-live-terminal.png");
            TerminalAction(screen,"marina-match-action");
            Check(match.Frame.phase==RoundPhase.Playing,"First End Match click preserves the running round");
            Check(Field<Text>(screen,"matchActionTitle").text=="Confirm end match","End Match asks for confirmation");
            TerminalAction(screen,"marina-match-action");
            Check(match.Frame.phase==RoundPhase.Victory,"Confirmed terminal command ends the actual round");
            Call(screen,"Refresh");
            Check(Field<Text>(screen,"mainHeading").text=="ROUND COMPLETE","Victory does not retain a live-match heading");
            yield return TerminalUntil(()=>match.Frame.phase==RoundPhase.Results,"Actual simulation advances from victory to results");
            Call(screen,"Refresh");yield return null;
            Check(Field<Text>(screen,"mainHeading").text=="MATCH RESULTS","Results heading replaces live timer");
            Check(Field<Text>(screen,"liveStandings").text=="FINAL STANDINGS","Terminal identifies final standings");
            Check(!Field<Text>(screen,"liveHazards").gameObject.activeSelf&&Field<Text>(screen,"matchSummary").gameObject.activeSelf,"Result summary replaces active-disaster strip");
            Check(((IPostGameResults)session).LatestReport!=null,"Real end command preserves post-game report");
            CaptureTerminal(Field<RectTransform>(screen,"mainFace"),"81-results-terminal.png");
            var original=MatchRuntime.Current;
            try
            {
                MatchRuntime.Current=new TerminalGuestView(original);Call(screen,"Refresh");
                var target=((IEnumerable)Field<object>(screen,"targets")).Cast<object>().Single(t=>Field<string>(t,"id")=="marina-match-action");
                Check(!Field<Func<bool>>(target,"allowed")(),"Guest-view fixture cannot configure the host's next match");
                Check(Field<Text>(screen,"matchActionTitle").text=="Host sets next match","Guest-view fixture explains host ownership");
            }
            finally {MatchRuntime.Current=original;Call(screen,"Refresh");}
            TerminalAction(screen,"marina-match-action");
            yield return TerminalUntil(()=>Services.Session is IslandLobby l&&!l.VisitingLobby&&l.CanConfigure,"Configure Next Match returns to editable lobby setup");
            lobby=(IslandLobby)Services.Session;
            screen=lobby.GetComponent<MarinaScreen>();
            var view=lobby.GetComponent<IslandLobbyView>();
            TerminalAction(screen,"marina-modes");
            Check(Field<string>(view,"setupPage")=="modes","Mode picker remains reachable after a match ends");
            Check(lobby.SelectMode("hill"),"New match can select a different mode");
            Call(view,"CloseSetupPage");
            TerminalAction(screen,"marina-maps");
            Check(Field<string>(view,"setupPage")=="maps","Map picker remains reachable after a match ends");
            var nextMap=((ILobbyDetails)lobby).Maps.First(m=>m.id!="castle");
            Check(lobby.SelectMap(nextMap.id),"New match can select a different map: "+nextMap.id);
            Call(view,"CloseSetupPage");
            TerminalAction(screen,"marina-settings");
            Check(Field<string>(view,"setupPage")=="settings","Match settings remain reachable after a match ends");
            setup=JsonUtility.FromJson<LobbySetup>(JsonUtility.ToJson(((ILobbyDetails)lobby).Setup));
            setup.population=2;setup.hands=0;setup.rules.countdownSeconds=12;setup.rules.automaticDisasters=false;setup.rules.roundSeconds=120;
            ((ILobbyDetails)lobby).ConfigureSetup(setup);Call(view,"CloseSetupPage");
            Check(((ILobbyDetails)lobby).Setup.population==2,"New match population setting is retained");
            Call(screen,"Refresh");yield return null;
            Check(Field<RectTransform>(screen,"modeCard").gameObject.activeSelf&&Field<RectTransform>(screen,"mapCard").gameObject.activeSelf,"New-match terminal exposes map and mode cards");
            CaptureTerminal(Field<RectTransform>(screen,"mainFace"),"82-next-match-setup.png");
            TerminalAction(screen,"marina-play");
            yield return TerminalUntil(()=>Services.Session is CastlePlaySession s&&s.Modes&&s.Modes.Rules.mode==GameModeKind.KingOfHill,"Terminal starts the newly configured match");
            session=(CastlePlaySession)Services.Session;match=session.Modes;session.SetMenu(false);
            Check(MatchSetup.MapId==nextMap.id,"New match receives the selected map");
            Check(match.Rules.countdownSeconds==12,"New match receives the configured countdown");
            session.VisitLobby();yield return TerminalUntil(()=>session.VisitingLobby,"Lobby terminal can be visited between rounds");
            screen=IslandLobby.Persistent.GetComponent<MarinaScreen>();Call(screen,"Refresh");
            Check(match.Frame.phase==RoundPhase.Countdown,"Actual new round is counting down during terminal review");
            Check(Field<Text>(screen,"mainHeading").text=="ROUND STARTING","Between-round terminal shows actual countdown phase");
            CaptureTerminal(Field<RectTransform>(screen,"mainFace"),"83-between-round-terminal.png");
            TerminalAction(screen,"marina-return-match");yield return null;
            Check(!session.VisitingLobby,"Terminal Return to Match restores play");
        }
        sealed class TerminalGuestView:IMatchRuntime
        {
            readonly IMatchRuntime source;public TerminalGuestView(IMatchRuntime source){this.source=source;}
            public ModeRules Rules=>source.Rules;public MatchFrame Frame=>source.Frame;public string LocalActorId=>source.LocalActorId;public bool Authority=>false;
            public IModeActor FindActor(string id)=>source.FindActor(id);public Transform FindParticipantRoot(string id)=>source.FindParticipantRoot(id);
            public ModeRules RulesFor(GameObject actor)=>source.RulesFor(actor);public bool CanClimb(GameObject actor,IClimbable climbable)=>source.CanClimb(actor,climbable);
            public bool RequestPunch(GameObject source,Vector3 origin,Vector3 direction,float speed)=>false;
            public void Configure(ModeRules rules)=>throw new Exception("Guest mutation");public void StartRound()=>throw new Exception("Guest mutation");
            public void ReturnToLobby()=>throw new Exception("Guest mutation");public void NextSpectator(int step){}public void ActivateHandPower(float seconds){}
        }
    }
}
