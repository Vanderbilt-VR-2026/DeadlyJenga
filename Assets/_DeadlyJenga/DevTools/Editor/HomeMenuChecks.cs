using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using LogType=UnityEngine.LogType;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class HomeMenuChecks
    {
        const string Key="HomeMenu.Checks";
        static IEnumerator run;
        static readonly Stack<IEnumerator> steps=new Stack<IEnumerator>();
        static int errors;
        static NetworkRunner probe;
        static NetworkProjectConfig.PeerModes oldPeer;
        static bool peerChanged;
        static string Report=>ProjectPath.Report("HomeMenuChecks.txt");
        [Serializable] sealed class Saved { public SceneSetup[] scenes;public string map,mode;public int population; }
        static HomeMenuChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        static void Tick()
        {
            if(run==null)
            {
                string request=ProjectPath.Report("HomeMenuChecks.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
                File.Delete(request);
                try { Start(); }catch(Exception error){File.AppendAllText(Report,error+"\n");}
                return;
            }
            try
            {
                if(steps.Count==0)steps.Push(run);
                while(steps.Count>0)
                {
                    var step=steps.Peek();
                    if(!step.MoveNext()){(step as IDisposable)?.Dispose();steps.Pop();continue;}
                    if(step.Current is IEnumerator nested){steps.Push(nested);continue;}
                    return;
                }
            }
            catch(Exception error){errors++;File.AppendAllText(Report,"FAIL "+error+"\n");}
            while(steps.Count>0)(steps.Pop() as IDisposable)?.Dispose();run=null;
            if(peerChanged){NetworkProjectConfig.Global.PeerMode=oldPeer;peerChanged=false;}
            Application.logMessageReceived-=Log;
            File.AppendAllText(Report,"COMPLETE; logged errors="+errors+". Desktop Editor; headset and separate-device networking not tested.\n");
            EditorApplication.ExitPlaymode();
        }
        public static void Start()
        {
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||SceneManager.GetSceneByPath(s.path).isDirty)))throw new InvalidOperationException("Unsaved scenes left untouched.");
            if(!ValidationRunGate.TryAcquire(Key))throw new InvalidOperationException("Another validation is running.");
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=scenes,map=MatchSetup.MapId,mode=MatchSetup.ModeId,population=MatchSetup.BotTarget}));
            File.WriteAllText(Report,"Home integration checks started "+DateTime.Now.ToString("O")+"\n");
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {
                SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Running",true);errors=0;
                EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();Application.logMessageReceived+=Log;run=Validate();
            }
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Running",false))
            {
                SessionState.SetBool(Key+"Running",false);Application.logMessageReceived-=Log;run=null;
                AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));
                MatchSetup.MapId=saved.map;MatchSetup.ModeId=saved.mode;MatchSetup.BotTarget=saved.population;
                EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);ValidationRunGate.Release(Key);
            }
        }
        static void Log(string message,string trace,LogType type)
        {
            if(message.Contains("PeerModes.Multiple requires a scene")){File.AppendAllText(Report,"Transport fixture diagnostic: "+message+"\n");return;}
            if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
        static IEnumerator Until(Func<bool> condition,string message,float seconds=45)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!condition()&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(condition(),message);
        }
        static IEnumerator Task(System.Threading.Tasks.Task task,string message)
        {yield return Until(()=>task.IsCompleted,message);if(task.IsFaulted)throw task.Exception;}
        static IEnumerator Validate()
        {
            Application.runInBackground=true;
            yield return Until(()=>Object.FindFirstObjectByType<HomeMenuView>(),"Home menu attaches through Core");
            var home=Object.FindFirstObjectByType<HomeSession>();var view=Object.FindFirstObjectByType<HomeMenuView>();
            Require(EditorBuildSettings.scenes.First(s=>s.enabled).path.EndsWith("/Home.unity"),"Home is first enabled build scene");
            Require(Services.Session==null&&Tracked.Movers.Count==0,"Home creates no game simulation or bots");
            Require(Resources.LoadAll<Texture2D>("HomeSlides").Length==4,"All four supplied slides load");
            home.Code="invalid";Require(!home.CodeValid,"Invalid join code rejected");home.JoinCode();Require(!home.Busy,"Invalid join does not connect");
            home.Code="US-ABC234";Require(home.CodeValid,"Existing region-prefixed code format supported");
            yield return new WaitForSecondsRealtime(1.2f);
            ScreenCapture.CaptureScreenshot(ProjectPath.Report("HomeMenu.png"));yield return new WaitForSecondsRealtime(.3f);
            view.ShowJoin(true);yield return null;ScreenCapture.CaptureScreenshot(ProjectPath.Report("HomeMenu-Join.png"));yield return new WaitForSecondsRealtime(.3f);view.ShowJoin(false);
            var animate=typeof(HomeMenuView).GetMethod("AnimateSlideshow",BindingFlags.Instance|BindingFlags.NonPublic);
            int first=view.SlideIndex;bool fullWhite=false;
            for(int i=0;i<180&&view.SlideIndex==first;i++){animate.Invoke(view,new object[]{.1f});fullWhite|=view.WhiteFade>=.99f;}
            Require(fullWhite&&view.WhiteFade>=.99f,"Actual slideshow changes images only at full white, including stalled frames");
            Require(view.SlideIndex==(first+1)%4,"Slideshow advances through supplied images in order");
            home.RandomLobby();
            yield return Until(()=>IslandLobby.Current&&IslandLobby.Current.SurvivorBots?.Count==9,"Random lobby fills to ten including player",50);
            Require(!IslandLobby.Current.Room.Connected,"Random lobby works without an Internet room");
            SceneManager.LoadScene("Home");yield return Until(()=>Object.FindFirstObjectByType<HomeMenuView>(),"Home can reopen");
            home=Object.FindFirstObjectByType<HomeSession>();home.Demo();
            yield return Until(()=>Services.Session?.Demo!=null,"Demo opens actual Game session",60);
            Require(MatchSetup.MapId=="castle"&&MatchSetup.ModeId=="demo","Demo selects original castle and sandbox mode");
            Require(Services.World!=null&&Services.Session.Player!=null,"Castle world and local player initialize");
            SceneManager.LoadScene("Home");yield return Until(()=>Object.FindFirstObjectByType<HomeMenuView>(),"Home reopens after Game");
            oldPeer=NetworkProjectConfig.Global.PeerMode;peerChanged=true;NetworkProjectConfig.Global.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
            home=Object.FindFirstObjectByType<HomeSession>();home.Host();
            yield return Until(()=>IslandLobby.Current&&IslandRoomConnection.Instance.Connected,"Home Host creates real Photon room and opens lobby",60);
            var room=IslandRoomConnection.Instance;var lobby=IslandLobby.Current;
            yield return new WaitForSecondsRealtime(1.5f);
            Require(room.IsHost&&lobby.SurvivorBots.Count==0&&!HomeSession.AutofillLobby,"Host lobby has zero bots");
            var app=Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion=room.Region;app.AppVersion=(app.AppVersion??"")+"/"+room.Catalog.NetworkVersion;
            var go=new GameObject("Home join transport fixture");Object.DontDestroyOnLoad(go);probe=go.AddComponent<NetworkRunner>();
            var connect=probe.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=room.Code,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Config=IslandRoomConnection.MigrationConfig(),Scene=new NetworkSceneInfo(),SceneManager=go.AddComponent<NetworkSceneManagerDefault>()});
            yield return Task(connect,"Second Fusion runner joins Home-created room");Require(connect.Result.Ok&&room.HumanCount==2,"Home host accepts another real network player");
            yield return Task(probe.Shutdown(),"Fixture client shuts down");Object.Destroy(go);probe=null;
            yield return Task(room.Leave(),"Home-created room closes cleanly");
            yield return new WaitForSecondsRealtime(1.5f);Require(lobby.SurvivorBots.Count==0,"Host entry remains bot-free after disconnect");
            SceneManager.LoadScene("Home");yield return Until(()=>Object.FindFirstObjectByType<HomeMenuView>(),"Home is ready for independent Join test");
            home=Object.FindFirstObjectByType<HomeSession>();room=IslandRoomConnection.Instance;
            string address=IslandRoomAddress.Create(room.Region);
            go=new GameObject("Independent Home join host");Object.DontDestroyOnLoad(go);probe=go.AddComponent<NetworkRunner>();
            var host=probe.StartGame(new StartGameArgs{GameMode=GameMode.Host,SessionName=address,PlayerCount=10,IsVisible=false,CustomPhotonAppSettings=app,Config=IslandRoomConnection.MigrationConfig(),Scene=new NetworkSceneInfo(),SceneManager=go.AddComponent<NetworkSceneManagerDefault>()});
            yield return Task(host,"Independent Photon host starts for Home Join test");Require(host.Result.Ok,"Independent host creates a real room");
            home.Code=address;home.JoinCode();
            yield return Until(()=>IslandLobby.Current&&room.Connected&&!room.Busy,"Home join code connects and opens actual guest lobby",60);
            Require(!room.IsHost&&room.HumanCount==2,"Home Join enters as guest rather than creating a new room");
            yield return new WaitForSecondsRealtime(1.2f);Require(IslandLobby.Current.SurvivorBots.Count==0,"Joined lobby contains no autofill bots");
            yield return Task(room.Leave(),"Home guest connection closes cleanly");
            yield return Task(probe.Shutdown(),"Independent fixture host shuts down");Object.Destroy(go);probe=null;
            Require(errors==0,"Home, lobby, castle and two-runner transport complete without errors");
        }
    }
}
