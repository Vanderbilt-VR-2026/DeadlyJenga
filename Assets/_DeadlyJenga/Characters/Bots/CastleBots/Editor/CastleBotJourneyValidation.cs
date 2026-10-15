using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CastleBots.Editor
{
    // Explicit menu action only. No watcher, automatic Play Mode or scene save.
    public static class CastleBotJourneyValidation
    {
        [MenuItem("Tools/Castle Bots/Observe Castle Exploration (30 Seconds)")]
        public static void ObserveCastle()
        {
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            if(!Application.isPlaying||!session)throw new InvalidOperationException("Run the castle demo first.");
            session.StartCoroutine(Observe(session));
        }
        static IEnumerator Observe(CastlePlaySession session)
        {
            var temporary=new List<CastleBotBrain>();
            var previousDiagnostics=new Dictionary<CastleBotBrain,bool>();
            var previous=new Dictionary<CastleBotBrain,Vector3>();
            var travel=new Dictionary<CastleBotBrain,float>();
            var bounds=new Dictionary<CastleBotBrain,Vector2>();
            var lines=new List<string>();
            // Keep the human and existing bots where they are. If the demo is
            // empty, create one ordinary survivor solely for this observation.
            if(session.Bots.Count==0){var spawned=session.Bots.SpawnBot();if(spawned)temporary.Add(spawned);}
            try
            {
                foreach(var bot in session.Bots.Bots)
                {
                    if(!bot)continue;previousDiagnostics[bot]=bot.diagnostics;bot.diagnostics=true;
                    previous[bot]=bot.transform.position;travel[bot]=0;bounds[bot]=new Vector2(bot.transform.position.y,bot.transform.position.y);
                }
                if(previous.Count==0){lines.Add("No survivor bots available to observe.");yield break;}
                for(int sample=0;sample<60;sample++)
                {
                    yield return new WaitForSeconds(.5f);
                    foreach(var entry in previousDiagnostics)
                    {
                        var bot=entry.Key;if(!bot)continue;var point=bot.transform.position;var delta=point-previous[bot];
                        // Exclude teleports/respawns from navigation distance.
                        if(!bot.Respawning&&delta.magnitude<4)travel[bot]+=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude;
                        previous[bot]=point;
                        if(bot.Motor.Grounded&&!bot.Respawning){var span=bounds[bot];bounds[bot]=new Vector2(Mathf.Min(span.x,point.y),Mathf.Max(span.y,point.y));}
                        lines.Add(sample*.5f+" "+bot.name+" position="+point+" "+bot.DebugState+" intent="+bot.Intent+" activity="+bot.Activity+" goal="+bot.Goal+" trip="+bot.TripDestination+" steps="+bot.RemainingWaypoints+" respawns="+bot.Respawns);
                    }
                }
                foreach(var entry in previousDiagnostics)
                {
                    var bot=entry.Key;if(!bot)continue;var span=bounds[bot];
                    lines.Add("SUMMARY "+bot.name+" horizontal travel="+travel[bot]+" grounded height range="+(span.y-span.x)+" respawns="+bot.Respawns+" jumps="+bot.Jumps+" vaults="+bot.Motor.VaultCount);
                }
            }
            finally
            {
                Directory.CreateDirectory("ArtDirection/CastleBots");File.WriteAllLines("ArtDirection/CastleBots/CastleExploration.txt",lines);
                foreach(var entry in previousDiagnostics)if(entry.Key)entry.Key.diagnostics=entry.Value;
                foreach(var bot in temporary)if(bot){bot.gameObject.SetActive(false);Object.Destroy(bot.gameObject);}
            }
        }
        [MenuItem("Tools/Castle Bots/Check Gap Execution (Play Mode)")]
        public static void RunExecution()
        {
            Run();
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            session.StartCoroutine(Execute(session));
        }
        static IEnumerator Execute(CastlePlaySession session)
        {
            var bot=session.Bots.SpawnBot();
            if(!bot){Debug.LogWarning("Gap execution needs one free NPC slot.");yield break;}
            var bodies=new List<Rigidbody>();var root=new GameObject("Temporary gap execution fixtures");
            var lines=new List<string>();bot.diagnostics=true;
            Rigidbody Box(string name,Vector3 position,Vector3 size)
            {
                var go=new GameObject(name);go.transform.SetParent(root.transform);go.transform.position=position;go.transform.localScale=size;go.AddComponent<BoxCollider>();
                var rb=go.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;go.AddComponent<CastleBlock>().SyncSurface();session.generator.Bodies.Add(rb);bodies.Add(rb);return rb;
            }
            try
            {
                Box("Lower",new Vector3(80,79,0),new Vector3(3,2,3));
                Box("Upper",new Vector3(84.6f,81,0),new Vector3(3,2,3));
                Box("Low roof",new Vector3(80,82.2f,0),new Vector3(3,.4f,3));Physics.SyncTransforms();
                bot.Motor.Teleport(new Vector3(80,80.06f,0),Quaternion.LookRotation(Vector3.right));bot.Spawned(false);bot.SetJourneyDestination(new Vector3(83.5f,82,0));
                int initial=bot.Motor.VaultCount;bool ascended=false,descended=false;
                for(int i=0;i<48;i++)
                {
                    yield return new WaitForSeconds(.2f);
                    lines.Add("UP "+bot.transform.position+" "+bot.DebugState+" "+bot.Intent+" target="+bot.Goal);
                    if(bot.Motor.Grounded&&bot.transform.position.y>81.8f){ascended=true;break;}
                }
                lines.Add((ascended&&bot.Motor.VaultCount>initial?"PASS ":"FAIL ")+"Actual motor stages under a roof, jumps a gap and vaults onto a 2m higher crate.");
                if(ascended)
                {
                    bot.SetJourneyDestination(new Vector3(80,80,0));
                    for(int i=0;i<48;i++)
                    {
                        yield return new WaitForSeconds(.2f);
                        lines.Add("DOWN "+bot.transform.position+" "+bot.DebugState+" "+bot.Intent+" target="+bot.Goal);
                        if(bot.Motor.Grounded&&bot.transform.position.x<81.5f&&Mathf.Abs(bot.transform.position.y-80)<.2f){descended=true;break;}
                    }
                }
                lines.Add((descended?"PASS ":"FAIL ")+"Actual motor returns across the lower gap without falling away.");
                Directory.CreateDirectory("ArtDirection/CastleBots");File.WriteAllLines("ArtDirection/CastleBots/JourneyExecution.txt",lines);
            }
            finally
            {
                foreach(var rb in bodies)session.generator.Bodies.Remove(rb);
                root.SetActive(false);Object.Destroy(root);bot.gameObject.SetActive(false);Object.Destroy(bot.gameObject);Physics.SyncTransforms();
            }
        }
        [MenuItem("Tools/Castle Bots/Check Gap Links (Play Mode)")]
        public static void Run()
        {
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            if(!Application.isPlaying||!session||!session.Player)throw new InvalidOperationException("Run the Game scene first; this check uses the current survivor dimensions.");
            var root=new GameObject("Temporary journey link checks");var results=new List<string>();var motor=session.Player;
            void Check(bool pass,string label)=>results.Add((pass?"PASS ":"FAIL ")+label);
            GameObject Box(string name,Vector3 position,Vector3 size)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform);go.transform.position=position;go.transform.localScale=size;go.layer=CastleBlock.SurfaceLayer;go.GetComponent<Renderer>().enabled=false;return go;
            }
            try
            {
                Box("Lower ledge",new Vector3(80,79,0),new Vector3(3,2,3));
                Box("Higher ledge",new Vector3(84.6f,81,0),new Vector3(3,2,3));
                Box("Low roof over takeoff",new Vector3(80,82.2f,0),new Vector3(3,.4f,3));Physics.SyncTransforms();
                var center=new Vector3(80,80.01f,0);var lip=new Vector3(81.15f,80.01f,0);var upper=new Vector3(83.5f,82,0);
                Check(CastleBotNavigation.Walkable(motor,center,lip),"Can stage from a low-roof platform centre to its launch edge.");
                Check(CastleBotNavigation.TryLink(motor,lip,upper,out var up,out float upSpeed)&&up==CastleBotRoute.Vault&&upSpeed<=motor.walkSpeed,"Can cross a no-floor gap and catch a ledge 2m higher.");
                Check(!CastleBotNavigation.TryLink(motor,center,upper,out _,out _),"Does not jump through the low roof from the platform centre.");
                Check(CastleBotNavigation.TryLink(motor,new Vector3(83.45f,82.01f,0),new Vector3(81.1f,80,0),out var down,out float downSpeed)&&down==CastleBotRoute.Drop&&downSpeed<=motor.walkSpeed,"Can descend across the gap with a controlled walk-off/drop.");
                Check(!CastleBotNavigation.TryLink(motor,lip,new Vector3(95,82,0),out _,out _),"Rejects an unreachable gap instead of inventing an aerial step.");
                Box("Blocked destination",new Vector3(83.65f,82.5f,0),new Vector3(.8f,1,1));Physics.SyncTransforms();
                Check(!CastleBotNavigation.Landing(motor,upper,out _),"Rejects a destination without standing clearance.");
                Directory.CreateDirectory("ArtDirection/CastleBots");File.WriteAllLines("ArtDirection/CastleBots/JourneyLinkValidation.txt",results);
                foreach(var line in results)Debug.Log(line);
            }
            finally{root.SetActive(false);Object.Destroy(root);Physics.SyncTransforms();}
        }
    }
}
