using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastleSocial;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;
using Object=UnityEngine.Object;
namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class IslandLobbyValidation
    {
        const string Key="IslandLobbyValidation";
        static readonly List<string> lines=new List<string>();
        static IslandLobbyValidation()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=()=>
                {var lobby=Object.FindFirstObjectByType<IslandLobby>();lobby.Room.StartCoroutine(Guard(Check(lobby)));};
                if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);if(Application.isBatchMode)EditorApplication.Exit(lines.Any(x=>x.StartsWith("FAIL"))?1:0);}
            };
        }
        public static void RunBatch()
        {
            lines.Clear();Directory.CreateDirectory("ArtDirection/IslandLobby");File.WriteAllText(ProjectPath.Report("IslandLobbyValidation.txt"),"Island lobby runtime validation\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Assert(bool ok,string message)
        {lines.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(ProjectPath.Report("IslandLobbyValidation.txt"),lines);if(!ok)throw new Exception(message);}
        static IEnumerator Until(Func<bool> predicate,string label,float timeout=30)
        {float end=Time.realtimeSinceStartup+timeout;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Assert(predicate(),label);}
        static IEnumerator TaskDone(Task task,string label,float timeout=45)
        {yield return Until(()=>task.IsCompleted,label,timeout);if(task.IsFaulted)throw task.Exception;}
        static IEnumerator Guard(IEnumerator root)
        {
            var stack=new Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0)
            {
                bool next=false;object value=null;Exception error=null;
                try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){lines.Add("FAIL "+error);File.WriteAllLines(ProjectPath.Report("IslandLobbyValidation.txt"),lines);Debug.LogException(error);EditorApplication.ExitPlaymode();yield break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;
            }
        }
        static void Capture(string name,Vector3 position,Vector3 target,float fov=55)
        {
            var go=new GameObject("Lobby evidence camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.03f;camera.farClipPlane=2000;camera.fieldOfView=fov;camera.clearFlags=CameraClearFlags.Skybox;
            camera.transform.position=position;camera.transform.LookAt(target);var rt=new RenderTexture(1600,1000,24);var old=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var png=new Texture2D(1600,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1600,1000),0,0);png.Apply();File.WriteAllBytes("ArtDirection/IslandLobby/"+name+".png",png.EncodeToPNG());Object.Destroy(png);}
            finally{camera.targetTexture=null;RenderTexture.active=old;Object.Destroy(rt);Object.Destroy(go);}
        }
        static IEnumerator Check(IslandLobby lobby)
        {
            yield return new WaitForSeconds(1);lobby.SetMenu(true);
            Assert(lobby.Player&&lobby.Player.GetComponent<CandyPlayerAvatar>().Rig,"Current survivor prefab spawns a complete avatar on the island.");
            Assert(lobby.Player.Grounded,"Character settles on the actual 3D sand collider.");
            Assert(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)==1,"Only the shared character camera is active; no second XR rig.");
            Assert(lobby.catalog.maps.Length>=1&&lobby.catalog.maps[0].id=="castle"&&lobby.catalog.modes.Length==2,"Catalog retains Castle, Demo and Placeholder alongside additional maps.");
            Assert(!lobby.InvokeChoice("mode-placeholder")&&!lobby.InvokeChoice("launch"),"Mode, map and launch controls are locked before hosting.");
            Assert(lobby.Environment.profile!=lobby.catalog.art&&lobby.Environment.profile.islandRadius==8,"Lobby uses a runtime art profile; castle art stays unchanged.");
            Assert(lobby.Environment.sandRenderer.GetComponent<MeshCollider>()&&!lobby.Environment.sandRenderer.GetComponent<Rigidbody>(),"Sand is 3D static collision terrain, with no island rigidbody.");
            Assert(!lobby.Voice.MicrophoneInput.Capturing&&lobby.Voice.Settings.mode==SocialOperatingMode.Offline,"Lobby voice is opt-in and independent of AI inference.");
            for(int i=0;i<100;i++){string address=IslandRoomAddress.Create("us");AssertAddress(address);}
            Assert(!IslandRoomAddress.TryParse("BAD","us",out _,out _)&&!IslandRoomAddress.TryParse("xx-ABC234","us",out _,out _),"Invalid code and unknown region are rejected.");
            Capture("01-island-overview",new Vector3(10,7,-12),new Vector3(0,.5f,0));
            Capture("02-terminals-idle",new Vector3(0,1.6f,-3),new Vector3(0,1.55f,.5f),75);
            var config=NetworkProjectConfig.Global;var original=config.PeerMode;NetworkRunner probe=null;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                yield return TaskDone(lobby.Room.Host(),"Real Photon host connection completes");Assert(lobby.Room.IsHost,"Photon host room is running: "+lobby.Room.Status);
                Assert(lobby.Room.Code.StartsWith("US-")&&lobby.Room.CanConfigure,"Host gets a region-qualified code and unlocked terminals.");
                Assert(lobby.InvokeChoice("mode-placeholder")&&!lobby.Room.CanLaunch,"Placeholder can be selected but cannot launch unfinished gameplay.");
                lobby.InvokeChoice("mode-demo");Assert(lobby.Room.CanLaunch,"Castle Demo is launchable by the host.");
                var probeObject=new GameObject("Photon lobby client probe");probe=probeObject.AddComponent<NetworkRunner>();
                var callbacks=new NetworkDelegates();int snapshots=0,lastMode=-1;bool authenticated=false,gotAudio=false;
                var primary=lobby.Room;var secondary=probe;
                callbacks.OnReliableDataReceived=(runner,source,key,data)=>
                {
                    key.GetInts(out int channel,out int kind,out int id,out _);
                    if(channel==0x49534C44&&kind==3)
                    {
                        using var reader=P.Reader(data.ToArray(),P.World);reader.ReadInt32();lastMode=reader.ReadInt32();int count=P.Count(reader,10);
                        for(int i=0;i<count;i++){var actor=P.ReadActor(reader);if(actor.id==secondary.LocalPlayer.ToString())authenticated=true;}
                        snapshots++;secondary.SendReliableDataToServer(ReliableKey.FromInts(channel,4,id,0),Array.Empty<byte>());
                    }
                    if(channel==0x49534C56&&kind==1){gotAudio=SocialAudioPacket.Decode(data.ToArray()).samples.Length>0;secondary.SendReliableDataToServer(ReliableKey.FromInts(channel,2,id,0),Array.Empty<byte>());}
                };
                probe.AddCallbacks(callbacks);var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion=primary.Region;app.AppVersion=(app.AppVersion??"")+"/"+lobby.catalog.NetworkVersion;
                var join=probe.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=primary.Code,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,SceneManager=probeObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return TaskDone(join,"Independent Photon client joins the host code");Assert(join.Result.Ok,"Client joins the same Fusion session: "+join.Result.ShutdownReason);
                secondary.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,1,0,0),Array.Empty<byte>());
                yield return Until(()=>snapshots>0,"Client receives lobby welcome before sending pose");
                var actorData=lobby.Capture("forged-id");var frame=actorData.avatar;Vector3 delta=new Vector3(-1.6f,0,.4f);frame.feet+=delta;frame.head+=delta;frame.left.position+=delta;frame.right.position+=delta;actorData.avatar=frame;
                secondary.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,2,1,0),P.Encode(P.Player,w=>P.Write(w,actorData)));
                yield return Until(()=>snapshots>0&&authenticated&&lobby.PeerCount==1,"Real lobby pose replicates and host replaces a forged player ID");
                primary.SelectMode(1);yield return Until(()=>lastMode==1,"Host mode selection replicates to the client");primary.SelectMode(0);
                lobby.Voice.SendAudio(new SocialAudioPacket{sequence=1,samples=Enumerable.Range(0,800).Select(i=>(float)Math.Sin(i*.2)*.1f).ToArray()},true);
                yield return Until(()=>gotAudio,"Actual PCM voice data crosses the Fusion lobby channel");
                secondary.SendReliableDataToServer(ReliableKey.FromInts(0x49534C56,1,3,0),new SocialAudioPacket{speaker="forged-voice",sequence=2,samples=new float[800]}.Encode());
                yield return Until(()=>lobby.Voice.Playback.SpeakerCount>0,"Incoming client voice reaches the shared playback system");
                yield return new WaitForSeconds(.2f);Capture("03-host-terminals-lit",new Vector3(0,1.6f,-3),new Vector3(0,1.55f,.5f),75);
                Capture("04-connection-terminal",new Vector3(-2.6f,1.55f,-2),new Vector3(-2.6f,1.55f,.3f),44);
                yield return TaskDone(probe.Shutdown(),"Client leaves cleanly");Object.Destroy(probeObject);probe=null;
                yield return Until(()=>lobby.PeerCount==0,"Disconnected guest avatar is removed");
                yield return TaskDone(primary.Leave(),"Host can close the room");Assert(!primary.Connected&&!primary.Busy&&!primary.HostSelected,"Leave resets the lobby for another connection attempt.");
                yield return TaskDone(primary.Join("BAD"),"Malformed code returns immediately");Assert(!primary.Busy&&!primary.Connected,"Invalid join does not create a room or lock the controls.");
                var pending=primary.Join(IslandRoomAddress.Create(primary.Region));yield return null;
                Assert(lobby.InvokeChoice("leave"),"Connecting UI exposes a cancel action");
                yield return TaskDone(pending,"Cancelled join releases its startup task");
                yield return Until(()=>!primary.Busy&&!primary.Connected,"Cancelled connection leaves the island ready to retry");
            }
            finally{if(probe){_=probe.Shutdown();Object.Destroy(probe.gameObject);}config.PeerMode=original;}
            yield return TaskDone(lobby.Room.Host(),"Host reconnects with a fresh runner in normal single-peer mode");Assert(lobby.Room.IsHost,"Rehosting succeeds after leaving.");
            var connection=lobby.Room;lobby.InvokeChoice("launch");
            yield return Until(()=>Object.FindFirstObjectByType<CastlePlaySession>()&&Object.FindFirstObjectByType<CastlePlaySession>().Network,"Host launch enters the actual castle Game scene",60);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();yield return new WaitForSeconds(1);
            Assert(!IslandLobby.Current&&session.Network.IsFusion&&connection.IsHost,"Same host runner survives lobby -> castle without duplicate lobby characters.");
            Assert(session.GetComponent<CandyDemoHud>()&&session.GetComponent<SocialWorld>(),"Original detailed in-game demo HUD and social voice system remain installed.");
            Assert(session.Bots.SpawnBot()&&session.HandBots.SpawnHand(),"Host can still spawn a survivor and giant hand in Castle Demo.");
            var menu=Object.FindFirstObjectByType<CastleWorldMenu>(FindObjectsInactive.Include);session.SetMenu(true);menu.Show(true,session.Player.viewCamera);
            var buttons=(UnityEngine.UI.Button[])typeof(CastleWorldMenu).GetField("buttons",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
            Assert(buttons.Length==14,"Headset demo menu retains original controls and adds host tuning.");
            session.Player.viewCamera.transform.LookAt(buttons[13].transform);menu.Point(session.Player.viewCamera,true);menu.Point(session.Player.viewCamera,false);
            Assert((bool)typeof(CastleWorldMenu).GetField("tuningPage",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu),"Gaze/trigger opens the in-game host tuning page.");
            float capacity=session.Player.climbGripSeconds;session.Player.viewCamera.transform.LookAt(buttons[1].transform);menu.Point(session.Player.viewCamera,true);
            Assert(session.Player.climbGripSeconds>capacity,"Host tuning modifies the live stamina capacity in game.");menu.Show(false,session.Player.viewCamera);
            yield return TaskDone(connection.Leave(),"Leaving Castle Demo shuts down the same room");
            yield return Until(()=>IslandLobby.Current&&IslandLobby.Current.Player,"Disconnect returns to the island with a fresh local character",60);
            Assert(!connection.Connected&&!Object.FindFirstObjectByType<CastlePlaySession>(),"No stale game session or runner remains after return.");
            File.WriteAllLines(ProjectPath.Report("IslandLobbyValidation.txt"),lines);EditorApplication.ExitPlaymode();
        }
        static void AssertAddress(string code)
        {if(!IslandRoomAddress.TryParse(code.ToLowerInvariant(),"eu",out var region,out var parsed)||region!="us"||parsed!=code)throw new Exception("Room-code round-trip failed");}
    }
}
