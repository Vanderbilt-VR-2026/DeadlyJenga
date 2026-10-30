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

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class CandyPolishReplayChecks
    {
        const string Key="CandyPolishReplay";
        static int checks,errors;static bool failed;
        static string Report=>ProjectPath.Report("CandyPolishReplay.txt");
        [Serializable] sealed class Scenes{public SceneSetup[] values;}
        static CandyPolishReplayChecks()
        {
            EditorApplication.update+=()=>
            {
                string path=ProjectPath.Report("CandyPolishReplay.request");
                if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool visualOnly=File.ReadAllText(path).Trim()=="visuals";File.Delete(path);Run();SessionState.SetBool(Key+"VisualOnly",visualOnly);
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {checks=errors=0;failed=false;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).values);
                    File.AppendAllText(Report,$"COMPLETE checks={checks} runtimeErrors={errors} failed={failed}. Desktop runtime; headset not tested.\n");
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Candy Polish and Mystery Replays")]
        public static void Run()
        {
            SessionState.SetBool(Key+"VisualOnly",false);
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(setup.Any(s=>s.isLoaded&&UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{values=setup}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            File.WriteAllText(Report,"Real Game hover, rifle, cosmetics and private Mystery replay checks.\n");
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
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();
        }
        sealed class Source:IEquipmentPickupSource{public EquipmentKind Kind{get;set;}public Vector3 Position{get;set;}public float Duration=>20;}
        static void Grant(MatchModeRuntime runtime,CastleSurvivor p,EquipmentKind kind)
        {int id=runtime.RegisterPickup(new Source{Kind=kind,Position=p.transform.position+Vector3.up*.2f});Check(runtime.RequestPickup(id,true),"Grant "+kind);Check(runtime.RequestEquipment(true,false,false),"Draw "+kind);}
        static IEnumerator Test()
        {
            var session=Object.FindFirstObjectByType<CastlePlaySession>();float until=Time.realtimeSinceStartup+30;
            while((session.Player==null||session.Modes==null)&&Time.realtimeSinceStartup<until)yield return null;
            Check(session.Player!=null&&session.Modes!=null,"solo Game initializes");
            session.SetRole(false);session.SetMenu(false);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            var oldWorld=Services.World;var fixture=new GameObject("Polish fixture world");var world=fixture.AddComponent<FakeWorldQuery>();world.waterHeight=-100;world.spawnPoint=new Vector3(150,10.05f,150);world.radius=150;world.topHeight=11;world.playableBounds=new Bounds(new Vector3(150,30,150),new Vector3(160,100,160));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(fixture.transform);floor.layer=Layers.Surface;floor.transform.position=new Vector3(150,9.5f,150);floor.transform.localScale=new Vector3(150,1,150);var rigid=floor.AddComponent<Rigidbody>();rigid.isKinematic=true;world.blocks.Add(rigid);Services.World=world;
            var runtime=session.Modes;var player=session.Survivor();var controls=player.GetComponent<CandyEquipment>();bool controlsEnabled=controls.enabled,motorEnabled=player.enabled;controls.enabled=true;
            try
            {
                var rules=ModeSimulation.Defaults(GameModeKind.Demo);rules.countdownSeconds=0;rules.healthEnabled=true;rules.island=IslandRule.Walkable;runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.4f);
                player.Place(world.spawnPoint,Quaternion.identity);player.InputSuspended=true;
                for(int i=0;i<4;i++)session.SurvivorBots.SpawnOne();yield return new WaitForSeconds(.5f);
                foreach(var member in session.SurvivorBots.Members){var m=member.gameObject.GetComponent<CastleSurvivor>();m.InputSuspended=true;m.GetComponent<CastleBotBrain>().enabled=false;m.InputDriver=null;m.Place(new Vector3(180,10.05f,180),Quaternion.identity);}
                var target=session.SurvivorBots.Members.First().gameObject.GetComponent<CastleSurvivor>();target.Place(player.transform.position+Vector3.forward*6,Quaternion.identity);Physics.SyncTransforms();
                Grant(runtime,player,EquipmentKind.DualRifle);yield return new WaitForSeconds(.45f);
                var pose=new Pose(player.ViewPose.position,Quaternion.LookRotation(target.Capsule.bounds.center+Vector3.right*(target.Capsule.bounds.extents.x+.05f)-player.ViewPose.position));float health=target.Health;
                Check(runtime.RequestEquipmentAttack(pose),"Rifle accepts a near-edge shot");Check(target.Health<health,"Generous rifle trace hits capsule edge");
                runtime.RequestEquipment(true,false,true);yield return new WaitForSeconds(.7f);target.Place(player.transform.position+Vector3.forward*6,Quaternion.identity);Physics.SyncTransforms();
                Check(runtime.RequestEquipmentAttack(new Pose(player.ViewPose.position,Quaternion.LookRotation(Vector3.forward))),"Shotgun accepts first shot");yield return new WaitForSeconds(.22f);
                Check(runtime.RequestEquipmentAttack(new Pose(player.ViewPose.position,Quaternion.LookRotation(Vector3.forward))),"Shotgun accepts next shot at .22 seconds");
                var shot=runtime.Frame.equipmentActions.Last(x=>x.action==EquipmentAction.Shot);Check(shot.endpoints.Length==7,"Shotgun retains seven visible pellets");
                var rifle=player.transform.Find("Visual - DualRifle");var carriage=rifle.Find("Rotating 180 degree barrel carriage");
                Check(carriage.Find("Shotgun optic")&&carriage.Find("Rifle optic")&&carriage.Find("Rapid shotgun barrel")&&carriage.Find("Precision rifle barrel"),"Both barrels and both optics always exist on carriage");
                Check(!rifle.Find("Support pump grip")&&carriage.Find("Rifle optic/Pump-scope knurl 0"),"Rotating rifle scope provides underside pump grip");
                var up=carriage.localRotation;runtime.RequestEquipment(true,false,false);yield return new WaitForSeconds(.09f);float intermediate=Quaternion.Angle(up,carriage.localRotation);Check(intermediate>5&&intermediate<170,"Barrel visibly interpolates during half turn");yield return new WaitForSeconds(.35f);Check(Quaternion.Angle(up,carriage.localRotation)>179,"Carriage finishes full half turn");
                CaptureModel(rifle,"Dual-Rifle-Optics.png",false);
                var sling=CandyWeaponVisual.Holster(EquipmentKind.DualRifle,Vector3.zero,Vector3.up*1.6f,Quaternion.identity,1,false);var axis=sling.rotation*Vector3.forward;
                Check(Mathf.Abs(axis.x)>.3f&&axis.y>.5f&&sling.position.z<-.2f,"Back sling has a genuinely diagonal upward barrel and back mount");
                var hip=CandyWeaponVisual.Holster(EquipmentKind.Blaster,Vector3.zero,Vector3.up*1.6f,Quaternion.identity,1,false);Check(hip.position.y>.6f&&hip.position.y<.75f,"Blaster attaches at upper thigh");
                Grant(runtime,player,EquipmentKind.PoweredArmor);controls.enabled=false;player.InputSuspended=false;player.enabled=false;float height=player.transform.position.y;
                for(int i=0;i<100;i++){player.ApplyHoverFlight(Vector3.zero,.02f,i<27);player.Simulate(Vector2.zero,false,.02f);}
                Check(player.transform.position.y>height+.8f&&player.transform.position.y<height+3&&Mathf.Abs(player.VerticalSpeed)<.1f,"Capsule takes off then holds altitude without holding trigger");
                float startX=player.transform.position.x;for(int i=0;i<70;i++){player.ApplyHoverFlight(Vector3.right*4,.02f,false);player.Simulate(Vector2.zero,false,.02f);}
                Check(player.transform.position.x>startX+2,"Hover flight steers with bounded velocity");player.enabled=motorEnabled;player.InputSuspended=true;
                foreach(var sound in new[]{DeadlyJenga.CandyAudio.CandySound.PlayerPain,DeadlyJenga.CandyAudio.CandySound.PlayerDeath,DeadlyJenga.CandyAudio.CandySound.PlayerHeal})
                    Check(DeadlyJenga.CandyAudio.CandyPlayerSounds.DefaultCue(sound).variants.All(c=>c&&c.channels==1&&c.samples>0),"Four imported physical candy cues: "+sound);
                var shader=Resources.Load<Shader>("CandyAlienPlasma");Check(shader&&shader.isSupported,"Stereo-compatible translucent plasma shader imports");
                var targetAvatar=target.GetComponent<CandyPlayerAvatar>();var oldAppearance=targetAvatar.Appearance;
                targetAvatar.ApplyAppearance(new AvatarAppearance{skin=AvatarSkin.JackOLantern});yield return new WaitForSeconds(.3f);
                var tint=new MaterialPropertyBlock();targetAvatar.Rig.bodyRenderer.GetPropertyBlock(tint);var bodyColor=tint.GetColor("_BaseColor");Check(bodyColor.maxColorComponent<.08f,"Changing into pumpkin while health shading is active preserves its dark body palette");
                CaptureModel(targetAvatar.Rig.transform,"Carved-Jack-Lantern.png",true);targetAvatar.ApplyAppearance(oldAppearance);
                player.Damage(12,null);yield return new WaitForSeconds(.3f);
                var healthCanvas=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(x=>x.name=="Local health response");var rim=healthCanvas.transform.Find("Peripheral impact").GetComponent<UnityEngine.UI.Image>();
                Check(rim.color.r>rim.color.g&&rim.color.a>0,"Actual local damage produces red peripheral streak feedback");
                player.Revive();yield return new WaitForSeconds(.9f);player.Damage(8,null);yield return new WaitForSeconds(.3f);player.Revive();yield return new WaitForSeconds(.25f);
                var hp=healthCanvas.transform.Find("Health well/Health").GetComponent<UnityEngine.UI.Image>();Check(hp.color.g>hp.color.r,"Actual health restoration produces green recovery feedback");
                if(SessionState.GetBool(Key+"VisualOnly",false))yield break;
                rules=ModeSimulation.Defaults(GameModeKind.MurderMystery);rules.countdownSeconds=0;rules.mystery.attackGraceSeconds=0;rules.island=IslandRule.Walkable;runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.4f);runtime.SetDemoRole(MysteryRole.Innocent);
                var mystery=(MysterySimulation)typeof(MatchModeRuntime).GetField("mystery",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(runtime);
                var members=session.SurvivorBots.Members.Select(x=>x.gameObject.GetComponent<CastleSurvivor>()).ToArray();var killer=members.First(m=>JsonUtility.FromJson<MysteryPrivateFrame>(runtime.CaptureSecret(m.ActorId)).role==MysteryRole.Murderer);
                var sheriff=members.First(m=>JsonUtility.FromJson<MysteryPrivateFrame>(runtime.CaptureSecret(m.ActorId)).role==MysteryRole.Sheriff);
                player.Place(world.spawnPoint,Quaternion.identity);player.InputSuspended=true;
                foreach(var m in members){m.InputSuspended=true;m.Place(world.spawnPoint+new Vector3(20,0,20),Quaternion.identity);}
                killer.Place(world.spawnPoint+Vector3.forward,Quaternion.LookRotation(Vector3.back));sheriff.Place(world.spawnPoint+Vector3.forward*8,Quaternion.LookRotation(Vector3.back));
                yield return new WaitForSecondsRealtime(10.4f);
                var replay=session.GetComponent<DeadlyJenga.Presentation.CandyMysteryReplay>();Check(replay&&replay.SampleCount>=90&&replay.RecordedNodes>0,"Ten-second bounded pose history records during ordinary Game");
                File.AppendAllText(Report,"MEASURED pose sampling: "+replay.RecordedNodes+" nodes; mean "+replay.AverageCaptureMilliseconds.ToString("F3")+" ms per 10 Hz sample. Small runtime fixture, not a stress or Quest benchmark.\n");
                var liveFrame=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
                int originalEyeMask=session.ActiveCamera.cullingMask;
                Check(!Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(c=>c.name=="Replay camera"),"Recording creates no extra render camera");
                mystery.SetSpecial(killer.ActorId,true);var direction=player.transform.position+Vector3.up*.6f-killer.ViewPose.position;Physics.SyncTransforms();
                Check(runtime.RequestPunch(killer.gameObject,killer.ViewPose.position,direction.normalized,4),"Murderer performs actual lethal strike");yield return new WaitForSeconds(.6f);
                var cue=((IMysteryReplaySource)runtime).ReplayCue;Check(cue!=null&&!cue.roundEnd&&cue.victim==runtime.LocalActorId&&cue.murderer==killer.ActorId,"Victim receives private murderer kill-cam cue");
                Check((runtime.Frame.mysteryReplay?.sequence??0)==0&&(JsonUtility.FromJson<MysteryPrivateFrame>(runtime.CaptureSecret(sheriff.ActorId)).replay?.sequence??0)==0,"Living sheriff / public snapshot cannot read victim replay cue");
                Check(replay.Playing,"Actual innocent death starts local replay playback");
                Check(session.ActiveCamera.cullingMask==(1<<5),"Live camera skips rendering the world hidden behind replay UI");
                var stage=GameObject.Find("Visual-only historical replay");var stages=Resources.FindObjectsOfTypeAll<GameObject>().Where(g=>g.name=="Visual-only historical replay"&&g.scene.IsValid()).ToArray();Check(stages.Length==1&&stages[0].GetComponentsInChildren<Collider>(true).Length==0&&stages[0].GetComponentsInChildren<Rigidbody>(true).Length==0,"Playback copies contain no collision or physics");
                var raw=Object.FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsSortMode.None).First(x=>x.name=="Murderer");Check(raw.texture is RenderTexture rt&&rt.IsCreated(),"Murderer perspective renders into a real replay texture");Capture((RenderTexture)raw.texture,"Mystery-Killcam.png");
                Check(JsonUtility.FromJson<MysteryPrivateFrame>(runtime.CaptureSecret(runtime.LocalActorId)).replay.murderer==killer.ActorId,"Private wire JSON preserves victim cue");
                string victimSecret=runtime.CaptureSecret(runtime.LocalActorId),observerSecret=runtime.CaptureSecret(sheriff.ActorId);
                mystery.SetSpecial(sheriff.ActorId,true);runtime.SetBotWeapon(sheriff.gameObject,true,false);yield return new WaitForSeconds(.5f);
                var aim=(killer.transform.position+Vector3.up*.6f-sheriff.ViewPose.position).normalized;
                Check(runtime.RequestPunch(sheriff.gameObject,sheriff.ViewPose.position,aim,4),"Sheriff shoots murderer through ordinary role weapon");yield return new WaitForSeconds(.8f);
                cue=((IMysteryReplaySource)runtime).ReplayCue;Check(cue!=null&&cue.roundEnd&&cue.murderer==killer.ActorId&&cue.sheriff==sheriff.ActorId,"Sheriff victory publishes two-perspective final replay");
                Check(runtime.Frame.mysteryReplay!=null&&ModeFrameValidation.Frame(runtime.Frame,world.Blocks.Count),"Final replay travels in validated public results frame");
                Check(replay.Playing&&Object.FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsSortMode.None).Any(x=>x.name=="Sheriff"),"Final replay replaces kill-cam with split screen");
                var final=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());final.phase=RoundPhase.Playing;Check(!ModeFrameValidation.Frame(final,world.Blocks.Count),"Validator rejects public role-revealing replay during live play");
                raw=Object.FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsSortMode.None).First(x=>x.name=="Sheriff");Capture((RenderTexture)raw.texture,"Mystery-SheriffReplay.png");
                var finalFrame=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
                yield return new WaitForSecondsRealtime(10.5f);Check(!replay.Playing,"Replay automatically releases its cameras and textures");
                Check(session.ActiveCamera.cullingMask==originalEyeMask,"Replay restores the original live camera culling mask");
                yield return MysteryReplayNetworkChecks.Run(liveFrame,finalFrame,victimSecret,observerSecret,Check);
            }
            finally{player.enabled=motorEnabled;controls.enabled=controlsEnabled;session.SurvivorBots.Clear();Object.Destroy(fixture);Services.World=oldWorld;}
        }
        static void Capture(RenderTexture target,string name)
        {
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=previous;
        }
        static void CaptureModel(Transform model,string name,bool head)
        {
            var renderers=model.GetComponentsInChildren<Renderer>();var layers=renderers.Select(r=>r.gameObject.layer).ToArray();foreach(var r in renderers)r.gameObject.layer=30;
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);var center=head?model.GetComponent<CandyPlayerRig>().HeadAnchor.position:bounds.center;
            var go=new GameObject("Polish model capture");var cam=go.AddComponent<Camera>();cam.enabled=false;cam.cullingMask=1<<30;cam.stereoTargetEye=StereoTargetEyeMask.None;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.04f,.06f,.1f);cam.fieldOfView=40;cam.nearClipPlane=.01f;
            cam.transform.position=center+(head?new Vector3(.1f,.015f,1.0f):new Vector3(-1,.55f,.65f).normalized*(bounds.extents.magnitude/Mathf.Sin(20*Mathf.Deg2Rad)*1.15f));cam.transform.LookAt(center);
            var rt=new RenderTexture(960,720,24);cam.targetTexture=rt;try{cam.Render();Capture(rt,name);}finally{rt.Release();Object.Destroy(rt);Object.Destroy(go);for(int i=0;i<renderers.Length;i++)renderers[i].gameObject.layer=layers[i];}
        }
    }
}
