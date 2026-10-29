using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Modes;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Fusion;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using LogType=UnityEngine.LogType;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class FirearmUpgradeChecks
    {
        const string Key="FirearmUpgradeChecks";
        static int errors,checks,fixtureNotes;static bool failed,transportFixture;
        static string Report=>ProjectPath.Report("FirearmUpgrade.txt");
        [Serializable] sealed class Setup{public SceneSetup[] scenes;}
        sealed class Pickup:IEquipmentPickupSource{public EquipmentKind Kind{get;set;}public Vector3 Position{get;set;}public float Duration=>20;}
        static FirearmUpgradeChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){SessionState.SetBool(Key+"Entered",true);AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);errors=checks=0;failed=false;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>new GameObject("Firearm checks").AddComponent<FirearmUpgradeDriver>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Entered",false))
                {
                    failed|=!SessionState.GetBool(Key+"Complete",false);
                    if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    var setup=JsonUtility.FromJson<Setup>(SessionState.GetString(Key+"Scenes",""));
                    if(setup.scenes.Any(s=>s.isActive&&s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                    File.AppendAllText(Report,$"COMPLETE checks={checks} runtimeErrors={errors} failed={failed}. No headset test. Photon transport is checked with two actual peers, not two physical headsets.\n");
                    if(Application.isBatchMode)EditorApplication.Exit(failed||errors>0?1:0);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Revolver and Gun Sights")]
        public static void Run()=>Start(false);
        public static void RunAimOnly()=>Start(true);
        static void Start(bool aimOnly)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();if(scenes.Any(s=>s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            BotObjectiveContractChecks.Run();
            if(!BotObjectiveContractChecks.LastPassed)throw new InvalidOperationException("Bot objective contracts failed.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Setup{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key+"Entered",false);SessionState.SetBool(Key,true);
            SessionState.SetBool(Key+"Complete",false);SessionState.SetBool(Key+"AimOnly",aimOnly);
            File.WriteAllText(Report,"Actual Game firearm, death drop, Mystery handoff and local zoom checks.\n");
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type){
            if(transportFixture&&type==LogType.Error&&NetworkProjectConfig.Global.PeerMode==NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.",StringComparison.Ordinal))
            {fixtureNotes++;File.AppendAllText(Report,"TRANSPORT FIXTURE NOTE Empty-scene multiple-peer diagnostic; this fixture tests reliable packets, not replicated gameplay scenes.\n");return;}
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Check(bool pass,string label){File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+label+"\n");if(!pass)throw new Exception(label);checks++;}
        static void Expect(bool pass,string label){File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+label+"\n");failed|=!pass;checks++;}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Test());
            while(stack.Count>0){bool more=false;object value=null;Exception error=null;try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}if(error!=null){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;}
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();SessionState.SetBool(Key+"Complete",true);
            if(Application.isBatchMode)
            {
                File.AppendAllText(Report,$"COMPLETE checks={checks} runtimeErrors={errors} transportFixtureNotes={fixtureNotes} failed={failed}. Batch exits from Play Mode; no scene assets saved. Headset unverified.\n");
                SessionState.SetBool(Key,false);EditorApplication.Exit(failed||errors>0?1:0);yield break;
            }
            EditorApplication.ExitPlaymode();
        }
        static IEnumerator Test()
        {
            float deadline=Time.realtimeSinceStartup+30;while(!(Services.Session is CastlePlaySession sessionReady&&sessionReady.Modes!=null)&&Time.realtimeSinceStartup<deadline)yield return null;
            var session=Services.Session as CastlePlaySession;Check(session&&session.Modes!=null,"Game initializes");session.SetRole(false);session.SetMenu(false);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            var runtime=session.Modes;var rules=ModeSimulation.Defaults(GameModeKind.Demo);rules.countdownSeconds=0;rules.island=IslandRule.Walkable;runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.3f);
            if(SessionState.GetBool(Key+"AimOnly",false)){yield return AimOnly(session);yield break;}
            yield return WeaponPresentationChecks.Run();yield return WeaponEventChecks.Run(session);
            runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.3f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=Layers.Surface;floor.transform.position=new Vector3(150,9,150);floor.transform.localScale=new Vector3(35,2,110);
            var player=session.Survivor();var controls=player.GetComponent<CandyEquipment>();
            var bot=session.SurvivorBots.SpawnOne();Check(bot,"Physical target spawns");bot.GetComponent<CastleBotBrain>().enabled=false;var target=bot.GetComponent<CastleSurvivor>();target.InputDriver=null;
            player.Place(new Vector3(150,10.05f,120),Quaternion.identity);player.InputSuspended=true;target.Place(new Vector3(150,10.05f,150),Quaternion.identity);target.InputSuspended=true;yield return new WaitForSeconds(.2f);
            int id=runtime.RegisterPickup(new Pickup{Kind=EquipmentKind.SheriffHammer,Position=player.transform.position+Vector3.forward*.6f+Vector3.up*.2f});Check(runtime.RequestPickup(id,true),"Revolver occupies the existing single inventory slot");controls.SetEquipped(true);yield return null;
            player.viewCamera.transform.rotation=Quaternion.LookRotation(target.Capsule.bounds.center-player.viewCamera.transform.position);Physics.SyncTransforms();float health=target.Health;
            session.SetMenu(false);player.InputSuspended=false;
            File.AppendAllText(Report,$"DIAGNOSTIC equipment={JsonUtility.ToJson(controls.Equipment)} menu={session.MenuOpen} alive={player.Alive} focused={player.HasInputFocus} climb={player.Climbing} vault={player.Vaulting} phase={runtime.Frame.phase} muzzle={controls.MuzzlePose.position} eye={player.ViewPose.position}\n");
            Check(controls.FireOnce(),"Revolver long-range shot accepted");player.InputSuspended=true;Check(Mathf.Abs(health-target.Health-85)<.1f,"Pistol deals 85 damage at 30 metres");
            Check(runtime.Frame.equipmentActions.Last().endpoints.Length==1&&ModeFrameValidation.EquipmentActions(runtime.Frame),"Long-range revolver endpoint validates in transported action frame");
            var pistolFrame=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());Capture(player.transform.Find("Visual - SheriffHammer"),"Sheriff-Revolver.png");
            yield return new WaitForSeconds(.8f);target.Place(player.transform.position+Vector3.forward*6,Quaternion.identity);target.InputSuspended=true;controls.ToggleRifleMode();yield return new WaitForSeconds(.45f);Physics.SyncTransforms();
            player.viewCamera.transform.rotation=Quaternion.LookRotation(target.Capsule.bounds.center-player.viewCamera.transform.position);health=target.Health;
            session.SetMenu(false);player.InputSuspended=false;Check(controls.FireOnce(),"Rapid revolver shotgun shot accepted");player.InputSuspended=true;Check(target.Health<health&&health-target.Health<=70.1f,"Revolver shotgun damage is bounded to seven 10-damage pellets");
            Check(runtime.Frame.equipmentActions.Last().shotgun&&runtime.Frame.equipmentActions.Last().endpoints.Length==7&&ModeFrameValidation.EquipmentActions(runtime.Frame),"Revolver shotgun transports seven valid visible endpoints");
            var shotgunFrame=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=Layers.Surface;wall.transform.position=player.transform.position+Vector3.forward*3+Vector3.up;wall.transform.localScale=new Vector3(6,4,.3f);yield return new WaitForSeconds(.31f);Physics.SyncTransforms();health=target.Health;
            session.SetMenu(false);player.InputSuspended=false;Check(controls.FireOnce(),"Rapid shotgun can fire again after .31 seconds");player.InputSuspended=true;Check(target.Health==health,"Solid cover blocks revolver pellets");Object.Destroy(wall);
            id=runtime.RegisterPickup(new Pickup{Kind=EquipmentKind.DualRifle,Position=player.transform.position+Vector3.forward*.6f+Vector3.up*.2f});Check(runtime.RequestPickup(id,true),"Rifle swaps through the same slot");controls.SetEquipped(true);yield return new WaitForSeconds(.4f);
            var carriage=player.transform.Find("Visual - DualRifle/Rotating 180 degree barrel carriage");Check(carriage,"Rifle has a rotating barrel carriage");var rotation=carriage.localRotation;controls.ToggleRifleMode();
            Check(!runtime.RequestEquipmentAttack(controls.MuzzlePose),"Mode switch prevents firing through its transition");yield return new WaitForSeconds(.4f);
            Check(Quaternion.Angle(rotation,carriage.localRotation)>179,"Barrels and attached optics turn a full 180 degrees");Check(carriage.Find("Shotgun optic")&&carriage.Find("Rifle optic"),"Both mode-specific optics travel with their barrel");Capture(player.transform.Find("Visual - DualRifle"),"Rotating-Rifle-Shotgun.png");
            controls.ToggleRifleMode();yield return new WaitForSeconds(.4f);Capture(player.transform.Find("Visual - DualRifle"),"Rotating-Rifle-Precision.png");
            yield return Aim(session,player,4);
            transportFixture=true;
            try{yield return FirearmNetworkChecks.Run(pistolFrame,shotgunFrame,Check);}finally{transportFixture=false;}
            rules=ModeSimulation.Defaults(GameModeKind.MurderMystery);rules.countdownSeconds=0;rules.mystery.attackGraceSeconds=0;rules.island=IslandRule.Walkable;runtime.Configure(rules);
            while(session.SurvivorBots.Count<4){var extra=session.SurvivorBots.SpawnOne();extra.GetComponent<CastleBotBrain>().enabled=false;extra.GetComponent<CastleSurvivor>().InputDriver=null;}
            runtime.StartRound();yield return new WaitForSeconds(.3f);runtime.SetDemoRole(MysteryRole.Sheriff);yield return new WaitForSeconds(.3f);
            player.Place(new Vector3(150,10.05f,120),Quaternion.identity);player.InputSuspended=true;controls.SetEquipped(false);yield return null;
            Check(controls.Equipment.kind==EquipmentKind.SheriffHammer&&!controls.Equipment.drawn,"Mystery issues the new revolver while keeping it concealed");
            var point=player.transform.position;int before=runtime.Frame.pickups.Count(p=>p.kind==EquipmentKind.SheriffHammer);player.Eliminate(null);yield return null;
            Check(runtime.Frame.pickups.Count(p=>p.kind==EquipmentKind.SheriffHammer)==before+1,"Holstered sheriff death drops exactly one revolver");var dropped=runtime.Frame.pickups.Last(p=>p.kind==EquipmentKind.SheriffHammer);
            Check(Vector3.Distance(dropped.position,point)<.4f,"Sheriff's revolver drops at his death/body position");runtime.HandleEliminated(player.gameObject,null);Check(runtime.Frame.pickups.Count(p=>p.kind==EquipmentKind.SheriffHammer)==before+1,"Repeated elimination callback cannot duplicate the drop");
            yield return new WaitForSeconds(1.1f);
            var innocent=runtime.Frame.players.First(p=>p.bot&&p.alive&&JsonUtility.FromJson<MysteryPrivateFrame>(runtime.CaptureSecret(p.id)).role==MysteryRole.Innocent);var receiver=runtime.FindActor(innocent.id);receiver.gameObject.GetComponent<IPlayableCharacter>().Place(point+Vector3.right*.6f,Quaternion.identity);Physics.SyncTransforms();
            runtime.ReceiveCommand(innocent.id,"equipment:"+JsonUtility.ToJson(new MatchModeRuntime.EquipmentCommand{action="pickup",pickup=dropped.id,swap=true}));
            Check(runtime.EquipmentFor(innocent.id).kind==EquipmentKind.SheriffHammer&&JsonUtility.FromJson<MysteryPrivateFrame>(runtime.CaptureSecret(innocent.id)).role==MysteryRole.Sheriff,"An innocent recovers both the physical gun and sheriff role");Check(!runtime.Frame.pickups.Any(p=>p.id==dropped.id),"Recovering consumes the dropped weapon");
            int speed=runtime.RegisterPickup(new Pickup{Kind=EquipmentKind.Speed,Position=receiver.transform.position+Vector3.forward*.5f+Vector3.up*.2f});
            runtime.ReceiveCommand(innocent.id,"equipment:"+JsonUtility.ToJson(new MatchModeRuntime.EquipmentCommand{action="pickup",pickup=speed,swap=true}));
            Check(runtime.EquipmentFor(innocent.id).kind==EquipmentKind.Speed,"Sheriff can swap the revolver into a physical ground pickup");
            var ownDrop=runtime.Frame.pickups.Last(p=>p.kind==EquipmentKind.SheriffHammer);yield return new WaitForSeconds(1.1f);
            runtime.ReceiveCommand(innocent.id,"equipment:"+JsonUtility.ToJson(new MatchModeRuntime.EquipmentCommand{action="pickup",pickup=ownDrop.id,swap=true}));
            Check(runtime.EquipmentFor(innocent.id).kind==EquipmentKind.SheriffHammer,"A living sheriff can retrieve his own swapped revolver");
            Object.Destroy(floor);
        }
        static IEnumerator Aim(CastlePlaySession session,CastleSurvivor player,float expectedMagnification)
        {
            var originalSettings=InputSystem.settings;
            var originalFlags=originalSettings.hideFlags;
            var focusedSettings=Object.Instantiate(originalSettings);focusedSettings.hideFlags=HideFlags.HideAndDontSave;
            focusedSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            focusedSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            Mouse mouse=null;DeadlyJenga.DevTools.LateFrameTestInput aimDriver=null;var oldLock=Cursor.lockState;
            try
            {
                // Input System deletes temporary default settings when they are replaced.
                if(originalFlags==HideFlags.HideAndDontSave)originalSettings.hideFlags=HideFlags.DontSave;
                InputSystem.settings=focusedSettings;
                mouse=InputSystem.AddDevice<Mouse>("Firearm validation mouse");InputSystem.EnableDevice(mouse);
                aimDriver=session.gameObject.AddComponent<DeadlyJenga.DevTools.LateFrameTestInput>();
                aimDriver.Apply=()=>{InputState.Change(mouse,new MouseState().WithButton(MouseButton.Right));mouse.MakeCurrent();session.SetMenu(false);player.InputSuspended=false;};
                player.InputSuspended=false;float fov=player.viewCamera.fieldOfView;
                for(int aimFrame=0;aimFrame<18;aimFrame++){aimDriver.Apply();yield return null;}
                File.AppendAllText(Report,$"ZOOM aiming={session.GetComponent<CandyGunSights>().Aiming} fov={player.viewCamera.fieldOfView} original={fov} driverFrames={aimDriver.AppliedFrames} menu={session.MenuOpen} inputPaused={player.InputSuspended} mouse={Mouse.current?.rightButton.isPressed} deviceEnabled={mouse.enabled} activeCameraIsPlayer={session.ActiveCamera==player.viewCamera}\n");
                Expect(aimDriver.AppliedFrames>=18,"Runtime input fixture executes during LateUpdate");
                Expect(session.GetComponent<CandyGunSights>().Aiming&&Mathf.Abs(player.viewCamera.fieldOfView/fov-1/expectedMagnification)<.03f,$"Holding aim produces {expectedMagnification}x desktop zoom");
                aimDriver.enabled=false;session.SetMenu(true);yield return null;Expect(Mathf.Abs(player.viewCamera.fieldOfView-fov)<.1f,"Opening the menu restores the original camera FOV");session.SetMenu(false);
            }
            finally
            {
                if(aimDriver){aimDriver.enabled=false;Object.Destroy(aimDriver);}
                if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
                InputSystem.settings=originalSettings;originalSettings.hideFlags=originalFlags;
                if(focusedSettings)Object.Destroy(focusedSettings);
                Cursor.lockState=oldLock;player.InputSuspended=true;
            }
            Expect(ReferenceEquals(InputSystem.settings,originalSettings)&&originalSettings.hideFlags==originalFlags,"Original input settings and lifetime flags restored without changing the project asset");
        }
        static IEnumerator AimOnly(CastlePlaySession session)
        {
            var player=session.Survivor();var controls=player.GetComponent<CandyEquipment>();var runtime=session.Modes;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=Layers.Surface;floor.transform.position=new Vector3(150,9,150);floor.transform.localScale=new Vector3(20,2,20);
            player.Place(new Vector3(150,10.05f,150),Quaternion.identity);player.InputSuspended=true;yield return new WaitForSeconds(.2f);
            foreach(var kind in new[]{EquipmentKind.Blaster,EquipmentKind.SheriffHammer,EquipmentKind.DualRifle})
            {
                int id=runtime.RegisterPickup(new Pickup{Kind=kind,Position=player.transform.position+Vector3.forward*.6f+Vector3.up*.2f});Check(runtime.RequestPickup(id,true),"Aim fixture equips "+kind);controls.SetEquipped(true);yield return new WaitForSeconds(.1f);
                yield return Aim(session,player,kind==EquipmentKind.DualRifle?4:kind==EquipmentKind.Blaster?1.5f:2);
                if(kind==EquipmentKind.DualRifle){controls.ToggleRifleMode();yield return new WaitForSeconds(.4f);yield return Aim(session,player,1);}
            }
            Object.Destroy(floor);
        }
        static void Capture(Transform model,string file)
        {
            var go=new GameObject("Firearm preview camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.01f;camera.fieldOfView=42;camera.transform.position=model.TransformPoint(new Vector3(.9f,.6f,-1.05f));camera.transform.LookAt(model.TransformPoint(new Vector3(0,.08f,.3f)));
            var texture=new RenderTexture(960,720,24);var old=RenderTexture.active;camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;var pixels=new Texture2D(960,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,720),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(file),pixels.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;Object.Destroy(pixels);Object.Destroy(texture);Object.Destroy(go);
        }
    }
    public sealed class FirearmUpgradeDriver:MonoBehaviour{}
}
