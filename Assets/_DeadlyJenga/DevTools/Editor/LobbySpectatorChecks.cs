using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class LobbySpectatorChecks
    {
        const string Key="LobbySpectatorChecks";
        static IEnumerator run;static int errors,checks;static bool failed;
        static string Report=>ProjectPath.Report("LobbySpectatorChecks.txt");
        static LobbySpectatorChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        public static void Run()
        {
            string root=Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            if(Path.GetFullPath(File.ReadAllText(ProjectPath.Report("CandyTerminalDesign.owner")).Trim()).ToLowerInvariant()!=root.ToLowerInvariant())throw new InvalidOperationException("Wrong owned editor.");
            if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Editor busy.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Unsaved scenes untouched.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Saved{scenes=EditorSceneManager.GetSceneManagerSetup(),background=Application.runInBackground,volume=AudioListener.volume,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession}));
            SessionState.SetBool(Key,true);File.WriteAllText(Report,"Actual Unity desktop TV / lobby visit checks\n"+root+"\n");
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        [Serializable] sealed class Saved{public SceneSetup[] scenes;public bool background,fromLobby,local;public float volume;}
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){errors=checks=0;failed=false;Application.logMessageReceived+=Log;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();run=Flatten(Validate());}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                Application.logMessageReceived-=Log;run=null;SessionState.SetBool(Key,false);
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Scenes",""));Application.runInBackground=saved.background;AudioListener.volume=saved.volume;MatchSetup.FromLobby=saved.fromLobby;MatchSetup.StartLocalSession=saved.local;
                if(saved.scenes.Any(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                File.AppendAllText(Report,"COMPLETE checks="+checks+" errors="+errors+" failed="+failed+"\nNo headset or two-player test.\n");
            }
        }
        static void Log(string m,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+m+"\n"+trace+"\n");}}
        static void Tick()
        {
            if(run==null)
            {
                string request=ProjectPath.Report("LobbySpectatorChecks.request");
                if(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(request)&&File.ReadAllText(request).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString()){File.Delete(request);Run();}
                return;
            }
            try{if(run.MoveNext())return;}catch(Exception e){failed=true;File.AppendAllText(Report,"FAIL "+e+"\n");}
            (run as IDisposable)?.Dispose();run=null;EditorApplication.ExitPlaymode();
        }
        static void Check(bool ok,string text){checks++;File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+text+"\n");if(!ok)throw new Exception(text);}
        static IEnumerator Flatten(IEnumerator source){while(source.MoveNext()){if(source.Current is IEnumerator nested){var child=Flatten(nested);while(child.MoveNext())yield return child.Current;}else yield return source.Current;}}
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static IEnumerator Validate()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            yield return Wait(8);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();Check(session&&session.Modes,"Game solo starts");
            var world=Services.World;var player=session.Player;var modes=session.Modes;var scene=session.gameObject.scene;
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.TargetPopulation=10;session.SurvivorBots.FillToTarget();
            yield return Wait(3);
            int bots=session.SurvivorBots.Count;float elapsed=modes.Frame.elapsed;
            session.VisitLobby();yield return null;
            Check(session.VisitingLobby,"Visit command creates lobby without scene transition");
            var lobby=Object.FindFirstObjectByType<IslandLobby>();Check(lobby&&lobby.VisitingLobby,"Annex lobby uses approved visit contract");
            Check(ReferenceEquals(Services.World,world)&&ReferenceEquals(Services.Session,session),"Match world and session remain authoritative");
            Check(IslandLobby.Current==null,"Annex does not claim network lobby routing");
            Check(scene==session.gameObject.scene&&player==session.Player&&modes==session.Modes,"Match scene, player and simulation are preserved");
            Check(!NetworkActors.Local.Contains(lobby.Player.gameObject.GetComponent<INetworkActor>()),"Visitor excluded from network actor registry");
            Check(!Tracked.Movers.Contains(lobby.Player as IMovementState),"Visitor excluded from gameplay mover registry");
            Check(!lobby.CanHost&&!lobby.CanConfigure&&!lobby.JoinEnabled&&lobby.CanStart,"Room changes locked; return enabled");
            var wall=lobby.GetComponentInChildren<LobbySpectatorTVWall>();Check(wall,"3 by 3 TV wall built");
            Check(wall.GetComponentsInChildren<UnityEngine.UI.RawImage>().Length==9,"Nine screen images");
            Check(wall.GetComponentsInChildren<Camera>().Length==1&&!wall.FeedCamera.enabled,"Only one manually scheduled feed camera");
            Check(!wall.FeedCamera.GetUniversalAdditionalCameraData().allowXRRendering&&!wall.FeedCamera.allowMSAA,"Feed camera disables stereo and MSAA");
            lobby.Player.Place(wall.transform.TransformPoint(new Vector3(0,0,-4)),Quaternion.LookRotation(wall.transform.forward));lobby.Player.InputSuspended=true;
            var eye=lobby.ActiveCamera;eye.transform.rotation=Quaternion.LookRotation(wall.transform.TransformPoint(new Vector3(0,1.5f,0))-eye.transform.position);
            float readyUntil=Time.realtimeSinceStartup+45;while(Time.realtimeSinceStartup<readyUntil&&(wall.AssignedChannels==0||wall.ReadyChannels<wall.AssignedChannels))yield return null;
            yield return Wait(1);
            Check(wall.ReadyChannels==wall.AssignedChannels,"Every assigned screen receives a real frame");
            Check(wall.AssignedChannels>0&&wall.RenderCount>0,"Actual living match participants render on TVs");
            File.AppendAllText(Report,"FEED COST meanSubmitMs="+wall.AverageRenderMilliseconds.ToString("F2")+" lastSubmitMs="+wall.RenderMilliseconds.ToString("F2")+" renders="+wall.RenderCount+" channels="+wall.AssignedChannels+"; includes startup, not GPU or overall FPS\n");
            Check(modes.Frame.elapsed>elapsed,"Match time continues during lobby visit");
            Check(session.SurvivorBots.Count>=bots,"Match bots survive the visit");
            Check(modes.Frame.players.All(p=>p.id!="lobby-visitor"),"Visitor never enters match scoreboard");
            Capture(eye,"LobbyTVWall.png");
            foreach(bool enabled in SessionState.GetBool(Key+"MeasureCost",false)?new[]{false,true,false,true}:Array.Empty<bool>())
            {
                wall.FeedsEnabled=enabled;yield return Wait(.5f);
                int frames=0,rendered=wall.RenderCount;float start=Time.realtimeSinceStartup,last=start;double sum=0;
                while(Time.realtimeSinceStartup-start<6){yield return null;if(Time.realtimeSinceStartup!=last){sum+=Time.realtimeSinceStartup-last;last=Time.realtimeSinceStartup;frames++;}}
                File.AppendAllText(Report,"SAMPLE feeds="+enabled+" updates="+frames+" averageMs="+(sum*1000/Math.Max(1,frames)).ToString("F2")+" renders="+(wall.RenderCount-rendered)+" lastRenderSubmitMs="+wall.RenderMilliseconds.ToString("F2")+" channels="+wall.AssignedChannels+" bots="+session.SurvivorBots.Count+"\n");
            }
            wall.FeedsEnabled=true;int before=wall.RenderCount;eye.transform.rotation=Quaternion.LookRotation(-wall.transform.forward);yield return Wait(1.5f);
            Check(wall.RenderCount==before,"Offscreen TVs perform zero render calls");
            lobby.StartOrCancel();yield return null;yield return null;
            Check(!session.VisitingLobby&&!Object.FindFirstObjectByType<LobbySpectatorTVWall>(),"Return button removes wall and lobby");
            Check(session.ActiveCamera&&session.ActiveCamera.enabled,"Match camera restored");
            if(Application.isFocused)Check(!session.MenuOpen&&Cursor.lockState==CursorLockMode.Locked,"Return restores gameplay input and mouse capture");
            Check(ReferenceEquals(Services.World,world)&&ReferenceEquals(Services.Session,session),"Global services preserved on return");
            session.VisitLobby();yield return null;Check(session.VisitingLobby,"Repeat visit works");session.ReturnToMatch();yield return null;yield return null;
            Check(!session.VisitingLobby&&Object.FindObjectsByType<LobbySpectatorTVWall>(FindObjectsSortMode.None).Length==0,"Repeat visit leaves no wall or camera leak");
            SceneManager.LoadScene("Lobby");yield return Wait(4);
            Check(IslandLobby.Current&&!IslandLobby.Current.VisitingLobby,"Ordinary lobby still initializes");
            var normalWall=IslandLobby.Current.GetComponentInChildren<LobbySpectatorTVWall>();Check(normalWall,"Ordinary lobby has the TV stack");
            Check(normalWall.GetComponentsInChildren<UnityEngine.UI.RawImage>().Length==9,"Ordinary lobby has nine screens");
            Check(errors==0,"No runtime errors");
        }
        static void Capture(Camera source,string name)
        {
            var root=new GameObject("TV evidence camera");var camera=root.AddComponent<Camera>();camera.CopyFrom(source);camera.GetUniversalAdditionalCameraData().allowXRRendering=false;camera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);camera.enabled=false;
            var visual=source.GetComponentInParent<IModeActor>()?.Visual;var renderers=visual?visual.GetComponentsInChildren<Renderer>(true):Array.Empty<Renderer>();var hidden=renderers.Select(x=>x.forceRenderingOff).ToArray();foreach(var renderer in renderers)renderer.forceRenderingOff=true;
            var target=new RenderTexture(1600,1100,24);camera.targetTexture=target;var previous=RenderTexture.active;Texture2D image=null;
            try{camera.Render();RenderTexture.active=target;image=new Texture2D(1600,1100,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());}
            finally{for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].forceRenderingOff=hidden[i];RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(root);target.Release();Object.Destroy(target);if(image)Object.Destroy(image);}
        }
    }
}
