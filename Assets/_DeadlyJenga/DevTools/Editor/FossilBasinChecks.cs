using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
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
        static Rigidbody foot;
        static string report;
        static bool valid;
        static System.Collections.IEnumerator peer;
        static CastleSurvivor walker;
        static Vector3 destination;
        static float lastWalk;
        static int walkStage;
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
        static void Wide(string name)=>Shot(name,new Vector3(-.5f,16,-42),new Vector3(0,7,0),32);
        static void Step()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("phase "+phase);
                if(Services.Session?.SurvivorBots==null)return;
                if(phase==0)
                {
                    MatchSetup.ApplyMap();map=Services.World as FossilBasinMap;
                    if(!map)throw new InvalidOperationException("Game did not select Fossil Basin");
                    initial=map.Blocks.Select(b=>b.position).ToArray();
                    camera=new GameObject("Fossil Basin review camera").AddComponent<Camera>();
                    camera.enabled=false;camera.farClipPlane=400;camera.nearClipPlane=.05f;
                    clock=Time.fixedTime;phase++;return;
                }
                float dt=Time.fixedTime-clock;
                if(phase==1&&dt>.05f)
                {
                    Wide("01-authored");phase++;return;
                }
                if(phase==2&&dt>3)
                {
                    float worst=map.Blocks.Select((b,i)=>Vector3.Distance(b.position,initial[i])).Max();
                    Check(worst<.7f,"unloaded maximum body travel "+worst);
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
                    if(bot)report+="Bot at "+bot.transform.position+"\n";
                    var probe=GameObject.CreatePrimitive(PrimitiveType.Sphere);probe.name="Tar probe";probe.transform.position=new Vector3(19,.85f,0);
                    probe.transform.localScale=Vector3.one*.3f;probe.AddComponent<Rigidbody>();dummy=probe.AddComponent<TestDummy>();
                    walker=Services.Session.Player?.gameObject.GetComponent<CastleSurvivor>();
                    if(walker)
                    {
                        walker.externallyDriven=true;
                        walker.Place(new Vector3(0,1.12f,-9),Quaternion.identity);
                        destination=new Vector3(9,3,-4);walkStage=0;
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
                    walker.externallyDriven=false;clock=Time.fixedTime;phase=3;return;
                }
                if(phase==3&&dt>1.5f)
                {
                    Check(dummy.Health==0,"tar eliminated falling damageable");
                    foot=map.Blocks.First(b=>b.name.StartsWith("Rex tarsals"));
                    footBefore=foot.position;
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
