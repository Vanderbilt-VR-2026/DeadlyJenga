using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastleBots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class FossilBasinChecks
    {
        const string Key="FossilBasinChecks.Active", Folder="Logs/Captures/FossilBasin";
        static int phase;
        static float clock;
        static double deadline;
        static FossilBasinMap map;
        static Vector3[] initial;
        static TestDummy dummy;
        static FossilMember pulled;
        static Camera camera;
        static Vector3 grab,footBefore;
        static Rigidbody eastRefuge;
        static Vector3 eastBefore;
        static Rigidbody foot;
        static string report;
        static bool valid;
        static System.Collections.IEnumerator peer;
        static CastleSurvivor walker;
        static Vector3 destination;
        static float lastWalk;
        static int walkStage;
        static CastleBotBrain routeBot;
        static bool botArrived;
        static readonly Vector3 BotGoal=new Vector3(-13,3.06f,-4);
        static void CheckClimb()
        {
            // Start a measured hand-span from an actual Stegosaurus leg face.
            var femur=map.Blocks.First(b=>b.name=="Stegosaurus upper leg -1");
            var col=femur.GetComponentInChildren<BoxCollider>();
            var ray=new Ray(col.bounds.center+Vector3.back*3,Vector3.forward);
            if(!col.Raycast(ray,out var hit,6)){Check(false,"bone face ray");return;}
            var outward=Vector3.ProjectOnPlane(hit.normal,Vector3.up).normalized;
            var feet=hit.point+outward*(walker.Capsule.radius+.065f)-Vector3.up*(walker.Height*.64f);
            walker.Place(feet,Quaternion.LookRotation(-outward));
            walker.InputSuspended=false;
            bool latched=false;float best=feet.y;
            for(int i=0;i<50;i++)
            {
                walker.Simulate(Vector2.up,false,.02f,true);
                latched|=walker.Climbing;
                if(walker.Climbing)best=Mathf.Max(best,walker.transform.position.y);
            }
            Check(latched&&best>feet.y+.12f,"survivor latched and climbed bone face; rise "+(best-feet.y));
        }
        static FossilBasinChecks(){EditorApplication.playModeStateChanged+=State;}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Start in Edit Mode");
            Directory.CreateDirectory(Folder);
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                MatchSetup.MapId="fossil-basin";MatchSetup.ModeId="demo";MatchSetup.FromLobby=false;
                phase=0;valid=true;report="";deadline=EditorApplication.timeSinceStartup+120;
                EditorApplication.update+=Step;
            }
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            SessionState.EraseBool(Key);
            int code=SessionState.GetInt(Key+".result",2);
            if(string.IsNullOrEmpty(SessionState.GetString("DJWarm.id","")))EditorApplication.Exit(code);
            else {Directory.CreateDirectory("Logs/Warm");File.WriteAllText("Logs/Warm/exit.txt",code.ToString());}
        }
        static void Check(bool ok,string label){report+=(ok?"PASS ":"FAIL ")+label+"\n";valid&=ok;}
        static void End()
        {
            File.WriteAllText(Folder+"/checks.txt",report);
            Debug.Log(report);
            SessionState.SetInt(Key+".result",valid?0:2);
            EditorApplication.update-=Step;EditorApplication.ExitPlaymode();
        }
        static void Shot(string name,Vector3 eye,Vector3 look,float fov)
        {
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(look-eye));camera.fieldOfView=fov;
            MapLiveCapture.RenderNow(camera,1440,960,Folder+"/"+name+".png");
        }
        static void Wide(string name)=>Shot(name,new Vector3(-.5f,12.6f,-41),new Vector3(0,7.6f,0),32);
        static void Step()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("phase "+phase);
                if(Services.Session?.SurvivorBots==null)return;
                if(phase==0)
                {
                    Services.Session.HandBots?.Clear();Services.Session.SurvivorBots.AutoFill=false;Services.Session.SurvivorBots.Clear();
                    MatchSetup.ApplyMap();map=Services.World as FossilBasinMap;
                    if(!map)throw new InvalidOperationException("Game did not select Fossil Basin");
                    initial=map.Blocks.Select(b=>b.position).ToArray();
                    report+="Rigid members "+initial.Length+", bearing shapes "+map.GetComponentsInChildren<BoxCollider>().Length+", joints "+map.GetComponentsInChildren<Joint>().Length+"\n";
                    camera=new GameObject("Fossil Basin review camera").AddComponent<Camera>();
                    camera.enabled=false;camera.farClipPlane=400;camera.nearClipPlane=.05f;
                    clock=Time.fixedTime;phase++;return;
                }
                float dt=Time.fixedTime-clock;
                if(routeBot && routeBot.Motor.Grounded && Vector3.Distance(routeBot.transform.position,BotGoal)<1.8f)botArrived=true;
                if(phase==1&&dt>.05f)
                {
                    var all=map.GetComponentsInChildren<FixedJoint>();
                    var gap=all.Select(j=>Vector3.Distance(j.transform.TransformPoint(j.anchor),j.connectedBody.transform.TransformPoint(j.connectedAnchor))).DefaultIfEmpty(0).Max();
                    report+="First physics frames: joints "+all.Length+", largest socket gap "+gap+"\n";
                    Wide("01-authored");phase++;return;
                }
                if(phase==2&&dt>3)
                {
                    float worst=map.Blocks.Select((b,i)=>Vector3.Distance(b.position,initial[i])).Max();
                    Check(worst<.7f,"unloaded maximum body travel "+worst);
                    foreach(var item in map.Blocks.Select((b,i)=>(b,travel:Vector3.Distance(b.position,initial[i]))).OrderByDescending(x=>x.travel).Take(8))
                        report+=item.b.name+" travel "+item.travel+" at "+item.b.position+"\n";
                    Wide("02-settled");
                    Shot("03-apron-eye",new Vector3(0,2.1f,-9),new Vector3(-4,7,0),70);
                    Shot("04-rib-eye",new Vector3(-11,12.5f,11),new Vector3(2,12,3),70);
                    var spawn=map.SafeSpawnPoint();
                    Check(Physics.Raycast(spawn+Vector3.up*.25f,Vector3.down,out var hit,.7f,1<<Layers.Block)&&hit.normal.y>.86f,"spawn has bearing");
                    Check(!Physics.CheckCapsule(spawn+Vector3.up*.22f,spawn+Vector3.up*1.05f,.20f,1<<Layers.Block),"spawn capsule clearance");
                    var plate=map.Blocks.First(b=>b.name=="Stegosaurus vertebra and ribs 4");
                    Check(Climbables.TryGet(plate.GetComponentInChildren<Collider>(),out var grip)&&grip.CanClimb&&grip.Body==plate,"bone resolves to moving climbing anchor");
                    var bots=Services.Session.SurvivorBots;bots.Running=true;
                    var bot=bots.SpawnOne();Check(bot!=null,"game spawned survivor bot");
                    if(bot)
                    {
                        routeBot=bot.GetComponent<CastleBotBrain>();botArrived=false;
                        if(routeBot)
                        {
                            routeBot.Motor.Place(new Vector3(-8.4f,1.08f,-9.1f),Quaternion.identity);
                            routeBot.SetJourneyDestination(BotGoal,40);
                        }
                    }
                    var probe=GameObject.CreatePrimitive(PrimitiveType.Sphere);probe.name="Tar probe";probe.transform.position=new Vector3(19,.85f,0);
                    probe.transform.localScale=Vector3.one*.3f;probe.AddComponent<Rigidbody>();dummy=probe.AddComponent<TestDummy>();
                    walker=Services.Session.Player?.gameObject.GetComponent<CastleSurvivor>();
                    if(walker)
                    {
                        walker.externallyDriven=true;
                        walker.Place(new Vector3(7,1.12f,-9),Quaternion.identity);
                        destination=new Vector3(9,3,-2);walkStage=0;
                        phase=20;lastWalk=clock=Time.fixedTime;return;
                    }
                    Check(false,"survivor motor available");clock=Time.fixedTime;phase++;return;
                }
                if(phase==20)
                {
                    var flat=Vector3.ProjectOnPlane(destination-walker.transform.position,Vector3.up);
                    var forward=walker.MovementForward;
                    var input=new Vector2(Vector3.Dot(flat.normalized,Vector3.Cross(Vector3.up,forward)),Vector3.Dot(flat.normalized,forward));
                    float elapsed=Time.fixedTime-lastWalk;lastWalk=Time.fixedTime;
                    while(elapsed>.001f){float step=Mathf.Min(.025f,elapsed);walker.Simulate(input,false,step);elapsed-=step;}
                    if(flat.magnitude>.55f && dt<10)return;
                    Check(flat.magnitude<=.55f && Mathf.Abs(walker.transform.position.y-destination.y)<.45f,
                        "real survivor walked flight "+walkStage+" endpoint "+walker.transform.position);
                    if(walkStage++==0){destination=new Vector3(14,4.9f,2);clock=Time.fixedTime;return;}
                    CheckClimb();walker.externallyDriven=false;clock=Time.fixedTime;phase=21;return;
                }
                if(phase==21)
                {
                    if(!botArrived&&routeBot&&dt<12)return;
                    Check(botArrived,"actual bot crossed apron to west landing; last position "+(routeBot?routeBot.transform.position.ToString():"missing"));
                    phase=3;clock=Time.fixedTime;return;
                }
                if(phase==3&&dt>1.5f)
                {
                    Check(dummy.Health==0,"tar eliminated falling damageable");
                    foot=map.Blocks.First(b=>b.name.StartsWith("Rex tarsals"));
                    footBefore=foot.position;
                    eastRefuge=map.Blocks.First(b=>b.name.StartsWith("East switchback deck"));
                    eastBefore=eastRefuge.position;
                    float loaded=map.Blocks.Select((body,i)=>Vector3.Distance(body.position,initial[i])).Max();
                    Check(loaded<.75f,"intact stability after player and bot loading; worst travel "+loaded);
                    pulled=map.Blocks.First(b=>b.name=="Rex near ankle pier -1/-1/2").GetComponent<FossilMember>();
                    grab=pulled.Body.worldCenterOfMass;
                    Check(pulled.TryStartGrab(camera.gameObject,grab),"grab top near-ankle bearing");
                    clock=Time.fixedTime;phase++;return;
                }
                if(phase==4)
                {
                    pulled.Hold(grab+Vector3.left*Mathf.Min(5,dt*6));
                    if(dt<1)return;
                    pulled.Release();
                    Check(Vector3.Distance(grab,pulled.Body.worldCenterOfMass)>.8f,"single pier physically withdrawn");
                    Wide("05-pull");
                    foreach(var b in map.Blocks.Where(b=>b.name.StartsWith("Rex near ankle pier")&&b!=pulled.Body))
                        b.GetComponent<FossilMember>().Push(Vector3.left*10,camera.gameObject);
                    clock=Time.fixedTime;phase++;return;
                }
                if(phase==5&&dt>2)
                {
                    Wide("06-foot-loss");
                    Check(footBefore.y-foot.position.y>.3f,"foot drop after loss of bearings "+(footBefore.y-foot.position.y));
                    Check(eastRefuge.gameObject.activeSelf && Vector3.Distance(eastRefuge.position,eastBefore)<.30f,
                        "independent eastern route survives western pier loss");
                    File.WriteAllText(Folder+"/desktop.txt",report);
                    peer=FossilBasinPeer.Receive(map,(CastlePlaySession)Services.Session,Check);
                    deadline=EditorApplication.timeSinceStartup+130;phase=6;return;
                }
                if(phase==6&&!peer.MoveNext()){(peer as IDisposable)?.Dispose();End();}
            }
            catch(Exception e){Check(false,e.ToString());End();}
        }
    }
}
