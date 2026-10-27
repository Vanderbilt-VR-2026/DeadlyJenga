using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using DeadlyJenga.Wildlife;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class LonghandBruteChecks
    {
        const string Key="LonghandBruteChecks";
        static IEnumerator run;static int errors,checks;static bool failed;
        static string Report=>ProjectPath.Report("LonghandBruteChecks.txt");
        [Serializable] sealed class Saved{public SceneSetup[] scenes;public string choices;public bool background,fromLobby,local;public float volume;}
        static LonghandBruteChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        public static void Run()
        {
            BotObjectiveContractChecks.Run();
            if(!BotObjectiveContractChecks.LastPassed)throw new Exception("Objective contracts failed");
            string root=Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            if(Path.GetFullPath(File.ReadAllText(ProjectPath.Report("CandyTerminalDesign.owner")).Trim()).ToLowerInvariant()!=root.ToLowerInvariant())throw new InvalidOperationException("Wrong owned editor.");
            if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Editor busy.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Unsaved scenes untouched.");
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=EditorSceneManager.GetSceneManagerSetup(),choices=JsonUtility.ToJson(SpectatorRecordingPlan.Current()),background=Application.runInBackground,volume=AudioListener.volume,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession}));
            SessionState.SetBool(Key,true);File.WriteAllText(Report,"Actual Unity Longhand and Gum Brute validation\n"+root+"\n");
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="mode-christmas-town";MatchSetup.ModeId="demo";MatchSetup.BotTarget=2;MatchSetup.HandCount=0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){SessionState.SetBool(Key+"Entered",true);errors=checks=0;failed=false;Application.logMessageReceived+=Log;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();run=Flatten(Validate());}
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Entered",false))
            {
                SessionState.SetBool(Key+"Entered",false);Application.logMessageReceived-=Log;run=null;SessionState.SetBool(Key,false);
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));Application.runInBackground=saved.background;AudioListener.volume=saved.volume;MatchSetup.FromLobby=saved.fromLobby;MatchSetup.StartLocalSession=saved.local;
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(saved.choices));
                if(saved.scenes.Any(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                File.AppendAllText(Report,"COMPLETE checks="+checks+" errors="+errors+" failed="+failed+"\nNo Quest claim. Actual Game enemy spawns and health/cooldown checks; real two-peer Photon payload and remote model checks. Transport-only scene adapter; full two-machine gameplay and Quest untested.\n");
            }
        }
        static void Log(string m,string trace,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            // Known independent editor service failures are recorded separately.
            if(trace.Contains("UnityEditor.Search.SearchDatabase")||trace.Contains("UnityEditor.AsyncHTTPClient")){File.AppendAllText(Report,"EDITOR SERVICE "+m+"\n");return;}
            errors++;File.AppendAllText(Report,"ERROR "+m+"\n"+trace+"\n");
        }
        static void Tick()
        {
            if(run==null)
            {
                string request=ProjectPath.Report("LonghandBruteChecks.request");
                if(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(request)&&File.ReadAllText(request).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString()){File.Delete(request);Run();}
                return;
            }
            try{if(run.MoveNext())return;}catch(Exception e){failed=true;File.AppendAllText(Report,"FAIL "+e+"\n");}
            (run as IDisposable)?.Dispose();run=null;EditorApplication.ExitPlaymode();
        }
        static void Check(bool ok,string text){checks++;File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+text+"\n");if(!ok)throw new Exception(text);}
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static IEnumerator Until(Func<bool> ready,string label,float seconds=40){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Check(ready(),label);}
        static IEnumerator Flatten(IEnumerator source)
        {
            while(source.MoveNext())
            {
                if(source.Current is IEnumerator nested){var child=Flatten(nested);while(child.MoveNext())yield return child.Current;}
                else if(source.Current is AsyncOperation operation){while(!operation.isDone)yield return null;}
                else if(source.Current is WaitForSeconds){var wait=Wait(.6f);while(wait.MoveNext())yield return null;}
                else yield return source.Current;
            }
        }
        static IEnumerator Validate()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            yield return Until(()=>Object.FindFirstObjectByType<CastlePlaySession>()?.Modes!=null,"Game solo starts");
            yield return Wait(2);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            var population=session.SurvivorBots as IEnemyPopulation;
            Check(population!=null,"Actual enemy population available");
            var combatRules=session.Modes.Rules.Copy();combatRules.healthEnabled=true;combatRules.island=IslandRule.Walkable;session.Modes.Configure(combatRules);session.Modes.StartRound();yield return Wait(.3f);
            Check(population.SpawnEnemy(new EnemySpawnSettings{archetypeId="unknown"})==null,"Unknown archetype rejected");
            foreach(var id in new[]{"longhand","gum-brute"})
            {
                var enemy=population.SpawnEnemy(new EnemySpawnSettings{archetypeId=id,hitDamage=id=="longhand"?25:40});
                Check(enemy,"Perimeter spawns "+id);yield return Wait(.8f);
                var profile=enemy.GetComponent<DeadlyJenga.CastleBots.CastleEnemyActor>();
                Check(profile.ArchetypeId==id,"Gameplay identity "+id);
                var visual=enemy.GetComponent<DeadlyJenga.CastleBots.CandyMayhemEnemyVisual>();
                Check(visual&&visual.Model,"Procedural model attaches "+id);
                Check(visual.Model.GetComponentsInChildren<Collider>().Length==0,"Model adds no colliders "+id);
                var rules=MatchRuntime.Current.RulesFor(enemy);
                Check(rules.punchCooldown>=1.6f&&rules.moveScale<1,"Distinct movement and recovery "+id);
                Check(rules.punchDamage==profile.HitDamage,"Authoritative enemy damage "+id);
                if(id=="gum-brute")Check(rules.punchDistance==12,"Brute heavy shove");
                else Check(profile.StrikeReach==3.25f,"Longhand reach");
                Capture(visual.Model,id);
                var frame=enemy.GetComponent<DeadlyJenga.CandyPlayers.CandyPlayerAvatar>().LastFrame;
                using(var stream=new MemoryStream())
                {
                    using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))DeadlyJenga.CharacterNetwork.CharacterWire.WriteSurvivor(writer,frame,0,default,profile);
                    stream.Position=0;
                    using(var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true)){
                        DeadlyJenga.CharacterNetwork.CharacterWire.ReadSurvivor(reader,out var decoded,out var color,out var appearance,out var decodedEnemy);
                        Check(decodedEnemy.archetypeId==id,"Codec round trip "+id);
                        var packet=stream.ToArray();packet[packet.Length-5]=12;bool rejected=false;
                        try{using(var bad=new BinaryReader(new MemoryStream(packet)))DeadlyJenga.CharacterNetwork.CharacterWire.ReadSurvivor(bad,out _,out _,out _,out _);}catch(InvalidDataException){rejected=true;}
                        Check(rejected,"Conflicting enemy flags rejected "+id);

                    }
                }
            }
            using(var stream=new MemoryStream()){
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))DeadlyJenga.CharacterNetwork.CharacterWire.WriteSurvivor(writer,session.Survivor().GetComponent<DeadlyJenga.CandyPlayers.CandyPlayerAvatar>().LastFrame,0,default,null);
                stream.Position=0;using(var reader=new BinaryReader(stream)){
                    DeadlyJenga.CharacterNetwork.CharacterWire.ReadSurvivor(reader,out _,out _,out _,out var human);
                    Check(human==null,"Ordinary human payload remains compatible without enemy metadata");
                }
            }
            Check(population.EnemyCount==2,"Both enemy species coexist");
            yield return Network(session);
            foreach(var enemy in Object.FindObjectsByType<DeadlyJenga.CastleBots.CastleEnemyActor>(FindObjectsSortMode.None))yield return Strike(session,enemy);
            yield return Wait(8);
            foreach(var visual in Object.FindObjectsByType<DeadlyJenga.CastleBots.CandyMayhemEnemyVisual>(FindObjectsSortMode.None))Check(!float.IsNaN(visual.transform.position.sqrMagnitude),"Finite navigation state");
            Check(population.EnemyCount==2,"Both enemies remain alive before clearing");
            population.ClearEnemies();yield return null;
            Check(population.EnemyCount==0,"Enemy clear leaves population clean");
            foreach(var id in new[]{"longhand","gum-brute"}){
                var enemy=population.SpawnEnemy(new EnemySpawnSettings{archetypeId=id});Check(enemy,"Death check spawns "+id);yield return Wait(.4f);
                enemy.GetComponent<IDamageable>().Eliminate(session.Survivor().gameObject);yield return null;
                var splats=Object.FindObjectsByType<DeadlyJenga.Presentation.CandyMayhemEnemyFinish>(FindObjectsSortMode.None).Where(e=>e.gameObject.name=="Visual - melted mayhem enemy").ToArray();
                Check(splats.Any(e=>e.GetComponentsInChildren<MeshRenderer>().Length>0),"Actual elimination creates nonempty candy splat "+id);
            }
            population.ClearEnemies();

        }
        static IEnumerator Strike(CastlePlaySession session,DeadlyJenga.CastleBots.CastleEnemyActor enemy)
        {
            var player=session.Survivor();var old=player.transform.position;var rotation=player.transform.rotation;bool paused=player.InputSuspended;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=Layers.Surface;floor.transform.position=new Vector3(enemy.ArchetypeId=="longhand"?1600:1500,10,1500);floor.transform.localScale=new Vector3(20,1,20);
            var brain=enemy.GetComponent<DeadlyJenga.CastleBots.CastleBotBrain>();var motor=brain.Motor;var enemyPoint=motor.transform.position;var enemyRotation=motor.transform.rotation;
            try{
                brain.enabled=false;motor.InputSuspended=true;player.InputSuspended=true;
                
                motor.Place(floor.transform.position+Vector3.up*.55f,Quaternion.LookRotation(Vector3.right));
                player.Place(floor.transform.position+new Vector3(enemy.ArchetypeId=="longhand"?2.6f:1.5f,.55f,0),Quaternion.identity);
                yield return Wait(.4f);
                var origin=motor.ViewPose.position;var direction=(player.transform.position+Vector3.up*.6f-origin).normalized;float health=player.Health;
                Check(Vector3.Distance(origin,player.transform.position+Vector3.up*.6f)<(enemy.ArchetypeId=="longhand"?3.4f:2.2f),"Fixture inside physical strike range "+enemy.ArchetypeId);
                Check(session.Modes.RequestPunch(enemy.gameObject,origin,direction,2),"Authoritative strike accepted "+enemy.ArchetypeId);
                Check(player.Health<health,"Actual victim health loss "+enemy.ArchetypeId);
                float after=player.Health;
                Check(!session.Modes.RequestPunch(enemy.gameObject,origin,direction,2)&&player.Health==after,"Authoritative recovery blocks repeated strike "+enemy.ArchetypeId);
            }finally{if(motor){motor.Place(enemyPoint,enemyRotation);motor.InputSuspended=false;brain.enabled=true;}player.Place(old,rotation);player.InputSuspended=paused;Object.Destroy(floor);}
        }
        static IEnumerator Network(CastlePlaySession session)
        {
            var config=Fusion.NetworkProjectConfig.Global;var old=config.PeerMode;var physics=Physics.simulationMode;
            Fusion.NetworkRunner host=null,guest=null;var views=new System.Collections.Generic.List<GameObject>();
            int received=0;string failure=null;
            try{
                config.PeerMode=Fusion.NetworkProjectConfig.PeerModes.Multiple;
                host=new GameObject("Mayhem enemy Photon host").AddComponent<Fusion.NetworkRunner>();
                guest=new GameObject("Mayhem enemy Photon guest").AddComponent<Fusion.NetworkRunner>();
                string room="mayhem-"+Guid.NewGuid().ToString("N").Substring(0,12);
                var app=Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion="us";app.AppVersion=(app.AppVersion??"")+"/mayhem-validation";
                var start=host.StartGame(new Fusion.StartGameArgs{GameMode=Fusion.GameMode.Host,SessionName=room,PlayerCount=2,CustomPhotonAppSettings=app,Scene=new Fusion.NetworkSceneInfo(),SceneManager=host.gameObject.AddComponent<MayhemTransportScenes>()});
                yield return Until(()=>start.IsCompleted,"Photon host startup",45);Check(!start.IsFaulted&&start.Result.Ok,"Photon host connected");
                var callbacks=new Fusion.NetworkDelegates();
                callbacks.OnReliableDataReceived=(runner,source,key,data)=>{
                    try{
                        var root=new GameObject("Received enemy view");views.Add(root);
                        var view=NetworkActors.CreateView(NetworkKinds.Survivor,root,new NetworkContext{host=false,localSurvivor=session.Survivor().gameObject,actorId="enemy-fixture-"+received});
                        using(var stream=new MemoryStream(data.ToArray()))using(var reader=new BinaryReader(stream))view.ReadState(reader,true);
                        view.Render(.016f);received++;
                    }catch(Exception e){failure=e.ToString();}
                };guest.AddCallbacks(callbacks);
                var join=guest.StartGame(new Fusion.StartGameArgs{GameMode=Fusion.GameMode.Client,SessionName=room,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Scene=new Fusion.NetworkSceneInfo(),SceneManager=guest.gameObject.AddComponent<MayhemTransportScenes>()});
                yield return Until(()=>join.IsCompleted,"Photon guest startup",45);Check(!join.IsFaulted&&join.Result.Ok,"Second Photon peer connected");Physics.simulationMode=physics;
                foreach(var enemy in Object.FindObjectsByType<DeadlyJenga.CastleBots.CastleEnemyActor>(FindObjectsSortMode.None)){
                    byte[] packet;using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))enemy.GetComponent<INetworkActor>().WriteState(writer);packet=stream.ToArray();}
                    foreach(var player in host.ActivePlayers)if(player!=host.LocalPlayer)host.SendReliableDataToPlayer(player,Fusion.Sockets.ReliableKey.FromInts(0x43415354,42,received,0),packet);
                    int expected=received+1;yield return Until(()=>received>=expected||failure!=null,"Reliable enemy payload received",15);Check(failure==null,"Remote decoder accepts actor payload: "+failure);
                }
                yield return null;yield return null;
                Check(received==2,"Both enemy archetypes transported between real Photon peers");
                foreach(var root in views){var visual=root.GetComponent<DeadlyJenga.CastleBots.CandyMayhemEnemyVisual>();Check(visual&&visual.Model,"Remote procedural model created");}
            }finally{
                if(guest){guest.Shutdown();Object.Destroy(guest.gameObject);}if(host){host.Shutdown();Object.Destroy(host.gameObject);}
                foreach(var root in views)if(root)Object.Destroy(root);config.PeerMode=old;Physics.simulationMode=physics;
            }
        }
        static void Capture(Transform model,string id)
        {
            var go=new GameObject("Animal evidence camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.72f,.82f,.88f);
            camera.orthographic=true;camera.orthographicSize=1.4f;
            camera.transform.position=model.position+new Vector3(2.8f,2.1f,4);camera.transform.LookAt(model.position+Vector3.up*1.05f);
            camera.nearClipPlane=.05f;camera.farClipPlane=8;camera.cullingMask=1<<31;
            var parts=model.GetComponentsInChildren<Transform>();var layers=parts.Select(p=>p.gameObject.layer).ToArray();foreach(var part in parts)part.gameObject.layer=31;
            var all=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);var hidden=all.Select(r=>r.forceRenderingOff).ToArray();
            for(int i=0;i<all.Length;i++)if(!all[i].transform.IsChildOf(model))all[i].forceRenderingOff=true;
            var lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None);var lit=lights.Select(l=>l.enabled).ToArray();foreach(var light in lights)light.enabled=false;
            var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.50f,.54f,.62f);
            var key=new GameObject("Animal studio key");var sun=key.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.color=new Color(1,.95f,.88f);sun.shadows=LightShadows.None;key.transform.rotation=Quaternion.Euler(40,-150,0);
            var target=new RenderTexture(768,768,24);var previous=RenderTexture.active;Texture2D image=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image=new Texture2D(768,768,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report("Enemy-"+id+".png"),image.EncodeToPNG());}
            finally{RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode;for(int i=0;i<lights.Length;i++)if(lights[i])lights[i].enabled=lit[i];Object.Destroy(key);for(int i=0;i<parts.Length;i++)if(parts[i])parts[i].gameObject.layer=layers[i];for(int i=0;i<all.Length;i++)if(all[i])all[i].forceRenderingOff=hidden[i];RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.Destroy(target);if(image)Object.Destroy(image);Object.Destroy(go);}
        }
    }
    // This fixture transports actual character payloads without loading extra Game sessions.
    public sealed class MayhemTransportScenes:MonoBehaviour,Fusion.INetworkSceneManager
    {
        public Scene MainRunnerScene{get;private set;}
        public bool IsBusy=>false;
        public void Initialize(Fusion.NetworkRunner runner){MainRunnerScene=SceneManager.CreateScene("Enemy transport "+GetInstanceID(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));}
        public void Shutdown(){if(MainRunnerScene.IsValid()&&MainRunnerScene.isLoaded)SceneManager.UnloadSceneAsync(MainRunnerScene);}
        public bool IsRunnerScene(Scene scene)=>scene==MainRunnerScene;
        public bool TryGetPhysicsScene3D(out PhysicsScene scene){scene=MainRunnerScene.GetPhysicsScene();return true;}
        public bool TryGetPhysicsScene2D(out PhysicsScene2D scene){scene=default;return false;}
        public void MakeDontDestroyOnLoad(GameObject go)=>SceneManager.MoveGameObjectToScene(go,MainRunnerScene);
        public bool MoveGameObjectToScene(GameObject go,Fusion.SceneRef scene){SceneManager.MoveGameObjectToScene(go,MainRunnerScene);return true;}
        public Fusion.SceneRef GetSceneRef(GameObject go)=>default;
        public Fusion.SceneRef GetSceneRef(string name)=>default;
        public Fusion.NetworkSceneAsyncOp LoadScene(Fusion.SceneRef scene,Fusion.NetworkLoadSceneParameters parameters)=>Fusion.NetworkSceneAsyncOp.FromTask(scene,System.Threading.Tasks.Task.CompletedTask);
        public Fusion.NetworkSceneAsyncOp UnloadScene(Fusion.SceneRef scene)=>Fusion.NetworkSceneAsyncOp.FromTask(scene,System.Threading.Tasks.Task.CompletedTask);
        public bool OnSceneInfoChanged(Fusion.NetworkSceneInfo info,Fusion.NetworkSceneInfoChangeSource source)=>true;
    }

}
