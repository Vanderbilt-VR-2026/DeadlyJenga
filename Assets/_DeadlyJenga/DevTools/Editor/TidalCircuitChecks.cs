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
            public CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)=>new CastleBotCommand{move=direction,look=direction};
        }
        // Only the initial approach is staged. Every waypoint after it uses motor input.
        static IEnumerator FollowRoute(string label,CastleSurvivor player,Driver driver,params Vector3[] points)
        {
            driver.direction=Vector3.zero;
            player.Teleport(points[0]+Vector3.up*.08f,Quaternion.identity);
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
            var player=session.Survivor();
            var driver=new Driver();
            player.externallyDriven=true;player.InputDriver=driver;
            var hinges=map.GetComponentsInChildren<HingeJoint>();
            Check(map.Blocks.Count(b=>!b.isKinematic)==hinges.Length,"intact structure has only seven independently simulated bodies; colliders="+map.GetComponentsInChildren<Collider>().Length);
            Check(hinges.Length==7,"seven independent machine bodies; bodies="+map.Blocks.Count);
            Shot("01-wide",new Vector3(-3,27,-50),new Vector3(0,2,1),true);
            Shot("02-drum",new Vector3(-25.8f,3.9f,-11.5f),new Vector3(-26,2,-7));
            Shot("03-teeters",new Vector3(-24.4f,4.1f,4),new Vector3(-27,2.8f,9));
            Shot("04-sweeper",new Vector3(23,4.4f,5),new Vector3(27,3.9f,9));
            Shot("05-ramps",new Vector3(22,4.1f,-12),new Vector3(26,2.8f,-7));
            foreach(var h in hinges.Where(h=>h.useMotor))
            {
                Quaternion before=h.transform.rotation;
                yield return new WaitForSeconds(.8f);
                Check(Quaternion.Angle(before,h.transform.rotation)>3,h.name+" turns under drive");
            }
            foreach(string label in new[]{"Tread drum","Teeter panel 0","Teeter panel 1","Four-arm sweeper","Tipping ramp panel 0","Tipping ramp panel 1","Tipping ramp panel 2"})
            {
                var body=Body(label);
                var p=body.position;
                bool sweep=label=="Four-arm sweeper";
                float x=p.x+(sweep?2.2f:0);
                float length=sweep?3.8f:label=="Tread drum"?2.7f:4;
                float top=sweep?3.3f:label=="Tread drum"?2.85f:3.0f;
                for(int sign=-1;sign<=1;sign+=2)
                {
                    driver.direction=Vector3.zero;
                    player.Teleport(new Vector3(x,top+.08f,p.z+sign*length),Quaternion.LookRotation(-sign*Vector3.forward));
                    yield return new WaitForSeconds(.35f);
                    driver.direction=-sign*Vector3.forward*.65f;
                    float end=Time.time+6;bool rode=false;float maxTilt=0;
                    while(Time.time<end && (player.transform.position.z-p.z)*-sign<length-.15f && player.transform.position.y>.35f)
                    {
                        rode|=player.StandingBody==body;
                        if(body.TryGetComponent<HingeJoint>(out var joint))maxTilt=Mathf.Max(maxTilt,Mathf.Abs(joint.angle));
                        yield return null;
                    }
                    driver.direction=Vector3.zero;
                    Check((player.transform.position.z-p.z)*-sign>=length-.15f && player.transform.position.y>top-.8f && (rode||sweep),
                        label+" direction "+sign+" motor crossing; rode="+rode+" angle="+maxTilt+" end="+player.transform.position);
                }
            }
            yield return FollowRoute("Low perimeter to middle hub",player,driver,new Vector3(13,1.6f,-16),
                new Vector3(13,1.6f,-14),new Vector3(12,3.1f,-10),new Vector3(10.2f,3.1f,-7),
                new Vector3(5,5,-3.8f),new Vector3(4,5,-2));
            yield return FollowRoute("Upper gallery to crown",player,driver,new Vector3(-15,5.5f,10),
                new Vector3(-13,5.5f,9),new Vector3(-3.6f,8.4f,2.8f),new Vector3(0,8.4f,0));
            driver.direction=Vector3.zero;
            player.Teleport(map.SafeSpawnPoint(),Quaternion.identity);
            Check(session.SurvivorBots.SpawnOne(),"spawn a survivor bot");
            yield return new WaitForSeconds(1);
            Check(session.SurvivorBots.Count>=1,"bot remains active after spawning");
            var crossing=Body("Midlevel right crossover");
            Vector3 initial=crossing.position;
            Vector3 view=new Vector3(12,9,-12),aim=new Vector3(13,2.5f,1.5f);
            Shot("06-before",view,aim);
            foreach(var post in map.Blocks.Where(b=>b.name.StartsWith("Right crossover pier post")))
                post.GetComponent<IPushable>().Push(Vector3.forward*12,player.gameObject);
            yield return new WaitForSeconds(.45f);Shot("07-falling",view,aim);
            yield return new WaitForSeconds(1.3f);Shot("08-after",view,aim);
            Check(crossing.position.y<initial.y-.55f,"right crossover falls into channel");
            yield return FollowRoute("Outer bypass after crossover collapse",player,driver,new Vector3(26,1.6f,0),
                new Vector3(28.5f,1.6f,0),new Vector3(32.55f,2.2f,-2.4f),new Vector3(34,2.2f,-3),
                new Vector3(34,2.2f,-13),new Vector3(32.55f,2.2f,-13.6f),new Vector3(28.5f,1.6f,-16),new Vector3(26,1.6f,-16));
            var drum=Body("Tread drum");
            Body("Drum bearing -1").GetComponent<IPushable>().Push(Vector3.left*15,player.gameObject);
            yield return new WaitForSeconds(.6f);
            Check(!drum.GetComponent<HingeJoint>() && !drum.isKinematic,"failed bearing disconnects drum motor");
            Check(Body("Four-arm sweeper").GetComponent<HingeJoint>().useMotor,"independent sweeper still powered");
            map.Rebuild();yield return new WaitForSeconds(.3f);
            Check(Body("Tread drum").GetComponent<HingeJoint>().useMotor && Body("Midlevel right crossover").isKinematic,"reset restores both routes");
            bool drowned=false;
            void OnEliminated(GameObject actor,GameObject source){if(actor==player.gameObject)drowned=true;}
            GameEvents.Eliminated+=OnEliminated;
            try
            {
                player.Teleport(new Vector3(-7,1,-9),Quaternion.identity);
                yield return new WaitForSeconds(2);
                Check(drowned,"survivor falls into an open channel and is eliminated");
            }
            finally{GameEvents.Eliminated-=OnEliminated;}
            map.SetReplica(true);Check(map.Blocks.All(b=>b.isKinematic),"replica suppresses simulation");
            map.SetReplica(false);Check(!Body("Tread drum").isKinematic,"authority resumes");
        }
        static void Shot(string name,Vector3 eye,Vector3 target,bool wide=false)
        {
            var camera=new GameObject("Tidal review camera").AddComponent<Camera>();
            camera.transform.position=eye;camera.transform.LookAt(target);camera.fieldOfView=65;
            camera.orthographic=wide;camera.orthographicSize=15.8f;camera.nearClipPlane=.05f;camera.farClipPlane=500;
            int width=wide?1800:1152,height=wide?680:648;
            var rt=RenderTexture.GetTemporary(width,height,24);
            var old=RenderTexture.active;
            var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
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
