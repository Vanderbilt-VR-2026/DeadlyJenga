using System;
using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class PowerupBotChecks
    {
        const string Key="PowerupBotChecks";
        [Serializable] sealed class Setup{public SceneSetup[] scenes;}
        sealed class Source:IEquipmentPickupSource{public EquipmentKind Kind{get;set;}public Vector3 Position{get;set;}public float Duration=>20;}
        sealed class Goal:IBotObjectives
        {
            public GameObject actor,target;public BotObjectiveKind kind=BotObjectiveKind.Attack;public Vector3 point;
            public void Collect(GameObject who,List<BotObjective> results){if(who==actor)results.Add(new BotObjective{id="weapon-fixture",kind=kind,subject=kind==BotObjectiveKind.Attack?target:null,position=kind==BotObjectiveKind.Attack?target.transform.position:point,radius=1,priority=2,expiresAt=Time.time+1});}
        }
        static bool failed;static int errors,checks;
        public static bool TransportActive{get;set;}
        static string Report=>ProjectPath.Report("PowerupBots.txt");
        static PowerupBotChecks()
        {
            EditorApplication.update+=()=>
            {
                if(!SessionState.GetBool(Key+"Combat",false)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return;
                string path=ProjectPath.Report("BotCombat.txt");if(!File.Exists(path))return;string report=File.ReadAllText(path);
                if(!report.Contains("COMPLETE failures="))return;
                SessionState.SetBool(Key+"Combat",false);
                if(Application.isBatchMode)EditorApplication.Exit(report.Contains("COMPLETE failures=0 runtimeErrors=0.")?0:1);
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {SessionState.SetBool(Key+"Entered",true);AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();errors=checks=0;failed=false;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>new GameObject("Powerup bot checks").AddComponent<PowerupBotCheckDriver>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Entered",false))
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    var setup=JsonUtility.FromJson<Setup>(SessionState.GetString(Key+"Scenes",""));if(setup.scenes.Any(s=>s.isActive&&s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                    File.AppendAllText(Report,$"COMPLETE checks={checks} errors={errors} failed={failed}. Native fixture surfaces and actual Game modes; no Quest test.\n");
                    if(Application.isBatchMode)EditorApplication.Exit(failed||errors>0||checks<20?1:0);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Random Powerups and Bot Guns")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();if(scenes.Any(s=>s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            BotObjectiveContractChecks.Run();if(!BotObjectiveContractChecks.LastPassed)throw new InvalidOperationException("Bot contracts failed.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Setup{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key+"Entered",false);SessionState.SetBool(Key,true);
            File.WriteAllText(Report,"Random spawning, native support/expiry, real bot collection, ordinary weapon skill fixtures and production Mystery witness acceptance.\n");
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        public static void RunCombatRegression()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Combat checks require saved, idle scenes.");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            typeof(BotCombatChecks).GetMethod("Begin",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{new[]{"approach-face-hit","mystery-murder","ctf-contact","mystery-unknown-contact","mystery-repel"}});
            SessionState.SetBool(Key+"Combat",true);
        }
        static void Log(string message,string trace,LogType type)
        {
            if(TransportActive&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.",StringComparison.Ordinal)){File.AppendAllText(Report,"FIXTURE NOTE Fusion multiple-peer empty-scene diagnostic; transport assertions remain enabled.\n");return;}
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}
        }
        static void Check(bool ok,string label){File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+label+"\n");if(!ok)throw new Exception(label);checks++;}
        static IEnumerator Until(Func<bool> condition,string label,float timeout=8,Func<string> diagnostic=null)
        {float until=Time.realtimeSinceStartup+timeout;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;bool passed=condition();Check(passed,label+(!passed&&diagnostic!=null?" · "+diagnostic():""));}
        static IEnumerator Guard()
        {
            foreach(var test in new[]{Test(),AimTest(),ModelGallery()})
            {
            var stack=new Stack<IEnumerator>();stack.Push(test);
            while(stack.Count>0){bool more=false;object value=null;Exception error=null;try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}if(error!=null){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;}
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            }
            EditorApplication.ExitPlaymode();
        }
        static Rigidbody Block(Vector3 position,Vector3 size)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Native powerup fixture block";go.layer=Layers.Surface;go.transform.position=position;go.transform.localScale=size;var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;go.AddComponent<BotCombatClimbSurface>();return body;
        }
        static void Grant(MatchModeRuntime runtime,CastleSurvivor bot,EquipmentKind kind)
        {
            int id=runtime.RegisterPickup(new Source{Kind=kind,Position=bot.transform.position+Vector3.forward*.65f+Vector3.up*.2f});
            runtime.ReceiveCommand(bot.ActorId,"equipment:"+JsonUtility.ToJson(new MatchModeRuntime.EquipmentCommand{action="pickup",pickup=id,swap=true}));Check(runtime.EquipmentFor(bot.ActorId).kind==kind,"Actual bot pickup grants "+kind);
        }
        static IEnumerator Test()
        {
            yield return Until(()=>Services.Session is CastlePlaySession s&&s.Modes!=null,"Actual Game starts",30);
            var session=(CastlePlaySession)Services.Session;session.SetMenu(false);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            var runtime=session.Modes;var settings=runtime.SpawnSettings;settings.enabled=false;runtime.ConfigurePowerupSpawns(settings);
            var bad=runtime.SpawnSettings;bad.interval=float.NaN;Check(!runtime.ConfigurePowerupSpawns(bad)&&float.IsFinite(runtime.SpawnSettings.interval),"Nonfinite configuration is rejected without mutation");
            bad=runtime.SpawnSettings;bad.types=new[]{new PowerupSpawnWeight{kind=EquipmentKind.Blaster,weight=1},new PowerupSpawnWeight{kind=EquipmentKind.Blaster,weight=2}};Check(!runtime.ConfigurePowerupSpawns(bad),"Duplicate item weights are rejected");
            int interested=Enumerable.Range(0,1000).Count(i=>BotEquipmentValue.Interested("bot-"+i,37,3,5));Check(interested>100&&interested<950,"Notice policy has varied, non-universal interest across actors");Check(BotEquipmentValue.NoticeRoll("bot-a",37)==BotEquipmentValue.NoticeRoll("bot-a",37),"Interest does not reroll every frame");
            Check(!BotGunPolicy.Shotgun(EquipmentKind.DualRifle,false,20)&&BotGunPolicy.Shotgun(EquipmentKind.DualRifle,false,6)&&BotGunPolicy.Shotgun(EquipmentKind.DualRifle,true,11),"Range policy uses long rifle at range and hysteresis near mode boundaries");
            var originalWorld=Services.World;var root=new GameObject("Powerup surface fixture");var world=root.AddComponent<FakeWorldQuery>();world.waterHeight=-100;world.spawnPoint=new Vector3(150,10.05f,150);world.topHeight=30;world.radius=250;world.playableBounds=new Bounds(new Vector3(220,20,150),new Vector3(250,80,100));
            world.blocks.Add(Block(new Vector3(150,9.5f,150),new Vector3(100,1,80)));world.blocks.Add(Block(new Vector3(220,19.5f,150),new Vector3(20,1,20)));world.blocks.Add(Block(new Vector3(290,29.5f,150),new Vector3(20,1,20)));
            foreach(var body in world.blocks)body.transform.SetParent(root.transform,true);
            var proxy=new GameObject("Grouped logical-block surface fixture");proxy.layer=Layers.Surface;proxy.transform.SetParent(world.blocks[2].transform,false);var proxyBody=proxy.AddComponent<Rigidbody>();proxyBody.isKinematic=true;proxyBody.useGravity=false;
            var proxyShape=proxy.AddComponent<BoxCollider>();Climbables.Register(proxyShape,world.blocks[2].GetComponent<BotCombatClimbSurface>());world.blocks[2].detectCollisions=false;
            var originalProvider=BotObjectives.Current;
            try
            {
                var rules=ModeSimulation.Defaults(GameModeKind.Demo);rules.countdownSeconds=0;rules.island=IslandRule.Walkable;rules.healthEnabled=true;rules.punchEnabled=true;runtime.Configure(rules);runtime.StartRound();session.Survivor().Place(new Vector3(150,10.05f,120),Quaternion.identity);session.Survivor().InputSuspended=true;yield return new WaitForSeconds(.4f);
                for(int tier=0;tier<3;tier++)
                {
                    for(int i=0;i<3;i++)world.blocks[i].gameObject.SetActive(i==tier);Physics.SyncTransforms();
                    settings=runtime.SpawnSettings;settings.enabled=true;settings.interval=1;settings.jitter=0;settings.maximum=1;settings.lifetime=5;settings.types=new[]{new PowerupSpawnWeight{kind=tier==1?EquipmentKind.DualRifle:EquipmentKind.Speed,weight=100}};runtime.ConfigurePowerupSpawns(settings);
                    int captureTier=tier;yield return Until(()=>runtime.Frame.pickups.Any(p=>p.randomSpawn&&Mathf.Abs(p.position.y-(10+captureTier*10+.3f))<.2f),"Random spawner accepts exposed tier "+tier,5);
                    Check(runtime.RandomPickupCount<=1,"Active random pickup cap is respected");
                    if(tier==1)Check(runtime.Frame.pickups.Any(p=>p.randomSpawn&&p.kind==EquipmentKind.DualRifle),"100% rifle weight produces only the chosen type");
                }
                var anchored=runtime.Frame.pickups.First(p=>p.randomSpawn);var supportBefore=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());Vector3 position=anchored.position;world.blocks[2].position+=Vector3.right*2;Physics.SyncTransforms();yield return new WaitForSeconds(.3f);
                Check(runtime.Frame.pickups.Any(p=>p.id==anchored.id&&Mathf.Abs(p.position.x-position.x-2)<.1f),"Pickup follows its actual moving support");
                var supportAfter=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
                settings=runtime.SpawnSettings;settings.enabled=false;runtime.ConfigurePowerupSpawns(settings);yield return new WaitForSeconds(5.2f);Check(!runtime.Frame.pickups.Any(p=>p.id==anchored.id),"Existing random pickups expire even with spawning disabled");
                var expired=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
                settings.enabled=true;settings.types[0].weight=0;runtime.ConfigurePowerupSpawns(settings);yield return new WaitForSeconds(1.25f);Check(runtime.RandomPickupCount==0,"Zero total weight pauses spawning");settings.enabled=false;runtime.ConfigurePowerupSpawns(settings);
                world.blocks[0].gameObject.SetActive(true);world.blocks[1].gameObject.SetActive(false);world.blocks[2].gameObject.SetActive(false);Physics.SyncTransforms();runtime.StartRound();
                var spawned=session.SurvivorBots.SpawnOne();Check(spawned,"Collector spawns on a walkable fixture surface: "+session.SurvivorBots.Status);var collector=spawned.GetComponent<CastleSurvivor>();yield return new WaitForSeconds(.4f);collector.Place(new Vector3(150,10.05f,150),Quaternion.identity);collector.InputSuspended=false;session.Survivor().Place(new Vector3(170,10.05f,125),Quaternion.identity);session.Survivor().InputSuspended=true;
                int selected=0;
                for(int i=0;i<12;i++){var point=collector.transform.position+Vector3.forward*2.5f+Vector3.up*.3f;float distance=Vector3.Distance(point,collector.transform.position);int id=runtime.RegisterPickup(new Source{Kind=EquipmentKind.DualRifle,Position=point});if(BotEquipmentValue.Interested(collector.ActorId,id,BotEquipmentValue.Desire(EquipmentKind.None,EquipmentKind.DualRifle,distance,.3f),distance)){selected=id;break;}}
                Check(selected>0,"Fixture includes a noticed valuable pickup");
                yield return Until(()=>runtime.EquipmentFor(collector.ActorId).kind==EquipmentKind.DualRifle,"Production director and ordinary movement collect a real pickup",14,()=>collector.GetComponent<CastleBotBrain>().ObjectiveId+" / "+collector.GetComponent<CastleBotBrain>().StationaryReason+" at "+collector.transform.position);
                collector.InputSuspended=true;yield return new WaitForSeconds(.6f);Check(runtime.ObjectiveDirector.History.Any(h=>h.actor==collector.ActorId&&h.objective.StartsWith("collect-equipment/")&&h.outcome==BotAssignmentOutcome.Succeeded),"Director records actual collection as success rather than arrival");
                Check(!runtime.SetBotWeapon(session.Player.gameObject,true,false),"Bot equipment API rejects a human actor");
                Grant(runtime,collector,EquipmentKind.Speed);yield return null;Check(collector.PowerupSpeedMultiplier==2,"Collected speed powerup changes the actual bot movement multiplier");
                Grant(runtime,collector,EquipmentKind.Jump);yield return null;Check(Mathf.Approximately(collector.EffectiveJumpHeight,collector.jumpHeight*3),"Collected jump powerup changes the actual bot jump height");
                var victim=session.SurvivorBots.SpawnOne().GetComponent<CastleSurvivor>();victim.GetComponent<CastleBotBrain>().enabled=false;victim.InputDriver=null;yield return new WaitForSeconds(.4f);
                var brain=collector.GetComponent<CastleBotBrain>();var goal=new Goal{actor=collector.gameObject,target=victim.gameObject};BotObjectives.Current=goal;
                foreach(var kind in Enum.GetValues(typeof(EquipmentKind)).Cast<EquipmentKind>().Where(k=>k>=EquipmentKind.Blaster&&!EquipmentSpecifications.Get(k).power))
                {
                    collector.InputSuspended=true;yield return Until(()=>runtime.Frame.blaster.shots.Length==0&&runtime.Frame.weaponProjectiles.Length==0,"Previous projectiles drain before testing "+kind,8);
                    collector.Place(new Vector3(150,10.05f,150),Quaternion.identity);collector.InputSuspended=true;float distance=BotGunPolicy.IsGun(kind)?kind==EquipmentKind.Blaster||kind==EquipmentKind.PoweredArmor?10:20:1.1f;
                    victim.Place(collector.transform.position+Vector3.forward*distance,Quaternion.identity);victim.InputSuspended=true;Physics.SyncTransforms();Grant(runtime,collector,kind);yield return new WaitForSeconds(.25f);if(kind==EquipmentKind.GrenadeLauncher){var controls=collector.GetComponent<CandyEquipment>();var probe=new Pose(collector.ViewPose.position,Quaternion.Euler(-20,0,0));controls.SetBotAim(probe);Check(Vector3.Dot(controls.MuzzlePose.rotation*Vector3.forward,probe.rotation*Vector3.forward)>.999f,"Bot muzzle preserves ballistic elevation instead of desktop reticle correction");}
                    float health=victim.Health;int shots=brain.WeaponShotsAccepted;bool observedShot=false;collector.InputSuspended=false;
                    yield return Until(()=>{observedShot|=runtime.Frame.equipmentActions.Any(a=>a.actor==collector.ActorId&&a.kind==kind&&a.action==EquipmentAction.Shot);return victim.Health<health;},"Actual bot skill damages a physical target using "+kind,9,()=>brain.WeaponReason+" / "+brain.ObjectiveId+" grounded="+collector.Grounded+" inventory="+JsonUtility.ToJson(runtime.EquipmentFor(collector.ActorId))+" shots="+brain.WeaponShotsAccepted+" muzzle="+collector.GetComponent<DeadlyJenga.CandyPlayers.CandyEquipment>().MuzzlePose.position+" target="+victim.Capsule.bounds.center);collector.InputSuspended=true;Check(brain.WeaponShotsAccepted>shots||observedShot,"Authoritative bot weapon fire observed for "+kind);File.AppendAllText(Report,$"WEAPON {kind}: damage={health-victim.Health:F1} reason={brain.WeaponReason}\n");
                }
                collector.Place(new Vector3(150,10.05f,150),Quaternion.identity);collector.InputSuspended=true;victim.Place(new Vector3(150,10.05f,156),Quaternion.identity);victim.InputSuspended=true;Grant(runtime,collector,EquipmentKind.DualRifle);collector.InputSuspended=false;
                yield return Until(()=>runtime.EquipmentFor(collector.ActorId).shotgun,"Bot selects shotgun for a nearby target",5);collector.InputSuspended=true;
                goal.kind=BotObjectiveKind.Travel;goal.point=collector.transform.position+Vector3.right*8;collector.InputSuspended=false;
                yield return Until(()=>!runtime.EquipmentFor(collector.ActorId).drawn,"Travel assignment makes the bot holster its rifle",5);Check(runtime.RulesFor(collector.gameObject).canJump&&runtime.RulesFor(collector.gameObject).canClimb&&runtime.RulesFor(collector.gameObject).canVault,"Holstering restores traversal permissions");collector.InputSuspended=true;
                goal.kind=BotObjectiveKind.Attack;collector.Place(new Vector3(150,10.05f,150),Quaternion.identity);victim.Place(new Vector3(150,10.05f,170),Quaternion.identity);victim.InputSuspended=true;
                var wall=Block(new Vector3(150,13,160),new Vector3(35,6,1));wall.transform.SetParent(root.transform,true);int before=brain.WeaponShotsAccepted;collector.InputSuspended=false;yield return new WaitForSeconds(1.5f);Check(brain.WeaponShotsAccepted==before,"Bot withholds gunfire through solid cover");collector.InputSuspended=true;Object.Destroy(wall.gameObject);
                Check(ModeFrameValidation.Frame(JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson()),world.Blocks.Count),"Configuration, inventory and random pickup metadata survive validated JSON");
                File.AppendAllText(Report,$"DIRECTOR peak={runtime.ObjectiveDirector?.PeakMilliseconds:F3}ms; this is a fixture sample, not a many-bot FPS benchmark.\n");
                yield return PowerupBotNetworkChecks.Run(new[]{supportBefore,supportAfter,expired},world.Blocks.Count,Check);
                yield return PowerupBotMysteryChecks.Run(session,Check);
            }
            finally{Climbables.Unregister(proxyShape);BotObjectives.Current=originalProvider;Services.World=originalWorld;Object.Destroy(root);}
            File.AppendAllText(Report,"OWN FEATURE CHECKS COMPLETE\n");
        }
        static IEnumerator ModelGallery()
        {
            var session=Services.Session as CastlePlaySession;var rules=ModeSimulation.Defaults(GameModeKind.Demo);rules.countdownSeconds=0;rules.defaultDagger=true;rules.punchEnabled=true;session.Modes.Configure(rules);session.Modes.StartRound();yield return new WaitForSeconds(.3f);
            var enemies=session.SurvivorBots as IEnemyPopulation;var enemy=enemies?.SpawnEnemy(new EnemySpawnSettings());Check(enemy,"Enemy regression spawns a real jaguar");yield return new WaitForSeconds(.3f);
            Check(session.Modes.EquipmentFor(enemy.GetComponent<IModeActor>().ActorId).kind==EquipmentKind.None,"PvP fallback dagger does not arm enemy NPCs or replace their claw damage");enemies.ClearEnemies();
            yield return ArsenalExpansionChecks.CaptureModels(session,Check);
        }
        static IEnumerator AimTest()
        {
            var session=Services.Session as CastlePlaySession;if(!session)yield break;var runtime=session.Modes;
            session.SurvivorBots.Clear();var demo=ModeSimulation.Defaults(GameModeKind.Demo);demo.countdownSeconds=0;runtime.Configure(demo);runtime.StartRound();session.SetRole(false);yield return new WaitForSeconds(.4f);
            yield return SharedAim(session);
        }
        static IEnumerator SharedAim(CastlePlaySession session)
        {
            string firearm=ProjectPath.Report("FirearmUpgrade.txt");string before=File.Exists(firearm)?File.ReadAllText(firearm):"";
            var method=typeof(FirearmUpgradeChecks).GetMethod("AimOnly",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            if(method==null)yield break;
            var stack=new Stack<IEnumerator>();stack.Push((IEnumerator)method.Invoke(null,new object[]{session}));Exception failure=null;
            while(stack.Count>0)
            {
                bool more=false;object value=null;try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){failure=e;break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            string after=File.Exists(firearm)?File.ReadAllText(firearm):"";string added=after.Length>=before.Length?after.Substring(before.Length):after;
            if(failure==null&&added.Contains("FAIL "))failure=new Exception("Shared aim assertions failed");
            if(failure!=null)failed=true;
            File.WriteAllText(ProjectPath.Report("FirearmAimShared.txt"),added+(failure!=null?"SHARED AIM FAIL "+failure+"\n":"SHARED AIM COMPLETE\n"));
            File.AppendAllText(Report,failure==null?"Shared firearm aiming fixture completed; see FirearmAimShared.txt.\n":"Shared firearm aiming fixture failed separately; see FirearmAimShared.txt.\n");
        }
    }
    public sealed class PowerupBotCheckDriver:MonoBehaviour{}
}
