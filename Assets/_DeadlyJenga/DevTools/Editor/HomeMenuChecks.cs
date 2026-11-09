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
        public static void RunPlayEntryBatch()
        {
            SessionState.SetBool(Key+"PlayEntryOnly",true);
            if(string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));
            Start();
        }
        public static void RunHostedPlayBatch()
        {
            SessionState.SetBool(Key+"HostedPlayOnly",true);
            if(string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));
            Start();
        }
        public static void RunQuestFlowBatch()
        {
            SessionState.SetBool(Key+"QuestOffline",true);
            if(string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));
            Start();
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
                if(SessionState.GetBool(Key+"QuestOffline",false)||SessionState.GetBool(Key+"PlayEntryOnly",false)||
                    SessionState.GetBool(Key+"HostedPlayOnly",false))
                {
                    SessionState.SetBool(Key+"PlayEntryOnly",false);
                    SessionState.SetBool(Key+"HostedPlayOnly",false);
                    SessionState.SetBool(Key+"QuestOffline",false);
                    EditorApplication.Exit(errors==0?0:2);
                }
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
            if(SessionState.GetBool(Key+"HostedPlayOnly",false))
            {
                oldPeer=NetworkProjectConfig.Global.PeerMode;
                peerChanged=true;
                NetworkProjectConfig.Global.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                home.Host();
                yield return Until(()=>IslandLobby.Current&&IslandRoomConnection.Instance.Connected&&
                    !IslandRoomConnection.Instance.Busy&&IslandLobby.Current.CanStart,
                    "Home Host opens a Play lobby that can choose its first game",65);
                yield return ModeSelectionNetworkChecks.Run(Require,alreadyHostedPlay:true);
                yield break;
            }
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
            int hill=Array.IndexOf(home.QuickPlayModes,"King of the Hill");
            Require(hill>=0,"Quick Play includes King of the Hill");
            home.SelectQuickPlayMode(hill);
            int crown=Array.FindIndex(home.QuickPlayMaps,title=>title.Contains("Crown"));
            Require(crown>=0,"Quick Play offers the compatible Crown Summit arena");
            home.StartQuickPlay(crown);
            yield return Until(()=>RoomGameExperience.Current?.GamePhase==RoomGamePhase.Playing,"Quick Play starts the selected game directly",75);
            var experience=RoomGameExperience.Current;
            Require(MatchSetup.ModeId=="hill"&&MatchSetup.MapId=="mode-koth","Selected map and mode were loaded");
            experience.SetExperience(GameExperience.Demo);
            Require(experience.Experience==GameExperience.Game&&!experience.CanConfigureExperience,"Play room rejects the Developer toggle");
            experience.RequestReturn();
            yield return Until(()=>IslandLobby.Current&&experience.GamePhase==RoomGamePhase.Selected&&
                experience.GameStatus.StartsWith("NEXT",StringComparison.Ordinal),
                "The lobby reveals a mode/map pair while counting down",60);
            Require(experience.CountdownSeconds>0&&experience.CountdownSeconds<=30,
                "The live lobby countdown is within the 30-second window ("+experience.CountdownSeconds+" seconds)");
            yield return Until(()=>IslandLobby.Current&&IslandLobby.Current.SurvivorBots?.Count==9,"Returning Play lobby fills to ten including player",45);
            Require(!IslandLobby.Current.Room.Connected,"Quick Play is available offline");
            yield return Until(()=>experience.GamePhase==RoomGamePhase.Playing&&!IslandLobby.Current,
                "The selected rotation launches automatically",70);
            SceneManager.LoadScene("Home");yield return Until(()=>Object.FindFirstObjectByType<HomeSession>(),"Home can reopen");
            home=Object.FindFirstObjectByType<HomeSession>();home.Demo();
            yield return Until(()=>IslandLobby.Current&&IslandLobby.Current.CanStart,"Developer opens the lobby directly",60);
            Require(IslandLobby.Current.Room.SharedDemoControls&&RoomGameExperience.Current.Experience==GameExperience.Demo,
                "The Developer lobby grants shared development controls");
            if(SessionState.GetBool(Key+"PlayEntryOnly",false))yield break;
            if(SessionState.GetBool(Key+"QuestOffline",false))
            {
                var lobbyView=Object.FindFirstObjectByType<IslandLobbyView>();
                Require(lobbyView&&lobbyView.InvokeChoice("mode-current"),"Lobby world terminal opens mode picker");
                var survivor=IslandLobby.Current.Player.gameObject.GetComponent<CastleSurvivor>();
                survivor.controlMode=FlyingHand.ControlMode.VR;survivor.RefreshMode();
                survivor.Place(lobbyView.WardrobePosition+Vector3.up*.04f,Quaternion.Euler(0,80,0));
                bool oldInput=survivor.InputSuspended;
                var eye=survivor.ViewCamera;
                lobbyView.SetWardrobeOpen(true);yield return null;
                Require(lobbyView.WardrobeOpen&&survivor.InputSuspended&&survivor.ViewCamera==eye,"Wardrobe pauses movement without swapping the tracked eye");
                var panel=(Transform)typeof(IslandLobbyView).GetField("wardrobePanel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(lobbyView);
                var forward=Vector3.ProjectOnPlane(eye.transform.forward,Vector3.up).normalized;
                Require(Vector3.Dot((panel.position-eye.transform.position).normalized,forward)>.92f,"Wardrobe aligns to the eye line when opened sideways");
                Require(lobbyView.InvokeChoice("wardrobe-open")&&!lobbyView.WardrobeOpen&&survivor.InputSuspended==oldInput,"Wardrobe Done restores input");
                lobbyView.SetWardrobeOpen(true);
                Require(lobbyView.WardrobeOpen,"Wardrobe can re-enter");
                lobbyView.SetWardrobeOpen(false);
            }

            IslandLobby.Current.SelectMap("castle");
            IslandLobby.Current.StartOrCancel();
            yield return Until(()=>Services.Session?.Demo!=null,"Developer lobby starts the actual Game session",60);
            Require(MatchSetup.MapId=="castle"&&MatchSetup.ModeId=="demo","Demo selects original castle and sandbox mode");
            Require(Services.World!=null&&Services.Session.Player!=null,"Castle world and local player initialize");

            if(SessionState.GetBool(Key+"QuestOffline",false))
            {
                var session=Services.Session;
                Require(MatchRuntime.Current is IDemoEquipmentControls grant&&grant.GiveEquipment(EquipmentKind.DualRifle),"Demo grants the real duplex rifle");
                var player=session.Player.gameObject.GetComponent<CastleSurvivor>();
                var equipment=player.GetComponent<IEquipmentControls>();
                equipment.SetEquipped(true);yield return new WaitForSecondsRealtime(.35f);
                Require(equipment.Equipment.drawn&&equipment.BlocksTraversal,"Drawn duplex blocks traversal");
                player.controlMode=FlyingHand.ControlMode.VR;player.RefreshMode();session.SetMenu(false);
                yield return new WaitForSecondsRealtime(.35f);
                var rifle=player.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Rifle optic");
                var shotgun=player.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Shotgun optic");
                Require(rifle&&shotgun,"Both production optic sockets exist on the equipped weapon");
                var sight=Object.FindFirstObjectByType<CandyGunSights>();
                Require(sight,"Live sight renderer is active");
                var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                var tick=typeof(CandyGunSights).GetMethod("LateUpdate",flags);
                var lensField=typeof(CandyGunSights).GetField("lens",flags);
                var camera=player.ViewCamera.transform;var savedCamera=new Pose(camera.position,camera.rotation);
                bool suspended=player.InputSuspended;
                try
                {
                    player.InputSuspended=false; // The no-headset editor pauses its XR input.
                    float scopeScale=Mathf.Abs(rifle.lossyScale.z);
                    camera.SetPositionAndRotation(rifle.position-rifle.forward*(.10f*scopeScale),rifle.rotation);
                    tick.Invoke(sight,null);
                    var lens=(GameObject)lensField.GetValue(sight);
                    string diagnostic=$"VR={session.IsVR} sameCamera={session.ActiveCamera==player.ViewCamera} suspended={player.InputSuspended} menu={session.MenuOpen} mode={equipment.Equipment.shotgun} stereo={player.ViewCamera.stereoEnabled} scale={scopeScale} optic={typeof(CandyGunSights).GetField("optic",flags).GetValue(sight)}";
                    Require(lens&&lens.activeSelf&&lens.transform.parent==rifle,"Raising the production rifle to the synthetic eye enables the magnifying lens without grip ("+diagnostic+")");
                    camera.position+=rifle.right*(.10f*scopeScale);tick.Invoke(sight,null);
                    Require(!lens.activeSelf,"Lateral eye offset hides the magnifying lens");
                }
                finally{player.InputSuspended=suspended;camera.SetPositionAndRotation(savedCamera.position,savedCamera.rotation);}
                equipment.ToggleRifleMode();yield return new WaitForSecondsRealtime(.45f);
                Require(equipment.Equipment.shotgun&&Mathf.Approximately(sight.Magnification,1),"Shotgun mode uses an unmagnified sight");
                var carriage=shotgun.parent;var root=carriage.parent;
                Require(root.InverseTransformPoint(shotgun.position).y>root.InverseTransformPoint(rifle.position).y,"Shotgun red dot rotates into the upper active position");
                equipment.SetEquipped(false);yield return null;
                Require(!equipment.Equipment.drawn&&!equipment.BlocksTraversal,"Holstering the duplex releases the traversal restriction");
                player.controlMode=FlyingHand.ControlMode.Desktop;player.RefreshMode();
                session.SetRole(true);yield return null;
                Require(session.PlayingHand&&session.Hand?.ViewCamera,"Flying hand takes camera ownership");
                var actor=session.Hand.gameObject.GetComponent<CastleHandActor>();
                var follow=actor.follow;
                actor.motor.ControlModeOverride=FlyingHand.ControlMode.VR;actor.motor.RefreshControlMode();
                var trackedEyeRotation=follow.viewCamera.transform.localRotation;
                follow.SnapToTarget();var heading=follow.transform.rotation;
                var target=follow.target;target.rotation=Quaternion.Euler(0,60,0)*target.rotation;
                follow.StepFollow(.5f);
                Require(Quaternion.Angle(heading,follow.transform.rotation)>20&&Quaternion.Angle(trackedEyeRotation,follow.viewCamera.transform.localRotation)<.01f,"Flying camera follows vehicle yaw without overwriting tracked head rotation");
                session.SetRole(false);yield return null;
                Require(!session.PlayingHand&&session.ActiveCamera==session.Player.ViewCamera,"Returning to survivor restores its active camera");
                MatchRuntime.Current.ReturnToLobby();
                yield return Until(()=>IslandLobby.Current&&Object.FindFirstObjectByType<IslandLobbyView>(),"Match returns to the real lobby");
            }
            SceneManager.LoadScene("Home");yield return Until(()=>Object.FindFirstObjectByType<HomeMenuView>(),"Home reopens after Game");
            if(SessionState.GetBool(Key+"QuestOffline",false))yield break;
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
