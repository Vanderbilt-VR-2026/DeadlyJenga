using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CharacterNetwork;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using LogType=UnityEngine.LogType;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class AvatarEnemyChecks
    {
        const string Key="AvatarEnemyChecks",Pref="CandyAvatar.Accessories.v1";
        [Serializable] sealed class Scenes{public SceneSetup[] scenes;}
        static int checks,errors,enemyHits;static float lastEnemyDamage;static bool starting;
        static string Report=>ProjectPath.Report("AvatarEnemies.txt");
        static AvatarEnemyChecks()
        {
            EditorApplication.update+=()=>
            {
                var request=ProjectPath.Report("AvatarEnemies.request");
                if(starting||!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                try{if(File.ReadAllText(request).Trim()=="equipment")SessionState.SetBool(Key+"EquipmentOnly",true);File.Delete(request);}catch(IOException){return;}Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    checks=errors=enemyHits=0;Application.logMessageReceived+=Log;GameEvents.Damaged+=Damaged;
                    AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);
                    EditorApplication.delayCall+=()=>{var go=new GameObject("Avatar and enemy integration checks");Object.DontDestroyOnLoad(go);go.AddComponent<AvatarEnemyCheckDriver>().StartCoroutine(Guard());};
                }
                if(state!=PlayModeStateChange.EnteredEditMode)return;
                Application.logMessageReceived-=Log;GameEvents.Damaged-=Damaged;SessionState.SetBool(Key,false);SessionState.SetBool(Key+"EquipmentOnly",false);
                if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
                if(SessionState.GetBool(Key+"HadPref",false))PlayerPrefs.SetInt(Pref,SessionState.GetInt(Key+"Pref",0));else PlayerPrefs.DeleteKey(Pref);PlayerPrefs.Save();
                if(SessionState.GetBool(Key+"HadHand",false))PlayerPrefs.SetInt("CandyBlaster.LeftHanded",SessionState.GetInt(Key+"Hand",0));else PlayerPrefs.DeleteKey("CandyBlaster.LeftHanded");PlayerPrefs.Save();
                EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).scenes);
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);Time.timeScale=SessionState.GetFloat(Key+"Scale",1);
                File.AppendAllText(Report,"PASS Original scenes, match choices, appearance preference and timing restored.\n");
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Avatar Attachments and Panther Enemies")]
        public static void Run()
        {
            if(starting)return;starting=true;
            try{StartChecks();}finally{starting=false;}
        }
        static void StartChecks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SpectatorVideoCapture.Busy)throw new InvalidOperationException("Stop Play Mode and recordings first.");
            var request=ProjectPath.Report("AvatarEnemies.request");File.WriteAllText(request,"run");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;File.Delete(request);
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>string.IsNullOrEmpty(s.path))||Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            File.WriteAllText(Report,"Actual Lobby wardrobe, Lobby -> Game -> Lobby persistence, actual panther motor/combat and remote-view wire fixtures. No headset evidence.\n");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"HadPref",PlayerPrefs.HasKey(Pref));SessionState.SetInt(Key+"Pref",PlayerPrefs.GetInt(Pref,0));
            SessionState.SetBool(Key+"HadHand",PlayerPrefs.HasKey("CandyBlaster.LeftHanded"));SessionState.SetInt(Key+"Hand",PlayerPrefs.GetInt("CandyBlaster.LeftHanded",0));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetFloat(Key+"Scale",Time.timeScale);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.BotTarget=1;MatchSetup.HandCount=0;Time.timeScale=1;
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();
        }
        static void Check(bool value,string label){File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);checks++;}
        static void Log(string message,string trace,LogType type)
        {
            if(NetworkProjectConfig.Global.PeerMode==NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.")){File.AppendAllText(Report,"INFO Known Fusion editor multi-peer scene diagnostic (same exception as the existing networking suite).\n");return;}
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}
        }
        static void Damaged(GameObject who,float amount){if(who==Services.Session?.Player?.gameObject){enemyHits++;lastEnemyDamage=amount;}}
        static IEnumerator Until(Func<bool> ready,string label,float seconds=35)
        {float end=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Check(ready(),label);}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(CheckScene());
            while(stack.Count>0)
            {
                bool more=false;object value=null;Exception error=null;try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            File.AppendAllText(Report,"COMPLETE checks="+checks+" runtimeErrors="+errors+"\n");EditorApplication.ExitPlaymode();
        }
        static object Field(object owner,string name)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        static Texture2D Read(RenderTexture texture)
        {
            var previous=RenderTexture.active;RenderTexture.active=texture;
            try{var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();return pixels;}finally{RenderTexture.active=previous;}
        }
        static IEnumerator CheckScene()
        {
            yield return Until(()=>IslandLobby.Current&&Services.Session?.Player!=null,"Actual lobby initializes");
            var lobby=IslandLobby.Current;var view=lobby.View();var avatar=lobby.Player.gameObject.GetComponent<CandyPlayerAvatar>();
            if(SessionState.GetBool(Key+"EquipmentOnly",false))
            {
                var roomOnly=lobby.Room;roomOnly.SelectMap(Array.FindIndex(roomOnly.Catalog.maps,m=>m.id=="mode-christmas-town"));roomOnly.SelectMode(Array.FindIndex(roomOnly.Catalog.modes,m=>m.id=="demo"));
                var setupOnly=roomOnly.Setup;setupOnly.population=1;setupOnly.hands=0;setupOnly.rules.automaticDisasters=false;setupOnly.rules.countdownSeconds=0;roomOnly.ConfigureSetup(setupOnly);roomOnly.Launch();
                yield return Until(()=>Services.Session is CastlePlaySession s&&s.Modes&&s.SurvivorBots!=null,"Actual equipment test Game starts");
                var equipmentSession=(CastlePlaySession)Services.Session;equipmentSession.SetMenu(false);if(equipmentSession.Social!=null)equipmentSession.Social.VoiceEnabled=false;
                yield return EquipmentChecks.Run(equipmentSession);
                equipmentSession.Modes.ReturnToLobby();yield return Until(()=>IslandLobby.Current&&Services.Session is IslandLobby,"Equipment test returns to lobby");
                yield return CheckNetwork(IslandLobby.Current,Services.Session.Player.gameObject.GetComponent<CandyPlayerAvatar>().Appearance.accessories);
                Check(errors==0,"No unexpected equipment runtime errors");yield break;
            }
            Check(avatar&&avatar.Rig,"Local lobby player exposes the existing candy avatar");
            var rig=avatar.Rig;var bodyMesh=rig.bodyRenderer.sharedMesh;var headMesh=rig.headRenderer.sharedMesh;int colliders=avatar.GetComponentsInChildren<Collider>(true).Length;
            bool paused=lobby.Player.InputSuspended;var mainCamera=lobby.Player.ViewCamera;
            lobby.Player.Place(view.WardrobePosition,Quaternion.identity);yield return null;
            yield return Until(()=>view.WardrobeOpen,"Walking into the pod opens its third-person preview");
            Check(lobby.Player.InputSuspended&&lobby.Player.ViewCamera==mainCamera,"Wardrobe pauses locomotion and preserves the tracked/player camera");
            var target=(RenderTexture)Field(view,"wardrobeTexture");var sheet=new Texture2D(768*4,768*3,TextureFormat.RGB24,false);
            for(int i=0;i<12;i++)
            {
                avatar.ApplyAppearance(default);Check(view.ToggleAccessory(i),"Wardrobe toggles "+(AvatarAccessory)(1u<<i));yield return null;
                Check(avatar.Appearance.accessories==(AvatarAccessory)(1u<<i),"Selection contains only the requested attachment");
                Check(rig.bodyRenderer.sharedMesh==bodyMesh&&rig.headRenderer.sharedMesh==headMesh&&rig.BoneCount==44,"Attachment preserves the original body, head and skeleton");
                Check(avatar.GetComponentsInChildren<Collider>(true).Length==colliders,"Attachment adds no colliders");
                var renderers=rig.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Attachment - ")).ToArray();
                Check(renderers.Length>0&&renderers.All(r=>r.sharedMaterial==rig.bodyRenderer.sharedMaterial),"Attachment uses the existing body material");
                Check(renderers.All(r=>r.GetComponent<MeshFilter>().sharedMesh.vertexCount>0),"Attachment geometry is nonempty");
                foreach(var r in renderers){var mesh=r.GetComponent<MeshFilter>().sharedMesh;Check(ReferenceEquals(mesh,CandyAccessoryMeshes.Get((AvatarAccessory)(1u<<i))),"Attachment mesh is shared");}
                var copy=Read(target);sheet.SetPixels((i%4)*768,(2-i/4)*768,768,768,copy.GetPixels());Object.Destroy(copy);
            }
            sheet.Apply();File.WriteAllBytes(ProjectPath.Report("Avatar-Attachments.png"),sheet.EncodeToPNG());Object.Destroy(sheet);
            avatar.ApplyAppearance(new AvatarAppearance{accessories=(AvatarAccessory)4095});yield return null;
            var photo=Read(target);File.WriteAllBytes(ProjectPath.Report("Avatar-Combined.png"),photo.EncodeToPNG());Object.Destroy(photo);
            CaptureWardrobe(view,1280,800);CaptureWardrobe(view,1024,768);CaptureWardrobe(view,2560,1080);
            var persisted=AvatarAccessory.Mohawk|AvatarAccessory.Glasses|AvatarAccessory.Cape;avatar.ApplyAppearance(new AvatarAppearance{accessories=persisted});
            view.SetWardrobeOpen(false);Check(lobby.Player.InputSuspended==paused,"Leaving wardrobe restores locomotion suspension state");
            Check(PlayerPrefs.GetInt(Pref,0)==(int)persisted,"Selection is persisted for scene changes and future runs");
            var room=lobby.Room;room.SelectMap(Array.FindIndex(room.Catalog.maps,m=>m.id=="mode-christmas-town"));room.SelectMode(Array.FindIndex(room.Catalog.modes,m=>m.id=="demo"));
            var setup=room.Setup;setup.population=1;setup.hands=0;setup.rules.automaticDisasters=false;setup.rules.countdownSeconds=0;room.ConfigureSetup(setup);room.Launch();
            yield return Until(()=>Services.Session is CastlePlaySession s&&s.Modes&&s.SurvivorBots!=null,"Actual Game scene starts from the lobby");
            var session=(CastlePlaySession)Services.Session;session.SetMenu(false);if(session.Social!=null)session.Social.VoiceEnabled=false;
            var player=session.Survivor();player.InputSuspended=true;avatar=player.GetComponent<CandyPlayerAvatar>();
            Check(avatar.Appearance.accessories==persisted,"Existing avatar retains selected attachments in Game");
            var enemies=(IEnemyPopulation)session.SurvivorBots;int ordinary=session.SurvivorBots.Count;
            Check(!enemies.SpawnEnemy(new EnemySpawnSettings{archetypeId="missing"})&&!enemies.SpawnEnemy(new EnemySpawnSettings{hitDamage=float.NaN}),"Enemy factory rejects unsupported and invalid requests");
            var enemyObject=enemies.SpawnEnemy(new EnemySpawnSettings());Check(enemyObject,"Panther spawns on actual Candy Town geometry");
            var enemy=enemyObject.GetComponent<CastleEnemyActor>();var brain=enemyObject.GetComponent<CastleBotBrain>();
            yield return new WaitForSeconds(.7f);
            Check(enemies.EnemyCount==1&&session.SurvivorBots.Count==ordinary,"Enemies are counted separately from ordinary bots");
            Check(!session.Modes.Frame.players.Any(p=>p.id==brain.Motor.ActorId),"Enemy does not enter player rosters or scoreboards");
            Check(brain.Motor.HasWalkableIsland&&session.Modes.RulesFor(enemyObject).punchDamage==20,"Panther uses walkable infected movement and lower damage");
            Check(enemyObject.GetComponent<CandyPlayerAvatar>().Appearance.accessories==(AvatarAccessory.PantherHead|AvatarAccessory.PantherClaws|AvatarAccessory.PantherTail),"Panther uses head, claw and tail attachments on the same candy rig");
            CapturePanther(enemyObject.transform,brain.Motor.playerScale);
            var bounds=Services.World.PlayableBounds;var start=enemyObject.transform.position;var offset=start-bounds.center;
            float radial=Mathf.Sqrt(Mathf.Pow(offset.x/bounds.extents.x,2)+Mathf.Pow(offset.z/bounds.extents.z,2));Check(radial>=.68f,"Initial enemy spawn is on a perimeter ring");
            var inward=Vector3.ProjectOnPlane(Services.World.Center-start,Vector3.up).normalized;
            var destination=start+inward*3;
            if(Physics.Raycast(destination+Vector3.up*8,Vector3.down,out var floor,20,(1<<Layers.Surface)|(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore))destination=floor.point+Vector3.up*.04f;
            player.Place(destination,Quaternion.LookRotation(-inward));player.InputSuspended=true;
            enemyHits=0;yield return Until(()=>enemyHits>0,"Panther pursues and lands a real ordinary-motor punch",30);
            Check(lastEnemyDamage==20&&player.Health>0,"Actual enemy hit deals 20 damage without instantly killing a full-health player");
            Check(brain.TravelDistance>.2f&&brain.PunchesAccepted>0,"Enemy used the shared bot navigation and combat controller");
            Wire(enemyObject.GetComponent<CandyPlayerAvatar>(),player.gameObject);
            brain.enabled=false;brain.Motor.InputSuspended=true;
            enemyObject.GetComponent<IDamageable>().Damage(1000,player.gameObject);
            yield return Until(()=>!enemyObject,"Enemy death despawns the NPC instead of turning it into a player",5);
            yield return new WaitForSeconds(.3f);Check(enemies.EnemyCount==0,"Enemy population releases the dead actor");
            var normal=session.SurvivorBots.SpawnOne();Check(normal,"Ordinary bot still spawns");
            Check(normal.GetComponent<CandyPlayerAvatar>().Appearance.accessories==AvatarAccessory.None,"Ordinary bots do not inherit the local player's saved accessories");
            Check(enemies.SpawnEnemy(new EnemySpawnSettings()),"A second enemy can be spawned");enemies.ClearEnemies();yield return null;
            Check(normal&&enemies.EnemyCount==0,"Clearing enemies preserves ordinary bots");
            yield return CheckBlaster(session,normal);
            yield return EquipmentChecks.Run(session);
            session.Modes.ReturnToLobby();yield return Until(()=>IslandLobby.Current&&Services.Session is IslandLobby,"Game returns to the lobby");
            Check(Services.Session.Player.gameObject.GetComponent<CandyPlayerAvatar>().Appearance.accessories==persisted,"Appearance persists after returning to lobby");
            yield return CheckNetwork(IslandLobby.Current,persisted);
            Check(errors==0,"No unexpected runtime errors");
        }
        static void Wire(CandyPlayerAvatar avatar,GameObject local)
        {
            byte[] bytes;using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream))avatar.WriteState(writer);bytes=stream.ToArray();}
            using(var reader=new BinaryReader(new MemoryStream(bytes)))
            {CharacterWire.ReadSurvivor(reader,out _,out _,out var appearance,out var enemy);Check(appearance.accessories==avatar.Appearance.accessories&&enemy?.hitDamage==20,"Wire preserves attachments and enemy metadata");}
            var root=new GameObject("Remote enemy fixture");var view=root.AddComponent<SurvivorNetworkView>().Init(new NetworkContext{localSurvivor=local,actorId="enemy-fixture"});
            using(var reader=new BinaryReader(new MemoryStream(bytes)))view.ReadState(reader,true);view.Render(.1f);
            Check(view.IsEnemy&&view.HitDamage==20&&root.GetComponentInChildren<CandyAvatarAttachments>().Appearance.accessories==avatar.Appearance.accessories,"Remote view reconstructs enemy metadata and the same attachments");Object.Destroy(root);
            var hostRoot=new GameObject("Human metadata validation fixture");var hostView=hostRoot.AddComponent<SurvivorNetworkView>().Init(new NetworkContext{host=true,localSurvivor=local,actorId="human-fixture"});
            using(var reader=new BinaryReader(new MemoryStream(bytes)))hostView.ReadState(reader,true);
            Check(!hostView.IsEnemy,"Connected humans cannot acquire NPC rules through appearance metadata");Object.Destroy(hostRoot);
            using(var stream=new MemoryStream())
            {using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))CharacterWire.WriteSurvivor(writer,avatar.LastFrame,0);stream.Position=0;using(var reader=new BinaryReader(stream)){CharacterWire.ReadSurvivor(reader,out _,out _,out var appearance,out var enemy);Check(appearance.accessories==AvatarAccessory.None&&enemy==null,"Legacy survivor payload retains an empty appearance");}}
        }
        static IEnumerator CheckBlaster(CastlePlaySession session,GameObject target)
        {
            var player=session.Survivor();var gun=player.GetComponent<CandyBlaster>();Check(gun,"Local survivor exposes blaster controls");
            gun.SetTestingEnabled(true);gun.SetDrawn(true);Check(gun.TestingEnabled&&gun.Drawn,"Demo testing toggle enables and draws the weapon");
            gun.SetLeftHanded(true);Check(((UnityEngine.InputSystem.InputAction)Field(player.GetComponent<CandyEquipment>(),"trigger")).bindings[0].path.Contains("LeftHand")&&((UnityEngine.InputSystem.InputAction)Field(player.GetComponent<CandyEquipment>(),"offTrigger")).bindings[0].path.Contains("RightHand"),"Left-handed option swaps fire and jump triggers");gun.SetLeftHanded(false);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Blaster fixture floor";floor.layer=Layers.Surface;floor.transform.position=new Vector3(110,9,100);floor.transform.localScale=new Vector3(30,2,40);
            var motor=target.GetComponent<CastleSurvivor>();target.GetComponent<CastleBotBrain>().enabled=false;
            player.Place(new Vector3(110,10.05f,100),Quaternion.identity);player.InputSuspended=true;
            motor.Place(new Vector3(110,10.05f,112),Quaternion.identity);motor.InputSuspended=true;
            var side=session.SurvivorBots.SpawnOne();Check(side,"Secondary blast target spawns");side.GetComponent<CastleBotBrain>().enabled=false;var sideMotor=side.GetComponent<CastleSurvivor>();sideMotor.Place(new Vector3(111.1f,10.05f,112),Quaternion.identity);sideMotor.InputSuspended=true;
            yield return new WaitForSeconds(.4f);Physics.SyncTransforms();
            player.viewCamera.transform.rotation=Quaternion.LookRotation(motor.Capsule.bounds.center-player.viewCamera.transform.position);
            Check(Physics.RaycastAll(player.viewCamera.transform.position,player.viewCamera.transform.forward,20,1<<Layers.Survivor,QueryTriggerInteraction.Ignore).Any(h=>h.collider==motor.Capsule),"Test reticle aims inside the actual scaled survivor collider");
            File.AppendAllText(Report,"INFO Plasma fixture scale="+motor.playerScale+" capsule="+motor.Capsule.bounds+" muzzle="+gun.MuzzlePose.position+" forward="+(gun.MuzzlePose.rotation*Vector3.forward)+"\n");
            player.InputSuspended=false;float health=motor.Health,otherHealth=sideMotor.Health;
            Check(gun.FireOnce(),"Dominant-weapon command launches actual plasma");Check(!gun.FireOnce(),"Immediate repeat shot is rejected by cooldown");
            Check(motor.Health==health,"Firing does not apply instant hit damage");yield return new WaitForSeconds(.25f);
            Check(session.Modes.Frame.blaster.shots.Length==1&&motor.Health==health,"Plasma visibly travels before reaching the target");
            yield return Until(()=>motor.Health<health,"Traveling plasma collides and damages its actual target",4);
            Check(sideMotor.Health<otherHealth&&health-motor.Health<=35,"Medium-area impact damages a nearby second actor with bounded falloff");
            gun.SetDrawn(false);Check(!gun.FireOnce(),"Holstered weapon cannot fire");gun.SetDrawn(true);
            player.InputSuspended=true;
            var model=player.transform.Find("Visual - three ring alien blaster");Check(model&&model.GetComponentsInChildren<MeshRenderer>().Length==3,"Blaster uses shared body, three-ring and vent meshes");
            Check(Mathf.Approximately(model.localScale.x,player.playerScale),"Held weapon scales with the existing avatar");
            CaptureModel(model);
            motor.Place(new Vector3(110,10.05f,112),Quaternion.identity);motor.InputSuspended=true;sideMotor.Place(new Vector3(111.1f,10.05f,112),Quaternion.identity);sideMotor.InputSuspended=true;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Plasma occlusion fixture";wall.layer=Layers.Surface;wall.transform.position=new Vector3(110,12,110.8f);wall.transform.localScale=new Vector3(8,4,.35f);Physics.SyncTransforms();
            yield return new WaitForSeconds(.7f);health=motor.Health;otherHealth=sideMotor.Health;player.InputSuspended=false;
            Check(gun.FireOnce(),"Weapon fires a second shot after recovery");yield return new WaitForSeconds(1.2f);
            Check(motor.Health==health&&sideMotor.Health==otherHealth,"Solid cover blocks plasma splash damage behind the impact");
            Check(!session.Modes.RequestBlasterShot(player.gameObject,new Pose(player.transform.position+Vector3.one*1000,Quaternion.identity)),"Authority rejects a forged remote muzzle position");
            gun.SetTestingEnabled(false);Check(!gun.TestingEnabled&&!gun.FireOnce()&&session.Modes.Frame.blaster.shots.Length==0,"Disabling testing removes projectiles and prevents shooting");
            player.InputSuspended=true;Object.Destroy(wall);Object.Destroy(floor);Object.Destroy(side);
        }
        static void CaptureModel(Transform model)
        {
            var go=new GameObject("Blaster capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=45;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.055f,.08f);
            float scale=Mathf.Abs(model.lossyScale.x);var center=model.position+model.rotation*new Vector3(0,0,.15f)*scale;camera.transform.position=center+model.rotation*new Vector3(.65f,.28f,.45f)*scale;camera.transform.LookAt(center);
            var texture=new RenderTexture(1024,768,24);camera.targetTexture=texture;camera.Render();var pixels=Read(texture);File.WriteAllBytes(ProjectPath.Report("Blaster-Three-Rings.png"),pixels.EncodeToPNG());Object.Destroy(pixels);camera.targetTexture=null;Object.Destroy(texture);Object.Destroy(go);
        }
        static void CapturePanther(Transform actor,float scale)
        {
            var go=new GameObject("Panther capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=42;camera.nearClipPlane=.01f;camera.farClipPlane=200;
            camera.transform.position=actor.position+actor.rotation*new Vector3(1.8f,1.4f,2.5f)*scale;camera.transform.LookAt(actor.position+Vector3.up*.9f*scale);
            var target=new RenderTexture(1024,1024,24);camera.targetTexture=target;camera.Render();var pixels=Read(target);File.WriteAllBytes(ProjectPath.Report("Panther-Enemy.png"),pixels.EncodeToPNG());Object.Destroy(pixels);camera.targetTexture=null;Object.Destroy(target);Object.Destroy(go);
        }
        static void CaptureWardrobe(IslandLobbyView view,int width,int height)
        {
            var root=(RectTransform)Field(view,"wardrobeDesktop");var canvas=root.GetComponent<Canvas>();var previousCamera=canvas.worldCamera;var previousMode=canvas.renderMode;float plane=canvas.planeDistance;
            var go=new GameObject("Wardrobe UI capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.01f;camera.farClipPlane=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.07f);camera.cullingMask=1<<5;
            var target=new RenderTexture(width,height,24);camera.targetTexture=target;
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.05f;Canvas.ForceUpdateCanvases();camera.Render();var pixels=Read(target);File.WriteAllBytes(ProjectPath.Report("Wardrobe-"+width+"x"+height+".png"),pixels.EncodeToPNG());Object.Destroy(pixels);
                var corners=new Vector3[4];foreach(var text in root.GetComponentsInChildren<UnityEngine.UI.Text>())
                {
                    text.rectTransform.GetWorldCorners(corners);foreach(var point in corners){var screen=camera.WorldToViewportPoint(point);Check(screen.x>=-.002f&&screen.x<=1.002f&&screen.y>=-.002f&&screen.y<=1.002f,"Wardrobe label stays on screen at "+width+"x"+height);}
                }
            }
            finally{canvas.renderMode=previousMode;canvas.worldCamera=previousCamera;canvas.planeDistance=plane;camera.targetTexture=null;Object.Destroy(target);Object.Destroy(go);Canvas.ForceUpdateCanvases();}
        }
        static IEnumerator CheckNetwork(IslandLobby lobby,AvatarAccessory expected)
        {
            var config=NetworkProjectConfig.Global;var original=config.PeerMode;NetworkRunner probe=null;var primary=lobby.Room;var expectedStyle=Services.Session.Player.gameObject.GetComponent<CandyPlayerAvatar>().Appearance;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                var host=primary.Host();yield return Until(()=>host.IsCompleted,"Photon host startup completes",45);Check(primary.IsHost,"Real Photon lobby host starts");
                var go=new GameObject("Avatar independent Photon client");probe=go.AddComponent<NetworkRunner>();var secondary=probe;var callbacks=new NetworkDelegates();bool hostAppearance=false,guestAppearance=false;
                callbacks.OnReliableDataReceived=(runner,source,key,data)=>
                {
                    key.GetInts(out int channel,out int kind,out int sequence,out _);if(channel!=0x49534C44||kind!=3)return;
                    using(var reader=P.Reader(data.ToArray(),P.World))
                    {
                        reader.ReadInt32();reader.ReadInt32();int count=P.Count(reader,10);
                        for(int i=0;i<count;i++)
                        {
                            var actor=P.ReadActor(reader);if(actor.kind!=NetworkKinds.Survivor)continue;
                            using(var state=P.StateReader(actor)){CharacterWire.ReadSurvivor(state,out _,out _,out var appearance,out _);if(appearance.accessories==expected&&CandyPlayerAvatar.StyleBits(appearance)==CandyPlayerAvatar.StyleBits(expectedStyle)){if(actor.id==secondary.LocalPlayer.ToString())guestAppearance=true;else hostAppearance=true;}}
                        }
                    }
                    secondary.SendReliableDataToServer(ReliableKey.FromInts(channel,4,sequence,0),Array.Empty<byte>());
                };
                probe.AddCallbacks(callbacks);var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion=primary.Region;app.AppVersion=(app.AppVersion??"")+"/"+lobby.catalog.NetworkVersion;
                var join=probe.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=primary.Code,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=go.AddComponent<NetworkSceneManagerDefault>()});
                yield return Until(()=>join.IsCompleted,"Independent Photon client connection completes",45);Check(join.Result.Ok,"Independent Photon client joins the appearance-compatible room");
                probe.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,1,0,0),Array.Empty<byte>());
                yield return Until(()=>hostAppearance,"Host attachments cross real Photon transport to an independent client",20);
                var guest=lobby.Capture("forged-id");probe.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,2,1,0),P.Encode(P.Player,w=>P.Write(w,guest)));
                yield return Until(()=>guestAppearance&&lobby.PeerCount==1,"Client attachments pass host authentication and replicate back through Photon",20);
                var shutdown=probe.Shutdown();yield return Until(()=>shutdown.IsCompleted,"Independent client shuts down",20);Object.Destroy(go);probe=null;
                var leave=primary.Leave();yield return Until(()=>leave.IsCompleted&&!primary.Connected,"Host room closes cleanly",20);
            }
            finally{if(probe){_ = probe.Shutdown();Object.Destroy(probe.gameObject);}if(primary.Connected)_=primary.Leave();config.PeerMode=original;}
        }
    }
    public sealed class AvatarEnemyCheckDriver:MonoBehaviour { }
}
