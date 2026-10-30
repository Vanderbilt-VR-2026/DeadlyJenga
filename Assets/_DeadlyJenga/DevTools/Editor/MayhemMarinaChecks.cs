using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CandyArt.Marina;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class MayhemMarinaChecks
    {
        const string Key="MayhemMarinaChecks";
        [MenuItem("Tools/Deadly Jenga/Lobby/Use Mayhem Marina (Default)")]
        public static void UseMarina(){SelectLayout(false);}
        [MenuItem("Tools/Deadly Jenga/Lobby/Use Previous Lobby (Backup)")]
        public static void UseLegacy(){SelectLayout(true);}
        [MenuItem("Tools/Deadly Jenga/Lobby/Use Mayhem Marina (Default)",true)]
        static bool CanUseMarina()=>!EditorApplication.isPlayingOrWillChangePlaymode;
        [MenuItem("Tools/Deadly Jenga/Lobby/Use Previous Lobby (Backup)",true)]
        static bool CanUseLegacy()=>!EditorApplication.isPlayingOrWillChangePlaymode;
        static void SelectLayout(bool legacy)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before changing lobby layout.");
            if(legacy)PlayerPrefs.SetInt(IslandLobby.LegacyLobbyPreference,1);else PlayerPrefs.DeleteKey(IslandLobby.LegacyLobbyPreference);
            PlayerPrefs.Save();Debug.Log((legacy?"Previous lobby backup":"Mayhem Marina default")+" selected. Enter Play Mode to load it.");
        }
        public static void RunBackup(){RunLayout(true);}
        public static void RunDefault(){RunLayout(false);}
        static void RunLayout(bool legacy)
        {
            SessionState.SetInt(Key+"Preference",PlayerPrefs.GetInt(IslandLobby.LegacyLobbyPreference,-1));
            SessionState.SetBool(Key+"Layout",true);SessionState.SetBool(Key+"Legacy",legacy);SelectLayout(legacy);
            File.WriteAllText(Report,"Actual Unity lobby default/backup validation.\n");
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));EditorApplication.EnterPlaymode();
        }
        static string Report=>ProjectPath.Report("MayhemMarina.txt");
        static int errors;static bool failed;static Vector3 captureOrigin;
        static MayhemMarinaChecks(){EditorApplication.playModeStateChanged+=State;}
        public static void RunBatch()
        {
            try
            {
                if(!Application.isBatchMode)throw new InvalidOperationException("Use the reserved batch editor.");
                InstallArt();File.WriteAllText(Report,"Actual Unity lobby geometry and integration checks. Staged avatar rigs are used for cinematic views; they are not measured bot behavior.\n");
                SessionState.SetBool(Key+"Network",false);SessionState.SetBool(Key+"Solo",false);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));EditorApplication.EnterPlaymode();
            }
            catch(Exception e){File.AppendAllText(Report,"PREFLIGHT FAIL "+e+"\n");Debug.LogException(e);EditorApplication.Exit(2);}
        }
        public static void RunSolo()
        {
            File.WriteAllText(Report,"Actual Game.unity solo smoke test with Marina sources installed; shared lobby hooks remain pending.\n");
            SessionState.SetBool(Key+"Network",false);SessionState.SetBool(Key+"Solo",true);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        public static void RunNetwork()
        {
            File.WriteAllText(Report,"Actual two-peer Photon lobby selection test with integrated Marina; no Quest hardware or full two-client match-travel claim.\n");
            SessionState.SetBool(Key+"Network",true);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));EditorApplication.EnterPlaymode();
        }
        static void InstallArt()
        {
            foreach(string name in new[]{"MarinaWood.png","MarinaStone.png","MarinaSky.png"})
            {
                string path=ProjectPath.Find(name);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer){importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.SaveAndReimport();}
            }
            string folder=ProjectPath.FolderOf("MarinaWood.png");string destination=folder+"/MarinaSky.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(destination))
            {
                var source=AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find("CarnivalPanorama-v1.mat"));
                if(source)AssetDatabase.CreateAsset(new Material(source){name="Marina clear coastal sky"},destination);
            }
            var marinaSky=AssetDatabase.LoadAssetAtPath<Material>(destination);
            if(marinaSky){marinaSky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPath.Find("MarinaSky.png")));EditorUtility.SetDirty(marinaSky);}
            AssetDatabase.SaveAssets();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                errors=0;failed=false;Application.logMessageReceived+=Log;EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
                var go=new GameObject("Marina validation driver");Object.DontDestroyOnLoad(go);go.AddComponent<MayhemMarinaChecksDriver>().StartCoroutine(Guard());
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                if(SessionState.GetBool(Key+"Layout",false))
                {
                    int preference=SessionState.GetInt(Key+"Preference",-1);
                    if(preference<0)PlayerPrefs.DeleteKey(IslandLobby.LegacyLobbyPreference);else PlayerPrefs.SetInt(IslandLobby.LegacyLobbyPreference,preference);
                    PlayerPrefs.Save();SessionState.SetBool(Key+"Layout",false);
                }
                SessionState.SetBool(Key,false);Application.logMessageReceived-=Log;AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();EditorApplication.Exit(errors==0&&!failed?0:2);
            }
        }
        static void Log(string value,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+value+"\n"+stack+"\n");}}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(SessionState.GetBool(Key+"Layout",false)?RunLayoutScene():SessionState.GetBool(Key+"Network",false)?RunNetworkScene():SessionState.GetBool(Key+"Solo",false)?RunSoloScene():Run());
            while(stack.Count>0)
            {
                bool next=false;object value=null;
                try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}
                catch(Exception e){failed=true;File.AppendAllText(Report,"FAIL "+e+"\n");break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            File.AppendAllText(Report,$"COMPLETE failed={failed} runtimeErrors={errors}\n");EditorApplication.ExitPlaymode();
        }
        static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
        static IEnumerator Wait(float duration)
        {float end=Time.realtimeSinceStartup+duration;while(Time.realtimeSinceStartup<end)yield return null;}
        static IEnumerator RunLayoutScene()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            float until=Time.realtimeSinceStartup+45;HomeSession home;
            while(!(home=Object.FindFirstObjectByType<HomeSession>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(home,"Home scene loaded");home.RandomLobby();until=Time.realtimeSinceStartup+45;IslandLobby lobby;
            while(!(lobby=Object.FindFirstObjectByType<IslandLobby>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(lobby&&lobby.View(),"Lobby and existing control view loaded");yield return Wait(2);
            bool legacy=SessionState.GetBool(Key+"Legacy",false);var view=lobby.View();
            Check(legacy?Mathf.Approximately(lobby.Radius,9):Mathf.Approximately(lobby.Radius,37),"Correct layout bounds selected");
            Check(legacy?!lobby.GetComponent<MayhemMarina>():lobby.GetComponent<MayhemMarina>().Built,"Correct layout art selected");
            Check(NetworkActors.Alive(lobby.Player),"Local survivor alive");Physics.SyncTransforms();
            Check(Physics.Raycast(lobby.SafeSpawnPoint()+Vector3.up*2,Vector3.down,5,~(1<<Layers.Survivor),QueryTriggerInteraction.Ignore),"Selected lobby safe spawn has solid support");
            if(legacy)
            {
                Check(lobby.GetComponents<CandyLobbyResort>().Any(x=>x.enabled),"Previous candy resort preserved and active");
                foreach(string name in new[]{"JOIN","HOST","MAP + MODE"})
                    Check(view.transform.Find(name)&&view.transform.Find(name).gameObject.activeInHierarchy,name+" previous terminal present");
                lobby.Player.Place(view.WardrobePosition+Vector3.up*.04f,Quaternion.identity);view.SetWardrobeOpen(true);yield return Wait(.2f);
                Check(view.WardrobeOpen,"Backup wardrobe opens");view.SetWardrobeOpen(false);
                Check(!lobby.GetComponentInChildren<DeadlyJenga.World.Maps.MarinaPracticePool>(),"Backup has no Marina practice overlay");
            }
            else
            {
                var screen=lobby.GetComponent<MarinaScreen>();Check(screen&&screen.Visible,"Marina facade active by default");
                foreach(string id in new[]{"marina-modes","marina-maps","marina-host","marina-join","marina-settings"})
                {Check(screen.Invoke(id),id+" opens");Check(view.InvokeChoice("marina-close"),id+" closes");}
            }
        }
        static IEnumerator RunNetworkScene()
        {
            Application.runInBackground=true;AudioListener.volume=0;float until=Time.realtimeSinceStartup+45;HomeSession home;
            while(!(home=Object.FindFirstObjectByType<HomeSession>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(home,"Actual Home scene loaded for peer validation");home.RandomLobby();until=Time.realtimeSinceStartup+45;IslandLobby lobby;
            while(!(lobby=Object.FindFirstObjectByType<IslandLobby>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(lobby&&lobby.GetComponent<MayhemMarina>(),"Actual integrated Marina loaded for two-peer test");yield return Wait(.3f);
            yield return ModeSelectionNetworkChecks.Run((passed,label)=>
            {
                Check(passed,label);
                if(label=="Second real Photon peer joins the production lobby")
                {
                    var pool=lobby.GetComponentInChildren<DeadlyJenga.World.Maps.MarinaPracticePool>();
                    Check(pool&&!pool.CanPush&&pool.Blocks.All(b=>b.Body.isKinematic),"Connected Marina practice toys remain static on shared lobby");
                }
            });
            Check(lobby&&lobby.Player.gameObject.activeInHierarchy,"Original Marina remains usable after both peers leave");
        }
        static IEnumerator RunSoloScene()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            float until=Time.realtimeSinceStartup+45;CastlePlaySession session;
            while(!(session=Object.FindFirstObjectByType<CastlePlaySession>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(session,"Actual Game.unity session exists");yield return Wait(6);
            Check(NetworkActors.Alive(session.Player),"Game.unity actual solo player is alive");
            Check(Services.World!=null&&Services.World.Blocks.Count>0,"Game.unity actual map blocks are loaded");
        }
        static IEnumerator Run()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;AudioListener.volume=0;
            float limit=Time.realtimeSinceStartup+45;HomeSession home;
            while(!(home=Object.FindFirstObjectByType<HomeSession>())&&Time.realtimeSinceStartup<limit)yield return null;
            Check(home,"Actual Home scene loaded");home.RandomLobby();
            limit=Time.realtimeSinceStartup+45;IslandLobby lobby;
            while(!(lobby=Object.FindFirstObjectByType<IslandLobby>())&&Time.realtimeSinceStartup<limit)yield return null;
            Check(lobby&&lobby.View(),"Actual Lobby scene and Presentation loaded");var view=lobby.View();
            if(Services.Session?.SurvivorBots!=null){Services.Session.SurvivorBots.AutoFill=false;Services.Session.SurvivorBots.Running=false;Services.Session.SurvivorBots.Clear();}
            captureOrigin=lobby.transform.position;
            var marina=lobby.GetComponent<MayhemMarina>();
            bool integrated=marina&&marina.Built;
            if(!integrated)
            {
                File.AppendAllText(Report,"GEOMETRY PREVIEW ONLY: shared lobby integration hooks are not installed in this snapshot. No functional-lobby claim.\n");
                foreach(var canvas in lobby.GetComponentsInChildren<Canvas>(true))canvas.gameObject.SetActive(false);
                foreach(var renderer in lobby.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                foreach(var resort in lobby.GetComponents<CandyLobbyResort>())resort.enabled=false;
                foreach(var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                    if(collider.gameObject.scene==lobby.gameObject.scene&&collider.GetComponentInParent<IPlayableCharacter>()==null)collider.enabled=false;
                File.AppendAllText(Report,"Preview fixture disables legacy collisions before testing the authored Marina mesh. This does not validate shared floor-replacement hooks.\n");
                marina=lobby.gameObject.AddComponent<MayhemMarina>();marina.Build(lobby,view.Environment);
                var screen=lobby.gameObject.AddComponent<MarinaScreen>();screen.Build(lobby,_=>{});
            }
            var practice=DeadlyJenga.World.Maps.MarinaPracticePool.BuildFor(lobby.gameObject);
            Check(practice.Blocks.Count==18,"Eighteen actual physics practice blocks built");
            Check(marina&&marina.Built,"Marina geometry completed without errors");
            Check(marina.CollisionTriangles>1000,"Authored solid collision mesh present");
            File.AppendAllText(Report,$"Architecture renderTriangles={marina.RenderTriangles}; collisionTriangles={marina.CollisionTriangles}; integration={integrated}\n");
            if(integrated)
            {
                Physics.SyncTransforms();
                foreach(string name in new[]{"Arrival","Wardrobe","Play interaction","Lounge","Roof terrace"})
                {
                    var p=marina.Anchor(name).position;Check(Physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,5,~(1<<Layers.Survivor),QueryTriggerInteraction.Ignore),name+" has real solid support");
                }
                Check(!Physics.Raycast(lobby.transform.TransformPoint(new Vector3(2,2,-8)),Vector3.down,3,~(1<<Layers.Survivor),QueryTriggerInteraction.Ignore),"Central inlet has no invisible legacy sand floor");
                Check(Physics.Raycast(lobby.transform.TransformPoint(new Vector3(2.8f,4,8.3f)),Vector3.up,out var ceiling,4,~(1<<Layers.Survivor),QueryTriggerInteraction.Ignore)&&ceiling.normal.y<-.3f,"Curved pavilion roof blocks upward passage through its underside");
                Check(lobby.Radius>30&&lobby.SafeSpawnPoint().y>1,"Larger marina lobby bounds and safe spawn active");
                var binding=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(IslandLobby).GetMethod("BuildFlatFloor",binding).Invoke(lobby,null);
                var gray=(GameObject)typeof(IslandLobby).GetField("flatFloor",binding).GetValue(lobby);Physics.SyncTransforms();
                try{Check(gray.GetComponent<Collider>().Raycast(new Ray(lobby.SafeSpawnPoint()+Vector3.up*2,Vector3.down),out _,4),"GameSession gray-box floor supports configured Marina spawn without art");}
                finally{gray.SetActive(false);Object.Destroy(gray);}
                var screen=lobby.GetComponent<MarinaScreen>();Check(screen&&screen.Visible,"Live Marina facade is active");
                Check(screen.Invoke("marina-modes"),"Mode card opens existing full mode picker");
                Check(!screen.Visible,"Facade yields to detail picker");Check(view.InvokeChoice("marina-close"),"Detail picker can return to facade");
                Check(screen.Invoke("marina-maps"),"Map card opens existing category browser");Check(view.InvokeChoice("marina-close"),"Map browser can return");
                foreach(var id in new[]{"marina-host","marina-join","marina-settings"})
                {
                    Check(screen.Invoke(id),id+" opens existing controls");Check(view.InvokeChoice("marina-close"),id+" can return");
                }
                screen.Invoke("marina-host");var back=view.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="marina-close").transform;
                view.Point(new Ray(back.position-back.forward*8,back.forward),true);
                Check(screen.Visible,"Actual UI ray can close detail from eight meters");
                Check(screen.Invoke("marina-style"),"Style quick travel opens wardrobe");yield return Wait(.25f);
                Check(view.WardrobeOpen,"Wardrobe remains open at new boutique position");
                var appearance=lobby.Player.gameObject.GetComponent<IAvatarAppearance>();var before=appearance.Appearance;
                try
                {
                    Check(view.InvokeChoice("wardrobe-0"),"Existing wardrobe entry is interactive");
                    Check(appearance.Appearance.accessories!=before.accessories,"Wardrobe applies cosmetic through existing appearance contract");
                    var mirror=view.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).First(x=>x.name=="Mirror image");
                    Check(mirror.texture is RenderTexture image&&image.IsCreated(),"Boutique mirror has a live rendered character texture");
                }
                finally{appearance.ApplyAppearance(before);view.SetWardrobeOpen(false);}
                yield return Wait(.25f);
                Check(screen.Visible,"Closing wardrobe restores facade");
                foreach(string name in new[]{"JOIN","HOST","MODE + MAP","WARDROBE"})
                {var backFace=view.transform.Find(name+" / readable back");Check(!backFace||!backFace.gameObject.activeInHierarchy,"Hidden "+name+" terminal has no orphan rear canvas");}
            }
            foreach(var renderer in Services.Session.Player.gameObject.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            if(Services.Session?.SurvivorBots!=null){Services.Session.SurvivorBots.AutoFill=false;Services.Session.SurvivorBots.Running=false;Services.Session.SurvivorBots.Clear();}
            lobby.enabled=false; // Freeze population refill only for these explicitly staged cinematic captures.
            var sky=RenderSettings.skybox;GameEvents.RaiseEmoted(lobby.gameObject,"LobbyParked");
            Check(RenderSettings.skybox!=sky,"Marina releases its global sky when parked");GameEvents.RaiseEmoted(lobby.gameObject,"LobbyResumed");
            Check(RenderSettings.skybox==sky,"Marina restores its coastal lighting when resumed");
            yield return Walk(lobby,new[]{new Vector3(-17,1.3f,-23),new Vector3(-11,1.4f,-14),new Vector3(-10,1.4f,-9),new Vector3(-9.5f,1.4f,-3),new Vector3(-7.7f,1.4f,3.5f)},"arrival pier and bridge");
            yield return Walk(lobby,new[]{new Vector3(-21,1.4f,3),new Vector3(-21,4.2f,17)},"upper terrace ramp");
            StagePeople();yield return Wait(3);
            var block=practice.Blocks[17];var start=block.transform.position;File.AppendAllText(Report,$"POOL BEFORE canPush={practice.CanPush} frozen={block.Body.isKinematic} position={start} simulation={Physics.simulationMode} timescale={Time.timeScale}\n");block.Push(Vector3.right*5,lobby.gameObject);yield return Wait(.65f);
            File.AppendAllText(Report,$"POOL AFTER position={block.transform.position} velocity={block.Body.linearVelocity} frozen={block.Body.isKinematic} displacement={Vector3.Distance(start,block.transform.position)}\n");
            Check(Vector3.Distance(start,block.transform.position)>.15f,"Practice tower responds to real physics pushes");
            practice.Rebuild();yield return Wait(.5f);
            Check(Vector3.Distance(start,block.transform.position)<.15f,"Practice tower reset restores layout");
            Capture(new Vector3(-20,8.8f,-22),new Vector3(0,2.7f,5),64,"Marina-Hero.png");yield return null;
            Capture(new Vector3(-24,13.5f,-27),new Vector3(0,2.8f,5),63,"Marina-Overview.png");yield return null;
            Capture(new Vector3(-8,3.25f,-12),new Vector3(-1.5f,3.4f,9.8f),74,"Marina-Arrival.png");yield return null;
            Capture(new Vector3(6,3.1f,1),new Vector3(13.2f,2.8f,7),65,"Marina-Lounge.png");yield return null;
            Capture(new Vector3(-17,3.2f,1),new Vector3(-13,3.4f,8),60,"Marina-Wardrobe.png");yield return null;
            Capture(new Vector3(-18,5.9f,12.5f),new Vector3(-10,4.9f,18),65,"Marina-Terrace.png");
            if(integrated)yield return Travel(lobby,marina);
        }
        static IEnumerator Travel(IslandLobby lobby,MayhemMarina marina)
        {
            lobby.enabled=true;lobby.SetMenu(false);
            var details=(ILobbyDetails)lobby;var setup=details.Setup;setup.population=1;details.ConfigureSetup(setup);
            Check(lobby.SelectMode("demo")&&lobby.SelectMap("castle"),"Actual lobby selects a short solo demo session");
            Check(lobby.GetComponent<MarinaScreen>().Invoke("marina-play"),"Marina PLAY requests the normal departure flow");
            float until=Time.realtimeSinceStartup+50;CastlePlaySession game;
            while(!(game=Object.FindFirstObjectByType<CastlePlaySession>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(game,"PLAY reaches actual Game.unity");yield return Wait(3);
            Check(NetworkActors.Alive(game.Player)&&Services.World.Blocks.Count>0,"Actual match player and physical map remain functional");
            Check(!lobby.Player.gameObject.activeInHierarchy,"Persistent Marina player is parked during the match");
            Check(RenderSettings.skybox!=Resources.Load<Material>("MayhemMarina/MarinaSky"),"Parked Marina does not replace match sky");
            var matchWorld=Services.World;game.VisitLobby();yield return Wait(.3f);
            Check(game.VisitingLobby&&lobby.Player.gameObject.activeInHierarchy,"Match menu can visit the same Marina");
            Check(ReferenceEquals(Services.World,matchWorld),"Lobby visit preserves the running match world");
            game.ReturnToMatch();yield return null;Check(!game.VisitingLobby,"Return to match restores match control");
            MatchRuntime.Current.ReturnToLobby();until=Time.realtimeSinceStartup+35;
            while(!ReferenceEquals(Services.Session,lobby)&&Time.realtimeSinceStartup<until)yield return null;
            Check(ReferenceEquals(Services.Session,lobby)&&lobby.GetComponent<MayhemMarina>()==marina,"Round return reuses the original Marina");
            Check(lobby.Player.gameObject.activeInHierarchy&&marina.Built,"Returned Marina remains usable");
            Check(Vector3.Distance(lobby.Player.transform.position,lobby.SafeSpawnPoint())<2,"Returned player is at the supported Marina spawn");
        }
        static IEnumerator Walk(IslandLobby lobby,Vector3[] points,string label)
        {
            var motor=lobby.Survivor();var driver=new MarinaWalkDriver();motor.externallyDriven=true;motor.InputDriver=driver;motor.InputSuspended=false;
            motor.Place(lobby.transform.TransformPoint(points[0]),Quaternion.identity);
            for(int i=1;i<points.Length;i++)
            {
                driver.target=lobby.transform.TransformPoint(points[i]);float until=Time.realtimeSinceStartup+12;
                while(Vector3.ProjectOnPlane(driver.target-motor.transform.position,Vector3.up).magnitude>.45f&&Time.realtimeSinceStartup<until)yield return null;
                File.AppendAllText(Report,$"WALK {label} at={motor.transform.position} target={driver.target} status={motor.ActionStatus}\n");
                Check(Vector3.ProjectOnPlane(driver.target-motor.transform.position,Vector3.up).magnitude<.6f,label+" waypoint "+i+" reached by actual survivor motor");
                Check(motor.transform.position.y>lobby.transform.position.y+.8f,label+" remains supported");
            }
            motor.InputDriver=null;motor.externallyDriven=false;motor.InputSuspended=true;
        }
        static void StagePeople()
        {
            var prefab=Resources.Load<CandyPlayerRig>("CandyPlayerVisual");Check(prefab,"Existing real character rig available");
            Vector3[] feet={new(-11,1.35f,-8),new(-8.2f,1.35f,-6.3f),new(-13,1.54f,8.2f),new(-3.5f,1.88f,6.5f),new(3.5f,1.88f,6.4f),new(13.3f,1.32f,4),new(-13,4.16f,16),new(17,.39f,-8)};
            for(int i=0;i<feet.Length;i++)
            {
                var rig=Object.Instantiate(prefab);rig.name="Staged real avatar rig "+i;rig.SetColor(i%rig.style.colors.Length);rig.ownerCamera=null;
                var q=Quaternion.Euler(0,i*53,0);feet[i]+=captureOrigin;var frame=new CandyAvatarFrame{feet=feet[i],head=feet[i]+Vector3.up*1.6f,headRotation=q,scale=1};
                frame.left=new CandyHandPose{position=feet[i]+q*new Vector3(-.36f,1.06f,.14f),rotation=q,skeletonTracked=true,skeleton=CandyHandSkeleton.Rest(true,.25f)};
                frame.right=new CandyHandPose{position=feet[i]+q*new Vector3(.37f,i%3==0?1.65f:1.12f,.23f),rotation=q*Quaternion.Euler(-35,0,-15),skeletonTracked=true,skeleton=CandyHandSkeleton.Rest(false,.15f)};rig.ApplyFrame(frame,true,0);
            }
        }
        static void Capture(Vector3 eye,Vector3 target,float fieldOfView,string filename)
        {
            eye+=captureOrigin;target+=captureOrigin;
            var go=new GameObject("Marina actual Unity capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=fieldOfView;camera.nearClipPlane=.08f;camera.farClipPlane=600;camera.allowHDR=true;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
            var rt=new RenderTexture(2560,1440,24);camera.targetTexture=rt;var previous=RenderTexture.active;
            try{camera.Render();RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(filename),image.EncodeToPNG());Object.Destroy(image);File.AppendAllText(Report,"CAPTURE "+filename+" 2560x1440 actual Unity\n");}
            finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(go);}
        }
    }
    public sealed class MarinaWalkDriver : DeadlyJenga.CastleBots.ICastleSurvivorDriver
    {
        public Vector3 target;
        public DeadlyJenga.CastleBots.CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)
        {var delta=Vector3.ProjectOnPlane(target-motor.transform.position,Vector3.up);return new DeadlyJenga.CastleBots.CastleBotCommand{move=delta.magnitude>.3f?delta.normalized:Vector3.zero,look=delta.normalized};}
    }
    public sealed class MayhemMarinaChecksDriver : MonoBehaviour { }
}
