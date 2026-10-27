using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Modes;
using DeadlyJenga.Presentation;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using LogType=UnityEngine.LogType;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class LobbyFlowChecks
    {
        const string Key="LobbyFlowChecks";
        static string Report=>ProjectPath.Report(SessionState.GetBool(Key+"PreviewOnly",false)?"WardrobePreview.txt":"LobbyFlow.txt");
        static int errors;static NetworkRunner probe;
        static LobbyFlowChecks()
        {
            EditorApplication.update+=()=>
            {
                string pid=System.Diagnostics.Process.GetCurrentProcess().Id.ToString();var preview=ProjectPath.Report("WardrobePreviewExpansionMain.request");bool only=File.Exists(preview)&&File.ReadAllText(preview).Trim()==pid+":isolated1";
                var request=only?preview:ProjectPath.Report("LobbyFlowExpansionMain.request");
                if(File.Exists(request)&&(only||File.ReadAllText(request).Trim()==pid+":expanded4")&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){File.Delete(request);SessionState.SetBool(Key+"PreviewOnly",only);Run();}
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){errors=0;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>{var root=new GameObject("Lobby flow check");Object.DontDestroyOnLoad(root);root.AddComponent<LobbyFlowDriver>().StartCoroutine(Guard(Check()));};}
                if(state==PlayModeStateChange.EnteredEditMode){Unlock();Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);SessionState.SetBool(Key+"PreviewOnly",false);string original=SessionState.GetString(Key+"Original","");if(File.Exists(original))EditorSceneManager.OpenScene(original);}
            };
        }
        static void Unlock(){if(!SessionState.GetBool(Key+"Locked",false))return;SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
        [MenuItem("Tools/Deadly Jenga/Validation/Check Lobby Flow")]
        public static void Run()
        {
            File.WriteAllText(Report,SessionState.GetBool(Key+"PreviewOnly",false)?"Isolated wardrobe render check\n":"Expanded lobby v4: offline bots, customizer, identity and two-runner checks\n");SessionState.SetString(Key+"Original",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(NetworkProjectConfig.Global.PeerMode==NetworkProjectConfig.PeerModes.Multiple&&message.Contains("PeerModes.Multiple requires a scene"))return;if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Require(bool ok,string message){File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        static IEnumerator Guard(IEnumerator test)
        {
            var stack=new Stack<IEnumerator>();stack.Push(test);
            while(stack.Count>0)
            {
                bool more=false;object value=null;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            if(probe){FusionMigrationCleanup.BeforeShutdown(probe);var shutdown=probe.Shutdown();while(!shutdown.IsCompleted)yield return null;if(probe)Object.Destroy(probe.gameObject);}
            if(IslandRoomConnection.Instance&&IslandRoomConnection.Instance.Connected){var leave=IslandRoomConnection.Instance.Leave();while(!leave.IsCompleted)yield return null;}
            EditorApplication.ExitPlaymode();
        }
        static IEnumerator Until(Func<bool> check,string label,float seconds=35)
        {float until=Time.realtimeSinceStartup+seconds;while(!check()&&Time.realtimeSinceStartup<until)yield return null;Require(check(),label);}
        static IEnumerator Done(Task task,string label,float seconds=45){yield return Until(()=>task.IsCompleted,label,seconds);if(task.IsFaulted)throw task.Exception;}
        static IEnumerator Check()
        {
            yield return new WaitForSecondsRealtime(1);
            var lobby=IslandLobby.Current;
            yield return Until(()=>lobby.SurvivorBots?.Count==9,"Offline lobby fills to nine bots plus the local player");
            var room=lobby.Room;var details=(ILobbyDetails)lobby;var view=lobby.View();lobby.SetMenu(true);
            Require(!room.Connected&&lobby.IsHost&&lobby.CanConfigure&&lobby.CanStart&&lobby.JoinEnabled,"Solo arrival can start and configure immediately while JOIN remains available, without a Fusion room");
            view.SetWardrobeOpen(true);yield return null;
            Require((bool)typeof(IslandLobbyView).GetField("wardrobeOpen",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view),"Customizer opens on the actual lobby avatar");
            typeof(IslandLobbyView).GetMethod("SetWardrobeCategory",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,new object[]{1});
            if(SessionState.GetBool(Key+"PreviewOnly",false))
            {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;var camera=(Camera)typeof(IslandLobbyView).GetField("wardrobeCamera",flags).GetValue(view);var render=typeof(IslandLobbyView).GetMethod("PresentWardrobe",flags);
                Color Sample(){var oldTarget=RenderTexture.active;RenderTexture.active=camera.targetTexture;var pixel=new Texture2D(1,1,TextureFormat.RGB24,false);pixel.ReadPixels(new Rect(camera.targetTexture.width/2,camera.targetTexture.height/2,1,1),0,0);pixel.Apply();var result=pixel.GetPixel(0,0);Object.Destroy(pixel);RenderTexture.active=oldTarget;return result;}
                render.Invoke(view,null);var beforePixel=Sample();var obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);obstruction.name="Preview occlusion regression fixture";obstruction.transform.position=camera.transform.position+camera.transform.forward*.35f;obstruction.transform.localScale=Vector3.one*.5f;
                render.Invoke(view,null);var afterPixel=Sample();Require(Vector4.Distance(beforePixel,afterPixel)<.03f,"A world object directly in front of the preview cannot obscure the avatar");Object.Destroy(obstruction);
                Require(!lobby.Player.gameObject.GetComponent<IModeActor>().Visual.GetComponentsInChildren<Renderer>().Any(r=>r.gameObject.layer==31),"Preview rendering restores the original avatar layers");
            }
            ScreenCapture.CaptureScreenshot(ProjectPath.Report("Expanded-Customizer.png"));yield return new WaitForSecondsRealtime(.4f);view.SetWardrobeOpen(false);
            if(SessionState.GetBool(Key+"PreviewOnly",false)){Require(errors==0,"Isolated wardrobe rendering finishes without runtime errors");File.AppendAllText(Report,"COMPLETE: actual offline lobby, avatar-only preview and restored renderer layers.\n");yield break;}
            string noun=room.LocalIdentity.noun;Require(details.Participants.Count==1&&lobby.Player.gameObject.GetComponent<IModeActor>().DisplayName==room.LocalIdentity.displayName,"Solo character receives its color-and-noun identity in the lobby");
            Require(view.InvokeChoice("map-current"),"Map dropdown opens");yield return null;
            Require(view.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("CHOOSE MAP")),"Dropdown displays the map list");view.InvokeChoice("picker-0");yield return null;
            Require(view.GetComponentsInChildren<RawImage>().Any(p=>p.texture),"Castle map uses an existing screenshot preview");
            Require(view.InvokeChoice("mode-current"),"Two-column mode picker opens");yield return null;int ctf=details.Modes.Where(m=>m.available&&m.id!="survival"&&m.id!="infection"&&m.id!="mystery"&&m.id!="demo").ToList().FindIndex(m=>m.id=="ctf");Require(ctf>=0&&view.InvokeChoice("mode-card-"+ctf),"Visible CTF card receives selection");yield return null;
            Require(room.Setup.rules.mode==GameModeKind.CaptureFlag,"Game mode picker applies its default rules (selected="+details.SelectedModeId+", slot="+ctf+")");
            Require(view.InvokeChoice("picker-0"),"Mode selection proceeds through its responsive map picker");yield return null;Require(view.InvokeChoice("mode-settings"),"Mode settings open after choosing the map");yield return null;int before=room.Setup.population;Require(view.InvokeChoice("setting-plus-0"),"Population increase receives its UI command");Require(room.Setup.population==before+1,"World-space lobby settings update the actual launch setup");view.InvokeChoice("setup-back");
            room.SelectMode(0);var setup=details.Setup;setup.population=10;setup.rules.punchDamage=23;setup.movement.gripSeconds=7;details.ConfigureSetup(setup);
            Require(room.Setup.rules.punchDamage==23&&MatchSetup.GripSeconds==7,"Mode and stamina tuning persist in launch settings");
            var simulation=new ModeSimulation(ModeSimulation.Defaults(GameModeKind.CaptureFlag));simulation.Register("one",ModeNames.Colors[3],false,true);simulation.PreserveIdentity("one","Trainer");simulation.Register("two",ModeNames.Colors[3],false,false);simulation.PreserveIdentity("two","Trainer");
            Require(simulation.Find("one").frame.name.EndsWith("Trainer")&&simulation.Find("two").frame.name.Contains("Trainer"),"Team colors change the prefix while preserving the identity noun");
            simulation.Configure(ModeSimulation.Defaults(GameModeKind.Survival));Require(simulation.Find("one").frame.name.EndsWith("Trainer"),"Changing modes preserves the noun");
            var allocator=new ModeNames(1);Require(allocator.Preserve(Color.red,"Trainer")!=allocator.Preserve(Color.red,"Trainer"),"Rare full-name collisions receive a numeric suffix");
            var app=PhotonAppSettings.Global.AppSettings;string protocol=(app.AppVersion??"")+"/"+lobby.catalog.NetworkVersion;
            using(var discovery=new IslandDiscovery(app.AppIdFusion,protocol,0)){discovery.InviteAll("US-ABC234",1);Require(discovery.LastInviteRecipients==0&&discovery.Status.Contains("0 nearby"),"Invite reports zero recipients when no compatible nearby players are known");}
            Capture(lobby,"LobbyFlow-Solo.png",new Vector3(0,2.4f,-6),new Vector3(0,1.6f,1.1f));
            Capture(lobby,"LobbyFlow-GameTerminal.png",new Vector3(3.1f,1.9f,-1.5f),new Vector3(3.05f,1.62f,1));
            var config=NetworkProjectConfig.Global;var old=config.PeerMode;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                yield return Done(room.Host(),"Real Photon room creation completes");yield return Until(()=>lobby.SurvivorBots?.Count==0,"Hosting removes all offline lobby bots");Require(room.IsHost&&room.HumanCount==1&&lobby.JoinEnabled,"Published solo room reports one connected player and keeps code entry active");
                var go=new GameObject("Independent lobby guest");probe=go.AddComponent<NetworkRunner>();var client=probe;var callbacks=new NetworkDelegates();IslandRoomConnection.LobbyState received=null;int packets=0;var actor=lobby.Capture("forged");
                callbacks.OnReliableDataReceived=(runner,source,key,bytes)=>
                {
                    key.GetInts(out int channel,out int kind,out int serial,out _);if(channel!=0x49534C44||kind!=3)return;
                    using var r=P.Reader(bytes.ToArray(),P.World);r.ReadInt32();r.ReadInt32();int count=P.Count(r,10);for(int i=0;i<count;i++)P.ReadActor(r);r.ReadBoolean();received=JsonUtility.FromJson<IslandRoomConnection.LobbyState>(P.Text(r,16000));packets++;
                    client.SendReliableDataToServer(ReliableKey.FromInts(channel,4,serial,0),Array.Empty<byte>());
                    client.SendReliableDataToServer(ReliableKey.FromInts(channel,2,serial,0),P.Encode(P.Player,w=>P.Write(w,actor)));
                };
                client.AddCallbacks(callbacks);var settings=app.GetCopy();settings.FixedRegion=room.Region;settings.AppVersion=(settings.AppVersion??"")+"/"+lobby.catalog.NetworkVersion;
                var task=client.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=room.Code,EnableClientSessionCreation=false,CustomPhotonAppSettings=settings,Config=IslandRoomConnection.MigrationConfig(),Scene=new NetworkSceneInfo(),SceneManager=go.AddComponent<NetworkSceneManagerDefault>()});yield return Done(task,"Independent Fusion client joins");Require(task.Result.Ok,"Actual second runner connects to the published code");
                client.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,1,0,0),P.Encode(P.Player,w=>w.Write(JsonUtility.ToJson(room.LocalIdentity))));
                yield return Until(()=>received?.roster?.Length==2&&lobby.PeerCount==1,"Roster and physical avatar replicate through the real lobby channel");
                Require(received.roster.Select(p=>p.noun).Distinct().Count()==2,"Joining with a duplicate preferred noun receives a unique lobby noun");
                Require(details.ConnectedCount==2&&received.roster.Count(p=>p.host)==1,"Connected count and single host designation replicate");
                void Command(string verb,int index=0,LobbySetup state=null)=>client.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,6,0,0),P.Encode(P.Player,w=>w.Write(JsonUtility.ToJson(new IslandRoomConnection.LobbyAction{verb=verb,index=index,setup=state}))));
                var unauthorized=room.Setup;unauthorized.rules.punchDamage=99;Command("setup",state:unauthorized);yield return new WaitForSecondsRealtime(.3f);Require(room.Setup.rules.punchDamage==23,"Guest cannot forge host-only setup changes");
                Command("request");yield return Until(()=>room.HostRequests.Count==1,"Guest host request reaches current host");Require(details.Notification.Contains("host request"),"Host receives an actionable HUD notification");
                string requester=room.HostRequests[0];view.InvokeChoice("request-accept");yield return Until(()=>received?.leader==requester,"Accepting request replicates lobby ownership");
                Require(!lobby.IsHost&&!lobby.CanConfigure&&lobby.JoinEnabled&&!view.InvokeChoice("launch"),"Former host's game terminal is locked while its join terminal remains usable");
                var accepted=room.Setup;accepted.rules.punchDamage=27;Command("setup",state:accepted);yield return Until(()=>room.Setup.rules.punchDamage==27,"New host configures the room through server-validated requests");
                lobby.EnteredCode="EU-ABC234";yield return new WaitForSecondsRealtime(.3f);Require(!JsonUtility.ToJson(received).Contains("EU-ABC234"),"Another player's typed join code is absent from replicated lobby state");
                yield return Done(client.Shutdown(),"Guest / delegated host leaves");Object.Destroy(go);probe=null;
                yield return Until(()=>lobby.IsHost&&lobby.CanConfigure&&details.Participants.Count==1,"Remaining player regains lobby ownership when delegated host leaves");
                Require(room.LocalIdentity.noun==noun,"Local noun survives online publishing and another player's departure");
                yield return Done(room.Leave(),"Network room closes cleanly");Require(lobby.IsHost&&lobby.CanStart&&!room.Connected&&lobby.JoinEnabled,"Leaving returns to a ready solo island without locking JOIN");
            }
            finally{config.PeerMode=old;}
            room.SelectMap(0);room.SelectMode(0);var launch=room.Setup;launch.population=1;launch.rules.punchDamage=31;launch.movement.gripSeconds=6;details.ConfigureSetup(launch);lobby.StartOrCancel();
            yield return Until(()=>Object.FindFirstObjectByType<CastlePlaySession>()?.Modes!=null,"Solo start enters the actual Game scene without a network room",60);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();Require(session.Network==null&&session.Modes.Rules.punchDamage==31,"Offline game uses the lobby's mode settings without starting Fusion");
            Require(session.Player.gameObject.GetComponent<IModeActor>().DisplayName.EndsWith(noun),"Lobby noun carries into the actual match avatar and scoreboard");Require(Mathf.Abs(((ISurvivorSettings)session.Player).CurrentRules.gripSeconds-6)<.01f,"Lobby stamina tuning applies to the real character");
            yield return Nameplates(session);
            Require(errors==0,"Lobby and Game finish integrated checks without errors");File.AppendAllText(Report,"COMPLETE: actual two-runner networking, lobby UI, private entry, solo launch, identity and visibility. Headset ergonomics and separate-device networking not verified.\n");
        }
        static IEnumerator Nameplates(CastlePlaySession session)
        {
            yield return new WaitForSecondsRealtime(.5f);var hud=session.GetComponent<CandyMatchHud>();hud.enabled=false;var eye=session.ActiveCamera;session.SetMenu(true);var oldPosition=eye.transform.position;var oldRotation=eye.transform.rotation;eye.transform.position=Services.World.Center+Vector3.right*(Services.World.Radius+25)+Vector3.up*25;eye.transform.rotation=Quaternion.identity;typeof(CandyMatchHud).GetField("camera",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(hud,eye);
            var sample=new MatchPlayerFrame{id="visibility-test",name="YellowTrainer",alive=true,color=Color.yellow,viewPosition=eye.transform.position+eye.transform.forward*5,position=eye.transform.position+eye.transform.forward*5-Vector3.up*1.6f};
            var frame=new MatchFrame{players=new[]{sample},rules=new ModeRules{mode=GameModeKind.Survival,punchEnabled=true}};
            var method=typeof(CandyMatchHud).GetMethod("Nameplates",BindingFlags.Instance|BindingFlags.NonPublic);
            var labels=(Dictionary<string,TextMesh>)typeof(CandyMatchHud).GetField("plates",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hud);
            method.Invoke(hud,new object[]{frame,"local"});Require(labels.TryGetValue(sample.id,out var tag)&&tag.gameObject.activeSelf&&tag.color==Color.white&&tag.transform.Find("Text shadow"),"Non-team tags use white text with a shadow");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=eye.transform.position+eye.transform.forward*2;wall.transform.localScale=Vector3.one*1.5f;wall.layer=Layers.Block;Physics.SyncTransforms();
            method.Invoke(hud,new object[]{frame,"local"});Require(!tag.gameObject.activeSelf,"Competitive name tags hide immediately behind geometry");
            Object.Destroy(wall);yield return null;frame.rules.punchEnabled=false;method.Invoke(hud,new object[]{frame,"local"});
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=eye.transform.position+eye.transform.forward*2;wall.transform.localScale=Vector3.one*1.5f;wall.layer=Layers.Block;Physics.SyncTransforms();method.Invoke(hud,new object[]{frame,"local"});Require(tag.gameObject.activeSelf,"Noncompetitive name tags retain their brief occlusion grace");
            yield return new WaitForSecondsRealtime(2.2f);method.Invoke(hud,new object[]{frame,"local"});Require(!tag.gameObject.activeSelf,"Noncompetitive occlusion grace expires after two seconds");
            Object.Destroy(wall);eye.transform.rotation=oldRotation*Quaternion.Euler(0,180,0);method.Invoke(hud,new object[]{frame,"local"});Require(!tag.gameObject.activeSelf,"Looking away hides names rather than placing them at the screen edge");eye.transform.SetPositionAndRotation(oldPosition,oldRotation);hud.enabled=true;
        }
        static void Capture(IslandLobby lobby,string name,Vector3 position,Vector3 target)
        {
            var eye=lobby.Player.ViewCamera;eye.transform.position=position;eye.transform.LookAt(target);foreach(var face in lobby.GetComponentsInChildren<LobbyTerminalFaces>())face.Refresh();
            var root=new GameObject("Lobby capture");var camera=root.AddComponent<Camera>();camera.fieldOfView=62;camera.transform.SetPositionAndRotation(eye.transform.position,eye.transform.rotation);var rt=new RenderTexture(1600,1100,24);var old=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,1100),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());}
            finally{RenderTexture.active=old;Object.Destroy(rt);if(pixels)Object.Destroy(pixels);Object.Destroy(root);}
        }
    }
    public sealed class LobbyFlowDriver:MonoBehaviour{}
}
