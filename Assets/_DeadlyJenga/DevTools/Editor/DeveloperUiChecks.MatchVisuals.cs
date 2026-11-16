using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt.Marina;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class DeveloperUiChecks
    {
        static IEnumerator MatchVisualChecks()
        {
            var lobby=Services.Session as IslandLobby;
            Check(lobby,"Actual lobby initialized");
            GameExperiencePanel.Resolve(lobby)?.SetExperience(GameExperience.Demo);
            lobby.SelectMap("castle");lobby.SelectMode("demo");
            var marina=lobby.GetComponent<MarinaScreen>();
            Check(marina&&lobby.CanStart,"Current front terminal can start a match");
            var target=((IEnumerable)Field<object>(marina,"targets")).Cast<object>().Single(t=>Field<string>(t,"id")=="marina-play");
            Field<Action>(target,"action")();
            float deadline=Time.realtimeSinceStartup+60;
            int last=-1;
            while(Services.Session is IslandLobby&&Time.realtimeSinceStartup<deadline)
            {
                if(lobby.StartPending)
                {
                    Call(marina,"Refresh");
                    string hint=lobby.SetupHint;
                    int second=hint.Contains(" 1")?1:hint.Contains(" 2")?2:3;
                    if(second!=last){CaptureTerminal(Field<RectTransform>(marina,"mainFace"),"60-launch-"+second+".png");last=second;}
                }
                yield return null;
            }
            while(!(Services.Session is CastlePlaySession ready&&ready.Modes&&MatchRuntime.Current!=null)&&Time.realtimeSinceStartup<deadline)yield return null;
            var session=Services.Session as CastlePlaySession;
            Check(session&&session.Modes,"Terminal launch travels to actual Game");
            session.SetMenu(false);session.SurvivorBots.AutoFill=false;
            float settle=Time.realtimeSinceStartup+3;while(Time.realtimeSinceStartup<settle)yield return null;
            session.SetMenu(false);
            var original=MatchRuntime.Current;
            var fixtureType=typeof(HudReadabilityChecks).GetNestedType("Fixture",BindingFlags.NonPublic);
            var fixture=(IMatchRuntime)Activator.CreateInstance(fixtureType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{original},null);
            var frame=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(original.Frame));
            frame.rules=ModeDefaults.Create(GameModeKind.KingOfHill);frame.phase=RoundPhase.Playing;frame.metricLabel="Score";
            frame.players=Enumerable.Range(0,12).Select(i=>new MatchPlayerFrame{id=i==0?original.LocalActorId:"review-"+i,name=i==0?"Evan":i==1?"A Very Long Player Name For Layout Review":"Player "+(i+1),alive=true,metric=120-i*7,rank=i+1,survivalSeconds=120-i*3,infections=12-i,color=Color.HSVToRGB(i/12f,.7f,1)}).ToArray();
            frame.eliminations=Array.Empty<EliminationFrame>();frame.eliminationEpoch="visual-review";
            var hud=session.GetComponent<CandyMatchHud>();var results=session.GetComponent<CandyResultsHud>();
            void Publish(){frame.revision++;fixtureType.GetProperty("Frame").SetValue(fixture,frame);MatchRuntime.Current=fixture;}
            void Shot(string name)
            {
                if(frame.phase!=RoundPhase.Results)session.SetMenu(false);
                Call(hud,"Update");Call(results,"Update");
                if(name.StartsWith("61")||name.StartsWith("62"))
                    Check(Field<Canvas>(hud,"canvas").enabled,"Gameplay HUD visible for "+name+"; menu="+session.MenuOpen+"; spectating="+(session.Demo as ISpectatorControls)?.Spectating);
                CaptureMatchCanvas(session,name);
            }
            try
            {
                Publish();yield return null;
                CandyMatchHud.ScoreboardPinned=true;yield return null;Shot("61-live-standings.png");
                CandyMatchHud.ScoreboardPinned=false;yield return null;Call(hud,"Update");
                frame=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(frame));
                frame.eliminations=new[]{new EliminationFrame{sequence=1,killer=frame.players[1].id,victim=original.LocalActorId},new EliminationFrame{sequence=2,killer=original.LocalActorId,victim=frame.players[2].id},new EliminationFrame{sequence=3,victim=frame.players[3].id},new EliminationFrame{sequence=4,killer=frame.players[4].id,victim=frame.players[5].id}};
                Publish();yield return null;Shot("62-elimination-feed.png");
                var feedCards=Field<Canvas>(hud,"canvas").GetComponentsInChildren<CanvasGroup>().Where(g=>g.name=="Elimination feed"&&g.gameObject.activeInHierarchy).ToArray();
                Check(feedCards.Length==4,"Four distinct elimination cards visible");
                foreach(var card in feedCards)
                {
                    var labels=card.GetComponentsInChildren<Text>().Where(t=>t.name=="Elimination victim"||t.name=="Elimination action").ToArray();
                    foreach(var label in labels)Check(label.preferredHeight<=label.rectTransform.rect.height+1,"Feed label fits: "+label.name);
                }
                Check(feedCards.SelectMany(g=>g.GetComponentsInChildren<Text>()).Any(t=>t.name=="Elimination victim"&&t.text=="YOU"),"Local death visibly names YOU as victim");
                Call(hud,"Update");
                Check(Field<Canvas>(hud,"canvas").GetComponentsInChildren<CanvasGroup>().Count(g=>g.name=="Elimination feed"&&g.gameObject.activeInHierarchy)==4,"Repeated snapshot does not duplicate feed cards");
                frame.phase=RoundPhase.Results;frame.winner="A Very Long Player Name For Layout Review";Publish();
                deadline=Time.realtimeSinceStartup+12;
                while((!Field<Canvas>(results,"canvas")||!Field<Canvas>(results,"canvas").gameObject.activeInHierarchy)&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(Field<Canvas>(results,"canvas")&&Field<Canvas>(results,"canvas").gameObject.activeInHierarchy,"Results visible after real game-over presentation delay");
                Shot("63-results.png");
                frame.rules=ModeDefaults.Create(GameModeKind.Infection);Publish();yield return null;Shot("64-results-infection.png");
                File.AppendAllText(Report,"EVIDENCE 61-64 use explicit long-name/ranking/elimination snapshot fixtures over actual Game presentation. No live peer claim.\n");
            }
            finally {MatchRuntime.Current=original;CandyMatchHud.ScoreboardPinned=false;session.SetMenu(false);}
            original.ReturnToLobby();deadline=Time.realtimeSinceStartup+60;
            while(!(Services.Session is IslandLobby)&&Time.realtimeSinceStartup<deadline)yield return null;
            var returned=Services.Session as IslandLobby;Check(returned,"Results return route reaches Lobby");
            var hall=returned.GetComponent<LobbyLastRoundBoard>();Check(hall,"Current lobby has last-round board");
            yield return null;CaptureTerminal(hall.GetComponentsInChildren<Canvas>().First(c=>c.name=="Last match billboard").GetComponent<RectTransform>(),"65-last-round-board.png");
        }

        static void CaptureMatchCanvas(CastlePlaySession session,string name)
        {
            var canvas=name.StartsWith("63")||name.StartsWith("64")
                ?Field<Canvas>(session.GetComponent<CandyResultsHud>(),"canvas")
                :Field<Canvas>(session.GetComponent<CandyMatchHud>(),"canvas");
            var rect=(RectTransform)canvas.transform;
            var mode=canvas.renderMode;var camera=canvas.worldCamera;var parent=rect.parent;
            var position=rect.localPosition;var rotation=rect.localRotation;var scale=rect.localScale;
            var size=rect.sizeDelta;var min=rect.anchorMin;var max=rect.anchorMax;
            var scaler=canvas.GetComponent<CanvasScaler>();bool scaling=scaler&&scaler.enabled;
            try
            {
                if(scaler)scaler.enabled=false;
                canvas.renderMode=RenderMode.WorldSpace;rect.SetParent(null,false);
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(1280,720);
                rect.position=new Vector3(0,200,0);rect.rotation=Quaternion.identity;rect.localScale=Vector3.one*.001f;
                Canvas.ForceUpdateCanvases();CaptureTerminal(rect,name);
                File.AppendAllText(Report,"CAPTURE "+name+" uses a temporary inspection camera over the production canvas; layout restored afterward.\n");
            }
            finally
            {
                rect.SetParent(parent,false);canvas.renderMode=mode;canvas.worldCamera=camera;
                rect.anchorMin=min;rect.anchorMax=max;rect.sizeDelta=size;
                rect.localPosition=position;rect.localRotation=rotation;rect.localScale=scale;
                if(scaler)scaler.enabled=scaling;
            }
        }
    }
}
