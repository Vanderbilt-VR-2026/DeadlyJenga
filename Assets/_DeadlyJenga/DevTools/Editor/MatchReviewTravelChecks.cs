using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
using DeadlyJenga.CandyAudio;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class MatchReviewTravelChecks
    {
        const string Key="MatchReviewTravelChecks";
        static string Report=>ProjectPath.Report("MatchReviewTravel.txt");
        static int failures,errors;
        static MatchReviewTravelChecks()
        {
            EditorApplication.update+=()=>
            {
                var request=ProjectPath.Report("MatchReviewTravel.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                File.Delete(request);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    failures=errors=0;Application.logMessageReceived+=Log;
                    EditorApplication.delayCall+=()=>{var go=new GameObject("Health and lobby checks");Object.DontDestroyOnLoad(go);go.AddComponent<MatchReviewTravelCheckDriver>().StartCoroutine(Guard());};
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Time.timeScale=1;Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                    var original=SessionState.GetString(Key+"Scene","");if(File.Exists(original))EditorSceneManager.OpenScene(original);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Development Review and Lobby Travel")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before this check.");
            File.WriteAllText(Report,"Development review, footprints and lobby travel integration\n");
            SessionState.SetString(Key+"Scene",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,UnityEngine.LogType type)
        {
            if(type!=UnityEngine.LogType.Error&&type!=UnityEngine.LogType.Exception&&type!=UnityEngine.LogType.Assert)return;
            if(NetworkProjectConfig.Global.PeerMode==NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.",StringComparison.Ordinal))
            {File.AppendAllText(Report,"EDITOR NOTE Headless extra Fusion peer has no scene; lobby poses and reliable transport are tested explicitly.\n");return;}
            if(message.StartsWith("[Package Manager Window]",StringComparison.Ordinal)){File.AppendAllText(Report,"EDITOR NOTE "+message+"\n");return;}
            errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");
        }
        static void Check(bool value,string label){File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        static IEnumerator Guard()
        {
            var test=CheckScene();
            while(true)
            {
                bool next=false;object value=null;Exception caught=null;
                try{next=test.MoveNext();if(next)value=test.Current;}catch(Exception e){caught=e;}
                if(caught!=null){failures++;File.AppendAllText(Report,"FAIL "+caught+"\n");break;}
                if(!next)break;yield return value;
            }
            File.AppendAllText(Report,$"COMPLETE failures={failures}; errors={errors}. Headset visibility and comfort remain unverified.\n");
            EditorApplication.ExitPlaymode();
        }
        static IEnumerator Until(Func<bool> predicate,string label,float timeout=40)
        {float end=Time.realtimeSinceStartup+timeout;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),label);}
        static IEnumerator CheckScene()
        {
            yield return new WaitForSeconds(1);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();Check(session&&session.Modes,"Actual Game scene starts");
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();
            for(int i=0;i<4;i++)Check(session.SurvivorBots.SpawnOne(),"Survivor bot spawns "+i);
            yield return new WaitForSeconds(.4f);
            var runtime=session.Modes;var rules=ModeSimulation.Defaults(GameModeKind.MurderMystery);
            rules.automaticDisasters=false;rules.countdownSeconds=0;rules.victorySeconds=.2f;
            runtime.Configure(rules);runtime.StartRound();
            yield return new WaitForSeconds(.25f);Time.timeScale=0;
            var review=(IMatchReviewControls)runtime;
            string murderer=review.PlayerInRole(MysteryRole.Murderer),sheriff=review.PlayerInRole(MysteryRole.Sheriff);
            Check(review.DevelopmentReviewEnabled&&review.CanEndGame&&murderer.Length>0&&sheriff.Length>0&&murderer!=sheriff,"Development review resolves both actual secret roles");
            var simulation=(ModeSimulation)Field(runtime,"simulation");
            session.developmentReviewTools=false;review.EndGame();review.WatchPlayer(murderer);
            Check(!review.DevelopmentReviewEnabled&&!review.CanEndGame&&review.PlayerInRole(MysteryRole.Murderer)==""&&simulation.Phase==RoundPhase.Playing&&!runtime.DemoSpectating,"Release switch denies role spoilers, watching and End Game even to the host");
            session.developmentReviewTools=true;
            foreach(string id in new[]{murderer,sheriff})
            {
                review.WatchPlayer(id);
                Check(id==runtime.LocalActorId?!runtime.DemoSpectating:runtime.DemoSpectating&&runtime.SpectatorRoot(runtime.OverrideCamera)==runtime.FindParticipantRoot(id),"Watch role selects that player's real first-person view: "+id);
            }
            runtime.SetDemoSpectating(false);
            Check(!runtime.CaptureFrameJson().Contains("developmentReview")&&!runtime.CaptureFrameJson().Contains("rolePreferences"),"Development review is absent from public match snapshots");
            var trailType=typeof(CandyMysteryTrails);var batchType=trailType.GetNestedType("Batch",BindingFlags.NonPublic);
            var oval=trailType.GetMethod("Oval",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(var scale in new[]{Vector3.one,new Vector3(4,2,8)})
            {
                var root=new GameObject("Footprint size fixture");root.transform.localScale=scale;root.transform.position=new Vector3(10,20,30);root.transform.rotation=Quaternion.Euler(0,35,0);
                var batch=Activator.CreateInstance(batchType,true);batchType.GetField("root").SetValue(batch,root.transform);
                var rotation=Quaternion.identity;var point=root.transform.position;
                oval.Invoke(null,new[]{batch,(object)(point+Vector3.forward*.075f),rotation,Vector3.up,.092f,.12f});
                oval.Invoke(null,new[]{batch,(object)(point-Vector3.forward*.075f),rotation,Vector3.up,.07f,.045f});
                var vertices=(List<Vector3>)batchType.GetField("vertices").GetValue(batch);var bounds=new Bounds(root.transform.TransformPoint(vertices[0]),Vector3.zero);
                foreach(var vertex in vertices)bounds.Encapsulate(root.transform.TransformPoint(vertex));
                Check(Mathf.Abs(bounds.size.x-.184f)<.001f&&Mathf.Abs(bounds.size.z-.315f)<.001f,"Footprint stays actual foot size on block scale "+scale);
                var old=root.transform.TransformPoint(vertices[0]);root.transform.position+=Vector3.up*5;
                Check(Vector3.Distance(root.transform.TransformPoint(vertices[0]),old+Vector3.up*5)<.001f,"Footprint follows its moving block");Object.Destroy(root);
            }
            int roundEnded=0;Action ended=()=>roundEnded++;GameEvents.RoundEnded+=ended;
            review.EndGame();GameEvents.RoundEnded-=ended;
            Check(runtime.Frame.phase==RoundPhase.Victory&&roundEnded==1&&runtime.LatestReport!=null,"End Game uses real victory event and captures the normal post-game report");
            simulation.Tick(.3f);Set(runtime,"nextSample",0f);Call(runtime,"Update");
            yield return null;
            var results=session.GetComponent<CandyResultsHud>();Call(results,"Update");
            Check(runtime.Frame.phase==RoundPhase.Results&&(bool)Field(results,"visible")&&session.MenuOpen,"Normal results screen opens after victory");
            Call(results,"Act",0);Check(simulation.Phase==RoundPhase.Countdown&&!session.MenuOpen,"Stay / Next Round starts the normal new-round path");
            Time.timeScale=1;runtime.ReturnToLobby();yield return Until(()=>IslandLobby.Current,"Return to Lobby uses actual scene flow");
            yield return new WaitForSecondsRealtime(.6f);
            var lobby=IslandLobby.Current;var room=lobby.Room;
            lobby.SelectMap("castle");lobby.SelectMode("demo");
            var config=NetworkProjectConfig.Global;var previous=config.PeerMode;NetworkRunner probe=null;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                var host=room.Host();yield return Until(()=>host.IsCompleted,"Photon host completes startup",50);if(host.IsFaulted)throw host.Exception;
                Check(room.IsHost,"Actual production Photon room hosts successfully: "+room.Status);
                var probeObject=new GameObject("Development travel client probe");Object.DontDestroyOnLoad(probeObject);probe=probeObject.AddComponent<NetworkRunner>();
                var secondary=probe;bool departure=false;int states=0;
                var callbacks=new NetworkDelegates();
                callbacks.OnReliableDataReceived=(runner,source,key,data)=>
                {
                    key.GetInts(out int channel,out int kind,out int serial,out _);
                    if(channel==0x44455054&&kind==1&&data.Length==4)departure=BitConverter.ToSingle(data.ToArray(),0)>0;
                    if(channel==0x49534C44&&kind==3){states++;runner.SendReliableDataToServer(ReliableKey.FromInts(channel,4,serial,0),Array.Empty<byte>());}
                };
                probe.AddCallbacks(callbacks);var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion=room.Region;app.AppVersion=(app.AppVersion??"")+"/"+lobby.catalog.NetworkVersion;
                var join=probe.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=room.Code,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=probeObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return Until(()=>join.IsCompleted,"Independent Photon client joins",50);Check(join.Result.Ok,"Two Photon peers connect to the actual room");
                probe.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,1,0,0),Array.Empty<byte>());
                yield return Until(()=>states>1,"Independent client acknowledges lobby setup");
                room.Launch();yield return Until(()=>room.Departing,"Host starts synchronized departure");
                yield return Until(()=>departure,"Departure packet reaches the second actual Fusion peer",1);
                Call(room,"CancelDeparture");yield return null;
                Check(!room.Departing&&!room.LaunchPending&&lobby.Player.transform.position.y<5,"Cancelled departure restores the lobby and its character");
                var stop=probe.Shutdown();yield return Until(()=>stop.IsCompleted,"Probe shuts down");Object.Destroy(probeObject);probe=null;
                var leave=room.Leave();yield return Until(()=>leave.IsCompleted&&!room.Connected,"Host room closes cleanly");
            }
            finally{if(probe){_=probe.Shutdown();Object.Destroy(probe.gameObject);}config.PeerMode=previous;}
            yield return new WaitForSecondsRealtime(1);
            lobby=IslandLobby.Current;room=lobby.Room;lobby.SelectMap("castle");lobby.SelectMode("demo");
            float baseY=lobby.Player.transform.position.y;int departures=0;Action<float> flight=seconds=>departures++;GameEvents.DepartureStarted+=flight;
            room.Launch();Check(room.Departing&&lobby.Player.InputSuspended,"Solo start pauses controls and begins flight rather than loading abruptly");
            yield return new WaitForSecondsRealtime(.5f);
            Check(IslandLobby.Current&&lobby.Player.transform.position.y>baseY+10&&lobby.Player.InputSuspended,"Lobby survivor actually launches upward");
            var curtain=Object.FindFirstObjectByType<CandySessionTravel>();Check(curtain,"Persistent white liquid transition exists");
            var material=(Material)Field(curtain,"liquid");Check(material&&material.GetFloat("_Progress")>.6f,"Flowing white flash covers view before map load");
            yield return Until(()=>Object.FindFirstObjectByType<CastlePlaySession>(),"Departure loads the selected real Game scene");
            yield return Until(()=>CandyAudioEngine.Instance&&CandyAudioEngine.Instance.GetComponentsInChildren<AudioSource>().Any(a=>a.clip&&a.clip.name.Contains("SpawnSlam")&&a.isPlaying),"Arrival plays the original candy slam through the pooled audio engine",2);
            yield return new WaitForSecondsRealtime(1.2f);GameEvents.DepartureStarted-=flight;
            session=Object.FindFirstObjectByType<CastlePlaySession>();
            Check(departures==1&&!room.Departing&&!Object.FindFirstObjectByType<CandySessionTravel>(),"Arrival clears departure state and removes the white curtain");
            Check(!(bool)Field(session,"travelInputHeld"),"Arrival restores input after the reveal");
            Check(errors==0,"No gameplay errors during integration");
        }
    }
    public sealed class MatchReviewTravelCheckDriver:MonoBehaviour {}
}

