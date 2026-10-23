using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastleBots;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CarrierFortressRouteValidation
    {
        const string Key="CarrierFortressRouteValidation";
        static readonly StringBuilder report=new StringBuilder();
        static bool groundStart;static CastleBotBrain bot;static float began,nextTrace,minHeight;static int failures;static double boot;
        sealed class ReturnGoal:IBotObjectives {public void Collect(GameObject actor,List<BotObjective> result){result.Add(new BotObjective{id="return",kind=BotObjectiveKind.Travel,position=destination,radius=1,priority=1.5f});}}
        static IBotObjectives previousObjectives;
        static readonly Vector3 destination=new Vector3(-40,36.07f,-5);
        static CarrierFortressRouteValidation()
        {
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){report.Clear();failures=0;bot=null;groundStart=SessionState.GetBool(Key+"Ground",false);boot=EditorApplication.timeSinceStartup;Time.timeScale=2;EditorApplication.update+=Tick;}};
        }
        public static void RunBatch(){SessionState.SetBool(Key+"Ground",false);Begin();}
        public static void RunGroundBatch(){SessionState.SetBool(Key+"Ground",true);Begin();}
        static void Begin(){SessionState.SetBool(Key,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        static void Build()
        {
            var prefab=Resources.Load<GameObject>("ModeMaps/Mode_ctf");if(!prefab)throw new Exception("ActualCTFprefab missing");var map=UnityEngine.Object.Instantiate(prefab);
            foreach(var component in map.GetComponentsInChildren<MonoBehaviour>())if(component is IRebuildable build){build.Rebuild();break;}
            report.AppendLine("Actual Mode_ctf groundStart="+groundStart+" blocks="+Services.World.Blocks.Count+" initial carrier support=(-38.05,16.06,0) target="+destination+" TimeScale2");
            var survivor=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleSurvivor.prefab")).GetComponent<CastleSurvivor>();
            var hand=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleFlyingHand.prefab")).GetComponent<CastleHandActor>();
            var host=new GameObject("Temporary actual-map carrier controller");var population=host.AddComponent<CastleBotPopulation>();
            population.Initialize(new BotWorld{Player=survivor,Hand=hand,survivorPrefab=survivor,handPrefab=hand,Bots=population});
            previousObjectives=BotObjectives.Current;BotObjectives.Current=new ReturnGoal();
            bot=population.SpawnBot();if(!bot)throw new Exception("Could not spawn actual survivor controller");
            bot.diagnostics=true;bot.Personality.exploration=0;bot.Personality.goofiness=0;bot.Personality.journeyDrive=1;
            bot.Motor.ApplyModeRules(new ModeRules{mode=GameModeKind.CaptureFlag,carryingFlag=true,canClimb=false,moveScale=.65f,vaultScale=.55f,healthEnabled=true,fallDamage=false,island=IslandRule.Walkable,automaticDisasters=false});
            bot.Motor.Teleport(groundStart?new Vector3(-34.39f,.06f,13.11f):new Vector3(-38.05f,16.06f,0),Quaternion.identity);bot.SetJourneyDestination(destination,500);began=Time.time;nextTrace=began;minHeight=16;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(!bot){if(EditorApplication.timeSinceStartup-boot<2)return;Build();return;}
                minHeight=Mathf.Min(minHeight,bot.transform.position.y);
                if(Time.time>=nextTrace){nextTrace=Time.time+5;report.AppendLine("TRACE t="+(Time.time-began)+" feet="+bot.transform.position+" hp="+bot.Motor.Health+" goal="+bot.Goal+" trip="+bot.TripDestination+" intent="+bot.Intent+" state="+bot.DebugState+" vaults="+bot.Motor.VaultCount+" climbs="+bot.Climbs+" search="+bot.LastRouteSearch+" change="+bot.LastRouteChange+" recovery="+bot.RecoveryDiagnostics);if((int)(Time.time-began)%30<5)AuditEdges();Save(true);}
                bool reached=Vector3.Distance(bot.transform.position,destination)<2&&bot.Motor.Grounded;
                if(!reached&&bot.Motor.Alive&&Time.time-began<240)return;
                bool pass=reached&&bot.Climbs==0;report.AppendLine((pass?"PASS ":"FAIL ")+"Carrier exits intermediate hollow roof and returns via genuine jump/slow-vault paths; minY="+minHeight+" climbs="+bot.Climbs+" search="+bot.LastRouteSearch+" change="+bot.LastRouteChange+" recovery="+bot.RecoveryDiagnostics);if(!pass)failures++;Finish();
            }
            catch(Exception error){report.AppendLine("FAIL "+error);failures++;Finish();}
        }
        static void AuditEdges()
        {
            var points=new List<Vector3>();int valid=0,linked=0;
            foreach(var body in Services.World.Blocks)
            {
                if(!body||Vector3.Distance(body.position,bot.transform.position)>10)continue;var box=body.GetComponent<BoxCollider>();if(!box)continue;
                CastleBotNavigation.SurfaceSamples(body,box,points);
                foreach(var p in points)
                {
                    if(Vector3.Distance(p,bot.transform.position)<.8f||Vector3.Distance(p,bot.transform.position)>7||!CastleBotNavigation.Landing(bot.Motor,p,out _))continue;valid++;
                    if(!CastleBotNavigation.TryLink(bot.Motor,bot.transform.position,p,out var route,out _))continue;
                    linked++;if(linked<15)report.AppendLine(" EDGE "+route+" to="+p+" body="+body.name+" standingBody="+(body==bot.Motor.StandingBody));
                }
            }
            report.AppendLine(" EDGES valid="+valid+" linked="+linked);
        }
        static void Save(bool progress){Directory.CreateDirectory("Logs/Validation");File.WriteAllText("Logs/Validation/"+(groundStart?"CarrierGround":"CarrierFortress")+(progress?".progress":"")+".txt",report.ToString());}
        static void Finish(){BotObjectives.Current=previousObjectives;EditorApplication.update-=Tick;SessionState.SetBool(Key,false);report.AppendLine("Failures="+failures+"; initial fixture placement only, no later teleports, scores, damage or block commands.");Save(false);Time.timeScale=1;EditorApplication.ExitPlaymode();if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(failures==0?0:1);}
    }
}

