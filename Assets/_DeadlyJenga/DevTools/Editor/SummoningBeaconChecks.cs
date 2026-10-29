using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class SummoningBeaconChecks
    {
        const string Key="SummoningBeacon.Checks";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static IEnumerator run; static int errors;
        static string Report=>ProjectPath.Report("SummoningBeaconChecks.txt");
        [Serializable] sealed class Saved {public SceneSetup[] scenes;public string map,mode;public int bots,hands;public bool fromLobby,local,background,defaultScene;public float volume;}
        static SummoningBeaconChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        static void Tick()
        {
            if(run==null)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isUpdating||EditorApplication.isCompiling)return;
                string close=ProjectPath.Report("SummoningBeaconChecks.close");
                if(Own(close)){File.Delete(close);EditorApplication.Exit(0);return;}
                string request=ProjectPath.Report("SummoningBeaconChecks.request");
                if(!Own(request))return;
                var scenes=EditorSceneManager.GetSceneManagerSetup();
                if(Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isLoaded&&s.isDirty)){File.WriteAllText(Report,"Deferred: unsaved scene left untouched.");return;}
                if(!ValidationRunGate.TryAcquire(Key))return;
                File.Delete(request);File.WriteAllText(Report,"Summoning beacon checks started "+DateTime.Now.ToString("O")+"\n");
                SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=scenes,defaultScene=SceneManager.GetActiveScene().rootCount>0,map=MatchSetup.MapId,mode=MatchSetup.ModeId,bots=MatchSetup.BotTarget,hands=MatchSetup.HandCount,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession,background=Application.runInBackground,volume=AudioListener.volume}));
                MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;
                SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();return;
            }
            try{if(run.MoveNext())return;}catch(Exception e){errors++;File.AppendAllText(Report,"FAIL "+e+"\n");}
            run=null;Application.logMessageReceived-=Log;
            File.AppendAllText(Report,"COMPLETE; errors="+errors+". Actual desktop Editor; remote actor selection uses an explicit fixture. No Quest or two-client verification.\n");
            EditorApplication.ExitPlaymode();
        }
        static bool Own(string file)=>File.Exists(file)&&File.ReadAllText(file).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)&&ValidationRunGate.Owns(Key))
            {
                SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Running",true);errors=0;
                EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();Application.logMessageReceived+=Log;run=Validate();
            }
            if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+"Running",false))return;
            SessionState.SetBool(Key+"Running",false);Application.logMessageReceived-=Log;run=null;
            AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
            var s=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));
            MatchSetup.MapId=s.map;MatchSetup.ModeId=s.mode;MatchSetup.BotTarget=s.bots;MatchSetup.HandCount=s.hands;MatchSetup.FromLobby=s.fromLobby;MatchSetup.StartLocalSession=s.local;Application.runInBackground=s.background;AudioListener.volume=s.volume;
            try{if(s.scenes!=null&&s.scenes.Any(x=>x.isLoaded&&!string.IsNullOrEmpty(x.path)))EditorSceneManager.RestoreSceneManagerSetup(s.scenes);else EditorSceneManager.NewScene(s.defaultScene?NewSceneSetup.DefaultGameObjects:NewSceneSetup.EmptyScene,NewSceneMode.Single);}
            finally{ValidationRunGate.Release(Key);}
        }
        static void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+stack+"\n");}}
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
        static void Phase(CandySessionTravel beacon,float phase)
        {
            var type=typeof(CandySessionTravel);float duration=(float)type.GetField("duration",Flags).GetValue(beacon);
            type.GetField("started",Flags).SetValue(beacon,Time.unscaledTime-duration*phase);
            type.GetMethod("Render",Flags).Invoke(beacon,null);
        }
        static SummoningRemoteFixture Fixture(string name,Vector3 position,string kind,bool active)
        {var g=new GameObject(name);g.transform.position=position;var f=g.AddComponent<SummoningRemoteFixture>();f.kind=kind;f.active=active;return f;}
        static CandySessionTravel CurrentTravel()
        =>(CandySessionTravel)typeof(CandySessionTravel).GetField("current",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        static IEnumerator ReentryChecks()
        {
            GameObject fixture=null;
            try
            {
                for(int state=0;state<2;state++)
                {
                    GameEvents.RaiseDepartureStarted(10);
                    var obsolete=CurrentTravel();Require(obsolete,"Reentry fixture obtains the live travel controller");
                    if(state==0)typeof(CandySessionTravel).GetMethod("StartArrival",Flags).Invoke(obsolete,null);
                    else typeof(CandySessionTravel).GetMethod("Finish",Flags).Invoke(obsolete,new object[]{false});
                    fixture=Fixture("New traveller after "+(state==0?"arrival":"finish"),Services.Session.Player.transform.position+Vector3.right*7,NetworkKinds.Survivor,true).gameObject;
                    GameEvents.RaiseDepartureStarted(10);
                    var replacement=CurrentTravel();
                    Require(replacement&&replacement!=obsolete&&!replacement.Arriving&&!obsolete.gameObject.activeSelf,
                        "New departure replaces "+(state==0?"arrival":"finishing")+" visuals immediately");
                    Require(replacement.GetComponentsInChildren<CandySummoningColumn>().Any(c=>c.Target==fixture.transform),
                        "Replacement snapshots the newly added traveller");
                    GameEvents.RaiseDepartureStarted(10);
                    Require(CurrentTravel()==replacement,"Duplicate active departure keeps its controller");
                    yield return null;
                    Require(!obsolete&&CurrentTravel()==replacement,"Deferred obsolete cleanup preserves the replacement owner");
                    Object.Destroy(fixture);fixture=null;yield return null;
                }
            }
            finally
            {
                if(fixture)Object.Destroy(fixture);
                GameEvents.RaiseEmoted(Services.Session.Player.gameObject,"DepartureCancelled");
            }
            float deadline=Time.realtimeSinceStartup+2;
            while(CurrentTravel()&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!CurrentTravel(),"Reentry fixtures clean up their final controller");
        }
        static IEnumerator Validate()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            float deadline=Time.realtimeSinceStartup+90;
            while(!IslandLobby.Current&&Time.realtimeSinceStartup<deadline)yield return null;
            var lobby=IslandLobby.Current;Require(lobby,"Actual lobby loads");
            var player=lobby.Player;Require(NetworkActors.Alive(player),"Local player exists");
            var original=new Pose(player.transform.position,player.transform.rotation);player.InputSuspended=true;
            var motor=lobby.Survivor();bool motorEnabled=motor.enabled;motor.enabled=false;
            var staged=new Pose(original.position+new Vector3(-3,0,-3),original.rotation);player.Place(staged.position,staged.rotation);
            var shader=Resources.Load<Shader>("CandyScenery/CandySummoningColumn");
            Require(shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"World strand shader imports without shader errors");
            var reentry=ReentryChecks();
            try{while(reentry.MoveNext())yield return reentry.Current;}
            finally{(reentry as IDisposable)?.Dispose();}
            var remote=Fixture("Remote human selection fixture",original.position+Vector3.right*8,NetworkKinds.Survivor,true);
            var inactive=Fixture("Inactive remote fixture",original.position+Vector3.left*8,NetworkKinds.Survivor,false);
            var hand=Fixture("Remote hand fixture",original.position+Vector3.forward*8,NetworkKinds.Hand,true);
            deadline=Time.realtimeSinceStartup+5;
            while((lobby.SurvivorBots?.Count??0)==0&&Time.realtimeSinceStartup<deadline)yield return null;
            var lobbyBots=lobby.SurvivorBots.Members.Where(b=>b&&b.activeInHierarchy).ToArray();
            Require(lobbyBots.Length>0,"Actual lobby survivor bots are available for departure coverage");
            GameEvents.RaiseDepartureStarted(10);yield return null;
            var beacon=Object.FindFirstObjectByType<CandySessionTravel>();Require(beacon,"Departure event creates the world beacon");
            var columns=Object.FindObjectsByType<CandySummoningColumn>(FindObjectsSortMode.None);
            File.AppendAllText(Report,"MEASURE columns="+columns.Length+" expected="+(2+lobbyBots.Length)+" snapshottedBots="+lobbyBots.Length+" liveBots="+lobby.SurvivorBots.Count+" controllers="+Object.FindObjectsByType<CandySessionTravel>(FindObjectsSortMode.None).Length+" arriving="+beacon.Arriving+"\n");
            File.AppendAllText(Report,"MEASURE targets="+string.Join(" | ",columns.Select(c=>c.Target?c.Target.name+"#"+c.Target.GetInstanceID():"<destroyed>"))+"\n");
            Require(columns.Length==2+lobbyBots.Length,"Local player, remote survivor and every active lobby bot receive exactly one departure column");
            Require(lobbyBots.All(b=>columns.Count(c=>c.Target==b.transform)==1),"Every lobby bot has its own exterior-visible departure strands");
            Require(!columns.Any(c=>c.Target==inactive.transform||c.Target==hand.transform),"Inactive actors and flying hands are excluded");
            Require(columns.All(c=>c.GetComponent<MeshFilter>().sharedMesh==columns[0].GetComponent<MeshFilter>().sharedMesh&&c.GetComponent<MeshRenderer>().sharedMaterial==columns[0].GetComponent<MeshRenderer>().sharedMaterial),"All human and bot strands share one mesh and material");
            var local=columns.Single(c=>c.Target==player.transform);
            Require(columns.Any(c=>c.Target==remote.transform),"Remote human has an exterior-visible column on this client");
            Require((local.Origin-staged.position).sqrMagnitude<.01f,"Column begins at its own player's position");
            Require(beacon.GetComponentsInChildren<Collider>().Length==0&&beacon.GetComponentsInChildren<Rigidbody>().Length==0,"Effect adds no physics bodies or colliders");
            Require(beacon.GetComponentsInChildren<Light>().Length==0,"Effect adds no dynamic lights");
            Require(columns.All(c=>c.GetComponent<MeshRenderer>().shadowCastingMode==ShadowCastingMode.Off),"Filaments do not add shadow rendering");
            Require(local.GetComponent<MeshFilter>().sharedMesh.vertexCount<5000,"Column is a bounded small shared mesh");
            Phase(beacon,.4f);Require(beacon.FadeAmount==0,"Screen stays clear during the summoning phase");
            GameEvents.RaiseDepartureStarted(10);yield return null;
            Require(Object.FindObjectsByType<CandySessionTravel>(FindObjectsSortMode.None).Length==1,"Repeated departure cue does not duplicate the beacon");
            Object.Destroy(remote.gameObject);Object.Destroy(inactive.gameObject);Object.Destroy(hand.gameObject);yield return null;
            Phase(beacon,.4f);
            var outsideObject=new GameObject("Exterior beacon capture camera");var outside=outsideObject.AddComponent<Camera>();outside.enabled=false;
            outside.fieldOfView=58;outside.clearFlags=CameraClearFlags.Skybox;outside.stereoTargetEye=StereoTargetEyeMask.None;
            outside.transform.position=staged.position+new Vector3(5,2.4f,-6);outside.transform.LookAt(staged.position+Vector3.up*1.1f);
            Capture(outside,"Summoning-Exterior-Ground.png");
            player.Place(staged.position+Vector3.up*4,staged.rotation);player.InputSuspended=true;yield return null;Phase(beacon,.45f);
            var block=new MaterialPropertyBlock();local.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Require(block.GetFloat("_Lift")>3,"Light collar follows the actual lifted player");
            outside.transform.position=player.transform.position+new Vector3(5,1.7f,-6);outside.transform.LookAt(player.transform.position+Vector3.up);
            Capture(outside,"Summoning-Exterior-Lift.png");
            var eye=player.ViewCamera;Require(eye,"Actual interior player camera exists");
            local.SetVisible(false);var baseline=Capture(eye,null);local.SetVisible(true);
            var withBeacon=Capture(eye,"Summoning-Interior.png");int changed=0;
            for(int i=0;i<baseline.Length;i++)if(Math.Abs(baseline[i].r-withBeacon[i].r)+Math.Abs(baseline[i].g-withBeacon[i].g)+Math.Abs(baseline[i].b-withBeacon[i].b)>24)changed++;
            float fraction=changed/(float)baseline.Length;File.AppendAllText(Report,"MEASURE interior changed-pixel fraction="+fraction.ToString("F4")+"\n");
            Require(fraction>.0002f&&fraction<.8f,"Strands render from inside while leaving most scenery visible");
            Phase(beacon,.9f);Require(beacon.FadeAmount>0&&beacon.FadeAmount<1,"Soft white fade begins only at the end");
            Phase(beacon,.999f);Require(beacon.FadeAmount>.99f,"White covers the scene change");
            ScreenCapture.CaptureScreenshot(ProjectPath.Report("Summoning-Final-White.png"));
            deadline=Time.realtimeSinceStartup+1;while(Time.realtimeSinceStartup<deadline)yield return null;
            GameEvents.RaiseEmoted(lobby.gameObject,"DepartureCancelled");deadline=Time.realtimeSinceStartup+2;
            while(Object.FindFirstObjectByType<CandySessionTravel>()&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!Object.FindFirstObjectByType<CandySessionTravel>()&&Object.FindObjectsByType<CandySummoningColumn>(FindObjectsSortMode.None).Length==0,"Cancellation removes all columns and the fade");
            Object.Destroy(outsideObject);player.Place(original.position,original.rotation);motor.enabled=motorEnabled;player.InputSuspended=false;
            Require(lobby.CanStart,"Actual lobby can start the game");float startY=player.transform.position.y;
            lobby.StartOrCancel();deadline=Time.realtimeSinceStartup+.3f;
            while(Time.realtimeSinceStartup<deadline)yield return null;
            Require(player.transform.position.y>startY+.5f,"Existing gameplay still physically lifts the player");
            deadline=Time.realtimeSinceStartup+90;
            while(Services.Session?.Demo==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(Services.Session?.Demo!=null&&Services.World!=null,"Real departure reaches playable Game scene");
            var session=Services.Session as CastlePlaySession;
            var descent=session.GetComponent<PlayerSpawnArrival>();Require(descent,"GameSession attaches initial arrival motion without changing the session controller");
            deadline=Time.realtimeSinceStartup+10;while(!descent.Active&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(descent.Active&&descent.DropHeight==10,"Open initial spawn begins a real ten-metre descent");
            float spawnHealth=session.Player.gameObject.GetComponent<IDamageable>().Health;
            var destination=descent.Destination;float high=session.Player.transform.position.y;
            var arrival=Object.FindFirstObjectByType<CandySessionTravel>();Require(arrival&&arrival.Arriving,"White departure changes into arrival presentation");
            var gameBots=session.SurvivorBots.Members.Where(b=>b&&b.activeInHierarchy).ToArray();
            Require(gameBots.Length>0,"Actual game survivor bots spawned");
            var botColumns=Object.FindObjectsByType<CandySummoningColumn>(FindObjectsSortMode.None);
            Require(gameBots.All(b=>botColumns.Count(c=>c.Target==b.transform)==1),"Every initial game bot receives exactly one arrival strand column");
            var shortColumn=Object.FindObjectsByType<CandySummoningColumn>(FindObjectsSortMode.None).Single(c=>c.Target==session.Player.transform);
            float bodyHeight=session.Player.gameObject.GetComponent<IMovementState>().Height;
            float strandHeight=shortColumn.GetComponent<MeshFilter>().sharedMesh.vertices.Max(v=>v.y);
            Require(strandHeight>bodyHeight+.1f&&strandHeight<=bodyHeight+1.01f,"Arrival strands end just above the character rather than reaching the sky");
            deadline=Time.realtimeSinceStartup+30;while(descent.Active&&descent.Progress<.48f&&Time.realtimeSinceStartup<deadline)yield return null;
            typeof(CandySessionTravel).GetMethod("Render",Flags).Invoke(arrival,null);
            File.AppendAllText(Report,"MEASURE arrival high="+high.ToString("F3")+" y="+session.Player.transform.position.y.ToString("F3")+" destination="+destination.y.ToString("F3")+" progress="+descent.Progress.ToString("F3")+" active="+descent.Active+" fade="+arrival.FadeAmount.ToString("F3")+"\n");
            Require(session.Player.transform.position.y<high-.5f&&session.Player.transform.position.y>destination.y,"The actual character moves downward while the white fade clears");
            File.AppendAllText(Report,"MEASURE arrival fade="+arrival.FadeAmount.ToString("F3")+" drop remaining="+(session.Player.transform.position.y-destination.y).ToString("F3")+" active="+descent.Active+"\n");
            Require(arrival.FadeAmount<.05f,"Soft white clears during the descent");
            Require((shortColumn.transform.position-session.Player.transform.position).sqrMagnitude<.02f,"Short strands follow the falling character without a frame of position lag");
            var arrivalCameraObject=new GameObject("Arrival exterior capture camera");var arrivalCamera=arrivalCameraObject.AddComponent<Camera>();arrivalCamera.enabled=false;
            arrivalCamera.fieldOfView=58;arrivalCamera.clearFlags=CameraClearFlags.Skybox;arrivalCamera.stereoTargetEye=StereoTargetEyeMask.None;
            arrivalCamera.transform.position=destination+new Vector3(8,5,-10);arrivalCamera.transform.LookAt(destination+Vector3.up*4);
            Capture(arrivalCamera,"Arrival-Route.png");
            arrivalCamera.transform.position=session.Player.transform.position+new Vector3(1.7f,.75f,-2.1f);arrivalCamera.transform.LookAt(session.Player.transform.position+Vector3.up*(bodyHeight*.65f));
            Capture(arrivalCamera,"Arrival-Exterior.png");Capture(session.Player.ViewCamera,"Arrival-Interior.png");
            deadline=Time.realtimeSinceStartup+30;while(descent.Active&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!descent.Active&&Mathf.Abs(session.Player.transform.position.y-destination.y)<.2f,"Open arrival lands at its intended spawn point");
            Require(session.Player.gameObject.GetComponent<IDamageable>().Health>=spawnHealth-.001f,"The initial drop does not inflict fall damage");
            deadline=Time.realtimeSinceStartup+6;
            while(Object.FindFirstObjectByType<CandySessionTravel>()&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!Object.FindFirstObjectByType<CandySessionTravel>()&&Object.FindObjectsByType<CandySummoningColumn>(FindObjectsSortMode.None).Length==0,"Arrival clears old world columns and the white fade");
            GameEvents.RaiseEmoted(session.gameObject,"TravelArrived");yield return null;
            Require(!descent.Active&&!Object.FindFirstObjectByType<CandySessionTravel>(),"A repeated ready cue does not replay the initial spawn");
            var actualMotor=session.Survivor();actualMotor.enabled=false;session.Player.Place(destination,session.Player.transform.rotation);
            var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.name="Arrival ceiling test fixture";roof.layer=Layers.Surface;roof.transform.position=destination+Vector3.up*(bodyHeight+1);roof.transform.localScale=new Vector3(4,.2f,4);Physics.SyncTransforms();
            typeof(PlayerSpawnArrival).GetMethod("Begin",Flags).Invoke(descent,null);
            Require(descent.Active&&descent.DropHeight>=0&&descent.DropHeight<2&&session.Player.transform.position.y+bodyHeight<roof.GetComponent<Collider>().bounds.min.y,"A ceiling limits the arrival to the available clearance below it");
            deadline=Time.realtimeSinceStartup+30;
            while(descent.Active&&descent.Progress<.67f&&Time.realtimeSinceStartup<deadline)yield return null;
            arrivalCamera.transform.position=destination+new Vector3(3,1.6f,-4);arrivalCamera.transform.LookAt(destination+Vector3.up);
            Capture(arrivalCamera,"Arrival-Covered.png");
            deadline=Time.realtimeSinceStartup+8;while(descent.Active&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!descent.Active&&Mathf.Abs(session.Player.transform.position.y-destination.y)<.2f,"Covered arrival lands without moving through the roof");
            roof.transform.position=destination+Vector3.up*(bodyHeight+.12f);Physics.SyncTransforms();
            session.Player.Place(destination,session.Player.transform.rotation);typeof(PlayerSpawnArrival).GetMethod("Begin",Flags).Invoke(descent,null);
            Require(descent.DropHeight==0&&(session.Player.transform.position-destination).sqrMagnitude<.01f,"A very low ceiling keeps the player safely at the spawn point");
            deadline=Time.realtimeSinceStartup+8;while(descent.Active&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!descent.Active,"Zero-clearance arrival still completes");
            Object.Destroy(roof);yield return null;Physics.SyncTransforms();
            session.Player.Place(destination,session.Player.transform.rotation);typeof(PlayerSpawnArrival).GetMethod("Begin",Flags).Invoke(descent,null);
            Require(descent.DropHeight==10,"Removing the ceiling restores the open route");
            var obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);obstruction.name="Mid-arrival moving obstruction fixture";obstruction.layer=Layers.Surface;obstruction.transform.position=destination+Vector3.up*4;obstruction.transform.localScale=new Vector3(4,.2f,4);Physics.SyncTransforms();
            deadline=Time.realtimeSinceStartup+8;while(descent.Active&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!descent.Active&&session.Player.transform.position.y>=obstruction.GetComponent<Collider>().bounds.max.y-.1f,"A new obstruction during descent stops the player above it without tunnelling");
            Object.Destroy(obstruction);yield return null;Physics.SyncTransforms();
            var trigger=GameObject.CreatePrimitive(PrimitiveType.Cube);trigger.name="Arrival trigger-only fixture";trigger.layer=Layers.Surface;trigger.transform.position=destination+Vector3.up*(bodyHeight+1);trigger.transform.localScale=new Vector3(4,.2f,4);trigger.GetComponent<Collider>().isTrigger=true;Physics.SyncTransforms();
            session.Player.Place(destination,session.Player.transform.rotation);typeof(PlayerSpawnArrival).GetMethod("Begin",Flags).Invoke(descent,null);
            Require(descent.DropHeight==10,"Trigger-only volumes do not block an open arrival route");
            var replacement=destination+Vector3.up*4;session.Player.Place(replacement,session.Player.transform.rotation);yield return null;
            Require(!descent.Active&&(session.Player.transform.position-replacement).sqrMagnitude<.01f,"Another gameplay teleport cancels arrival without pulling the character back into the drop");
            Object.Destroy(trigger);Object.Destroy(arrivalCameraObject);session.Player.Place(destination,session.Player.transform.rotation);actualMotor.enabled=true;
        }
        static Color32[] Capture(Camera camera,string file)
        {
            var before=camera.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(2000,1250,24);
            var image=new Texture2D(2000,1250,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,2000,1250),0,0);image.Apply();var pixels=image.GetPixels32();
                if(file!=null)File.WriteAllBytes(ProjectPath.Report(file),image.EncodeToPNG());return pixels;
            }
            finally{camera.targetTexture=before;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(image);}
        }
    }
    public sealed class SummoningRemoteFixture : MonoBehaviour,INetworkActorView
    {
        public string kind;public bool active;
        public string NetworkKind=>kind;public bool NetworkActive=>active;public Vector3 NetworkPosition=>transform.position;
        public void WriteState(BinaryWriter writer){}public void ReadState(BinaryReader reader,bool visible){}
        public void Render(float deltaTime){}public void Suspend(){active=false;}
    }
}
