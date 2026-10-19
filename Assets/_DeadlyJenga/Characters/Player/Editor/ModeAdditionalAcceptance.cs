using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastleBots;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class ModeAdditionalAcceptance
    {
        const string Key="ModeAdditionalAcceptance";
        static readonly StringBuilder report=new StringBuilder();
        static readonly Dictionary<CastleBotBrain,float> starts=new Dictionary<CastleBotBrain,float>();
        static ISessionInfo session;static IMatchRuntime runtime;
        static int selection,stage,hits,hazards,failures,maxCaptures,maxInfected;
        static float begun,nextTrace,maxHill,travel,startAt;static double boot;static bool initialized,pendingStart;
        static ModeAdditionalAcceptance()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
                selection=SessionState.GetInt(Key+"Selection",0);stage=0;initialized=false;failures=0;boot=EditorApplication.timeSinceStartup;report.Clear();
                Time.timeScale=2;GameEvents.Damaged+=Damage;GameEvents.HazardStarted+=Hazard;EditorApplication.update+=Tick;
            };
        }
        public static void RunRemainingBatch()=>Begin(0);
        public static void RunDynamicCtfBatch()=>Begin(1);
        public static void RunDynamicInfectionBatch()=>Begin(2);
        public static void RunKohBatch()=>Begin(3);
        static void Begin(int choice)
        {SessionState.SetInt(Key+"Selection",choice);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();}
        static void Damage(GameObject actor,float amount){if(amount>0)hits++;}
        static void Hazard(HazardType type,Vector3 position){hazards++;report.AppendLine("REAL HAZARD "+(type?type.name:"unknown")+" at "+position);}
        static void Configure(GameModeKind kind,string map)
        {
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();
            var previous=Services.World as Component;if(previous){previous.gameObject.SetActive(false);UnityEngine.Object.Destroy(previous.gameObject);}
            var prefab=Resources.Load<GameObject>("ModeMaps/Mode_"+map);if(!prefab)throw new Exception("Missing actual map "+map);
            var instance=UnityEngine.Object.Instantiate(prefab);foreach(var c in instance.GetComponentsInChildren<MonoBehaviour>())if(c is IRebuildable builder){builder.Rebuild();break;}
            var rules=ModeDefaults.Create(kind);rules.countdownSeconds=.5f;rules.island=IslandRule.Walkable;
            rules.automaticDisasters=selection==1||selection==2||kind==GameModeKind.Survival;
            rules.starterInfected=kind==GameModeKind.Infection;
            if(kind==GameModeKind.TeamTickets){rules.tickets=1;rules.punchDamage=100;rules.fallDamage=false;}
            if(kind==GameModeKind.Survival){rules.roundSeconds=45;rules.disasterMinInterval=rules.disasterMaxInterval=8;rules.disasterWarningSeconds=2;rules.maxConcurrentDisasters=1;}
            else rules.roundSeconds=selection==0||selection==3?300:600;
            runtime.Configure(rules);report.AppendLine("RECOMMENDED initialCount="+session.SurvivorBots.Count+" status="+session.SurvivorBots.Status);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();for(int i=0;i<12;i++)session.SurvivorBots.SpawnOne();
            Check(session.SurvivorBots.Count==12,"Actual map spawns12 ordinary bot controllers; count="+session.SurvivorBots.Count+" status="+session.SurvivorBots.Status);
            starts.Clear();foreach(var member in session.SurvivorBots.Members)if(member&&member.GetComponent<CastleBotBrain>() is CastleBotBrain brain)starts[brain]=brain.TravelDistance;
            hits=hazards=maxCaptures=maxInfected=0;maxHill=travel=0;begun=Time.time;nextTrace=begun+10;
            pendingStart=true;startAt=Time.time+.7f;report.AppendLine("MAP Mode_"+map+" blocks="+Services.World.Blocks.Count+" mode="+kind+" scheduler="+rules.automaticDisasters+" interval="+rules.disasterMinInterval+".."+rules.disasterMaxInterval+" timeScale=2 (not a performance test)");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(!initialized)
                {
                    if(EditorApplication.timeSinceStartup-boot<8)return;
                    session=Services.Session;runtime=MatchRuntime.Current;if(session==null||runtime==null||!runtime.Authority)throw new Exception("Authoritative Game not ready");
                    session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
                    initialized=true;
                    if(selection==1)Configure(GameModeKind.CaptureFlag,"ctf");else if(selection==2)Configure(GameModeKind.Infection,"infection");else Configure(GameModeKind.KingOfHill,"koth");return;
                }
                if(pendingStart){if(Time.time<startAt)return;pendingStart=false;runtime.StartRound();begun=Time.time;nextTrace=begun+10;return;}
                var frame=runtime.Frame;float elapsed=Time.time-begun;
                foreach(var team in frame.teams)maxCaptures=Mathf.Max(maxCaptures,team.captures);
                int infected=0;foreach(var p in frame.players){if(p.bot)maxHill=Mathf.Max(maxHill,p.metric);if(p.role=="infected")infected++;}maxInfected=Mathf.Max(maxInfected,infected);
                foreach(var pair in starts)if(pair.Key)travel=Mathf.Max(travel,pair.Key.TravelDistance-pair.Value);
                if(Time.time>=nextTrace)
                {
                    nextTrace=Time.time+10;report.AppendLine("TRACE elapsed="+elapsed+" match="+frame.elapsed+" phase="+frame.phase+" captures="+maxCaptures+" infected="+maxInfected+" hillMetric="+maxHill+" damagingHits="+hits+" hazards="+hazards+" maxActualTravel="+travel+" bots="+session.SurvivorBots.Count+" hill="+frame.hill);
                    if(selection==3||selection==0&&stage==0||selection==1&&(int)(elapsed/10)%3==0)
                        foreach(var member in session.SurvivorBots.Members)if(member&&member.GetComponent<CastleBotBrain>() is CastleBotBrain brain)
                        {
                            var actor=(IModeActor)brain.Motor;
                            report.AppendLine(" BOT "+actor.ActorId+" team="+actor.Team+" feet="+brain.transform.position+" alive="+brain.Motor.Alive+" grounded="+brain.Motor.Grounded+" carrying="+runtime.RulesFor(member).carryingFlag+" trip="+brain.TripDestination+" objective="+brain.ObjectiveId+" goal="+brain.Goal+" intent="+brain.Intent+" routesFailed="+brain.RouteFailures+" climbs="+brain.Climbs);
                        }
                    if(selection==1)foreach(var flag in frame.flags)
                    {
                        bool blocked=Physics.CheckCapsule(flag.home+Vector3.up*.38f,flag.home+Vector3.up*1.55f,.30f,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore);
                        report.AppendLine(" HOME team="+flag.team+" capsuleBlocked="+blocked);
                    }
                    foreach(var flag in frame.flags)report.AppendLine(" FLAG "+flag.team+" carrier="+flag.carrier+" pos="+flag.position+" home="+flag.home);
                    Save(true);
                }
                bool done=false;
                if(selection==1)
                {
                    done=maxCaptures>0&&hazards>0;
                    if(!done&&elapsed<600&&frame.phase!=RoundPhase.Results)return;
                    Check(maxCaptures>0,"Actual CTF bots complete a natural capture across authored fortress routes");Check(hazards>0,"Default90..120s scheduler genuinely launches a disaster during CTF");Finish();return;
                }
                if(selection==2)
                {
                    done=maxInfected>1&&hazards>0;
                    if(!done&&elapsed<360&&frame.phase!=RoundPhase.Results)return;
                    Check(maxInfected>1,"Actual Infection round naturally converts another participant");Check(hazards>0,"Default scheduler launches real hazards in Infection");Finish();return;
                }
                if(stage==0)
                {
                    if(maxHill<=0&&elapsed<240)return;
                    Check(maxHill>0,"Ordinary KOH bot reaches real hill region and earns control seconds");if(selection==3){Finish();return;}stage=1;Configure(GameModeKind.TeamTickets,"tickets");return;
                }
                if(stage==1)
                {
                    bool depleted=false;foreach(var team in frame.teams)if(team.tickets==0)depleted=true;
                    if(!(depleted&&hits>0)&&elapsed<120)return;
                    Check(depleted&&hits>0,"Legitimate bot punches deplete an initially one-ticket team reserve");stage=2;Configure(GameModeKind.Survival,"survival");return;
                }
                if(frame.elapsed<45&&elapsed<90&&frame.phase!=RoundPhase.Results&&frame.phase!=RoundPhase.Victory)return;
                Check(hazards>0,"Survival uses actual scheduler warning and hazard launch (8s test interval)");Check(travel>2,"Survival bots physically travel toward safe support during real scheduled disaster");Finish();
            }
            catch(Exception error){report.AppendLine("FAIL "+error);failures++;Finish();}
        }
        static void Check(bool pass,string message){report.AppendLine((pass?"PASS ":"FAIL ")+message);if(!pass)failures++;}
        static string Name=>selection==3?"KohRoutes":selection==1?"DynamicCtf":selection==2?"DynamicInfection":"RemainingModes";
        static void Save(bool progress){Directory.CreateDirectory("Logs/Validation");File.WriteAllText("Logs/Validation/"+Name+(progress?".progress":"")+".txt",report.ToString());}
        static void Finish()
        {
            EditorApplication.update-=Tick;GameEvents.Damaged-=Damage;GameEvents.HazardStarted-=Hazard;SessionState.SetBool(Key,false);
            report.AppendLine("Failures="+failures+". No test damage, block force, score assignments or participant teleports; normal mode spawning only. Quest and real peers not verified.");Save(false);Time.timeScale=1;EditorApplication.ExitPlaymode();
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(failures==0?0:1);
        }
    }
}

