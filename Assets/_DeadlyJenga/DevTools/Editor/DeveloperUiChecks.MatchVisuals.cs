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
            var original=MatchRuntime.Current;
            var fixtureType=typeof(HudReadabilityChecks).GetNestedType("Fixture",BindingFlags.NonPublic);
            var fixture=(IMatchRuntime)Activator.CreateInstance(fixtureType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{original},null);
            var frame=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(original.Frame));
            frame.rules=ModeDefaults.Create(GameModeKind.KingOfHill);frame.phase=RoundPhase.Playing;frame.metricLabel="Score";
            frame.players=Enumerable.Range(0,12).Select(i=>new MatchPlayerFrame{id=i==0?original.LocalActorId:"review-"+i,name=i==0?"Evan":i==1?"A Very Long Player Name For Layout Review":"Player "+(i+1),alive=true,metric=120-i*7,rank=i+1,survivalSeconds=120-i*3,infections=12-i,color=Color.HSVToRGB(i/12f,.7f,1)}).ToArray();
            frame.eliminations=Array.Empty<EliminationFrame>();frame.eliminationEpoch="visual-review";
            var hud=session.GetComponent<CandyMatchHud>();var results=session.GetComponent<CandyResultsHud>();
            void Publish(){frame.revision++;fixtureType.GetProperty("Frame").SetValue(fixture,frame);MatchRuntime.Current=fixture;}
            void Shot(string name){Call(hud,"Update");Call(results,"Update");CaptureMatchCanvas(session,name);}
            try
            {
                Publish();yield return null;
                CandyMatchHud.ScoreboardPinned=true;yield return null;Shot("61-live-standings.png");
                CandyMatchHud.ScoreboardPinned=false;yield return null;Call(hud,"Update");
                frame=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(frame));
                frame.eliminations=new[]{new EliminationFrame{sequence=1,killer=frame.players[1].id,victim=original.LocalActorId},new EliminationFrame{sequence=2,killer=original.LocalActorId,victim=frame.players[2].id},new EliminationFrame{sequence=3,victim=frame.players[3].id},new EliminationFrame{sequence=4,killer=frame.players[4].id,victim=frame.players[5].id}};
                Publish();yield return null;Shot("62-elimination-feed.png");
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
            var camera=session.ActiveCamera;var previous=camera.targetTexture;var clear=camera.clearFlags;int mask=camera.cullingMask;
            var canvases=session.GetComponentsInChildren<Canvas>(true).Where(c=>c.enabled&&c.gameObject.activeInHierarchy).ToArray();
            var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();
            var objects=canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).Distinct().ToArray();var layers=objects.Select(o=>o.layer).ToArray();
            var rt=new RenderTexture(1600,900,24);var old=RenderTexture.active;Texture2D pixels=null;
            var data=camera.GetUniversalAdditionalCameraData();bool post=data.renderPostProcessing;
            try
            {
                camera.targetTexture=rt;
                foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.01f;}
                foreach(var o in objects)o.layer=31;
                Canvas.ForceUpdateCanvases();camera.cullingMask=mask&~(1<<31);camera.Render();
                camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.Depth;data.renderPostProcessing=false;camera.Render();
                RenderTexture.active=rt;pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,name),pixels.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
                for(int i=0;i<objects.Length;i++)objects[i].layer=layers[i];
                camera.targetTexture=previous;camera.clearFlags=clear;camera.cullingMask=mask;data.renderPostProcessing=post;RenderTexture.active=old;rt.Release();Object.Destroy(rt);Object.Destroy(pixels);
            }
        }
    }
}
