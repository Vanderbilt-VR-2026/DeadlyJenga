using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LogType=UnityEngine.LogType;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class RoomFlowChecks
    {
        const string Key="DeadlyJenga.RoomFlowChecks";
        static string Report=>ProjectPath.Report("RoomFlowChecks.txt");
        static int errors;
        static bool failed;

        static RoomFlowChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    Application.runInBackground=true;
                    Application.logMessageReceived+=Log;
                    EditorApplication.LockReloadAssemblies();
                    var root=new GameObject("Room flow validation");
                    Object.DontDestroyOnLoad(root);
                    root.AddComponent<RoomFlowDriver>().StartCoroutine(Guard());
                }
                else if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;
                    SessionState.SetBool(Key,false);
                    EditorApplication.UnlockReloadAssemblies();
                    EditorApplication.Exit(failed||errors>0?2:0);
                }
            };
        }
        public static void RunBatch()
        {
            File.WriteAllText(Report,"Real Unity host + Photon transport probe (guest scene is not instantiated).\n");
            DeathPaintChecks.Run();
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));
            SessionState.SetBool(Key,true);
            EditorApplication.EnterPlaymode();
        }
        static void Log(string text,string trace,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            errors++;File.AppendAllText(Report,"ERROR "+text+"\n"+trace+"\n");
        }
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool next=false;object value=null;
                try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}
                catch(Exception error){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(value is IEnumerator child){stack.Push(child);continue;}
                yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            File.AppendAllText(Report,$"COMPLETE failed={failed} errors={errors}\n");
            EditorApplication.ExitPlaymode();
        }
        static void Check(bool value,string label)
        {
            if(!value)throw new Exception(label);
            File.AppendAllText(Report,"PASS "+label+"\n");
        }
        static IEnumerator Wait(Func<bool> condition,string label,float seconds=35)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!condition()&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(condition(),label);
        }
        static IEnumerator Run()
        {
            var lobby=IslandLobby.Current;
            Check(lobby&&lobby.Room,"Playable lobby starts");
            var room=lobby.Room;
            var host=room.Host();
            yield return Wait(()=>host.IsCompleted&&room.IsHost,"Production host opens a real Fusion room",45);
            string code=room.Code;
            var app=PhotonAppSettings.Global.AppSettings.GetCopy();
            app.FixedRegion=room.Region;
            app.AppVersion=(app.AppVersion??"")+"/"+room.Catalog.NetworkVersion;
            using(var listener=new IslandDiscovery(app.AppIdFusion,app.AppVersion))
            {
                room.InviteNearby();
                yield return Wait(()=>
                {
                    listener.Tick(Time.unscaledTime,"",1,false);
                    return listener.Invitation?.message.code==code;
                },"Actual UDP invite reaches a listener that had not advertised",8);
                listener.Dismiss();listener.Scan();
                yield return Wait(()=>
                {
                    listener.Tick(Time.unscaledTime,"",1,false);
                    return listener.Nearby.Any(e=>e.message.code==code);
                },"Actual UDP scan discovers the joinable Fusion room",8);
            }

            var peerRoot=new GameObject("Independent Photon client runner");
            Object.DontDestroyOnLoad(peerRoot);
            var peer=peerRoot.AddComponent<NetworkRunner>();
            var peerScenes=peerRoot.AddComponent<RoomSceneProbe>();
            string offeredMap=null;int offer=0,attempt=0;
            bool reply=false;
            var callbacks=new NetworkDelegates();
            callbacks.OnReliableDataReceived=(runner,sender,key,data)=>
            {
                key.GetInts(out int channel,out int kind,out int serial,out int packet);
                if(channel==0x49534C44&&kind==3)
                    runner.SendReliableDataToServer(ReliableKey.FromInts(channel,4,serial,0),Array.Empty<byte>());
                if(channel!=0x4D415053||kind!=1)return;
                offeredMap=System.Text.Encoding.UTF8.GetString(data);offer=serial;attempt=packet;
                if(reply)runner.SendReliableDataToServer(ReliableKey.FromInts(channel,2,offer,attempt),data.ToArray());
            };
            peer.AddCallbacks(callbacks);
            try
            {
                var join=peer.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=code,
                    EnableClientSessionCreation=false,CustomPhotonAppSettings=app,SceneManager=peerScenes,
                    Config=IslandRoomConnection.MigrationConfig()});
                yield return Wait(()=>join.IsCompleted,"Second Photon peer connects",45);
                Check(join.Result.Ok,"Guest joined using the actual room code");
                peer.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,1,0,0),Array.Empty<byte>());
                MatchSetup.BotTarget=2;
                room.SelectMode(Array.FindIndex(room.Catalog.modes,m=>m.id=="demo"));
                room.SelectMap(Array.FindIndex(room.Catalog.maps,m=>m.id=="castle"));
                room.Launch();
                CastlePlaySession first=null;
                yield return Wait(()=>(first=Object.FindFirstObjectByType<CastlePlaySession>())&&first.Network,
                    "Host launches actual Castle Demo with the second peer connected",55);
                yield return Wait(()=>peerScenes.IsAt("Game"),"Guest receives Fusion's Game scene selection");
                Check(MultiplayerPanel.JoinCode(first.Multiplayer)==code,"Pause menu resolves the live join code");

                var victim=first.Player.gameObject.GetComponent<IDamageable>();
                victim.Eliminate(null);
                Material Paint()
                {
                    var canvas=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                        .FirstOrDefault(c=>c.name=="Visual - liquid respawn curtain"&&c.enabled);
                    return canvas?canvas.GetComponentInChildren<Image>()?.material:null;
                }
                yield return Wait(()=>Paint()&&Paint().shader.name=="Candy Tropical/Death Paint"&&Paint().GetFloat("_Age")>=.28f,
                    "Actual local elimination reaches full paint before the next view",8);
                yield return Wait(()=>Paint()&&Paint().shader.name=="Candy Tropical/Death Paint"&&Paint().GetFloat("_Wipe")>.1f,
                    "Actual next player view starts the wipe",12);
                yield return Wait(()=>!Paint()||Paint().shader.name!="Candy Tropical/Death Paint","Death paint leaves the viewport",8);

                var target=room.Catalog.maps.First(m=>m.id!="castle"&&m.mapPrefab&&
                    m.SupportsMode("demo")&&m.scenePath==room.Catalog.maps[room.MapIndex].scenePath);
                var before=peerScenes.SceneVersion;
                ((IDemoMapControls)first.Demo).SelectMap(target.id);
                yield return Wait(()=>offeredMap==target.id&&offer>0,"Connected guest receives a versioned map offer");
                var payload=System.Text.Encoding.UTF8.GetBytes(target.id);
                peer.SendReliableDataToServer(ReliableKey.FromInts(0x4D415053,2,offer-1,attempt),payload);
                yield return new WaitForSecondsRealtime(.25f);
                Check(room.SwitchingMap&&MatchSetup.MapId!=target.id,"Stale ACK cannot start the new map");
                reply=true;
                peer.SendReliableDataToServer(ReliableKey.FromInts(0x4D415053,2,offer,attempt),payload);
                CastlePlaySession second=null;
                yield return Wait(()=>(second=Object.FindFirstObjectByType<CastlePlaySession>())&&second!=first&&second.Network&&
                    MatchSetup.MapId==target.id&&!room.SwitchingMap,"Host loads the new map even when it uses Game.unity",55);
                yield return Wait(()=>peerScenes.IsAt("Game")&&peerScenes.SceneVersion!=before,"Guest receives a new Fusion load for the same scene");
                Check(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(c=>
                    c is IWorldQuery&&c.transform.root.name==target.mapPrefab.name+"(Clone)"),"Selected map prefab becomes the live world");
                Check(room.Code==code&&room.HumanCount==2,"Both peers remain in the original room after map change");

                second.Modes.ReturnToLobby();
                yield return Wait(()=>IslandLobby.Current==lobby&&!lobby.MatchResident&&room.Connected,
                    "Host returns to the same live lobby without disconnecting",50);
                yield return Wait(()=>peerScenes.IsAt("Lobby"),"Guest receives the room-wide Lobby scene");
                Check(room.Code==code&&room.HumanCount==2,"Both peers keep the room and join code on return");
                var leave=peer.Shutdown();
                yield return Wait(()=>leave.IsCompleted,"Guest leaves cleanly");
                var close=room.Leave();
                yield return Wait(()=>close.IsCompleted&&!room.Connected,"Host closes the test room");
            }
            finally
            {
                if(peer&&peer.IsRunning)_=peer.Shutdown();
                if(peerRoot)Object.Destroy(peerRoot);
            }
        }
    }

    public sealed class RoomFlowDriver:MonoBehaviour {}

    public sealed class RoomSceneProbe:NetworkSceneManagerDefault
    {
        NetworkSceneInfo sceneInfo;
        public int SceneVersion=>sceneInfo.Version;
        public bool IsAt(string name)=>sceneInfo.SceneCount>0&&sceneInfo.Scenes[sceneInfo.SceneCount-1].IsIndex&&
            Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(sceneInfo.Scenes[sceneInfo.SceneCount-1].AsIndex))==name;
        public override bool OnSceneInfoChanged(NetworkSceneInfo next,NetworkSceneInfoChangeSource source)
        {sceneInfo=next;return true;}
    }
}
