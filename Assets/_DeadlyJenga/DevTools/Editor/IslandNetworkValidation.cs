using DeadlyJenga.Core;
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
using UnityEditor.Compilation;
using UnityEngine;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastleSocial;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;
using Object=UnityEngine.Object;
namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class IslandNetworkValidation
    {
        const string Key="IslandNetworkValidation";
        static readonly List<string> lines=new List<string>();
        static readonly List<string> errors=new List<string>();
        static bool running,expectCancelledJoin;
        static readonly List<Action<object>> pausedCompileHooks=new List<Action<object>>();
        static void PauseCompileShutdown()
        {
            // Other chats can compile while this test uses the locked, already-loaded assemblies.
            var field=typeof(CompilationPipeline).GetField("compilationStarted",BindingFlags.Static|BindingFlags.NonPublic);
            if(!(field?.GetValue(null) is Delegate handlers))return;
            foreach(var callback in handlers.GetInvocationList())
                if(callback.Method.DeclaringType?.FullName?.StartsWith("Fusion.Editor.EditorRecompileHook")==true&&callback is Action<object> action)
                {CompilationPipeline.compilationStarted-=action;pausedCompileHooks.Add(action);}
        }
        static IslandNetworkValidation()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {running=true;SessionState.SetBool(Key+"Running",true);EditorApplication.delayCall+=()=>
                    {AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();PauseCompileShutdown();SessionState.SetBool(Key+"Locked",true);SessionState.SetBool(Key+"Frozen",true);errors.Clear();Application.logMessageReceived+=RecordError;Debug.Log("Island network runtime checks started");var lobby=Object.FindFirstObjectByType<IslandLobby>();lobby.Room.StartCoroutine(Guard(Check(lobby)));};}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=RecordError;
                    SessionState.SetBool(Key+"Running",false);
                    if(SessionState.GetBool(Key+"Frozen",false)){SessionState.SetBool(Key+"Frozen",false);AssetDatabase.AllowAutoRefresh();}
                    if(SessionState.GetBool(Key+"Restart",false)){SessionState.SetBool(Key+"Restart",false);RunBatch();return;}
                    SessionState.SetBool(Key,false);if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)||lines.Any(x=>x.StartsWith("FAIL"))?1:0);
                }
            };
            EditorApplication.delayCall+=()=>
            {
                if(!SessionState.GetBool(Key,false)||SessionState.GetBool(Key+"Done",false)||running)return;
                if(EditorApplication.isPlaying&&SessionState.GetBool(Key+"Running",false)){SessionState.SetBool(Key+"Restart",true);EditorApplication.ExitPlaymode();}
                else if(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling)EditorApplication.EnterPlaymode();
            };
        }
        static void RecordError(string message,string stack,UnityEngine.LogType type)
        {if(NetworkProjectConfig.Global.PeerMode==NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig."))return;if(expectCancelledJoin&&message.Contains("StartGame Failed:")&&message.Contains("ShutdownReason: OperationCanceled"))return;if(type==UnityEngine.LogType.Exception||type==UnityEngine.LogType.Error||type==UnityEngine.LogType.Assert)errors.Add(message);}
        static void Finish(bool failed=false)
        {SessionState.SetBool(Key+"Done",true);SessionState.SetBool(Key+"Failed",failed);foreach(var hook in pausedCompileHooks)CompilationPipeline.compilationStarted+=hook;pausedCompileHooks.Clear();if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);EditorApplication.UnlockReloadAssemblies();}EditorApplication.ExitPlaymode();}
        public static void RunBatch()
        {
            var link=AssetDatabase.FindAssets("FusionMigration.link").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>Path.GetFileName(p)=="FusionMigration.link.xml");
            if(link!=null){string error=AssetDatabase.MoveAsset(link,Path.GetDirectoryName(link).Replace('\\','/')+"/link.xml");if(error.Length>0)throw new Exception(error);}
            errors.Clear();
            lines.Clear();Directory.CreateDirectory("ArtDirection/IslandLobby");File.WriteAllText(ProjectPath.Report("IslandNetworkValidation.txt"),"Island lobby runtime validation\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);SessionState.SetBool(Key+"Running",false);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Assert(bool ok,string message)
        {lines.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(ProjectPath.Report("IslandNetworkValidation.txt"),lines);if(!ok)throw new Exception(message);}
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
                if(error!=null){lines.Add("FAIL "+error);File.WriteAllLines(ProjectPath.Report("IslandNetworkValidation.txt"),lines);Debug.LogException(error);Finish(true);yield break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;
            }
        }
        static void Capture(string name,Vector3 position,Vector3 look,float fov=55){}
        static IEnumerator Check(IslandLobby lobby)
        {
            yield return new WaitForSeconds(1);lobby.SetMenu(true);
            Assert(lobby.Player&&lobby.Player.GetComponent<CandyPlayerAvatar>().Rig,"Current survivor prefab spawns a complete avatar on the island.");
            Assert(lobby.Player.Grounded,"Character settles on the actual 3D sand collider.");
            Assert(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)==1,"Only the shared character camera is active; no second XR rig.");
            Assert(lobby.catalog.maps.Length>=1&&lobby.catalog.maps[0].id=="castle"&&lobby.catalog.modes.Length==2,"Catalog retains Castle, Demo and Placeholder alongside additional maps.");
            var catalogCopy=Object.Instantiate(lobby.catalog);string networkVersion=catalogCopy.NetworkVersion;
            catalogCopy.maps=(IslandLobbyCatalog.Map[])catalogCopy.maps.Clone();
            catalogCopy.maps=catalogCopy.maps.Concat(new[]{new IslandLobbyCatalog.Map{id="integration-only-map"}}).ToArray();
            Assert(catalogCopy.NetworkVersion!=networkVersion,"Catalog additions change network compatibility instead of reinterpreting older map indices");Object.Destroy(catalogCopy);
            Assert(!lobby.InvokeChoice("mode-placeholder")&&!lobby.InvokeChoice("launch"),"Mode, map and launch controls are locked before hosting.");
            Assert(lobby.Environment.profile!=lobby.catalog.art&&lobby.Environment.profile.islandRadius==8,"Lobby uses a runtime art profile; castle art stays unchanged.");
            Assert(lobby.Environment.sandRenderer.GetComponent<MeshCollider>()&&!lobby.Environment.sandRenderer.GetComponent<Rigidbody>(),"Sand is 3D static collision terrain, with no island rigidbody.");
            Assert(!lobby.Voice.MicrophoneInput.Capturing&&lobby.Voice.Settings.mode==SocialOperatingMode.Offline,"Lobby voice is opt-in and independent of AI inference.");
            for(int i=0;i<100;i++){string address=IslandRoomAddress.Create("us");AssertAddress(address);}
            Assert(!IslandRoomAddress.TryParse("BAD","us",out _,out _)&&!IslandRoomAddress.TryParse("xx-ABC234","us",out _,out _),"Invalid code and unknown region are rejected.");
            Capture("01-island-overview",new Vector3(10,7,-12),new Vector3(0,.5f,0));
            Capture("02-terminals-idle",new Vector3(0,1.6f,-3),new Vector3(0,1.55f,.5f),75);
            var appId=PhotonAppSettings.Global.AppSettings.AppIdFusion;string protocol=(PhotonAppSettings.Global.AppSettings.AppVersion??"")+"/"+lobby.catalog.NetworkVersion;
            Assert(lobby.Room.Discovery!=null,"LAN discovery starts without legacy TCP networking");
            using(var a=new IslandDiscovery(appId,protocol,0))using(var b=new IslandDiscovery(appId,protocol,0))using(var wrong=new IslandDiscovery("other-game",protocol,0))
            {
                var endpoint=new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,47738);var bytes=a.EncodePresence("US-ABC234",2);
                Assert(b.Receive(bytes,endpoint,1)&&b.Nearby.Count==1,"Compatible LAN advertisement resolves a normal Fusion address");
                Assert(!wrong.Receive(bytes,endpoint,1),"Other Photon applications are filtered out of nearby rooms");
                Assert(!b.Receive(new byte[2048],endpoint,1)&&!b.Receive(System.Text.Encoding.UTF8.GetBytes("not-json"),endpoint,1),"Malformed and oversized discovery packets are rejected");
                b.Tick(7,"",1,false);Assert(b.Nearby.Count==0,"Stale nearby rooms expire");
            }
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
                var join=probe.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=primary.Code,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=probeObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return TaskDone(join,"Independent Photon client joins the host code");Assert(join.Result.Ok,"Client joins the same Fusion session: "+join.Result.ShutdownReason);
                secondary.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,1,0,0),Array.Empty<byte>());
                yield return Until(()=>snapshots>0,"Client receives lobby welcome before sending pose");
                var actorData=lobby.Capture("forged-id");DeadlyJenga.CandyPlayers.CandyAvatarFrame frame;int forgedColor;using(var stateReader=DeadlyJenga.CastlePrototype.CastleLanProtocol.StateReader(actorData))DeadlyJenga.CharacterNetwork.CharacterWire.ReadSurvivor(stateReader,out frame,out forgedColor);Vector3 delta=new Vector3(-1.6f,0,.4f);frame.feet+=delta;frame.head+=delta;frame.left.position+=delta;frame.right.position+=delta;using(var stateStream=new System.IO.MemoryStream()){using(var stateWriter=new System.IO.BinaryWriter(stateStream))DeadlyJenga.CharacterNetwork.CharacterWire.WriteSurvivor(stateWriter,frame,forgedColor);actorData.state=stateStream.ToArray();}
                secondary.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,2,1,0),P.Encode(P.Player,w=>P.Write(w,actorData)));
                yield return Until(()=>snapshots>0&&authenticated&&lobby.PeerCount==1,"Real lobby pose replicates and host replaces a forged player ID");
                using(var observer=new IslandDiscovery(appId,protocol))
                {
                    yield return Until(()=>{observer.Tick(Time.unscaledTime,"",1,false);return observer.Nearby.Any(e=>e.message.code==primary.Code);},"Actual UDP broadcast discovers the running Fusion room",8);
                    primary.InviteNearby();yield return Until(()=>{observer.Tick(Time.unscaledTime,"",1,false);return observer.Invitation!=null;},"Host invitation reaches another app listener over UDP",8);
                    observer.Dismiss();Assert(observer.Invitation==null,"Invitation can be declined without joining");
                }
                Assert(lobby.InvokeChoice("shared-controls")&&primary.SharedDemoControls,"Host enables shared Demo powers from the terminal");
                primary.SelectMode(1);yield return Until(()=>lastMode==1,"Host mode selection replicates to the client");primary.SelectMode(0);primary.Launch();
                Assert(primary.LaunchPending&&IslandLobby.Current,"Start waits until clients acknowledge the newest mode and map selection");primary.CancelLaunch();
                Assert(!primary.LaunchPending&&primary.CanConfigure,"Host can cancel a pending start and edit the lobby again");
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
                expectCancelledJoin=true;var pending=primary.Join(IslandRoomAddress.Create(primary.Region));yield return null;
                Assert(lobby.InvokeChoice("leave"),"Connecting UI exposes a cancel action");
                yield return TaskDone(pending,"Cancelled join releases its startup task");
                yield return Until(()=>!primary.Busy&&!primary.Connected,"Cancelled connection leaves the island ready to retry");expectCancelledJoin=false;
                probeObject=new GameObject("Migration host probe");probe=probeObject.AddComponent<NetworkRunner>();secondary=probe;
                var hostCallbacks=new NetworkDelegates();
                hostCallbacks.OnReliableDataReceived=(runner,source,key,data)=>
                {
                    key.GetInts(out int channel,out int kind,out int serial,out _);if(channel!=0x49534C44)return;
                    if(kind==1)
                    {
                        runner.SendReliableDataToPlayer(source,ReliableKey.FromInts(channel,5,0,0),P.Encode(P.Player,w=>P.Write(w,new P.Pose(IslandLobby.SpawnPoint(source.RawEncoded),Quaternion.identity))));
                        runner.SendReliableDataToPlayer(source,ReliableKey.FromInts(channel,3,9,0),P.Encode(P.World,w=>{w.Write(0);w.Write(0);w.Write(0);w.Write(true);}));
                    }
                    if(kind==2)runner.SendReliableDataToPlayer(source,ReliableKey.FromInts(channel,4,serial,0),Array.Empty<byte>());
                };
                probe.AddCallbacks(hostCallbacks);string migrationCode=IslandRoomAddress.Create(primary.Region);
                var hostStart=probe.StartGame(new StartGameArgs{GameMode=GameMode.Host,SessionName=migrationCode,PlayerCount=10,CustomPhotonAppSettings=app,Config=IslandRoomConnection.MigrationConfig(),Scene=new NetworkSceneInfo(),SceneManager=probeObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return TaskDone(hostStart,"Independent migration host starts");Assert(hostStart.Result.Ok,"Migration host connected");
                yield return TaskDone(primary.Join(migrationCode),"Production client joins the migration host");
                yield return Until(()=>primary.SharedDemoControls,"Host lobby permissions reach the actual client");
                Assert(!primary.IsHost&&Vector3.Distance(lobby.Player.transform.position,IslandLobby.SpawnPoint(primary.Runner.LocalPlayer.RawEncoded))<1,"Joining client spawns at the host-assigned island spot");
                var snapshot=probe.PushHostMigrationSnapshot();yield return TaskDone(snapshot,"Host uploads migration snapshot");Assert(snapshot.Result,"Photon accepts the migration snapshot");
                yield return new WaitForSeconds(3);var oldRunner=primary.Runner;
                yield return TaskDone(probe.Shutdown(),"Original host leaves");Object.Destroy(probeObject);probe=null;
                yield return Until(()=>primary.IsHost&&!primary.Busy&&primary.Runner!=oldRunner,"Remaining player automatically becomes host using the production migration path",65);
                Assert(primary.Code==migrationCode&&primary.SharedDemoControls,"Host handover preserves room code and shared Demo permissions");
                yield return TaskDone(primary.Leave(),"Migrated host leaves cleanly");

            }
            finally{if(probe){_=probe.Shutdown();Object.Destroy(probe.gameObject);}config.PeerMode=original;}
            yield return TaskDone(lobby.Room.Host(),"Host reconnects with a fresh runner in normal single-peer mode");Assert(lobby.Room.IsHost,"Rehosting succeeds after leaving.");
            var connection=lobby.Room;lobby.InvokeChoice("launch");
            yield return Until(()=>Object.FindFirstObjectByType<CastlePlaySession>()&&Object.FindFirstObjectByType<CastlePlaySession>().Network,"Host launch enters the actual castle Game scene",60);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();yield return new WaitForSeconds(1);
            Assert(!IslandLobby.Current&&session.Network.IsFusion&&connection.IsHost,"Same host runner survives lobby -> castle without duplicate lobby characters.");
            Assert(session.GetComponent<CandyDemoHud>()&&session.GetComponent<SocialWorld>(),"Original detailed in-game demo HUD and social voice system remain installed.");
            Assert(session.Bots.SpawnBot()&&session.HandBots.SpawnHand(),"Host can still spawn a survivor and giant hand in Castle Demo.");
            using(var sender=new IslandDiscovery(appId,protocol,0))
            {
                var invite=JsonUtility.FromJson<IslandDiscovery.Message>(System.Text.Encoding.UTF8.GetString(sender.EncodePresence(IslandRoomAddress.Create("us"),1)));
                invite.kind="invite";invite.target=connection.Discovery.Id;
                connection.Discovery.Receive(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(invite)),new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,IslandDiscovery.Port),Time.unscaledTime);
                Assert(session.Network.HasPending,"A nearby invitation reaches the in-game Friends notification state");connection.Discovery.Dismiss();
            }
            var sync=session.GetComponent<CastleFusionSession>();Assert(sync.CanUseDemoControls&&!connection.SharedDemoControls,"Only the host has Demo powers by default");
            sync.SetSharedControls(true);Assert(connection.SharedDemoControls,"In-game host grants everyone Demo powers");sync.SetSharedControls(false);
            var proxyObject=new GameObject("Temporary remote hazard proxy");
            var proxyContext=new NetworkContext{host=true,localSurvivor=session.Player.gameObject,localHand=session.Hand.gameObject,actorId="integration-peer",sendImpact=(id,push,damage)=>sync.QueueDisasterImpact(id,push,damage)};
            var proxyView=NetworkActors.CreateView(NetworkKinds.Survivor,proxyObject,proxyContext);
            byte[] proxyState;using(var stream=new System.IO.MemoryStream()){using(var writer=new System.IO.BinaryWriter(stream)){foreach(var local in NetworkActors.Local)if(local.NetworkKind==NetworkKinds.Survivor){local.WriteState(writer);break;}}proxyState=stream.ToArray();}
            using(var reader=new System.IO.BinaryReader(new System.IO.MemoryStream(proxyState)))proxyView.ReadState(reader,true);
            var proxy=(Component)proxyView;
            Assert(proxy.GetComponentsInChildren<CapsuleCollider>().Any(c=>c.enabled&&c.isTrigger)&&proxy.GetComponentsInChildren<Camera>(true).Length==0,"Remote survivor exposes a host hazard target without owning a camera");
            var impacts=(System.Collections.IDictionary)typeof(CastleFusionSession).GetField("disasterImpacts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sync);
            ((IPushable)proxyView).Push(Vector3.forward,proxyObject);Assert(impacts.Contains("integration-peer"),"Host remote-survivor impact is routed to the owning player");impacts.Clear();proxyView.Suspend();((IPushable)proxyView).Push(Vector3.forward,proxyObject);
            Assert(!impacts.Contains("integration-peer")&&!proxy.GetComponentsInChildren<CapsuleCollider>().Any(c=>c.enabled),"Suspended remote actors cannot receive stale disaster impacts");Object.Destroy(proxyObject);
            Assert(DemoDisasters.Execute(session,"Tsunami")&&DeadlyJenga.Core.Tracked.Waves.Any(w=>w.Wave.active),"A real host disaster publishes active shared wave state");
            var first=DeadlyJenga.Core.Services.World.Blocks[0];var originalPosition=first.position;
            sync.Demo.target=14;sync.Demo.handLimit=7;sync.Demo.autoFill=false;
            sync.PauseForMigration();Assert(DeadlyJenga.Core.Tracked.Waves.Count==0,"Host migration cancels active disasters and clears wave sources");Assert(first.isKinematic,"Physics freezes while host transfer is in progress");sync.ResumeAfterMigration(connection.Runner);
            Assert(!first.isKinematic&&Vector3.Distance(first.position,originalPosition)<.01f,"Host resume retains block positions and restores physics authority");
            Assert(session.Bots.targetPopulation==14&&session.Bots.Capacity==int.MaxValue,"Host handover preserves the fill target and uncapped spawning");
            int beforeBots=session.Bots.Count;sync.Request(new CastleDemoCommand{action=CastleDemoAction.AddSurvivor});Assert(session.Bots.Count==beforeBots+1,"Demo command path executes through the authority");
            // Exercise the exact custom-state promotion used after native migration, with fallen geometry.
            first.position+=new Vector3(.35f,.2f,.15f);var fallenPosition=first.position;
            byte[] preserved=session.Network.FusionCaptureWorld();byte[] welcome=session.Network.FusionWelcome(connection.Runner.LocalPlayer.ToString());
            session.Network.InitializeFusion(session,connection.Runner.LocalPlayer.ToString(),true);
            session.Network.FusionReceiveWelcome(welcome);session.Network.FusionApplyWorld(preserved);
            first=DeadlyJenga.Core.Services.World.Blocks[0];
            Assert(first.isKinematic,"A client replica has no independent block simulation");
            sync.PauseForMigration();sync.ResumeAfterMigration(connection.Runner);
            Assert(!first.isKinematic&&Vector3.Distance(DeadlyJenga.Core.Services.World.Blocks[0].position,fallenPosition)<.01f,"Client-to-host promotion restores the last fallen-block arrangement instead of rebuilding the castle");
            Assert(session.GetComponent<SocialWorld>().IsAuthority,"AI voice authority follows the promoted physics host");

            var menu=Object.FindFirstObjectByType<CastleWorldMenu>(FindObjectsInactive.Include);session.SetMenu(true);menu.Show(true,session.Player.viewCamera);
            var buttons=(UnityEngine.UI.Button[])typeof(CastleWorldMenu).GetField("buttons",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
            Assert(buttons.Length>=14,"Headset demo menu retains original controls and host tuning alongside new pages.");
            session.Player.viewCamera.transform.LookAt(buttons[13].transform);menu.Point(session.Player.viewCamera,true);menu.Point(session.Player.viewCamera,false);
            Assert((bool)typeof(CastleWorldMenu).GetField("tuningPage",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu),"Gaze/trigger opens the in-game host tuning page.");
            float capacity=session.Player.climbGripSeconds;session.Player.viewCamera.transform.LookAt(buttons[1].transform);menu.Point(session.Player.viewCamera,true);
            Assert(session.Player.climbGripSeconds>capacity,"Host tuning modifies the live stamina capacity in game.");menu.Show(false,session.Player.viewCamera);
            yield return TaskDone(connection.Leave(),"Leaving Castle Demo shuts down the same room");
            yield return Until(()=>IslandLobby.Current&&IslandLobby.Current.Player,"Disconnect returns to the island with a fresh local character",60);
            Assert(!connection.Connected&&!Object.FindFirstObjectByType<CastlePlaySession>(),"No stale game session or runner remains after return.");
            Assert(errors.Count==0,"Runtime completes without unexpected Unity errors or exceptions: "+string.Join("; ",errors));
            File.WriteAllLines(ProjectPath.Report("IslandNetworkValidation.txt"),lines);Finish();
        }
        static void AssertAddress(string code)
        {if(!IslandRoomAddress.TryParse(code.ToLowerInvariant(),"eu",out var region,out var parsed)||region!="us"||parsed!=code)throw new Exception("Room-code round-trip failed");}
    }
}
