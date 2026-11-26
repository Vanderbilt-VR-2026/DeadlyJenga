using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class TidalCircuitChecks
    {
        const string Key="TidalCircuitChecks";
        const string Folder="Logs/Captures/tidal-circuit";
        static int failed;
        static TidalCircuitChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                    EditorApplication.delayCall+=()=> {
                        var session=Object.FindFirstObjectByType<CastlePlaySession>();
                        if(!session){Check(false,"Game session missing");EditorApplication.ExitPlaymode();return;}
                        session.StartCoroutine(RunSteps(session));
                    };
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key,false);
                    int code=SessionState.GetInt(Key+"Result",1);
                    if(SessionState.GetString("DJWarm.id","")!="")
                    {Directory.CreateDirectory("Logs/Warm");File.WriteAllText("Logs/Warm/exit.txt",code.ToString());}
                    else if(Application.isBatchMode)EditorApplication.Exit(code);
                }
            };
        }
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder+"/checks.txt","Game.unity / Tidal Circuit / scripted desktop motor input\n");
            if (!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))
            {
                Check(false, "disaster configuration validator is unavailable");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Result",1);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        static IEnumerator RunSteps(CastlePlaySession session)
        {
            failed=0;
            var steps=Sequence(session);
            while(true)
            {
                bool next;
                try{next=steps.MoveNext();}
                catch(Exception e){Check(false,e.ToString());break;}
                if(!next)break;
                yield return steps.Current;
            }
            SessionState.SetInt(Key+"Result",failed==0?0:1);
            EditorApplication.ExitPlaymode();
        }
        static void Check(bool valid,string label)
        {
            if(!valid)failed++;
            File.AppendAllText(Folder+"/checks.txt",(valid?"PASS ":"FAIL ")+label+"\n");
        }
        sealed class Driver : ICastleSurvivorDriver
        {
            public Vector3 direction;
            public int samples;
            public CastleBotCommand ReadCommand(CastleSurvivor motor,float dt){samples++;return new CastleBotCommand{move=direction,look=direction};}
        }
        // Only the initial approach is staged. Every waypoint after it uses motor input.
        static IEnumerator FollowRoute(string label,CastleSurvivor player,Driver driver,params Vector3[] points)
        {
            driver.direction=Vector3.zero;
            player.Teleport(points[0]+Vector3.up*.25f,Quaternion.identity);
            yield return new WaitForSeconds(.25f);
            for(int i=1;i<points.Length;i++)
            {
                float end=Time.time+8;
                Vector3 Horizontal() => Vector3.ProjectOnPlane(points[i]-player.transform.position,Vector3.up);
                while(Horizontal().magnitude>.24f && Time.time<end && player.transform.position.y>-.32f)
                {
                    driver.direction=Horizontal().normalized*.62f;
                    yield return null;
                }
                driver.direction=Vector3.zero;
                if(Horizontal().magnitude>.3f || Mathf.Abs(player.transform.position.y-points[i].y)>.52f)
                {
                    Check(false,label+" stopped at waypoint "+i+" feet="+player.transform.position+" target="+points[i]);
                    yield break;
                }
            }
            Check(true,label+" completed "+(points.Length-1)+" waypoints");
        }

        static IEnumerator Sequence(CastlePlaySession session)
        {
            float deadline=Time.realtimeSinceStartup+40;
            while((!session.Survivor()||session.SurvivorBots==null)&&Time.realtimeSinceStartup<deadline)yield return null;
            MatchSetup.MapId="tidal-circuit";MatchSetup.ModeId="demo";MatchSetup.ApplyMap();
            yield return new WaitForSeconds(1);
            var map=Services.World as TidalCircuitMap;
            Check(map,"catalog prefab selected and world registered");
            if(!map)yield break;
            session.SetRole(false);session.SetMenu(false);
            session.SurvivorBots.Clear();session.HandBots.Clear();
            Rigidbody Body(string name)=>map.Blocks.First(b=>b.name==name);
            Check(session.SurvivorBots.SpawnOne(),"spawn scripted driver on survivor motor");
            yield return new WaitForSeconds(.4f);
            var player=session.SurvivorBots.Members.First().GetComponent<CastleSurvivor>();
            var driver=new Driver();
            player.externallyDriven=true;player.InputDriver=driver;
            var hinges=map.GetComponentsInChildren<HingeJoint>();
            string unseated=string.Join(", ",map.Blocks.Where(b=>!b.isKinematic && !b.GetComponent<HingeJoint>()).Select(b=>b.name));
            Check(map.Blocks.Count(b=>!b.isKinematic)==hinges.Length,"intact structure has only seven independently simulated bodies; colliders="+map.GetComponentsInChildren<Collider>().Length+(unseated.Length>0?" unseated=["+unseated+"]":""));
            Check(hinges.Length==7,"seven independent machine bodies; bodies="+map.Blocks.Count+" hinges="+hinges.Length);
            yield return Shot("01-wide", new Vector3(-4,54,-129), new Vector3(0,4.7f,1.4f), true);
            yield return Shot("02-drum", new Vector3(-32,4.45f,-22), Body("Tread drum").position);
            yield return Shot("03-teeters", new Vector3(-31,7.4f,4.5f), Body("Teeter panel 0").position);
            yield return Shot("04-sweeper", new Vector3(22,7.4f,9), Body("Four-arm sweeper").position);
            yield return Shot("05-ramps", new Vector3(31,7.4f,-17), Body("Tipping ramp panel 1").position);
            foreach (var hinge in hinges.Where(h => h && h.useMotor))
            {
                Quaternion before = hinge.transform.rotation;
                yield return new WaitForSeconds(.8f);
                Check(hinge && Quaternion.Angle(before, hinge.transform.rotation) > 3, "driven rotor turns");
            }
            Check(map.GetComponentsInChildren<HingeJoint>().Length == 7, "all seven machines remain seated");
            yield return FollowRoute("West boardwalk", player, driver, new Vector3(-23.7f,1.25f,-22),
                new Vector3(-15.9f,1.4f,-22), new Vector3(-7.3f,1.65f,-20),
                new Vector3(.5f,1.46f,-18.4f), new Vector3(8,1.3f,-19.5f));
            // Confirm physical walkable surfaces at the three tiers before destructive checks.
            var physics = map.gameObject.scene.GetPhysicsScene();
            foreach (var deck in new[] {"Hub lower", "Hub middle", "Hub crown"})
            {
                var body = Body(deck);
                var start = body.position + Vector3.up * .37f;
                Check(physics.Raycast(start, Vector3.down, out var hit, .5f, 1 << Layers.Block,
                    QueryTriggerInteraction.Ignore) && hit.rigidbody == body, deck + " has a usable floor");
            }
            driver.direction=Vector3.zero;
            player.Teleport(map.SafeSpawnPoint(),Quaternion.identity);
            Check(session.SurvivorBots.SpawnOne(),"spawn a survivor bot");
            yield return new WaitForSeconds(1);
            Check(session.SurvivorBots.Count>=1,"bot remains active after spawning");
            var crossing=Body("Midlevel right crossover");
            Vector3 initial=crossing.position;
            Vector3 view=new Vector3(17,9,-13),aim=new Vector3(19.2f,2.7f,-4.5f);
            yield return Shot("06-before",view,aim);
            foreach(var post in map.Blocks.Where(b=>b.name.StartsWith("Right crossover pier post")))
                post.GetComponent<IPushable>().Push(Vector3.forward*12,player.gameObject);
            yield return new WaitForSeconds(.45f);yield return Shot("07-falling",view,aim);
            yield return new WaitForSeconds(1.3f);yield return Shot("08-after",view,aim);
            Check(crossing.position.y<initial.y-.55f,"right crossover falls into channel");
            Check(!Body("Right crossover west section").isKinematic && !Body("Right crossover east section").isKinematic,
                "pier loss releases three separate sections");
            yield return FollowRoute("East shoreline after crossover collapse", player, driver, new Vector3(17.5f,1.25f,-21.8f),
                new Vector3(26.2f,1.43f,-21), new Vector3(40.8f,1.35f,-23.8f));
            var drum=Body("Tread drum");
            Body("Drum bearing -1").GetComponent<IPushable>().Push(Vector3.left*15,player.gameObject);
            yield return new WaitForSeconds(.6f);
            Check(!drum.GetComponent<HingeJoint>() && !drum.isKinematic,"failed bearing disconnects drum motor");
            Check(Body("Four-arm sweeper").TryGetComponent<HingeJoint>(out var intact) && intact.useMotor,"independent sweeper still powered");
            map.Rebuild();yield return new WaitForSeconds(.3f);
            Check(Body("Tread drum").GetComponent<HingeJoint>().useMotor && Body("Midlevel right crossover").isKinematic,"reset restores both routes");
            bool drowned=false;
            void OnEliminated(GameObject actor,GameObject source){if(actor==player.gameObject)drowned=true;}
            GameEvents.Eliminated+=OnEliminated;
            try
            {
                player.Teleport(new Vector3(0,1,-26),Quaternion.identity);
                yield return new WaitForSeconds(2);
                Check(drowned,"survivor falls into an open channel and is eliminated");
            }
            finally{GameEvents.Eliminated-=OnEliminated;}
            map.SetReplica(true);Check(map.Blocks.All(b=>b.isKinematic),"replica suppresses simulation");
            map.SetReplica(false);Check(!Body("Tread drum").isKinematic,"authority resumes");
        }
        static IEnumerator Shot(string name,Vector3 eye,Vector3 target,bool wide=false)
        {
            var camera=new GameObject("Tidal review camera").AddComponent<Camera>();
            camera.transform.position=eye;camera.transform.LookAt(target);camera.fieldOfView=65;
            camera.orthographic=wide;camera.orthographicSize=23f;camera.nearClipPlane=.05f;camera.farClipPlane=500;
            int width=wide?1800:1152,height=wide?680:648;
            var rt=RenderTexture.GetTemporary(width,height,24);
            var old=RenderTexture.active;
            var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;
                // Allow LateUpdate to submit structural instances to this camera.
                yield return null;
                yield return null;
                RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
                File.WriteAllBytes(Path.Combine(Folder,name+".png"),tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(tex);Object.Destroy(camera.gameObject);
            }
        }
    }
}
