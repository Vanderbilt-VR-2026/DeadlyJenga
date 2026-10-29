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
using DeadlyJenga.Core.Testing;
using DeadlyJenga.World.Hazards;
using DeadlyJenga.CandyArt;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class FireDisasterChecks
    {
        const string Key="FireDisasterChecks";
        static string Report=>ProjectPath.Report("FireDisaster.txt");
        static int failures,errors;
        static FireDisasterChecks()
        {
            EditorApplication.update+=()=>
            {
                var request=ProjectPath.Report("FireDisaster.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                File.Delete(request);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    failures=errors=0;Application.logMessageReceived+=Log;
                    EditorApplication.delayCall+=()=>{var go=new GameObject("Health and lobby checks");Object.DontDestroyOnLoad(go);go.AddComponent<FireDisasterCheckDriver>().StartCoroutine(Guard());};
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Time.timeScale=1;Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                    var original=SessionState.GetString(Key+"Scene","");if(File.Exists(original))EditorSceneManager.OpenScene(original);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Engulfing Fire and Burnout")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before this check.");
            File.WriteAllText(Report,"Fire engulfing, spread, burnout and replicated scars\n");
            SessionState.SetString(Key+"Scene",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,UnityEngine.LogType type)
        {
            if(type!=UnityEngine.LogType.Error&&type!=UnityEngine.LogType.Exception&&type!=UnityEngine.LogType.Assert)return;
            if(message.StartsWith("[Package Manager Window]",StringComparison.Ordinal)){File.AppendAllText(Report,"EDITOR NOTE "+message+"\n");return;}
            if(NetworkProjectConfig.Global.PeerMode==NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.",StringComparison.Ordinal)){File.AppendAllText(Report,"EDITOR NOTE Extra transport peer has no game scene.\n");return;}
            errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");
        }
        static void Check(bool value,string label){File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static FieldInfo FindField(object target,string name){for(var t=target.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(f!=null)return f;}throw new MissingFieldException(name);}
        static object Field(object target,string name)=>FindField(target,name).GetValue(target);
        static void Set(object target,string name,object value)=>FindField(target,name).SetValue(target,value);
        static object Call(object target,string name,params object[] args){for(var t=target.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(target,args);}throw new MissingMethodException(name);}
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
        [Serializable] sealed class Packet{public DisasterFrame[] frames;}
        static void Advance(FireHazard fire,float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/Time.fixedDeltaTime);i++)Call(fire,"FixedUpdate");}
        static IEnumerator Until(Func<bool> predicate,string label,float timeout=50){float end=Time.realtimeSinceStartup+timeout;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),label);}
        static void Preview(CandyDisasterVisuals visual,FakeWorldQuery world,CastlePlaySession session,string name)
        {
            var bounds=new Bounds(world.blocks[0].position,Vector3.zero);foreach(var b in world.blocks)bounds.Encapsulate(b.GetComponent<Collider>().bounds);
            var go=new GameObject("Temporary fire preview");var view=go.AddComponent<Camera>();view.CopyFrom(session.ActiveCamera);view.enabled=false;view.nearClipPlane=.1f;view.farClipPlane=150;view.fieldOfView=48;
            float radius=Mathf.Max(9,bounds.size.x);view.transform.position=bounds.center+new Vector3(radius*.7f,radius*.45f,-radius*1.1f);view.transform.LookAt(bounds.center+Vector3.up*.7f);
            var eye=session.ActiveCamera.transform;var old=new Pose(eye.position,eye.rotation);eye.SetPositionAndRotation(view.transform.position,view.transform.rotation);Call(visual,"LateUpdate");eye.SetPositionAndRotation(old.position,old.rotation);
            var target=RenderTexture.GetTemporary(1280,720,24);var previous=RenderTexture.active;view.targetTexture=target;view.Render();RenderTexture.active=target;
            var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name+".png"),pixels.EncodeToPNG());
            RenderTexture.active=previous;view.targetTexture=null;RenderTexture.ReleaseTemporary(target);Object.Destroy(pixels);Object.Destroy(go);
        }
        static IEnumerator CheckScene()
        {
            yield return new WaitForSeconds(1);
            var validation=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="DeadlyJenga.World.Editor").GetType("DeadlyJenga.World.Hazards.Editor.HazardValidation");validation.GetMethod("ValidateCatalog").Invoke(null,null);
            Check(true,"All disaster prefabs pass the existing map-aware catalog validation");
            var session=Object.FindFirstObjectByType<CastlePlaySession>();Check(session&&session.Modes,"Actual Game starts without errors");session.Modes.enabled=false;session.enabled=false;session.SetMenu(true);Time.timeScale=0;
            var originalWorld=Services.World;var template=originalWorld.Blocks.First(b=>b&&b.GetComponent<IClimbable>()?.Visual);
            var fixture=new GameObject("Temporary fire map");fixture.transform.position=new Vector3(1000,1000,1000);var world=fixture.AddComponent<FakeWorldQuery>();world.playableBounds=new Bounds(new Vector3(30,10,0),new Vector3(160,40,60));
            Rigidbody Clone(int i)
            {var go=Object.Instantiate(template.gameObject);go.name="Fire test block "+i;go.transform.SetPositionAndRotation(fixture.transform.position+new Vector3(i*25,2,0),Quaternion.identity);var b=go.GetComponent<Rigidbody>();b.isKinematic=true;b.useGravity=false;return b;}
            for(int i=0;i<3;i++)world.blocks.Add(Clone(i));yield return null;Physics.SyncTransforms();
            var prefab=DemoDisasters.Prefabs.First(p=>p&&p.GetComponent<FireHazard>());
            var fire=Object.Instantiate(prefab).GetComponent<FireHazard>();
            var definitions=fire.Parameters;var settings=definitions.Select(p=>p.initial).ToArray();
            void Value(string id,float value){int i=Enumerable.Range(0,definitions.Count).First(n=>definitions[n].id==id);settings[i]=value;}
            Value("seeds",1);Value("warning",0);Value("contact",.2f);Value("duration",3);Value("neverEnd",1);
            Check(definitions.Count<=8&&settings[Enumerable.Range(0,definitions.Count).First(n=>definitions[n].id=="burnSeconds")]==30,"Fire defaults to thirty seconds per block and fits both demo menus");
            fire.Configure(settings);Check(fire.TryValidate(world,out _),"Translated test map validates");Check(!fire.TryValidate(null,out _),"Missing map is rejected");
            fire.Begin();Advance(fire,.04f);int seed=fire.Capture().blocks[0],next=(seed+1)%3,last=(seed+2)%3;
            void Beside(int target,int source){var a=world.blocks[source].GetComponent<Collider>().bounds;var b=world.blocks[target].GetComponent<Collider>().bounds;world.blocks[target].transform.position=world.blocks[source].position+Vector3.right*(a.extents.x+b.extents.x+.05f);Physics.SyncTransforms();}
            Beside(next,seed);Advance(fire,1);
            Check(fire.Capture().blocks.Contains(next),"Touching blocks spread the fire through the configured contact-time logic");
            Set(fire,"age",4f);Beside(last,next);Advance(fire,1);
            Check(fire.Active&&fire.Capture().remaining<0&&fire.Capture().blocks.Contains(last),"Never end keeps spreading after the normal spread-duration cutoff");
            Check(fire.Capture().burns.Select(x=>x.flameRemaining).Distinct().Count()>1,"Each newly ignited block gets its own burn timer");
            var gameType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");if(gameType!=null)EditorWindow.GetWindow(gameType).Focus();yield return null;
            var visual=Object.FindFirstObjectByType<CandyDisasterVisuals>();Preview(visual,world,session,"Fire-Engulfed");
            Check(((List<Matrix4x4>)Field(visual,"fireShells")).Count==3,"Every burning block gets a full-body flame shell");
            foreach(string shaderName in new[]{"CandyBlockFire","CandyCharredBlock"}){var shader=Resources.Load<Shader>("CandyScenery/"+shaderName);Check(shader&&!ShaderUtil.ShaderHasError(shader),shaderName+" imports and compiles");}
            Set(fire,"age",30.2f);Advance(fire,.12f);var scars=world.GetComponent<FireScars>();
            Check(scars&&scars.Burnt(seed)&&!fire.Capture().blocks.Contains(seed)&&fire.Capture().blocks.Contains(last),"Older blocks extinguish while later ignitions continue burning");
            Set(fire,"age",36f);Advance(fire,.12f);
            Check(!fire.Active&&scars.Capture().burns.Length==3&&scars.Capture().residualOnly,"Exhausted fuel ends the active hazard but leaves all burned surfaces");
            Check(scars.Capture().burns.All(b=>b.flameRemaining==0&&b.smokeRemaining>0&&b.smokeRemaining<=20),"Extinguished blocks have light smoke for the configured aftermath period");
            yield return new WaitForSecondsRealtime(.12f);Preview(visual,world,session,"Fire-Charred");
            Check(world.blocks.All(b=>b.GetComponent<IClimbable>().Visual.GetComponentsInChildren<Renderer>().All(r=>!r.sharedMaterial||r.sharedMaterial.shader.name=="Candy Tropical/Charred Block")),"The entire visual mesh receives the dark charred surface");
            Object.Destroy(fire.gameObject);
            var wire=CastleFusionWire.Compress(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Packet{frames=new[]{scars.Capture()}})));
            var receiverObject=new GameObject("Fire snapshot receiver");var receiver=receiverObject.AddComponent<CastleFusionSession>();receiver.enabled=false;
            Call(receiver,"ReceiveDisasters",wire);Check(DisasterStates.Active.Any(s=>!ReferenceEquals(s,scars)&&s.Capture()?.residualOnly==true&&s.Capture().burns.Length==3),"Production receiver reconstructs charred surfaces for late joiners");Call(receiver,"ResetDisasters");
            bool rejected=false;try{var bad=new DisasterFrame{kind="Fire",burns=new[]{new BlockBurnFrame{block=0,flameRemaining=999}}};Call(receiver,"ReceiveDisasters",CastleFusionWire.Compress(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Packet{frames=new[]{bad}}))));}catch(TargetInvocationException e){rejected=e.InnerException is InvalidDataException;}Check(rejected,"Out-of-range remote burn times are rejected");
            var replacement=Clone(0);Object.Destroy(world.blocks[0].gameObject);world.blocks[0]=replacement;Physics.SyncTransforms();Check(scars.Capture()==null,"Map rebuild clears burned surface state");
            var finite=Object.Instantiate(prefab).GetComponent<FireHazard>();Value("neverEnd",0);finite.Configure(settings);finite.Begin();Advance(finite,.04f);int sourceId=finite.Capture().blocks[0],targetId=(sourceId+1)%3;
            world.blocks[targetId].transform.position=fixture.transform.position+new Vector3(100,2,0);Physics.SyncTransforms();Set(finite,"age",4f);Beside(targetId,sourceId);Advance(finite,1);
            Check(finite.Active&&!finite.Capture().blocks.Contains(targetId),"Finite mode stops new spread after its deadline but lets existing blocks finish burning");finite.End();Object.Destroy(finite.gameObject);
            foreach(var b in world.blocks)Object.Destroy(b.gameObject);Object.Destroy(fixture);Services.World=originalWorld;Call(visual,"LateUpdate");
            var max=new Packet{frames=new[]{new DisasterFrame{kind="Fire",residualOnly=true,burns=Enumerable.Range(0,20000).Select(i=>new BlockBurnFrame{block=i}).ToArray()}}};
            var raw=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(max));Check(raw.Length<CastleFusionWire.MaxBytes,"Twenty thousand burned blocks fit the existing bounded snapshot format");File.AppendAllText(Report,"MEASURE 20,000-block aftermath: "+raw.Length+" JSON bytes, "+CastleFusionWire.Compress(raw).Length+" compressed bytes.\n");
            Time.timeScale=1;var config=NetworkProjectConfig.Global;var previousMode=config.PeerMode;NetworkRunner host=null,client=null;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                var hg=new GameObject("Fire transport host");host=hg.AddComponent<NetworkRunner>();var cg=new GameObject("Fire transport guest");client=cg.AddComponent<NetworkRunner>();
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion="us";app.AppVersion=(app.AppVersion??"")+"/fire-state-test";string room="fire-"+Guid.NewGuid().ToString("N").Substring(0,10);
                var start=host.StartGame(new StartGameArgs{GameMode=GameMode.Host,SessionName=room,PlayerCount=2,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=hg.AddComponent<NetworkSceneManagerDefault>()});yield return Until(()=>start.IsCompleted,"Real Fusion host starts");Check(start.Result.Ok,"Fire transport host connects");
                bool received=false;var callbacks=new NetworkDelegates();callbacks.OnReliableDataReceived=(r,p,key,data)=>{key.GetInts(out int channel,out int kind,out _,out _);if(channel==0x43415354&&kind==11){Call(receiver,"ReceiveDisasters",data.ToArray());received=true;}};client.AddCallbacks(callbacks);
                var join=client.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=room,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=cg.AddComponent<NetworkSceneManagerDefault>()});yield return Until(()=>join.IsCompleted,"Real second Fusion peer joins");Check(join.Result.Ok,"Second peer joins the fire transport room");
                host.SendReliableDataToPlayer(client.LocalPlayer,ReliableKey.FromInts(0x43415354,11,1,0),wire);yield return Until(()=>received,"Fire aftermath crosses the actual Fusion disaster channel and production decoder");
                Call(receiver,"ResetDisasters");var stop=client.Shutdown();yield return Until(()=>stop.IsCompleted,"Guest shuts down");Object.Destroy(cg);client=null;stop=host.Shutdown();yield return Until(()=>stop.IsCompleted,"Host shuts down");Object.Destroy(hg);host=null;
            }
            finally{if(client){_=client.Shutdown();Object.Destroy(client.gameObject);}if(host){_=host.Shutdown();Object.Destroy(host.gameObject);}config.PeerMode=previousMode;}
            Object.Destroy(receiverObject);Time.timeScale=1;session.enabled=true;session.Modes.enabled=true;Check(errors==0,"No gameplay runtime errors during fire regression checks");
        }
    }
    public sealed class FireDisasterCheckDriver:MonoBehaviour {}
}

