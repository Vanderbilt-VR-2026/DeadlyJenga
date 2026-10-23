using System;
using FlyingHand;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class QueuedFeatureChecks
    {
        const string Key="QueuedFeatureChecks";
        [Serializable] sealed class Scenes {public SceneSetup[] scenes;}
        static int checks,errors;
        static string Report=>ProjectPath.Report("QueuedFeatures.txt");
        static QueuedFeatureChecks()
        {
            EditorApplication.update+=()=>{
                var request=ProjectPath.Report("QueuedFeatures.request");
                if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                SessionState.SetBool(Key+"CastleOnly",File.ReadAllText(request).Trim()=="castle");File.Delete(request);Run();
            };
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){checks=errors=0;Application.logMessageReceived+=Log;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();EditorApplication.delayCall+=()=>new GameObject("Queued feature checks").AddComponent<QueuedFeatureDriver>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode){Application.logMessageReceived-=Log;AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();SessionState.SetBool(Key,false);SessionState.SetBool(Key+"CastleOnly",false);EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).scenes);SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);File.AppendAllText(Report,"PASS Original scenes and choices restored\n");}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Queued Features")]
        public static void Run()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){File.WriteAllText(ProjectPath.Report("QueuedFeatures.request"),SessionState.GetBool(Key+"CastleOnly",false)?"castle":"run");return;}
            var scenes=EditorSceneManager.GetSceneManagerSetup();if(scenes.Any(s=>s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new Exception("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";
            File.WriteAllText(Report,"Actual Unity Game, isolated PhysX castle, avatar and synthetic XR regression checks. No physical headset.\n");EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Check(bool pass,string text){File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+text+"\n");if(!pass)throw new Exception(text);checks++;}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Checks());
            while(stack.Count>0){bool more=false;object value=null;Exception error=null;try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}if(error!=null){File.AppendAllText(Report,"FAIL "+error+"\n");break;}if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;}
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();File.AppendAllText(Report,"COMPLETE checks="+checks+" runtimeErrors="+errors+"\n");EditorApplication.ExitPlaymode();
        }
        static IEnumerator Checks()
        {
            float end=Time.realtimeSinceStartup+40;while(!(Services.Session is CastlePlaySession)&&Time.realtimeSinceStartup<end)yield return null;
            var session=Services.Session as CastlePlaySession;Check(session&&Services.World!=null,"Game initializes");session.SurvivorBots.AutoFill=false;if(session.Social!=null)session.Social.VoiceEnabled=false;
            var player=session.Survivor();player.InputSuspended=true;var rules=session.Modes.Rules.Copy();rules.countdownSeconds=0;rules.automaticDisasters=false;rules.fallDamage=false;rules.island=IslandRule.Walkable;session.Modes.Configure(rules);session.Modes.StartRound();yield return new WaitForSeconds(.2f);
            ModeCriteriaChecks.Run();Check(File.ReadAllText(ProjectPath.Report("ModeCriteria.txt")).StartsWith("PASS "),"All mode criteria model regressions pass");
            Check(IslandLobbyCatalog.Runtime(Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog")).maps.Any(m=>m.id=="mode-castle-gaps"),"Long castle is independently selectable in the runtime map catalog");
            var previous=Services.World;var prefab=Resources.Load<GameObject>("ModeMaps/Mode_castle-gaps");Check(prefab,"Long castle prefab loads by Resources name");
            var fallback=Object.Instantiate(prefab.GetComponent<CastleGenerator>().blockPrefab);fallback.transform.position=Vector3.one*1000;fallback.GetComponent<CastleBlock>().appearance=null;
            Check(fallback.GetComponent<Rigidbody>()&&fallback.GetComponent<BoxCollider>()&&fallback.GetComponentsInChildren<MeshRenderer>().Length>0,"Copied block retains World physics and a visible gray-box fallback without Presentation art");Object.Destroy(fallback);
            var oldScene=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("Long castle isolated gravity QA",new CreateSceneParameters(LocalPhysicsMode.Physics3D));GameObject root=null;
            try
            {
                SceneManager.SetActiveScene(scene);root=Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(root,scene);var map=root.GetComponent<CastleGenerator>();map.Rebuild();var bodies=map.Bodies.ToArray();var original=CastleLayout.Build(new CastleSettings{wallCourses=14});
                Check(map.settings.blockSize==new Vector3(9,2,2)&&map.settings.moduleGap==3f,"Blocks are exactly 50 percent longer with three-metre outer tower gaps");
                Check(bodies.Length==original.Count,"Outer ring topology is preserved with raised wall landings");
                Check(map.foundation.localScale==new Vector3(61,1,61)&&map.PlayableBounds.size.x>=61,"Foundation and disaster bounds expand with the layout");
                var positions=bodies.Select(b=>b.position).ToArray();var rotations=bodies.Select(b=>b.rotation).ToArray();var physics=scene.GetPhysicsScene();Physics.SyncTransforms();for(int i=0;i<750;i++)physics.Simulate(.02f);
                float drift=bodies.Select((b,i)=>Vector3.Distance(b.position,positions[i])).Max();float tilt=bodies.Select((b,i)=>Quaternion.Angle(b.rotation,rotations[i])).Max();
                File.AppendAllText(Report,"MEASURE long castle 15s dynamic gravity drift="+drift+"m tilt="+tilt+"deg\n");Check(drift<.7f&&tilt<4&&bodies.All(b=>b.position.y>0),"Long castle survives 15 seconds of real isolated dynamic physics");
                float velocity=Mathf.Sqrt(2*player.gravity*player.EffectiveJumpHeight);float reach=player.vrWalkSpeed*(velocity+Mathf.Sqrt(velocity*velocity+2*player.gravity*10))/player.gravity;
                Check(map.settings.moduleGap+player.Capsule.radius*2<reach,"Outer gap fits default VR jump reach within five courses, including body clearance");
                float walkingReach=player.vrWalkSpeed*Mathf.Sqrt(20/player.gravity)+player.Capsule.radius*2;
                Check(map.settings.moduleGap>walkingReach,"Walking off cannot bridge the outer gap within five courses even with capsule overlap");
                File.AppendAllText(Report,"MEASURE outer gap="+map.settings.moduleGap+"m, analytical VR jump reach at 10m descent="+reach+"m, walking overlap bound="+walkingReach+"m.\n");
            }
            finally{SceneManager.SetActiveScene(oldScene);if(root)Object.Destroy(root);SceneManager.UnloadSceneAsync(scene);Services.World=previous;}
            MatchSetup.MapId="mode-castle-gaps";MatchSetup.ApplyMap();yield return new WaitForSeconds(.3f);
            var liveMap=Services.World as CastleGenerator;Check(liveMap&&liveMap.settings.blockSize.x==9,"Actual Game map-switch path loads the independent long castle");
            var environment=Object.FindFirstObjectByType<DeadlyJenga.CandyArt.CandyEnvironment>();environment.Refresh();
            Check(environment.foundation&&environment.foundation.IsChildOf(liveMap.transform),"Island environment rebinds to the expanded foundation");
            var sand=environment.sandRenderer.GetComponent<DeadlyJenga.CandyArt.CandySandbar>();Check(sand&&sand.HalfSize.x>=liveMap.FootprintHalfSize,"Island sand coverage expands to the copied castle footprint");
            player.Place(liveMap.SafeSpawnPoint(),Quaternion.identity);Capture(liveMap.Center+new Vector3(65,65,78),liveMap.Center+Vector3.up*20,"Castle-Long-Blocks-and-Gaps.png");
            CheckGapMotor(player,liveMap.settings);
            if(SessionState.GetBool(Key+"CastleOnly",false))
            {
                Check(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Disaster setup validator executes for widened castle");
                Check(errors==0,"No unexpected runtime errors");yield break;
            }
            var avatar=player.GetComponent<CandyPlayerAvatar>();var saved=avatar.Appearance;int colliderCount=player.GetComponentsInChildren<Collider>(true).Length;
            try
            {
                foreach(AvatarSkin skin in Enum.GetValues(typeof(AvatarSkin)))
                {
                    var appearance=new AvatarAppearance{skin=skin,hair=AvatarHair.CrewCut,eyewear=AvatarEyewear.Aviator,clothing=AvatarClothing.BowTie};avatar.ApplyAppearance(appearance);yield return null;
                    Check(avatar.Appearance.skin==skin&&avatar.Rig.GetComponent<CandyAvatarWardrobe>(),"Skin "+skin+" applies to existing rig");Check(player.GetComponentsInChildren<Collider>(true).Length==colliderCount,"Skin "+skin+" preserves collision");
                    Check(avatar.Rig.BoneCount==44,"Skin "+skin+" preserves the shared 44-bone avatar");Capture(player.transform.position+avatar.LastFrame.headRotation*new Vector3(1.3f,.8f,1.7f),player.transform.position+Vector3.up*.43f,"Skin-"+skin+".png");
                    using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))CharacterWire.WriteSurvivor(writer,avatar.LastFrame,0,appearance);stream.Position=0;using(var reader=new BinaryReader(stream)) {CharacterWire.ReadSurvivor(reader,out _,out _,out var decoded,out _);Check(decoded.Equals(appearance),"Skin "+skin+" and style survive complete actor wire roundtrip");}}
                }
                avatar.ApplyAppearance(new AvatarAppearance{poweredArmor=true});yield return null;var armor=avatar.Rig.GetComponent<CandyAvatarWardrobe>();Check(armor.LeftPalmEmitter&&armor.RightPalmEmitter&&armor.LeftSoleEmitter&&armor.RightSoleEmitter,"Armor has palm and sole emitter attachment points");Check(player.GetComponentsInChildren<Collider>(true).Length==colliderCount,"Armor remains a cosmetic overlay");
                Capture(player.transform.position+avatar.LastFrame.headRotation*new Vector3(1.3f,.8f,1.7f),player.transform.position+Vector3.up*.43f,"Skin-Powered-Armor.png");
            }
            finally{avatar.ApplyAppearance(saved);}
            var tracking=player.GetComponent<CandyTrackingSource>();bool wasEnabled=tracking.enabled;tracking.enabled=false;Check(!tracking.WorldHand(true).tracked&&!tracking.WorldHand(false).tracked&&!tracking.RightPinching&&!tracking.LeftPinching,"Disabled tracking cannot expose cached poses or gestures");tracking.enabled=wasEnabled;
            session.SetMenu(false);player.InputSuspended=false;var camera=player.ViewCamera;bool cameraEnabled=camera.enabled;
            var inspection=CandyThirdPersonInspection.For(session);inspection.Begin();yield return null;
            Check(inspection.Inspecting&&player.InputSuspended&&inspection.InspectionCamera,"Demo inspection suspends movement and creates a separate orbit camera");
            Check(!session.IsVR&&!camera.enabled&&!inspection.InspectionCamera.GetUniversalAdditionalCameraData().allowXRRendering,"Desktop inspection renders independently of the first-person camera");inspection.End();yield return null;
            Check(camera.enabled==cameraEnabled&&!player.InputSuspended,"Leaving inspection restores camera and movement");player.InputSuspended=true;
            yield return MeltControlChecks.Play(session,Check,(melt,name)=>Capture(melt.CurrentBodyBounds.center+new Vector3(1.2f,.75f,1.6f),melt.CurrentBodyBounds.center,name+".png"));
            foreach(string guid in AssetDatabase.FindAssets("t:Shader",new[]{ProjectPath.FolderOf("CandySpirit.shader").TrimEnd('/')})){var shader=AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));Check(shader&&!ShaderUtil.ShaderHasError(shader),"Runtime scenery shader compiles: "+shader.name);}
            Check(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Disaster setup validator executes against expanded map support");
            yield return DeadlyJenga.CastlePrototype.Editor.CastleInputValidation.Validate(session,true);
            var input=File.ReadAllText(ProjectPath.Report("InputValidation.txt"));Check(!input.Contains("FAIL "),"Real player loop synthetic controller movement, jump, menu and role regressions pass");Check(errors==0,"No unexpected runtime errors");
        }
        static void CheckGapMotor(CastleSurvivor player,CastleSettings settings)
        {
            // Static full block colliders isolate movement from tower wobble. The live map is tested separately.
            var origin=new Vector3(2000,100,2000);var obstacles=new List<GameObject>();
            bool enabled=player.enabled;var mode=player.controlMode;var position=player.transform.position;var rotation=player.transform.rotation;
            try
            {
                player.enabled=false;player.controlMode=ControlMode.VR;player.RefreshMode();player.viewCamera.transform.localRotation=Quaternion.identity;
                GameObject Block(Vector3 center,Vector3 size)
                {var go=new GameObject("Gap traversal block");go.layer=Layers.Surface;go.transform.position=origin+center;go.transform.localScale=size;go.AddComponent<BoxCollider>();obstacles.Add(go);return go;}
                var takeoff=Block(new Vector3(0,-1,-settings.blockSize.x*.5f),new Vector3(2,2,settings.blockSize.x));
                var landing=Block(new Vector3(0,-5,settings.moduleGap+settings.blockSize.x*.5f),new Vector3(2,2,settings.blockSize.x));
                int firstJumpDrop=0;
                foreach(float speed in new[]{player.vrWalkSpeed,player.walkSpeed})
                {
                    player.controlMode=speed==player.vrWalkSpeed?ControlMode.VR:ControlMode.Desktop;player.RefreshMode();
                    for(int courses=1;courses<=5;courses++)
                    {
                        float drop=courses*settings.blockSize.y;landing.transform.position=origin+new Vector3(0,-drop-1,settings.moduleGap+settings.blockSize.x*.5f);Physics.SyncTransforms();
                        foreach(bool jump in new[]{false,true})
                        {
                            player.Teleport(origin+new Vector3(0,.02f,-.1f),Quaternion.identity);
                            for(int i=0;i<12;i++)player.Simulate(Vector2.zero,false,1f/120);
                            Check(player.Grounded,"Traversal takeoff has physical foot support");
                            bool landed=false;Vector3 end=default;
                            for(int i=0;i<360;i++)
                            {
                                player.Simulate(Vector2.up,jump&&i==0,1f/120);end=player.FootPosition-origin;
                                if(player.Grounded&&player.StandingSurface&&player.StandingSurface.gameObject==landing){landed=true;break;}
                                if(end.y < -drop-2)break;
                            }
                            File.AppendAllText(Report,"MEASURE motor "+speed+"m/s "+(jump?"jump":"walk")+" drop="+courses+" courses landed="+landed+" feet="+end+"\n");
                            if(speed==player.vrWalkSpeed&&!jump)Check(!landed,"Default VR walking cannot cross at "+courses+" course drop");
                            if(jump&&landed&&speed==player.vrWalkSpeed&&firstJumpDrop==0)firstJumpDrop=courses;
                            if(jump&&courses==5)Check(landed,"Real player motor crosses at "+speed+"m/s within five courses");
                        }
                    }
                }
                Check(firstJumpDrop>=1&&firstJumpDrop<=5,"Default VR jump reaches the next tower with a one-to-five-course drop (first success "+firstJumpDrop+")");
            }
            finally
            {
                foreach(var go in obstacles)Object.DestroyImmediate(go);player.controlMode=mode;player.RefreshMode();player.Teleport(position,rotation);player.enabled=enabled;
            }
        }
        static void Capture(Vector3 position,Vector3 target,string name)
        {
            var go=new GameObject("Feature capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;camera.nearClipPlane=.03f;camera.fieldOfView=45;camera.farClipPlane=200;camera.cullingMask=~(1<<5);camera.transform.position=position;camera.transform.LookAt(target);
            var texture=new RenderTexture(960,720,24);var previous=RenderTexture.active;camera.targetTexture=texture;
            var batches=Resources.FindObjectsOfTypeAll<DeadlyJenga.CandyArt.CandyCrateBatchRenderer>().Where(b=>b.gameObject.scene.IsValid()&&b.isActiveAndEnabled).ToArray();foreach(var batch in batches)batch.enabled=false;
            try{camera.Render();RenderTexture.active=texture;var pixels=new Texture2D(960,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,720),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());Object.Destroy(pixels);}
            finally{foreach(var batch in batches)if(batch)batch.enabled=true;RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(texture);Object.Destroy(go);}
        }
    }
    public sealed class QueuedFeatureDriver:MonoBehaviour { }
}
