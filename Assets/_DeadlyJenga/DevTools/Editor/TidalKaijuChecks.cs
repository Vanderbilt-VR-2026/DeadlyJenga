using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.World.Hazards;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]public static class TidalKaijuChecks
    {
        const string Key="TidalKaijuChecks";
        [Serializable]sealed class Scenes{public SceneSetup[] scenes;}
        static string Report=>ProjectPath.Report("TidalKaiju.txt");
        static int checks,errors;
        static TidalKaijuChecks()
        {
            EditorApplication.update+=()=>{
                string request=ProjectPath.Report("TidalKaiju.request");
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||File.Exists(ProjectPath.Report("TsunamiChecks.request"))||!File.Exists(request))return;
                File.Delete(request);Run();
            };
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    checks=errors=0;Application.logMessageReceived+=Log;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();
                    EditorApplication.delayCall+=()=>{var root=new GameObject("Tidal integration checks");Object.DontDestroyOnLoad(root);root.AddComponent<TidalCheckDriver>().StartCoroutine(Guard());};
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).scenes);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);Time.timeScale=SessionState.GetFloat(Key+"Time",1);
                    File.AppendAllText(Report,"PASS Original scenes, settings and timing restored.\n");
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Tidal Kaiju and Low Gravity")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var request=ProjectPath.Report("TidalKaiju.request");File.WriteAllText(request,"run");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;File.Delete(request);
            var scenes=EditorSceneManager.GetSceneManagerSetup();if(scenes.Any(s=>s.isLoaded&&UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))throw new Exception("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetFloat(Key+"Time",Time.timeScale);SessionState.SetBool(Key,true);
            File.WriteAllText(Report,"Actual Game, physical low gravity and presentation fixtures. No headset / GPU performance certification.\n");
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";Time.timeScale=1;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type){if(message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.")){File.AppendAllText(Report,"INFO Known Fusion multi-peer editor diagnostic.\n");return;}if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Check(bool pass,string name){File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+name+"\n");if(!pass)throw new Exception(name);checks++;}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Checks());
            while(stack.Count>0)
            {
                bool next=false;object current=null;Exception error=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}if(current is IEnumerator child){stack.Push(child);continue;}yield return current;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            File.AppendAllText(Report,"COMPLETE checks="+checks+" runtimeErrors="+errors+"\n");EditorApplication.ExitPlaymode();
        }
        static IEnumerator Checks()
        {
            float end=Time.realtimeSinceStartup+40;while(!(Services.Session is CastlePlaySession s&&s.Modes&&Services.World!=null)&&Time.realtimeSinceStartup<end)yield return null;
            var session=Services.Session as CastlePlaySession;Check(session&&Services.World!=null,"Real Game initializes");
            session.SetMenu(false);session.SurvivorBots.AutoFill=false;if(session.Social!=null)session.Social.VoiceEnabled=false;
            var rules=session.Modes.Rules.Copy();rules.island=IslandRule.Walkable;rules.countdownSeconds=0;rules.automaticDisasters=false;session.Modes.Configure(rules);session.Modes.StartRound();yield return new WaitForSeconds(.3f);
            foreach(string name in new[]{"CandySpirit","CandyWetSurface"}){var shader=Resources.Load<Shader>("CandyScenery/"+name);Check(shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),name+" shader compiles");}
            var gravityBot=session.SurvivorBots.SpawnOne();Check(gravityBot,"An actual population bot spawns on the real map for the gravity check");
            var oldWorld=Services.World;var mapObject=new GameObject("Physical low gravity fixture map");mapObject.transform.position=new Vector3(110,0,100);var map=mapObject.AddComponent<FakeWorldQuery>();map.waterHeight=-1000;map.playableBounds=new Bounds(new Vector3(0,30,0),new Vector3(60,100,60));map.topHeight=10;
            var body=Object.Instantiate(oldWorld.Blocks.First(b=>b));body.name="Wet and low gravity physical block";body.transform.SetParent(null);body.isKinematic=false;body.useGravity=true;body.position=new Vector3(110,35,100);body.linearVelocity=Vector3.zero;map.blocks.Add(body);
            var player=session.Survivor();player.Place(new Vector3(115,35,100),Quaternion.identity);player.InputSuspended=true;
            yield return new WaitForSeconds(.6f);float normalBody=-body.linearVelocity.y,normalPlayer=-player.VerticalSpeed;
            Check(normalBody>3&&normalPlayer>5,"Normal block and player gravity establish real falling baselines");
            var prefab=Resources.Load<GameObject>("DemoDisasters/AntiGravity");var hazard=Object.Instantiate(prefab).GetComponent<BasicDisaster>();var values=hazard.Parameters.Select(p=>p.initial).ToArray();values[Array.FindIndex(hazard.Parameters.ToArray(),p=>p.id=="warning")]=0;hazard.Configure(values);Check(DemoDisasters.Label(prefab)=="Low Gravity","Disaster menu exposes Low Gravity while retaining its stable prefab ID");
            body.position=new Vector3(110,35,100);body.linearVelocity=Vector3.zero;player.Place(new Vector3(115,35,100),Quaternion.identity);player.InputSuspended=true;hazard.Begin();
            yield return new WaitForSeconds(.6f);
            File.AppendAllText(Report,"INFO falling speeds normal body="+normalBody+" player="+normalPlayer+" low body="+(-body.linearVelocity.y)+" player="+(-player.VerticalSpeed)+"\n");
            Check(body.linearVelocity.y<0&&-body.linearVelocity.y<normalBody*.6f,"Low-gravity block falls downward slowly without upward lift");
            Check(player.VerticalSpeed<0&&-player.VerticalSpeed<normalPlayer*.6f,"Actual player / bot motor falls downward slowly");
            yield return new WaitForSeconds(2);
            Check(body.linearVelocity.y>=-4.3f&&player.VerticalSpeed>=-4.1f,"Falling speed is bounded for blocks and players");
            hazard.End();float before=body.linearVelocity.y;yield return new WaitForSeconds(.4f);Check(body.linearVelocity.y<before-2,"Stopping the disaster restores natural block gravity");Check(body.useGravity,"Block gravity flag was never overwritten");
            var bot=gravityBot;
            var botMotor=bot.GetComponent<CastleSurvivor>();botMotor.Place(new Vector3(118,35,100),Quaternion.identity);botMotor.InputSuspended=true;hazard.Begin();yield return new WaitForSeconds(.6f);
            Check(botMotor.externallyDriven&&botMotor.VerticalSpeed<0&&botMotor.VerticalSpeed>-3,"An actual externally driven bot also falls slowly");hazard.End();Object.Destroy(bot);
            var wet=CandyWaterContactEffects.Current;Check(wet,"Shared wet/splash service attaches to the real session");
            map.waterHeight=0;body.isKinematic=true;body.position=new Vector3(110,10,100);yield return null;
            var visual=body.GetComponent<IClimbable>().Visual;var original=visual.GetComponentsInChildren<MeshRenderer>().First();var material=original.sharedMaterial;int colliders=body.GetComponentsInChildren<Collider>().Length;
            wet.Soak(body,body.position,true);yield return null;
            Check(wet.WetRendererCount>0,"Hand water contact creates a drying glossy block coating");
            Check(original.sharedMaterial==material&&body.GetComponentsInChildren<Collider>().Length==colliders,"Wet coating preserves original block material and all physics");
            Check(visual.GetComponentsInChildren<MeshRenderer>().Any(r=>r.name=="Visual - drying water stain"),"Water coating follows the block visual");
            for(int i=0;i<40;i++)
            {
                var wave=new WaveFrame{active=true,center=new Vector3(110,10,100),direction=Quaternion.Euler(0,i*17,0)*Vector3.forward,width=30,depth=20,height=12,waterHeight=0};
                var point=wave.center+new Vector3(Mathf.Sin(i)*18,0,Mathf.Cos(i)*15);
                float physics=TsunamiHazard.SurfaceHeight(wave,point),art=CandyWaterContactEffects.WaveSurface(wave,point);
                Check(float.IsNegativeInfinity(physics)&&float.IsNegativeInfinity(art)||Mathf.Abs(physics-art)<.0001f,"Wet tsunami coverage matches authoritative profile "+i);
            }
            wet.enabled=false;wet.enabled=true;yield return null;Check(wet.WetRendererCount==0,"Disabling the wet effect cleans up coatings");Capture(body.position+new Vector3(3,2,3),body.position,"Tidal-Dry-Block.png");
            var waveObject=new GameObject("Wave wetting fixture");var waveView=waveObject.AddComponent<TidalWaveFixture>();waveView.wave=new WaveFrame{active=true,center=body.position,direction=Vector3.forward,width=30,depth=20,height=20,waterHeight=0};Tracked.Waves.Add(waveView);
            yield return new WaitForSeconds(.2f);Check(wet.WetRendererCount>0,"A tsunami frame wets an actual block through the shared scanner");Tracked.Waves.Remove(waveView);Object.Destroy(waveObject);
            var rig=new GameObject("Kaiju appearance view fixture");var spirit=rig.AddComponent<TidalSpiritFixture>();spirit.Build(new Vector3(110,15,85));yield return new WaitForSeconds(1.3f);
            Check(Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Any(f=>f.sharedMesh&&f.sharedMesh.name=="Sculpted tidal kaiju skull"),"Kaiju uses its new sculpted skull rather than a sphere");
            var model=Object.FindObjectsByType<CandySeaSpirit>(FindObjectsSortMode.None).FirstOrDefault(c=>c.transform.Find("Visual - ocean spirit bust and arms"));
            Check(model,"Sea-spirit appearance discovers the existing Core view contract");Check(model.GetComponentsInChildren<Collider>().Length==0,"Kaiju art creates no colliders");
            Check(model.GetComponentsInChildren<MeshRenderer>().Count()>30,"Crown, gills, luminous ribs, teeth and dorsal armor render");
            Capture(new Vector3(127,24,110),new Vector3(110,16,85),"Tidal-Kaiju.png");
            Capture(body.position+new Vector3(3,2,3),body.position,"Tidal-Wet-Block.png");
            spirit.active=false;yield return new WaitForSeconds(1.2f);Check(!rig.GetComponentsInChildren<Collider>().Any(),"Visual fixture leaves gameplay geometry alone");
            Object.Destroy(rig);Object.Destroy(hazard.gameObject);Object.Destroy(body.gameObject);Object.Destroy(mapObject);Services.World=oldWorld;
            player.Place(oldWorld.SafeSpawnPoint(),Quaternion.identity);player.InputSuspended=true;
            ((ISeaSpiritControls)session).SetSeaSpirit(true);yield return new WaitForSeconds(1.5f);Check(((ISeaSpiritControls)session).SeaSpiritActive,"Real playable AR sea spirit still activates with the new appearance");((ISeaSpiritControls)session).SetSeaSpirit(false);
            session.Modes.ReturnToLobby();float lobbyWait=Time.realtimeSinceStartup+30;while(!IslandLobby.Current&&Time.realtimeSinceStartup<lobbyWait)yield return null;Check(IslandLobby.Current,"Actual Game returns to lobby after water / gravity checks");
            var networkAvatar=Services.Session.Player.gameObject.GetComponent<DeadlyJenga.CandyPlayers.CandyPlayerAvatar>();var savedAppearance=networkAvatar.Appearance;
            try
            {
                var networkAppearance=savedAppearance;networkAppearance.skin=AvatarSkin.Penguin;networkAppearance.hair=AvatarHair.Braids;networkAppearance.eyewear=AvatarEyewear.Visor;networkAppearance.clothing=AvatarClothing.FootballPads;networkAppearance.poweredArmor=true;networkAvatar.ApplyAppearance(networkAppearance);
                var network=(IEnumerator)typeof(AvatarEnemyChecks).GetMethod("CheckNetwork",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{IslandLobby.Current,networkAvatar.Appearance.accessories});
                yield return network;Check(true,"Two real Photon runners exchange non-default skin, hair, eyewear, clothing and armor states");
            }
            finally{if(networkAvatar)networkAvatar.ApplyAppearance(savedAppearance);}
            Check(errors==0,"No unexpected runtime errors");
        }
        static void Capture(Vector3 position,Vector3 target,string name)
        {
            var go=new GameObject("Tidal capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.03f;camera.fieldOfView=45;camera.farClipPlane=80;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.01f,.025f,.045f);camera.cullingMask=~(1<<5);camera.transform.position=position;camera.transform.LookAt(target);
            var texture=new RenderTexture(1280,960,24);var previous=RenderTexture.active;camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;var image=new Texture2D(1280,960,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,960),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(image);Object.Destroy(texture);Object.Destroy(go);
        }
    }
    public sealed class TidalCheckDriver:MonoBehaviour{}
    public sealed class TidalWaveFixture:MonoBehaviour,IWaveState{public WaveFrame wave;public WaveFrame Wave=>wave;}
    public sealed class TidalSpiritFixture:MonoBehaviour,ISeaSpiritView
    {
        public bool active=true;public bool Active=>active;
        public Transform BustAnchor{get;private set;}public Transform LeftShoulder{get;private set;}public Transform RightShoulder{get;private set;}public Transform LeftHandAnchor{get;private set;}public Transform RightHandAnchor{get;private set;}
        Transform Anchor(string name,Vector3 position){var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=position;return go.transform;}
        public void Build(Vector3 point){BustAnchor=Anchor("Bust",point);LeftShoulder=Anchor("Left shoulder",point+new Vector3(-4,-3,0));RightShoulder=Anchor("Right shoulder",point+new Vector3(4,-3,0));LeftHandAnchor=Anchor("Left hand",point+new Vector3(-7,-6,5));RightHandAnchor=Anchor("Right hand",point+new Vector3(7,-6,5));}
    }
}
