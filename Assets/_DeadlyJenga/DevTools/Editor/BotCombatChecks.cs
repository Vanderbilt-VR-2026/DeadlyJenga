using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class BotCombatChecks
    {
        const string Key="BotCombatChecks";
        static readonly string[] AllCases={"approach-face-hit","too-close","sustained-combat","hill-hit","territory-push","infection-conversion","mystery-murder","airborne-opponent","gap-reach","ground-lip","wall","remembered-only","unleased-target","friendly-target","ally-interception","disabled","carrier-attacker","carrier-flank","cancel-windup","dodge-windup","cancel-climb","hill-contact","territory-contact","ctf-contact","survivor-repel","retreat-repel","mystery-unknown-contact","mystery-repel","survival-option"};
        static string[] Cases=AllCases;
        [Serializable] sealed class Request { public string[] cases; }
        [Serializable] sealed class Scenes { public SceneSetup[] scenes; }
        sealed class Target : IBotObjectives,IBotObjectiveExecution
        {
            public GameObject source,victim;public bool enabled=true,memoryOnly,authorized=true;public Vector3 remembered;
            public BotObjectiveKind kind=BotObjectiveKind.Attack;
            public void Collect(GameObject actor,List<BotObjective> goals)
            {if(enabled&&actor==source&&victim)goals.Add(new BotObjective{id="fixture-attack",kind=kind,subject=memoryOnly?null:victim,position=memoryOnly?remembered:victim.transform.position,radius=1,priority=1,expiresAt=Time.time+1});}
            public bool TryGetPolicy(GameObject actor,string objectiveId,out BotExecutionPolicy policy)
            {policy=new BotExecutionPolicy{assignment=1};return enabled&&authorized&&actor==source&&objectiveId=="fixture-attack";}
            public void Report(GameObject actor,BotExecutionFeedback feedback){}
        }
        sealed class JumpDriver:ICastleSurvivorDriver
        {public CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)=>new CastleBotCommand{jump=motor.Grounded,look=Vector3.left};}
        static CastlePlaySession session;
        static GameModeKind fixtureMode;
        static CastleBotBrain attacker;
        static CastleSurvivor victim,ally;
        static Target target;
        static GameObject floor,wall,extraSupport;
        static int stage=-1,phase,failures,errors,damageEvents,pushes,victimPushes,attemptsAtStart;
        static float at,began,healthBefore,allyHealth,firstPunch,nextCsv;
        static double boot;
        static bool initialized,changed,sawWindup,sawFlank,sawApproach,sawClimb,cadence,airDamage,groundProbePassed,sawContactTask;
        static float lastPunch;
        static string Report=>ProjectPath.Report("BotCombat.txt");
        static string Csv=>ProjectPath.Report("BotCombat.csv");
        static BotCombatChecks(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=State;}
        [MenuItem("Tools/Deadly Jenga/Validation/Check Bot Combat")]
        public static void Run()=>Begin(AllCases);
        static void Begin(string[] selected)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SpectatorVideoCapture.Busy)throw new InvalidOperationException("Combat checks require an idle Editor.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>string.IsNullOrEmpty(s.path))||Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            if(selected==null||selected.Length==0||selected.Any(s=>!AllCases.Contains(s)))throw new ArgumentException("Choose valid combat case names.");
            if(selected.Length>AllCases.Length)throw new ArgumentException("Split repeated combat cases into separate runs.");
            SessionState.SetString(Key+"Cases",JsonUtility.ToJson(new Request{cases=selected}));
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            SessionState.SetInt(Key+"Role",(int)MysteryChoices.RequestedRole);
            SessionState.SetFloat(Key+"Scale",Time.timeScale);SessionState.SetInt(Key+"Capture",Time.captureFramerate);SessionState.SetInt(Key+"Target",Application.targetFrameRate);
            File.WriteAllText(Report,"Real Game scene combat fixtures with six controlled bots. Mechanics cases supply an initial goal; production contact cases use real mode facts/director after normal perception settles. Ordinary motors, cooldowns, geometry and authoritative outcomes. Full natural matches are separate checks.\n");
            File.WriteAllText(Csv,"case,time,state,reason,x,y,z,victimHealth,attempts,accepted,withheld\n");
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Restore",true);
            MatchSetup.FromLobby=true;MatchSetup.BotTarget=1;MatchSetup.StartLocalSession=false;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
            if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+"Restore",false))return;
            ValidationRunGate.Release(Key);
            SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Restore",false);
            EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).scenes);
            SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
            MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
            MysteryChoices.RequestedRole=(MysteryRole)SessionState.GetInt(Key+"Role",0);
            File.AppendAllText(Report,"PASS Original scene setup and match choices restored.\n");
        }
        static void Poll()
        {
            if(!SessionState.GetBool(Key,false))
            {
                string request=ProjectPath.Report("BotCombat.request");
                if(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&File.Exists(request))
                {if(!ValidationRunGate.TryAcquire(Key))return;string input=File.ReadAllText(request);File.Delete(request);Begin(input.TrimStart().StartsWith("{")?JsonUtility.FromJson<Request>(input).cases:AllCases);}
                return;
            }
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try
            {
                if(!initialized)
                {
                    AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);
                    initialized=true;stage=-1;phase=failures=errors=0;boot=EditorApplication.timeSinceStartup;
                    Cases=JsonUtility.FromJson<Request>(SessionState.GetString(Key+"Cases","")).cases;
                    Time.captureFramerate=60;Time.timeScale=1;Application.targetFrameRate=60;
                    Application.logMessageReceived+=Log;GameEvents.Damaged+=Damage;GameEvents.Pushed+=Push;
                }
                string cancel=ProjectPath.Report("BotCombat.cancel");if(File.Exists(cancel)){File.Delete(cancel);throw new Exception("Cancelled");}
                if(!session)
                {
                    session=Services.Session as CastlePlaySession;
                    if(!session||session.SurvivorBots==null){if(EditorApplication.timeSinceStartup-boot>30)throw new Exception("Session startup timeout");return;}
                    session.SetMenu(false);session.Survivor().InputSuspended=true;if(session.Social!=null)session.Social.VoiceEnabled=false;
                    floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Combat fixture support";floor.layer=Layers.Surface;floor.transform.position=new Vector3(110,9,100);floor.transform.localScale=new Vector3(80,2,80);
                    var body=floor.AddComponent<Rigidbody>();body.useGravity=false;body.isKinematic=true;
                    Next();return;
                }
                if(Time.time<at)return;
                if(phase==0){Spawn();return;}
                if(phase==4){SpawnActors();return;}
                if(phase==1){Prepare();return;}
                if(phase==3)
                {
                    attacker.enabled=true;attacker.Motor.InputDriver=attacker;attacker.Motor.InputSuspended=false;
                    began=Time.time;nextCsv=0;
                    phase=2;at=Time.time+.016f;return;
                }
                Sample();
            }
            catch(Exception e){failures++;File.AppendAllText(Report,"FAIL "+e+"\n");Finish();}
        }
        static void Next()
        {
            if(attacker)attacker.Motor.PunchRequested-=Punch;
            BotObjectives.Current=session.Modes;
            if(wall)UnityEngine.Object.Destroy(wall);wall=null;
            if(extraSupport)UnityEngine.Object.Destroy(extraSupport);extraSupport=null;
            session.SurvivorBots.Clear();stage++;
            if(stage>=Cases.Length){Finish();return;}
            phase=0;at=Time.time+.1f;
        }
        static void Spawn()
        {
            string name=Cases[stage];
            var mode=name.StartsWith("carrier")||name=="ctf-contact"?GameModeKind.CaptureFlag:name=="hill-hit"||name=="hill-contact"?GameModeKind.KingOfHill:name=="territory-push"||name=="territory-contact"?GameModeKind.BlockControl:name=="infection-conversion"||name=="survivor-repel"?GameModeKind.Infection:name.StartsWith("mystery-")?GameModeKind.MurderMystery:name=="survival-option"?GameModeKind.Survival:GameModeKind.TeamTickets;
            var rules=ModeSimulation.Defaults(mode);
            fixtureMode=mode;
            rules.defaultDagger=false;rules.countdownSeconds=0;rules.roundSeconds=0;rules.automaticDisasters=false;
            rules.starterInfected=false;rules.mystery.attackGraceSeconds=0;
            if(Cases[stage]=="disabled")rules.punchEnabled=false;
            if(name=="survival-option")rules.punchEnabled=true;
            MatchSetup.FromLobby=true;MatchSetup.BotTarget=1;MatchSetup.StartLocalSession=false;
            session.Modes.Configure(rules);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();
            phase=4;at=Time.time+.1f;
        }
        static void SpawnActors()
        {
            string name=Cases[stage];var mode=fixtureMode;
            MatchSetup.BotTarget=7;session.SurvivorBots.TargetPopulation=7;
            for(int i=0;i<6;i++)if(!session.SurvivorBots.SpawnOne())throw new Exception("Spawn failed");
            foreach(var bot in session.Bots().Bots){bot.enabled=false;bot.Motor.InputDriver=null;bot.Motor.InputSuspended=true;}
            if(mode==GameModeKind.MurderMystery)
            {
                MysteryChoices.RequestedRole=MysteryRole.Innocent;
                var preferences=(Dictionary<string,MysteryRole>)typeof(MatchModeRuntime).GetField("rolePreferences",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.Modes);
                var bots=session.Bots().Bots;
                int killer=name=="mystery-repel"||name=="mystery-unknown-contact"?1:0;
                for(int i=0;i<bots.Count;i++)preferences[bots[i].GetComponent<ISocialBot>().NetworkId]=i==killer?MysteryRole.Murderer:i==2?MysteryRole.Sheriff:MysteryRole.Innocent;
            }
            session.Modes.StartRound();
            if(mode==GameModeKind.Infection)
            {var sim=Sim();sim.Tick(.001f);typeof(ModeSimulation).GetMethod("MakeInfected",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{sim.Find(session.Bots().Bots[name=="survivor-repel"?1:0].Motor.ActorId)});}
            phase=1;at=Time.time+1.5f;
        }
        static ModeSimulation Sim()=>(ModeSimulation)typeof(MatchModeRuntime).GetField("simulation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.Modes);
        static void Prepare()
        {
            var bots=session.Bots().Bots;attacker=bots[0];
            if(bots.Count!=6)throw new Exception("Combat fixture population changed: "+bots.Count);
            victim=(attacker.Motor.Team<0?bots[1]:bots.First(b=>b.Motor.Team!=attacker.Motor.Team)).Motor;
            ally=(attacker.Motor.Team<0?bots[3]:bots.First(b=>b!=attacker&&b.Motor.Team==attacker.Motor.Team)).Motor;
            if(Sim().Find(attacker.Motor.ActorId)==null||Sim().Find(victim.ActorId)==null)throw new Exception("Fixture actors were not registered before placement");
            for(int i=0;i<bots.Count;i++){var bot=bots[i];bot.enabled=false;bot.Motor.InputDriver=null;bot.Motor.InputSuspended=true;bot.Motor.Place(new Vector3(130,10.05f,120+i*2),Quaternion.identity);}
            session.Survivor().Place(new Vector3(140,10.05f,100),Quaternion.identity);
            attacker.Motor.Place(new Vector3(100,10.05f,100),Quaternion.LookRotation(Vector3.left));
            victim.Place(new Vector3(Cases[stage]=="approach-face-hit"?105:101.6f,10.05f,100),Quaternion.LookRotation(Vector3.left));
            if(Cases[stage]=="too-close")victim.Place(new Vector3(100.55f,10.05f,100),Quaternion.LookRotation(Vector3.left));
            if(Cases[stage]=="friendly-target")victim=ally;
            if(Cases[stage]=="friendly-target")victim.Place(new Vector3(101.6f,10.05f,100),Quaternion.identity);
            if(Cases[stage]=="ally-interception")ally.Place(new Vector3(100.8f,10.05f,100),Quaternion.identity);
            if(Cases[stage]=="wall")
            {
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Combat occlusion fixture";wall.layer=Layers.Surface;
                wall.transform.position=new Vector3(100.8f,14,100);wall.transform.localScale=new Vector3(.3f,8,70);
            }
            if(Cases[stage].StartsWith("carrier"))
            {
                var homes=new[]{new Vector3(130,10.05f,90),new Vector3(130,10.05f,110)};
                bool sourceCarrier=Cases[stage]=="carrier-attacker";
                int owner=sourceCarrier?victim.Team:attacker.Motor.Team;
                homes[owner]=(sourceCarrier?attacker.Motor:victim).transform.position;
                Sim().SetFlagHomes(homes);
            }
            Physics.SyncTransforms();
            target=new Target{source=attacker.gameObject,victim=victim.gameObject,memoryOnly=Cases[stage]=="remembered-only",authorized=Cases[stage]!="unleased-target",remembered=victim.transform.position};BotObjectives.Current=target;
            attacker.enabled=true;attacker.Motor.InputDriver=attacker;attacker.Motor.InputSuspended=false;
            healthBefore=victim.Health;allyHealth=ally.Health;began=Time.time;firstPunch=-1;lastPunch=-100;cadence=true;nextCsv=0;
            changed=sawWindup=sawFlank=sawApproach=sawClimb=airDamage=sawContactTask=false;damageEvents=pushes=victimPushes=0;attemptsAtStart=attacker.PunchAttempts;
            attacker.Motor.PunchRequested+=Punch;phase=2;at=Time.time+.016f;
            if(Cases[stage]=="cancel-climb")PrepareClimb();
            if(Cases[stage]=="airborne-opponent")
            {
                attacker.Motor.Place(new Vector3(100,10.05f,100),Quaternion.LookRotation(Vector3.right));
                victim.Place(new Vector3(101.3f,10.05f,100),Quaternion.LookRotation(Vector3.left));
                victim.InputDriver=new JumpDriver();victim.InputSuspended=false;
            }
            if(Cases[stage]=="gap-reach")
            {
                wall=Support(new Vector3(100,12,100),new Vector3(.4f,4,.4f));
                extraSupport=Support(new Vector3(101.7f,12,100),new Vector3(.4f,4,.4f));
                Physics.SyncTransforms();
                attacker.Motor.Place(new Vector3(100,14.05f,100),Quaternion.LookRotation(Vector3.right));
                victim.Place(new Vector3(101.7f,14.05f,100),Quaternion.LookRotation(Vector3.left));
            }
            if(Cases[stage]=="ground-lip")PrepareGroundProbe();
            if(Production(Cases[stage]))
            {
                attacker.Spawned(false);BotObjectives.Current=session.Modes;
                if(Cases[stage]=="retreat-repel")attacker.Motor.Damage(80,null);
                attacker.enabled=false;attacker.Motor.InputDriver=null;attacker.Motor.InputSuspended=true;
                if(Cases[stage]=="mystery-repel")WitnessStrike();
                phase=3;at=Time.time+.9f;
                File.AppendAllText(Report,"PRODUCTION contact policy: initial pair remains still for .9s of ordinary perception, then actual mode facts/director choose the target. No knowledge or attack assignment is injected.\n");
            }
            File.AppendAllText(Report,"BEGIN "+Cases[stage]+"\n");
        }
        static GameObject Support(Vector3 position,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Combat supported footing fixture";go.layer=Layers.Surface;
            go.transform.position=position;go.transform.localScale=scale;var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;return go;
        }
        static bool Production(string name)=>name.EndsWith("-contact",StringComparison.Ordinal)||name=="survivor-repel"||name=="retreat-repel"||name=="mystery-repel"||name=="survival-option";
        static void WitnessStrike()
        {
            var mystery=(MysterySimulation)typeof(MatchModeRuntime).GetField("mystery",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.Modes);
            if(mystery.Role(victim.ActorId)!=MysteryRole.Murderer)throw new Exception("Witness fixture requires the initial murderer role");
            mystery.SetSpecial(victim.ActorId,true);victim.InputSuspended=false;
            bool accepted=victim.RequestPunch(victim.ViewPose.position,Vector3.forward,2);victim.InputSuspended=true;
            var strikes=(Dictionary<string,float>)typeof(MatchModeRuntime).GetField("mysteryStrikes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.Modes);
            if(!accepted||!strikes.TryGetValue(victim.ActorId,out var until)||until<=Time.time)throw new Exception("Initial special strike was not announced through the actual punch action");
            File.AppendAllText(Report,"Murderer made a real visible special strike into empty space during initial observation; observer knowledge must come from normal witness processing.\n");
        }
        static void PrepareGroundProbe()
        {
            wall=Support(new Vector3(100,13,100),new Vector3(4,2,2));Physics.SyncTransforms();
            var motor=attacker.Motor;motor.InputSuspended=true;target.enabled=false;
            var probe=typeof(CastleSurvivor).GetMethod("ProbeGround",BindingFlags.Instance|BindingFlags.NonPublic);
            bool Ground(Vector3 feet){motor.Teleport(feet,Quaternion.identity);probe.Invoke(motor,null);return motor.Grounded;}
            float radius=motor.Capsule.radius,scale=motor.playerScale;
            float y=14-(radius*.8f+motor.Capsule.skinWidth);
            bool lip=Ground(new Vector3(100,y,101+radius*.925f));
            bool corner=Ground(new Vector3(102+radius*.65f,y,101+radius*.65f));
            bool air=!Ground(new Vector3(100,14,103));
            bool side=!Ground(new Vector3(100,13,101+radius*.6f));
            bool ordinary=Ground(new Vector3(100,14-(.1f*scale+motor.Capsule.skinWidth),101+radius*.35f));
            groundProbePassed=lip&&corner&&air&&side&&ordinary;
            File.AppendAllText(Report,"Ground probe: rounded lip="+lip+" diagonal corner="+corner+" unsupported-air rejected="+air+" vertical-wall rejected="+side+" ordinary lip="+ordinary+" radius="+radius+" scale="+scale+"\n");
        }
        static void Field(string name,object value)=>typeof(CastleBotBrain).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(attacker,value);
        static void PrepareClimb()
        {
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Climb cancellation fixture";wall.layer=Layers.Surface;
            wall.transform.position=new Vector3(104,12,100);wall.transform.localScale=new Vector3(4,4,4);
            var body=wall.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;wall.AddComponent<BotCombatClimbSurface>();
            Physics.SyncTransforms();
            var end=new Vector3(102.8f,14,100);
            attacker.Motor.Place(new Vector3(101.5f,10.05f,100),Quaternion.LookRotation(Vector3.right));
            victim.Place(new Vector3(130,10.05f,100),Quaternion.identity);
            target.kind=BotObjectiveKind.Travel;target.memoryOnly=true;target.remembered=end;
            attacker.SetJourneyDestination(end,30);
            var objective=new BotObjective{id="fixture-attack",kind=BotObjectiveKind.Travel,position=end,radius=.5f,expiresAt=Time.time+30};
            if(!(bool)typeof(CastleBotBrain).GetMethod("BindExecution",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(attacker,new object[]{objective,false}))throw new Exception("Climb fixture could not bind its initial travel assignment");
            Field("selectedObjective",objective);Field("hasObjective",true);Field("nextObjective",Time.time+30);
            Field("goal",new CastleBotLanding{surface=wall.transform,body=body,local=wall.transform.InverseTransformPoint(end),route=CastleBotRoute.Climb});
            Field("hasGoal",true);Field("goalSince",Time.time);Field("nextPlan",Time.time+30);sawClimb=false;
            File.AppendAllText(Report,"FIXTURE cancel-climb: one initial physical Climb goal on a supported 4m test surface. Once the normal motor grips, revoke its strategic lease. Only ordinary movement may finish the mantle.\n");
        }
        static void Sample()
        {
            string name=Cases[stage];float elapsed=Time.time-began;
            if(Production(name)&&session.Modes.ObjectiveDirector!=null&&session.Modes.ObjectiveDirector.TryGet(attacker.Motor.ActorId,out var contact,out _)&&contact.kind==BotObjectiveKind.Attack&&contact.id.StartsWith("repel/",StringComparison.Ordinal))sawContactTask=true;
            sawWindup|=attacker.CombatState==BotCombatState.Windup;sawFlank|=attacker.CombatState==BotCombatState.Flank;sawApproach|=attacker.CombatState==BotCombatState.Approach;
            if((name=="cancel-windup"||name=="dodge-windup")&&!changed&&attacker.CombatState==BotCombatState.Windup)
            {changed=true;if(name=="cancel-windup")target.enabled=false;else victim.Place(new Vector3(125,10.05f,100),Quaternion.identity);}
            if(name=="cancel-climb"&&!changed&&attacker.Motor.Climbing){sawClimb=true;changed=true;target.enabled=false;File.AppendAllText(Report,"Climb lease revoked after real grip at "+attacker.transform.position+"\n");}
            var p=attacker.transform.position;
            if(Time.time>=nextCsv)
            {nextCsv=Time.time+.1f;File.AppendAllText(Csv,string.Join(",",name,F(elapsed),attacker.CombatState,attacker.CombatReason.Replace(',',';'),F(p.x),F(p.y),F(p.z),F(victim.Health),attacker.PunchAttempts,attacker.PunchesAccepted,attacker.PunchesWithheld)+"\n");}
            bool positive=name=="approach-face-hit"||name=="too-close"||name=="carrier-flank"||name=="ally-interception"||name=="sustained-combat"||name=="hill-hit"||name=="territory-push"||name=="infection-conversion"||name=="mystery-murder"||name=="cancel-climb"||name=="airborne-opponent"||name=="gap-reach";
            positive|=Production(name)&&name!="mystery-unknown-contact";
            float limit=positive?16:4;
            if(name=="ground-lip")limit=.05f;
            bool hit=name=="territory-push"?victimPushes>0:name=="infection-conversion"?Sim().Find(victim.ActorId).frame.role=="infected":name=="mystery-murder"?session.Modes.Frame.mystery.bodies.Any(b=>b.id==victim.ActorId):victim.Health<healthBefore;
            if(name=="sustained-combat")hit=damageEvents>=3;
            if(name=="territory-contact"||name=="survival-option")hit=victimPushes>0;
            if(name=="airborne-opponent")hit=airDamage;
            if(name=="cancel-climb")hit=changed&&sawClimb&&attacker.Motor.Grounded&&!attacker.Motor.Climbing&&!attacker.Motor.Vaulting&&attacker.transform.position.y>13.8f;
            if(positive&&elapsed<limit&&!hit)return;
            if(!positive&&elapsed<limit)return;
            bool pass=positive?hit&&cadence&&firstPunch>=.25f:attacker.PunchAttempts==attemptsAtStart&&!hit;
            if(name=="cancel-climb")pass=hit&&attacker.PunchAttempts==attemptsAtStart;
            if(name=="ground-lip")pass=groundProbePassed;
            if(name=="gap-reach")pass&=attacker.Motor.Grounded&&Vector3.ProjectOnPlane(attacker.transform.position-new Vector3(100,0,100),Vector3.up).sqrMagnitude<.04f;
            if(Production(name))pass&=name=="mystery-unknown-contact"?!sawContactTask:sawContactTask;
            if(name=="survivor-repel")pass&=Sim().Find(attacker.Motor.ActorId).frame.role=="survivor"&&Sim().Find(victim.ActorId).frame.role=="infected";
            if(name=="retreat-repel")pass&=attacker.Motor.Health<attacker.Motor.MaxHealth*.35f;
            if(name=="mystery-repel"||name=="mystery-unknown-contact")
            {var mystery=(MysterySimulation)typeof(MatchModeRuntime).GetField("mystery",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.Modes);pass&=mystery.Role(attacker.Motor.ActorId)==MysteryRole.Innocent&&mystery.Role(victim.ActorId)==MysteryRole.Murderer;}
            if(name=="approach-face-hit")pass&=sawWindup&&sawApproach;
            if(name=="carrier-flank")pass&=sawFlank&&session.Modes.RulesFor(victim.gameObject).carryingFlag;
            if(name=="ally-interception")pass&=ally.Health==allyHealth&&pushes==0&&attacker.PunchesWithheld>0;
            if(name=="cancel-windup"||name=="dodge-windup")pass&=changed;
            if(name=="carrier-attacker")pass&=session.Modes.RulesFor(attacker.gameObject).carryingFlag;
            if(!pass)failures++;
            if(!pass&&Production(name))DiagnoseContact();
            File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+name+" seconds="+elapsed.ToString("F3")+" health="+victim.Health+" attempts="+(attacker.PunchAttempts-attemptsAtStart)+" damageEvents="+damageEvents+" victimPushes="+victimPushes+" withheld="+attacker.PunchesWithheld+" windup="+sawWindup+" flank="+sawFlank+" changed="+changed+" climbed="+sawClimb+" productionContact="+sawContactTask+" position="+attacker.transform.position+" allyPushes="+pushes+" reason="+attacker.CombatReason+"\n");
            Next();
        }
        static void DiagnoseContact()
        {
            var modes=session.Modes;var sim=Sim();var self=sim.Find(attacker.Motor.ActorId);var other=sim.Find(victim.ActorId);
            File.AppendAllText(Report,"CONTACT DEBUG mode="+modes.Rules.mode+" phase="+modes.Frame.phase+" authority="+modes.Authority+" provider="+BotObjectives.Current?.GetType().Name+" source="+attacker.Motor.ActorId+" victim="+victim.ActorId+"\n");
            foreach(var p in new[]{self,other})if(p!=null)File.AppendAllText(Report,"  participant="+p.frame.id+" present="+p.present+" alive="+p.frame.alive+" dying="+p.frame.dying+" bot="+p.frame.bot+" role="+p.frame.role+" team="+p.frame.team+" framePos="+p.frame.position+"\n");else File.AppendAllText(Report,"  MISSING participant\n");
            File.AppendAllText(Report,"  body source="+attacker.transform.position+" victim="+victim.transform.position+" sourceAlive="+attacker.Motor.Alive+" victimAlive="+victim.Alive+" punchEnabled="+modes.RulesFor(attacker.gameObject).punchEnabled+"\n");
            if(modes.ObjectiveDirector!=null)foreach(var e in modes.ObjectiveDirector.History.TakeLast(12))File.AppendAllText(Report,"  assignment="+e.actor+" "+e.objective+" "+e.outcome+" "+e.reason+"\n");
        }
        static void Punch(CastleSurvivor actor,Vector3 origin,Vector3 direction,float speed)
        {float t=Time.time-began;if(firstPunch<0)firstPunch=t;if(Time.time-lastPunch<actor.punchCooldown-.01f)cadence=false;lastPunch=Time.time;}
        static void Damage(GameObject actor,float amount)
        {if(victim&&actor==victim.gameObject){damageEvents++;if(attacker&&victim.transform.position.y-attacker.transform.position.y>.65f)airDamage=true;}}
        static void Push(GameObject actor,Vector3 force){if(ally&&actor==ally.gameObject)pushes++;if(victim&&actor==victim.gameObject)victimPushes++;}
        static string F(float value)=>value.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Finish()
        {
            SessionState.SetBool(Key,false);Application.logMessageReceived-=Log;GameEvents.Damaged-=Damage;GameEvents.Pushed-=Push;
            if(attacker)attacker.Motor.PunchRequested-=Punch;
            BotObjectives.Current=session?session.Modes:null;
            Time.timeScale=SessionState.GetFloat(Key+"Scale",1);Time.captureFramerate=SessionState.GetInt(Key+"Capture",0);Application.targetFrameRate=SessionState.GetInt(Key+"Target",-1);
            try{File.AppendAllText(Report,"COMPLETE failures="+failures+" runtimeErrors="+errors+". Desktop fixtures only; no headset or second peer.\n");}
            finally{EditorApplication.ExitPlaymode();}
        }
    }
    public sealed class BotCombatClimbSurface:MonoBehaviour,IClimbable
    {
        public bool CanClimb=>true;
        public float Grip=>1;
        public Transform Anchor=>transform;
        public Rigidbody Body=>GetComponent<Rigidbody>();
        public Transform Visual=>transform;
    }
}
