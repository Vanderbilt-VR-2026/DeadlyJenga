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
    public sealed class NavigationClimbStandIn : MonoBehaviour, IClimbable
    {
        public bool CanClimb=>true;
        public float Grip=>1;
        public Transform Anchor=>transform;
        public Rigidbody Body=>GetComponent<Rigidbody>();
        public Transform Visual=>null;
    }

    [InitializeOnLoad]
    public static class BotNavigationValidation
    {
        const string Key="BotNavigationValidation";
        static GameObject fixture;
        static FakeWorldQuery world;
        static CastleBotBrain bot;
        static CastleBotPopulation population;
        static Vector3 goal;
        static double started;
        static int stage,failures,carrierStepVaults,carrierStepClimbs;
        static double nextTrace;
        static readonly StringBuilder report=new StringBuilder();
        static BotNavigationValidation()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
                EditorApplication.update+=Tick;
                try{Build();}catch(Exception error){report.AppendLine(error.ToString());failures++;Finish();}
            };
        }
        public static void RunBatch()
        {
            SessionState.SetString(Key+"Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static Rigidbody Box(string name,Vector3 position,Vector3 scale,Quaternion rotation)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(fixture.transform);
            go.transform.SetPositionAndRotation(position,rotation);go.transform.localScale=scale;go.layer=Layers.Surface;
            var body=go.AddComponent<Rigidbody>();body.isKinematic=true;go.AddComponent<NavigationClimbStandIn>();world.blocks.Add(body);return body;
        }
        static void Build()
        {
            report.Clear();stage=0;failures=0;
            fixture=new GameObject("Temporary real bot route checks");world=fixture.AddComponent<FakeWorldQuery>();world.waterHeight=-20;world.topHeight=5;world.radius=20;
            Box("Start",new Vector3(0,0,0),new Vector3(4,.5f,4),Quaternion.identity);
            Box("Gap landing",new Vector3(0,0,4),new Vector3(2,.5f,2),Quaternion.identity);
            Box("Climb course A",new Vector3(10,1.25f,3),new Vector3(2,2,2),Quaternion.Euler(0,45,0));
            Box("Climb course B",new Vector3(10,3.29f,3),new Vector3(2,2,2),Quaternion.Euler(0,45,0));
            Box("Climb start",new Vector3(10,0,0),new Vector3(6,.5f,5),Quaternion.identity);
            var inverted=Box("Inverted surface",new Vector3(30,0,0),new Vector3(2,1,4),Quaternion.Euler(0,0,180));
            var points=new System.Collections.Generic.List<Vector3>();CastleBotNavigation.SurfaceSamples(inverted,inverted.GetComponent<BoxCollider>(),points);
            bool sampled=points.Count>0&&points.TrueForAll(p=>Mathf.Abs(p.y-.5f)<.01f);
            report.AppendLine((sampled?"PASS ":"FAIL ")+"World-up sampling finds inverted block top");if(!sampled)failures++;
            var survivor=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleSurvivor.prefab")).GetComponent<CastleSurvivor>();
            var hand=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleFlyingHand.prefab")).GetComponent<CastleHandActor>();
            population=fixture.AddComponent<CastleBotPopulation>();
            var session=new BotWorld{Player=survivor,Hand=hand,survivorPrefab=survivor,handPrefab=hand,Bots=population};population.Initialize(session);
            bot=population.SpawnBot();if(!bot)throw new Exception("Fixture bot did not spawn");
            bot.diagnostics=true;
            var social=bot.gameObject.AddComponent<SocialBotDriver>();
            social.Tune(new SocialBotTuning{bravery=.8f,pace=1,decisionInterval=.2f,journeyDrive=1,exploration=0,restSeconds=0});
            social.LookTarget=()=>bot.transform.position-bot.transform.forward*10;
            Box("Edge support",new Vector3(50,0,0),new Vector3(4,.5f,4),Quaternion.identity);
            Box("Adjacent wall",new Vector3(52.1f,1.25f,0),new Vector3(.2f,2,4),Quaternion.identity);
            Box("Underside without floor",new Vector3(70,3,0),new Vector3(4,.5f,4),Quaternion.identity);
            Box("Carrier stair lower",new Vector3(90,0,0),new Vector3(6,.5f,6),Quaternion.identity);
            Box("Carrier stair upper",new Vector3(94,1.25f,0),new Vector3(2,2,6),Quaternion.identity);
            bot.Personality.exploration=0;bot.Personality.goofiness=0;bot.Personality.journeyDrive=1;
            bot.Motor.Teleport(new Vector3(0,.31f,1.1f),Quaternion.identity);
            goal=new Vector3(0,.29f,4);bot.SetJourneyDestination(goal,30);started=EditorApplication.timeSinceStartup;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||!bot)return;
            if(EditorApplication.timeSinceStartup>nextTrace)
            {
                nextTrace=EditorApplication.timeSinceStartup+1;
                report.AppendLine("TRACE stage="+stage+" feet="+bot.transform.position+" goal="+bot.Goal+" grounded="+bot.Motor.Grounded+" paused="+bot.Motor.InputSuspended+" capsule="+bot.Motor.Capsule.enabled+" state="+bot.DebugState);
            }
            double elapsed=EditorApplication.timeSinceStartup-started;
            if(stage==4)
            {
                if(elapsed<.35)return;
                bool supported=bot.Motor.Grounded;bot.Motor.Simulate(Vector2.zero,true,.02f);
                bool jumps=bot.Motor.VerticalSpeed>0;
                report.AppendLine((supported&&jumps?"PASS ":"FAIL ")+"Wall-adjacent floor remains grounded and permits a real jump");if(!supported||!jumps)failures++;
                bot.Motor.Teleport(new Vector3(70,1.5f,0),Quaternion.identity);stage=5;started=EditorApplication.timeSinceStartup;return;
            }
            if(stage==5)
            {
                if(elapsed<.3)return;
                report.AppendLine((!bot.Motor.Grounded?"PASS ":"FAIL ")+"Underside and distant ground do not grant support");if(bot.Motor.Grounded)failures++;Finish();return;
            }
            bool arrived=Vector3.Distance(bot.transform.position,goal)<.6f&&bot.Motor.Grounded;
            if(stage==3)arrived&=bot.Motor.VaultCount>carrierStepVaults&&bot.Climbs==carrierStepClimbs;
            if(!arrived&&elapsed<18)return;
            string title=stage==0?"Socially distracted bot crosses a gap":stage==1?"Socially distracted bot climbs yaw tower through seam":stage==2?"Slower flag carrier crosses gap using permitted jump/vault":"Flag carrier executes actual 2m stair jump and slow vault without climbing";
            report.AppendLine((arrived?"PASS ":"FAIL ")+title+" position="+bot.transform.position+" jumps="+bot.Jumps+" climbs="+bot.Climbs+" failures="+bot.RouteFailures+" intent="+bot.Intent);
            if(!arrived)failures++;
            stage++;started=EditorApplication.timeSinceStartup;
            if(stage==1)
            {
                bot.Motor.Teleport(new Vector3(10,.31f,1.1f),Quaternion.identity);
                goal=new Vector3(10,4.33f,2.8f);bot.SetJourneyDestination(goal,30);
                bool link=CastleBotNavigation.ClimbLink(bot.Motor,bot.transform.position,goal,out var face);
                report.AppendLine("TRACE direct climb link="+link+" face="+face.point+" normal="+face.normal+" stamina="+bot.Motor.GripRemaining);
            }
            else if(stage==2)
            {
                bot.Motor.ConfigureActions(false,true,.65f,.55f);bot.Motor.Teleport(new Vector3(0,.31f,1.1f),Quaternion.identity);
                goal=new Vector3(0,.29f,4);bot.SetJourneyDestination(goal,30);
            }
            else if(stage==3)
            {
                bot.Motor.Teleport(new Vector3(91.5f,.31f,0),Quaternion.Euler(0,90,0));
                goal=new Vector3(94,2.30f,0);bot.SetJourneyDestination(goal,30);carrierStepVaults=bot.Motor.VaultCount;carrierStepClimbs=bot.Climbs;
            }
            else
            {
                bot.Motor.ConfigureActions(false,false);bot.Motor.InputSuspended=true;bot.Motor.Teleport(new Vector3(51.96f,.28f,0),Quaternion.identity);
            }
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;SessionState.SetBool(Key,false);
            report.AppendLine("Failures: "+failures+". Initial fixture placement only; routes use actual bot commands and survivor physics. No mid-route teleporting.");
            report.AppendLine("Not verified: 100-bot Quest performance, real multiplayer, arbitrary map reachability.");
            Directory.CreateDirectory("Logs/Validation");File.WriteAllText("Logs/Validation/BotNavigation.txt",report.ToString());
            EditorApplication.ExitPlaymode();
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(failures==0?0:1);
        }
    }
}
