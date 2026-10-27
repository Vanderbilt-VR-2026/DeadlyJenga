using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class ArsenalExpansionChecks
    {
        const string Key="ArsenalExpansion";
        static int checks,errors;static bool failed,completed;
        static string Report=>ProjectPath.Report("ArsenalExpansion.txt");
        sealed class PickupSource:IEquipmentPickupSource {public EquipmentKind Kind{get;set;}public Vector3 Position{get;set;}public float Duration{get;set;}=1;}
        [Serializable] sealed class Scenes{public SceneSetup[] values;}
        static ArsenalExpansionChecks()
        {
            EditorApplication.update+=()=>
            {
                string path=ProjectPath.Report("ArsenalExpansionMain.request");
                if(!File.Exists(path)||File.ReadAllText(path).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString()+":v9"||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                if(!ValidationRunGate.TryAcquire(Key))return;
                File.Delete(path);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {checks=errors=0;failed=completed=false;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    ValidationRunGate.Release(Key);
                    if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).values);
                    failed|=!completed;File.AppendAllText(Report,$"COMPLETE checks={checks} runtimeErrors={errors} failed={failed}. Desktop runtime; headset not tested.\n");
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Arsenal Expansion")]
        public static void Run()
        {
            SessionState.SetBool(Key+"VisualOnly",false);
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(setup.Any(s=>s.isLoaded&&UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{values=setup}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            File.WriteAllText(Report,"Arsenal expansion v9 (main editor): actual Game mechanics, categorized menus, expanded behaviors and captures.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType kind)
        {
            if(Fusion.NetworkProjectConfig.Global.PeerMode==Fusion.NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.",StringComparison.Ordinal)){File.AppendAllText(Report,"TRANSPORT FIXTURE NOTE: empty scene multiple-peer diagnostic; packet test, not replicated gameplay scene.\n");return;}
            if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}
        }
        static void Check(bool pass,string label)
        {File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+label+"\n");checks++;if(!pass)throw new InvalidOperationException(label);}
        static IEnumerator Guard()
        {
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Test());
            while(stack.Count>0)
            {
                bool more=false;object value=null;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();completed=!failed;EditorApplication.ExitPlaymode();
        }
        static IEnumerator Test()
        {
            var session=Object.FindFirstObjectByType<CastlePlaySession>();float until=Time.realtimeSinceStartup+30;
            while((session.Player==null||session.Modes==null)&&Time.realtimeSinceStartup<until)yield return null;
            Check(session.Player!=null&&session.Modes!=null,"Actual Game initializes");session.SetRole(false);session.SetMenu(false);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            var runtime=session.Modes;var player=session.Survivor();var rules=ModeSimulation.Defaults(GameModeKind.Demo);rules.countdownSeconds=0;rules.healthEnabled=true;rules.deathEnabled=false;rules.fallDamage=false;rules.punchEnabled=true;rules.island=IslandRule.Walkable;runtime.Configure(rules);runtime.StartRound();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Arsenal native collision fixture";floor.layer=Layers.Surface;floor.transform.position=new Vector3(150,9,150);floor.transform.localScale=new Vector3(70,2,70);
            player.Place(new Vector3(150,10.02f,150),Quaternion.identity);player.InputSuspended=true;
            var created=session.SurvivorBots.SpawnOne();Check(created,"Target bot spawns");var target=created.GetComponent<CastleSurvivor>();target.GetComponent<CastleBotBrain>().enabled=false;target.InputSuspended=true;
            yield return new WaitForSeconds(.4f);
            Check(EquipmentSpecifications.All.Length==Enum.GetValues(typeof(EquipmentKind)).Length,"Every serialized equipment kind has one shared specification");
            Check((int)EquipmentKind.PoweredArmor==8&&(int)EquipmentKind.Blaster==3,"Existing equipment wire identities stay unchanged");
            foreach(EquipmentKind kind in Enum.GetValues(typeof(EquipmentKind)))
            {
                if(kind<=EquipmentKind.PoweredArmor||EquipmentSpecifications.Get(kind).power)continue;
                player.Place(new Vector3(150,10.02f,150),Quaternion.identity);player.InputSuspended=true;
                target.Place(new Vector3(150,10.02f,EquipmentSpecifications.Get(kind).melee?151:kind==EquipmentKind.GrenadeLauncher?155:158),Quaternion.identity);target.InputSuspended=true;Physics.SyncTransforms();yield return new WaitForSeconds(.24f);
                Check(runtime.GiveEquipment(kind),"Demo grant equips "+kind);Check(runtime.EquipmentFor(runtime.LocalActorId).drawn,"Granted weapon is drawn: "+kind);
                float before=target.Health;var origin=player.ViewPose.position+Vector3.forward*.4f;var pose=new Pose(origin,Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-origin));
                if(EquipmentSpecifications.Get(kind).charge>0)
                {
                    var held=(IHeldEquipmentRuntime)runtime;
                    Check(held.RequestEquipmentTrigger(true,pose),"Held trigger accepted for "+kind);
                    float start=Time.time;while(Time.time-start<EquipmentSpecifications.Get(kind).charge-.12f){held.RequestEquipmentTrigger(true,pose);yield return new WaitForSeconds(.07f);}
                    Check(target.Health==before,"No early damage before spin-up: "+kind);
                    while(Time.time-start<EquipmentSpecifications.Get(kind).charge+(kind==EquipmentKind.ChainGun?.9f:.3f)){held.RequestEquipmentTrigger(true,pose);yield return new WaitForSeconds(.07f);}held.RequestEquipmentTrigger(false,pose);
                }
                else if(EquipmentSpecifications.Get(kind).automatic)
                {var held=(IHeldEquipmentRuntime)runtime;for(int i=0;i<4;i++){held.RequestEquipmentTrigger(true,pose);yield return new WaitForSeconds(.08f);}held.RequestEquipmentTrigger(false,pose);}
                else {Check(runtime.RequestEquipmentAttack(pose),"Authoritative attack accepted for "+kind);if(EquipmentSpecifications.Get(kind).projectileSpeed>0)Check(runtime.Frame.weaponProjectiles.Any(p=>p.kind==kind),"Travelling projectile publishes immediately for "+kind);}
                float end=Time.realtimeSinceStartup+3;while(target.Health>=before&&Time.realtimeSinceStartup<end)yield return null;
                Check(target.Health<before,"Physical target takes damage from "+kind+(target.Health<before?"":"; shooter="+player.transform.position+" target="+target.transform.position+" state="+JsonUtility.ToJson(runtime.EquipmentFor(runtime.LocalActorId))));
                Check(ModeFrameValidation.Frame(JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson()),Services.World.Blocks.Count),"Snapshot validates after "+kind);
                yield return new WaitForSeconds(.25f);
            }
            target.Place(new Vector3(150,10.02f,158),Quaternion.identity);target.InputSuspended=true;yield return new WaitForSeconds(.25f);
            var muzzle=new Pose(player.ViewPose.position+Vector3.forward*.4f,Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-player.ViewPose.position-Vector3.forward*.4f));var trigger=(IHeldEquipmentRuntime)runtime;
            Check(runtime.GiveEquipment(EquipmentKind.StockPistol)&&runtime.RequestEquipment(true,false,true),"Stock pistol switches to burst mode");yield return new WaitForSeconds(.45f);int first=runtime.Frame.equipmentSequence;
            for(int i=0;i<7;i++){trigger.RequestEquipmentTrigger(true,muzzle);yield return new WaitForSeconds(.08f);}
            Check(runtime.Frame.equipmentActions.Count(a=>a.id>first&&a.kind==EquipmentKind.StockPistol&&a.action==EquipmentAction.Shot)==3,"One held burst fires exactly three rounds");trigger.RequestEquipmentTrigger(false,muzzle);
            target.Place(new Vector3(150,10.02f,156),Quaternion.identity);target.InputSuspended=true;yield return new WaitForSeconds(.25f);
            Check(runtime.GiveEquipment(EquipmentKind.Chainsaw)&&runtime.RequestEquipment(true,false,true),"Chainsaw switches to wheel launcher");yield return new WaitForSeconds(.45f);
            muzzle=new Pose(player.ViewPose.position+Vector3.forward*.4f,Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-player.ViewPose.position-Vector3.forward*.4f));
            float chargeStart=Time.time;bool wheel=false;while(Time.time-chargeStart<1.25f){trigger.RequestEquipmentTrigger(true,muzzle);yield return new WaitForSeconds(.07f);wheel|=runtime.Frame.weaponProjectiles.Any(p=>p.kind==EquipmentKind.Chainsaw);}trigger.RequestEquipmentTrigger(false,muzzle);Check(wheel,"One-second charged chainsaw launches a travelling wheel");
            Check(runtime.GiveEquipment(EquipmentKind.LaserAxe)&&runtime.RequestEquipment(true,false,true),"Axe switches to full-width laser");yield return new WaitForSeconds(.45f);float health=target.Health;Check(runtime.RequestEquipmentAttack(muzzle),"Axe laser attack accepted");Check(target.Health<health,"Axe beam damages a physical target at range");
            target.Place(new Vector3(150,10.02f,156),Quaternion.identity);target.InputSuspended=true;yield return new WaitForSeconds(.25f);
            Check(runtime.GiveEquipment(EquipmentKind.WristLaser),"Wrist laser equips");health=target.Health;for(int i=0;i<4;i++){trigger.RequestEquipmentTrigger(true,muzzle);yield return new WaitForSeconds(.08f);}trigger.RequestEquipmentTrigger(false,muzzle);float stopped=target.Health;yield return new WaitForSeconds(.3f);Check(stopped<health&&target.Health==stopped,"Continuous beam deals repeated damage and stops on release");
            rules.defaultDagger=true;runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.3f);
            player.Place(new Vector3(150,10.02f,150),Quaternion.identity);player.InputSuspended=true;target.Place(new Vector3(150,10.02f,151),Quaternion.identity);target.InputSuspended=true;yield return new WaitForSeconds(.25f);
            Check(runtime.EquipmentFor(runtime.LocalActorId).kind==EquipmentKind.Dagger&&runtime.EquipmentFor(runtime.LocalActorId).builtIn,"Configured mode supplies an always-ready fallback dagger");
            var knifeOrigin=player.ViewPose.position+Vector3.forward*.4f;var knifePose=new Pose(knifeOrigin,Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-knifeOrigin));float knifeHealth=target.Health;
            Check(runtime.RequestEquipmentAttack(knifePose)&&Mathf.Abs(knifeHealth-target.Health-EquipmentSpecifications.Get(EquipmentKind.Dagger).damage)<.01f,"Fallback dagger inflicts its stronger PvP melee damage");
            Check(runtime.RequestEquipment(true,true,false)&&runtime.EquipmentFor(runtime.LocalActorId).leftHanded,"Fallback dagger respects dominant-hand selection");runtime.RequestEquipment(true,false,false);
            Check(runtime.GiveEquipment(EquipmentKind.Speed),"Speed power equips beside fallback dagger");yield return new WaitForSeconds(.15f);
            Check(runtime.EquipmentFor(runtime.LocalActorId).kind==EquipmentKind.Dagger&&runtime.EquipmentFor(runtime.LocalActorId).activePower==EquipmentKind.Speed&&player.PowerupSpeedMultiplier==2,"Movement power keeps the fallback blade visible and applies its real effect");
            var power=runtime.RegisterPickup(new PickupSource{Kind=EquipmentKind.Jump,Position=player.transform.position+Vector3.forward*.5f+Vector3.up*.3f});
            Check(!runtime.RequestPickup(power,false)&&runtime.RequestPickup(power,true),"A carried timed power still requires normal swap confirmation");yield return new WaitForSeconds(.15f);
            Check(runtime.EquipmentFor(runtime.LocalActorId).activePower==EquipmentKind.Jump&&Mathf.Approximately(player.EffectiveJumpHeight,player.jumpHeight*3),"Jump power coexists with the blade");
            var timed=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());Check(ModeFrameValidation.Frame(timed,Services.World.Blocks.Count),"Combined fallback and timed-power snapshot validates");
            typeof(MatchModeRuntime).GetMethod("RestoreEquipment",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(runtime,new object[]{timed});
            Check(runtime.EquipmentFor(runtime.LocalActorId).activePower==EquipmentKind.Jump,"Authority restore keeps the timed power beneath the fallback weapon");yield return new WaitForSeconds(1.2f);
            Check(runtime.EquipmentFor(runtime.LocalActorId).kind==EquipmentKind.Dagger&&runtime.EquipmentFor(runtime.LocalActorId).activePower==EquipmentKind.None,"Timed-power expiry returns to the ordinary fallback blade");
            int pickups=runtime.Frame.pickups.Length;Check(runtime.GiveEquipment(EquipmentKind.RocketLauncher),"Pickup weapon replaces fallback dagger");Check(runtime.Frame.pickups.Length==pickups,"Fallback dagger does not generate a ground drop");
            Check(new[]{GameModeKind.CaptureFlag,GameModeKind.KingOfHill,GameModeKind.BlockControl,GameModeKind.TeamTickets}.All(m=>ModeSimulation.Defaults(m).defaultDagger)&&!ModeSimulation.Defaults(GameModeKind.MurderMystery).defaultDagger,"PvP defaults use daggers while Mystery keeps role weapons");
            rules.defaultDagger=false;runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.3f);player.Place(new Vector3(150,10.02f,150),Quaternion.identity);player.InputSuspended=true;
            Check(runtime.GiveEquipment(EquipmentKind.NeedleDrill),"Snapshot fixture equips charged weapon");trigger.RequestEquipmentTrigger(true,muzzle);yield return new WaitForSeconds(.12f);
            var saved=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());Check(saved.equipment.Any(e=>e.kind==EquipmentKind.NeedleDrill&&e.triggerHeld&&e.charge>0&&e.charge<1),"Charge progress is replicated as finite bounded data");trigger.RequestEquipmentTrigger(false,muzzle);
            saved.equipment.First(e=>e.kind==EquipmentKind.NeedleDrill).charge=float.NaN;Check(!ModeFrameValidation.Frame(saved,Services.World.Blocks.Count),"Malformed charge snapshot is rejected");
            Check(runtime.SpawnSettings.types.Length==EquipmentSpecifications.All.Length-1,"Random spawner exposes every item weight");
            BotObjectiveContractChecks.Run();Check(BotObjectiveContractChecks.LastPassed,"Existing objective contracts pass");
            Check(runtime.GiveEquipment(EquipmentKind.RocketLauncher),"Transport fixture equips rocket launcher");
            var sky=new Pose(player.ViewPose.position+Vector3.up*.2f,Quaternion.LookRotation((Vector3.forward+Vector3.up).normalized));Check(runtime.RequestEquipmentAttack(sky),"Transport fixture launches a rocket");
            var snapshot=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());Check(snapshot.weaponProjectiles.Length>0,"Immediate snapshot includes an airborne rocket");
            typeof(MatchModeRuntime).GetMethod("RestoreEquipment",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(runtime,new object[]{snapshot});yield return null;Check(runtime.Frame.weaponProjectiles.Any(p=>p.kind==EquipmentKind.RocketLauncher),"Authority restore retains airborne rocket state");
            Check(runtime.GiveEquipment(EquipmentKind.PoweredArmor)&&runtime.RequestThrust(true),"Transport fixture equips active suit beside airborne rocket");var active=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());runtime.RequestThrust(false);var inactive=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
            yield return PoweredSuitNetworkChecks.Run(runtime,active,inactive,Check);
            yield return Gallery(player.GetComponent<CandyPlayerAvatar>().Rig.style.material);
            var hud=session.GetComponent<CandyDemoHud>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            Check(DemoMenuSections.Tabs.Length==4&&DemoMenuSections.Sections.SelectMany(x=>x).Contains("Disasters")&&DemoMenuSections.Sections.SelectMany(x=>x).Contains("Powerup spawning"),"Four main tabs retain disasters and niche powerup controls");
            for(int i=0;i<4;i++){typeof(CandyDemoHud).GetField("page",flags).SetValue(hud,i);typeof(CandyDemoHud).GetField("subsection",flags).SetValue(hud,0);session.SetMenu(true);yield return new WaitForSeconds(.15f);yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(ProjectPath.Report("Arsenal-Menu-"+i+".png"),shot.EncodeToPNG());Object.Destroy(shot);}
            var menu=Object.FindFirstObjectByType<CastleWorldMenu>(FindObjectsInactive.Include);Check(menu,"VR menu exists");menu.Show(true,session.ActiveCamera);typeof(CastleWorldMenu).GetField("menuCategory",flags).SetValue(menu,-1);typeof(CastleWorldMenu).GetMethod("PointOrganizedMenu",flags).Invoke(menu,new object[]{session.ActiveCamera,false});var texts=menu.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t=>t.text).ToArray();Check(DemoMenuSections.Tabs.All(t=>texts.Contains(t)),"VR root shows exactly the four requested main categories");menu.Show(false,session.ActiveCamera);
            Check(Shader.Find("DeadlyJenga/Pickup Cloud")&&!UnityEditor.ShaderUtil.ShaderHasError(Shader.Find("DeadlyJenga/Pickup Cloud")),"Static-cloud pickup shader compiles");
            session.SetMenu(false);yield return ExpandedBehaviourChecks.Run(session,Check);
            Check(errors==0,"No unexpected runtime errors");Object.Destroy(floor);
        }
        public static IEnumerator CaptureModels(CastlePlaySession session,Action<bool,string> verify)=>Gallery(session.Survivor().GetComponent<CandyPlayerAvatar>().Rig.style.material,verify);
        static IEnumerator Gallery(Material material,Action<bool,string> verify=null)
        {
            var validate=verify??new Action<bool,string>(Check);
            var root=new GameObject("Arsenal visual inspection gallery");var origin=new Vector3(2000,1000,2000);int index=0;
            foreach(var offset in new[]{new Vector3(-3,3,-3),new Vector3(4,1,-2),new Vector3(0,-2,-3)}){var lamp=new GameObject("Gallery softbox",typeof(Light));lamp.transform.SetParent(root.transform,false);lamp.transform.position=origin+offset;var light=lamp.GetComponent<Light>();light.type=LightType.Point;light.range=20;light.intensity=offset.y>2?24:12;light.color=offset.x>0?new Color(.5f,.8f,1):Color.white;light.shadows=LightShadows.None;}
            foreach(EquipmentKind kind in Enum.GetValues(typeof(EquipmentKind)))
            {
                if(kind<=EquipmentKind.PoweredArmor||EquipmentSpecifications.Get(kind).power)continue;
                var holder=new GameObject(kind.ToString());holder.transform.SetParent(root.transform,false);var position=origin+new Vector3((index%5-2)*1.9f,(1-index/5)*1.6f,0);var rotation=Quaternion.Euler(-15,-55,0);
                var frame=new CandyAvatarFrame{scale=1,feet=position-Vector3.up,head=position+Vector3.up*.5f,headRotation=rotation,left=new CandyHandPose{position=position+Vector3.left*.2f,rotation=rotation},right=new CandyHandPose{position=position,rotation=rotation}};
                var visual=new CandyWeaponVisual(holder.transform,material);visual.Present(new EquipmentState{kind=kind,drawn=true,triggerHeld=true,charge=.5f},frame,true);
                validate(holder.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)>2&&holder.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)<=10&&holder.GetComponentsInChildren<Collider>().Length==0,"Detailed collider-free model: "+kind);
                validate(EquipmentModelData.Parts.TryGetValue(kind,out var parts)&&parts.Length>2,"Live pickup geometry catalog includes "+kind);
                var label=new GameObject(kind+" label",typeof(TextMesh));label.transform.SetParent(root.transform,false);label.transform.position=position+new Vector3(0,-.50f,-1);var text=label.GetComponent<TextMesh>();text.text=EquipmentSpecifications.Get(kind).name.Replace(" / ","\n");text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.characterSize=.035f;text.fontSize=44;text.color=Color.white;index++;
            }
            var go=new GameObject("Gallery camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.transform.position=origin+Vector3.back*12;camera.transform.rotation=Quaternion.identity;camera.orthographic=true;camera.orthographicSize=3.2f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.12f,.18f);camera.farClipPlane=30;camera.nearClipPlane=.01f;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
            var rt=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;camera.targetTexture=rt;yield return null;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report("Arsenal-Gallery.png"),pixels.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(pixels);
            foreach(var text in root.GetComponentsInChildren<TextMesh>())text.gameObject.SetActive(false);
            foreach(var kind in new[]{EquipmentKind.RocketLauncher,EquipmentKind.StockPistol,EquipmentKind.NeedleDrill,EquipmentKind.LaserAxe})
            {
                var holder=root.transform.Find(kind.ToString());var renderers=holder.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);camera.transform.position=bounds.center+Vector3.back*4;camera.orthographicSize=Mathf.Max(bounds.size.y,bounds.size.x/1.6f)*.65f;camera.Render();RenderTexture.active=rt;var detail=new Texture2D(1600,1000,TextureFormat.RGB24,false);detail.ReadPixels(new Rect(0,0,1600,1000),0,0);detail.Apply();File.WriteAllBytes(ProjectPath.Report("Arsenal-"+kind+".png"),detail.EncodeToPNG());Object.Destroy(detail);RenderTexture.active=previous;
            }
            foreach(Transform child in root.transform)if(!child.GetComponent<Light>())child.gameObject.SetActive(false);
            var runtime=(Services.Session as CastlePlaySession).Modes;var showcase=new[]{EquipmentKind.RocketLauncher,EquipmentKind.NeedleDrill,EquipmentKind.Speed,EquipmentKind.Jump,EquipmentKind.PoweredArmor};for(int i=0;i<showcase.Length;i++)runtime.RegisterPickup(new PickupSource{Kind=showcase[i],Position=origin+new Vector3((i-(showcase.Length-1)*.5f)*.7f,0,0),Duration=20});
            camera.transform.position=origin+Vector3.back*5;camera.orthographicSize=1.15f;yield return new WaitForSeconds(.25f);camera.Render();RenderTexture.active=rt;var pickups=new Texture2D(1600,1000,TextureFormat.RGB24,false);pickups.ReadPixels(new Rect(0,0,1600,1000),0,0);pickups.Apply();File.WriteAllBytes(ProjectPath.Report("Arsenal-Pickups.png"),pickups.EncodeToPNG());Object.Destroy(pickups);RenderTexture.active=previous;
            validate(Object.FindFirstObjectByType<DeadlyJenga.Presentation.CandyEquipmentView>().GetComponentsInChildren<Transform>().Any(t=>t.name=="Live equipment model"),"Actual world pickups contain live equipment geometry");
            camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(go);Object.Destroy(root);yield return null;
        }
    }
}
