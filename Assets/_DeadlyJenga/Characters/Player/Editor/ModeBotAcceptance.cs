using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastleBots;

namespace DeadlyJenga.CastlePrototype.Editor
{
    // A small physical map isolates objective execution from the castle's long travel distances.
    [InitializeOnLoad]
    public static class ModeBotAcceptance
    {
        const string Key="ModeBotAcceptance";
        static readonly StringBuilder report=new StringBuilder();
        static double stageAt;
        static int stage,failures,hits;
        static GameObject fixture;
        static ISessionInfo session;
        static IMatchRuntime runtime;
        static bool sawCarrier;
        static bool fullMap,diagnostic;
        static float simulationStarted,originalTimeScale;
        static readonly System.Collections.Generic.Dictionary<string,float> infectedLow=new System.Collections.Generic.Dictionary<string,float>();
        static float infectedRise;
        static int completedRounds;
        static float nextTrace;
        static int maximumCaptures,maximumOwned,maximumInfected;
        static ModeBotAcceptance()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
                report.Clear();stage=0;failures=0;stageAt=EditorApplication.timeSinceStartup;
                fullMap=SessionState.GetBool(Key+"Full",false);diagnostic=SessionState.GetBool(Key+"Diagnostic",false);originalTimeScale=Time.timeScale;
                if(fullMap)Time.timeScale=2;
                EditorApplication.update+=Tick;GameEvents.Damaged+=Damage;
            };
        }
        public static void RunBatch()
        { SessionState.SetBool(Key+"Diagnostic",false);Begin(false); }
        public static void RunCastleBatch()
        { SessionState.SetBool(Key+"Diagnostic",false);Begin(true); }
        public static void RunCtfDiagnosticBatch(){SessionState.SetBool(Key+"Diagnostic",true);Begin(false);}
        static void Begin(bool actualMap)
        {
            SessionState.SetBool(Key+"Full",actualMap);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Damage(GameObject actor,float amount){if(stage==3&&amount>0)hits++;}
        static void BuildMap()
        {
            fixture=new GameObject("Mode bot acceptance island");fixture.transform.position=new Vector3(1000,0,1000);
            var world=fixture.AddComponent<FakeWorldQuery>();world.waterHeight=-20;world.topHeight=.25f;world.radius=8;
            world.spawnPoint=fixture.transform.position+Vector3.up*.31f;
            world.playableBounds=new Bounds(new Vector3(0,2,0),new Vector3(15,6,15));
            for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)
            {
                var tile=GameObject.CreatePrimitive(PrimitiveType.Cube);tile.name="Claimable terrain tile";
                tile.transform.SetParent(fixture.transform);tile.transform.localPosition=new Vector3(x*3,0,z*3);tile.transform.localScale=new Vector3(2.95f,.5f,2.95f);tile.layer=Layers.Surface;
                var body=tile.AddComponent<Rigidbody>();body.isKinematic=true;tile.AddComponent<NavigationClimbStandIn>();world.blocks.Add(body);
            }
            Physics.SyncTransforms();
        }
        static void Configure(GameModeKind kind)
        {
            if(fullMap)
            {
                string map=kind==GameModeKind.CaptureFlag?"ctf":kind==GameModeKind.Infection?"infection":"territory";
                var prefab=Resources.Load<GameObject>("ModeMaps/Mode_"+map);
                if(!prefab)throw new Exception("Mode map prefab missing: "+map);
                var previous=Services.World as Component;
                if(previous){previous.gameObject.SetActive(false);UnityEngine.Object.Destroy(previous.gameObject);}
                var instance=UnityEngine.Object.Instantiate(prefab);
                foreach(var component in instance.GetComponentsInChildren<MonoBehaviour>())
                    if(component is IRebuildable rebuildable){rebuildable.Rebuild();break;}
                report.AppendLine("MAP Mode_"+map+" blocks="+Services.World?.Blocks.Count);
            }
            var rules=ModeDefaults.Create!=null?ModeDefaults.Create(kind):new ModeRules{mode=kind};
            rules.countdownSeconds=.5f;rules.roundSeconds=kind==GameModeKind.BlockControl?30:0;rules.island=IslandRule.Walkable;
            rules.flagTarget=1;rules.flagWinByTwo=false;rules.claimSeconds=1;rules.reclaimSeconds=2;
            rules.starterInfected=kind==GameModeKind.Infection;rules.automaticDisasters=false;
            rules.punchDistance=1;rules.punchCooldown=.85f;
            runtime.Configure(rules);
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();
            int count=fullMap?12:8;
            for(int i=0;i<count;i++)session.SurvivorBots.SpawnOne();
            session.SurvivorBots.AutoFill=false;
            foreach(var member in session.SurvivorBots.Members)if(member&&member.GetComponent<CastleBotBrain>() is CastleBotBrain brain)brain.diagnostics=true;
            Check("Fixture spawned "+count+" real survivor bots for "+kind,session.SurvivorBots.Count==count);
            maximumCaptures=maximumOwned=maximumInfected=hits=0;sawCarrier=false;
            stageAt=EditorApplication.timeSinceStartup;
            simulationStarted=Time.time;nextTrace=Time.time+5;infectedLow.Clear();infectedRise=0;completedRounds=0;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(stage==0)
                {
                    if(EditorApplication.timeSinceStartup-stageAt<8)return;
                    session=Services.Session;runtime=MatchRuntime.Current;
                    if(session==null||runtime==null||!runtime.Authority)throw new Exception("Authoritative Game session did not initialize");
                    session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
                    if(!fullMap)BuildMap();
                    report.AppendLine("MAP "+(fullMap?"actual mode map prefabs":"small physical tile fixture")+" timeScale="+Time.timeScale);
                    Configure(GameModeKind.CaptureFlag);stage=1;return;
                }
                if(EditorApplication.timeSinceStartup-stageAt<1)return;
                if(runtime.Frame.phase==RoundPhase.Waiting)runtime.StartRound();
                var frame=runtime.Frame;
                if(stage==1&&Time.time>=nextTrace)
                {
                    nextTrace=Time.time+5;report.AppendLine("CTF TRACE t="+(Time.time-simulationStarted).ToString("F1"));
                    foreach(var flag in frame.flags)report.AppendLine(" flag team="+flag.team+" atHome="+flag.atHome+" carrier="+flag.carrier+" position="+flag.position+" home="+flag.home);
                    foreach(var player in frame.players)report.AppendLine(" actor="+player.id+" team="+player.team+" alive="+player.alive+" dying="+player.dying+" hp="+player.health+" position="+player.position);
                    foreach(var member in session.SurvivorBots.Members)
                    {
                        if(!member||!(member.GetComponent<CastleBotBrain>() is CastleBotBrain brain))continue;
                        var actor=member.GetComponent<IModeActor>();var rules=runtime.RulesFor(member);
                        report.AppendLine(" MOTOR "+actor.ActorId+" alive="+brain.Motor.Alive+" inputSuspended="+brain.Motor.InputSuspended+" respawning="+brain.Respawning+" moveScale="+rules.moveScale+" objective="+brain.ObjectiveId+" trip="+brain.TripDestination+" menu="+session.MenuOpen+" state="+brain.DebugState);
                    }
                    Directory.CreateDirectory("Logs/Validation");File.WriteAllText(fullMap?"Logs/Validation/ModeBotsCastle.progress.txt":"Logs/Validation/ModeBots.progress.txt",report.ToString());
                }
                foreach(var team in frame.teams){maximumCaptures=Mathf.Max(maximumCaptures,team.captures);maximumOwned=Mathf.Max(maximumOwned,team.ownedBlocks);}
                foreach(var flag in frame.flags)if(!string.IsNullOrEmpty(flag.carrier))sawCarrier=true;
                int infected=0;foreach(var player in frame.players)if(player.role=="infected")
                {
                    infected++;
                    if(!infectedLow.TryGetValue(player.id,out float low)||player.position.y<low)infectedLow[player.id]=player.position.y;
                    infectedRise=Mathf.Max(infectedRise,player.position.y-infectedLow[player.id]);
                }
                maximumInfected=Mathf.Max(maximumInfected,infected);
                double elapsed=Time.time-simulationStarted;
                if(diagnostic&&elapsed>=30){Snapshot();report.AppendLine("DIAGNOSTIC ONLY: does not constitute CTF acceptance.");Finish();return;}
                if(fullMap&&stage==1&&(frame.phase==RoundPhase.Results||frame.phase==RoundPhase.Victory)&&elapsed<240)
                {completedRounds++;runtime.StartRound();}
                bool finished=stage==1?(fullMap?elapsed>=240:maximumCaptures>0):stage==2?(frame.phase==RoundPhase.Victory||frame.phase==RoundPhase.Results):maximumInfected>1&&hits>0&&(!fullMap||infectedRise>2);
                if(!finished&&elapsed<(fullMap?(stage==3?180:stage==2?90:245):(stage==3?65:stage==1?120:50)))return;
                if(stage==1)
                {
                    Check("Eight ordinary bots acquire and capture a flag",sawCarrier&&maximumCaptures>0);
                    Snapshot();Configure(GameModeKind.BlockControl);stage=2;
                }
                else if(stage==2)
                {
                    Check("Ordinary bot standing claims blocks and earns ownership points",maximumOwned>=2&&AnyPoints(frame));
                    Check("Block control runs to a complete results/victory phase",frame.phase==RoundPhase.Results||frame.phase==RoundPhase.Victory);
                    Snapshot();Configure(GameModeKind.Infection);stage=3;
                }
                else
                {
                    Check("Starter infected bot lands legitimate damaging attacks and converts another player",maximumInfected>1&&hits>0);
                    if(fullMap)Check("Infected participants climb from lower map regions",infectedRise>2);
                    Snapshot();Finish();
                }
            }
            catch(Exception error){report.AppendLine(error.ToString());failures++;Finish();}
        }
        static bool AnyPoints(MatchFrame frame){foreach(var team in frame.teams)if(team.points>0)return true;return false;}
        static void Check(string title,bool pass){report.AppendLine((pass?"PASS ":"FAIL ")+title);if(!pass)failures++;}
        static void Snapshot()
        {
            report.AppendLine("MEASURED mode="+runtime.Rules.mode+" phase="+runtime.Frame.phase+" carriers="+sawCarrier+" captures="+maximumCaptures+" owned="+maximumOwned+" infected="+maximumInfected+" damaging hits="+hits+" infected rise="+infectedRise+" replayed rounds="+completedRounds+" match elapsed="+runtime.Frame.elapsed+" simulated seconds="+(Time.time-simulationStarted));
            foreach(var actor in session.SurvivorBots.Members)
            {
                var bot=actor.GetComponent<CastleBotBrain>();if(!bot)continue;
                report.AppendLine(bot.name+" objective="+bot.ObjectiveId+" goalsReached="+bot.ObjectivesReached+" routeFailures="+bot.RouteFailures+" jumps="+bot.Jumps+" climbs="+bot.Climbs+" feet="+bot.transform.position+" intent="+bot.Intent);
            }
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;GameEvents.Damaged-=Damage;SessionState.SetBool(Key,false);
            Time.timeScale=originalTimeScale;
            report.AppendLine("Failures: "+failures+". Initial normal mode spawning only; no test teleports, scripted damage, simulated captures, or invented scores.");
            report.AppendLine("Not verified: real peers, arbitrary castle/cathedral objectives, Quest frame/thermal budget, 50/100 simultaneous bots.");
            Directory.CreateDirectory("Logs/Validation");File.WriteAllText(fullMap?"Logs/Validation/ModeBotsCastle.txt":"Logs/Validation/ModeBots.txt",report.ToString());
            EditorApplication.ExitPlaymode();
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(failures==0?0:1);
        }
    }
}
