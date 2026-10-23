using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.CandyPlayers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using InputKey=UnityEngine.InputSystem.Key;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit integration run. Scene assets are never saved or regenerated.
    [InitializeOnLoad]
    public static class ModeRuntimeGameChecks
    {
        const string Key="ModeRuntimeGameChecks";
        [Serializable] sealed class ShelterScenes { public SceneSetup[] scenes; }
        static int failures, errors;
        static string Report=>ProjectPath.Report("ModeRuntimeGame.txt");
        static ModeRuntimeGameChecks()
        {
            EditorApplication.update+=PollShelter;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    failures=errors=0;Application.logMessageReceived+=Log;
                    EditorApplication.delayCall+=()=>{var driver=new GameObject("Mode integration test driver");Object.DontDestroyOnLoad(driver);driver.AddComponent<ModeGameCheckDriver>().StartCoroutine(Guard(SessionState.GetBool(Key+"ShelterOnly",false)?ShelterCheck():Check()));};
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    if(SessionState.GetBool(Key+"Batch",false))EditorApplication.Exit(SessionState.GetInt(Key+"Failures",1)==0?0:1);
                    else {var original=SessionState.GetString(Key+"Original","");if(File.Exists(original)&&!SessionState.GetBool(Key+"ShelterOnly",false))EditorSceneManager.OpenScene(original);}
                    if(SessionState.GetBool(Key+"ShelterOnly",false))
                    {
                        var saved=JsonUtility.FromJson<ShelterScenes>(SessionState.GetString(Key+"SavedScenes",""));
                        if(saved?.scenes!=null)EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                        var choices=SessionState.GetString(Key+"SavedChoices","");
                        if(!string.IsNullOrEmpty(choices))SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(choices));
                        MatchSetup.FromLobby=SessionState.GetBool(Key+"SavedLobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"SavedLocal",false);
                        SessionState.SetBool(Key+"ShelterOnly",false);
                        bool same=saved?.scenes!=null&&EditorSceneManager.GetSceneManagerSetup().Select(s=>s.path).SequenceEqual(saved.scenes.Select(s=>s.path));
                        File.AppendAllText(Report,(same?"PASS ":"FAIL ")+"Original scene setup restored; match choices reapplied.\n");
                    }
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validate Actual Game Mode Runtime")]
        public static void Run(){SessionState.SetBool(Key+"ShelterOnly",false);Begin(false);}
        public static void RunBatch(){SessionState.SetBool(Key+"ShelterOnly",false);Begin(true);}
        static void PollShelter()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SpectatorVideoCapture.Busy)return;
            string request=ProjectPath.Report("BotShelter.request");if(!File.Exists(request))return;
            File.Delete(request);RunShelterOnly();
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Demo Bot Shelter")]
        public static void RunShelterOnly()
        {
            if(EditorSceneManager.GetSceneManagerSetup().Any(s=>string.IsNullOrEmpty(s.path))||Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save edited scenes before running bot shelter checks.");
            SessionState.SetString(Key+"SavedChoices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetString(Key+"SavedScenes",JsonUtility.ToJson(new ShelterScenes{scenes=EditorSceneManager.GetSceneManagerSetup()}));
            SessionState.SetBool(Key+"SavedLobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"SavedLocal",MatchSetup.StartLocalSession);
            SessionState.SetBool(Key+"ShelterOnly",true);Begin(false);
        }
        static IEnumerator ShelterCheck()
        {
            yield return Until(()=>Services.Session is CastlePlaySession s&&s.Modes&&s.Player!=null,"Game session starts");
            var session=(CastlePlaySession)Services.Session;
            if(session.Social!=null)session.Social.VoiceEnabled=false;
            session.SetMenu(false);session.Survivor().InputSuspended=true;
            CheckCapsuleEdges(session);
            yield return ShelterRoute(session,session.Modes);
        }
        static void CheckCapsuleEdges(CastlePlaySession session)
        {
            var motor=session.Survivor();var saved=motor.transform.position;var rotation=motor.transform.rotation;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Capsule edge grounding fixture";floor.layer=Layers.Surface;
            float scale=motor.playerScale;floor.transform.position=new Vector3(100,9,100);floor.transform.localScale=new Vector3(4*scale,2,2*scale);
            var body=floor.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            var probe=typeof(CastleSurvivor).GetMethod("ProbeGround",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            try
            {
                float y=10-(.1f*scale+motor.Capsule.skinWidth);
                motor.Teleport(new Vector3(100,y,100+scale+motor.Capsule.radius*.35f),Quaternion.identity);probe.Invoke(motor,null);
                CheckThat(motor.Grounded,"Actual capsule lip contact accepts skin-width penetration");
                motor.Teleport(new Vector3(100,y,100+scale+2),Quaternion.identity);probe.Invoke(motor,null);
                CheckThat(!motor.Grounded,"Unsupported air does not become ground");
                motor.Teleport(new Vector3(100,9,100+scale+motor.Capsule.radius*.6f),Quaternion.identity);probe.Invoke(motor,null);
                CheckThat(!motor.Grounded,"Vertical wall contact does not become ground");
            }
            finally{motor.Teleport(saved,rotation);Object.Destroy(floor);}
        }
        static void Begin(bool batch)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play Mode before running the mode fixture.");
            File.WriteAllText(Report,"Actual Game scene mode integration\n");
            SessionState.SetString(Key+"Original",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            SessionState.SetBool(Key+"Batch",batch);SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Failures",1);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void CheckThat(bool pass,string label)
        {if(!pass)failures++;File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+label+"\n");}
        static IEnumerator Guard(IEnumerator test)
        {
            var stack=new Stack<IEnumerator>();stack.Push(test);
            while(stack.Count>0)
            {
                object value=null;bool next=false;Exception error=null;
                try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){failures++;File.AppendAllText(Report,"FAIL fixture exception "+error+"\n");break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            Time.timeScale=1;CheckThat(errors==0,"No runtime errors or exceptions during actual scene checks");
            SessionState.SetInt(Key+"Failures",failures);File.AppendAllText(Report,$"COMPLETE failures={failures}; errors={errors}. No physical headset or second Fusion peer used.\n");
            EditorApplication.ExitPlaymode();
        }
        static IEnumerator Until(Func<bool> condition,string label,float timeout=8)
        {float end=Time.realtimeSinceStartup+timeout;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;if(!condition())throw new Exception("Timeout: "+label);}
        static MatchPlayerFrame Self(MatchModeRuntime runtime)=>runtime.Frame.players.FirstOrDefault(p=>p.id==runtime.LocalActorId);
        static void ClearBots(CastlePlaySession session){session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();}
        static IEnumerator Check()
        {
            yield return Until(()=>Services.Session is CastlePlaySession s&&s.Modes&&s.Player!=null,"Game session starts");
            var session=(CastlePlaySession)Services.Session;var runtime=session.Modes;var player=session.Survivor();
            ClearBots(session);session.SetMenu(false);player.InputSuspended=true;
            yield return new WaitForSeconds(.5f);
            CheckThat(runtime.Authority&&runtime.Frame.phase==RoundPhase.Playing&&Services.World.Blocks.Count>100,"Actual Game creates playable demo and physical world");
            var avatar=player.GetComponent<CandyPlayerAvatar>();avatar.RefreshNow();
            CheckThat(avatar.Rig.calibration==null,"Keyboard avatar does not receive tracked-controller calibration");
            CheckThat(Vector3.Dot(avatar.Rig.LeftPalm.forward,avatar.LastFrame.headRotation*Vector3.forward)>.8f&&Vector3.Dot(avatar.Rig.RightPalm.forward,avatar.LastFrame.headRotation*Vector3.forward)>.8f,"Both keyboard palms face forward rather than down");
            var torsoMesh=new Mesh();var headMesh=new Mesh();avatar.Rig.bodyRenderer.BakeMesh(torsoMesh);avatar.Rig.headRenderer.BakeMesh(headMesh);
            float torsoTop=float.NegativeInfinity;var weights=avatar.Rig.bodyRenderer.sharedMesh.boneWeights;var vertices=torsoMesh.vertices;
            for(int i=0;i<vertices.Length;i++)if(weights[i].boneIndex0==0&&weights[i].weight0>.99f)torsoTop=Mathf.Max(torsoTop,avatar.Rig.bodyRenderer.transform.TransformPoint(vertices[i]).y);
            float headBottom=headMesh.vertices.Min(v=>avatar.Rig.headRenderer.transform.TransformPoint(v).y);
            File.AppendAllText(Report,$"AVATAR neckTop={torsoTop} headBottom={headBottom} feet={avatar.LastFrame.feet} eyes={avatar.LastFrame.head} rigScale={avatar.Rig.transform.lossyScale}\n");
            CheckThat(headBottom<torsoTop-.025f*avatar.LastFrame.scale,"Actual skinned head overlaps torso neck instead of floating above it");
            bool connected=true;
            foreach(float pitch in new[]{-60f,60f})
            {
                var tilted=avatar.LastFrame;tilted.headRotation=Quaternion.Euler(pitch,tilted.headRotation.eulerAngles.y,0);avatar.Rig.ApplyFrame(tilted);avatar.Rig.headRenderer.BakeMesh(headMesh);
                connected&=headMesh.vertices.Min(v=>avatar.Rig.headRenderer.transform.TransformPoint(v).y)<torsoTop;
            }
            avatar.Rig.ApplyFrame(avatar.LastFrame);CheckThat(connected,"Head remains connected when looking up and down");
            CaptureAvatar(avatar.Rig,avatar.LastFrame);
            CheckHealthShader();
            Object.Destroy(torsoMesh);Object.Destroy(headMesh);
            var hud=session.GetComponent<CandyDemoHud>();
            var pageField=typeof(CandyDemoHud).GetField("page",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var pageName=typeof(CandyDemoHud).GetMethod("PageName",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(int i=0;i<8;i++)if((string)pageName.Invoke(hud,new object[]{i})=="Modes"){pageField.SetValue(hud,i);break;}
            session.SetMenu(true);yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();
            var menuImage=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(ProjectPath.Report("Repair-Modes.png"),menuImage.EncodeToPNG());Object.Destroy(menuImage);session.SetMenu(false);
            for(int i=0;i<8;i++)if((string)pageName.Invoke(hud,new object[]{i})=="Voice"){pageField.SetValue(hud,i);break;}
            session.SetMenu(true);yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();
            menuImage=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(ProjectPath.Report("Repair-Voice.png"),menuImage.EncodeToPNG());Object.Destroy(menuImage);session.SetMenu(false);
            var bot=session.SurvivorBots.SpawnOne();yield return new WaitForSeconds(.5f);
            CheckThat(bot&&runtime.Frame.players.Count(p=>p.bot)==1&&runtime.FindActor(bot.GetComponent<IModeActor>().ActorId)!=null,"Spawned population bot enters authoritative mode roster and objective actor registry");
            bot.GetComponent<IDamageable>().Damage(35,null);yield return new WaitForSeconds(.3f);
            var botPlayable=bot.GetComponent<IPlayableCharacter>();botPlayable.Place(bot.transform.position,bot.transform.rotation);
            runtime.ResumeAuthorityFromFrame();
            CheckThat(Mathf.Abs(bot.GetComponent<IDamageable>().Health-65)<.1f,"Authority resume restores recreated local bot health before fresh observation");
            ClearBots(session);yield return new WaitForSeconds(.3f);
            string name=Self(runtime).name;var actor=(IModeActor)player;
            var catalogSource=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");int savedMapCount=catalogSource.maps.Length;
            var catalog=IslandLobbyCatalog.Runtime(catalogSource);string originalMap=MatchSetup.MapId;
            bool catalogComplete=true;foreach(string family in new[]{"ctf","infection","survival","territory","koth","tickets"})
            {MatchSetup.MapId="mode-"+family;var selected=MatchSetup.SelectedMap;catalogComplete&=selected!=null&&selected.mapPrefab&&selected.recommendedPlayers>=10&&!string.IsNullOrEmpty(selected.scenePath)&&!string.IsNullOrEmpty(selected.recommendedMode);}
            MatchSetup.MapId=originalMap;
            CheckThat(catalog!=catalogSource&&catalogSource.maps.Length==savedMapCount&&catalogComplete,"Runtime-only lobby catalog exposes all six real mode prefabs and recommendations without editing asset");

            var demo=ModeDefaults.Create(GameModeKind.Demo);demo.healthEnabled=true;demo.deathEnabled=false;demo.island=IslandRule.Walkable;runtime.Configure(demo);
            yield return new WaitForSeconds(.5f);
            var healthRenderer=avatar.Rig.bodyRenderer;var healthProps=new MaterialPropertyBlock();
            player.Damage(25,null);
            yield return Until(()=>{healthRenderer.GetPropertyBlock(healthProps);return healthProps.GetFloat("_Health")<.999f;},"Health color begins draining");
            CheckThat(healthProps.GetFloat("_Health")>.75f,"Chunk damage animates through intermediate health color instead of stepping instantly");
            yield return new WaitForSeconds(.4f);healthRenderer.GetPropertyBlock(healthProps);
            CheckThat(Mathf.Abs(healthProps.GetFloat("_Health")-.75f)<.005f,"Health color quickly settles at actual health");
            var oldFeet=player.transform.position;player.Place(oldFeet+Vector3.up*2,player.transform.rotation);yield return null;yield return null;
            healthRenderer.GetPropertyBlock(healthProps);
            CheckThat(Mathf.Abs(healthProps.GetFloat("_Bottom")-player.transform.position.y)<.15f,"Health cutoff follows airborne avatar every frame");
            player.Place(oldFeet,player.transform.rotation);
            yield return null;player.Damage(1000,null);yield return new WaitForSeconds(.3f);
            CheckThat(player.Health==0&&!Self(runtime).dying&&Self(runtime).alive,"Demo health zero persists with death disabled");
            demo.deathEnabled=true;runtime.Configure(demo);yield return null;player.Damage(1,null);
            yield return Until(()=>Self(runtime).dying,"Demo death toggle enables death");
            CheckThat(actor.IsLocal&&!player.viewCamera.enabled&&runtime.OverrideCamera,"Death camera override preserves local actor identity");
            yield return Until(()=>!Self(runtime).dying&&player.Health>0,"Demo respawns after paint transition");
            CheckThat(Self(runtime).name==name&&actor.IsLocal,"Respawn preserves generated name and local control identity");

            demo.island=IslandRule.Sink;demo.sinkGraceSeconds=1.5f;demo.deathEnabled=false;runtime.Configure(demo);
            var world=Services.World;Vector3 island=world.Center;bool found=false;
            var foundation=session.Generator().foundation.GetComponent<Collider>();
            CheckThat(foundation&&foundation.enabled,"Actual map foundation collider is enabled");
            for(int i=0;i<32&&foundation;i++)
            {
                float angle=i*Mathf.PI/16;var bounds=foundation.bounds;
                Vector3 point=new Vector3(bounds.center.x+Mathf.Sin(angle)*bounds.extents.x*.94f,bounds.max.y+2,bounds.center.z+Mathf.Cos(angle)*bounds.extents.z*.94f);
                if(!foundation.Raycast(new Ray(point,Vector3.down),out var hit,4))continue;
                var feet=hit.point+Vector3.up*.04f;float radius=player.Capsule.radius;
                if(Physics.CheckCapsule(feet+Vector3.up*(radius+.03f),feet+Vector3.up*(player.StandingHeight-radius),radius,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore))continue;
                island=feet;found=true;break;
            }
            CheckThat(found,"Find capsule-clear perimeter point on actual island foundation");
            if(!found)throw new Exception("No clear actual island foundation contact to test.");
            player.Place(island,Quaternion.identity);yield return new WaitForSeconds(.45f);
            File.AppendAllText(Report,$"SINK CONTACT feet={player.transform.position} grounded={player.Grounded} surface={player.StandingSurface?.name} body={actor.StandingBody?.name} view={actor.ViewPose.position} water={world.WaterHeight(island)} phase={runtime.Frame.phase} island={runtime.Rules.island} dying={Self(runtime).dying}\n");
            yield return Until(()=>runtime.RulesFor(player.gameObject).sinking,"Real grounded island contact enters sinking");
            var sink=runtime.RulesFor(player.gameObject);
            CheckThat(sink.moveScale<.3f&&!sink.canJump&&sink.canVault&&Self(runtime).sinking,"Sinking slows movement, disables jump, permits ledge vault and is replicated in frame");
            float y=player.transform.position.y;player.Simulate(Vector2.zero,true,.02f);yield return null;
            CheckThat(player.transform.position.y<y+.12f,"Jump cannot escape sinking foundation");
            yield return Until(()=>Self(runtime).dying,"Island grace expires even in health immortal demo");
            yield return Until(()=>!Self(runtime).dying&&player.Health>0,"Island death returns demo player to tower");
            player.Place(new Vector3(world.Center.x+world.Radius*2,world.WaterHeight(world.Center)-player.StandingHeight+.15f,world.Center.z),Quaternion.identity);
            runtime.SendMessage("LateUpdate");
            CheckThat(runtime.OverrideCamera&&runtime.OverrideCamera.transform.position.y>=world.WaterHeight(world.Center),"Water crossing hides immediately between five-Hz death samples");
            yield return Until(()=>Self(runtime).dying,"Ocean camera threshold triggers drowning");
            CheckThat(runtime.OverrideCamera&&runtime.OverrideCamera.transform.position.y>=world.WaterHeight(world.Center),"Drowning view never shows underneath ocean/map");
            yield return Until(()=>!Self(runtime).dying&&player.Health>0,"Ocean demo respawn works despite health immortality");
            session.Hand.ViewCamera.enabled=false;session.SetRole(true);yield return new WaitForSeconds(.3f);
            CheckThat(session.ActiveCamera==session.Hand.ViewCamera&&session.ActiveCamera.isActiveAndEnabled&&session.ActiveCamera.targetDisplay==0,"Demo flying-hand switch restores a live Display 1 camera after death-camera use");
            CaptureCamera(session.Hand.ViewCamera,"Repair-FlyingHand.png");
            yield return DesktopPoseKeys(session);
            session.SetRole(false);yield return null;
            CheckThat(session.ActiveCamera==player.viewCamera&&player.viewCamera.isActiveAndEnabled,"Returning from flying hand restores survivor camera");
            player.Place(new Vector3(world.Center.x+world.Radius*2,world.WaterHeight(world.Center)-player.StandingHeight+.15f,world.Center.z),Quaternion.identity);
            runtime.SendMessage("LateUpdate");session.SetRole(true);yield return null;runtime.SendMessage("LateUpdate");
            CheckThat(runtime.OverrideCamera==null&&session.Hand.ViewCamera.isActiveAndEnabled,"Switching while ocean override is active cannot disable the hand camera");
            session.SetRole(false);yield return new WaitForSeconds(.3f);

            var control=ModeDefaults.Create(GameModeKind.BlockControl);control.countdownSeconds=.6f;control.automaticDisasters=false;control.claimSeconds=.4f;control.claimDecaySeconds=30;runtime.Configure(control);ClearBots(session);
            yield return new WaitForSeconds(.3f);runtime.StartRound();ClearBots(session);
            CheckThat(runtime.Frame.phase==RoundPhase.Countdown&&!runtime.RulesFor(player.gameObject).canJump&&runtime.RulesFor(player.gameObject).moveScale==0,"Competitive countdown freezes movement, jump and climbing");
            yield return Until(()=>runtime.Frame.phase==RoundPhase.Playing,"Competitive countdown completes");
            CheckThat(Enumerable.Range(0,8).Any(i=>(string)pageName.Invoke(hud,new object[]{i})=="Disasters")&&DemoDisasters.CanTrigger(session),"Host retains usable Disasters page during competitive game modes");
            var block=Services.World.Blocks.OrderByDescending(b=>b.position.y).First();var blockCollider=block.GetComponent<Collider>();
            CheckThat(Services.World.Blocks.All(b=>b.GetComponent<IBlockOwnership>()?.OwnerTeam==-1),"Territory round begins with every physical block neutral");
            player.Place(new Vector3(blockCollider.bounds.center.x,blockCollider.bounds.max.y+.04f,blockCollider.bounds.center.z),Quaternion.identity);
            yield return Until(()=>runtime.Frame.claims.Any(c=>c.owner==actor.Team),"Actual grounded player claims block",6);
            var claim=runtime.Frame.claims.First(c=>c.owner==actor.Team);float elapsed=runtime.Frame.elapsed;
            control.claimSeconds=2;runtime.Configure(control);yield return new WaitForSeconds(.3f);
            CheckThat(runtime.Frame.phase==RoundPhase.Playing&&runtime.Frame.elapsed>=elapsed&&runtime.Frame.claims.Any(c=>c.block==claim.block&&c.owner==actor.Team),"Live slider changes preserve round time and captured ownership");
            Vector3 initial=Services.World.Blocks[0].position;Services.World.Blocks[0].position+=Vector3.right*7;runtime.StartRound();ClearBots(session);yield return new WaitForSeconds(.1f);
            CheckThat(Vector3.Distance(Services.World.Blocks[0].position,initial)<.5f,"Next competitive round rebuilds actual displaced map geometry");

            var survival=ModeDefaults.Create(GameModeKind.Survival);survival.automaticDisasters=false;survival.countdownSeconds=0;survival.roundSeconds=10;survival.victorySeconds=.3f;runtime.Configure(survival);ClearBots(session);runtime.StartRound();ClearBots(session);
            var peerA=MakePeer(session,"fixture-peer-a",Services.World.SafeSpawnPoint()+Vector3.right*2);
            var peerB=MakePeer(session,"fixture-peer-b",Services.World.SafeSpawnPoint()+Vector3.left*2);
            runtime.RegisterRemote("fixture-peer-a",peerA.gameObject);runtime.RegisterRemote("fixture-peer-b",peerB.gameObject);
            yield return new WaitForSeconds(.3f);
            CheckThat(Vector3.Distance(peerA.transform.position,peerA.NetworkPosition)<.01f,"Remote actor root position follows serialized network feet");
            runtime.RecordRemoteDamage("fixture-peer-a",25);yield return new WaitForSeconds(.3f);
            CheckThat(Mathf.Abs(runtime.Frame.players.First(p=>p.id=="fixture-peer-a").health-.75f)<.01f,"Remote proxy damage is tracked by authoritative health rather than constant proxy Health");
            var promotion=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());runtime.RecordRemoteDamage("fixture-peer-a",20);
            typeof(MatchModeRuntime).GetMethod("RestoreAuthorityState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(runtime,new object[]{promotion});
            runtime.RecordRemoteDamage("fixture-peer-a",10);yield return new WaitForSeconds(.3f);
            CheckThat(Mathf.Abs(runtime.Frame.players.First(p=>p.id=="fixture-peer-a").health-.65f)<.01f,"Actual authority restoration seeds remote health before subsequent damage");

            player.Eliminate(null);yield return Until(()=>Self(runtime).role=="spectator"&&!Self(runtime).dying,"Eliminated survivor enters spectator");
            yield return new WaitForSeconds(.3f);string before=Self(runtime).spectating;
            var driver=Object.FindFirstObjectByType<ModeGameCheckDriver>();driver.ForceGameplayInput=true;yield return null;
            var inputSettings=InputSystem.settings;var previousRouting=inputSettings.editorInputBehaviorInPlayMode;var previousBackground=inputSettings.backgroundBehavior;
            inputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;inputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var previousKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.EnableDevice(keyboard);keyboard.MakeCurrent();
            try
            {
            InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.RightArrow));
            yield return new WaitForSeconds(.1f);
            File.AppendAllText(Report,$"KEY INPUT temporaryDevice=true enabled={keyboard.enabled} updateSawPress={driver.SawSpectatorKey} role={Self(runtime).role}\n");
            InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return new WaitForSeconds(.3f);
            }
            finally {InputSystem.RemoveDevice(keyboard);if(previousKeyboard!=null)previousKeyboard.MakeCurrent();inputSettings.editorInputBehaviorInPlayMode=previousRouting;inputSettings.backgroundBehavior=previousBackground;}
            driver.ForceGameplayInput=false;
            File.AppendAllText(Report,$"SPECTATOR INPUT before={before} after={Self(runtime).spectating} menu={session.MenuOpen} vr={session.IsVR} alivePeers={string.Join(",",runtime.Frame.players.Where(p=>p.alive).Select(p=>p.id))}\n");
            CheckThat(Self(runtime).spectating!=before&&Self(runtime).spectating!=null,"Desktop spectator arrow changes watched living player");
            CheckThat(runtime.OverrideCamera&&actor.IsLocal,"Spectator overrides view while retaining local actor identity");
            var watched=runtime.FindActor(Self(runtime).spectating);var watchedView=watched as SurvivorNetworkView;
            if(watchedView)
            {
                Vector3 previousView=runtime.OverrideCamera.transform.position;
                Vector3 moved=watchedView.NetworkPosition+Vector3.right*.7f;WritePeer(watchedView,moved);watchedView.Render(.016f);yield return null;
                CheckThat(Vector3.Distance(runtime.OverrideCamera.transform.position,previousView)>.05f&&Vector3.Distance(runtime.OverrideCamera.transform.position,watchedView.ViewPose.position)<.03f,"Spectator follows live remote view between five-Hz match samples");
            }
            yield return Until(()=>runtime.Frame.phase==RoundPhase.Results,"Round advances through victory to results",15);
            CheckThat(session.LatestReport!=null&&Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(b=>b&&b.GetType().Name=="CandyResultsHud"),"Completed actual round exposes report and results presentation");
            CheckThat(!runtime.RulesFor(player.gameObject).punchEnabled&&runtime.RulesFor(player.gameObject).moveScale==0,"Results prevent continued combat and movement");
            var report=session.LatestReport;var row=report.Rows.First(r=>r.ParticipantId==runtime.LocalActorId);int survivalColumn=report.Columns.ToList().FindIndex(c=>c.Id=="alive_seconds");
            CheckThat(Math.Abs(report.DurationSeconds-runtime.Frame.elapsed)<.01&&survivalColumn>=0&&Math.Abs(row.Values[survivalColumn].Value-Self(runtime).survivalSeconds)<.01&&report.Description.Contains("Host changed"),"Migrated results preserve full authoritative duration and survival totals with sampled-history disclosure");
            CheckClientBotBinding(session,runtime);
            MatchSetup.MapId="mode-ctf";MatchSetup.ApplyMap();yield return new WaitForSeconds(.2f);
            CheckThat(Services.World!=world&&Services.World.Blocks.Count>400&&Services.World.PlayableBounds.size.x>60,"Selected mode map prefab actually replaces world through MatchSetup");
            MatchSetup.MapId=originalMap;

            Object.Destroy(peerA.gameObject);Object.Destroy(peerB.gameObject);
            yield return ShelterRoute(session,runtime);
            runtime.ReturnToLobby();yield return Until(()=>Services.Session is IslandLobby,"Results return-to-lobby scene flow");
            CheckThat(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Lobby","Return to lobby loads actual starting scene");
        }
        static IEnumerator DesktopPoseKeys(CastlePlaySession session)
        {
            var hand=session.HandActor();var bridge=hand.GetComponent<DeadlyJenga.CandyPhysicsHands.CandyFlyingHandBridge>();
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            var lockState=Cursor.lockState;bool enabled=session.enabled;
            session.SetMenu(false);session.enabled=false;Cursor.lockState=CursorLockMode.None;
            hand.Place(Services.World.Center+Vector3.right*(Services.World.Radius+25)+Vector3.up*25,Quaternion.identity);
            try
            {
                yield return new WaitForSeconds(.7f);
                bridge.SelectDesktopPose(DeadlyJenga.CandyPhysicsHands.DesktopHandPose.Spread);
                var keys=new[]{InputKey.Digit1,InputKey.Digit2,InputKey.Digit3,InputKey.Digit4,InputKey.Digit5,InputKey.Digit6,InputKey.Numpad1,InputKey.Numpad2,InputKey.Numpad3,InputKey.Numpad4,InputKey.Numpad5,InputKey.Numpad6};
                foreach(var key in keys)
                {
                    int index=Array.IndexOf(keys,key)%6;var expected=(DeadlyJenga.CandyPhysicsHands.DesktopHandPose)index;
                    InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(key));yield return null;yield return null;
                    CheckThat(bridge.DesktopPose==expected,"Actual flying hand accepts "+key+" as "+expected+" with session-owned cursor");
                    InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return new WaitForSeconds(.8f);
                    var skeleton=DeadlyJenga.CandyPhysicsHands.DesktopHandPoses.Build(bridge.useLeftHand,expected);
                    float error=0;for(int j=0;j<20;j++)error=Mathf.Max(error,Vector3.Distance(bridge.PhysicalHand.Target.skeleton.Joint(j),skeleton.Joint(j)));
                    CheckThat(error<.008f,"Selected "+expected+" reaches the physical hand target through smooth transition");
                    var physical=bridge.PhysicalHand;float angle=0;
                    for(int f=0;f<5;f++)for(int j=0;j<3;j++)
                    {
                        int joint=f*4+j;var rotation=DeadlyJenga.CandyPhysicsHands.CandyPhysicalHand.SegmentRotation(physical.Target.rotation*(skeleton.Joint(joint+1)-skeleton.Joint(joint)),physical.Target.rotation*Vector3.up);
                        angle=Mathf.Max(angle,Quaternion.Angle(physical.bodies[1+f*3+j].rotation,rotation));
                    }
                    File.AppendAllText(Report,$"PHYSICAL {key} maximum finger angle error={angle:F1} interactive={physical.IsInteractive}\n");
                    CheckThat(physical.IsInteractive&&angle<45,"Actual physical fingers follow "+expected+" rather than only updating its input value");
                    if(index==2||index==3)
                    {
                        var cameraRoot=new GameObject("Side view of keyboard pose");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=50;
                        var pose=physical.Target;camera.transform.position=pose.position+pose.rotation*new Vector3(.22f,.16f,-.02f)*bridge.settings.size;camera.transform.LookAt(pose.position+pose.rotation*Vector3.forward*.07f*bridge.settings.size);
                        CaptureCamera(camera,"Repair-Keys-"+key+".png");Object.Destroy(cameraRoot);
                    }
                }
                session.SetMenu(true);var before=bridge.DesktopPose;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(InputKey.Digit3));yield return null;yield return null;
                CheckThat(bridge.DesktopPose==before,"Typing numbers while menu is open cannot change the hand pose");
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());session.SetMenu(false);yield return null;
                InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));yield return new WaitForSeconds(.3f);
                CheckThat(bridge.DesktopClench>.9f,"Session-owned cursor also permits click-to-clench");
                InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState());yield return new WaitForSeconds(.3f);
                CheckThat(bridge.DesktopClench<.1f,"Releasing mouse unclenches the selected pose");
            }
            finally{InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);session.enabled=enabled;Cursor.lockState=lockState;session.SetMenu(false);}
        }
        static IEnumerator ShelterRoute(CastlePlaySession session,MatchModeRuntime runtime)
        {
            ClearBots(session);var originalWorld=Services.World;var fixture=new GameObject("Actual bot shelter route");fixture.transform.position=new Vector3(1000,0,1000);
            var fake=fixture.AddComponent<DeadlyJenga.Core.Testing.FakeWorldQuery>();fake.waterHeight=-20;fake.radius=10;fake.topHeight=.25f;fake.spawnPoint=fixture.transform.position+Vector3.left*3+Vector3.up*.31f;
            for(int i=0;i<2;i++)
            {
                var tile=GameObject.CreatePrimitive(PrimitiveType.Cube);tile.transform.SetParent(fixture.transform);tile.transform.localPosition=new Vector3(i==0?-3:3,i==0?0:-.05f,0);tile.transform.localScale=new Vector3(6,.5f,6);tile.layer=Layers.Surface;
                var body=tile.AddComponent<Rigidbody>();body.isKinematic=true;tile.AddComponent<ModeShelterFloor>();fake.blocks.Add(body);
            }
            var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.transform.SetParent(fixture.transform);roof.transform.localPosition=new Vector3(3,3,0);roof.transform.localScale=new Vector3(5.5f,.5f,5.5f);roof.layer=Layers.Block;
            var rain=new ModeShelterRain();DisasterStates.Active.Add(rain);
            try
            {
                var rules=ModeSimulation.Defaults(GameModeKind.Demo);rules.island=IslandRule.Walkable;runtime.Configure(rules);session.SurvivorBots.AutoFill=false;Physics.SyncTransforms();yield return new WaitForSeconds(.5f);
                var bot=session.SurvivorBots.SpawnOne();CheckThat(bot,"Shelter route spawns an ordinary bot on the uncovered initial platform");
                if(bot)
                {
                    var actor=bot.GetComponent<IModeActor>();CheckThat(!fake.IsSheltered(actor.ViewPose.position),"Shelter-route bot begins genuinely exposed to the sky");
                    float end=Time.time+25;bool choseShelter=false;
                    while(Time.time<end&&!fake.IsSheltered(actor.ViewPose.position))
                    {
                        var goals=new List<BotObjective>();runtime.Collect(bot,goals);choseShelter|=goals.Any(g=>g.kind==BotObjectiveKind.Guard&&fake.IsSheltered(g.position+actor.ViewPose.position-actor.transform.position));yield return null;
                    }
                    CheckThat(choseShelter&&fake.IsSheltered(actor.ViewPose.position)&&bot.transform.position.x>fixture.transform.position.x,"Ordinary bot follows a real route into standing-clear acid-rain shelter without test movement or damage");
                }
            }
            finally{DisasterStates.Active.Remove(rain);ClearBots(session);Services.World=originalWorld;Object.Destroy(fixture);}
        }
        static void CaptureAvatar(CandyPlayerRig rig,CandyAvatarFrame frame)
        {
            var parts=rig.GetComponentsInChildren<Renderer>();var layers=parts.Select(r=>r.gameObject.layer).ToArray();
            var root=new GameObject("Avatar regression camera");var camera=root.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.1f,.19f);camera.fieldOfView=40;
            camera.transform.position=frame.feet+new Vector3(2.4f,1.55f,-2.8f)*frame.scale;camera.transform.LookAt(frame.feet+Vector3.up*.95f*frame.scale);
            for(int i=0;i<parts.Length;i++)parts[i].gameObject.layer=31;
            try{CaptureCamera(camera,"Repair-Avatar.png");}finally{for(int i=0;i<parts.Length;i++)parts[i].gameObject.layer=layers[i];Object.Destroy(root);}
        }
        static void CaptureCamera(Camera camera,string name)
        {
            var target=RenderTexture.GetTemporary(960,600,24);var old=RenderTexture.active;var previous=camera.targetTexture;
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;var image=new Texture2D(960,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,600),0,0);image.Apply();
            File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());camera.targetTexture=previous;RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);Object.Destroy(image);
        }
        static void CheckHealthShader()
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.layer=31;cube.transform.position=new Vector3(4000,10,4000);
            var material=new Material(Resources.Load<Shader>("CandyScenery/CandyAvatarHealth"));material.SetColor("_BaseColor",new Color(1,.02f,.02f));material.SetFloat("_Bottom",0);material.SetFloat("_Top",2);cube.GetComponent<Renderer>().sharedMaterial=material;
            var root=new GameObject("Airborne health pixel camera");var camera=root.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.transform.position=cube.transform.position+Vector3.back*3;camera.transform.LookAt(cube.transform);
            var target=RenderTexture.GetTemporary(64,64,24);var old=RenderTexture.active;var image=new Texture2D(64,64,TextureFormat.RGB24,false);camera.targetTexture=target;
            try
            {
                material.SetFloat("_Health",1);camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();var full=image.GetPixel(32,32);
                material.SetFloat("_Health",0);camera.Render();image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();var empty=image.GetPixel(32,32);
                CheckThat(empty.g>full.g+.15f&&full.r>full.g+.15f,"Rendered full-health avatar stays colored above cutoff; pale layer appears only after damage");
            }
            finally{RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);Object.Destroy(image);Object.Destroy(root);Object.Destroy(cube);Object.Destroy(material);}
        }
        sealed class ModeShelterRain:IDisasterState
        {public DisasterFrame Capture()=>new DisasterFrame{kind="AcidRain",warning=0,remaining=60};}
        static void CheckClientBotBinding(CastlePlaySession session,MatchModeRuntime runtime)
        {
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var property=typeof(CastlePlaySession).GetField("<Network>k__BackingField",flags);var previous=property.GetValue(session);
            var network=session.gameObject.AddComponent<CastleLanSession>();network.enabled=false;
            var view=MakePeer(session,"bot:fixture-client",Services.World.SafeSpawnPoint()+Vector3.right*3);
            var views=(Dictionary<string,INetworkActorView>)typeof(CastleLanSession).GetField("views",flags).GetValue(network);
            views["bot:fixture-client"]=view;
            typeof(CastleLanSession).GetField("session",flags).SetValue(network,session);
            typeof(CastleLanSession).GetField("id",flags).SetValue(network,runtime.LocalActorId);
            typeof(CastleLanSession).GetField("fusionMode",flags).SetValue(network,true);
            typeof(CastleLanSession).GetField("fusionClient",flags).SetValue(network,true);
            try
            {
                property.SetValue(session,network);
                var state=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());state.revision++;
                var rows=state.players.ToList();rows.Add(new MatchPlayerFrame{id="bot:fixture-client",name="CyanFixtureBot",bot=true,alive=true,color=Color.cyan,position=view.NetworkPosition,viewPosition=view.ViewPose.position,viewRotation=Quaternion.identity});state.players=rows.ToArray();
                bool accepted=runtime.ReceiveFrameJson(JsonUtility.ToJson(state));var bound=runtime.FindActor("bot:fixture-client");
                CheckThat(accepted&&bound!=null&&bound.DisplayName=="CyanFixtureBot"&&bound.Visual!=null,"Client mode frame binds remote bot proxy for nameplates, team and health presentation");
            }
            finally{property.SetValue(session,previous);views.Clear();Object.Destroy(view.gameObject);Object.Destroy(network);}
        }
        static SurvivorNetworkView MakePeer(CastlePlaySession session,string id,Vector3 feet)
        {
            var root=new GameObject(id);var view=root.AddComponent<SurvivorNetworkView>().Init(new NetworkContext{host=true,actorId=id,localSurvivor=session.Player.gameObject,localHand=session.Hand.gameObject,sendImpact=(who,push,damage)=>session.Modes.RecordRemoteDamage(who,damage)});
            WritePeer(view,feet);return view;
        }
        static void WritePeer(SurvivorNetworkView view,Vector3 feet)
        {
            var pose=new CandyAvatarFrame{feet=feet,head=feet+Vector3.up*1.5f,headRotation=Quaternion.identity,scale=1,left=new CandyHandPose{rotation=Quaternion.identity},right=new CandyHandPose{rotation=Quaternion.identity}};
            using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))CharacterWire.WriteSurvivor(writer,pose,0);stream.Position=0;using(var reader=new BinaryReader(stream))view.ReadState(reader,true);}
        }
    }
    public sealed class ModeShelterFloor:MonoBehaviour,IClimbable
    {
        public bool CanClimb=>true;public float Grip=>1;public Transform Anchor=>transform;public Rigidbody Body=>GetComponent<Rigidbody>();public Transform Visual=>transform;
    }
    [DefaultExecutionOrder(225)]
    public sealed class ModeGameCheckDriver : MonoBehaviour
    {
        public bool ForceGameplayInput, SawSpectatorKey;
        // Batch mode cannot hold a native cursor lock. Run the input case as if the Game view had focus.
        void Update(){if(ForceGameplayInput&&Services.Session is CastlePlaySession session){session.SetMenu(false);if(Keyboard.current!=null&&Keyboard.current.rightArrowKey.wasPressedThisFrame)SawSpectatorKey=true;}}
    }
}
