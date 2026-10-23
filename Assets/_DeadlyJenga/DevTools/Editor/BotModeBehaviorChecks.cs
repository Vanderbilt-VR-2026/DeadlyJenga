using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class BotModeBehaviorChecks
    {
        const string Key="BotModeBehaviorChecks";
        [Serializable] public sealed class Request
        {
            public int bots=10,maxSeconds=300;
            public bool ascentFixture, mysteryInteractions;
            public GameModeKind[] modes={GameModeKind.Demo,GameModeKind.Survival,GameModeKind.KingOfHill,GameModeKind.BlockControl,GameModeKind.TeamTickets,GameModeKind.Infection,GameModeKind.MurderMystery,GameModeKind.CaptureFlag};
        }
        [Serializable] sealed class Scenes { public SceneSetup[] scenes; }
        static Request request;
        static CastlePlaySession session;
        static GameObject previousWorld,interactionFloor;
        static string interactionTarget;
        static Vector3 initialBodyPosition;
        static int stage=-1,failures,runtimeErrors,initialInfected,peakInfected,owned,captures,botKills,damage;
        static float began,nextSample,maxHill,pendingInteractionAt,pendingMurderAt;
        static double boot;
        static bool ready,restoring,pendingStage,pendingRound;
        static int setupFrame;
        static readonly HashSet<int> damaged=new HashSet<int>();
        static readonly HashSet<string> actionNames=new HashSet<string>();
        static string Report=>ProjectPath.Report("BotModeBehaviors.txt");
        static string Csv=>ProjectPath.Report("BotModeBehaviors.csv");
        static BotModeBehaviorChecks()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Restore",false))Restore();};
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check All Mode Bot Behaviors")]
        public static void Run()=>Begin(new Request());
        static void Begin(Request input)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SpectatorVideoCapture.Busy)throw new InvalidOperationException("Bot mode checks require an idle Editor.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>string.IsNullOrEmpty(s.path))||Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            if(input.bots<2||input.bots>50||input.modes==null||input.modes.Length==0||input.modes.Any(m=>!Enum.IsDefined(typeof(GameModeKind),m)))throw new ArgumentException("Choose 2..50 bots and valid modes.");
            if(input.mysteryInteractions&&(input.bots<4||!input.modes.Contains(GameModeKind.MurderMystery)))throw new ArgumentException("Mystery interaction fixtures require at least four bots and a Mystery stage.");
            input.maxSeconds=Mathf.Clamp(input.maxSeconds,20,300);
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            SessionState.SetInt(Key+"Role",(int)MysteryChoices.RequestedRole);
            SessionState.SetFloat(Key+"Scale",Time.timeScale);SessionState.SetInt(Key+"Capture",Time.captureFramerate);SessionState.SetInt(Key+"Target",Application.targetFrameRate);
            SessionState.SetString(Key+"Request",JsonUtility.ToJson(input));SessionState.SetBool(Key,true);
            File.WriteAllText(Report,"Actual Game scene, ordinary bot motors and authoritative outcomes. Per-mode maps; Mystery uses the KOTH map because no dedicated Mystery map is shipped. Controlled initial setups are described per fixture below. Subsequent movement, interactions, damage, claims, infection conversions and scoring use ordinary gameplay. Disasters disabled to isolate objectives.\n");
            File.WriteAllText(Csv,"mode,time,id,objective,action,state,x,y,z,health,role,travel,idle,tripX,tripY,tripZ,goalX,goalY,goalZ,debug,routeChange,combat,combatReason,punchAttempts,punchAccepted,punchWithheld\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))
            {
                var path=ProjectPath.Report("BotModeBehaviors.request.json");
                if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SpectatorVideoCapture.Busy)return;
                try{var json=File.ReadAllText(path);File.Delete(path);Begin(JsonUtility.FromJson<Request>(json));}catch(Exception e){File.WriteAllText(Report,"FAIL "+e);}
                return;
            }
            var cancel=ProjectPath.Report("BotModeBehaviors.cancel");
            if(File.Exists(cancel)){File.Delete(cancel);failures++;File.AppendAllText(Report,"Cancelled.\n");Finish();return;}
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!EditorApplication.isPlaying)return;
            try
            {
                if(request==null)
                {
                    request=JsonUtility.FromJson<Request>(SessionState.GetString(Key+"Request",""));stage=-1;failures=runtimeErrors=0;ready=false;pendingRound=pendingStage=false;pendingInteractionAt=0;boot=EditorApplication.timeSinceStartup;
                    Time.captureFramerate=60;Time.timeScale=1;Application.targetFrameRate=60;
                    Application.logMessageReceived+=OnLog;GameEvents.Damaged+=OnDamage;GameEvents.Eliminated+=OnEliminated;
                }
                if(!ready)
                {
                    if(EditorApplication.timeSinceStartup-boot<3)return;
                    session=UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();
                    if(!session||session.SurvivorBots==null){if(EditorApplication.timeSinceStartup-boot>30)throw new Exception("Session startup timeout");return;}
                    if(session.Social!=null)session.Social.VoiceEnabled=false;
                    session.SetMenu(false);session.Survivor().InputSuspended=true;ready=true;Next();return;
                }
                if(pendingRound){if(Time.frameCount<setupFrame)return;pendingRound=false;StartStage();return;}
                if(pendingStage){if(Time.frameCount<setupFrame)return;pendingStage=false;SetupStage();return;}
                if(pendingInteractionAt>0){if(Time.time<pendingInteractionAt)return;pendingInteractionAt=0;SetupMysteryInteraction();began=nextSample=Time.time;return;}
                if(pendingMurderAt>0){if(Time.time<pendingMurderAt)return;pendingMurderAt=0;PrepareSheriffBody();began=nextSample=Time.time;return;}
                var frame=session.Modes.Frame;var mode=request.modes[stage];
                peakInfected=Mathf.Max(peakInfected,frame.players.Count(p=>p.bot&&p.role=="infected"));
                owned=Mathf.Max(owned,frame.teams.Sum(t=>t.ownedBlocks));captures=Mathf.Max(captures,frame.teams.Sum(t=>t.captures));
                if(mode==GameModeKind.KingOfHill)maxHill=Mathf.Max(maxHill,frame.players.Where(p=>p.bot).Select(p=>p.metric).DefaultIfEmpty().Max());
                if(Time.time>=nextSample)
                {
                    nextSample=Time.time+1;
                    if(request.mysteryInteractions&&mode==GameModeKind.MurderMystery)foreach(var body in Mystery().Public().bodies)File.AppendAllText(Report,"Body t="+F(Time.time-began)+" at="+body.position+" reported="+body.reported+" lost="+body.lost+" sheriff="+body.sheriff+" finder="+body.finder+"\n");
                    foreach(var bot in session.Bots().Bots)
                    {
                        var actor=bot.GetComponent<IModeActor>();var p=frame.players.FirstOrDefault(p=>p.id==actor.ActorId);var pos=bot.transform.position;
                        actionNames.Add(string.IsNullOrEmpty(bot.ObjectiveId)?"free-explore":bot.ObjectiveId.Split('/')[0]);
                        File.AppendAllText(Csv,string.Join(",",mode,F(Time.time-began),actor.ActorId,bot.ObjectiveId,bot.CurrentAction,bot.MovementState,F(pos.x),F(pos.y),F(pos.z),F(p?.health??1),p?.role,F(bot.TravelDistance),F(bot.UnexplainedIdleSeconds),F(bot.TripDestination.x),F(bot.TripDestination.y),F(bot.TripDestination.z),F(bot.Goal.x),F(bot.Goal.y),F(bot.Goal.z),bot.DebugState?.Replace(',',';'),bot.LastRouteChange?.Replace(',',';'),bot.CombatState,bot.CombatReason?.Replace(',',';'),bot.PunchAttempts,bot.PunchesAccepted,bot.PunchesWithheld)+"\n");
                    }
                }
                float elapsed=Time.time-began;
                if(elapsed<20)return;
                bool outcome=Outcome(mode);
                float deadline=request.mysteryInteractions&&mode==GameModeKind.MurderMystery?45:request.maxSeconds;
                if(!outcome&&elapsed<deadline&&frame.phase==RoundPhase.Playing)return;
                int moved=session.Bots().Bots.Count(b=>b.TravelDistance>2);
                bool pass=outcome&&(request.mysteryInteractions&&mode==GameModeKind.MurderMystery||moved>=Mathf.Max(1,request.bots/2))&&session.Bots().Bots.All(b=>b.UnexplainedIdleSeconds<Mathf.Max(10,elapsed*.6f));
                if(!pass)failures++;
                File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+mode+" seconds="+F(elapsed)+" bots="+session.SurvivorBots.Count+" moved="+moved+" hillSeconds="+F(maxHill)+" owned="+owned+" captures="+captures+" infected="+initialInfected+"->"+peakInfected+" botKills="+botKills+" damageEvents="+damage+" damagedBots="+damaged.Count+" actions="+string.Join("|",actionNames.OrderBy(s=>s))+"\n");
                var director=session.Modes.ObjectiveDirector;
                if(director!=null)foreach(var e in director.History)File.AppendAllText(Report,"  "+e.actor+" "+e.objective+" "+e.outcome+" "+e.reason+"\n");
                if(stage+1<request.modes.Length)Next();else Finish();
            }
            catch(Exception e){failures++;File.AppendAllText(Report,"FAIL "+e+"\n");Finish();}
        }
        static bool Outcome(GameModeKind mode)
        {
            var frame=session.Modes.Frame;
            switch(mode)
            {
                case GameModeKind.Demo:return session.Bots().Bots.Any(b=>b.TravelDistance>4);
                case GameModeKind.Survival:return frame.players.Any(p=>p.bot&&p.alive&&p.survivalSeconds>=20);
                case GameModeKind.KingOfHill:return maxHill>=1;
                case GameModeKind.BlockControl:return owned>=2&&frame.teams.Any(t=>t.points>1);
                case GameModeKind.TeamTickets:return frame.players.Any(p=>p.bot&&p.kills>0)&&damage>0;
                case GameModeKind.Infection:return peakInfected>initialInfected&&frame.players.Any(p=>p.bot&&p.kills>0);
                case GameModeKind.MurderMystery:return request.mysteryInteractions?InteractionOutcome():frame.players.Any(p=>p.bot&&p.kills>0)&&frame.mystery.bodies.Length>0;
                case GameModeKind.CaptureFlag:return captures>0;
                default:return false;
            }
        }
        static void Next()
        {
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();
            if(interactionFloor){UnityEngine.Object.Destroy(interactionFloor);interactionFloor=null;}
            previousWorld=(Services.World as Component)?.gameObject;
            stage++;pendingStage=true;setupFrame=Time.frameCount+2;
        }
        static void SetupStage()
        {
            var mode=request.modes[stage];
            string map=mode==GameModeKind.CaptureFlag?"ctf":mode==GameModeKind.BlockControl?"territory":mode==GameModeKind.TeamTickets?"tickets":mode==GameModeKind.Infection?"infection":mode==GameModeKind.KingOfHill||mode==GameModeKind.MurderMystery?"koth":"survival";
            var prefab=Resources.Load<GameObject>("ModeMaps/Mode_"+map);if(!prefab)throw new Exception("Missing mode map "+map);
            var root=UnityEngine.Object.Instantiate(prefab);
            if(previousWorld){previousWorld.SetActive(false);UnityEngine.Object.Destroy(previousWorld);previousWorld=null;}foreach(var c in root.GetComponentsInChildren<MonoBehaviour>())if(c is IRebuildable builder){builder.Rebuild();break;}
            pendingRound=true;setupFrame=Time.frameCount+2;
        }
        static void StartStage()
        {
            var mode=request.modes[stage];
            var rules=ModeSimulation.Defaults(mode);rules.countdownSeconds=0;rules.roundSeconds=0;rules.automaticDisasters=false;rules.starterInfected=false;
            rules.mystery.firstDisasterSeconds=3600;
            if(request.mysteryInteractions)rules.mystery.attackGraceSeconds=0;
            MatchSetup.FromLobby=true;MatchSetup.BotTarget=request.bots+1;MatchSetup.StartLocalSession=false;
            session.Modes.Configure(rules);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();
            for(int i=0;i<request.bots;i++)if(!session.SurvivorBots.SpawnOne())throw new Exception("Bot spawn failed");
            foreach(var bot in session.Bots().Bots){SocialBots.Attach(bot.gameObject);bot.diagnostics=true;}
            if(mode==GameModeKind.MurderMystery)
            {
                MysteryChoices.RequestedRole=request.mysteryInteractions?MysteryRole.Murderer:MysteryRole.Innocent;
                var preferences=(Dictionary<string,MysteryRole>)typeof(MatchModeRuntime).GetField("rolePreferences",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.Modes);
                preferences.Clear();
                if(request.mysteryInteractions)
                {
                    foreach(var bot in session.Bots().Bots)preferences[bot.GetComponent<ISocialBot>().NetworkId]=MysteryRole.Innocent;
                    preferences[session.Bots().Bots[stage%2==0?2:1].GetComponent<ISocialBot>().NetworkId]=MysteryRole.Sheriff;
                }
                else {preferences[session.Bots().Bots[0].GetComponent<ISocialBot>().NetworkId]=MysteryRole.Murderer;preferences[session.Bots().Bots[1].GetComponent<ISocialBot>().NetworkId]=MysteryRole.Sheriff;}
            }
            session.Modes.StartRound();session.Survivor().InputSuspended=true;
            if(mode==GameModeKind.Infection)
            {
                var sim=(ModeSimulation)typeof(MatchModeRuntime).GetField("simulation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.Modes);
                sim.Tick(.001f);
                typeof(ModeSimulation).GetMethod("MakeInfected",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{sim.Find(session.Bots().Bots[0].GetComponent<IModeActor>().ActorId)});
            }
            if(request.mysteryInteractions&&mode==GameModeKind.MurderMystery)pendingInteractionAt=Time.time+2;
            if(request.ascentFixture)
            {
                for(int i=0;i<session.Bots().Bots.Count;i++)
                {
                    var bot=session.Bots().Bots[i];var probe=new Vector3(i%2==0?2:-2,53,0);
                    if(Physics.Raycast(probe,Vector3.down,out var hit,3,CastleBotNavigation.Mask,QueryTriggerInteraction.Ignore))
                        bot.Motor.Place(hit.point+Vector3.up*.04f,Quaternion.LookRotation(Vector3.ProjectOnPlane(-probe,Vector3.up)));
                }
                File.AppendAllText(Report,"Initial ascent fixture places bots on the central tower at 52m; all subsequent climbing is ordinary motor execution.\n");
            }
            began=Time.time;nextSample=began;owned=captures=botKills=damage=0;maxHill=0;initialInfected=mode==GameModeKind.Infection?1:0;peakInfected=initialInfected;
            damaged.Clear();actionNames.Clear();
            File.AppendAllText(Report,"BEGIN "+mode+" bots="+request.bots+"\n");
        }
        static ModeSimulation Simulation()=>(ModeSimulation)typeof(MatchModeRuntime).GetField("simulation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.Modes);
        static MysterySimulation Mystery()=>(MysterySimulation)typeof(MatchModeRuntime).GetField("mystery",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.Modes);
        static void SetupMysteryInteraction()
        {
            var sim=Simulation();sim.Tick(.001f);var mystery=Mystery();var bots=session.Bots().Bots;
            var floor=interactionFloor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Mystery interaction fixture floor";
            floor.transform.position=new Vector3(100,9,100);floor.transform.localScale=new Vector3(100,2,16);
            var support=floor.AddComponent<Rigidbody>();support.isKinematic=true;support.useGravity=false;
            Physics.SyncTransforms();
            bots[0].Motor.Place(new Vector3(95,10.05f,100),Quaternion.LookRotation(Vector3.right));
            bots[1].Motor.Place(new Vector3(100,10.05f,100),Quaternion.identity);
            bots[2].Motor.Place(new Vector3(104,10.05f,103),Quaternion.LookRotation(Vector3.left));
            bots[3].Motor.Place(new Vector3(104,10.05f,97),Quaternion.LookRotation(Vector3.left));
            session.Survivor().Place(new Vector3(140,10.05f,100),Quaternion.identity);
            interactionTarget=bots[1].GetComponent<IModeActor>().ActorId;
            if(stage%2==0)
            {
                string offender=bots[3].GetComponent<IModeActor>().ActorId;
                if(mystery.Lethal(offender,interactionTarget,false,null)!=MysteryHit.InnocentMistake)throw new Exception("Downed fixture could not be prepared");
                sim.Find(interactionTarget).frame.role="downed";sim.Kill(offender,null);
                bots[3].GetComponent<IDamageable>().Eliminate(null);
                if(!mystery.IsDowned(interactionTarget))throw new Exception("Missing initial downed victim");
                File.AppendAllText(Report,"FIXTURE revive: initial innocent mistake downs one bot. Helpers must approach and sustain a real revive command. No revival is injected.\n");
            }
            else {pendingMurderAt=Time.time+.6f;}
        }
        static void PrepareSheriffBody()
        {
                var sim=Simulation();var mystery=Mystery();var bots=session.Bots().Bots;
                string killer=session.Modes.LocalActorId;mystery.SetSpecial(killer,true);var p=sim.Find(interactionTarget).frame;initialBodyPosition=bots[1].transform.position;
                if(mystery.Lethal(killer,interactionTarget,true,new MysteryBodyFrame{id=interactionTarget,name=p.name,color=p.color,position=bots[1].transform.position,supportBlock=-1})!=MysteryHit.Murder)throw new Exception("Badge fixture could not be prepared");
                ((HashSet<string>)typeof(MatchModeRuntime).GetField("corpseDeaths",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.Modes)).Add(interactionTarget);
                sim.Kill(interactionTarget,killer);bots[1].GetComponent<IDamageable>().Eliminate(session.Player.gameObject);
                File.AppendAllText(Report,"FIXTURE report-badge: initial murdered sheriff body. Innocent bots must approach, report and recover the badge through validated commands. No report or promotion is injected.\n");
                foreach(var b in mystery.Public().bodies)File.AppendAllText(Report,"Initial body "+b.id+" sheriff="+b.sheriff+" at="+b.position+" actorRole="+mystery.Role(interactionTarget)+"\n");
        }
        static bool InteractionOutcome()
        {
            var mystery=Mystery();if(mystery==null)return false;
            if(stage%2==0)return mystery.Alive(interactionTarget)&&!mystery.IsDowned(interactionTarget)&&Simulation().Find(interactionTarget)?.frame.role=="survivor";
            return mystery.Public().bodies.Any(b=>b.id==interactionTarget&&b.reported&&!b.lost&&Vector3.Distance(b.position,initialBodyPosition)<4)&&session.Bots().Bots.Any(b=>b.GetComponent<IModeActor>().ActorId!=interactionTarget&&mystery.Role(b.GetComponent<IModeActor>().ActorId)==MysteryRole.Sheriff);
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&(message.StartsWith("Bot objective contract failed")||stack.Contains("Assets/_DeadlyJenga")||stack.Contains("Assets\\_DeadlyJenga")))
            {runtimeErrors++;File.AppendAllText(Report,"RUNTIME ERROR "+message+"\n");}
        }
        static void OnDamage(GameObject who,float amount){if(who&&who.GetComponent<IModeActor>()?.IsBot==true){damage++;damaged.Add(who.GetInstanceID());}}
        static void OnEliminated(GameObject who,GameObject source){if(who&&who.GetComponent<IModeActor>()?.IsBot==true&&source&&source.GetComponent<IModeActor>()?.IsBot==true)botKills++;}
        static string F(float value)=>value.ToString("F3",CultureInfo.InvariantCulture);
        static void Finish()
        {
            if(restoring)return;restoring=true;
            Application.logMessageReceived-=OnLog;GameEvents.Damaged-=OnDamage;GameEvents.Eliminated-=OnEliminated;
            Time.timeScale=SessionState.GetFloat(Key+"Scale",1);Time.captureFramerate=SessionState.GetInt(Key+"Capture",0);Application.targetFrameRate=SessionState.GetInt(Key+"Target",-1);
            failures+=runtimeErrors;
            SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Restore",true);try{File.AppendAllText(Report,"Finished failures="+failures+" runtimeErrors="+runtimeErrors+". Desktop only.\n");}
            finally{request=null;EditorApplication.ExitPlaymode();}
        }
        static void Restore()
        {
            SessionState.SetBool(Key+"Restore",false);restoring=false;
            var scenes=JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes",""));EditorSceneManager.RestoreSceneManagerSetup(scenes.scenes);
            SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
            MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);MysteryChoices.RequestedRole=(MysteryRole)SessionState.GetInt(Key+"Role",0);
            bool same=EditorSceneManager.GetSceneManagerSetup().Select(s=>s.path).SequenceEqual(scenes.scenes.Select(s=>s.path));
            File.AppendAllText(Report,(same?"PASS ":"FAIL ")+"Original scene setup restored; match choices reapplied.\n");
        }
    }
}
